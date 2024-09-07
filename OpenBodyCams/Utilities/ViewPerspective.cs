using System;

using UnityEngine.Rendering;
using UnityEngine;
using GameNetcodeStuff;

using OpenBodyCams.Patches;

namespace OpenBodyCams.Utilities
{
    public enum Perspective
    {
        Original,
        FirstPerson,
        ThirdPerson,
    }

    public static class ViewPerspective
    {
        public const int DEFAULT_LAYER = 0;
        public const int ENEMIES_LAYER = 19;
        public const int ENEMIES_NOT_RENDERED_LAYER = 23;

        private static void SetCosmeticHidden(GameObject cosmetic, bool hidden)
        {
            cosmetic.layer = hidden ? ENEMIES_NOT_RENDERED_LAYER : DEFAULT_LAYER;
        }

        public static void PrepareModelState(PlayerControllerB player, ref PlayerModelState state)
        {
            if (player is null)
            {
                state.thirdPersonCosmetics = [];
                state.thirdPersonCosmeticsLayers = [];

                state.firstPersonCosmetics = [];
                state.firstPersonCosmeticsLayers = [];
                return;
            }

            Cosmetics.CollectCosmetics(player, out state.thirdPersonCosmetics, out state.firstPersonCosmetics, out state.hasViewmodelReplacement);
            state.thirdPersonCosmeticsLayers = new int[state.thirdPersonCosmetics.Length];
            state.firstPersonCosmeticsLayers = new int[state.firstPersonCosmetics.Length];
        }

        public static void Apply(PlayerControllerB player, ref PlayerModelState state, Perspective perspective)
        {
            if (player is null)
                return;

            if (state.isValid)
                Restore(player, state);

            if (perspective == Perspective.Original)
                return;

            // Save
            state.thirdPersonBodyShadowMode = player.thisPlayerModel.shadowCastingMode;
            state.thirdPersonBodyLayer = player.thisPlayerModel.gameObject.layer;

            state.firstPersonArmsEnabled = player.thisPlayerModelArms.enabled;
            state.firstPersonArmsLayer = player.thisPlayerModelArms.gameObject.layer;

            if (player.currentlyHeldObjectServer != null)
            {
                state.heldItemPosition = player.currentlyHeldObjectServer.transform.position;
                state.heldItemRotation = player.currentlyHeldObjectServer.transform.rotation;

                if (player.currentlyHeldObjectServer is FlashlightItem flashlight)
                {
                    state.helmetLightEnabled = player.helmetLight.enabled;
                    state.heldLightEnabled = flashlight.flashlightBulb.enabled;
                }
            }

            for (int i = 0; i < state.thirdPersonCosmetics.Length; i++)
                state.thirdPersonCosmeticsLayers[i] = state.thirdPersonCosmetics[i].layer;
            for (int i = 0; i < state.firstPersonCosmetics.Length; i++)
                state.firstPersonCosmeticsLayers[i] = state.firstPersonCosmetics[i].layer;

            // Modify
            static void AttachItem(GrabbableObject item, Transform holder)
            {
                item.transform.rotation = holder.rotation;
                item.transform.Rotate(item.itemProperties.rotationOffset);
                item.transform.position = holder.position + holder.rotation * item.itemProperties.positionOffset;
            }

            if (perspective == Perspective.FirstPerson)
            {
                player.thisPlayerModel.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                player.thisPlayerModel.gameObject.layer = ENEMIES_NOT_RENDERED_LAYER;

                if (!state.hasViewmodelReplacement)
                {
                    player.thisPlayerModelArms.enabled = true;
                    player.thisPlayerModelArms.gameObject.layer = DEFAULT_LAYER;
                }

                if (player.currentlyHeldObjectServer != null)
                {
                    AttachItem(player.currentlyHeldObjectServer, player.localItemHolder);

                    if (player.currentlyHeldObjectServer is FlashlightItem flashlight)
                    {
                        player.helmetLight.enabled = false;
                        flashlight.flashlightBulb.enabled = flashlight.isBeingUsed;
                        flashlight.flashlightBulbGlow.enabled = flashlight.isBeingUsed;
                    }
                }

                foreach (var cosmetic in state.thirdPersonCosmetics)
                    SetCosmeticHidden(cosmetic, true);
                foreach (var cosmetic in state.firstPersonCosmetics)
                    SetCosmeticHidden(cosmetic, false);
            }
            else if (perspective == Perspective.ThirdPerson)
            {
                player.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On;
                player.thisPlayerModel.gameObject.layer = DEFAULT_LAYER;

                if (!state.hasViewmodelReplacement)
                {
                    player.thisPlayerModelArms.enabled = false;
                    player.thisPlayerModelArms.gameObject.layer = ENEMIES_NOT_RENDERED_LAYER;
                }

                if (player.currentlyHeldObjectServer != null)
                {
                    AttachItem(player.currentlyHeldObjectServer, player.serverItemHolder);

                    if (player.currentlyHeldObjectServer is FlashlightItem flashlight)
                    {
                        player.helmetLight.enabled = flashlight.isBeingUsed;
                        flashlight.flashlightBulb.enabled = false;
                        flashlight.flashlightBulbGlow.enabled = false;
                    }
                }

                foreach (var cosmetic in state.thirdPersonCosmetics)
                    SetCosmeticHidden(cosmetic, false);
                foreach (var cosmetic in state.firstPersonCosmetics)
                    SetCosmeticHidden(cosmetic, true);
            }

            PatchFlowerSnakeEnemy.SetClingingAnimationPositionsForPlayer(player, perspective);
            PatchCentipedeAI.SetClingingAnimationPositionsForPlayer(player, perspective);
        }

        public static void Restore(PlayerControllerB player, PlayerModelState state)
        {
            if (player is null)
                return;

            player.thisPlayerModel.shadowCastingMode = state.thirdPersonBodyShadowMode;
            player.thisPlayerModel.gameObject.layer = state.thirdPersonBodyLayer;

            player.thisPlayerModelArms.enabled = state.firstPersonArmsEnabled;
            player.thisPlayerModelArms.gameObject.layer = state.firstPersonArmsLayer;

            for (int i = 0; i < state.thirdPersonCosmetics.Length; i++)
                state.thirdPersonCosmetics[i].layer = state.thirdPersonCosmeticsLayers[i];

            for (int i = 0; i < state.firstPersonCosmetics.Length; i++)
                state.firstPersonCosmetics[i].layer = state.firstPersonCosmeticsLayers[i];

            if (player.currentlyHeldObjectServer != null)
            {
                player.currentlyHeldObjectServer.transform.position = state.heldItemPosition;
                player.currentlyHeldObjectServer.transform.rotation = state.heldItemRotation;

                if (player.currentlyHeldObjectServer is FlashlightItem flashlight)
                {
                    player.helmetLight.enabled = state.helmetLightEnabled;
                    flashlight.flashlightBulb.enabled = state.heldLightEnabled;
                    flashlight.flashlightBulbGlow.enabled = state.heldLightEnabled;
                }
            }

            PatchFlowerSnakeEnemy.SetClingingAnimationPositionsForPlayer(player, Perspective.Original);
            PatchCentipedeAI.SetClingingAnimationPositionsForPlayer(player, Perspective.Original);
        }
    }

    public struct PlayerModelState()
    {
        internal bool isValid = false;

        internal ShadowCastingMode thirdPersonBodyShadowMode;
        internal int thirdPersonBodyLayer;

        internal bool firstPersonArmsEnabled;
        internal int firstPersonArmsLayer;

        internal GameObject[] thirdPersonCosmetics = [];
        internal int[] thirdPersonCosmeticsLayers = [];

        internal GameObject[] firstPersonCosmetics = [];
        internal int[] firstPersonCosmeticsLayers = [];

        internal bool hasViewmodelReplacement = false;

        internal Vector3 heldItemPosition;
        internal Quaternion heldItemRotation;

        internal bool helmetLightEnabled;
        internal bool heldLightEnabled;

        private static bool AllObjectsExistInArray(GameObject[] objects)
        {
            foreach (var obj in objects)
            {
                if (obj == null)
                    return false;
            }
            return true;
        }

        internal readonly bool VerifyCosmeticsExist(string name)
        {
            if (!isValid)
                return true;

            if (!AllObjectsExistInArray(thirdPersonCosmetics))
            {
                Plugin.Instance.Logger.LogError($"A third-person cosmetic attached to {name} has been destroyed.");
                return false;
            }
            if (!AllObjectsExistInArray(firstPersonCosmetics))
            {
                Plugin.Instance.Logger.LogError($"A first-person cosmetic attached to {name} has been destroyed.");
                return false;
            }

            return true;
        }

        internal readonly bool ReferencesObject(GameObject obj)
        {
            if (!isValid)
                return false;

            if (Array.IndexOf(thirdPersonCosmetics, obj) != -1)
                return true;
            if (Array.IndexOf(firstPersonCosmetics, obj) != -1)
                return true;
            return false;
        }
    }
}
