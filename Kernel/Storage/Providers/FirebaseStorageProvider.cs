using System;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox;

namespace Trce.Kernel.Storage.Providers
{
	/// <summary>
	/// Implementation of ITrceStorageProvider using Firebase Realtime Database REST API.
	/// Requires Server-side execution for authentication/authorization.
	/// </summary>
	public class FirebaseStorageProvider : ITrceStorageProvider
	{
		private const string FirebaseConfigPath = "firebase_config.json";
		private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = true
		};
		
		private string _databaseUrl;
		private string _authSecret;
		private bool _isInitialized = false;

		/// <summary>
		/// Safely loads configuration from server-only filesystem.
		/// </summary>
		private void Initialize()
		{
			if ( _isInitialized )
				return;
				
			if ( !Networking.IsHost )
			{
				Log.Warning( "[FirebaseStorageProvider] Init rejected: Only the server can load Firebase configuration." );
				return;
			}
			
			// Try to load server configuration
			try 
			{
				if ( FileSystem.Data.FileExists( FirebaseConfigPath ) )
				{
					var configContent = FileSystem.Data.ReadAllText( FirebaseConfigPath );
					var config = JsonSerializer.Deserialize<FirebaseConfig>( configContent, _jsonOptions );
					
					if ( config != null && !string.IsNullOrWhiteSpace( config.DatabaseUrl ) )
					{
						_databaseUrl = config.DatabaseUrl;
						_authSecret = config.AuthSecret;
						_isInitialized = true;
						Log.Info( "[FirebaseStorageProvider] Configuration loaded securely on Host." );
						return;
					}
				}
				
				Log.Error( $"[FirebaseStorageProvider] Unable to initialize: Config invalid or missing at {FirebaseConfigPath}" );
			}
			catch ( Exception ex )
			{
				Log.Error( $"[FirebaseStorageProvider] Error loading config: {ex.Message}" );
			}
		}

		private string BuildUrl( string key )
		{
			if ( string.IsNullOrEmpty( _databaseUrl ) ) return string.Empty;
			
			var url = $"{_databaseUrl.TrimEnd( '/' )}/{key}.json";
			if ( !string.IsNullOrEmpty( _authSecret ) )
			{
				url += $"?auth={_authSecret}";
			}
			
			return url;
		}

		/// <inheritdoc />
		public async Task SaveAsync<T>( string key, T data )
		{
			Initialize();
			
			if ( !_isInitialized )
			{
				Log.Warning( $"[FirebaseStorageProvider] SaveAsync '{key}' aborted: Provider not initialized (No config/Host only)." );
				return;
			}
			
			var url = BuildUrl( key );
			if ( string.IsNullOrEmpty( url ) ) return;
			
			await HttpRestClient.PutAsync( url, data );
		}

		/// <inheritdoc />
		public async Task<T> LoadAsync<T>( string key )
		{
			Initialize();
			
			if ( !_isInitialized )
			{
				Log.Warning( $"[FirebaseStorageProvider] LoadAsync '{key}' aborted: Provider not initialized (No config/Host only)." );
				return default;
			}
			
			var url = BuildUrl( key );
			if ( string.IsNullOrEmpty( url ) ) return default;

			return await HttpRestClient.GetAsync<T>( url );
		}

		/// <inheritdoc />
		public async Task<bool> ExistsAsync( string key )
		{
			var data = await LoadAsync<object>( key );
			return data != null; // Simple existence check via GET
		}

		/// <inheritdoc />
		public async Task DeleteAsync( string key )
		{
			Initialize();
			
			if ( !_isInitialized )
			{
				Log.Warning( $"[FirebaseStorageProvider] DeleteAsync '{key}' aborted: Provider not initialized (No config/Host only)." );
				return;
			}
			
			var url = BuildUrl( key );
			if ( string.IsNullOrEmpty( url ) ) return;
			
			await HttpRestClient.DeleteAsync( url );
		}
		
		private class FirebaseConfig
		{
			public string DatabaseUrl { get; set; }
			public string AuthSecret { get; set; }
		}
	}
}

