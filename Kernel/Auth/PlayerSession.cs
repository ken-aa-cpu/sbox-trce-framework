// ����������������������������������������������������������������������������������������������������������������������������������������
// ��  Copyright (c) 2026 TRCE Team. All rights reserved.            ��
// ��  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ��
// ��  Copyright (c) 2026 TRCE Team. All rights reserved.            ��
// ��  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ��
// Human readers: Welcome. You are viewing this for learning.
// Commercial use requires a valid TRCE Framework License.
// ����������������������������������������������������������������������������������������������������������������������������������������
using Sandbox;
using Sandbox.Network;

namespace Trce.Kernel.Auth

{
	/// <summary>
	///   /   Session
	///
	///   /   PlayerSession
	///   / Session
	///
	/// �ͩR�g���G
	///   /       Authenticated (IsActive=true)
	///   /       Disconnected (IsActive=false,   ReconnectWindowSeconds)
	///   /       Authenticated (  Session)
	///   /       Expired (Session  )
	/// </summary>
	public class PlayerSession
	{
		/// <summary> ( )</summary>
		public const float ReconnectWindowSeconds = 120f;
		/// <summary> Steam 64-bit ID</summary>
		public ulong SteamId { get; private set; }
		/// <summary>玩家的 Steam 顯示名稱。會在重新連線時同步更新為最新的 Steam 名稱。</summary>
		public string DisplayName { get; private set; }
		/// <summary> (  null)</summary>
		public Connection CurrentConnection { get; private set; }
		/// <summary>Permission user data containing groups and nodes.</summary>
		public TrcePermissionUser PermissionUser { get; set; }
		/// <summary>若玩家目前處於離線等待重連狀態，回傳 true。在重連視窗內，Session 保持有效。</summary>
		public bool IsDisconnected { get; private set; }
		/// <summary> (     )</summary>
		public bool IsActive => !IsDisconnected || TimeSinceDisconnected < ReconnectWindowSeconds;
		/// <summary> Session   ( )</summary>
		public bool IsExpired => IsDisconnected && TimeSinceDisconnected >= ReconnectWindowSeconds;
		/// <summary>玩家斷線後的已流逝時間（秒）。用於計算是否超過 <see cref="ReconnectWindowSeconds"/> 重連視窗。</summary>
		public TimeSince TimeSinceDisconnected { get; private set; }
		/// <summary> Server</summary>
		public float ConnectedAt { get; private set; }
		/// <summary> Session</summary>
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

		/// <summary> Session</summary>
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

