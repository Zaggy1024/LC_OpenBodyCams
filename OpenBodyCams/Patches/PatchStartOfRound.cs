using HarmonyLib;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    internal class PatchStartOfRound
    {
        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        [HarmonyPriority(Priority.VeryHigh)]
        static void StartPostfix()
        {
            ShipObjects.EarlyInitialization();
        }

        [HarmonyPostfix]
        [HarmonyPatch("ReviveDeadPlayers")]
        static void ReviveDeadPlayersPostfix()
        {
            BodyCamComponent.MarkTargetStatusChangedForAllBodyCams();
        }
    }
}
