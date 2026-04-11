using Sandbox;
using System;
using System.Collections.Generic;
using Trce.Kernel.Net;

namespace Trce.Plugins.Combat

{
	/// <summary>
	/// Server 端�??��?證�???
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
	///   / Server  ? ?  ??? ? ?
	/// </summary>
	public static class ServerHitValidator
	{
		/// <summary> (Client ??Server  ?)</summary>
		private const float PositionTolerance = 150f;
		/// <summary> ? ?  ( ?)</summary>
		private const float TimeTolerance = 0.2f;
		/// <summary> ( ?</summary>
		private const float AngleTolerance = 15f;
		/// <summary> ? ?</summary>
		private static Dictionary<ulong, int> suspiciousCount = new();
		/// <summary> ? ?? ?</summary>
		private static Dictionary<ulong, double> lastFireTime = new();
		/// <summary> ? ? ? ? ??(SteamId, ? ? , ? ? )</summary>
		public static Action<ulong, int, string> OnSuspiciousPlayer;
		// ?��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��???
		//  ?��?驗�?
		// ?��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��??��???
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
			// 位置驗?
			float posDiff = (clientOrigin - serverPlayerPosition).Length;
			if ( posDiff > PositionTolerance )
			{
				return Reject( result, shooterSteamId, "PositionMismatch",
					$"Position delta {posDiff:F0} > tolerance {PositionTolerance}" );
			}

			// 射???驗?
			if ( lastFireTime.TryGetValue( shooterSteamId, out double lastTime ) )
			{
				double interval = Time.NowDouble - lastTime;
				double minInterval = weapon.FireRate - TimeTolerance; // Changed to double for consistency
				if ( interval < minInterval )
				{
					return Reject( result, shooterSteamId, "FireRateTooFast",
						$"Fire interval {interval:F3}s < min {minInterval:F3}s" );
				}

			}
			lastFireTime[shooterSteamId] = Time.NowDouble;
			// 角度驗?
			Vector3 serverForward = serverPlayerRotation.Forward;
			float angle = MathF.Acos( Math.Clamp(
				Vector3.Dot( clientDirection.Normal, serverForward.Normal ), -1f, 1f ) );
			float angleDeg = angle * ( 180f / MathF.PI );
			if ( angleDeg > AngleTolerance )
			{
				return Reject( result, shooterSteamId, "AngleMismatch",
					$"Angle delta {angleDeg:F1}° > tolerance {AngleTolerance}°" );
			}

			// Server  ?Raycast
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
			Log.Info( $"[HitValidator] ? ????: {shooterSteamId} ??" +
				$"{trace.GameObject?.Name} ({result.FinalDamage:F0} dmg, {result.Distance:F0}m" +
				$"{( result.IsHeadshot ? ", headshot!" : "" )})" );
			return result;
		}

		// ????????????????????????????????????????
		// ????????????????????????????????????????
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

		// ????????????????????????????????????????
		//  ?常管?
		// ????????????????????????????????????????
		private static HitValidationResult Reject( HitValidationResult result, ulong steamId, string code, string detail )
		{
			result.IsValid = false;
			result.RejectReason = code;
			if ( !suspiciousCount.ContainsKey( steamId ) )
				suspiciousCount[steamId] = 0;
			suspiciousCount[steamId]++;
			int count = suspiciousCount[steamId];
			Log.Warning( $"[HitValidator] ? ??? ({code}): {detail}" +
				$"[Player: {steamId}, violations: {count}]" );
			if ( count >= 10 )
			{
				Log.Error( $"[HitValidator] ?? ? {steamId} ???{count}??" );
				OnSuspiciousPlayer?.Invoke( steamId, count, code );
			}
			return result;
		}

		private static void ClearSuspicion( ulong steamId )
		{
			if ( suspiciousCount.TryGetValue( steamId, out int count ) && count > 0 )
			{
				suspiciousCount[steamId] = Math.Max( 0, count - 1 );
			}

		}

		/// <summary> ? ??</summary>
		public static void ResetAll()
		{
			suspiciousCount.Clear();
			lastFireTime.Clear();
		}

	}

}

