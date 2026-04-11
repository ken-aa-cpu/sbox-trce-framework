using Sandbox;
using System;
using Trce.Kernel.Bridge;

namespace Trce.Kernel.Net

{
	/// <summary>
	/// 伺服器端亂數產生器 (Server)
	///
	/// 確保隨機數種子 (Seed) 一致
	///
	/// 安全性:
	///   - 只有 Server 可以呼叫 Next / Shuffle
	///   - Client 無法呼叫，以防修改 Seed
	///   - Seed 會透過 [Sync] 同步到 Client (供 UI 即時預測或顯示特效用)
	///
	/// 使用範例:
	///    int value = Scene.Get<TrceRNG>().Next( 0, 10 );
	///    Scene.Get<TrceRNG>().Shuffle( playerList );
	/// </summary>
	[Title( "TRCE RNG" ), Group( "Trce - Kernel" )]
	public class TrceRNG : Component
	{
		/// <summary> 目前回合的亂數種子 (同步到 Client 供 UI 使用) </summary>
		[Sync]
		public int CurrentRoundSeed { get; private set; }
		/// <summary> Server 端使用的亂數產生器 </summary>
		private System.Random serverRandom;
		private SandboxBridge _bridge;
		protected override void OnAwake()
		{
			_bridge = SandboxBridge.Instance;
		}

		/// <summary>
		/// 根據回合生命週期 (RoundLifecycle) 初始化亂數種子
		/// </summary>
		public void InitializeNewRoundSeed()
		{
			if ( !(_bridge?.IsServer ?? false) ) return;
			CurrentRoundSeed = Guid.NewGuid().GetHashCode();
			serverRandom = new System.Random( CurrentRoundSeed );
			Log.Info( $"[RNG] Round seed initialized: {CurrentRoundSeed}" );
		}

		/// <summary>
		/// 產生介於 [min, max) 之間的隨機整數
		/// 必須在 Server 端呼叫
		/// </summary>
		public int Next( int min, int max )
		{
			if ( !(_bridge?.IsServer ?? false) || serverRandom == null )
			{
				Log.Error( "[RNG] Cannot generate random number from Client in Gameplay" );
				return min;
			}
			return serverRandom.Next( min, max );
		}

		/// <summary>
		/// 產生 0.0 ~ 1.0 的隨機浮點數
		/// </summary>
		public float NextFloat()
		{
			if ( !(_bridge?.IsServer ?? false) || serverRandom == null )
			{
				Log.Error( "[RNG] Cannot generate random float from Client in Gameplay" );
				return 0f;
			}
			return (float)serverRandom.NextDouble();
		}

		/// <summary>
		/// 使用 Fisher-Yates 演算法洗牌
		/// </summary>
		public void Shuffle<T>( System.Collections.Generic.List<T> list )
		{
			if ( !(_bridge?.IsServer ?? false) || serverRandom == null ) return;
			int n = list.Count;
			while ( n > 1 )
			{
				n--;
				int k = serverRandom.Next( n + 1 );
				(list[k], list[n]) = (list[n], list[k]);
			}

		}

		/// <summary> 從列表中隨機挑選一個元素 </summary>
		public T PickRandom<T>( System.Collections.Generic.List<T> list )
		{
			if ( !(_bridge?.IsServer ?? false) || serverRandom == null || list == null || list.Count == 0 )
				return default;
			return list[serverRandom.Next( 0, list.Count )];
		}

	}

}

