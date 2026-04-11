using System.Threading.Tasks;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Net;
using Trce.Kernel.Papi;
using Trce.Kernel.Plugin;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Pawn
{
	/// <summary>
	///   / Effect Data Container
	/// </summary>
	public class TrceEffectData
	{
		public string EffectId { get; set; }
		public string DisplayName { get; set; }
		public string IconPath { get; set; }
		public bool IsBuff { get; set; } = true;
		public float DurationSec { get; set; } = 10f;

		/// <summary> Stat Modifiers (StatKey, (Value, Type))</summary>
		public Dictionary<string, (float value, ModifierType type)> StatModifiers { get; set; } = new();

		/// <summary> Effect Tags e.g. "Fire", "Poison"</summary>
		public List<string> Tags { get; set; } = new();
	}

	/// <summary>
	///   / Runtime Active Effect instance
	/// </summary>
	public class ActiveEffect
	{
		public string InstanceId { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
		public TrceEffectData Data { get; set; }
		public float EndTime { get; set; }
		public int Stacks { get; set; } = 1;

		public bool IsExpired => EndTime > 0 && Time.Now >= EndTime;
	}

	/// <summary>
	///   / TRCE Player Effects Manager (Buff / Debuff System)
	///   / handles registration, application, and ticking of effects.
	/// </summary>
	[TrcePlugin(
		Id = "trce.pawn.effects",
		Name = "TRCE Player Effects System",
		Version = "1.0.0",
		Depends = new[] { "trce.pawn.stats" }
	)]
	public class TrcePlayerEffectsManager : TrcePlugin, ITrcePlaceholderProvider
	{
		public string ProviderId => "effects";

		/// <summary> Player Effects map [SteamId -> List of ActiveEffects]</summary>
		private Dictionary<ulong, List<ActiveEffect>> playerEffects = new();

		/// <summary> Registered effect templates</summary>
		private Dictionary<string, TrceEffectData> effectTemplates = new();

		private TimeSince timeSinceCleanup;

		public Action<ulong, string> OnEffectApplied;
		public Action<ulong, string> OnEffectRemoved;
		public Action<ulong, string> OnEffectExpired;

		protected override async Task OnPluginEnabled()
		{
			PlaceholderAPI.For( this )?.RegisterProvider( this );

			// Register Default Templates
			RegisterTemplate( new TrceEffectData
			{
				EffectId = "regeneration",
				DisplayName = "Regeneration",
				IsBuff = true,
				DurationSec = 15f,
				StatModifiers = new() { { "vitality", (5f, ModifierType.Flat) } },
				Tags = new() { "heal" }
			});

			RegisterTemplate( new TrceEffectData
			{
				EffectId = "slowness",
				DisplayName = "Slowness",
				IsBuff = false,
				DurationSec = 5f,
				StatModifiers = new() { { "agility", (-30f, ModifierType.Percent) } },
				Tags = new() { "movement", "debuff" }
			});
		}

		protected override void OnPluginDisabled()
		{
			PlaceholderAPI.For( this )?.UnregisterProvider( this );
		}

		protected override void OnFixedUpdate()
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;

			if ( timeSinceCleanup >= 1f )
			{
				timeSinceCleanup = 0f;
				CleanupExpiredEffects();
			}
		}

		// ====================================================================
		// Template Management
		// ====================================================================

		public void RegisterTemplate( TrceEffectData data )
		{
			effectTemplates[data.EffectId] = data;
			Log.Info( $"[Effects:{GameObject.Name}] Registered effect: {data.EffectId}" );
		}

		// ====================================================================
		// Apply / Remove Logic
		// ====================================================================

		public void ApplyEffect( ulong steamId, string effectId, int stacks = 1 )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			if ( !effectTemplates.TryGetValue( effectId, out var template ) )
			{
				Log.Warning( $"[Effects:{GameObject.Name}] Effect template not found: {effectId}" );
				return;
			}

			if ( !playerEffects.ContainsKey( steamId ) )
				playerEffects[steamId] = new List<ActiveEffect>();

			var list = playerEffects[steamId];
			var existing = list.FirstOrDefault( e => e.Data.EffectId == effectId );

			if ( existing != null )
			{
				existing.Stacks += stacks;
				if ( template.DurationSec > 0 )
					existing.EndTime = Time.Now + template.DurationSec;
			}
			else
			{
				var newEffect = new ActiveEffect
				{
					Data = template,
					Stacks = stacks,
					EndTime = template.DurationSec > 0 ? Time.Now + template.DurationSec : -1f
				};
				list.Add( newEffect );

				var stats = GetPlugin<TrcePlayerStats>();
				if ( stats != null )
				{
					foreach ( var (key, mod) in template.StatModifiers )
					{
						stats.AddModifier( new StatModifier
						{
							StatKey = key,
							Value = mod.value,
							Type = mod.type,
							Source = $"effect_{newEffect.InstanceId}"
						} );
					}
				}
			}

			OnEffectApplied?.Invoke( steamId, effectId );

			PlaceholderAPI.For( this )?.InvalidateCache();
		}

		public void RemoveEffect( ulong steamId, string effectId )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) || !playerEffects.TryGetValue( steamId, out var list ) ) return;

			var effectsToRemove = list.Where( e => e.Data.EffectId == effectId ).ToList();
			if ( effectsToRemove.Count == 0 ) return;

			var stats = GetPlugin<TrcePlayerStats>();

			foreach ( var eff in effectsToRemove )
			{
				list.Remove( eff );
				if ( stats != null )
				{
					stats.RemoveModifier( $"effect_{eff.InstanceId}" );
				}
			}

			OnEffectRemoved?.Invoke( steamId, effectId );

			PlaceholderAPI.For( this )?.InvalidateCache();
		}

		private void CleanupExpiredEffects()
		{
			var stats = GetPlugin<TrcePlayerStats>();
			bool updated = false;

			foreach ( var (steamId, list) in playerEffects )
			{
				for ( int i = list.Count - 1; i >= 0; i-- )
				{
					var eff = list[i];
					if ( eff.IsExpired )
					{
						list.RemoveAt( i );
						if ( stats != null )
						{
							stats.RemoveModifier( $"effect_{eff.InstanceId}" );
						}

						OnEffectExpired?.Invoke( steamId, eff.Data.EffectId );

						updated = true;
					}
				}
			}

			if ( updated )
			{
				PlaceholderAPI.For( this )?.InvalidateCache();
			}
		}

		// ====================================================================
		//  Queries
		// ====================================================================

		public bool HasEffectTag( ulong steamId, string tag )
		{
			if ( !playerEffects.TryGetValue( steamId, out var list ) ) return false;
			return list.Any( e => e.Data.Tags.Contains( tag ) );
		}

		// ====================================================================
		//  PAPI Support
		// ====================================================================

		public string TryResolvePlaceholder( string key )
		{
			ulong steamId = Connection.Local?.SteamId ?? 0ul;
			if ( steamId == 0ul ) return null;

			if ( key == "effects_my_list" )
			{
				if ( !playerEffects.TryGetValue( steamId, out var list ) || list.Count == 0 )
					return "No Effects";

				return string.Join( ", ", list.Select( e => $"{(e.Data.IsBuff ? "&a" : "&c")}{e.Data.DisplayName}&r" ) );
			}

			return null;
		}
	}
}

