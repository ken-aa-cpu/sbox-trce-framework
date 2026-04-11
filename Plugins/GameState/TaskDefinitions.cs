using Sandbox;
using System.Collections.Generic;

namespace Trce.Plugins.GameState

{
	/// <summary>
	/// 任�??�??
	/// </summary>
	public enum TaskState
	{
		Available,
		InProgress,
		Completed,
		Cooldown,
		Disabled
	}

	/// <summary>
	///   / ? ? ??  -  ?Tracker ? ? ? ?? ??( ? ? ? ? ??
	/// </summary>
	public interface ITrceTask
	{
		string TaskId { get; }
		string DisplayName { get; }
		float Weight { get; } // 權�?（影?��?�?
		float CurrentProgress { get; } // 0~1
		TaskState State { get; }
		bool IsGlobal { get; } // ?�否?�全局任�?
	}

	/// <summary>
	///   /  ?? ? ?- ? ?  X% ? ?? ?? ? ?
	/// </summary>
	public abstract class TaskThresholdAction : GameResource
	{
		[Property, Range(0, 1)] public float TargetProgress { get; set; } = 0.5f;
		/// <summary> ? ?? ? (??Tracker  )</summary>
		public abstract void Execute( TaskProgressTracker tracker );
	}

}

