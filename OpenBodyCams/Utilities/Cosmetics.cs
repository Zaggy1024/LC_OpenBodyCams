using System;
using System.Collections.Generic;
using System.Diagnostics;

using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using OpenBodyCams.Compatibility;

namespace OpenBodyCams.Utilities
{
    static class Cosmetics
    {
        internal static bool PrintDebugInfo = false;

        [Flags]
        enum CompatibilityMode
        {
            None = 0,
            MoreCompany = 1,
            AdvancedCompany = 2,
            ModelReplacementAPI = 4,
            LethalVRM = 8,
        }

        private static CompatibilityMode compatibilityMode = CompatibilityMode.None;

        public static void Initialize(Harmony harmony)
        {
            var hasAdvancedCompany = Chainloader.PluginInfos.ContainsKey(ModGUIDs.AdvancedCompany);

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

            if (Plugin.EnableMoreCompanyCosmeticsCompatibility.Value && !hasAdvancedCompany && Chainloader.PluginInfos.ContainsKey(ModGUIDs.MoreCompany))
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

            if (Plugin.EnableModelReplacementAPICompatibility.Value && Chainloader.PluginInfos.ContainsKey(ModGUIDs.ModelReplacementAPI))
            {
                if (ModelReplacementAPICompatibility.Initialize(harmony))
                {
                    compatibilityMode |= CompatibilityMode.ModelReplacementAPI;
                    Plugin.Instance.Logger.LogInfo("ModelReplacementAPI compatibility mode is enabled.");
                }
                else
                {
                    Plugin.Instance.Logger.LogWarning("ModelReplacementAPI is installed, but the compatibility feature failed to initialize.");
                }
            }

            if (Plugin.EnableLethalVRMCompatibility.Value && Chainloader.PluginInfos.ContainsKey(ModGUIDs.LethalVRM))
            {
                if (LethalVRMCompatibility.Initialize(harmony))
                {
                    compatibilityMode |= CompatibilityMode.LethalVRM;
                    Plugin.Instance.Logger.LogInfo("LethalVRM compatibility mode is enabled.");
                }
                else
                {
                    Plugin.Instance.Logger.LogWarning("LethalVRM is installed, but the compatibility feature failed to initialize.");
                }
            }
        }

        private static void DebugLog(object data)
        {
            if (PrintDebugInfo)
                Plugin.Instance.Logger.LogInfo(data);
        }

        private static GameObject[] DoCollectThirdPersonCosmetics(PlayerControllerB player)
        {
            if (player == null)
                return [];

            DebugLog($"Collecting third-person cosmetics for {player.playerUsername}.");
            GameObject[] result = [];

            if (compatibilityMode.HasFlag(CompatibilityMode.MoreCompany))
            {
                var mcCosmetics = MoreCompanyCompatibility.CollectCosmetics(player);
                result = [.. result, .. mcCosmetics];
                DebugLog($"Collected {mcCosmetics.Length} MoreCompany third-person cosmetics objects.");
            }

            if (compatibilityMode.HasFlag(CompatibilityMode.AdvancedCompany))
            {
                var acCosmetics = AdvancedCompanyCompatibility.CollectCosmetics(player);
                result = [.. result, .. acCosmetics];
                DebugLog($"Collected {acCosmetics.Length} AdvancedCompany third-person cosmetics objects.");
            }

            if (compatibilityMode.HasFlag(CompatibilityMode.ModelReplacementAPI))
            {
                var mrCosmetics = ModelReplacementAPICompatibility.CollectCosmetics(player);
                result = [.. result, .. mrCosmetics];
                DebugLog($"Collected {mrCosmetics.Length} ModelReplacementAPI third-person cosmetics objects.");
            }

            if (compatibilityMode.HasFlag(CompatibilityMode.LethalVRM))
            {
                var vrmCosmetics = LethalVRMCompatibility.CollectCosmetics(player);
                result = [.. result, .. vrmCosmetics];
                DebugLog($"Collected {vrmCosmetics.Length} LethalVRM cosmetics objects.");
            }

            Plugin.Instance.Logger.LogInfo($"Collected {result.Length} third-person cosmetics objects for {player.playerUsername}.");

            if (PrintDebugInfo)
            {
                Plugin.Instance.Logger.LogInfo($"Called from:");
                var stackFrames = new StackTrace().GetFrames();
                for (int i = 1; i < stackFrames.Length; i++)
                {
                    var frame = stackFrames[i];
                    Plugin.Instance.Logger.LogInfo($"  {frame.GetMethod().DeclaringType.Name}.{frame.GetMethod().Name}()");
                }
            }

            return result;
        }

        public static GameObject[] CollectThirdPersonCosmetics(PlayerControllerB player)
        {
            try
            {
                return DoCollectThirdPersonCosmetics(player);
            }
            catch (Exception e)
            {
                Plugin.Instance.Logger.LogError($"Failed to get third-person cosmetics for player {player.playerUsername}:");
                Plugin.Instance.Logger.LogError(e);
                return [];
            }
        }
    }
}
