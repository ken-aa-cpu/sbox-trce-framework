using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using Sandbox;
using System.Linq;

namespace Trce.Kernel.Storage
{
	/// <summary>
	/// Provides a safe, asynchronous REST API wrapper for HTTP requests in the TRCE framework.
	/// Handles JSON serialization/deserialization and centralized error reporting.
	/// Uses s&amp;box's native Sandbox.Http API.
	/// </summary>
	public static class HttpRestClient
	{
		private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false
		};

		/// <summary>
		/// Performs an asynchronous HTTP GET request and deserializes the JSON response.
		/// </summary>
		public static async Task<T> GetAsync<T>( string url )
		{
			try
			{
				string responseString = await Http.RequestStringAsync( url );

				if ( string.IsNullOrWhiteSpace( responseString ) )
				{
					Log.Warning( $"[HttpRestClient] Received empty response from GET {url}" );
					return default;
				}

				return JsonSerializer.Deserialize<T>( responseString, _jsonOptions );
			}
			catch ( Exception ex )
			{
				Log.Error( $"[HttpRestClient] GET request failed: {url}. Exception: {ex.Message}" );
				return default;
			}
		}

		/// <summary>
		/// Performs an asynchronous HTTP PUT request with JSON payload.
		/// </summary>
		public static async Task<bool> PutAsync<T>( string url, T data )
		{
			try
			{
				var jsonContent = JsonSerializer.Serialize( data, _jsonOptions );
				
				// Use native sbox Http.CreateJsonContent for the body
				using var content = Sandbox.Http.CreateJsonContent( data );
				await Http.RequestAsync( url, "PUT", content );
				
				return true;
			}
			catch ( Exception ex )
			{
				Log.Error( $"[HttpRestClient] PUT request failed: {url}. Exception: {ex.Message}" );
				return false;
			}
		}

		/// <summary>
		/// Performs an asynchronous HTTP DELETE request.
		/// </summary>
		public static async Task<bool> DeleteAsync( string url )
		{
			try
			{
				await Http.RequestAsync( "DELETE", url );
				return true;
			}
			catch ( Exception ex )
			{
				Log.Error( $"[HttpRestClient] DELETE request failed: {url}. Exception: {ex.Message}" );
				return false;
			}
		}
	}
}

