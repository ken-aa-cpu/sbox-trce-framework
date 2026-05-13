// File: Code/Kernel/Stats/TrceStatPlugin.cs
// Encoding: UTF-8 (No BOM)
// Phase 2: IAttributeService core implementation — dirty-flag caching, Zero-GC calculation path, hot-reload safety.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox;
using Trce.Kernel.Event;
using Trce.Kernel.Plugin;

namespace Trce.Kernel.Stats;

/// <summary>
/// 【Phase 2 — TRCE Universal Attribute Service Implementation (Stat Plugin)】
/// </summary>
[TrcePlugin( Id = "trce.stats", Name = "TRCE Stat System", Version = "2.0.0", Author = "TRCE Team" )]
[Icon( "bar_chart" )]
[Title( "TRCE Stat Plugin" )]
public sealed class TrceStatPlugin : TrcePlugin, IAttributeService
{
	private sealed class AttrState
	{
		public float BaseValue;
		public readonly Dictionary<Guid, AttributeModifier> Modifiers = new();
		public float CachedTotal;
		public bool IsDirty = true;
	}

	// No static fields — guaranteed to be GC-collected after hot-reload with zero residual state.
	private readonly Dictionary<ulong, Dictionary<string, AttrState>> _entityData = new();

	protected override Task OnPluginEnabled()
	{
		TrceServiceManager.Instance?.RegisterService<IAttributeService>( this );
		return Task.CompletedTask;
	}

	protected override void OnPluginDisabled()
	{
		// Failsafe core: forcibly clear all entity attribute caches to ensure zero residual state after hot-reload.
		_entityData.Clear();
		TrceServiceManager.Instance?.UnregisterService<IAttributeService>();
	}

	public float GetTotalValue( ulong steamId, string attrId )
	{
		if ( !_entityData.TryGetValue( steamId, out var attrMap ) )
			return 0f;

		if ( !attrMap.TryGetValue( attrId, out var state ) )
			return 0f;

		if ( !state.IsDirty )
			return state.CachedTotal;

		state.CachedTotal = CalculateTotal( state );
		state.IsDirty     = false;
		return state.CachedTotal;
	}

	public void SetBaseValue( ulong steamId, string attrId, float value )
	{
		var state = GetOrCreateState( steamId, attrId );

		if ( state.BaseValue == value )
			return;

		float oldTotal = state.IsDirty ? CalculateTotal( state ) : state.CachedTotal;
		state.BaseValue = value;

		float newTotal    = CalculateTotal( state );
		state.CachedTotal = newTotal;
		state.IsDirty     = false;

		if ( oldTotal != newTotal )
			PublishAttributeChanged( steamId, attrId, oldTotal, newTotal );
	}

	public Guid AddModifier( ulong steamId, string attrId, AttributeModifier modifier )
	{
		var state      = GetOrCreateState( steamId, attrId );
		var modifierId = Guid.NewGuid();

		float oldTotal = state.IsDirty ? CalculateTotal( state ) : state.CachedTotal;

		state.Modifiers[modifierId] = modifier;

		float newTotal    = CalculateTotal( state );
		state.CachedTotal = newTotal;
		state.IsDirty     = false;

		if ( oldTotal != newTotal )
			PublishAttributeChanged( steamId, attrId, oldTotal, newTotal );

		return modifierId;
	}

	public void RemoveModifier( ulong steamId, string attrId, Guid modifierId )
	{
		if ( !_entityData.TryGetValue( steamId, out var attrMap ) )
			return;

		if ( !attrMap.TryGetValue( attrId, out var state ) )
			return;

		float oldTotal = state.IsDirty ? CalculateTotal( state ) : state.CachedTotal;

		if ( !state.Modifiers.Remove( modifierId ) )
			return;

		float newTotal    = CalculateTotal( state );
		state.CachedTotal = newTotal;
		state.IsDirty     = false;

		if ( oldTotal != newTotal )
			PublishAttributeChanged( steamId, attrId, oldTotal, newTotal );
	}

	private AttrState GetOrCreateState( ulong steamId, string attrId )
	{
		if ( !_entityData.TryGetValue( steamId, out var attrMap ) )
		{
			attrMap = new Dictionary<string, AttrState>( StringComparer.Ordinal );
			_entityData[steamId] = attrMap;
		}

		if ( !attrMap.TryGetValue( attrId, out var state ) )
		{
			state           = new AttrState();
			attrMap[attrId] = state;
		}

		return state;
	}

	private static float CalculateTotal( AttrState state )
	{
		float addSum     = state.BaseValue;
		float mulProduct = 1f;

		// Zero-GC core iteration: uses Dictionary Struct Enumerator — LINQ is strictly forbidden.
		foreach ( var mod in state.Modifiers.Values )
		{
			if ( mod.Type == ModifierType.Add )
				addSum += mod.Value;
			else
				mulProduct *= mod.Value;
		}

		return addSum * mulProduct;
	}

	private static void PublishAttributeChanged( ulong steamId, string attrId, float oldValue, float newValue )
	{
		GlobalEventBus.Publish( new CoreEvents.AttributeChangedEvent(
			steamId, attrId, oldValue, newValue
		) );
	}
}
