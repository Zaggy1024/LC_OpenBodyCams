using System.Collections.Generic;
using System.Reflection.Emit;

using GameNetcodeStuff;
using HarmonyLib;

using OpenBodyCams.Utilities.IL;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(HauntedMaskItem))]
internal static class PatchHauntedMaskItem
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(HauntedMaskItem.CreateMimicServerRpc))]
    private static void CreateMimicServerRpcPostfix()
    {
        BodyCamComponent.MarkTargetStatusChangedForAllBodyCams();
    }

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(HauntedMaskItem.waitForMimicEnemySpawn), MethodType.Enumerator)]
    private static IEnumerable<CodeInstruction> waitForMimicEnemySpawnTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return new ILInjector(instructions)
            .ToEnd()
            .ReverseFindStart([
                ILMatcher.Opcode(OpCodes.Ldc_I4_0),
                ILMatcher.Opcode(OpCodes.Ret),
            ])
            .Insert([
                new(OpCodes.Call, typeof(BodyCamComponent).GetMethod(nameof(BodyCamComponent.MarkTargetStatusChangedForAllBodyCams), []))
            ])
            .ReleaseInstructions();
    }
}

[HarmonyPatch(typeof(MaskedPlayerEnemy))]
internal static class PatchMaskedPlayerEnemy
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(MaskedPlayerEnemy.FinishKillAnimation))]
    private static void FinishKillAnimationPrefix(bool __0)
    {
        var killedPlayer = __0;
        if (killedPlayer)
            BodyCamComponent.MarkTargetStatusChangedForAllBodyCams();
    }

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(MaskedPlayerEnemy.waitForMimicEnemySpawn), MethodType.Enumerator)]
    private static IEnumerable<CodeInstruction> waitForMimicEnemySpawnTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return new ILInjector(instructions)
            .ToEnd()
            .ReverseFindStart([
                ILMatcher.Opcode(OpCodes.Ldc_I4_0),
                ILMatcher.Opcode(OpCodes.Ret),
            ])
            .Insert([
                new(OpCodes.Call, typeof(BodyCamComponent).GetMethod(nameof(BodyCamComponent.MarkTargetStatusChangedForAllBodyCams), []))
            ])
            .ReleaseInstructions();
    }

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(MaskedPlayerEnemy.killAnimation), MethodType.Enumerator)]
    private static IEnumerable<CodeInstruction> killAnimationTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        if (!Plugin.FixMaskedConversionForClients.Value)
            return instructions;

        var injector = new ILInjector(instructions);

        injector
            .FindStart(ILMatcher.Callvirt(typeof(PlayerControllerB).GetMethod(nameof(PlayerControllerB.KillPlayer))))
            .GoToPush(3);

        if (!injector.IsValid || injector.Instruction.opcode != OpCodes.Ldc_I4_0)
        {
            Plugin.Instance.Logger.LogWarning("Failed to change the masked kill animation to spawn a body.");
            Plugin.Instance.Logger.LogWarning("Clients may not be able to see through the perspective of their converted teammates.");
            return instructions;
        }

        injector.Instruction.opcode = OpCodes.Ldc_I4_1;

        return injector.ReleaseInstructions();
    }
}
