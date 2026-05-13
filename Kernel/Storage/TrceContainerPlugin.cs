using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sandbox;
using Trce.Kernel.Plugin;

namespace Trce.Kernel.Storage;


public class TrceContainerPlugin : TrcePlugin, IContainerService
{
    private class ContainerState
    {
        public int Capacity;
        public List<ItemStack> Items;
        public SemaphoreSlim Lock;
    }

    private readonly Dictionary<GameObject, ContainerState> _containers = new();

    public Task InitializeContainerAsync(GameObject container, int capacity)
    {
        if (container == null) return Task.CompletedTask;

        lock (_containers)
        {
            if (!_containers.ContainsKey(container))
            {
                _containers[container] = new ContainerState
                {
                    Capacity = capacity,
                    Items = new List<ItemStack>(capacity),
                    Lock = new SemaphoreSlim(1, 1) // Initially open; only one thread may enter at a time.
                };
            }
        }
        return Task.CompletedTask;
    }

    public async Task<ItemStack> TryAddItemAsync(GameObject container, ItemStack item)
    {
        if (container == null || item.Amount <= 0) return item;

        ContainerState state;
        lock (_containers)
        {
            if (!_containers.TryGetValue(container, out state))
                return item;
        }

        await state.Lock.WaitAsync();
        try
        {
            // Prefer merging into an existing ItemStack with the same ItemId (no MaxStackSize limit — merge directly).
            for (int i = 0; i < state.Items.Count; i++)
            {
                if (item.Amount <= 0) break;

                var existing = state.Items[i];
                if (existing.ItemId == item.ItemId)
                {
                    state.Items[i] = existing.Merge(item);
                    item = default; // Fully merged.
                    break;
                }
            }

            // If there are still remaining items, find an empty slot (within capacity) and add a new stack.
            if (item.Amount > 0 && state.Items.Count < state.Capacity)
            {
                state.Items.Add(item);
                return default; // default(ItemStack) means empty — all items were placed.
            }

            return item; // Return the leftover items that could not fit.
        }
        finally
        {
            state.Lock.Release();
        }
    }

    public async Task<ItemStack> TakeItemAsync(GameObject container, string itemId, int count)
    {
        if (container == null || count <= 0) return default;

        ContainerState state;
        lock (_containers)
        {
            if (!_containers.TryGetValue(container, out state))
                return default;
        }

        await state.Lock.WaitAsync();
        try
        {
            int remainingToTake = count;
            int takenCount = 0;
            ItemStack lastTaken = default;

            // Traverse from back to front so removing fully-depleted stacks does not shift remaining indices.
            for (int i = state.Items.Count - 1; i >= 0; i--)
            {
                if (remainingToTake <= 0) break;

                var existing = state.Items[i];
                if (existing.ItemId == itemId)
                {
                    lastTaken = existing;
                    if (existing.Amount <= remainingToTake)
                    {
                        takenCount += existing.Amount;
                        remainingToTake -= existing.Amount;
                        state.Items.RemoveAt(i);
                    }
                    else
                    {
                        // Deduct the required amount and write the remainder back to the list.
                        var splitResult = existing.Split(remainingToTake);
                        state.Items[i] = splitResult.remainder;
                        takenCount += remainingToTake;
                        remainingToTake = 0;
                    }
                }
            }

            if (takenCount > 0)
            {
                // Construct a new ItemStack representing the accumulated taken amount.
                // (Replace with a constructor or factory if one becomes available.)
                return new ItemStack(lastTaken.ItemId, takenCount, lastTaken.Metadata);
            }

            return default;
        }
        finally
        {
            state.Lock.Release();
        }
    }

    public IReadOnlyList<ItemStack> GetContents(GameObject container)
    {
        if (container == null) return Array.Empty<ItemStack>();

        ContainerState state;
        lock (_containers)
        {
            if (!_containers.TryGetValue(container, out state))
                return Array.Empty<ItemStack>();
        }

        // GetContents is synchronous. If using SemaphoreSlim(1,1) and contention occurs, wait briefly to avoid deadlock.
        if (state.Lock.Wait(50))
        {
            try
            {
                return state.Items.ToArray(); // Return a snapshot so callers cannot mutate internal state directly.
            }
            finally
            {
                state.Lock.Release();
            }
        }

        return Array.Empty<ItemStack>();
    }

    public async Task DestroyContainerAsync(GameObject container)
    {
        if (container == null) return;

        ContainerState state;
        lock (_containers)
        {
            if (_containers.TryGetValue(container, out state))
            {
                _containers.Remove(container);
            }
            else
            {
                return;
            }
        }

        // Ensure any in-flight operation completes before destroying.
        await state.Lock.WaitAsync();
        try
        {
            state.Items.Clear();
        }
        finally
        {
            state.Lock.Release();
            state.Lock.Dispose();
        }
    }

    protected override void OnPluginDisabled()
    {
        base.OnPluginDisabled();

        lock (_containers)
        {
            foreach (var kvp in _containers)
            {
                var state = kvp.Value;
                try
                {
                    // Force-release any stuck lock before Dispose to prevent deadlocks.
                    if (state.Lock.CurrentCount == 0)
                    {
                        state.Lock.Release();
                    }
                    state.Lock.Dispose();
                }
                catch (ObjectDisposedException) { }
                catch (SemaphoreFullException) { } 
            }
            _containers.Clear();
        }
    }
}
