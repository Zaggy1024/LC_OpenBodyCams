using HarmonyLib;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(TimeOfDay))]
internal class PatchTimeOfDay
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(nameof(TimeOfDay.Start))]
    private static void StartPostfix()
    {
        BodyCamComponent.UpdateWeathers();
    }
}
