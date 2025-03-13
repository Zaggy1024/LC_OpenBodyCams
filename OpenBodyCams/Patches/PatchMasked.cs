using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

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
            .GoToEnd()
            .ReverseFind([
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
            .GoToEnd()
            .ReverseFind([
                ILMatcher.Opcode(OpCodes.Ldc_I4_0),
                ILMatcher.Opcode(OpCodes.Ret),
            ])
            .Insert([
                new(OpCodes.Call, typeof(BodyCamComponent).GetMethod(nameof(BodyCamComponent.MarkTargetStatusChangedForAllBodyCams), []))
            ])
            .ReleaseInstructions();
    }

    private static void StartCoroutineToSetPlayerMimicked(int playerID, in NetworkObjectReference maskedReference)
    {
        if (!Plugin.FixMaskedConversionForClients.Value)
            return;

        var player = StartOfRound.Instance.allPlayerScripts[playerID];
        player.StartCoroutine(SetPlayerMimicked(player, maskedReference));
    }

    private static IEnumerator SetPlayerMimicked(PlayerControllerB player, NetworkObjectReference maskedReference)
    {
        NetworkObject maskedObject = null;
        yield return new WaitUntil(() => maskedReference.TryGet(out maskedObject));
        if (!maskedObject.TryGetComponent<MaskedPlayerEnemy>(out var masked))
            yield break;
        player.redirectToEnemy = masked;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(MaskedPlayerEnemy.waitForMimicEnemySpawn), MethodType.Enumerator)]
    private static IEnumerable<CodeInstruction> waitForMimicEnemySpawnTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        //   IEnumerator waitForMimicEnemySpawn(NetworkObjectReference netObjectRef, bool inFactory, int playerKilled)
        //   {
        // +   StartCoroutineToSetPlayerMimicked(playerKilled, in netObjectRef)
        //     ...
        //   }
        var injector = new ILInjector(instructions)
            .Find([
                ILMatcher.Ldarg(0),
                ILMatcher.Predicate(insn => insn.opcode == OpCodes.Ldfld && ((FieldInfo)insn.operand).Name.EndsWith("state")),
                ILMatcher.StlocCapture(out var stateLocalIndex),
                ILMatcher.Ldloc(in stateLocalIndex),
                ILMatcher.Opcode(OpCodes.Switch),
            ]);

        if (!injector.IsValid)
        {
            Plugin.Instance.Logger.LogError($"Failed to find the switch statement in {nameof(MaskedPlayerEnemy)}.{nameof(MaskedPlayerEnemy.waitForMimicEnemySpawn)}");
            return instructions;
        }

        var switchLabels = (Label[])injector.LastMatchedInstruction.operand;
        return injector
            .FindLabel(switchLabels[0])
            .InsertAfterBranch([
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, method.DeclaringType.GetField("playerKilled")),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldflda, method.DeclaringType.GetField("netObjectRef")),
                new(OpCodes.Call, typeof(PatchMaskedPlayerEnemy).GetMethod(nameof(StartCoroutineToSetPlayerMimicked), BindingFlags.NonPublic | BindingFlags.Static, [typeof(int), typeof(NetworkObjectReference).MakeByRefType()])),
            ])
            .ReleaseInstructions();
    }
}
