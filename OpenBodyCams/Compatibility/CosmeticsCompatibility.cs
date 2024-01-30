using System;
using System.Linq;

using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using OpenBodyCams.Compatibility;

namespace OpenBodyCams
{
    static class CosmeticsCompatibility
    {
        [Flags]
        enum CompatibilityMode
        {
            None = 0,
            MoreCompany = 1,
            AdvancedCompany = 2,
            ModelReplacementAPI = 4,
        }

        private static CompatibilityMode compatibilityMode = CompatibilityMode.None;

        public static void Initialize(Harmony harmony)
        {
            var hasAdvancedCompany = Chainloader.PluginInfos.ContainsKey("com.potatoepet.AdvancedCompany");

            if (Plugin.EnableAdvancedCompanyCosmeticsCompatibility.Value && hasAdvancedCompany)
            {
                if (AdvancedCompanyCompatibility.Initialize(harmony))
                {
                    compatibilityMode |= CompatibilityMode.AdvancedCompany;
                    Plugin.Instance.Logger.LogInfo("AdvancedCompany compatibility mode is enabled.");
                }
                else
                {
                    Plugin.Instance.Logger.LogWarning("AdvancedCompany is installed, but the compatibility feature failed to initialize.");
                }
            }

            if (Plugin.EnableMoreCompanyCosmeticsCompatibility.Value && !hasAdvancedCompany && Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"))
            {
                if (MoreCompanyCompatibility.Initialize(harmony))
                {
                    compatibilityMode |= CompatibilityMode.MoreCompany;
                    Plugin.Instance.Logger.LogInfo("MoreCompany compatibility mode is enabled.");
                }
                else
                {
                    Plugin.Instance.Logger.LogWarning("MoreCompany is installed, but the compatibility feature failed to initialize.");
                }
            }

            if (Plugin.EnableModelReplacementAPICompatibility.Value && Chainloader.PluginInfos.ContainsKey("meow.ModelReplacementAPI"))
            {
                compatibilityMode |= CompatibilityMode.ModelReplacementAPI;
                Plugin.Instance.Logger.LogInfo("ModelReplacementAPI compatibility mode is enabled.");
            }
        }

        public static GameObject[] CollectCosmetics(PlayerControllerB player)
        {
            if (player == null)
                return new GameObject[0];
            var result = Enumerable.Empty<GameObject>();
            if (compatibilityMode.HasFlag(CompatibilityMode.MoreCompany))
                result = result.Concat(MoreCompanyCompatibility.CollectCosmetics(player));
            if (compatibilityMode.HasFlag(CompatibilityMode.AdvancedCompany))
                result = result.Concat(AdvancedCompanyCompatibility.CollectCosmetics(player));
            if (compatibilityMode.HasFlag(CompatibilityMode.ModelReplacementAPI))
                result = result.Concat(ModelReplacementAPICompatibility.CollectCosmetics(player));
            var resultArray = result.ToArray();
            Plugin.Instance.Logger.LogInfo($"Collected {resultArray.Length} cosmetics objects for {player.playerUsername}.");
            return result.ToArray();
        }
    }
}
