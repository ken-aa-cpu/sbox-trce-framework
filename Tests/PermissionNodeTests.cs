// File: Code/Tests/PermissionNodeTests.cs
// Encoding: UTF-8 (No BOM)
// Run: trce_test_permissions
//
// Test scope:
//   - PermissionNode.HasNode  : wildcard expansion, direct nodes, group nodes
//   - PermissionNode.MatchesPermission (via HasNode): prefix wildcard, exact match, no false positives
//   - TrceAuthPlugin.HasPermission    : group inheritance (recursive), cycle detection
//   - TrceAuthPlugin.CheckWildcard    : supreme *, prefix "admin.*", exact, case-insensitivity

using Sandbox;
using System;
using System.Collections.Generic;
using Trce.Kernel.Auth;

namespace Trce.Tests
{
	/// <summary>
	/// Unit tests for permission wildcard expansion and group inheritance logic.
	/// Run by typing <c>trce_test_permissions</c> in the game console.
	/// </summary>
	public static class PermissionNodeTests
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

		[Sandbox.ConCmd( "trce_test_permissions" )]
		public static void RunAll()
		{
			_passed = 0;
			_failed = 0;

			Log.Info( "═══════════════════════════════════════════════════════" );
			Log.Info( "  PermissionNodeTests" );
			Log.Info( "═══════════════════════════════════════════════════════" );

			// ── PermissionNode.HasNode ───────────────────────────────────
			Test_HasNode_DirectNode_Exact();
			Test_HasNode_DirectNode_Star_GrantsAll();
			Test_HasNode_DirectNode_PrefixWildcard();
			Test_HasNode_DirectNode_PrefixWildcard_NoFalsePositive();
			Test_HasNode_GroupNode_Exact();
			Test_HasNode_GroupNode_Star_GrantsAll();
			Test_HasNode_GroupNode_PrefixWildcard();
			Test_HasNode_NoUser_ReturnsFalse();
			Test_HasNode_EmptyNode_ReturnsFalse();

			// ── TrceAuthPlugin.HasPermission (group inheritance) ─────────
			Test_AuthPlugin_DirectPermission();
			Test_AuthPlugin_GroupPermission();
			Test_AuthPlugin_GroupInheritance_SingleLevel();
			Test_AuthPlugin_GroupInheritance_MultiLevel();
			Test_AuthPlugin_CyclicInheritance_NoDeadlock();
			Test_AuthPlugin_Wildcard_Supreme();
			Test_AuthPlugin_Wildcard_Prefix();
			Test_AuthPlugin_Wildcard_NoFalsePositive();
			Test_AuthPlugin_CaseInsensitive();
			Test_AuthPlugin_ExpiredPermission_Denied();
			Test_AuthPlugin_NonExpiredPermission_Granted();

			Log.Info( $"─── Result: {_passed} passed, {_failed} failed ─────────────────" );
		}

		// ─── Helpers ─────────────────────────────────────────────────────

		private static TrcePermissionUser MakeUser( params string[] directNodes )
		{
			return new TrcePermissionUser
			{
				SteamId = 0,
				Groups = new List<string>(),
				Nodes  = new List<string>( directNodes )
			};
		}

		private static TrcePermissionGroup MakeGroup( string name, params string[] nodes )
		{
			return new TrcePermissionGroup
			{
				Name  = name,
				Nodes = new List<string>( nodes )
			};
		}

		// ─── PermissionNode.HasNode tests ─────────────────────────────────

		// T1: User has exact node → granted.
		private static void Test_HasNode_DirectNode_Exact()
		{
			var user = MakeUser( "trce.chat.send" );
			Assert( PermissionNode.HasNode( user, null, "trce.chat.send" ),
				"T1 HasNode: direct exact node granted" );
		}

		// T2: User has "*" → any node granted.
		private static void Test_HasNode_DirectNode_Star_GrantsAll()
		{
			var user = MakeUser( "*" );
			Assert( PermissionNode.HasNode( user, null, "anything.at.all" ),
				"T2 HasNode: direct '*' grants any node" );
		}

		// T3: User has "trce.chat.*" → "trce.chat.send" is granted.
		private static void Test_HasNode_DirectNode_PrefixWildcard()
		{
			var user = MakeUser( "trce.chat.*" );
			Assert( PermissionNode.HasNode( user, null, "trce.chat.send" ),
				"T3 HasNode: prefix wildcard 'trce.chat.*' grants 'trce.chat.send'" );
		}

		// T4: "trce.chat.*" must NOT grant "trce.admin.kick" (no false positive).
		private static void Test_HasNode_DirectNode_PrefixWildcard_NoFalsePositive()
		{
			var user = MakeUser( "trce.chat.*" );
			Assert( !PermissionNode.HasNode( user, null, "trce.admin.kick" ),
				"T4 HasNode: prefix wildcard 'trce.chat.*' does not grant 'trce.admin.kick'" );
		}

		// T5: Node comes from a group, not from the user directly.
		private static void Test_HasNode_GroupNode_Exact()
		{
			var user  = MakeUser();
			var group = MakeGroup( "mod", "trce.kick" );
			Assert( PermissionNode.HasNode( user, new List<TrcePermissionGroup> { group }, "trce.kick" ),
				"T5 HasNode: group exact node granted" );
		}

		// T6: Group has "*" → any node granted to the user.
		private static void Test_HasNode_GroupNode_Star_GrantsAll()
		{
			var user  = MakeUser();
			var group = MakeGroup( "admin", "*" );
			Assert( PermissionNode.HasNode( user, new List<TrcePermissionGroup> { group }, "anything" ),
				"T6 HasNode: group '*' grants any node" );
		}

		// T7: Group has prefix wildcard.
		private static void Test_HasNode_GroupNode_PrefixWildcard()
		{
			var user  = MakeUser();
			var group = MakeGroup( "staff", "trce.admin.*" );
			Assert( PermissionNode.HasNode( user, new List<TrcePermissionGroup> { group }, "trce.admin.ban" ),
				"T7 HasNode: group prefix wildcard 'trce.admin.*' grants 'trce.admin.ban'" );
		}

		// T8: null user → false.
		private static void Test_HasNode_NoUser_ReturnsFalse()
		{
			Assert( !PermissionNode.HasNode( null, null, "trce.kick" ),
				"T8 HasNode: null user returns false" );
		}

		// T9: Empty node string → false.
		private static void Test_HasNode_EmptyNode_ReturnsFalse()
		{
			var user = MakeUser( "*" );
			Assert( !PermissionNode.HasNode( user, null, "" ),
				"T9 HasNode: empty node string returns false" );
		}

		// ─── TrceAuthPlugin.HasPermission tests ─────────────────────────

		private static TrceAuthPlugin MakePlugin() => new TrceAuthPlugin();

		// T10: User has direct permission.
		private static void Test_AuthPlugin_DirectPermission()
		{
			var plugin = MakePlugin();
			plugin.GrantPermission( 1ul, "trce.test" );
			Assert( plugin.HasPermission( 1ul, "trce.test" ),
				"T10 AuthPlugin: direct permission granted" );
		}

		// T11: Permission comes from group, not directly from user.
		private static void Test_AuthPlugin_GroupPermission()
		{
			var plugin = MakePlugin();
			plugin.AddGroupPermission( "mod", "trce.kick" );
			plugin.AddUserToGroup( 2ul, "mod" );
			Assert( plugin.HasPermission( 2ul, "trce.kick" ),
				"T11 AuthPlugin: group permission granted" );
		}

		// T12: Group inherits from parent group (one level).
		private static void Test_AuthPlugin_GroupInheritance_SingleLevel()
		{
			var plugin = MakePlugin();
			plugin.AddGroupPermission( "vip", "trce.vip.lounge" );
			plugin.SetGroupInheritance( "staff", "vip" ); // staff inherits vip
			plugin.AddGroupPermission( "staff", "trce.kick" );
			plugin.AddUserToGroup( 3ul, "staff" );

			Assert( plugin.HasPermission( 3ul, "trce.kick" ),
				"T12a AuthPlugin: direct group permission on staff" );
			Assert( plugin.HasPermission( 3ul, "trce.vip.lounge" ),
				"T12b AuthPlugin: single-level inherited permission from vip" );
		}

		// T13: Multi-level inheritance chain: admin → staff → vip.
		private static void Test_AuthPlugin_GroupInheritance_MultiLevel()
		{
			var plugin = MakePlugin();
			plugin.AddGroupPermission( "vip",   "trce.vip.lounge" );
			plugin.AddGroupPermission( "staff",  "trce.kick" );
			plugin.AddGroupPermission( "admin",  "trce.ban" );
			plugin.SetGroupInheritance( "staff", "vip" );
			plugin.SetGroupInheritance( "admin", "staff" );
			plugin.AddUserToGroup( 4ul, "admin" );

			Assert( plugin.HasPermission( 4ul, "trce.ban" ),       "T13a Multi-level: admin.ban" );
			Assert( plugin.HasPermission( 4ul, "trce.kick" ),      "T13b Multi-level: staff.kick via admin" );
			Assert( plugin.HasPermission( 4ul, "trce.vip.lounge" ),"T13c Multi-level: vip.lounge via admin→staff" );
		}

		// T14: Cyclic group inheritance must not deadlock or throw.
		private static void Test_AuthPlugin_CyclicInheritance_NoDeadlock()
		{
			var plugin = MakePlugin();
			plugin.SetGroupInheritance( "a", "b" );
			plugin.SetGroupInheritance( "b", "a" ); // cycle
			plugin.AddGroupPermission( "a", "node.x" );
			plugin.AddUserToGroup( 5ul, "a" );

			bool threw = false;
			try { plugin.HasPermission( 5ul, "node.x" ); }
			catch ( Exception ) { threw = true; }

			Assert( !threw, "T14 AuthPlugin: cyclic group inheritance does not deadlock or throw" );
		}

		// T15: Supreme wildcard "*" grants everything.
		private static void Test_AuthPlugin_Wildcard_Supreme()
		{
			var plugin = MakePlugin();
			plugin.GrantPermission( 6ul, "*" );
			Assert( plugin.HasPermission( 6ul, "anything.at.all" ),
				"T15 AuthPlugin: '*' grants any permission" );
		}

		// T16: Prefix wildcard "admin.*" grants "admin.kick" but specific only.
		private static void Test_AuthPlugin_Wildcard_Prefix()
		{
			var plugin = MakePlugin();
			plugin.GrantPermission( 7ul, "admin.*" );
			Assert( plugin.HasPermission( 7ul, "admin.kick" ),
				"T16 AuthPlugin: prefix wildcard 'admin.*' grants 'admin.kick'" );
		}

		// T17: "admin.*" must NOT grant "chat.say" (no false positive).
		private static void Test_AuthPlugin_Wildcard_NoFalsePositive()
		{
			var plugin = MakePlugin();
			plugin.GrantPermission( 8ul, "admin.*" );
			Assert( !plugin.HasPermission( 8ul, "chat.say" ),
				"T17 AuthPlugin: prefix wildcard 'admin.*' does not grant 'chat.say'" );
		}

		// T18: Permission check is case-insensitive.
		private static void Test_AuthPlugin_CaseInsensitive()
		{
			var plugin = MakePlugin();
			plugin.GrantPermission( 9ul, "trce.Kick" );
			Assert( plugin.HasPermission( 9ul, "trce.kick" ),
				"T18 AuthPlugin: permission check is case-insensitive" );
		}

		// T19: An expired timed permission is denied.
		private static void Test_AuthPlugin_ExpiredPermission_Denied()
		{
			var plugin = MakePlugin();
			// Grant a permission that expired 1 second ago
			plugin.GrantPermission( 10ul, "trce.temp", TimeSpan.FromSeconds( -1 ) );
			Assert( !plugin.HasPermission( 10ul, "trce.temp" ),
				"T19 AuthPlugin: expired timed permission is denied" );
		}

		// T20: A non-expired timed permission is granted.
		private static void Test_AuthPlugin_NonExpiredPermission_Granted()
		{
			var plugin = MakePlugin();
			// Grant a permission expiring in 1 hour
			plugin.GrantPermission( 11ul, "trce.temp", TimeSpan.FromHours( 1 ) );
			Assert( plugin.HasPermission( 11ul, "trce.temp" ),
				"T20 AuthPlugin: non-expired timed permission is granted" );
		}
	}
}
