// ====================================================================
// ╔══════════════════════════════════════════════════════════════════╗
// ║  TRCE FRAMEWORK — PROPRIETARY SOURCE CODE                       ║
// ║  Copyright (c) 2026 TRCE Team. All rights reserved.            ║
// ║  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ║
// ╚══════════════════════════════════════════════════════════════════╝
// ====================================================================

using Sandbox;
using System;
using System.Collections.Generic;
using Trce.Kernel.Net;
using Trce.Kernel.Plugin;
using Trce.Plugins.Storage;
using System.Threading.Tasks;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Pawn
{
	/// <summary>
	///   / TRCE Gear and Equipment Management Plugin
	/// </summary>
	[TrcePlugin(
		Id = "trce.pawn.gear",
		Name = "TRCE Gear System",
		Version = "1.0.0",
		Depends = new[] { "trce.pawn.stats" }
	)]
	public class TrceGearManager : TrcePlugin
	{
		public enum GearSlot { Weapon, Head, Body, Legs, Accessory1, Accessory2 }

		public Action<GearSlot, string, string> OnGearEquipped;

		private Dictionary<GearSlot, string> equippedItems = new();

		private TrcePlayerStats stats;

		protected override async Task OnPluginEnabled()
		{
			stats = GameObject.Components.Get<TrcePlayerStats>();
			await LoadGearAsync();
		}

		private string GetSaveKey()
		{
			// If we are on a player GameObject, use their SteamId as part of the key.
			// Default to GameObject name if no owner is found.
			var conn = GameObject.Network.Owner;
			ulong steamId = conn?.SteamId ?? 0ul;
			return steamId != 0 ? $"gear_{steamId}" : $"gear_{GameObject.Name}";
		}

		public async Task SaveGearAsync()
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null ) return;
			await bridge.SaveData( GetSaveKey(), equippedItems );
			Log.Info( $"[TRCE-Gear] Saved gear data for {GetSaveKey()}" );
		}

		public async Task LoadGearAsync()
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null ) return;
			var data = await bridge.LoadData<Dictionary<GearSlot, string>>( GetSaveKey() );
			if ( data != null )
			{
				equippedItems = data;
				Log.Info( $"[TRCE-Gear] Loaded gear data for {GetSaveKey()}" );
			}
		}

		// ====================================================================
		// Equip / Unequip Logic
		// ====================================================================

		public async Task<bool> TryEquip( Trce.Plugins.Storage.TrceItemInstance item, GearSlot slot )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return false;

			var def = Scene.Get<TrceItemManager>()?.GetDefinition( item.ItemId );
			if ( def == null ) return false;

			await Unequip( slot );

			equippedItems[slot] = item.ItemId;
			ApplyStatModifiers( def, slot, add: true );

			OnGearEquipped?.Invoke( slot, item.ItemId, item.Uid );

			Log.Info( $"[TRCE-Gear] Equipped {item.ItemId} to slot {slot}" );
			await SaveGearAsync();
			return true;
		}

		public async Task Unequip( GearSlot slot )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			if ( !equippedItems.ContainsKey( slot ) ) return;

			var oldDef = Scene.Get<TrceItemManager>()?.GetDefinition( equippedItems[slot] );
			if ( oldDef != null ) ApplyStatModifiers( oldDef, slot, add: false );

			equippedItems.Remove( slot );
			await SaveGearAsync();
		}

		// ====================================================================
		// Stat Modifiers Integration
		// ====================================================================

		private void ApplyStatModifiers( Trce.Plugins.Storage.TrceItemDefinition def, GearSlot slot, bool add )
		{
			if ( stats == null ) return;
			string source = $"gear_slot_{slot}";

			if ( !add )
			{
				stats.RemoveModifier( source );
				return;
			}

			foreach ( var stat in def.Tags )
			{
				stats.AddModifier( new StatModifier
				{
					StatKey = stat.Key,
					Value   = stat.Value,
					Type    = ModifierType.Flat,
					Source  = source,
					Duration = -1
				} );
			}
		}

		public string GetEquippedItemId( GearSlot slot )
			=> equippedItems.TryGetValue( slot, out var id ) ? id : null;
	}
}

