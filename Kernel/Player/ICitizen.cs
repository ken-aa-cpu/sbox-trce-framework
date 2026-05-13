using Sandbox;

namespace Trce.Kernel.Player
{
	/// <summary>
	/// Citizen interface: defines the control surface that all "citizens" (players or NPCs) must expose.
	/// Allows AI systems and player input to share the same logic pipeline.
	/// </summary>
	public interface ICitizen
	{
		/// <summary>
		/// Gets or sets the citizen's current intent.
		/// For players this is updated every frame by the input system.
		/// For NPCs this is set by the AI decision system.
		/// </summary>
		CitizenIntent Intent { get; set; }

		/// <summary> Reference to the owning <see cref="GameObject"/>. </summary>
		GameObject GameObject { get; }

		/// <summary> World-space position. </summary>
		Vector3 Position { get; set; }

		/// <summary> Body rotation. </summary>
		Rotation Rotation { get; set; }

		/// <summary> Eye / head rotation. </summary>
		Rotation EyeRotation { get; set; }

		/// <summary>
		/// Executes a named action.
		/// The ActionEngine resolves the name into animation parameters or script logic.
		/// </summary>
		void ExecuteAction( string actionName );

		/// <summary> Fired when an action is requested (e.g. by input or AI). </summary>
		event System.Action<string> OnActionRequested;

		/// <summary>
		/// When <c>true</c>, the citizen is under script control (cutscene, forced state such as being knocked out).
		/// All action requests are silently discarded.
		/// </summary>
		bool IsControlledByScript { get; set; }
	}
}
