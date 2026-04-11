using System.Collections.Generic;
using System.Threading.Tasks;
namespace Trce.Kernel.Plugin.Services

{
/// <summary>
/// Inventory service interface for managing player item storage.
/// </summary>
public interface IInventoryService
{
/// <summary>Gets all item IDs in a player's inventory.</summary>
Task<IReadOnlyList<string>> GetItemsAsync( ulong steamId );
/// <summary>Adds an item to a player's inventory. Returns false on failure.</summary>
Task<bool> AddItemAsync( ulong steamId, string itemId );
/// <summary>Removes an item from a player's inventory.</summary>
Task<bool> RemoveItemAsync( ulong steamId, string itemId );
/// <summary>Checks if a player has a specific item.</summary>
Task<bool> HasItemAsync( ulong steamId, string itemId );
/// <summary>Gets the number of free inventory slots.</summary>
Task<int> GetFreeSlotsAsync( ulong steamId );
/// <summary>Clears a player's inventory.</summary>
Task ClearInventoryAsync( ulong steamId );
/// <summary>Loads a player's inventory from storage.</summary>
Task LoadInventoryAsync( ulong steamId );
/// <summary>Saves a player's inventory to storage.</summary>
Task SaveInventoryAsync( ulong steamId );
/// <summary>Checks if a player's inventory is currently loading.</summary>
bool IsLoading( ulong steamId );
}

}