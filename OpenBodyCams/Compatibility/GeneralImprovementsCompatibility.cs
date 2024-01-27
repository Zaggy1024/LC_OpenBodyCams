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
                    return "Monitors/TopGroupL/Screen1";
                case 1:
                    return "Monitors/TopGroupL/Screen2";
                case 2:
                    return "Monitors/TopGroupM/Screen3";
                case 3:
                    return "Monitors/TopGroupM/Screen4";
                case 4:
                    return "Monitors/TopGroupR/Screen5";
                case 5:
                    return "Monitors/TopGroupR/Screen6";
                case 6:
                    return "Monitors/TopGroupL/Screen7";
                case 7:
                    return "Monitors/TopGroupL/Screen8";
                case 8:
                    return "Monitors/TopGroupM/Screen9";
                case 9:
                    return "Monitors/TopGroupM/Screen10";
                case 10:
                    return "Monitors/TopGroupR/Screen11";
                case 11:
                    return "Monitors/TopGroupR/Screen12";
                case 12:
                    return "Monitors/BigLeft/LScreen";
                case 13:
                    return "Monitors/BigRight/RScreen";
            }

            return null;
        }

        public static MeshRenderer GetMonitorForID(int id)
        {
            return GameObject.Find("Environment/HangarShip/ShipModels2b/MonitorWall/MonitorGroup(Clone)/" + getMonitorPath(id))?.GetComponent<MeshRenderer>();
        }
    }
}
