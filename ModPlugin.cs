using System;
using BepInEx;
using R2API.Utils;
using R2API;
using UnityEngine;
using RoR2;
using RoR2.Skills;
using EntityStates;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using MonoMod.Cil;

namespace HeavySlam
{
    [BepInDependency(R2API.R2API.PluginGUID, R2API.R2API.PluginVersion)]
    [BepInPlugin(GUID, NAME, VERSION)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [R2APISubmoduleDependency(nameof(CommandHelper), nameof(LanguageAPI))]
    public class ModPlugin : BaseUnityPlugin
    {
        public static ModPlugin Instance { get; private set; }

        public const string GUID = "com.andrewtoasterr.heavyslam";
        public const string NAME = "Loader's Heavy-Slam";
        public const string VERSION = "1.0.1.0";

        public static ManualLogSource Log { get => Instance.Logger; }

        private void Awake()
        {
            Instance = this;
            ModConfig.InitializeConfig(Config);

            SkillDef slamDef = Resources.Load<GameObject>("Prefabs/CharacterBodies/LoaderBody").GetComponent<SkillLocator>().special.skillFamily.variants[1].skillDef;
            Array.Resize(ref slamDef.keywordTokens, slamDef.keywordTokens.Length + 1);
            slamDef.keywordTokens[slamDef.keywordTokens.Length - 1] = "KEYWORD_HEAVY";

            Logger.LogInfo(slamDef.skillDescriptionToken);
            slamDef.skillDescriptionToken = "LOADER_SPECIAL_ALT_DESCRIPTION_HEAVY";

            // Ngl, pretty cheeky
            LanguageAPI.Add("LOADER_SPECIAL_ALT_DESCRIPTION_HEAVY", $"<style=cIsDamage>Stunning</style> and <style=cIsUtility>Heavy</style>. Slam your fists down, dealing <style=cIsDamage>{2000 * ModConfig.BaseDamageCoef.Value}%</style> damage on impact.", "en");

            On.EntityStates.Loader.GroundSlam.OnMovementHit += ThunderSlamPatch.SlamHitGround;
            On.EntityStates.Loader.GroundSlam.DetonateAuthority += ThunderSlamPatch.CreateExplosion;
            On.EntityStates.Loader.GroundSlam.FixedUpdate += ThunderSlamPatch.ModifyHandEffects;

            // Adds our command
            CommandHelper.AddToConsoleWhenReady();
        }

        private void OnDestroy()
        {
            On.EntityStates.Loader.GroundSlam.OnMovementHit -= ThunderSlamPatch.SlamHitGround;
            On.EntityStates.Loader.GroundSlam.DetonateAuthority -= ThunderSlamPatch.CreateExplosion;
            On.EntityStates.Loader.GroundSlam.FixedUpdate -= ThunderSlamPatch.ModifyHandEffects;
        }
    }
}
