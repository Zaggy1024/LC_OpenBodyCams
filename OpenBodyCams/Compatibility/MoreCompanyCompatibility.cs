using System;
using System.Collections.Generic;
using System.Linq;
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

        Plugin.Instance.Logger.LogInfo($"Patched MoreCompany to spawn cosmetics on the local player.");
        return true;
    }

    private static void UpdateCosmetics()
    {
        BodyCamComponent.MarkTargetDirtyUntilRenderForAllBodyCams();
    }

    private static void SetUpLocalMoreCompanyCosmetics(CosmeticApplication cosmeticApplication)
    {
        foreach (var cosmetic in cosmeticApplication.spawnedCosmetics)
        {
            foreach (var child in cosmetic.GetComponentsInChildren<Transform>())
                child.gameObject.layer = ViewPerspective.ENEMIES_NOT_RENDERED_LAYER;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static IEnumerable<GameObject> CollectCosmetics(PlayerControllerB player)
    {
        Plugin.Instance.Logger.LogInfo($"Getting MoreCompany cosmetic models for {player.playerUsername}");
        return player.GetComponentsInChildren<CosmeticApplication>()
            .SelectMany(cosmeticApplication => cosmeticApplication.spawnedCosmetics)
            .Where(cosmetic => cosmetic != null)
            .SelectMany(cosmetic => cosmetic.GetComponentsInChildren<Transform>())
            .Select(cosmeticObject => cosmeticObject.gameObject);
    }


    [HarmonyPatch(typeof(CosmeticApplication), "UpdateAllCosmeticVisibilities")]
    [HarmonyPostfix]
    public static void UpdateAllCosmeticVisibilities(CosmeticApplication __instance, bool isLocalPlayer)
    {
        if (__instance.parentType == ParentType.Player && isLocalPlayer)
        {
            if (MainClass.cosmeticsSyncOther.Value)
            {
                foreach (var spawnedCosmetic in __instance.spawnedCosmetics)
                {
                    if (spawnedCosmetic.cosmeticType == CosmeticType.HAT && __instance.detachedHead) continue;
                    spawnedCosmetic.gameObject.SetActive(true);

                    CosmeticRegistry.RecursiveLayerChange(spawnedCosmetic.transform, ViewPerspective.ENEMIES_NOT_RENDERED_LAYER);
                }
            }

            BodyCamComponent.MarkTargetDirtyUntilRenderForAllBodyCams();
        }
    }
}
