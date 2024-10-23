using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

using GameNetcodeStuff;
using HarmonyLib;

using OpenBodyCams.Utilities;
using OpenBodyCams.Utilities.IL;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(UnlockableSuit))]
internal static class PatchUnlockableSuit
{
    [HarmonyTranspiler]
    [HarmonyPatch(nameof(UnlockableSuit.SwitchSuitForPlayer))]
    private static IEnumerable<CodeInstruction> SwitchSuitForPlayerTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var injector = new ILInjector(instructions)
            .Find([
                ILMatcher.Call(Reflection.m_GameNetworkManager_get_Instance),
                ILMatcher.Ldfld(Reflection.f_GameNetworkManager_localPlayerController),
                ILMatcher.Ldarg(0),
                ILMatcher.Call(Reflection.m_Object_op_Inequality),
                ILMatcher.Opcode(OpCodes.Brfalse),
            ]);

        if (!injector.IsValid)
        {
            Plugin.Instance.Logger.LogError($"Failed to find branch to spawn vanilla third-person suit cosmetics.{new StackTrace()}");
            return instructions;
        }

        var isLocalPlayerLabel = (Label)injector.LastMatchedInstruction.operand;
        injector
            .RemoveLastMatch()
            .FindLabel(isLocalPlayerLabel)
            .ReverseFind(ILMatcher.Opcode(OpCodes.Br));

        if (!injector.IsValid)
        {
            Plugin.Instance.Logger.LogError($"Failed to find branch to spawn vanilla first-person suit cosmetics.{new StackTrace()}");
            return instructions;
        }

        var isNotLocalPlayerLabel = (Label)injector.LastMatchedInstruction.operand;
        return injector
            .Remove()
            .FindLabel(isNotLocalPlayerLabel)
            .Insert([
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, typeof(PatchUnlockableSuit).GetMethod(nameof(AfterCosmeticsSpawned), BindingFlags.NonPublic | BindingFlags.Static, [typeof(PlayerControllerB)])),
            ])
            .ReleaseInstructions();
    }

    private static void AfterCosmeticsSpawned(PlayerControllerB player)
    {
        var firstPersonLayer = ViewPerspective.ENEMIES_NOT_RENDERED_LAYER;
        var thirdPersonLayer = ViewPerspective.DEFAULT_LAYER;

        if (player == GameNetworkManager.Instance.localPlayerController)
            (firstPersonLayer, thirdPersonLayer) = (thirdPersonLayer, firstPersonLayer);

        foreach (var cosmeticObject in Cosmetics.CollectVanillaFirstPersonCosmetics(player))
            cosmeticObject.gameObject.layer = firstPersonLayer;
        foreach (var cosmeticObject in Cosmetics.CollectVanillaThirdPersonCosmetics(player))
            cosmeticObject.gameObject.layer = thirdPersonLayer;

        BodyCamComponent.MarkTargetDirtyUntilRenderForAllBodyCams(player.transform);
    }
}
