using Sandbox;
using System.Collections.Generic;

namespace Trce.Kernel.Action
{
	/// <summary>
	///   定義一個動作映射資源。
	///   將語義化的動作名稱（如 "PrimaryAttack"）映射到具體的動畫參數。
	/// </summary>
	[GameResource( "TRCE Action Mapping", "trcemap", "Maps an action name to animator parameters." )]
	public class ActionMapping : GameResource
	{
		[Property] public string ActionId { get; set; } = "new_action";

		[Property, Category( "Animator" )] 
		public List<ActionParameter> Parameters { get; set; } = new();

		public struct ActionParameter
		{
			public string Name { get; set; }
			public ParameterType Type { get; set; }
			public float Value { get; set; }
			public bool BoolValue { get; set; }
			public int IntValue { get; set; }
		}

		public enum ParameterType
		{
			Trigger,
			Bool,
			Float,
			Int
		}
	}
}
