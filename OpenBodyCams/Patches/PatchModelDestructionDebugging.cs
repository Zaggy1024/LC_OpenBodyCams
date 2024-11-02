using System.Diagnostics;

using HarmonyLib;
using UnityEngine;

namespace OpenBodyCams.Patches
{
    internal static class PatchModelDestructionDebugging
    {
        private static bool IsChildReferenced(GameObject obj)
        {
            var transforms = obj.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (var transform in transforms)
            {
                if (BodyCamComponent.AnyBodyCamHasReference(transform.gameObject))
                    return true;
            }

            var renderers = obj.GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (var renderer in renderers)
            {
                if (BodyCamComponent.AnyBodyCamHasReference(renderer))
                    return true;
            }

            return false;
        }

        private static bool IsReferencedObject(Object obj)
        {
            // It's not an error to call Destroy on null.
            // Also, if an object is already destroyed, then we shouldn't check it for references,
            // since (hopefully) that will have been caught already.
            if (obj == null)
                return false;
            if (obj is Renderer renderer)
                return BodyCamComponent.AnyBodyCamHasReference(renderer);
            if (obj is GameObject gameObject)
                return IsChildReferenced(gameObject);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Object), nameof(Object.Destroy), [typeof(Object), typeof(float)])]
        private static void DestroyingObject(Object obj, float t)
        {
            if (IsReferencedObject(obj))
                Plugin.Instance.Logger.LogWarning($"In {t} seconds, {obj.name} will be destroyed while it is referenced by a body cam.\n{new StackTrace(2)}");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Object), nameof(Object.DestroyImmediate), [typeof(Object), typeof(bool)])]
        private static void DestroyingObjectImmediately(Object obj)
        {
            if (IsReferencedObject(obj))
                Plugin.Instance.Logger.LogWarning($"Immediately destroying {obj.name} which is referenced by a body cam.\n{new StackTrace(2)}");
        }
    }
}
