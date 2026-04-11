using Sandbox;
using System;
using System.Linq;

namespace Trce.Plugins.GameState
{

	[GameResource( "TRCE Task: Switch Phase", "trcetask", "Switch game phase when progress reached" )]
	public class SwitchPhaseAction : TaskThresholdAction
	{
		[Property] public GamePhase TargetPhase { get; set; } = GamePhase.HuntPhase;
		[Property] public float Duration { get; set; } = 0f;

		public override void Execute( TaskProgressTracker tracker )
		{
			var phaseMgr = Sandbox.Game.ActiveScene.Get<GamePhaseManager>();
			if ( phaseMgr == null )
			{
				phaseMgr = tracker.Scene.GetAllComponents<GamePhaseManager>().FirstOrDefault();
			}
			phaseMgr?.SwitchPhase( TargetPhase, Duration );
		}
	}

	[GameResource( "TRCE Task: Log Message", "trcetask", "Log a message when progress reached" )]
	public class LogMessageAction : TaskThresholdAction
	{
		[Property] public string Message { get; set; } = "Threshold Reached!";

		public override void Execute( TaskProgressTracker tracker )
		{
			Log.Info( $"[TaskAction] {Message} (Progress: {tracker.Progress * 100:F1}%)" );
		}
	}
}

