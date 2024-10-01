using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using AdvancedCompany.Game;
using AdvancedCompany.Objects;
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

            (string, Type[])[] postfixToMethods =
                [
                    (nameof(Player.SetCosmetics), [typeof(string[]), typeof(bool)]),
                    (nameof(Player.AddCosmetic), [typeof(string)]),
                    (nameof(Player.ReequipHead), []),
                    (nameof(Player.ReequipBody), []),
                    (nameof(Player.ReequipFeet), []),
                    (nameof(Player.UnequipAll), []),
                ];
            var postfixMethod = typeof(AdvancedCompanyCompatibility).GetMethod(nameof(AfterEquipmentChange), BindingFlags.NonPublic | BindingFlags.Static);

            foreach ((string methodName, Type[] types) in postfixToMethods)
            {
                var method = t_Player.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, types, null);
                if (method is null)
                {
                    Plugin.Instance.Logger.LogWarning($"Failed to find {t_Player.Name}.{methodName} to apply postfix.");
                    return false;
                }

                harmony
                    .CreateProcessor(method)
                    .AddPostfix(postfixMethod).Patch();
            }

            harmony
                .CreateProcessor(typeof(LightShoeRGB).GetMethod(nameof(LightShoeRGB.LiftCurse), BindingFlags.NonPublic | BindingFlags.Instance))
                .AddPostfix(typeof(AdvancedCompanyCompatibility).GetMethod(nameof(UpdateCosmeticsAfterCoroutine), BindingFlags.NonPublic | BindingFlags.Static))
                .Patch();

            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IEnumerable<GameObject> CollectCosmetics(PlayerControllerB player)
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
                .Where(cosmetic => cosmetic != null)
                .SelectMany(cosmetic => cosmetic.GetComponentsInChildren<Transform>())
                .Select(transform => transform.gameObject)
                .Concat(acPlayer.HeadMount == null ? [] : [acPlayer.HeadMount]);
        }

        static void AfterEquipmentChange()
        {
            BodyCamComponent.MarkTargetDirtyUntilRenderForAllBodyCams();
        }

        static IEnumerator UpdateCosmeticsAfterCoroutine(IEnumerator __result)
        {
            while (true)
            {
                try
                {
                    if (!__result.MoveNext())
                        break;
                }
                catch (Exception e)
                {
                    Plugin.Instance.Logger.LogError("AdvancedCompany encountered an error in a coroutine:");
                    Plugin.Instance.Logger.LogError(e);
                    break;
                }
                yield return __result.Current;
            }

            BodyCamComponent.MarkTargetDirtyUntilRenderForAllBodyCams();
        }
    }
}
