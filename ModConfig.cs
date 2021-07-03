using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using BepInEx.Configuration;
using R2API.Utils;
using RoR2;
using RoR2.Skills;

namespace HeavySlam
{
    public static class ModConfig
    {
        public static bool Initialized { get; private set; }

        public static ConfigEntry<float> DamageSpeedCoef { get; private set; }
        public static ConfigEntry<float> RadiusSpeedCoef { get; private set; }
        public static ConfigEntry<float> BlastForceSpeedCoef { get; private set; }
        public static ConfigEntry<float> MinimalSpeed { get; private set; }

        public static ConfigFile BaseConfig { get; private set; }

        public static void InitializeConfig(ConfigFile file)
        {
            BaseConfig = file;

            file.Bind("Comments", "Formula", "This is the formula used for scaling with speed", "default + (default * (speed / 100) * valueCoef");

            MinimalSpeed = file.Bind("Settings", "MinimalSpeed", 75f, "The minimum speed after which scaling is applied (100 = 1s of falling)");

            DamageSpeedCoef = file.Bind("Coefficients", "DamageSpeedCoef", 0.478f, "Controls how to scale damage with fall speed");
            RadiusSpeedCoef = file.Bind("Coefficients", "RadiusSpeedCoef", 0.698f, "Controls how to scale radius with fall speed");
            BlastForceSpeedCoef = file.Bind("Coefficients", "BlastForceSpeedCoef", 0.547f, "Controls how to scale blast force with fall speed");

            Initialized = true;
        }

        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Console Command")]
        [SuppressMessage("Redundancy", "RCS1163:Unused parameter.", Justification = "Console Command")]
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Console Command")]
        [ConCommand(commandName = "heavyslam_config_reload", flags = ConVarFlags.ExecuteOnServer, helpText = "Reload's the Loader's Heavy-Slam's configuration file")]
        private static void ConfigReloadCommand(ConCommandArgs args)
        {
            BaseConfig.Reload();
        }
    }
}
