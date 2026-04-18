// File: Code/Kernel/Plugin/Services/IConfrontationService.cs
// Encoding: UTF-8 (No BOM)

using System;

namespace Trce.Kernel.Plugin.Services
{
	/// <summary>
	/// Service contract for the confrontation voting system.
	/// Consumers should resolve via GetService&lt;IConfrontationService&gt;() instead of
	/// Scene.Get&lt;ConfrontationManager&gt;().
	/// </summary>
	public interface IConfrontationService
	{
		/// <summary>
		/// Fired when a confrontation vote resolves.
		/// Parameters: (targetSteamId, voteCount, resultType)
		/// </summary>
		event Action<ulong, int, string> OnConfrontationResult;
	}
}
