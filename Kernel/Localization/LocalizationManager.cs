using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Trce.Kernel.Bridge;
using Trce.Kernel.Papi;

namespace Trce.Kernel.Localization
{
	[Title( "Localization Manager" ), Group( "Trce - Kernel" ), Icon( "language" )]
	public class LocalizationManager : Component
	{

		public static System.Action OnLanguageChanged;

		[Property] public string DefaultLanguage { get; set; } = "en";
		[Sync(SyncFlags.FromHost)] public string CurrentLanguage { get; set; }

		private const string BaseLangPath = "Localization/";
		private Dictionary<string, Dictionary<string, string>> langTable = new();

		protected override void OnAwake()
		{
			CurrentLanguage = DefaultLanguage;
		}

		protected override void OnStart()
		{
			ReloadAll();
			RegisterPapiHooks();
			RegisterCommand();
		}

		private void RegisterCommand()
		{
			var cm = Scene.Get<Trce.Kernel.Command.TrceCommandManager>();
			if ( cm == null ) return;

			cm.Register( new Trce.Kernel.Command.TrceCommandManager.CommandInfo
			{
				Name = "lang",
				Description = "Switch UI language (Local only)",
				RequiredWeight = 0,
				Handler = ( steamId, args ) =>
				{
					if ( args.Length < 1 ) return;
					string code = args[0].ToLower();

					if ( !langTable.ContainsKey( code ) ) return;

					RpcSwitchLanguage( steamId, code );
				},
/*
				SuggestionProvider = ( steamId, argIndex, currentArgs ) =>
				{
					if ( argIndex == 0 ) return langTable.Keys.ToArray();
					return null;
				}
				*/
			} );
		}

		[Rpc.Broadcast]
		private void RpcSwitchLanguage( ulong targetSteamId, string code )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null ) return;

			if ( bridge.LocalSteamId == targetSteamId )
			{
				SwitchLanguage( code );
				OnLanguageChanged?.Invoke();
			}
		}

		public void ReloadAll()
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null ) return;

			try
			{
				langTable.Clear();
				var files = bridge.FindProjectFiles( BaseLangPath, "*.json", true );

				foreach ( var filePath in files )
				{
					// Extract filename without extension using string ops (avoids System.IO)
					var fileNameOnly = filePath.Contains( '/' ) ? filePath.Substring( filePath.LastIndexOf( '/' ) + 1 ) : filePath;
					var dotIdx = fileNameOnly.LastIndexOf( '.' );
					string fileName = dotIdx > 0 ? fileNameOnly.Substring( 0, dotIdx ) : fileNameOnly;
					if ( !fileName.Contains('_') ) continue;

					string langCode = fileName.Split( '_' ).Last().ToLower();
					string jsonContent = bridge.ReadProjectFile( $"{BaseLangPath}{filePath}" );
					if ( string.IsNullOrEmpty(jsonContent) ) continue;

					var entry = JsonSerializer.Deserialize<Dictionary<string, string>>( jsonContent );
					if ( entry != null )
					{
						if ( !langTable.ContainsKey( langCode ) )
							langTable[langCode] = new Dictionary<string, string>();

						foreach ( var kv in entry )
						{
							langTable[langCode][kv.Key] = kv.Value;
						}
					}
				}
				UpdatePapiEntries();
			}
			catch ( Exception ex )
			{
				Log.Error( $"[Localization] Load Error: {ex.Message}" );
			}
		}

		private void RegisterPapiHooks() => UpdatePapiEntries();

		public void UpdatePapiEntries()
		{
			if ( !langTable.TryGetValue( CurrentLanguage, out var strings ) ) return;
			var papi = PlaceholderAPI.For( this );
			if ( papi == null ) return;

			foreach ( var kvp in strings )
			{
				papi.Register( $"lang_{kvp.Key}", () => Translate( kvp.Key ) );
			}
			papi.InvalidateCache();
		}

		public string Translate( string key )
		{
			if ( langTable.TryGetValue( CurrentLanguage, out var strings ) && strings.TryGetValue( key, out var val ) )
				return val;

			if ( CurrentLanguage != DefaultLanguage && langTable.TryGetValue( DefaultLanguage, out var defStrings ) )
				if ( defStrings.TryGetValue( key, out var defVal ) ) return defVal;

			return $"#{key}";
		}

		public void SwitchLanguage( string langCode )
		{
			langCode = langCode.ToLower();
			if ( !langTable.ContainsKey( langCode ) ) return;

			CurrentLanguage = langCode;
			UpdatePapiEntries();
		}
	}
}

