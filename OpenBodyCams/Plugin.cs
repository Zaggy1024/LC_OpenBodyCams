using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

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
    [BepInDependency(ModGUIDs.LethalLib, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(ModGUIDs.LethalVRM, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(ModGUIDs.ModelReplacementAPI, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(ModGUIDs.MoreCompany, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string MOD_NAME = "OpenBodyCams";
        public const string MOD_UNIQUE_NAME = "Zaggy1024." + MOD_NAME;
        public const string MOD_VERSION = "2.3.0";

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
        public static ConfigEntry<bool> DisplayOriginalScreenWhenDisabled;

        public static ConfigEntry<bool> TerminalPiPBodyCamEnabled;
        public static ConfigEntry<PiPPosition> TerminalPiPPosition;
        public static ConfigEntry<int> TerminalPiPWidth;

        public static ConfigEntry<bool> ShipUpgradeEnabled;
        public static ConfigEntry<int> ShipUpgradePrice;

        public static ConfigEntry<int> GeneralImprovementsBetterMonitorIndex;
        public static ConfigEntry<bool> EnableMoreCompanyCosmeticsCompatibility;
        public static ConfigEntry<bool> EnableAdvancedCompanyCosmeticsCompatibility;
        public static ConfigEntry<bool> EnableModelReplacementAPICompatibility;
        public static ConfigEntry<bool> EnableLethalVRMCompatibility;

        public static ConfigEntry<bool> SwapInternalAndExternalShipCameras;
        public static ConfigEntry<bool> DisableCameraOnSmallMonitor;
        public static ConfigEntry<string> ExternalCameraEmissiveColor;

        public static ConfigEntry<bool> FixDroppedItemRotation;

        public static ConfigEntry<bool> PrintCosmeticsDebugInfo;
        public static ConfigEntry<bool> BruteForcePreventFreezes;
        public static ConfigEntry<bool> ReferencedObjectDestructionDetectionEnabled;
        public static ConfigEntry<string> LastConfigVersion;

        internal static bool DisplayBodyCamUpgradeTip = false;

        private static readonly Harmony DestructionDetectionPatch = new(MOD_UNIQUE_NAME + ".DestructionDetectionPatch");

        internal static AssetBundle Assets;

        const string OptionDisabledWithBetterMonitors = "This has no effect when GeneralImprovements's UseBetterMonitors option is enabled.";

        public new ManualLogSource Logger => base.Logger;

        void Awake()
        {
            Instance = this;

            var assetBundlePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "openbodycams");
            Assets = AssetBundle.LoadFromFile(assetBundlePath);

            if (Assets != null)
                Instance.Logger.LogInfo("Successfully loaded OpenBodyCams assets.");
            else
                Instance.Logger.LogError("Failed to load the asset bundle, some features may be missing.");

            harmony.PatchAll(typeof(PatchStartOfRound));
            harmony.PatchAll(typeof(PatchManualCameraRenderer));
            harmony.PatchAll(typeof(PatchPlayerControllerB));
            harmony.PatchAll(typeof(PatchHauntedMaskItem));
            harmony.PatchAll(typeof(PatchMaskedPlayerEnemy));
            harmony.PatchAll(typeof(PatchUnlockableSuit));
            harmony.PatchAll(typeof(PatchCentipedeAI));
            harmony.PatchAll(typeof(PatchFlowerSnakeEnemy));
            harmony.PatchAll(typeof(PatchCopyVanillaFlowerSnakeEnemyCode));

            // Camera:
            CameraMode = Config.Bind("Camera", "Mode", CameraModeOptions.Head, "Choose where to attach the camera. 'Head' will attach the camera to the right side of the head, 'Body' will attach it to the chest.");
            HorizontalResolution = Config.Bind("Camera", "HorizontalResolution", 160, "The horizontal resolution of the rendering. The vertical resolution is calculated based on the aspect ratio of the monitor.");
            FieldOfView = Config.Bind("Camera", "FieldOfView", 65f, "The vertical FOV of the camera in degrees.");
            RenderDistance = Config.Bind("Camera", "RenderDistance", 25f, "The far clip plane for the body cam. Lowering may improve framerates.");
            Framerate = Config.Bind("Camera", "Framerate", 0f, "The number of frames to render per second. A value of 0 will render at the game's framerate. Setting this significantly below the average framerate on the ship will improve performance.");
            NightVisionBrightness = Config.Bind("Camera", "NightVisionBrightness", 1f, "A multiplier for the intensity of the area light used to brighten dark areas. A value of 1 is identical to the player's actual vision.");
            MonitorEmissiveColor = Config.Bind("Camera", "MonitorEmissiveColor", "0.05, 0.13, 0.05", "Adjust the color that is emitted from the body cam monitor, using comma-separated decimal numbers for red, green and blue.");
            MonitorTextureFiltering = Config.Bind("Camera", "MonitorTextureFiltering", FilterMode.Bilinear, "The texture filtering to use for the body cam on the monitor. Bilinear and Trilinear will result in a smooth image, while Point will result in sharp square edges. If Point is used, a fairly high resolution is recommended.");
            RadarBoosterPanRPM = Config.Bind("Camera", "RadarBoosterPanRPM", 9f, "The rotations per minute to turn the camera when a radar booster is selected. If the value is set to 0, the radar booster camera will face in the direction player faces when it is placed.");
            UseTargetTransitionAnimation = Config.Bind("Camera", "UseTargetTransitionAnimation", true, "Enables a green flash animation on the body cam screen mirroring the one that the radar map shows when switching targets.");
            DisableCameraWhileTargetIsOnShip = Config.Bind("Camera", "DisableCameraWhileTargetIsOnShip", false, "With this option enabled, the camera will stop rendering when the target is onboard the ship to reduce the performance hit of rendering a large number of items on the ship twice.");
            EnableCamera = Config.Bind("Camera", "EnableCamera", true, "Enables/disables rendering of the body cam, and can be enabled/disabled during a game with LethalConfig.");
            DisplayOriginalScreenWhenDisabled = Config.Bind("Camera", "DisplayOriginalScreenWhenDisabled", true, $"When enabled, the screen that the body cam replaces will be displayed when it is disabled due to invalid targets.");

            CameraMode.SettingChanged += (_, _) => BodyCamComponent.MarkTargetStatusChangedForAllBodyCams();
            FieldOfView.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            RenderDistance.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            NightVisionBrightness.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            MonitorTextureFiltering.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            RadarBoosterPanRPM.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            DisableCameraWhileTargetIsOnShip.SettingChanged += (_, _) => BodyCamComponent.UpdateAllCameraSettings();
            DisplayOriginalScreenWhenDisabled.SettingChanged += (_, _) => ShipObjects.UpdateMainBodyCamNoTargetMaterial();

            HorizontalResolution.SettingChanged += (_, _) => ShipObjects.UpdateMainBodyCamSettings();
            Framerate.SettingChanged += (_, _) => ShipObjects.UpdateMainBodyCamSettings();
            MonitorEmissiveColor.SettingChanged += (_, _) => ShipObjects.UpdateMainBodyCamSettings();
            EnableCamera.SettingChanged += (_, _) => ShipObjects.UpdateMainBodyCamSettings();

            // Terminal:
            TerminalPiPBodyCamEnabled = Config.Bind("Terminal", "EnablePiPBodyCam", false, "Adds a 'view bodycam' command to the terminal that places a picture-in-picture view of the bodycam in front of the radar map.");
            TerminalPiPPosition = Config.Bind("Terminal", "PiPPosition", PiPPosition.BottomRight, "The corner inside the terminal's radar map to align the body cam to.");
            TerminalPiPWidth = Config.Bind("Terminal", "PiPWidth", 150, "The width of the picture-in-picture in pixels.");

            TerminalPiPBodyCamEnabled.SettingChanged += (_, _) => TerminalCommands.Initialize();
            TerminalPiPPosition.SettingChanged += (_, _) => TerminalCommands.Initialize();
            TerminalPiPWidth.SettingChanged += (_, _) => TerminalCommands.Initialize();

            harmony.PatchAll(typeof(TerminalCommands));

            // Upgrades:
            ShipUpgradeEnabled = Config.Bind("ShipUpgrade", "Enabled", true, "Adds a ship upgrade that enables the body cam on the main monitors only after it is bought.\n\nNOTE: The upgrade will only be available if LethalLib is installed. Without it, the main body cam will always be enabled.");
            ShipUpgradePrice = Config.Bind("ShipUpgrade", "Price", 200, "The price at which the ship upgrade is sold in the store.");

            // Compatibility:
            GeneralImprovementsBetterMonitorIndex = Config.Bind("Compatibility", "GeneralImprovementsBetterMonitorIndex", 0,
                new ConfigDescription(
                    "With GeneralImprovements's UseBetterMonitors enabled, choose which monitor to display the body cam on.\n" +
                    "A value of 0 will place it on the large monitor on the right.\n" +
                    "Values greater than 0 go left to right, top to bottom, skipping the large center monitor. Without AddMoreBetterMonitors, the maximum value is 9, rather than 14.", new AcceptableValueRange<int>(0, 14)));
            EnableMoreCompanyCosmeticsCompatibility = Config.Bind("Compatibility", "EnableMoreCompanyCosmeticsCompatibility", true, "If this is enabled, a patch will be applied to MoreCompany to spawn cosmetics for the local player, and all cosmetics will be shown and hidden based on the camera's perspective.");
            EnableAdvancedCompanyCosmeticsCompatibility = Config.Bind("Compatibility", "EnableAdvancedCompanyCosmeticsCompatibility", true, "When this is enabled and AdvancedCompany is installed, all cosmetics will be shown and hidden based on the camera's perspective.");
            EnableModelReplacementAPICompatibility = Config.Bind("Compatibility", "EnableModelReplacementAPICompatibility", true, "When enabled, this will get the third person model replacement and hide/show it based on the camera's perspective.");
            EnableLethalVRMCompatibility = Config.Bind("Compatibility", "EnableLethalVRMCompatibility", true, "When enabled, any VRM model will be hidden/shown based on the camera's perspective.");

            // Ship:
            SwapInternalAndExternalShipCameras = Config.Bind("Ship", "SwapInternalAndExternalShipCameras", false, $"Causes the internal ship camera to be placed onto big monitor, and the external one to be placed onto the small monitor. {OptionDisabledWithBetterMonitors}");
            DisableCameraOnSmallMonitor = Config.Bind("Ship", "DisableCameraOnSmallMonitor", false, $"Disables whichever camera is placed onto the small camera monitor. {OptionDisabledWithBetterMonitors}");
            ExternalCameraEmissiveColor = Config.Bind("Ship", "ExternalCameraEmissiveColor", "", "Sets the color emitted by the external camera screen, using comma-separated decimal numbers for red, green and blue.");

            ExternalCameraEmissiveColor.SettingChanged += (_, _) => ShipObjects.SetExternalCameraEmissiveColor();

            // Misc:
            FixDroppedItemRotation = Config.Bind("Misc", "FixDroppedItemRotation", true, "If enabled, the mod will patch a bug that causes the rotation of dropped items to be desynced between clients.");

            // Debug:
            PrintCosmeticsDebugInfo = Config.Bind("Debug", "PrintCosmeticsDebugInfo", false, "Prints extra information about the cosmetics being collected for each player, as well as the code that is causing the collection.");
            BruteForcePreventFreezes = Config.Bind("Debug", "BruteForcePreventFreezes", false, "Enable a brute force approach to preventing errors in the camera setup callback that can cause the screen to freeze.");
            ReferencedObjectDestructionDetectionEnabled = Config.Bind("Debug", "ModelDestructionDebuggingPatchEnabled", false, "Enable this option when reproducing a camera freeze. This will cause a debug message to be printed when a model that a body cam is tracking is destroyed.");
            LastConfigVersion = Config.Bind("Debug", "LastConfigVersion", "", "The last version of the mod that loaded/saved this config file. Used for setting migration.");

            PrintCosmeticsDebugInfo.SettingChanged += (_, _) => Cosmetics.PrintDebugInfo = PrintCosmeticsDebugInfo.Value;
            Cosmetics.PrintDebugInfo = PrintCosmeticsDebugInfo.Value;
            BruteForcePreventFreezes.SettingChanged += (_, _) => BodyCamComponent.UpdateStaticSettings();
            ReferencedObjectDestructionDetectionEnabled.SettingChanged += (_, _) => UpdateReferencedObjectDestructionDetectionEnabled();
            UpdateReferencedObjectDestructionDetectionEnabled();

            MigrateSettings();

            Cosmetics.Initialize(harmony);

            harmony.PatchAll(typeof(PatchFixItemDropping));

            BodyCamComponent.InitializeStatic();

            ShipUpgrades.Initialize();
        }

        private void UpdateReferencedObjectDestructionDetectionEnabled()
        {
            if (ReferencedObjectDestructionDetectionEnabled.Value)
                DestructionDetectionPatch.PatchAll(typeof(PatchModelDestructionDebugging));
            else
                DestructionDetectionPatch.UnpatchSelf();
        }

        private void MigrateSettings()
        {
            if (AccessTools.PropertyGetter(typeof(ConfigFile), "OrphanedEntries") is not MethodInfo orphansFieldGetter)
            {
                Logger.LogError("Failed to migrate config, orphaned entries property was not found.");
                return;
            }
            if (orphansFieldGetter.Invoke(Config, []) is not Dictionary<ConfigDefinition, string> orphans)
            {
                Logger.LogError("Failed to migrate config, orphaned entries was not of the expected type.");
                return;
            }

            if (!Version.TryParse(LastConfigVersion.Value, out var lastVersion))
                lastVersion = new Version(2, 0, 0);

            Logger.LogInfo($"Last config version is {lastVersion}.");

            var disableInternalShipCameraDefinition = new ConfigDefinition("Misc", "DisableInternalShipCamera");

            if (orphans.TryGetValue(disableInternalShipCameraDefinition, out var disableInternalShipCameraValue))
            {
                Logger.LogInfo($"{disableInternalShipCameraDefinition} option was found set to '{disableInternalShipCameraValue}' in the config, migrating it over to {DisableCameraOnSmallMonitor.Definition}.");
                orphans.Remove(disableInternalShipCameraDefinition);
                DisableCameraOnSmallMonitor.Value = TomlTypeConverter.ConvertToValue<bool>(disableInternalShipCameraValue);
            }

            if (lastVersion < new Version(2, 0, 2) && !ShipUpgradeEnabled.Value)
            {
                Logger.LogInfo($"{ShipUpgradeEnabled.Definition} was set to its 2.0.0 default value 'false', resetting it to 'true'.");
                ShipUpgradeEnabled.Value = true;
            }

            if (lastVersion < new Version(2, 0, 4))
                DisplayBodyCamUpgradeTip = true;

            LastConfigVersion.Value = MOD_VERSION;
        }

        private static Color ParseColor(string str)
        {
            var components = str
                .Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => float.Parse(x.Trim(), CultureInfo.InvariantCulture))
                .ToArray();
            if (components.Length < 3)
                throw new FormatException("Not enough color components");
            if (components.Length > 4)
                throw new FormatException("Too many color components");
            return new Color(components[0], components[1], components[2], components.Length == 4 ? components[3] : 0);
        }

        internal static Color GetBodyCamEmissiveColor()
        {
            try
            {
                return ParseColor(MonitorEmissiveColor.Value);
            }
            catch (Exception e)
            {
                Instance.Logger.LogWarning($"Failed to parse the body cam screen's emissive color: {e}");
                return ParseColor((string)MonitorEmissiveColor.DefaultValue);
            }
        }

        internal static Color? GetExternalCameraEmissiveColor()
        {
            var colorAsString = ExternalCameraEmissiveColor.Value;
            if (colorAsString == "")
                return null;
            try
            {
                return ParseColor(ExternalCameraEmissiveColor.Value);
            }
            catch (Exception e)
            {
                Instance.Logger.LogWarning($"Failed to parse the external camera screen's emissive color: {e}");
                return null;
            }
        }
    }
}
