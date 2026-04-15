// File: Code/Kernel/Event/CoreEvents.cs
// Encoding: UTF-8 (No BOM)
// All payloads are readonly structs — guaranteed Stack allocation, zero GC pressure.

using Sandbox;

namespace Trce.Kernel.Event
{
	/// <summary>
	/// <para>【Phase 2 核心事件定義】</para>
	/// <para>
	/// 所有事件均為 <c>readonly struct</c>，實作 <see cref="ITrceEvent"/>。
	/// 遵守 Zero-Allocation 原則：Payload 存在於 Stack，不產生 Heap 分配。
	/// </para>
	/// </summary>
	public static class CoreEvents
	{
		// ─────────────────────────────────────────────
		//  戰鬥類事件
		// ─────────────────────────────────────────────

		/// <summary>
		/// 玩家受到傷害時發布。
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
		/// 武器成功射擊一發後發布（每顆子彈觸發一次）。
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
		//  [新增] 擊殺與死亡事件
		// ─────────────────────────────────────────────

		/// <summary>
		/// 當玩家血量歸零被擊殺時，由伺服器向全域發布。
		/// 取代原本消耗效能的 DeathManager 搜尋。
		/// </summary>
		public readonly struct PlayerKilledEvent : ITrceEvent
		{
			public readonly ulong VictimSteamId;   // 死者的 Steam ID
			public readonly ulong AttackerSteamId; // 殺手的 Steam ID
			public readonly Vector3 HitPosition;   // 致命一擊的物理座標

			public PlayerKilledEvent(ulong victimSteamId, ulong attackerSteamId, Vector3 hitPosition)
			{
				VictimSteamId   = victimSteamId;
				AttackerSteamId = attackerSteamId;
				HitPosition     = hitPosition;
			}
		}

		// ─────────────────────────────────────────────
		//  互動類事件
		// ─────────────────────────────────────────────

		/// <summary>
		/// 玩家的互動準心對準目標改變時發布（包含取消對準）。
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
		//  生命值類事件
		// ─────────────────────────────────────────────

		/// <summary>
		/// 任意實體的生命值發生變化時發布（回血、受傷、復活等）。
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
		//  網路連線類事件（由 TrceNetManager 發布）
		// ─────────────────────────────────────────────

		/// <summary>
		/// 當客戶端通過驗證且連線處於活躍狀態時，由 TrceNetManager 發布。
		/// <para>
		/// 遊戲模式插件應訂閱此事件來負責生成玩家 Pawn，
		/// 而非直接耦合至 TrceNetManager。
		/// </para>
		/// </summary>
		public readonly struct ClientReadyEvent : ITrceEvent
		{
			/// <summary>已通過驗證的客戶端連線物件。</summary>
			public readonly Connection Channel;

			/// <summary>客戶端的 Steam ID（64 位元整數）。</summary>
			public readonly ulong SteamId;

			/// <summary>客戶端的顯示名稱。</summary>
			public readonly string DisplayName;

			public ClientReadyEvent( Connection channel, ulong steamId, string displayName )
			{
				Channel     = channel;
				SteamId     = steamId;
				DisplayName = displayName;
			}
		}

		/// <summary>
		/// 當客戶端中斷連線時，由 TrceNetManager 發布。
		/// 遊戲模式插件應訂閱此事件以清理玩家 Pawn 或狀態。
		/// </summary>
		public readonly struct ClientDisconnectedEvent : ITrceEvent
		{
			/// <summary>已中斷連線的客戶端連線物件。</summary>
			public readonly Connection Channel;

			/// <summary>客戶端的 Steam ID（64 位元整數）。</summary>
			public readonly ulong SteamId;

			/// <summary>客戶端的顯示名稱。</summary>
			public readonly string DisplayName;

			public ClientDisconnectedEvent( Connection channel, ulong steamId, string displayName )
			{
				Channel     = channel;
				SteamId     = steamId;
				DisplayName = displayName;
			}
		}

		// ─────────────────────────────────────────────
		//  屬性數值類事件（由 TrceStatPlugin 發布）
		// ─────────────────────────────────────────────

		/// <summary>
		/// 當實體的某個屬性最終值（含所有修飾符計算後）發生改變時，由 <c>TrceStatPlugin</c> 向全域發布。
		/// <para>
		/// <b>觸發條件：</b>呼叫 <c>IAttributeService.SetBaseValue</c>、<c>AddModifier</c> 或
		/// <c>RemoveModifier</c>，且計算後的最終值確實發生改變時才發布。
		/// 若值未改變（No-Op 情境），不會發布此事件。
		/// </para>
		/// <para>
		/// <b>訂閱範例：</b>
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
			/// <summary>屬性發生改變的實體 Steam ID（64 位元）。</summary>
			public readonly ulong SteamId;

			/// <summary>改變的屬性識別字串，例如 <c>"player.move_speed"</c>。</summary>
			public readonly string AttrId;

			/// <summary>改變前的屬性最終值（已含所有修飾符計算）。</summary>
			public readonly float OldValue;

			/// <summary>改變後的屬性最終值（已含所有修飾符計算）。</summary>
			public readonly float NewValue;

			/// <summary>此次變化的差值 (<c>NewValue - OldValue</c>)。正值為增加，負值為減少。</summary>
			public float Delta => NewValue - OldValue;

			/// <summary>
			/// 建立一個 <see cref="AttributeChangedEvent"/> 實例。
			/// </summary>
			/// <param name="steamId">目標實體的 Steam ID。</param>
			/// <param name="attrId">改變的屬性識別字串。</param>
			/// <param name="oldValue">改變前的最終值。</param>
			/// <param name="newValue">改變後的最終值。</param>
			public AttributeChangedEvent( ulong steamId, string attrId, float oldValue, float newValue )
			{
				SteamId  = steamId;
				AttrId   = attrId;
				OldValue = oldValue;
				NewValue = newValue;
			}
		}

		// ─────────────────────────────────────────────
		//  狀態標籤類事件（由 TrceStateTagPlugin 發布）
		// ─────────────────────────────────────────────

		/// <summary>
		/// 當標籤成功被添加至 <see cref="GameObject"/> 時，由 <c>TrceStateTagPlugin</c> 向全域發布。
		/// <para>
		/// <b>觸發條件：</b>呼叫 <c>IStateTagService.AddTag</c> 後，目標的
		/// <c>target.Tags</c> 確實發生變更時才發布（防止重複標籤觸發冗餘事件）。
		/// </para>
		/// </summary>
		public readonly struct TagAddedEvent : ITrceEvent
		{
			/// <summary>被添加標籤的目標 <see cref="GameObject"/>。</summary>
			public readonly GameObject Target;

			/// <summary>被添加的標籤字串。</summary>
			public readonly string Tag;

			/// <summary>
			/// 建立一個 <see cref="TagAddedEvent"/> 實例。
			/// </summary>
			/// <param name="target">被添加標籤的目標物件。</param>
			/// <param name="tag">被添加的標籤字串。</param>
			public TagAddedEvent( GameObject target, string tag )
			{
				Target = target;
				Tag    = tag;
			}
		}

		/// <summary>
		/// 當標籤成功從 <see cref="GameObject"/> 移除時，由 <c>TrceStateTagPlugin</c> 向全域發布。
		/// <para>
		/// <b>觸發條件：</b>呼叫 <c>IStateTagService.RemoveTag</c> 或計時器過期後，目標的
		/// <c>target.Tags</c> 確實發生變更時才發布（若標籤原本不存在，不會發布此事件）。
		/// </para>
		/// </summary>
		public readonly struct TagRemovedEvent : ITrceEvent
		{
			/// <summary>被移除標籤的目標 <see cref="GameObject"/>。</summary>
			public readonly GameObject Target;

			/// <summary>被移除的標籤字串。</summary>
			public readonly string Tag;

			/// <summary>
			/// 建立一個 <see cref="TagRemovedEvent"/> 實例。
			/// </summary>
			/// <param name="target">被移除標籤的目標物件。</param>
			/// <param name="tag">被移除的標籤字串。</param>
			public TagRemovedEvent( GameObject target, string tag )
			{
				Target = target;
				Tag    = tag;
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
		}
	}
}