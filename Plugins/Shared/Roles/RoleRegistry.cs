using Sandbox;
using Sandbox.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Shared.Roles
{
	public class RoleRegistry : Component
	{

		public struct RoleDefinition
		{
			public string Id;
			public string TeamId;
			public int MaxCount;
			public string DisplayName;
			public string Description;
		}

		private List<RoleDefinition> roleDatabase = new();
		private Dictionary<ulong, string> playerRoles = new();
		private Dictionary<ulong, string> playerTeams = new();

		protected override void OnAwake()
		{

			SetupRoles();
		}

		private void SetupRoles()
		{
			roleDatabase.Clear();
			roleDatabase.Add( new RoleDefinition { Id = "civilian", TeamId = "crew", MaxCount = 4, DisplayName = "Civilian", Description = "Normal Crew" } );
			roleDatabase.Add( new RoleDefinition { Id = "engineer", TeamId = "crew", MaxCount = 2, DisplayName = "Engineer", Description = "Fix things" } );
			roleDatabase.Add( new RoleDefinition { Id = "detective", TeamId = "crew", MaxCount = 1, DisplayName = "Detective", Description = "Investigate" } );
			roleDatabase.Add( new RoleDefinition { Id = "stalker", TeamId = "killer", MaxCount = 1, DisplayName = "Stalker", Description = "Killer" } );
			roleDatabase.Add( new RoleDefinition { Id = "assassin", TeamId = "killer", MaxCount = 1, DisplayName = "Assassin", Description = "Fast Killer" } );
		}

		public void AssignRoles( List<ulong> steamIds )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			playerRoles.Clear();
			playerTeams.Clear();

			var shuffled = steamIds.OrderBy( _ => Guid.NewGuid() ).ToList();
			if ( shuffled.Count > 0 )
			{
				Assign( shuffled[0], "stalker", "killer" );
				for ( int i = 1; i < shuffled.Count; i++ )
				{
					Assign( shuffled[i], "civilian", "crew" );
				}
			}
		}

		private void Assign( ulong steamId, string roleId, string teamId )
		{
			playerRoles[steamId] = roleId;
			playerTeams[steamId] = teamId;
		}

		public string GetRole( ulong steamId ) => playerRoles.GetValueOrDefault( steamId, "unknown" );
		public string GetTeam( ulong steamId ) => playerTeams.GetValueOrDefault( steamId, "none" );

		public List<ulong> GetAllKillers() => playerTeams.Where( kvp => kvp.Value == "killer" ).Select( kvp => kvp.Key ).ToList();
		public List<ulong> GetAllCrew() => playerTeams.Where( kvp => kvp.Value == "crew" ).Select( kvp => kvp.Key ).ToList();
	}
}
