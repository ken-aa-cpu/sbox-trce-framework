using Sandbox;
using System;
using System.Linq;
using Trce.Kernel.Player;
using Trce.Plugins.Shared.Input; // For Inventory if needed
using Trce.Plugins.Social; // For ChatManager
using Trce.Kernel.Plugin;

namespace Trce.Plugins.Finance
{
	[GameResource( "TRCE Action: Give Item", "trceaction", "Give an item to player on purchase" )]
	public class GiveItemAction : ShopAction
	{
		[Property] public string ItemId { get; set; } = "pistol";
		[Property] public int Amount { get; set; } = 1;

		public override void Execute( GameObject player, ShopItemResource item )
		{
			if ( player?.Network?.Owner == null ) return;
			var steamId = player.Network.Owner.SteamId;
			// Note: Replace with actual inventory system call
			Log.Info( $"[ShopAction] Given {ItemId} x{Amount} to {player.Name}" );
		}
	}

	[GameResource( "TRCE Action: Heal Player", "trceaction", "Heal player on purchase" )]
	public class HealPlayerAction : ShopAction
	{
		[Property] public float Amount { get; set; } = 50f;
		[Property] public bool CanExceedMax { get; set; } = false;

		public override void Execute( GameObject player, ShopItemResource item )
		{
			if ( player?.Network?.Owner == null ) return;
			var steamId = player.Network.Owner.SteamId;
			var state = TrceServiceManager.Instance?.GetService<Trce.Kernel.Plugin.Services.IPlayerManagerService>()?.GetPlayer( steamId );
			if ( state == null ) return;

			float oldHealth = state.Health;
			state.Health = Math.Clamp( state.Health + Amount, 0f, CanExceedMax ? 9999f : state.MaxHealth );
			Log.Info( $"[ShopAction] Healed {player.Name}: {oldHealth:F0} -> {state.Health:F0}" );
		}
	}

	[GameResource( "TRCE Action: Broadcast", "trceaction", "Broadcast a message on purchase" )]
	public class BroadcastPurchaseAction : ShopAction
	{
		[Property, TextArea] public string MessageTemplate { get; set; } = "&ePlayer &f%player% &ebought &a%item%&e!";

		public override void Execute( GameObject player, ShopItemResource item )
		{
			if ( player?.Network?.Owner == null ) return;
			var chat = player.Scene.GetAllComponents<ChatManager>().FirstOrDefault();
			if ( chat == null ) return;

			string msg = MessageTemplate
				.Replace( "%player%", player.Network.Owner.DisplayName )
				.Replace( "%item%", item?.ItemName ?? "Item" );

			chat.SendSystemMessage( msg );
		}
	}
}

