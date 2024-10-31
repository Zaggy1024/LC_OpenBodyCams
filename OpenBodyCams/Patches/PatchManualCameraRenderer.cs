using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

using OpenBodyCams.Compatibility;
using OpenBodyCams.Components;
using OpenBodyCams.Utilities;
using OpenBodyCams.Utilities.IL;

namespace OpenBodyCams.Patches;

[HarmonyPatch(typeof(ManualCameraRenderer))]
internal static class PatchManualCameraRenderer
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(ManualCameraRenderer.updateMapTarget))]
    private static IEnumerator updateMapTargetPostfix(IEnumerator result, ManualCameraRenderer __instance)
    {
        SyncBodyCamToRadarMap.StartTargetTransitionForMap(__instance);

        while (result.MoveNext())
            yield return result.Current;

        SyncBodyCamToRadarMap.UpdateBodyCamTargetForMap(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(ManualCameraRenderer.SwitchScreenOn))]
    [HarmonyAfter(ModGUIDs.GeneralImprovements)]
    private static void SwitchScreenOnPostfix(ManualCameraRenderer __instance)
    {
        SyncBodyCamToRadarMap.UpdateBodyCamTargetForMap(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(ManualCameraRenderer.MeetsCameraEnabledConditions))]
    private static void MeetsCameraEnabledConditionsPostfix(ManualCameraRenderer __instance, ref bool __result, PlayerControllerB player)
    {
        if ((object)__instance == ShipObjects.CameraReplacedByBodyCam && __result && !ShipObjects.MainBodyCam.IsBlanked)
            __result = false;

        // The internal ship camera makes the monitors visible, and since it renders while the monitors are visible, it nevers tops rendering.
        // This causes the map screen to by extension to never become invisible while the player is in the ship.

        // To counteract this, implement a manual frustum test for the internal ship camera, excluding itself to prevent the loop.
        if ((object)__instance == ShipObjects.InternalCameraRenderer)
        {
            if (!__result)
                return;

            __result = __instance.mesh.IsVisibleToAnyCameraExcept(__instance.cam);
            return;
        }

        // By doing the above, we also cause the map screen to stop rendering when entering the terminal. Therefore, we need to test whether
        // the terminal is in use and enable it if so.
        if ((object)__instance == StartOfRound.Instance.mapScreen)
        {
            if (__result)
                return;

            if (ShipObjects.TwoRadarCamsPresent)
                return;

            __result = ShipObjects.TerminalScript.terminalUIScreen.isActiveAndEnabled;
            return;
        }

        // The door screen also relies on this bug, so we need to test whether it is visible and enable it if so.
        if ((object)__instance == ShipObjects.ExternalCameraRenderer)
        {
            if (!ShipObjects.DoorScreenUsesExternalCamera)
                return;
            if (__result)
                return;
            if (!ShipObjects.DoorScreenRenderer.isVisible)
                return;
            if (!player.isInHangarShipRoom && StartOfRound.Instance.shipDoorsEnabled)
                return;

            __result = ShipObjects.DoorScreenRenderer.IsVisibleToAnyCameraExcept(ShipObjects.InternalCameraRenderer.cam);
            return;
        }
    }

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(ManualCameraRenderer.AddTransformAsTargetToRadar))]
    private static IEnumerable<CodeInstruction> AddTransformAsTargetToRadarTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        var transformArg = method.GetFirstParameterIndexOfType(typeof(Transform)) + 1;
        return new ILInjector(instructions).GoToEnd()
            .ReverseFind([
                ILMatcher.Opcode(OpCodes.Ret),
            ])
            .Insert([
                InstructionUtilities.MakeLdarg(transformArg),
                new(OpCodes.Ldnull),
                new(OpCodes.Call, typeof(ReverbTriggerTracker).GetMethod(nameof(ReverbTriggerTracker.AddTrackersToTarget), BindingFlags.NonPublic | BindingFlags.Static, [typeof(Transform), typeof(AudioReverbTrigger)])),
            ])
            .ReleaseInstructions();
    }

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(ManualCameraRenderer.RemoveTargetFromRadar))]
    private static IEnumerable<CodeInstruction> RemoveTargetFromRadarTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        // RemoveTargetFromRadar is invoked by RadarBoosterItem, which in turn invokes updateMapTarget it as if it was called from
        // an RPC handler, so it skips checking if the target index is valid. This means that the radar target index can point to a
        // player object that hasn't been taken control of, so the body cam target is invalid.

        // We fix this by only setting the target on the owner and allow that to sync the correct target.

        // + if (IsOwner)
        // -     SwitchRadarTargetForward(callRPC: false);
        // +     SwitchRadarTargetForward(callRPC: true);
        var injector = new ILInjector(instructions)
            .Find([
                ILMatcher.Ldarg(0),
                ILMatcher.Ldc(0),
                ILMatcher.Call(typeof(ManualCameraRenderer).GetMethod(nameof(ManualCameraRenderer.SwitchRadarTargetForward), [typeof(bool)])),
            ]);

        if (!injector.IsValid)
        {
            Plugin.Instance.Logger.LogError($"Failed to find call to SwitchRadarTargetForward\n{new StackTrace()}");
            return instructions;
        }

        var notOwnerLabel = generator.DefineLabel();
        injector.GetRelativeInstruction(1).opcode = OpCodes.Ldc_I4_1;
        return injector
            .Insert([
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, typeof(NetworkBehaviour).GetMethod("get_IsOwner")),
                new(OpCodes.Brfalse, notOwnerLabel),
            ])
            .GoToMatchEnd()
            .AddLabel(notOwnerLabel)
            .ReleaseInstructions();
    }
}
