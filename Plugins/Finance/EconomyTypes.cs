using System.Collections.Generic;
using Trce.Kernel.Plugin.Services;

namespace Trce.Plugins.Finance
{
	/// <summary>
	/// </summary>
	public class ShopItem
	{
		public string ItemId { get; set; }
		public string DisplayName { get; set; }
		public string Description { get; set; }
		public string Category { get; set; }
		public string IconPath { get; set; }
		public int PriceTraceCoin { get; set; }
		public int PriceTracePoint { get; set; }
		public bool IsLimited { get; set; }
		public int StockLimit { get; set; } = -1;
		public int PerPlayerLimit { get; set; } = -1;
		public int RequiredPassLevel { get; set; } = 0;
		public List<string> GrantItems { get; set; } = new();
	}

}

