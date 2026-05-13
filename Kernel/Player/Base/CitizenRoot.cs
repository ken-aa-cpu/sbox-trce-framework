// File: Code/Kernel/Player/Base/CitizenRoot.cs
// Encoding: UTF-8 (No BOM)
// Role: Intent Gatekeeper — filters raw intent through runtime state tags before passing it downstream.
// DI: IStateTagService / IAttributeService resolved dynamically via TrceServiceManager (non-static).
// Performance: FilterIntent hot-path — zero String alloc / zero GC.

using Sandbox;
using System;
using Trce.Kernel.Player;
using Trce.Kernel.Plugin;
using Trce.Kernel.Stats;

namespace Trce.Kernel.Plugin.Pawn.Base
{
    /// <summary>
    /// <para>【Tier 1 — Citizen Root / Root Orchestrator】</para>
    /// <para>
    /// The first tier of the authority hierarchy. Holds and filters the per-frame intent
    /// (<see cref="CitizenIntent"/>) and broadcasts the safe intent to downstream subsystems
    /// (MovementEngine, ModelEngine, …).
    /// </para>
    /// <para>
    /// <b>Intent Gatekeeper:</b><br/>
    /// Each frame, after a Brain or AI writes a raw intent, <see cref="FilterIntent"/> must be
    /// called to obtain the "safe intent", which is then written back to <see cref="Intent"/>.<br/>
    /// Filtering rules:
    /// <list type="bullet">
    ///   <item><description><c>state.dead</c> or <c>restrict.move</c> → clears WishMove / WishJump.</description></item>
    ///   <item><description><c>state.dead</c> or <c>restrict.action</c> → clears WishAttack / WishUse / ActiveAction.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Service injection:</b><br/>
    /// <see cref="IStateTagService"/> and <see cref="IAttributeService"/> are resolved dynamically
    /// via <see cref="TrceServiceManager.GetService{T}"/> and stored as <b>non-static</b> instance
    /// fields, ensuring no ghost data survives a hot-reload.
    /// </para>
    /// </summary>
    [Title( "Citizen Root" )]
    [Category( "TRCE Core - Base" )]
    [Icon( "account_tree" )]
    public class CitizenRoot : Component, ICitizen
    {
        // ====================================================================
        //  Hot-path constants — pre-allocated; FilterIntent must never allocate.
        // ====================================================================

        /// <summary>Dead-state tag. Locks movement and actions.</summary>
        private const string TAG_STATE_DEAD       = "state.dead";

        /// <summary>Movement-restriction tag. Locks WishMove / WishJump.</summary>
        private const string TAG_RESTRICT_MOVE    = "restrict.move";

        /// <summary>Action-restriction tag. Locks WishAttack / WishUse / ActiveAction.</summary>
        private const string TAG_RESTRICT_ACTION  = "restrict.action";

        // ====================================================================
        //  Service cache (instance fields — non-static; hot-reload safe)
        //  Lazy-Resolve pattern: resolved in OnStart; if the service starts later
        //  than this component, FilterIntent resolves it on first call and caches it.
        // ====================================================================

        /// <summary>
        /// State-tag service cache. Populated in <see cref="OnStart"/> or lazily
        /// by <see cref="FilterIntent"/> on its first call.
        /// </summary>
        private IStateTagService _stateTagService;

        /// <summary>
        /// Attribute service cache. Used by subclasses for attribute queries (not the core filter path).
        /// Populated lazily in <see cref="OnStart"/>.
        /// </summary>
        private IAttributeService _attributeService;

        // ====================================================================
        //  ICitizen interface implementation — data state
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
        //  Broadcast events (Delegation Events — Tier 2 subscription entry)
        // ====================================================================

        /// <inheritdoc cref="ICitizen.OnActionRequested"/>
        public event Action<string> OnActionRequested;

        // ====================================================================
        //  Lifecycle
        // ====================================================================

        /// <summary>
        /// Resolves services from <see cref="TrceServiceManager"/> when the component starts.
        /// If a service is not yet ready (plugin load-order issue), <see cref="FilterIntent"/>
        /// will retry the resolution on its first call.
        /// </summary>
        protected override void OnStart()
        {
            // Dynamic resolution, non-static — prevents hot-reload ghost data.
            _stateTagService  = TrceServiceManager.Instance?.GetService<IStateTagService>();
            _attributeService = TrceServiceManager.Instance?.GetService<IAttributeService>();

            if ( _stateTagService is null )
            {
                Log.Warning( "[CitizenRoot] IStateTagService is not ready — will retry on first FilterIntent call." );
            }
        }

        // ====================================================================
        //  Intent Gatekeeper (hot path)
        //  ✔ TAG_* are compile-time interned const strings → zero runtime alloc.
        //  ✔ HasTag delegates directly to s&box target.Tags.Has → O(1), zero GC.
        //  ✔ Lazy-Resolve degrades to a single null-check once resolved → near-zero cost.
        //  ✗ Forbidden: $"" interpolation, string.Format, LINQ, new string(), ToString().
        // ====================================================================

        /// <summary>
        /// Filters a raw intent through the current state tags and returns a safe copy.
        /// <para>
        /// <b>Calling convention:</b> Brain / AI passes the raw intent in each frame,
        /// then writes the returned safe intent back to <see cref="Intent"/>.
        /// </para>
        /// <para><b>【Hot path】</b> — String alloc and GC allocation are strictly forbidden.</para>
        /// </summary>
        /// <param name="rawIntent">Raw, unvalidated intent from the input system or AI.</param>
        /// <returns>The safe intent after state-tag filtering.</returns>
        public CitizenIntent FilterIntent( CitizenIntent rawIntent )
        {
            // ── Lazy-Resolve: if OnStart failed, retry once here ──────────────────────────
            // ??= ensures GetService is called only while still null;
            // once resolved it degrades to a pure null-check — negligible hot-path cost.
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            _stateTagService ??= TrceServiceManager.Instance?.GetService<IStateTagService>();

            // If the service is unavailable, pass the raw intent through to avoid blocking gameplay.
            if ( _stateTagService is null )
                return rawIntent;

            var go = GameObject;

            // ── Query state.dead (needed for both restriction categories) ─────────────────
            // Check dead first to avoid calling HasTag twice for the same tag.
            bool isDead = _stateTagService.HasTag( go, TAG_STATE_DEAD );

            // ── Movement restriction: state.dead | restrict.move ──────────────────────────
            bool lockMove = isDead || _stateTagService.HasTag( go, TAG_RESTRICT_MOVE );
            if ( lockMove )
            {
                rawIntent.WishMove = Vector3.Zero;
                rawIntent.WishJump = false;
            }

            // ── Action restriction: state.dead | restrict.action ──────────────────────────
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
        //  Action dispatch (Tier 1 → Tier 2)
        // ====================================================================

        /// <inheritdoc cref="ICitizen.ExecuteAction"/>
        public virtual void ExecuteAction( string actionName )
        {
            if ( string.IsNullOrEmpty( actionName ) ) return;

            // Under script control (cutscene, forced narrative): silently discard all action requests.
            if ( IsControlledByScript ) return;

            // Broadcast to all subscribed Tier 2 subsystems (ModelEngine, SkillEngine, …).
            OnActionRequested?.Invoke( actionName );
        }
    }
}
