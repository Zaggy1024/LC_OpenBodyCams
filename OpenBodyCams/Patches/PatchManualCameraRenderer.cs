using System;
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
            if ((object)__instance == StartOfRound.Instance.mapScreen)
            {
                if (__result)
                    return;

                if (Plugin.TwoRadarCamsPresent)
                    return;

                if (Plugin.TerminalScript.terminalUIScreen.isActiveAndEnabled)
                    __result = true;
            }
            else if ((object)__instance == PatchStartOfRound.ShipCameraRenderer)
            {
                if (!__result)
                    return;

                var meshBounds = __instance.mesh.bounds;
                var isVisible = false;

                foreach (var camera in Camera.allCameras)
                {
                    if ((object)camera == __instance.cam)
                        continue;
                    if (!camera.isActiveAndEnabled)
                        continue;
                    if ((camera.cullingMask & (1 << __instance.mesh.gameObject.layer)) == 0)
                        continue;

                    if (GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), meshBounds))
                    {
                        isVisible = true;
                        break;
                    }
                }

                __result = isVisible;
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
