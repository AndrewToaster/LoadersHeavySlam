using System;
using System.Collections.Generic;
using System.Reflection;
using EntityStates;
using EntityStates.Loader;
using HarmonyLib;
using RoR2;
using UnityEngine;

namespace HeavySlam
{
	// I have to do this terribleness because there is not a 'IL.' or 'On.' event implemented yet
	[HarmonyPatch(typeof(GroundSlam))]
	internal static class GroundSlamPatch
	{
		// Fast-Reflection fields
		private static MethodInfo M_RollCrit;
		private static AccessTools.FieldRef<BaseState, float> F_damageStat;

		// 'Skill ref -> Speed' dictionary
		private static readonly Dictionary<GroundSlam, float> _slamSpeedDict = new Dictionary<GroundSlam, float>();

		private static bool _init;

		// Initialize Member references for fast reflection
		private static void Init()
        {
			// Reference to 'BaseState.RollCrit()' method
			M_RollCrit = AccessTools.Method(typeof(BaseState), "RollCrit");
			// Reference to 'BaseState.damageStat' field
			F_damageStat = AccessTools.FieldRefAccess<BaseState, float>("damageStat");
			_init = true;
        }

		// Collect the speed before impact
		[HarmonyPrefix]
		[HarmonyPatch("OnMovementHit")]
		internal static bool OnMovementHit(CharacterMotor.MovementHitInfo movementHitInfo, GroundSlam __instance)
		{
			_slamSpeedDict[__instance] = movementHitInfo.velocity.y;
			return true;
		}

		// Override the default explode method
		[HarmonyPrefix]
		[HarmonyPatch("DetonateAuthority")]
		internal static bool DetonateAuthority(ref BlastAttack.Result __result, GroundSlam __instance)
		{
			// Check if fast-reflection is initialized
			if (!_init)
				Init();

			// Get the velocity from our dictionary
			float velocity = Mathf.Abs(_slamSpeedDict.GetValueSafe(__instance));
			// Remove the entry from the dictionary
			_slamSpeedDict.Remove(__instance);

			// <--- Original Code --->
			Vector3 footPosition = __instance.outer.commonComponents.characterBody.footPosition;
			EffectManager.SpawnEffect(GroundSlam.blastEffectPrefab, new EffectData
			{
				origin = footPosition,
				scale = GroundSlam.blastRadius
			}, true);
			// <--- End of Original Code --->

			__result = new BlastAttack
			{
				attacker = __instance.outer.gameObject,
				// Scale damage
				baseDamage = ScaleValue(F_damageStat(__instance) * GroundSlam.blastDamageCoefficient, velocity, ModConfig.DamageSpeedCoef.Value),
				// Scale knock-up
				baseForce = ScaleValue(GroundSlam.blastForce, velocity, ModConfig.BlastForceSpeedCoef.Value),
				bonusForce = GroundSlam.blastBonusForce,
				// Roll critical-hit
				crit = (bool)M_RollCrit.Invoke(__instance, null),
				damageType = DamageType.Stun1s,
				falloffModel = BlastAttack.FalloffModel.None,
				procCoefficient = GroundSlam.blastProcCoefficient,
				// Scale radius
				radius = ScaleValue(GroundSlam.blastRadius, velocity, ModConfig.RadiusSpeedCoef.Value),
				position = footPosition,
				attackerFiltering = AttackerFiltering.NeverHit,
				impactEffect = EffectCatalog.FindEffectIndexFromPrefab(GroundSlam.blastImpactEffectPrefab),
				teamIndex = __instance.outer.commonComponents.teamComponent.teamIndex
			}.Fire();

			// Skip the original method
			return false;
		}

		// Scale using our formula
		private static float ScaleValue(float val, float speed, float coef)
        {
			return speed < ModConfig.MinimalSpeed.Value ? val : val + (val * (speed / 100) * coef);
        }

		/*private static float GetDamage(float defaultDamage, float speed)
        {
			float val;
			if (speed < minimalSpeed)
				val = defaultDamage;
			else
				val = defaultDamage + (defaultDamage * (speed / 100) * damageSpeedCoef);
#if DEBUG
			ModPlugin.Log.LogInfo($"REPORT DMG: Speed -> {speed} | Normal -> {defaultDamage} | New -> {val}");
#endif
			return val;
        }

		private static float GetRadius(float defaultRadius, float speed)
		{
			float val;
			if (speed < minimalSpeed)
				val = defaultRadius;
			else
				val = defaultRadius + (defaultRadius * (speed / 100) * radiusSpeedCoef);
#if DEBUG
			ModPlugin.Log.LogInfo($"REPORT RADIUS: Speed -> {speed} | Normal -> {defaultRadius} | New -> {val}");
#endif
			return val;
		}

		private static float GetBlast(float defaultBlast, float speed)
		{
			float val;
			if (speed < minimalSpeed)
				val = defaultBlast;
			else
				val = defaultBlast + (defaultBlast * (speed / 100) * blastForceSpeedCoef);
#if DEBUG
			ModPlugin.Log.LogInfo($"REPORT BLAST: Speed -> {speed} | Normal -> {defaultBlast} | New -> {val}");
#endif
			return val;
		}*/
	}
}
