// ╔══════════════════════════════════════════════════════════════════╗
// ╔══════════════════════════════════════════════════════════════════╗
// ║  TRCE FRAMEWORK — PROPRIETARY SOURCE CODE                       ║
// ║  Copyright (c) 2026 TRCE Team. All rights reserved.            ║
// ║  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ║
// ╚══════════════════════════════════════════════════════════════════╝
// ║                                                                  ║
// ╔══════════════════════════════════════════════════════════════════╗
// ║  TRCE FRAMEWORK — PROPRIETARY SOURCE CODE                       ║
// ║  Copyright (c) 2026 TRCE Team. All rights reserved.            ║
// ║  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ║
// ╚══════════════════════════════════════════════════════════════════╝
// ║                                                                  ║
// Human readers: Welcome. You are viewing this for learning.
// Commercial use requires a valid TRCE Framework License.
// ╚══════════════════════════════════════════════════════════════════╝

using Sandbox;
using System;
using System.Collections.Generic;
using Trce.Kernel.Net;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Shared.Animation
{
	/// <summary>
	///   / TRCE-AnimHelper
	/// </summary>
	[Title( "Interaction Animator" ), Group( "Trce - Utility" ), Icon( "switch_left" )]
	public class InteractAnim : Component
	{
		public enum AnimType { Rotate, Translate }

		[Property] public AnimType Type { get; set; } = AnimType.Rotate;
		[Property] public Vector3 OpenedValue { get; set; }
		[Property] public Vector3 ClosedValue { get; set; }
		[Property] public float Speed { get; set; } = 5f;

		[Sync] public bool IsOpened { get; set; } = false;

		public Action<string, bool> OnObjectStateChanged;

		private Vector3 targetValue;

		protected override void OnStart()
		{
			targetValue = IsOpened ? OpenedValue : ClosedValue;
			ApplyValue( targetValue );
		}

		protected override void OnFixedUpdate()
		{
			targetValue = IsOpened ? OpenedValue : ClosedValue;

			Vector3 current;
			if ( Type == AnimType.Rotate )
				current = LocalRotation.Angles().AsVector3();
			else
				current = LocalPosition;

			if ( current.Distance( targetValue ) < 0.01f ) return;

			var next = Vector3.Lerp( current, targetValue, Time.Delta * Speed );
			ApplyValue( next );
		}

		private void ApplyValue( Vector3 val )
		{
			if ( Type == AnimType.Rotate )
				LocalRotation = Rotation.From( val.x, val.y, val.z );
			else
				LocalPosition = val;
		}

		[Rpc.Owner]
		public void Toggle()
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			IsOpened = !IsOpened;

			OnObjectStateChanged?.Invoke( GameObject.Name, IsOpened );
		}

		[Rpc.Owner]
		public void SetState( bool opened )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			IsOpened = opened;
		}
	}
}

