// File: Kernel/Net/INetManager.cs
// Encoding: UTF-8 (No BOM)
// Interface for the TrceNetManager kernel system.
// Allows plugins to resolve the net manager via TrceServiceManager.

using Sandbox;
using System.Threading.Tasks;

namespace Trce.Kernel.Net
{
	/// <summary>
	/// Abstraction of TRCE's network lifecycle manager.
	/// Resolve via <c>GetService&lt;INetManager&gt;()</c> in plugins;
	/// avoid using <see cref="TrceNetManager.Instance"/> directly.
	/// </summary>
	public interface INetManager
	{
		/// <summary>
		/// Handles a new client connection: runs auth, validates the connection, then
		/// publishes <c>ClientReadyEvent</c> via <see cref="Event.GlobalEventBus"/>.
		/// </summary>
		Task DispatchClientConnected( Connection channel );

		/// <summary>
		/// Handles a client disconnect: notifies auth service and publishes
		/// <c>ClientDisconnectedEvent</c> via <see cref="Event.GlobalEventBus"/>.
		/// </summary>
		void DispatchClientDisconnected( Connection channel );
	}
}
