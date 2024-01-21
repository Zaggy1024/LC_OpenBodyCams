using System.Collections;

using HarmonyLib;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(HauntedMaskItem))]
    internal class PatchHauntedMaskItem
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(HauntedMaskItem.CreateMimicServerRpc))]
        static void CreateMimicServerRpcPostfix()
        {
            Plugin.BodyCam.UpdateCurrentTarget();
        }

        [HarmonyPostfix]
        [HarmonyPatch("waitForMimicEnemySpawn")]
        static IEnumerator waitForMimicEnemySpawnPostfix(IEnumerator __result)
        {
            while (__result.MoveNext())
                yield return __result.Current;

            Plugin.BodyCam.UpdateCurrentTarget();
        }
    }

    [HarmonyPatch(typeof(MaskedPlayerEnemy))]
    internal class PatchMaskedPlayerEnemy
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(MaskedPlayerEnemy.FinishKillAnimation))]
        static void FinishKillAnimationPrefix(bool __0)
        {
            var killedPlayer = __0;
            if (killedPlayer)
                Plugin.BodyCam.UpdateCurrentTarget();
        }

        [HarmonyPostfix]
        [HarmonyPatch("waitForMimicEnemySpawn")]
        static IEnumerator waitForMimicEnemySpawnPostfix(IEnumerator __result)
        {
            while (__result.MoveNext())
                yield return __result.Current;

            Plugin.BodyCam.UpdateCurrentTarget();
        }
    }
}
