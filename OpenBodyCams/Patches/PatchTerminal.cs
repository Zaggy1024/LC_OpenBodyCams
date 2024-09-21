using HarmonyLib;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(Terminal))]
internal static class PatchTerminal
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(Terminal.Awake))]
    private static void AwakePostfix(Terminal __instance)
    {
        ShipObjects.TerminalScript = __instance;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Terminal.Start))]
    private static void StartPostfix()
    {
        ShipObjects.EarlyInitialization();
    }
}
