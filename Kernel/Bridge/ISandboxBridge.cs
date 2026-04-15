// File: Kernel/Bridge/ISandboxBridge.cs
// Encoding: UTF-8 (No BOM)
// Interface for the SandboxBridge kernel system.
// Allows plugins to resolve the bridge via TrceServiceManager instead of the static Instance.

using Sandbox;
using Sandbox.Network;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Trce.Kernel.Bridge
{
	/// <summary>
	/// Abstraction of the s&amp;box API layer.
	/// Resolve via <c>GetService&lt;ISandboxBridge&gt;()</c> in plugins;
	/// avoid using <see cref="SandboxBridge.Instance"/> directly.
	/// </summary>
	public interface ISandboxBridge
	{
		// ── Identity ─────────────────────────────────────────────────────
		bool IsServer { get; }
		bool IsClient { get; }
		Connection LocalConnection { get; }
		ulong LocalSteamId { get; }

		// ── Scene Helpers ─────────────────────────────────────────────────
		Guid GetObjectId( GameObject obj );
		int GetNetworkIdInt( GameObject obj );
		uint GetNetworkIdUInt( GameObject obj );

		// ── Connections ───────────────────────────────────────────────────
		IEnumerable<Connection> GetAllConnections();
		int GetPlayerCount();
		Connection FindConnectionBySteamId( ulong steamId );

		// ── Time ──────────────────────────────────────────────────────────
		float ServerTime { get; }
		float DeltaTime { get; }

		// ── Spawning ──────────────────────────────────────────────────────
		GameObject SpawnNetworked( GameObject prefab, Transform spawnTransform, Connection owner );

		// ── Data Persistence ──────────────────────────────────────────────
		Task SaveData<T>( string fileName, T data );
		Task<T> LoadData<T>( string fileName, T defaultValue = default );
		Task<bool> DataExists( string fileName );

		// ── Logging ───────────────────────────────────────────────────────
		void LogInfo( string message );
		void LogWarning( string message );
		void LogError( string message );
		void LogModule( string moduleName, string message );
	}
}
