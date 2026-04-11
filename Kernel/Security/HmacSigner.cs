using System;
using System.Text;

namespace Trce.Kernel.Security

{
	/// <summary>
	/// 輕量級安全簽章產生器 (因應 s&box 白名單限制)
	///
	/// 由於 s&box 禁用了 System.Security.Cryptography 大部分功能，
	/// 這裡使用 C# 實作的 FNV-1a 雜湊加上自訂格式來模擬簽章。
	///
	/// 適用場景: HMAC 簽章與驗證 (Server 端為主)
	/// - Server 產生包含簽章的資料
	/// - Client 透過 [Sync] 從 Server 收到附帶簽章的資料
	/// - 當 Client 需要進行關鍵操作時，傳回資料與簽章讓 Server 驗證
	///
	/// Usage:
	///   string sig = HmacSigner.Sign( "data", roundSecret );
	///   bool ok = HmacSigner.Verify( "data", sig, roundSecret );
	/// </summary>
	public static class HmacSigner
	{
		/// <summary> 利用指定金鑰對字串資料進行簽章 </summary>
		/// <param name="rawData">要簽章的原始資料</param>
		/// <param name="secret">用於加密的金鑰 (例如 RoundSecret)</param>
		/// <returns>Hex 格式的簽章字串</returns>
		public static string Sign( string rawData, string secret )
		{
			if ( string.IsNullOrEmpty( rawData ) || string.IsNullOrEmpty( secret ) )
				return string.Empty;
			// 透過在前後加上金鑰來簡單模擬防篡改: secret + rawData + secret
			var combined = $"{secret}|{rawData}|{secret}";
			var hash = Fnv1aHash64( combined );
			return hash.ToString( "x16" );
		}

		/// <summary>
		/// </summary>
		public static bool Verify( string rawData, string signature, string secret )
		{
			if ( string.IsNullOrEmpty( signature ) )
				return false;
			var expected = Sign( rawData, secret );
			return string.Equals( expected, signature, StringComparison.OrdinalIgnoreCase );
		}

		/// <summary>
		/// 產生一組隨機的安全金鑰 (利用 s&box 支援的 System.Guid)
		/// </summary>
		public static string GenerateRoundSecret()
		{
			return $"{Guid.NewGuid():N}{Guid.NewGuid():N}";
		}

		/// <summary> 將多個欄位循序組合後產生簽章字串 </summary>
		public static string SignFields( string secret, params string[] fields )
		{
			var combined = string.Join( "|", fields );
			return Sign( combined, secret );
		}

		/// <summary> 驗證多個欄位組合後的簽章結果是否相符 </summary>
		public static bool VerifyFields( string signature, string secret, params string[] fields )
		{
			var combined = string.Join( "|", fields );
			return Verify( combined, signature, secret );
		}

		// FNV-1a 64-bit Hash (C#)
		// System.Security.Cryptography
		private const ulong FnvOffsetBasis = 14695981039346656037;
		private const ulong FnvPrime = 1099511628211;
		/// <summary>
		/// FNV-1a 64-bit 核心演算法實作
		/// </summary>
		private static ulong Fnv1aHash64( string input )
		{
			ulong hash = FnvOffsetBasis;
			for ( int i = 0; i < input.Length; i++ )
			{
				hash ^= input[i];
				hash *= FnvPrime;
			}
			return hash;
		}

	}

}

