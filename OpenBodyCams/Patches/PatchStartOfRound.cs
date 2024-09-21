using HarmonyLib;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(StartOfRound))]
internal static class PatchStartOfRound
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(StartOfRound.ReviveDeadPlayers))]
    private static void ReviveDeadPlayersPostfix()
    {
        BodyCamComponent.MarkTargetStatusChangedForAllBodyCams();
    }
}
