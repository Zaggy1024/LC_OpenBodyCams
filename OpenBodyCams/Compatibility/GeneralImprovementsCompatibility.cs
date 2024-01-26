using UnityEngine;

namespace OpenBodyCams.Compatibility
{
    public static class GeneralImprovementsCompatibility
    {
        private static string getMonitorPath(int id)
        {
            switch (id)
            {
                case 0:
                    return "Monitors/StructureL/Screen1";
                case 1:
                    return "Monitors/StructureL/Screen2";
                case 2:
                    return "Monitors/StructureM/Screen3";
                case 3:
                    return "Monitors/StructureM/Screen4";
                case 4:
                    return "Monitors/StructureR/Screen5";
                case 5:
                    return "Monitors/StructureR/Screen6";
                case 6:
                    return "Monitors/StructureL/Screen7";
                case 7:
                    return "Monitors/StructureL/Screen8";
                case 8:
                    return "Monitors/StructureM/Screen9";
                case 9:
                    return "Monitors/StructureM/Screen10";
                case 10:
                    return "Monitors/StructureR/Screen11";
                case 11:
                    return "Monitors/StructureR/Screen12";
                case 12:
                    return "BigMonitors/Left/LScreen";
                case 13:
                    return "BigMonitors/Right/RScreen";
            }

            return null;
        }

        public static MeshRenderer GetMonitorForID(int id)
        {
            return GameObject.Find("Environment/HangarShip/ShipModels2b/MonitorWall/MonitorGroup(Clone)/" + getMonitorPath(id))?.GetComponent<MeshRenderer>();
        }
    }
}
