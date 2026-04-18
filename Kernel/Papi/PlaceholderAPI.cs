using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Trce.Kernel.Plugin;

namespace Trce.Kernel.Papi
{

	/// <summary>
	/// TRACE-PAPI (Placeholder API) v2.0
	/// Dynamic text replacement system with Hierarchy Scoping support.
	/// </summary>
	[Title( "Placeholder API" ), Group( "Trce - Kernel" )]
	public class PlaceholderAPI : Component
	{
		private static PlaceholderAPI globalInstance;
		public static PlaceholderAPI Global => globalInstance;

		[Property] public bool IsGlobal { get; set; } = false;

		private readonly Dictionary<string, Func<string>> placeholders = new();
		private readonly List<ITrcePlaceholderProvider> providers = new();

		// Cache system
		private readonly Dictionary<string, (string value, float expireAt)> valueCache = new();
		private readonly Dictionary<string, (string result, float expireAt)> templateCache = new();

		/// <summary>
		/// Default time-to-live for cached placeholder values, in seconds.
		/// A short TTL ensures near-real-time updates while avoiding per-frame re-resolution.
		/// </summary>
		private const float DefaultCacheTtlSeconds = 0.1f;

		public float CacheTtl { get; set; } = DefaultCacheTtlSeconds;

		protected override void OnAwake()
		{
			if ( IsGlobal )
			{
				globalInstance = this;

				// P1-3: Also register in TrceServiceManager so GetService<IPlaceholderService>() can
				// find this instance. TrcePlaceholderPlugin (if present) will overwrite this entry
				// with its own registration — that is the intended "last-writer-wins" behaviour.
				TrceServiceManager.Instance?.RegisterService<IPlaceholderService>( new PlaceholderAPIBridge( this ) );
			}
			Log.Info( $"[PAPI:{( IsGlobal ? "Global" : "Local" )}] Placeholder system initialized." );
		}

		/// <summary>Find the closest PAPI instance to the given context.</summary>
		public static PlaceholderAPI For( GameObject context )
		{
			if ( context == null ) return Global;
			var local = context.Components.GetInAncestors<PlaceholderAPI>();
			return local ?? Global;
		}

		public static PlaceholderAPI For( Component comp ) => For( comp?.GameObject );

		// ═══════════════════════════════════════
		//  Management
		// ═══════════════════════════════════════

		public void Register( string name, Func<string> resolver )
		{
			if ( string.IsNullOrEmpty( name ) || resolver == null ) return;
			placeholders[name.ToLowerInvariant()] = resolver;
			InvalidateCache();
		}

		public void RegisterProvider( ITrcePlaceholderProvider provider )
		{
			if ( provider == null || providers.Contains( provider ) ) return;
			providers.Add( provider );
			InvalidateCache();
		}

		public void UnregisterProvider( ITrcePlaceholderProvider provider )
		{
			if ( providers.Remove( provider ) )
				InvalidateCache();
		}

		public void InvalidateCache()
		{
			valueCache.Clear();
			templateCache.Clear();
		}

		// ═══════════════════════════════════════
		//  Replacement Logic (Core)
		// ═══════════════════════════════════════

		/// <summary>
		/// Static convenience method: auto-finds context and replaces placeholders.
		/// </summary>
		public static string Replace( GameObject context, string input )
		{
			var papi = For( context );
			return papi != null ? papi.ReplaceInternal( input ) : input;
		}

		public string ReplaceInternal( string input )
		{
			if ( string.IsNullOrEmpty( input ) ) return input;

			float now = Time.Now;
			if ( templateCache.TryGetValue( input, out var cached ) && now < cached.expireAt )
				return cached.result;

			var result = new System.Text.StringBuilder( input.Length );
			int i = 0;

			while ( i < input.Length )
			{
				if ( input[i] == '%' )
				{
					int end = input.IndexOf( '%', i + 1 );
					if ( end > i + 1 )
					{
						var name = input.Substring( i + 1, end - i - 1 ).ToLowerInvariant();
						string val = ResolveKey( name );

						if ( val != null )
						{
							result.Append( val );
							i = end + 1;
							continue;
						}
					}
				}
				result.Append( input[i] );
				i++;
			}

			var final = result.ToString();
			templateCache[input] = (final, now + CacheTtl);
			return final;
		}

		private string ResolveKey( string name )
		{
			float now = Time.Now;

			// 1. Check local cache
			if ( valueCache.TryGetValue( name, out var cached ) && now < cached.expireAt )
				return cached.value;

			// 2. Check direct local registrations
			if ( placeholders.TryGetValue( name, out var resolver ) )
			{
				var val = resolver();
				valueCache[name] = (val, now + CacheTtl);
				return val;
			}

			// 3. Check local Providers
			foreach ( var provider in providers )
			{
				var val = provider.TryResolvePlaceholder( name );
				if ( val != null )
				{
					valueCache[name] = (val, now + CacheTtl);
					return val;
				}
			}

			// 4. If not global instance, try resolving from global instance (recursive upward)
			if ( !IsGlobal && Global != null )
			{
				return Global.ResolveKey( name );
			}

			return null;
		}
	}

	// ═══════════════════════════════════════
	//  P1-3: IPlaceholderService Bridge
	// ═══════════════════════════════════════

	/// <summary>
	/// P1-3: Adapts the legacy <see cref="PlaceholderAPI"/> into the <see cref="IPlaceholderService"/> contract
	/// so it can be registered in <see cref="TrceServiceManager"/> and resolved via
	/// <c>GetService&lt;IPlaceholderService&gt;()</c>.
	/// <para>
	/// Only used when <see cref="TrcePlaceholderPlugin"/> is <i>not</i> present in the scene.
	/// If both coexist, <see cref="TrcePlaceholderPlugin"/> will overwrite this entry on its own
	/// <c>OnPluginEnabled()</c> call (last-writer-wins semantics).
	/// </para>
	/// </summary>
	private sealed class PlaceholderAPIBridge : IPlaceholderService
	{
		private readonly PlaceholderAPI _api;
		public PlaceholderAPIBridge( PlaceholderAPI api ) => _api = api;

		public void RegisterProvider( string prefix, ITrcePlaceholderProvider provider )
			=> _api.RegisterProvider( provider );

		public void UnregisterProvider( string prefix )
		{
			// The legacy PlaceholderAPI does not organise providers by prefix,
			// so prefix-based removal cannot be accurately performed through this bridge.
			// Callers should prefer TrcePlaceholderPlugin which supports this operation natively.
		}

		public string Parse( string text, GameObject context = null )
			=> PlaceholderAPI.Replace( context ?? _api.GameObject, text );
	}
}

