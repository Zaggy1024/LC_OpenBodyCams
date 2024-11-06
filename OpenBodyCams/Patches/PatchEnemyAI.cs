using GameNetcodeStuff;
using HarmonyLib;

using OpenBodyCams.Components;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(EnemyAI))]
internal static class PatchEnemyAI
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(EnemyAI.Start))]
    private static void StartPostfix(EnemyAI __instance)
    {
        PlayerControllerB playerMimicking = null;
        foreach (var player in StartOfRound.Instance.allPlayerScripts)
        {
            if (player.redirectToEnemy == __instance)
            {
                playerMimicking = player;
                break;
            }
        }

        TargetTracker.AddTrackersToTarget(__instance.NetworkObject.transform, playerMimicking?.transform);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.OnDestroy))]
    private static void OnDestroyPrefix(EnemyAI __instance)
    {
        if (__instance is FlowerSnakeEnemy flowerSnake)
        {
            PatchFlowerSnakeEnemy.FlowerSnakeStoppedClingingToPlayer(flowerSnake);
            return;
        }
        if (__instance is CentipedeAI centipede)
        {
            PatchCentipedeAI.CentipedeStoppedClingingToPlayer(centipede);
            return;
        }
    }
}
