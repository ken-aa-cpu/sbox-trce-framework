using System;
using System.Collections.Generic;
using Trce.Kernel.Player;

namespace Trce.Kernel.Plugin.Services
{
	public interface IPlayerManagerService
	{
		event System.Action<TrcePlayerState> OnPlayerJoined;
		event System.Action<ulong> OnPlayerLeft;
		event System.Action<TrcePlayerState, float, float> OnHealthChanged;
		event System.Action<TrcePlayerState, ulong> OnPlayerDied;
		event System.Action<TrcePlayerState> OnPlayerRevived;
		event System.Action<TrcePlayerState> OnRoleAssigned;
		event System.Action<TrcePlayerState, string, string> OnZoneChanged;
		event System.Action<TrcePlayerState, string> OnPlayerDataChanged;
		event System.Action OnRoundReset;

		IReadOnlyList<ulong> GetAllPlayerIds();
		string GetDisplayName( ulong steamId );
		bool IsOnline( ulong steamId );
		IReadOnlyList<ulong> GetTeamPlayers( string teamId );

		void OnPlayerConnected( ulong steamId, string displayName );
		void OnPlayerDisconnected( ulong steamId );

		void SetHealth( ulong steamId, float newHealth );
		void SetAliveState( ulong steamId, AliveState newState, ulong killerId = 0 );
		void SetRole( ulong steamId, string roleId, string teamId );

		TrcePlayerState GetPlayer( ulong steamId );
		IReadOnlyCollection<TrcePlayerState> GetAllPlayers();

		void ResetForNewRound();
	}
}
