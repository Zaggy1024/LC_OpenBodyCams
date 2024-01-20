using System;
using System.Linq;

using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace OpenBodyCams
{
    public class BodyCamComponent : MonoBehaviour
    {
        const int BODY_CAM_ONLY_LAYER = 31;
        const float PAN_SPEED = 40.0f;

        public GameObject cameraObject;
        public Camera camera;

        private MeshRenderer monitorMesh;
        private Material monitorMaterial;

        private PlayerModelState localPlayerModelState;

        private PlayerControllerB currentPlayer;
        private PlayerModelState currentPlayerModelState;

        private float elapsedSinceLastFrame = 0;
        private float timePerFrame = 0;

        private bool panCamera = false;
        private float panAngle = 0;

        private Animator greenFlashAnimator;

        public void Start()
        {
            Plugin.BodyCam = this;

            cameraObject = new GameObject("BodyCam");
            camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.cullingMask = unchecked((int)0b1010_0001_0011_1011_0001_0111_0101_1011);
            var cameraData = cameraObject.AddComponent<HDAdditionalCameraData>();
            cameraData.volumeLayerMask = 1;

            monitorMesh = GetComponent<MeshRenderer>();
            monitorMaterial = monitorMesh.materials.First(material => material.mainTexture.name == "shipScreen2") ?? throw new Exception("Failed to get the ship screen material.");

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

            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
            RenderPipelineManager.endCameraRendering += EndCameraRendering;
        }

        public void UpdateSettings()
        {
            camera.targetTexture = new RenderTexture(Plugin.HorizontalResolution.Value, Plugin.HorizontalResolution.Value * 3 / 4, 32);
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
            var mapScreen = StartOfRound.Instance.mapScreen;

            currentPlayer = mapScreen.targetedPlayer;

            Transform attachToObject = mapScreen.radarTargets[mapScreen.targetTransformIndex].transform;
            string attachmentPoint = null;
            Vector3 offset = Vector3.zero;

            if (attachToObject.GetComponent<RadarBoosterItem>() is object)
            {
                offset = new Vector3(0, 1.5f, 0);
                panCamera = true;
            }
            else if (currentPlayer is object)
            {
                if (currentPlayer.isPlayerDead == true && currentPlayer.deadBody is object)
                {
                    if (Plugin.CameraMode.Value == CameraModeOptions.Head)
                        attachToObject = currentPlayer.deadBody.transform.Find("spine.001/spine.002/spine.003/spine.004");
                    else
                        attachToObject = currentPlayer.deadBody.transform.Find("spine.001/spine.002/spine.003");
                }
                else
                {
                    if (Plugin.CameraMode.Value == CameraModeOptions.Head)
                        attachToObject = currentPlayer.gameplayCamera.transform;
                    else
                        attachToObject = currentPlayer.playerGlobalHead.transform.parent;
                }
                if (Plugin.CameraMode.Value == CameraModeOptions.Head)
                    offset = new Vector3(0.07f, 0, 0.125f);
                else
                    offset = new Vector3(0.07f, 0.1f, 0.2f);
                panCamera = false;
            }

            if (attachToObject is null)
                return;

            var attachmentPointObject = attachToObject.transform;

            if (attachmentPoint is string)
                attachmentPointObject = attachmentPointObject.transform.Find(attachmentPoint);
            cameraObject.transform.SetParent(attachmentPointObject, false);
            cameraObject.transform.localPosition = offset;
            cameraObject.transform.localRotation = Quaternion.identity;
        }

        private enum Perspective
        {
            FirstPerson,
            ThirdPerson,
        }

        private static void SaveStateAndApplyPerspective(PlayerControllerB player, ref PlayerModelState state, Perspective perspective)
        {
            if (player is null)
                return;

            // Save
            state.shadowMode = player.thisPlayerModel.shadowCastingMode;
            state.armsHidden = player.thisPlayerModelArms.forceRenderingOff;

            if (player.currentlyHeldObjectServer is object)
            {
                state.heldItemPosition = player.currentlyHeldObjectServer.transform.position;
                state.heldItemRotation = player.currentlyHeldObjectServer.transform.rotation;
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
                    player.thisPlayerModelArms.forceRenderingOff = false;

                    if (player.currentlyHeldObjectServer is object)
                        AttachItem(player.currentlyHeldObjectServer, player.localItemHolder);
                    break;
                case Perspective.ThirdPerson:
                    player.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On;
                    player.thisPlayerModelArms.forceRenderingOff = true;

                    if (player.currentlyHeldObjectServer is object)
                        AttachItem(player.currentlyHeldObjectServer, player.serverItemHolder);
                    break;
            }
        }

        private static void RestoreState(PlayerControllerB player, PlayerModelState state)
        {
            if (player is null)
                return;

            player.thisPlayerModel.shadowCastingMode = state.shadowMode;
            player.thisPlayerModelArms.forceRenderingOff = state.armsHidden;

            if (player.currentlyHeldObjectServer is object)
            {
                player.currentlyHeldObjectServer.transform.position = state.heldItemPosition;
                player.currentlyHeldObjectServer.transform.rotation = state.heldItemRotation;
            }
        }

        private void BeginCameraRendering(ScriptableRenderContext context, Camera renderedCamera)
        {
            if ((object)renderedCamera != camera)
                return;

            var localPlayer = StartOfRound.Instance.localPlayerController;
            if ((object)localPlayer == currentPlayer)
                return;

            SaveStateAndApplyPerspective(currentPlayer, ref currentPlayerModelState, Perspective.FirstPerson);
            SaveStateAndApplyPerspective(localPlayer, ref localPlayerModelState, Perspective.ThirdPerson);
        }

        private void EndCameraRendering(ScriptableRenderContext context, Camera renderedCamera)
        {
            if ((object)renderedCamera != camera)
                return;

            var localPlayer = StartOfRound.Instance.localPlayerController;
            if ((object)localPlayer == currentPlayer)
                return;

            RestoreState(localPlayer, localPlayerModelState);
            RestoreState(currentPlayer currentPlayerModelState);
        }

        public void Update()
        {
            var spectatedPlayer = StartOfRound.Instance.localPlayerController;
            if (spectatedPlayer is null)
                return;
            if (spectatedPlayer.spectatedPlayerScript is object)
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
        public bool armsHidden;
        public Vector3 heldItemPosition;
        public Quaternion heldItemRotation;
    }

}
