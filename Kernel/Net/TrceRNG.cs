using Sandbox;
using System;
using Trce.Kernel.Bridge;

namespace Trce.Kernel.Net

{
	/// <summary>
	/// /  ?? ? ???(Server  ?)
	///
	/// ?�?��??��?輯�??��??�都必�??��?此�??��?得�?
	///   /  ? ? ??Seed ??? ?? ?? ?? ?
	///
	/// 安全?��?�?
	///   - ?��? Server 端可以呼??Next / Shuffle
	///   /   - Client  ? ? ? ??
	/// /   -  ?? ? ? ?Seed
	///   /   - Seed ? ? [Sync] ? ???Client (?  UI  ?? ? ?
	///
	/// 使用?��?�?
	/// /   int value = Scene.Get<TrceRNG>().Next( 0, 10 );
	/// /   Scene.Get<TrceRNG>().Shuffle( playerList );
	/// </summary>
	[Title( "TRCE RNG" ), Group( "Trce - Kernel" )]
	public class TrceRNG : Component
	{
		/// <summary> ? ?? ?? ?(? ?Client ?  UI)</summary>
		[Sync]
		public int CurrentRoundSeed { get; private set; }
		/// <summary>Server  ? ?? ? </summary>
		private System.Random serverRandom;
		private SandboxBridge _bridge;
		protected override void OnAwake()
		{
			_bridge = SandboxBridge.Instance;
		}

		/// <summary>
		/// / ? ? ?? ?? ?? ? ?
		///   /  ? ?? ??  RoundLifecycle ?
		/// </summary>
		public void InitializeNewRoundSeed()
		{
			if ( !(_bridge?.IsServer ?? false) ) return;
			CurrentRoundSeed = Guid.NewGuid().GetHashCode();
			serverRandom = new System.Random( CurrentRoundSeed );
			Log.Info( $"[RNG] ????Seed ???? {CurrentRoundSeed}" );
		}

		/// <summary>
		///   / ? ? ?? ??[min, max)
		/// ?�能??Server 端呼??
		/// </summary>
		public int Next( int min, int max )
		{
			if ( !(_bridge?.IsServer ?? false) || serverRandom == null )
			{
				Log.Error( "[RNG] ?Client ??Gameplay ????" );
				return min;
			}
			return serverRandom.Next( min, max );
		}

		/// <summary>
		/// / ? ? 0.0 ~ 1.0 ?
		/// </summary>
		public float NextFloat()
		{
			if ( !(_bridge?.IsServer ?? false) || serverRandom == null )
			{
				Log.Error( "[RNG] ?Client ??Gameplay ????" );
				return 0f;
			}
			return (float)serverRandom.NextDouble();
		}

		/// <summary>
		///   / Fisher-Yates  ?? ???
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

		/// <summary>
		/// /  ? ? ?? ? ? ? ?
		/// </summary>
		public T PickRandom<T>( System.Collections.Generic.List<T> list )
		{
			if ( !(_bridge?.IsServer ?? false) || serverRandom == null || list == null || list.Count == 0 )
				return default;
			return list[serverRandom.Next( 0, list.Count )];
		}

	}

}

