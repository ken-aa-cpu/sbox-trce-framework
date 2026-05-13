// File: Code/Kernel/Stats/IAttributeService.cs
// Encoding: UTF-8 (No BOM)
// Phase 2: Universal attribute service contract — TRCE Numeric Socket System.

using System;

namespace Trce.Kernel.Stats;

/// <summary>
/// <para>【Phase 2 — Universal Attribute Service Public Contract (Numeric Socket)】</para>
/// <para>
/// Provides a "Numeric Socket" pattern for a generic entity attribute system. Any plugin can
/// define floating-point attributes for entities (e.g. <c>"player.move_speed"</c>,
/// <c>"player.max_health"</c>) and layer or modify values freely via <see cref="AttributeModifier"/>
/// without touching core code, achieving a fully decoupled numeric system.
/// </para>
/// <para>
/// <b>Core calculation formula:</b><br/>
/// <c>Final value = (Base value + Σ all Add-type modifiers) × Π all Multiply-type modifiers</c>
/// </para>
/// <para>
/// <b>Performance guarantee:</b><br/>
/// Implementations must use a dirty-flag caching mechanism so that <see cref="GetTotalValue"/>
/// is O(1) when no modifiers have changed since the last computation.
/// </para>
/// </summary>
public interface IAttributeService
{
	/// <summary>
	/// Computes and returns the final value of the specified entity attribute.
	/// <para>
	/// <b>【Performance guarantee — O(1) cache hit】:</b> The internal implementation uses a
	/// dirty-flag cache. If no modifiers have changed since the last computation, this call
	/// returns the cached value with zero recalculation overhead.
	/// </para>
	/// </summary>
	float GetTotalValue( ulong steamId, string attrId );

	/// <summary>
	/// Sets the base value (Base Value) of the specified entity attribute
	/// and fires an attribute-changed event when the value actually changes.
	/// </summary>
	void SetBaseValue( ulong steamId, string attrId, float value );

	/// <summary>
	/// Adds an <see cref="AttributeModifier"/> to the specified entity attribute
	/// and returns a unique identifier that can be used to remove it later.
	/// </summary>
	Guid AddModifier( ulong steamId, string attrId, AttributeModifier modifier );

	/// <summary>
	/// Removes the <see cref="AttributeModifier"/> identified by <paramref name="modifierId"/>
	/// from the specified entity attribute.
	/// </summary>
	void RemoveModifier( ulong steamId, string attrId, Guid modifierId );
}
