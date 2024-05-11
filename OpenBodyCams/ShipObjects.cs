using System;
using System.Linq;

using UnityEngine;

using OpenBodyCams.Compatibility;
using OpenBodyCams.API;
using OpenBodyCams.Utilities;

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
        internal static Material ExternalCameraMaterial;
        internal static Color OriginalExternalCameraEmissiveColor;

        internal static MeshRenderer DoorScreenRenderer;
        internal static bool DoorScreenUsesExternalCamera = false;

        internal static ManualCameraRenderer ShipCameraOnSmallMonitor;
        internal static ManualCameraRenderer CameraReplacedByBodyCam;

        public static void EarlyInitialization()
        {
            BlackScreenMaterial = StartOfRound.Instance.mapScreen.offScreenMat;

            InternalCameraRenderer = GameObject.Find("Environment/HangarShip/Cameras/ShipCamera")?.GetComponent<ManualCameraRenderer>();

            ExternalCameraRenderer = GameObject.Find("Environment/HangarShip/Cameras/FrontDoorSecurityCam/SecurityCamera")?.GetComponent<ManualCameraRenderer>();
            DoorScreenRenderer = GameObject.Find("Environment/HangarShip/ShipModels2b/MonitorWall/SingleScreen")?.GetComponent<MeshRenderer>();

            ShipCameraOnSmallMonitor = InternalCameraRenderer;

            BodyCamComponent.InitializeAtStartOfGame();

            // HACK: We have to get to the emissive color before GeneralImprovements (as of 1.2.5) can, since the material
            //       gets re-instantiated every time it is assigned back to the screen's renderer, so any changes to it
            //       will not be shared.
            ExternalCameraMaterial = ExternalCameraRenderer.mesh.sharedMaterials[2];
            OriginalExternalCameraEmissiveColor = ExternalCameraMaterial.GetColor("_EmissiveColor");
            SetExternalCameraEmissiveColor();
        }

        internal static void SetExternalCameraEmissiveColor()
        {
            var externalCameraEmissive = Plugin.GetExternalCameraEmissiveColor().GetValueOrDefault(OriginalExternalCameraEmissiveColor);
            ExternalCameraMaterial.SetColor("_EmissiveColor", externalCameraEmissive);
        }

        public static void LateInitialization()
        {
            ManageShipCameras();

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

        private static void ManageShipCameras()
        {
            if (!GeneralImprovementsCompatibility.BetterMonitorsEnabled)
            {
                if (Plugin.SwapInternalAndExternalShipCameras.Value)
                    SwapShipCameras();
                if (Plugin.DisableCameraOnSmallMonitor.Value)
                    DisableCameraOnSmallMonitor();
            }
        }

        private static int GetCameraMaterialIndex(ManualCameraRenderer shipCamera)
        {
            if (shipCamera == null)
                return -1;
            if (shipCamera.mesh == null)
                return -1;
            var texture = shipCamera.cam.targetTexture;
            return Array.FindIndex(shipCamera.mesh.sharedMaterials, material => material.mainTexture == texture);
        }

        private static void SwapShipCameras()
        {
            var internalCameraMaterialIndex = GetCameraMaterialIndex(InternalCameraRenderer);
            var externalCameraMaterialIndex = GetCameraMaterialIndex(ExternalCameraRenderer);

            if (internalCameraMaterialIndex == -1 || externalCameraMaterialIndex == -1)
            {
                Plugin.Instance.Logger.LogError($"{Plugin.SwapInternalAndExternalShipCameras.Definition} is enabled, but one of the ship's cameras' materials was not found.");
                return;
            }

            var internalCameraMaterial = InternalCameraRenderer.mesh.sharedMaterials[internalCameraMaterialIndex];
            InternalCameraRenderer.mesh.SetMaterial(internalCameraMaterialIndex, ExternalCameraRenderer.mesh.sharedMaterials[externalCameraMaterialIndex]);
            ExternalCameraRenderer.mesh.SetMaterial(externalCameraMaterialIndex, internalCameraMaterial);
            (InternalCameraRenderer.mesh, ExternalCameraRenderer.mesh) = (ExternalCameraRenderer.mesh, InternalCameraRenderer.mesh);

            ShipCameraOnSmallMonitor = ExternalCameraRenderer;
        }

        private static void DisableCameraOnSmallMonitor()
        {
            ShipCameraOnSmallMonitor.mesh.SetMaterial(GetCameraMaterialIndex(ShipCameraOnSmallMonitor), BlackScreenMaterial);
            ShipCameraOnSmallMonitor.cam.enabled = false;
            ShipCameraOnSmallMonitor.enabled = false;
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
                    MainBodyCam.MonitorDisabledMaterial = null;
                }

                if (MainBodyCam.MonitorRenderer == null)
                {
                    Plugin.Instance.Logger.LogError("Failed to find the monitor renderer.");
                    UnityEngine.Object.DestroyImmediate(MainBodyCam);
                    return;
                }

                if (MainBodyCam.MonitorDisabledMaterial == null)
                    MainBodyCam.MonitorDisabledMaterial = BlackScreenMaterial;

                UpdateMainBodyCamNoTargetMaterial();

                if (ShipUpgrades.BodyCamUnlockable != null)
                    MainBodyCam.enabled = ShipUpgrades.BodyCamUnlockableIsPlaced;
                else
                    BodyCam.BodyCamReceiverBecameEnabled();

                CameraReplacedByBodyCam = null;
                if (MainBodyCam.MonitorDisabledMaterial.mainTexture is RenderTexture originalTexture)
                {
                    if (originalTexture == InternalCameraRenderer.cam.targetTexture)
                        CameraReplacedByBodyCam = InternalCameraRenderer;
                    else if (originalTexture == ExternalCameraRenderer.cam.targetTexture)
                        CameraReplacedByBodyCam = ExternalCameraRenderer;
                }
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
