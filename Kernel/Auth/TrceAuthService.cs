using Sandbox;
using Sandbox.Network;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trce.Kernel.Bridge;
using Trce.Kernel.Plugin;
using Trce.Kernel.SRE;

namespace Trce.Kernel.Auth
{
	/// <summary>
	/// <para>TRCE Auth Service — Session Manager</para>
	/// <para>
	/// <b>P1-5 Role Clarification:</b><br/>
	/// <list type="bullet">
	///   <item><description>
	///     <b>TrceAuthService</b> (this class): Owns player <c>Session</c> lifecycle — connect, reconnect,
	///     disconnect, expiry. Resolves and caches <see cref="TrcePermissionUser"/> on authentication.
	///     Registers itself as <see cref="IAuthService"/>.
	///   </description></item>
	///   <item><description>
	///     <b>TrceAuthPlugin</b>: Owns all runtime <c>Permission</c> checks (grant, revoke, wildcard,
	///     inheritance). Registers itself as <see cref="IPermissionService"/>.
	///   </description></item>
	/// </list>
	/// When both are present in a scene, <see cref="HasPermission"/> delegates to
	/// <see cref="IPermissionService"/>, falling back to <see cref="PermissionNode"/> only if the
	/// service is unavailable.
	/// </para>
	/// </summary>
	[Title( "TRCE Auth Service" ), Group( "Trce - Kernel" ), Icon( "security" )]
	public class TrceAuthService : GameObjectSystem, ISceneStartup, IAuthService
	{
		public static TrceAuthService Instance { get; private set; }

		private ConcurrentDictionary<ulong, PlayerSession> sessions = new();
		private RealTimeSince timeSinceCleanup = 0;
		private const float CleanupIntervalSeconds = 30f;

		private Task _initTask;
		public bool IsReady => _initTask != null && _initTask.IsCompletedSuccessfully;

		public TrceAuthService( Scene scene ) : base( scene )
		{
			Instance = this;
		}

		public void OnLevelLoaded()
		{
			if ( SandboxBridge.Instance != null && !SandboxBridge.Instance.IsServer )
			{
				return;
			}

			// Register with TrceServiceManager so plugins can resolve via GetService<IAuthService>().
			TrceServiceManager.Instance?.RegisterService<IAuthService>( this, ServicePriority.Kernel );

			_initTask = PermissionNode.InitializeAsync();
		}

		public async Task EnsureReady()
		{
			if ( _initTask == null ) return;
			await _initTask;
		}

		protected void OnFixedUpdate()
		{
			if ( SandboxBridge.Instance == null || !SandboxBridge.Instance.IsServer )
				return;

			if ( timeSinceCleanup >= CleanupIntervalSeconds )
			{
				timeSinceCleanup = 0;
				CleanupExpiredSessions();
			}
		}

		public async System.Threading.Tasks.Task<PlayerSession> Authenticate( Connection connection )
		{
			if ( !IsReady ) await EnsureReady();

			var bridge = SandboxBridge.Instance;
			if ( bridge == null || !bridge.IsServer ) return null;
			ulong steamId = connection.SteamId;

			if ( sessions.TryGetValue( steamId, out var existing ) )
			{
				if ( existing.IsDisconnected )
				{
					if ( existing.Reconnect( connection ) )
					{
						bridge.LogModule( "Auth", $"Reconnected {connection.DisplayName} ({steamId})" );
						return existing;
					}
					else
					{
						bridge.LogModule( "Auth", $"Session expired for {connection.DisplayName} ({steamId}), recreating." );
						sessions.TryRemove( steamId, out _ );
					}
				}
				else
				{
					bridge.LogError( $"[Auth] Login fail: {connection.DisplayName} ({steamId}) already exists." );
					return null;
				}
			}

			var session = new PlayerSession( connection, bridge.ServerTime );
			
			// Resolve user from storage (or create default)
			session.PermissionUser = await PermissionNode.ResolveUserAsync( steamId );
			
			sessions.TryAdd( steamId, session );
			bridge.LogModule( "Auth", $"Authenticated: {connection.DisplayName} ({steamId}) with {session.PermissionUser.Groups.Count} groups." );
			return session;
		}

		public void HandleDisconnect( Connection connection )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null || !bridge.IsServer ) return;
			ulong steamId = connection.SteamId;

			if ( sessions.TryGetValue( steamId, out var session ) )
			{
				session.MarkDisconnected();
				bridge.LogModule( "Auth",
					$"{connection.DisplayName} ({steamId}) disconnected. Session window: {PlayerSession.ReconnectWindowSeconds}s." );
			}
		}

		public PlayerSession GetSession( ulong steamId )
		{
			sessions.TryGetValue( steamId, out var session );
			return session;
		}

		public int GetWeight( ulong steamId )
		{
			var session = GetSession( steamId );
			return PermissionNode.GetUserWeight( session?.PermissionUser );
		}

		public List<PlayerSession> GetActiveSessions()
		{
			return sessions.Values.Where( s => s.IsActive && !s.IsDisconnected ).ToList();
		}

		public List<PlayerSession> GetAllSessions()
		{
			return sessions.Values.ToList();
		}

		public int ActivePlayerCount => sessions.Values.Count( s => s.IsActive && !s.IsDisconnected );

		public bool HasPermission( ulong steamId, string permission )
		{
			// P1-5: Delegate to IPermissionService (TrceAuthPlugin) as the authoritative source.
			// Fall back to PermissionNode only when TrceAuthPlugin is not loaded.
			var permService = TrceServiceManager.Instance?.GetService<IPermissionService>();
			if ( permService != null )
				return permService.HasPermission( steamId, permission );

			// Fallback: PermissionNode direct check (legacy path).
			var session = GetSession( steamId );
			if ( session?.PermissionUser == null ) return false;
			return PermissionNode.HasPermission( session.PermissionUser, permission );
		}

		private void CleanupExpiredSessions()
		{
			// ConcurrentDictionary is safe to mutate during enumeration — no ToList() snapshot needed.
			foreach ( var kvp in sessions )
			{
				if ( !kvp.Value.IsExpired ) continue;

				if ( sessions.TryRemove( kvp.Key, out var removed ) )
				{
					SandboxBridge.Instance?.LogModule( "Auth",
						$"Session expired and swept: {removed.DisplayName} ({kvp.Key})" );
				}
			}
		}

		public void ClearAllSessions()
		{
			sessions.Clear();
			SandboxBridge.Instance?.LogModule( "Auth", "All sessions cleared." );
		}

		// ═══════════════════════════════════════
		//  Admin Commands
		// ═══════════════════════════════════════

		// ═══════════════════════════════════════
		//  Internal Async Safety Wrapper
		// ═══════════════════════════════════════

		/// <summary>
		/// P1-1: Fire-and-forget guard for static ConCmd async calls.
		/// Captures and reports exceptions via SreSystem instead of silently swallowing them.
		/// </summary>
		private static async void FireAndLog( System.Threading.Tasks.Task task, string context )
		{
			try
			{
				await task;
			}
			catch ( System.Exception ex )
			{
				var msg = $"[Auth] {context} failed: {ex.Message}";
				Log.Error( msg );
				if ( SreSystem.Instance != null )
					await SreSystem.Instance.ReportError( "TrceAuthService", msg, ex.StackTrace );
			}
		}

		[Sandbox.ConCmd( "trce_perm_reload" )]
		public static void ReloadPermissions()
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null || !bridge.IsServer ) return;

			FireAndLog( PermissionNode.InitializeAsync(), "ReloadPermissions" );
			Log.Info( "[Auth] Permission data reloaded from storage." );
		}

		[Sandbox.ConCmd( "trce_perm_user_addgroup" )]
		public static void AddUserGroup( string steamIdStr, string groupName )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null || !bridge.IsServer ) return;

			if ( !ulong.TryParse( steamIdStr, out ulong steamId ) )
			{
				Log.Error( $"&c[Auth] Invalid SteamID format: {steamIdStr}. Must be a numeric SteamID64." );
				return;
			}

			FireAndLog( AddUserGroupAsync( steamId, groupName ), "AddUserGroup" );
		}

		private static async Task AddUserGroupAsync( ulong steamId, string groupName )
		{
			if ( PermissionNode.AllUsers.Any( u => u.SteamId == steamId && u.Groups.Contains( groupName ) ) )
			{
				Log.Warning( $"[Auth] User {steamId} is already a member of '{groupName}'." );
				return;
			}

			await PermissionNode.AddUserToGroupAsync( steamId, groupName );
			Log.Info( $"[Auth] Successfully added user {steamId} to group '{groupName}'." );
		}

		[Sandbox.ConCmd( "trce_perm_user_addnode" )]
		public static void AddUserNode( string steamIdStr, string node )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null || !bridge.IsServer ) return;

			if ( !ulong.TryParse( steamIdStr, out ulong steamId ) )
			{
				Log.Error( $"&c[Auth] Invalid SteamID format: {steamIdStr}. Must be a numeric SteamID64." );
				return;
			}

			FireAndLog( AddUserNodeAsync( steamId, node ), "AddUserNode" );
		}

		private static async Task AddUserNodeAsync( ulong steamId, string node )
		{
			if ( PermissionNode.AllUsers.Any( u => u.SteamId == steamId && u.Nodes.Contains( node ) ) )
			{
				Log.Warning( $"[Auth] User {steamId} already has node '{node}'." );
				return;
			}

			await PermissionNode.AddNodeToUserAsync( steamId, node );
			Log.Info( $"[Auth] Successfully added node '{node}' to user {steamId}." );
		}
		[Sandbox.ConCmd( "trce_perm_group_create" )]
		public static void CreateGroup( string name, int weight )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null || !bridge.IsServer ) return;

			FireAndLog( CreateGroupAsync( name, weight ), "CreateGroup" );
		}

		private static async Task CreateGroupAsync( string name, int weight )
		{
			if ( PermissionNode.AllGroups.Any( g => g.Name == name ) )
			{
				Log.Warning( $"[Auth] Group '{name}' already exists." );
				return;
			}

			await PermissionNode.CreateGroupAsync( name, weight );
			Log.Info( $"[Auth] Created group '{name}' with weight {weight}." );
		}

		[Sandbox.ConCmd( "trce_perm_group_addnode" )]
		public static void AddGroupNode( string groupName, string node )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null || !bridge.IsServer ) return;

			FireAndLog( AddGroupNodeAsync( groupName, node ), "AddGroupNode" );
		}

		private static async Task AddGroupNodeAsync( string groupName, string node )
		{
			if ( !PermissionNode.AllGroups.Any( g => g.Name == groupName ) )
			{
				Log.Error( $"&c[Auth] Group '{groupName}' not found." );
				return;
			}

			await PermissionNode.AddNodeToGroupAsync( groupName, node );
			Log.Info( $"[Auth] 已將節點 '{node}' 加入群組 '{groupName}'。" );
		}

		[Sandbox.ConCmd( "trce_perm_info" )]
		public static void ShowPlayerPermInfo( string steamIdStr )
		{
			if ( !ulong.TryParse( steamIdStr, out ulong steamId ) )
			{
				Log.Error( $"&c[Auth] Invalid SteamID format: {steamIdStr}. Must be a numeric SteamID64." );
				return;
			}

			var user = PermissionNode.AllUsers.FirstOrDefault( u => u.SteamId == steamId );
			if ( user == null )
			{
				Log.Error( $"&c[Auth] User {steamId} has no specific permission records." );
				return;
			}

			Log.Info( $"=== Permission info for user {steamId} ===" );
			Log.Info( $"Groups: {string.Join( ", ", user.Groups )}" );
			Log.Info( $"Nodes: {string.Join( ", ", user.Nodes )}" );
		}

		[Sandbox.ConCmd( "trce_perm_list" )]
		public static void ListAllPermissions()
		{
			Log.Info( "=== Registered Permission Groups ===" );
			foreach ( var group in PermissionNode.AllGroups )
			{
				Log.Info( $"Group: {group.Name} (weight: {group.Weight})" );
				Log.Info( $"  Nodes: {string.Join( ", ", group.Nodes )}" );
			}
		}
	}
}

