using Sandbox;
using System;
using System.Linq;

namespace Trce.Plugins.Shared.Map
{
	/// <summary>
	/// TRCE ?氱敤瑙哥櫦??
	/// </summary>
	[Title( "TRCE Trigger" ), Icon( "settings_input_component" )]
	public class TrceTrigger : Component, Component.ITriggerListener
	{
		[Property] public Action<GameObject> OnEnter { get; set; }
		[Property] public Action<GameObject> OnExit { get; set; }
		[Property] public string TargetTag { get; set; } = "player";

		void ITriggerListener.OnTriggerEnter( Collider other )
		{
			if ( string.IsNullOrEmpty( TargetTag ) || other.GameObject.Tags.Has( TargetTag ) )
			{
				OnEnter?.Invoke( other.GameObject );
			}
		}

		void ITriggerListener.OnTriggerExit( Collider other )
		{
			if ( string.IsNullOrEmpty( TargetTag ) || other.GameObject.Tags.Has( TargetTag ) )
			{
				OnExit?.Invoke( other.GameObject );
			}
		}
	}
}

