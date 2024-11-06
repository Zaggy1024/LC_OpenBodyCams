using HarmonyLib;

using OpenBodyCams.Components;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(DeadBodyInfo))]
internal static class PatchDeadBodyInfo
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(DeadBodyInfo.Start))]
    private static void StartPostfix(DeadBodyInfo __instance)
    {
        TargetTracker.AddTrackersToTarget(__instance.transform, __instance.playerScript?.transform);
    }
}
