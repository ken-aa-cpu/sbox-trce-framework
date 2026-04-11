// ╔══════════════════════════════════════════════════════════════════╗
// ║  TRCE FRAMEWORK — PROPRIETARY SOURCE CODE                       ║
// ║  Copyright (c) 2026 TRCE Team. All rights reserved.            ║
// ║  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ║
// ╚══════════════════════════════════════════════════════════════════╝
using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using Trce.Kernel.Net;
using Trce.Kernel.Auth;

namespace Trce.Plugins.Social
{
	[Title( "Hologram Screen" ), Group( "Trce - Interaction" ), Icon( "vibration" )]
	public class HologramScreen : Component
	{
		[Property, Category( "Display" )] public string Title { get; set; } = "SYSTEM TERMINAL";
		[Property, Category( "Display" )] public Color ThemeColor { get; set; } = Color.Cyan;
		[Property, Category( "Content" ), TextArea] public string BodyText { get; set; } = "System status: OK\nProgress: %progress%%";
		[Property, Category( "Buttons" )] public List<HoloButton> Buttons { get; set; } = new();
		public Action<int, string, string> OnButtonInteracted;
		public struct HoloButton
		{
			public string Label { get; set; }
			public string RequiredPermission { get; set; }
			public string EventToFire { get; set; }
			public string EventParam { get; set; }
		}

		protected override void OnStart() { Log.Info( $"[Hologram] {Title} initialized." ); }
		public void OnButtonClicked( int index )
		{
			if ( index < 0 || index >= Buttons.Count ) return;
			var btn = Buttons[index];
			if ( !string.IsNullOrEmpty( btn.RequiredPermission ) )
			{
				if ( TrceAuthService.Instance != null && !TrceAuthService.Instance.HasPermission( Connection.Local.SteamId, btn.RequiredPermission ) )
				{
					Log.Warning( $"[Hologram] Permission denied: {btn.RequiredPermission}" );
					return;
				}

			}
			OnButtonInteracted?.Invoke( index, btn.EventToFire, btn.EventParam );
		}

	}

}

