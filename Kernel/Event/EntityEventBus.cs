// File: Code/Kernel/Event/EntityEventBus.cs
// Encoding: UTF-8 (No BOM)

using System;
using System.Collections.Generic;
using Sandbox;

namespace Trce.Kernel.Event
{
	/// <summary>
	/// <para>【Zero-Allocation 個體事件總線】</para>
	/// <para>
	/// 此 <see cref="Component"/> 掛載於特定 <see cref="GameObject"/>（例如玩家、NPC、實體）上，
	/// 提供作用域限定於該物件的事件訂閱與發布機制。
	/// </para>
	/// <para>
	/// <b>架構設計說明：</b><br/>
	/// 與 <see cref="GlobalEventBus"/> 的靜態泛型不同，個體總線需要以實例為單位儲存委派，
	/// 因此內部採用 <c>Dictionary&lt;Type, object&gt;</c> 作為委派儲存槽。<br/>
	/// <br/>
	/// <b>為何不會 Boxing？</b><br/>
	/// <c>Action&lt;TEvent&gt;</c> 是 <b>參考類型 (Reference Type)</b>，將其轉型為 <c>object</c>
	/// 並儲存，以及從 <c>object</c> 轉型取回，皆是 <b>參考轉型 (Reference Cast)</b>，
	/// 而非值類型的裝箱 (Boxing)。事件 Payload (<typeparamref name="TEvent"/>) 本身受
	/// <c>struct</c> 約束，在委派呼叫時以值傳遞，亦不發生 Boxing。
	/// </para>
	/// <para>
	/// <b>效能分析：</b><br/>
	/// - <b>Subscribe/Unsubscribe：</b>O(1) 平均（Dictionary 查找）。<br/>
	/// - <b>Publish：</b>O(1) 平均（一次 Dictionary 查找 + 一次 reference cast + Delegate Invoke）。<br/>
	/// - <b>GC Pressure：</b>僅在首次 Subscribe 新事件類型時產生 <c>Action&lt;TEvent&gt;</c>
	///   Delegate 物件（一次性成本），熱路徑 (Hot Path) 的 Publish 零分配。
	/// </para>
	/// </summary>
	public sealed class EntityEventBus : Component
	{
		/// <summary>
		/// 委派儲存字典。Key 為事件類型的 <see cref="Type"/>，Value 為對應的
		/// <c>Action&lt;TEvent&gt;</c>（以 object 儲存，避免需要非泛型基底類別）。
		/// </summary>
		private readonly Dictionary<Type, object> _handlers = new();

		/// <summary>
		/// 向此個體事件總線訂閱一個事件。
		/// <para>作用域僅限於此 Component 所掛載的 <see cref="GameObject"/>。</para>
		/// </summary>
		/// <typeparam name="TEvent">事件類型，必須是 <c>readonly struct</c> 且實作 <see cref="ITrceEvent"/>。</typeparam>
		/// <param name="handler">事件觸發時呼叫的回呼函式。</param>
		/// <exception cref="ArgumentNullException">當 <paramref name="handler"/> 為 null 時拋出。</exception>
		public void Subscribe<TEvent>(Action<TEvent> handler)
			where TEvent : struct, ITrceEvent
		{
			if (handler is null)
				throw new ArgumentNullException(nameof(handler));

			var key = typeof(TEvent);

			if (_handlers.TryGetValue(key, out var existing))
			{
				// existing 是一個 Action<TEvent>（參考類型），此處為參考轉型，非 Boxing。
				var existingTyped = (Action<TEvent>)existing;
				_handlers[key] = existingTyped + handler;
			}
			else
			{
				_handlers[key] = handler;
			}
		}

		/// <summary>
		/// 從此個體事件總線取消訂閱一個事件。
		/// </summary>
		/// <typeparam name="TEvent">事件類型。</typeparam>
		/// <param name="handler">要取消訂閱的回呼函式（須與訂閱時為同一實例）。</param>
		public void Unsubscribe<TEvent>(Action<TEvent> handler)
			where TEvent : struct, ITrceEvent
		{
			if (handler is null)
				return;

			var key = typeof(TEvent);

			if (!_handlers.TryGetValue(key, out var existing))
				return;

			// 參考轉型，非 Boxing。
			var existingTyped = (Action<TEvent>)existing;
			var updated = existingTyped - handler;

			if (updated is null)
				_handlers.Remove(key);
			else
				_handlers[key] = updated;
		}

		/// <summary>
		/// 向此個體總線的所有訂閱者發布一個事件。
		/// <para>
		/// <b>【Zero-Allocation 熱路徑 (Hot Path)】</b><br/>
		/// - 一次 Dictionary 查找（O(1) 平均）。<br/>
		/// - 一次參考轉型（Reference Cast，非 Boxing）。<br/>
		/// - <typeparamref name="TEvent"/> 值傳遞，不發生 Boxing。<br/>
		/// - 無 LINQ，無動態記憶體分配。
		/// </para>
		/// </summary>
		/// <typeparam name="TEvent">事件類型。</typeparam>
		/// <param name="eventData">要發布的事件資料（值類型，存在於 Stack 上）。</param>
		public void Publish<TEvent>(TEvent eventData)
			where TEvent : struct, ITrceEvent
		{
			var key = typeof(TEvent);

			if (!_handlers.TryGetValue(key, out var existing))
				return;

			// 參考轉型（Reference Cast），非 Boxing。
			// eventData 以值傳遞至 Invoke，泛型約束 struct 保證不裝箱。
			((Action<TEvent>)existing).Invoke(eventData);
		}

		/// <summary>
		/// 清除此個體總線上特定事件類型的所有訂閱。
		/// </summary>
		/// <typeparam name="TEvent">要清除的事件類型。</typeparam>
		public void ClearAll<TEvent>()
			where TEvent : struct, ITrceEvent
		{
			_handlers.Remove(typeof(TEvent));
		}

		/// <summary>
		/// 清除此個體總線上所有事件類型的所有訂閱。
		/// <para>通常在 GameObject 銷毀前或場景切換時呼叫，防止 Stale Delegate 懸掛。</para>
		/// </summary>
		public void ClearAll()
		{
			_handlers.Clear();
		}

		/// <summary>
		/// 當此 Component 從 <see cref="GameObject"/> 上移除或 GameObject 被銷毀時，
		/// 自動清除所有委派，防止記憶體洩漏。
		/// </summary>
		protected override void OnDestroy()
		{
			_handlers.Clear();
		}
	}
}
