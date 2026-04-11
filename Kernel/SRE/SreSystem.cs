using Sandbox;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Trce.Kernel.SRE
{
	/// <summary>
	/// SRE System：TRCE 框架的插件健康狀態監控系統。
	/// 負責追蹤所有插件的啟動狀態，並在發生錯誤時統一記錄。
	/// 由引擎自動為每個場景實例化。
	/// </summary>
	public class SreSystem : GameObjectSystem, ISceneStartup
	{
		public static SreSystem Instance { get; private set; }

		private readonly ConcurrentDictionary<string, DateTime> _activePlugins = new();

		public SreSystem( Scene scene ) : base( scene )
		{
			Instance = this;
			Log.Info( "[SRE] Global SRE System active for current scene." );
		}

		void ISceneStartup.OnHostInitialize()
		{
			Log.Info( "[SRE] Host initialization sequence started." );
		}

		/// <summary>
		/// 插件啟動時向 SRE 報到，記錄插件 ID 與版本。
		/// 由 TrcePlugin.InitializeAsync() 自動呼叫，不需手動呼叫。
		/// </summary>
		public void CheckIn( string pluginId, string version )
		{
			_activePlugins[pluginId] = DateTime.Now;
			Log.Info( $"[SRE] Plugin check-in: {pluginId} (v{version})" );
		}

		/// <summary>
		/// 插件發生錯誤時向 SRE 報告，統一記錄錯誤來源與訊息。
		/// 由 TrcePlugin.SafeExecute() 自動呼叫，不需手動呼叫。
		/// </summary>
		public System.Threading.Tasks.Task ReportError( string source, string message, string stackTrace )
		{
			Log.Error( $"[SRE] Error from '{source}': {message}" );

			if ( !string.IsNullOrEmpty( stackTrace ) )
				Log.Error( $"[SRE] Stack trace:\n{stackTrace}" );

			return System.Threading.Tasks.Task.CompletedTask;
		}

		/// <summary>
		/// 取得所有已啟動的插件 ID 清單。
		/// </summary>
		public List<string> GetActivePlugins() => new List<string>( _activePlugins.Keys );
	}
}