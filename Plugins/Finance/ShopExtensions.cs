using Sandbox;
using System;
using System.Linq;
using Trce.Kernel.Player;
using Trce.Kernel.Auth;

namespace Trce.Plugins.Finance
{
	[GameResource( "TRCE Req: Team", "trce_req", "Limit purchase to a specific team" )]
	public class TeamRequirement : ShopRequirement
	{
		[Property] public string RequiredTeam { get; set; } = "killer";

		public override bool IsMet( PurchaseContext ctx )
		{
			var state = Sandbox.Game.ActiveScene?.Get<TrcePlayerManager>()?.GetPlayer( ctx.BuyerId );
			return state?.TeamId == RequiredTeam;
		}

		public override string GetFailureReason( PurchaseContext ctx ) => $"Only {RequiredTeam} can buy this.";
	}

	[GameResource( "TRCE Req: Permission", "trce_req", "Limit purchase to a permission node" )]
	public class PermissionRequirement : ShopRequirement
	{
		[Property] public string Node { get; set; } = "trce.vip";

		public override bool IsMet( PurchaseContext ctx )
		{
			return Sandbox.Game.ActiveScene?.Get<TrceAuthService>()?.HasPermission( ctx.BuyerId, Node ) ?? false;
		}

		public override string GetFailureReason( PurchaseContext ctx ) => "Missing required permission.";
	}

	[GameResource( "TRCE Action: Teleport", "trce_action", "Teleport player home on purchase" )]
	public class TeleportAction : ShopAction
	{
		[Property] public string TargetTag { get; set; } = "safe_zone";

		public override void Execute( GameObject player, ShopItemResource item )
		{
			var target = player.Scene.GetAllObjects( false ).FirstOrDefault( o => o.Tags.Has( TargetTag ) );
			if ( target != null )
			{
				player.WorldPosition = target.WorldPosition;
			}
		}
	}
}

