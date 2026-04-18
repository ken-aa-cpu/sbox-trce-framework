using Sandbox;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Trce.Kernel.Plugin;

namespace Trce.Kernel.SRE
{
	/// <summary>
	/// SRE System: Plugin health-monitoring system for the TRCE framework.
	/// Tracks startup state and cumulative error counts for all plugins,
	/// and escalates alerts when a plugin exceeds the error threshold.
	/// Automatically instantiated per scene by the engine.
	/// </summary>
	public class SreSystem : GameObjectSystem, ISceneStartup, ISreSystem
	{
		public static SreSystem Instance { get; private set; }

		public System.Action OnDiagnosisStarted;
		public System.Action<string> OnDiagnosisReceived;

		/// <summary>Fired when a plugin's error count reaches the alert threshold.</summary>
		/// <remarks>Parameters: (pluginId, errorCount)</remarks>
		public static event Action<string, int> OnPluginErrorThresholdReached;

		private readonly ConcurrentDictionary<string, DateTime> _activePlugins = new();

		/// <summary>P1-2: Tracks cumulative error count per plugin source.</summary>
		private readonly ConcurrentDictionary<string, int> _errorCounts = new();

		/// <summary>
		/// P1-2: The number of errors from a single plugin source that triggers an escalated alert.
		/// </summary>
		private const int ErrorThreshold = 5;

		public SreSystem( Scene scene ) : base( scene )
		{
			Instance = this;
		}

		public void OnLevelLoaded()
		{
			// Register with TrceServiceManager so plugins can resolve via GetService<ISreSystem>().
			// Must be done here (not in constructor) — TrceServiceManager may not exist yet at ctor time.
			TrceServiceManager.Instance?.RegisterService<ISreSystem>( this );
			Log.Info( "[SRE] Global SRE System active for current scene." );
		}

		void ISceneStartup.OnHostInitialize()
		{
			Log.Info( "[SRE] Host initialization sequence started." );
		}

		/// <summary>
		/// Called by a plugin at startup to register its ID and version with SRE.
		/// Invoked automatically by <c>TrcePlugin.InitializeAsync()</c> — do not call manually.
		/// </summary>
		public void CheckIn( string pluginId, string version )
		{
			_activePlugins[pluginId] = DateTime.Now;
			// Reset error counter on successful check-in (plugin restarted cleanly).
			_errorCounts.TryRemove( pluginId, out _ );
			Log.Info( $"[SRE] Plugin check-in: {pluginId} (v{version})" );
		}

		/// <summary>
		/// P1-2: Reports an error from a plugin and records the source and message centrally.
		/// When the error count exceeds <see cref="ErrorThreshold"/>, escalates to an Error-level log
		/// and fires <see cref="OnPluginErrorThresholdReached"/> for external handling (e.g. auto-restart).
		/// Invoked automatically by <c>TrcePlugin.SafeExecute()</c> / <c>SafeExecuteAsync()</c> — do not call manually.
		/// </summary>
		public System.Threading.Tasks.Task ReportError( string source, string message, string stackTrace )
		{
			// Increment error counter for this source.
			int newCount = _errorCounts.AddOrUpdate( source, 1, ( _, old ) => old + 1 );

			Log.Error( $"[SRE] Error from '{source}' (total: {newCount}): {message}" );

			if ( !string.IsNullOrEmpty( stackTrace ) )
				Log.Error( $"[SRE] Stack trace:\n{stackTrace}" );

			// P1-2: Escalate when error count hits threshold.
			if ( newCount >= ErrorThreshold )
			{
				Log.Error( $"[SRE] ⚠️ HEALTH ALERT: Plugin '{source}' has accumulated {newCount} errors " +
				           $"(threshold: {ErrorThreshold}). Consider restarting or disabling this plugin." );
				OnPluginErrorThresholdReached?.Invoke( source, newCount );
			}

			return System.Threading.Tasks.Task.CompletedTask;
		}

		/// <summary>
		/// Returns the list of all plugin IDs that have successfully checked in.
		/// </summary>
		public List<string> GetActivePlugins() => new List<string>( _activePlugins.Keys );

		/// <summary>
		/// Gets the current error count for the specified plugin source.
		/// Returns 0 if no errors have been recorded.
		/// </summary>
		public int GetErrorCount( string source )
		{
			_errorCounts.TryGetValue( source, out int count );
			return count;
		}

		/// <summary>
		/// Resets the error counter for the specified plugin source.
		/// Useful after a plugin has been manually recovered.
		/// </summary>
		public void ResetErrorCount( string source )
		{
			_errorCounts.TryRemove( source, out _ );
			Log.Info( $"[SRE] Error count reset for plugin: '{source}'." );
		}
	}
}