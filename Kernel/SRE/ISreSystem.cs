// File: Kernel/SRE/ISreSystem.cs
// Encoding: UTF-8 (No BOM)
// Interface for the SreSystem kernel system.
// Allows plugins to resolve the SRE monitor via TrceServiceManager.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Trce.Kernel.SRE
{
	/// <summary>
	/// Abstraction of TRCE's plugin health monitoring system (SRE).
	/// Resolve via <c>GetService&lt;ISreSystem&gt;()</c> in plugins;
	/// avoid using <see cref="SreSystem.Instance"/> directly.
	/// </summary>
	public interface ISreSystem
	{
		/// <summary>Called by TrcePlugin on startup to register the plugin with the SRE monitor.</summary>
		void CheckIn( string pluginId, string version );

		/// <summary>Called by TrcePlugin.SafeExecute on unhandled exceptions to log and report the error.</summary>
		Task ReportError( string source, string message, string stackTrace );

		/// <summary>Returns a snapshot of all currently checked-in plugin IDs.</summary>
		List<string> GetActivePlugins();
	}
}
