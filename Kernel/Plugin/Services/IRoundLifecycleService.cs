// File: Code/Kernel/Plugin/Services/IRoundLifecycle.cs
// Encoding: UTF-8 (No BOM)

using System;

namespace Trce.Kernel.Plugin.Services
{
	/// <summary>
	/// Service contract for round lifecycle coordination.
	/// Consumers should resolve via GetService&lt;IRoundLifecycleService&gt;() instead of
	/// Scene.Get&lt;RoundLifecycle&gt;().
	/// </summary>
	public interface IRoundLifecycleService
	{
		/// <summary>Current round number (1-based).</summary>
		int RoundNumber { get; }

		/// <summary>Fired when a new round starts. Parameter: roundNumber.</summary>
		event Action<int> OnRoundStarted;

		/// <summary>Fired when a round has been cleaned up. Parameter: roundNumber.</summary>
		event Action<int> OnRoundCleanedUp;
	}
}
