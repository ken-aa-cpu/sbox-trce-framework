using System.Collections.Generic;

namespace Trce.Plugins.Combat

{
	/// <summary>
	/// </summary>
	public enum AmmoType
	{
		/// <summary>標準子彈。無特殊效果，全傷害輸出。所有武器的預設彈藥類型。</summary>
		Standard,
		/// <summary> N   ( )</summary>
		Tranquilizer,
		/// <summary> +</summary>
		Incendiary,
		/// <summary> /</summary>
		ArmorPiercing,
		/// <summary> 0   ( )</summary>
		Blank,
		/// <summary> N</summary>
		Marker
	}

	/// <summary>
	/// </summary>
	public class AmmoDefinition
	{
		public AmmoType Type { get; set; }
		public string DisplayName { get; set; }
		public float DamageMultiplier { get; set; } = 1.0f;
		public float StunDuration { get; set; } = 0f;
		public float BurnDamagePerSecond { get; set; } = 0f;
		public float BurnDuration { get; set; } = 0f;
		public float MarkDuration { get; set; } = 0f;
		public bool CanPenetrate { get; set; } = false;
		public string ImpactEffect { get; set; } = "";
		public string TrailColor { get; set; } = "#ffffff";
	}

	/// <summary>
	///
	///   /   WeaponDefinition
	/// /   -   ( )
	/// /   -   ( )
	///   - ��ť�ݩ� (�ҫ��B���ġB�S��)
	///
	///   /   JSON   Inspector
	///   / Web Editor
	/// </summary>
	public class WeaponDefinition
	{
		//  򥻸T 
		public string WeaponId { get; set; } = "pistol_oneshot";
		public string DisplayName { get; set; } = "Default Weapon";
		public string Description { get; set; } = "A standard weapon.";
		//  ˮ` 
		public float BaseDamage { get; set; } = 100f;
		public float HeadshotMultiplier { get; set; } = 2.0f;
		public float FalloffStart { get; set; } = 500f;
		public float FalloffEnd { get; set; } = 2000f;
		public float MinDamagePercent { get; set; } = 0.3f;
		//  g 
		public float FireRate { get; set; } = 0.5f;
		public bool IsAutomatic { get; set; } = false;
		public int BurstCount { get; set; } = 1;
		public float BurstDelay { get; set; } = 0.05f;
		//  uX 
		public int MagazineSize { get; set; } = 1;
		public int MaxReserveAmmo { get; set; } = 0;
		public float ReloadTime { get; set; } = 2.0f;
		public bool AutoReload { get; set; } = true;
		//  uD 
		public float MaxRange { get; set; } = 5000f;
		public float SpreadAngle { get; set; } = 0.5f;
		public float SpreadIncreasePerShot { get; set; } = 0.2f;
		public float SpreadRecoveryRate { get; set; } = 1.0f;
		public float RecoilPitch { get; set; } = 2.0f;
		public float RecoilYaw { get; set; } = 0.5f;
		//  ı 
		public string ModelPath { get; set; } = "";
		public float ModelScale { get; set; } = 1.0f;
		public string MuzzleFlashEffect { get; set; } = "";
		public string ImpactEffect { get; set; } = "";
		public string TracerEffect { get; set; } = "";
		public float TracerSpeed { get; set; } = 8000f;
		//   
		public string FireSound { get; set; } = "";
		public string ReloadSound { get; set; } = "";
		public string EmptySound { get; set; } = "";
		public string EquipSound { get; set; } = "";
		public List<AmmoType> AvailableAmmoTypes { get; set; } = new() { AmmoType.Standard };
		//  w]Zut 
		/// <summary> ( )</summary>
		public static WeaponDefinition OneShotPistol => new()
		{
			WeaponId = "pistol_oneshot",
			DisplayName = "Pistol",
			Description = "A reliable sidearm, fires one shot at a time.",
			BaseDamage = 200f,
			HeadshotMultiplier = 1.0f,
			FireRate = 1.0f,
			MagazineSize = 1,
			MaxReserveAmmo = 0,
			SpreadAngle = 0.3f,
			MaxRange = 3000f,
			AvailableAmmoTypes = new() { AmmoType.Standard }
		};
		/// <summary>麻醉槍預設定義。命中後使目標暈厥 8 秒，BaseDamage 為 0（不造成生命值傷害）。</summary>
		public static WeaponDefinition TranquilizerGun => new()
		{
			WeaponId = "gun_tranquilizer",
			DisplayName = "Shotgun",
			Description = "Pre-loaded with 8 shells.",
			BaseDamage = 0f,
			FireRate = 2.0f,
			MagazineSize = 1,
			MaxReserveAmmo = 0,
			SpreadAngle = 0.8f,
			MaxRange = 2000f,
			AvailableAmmoTypes = new() { AmmoType.Tranquilizer }
		};
	}

}

