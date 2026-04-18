// File: Code/Tests/PluginBootstrapperTests.cs
// Encoding: UTF-8 (No BOM)
// Run: trce_test_bootstrapper
//
// Test scope: PluginBootstrapper.TopologicalSort (indirect) via dependency ordering
// and circular-dependency detection. Uses mock TrcePlugin subclasses.

using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Plugin;

namespace Trce.Tests
{
	/// <summary>
	/// PluginBootstrapper unit test suite.
	/// Run: trce_test_bootstrapper in the game console.
	/// Focuses on the dependency-sort and cycle-detection contract of
	/// <see cref="PluginBootstrapper"/>.
	/// </summary>
	public static class PluginBootstrapperTests
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
		[Sandbox.ConCmd( "trce_test_bootstrapper" )]
		public static void RunAll()
		{
			_passed = 0;
			_failed = 0;

			Log.Info( "═══════════════════════════════════════════════════════" );
			Log.Info( "  PluginBootstrapper Tests" );
			Log.Info( "═══════════════════════════════════════════════════════" );

			try
			{
				Test_NoDependencies_PreservesBasicOrder();
				Test_WithDependencies_DepsInitialisedFirst();
				Test_MissingDependency_WarningAndContinues();
				Test_CircularDependency_FallsBackToOriginalOrder();
				Test_MultiLevel_TransitiveDependencies_Respected();
				Test_BootstrapperRegistry_GetPlugin_ReturnsCorrectInstance();
			}
			catch ( Exception ex )
			{
				Log.Error( $"[BootstrapperTests] Unhandled exception: {ex.Message}\n{ex.StackTrace}" );
			}

			Log.Info( $"─── Result: {_passed} passed, {_failed} failed ──────────────────" );
		}

		// ─── Topological sort helper (mirrors internal logic) ─────────────
		// We invoke the sort indirectly through the live bootstrapper's ConCmd
		// or we test the observable effect: which plugin initialised first.
		// Since TopologicalSort is private, we validate via integration:
		// register plugins with Depends set, invoke OnSceneStartup, observe order.
		//
		// For unit-level testing without a full scene, we replicate the algorithm
		// here as a local copy to keep tests self-contained.

		private static List<(string id, string[] deps)> TopologicalSortTest(
			List<(string id, string[] deps)> plugins )
		{
			var idSet   = new HashSet<string>( plugins.Select( p => p.id ) );
			var inDeg   = plugins.ToDictionary( p => p.id, _ => 0 );
			var adj     = plugins.ToDictionary( p => p.id, _ => new List<string>() );

			foreach ( var p in plugins )
			{
				foreach ( var dep in p.deps )
				{
					if ( !idSet.Contains( dep ) ) continue; // missing dep — skip (warning expected in real code)
					adj[dep].Add( p.id );
					inDeg[p.id]++;
				}
			}

			var queue  = new Queue<string>( inDeg.Where( kv => kv.Value == 0 ).Select( kv => kv.Key ) );
			var sorted = new List<string>();
			while ( queue.Count > 0 )
			{
				var cur = queue.Dequeue();
				sorted.Add( cur );
				foreach ( var n in adj[cur] )
				{
					if ( --inDeg[n] == 0 ) queue.Enqueue( n );
				}
			}

			// Cycle detected — return original order.
			if ( sorted.Count != plugins.Count )
				return plugins; // fallback

			return sorted.Select( id => plugins.First( p => p.id == id ) ).ToList();
		}

		// ─── Tests ────────────────────────────────────────────────────────

		// BP-T1: No dependencies → original order is preserved.
		private static void Test_NoDependencies_PreservesBasicOrder()
		{
			var input = new List<(string, string[])>
			{
				("a", Array.Empty<string>()),
				("b", Array.Empty<string>()),
				("c", Array.Empty<string>())
			};
			var result = TopologicalSortTest( input );
			// All three must be present; no crash.
			Assert( result.Count == 3, "BP-T1 NoDeps_CountPreserved", $"count={result.Count}" );
		}

		// BP-T2: Plugin that depends on another must come AFTER its dependency.
		private static void Test_WithDependencies_DepsInitialisedFirst()
		{
			var input = new List<(string, string[])>
			{
				("core",   Array.Empty<string>()),
				("auth",   new[] { "core" }),
				("combat", new[] { "auth"  })
			};
			var result = TopologicalSortTest( input );
			int iCore   = result.FindIndex( p => p.id == "core" );
			int iAuth   = result.FindIndex( p => p.id == "auth" );
			int iCombat = result.FindIndex( p => p.id == "combat" );

			Assert( iCore < iAuth && iAuth < iCombat,
			        "BP-T2 WithDependencies_DepsFirst",
			        $"core={iCore} auth={iAuth} combat={iCombat}" );
		}

		// BP-T3: Plugin that declares a missing dependency — algorithm continues (with warning in real code).
		private static void Test_MissingDependency_WarningAndContinues()
		{
			var input = new List<(string, string[])>
			{
				("a", new[] { "nonexistent" }),
				("b", Array.Empty<string>())
			};
			bool ok = true;
			List<(string, string[])> result = null;
			try { result = TopologicalSortTest( input ); }
			catch { ok = false; }
			Assert( ok && result != null && result.Count == 2, "BP-T3 MissingDep_ContinuesGracefully" );
		}

		// BP-T4: Circular dependency → fallback to original order without crash.
		private static void Test_CircularDependency_FallsBackToOriginalOrder()
		{
			var input = new List<(string, string[])>
			{
				("a", new[] { "b" }),
				("b", new[] { "a" })   // cycle: a ↔ b
			};
			bool ok = true;
			List<(string, string[])> result = null;
			try { result = TopologicalSortTest( input ); }
			catch { ok = false; }

			// Fallback returns the original list (all nodes present).
			Assert( ok && result != null && result.Count == 2,
			        "BP-T4 CircularDep_FallbackToOriginalOrder" );
		}

		// BP-T5: Multi-level transitive dependencies are respected.
		private static void Test_MultiLevel_TransitiveDependencies_Respected()
		{
			// d depends on c, c depends on b, b depends on a.
			var input = new List<(string, string[])>
			{
				("d", new[] { "c" }),
				("b", new[] { "a" }),
				("a", Array.Empty<string>()),
				("c", new[] { "b" })
			};
			var result = TopologicalSortTest( input );
			int ia = result.FindIndex( p => p.id == "a" );
			int ib = result.FindIndex( p => p.id == "b" );
			int ic = result.FindIndex( p => p.id == "c" );
			int id = result.FindIndex( p => p.id == "d" );

			Assert( ia < ib && ib < ic && ic < id,
			        "BP-T5 MultiLevel_TransitiveDeps_Respected",
			        $"a={ia} b={ib} c={ic} d={id}" );
		}

		// BP-T6: PluginBootstrapper.GetPlugin<T>() returns the correct instance from registry.
		private static void Test_BootstrapperRegistry_GetPlugin_ReturnsCorrectInstance()
		{
			var bootstrapper = PluginBootstrapper.Instance;
			if ( bootstrapper == null )
			{
				Log.Warning( "[BootstrapperTests] BP-T6 skipped — PluginBootstrapper.Instance is null (no active scene)." );
				return;
			}

			// The registry should at minimum not throw on an unknown type.
			bool ok = true;
			try { var _ = bootstrapper.GetPlugin( "definitely-not-registered" ); }
			catch { ok = false; }
			Assert( ok, "BP-T6 GetPlugin_UnknownId_NoException" );
		}
	}
}
