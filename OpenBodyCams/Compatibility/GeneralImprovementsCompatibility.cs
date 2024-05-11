using System;
using System.Runtime.CompilerServices;

using BepInEx.Bootstrap;
using GeneralImprovements.Assets;
using UnityEngine;

namespace OpenBodyCams.Compatibility
{
    public static class GeneralImprovementsCompatibility
    {
        private const string MonitorGroupPath = "Environment/HangarShip/ShipModels2b/MonitorWall/MonitorGroup(Clone)";

        internal static bool GeneralImprovementsEnabled => Chainloader.PluginInfos.ContainsKey(ModGUIDs.GeneralImprovements);
        internal static bool BetterMonitorsEnabled
        {
            get
            {
                return GameObject.Find(MonitorGroupPath) != null;
            }
        }

        private static string GetMonitorPath(int id)
        {
            return id switch
            {
                0 => $"{MonitorGroupPath}/Monitors/TopGroupL/Screen1",
                1 => $"{MonitorGroupPath}/Monitors/TopGroupL/Screen2",
                2 => $"{MonitorGroupPath}/Monitors/TopGroupM/Screen3",
                3 => $"{MonitorGroupPath}/Monitors/TopGroupM/Screen4",
                4 => $"{MonitorGroupPath}/Monitors/TopGroupR/Screen5",
                5 => $"{MonitorGroupPath}/Monitors/TopGroupR/Screen6",
                6 => $"{MonitorGroupPath}/Monitors/TopGroupL/Screen7",
                7 => $"{MonitorGroupPath}/Monitors/TopGroupL/Screen8",
                8 => $"{MonitorGroupPath}/Monitors/TopGroupM/Screen9",
                9 => $"{MonitorGroupPath}/Monitors/TopGroupM/Screen10",
                10 => $"{MonitorGroupPath}/Monitors/TopGroupR/Screen11",
                11 => $"{MonitorGroupPath}/Monitors/TopGroupR/Screen12",
                12 => $"{MonitorGroupPath}/Monitors/BigLeft/LScreen",
                13 => $"{MonitorGroupPath}/Monitors/BigRight/RScreen",
                _ => null,
            };
        }

        public static MeshRenderer GetMonitorForID(int id)
        {
            return GameObject.Find(GetMonitorPath(id))?.GetComponent<MeshRenderer>();
        }
    }
}
