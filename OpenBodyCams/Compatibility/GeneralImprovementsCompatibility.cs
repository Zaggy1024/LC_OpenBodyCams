using System.Runtime.CompilerServices;

using BepInEx.Bootstrap;
using GeneralImprovements.API;
using UnityEngine;

namespace OpenBodyCams.Compatibility;

internal static class GeneralImprovementsCompatibility
{
    internal static bool GeneralImprovementsEnabled => Chainloader.PluginInfos.ContainsKey(ModGUIDs.GeneralImprovements);
    internal static bool BetterMonitorsEnabled
    {
        get
        {
            if (GeneralImprovementsEnabled)
            {
                if (BetterMonitorsEnabledWithAPI())
                    return true;
            }

            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool BetterMonitorsEnabledWithAPI()
    {
        // Versions up to and including 1.4.3 don't set NewMonitorMeshActive.
        return MonitorsAPI.GetMonitorAtIndex(0)?.MeshRenderer != null;
    }

    internal readonly struct GeneralImprovementsMonitorSpecification(Renderer renderer, int materialIndex, Material originalMaterial)
    {
        internal readonly Renderer Renderer = renderer;
        internal readonly int MaterialIndex = materialIndex;
        internal readonly Material OriginalMaterial = originalMaterial;
    }

    internal static GeneralImprovementsMonitorSpecification? GetMonitorForID(int id)
    {
        if (!BetterMonitorsEnabled)
            return null;

        if (id < 0)
        {
            GeneralImprovementsMonitorSpecification? monitor = null;
            var i = 0;
            while (true)
            {
                var nextMonitor = GetMonitorForIDWithAPI(i);
                if (nextMonitor == null)
                    break;
                monitor = nextMonitor;
                i++;
            }

            return monitor;
        }

        return GetMonitorForIDWithAPI(id);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static GeneralImprovementsMonitorSpecification? GetMonitorForIDWithAPI(int id)
    {
        var monitorInfo = MonitorsAPI.GetMonitorAtIndex(id);
        if (monitorInfo == null)
            return null;

        return new GeneralImprovementsMonitorSpecification(monitorInfo.MeshRenderer, monitorInfo.ScreenMaterialIndex, monitorInfo.AssignedMaterial);
    }
}
