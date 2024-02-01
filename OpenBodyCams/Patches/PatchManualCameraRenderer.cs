using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using GameNetcodeStuff;
using HarmonyLib;
using OpenBodyCams.Compatibility;
using Unity.Netcode;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(ManualCameraRenderer))]
    internal class PatchManualCameraRenderer
    {
        [HarmonyPostfix]
        [HarmonyPatch("updateMapTarget")]
        static IEnumerator updateMapTargetPostfix(IEnumerator result, ManualCameraRenderer __instance)
        {
            if (__instance == StartOfRound.Instance.mapScreen)
                SyncBodyCamToRadarMap.StartTargetTransitionForMap(__instance);

            while (result.MoveNext())
                yield return result.Current;

            if (__instance != StartOfRound.Instance.mapScreen)
                yield break;

            SyncBodyCamToRadarMap.UpdateBodyCamTargetForMap(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ManualCameraRenderer.SwitchScreenOn))]
        [HarmonyAfter(ModGUIDs.GeneralImprovements)]
        static void SwitchScreenOnPostfix(ManualCameraRenderer __instance)
        {
            if ((object)__instance.cam != __instance.mapCamera)
                return;
            SyncBodyCamToRadarMap.UpdateBodyCamTargetForMap(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("MeetsCameraEnabledConditions")]
        static void MeetsCameraEnabledConditionsPostfix(ManualCameraRenderer __instance, ref bool __result, PlayerControllerB player)
        {
            if ((object)__instance == StartOfRound.Instance.mapScreen)
            {
                if (__result)
                    return;

                if (ShipObjects.TwoRadarCamsPresent)
                    return;

                if (ShipObjects.TerminalScript.terminalUIScreen.isActiveAndEnabled)
                    __result = true;
            }
            else if ((object)__instance == ShipObjects.ShipCameraRenderer)
            {
                if (!__result)
                    return;

                __result = Utilities.IsRendererVisibleToAnyCameraExcept(__instance.mesh, __instance.cam);
            }
            else if ((object)__instance == ShipObjects.ExternalCameraRenderer)
            {
                if (!ShipObjects.DoorScreenUsesExternalCamera)
                    return;
                if (__result)
                    return;
                if (!player.isInHangarShipRoom && StartOfRound.Instance.shipDoorsEnabled)
                    return;

                __result = Utilities.IsRendererVisibleToAnyCameraExcept(ShipObjects.DoorScreenRenderer, __instance.cam, true);
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(ManualCameraRenderer.RemoveTargetFromRadar))]
        static IEnumerable<CodeInstruction> RemoveTargetFromRadarTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            if (ShipObjects.TwoRadarCamsPresent)
                return instructions;

            // RemoveTargetFromRadar is invoked by RadarBoosterItem, which in turn invokes updateMapTarget it as if it was called from
            // an RPC handler, so it skips checking if the target index is valid. This means that the radar target index can point to a
            // player object that hasn't been taken control of, so the body cam target is invalid.

            // We fix this by only setting the target on the owner and allow that to sync the correct target.
            var m_ManualCameraRenderer_SwitchRadarTargetForward = typeof(ManualCameraRenderer).GetMethod(nameof(ManualCameraRenderer.SwitchRadarTargetForward), new Type[] { typeof(bool) });

            var m_NetworkBehaviour_IsOwner = typeof(NetworkBehaviour).GetMethod("get_IsOwner");

            var instructionsList = instructions.ToList();

            // SwitchRadarTargetForward(callRPC: false);
            var switchRadarTargetForward = instructionsList.FindIndexOfSequence(new Predicate<CodeInstruction>[]
            {
                insn => insn.IsLdarg(0),
                insn => insn.LoadsConstant(0),
                insn => insn.Calls(m_ManualCameraRenderer_SwitchRadarTargetForward),
            });
            // if (IsOwner)
            //   SwitchRadarTargetForward(callRPC: true);
            instructionsList[switchRadarTargetForward.Start + 1] = new CodeInstruction(OpCodes.Ldc_I4_1);
            var notOwnerLabel = generator.DefineLabel();
            instructionsList[switchRadarTargetForward.End].labels.Add(notOwnerLabel);
            instructionsList.InsertRange(switchRadarTargetForward.Start, new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, m_NetworkBehaviour_IsOwner),
                new CodeInstruction(OpCodes.Brfalse_S, notOwnerLabel),
            });

            return instructionsList;
        }
    }
}
