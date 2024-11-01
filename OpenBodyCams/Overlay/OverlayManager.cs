using System;

using TMPro;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

using OpenBodyCams.Utilities;

namespace OpenBodyCams.Overlay;

internal class OverlayManager : MonoBehaviour
{
    internal BodyCamComponent BodyCam;

    private Camera camera;

    private Canvas canvas;
    private TextMeshProUGUI textRenderer;

    private float baseFontSize;
    private Vector4 baseMargin;

    private bool renderThisFrame = false;

    private void Start()
    {
        camera = GetComponentInChildren<Camera>();

        var volume = camera.gameObject.AddComponent<CustomPassVolume>();
        volume.targetCamera = camera;

        var pass = (TransparentRenderTexturePass)volume.AddPassOfType<TransparentRenderTexturePass>();
        pass.targetColorBuffer = CustomPass.TargetBuffer.Custom;
        pass.targetDepthBuffer = CustomPass.TargetBuffer.Custom;
        pass.clearFlags = UnityEngine.Rendering.ClearFlag.All;
        pass.targetTexture = camera.targetTexture;
        camera.targetTexture = new RenderTexture(camera.targetTexture)
        {
            name = "Dummy Overlay Camera Output",
        };

        var hdCamera = camera.GetComponent<HDAdditionalCameraData>();
        hdCamera.customRenderingSettings = true;
        hdCamera.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.DecalLayers] = true;
        hdCamera.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.DecalLayers, false);

        canvas = GetComponentInChildren<Canvas>();

        textRenderer = canvas.GetComponentInChildren<TextMeshProUGUI>();
        textRenderer.font = StartOfRound.Instance.screenLevelDescription.font;
        baseFontSize = textRenderer.fontSize;
        baseMargin = textRenderer.margin;

        BodyCam.OnCameraStatusChanged += BodyCamStatusChanged;
        BodyCam.OnScreenPowerChanged += BodyCamScreenPowerChanged;
        API.BodyCam.OnBodyCamReceiverBecameEnabled += UpdateText;
        API.BodyCam.OnBodyCamReceiverBecameDisabled += UpdateText;

        CreateOverlayMesh();

        UpdatePreferences();
        UpdateText();
    }

    private void CreateOverlayMesh()
    {
        var overlayObject = Instantiate(Plugin.Assets.LoadAsset<GameObject>("Assets/OpenBodyCams/Prefabs/BodyCamOverlayMesh.prefab"));

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

    internal void UpdatePreferences()
    {
        textRenderer.fontSize = baseFontSize * Plugin.OverlayTextScale.Value;
        textRenderer.margin = baseMargin * Plugin.OverlayTextScale.Value;

        renderThisFrame = true;
    }

    private void BodyCamStatusChanged(CameraRenderingStatus _)
    {
        UpdateText();
    }

    private void BodyCamScreenPowerChanged(bool _)
    {
        UpdateText();
    }

    internal void UpdateText()
    {
        canvas.gameObject.SetActive(BodyCam.IsScreenPowered());

        textRenderer.text = GetText();
        textRenderer.enabled = textRenderer.text != "";

        renderThisFrame = true;
    }

    private string GetText()
    {
        if (ShipUpgrades.BodyCamUnlockable != null)
        {
            if (!ShipUpgrades.BodyCamUnlockable.hasBeenUnlockedByPlayer)
                return Plugin.BuyAntennaText.Value.Replace("{price}", ShipUpgrades.BodyCamPrice.ToString(), StringComparison.OrdinalIgnoreCase);

            if (!ShipUpgrades.BodyCamUnlockableIsPlaced)
                return Plugin.AntennaStoredText.Value;
        }

        return BodyCam.CameraStatus switch
        {
            CameraRenderingStatus.Rendering => Plugin.DefaultText.Value,
            CameraRenderingStatus.TargetInvalid => Plugin.TargetInvalidText.Value,
            CameraRenderingStatus.TargetDisabledOnShip => Plugin.TargetOnShipText.Value,
            _ => "",
        };
    }

    private void OnDestroy()
    {
        BodyCam.OnCameraStatusChanged -= BodyCamStatusChanged;
        BodyCam.OnScreenPowerChanged -= BodyCamScreenPowerChanged;
        API.BodyCam.OnBodyCamReceiverBecameEnabled -= UpdateText;
        API.BodyCam.OnBodyCamReceiverBecameDisabled -= UpdateText;
    }
}
