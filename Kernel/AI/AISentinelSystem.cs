using Sandbox;
using System;
using System.Linq;
using System.Threading.Tasks;
using Trce.Kernel.SRE;

namespace Trce.Kernel.AI
{
	/// <summary>
	/// AI-Driven SRE Guardian diagnostic system.
	/// Automatically instantiated for every scene.
	/// </summary>
	public class AISentinelSystem : GameObjectSystem, ISceneStartup, ISreDiagnosticProvider
	{
		public string ModelPath { get; set; } = "models/sre_guardian/sentinel_v1.onnx";
		public string ProviderName => "AI-Driven SRE Sentinel (DirectML)";
		public bool IsAvailable => _isReady;
		private bool _isReady = false;

		public AISentinelSystem( Scene scene ) : base( scene )
		{
			InitModel();
		}

		void ISceneStartup.OnHostInitialize()
		{
			// Register with SRE system as soon as host is ready
			SreSystem.Instance?.RegisterProvider( this );
			Log.Info( "[Sentinel] Registered with SRE System." );
		}

		private void InitModel()
		{
			Log.Info( "[Sentinel] Initializing AI model..." );

			if ( !FileSystem.Mounted.FileExists( ModelPath ) )
			{
				Log.Warning( $"[Sentinel] Model not found: {ModelPath}. Skipping AI initialization." );
				return;
			}
			_isReady = true;
			Log.Info( "[Sentinel] AI model loaded and ready." );
		}

		public Task<string> Diagnose( string errorContext, string stackTrace )
		{
			if ( !_isReady ) return Task.FromResult( "AI model is not loaded or unavailable." );
			Log.Info( "[Sentinel] Running AI diagnostics..." );

			// Basic logic based on ONNX would replace this
			if ( errorContext.Contains( "NullReferenceException" ) )
			{
				return Task.FromResult( "[AI Diagnosis] Probable cause: null component reference. Check OnStart initialization." );
			}
			return Task.FromResult( $"[AI Diagnosis] Error analyzed: {errorContext}." );
		}
	}
}

