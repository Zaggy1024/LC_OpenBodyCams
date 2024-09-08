using System;
using System.Linq;

using UnityEngine;
using TMPro;

using OpenBodyCams.Compatibility;
using OpenBodyCams.API;
using OpenBodyCams.Utilities;
using UnityEngine.Rendering.HighDefinition;

namespace OpenBodyCams
{
    public static class ShipObjects
    {
        internal static Material BlackScreenMaterial;

        internal static Terminal TerminalScript;
        internal static bool TwoRadarCamsPresent = false;

        internal static BodyCamComponent MainBodyCam;

        private static Canvas mainBodyCamOverlay;
        private static RectTransform mainBodyCamOverlayTextTransform;
        private static TextMeshProUGUI mainBodyCamOverlayTextRenderer;
        private static bool lastOverlayedCameraHadTransparentPass;

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
            TerminalScript = UnityEngine.Object.FindAnyObjectByType<Terminal>();

            BlackScreenMaterial = StartOfRound.Instance.mapScreen.offScreenMat;

            InternalCameraRenderer = GameObject.Find("Environment/HangarShip/Cameras/ShipCamera")?.GetComponent<ManualCameraRenderer>();

            ExternalCameraRenderer = GameObject.Find("Environment/HangarShip/Cameras/FrontDoorSecurityCam/SecurityCamera")?.GetComponent<ManualCameraRenderer>();
            DoorScreenRenderer = GameObject.Find("Environment/HangarShip/ShipModels2b/MonitorWall/SingleScreen")?.GetComponent<MeshRenderer>();

            ShipCameraOnSmallMonitor = InternalCameraRenderer;

            BodyCamComponent.InitializeAtStartOfGame();

            if (ExternalCameraRenderer != null)
            {
                // HACK: We have to get to the emissive color before GeneralImprovements (as of 1.2.5) can, since the material
                //       gets re-instantiated every time it is assigned back to the screen's renderer, so any changes to it
                //       will not be shared.
                ExternalCameraMaterial = ExternalCameraRenderer.mesh.sharedMaterials[2];
                OriginalExternalCameraEmissiveColor = ExternalCameraMaterial.GetColor("_EmissiveColor");
                SetExternalCameraEmissiveColor();
            }
        }

        internal static void SetExternalCameraEmissiveColor()
        {
            var externalCameraEmissive = Plugin.GetExternalCameraEmissiveColor().GetValueOrDefault(OriginalExternalCameraEmissiveColor);
            ExternalCameraMaterial?.SetColor("_EmissiveColor", externalCameraEmissive);
        }

        public static void LateInitialization()
        {
            ManageShipCameras();

            TwoRadarCamsPresent = TerminalScript.GetComponent<ManualCameraRenderer>() != null;

            if (DoorScreenRenderer != null && ExternalCameraRenderer?.cam != null)
                DoorScreenUsesExternalCamera = DoorScreenRenderer.sharedMaterials.Any(mat => mat.mainTexture == ExternalCameraRenderer.cam.targetTexture);

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
            var cameraMaterialIndex = GetCameraMaterialIndex(ShipCameraOnSmallMonitor);

            if (cameraMaterialIndex == -1)
            {
                Plugin.Instance.Logger.LogError($"{Plugin.DisableCameraOnSmallMonitor.Definition} is enabled, but the small monitor's camera material was not found.");
                return;
            }

            ShipCameraOnSmallMonitor.mesh.SetMaterial(cameraMaterialIndex, BlackScreenMaterial);
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

                if (!GeneralImprovementsCompatibility.BetterMonitorsEnabled)
                {
                    MainBodyCam.MonitorRenderer = bottomMonitors.GetComponent<MeshRenderer>();
                    MainBodyCam.MonitorMaterialIndex = 2;
                    MainBodyCam.MonitorDisabledMaterial = MainBodyCam.MonitorRenderer.sharedMaterials[MainBodyCam.MonitorMaterialIndex];
                }
                else
                {
                    int monitorID = Plugin.GeneralImprovementsBetterMonitorIndex.Value - 1;
                    var giMonitor = GeneralImprovementsCompatibility.GetMonitorForID(monitorID);
                    if (giMonitor.HasValue)
                    {
                        MainBodyCam.MonitorRenderer = giMonitor.Value.Renderer;
                        MainBodyCam.MonitorMaterialIndex = giMonitor.Value.MaterialIndex;
                        MainBodyCam.MonitorDisabledMaterial = giMonitor.Value.OriginalMaterial;
                    }
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
                    if (originalTexture == InternalCameraRenderer?.cam?.targetTexture)
                        CameraReplacedByBodyCam = InternalCameraRenderer;
                    else if (originalTexture == ExternalCameraRenderer?.cam?.targetTexture)
                        CameraReplacedByBodyCam = ExternalCameraRenderer;
                }
            }

            InitializeMainBodyCamOverlay();
            UpdateMainBodyCamSettings();
        }

        private static void InitializeMainBodyCamOverlay()
        {
            var canvasObject = new GameObject("MainBodyCamOverlay");
            canvasObject.transform.SetParent(GameObject.Find("Systems/UI").transform, false);

            mainBodyCamOverlay = canvasObject.AddComponent<Canvas>();
            mainBodyCamOverlay.renderMode = RenderMode.ScreenSpaceCamera;

            var textObject = new GameObject("Text");

            mainBodyCamOverlayTextRenderer = textObject.AddComponent<TextMeshProUGUI>();
            mainBodyCamOverlayTextRenderer.transform.SetParent(canvasObject.transform, false);
            mainBodyCamOverlayTextRenderer.font = StartOfRound.Instance.screenLevelDescription.font;

            mainBodyCamOverlayTextTransform = textObject.GetComponent<RectTransform>();

            MainBodyCam.OnCameraStatusChanged += _ => UpdateMainBodyCamOverlaySettings();
        }

        internal static void UpdateMainBodyCamNoTargetMaterial()
        {
            if (Plugin.DisplayOriginalScreenWhenDisabled.Value)
                MainBodyCam.MonitorNoTargetMaterial = MainBodyCam.MonitorDisabledMaterial;
            else
                MainBodyCam.MonitorNoTargetMaterial = null;
            MainBodyCam.UpdateScreenMaterial();
        }

        internal static void UpdateMainBodyCamSettings()
        {
            if (MainBodyCam == null)
                return;

            MainBodyCam.Resolution = new Vector2Int(Plugin.HorizontalResolution.Value, Plugin.HorizontalResolution.Value * 3 / 4);
            MainBodyCam.Framerate = Plugin.Framerate.Value;

            MainBodyCam.EnableCamera = Plugin.EnableCamera.Value;

            MainBodyCam.UpdateScreenMaterial();
            MainBodyCam.MonitorOnMaterial.SetColor("_EmissiveColor", Plugin.GetBodyCamEmissiveColor());

            UpdateMainBodyCamOverlaySettings();
        }

        private static void MatchOriginalCameraResolutionToBodyCam()
        {
            // If the original camera that may display our overlay is too low-res, it will not be
            // legible.
            var newTexture = new RenderTexture(MainBodyCam.Resolution.x, MainBodyCam.Resolution.y, CameraReplacedByBodyCam.cam.targetTexture.depth)
            {
                filterMode = MainBodyCam.MonitorOnMaterial.mainTexture.filterMode,
            };
            CameraReplacedByBodyCam.cam.targetTexture = newTexture;
            MainBodyCam.MonitorDisabledMaterial = new(MainBodyCam.MonitorDisabledMaterial)
            {
                mainTexture = newTexture,
            };
            UpdateMainBodyCamNoTargetMaterial();
        }

        private static bool SetTransparentPassEnabled(Camera camera, bool enabled)
        {
            if (camera.GetComponent<HDAdditionalCameraData>() is not { } hdCamera)
                return false;
            var settings = hdCamera.renderingPathCustomFrameSettings;
            var wasEnabled = settings.IsEnabled(FrameSettingsField.TransparentObjects);
            settings.SetEnabled(FrameSettingsField.TransparentObjects, enabled);
            hdCamera.renderingPathCustomFrameSettings = settings;
            return wasEnabled;
        }

        internal static void UpdateMainBodyCamOverlaySettings()
        {
            if (mainBodyCamOverlay.worldCamera is { } camera)
                SetTransparentPassEnabled(camera, lastOverlayedCameraHadTransparentPass);

            MatchOriginalCameraResolutionToBodyCam();

            var cameraToAttachTo = MainBodyCam.IsBlanked ? CameraReplacedByBodyCam.cam : MainBodyCam.Camera;
            mainBodyCamOverlay.worldCamera = cameraToAttachTo;
            mainBodyCamOverlay.planeDistance = cameraToAttachTo.nearClipPlane + 0.01f;
            lastOverlayedCameraHadTransparentPass = SetTransparentPassEnabled(cameraToAttachTo, true);

            mainBodyCamOverlayTextTransform.sizeDelta = mainBodyCamOverlay.renderingDisplaySize;
            mainBodyCamOverlayTextRenderer.fontSize = Math.Max(mainBodyCamOverlay.renderingDisplaySize.y / 10, 17);
            mainBodyCamOverlayTextRenderer.margin = Vector4.one * Math.Max(mainBodyCamOverlay.renderingDisplaySize.y / 40, 2);

            UpdateMainBodyCamOverlayText();
        }

        internal static void UpdateMainBodyCamOverlayText()
        {
            mainBodyCamOverlayTextRenderer.enabled = true;

            if (ShipUpgrades.BodyCamUnlockable != null)
            {
                if (!ShipUpgrades.BodyCamUnlockable.hasBeenUnlockedByPlayer)
                {
                    mainBodyCamOverlayTextRenderer.text = $"Body cam ${ShipUpgrades.BodyCamPrice}";
                    mainBodyCamOverlayTextRenderer.color = Color.yellow;
                    return;
                }

                if (!ShipUpgrades.BodyCamUnlockableIsPlaced)
                {
                    mainBodyCamOverlayTextRenderer.text = "Antenna stored";
                    mainBodyCamOverlayTextRenderer.color = Color.yellow;
                    return;
                }
            }

            switch (MainBodyCam.CameraStatus)
            {
                case CameraRenderingStatus.TargetInvalid:
                    mainBodyCamOverlayTextRenderer.text = "Signal lost";
                    mainBodyCamOverlayTextRenderer.color = Color.red;
                    return;
                case CameraRenderingStatus.TargetDisabledOnShip:
                    mainBodyCamOverlayTextRenderer.text = "Target on ship";
                    mainBodyCamOverlayTextRenderer.color = Color.green;
                    return;
            }

            mainBodyCamOverlayTextRenderer.enabled = false;
        }
    }
}
