using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using EntityStates;
using EntityStates.Loader;
using HarmonyLib;
using RoR2;
using UnityEngine;
using CF = HeavySlam.ModConfig;

namespace HeavySlam
{
	public static class ThunderSlamPatch
	{
		// Fast-Reflection fields
		private static readonly MethodInfo M_RollCrit;
		private static readonly AccessTools.FieldRef<BaseState, float> F_damageStat;
		private static readonly AccessTools.FieldRef<GroundSlam, GameObject> F_leftHandEffect;
		private static readonly AccessTools.FieldRef<GroundSlam, GameObject> F_rightHandEffect;

		// Staging Effects
		private static readonly Color Stage1Color = new Color(0.584f, 0.836f, 1f);
		private static readonly Color Stage2Color = new Color(1, 0.367f, 0);
		private static readonly Color Stage3Color = new Color(1, 0, 0.313f);

		// Staging Effects
		private const float Stage1Scale = 0.6f;
		private const float Stage2Scale = 0.8f;
		private const float Stage3Scale = 1f;

		// 'Skill ref -> Speed' dictionary
		private static readonly Dictionary<GroundSlam, float> _slamSpeedDict;
		private static readonly Dictionary<GroundSlam, Tuple<ParticleSystem, ParticleSystem>> _slamParticles;

		// Initialize Member references for fast reflection
		static ThunderSlamPatch()
		{
			// Reference to 'BaseState.RollCrit()' method
			M_RollCrit = AccessTools.Method(typeof(BaseState), "RollCrit");
			// Reference to 'BaseState.damageStat' field
			F_damageStat = AccessTools.FieldRefAccess<BaseState, float>("damageStat");
			// Effect fields
			F_leftHandEffect = AccessTools.FieldRefAccess<GroundSlam, GameObject>("leftFistEffectInstance");
			F_rightHandEffect = AccessTools.FieldRefAccess<GroundSlam, GameObject>("rightFistEffectInstance");

			// Create our dictionary
			_slamSpeedDict = new Dictionary<GroundSlam, float>();

			// Create our particle dictionary
			_slamParticles = new Dictionary<GroundSlam, Tuple<ParticleSystem, ParticleSystem>>();
		}

		// Collect the speed before impact
		public static void SlamHitGround(On.EntityStates.Loader.GroundSlam.orig_OnMovementHit orig, GroundSlam self, ref CharacterMotor.MovementHitInfo movementHitInfo)
		{
			_slamSpeedDict[self] = Math.Abs(movementHitInfo.velocity.y);
			orig(self, ref movementHitInfo);
		}

		// Override the default explode method
		public static BlastAttack.Result CreateExplosion(On.EntityStates.Loader.GroundSlam.orig_DetonateAuthority _, GroundSlam self)
		{
			// Get the velocity from our dictionary
			float velocity = _slamSpeedDict.GetValueSafe(self);
			// Remove the entry from the dictionary
			_slamSpeedDict.Remove(self);
			// Remove Particles Handle
			_slamParticles.Remove(self);

			// Making a local variable to avoid calling the same function twice
			float newRadius = ScaleValue(GroundSlam.blastRadius, velocity, CF.RadiusSpeedCoef.Value, CF.BaseRadiusCoef.Value, CF.ShouldScaleRadius.Value);

			// Original Code
			Vector3 footPosition = self.outer.commonComponents.characterBody.footPosition;
			EffectManager.SpawnEffect(GroundSlam.blastEffectPrefab, new EffectData
			{
				origin = footPosition,
				scale = newRadius,
			}, true);

			// Modified blast attack
			return new BlastAttack
			{
				attacker = self.outer.gameObject,
				// Scale damage
				baseDamage = ScaleValue(F_damageStat(self) * GroundSlam.blastDamageCoefficient, velocity, CF.DamageSpeedCoef.Value, CF.BaseDamageCoef.Value, CF.ShouldScaleDamage.Value),
				// Scale knock-up
				baseForce = ScaleValue(GroundSlam.blastForce, velocity, CF.BlastForceSpeedCoef.Value, CF.BaseBlastForceCoef.Value, CF.ShouldScaleBlastForce.Value),
				bonusForce = GroundSlam.blastBonusForce,
				// Roll critical-hit
				crit = (bool)M_RollCrit.Invoke(self, null),
				damageType = DamageType.Stun1s,
				falloffModel = BlastAttack.FalloffModel.None,
				procCoefficient = GroundSlam.blastProcCoefficient,
				// Scale radius
				radius = newRadius,
				position = footPosition,
				attackerFiltering = AttackerFiltering.NeverHit,
				impactEffect = EffectCatalog.FindEffectIndexFromPrefab(GroundSlam.blastImpactEffectPrefab),
				teamIndex = self.outer.commonComponents.teamComponent.teamIndex
			}.Fire();
		}

		// Modify Effects to signify speed
		public static void ModifyHandEffects(On.EntityStates.Loader.GroundSlam.orig_FixedUpdate orig, GroundSlam self)
		{
			if (CF.ShouldModifyParticles.Value)
			{
				Tuple<ParticleSystem, ParticleSystem> effects;
				if (_slamParticles.ContainsKey(self))
				{
					effects = _slamParticles[self];
				}
				else
				{
					// Ewwwww
					effects = new Tuple<ParticleSystem, ParticleSystem>(
						F_leftHandEffect(self).transform.Find("LoaderLightning").GetComponent<ParticleSystem>(),
						F_rightHandEffect(self).transform.Find("LoaderLightning").GetComponent<ParticleSystem>());

					_slamParticles[self] = effects;
				}

				float velocity = Mathf.Abs(self.outer.commonComponents.characterMotor.velocity.y);
				ChangeParticles(velocity, effects.Item1);
				ChangeParticles(velocity, effects.Item2);
			}

			orig(self);
		}

		// Scale using our formula
		private static float ScaleValue(float val, float speed, float speedCoef, float baseCoef, bool shouldScale)
		{
			val *= baseCoef;
			if (shouldScale && speed < CF.MinimalSpeed.Value)
			{
				return val + (val * (GetModifedSpeed(speed) / 100) * speedCoef);
			}
			else
            {
				return val;
            }
		}

		// Modify speed if necessary
		private static float GetModifedSpeed(float speed)
        {
			return CF.ShouldRemoveMinSpeed.Value ? speed - CF.MinimalSpeed.Value : speed;
        }

		// Not my proudest method, but it will work for now
		private static void ChangeParticles(float speed, ParticleSystem system)
		{
			Color c = Color.white;
			float s = 0.5f;

			Color cN = Stage1Color;
			float sN = Stage1Scale;

			float vS = 0f;
			float vE = 100f;

			if (speed >= 100 && speed < 200)
			{
				c = Stage1Color;
				s = Stage1Scale;
				cN = Stage2Color;
				sN = Stage2Scale;
				vS = 100f;
				vE = 200f;
			}
			else if (speed >= 200 && speed < 300)
			{
				c = Stage2Color;
				s = Stage2Scale;
				cN = Stage3Color;
				sN = Stage3Scale;
				vS = 200f;
				vE = 300f;
			}
			else if (speed >= 300)
			{
				c = Stage3Color;
				s = Stage3Scale;
			}

			if (speed < 300)
			{
				float vL = MapRange(speed, vS, vE, 0f, 1f);

				c = Color.Lerp(c, cN, vL);
				s = Mathf.Lerp(s, sN, vL);
			}

			var main = system.main;
			var color = main.startColor;

			color.color = c;
			color.colorMax = c;
			color.colorMin = c;
			color.mode = ParticleSystemGradientMode.Color;

			main.startSizeMultiplier = s;
			main.startColor = color;
		}

		private static float MapRange(this float value, float baseStart, float baseEnd, float targetStart, float targetEnd)
		{
			return ((value - baseStart) / (baseEnd - baseStart) * (targetEnd - targetStart)) + targetStart;
		}
	}
}
