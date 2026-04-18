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
		/// Service dictionary keyed by the public contract type (Interface or Class) with the service instance as value.
		/// <para>Note: the dictionary stores <c>object</c>, but all queries go through generics — no boxing occurs on the GetService path.</para>
		/// </summary>
		// P2-B: ConcurrentDictionary — lock-free reads on the GetService hot path.
		private readonly ConcurrentDictionary<Type, object> _services = new();

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
		/// <b>Override behaviour:</b> If a service of the same type already exists, the new instance replaces it.
		/// This allows plugins to swap a default service implementation with an upgraded version,
		/// and a <see cref="Log.Info"/> message is emitted for debuggability.
		/// </para>
		/// <para>This method is a one-time setup cost — <b>not on the hot path</b>.</para>
		/// </summary>
		/// <typeparam name="T">The public contract type of the service (preferably an Interface, e.g. <c>IInventoryService</c>). Must be a class.</typeparam>
		/// <param name="serviceInstance">The service instance to register. Must not be null.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceInstance"/> is null.</exception>
		public void RegisterService<T>( T serviceInstance ) where T : class
		{
			if ( serviceInstance is null )
				throw new ArgumentNullException( nameof(serviceInstance), $"[TrceServiceManager] Cannot register a null instance for service '{typeof(T).Name}'." );

			var serviceType = typeof(T);
			var wasReplaced = _services.ContainsKey( serviceType );
			_services[serviceType] = serviceInstance;  // ConcurrentDictionary indexer is an atomic operation

			if ( wasReplaced )
				Log.Info( $"🔄 [TrceServiceManager] Service '{serviceType.Name}' is being REPLACED by a new instance. This is intentional if a plugin is upgrading the service." );

			Log.Info( $"✅ [TrceServiceManager] Registered service: '{serviceType.Name}' → {serviceInstance.GetType().Name}" );
		}

		/// <summary>
		/// Removes the service keyed by type <typeparamref name="T"/> from the registry.
		/// <para>If the service does not exist, this method is a silent no-op.</para>
		/// <para>This method is a one-time operation — <b>not on the hot path</b>.</para>
		/// </summary>
		/// <typeparam name="T">The public contract type of the service to remove.</typeparam>
		public void UnregisterService<T>() where T : class
		{
			if ( _services.TryRemove( typeof(T), out _ ) )
				Log.Info( $"🗑️ [TrceServiceManager] Unregistered service: '{typeof(T).Name}'" );
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
			return _services.TryGetValue( typeof(T), out var raw ) ? (T)raw : null;
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
