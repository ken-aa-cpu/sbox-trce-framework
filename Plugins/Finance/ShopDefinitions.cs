// ?��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��?
// ����������������������������������������������������������������������������������������������������������������������������������������
// ��  Copyright (c) 2026 TRCE Team. All rights reserved.            ��
// ��  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ��
// ?��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��?
using Sandbox;
using System.Collections.Generic;
using Trce.Kernel.Plugin.Services;

namespace Trce.Plugins.Finance

{
	/// <summary>
	/// / ? ?? ?
	/// </summary>
	public enum ShopCategory
	{
		Weapon,
		Ammo,
		Healing,
		Utility,
		Special,
		Cosmetic,
		Food
	}

	/// <summary>
	///   /  ? ???(GUI ? )
	/// </summary>
	public abstract class ShopRequirement : GameResource
	{
		public abstract bool IsMet( PurchaseContext ctx );
		public abstract string GetFailureReason( PurchaseContext ctx );
	}

	/// <summary>
	///   / ? ? ? ? ???(GUI ? )
	/// </summary>
	public abstract class ShopAction : GameResource
	{
		/// <summary> ? ? ?? ?</summary>
		public abstract void Execute( GameObject player, ShopItemResource item );
	}

	/// <summary>
	/// / ? ?? ?  (Blueprint)
	/// /  ? ?? ? ?? ?? ???
	/// </summary>
	[GameResource( "TRCE Shop Item", "trceshop", "Definition of a shop item" )]
	public class ShopItemResource : GameResource
	{
		[Property, Group("Display")] public string ItemName { get; set; } = "New Item";
		[Property, Group("Display"), TextArea] public string Description { get; set; } = "Item description...";
		[Property, Group("Display")] public Sandbox.Texture Icon { get; set; }
		[Property, Group("Display")] public ShopCategory Category { get; set; } = ShopCategory.Utility;
		[Property, Group("Pricing")] public CurrencyType DefaultCurrency { get; set; } = CurrencyType.TracePoint;
		[Property, Group("Pricing")] public float BasePrice { get; set; } = 100f;
		[Property, Group("Logic")] public bool IsEnabled { get; set; } = true;
		[Property, Group("Logic")] public int DefaultStock { get; set; } = -1;
		[Property, Group("Logic")] public int SortOrder { get; set; } = 100;
		/// <summary> ? ? ?? ?? ?</summary>
		[Property, Group("Logic")] public List<ShopRequirement> Requirements { get; set; } = new();
		/// <summary> ?? ??</summary>
		[Property, Group("Logic")] public List<ShopAction> Actions { get; set; } = new();
	}

	/// <summary>
	///   / ? ?? ?? ? ?? ???(Listing)
	/// / ? ?? ? ? ?
	/// </summary>
	public class ShopListing
	{
		public string ListingId { get; set; }
		public ShopItemResource Template { get; set; }

		/// <summary> 0  ? ?SteamID</summary>
		public ulong SellerId { get; set; } = 0;
		public float PriceOverride { get; set; }
		public int CurrentStock { get; set; } = -1;

		/// <summary> ? ? ? ?( ?: "general", "medic", "player_stall")</summary>
		public string ShopTag { get; set; } = "general";
		public bool IsInfinite => CurrentStock < 0;
		public bool IsPlayerListing => SellerId > 0;
	}

	/// <summary>
	///   /  ? ???-  ? ?
	/// </summary>
	public class PurchaseContext
	{
		public ulong BuyerId { get; set; }
		public ShopListing Listing { get; set; }

		public float FinalPrice { get; set; }
		public bool Cancelled { get; set; }
		public string CancelReason { get; set; }

		public Dictionary<string, object> Metadata { get; set; } = new();
	}

	/// <summary>
	/// 管�??�濾??- 供代碼層級�??�擴??(例�??��??�扣)
	/// </summary>
	public interface IShopModifier
	{
		void OnModifyPrice( PurchaseContext ctx );
		void OnValidate( PurchaseContext ctx );
	}

}


