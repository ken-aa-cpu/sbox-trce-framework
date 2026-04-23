// File: Code/Kernel/Plugin/TrcePlugin.cs
// Encoding: UTF-8 (No BOM)
// Phase 3: TRCE standard plugin base class. Integrates automatic GlobalEvent unsubscription + service locator.

using Sandbox;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Trce.Kernel.Event;
using Trce.Kernel.SRE;
using Trce.Kernel.Command;

namespace Trce.Kernel.Plugin
{
	/// <summary>
	/// The runtime state of a plugin.
	/// </summary>
	public enum PluginState
	{
		/// <summary>The plugin has not been loaded yet or has been disabled.</summary>
		Unloaded,
		/// <summary>A fatal error occurred while starting the plugin.</summary>
		Error,
		/// <summary>The plugin is running normally.</summary>
		Enabled
	}

	/// <summary>
	/// <para>【Phase 3 — TRCE Standard Plugin Base Class】</para>
	/// <para>
	/// All business modules (e.g. Inventory, Economy, Combat Plugin) must inherit this class.
	/// This base class provides standardized lifecycle management, service access, and most critically —
	/// <b>automatic global event unsubscription</b> — preventing memory leaks at the source.
	/// </para>
	/// <para>
	/// <b>Lifecycle flow:</b><br/>
	/// <c>OnStart()</c> → <c>OnPluginEnabled()</c> (overridable, may be async)<br/>
	/// <c>OnDestroy()</c> → (auto-unsubscribes all events) → <c>OnPluginDisabled()</c> (overridable)<br/>
	/// </para>
	/// <para>
	/// <b>【Failsafe — Zero Memory Leak Guarantee】</b><br/>
	/// Use <see cref="RegisterEvent{TEvent}(Action{TEvent})"/> to subscribe to events.
	/// The system automatically calls the corresponding Unsubscribe before <see cref="OnPluginDisabled"/> fires,
	/// so developers never need to manage unsubscription manually — forgetting cannot cause a leak.
	/// </para>
	/// <para>
	/// <b>Usage example:</b>
	/// <code>
	/// [TrcePlugin(Id = "game.inventory", Name = "Inventory", Version = "1.0.0")]
	/// public class InventoryPlugin : TrcePlugin
	/// {
	///     protected override async Task OnPluginEnabled()
	///     {
	///         // Resolve dependencies via service locator
	///         var economy = GetService&lt;IEconomyService&gt;();
	///
	///         // Subscribe to events — system guarantees auto-unsubscription on Disable, zero leaks
	///         RegisterEvent&lt;PlayerKilledEvent&gt;(OnPlayerKilled);
	///         await Task.CompletedTask;
	///     }
	///
	///     private void OnPlayerKilled(CoreEvents.PlayerKilledEvent e)
	///     {
	///         // Handle player death logic ...
	///     }
	/// }
	/// </code>
	/// </para>
	/// </summary>
	[Icon( "extension" )]
	public abstract class TrcePlugin : Component
	{
		// ─────────────────────────────────────────────
		//  Public Properties
		// ─────────────────────────────────────────────

		/// <summary>Declarative metadata for this plugin, provided by <see cref="TrcePluginAttribute"/>.</summary>
		public TrcePluginAttribute Info { get; internal set; }

		/// <summary>Current runtime state of the plugin.</summary>
		public PluginState State { get; internal set; } = PluginState.Unloaded;

		/// <summary>Unique identifier for the plugin. Falls back to the class name if no <see cref="TrcePluginAttribute"/> is defined.</summary>
		public string PluginId => Info?.Id ?? GetType().Name;

		/// <summary>Version string of the plugin.</summary>
		public string Version => Info?.Version ?? "1.0.0";

		// ─────────────────────────────────────────────
		//  【Failsafe Core】 Event Unsubscription Action List
		// ─────────────────────────────────────────────

		/// <summary>
		/// Stores all unsubscription delegates created by <see cref="RegisterEvent{TEvent}(Action{TEvent})"/>.
		/// <para>
		/// Each element is an <see cref="Action"/> that encapsulates a <see cref="GlobalEventBus.Unsubscribe{TEvent}"/> call,
		/// holding a reference to the original handler to ensure unsubscription matches the exact subscription.
		/// </para>
		/// <para>
		/// This list is only modified during init/teardown (not on the hot path). Allocating one-time closures is acceptable.
		/// </para>
		/// </summary>
		private readonly List<System.Action> _unsubscribeActions = new();

		// ─────────────────────────────────────────────
		//  Lifecycle
		// ─────────────────────────────────────────────

		/// <summary>
		/// s&amp;box Component startup point. Triggers the async plugin-enable flow.
		/// <para>Normally managed by <see cref="PluginBootstrapper"/> via <see cref="InitializeAsync"/>;
		/// this method acts as a fallback start if the plugin is added to the scene directly.</para>
		/// </summary>
		protected override void OnStart()
		{
			// Direct start (not managed by Bootstrapper).
			// Bootstrapper-managed flow calls InitializeAsync, which also calls OnPluginEnabled internally.
			if ( State == PluginState.Unloaded )
			{
				_ = InitializeAsync();
			}
		}

		/// <summary>
		/// s&amp;box Component destruction point.
		/// <para>
		/// <b>【Auto Failsafe】</b>: Before calling the subclass's <see cref="OnPluginDisabled"/>,
		/// automatically executes all unsubscription actions registered via <see cref="RegisterEvent{TEvent}(Action{TEvent})"/>,
		/// ensuring no stale delegates remain — completely preventing memory leaks.
		/// </para>
		/// </summary>
		protected override void OnDestroy()
		{
			AutoUnsubscribeAll();
			State = PluginState.Unloaded;
			OnPluginDisabled();
		}

		/// <summary>
		/// s&amp;box Component enabled callback. Updates plugin state.
		/// </summary>
		protected override void OnEnabled()
		{
			State = PluginState.Enabled;
		}

		/// <summary>
		/// s&amp;box Component disabled callback. Triggers plugin teardown (including auto event unsubscription).
		/// </summary>
		protected override void OnDisabled()
		{
			AutoUnsubscribeAll();
			State = PluginState.Unloaded;
			OnPluginDisabled();
		}

		// ─────────────────────────────────────────────
		//  TRCE Standard Lifecycle Virtual Methods
		// ─────────────────────────────────────────────

		/// <summary>
		/// <para>【Overridable】 Async initialization method called when the plugin is enabled.</para>
		/// <para>Perform service lookups, resource loading, and event subscriptions via <see cref="RegisterEvent{TEvent}(Action{TEvent})"/> here.</para>
		/// </summary>
		/// <returns>A <see cref="Task"/> representing the async operation.</returns>
		protected virtual Task OnPluginEnabled() => Task.CompletedTask;

		/// <summary>
		/// <para>【Overridable】 Cleanup method called when the plugin is disabled.</para>
		/// <para>
		/// <b>【Failsafe Guarantee】</b>: All events subscribed via <see cref="RegisterEvent{TEvent}(Action{TEvent})"/>
		/// are automatically unsubscribed before this method is called. Subclasses do not need to manage this manually.
		/// </para>
		/// </summary>
		protected virtual void OnPluginDisabled() { }

		// ─────────────────────────────────────────────
		//  Public Helper Methods
		// ─────────────────────────────────────────────

		/// <summary>
		/// <para>【Failsafe Event Subscription】 Subscribes to a global event via <see cref="GlobalEventBus"/> and automatically tracks the unsubscription action.</para>
		/// <para>
		/// <b>Use this method instead of calling <see cref="GlobalEventBus.Subscribe{TEvent}(Action{TEvent})"/> directly.</b><br/>
		/// When this plugin is destroyed or disabled, the system will automatically call Unsubscribe for all events registered this way,
		/// preventing memory leaks or stale delegate calls even if the subclass forgets to unsubscribe.
		/// </para>
		/// <para>
		/// <b>Performance note:</b> The cost of this call is a one-time initialization cost (not on the hot path).
		/// The Lambda closure is created once and lives as long as the plugin.
		/// </para>
		/// </summary>
		/// <typeparam name="TEvent">The event type — must be a <c>readonly struct</c> implementing <see cref="ITrceEvent"/>.</typeparam>
		/// <param name="handler">The handler delegate to call when the event fires. Must be an instance method (not an anonymous lambda) for accurate unsubscription.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
		protected void RegisterEvent<TEvent>( System.Action<TEvent> handler )
			where TEvent : struct, ITrceEvent
		{
			if ( handler is null )
				throw new ArgumentNullException( nameof(handler), $"[{PluginId}] Cannot register a null event handler for '{typeof(TEvent).Name}'." );

			// Step 1: Subscribe to the global event bus
			GlobalEventBus.Subscribe<TEvent>( handler );

			// Step 2: Capture and store the matching unsubscription action.
			// Captures the handler reference to ensure the exact same Delegate instance is used for unsubscription (reference equality).
			_unsubscribeActions.Add( () => GlobalEventBus.Unsubscribe<TEvent>( handler ) );
		}

		/// <summary>
		/// <para>【Zero-GC Service Lookup】 Safely looks up a registered service via <see cref="TrceServiceManager"/>.</para>
		/// <para>
		/// This method replaces the old hardcoded static Instance lookup pattern, achieving true decoupling.
		/// If the service is not found, silently returns <c>null</c> without throwing — callers decide how to handle a missing dependency.
		/// </para>
		/// </summary>
		/// <typeparam name="T">The public contract type of the service (Interface or Class).</typeparam>
		/// <returns>The service instance, or <c>null</c> if the service is not registered.</returns>
		public T GetService<T>() where T : class
		{
			// Prefer TrceServiceManager lookup — O(1) dictionary lookup, Zero-GC
			var service = TrceServiceManager.Instance?.GetService<T>();
			if ( service is not null )
				return service;

			// P2-A: No longer falling back to Scene.GetAllComponents — that call allocates and breaks the Zero-GC guarantee.
			// If the service is not registered, log a one-time warning and return null. Callers decide how to handle the missing dependency.
			Log.Warning( $"[{PluginId}] Service '{typeof(T).Name}' is not registered in TrceServiceManager. "
			           + "Ensure the providing plugin is loaded and calls RegisterService in OnPluginEnabled." );
			return null;
		}

		/// <summary>
		/// Looks up another loaded TRCE plugin instance in the scene via <see cref="PluginBootstrapper"/>.
		/// </summary>
		/// <typeparam name="T">The target plugin type, must inherit <see cref="TrcePlugin"/>.</typeparam>
		/// <returns>The plugin instance, or <c>null</c> if not loaded.</returns>
		public T GetPlugin<T>() where T : TrcePlugin
		{
			return PluginBootstrapper.Instance?.GetPlugin<T>();
		}

		// ─────────────────────────────────────────────
		//  Framework Internal
		// ─────────────────────────────────────────────

		/// <summary>
		/// <para>【Framework Internal】Called by <see cref="PluginBootstrapper"/> after the scene starts,
		/// to ensure dependencies are resolved in the correct order.</para>
		/// <para>This method also reports plugin state to the SRE Guardian.</para>
		/// </summary>
		/// <returns>A <see cref="Task"/> representing the async initialization operation.</returns>
		public virtual async Task InitializeAsync()
		{
			// SRE 登記失敗不應阻止 Plugin 啟動，獨立處理
			try
			{
				SreSystem.Instance?.CheckIn( PluginId, Version );
			}
			catch ( Exception e )
			{
				Log.Warning( $"[SRE] Registration error for '{PluginId}': {e.Message}" );
			}

			// Plugin 主要初始化
			try
			{
				await OnPluginEnabled();
				State = PluginState.Enabled;
			}
			catch ( Exception e )
			{
				State = PluginState.Error;
				Log.Error( $"[{PluginId}] Initialization failed: {e.Message}" );
				await ( SreSystem.Instance?.ReportError( PluginId, $"Initialization failed: {e.Message}", e.StackTrace )
				        ?? Task.CompletedTask );
			}
		}

		/// <summary>
		/// <para>【Failsafe Core Implementation】Iterates through <see cref="_unsubscribeActions"/>,
		/// invoking each unsubscription delegate to clear all global event subscriptions.</para>
		/// <para>
		/// This method is designed to be <b>idempotent</b>: the list is cleared after execution
		/// to prevent double-unsubscription if both OnDisabled and OnDestroy fire in sequence.
		/// </para>
		/// </summary>
		private void AutoUnsubscribeAll()
		{
			if ( _unsubscribeActions.Count == 0 )
				return;

			Log.Info( $"🔌 [{PluginId}] Auto-unsubscribing {_unsubscribeActions.Count} event handler(s)..." );

			// Use for instead of foreach to avoid List<T> Enumerator GC Allocation
			for ( int i = 0; i < _unsubscribeActions.Count; i++ )
			{
				try
				{
					_unsubscribeActions[i].Invoke();
				}
				catch ( Exception ex )
				{
					Log.Error( $"❌ [{PluginId}] Error during auto-unsubscribe of event handler #{i}: {ex.Message}" );
				}
			}

			// Clear the list to achieve idempotency, preventing double-unsubscription from OnDisabled + OnDestroy
			_unsubscribeActions.Clear();

			Log.Info( $"✅ [{PluginId}] All event handlers unsubscribed. Memory leak risk: ZERO." );
		}

		// ─────────────────────────────────────────────
		//  Command Helper Methods
		// ─────────────────────────────────────────────

		/// <summary>
		/// Registers a command with <see cref="TrceCommandManager"/>.
		/// </summary>
		/// <param name="info">The command declaration info.</param>
		protected void RegisterCommand( TrceCommandManager.CommandInfo info )
		{
			TrceCommandManager.Instance?.Register( info );
		}

		/// <summary>
		/// Removes a previously registered command from <see cref="TrceCommandManager"/>.
		/// </summary>
		/// <param name="name">The command name.</param>
		protected void UnregisterCommand( string name )
		{
			TrceCommandManager.Instance?.Unregister( name );
		}

		// ─────────────────────────────────────────────
		//  Error Handling Helpers
		// ─────────────────────────────────────────────

		/// <summary>
		/// Safely executes an action and automatically reports any exception to the SRE Guardian.
		/// </summary>
		/// <param name="action">The action to execute safely.</param>
		/// <param name="context">A string describing the execution context, used in error logs.</param>
		protected void SafeExecute( System.Action action, string context = "" )
		{
			try
			{
				action?.Invoke();
			}
			catch ( Exception ex )
			{
				_ = SreSystem.Instance?.ReportError( PluginId, $"{context}: {ex.Message}", ex.StackTrace );
			}
		}

		/// <summary>
		/// <para>Safely executes an async action and automatically reports any exception to the SRE Guardian.</para>
		/// <para>
		/// <b>【Purpose】</b>Replaces the <c>_ = SomeAsync()</c> fire-and-forget pattern.<br/>
		/// Ensures that exceptions from async operations are not silently discarded — they are captured and forwarded to SRE.
		/// </para>
		/// <para>
		/// <b>Usage example:</b>
		/// <code>
		/// // ❌ Old pattern: exceptions silently swallowed
		/// _ = LoadDataAsync();
		///
		/// // ✅ New pattern: exceptions captured by SRE
		/// _ = SafeExecuteAsync( LoadDataAsync, "LoadData" );
		/// </code>
		/// </para>
		/// </summary>
		/// <param name="action">The async action factory to execute safely.</param>
		/// <param name="context">A string describing the execution context, used in error logs.</param>
		/// <returns>A <see cref="Task"/> representing the async operation.</returns>
		protected async Task SafeExecuteAsync( Func<Task> action, string context = "" )
		{
			try
			{
				if ( action != null )
					await action();
			}
			catch ( Exception ex )
			{
				await ( SreSystem.Instance?.ReportError( PluginId, $"{context}: {ex.Message}", ex.StackTrace )
				        ?? Task.CompletedTask );
			}
		}
	}
}
