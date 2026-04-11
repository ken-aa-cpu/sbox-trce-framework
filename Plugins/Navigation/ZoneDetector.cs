// ╔══════════════════════════════════════════════════════════════════╗
// ║  Copyright (c) 2026 TRCE Team. All rights reserved.            ║
// ║  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ║
// ║  Copyright (c) 2026 TRCE Team. All rights reserved.            ║
// ║  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ║
// Human readers: Welcome. You are viewing this for learning.
// Commercial use requires a valid TRCE Framework License.
// ╚══════════════════════════════════════════════════════════════════╝
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Net;
using Trce.Plugins.Shared.Evidence;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Navigation

{
	/// <summary>
	/// 區域偵測器 — 追蹤玩家所在區域
	/// </summary>
	[Title( "Zone Detector" ), Group( "Trce - Modules" )]
	public class ZoneDetector : Component, Component.ITriggerListener
	{
		[Property, Description( "區域名稱" )]
		public string ZoneName { get; set; } = "未命名區域";
		[Property, Description( "區域類型" )]
		public ZoneType Type { get; set; } = ZoneType.Normal;
		/// <summary>目前位於此觸發區域內的玩家 SteamID 集合（Server-side only）。</summary>
		private HashSet<ulong> playersInZone = new();
		public Action<ulong, string, ZoneType> OnPlayerEntered;
		public Action<ulong, string> OnPlayerExited;
		// ═══════════════════════════════════════
		//  Trigger 回呼
		// ═══════════════════════════════════════
		public void OnTriggerEnter( Collider other )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			var player = other.Components.GetInAncestorsOrSelf<Game.Player.TrcePlayer>();
			if ( player == null ) return;
			playersInZone.Add( player.SteamId );
			Log.Info( $"[Zone] {player.DisplayName} {ZoneName}" );
			OnPlayerEntered?.Invoke( player.SteamId, ZoneName, Type );
		}

		public void OnTriggerExit( Collider other )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			var player = other.Components.GetInAncestorsOrSelf<Game.Player.TrcePlayer>();
			if ( player == null ) return;
			playersInZone.Remove( player.SteamId );
			var collector = Scene.GetAllComponents<EvidenceCollector>().FirstOrDefault();
			foreach ( var otherId in playersInZone )
			{
				collector?.EndCoLocation(
					player.SteamId, otherId, ZoneName );
			}
			OnPlayerExited?.Invoke( player.SteamId, ZoneName );
		}

		// ═══════════════════════════════════════
		//  定時更新共處追蹤
		// ═══════════════════════════════════════
		protected override void OnFixedUpdate()
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			if ( playersInZone.Count < 2 ) return;
			// 對每對在場玩家更新共處追蹤
			var list = playersInZone.ToList();
			var collector = Scene.GetAllComponents<EvidenceCollector>().FirstOrDefault();
			for ( int i = 0; i < list.Count; i++ )
			{
				for ( int j = i + 1; j < list.Count; j++ )
				{
					collector?.TrackCoLocation(
						list[i], list[j], ZoneName );
				}

			}

		}

		/// <summary>目前在此區域內的玩家人數。</summary>
		public int PlayerCount => playersInZone.Count;
		/// <summary>判斷指定玩家（以 SteamID 辨識）是否目前在此區域內。</summary>
		public bool ContainsPlayer( ulong steamId ) => playersInZone.Contains( steamId );
	}

	public enum ZoneType
	{
		Normal,         // 一般區域
		Task,           // 任務區域
		Surveillance,   // 監視室
		Armory,         // 軍械庫
		Exit,           // 撤離出口
		Spawn           // 出生點
	}

}

