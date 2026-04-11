namespace Trce.Kernel.Plugin.Services
{
/// <summary>
/// Health service interface for managing player health states.
/// </summary>
public interface IHealthService
{
/// <summary>Gets the current health of a player.</summary>
float GetHealth( ulong steamId );
/// <summary>Gets the maximum health of a player.</summary>
float GetMaxHealth( ulong steamId );
/// <summary>Heals a player by the specified amount.</summary>
float Heal( ulong steamId, float amount, string source = "" );
/// <summary>Damages a player by the specified amount.</summary>
float Damage( ulong steamId, float amount, ulong attackerId = 0, string cause = "" );
/// <summary>Sets the health of a player to a specific value.</summary>
void SetHealth( ulong steamId, float amount );
/// <summary>Sets the maximum health of a player.</summary>
void SetMaxHealth( ulong steamId, float maxAmount );
/// <summary>Checks if a player is alive.</summary>
bool IsAlive( ulong steamId );
}

}