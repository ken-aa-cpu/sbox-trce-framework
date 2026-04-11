using Sandbox;
using System;
using Trce.Kernel.Player;

namespace Trce.Kernel.Plugin.Pawn.Base
{
    /// <summary>
    ///   / 視覺調度核心 (Model Engine Base)
    ///   / 權力結構的第二層。負責監聽 CitizenRoot 的意圖 (Intent)，並將其轉換為模型的動畫參數。
    /// </summary>
    [Title( "TRCE Model Engine (模型引擎)" )]
    [Category( "TRCE Core - Base" )]
    [Icon( "animation" )]
    public class TrceModelEngine : Component
    {
        [Property, Description("要驅動的目標模型渲染器")]
        public SkinnedModelRenderer TargetModel { get; set; }

        protected ICitizen _root;

        protected override void OnStart()
        {
            // 向上尋找權力核心 (CitizenRoot)
            _root = Components.Get<ICitizen>();

            if ( _root == null )
            {
                Log.Warning( $"[{GameObject.Name}] TrceModelEngine 找不到 ICitizen 權力核心，動畫引擎將處於休眠狀態。" );
                return;
            }

            // 訂閱動作廣播 (例如：噴火、攻擊、施法)
            _root.OnActionRequested += HandleActionRequested;
        }

        protected override void OnUpdate()
        {
            if ( _root == null || TargetModel == null ) return;

            // 每幀讀取意圖，並更新基礎動畫 (此為虛擬方法，供子類別如飛龍或人類具體實作)
            UpdateBaseAnimations( _root.Intent );
        }

        /// <summary>
        ///   處理來自大腦的疊加動作指令 (Action Layer)
        /// </summary>
        protected virtual void HandleActionRequested( string actionName )
        {
            // 範例：如果 actionName == "fire_breath"，則呼叫 TargetModel.Set("b_fire", true)
            // 基底類別不實作具體動畫，留給衍生類別處理
            Log.Info($"[ModelEngine] 收到動作廣播: {actionName}，準備進行視覺渲染...");
        }

        /// <summary>
        ///   處理來自大腦的基礎移動意圖 (Base Layer)
        /// </summary>
        protected virtual void UpdateBaseAnimations( CitizenIntent intent )
        {
            // 範例：將 intent.WishMove 轉換為模型的 move_x, move_y 參數
            // 基底類別不實作具體參數，留給衍生類別 (如 DragonModelEngine) 處理
        }

        protected override void OnDestroy()
        {
            // 確保組件銷毀時解除訂閱，防止 Memory Leak
            if ( _root != null )
            {
                _root.OnActionRequested -= HandleActionRequested;
            }
        }
    }
}
