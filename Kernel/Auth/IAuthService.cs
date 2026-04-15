// File: Kernel/Auth/IAuthService.cs
// Encoding: UTF-8 (No BOM)
// Interface for the TrceAuthService kernel system.
// Allows plugins to resolve the auth service via TrceServiceManager.

using Sandbox;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Trce.Kernel.Auth
{
	/// <summary>
	/// Abstraction of TRCE's player authentication and session management service.
	/// Resolve via <c>GetService&lt;IAuthService&gt;()</c> in plugins;
	/// avoid using <see cref="TrceAuthService.Instance"/> directly.
	/// </summary>
	public interface IAuthService
	{
		/// <summary>Returns true once the permission data has finished loading.</summary>
		bool IsReady { get; }

		/// <summary>Awaits permission initialization. Safe to call multiple times.</summary>
		Task EnsureReady();

		/// <summary>Authenticates a connecting client. Returns null if rejected.</summary>
		Task<PlayerSession> Authenticate( Connection connection );

		/// <summary>Marks the player's session as disconnected.</summary>
		void HandleDisconnect( Connection connection );

		/// <summary>Returns the session for the given SteamID, or null if not found.</summary>
		PlayerSession GetSession( ulong steamId );

		/// <summary>Returns the permission weight for the given SteamID.</summary>
		int GetWeight( ulong steamId );

		/// <summary>Returns all currently active (connected, non-expired) sessions.</summary>
		List<PlayerSession> GetActiveSessions();

		/// <summary>Returns all sessions including disconnected ones still in the window.</summary>
		List<PlayerSession> GetAllSessions();

		/// <summary>Number of currently active players.</summary>
		int ActivePlayerCount { get; }

		/// <summary>Returns true if the player holds the given permission node.</summary>
		bool HasPermission( ulong steamId, string permission );

		/// <summary>Clears all sessions immediately (e.g. for testing or server shutdown).</summary>
		void ClearAllSessions();
	}
}
