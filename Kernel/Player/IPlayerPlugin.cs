namespace Trce.Kernel.Player
{
	/// <summary>
	/// Copyright (c) 2026 TRCE Team. All rights reserved.
	/// [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.
	///
	/// public class MyPlugin : Component, IPlayerPlugin
	///     public string PluginId => "trce.myplugin";
	///     public void OnPlayerJoined(TrcePlayerState p) { ... }
	///     protected override void OnStart()
	///         Scene.Get<TrcePlayerManager>()?.RegisterPlugin(this);
	/// </summary>
	public interface IPlayerPlugin
	{
		/// <summary>
		/// 插件的識別碼，例如 "trce.moduleName" 或 "ext.authorName.pluginName"
		/// </summary>
		string PluginId { get; }
		/// <summary> 當玩家加入時執行 </summary>
		void OnPlayerJoined( TrcePlayerState player );
		/// <summary> 當玩家離開時執行 </summary>
		void OnPlayerLeft( ulong steamId );
		/// <summary> 當回合重置時執行 </summary>
		void OnRoundReset();
		// 預設介面方法 (可選實作)
		/// <summary> 當玩家死亡時執行 (Server Only) </summary>
		void OnPlayerDied( TrcePlayerState victim, ulong killerId ) { }
		/// <summary> 當玩家復活時執行 (Server Only) </summary>
		void OnPlayerRevived( TrcePlayerState player ) { }
		/// <summary> 當指派角色給玩家時執行 (Server Only) </summary>
		void OnRoleAssigned( TrcePlayerState player ) { }
		/// <summary>
		/// 當玩家的 ServerData 指定 key 發生變更時執行
		/// </summary>
		void OnPlayerDataChanged( TrcePlayerState player, string key ) { }
		/// <summary> 當玩家生命值改變時執行 (Server Only) </summary>
		void OnHealthChanged( TrcePlayerState player, float oldHealth, float newHealth ) { }
		/// <summary> 當玩家區域改變時執行 (Server Only) </summary>
		void OnZoneChanged( TrcePlayerState player, string oldZone, string newZone ) { }
	}

}

