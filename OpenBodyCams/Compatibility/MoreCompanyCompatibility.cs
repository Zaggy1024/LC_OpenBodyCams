using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using OpenBodyCams.Patches;

namespace OpenBodyCams.Compatibility
{
    public static class MoreCompanyCompatibility
    {
        static Type t_ClientReceiveMessagePatch;
        static MethodInfo m_ClientReceiveMessagePatch_HandleDataMessage;

        static Type t_CosmeticApplication;
        static MethodInfo m_CosmeticApplication_ClearCosmetics;
        static FieldInfo f_CosmeticApplication_spawnedCosmetics;

        public static bool ApplyPatches(Harmony harmony)
        {
            Assembly moreCompanyAssembly;
            try
            {
                moreCompanyAssembly = Assembly.Load("MoreCompany");
            }
            catch
            {
                Plugin.Instance.Logger.LogInfo("MoreCompany is not present or the version is unsupported.");
                return false;
            }

            t_ClientReceiveMessagePatch = moreCompanyAssembly.GetType("MoreCompany.ClientReceiveMessagePatch");
            if (t_ClientReceiveMessagePatch is null)
            {
                Plugin.Instance.Logger.LogInfo($"MoreCompany is not installed, or its version is incompatible with {Plugin.MOD_NAME}'s patch.");
                return false;
            }
            m_ClientReceiveMessagePatch_HandleDataMessage = t_ClientReceiveMessagePatch.GetMethod("HandleDataMessage", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(string) }, null);

            t_CosmeticApplication = moreCompanyAssembly.GetType("MoreCompany.Cosmetics.CosmeticApplication");
            if (t_ClientReceiveMessagePatch is null)
            {
                Plugin.Instance.Logger.LogInfo($"MoreCompany is installed, but `MoreCompany.Cosmetics.CosmeticApplication` was not found.");
                return false;
            }
            m_CosmeticApplication_ClearCosmetics = t_CosmeticApplication.GetMethod("ClearCosmetics", new Type[0]);
            f_CosmeticApplication_spawnedCosmetics = t_CosmeticApplication.GetField("spawnedCosmetics");
            if (m_CosmeticApplication_ClearCosmetics is null || f_CosmeticApplication_spawnedCosmetics is null)
            {
                Plugin.Instance.Logger.LogInfo($"MoreCompany is installed, but `CosmeticApplication` members were not found.");
                return false;
            }

            var thisType = typeof(MoreCompanyCompatibility);
            harmony.CreateProcessor(m_ClientReceiveMessagePatch_HandleDataMessage)
                .AddTranspiler(thisType.GetMethod(nameof(ClientReceiveMessagePatch_HandleDataMessageTranspiler)))
                .AddPostfix(thisType.GetMethod(nameof(ClientReceiveMessagePatch_HandleDataMessagePostfix)))
                .Patch();
            Plugin.Instance.Logger.LogInfo($"Patched MoreCompany to spawn cosmetics on the local player.");
            return true;
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
                new CodeInstruction(OpCodes.Ldfld, f_CosmeticApplication_spawnedCosmetics),
                CodeInstruction.Call(typeof(MoreCompanyCompatibility), "SetUpLocalMoreCompanyCosmetics"),
            });

            return instructionsList;
        }

        public static void ClientReceiveMessagePatch_HandleDataMessagePostfix()
        {
            Plugin.BodyCam.UpdateCurrentTarget();
        }

        public static void SetUpLocalMoreCompanyCosmetics(IList cosmetics)
        {
            foreach (var cosmetic in cosmetics.Cast<Component>())
            {
                foreach (var child in cosmetic.GetComponentsInChildren<Transform>())
                    child.gameObject.layer = BodyCamComponent.ENEMIES_NOT_RENDERED_LAYER;
            }
        }

        public static GameObject[] CollectCosmetics(PlayerControllerB player)
        {
            if (player.GetComponentInChildren(t_CosmeticApplication) is Behaviour cosmeticApplication)
            {
                Plugin.Instance.Logger.LogInfo($"Getting MoreCompany cosmetic models for {player.playerUsername}");
                var cosmeticsList = (IList)f_CosmeticApplication_spawnedCosmetics.GetValue(cosmeticApplication);
                var result = cosmeticsList.Cast<Component>().SelectMany(cosmetic => cosmetic.GetComponentsInChildren<Transform>()).Select(cosmeticObject => cosmeticObject.gameObject).ToArray();
                return result;
            }

            return new GameObject[0];
        }
    }
}
