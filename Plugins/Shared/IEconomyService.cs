namespace Trce.Kernel.Plugin.Services
{
/// <summary>
/// Economy service interface for managing player currency and transactions.
/// </summary>
public interface IEconomyService
{
/// <summary>Gets the current balance of a player.</summary>
float GetBalance( ulong steamId );
/// <summary>Adds currency to a player's balance.</summary>
float Add( ulong steamId, float amount, string reason = "" );
/// <summary>Deducts currency from a player. Returns false if insufficient funds.</summary>
bool Deduct( ulong steamId, float amount, string reason = "" );
/// <summary>Sets the balance of a player to a specific amount.</summary>
void SetBalance( ulong steamId, float amount );
/// <summary>Checks if a player has enough currency.</summary>
bool HasEnough( ulong steamId, float amount );
/// <summary>Transfers currency from one player to another.</summary>
bool Transfer( ulong fromSteamId, ulong toSteamId, float amount );
}

}