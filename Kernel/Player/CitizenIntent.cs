using Sandbox;

namespace Trce.Kernel.Player
{
	/// <summary>
	///   代表公民（玩家或 NPC）的動作意圖。
	///   這允許我們將「按鈕輸入」或「AI 決策」與「物理運動/動畫」解耦。
	/// </summary>
	public struct CitizenIntent
	{
		/// <summary> 相對於世界的移動向量 (0 到 1) </summary>
		public Vector3 WishMove { get; set; }

		/// <summary> 視角朝向 (Quaternion) </summary>
		public Rotation WishLook { get; set; }

		/// <summary> 是否想要跳躍 </summary>
		public bool WishJump { get; set; }

		/// <summary> 是否想要衝刺 </summary>
		public bool WishSprint { get; set; }

		/// <summary>
		///   主要攻擊請求 (attack1 / 左鍵)。
		///   由 <c>CitizenRoot.FilterIntent</c> 在 restrict.action 或 state.dead 時強制歸零。
		/// </summary>
		public bool WishAttack { get; set; }

		/// <summary>
		///   互動 / 使用請求 (use / E 鍵)。
		///   由 <c>CitizenRoot.FilterIntent</c> 在 restrict.action 或 state.dead 時強制歸零。
		/// </summary>
		public bool WishUse { get; set; }

		/// <summary> 具名動作字串 (Interact, Reload, etc.)；歸零時設為 null。 </summary>
		public string ActiveAction { get; set; }

		/// <summary> 蹲下程度 (0 到 1) </summary>
		public float DuckAmount { get; set; }
	}
}

