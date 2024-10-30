using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using MoreCompany.Cosmetics;

using OpenBodyCams.Utilities;
using OpenBodyCams.Patches;
using OpenBodyCams.Utilities.IL;

namespace OpenBodyCams.Compatibility;

internal static class MoreCompanyCompatibility
{
    private static readonly MethodInfo m_CosmeticApplication_UpdateAllCosmeticVisibilities = typeof(CosmeticApplication).GetMethod(nameof(CosmeticApplication.UpdateAllCosmeticVisibilities), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool Initialize(Harmony harmony)
    {
        try
        {
            if (Chainloader.PluginInfos[ModGUIDs.MoreCompany].Metadata.Version < new Version(1, 11, 0))
            {
                Plugin.Instance.Logger.LogError($"The MoreCompany cosmetics compatibility mode requires v1.11.0+. Please upgrade MoreCompany.");
                return false;
            }

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

        harmony.CreateProcessor(m_CosmeticApplication_UpdateAllCosmeticVisibilities)
            .AddPostfix(typeof(MoreCompanyCompatibility).GetMethod(nameof(UpdateCosmetics), BindingFlags.NonPublic | BindingFlags.Static))
            .Patch();

        return true;
    }

    private static void UpdateCosmetics(Component __instance)
    {
        BodyCamComponent.MarkAnyParentDirtyUntilRenderForAllBodyCams(__instance.transform);
    }

    private static void ApplyLocalCosmeticsPatch(Harmony harmony)
    {
        harmony.CreateProcessor(m_CosmeticApplication_UpdateAllCosmeticVisibilities)
            .AddTranspiler(typeof(MoreCompanyCompatibility).GetMethod(nameof(UpdateAllCosmeticVisibilitiesPostfix), BindingFlags.NonPublic | BindingFlags.Static))
            .Patch();
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CosmeticApplication), nameof(CosmeticApplication.UpdateAllCosmeticVisibilities))]
    private static IEnumerable<CodeInstruction> UpdateAllCosmeticVisibilitiesPostfix(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        // - isActive = MainClass.cosmeticsSyncOther.Value && !isLocalPlayer;
        // + isActive = MainClass.cosmeticsSyncOther.Value;
        var isLocalPlayerArg = method.GetParameterIndex("isLocalPlayer") + 1;
        if (isLocalPlayerArg == 0)
            throw new Exception($"Failed to find the 'isLocalPlayer' parameter of {nameof(CosmeticApplication.UpdateAllCosmeticVisibilities)}");

        var injector = new ILInjector(instructions)
            .Find([
                ILMatcher.Opcode(OpCodes.Brfalse),
                ILMatcher.Ldarg(isLocalPlayerArg),
                ILMatcher.Ldc(0),
                ILMatcher.Opcode(OpCodes.Ceq),
                ILMatcher.Opcode(OpCodes.Br),
                ILMatcher.Ldc(0),
            ]);

        if (!injector.IsValid)
            throw new Exception("Failed to find check for local player when spawning cosmetics");

        injector
            .RemoveLastMatch();

        //   spawnedCosmetic.gameObject.SetActive(isActive)
        // + if (isLocalPlayer)
        // +     SetLocalCosmeticsLayers(spawnedCosmetic);
        injector
            .Find([
                ILMatcher.Ldloc(),
                ILMatcher.Callvirt(AccessTools.DeclaredPropertyGetter(typeof(Component), nameof(Component.gameObject))),
                ILMatcher.Ldloc(),
                ILMatcher.Callvirt(typeof(GameObject).GetMethod(nameof(GameObject.SetActive))),
            ]);

        if (!injector.IsValid)
            throw new Exception("Failed to find setting active state of cosmetics");

        var loadCosmetic = injector.Instruction.Clone();
        var notLocalPlayerLabel = generator.DefineLabel();
        return injector
            .GoToMatchEnd()
            .AddLabel(notLocalPlayerLabel)
            .InsertInPlace([
                InstructionUtilities.MakeLdarg(isLocalPlayerArg),
                new CodeInstruction(OpCodes.Brfalse, notLocalPlayerLabel),
                loadCosmetic,
                new CodeInstruction(OpCodes.Call, typeof(MoreCompanyCompatibility).GetMethod(nameof(SetLocalCosmeticsLayers), BindingFlags.NonPublic | BindingFlags.Static)),
            ])
            .ReleaseInstructions();
    }

    private static void SetLocalCosmeticsLayers(Component root)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>())
            transform.gameObject.layer = ViewPerspective.ENEMIES_NOT_RENDERED_LAYER;
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
