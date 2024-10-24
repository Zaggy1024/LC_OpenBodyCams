using System;
using System.Collections.Generic;
using System.Diagnostics;

using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using OpenBodyCams.Compatibility;
using OpenBodyCams.API;

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

        public static List<GameObject> CollectVanillaFirstPersonCosmetics(PlayerControllerB player)
        {
            var headChildrenTransforms = player.headCostumeContainerLocal.GetComponentsInChildren<Transform>();

            var objects = new List<GameObject>(headChildrenTransforms.Length - 1);

            for (var i = 1; i < headChildrenTransforms.Length; i++)
                objects.Add(headChildrenTransforms[i].gameObject);
            return objects;
        }

        public static List<GameObject> CollectVanillaThirdPersonCosmetics(PlayerControllerB player)
        {
            var headChildrenTransforms = player.headCostumeContainer.GetComponentsInChildren<Transform>();
            var lowerTorsoChildrenTransforms = player.lowerTorsoCostumeContainer.GetComponentsInChildren<Transform>();

            var childrenObjects = new List<GameObject>(headChildrenTransforms.Length + lowerTorsoChildrenTransforms.Length - 2);

            // Start at 1 to skip the container.
            for (var i = 1; i < headChildrenTransforms.Length; i++)
                childrenObjects.Add(headChildrenTransforms[i].gameObject);
            for (var i = 1; i < lowerTorsoChildrenTransforms.Length; i++)
                childrenObjects.Add(lowerTorsoChildrenTransforms[i].gameObject);
            return childrenObjects;
        }

        private static void DoCollectCosmetics(PlayerControllerB player, out GameObject[] thirdPersonCosmetics, out GameObject[] firstPersonCosmetics, out bool hasViewmodelReplacement)
        {
            if (player == null)
            {
                thirdPersonCosmetics = [];
                firstPersonCosmetics = [];
                hasViewmodelReplacement = false;
                return;
            }

            var thirdPersonCosmeticsList = CollectVanillaThirdPersonCosmetics(player);
            var firstPersonCosmeticsList = CollectVanillaFirstPersonCosmetics(player);
            hasViewmodelReplacement = false;

            BodyCam.CollectPlayerThirdPersonCosmetics(player, thirdPersonCosmeticsList);
            BodyCam.CollectPlayerFirstPersonCosmetics(player, firstPersonCosmeticsList, ref hasViewmodelReplacement);

            if (compatibilityMode.HasFlag(CompatibilityMode.MoreCompany))
                MoreCompanyCompatibility.CollectCosmetics(player, thirdPersonCosmeticsList);

            if (compatibilityMode.HasFlag(CompatibilityMode.AdvancedCompany))
                AdvancedCompanyCompatibility.CollectCosmetics(player, thirdPersonCosmeticsList);

            if (compatibilityMode.HasFlag(CompatibilityMode.LethalVRM))
                LethalVRMCompatibility.CollectCosmetics(player, thirdPersonCosmeticsList);

            if (compatibilityMode.HasFlag(CompatibilityMode.ModelReplacementAPI))
                ModelReplacementAPICompatibility.CollectCosmetics(player, thirdPersonCosmeticsList, firstPersonCosmeticsList, ref hasViewmodelReplacement);

            thirdPersonCosmetics = [.. thirdPersonCosmeticsList];
            firstPersonCosmetics = [.. firstPersonCosmeticsList];

            Plugin.Instance.Logger.LogInfo($"Collected {thirdPersonCosmetics.Length} third-person and {firstPersonCosmetics.Length} cosmetics for {player.playerUsername} with{(hasViewmodelReplacement ? "" : "out")} a viewmodel replacement.");

            if (PrintDebugInfo)
            {
                Plugin.Instance.Logger.LogInfo($"Stack trace:");
                var stackFrames = new StackTrace().GetFrames();
                for (int i = 1; i < stackFrames.Length; i++)
                {
                    var frame = stackFrames[i];
                    Plugin.Instance.Logger.LogInfo($"  {frame.GetMethod().DeclaringType.Name}.{frame.GetMethod().Name}()");
                }
            }
        }

        public static void CollectCosmetics(PlayerControllerB player, out GameObject[] thirdPersonCosmetics, out GameObject[] firstPersonCosmetics, out bool hasViewmodelReplacement)
        {
            try
            {
                DoCollectCosmetics(player, out thirdPersonCosmetics, out firstPersonCosmetics, out hasViewmodelReplacement);
            }
            catch (Exception e)
            {
                Plugin.Instance.Logger.LogError($"Failed to get third-person cosmetics for player {player.playerUsername}:");
                Plugin.Instance.Logger.LogError(e);
                thirdPersonCosmetics = [];
                firstPersonCosmetics = [];
                hasViewmodelReplacement = false;
            }
        }

        internal static void CollectChildCosmetics(GameObject obj, List<GameObject> children)
        {
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
                children.Add(renderer.gameObject);
        }
    }
}
