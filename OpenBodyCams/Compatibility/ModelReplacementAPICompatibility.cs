using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using GameNetcodeStuff;
using HarmonyLib;
using ModelReplacement;
using UnityEngine;

namespace OpenBodyCams.Compatibility
{
    public static class ModelReplacementAPICompatibility
    {
        public static MethodInfo m_UpdateModelReplacement;

        public static bool Initialize(Harmony harmony)
        {
            var m_ViewStateManager_ReportBodyReplacementAddition = typeof(ViewStateManager).GetMethod(nameof(ViewStateManager.ReportBodyReplacementAddition), [typeof(BodyReplacementBase)]);
            if (m_ViewStateManager_ReportBodyReplacementAddition is null)
            {
                Plugin.Instance.Logger.LogInfo($"ModelReplacementAPI is installed, but the `ViewStateManager.ReportBodyReplacementAddition()` method was not found.");
                return false;
            }

            var m_ModelReplacementAPI_RemovePlayerModelReplacement = typeof(ModelReplacementAPI).GetMethod(nameof(ModelReplacementAPI.RemovePlayerModelReplacement), [typeof(PlayerControllerB)]);
            if (m_ModelReplacementAPI_RemovePlayerModelReplacement is null)
            {
                Plugin.Instance.Logger.LogInfo($"ModelReplacementAPI is installed, but the `ModelReplacementAPI.RemovePlayerModelReplacement()` method was not found.");
                return false;
            }

            m_UpdateModelReplacement = typeof(ModelReplacementAPICompatibility).GetMethod(nameof(UpdateModelReplacement), [typeof(ViewStateManager)]);

            harmony.CreateProcessor(m_ViewStateManager_ReportBodyReplacementAddition)
                .AddPostfix(m_UpdateModelReplacement)
                .Patch();

            var m_RemoveModelReplacementTranspiler = typeof(ModelReplacementAPICompatibility).GetMethod(nameof(RemoveModelReplacementTranspiler));
            harmony.CreateProcessor(m_ModelReplacementAPI_RemovePlayerModelReplacement)
                .AddTranspiler(m_RemoveModelReplacementTranspiler)
                .Patch();

            m_UpdateModelReplacement = null;
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CollectCosmetics(PlayerControllerB player, List<GameObject> thirdPersonCosmetics, List<GameObject> firstPersonCosmetics, ref bool hasViewmodelReplacement)
        {
            var bodyReplacement = player.GetComponent<ViewStateManager>()?.bodyReplacement;

            if (bodyReplacement == null)
                return;

            if (bodyReplacement.replacementModel != null)
                thirdPersonCosmetics.AddRange(bodyReplacement.replacementModel.GetComponentsInChildren<Transform>().Select(cosmeticObject => cosmeticObject.gameObject));

            if (bodyReplacement.replacementViewModel != null)
            {
                firstPersonCosmetics.AddRange(bodyReplacement.replacementViewModel.GetComponentsInChildren<Transform>().Select(cosmeticObject => cosmeticObject.gameObject));
                hasViewmodelReplacement = true;
            }
        }

        public static void UpdateModelReplacement(ViewStateManager __instance)
        {
            BodyCamComponent.MarkTargetDirtyUntilRenderForAllBodyCams(__instance.controller.transform);
        }

        public static IEnumerable<CodeInstruction> RemoveModelReplacementTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = new List<CodeInstruction>(instructions);

            var m_Object_Destroy = typeof(UnityEngine.Object).GetMethod(nameof(UnityEngine.Object.Destroy), [ typeof(UnityEngine.Object) ]);
            var m_Component_GetComponent_ViewStateManager = typeof(Component).GetMethods().First(method => method.Name == "GetComponent" && method.IsGenericMethod).MakeGenericMethod([typeof(ViewStateManager)]);

            var m_ViewStateManager_ReportBodyReplacementRemoval = typeof(ViewStateManager).GetMethod(nameof(ViewStateManager.ReportBodyReplacementRemoval), []);

            var destroyBodyReplacement = instructionsList.FindIndex(insn => insn.Calls(m_Object_Destroy));
            instructionsList.InsertRange(destroyBodyReplacement + 1,
                [
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Call, m_Component_GetComponent_ViewStateManager),
                    new(OpCodes.Dup),
                    new(OpCodes.Call, m_ViewStateManager_ReportBodyReplacementRemoval),
                    new(OpCodes.Call, m_UpdateModelReplacement),
                ]);

            return instructionsList;
        }
    }
}
