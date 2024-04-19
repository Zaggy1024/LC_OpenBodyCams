using GameNetcodeStuff;
using HarmonyLib;

using OpenBodyCams.Compatibility;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal static class PatchPlayerControllerB
    {
        [HarmonyFinalizer]
        [HarmonyPatch(nameof(PlayerControllerB.ConnectClientToPlayerObject))]
        [HarmonyAfter(ModGUIDs.GeneralImprovements)]
        private static void ConnectClientToPlayerObjectFinalizer()
        {
            ShipObjects.LateInitialization();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerControllerB.KillPlayer))]
        private static void KillPlayerPostfix(PlayerControllerB __instance)
        {
            if (__instance.IsOwner)
                BodyCamComponent.MarkTargetStatusChangedForAllBodyCams(__instance.transform);
        }

        [HarmonyPostfix]
        [HarmonyPatch("KillPlayerClientRpc")]
        private static void KillPlayerClientRpcPostfix(PlayerControllerB __instance)
        {
            if (!__instance.IsOwner)
                BodyCamComponent.MarkTargetStatusChangedForAllBodyCams(__instance.transform);
        }
    }
}
