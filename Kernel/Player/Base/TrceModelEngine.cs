using Sandbox;
using System;
using Trce.Kernel.Player;

namespace Trce.Kernel.Plugin.Pawn.Base
{
    /// <summary>
    /// Visual dispatch core (Model Engine Base).
    /// Tier 2 of the authority hierarchy. Listens to the intent from CitizenRoot
    /// and translates it into model animation parameters.
    /// </summary>
    [Title( "TRCE Model Engine" )]
    [Category( "TRCE Core - Base" )]
    [Icon( "animation" )]
    public class TrceModelEngine : Component
    {
        [Property, Description("Target skinned model renderer to drive")]
        public SkinnedModelRenderer TargetModel { get; set; }

        protected ICitizen _root;

        protected override void OnStart()
        {
            // Locate the Citizen authority core (CitizenRoot) on this GameObject.
            _root = Components.Get<ICitizen>();

            if ( _root == null )
            {
                Log.Warning( $"[{GameObject.Name}] TrceModelEngine: ICitizen core not found. Animation engine will remain dormant." );
                return;
            }

            // Subscribe to action broadcasts (e.g. fire breath, attack, cast spell).
            _root.OnActionRequested += HandleActionRequested;
        }

        protected override void OnUpdate()
        {
            if ( _root == null || TargetModel == null ) return;

            // Read intent each frame and update base animations.
            // (virtual — concrete implementations provided by subclasses such as a dragon or humanoid)
            UpdateBaseAnimations( _root.Intent );
        }

        /// <summary>
        /// Handles an additive action command broadcast from the brain (Action Layer).
        /// </summary>
        protected virtual void HandleActionRequested( string actionName )
        {
            // Example: if actionName == "fire_breath", call TargetModel.Set("b_fire", true).
            // Base class does not implement specific animations — subclasses handle them.
            Log.Info($"[ModelEngine] Action broadcast received: {actionName} — forwarding to visual layer.");
        }

        /// <summary>
        /// Handles base movement intent from the brain (Base Layer).
        /// </summary>
        protected virtual void UpdateBaseAnimations( CitizenIntent intent )
        {
            // Example: convert intent.WishMove into model move_x, move_y parameters.
            // Base class provides no concrete parameters — subclasses (e.g. DragonModelEngine) do.
        }

        protected override void OnDestroy()
        {
            // Unsubscribe when the component is destroyed to prevent memory leaks.
            if ( _root != null )
            {
                _root.OnActionRequested -= HandleActionRequested;
            }
        }
    }
}
