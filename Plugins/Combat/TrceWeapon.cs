using Sandbox;
using System;
using Trce.Kernel.Bridge;
using Trce.Kernel.Event;
using Trce.Kernel.Player;
// 假設 CoreEvents 中的 struct 定義於此命名空間或類別下
using static Trce.Kernel.Event.CoreEvents; 

namespace Trce.Plugins.Combat
{
	[Title( "Trce Weapon" ), Group( "Trce - Modules" )]
	public class TrceWeapon : Component
	{
		public enum WeaponState { Idle, Firing, Reloading, Switching, OutOfAmmo }

		[Property, Group("Data")] public string WeaponId { get; set; } = "pistol_oneshot";
		[Sync, Property, Group("Status")] public int CurrentAmmo { get; set; }
		[Sync, Property, Group("Status")] public int ReserveAmmo { get; set; }
		[Sync, Property, Group("Status")] public WeaponState State { get; set; } = WeaponState.Idle;

		[Property, Group("Visuals")] public SoundEvent FireSoundEvent { get; set; }

		public Action<ulong, string, int, bool> OnWeaponFired;
		public Action<ulong, float> OnPlayerStunned;
		public Action<ulong, float> OnPlayerMarked;
		public Action<string, AmmoType> OnAmmoTypeSwitched;

		private WeaponDefinition definition;
		
		// 修正 1：徹底分開客戶端預測與伺服器驗證的計時器，防止 Listen Server (房主) 卡彈
		private float _clientLastFireTime; 
		private float _serverLastFireTime; 
		
		private float reloadStartTime;
		private float currentSpread;

		private EntityEventBus localEventBus;

		[Sync] public int CurrentAmmoTypeIndex { get; set; } = 0;

		protected override void OnStart() 
		{ 
			// 快取 EventBus 避免運行時的 GC 開銷
			localEventBus = Components.GetInAncestorsOrSelf<EntityEventBus>();

			LoadDefinition(); 
			if ( definition != null && Networking.IsHost ) 
			{ 
				CurrentAmmo = definition.MagazineSize; 
				ReserveAmmo = definition.MaxReserveAmmo; 
			} 
		}

		protected override void OnFixedUpdate()
		{
			if ( !Networking.IsHost ) return;
			if ( State == WeaponState.Reloading && Time.Now - reloadStartTime >= definition.ReloadTime ) FinishReload();
			if ( currentSpread > 0 ) currentSpread = Math.Max( 0, currentSpread - Time.Delta * 2f );
		}

		/// <summary>
		/// 武器主要開火邏輯 (支援 Client-Prediction)
		/// </summary>
		public void PrimaryAttack( Vector3 origin, Vector3 direction )
		{
			if ( definition == null || State != WeaponState.Idle || CurrentAmmo <= 0 ) return;
			
			// 客戶端預測冷卻驗證
			if ( Time.Now - _clientLastFireTime < 60f / definition.FireRate ) return;

			if ( !IsProxy )
			{
				_clientLastFireTime = Time.Now;
				CurrentAmmo--;
				currentSpread += definition.SpreadIncreasePerShot;

				if ( FireSoundEvent != null ) Sound.Play( FireSoundEvent, origin );

				// 發布開火事件給 UI (修正：因應 s&box API 更新改用 SandboxBridge 封裝的網路識別)
				if ( localEventBus != null )
				{
					uint ownerNetId = SandboxBridge.Instance != null 
						? SandboxBridge.Instance.GetNetworkIdUInt( GameObject ) 
						: (uint)GameObject.Id.GetHashCode();
					localEventBus.Publish( new WeaponFiredEvent( ownerNetId, WeaponId, CurrentAmmo, currentSpread ) ); 
				}

				RpcServerFire( origin, direction );
			}
		}

		[Rpc.Broadcast]
		private void RpcServerFire( Vector3 origin, Vector3 direction )
		{
			// 其他玩家的客戶端 (Proxy) 播放開火音效與特效
			if ( IsProxy && FireSoundEvent != null ) Sound.Play( FireSoundEvent, origin );

			// 伺服器驗證區塊 (Server-Authority Hit Validation)
			if ( Networking.IsHost )
			{
				float expectedDelay = 60f / definition.FireRate;
				// 伺服器獨立冷卻驗證 (容許 10% 網路誤差)
				if ( Time.Now - _serverLastFireTime < expectedDelay * 0.9f ) return;
				_serverLastFireTime = Time.Now;

				// 伺服器端同步數據 (若為 IsProxy 才需要扣彈藥，因房主已在 PrimaryAttack 扣過)
				if ( IsProxy ) 
				{
					CurrentAmmo--;
					currentSpread += definition.SpreadIncreasePerShot;
				}

				var owner = Components.GetInAncestorsOrSelf<ITrcePlayer>();
				if ( owner == null || !owner.IsValid ) return;

				// 嚴格無 GC 射線檢測
				var tr = Scene.Trace.Ray( origin, origin + direction * 5000f )
					.UseHitboxes()
					.IgnoreGameObjectHierarchy( GameObject ) 
					.Run();

				if ( tr.Hit && tr.GameObject != null )
				{
					var hitPlayer = tr.GameObject.Components.GetInAncestorsOrSelf<ITrcePlayer>();
					var hasHitbox = tr.Hitbox != null;

					if ( hitPlayer != null || hasHitbox )
					{
						ApplyDamage( tr.GameObject, definition.BaseDamage, owner.SteamId, tr.HitPosition );
					}
				}

				OnWeaponFired?.Invoke( owner.SteamId, definition.WeaponId, CurrentAmmo, tr.Hit && tr.GameObject != null );
			}
		}

		// 無 GC 開銷的傷害分派
		private void ApplyDamage( GameObject targetObject, float damage, ulong shooterSteamId, Vector3 hitPos )
		{
			var targetPlayer = targetObject.Components.GetInAncestorsOrSelf<ITrcePlayer>();
			if ( targetPlayer == null || !targetPlayer.IsValid ) return;

			// 使用 EventBus 即時分派受傷事件 (修正：使用 Struct 建構子並由 SandboxBridge 取得正確網路識別碼)
			var targetEventBus = targetObject.Components.GetInAncestorsOrSelf<EntityEventBus>();
			if ( targetEventBus != null )
			{
				uint targetNetId = SandboxBridge.Instance != null 
					? SandboxBridge.Instance.GetNetworkIdUInt( targetObject ) 
					: (uint)targetObject.Id.GetHashCode();
				targetEventBus.Publish( new PlayerDamagedEvent( targetNetId, (uint)shooterSteamId, damage ) );
			}

			var ammoType = GetCurrentAmmoType();
			switch ( ammoType )
			{
				case AmmoType.Standard:
				case AmmoType.ArmorPiercing:
					if ( damage >= 100f )
					{
						// 修正 2：徹底消滅 LINQ 搜尋！改用 GlobalEventBus 發送死亡事件
						GlobalEventBus.Publish( new PlayerKilledEvent( targetPlayer.SteamId, shooterSteamId, hitPos ) );
					}
					break;
				case AmmoType.Tranquilizer: 
					OnPlayerStunned?.Invoke( targetPlayer.SteamId, 8f ); 
					break;
				case AmmoType.Marker: 
					OnPlayerMarked?.Invoke( targetPlayer.SteamId, 15f ); 
					break;
			}
		}

		public void StartReload()
		{
			if ( definition == null || State != WeaponState.Idle || CurrentAmmo >= definition.MagazineSize || ( ReserveAmmo <= 0 && definition.MaxReserveAmmo > 0 ) ) return;
			State = WeaponState.Reloading;
			reloadStartTime = Time.Now;
		}

		private void FinishReload()
		{
			if ( !Networking.IsHost ) return; 
			int needed = definition.MagazineSize - CurrentAmmo;
			int available = definition.MaxReserveAmmo > 0 ? Math.Min( needed, ReserveAmmo ) : needed;
			CurrentAmmo += available;
			if ( definition.MaxReserveAmmo > 0 ) ReserveAmmo -= available;
			State = WeaponState.Idle;
		}

		[Rpc.Owner]
		public void RequestSwitchAmmo()
		{
			if ( !Networking.IsHost || definition == null || definition.AvailableAmmoTypes.Count <= 1 ) return;
			CurrentAmmoTypeIndex = ( CurrentAmmoTypeIndex + 1 ) % definition.AvailableAmmoTypes.Count;
			OnAmmoTypeSwitched?.Invoke( definition.WeaponId, GetCurrentAmmoType() );
		}

		public AmmoType GetCurrentAmmoType() => ( definition == null || definition.AvailableAmmoTypes.Count == 0 ) ? AmmoType.Standard : definition.AvailableAmmoTypes[CurrentAmmoTypeIndex % definition.AvailableAmmoTypes.Count];

		private void LoadDefinition() 
		{ 
			definition = WeaponId switch { 
				"pistol_oneshot" => WeaponDefinition.OneShotPistol, 
				"gun_tranquilizer" => WeaponDefinition.TranquilizerGun, 
				_ => WeaponDefinition.OneShotPistol 
			}; 
		}
		
		public WeaponDefinition GetDefinition() => definition;
	}
}