using System.Collections.Generic;

namespace Trce.Kernel.Plugin.Services
{
/// <summary>
/// Player service interface for querying player state information.
/// </summary>
public interface IPlayerService
{
/// <summary>Gets all online player SteamIds.</summary>
IReadOnlyList<ulong> GetAllPlayerIds();
/// <summary>Gets the display name of a player.</summary>
string GetDisplayName( ulong steamId );
/// <summary>Checks if a player is currently online.</summary>
bool IsOnline( ulong steamId );
/// <summary>Gets all player SteamIds in a specific team.</summary>
IReadOnlyList<ulong> GetTeamPlayers( string teamId );
/// <summary>Gets the team ID of a player.</summary>
string GetTeamId( ulong steamId );
/// <summary>Gets the role ID of a player.</summary>
string GetRoleId( ulong steamId );
}

/// <summary>
/// Weapon service interface for managing player weapon inventory.
/// </summary>
public interface IWeaponService
{
/// <summary>Gives a weapon to a player.</summary>
bool GiveWeapon( ulong steamId, string weaponId );
/// <summary>Takes a weapon from a player.</summary>
bool TakeWeapon( ulong steamId, string weaponId );
/// <summary>Gets all weapon IDs held by a player.</summary>
IReadOnlyList<string> GetWeapons( ulong steamId );
/// <summary>Checks if a player has a specific weapon.</summary>
bool HasWeapon( ulong steamId, string weaponId );
/// <summary>Clears all weapons from a player.</summary>
void ClearWeapons( ulong steamId );
}

/// <summary>
/// Game mode service interface for querying current game state.
/// </summary>
public interface IGameModeService
{
/// <summary>Gets the current game mode ID (e.g. "murder_mystery", "zombie_survival").</summary>
string CurrentGameModeId { get; }
/// <summary>Gets whether the current round is active.</summary>
bool IsRoundActive { get; }
/// <summary>Gets the current phase name (e.g. "TaskPhase", "HuntPhase").</summary>
string CurrentPhaseName { get; }
}
}