using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using AdvancedCompany.Lib;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace OpenBodyCams.Compatibility
{
    public static class AdvancedCompanyCompatibility
    {
        static MethodInfo m_Player_GetPlayer;
        static FieldInfo f_Player_HeadMount;
        static FieldInfo f_Player_EquipmentItemsHead;
        static FieldInfo f_Player_EquipmentItemsBody;
        static FieldInfo f_Player_EquipmentItemsFeet;
        static FieldInfo f_Player_AppliedCosmetics;

        public static bool Initialize(Harmony harmony)
        {
            var t_Player = typeof(AdvancedCompany.Plugin).Assembly.GetType("AdvancedCompany.Game.Player");
            if (t_Player is null)
            {
                Plugin.Instance.Logger.LogWarning("Could not find the AdvancedCompany Player class.");
                return false;
            }

            m_Player_GetPlayer = t_Player.GetMethod("GetPlayer", new Type[] { typeof(PlayerControllerB) });
            if (m_Player_GetPlayer is null)
            {
                Plugin.Instance.Logger.LogWarning("Could not find the method to get an AdvancedCompany Player.");
                return false;
            }

            f_Player_HeadMount = t_Player.GetField("HeadMount", BindingFlags.Public | BindingFlags.Instance);
            if (f_Player_HeadMount is null)
            {
                Plugin.Instance.Logger.LogWarning("AdvancedCompany's Player.HeadMount field was not found.");
                return false;
            }

            f_Player_EquipmentItemsHead = t_Player.GetField("EquipmentItemsHead", BindingFlags.NonPublic | BindingFlags.Instance);
            f_Player_EquipmentItemsBody = t_Player.GetField("EquipmentItemsBody", BindingFlags.NonPublic | BindingFlags.Instance);
            f_Player_EquipmentItemsFeet = t_Player.GetField("EquipmentItemsFeet", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f_Player_EquipmentItemsHead is null || f_Player_EquipmentItemsBody is null || f_Player_EquipmentItemsFeet is null)
            {
                Plugin.Instance.Logger.LogWarning("One or multiple of AdvancedCompany's player equipment fields were not found.");
                return false;
            }

            f_Player_AppliedCosmetics = t_Player.GetField("AppliedCosmetics", BindingFlags.Public | BindingFlags.Instance);
            if (f_Player_AppliedCosmetics is null)
            {
                Plugin.Instance.Logger.LogWarning("AdvancedCompany's Player.AppliedCosmetics field was not found.");
                return false;
            }

            var postfixToMethods = new string[] { "ReequipHead", "ReequipBody", "ReequipFeet", "UnequipAll" };
            var postfixMethod = typeof(AdvancedCompanyCompatibility).GetMethod(nameof(OnEquipmentChange), BindingFlags.NonPublic | BindingFlags.Static);

            foreach (string methodName in postfixToMethods)
            {
                var method = t_Player.GetMethod(methodName, new Type[0]);
                if (method is null)
                {
                    Plugin.Instance.Logger.LogWarning($"Failed to find {t_Player.Name}.{methodName} to apply postfix.");
                    return false;
                }

                harmony
                    .CreateProcessor(method)
                    .AddPostfix(postfixMethod).Patch();
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static GameObject[] CollectCosmetics(PlayerControllerB player)
        {
            object acPlayer = m_Player_GetPlayer.Invoke(null, new object[] { player });
            IEnumerable<GameObject> attachedObjects = Cosmetics.GetSpawnedCosmetics(player);

            if (f_Player_EquipmentItemsHead.GetValue(acPlayer) is GameObject[] headObjects)
                attachedObjects = attachedObjects.Concat(headObjects);
            if (f_Player_EquipmentItemsBody.GetValue(acPlayer) is GameObject[] bodyObjects)
                attachedObjects = attachedObjects.Concat(bodyObjects);
            if (f_Player_EquipmentItemsFeet.GetValue(acPlayer) is GameObject[] feetObjects)
                attachedObjects = attachedObjects.Concat(feetObjects);

            var headmount = f_Player_HeadMount.GetValue(acPlayer) as GameObject;

            return attachedObjects
                .SelectMany(cosmetic => cosmetic.GetComponentsInChildren<Transform>())
                .Select(transform => transform.gameObject)
                .Concat(headmount == null ? new GameObject[0] : new GameObject[] { headmount })
                .ToArray();
        }

        static void OnEquipmentChange()
        {
            BodyCamComponent.UpdateAllTargetStatuses();
        }
    }
}
