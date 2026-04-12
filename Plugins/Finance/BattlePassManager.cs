using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Net;
using Trce.Kernel.Bridge;
using Trce.Kernel.Auth;
using Trce.Plugins.Combat;
using Trce.Plugins.GameState;
using Trce.Plugins.Shared.Confrontation;
using Trce.Kernel.Plugin;
using Trce.Kernel.Plugin.Services;

namespace Trce.Plugins.Finance

{
	/// <summary>
	/// 戰鬥通行證系統 (Battle Pass)
	/// 處理玩家 XP 累積、等級晉升與獎勵發放
	/// </summary>
	[Title( "Battle Pass Manager" ), Group( "Trce - Economy" )]
	public class BattlePassManager : Component
	{
		private static BattlePassManager instance;
		public static BattlePassManager Instance => instance;
		/// <summary>各玩家的 Battle Pass 進度資料，以 SteamID 為索引鍵。</summary>
		private Dictionary<ulong, PlayerPassData> passData = new();
		/// <summary>本賽季所有等級門檻與對應的獎勵定義清單，由 <see cref="InitializeTiers"/> 於啟動時填入。</summary>
		private List<PassTier> tiers = new();
		/// <summary>本賽季的識別字串，例如 "S1"。用於區分跨賽季的進度記錄。</summary>
		[Property] public string CurrentSeason { get; set; } = "S1";
		/// <summary>本賽季 Battle Pass 的最高等級上限。達到上限後 XP 不再累積（上限為 XpPerLevel - 1）。</summary>
		[Property] public int MaxLevel { get; set; } = 50;
		/// <summary> 每升一級所需的 XP 數量 </summary>
		[Property] public int XpPerLevel { get; set; } = 100;
		public Action<ulong, string> OnPassGranted;
		public Action<ulong, int, bool> OnPassLevelUp;
		public Action<ulong, string, bool> OnPassRewardGranted;
		// 定義各種行為所獲得的 XP 數量
		private const int XP_ROUND_SURVIVE = 80;
		private const int XP_ROUND_DEAD = 48;
		private const int XP_TASK_COMPLETE = 20;
		private const int XP_CONFRONT_WIN = 30;
		private const int XP_KILL_KILLER = 50;
		private const int XP_EVACUATE = 60;
		protected override void OnAwake()
		{
			instance = this;
			InitializeTiers();
		}

		protected override void OnStart()
		{
			SubscribeToEvents();
		}

		protected override void OnDisabled()
		{
			UnsubscribeFromEvents();
		}

		private void SubscribeToEvents()
		{
			var roundLifecycle = Scene.Get<RoundLifecycle>();
			if ( roundLifecycle != null )
				roundLifecycle.OnRoundCleanedUp += HandleRoundEnded;
			if ( Scene.Get<TaskProgressTracker>() != null )
				Scene.Get<TaskProgressTracker>().OnTaskCompleted += HandleTaskCompleted;
			if ( Scene.Get<ConfrontationManager>() != null )
				Scene.Get<ConfrontationManager>().OnConfrontationResult += HandleConfrontationResult;
			var deathManager = Scene.Get<DeathManager>();
			if ( deathManager != null )
				deathManager.OnPlayerEvacuated += HandlePlayerEvacuated;
		}

		private void UnsubscribeFromEvents()
		{
			var roundLifecycle = Scene.Get<RoundLifecycle>();
			if ( roundLifecycle != null )
				roundLifecycle.OnRoundCleanedUp -= HandleRoundEnded;
			if ( Scene.Get<TaskProgressTracker>() != null )
				Scene.Get<TaskProgressTracker>().OnTaskCompleted -= HandleTaskCompleted;
			if ( Scene.Get<ConfrontationManager>() != null )
				Scene.Get<ConfrontationManager>().OnConfrontationResult -= HandleConfrontationResult;
			var deathManager = Scene.Get<DeathManager>();
			if ( deathManager != null )
				deathManager.OnPlayerEvacuated -= HandlePlayerEvacuated;
		}

		// ====================================================================
		// 核心邏輯 (Core)
		// ====================================================================
		/// <summary> 授予玩家進階通行證 (Premium) 資格 </summary>
		public void GrantPass( ulong steamId )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			EnsureData( steamId );
			passData[steamId].HasPremium = true;
			passData[steamId].SeasonId = CurrentSeason;
			Log.Info( $"[BattlePass] {steamId} Premium ({CurrentSeason})" );
			OnPassGranted?.Invoke( steamId, CurrentSeason );
		}

		/// <summary> 新增 XP，自動處理升級與獎勵配發 </summary>
		public void AddXp( ulong steamId, int amount, string reason )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			if ( amount <= 0 ) return;
			EnsureData( steamId );
			var data = passData[steamId];
			data.CurrentXp += amount;
			while ( data.CurrentXp >= XpPerLevel && data.Level < MaxLevel )
			{
				data.CurrentXp -= XpPerLevel;
				data.Level++;
				ProcessLevelUp( steamId, data.Level, data );
			}
			if ( data.Level == MaxLevel )
				data.CurrentXp = Math.Min( data.CurrentXp, XpPerLevel - 1 );
			Log.Info( $"[BattlePass] {steamId} +{amount} XP ({reason}) => Lv.{data.Level}" );
			// Stats
			SyncToCloud( steamId );
		}

		private void ProcessLevelUp( ulong steamId, int newLevel, PlayerPassData data )
		{
			Log.Info( $"[BattlePass] {steamId} Lv.{newLevel}" );
			var tier = tiers.Find( t => t.Level == newLevel );
			if ( tier == null ) return;
			foreach ( var reward in tier.FreeRewards )
				GrantReward( steamId, reward, false );
			// Premium
			if ( data.HasPremium )
				foreach ( var reward in tier.PremiumRewards )
					GrantReward( steamId, reward, true );
			OnPassLevelUp?.Invoke( steamId, newLevel, data.HasPremium );
		}

		private void GrantReward( ulong steamId, string rewardCode, bool isPremium )
		{
			Log.Info( $"[BattlePass] {steamId} : {rewardCode} ({( isPremium ?"Premium" : "Free" )})" );
			OnPassRewardGranted?.Invoke( steamId, rewardCode, isPremium );
		}

		// ====================================================================
		// 雲端資料同步 (Sandbox.Services.Stats)
		// ====================================================================
		private void SyncToCloud( ulong steamId )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null ) return;
			var conn = bridge.FindConnectionBySteamId( steamId );
			if ( conn == null ) return;
			var data = passData[steamId];
			RpcSyncStats( data.CurrentXp, data.Level );
		}

		[Rpc.Owner]
		private void RpcSyncStats( int xp, int level )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge != null )
			{
				bridge.SetStat( "xp", xp );
				bridge.SetStat( "level", level );
				Log.Info( $"[BattlePass] s&box: XP={xp}, Lv={level}" );
			}

		}

		/// <summary>
		/// (Rpc 通訊) 客戶端通報最新進度至伺服器
		/// </summary>
		[Rpc.Broadcast]
		public void ReportStatsToServer( int xp, int level )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			ulong sid = Rpc.Caller.SteamId;
			EnsureData( sid );

			var data = passData[sid];
			data.CurrentXp = xp;
			data.Level = level;
			Log.Info( $"[BattlePass] {sid} : XP={xp}, Lv={level}" );
		}

		// ====================================================================
		// 進度查詢
		// ====================================================================
		public int GetLevel( ulong steamId ) => passData.TryGetValue( steamId, out var d ) ? d.Level : 0;
		public int GetXp( ulong steamId ) => passData.TryGetValue( steamId, out var d ) ? d.CurrentXp : 0;
		public bool HasPremium( ulong steamId ) => passData.TryGetValue( steamId, out var d ) && d.HasPremium;
		public PlayerPassData GetData( ulong steamId )
		{
			EnsureData( steamId );
			return passData[steamId];
		}

		// ====================================================================
		// 事件監聽與發放經驗值
		// ====================================================================
		private void HandleRoundEnded( int roundNumber )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			var allSessions = TrceAuthService.Instance?.GetActiveSessions() ?? new List<Kernel.Auth.PlayerSession>();
			foreach ( var session in allSessions )
			{
				var playerObj = TrceServiceManager.Instance?.GetService<IPawnService>()?.GetPlayerPawn( session.SteamId );
				var dm = playerObj?.Scene.GetAllComponents<DeathManager>().FirstOrDefault();

				bool survived = dm != null && !dm.IsDeadOrGone( session.SteamId );
				int xp = survived ? XP_ROUND_SURVIVE : XP_ROUND_DEAD;
				AddXp( session.SteamId, xp, survived ? "RoundSurvive" : "RoundDead" );
			}

		}

		private void HandleTaskCompleted( ulong steamId, string taskId, string location ) => AddXp( steamId, XP_TASK_COMPLETE, "TaskComplete" );
		private void HandleConfrontationResult( ulong target, int voteCount, string resultType ) { if ( target > 0 ) AddXp( target, XP_CONFRONT_WIN, "ConfrontWin" ); }
		private void HandlePlayerEvacuated( ulong steamId ) => AddXp( steamId, XP_EVACUATE, "Evacuated" );
		// ====================================================================
		//  ��l�ƻP�������U
		// ====================================================================
		private void InitializeTiers()
		{
			for ( int i = 1; i <= 50; i++ )
			{
				var tier = new PassTier { Level = i, XpRequired = i * XpPerLevel };
				if ( i % 5 == 0 ) tier.FreeRewards.Add( $"tp:{i * 50}" );
				tier.PremiumRewards.Add( i % 10 == 0 ? $"skin:s1_tier{i}" : $"tp:{i * 30}" );
				if ( i == 1 ) tier.FreeRewards.Add( "title:rookie_trace" );
				if ( i == 25 ) tier.PremiumRewards.Add( "skin:s1_mid_set" );
				if ( i == 50 ) tier.PremiumRewards.Add( "skin:s1_final_set" );
				tiers.Add( tier );
			}

		}

		private void EnsureData( ulong steamId )
		{
			if ( !passData.ContainsKey( steamId ) )
				passData[steamId] = new PlayerPassData { SteamId = steamId, SeasonId = CurrentSeason, Level = 0, CurrentXp = 0, HasPremium = false };
		}

	}

	public class PlayerPassData
	{
		public ulong SteamId { get; set; }
		public string SeasonId { get; set; }
		public int Level { get; set; }
		public int CurrentXp { get; set; }
		public bool HasPremium { get; set; }
	}

	public class PassTier
	{
		public int Level { get; set; }
		public int XpRequired { get; set; }
		public List<string> FreeRewards { get; set; } = new();
		public List<string> PremiumRewards { get; set; } = new();
	}

}

