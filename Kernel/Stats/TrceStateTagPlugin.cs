// File: Code/Kernel/Stats/TrceStateTagPlugin.cs
// Encoding: UTF-8 (No BOM)
// Phase 2: IStateTagService core implementation — wraps s&box native Tags, Zero-GC timer loop, hot-reload safety.

using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox;
using Trce.Kernel.Event;
using Trce.Kernel.Plugin;

namespace Trce.Kernel.Stats;

/// <summary>
/// 【Phase 2 — TRCE Universal State-Tag Service Implementation (State Tag Plugin)】
/// <para>
/// Wraps the s&amp;box native <c>GameObject.Tags.Has / Add / Remove</c> directly
/// and layers an optional auto-expiry timer mechanism on top.
/// </para>
/// <para>
/// <b>【Zero-GC performance deadline — OnUpdate loop】</b><br/>
/// The timer scan uses a pre-allocated <c>_expiredKeys</c> staging list,
/// with a <c>for</c>-indexed reverse-removal loop to achieve absolute zero GC alloc:
/// <list type="bullet">
///   <item>LINQ is strictly forbidden.</item>
///   <item>Removal during Dictionary foreach iteration is strictly forbidden.</item>
///   <item><c>_expiredKeys</c> is cleared in OnPluginDisabled — never reallocated.</item>
/// </list>
/// </para>
/// <para>
/// <b>【Failsafe】:</b> OnPluginDisabled forcibly clears all timer caches, ensuring zero residual state after hot-reload.
/// </para>
/// </summary>
[TrcePlugin( Id = "trce.statetag", Name = "TRCE State Tag System", Version = "2.0.0", Author = "TRCE Team" )]
[Icon( "label" )]
[Title( "TRCE State Tag Plugin" )]
public sealed class TrceStateTagPlugin : TrcePlugin, IStateTagService
{
	// ─────────────────────────────────────────────
	//  Timer Data Structures
	// ─────────────────────────────────────────────

	/// <summary>
	/// Composite key: (GameObject, tag) → expiry time (<c>Time.Now + duration</c>).
	/// <para>No static fields — guaranteed to be GC-collected after hot-reload with zero residual state.</para>
	/// </summary>
	private readonly Dictionary<(GameObject, string), float> _timers = new();

	/// <summary>
	/// 【Zero-GC design】 Pre-allocated staging list for removals.
	/// OnUpdate reuses this list every frame to collect expired keys before batch removal,
	/// preventing <c>InvalidOperationException</c> from concurrent modification.
	/// </summary>
	private readonly List<(GameObject, string)> _expiredKeys = new();

	// ─────────────────────────────────────────────
	//  Lifecycle
	// ─────────────────────────────────────────────

	/// <inheritdoc/>
	protected override Task OnPluginEnabled()
	{
		TrceServiceManager.Instance?.RegisterService<IStateTagService>( this );
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	protected override void OnPluginDisabled()
	{
		// Failsafe core: clear all timer caches and staging list to ensure zero residual state after hot-reload.
		_timers.Clear();
		_expiredKeys.Clear();
		TrceServiceManager.Instance?.UnregisterService<IStateTagService>();
	}

	// ─────────────────────────────────────────────
	//  OnUpdate — Zero-GC Timer Scan Loop
	// ─────────────────────────────────────────────

	/// <summary>
	/// Scans expired tags every frame and removes them automatically.
	/// <para>
	/// <b>【Zero-GC implementation details】</b><br/>
	/// 1. Iterates <c>_timers</c> KeyValuePair List with a for loop (avoids Dictionary Enumerator throwing on mutation).<br/>
	/// 2. Uses the pre-allocated <c>_expiredKeys</c> list to collect expired entries.<br/>
	/// 3. Iterates <c>_expiredKeys</c> in reverse to perform removals without index shifting.<br/>
	/// 4. Zero LINQ, zero anonymous objects, zero boxing.
	/// </para>
	/// </summary>
	protected override void OnUpdate()
	{
		if ( _timers.Count == 0 )
			return;

		float now = Time.Now;

		// Pass 1: collect all expired keys into the pre-allocated staging list.
		// foreach is used for read-only access here (no Remove calls), conforming to Zero-GC rules.
		foreach ( var kv in _timers )
		{
			if ( now >= kv.Value )
				_expiredKeys.Add( kv.Key );
		}

		// Pass 2: batch-remove expired tags (reverse order prevents List index shifting).
		for ( int i = _expiredKeys.Count - 1; i >= 0; i-- )
		{
			var key = _expiredKeys[i];
			_timers.Remove( key );

			// Only remove and fire the event if the tag is actually present.
			if ( key.Item1.IsValid() && key.Item1.Tags.Has( key.Item2 ) )
			{
				key.Item1.Tags.Remove( key.Item2 );
				GlobalEventBus.Publish( new CoreEvents.TagRemovedEvent( key.Item1, key.Item2 ) );
			}
		}

		// Clear the staging list for reuse next frame (no new allocation — Zero-GC).
		_expiredKeys.Clear();
	}

	// ─────────────────────────────────────────────
	//  IStateTagService Implementation
	// ─────────────────────────────────────────────

	/// <inheritdoc/>
	public bool HasTag( GameObject target, string tag )
	{
		return target.Tags.Has( tag );
	}

	/// <inheritdoc/>
	public void AddTag( GameObject target, string tag, float? durationSeconds = null )
	{
		// Only add and fire event if the tag is not already present (prevents redundant events).
		bool alreadyHas = target.Tags.Has( tag );

		if ( !alreadyHas )
		{
			target.Tags.Add( tag );
			GlobalEventBus.Publish( new CoreEvents.TagAddedEvent( target, tag ) );
		}

		// If a duration is provided, always update (reset) the timer regardless of whether the tag was just added.
		if ( durationSeconds.HasValue )
		{
			var key = (target, tag);
			_timers[key] = Time.Now + durationSeconds.Value;
		}
		else if ( alreadyHas )
		{
			// Tag already exists with no duration provided → No-Op (preserve existing timer if any).
		}
	}

	/// <inheritdoc/>
	public void RemoveTag( GameObject target, string tag )
	{
		// Only remove and fire event if the tag actually exists.
		if ( !target.Tags.Has( tag ) )
			return;

		target.Tags.Remove( tag );

		// Also remove the corresponding timer entry (if any).
		var key = (target, tag);
		_timers.Remove( key );

		GlobalEventBus.Publish( new CoreEvents.TagRemovedEvent( target, tag ) );
	}
}
