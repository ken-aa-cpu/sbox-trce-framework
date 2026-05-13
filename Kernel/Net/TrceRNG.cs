using Sandbox;
using System;
using Trce.Kernel.Bridge;

namespace Trce.Kernel.Net

{
	/// <summary>
	/// Server-side deterministic random number generator.
	/// <para>
	/// Ensures that the random seed is consistent across the server for a given round.
	/// The seed is synced to clients via <c>[Sync]</c> so they can use it for UI prediction or visual effects.
	/// </para>
	/// <para>
	/// <b>Security model:</b><br/>
	/// - Only the Server may call <see cref="Next"/>, <see cref="NextFloat"/>, or <see cref="Shuffle{T}"/>.<br/>
	/// - Clients cannot advance the generator, preventing seed manipulation.<br/>
	/// - The current seed is broadcast via <c>[Sync]</c> for read-only client use only.
	/// </para>
	/// <para>
	/// <b>Usage example:</b>
	/// <code>
	/// int value = Scene.Get&lt;TrceRNG&gt;().Next( 0, 10 );
	/// Scene.Get&lt;TrceRNG&gt;().Shuffle( playerList );
	/// </code>
	/// </para>
	/// </summary>
	[Title( "TRCE RNG" ), Group( "Trce - Kernel" )]
	public class TrceRNG : Component
	{
		/// <summary> The random seed for the current round, synced to clients for UI use. </summary>
		[Sync]
		public int CurrentRoundSeed { get; private set; }
		/// <summary> The server-side random number generator instance. </summary>
		private System.Random serverRandom;
		private SandboxBridge _bridge;
		protected override void OnAwake()
		{
			_bridge = SandboxBridge.Instance;
		}

		/// <summary>
		/// Initializes a new random seed tied to the current round lifecycle.
		/// Must be called by the game-mode plugin at the start of each round.
		/// </summary>
		public void InitializeNewRoundSeed()
		{
			if ( !(_bridge?.IsServer ?? false) ) return;
			CurrentRoundSeed = Guid.NewGuid().GetHashCode();
			serverRandom = new System.Random( CurrentRoundSeed );
			Log.Info( $"[RNG] Round seed initialized: {CurrentRoundSeed}" );
		}

		/// <summary>
		/// Returns a random integer in the range [min, max).
		/// Must be called on the Server.
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
		/// Returns a random float in the range [0.0, 1.0].
		/// Must be called on the Server.
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
		/// Shuffles the list in-place using the Fisher-Yates algorithm.
		/// Must be called on the Server.
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

		/// <summary> Picks a random element from the list. Returns <c>default</c> if the list is null or empty. </summary>
		public T PickRandom<T>( System.Collections.Generic.List<T> list )
		{
			if ( !(_bridge?.IsServer ?? false) || serverRandom == null || list == null || list.Count == 0 )
				return default;
			return list[serverRandom.Next( 0, list.Count )];
		}

	}

}
