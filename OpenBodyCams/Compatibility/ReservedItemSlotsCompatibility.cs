using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using GameNetcodeStuff;
using HarmonyLib;
using ReservedItemSlotCore.Patches;
using UnityEngine;

using OpenBodyCams.Utilities.IL;
using static UnityEngine.Rendering.DebugUI;
using ReservedItemSlotCore;
using ReservedItemSlotCore.Data;

namespace OpenBodyCams.Compatibility;

internal static class ReservedItemSlotsCompatibility
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool InitializeImpl(Harmony harmony)
    {
        var t_ReservedItemsPatcher = typeof(ReservedItemsPatcher);

        (string, Type[])[] transpileMethods = [
            (nameof(ReservedItemsPatcher.OnEquipReservedItem), [typeof(GrabbableObject)]),
            (nameof(ReservedItemsPatcher.OnPocketReservedItem), [typeof(GrabbableObject)]),
            (nameof(ReservedItemsPatcher.ResetReservedItemLayer), [typeof(GrabbableObject)]),
        ];
        var transpiler = typeof(ReservedItemSlotsCompatibility).GetMethod(nameof(MarkDirtyAtEndOfMethod), BindingFlags.NonPublic | BindingFlags.Static);

        foreach ((string methodName, Type[] types) in transpileMethods)
        {
            var method = t_ReservedItemsPatcher.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, types, null);
            if (method is null)
            {
                Plugin.Instance.Logger.LogWarning($"Failed to find {t_ReservedItemsPatcher.FullName}.{methodName} to apply postfix.");
                return false;
            }

            harmony
                .CreateProcessor(method)
                .AddTranspiler(transpiler).Patch();
        }

        return true;
    }

    private static IEnumerable<CodeInstruction> MarkDirtyAtEndOfMethod(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        return new ILInjector(instructions)
            .GoToEnd()
            .ReverseFind(ILMatcher.Opcode(OpCodes.Ret))
            .ReverseFind(ILMatcher.Not(ILMatcher.Opcode(OpCodes.Nop)))
            .GoToMatchEnd()
            .InsertInPlace([
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, typeof(ReservedItemSlotsCompatibility).GetMethod(nameof(MarkCosmeticsDirty), BindingFlags.NonPublic | BindingFlags.Static)),
            ])
            .ReleaseInstructions();
    }

    private static void MarkCosmeticsDirty(GrabbableObject item)
    {
        if (item.playerHeldBy != null)
            BodyCamComponent.MarkTargetStatusChangedForAllBodyCams(item.playerHeldBy.transform);
        else
            BodyCamComponent.MarkTargetStatusChangedForAllBodyCams();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void CollectCosmetics(PlayerControllerB player, List<GameObject> thirdPersonCosmetics)
    {
        if (!ReservedPlayerData.allPlayerData.TryGetValue(player, out var playerData))
            return;

        for (var i = 0; i < player.ItemSlots.Length; i++)
        {
            if (player.currentItemSlot == i)
                continue;
            if (!playerData.IsReservedItemSlot(i))
                continue;
            var item = player.ItemSlots[i];
            if (item == null)
                continue;

            foreach (var renderer in item.GetComponentsInChildren<MeshRenderer>())
            {
                var obj = renderer.gameObject;
                if (obj.CompareTag("DoNotSet"))
                    continue;
                if (obj.CompareTag("InteractTrigger"))
                    continue;
                var layer = obj.layer;
                if (layer == 14 || layer == 22)
                    continue;
                thirdPersonCosmetics.Add(obj);
            }
        }
    }
}
