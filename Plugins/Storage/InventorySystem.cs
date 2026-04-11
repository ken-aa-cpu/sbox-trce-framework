using System.Threading.Tasks;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Net;
using Trce.Kernel.Plugin;
using Trce.Kernel.Plugin.Services;
using Trce.Kernel.Player;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Storage
{
	/// <summary>
	///   / TRCE Inventory System
	///   / Manages player inventories as a plugin service.
	/// </summary>
	[TrcePlugin(
		Id = "trce.inventory",
		Name = "TRCE Inventory System",
		Version = "1.1.0",
		Author = "TRCE Team"
	)]
	[Icon( "backpack" )]
	public class InventorySystem : TrcePlugin, IInventoryService
	{
		private static InventorySystem instance;
		public static InventorySystem Instance => instance;

		public Action<ulong, string, string> OnInventoryChanged;

		private readonly Dictionary<ulong, List<string>> playerInventories = new();

		private readonly Dictionary<ulong, bool> _isLoading = new();
		private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, System.Threading.SemaphoreSlim> _locks = new();

		[Property] public int DefaultSlotCount { get; set; } = 8;

		protected override Task OnPluginEnabled()
		{
			instance = this;
			return Task.CompletedTask;
		}

		private string GetSaveKey(ulong steamId) => $"inventory_{steamId}";

		public async Task SaveInventoryAsync(ulong steamId)
		{
			var syncLock = _locks.GetOrAdd(steamId, _ => new System.Threading.SemaphoreSlim(1, 1));
			await syncLock.WaitAsync();
			try
			{
				var bridge = SandboxBridge.Instance;
				if (bridge == null) return;
				if (playerInventories.TryGetValue(steamId, out var inv))
				{
					// Creating a snapshot to prevent concurrent modification during serialization
					var snapshot = inv.ToList();
					await bridge.SaveData(GetSaveKey(steamId), snapshot);
					Log.Info($"[TRCE-Inventory] Saved inventory data for {steamId}");
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[TRCE-Inventory] Failed to save inventory for {steamId}: {ex.Message}");
			}
			finally
			{
				syncLock.Release();
			}
		}

		public async Task LoadInventoryAsync(ulong steamId)
		{
			var syncLock = _locks.GetOrAdd(steamId, _ => new System.Threading.SemaphoreSlim(1, 1));
			await syncLock.WaitAsync();
			_isLoading[steamId] = true;
			try
			{
				var bridge = SandboxBridge.Instance;
				if (bridge == null) return;
				
				var data = await bridge.LoadData<List<string>>(GetSaveKey(steamId));
				if (data != null && data.Count > 0)
				{
					// Ensure size is correct
					while(data.Count < DefaultSlotCount) data.Add("");
					playerInventories[steamId] = data;
					Log.Info($"[TRCE-Inventory] Loaded inventory data for {steamId}");
				}
				else
				{
					var inv = new List<string>();
					for (int i = 0; i < DefaultSlotCount; i++) inv.Add("");
					playerInventories[steamId] = inv;
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[TRCE-Inventory] Failed to load inventory for {steamId}: {ex.Message}");
			}
			finally
			{
				_isLoading[steamId] = false;
				syncLock.Release();
				OnInventoryChanged?.Invoke(steamId, "loaded", "");
			}
		}

		public bool IsLoading(ulong steamId)
		{
			return _isLoading.TryGetValue(steamId, out var loading) && loading;
		}

		private async Task<List<string>> GetOrSpawnInventoryAsync( ulong steamId )
		{
			if ( !playerInventories.ContainsKey( steamId ) )
			{
				await LoadInventoryAsync(steamId);
			}
			else
			{
				while (_isLoading.TryGetValue(steamId, out var loading) && loading)
				{
					await Task.Delay(10);
				}
			}
			return playerInventories[steamId];
		}

		// ====================================================================
		// IInventoryService Implementation
		// ====================================================================

		public async Task<IReadOnlyList<string>> GetItemsAsync( ulong steamId )
		{
			var inv = await GetOrSpawnInventoryAsync( steamId );
			return inv.Where( i => !string.IsNullOrEmpty( i ) ).ToList();
		}

		public async Task<bool> AddItemAsync( ulong steamId, string itemId )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return false;

			var inv = await GetOrSpawnInventoryAsync( steamId );
			int emptySlot = inv.IndexOf( "" );
			if ( emptySlot == -1 ) return false;

			inv[emptySlot] = itemId;

			OnInventoryChanged?.Invoke( steamId, "add", itemId );
			await SaveInventoryAsync(steamId);

			return true;
		}

		public async Task<bool> RemoveItemAsync( ulong steamId, string itemId )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return false;

			var inv = await GetOrSpawnInventoryAsync( steamId );
			int index = inv.IndexOf( itemId );
			if ( index == -1 ) return false;

			inv[index] = "";

			OnInventoryChanged?.Invoke( steamId, "remove", itemId );
			await SaveInventoryAsync(steamId);

			return true;
		}

		public async Task<bool> HasItemAsync( ulong steamId, string itemId )
		{
			var inv = await GetOrSpawnInventoryAsync( steamId );
			return inv.Contains( itemId );
		}

		public async Task<int> GetFreeSlotsAsync( ulong steamId )
		{
			var inv = await GetOrSpawnInventoryAsync( steamId );
			return inv.Count( i => string.IsNullOrEmpty( i ) );
		}

		public async Task ClearInventoryAsync( ulong steamId )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			var inv = await GetOrSpawnInventoryAsync( steamId );
			for ( int i = 0; i < inv.Count; i++ ) inv[i] = "";
			
			OnInventoryChanged?.Invoke( steamId, "clear", "" );
			await SaveInventoryAsync(steamId);
		}
	}
}


