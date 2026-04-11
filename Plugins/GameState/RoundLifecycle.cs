using System.Threading.Tasks;
// ====================================================================
// ╔══════════════════════════════════════════════════════════════════╗
// ║  TRCE FRAMEWORK — PROPRIETARY SOURCE CODE                       ║
// ║  Copyright (c) 2026 TRCE Team. All rights reserved.            ║
// ║  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ║
// ╚══════════════════════════════════════════════════════════════════╝
// ====================================================================

using System;
using System.Collections.Generic;
using Sandbox;
using Trce.Kernel.Net;
using Trce.Kernel.Plugin;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.GameState
{
	/// <summary>
	///   / Round Lifecycle Manager
	///   / Coordinates round start, end, and cleanup across the system.
	/// </summary>
	[TrcePlugin(
		Id = "trce.gamestate.round",
		Name = "TRCE Round Manager",
		Version = "1.0.0",
		Depends = new[] { "trce.gamestate.phase" }
	)]
	public class RoundLifecycle : TrcePlugin
	{

		public Action<int> OnRoundStarted;

		public Action<int> OnRoundCleanedUp;

		[Sync]
		public int RoundNumber { get; private set; } = 0;

		protected override async Task OnPluginEnabled()
		{
		}

		protected override void OnStart()
		{
			var phaseMgr = Scene.Get<GamePhaseManager>();
			if ( phaseMgr != null )
			{
				phaseMgr.OnPhaseChanged += HandlePhaseChanged;
			}
		}

		protected override void OnPluginDisabled()
		{
			var phaseMgr = Scene.Get<GamePhaseManager>();
			if ( phaseMgr != null )
			{
				phaseMgr.OnPhaseChanged -= HandlePhaseChanged;
			}
		}

		// ====================================================================
		// Round Control
		// ====================================================================

		public void StartNewRound()
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;

			RoundNumber++;
			Log.Info( $"[RoundLifecycle] === ROUND {RoundNumber} STARTED ===" );

			Scene.Get<Kernel.Net.TrceRNG>()?.InitializeNewRoundSeed();

			Scene.Get<Kernel.Net.SnapshotSync>()?.ResetForNewRound();

			GetPlugin<TaskProgressTracker>()?.ResetProgress();

			GetPlugin<GamePhaseManager>()?.ResetForNewRound();

			OnRoundStarted?.Invoke( RoundNumber );

			Scene.Get<GamePhaseManager>()?.StartGame();
		}

		public void CleanupRound()
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;

			Log.Info( $"[RoundLifecycle] Round {RoundNumber} cleaned up." );

			OnRoundCleanedUp?.Invoke( RoundNumber );
		}

		// ====================================================================
		// Event Handlers
		// ====================================================================

		private void HandlePhaseChanged( GamePhase oldPhase, GamePhase newPhase, float duration )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;

			if ( newPhase == GamePhase.EndRound )
			{
				Log.Info( "[RoundLifecycle] Round ending, preparing for cleanup." );
			}
			else if ( newPhase == GamePhase.Lobby )
			{
				CleanupRound();
			}
		}
	}
}


