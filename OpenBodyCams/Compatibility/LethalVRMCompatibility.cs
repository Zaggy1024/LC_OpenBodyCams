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
        static IEnumerable vrmInstances;

        static FieldInfo f_LethalVRMInstance_PlayerControllerB;
        static FieldInfo f_LethalVRMInstance_renderers;

        public static bool Initialize(Harmony harmony)
        {
            var vrmManager = GameObject.Find("LethalVRM Manager")?.GetComponent<LethalVRMManager>();
            if (vrmManager is null)
            {
                Plugin.Instance.Logger.LogWarning("Failed to find the LethalVRMManager instance.");
                return false;
            }
            var f_LethalVRMManager_instances = typeof(LethalVRMManager).GetField("instances", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            vrmInstances = f_LethalVRMManager_instances?.GetValue(vrmManager) as IEnumerable;
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
            f_LethalVRMInstance_PlayerControllerB = t_LethalVRMInstance.GetField("PlayerControllerB", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f_LethalVRMInstance_PlayerControllerB is null)
            {
                Plugin.Instance.Logger.LogWarning("LethalVRMInstance.PlayerControllerB field not found.");
                return false;
            }
            f_LethalVRMInstance_renderers = t_LethalVRMInstance.GetField("renderers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f_LethalVRMInstance_renderers is null)
            {
                Plugin.Instance.Logger.LogWarning("LethalVRMInstance.renderers field not found.");
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

            var updateTargetMethod = typeof(BodyCamComponent).GetMethod(nameof(BodyCamComponent.UpdateCurrentTarget));
            instructionsList.InsertRange(instructionsList.Count() - 2, new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldsfld, typeof(Plugin).GetField(nameof(Plugin.BodyCam))),
                new CodeInstruction(OpCodes.Call, updateTargetMethod),
            });

            return instructionsList;
        }

        public static GameObject[] CollectCosmetics(PlayerControllerB player)
        {
            foreach (var instance in vrmInstances)
            {
                if (!ReferenceEquals(f_LethalVRMInstance_PlayerControllerB.GetValue(instance), player))
                    continue;
                var renderers = f_LethalVRMInstance_renderers.GetValue(instance) as ICollection<Renderer>;
                return renderers.Select(renderer => renderer.gameObject).ToArray();
            }

            return new GameObject[0];
        }
    }
}
