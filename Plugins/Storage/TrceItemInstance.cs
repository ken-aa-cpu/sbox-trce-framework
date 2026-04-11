// ����������������������������������������������������������������������������������������������������������������������������������������
// ��  Copyright (c) 2026 TRCE Team. All rights reserved.            ��
// ��  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ��
using Sandbox;
using System;
using System.Text;
using Trce.Kernel.Auth;

namespace Trce.Plugins.Storage

{
	/// <summary>
	///   / TRCE-Items   (Item Instance)
	///
	///   /   TrceItemInstance
	///   /   (TrceItemDefinition)
	///
	/// �w������G
	///   /   -   UID (Guid)  ID
	///   /   - HMAC   HMAC
	/// </summary>
	public class TrceItemInstance
	{
		// ������������������������������������ �����ѧO ������������������������������������
		/// <summary> ID ( )</summary>
		public string Uid { get; private set; }
		/// <summary> ID</summary>
		public string ItemId { get; private set; }
		// ������������������������������������ ���A ������������������������������������
		public int Quantity { get; private set; }
		public System.Collections.Generic.Dictionary<string, float> DynamicStats { get; private set; } = new();
		/// <summary> ( : "quest_item", "stolen")</summary>
		public System.Collections.Generic.List<string> Tags { get; private set; } = new();
		/// <summary> (NBT  : { "lore": " ", "color": "#FF0000" })</summary>
		public System.Collections.Generic.Dictionary<string, string> JsonData { get; private set; } = new();
		/// <summary> HMAC</summary>
		public string Signature { get; private set; }
		// ������������������������������������ �غc ������������������������������������
		private TrceItemInstance() { }
		/// <summary>
		/// /   Server
		///   /   new  UID
		/// </summary>
		public static TrceItemInstance Create( string itemId, int quantity = 1 )
		{
			var instance = new TrceItemInstance
			{
				Uid = Guid.NewGuid().ToString( "N" )[..16].ToUpper(), // 16 �X�j�g UID
				ItemId = itemId,
				Quantity = quantity,
			};
			instance.Sign();
			return instance;
		}

		/// <summary> N   true</summary>
		public bool ConsumeOne( int amount = 1 )
		{
			Quantity = Math.Max( 0, Quantity - amount );
			Sign();
			return Quantity <= 0;
		}

		/// <summary>將指定數量疊加至本物品的 Quantity，並重新計算 HMAC 防篡改簽名。</summary>
		public void Stack( int amount )
		{
			Quantity += amount;
			Sign();
		}

		/// <summary> Stat</summary>
		public void SetStat( string key, float value )
		{
			DynamicStats[key] = value;
			Sign();
		}

		/// <summary>設定物品的自訂 JSON 後設資料欄位（類似 NBT），並重新計算 HMAC 簽名。</summary>
		public void SetData( string key, string value )
		{
			JsonData[key] = value;
			Sign();
		}

		public void AddTag( string tag )
		{
			if ( !Tags.Contains( tag ) )
			{
				Tags.Add( tag );
				Sign();
			}

		}

		// ������������������������������������ HMAC �w�� ������������������������������������
		/// <summary> HMAC</summary>
		private void Sign()
		{
			string payload = $"{Uid}|{ItemId}|{Quantity}";
			Signature = Kernel.Security.HmacSigner.Sign( payload, Kernel.Identity.TrceFingerprint.BuildId );
		}

		/// <summary>驗證本物品的 HMAC 簽名是否與當前狀態相符。回傳 <c>false</c> 表示物品資料已遭竄改。</summary>
		public bool VerifyIntegrity()
		{
			string payload = $"{Uid}|{ItemId}|{Quantity}";
			string expected = Kernel.Security.HmacSigner.Sign( payload, Kernel.Identity.TrceFingerprint.BuildId );
			return expected == Signature;
		}

		public override string ToString()
			=> $"[TrceItem] {ItemId} x{Quantity} (UID: {Uid})";
	}

}

