using System.Threading.Tasks;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Net;
using Trce.Kernel.Plugin;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.GameState
{
	/// <summary>
	/// TaskProgressTracker v2.0
	/// </summary>
	[TrcePluginAttribute(
		Id = "trce.gamestate.taskTracker",
		Name = "TRCE Task Progress Tracker",
		Version = "2.0.0",
		Depends = new[] { "trce.gamestate.phase" }
	)]
	public class TaskProgressTracker : TrcePlugin
	{
		public Action<float> OnProgressUpdated;
		
		[Sync(SyncFlags.FromHost), Property, ReadOnly]
		public float Progress { get; private set; } = 0f;
		
		public Action OnProgressReached100;
		public Action<ulong, string, string> OnTaskCompleted;
		
		[Property, Group("Triggers")]
		public List<TaskThresholdAction> ThresholdActions { get; set; } = new();
		
		private HashSet<string> triggeredIds = new();
		private readonly List<ITrceTask> roomTasks = new();

		protected override async Task OnPluginEnabled()
		{
		}

		protected override void OnPluginDisabled()
		{
		}

		protected override void OnStart()
		{
			if ( (SandboxBridge.Instance?.IsServer ?? false) )
			{
				RefreshTaskPool();

				var papi = Kernel.Papi.PlaceholderAPI.For( this );
				papi?.Register( "trce_progress", () => $"{( Progress * 100 ):F0}%" );
			}
		}

		public void RefreshTaskPool()
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			roomTasks.Clear();

			var found = Scene.GetAllComponents<Component>().OfType<ITrceTask>();
			roomTasks.AddRange( found );

			Log.Info( $"[TaskSystem] Found {roomTasks.Count} tasks" );
			UpdateProgress();
		}

		private float bonusProgress = 0f;
		
		public void AddProgress( float amount, string source )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			bonusProgress += amount;
			UpdateProgress();
			Log.Info( $"[TaskSystem] Added Progress: +{amount * 100:F1}% ({source})" );
		}

		public void UpdateProgress()
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			float totalWeight = roomTasks.Sum( t => t.Weight );
			if ( totalWeight <= 0 )
			{
				Progress = Math.Clamp( bonusProgress, 0f, 1f );
			}
			else
			{
				float completedWeight = roomTasks.Where( t => t.State == TaskState.Completed ).Sum( t => t.Weight );
				float partialWeight = roomTasks.Where( t => t.State == TaskState.InProgress )
											   .Sum( t => t.Weight * t.CurrentProgress );
				Progress = Math.Clamp( ( completedWeight + partialWeight ) / totalWeight + bonusProgress, 0f, 1f );
			}
			
			OnProgressUpdated?.Invoke( Progress );
			
			if ( Progress >= 1.0f )
			{
				OnProgressReached100?.Invoke();
			}

			CheckActions();
		}

		private void CheckActions()
		{
			foreach ( var action in ThresholdActions )
			{
				if ( action == null ) continue;
				string key = action.ResourcePath;
				if ( triggeredIds.Contains( key ) ) continue;
				if ( Progress >= action.TargetProgress )
				{
					triggeredIds.Add( key );
					Log.Info( $"[TaskSystem] Triggered: {action.ResourceName} ({action.TargetProgress * 100}%)" );
					action.Execute( this );
				}
			}
		}

		public void ResetProgress()
		{
			bonusProgress = 0;
			triggeredIds.Clear();
			UpdateProgress();
		}
	}
}


