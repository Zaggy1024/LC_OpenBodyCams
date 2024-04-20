using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

using HarmonyLib;
using GameNetcodeStuff;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(FlowerSnakeEnemy))]
    internal static class PatchFlowerSnakeEnemy
    {
        internal static List<CodeInstruction> FirstPersonClingingAnimationInstructions;
        internal static List<CodeInstruction> ThirdPersonClingingAnimationInstructions;

        internal static HashSet<FlowerSnakeEnemy>[] PlayersClingedFlowerSnakes;

        public static void SetFirstPersonClingingAnimationPosition(FlowerSnakeEnemy flowerSnake)
        {
            throw new NotImplementedException($"{nameof(SetFirstPersonClingingAnimationPosition)} stub was called, code was not copied successfully");
        }

        public static void SetThirdPersonClingingAnimationPosition(FlowerSnakeEnemy flowerSnake)
        {
            throw new NotImplementedException($"{nameof(SetFirstPersonClingingAnimationPosition)} stub was called, code was not copied successfully");
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(FlowerSnakeEnemy.SetClingingAnimationPosition))]
        private static IEnumerable<CodeInstruction> SetClingingAnimationPositionTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = instructions.ToList();

            var m_FlowerSnakeEnemy_clingingToPlayer = typeof(FlowerSnakeEnemy).GetField(nameof(FlowerSnakeEnemy.clingingToPlayer));

            var checkIsLocalPlayer = instructionsList.FindIndexOfSequence(
                [
                    insn => insn.opcode == OpCodes.Ldarg_0,
                    insn => insn.LoadsField(m_FlowerSnakeEnemy_clingingToPlayer),
                    insn => insn.Calls(Reflection.m_GameNetworkManager_get_Instance),
                    insn => insn.LoadsField(Reflection.f_GameNetworkManager_localPlayerController),
                    insn => insn.Calls(Reflection.m_Object_op_Equality),
                    insn => insn.opcode == OpCodes.Brfalse_S || insn.opcode == OpCodes.Brfalse,
                ]);
            var isNotLocalPlayerLabel = (Label)instructionsList[checkIsLocalPlayer.End - 1].operand;

            var isNotLocalPlayer = instructionsList.FindIndex(checkIsLocalPlayer.End, insn => insn.labels.Contains(isNotLocalPlayerLabel));

            FirstPersonClingingAnimationInstructions = instructionsList.GetRange(checkIsLocalPlayer.End, isNotLocalPlayer - checkIsLocalPlayer.End);
            ThirdPersonClingingAnimationInstructions = instructionsList.GetRange(isNotLocalPlayer, instructionsList.Count - isNotLocalPlayer);

            return instructions;
        }

        private static void EnsurePlayersClingedFlowerSnakesArray()
        {
            var playerCount = StartOfRound.Instance.allPlayerScripts.Length;
            if (PlayersClingedFlowerSnakes == null)
                PlayersClingedFlowerSnakes = new HashSet<FlowerSnakeEnemy>[playerCount];
            else if (PlayersClingedFlowerSnakes.Length != playerCount)
                Array.Resize(ref PlayersClingedFlowerSnakes, playerCount);
            else
                return;

            for (var i = 0; i < playerCount; i++)
            {
                if (PlayersClingedFlowerSnakes[i] == null)
                    PlayersClingedFlowerSnakes[i] = [];
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(FlowerSnakeEnemy.SetClingToPlayer))]
        private static void SetClingToPlayerPrefix(FlowerSnakeEnemy __instance, PlayerControllerB playerToCling)
        {
            EnsurePlayersClingedFlowerSnakesArray();
            if (__instance.clingingToPlayer != null)
                PlayersClingedFlowerSnakes[__instance.clingingToPlayer.playerClientId].Remove(__instance);
            PlayersClingedFlowerSnakes[playerToCling.playerClientId].Add(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(FlowerSnakeEnemy.StopClingingOnLocalClient))]
        private static void StopClingingToPlayerPrefix(FlowerSnakeEnemy __instance)
        {
            EnsurePlayersClingedFlowerSnakesArray();
            if (__instance.clingingToPlayer != null)
                PlayersClingedFlowerSnakes[__instance.clingingToPlayer.playerClientId].Remove(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.OnDestroy))]
        private static void UnloadSceneObjectsEarly(EnemyAI __instance)
        {
            if (__instance is not FlowerSnakeEnemy flowerSnake)
                return;
            StopClingingToPlayerPrefix(flowerSnake);
        }
    }

    [HarmonyPatch(typeof(PatchFlowerSnakeEnemy))]
    internal class PatchCopyVanillaFlowerSnakeEnemyCode
    {
        private static IEnumerable<CodeInstruction> ConvertInstructions(List<CodeInstruction> instructions)
        {
            return instructions.Append(new CodeInstruction(OpCodes.Ret));
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(PatchFlowerSnakeEnemy.SetFirstPersonClingingAnimationPosition))]
        static IEnumerable<CodeInstruction> SetFirstPersonClingingAnimationPositionInjector(MethodBase method, ILGenerator generator)
        {
            return Common.TransferLabelsAndVariables(method, ref PatchFlowerSnakeEnemy.FirstPersonClingingAnimationInstructions, generator);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(PatchFlowerSnakeEnemy.SetThirdPersonClingingAnimationPosition))]
        static IEnumerable<CodeInstruction> SetThirdPersonClingingAnimationPositionInjector(MethodBase method, ILGenerator generator)
        {
            return Common.TransferLabelsAndVariables(method, ref PatchFlowerSnakeEnemy.ThirdPersonClingingAnimationInstructions, generator);
        }
    }
}
