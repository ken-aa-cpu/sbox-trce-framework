using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trce.Kernel.Storage;

namespace Trce.Kernel.Auth
{
	public class TrcePermissionGroup
	{
		public string Name { get; set; }
		public List<string> Nodes { get; set; } = new();
		public int Weight { get; set; }
	}

	public class TrcePermissionUser
	{
		public ulong SteamId { get; set; }
		public List<string> Groups { get; set; } = new();
		public List<string> Nodes { get; set; } = new();
	}

	public static class PermissionNode
	{
		public static IReadOnlyList<TrcePermissionGroup> AllGroups => _allGroups;
		public static IReadOnlyList<TrcePermissionUser>  AllUsers  => _allUsers;

		private static readonly object _writeLock = new();
		private static List<TrcePermissionGroup> _allGroups = new();
		private static List<TrcePermissionUser> _allUsers = new();
		private static bool _initialized;

		/// <summary>
		/// Saves the current permission groups and users to JSON files.
		/// </summary>
		public static async Task SaveConfigAsync()
		{
			if ( TrceStorageService.Instance == null )
			{
				Log.Warning( "[PermissionNode] Cannot save: TrceStorageService.Instance is null." );
				return;
			}

			// Capture current references outside the lock so we don't await inside a lock block.
			List<TrcePermissionGroup> groupsSnapshot;
			List<TrcePermissionUser> usersSnapshot;
			lock ( _writeLock )
			{
				groupsSnapshot = _allGroups;
				usersSnapshot  = _allUsers;
			}

			await TrceStorageService.Instance.SaveAsync( "perm_groups.json", groupsSnapshot );
			await TrceStorageService.Instance.SaveAsync( "perm_users.json", usersSnapshot );

			Log.Info( $"[PermissionNode] Saved {groupsSnapshot.Count} groups and {usersSnapshot.Count} users." );
		}

		/// <summary>
		/// P0-5: Clears all static state so the next scene starts with a clean slate.
		/// Must be called from SandboxBridge.OnLevelLoaded() before InitializeAsync().
		/// </summary>
		public static void ResetStatic()
		{
			_allGroups = new();
			_allUsers = new();
			_initialized = false;
			Log.Info( "[PermissionNode] Static state cleared for new scene." );
		}

		/// <summary>
		/// Initializes permission groups and users from JSON files using TrceStorageService.
		/// </summary>
		public static async Task InitializeAsync()
		{
			if ( TrceStorageService.Instance == null )
			{
				Log.Warning( "[PermissionNode] Cannot initialize: TrceStorageService.Instance is null." );
				return;
			}

			// Load fully into local variables first — readers will not see a half-populated list.
			var loadedGroups = await TrceStorageService.Instance.LoadAsync<List<TrcePermissionGroup>>( "perm_groups.json" ) ?? new();
			var loadedUsers  = await TrceStorageService.Instance.LoadAsync<List<TrcePermissionUser>>( "perm_users.json" ) ?? new();

			bool wasSeeded = false;
			// Seed default groups if empty (before atomic assignment so seed is part of the complete list).
			if ( loadedGroups.Count == 0 )
			{
				Log.Info( "[PermissionNode] Seeding default groups..." );
				loadedGroups.Add( new TrcePermissionGroup { Name = "admin", Weight = 100, Nodes = new List<string> { "*" } } );
				loadedGroups.Add( new TrcePermissionGroup { Name = "player", Weight = 0, Nodes = new List<string> { "trce.chat.*", "trce.help" } } );
				wasSeeded = true;
			}

			// Atomic reference replacement — readers always see a complete list.
			_allGroups = loadedGroups;
			_allUsers  = loadedUsers;

			// Persist seeded defaults if we just created them.
			if ( wasSeeded )
				await SaveConfigAsync();

			_initialized = true;
			Log.Info( $"[PermissionNode] Initialized with {_allGroups.Count} groups and {_allUsers.Count} specific user overrides." );
		}

		/// <summary>
		/// Single-level node check for a user and their assigned groups.
		/// </summary>
		public static bool HasNode( TrcePermissionUser user, List<TrcePermissionGroup> groups, string targetNode )
		{
			if ( user == null || string.IsNullOrEmpty( targetNode ) ) return false;

			// 1. Check user direct nodes (including wildcard '*')
			if ( user.Nodes.Any( n => n == targetNode || n == "*" ) ) return true;
			foreach ( var node in user.Nodes )
			{
				if ( MatchesPermission( node, targetNode ) ) return true;
			}

			// 2. Iterate through provided groups and check their nodes
			if ( groups != null )
			{
				foreach ( var group in groups )
				{
					if ( group.Nodes.Any( n => n == targetNode || n == "*" ) ) return true;
					foreach ( var node in group.Nodes )
					{
						if ( MatchesPermission( node, targetNode ) ) return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Loads or creates a user permission profile for a specific SteamId.
		/// </summary>
		public static async Task<TrcePermissionUser> ResolveUserAsync( ulong steamId )
		{
			// Fast path — unlocked read (volatile guarantees we see the current reference).
			var user = _allUsers.FirstOrDefault( u => u.SteamId == steamId );
			if ( user != null ) return user;

			// Prepare new user before entering the lock to minimise lock duration.
			var newUser = new TrcePermissionUser
			{
				SteamId = steamId,
				Groups  = new List<string> { "player" }
			};

			bool shouldSave = false;
			lock ( _writeLock )
			{
				// Double-checked: another thread may have inserted between our fast-path read and now.
				var existing = _allUsers.FirstOrDefault( u => u.SteamId == steamId );
				if ( existing != null ) return existing;

				_allUsers.Add( newUser );
				shouldSave = true;
			}

			// Persist outside the lock — C# forbids await inside a lock block.
			if ( shouldSave )
				await SaveConfigAsync();

			return newUser;
		}

		/// <summary>
		/// Adds a group membership to the specified user, persisting the change.
		/// Thread-safe: write is protected by _writeLock.
		/// </summary>
		public static async Task AddUserToGroupAsync( ulong steamId, string groupName )
		{
			bool shouldSave = false;

			lock ( _writeLock )
			{
				var user = _allUsers.FirstOrDefault( u => u.SteamId == steamId );
				if ( user == null )
				{
					user = new TrcePermissionUser { SteamId = steamId, Groups = new List<string> { "player" } };
					_allUsers.Add( user );
				}

				if ( !user.Groups.Contains( groupName ) )
				{
					user.Groups.Add( groupName );
					shouldSave = true;
				}
			}

			if ( shouldSave )
				await SaveConfigAsync();
		}

		/// <summary>
		/// Adds a permission node to the specified user, persisting the change.
		/// Thread-safe: write is protected by _writeLock.
		/// </summary>
		public static async Task AddNodeToUserAsync( ulong steamId, string node )
		{
			bool shouldSave = false;

			lock ( _writeLock )
			{
				var user = _allUsers.FirstOrDefault( u => u.SteamId == steamId );
				if ( user == null )
				{
					user = new TrcePermissionUser { SteamId = steamId, Groups = new List<string> { "player" } };
					_allUsers.Add( user );
				}

				if ( !user.Nodes.Contains( node ) )
				{
					user.Nodes.Add( node );
					shouldSave = true;
				}
			}

			if ( shouldSave )
				await SaveConfigAsync();
		}

		/// <summary>
		/// Creates a new permission group if one with the same name does not exist, persisting the change.
		/// Thread-safe: write is protected by _writeLock.
		/// </summary>
		public static async Task CreateGroupAsync( string name, int weight )
		{
			bool shouldSave = false;

			lock ( _writeLock )
			{
				if ( _allGroups.Any( g => g.Name == name ) )
					return;

				_allGroups.Add( new TrcePermissionGroup { Name = name, Weight = weight } );
				shouldSave = true;
			}

			if ( shouldSave )
				await SaveConfigAsync();
		}

		/// <summary>
		/// Adds a permission node to the specified group, persisting the change.
		/// Thread-safe: write is protected by _writeLock.
		/// </summary>
		public static async Task AddNodeToGroupAsync( string groupName, string node )
		{
			bool shouldSave = false;

			lock ( _writeLock )
			{
				var group = _allGroups.FirstOrDefault( g => g.Name == groupName );
				if ( group == null ) return;

				if ( !group.Nodes.Contains( node ) )
				{
					group.Nodes.Add( node );
					shouldSave = true;
				}
			}

			if ( shouldSave )
				await SaveConfigAsync();
		}

		/// <summary>
		/// Convenience method to check permission using internal group cache.
		/// </summary>
		public static bool HasPermission( TrcePermissionUser user, string permission )
		{
			if ( !_initialized || user == null ) return false;

			var userGroups = _allGroups.Where( g => user.Groups.Contains( g.Name ) ).ToList();
			return HasNode( user, userGroups, permission );
		}

		/// <summary>
		/// Gets the highest weight among the user's groups.
		/// Used for legacy weight-based checks.
		/// </summary>
		public static int GetUserWeight( TrcePermissionUser user )
		{
			if ( !_initialized || user == null ) return 0;
			if ( user.Groups.Count == 0 ) return 0;

			return _allGroups
				.Where( g => user.Groups.Contains( g.Name ) )
				.Select( g => g.Weight )
				.DefaultIfEmpty( 0 )
				.Max();
		}

		private static bool MatchesPermission( string pattern, string target )
		{
			if ( pattern == target ) return true;
			if ( pattern.EndsWith( ".*" ) )
			{
				var prefix = pattern.Substring( 0, pattern.Length - 1 );
				return target.StartsWith( prefix );
			}
			return false;
		}
	}
}


