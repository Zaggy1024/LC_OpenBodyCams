using System;
using System.Linq;

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
        }

        private static CompatibilityMode compatibilityMode = CompatibilityMode.None;

        public static void Initialize(Harmony harmony)
        {
            if (Plugin.EnableMoreCompanyCosmeticsCompatibility.Value && MoreCompanyCompatibility.ApplyPatches(harmony))
            {
                compatibilityMode |= CompatibilityMode.MoreCompany;
                Plugin.Instance.Logger.LogInfo("MoreCompany compatibility mode is enabled.");
            }
        }

        public static GameObject[] CollectCosmetics(PlayerControllerB player)
        {
            if (player == null)
                return new GameObject[0];
            var result = Enumerable.Empty<GameObject>();
            if (compatibilityMode.HasFlag(CompatibilityMode.MoreCompany))
                result = result.Concat(MoreCompanyCompatibility.CollectCosmetics(player));
            return result.ToArray();
        }
    }
}
