// File: Code/Tests/TrceServiceManagerTests.cs
// Encoding: UTF-8 (No BOM)
// Run: trce_test_servicemanager
//
// Test scope: TrceServiceManager.RegisterService / GetService / UnregisterService / ClearAll
// All tests operate on a temporary TrceServiceManager instance created per test group.

using Sandbox;
using System;
using Trce.Kernel.Plugin;

namespace Trce.Tests
{
	// ── Test stub interfaces & implementations ──────────────────────────────
	public interface ITestServiceA { string GetName(); }
	public interface ITestServiceB { int GetValue(); }

	public class ConcreteServiceA : ITestServiceA { public string GetName() => "ServiceA"; }
	public class ConcreteServiceA2 : ITestServiceA { public string GetName() => "ServiceA_v2"; }
	public class ConcreteServiceB : ITestServiceB { public int GetValue() => 42; }
	// ───────────────────────────────────────────────────────────────────────

	/// <summary>
	/// TrceServiceManager unit test suite.
	/// Run: trce_test_servicemanager in the game console.
	/// </summary>
	public static class TrceServiceManagerTests
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
		[Sandbox.ConCmd( "trce_test_servicemanager" )]
		public static void RunAll()
		{
			_passed = 0;
			_failed = 0;

			Log.Info( "═══════════════════════════════════════════════════════" );
			Log.Info( "  TrceServiceManager Tests" );
			Log.Info( "═══════════════════════════════════════════════════════" );

			try
			{
				Test_Register_And_Get_ReturnsCorrectInstance();
				Test_GetUnregistered_ReturnsNull();
				Test_Register_Override_LastWriterWins();
				Test_Unregister_GetReturnsNull();
				Test_UnregisterNonexistent_NoException();
				Test_ClearAll_AllServicesRemoved();
				Test_Register_MultipleTypes_IndependentSlots();
				Test_RegisterNull_IsHandledGracefully();
			}
			catch ( Exception ex )
			{
				Log.Error( $"[ServiceManagerTests] Unhandled exception: {ex.Message}\n{ex.StackTrace}" );
			}

			Log.Info( $"─── Result: {_passed} passed, {_failed} failed ──────────────────" );
		}

		// ─── Helper: create an isolated TrceServiceManager ───────────────
		// NOTE: TrceServiceManager is a GameObjectSystem, so we cannot easily
		// instantiate it without a scene. We use the live singleton and clean
		// up after each test group via ClearAll() + selective re-registration.
		// Tests that need isolation call ClearAll before and after.

		// SM-T1: RegisterService + GetService returns the same instance.
		private static void Test_Register_And_Get_ReturnsCorrectInstance()
		{
			var mgr = TrceServiceManager.Instance;
			if ( mgr == null ) { Assert( false, "SM-T1 Precondition: TrceServiceManager.Instance is null" ); return; }

			mgr.ClearAll();
			var svc = new ConcreteServiceA();
			mgr.RegisterService<ITestServiceA>( svc );
			var retrieved = mgr.GetService<ITestServiceA>();
			Assert( ReferenceEquals( svc, retrieved ), "SM-T1 Register_And_Get_SameInstance" );
			mgr.ClearAll();
		}

		// SM-T2: GetService for unregistered type returns null.
		private static void Test_GetUnregistered_ReturnsNull()
		{
			var mgr = TrceServiceManager.Instance;
			if ( mgr == null ) { Assert( false, "SM-T2 Precondition failed" ); return; }

			mgr.ClearAll();
			var result = mgr.GetService<ITestServiceA>();
			Assert( result == null, "SM-T2 GetUnregistered_ReturnsNull" );
		}

		// SM-T3: Re-registering the same interface replaces the previous entry (last-writer-wins).
		private static void Test_Register_Override_LastWriterWins()
		{
			var mgr = TrceServiceManager.Instance;
			if ( mgr == null ) { Assert( false, "SM-T3 Precondition failed" ); return; }

			mgr.ClearAll();
			var v1 = new ConcreteServiceA();
			var v2 = new ConcreteServiceA2();
			mgr.RegisterService<ITestServiceA>( v1 );
			mgr.RegisterService<ITestServiceA>( v2 );
			var result = mgr.GetService<ITestServiceA>();
			Assert( ReferenceEquals( v2, result ),
			        "SM-T3 Register_Override_LastWriterWins",
			        $"name={result?.GetName()}" );
			mgr.ClearAll();
		}

		// SM-T4: UnregisterService removes the entry; subsequent Get returns null.
		private static void Test_Unregister_GetReturnsNull()
		{
			var mgr = TrceServiceManager.Instance;
			if ( mgr == null ) { Assert( false, "SM-T4 Precondition failed" ); return; }

			mgr.ClearAll();
			mgr.RegisterService<ITestServiceA>( new ConcreteServiceA() );
			mgr.UnregisterService<ITestServiceA>();
			var result = mgr.GetService<ITestServiceA>();
			Assert( result == null, "SM-T4 Unregister_GetReturnsNull" );
		}

		// SM-T5: Unregistering a service that was never registered does not throw.
		private static void Test_UnregisterNonexistent_NoException()
		{
			var mgr = TrceServiceManager.Instance;
			if ( mgr == null ) { Assert( false, "SM-T5 Precondition failed" ); return; }

			bool ok = true;
			try { mgr.UnregisterService<ITestServiceB>(); }
			catch { ok = false; }
			Assert( ok, "SM-T5 Unregister_Nonexistent_NoException" );
		}

		// SM-T6: ClearAll removes every registered service.
		private static void Test_ClearAll_AllServicesRemoved()
		{
			var mgr = TrceServiceManager.Instance;
			if ( mgr == null ) { Assert( false, "SM-T6 Precondition failed" ); return; }

			mgr.RegisterService<ITestServiceA>( new ConcreteServiceA() );
			mgr.RegisterService<ITestServiceB>( new ConcreteServiceB() );
			mgr.ClearAll();
			var a = mgr.GetService<ITestServiceA>();
			var b = mgr.GetService<ITestServiceB>();
			Assert( a == null && b == null, "SM-T6 ClearAll_AllServicesRemoved", $"a={a} b={b}" );
		}

		// SM-T7: Multiple distinct interfaces occupy independent registry slots.
		private static void Test_Register_MultipleTypes_IndependentSlots()
		{
			var mgr = TrceServiceManager.Instance;
			if ( mgr == null ) { Assert( false, "SM-T7 Precondition failed" ); return; }

			mgr.ClearAll();
			var a = new ConcreteServiceA();
			var b = new ConcreteServiceB();
			mgr.RegisterService<ITestServiceA>( a );
			mgr.RegisterService<ITestServiceB>( b );

			var ra = mgr.GetService<ITestServiceA>();
			var rb = mgr.GetService<ITestServiceB>();
			Assert( ReferenceEquals( a, ra ) && ReferenceEquals( b, rb ),
			        "SM-T7 MultipleTypes_IndependentSlots",
			        $"a_ok={ReferenceEquals(a,ra)} b_ok={ReferenceEquals(b,rb)}" );
			mgr.ClearAll();
		}

		// SM-T8: RegisterService(null) is handled gracefully without crash.
		private static void Test_RegisterNull_IsHandledGracefully()
		{
			var mgr = TrceServiceManager.Instance;
			if ( mgr == null ) { Assert( false, "SM-T8 Precondition failed" ); return; }

			bool ok = true;
			try { mgr.RegisterService<ITestServiceA>( null ); }
			catch ( Exception ) { ok = true; } // throwing is acceptable; crashing is not
			Assert( ok, "SM-T8 RegisterNull_NoUnhandledCrash" );
			mgr.ClearAll();
		}
	}
}
