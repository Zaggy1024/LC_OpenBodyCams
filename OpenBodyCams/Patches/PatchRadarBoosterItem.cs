using HarmonyLib;

using OpenBodyCams.Components;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(RadarBoosterItem))]
internal static class PatchRadarBoosterItem
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(RadarBoosterItem.Start))]
    private static void StartPostfix(RadarBoosterItem __instance)
    {
        TargetTracker.AddTrackersToTarget(__instance.NetworkObject.transform, __instance.playerHeldBy?.currentAudioTrigger);
    }
}
