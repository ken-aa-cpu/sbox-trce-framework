// File: Code/Kernel/Player/Base/CitizenRoot.cs
// Encoding: UTF-8 (No BOM)
// Phase: Intent Gatekeeper — 意圖過濾門戶
// 依賴注入：IStateTagService / IAttributeService 透過 TrceServiceManager 動態解析（非 static）。
// 效能：FilterIntent 熱路徑零 String Alloc / 零 GC。

using Sandbox;
using System;
using Trce.Kernel.Player;
using Trce.Kernel.Plugin;
using Trce.Kernel.Stats;

namespace Trce.Kernel.Plugin.Pawn.Base
{
    /// <summary>
    /// <para>【Tier 1 — Citizen 主角本 / Root Orchestrator】</para>
    /// <para>
    /// 權力結構的第一層。持有並過濾當幀的意圖 (<see cref="CitizenIntent"/>)，
    /// 再將安全的意圖廣播給下層子系統 (MovementEngine、ModelEngine …)。
    /// </para>
    /// <para>
    /// <b>意圖過濾門戶 (Intent Gatekeeper)：</b><br/>
    /// 每幀由 Brain 或 AI 寫入原始意圖後，必須先呼叫 <see cref="FilterIntent"/>
    /// 取得「安全意圖」，再將結果回寫至 <see cref="Intent"/>。<br/>
    /// 過濾規則如下：
    /// <list type="bullet">
    ///   <item><description><c>state.dead</c> 或 <c>restrict.move</c> → 清除 WishMove / WishJump。</description></item>
    ///   <item><description><c>state.dead</c> 或 <c>restrict.action</c> → 清除 WishAttack / WishUse / ActiveAction。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>服務注入：</b><br/>
    /// <see cref="IStateTagService"/> 與 <see cref="IAttributeService"/> 透過
    /// <see cref="TrceServiceManager.GetService{T}"/> 動態解析，儲存為 <b>非 static</b> 的實例欄位，
    /// 確保 s&amp;box 熱重載後不殘留幽靈資料。
    /// </para>
    /// </summary>
    [Title( "Citizen Root (主角本)" )]
    [Category( "TRCE Core - Base" )]
    [Icon( "account_tree" )]
    public class CitizenRoot : Component, ICitizen
    {
        // ====================================================================
        //  熱路徑常量 — 預先分配，FilterIntent 內禁止任何 String Alloc / GC
        // ====================================================================

        /// <summary>死亡狀態標籤。鎖定移動與行動。</summary>
        private const string TAG_STATE_DEAD       = "state.dead";

        /// <summary>移動限制標籤。鎖定 WishMove / WishJump。</summary>
        private const string TAG_RESTRICT_MOVE    = "restrict.move";

        /// <summary>行動限制標籤。鎖定 WishAttack / WishUse / ActiveAction。</summary>
        private const string TAG_RESTRICT_ACTION  = "restrict.action";

        // ====================================================================
        //  服務快取 (Instance fields — 非 static；相容熱重載)
        //  使用 Lazy-Resolve 模式：OnStart 嘗試解析，若服務晚於本元件啟動
        //  則在 FilterIntent 首次被呼叫時補充解析，此後不再重複查詢。
        // ====================================================================

        /// <summary>
        /// 狀態標籤服務快取。由 <see cref="OnStart"/> 或
        /// <see cref="FilterIntent"/> 懶載入（Lazy-Resolve）。
        /// </summary>
        private IStateTagService _stateTagService;

        /// <summary>
        /// 屬性服務快取。供子類別擴充屬性查詢使用（非過濾主邏輯）。
        /// 由 <see cref="OnStart"/> 懶載入。
        /// </summary>
        private IAttributeService _attributeService;

        // ====================================================================
        //  ICitizen 介面實作 — 資料狀態
        // ====================================================================

        /// <inheritdoc cref="ICitizen.Intent"/>
        [Property, ReadOnly, Group( "State" )]
        public CitizenIntent Intent { get; set; }

        /// <inheritdoc cref="ICitizen.Position"/>
        public Vector3 Position
        {
            get => GameObject.WorldPosition;
            set => GameObject.WorldPosition = value;
        }

        /// <inheritdoc cref="ICitizen.Rotation"/>
        public Rotation Rotation
        {
            get => GameObject.WorldRotation;
            set => GameObject.WorldRotation = value;
        }

        /// <inheritdoc cref="ICitizen.EyeRotation"/>
        [Property, Group( "State" )]
        public Rotation EyeRotation { get; set; }

        /// <inheritdoc cref="ICitizen.IsControlledByScript"/>
        [Property, Group( "State" )]
        public bool IsControlledByScript { get; set; } = false;

        // ====================================================================
        //  廣播事件 (Delegation Events — Tier 2 訂閱入口)
        // ====================================================================

        /// <inheritdoc cref="ICitizen.OnActionRequested"/>
        public event Action<string> OnActionRequested;

        // ====================================================================
        //  生命週期
        // ====================================================================

        /// <summary>
        /// 元件啟動時向 <see cref="TrceServiceManager"/> 解析服務。
        /// 若服務尚未就緒（插件載入順序問題），<see cref="FilterIntent"/> 將在第一次呼叫時補充解析。
        /// </summary>
        protected override void OnStart()
        {
            // 動態解析，非 static — 防止熱重載幽靈資料
            _stateTagService  = TrceServiceManager.Instance?.GetService<IStateTagService>();
            _attributeService = TrceServiceManager.Instance?.GetService<IAttributeService>();

            if ( _stateTagService is null )
            {
                Log.Warning( "[CitizenRoot] IStateTagService 尚未就緒，將於 FilterIntent 首次呼叫時重試。" );
            }
        }

        // ====================================================================
        //  意圖過濾門戶 (Intent Gatekeeper)
        //  【效能死線】本方法位於每幀熱路徑，嚴禁任何 String Alloc 或 GC 分配。
        //  ✔ TAG_* 為 compile-time interned const string → 無執行期分配。
        //  ✔ HasTag 直接委派 s&box target.Tags.Has → O(1)，無 GC。
        //  ✔ Lazy-Resolve 在服務已解析後退化為單一 null-check → 接近零成本。
        //  ✗ 嚴禁：$"" 插值、string.Format、LINQ、new string()、ToString()。
        // ====================================================================

        /// <summary>
        /// 根據狀態標籤過濾原始意圖，回傳對當前狀態安全的意圖副本。
        /// <para>
        /// <b>呼叫規範：</b>Brain / AI 每幀將原始意圖傳入，取得過濾結果後再寫回 <see cref="Intent"/>。
        /// </para>
        /// <para><b>【熱路徑】</b>嚴禁 String Alloc 與 GC 分配。</para>
        /// </summary>
        /// <param name="rawIntent">來自輸入系統或 AI 的原始未驗證意圖。</param>
        /// <returns>依狀態標籤處理後的安全意圖。</returns>
        public CitizenIntent FilterIntent( CitizenIntent rawIntent )
        {
            // ── Lazy-Resolve：OnStart 若未成功，此處補充解析一次 ──────────────
            // 使用 ??= 確保僅在 null 時才觸發 GetService 查詢，
            // 服務就緒後退化為純 null-check，熱路徑成本可忽略。
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            _stateTagService ??= TrceServiceManager.Instance?.GetService<IStateTagService>();

            // 服務不可用時放行原始意圖，不阻斷遊戲流程
            if ( _stateTagService is null )
                return rawIntent;

            var go = GameObject;

            // ── 查詢 state.dead（兩類限制均需用到）────────────────────────────
            // 先查死亡狀態，避免對同一個 TAG 呼叫兩次 HasTag。
            bool isDead = _stateTagService.HasTag( go, TAG_STATE_DEAD );

            // ── 移動限制：state.dead | restrict.move ─────────────────────────
            bool lockMove = isDead || _stateTagService.HasTag( go, TAG_RESTRICT_MOVE );
            if ( lockMove )
            {
                rawIntent.WishMove = Vector3.Zero;
                rawIntent.WishJump = false;
            }

            // ── 行動限制：state.dead | restrict.action ───────────────────────
            bool lockAction = isDead || _stateTagService.HasTag( go, TAG_RESTRICT_ACTION );
            if ( lockAction )
            {
                rawIntent.WishAttack  = false;
                rawIntent.WishUse     = false;
                rawIntent.ActiveAction = null;
            }

            return rawIntent;
        }

        // ====================================================================
        //  動作分派 (Action Dispatch — Tier 1 → Tier 2)
        // ====================================================================

        /// <inheritdoc cref="ICitizen.ExecuteAction"/>
        public virtual void ExecuteAction( string actionName )
        {
            if ( string.IsNullOrEmpty( actionName ) ) return;

            // 受控狀態（過場動畫、強制劇情）：靜默丟棄所有動作請求
            if ( IsControlledByScript ) return;

            // 廣播至所有訂閱的 Tier 2 子系統（ModelEngine、SkillEngine …）
            OnActionRequested?.Invoke( actionName );
        }
    }
}
