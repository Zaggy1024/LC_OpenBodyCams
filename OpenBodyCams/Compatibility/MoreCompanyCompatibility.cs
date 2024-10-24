using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using MoreCompany;
using MoreCompany.Cosmetics;

using OpenBodyCams.Patches;
using OpenBodyCams.Utilities;
using OpenBodyCams.Utilities.IL;

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
            return false;
        }
    }

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
            Plugin.Instance.Logger.LogError(exception);
        }

        (string, Type[])[] cosmeticApplicationMethods = [
            (nameof(CosmeticApplication.ClearCosmetics), []),
            (nameof(CosmeticApplication.ApplyCosmetic), [typeof(string), typeof(bool)]),
        ];
        var m_UpdateCosmetics = typeof(MoreCompanyCompatibility).GetMethod(nameof(UpdateCosmetics), BindingFlags.NonPublic | BindingFlags.Static);

        foreach (var (method, parameters) in cosmeticApplicationMethods)
        {
            harmony.CreateProcessor(typeof(CosmeticApplication).GetMethod(method, parameters))
                .AddPrefix(m_UpdateCosmetics)
                .Patch();
        }

        return true;
    }

    private static void ApplyLocalCosmeticsPatch(Harmony harmony)
    {
        var m_ClientReceiveMessagePatch_HandleDataMessage = typeof(ClientReceiveMessagePatch).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).First(method => method.Name == "HandleDataMessage");
        harmony.CreateProcessor(m_ClientReceiveMessagePatch_HandleDataMessage)
            .AddTranspiler(typeof(MoreCompanyCompatibility).GetMethod(nameof(ClientReceiveMessagePatch_HandleDataMessageTranspiler), BindingFlags.NonPublic | BindingFlags.Static))
            .Patch();
    }

    private static IEnumerable<CodeInstruction> ClientReceiveMessagePatch_HandleDataMessageTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        // Change cosmetic spawning to keep the cosmetics applied to the local player, but placed into the invisible enemies layer to match
        // other mods that use it to display cosmetics in third person.

        // Search for:
        //   bool isLocalPlayer = playerId == StartOfRound.Instance.thisClientPlayerId;
        // Debug IL:
        var injector = new ILInjector(instructions)
            .Find([
                ILMatcher.Ldloc(),
                ILMatcher.Call(Reflection.m_StartOfRound_get_Instance),
                ILMatcher.Ldfld(Reflection.f_StartOfRound_thisClientPlayerId),
                ILMatcher.Opcode(OpCodes.Ceq),
                ILMatcher.Stloc(),
                ILMatcher.Ldloc(),
                ILMatcher.Opcode(OpCodes.Brfalse),
            ]);
        // Release IL:
        if (!injector.IsValid)
        {
            injector
                .GoToStart()
                .Find([
                    ILMatcher.Ldloc(),
                    ILMatcher.Call(Reflection.m_StartOfRound_get_Instance),
                    ILMatcher.Ldfld(Reflection.f_StartOfRound_thisClientPlayerId),
                    ILMatcher.Opcode(OpCodes.Bne_Un),
                ]);
        }

        // - cosmeticApplication.ClearCosmetics();
        // + MoreCompanyCompatibility.SetUpLocalMoreCompanyCosmetics(cosmeticApplication);
        injector
            .Find([
                ILMatcher.Ldloc(),
                ILMatcher.Callvirt(typeof(CosmeticApplication).GetMethod(nameof(CosmeticApplication.ClearCosmetics), []))
            ]);

        if (!injector.IsValid)
        {
            Plugin.Instance.Logger.LogError($"Failed to find where the local player's MoreCompany cosmetics are cleared.");
            return instructions;
        }

        injector.LastMatchedInstruction = new CodeInstruction(OpCodes.Call, typeof(MoreCompanyCompatibility).GetMethod(nameof(SetUpLocalMoreCompanyCosmetics), BindingFlags.NonPublic | BindingFlags.Static, [typeof(CosmeticApplication)]));
        return injector.ReleaseInstructions();
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
