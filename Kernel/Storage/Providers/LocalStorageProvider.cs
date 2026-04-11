using Sandbox;
using System.Threading.Tasks;

namespace Trce.Kernel.Storage.Providers
{
	/// <summary>
	/// Local storage implementation using Sandbox's FileSystem.Data.
	/// </summary>
	public class LocalStorageProvider : ITrceStorageProvider
	{
		private string GetFilePath( string key )
		{
			if ( !key.EndsWith( ".json" ) )
			{
				key += ".json";
			}
			return key;
		}

		/// <summary>
		/// Saves data to the local file system asynchronously.
		/// </summary>
		public Task SaveAsync<T>( string key, T data )
		{
			FileSystem.Data.WriteJson( GetFilePath( key ), data );
			return Task.CompletedTask;
		}

		/// <summary>
		/// Loads data from the local file system asynchronously.
		/// </summary>
		public Task<T> LoadAsync<T>( string key )
		{
			string path = GetFilePath( key );
			if ( !FileSystem.Data.FileExists( path ) )
			{
				return Task.FromResult( default( T ) );
			}
			return Task.FromResult( FileSystem.Data.ReadJson<T>( path ) );
		}

		/// <summary>
		/// Checks if a file exists in the local file system asynchronously.
		/// </summary>
		public Task<bool> ExistsAsync( string key )
		{
			return Task.FromResult( FileSystem.Data.FileExists( GetFilePath( key ) ) );
		}

		/// <summary>
		/// Deletes a file from the local file system asynchronously.
		/// </summary>
		public Task DeleteAsync( string key )
		{
			string path = GetFilePath( key );
			if ( FileSystem.Data.FileExists( path ) )
			{
				FileSystem.Data.DeleteFile( path );
			}
			return Task.CompletedTask;
		}
	}
}

