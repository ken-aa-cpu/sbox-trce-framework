using Sandbox;
using System.Collections.Generic;

namespace Trce.Plugins.Storage
{
	public enum ItemType
	{
		Weapon, Consumable, Tool, Clue, KeyItem, Cosmetic
	}

	public enum ItemRarity
	{
		Common, Rare, Epic, Legendary, Unique
	}

	/// <summary>
	///   / TRCE   (Data Asset)
	/// </summary>
	[GameResource( "TRCE Item", "item", "TRCE 物品定義資源文件" )]
	public class TrceItemDefinition : GameResource
	{
		[Property, Category( "基礎資訊" )] public string ItemId { get; set; } = "new_item";
		[Property, Category( "基礎資訊" )] public string DisplayName { get; set; } = "新物品";
		[Property, Category( "基礎資訊" ), TextArea] public string Description { get; set; } = "";
		[Property, Category( "基礎資訊" )] public string IconPath { get; set; } = "ui/icons/items/none.png";

		[Property, Category( "屬性" )] public ItemType Type { get; set; } = ItemType.Tool;
		[Property, Category( "屬性" )] public ItemRarity Rarity { get; set; } = ItemRarity.Common;
		[Property, Category( "屬性" )] public int MaxStack { get; set; } = 1;
		[Property, Category( "屬性" )] public float Weight { get; set; } = 1.0f;

		[Property, Category( "戰鬥" )] public float BaseDamage { get; set; } = 0f;
		[Property, Category( "戰鬥" )] public string DamageType { get; set; } = "physical";

		[Property, Category( "自定義資料" )] public List<TrceItemStat> Tags { get; set; } = new();

		[Property, Category( "行為" )] public string OnUseEvent { get; set; } = "";
		[Property, Category( "行為" )] public string OnInteractEvent { get; set; } = "";
		[Property, Category( "行為" )] public string LinkedSkillId { get; set; } = "";
	}

	public class TrceItemStat
	{
		[Property] public string Key { get; set; }
		[Property] public float Value { get; set; }
	}
}


