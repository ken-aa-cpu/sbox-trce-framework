namespace Trce.Kernel.Papi

{
	/// <summary>
	/// Copyright (c) 2026 TRCE Team. All rights reserved.
	/// [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.
	///
	/// Component PAPI
	/// %xxx%
	/// TRCE
	///
	/// trce_* (%trce_balance%)
	/// ext_* (%ext_pet_name%)
	/// game_* (%game_round_count%)
	///
	///   public class PetSystem : Component, ITrcePlaceholderProvider
	///       public string ProviderId => "ext_pet";
	///
	///       public string TryResolvePlaceholder(string key)
	///           return key switch
	///               "ext_pet_name"    => pet.Name,
	///               "ext_pet_level"   => pet.Level.ToString(),
	///               "ext_pet_hunger"  => $"{pet.Hunger}%",
	///               _                 => null
	///           };
	///
	///   // UI 模板
	///   // "你的寵物 %ext_pet_name% (等級 %ext_pet_level%)"
	/// </summary>
	public interface ITrcePlaceholderProvider
	{
		/// <summary>
		/// 此提供者的識別碼
		/// </summary>
		string ProviderId { get; }
		/// <summary>
		/// 嘗試解析佔位符 Key
		/// 若無對應 Key 請返回 <c>null</c>
		/// </summary>
		/// <param name="key">佔位符 (不含 %)</param>
		/// <returns>解析結果，若無則返回 null</returns>
		string TryResolvePlaceholder( string key );
	}

}
