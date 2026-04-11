using System;

namespace Trce.Kernel.Plugin
{
	/// <summary>
// ����������������������������������������������������������������������������������������������������������������������������������������
// ��  TRCE FRAMEWORK �X PROPRIETARY SOURCE CODE                       ��
// ��  Copyright (c) 2026 TRCE Team. All rights reserved.            ��
// ��  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ��
// ����������������������������������������������������������������������������������������������������������������������������������������
	///
// ����������������������������������������������������������������������������������������������������������������������������������������
// ��  TRCE FRAMEWORK �X PROPRIETARY SOURCE CODE                       ��
// ��  Copyright (c) 2026 TRCE Team. All rights reserved.            ��
// ��  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ��
// ����������������������������������������������������������������������������������������������������������������������������������������
	/// </summary>
	[AttributeUsage( AttributeTargets.Class, Inherited = false, AllowMultiple = false )]
	public class TrcePluginAttribute : Attribute
	{
		/// <summary> ? ? ? ? ? "trce.inventory" ??"game.murder_mystery"</summary>
		public string Id { get; set; }

		/// <summary> ? ??Log ? ? ?</summary>
		public string Name { get; set; }

		/// <summary>? </summary>
		public string Version { get; set; } = "1.0.0";

		/// <summary> </summary>
		public string Author { get; set; } = "Unknown";

		/// <summary>
		///   /  ? ? ? ? ?ID
		///   / Manager ? ? ? ? ?? ? ???(OnEnable)??
		///   / ? ? ? ? ? ? ?? ?
		/// </summary>
		public string[] Depends { get; set; } = Array.Empty<string>();

		/// <summary>
		///   /  ? ? ? ? ? ? ? ? ?? ? ? ? ? ?? ???
		/// </summary>
		public string[] SoftDepends { get; set; } = Array.Empty<string>();
	}
}

