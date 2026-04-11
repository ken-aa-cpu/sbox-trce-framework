using System.Threading.Tasks;
// ====================================================================
// ����������������������������������������������������������������������������������������������������������������������������������������
// ��  Copyright (c) 2026 TRCE Team. All rights reserved.            ��
// ��  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ��
// ====================================================================
using Sandbox;
using System;
using System.Collections.Generic;
using Trce.Kernel.Net;
using Trce.Kernel.Plugin;
using Trce.Kernel.Plugin.Services;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Finance

{
	/// <summary>
	///   / Currency Manager - Server authoritative economy system with Cloud Sync.
	///   / Provides IEconomyService for TracePoint and TraceCoin operations.
	/// </summary>
	[TrcePlugin(
		Id = "trce.economy",
		Name = "TRCE Economy System",
		Version = "1.2.0 (Cloud)",
		Author = "TRCE Team"
	)]
	public class CurrencyManager : TrcePlugin, IEconomyService
	{
		private static CurrencyManager instance;
		public static CurrencyManager Instance => instance;
		/// <summary> (SteamId, TraceCoin, TracePoint)</summary>
		public Action<ulong, float, float> OnBalanceChanged;
		/// <summary> Player Wallets (SteamID -> [CurrencyType -> Balance])</summary>
		private Dictionary<ulong, Dictionary<CurrencyType, float>> wallets = new();
		/// <summary> Daily Administrator Grant Tracking</summary>
		private Dictionary<ulong, float> todayAdminGrants = new();
		private const float AdminGrantDailyLimit = 500f;
		protected override async Task OnPluginEnabled()
		{
			instance = this;
		}

		// ====================================================================
		// IEconomyService Implementation
		// ====================================================================
		public float GetBalance( ulong steamId ) => GetBalance( steamId, CurrencyType.TracePoint );
		public float Add( ulong steamId, float amount, string reason = "" )
		{
			Add( steamId, CurrencyType.TracePoint, amount, reason );
			return GetBalance( steamId );
		}

		public void Add( ulong steamId, CurrencyType currency, float amount, string reason = "" )
		{
			GrantCurrency( steamId, currency, amount, reason );
		}

		public bool Deduct( ulong steamId, float amount, string reason = "" )
		{
			return Deduct( steamId, CurrencyType.TracePoint, amount, reason );
		}

		public void SetBalance( ulong steamId, float amount )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			EnsureWallet( steamId );
			wallets[steamId][CurrencyType.TracePoint] = amount;
			NotifyBalanceChanged( steamId );
			SyncToCloud( steamId );
		}

		public bool HasEnough( ulong steamId, float amount )
		{
			return GetBalance( steamId ) >= amount;
		}

		public bool Transfer( ulong fromSteamId, ulong toSteamId, float amount )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return false;
			if ( !HasEnough( fromSteamId, amount ) ) return false;
			Deduct( fromSteamId, amount, $"Transfer to {toSteamId}" );
			Add( toSteamId, amount, $"Transfer from {fromSteamId}" );
			return true;
		}

		public float GetBalance( ulong steamId, CurrencyType currency )
		{
			return wallets.TryGetValue( steamId, out var w ) &&
				w.TryGetValue( currency, out float bal ) ? bal : 0f;
		}

		public bool HasEnough( ulong steamId, CurrencyType currency, float amount )
			=> GetBalance( steamId, currency ) >= amount;
		// ====================================================================
		// Server-Only Management Logic
		// ====================================================================
		public bool Deduct( ulong steamId, CurrencyType currency, float amount, string reason )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return false;
			if ( amount <= 0 ) return false;
			float current = GetBalance( steamId, currency );
			if ( current < amount )
			{
				Log.Warning( $"[Economy] Deduction failed: {steamId} {currency} insufficient (Needs {amount}, has {current})" );
				return false;
			}
			EnsureWallet( steamId );
			wallets[steamId][currency] = current - amount;
			NotifyBalanceChanged( steamId );
			SyncToCloud( steamId );
			return true;
		}

		public bool GrantFromSteam( ulong steamId, float amount, string transactionId )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return false;
			if ( amount <= 0 || amount > 10000 ) return false;
			if ( IsTransactionProcessed( transactionId ) )
			{
				Log.Warning( $"[Economy] Replay transaction detected: {transactionId} (Player: {steamId})" );
				return false;
			}
			EnsureWallet( steamId );
			wallets[steamId][CurrencyType.TraceCoin] =
				GetBalance( steamId, CurrencyType.TraceCoin ) + amount;
			MarkTransactionProcessed( transactionId );
			Log.Info( $"[Economy] Steam Purchase: {steamId} +{amount} TC (TX:{transactionId})" );
			NotifyBalanceChanged( steamId );
			SyncToCloud( steamId );
			return true;
		}

		public void GrantCurrency( ulong steamId, CurrencyType currency, float amount, string reason )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) || amount <= 0 ) return;
			EnsureWallet( steamId );
			wallets[steamId][currency] = GetBalance( steamId, currency ) + amount;
			NotifyBalanceChanged( steamId );
			SyncToCloud( steamId );
		}

		// ====================================================================
		// Cloud Synchronization (Sandbox.Services.Stats)
		// ====================================================================
		private void SyncToCloud( ulong steamId )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge == null ) return;
			var conn = bridge.FindConnectionBySteamId( steamId );
			if ( conn == null ) return;
			float tc = GetBalance( steamId, CurrencyType.TraceCoin );
			float tp = GetBalance( steamId, CurrencyType.TracePoint );

			RpcSyncStats( tc, tp );
		}

		[Rpc.Owner]
		private void RpcSyncStats( float tc, float tp )
		{
			var bridge = SandboxBridge.Instance;
			if ( bridge != null )
			{
				bridge.SetStat( "tc", tc );
				bridge.SetStat( "tp", tp );
				Log.Info( $"[Economy] Cloud Balance Synced: TC={tc}, TP={tp}" );
			}

		}

		/// <summary>
		/// </summary>
		[Rpc.Broadcast]
		public void ReportBalanceToServer( float tc, float tp )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return;
			ulong sid = Rpc.Caller.SteamId;
			EnsureWallet( sid );

			wallets[sid][CurrencyType.TraceCoin] = tc;
			wallets[sid][CurrencyType.TracePoint] = tp;

			Log.Info( $"[Economy] Initialized wallet for {sid} from cloud: TC={tc}, TP={tp}" );
			NotifyBalanceChanged( sid );
		}

		// ====================================================================
		// Internal Helpers
		// ====================================================================
		private void EnsureWallet( ulong steamId )
		{
			if ( !wallets.ContainsKey( steamId ) )
				wallets[steamId] = new Dictionary<CurrencyType, float>
				{
					{ CurrencyType.TraceCoin, 0f },
					{ CurrencyType.TracePoint, 0f }
				};
		}

		private void NotifyBalanceChanged( ulong steamId )
		{
			float tc = GetBalance( steamId, CurrencyType.TraceCoin );
			float tp = GetBalance( steamId, CurrencyType.TracePoint );
			OnBalanceChanged?.Invoke( steamId, tc, tp );
		}

		private HashSet<string> processedTransactions = new();
		private bool IsTransactionProcessed( string txId ) => processedTransactions.Contains( txId );
		private void MarkTransactionProcessed( string txId ) => processedTransactions.Add( txId );
	}

}


