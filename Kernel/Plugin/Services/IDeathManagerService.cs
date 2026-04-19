// File: Code/Kernel/Plugin/Services/IDeathManagerService.cs
// Encoding: UTF-8 (No BOM)

using System;

namespace Trce.Kernel.Plugin.Services
{
	/// <summary>
	/// Service contract for death and evacuation tracking.
	/// Consumers should resolve via GetService&lt;IDeathManagerService&gt;() instead of
	/// Scene.Get&lt;DeathManager&gt;().
	/// </summary>
	public interface IDeathManagerService
	{
		event System.Action<ulong, ulong> OnPlayerKilled;
		event System.Action<ulong, ulong, Vector3, string> OnPlayerDowned;
		event System.Action<ulong, ulong> OnPlayerRevived;
		event System.Action<ulong> OnPlayerExecuted;

		void ProcessExecution( ulong steamId );

		/// <summary>Returns true if the specified player is dead or otherwise gone from the round.</summary>
		bool IsDeadOrGone( ulong steamId );

		/// <summary>Fired when all killers have been eliminated.</summary>
		event System.Action OnAllKillersDead;

		/// <summary>Fired when all crew members have been eliminated.</summary>
		event System.Action OnAllCrewDead;

		/// <summary>Fired when all crew members have evacuated.</summary>
		event System.Action OnAllCrewEvacuated;

		/// <summary>Fired when a single player evacuates. Parameter: steamId.</summary>
		event System.Action<ulong> OnPlayerEvacuated;
	}
}
