// ?”ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג?
// שÝשששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששß
// שר  Copyright (c) 2026 TRCE Team. All rights reserved.            שר
// שר  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      שר
// ?ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג??ג?
namespace Trce.Plugins.Storage

{
	/// <summary>
// שÝשששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששששß
// שר  Copyright (c) 2026 TRCE Team. All rights reserved.            שר
// שר  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      שר
	///
	///   / ? ?? ??TRCE ? ? ? ? ? ? ? ?Component  ??
	///
	/// /  ? ?
	///   /   //  ? ? ?? ? ? ?
	///   /   public class HealPotionBehavior : Component, IItemBehavior
	///   /       public string BehaviorId => "ext_heal_potion";
	///
	///   /       public void OnUse(TrceItemInstance item, ulong userSteamId)
	///   /           var health = Game.Scene.GetAllComponents&lt;HealthSystem&gt;()
	///   /               .FirstOrDefault(h => h.OwnerId == userSteamId);
	///           health?.Heal(50f);
	///   /           item.ConsumeOne(); //  ???
	///
	///   /       public void OnInteract(TrceItemInstance item, ulong userSteamId, Vector3 targetPos)
	///   /           // ? ?? ?
	/// </summary>
	public interface IItemBehavior
	{
		/// <summary>
		///   /  ?? ?? ? ? ? TrceItemDefinition.ItemId
		///   / ? ? xt_yourplugin_itemname
		/// </summary>
		string BehaviorId { get; }
		/// <summary>
		/// / ? ? ? ? / ? ?
		/// /  ?  Server  ? ?
		/// </summary>
		void OnUse( TrceItemInstance item, ulong userSteamId );
		/// <summary>
		///   / ? ? ? ?? / ?? ?
		/// /  ?  Server  ? ?
		/// </summary>
		void OnInteract( TrceItemInstance item, ulong userSteamId, Vector3 targetPos );
	}

}

