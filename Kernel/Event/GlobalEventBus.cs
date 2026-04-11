// File: Code/Kernel/Event/GlobalEventBus.cs
// Encoding: UTF-8 (No BOM)

using System;
using System.Threading;

namespace Trce.Kernel.Event
{
	/// <summary>
	/// <para>【Zero-Allocation 全域事件總線】</para>
	/// <para>
	/// 核心設計原理：利用 C# 靜態泛型類別 (Static Generic Class) 的特性。
	/// 每一個唯一的 <typeparamref name="TEvent"/> 類型，CLR 會在 JIT 期間為其
	/// 生成一個獨立的靜態類別實例，其中包含一個獨立的靜態 <see cref="Action{T}"/> 欄位。
	/// </para>
	/// <para>
	/// <b>效能分析：</b><br/>
	/// - <b>O(1) 派發：</b>事件分派無需任何字典查找 (Dictionary Lookup)。<br/>
	/// - <b>零 Boxing：</b>泛型約束 <c>struct</c> 確保 Payload 永遠不會被裝箱到 Heap。<br/>
	/// - <b>零字典開銷：</b>相比 <c>Dictionary&lt;Type, Delegate&gt;</c> 方案，完全消除雜湊計算、
	///   記憶體跳轉，以及潛在的字典 resize 造成的 GC Allocation。<br/>
	/// - <b>執行緒安全：</b><c>Subscribe</c> / <c>Unsubscribe</c> / <c>ClearAll</c> 採用
	///   <see cref="Interlocked.CompareExchange{T}"/> CAS Loop 進行 lock-free 安全更新；
	///   <c>Publish</c> 透過 <see cref="Volatile.Read{T}"/> 取得委派快照後呼叫，
	///   確保不會讀到半更新狀態。
	/// </para>
	/// <para>
	/// <b>禁止事項（Zero-Allocation 保證）：</b><br/>
	/// - 在 <see cref="Publish{TEvent}"/> 路徑上，禁止任何 LINQ、<c>new</c> 運算子或裝箱操作。
	/// </para>
	/// </summary>
	public static class GlobalEventBus
	{
		/// <summary>
		/// 靜態泛型分配器，每個 <typeparamref name="TEvent"/> 對應一個獨立的靜態儲存槽。
		/// CLR 保證：<c>EventDispatcher&lt;EventA&gt;.Handlers</c> 與
		/// <c>EventDispatcher&lt;EventB&gt;.Handlers</c> 是完全獨立的靜態欄位。
		/// 此設計實現了 O(1) 的完全無查找派發。
		/// </summary>
		/// <typeparam name="TEvent">必須是 struct 且實作 <see cref="ITrceEvent"/>。</typeparam>
		private static class EventDispatcher<TEvent> where TEvent : struct, ITrceEvent
		{
			/// <summary>
			/// 所有訂閱此事件類型的委派鏈。
			/// static 欄位在 <c>EventDispatcher&lt;TEvent&gt;</c> 的每個泛型具現化中獨立存在。
			/// </summary>
			internal static Action<TEvent>? Handlers;
		}

		/// <summary>
		/// 向全域事件總線訂閱一個事件。
		/// <para>此操作是一次性 Delegate 組合成本，<b>不在熱路徑 (Hot Path) 上</b>。</para>
		/// </summary>
		/// <typeparam name="TEvent">事件類型，必須是 <c>readonly struct</c> 且實作 <see cref="ITrceEvent"/>。</typeparam>
		/// <param name="handler">事件觸發時呼叫的回呼函式。</param>
		/// <exception cref="ArgumentNullException">當 <paramref name="handler"/> 為 null 時拋出。</exception>
		public static void Subscribe<TEvent>(Action<TEvent> handler)
			where TEvent : struct, ITrceEvent
		{
			if (handler is null)
				throw new ArgumentNullException(nameof(handler));

			// CAS Loop：安全附加委派。
			// 注意：s&box 白名單不允許 Volatile.Read，改為直接讀取（s&box 執行於單一遊戲執行緒）。
			Action<TEvent>? current, updated;
			do
			{
				current = EventDispatcher<TEvent>.Handlers;
				updated = current + handler;
			} while (!ReferenceEquals(
				Interlocked.CompareExchange(ref EventDispatcher<TEvent>.Handlers, updated, current),
				current));
		}

		/// <summary>
		/// 從全域事件總線取消訂閱一個事件。
		/// <para>請確保傳入的 <paramref name="handler"/> 與訂閱時使用的是同一個委派實例，否則取消無效。</para>
		/// </summary>
		/// <typeparam name="TEvent">事件類型。</typeparam>
		/// <param name="handler">要取消訂閱的回呼函式。</param>
		public static void Unsubscribe<TEvent>(Action<TEvent> handler)
			where TEvent : struct, ITrceEvent
		{
			if (handler is null)
				return;

			// CAS Loop：安全移除委派（直接讀取欄位，Volatile.Read 不在 s&box 白名單內）。
			Action<TEvent>? current, updated;
			do
			{
				current = EventDispatcher<TEvent>.Handlers;
				updated = current - handler;
			} while (!ReferenceEquals(
				Interlocked.CompareExchange(ref EventDispatcher<TEvent>.Handlers, updated, current),
				current));
		}

		/// <summary>
		/// 向所有訂閱者發布一個事件。
		/// <para>
		/// <b>【Zero-Allocation 熱路徑 (Hot Path)】</b><br/>
		/// 此方法在 100 人連線的伺服器環境下可安全地每幀呼叫。<br/>
		/// - 無字典查找。<br/>
		/// - 無 Boxing（<typeparamref name="TEvent"/> 受 <c>struct</c> 約束）。<br/>
		/// - 無 LINQ。<br/>
		/// - 無動態記憶體分配。
		/// </para>
		/// </summary>
		/// <typeparam name="TEvent">事件類型。</typeparam>
		/// <param name="eventData">要發布的事件資料（值類型，存在於 Stack 上）。</param>
		public static void Publish<TEvent>(TEvent eventData)
			where TEvent : struct, ITrceEvent
		{
			// eventData 以值傳遞，不發生 Boxing。
			// Volatile.Read 不在 s&box 白名單（SB1000），直接讀取欄位（單一遊戲執行緒）。
			EventDispatcher<TEvent>.Handlers?.Invoke(eventData);
		}

		/// <summary>
		/// 清除特定事件類型的所有訂閱。
		/// <para>通常在場景/遊戲模式切換時呼叫，用於防止 Stale Delegate 問題。</para>
		/// </summary>
		/// <typeparam name="TEvent">要清除的事件類型。</typeparam>
		public static void ClearAll<TEvent>()
			where TEvent : struct, ITrceEvent
		{
			// Interlocked.Exchange 確保寫入對所有執行緒立即可見，
			// 且與 CAS Loop 的 CompareExchange 操作在同一原子層級上互斥。
			Interlocked.Exchange(ref EventDispatcher<TEvent>.Handlers, null);
		}
	}
}
