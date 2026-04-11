using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Papi;
using Trce.Kernel.Plugin;

namespace Trce.Plugins.Pawn
{
	/// <summary>
	///   / TRCE Stat Definition Resource
	/// </summary>
	[GameResource( "TRCE Stat Definition", "trcestat", "TRCE Stat Definition", Icon = "bar_chart" )]
	public class TrceStatDefinition : GameResource
	{
		[Property] public string StatKey { get; set; } = "health";
		[Property] public string DisplayName { get; set; } = "Stat Name";
		[Property] public float DefaultBase { get; set; } = 100f;
		[Property] public float MinValue { get; set; } = 0f;
		[Property] public float MaxValue { get; set; } = 9999f;
	}

	public enum ModifierType { Flat, Percent }

	/// <summary> Stat Modifier Container (Buff/Debuff)</summary>
	public class StatModifier
	{
		public string StatKey { get; set; }
		public float Value { get; set; }
		public ModifierType Type { get; set; } = ModifierType.Flat;
		public string Source { get; set; }
		public float Duration { get; set; } = -1; // -1 = Permanent, >0 = Seconds
	}

	/// <summary>
	///   / TRCE Player Stats Plugin
	/// </summary>
	[TrcePlugin(
		Id = "trce.pawn.stats",
		Name = "TRCE Player Stats",
		Version = "2.1.0"
	)]
	public class TrcePlayerStats : TrcePlugin, ITrcePlaceholderProvider
	{
		public string ProviderId => "ext_stats";

		private Dictionary<string, TrceStatDefinition> definitions = new();
		private Dictionary<string, float> baseStats = new();

		private List<StatModifier> modifiers = new();

		private bool isDirty = true;
		private Dictionary<string, float> cachedFinalStats = new();

		protected override void OnStart()
		{
			LoadDefinitions();
			PlaceholderAPI.For( this )?.RegisterProvider( this );
		}

		protected override void OnPluginDisabled()
		{
			PlaceholderAPI.For( this )?.UnregisterProvider( this );
		}

		private void LoadDefinitions()
		{
			var defs = ResourceLibrary.GetAll<TrceStatDefinition>();
			foreach ( var def in defs )
			{
				if ( string.IsNullOrEmpty( def.StatKey ) ) continue;
				definitions[def.StatKey] = def;
				baseStats[def.StatKey] = def.DefaultBase;
			}

			if ( definitions.Count == 0 )
			{
				Log.Warning( $"[TrceStats:{GameObject.Name}] No StatDefinitions found, using defaults." );
				baseStats["vitality"] = 100f;
				baseStats["speed"] = 200f;
				baseStats["strength"] = 10f;
				baseStats["agility"] = 10f;
				baseStats["intelligence"] = 10f;
			}

			MarkDirty();
		}

		public void SetBaseStat( string key, float value )
		{
			baseStats[key] = value;
			MarkDirty();
		}

		public float GetBaseStat( string key )
		{
			return baseStats.GetValueOrDefault( key, 0f );
		}

		/// <summary> Get Final Decided Stat Value</summary>
		public float GetStat( string key )
		{
			if ( isDirty ) RecalculateAll();
			return cachedFinalStats.GetValueOrDefault( key, 0f );
		}

		private void RecalculateAll()
		{
			cachedFinalStats.Clear();

			foreach ( var kvp in baseStats )
			{
				string key = kvp.Key;
				float baseVal = kvp.Value;

				float flat = 0; float pct = 0;
				foreach ( var m in modifiers )
				{
					if ( m.StatKey != key ) continue;
					if ( m.Type == ModifierType.Flat ) flat += m.Value;
					else pct += m.Value;
				}

				float finalVal = (baseVal + flat) * (1f + pct / 100f);

				if ( definitions.TryGetValue( key, out var def ) )
				{
					finalVal = Math.Clamp( finalVal, def.MinValue, def.MaxValue );
				}

				cachedFinalStats[key] = finalVal;
			}

			isDirty = false;
		}

		private void MarkDirty()
		{
			isDirty = true;
			PlaceholderAPI.For( this )?.InvalidateCache();
		}

		public void AddModifier( StatModifier mod )
		{
			modifiers.Add( mod );
			MarkDirty();
		}

		public void RemoveModifier( string source )
		{
			modifiers.RemoveAll( m => m.Source == source );
			MarkDirty();
		}

		public string TryResolvePlaceholder( string key )
		{
			const string prefix = "ext_stats_";
			if ( key.StartsWith( prefix ) )
			{
				string statKey = key.Substring( prefix.Length );
				if ( baseStats.ContainsKey( statKey ) )
				{
					return GetStat( statKey ).ToString( "F1" );
				}
			}
			return null;
		}
	}
}


