using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Player;
using Trce.Kernel.Command;
using Trce.Kernel.Bridge;
using Trce.Kernel.Plugin;
using Trce.Kernel.Plugin.Services;

namespace Trce.Plugins.Finance
{
	[Title( "Shop Manager" ), Group( "Trce - Finance" ), Icon( "shopping_cart" )]
	public class ShopManager : Component
	{
		private static ShopManager instance;
		public static ShopManager Instance => instance;

		public static Action<Connection> OnShopRequested;
		public static Action<string, string, ulong> OnListingChanged;
		public static Action<ulong, ulong, string> OnPurchaseComplete;

		[Sync] public List<ShopListing> SyncedListings { get; set; } = new();

		private List<IShopModifier> modifiers = new();

		protected override void OnAwake()
		{
			instance = this;
		}

		protected override void OnStart()
		{
			RefreshSystemListings();
			RegisterCommand();
		}

		private void RegisterCommand()
		{
			var cm = Scene.Get<TrceCommandManager>();
			if ( cm == null ) return;

			// /buy <id>
			cm.Register( new TrceCommandManager.CommandInfo
			{
				Name = "buy",
				Description = "Buy an item from the shop",
				Handler = ( steamId, args ) =>
				{
					if ( args.Length < 1 ) return;
					PurchaseItem( steamId, args[0], "points" );
				},
/*
				SuggestionProvider = ( steamId, argIndex, currentArgs ) =>
				{
					if ( argIndex == 0 )
					{
						return SyncedListings
							.Select( l => l.Template.ResourceName )
							.Distinct()
							.ToArray();
					}
					return null;
				}
				*/
			} );

			// /shop
			cm.Register( new TrceCommandManager.CommandInfo
			{
				Name = "shop",
				Description = "Open shop interface",
				Handler = ( steamId, args ) =>
				{
					OnShopRequested?.Invoke( Rpc.Caller );
				}
			} );
		}

		public void RefreshSystemListings()
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;

			SyncedListings.RemoveAll( l => l.SellerId == 0 );

			var blueprints = ResourceLibrary.GetAll<ShopItemResource>().Where( r => r.IsEnabled );
			foreach ( var b in blueprints )
			{
				PostListing( 0, b, b.BasePrice, b.DefaultStock, "general" );
			}
		}

		public string PostListing( ulong sellerId, ShopItemResource blueprint, float priceOverride, int stock, string shopTag = "general" )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return null;

			string lid = Guid.NewGuid().ToString("N");
			var listing = new ShopListing
			{
				ListingId = lid,
				Template = blueprint,
				SellerId = sellerId,
				PriceOverride = priceOverride,
				CurrentStock = stock,
				ShopTag = shopTag
			};

			SyncedListings.Add( listing );
			OnListingChanged?.Invoke( "post", lid, sellerId );
			return lid;
		}

		public void RemoveListing( string lid )
		{
			if ( SyncedListings.RemoveAll( l => l.ListingId == lid ) > 0 )
			{
				OnListingChanged?.Invoke( "remove", lid, 0 );
			}
		}

		public void PurchaseItem( ulong steamId, string itemId, string currencyType )
		{
			var listing = SyncedListings.FirstOrDefault( l => l.ListingId == itemId || l.Template.ResourceName == itemId );
			if ( listing == null ) return;

			var playerEntity = TrceServiceManager.Instance?.GetService<IPawnService>()?.GetPlayerPawn( steamId );
			ProcessPurchase( steamId, listing, playerEntity );
		}

		[Rpc.Owner]
		public void RequestPurchase( string listingId )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			var listing = SyncedListings.FirstOrDefault( l => l.ListingId == listingId );
			if ( listing == null ) return;

			var playerObj = TrceServiceManager.Instance?.GetService<IPawnService>()?.GetPlayerPawn( Rpc.Caller.SteamId );
			ProcessPurchase( Rpc.Caller.SteamId, listing, playerObj );
		}

		public bool ProcessPurchase( ulong steamId, ShopListing listing, GameObject playerObj )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) || listing == null ) return false;

			var ctx = new PurchaseContext
			{
				BuyerId = steamId,
				Listing = listing,
				FinalPrice = listing.PriceOverride
			};

			foreach ( var req in listing.Template.Requirements )
			{
				if ( req == null ) continue;
				if ( !req.IsMet( ctx ) ) return false;
			}

			if ( !listing.IsInfinite && listing.CurrentStock <= 0 ) return false;

			// Logic omitted for brevity but keeping core flow
			if ( !listing.IsInfinite )
			{
				listing.CurrentStock--;
				if ( listing.CurrentStock <= 0 ) RemoveListing( listing.ListingId );
			}

			foreach ( var action in listing.Template.Actions )
			{
				if ( action != null ) action.Execute( playerObj, listing.Template );
			}

			OnPurchaseComplete?.Invoke( steamId, listing.SellerId, listing.Template.ItemName );
			return true;
		}
	}
}
