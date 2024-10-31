using GameNetcodeStuff;
using HarmonyLib;

using OpenBodyCams.Components;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(PlayerControllerB))]
internal static class PatchPlayerControllerB
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(PlayerControllerB.Start))]
    private static void StartPostfix(PlayerControllerB __instance)
    {
        ReverbTriggerTracker.AddTrackersToTarget(__instance.NetworkObject.transform);
    }

    [HarmonyFinalizer]
    [HarmonyPatch(nameof(PlayerControllerB.ConnectClientToPlayerObject))]
    [HarmonyPriority(Priority.VeryLow)]
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
    [HarmonyPatch(nameof(PlayerControllerB.KillPlayerClientRpc))]
    private static void KillPlayerClientRpcPostfix(PlayerControllerB __instance)
    {
        if (!__instance.IsOwner)
            BodyCamComponent.MarkTargetStatusChangedForAllBodyCams(__instance.transform);
    }
}
