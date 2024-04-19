using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace OpenBodyCams
{
    public class BodyCamComponent : MonoBehaviour
    {
        [Flags]
        private enum TargetDirtyStatus
        {
            None = 0,
            Immediate = 1,
            UntilRender = 2,
        }

        public const int DEFAULT_LAYER = 0;
        public const int ENEMIES_LAYER = 19;
        public const int ENEMIES_NOT_RENDERED_LAYER = 23;

        private const float RADAR_BOOSTER_INITIAL_PAN = 270;

        private static readonly Vector3 BODY_CAM_OFFSET = new(0.07f, 0, 0.15f);
        private static readonly Vector3 CAMERA_CONTAINER_OFFSET = new(0.07f, 0, 0.125f);

        private static BodyCamComponent[] AllBodyCams = [];
        public static BodyCamComponent[] GetAllBodyCams() { return [.. AllBodyCams]; }

        private static BodyCamComponent lastBodyCamRendered;

        private static int bodyCamCullingMask;
        private static FrameSettings mainCameraCustomFrameSettings;
        private static FrameSettingsOverrideMask mainCameraCustomFrameSettingsMask;
        private static Material fogShaderMaterial;
        private static GameObject nightVisionPrefab;
        private static Light vanillaMapNightVisionLight;
        private static bool hasFinishedStaticSetup = false;

        private static bool disableCameraWhileTargetIsOnShip = false;
        private static Color screenEmissiveColor;

        private static float radarBoosterPanSpeed;

        private static bool bruteForcePreventNullModels;

        internal GameObject CameraObject;
        internal Camera Camera;
        public Camera GetCamera() { return Camera; }

        public event Action<Camera> OnCameraCreated;
        public event Action<RenderTexture> OnRenderTextureCreated;
        public event Action<bool> OnBlankedSet;

        internal Renderer MonitorRenderer;
        internal int MonitorMaterialIndex = -1;
        internal Material MonitorOnMaterial;
        internal Material MonitorOffMaterial;
        internal bool MonitorIsOn = true;

        private bool keepCameraOn = false;
        public bool ForceEnableCamera { get => keepCameraOn; set => keepCameraOn = value; }

        private bool enableCamera = true;
        private bool wasBlanked = false;
        public bool IsBlanked { get => wasBlanked; }

        private bool vanillaMapNightVisionLightWasEnabled;

        private GameObject[] localPlayerCosmetics = [];
        private PlayerModelState localPlayerModelState;

        private PlayerControllerB currentPlayer;
        private GameObject[] currentPlayerCosmetics = [];
        private PlayerModelState currentPlayerModelState;

        private Transform currentActualTarget;
        internal Renderer[] currentlyViewedMeshes = [];

        private TargetDirtyStatus targetDirtyStatus = TargetDirtyStatus.None;

        private float elapsedSinceLastFrame = 0;
        private float timePerFrame = 0;

        private bool panCamera = false;
        private float panAngle = RADAR_BOOSTER_INITIAL_PAN;

        private Light nightVisionLight;
        private MeshRenderer greenFlashRenderer;
        private Animator greenFlashAnimator;

        private MeshRenderer fogShaderPlaneRenderer;

        internal static void InitializeStatic()
        {
            RenderPipelineManager.beginCameraRendering += BeginAnyCameraRendering;
            RenderPipelineManager.endCameraRendering += EndAnyCameraRendering;
        }

        internal static void InitializeAtStartOfGame()
        {
            var aPlayerScript = StartOfRound.Instance.allPlayerScripts[0];

            bodyCamCullingMask = aPlayerScript.gameplayCamera.cullingMask & ~LayerMask.GetMask(["Ignore Raycast", "UI", "HelmetVisor"]);

            var mainCameraAdditionalData = aPlayerScript.gameplayCamera.GetComponent<HDAdditionalCameraData>();
            if (mainCameraAdditionalData.customRenderingSettings)
            {
                Plugin.Instance.Logger.LogInfo($"Using custom camera settings from {mainCameraAdditionalData.name}.");
                mainCameraCustomFrameSettings = mainCameraAdditionalData.renderingPathCustomFrameSettings;
                mainCameraCustomFrameSettingsMask = mainCameraAdditionalData.renderingPathCustomFrameSettingsOverrideMask;
            }

            fogShaderMaterial = aPlayerScript.localVisor.transform.Find("ScavengerHelmet/Plane").GetComponent<MeshRenderer>().sharedMaterial;

            nightVisionPrefab = Instantiate(aPlayerScript.nightVision.gameObject);
            nightVisionPrefab.hideFlags = HideFlags.HideAndDontSave;
            nightVisionPrefab.name = "NightVision";
            nightVisionPrefab.transform.localPosition = Vector3.zero;
            nightVisionPrefab.SetActive(false);

            var nightVisionLight = nightVisionPrefab.GetComponent<Light>();
            nightVisionLight.enabled = false;

            vanillaMapNightVisionLight = StartOfRound.Instance.mapScreen.mapCameraLight;

            UpdateAllCameraSettings();

            hasFinishedStaticSetup = true;
        }

        public static void UpdateAllCameraSettings()
        {
            UpdateStaticSettings();

            foreach (var bodyCam in AllBodyCams)
                bodyCam.UpdateSettings();
        }

        private static Color ParseColor(string str)
        {
            var components = str
                .Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => float.Parse(x.Trim(), CultureInfo.InvariantCulture))
                .ToArray();
            if (components.Length < 0 || components.Length > 4)
                throw new ArgumentException("Too many color components");
            return new Color(components[0], components[1], components[2], components.Length == 4 ? components[3] : 0);
        }

        private static Color GetEmissiveColor()
        {
            try
            {
                return ParseColor(Plugin.MonitorEmissiveColor.Value);
            }
            catch (Exception e)
            {
                Plugin.Instance.Logger.LogWarning($"Failed to parse emissive color: {e}");
                return ParseColor((string)Plugin.MonitorEmissiveColor.DefaultValue);
            }
        }

        internal static void UpdateStaticSettings()
        {
            screenEmissiveColor = GetEmissiveColor();
            disableCameraWhileTargetIsOnShip = Plugin.DisableCameraWhileTargetIsOnShip.Value;

            radarBoosterPanSpeed = Plugin.RadarBoosterPanRPM.Value * 360 / 60;

            bruteForcePreventNullModels = Plugin.BruteForcePreventFreezes.Value;
        }

        public static void MarkTargetDirtyUntilRenderForAllBodyCams(Transform target)
        {
            foreach (var bodyCam in AllBodyCams)
                bodyCam.MarkTargetDirtyUntilRender(target);
        }

        public static void MarkTargetDirtyUntilRenderForAllBodyCams()
        {
            foreach (var bodyCam in AllBodyCams)
                bodyCam.MarkTargetDirtyUntilRender();
        }

        public static void MarkTargetStatusChangedForAllBodyCams(Transform target)
        {
            foreach (var bodyCam in AllBodyCams)
                bodyCam.MarkTargetStatusChanged(target);
        }

        public static void MarkTargetStatusChangedForAllBodyCams()
        {
            foreach (var bodyCam in AllBodyCams)
                bodyCam.MarkTargetStatusChanged();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ResetLastRenderedBodyCam()
        {
            if (lastBodyCamRendered != null)
            {
                lastBodyCamRendered.ResetCameraRendering();
                lastBodyCamRendered = null;
            }
        }

        static void BeginAnyCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // HDRP does not end one camera's rendering before beginning another's.
            // Reset the camera perspective if any other camera is beginning rendering.
            // This appears to still allow the perspective change to take effect properly.
            ResetLastRenderedBodyCam();

            var bodyCamCount = AllBodyCams.Length;
            for (int i = 0; i < bodyCamCount; i++)
            {
                var bodyCam = AllBodyCams[i];
                if ((object)bodyCam.Camera == camera)
                {
                    lastBodyCamRendered = bodyCam;
                    bodyCam.BeginCameraRendering();
                    return;
                }
            }
        }

        static void EndAnyCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            ResetLastRenderedBodyCam();
        }

        void Awake()
        {
            if (!hasFinishedStaticSetup)
            {
                Plugin.Instance.Logger.LogError("Attempted to create a body cam before static initialization has been completed.");
                Plugin.Instance.Logger.LogError("This may occur if the save is corrupted, or if a mod caused an error during the start of the game.");
                Destroy(this);
                return;
            }

            AllBodyCams = [.. AllBodyCams, this];
        }

        void Start()
        {
            if (MonitorRenderer != null)
            {
                MonitorOnMaterial = new(Shader.Find("HDRP/Unlit")) { name = "BodyCamMaterial" };
                MonitorOnMaterial.SetFloat("_AlbedoAffectEmissive", 1);

                MonitorOffMaterial = ShipObjects.blackScreenMaterial;
                SetMaterial(MonitorRenderer, MonitorMaterialIndex, MonitorOnMaterial);
            }

            EnsureCameraExists();

            SyncBodyCamToRadarMap.UpdateBodyCamTarget(this);
        }

        private static void SetMaterial(Renderer renderer, int index, Material material)
        {
            var materials = renderer.sharedMaterials;
            materials[index] = material;
            renderer.sharedMaterials = materials;
        }

        public void EnsureCameraExists()
        {
            if (CameraObject != null)
                return;

            Plugin.Instance.Logger.LogInfo("Camera has been destroyed, recreating it.");

            CameraObject = new GameObject("BodyCam");
            Camera = CameraObject.AddComponent<Camera>();
            Camera.nearClipPlane = 0.05f;
            Camera.cullingMask = bodyCamCullingMask;

            var cameraData = CameraObject.AddComponent<HDAdditionalCameraData>();
            cameraData.volumeLayerMask = 1;
            if (mainCameraCustomFrameSettings != null)
            {
                cameraData.customRenderingSettings = true;
                cameraData.renderingPathCustomFrameSettings = mainCameraCustomFrameSettings;
                cameraData.renderingPathCustomFrameSettingsOverrideMask = mainCameraCustomFrameSettingsMask;
            }

            var nightVision = Instantiate(nightVisionPrefab);
            nightVision.transform.SetParent(CameraObject.transform, false);
            nightVision.SetActive(true);
            nightVisionLight = nightVision.GetComponent<Light>();

            UpdateSettings();

            var greenFlashParent = new GameObject("CameraGreenTransitionScaler");
            greenFlashParent.transform.SetParent(CameraObject.transform, false);
            greenFlashParent.transform.localScale = new Vector3(1, 0.004f, 1);

            var greenFlashObject = Instantiate(StartOfRound.Instance.mapScreen.mapCameraAnimator.gameObject);
            greenFlashObject.transform.SetParent(greenFlashParent.transform, false);
            greenFlashObject.transform.localPosition = new Vector3(0, 0, 0.1f);
            greenFlashObject.layer = DEFAULT_LAYER;
            greenFlashRenderer = greenFlashObject.GetComponent<MeshRenderer>();
            greenFlashRenderer.forceRenderingOff = true;
            greenFlashAnimator = greenFlashObject.GetComponent<Animator>() ?? throw new Exception("Green flash object copied from the map screen has no Animator.");

            // The animator defaults to a state that has no values for localScale and Renderer.enabled.
            // However, Unity keeps default values for these properties that are seemingly set when the
            // animator is cloned. Therefore, we need to play the last frame of the transition to reset
            // them to their default values.
            greenFlashAnimator.Play("MapTransitionGreen", layer: 0, normalizedTime: 1);
            greenFlashAnimator.WriteDefaultValues();

            var fogShaderPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(fogShaderPlane.GetComponent<MeshCollider>());
            fogShaderPlane.transform.SetParent(CameraObject.transform, false);
            fogShaderPlane.transform.localPosition = new Vector3(0, 0, Camera.nearClipPlane * 2);
            fogShaderPlane.transform.localRotation = Quaternion.Euler(0, 0, 0);
            fogShaderPlaneRenderer = fogShaderPlane.GetComponent<MeshRenderer>();
            fogShaderPlaneRenderer.sharedMaterial = fogShaderMaterial;
            fogShaderPlaneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            fogShaderPlaneRenderer.receiveShadows = false;
            fogShaderPlaneRenderer.forceRenderingOff = true;

            OnCameraCreated?.Invoke(Camera);
        }

        public void UpdateSettings()
        {
            Camera.targetTexture = new RenderTexture(Plugin.HorizontalResolution.Value, Plugin.HorizontalResolution.Value * 3 / 4, 32)
            {
                name = $"{name}.RenderTexture",
                filterMode = Plugin.MonitorTextureFiltering.Value,
            };
            Camera.fieldOfView = Plugin.FieldOfView.Value;

            if (MonitorOnMaterial != null)
            {
                MonitorOnMaterial.mainTexture = Camera.targetTexture;
                MonitorOnMaterial.SetColor("_EmissiveColor", screenEmissiveColor);
            }

            Camera.farClipPlane = Plugin.RenderDistance.Value;

            if (Plugin.Framerate.Value != 0)
            {
                timePerFrame = 1.0f / Plugin.Framerate.Value;
                Camera.enabled = false;
            }
            else
            {
                timePerFrame = 0;
                Camera.enabled = false;
            }

            nightVisionLight.intensity = Plugin.NightVisionIntensityBase * Plugin.NightVisionBrightness.Value;
            nightVisionLight.range = Plugin.NightVisionRangeBase * Plugin.NightVisionBrightness.Value;

            enableCamera = Plugin.EnableCamera.Value;

            OnRenderTextureCreated?.Invoke(Camera.targetTexture);
        }

        public void StartTargetTransition()
        {
            if (Plugin.UseTargetTransitionAnimation.Value)
                greenFlashAnimator?.SetTrigger("Transition");
        }

        private static void SetCosmeticHidden(GameObject cosmetic, bool hidden)
        {
            cosmetic.layer = hidden ? ENEMIES_NOT_RENDERED_LAYER : DEFAULT_LAYER;
        }

        public void SetScreenPowered(bool powered)
        {
            if (MonitorRenderer == null)
                return;

            if (powered == MonitorIsOn)
                return;

            if (powered)
            {
                StartTargetTransition();
                SetMaterial(MonitorRenderer, MonitorMaterialIndex, MonitorOnMaterial);
                MonitorIsOn = true;
                return;
            }

            SetMaterial(MonitorRenderer, MonitorMaterialIndex, MonitorOffMaterial);
            MonitorIsOn = false;
        }

        public bool IsScreenPowered()
        {
            return MonitorIsOn;
        }

        private void SetScreenBlanked(bool blanked)
        {
            if (blanked != wasBlanked)
            {
                if (MonitorOnMaterial != null)
                    MonitorOnMaterial.color = blanked ? Color.black : Color.white;
                OnBlankedSet?.Invoke(blanked);
            }
            wasBlanked = blanked;
        }

        private bool ShouldHideOutput()
        {
            if (!enableCamera)
                return true;

            if (currentActualTarget == null)
                return true;

            if (currentPlayer is not null)
            {
                if (currentPlayer.isPlayerControlled)
                    return disableCameraWhileTargetIsOnShip && currentPlayer.isInHangarShipRoom;
                if (!currentPlayer.isPlayerDead)
                    return false;
                if (currentPlayer.redirectToEnemy != null)
                    return disableCameraWhileTargetIsOnShip && currentPlayer.redirectToEnemy.isInsidePlayerShip;
                if (currentPlayer.deadBody != null)
                    return disableCameraWhileTargetIsOnShip && currentPlayer.deadBody.isInShip;
            }

            var radarBooster = currentActualTarget.GetComponent<RadarBoosterItem>();
            if (radarBooster is not null)
                return disableCameraWhileTargetIsOnShip && radarBooster.isInShipRoom;

            return true;
        }

        private static void CollectDescendentModelsToHide(Transform parent, List<Renderer> list)
        {
            // Skip all descendents of body cams. Otherwise, we will set visibility on
            // the green flash animation and make it visible to the main camera, and
            // invisible to the body cam.
            foreach (var bodyCam in AllBodyCams)
            {
                if (parent == bodyCam.CameraObject.transform)
                    return;
            }

            var renderer = parent.GetComponent<Renderer>();
            if (renderer != null && ((1 << renderer.gameObject.layer) & bodyCamCullingMask) != 0)
                list.Add(renderer);

            foreach (Transform transform in parent)
                CollectDescendentModelsToHide(transform, list);
        }

        private static Renderer[] CollectModelsToHide(Transform parent)
        {
            var descendentRenderers = new List<Renderer>(20);
            CollectDescendentModelsToHide(parent, descendentRenderers);
            return [.. descendentRenderers];
        }

        private bool TargetWouldRequireUpdate(Transform target)
        {
            if (target is null)
                return true;
            if ((object)currentActualTarget == target)
                return true;
            if ((object)StartOfRound.Instance.localPlayerController.transform == target)
                return true;
            return false;
        }

        public void MarkTargetStatusChanged(Transform transform)
        {
            if (!targetDirtyStatus.HasFlag(TargetDirtyStatus.Immediate) && TargetWouldRequireUpdate(transform))
                targetDirtyStatus |= TargetDirtyStatus.Immediate;
        }

        public void MarkTargetStatusChanged()
        {
            MarkTargetStatusChanged(null);
        }

        public void MarkTargetDirtyUntilRender(Transform transform)
        {
            if (!targetDirtyStatus.HasFlag(TargetDirtyStatus.UntilRender) && TargetWouldRequireUpdate(transform))
                targetDirtyStatus |= TargetDirtyStatus.UntilRender;
        }

        public void MarkTargetDirtyUntilRender()
        {
            MarkTargetDirtyUntilRender(null);
        }

        private void ClearTargetDirtyImmediate()
        {
            targetDirtyStatus &= ~TargetDirtyStatus.Immediate;
        }

        public void SetTargetToNone()
        {
            ClearTargetDirtyImmediate();

            currentPlayer = null;
            currentActualTarget = null;
            currentlyViewedMeshes = [];

            if (CameraObject == null)
                return;
            CameraObject.transform.SetParent(null, false);
            CameraObject.transform.localPosition = Vector3.zero;
            CameraObject.transform.localRotation = Quaternion.identity;
        }

        public void SetTargetToPlayer(PlayerControllerB player)
        {
            if (player == null)
            {
                SetTargetToNone();
                return;
            }

            EnsureCameraExists();

            ClearTargetDirtyImmediate();

            currentPlayer = player;
            UpdateModelReferences();

            panCamera = false;
            Vector3 offset = Vector3.zero;

            currentActualTarget = null;
            Transform attachmentPoint = null;

            if (!currentPlayer.isPlayerDead)
            {
                if (Plugin.CameraMode.Value == CameraModeOptions.Head)
                {
                    attachmentPoint = currentPlayer.gameplayCamera.transform;
                    offset = CAMERA_CONTAINER_OFFSET;
                }
                else
                {
                    attachmentPoint = currentPlayer.playerGlobalHead.transform.parent;
                    offset = BODY_CAM_OFFSET;
                }

                currentActualTarget = currentPlayer.transform;
                currentlyViewedMeshes = [];
            }
            else if (currentPlayer.redirectToEnemy != null)
            {
                if (currentPlayer.redirectToEnemy is MaskedPlayerEnemy masked)
                {
                    if (Plugin.CameraMode.Value == CameraModeOptions.Head)
                    {
                        attachmentPoint = masked.headTiltTarget;
                        offset = CAMERA_CONTAINER_OFFSET;
                    }
                    else
                    {
                        attachmentPoint = masked.animationContainer.Find("metarig/spine/spine.001/spine.002/spine.003");
                        offset = BODY_CAM_OFFSET;
                    }
                }
                else
                {
                    attachmentPoint = currentPlayer.redirectToEnemy.eye;
                }

                currentActualTarget = currentPlayer.redirectToEnemy.transform;
                currentlyViewedMeshes = CollectModelsToHide(currentActualTarget);
            }
            else if (currentPlayer.deadBody != null)
            {
                Transform obstructingMeshParent;
                if (Plugin.CameraMode.Value == CameraModeOptions.Head)
                {
                    attachmentPoint = currentPlayer.deadBody.transform.Find("spine.001/spine.002/spine.003/spine.004/spine.004_end");
                    obstructingMeshParent = attachmentPoint.parent;
                    offset = CAMERA_CONTAINER_OFFSET - new Vector3(0, 0.15f, 0);
                }
                else
                {
                    attachmentPoint = currentPlayer.deadBody.transform.Find("spine.001/spine.002/spine.003");
                    obstructingMeshParent = attachmentPoint;
                    offset = BODY_CAM_OFFSET;
                }

                currentActualTarget = currentPlayer.deadBody.transform;
                currentlyViewedMeshes = CollectModelsToHide(obstructingMeshParent);
            }

            if (attachmentPoint == null)
            {
                SetTargetToNone();
                return;
            }

            CameraObject.transform.SetParent(attachmentPoint, false);
            CameraObject.transform.localPosition = offset;
            CameraObject.transform.localRotation = Quaternion.identity;
        }

        public void SetTargetToTransform(Transform transform)
        {
            if (transform == null || transform.gameObject == null)
            {
                SetTargetToNone();
                return;
            }

            EnsureCameraExists();

            ClearTargetDirtyImmediate();

            currentPlayer = null;
            currentActualTarget = transform;
            UpdateModelReferences();

            panCamera = false;
            Vector3 offset = Vector3.zero;

            if (currentActualTarget.GetComponent<RadarBoosterItem>() != null)
            {
                currentlyViewedMeshes = [currentActualTarget.transform.Find("AnimContainer/Rod").GetComponent<Renderer>()];
                offset = new Vector3(0, 1.5f, 0);
                panCamera = true;
            }

            CameraObject.transform.SetParent(currentActualTarget.transform, false);
            CameraObject.transform.localPosition = offset;
            CameraObject.transform.localRotation = Quaternion.identity;
        }

        private void UpdateTargetStatus()
        {
            if (currentPlayer != null)
                SetTargetToPlayer(currentPlayer);
            else
                SetTargetToTransform(currentActualTarget);
        }

        private void UpdateModelReferences()
        {
            currentPlayerCosmetics = CosmeticsCompatibility.CollectCosmetics(currentPlayer);
            currentPlayerModelState.cosmeticsLayers = new int[currentPlayerCosmetics.Length];

            if (currentPlayer != StartOfRound.Instance.localPlayerController)
            {
                localPlayerCosmetics = CosmeticsCompatibility.CollectCosmetics(StartOfRound.Instance.localPlayerController);
                localPlayerModelState.cosmeticsLayers = new int[localPlayerCosmetics.Length];
            }
            else
            {
                localPlayerCosmetics = [];
                localPlayerModelState.cosmeticsLayers = [];
            }
        }

        private enum Perspective
        {
            FirstPerson,
            ThirdPerson,
        }

        private static void SaveStateAndApplyPerspective(PlayerControllerB player, ref GameObject[] cosmetics, ref PlayerModelState state, Perspective perspective)
        {
            if (player is null)
                return;

            // Save
            state.bodyShadowMode = player.thisPlayerModel.shadowCastingMode;
            state.bodyLayer = player.thisPlayerModel.gameObject.layer;

            state.armsEnabled = player.thisPlayerModelArms.enabled;
            state.armsLayer = player.thisPlayerModelArms.gameObject.layer;

            if (player.currentlyHeldObjectServer != null)
            {
                state.heldItemPosition = player.currentlyHeldObjectServer.transform.position;
                state.heldItemRotation = player.currentlyHeldObjectServer.transform.rotation;
            }

            for (int i = 0; i < cosmetics.Length; i++)
                state.cosmeticsLayers[i] = cosmetics[i].layer;

            // Modify
            void AttachItem(GrabbableObject item, Transform holder)
            {
                item.transform.rotation = holder.rotation;
                item.transform.Rotate(item.itemProperties.rotationOffset);
                item.transform.position = holder.position + (holder.rotation * item.itemProperties.positionOffset);
            }

            switch (perspective)
            {
                case Perspective.FirstPerson:
                    player.thisPlayerModel.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    player.thisPlayerModel.gameObject.layer = ENEMIES_NOT_RENDERED_LAYER;

                    player.thisPlayerModelArms.enabled = true;
                    player.thisPlayerModelArms.gameObject.layer = DEFAULT_LAYER;

                    if (player.currentlyHeldObjectServer != null)
                        AttachItem(player.currentlyHeldObjectServer, player.localItemHolder);

                    foreach (var cosmetic in cosmetics)
                        SetCosmeticHidden(cosmetic, true);
                    break;
                case Perspective.ThirdPerson:
                    player.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On;
                    player.thisPlayerModel.gameObject.layer = DEFAULT_LAYER;

                    player.thisPlayerModelArms.enabled = false;
                    player.thisPlayerModelArms.gameObject.layer = ENEMIES_NOT_RENDERED_LAYER;

                    if (player.currentlyHeldObjectServer != null)
                        AttachItem(player.currentlyHeldObjectServer, player.serverItemHolder);

                    foreach (var cosmetic in cosmetics)
                        SetCosmeticHidden(cosmetic, false);
                    break;
            }
        }

        private static void RestoreState(PlayerControllerB player, GameObject[] cosmetics, PlayerModelState state)
        {
            if (player is null)
                return;

            player.thisPlayerModel.shadowCastingMode = state.bodyShadowMode;
            player.thisPlayerModel.gameObject.layer = state.bodyLayer;

            player.thisPlayerModelArms.enabled = state.armsEnabled;
            player.thisPlayerModelArms.gameObject.layer = state.armsLayer;

            for (int i = 0; i < cosmetics.Length; i++)
                cosmetics[i].layer = state.cosmeticsLayers[i];

            if (player.currentlyHeldObjectServer != null)
            {
                player.currentlyHeldObjectServer.transform.position = state.heldItemPosition;
                player.currentlyHeldObjectServer.transform.rotation = state.heldItemRotation;
            }
        }

        private void UpdateTargetStatusBeforeRender()
        {
            if (targetDirtyStatus.HasFlag(TargetDirtyStatus.UntilRender))
            {
                UpdateTargetStatus();
                targetDirtyStatus ^= TargetDirtyStatus.UntilRender;
            }
        }

        private void BeginCameraRendering()
        {
            UpdateTargetStatusBeforeRender();

            vanillaMapNightVisionLightWasEnabled = vanillaMapNightVisionLight.enabled;
            vanillaMapNightVisionLight.enabled = false;

            nightVisionLight.enabled = true;
            greenFlashRenderer.forceRenderingOff = false;
            fogShaderPlaneRenderer.forceRenderingOff = false;

            var localPlayer = StartOfRound.Instance.localPlayerController;

            SaveStateAndApplyPerspective(currentPlayer, ref currentPlayerCosmetics, ref currentPlayerModelState, Perspective.FirstPerson);
            if ((object)currentPlayer != localPlayer)
                SaveStateAndApplyPerspective(localPlayer, ref localPlayerCosmetics, ref localPlayerModelState, Perspective.ThirdPerson);

            bool warnedNullMesh = false;
            foreach (var mesh in currentlyViewedMeshes)
            {
                if (mesh == null)
                {
                    if (!warnedNullMesh)
                        Plugin.Instance.Logger.LogError($"Mesh obstructing the body cam on {name} which should be hidden was unexpectedly null.");
                    warnedNullMesh = true;
                    continue;
                }
                mesh.forceRenderingOff = true;
            }
        }

        private void ResetCameraRendering()
        {
            vanillaMapNightVisionLight.enabled = vanillaMapNightVisionLightWasEnabled;

            nightVisionLight.enabled = false;
            greenFlashRenderer.forceRenderingOff = true;
            fogShaderPlaneRenderer.forceRenderingOff = true;

            var localPlayer = StartOfRound.Instance.localPlayerController;

            RestoreState(currentPlayer, currentPlayerCosmetics, currentPlayerModelState);
            if ((object)currentPlayer != localPlayer)
                RestoreState(localPlayer, localPlayerCosmetics, localPlayerModelState);

            foreach (var mesh in currentlyViewedMeshes)
            {
                if (mesh == null)
                    continue;
                mesh.forceRenderingOff = false;
            }
        }

        bool AllCosmeticsExist(PlayerControllerB player, GameObject[] cosmetics)
        {
            foreach (var cosmetic in cosmetics)
            {
                if (cosmetic == null)
                {
                    Plugin.Instance.Logger.LogError($"A cosmetic attached to {player.playerUsername} has been destroyed, re-collecting cosmetics.");
                    return false;
                }
            }
            return true;
        }

        private void UpdateTargetStatusDuringUpdate()
        {
            if (targetDirtyStatus.HasFlag(TargetDirtyStatus.Immediate))
                UpdateTargetStatus();
        }

        void LateUpdate()
        {
            EnsureCameraExists();

            UpdateTargetStatusDuringUpdate();

            var spectatedPlayer = StartOfRound.Instance.localPlayerController;
            if (spectatedPlayer == null)
                return;
            if (spectatedPlayer.spectatedPlayerScript != null)
                spectatedPlayer = spectatedPlayer.spectatedPlayerScript;
            bool enableCamera = keepCameraOn ||
                (MonitorRenderer != null
                && MonitorRenderer.isVisible
                && spectatedPlayer.isInHangarShipRoom
                && IsScreenPowered());

            if (enableCamera)
            {
                var disable = ShouldHideOutput();
                enableCamera = !disable;
                SetScreenBlanked(disable);
            }

            if (enableCamera && bruteForcePreventNullModels)
            {
                // Brute force check if all models are still valid to prevent rendering from failing and
                // causing a frozen screen.
                bool foundNull = false;

                if (currentPlayer != null && !AllCosmeticsExist(currentPlayer, currentPlayerCosmetics))
                    foundNull = true;
                var localPlayer = StartOfRound.Instance.localPlayerController;
                if ((object)currentPlayer != localPlayer && !AllCosmeticsExist(localPlayer, localPlayerCosmetics))
                    foundNull = true;

                foreach (var renderer in currentlyViewedMeshes)
                {
                    if (renderer == null)
                    {
                        Plugin.Instance.Logger.LogError($"A mesh attached to non-player target {currentActualTarget.name} is null, marking dirty.");
                        foundNull = true;
                        break;
                    }
                }

                if (foundNull)
                    UpdateTargetStatus();
            }

            if (!enableCamera)
            {
                Camera.enabled = false;
                return;
            }

            if (radarBoosterPanSpeed != 0)
                panAngle = (panAngle + (Time.deltaTime * radarBoosterPanSpeed)) % 360;
            else
                panAngle = RADAR_BOOSTER_INITIAL_PAN;
            if (panCamera)
                CameraObject.transform.localRotation = Quaternion.Euler(0, panAngle, 0);

            if (timePerFrame > 0)
            {
                elapsedSinceLastFrame += Time.deltaTime;
                if (elapsedSinceLastFrame >= timePerFrame)
                {
                    Camera.Render();
                    elapsedSinceLastFrame %= timePerFrame;
                }
            }
            else
            {
                Camera.enabled = true;
            }
        }

        void OnDestroy()
        {
            AllBodyCams = AllBodyCams.Where(bodyCam => (object)bodyCam != this).ToArray();

            SyncBodyCamToRadarMap.OnBodyCamDestroyed(this);
        }

        private static bool PlayerContainsRenderer(PlayerControllerB player, Renderer renderer)
        {
            if (player == null)
                return false;
            if (player.thisPlayerModelArms == renderer)
                return true;
            if (player.thisPlayerModel == renderer)
                return true;
            return false;
        }

        public bool HasReference(Renderer renderer)
        {
            if (targetDirtyStatus.HasFlag(TargetDirtyStatus.UntilRender))
                return false;
            if (targetDirtyStatus.HasFlag(TargetDirtyStatus.Immediate))
                UpdateTargetStatus();

            if (PlayerContainsRenderer(currentPlayer, renderer))
                return true;
            if (PlayerContainsRenderer(StartOfRound.Instance?.localPlayerController, renderer))
                return true;
            if (Array.IndexOf(currentlyViewedMeshes, renderer) != -1)
                return true;
            return false;
        }

        public bool HasReference(GameObject gameObject)
        {
            if (targetDirtyStatus.HasFlag(TargetDirtyStatus.UntilRender))
                return false;
            if (targetDirtyStatus.HasFlag(TargetDirtyStatus.Immediate))
                UpdateTargetStatus();

            if (Array.IndexOf(currentPlayerCosmetics, gameObject) != -1)
                return true;
            return Array.IndexOf(localPlayerCosmetics, gameObject) != -1;
        }

        public static bool AnyBodyCamHasReference(Renderer renderer)
        {
            foreach (var bodyCam in AllBodyCams)
            {
                if (bodyCam.HasReference(renderer))
                    return true;
            }
            return false;
        }

        public static bool AnyBodyCamHasReference(GameObject gameObject)
        {
            foreach (var bodyCam in AllBodyCams)
            {
                if (bodyCam.HasReference(gameObject))
                    return true;
            }
            return false;
        }
    }

    internal struct PlayerModelState
    {
        public ShadowCastingMode bodyShadowMode;
        public int bodyLayer;
        public bool armsEnabled;
        public int armsLayer;
        public int[] cosmeticsLayers;
        public Vector3 heldItemPosition;
        public Quaternion heldItemRotation;
    }

}
