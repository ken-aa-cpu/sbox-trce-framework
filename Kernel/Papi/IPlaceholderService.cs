using Sandbox;

namespace Trce.Kernel.Papi
{
	/// <summary>
	/// <para>【Phase 4 — Placeholder 服務合約 (Service Contract)】</para>
	/// <para>
	/// TRCE Placeholder API 的公開服務介面。此介面是整個 PAPI 系統的唯一存取點，
	/// 由 <see cref="TrceServiceManager"/> 持有並對外提供。
	/// </para>
	/// <para>
	/// <b>架構原則：</b><br/>
	/// 此介面「絕對不包含」任何如 <c>%player_money%</c> 的具體標籤邏輯。
	/// 它是一個純粹的「轉運站 (Dispatcher)」，職責僅為：
	/// <list type="number">
	///   <item><description>維護一個以 <c>prefix</c> 為鍵的 <see cref="ITrcePlaceholderProvider"/> 字典。</description></item>
	///   <item><description>解析輸入字串中的 <c>%prefix_tag%</c> 佔位符，並分派給對應的 Provider。</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// <b>插件整合範例：</b>
	/// <code>
	/// // 在你的 EconomyPlugin 的 OnPluginEnabled() 中：
	/// var papi = GetService&lt;IPlaceholderService&gt;();
	/// papi?.RegisterProvider("economy", this); // 'this' 實作 ITrcePlaceholderProvider
	///
	/// // 在 UI 模板字串中：
	/// // "你的餘額：%economy_balance% 金幣"
	/// // → PAPI 找到 prefix="economy" 的 provider，呼叫 TryResolvePlaceholder("economy_balance")
	/// </code>
	/// </para>
	/// </summary>
	public interface IPlaceholderService
	{
		/// <summary>
		/// <para>向 Placeholder 服務中心註冊一個標籤解析提供者。</para>
		/// <para>
		/// <b>Prefix 慣例：</b><br/>
		/// Prefix 應使用全小寫、無空格的識別碼（例如 <c>"economy"</c>、<c>"pet"</c>、<c>"quest"</c>）。<br/>
		/// 系統將自動以小寫形式正規化 <paramref name="prefix"/>。<br/>
		/// 若相同 prefix 已被註冊，新的 <paramref name="provider"/> 將覆蓋舊有的。
		/// </para>
		/// <para>
		/// 此操作為一次性設定，<b>不在熱路徑 (Hot Path) 上</b>。
		/// </para>
		/// </summary>
		/// <param name="prefix">
		/// 此提供者負責解析的標籤前綴（不含底線 <c>_</c>）。<br/>
		/// 例如 prefix = <c>"economy"</c> 將負責解析 <c>%economy_*%</c> 格式的所有標籤。
		/// </param>
		/// <param name="provider">實作了 <see cref="ITrcePlaceholderProvider"/> 的解析器實例，不可為 null。</param>
		void RegisterProvider( string prefix, ITrcePlaceholderProvider provider );

		/// <summary>
		/// <para>從 Placeholder 服務中心移除指定 prefix 的標籤解析提供者。</para>
		/// <para>若該 prefix 的 Provider 不存在，此方法靜默地無操作 (No-Op)。</para>
		/// <para>通常在對應的插件停用 (<c>OnPluginDisabled</c>) 時呼叫，以確保不留下懸空參考。</para>
		/// </summary>
		/// <param name="prefix">要移除的提供者的 prefix（大小寫不敏感）。</param>
		void UnregisterProvider( string prefix );

		/// <summary>
		/// <para>【核心方法】解析字串中所有 <c>%prefix_key%</c> 格式的佔位符，並回傳替換後的結果。</para>
		/// <para>
		/// <b>解析規則：</b>
		/// <list type="bullet">
		///   <item><description>佔位符格式為 <c>%{prefix}_{key_suffix}%</c>（必須包含底線 <c>_</c> 以區分 prefix）。</description></item>
		///   <item><description>系統以第一個 <c>_</c> 分割，提取 prefix 並在字典中查找對應 Provider。</description></item>
		///   <item><description>若 Provider 存在，呼叫 <see cref="ITrcePlaceholderProvider.TryResolvePlaceholder"/> 傳入完整的 key（含 prefix）。</description></item>
		///   <item><description>若 Provider 不存在或回傳 <c>null</c>，保留原始佔位符文字不做替換。</description></item>
		/// </list>
		/// </para>
		/// <para>
		/// <b>效能說明：</b>此方法的設計目標是 Zero-GC 傾向。
		/// 實作類別應使用 <see cref="System.Text.StringBuilder"/> 或
		/// <see cref="System.Span{T}"/> 等低分配策略，並搭配結果快取 (Cache)
		/// 來避免對靜態模板字串的重複解析。
		/// </para>
		/// </summary>
		/// <param name="text">包含零個或多個 <c>%prefix_key%</c> 佔位符的原始字串。</param>
		/// <param name="context">
		/// (可選) 觸發解析的場景物件，供 Provider 存取特定物件的上下文資訊（例如取得玩家的 HP）。
		/// 若為 <c>null</c>，Provider 應回退至全域/靜態資料。
		/// </param>
		/// <returns>所有可識別的佔位符已被替換後的字串。若輸入為 null 或空白，直接回傳原值。</returns>
		string Parse( string text, GameObject context = null );
	}
}
