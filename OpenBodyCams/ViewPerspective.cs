﻿using UnityEngine.Rendering;
using UnityEngine;
using GameNetcodeStuff;

namespace OpenBodyCams
{
    internal enum Perspective
    {
        FirstPerson,
        ThirdPerson,
    }

    internal static class ViewPerspective
    {
        public const int DEFAULT_LAYER = 0;
        public const int ENEMIES_LAYER = 19;
        public const int ENEMIES_NOT_RENDERED_LAYER = 23;

        private static void SetCosmeticHidden(GameObject cosmetic, bool hidden)
        {
            cosmetic.layer = hidden ? ENEMIES_NOT_RENDERED_LAYER : DEFAULT_LAYER;
        }

        internal static void Apply(PlayerControllerB player, ref GameObject[] cosmetics, ref PlayerModelState state, Perspective perspective)
        {
            if (player is null)
                return;

            // Save
            state.bodyShadowMode = player.thisPlayerModel.shadowCastingMode;
            state.bodyLayer = player.thisPlayerModel.gameObject.layer;

            state.armsEnabled = player.thisPlayerModelArms.enabled;
            state.armsLayer = player.thisPlayerModelArms.gameObject.layer;

            if (player.currentlyHeldObjectServer != null)
            {
                state.heldItemPosition = player.currentlyHeldObjectServer.transform.position;
                state.heldItemRotation = player.currentlyHeldObjectServer.transform.rotation;
            }

            for (int i = 0; i < cosmetics.Length; i++)
                state.cosmeticsLayers[i] = cosmetics[i].layer;

            // Modify
            void AttachItem(GrabbableObject item, Transform holder)
            {
                item.transform.rotation = holder.rotation;
                item.transform.Rotate(item.itemProperties.rotationOffset);
                item.transform.position = holder.position + (holder.rotation * item.itemProperties.positionOffset);
            }

            switch (perspective)
            {
                case Perspective.FirstPerson:
                    player.thisPlayerModel.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    player.thisPlayerModel.gameObject.layer = ENEMIES_NOT_RENDERED_LAYER;

                    player.thisPlayerModelArms.enabled = true;
                    player.thisPlayerModelArms.gameObject.layer = DEFAULT_LAYER;

                    if (player.currentlyHeldObjectServer != null)
                        AttachItem(player.currentlyHeldObjectServer, player.localItemHolder);

                    foreach (var cosmetic in cosmetics)
                        SetCosmeticHidden(cosmetic, true);
                    break;
                case Perspective.ThirdPerson:
                    player.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On;
                    player.thisPlayerModel.gameObject.layer = DEFAULT_LAYER;

                    player.thisPlayerModelArms.enabled = false;
                    player.thisPlayerModelArms.gameObject.layer = ENEMIES_NOT_RENDERED_LAYER;

                    if (player.currentlyHeldObjectServer != null)
                        AttachItem(player.currentlyHeldObjectServer, player.serverItemHolder);

                    foreach (var cosmetic in cosmetics)
                        SetCosmeticHidden(cosmetic, false);
                    break;
            }
        }

        internal static void Restore(PlayerControllerB player, GameObject[] cosmetics, PlayerModelState state)
        {
            if (player is null)
                return;

            player.thisPlayerModel.shadowCastingMode = state.bodyShadowMode;
            player.thisPlayerModel.gameObject.layer = state.bodyLayer;

            player.thisPlayerModelArms.enabled = state.armsEnabled;
            player.thisPlayerModelArms.gameObject.layer = state.armsLayer;

            for (int i = 0; i < cosmetics.Length; i++)
                cosmetics[i].layer = state.cosmeticsLayers[i];

            if (player.currentlyHeldObjectServer != null)
            {
                player.currentlyHeldObjectServer.transform.position = state.heldItemPosition;
                player.currentlyHeldObjectServer.transform.rotation = state.heldItemRotation;
            }
        }
    }

    internal struct PlayerModelState
    {
        public ShadowCastingMode bodyShadowMode;
        public int bodyLayer;
        public bool armsEnabled;
        public int armsLayer;
        public int[] cosmeticsLayers;
        public Vector3 heldItemPosition;
        public Quaternion heldItemRotation;
    }
}
