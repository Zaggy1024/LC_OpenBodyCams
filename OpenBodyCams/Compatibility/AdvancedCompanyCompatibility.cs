using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using AdvancedCompany.Game;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace OpenBodyCams.Compatibility
{
    public static class AdvancedCompanyCompatibility
    {
        public static bool Initialize(Harmony harmony)
        {
            var t_Player = typeof(Player);

            string[] postfixToMethods =
                [
                    nameof(Player.ReequipHead),
                    nameof(Player.ReequipBody),
                    nameof(Player.ReequipFeet),
                    nameof(Player.UnequipAll)
                ];
            var postfixMethod = typeof(AdvancedCompanyCompatibility).GetMethod(nameof(OnEquipmentChange), BindingFlags.NonPublic | BindingFlags.Static);

            foreach (string methodName in postfixToMethods)
            {
                var method = t_Player.GetMethod(methodName, []);
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
            Player acPlayer = Player.GetPlayer(player);
            IEnumerable<GameObject> attachedObjects = acPlayer.AppliedCosmetics.Values;

            if (acPlayer.EquipmentItemsHead is GameObject[] headObjects)
                attachedObjects = attachedObjects.Concat(headObjects);
            if (acPlayer.EquipmentItemsBody is GameObject[] bodyObjects)
                attachedObjects = attachedObjects.Concat(bodyObjects);
            if (acPlayer.EquipmentItemsFeet is GameObject[] feetObjects)
                attachedObjects = attachedObjects.Concat(feetObjects);

            return attachedObjects
                .SelectMany(cosmetic => cosmetic.GetComponentsInChildren<Transform>())
                .Select(transform => transform.gameObject)
                .Concat(acPlayer.HeadMount == null ? [] : [ acPlayer.HeadMount ])
                .ToArray();
        }

        static void OnEquipmentChange()
        {
            BodyCamComponent.UpdateAllTargetStatuses();
        }
    }
}
