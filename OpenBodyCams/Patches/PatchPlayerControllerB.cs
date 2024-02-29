using GameNetcodeStuff;
using HarmonyLib;

using OpenBodyCams.Compatibility;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal class PatchPlayerControllerB
    {
        [HarmonyFinalizer]
        [HarmonyPatch(nameof(PlayerControllerB.ConnectClientToPlayerObject))]
        [HarmonyAfter(ModGUIDs.GeneralImprovements)]
        static void ConnectClientToPlayerObjectFinalizer()
        {
            ShipObjects.LateInitialization();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerControllerB.KillPlayer))]
        static void KillPlayerPostfix(PlayerControllerB __instance)
        {
            if (__instance.IsOwner)
                BodyCamComponent.UpdateAllTargetStatuses();
        }

        [HarmonyPostfix]
        [HarmonyPatch("KillPlayerClientRpc")]
        static void KillPlayerClientRpcPostfix(PlayerControllerB __instance)
        {
            if (!__instance.IsOwner)
                BodyCamComponent.UpdateAllTargetStatuses();
        }
    }
}
