using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;

using HarmonyLib;
using GameNetcodeStuff;

using OpenBodyCams.Utilities;
using OpenBodyCams.Utilities.IL;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(FlowerSnakeEnemy))]
internal static class PatchFlowerSnakeEnemy
{
    internal static List<CodeInstruction> FirstPersonClingingAnimationInstructions;
    internal static List<CodeInstruction> ThirdPersonClingingAnimationInstructions;

    internal static HashSet<FlowerSnakeEnemy>[] FlowerSnakesAttachedToPlayers;
    internal static bool HasWarnedClingingMismatch = false;

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
                if (clingingFlowerSnake.clingingToPlayer == null)
                {
                    if (!HasWarnedClingingMismatch)
                    {
                        Plugin.Instance.Logger.LogWarning($"{clingingFlowerSnake} should be clinging to a player according to our hooks, but it is not.");
                        HasWarnedClingingMismatch = true;
                    }
                    continue;
                }

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
        var matcher = new ILInjector(instructions)
            .Find([
                ILMatcher.Opcode(OpCodes.Ldarg_0),
                ILMatcher.Ldfld(typeof(FlowerSnakeEnemy).GetField(nameof(FlowerSnakeEnemy.clingingToPlayer))),
                ILMatcher.Call(Reflection.m_GameNetworkManager_get_Instance),
                ILMatcher.Ldfld(Reflection.f_GameNetworkManager_localPlayerController),
                ILMatcher.Call(Reflection.m_Object_op_Equality),
                ILMatcher.Opcode(OpCodes.Brfalse),
            ])
            .GoToLastMatchedInstruction();

        if (!matcher.IsValid)
        {
            Plugin.Instance.Logger.LogError("Failed to find code block for the flower snake clinging to the local player.");
            return instructions;
        }

        FirstPersonClingingAnimationInstructions = matcher.SkipBranch().GetLastMatch();
        ThirdPersonClingingAnimationInstructions = matcher.GoToEnd().GetLastMatch();

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
        FlowerSnakeStartedClingingToPlayer(__instance, playerToCling);
    }

    internal static void FlowerSnakeStartedClingingToPlayer(FlowerSnakeEnemy flowerSnake, PlayerControllerB player)
    {
        EnsureFlowerSnakesAttachedToPlayersArrayIsCorrectSize();
        if (flowerSnake.clingingToPlayer != null)
            FlowerSnakesAttachedToPlayers[flowerSnake.clingingToPlayer.playerClientId].Remove(flowerSnake);
        FlowerSnakesAttachedToPlayers[player.playerClientId].Add(flowerSnake);
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
