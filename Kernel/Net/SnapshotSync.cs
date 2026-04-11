using Sandbox;
using System;
using Trce.Kernel.Bridge;
using Trce.Kernel.Security;

namespace Trce.Kernel.Net

{
	/// <summary>
	///   / ? ? ???(Snapshot Sync)
	/// </summary>
	[Title( "Snapshot Sync" ), Group( "Trce - Kernel" )]
	public class SnapshotSync : Component
	{
		/// <summary> ? ? ? ( ?</summary>
		[Property, Description( "Snapshot checksum key" )]
		public float SyncIntervalSeconds { get; set; } = 10f;
		/// <summary> ? ?? ?? ???(Server ? ?? ? ? ?</summary>
		private string roundSecret;
		/// <summary> </summary>
		private TimeSince timeSinceLastSync = 0;
		/// <summary> ?  ( ??</summary>
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

		// ?��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��???
		// Server  ? ? ??
		// ?��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��???
		private string BuildStateString()
		{
			var rngSeed = _rng?.CurrentRoundSeed ?? 0;
			// EventBus history removed - using simple time slice for fingerprint
			return $"rng:{rngSeed}|time:{Math.Floor( Time.Now )}";
		}

		/// <summary> ?? ??Hash  </summary>
		private void BroadcastStateHash()
		{
			var stateString = BuildStateString();
			var hash = HmacSigner.Sign( stateString, roundSecret );
			RpcBroadcastHash( hash, stateString );
		}

		// ?��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��???
		//  Client 端�?驗�?
		// ?��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��???
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

		/// <summary> Client  ?? ? ?</summary>
		[Rpc.Owner]
		private void RequestResync()
		{
			if ( !(_bridge?.IsServer ?? false) ) return;
			DesyncCount++;
			Log.Warning( $"[SnapshotSync] Resync requested. Count: {DesyncCount}" );
			OnResyncRequested?.Invoke( Network.Owner.SteamId );
		}

		/// <summary> ? ?? ?? ??</summary>
		public void ResetForNewRound()
		{
			if ( !(_bridge?.IsServer ?? false) ) return;
			roundSecret = HmacSigner.GenerateRoundSecret();
			DesyncCount = 0;
			Log.Info( "[SnapshotSync] Reset for new round." );
		}

	}

}

