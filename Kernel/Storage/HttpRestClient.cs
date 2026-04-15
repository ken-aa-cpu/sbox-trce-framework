using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using Sandbox;

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
		/// <typeparam name="T">The type to deserialize the response into.</typeparam>
		/// <param name="url">The target URL (must NOT contain auth credentials).</param>
		/// <param name="headers">Optional request headers, e.g. Authorization.</param>
		public static async Task<T> GetAsync<T>( string url, Dictionary<string, string> headers = null )
		{
			try
			{
				// In s&box, we can't send a raw HttpRequestMessage to Http.RequestAsync.
				// We use an empty content object to attach headers.
				using var content = new ByteArrayContent( Array.Empty<byte>() );
				
				if ( headers != null )
					foreach ( var kv in headers )
						content.Headers.TryAddWithoutValidation( kv.Key, kv.Value );

				var response = await Http.RequestAsync( url, "GET", content );
				var responseString = await response.Content.ReadAsStringAsync();

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
		/// <typeparam name="T">The type of data to serialize and send.</typeparam>
		/// <param name="url">The target URL (must NOT contain auth credentials).</param>
		/// <param name="data">The data to PUT.</param>
		/// <param name="headers">Optional request headers, e.g. Authorization.</param>
		public static async Task<bool> PutAsync<T>( string url, T data, Dictionary<string, string> headers = null )
		{
			try
			{
				// Use native sbox Http.CreateJsonContent for the body
				using var content = Sandbox.Http.CreateJsonContent( data );

				if ( headers != null )
				{
					foreach ( var kv in headers )
						content.Headers.TryAddWithoutValidation( kv.Key, kv.Value );
				}

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
		/// <param name="url">The target URL (must NOT contain auth credentials).</param>
		/// <param name="headers">Optional request headers, e.g. Authorization.</param>
		public static async Task<bool> DeleteAsync( string url, Dictionary<string, string> headers = null )
		{
			try
			{
				// Use empty content to attach headers for the DELETE request
				using var content = new ByteArrayContent( Array.Empty<byte>() );
				
				if ( headers != null )
					foreach ( var kv in headers )
						content.Headers.TryAddWithoutValidation( kv.Key, kv.Value );

				await Http.RequestAsync( url, "DELETE", content );
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
