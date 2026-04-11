// ====================================================================
// ╔══════════════════════════════════════════════════════════════════╗
// ║  TRCE FRAMEWORK — PROPRIETARY SOURCE CODE                       ║
// ║  Copyright (c) 2026 TRCE Team. All rights reserved.            ║
// ║  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ║
// ╚══════════════════════════════════════════════════════════════════╝
// ====================================================================

using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Plugin;
using Trce.Plugins.GameState;
using Trce.Plugins.Combat;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Shared.Evidence
{
	/// <summary>
	///   / Evidence Collector System
	///   / Generates Fact Cards based on player behavior (Co-location, Task completion, etc.)
	/// </summary>
	[TrcePlugin(
		Id = "trce.evidence",
		Name = "TRCE Evidence System",
		Version = "1.0.0",
		Author = "TRCE Team"
	)]
	public class EvidenceCollector : TrcePlugin
	{
		private Dictionary<ulong, List<FactCard>> playerCards = new();

		private Dictionary<string, float> coLocationTracking = new();

		[Property, Description( "Seconds needed for co-location fact generation" )]
		public float CoLocationThreshold { get; set; } = 15f;

		[Property, Description( "Max cards shown in confrontation for each player" )]
		public int MaxCardsPerPlayer { get; set; } = 3;

		public Action<ulong, FactCard> OnFactCardGenerated;

		protected override void OnStart()
		{
			var taskTracker = Scene.Get<TaskProgressTracker>();
			if ( taskTracker != null )
				taskTracker.OnTaskCompleted += HandleTaskCompleted;

			var deathManager = Scene.Get<DeathManager>();
			if ( deathManager != null )
				deathManager.OnPlayerKilled += HandlePlayerKilled;

			var roundLifecycle = Scene.Get<RoundLifecycle>();
			if ( roundLifecycle != null )
				roundLifecycle.OnRoundStarted += ( _ ) => ResetAll();
		}

		protected override void OnPluginDisabled()
		{
			var taskTracker = Scene.Get<TaskProgressTracker>();
			if ( taskTracker != null )
				taskTracker.OnTaskCompleted -= HandleTaskCompleted;

			var deathManager = Scene.Get<DeathManager>();
			if ( deathManager != null )
				deathManager.OnPlayerKilled -= HandlePlayerKilled;
		}

		// ====================================================================
		// Card Generation
		// ====================================================================

		public void AddCard( ulong steamId, FactCard card )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;

			if ( !playerCards.ContainsKey( steamId ) )
				playerCards[steamId] = new List<FactCard>();

			playerCards[steamId].Add( card );

			Log.Info( $"[Evidence] Generated card for player {steamId}: {card.Type} ({card.CardId})" );

			OnFactCardGenerated?.Invoke( steamId, card );
		}

		public void TrackCoLocation( ulong playerA, ulong playerB, string zone )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			if ( playerA == playerB ) return;

			ulong first = Math.Min( playerA, playerB );
			ulong second = Math.Max( playerA, playerB );
			string key = $"{first}:{second}:{zone}";

			if ( !coLocationTracking.ContainsKey( key ) )
			{
				coLocationTracking[key] = Time.Now;
				return;
			}

			float startTime = coLocationTracking[key];
			float duration = Time.Now - startTime;

			if ( duration >= CoLocationThreshold )
			{
				AddCard( playerA, FactCard.CreateCoLocation( playerA, playerB, zone, duration, Time.Now ) );
				AddCard( playerB, FactCard.CreateCoLocation( playerB, playerA, zone, duration, Time.Now ) );

				coLocationTracking[key] = Time.Now;
			}
		}

		public void EndCoLocation( ulong playerA, ulong playerB, string zone )
		{
			ulong first = Math.Min( playerA, playerB );
			ulong second = Math.Max( playerA, playerB );
			coLocationTracking.Remove( $"{first}:{second}:{zone}" );
		}

		// ====================================================================
		//  Queries
		// ====================================================================

		public List<FactCard> GetPlayerCards( ulong steamId )
		{
			return playerCards.TryGetValue( steamId, out var cards )
				? new List<FactCard>( cards ) : new List<FactCard>();
		}

		public List<FactCard> GetPlayableCards( ulong steamId )
		{
			return GetPlayerCards( steamId )
				.Where( c => !c.IsPlayed )
				.OrderByDescending( c => c.Timestamp )
				.Take( MaxCardsPerPlayer )
				.ToList();
		}

		public void MarkCardPlayed( ulong steamId, string cardId )
		{
			if ( playerCards.TryGetValue( steamId, out var cards ) )
			{
				var card = cards.FirstOrDefault( c => c.CardId == cardId );
				if ( card != null ) card.IsPlayed = true;
			}
		}

		public void ResetAll()
		{
			playerCards.Clear();
			coLocationTracking.Clear();
		}

		// ====================================================================
		// Event Handlers
		// ====================================================================

		private void HandleTaskCompleted( ulong steamId, string taskId, string location )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			if ( steamId > 0 )
				AddCard( steamId, FactCard.CreateTaskCompleted( steamId, taskId, location, Time.Now ) );
		}

		private void HandlePlayerKilled( ulong victimId, ulong attackerId )
		{
			// Player killed logic for evidence if needed
		}
	}
}

