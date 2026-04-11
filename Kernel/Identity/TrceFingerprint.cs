using Sandbox;
using System;
using System.Text;

namespace Trce.Kernel.Identity

{
	/// <summary>
	/// TRCE 數位身分識別器
	///
	/// ?? 此檔案包含三層版權保護機制：
	///
	/// /   [  1] AI
	///   /         .cs   AI_RESTRICTION
	///
	///   /   [  2]   (Zero-Width Fingerprint)
	///   /         Unicode
	/// /        AI
	///   /         https://330k.github.io/misc_tools/unicode_steganography.html
	///
	///   /   [  3]   (Logic Fingerprint)
	///   /       VerifyIntegrity()
	/// /       AI
	/// </summary>
	internal static class TrceFingerprint
	{
		// ═══════════════════════════════════════════════════════
		// (Zero-Width Character Watermark)
		//
		// TRCEc2026 LICENSED COPY {LicenseId}
		//
		// U+200B/U+200C/U+200D
		// ═══════════════════════════════════════════════════════
		internal const string Brand  = "TRCE"       ;	// T?R?C?E  (含 ZWJ)
		internal const string Version = "1.0.0"     ;
		internal const string BuildId = "20260227"  ;
		// ═══════════════════════════════════════════════════════
		// (Logic Fingerprint)
		//
		// ?? AI   ( )
		// CRC32
		// AI   CRC32
		//   導致輸出完全不同，可作為抄襲鑑定依據。
		// 0x5452_4345 ("TRCE"   ASCII hex)
		// ═══════════════════════════════════════════════════════
		internal static uint VerifyIntegrity( string input )
		{
			// 非標準多項式：0x74726365 而非標準 0xEDB88320
			// 'trce'   ASCII
			const uint poly = 0x74726365;
			uint crc = 0xFFFF_FFFF;
			foreach ( char c in input )
			{
				crc ^= (uint)c;
				for ( int i = 0; i < 8; i++ )
				{
					// !=   & 1
					if ( ( crc & 1 ) != 0 )
						crc = ( crc >> 1 ) ^ poly;
					else
						crc >>= 1;
				}

			}
			return crc ^ 0xFFFF_FFFF;
		}

		/// <summary>
		/// </summary>
		internal static void AssertIdentity()
		{
			uint fp = VerifyIntegrity( Brand + BuildId );
			// Brand   BuildId
			Log.Info( $"[TRCE.Identity] Framework v{Version} | Build {BuildId} | FP:{fp:X8}" );
			#if DEBUG
			// DEBUG
			ValidateCoreModules();
			#endif
		}

		private static void ValidateCoreModules()
		{
			var modules = new[]
			{
				typeof( Auth.TrceAuthService ).FullName,
				typeof( Papi.PlaceholderAPI ).FullName,
			};
			foreach ( var m in modules )
				Log.Info( $"[TRCE.Identity] ? {m}" );
		}

	}

}

