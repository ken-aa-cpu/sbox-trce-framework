using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace Trce.Kernel.Action
{
	/// <summary>
	///   ActionEngine 負責解析語義化的動作並將其應用於動畫器。
	///   這實現了「邏輯與動畫解耦」的核心目標。
	/// </summary>
	public class ActionEngine : Component
	{
		[Property] public List<ActionMapping> Mappings { get; set; } = new();
		[Property] public SkinnedModelRenderer Renderer { get; set; }

		/// <summary>
		///   根據動作 ID 執行映射的動畫參數。
		/// </summary>
		public void RunAction( string actionId )
		{
			if ( Renderer == null ) return;

			var mapping = Mappings.FirstOrDefault( m => m.ActionId == actionId );
			if ( mapping == null )
			{
				Log.Warning( $"[ActionEngine] No mapping found for action: {actionId}" );
				return;
			}

			foreach ( var param in mapping.Parameters )
			{
				switch ( param.Type )
				{
					case ActionMapping.ParameterType.Trigger:
						Renderer.Set( param.Name, true ); // S&box triggers are often just bools set to true
						break;
					case ActionMapping.ParameterType.Bool:
						Renderer.Set( param.Name, param.BoolValue );
						break;
					case ActionMapping.ParameterType.Float:
						Renderer.Set( param.Name, param.Value );
						break;
					case ActionMapping.ParameterType.Int:
						Renderer.Set( param.Name, param.IntValue );
						break;
				}
			}
		}
	}
}
