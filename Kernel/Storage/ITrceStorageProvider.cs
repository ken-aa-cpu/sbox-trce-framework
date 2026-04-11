using System.Threading.Tasks;

namespace Trce.Kernel.Storage
{
	/// <summary>
	/// Interface for TRCE storage providers (e.g., Local File, Firebase, etc.)
	/// Defines a standard contract for asynchronous data persistence.
	/// </summary>
	public interface ITrceStorageProvider
	{
		/// <summary>
		/// Saves data to the storage asynchronously.
		/// </summary>
		/// <typeparam name="T">The type of data to save.</typeparam>
		/// <param name="key">The unique identifier for the data.</param>
		/// <param name="data">The data object to serialize and store.</param>
		Task SaveAsync<T>( string key, T data );

		/// <summary>
		/// Loads data from the storage asynchronously.
		/// </summary>
		/// <typeparam name="T">The type of data to load.</typeparam>
		/// <param name="key">The unique identifier for the data.</param>
		/// <returns>The deserialized data, or default if not found.</returns>
		Task<T> LoadAsync<T>( string key );

		/// <summary>
		/// Checks if data exists in the storage for the given key.
		/// </summary>
		/// <param name="key">The unique identifier to check.</param>
		/// <returns>True if data exists, false otherwise.</returns>
		Task<bool> ExistsAsync( string key );

		/// <summary>
		/// Deletes data from the storage for the given key.
		/// </summary>
		/// <param name="key">The unique identifier for the data to delete.</param>
		Task DeleteAsync( string key );
	}
}

