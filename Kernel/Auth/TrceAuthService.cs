using Sandbox;
using Sandbox.Network;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trce.Kernel.Bridge;
using Trce.Kernel.Plugin;

namespace Trce.Kernel.Auth
{
	/// <summary>
	/// TRCE Auth Service
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
			TrceServiceManager.Instance?.RegisterService<IAuthService>( this );

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

		[Sandbox.ConCmd( "trce_perm_reload" )]
		public static void ReloadPermissions()
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null || !bridge.IsServer ) return;

			_ = PermissionNode.InitializeAsync();
			Log.Info( "[Auth] 權限資料已從儲存空間重新載入。" );
		}

		[Sandbox.ConCmd( "trce_perm_user_addgroup" )]
		public static void AddUserGroup( string steamIdStr, string groupName )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null || !bridge.IsServer ) return;

			if ( !ulong.TryParse( steamIdStr, out ulong steamId ) )
			{
				Log.Error( $"&c[Auth] 無效的 SteamID 格式: {steamIdStr}。須為數字型的 SteamID64。" );
				return;
			}

			_ = AddUserGroupAsync( steamId, groupName );
		}

		private static async Task AddUserGroupAsync( ulong steamId, string groupName )
		{
			var user = PermissionNode.AllUsers.FirstOrDefault( u => u.SteamId == steamId );
			if ( user == null )
			{
				user = new TrcePermissionUser { SteamId = steamId };
				PermissionNode.AllUsers.Add( user );
			}

			// Validate group existence optionally, but let's at least add it if it doesn't exist in user list
			if ( !user.Groups.Contains( groupName ) )
			{
				user.Groups.Add( groupName );
				await PermissionNode.SaveConfigAsync();
				Log.Info( $@"[Auth] 已成功將使用者 {steamId} 加入群組 '{groupName}'。" );
			}
			else
			{
				Log.Warning( $"[Auth] 使用者 {steamId} 已經是 '{groupName}' 的成員。" );
			}
		}

		[Sandbox.ConCmd( "trce_perm_user_addnode" )]
		public static void AddUserNode( string steamIdStr, string node )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null || !bridge.IsServer ) return;

			if ( !ulong.TryParse( steamIdStr, out ulong steamId ) )
			{
				Log.Error( $"&c[Auth] 無效的 SteamID 格式: {steamIdStr}。須為數字型的 SteamID64。" );
				return;
			}

			_ = AddUserNodeAsync( steamId, node );
		}

		private static async Task AddUserNodeAsync( ulong steamId, string node )
		{
			var user = PermissionNode.AllUsers.FirstOrDefault( u => u.SteamId == steamId );
			if ( user == null )
			{
				user = new TrcePermissionUser { SteamId = steamId };
				PermissionNode.AllUsers.Add( user );
			}

			if ( !user.Nodes.Contains( node ) )
			{
				user.Nodes.Add( node );
				await PermissionNode.SaveConfigAsync();
				Log.Info( $@"[Auth] 已成功將節點 '{node}' 加入使用者 {steamId}。" );
			}
			else
			{
				Log.Warning( $"[Auth] 使用者 {steamId} 已經擁有節點 '{node}'。" );
			}
		}
		[Sandbox.ConCmd( "trce_perm_group_create" )]
		public static void CreateGroup( string name, int weight )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null || !bridge.IsServer ) return;

			_ = CreateGroupAsync( name, weight );
		}

		private static async Task CreateGroupAsync( string name, int weight )
		{
			if ( PermissionNode.AllGroups.Any( g => g.Name == name ) )
			{
				Log.Warning( $"[Auth] 群組 '{name}' 已經存在。" );
				return;
			}

			PermissionNode.AllGroups.Add( new TrcePermissionGroup { Name = name, Weight = weight } );
			await PermissionNode.SaveConfigAsync();
			Log.Info( $"[Auth] 已建立群組 '{name}'，權重為 {weight}。" );
		}

		[Sandbox.ConCmd( "trce_perm_group_addnode" )]
		public static void AddGroupNode( string groupName, string node )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null || !bridge.IsServer ) return;

			_ = AddGroupNodeAsync( groupName, node );
		}

		private static async Task AddGroupNodeAsync( string groupName, string node )
		{
			var group = PermissionNode.AllGroups.FirstOrDefault( g => g.Name == groupName );
			if ( group == null )
			{
				Log.Error( $"&c[Auth] 找不到群組 '{groupName}'。" );
				return;
			}

			if ( !group.Nodes.Contains( node ) )
			{
				group.Nodes.Add( node );
				await PermissionNode.SaveConfigAsync();
				Log.Info( $"[Auth] 已將節點 '{node}' 加入群組 '{groupName}'。" );
			}
		}

		[Sandbox.ConCmd( "trce_perm_info" )]
		public static void ShowPlayerPermInfo( string steamIdStr )
		{
			if ( !ulong.TryParse( steamIdStr, out ulong steamId ) )
			{
				Log.Error( $"&c[Auth] 無效的 SteamID 格式: {steamIdStr}。須為數字型的 SteamID64。" );
				return;
			}

			var user = PermissionNode.AllUsers.FirstOrDefault( u => u.SteamId == steamId );
			if ( user == null )
			{
				Log.Error( $"&c[Auth] 使用者 {steamId} 目前沒有特定權限記錄。" );
				return;
			}

			Log.Info( $"=== 使用者 {steamId} 的權限資訊 ===" );
			Log.Info( $"群組 (Groups): {string.Join( ", ", user.Groups )}" );
			Log.Info( $"節點 (Nodes): {string.Join( ", ", user.Nodes )}" );
		}

		[Sandbox.ConCmd( "trce_perm_list" )]
		public static void ListAllPermissions()
		{
			Log.Info( "=== 已註冊的權限群組 (Permission Groups) ===" );
			foreach ( var group in PermissionNode.AllGroups )
			{
				Log.Info( $"群組: {group.Name} (權重: {group.Weight})" );
				Log.Info( $"  節點: {string.Join( ", ", group.Nodes )}" );
			}
		}
	}
}

