using System.Collections.Generic;
using System.Runtime.CompilerServices;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using MoreCompany;
using MoreCompany.Cosmetics;

using OpenBodyCams.Utilities;

namespace OpenBodyCams.Compatibility;

internal static class MoreCompanyCompatibility
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool Initialize(Harmony harmony)
    {
        harmony.PatchAll(typeof(MoreCompanyCompatibility));
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CosmeticApplication), nameof(CosmeticApplication.UpdateAllCosmeticVisibilities))]
    private static void UpdateAllCosmeticVisibilities(CosmeticApplication __instance, bool isLocalPlayer)
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
