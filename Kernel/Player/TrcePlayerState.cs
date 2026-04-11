using Sandbox;
using System.Collections.Generic;
using Trce.Kernel.Plugin.Services;

namespace Trce.Kernel.Player

{
	/// <summary>
	/// 玩家的狀態資料（伺服器與客戶端同步）
	/// </summary>
	public class TrcePlayerState
	{
		// [Sync] 同步到 Client 的屬性
		public ulong SteamId { get; internal set; }
		public string DisplayName { get; internal set; }
		public AliveState AliveState { get; internal set; } = AliveState.Alive;
		/// <summary> 所屬隊伍 ID </summary>
		public string TeamId { get; internal set; }
		/// <summary> 扮演角色 ID </summary>
		public string RoleId { get; internal set; }
		// 僅限 Server 存取的屬性
		public float Health { get; internal set; } = 100f;
		public float MaxHealth { get; internal set; } = 100f;
		public string CurrentZone { get; internal set; } = "";
		public int KillCount { get; internal set; } = 0;
		public int TaskCompleteCount { get; internal set; } = 0;
		public double JoinTime { get; internal set; }
		public double LastModifiedTime { get; internal set; }
		/// <summary> 伺服器端暫存的動態資料字典 </summary>
		internal Dictionary<string, object> ServerData { get; } = new();
		public bool IsAlive => AliveState == AliveState.Alive;
		public float HealthPercent => MaxHealth > 0 ? Health / MaxHealth : 0f;
		public override string ToString() =>
			$"[Player] {DisplayName} ({SteamId}) | {AliveState} | {RoleId ?? "NoRole"} | HP:{Health:F0}";
	}

}

