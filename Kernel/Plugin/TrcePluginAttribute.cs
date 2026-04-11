using System;

namespace Trce.Kernel.Plugin
{
	/// <summary>
	/// TRCE FRAMEWORK PROPRIETARY SOURCE CODE
	/// Copyright (c) 2026 TRCE Team. All rights reserved.
	/// [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.
	/// </summary>
	[AttributeUsage( AttributeTargets.Class, Inherited = false, AllowMultiple = false )]
	public class TrcePluginAttribute : Attribute
	{
		/// <summary> 插件的唯一識別碼，例如 "trce.inventory" 或 "game.murder_mystery" </summary>
		public string Id { get; set; }

		/// <summary> 插件的顯示名稱 (用於 Log 和 UI) </summary>
		public string Name { get; set; }

		/// <summary> 插件的版本號 </summary>
		public string Version { get; set; } = "1.0.0";

		/// <summary> 插件的作者 </summary>
		public string Author { get; set; } = "Unknown";

		/// <summary>
		/// 硬依賴列表。Manager 會在這些插件啟動 (OnEnable) 後才啟動此插件
		/// </summary>
		public string[] Depends { get; set; } = Array.Empty<string>();

		/// <summary> 軟依賴列表。不強制要求，但若存在會影響啟動順序 </summary>
		public string[] SoftDepends { get; set; } = Array.Empty<string>();
	}
}

