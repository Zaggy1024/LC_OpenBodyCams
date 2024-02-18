using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using GameNetcodeStuff;
using HarmonyLib;
using LethalVRM;
using UnityEngine;

namespace OpenBodyCams.Compatibility
{
    public static class LethalVRMCompatibility
    {
        // This must not reference a LethalVRM type so that we don't automatically load the assembly.
        static IEnumerable vrmInstances;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Initialize(Harmony harmony)
        {
            var vrmManager = GameObject.Find("LethalVRM Manager")?.GetComponent<LethalVRMManager>();
            if (vrmManager is null)
            {
                Plugin.Instance.Logger.LogWarning("Failed to find the LethalVRMManager instance.");
                return false;
            }
            vrmInstances = vrmManager.instances;
            if (vrmInstances is null)
            {
                Plugin.Instance.Logger.LogWarning("Failed to get the value of the LethalVRMManager.instances field.");
                return false;
            }

            var t_LethalVRMInstance = typeof(LethalVRMManager).GetNestedType("LethalVRMInstance", BindingFlags.NonPublic | BindingFlags.Public);
            if (t_LethalVRMInstance is null)
            {
                Plugin.Instance.Logger.LogWarning("LethalVRMInstance class not found.");
                return false;
            }

            var loadModelMethod = typeof(LethalVRMManager).GetMethod("LoadModelToPlayer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var moveNextMethod = loadModelMethod
                .GetCustomAttribute<AsyncStateMachineAttribute>().StateMachineType
                .GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);
            harmony
                .CreateProcessor(moveNextMethod)
                .AddTranspiler(typeof(LethalVRMCompatibility).GetMethod(nameof(LoadModelToPlayerPostfix), BindingFlags.Static | BindingFlags.NonPublic))
                .Patch();

            return true;
        }

        static IEnumerable<CodeInstruction> LoadModelToPlayerPostfix(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = instructions.ToList();

            var updateTargetMethod = typeof(BodyCamComponent).GetMethod(nameof(BodyCamComponent.UpdateAllTargetStatuses));
            instructionsList.Insert(instructionsList.Count() - 2, new CodeInstruction(OpCodes.Call, updateTargetMethod));

            return instructionsList;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static GameObject[] CollectCosmetics(PlayerControllerB player)
        {
            foreach (var instance in (ICollection<LethalVRMManager.LethalVRMInstance>)vrmInstances)
            {
                if (instance == null)
                    continue;
                if (!ReferenceEquals(instance.PlayerControllerB, player))
                    continue;
                return instance.renderers
                    .Select(renderer => renderer.gameObject)
                    .ToArray();
            }

            return new GameObject[0];
        }
    }
}
