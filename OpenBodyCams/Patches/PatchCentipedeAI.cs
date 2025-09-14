using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using GameNetcodeStuff;
using HarmonyLib;

using OpenBodyCams.Utilities;
using OpenBodyCams.Utilities.IL;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(CentipedeAI))]
internal class PatchCentipedeAI
{
    internal static HashSet<CentipedeAI>[] CentipedesAttachedToPlayers;
    internal static bool HasWarnedClingingMismatch = false;

    public static void SetClingingAnimationPositionsForPlayer(PlayerControllerB player, Perspective perspective)
    {
        if (CentipedesAttachedToPlayers == null)
            return;
        if (player.playerClientId < 0 || (int)player.playerClientId >= CentipedesAttachedToPlayers.Length)
            return;

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

    [HarmonyPrefix]
    [HarmonyPatch(nameof(CentipedeAI.ClingToPlayer))]
    private static void ClingToPlayerPrefix(CentipedeAI __instance, PlayerControllerB playerScript)
    {
        CentipedeStartedClingingToPlayer(__instance, playerScript);
    }

    internal static void CentipedeStartedClingingToPlayer(CentipedeAI centipede, PlayerControllerB player)
    {
        EnsureCentipedesAttachedToPlayersArrayIsCorrectSize();
        if (centipede.clingingToPlayer != null)
            CentipedesAttachedToPlayers[centipede.clingingToPlayer.playerClientId].Remove(centipede);
        CentipedesAttachedToPlayers[player.playerClientId].Add(centipede);
    }

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(CentipedeAI.UnclingFromPlayer), MethodType.Enumerator)]
    private static IEnumerable<CodeInstruction> UnclingFromPlayerTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var injector = new ILInjector(instructions);

        injector
            .Find([
                ILMatcher.Opcode(OpCodes.Ldnull),
                ILMatcher.Stfld(typeof(CentipedeAI).GetField(nameof(CentipedeAI.clingingToPlayer))),
            ]);

        if (!injector.IsValid)
        {
            Plugin.Instance.Logger.LogError($"Failed to patch CentipedeAI.UnclingFromPlayer()");
            return instructions;
        }

        return injector
            .Insert([
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Call, typeof(PatchCentipedeAI).GetMethod(nameof(CentipedeStoppedClingingToPlayer), BindingFlags.NonPublic | BindingFlags.Static, [typeof(CentipedeAI)])),
            ])
            .ReleaseInstructions();
    }

    internal static void CentipedeStoppedClingingToPlayer(CentipedeAI centipede)
    {
        EnsureCentipedesAttachedToPlayersArrayIsCorrectSize();
        if (centipede.clingingToPlayer != null)
            CentipedesAttachedToPlayers[centipede.clingingToPlayer.playerClientId].Remove(centipede);
    }
}
