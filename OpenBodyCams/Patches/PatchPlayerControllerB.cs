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
            if (__instance.IsOwner)
                Plugin.BodyCam.UpdateCurrentTarget();
        }

        [HarmonyPostfix]
        [HarmonyPatch("KillPlayerClientRpc")]
        static void KillPlayerClientRpcPostfix(PlayerControllerB __instance)
        {
            if (!__instance.IsOwner)
                Plugin.BodyCam.UpdateCurrentTarget();
        }
    }
}
