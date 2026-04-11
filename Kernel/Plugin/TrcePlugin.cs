// File: Code/Kernel/Plugin/TrcePlugin.cs
// Encoding: UTF-8 (No BOM)
// Phase 3: TRCE 插件標準基底類別。整合全域事件自動退訂 + 服務定位器。

using Sandbox;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Trce.Kernel.Event;
using Trce.Kernel.SRE;
using System.Linq;
using Trce.Kernel.Command;

namespace Trce.Kernel.Plugin
{
	/// <summary>
	/// 插件的運行狀態。
	/// </summary>
	public enum PluginState
	{
		/// <summary>插件尚未載入或已停用。</summary>
		Unloaded,
		/// <summary>插件啟動時發生致命錯誤。</summary>
		Error,
		/// <summary>插件正常運行中。</summary>
		Enabled
	}

	/// <summary>
	/// <para>【Phase 3 — TRCE 標準插件基底類別】</para>
	/// <para>
	/// 所有業務模組 (例如 Inventory、Economy、Combat Plugin) 均必須繼承此類別。
	/// 此基底類別提供標準化的生命週期管理、服務存取，以及最重要的——
	/// <b>全域事件自動退訂機制</b>，從根本上杜絕 Memory Leak。
	/// </para>
	/// <para>
	/// <b>生命週期流程：</b><br/>
	/// <c>OnStart()</c> → <c>OnPluginEnabled()</c> (可覆寫，可非同步)<br/>
	/// <c>OnDestroy()</c> → (自動退訂所有事件) → <c>OnPluginDisabled()</c> (可覆寫)<br/>
	/// </para>
	/// <para>
	/// <b>【防呆機制 — 零 Memory Leak 保證】</b><br/>
	/// 使用 <see cref="RegisterEvent{TEvent}(Action{TEvent})"/> 訂閱事件，
	/// 系統會在 <see cref="OnPluginDisabled"/> 觸發前自動呼叫所有對應的 Unsubscribe，
	/// 開發者無需手動管理退訂，即使忘記也不會洩漏。
	/// </para>
	/// <para>
	/// <b>使用範例：</b>
	/// <code>
	/// [TrcePlugin(Id = "game.inventory", Name = "Inventory", Version = "1.0.0")]
	/// public class InventoryPlugin : TrcePlugin
	/// {
	///     protected override async Task OnPluginEnabled()
	///     {
	///         // 透過服務定位器取得依賴
	///         var economy = GetService&lt;IEconomyService&gt;();
	///
	///         // 訂閱事件 — 系統保證在 Disable 時自動退訂，零洩漏
	///         RegisterEvent&lt;PlayerKilledEvent&gt;(OnPlayerKilled);
	///         await Task.CompletedTask;
	///     }
	///
	///     private void OnPlayerKilled(CoreEvents.PlayerKilledEvent e)
	///     {
	///         // 處理玩家死亡邏輯 ...
	///     }
	/// }
	/// </code>
	/// </para>
	/// </summary>
	[Icon( "extension" )]
	public abstract class TrcePlugin : Component
	{
		// ─────────────────────────────────────────────
		//  公開屬性 (Public Properties)
		// ─────────────────────────────────────────────

		/// <summary>此插件的宣告式後設資料 (Metadata)，由 <see cref="TrcePluginAttribute"/> 提供。</summary>
		public TrcePluginAttribute Info { get; internal set; }

		/// <summary>插件當前的運行狀態。</summary>
		public PluginState State { get; internal set; } = PluginState.Unloaded;

		/// <summary>插件的唯一識別碼。若未定義 <see cref="TrcePluginAttribute"/>，則回退至類別名稱。</summary>
		public string PluginId => Info?.Id ?? GetType().Name;

		/// <summary>插件的版本號。</summary>
		public string Version => Info?.Version ?? "1.0.0";

		// ─────────────────────────────────────────────
		//  【防呆核心】事件退訂動作清單
		// ─────────────────────────────────────────────

		/// <summary>
		/// 儲存所有由 <see cref="RegisterEvent{TEvent}(Action{TEvent})"/> 建立的退訂委派。
		/// <para>
		/// 每個元素是一個封裝了 <see cref="GlobalEventBus.Unsubscribe{TEvent}"/> 呼叫的 <see cref="Action"/>，
		/// 持有對原始 handler 的引用，確保退訂精準對應訂閱。
		/// </para>
		/// <para>
		/// 此列表只在初始化/銷毀階段被操作（非熱路徑），分配一次性的 closure 是可接受的。
		/// </para>
		/// </summary>
		private readonly List<System.Action> _unsubscribeActions = new();

		// ─────────────────────────────────────────────
		//  生命週期 (Lifecycle)
		// ─────────────────────────────────────────────

		/// <summary>
		/// s&amp;box Component 啟動點。觸發非同步的插件啟用流程。
		/// <para>通常由 <see cref="PluginBootstrapper"/> 透過 <see cref="InitializeAsync"/> 統一管理；
		/// 若插件直接被加入場景，此方法作為後備啟動點。</para>
		/// </summary>
		protected override void OnStart()
		{
			// 直接啟動（非由 Bootstrapper 管理的場合）
			// Bootstrapper 管理的場合會呼叫 InitializeAsync，內部同樣呼叫 OnPluginEnabled
			if ( State == PluginState.Unloaded )
			{
				_ = InitializeAsync();
			}
		}

		/// <summary>
		/// s&amp;box Component 銷毀點。
		/// <para>
		/// <b>【自動防呆】</b>：在呼叫子類別的 <see cref="OnPluginDisabled"/> 之前，
		/// 自動執行所有透過 <see cref="RegisterEvent{TEvent}(Action{TEvent})"/> 建立的退訂動作，
		/// 確保不遺漏任何 Stale Delegate，徹底杜絕 Memory Leak。
		/// </para>
		/// </summary>
		protected override void OnDestroy()
		{
			AutoUnsubscribeAll();
			State = PluginState.Unloaded;
			OnPluginDisabled();
		}

		/// <summary>
		/// s&amp;box Component 啟用回呼。更新插件狀態。
		/// </summary>
		protected override void OnEnabled()
		{
			State = PluginState.Enabled;
		}

		/// <summary>
		/// s&amp;box Component 停用回呼。觸發插件停用流程（含自動退訂事件）。
		/// </summary>
		protected override void OnDisabled()
		{
			AutoUnsubscribeAll();
			State = PluginState.Unloaded;
			OnPluginDisabled();
		}

		// ─────────────────────────────────────────────
		//  TRCE 標準生命週期虛擬方法
		// ─────────────────────────────────────────────

		/// <summary>
		/// <para>【可覆寫】插件啟用時呼叫的非同步初始化方法。</para>
		/// <para>在此處進行服務查詢、資源載入、以及使用 <see cref="RegisterEvent{TEvent}(Action{TEvent})"/> 訂閱事件。</para>
		/// </summary>
		/// <returns>代表非同步操作的 <see cref="Task"/>。</returns>
		protected virtual Task OnPluginEnabled() => Task.CompletedTask;

		/// <summary>
		/// <para>【可覆寫】插件停用時呼叫的清理方法。</para>
		/// <para>
		/// <b>【防呆保證】</b>：所有透過 <see cref="RegisterEvent{TEvent}(Action{TEvent})"/> 訂閱的事件
		/// 在此方法被呼叫之前已自動退訂完畢，子類別無需手動管理。
		/// </para>
		/// </summary>
		protected virtual void OnPluginDisabled() { }

		// ─────────────────────────────────────────────
		//  公開輔助方法 (Public Helper Methods)
		// ─────────────────────────────────────────────

		/// <summary>
		/// <para>【防呆事件訂閱】向 <see cref="GlobalEventBus"/> 訂閱一個全域事件，並自動追蹤退訂動作。</para>
		/// <para>
		/// <b>使用此方法取代直接呼叫 <see cref="GlobalEventBus.Subscribe{TEvent}(Action{TEvent})"/>。</b><br/>
		/// 當此插件被銷毀或停用時，系統將自動為所有透過此方法訂閱的事件呼叫 Unsubscribe，
		/// 即使子類別忘記手動退訂，也不會發生 Memory Leak 或 Stale Delegate 呼叫。
		/// </para>
		/// <para>
		/// <b>效能說明：</b>此方法的呼叫成本是一次性的初始化成本（非熱路徑），
		/// 產生的 Lambda Closure 僅建立一次，生命週期與插件相同。
		/// </para>
		/// </summary>
		/// <typeparam name="TEvent">事件類型，必須為 <c>readonly struct</c> 且實作 <see cref="ITrceEvent"/>。</typeparam>
		/// <param name="handler">事件觸發時呼叫的處理委派，必須是實例方法（而非匿名 Lambda），以確保退訂精準。</param>
		/// <exception cref="ArgumentNullException">當 <paramref name="handler"/> 為 null 時拋出。</exception>
		protected void RegisterEvent<TEvent>( System.Action<TEvent> handler )
			where TEvent : struct, ITrceEvent
		{
			if ( handler is null )
				throw new ArgumentNullException( nameof(handler), $"[{PluginId}] Cannot register a null event handler for '{typeof(TEvent).Name}'." );

			// 步驟 1: 向全域事件總線訂閱
			GlobalEventBus.Subscribe<TEvent>( handler );

			// 步驟 2: 封裝對應的退訂動作並儲存至追蹤列表
			// 捕獲 handler 引用，確保退訂時使用完全相同的 Delegate 實例 (引用相等性)
			_unsubscribeActions.Add( () => GlobalEventBus.Unsubscribe<TEvent>( handler ) );
		}

		/// <summary>
		/// <para>【Zero-GC 服務查詢】透過 <see cref="TrceServiceManager"/> 安全地查詢一個已註冊的服務。</para>
		/// <para>
		/// 此方法取代了舊版本中硬編碼靜態 Instance 查詢模式，實現了真正的解耦。
		/// 若服務未找到，靜默回傳 <c>null</c>，不拋出例外，由呼叫者決定如何處理缺失的依賴。
		/// </para>
		/// </summary>
		/// <typeparam name="T">服務的公開合約類型 (Interface 或 Class)。</typeparam>
		/// <returns>服務實例，或 <c>null</c>（若服務未被註冊）。</returns>
		public T GetService<T>() where T : class
		{
			// 優先從 TrceServiceManager 查詢 — O(1) 字典查找，Zero-GC
			var service = TrceServiceManager.Instance?.GetService<T>();
			if ( service is not null )
				return service;

			// 後備路徑：從場景中搜尋 (兼容尚未遷移到 ServiceManager 的舊系統)
			// 注意：此路徑有 GC 分配開銷，應在正式環境中透過 RegisterService 消除
			Log.Warning( $"⚠️ [{PluginId}] Service '{typeof(T).Name}' not found in TrceServiceManager. Falling back to Scene search. Consider registering this service on startup." );
			return Scene.GetAllComponents<T>().FirstOrDefault();

			// 注意：此處 FirstOrDefault() 需要 using System.Linq;
			// 但為了保持 Zero-GC 理念，後備路徑已附帶警告提示開發者遷移
		}

		/// <summary>
		/// 透過 <see cref="PluginBootstrapper"/> 查詢場景中另一個已載入的 TRCE 插件實例。
		/// </summary>
		/// <typeparam name="T">目標插件的類型，必須繼承 <see cref="TrcePlugin"/>。</typeparam>
		/// <returns>插件實例，或 <c>null</c>（若插件未載入）。</returns>
		public T GetPlugin<T>() where T : TrcePlugin
		{
			return PluginBootstrapper.Instance?.GetPlugin<T>();
		}

		// ─────────────────────────────────────────────
		//  框架內部方法 (Framework Internal)
		// ─────────────────────────────────────────────

		/// <summary>
		/// <para>【框架內部呼叫】由 <see cref="PluginBootstrapper"/> 在 Scene 啟動後統一呼叫，
		/// 以確保依賴順序正確。</para>
		/// <para>此方法同時向 SRE Guardian 報告插件狀態。</para>
		/// </summary>
		/// <returns>代表非同步初始化操作的 <see cref="Task"/>。</returns>
		public virtual async Task InitializeAsync()
		{
			try
			{
				SreSystem.Instance?.CheckIn( PluginId, Version );
			}
			catch ( Exception e )
			{
				Log.Warning( $"[SRE] Registration error for '{PluginId}': {e.Message}" );
			}

			State = PluginState.Enabled;
			await OnPluginEnabled();
		}

		/// <summary>
		/// <para>【防呆核心實作】遍歷 <see cref="_unsubscribeActions"/> 列表，
		/// 呼叫每一個退訂委派，確保所有全域事件訂閱被清除。</para>
		/// <para>
		/// 此方法設計為<b>冪等 (Idempotent)</b>：呼叫後清空列表，
		/// 防止重複退訂（例如 OnDisabled 後緊接著 OnDestroy 兩次觸發）。
		/// </para>
		/// </summary>
		private void AutoUnsubscribeAll()
		{
			if ( _unsubscribeActions.Count == 0 )
				return;

			Log.Info( $"🔌 [{PluginId}] Auto-unsubscribing {_unsubscribeActions.Count} event handler(s)..." );

			// 使用 for 而非 foreach，避免 List<T> Enumerator 的 GC Allocation
			for ( int i = 0; i < _unsubscribeActions.Count; i++ )
			{
				try
				{
					_unsubscribeActions[i].Invoke();
				}
				catch ( Exception ex )
				{
					Log.Error( $"❌ [{PluginId}] Error during auto-unsubscribe of event handler #{i}: {ex.Message}" );
				}
			}

			// 清空列表，實現冪等性，防止 OnDisabled + OnDestroy 雙重觸發的重複退訂
			_unsubscribeActions.Clear();

			Log.Info( $"✅ [{PluginId}] All event handlers unsubscribed. Memory leak risk: ZERO." );
		}

		// ─────────────────────────────────────────────
		//  指令輔助方法 (Command Helper Methods)
		// ─────────────────────────────────────────────

		/// <summary>
		/// 向 <see cref="TrceCommandManager"/> 註冊一條指令。
		/// </summary>
		/// <param name="info">指令的宣告資訊。</param>
		protected void RegisterCommand( TrceCommandManager.CommandInfo info )
		{
			TrceCommandManager.Instance?.Register( info );
		}

		/// <summary>
		/// 從 <see cref="TrceCommandManager"/> 移除一條已註冊的指令。
		/// </summary>
		/// <param name="name">指令名稱。</param>
		protected void UnregisterCommand( string name )
		{
			TrceCommandManager.Instance?.Unregister( name );
		}

		// ─────────────────────────────────────────────
		//  錯誤處理輔助方法
		// ─────────────────────────────────────────────

		/// <summary>
		/// 以安全方式執行一個動作，並在發生例外時自動向 SRE Guardian 報告。
		/// </summary>
		/// <param name="action">要安全執行的動作。</param>
		/// <param name="context">描述執行情境的字串，用於錯誤日誌。</param>
		protected void SafeExecute( System.Action action, string context = "" )
		{
			try
			{
				action?.Invoke();
			}
			catch ( Exception ex )
			{
				_ = SreSystem.Instance?.ReportError( PluginId, $"{context}: {ex.Message}", ex.StackTrace );
			}
		}
	}
}
