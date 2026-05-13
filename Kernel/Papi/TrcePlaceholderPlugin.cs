using Sandbox;
using System;
using System.Collections.Generic;
using System.Text;

using System.Threading.Tasks;
using Trce.Kernel.Plugin;

namespace Trce.Kernel.Papi
{
	/// <summary>
	/// <para>【Phase 4 — TRCE Placeholder Core Service Plugin】</para>
	/// <para>
	/// Inherits from <see cref="TrcePlugin"/> and implements <see cref="IPlaceholderService"/>.<br/>
	/// This plugin acts as the "relay service" of the PAPI system — it contains
	/// <b>no concrete tag resolution logic of its own</b>.<br/>
	/// All specific placeholder resolution (e.g. <c>%economy_balance%</c>) must be injected by
	/// each business plugin via <see cref="RegisterProvider"/>.
	/// </para>
	/// <para>
	/// <b>Lifecycle:</b><br/>
	/// <c>OnPluginEnabled()</c> → registers itself with <see cref="TrceServiceManager"/> as <see cref="IPlaceholderService"/>.<br/>
	/// <c>OnPluginDisabled()</c> → unregisters from <see cref="TrceServiceManager"/> and clears all registered Providers.
	/// </para>
	/// <para>
	/// <b>Performance architecture (Zero-GC oriented):</b>
	/// <list type="bullet">
	///   <item><description>
	///     <b>Provider dictionary:</b> a <see cref="Dictionary{TKey,TValue}"/> keyed by lowercase string prefix.
	///     All reads and writes are protected by <c>lock (_lock)</c>
	///     (s&amp;box sandbox blocks <c>ReaderWriterLockSlim</c>; <c>object + lock</c> is equivalent
	///     in the single-threaded game environment).
	///   </description></item>
	///   <item><description>
	///     <b>Parse hot path:</b> uses a reusable <see cref="StringBuilder"/> obtained from a pool,
	///     avoiding a new string-builder allocation on every parse call (zero GC alloc).
	///   </description></item>
	///   <item><description>
	///     <b>Span slicing:</b> uses <see cref="ReadOnlySpan{T}"/> (<c>AsSpan()</c>) and
	///     <see cref="string.IndexOf(char, int)"/> to locate <c>%</c> boundaries,
	///     completely avoiding <see cref="string.Substring"/> during prefix extraction.
	///   </description></item>
	/// </list>
	/// </para>
	/// </summary>
	[Title( "Placeholder Service" ), Group( "Trce - Kernel/Papi" ), Icon( "tag" )]
	public sealed class TrcePlaceholderPlugin : TrcePlugin, IPlaceholderService
	{
		// ─────────────────────────────────────────────
		//  Internal Storage
		// ─────────────────────────────────────────────

		/// <summary>
		/// Provider dictionary keyed by lowercase prefix. All reads and writes are protected by <see cref="_lock"/>.
		/// </summary>
		private readonly Dictionary<string, ITrcePlaceholderProvider> _providers = new( StringComparer.Ordinal );

		/// <summary>
		/// Synchronization lock. The s&amp;box sandbox blocks <c>ReaderWriterLockSlim</c>;
		/// <c>object + lock</c> provides thread-safe protection instead.
		/// </summary>
		private readonly object _lock = new();

		// ─────────────────────────────────────────────
		//  Reusable StringBuilder Pool (Zero-GC core)
		// ─────────────────────────────────────────────

		/// <summary>
		/// Reusable <see cref="StringBuilder"/> instance for use by <see cref="Parse"/> on the main thread.
		/// <para>
		/// <b>Note:</b> s&amp;box UI updates and game logic both run on the main thread, so sharing
		/// a single <see cref="StringBuilder"/> is safe. If multi-threaded parsing is needed in the
		/// future, upgrade to <c>ThreadLocal&lt;StringBuilder&gt;</c> or an object pool.
		/// </para>
		/// </summary>
		private readonly StringBuilder _sharedBuilder = new( 256 );

		// ─────────────────────────────────────────────
		//  Lifecycle
		// ─────────────────────────────────────────────

		/// <summary>
		/// <para>Called when the plugin is enabled. Registers itself with <see cref="TrceServiceManager"/> as the <see cref="IPlaceholderService"/> implementation.</para>
		/// <para>After this call, all other plugins can obtain the service via <c>GetService&lt;IPlaceholderService&gt;()</c>.</para>
		/// </summary>
		/// <returns>A <see cref="Task"/> representing the async operation.</returns>
		protected override async Task OnPluginEnabled()
		{
			TrceServiceManager.Instance?.RegisterService<IPlaceholderService>( this );
			Log.Info( "🏷️ [TrcePlaceholderPlugin] IPlaceholderService registered. Ready to accept providers." );
			await Task.CompletedTask;
		}

		/// <summary>
		/// <para>Called when the plugin is disabled. Unregisters the service from <see cref="TrceServiceManager"/> and clears all registered Providers.</para>
		/// <para>Ensures no dangling service or Provider references remain after the plugin stops.</para>
		/// </summary>
		protected override void OnPluginDisabled()
		{
			TrceServiceManager.Instance?.UnregisterService<IPlaceholderService>();

			// Clear all Providers to release references to other plugin instances and prevent memory leaks.
			lock ( _lock )
			{
				_providers.Clear();
			}

			Log.Info( "🏷️ [TrcePlaceholderPlugin] IPlaceholderService unregistered. All providers cleared." );
		}

		// ─────────────────────────────────────────────
		//  IPlaceholderService Implementation
		// ─────────────────────────────────────────────

		/// <inheritdoc/>
		/// <remarks>
		/// If a provider already exists for the same <paramref name="prefix"/>, the new
		/// <paramref name="provider"/> replaces the old one and a log message is emitted
		/// so developers can diagnose conflicts.
		/// </remarks>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="prefix"/> is null/whitespace or <paramref name="provider"/> is null.
		/// </exception>
		public void RegisterProvider( string prefix, ITrcePlaceholderProvider provider )
		{
			if ( string.IsNullOrWhiteSpace( prefix ) )
				throw new ArgumentNullException( nameof(prefix), "[TrcePlaceholderPlugin] Provider prefix cannot be null or whitespace." );

			if ( provider is null )
				throw new ArgumentNullException( nameof(provider), $"[TrcePlaceholderPlugin] Cannot register a null provider for prefix '{prefix}'." );

			// Normalize: force lowercase to avoid case-sensitive lookup mismatches.
			var normalizedPrefix = prefix.ToLowerInvariant();

			lock ( _lock )
			{
				if ( _providers.TryGetValue( normalizedPrefix, out var existing ) )
				{
					Log.Info( $"🔄 [TrcePlaceholderPlugin] Provider for prefix '%{normalizedPrefix}_*%' replaced: '{existing.GetType().Name}' → '{provider.GetType().Name}'." );
				}
				_providers[normalizedPrefix] = provider;
			}

			Log.Info( $"✅ [TrcePlaceholderPlugin] Registered placeholder provider: prefix='%{normalizedPrefix}_*%' → {provider.GetType().Name}" );
		}

		/// <inheritdoc/>
		public void UnregisterProvider( string prefix )
		{
			if ( string.IsNullOrWhiteSpace( prefix ) )
				return;

			var normalizedPrefix = prefix.ToLowerInvariant();
			bool removed;

			lock ( _lock )
			{
				removed = _providers.Remove( normalizedPrefix );
			}

			if ( removed )
				Log.Info( $"🗑️ [TrcePlaceholderPlugin] Unregistered placeholder provider for prefix: '%{normalizedPrefix}_*%'" );
		}

		/// <inheritdoc/>
		/// <remarks>
		/// <para>
		/// <b>Zero-GC parse strategy:</b>
		/// <list type="number">
		///   <item><description>Uses boundary detection (<c>IndexOf('%')</c>) to scan the input, avoiding Regex GC pressure.</description></item>
		///   <item><description>Uses <c>AsSpan()</c> + <c>IndexOf('_')</c> to extract the prefix, avoiding <c>Substring</c> allocation.</description></item>
		///   <item><description>Reuses <see cref="_sharedBuilder"/> (calling <c>Clear()</c> before each use) to avoid creating a new <see cref="StringBuilder"/>.</description></item>
		///   <item><description>Provider dictionary lookup is a <c>TryGetValue</c> call protected by <c>lock (_lock)</c> — O(1), zero GC.</description></item>
		///   <item><description>Only calls <c>ToString()</c> when at least one replacement occurred; otherwise returns the original input reference (zero alloc).</description></item>
		/// </list>
		/// </para>
		/// </remarks>
		public string Parse( string text, GameObject context = null )
		{
			// Fast path: null, empty string, or no '%' character → return the original reference (zero-GC, zero alloc).
			if ( string.IsNullOrEmpty( text ) )
				return text;

			int firstPercent = text.IndexOf( '%' );
			if ( firstPercent < 0 )
				return text;

			// Enter the parse path — reuse the shared StringBuilder to avoid repeated allocation.
			var builder = _sharedBuilder;
			builder.Clear();

			int i = 0;
			int length = text.Length;
			bool anyReplacement = false;

			// Acquire the lock to protect reads from the Provider dictionary.
			lock ( _lock )
			{
				while ( i < length )
				{
					if ( text[i] != '%' )
					{
						builder.Append( text[i] );
						i++;
						continue;
					}

					// Found a '%' — look for the closing '%'.
					int end = text.IndexOf( '%', i + 1 );

					// No closing '%', or empty placeholder (%%) — treat as a literal character.
					if ( end <= i + 1 )
					{
						builder.Append( '%' );
						i++;
						continue;
					}

					// Extract the placeholder key (without surrounding %), e.g. "economy_balance".
					// Use Span to avoid Substring heap allocation.
					ReadOnlySpan<char> keySpan = text.AsSpan( i + 1, end - i - 1 );

					// Key must contain an underscore to split the prefix.
					int underscoreIdx = keySpan.IndexOf( '_' );
					if ( underscoreIdx <= 0 )
					{
						// No prefix (e.g. %somekey%) — preserve the original text.
						builder.Append( '%' );
						builder.Append( keySpan );
						builder.Append( '%' );
						i = end + 1;
						continue;
					}

					// Extract the prefix span (e.g. "economy").
					ReadOnlySpan<char> prefixSpan = keySpan.Slice( 0, underscoreIdx );

					// Dictionary lookup requires a string key. The ToString() here is the only mandatory alloc.
					// 🔧 Optimization note: .NET 9+ offers Dictionary<string,V>.GetValueOrDefault(ReadOnlySpan<char>)
					//    and similar APIs. For maximum compatibility the current approach retains ToString().
					//    If profiling identifies this as a bottleneck, consider a custom IEqualityComparer<string>
					//    with CollectionsMarshal.GetValueRefOrNullRef to eliminate this allocation.
					string prefixKey = prefixSpan.ToString().ToLowerInvariant();

					if ( _providers.TryGetValue( prefixKey, out var provider ) )
					{
						// Pass the full key to the Provider (the provider decides how to handle the entire key, including prefix).
						// This Substring is required by the Provider contract on the hit path and cannot be eliminated further.
						string fullKey = text.Substring( i + 1, end - i - 1 );
						string resolved = provider.TryResolvePlaceholder( fullKey );

						if ( resolved is not null )
						{
							builder.Append( resolved );
							i = end + 1;
							anyReplacement = true;
							continue;
						}
					}

					// Provider not found or returned null → preserve the original placeholder text.
					builder.Append( '%' );
					builder.Append( keySpan );
					builder.Append( '%' );
					i = end + 1;
				}
			}

			// If no valid replacement occurred, return the original string reference (zero-GC — avoids ToString() alloc).
			if ( !anyReplacement )
				return text;

			return builder.ToString();
		}
	}
}
