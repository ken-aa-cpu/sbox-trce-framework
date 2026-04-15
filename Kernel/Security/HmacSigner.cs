// File: Kernel/Security/HmacSigner.cs
// Encoding: UTF-8 (No BOM)
// Pure-C# HMAC-SHA256. No dependency on System.Security.Cryptography.
// Drop-in replacement for the former FNV-1a-based signer.

using System;
using System.Text;

namespace Trce.Kernel.Security
{
	/// <summary>
	/// Lightweight HMAC-SHA256 signer for s&amp;box environments where
	/// System.Security.Cryptography is partially blocklisted.
	/// All public methods are API-compatible with the previous FNV-1a version.
	/// </summary>
	public static class HmacSigner
	{
		// ── Public API ────────────────────────────────────────────────────

		/// <summary>Signs rawData with the given secret key. Returns a 64-char lowercase hex string.</summary>
		/// <param name="rawData">The raw data to sign.</param>
		/// <param name="secret">The secret key (e.g. RoundSecret).</param>
		/// <returns>64-character lowercase hex signature string.</returns>
		public static string Sign( string rawData, string secret )
		{
			if ( string.IsNullOrEmpty( rawData ) || string.IsNullOrEmpty( secret ) )
				return string.Empty;

			var keyBytes  = Encoding.UTF8.GetBytes( secret );
			var dataBytes = Encoding.UTF8.GetBytes( rawData );
			var hash      = HmacSha256( keyBytes, dataBytes );
			return BytesToHex( hash );
		}

		/// <summary>Constant-time verification. Returns true if the signature matches.</summary>
		/// <param name="rawData">The original raw data.</param>
		/// <param name="signature">The signature to verify.</param>
		/// <param name="secret">The secret key used to sign.</param>
		/// <returns>True if the signature is valid; false otherwise.</returns>
		public static bool Verify( string rawData, string signature, string secret )
		{
			if ( string.IsNullOrEmpty( signature ) ) return false;
			var expected = Sign( rawData, secret );
			return ConstantTimeEquals( expected, signature );
		}

		/// <summary>Generates a 64-char cryptographically random secret using Guid entropy.</summary>
		/// <returns>A 64-character random secret string.</returns>
		public static string GenerateRoundSecret()
			=> $"{Guid.NewGuid():N}{Guid.NewGuid():N}";

		/// <summary>Signs the pipe-joined concatenation of <paramref name="fields"/>.</summary>
		/// <param name="secret">The secret key.</param>
		/// <param name="fields">Fields to join with '|' before signing.</param>
		/// <returns>Signature string.</returns>
		public static string SignFields( string secret, params string[] fields )
			=> Sign( string.Join( "|", fields ), secret );

		/// <summary>Verifies the pipe-joined concatenation of <paramref name="fields"/>.</summary>
		/// <param name="signature">The signature to verify.</param>
		/// <param name="secret">The secret key.</param>
		/// <param name="fields">Fields to join with '|' before verification.</param>
		/// <returns>True if the signature is valid.</returns>
		public static bool VerifyFields( string signature, string secret, params string[] fields )
			=> Verify( string.Join( "|", fields ), signature, secret );

		// ── HMAC-SHA256 Core ──────────────────────────────────────────────

		private const int BlockSize = 64;
		private const int HashSize  = 32;

		private static byte[] HmacSha256( byte[] key, byte[] data )
		{
			// Keys longer than block size are pre-hashed per RFC 2104
			if ( key.Length > BlockSize )
				key = Sha256( key );

			var k = new byte[BlockSize];
			Array.Copy( key, k, key.Length );

			var ipad = new byte[BlockSize + data.Length];
			var opad = new byte[BlockSize + HashSize];

			for ( int i = 0; i < BlockSize; i++ )
			{
				ipad[i] = (byte)( k[i] ^ 0x36 );
				opad[i] = (byte)( k[i] ^ 0x5C );
			}

			Array.Copy( data, 0, ipad, BlockSize, data.Length );
			var inner = Sha256( ipad );
			Array.Copy( inner, 0, opad, BlockSize, HashSize );
			return Sha256( opad );
		}

		// ── Pure-C# SHA-256 (FIPS 180-4) ─────────────────────────────────

		private static readonly uint[] K =
		{
			0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
			0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
			0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
			0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
			0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
			0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
			0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
			0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
		};

		private static byte[] Sha256( byte[] data )
		{
			ulong bitLen = (ulong)data.Length * 8;
			int padLen   = ( data.Length % 64 < 56 ) ? ( 56 - data.Length % 64 ) : ( 120 - data.Length % 64 );
			var msg      = new byte[data.Length + padLen + 8];
			Array.Copy( data, msg, data.Length );
			msg[data.Length] = 0x80;
			for ( int i = 0; i < 8; i++ )
				msg[msg.Length - 1 - i] = (byte)( bitLen >> ( i * 8 ) );

			uint h0 = 0x6a09e667, h1 = 0xbb67ae85, h2 = 0x3c6ef372, h3 = 0xa54ff53a;
			uint h4 = 0x510e527f, h5 = 0x9b05688c, h6 = 0x1f83d9ab, h7 = 0x5be0cd19;
			var w = new uint[64];

			for ( int i = 0; i < msg.Length; i += 64 )
			{
				for ( int j = 0; j < 16; j++ )
					w[j] = ( (uint)msg[i + j * 4] << 24 ) | ( (uint)msg[i + j * 4 + 1] << 16 )
					     | ( (uint)msg[i + j * 4 + 2] << 8 ) | msg[i + j * 4 + 3];

				for ( int j = 16; j < 64; j++ )
				{
					var s0 = Ror( w[j - 15], 7  ) ^ Ror( w[j - 15], 18 ) ^ ( w[j - 15] >> 3  );
					var s1 = Ror( w[j - 2],  17 ) ^ Ror( w[j - 2],  19 ) ^ ( w[j - 2]  >> 10 );
					w[j] = w[j - 16] + s0 + w[j - 7] + s1;
				}

				uint a = h0, b = h1, c = h2, d = h3, e = h4, f = h5, g = h6, h = h7;
				for ( int j = 0; j < 64; j++ )
				{
					var S1  = Ror( e, 6  ) ^ Ror( e, 11 ) ^ Ror( e, 25 );
					var ch  = ( e & f ) ^ ( ~e & g );
					var t1  = h + S1 + ch + K[j] + w[j];
					var S0  = Ror( a, 2  ) ^ Ror( a, 13 ) ^ Ror( a, 22 );
					var maj = ( a & b ) ^ ( a & c ) ^ ( b & c );
					var t2  = S0 + maj;
					h = g; g = f; f = e; e = d + t1; d = c; c = b; b = a; a = t1 + t2;
				}
				h0 += a; h1 += b; h2 += c; h3 += d; h4 += e; h5 += f; h6 += g; h7 += h;
			}

			var result = new byte[32];
			void Pack( uint val, int idx )
			{
				result[idx]     = (byte)( val >> 24 );
				result[idx + 1] = (byte)( val >> 16 );
				result[idx + 2] = (byte)( val >> 8  );
				result[idx + 3] = (byte)  val;
			}
			Pack( h0, 0 ); Pack( h1, 4 ); Pack( h2, 8  ); Pack( h3, 12 );
			Pack( h4, 16 ); Pack( h5, 20 ); Pack( h6, 24 ); Pack( h7, 28 );
			return result;
		}

		private static uint Ror( uint x, int n ) => ( x >> n ) | ( x << ( 32 - n ) );

		private static string BytesToHex( byte[] b )
		{
			var sb = new StringBuilder( b.Length * 2 );
			foreach ( var v in b ) sb.Append( v.ToString( "x2" ) );
			return sb.ToString();
		}

		/// <summary>
		/// Compares two strings in constant time to prevent timing-based side-channel attacks.
		/// The comparison takes the same time regardless of where strings first differ.
		/// </summary>
		private static bool ConstantTimeEquals( string a, string b )
		{
			if ( a.Length != b.Length ) return false;
			int diff = 0;
			for ( int i = 0; i < a.Length; i++ ) diff |= a[i] ^ b[i];
			return diff == 0;
		}
	}
}
