using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox;

namespace Trce.Kernel.Storage;


public interface IContainerService
{
    Task InitializeContainerAsync(GameObject container, int capacity);
    Task<ItemStack> TryAddItemAsync(GameObject container, ItemStack item);
    Task<ItemStack> TakeItemAsync(GameObject container, string itemId, int count);
    IReadOnlyList<ItemStack> GetContents(GameObject container);
    Task DestroyContainerAsync(GameObject container);
}
