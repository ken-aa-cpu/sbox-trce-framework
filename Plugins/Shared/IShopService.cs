using Sandbox;
using System.Collections.Generic;

namespace Trce.Kernel.Plugin.Services
{
	/// <summary>
	/// Core interface for handling shop and microtransaction related operations.
	/// </summary>
	public interface IShopService
	{
		/// <summary>
		/// Purchases a specific item for the given player utilizing the specified currency type.
		/// </summary>
		void PurchaseItem( ulong steamId, string itemId, string currencyType );

		/// <summary>
		/// Retrieves a collection of all available item IDs currently listed in the shop catalog.
		/// </summary>
		IEnumerable<string> GetCatalogItems();
	}

	/// <summary>
	/// Core interface for managing player Battle Pass progression and status.
	/// </summary>
	public interface IBattlePassService
	{
		int GetLevel( ulong steamId );
		void AddExperience( ulong steamId, int amount );
		bool HasPremiumPass( ulong steamId );
	}
}
