// File: Code/Kernel/Net/TraceNetManager.cs
// Encoding: UTF-8 (No BOM)

using Sandbox;
using System.Threading.Tasks;
using Trce.Kernel.Auth;
using Trce.Kernel.Event;
using Trce.Kernel.Plugin;

namespace Trce.Kernel.Net
{
	/// <summary>
	/// <para>【TRCE 網路核心管理器】</para>
	/// <para>
	/// 本類別的唯一職責：管理伺服器連線的生命週期狀態，
	/// 並在連線準備就緒後，透過 <see cref="GlobalEventBus"/> 廣播訊號。
	/// </para>
	/// <para>
	/// <b>架構邊界（必須嚴格遵守）：</b><br/>
	/// - 本類別絕對不可直接持有任何遊戲實體的引用（如 PlayerPrefab、SpawnPoint）。<br/>
	/// - 玩家 Pawn 的生成邏輯屬於「遊戲模式插件」的職責，
	///   由插件訂閱 <see cref="CoreEvents.ClientReadyEvent"/> 來實作。<br/>
	/// - 本類別只負責發布訊號，不關心訊號被誰消費。
	/// </para>
	/// </summary>
	[Title( "TRCE Net Manager" ), Group( "Trce - Kernel" ), Icon( "wifi" )]
	public class TrceNetManager : GameObjectSystem, ISceneStartup, INetManager
	{
		// ═══════════════════════════════════════════════════════════════════
		//  Singleton (GameObjectSystem 保證每個 Scene 僅有一個實例)
		// ═══════════════════════════════════════════════════════════════════

		/// <summary>
		/// 當前場景的 TrceNetManager 實例。
		/// </summary>
		public static TrceNetManager Instance { get; private set; }

		// ═══════════════════════════════════════════════════════════════════
		//  建構子
		// ═══════════════════════════════════════════════════════════════════

		public TrceNetManager( Scene scene ) : base( scene )
		{
			Instance = this;
		}

		// ═══════════════════════════════════════════════════════════════════
		//  ISceneStartup
		// ═══════════════════════════════════════════════════════════════════

		/// <inheritdoc/>
		public void OnSceneStartup()
		{
			// Register with TrceServiceManager so plugins can resolve via GetService<INetManager>().
			TrceServiceManager.Instance?.RegisterService<INetManager>( this );
			Log.Info( "[Net] TrceNetManager initialized." );
		}

		// ═══════════════════════════════════════════════════════════════════
		//  連線生命週期入口（由 GameMode / 頂層 Component 呼叫）
		// ═══════════════════════════════════════════════════════════════════

		/// <summary>
		/// 處理客戶端連線請求。
		/// <para>
		/// 流程：
		/// <list type="number">
		///   <item>委託 <see cref="TrceAuthService"/> 進行身份驗證。</item>
		///   <item>驗證失敗或連線已失效則中止。</item>
		///   <item>驗證成功後，透過 <see cref="GlobalEventBus"/> 發布
		///         <see cref="CoreEvents.ClientReadyEvent"/>，
		///         由遊戲模式插件負責後續的 Pawn 生成。</item>
		/// </list>
		/// </para>
		/// </summary>
		/// <param name="channel">發起連線的客戶端 <see cref="Connection"/>。</param>
		public async Task DispatchClientConnected( Connection channel )
		{
			// ── Step 1：身份驗證（僅在 Auth 服務可用時執行）────────────────
			var auth = TrceAuthService.Instance;
			if ( auth != null )
			{
				var session = await auth.Authenticate( channel );

				// 驗證失敗（已有重複連線、被封鎖等）
				if ( session == null )
				{
					Log.Warning( $"[Net] Auth rejected connection for {channel.DisplayName} ({channel.SteamId})." );
					return;
				}
			}

			// ── Step 2：驗證連線仍然有效（防止 Auth 期間掉線）──────────────
			if ( !channel.IsActive )
			{
				Log.Warning( $"[Net] Connection became inactive during auth: {channel.DisplayName} ({channel.SteamId})." );
				return;
			}

			// ── Step 3：Zero-GC 事件廣播，通知所有遊戲模式插件 ────────────
			// struct 於 Stack 建立，Publish 路徑無 Heap Allocation。
			var evt = new CoreEvents.ClientReadyEvent(
				channel:     channel,
				steamId:     channel.SteamId,
				displayName: channel.DisplayName
			);

			GlobalEventBus.Publish( evt );

			Log.Info( $"[Net] ClientReadyEvent published for {channel.DisplayName} ({channel.SteamId})." );
		}

		/// <summary>
		/// 處理客戶端斷線。
		/// <para>
		/// 流程：
		/// <list type="number">
		///   <item>通知 <see cref="TrceAuthService"/> 標記 Session 為斷線。</item>
		///   <item>透過 <see cref="GlobalEventBus"/> 發布
		///         <see cref="CoreEvents.ClientDisconnectedEvent"/>，
		///         由遊戲模式插件負責清理 Pawn 與相關狀態。</item>
		/// </list>
		/// </para>
		/// </summary>
		/// <param name="channel">已斷線的客戶端 <see cref="Connection"/>。</param>
		public void DispatchClientDisconnected( Connection channel )
		{
			// ── Step 1：通知 Auth 服務標記斷線 Session ──────────────────────
			TrceAuthService.Instance?.HandleDisconnect( channel );

			// ── Step 2：Zero-GC 事件廣播，通知所有遊戲模式插件 ────────────
			var evt = new CoreEvents.ClientDisconnectedEvent(
				channel:     channel,
				steamId:     channel.SteamId,
				displayName: channel.DisplayName
			);

			GlobalEventBus.Publish( evt );

			Log.Info( $"[Net] ClientDisconnectedEvent published for {channel.DisplayName} ({channel.SteamId})." );
		}
	}
}
