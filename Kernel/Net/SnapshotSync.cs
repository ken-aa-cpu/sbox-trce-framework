using Sandbox;
using System;
using Trce.Kernel.Bridge;
using Trce.Kernel.Security;

namespace Trce.Kernel.Net

{
	/// <summary>
	/// Server-authoritative snapshot synchronization.
	/// Periodically broadcasts a state hash to all clients.
	/// Clients that detect a mismatch request a resync.
	/// </summary>
	[Title( "Snapshot Sync" ), Group( "Trce - Kernel" )]
	public class SnapshotSync : Component
	{
		/// <summary>Interval in seconds between each state broadcast from the server.</summary>
		[Property, Description( "Snapshot checksum key" )]
		public float SyncIntervalSeconds { get; set; } = 10f;

		/// <summary>The round secret used by the server to sign each state hash (server-only).</summary>
		private string roundSecret;

		/// <summary>Tracks elapsed time since the last sync broadcast.</summary>
		private TimeSince timeSinceLastSync = 0;

		/// <summary>Cumulative count of desync events detected for this session.</summary>
		[Sync]
		public int DesyncCount { get; private set; }

		public Action<ulong> OnResyncRequested;

		private SandboxBridge _bridge;
		private TrceRNG _rng;

		protected override void OnAwake()
		{
			_bridge = SandboxBridge.Instance;
			_rng = Scene.Get<TrceRNG>();
			if ( _bridge?.IsServer ?? false )
			{
				roundSecret = HmacSigner.GenerateRoundSecret();
				Log.Info( "[SnapshotSync] Server init complete." );
			}

		}

		protected override void OnFixedUpdate()
		{
			if ( !(_bridge?.IsServer ?? false) ) return;
			if ( timeSinceLastSync >= SyncIntervalSeconds )
			{
				timeSinceLastSync = 0;
				BroadcastStateHash();
			}

		}

		// ─────────────────────────────────────────────────────────────────
		// Server-side state construction
		// ─────────────────────────────────────────────────────────────────

		private string BuildStateString()
		{
			var rngSeed = _rng?.CurrentRoundSeed ?? 0;
			// EventBus history removed - using simple time slice for fingerprint
			return $"rng:{rngSeed}|time:{Math.Floor( Time.Now )}";
		}

		/// <summary>Signs the current state string and broadcasts it to all clients.</summary>
		private void BroadcastStateHash()
		{
			var stateString = BuildStateString();
			var hash = HmacSigner.Sign( stateString, roundSecret );
			RpcBroadcastHash( hash, stateString );
		}

		// ─────────────────────────────────────────────────────────────────
		// Client-side verification
		// ─────────────────────────────────────────────────────────────────

		[Rpc.Broadcast]
		private void RpcBroadcastHash( string serverHash, string stateString )
		{
			if ( _bridge?.IsServer ?? false ) return;
			var localState = BuildStateString();
			if ( localState != stateString )
			{
				Log.Warning( $"[SnapshotSync] Desync: Server: {stateString} Local: {localState}" );
				RequestResync();
			}

		}

		/// <summary>Called by the client to notify the server that a resync is needed.</summary>
		[Rpc.Owner]
		private void RequestResync()
		{
			if ( !(_bridge?.IsServer ?? false) ) return;
			DesyncCount++;
			Log.Warning( $"[SnapshotSync] Resync requested. Count: {DesyncCount}" );
			OnResyncRequested?.Invoke( Network.Owner.SteamId );
		}

		/// <summary>Resets the round secret and desync counter at the start of a new round.</summary>
		public void ResetForNewRound()
		{
			if ( !(_bridge?.IsServer ?? false) ) return;
			roundSecret = HmacSigner.GenerateRoundSecret();
			DesyncCount = 0;
			Log.Info( "[SnapshotSync] Reset for new round." );
		}

	}

}
