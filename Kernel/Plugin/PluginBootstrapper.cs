using Sandbox;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Trce.Kernel.Plugin
{
	/// <summary>
	///   Scans the scene and initialises all <see cref="TrcePlugin"/> instances in dependency order.
	///   Acts as the plugin registry for the current scene.
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

			var pluginList = Scene.GetAllComponents<TrcePlugin>().ToList();
			Log.Info( $"📦 Found {pluginList.Count} plugins to register." );

			foreach ( var plugin in pluginList )
			{
				_plugins[plugin.PluginId] = plugin;
				_pluginsByType[plugin.GetType()] = plugin;
			}

			// P1-4: Sort plugins by dependency order (Kahn's topological sort).
			var sortedList = TopologicalSort( pluginList );

			foreach ( var plugin in sortedList )
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

		// ─────────────────────────────────────────────
		//  P1-4: Topological Sort (Kahn's Algorithm)
		// ─────────────────────────────────────────────

		/// <summary>
		/// P1-4: Sorts the given plugin list by their <see cref="TrcePluginAttribute.Depends"/> declarations
		/// using Kahn's algorithm. If a cycle is detected the original scan order is returned as a fallback.
		/// </summary>
		private List<TrcePlugin> TopologicalSort( List<TrcePlugin> plugins )
		{
			// Build a lookup from plugin ID → plugin instance.
			var idMap = new Dictionary<string, TrcePlugin>( plugins.Count );
			foreach ( var p in plugins )
				idMap[p.PluginId] = p;

			// in-degree[id] = number of dependencies that appear in this scene.
			var inDegree = new Dictionary<string, int>( plugins.Count );
			// adjacency[id] = list of plugins that depend on id (i.e. must come AFTER id).
			var adjacency = new Dictionary<string, List<string>>( plugins.Count );

			foreach ( var p in plugins )
			{
				if ( !inDegree.ContainsKey( p.PluginId ) )
					inDegree[p.PluginId] = 0;

				if ( !adjacency.ContainsKey( p.PluginId ) )
					adjacency[p.PluginId] = new List<string>();
			}

			foreach ( var p in plugins )
			{
				var attr = p.Info;
				if ( attr == null ) continue;

				foreach ( var dep in attr.Depends )
				{
					if ( !idMap.ContainsKey( dep ) )
					{
						// Dependency not found in scene — log a warning and skip.
						Log.Warning( $"[Bootstrapper] Plugin '{p.PluginId}' depends on '{dep}' which is not present in the scene." );
						continue;
					}

					// dep must initialise before p → dep → p edge.
					adjacency[dep].Add( p.PluginId );
					inDegree[p.PluginId]++;
				}
			}

			// Kahn's BFS: start with all nodes that have no in-scene dependencies.
			var queue = new Queue<string>();
			foreach ( var kvp in inDegree )
			{
				if ( kvp.Value == 0 )
					queue.Enqueue( kvp.Key );
			}

			var sorted = new List<TrcePlugin>( plugins.Count );
			while ( queue.Count > 0 )
			{
				var current = queue.Dequeue();
				if ( idMap.TryGetValue( current, out var plugin ) )
					sorted.Add( plugin );

				foreach ( var neighbor in adjacency[current] )
				{
					inDegree[neighbor]--;
					if ( inDegree[neighbor] == 0 )
						queue.Enqueue( neighbor );
				}
			}

			// If cycle detected (not all nodes were processed), fall back to original order.
			if ( sorted.Count != plugins.Count )
			{
				Log.Error( "[Bootstrapper] ⚠️ Circular plugin dependency detected! " +
				           "Falling back to scene scan order. Check TrcePluginAttribute.Depends declarations." );

				var cyclic = plugins.Where( p => !sorted.Contains( p ) ).Select( p => p.PluginId );
				Log.Error( $"[Bootstrapper] Plugins involved in cycle: {string.Join( ", ", cyclic )}" );

				return plugins;
			}

			Log.Info( $"[Bootstrapper] Dependency-sorted initialisation order: {string.Join( " → ", sorted.Select( p => p.PluginId ) )}" );
			return sorted;
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
