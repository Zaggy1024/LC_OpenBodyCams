using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using OpenBodyCams.Utilities;

namespace OpenBodyCams.Components;

internal class TargetTracker : MonoBehaviour
{
    internal class TargetInfo
    {
        internal Transform root;
        internal AudioReverbTrigger lastTrigger;
        internal BitArray enabledWeathers = new(0);

        internal TargetInfo(Transform root, TargetInfo copyFrom = null)
        {
            this.root = root;

            if (copyFrom != null)
            {
                lastTrigger = copyFrom.lastTrigger;
                enabledWeathers = new BitArray(copyFrom.enabledWeathers);
            }
        }
    }

    private static readonly int triggersLayer = LayerMask.NameToLayer("Triggers");
    private static readonly int triggersExclusionMask = ~LayerMask.GetMask("Triggers", "InteractableObject");

    private static readonly Material colliderMaterial = new(Shader.Find("HDRP/Lit"));

    private static readonly Dictionary<Transform, TargetInfo> targetReverbTriggers = [];

    internal static bool CanCloneCollider(Collider collider)
    {
        if (collider is BoxCollider)
            return true;
        return false;
    }

    internal static GameObject CreateColliderObjectAsChildOf(Collider collider)
    {
        const string name = $"OpenBodyCams_{nameof(TargetTracker)}";
        const float minHeight = 2.5f;

        if (collider is BoxCollider boxCollider)
        {
            var size = boxCollider.size;
            if (size.y < minHeight)
                size.y = minHeight;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;

            cube.transform.SetParent(collider.transform, false);
            cube.transform.localPosition = boxCollider.center;
            cube.transform.localScale = boxCollider.size;

            return cube;
        }

        return null;
    }

    internal static void AddTrackersToTarget(Transform target, Transform copyFrom = null)
    {
        if (targetReverbTriggers.ContainsKey(target))
            return;

        var originalCollider = target.GetComponentInChildrenBreadthFirst<Collider>(CanCloneCollider);

        if (originalCollider == null)
        {
            Plugin.Instance.Logger.LogWarning($"Radar target {target.name} has no usable colliders");
            return;
        }

        TargetTracker tracker;

        // Create an object to place in the Triggers layer so that OnTriggerStay() will
        // be called. Restrict this to only colliding with other triggers to minimize the
        // number of calls to OnTriggerStay().
        //
        // We also create one for any items, since they will be held above the ground when
        // carried, and we don't want to miss the triggers near the ship that are not tall
        // enough to hit them.
        var trackerObject = CreateColliderObjectAsChildOf(originalCollider);
        trackerObject.layer = triggersLayer;

        var collider = trackerObject.GetComponent<Collider>();
        collider.excludeLayers = triggersExclusionMask;

        if (target.TryGetComponent<GrabbableObject>(out _))
        {
            // Expand the collision and offset it downwards if it can be held.
            // Triggers around the ship don't extend high enough to trigger otherwise.
            const float expand = 1.25f;
            trackerObject.transform.localPosition -= new Vector3(0, expand / 2, 0);
            trackerObject.transform.localScale += new Vector3(0, expand, 0);
        }

        trackerObject.GetComponent<Renderer>().sharedMaterial = colliderMaterial;

        trackerObject.GetComponent<Collider>().isTrigger = true;

        var rigidBody = trackerObject.AddComponent<Rigidbody>();
        rigidBody.isKinematic = true;

        tracker = trackerObject.AddComponent<TargetTracker>();

        tracker.info = new TargetInfo(target, copyFrom != null ? GetCurrentInfo(copyFrom) : null);
        targetReverbTriggers.Add(target, tracker.info);
    }

    internal static TargetInfo GetCurrentInfo(Transform target)
    {
        if (!targetReverbTriggers.TryGetValue(target, out var info))
            return null;
        return info;
    }

    internal TargetInfo info;

    private void Start()
    {
        if (info?.root == null)
            Destroy(this);

        if (!TryGetComponent<Collider>(out _))
            Plugin.Instance.Logger.LogError($"{this} was added to an object without a collider.");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.TryGetComponent<AudioReverbTrigger>(out var trigger))
            return;

        if (info.lastTrigger != trigger)
            ChangeTriggers(trigger);

        info.lastTrigger = trigger;
    }

    private void ChangeTriggers(AudioReverbTrigger trigger)
    {
        // Based on AudioReverbTrigger.ChangeAudioReverbForPlayer().
        if (trigger.usePreset != -1)
        {
            var presets = FindAnyObjectByType<AudioReverbPresets>();
            if (presets == null)
                return;
            if (trigger.usePreset < 0 || trigger.usePreset >= presets.audioPresets.Length)
                return;

            ChangeTriggers(presets.audioPresets[trigger.usePreset]);
            return;
        }

        var enabledWeathers = info.enabledWeathers;
        enabledWeathers.Length = TimeOfDay.Instance?.effects.Length ?? 0;

        if (trigger.disableAllWeather)
        {
            enabledWeathers.SetAll(false);
        }
        else
        {
            if (trigger.weatherEffect >= 0 && trigger.weatherEffect < enabledWeathers.Length)
                enabledWeathers[trigger.weatherEffect] = trigger.effectEnabled;

            if (trigger.enableCurrentLevelWeather)
            {
                var currentWeather = (int)TimeOfDay.Instance.currentLevelWeather;
                if (currentWeather >= 0 && currentWeather < enabledWeathers.Length)
                    enabledWeathers[currentWeather] = true;
            }
        }
    }

    private void OnDestroy()
    {
        if (info != null)
            targetReverbTriggers.Remove(info.root);
    }
}
