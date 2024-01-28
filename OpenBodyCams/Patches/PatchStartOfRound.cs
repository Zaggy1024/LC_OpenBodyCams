using System;

using UnityEngine;
using HarmonyLib;

using OpenBodyCams.Compatibility;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    internal class PatchStartOfRound
    {
        public static Material blackScreenMaterial;

        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        [HarmonyPriority(Priority.VeryHigh)]
        static void StartPostfix()
        {
            blackScreenMaterial = StartOfRound.Instance.mapScreen.offScreenMat;

            if (Plugin.DisableInternalShipCamera.Value)
                DisableShipCamera();
        }

        static void DisableShipCamera()
        {
            var shipCameraObject = GameObject.Find("Environment/HangarShip/Cameras/ShipCamera");
            if (shipCameraObject == null)
            {
                Plugin.Instance.Logger.LogError("Could not find the internal ship camera object.");
                return;
            }

            var shipCameraRenderer = shipCameraObject.GetComponent<ManualCameraRenderer>();
            if (shipCameraRenderer?.mesh == null)
            {
                Plugin.Instance.Logger.LogError("Internal ship camera does not have a camera renderer.");
                return;
            }

            var shipScreenMaterialIndex = Array.FindIndex(shipCameraRenderer.mesh.sharedMaterials, material => material.name.StartsWith("ShipScreen1Mat"));

            if (blackScreenMaterial == null || shipScreenMaterialIndex == -1)
            {
                Plugin.Instance.Logger.LogError("Internal ship camera monitor does not have the expected materials.");
                return;
            }

            shipCameraObject.SetActive(false);

            var newMaterials = shipCameraRenderer.mesh.sharedMaterials;
            newMaterials[shipScreenMaterialIndex] = blackScreenMaterial;
            shipCameraRenderer.mesh.sharedMaterials = newMaterials;
        }

        [HarmonyPostfix]
        [HarmonyPatch("ReviveDeadPlayers")]
        static void ReviveDeadPlayersPostfix()
        {
            Plugin.BodyCam.UpdateCurrentTarget();
        }
    }
}
