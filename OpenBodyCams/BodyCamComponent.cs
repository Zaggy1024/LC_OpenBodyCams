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
        const int BODY_CAM_ONLY_LAYER = 31;
        const float PAN_SPEED = 40.0f;

        public static readonly Vector3 BODY_CAM_OFFSET = new Vector3(0.07f, 0, 0.15f);
        public static readonly Vector3 CAMERA_CONTAINER_OFFSET = new Vector3(0.07f, 0, 0.125f);

        public GameObject cameraObject;
        public Camera camera;

        private MeshRenderer monitorMesh;
        private Material monitorMaterial;

        private Renderer[] localPlayerMoreCompanyCosmetics = new Renderer[0];
        private PlayerModelState localPlayerModelState;

        private PlayerControllerB currentPlayer;
        private Renderer[] currentPlayerMoreCompanyCosmetics = new Renderer[0];
        private PlayerModelState currentPlayerModelState;

        private Transform currentActualTarget;
        private Renderer[] currentlyViewedMeshes;

        private float elapsedSinceLastFrame = 0;
        private float timePerFrame = 0;

        private bool panCamera = false;
        private float panAngle = 0;

        private Animator greenFlashAnimator;

        public void Start()
        {
            Plugin.BodyCam = this;

            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
            RenderPipelineManager.endCameraRendering += EndCameraRendering;

            monitorMesh = GetComponent<MeshRenderer>();
            monitorMaterial = monitorMesh.materials.First(material => material.mainTexture.name == "shipScreen2") ?? throw new Exception("Failed to get the ship screen material.");

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

            var greenFlashParent = new GameObject("CameraGreenTransitionScaler");
            greenFlashParent.transform.SetParent(cameraObject.transform, false);
            greenFlashParent.transform.localScale = new Vector3(1, 0.004f, 1);

            var greenFlashObject = Instantiate(StartOfRound.Instance.mapScreen.mapCameraAnimator.gameObject);
            greenFlashObject.transform.SetParent(greenFlashParent.transform, false);
            greenFlashObject.transform.localPosition = new Vector3(0, 0, 0.1f);
            greenFlashObject.layer = BODY_CAM_ONLY_LAYER;
            greenFlashAnimator = greenFlashObject.GetComponent<Animator>() ?? throw new Exception("Green flash object copied from the map screen has no Animator.");

            var aPlayerScript = StartOfRound.Instance.allPlayerScripts[0];

            var fogShaderPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            fogShaderPlane.transform.SetParent(cameraObject.transform, false);
            var fogShaderPlaneMesh = fogShaderPlane.GetComponent<MeshRenderer>();
            fogShaderPlaneMesh.sharedMaterial = aPlayerScript.localVisor.transform.Find("ScavengerHelmet/Plane").GetComponent<MeshRenderer>().sharedMaterial;
            fogShaderPlaneMesh.shadowCastingMode = ShadowCastingMode.Off;
            fogShaderPlaneMesh.receiveShadows = false;
            fogShaderPlane.transform.localPosition = new Vector3(0, 0, 0.5f);
            fogShaderPlane.transform.localRotation = Quaternion.Euler(270, 0, 0);
            fogShaderPlane.layer = BODY_CAM_ONLY_LAYER;
            Destroy(fogShaderPlane.GetComponent<MeshCollider>());

            var nightVision = Instantiate(aPlayerScript.nightVision.gameObject);
            nightVision.name = "NightVision";
            nightVision.transform.SetParent(cameraObject.transform, false);
            nightVision.transform.localPosition = Vector3.zero;
            var nightVisionLight = nightVision.GetComponent<Light>();
            nightVisionLight.enabled = true;
            nightVisionLight.cullingMask = 1 << BODY_CAM_ONLY_LAYER;

            UpdateSettings();
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

            UpdateCurrentTarget();
        }

        public void StartTargetTransition()
        {
            greenFlashAnimator.SetTrigger("Transition");
        }

        public void UpdateCurrentTarget()
        {
            EnsureCameraExists();

            var mapScreen = StartOfRound.Instance.mapScreen;

            // Ensure that we have a reference to null if the targeted player is being destroyed.
            currentPlayer = mapScreen.targetedPlayer;
            currentActualTarget = mapScreen.radarTargets[mapScreen.targetTransformIndex].transform;
            currentlyViewedMeshes = new Renderer[0];

            if (currentActualTarget == null)
                return;

            if (MoreCompanyCompatibilityPatch.f_CosmeticApplication_spawnedCosmetics is object)
            {
                Renderer[] CollectCosmetics(PlayerControllerB player, bool hidden)
                {
                    if (player != null && player.GetComponentInChildren(MoreCompanyCompatibilityPatch.t_CosmeticApplication) is Behaviour cosmeticApplication)
                    {
                        Plugin.Instance.Logger.LogInfo($"Getting MoreCompany cosmetic models for {player.playerUsername}");
                        var cosmeticsList = (IList)MoreCompanyCompatibilityPatch.f_CosmeticApplication_spawnedCosmetics.GetValue(cosmeticApplication);
                        var result = cosmeticsList.Cast<Component>().SelectMany(cosmetic => cosmetic.GetComponentsInChildren<Renderer>()).ToArray();
                        cosmeticApplication.enabled = true;
                        foreach (var cosmeticRenderer in result)
                            cosmeticRenderer.forceRenderingOff = hidden;
                        return result;
                    }

                    return new Renderer[0];
                }

                currentPlayerMoreCompanyCosmetics = CollectCosmetics(currentPlayer, hidden: false);
                localPlayerMoreCompanyCosmetics = CollectCosmetics(StartOfRound.Instance.localPlayerController, hidden: true);
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
            {
                cameraObject.transform.SetParent(null, false);
                cameraObject.transform.localPosition = Vector3.zero;
                cameraObject.transform.localRotation = Quaternion.identity;
                return;
            }

            cameraObject.transform.SetParent(currentActualTarget.transform, false);
            cameraObject.transform.localPosition = offset;
            cameraObject.transform.localRotation = Quaternion.identity;
        }

        private enum Perspective
        {
            FirstPerson,
            ThirdPerson,
        }

        private static void SaveStateAndApplyPerspective(PlayerControllerB player, Renderer[] moreCompanyCosmetics, ref PlayerModelState state, Perspective perspective)
        {
            if (player == null)
                return;

            // Save
            state.shadowMode = player.thisPlayerModel.shadowCastingMode;
            state.armsEnabled = player.thisPlayerModelArms.enabled;
            state.armsHidden = player.thisPlayerModelArms.forceRenderingOff;

            if (player.currentlyHeldObjectServer != null)
            {
                state.heldItemPosition = player.currentlyHeldObjectServer.transform.position;
                state.heldItemRotation = player.currentlyHeldObjectServer.transform.rotation;
            }

            state.moreCompanyCosmeticsHidden = moreCompanyCosmetics.Length > 0 ? moreCompanyCosmetics[0].forceRenderingOff : false;

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
                    player.thisPlayerModelArms.enabled = true;
                    player.thisPlayerModelArms.forceRenderingOff = false;

                    if (player.currentlyHeldObjectServer != null)
                        AttachItem(player.currentlyHeldObjectServer, player.localItemHolder);

                    foreach (var cosmetic in moreCompanyCosmetics)
                        cosmetic.forceRenderingOff = true;
                    break;
                case Perspective.ThirdPerson:
                    player.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On;
                    player.thisPlayerModelArms.enabled = false;
                    player.thisPlayerModelArms.forceRenderingOff = true;

                    if (player.currentlyHeldObjectServer != null)
                        AttachItem(player.currentlyHeldObjectServer, player.serverItemHolder);

                    foreach (var cosmetic in moreCompanyCosmetics)
                        cosmetic.forceRenderingOff = false;
                    break;
            }
        }

        private static void RestoreState(PlayerControllerB player, Renderer[] moreCompanyCosmetics, PlayerModelState state)
        {
            if (player == null)
                return;

            player.thisPlayerModel.shadowCastingMode = state.shadowMode;
            player.thisPlayerModelArms.enabled = state.armsEnabled;
            player.thisPlayerModelArms.forceRenderingOff = state.armsHidden;

            foreach (var cosmetic in moreCompanyCosmetics)
                cosmetic.forceRenderingOff = state.moreCompanyCosmeticsHidden;

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

            foreach (var mesh in currentlyViewedMeshes)
                mesh.forceRenderingOff = true;

            var localPlayer = StartOfRound.Instance.localPlayerController;
            if ((object)localPlayer == currentPlayer)
                return;

            SaveStateAndApplyPerspective(currentPlayer, currentPlayerMoreCompanyCosmetics, ref currentPlayerModelState, Perspective.FirstPerson);
            SaveStateAndApplyPerspective(localPlayer, localPlayerMoreCompanyCosmetics, ref localPlayerModelState, Perspective.ThirdPerson);
        }

        private void EndCameraRendering(ScriptableRenderContext context, Camera renderedCamera)
        {
            if ((object)renderedCamera != camera)
                return;

            foreach (var mesh in currentlyViewedMeshes)
                mesh.forceRenderingOff = false;

            var localPlayer = StartOfRound.Instance.localPlayerController;
            if ((object)localPlayer == currentPlayer)
                return;

            RestoreState(localPlayer, localPlayerMoreCompanyCosmetics, localPlayerModelState);
            RestoreState(currentPlayer, currentPlayerMoreCompanyCosmetics, currentPlayerModelState);
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
        public ShadowCastingMode shadowMode;
        public bool armsEnabled;
        public bool armsHidden;
        public bool moreCompanyCosmeticsHidden;
        public Vector3 heldItemPosition;
        public Quaternion heldItemRotation;
    }

}
