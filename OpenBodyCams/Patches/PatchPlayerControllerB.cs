using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using OpenBodyCams.Compatibility;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal class PatchPlayerControllerB
    {
        // Run after the GeneralImprovements finalizer which has a Low priority.
        [HarmonyFinalizer]
        [HarmonyPatch(nameof(PlayerControllerB.ConnectClientToPlayerObject))]
        [HarmonyPriority(Priority.VeryLow)]
        static void ConnectClientToPlayerObjectFinalizer(PlayerControllerB __instance)
        {
            Plugin.Instance.Logger.LogInfo("ConnectClientToPlayerObject");
            InitializeBodyCam();
        }

        static void InitializeBodyCam()
        {
            var bottomMonitors = GameObject.Find("Environment/HangarShip/ShipModels2b/MonitorWall/Cube.001");
            if (bottomMonitors == null)
            {
                Plugin.Instance.Logger.LogError("Could not find the bottom monitors' game object.");
                return;
            }

            if (bottomMonitors.GetComponent<BodyCamComponent>() == null)
            {
                var bodyCam = bottomMonitors.AddComponent<BodyCamComponent>();

                var renderer = bottomMonitors.GetComponent<MeshRenderer>();
                var materialIndex = 2;

                // GeneralImprovements BetterMonitors enabled:
                int monitorID = Plugin.GeneralImprovementsBetterMonitorIndex.Value - 1;
                if (monitorID < 0)
                    monitorID = 13;
                if (GeneralImprovementsCompatibility.GetMonitorForID(monitorID) is MeshRenderer giMonitorRenderer)
                {
                    renderer = giMonitorRenderer;
                    materialIndex = 0;
                }

                if (renderer == null)
                {
                    Plugin.Instance.Logger.LogError("Failed to find the monitor renderer.");
                    return;
                }

                bodyCam.monitorRenderer = renderer;
                bodyCam.monitorMaterialIndex = materialIndex; ;
                bodyCam.enabled = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerControllerB.KillPlayer))]
        static void KillPlayerPostfix(PlayerControllerB __instance)
        {
            if (__instance.IsOwner)
                Plugin.BodyCam.UpdateCurrentTarget();
        }

        [HarmonyPostfix]
        [HarmonyPatch("KillPlayerClientRpc")]
        static void KillPlayerClientRpcPostfix(PlayerControllerB __instance)
        {
            if (!__instance.IsOwner)
                Plugin.BodyCam.UpdateCurrentTarget();
        }
    }
}
