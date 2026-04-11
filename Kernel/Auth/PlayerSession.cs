using Sandbox;
using Sandbox.Network;

namespace Trce.Kernel.Auth

{
	/// <summary>
	/// 代表一個連線玩家的工作階段。
	/// 管理驗證狀態與斷線重連機制。
	///
	/// 工作階段生命週期 (Lifecycle):
	///       Authenticated (IsActive=true, 玩家在線)
	///       Disconnected (IsActive=false, 進入 ReconnectWindowSeconds 倒數)
	///       Authenticated (在倒數內重新連線，Session 延續)
	///       Expired (超過斷線寬限期，Session 註銷)
	/// </summary>
	public class PlayerSession
	{
		/// <summary> 允許重新連線的寬限時間 (秒) </summary>
		public const float ReconnectWindowSeconds = 120f;
		/// <summary> Steam 64-bit ID</summary>
		public ulong SteamId { get; private set; }
		/// <summary>玩家的 Steam 顯示名稱。會在重新連線時同步更新為最新的 Steam 名稱。</summary>
		public string DisplayName { get; private set; }
		/// <summary> 目前的連線物件 (斷線時為 null) </summary>
		public Connection CurrentConnection { get; private set; }
		/// <summary>Permission user data containing groups and nodes.</summary>
		public TrcePermissionUser PermissionUser { get; set; }
		/// <summary>若玩家目前處於離線等待重連狀態，回傳 true。在重連視窗內，Session 保持有效。</summary>
		public bool IsDisconnected { get; private set; }
		/// <summary> 確認 Session 是否仍處於啟用或可供重連的狀態 </summary>
		public bool IsActive => !IsDisconnected || TimeSinceDisconnected < ReconnectWindowSeconds;
		/// <summary> Session 是否已經逾時且再也無法重連 (應予以清理) </summary>
		public bool IsExpired => IsDisconnected && TimeSinceDisconnected >= ReconnectWindowSeconds;
		/// <summary>玩家斷線後的已流逝時間（秒）。用於計算是否超過 <see cref="ReconnectWindowSeconds"/> 重連視窗。</summary>
		public TimeSince TimeSinceDisconnected { get; private set; }
		/// <summary> Session 建立的 Server 時間 (初次連線時間) </summary>
		public float ConnectedAt { get; private set; }
		/// <summary> 建立初始 Session </summary>
		public PlayerSession( Connection connection, float serverTime )
		{
			SteamId = connection.SteamId;
			DisplayName = connection.DisplayName;
			CurrentConnection = connection;
			ConnectedAt = serverTime;
			IsDisconnected = false;
		}

		/// <summary>將此 Session 標記為已斷線並啟動重連計時視窗。由 <c>TrceNetManager</c> 在玩家斷線時呼叫。</summary>
		public void MarkDisconnected()
		{
			IsDisconnected = true;
			CurrentConnection = null;
			TimeSinceDisconnected = 0;
		}

		/// <summary> 玩家重新連入時恢復 Session </summary>
		public bool Reconnect( Connection newConnection )
		{
			if ( IsExpired )
				return false;
			CurrentConnection = newConnection;
			DisplayName = newConnection.DisplayName;
			IsDisconnected = false;
			return true;
		}

		public override string ToString()
		{
			var status = IsDisconnected ? "DISCONNECTED" : "ACTIVE";
			return $"[Session] {DisplayName} ({SteamId}) | {status} | Groups: {string.Join( ", ", PermissionUser?.Groups ?? new() )}";
		}

	}

}

