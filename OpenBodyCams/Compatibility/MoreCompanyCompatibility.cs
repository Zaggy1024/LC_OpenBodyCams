using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using MoreCompany;
using MoreCompany.Cosmetics;

using OpenBodyCams.Utilities;
using OpenBodyCams.Patches;

namespace OpenBodyCams.Compatibility;

internal static class MoreCompanyCompatibility
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool Initialize(Harmony harmony)
    {
        try
        {
            return InitializeImpl(harmony);
        }
        catch (Exception exception)
        {
            Plugin.Instance.Logger.LogError(exception);
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool InitializeImpl(Harmony harmony)
    {
        try
        {
            ApplyLocalCosmeticsPatch(harmony);
            Plugin.Instance.Logger.LogInfo($"Patched MoreCompany to spawn cosmetics on the local player.");
        }
        catch (Exception exception)
        {
            Plugin.Instance.Logger.LogError("Failed to patch MoreCompany to spawn cosmetics on the local player.");
            Plugin.Instance.Logger.LogError("The MoreCompany cosmetics compatibility will continue to function, but only other clients' cosmetics will be visible.");
            Plugin.Instance.Logger.LogError(exception);
        }

        return true;
    }

    private static void ApplyLocalCosmeticsPatch(Harmony harmony)
    {
        var targetMethod = typeof(CosmeticApplication).GetMethod(nameof(CosmeticApplication.UpdateAllCosmeticVisibilities), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, [typeof(bool)]);
        if (targetMethod == null)
            throw new MemberNotFoundException("CosmeticApplication.UpdateAllCosmeticVisibilities");

        harmony.CreateProcessor(targetMethod)
            .AddPostfix(typeof(MoreCompanyCompatibility).GetMethod(nameof(UpdateAllCosmeticVisibilitiesPostfix), BindingFlags.NonPublic | BindingFlags.Static))
            .Patch();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CosmeticApplication), nameof(CosmeticApplication.UpdateAllCosmeticVisibilities))]
    private static void UpdateAllCosmeticVisibilitiesPostfix(CosmeticApplication __instance, bool isLocalPlayer)
    {
        if (__instance.parentType != ParentType.Player)
            return;
        if (!isLocalPlayer)
            return;
        if (!MainClass.cosmeticsSyncOther.Value)
            return;

        foreach (var spawnedCosmetic in __instance.spawnedCosmetics)
        {
            if (spawnedCosmetic.cosmeticType == CosmeticType.HAT && __instance.detachedHead)
                continue;

            spawnedCosmetic.gameObject.SetActive(true);
            CosmeticRegistry.RecursiveLayerChange(spawnedCosmetic.transform, ViewPerspective.ENEMIES_NOT_RENDERED_LAYER);
        }

        BodyCamComponent.MarkTargetDirtyUntilRenderForAllBodyCams();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void CollectCosmetics(PlayerControllerB player, List<GameObject> thirdPersonCosmetics)
    {
        foreach (var cosmetics in player.GetComponentsInChildren<CosmeticApplication>())
        {
            foreach (var cosmetic in cosmetics.spawnedCosmetics)
            {
                if (cosmetic == null)
                    continue;
                Cosmetics.CollectChildCosmetics(cosmetic.gameObject, thirdPersonCosmetics);
            }
        }
    }
}
