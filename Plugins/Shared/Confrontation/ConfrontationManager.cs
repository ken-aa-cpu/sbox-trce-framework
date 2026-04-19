using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trce.Kernel.Net;
using Trce.Plugins.Shared.Evidence;
using Trce.Kernel.Plugin;
using Trce.Kernel.Plugin.Services;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Shared.Confrontation
{
	/// <summary>
	///   / Confrontation State Machine
	/// </summary>
	public enum ConfrontationState
	{
		Inactive,
		CardPlay,
		Voting,
		Result
	}

	/// <summary>
	///   / Card entry played by a player
	/// </summary>
	public class PlayedCardEntry
	{
		public ulong PlayerSteamId { get; set; }
		public Trce.Plugins.Shared.Evidence.FactCard Card { get; set; }
		public float PlayedTime { get; set; }
	}

	/// <summary>
	///   / Confrontation Manager Plugin
	/// </summary>
	[TrcePlugin(
		Id = "trce.shared.confrontation",
		Name = "TRCE Confrontation System",
		Version = "1.0.0",
		Depends = new[] { "trce.gamestate.phase", "trce.death" }
	)]
	public class ConfrontationManager : TrcePlugin, IConfrontationService
	{

		[Sync] public ConfrontationState CurrentState { get; private set; } = ConfrontationState.Inactive;
		[Sync] public double StateStartTime { get; private set; }
		[Sync] public int CurrentThreshold { get; private set; }

		[Property] public float CardPlayDuration { get; set; } = 20f;
		[Property] public float VoteDuration { get; set; } = 10f;
		[Property] public string VoteEffect { get; set; } = "seal";
		[Property] public string TieBreaker { get; set; } = "skip";
		[Property] public bool GhostCanVote { get; set; } = true;

		public Action<int> OnConfrontationStarted;
		public Action<int> OnVotingStarted;
		public event Action<ulong, int, string> OnConfrontationResult;
		public Action OnConfrontationEnded;

		private List<PlayedCardEntry> playedCards = new();

		private Dictionary<ulong, ulong> votes = new();

		private Trce.Kernel.Bridge.SandboxBridge _bridge;
		private Trce.Kernel.Bridge.SandboxBridge Bridge => _bridge ??= SandboxBridge.Instance;

		protected override Task OnPluginEnabled()
		{
			// P0-2: Register as IConfrontationService so consumers resolve via interface.
			TrceServiceManager.Instance?.RegisterService<IConfrontationService>( this );
			return Task.CompletedTask;
		}

		protected override void OnFixedUpdate()
		{
			if ( !(Bridge?.IsServer ?? false) ) return;
			if ( CurrentState == ConfrontationState.Inactive ) return;

			float elapsed = (float)(Time.Now - StateStartTime);

			switch ( CurrentState )
			{
				case ConfrontationState.CardPlay:
					if ( elapsed >= CardPlayDuration )
						TransitionToVoting();
					break;

				case ConfrontationState.Voting:
					if ( elapsed >= VoteDuration )
						TransitionToResult();
					break;

				case ConfrontationState.Result:
					if ( elapsed >= 5f )
						EndConfrontation();
					break;
			}
		}

		// ====================================================================
		// Lifecycle Control
		// ====================================================================

		public void StartConfrontation( int threshold )
		{
			if ( !(Bridge?.IsServer ?? false) ) return;
			if ( CurrentState != ConfrontationState.Inactive ) return;

			CurrentThreshold = threshold;
			playedCards.Clear();
			votes.Clear();

			CurrentState = ConfrontationState.CardPlay;
			StateStartTime = Time.NowDouble;

			Log.Info( $"[Confrontation:{GameObject.Name}] Confrontation Started (Threshold: {threshold}%)" );

			// P0-1/P0-2: Use IGamePhaseService instead of Scene.GetAllComponents<GamePhaseManager>()
			GetService<IGamePhaseService>()?.EnterConfrontation();

			OnConfrontationStarted?.Invoke( threshold );
		}

		private void TransitionToVoting()
		{
			CurrentState = ConfrontationState.Voting;
			StateStartTime = Time.NowDouble;

			Log.Info( $"[Confrontation:{GameObject.Name}] Transition to Voting ({playedCards.Count} cards played)" );

			OnVotingStarted?.Invoke( playedCards.Count );
		}

		private void TransitionToResult()
		{
			CurrentState = ConfrontationState.Result;
			StateStartTime = Time.NowDouble;

			var voteCount = new Dictionary<ulong, int>();
			foreach ( var kvp in votes )
			{
				if ( !voteCount.ContainsKey( kvp.Value ) )
					voteCount[kvp.Value] = 0;
				voteCount[kvp.Value]++;
			}

			if ( voteCount.Count == 0 )
			{
				Log.Info( $"[Confrontation:{GameObject.Name}] No votes cast, skipping." );
				OnConfrontationResult?.Invoke( 0, 0, "skip" );
				return;
			}

			int maxVotes = voteCount.Values.Max();
			var topTargets = voteCount.Where( kvp => kvp.Value == maxVotes )
				.Select( kvp => kvp.Key ).ToList();

			ulong target;

			if ( topTargets.Count > 1 )
			{
				switch ( TieBreaker )
				{
					case "random":
						target = Scene.Get<TrceRNG>()?.PickRandom( topTargets ) ?? topTargets[0];
						break;
					case "skip":
					default:
						Log.Info( $"[Confrontation:{GameObject.Name}] Vote tied, skipping." );
						OnConfrontationResult?.Invoke( 0, 0, "tie" );
						return;
				}
			}
			else
			{
				target = topTargets[0];
			}

			Log.Info( $"[Confrontation:{GameObject.Name}] Final Result: {target} ({maxVotes} votes), Effect: {VoteEffect}" );

			switch ( VoteEffect )
			{
				case "execute":
					// P0-2: Use IDeathManagerService instead of Scene.GetAllComponents<DeathManager>()
					GetService<IDeathManagerService>()?.IsDeadOrGone( target ); // existence check
					GetService<IDeathManagerService>()?.ProcessExecution( target );
					break;

				case "seal":
				default:
					break;
			}

			OnConfrontationResult?.Invoke( target, maxVotes, VoteEffect );
		}

		private void EndConfrontation()
		{
			CurrentState = ConfrontationState.Inactive;

			Log.Info( $"[Confrontation:{GameObject.Name}] Confrontation Ended, Resuming Task Phase." );

			// P0-1/P0-2: Use IGamePhaseService instead of Scene.GetAllComponents<GamePhaseManager>()
			GetService<IGamePhaseService>()?.ResumeTaskPhase();

			OnConfrontationEnded?.Invoke();
		}

		// ====================================================================
		// Player Interaction
		// ====================================================================

		public void PlayCard( ulong steamId, string cardId )
		{
			if ( !(Bridge?.IsServer ?? false) ) return;
			if ( CurrentState != ConfrontationState.CardPlay ) return;

			if ( playedCards.Any( p => p.PlayerSteamId == steamId ) ) return;

			var card = Scene.GetAllComponents<EvidenceCollector>().FirstOrDefault()?.GetPlayerCards( steamId )
				?.FirstOrDefault( c => c.CardId == cardId && !c.IsPlayed );

			if ( card == null ) return;

			card.IsPlayed = true;
			playedCards.Add( new PlayedCardEntry
			{
				PlayerSteamId = steamId,
				Card = card,
				PlayedTime = Time.Now
			} );

			Log.Info( $"[Confrontation:{GameObject.Name}] Player {steamId} played card: {card.Type} ({cardId})" );
		}

		public void CastVote( ulong voterSteamId, ulong targetSteamId )
		{
			if ( !(Bridge?.IsServer ?? false) ) return;
			if ( CurrentState != ConfrontationState.Voting ) return;

			if ( !GhostCanVote && GetService<IDeathManagerService>()?.IsDeadOrGone( voterSteamId ) == true )
				return;

			if ( voterSteamId == targetSteamId ) return;

			votes[voterSteamId] = targetSteamId;

			Log.Info( $"[Confrontation:{GameObject.Name}] Voter {voterSteamId} cast vote for {targetSteamId}" );
		}

		// ====================================================================
		//  Queries
		// ====================================================================

		public float GetTimeRemaining()
		{
			float duration = CurrentState switch
			{
				ConfrontationState.CardPlay => CardPlayDuration,
				ConfrontationState.Voting => VoteDuration,
				ConfrontationState.Result => 5f,
				_ => 0f
			};
			return (float)Math.Max( 0, duration - ( Time.NowDouble - StateStartTime ) );
		}
	}
}


