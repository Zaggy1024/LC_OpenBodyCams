using HarmonyLib;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(StartOfRound))]
internal static class PatchStartOfRound
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(StartOfRound.Start))]
    private static void StartPostfix()
    {
        ShipObjects.EarlyInitialization();
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(StartOfRound.ReviveDeadPlayers))]
    private static void ReviveDeadPlayersPostfix()
    {
        BodyCamComponent.MarkTargetStatusChangedForAllBodyCams();
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(StartOfRound.SyncShipUnlockablesClientRpc))]
    private static void SyncShipUnlockablesClientRpcPostfix()
    {
        ShipObjects.Overlay?.UpdateText();
    }
}
