using Sandbox;
using System.Collections.Generic;

namespace Trce.Kernel.Plugin.Services
{
	/// <summary>
	/// ?†å??å?ä»‹é¢
	/// </summary>
	public interface IShopService
	{
		/// <summary>
		/// /  ? ? ?
		/// </summary>
		void PurchaseItem( ulong steamId, string itemId, string currencyType );

		/// <summary>
		/// ?–å??€?‰å¯?¨å??å?è¡?
		/// </summary>
		IEnumerable<string> GetCatalogItems();
	}

	/// <summary>
	/// ?šè?è­‰æ??™ä???
	/// </summary>
	public interface IBattlePassService
	{
		int GetLevel( ulong steamId );
		void AddExperience( ulong steamId, int amount );
		bool HasPremiumPass( ulong steamId );
	}
}

