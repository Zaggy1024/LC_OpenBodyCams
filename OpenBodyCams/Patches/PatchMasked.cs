using System.Collections.Generic;
using System.Reflection.Emit;

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
}
