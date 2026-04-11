// File: Code/Kernel/Event/ITrceEvent.cs
// Encoding: UTF-8 (No BOM)

namespace Trce.Kernel.Event
{
	/// <summary>
	/// <para>【Zero-Allocation 架構】TRCE 事件系統的根標記介面。</para>
	/// <para>
	/// 所有事件負載 (Payload) 必須同時實作此介面並宣告為 <c>readonly struct</c>。
	/// 這確保了事件資料 100% 分配於 Stack 上，完全避免 Heap Allocation 與 GC 壓力。
	/// </para>
	/// <para>
	/// <b>強制規範：</b>實作者必須宣告為 <c>readonly struct</c>，違者將在
	/// <see cref="GlobalEventBus"/> 與 <see cref="EntityEventBus"/> 的泛型約束
	/// (<c>where TEvent : struct, ITrceEvent</c>) 處產生編譯錯誤，由編譯器強制執行此規範。
	/// </para>
	/// <example>
	/// <code>
	/// // ✅ 正確用法
	/// public readonly struct PlayerDamagedEvent : ITrceEvent
	/// {
	///     public readonly int AttackerId;
	///     public readonly float Damage;
	///     public PlayerDamagedEvent(int attackerId, float damage)
	///     {
	///         AttackerId = attackerId;
	///         Damage = damage;
	///     }
	/// }
	///
	/// // ❌ 錯誤用法（class 無法通過泛型約束 where TEvent : struct）
	/// public class WrongEvent : ITrceEvent { }
	/// </code>
	/// </example>
	/// </summary>
	public interface ITrceEvent { }
}
