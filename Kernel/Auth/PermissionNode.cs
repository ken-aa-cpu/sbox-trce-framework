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
		public static List<TrcePermissionGroup> AllGroups => _allGroups;
		public static List<TrcePermissionUser> AllUsers => _allUsers;

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

			await TrceStorageService.Instance.SaveAsync( "perm_groups.json", _allGroups );
			await TrceStorageService.Instance.SaveAsync( "perm_users.json", _allUsers );
			
			Log.Info( $"[PermissionNode] Saved {_allGroups.Count} groups and {_allUsers.Count} users." );
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

			_allGroups = await TrceStorageService.Instance.LoadAsync<List<TrcePermissionGroup>>( "perm_groups.json" ) ?? new();
			_allUsers = await TrceStorageService.Instance.LoadAsync<List<TrcePermissionUser>>( "perm_users.json" ) ?? new();
			
			// Seed default groups if empty
			if ( _allGroups.Count == 0 )
			{
				Log.Info( "[PermissionNode] Seeding default groups..." );
				_allGroups.Add( new TrcePermissionGroup { Name = "admin", Weight = 100, Nodes = new List<string> { "*" } } );
				_allGroups.Add( new TrcePermissionGroup { Name = "player", Weight = 0, Nodes = new List<string> { "trce.chat.*", "trce.help" } } );
				await SaveConfigAsync();
			}

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
			// Try to find in loaded list
			var user = _allUsers.FirstOrDefault( u => u.SteamId == steamId );
			if ( user != null ) return user;

			// If not found, create a default "Player" model
			return new TrcePermissionUser
			{
				SteamId = steamId,
				Groups = new List<string> { "player" }
			};
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


