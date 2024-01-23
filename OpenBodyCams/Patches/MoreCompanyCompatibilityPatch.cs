using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

namespace OpenBodyCams.Patches
{
    public static class MoreCompanyCompatibilityPatch
    {
        public static readonly Type t_ClientReceiveMessagePatch = AccessTools.TypeByName("MoreCompany.ClientReceiveMessagePatch");
        public static readonly MethodInfo m_ClientReceiveMessagePatch_HandleDataMessage = t_ClientReceiveMessagePatch is Type ? AccessTools.Method(t_ClientReceiveMessagePatch, "HandleDataMessage", new Type[] { typeof(string) }) : null;

        public static readonly Type t_CosmeticApplication = AccessTools.TypeByName("MoreCompany.Cosmetics.CosmeticApplication");
        public static readonly MethodInfo m_CosmeticApplication_ClearCosmetics = t_CosmeticApplication is Type ? AccessTools.Method(t_CosmeticApplication, "ClearCosmetics", new Type[0]) : null;
        public static readonly FieldInfo f_CosmeticApplication_spawnedCosmetics = t_CosmeticApplication is Type ? AccessTools.Field(t_CosmeticApplication, "spawnedCosmetics") : null;

        public static void ApplyPatches(Harmony harmony)
        {
            if (m_ClientReceiveMessagePatch_HandleDataMessage is null || m_CosmeticApplication_ClearCosmetics is null)
            {
                Plugin.Instance.Logger.LogInfo($"MoreCompany is not installed, or is incompatible with the {Plugin.MOD_NAME} patch.");
                return;
            }

            var thisType = typeof(MoreCompanyCompatibilityPatch);
            harmony.CreateProcessor(m_ClientReceiveMessagePatch_HandleDataMessage)
                .AddTranspiler(thisType.GetMethod(nameof(ClientReceiveMessagePatch_HandleDataMessageTranspiler)))
                .AddPostfix(thisType.GetMethod(nameof(ClientReceiveMessagePatch_HandleDataMessagePostfix)))
                .Patch();
            Plugin.Instance.Logger.LogInfo($"Patched MoreCompany to spawn cosmetics on the local player.");
        }

        public static IEnumerable<CodeInstruction> ClientReceiveMessagePatch_HandleDataMessageTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = instructions.ToList();

            var isLocalPlayer = instructionsList.FindIndexOfSequence(new Predicate<CodeInstruction>[]
            {
                insn => insn.IsLdloc(),
                insn => insn.Calls(Reflection.m_StartOfRound_get_Instance),
                insn => insn.LoadsField(Reflection.f_StartOfRound_thisClientPlayerId),
                insn => insn.opcode == OpCodes.Ceq,
                insn => insn.IsStloc(),
            });

            var clearCosmetics = instructionsList.FindIndexOfSequence(isLocalPlayer.End, new Predicate<CodeInstruction>[]
            {
                insn => insn.IsLdloc(),
                insn => insn.Calls(m_CosmeticApplication_ClearCosmetics),
            });
            instructionsList.RemoveAt(clearCosmetics.End - 1);
            instructionsList.InsertRange(clearCosmetics.End - 1, new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldc_I4_0),
                new CodeInstruction(OpCodes.Call, Reflection.m_Behaviour_set_enabled),
            });

            return instructionsList;
        }

        public static void ClientReceiveMessagePatch_HandleDataMessagePostfix()
        {
            Plugin.BodyCam.UpdateCurrentTarget();
        }
    }
}
