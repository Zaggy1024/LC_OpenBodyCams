using System;
using System.Collections;
using System.Linq;

using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

using OpenBodyCams.Patches;

namespace OpenBodyCams
{
    public class BodyCamComponent : MonoBehaviour
    {
        const int DEFAULT_LAYER = 0;
        const int ENEMIES_LAYER = 19;
        const int ENEMIES_NOT_RENDERED_LAYER = 23;
        const int BODY_CAM_ONLY_LAYER = 31;
        const float PAN_SPEED = 40.0f;

        public static readonly Vector3 BODY_CAM_OFFSET = new Vector3(0.07f, 0, 0.15f);
        public static readonly Vector3 CAMERA_CONTAINER_OFFSET = new Vector3(0.07f, 0, 0.125f);

        private static Material fogShaderMaterial;
        private static GameObject nightVisionPrefab;

        public GameObject cameraObject;
        public Camera camera;
        public Light nightVisionLight;

        private ManualCameraRenderer mapRenderer;

        private MeshRenderer monitorMesh;
        private Material monitorMaterial;

        private GameObject[] localPlayerMoreCompanyCosmetics = new GameObject[0];
        private PlayerModelState localPlayerModelState;

        private PlayerControllerB currentPlayer;
        private GameObject[] currentPlayerMoreCompanyCosmetics = new GameObject[0];
        private PlayerModelState currentPlayerModelState;

        private Transform currentActualTarget;
        private Renderer[] currentlyViewedMeshes = new Renderer[0];

        private float elapsedSinceLastFrame = 0;
        private float timePerFrame = 0;

        private bool panCamera = false;
        private float panAngle = 0;

        private Animator greenFlashAnimator;

        public void Start()
        {
            Plugin.BodyCam = this;

            mapRenderer = GetComponentsInChildren<ManualCameraRenderer>().First(renderer => renderer.cam == renderer.mapCamera);

            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
            RenderPipelineManager.endCameraRendering += EndCameraRendering;

            monitorMesh = GetComponent<MeshRenderer>();
            monitorMaterial = monitorMesh.materials.First(material => material.mainTexture.name == "shipScreen2") ?? throw new Exception("Failed to get the ship screen material.");

            var aPlayerScript = StartOfRound.Instance.allPlayerScripts[0];

            fogShaderMaterial = aPlayerScript.localVisor.transform.Find("ScavengerHelmet/Plane").GetComponent<MeshRenderer>().sharedMaterial;

            nightVisionPrefab = Instantiate(aPlayerScript.nightVision.gameObject);
            nightVisionPrefab.name = "NightVision";
            nightVisionPrefab.transform.localPosition = Vector3.zero;
            nightVisionPrefab.SetActive(false);
            var nightVisionLight = nightVisionPrefab.GetComponent<Light>();
            nightVisionLight.enabled = true;
            nightVisionLight.cullingMask = 1 << BODY_CAM_ONLY_LAYER;

            // By default, the map's night vision light renders on all layers, so let's change that so we don't see it on the body cam.
            var mapLight = StartOfRound.Instance.mapScreen.mapCameraLight;
            mapLight.cullingMask = 1 << mapLight.gameObject.layer;

            EnsureCameraExists();
        }

        public void EnsureCameraExists()
        {
            if (cameraObject != null)
                return;

            cameraObject = new GameObject("BodyCam");
            camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.cullingMask = unchecked((int)0b1010_0001_0011_1011_0001_0111_0101_1011);
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
            greenFlashObject.layer = BODY_CAM_ONLY_LAYER;
            greenFlashAnimator = greenFlashObject.GetComponent<Animator>() ?? throw new Exception("Green flash object copied from the map screen has no Animator.");

            var fogShaderPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            fogShaderPlane.transform.SetParent(cameraObject.transform, false);
            var fogShaderPlaneMesh = fogShaderPlane.GetComponent<MeshRenderer>();
            fogShaderPlaneMesh.sharedMaterial = fogShaderMaterial;
            fogShaderPlaneMesh.shadowCastingMode = ShadowCastingMode.Off;
            fogShaderPlaneMesh.receiveShadows = false;
            fogShaderPlane.transform.localPosition = new Vector3(0, 0, 0.5f);
            fogShaderPlane.transform.localRotation = Quaternion.Euler(270, 0, 0);
            fogShaderPlane.layer = BODY_CAM_ONLY_LAYER;
            Destroy(fogShaderPlane.GetComponent<MeshCollider>());
        }

        public void UpdateSettings()
        {
            camera.targetTexture = new RenderTexture(Plugin.HorizontalResolution.Value, Plugin.HorizontalResolution.Value * 3 / 4, 32);
            camera.fieldOfView = Plugin.FieldOfView.Value;
            monitorMaterial.mainTexture = camera.targetTexture;

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

            UpdateCurrentTarget();
        }

        public void StartTargetTransition()
        {
            greenFlashAnimator.SetTrigger("Transition");
        }

        private static void SetCosmeticHidden(GameObject cosmetic, bool hidden)
        {
            cosmetic.layer = hidden ? ENEMIES_NOT_RENDERED_LAYER : DEFAULT_LAYER;
        }

        private static GameObject[] CollectMoreCompanyCosmetics(PlayerControllerB player, bool hidden)
        {
            if (!Plugin.EnableMoreCompanyCosmeticsCompatibility.Value)
                return new GameObject[0];
            if (player == null)
                return new GameObject[0];

            if (player.GetComponentInChildren(MoreCompanyCompatibilityPatch.t_CosmeticApplication) is Behaviour cosmeticApplication)
            {
                Plugin.Instance.Logger.LogInfo($"Getting MoreCompany cosmetic models for {player.playerUsername}");
                var cosmeticsList = (IList)MoreCompanyCompatibilityPatch.f_CosmeticApplication_spawnedCosmetics.GetValue(cosmeticApplication);
                var result = cosmeticsList.Cast<Component>().SelectMany(cosmetic => cosmetic.GetComponentsInChildren<Transform>()).Select(cosmeticObject => cosmeticObject.gameObject).ToArray();
                cosmeticApplication.enabled = true;
                foreach (var cosmeticObject in result)
                    SetCosmeticHidden(cosmeticObject, hidden);
                return result;
            }

            return new GameObject[0];
        }

        public void UpdateCurrentTarget()
        {
            UpdateCurrentTargetInternal();
            if (currentActualTarget == null)
            {
                cameraObject.transform.SetParent(null, false);
                cameraObject.transform.localPosition = Vector3.zero;
                cameraObject.transform.localRotation = Quaternion.identity;
            }
        }

        private void UpdateCurrentTargetInternal()
        {
            EnsureCameraExists();

            // Ensure that we have a reference to null if the targeted player is being destroyed.
            currentPlayer = mapRenderer.targetedPlayer;
            currentActualTarget = mapRenderer.radarTargets[mapRenderer.targetTransformIndex].transform;
            currentlyViewedMeshes = new Renderer[0];

            if (currentActualTarget == null)
                return;

            if (MoreCompanyCompatibilityPatch.f_CosmeticApplication_spawnedCosmetics is object)
            {
                currentPlayerMoreCompanyCosmetics = CollectMoreCompanyCosmetics(currentPlayer, hidden: false);
                localPlayerMoreCompanyCosmetics = CollectMoreCompanyCosmetics(StartOfRound.Instance.localPlayerController, hidden: true);
            }

            Vector3 offset = Vector3.zero;

            Renderer[] CollectModelsToHide(Transform parent)
            {
                return parent.GetComponentsInChildren<Renderer>().Where(r => r.gameObject.layer == DEFAULT_LAYER || r.gameObject.layer == ENEMIES_LAYER).ToArray();
            }

            if (currentActualTarget.GetComponent<RadarBoosterItem>() != null)
            {
                currentlyViewedMeshes = new Renderer[] { currentActualTarget.transform.Find("AnimContainer/Rod").GetComponent<Renderer>() };
                offset = new Vector3(0, 1.5f, 0);
                panCamera = true;
            }
            else if (currentPlayer is object)
            {
                if (currentPlayer.isPlayerDead)
                {
                    if (currentPlayer.redirectToEnemy != null)
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
                }
                else
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
                }
                panCamera = false;
            }

            if (currentActualTarget == null)
                return;

            cameraObject.transform.SetParent(currentActualTarget.transform, false);
            cameraObject.transform.localPosition = offset;
            cameraObject.transform.localRotation = Quaternion.identity;
        }

        private enum Perspective
        {
            FirstPerson,
            ThirdPerson,
        }

        private static void SaveStateAndApplyPerspective(PlayerControllerB player, ref GameObject[] moreCompanyCosmetics, ref PlayerModelState state, Perspective perspective)
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

            if (moreCompanyCosmetics.Length > 0)
            {
                if (moreCompanyCosmetics[0] == null)
                    moreCompanyCosmetics = CollectMoreCompanyCosmetics(player, hidden: false);
                state.moreCompanyCosmeticsLayer = moreCompanyCosmetics[0].layer;
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

                    foreach (var cosmetic in moreCompanyCosmetics)
                        SetCosmeticHidden(cosmetic, true);
                    break;
                case Perspective.ThirdPerson:
                    player.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On;
                    player.thisPlayerModel.gameObject.layer = DEFAULT_LAYER;

                    player.thisPlayerModelArms.enabled = false;
                    player.thisPlayerModelArms.gameObject.layer = ENEMIES_NOT_RENDERED_LAYER;

                    if (player.currentlyHeldObjectServer != null)
                        AttachItem(player.currentlyHeldObjectServer, player.serverItemHolder);

                    foreach (var cosmetic in moreCompanyCosmetics)
                        SetCosmeticHidden(cosmetic, false);
                    break;
            }
        }

        private static void RestoreState(PlayerControllerB player, GameObject[] moreCompanyCosmetics, PlayerModelState state)
        {
            if (player == null)
                return;

            player.thisPlayerModel.shadowCastingMode = state.bodyShadowMode;
            player.thisPlayerModel.gameObject.layer = state.bodyLayer;

            player.thisPlayerModelArms.enabled = state.armsEnabled;
            player.thisPlayerModelArms.gameObject.layer = state.armsLayer;

            foreach (var cosmetic in moreCompanyCosmetics)
                cosmetic.layer = state.moreCompanyCosmeticsLayer;

            if (player.currentlyHeldObjectServer != null)
            {
                player.currentlyHeldObjectServer.transform.position = state.heldItemPosition;
                player.currentlyHeldObjectServer.transform.rotation = state.heldItemRotation;
            }
        }

        private void BeginCameraRendering(ScriptableRenderContext context, Camera renderedCamera)
        {
            if ((object)renderedCamera != camera)
                return;

            if (currentlyViewedMeshes.Length > 0 && currentlyViewedMeshes[0] == null)
                return;
            foreach (var mesh in currentlyViewedMeshes)
                mesh.forceRenderingOff = true;

            var localPlayer = StartOfRound.Instance.localPlayerController;

            SaveStateAndApplyPerspective(currentPlayer, ref currentPlayerMoreCompanyCosmetics, ref currentPlayerModelState, Perspective.FirstPerson);
            if ((object)currentPlayer != localPlayer)
                SaveStateAndApplyPerspective(localPlayer, ref localPlayerMoreCompanyCosmetics, ref localPlayerModelState, Perspective.ThirdPerson);
        }

        private void EndCameraRendering(ScriptableRenderContext context, Camera renderedCamera)
        {
            if ((object)renderedCamera != camera)
                return;

            if (currentlyViewedMeshes.Length > 0 && currentlyViewedMeshes[0] == null)
                return;
            foreach (var mesh in currentlyViewedMeshes)
                mesh.forceRenderingOff = false;

            var localPlayer = StartOfRound.Instance.localPlayerController;

            RestoreState(currentPlayer, currentPlayerMoreCompanyCosmetics, currentPlayerModelState);
            if ((object)currentPlayer != localPlayer)
                RestoreState(localPlayer, localPlayerMoreCompanyCosmetics, localPlayerModelState);
        }

        public void Update()
        {
            EnsureCameraExists();

            var spectatedPlayer = StartOfRound.Instance.localPlayerController;
            if (spectatedPlayer == null)
                return;
            if (spectatedPlayer.spectatedPlayerScript != null)
                spectatedPlayer = spectatedPlayer.spectatedPlayerScript;
            bool enable = monitorMesh.isVisible && spectatedPlayer.isInHangarShipRoom;

            if (!enable)
            {
                camera.enabled = false;
                return;
            }

            panAngle += Time.deltaTime * PAN_SPEED;
            if (panCamera)
                cameraObject.transform.localRotation = Quaternion.Euler(0, panAngle, 0);

            if (timePerFrame > 0)
            {
                elapsedSinceLastFrame += Time.deltaTime;
                if (elapsedSinceLastFrame >= timePerFrame)
                {
                    camera.Render();
                    elapsedSinceLastFrame = 0;
                }
            }
            else
            {
                camera.enabled = true;
            }
        }

        public void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= EndCameraRendering;
            Plugin.BodyCam = null;
        }
    }

    internal struct PlayerModelState
    {
        public ShadowCastingMode bodyShadowMode;
        public int bodyLayer;
        public bool armsEnabled;
        public int armsLayer;
        public int moreCompanyCosmeticsLayer;
        public Vector3 heldItemPosition;
        public Quaternion heldItemRotation;
    }

}
