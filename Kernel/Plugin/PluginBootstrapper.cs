using Sandbox;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Trce.Kernel.Plugin
{
	/// <summary>
	///   負責掃描場景並按順序初始化所有 TrcePlugin。同時作為插件總表 (Plugin Registry)。
	/// </summary>
	public class PluginBootstrapper : GameObjectSystem, ISceneStartup
	{
		public static PluginBootstrapper Instance { get; private set; }
		
		private readonly Dictionary<string, TrcePlugin> _plugins = new();
		private readonly Dictionary<System.Type, TrcePlugin> _pluginsByType = new();

		public PluginBootstrapper( Scene scene ) : base( scene ) 
		{ 
			Instance = this;
		}

		public async Task OnSceneStartup()
		{
			Log.Info( "🚀 TRCE Plugin Bootstrapper: Starting..." );

			_plugins.Clear();
			_pluginsByType.Clear();

			// 獲取場景中所有的插件
			var pluginList = Scene.GetAllComponents<TrcePlugin>().ToList();
			Log.Info( $"📦 Found {pluginList.Count} plugins to register." );

			// 註冊插件到字典以供快速查詢
			foreach ( var plugin in pluginList )
			{
				_plugins[plugin.PluginId] = plugin;
				_pluginsByType[plugin.GetType()] = plugin;
			}

			// 依次執行初始化
			foreach ( var plugin in pluginList )
			{
				try
				{
					Log.Info( $"🔧 Initializing Plugin: {plugin.PluginId} (v{plugin.Version})" );
					await plugin.InitializeAsync();
				}
				catch ( System.Exception ex )
				{
					Log.Error( $"❌ Failed to initialize plugin {plugin.PluginId}: {ex.Message}" );
				}
			}

			Log.Info( "✅ All plugins initialized and registered." );
		}

		public T GetPlugin<T>() where T : TrcePlugin
		{
			if ( _pluginsByType.TryGetValue( typeof( T ), out var plugin ) )
				return (T)plugin;
			return null;
		}

		public TrcePlugin GetPlugin( string id )
		{
			_plugins.TryGetValue( id, out var plugin );
			return plugin;
		}
	}
}
