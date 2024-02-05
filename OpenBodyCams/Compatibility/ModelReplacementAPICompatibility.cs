﻿using System;
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
        private static MethodInfo m_UpdateModelReplacement;

        private static FieldInfo f_ViewStateManager_bodyReplacement;

        public static bool Initialize(Harmony harmony)
        {
            var m_ViewStateManager_ReportBodyReplacementAddition = typeof(ViewStateManager).GetMethod(nameof(ViewStateManager.ReportBodyReplacementAddition), new Type[] { typeof(BodyReplacementBase) });
            if (m_ViewStateManager_ReportBodyReplacementAddition is null)
            {
                Plugin.Instance.Logger.LogInfo($"ModelReplacementAPI is installed, but the `ViewStateManager.ReportBodyReplacementAddition()` method was not found.");
                return false;
            }

            var m_ModelReplacementAPI_RemovePlayerModelReplacement = typeof(ModelReplacementAPI).GetMethod(nameof(ModelReplacementAPI.RemovePlayerModelReplacement), new Type[] { typeof(PlayerControllerB) });
            if (m_ModelReplacementAPI_RemovePlayerModelReplacement is null)
            {
                Plugin.Instance.Logger.LogInfo($"ModelReplacementAPI is installed, but the `ModelReplacementAPI.RemovePlayerModelReplacement()` method was not found.");
                return false;
            }

            f_ViewStateManager_bodyReplacement = typeof(ViewStateManager).GetField("bodyReplacement", BindingFlags.Instance | BindingFlags.NonPublic);

            if (f_ViewStateManager_bodyReplacement is null)
            {
                Plugin.Instance.Logger.LogInfo($"ModelReplacementAPI is installed, but the `ViewStateManager.bodyReplacement` field was not found.");
                return false;
            }

            m_UpdateModelReplacement = typeof(ModelReplacementAPICompatibility).GetMethod(nameof(UpdateModelReplacement));

            harmony.CreateProcessor(m_ViewStateManager_ReportBodyReplacementAddition)
                .AddPostfix(m_UpdateModelReplacement)
                .Patch();

            var m_RemoveModelReplacementTranspiler = typeof(ModelReplacementAPICompatibility).GetMethod(nameof(RemoveModelReplacementTranspiler));
            harmony.CreateProcessor(m_ModelReplacementAPI_RemovePlayerModelReplacement)
                .AddTranspiler(m_RemoveModelReplacementTranspiler)
                .Patch();
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static GameObject[] CollectCosmetics(PlayerControllerB player)
        {
            var viewStateManager = player.GetComponent<ViewStateManager>();
            var bodyReplacement = f_ViewStateManager_bodyReplacement.GetValue(viewStateManager) as BodyReplacementBase;

            if (bodyReplacement == null)
                return new GameObject[0];

            return bodyReplacement.replacementModel.GetComponentsInChildren<Transform>().Select(cosmeticObject => cosmeticObject.gameObject).ToArray();
        }

        public static void UpdateModelReplacement()
        {
            BodyCamComponent.UpdateAllTargetStatuses();
        }

        public static IEnumerable<CodeInstruction> RemoveModelReplacementTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = new List<CodeInstruction>(instructions);

            var m_Object_Destroy = typeof(UnityEngine.Object).GetMethod(nameof(UnityEngine.Object.Destroy), new Type[] { typeof(UnityEngine.Object) });
            var m_Component_GetComponent_ViewStateManager = typeof(Component).GetMethods().First(method => method.Name == "GetComponent" && method.IsGenericMethod).MakeGenericMethod(new Type[] { typeof(ViewStateManager) });

            var m_ViewStateManager_ReportBodyReplacementRemoval = typeof(ViewStateManager).GetMethod(nameof(ViewStateManager.ReportBodyReplacementRemoval), new Type[0]);

            var destroyBodyReplacement = instructionsList.FindIndex(insn => insn.Calls(m_Object_Destroy));
            instructionsList.InsertRange(destroyBodyReplacement + 1,
                new CodeInstruction[] {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, m_Component_GetComponent_ViewStateManager),
                    new CodeInstruction(OpCodes.Call, m_ViewStateManager_ReportBodyReplacementRemoval),
                    new CodeInstruction(OpCodes.Call, m_UpdateModelReplacement),
                });

            return instructionsList;
        }
    }
}
