// File: Code/Kernel/Plugin/TrceServiceManager.cs
// Encoding: UTF-8 (No BOM)
// Phase 3: 服務註冊中心 (Service Locator) — TRCE 框架核心。
// 目標：消滅單例模式、提供 O(1) 查找、執行緒安全、Zero-GC 熱路徑。

using Sandbox;
using System;
using System.Collections.Concurrent;

namespace Trce.Kernel.Plugin
{
	/// <summary>
	/// <para>【Phase 3 — 服務註冊中心 (Service Locator)】</para>
	/// <para>
	/// 繼承自 <see cref="GameObjectSystem"/>，由 s&amp;box 引擎保證每個 Scene 中唯一且自動實例化。
	/// 這取代了所有使用靜態 <c>Instance</c> 的單例模式 (Singleton Pattern)，
	/// 是 TRCE 插件生態系的依賴注入核心。
	/// </para>
	/// <para>
	/// <b>架構設計原則：</b><br/>
	/// - <b>O(1) 查找：</b>使用 <see cref="Dictionary{TKey, TValue}"/> 以 <see cref="Type"/> 為鍵進行哈希查找。<br/>
	/// - <b>執行緒安全：</b>所有讀寫操作均透過 <c>lock</c> 保護，防止非同步初始化造成的競爭條件 (Race Condition)。<br/>
	/// - <b>Zero-GC 熱路徑：</b><see cref="GetService{T}"/> 在命中路徑上無記憶體分配。<br/>
	///   所有服務必須為 <c>class</c> 類型（引用類型），避免裝箱 (Boxing) 到 <c>object</c>。<br/>
	/// - <b>無反射：</b>所有操作均透過 C# 泛型靜態分派完成，無任何 Reflection 呼叫。<br/>
	/// </para>
	/// <para>
	/// <b>使用範例：</b><br/>
	/// <code>
	/// // 在服務自身的 OnStart 中註冊：
	/// TrceServiceManager.Instance?.RegisterService&lt;IInventoryService&gt;(this);
	///
	/// // 在任意 Component 中查詢：
	/// var inventory = TrceServiceManager.Instance?.GetService&lt;IInventoryService&gt;();
	/// </code>
	/// </para>
	/// </summary>
	public sealed class TrceServiceManager : GameObjectSystem
	{
		// ─────────────────────────────────────────────
		//  靜態存取點 (Static Access Point)
		// ─────────────────────────────────────────────

		/// <summary>
		/// 取得當前 Scene 的 <see cref="TrceServiceManager"/> 實例。
		/// <para>由 <see cref="GameObjectSystem"/> 基底類別的生命週期保證此值在 Scene 啟動後有效。</para>
		/// </summary>
		public static TrceServiceManager Instance { get; private set; }

		// ─────────────────────────────────────────────
		//  內部儲存 (Internal Storage)
		// ─────────────────────────────────────────────

		/// <summary>
		/// 服務字典，以服務的公開合約類型 (Interface 或 Class) 為鍵，服務實例為值。
		/// <para>注意：字典儲存 <c>object</c>，但所有查詢透過泛型進行，不會在 GetService 路徑上產生 Boxing。</para>
		/// </summary>
		// P2-B: 改用 ConcurrentDictionary — GetService 熱路徑無鎖讀取，_lock 已不再需要。
		private readonly ConcurrentDictionary<Type, object> _services = new();

		// ─────────────────────────────────────────────
		//  建構子 & 生命週期
		// ─────────────────────────────────────────────

		/// <summary>
		/// 由 s&amp;box 引擎自動呼叫的建構子。繼承 <see cref="GameObjectSystem"/> 確保此類別為 Scene 層級的唯一系統。
		/// </summary>
		/// <param name="scene">此系統所屬的場景實例。</param>
		public TrceServiceManager( Scene scene ) : base( scene )
		{
			Instance = this;
			Log.Info( "🗂️ [TrceServiceManager] Service Locator initialized." );
		}

		// ─────────────────────────────────────────────
		//  公開 API (Public API)
		// ─────────────────────────────────────────────

		/// <summary>
		/// 以類型 <typeparamref name="T"/> 為鍵，向服務中心註冊一個服務實例。
		/// <para>
		/// <b>覆蓋行為：</b>若相同類型的服務已存在，新實例將覆蓋舊實例。
		/// 此設計允許插件更換預設服務的實作 (例如用高效版本替換預設版本)，
		/// 並輸出 <see cref="Log.Info"/> 以方便偵錯。
		/// </para>
		/// <para>此方法為一次性設定成本，<b>不在熱路徑 (Hot Path) 上</b>，允許鎖定操作。</para>
		/// </summary>
		/// <typeparam name="T">服務的公開合約類型 (建議為 Interface，例如 <c>IInventoryService</c>)。必須為 class。</typeparam>
		/// <param name="serviceInstance">要註冊的服務實例，不可為 null。</param>
		/// <exception cref="ArgumentNullException">當 <paramref name="serviceInstance"/> 為 null 時拋出。</exception>
		public void RegisterService<T>( T serviceInstance ) where T : class
		{
			if ( serviceInstance is null )
				throw new ArgumentNullException( nameof(serviceInstance), $"[TrceServiceManager] Cannot register a null instance for service '{typeof(T).Name}'." );

			var serviceType = typeof(T);
			var wasReplaced = _services.ContainsKey( serviceType );
			_services[serviceType] = serviceInstance;  // ConcurrentDictionary indexer 是原子操作

			if ( wasReplaced )
				Log.Info( $"🔄 [TrceServiceManager] Service '{serviceType.Name}' is being REPLACED by a new instance. This is intentional if a plugin is upgrading the service." );

			Log.Info( $"✅ [TrceServiceManager] Registered service: '{serviceType.Name}' → {serviceInstance.GetType().Name}" );
		}

		/// <summary>
		/// 從服務中心移除以類型 <typeparamref name="T"/> 為鍵的服務。
		/// <para>若服務不存在，此方法靜默地無操作 (No-Op)。</para>
		/// <para>此方法為一次性操作，<b>不在熱路徑 (Hot Path) 上</b>，允許鎖定操作。</para>
		/// </summary>
		/// <typeparam name="T">要移除的服務的公開合約類型。</typeparam>
		public void UnregisterService<T>() where T : class
		{
			if ( _services.TryRemove( typeof(T), out _ ) )
				Log.Info( $"🗑️ [TrceServiceManager] Unregistered service: '{typeof(T).Name}'" );
		}

		/// <summary>
		/// <para>【Zero-GC 熱路徑】安全地查詢並回傳類型 <typeparamref name="T"/> 的服務實例。</para>
		/// <para>
		/// <b>效能分析：</b><br/>
		/// - 命中路徑 (Hit Path)：一次 <see cref="Dictionary{TKey,TValue}.TryGetValue"/> 哈希查找 + 一次引用類型強制轉型，<b>零 GC Allocation</b>。<br/>
		/// - 未命中路徑 (Miss Path)：同上，額外回傳 <c>null</c>，仍為零分配。<br/>
		/// - 注意：<c>lock</c> 在讀操作上有輕微的效能開銷。若日後效能分析 (Profiling) 顯示此為瓶頸，
		///   可升級至 <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/> 或無鎖讀取模式。
		/// </para>
		/// </summary>
		/// <typeparam name="T">要查詢的服務的公開合約類型。必須為 class。</typeparam>
		/// <returns>
		/// 若服務已註冊，回傳服務實例；否則回傳 <c>null</c>。
		/// 永遠不會拋出例外。
		/// </returns>
		public T GetService<T>() where T : class
		{
			// P2-B: ConcurrentDictionary.TryGetValue 為無鎖讀取，O(1) 哈希查找，Zero-GC Allocation。
			return _services.TryGetValue( typeof(T), out var raw ) ? (T)raw : null;
		}

		/// <summary>
		/// 清空所有已註冊的服務。
		/// <para>通常在場景卸載或測試環境重置時呼叫。</para>
		/// </summary>
		public void ClearAll()
		{
			_services.Clear();
			Log.Info( "🧹 [TrceServiceManager] All services have been cleared." );
		}
	}
}
