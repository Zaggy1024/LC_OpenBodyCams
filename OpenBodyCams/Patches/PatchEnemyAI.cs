using HarmonyLib;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(EnemyAI))]
internal static class PatchEnemyAI
{
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
