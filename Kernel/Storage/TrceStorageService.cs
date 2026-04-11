using Sandbox;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Trce.Kernel.Storage
{
	public enum StorageProviderType
	{
		Local,
		Firebase
	}

	/// <summary>
	/// Global storage service for TRCE framework.
	/// Acts as a central access point for storage operations, allowing hot-swapping of providers.
	/// </summary>
	[Title( "TRCE Storage Service" ), Group( "Trce - Kernel" ), Icon( "storage" )]
	public class TrceStorageService : GameObjectSystem
	{
		public StorageProviderType ActiveProviderType
		{
			get => _activeProviderType;
			set
			{
				_activeProviderType = value;
				UpdateProvider();
			}
		}
		private StorageProviderType _activeProviderType = StorageProviderType.Firebase;

		/// <summary>
		/// The current storage provider being used.
		/// This can be swapped at runtime (e.g., from Local to Firebase).
		/// </summary>
		public ITrceStorageProvider Provider { get; set; }

		private Providers.LocalStorageProvider _fallbackProvider = new Providers.LocalStorageProvider();

		/// <summary>
		/// Global instance for easy access. 
		/// </summary>
		public static TrceStorageService Instance { get; private set; }

		public TrceStorageService( Scene scene ) : base( scene )
		{
			Instance = this;
			UpdateProvider();
		}

		private void UpdateProvider()
		{
			if ( _activeProviderType == StorageProviderType.Firebase )
			{
				Provider = new Providers.FirebaseStorageProvider();
			}
			else
			{
				Provider = new Providers.LocalStorageProvider();
			}
		}

		/// <summary>
		/// Saves data to the current provider asynchronously.
		/// </summary>
		public async Task SaveAsync<T>( string key, T data )
		{
			if ( Provider == null ) return;
			try
			{
				await Provider.SaveAsync( key, data );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"[TrceStorageService] Provider error on SaveAsync: {ex.Message}. Falling back to LocalStorageProvider." );
				await _fallbackProvider.SaveAsync( key, data );
			}
		}

		/// <summary>
		/// Loads data from the current provider asynchronously.
		/// </summary>
		public async Task<T> LoadAsync<T>( string key )
		{
			if ( Provider == null ) return default;
			try
			{
				return await Provider.LoadAsync<T>( key );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"[TrceStorageService] Provider error on LoadAsync: {ex.Message}. Falling back to LocalStorageProvider." );
				return await _fallbackProvider.LoadAsync<T>( key );
			}
		}

		/// <summary>
		/// Checks if data exists in the current provider asynchronously.
		/// </summary>
		public async Task<bool> ExistsAsync( string key )
		{
			if ( Provider == null ) return false;
			try
			{
				return await Provider.ExistsAsync( key );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"[TrceStorageService] Provider error on ExistsAsync: {ex.Message}. Falling back to LocalStorageProvider." );
				return await _fallbackProvider.ExistsAsync( key );
			}
		}

		/// <summary>
		/// Deletes data from the current provider asynchronously.
		/// </summary>
		public async Task DeleteAsync( string key )
		{
			if ( Provider == null ) return;
			try
			{
				await Provider.DeleteAsync( key );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"[TrceStorageService] Provider error on DeleteAsync: {ex.Message}. Falling back to LocalStorageProvider." );
				await _fallbackProvider.DeleteAsync( key );
			}
		}

		[ConCmd( "trce_storage_migrate_to_cloud", Help = "Migrates all local JSON storage files to the Firebase cloud provider." )]
		public static void MigrateToCloudCmd()
		{
			_ = MigrateToCloudAsync();
		}

		private static async Task MigrateToCloudAsync()
		{
			if ( !Networking.IsHost )
			{
				Log.Error( "Migration can only be run on the Host/Server." );
				return;
			}

			Log.Info( "[TrceStorageService] Starting migration to cloud..." );
			
			var firebaseProvider = new Providers.FirebaseStorageProvider();

			try
			{
				var files = FileSystem.Data.FindFile( "", "*.json", true );
				int count = 0;
				
				foreach ( var file in files )
				{
					if ( file.Contains( "firebase_config.json" ) ) continue;

					string key = file.Replace( "\\", "/" ).Replace( ".json", "" );
					
					string content = FileSystem.Data.ReadAllText( file );
					if ( string.IsNullOrWhiteSpace( content ) ) continue;
					
					using var jsonDoc = JsonDocument.Parse( content );
					
					await firebaseProvider.SaveAsync( key, jsonDoc.RootElement );
					count++;
					Log.Info( $"[TrceStorageService] Migrated: {key}" );
				}
				
				Log.Info( $"[TrceStorageService] Migration completed successfully. Migrated {count} files." );
			}
			catch ( Exception ex )
			{
				Log.Error( $"[TrceStorageService] Migration failed: {ex.Message}" );
			}
		}
	}
}

