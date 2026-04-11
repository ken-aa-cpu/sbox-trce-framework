using Sandbox;
using Trce.Kernel.Plugin.Interaction;
using Trce.Kernel.Bridge;
using Trce.Game.Player; // TrcePlayer
using Trce.Plugins.Finance; // ShopManager

namespace Trce.Plugins.Shared.Npc
{
	/// <summary>
	///   / Citizen Trait: Merchant (Merchant Trait)
	///   / Attach this component to any GameObject with a Collider to turn it into a shop NPC.
	/// </summary>
	[Title("Citizen Trait: Merchant")]
	[Category("TRCE NPCs")]
	[Icon("store")]
	public class CitizenMerchantTrait : Component, IInteractable
	{
		[Property, Description("Merchant display name")]
		public string MerchantName { get; set; } = "Black Market Merchant";

		// ====================================================================
		// IInteractable Implementation
		// ====================================================================

		public string InteractionLabel => $"Trade with {MerchantName}";

		public bool CanInteract( GameObject user )
		{
			// Allow interaction as long as player is alive; add distance/faction checks here
			return true;
		}

		public void OnInteract( GameObject user )
		{
			// Get the player from the interaction source
			var player = user.Components.GetInAncestorsOrSelf<TrcePlayer>();
			ulong steamId = player?.SteamId ?? 0;

			if ( steamId == 0 ) return;

			Log.Info( $"[Merchant Trait] Player {steamId} is interacting with {MerchantName}." );

			// Resolve the player's network Connection then signal the static ShopManager event.
			// TrceShopUI subscribes to ShopManager.OnShopRequested and will open the shop
			// only when the fired Connection matches Connection.Local on the client side.
			var conn = SandboxBridge.Instance?.FindConnectionBySteamId( steamId );
			if ( conn == null )
			{
				Log.Warning( $"[Merchant Trait] Cannot find Connection for SteamID {steamId}." );
				return;
			}

			ShopManager.OnShopRequested?.Invoke( conn );
		}
	}
}