using Sandbox;
using System.Collections.Generic;
using Trce.Kernel.Plugin.Services;

namespace Trce.Kernel.Player

{
	/// <summary>
	/// Player state data (synchronized between server and client).
	/// </summary>
	public class TrcePlayerState
	{
		// Properties synced to clients via [Sync].
		public ulong SteamId { get; internal set; }
		public string DisplayName { get; internal set; }
		public AliveState AliveState { get; internal set; } = AliveState.Alive;
		/// <summary> Team ID the player belongs to. </summary>
		public string TeamId { get; internal set; }
		/// <summary> Role ID the player is currently playing. </summary>
		public string RoleId { get; internal set; }
		// Server-only properties.
		public float Health { get; internal set; } = 100f;
		public float MaxHealth { get; internal set; } = 100f;
		public string CurrentZone { get; internal set; } = "";
		public int KillCount { get; internal set; } = 0;
		public int TaskCompleteCount { get; internal set; } = 0;
		public double JoinTime { get; internal set; }
		public double LastModifiedTime { get; internal set; }
		/// <summary> Server-side transient dynamic data dictionary. </summary>
		internal Dictionary<string, object> ServerData { get; } = new();
		public bool IsAlive => AliveState == AliveState.Alive;
		public float HealthPercent => MaxHealth > 0 ? Health / MaxHealth : 0f;
		public override string ToString() =>
			$"[Player] {DisplayName} ({SteamId}) | {AliveState} | {RoleId ?? "NoRole"} | HP:{Health:F0}";
	}

}
