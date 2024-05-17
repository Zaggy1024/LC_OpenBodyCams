using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using GameNetcodeStuff;
using HarmonyLib;

using OpenBodyCams.Utilities;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(CentipedeAI))]
    internal class PatchCentipedeAI
    {
        internal static HashSet<CentipedeAI>[] CentipedesAttachedToPlayers;
        internal static bool HasWarnedClingingMismatch = false;

        public static void SetClingingAnimationPositionsForPlayer(PlayerControllerB player, Perspective perspective)
        {
            if (CentipedesAttachedToPlayers != null)
            {
                foreach (var clingingCentipede in CentipedesAttachedToPlayers[player.playerClientId])
                {
                    if (clingingCentipede == null)
                        continue;
                    if (clingingCentipede.isEnemyDead)
                        continue;
                    if (clingingCentipede.clingingToDeadBody)
                        continue;
                    if (clingingCentipede.clingingToPlayer == null)
                    {
                        if (!HasWarnedClingingMismatch)
                        {
                            Plugin.Instance.Logger.LogWarning($"{clingingCentipede} should be clinging to a player according to our hooks, but it is not.");
                            HasWarnedClingingMismatch = true;
                        }
                        continue;
                    }

                    var originallyClingedToLocalPlayer = clingingCentipede.clingingToLocalClient;

                    switch (perspective)
                    {
                        case Perspective.Original:
                            break;
                        case Perspective.FirstPerson:
                            clingingCentipede.clingingToLocalClient = true;
                            break;
                        case Perspective.ThirdPerson:
                            clingingCentipede.clingingToLocalClient = false;
                            break;
                    }

                    clingingCentipede.UpdatePositionToClingingPlayerHead();
                    clingingCentipede.clingingToLocalClient = originallyClingedToLocalPlayer;
                }
            }
        }

        private static void EnsureCentipedesAttachedToPlayersArrayIsCorrectSize()
        {
            var playerCount = StartOfRound.Instance.allPlayerScripts.Length;
            if (CentipedesAttachedToPlayers == null)
                CentipedesAttachedToPlayers = new HashSet<CentipedeAI>[playerCount];
            else if (CentipedesAttachedToPlayers.Length != playerCount)
                Array.Resize(ref CentipedesAttachedToPlayers, playerCount);
            else
                return;

            for (var i = 0; i < playerCount; i++)
            {
                if (CentipedesAttachedToPlayers[i] == null)
                    CentipedesAttachedToPlayers[i] = [];
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(CentipedeAI.ClingToPlayer))]
        private static void ClingToPlayerPostfix(CentipedeAI __instance, PlayerControllerB playerScript)
        {
            EnsureCentipedesAttachedToPlayersArrayIsCorrectSize();
            if (__instance.clingingToPlayer != null)
                CentipedesAttachedToPlayers[__instance.clingingToPlayer.playerClientId].Remove(__instance);
            CentipedesAttachedToPlayers[playerScript.playerClientId].Add(__instance);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(CentipedeAI.UnclingFromPlayer), MethodType.Enumerator)]
        private static IEnumerable<CodeInstruction> UnclingFromPlayerTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var clingedToPlayer = typeof(CentipedeAI).GetField(nameof(CentipedeAI.clingingToPlayer));

            var instructionsList = instructions.ToList();

            var clearClingedToPlayer = instructionsList.FindIndexOfSequence(
                [
                    insn => insn.opcode == OpCodes.Ldnull,
                    insn => insn.StoresField(clingedToPlayer),
                ]);
            instructionsList.InsertRange(clearClingedToPlayer.Start,
                [
                    new CodeInstruction(OpCodes.Ldloc_1),
                    new CodeInstruction(OpCodes.Call, typeof(PatchCentipedeAI).GetMethod(nameof(CentipedeStoppedClingingToPlayer), BindingFlags.NonPublic | BindingFlags.Static, [typeof(CentipedeAI)])),
                ]);

            return instructionsList;
        }

        internal static void CentipedeStoppedClingingToPlayer(CentipedeAI centipede)
        {
            EnsureCentipedesAttachedToPlayersArrayIsCorrectSize();
            if (centipede.clingingToPlayer != null)
                CentipedesAttachedToPlayers[centipede.clingingToPlayer.playerClientId].Remove(centipede);
        }
    }
}
