using System.Diagnostics;

using HarmonyLib;
using UnityEngine;

namespace OpenBodyCams.Patches
{
    internal static class PatchModelDestructionDebugging
    {
        private static bool IsReferencedObject(Object obj)
        {
            // It's not an error to call Destroy on null.
            // Also, if an object is already destroyed, then we shouldn't check it for references,
            // since (hopefully) that will have been caught already.
            if (obj == null)
                return false;
            if (obj is Renderer)
                return BodyCamComponent.AnyBodyCamHasReference((Renderer)obj);
            if (obj is GameObject gameObject)
            {
                var transforms = gameObject.GetComponentsInChildren<Transform>(includeInactive: true);
                foreach (var transform in transforms)
                {
                    if (BodyCamComponent.AnyBodyCamHasReference(transform.gameObject))
                        return true;
                }

                var renderers = gameObject.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (BodyCamComponent.AnyBodyCamHasReference(renderer))
                        return true;
                }
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Object), nameof(Object.Destroy), [typeof(Object), typeof(float)])]
        private static void DestroyingObject(Object obj, float t)
        {
            if (IsReferencedObject(obj))
            {
                Plugin.Instance.Logger.LogWarning($"In {t} seconds, {obj.name} will be destroyed while it is referenced by a body cam.");
                Plugin.Instance.Logger.LogWarning(new StackTrace());
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Object), nameof(Object.DestroyImmediate), [typeof(Object), typeof(bool)])]
        private static void DestroyingObjectImmediately(Object obj)
        {
            if (IsReferencedObject(obj))
            {
                Plugin.Instance.Logger.LogWarning($"Immediately destroying {obj.name} which is referenced by a body cam.");
                Plugin.Instance.Logger.LogWarning(new StackTrace());
            }
        }
    }
}
