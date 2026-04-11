using System;
using System.Collections.Generic;

namespace Trce.Plugins.Shared.Evidence
{
	public enum FactType
	{
		CoLocation,
		TaskCompleted,
		Movement,
		BodyFound,
		SystemFault,
		AbilityUsed,
		Custom
	}

	public class FactCard
	{
		public string CardId { get; set; }
		public FactType Type { get; set; }
		public float Timestamp { get; set; }
		public ulong OwnerSteamId { get; set; }
		public List<ulong> InvolvedPlayers { get; set; } = new();
		public string Location { get; set; }
		public float Duration { get; set; }
		public string DisplayTemplate { get; set; }
		public Dictionary<string, string> ExtraData { get; set; } = new();
		public bool IsPlayed { get; set; } = false;

		public static string GenerateId()
		{
			return Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );
		}

		public static FactCard CreateCoLocation( ulong observer, ulong target, string location, float duration, float timestamp )
		{
			return new FactCard
			{
				CardId = GenerateId(),
				Type = FactType.CoLocation,
				Timestamp = timestamp,
				OwnerSteamId = observer,
				InvolvedPlayers = new List<ulong> { target },
				Location = location,
				Duration = duration,
				DisplayTemplate = $"%lang_fact_colocation%" // 語系包: "Saw %target% at {location} for {duration:F0}s"
			};
		}

		public static FactCard CreateTaskCompleted( ulong player, string taskId, string location, float timestamp )
		{
			return new FactCard
			{
				CardId = GenerateId(),
				Type = FactType.TaskCompleted,
				Timestamp = timestamp,
				OwnerSteamId = player,
				Location = location,
				DisplayTemplate = $"%lang_fact_task_done%", // 語系包: "Completed a task at {location}"
				ExtraData = new Dictionary<string, string> { { "taskId", taskId } }
			};
		}

		public static FactCard CreateBodyFound( ulong finder, ulong victim, string location, float timestamp )
		{
			return new FactCard
			{
				CardId = GenerateId(),
				Type = FactType.BodyFound,
				Timestamp = timestamp,
				OwnerSteamId = finder,
				InvolvedPlayers = new List<ulong> { victim },
				Location = location,
				DisplayTemplate = $"%lang_fact_body_found%" // 語系包: "Found %victim%'s body at {location}"
			};
		}
	}
}

