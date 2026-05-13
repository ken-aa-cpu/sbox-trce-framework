// File: Code/Kernel/Event/EntityEventBus.cs
// Encoding: UTF-8 (No BOM)

using System;
using System.Collections.Generic;
using Sandbox;

namespace Trce.Kernel.Event
{
	/// <summary>
	/// <para>【Zero-Allocation Entity Event Bus】</para>
	/// <para>
	/// This <see cref="Component"/> is attached to a specific <see cref="GameObject"/>
	/// (e.g. a player, NPC, or entity) and provides an event subscription / publication
	/// mechanism scoped to that object only.
	/// </para>
	/// <para>
	/// <b>Architecture note:</b><br/>
	/// Unlike the static generic <see cref="GlobalEventBus"/>, an entity bus must store
	/// delegates per instance, so it uses a <c>Dictionary&lt;Type, object&gt;</c> as the slot store.<br/>
	/// <br/>
	/// <b>Why no boxing?</b><br/>
	/// <c>Action&lt;TEvent&gt;</c> is a <b>reference type</b>. Casting it to <c>object</c>
	/// for storage and back again is a <b>reference cast</b>, not a value-type boxing operation.
	/// The event payload (<typeparamref name="TEvent"/>) is constrained to <c>struct</c>
	/// and is passed by value in the delegate call — also no boxing.
	/// </para>
	/// <para>
	/// <b>Performance profile:</b><br/>
	/// - <b>Subscribe / Unsubscribe:</b> O(1) average (Dictionary lookup).<br/>
	/// - <b>Publish:</b> O(1) average (one Dictionary lookup + one reference cast + Delegate.Invoke).<br/>
	/// - <b>GC pressure:</b> Only when subscribing a new event type for the first time
	///   (one-time <c>Action&lt;TEvent&gt;</c> delegate allocation); hot-path Publish is zero-alloc.
	/// </para>
	/// </summary>
	public sealed class EntityEventBus : Component
	{
		/// <summary>
		/// Delegate storage dictionary. Key is the event <see cref="Type"/>;
		/// value is the corresponding <c>Action&lt;TEvent&gt;</c> stored as <c>object</c>
		/// (avoids requiring a non-generic base class).
		/// </summary>
		private readonly Dictionary<Type, object> _handlers = new();

		/// <summary>
		/// Subscribes to an event on this entity event bus.
		/// <para>Scope is limited to the <see cref="GameObject"/> this Component is attached to.</para>
		/// </summary>
		/// <typeparam name="TEvent">Event type; must be a <c>readonly struct</c> implementing <see cref="ITrceEvent"/>.</typeparam>
		/// <param name="handler">The callback to invoke when the event fires.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
		public void Subscribe<TEvent>(Action<TEvent> handler)
			where TEvent : struct, ITrceEvent
		{
			if (handler is null)
				throw new ArgumentNullException(nameof(handler));

			var key = typeof(TEvent);

			if (_handlers.TryGetValue(key, out var existing))
			{
				// existing is an Action<TEvent> (reference type) — this is a reference cast, not boxing.
				var existingTyped = (Action<TEvent>)existing;
				_handlers[key] = existingTyped + handler;
			}
			else
			{
				_handlers[key] = handler;
			}
		}

		/// <summary>
		/// Unsubscribes from an event on this entity event bus.
		/// </summary>
		/// <typeparam name="TEvent">Event type.</typeparam>
		/// <param name="handler">The callback to remove (must be the same instance used when subscribing).</param>
		public void Unsubscribe<TEvent>(Action<TEvent> handler)
			where TEvent : struct, ITrceEvent
		{
			if (handler is null)
				return;

			var key = typeof(TEvent);

			if (!_handlers.TryGetValue(key, out var existing))
				return;

			// Reference cast — not boxing.
			var existingTyped = (Action<TEvent>)existing;
			var updated = existingTyped - handler;

			if (updated is null)
				_handlers.Remove(key);
			else
				_handlers[key] = updated;
		}

		/// <summary>
		/// Publishes an event to all subscribers on this entity bus.
		/// <para>
		/// <b>【Zero-Allocation Hot Path】</b><br/>
		/// - One Dictionary lookup (O(1) average).<br/>
		/// - One reference cast (not boxing).<br/>
		/// - <typeparamref name="TEvent"/> is passed by value — no boxing.<br/>
		/// - No LINQ, no dynamic memory allocation.
		/// </para>
		/// </summary>
		/// <typeparam name="TEvent">Event type.</typeparam>
		/// <param name="eventData">The event data (value type, lives on the stack).</param>
		public void Publish<TEvent>(TEvent eventData)
			where TEvent : struct, ITrceEvent
		{
			var key = typeof(TEvent);

			if (!_handlers.TryGetValue(key, out var existing))
				return;

			// Reference cast — not boxing.
			// eventData is passed by value to Invoke; the struct constraint guarantees no boxing.
			((Action<TEvent>)existing).Invoke(eventData);
		}

		/// <summary>
		/// Clears all subscriptions for a specific event type on this entity bus.
		/// </summary>
		/// <typeparam name="TEvent">The event type to clear.</typeparam>
		public void ClearAll<TEvent>()
			where TEvent : struct, ITrceEvent
		{
			_handlers.Remove(typeof(TEvent));
		}

		/// <summary>
		/// Clears all subscriptions for all event types on this entity bus.
		/// <para>Typically called before a GameObject is destroyed or on scene transition to prevent stale delegate references.</para>
		/// </summary>
		public void ClearAll()
		{
			_handlers.Clear();
		}

		/// <summary>
		/// Automatically clears all delegates when this Component is removed from its
		/// <see cref="GameObject"/> or the GameObject is destroyed, preventing memory leaks.
		/// </summary>
		protected override void OnDestroy()
		{
			_handlers.Clear();
		}
	}
}
