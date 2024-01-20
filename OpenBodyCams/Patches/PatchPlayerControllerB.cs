using GameNetcodeStuff;
using HarmonyLib;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal class PatchPlayerControllerB
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerControllerB.KillPlayer))]
        static void KillPlayerPostfix(PlayerControllerB __instance)
        {
            Plugin.BodyCam.UpdateCurrentTarget();
        }
    }
}
