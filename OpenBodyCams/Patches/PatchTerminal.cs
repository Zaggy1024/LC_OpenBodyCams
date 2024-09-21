using HarmonyLib;

namespace OpenBodyCams.Patches;

internal static class PatchTerminal
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.Start))]
    private static void StartPostfix()
    {
        ShipObjects.EarlyInitialization();
    }
}
