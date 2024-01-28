using System;
using System.Collections;

using HarmonyLib;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(ManualCameraRenderer))]
    internal class PatchManualCameraRenderer
    {
        [HarmonyPostfix]
        [HarmonyPatch("updateMapTarget")]
        static IEnumerator updateMapTargetPostfix(IEnumerator result, ManualCameraRenderer __instance)
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
            if (__result)
                return;

            if ((object)__instance == StartOfRound.Instance.mapScreen)
            {
                if (Plugin.TwoRadarCamsPresent)
                    return;

                if (Plugin.TerminalScript.terminalUIScreen.isActiveAndEnabled)
                    __result = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ManualCameraRenderer.RemoveTargetFromRadar))]
        static void RemoveTargetFromRadarPostfix(ManualCameraRenderer __instance)
        {
            if (Plugin.TwoRadarCamsPresent)
                return;
            // RemoveTargetFromRadar is invoked by RadarBoosterItem, but it invokes it as if it was called from an RPC handler,
            // which causes the target switch to not check if the target index is valid. This means that the radar target index
            // can point to a player object that hasn't been taken control of, so the body cam shows an out-of-bounds area instead.
            var player = __instance.targetedPlayer;
            if (player != null && !player.isPlayerControlled && !player.isPlayerDead && player.redirectToEnemy == null)
                __instance.SwitchRadarTargetAndSync(Math.Min(__instance.targetTransformIndex, __instance.radarTargets.Count));
        }
    }
}
