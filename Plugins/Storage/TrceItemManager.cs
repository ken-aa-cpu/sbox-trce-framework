// ╔══════════════════════════════════════════════════════════════════╗
// ║  TRCE FRAMEWORK — PROPRIETARY SOURCE CODE                       ║
// ║  Copyright (c) 2026 TRCE Team. All rights reserved.            ║
// ║  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ║
// ╚══════════════════════════════════════════════════════════════════╝

using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Net;
using Trce.Kernel.Auth;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Storage
{
	/// <summary>
	/// TRCE-Items Management System (Server Authoritative)
	///
	/// Core responsibilities:
	///   - Loads all ItemId-to-TrceItemDefinition definition mappings
	///   - Manages registered IItemBehavior plugin extensions for custom item logic
	///   - Dispatches OnUse / OnInteract callbacks
	/// </summary>
	[Title( "TRCE-Items Manager" ), Group( "Trce - Items" ), Icon( "inventory_2" )]
	public class TrceItemManager : Component
	{
		public static TrceItemManager Instance { get; private set; }

		/// <summary>All registered item definitions (ItemId to Definition)</summary>
		private Dictionary<string, TrceItemDefinition> itemDefs = new();

		/// <summary>All registered behavior plugins (BehaviorId to IItemBehavior)</summary>
		private Dictionary<string, IItemBehavior> behaviors = new();

		public Action<ulong, string, string> OnItemUsed;
		public Action<ulong, string> OnSkillCast;

		protected override void OnAwake() => Instance = this;

		protected override void OnStart() => LoadAllDefinitions();

		// ═══════════════════════════════════════
		//  Definition Management
		// ═══════════════════════════════════════

		private void LoadAllDefinitions()
		{
			// Use s&box ResourceLibrary to auto-discover all .trceitem resource files
			var all = ResourceLibrary.GetAll<TrceItemDefinition>();
			foreach ( var def in all )
			{
				if ( string.IsNullOrEmpty( def.ItemId ) ) continue;
				itemDefs[def.ItemId] = def;
			}
			Log.Info( $"[TRCE-Items] Loaded {itemDefs.Count} item definitions." );
		}

		public TrceItemDefinition GetDefinition( string itemId )
			=> itemDefs.TryGetValue( itemId, out var def ) ? def : null;

		// ═══════════════════════════════════════
		//  Behavior Plugin Registration (Plugin Ecosystem)
		// ═══════════════════════════════════════

		/// <summary>Allows external plugins to register custom item behavior handlers.</summary>
		public static void RegisterBehavior( IItemBehavior behavior )
		{
			if ( Instance == null || behavior == null ) return;
			Instance.behaviors[behavior.BehaviorId] = behavior;
			Log.Info( $"[TRCE-Items] Registered behavior: {behavior.BehaviorId}" );
		}

		// ═══════════════════════════════════════
		//  Item Usage (Server-Side Only)
		// ═══════════════════════════════════════

		/// <summary>Dispatches OnUse callback for an item.</summary>
		public void UseItem( TrceItemInstance item, ulong userSteamId )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;

			// Permission check
			var authService = TrceAuthService.Instance;
			if ( authService != null && !authService.HasPermission( userSteamId, "trce.items.use" ) )
			{
				Log.Warning( $"[TRCE-Items] Player {userSteamId} lacks permission to use items." );
				return;
			}

			var def = GetDefinition( item.ItemId );
			if ( def == null ) return;

			// 1. Check for registered IItemBehavior
			if ( behaviors.TryGetValue( item.ItemId, out var behavior ) )
			{
				behavior.OnUse( item, userSteamId );
				return;
			}

			// 2. No behavior plugin — dispatch Action event
			if ( !string.IsNullOrEmpty( def.OnUseEvent ) )
			{
				OnItemUsed?.Invoke( userSteamId, item.ItemId, def.OnUseEvent );
			}

			// 3. Dispatch linked SkillId
			if ( !string.IsNullOrEmpty( def.LinkedSkillId ) )
			{
				OnSkillCast?.Invoke( userSteamId, def.LinkedSkillId );
			}
		}

		/// <summary>Creates a new item instance (Server Only).</summary>
		public TrceItemInstance CreateItem( string itemId, int quantity = 1 )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return null;
			if ( !itemDefs.ContainsKey( itemId ) )
			{
				Log.Warning( $"[TRCE-Items] Unknown item definition: {itemId}" );
				return null;
			}
			return TrceItemInstance.Create( itemId, quantity );
		}
	}
}

