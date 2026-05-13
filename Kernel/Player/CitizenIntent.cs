using Sandbox;

namespace Trce.Kernel.Player
{
	/// <summary>
	/// Represents the movement and action intent of a citizen (player or NPC).
	/// Decouples "button input" or "AI decisions" from "physics / animation" execution.
	/// </summary>
	public struct CitizenIntent
	{
		/// <summary> World-relative movement vector (0 to 1). </summary>
		public Vector3 WishMove { get; set; }

		/// <summary> View direction (Quaternion). </summary>
		public Rotation WishLook { get; set; }

		/// <summary> Whether the citizen wants to jump. </summary>
		public bool WishJump { get; set; }

		/// <summary> Whether the citizen wants to sprint. </summary>
		public bool WishSprint { get; set; }

		/// <summary>
		/// Primary attack request (attack1 / left-click).
		/// Forced to <c>false</c> by <c>CitizenRoot.FilterIntent</c> when <c>restrict.action</c> or <c>state.dead</c> is set.
		/// </summary>
		public bool WishAttack { get; set; }

		/// <summary>
		/// Interaction / use request (use / E key).
		/// Forced to <c>false</c> by <c>CitizenRoot.FilterIntent</c> when <c>restrict.action</c> or <c>state.dead</c> is set.
		/// </summary>
		public bool WishUse { get; set; }

		/// <summary> Named action string (Interact, Reload, etc.); set to <c>null</c> when cleared. </summary>
		public string ActiveAction { get; set; }

		/// <summary> Crouch amount (0 to 1). </summary>
		public float DuckAmount { get; set; }
	}
}


