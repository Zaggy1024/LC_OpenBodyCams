using System.Collections;

using HarmonyLib;
using UnityEngine;

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
            Plugin.BodyCam?.UpdateCurrentTarget();
        }

        [HarmonyPostfix]
        [HarmonyPatch("MeetsCameraEnabledConditions")]
        static void MeetsCameraEnabledConditionsPostfix(ManualCameraRenderer __instance, ref bool __result)
        {
            if ((object)__instance == StartOfRound.Instance.mapScreen)
            {
                var terminal = Object.FindObjectOfType<Terminal>();
                if (terminal.GetComponent<ManualCameraRenderer>() != null)
                    return;

                if (terminal.terminalUIScreen.enabled)
                    __result = true;
            }
        }
    }
}
