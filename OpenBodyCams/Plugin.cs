using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

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
        public const string MOD_VERSION = "0.0.1";

        private readonly Harmony harmony = new Harmony(MOD_UNIQUE_NAME);

        public static Plugin Instance { get; private set; }

        public static ConfigEntry<CameraModeOptions> CameraMode;
        public static ConfigEntry<int> HorizontalResolution;
        public static ConfigEntry<float> RenderDistance;
        public static ConfigEntry<float> Framerate;

        public static ConfigEntry<bool> DisableInternalShipCamera;

        public new ManualLogSource Logger => base.Logger;

        public static BodyCamComponent BodyCam;

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
            RenderDistance = Config.Bind("Camera", "RenderDistance", 25f, "The far clip plane for the body cam. Lowering may improve framerates.");
            Framerate = Config.Bind("Camera", "Framerate", 0f, "The number of frames to render per second. Higher framerates will negatively affect performance. A value of 0 will render at the game's framerate and results in best performance.");

            CameraMode.SettingChanged += (s, e) => BodyCam.UpdateSettings();
            HorizontalResolution.SettingChanged += (s, e) => BodyCam.UpdateSettings();
            RenderDistance.SettingChanged += (s, e) => BodyCam.UpdateSettings();
            Framerate.SettingChanged += (s, e) => BodyCam.UpdateSettings();

            DisableInternalShipCamera = Config.Bind("Misc", "DisableInternalShipCamera", false, "Whether to disable the internal ship camera displayed above the bodycam monitor.");
        }
    }
}
