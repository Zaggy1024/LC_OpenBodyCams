using System;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using GameNetcodeStuff;

using OpenBodyCams.Utilities;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(FlowerSnakeEnemy))]
internal static class PatchFlowerSnakeEnemy
{
    internal static HashSet<FlowerSnakeEnemy>[] FlowerSnakesAttachedToPlayers;
    internal static bool HasWarnedClingingMismatch = false;

    public static void SetClingingAnimationPositionsForPlayer(PlayerControllerB player, Perspective perspective)
    {
        if (FlowerSnakesAttachedToPlayers == null)
            return;
        if (player.playerClientId < 0 || (int)player.playerClientId >= FlowerSnakesAttachedToPlayers.Length)
            return;

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

            var localPlayer = GameNetworkManager.Instance.localPlayerController;
            switch (perspective)
            {
                case Perspective.FirstPerson:
                    GameNetworkManager.Instance.localPlayerController = player;
                    break;
                case Perspective.ThirdPerson:
                    GameNetworkManager.Instance.localPlayerController = StartOfRound.Instance.allPlayerScripts.First(p => p != localPlayer);
                    break;
            }
            clingingFlowerSnake.SetClingingAnimationPosition();
            GameNetworkManager.Instance.localPlayerController = localPlayer;
        }
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
