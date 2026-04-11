using System;
using System.Text;

namespace Trce.Kernel.Security

{
	/// <summary>
	///   /   (s&amp;box Whitelist  )
	///
	///
	/// ��@�����G
	///   / s&amp;box   System.Security.Cryptography
	///   /   C#   FNV-1a +
	///
	/// �w���ʦҶq�G
	///   /   HMAC  Server
	/// /   -   Server
	///   /   - Client   Server   [Sync]
	///   /   -
	///
	/// �ϥΤ覡�G
	///   /   string sig = HmacSigner.Sign( "data", roundSecret );
	///   /   bool ok = HmacSigner.Verify( "data", sig, roundSecret );
	/// </summary>
	public static class HmacSigner
	{
		/// <summary>
		/// </summary>
		/// <param name="rawData"> </param>
		/// <param name="secret">  ( )</param>
		/// <returns>Hex  </returns>
		public static string Sign( string rawData, string secret )
		{
			if ( string.IsNullOrEmpty( rawData ) || string.IsNullOrEmpty( secret ) )
				return string.Empty;
			// : secret + rawData + secret ( )
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
		///   /   Guid   (s&amp;box   System.Guid)
		/// </summary>
		public static string GenerateRoundSecret()
		{
			return $"{Guid.NewGuid():N}{Guid.NewGuid():N}";
		}

		/// <summary>
		/// </summary>
		public static string SignFields( string secret, params string[] fields )
		{
			var combined = string.Join( "|", fields );
			return Sign( combined, secret );
		}

		/// <summary>
		/// </summary>
		public static bool VerifyFields( string signature, string secret, params string[] fields )
		{
			var combined = string.Join( "|", fields );
			return Verify( combined, signature, secret );
		}

		// ������������������������������������������������������������������������������
		// FNV-1a 64-bit Hash (  C#  )
		// System.Security.Cryptography
		// ������������������������������������������������������������������������������
		private const ulong FnvOffsetBasis = 14695981039346656037;
		private const ulong FnvPrime = 1099511628211;
		/// <summary>
		/// FNV-1a 64-bit ������
		/// �ֳt�B���ä����B�s�~���̿�
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

