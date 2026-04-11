using Sandbox;
using System.Collections.Generic;

namespace Trce.Kernel.Plugin.Services

{
/// <summary>
/// Pawn service interface for spawning and managing player/NPC pawns.
/// </summary>
public interface IPawnService
{
/// <summary>
/// Spawns a player pawn.
/// </summary>
/// <param name="steamId">The player's SteamId.</param>
/// <param name="prefabPath">Optional prefab path. Uses default if null.</param>
/// <returns>The spawned GameObject.</returns>
GameObject SpawnPlayerPawn( ulong steamId, string prefabPath = null );
/// <summary>
/// Spawns an NPC pawn.
/// </summary>
/// <param name="npcId">NPC identifier (e.g. "merch_01").</param>
/// <param name="prefabPath">Prefab path for the NPC.</param>
/// <param name="spawnPos">World position to spawn at.</param>
/// <returns>The spawned NPC GameObject.</returns>
GameObject SpawnNpcPawn( string npcId, string prefabPath, Vector3 spawnPos );
/// <summary>
/// Gets a player's pawn GameObject.
/// </summary>
GameObject GetPlayerPawn( ulong steamId );
/// <summary>
/// Removes and destroys a player's pawn.
/// </summary>
void RemovePawn( ulong steamId );
/// <summary>
/// Gets all active pawns (including NPCs).
/// </summary>
IEnumerable<GameObject> GetAllPawns();
/// <summary>
/// Sets the model for a pawn.
/// </summary>
void SetModel( GameObject pawn, string modelPath );
}

}