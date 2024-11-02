using System;
using System.Diagnostics;
using System.Text;

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
            Restore(ref state);

            state.player = player;
            state.lastPerspective = Perspective.Original;

            if (player is null)
            {
                state.thirdPersonCosmetics = [];
                state.thirdPersonCosmeticsLayers = [];
                state.thirdPersonCosmeticNames = [];

                state.firstPersonCosmetics = [];
                state.firstPersonCosmeticsLayers = [];
                state.firstPersonCosmeticNames = [];
                return;
            }

            Cosmetics.CollectCosmetics(player, out state.thirdPersonCosmetics, out state.firstPersonCosmetics, out state.hasViewmodelReplacement);

            state.thirdPersonCosmeticsLayers = new int[state.thirdPersonCosmetics.Length];
            state.thirdPersonCosmeticNames = GetCosmeticsPaths(state.thirdPersonCosmetics, player.playerBodyAnimator.transform);

            state.firstPersonCosmeticsLayers = new int[state.firstPersonCosmetics.Length];
            state.firstPersonCosmeticNames = GetCosmeticsPaths(state.firstPersonCosmetics, player.playerBodyAnimator.transform);
        }

        public static void Apply(ref PlayerModelState state, Perspective perspective)
        {
            var player = state.player;

            if (player is null)
                return;

            Restore(ref state);

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
            {
                if (state.thirdPersonCosmetics[i] == null)
                {
                    ref var debugName = ref state.thirdPersonCosmeticNames[i];
                    if (debugName != null)
                    {
                        Plugin.Instance.Logger.LogWarning($"{player.playerUsername}'s third-person cosmetic {debugName} is null.\n{new StackTrace()}");
                        debugName = null;
                    }
                    continue;
                }
                state.thirdPersonCosmeticsLayers[i] = state.thirdPersonCosmetics[i].layer;
            }
            for (int i = 0; i < state.firstPersonCosmetics.Length; i++)
            {
                if (state.firstPersonCosmetics[i] == null)
                {
                    ref var debugName = ref state.firstPersonCosmeticNames[i];
                    if (debugName != null)
                    {
                        Plugin.Instance.Logger.LogWarning($"{player.playerUsername}'s first-person cosmetic {debugName} is null.\n{new StackTrace()}");
                        debugName = null;
                    }
                    continue;
                }
                state.firstPersonCosmeticsLayers[i] = state.firstPersonCosmetics[i].layer;
            }

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
                {
                    if (cosmetic == null)
                        continue;
                    SetCosmeticHidden(cosmetic, true);
                }
                foreach (var cosmetic in state.firstPersonCosmetics)
                {
                    if (cosmetic == null)
                        continue;
                    SetCosmeticHidden(cosmetic, false);
                }
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
                {
                    if (cosmetic == null)
                        continue;
                    SetCosmeticHidden(cosmetic, false);
                }
                foreach (var cosmetic in state.firstPersonCosmetics)
                {
                    if (cosmetic == null)
                        continue;
                    SetCosmeticHidden(cosmetic, true);
                }
            }

            PatchFlowerSnakeEnemy.SetClingingAnimationPositionsForPlayer(player, perspective);
            PatchCentipedeAI.SetClingingAnimationPositionsForPlayer(player, perspective);

            state.lastPerspective = perspective;
        }

        public static void Restore(ref PlayerModelState state)
        {
            var player = state.player;

            if (player is null || state.lastPerspective == Perspective.Original)
                return;

            player.thisPlayerModel.shadowCastingMode = state.thirdPersonBodyShadowMode;
            player.thisPlayerModel.gameObject.layer = state.thirdPersonBodyLayer;

            player.thisPlayerModelArms.enabled = state.firstPersonArmsEnabled;
            player.thisPlayerModelArms.gameObject.layer = state.firstPersonArmsLayer;

            for (int i = 0; i < state.thirdPersonCosmetics.Length; i++)
            {
                if (state.thirdPersonCosmetics[i] == null)
                    continue;
                state.thirdPersonCosmetics[i].layer = state.thirdPersonCosmeticsLayers[i];
            }
            for (int i = 0; i < state.firstPersonCosmetics.Length; i++)
            {
                if (state.firstPersonCosmetics[i] == null)
                    continue;
                state.firstPersonCosmetics[i].layer = state.firstPersonCosmeticsLayers[i];
            }

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

            state.lastPerspective = Perspective.Original;
        }

        private static string[] GetCosmeticsPaths(GameObject[] objects, Transform root)
        {
            var result = new string[objects.Length];

            for (var i = 0; i < objects.Length; i++)
            {
                var transform = objects[i].transform;
                var builder = new StringBuilder(transform.name);

                while (true)
                {
                    transform = transform.parent;
                    if (transform == null || transform == root)
                        break;
                    builder.Insert(0, '/');
                    builder.Insert(0, transform.name);
                    if (transform.name.EndsWith("(Clone)"))
                        break;
                }
                result[i] = builder.ToString();
            }

            return result;
        }
    }

    public struct PlayerModelState()
    {
        internal PlayerControllerB player;

        internal Perspective lastPerspective = Perspective.Original;

        internal ShadowCastingMode thirdPersonBodyShadowMode;
        internal int thirdPersonBodyLayer;

        internal bool firstPersonArmsEnabled;
        internal int firstPersonArmsLayer;

        internal GameObject[] thirdPersonCosmetics = [];
        internal int[] thirdPersonCosmeticsLayers = [];
        internal string[] thirdPersonCosmeticNames = [];

        internal GameObject[] firstPersonCosmetics = [];
        internal int[] firstPersonCosmeticsLayers = [];
        internal string[] firstPersonCosmeticNames = [];

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
            if (player == null)
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
            if (player == null)
                return false;

            if (Array.IndexOf(thirdPersonCosmetics, obj) != -1)
                return true;
            if (Array.IndexOf(firstPersonCosmetics, obj) != -1)
                return true;
            return false;
        }
    }
}
