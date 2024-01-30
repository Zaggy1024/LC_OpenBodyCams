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

        public static readonly Vector3 BODY_CAM_OFFSET = new Vector3(0.07f, 0, 0.15f);
        public static readonly Vector3 CAMERA_CONTAINER_OFFSET = new Vector3(0.07f, 0, 0.125f);

        public static BodyCamComponent[] AllBodyCams = new BodyCamComponent[0];

        private static Material fogShaderMaterial;
        private static GameObject nightVisionPrefab;

        private static bool disableCameraWhileTargetIsOnShip = false;
        private static Color screenEmissiveColor;

        private static float radarBoosterPanSpeed;

        public GameObject cameraObject;
        public Camera camera;
        public Light nightVisionLight;

        public MeshRenderer monitorRenderer;
        public int monitorMaterialIndex;
        public Material monitorOnMaterial;
        public Material monitorOffMaterial;

        private bool enableCamera = true;
        private bool wasBlanked = false;

        private bool hasSetUpCamera = false;

        private GameObject[] localPlayerCosmetics = new GameObject[0];
        private PlayerModelState localPlayerModelState;

        private PlayerControllerB currentPlayer;
        private GameObject[] currentPlayerCosmetics = new GameObject[0];
        private PlayerModelState currentPlayerModelState;

        private Transform currentActualTarget;
        private Renderer[] currentlyViewedMeshes = new Renderer[0];

        private float elapsedSinceLastFrame = 0;
        private float timePerFrame = 0;

        private bool panCamera = false;
        private float panAngle = RADAR_BOOSTER_INITIAL_PAN;

        private MeshRenderer greenFlashRenderer;
        private Animator greenFlashAnimator;

        private MeshRenderer fogShaderPlaneRenderer;

        public static void InitializeStatic()
        {
            RenderPipelineManager.beginCameraRendering += BeginAnyCameraRendering;
            RenderPipelineManager.endCameraRendering += EndAnyCameraRendering;
        }

        public static void InitializeAtStartOfGame()
        {
            var aPlayerScript = StartOfRound.Instance.allPlayerScripts[0];

            fogShaderMaterial = aPlayerScript.localVisor.transform.Find("ScavengerHelmet/Plane").GetComponent<MeshRenderer>().sharedMaterial;

            nightVisionPrefab = Instantiate(aPlayerScript.nightVision.gameObject);
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

        private static Color GetEmissiveColor()
        {
            Color ParseColor(string str)
            {
                var components = str
                    .Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => float.Parse(x.Trim(), CultureInfo.InvariantCulture))
                    .ToArray();
                if (components.Length < 0 || components.Length > 4)
                    throw new ArgumentException("Too many color components");
                return new Color(components[0], components[1], components[2], components.Length == 4 ? components[3] : 0);
            }
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

        public static void UpdateStaticSettings()
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
                if ((object)bodyCam.camera == camera)
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
                if ((object)bodyCam.camera == camera)
                {
                    bodyCam.ResetCameraRendering();
                    return;
                }
            }
        }

        void Awake()
        {
            AllBodyCams = AllBodyCams.Append(this).ToArray();

            monitorOnMaterial = new Material(Shader.Find("HDRP/Unlit"));
            monitorOnMaterial.name = "BodyCamMaterial";
            monitorOnMaterial.SetFloat("_AlbedoAffectEmissive", 1);

            monitorOffMaterial = ShipObjects.blackScreenMaterial;

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
            SetMaterial(monitorRenderer, monitorMaterialIndex, monitorOnMaterial);
        }

        private static void SetMaterial(MeshRenderer renderer, int index, Material material)
        {
            var materials = renderer.sharedMaterials;
            materials[index] = material;
            renderer.sharedMaterials = materials;
        }

        public void EnsureCameraExists()
        {
            if (cameraObject != null)
                return;

            Plugin.Instance.Logger.LogInfo("Camera has been destroyed, recreating it.");

            cameraObject = new GameObject("BodyCam");
            camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.cullingMask = 0b0010_0001_0011_1011_0001_0111_0101_1011;
            var cameraData = cameraObject.AddComponent<HDAdditionalCameraData>();
            cameraData.volumeLayerMask = 1;

            var nightVision = Instantiate(nightVisionPrefab);
            nightVision.transform.SetParent(cameraObject.transform, false);
            nightVision.SetActive(true);
            nightVisionLight = nightVision.GetComponent<Light>();

            UpdateSettings();

            var greenFlashParent = new GameObject("CameraGreenTransitionScaler");
            greenFlashParent.transform.SetParent(cameraObject.transform, false);
            greenFlashParent.transform.localScale = new Vector3(1, 0.004f, 1);

            var greenFlashObject = Instantiate(StartOfRound.Instance.mapScreen.mapCameraAnimator.gameObject);
            greenFlashObject.transform.SetParent(greenFlashParent.transform, false);
            greenFlashObject.transform.localPosition = new Vector3(0, 0, 0.1f);
            greenFlashObject.layer = DEFAULT_LAYER;
            greenFlashRenderer = greenFlashObject.GetComponent<MeshRenderer>();
            greenFlashRenderer.forceRenderingOff = true;
            greenFlashAnimator = greenFlashObject.GetComponent<Animator>() ?? throw new Exception("Green flash object copied from the map screen has no Animator.");

            var fogShaderPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Destroy(fogShaderPlane.GetComponent<MeshCollider>());
            fogShaderPlane.transform.SetParent(cameraObject.transform, false);
            fogShaderPlane.transform.localPosition = new Vector3(0, 0, 0.5f);
            fogShaderPlane.transform.localRotation = Quaternion.Euler(270, 0, 0);
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
            camera.targetTexture = new RenderTexture(Plugin.HorizontalResolution.Value, Plugin.HorizontalResolution.Value * 3 / 4, 32);
            camera.targetTexture.filterMode = Plugin.MonitorTextureFiltering.Value;
            camera.fieldOfView = Plugin.FieldOfView.Value;

            monitorOnMaterial.mainTexture = camera.targetTexture;
            monitorOnMaterial.SetColor("_EmissiveColor", screenEmissiveColor);

            camera.farClipPlane = Plugin.RenderDistance.Value;

            if (Plugin.Framerate.Value != 0)
            {
                timePerFrame = 1.0f / Plugin.Framerate.Value;
                camera.enabled = false;
            }
            else
            {
                timePerFrame = 0;
                camera.enabled = false;
            }

            nightVisionLight.intensity = Plugin.NightVisionIntensityBase * Plugin.NightVisionBrightness.Value;
            nightVisionLight.range = Plugin.NightVisionRangeBase * Plugin.NightVisionBrightness.Value;

            enableCamera = Plugin.EnableCamera.Value;
        }

        public void StartTargetTransition()
        {
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
                if (monitorRenderer.sharedMaterials[monitorMaterialIndex] == monitorOffMaterial)
                    StartTargetTransition();
                SetMaterial(monitorRenderer, monitorMaterialIndex, monitorOnMaterial);
                return;
            }

            SetMaterial(monitorRenderer, monitorMaterialIndex, monitorOffMaterial);
        }

        public void SetScreenBlanked(bool blanked)
        {
            if (blanked != wasBlanked)
                monitorOnMaterial.color = blanked ? Color.black : Color.white;
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
            currentlyViewedMeshes = new Renderer[0];
            cameraObject.transform.SetParent(null, false);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;
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

            cameraObject.transform.SetParent(currentActualTarget.transform, false);
            cameraObject.transform.localPosition = offset;
            cameraObject.transform.localRotation = Quaternion.identity;
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
                currentlyViewedMeshes = new Renderer[] { currentActualTarget.transform.Find("AnimContainer/Rod").GetComponent<Renderer>() };
                offset = new Vector3(0, 1.5f, 0);
                panCamera = true;
            }

            cameraObject.transform.SetParent(currentActualTarget.transform, false);
            cameraObject.transform.localPosition = offset;
            cameraObject.transform.localRotation = Quaternion.identity;
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
            if (currentlyViewedMeshes.Length > 0 && currentlyViewedMeshes[0] == null)
                return;
            foreach (var mesh in currentlyViewedMeshes)
                mesh.forceRenderingOff = true;

            nightVisionLight.enabled = true;
            greenFlashRenderer.forceRenderingOff = false;
            fogShaderPlaneRenderer.forceRenderingOff = false;

            var localPlayer = StartOfRound.Instance.localPlayerController;

            SaveStateAndApplyPerspective(currentPlayer, ref currentPlayerCosmetics, ref currentPlayerModelState, Perspective.FirstPerson);
            if ((object)currentPlayer != localPlayer)
                SaveStateAndApplyPerspective(localPlayer, ref localPlayerCosmetics, ref localPlayerModelState, Perspective.ThirdPerson);

            hasSetUpCamera = true;
        }

        private void ResetCameraRendering()
        {
            if (!hasSetUpCamera)
                return;
            hasSetUpCamera = false;

            if (currentlyViewedMeshes.Length > 0 && currentlyViewedMeshes[0] == null)
                return;
            foreach (var mesh in currentlyViewedMeshes)
                mesh.forceRenderingOff = false;

            nightVisionLight.enabled = false;
            greenFlashRenderer.forceRenderingOff = true;
            fogShaderPlaneRenderer.forceRenderingOff = true;

            var localPlayer = StartOfRound.Instance.localPlayerController;

            RestoreState(currentPlayer, currentPlayerCosmetics, currentPlayerModelState);
            if ((object)currentPlayer != localPlayer)
                RestoreState(localPlayer, localPlayerCosmetics, localPlayerModelState);
        }

        public void Update()
        {
            EnsureCameraExists();

            var spectatedPlayer = StartOfRound.Instance.localPlayerController;
            if (spectatedPlayer == null)
                return;
            if (spectatedPlayer.spectatedPlayerScript != null)
                spectatedPlayer = spectatedPlayer.spectatedPlayerScript;
            bool enableCamera = monitorRenderer.isVisible
                && spectatedPlayer.isInHangarShipRoom
                && (object)monitorRenderer.sharedMaterials[monitorMaterialIndex] == monitorOnMaterial;

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
                camera.enabled = false;
                return;
            }

            if (radarBoosterPanSpeed != 0)
                panAngle = (panAngle + (Time.deltaTime * radarBoosterPanSpeed)) % 360;
            else
                panAngle = RADAR_BOOSTER_INITIAL_PAN;
            if (panCamera)
                cameraObject.transform.localRotation = Quaternion.Euler(0, panAngle, 0);

            if (timePerFrame > 0)
            {
                elapsedSinceLastFrame += Time.deltaTime;
                if (elapsedSinceLastFrame >= timePerFrame)
                {
                    camera.Render();
                    elapsedSinceLastFrame %= timePerFrame;
                }
            }
            else
            {
                camera.enabled = true;
            }
        }

        public void OnDestroy()
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
