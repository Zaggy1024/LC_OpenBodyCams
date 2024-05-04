using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;

using OpenBodyCams.Compatibility;
using OpenBodyCams.Utilities;

namespace OpenBodyCams.Patches
{
    [HarmonyPatch(typeof(ManualCameraRenderer))]
    internal static class PatchManualCameraRenderer
    {
        [HarmonyPostfix]
        [HarmonyPatch("updateMapTarget")]
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
        [HarmonyPatch("MeetsCameraEnabledConditions")]
        private static void MeetsCameraEnabledConditionsPostfix(ManualCameraRenderer __instance, ref bool __result, PlayerControllerB player)
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

                __result = __instance.mesh.IsVisibleToAnyCameraExcept(__instance.cam);
            }
            else if ((object)__instance == ShipObjects.ExternalCameraRenderer)
            {
                if (!ShipObjects.DoorScreenUsesExternalCamera)
                    return;
                if (__result)
                    return;
                if (!player.isInHangarShipRoom && StartOfRound.Instance.shipDoorsEnabled)
                    return;

                __result = ShipObjects.DoorScreenRenderer.IsVisibleToAnyCameraExcept(__instance.cam);
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(ManualCameraRenderer.RemoveTargetFromRadar))]
        private static IEnumerable<CodeInstruction> RemoveTargetFromRadarTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            // RemoveTargetFromRadar is invoked by RadarBoosterItem, which in turn invokes updateMapTarget it as if it was called from
            // an RPC handler, so it skips checking if the target index is valid. This means that the radar target index can point to a
            // player object that hasn't been taken control of, so the body cam target is invalid.

            // We fix this by only setting the target on the owner and allow that to sync the correct target.
            var m_ManualCameraRenderer_SwitchRadarTargetForward = typeof(ManualCameraRenderer).GetMethod(nameof(ManualCameraRenderer.SwitchRadarTargetForward), [ typeof(bool) ]);

            var m_NetworkBehaviour_IsOwner = typeof(NetworkBehaviour).GetMethod("get_IsOwner");

            var instructionsList = instructions.ToList();

            // SwitchRadarTargetForward(callRPC: false);
            var switchRadarTargetForward = instructionsList.FindIndexOfSequence(
                [
                    insn => insn.IsLdarg(0),
                    insn => insn.LoadsConstant(0),
                    insn => insn.Calls(m_ManualCameraRenderer_SwitchRadarTargetForward),
                ]);
            // if (IsOwner)
            //   SwitchRadarTargetForward(callRPC: true);
            instructionsList[switchRadarTargetForward.Start + 1] = new(OpCodes.Ldc_I4_1);
            var notOwnerLabel = generator.DefineLabel();
            instructionsList[switchRadarTargetForward.End].labels.Add(notOwnerLabel);
            instructionsList.InsertRange(switchRadarTargetForward.Start,
                [
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Call, m_NetworkBehaviour_IsOwner),
                    new(OpCodes.Brfalse_S, notOwnerLabel),
                ]);

            return instructionsList;
        }
    }
}
