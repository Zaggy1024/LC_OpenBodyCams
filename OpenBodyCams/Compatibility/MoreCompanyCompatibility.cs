using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using MoreCompany;
using MoreCompany.Cosmetics;

using OpenBodyCams.Patches;

namespace OpenBodyCams.Compatibility
{
    public static class MoreCompanyCompatibility
    {
        static MethodInfo m_CosmeticApplication_ClearCosmetics;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Initialize(Harmony harmony)
        {
            var m_ClientReceiveMessagePatch_HandleDataMessage = typeof(ClientReceiveMessagePatch).GetMethod("HandleDataMessage", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(string) }, null);

            m_CosmeticApplication_ClearCosmetics = typeof(CosmeticApplication).GetMethod(nameof(CosmeticApplication.ClearCosmetics), new Type[0]);
            if (m_CosmeticApplication_ClearCosmetics is null)
            {
                Plugin.Instance.Logger.LogInfo($"MoreCompany is installed, but `CosmeticApplication.ClearCosmetics()` was not found.");
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
                CodeInstruction.Call(typeof(MoreCompanyCompatibility), nameof(SetUpLocalMoreCompanyCosmetics)),
            });

            return instructionsList;
        }

        static void SetUpLocalMoreCompanyCosmetics(CosmeticApplication cosmeticApplication)
        {
            foreach (var cosmetic in cosmeticApplication.spawnedCosmetics)
            {
                foreach (var child in cosmetic.GetComponentsInChildren<Transform>())
                    child.gameObject.layer = BodyCamComponent.ENEMIES_NOT_RENDERED_LAYER;
            }
        }

        public static void ClientReceiveMessagePatch_HandleDataMessagePostfix()
        {
            Plugin.BodyCam?.UpdateCurrentTarget();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static GameObject[] CollectCosmetics(PlayerControllerB player)
        {
            if (player.GetComponentInChildren<CosmeticApplication>() is CosmeticApplication cosmeticApplication)
            {
                Plugin.Instance.Logger.LogInfo($"Getting MoreCompany cosmetic models for {player.playerUsername}");
                return cosmeticApplication.spawnedCosmetics.SelectMany(cosmetic => cosmetic.GetComponentsInChildren<Transform>()).Select(cosmeticObject => cosmeticObject.gameObject).ToArray();
            }

            return new GameObject[0];
        }
    }
}
