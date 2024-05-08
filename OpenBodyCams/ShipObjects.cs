using System;
using System.Linq;

using UnityEngine;

using OpenBodyCams.Compatibility;
using OpenBodyCams.API;

namespace OpenBodyCams
{
    public static class ShipObjects
    {
        internal static Material BlackScreenMaterial;

        internal static Terminal TerminalScript;
        internal static bool TwoRadarCamsPresent = false;

        public static BodyCamComponent MainBodyCam;

        internal static ManualCameraRenderer InternalCameraRenderer;

        internal static ManualCameraRenderer ExternalCameraRenderer;
        internal static MeshRenderer DoorScreenRenderer;
        internal static bool DoorScreenUsesExternalCamera = false;

        public static void EarlyInitialization()
        {
            BlackScreenMaterial = StartOfRound.Instance.mapScreen.offScreenMat;

            ExternalCameraRenderer = GameObject.Find("Environment/HangarShip/Cameras/FrontDoorSecurityCam/SecurityCamera")?.GetComponent<ManualCameraRenderer>();
            DoorScreenRenderer = GameObject.Find("Environment/HangarShip/ShipModels2b/MonitorWall/SingleScreen")?.GetComponent<MeshRenderer>();

            GetAndMaybeDisableShipCamera();

            BodyCamComponent.InitializeAtStartOfGame();
        }

        private static void GetAndMaybeDisableShipCamera()
        {
            var shipCameraObject = GameObject.Find("Environment/HangarShip/Cameras/ShipCamera");
            if (shipCameraObject == null)
            {
                Plugin.Instance.Logger.LogError("Could not find the internal ship camera object.");
                return;
            }

            InternalCameraRenderer = shipCameraObject.GetComponent<ManualCameraRenderer>();
            if (InternalCameraRenderer?.mesh == null)
            {
                Plugin.Instance.Logger.LogError("Internal ship camera does not have a camera renderer.");
                return;
            }

            if (!Plugin.DisableInternalShipCamera.Value)
                return;

            var shipScreenMaterialIndex = Array.FindIndex(InternalCameraRenderer.mesh.sharedMaterials, material => material.name.StartsWith("ShipScreen1Mat"));

            if (BlackScreenMaterial == null || shipScreenMaterialIndex == -1)
            {
                Plugin.Instance.Logger.LogError("Internal ship camera monitor does not have the expected materials.");
                return;
            }

            shipCameraObject.SetActive(false);

            var newMaterials = InternalCameraRenderer.mesh.sharedMaterials;
            newMaterials[shipScreenMaterialIndex] = BlackScreenMaterial;
            InternalCameraRenderer.mesh.sharedMaterials = newMaterials;
        }

        public static void LateInitialization()
        {
            TerminalScript = UnityEngine.Object.FindObjectOfType<Terminal>();
            TwoRadarCamsPresent = TerminalScript.GetComponent<ManualCameraRenderer>() != null;

            if (DoorScreenRenderer != null)
                DoorScreenUsesExternalCamera = DoorScreenRenderer.sharedMaterials.Any(mat => mat.mainTexture == ExternalCameraRenderer.cam.targetTexture) == true;

            InitializeBodyCam();
            TerminalCommands.Initialize();

            // Prevent the radar from targeting a nonexistent player at the start of a round:
            if (!TwoRadarCamsPresent && StartOfRound.Instance.IsServer)
            {
                var mainMap = StartOfRound.Instance.mapScreen;
                mainMap.SwitchRadarTargetAndSync(Math.Min(mainMap.targetTransformIndex, mainMap.radarTargets.Count - 1));
            }
        }

        private static void InitializeBodyCam()
        {
            var bottomMonitors = GameObject.Find("Environment/HangarShip/ShipModels2b/MonitorWall/Cube.001");
            if (bottomMonitors == null)
            {
                Plugin.Instance.Logger.LogError("Could not find the bottom monitors' game object.");
                return;
            }

            if (bottomMonitors.GetComponent<BodyCamComponent>() == null)
            {
                MainBodyCam = bottomMonitors.AddComponent<BodyCamComponent>();

                bottomMonitors.AddComponent<SyncBodyCamToRadarMap>();

                MainBodyCam.MonitorRenderer = bottomMonitors.GetComponent<MeshRenderer>();
                MainBodyCam.MonitorMaterialIndex = 2;
                MainBodyCam.MonitorDisabledMaterial = MainBodyCam.MonitorRenderer.sharedMaterials[MainBodyCam.MonitorMaterialIndex];

                // GeneralImprovements BetterMonitors enabled:
                int monitorID = Plugin.GeneralImprovementsBetterMonitorIndex.Value - 1;
                if (monitorID < 0)
                    monitorID = 13;
                if (GeneralImprovementsCompatibility.GetMonitorForID(monitorID) is MeshRenderer giMonitorRenderer)
                {
                    MainBodyCam.MonitorRenderer = giMonitorRenderer;
                    MainBodyCam.MonitorMaterialIndex = 0;
                    MainBodyCam.MonitorDisabledMaterial = GeneralImprovementsCompatibility.GetOriginalMonitorMaterial(monitorID);
                }

                if (MainBodyCam.MonitorRenderer == null)
                {
                    Plugin.Instance.Logger.LogError("Failed to find the monitor renderer.");
                    UnityEngine.Object.DestroyImmediate(MainBodyCam);
                    return;
                }
                UpdateMainBodyCamNoTargetMaterial();

                if (ShipUpgrades.BodyCamUnlockable != null)
                    MainBodyCam.enabled = ShipUpgrades.BodyCamUnlockableIsPlaced;
                else
                    BodyCam.BodyCamReceiverBecameEnabled();
            }
        }

        internal static void UpdateMainBodyCamNoTargetMaterial()
        {
            if (Plugin.DisplayOriginalScreenWhenDisabled.Value)
                MainBodyCam.MonitorNoTargetMaterial = MainBodyCam.MonitorDisabledMaterial;
            else
                MainBodyCam.MonitorNoTargetMaterial = null;
            MainBodyCam.UpdateScreenMaterial();
        }
    }
}
