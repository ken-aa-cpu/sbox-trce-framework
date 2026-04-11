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
                    Lock = new SemaphoreSlim(1, 1) // 初始即開放，一次僅允許一條執行緒進入
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
            // 優先尋找相同 ItemId 的 ItemStack 進行合併（ItemStack 無 MaxStackSize，直接合併）
            for (int i = 0; i < state.Items.Count; i++)
            {
                if (item.Amount <= 0) break;

                var existing = state.Items[i];
                if (existing.ItemId == item.ItemId)
                {
                    state.Items[i] = existing.Merge(item);
                    item = default; // 全部合併進去了
                    break;
                }
            }

            // 若還有剩餘物品，尋找空位(槽位容量)放置新 Stack
            if (item.Amount > 0 && state.Items.Count < state.Capacity)
            {
                state.Items.Add(item);
                return default; // default(ItemStack) 代表為空，全部已放入
            }

            return item; // 回傳放不下的剩餘物品
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

            // 從後面往前遍歷，方便在扣光時直接移除元素且不影響索引
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
                        // 扣除所需的數量，把剩餘的部份寫回 List
                        var splitResult = existing.Split(remainingToTake);
                        state.Items[i] = splitResult.remainder;
                        takenCount += remainingToTake;
                        remainingToTake = 0;
                    }
                }
            }

            if (takenCount > 0)
            {
                // 我們累積了取出的總數 takenCount
                // (如果您這有建構子或其他調整方式可以自行替換，此處使用 with 語法建立回傳品)
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

        // GetContents 是同步的，若使用 SemaphoreSlim(1,1) 若遇爭用則短暫等待，避免 Deadlock
        if (state.Lock.Wait(50))
        {
            try
            {
                return state.Items.ToArray(); // 回傳快照(Snapshot)，杜絕外部直接篡改內部指標
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

        // 確保完成當前操作後再銷毀
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
                    // 強制解除卡死的鎖，為 Dispose 做準備
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
