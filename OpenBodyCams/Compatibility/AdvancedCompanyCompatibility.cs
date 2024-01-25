﻿using System;
using System.Collections;
using System.Linq;
using System.Reflection;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace OpenBodyCams.Compatibility
{
    public static class AdvancedCompanyCompatibility
    {
        static Type t_Cosmetics;
        static MethodInfo m_Cosmetics_GetSpawnedCosmetics;

        public static bool Initialize()
        {
            t_Cosmetics = AccessTools.AllTypes().FirstOrDefault(t => t.FullName == "AdvancedCompany.Lib.Cosmetics");
            if (t_Cosmetics is null)
            {
                Plugin.Instance.Logger.LogInfo("AdvancedCompany is not present or the version is unsupported.");
                return false;
            }
            m_Cosmetics_GetSpawnedCosmetics = t_Cosmetics.GetMethod("GetSpawnedCosmetics", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(PlayerControllerB) }, null);
            if (m_Cosmetics_GetSpawnedCosmetics is null)
            {
                Plugin.Instance.Logger.LogInfo("AdvancedCompany is installed, but Cosmetics.GetSpawnedCosmetics is not found.");
                return false;
            }

            return true;
        }

        public static GameObject[] CollectCosmetics(PlayerControllerB player)
        {
            var cosmetics = (GameObject[])m_Cosmetics_GetSpawnedCosmetics.Invoke(null, new object[] { player });
            return cosmetics.SelectMany(cosmetic => cosmetic.GetComponentsInChildren<Transform>()).Select(transform => transform.gameObject).ToArray();
        }
    }
}
