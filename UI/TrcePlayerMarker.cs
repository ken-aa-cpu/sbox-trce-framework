using Sandbox;
using Sandbox.UI;
using System.Linq;
using Trce.Plugins.Shared.Teams;
using Trce.Game.Player;
using Trce.Kernel.Plugin;

namespace Sandbox.UI
{
	/// <summary>
	/// World-space player name marker (attached above a player's head).
	///
	/// Features:
	///   1. Dynamically updates team color from the team system.
	///   2. Renders three elements: Dot, Name label, and Focus highlight.
	///   3. Automatically hides when out of range or behind obstacles.
	/// </summary>
	[Icon( "person_pin" )]
	public class TrcePlayerMarker : Component
	{
		[Property] public TrcePlayer TargetPlayer { get; set; }
		[Property] public Vector3 Offset { get; set; } = Vector3.Up * 75f;

		private Sandbox.WorldPanel worldPanel;
		private float currentDistance;
		public bool IsFocused { get; private set; }
		public Color TeamColor { get; private set; } = Color.White;
		public string TeamName { get; private set; }

		protected override void OnStart()
		{
			if ( TargetPlayer == null ) TargetPlayer = Components.Get<TrcePlayer>();

			// Create WorldPanel
			worldPanel = Components.Create<Sandbox.WorldPanel>();
			worldPanel.GameObject.Flags |= GameObjectFlags.NotSaved;
			worldPanel.PanelSize = new Vector2( 500, 200 );
			worldPanel.RenderOptions.Overlay = true;

			// Build UI
			var ui = worldPanel.Components.Create<Sandbox.UI.TrcePlayerMarkerUI>();
			ui.Marker = this;
		}

		public bool IsVisibleToLocal { get; private set; } = true;

		protected override void OnUpdate()
		{
			if ( TargetPlayer == null || worldPanel == null ) return;

			// Calculate distance to camera
			currentDistance = (Scene.Camera.WorldPosition - TargetPlayer.WorldPosition).Length;

			// Hide marker if too far away
			if ( currentDistance > 2000f )
			{
				worldPanel.Enabled = false;
				return;
			}

			// Billboard: position above player and face toward camera
			worldPanel.WorldPosition = TargetPlayer.WorldPosition + Offset;
			worldPanel.WorldRotation = Rotation.LookAt( Scene.Camera.WorldPosition - worldPanel.WorldPosition );

			// 1. Update team info and focus state
			UpdateTeamInfo();
			UpdateFocusState();

			// 2. Determine line-of-sight visibility
			UpdateVisibility();

			worldPanel.Enabled = IsVisibleToLocal;
		}

		private void UpdateVisibility()
		{
			var teamMgr = Scene.GetAllComponents<TrceTeamManager>().FirstOrDefault();
			bool isTeammate = teamMgr?.AreTeammates( Connection.Local?.SteamId ?? 0ul, TargetPlayer.SteamId ) ?? false;

			// Teammates are always visible
			if ( isTeammate )
			{
				IsVisibleToLocal = true;
				return;
			}

			// Non-teammates: check line of sight (raycast)
			var tr = Scene.Trace.Ray( Scene.Camera.WorldPosition, worldPanel.WorldPosition )
				.IgnoreGameObject( Scene.Camera.GameObject )
				.IgnoreGameObject( TargetPlayer.GameObject )
				.Run();

			// If ray hit something before reaching the marker, it's occluded
			IsVisibleToLocal = !tr.Hit;
		}

		private void UpdateFocusState()
		{
			// Cast forward from camera to detect focus target
			var tr = Scene.Trace.Ray( Scene.Camera.WorldPosition, Scene.Camera.WorldPosition + Scene.Camera.WorldRotation.Forward * 2000f )
				.IgnoreGameObject( Scene.Camera.GameObject )
				.Run();

			IsFocused = tr.Hit && tr.GameObject.Components.GetInAncestorsOrSelf<TrcePlayer>() == TargetPlayer;
		}

		private void UpdateTeamInfo()
		{
			var teamMgr = Scene.GetAllComponents<TrceTeamManager>().FirstOrDefault();
			if ( teamMgr != null )
			{
				var team = teamMgr.GetPlayerTeam( TargetPlayer.SteamId );
				TeamColor = team?.TeamColor ?? Color.White;
				TeamName = team?.Name;
			}
		}

		public string GetDisplayName() => TargetPlayer?.DisplayName ?? "Unknown";

		protected override void OnDestroy()
		{
			worldPanel?.Destroy();
		}
	}
}

