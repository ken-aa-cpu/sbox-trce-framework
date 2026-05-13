// File: Code/Kernel/Player/TrceHumanoidModelEngine.cs
using Sandbox;
using System.Collections.Generic;
using Trce.Kernel.Player;
using Trce.Kernel.Plugin.Pawn.Base;

namespace Trce.Kernel.Plugin.Pawn
{
    /// <summary>
    /// Humanoid animation engine.
    /// Translates <see cref="CitizenIntent"/> into s&amp;box native Citizen model AnimGraph parameters.
    /// Refactored to the official animation spec — hardcoded strings eliminated for extensibility.
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
        [Description( "Mapping table from logical action names to AnimGraph parameter names (e.g. attack_primary -> b_attack)" )]
        public Dictionary<string, string> ActionMap { get; set; } = new()
        {
            { "attack_primary", "b_attack" }
        };

        protected override void UpdateBaseAnimations( CitizenIntent intent )
        {
            if ( TargetModel == null ) return;

            // Verify that a model resource is assigned (2026 API standard).
            if ( TargetModel.Model == null )
            {
                Log.Warning( $"[TRCE] {GameObject.Name}: TargetModel has no model resource assigned." );
                return;
            }

            // Verify that AnimGraph is enabled on the renderer.
            if ( !TargetModel.UseAnimGraph )
            {
                Log.Warning( $"[TRCE] {GameObject.Name}: UseAnimGraph is not enabled — this will cause a T-Pose." );
            }

            var controller = GameObject.Components.Get<CharacterController>();
            if ( controller == null ) return;

            // 1. Set grounded / airborne state (prevents T-Pose cross-lock).
            TargetModel.Set( GroundedParam, controller.IsOnGround );
            TargetModel.Set( "b_grounded", controller.IsOnGround );

            // 2. Velocity calculation: read true physics velocity from CharacterController.
            // Transform world-space velocity into character-local X (forward/back) and Y (left/right).
            var localVelocity = GameObject.Transform.Rotation.Inverse * controller.Velocity;

            // Compute movement ratio: divide local-space velocity by WalkSpeed.
            float ratioX = localVelocity.x / WalkSpeed;
            float ratioY = localVelocity.y / WalkSpeed;

            // Write movement parameters (aligned with s&box official spec for raw velocity and move ratio).
            TargetModel.Set( MoveXParam, ratioX );
            TargetModel.Set( MoveYParam, ratioY );
            TargetModel.Set( "move_x", ratioX );
            TargetModel.Set( "move_y", ratioY );
            TargetModel.Set( "wish_x", ratioX );
            TargetModel.Set( "wish_y", ratioY );

            // 3. Handle run/walk animation switch (based on horizontal physics speed).
            float horizontalSpeed = localVelocity.WithZ( 0 ).Length;
            TargetModel.Set( RunParam, horizontalSpeed > RunSpeedThreshold );

            // 4. Jump trigger (driven by WishJump from intent).
            TargetModel.Set( JumpParam, intent.WishJump );
            TargetModel.Set( "b_jump", intent.WishJump );
        }

        protected override void HandleActionRequested( string actionName )
        {
            base.HandleActionRequested( actionName );

            // Map the logical action to a visual animation trigger via ActionMap.
            if ( ActionMap != null && ActionMap.TryGetValue( actionName, out var triggerName ) )
            {
                TargetModel.Set( triggerName, true );
            }
        }
    }
}
