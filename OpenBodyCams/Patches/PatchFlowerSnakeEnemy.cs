using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

using HarmonyLib;
using GameNetcodeStuff;

using OpenBodyCams.Utilities;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(FlowerSnakeEnemy))]
    internal static class PatchFlowerSnakeEnemy
    {
        internal static List<CodeInstruction> FirstPersonClingingAnimationInstructions;
        internal static List<CodeInstruction> ThirdPersonClingingAnimationInstructions;

        internal static HashSet<FlowerSnakeEnemy>[] FlowerSnakesAttachedToPlayers;

        internal static void SetFirstPersonClingingAnimationPosition(FlowerSnakeEnemy flowerSnake)
        {
            throw new NotImplementedException($"{nameof(SetFirstPersonClingingAnimationPosition)} stub was called, code was not copied successfully");
        }

        internal static void SetThirdPersonClingingAnimationPosition(FlowerSnakeEnemy flowerSnake)
        {
            throw new NotImplementedException($"{nameof(SetThirdPersonClingingAnimationPosition)} stub was called, code was not copied successfully");
        }

        public static void SetClingingAnimationPositionsForPlayer(PlayerControllerB player, Perspective perspective)
        {
            if (FlowerSnakesAttachedToPlayers != null)
            {
                foreach (var clingingFlowerSnake in FlowerSnakesAttachedToPlayers[player.playerClientId])
                {
                    if (clingingFlowerSnake == null)
                        continue;
                    switch (perspective)
                    {
                        case Perspective.Original:
                            clingingFlowerSnake.SetClingingAnimationPosition();
                            break;
                        case Perspective.FirstPerson:
                            SetFirstPersonClingingAnimationPosition(clingingFlowerSnake);
                            break;
                        case Perspective.ThirdPerson:
                            SetThirdPersonClingingAnimationPosition(clingingFlowerSnake);
                            break;
                    }
                }
            }
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

        private static void EnsureFlowerSnakesAttachedToPlayersArrayIsCorrectSize()
        {
            var playerCount = StartOfRound.Instance.allPlayerScripts.Length;
            if (FlowerSnakesAttachedToPlayers == null)
                FlowerSnakesAttachedToPlayers = new HashSet<FlowerSnakeEnemy>[playerCount];
            else if (FlowerSnakesAttachedToPlayers.Length != playerCount)
                Array.Resize(ref FlowerSnakesAttachedToPlayers, playerCount);
            else
                return;

            for (var i = 0; i < playerCount; i++)
            {
                if (FlowerSnakesAttachedToPlayers[i] == null)
                    FlowerSnakesAttachedToPlayers[i] = [];
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(FlowerSnakeEnemy.SetClingToPlayer))]
        private static void SetClingToPlayerPrefix(FlowerSnakeEnemy __instance, PlayerControllerB playerToCling)
        {
            EnsureFlowerSnakesAttachedToPlayersArrayIsCorrectSize();
            if (__instance.clingingToPlayer != null)
                FlowerSnakesAttachedToPlayers[__instance.clingingToPlayer.playerClientId].Remove(__instance);
            FlowerSnakesAttachedToPlayers[playerToCling.playerClientId].Add(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(FlowerSnakeEnemy.StopClingingOnLocalClient))]
        private static void StopClingingToPlayerPrefix(FlowerSnakeEnemy __instance)
        {
            FlowerSnakeStoppedClingingToPlayer(__instance);
        }

        internal static void FlowerSnakeStoppedClingingToPlayer(FlowerSnakeEnemy flowerSnake)
        {
            EnsureFlowerSnakesAttachedToPlayersArrayIsCorrectSize();
            if (flowerSnake.clingingToPlayer != null)
                FlowerSnakesAttachedToPlayers[flowerSnake.clingingToPlayer.playerClientId].Remove(flowerSnake);
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
