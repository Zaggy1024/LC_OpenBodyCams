using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

using OpenBodyCams.Patches;

namespace OpenBodyCams
{
    public enum CameraModeOptions
    {
        Body,
        Head,
    }

    [BepInPlugin(MOD_UNIQUE_NAME, MOD_NAME, MOD_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string MOD_NAME = "OpenBodyCams";
        public const string MOD_UNIQUE_NAME = "Zaggy1024." + MOD_NAME;
        public const string MOD_VERSION = "1.0.18";

        private readonly Harmony harmony = new Harmony(MOD_UNIQUE_NAME);

        public static Plugin Instance { get; private set; }

        public static ConfigEntry<CameraModeOptions> CameraMode;
        public static ConfigEntry<int> HorizontalResolution;
        public static ConfigEntry<float> FieldOfView;
        public static ConfigEntry<float> RenderDistance;
        public static ConfigEntry<float> Framerate;
        public const float NightVisionIntensityBase = 367;
        public const float NightVisionRangeBase = 12;
        public static ConfigEntry<float> NightVisionBrightness;
        public static ConfigEntry<string> MonitorEmissiveColor;
        public static ConfigEntry<bool> EnableCamera;

        public static ConfigEntry<bool> EnableMoreCompanyCosmeticsCompatibility;
        public static ConfigEntry<bool> EnableAdvancedCompanyCosmeticsCompatibility;
        public static ConfigEntry<int> GeneralImprovementsBetterMonitorIndex;

        public static ConfigEntry<bool> DisableInternalShipCamera;

        public new ManualLogSource Logger => base.Logger;

        public static BodyCamComponent BodyCam;

        public static Terminal TerminalScript;
        public static bool TwoRadarCamsPresent = false;

        void Awake()
        {
            Instance = this;

            harmony.PatchAll(typeof(PatchStartOfRound));
            harmony.PatchAll(typeof(PatchManualCameraRenderer));
            harmony.PatchAll(typeof(PatchPlayerControllerB));
            harmony.PatchAll(typeof(PatchHauntedMaskItem));
            harmony.PatchAll(typeof(PatchMaskedPlayerEnemy));

            CameraMode = Config.Bind("Camera", "Mode", CameraModeOptions.Head, "Choose where to attach the camera. 'Head' will attach the camera to the right side of the head, 'Body' will attach it to the chest.");
            HorizontalResolution = Config.Bind("Camera", "HorizontalResolution", 160, "The horizontal resolution of the rendering. The vertical resolution is calculated based on the aspect ratio of the monitor.");
            FieldOfView = Config.Bind("Camera", "FieldOfView", 65f, "The vertical FOV of the camera in degrees.");
            RenderDistance = Config.Bind("Camera", "RenderDistance", 25f, "The far clip plane for the body cam. Lowering may improve framerates.");
            Framerate = Config.Bind("Camera", "Framerate", 0f, "The number of frames to render per second. A value of 0 will render at the game's framerate and results in best performance. Higher framerates will negatively affect performance, values between 0 and 30 are recommended.");
            NightVisionBrightness = Config.Bind("Camera", "NightVisionBrightness", 1f, "A multiplier for the intensity of the area light used to brighten dark areas. A value of 1 is identical to the player's actual vision.");
            MonitorEmissiveColor = Config.Bind("Camera", "MonitorEmissiveColor", "0.05, 0.13, 0.05", "Adjust the color that is emitted from the body cam monitor.");
            EnableCamera = Config.Bind("Camera", "EnableCamera", true, "Disables rendering of the body cam, and can be enabled/disabled during a game with LethalConfig.");

            CameraMode.SettingChanged += (s, e) => BodyCam.UpdateSettings();
            HorizontalResolution.SettingChanged += (s, e) => BodyCam.UpdateSettings();
            FieldOfView.SettingChanged += (s, e) => BodyCam.UpdateSettings();
            RenderDistance.SettingChanged += (s, e) => BodyCam.UpdateSettings();
            Framerate.SettingChanged += (s, e) => BodyCam.UpdateSettings();
            NightVisionBrightness.SettingChanged += (s, e) => BodyCam.UpdateSettings();
            MonitorEmissiveColor.SettingChanged += (s, e) => BodyCam.UpdateSettings();
            EnableCamera.SettingChanged += (s, e) => BodyCam.UpdateSettings();

            EnableMoreCompanyCosmeticsCompatibility = Config.Bind("Compatibility", "EnableMoreCompanyCosmeticsCompatibility", true, "If this is enabled, a patch will be applied to MoreCompany to spawn cosmetics for the local player, and all cosmetics will be shown and hidden based on the camera's perspective.");
            EnableAdvancedCompanyCosmeticsCompatibility = Config.Bind("Compatibility", "EnableAdvancedCompanyCosmeticsCompatibility", true, "When this is enabled and AdvancedCompany is installed, all cosmetics will be shown and hidden based on the camera's perspective.");
            GeneralImprovementsBetterMonitorIndex = Config.Bind("Compatibility", "GeneralImprovementsBetterMonitorIndex", 0, new ConfigDescription("Choose which of GeneralImprovements' extended monitor set to display the body cam on. A value of 0 will place it on the large monitor on the right, 1-14 goes left to right, top to bottom, skipping the large center monitor.", new AcceptableValueRange<int>(0, 14)));

            DisableInternalShipCamera = Config.Bind("Misc", "DisableInternalShipCamera", false, "Whether to disable the internal ship camera displayed above the bodycam monitor.");

            CosmeticsCompatibility.Initialize(harmony);
        }

        public static void OnLocalPlayerConnected()
        {
            TerminalScript = FindObjectOfType<Terminal>();
            TwoRadarCamsPresent = TerminalScript.GetComponent<ManualCameraRenderer>() != null;
        }
    }
}
