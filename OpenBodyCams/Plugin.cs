using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

using OpenBodyCams.Compatibility;
using OpenBodyCams.Patches;
using OpenBodyCams.Utilities;

namespace OpenBodyCams
{
    public enum CameraModeOptions
    {
        Body,
        Head,
    }

    [BepInPlugin(MOD_UNIQUE_NAME, MOD_NAME, MOD_VERSION)]
    [BepInDependency(ModGUIDs.AdvancedCompany, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(ModGUIDs.MoreCompany, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(ModGUIDs.ModelReplacementAPI, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(ModGUIDs.LethalVRM, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string MOD_NAME = "OpenBodyCams";
        public const string MOD_UNIQUE_NAME = "Zaggy1024." + MOD_NAME;
        public const string MOD_VERSION = "1.3.0";

        private readonly Harmony harmony = new(MOD_UNIQUE_NAME);

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
        public static ConfigEntry<FilterMode> MonitorTextureFiltering;
        public static ConfigEntry<float> RadarBoosterPanRPM;
        public static ConfigEntry<bool> UseTargetTransitionAnimation;
        public static ConfigEntry<bool> DisableCameraWhileTargetIsOnShip;
        public static ConfigEntry<bool> EnableCamera;

        public static ConfigEntry<bool> TerminalPiPBodyCamEnabled;
        public static ConfigEntry<PiPPosition> TerminalPiPPosition;
        public static ConfigEntry<int> TerminalPiPWidth;

        public static ConfigEntry<int> GeneralImprovementsBetterMonitorIndex;
        public static ConfigEntry<bool> EnableMoreCompanyCosmeticsCompatibility;
        public static ConfigEntry<bool> EnableAdvancedCompanyCosmeticsCompatibility;
        public static ConfigEntry<bool> EnableModelReplacementAPICompatibility;
        public static ConfigEntry<bool> EnableLethalVRMCompatibility;

        public static ConfigEntry<bool> DisableInternalShipCamera;
        public static ConfigEntry<bool> FixDroppedItemRotation;

        public static ConfigEntry<bool> PrintCosmeticsDebugInfo;
        public static ConfigEntry<bool> BruteForcePreventFreezes;
        public static ConfigEntry<bool> ReferencedObjectDestructionDetectionEnabled;

        private static readonly Harmony DestructionDetectionPatch = new(MOD_UNIQUE_NAME + ".DestructionDetectionPatch");

        public new ManualLogSource Logger => base.Logger;

        void Awake()
        {
            Instance = this;

            harmony.PatchAll(typeof(PatchStartOfRound));
            harmony.PatchAll(typeof(PatchManualCameraRenderer));
            harmony.PatchAll(typeof(PatchPlayerControllerB));
            harmony.PatchAll(typeof(PatchHauntedMaskItem));
            harmony.PatchAll(typeof(PatchMaskedPlayerEnemy));
            harmony.PatchAll(typeof(PatchUnlockableSuit));

            // Camera:
            CameraMode = Config.Bind("Camera", "Mode", CameraModeOptions.Head, "Choose where to attach the camera. 'Head' will attach the camera to the right side of the head, 'Body' will attach it to the chest.");
            HorizontalResolution = Config.Bind("Camera", "HorizontalResolution", 160, "The horizontal resolution of the rendering. The vertical resolution is calculated based on the aspect ratio of the monitor.");
            FieldOfView = Config.Bind("Camera", "FieldOfView", 65f, "The vertical FOV of the camera in degrees.");
            RenderDistance = Config.Bind("Camera", "RenderDistance", 25f, "The far clip plane for the body cam. Lowering may improve framerates.");
            Framerate = Config.Bind("Camera", "Framerate", 0f, "The number of frames to render per second. A value of 0 will render at the game's framerate and results in best performance. Higher framerates will negatively affect performance, values between 0 and 30 are recommended.");
            NightVisionBrightness = Config.Bind("Camera", "NightVisionBrightness", 1f, "A multiplier for the intensity of the area light used to brighten dark areas. A value of 1 is identical to the player's actual vision.");
            MonitorEmissiveColor = Config.Bind("Camera", "MonitorEmissiveColor", "0.05, 0.13, 0.05", "Adjust the color that is emitted from the body cam monitor.");
            MonitorTextureFiltering = Config.Bind("Camera", "MonitorTextureFiltering", FilterMode.Bilinear, "The texture filtering to use for the body cam on the monitor. Bilinear and Trilinear will result in a smooth image, while Point will result in sharp square edges. If Point is used, a fairly high resolution is recommended.");
            RadarBoosterPanRPM = Config.Bind("Camera", "RadarBoosterPanRPM", 9f, "The rotations per minute to turn the camera when a radar booster is selected. If the value is set to 0, the radar booster camera will face in the direction player faces when it is placed.");
            UseTargetTransitionAnimation = Config.Bind("Camera", "UseTargetTransitionAnimation", true, "Enables a green flash animation on the body cam screen mirroring the one that the radar map shows when switching targets.");
            DisableCameraWhileTargetIsOnShip = Config.Bind("Camera", "DisableCameraWhileTargetIsOnShip", false, "With this option enabled, the camera will stop rendering when the target is onboard the ship to reduce the performance hit of rendering a large number of items on the ship twice.");
            EnableCamera = Config.Bind("Camera", "EnableCamera", true, "Enables/disables rendering of the body cam, and can be enabled/disabled during a game with LethalConfig.");

            CameraMode.SettingChanged += (_, _) => BodyCamComponent.MarkTargetStatusChangedForAllBodyCams();
            HorizontalResolution.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            FieldOfView.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            RenderDistance.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            Framerate.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            NightVisionBrightness.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            MonitorEmissiveColor.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            MonitorTextureFiltering.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            RadarBoosterPanRPM.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            DisableCameraWhileTargetIsOnShip.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            EnableCamera.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();

            // Terminal:
            TerminalPiPBodyCamEnabled = Config.Bind("Terminal", "EnablePiPBodyCam", false, "Adds a 'view bodycam' command to the terminal that places a picture-in-picture view of the bodycam in front of the radar map.");
            TerminalPiPPosition = Config.Bind("Terminal", "PiPPosition", PiPPosition.BottomRight, "The corner inside the terminal's radar map to align the body cam to.");
            TerminalPiPWidth = Config.Bind("Terminal", "PiPWidth", 150, "The width of the picture-in-picture in pixels.");

            TerminalPiPBodyCamEnabled.SettingChanged += (_, _) => TerminalCommands.Initialize();
            TerminalPiPPosition.SettingChanged += (_, _) => TerminalCommands.Initialize();
            TerminalPiPWidth.SettingChanged += (_, _) => TerminalCommands.Initialize();

            harmony.PatchAll(typeof(TerminalCommands));

            // Compatibility:
            GeneralImprovementsBetterMonitorIndex = Config.Bind("Compatibility", "GeneralImprovementsBetterMonitorIndex", 0, new ConfigDescription("Choose which of GeneralImprovements' extended monitor set to display the body cam on. A value of 0 will place it on the large monitor on the right, 1-14 goes left to right, top to bottom, skipping the large center monitor.", new AcceptableValueRange<int>(0, 14)));
            EnableMoreCompanyCosmeticsCompatibility = Config.Bind("Compatibility", "EnableMoreCompanyCosmeticsCompatibility", true, "If this is enabled, a patch will be applied to MoreCompany to spawn cosmetics for the local player, and all cosmetics will be shown and hidden based on the camera's perspective.");
            EnableAdvancedCompanyCosmeticsCompatibility = Config.Bind("Compatibility", "EnableAdvancedCompanyCosmeticsCompatibility", true, "When this is enabled and AdvancedCompany is installed, all cosmetics will be shown and hidden based on the camera's perspective.");
            EnableModelReplacementAPICompatibility = Config.Bind("Compatibility", "EnableModelReplacementAPICompatibility", true, "When enabled, this will get the third person model replacement and hide/show it based on the camera's perspective.");
            EnableLethalVRMCompatibility = Config.Bind("Compatibility", "EnableLethalVRMCompatibility", true, "When enabled, any VRM model will be hidden/shown based on the camera's perspective.");

            // Misc:
            DisableInternalShipCamera = Config.Bind("Misc", "DisableInternalShipCamera", false, "Whether to disable the internal ship camera displayed above the bodycam monitor.");
            FixDroppedItemRotation = Config.Bind("Misc", "FixDroppedItemRotation", true, "If enabled, the mod will patch a bug that causes the rotation of dropped items to be desynced between clients.");

            // Debug:
            PrintCosmeticsDebugInfo = Config.Bind("Debug", "PrintCosmeticsDebugInfo", false, "Prints extra information about the cosmetics being collected for each player, as well as the code that is causing the collection.");
            BruteForcePreventFreezes = Config.Bind("Debug", "BruteForcePreventFreezes", false, "Enable a brute force approach to preventing errors in the camera setup callback that can cause the screen to freeze.");
            ReferencedObjectDestructionDetectionEnabled = Config.Bind("Debug", "ModelDestructionDebuggingPatchEnabled", false, "Enable this option when reproducing a camera freeze. This will cause a debug message to be printed when a model that a body cam is tracking is destroyed.");

            PrintCosmeticsDebugInfo.SettingChanged += (_, _) => Cosmetics.PrintDebugInfo = PrintCosmeticsDebugInfo.Value;
            Cosmetics.PrintDebugInfo = PrintCosmeticsDebugInfo.Value;
            BruteForcePreventFreezes.SettingChanged += (_, _) => BodyCamComponent.UpdateStaticSettings();
            ReferencedObjectDestructionDetectionEnabled.SettingChanged += (_, _) => UpdateReferencedObjectDestructionDetectionEnabled();
            UpdateReferencedObjectDestructionDetectionEnabled();

            Cosmetics.Initialize(harmony);

            harmony.PatchAll(typeof(PatchFixItemDropping));

            BodyCamComponent.InitializeStatic();
        }

        private void UpdateReferencedObjectDestructionDetectionEnabled()
        {
            if (ReferencedObjectDestructionDetectionEnabled.Value)
                DestructionDetectionPatch.PatchAll(typeof(PatchModelDestructionDebugging));
            else
                DestructionDetectionPatch.UnpatchSelf();
        }
    }
}
