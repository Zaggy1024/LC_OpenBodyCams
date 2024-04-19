using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using OpenBodyCams.Utilities;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(UnlockableSuit))]
    internal static class PatchUnlockableSuit
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(UnlockableSuit.SwitchSuitForPlayer))]
        private static IEnumerable<CodeInstruction> SwitchSuitForPlayerTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo m_GameNetworkManager_get_Instance = typeof(GameNetworkManager).GetMethod("get_Instance", []);
            FieldInfo f_GameNetworkManager_localPlayerController = typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.localPlayerController));

            MethodInfo m_Object_op_Inequality = typeof(Object).GetMethod("op_Inequality", [typeof(Object), typeof(Object)]);

            var instructionsList = instructions.ToList();

            var checkForLocalPlayer = instructionsList.FindIndexOfSequence(
                [
                    insn => insn.Calls(m_GameNetworkManager_get_Instance),
                    insn => insn.LoadsField(f_GameNetworkManager_localPlayerController),
                    insn => insn.opcode == OpCodes.Ldarg_0,
                    insn => insn.Calls(m_Object_op_Inequality),
                    insn => insn.opcode == OpCodes.Brfalse_S || insn.opcode == OpCodes.Brfalse,
                ]);
            var isLocalPlayerLabel = (Label)instructionsList[checkForLocalPlayer.End - 1].operand;
            var isLocalPlayerIndex = instructionsList.FindIndex(checkForLocalPlayer.End, insn => insn.labels.Contains(isLocalPlayerLabel));

            var isNotLocalPlayerJump = instructionsList.FindLastIndex(isLocalPlayerIndex, insn => insn.opcode == OpCodes.Br_S || insn.opcode == OpCodes.Br);
            var isNotLocalPlayerLabel = (Label)instructionsList[isNotLocalPlayerJump].operand;

            instructionsList.RemoveRange(checkForLocalPlayer);
            instructionsList.RemoveAt(isNotLocalPlayerJump - checkForLocalPlayer.Size);

            var afterCosmeticsSpawned = instructionsList.FindIndex(checkForLocalPlayer.Start, insn => insn.labels.Contains(isNotLocalPlayerLabel));
            instructionsList.InsertRange(afterCosmeticsSpawned,
                [
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Call, typeof(PatchUnlockableSuit).GetMethod(nameof(AfterCosmeticsSpawned), BindingFlags.NonPublic | BindingFlags.Static, [typeof(PlayerControllerB)])),
                ]);

            Plugin.Instance.Logger.LogInfo("Patched UnlockableSuit to spawn cosmetics for both perspectives on all players.");
            return instructionsList;
        }

        private static void AfterCosmeticsSpawned(PlayerControllerB player)
        {
            var firstPersonLayer = ViewPerspective.ENEMIES_NOT_RENDERED_LAYER;
            var thirdPersonLayer = ViewPerspective.DEFAULT_LAYER;

            if (player == GameNetworkManager.Instance.localPlayerController)
                (firstPersonLayer, thirdPersonLayer) = (thirdPersonLayer, firstPersonLayer);

            foreach (var cosmeticObject in Cosmetics.CollectVanillaFirstPersonCosmetics(player))
                cosmeticObject.gameObject.layer = firstPersonLayer;
            foreach (var cosmeticObject in Cosmetics.CollectVanillaThirdPersonCosmetics(player))
                cosmeticObject.gameObject.layer = thirdPersonLayer;

            BodyCamComponent.MarkTargetDirtyUntilRenderForAllBodyCams(player.transform);
        }
    }
}
