using System.Collections;

using HarmonyLib;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(ManualCameraRenderer))]
    internal class PatchManualCameraRenderer
    {
        [HarmonyPostfix]
        [HarmonyPatch("updateMapTarget")]
        static IEnumerator updateMapTargetPostfix(IEnumerator result, ManualCameraRenderer __instance, int __0)
        {
            if (__instance == StartOfRound.Instance.mapScreen)
                Plugin.BodyCam?.StartTargetTransition();

            while (result.MoveNext())
                yield return result.Current;

            if (__instance != StartOfRound.Instance.mapScreen)
                yield break;
            Plugin.BodyCam?.UpdateCurrentTarget();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ManualCameraRenderer.SwitchScreenOn))]
        static void SwitchScreenOnPostfix()
        {
            Plugin.Instance.Logger.LogInfo("Screen power switch");
            Plugin.BodyCam?.UpdateCurrentTarget();
        }
    }
}
