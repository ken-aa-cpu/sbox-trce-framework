using System.Threading.Tasks;
// ====================================================================
// ����������������������������������������������������������������������������������������������������������������������������������������
// ��  TRCE FRAMEWORK �X PROPRIETARY SOURCE CODE                       ��
// ��  Copyright (c) 2026 TRCE Team. All rights reserved.            ��
// ��  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ��
// ����������������������������������������������������������������������������������������������������������������������������������������
// ====================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Trce.Kernel.Net;
using Trce.Kernel.Plugin;
using Trce.Kernel.Papi;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Shared.Teams
{
	/// <summary>
	///   / Team Member data container
	/// </summary>
	public class TeamMember
	{
		public ulong SteamId { get; set; }
		public string Name { get; set; }
		public bool IsReady { get; set; }
		public string RoleId { get; set; }
	}

	/// <summary>
	///   / Team Data container
	/// </summary>
	public class TeamData
	{
		public string TeamId { get; set; }
		public string Name { get; set; }
		public Color TeamColor { get; set; } = Color.White;
		public ulong LeaderId { get; set; }
		public List<TeamMember> Members { get; set; } = new();
		public Dictionary<string, string> Tags { get; set; } = new();
	}

	/// <summary>
	///   / TRCE Team Manager Plugin
	/// </summary>
	[TrcePlugin(
		Id = "trce.shared.teams",
		Name = "TRCE Team System",
		Version = "1.0.0"
	)]
	public class TrceTeamManager : TrcePlugin, ITrcePlaceholderProvider
	{

		public string ProviderId => "teams";

		public Action<string, ulong> OnTeamCreated;
		public Action<string, ulong> OnMemberJoined;

		/// <summary> Team map [TeamId -> Data]</summary>
		private Dictionary<string, TeamData> teams = new();

		/// <summary> Player to Team map [SteamId -> TeamId]</summary>
		private Dictionary<ulong, string> playerTeamMap = new();

		protected override async Task OnPluginEnabled()
		{

			PlaceholderAPI.For( this )?.RegisterProvider( this );
		}

		protected override void OnPluginDisabled()
		{
			PlaceholderAPI.For( this )?.UnregisterProvider( this );
		}

		// ====================================================================
		// Server Management
		// ====================================================================

		public TeamData CreateTeam( string name, ulong leaderId )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return null;

			var teamId = Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );
			var team = new TeamData
			{
				TeamId = teamId,
				Name = name,
				LeaderId = leaderId
			};

			teams[teamId] = team;
			AddMember( teamId, leaderId );

			OnTeamCreated?.Invoke( teamId, leaderId );

			return team;
		}

		public bool AddMember( string teamId, ulong steamId )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return false;
			if ( !teams.TryGetValue( teamId, out var team ) ) return false;

			LeaveTeam( steamId );

			var playerName = Connection.All.FirstOrDefault( c => c.SteamId == steamId )?.DisplayName ?? "Unknown Player";

			team.Members.Add( new TeamMember { SteamId = steamId, Name = playerName } );
			playerTeamMap[steamId] = teamId;

			OnMemberJoined?.Invoke( teamId, steamId );

			PlaceholderAPI.For( this )?.InvalidateCache();
			return true;
		}

		public void LeaveTeam( ulong steamId )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			if ( !playerTeamMap.TryGetValue( steamId, out var teamId ) ) return;

			if ( teams.TryGetValue( teamId, out var team ) )
			{
				team.Members.RemoveAll( m => m.SteamId == steamId );

				if ( team.Members.Count == 0 )
				{
					teams.Remove( teamId );
				}
				else if ( team.LeaderId == steamId )
				{
					team.LeaderId = team.Members[0].SteamId;
				}
			}

			playerTeamMap.Remove( steamId );
			PlaceholderAPI.For( this )?.InvalidateCache();
		}

		// ====================================================================
		//  Queries
		// ====================================================================

		public TeamData GetPlayerTeam( ulong steamId )
		{
			if ( playerTeamMap.TryGetValue( steamId, out var teamId ) )
			{
				return teams.GetValueOrDefault( teamId );
			}
			return null;
		}

		public bool AreTeammates( ulong id1, ulong id2 )
		{
			if ( !playerTeamMap.TryGetValue( id1, out var team1 ) ) return false;
			if ( !playerTeamMap.TryGetValue( id2, out var team2 ) ) return false;
			return team1 == team2;
		}

		// ====================================================================
		//  PAPI Support
		// ====================================================================

		public string TryResolvePlaceholder( string key )
		{
			if ( key == "teams_my_name" )
			{
				var team = GetPlayerTeam( Connection.Local?.SteamId ?? 0ul );
				return team?.Name ?? "No Team";
			}

			if ( key == "teams_my_count" )
			{
				var team = GetPlayerTeam( Connection.Local?.SteamId ?? 0ul );
				return team?.Members.Count.ToString() ?? "0";
			}

			return null;
		}
	}
}

