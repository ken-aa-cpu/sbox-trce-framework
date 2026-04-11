using Sandbox;
using System;
using System.Collections.Generic;
using System.Text;

using System.Threading.Tasks;
using Trce.Kernel.Plugin;

namespace Trce.Kernel.Papi
{
	/// <summary>
	/// <para>【Phase 4 — TRCE Placeholder 核心服務插件】</para>
	/// <para>
	/// 繼承自 <see cref="TrcePlugin"/>，並實作 <see cref="IPlaceholderService"/> 介面。<br/>
	/// 此插件作為 PAPI 系統的「轉運服務」，自身<b>不包含任何具體的標籤邏輯</b>。<br/>
	/// 所有具體的佔位符解析（如 <c>%economy_balance%</c>）必須由各自的業務插件透過
	/// <see cref="RegisterProvider"/> 自行注入。
	/// </para>
	/// <para>
	/// <b>生命週期：</b><br/>
	/// <c>OnPluginEnabled()</c> → 向 <see cref="TrceServiceManager"/> 注冊自身為 <see cref="IPlaceholderService"/>。<br/>
	/// <c>OnPluginDisabled()</c> → 從 <see cref="TrceServiceManager"/> 登出，並清空所有已注冊的 Provider。
	/// </para>
	/// <para>
	/// <b>效能架構（Zero-GC 傾向）：</b>
	/// <list type="bullet">
	///   <item><description>
	///     <b>Provider 字典</b>：以 <c>string prefix</c> 為鍵的 <see cref="Dictionary{TKey,TValue}"/>，
	///     所有讀寫操作透過 <c>lock (_lock)</c> 保護（s&box 沙盒環境不允許 ReaderWriterLockSlim，
	///     改用 object + lock 語法，在單執行緒遊戲環境中效果相同）。
	///   </description></item>
	///   <item><description>
	///     <b>Parse 熱路徑</b>：使用預先從 <see cref="StringBuilderPool"/> 取得的可複用
	///     <see cref="StringBuilder"/>，避免每次解析都建立新的字串建構器物件（零 GC 分配）。
	///   </description></item>
	///   <item><description>
	///     <b>Span 切割</b>：使用 <see cref="ReadOnlySpan{T}"/>（<c>AsSpan()</c>）和
	///     <see cref="string.IndexOf(char, int)"/> 定位 <c>%</c> 邊界，
	///     完全避免 <see cref="string.Substring"/> 在 prefix 提取階段的分配。
	///   </description></item>
	/// </list>
	/// </para>
	/// </summary>
	[Title( "Placeholder Service" ), Group( "Trce - Kernel/Papi" ), Icon( "tag" )]
	public sealed class TrcePlaceholderPlugin : TrcePlugin, IPlaceholderService
	{
		// ─────────────────────────────────────────────
		//  內部儲存 (Internal Storage)
		// ─────────────────────────────────────────────

		/// <summary>
		/// 以小寫 prefix 為鍵的 Provider 字典。所有讀寫操作由 <see cref="_lock"/> 保護。
		/// </summary>
		private readonly Dictionary<string, ITrcePlaceholderProvider> _providers = new( StringComparer.Ordinal );

		/// <summary>
		/// 同步鎖。s&box 沙盒環境封鎖了 ReaderWriterLockSlim，
		/// 改用 object + lock 語法進行執行緒安全保護。
		/// </summary>
		private readonly object _lock = new();

		// ─────────────────────────────────────────────
		//  可複用 StringBuilder 池 (Zero-GC 核心)
		// ─────────────────────────────────────────────

		/// <summary>
		/// 可複用的 <see cref="StringBuilder"/> 實例，供主線程上的 <see cref="Parse"/> 使用。
		/// <para>
		/// <b>注意：</b>s&amp;box 的 UI 更新和遊戲邏輯均在主線程上執行，
		/// 因此使用單一共享的 StringBuilder 是安全的。
		/// 若日後有多線程解析需求，應升級為 <c>ThreadLocal&lt;StringBuilder&gt;</c> 或物件池。
		/// </para>
		/// </summary>
		private readonly StringBuilder _sharedBuilder = new( 256 );

		// ─────────────────────────────────────────────
		//  生命週期 (Lifecycle)
		// ─────────────────────────────────────────────

		/// <summary>
		/// <para>插件啟用時呼叫。向 <see cref="TrceServiceManager"/> 注冊自身為 <see cref="IPlaceholderService"/> 的實作。</para>
		/// <para>此操作完成後，所有其他插件可透過 <c>GetService&lt;IPlaceholderService&gt;()</c> 取得此服務。</para>
		/// </summary>
		/// <returns>代表非同步操作的 <see cref="Task"/>。</returns>
		protected override async Task OnPluginEnabled()
		{
			TrceServiceManager.Instance?.RegisterService<IPlaceholderService>( this );
			Log.Info( "🏷️ [TrcePlaceholderPlugin] IPlaceholderService registered. Ready to accept providers." );
			await Task.CompletedTask;
		}

		/// <summary>
		/// <para>插件停用時呼叫。從 <see cref="TrceServiceManager"/> 登出服務，並清空所有已注冊的 Provider。</para>
		/// <para>此操作確保插件停用後不留下任何懸空服務參考或 Provider 參考。</para>
		/// </summary>
		protected override void OnPluginDisabled()
		{
			TrceServiceManager.Instance?.UnregisterService<IPlaceholderService>();

			// 清空所有 Provider，釋放對其他插件實例的參考，防止 Memory Leak
			lock ( _lock )
			{
				_providers.Clear();
			}

			Log.Info( "🏷️ [TrcePlaceholderPlugin] IPlaceholderService unregistered. All providers cleared." );
		}

		// ─────────────────────────────────────────────
		//  IPlaceholderService 實作
		// ─────────────────────────────────────────────

		/// <inheritdoc/>
		/// <remarks>
		/// 若相同 <paramref name="prefix"/> 已存在，新 <paramref name="provider"/> 將覆蓋舊有的，
		/// 並輸出日誌告知開發者（方便排查衝突）。
		/// </remarks>
		/// <exception cref="ArgumentNullException">
		/// 當 <paramref name="prefix"/> 為 null/空白 或 <paramref name="provider"/> 為 null 時拋出。
		/// </exception>
		public void RegisterProvider( string prefix, ITrcePlaceholderProvider provider )
		{
			if ( string.IsNullOrWhiteSpace( prefix ) )
				throw new ArgumentNullException( nameof(prefix), "[TrcePlaceholderPlugin] Provider prefix cannot be null or whitespace." );

			if ( provider is null )
				throw new ArgumentNullException( nameof(provider), $"[TrcePlaceholderPlugin] Cannot register a null provider for prefix '{prefix}'." );

			// 正規化：強制小寫，避免大小寫造成查找失敗
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
		/// <b>Zero-GC 解析策略：</b>
		/// <list type="number">
		///   <item><description>使用預定義邊界偵測（<c>IndexOf('%')</c>）掃描輸入，避免 <c>Regex</c> 的 GC 壓力。</description></item>
		///   <item><description>使用 <c>AsSpan()</c> + <c>IndexOf('_')</c> 提取 prefix，避免 <c>Substring</c> 分配。</description></item>
		///   <item><description>複用 <see cref="_sharedBuilder"/>（每次使用前呼叫 <c>Clear()</c>），避免建立新的 <see cref="StringBuilder"/>。</description></item>
		///   <item><description>Provider 字典查找透過 <c>lock (_lock)</c> 保護的 <c>TryGetValue</c> 完成（O(1)，無 GC）。</description></item>
		///   <item><description>只有在確實有替換發生時才呼叫 <c>ToString()</c>，否則直接回傳原始輸入（零分配）。</description></item>
		/// </list>
		/// </para>
		/// </remarks>
		public string Parse( string text, GameObject context = null )
		{
			// 快速路徑：null、空字串、或不含 '%' 符號 → 直接回傳原始參考（Zero-GC，無任何分配）
			if ( string.IsNullOrEmpty( text ) )
				return text;

			int firstPercent = text.IndexOf( '%' );
			if ( firstPercent < 0 )
				return text;

			// 進入解析路徑，使用共享 StringBuilder 以避免重複分配
			var builder = _sharedBuilder;
			builder.Clear();

			int i = 0;
			int length = text.Length;
			bool anyReplacement = false;

			// 進入鎖：保護 Provider 字典的讀取操作
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

					// 找到第一個 '%'，尋找配對的結束 '%'
					int end = text.IndexOf( '%', i + 1 );

					// 若無結束 '%' 或為空佔位符 (%%)，視為普通字元
					if ( end <= i + 1 )
					{
						builder.Append( '%' );
						i++;
						continue;
					}

					// 提取佔位符鍵（不含兩端的 %），例如 "economy_balance"
					// 使用 Span 避免 Substring 的堆積分配
					ReadOnlySpan<char> keySpan = text.AsSpan( i + 1, end - i - 1 );

					// 必須包含底線才能分割 prefix
					int underscoreIdx = keySpan.IndexOf( '_' );
					if ( underscoreIdx <= 0 )
					{
						// 無 prefix（如 %somekey%），保留原文
						builder.Append( '%' );
						builder.Append( keySpan );
						builder.Append( '%' );
						i = end + 1;
						continue;
					}

					// 提取 prefix span（例如 "economy"）
					ReadOnlySpan<char> prefixSpan = keySpan.Slice( 0, underscoreIdx );

					// 字典查找：需要 string key。此處的 ToString() 是唯一必要的分配點。
					// 🔧 優化說明：.NET 9+ 提供 Dictionary<string, V>.GetValueOrDefault(ReadOnlySpan<char>)
					//    等 API，但目前為了最大相容性，此處保留 ToString()。
					//    若效能分析 (Profiling) 顯示此為瓶頸，可升級至使用自訂 IEqualityComparer<string>
					//    搭配 CollectionsMarshal.GetValueRefOrNullRef 來嘗試減少此分配。
					string prefixKey = prefixSpan.ToString().ToLowerInvariant();

					if ( _providers.TryGetValue( prefixKey, out var provider ) )
					{
						// 將完整 key 傳給 Provider（Provider 需要決定如何處理 entire key，包含 prefix）
						// 此 Substring 在命中路徑上是 Provider 合約要求的，無法進一步消除。
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

					// Provider 未找到或回傳 null → 保留原始佔位符文字
					builder.Append( '%' );
					builder.Append( keySpan );
					builder.Append( '%' );
					i = end + 1;
				}
			}

			// 若解析過程中無任何有效替換，直接回傳原始字串參考（Zero-GC，避免 ToString() 分配）
			if ( !anyReplacement )
				return text;

			return builder.ToString();
		}
	}
}
