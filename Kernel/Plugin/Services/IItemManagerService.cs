using System;
using Trce.Plugins.Storage;

namespace Trce.Kernel.Plugin.Services
{
	public interface IItemManagerService
	{
		TrceItemDefinition GetDefinition( string itemId );
		void UseItem( TrceItemInstance item, ulong userSteamId );
		TrceItemInstance CreateItem( string itemId, int quantity = 1 );
	}
}
