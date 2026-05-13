// File: Code/Kernel/Player/Base/TrceMovementEngine.cs
using Sandbox;
using Trce.Kernel.Player;

namespace Trce.Kernel.Plugin.Pawn.Base
{
    /// <summary>
    /// Physics movement core (Movement Engine Base).
    /// Tier 2 of the authority hierarchy. Reads the intent from CitizenRoot and drives CharacterController.
    /// </summary>
    [Title( "TRCE Movement Engine" )]
    [Category( "TRCE Core - Base" )]
    [Icon( "directions_run" )]
    public class TrceMovementEngine : Component
    {
        [Property, Description("Base movement speed")]
        public float BaseMoveSpeed { get; set; } = 150f;

        [Property, Description("Ground friction")]
        public float Friction { get; set; } = 5.0f;

        protected ICitizen _root;
        protected CharacterController _controller;

        protected override void OnStart()
        {
            // Locate the Citizen authority core (CitizenRoot) on this GameObject.
            _root = Components.Get<ICitizen>();

            // Acquire the physics CharacterController on this GameObject.
            _controller = Components.Get<CharacterController>();

            if ( _root == null )
            {
                Log.Warning( $"[{GameObject.Name}] TrceMovementEngine: ICitizen core not found. Entity will not be able to move." );
            }
        }

        protected override void OnFixedUpdate()
        {
            if ( _root == null || _controller == null ) return;

            // If this is a network proxy not owned locally, skip — let s&box native Sync handle physics.
            if ( IsProxy && !Network.IsOwner ) return;

            ApplyMovement( _root.Intent );
        }

        /// <summary>
        /// Converts intent into physical displacement.
        /// Can be overridden by subclasses (e.g. dragon flight, vehicle driving).
        /// </summary>
        protected virtual void ApplyMovement( CitizenIntent intent )
        {
            // 1. Compute target horizontal velocity.
            var targetVelocity = intent.WishMove * BaseMoveSpeed;
            if ( intent.WishSprint )
            {
                targetVelocity *= 1.5f;
            }

            // 2. Inherit current vertical velocity (handles gravity and jumping).
            var currentVelocity = _controller.Velocity;
            if ( _controller.IsOnGround )
            {
                currentVelocity.z = 0; // Zero out vertical velocity while grounded.
                if ( intent.WishJump )
                {
                    // Apply jump impulse.
                    currentVelocity.z = 350f; // Initial upward velocity.
                    _controller.Punch( Vector3.Up * 50f ); // Additional physics impulse.
                }
            }
            else
            {
                currentVelocity += Scene.PhysicsWorld.Gravity * Time.Delta; // Apply gravity while airborne.
            }

            // 3. Combine the brain's horizontal velocity (X, Y) with the physics vertical velocity (Z).
            currentVelocity = new Vector3( targetVelocity.x, targetVelocity.y, currentVelocity.z );

            // 5. Rotate the character body to face the movement direction (smooth turn).
            if ( intent.WishMove.Length > 0.1f )
            {
                var targetRot = Rotation.LookAt( intent.WishMove, Vector3.Up );
                GameObject.Transform.Rotation = Rotation.Slerp( GameObject.Transform.Rotation, targetRot, Time.Delta * 10f );
            }

            // 6. Write the final velocity and step the controller.
            _controller.Velocity = currentVelocity;
            _controller.Move();
        }
    }
}
