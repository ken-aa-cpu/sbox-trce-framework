// File: Code/Kernel/Player/TrceHumanoidModelEngine.cs
using Sandbox;
using System.Collections.Generic;
using Trce.Kernel.Player;
using Trce.Kernel.Plugin.Pawn.Base;

namespace Trce.Kernel.Plugin.Pawn
{
    /// <summary>
    ///   人形動畫引擎 (Humanoid Model Engine)
    ///   負責將 CitizenIntent 轉換為 s&box 原生 Citizen 模型的 AnimGraph 參數。
    ///   已根據官方動畫規範重構，消除硬編碼以提高擴展性。
    /// </summary>
    [Title( "TRCE Humanoid Model Engine" )]
    [Category( "TRCE Core - Visuals" )]
    [Icon( "directions_walk" )]
    public class TrceHumanoidModelEngine : TrceModelEngine
    {
        [Property, Group( "Animation Mapping" )] public string GroundedParam { get; set; } = "b_grounded";
        [Property, Group( "Animation Mapping" )] public string MoveXParam { get; set; } = "move_x";
        [Property, Group( "Animation Mapping" )] public string MoveYParam { get; set; } = "move_y";
        [Property, Group( "Animation Mapping" )] public string JumpParam { get; set; } = "b_jump";
        [Property, Group( "Animation Mapping" )] public string RunParam { get; set; } = "b_run";
        [Property, Group( "Animation Mapping" )] public float RunSpeedThreshold { get; set; } = 250f;

        [Property, Group( "Animation Settings" )] public float WalkSpeed { get; set; } = 150f;
        [Property, Group( "Animation Settings" )] public float RunSpeed { get; set; } = 300f;

        [Property, Group( "Animation Mapping" )]
        [Description( "動作指令與 AnimGraph 參數的映射表 (例如: attack_primary -> b_attack)" )]
        public Dictionary<string, string> ActionMap { get; set; } = new()
        {
            { "attack_primary", "b_attack" }
        };

        protected override void UpdateBaseAnimations( CitizenIntent intent )
        {
            if ( TargetModel == null ) return;

            // 檢查是否有掛載 模型資源 (2026 標準寫法)
            if ( TargetModel.Model == null )
            {
                Log.Warning( $"[TRCE] {GameObject.Name} 的 TargetModel 尚未掛載模型資源！" );
                return;
            }

            // 檢查組件是否啟用了動畫圖
            if ( !TargetModel.UseAnimGraph )
            {
                Log.Warning( $"[TRCE] {GameObject.Name} 未啟用 UseAnimGraph，這會導致 T-Pose。" );
            }

            var controller = GameObject.Components.Get<CharacterController>();
            if ( controller == null ) return;

            // 1. 設定空中/地面狀態 (解除十字架 T-Pose)
            TargetModel.Set( GroundedParam, controller.IsOnGround );
            TargetModel.Set( "b_grounded", controller.IsOnGround );

            // 2. 速度計算：直接從 CharacterController.Velocity 獲取真正的物理速度
            // 將世界座標下的物理速度轉換為角色局部空間的 X (前後) 與 Y (左右)
            var localVelocity = GameObject.Transform.Rotation.Inverse * controller.Velocity;
            
            // 計算移動比例：將本地空間速度除以 WalkSpeed
            float ratioX = localVelocity.x / WalkSpeed;
            float ratioY = localVelocity.y / WalkSpeed;

            // 寫入移動參數 (對齊 s&box 官方規範，Raw Velocity 以及 move ratio)
            TargetModel.Set( MoveXParam, ratioX );
            TargetModel.Set( MoveYParam, ratioY );
            TargetModel.Set( "move_x", ratioX );
            TargetModel.Set( "move_y", ratioY );
            TargetModel.Set( "wish_x", ratioX );
            TargetModel.Set( "wish_y", ratioY );

            // 3. 處理跑步動畫切換 (依據物理水平速度大小)
            float horizontalSpeed = localVelocity.WithZ( 0 ).Length;
            TargetModel.Set( RunParam, horizontalSpeed > RunSpeedThreshold );

            // 4. 跳躍觸發 (使用意圖中的 WishJump)
            TargetModel.Set( JumpParam, intent.WishJump );
            TargetModel.Set( "b_jump", intent.WishJump );
        }

        protected override void HandleActionRequested( string actionName )
        {
            base.HandleActionRequested( actionName );

            // 透過 ActionMap 將邏輯動作映射至視覺動畫觸發器
            if ( ActionMap != null && ActionMap.TryGetValue( actionName, out var triggerName ) )
            {
                TargetModel.Set( triggerName, true );
            }
        }
    }
}
