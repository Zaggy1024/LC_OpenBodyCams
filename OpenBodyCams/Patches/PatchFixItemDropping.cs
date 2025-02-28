using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using OpenBodyCams.Utilities.IL;

namespace OpenBodyCams.Patches;

internal static class PatchFixItemDropping
{
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ThrowObjectClientRpc))]
    private static IEnumerable<CodeInstruction> ThrowObjectClientRpcTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        if (!Plugin.FixDroppedItemRotation.Value)
            return instructions;

        var yRotArg = Array.FindIndex(method.GetParameters(), p => p.Name == "floorYRot") + 1;
        if (yRotArg < 1)
        {
            Plugin.Instance.Logger.LogWarning("Dropped item patch transpiler failed to find the floorYRot argument.");
            return instructions;
        }

        var injector = new ILInjector(instructions)
            .Find([
                ILMatcher.Call(typeof(PlayerControllerB).GetMethod(nameof(PlayerControllerB.SetObjectAsNoLongerHeld), [typeof(bool), typeof(bool), typeof(Vector3), typeof(GrabbableObject), typeof(int)]))
            ])
            .GoToPush(1);

        if (!injector.IsValid)
        {
            Plugin.Instance.Logger.LogWarning("Dropped item patch transpiler failed to get the instructions pushing the floorYRot value.");
            return instructions;
        }

        return injector
            .ReplaceLastMatch(new CodeInstruction(OpCodes.Ldarg, yRotArg))
            .ReleaseInstructions();
    }
}
