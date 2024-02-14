using System;
using System.Data;
using System.Globalization;
using System.Linq;

using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace OpenBodyCams
{
    public class BodyCamComponent : MonoBehaviour
    {
        public const int DEFAULT_LAYER = 0;
        public const int ENEMIES_LAYER = 19;
        public const int ENEMIES_NOT_RENDERED_LAYER = 23;

        private const float RADAR_BOOSTER_INITIAL_PAN = 270;

        private static readonly Vector3 BODY_CAM_OFFSET = new(0.07f, 0, 0.15f);
        private static readonly Vector3 CAMERA_CONTAINER_OFFSET = new(0.07f, 0, 0.125f);

        private static BodyCamComponent[] AllBodyCams = [];
        public static BodyCamComponent[] GetAllBodyCams() { return [.. AllBodyCams]; }

        private static int mainCameraCullingMask;
        private static FrameSettings mainCameraCustomFrameSettings;
        private static FrameSettingsOverrideMask mainCameraCustomFrameSettingsMask;
        private static Material fogShaderMaterial;
        private static GameObject nightVisionPrefab;

        private static bool disableCameraWhileTargetIsOnShip = false;
        private static Color screenEmissiveColor;

        private static float radarBoosterPanSpeed;

        internal GameObject CameraObject;
        internal Camera Camera;
        public Camera GetCamera() { return Camera; }

        internal Renderer MonitorRenderer;
        internal int MonitorMaterialIndex;
        internal Material MonitorOnMaterial;
        internal Material MonitorOffMaterial;

        private bool enableCamera = true;
        private bool wasBlanked = false;

        private bool hasSetUpCamera = false;

        private GameObject[] localPlayerCosmetics = [];
        private PlayerModelState localPlayerModelState;

        private PlayerControllerB currentPlayer;
        private GameObject[] currentPlayerCosmetics = [];
        private PlayerModelState currentPlayerModelState;

        private Transform currentActualTarget;
        private Renderer[] currentlyViewedMeshes = [];

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

            mainCameraCullingMask = aPlayerScript.gameplayCamera.cullingMask;

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

            UpdateAllCameraSettings();
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
        }

        public static void UpdateAllTargetStatuses()
        {
            foreach (var bodyCam in AllBodyCams)
                bodyCam.UpdateTargetStatus();
        }

        static void BeginAnyCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // HDRP does not end one camera's rendering before beginning another's.
            // Reset the camera perspective if any other camera is beginning rendering.
            // This appears to still allow the perspective change to take effect properly.
            foreach (var bodyCam in AllBodyCams)
                bodyCam.ResetCameraRendering();

            foreach (var bodyCam in AllBodyCams)
            {
                if ((object)bodyCam.Camera == camera)
                {
                    bodyCam.BeginCameraRendering();
                    return;
                }
            }
        }

        static void EndAnyCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            foreach (var bodyCam in AllBodyCams)
            {
                if ((object)bodyCam.Camera == camera)
                {
                    bodyCam.ResetCameraRendering();
                    return;
                }
            }
        }

        void Awake()
        {
            AllBodyCams = [.. AllBodyCams, this];

            MonitorOnMaterial = new(Shader.Find("HDRP/Unlit")) { name = "BodyCamMaterial" };
            MonitorOnMaterial.SetFloat("_AlbedoAffectEmissive", 1);

            MonitorOffMaterial = ShipObjects.blackScreenMaterial;

            var nightVisionLight = nightVisionPrefab.GetComponent<Light>();
            nightVisionLight.enabled = false;

            // By default, the map's night vision light renders on all layers, so let's change that so we don't see it on the body cam.
            var mapLight = StartOfRound.Instance.mapScreen.mapCameraLight;
            mapLight.cullingMask = 1 << mapLight.gameObject.layer;

            EnsureCameraExists();

            SyncBodyCamToRadarMap.UpdateBodyCamTarget(this);
        }

        void Start()
        {
            SetMaterial(MonitorRenderer, MonitorMaterialIndex, MonitorOnMaterial);
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
            Camera.cullingMask = mainCameraCullingMask & ~LayerMask.GetMask(["Ignore Raycast", "UI", "HelmetVisor"]);

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

            // Cloning the transition while it is playing seems to freeze it, so play the animation here to let it reset.
            StartTargetTransition();
        }

        public void UpdateSettings()
        {
            Camera.targetTexture = new RenderTexture(Plugin.HorizontalResolution.Value, Plugin.HorizontalResolution.Value * 3 / 4, 32);
            Camera.targetTexture.filterMode = Plugin.MonitorTextureFiltering.Value;
            Camera.fieldOfView = Plugin.FieldOfView.Value;

            MonitorOnMaterial.mainTexture = Camera.targetTexture;
            MonitorOnMaterial.SetColor("_EmissiveColor", screenEmissiveColor);

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
        }

        public void StartTargetTransition()
        {
            if (Plugin.UseTargetTransitionAnimation.Value)
                greenFlashAnimator.SetTrigger("Transition");
        }

        private static void SetCosmeticHidden(GameObject cosmetic, bool hidden)
        {
            cosmetic.layer = hidden ? ENEMIES_NOT_RENDERED_LAYER : DEFAULT_LAYER;
        }

        public void SetScreenPowered(bool powered)
        {
            if (powered)
            {
                if (MonitorRenderer.sharedMaterials[MonitorMaterialIndex] == MonitorOffMaterial)
                    StartTargetTransition();
                SetMaterial(MonitorRenderer, MonitorMaterialIndex, MonitorOnMaterial);
                return;
            }

            SetMaterial(MonitorRenderer, MonitorMaterialIndex, MonitorOffMaterial);
        }

        private void SetScreenBlanked(bool blanked)
        {
            if (blanked != wasBlanked)
                MonitorOnMaterial.color = blanked ? Color.black : Color.white;
            wasBlanked = blanked;
        }

        private bool ShouldHideOutput()
        {
            if (!enableCamera)
                return true;
            if (currentActualTarget == null)
                return true;
            if (currentPlayer != null && !currentPlayer.isPlayerControlled && currentPlayer.deadBody == null && currentPlayer.redirectToEnemy == null)
                return true;

            return disableCameraWhileTargetIsOnShip && currentPlayer?.isInHangarShipRoom == true;
        }

        private static Renderer[] CollectModelsToHide(Transform parent)
        {
            return parent.GetComponentsInChildren<Renderer>().Where(r => r.gameObject.layer == DEFAULT_LAYER || r.gameObject.layer == ENEMIES_LAYER).ToArray();
        }

        public void SetTargetToNone()
        {
            currentPlayer = null;
            currentActualTarget = null;
            currentlyViewedMeshes = [];
            CameraObject.transform.SetParent(null, false);
            CameraObject.transform.localPosition = Vector3.zero;
            CameraObject.transform.localRotation = Quaternion.identity;
        }

        public void UpdateTargetStatus()
        {
            if (currentPlayer != null)
                SetTargetToPlayer(currentPlayer);
            else
                SetTargetToTransform(currentActualTarget);
        }

        public void SetTargetToPlayer(PlayerControllerB player)
        {
            if (player == null)
            {
                SetTargetToNone();
                return;
            }

            currentPlayer = player;
            UpdateModelReferences();

            panCamera = false;
            Vector3 offset = Vector3.zero;

            if (!currentPlayer.isPlayerDead)
            {
                if (Plugin.CameraMode.Value == CameraModeOptions.Head)
                {
                    currentActualTarget = currentPlayer.gameplayCamera.transform;
                    offset = CAMERA_CONTAINER_OFFSET;
                }
                else
                {
                    currentActualTarget = currentPlayer.playerGlobalHead.transform.parent;
                    offset = BODY_CAM_OFFSET;
                }

                currentlyViewedMeshes = new Renderer[0];
            }
            else if (currentPlayer.redirectToEnemy != null)
            {
                if (currentPlayer.redirectToEnemy is MaskedPlayerEnemy masked)
                {
                    if (Plugin.CameraMode.Value == CameraModeOptions.Head)
                    {
                        currentActualTarget = masked.headTiltTarget;
                        offset = CAMERA_CONTAINER_OFFSET;
                    }
                    else
                    {
                        currentActualTarget = masked.animationContainer.Find("metarig/spine/spine.001/spine.002/spine.003");
                        offset = BODY_CAM_OFFSET;
                    }
                }
                else
                {
                    currentActualTarget = currentPlayer.redirectToEnemy.eye;
                }

                currentlyViewedMeshes = CollectModelsToHide(currentPlayer.redirectToEnemy.transform);
            }
            else if (currentPlayer.deadBody != null)
            {
                if (Plugin.CameraMode.Value == CameraModeOptions.Head)
                {
                    currentActualTarget = currentPlayer.deadBody.transform.Find("spine.001/spine.002/spine.003/spine.004/spine.004_end");
                    offset = CAMERA_CONTAINER_OFFSET - new Vector3(0, 0.15f, 0);
                }
                else
                {
                    currentActualTarget = currentPlayer.deadBody.transform.Find("spine.001/spine.002/spine.003");
                    offset = BODY_CAM_OFFSET;
                }
                currentlyViewedMeshes = CollectModelsToHide(currentPlayer.deadBody.transform);
            }
            else
            {
                SetTargetToNone();
                return;
            }

            CameraObject.transform.SetParent(currentActualTarget.transform, false);
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

        private void UpdateModelReferences()
        {
            currentPlayerCosmetics = CosmeticsCompatibility.CollectCosmetics(currentPlayer);
            currentPlayerModelState.cosmeticsLayers = new int[currentPlayerCosmetics.Length];
            localPlayerCosmetics = CosmeticsCompatibility.CollectCosmetics(StartOfRound.Instance.localPlayerController);
            localPlayerModelState.cosmeticsLayers = new int[localPlayerCosmetics.Length];
        }

        private enum Perspective
        {
            FirstPerson,
            ThirdPerson,
        }

        private static void SaveStateAndApplyPerspective(PlayerControllerB player, ref GameObject[] cosmetics, ref PlayerModelState state, Perspective perspective)
        {
            if (player == null)
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

            if (cosmetics.Length > 0)
            {
                if (cosmetics[0] == null)
                {
                    cosmetics = CosmeticsCompatibility.CollectCosmetics(player);
                    state.cosmeticsLayers = new int[cosmetics.Length];
                }

                for (int i = 0; i < cosmetics.Length; i++)
                    state.cosmeticsLayers[i] = cosmetics[i].layer;
            }

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
            if (player == null)
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

        private void BeginCameraRendering()
        {
            hasSetUpCamera = true;

            nightVisionLight.enabled = true;
            greenFlashRenderer.forceRenderingOff = false;
            fogShaderPlaneRenderer.forceRenderingOff = false;

            var localPlayer = StartOfRound.Instance.localPlayerController;

            SaveStateAndApplyPerspective(currentPlayer, ref currentPlayerCosmetics, ref currentPlayerModelState, Perspective.FirstPerson);
            if ((object)currentPlayer != localPlayer)
                SaveStateAndApplyPerspective(localPlayer, ref localPlayerCosmetics, ref localPlayerModelState, Perspective.ThirdPerson);

            foreach (var mesh in currentlyViewedMeshes)
            {
                if (mesh == null)
                    continue;
                mesh.forceRenderingOff = true;
            }
        }

        private void ResetCameraRendering()
        {
            if (!hasSetUpCamera)
                return;
            hasSetUpCamera = false;

            nightVisionLight.enabled = false;
            greenFlashRenderer.forceRenderingOff = true;
            fogShaderPlaneRenderer.forceRenderingOff = true;

            var localPlayer = StartOfRound.Instance.localPlayerController;

            RestoreState(currentPlayer, currentPlayerCosmetics, currentPlayerModelState);
            if ((object)currentPlayer != localPlayer)
                RestoreState(localPlayer, localPlayerCosmetics, localPlayerModelState);

            if (currentlyViewedMeshes.Length > 0 && currentlyViewedMeshes[0] == null)
                return;
            foreach (var mesh in currentlyViewedMeshes)
                mesh.forceRenderingOff = false;
        }

        void Update()
        {
            EnsureCameraExists();

            var spectatedPlayer = StartOfRound.Instance.localPlayerController;
            if (spectatedPlayer == null)
                return;
            if (spectatedPlayer.spectatedPlayerScript != null)
                spectatedPlayer = spectatedPlayer.spectatedPlayerScript;
            bool enableCamera = MonitorRenderer.isVisible
                && spectatedPlayer.isInHangarShipRoom
                && (object)MonitorRenderer.sharedMaterials[MonitorMaterialIndex] == MonitorOnMaterial;

            if (enableCamera)
            {
                var disable = ShouldHideOutput();
                enableCamera = !disable;
                if (disable != wasBlanked)
                {
                    SetScreenBlanked(disable);
                    wasBlanked = disable;
                }
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
