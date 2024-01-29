using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using OpenBodyCams.Compatibility;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal class PatchPlayerControllerB
    {
        // Run after the GeneralImprovements finalizer which has a Low priority.
        [HarmonyFinalizer]
        [HarmonyPatch(nameof(PlayerControllerB.ConnectClientToPlayerObject))]
        [HarmonyPriority(Priority.VeryLow)]
        static void ConnectClientToPlayerObjectFinalizer(PlayerControllerB __instance)
        {
            ShipObjects.LateInitialization();
        }

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
