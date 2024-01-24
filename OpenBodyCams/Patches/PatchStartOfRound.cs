using System;

using UnityEngine;
using HarmonyLib;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    internal class PatchStartOfRound
    {
        public static Material blackScreenMaterial;

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        static void AwakePostfix()
        {
            blackScreenMaterial = StartOfRound.Instance.mapScreen.offScreenMat;

            InitializeBodyCam();

            if (Plugin.DisableInternalShipCamera.Value)
                DisableShipCamera();
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
                bottomMonitors.AddComponent<BodyCamComponent>();
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

            var shipCamera = shipCameraObject.GetComponent<Camera>();
            if (shipCamera == null)
            {
                Plugin.Instance.Logger.LogError("Ship camera does not contain a camera component.");
                return;
            }

            shipCameraRenderer.enabled = false;

            var newMaterials = shipCameraRenderer.mesh.sharedMaterials;
            newMaterials[shipScreenMaterialIndex] = blackScreenMaterial;
            shipCameraRenderer.mesh.sharedMaterials = newMaterials;

            shipCamera.enabled = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch("ReviveDeadPlayers")]
        static void ReviveDeadPlayersPostfix()
        {
            Plugin.BodyCam.UpdateCurrentTarget();
        }
    }
}
