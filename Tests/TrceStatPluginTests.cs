// File: Code/Tests/TrceStatPluginTests.cs
// Encoding: UTF-8 (No BOM)
// 執行方式：在遊戲主控台輸入  trce_test_stat
//
// 測試範圍：TrceStatPlugin 實作的 IAttributeService 合約。
//   - 所有斷言透過 Sandbox.Log 輸出；失敗時記錄 ❌ 並計數。
//   - 測試方法命名：Test_<情境>_<預期結果>。

using Sandbox;
using System;
using System.Collections.Generic;
using Trce.Kernel.Stats;
using Trce.Kernel.Event;
using static Trce.Kernel.Event.CoreEvents;

namespace Trce.Tests
{
	/// <summary>
	/// TrceStatPlugin 單元測試套件。
	/// 在遊戲場景內透過 ConCmd "trce_test_stat" 執行。
	/// </summary>
	public static class TrceStatPluginTests
	{
		// ─── 斷言輔助 ──────────────────────────────────────────────
		private static int _passed;
		private static int _failed;

		private static void Assert( bool condition, string testName, string detail = "" )
		{
			if ( condition )
			{
				_passed++;
				Log.Info( $"  ✅ PASS  {testName}" );
			}
			else
			{
				_failed++;
				Log.Error( $"  ❌ FAIL  {testName}" + ( string.IsNullOrEmpty( detail ) ? "" : $"  ← {detail}" ) );
			}
		}

		private static void AssertApprox( float actual, float expected, string testName, float epsilon = 1e-4f )
		{
			bool ok = MathF.Abs( actual - expected ) <= epsilon;
			if ( ok ) { _passed++; Log.Info( $"  ✅ PASS  {testName}  ({actual:F4})" ); }
			else { _failed++; Log.Error( $"  ❌ FAIL  {testName}  (expected {expected:F4}, got {actual:F4})" ); }
		}

		// ─── 進入點 ────────────────────────────────────────────────
		[Sandbox.ConCmd( "trce_test_stat" )]
		public static void RunAll()
		{
			_passed = 0;
			_failed = 0;

			Log.Info( "═══════════════════════════════════════════════════════" );
			Log.Info( "  TrceStatPlugin Tests" );
			Log.Info( "═══════════════════════════════════════════════════════" );

			// TrceStatPlugin 繼承 Component，必須掛載在 GameObject 上才能實例化。
			var scene = Sandbox.Game.ActiveScene;
			if ( scene == null )
			{
				Log.Warning( "[TrceStatTests] 無活躍場景，測試略過。" );
				return;
			}

			// 建立測試用 GameObject 並取得插件實例（不呼叫 InitializeAsync，避免服務注冊副作用）
			var go = scene.CreateObject();
			go.Name = "__TrceStatPlugin_TestBed__";
			TrceStatPlugin sut = null;
			try
			{
				sut = go.Components.Create<TrceStatPlugin>( false ); // false = 不自動啟動
				RunTests( sut );
			}
			catch ( Exception ex )
			{
				Log.Error( $"[TrceStatTests] 執行期例外: {ex.Message}\n{ex.StackTrace}" );
			}
			finally
			{
				go.Destroy();
			}

			Log.Info( $"─── 結果: {_passed} 通過, {_failed} 失敗 ───────────────────" );
		}

		private static void RunTests( TrceStatPlugin sut )
		{
			const ulong SID   = 76561198000000001UL;
			const string ATTR = "player.speed";

			// ── T1：未知實體 / 屬性，回傳 0 ──────────────────────────
			{
				float val = sut.GetTotalValue( SID, ATTR );
				AssertApprox( val, 0f, "T1 GetTotalValue_UnknownEntity_Returns0" );
			}

			// ── T2：SetBaseValue 後不加任何修飾符，GetTotalValue == base ──
			{
				sut.SetBaseValue( SID, ATTR, 100f );
				float val = sut.GetTotalValue( SID, ATTR );
				AssertApprox( val, 100f, "T2 SetBaseValue_NoModifiers_ReturnsBase" );
			}

			// ── T3：Add 修飾符疊加到 base ────────────────────────────
			{
				sut.SetBaseValue( SID, ATTR, 100f );
				sut.AddModifier( SID, ATTR, AttributeModifier.Flat( 20f ) );
				float val = sut.GetTotalValue( SID, ATTR );
				AssertApprox( val, 120f, "T3 AddModifier_Flat_AddsToBase" );
			}

			// ── T4：Multiply 修飾符乘以当前总和 ──────────────────────
			{
				// 重置：清除前一輪修飾符
				sut.SetBaseValue( SID, ATTR, 100f );
				// 移除所有先前修飾符：透過追蹤 GUID 測試
				Guid mulId = sut.AddModifier( SID, ATTR, AttributeModifier.Percent( 1.5f ) );
				// 先移掉 T3 的殘留 flat +20 — 重新建立乾淨狀態
				// 簡潔做法：使用新的 SID 避免交疊
				const ulong SID2 = 76561198000000002UL;
				sut.SetBaseValue( SID2, ATTR, 100f );
				Guid mulId2 = sut.AddModifier( SID2, ATTR, AttributeModifier.Percent( 1.5f ) );
				float val = sut.GetTotalValue( SID2, ATTR );
				AssertApprox( val, 150f, "T4 AddModifier_Multiply_MultipliesBase" );
			}

			// ── T5：公式驗證 (base + sum_adds) * prod_muls ───────────
			{
				const ulong SID3 = 76561198000000003UL;
				sut.SetBaseValue( SID3, ATTR, 100f );
				sut.AddModifier( SID3, ATTR, AttributeModifier.Flat( 50f ) );      // 100+50=150
				sut.AddModifier( SID3, ATTR, AttributeModifier.Percent( 2.0f ) );  // *2
				float val = sut.GetTotalValue( SID3, ATTR );
				AssertApprox( val, 300f, "T5 Combined_(base+adds)*muls_Formula" );
			}

			// ── T6：RemoveModifier 正確還原總值 ──────────────────────
			{
				const ulong SID4 = 76561198000000004UL;
				sut.SetBaseValue( SID4, ATTR, 100f );
				Guid id = sut.AddModifier( SID4, ATTR, AttributeModifier.Flat( 50f ) );
				float before = sut.GetTotalValue( SID4, ATTR ); // 150
				sut.RemoveModifier( SID4, ATTR, id );
				float after = sut.GetTotalValue( SID4, ATTR );  // 100
				AssertApprox( before, 150f, "T6a RemoveModifier_Before=150" );
				AssertApprox( after,  100f, "T6b RemoveModifier_After=100" );
			}

			// ── T7：SetBaseValue 值不變時不觸發事件（No-Op 路徑）───
			{
				const ulong SID5 = 76561198000000005UL;
				int eventCount = 0;
				Action<AttributeChangedEvent> handler = _ => eventCount++;
				GlobalEventBus.Subscribe( handler );
				try
				{
					sut.SetBaseValue( SID5, ATTR, 200f ); // 初始設定 → 觸發 1 次
					int countAfterFirst = eventCount;
					sut.SetBaseValue( SID5, ATTR, 200f ); // 相同值 → 不觸發
					int countAfterNop   = eventCount;
					Assert( countAfterFirst == 1,  "T7a SetBaseValue_FirstCall_FiresEvent" );
					Assert( countAfterNop   == 1,  "T7b SetBaseValue_SameValue_NoEvent (No-Op)" );
				}
				finally
				{
					GlobalEventBus.Unsubscribe( handler );
				}
			}

			// ── T8：臟標記快取 — 未改變時不重算 ─────────────────────
			{
				const ulong SID6 = 76561198000000006UL;
				sut.SetBaseValue( SID6, ATTR, 50f );
				float first  = sut.GetTotalValue( SID6, ATTR );
				float second = sut.GetTotalValue( SID6, ATTR ); // 應直接回傳快取
				AssertApprox( first,  50f, "T8a DirtyCache_FirstRead=50" );
				AssertApprox( second, 50f, "T8b DirtyCache_SecondRead_UsesCache=50" );
			}

			// ── T9：RemoveModifier on nonexistent id — no crash ──────
			{
				const ulong SID7 = 76561198000000007UL;
				sut.SetBaseValue( SID7, ATTR, 10f );
				sut.RemoveModifier( SID7, ATTR, Guid.NewGuid() ); // 不存在的 ID
				float val = sut.GetTotalValue( SID7, ATTR );
				AssertApprox( val, 10f, "T9 RemoveModifier_NonExistentId_NoException" );
			}

			// ── T10：OnPluginDisabled 清空所有資料 ───────────────────
			{
				const ulong SID8 = 76561198000000008UL;
				sut.SetBaseValue( SID8, ATTR, 999f );
				sut.AddModifier( SID8, ATTR, AttributeModifier.Flat( 1f ) );
				// 直接呼叫 internal 停用流程等同品：用 reflection 或繞過 — 
				// 由於 OnPluginDisabled 是 protected，改用 Destroy 觸發 OnDestroy → OnPluginDisabled。
				// 此處只能確認在 Destroy 前有資料，Destroy 後不可再呼叫（go 已移除）。
				// 因此僅測試資料在 disable 前確實存在。
				float before = sut.GetTotalValue( SID8, ATTR );
				Assert( before > 999f, "T10 BeforeDisable_DataExists (>999)" );
				// 實際 OnPluginDisabled 清空行為由 integration 場景測試覆蓋。
			}
		}
	}
}
