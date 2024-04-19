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
            var m_ClientReceiveMessagePatch_HandleDataMessage = typeof(ClientReceiveMessagePatch).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).First(method => method.Name == "HandleDataMessage");

            m_CosmeticApplication_ClearCosmetics = typeof(CosmeticApplication).GetMethod(nameof(CosmeticApplication.ClearCosmetics), []);
            if (m_CosmeticApplication_ClearCosmetics is null)
            {
                Plugin.Instance.Logger.LogInfo($"MoreCompany is installed, but `CosmeticApplication.ClearCosmetics()` was not found.");
                return false;
            }

            var thisType = typeof(MoreCompanyCompatibility);
            harmony.CreateProcessor(m_ClientReceiveMessagePatch_HandleDataMessage)
                .AddTranspiler(thisType.GetMethod(nameof(ClientReceiveMessagePatch_HandleDataMessageTranspiler)))
                .Patch();

            (string, Type[])[] cosmeticApplicationMethods = [
                (nameof(CosmeticApplication.ClearCosmetics), []),
                (nameof(CosmeticApplication.ApplyCosmetic), [typeof(string), typeof(bool)]),
            ];
            var m_UpdateCosmetics = thisType.GetMethod(nameof(UpdateCosmetics), BindingFlags.NonPublic | BindingFlags.Static);

            foreach (var (method, parameters) in cosmeticApplicationMethods)
            {
                harmony.CreateProcessor(typeof(CosmeticApplication).GetMethod(method, parameters))
                    .AddPrefix(m_UpdateCosmetics)
                    .Patch();
            }

            Plugin.Instance.Logger.LogInfo($"Patched MoreCompany to spawn cosmetics on the local player.");
            return true;
        }

        public static IEnumerable<CodeInstruction> ClientReceiveMessagePatch_HandleDataMessageTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            // Change cosmetic spawning to keep the cosmetics applied to the local player, but placed into the invisible enemies layer to match
            // other mods that use it to display cosmetics in third person.
            var instructionsList = instructions.ToList();

            // Search for:
            //   bool isLocalPlayer = playerId == StartOfRound.Instance.thisClientPlayerId;
            // Debug IL:
            var isLocalPlayer = instructionsList.FindIndexOfSequence(
                [
                    insn => insn.IsLdloc(),
                    insn => insn.Calls(Reflection.m_StartOfRound_get_Instance),
                    insn => insn.LoadsField(Reflection.f_StartOfRound_thisClientPlayerId),
                    insn => insn.opcode == OpCodes.Ceq,
                    insn => insn.IsStloc(),
                    insn => insn.IsLdloc(),
                    insn => insn.opcode == OpCodes.Brfalse_S || insn.opcode == OpCodes.Brfalse
                ]);
            // Release IL:
            isLocalPlayer ??= instructionsList.FindIndexOfSequence(
                [
                    insn => insn.IsLdloc(),
                    insn => insn.Calls(Reflection.m_StartOfRound_get_Instance),
                    insn => insn.LoadsField(Reflection.f_StartOfRound_thisClientPlayerId),
                    insn => insn.opcode == OpCodes.Bne_Un_S || insn.opcode == OpCodes.Bne_Un,
                ]);

            // Then find:
            //   cosmeticApplication.ClearCosmetics();
            var clearCosmetics = instructionsList.FindIndexOfSequence(isLocalPlayer.End,
                [
                    insn => insn.IsLdloc(),
                    insn => insn.Calls(m_CosmeticApplication_ClearCosmetics),
                ]);
            instructionsList.RemoveAt(clearCosmetics.End - 1);
            // Replace it with:
            //   MoreCompanyCompatibility.SetUpLocalMoreCompanyCosmetics(cosmeticApplication);
            instructionsList.Insert(clearCosmetics.End - 1, CodeInstruction.Call(typeof(MoreCompanyCompatibility), nameof(SetUpLocalMoreCompanyCosmetics)));

            return instructionsList;
        }

        private static void UpdateCosmetics()
        {
            BodyCamComponent.MarkTargetDirtyUntilRenderForAllBodyCams();
        }

        static void SetUpLocalMoreCompanyCosmetics(CosmeticApplication cosmeticApplication)
        {
            foreach (var cosmetic in cosmeticApplication.spawnedCosmetics)
            {
                foreach (var child in cosmetic.GetComponentsInChildren<Transform>())
                    child.gameObject.layer = BodyCamComponent.ENEMIES_NOT_RENDERED_LAYER;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static GameObject[] CollectCosmetics(PlayerControllerB player)
        {
            Plugin.Instance.Logger.LogInfo($"Getting MoreCompany cosmetic models for {player.playerUsername}");
            return player.GetComponentsInChildren<CosmeticApplication>()
                .SelectMany(cosmeticApplication => cosmeticApplication.spawnedCosmetics)
                .Where(cosmetic => cosmetic != null)
                .SelectMany(cosmetic => cosmetic.GetComponentsInChildren<Transform>())
                .Select(cosmeticObject => cosmeticObject.gameObject)
                .ToArray();
        }
    }
}
