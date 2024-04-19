using System.Collections;

using HarmonyLib;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(HauntedMaskItem))]
    internal static class PatchHauntedMaskItem
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(HauntedMaskItem.CreateMimicServerRpc))]
        private static void CreateMimicServerRpcPostfix()
        {
            BodyCamComponent.MarkTargetStatusChangedForAllBodyCams();
        }

        [HarmonyPostfix]
        [HarmonyPatch("waitForMimicEnemySpawn")]
        private static IEnumerator waitForMimicEnemySpawnPostfix(IEnumerator __result)
        {
            while (__result.MoveNext())
                yield return __result.Current;

            BodyCamComponent.MarkTargetStatusChangedForAllBodyCams();
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

        [HarmonyPostfix]
        [HarmonyPatch("waitForMimicEnemySpawn")]
        private static IEnumerator waitForMimicEnemySpawnPostfix(IEnumerator __result)
        {
            while (__result.MoveNext())
                yield return __result.Current;

            BodyCamComponent.MarkTargetStatusChangedForAllBodyCams();
        }
    }
}
