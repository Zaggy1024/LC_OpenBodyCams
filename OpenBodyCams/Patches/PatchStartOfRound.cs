using HarmonyLib;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    internal static class PatchStartOfRound
    {
        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        [HarmonyPriority(Priority.VeryHigh)]
        private static void StartPostfix()
        {
            ShipObjects.EarlyInitialization();
        }

        [HarmonyPostfix]
        [HarmonyPatch("ReviveDeadPlayers")]
        private static void ReviveDeadPlayersPostfix()
        {
            BodyCamComponent.MarkTargetStatusChangedForAllBodyCams();
        }
    }
}
