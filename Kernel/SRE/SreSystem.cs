using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Collections.Concurrent;

namespace Trce.Kernel.SRE
{
	/// <summary>
	/// SRE System: AI-driven reliability engineering monitor for the TRCE Framework.
	/// Automatically instantiated by the engine for every scene.
	/// </summary>
	public class SreSystem : GameObjectSystem, ISceneStartup
	{
		public static SreSystem Instance { get; private set; }

		public event System.Action OnDiagnosisStarted;
		public event System.Action<string> OnDiagnosisReceived;

		private ConcurrentDictionary<string, DateTime> _activePlugins = new();
		private ISreDiagnosticProvider _diagnosticProvider;

		public SreSystem( Scene scene ) : base( scene )
		{
			Instance = this;
			Log.Info( "[SRE] Global SRE System active for current scene." );
		}

		void ISceneStartup.OnHostInitialize()
		{
			// Host-side initialization
			Log.Info( "[SRE] Host initialization sequence started." );
		}

		/// <summary>
		/// Registers an AI diagnostic provider.
		/// </summary>
		public void RegisterProvider( ISreDiagnosticProvider provider )
		{
			_diagnosticProvider = provider;
			Log.Info( $"[SRE] Diagnostic provider registered: {provider.ProviderName}" );
		}

		public void CheckIn( string pluginId, string version )
		{
			_activePlugins[pluginId] = DateTime.Now;
			Log.Info( $"[SRE] Plugin check-in: {pluginId} (v{version})" );
		}

		public async Task ReportError( string source, string message, string stackTrace )
		{
			Log.Error( $"[SRE] Error from: {source} | Message: {message}" );
			
			if ( _diagnosticProvider != null && _diagnosticProvider.IsAvailable )
			{
				OnDiagnosisStarted?.Invoke();
				string diagnosis = await _diagnosticProvider.Diagnose( message, stackTrace );
				Log.Warning( $"[AI Diagnosis]: {diagnosis}" );
				OnDiagnosisReceived?.Invoke( diagnosis );
			}
			else
			{
				Log.Info( "[SRE] No AI diagnostic provider available." );
			}
		}

		public List<string> GetActivePlugins() => _activePlugins.Keys.ToList();
	}

	public interface ISreDiagnosticProvider
	{
		string ProviderName { get; }
		bool IsAvailable { get; }
		Task<string> Diagnose( string errorContext, string stackTrace );
	}
}

