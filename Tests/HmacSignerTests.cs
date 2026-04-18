// File: Code/Tests/HmacSignerTests.cs
// Encoding: UTF-8 (No BOM)
// Run: trce_test_hmac
//
// Test scope: HmacSigner.Sign / Verify / VerifyFields / constant-time comparison
// Uses known test vectors derived from RFC 2202 / NIST to validate the pure-C# implementation.

using Sandbox;
using System;
using Trce.Kernel.Security;

namespace Trce.Tests
{
	/// <summary>
	/// HmacSigner unit test suite.
	/// Run: trce_test_hmac in the game console.
	/// </summary>
	public static class HmacSignerTests
	{
		// ─── Assert helpers ──────────────────────────────────────────────
		private static int _passed;
		private static int _failed;

		private static void Assert( bool condition, string name, string detail = "" )
		{
			if ( condition )
			{
				_passed++;
				Log.Info( $"  ✅ PASS  {name}" );
			}
			else
			{
				_failed++;
				Log.Error( $"  ❌ FAIL  {name}" + ( detail.Length > 0 ? $"  ← {detail}" : "" ) );
			}
		}

		// ─── Entry point ─────────────────────────────────────────────────
		[Sandbox.ConCmd( "trce_test_hmac" )]
		public static void RunAll()
		{
			_passed = 0;
			_failed = 0;

			Log.Info( "═══════════════════════════════════════════════════════" );
			Log.Info( "  HmacSigner Tests" );
			Log.Info( "═══════════════════════════════════════════════════════" );

			try
			{
				Test_KnownVector_RFC2202_TC1();
				Test_KnownVector_SimpleString();
				Test_Verify_ValidSignature_ReturnsTrue();
				Test_Verify_TamperedData_ReturnsFalse();
				Test_Verify_TamperedSignature_ReturnsFalse();
				Test_Verify_WrongKey_ReturnsFalse();
				Test_Sign_EmptyInputs_ReturnsEmpty();
				Test_Verify_EmptySignature_ReturnsFalse();
				Test_SignFields_SameAsManualJoin();
				Test_VerifyFields_Valid_ReturnsTrue();
				Test_VerifyFields_Tampered_ReturnsFalse();
				Test_ConstantTimeComparison_DifferentLengthsFail();
				Test_Sign_Result_Is64CharHex();
				Test_Sign_Deterministic_SameInputSameOutput();
				Test_GenerateRoundSecret_Returns64Chars();
			}
			catch ( Exception ex )
			{
				Log.Error( $"[HmacSignerTests] Unhandled exception: {ex.Message}\n{ex.StackTrace}" );
			}

			Log.Info( $"─── Result: {_passed} passed, {_failed} failed ──────────────────" );
		}

		// ─── Tests ────────────────────────────────────────────────────────

		// HM-T1: Known vector — HMAC-SHA256("key", "The quick brown fox jumps over the lazy dog")
		// Expected: f7bc83f430538424b13298e6aa6fb143ef4d59a14946175997479dbc2d1a3cd8
		// Reference: https://en.wikipedia.org/wiki/HMAC (example values)
		private static void Test_KnownVector_RFC2202_TC1()
		{
			const string key      = "key";
			const string data     = "The quick brown fox jumps over the lazy dog";
			const string expected = "f7bc83f430538424b13298e6aa6fb143ef4d59a14946175997479dbc2d1a3cd8";

			var result = HmacSigner.Sign( data, key );
			Assert( result == expected,
			        "HM-T1 KnownVector_QuickBrownFox",
			        $"expected={expected}\n  got    ={result}" );
		}

		// HM-T2: Simple deterministic sign with known key and data.
		private static void Test_KnownVector_SimpleString()
		{
			var result = HmacSigner.Sign( "hello", "world" );
			// Verify with the same key — must return true.
			var verify = HmacSigner.Verify( "hello", result, "world" );
			Assert( verify && result.Length == 64, "HM-T2 KnownVector_SimpleString_Roundtrip", $"sig={result}" );
		}

		// HM-T3: Verify returns true for a valid (data, signature, key) triple.
		private static void Test_Verify_ValidSignature_ReturnsTrue()
		{
			const string key  = "server-secret-key";
			const string data = "steamid:76561198000000000|round:42";
			var sig = HmacSigner.Sign( data, key );
			Assert( HmacSigner.Verify( data, sig, key ),
			        "HM-T3 Verify_ValidSignature_ReturnsTrue" );
		}

		// HM-T4: Tampering with the data invalidates the signature.
		private static void Test_Verify_TamperedData_ReturnsFalse()
		{
			const string key  = "server-secret-key";
			const string data = "steamid:76561198000000000|round:42";
			var sig = HmacSigner.Sign( data, key );
			Assert( !HmacSigner.Verify( data + "X", sig, key ),
			        "HM-T4 Verify_TamperedData_ReturnsFalse" );
		}

		// HM-T5: Tampering with the signature is detected.
		private static void Test_Verify_TamperedSignature_ReturnsFalse()
		{
			const string key  = "server-secret-key";
			const string data = "steamid:76561198000000000|round:42";
			var sig = HmacSigner.Sign( data, key );
			// Flip the first character.
			var tampered = ( sig[0] == 'a' ? 'b' : 'a' ) + sig.Substring( 1 );
			Assert( !HmacSigner.Verify( data, tampered, key ),
			        "HM-T5 Verify_TamperedSignature_ReturnsFalse" );
		}

		// HM-T6: Using a different key produces a different (invalid) signature.
		private static void Test_Verify_WrongKey_ReturnsFalse()
		{
			var sig = HmacSigner.Sign( "data", "correct-key" );
			Assert( !HmacSigner.Verify( "data", sig, "wrong-key" ),
			        "HM-T6 Verify_WrongKey_ReturnsFalse" );
		}

		// HM-T7: Signing with empty data or empty key returns empty string.
		private static void Test_Sign_EmptyInputs_ReturnsEmpty()
		{
			Assert( HmacSigner.Sign( "", "key" ) == string.Empty,
			        "HM-T7a Sign_EmptyData_ReturnsEmpty" );
			Assert( HmacSigner.Sign( "data", "" ) == string.Empty,
			        "HM-T7b Sign_EmptyKey_ReturnsEmpty" );
		}

		// HM-T8: Verify with an empty signature returns false without exception.
		private static void Test_Verify_EmptySignature_ReturnsFalse()
		{
			bool result = false;
			bool ok = true;
			try { result = HmacSigner.Verify( "data", "", "key" ); }
			catch { ok = false; }
			Assert( ok && !result, "HM-T8 Verify_EmptySignature_ReturnsFalse" );
		}

		// HM-T9: SignFields produces the same output as signing the manually pipe-joined string.
		private static void Test_SignFields_SameAsManualJoin()
		{
			const string secret = "round-secret";
			var manual = HmacSigner.Sign( "steam:123|round:5|action:hit", secret );
			var fields = HmacSigner.SignFields( secret, "steam:123", "round:5", "action:hit" );
			Assert( manual == fields, "HM-T9 SignFields_SameAsManualJoin", $"manual={manual}\nfields={fields}" );
		}

		// HM-T10: VerifyFields returns true for a valid signature over the joined fields.
		private static void Test_VerifyFields_Valid_ReturnsTrue()
		{
			const string secret = "round-secret";
			var sig = HmacSigner.SignFields( secret, "a", "b", "c" );
			Assert( HmacSigner.VerifyFields( sig, secret, "a", "b", "c" ),
			        "HM-T10 VerifyFields_Valid_ReturnsTrue" );
		}

		// HM-T11: VerifyFields returns false if any field is changed.
		private static void Test_VerifyFields_Tampered_ReturnsFalse()
		{
			const string secret = "round-secret";
			var sig = HmacSigner.SignFields( secret, "a", "b", "c" );
			Assert( !HmacSigner.VerifyFields( sig, secret, "a", "b", "TAMPERED" ),
			        "HM-T11 VerifyFields_Tampered_ReturnsFalse" );
		}

		// HM-T12: Constant-time comparison rejects strings of different lengths without short-circuit.
		// (We verify this indirectly: different-length hex strings must fail verify.)
		private static void Test_ConstantTimeComparison_DifferentLengthsFail()
		{
			var sig  = HmacSigner.Sign( "data", "key" );
			var short1 = sig.Substring( 0, 32 ); // Half-length hex
			Assert( !HmacSigner.Verify( "data", short1, "key" ),
			        "HM-T12 ConstantTime_DifferentLength_DoesNotVerify" );
		}

		// HM-T13: Sign output is always exactly 64 lowercase hex characters.
		private static void Test_Sign_Result_Is64CharHex()
		{
			var sig = HmacSigner.Sign( "arbitrary payload", "arbitrary key" );
			bool is64Hex = sig.Length == 64;
			foreach ( char c in sig ) if ( !Uri.IsHexDigit( c ) ) { is64Hex = false; break; }
			Assert( is64Hex, "HM-T13 Sign_Result_Is64CharHex", $"len={sig.Length} sig={sig}" );
		}

		// HM-T14: Sign is deterministic — same inputs always produce identical output.
		private static void Test_Sign_Deterministic_SameInputSameOutput()
		{
			var s1 = HmacSigner.Sign( "payload", "secret" );
			var s2 = HmacSigner.Sign( "payload", "secret" );
			Assert( s1 == s2, "HM-T14 Sign_Deterministic", $"s1={s1}\ns2={s2}" );
		}

		// HM-T15: GenerateRoundSecret returns a 64-character string each time.
		private static void Test_GenerateRoundSecret_Returns64Chars()
		{
			var secret1 = HmacSigner.GenerateRoundSecret();
			var secret2 = HmacSigner.GenerateRoundSecret();
			Assert( secret1.Length == 64 && secret2.Length == 64 && secret1 != secret2,
			        "HM-T15 GenerateRoundSecret_Returns64Chars_AndUnique",
			        $"len1={secret1.Length} len2={secret2.Length} equal={secret1 == secret2}" );
		}
	}
}
