// File: Code/Kernel/Plugin/Services/ITaskProgressService.cs
// Encoding: UTF-8 (No BOM)

using System;

namespace Trce.Kernel.Plugin.Services
{
	/// <summary>
	/// Service contract for task progress tracking.
	/// Consumers should resolve via GetService&lt;ITaskProgressService&gt;() instead of
	/// Scene.Get&lt;TaskProgressTracker&gt;().
	/// </summary>
	public interface ITaskProgressService
	{
		/// <summary>Current aggregate progress [0..1].</summary>
		float Progress { get; }

		/// <summary>Fired when a player completes a task. Parameters: (steamId, taskId, location).</summary>
		event Action<ulong, string, string> OnTaskCompleted;

		/// <summary>Fired when cumulative progress reaches 100%.</summary>
		event Action OnProgressReached100;

		/// <summary>Resets all task progress for a new round.</summary>
		void ResetProgress();
	}
}
