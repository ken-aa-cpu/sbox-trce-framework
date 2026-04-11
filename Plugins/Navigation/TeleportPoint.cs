using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Net;
using Trce.Kernel.Auth;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Navigation
{
	[Title( "Teleport Point" ), Group( "Trce - Utility" ), Icon( "shortcut" )]
	public class TeleportPoint : Component
	{
		[Property] public string PointTag { get; set; } = "default";
		private static List<TeleportPoint> allPoints = new();
		public static Action<string, string> OnTeleportComplete;
		protected override void OnEnabled() { allPoints.Add( this ); }
		protected override void OnDisabled() { allPoints.Remove( this ); }
		public static void TeleportTo( GameObject target, string tag )
		{
			var point = allPoints.FirstOrDefault( p => p.PointTag == tag );
			if ( point == null || target == null ) return;
			target.WorldPosition = point.WorldPosition;
			target.WorldRotation = point.WorldRotation;
			OnTeleportComplete?.Invoke( target.Name, tag );
		}

		[Property] public bool IsTrigger { get; set; } = false;
		[Property] public string TargetTag { get; set; }
		public void OnTriggerEnter( Collider other )
		{
			if ( !IsTrigger || string.IsNullOrEmpty( TargetTag ) ) return;
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;

			var root = other.GameObject.Root;

			// Permission Check
			var ownerConn = root?.Network?.Owner;
			var authService = TrceAuthService.Instance;
			if ( ownerConn != null && authService != null )
			{
				ulong steamId = ownerConn.SteamId;
				if ( steamId > 0 && !authService.HasPermission( steamId, "trce.teleport.use" ) )
				{
					Log.Warning( $"[Teleport] Player {steamId} denied access to teleport {TargetTag}" );
					return;
				}

			}
			TeleportTo( root, TargetTag );
		}

	}

}

