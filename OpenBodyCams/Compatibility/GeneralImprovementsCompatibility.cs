using System;
using System.Runtime.CompilerServices;

using BepInEx.Bootstrap;
using GeneralImprovements.API;
using UnityEngine;

namespace OpenBodyCams.Compatibility
{
    public static class GeneralImprovementsCompatibility
    {
        private const string MonitorGroupPath = "Environment/HangarShip/ShipModels2b/MonitorWall/MonitorGroup(Clone)";
        private static readonly string[] MonitorPaths = [
            $"{MonitorGroupPath}/Monitors/TopGroupL/Screen1",
            $"{MonitorGroupPath}/Monitors/TopGroupL/Screen2",
            $"{MonitorGroupPath}/Monitors/TopGroupM/Screen3",
            $"{MonitorGroupPath}/Monitors/TopGroupM/Screen4",
            $"{MonitorGroupPath}/Monitors/TopGroupR/Screen5",
            $"{MonitorGroupPath}/Monitors/TopGroupR/Screen6",
            $"{MonitorGroupPath}/Monitors/TopGroupL/Screen7",
            $"{MonitorGroupPath}/Monitors/TopGroupL/Screen8",
            $"{MonitorGroupPath}/Monitors/TopGroupM/Screen9",
            $"{MonitorGroupPath}/Monitors/TopGroupM/Screen10",
            $"{MonitorGroupPath}/Monitors/TopGroupR/Screen11",
            $"{MonitorGroupPath}/Monitors/TopGroupR/Screen12",
            $"{MonitorGroupPath}/Monitors/BigLeft/LScreen",
            $"{MonitorGroupPath}/Monitors/BigRight/RScreen",
        ];

        internal static bool GeneralImprovementsEnabled => Chainloader.PluginInfos.ContainsKey(ModGUIDs.GeneralImprovements);
        internal static bool BetterMonitorsEnabled
        {
            get
            {
                if (GeneralImprovementsEnabled)
                {
                    try
                    {
                        // As of 1.2.7, this API is not hooked up properly yet, so it won't be accurate.
                        // Check manually as well.
                        if (BetterMonitorsEnabledWithAPI())
                            return true;
                    }
                    catch { }
                }

                return GameObject.Find(MonitorGroupPath) != null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool BetterMonitorsEnabledWithAPI()
        {
            return MonitorsAPI.NewMonitorMeshActive;
        }

        public readonly struct GeneralImprovementsMonitorSpecification(Renderer renderer, int materialIndex, Material originalMaterial)
        {
            public readonly Renderer Renderer = renderer;
            public readonly int MaterialIndex = materialIndex;
            public readonly Material OriginalMaterial = originalMaterial;
        }

        public static GeneralImprovementsMonitorSpecification? GetMonitorForID(int id)
        {
            if (!BetterMonitorsEnabled)
                return null;

            if (id < 0)
            {
                for (var i = MonitorPaths.Length; i-- > 0;)
                {
                    var monitor = GetMonitorForIDImpl(i);
                    if (monitor != null)
                        return monitor;
                }
            }
            else
            {
                return GetMonitorForIDImpl(id);
            }

            return null;
        }

        private static GeneralImprovementsMonitorSpecification? GetMonitorForIDImpl(int id)
        {
            if (!BetterMonitorsEnabled)
                return null;

            try
            {
                return GetMonitorForIDWithAPI(id);
            }
            catch { }

            if (id < MonitorPaths.Length)
            {
                var currentID = 0;
                for (var i = 0; i < MonitorPaths.Length; i++)
                {
                    var renderer = GameObject.Find(MonitorPaths[i])?.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    {
                        if (currentID == id)
                            return new GeneralImprovementsMonitorSpecification(renderer, 0, null);
                        currentID++;
                    }
                }
            }

            return null;
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
}
