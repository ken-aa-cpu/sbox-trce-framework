namespace Trce.Plugins.Storage

{
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

