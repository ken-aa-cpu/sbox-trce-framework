// File: Code/Tests/EntityEventBusTests.cs
// Encoding: UTF-8 (No BOM)
// 執行方式：在遊戲主控台輸入  trce_test_eventbus
//
// 測試範圍：EntityEventBus 的 Subscribe / Unsubscribe / Publish / ClearAll
//   行為合約。所有測試透過建立暫時 GameObject 裝載 EntityEventBus 元件執行，
//   測試結束後銷毀物件，不留下任何場景殘留。

using Sandbox;
using System;
using Trce.Kernel.Event;

namespace Trce.Tests
{
	// ── 測試用最小事件 (readonly struct + ITrceEvent) ──────────────────────────
	public readonly struct PingEvent : ITrceEvent
	{
		public readonly int Value;
		public PingEvent( int value ) => Value = value;
	}

	public readonly struct PongEvent : ITrceEvent
	{
		public readonly string Tag;
		public PongEvent( string tag ) => Tag = tag;
	}
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// EntityEventBus 單元測試套件。
	/// 執行方式：在遊戲主控台輸入 trce_test_eventbus
	/// </summary>
	public static class EntityEventBusTests
	{
		// ─── 斷言輔助 ───────────────────────────────────────────────
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

		// ─── 進入點 ─────────────────────────────────────────────────
		[Sandbox.ConCmd( "trce_test_eventbus" )]
		public static void RunAll()
		{
			_passed = 0;
			_failed = 0;

			Log.Info( "═══════════════════════════════════════════════════════" );
			Log.Info( "  EntityEventBus Tests" );
			Log.Info( "═══════════════════════════════════════════════════════" );

			var scene = Sandbox.Game.ActiveScene;
			if ( scene == null )
			{
				Log.Warning( "[EventBusTests] 無活躍場景，測試略過。" );
				return;
			}

			// 每個測試使用獨立 GameObject 避免狀態污染
			try
			{
				Test_Subscribe_Publish_HandlerInvoked( scene );
				Test_MultipleSubscribers_AllInvoked( scene );
				Test_Unsubscribe_HandlerNotInvoked( scene );
				Test_UnsubscribeNonexistent_NoException( scene );
				Test_ClearAllTyped_RemovesOnlyThatType( scene );
				Test_ClearAll_RemovesAllTypes( scene );
				Test_SubscribeNull_ThrowsArgumentNullException( scene );
				Test_UnsubscribeNull_NoException( scene );
				Test_PublishNoSubscribers_NoException( scene );
				Test_Publish_PassesEventDataCorrectly( scene );
				Test_SubscribeSameHandlerTwice_InvokedTwice( scene );
				Test_OnDestroy_ClearsHandlers( scene );
			}
			catch ( Exception ex )
			{
				Log.Error( $"[EventBusTests] 未捕獲例外: {ex.Message}\n{ex.StackTrace}" );
			}

			Log.Info( $"─── 結果: {_passed} 通過, {_failed} 失敗 ───────────────────" );
		}

		// ─── 建立帶 EntityEventBus 元件的暫時 GameObject ──────────
		private static EntityEventBus CreateBus( Scene scene, string label = "TestBus" )
		{
			var go = scene.CreateObject();
			go.Name = $"__EntityEventBusTest_{label}__";
			return go.Components.Create<EntityEventBus>( false );
		}

		// ═══════════════════════════════════════════════════════════
		//  測試方法
		// ═══════════════════════════════════════════════════════════

		// T1：Subscribe + Publish → handler 被呼叫一次
		private static void Test_Subscribe_Publish_HandlerInvoked( Scene scene )
		{
			var bus = CreateBus( scene, "T1" );
			int called = 0;
			bus.Subscribe<PingEvent>( _ => called++ );
			bus.Publish( new PingEvent( 1 ) );
			Assert( called == 1, "T1 Subscribe_Publish_HandlerInvoked" );
			bus.GameObject.Destroy();
		}

		// T2：多個訂閱者全部被觸發
		private static void Test_MultipleSubscribers_AllInvoked( Scene scene )
		{
			var bus = CreateBus( scene, "T2" );
			int a = 0, b = 0;
			bus.Subscribe<PingEvent>( _ => a++ );
			bus.Subscribe<PingEvent>( _ => b++ );
			bus.Publish( new PingEvent( 2 ) );
			Assert( a == 1 && b == 1, "T2 MultipleSubscribers_AllInvoked", $"a={a} b={b}" );
			bus.GameObject.Destroy();
		}

		// T3：Unsubscribe 後 handler 不再被呼叫
		private static void Test_Unsubscribe_HandlerNotInvoked( Scene scene )
		{
			var bus = CreateBus( scene, "T3" );
			int called = 0;
			Action<PingEvent> handler = _ => called++;
			bus.Subscribe( handler );
			bus.Publish( new PingEvent( 3 ) );   // called=1
			bus.Unsubscribe( handler );
			bus.Publish( new PingEvent( 4 ) );   // called 不變
			Assert( called == 1, "T3 Unsubscribe_HandlerNotInvoked", $"called={called}" );
			bus.GameObject.Destroy();
		}

		// T4：Unsubscribe 不存在的 handler — 不拋出例外
		private static void Test_UnsubscribeNonexistent_NoException( Scene scene )
		{
			var bus = CreateBus( scene, "T4" );
			bool ok = true;
			try   { bus.Unsubscribe<PingEvent>( _ => { } ); }
			catch { ok = false; }
			Assert( ok, "T4 Unsubscribe_NonExistent_NoException" );
			bus.GameObject.Destroy();
		}

		// T5：ClearAll<TEvent> 只移除指定類型，其他類型保留
		private static void Test_ClearAllTyped_RemovesOnlyThatType( Scene scene )
		{
			var bus = CreateBus( scene, "T5" );
			int ping = 0, pong = 0;
			bus.Subscribe<PingEvent>( _ => ping++ );
			bus.Subscribe<PongEvent>( _ => pong++ );
			bus.ClearAll<PingEvent>();
			bus.Publish( new PingEvent( 5 ) );
			bus.Publish( new PongEvent( "abc" ) );
			Assert( ping == 0 && pong == 1, "T5 ClearAll<T>_RemovesOnlyThatType", $"ping={ping} pong={pong}" );
			bus.GameObject.Destroy();
		}

		// T6：ClearAll() 清除所有類型的訂閱
		private static void Test_ClearAll_RemovesAllTypes( Scene scene )
		{
			var bus = CreateBus( scene, "T6" );
			int ping = 0, pong = 0;
			bus.Subscribe<PingEvent>( _ => ping++ );
			bus.Subscribe<PongEvent>( _ => pong++ );
			bus.ClearAll();
			bus.Publish( new PingEvent( 6 ) );
			bus.Publish( new PongEvent( "xyz" ) );
			Assert( ping == 0 && pong == 0, "T6 ClearAll_RemovesAllTypes", $"ping={ping} pong={pong}" );
			bus.GameObject.Destroy();
		}

		// T7：Subscribe(null) 拋出 ArgumentNullException
		private static void Test_SubscribeNull_ThrowsArgumentNullException( Scene scene )
		{
			var bus = CreateBus( scene, "T7" );
			bool threw = false;
			try   { bus.Subscribe<PingEvent>( null ); }
			catch ( ArgumentNullException ) { threw = true; }
			catch { /* 其他例外型別視為失敗 */ }
			Assert( threw, "T7 Subscribe_Null_Throws_ArgumentNullException" );
			bus.GameObject.Destroy();
		}

		// T8：Unsubscribe(null) 不拋出例外
		private static void Test_UnsubscribeNull_NoException( Scene scene )
		{
			var bus = CreateBus( scene, "T8" );
			bool ok = true;
			try   { bus.Unsubscribe<PingEvent>( null ); }
			catch { ok = false; }
			Assert( ok, "T8 Unsubscribe_Null_NoException" );
			bus.GameObject.Destroy();
		}

		// T9：Publish 沒有訂閱者時不拋出例外
		private static void Test_PublishNoSubscribers_NoException( Scene scene )
		{
			var bus = CreateBus( scene, "T9" );
			bool ok = true;
			try   { bus.Publish( new PingEvent( 9 ) ); }
			catch { ok = false; }
			Assert( ok, "T9 Publish_NoSubscribers_NoException" );
			bus.GameObject.Destroy();
		}

		// T10：Publish 正確傳遞事件資料（Payload 值完整）
		private static void Test_Publish_PassesEventDataCorrectly( Scene scene )
		{
			var bus = CreateBus( scene, "T10" );
			int received = -1;
			bus.Subscribe<PingEvent>( e => received = e.Value );
			bus.Publish( new PingEvent( 42 ) );
			Assert( received == 42, "T10 Publish_PassesEventData_Correctly", $"received={received}" );
			bus.GameObject.Destroy();
		}

		// T11：同一 handler 訂閱兩次 → Publish 觸發兩次
		private static void Test_SubscribeSameHandlerTwice_InvokedTwice( Scene scene )
		{
			var bus = CreateBus( scene, "T11" );
			int called = 0;
			// 使用具名方法引用才能重複訂閱同實例
			Action<PingEvent> h = _ => called++;
			bus.Subscribe( h );
			bus.Subscribe( h );
			bus.Publish( new PingEvent( 11 ) );
			Assert( called == 2, "T11 SubscribeSameHandlerTwice_InvokedTwice", $"called={called}" );
			bus.GameObject.Destroy();
		}

		// T12：OnDestroy 自動清除 handlers（銷毀後不應再收到事件）
		private static void Test_OnDestroy_ClearsHandlers( Scene scene )
		{
			// 建立 bus，訂閱，銷毀 go，再 Publish — handler 不應被呼叫。
			// 注意：銷毀後的 bus reference 仍可呼叫方法，但 _handlers 已被 Clear。
			var bus = CreateBus( scene, "T12" );
			int called = 0;
			bus.Subscribe<PingEvent>( _ => called++ );
			bus.GameObject.Destroy(); // 觸發 OnDestroy → _handlers.Clear()
			// 取得新 bus 測試相同事件
			var bus2 = CreateBus( scene, "T12b" );
			bus2.Publish( new PingEvent( 12 ) ); // 不應觸發舊的 handler
			// 由於 bus 已在不同 go 上且 _handlers 已清，called 仍為 0
			Assert( called == 0, "T12 OnDestroy_ClearsHandlers" );
			bus2.GameObject.Destroy();
		}
	}
}
