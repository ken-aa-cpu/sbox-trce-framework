using System.Threading.Tasks;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Bridge;
using Trce.Kernel.Net;
using Trce.Kernel.Security;
using Trce.Kernel.Plugin;
using Trce.Kernel.Plugin.Services;

namespace Trce.Plugins.GameState
{
	/// <summary>
	///   / Game Phase Enumeration
	/// </summary>
	public enum GamePhase
	{
		Lobby,
		Intro,
		TaskPhase,
		Confrontation,
		ArmoryUnlocked,
		HuntPhase,
		EndRound
	}

	/// <summary>
	///   / Game Phase Manager State Machine
	///   / Handles phase transitions, synchronization, and timing.
	/// </summary>
	[TrcePlugin(
		Id = "trce.gamestate.phase",
		Name = "TRCE Game Phase Manager",
		Version = "1.0.0",
		Author = "TRCE Team"
	)]
	[Icon( "hourglass_empty" )]
	public class GamePhaseManager : TrcePlugin, IGamePhaseService
	{
		// Explicit event declarations to satisfy IGamePhaseService contract
		public event Action<GamePhaseEnum, GamePhaseEnum, float> OnPhaseChanged;

		public event Action<string, string> OnRoundEnded;

		// ====================================================================
		//  Sync State
		// ====================================================================

		// IGamePhaseService exposes CurrentPhase typed as GamePhaseEnum.
		// The internal GamePhase enum is kept for backward compatibility within the Plugins.GameState namespace.
		[Sync(SyncFlags.FromHost), Property]
		public GamePhase CurrentPhase { get; private set; } = GamePhase.Lobby;

		// IGamePhaseService explicit implementation
		GamePhaseEnum IGamePhaseService.CurrentPhase => (GamePhaseEnum)(int)CurrentPhase;

		void IGamePhaseService.SwitchPhase( GamePhaseEnum newPhase, float duration )
			=> SwitchPhase( (GamePhase)(int)newPhase, duration );

		[Sync(SyncFlags.FromHost)]
		public double PhaseStartTime { get; private set; }

		[Sync(SyncFlags.FromHost)]
		public float PhaseDuration { get; private set; }

		[Sync(SyncFlags.FromHost)]
		public string PhaseFingerprint { get; private set; }

		// ====================================================================
		// Configuration
		// ====================================================================

		[Property] public float IntroDuration { get; set; } = 8f;
		[Property] public float TaskPhaseDuration { get; set; } = 360f;
		[Property] public float ConfrontationDuration { get; set; } = 30f;
		[Property] public float HuntPhaseDuration { get; set; } = 180f;
		[Property] public int MinPlayersToStart { get; set; } = 2;

		private string roundSecret;
		private TimeSince timeSinceFingerprintUpdate = 0;

		private SandboxBridge _bridge;
		private SandboxBridge Bridge => _bridge ??= SandboxBridge.Instance;

		// ====================================================================
		//  Lifecycle
		// ====================================================================

		protected override async Task OnPluginEnabled()
		{
			// P0-1: Register as IGamePhaseService so all consumers use the interface.
			TrceServiceManager.Instance?.RegisterService<IGamePhaseService>( this );
			await Task.CompletedTask;
		}

		protected override void OnStart()
		{
			var taskTracker = GetService<ITaskProgressService>();
			if ( taskTracker != null )
			{
				taskTracker.OnProgressReached100 += OnProgressReached100;
			}

			var deathManager = GetService<IDeathManagerService>();
			if ( deathManager != null )
			{
				deathManager.OnAllKillersDead += () => EndRound( "crew", "KillersEliminated" );
				deathManager.OnAllCrewDead += () => EndRound( "killer", "CrewEliminated" );
				deathManager.OnAllCrewEvacuated += () => EndRound( "crew", "Evacuated" );
			}

			if ( (Bridge?.IsServer ?? false) )
			{
				roundSecret = HmacSigner.GenerateRoundSecret();
				Log.Info( $"[PhaseManager:{GameObject.Name}] Server initialized." );
			}
		}

		protected override void OnPluginDisabled()
		{
			var taskTracker = GetService<ITaskProgressService>();
			if ( taskTracker != null )
			{
				taskTracker.OnProgressReached100 -= OnProgressReached100;
			}
		}

		protected override void OnFixedUpdate()
		{
			if ( !(Bridge?.IsServer ?? false) ) return;

			if ( PhaseDuration > 0 )
			{
				float remaining = GetTimeRemaining();
				if ( remaining <= 0 )
				{
					OnPhaseTimeExpired();
				}
			}

			if ( timeSinceFingerprintUpdate > 5f )
			{
				timeSinceFingerprintUpdate = 0;
				UpdateFingerprint();
			}
		}

		// ====================================================================
		// Transitions
		// ====================================================================

		public void SwitchPhase( GamePhase newPhase, float duration = 0f )
		{
			if ( !(Bridge?.IsServer ?? false) ) return;

			var oldPhase = CurrentPhase;
			CurrentPhase = newPhase;
			PhaseDuration = duration;
			PhaseStartTime = Time.NowDouble;
			UpdateFingerprint();

			Log.Info( $"[PhaseManager] Phase Transition: {oldPhase} -> {newPhase} ({duration}s)" );

			string phaseName = newPhase switch
			{
				GamePhase.Lobby          => "&bLobby",
				GamePhase.Intro          => "&ePreparation",
				GamePhase.TaskPhase      => "&aTasks",
				GamePhase.Confrontation  => "&cConfrontation",
				GamePhase.ArmoryUnlocked => "&6Armory Unlocked",
				GamePhase.HuntPhase      => "&4&lHunt Phase!",
				GamePhase.EndRound       => "&dRound Ended",
				_                        => newPhase.ToString()
			};
			GetPlugin<Social.ChatManager>()?.SendSystemMessage( $"&rPhase Transition -> {phaseName}" );

			OnPhaseChanged?.Invoke( (GamePhaseEnum)(int)oldPhase, (GamePhaseEnum)(int)newPhase, duration );
		}

		public void StartGame()
		{
			if ( !(Bridge?.IsServer ?? false) ) return;
			if ( CurrentPhase != GamePhase.Lobby ) return;

			Log.Info( "[PhaseManager] Game Starting!" );
			SwitchPhase( GamePhase.Intro, IntroDuration );
		}

		[ConCmd( "trce_admin_start", Help = "Force start TRCE round" )]
		public static void CmdAdminStart()
		{
			// P0-1: Use IGamePhaseService via TrceServiceManager instead of Scene.Get<GamePhaseManager>()
			var manager = TrceServiceManager.Instance?.GetService<IGamePhaseService>();
			if ( manager != null )
			{
				manager.StartGame();
			}
			else
			{
				Log.Warning( "IGamePhaseService not found — ensure GamePhaseManager plugin is active." );
			}
		}

		public void EnterConfrontation()
		{
			if ( !(Bridge?.IsServer ?? false) ) return;
			if ( CurrentPhase != GamePhase.TaskPhase ) return;

			SwitchPhase( GamePhase.Confrontation, ConfrontationDuration );
		}

		public void ResumeTaskPhase()
		{
			if ( !(Bridge?.IsServer ?? false) ) return;
			if ( CurrentPhase != GamePhase.Confrontation ) return;

			SwitchPhase( GamePhase.TaskPhase, TaskPhaseDuration );
		}

		private void OnProgressReached100()
		{
			if ( !(Bridge?.IsServer ?? false) ) return;
			if ( CurrentPhase != GamePhase.TaskPhase ) return;

			Log.Info( "[PhaseManager] Progress reached 100% - Entering Endgame!" );
			SwitchPhase( GamePhase.ArmoryUnlocked, 5f );
		}

		public void EnterEndgame()
		{
			OnProgressReached100();
		}

		public void EnterHuntPhase()
		{
			if ( !(Bridge?.IsServer ?? false) ) return;
			SwitchPhase( GamePhase.HuntPhase, HuntPhaseDuration );
		}

		public void EndRound( string winner, string reason )
		{
			if ( !(Bridge?.IsServer ?? false) ) return;
			SwitchPhase( GamePhase.EndRound, 15f );
			OnRoundEnded?.Invoke( winner, reason );
		}

		// ====================================================================
		//  Queries
		// ====================================================================

		public float GetTimeRemaining()
		{
			if ( PhaseDuration <= 0 ) return float.MaxValue;
			double elapsed = Time.NowDouble - PhaseStartTime;
			return (float)Math.Max( 0, PhaseDuration - elapsed );
		}

		public double GetTimeElapsed()
		{
			return Time.NowDouble - PhaseStartTime;
		}

		private void OnPhaseTimeExpired()
		{
			switch ( CurrentPhase )
			{
				case GamePhase.Intro:
					SwitchPhase( GamePhase.TaskPhase, TaskPhaseDuration );
					break;

				case GamePhase.TaskPhase:
					EndRound( "killer", "TaskTimeExpired" );
					break;

				case GamePhase.Confrontation:
					ResumeTaskPhase();
					break;

				case GamePhase.ArmoryUnlocked:
					EnterHuntPhase();
					break;

				case GamePhase.HuntPhase:
					EndRound( "killer", "EvacTimeExpired" );
					break;

				case GamePhase.EndRound:
					SwitchPhase( GamePhase.Lobby, 0 );
					break;
			}
		}

		// ====================================================================
		//  Security
		// ====================================================================

		private void UpdateFingerprint()
		{
			if ( string.IsNullOrEmpty( roundSecret ) ) return;

			PhaseFingerprint = HmacSigner.SignFields( roundSecret,
				CurrentPhase.ToString(),
				PhaseStartTime.ToString( "F2" ),
				PhaseDuration.ToString( "F2" ) );
		}

		public void ResetForNewRound()
		{
			if ( !(Bridge?.IsServer ?? false) ) return;
			roundSecret = HmacSigner.GenerateRoundSecret();
		}
	}
}


