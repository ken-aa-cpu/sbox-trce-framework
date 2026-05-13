// File: Code/Kernel/Stats/IStateTagService.cs
// Encoding: UTF-8 (No BOM)
// Phase 2: Universal state-tag service contract — TRCE State Tag System.

using Sandbox;

namespace Trce.Kernel.Stats;

/// <summary>
/// <para>【Phase 2 — Universal State-Tag Service Public Contract (State Tag System)】</para>
/// <para>
/// Provides a unified management interface for <see cref="GameObject"/> tags, supporting
/// real-time add, remove, and an optional auto-expiry mechanism (Duration).
/// Suitable for time-limited states such as "stunned", "burning", or "invincible".
/// </para>
/// <para>
/// <b>Architecture principle:</b><br/>
/// The implementation wraps the s&amp;box native <c>target.Tags.Has / Add / Remove</c>
/// directly, ensuring full compatibility with the engine tag system and no additional abstraction overhead.
/// </para>
/// <para>
/// <b>Event integration:</b><br/>
/// When a tag genuinely changes, the implementation layer publishes
/// <see cref="Trce.Kernel.Event.CoreEvents.TagAddedEvent"/> or
/// <see cref="Trce.Kernel.Event.CoreEvents.TagRemovedEvent"/> via <c>GlobalEventBus</c>.
/// Duplicate adds (tag already present) or removing a non-existent tag will not fire any event.
/// </para>
/// <para>
/// <b>Performance guarantee:</b><br/>
/// <c>HasTag</c> delegates directly to <c>target.Tags.Has</c> — O(1) operation.
/// The timer-update loop uses a Zero-GC design; LINQ and removal during iteration are forbidden.
/// </para>
/// </summary>
public interface IStateTagService
{
	/// <summary>
	/// Checks whether the target <see cref="GameObject"/> has the specified tag.
	/// <para>
	/// <b>【Performance guarantee — O(1)】:</b> Delegates directly to the s&amp;box native
	/// <c>target.Tags.Has(tag)</c> with zero additional overhead.
	/// </para>
	/// </summary>
	/// <param name="target">The target object to check.</param>
	/// <param name="tag">The tag string to query.</param>
	/// <returns><c>true</c> if the target has the tag; otherwise <c>false</c>.</returns>
	bool HasTag( GameObject target, string tag );

	/// <summary>
	/// Adds the specified tag to the target <see cref="GameObject"/>.
	/// <para>
	/// If <paramref name="durationSeconds"/> has a value, the tag will be automatically removed after
	/// the specified number of seconds.<br/>
	/// If the tag already exists, the timer is reset to the new duration (if provided); if no duration
	/// is provided, this is a No-Op.<br/>
	/// <see cref="Trce.Kernel.Event.CoreEvents.TagAddedEvent"/> is only published when the tag is
	/// actually added to <c>target.Tags</c>.
	/// </para>
	/// </summary>
	/// <param name="target">The target object to add the tag to.</param>
	/// <param name="tag">The tag string to add.</param>
	/// <param name="durationSeconds">
	/// Optional duration in seconds. If <c>null</c>, the tag persists until manually removed.
	/// </param>
	void AddTag( GameObject target, string tag, float? durationSeconds = null );

	/// <summary>
	/// Removes the specified tag from the target <see cref="GameObject"/>.
	/// <para>
	/// If the tag does not exist, this method is a No-Op — no exception is thrown and no event
	/// is published.<br/>
	/// <see cref="Trce.Kernel.Event.CoreEvents.TagRemovedEvent"/> is only published when the tag is
	/// actually removed from <c>target.Tags</c>.
	/// </para>
	/// </summary>
	/// <param name="target">The target object to remove the tag from.</param>
	/// <param name="tag">The tag string to remove.</param>
	void RemoveTag( GameObject target, string tag );
}
