using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

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
}

