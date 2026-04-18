using System;
using System.Collections.Generic;
using Trce.Kernel.Player;

namespace Trce.Kernel.Plugin.Services
{
	public interface IPlayerManagerService : ITrceService
	{
		event Action<TrcePlayerState> OnPlayerJoined;
		event Action<ulong> OnPlayerLeft;
		event Action<TrcePlayerState, float, float> OnHealthChanged;
		event Action<TrcePlayerState, ulong> OnPlayerDied;
		event Action<TrcePlayerState> OnPlayerRevived;
		event Action<TrcePlayerState> OnRoleAssigned;
		event Action<TrcePlayerState, string, string> OnZoneChanged;
		event Action<TrcePlayerState, string> OnPlayerDataChanged;
		event Action OnRoundReset;

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
