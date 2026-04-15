// File: Code/Tests/TrceAuthServiceTests.cs
// Encoding: UTF-8 (No BOM)
// Run: trce_test_auth
//
// Test scope: HmacSigner Sign/Verify round-trip, wrong-key rejection,
//             empty-input guard, and determinism (constant-time equality).

using Sandbox;
using Trce.Kernel.Security;

namespace Trce.Tests
{
	/// <summary>
	/// Unit tests for HmacSigner and TrceAuthService behaviour.
	/// Run by typing <c>trce_test_auth</c> in the game console.
	/// </summary>
	public static class TrceAuthServiceTests
	{
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

		[Sandbox.ConCmd( "trce_test_auth" )]
		public static void RunAll()
		{
			_passed = 0;
			_failed = 0;

			Log.Info( "═══════════════════════════════════════════════════════" );
			Log.Info( "  TrceAuthServiceTests" );
			Log.Info( "═══════════════════════════════════════════════════════" );

			Test_HmacSigner_SignAndVerify();
			Test_HmacSigner_WrongKeyFails();
			Test_HmacSigner_WrongDataFails();
			Test_HmacSigner_EmptyDataReturnsEmpty();
			Test_HmacSigner_EmptySecretReturnsEmpty();
			Test_HmacSigner_Deterministic();
			Test_HmacSigner_SignFields_RoundTrip();
			Test_HmacSigner_SignFields_WrongOrder_Fails();
			Test_HmacSigner_GenerateRoundSecret_Is64Chars();
			Test_HmacSigner_GenerateRoundSecret_IsUnique();

			Log.Info( $"─── Result: {_passed} passed, {_failed} failed ─────────────────" );
		}

		// T1: Sign + Verify round-trip with the same key should succeed.
		private static void Test_HmacSigner_SignAndVerify()
		{
			var sig = HmacSigner.Sign( "hello", "mysecret" );
			var ok  = HmacSigner.Verify( "hello", sig, "mysecret" );
			Assert( ok, "T1 HmacSigner: Sign + Verify round-trip" );
		}

		// T2: Verification must fail when the wrong secret is used.
		private static void Test_HmacSigner_WrongKeyFails()
		{
			var sig = HmacSigner.Sign( "hello", "correctkey" );
			var ok  = HmacSigner.Verify( "hello", sig, "wrongkey" );
			Assert( !ok, "T2 HmacSigner: Wrong key must fail verification" );
		}

		// T3: Verification must fail when the data has been tampered with.
		private static void Test_HmacSigner_WrongDataFails()
		{
			var sig = HmacSigner.Sign( "original", "key" );
			var ok  = HmacSigner.Verify( "tampered", sig, "key" );
			Assert( !ok, "T3 HmacSigner: Tampered data must fail verification" );
		}

		// T4: Empty data should return an empty string, not throw.
		private static void Test_HmacSigner_EmptyDataReturnsEmpty()
		{
			var sig = HmacSigner.Sign( "", "key" );
			Assert( sig == string.Empty, "T4 HmacSigner: Empty data returns empty string" );
		}

		// T5: Empty secret should return an empty string, not throw.
		private static void Test_HmacSigner_EmptySecretReturnsEmpty()
		{
			var sig = HmacSigner.Sign( "data", "" );
			Assert( sig == string.Empty, "T5 HmacSigner: Empty secret returns empty string" );
		}

		// T6: Same input must always produce the same signature (deterministic / no random).
		private static void Test_HmacSigner_Deterministic()
		{
			var a = HmacSigner.Sign( "data", "key" );
			var b = HmacSigner.Sign( "data", "key" );
			Assert( a == b, "T6 HmacSigner: Identical inputs produce identical signatures", $"a={a} b={b}" );
		}

		// T7: SignFields / VerifyFields round-trip.
		private static void Test_HmacSigner_SignFields_RoundTrip()
		{
			var sig = HmacSigner.SignFields( "secret", "alice", "42", "admin" );
			var ok  = HmacSigner.VerifyFields( sig, "secret", "alice", "42", "admin" );
			Assert( ok, "T7 HmacSigner: SignFields + VerifyFields round-trip" );
		}

		// T8: VerifyFields must fail when field order differs.
		private static void Test_HmacSigner_SignFields_WrongOrder_Fails()
		{
			var sig = HmacSigner.SignFields( "secret", "alice", "42" );
			var ok  = HmacSigner.VerifyFields( sig, "secret", "42", "alice" ); // swapped
			Assert( !ok, "T8 HmacSigner: SignFields wrong field order must fail" );
		}

		// T9: GenerateRoundSecret must produce a 64-character hex string.
		private static void Test_HmacSigner_GenerateRoundSecret_Is64Chars()
		{
			var secret = HmacSigner.GenerateRoundSecret();
			Assert( secret.Length == 64, "T9 HmacSigner: GenerateRoundSecret is 64 chars", $"len={secret.Length}" );
		}

		// T10: GenerateRoundSecret must produce unique values on each call.
		private static void Test_HmacSigner_GenerateRoundSecret_IsUnique()
		{
			var a = HmacSigner.GenerateRoundSecret();
			var b = HmacSigner.GenerateRoundSecret();
			Assert( a != b, "T10 HmacSigner: GenerateRoundSecret produces unique secrets" );
		}
	}
}
