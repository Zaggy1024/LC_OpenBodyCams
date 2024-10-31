using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

using OpenBodyCams.Utilities;
using OpenBodyCams.API;
using OpenBodyCams.Patches;

namespace OpenBodyCams
{
    public class BodyCamComponent : MonoBehaviour
    {
        #region Public API
        #nullable enable
        public static BodyCamComponent[] GetAllBodyCams() { return [.. AllBodyCams]; }

        public delegate bool TargetChangedToTransform(BodyCamComponent bodyCam, Transform target, ref Transform attachmentPoint, ref Vector3 offset, ref Quaternion angle);
        // Called before the target is assigned to a transform. Currently, this will not be called
        // for players, masked enemies, or radar boosters.
        //
        // This can be used to override the attachment point and offset of the body cam on any
        // custom radar targets. Return true if you have set the attachment point, or if you
        // would like the body cam to attach to the root of your radar target.
        //
        // If you are only interested in reacting to the target of a body cam changing, please
        // use OnTargetChanged instead.
        public static event TargetChangedToTransform? BeforeTargetChangedToTransform;

        public delegate void RenderersToHideTransformer(BodyCamComponent bodyCam, ref Renderer[] renderers);
        // This can be used to append to or override the renderers that are hidden for non-player
        // targets. Any renderers in the list passed by reference to this event's handler will be
        // hidden when the body cam this component controls is rendered.
        //
        // The list provided to the event will be empty for players, but the renderers returned
        // will still be hidden, along with all the player models that are hidden/shown by default.
        public static event RenderersToHideTransformer? RenderersToHideTransformers;

        public Camera? GetCamera() { return Camera; }

        // This event is fired whenever the camera is created/recreated. No settings from the old
        // camera instances will carry over to a new camera, so this should be used to apply any
        // necessary settings to the new camera instance.
        public event Action<Camera>? OnCameraCreated;
        // This event will fire any time the render texture is created/recreated. The texture may
        // change when settings in the OpenBodyCams config are changed, or when any of this
        // component's properties that affect camera or texture settings are changed.
        public event Action<RenderTexture>? OnRenderTextureCreated;
        // Use this event to hide/show the output of the body cam wherever it is used. If this
        // event is ignored, then frozen or invalid video may display on your materials.
        public event Action<bool>? OnBlankedSet;
        // This event is fired when the camera's rendering status changes. See members of CameraRenderingStatus.
        public event Action<CameraRenderingStatus>? OnCameraStatusChanged;
        // This event is fired when the screen is powered off or on.
        public event Action<bool>? OnScreenPowerChanged;

        [Obsolete]
        public delegate Renderer[] GetRenderersToHide(Renderer[] renderers);
        [Obsolete("Use RenderersToHideTransformers")]
        public event GetRenderersToHide? OnRenderersToHideChanged;

        public event BodyCam.BodyCamStatusUpdate? OnTargetChanged;

        // Used by API users to indicate whether the camera this component controls is remote, i.e.
        // wirelessly connected to the ship. This is intended to be used by other mods to incorporate
        // effects like static based on gameplay events.
        public bool IsRemoteCamera
        {
            get { return isRemoteCamera; }
            set
            {
                if (isRemoteCamera != value)
                {
                    isRemoteCamera = value;
                    TargetHasChanged();
                }
            }
        }

        // Forces the camera to continue rendering regardless of its renderer's visibility,
        // as well as ignoring the option to disable cameras while their target is on the ship.
        public bool ForceEnableCamera { get => keepCameraOn; set => keepCameraOn = value; }

        // The resolution of the render texture that is created and assigned to the camera
        // every time it is instantiated.
        public Vector2Int Resolution
        {
            get => resolution;
            set
            {
                resolution = value;
                UpdateSettings();
            }
        }

        // Whether the camera is currently rendering to the texture.
        public bool IsBlanked { get => !CameraShouldRender(cameraStatus); }

        // Whether the camera is currently rendering to the texture.
        public CameraRenderingStatus CameraStatus { get => cameraStatus; }

        // The framerate at which to render the camera. Lower values may improve game performance.
        //
        // A value of 0 will result in rendering the camera every game frame.
        public float Framerate
        {
            get => 1f / timePerFrame;
            set
            {
                if (value != 0)
                    timePerFrame = 1.0f / value;
                else
                    timePerFrame = 0;
            }
        }

        // The player that the body cam is currently attached to and displaying the perspective of.
        public PlayerControllerB? CurrentPlayerTarget => currentPlayer;
        // The transform that the body cam is currently attached to. When the target is a player,
        // this will be the same as CurrentPlayerTarget.transform.
        //
        // When using this, please note that mods may set the target to any object, so this is not
        // only restricted to players, enemies, and radar boosters.
        public Transform? CurrentTarget => currentActualTarget;
        #nullable restore
        #endregion

        #region Global constants
        private const float RADAR_BOOSTER_INITIAL_PAN = 270;

        private static readonly Vector3 BODY_CAM_OFFSET = new(0.07f, 0, 0.16f);
        private static readonly Vector3 CAMERA_CONTAINER_OFFSET = new(0.07f, 0, 0.125f);
        #endregion

        #region Per-game constants
        private static readonly int CullModeProperty = Shader.PropertyToID("_CullMode");

        private static int bodyCamCullingMask;
        private static FrameSettings mainCameraCustomFrameSettings;
        private static FrameSettingsOverrideMask mainCameraCustomFrameSettingsMask;
        private static Material fogShaderMaterial;
        private static GameObject nightVisionPrefab;
        private static Light vanillaMapNightVisionLight;
        #endregion

        #region Static state
        private static BodyCamComponent[] AllBodyCams = [];

        private static BodyCamComponent lastBodyCamCulled;
        private static BodyCamComponent lastBodyCamRendered;
        #endregion

        #region Global options
        private static bool disableCameraWhileTargetIsOnShip = false;

        private static float radarBoosterPanSpeed;

        private static bool bruteForcePreventNullModels;
        #endregion

        #region Camera info
        private Transform CameraContainer;
        internal Transform CameraTransform;
        internal Camera Camera;
        #endregion

        #region Monitor info
        internal Renderer MonitorRenderer;
        internal int MonitorMaterialIndex = -1;
        internal Material MonitorOnMaterial;
        internal Material MonitorNoTargetMaterial;
        internal Material MonitorOffMaterial;
        internal Material MonitorDisabledMaterial;
        internal bool MonitorIsOn = true;
        #endregion

        #region Internal API state
        internal bool EnableCamera = true;

        private bool keepCameraOn = false;

        private static readonly Vector2Int DefaultResolution = new(160, 120);
        private Vector2Int resolution = DefaultResolution;

        private bool isRemoteCamera = true;
        #endregion

        #region Camera rendering state
        private bool vanillaMapNightVisionLightWasEnabled;

        private PlayerModelState localPlayerModelState;

        private PlayerControllerB currentPlayer;
        private PlayerModelState currentPlayerModelState;

        private Transform currentActualTarget;
        private Transform currentAttachmentPoint;
        private Renderer[] currentRenderersToHide = [];

        private Material currentObstructingMaterial;
        private float currentObstructingMaterialCullMode;

        private delegate void GetCameraPosition(in Vector3 position, ref bool isInInterior, ref bool isInShip);
        private GetCameraPosition cameraPositionGetter;

        private bool originalDirectSunlightEnabled;
        private bool originalIndirectSunlightEnabled;
        private bool targetSunlightEnabled = true;

        private float originalBlackSkyVolumeWeight;
        private float targetBlackSkyVolumeWeight = 0;

        private float originalIndirectSunlightDimmer;
        private float targetIndirectSunlightDimmer = 0;
        #endregion

        #region Objects for body cam rendering
        private Light nightVisionLight;
        private MeshRenderer greenFlashRenderer;
        private Animator greenFlashAnimator;

        private MeshRenderer fogShaderPlaneRenderer;
        #endregion

        #region General state
        private TargetDirtyStatus targetDirtyStatus = TargetDirtyStatus.None;

        private float elapsedSinceLastFrame = 0;
        private float timePerFrame = 0;

        private bool panCamera = false;
        private float panAngle = RADAR_BOOSTER_INITIAL_PAN;

        private CameraRenderingStatus cameraStatus = CameraRenderingStatus.Rendering;
        #endregion

        [Flags]
        private enum TargetDirtyStatus
        {
            None = 0,
            Immediate = 1,
            UntilRender = 2,
        }

        internal static void InitializeStatic()
        {
            PatchHDRenderPipeline.BeforeCameraCulling += BeforeCullingAnyCamera;
            PatchHDRenderPipeline.BeforeCameraRendering += BeforeRenderingAnyCamera;
            RenderPipelineManager.endCameraRendering += AfterRenderingAnyCamera;
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
        }

        internal static bool HasFinishedGameStartSetup()
        {
            return vanillaMapNightVisionLight != null;
        }

        public static void UpdateAllCameraSettings()
        {
            UpdateStaticSettings();

            foreach (var bodyCam in AllBodyCams)
                bodyCam.UpdateSettings();
        }

        internal static void UpdateStaticSettings()
        {
            disableCameraWhileTargetIsOnShip = Plugin.DisableCameraWhileTargetIsOnShip.Value;

            radarBoosterPanSpeed = Plugin.RadarBoosterPanRPM.Value * 360 / 60;

            bruteForcePreventNullModels = Plugin.BruteForcePreventFreezes.Value;
        }

        public static void MarkTargetDirtyUntilRenderForAllBodyCams(Transform target)
        {
            foreach (var bodyCam in AllBodyCams)
                bodyCam.MarkTargetDirtyUntilRender(target);
        }

        public static void MarkAnyParentDirtyUntilRenderForAllBodyCams(Transform target)
        {
            while (target != null)
            {
                MarkTargetDirtyUntilRenderForAllBodyCams(target);
                target = target.parent;
            }
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

        private static void RevertLastOverrides()
        {
            if (lastBodyCamCulled != null)
            {
                lastBodyCamCulled.RevertCullingOverrides();
                lastBodyCamCulled = null;
            }

            if (lastBodyCamRendered != null)
            {
                lastBodyCamRendered.RevertRenderingOverrides();
                lastBodyCamRendered = null;
            }
        }

        private static void BeforeCullingAnyCamera(ScriptableRenderContext context, Camera camera)
        {
            RevertLastOverrides();

            var bodyCamCount = AllBodyCams.Length;
            for (int i = 0; i < bodyCamCount; i++)
            {
                var bodyCam = AllBodyCams[i];
                if ((object)bodyCam.Camera == camera)
                {
                    lastBodyCamCulled = bodyCam;
                    bodyCam.ApplyCullingOverrides();
                    return;
                }
            }
        }

        internal static void BeforeRenderingAnyCamera(ScriptableRenderContext context, Camera camera)
        {
            RevertLastOverrides();

            var bodyCamCount = AllBodyCams.Length;
            for (int i = 0; i < bodyCamCount; i++)
            {
                var bodyCam = AllBodyCams[i];
                if ((object)bodyCam.Camera == camera)
                {
                    lastBodyCamRendered = bodyCam;
                    bodyCam.ApplyRenderingOverrides();
                    return;
                }
            }
        }

        internal static void AfterRenderingAnyCamera(ScriptableRenderContext context, Camera camera)
        {
            RevertLastOverrides();
        }

        void Awake()
        {
            if (!HasFinishedGameStartSetup())
            {
                Plugin.Instance.Logger.LogError("Attempted to create a body cam before static initialization has been completed.");
                Plugin.Instance.Logger.LogError("This may occur if the save is corrupted, or if a mod caused an error during the start of the game.");
                Destroy(this);
                return;
            }

            AllBodyCams = [.. AllBodyCams, this];

            BodyCam.BodyCamInstantiated(this);
        }

        void Start()
        {
            if (!HasFinishedGameStartSetup())
                return;
            CreateCamera();

            SyncBodyCamToRadarMap.UpdateBodyCamTarget(this);
        }

        private void SetMonitorMaterial(Material material)
        {
            if (MonitorRenderer == null)
                return;
            MonitorRenderer.SetMaterial(MonitorMaterialIndex, material);
        }

        private void EnsureMaterialsExist()
        {
            if (MonitorOnMaterial == null)
            {
                MonitorOnMaterial = new(Shader.Find("HDRP/Unlit")) { name = "BodyCamMaterial" };
                MonitorOnMaterial.SetFloat("_AlbedoAffectEmissive", 1);
                MonitorOnMaterial.SetColor("_EmissiveColor", Color.white);
            }

            if (MonitorOffMaterial == null)
                MonitorOffMaterial = ShipObjects.BlackScreenMaterial;
        }

        private void CreateCamera()
        {
            Plugin.Instance.Logger.LogInfo("Camera has been destroyed, recreating it.");
            UpdateScreenMaterial();

            CameraContainer = new GameObject("BodyCamContainer").transform;

            var cameraObject = new GameObject("BodyCam");
            CameraTransform = cameraObject.transform;
            CameraTransform.SetParent(CameraContainer, false);
            Camera = cameraObject.AddComponent<Camera>();
            Camera.nearClipPlane = 0.01f;
            Camera.cullingMask = bodyCamCullingMask;

            var cameraData = cameraObject.AddComponent<HDAdditionalCameraData>();
            cameraData.volumeLayerMask = 1;
            if (mainCameraCustomFrameSettings != null)
            {
                cameraData.customRenderingSettings = true;
                ref var frameSettings = ref cameraData.renderingPathCustomFrameSettings;
                ref var frameSettingsMask = ref cameraData.renderingPathCustomFrameSettingsOverrideMask;
                frameSettings = mainCameraCustomFrameSettings;
                frameSettingsMask = mainCameraCustomFrameSettingsMask;

                frameSettings.SetEnabled(FrameSettingsField.Tonemapping, false);
                frameSettingsMask.mask[(uint)FrameSettingsField.Tonemapping] = true;

                frameSettings.SetEnabled(FrameSettingsField.ColorGrading, false);
                frameSettingsMask.mask[(uint)FrameSettingsField.ColorGrading] = true;
            }

            // Make camera data persistent so that by setting Camera.enabled we don't incur
            // unnecessary overhead in the render pass.
            // This also prevents a flickering effect in the custom pass that occurs when
            // running a framerate limit.
            cameraData.hasPersistentHistory = true;

            var nightVision = Instantiate(nightVisionPrefab);
            nightVision.transform.SetParent(CameraTransform, false);
            nightVision.SetActive(true);
            nightVisionLight = nightVision.GetComponent<Light>();

            UpdateSettings();

            var greenFlashParent = new GameObject("CameraGreenTransitionScaler");
            greenFlashParent.transform.SetParent(CameraTransform, false);
            greenFlashParent.transform.localScale = new Vector3(1, 0.004f, 1);

            var greenFlashObject = Instantiate(StartOfRound.Instance.mapScreen.mapCameraAnimator.gameObject);
            greenFlashObject.transform.SetParent(greenFlashParent.transform, false);
            greenFlashObject.transform.localPosition = new Vector3(0, 0, 0.1f);
            greenFlashObject.layer = ViewPerspective.DEFAULT_LAYER;
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
            fogShaderPlane.transform.SetParent(CameraTransform, false);
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
            if (Camera == null)
                return;

            var horizontalResolution = resolution.x;
            var verticalResolution = resolution.y;

            Camera.targetTexture = new RenderTexture(horizontalResolution, verticalResolution, 32)
            {
                name = $"{name}.RenderTexture",
                filterMode = Plugin.MonitorTextureFiltering.Value,
            };
            Camera.fieldOfView = Plugin.FieldOfView.Value;

            if (MonitorOnMaterial != null)
                MonitorOnMaterial.mainTexture = Camera.targetTexture;

            Camera.farClipPlane = Plugin.RenderDistance.Value;

            nightVisionLight.intensity = Plugin.NightVisionIntensityBase * Plugin.NightVisionBrightness.Value;
            nightVisionLight.range = Plugin.NightVisionRangeBase * Plugin.NightVisionBrightness.Value;

            OnRenderTextureCreated?.Invoke(Camera.targetTexture);
        }

        public void StartTargetTransition()
        {
            if (Plugin.UseTargetTransitionAnimation.Value)
                greenFlashAnimator?.SetTrigger("Transition");
        }

        public void UpdateScreenMaterial()
        {
            if (!HasFinishedGameStartSetup())
                return;

            EnsureMaterialsExist();

            if (!MonitorIsOn)
            {
                if (MonitorOffMaterial != null)
                    SetMonitorMaterial(MonitorOffMaterial);
                return;
            }

            var isEnabled = enabled;
            if (IsBlanked || !isEnabled)
            {
                MonitorOnMaterial.color = Color.black;

                if (!isEnabled && MonitorDisabledMaterial != null)
                {
                    SetMonitorMaterial(MonitorDisabledMaterial);
                    return;
                }
                if (MonitorNoTargetMaterial != null)
                {
                    SetMonitorMaterial(MonitorNoTargetMaterial);
                    return;
                }
            }
            else
            {
                MonitorOnMaterial.color = Color.white;
            }

            SetMonitorMaterial(MonitorOnMaterial);
        }

        public void SetScreenPowered(bool powered)
        {
            if (MonitorRenderer == null)
                return;

            if (powered == MonitorIsOn)
                return;

            if (powered)
                StartTargetTransition();

            MonitorIsOn = powered;
            UpdateScreenMaterial();

            OnScreenPowerChanged?.Invoke(MonitorIsOn);
        }

        public bool IsScreenPowered()
        {
            return MonitorIsOn;
        }

        private bool CameraShouldRender(CameraRenderingStatus status)
        {
            return status == CameraRenderingStatus.Rendering;
        }

        private void SetStatus(CameraRenderingStatus newStatus)
        {
            if (newStatus != cameraStatus)
            {
                bool blankedChanged = CameraShouldRender(cameraStatus) == CameraShouldRender(newStatus);
                cameraStatus = newStatus;
                UpdateScreenMaterial();
                if (blankedChanged)
                    OnBlankedSet?.Invoke(IsBlanked);
                OnCameraStatusChanged?.Invoke(cameraStatus);
            }
        }

        private CameraRenderingStatus GetUpdatedCameraStatus()
        {
            if (!EnableCamera)
                return CameraRenderingStatus.Disabled;

            if (currentActualTarget == null)
                return CameraRenderingStatus.TargetInvalid;

            if (currentPlayer is not null)
            {
                if (currentPlayer.isPlayerControlled)
                {
                    if (currentPlayer.isInHangarShipRoom)
                        return CameraRenderingStatus.TargetDisabledOnShip;
                    return CameraRenderingStatus.Rendering;
                }

                if (!currentPlayer.isPlayerDead)
                    return CameraRenderingStatus.Rendering;

                if (currentPlayer.redirectToEnemy != null)
                {
                    if (currentPlayer.redirectToEnemy.isInsidePlayerShip)
                        return CameraRenderingStatus.TargetDisabledOnShip;
                    return CameraRenderingStatus.Rendering;
                }

                if (currentPlayer.deadBody != null)
                {
                    if (currentPlayer.deadBody.isInShip)
                        return CameraRenderingStatus.TargetDisabledOnShip;
                    return CameraRenderingStatus.Rendering;
                }
            }

            var radarBooster = currentActualTarget.GetComponent<RadarBoosterItem>();
            if (radarBooster is not null)
            {
                var beltBagPosition = new Vector3(3000, -400, 3000);
                if (radarBooster.targetFloorPosition.Equals(beltBagPosition))
                    return CameraRenderingStatus.TargetInvalid;

                if (radarBooster.isInShipRoom)
                    return CameraRenderingStatus.TargetDisabledOnShip;

                return CameraRenderingStatus.Rendering;
            }

            return CameraRenderingStatus.Rendering;
        }

        private static void CollectDescendentModelsToHide(Transform parent, List<Renderer> list)
        {
            // Skip all descendents of body cams. Otherwise, we will set visibility on
            // the green flash animation and make it visible to the main camera, and
            // invisible to the body cam.
            foreach (var bodyCam in AllBodyCams)
            {
                if (parent == bodyCam.CameraTransform)
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

        private void SetRenderersToHide(Renderer[] renderers)
        {
            if (currentActualTarget != null && OnRenderersToHideChanged != null)
                renderers = OnRenderersToHideChanged(renderers);

            RenderersToHideTransformers?.Invoke(this, ref renderers);

            currentRenderersToHide = renderers;
        }

        private void TargetHasChanged()
        {
            UpdateOverrides(float.PositiveInfinity);

            OnTargetChanged?.Invoke(this);
        }

        public void SetTargetToNone()
        {
            if (CameraContainer == null)
                return;

            ClearTargetDirtyImmediate();

            currentPlayer = null;
            currentActualTarget = null;
            currentAttachmentPoint = null;
            SetRenderersToHide([]);
            UpdateModelReferences();

            cameraPositionGetter = null;

            currentObstructingMaterial = null;

            CameraTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            TargetHasChanged();
        }

        public void SetTargetToPlayer(PlayerControllerB player)
        {
            if (CameraContainer == null)
                return;

            if (player == null)
            {
                SetTargetToNone();
                return;
            }

            ClearTargetDirtyImmediate();

            currentPlayer = player;
            UpdateModelReferences();

            panCamera = false;

            currentObstructingMaterial = null;

            var offset = Vector3.zero;

            if (!currentPlayer.isPlayerDead)
            {
                if (Plugin.CameraMode.Value == CameraModeOptions.Head)
                {
                    currentAttachmentPoint = currentPlayer.gameplayCamera.transform;
                    offset = CAMERA_CONTAINER_OFFSET;
                }
                else
                {
                    currentAttachmentPoint = currentPlayer.playerGlobalHead.transform.parent;
                    offset = BODY_CAM_OFFSET;
                }

                currentActualTarget = currentPlayer.transform;
                SetRenderersToHide([]);

                cameraPositionGetter = (in Vector3 _, ref bool isInInterior, ref bool isInShip) =>
                {
                    isInInterior = currentPlayer.isInsideFactory;
                    isInShip = currentPlayer.isInHangarShipRoom;
                };
            }
            else if (currentPlayer.redirectToEnemy != null)
            {
                if (currentPlayer.redirectToEnemy is MaskedPlayerEnemy masked)
                {
                    if (Plugin.CameraMode.Value == CameraModeOptions.Head)
                    {
                        currentAttachmentPoint = masked.headTiltTarget;
                        offset = CAMERA_CONTAINER_OFFSET;
                    }
                    else
                    {
                        currentAttachmentPoint = masked.animationContainer.Find("metarig/spine/spine.001/spine.002/spine.003");
                        offset = BODY_CAM_OFFSET;
                    }
                }
                else
                {
                    currentAttachmentPoint = currentPlayer.redirectToEnemy.eye;
                }

                currentActualTarget = currentPlayer.redirectToEnemy.transform;
                SetRenderersToHide(CollectModelsToHide(currentActualTarget));

                cameraPositionGetter = (in Vector3 _, ref bool isInInterior, ref bool isInShip) =>
                {
                    if (currentPlayer.redirectToEnemy == null)
                        return;
                    isInInterior = !currentPlayer.redirectToEnemy.isOutside;
                    isInShip = currentPlayer.redirectToEnemy.isInsidePlayerShip;
                };
            }
            else if (currentPlayer.deadBody != null)
            {
                Transform obstructingMeshParent;
                if (Plugin.CameraMode.Value == CameraModeOptions.Head)
                {
                    currentAttachmentPoint = currentPlayer.deadBody.transform.Find("spine.001/spine.002/spine.003/spine.004/spine.004_end");
                    obstructingMeshParent = currentAttachmentPoint.parent;
                    offset = CAMERA_CONTAINER_OFFSET;
                }
                else
                {
                    currentAttachmentPoint = currentPlayer.deadBody.transform.Find("spine.001/spine.002/spine.003");
                    obstructingMeshParent = currentAttachmentPoint;
                    offset = BODY_CAM_OFFSET;
                }

                currentActualTarget = currentPlayer.deadBody.transform;
                SetRenderersToHide(CollectModelsToHide(obstructingMeshParent));
                currentObstructingMaterial = currentActualTarget.GetComponent<Renderer>()?.sharedMaterial;

                cameraPositionGetter = (in Vector3 _, ref bool isInInterior, ref bool isInShip) =>
                {
                    if (currentPlayer.deadBody == null)
                        return;
                    if (currentPlayer.deadBody.grabBodyObject != null)
                        isInInterior = currentPlayer.deadBody.grabBodyObject.isInFactory;
                    isInShip = currentPlayer.deadBody.isInShip;
                };
            }

            CameraTransform.SetLocalPositionAndRotation(offset, Quaternion.identity);

            TargetHasChanged();
        }

        public void SetTargetToTransform(Transform transform)
        {
            if (CameraContainer == null)
                return;

            if (transform == null || transform.gameObject == null)
            {
                SetTargetToNone();
                return;
            }

            ClearTargetDirtyImmediate();

            currentPlayer = null;
            currentActualTarget = transform;
            currentAttachmentPoint = null;
            UpdateModelReferences();

            panCamera = false;

            var offset = Vector3.zero;
            var angle = Quaternion.identity;

            if (currentActualTarget.GetComponent<RadarBoosterItem>() is { } radarBooster)
            {
                SetRenderersToHide([currentActualTarget.transform.Find("AnimContainer/Rod").GetComponent<Renderer>()]);
                currentAttachmentPoint = currentActualTarget;
                offset = new Vector3(0, 1.5f, 0);
                panCamera = true;

                cameraPositionGetter = (in Vector3 _, ref bool isInInterior, ref bool isInShip) =>
                {
                    if (currentActualTarget != radarBooster.transform)
                        return;
                    isInInterior = radarBooster.isInFactory;
                    isInShip = radarBooster.isInShipRoom;
                };
            }
            else
            {
                cameraPositionGetter = (in Vector3 position, ref bool isInInterior, ref bool isInShip) =>
                {
                    isInInterior = position.y < -80;
                };

                currentAttachmentPoint = currentActualTarget;

                if (BeforeTargetChangedToTransform != null)
                {
                    foreach (var handler in BeforeTargetChangedToTransform.GetInvocationList())
                    {
                        if (((TargetChangedToTransform)handler).Invoke(this, currentActualTarget, ref currentAttachmentPoint, ref offset, ref angle))
                            break;
                    }
                }
            }

            CameraTransform.SetLocalPositionAndRotation(offset, angle);

            TargetHasChanged();
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
            ViewPerspective.PrepareModelState(currentPlayer, ref currentPlayerModelState);
            if (currentPlayer != StartOfRound.Instance.localPlayerController)
                ViewPerspective.PrepareModelState(StartOfRound.Instance.localPlayerController, ref localPlayerModelState);
        }

        private void UpdateTargetStatusBeforeRender()
        {
            if (targetDirtyStatus.HasFlag(TargetDirtyStatus.UntilRender))
            {
                UpdateTargetStatus();
                targetDirtyStatus ^= TargetDirtyStatus.UntilRender;
            }
        }

        private void ApplyCullingOverrides()
        {
            UpdateTargetStatusBeforeRender();

            CameraContainer.SetPositionAndRotation(currentAttachmentPoint.position, currentAttachmentPoint.rotation);

            vanillaMapNightVisionLightWasEnabled = vanillaMapNightVisionLight.enabled;
            vanillaMapNightVisionLight.enabled = false;

            nightVisionLight.enabled = true;
            greenFlashRenderer.forceRenderingOff = false;
            fogShaderPlaneRenderer.forceRenderingOff = false;

            var localPlayer = StartOfRound.Instance.localPlayerController;

            ViewPerspective.Apply(ref currentPlayerModelState, Perspective.FirstPerson);
            if ((object)currentPlayer != localPlayer)
                ViewPerspective.Apply(ref localPlayerModelState, Perspective.ThirdPerson);

            bool warnedNullMesh = false;
            foreach (var mesh in currentRenderersToHide)
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

            if (currentObstructingMaterial != null)
            {
                currentObstructingMaterialCullMode = currentObstructingMaterial.GetFloat(CullModeProperty);
                currentObstructingMaterial.SetFloat(CullModeProperty, (float)CullMode.Back);
            }

            var sunDirect = TimeOfDay.Instance.sunDirect;
            if (sunDirect != null)
            {
                var sunIndirect = TimeOfDay.Instance.sunIndirect;

                originalDirectSunlightEnabled = sunDirect.enabled;
                originalIndirectSunlightEnabled = sunIndirect.enabled;
                sunDirect.enabled = targetSunlightEnabled;
                sunIndirect.enabled = targetSunlightEnabled;
            }

            var blackSkyVolume = StartOfRound.Instance.blackSkyVolume;
            originalBlackSkyVolumeWeight = blackSkyVolume.weight;
            blackSkyVolume.weight = targetBlackSkyVolumeWeight;
        }

        private void RevertCullingOverrides()
        {
            vanillaMapNightVisionLight.enabled = vanillaMapNightVisionLightWasEnabled;

            nightVisionLight.enabled = false;
            greenFlashRenderer.forceRenderingOff = true;
            fogShaderPlaneRenderer.forceRenderingOff = true;

            var localPlayer = StartOfRound.Instance.localPlayerController;

            ViewPerspective.Restore(ref currentPlayerModelState);
            if ((object)currentPlayer != localPlayer)
                ViewPerspective.Restore(ref localPlayerModelState);

            foreach (var mesh in currentRenderersToHide)
            {
                if (mesh == null)
                    continue;
                mesh.forceRenderingOff = false;
            }

            currentObstructingMaterial?.SetFloat(CullModeProperty, currentObstructingMaterialCullMode);

            var sunDirect = TimeOfDay.Instance.sunDirect;
            if (sunDirect != null)
            {
                var sunIndirect = TimeOfDay.Instance.sunIndirect;

                sunDirect.enabled = originalDirectSunlightEnabled;
                sunIndirect.enabled = originalIndirectSunlightEnabled;
            }

            var blackSkyVolume = StartOfRound.Instance.blackSkyVolume;
            blackSkyVolume.weight = originalBlackSkyVolumeWeight;
        }

        private void ApplyRenderingOverrides()
        {
            var sunIndirectHDRP = TimeOfDay.Instance.indirectLightData;
            if (sunIndirectHDRP != null)
            {
                originalIndirectSunlightDimmer = sunIndirectHDRP.lightDimmer;
                sunIndirectHDRP.lightDimmer = targetIndirectSunlightDimmer;
                // Grab the clamped value from the component.
                targetIndirectSunlightDimmer = sunIndirectHDRP.lightDimmer;
            }
        }

        private void RevertRenderingOverrides()
        {
            var sunIndirectHDRP = TimeOfDay.Instance.indirectLightData;
            if (sunIndirectHDRP != null)
                sunIndirectHDRP.lightDimmer = originalIndirectSunlightDimmer;
        }

        private void UpdateTargetStatusDuringUpdate()
        {
            if (targetDirtyStatus.HasFlag(TargetDirtyStatus.Immediate))
                UpdateTargetStatus();
        }

        private void UpdateOverrides(float deltaTime)
        {
            var isInInterior = false;
            var isInShip = false;
            cameraPositionGetter?.Invoke(CameraTransform.position, ref isInInterior, ref isInShip);

            targetSunlightEnabled = !isInInterior;
            targetBlackSkyVolumeWeight = isInInterior ? 1 : 0;
            targetIndirectSunlightDimmer = Mathf.Lerp(targetIndirectSunlightDimmer, isInShip ? 0 : 1, Mathf.Clamp01(5 * deltaTime));
        }

        private void LateUpdate()
        {
            UpdateTargetStatusDuringUpdate();

            var spectatedPlayer = StartOfRound.Instance.localPlayerController;
            if (spectatedPlayer == null)
                return;
            if (spectatedPlayer.spectatedPlayerScript != null)
                spectatedPlayer = spectatedPlayer.spectatedPlayerScript;

            UpdateOverrides(Time.deltaTime);

            bool enableCameraThisFrame = keepCameraOn ||
                (MonitorRenderer != null
                && MonitorRenderer.isVisible
                && spectatedPlayer.isInHangarShipRoom
                && IsScreenPowered());

            if (enableCameraThisFrame)
            {
                var newStatus = GetUpdatedCameraStatus();
                if (newStatus == CameraRenderingStatus.TargetDisabledOnShip && (!disableCameraWhileTargetIsOnShip || keepCameraOn))
                    newStatus = CameraRenderingStatus.Rendering;
                SetStatus(newStatus);
                enableCameraThisFrame = !IsBlanked;
            }

            if (enableCameraThisFrame && bruteForcePreventNullModels)
            {
                // Brute force check if all models are still valid to prevent rendering from failing and
                // causing a frozen screen.
                bool foundNull = false;

                if (currentPlayer != null && !currentPlayerModelState.VerifyCosmeticsExist(currentPlayer.playerUsername))
                    foundNull = true;
                var localPlayer = StartOfRound.Instance.localPlayerController;
                if ((object)currentPlayer != localPlayer && !localPlayerModelState.VerifyCosmeticsExist(localPlayer.playerUsername))
                    foundNull = true;

                foreach (var renderer in currentRenderersToHide)
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

            if (!enableCameraThisFrame)
            {
                Camera.enabled = false;
                return;
            }

            if (radarBoosterPanSpeed != 0)
                panAngle = (panAngle + (Time.deltaTime * radarBoosterPanSpeed)) % 360;
            else
                panAngle = RADAR_BOOSTER_INITIAL_PAN;
            if (panCamera)
                CameraTransform.localRotation = Quaternion.Euler(0, panAngle, 0);

            if (timePerFrame > 0)
            {
                elapsedSinceLastFrame += Time.deltaTime;
                Camera.enabled = elapsedSinceLastFrame >= timePerFrame;
                elapsedSinceLastFrame %= timePerFrame;
            }
            else
            {
                Camera.enabled = true;
            }
        }

        void OnDisable()
        {
            SetStatus(CameraRenderingStatus.Disabled);
            UpdateScreenMaterial();
            Camera.enabled = false;
        }

        void OnEnable()
        {
            UpdateScreenMaterial();
        }

        void OnDestroy()
        {
            if (CameraContainer != null)
                Destroy(CameraContainer.gameObject);

            AllBodyCams = AllBodyCams.Where(bodyCam => (object)bodyCam != this).ToArray();

            SyncBodyCamToRadarMap.OnBodyCamDestroyed(this);

            BodyCam.BodyCamDestroyed(this);
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
            if (Array.IndexOf(currentRenderersToHide, renderer) != -1)
                return true;
            return false;
        }

        public bool HasReference(GameObject gameObject)
        {
            if (targetDirtyStatus.HasFlag(TargetDirtyStatus.UntilRender))
                return false;
            if (targetDirtyStatus.HasFlag(TargetDirtyStatus.Immediate))
                UpdateTargetStatus();

            if (currentPlayerModelState.ReferencesObject(gameObject))
                return true;
            if (localPlayerModelState.ReferencesObject(gameObject))
                return true;
            return false;
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
}
