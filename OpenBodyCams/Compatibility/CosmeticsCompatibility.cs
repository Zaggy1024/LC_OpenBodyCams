using System;
using System.Linq;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using OpenBodyCams.Compatibility;
using BepInEx.Bootstrap;

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
        }

        private static CompatibilityMode compatibilityMode = CompatibilityMode.None;

        public static void Initialize(Harmony harmony)
        {
            var hasAdvancedCompany = Chainloader.PluginInfos.ContainsKey("com.potatoepet.AdvancedCompany");

            if (Plugin.EnableAdvancedCompanyCosmeticsCompatibility.Value && hasAdvancedCompany)
            {
                compatibilityMode |= CompatibilityMode.AdvancedCompany;
                Plugin.Instance.Logger.LogInfo("AdvancedCompany compatibility mode is enabled.");
            }

            if (Plugin.EnableMoreCompanyCosmeticsCompatibility.Value && !hasAdvancedCompany && Chainloader.PluginInfos.ContainsKey("me.swipez.melonloader.morecompany"))
            {
                if (MoreCompanyCompatibility.Initialize(harmony))
                {
                    compatibilityMode |= CompatibilityMode.MoreCompany;
                    Plugin.Instance.Logger.LogInfo("MoreCompany compatibility mode is enabled.");
                }
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
            return result.ToArray();
        }
    }
}
