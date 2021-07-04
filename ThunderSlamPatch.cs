using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using EntityStates;
using EntityStates.Loader;
using HarmonyLib;
using RoR2;
using UnityEngine;
using static HeavySlam.Helpers;
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
		private static readonly AccessTools.FieldRef<GroundSlam, bool> F_detonateNextFrame;

		// Staging Effects
		private static readonly Color Stage1Color = new Color(0.584f, 0.836f, 1f);
		private static readonly Color Stage2Color = new Color(1, 0.367f, 0);
		private static readonly Color Stage3Color = new Color(1, 0, 0.313f);

		// Staging Effects
		private const float Stage1Scale = 0.6f;
		private const float Stage2Scale = 0.8f;
		private const float Stage3Scale = 1f;

		// 'Skill ref -> Speed' dictionary
		private static readonly WeakTable<GroundSlam, float> _slamSpeedDict;
		private static readonly WeakTable<GroundSlam, bool> _slamMidAirDict;
		private static readonly ConditionalWeakTable<GroundSlam, Tuple<ParticleSystem, ParticleSystem>> _slamParticlesDict;

		// Initialize Member references for fast reflection
		static ThunderSlamPatch()
		{
			// Reference to 'BaseState.RollCrit()' method
			M_RollCrit = AccessTools.Method(typeof(BaseState), "RollCrit");
			// Reference to 'BaseState.damageStat' field
			F_damageStat = AccessTools.FieldRefAccess<BaseState, float>("damageStat");
			// Reference to 'GroundSlam.detonateNextFrame' field
			F_detonateNextFrame = AccessTools.FieldRefAccess<GroundSlam, bool>("detonateNextFrame");
			// Effect fields
			F_leftHandEffect = AccessTools.FieldRefAccess<GroundSlam, GameObject>("leftFistEffectInstance");
			F_rightHandEffect = AccessTools.FieldRefAccess<GroundSlam, GameObject>("rightFistEffectInstance");

			/*
			// Create our speed dictionary
			_slamSpeedDict = new Dictionary<GroundSlam, float>();

			// Create our particle dictionary
			_slamParticles = new Dictionary<GroundSlam, Tuple<ParticleSystem, ParticleSystem>>();
			*/

			_slamSpeedDict = new WeakTable<GroundSlam, float>();
			_slamMidAirDict = new WeakTable<GroundSlam, bool>();
			_slamParticlesDict = new ConditionalWeakTable<GroundSlam, Tuple<ParticleSystem, ParticleSystem>>();
		}

        public static void OnExit(On.EntityStates.Loader.GroundSlam.orig_OnExit orig, GroundSlam self)
        {
			// Call base (removes effects)
			orig(self);

			// Remove handles
			_slamSpeedDict.Remove(self);
			_slamParticlesDict.Remove(self);
		}

        // Collect the speed before impact
        public static void SlamHitGround(On.EntityStates.Loader.GroundSlam.orig_OnMovementHit orig, GroundSlam self, ref CharacterMotor.MovementHitInfo movementHitInfo)
		{
			_slamSpeedDict.Add(self, Math.Abs(movementHitInfo.velocity.y));
			orig(self, ref movementHitInfo);
		}

		// Override the default explode method
		public static BlastAttack.Result CreateExplosion(On.EntityStates.Loader.GroundSlam.orig_DetonateAuthority _, GroundSlam self)
		{
            // Get the velocity from our dictionary
            if (!_slamSpeedDict.TryPopValue(self, out float velocity))
			{
				velocity = Mathf.Abs(GetSpeed(self));
			}

			// Get whether or not we exploded mid air
			_slamMidAirDict.TryPopValue(self, out bool midAir);

			float baseRadius = GroundSlam.blastRadius * CF.BaseRadiusCoef.Value;
			float baseDamage = F_damageStat(self) * GroundSlam.blastDamageCoefficient * CF.BaseDamageCoef.Value;
			float baseBlastForce = GroundSlam.blastForce * CF.BaseBlastForceCoef.Value;

			float newRadius = ScaleValue(baseRadius, velocity, CF.RadiusSpeedCoef.Value, CF.ShouldScaleRadius.Value);
			float newDamage = ScaleValue(baseDamage, velocity, CF.DamageSpeedCoef.Value, CF.ShouldScaleDamage.Value);
			float newBlastForce = ScaleValue(baseBlastForce, velocity, CF.BlastForceSpeedCoef.Value, CF.ShouldScaleBlastForce.Value);

			Vector3 detonatePosition = self.outer.commonComponents.characterBody.footPosition;

			if (midAir)
            {
                switch (CF.MidAirScaleType.Value)
                {
                    case MidAirScaling.AfterSpeedScaling:
						{
							newRadius *= CF.MidAirRadiusCoef.Value;
							newDamage *= CF.MidAirDamageCoef.Value;
							newBlastForce *= CF.MidAirBlastForceCoef.Value;
						}
                        break;
                    case MidAirScaling.BeforeSpeedScaling:
						{
							newRadius = ScaleValue(baseRadius * CF.MidAirRadiusCoef.Value, velocity, CF.RadiusSpeedCoef.Value, CF.ShouldScaleRadius.Value) * CF.MidAirRadiusCoef.Value;
							newDamage = ScaleValue(baseDamage * CF.MidAirDamageCoef.Value, velocity, CF.DamageSpeedCoef.Value, CF.ShouldScaleDamage.Value) * CF.MidAirDamageCoef.Value;
							newBlastForce = ScaleValue(baseBlastForce * CF.MidAirBlastForceCoef.Value, velocity, CF.BlastForceSpeedCoef.Value, CF.ShouldScaleBlastForce.Value) * CF.MidAirBlastForceCoef.Value;
						}
                        break;
                    case MidAirScaling.IgnoreSpeedScaling:
                        {
							newRadius = baseRadius * CF.MidAirRadiusCoef.Value;
							newDamage = baseDamage * CF.MidAirDamageCoef.Value;
							newBlastForce = baseBlastForce * CF.MidAirBlastForceCoef.Value;
                        }
                        break;
                }

				detonatePosition += Vector3.down * CF.MidAirPositionOffset.Value;
			}

			EffectManager.SpawnEffect(GroundSlam.blastEffectPrefab, new EffectData
			{
				origin = detonatePosition,
				scale = newRadius,
			}, true);

			// Modified blast attack
			return new BlastAttack
			{
				attacker = self.outer.gameObject,
				// Scale damage
				baseDamage = newDamage,
				// Scale knock-up
				baseForce = newBlastForce,
				bonusForce = GroundSlam.blastBonusForce,
				// Roll critical-hit
				crit = (bool)M_RollCrit.Invoke(self, null),
				damageType = DamageType.Stun1s,
				falloffModel = BlastAttack.FalloffModel.None,
				procCoefficient = GroundSlam.blastProcCoefficient,
				// Scale radius
				radius = newRadius,
				position = detonatePosition,
				attackerFiltering = AttackerFiltering.NeverHit,
				impactEffect = EffectCatalog.FindEffectIndexFromPrefab(GroundSlam.blastImpactEffectPrefab),
				teamIndex = self.outer.commonComponents.teamComponent.teamIndex
			}.Fire();
		}

		public static void FixedUpdate(On.EntityStates.Loader.GroundSlam.orig_FixedUpdate orig, GroundSlam self)
		{
			// Modify Effects to signify speed
			if (CF.ShouldModifyParticles.Value)
			{
                if (!_slamParticlesDict.TryGetValue(self, out Tuple<ParticleSystem, ParticleSystem> effects))
                {
                    effects = new Tuple<ParticleSystem, ParticleSystem>(
                        F_leftHandEffect(self).transform.Find("LoaderLightning").GetComponent<ParticleSystem>(),
                        F_rightHandEffect(self).transform.Find("LoaderLightning").GetComponent<ParticleSystem>());

                    _slamParticlesDict.Add(self, effects);
                }

                float velocity = Mathf.Abs(GetSpeed(self));
				ChangeParticles(velocity, effects.Item1);
				ChangeParticles(velocity, effects.Item2);
			}

			if (CF.CanDetonateMidAir.Value)
            {
				var input = self.outer.commonComponents.inputBank;
				if (input.skill4.down && !input.skill4.wasDown)
				{
					F_detonateNextFrame(self) = true;

					// Check if we are above ground
					if (!Physics.Raycast(self.outer.commonComponents.characterBody.footPosition, Vector3.down, CF.MidAirGroundOffset.Value, LayerIndex.world.intVal, QueryTriggerInteraction.Ignore))
                    {
						_slamMidAirDict.Add(self, true);
                    }
				}
			}

			orig(self);
		}

		// Scale using our formula
		private static float ScaleValue(float val, float speed, float speedCoef, bool shouldScale)
		{
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

		private static float GetSpeed(GroundSlam slam)
        {
			CharacterMotor motor = slam.outer.commonComponents.characterMotor;
            return motor ? motor.velocity.y : 0;
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
				float vL = speed.MapRange(vS, vE, 0f, 1f);

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
	}
}
