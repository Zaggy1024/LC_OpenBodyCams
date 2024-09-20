using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace OpenBodyCams.Patches;

internal static class PatchFixItemDropping
{
    private readonly static MethodInfo m_PlayerControllerB_SetObjectAsNoLongerHeld = typeof(PlayerControllerB).GetMethod(nameof(PlayerControllerB.SetObjectAsNoLongerHeld), [ typeof(bool), typeof(bool), typeof(Vector3), typeof(GrabbableObject), typeof(int) ]);

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlayerControllerB), "ThrowObjectClientRpc")]
    private static IEnumerable<CodeInstruction> ThrowObjectClientRpcTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        if (!Plugin.FixDroppedItemRotation.Value)
            return instructions;

        var instructionsList = instructions.ToList();

        var yRotArg = Array.FindIndex(method.GetParameters(), p => p.Name == "floorYRot") + 1;
        if (yRotArg < 1)
        {
            Plugin.Instance.Logger.LogWarning("Dropped item patch transpiler failed to find the floorYRot argument.");
            return instructions;
        }
        var stopHolding = instructionsList.FindIndex(insn => insn.Calls(m_PlayerControllerB_SetObjectAsNoLongerHeld));
        if (stopHolding == -1)
        {
            Plugin.Instance.Logger.LogWarning("Dropped item patch transpiler failed to find the call to SetObjectAsNoLongerHeld().");
            return instructions;
        }
        var dropRotation = instructionsList.InstructionRangeForStackItems(stopHolding, 0, 0);
        if (dropRotation is null)
        {
            Plugin.Instance.Logger.LogWarning("Dropped item patch transpiler failed to get the instructions pushing the floorYRot value.");
            return instructions;
        }

        instructionsList.RemoveRange(dropRotation.Start, dropRotation.Size);
        instructionsList.Insert(dropRotation.Start, new CodeInstruction(OpCodes.Ldarg, yRotArg));

        return instructionsList;
    }
}
