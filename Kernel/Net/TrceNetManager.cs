// File: Code/Kernel/Net/TrceNetManager.cs
// Encoding: UTF-8 (No BOM)

using Sandbox;
using System.Threading.Tasks;
using Trce.Kernel.Auth;
using Trce.Kernel.Event;
using Trce.Kernel.Plugin;

namespace Trce.Kernel.Net
{
	/// <summary>
	/// <para>【TRCE Network Core Manager】</para>
	/// <para>
	/// Single responsibility: manage the lifecycle state of server connections and broadcast
	/// a signal via <see cref="GlobalEventBus"/> once a connection is ready.
	/// </para>
	/// <para>
	/// <b>Architecture boundary (must be strictly observed):</b><br/>
	/// - This class must never hold a direct reference to any game entity (e.g. PlayerPrefab, SpawnPoint).<br/>
	/// - Player-Pawn spawning logic is the responsibility of the game-mode plugin,
	///   which subscribes to <see cref="CoreEvents.ClientReadyEvent"/>.<br/>
	/// - This class only publishes signals; it does not care who consumes them.
	/// </para>
	/// </summary>
	[Title( "TRCE Net Manager" ), Group( "Trce - Kernel" ), Icon( "wifi" )]
	public class TrceNetManager : GameObjectSystem, ISceneStartup, INetManager
	{
		// ═══════════════════════════════════════════════════════════════════
		//  Singleton (GameObjectSystem guarantees one instance per Scene)
		// ═══════════════════════════════════════════════════════════════════

		/// <summary>
		/// P2-2: Internal direct-access instance for framework-internal use only.
		/// <b>External code must resolve via <c>TrceServiceManager.Instance.GetService&lt;INetManager&gt;()</c> instead.</b>
		/// </summary>
		internal static TrceNetManager Instance { get; private set; }

		// ═══════════════════════════════════════════════════════════════════
		//  Constructor
		// ═══════════════════════════════════════════════════════════════════

		public TrceNetManager( Scene scene ) : base( scene )
		{
			Instance = this;
		}

		// ═══════════════════════════════════════════════════════════════════
		//  ISceneStartup
		// ═══════════════════════════════════════════════════════════════════

		/// <inheritdoc/>
		public void OnSceneStartup()
		{
			// Register with TrceServiceManager so plugins can resolve via GetService<INetManager>().
			TrceServiceManager.Instance?.RegisterService<INetManager>( this, ServicePriority.Kernel );
			Log.Info( "[Net] TrceNetManager initialized." );
		}

		// ═══════════════════════════════════════════════════════════════════
		//  Connection lifecycle entry points (called by GameMode / top-level Component)
		// ═══════════════════════════════════════════════════════════════════

		/// <summary>
		/// Handles an incoming client connection request.
		/// <para>
		/// <b>Flow:</b>
		/// <list type="number">
		///   <item>Delegates authentication to <see cref="TrceAuthService"/>.</item>
		///   <item>Aborts if authentication fails or the connection is no longer active.</item>
		///   <item>On success, publishes <see cref="CoreEvents.ClientReadyEvent"/> via
		///         <see cref="GlobalEventBus"/> so game-mode plugins can spawn the Pawn.</item>
		/// </list>
		/// </para>
		/// </summary>
		/// <param name="channel">The <see cref="Connection"/> that initiated the connection.</param>
		public async Task DispatchClientConnected( Connection channel )
		{
			// ── Step 1: Authentication (only when the Auth service is available) ──────────────
			var auth = TrceAuthService.Instance;
			if ( auth != null )
			{
				var session = await auth.Authenticate( channel );

				// Authentication failed (duplicate connection, banned, etc.)
				if ( session == null )
				{
					Log.Warning( $"[Net] Auth rejected connection for {channel.DisplayName} ({channel.SteamId})." );
					return;
				}
			}

			// ── Step 2: Verify the connection is still active (guards against drop during auth) ─
			if ( !channel.IsActive )
			{
				Log.Warning( $"[Net] Connection became inactive during auth: {channel.DisplayName} ({channel.SteamId})." );
				return;
			}

			// ── Step 3: Zero-GC event broadcast — notifies all game-mode plugins ─────────────
			// struct created on the stack; Publish path has no Heap allocation.
			var evt = new CoreEvents.ClientReadyEvent(
				channel:     channel,
				steamId:     channel.SteamId,
				displayName: channel.DisplayName
			);

			GlobalEventBus.Publish( evt );

			Log.Info( $"[Net] ClientReadyEvent published for {channel.DisplayName} ({channel.SteamId})." );
		}

		/// <summary>
		/// Handles a client disconnection.
		/// <para>
		/// <b>Flow:</b>
		/// <list type="number">
		///   <item>Notifies <see cref="TrceAuthService"/> to mark the Session as disconnected.</item>
		///   <item>Publishes <see cref="CoreEvents.ClientDisconnectedEvent"/> via <see cref="GlobalEventBus"/>
		///         so game-mode plugins can clean up the Pawn and related state.</item>
		/// </list>
		/// </para>
		/// </summary>
		/// <param name="channel">The <see cref="Connection"/> that disconnected.</param>
		public void DispatchClientDisconnected( Connection channel )
		{
			// ── Step 1: Notify the Auth service to mark the session as disconnected ──────────
			TrceAuthService.Instance?.HandleDisconnect( channel );

			// ── Step 2: Zero-GC event broadcast — notifies all game-mode plugins ─────────────
			var evt = new CoreEvents.ClientDisconnectedEvent(
				channel:     channel,
				steamId:     channel.SteamId,
				displayName: channel.DisplayName
			);

			GlobalEventBus.Publish( evt );

			Log.Info( $"[Net] ClientDisconnectedEvent published for {channel.DisplayName} ({channel.SteamId})." );
		}
	}
}
