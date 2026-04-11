// ?â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â?
// ùÝùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùß
// ùø  TRCE FRAMEWORK ¡X PROPRIETARY SOURCE CODE                       ùø
// ùø  Copyright (c) 2026 TRCE Team. All rights reserved.            ùø
// ùø  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ùø
// ùãùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùùå
// ?â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â??â?
using Trce.Kernel.Bridge;
using Sandbox;
using Sandbox.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Player;
using Trce.Kernel.Plugin;

using Trce.Kernel.Plugin.Services;

namespace Trce.Plugins.Combat
{
	[Title( "Death Manager" ), Group( "Trce - Modules" )]
	public class DeathManager : TrcePlugin
	{

		// Local enum removed, using Trce.Kernel.Plugin.Services.AliveState

		public class LifeEventContext
		{
			public ulong VictimId { get; set; }
			public ulong InstigatorId { get; set; }
			public Vector3 Location { get; set; }
			public AliveState TargetState { get; set; }
			public string Cause { get; set; }
			public bool Cancelled { get; set; }
		}

		public interface ILifeEventProcessor { void OnPreProcess( DeathManager m, LifeEventContext c ); void OnPostProcess( DeathManager m, LifeEventContext c ); }

		[Property] private List<ILifeEventProcessor> processors = new();

		private Dictionary<ulong, AliveState> playerStates = new();

		public Action<ulong, ulong> OnPlayerKilled;
		public Action<ulong, ulong, Vector3, string> OnPlayerDowned;
		public Action<ulong, ulong> OnPlayerRevived;
		public Action<ulong> OnPlayerExecuted;
		public Action<ulong> OnPlayerEvacuated;

		public Action OnAllKillersDead;
		public Action OnAllCrewDead;
		public Action OnAllCrewEvacuated;

		protected override void OnAwake()
		{
			if ( processors.Count == 0 ) processors.Add( new DefaultLifeProcessor() );
		}

		public bool Execute( LifeEventContext ctx )
		{
			if ( ctx == null ) return false;
			foreach ( var p in processors ) p.OnPreProcess( this, ctx );
			if ( ctx.Cancelled ) return false;

			playerStates[ctx.VictimId] = ctx.TargetState;
			Scene.Get<TrcePlayerManager>()?.SetAliveState( ctx.VictimId, ctx.TargetState, ctx.InstigatorId );

			foreach ( var p in processors ) p.OnPostProcess( this, ctx );
			if ( (SandboxBridge.Instance?.IsServer ?? false) ) CheckWinConditions();
			return true;
		}

		private void CheckWinConditions()
		{
			var roles = Scene.GetAllComponents<Trce.Plugins.Shared.Roles.RoleRegistry>().FirstOrDefault();
			if ( roles == null ) return;

			var killers = roles.GetAllKillers();
			var crew = roles.GetAllCrew();

			bool anyKillerAlive = killers.Any( id => IsAlive( id ) || IsDowned( id ) );
			bool anyCrewAlive = crew.Any( id => IsAlive( id ) || IsDowned( id ) );
			bool anyCrewRemaining = crew.Any( id => !IsDeadOrGone( id ) );

			if ( killers.Count > 0 && !anyKillerAlive ) OnAllKillersDead?.Invoke();
			else if ( crew.Count > 0 && !anyCrewAlive ) OnAllCrewDead?.Invoke();
			else if ( crew.Count > 0 && !anyCrewRemaining )
			{
				bool allCrewGone = crew.All( id => GetState( id ) == AliveState.Dead || GetState( id ) == AliveState.Executed || GetState( id ) == AliveState.Evacuated );
				if ( allCrewGone && crew.Any( id => GetState( id ) == AliveState.Evacuated ) ) OnAllCrewEvacuated?.Invoke();
			}
		}

		public void ProcessDeath( ulong victim, ulong killer, Vector3 loc, string cause = "killed" ) => Execute( new LifeEventContext { VictimId = victim, InstigatorId = killer, Location = loc, TargetState = AliveState.Dead, Cause = cause } );
		public void ProcessDowned( ulong victim, ulong attacker, Vector3 loc ) => Execute( new LifeEventContext { VictimId = victim, InstigatorId = attacker, Location = loc, TargetState = AliveState.Downed, Cause = "downed" } );
		public void ProcessRevive( ulong target, ulong reviver ) => Execute( new LifeEventContext { VictimId = target, InstigatorId = reviver, TargetState = AliveState.Alive, Cause = "revive" } );
		public void ProcessExecution( ulong target ) => Execute( new LifeEventContext { VictimId = target, TargetState = AliveState.Executed, Cause = "execution" } );
		public void ProcessEvacuation( ulong steamId ) => Execute( new LifeEventContext { VictimId = steamId, TargetState = AliveState.Evacuated, Cause = "evacuation" } );

		public AliveState GetState( ulong id ) => playerStates.TryGetValue( id, out var s ) ? s : AliveState.Alive;
		public bool IsAlive( ulong id ) => GetState( id ) == AliveState.Alive;
		public bool IsGhost( ulong id ) => GetState( id ) == AliveState.Dead;
		public bool IsDowned( ulong id ) => GetState( id ) == AliveState.Downed;
		public bool IsDeadOrGone( ulong id ) { var s = GetState( id ); return s == AliveState.Dead || s == AliveState.Executed; }
		public bool IsAlreadyDead( ulong id ) => GetState( id ) == AliveState.Dead;

		public void RegisterPlayer( ulong id ) => playerStates[id] = AliveState.Alive;
		public void ResetAll() => playerStates.Clear();
		public HashSet<ulong> GetAliveSteamIds() => new( playerStates.Where( kvp => kvp.Value == AliveState.Alive ).Select( kvp => kvp.Key ) );

		public class DefaultLifeProcessor : ILifeEventProcessor
		{
			public void OnPreProcess( DeathManager m, LifeEventContext c ) { }
			public void OnPostProcess( DeathManager m, LifeEventContext c )
			{
				switch ( c.TargetState )
				{
					case AliveState.Dead: m.OnPlayerKilled?.Invoke( c.VictimId, c.InstigatorId ); break;
					case AliveState.Downed: m.OnPlayerDowned?.Invoke( c.VictimId, c.InstigatorId, c.Location, c.Cause ); break;
					case AliveState.Alive: m.OnPlayerRevived?.Invoke( c.VictimId, c.InstigatorId ); break;
					case AliveState.Executed: m.OnPlayerExecuted?.Invoke( c.VictimId ); break;
					case AliveState.Evacuated: m.OnPlayerEvacuated?.Invoke( c.VictimId ); break;
				}
			}
		}
	}
}

