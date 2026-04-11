using System.Threading.Tasks;
// ====================================================================
// ╔══════════════════════════════════════════════════════════════════╗
// ║  Copyright (c) 2026 TRCE Team. All rights reserved.            ║
// ║  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ║
// ====================================================================
using Sandbox;
using System;
using System.Collections.Generic;
using Trce.Kernel.Net;
using Trce.Kernel.Plugin;

namespace Trce.Plugins.Shared.Input
{
	/// <summary>
	/// TRCE-Input Manager
	/// </summary>
	[TrcePlugin(
		Id = "trce.shared.input",
		Name = "TRCE Input System",
		Version = "1.0.0"
	)]
	public class TrceInputManager : TrcePlugin
	{
		[Property, Category( "UI" )] public string ShopKey { get; set; } = "B";
		[Property, Category( "UI" )] public string InventoryKey { get; set; } = "I";
		[Property, Category( "UI" )] public string PassKey { get; set; } = "P";
		[Property, Category( "UI" )] public string StatsKey { get; set; } = "C";
		[Property, Category( "UI" )] public string TeamKey { get; set; } = "T";
		
		public Action<string> OnUiToggleRequested;

		protected override async Task OnPluginEnabled()
		{
		}

		protected override void OnUpdate()
		{
			if ( IsProxy || Sandbox.Input.UsingController ) return;
			CheckKey( ShopKey, "shop" );
			CheckKey( InventoryKey, "inventory" );
			CheckKey( PassKey, "battlepass" );
			CheckKey( StatsKey, "stats" );
			CheckKey( TeamKey, "team" );
		}

		private void CheckKey( string keyName, string panelId )
		{
			if ( Sandbox.Input.Pressed( keyName ) )
			{
				OnUiToggleRequested?.Invoke( panelId );
			}
		}
	}
}


