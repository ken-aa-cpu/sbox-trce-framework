// File: Code/Kernel/Plugin/TrceServiceManager.cs
// Encoding: UTF-8 (No BOM)
// Phase 3: Service Locator — TRCE framework core.
// Goals: eliminate singleton patterns, O(1) lookup, thread-safe, Zero-GC hot path.

using Sandbox;
using System;
using System.Collections.Concurrent;

namespace Trce.Kernel.Plugin
{
	/// <summary>
	/// Defines the registration priority for services in <see cref="TrceServiceManager"/>.
	/// Higher priority services cannot be overridden by lower priority registrations.
	/// Mirrors the ServicePriority concept from Spigot's ServicesManager.
	/// </summary>
	public enum ServicePriority
	{
		/// <summary>Default priority for all game/business plugins.</summary>
		Normal = 0,
		/// <summary>Reserved for TRCE Kernel layer services. Business plugins must not use this.</summary>
		Kernel = 99
	}

	/// <summary>
	/// <para>【Phase 3 — Service Locator】</para>
	/// <para>
	/// Inherits from <see cref="GameObjectSystem"/>, guaranteed by the s&amp;box engine to be
	/// unique and automatically instantiated per <see cref="Scene"/>.
	/// This replaces all static <c>Instance</c> singleton patterns and serves as
	/// the dependency-injection core of the TRCE plugin ecosystem.
	/// </para>
	/// <para>
	/// <b>Architecture principles:</b><br/>
	/// - <b>O(1) lookup:</b> uses a <see cref="Dictionary{TKey, TValue}"/> keyed by <see cref="Type"/> for hash lookups.<br/>
	/// - <b>Thread-safe:</b> all reads and writes are protected by <c>ConcurrentDictionary</c>, preventing race conditions during async initialization.<br/>
	/// - <b>Zero-GC hot path:</b> <see cref="GetService{T}"/> allocates nothing on the hit path.<br/>
	///   All services must be <c>class</c> (reference type) to avoid boxing to <c>object</c>.<br/>
	/// - <b>No reflection:</b> all operations use C# generic static dispatch — zero Reflection calls.<br/>
	/// </para>
	/// <para>
	/// <b>Usage example:</b><br/>
	/// <code>
	/// // Register inside the service's own OnStart:
	/// TrceServiceManager.Instance?.RegisterService&lt;IInventoryService&gt;(this);
	///
	/// // Query from any Component:
	/// var inventory = TrceServiceManager.Instance?.GetService&lt;IInventoryService&gt;();
	/// </code>
	/// </para>
	/// </summary>
	public sealed class TrceServiceManager : GameObjectSystem
	{
		// ─────────────────────────────────────────────
		//  Static Access Point
		// ─────────────────────────────────────────────

		/// <summary>
		/// Returns the <see cref="TrceServiceManager"/> instance for the current scene.
		/// <para>Guaranteed valid after scene startup by the <see cref="GameObjectSystem"/> base lifecycle.</para>
		/// </summary>
		public static TrceServiceManager Instance { get; private set; }

		// ─────────────────────────────────────────────
		//  Internal Storage
		// ─────────────────────────────────────────────

		/// <summary>
		/// Internal record pairing a service instance with its registration priority.
		/// </summary>
		private sealed class ServiceEntry
		{
			public object Instance { get; }
			public ServicePriority Priority { get; }
			public ServiceEntry( object instance, ServicePriority priority )
			{
				Instance  = instance;
				Priority  = priority;
			}
		}

		/// <summary>
		/// Service dictionary keyed by the public contract type (Interface or Class).
		/// <para>Note: all queries go through generics — no boxing occurs on the GetService path.</para>
		/// </summary>
		// P2-B: ConcurrentDictionary — lock-free reads on the GetService hot path.
		private readonly ConcurrentDictionary<Type, ServiceEntry> _services = new();

		// ─────────────────────────────────────────────
		//  Constructor & Lifecycle
		// ─────────────────────────────────────────────

		/// <summary>
		/// Constructor called automatically by the s&amp;box engine.
		/// Inheriting <see cref="GameObjectSystem"/> ensures this class is the unique system per scene.
		/// </summary>
		/// <param name="scene">The scene instance this system belongs to.</param>
		public TrceServiceManager( Scene scene ) : base( scene )
		{
			Instance = this;
			Log.Info( "🗂️ [TrceServiceManager] Service Locator initialized." );
		}

		// ─────────────────────────────────────────────
		//  Public API
		// ─────────────────────────────────────────────

		/// <summary>
		/// Registers a service instance keyed by type <typeparamref name="T"/>.
		/// <para>
		/// <b>Priority protection:</b> If a service of the same type is already registered at a higher
		/// priority, this registration is silently rejected and a warning is logged.
		/// Same-priority registrations replace the existing instance (last-write-wins).
		/// </para>
		/// <para>This method is a one-time setup cost — <b>not on the hot path</b>.</para>
		/// </summary>
		/// <typeparam name="T">The public contract type of the service (preferably an Interface, e.g. <c>IInventoryService</c>). Must be a class.</typeparam>
		/// <param name="serviceInstance">The service instance to register. Must not be null.</param>
		/// <param name="priority">Registration priority. Kernel-layer services must pass <see cref="ServicePriority.Kernel"/>.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceInstance"/> is null.</exception>
		public void RegisterService<T>( T serviceInstance, ServicePriority priority = ServicePriority.Normal ) where T : class
		{
			if ( serviceInstance is null )
				throw new ArgumentNullException( nameof(serviceInstance), $"[TrceServiceManager] Cannot register a null instance for service '{typeof(T).Name}'." );

			var serviceType = typeof(T);

			if ( _services.TryGetValue( serviceType, out var existing ) )
			{
				if ( (int)priority < (int)existing.Priority )
				{
					Log.Warning( $"⛔ [TrceServiceManager] Rejected registration of '{serviceType.Name}' " +
					             $"— existing priority '{existing.Priority}' outranks '{priority}'. " +
					             $"Attempted registrant: {serviceInstance.GetType().Name}" );
					return;
				}

				if ( priority == existing.Priority )
					Log.Info( $"🔄 [TrceServiceManager] Service '{serviceType.Name}' is being REPLACED by a new instance at same priority '{priority}'." );
				else
					Log.Info( $"⬆️ [TrceServiceManager] Service '{serviceType.Name}' is being UPGRADED from '{existing.Priority}' to '{priority}'." );
			}

			_services[serviceType] = new ServiceEntry( serviceInstance, priority );
			Log.Info( $"✅ [TrceServiceManager] Registered service: '{serviceType.Name}' → {serviceInstance.GetType().Name} [{priority}]" );
		}

		/// <summary>
		/// Removes the service keyed by type <typeparamref name="T"/> from the registry.
		/// <para>If the service does not exist, this method is a silent no-op.</para>
		/// <para>This method is a one-time operation — <b>not on the hot path</b>.</para>
		/// </summary>
		/// <typeparam name="T">The public contract type of the service to remove.</typeparam>
		public void UnregisterService<T>() where T : class
		{
			if ( _services.TryRemove( typeof(T), out var removed ) )
				Log.Info( $"🗑️ [TrceServiceManager] Unregistered service: '{typeof(T).Name}' [was: {removed.Priority}]" );
		}

		/// <summary>
		/// <para>【Zero-GC Hot Path】 Safely looks up and returns the service instance of type <typeparamref name="T"/>.</para>
		/// <para>
		/// <b>Performance analysis:</b><br/>
		/// - Hit path: one <see cref="ConcurrentDictionary{TKey,TValue}.TryGetValue"/> hash lookup + one reference cast — <b>zero GC allocation</b>.<br/>
		/// - Miss path: same as hit path, returns <c>null</c> — still zero allocation.<br/>
		/// </para>
		/// </summary>
		/// <typeparam name="T">The public contract type of the service to look up. Must be a class.</typeparam>
		/// <returns>
		/// The service instance if registered; otherwise <c>null</c>.
		/// Never throws.
		/// </returns>
		public T GetService<T>() where T : class
		{
			// P2-B: ConcurrentDictionary.TryGetValue is a lock-free read, O(1) hash lookup, Zero-GC Allocation.
			return _services.TryGetValue( typeof(T), out var entry ) ? (T)entry.Instance : null;
		}

		/// <summary>
		/// Clears all registered services.
		/// <para>Typically called on scene unload or when resetting a test environment.</para>
		/// </summary>
		public void ClearAll()
		{
			_services.Clear();
			Log.Info( "🧹 [TrceServiceManager] All services have been cleared." );
		}
	}
}
