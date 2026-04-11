using Sandbox;
using System;

namespace Trce.Kernel.Plugin.Pawn

{

	public class PawnController : Component
	{
		[Property] public CharacterController Controller { get; set; }
		[Property] public float MoveSpeed { get; set; } = 150f;

		private TrcePawn pawn;
		protected override void OnStart()
		{
			pawn = Components.Get<TrcePawn>();

			// 憒??舀?啁摰塚????璈?
			if ( IsProxy == false ) // Network.IsOwner is false means a proxy
			{
			}

		}

		protected override void OnFixedUpdate()
		{
			if ( IsProxy ) return; // ?芣? Owner ??批蝘餃?
			var angles = Scene.Camera.WorldRotation.Angles();
			angles.pitch = 0;

			var move = Input.AnalogMove;
			var worldMove = Rotation.From( angles ) * move;
			if ( Controller != null )
			{
				Controller.Accelerate( worldMove * MoveSpeed );
				Controller.ApplyFriction( 5.0f );
				Controller.Move();
			}

			// ?湔?
			if ( pawn != null )
			{
				pawn.SetAnimParameter( "move_x", move.x );
				pawn.SetAnimParameter( "move_y", move.y );
				pawn.SetAnimParameter( "is_grounded", Controller?.IsOnGround ?? true );
			}

		}

	}

}


