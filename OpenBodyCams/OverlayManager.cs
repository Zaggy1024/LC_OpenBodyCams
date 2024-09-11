using TMPro;
using UnityEngine;

using OpenBodyCams.Utilities;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

namespace OpenBodyCams
{
    internal class OverlayManager : MonoBehaviour
    {
        private static readonly int ForegroundColorProperty = Shader.PropertyToID("_Color");

        internal BodyCamComponent BodyCam;

        private Camera camera;
        private Canvas canvas;
        private TextMeshProUGUI textRenderer;

        private Material overlayMaterial;

        private bool renderThisFrame = false;

        private void Start()
        {
            camera = GetComponentInChildren<Camera>();

            canvas = GetComponentInChildren<Canvas>();

            textRenderer = GetComponentInChildren<TextMeshProUGUI>();
            textRenderer.font = StartOfRound.Instance.screenLevelDescription.font;

            BodyCam.OnCameraStatusChanged += _ => UpdateText();
            API.BodyCam.OnBodyCamReceiverBecameEnabled += UpdateText;
            API.BodyCam.OnBodyCamReceiverBecameDisabled += UpdateText;

            CreateOverlayMesh();

            UpdateText();
        }

        private void CreateOverlayMesh()
        {
            var overlayObject = Instantiate(Plugin.Assets.LoadAsset<GameObject>("Assets/OpenBodyCams/Prefabs/BodyCamOverlayMesh.prefab"));

            overlayMaterial = overlayObject.GetComponent<Renderer>().sharedMaterial;

            var overlayTransform = overlayObject.transform;
            var copyTransform = BodyCam.MonitorRenderer.transform;
            overlayTransform.parent = copyTransform.parent;
            overlayTransform.localScale = copyTransform.localScale;
            overlayTransform.SetLocalPositionAndRotation(copyTransform.localPosition, copyTransform.localRotation);

            var overlayMeshFilter = overlayObject.GetComponent<MeshFilter>();
            overlayMeshFilter.mesh = MeshUtils.CopySubmesh(BodyCam.MonitorRenderer.GetComponent<MeshFilter>().mesh, BodyCam.MonitorMaterialIndex);
        }

        private void Update()
        {
            camera.enabled = renderThisFrame;
            renderThisFrame = false;
        }

        internal void UpdateText()
        {
            textRenderer.enabled = GetTextAndColor(out var text, out var color);
            textRenderer.text = text;
            overlayMaterial.SetColor(ForegroundColorProperty, color);

            renderThisFrame = true;
        }

        private bool GetTextAndColor(out string text, out Color color)
        {
            text = "";
            color = Color.clear;

            if (ShipUpgrades.BodyCamUnlockable != null)
            {
                if (!ShipUpgrades.BodyCamUnlockable.hasBeenUnlockedByPlayer)
                {
                    text = $"Body cam ${ShipUpgrades.BodyCamPrice}";
                    color = Color.yellow;
                    return true;
                }

                if (!ShipUpgrades.BodyCamUnlockableIsPlaced)
                {
                    text = "Antenna stored";
                    color = Color.yellow;
                    return true;
                }
            }

            switch (BodyCam.CameraStatus)
            {
                case CameraRenderingStatus.TargetInvalid:
                    text = "Signal lost";
                    color = Color.red;
                    return true;
                case CameraRenderingStatus.TargetDisabledOnShip:
                    text = "Target on ship";
                    color = Color.green;
                    return true;
            }

            return false;
        }
    }
}
