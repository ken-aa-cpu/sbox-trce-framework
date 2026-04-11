using Sandbox;
using System.Collections.Generic;
using Trce.Kernel.Plugin.Services;

namespace Trce.Plugins.Finance

{
	/// <summary> 商店商品分類 (用來在商店 UI 中過濾商品) </summary>
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

	/// <summary> 定義購買商品的先決條件 (供 GUI 或邏輯判斷使用) </summary>
	public abstract class ShopRequirement : GameResource
	{
		public abstract bool IsMet( PurchaseContext ctx );
		public abstract string GetFailureReason( PurchaseContext ctx );
	}

	/// <summary> 定義購買商品後執行的各項動作 (對應前端的按鈕事件或後端分配資源) </summary>
	public abstract class ShopAction : GameResource
	{
		/// <summary> 執行具體邏輯 (給予道具、扣款等) </summary>
		public abstract void Execute( GameObject player, ShopItemResource item );
	}

	/// <summary>
	/// 商店商品的基礎定義資源 (Blueprint)
	/// 允許透過 s&box 編輯器進行資料配置
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
		/// <summary> 購買此商品所需滿足的條件列表 </summary>
		[Property, Group("Logic")] public List<ShopRequirement> Requirements { get; set; } = new();
		/// <summary> 購買後會觸發的行為處理列表 </summary>
		[Property, Group("Logic")] public List<ShopAction> Actions { get; set; } = new();
	}

	/// <summary>
	/// 商品在商店中實際的架上清單及庫存狀態 (Listing)
	/// 支援玩家之間交易或無限庫存的官方商店
	/// </summary>
	public class ShopListing
	{
		public string ListingId { get; set; }
		public ShopItemResource Template { get; set; }

		/// <summary> 賣家的 SteamID (0 代表系統官方商品) </summary>
		public ulong SellerId { get; set; } = 0;
		public float PriceOverride { get; set; }
		public int CurrentStock { get; set; } = -1;

		/// <summary> 商店標籤，決定此商品在哪種類型的商店顯示 (例如 "general", "medic", "player_stall") </summary>
		public string ShopTag { get; set; } = "general";
		public bool IsInfinite => CurrentStock < 0;
		public bool IsPlayerListing => SellerId > 0;
	}

	/// <summary> 購買交易當下的環境與資料，用於傳遞資訊給先決條件與行為動作 </summary>
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
	/// 介面: 用於在核心機制之外，動態修改交易內容 (例如: 特殊事件降價、技能減免等機制)
	/// </summary>
	public interface IShopModifier
	{
		void OnModifyPrice( PurchaseContext ctx );
		void OnValidate( PurchaseContext ctx );
	}

}


