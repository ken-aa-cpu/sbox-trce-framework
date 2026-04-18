// File: Code/Kernel/Event/GlobalEventBus.cs
// Encoding: UTF-8 (No BOM)

using System;
using System.Threading;

namespace Trce.Kernel.Event
{
	/// <summary>
	/// <para>【Zero-Allocation Global Event Bus】</para>
	/// <para>
	/// Core design principle: exploits C# static generic class specialization.
	/// For each unique <typeparamref name="TEvent"/> type, the CLR generates a separate
	/// static class instance at JIT time, each containing its own independent static
	/// <see cref="Action{T}"/> field.
	/// </para>
	/// <para>
	/// <b>Performance analysis:</b><br/>
	/// - <b>O(1) dispatch:</b> no dictionary lookup required for event publication.<br/>
	/// - <b>Zero boxing:</b> the <c>struct</c> constraint ensures payloads never get boxed to the Heap.<br/>
	/// - <b>Zero dictionary overhead:</b> compared to a <c>Dictionary&lt;Type, Delegate&gt;</c> approach,
	///   eliminates hash computation, memory indirection, and GC pressure from potential dictionary resizes.<br/>
	/// - <b>Thread-safe:</b> <c>Subscribe</c> / <c>Unsubscribe</c> / <c>ClearAll</c> use a
	///   <see cref="Interlocked.CompareExchange{T}"/> CAS loop for lock-free safe updates;
	///   <c>Publish</c> reads the delegate snapshot directly (single game thread), preventing half-updated reads.
	/// </para>
	/// <para>
	/// <b>Restrictions (Zero-Allocation guarantee):</b><br/>
	/// - Inside the <see cref="Publish{TEvent}"/> path: no LINQ, no <c>new</c> operator, no boxing operations.
	/// </para>
	/// </summary>
	public static class GlobalEventBus
	{
		/// <summary>
		/// Static generic dispatcher — each <typeparamref name="TEvent"/> specialization has its own
		/// independent static storage slot.
		/// CLR guarantee: <c>EventDispatcher&lt;EventA&gt;.Handlers</c> and
		/// <c>EventDispatcher&lt;EventB&gt;.Handlers</c> are completely separate static fields.
		/// This design achieves O(1) fully lookup-free dispatch.
		/// </summary>
		/// <typeparam name="TEvent">Must be a struct implementing <see cref="ITrceEvent"/>.</typeparam>
		private static class EventDispatcher<TEvent> where TEvent : struct, ITrceEvent
		{
			/// <summary>
			/// Delegate chain for all subscribers of this event type.
			/// The static field exists independently in each generic specialization of <c>EventDispatcher&lt;TEvent&gt;</c>.
			/// </summary>
			internal static Action<TEvent>? Handlers;
		}

		/// <summary>
		/// Subscribes a handler to the global event bus.
		/// <para>This is a one-time delegate-combine cost — <b>not on the hot path</b>.</para>
		/// </summary>
		/// <typeparam name="TEvent">The event type, must be a <c>readonly struct</c> implementing <see cref="ITrceEvent"/>.</typeparam>
		/// <param name="handler">The callback to invoke when the event fires.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
		public static void Subscribe<TEvent>(Action<TEvent> handler)
			where TEvent : struct, ITrceEvent
		{
			if (handler is null)
				throw new ArgumentNullException(nameof(handler));

			// CAS Loop: safely append the delegate.
			// Note: s&box whitelist does not allow Volatile.Read — direct field read is fine (single game thread).
			Action<TEvent>? current, updated;
			do
			{
				current = EventDispatcher<TEvent>.Handlers;
				updated = current + handler;
			} while (!ReferenceEquals(
				Interlocked.CompareExchange(ref EventDispatcher<TEvent>.Handlers, updated, current),
				current));
		}

		/// <summary>
		/// Unsubscribes a handler from the global event bus.
		/// <para>Make sure the <paramref name="handler"/> passed is the same delegate instance used during subscription, otherwise the unsubscription has no effect.</para>
		/// </summary>
		/// <typeparam name="TEvent">The event type.</typeparam>
		/// <param name="handler">The callback to remove.</param>
		public static void Unsubscribe<TEvent>(Action<TEvent> handler)
			where TEvent : struct, ITrceEvent
		{
			if (handler is null)
				return;

			// CAS Loop: safely remove the delegate (direct field read — Volatile.Read not on s&box whitelist).
			Action<TEvent>? current, updated;
			do
			{
				current = EventDispatcher<TEvent>.Handlers;
				updated = current - handler;
			} while (!ReferenceEquals(
				Interlocked.CompareExchange(ref EventDispatcher<TEvent>.Handlers, updated, current),
				current));
		}

		/// <summary>
		/// Publishes an event to all subscribers.
		/// <para>
		/// <b>【Zero-Allocation Hot Path】</b><br/>
		/// Safe to call every frame on a server with 100 connected clients.<br/>
		/// - No dictionary lookup.<br/>
		/// - No boxing (<typeparamref name="TEvent"/> is constrained to <c>struct</c>).<br/>
		/// - No LINQ.<br/>
		/// - No dynamic memory allocation.
		/// </para>
		/// </summary>
		/// <typeparam name="TEvent">The event type.</typeparam>
		/// <param name="eventData">The event payload (value type, lives on the Stack).</param>
		public static void Publish<TEvent>(TEvent eventData)
			where TEvent : struct, ITrceEvent
		{
			// eventData is passed by value — no boxing occurs.
			// Volatile.Read is not on the s&box whitelist (SB1000); direct field read is used (single game thread).
			EventDispatcher<TEvent>.Handlers?.Invoke(eventData);
		}

		/// <summary>
		/// Clears all subscriptions for a specific event type.
		/// <para>Typically called during scene or game-mode transitions to prevent stale delegate issues.</para>
		/// </summary>
		/// <typeparam name="TEvent">The event type to clear.</typeparam>
		public static void ClearAll<TEvent>()
			where TEvent : struct, ITrceEvent
		{
			// Interlocked.Exchange ensures the write is immediately visible to all threads,
			// and is mutually exclusive with the CAS Loop's CompareExchange at the same atomic level.
			Interlocked.Exchange(ref EventDispatcher<TEvent>.Handlers, null);
		}
	}
}
