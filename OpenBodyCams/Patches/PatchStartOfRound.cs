using System.Linq;

using UnityEngine;
using HarmonyLib;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    internal class PatchStartOfRound
    {
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

            var blackScreenMaterial = shipCameraRenderer.mesh.materials.First(material => material.name.StartsWith("BlackScreen"));
            var shipScreenMaterialIndex = 0;
            while (!shipCameraRenderer.mesh.sharedMaterials[shipScreenMaterialIndex].name.StartsWith("ShipScreen1Mat"))
                shipScreenMaterialIndex++;

            if (blackScreenMaterial == null || shipScreenMaterialIndex >= shipCameraRenderer.mesh.sharedMaterials.Length)
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
        [HarmonyPatch("Awake")]
        static void AwakePostfix()
        {
            InitializeBodyCam();

            if (Plugin.DisableInternalShipCamera.Value)
                DisableShipCamera();
        }

        [HarmonyPostfix]
        [HarmonyPatch("ReviveDeadPlayers")]
        static void ReviveDeadPlayersPostfix()
        {
            Plugin.BodyCam.UpdateCurrentTarget();
        }
    }
}
