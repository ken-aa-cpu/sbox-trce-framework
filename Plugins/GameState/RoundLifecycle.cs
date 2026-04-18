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
using Trce.Kernel.Plugin.Services;
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
	public class RoundLifecycle : TrcePlugin, IRoundLifecycleService
	{

		// IRoundLifecycleService events
		public event Action<int> OnRoundStarted;

		public event Action<int> OnRoundCleanedUp;

		[Sync]
		public int RoundNumber { get; private set; } = 0;

		protected override async Task OnPluginEnabled()
		{
			// P0-2: Register as IRoundLifecycleService so consumers resolve via interface.
			TrceServiceManager.Instance?.RegisterService<IRoundLifecycleService>( this );
			await Task.CompletedTask;
		}

		protected override void OnStart()
		{
			// P0-1: resolve via IGamePhaseService instead of direct Scene.Get<GamePhaseManager>()
			var phaseMgr = GetService<IGamePhaseService>();
			if ( phaseMgr != null )
			{
				phaseMgr.OnPhaseChanged += HandlePhaseChanged;
			}
		}

		protected override void OnPluginDisabled()
		{
			var phaseMgr = GetService<IGamePhaseService>();
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

			// P0-1: Use IGamePhaseService instead of Scene.Get<GamePhaseManager>()
			var phaseMgr = GetService<IGamePhaseService>();
			phaseMgr?.ResetForNewRound();

			OnRoundStarted?.Invoke( RoundNumber );

			phaseMgr?.StartGame();
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

		private void HandlePhaseChanged( GamePhaseEnum oldPhase, GamePhaseEnum newPhase, float duration )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;

			if ( newPhase == GamePhaseEnum.EndRound )
			{
				Log.Info( "[RoundLifecycle] Round ending, preparing for cleanup." );
			}
			else if ( newPhase == GamePhaseEnum.Lobby )
			{
				CleanupRound();
			}
		}
	}
}


