using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using AdvancedCompany.Game;
using AdvancedCompany.Objects;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using OpenBodyCams.Utilities;

namespace OpenBodyCams.Compatibility;

internal static class AdvancedCompanyCompatibility
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
            return false;
        }
    }

    internal static bool InitializeImpl(Harmony harmony)
    {
        var t_Player = typeof(Player);

        (string, Type[])[] postfixToMethods = [
            (nameof(Player.SetCosmetics), [typeof(string[]), typeof(bool)]),
            (nameof(Player.AddCosmetic), [typeof(string)]),
            (nameof(Player.ReequipHead), []),
            (nameof(Player.ReequipBody), []),
            (nameof(Player.ReequipFeet), []),
            (nameof(Player.UnequipAll), []),
        ];
        var postfixMethod = typeof(AdvancedCompanyCompatibility).GetMethod(nameof(AfterEquipmentChange), BindingFlags.NonPublic | BindingFlags.Static);

        foreach ((string methodName, Type[] types) in postfixToMethods)
        {
            var method = t_Player.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, types, null);
            if (method is null)
            {
                Plugin.Instance.Logger.LogWarning($"Failed to find {t_Player.FullName}.{methodName} to apply postfix.");
                return false;
            }

            harmony
                .CreateProcessor(method)
                .AddPostfix(postfixMethod).Patch();
        }

        harmony
            .CreateProcessor(typeof(LightShoeRGB).GetMethod(nameof(LightShoeRGB.LiftCurse), BindingFlags.NonPublic | BindingFlags.Instance))
            .AddPostfix(typeof(AdvancedCompanyCompatibility).GetMethod(nameof(UpdateCosmeticsAfterCoroutine), BindingFlags.NonPublic | BindingFlags.Static))
            .Patch();

        return true;
    }

    private static void AddChildren(IEnumerable<GameObject> objects, List<GameObject> toList)
    {
        foreach (var obj in objects)
        {
            if (obj == null)
                continue;
            Cosmetics.CollectChildCosmetics(obj, toList);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void CollectCosmetics(PlayerControllerB player, List<GameObject> thirdPersonCosmetics)
    {
        Player acPlayer = Player.GetPlayer(player);
        AddChildren(acPlayer.AppliedCosmetics.Values, thirdPersonCosmetics);
        thirdPersonCosmetics.AddRange(acPlayer.AppliedCosmetics.Values);
        if (acPlayer.EquipmentItemsHead is GameObject[] headObjects)
            AddChildren(headObjects, thirdPersonCosmetics);
        if (acPlayer.EquipmentItemsBody is GameObject[] bodyObjects)
            AddChildren(bodyObjects, thirdPersonCosmetics);
        if (acPlayer.EquipmentItemsFeet is GameObject[] feetObjects)
            AddChildren(feetObjects, thirdPersonCosmetics);

        if (acPlayer.HeadMount)
            Cosmetics.CollectChildCosmetics(acPlayer.HeadMount, thirdPersonCosmetics);
    }

    private static void AfterEquipmentChange()
    {
        BodyCamComponent.MarkTargetDirtyUntilRenderForAllBodyCams();
    }

    private static IEnumerator UpdateCosmeticsAfterCoroutine(IEnumerator __result)
    {
        while (true)
        {
            try
            {
                if (!__result.MoveNext())
                    break;
            }
            catch (Exception e)
            {
                Plugin.Instance.Logger.LogError("AdvancedCompany encountered an error in a coroutine:");
                Plugin.Instance.Logger.LogError(e);
                break;
            }
            yield return __result.Current;
        }

        BodyCamComponent.MarkTargetDirtyUntilRenderForAllBodyCams();
    }
}
