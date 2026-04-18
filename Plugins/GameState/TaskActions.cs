using Sandbox;
using System;
using System.Linq;
using Trce.Kernel.Plugin.Services;

namespace Trce.Plugins.GameState
{

	[GameResource( "TRCE Task: Switch Phase", "trcetask", "Switch game phase when progress reached" )]
	public class SwitchPhaseAction : TaskThresholdAction
	{
		[Property] public GamePhaseEnum TargetPhase { get; set; } = GamePhaseEnum.HuntPhase;
		[Property] public float Duration { get; set; } = 0f;

		public override void Execute( TaskProgressTracker tracker )
		{
			// P0-1: Resolve IGamePhaseService from TrceServiceManager instead of Scene.Get<GamePhaseManager>()
			var phaseSvc = Trce.Kernel.Plugin.TrceServiceManager.Instance?.GetService<IGamePhaseService>();
			phaseSvc?.SwitchPhase( TargetPhase, Duration );
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

