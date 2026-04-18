// File: Code/Kernel/Event/CoreEvents.cs
// Encoding: UTF-8 (No BOM)
// All payloads are readonly structs — guaranteed Stack allocation, zero GC pressure.

using Sandbox;

namespace Trce.Kernel.Event
{
	/// <summary>
	/// <para>【Phase 2 — Core Event Definitions】</para>
	/// <para>
	/// All events are <c>readonly struct</c> implementing <see cref="ITrceEvent"/>.
	/// Follows the Zero-Allocation principle: payloads reside on the Stack with no Heap allocation.
	/// </para>
	/// </summary>
	public static class CoreEvents
	{
		// ─────────────────────────────────────────────
		//  Combat Events
		// ─────────────────────────────────────────────

		/// <summary>
		/// Published when a player takes damage.
		/// </summary>
		public readonly struct PlayerDamagedEvent : ITrceEvent
		{
			public readonly uint TargetNetworkId;
			public readonly uint AttackerNetworkId;
			public readonly float DamageAmount;

			public PlayerDamagedEvent(uint targetNetworkId, uint attackerNetworkId, float damageAmount)
			{
				TargetNetworkId   = targetNetworkId;
				AttackerNetworkId = attackerNetworkId;
				DamageAmount      = damageAmount;
			}
		}

		/// <summary>
		/// Published when a weapon successfully fires one round (fired once per bullet).
		/// </summary>
		public readonly struct WeaponFiredEvent : ITrceEvent
		{
			public readonly uint OwnerNetworkId;
			public readonly string WeaponId;
			public readonly int CurrentAmmo;
			public readonly float CurrentSpread;

			public WeaponFiredEvent(uint ownerNetworkId, string weaponId, int currentAmmo, float currentSpread)
			{
				OwnerNetworkId = ownerNetworkId;
				WeaponId       = weaponId;
				CurrentAmmo    = currentAmmo;
				CurrentSpread  = currentSpread;
			}
		}

		// ─────────────────────────────────────────────
		//  Kill & Death Events
		// ─────────────────────────────────────────────

		/// <summary>
		/// Published server-wide by the server when a player's health reaches zero and they are killed.
		/// Replaces the former performance-costly DeathManager search pattern.
		/// </summary>
		public readonly struct PlayerKilledEvent : ITrceEvent
		{
			public readonly ulong VictimSteamId;   // Steam ID of the victim
			public readonly ulong AttackerSteamId; // Steam ID of the killer
			public readonly Vector3 HitPosition;   // Physical coordinates of the killing blow

			public PlayerKilledEvent(ulong victimSteamId, ulong attackerSteamId, Vector3 hitPosition)
			{
				VictimSteamId   = victimSteamId;
				AttackerSteamId = attackerSteamId;
				HitPosition     = hitPosition;
			}
		}

		// ─────────────────────────────────────────────
		//  Interaction Events
		// ─────────────────────────────────────────────

		/// <summary>
		/// Published when the player's interaction reticle target changes (including loss of target).
		/// </summary>
		public readonly struct InteractionTargetChangedEvent : ITrceEvent
		{
			public readonly int TargetNetworkId;
			public readonly string InteractionLabel;
			public bool HasTarget => TargetNetworkId != -1;

			public InteractionTargetChangedEvent(int targetNetworkId, string interactionLabel)
			{
				TargetNetworkId  = targetNetworkId;
				InteractionLabel = interactionLabel;
			}

			public static InteractionTargetChangedEvent NoTarget() => new InteractionTargetChangedEvent(-1, null);
		}

		// ─────────────────────────────────────────────
		//  Health Events
		// ─────────────────────────────────────────────

		/// <summary>
		/// Published when any entity's health value changes (healing, damage, revival, etc.).
		/// </summary>
		public readonly struct HealthChangedEvent : ITrceEvent
		{
			public readonly int TargetNetworkId;
			public readonly float OldHealth;
			public readonly float NewHealth;
			public float Delta => NewHealth - OldHealth;

			public HealthChangedEvent(int targetNetworkId, float oldHealth, float newHealth)
			{
				TargetNetworkId = targetNetworkId;
				OldHealth       = oldHealth;
				NewHealth       = newHealth;
			}
		}

		// ─────────────────────────────────────────────
		//  Network / Connection Events (published by TrceNetManager)
		// ─────────────────────────────────────────────

		/// <summary>
		/// Published by TrceNetManager when a client is authenticated and the connection is active.
		/// <para>
		/// Game-mode plugins should subscribe to this event to spawn the player Pawn,
		/// rather than coupling directly to TrceNetManager.
		/// </para>
		/// </summary>
		public readonly struct ClientReadyEvent : ITrceEvent
		{
			/// <summary>The authenticated client connection object.</summary>
			public readonly Connection Channel;

			/// <summary>The client's Steam ID (64-bit integer).</summary>
			public readonly ulong SteamId;

			/// <summary>The client's display name.</summary>
			public readonly string DisplayName;

			public ClientReadyEvent( Connection channel, ulong steamId, string displayName )
			{
				Channel     = channel;
				SteamId     = steamId;
				DisplayName = displayName;
			}
		}

		/// <summary>
		/// Published by TrceNetManager when a client disconnects.
		/// Game-mode plugins should subscribe to this event to clean up the player's Pawn or state.
		/// </summary>
		public readonly struct ClientDisconnectedEvent : ITrceEvent
		{
			/// <summary>The disconnected client connection object.</summary>
			public readonly Connection Channel;

			/// <summary>The client's Steam ID (64-bit integer).</summary>
			public readonly ulong SteamId;

			/// <summary>The client's display name.</summary>
			public readonly string DisplayName;

			public ClientDisconnectedEvent( Connection channel, ulong steamId, string displayName )
			{
				Channel     = channel;
				SteamId     = steamId;
				DisplayName = displayName;
			}
		}

		// ─────────────────────────────────────────────
		//  Attribute Events (published by TrceStatPlugin)
		// ─────────────────────────────────────────────

		/// <summary>
		/// Published globally by <c>TrceStatPlugin</c> when an entity's final attribute value
		/// (after all modifiers) changes.
		/// <para>
		/// <b>Trigger condition:</b> fired by <c>IAttributeService.SetBaseValue</c>, <c>AddModifier</c>, or
		/// <c>RemoveModifier</c> only when the computed final value actually changes.
		/// If the value is unchanged (no-op), this event is not published.
		/// </para>
		/// <para>
		/// <b>Subscription example:</b>
		/// <code>
		/// RegisterEvent&lt;CoreEvents.AttributeChangedEvent&gt;( OnAttrChanged );
		///
		/// private void OnAttrChanged( CoreEvents.AttributeChangedEvent e )
		/// {
		///     if ( e.AttrId == "player.move_speed" )
		///         SyncSpeedToClient( e.SteamId, e.NewValue );
		/// }
		/// </code>
		/// </para>
		/// </summary>
		public readonly struct AttributeChangedEvent : ITrceEvent
		{
			/// <summary>The Steam ID of the entity whose attribute changed (64-bit).</summary>
			public readonly ulong SteamId;

			/// <summary>The attribute identifier string, e.g. <c>"player.move_speed"</c>.</summary>
			public readonly string AttrId;

			/// <summary>The final attribute value before the change (all modifiers applied).</summary>
			public readonly float OldValue;

			/// <summary>The final attribute value after the change (all modifiers applied).</summary>
			public readonly float NewValue;

			/// <summary>The delta of this change (<c>NewValue - OldValue</c>). Positive means increase, negative means decrease.</summary>
			public float Delta => NewValue - OldValue;

			/// <summary>
			/// Creates an <see cref="AttributeChangedEvent"/> instance.
			/// </summary>
			/// <param name="steamId">Steam ID of the target entity.</param>
			/// <param name="attrId">The attribute identifier string.</param>
			/// <param name="oldValue">The final value before the change.</param>
			/// <param name="newValue">The final value after the change.</param>
			public AttributeChangedEvent( ulong steamId, string attrId, float oldValue, float newValue )
			{
				SteamId  = steamId;
				AttrId   = attrId;
				OldValue = oldValue;
				NewValue = newValue;
			}
		}

		// ─────────────────────────────────────────────
		//  State Tag Events (published by TrceStateTagPlugin)
		// ─────────────────────────────────────────────

		/// <summary>
		/// Published globally by <c>TrceStateTagPlugin</c> when a tag is successfully added to a <see cref="GameObject"/>.
		/// <para>
		/// <b>Trigger condition:</b> fired after <c>IStateTagService.AddTag</c> only when the target's
		/// <c>target.Tags</c> actually changes (prevents redundant events for duplicate tags).
		/// </para>
		/// </summary>
		public readonly struct TagAddedEvent : ITrceEvent
		{
			/// <summary>The target <see cref="GameObject"/> that received the tag.</summary>
			public readonly GameObject Target;

			/// <summary>The tag string that was added.</summary>
			public readonly string Tag;

			/// <summary>
			/// Creates a <see cref="TagAddedEvent"/> instance.
			/// </summary>
			/// <param name="target">The target object that received the tag.</param>
			/// <param name="tag">The tag string that was added.</param>
			public TagAddedEvent( GameObject target, string tag )
			{
				Target = target;
				Tag    = tag;
			}
		}

		/// <summary>
		/// Published globally by <c>TrceStateTagPlugin</c> when a tag is successfully removed from a <see cref="GameObject"/>.
		/// <para>
		/// <b>Trigger condition:</b> fired after <c>IStateTagService.RemoveTag</c> or timer expiry only when
		/// the target's <c>target.Tags</c> actually changes (if the tag did not exist, event is not published).
		/// </para>
		/// </summary>
		public readonly struct TagRemovedEvent : ITrceEvent
		{
			/// <summary>The target <see cref="GameObject"/> whose tag was removed.</summary>
			public readonly GameObject Target;

			/// <summary>The tag string that was removed.</summary>
			public readonly string Tag;

			/// <summary>
			/// Creates a <see cref="TagRemovedEvent"/> instance.
			/// </summary>
			/// <param name="target">The target object whose tag was removed.</param>
			/// <param name="tag">The tag string that was removed.</param>
			public TagRemovedEvent( GameObject target, string tag )
			{
				Target = target;
				Tag    = tag;
			}
		}

		// ─────────────────────────────────────────────
		//  Snapshot / Desync Detection (P2-4)
		// ─────────────────────────────────────────────

		/// <summary>
		/// Published by <c>SnapshotSync</c> at the start of each sync cycle.
		/// Any plugin can subscribe and call <see cref="SnapshotStateCollector.Contribute"/> to append an opaque state token
		/// (e.g. a hash of player positions, health values, or item counts) to the desync fingerprint.
		/// This increases entropy without creating a direct dependency on any specific plugin.
		/// </summary>
		public readonly struct SnapshotStateContributionEvent : ITrceEvent
		{
			/// <summary>The collector that accumulates state tokens from all subscribers.</summary>
			public readonly SnapshotStateCollector Collector;

			public SnapshotStateContributionEvent( SnapshotStateCollector collector )
			{
				Collector = collector;
			}
		}
	}
}

namespace Trce.Kernel.Event
{
	/// <summary>
	/// Companion helper that bulk-clears every known <see cref="CoreEvents"/> event type
	/// from <see cref="GlobalEventBus"/> on scene transitions.
	/// <para>
	/// <b>Maintenance rule:</b> Whenever a new event struct is added to <see cref="CoreEvents"/>,
	/// it must also be listed in <see cref="ClearAllCoreEvents"/>.
	/// </para>
	/// </summary>
	public static class CoreEventsBus
	{
		/// <summary>
		/// Clears all TRCE framework event subscriptions.
		/// Call this in <c>SandboxBridge.OnLevelLoaded()</c> on every scene transition
		/// to prevent stale delegates from firing on destroyed objects.
		/// </summary>
		public static void ClearAllCoreEvents()
		{
			// ── Combat ──────────────────────────────────────────────────────
			GlobalEventBus.ClearAll<CoreEvents.PlayerDamagedEvent>();
			GlobalEventBus.ClearAll<CoreEvents.WeaponFiredEvent>();
			GlobalEventBus.ClearAll<CoreEvents.PlayerKilledEvent>();

			// ── Interaction ──────────────────────────────────────────────────
			GlobalEventBus.ClearAll<CoreEvents.InteractionTargetChangedEvent>();

			// ── Health ───────────────────────────────────────────────────────
			GlobalEventBus.ClearAll<CoreEvents.HealthChangedEvent>();

			// ── Network / Connection ─────────────────────────────────────────
			GlobalEventBus.ClearAll<CoreEvents.ClientReadyEvent>();
			GlobalEventBus.ClearAll<CoreEvents.ClientDisconnectedEvent>();

			// ── Attributes & State Tags ──────────────────────────────────────
			GlobalEventBus.ClearAll<CoreEvents.AttributeChangedEvent>();
			GlobalEventBus.ClearAll<CoreEvents.TagAddedEvent>();
			GlobalEventBus.ClearAll<CoreEvents.TagRemovedEvent>();

			// ── Snapshot / Desync Detection ──────────────────────────────────
			GlobalEventBus.ClearAll<CoreEvents.SnapshotStateContributionEvent>();
		}
	}
}