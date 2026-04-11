// File: Code/Kernel/Stats/IAttributeService.cs
// Encoding: UTF-8 (No BOM)
// Phase 2: 通用屬性服務合約 — TRCE 數值插座系統 (Numeric Socket System)。

using System;

namespace Trce.Kernel.Stats;

/// <summary>
/// <para>【Phase 2 — 通用屬性服務公開合約 (Numeric Socket)】</para>
/// <para>
/// 提供「數值插座 (Numeric Socket)」模式的通用實體屬性系統。任何插件均可透過此合約
/// 定義實體的浮點屬性（如移動速度 <c>"player.move_speed"</c>、最大生命值 <c>"player.max_health"</c>）
/// 並透過 <see cref="AttributeModifier"/> 在不修改核心程式碼的情況下自由疊加與修飾數值，
/// 實現完全解耦的數值系統。
/// </para>
/// <para>
/// <b>核心計算公式：</b><br/>
/// <c>最終值 = (基礎值 + Σ所有 Add 型修飾符) × Π所有 Multiply 型修飾符</c>
/// </para>
/// <para>
/// <b>效能保證：</b><br/>
/// 實作層必須採用「臟標記 (Dirty Flag)」快取機制，確保在未發生修飾符變動時，
/// <see cref="GetTotalValue"/> 的呼叫為 O(1) 直接回傳，不重複計算。
/// </para>
/// </summary>
public interface IAttributeService
{
	/// <summary>
	/// 計算並回傳指定實體屬性的最終值。
	/// <para>
	/// <b>【效能保證 — O(1) 快取命中】</b>：內部實作採用臟標記快取機制。
	/// 若自上次修改後未發生任何變動，此呼叫直接回傳快取值，無任何重新計算開銷。
	/// </para>
	/// </summary>
	float GetTotalValue( ulong steamId, string attrId );

	/// <summary>
	/// 設定指定實體屬性的基礎值 (Base Value)，並在值發生改變時觸發屬性變更事件。
	/// </summary>
	void SetBaseValue( ulong steamId, string attrId, float value );

	/// <summary>
	/// 向指定實體的屬性添加一個 <see cref="AttributeModifier"/>，並回傳可用於後續移除的唯一識別碼。
	/// </summary>
	Guid AddModifier( ulong steamId, string attrId, AttributeModifier modifier );

	/// <summary>
	/// 依唯一識別碼移除指定實體屬性上的一個 <see cref="AttributeModifier"/>。
	/// </summary>
	void RemoveModifier( ulong steamId, string attrId, Guid modifierId );
}
