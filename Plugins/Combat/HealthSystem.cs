// ====================================================================
// ╔══════════════════════════════════════════════════════════════════╗
// ║  TRCE FRAMEWORK — PROPRIETARY SOURCE CODE                       ║
// ║  Copyright (c) 2026 TRCE Team. All rights reserved.            ║
// ║  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ║
// ╚══════════════════════════════════════════════════════════════════╝
// ====================================================================

using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Net;
using Trce.Kernel.Player;
using System.Threading.Tasks;
using Trce.Kernel.Plugin;
using Trce.Kernel.Plugin.Services;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Combat
{
	/// <summary>
	///   / TRCE Health+ Enhanced Health Module
	///   / Manages player health and regeneration as a plugin service.
	/// </summary>
	[TrcePlugin(
		Id = "trce.health",
		Name = "TRCE Health System",
		Version = "1.1.0",
		Author = "TRCE Team"
	)]
	[Icon( "favorite" )]
	public class HealthSystem : TrcePlugin, IHealthService
	{

		public Action<ulong, float, string> OnHealed;
		public Action<ulong, float, ulong, string> OnDamaged;

		private Dictionary<ulong, TimeSince> lastDamageTimes = new();

		private Trce.Kernel.Bridge.SandboxBridge _bridge;
		private Trce.Kernel.Bridge.SandboxBridge Bridge => _bridge ??= SandboxBridge.Instance;

		private TrcePlayerManager _playerManager;
		private TrcePlayerManager PlayerManager => _playerManager ??= Scene.Get<TrcePlayerManager>();

		protected override Task OnPluginEnabled()
		{
			return Task.CompletedTask;
		}

		protected override void OnFixedUpdate()
		{
			if ( !(Bridge?.IsServer ?? false) ) return;

			// Global Health Regeneration Logic
			var players = PlayerManager?.GetAllPlayers();
			if ( players == null ) return;

			foreach ( var p in players )
			{
				float regenDelay = 5f;
				float regenRate = 2f;

				if ( lastDamageTimes.TryGetValue( p.SteamId, out var time ) && time > regenDelay )
				{
					if ( p.Health < p.MaxHealth )
					{
						float newHealth = Math.Min( p.MaxHealth, p.Health + regenRate * Time.Delta );
						PlayerManager.SetHealth( p.SteamId, newHealth );
					}
				}
			}
		}

		// ====================================================================
		// IHealthService Implementation
		// ====================================================================

		public float GetHealth( ulong steamId ) => PlayerManager?.GetPlayer( steamId )?.Health ?? 0f;
		public float GetMaxHealth( ulong steamId ) => PlayerManager?.GetPlayer( steamId )?.MaxHealth ?? 100f;

		public float Heal( ulong steamId, float amount, string source = "" )
		{
			if ( !(Bridge?.IsServer ?? false) ) return GetHealth( steamId );
			var p = PlayerManager?.GetPlayer( steamId );
			if ( p == null ) return 0f;

			float newHealth = Math.Min( p.MaxHealth, p.Health + amount );
			PlayerManager.SetHealth( steamId, newHealth );

			OnHealed?.Invoke( steamId, amount, source );

			return p.Health;
		}

		public float Damage( ulong steamId, float amount, ulong attackerId = 0, string cause = "" )
		{
			if ( !(Bridge?.IsServer ?? false) ) return GetHealth( steamId );
			var p = PlayerManager?.GetPlayer( steamId );
			if ( p == null || !p.IsAlive ) return 0f;

			lastDamageTimes[steamId] = 0;
			float newHealth = p.Health - amount;

			PlayerManager.SetHealth( steamId, newHealth );

			OnDamaged?.Invoke( steamId, amount, attackerId, cause );

			return p.Health;
		}

		public void SetHealth( ulong steamId, float amount )
		{
			PlayerManager?.SetHealth( steamId, amount );
		}

		public void SetMaxHealth( ulong steamId, float maxAmount )
		{
			var p = PlayerManager?.GetPlayer( steamId );
			if ( p == null ) return;
			p.MaxHealth = maxAmount;
		}

		public bool IsAlive( ulong steamId ) => PlayerManager?.GetPlayer( steamId )?.IsAlive ?? false;
	}
}

