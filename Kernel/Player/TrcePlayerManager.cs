using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Bridge;
using Trce.Kernel.Net;
using Trce.Kernel.Plugin.Services;
using Trce.Kernel.Event;

namespace Trce.Kernel.Player

{
	/// <summary>
	///   TRCE 玩家管理系統：全局管理所有玩家狀態 (GameObjectSystem)
	/// </summary>
	public class TrcePlayerManager : GameObjectSystem, ISceneStartup
	{
		public static TrcePlayerManager Instance { get; private set; }

		private readonly Dictionary<ulong, TrcePlayerState> _registry = new();

		// 事件定義保持不變，但改為靜態或透過 Instance 存取
		public event System.Action<TrcePlayerState> OnPlayerJoined;
		public event System.Action<ulong> OnPlayerLeft;
		public event System.Action<TrcePlayerState, float, float> OnHealthChanged;
		public event System.Action<TrcePlayerState, ulong> OnPlayerDied;
		public event System.Action<TrcePlayerState> OnPlayerRevived;
		public event System.Action<TrcePlayerState> OnRoleAssigned;
		public event System.Action<TrcePlayerState, string, string> OnZoneChanged;
		public event System.Action<TrcePlayerState, string> OnPlayerDataChanged;
		public event System.Action OnRoundReset;

		public TrcePlayerManager( Scene scene ) : base( scene )
		{
			Instance = this;
		}

		public void OnSceneStartup()
		{
			GlobalEventBus.Subscribe<CoreEvents.ClientReadyEvent>( HandlePlayerConnected );
			GlobalEventBus.Subscribe<CoreEvents.ClientDisconnectedEvent>( HandlePlayerDisconnected );
			Log.Info( "[TrcePlayerManager] System initialized via GameObjectSystem." );
		}

		private void HandlePlayerConnected( CoreEvents.ClientReadyEvent e ) => OnPlayerConnected( e.SteamId, e.DisplayName );
		private void HandlePlayerDisconnected( CoreEvents.ClientDisconnectedEvent e ) => OnPlayerDisconnected( e.SteamId );

		public IReadOnlyList<ulong> GetAllPlayerIds() => _registry.Keys.ToList();
		public string GetDisplayName( ulong steamId ) => _registry.TryGetValue( steamId, out var state ) ? state.DisplayName : "Unknown";
		public bool IsOnline( ulong steamId ) => _registry.ContainsKey( steamId );
		
		public IReadOnlyList<ulong> GetTeamPlayers( string teamId )
			=> _registry.Values.Where( p => p.TeamId == teamId ).Select( p => p.SteamId ).ToList();

		public void OnPlayerConnected( ulong steamId, string displayName )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			if ( _registry.ContainsKey( steamId ) ) return;

			var state = new TrcePlayerState
			{
				SteamId = steamId,
				DisplayName = displayName,
				JoinTime = Time.NowDouble,
				LastModifiedTime = Time.NowDouble
			};
			_registry[steamId] = state;
			try { OnPlayerJoined?.Invoke( state ); } catch ( Exception ex ) { Log.Error( ex ); }
		}

		public void OnPlayerDisconnected( ulong steamId )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			if ( !_registry.TryGetValue( steamId, out var state ) ) return;
			try { OnPlayerLeft?.Invoke( steamId ); } catch ( Exception ex ) { Log.Error( ex ); }
			_registry.Remove( steamId );
		}

		public void SetHealth( ulong steamId, float newHealth )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) || !_registry.TryGetValue( steamId, out var state ) ) return;
			float old = state.Health;
			state.Health = Math.Clamp( newHealth, 0f, state.MaxHealth );
			state.LastModifiedTime = Time.NowDouble;
			try { OnHealthChanged?.Invoke( state, old, state.Health ); } catch ( Exception ex ) { Log.Error( ex ); }
			if ( state.Health <= 0f && state.IsAlive )
				SetAliveState( steamId, AliveState.Dead, 0 );
		}

		public void SetAliveState( ulong steamId, AliveState newState, ulong killerId = 0 )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) || !_registry.TryGetValue( steamId, out var state ) ) return;
			state.AliveState = newState;
			state.LastModifiedTime = Time.NowDouble;
			if ( newState == AliveState.Dead )
			{
				try { OnPlayerDied?.Invoke( state, killerId ); } catch ( Exception ex ) { Log.Error( ex ); }
			}
			else if ( newState == AliveState.Alive )
			{
				state.Health = state.MaxHealth;
				try { OnPlayerRevived?.Invoke( state ); } catch ( Exception ex ) { Log.Error( ex ); }
			}
		}

		public void SetRole( ulong steamId, string roleId, string teamId )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) || !_registry.TryGetValue( steamId, out var state ) ) return;
			state.RoleId = roleId;
			state.TeamId = teamId;
			state.LastModifiedTime = Time.NowDouble;
			try { OnRoleAssigned?.Invoke( state ); } catch ( Exception ex ) { Log.Error( ex ); }
		}

		public TrcePlayerState GetPlayer( ulong steamId ) => _registry.TryGetValue( steamId, out var s ) ? s : null;
		public IReadOnlyCollection<TrcePlayerState> GetAllPlayers() => _registry.Values;

		public void ResetForNewRound()
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			foreach ( var state in _registry.Values )
			{
				state.AliveState = AliveState.Alive;
				state.Health = state.MaxHealth;
				state.RoleId = "";
				state.TeamId = "";
				state.ServerData.Clear();
				state.LastModifiedTime = Time.NowDouble;
			}
			try { OnRoundReset?.Invoke(); } catch ( Exception ex ) { Log.Error( ex ); }
		}
	}

}

