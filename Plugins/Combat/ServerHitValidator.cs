using Sandbox;
using System;
using System.Collections.Generic;
using Trce.Kernel.Net;

namespace Trce.Plugins.Combat

{
	/// <summary>
	/// Result of a server-side hit validation check.
	/// </summary>
	public class HitValidationResult
	{
		public bool IsValid { get; set; }
		public string RejectReason { get; set; }
		public Vector3 HitPosition { get; set; }
		public GameObject HitObject { get; set; }
		public bool IsHeadshot { get; set; }
		public float Distance { get; set; }
		public float FinalDamage { get; set; }
	}

	/// <summary>
	/// Server-side anti-cheat hit validator.
	/// Checks position, fire-rate, and aim-angle plausibility before accepting a hit.
	/// <para>
	/// P0-4: <c>suspiciousCount</c> and <c>lastFireTime</c> are static; call
	/// <see cref="ResetAll"/> from <see cref="Trce.Kernel.Bridge.SandboxBridge.OnLevelLoaded"/>
	/// on every scene change to prevent cross-scene state contamination.
	/// </para>
	/// </summary>
	public static class ServerHitValidator
	{
		/// <summary>Maximum allowed distance (units) between client-reported origin and server player position.</summary>
		private const float PositionTolerance = 150f;

		/// <summary>Allowable time skew (seconds) for fire-rate checks.</summary>
		private const float TimeTolerance = 0.2f;

		/// <summary>Maximum allowed angle (degrees) between client aim direction and server player facing direction.</summary>
		private const float AngleTolerance = 15f;

		/// <summary>Running count of suspicious events per Steam ID.</summary>
		private static Dictionary<ulong, int> suspiciousCount = new();

		/// <summary>Timestamp of the last processed shot per Steam ID.</summary>
		private static Dictionary<ulong, double> lastFireTime = new();

		/// <summary>
		/// P1-6: Per-player list of violation timestamps (UTC seconds since epoch).
		/// Violations older than <see cref="ViolationDecaySeconds"/> are pruned on every valid hit,
		/// allowing legitimate players to naturally recover from false-positive accumulation.
		/// </summary>
		private static Dictionary<ulong, List<double>> violationTimestamps = new();

		/// <summary>P1-6: Violations older than this many seconds are automatically expired.</summary>
		private const double ViolationDecaySeconds = 60.0;


		/// <summary>
		/// Fired when a player's suspicion count reaches or exceeds the alert threshold.
		/// Parameters: (steamId, violationCount, reasonCode)
		/// </summary>
		public static Action<ulong, int, string> OnSuspiciousPlayer;

		// ====================================================================
		//  Validation Entry Point
		// ====================================================================

		public static HitValidationResult Validate(
			ulong shooterSteamId,
			Vector3 clientOrigin,
			Vector3 clientDirection,
			Vector3 serverPlayerPosition,
			Rotation serverPlayerRotation,
			WeaponDefinition weapon,
			Scene scene )
		{
			var result = new HitValidationResult();

			// Position check
			float posDiff = (clientOrigin - serverPlayerPosition).Length;
			if ( posDiff > PositionTolerance )
			{
				return Reject( result, shooterSteamId, "PositionMismatch",
					$"Position delta {posDiff:F0} > tolerance {PositionTolerance}" );
			}

			// Fire-rate check
			if ( lastFireTime.TryGetValue( shooterSteamId, out double lastTime ) )
			{
				double interval = Time.NowDouble - lastTime;
				double minInterval = weapon.FireRate - TimeTolerance;
				if ( interval < minInterval )
				{
					return Reject( result, shooterSteamId, "FireRateTooFast",
						$"Fire interval {interval:F3}s < min {minInterval:F3}s" );
				}

			}
			lastFireTime[shooterSteamId] = Time.NowDouble;

			// Angle check
			Vector3 serverForward = serverPlayerRotation.Forward;
			float angle = MathF.Acos( Math.Clamp(
				Vector3.Dot( clientDirection.Normal, serverForward.Normal ), -1f, 1f ) );
			float angleDeg = angle * ( 180f / MathF.PI );
			if ( angleDeg > AngleTolerance )
			{
				return Reject( result, shooterSteamId, "AngleMismatch",
					$"Angle delta {angleDeg:F1}° > tolerance {AngleTolerance}°" );
			}

			// Server-side raycast
			var ray = new Ray( serverPlayerPosition + Vector3.Up * 64f, clientDirection.Normal );
			var trace = scene.Trace.Ray( ray, weapon.MaxRange )
				.WithoutTags( "trigger", "player_clip" )
				.Run();
			if ( !trace.Hit )
			{
				result.IsValid = true;
				result.HitPosition = trace.EndPosition;
				result.FinalDamage = 0;
				ClearSuspicion( shooterSteamId );
				return result;
			}
			result.IsValid = true;
			result.HitPosition = trace.HitPosition;
			result.HitObject = trace.GameObject;
			result.Distance = trace.Distance;
			if ( trace.Bone > 0 )
			{
				result.IsHeadshot = trace.Bone <= 3;
			}
			result.FinalDamage = CalculateDamage( weapon, result.Distance, result.IsHeadshot );
			ClearSuspicion( shooterSteamId );
			Log.Info( $"[HitValidator] Hit accepted: {shooterSteamId} -> " +
				$"{trace.GameObject?.Name} ({result.FinalDamage:F0} dmg, {result.Distance:F0}m" +
				$"{( result.IsHeadshot ? ", headshot!" : "" )})" );
			return result;
		}

		// ====================================================================
		//  Damage Calculation
		// ====================================================================

		public static float CalculateDamage( WeaponDefinition weapon, float distance, bool isHeadshot )
		{
			float damage = weapon.BaseDamage;
			if ( distance > weapon.FalloffStart )
			{
				float falloffRange = weapon.FalloffEnd - weapon.FalloffStart;
				float falloffProgress = Math.Clamp( ( distance - weapon.FalloffStart ) / falloffRange, 0f, 1f );
				float multiplier = 1f - ( 1f - weapon.MinDamagePercent ) * falloffProgress;
				damage *= multiplier;
			}
			if ( isHeadshot )
				damage *= weapon.HeadshotMultiplier;
			return damage;
		}

		// ====================================================================
		//  Suspicion Management
		// ====================================================================

		private static HitValidationResult Reject( HitValidationResult result, ulong steamId, string code, string detail )
		{
			result.IsValid = false;
			result.RejectReason = code;

			// P1-6: Record violation timestamp instead of incrementing a decrement-only counter.
			if ( !violationTimestamps.TryGetValue( steamId, out var timestamps ) )
			{
				timestamps = new List<double>();
				violationTimestamps[steamId] = timestamps;
			}
			timestamps.Add( Time.NowDouble );

			// Also increment legacy counter for backwards-compat with OnSuspiciousPlayer event.
			if ( !suspiciousCount.ContainsKey( steamId ) )
				suspiciousCount[steamId] = 0;
			suspiciousCount[steamId]++;
			int count = suspiciousCount[steamId];

			Log.Warning( $"[HitValidator] Suspicious activity ({code}): {detail} " +
				$"[Player: {steamId}, violations: {count}]" );
			if ( count >= 10 )
			{
				Log.Error( $"[HitValidator] ALERT: Player {steamId} has {count} violations." );
				OnSuspiciousPlayer?.Invoke( steamId, count, code );
			}
			return result;
		}

		/// <summary>
		/// P1-6: Prunes violations older than <see cref="ViolationDecaySeconds"/> for the given player.
		/// Replaces the old single-decrement pattern so legitimate players naturally recover.
		/// </summary>
		private static void ClearSuspicion( ulong steamId )
		{
			// Decay time-based violation list.
			if ( violationTimestamps.TryGetValue( steamId, out var timestamps ) )
			{
				double cutoff = Time.NowDouble - ViolationDecaySeconds;
				timestamps.RemoveAll( t => t < cutoff );

				// Sync the legacy suspiciousCount to reflect the remaining active violations.
				suspiciousCount[steamId] = timestamps.Count;

				if ( timestamps.Count == 0 )
					violationTimestamps.Remove( steamId );
			}
			else if ( suspiciousCount.TryGetValue( steamId, out int count ) && count > 0 )
			{
				// Legacy fallback: still decrement if no timestamp list exists yet.
				suspiciousCount[steamId] = Math.Max( 0, count - 1 );
			}
		}

		/// <summary>
		/// P0-4: Clears all static hit-validation state.
		/// Called by <see cref="Trce.Kernel.Bridge.SandboxBridge.OnLevelLoaded"/> on every scene change.
		/// </summary>
		public static void ResetAll()
		{
			suspiciousCount.Clear();
			lastFireTime.Clear();
			violationTimestamps.Clear(); // P1-6: Also clear the timestamp lists.
		}

	}

}

