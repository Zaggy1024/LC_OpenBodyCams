using System.Collections.Generic;

using UnityEngine;

using OpenBodyCams.Utilities;

namespace OpenBodyCams.Components;

internal class ReverbTriggerTracker : MonoBehaviour
{
    internal class TargetInfo(Transform root, AudioReverbTrigger initialTrigger)
    {
        internal Transform root = root;
        internal AudioReverbTrigger lastTrigger = initialTrigger;
    }

    private static readonly int triggersLayer = LayerMask.NameToLayer("Triggers");
    private static readonly int triggersExcludedLayers = LayerMask.GetMask("Foliage", "PhysicsObject", "NavigationSurface", "MoldSpore", "LineOfSight", "ScanNode", "Terrain", "PlacementBlocker", "Vehicle");

    private static readonly Material colliderMaterial = new(Shader.Find("HDRP/Lit"));

    private static Dictionary<Transform, TargetInfo> targetReverbTriggers = [];

    internal static bool CanCloneCollider(Collider collider)
    {
        if (collider is BoxCollider)
            return true;
        return false;
    }

    internal static GameObject CreateColliderObjectAsChildOf(Collider collider)
    {
        const string name = "OpenBodyCams_ReverbTriggerTracker";
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

    internal static void AddTrackersToTarget(Transform target, AudioReverbTrigger initialTrigger = null)
    {
        if (targetReverbTriggers.ContainsKey(target))
            return;

        var collider = target.GetComponentInChildrenBreadthFirst<Collider>(CanCloneCollider);

        if (collider == null)
        {
            Plugin.Instance.Logger.LogWarning($"Radar target {target.name} has no usable colliders");
            return;
        }

        ReverbTriggerTracker tracker;

        var colliderLayer = collider.gameObject.layer;
        bool isItem = target.TryGetComponent<GrabbableObject>(out _);

        if (isItem || ((1 << collider.gameObject.layer) & triggersExcludedLayers) != 0)
        {
            // If the collider isn't in a layer that will interact with the reverb triggers,
            // create an object to place in the Triggers layer so that OnTriggerStay() will
            // be called.
            //
            // We also create one for any items, since they will be held above the ground when
            // carried, and we don't want to miss the triggers near the ship that are not tall
            // enough to hit them.
            var trackerObject = CreateColliderObjectAsChildOf(collider);
            trackerObject.layer = triggersLayer;

            if (isItem)
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

            tracker = trackerObject.AddComponent<ReverbTriggerTracker>();
        }
        else
        {
            tracker = collider.gameObject.AddComponent<ReverbTriggerTracker>();
        }

        tracker.info = new TargetInfo(target, initialTrigger);
        targetReverbTriggers.Add(target, tracker.info);
    }

    internal static AudioReverbTrigger GetCurrentReverbTrigger(Transform target)
    {
        if (!targetReverbTriggers.TryGetValue(target, out var info))
            return null;
        return info.lastTrigger;
    }

    internal TargetInfo info;

    private void Start()
    {
        if (!TryGetComponent<Collider>(out _))
            Plugin.Instance.Logger.LogError($"{this} was added to an object without a collider.");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.TryGetComponent<AudioReverbTrigger>(out var trigger))
            return;

        if (info.lastTrigger != trigger)
            BodyCamComponent.UpdateTargetReverbTriggerForAllBodyCams(info.root, trigger);

        info.lastTrigger = trigger;
    }

    private void OnDestroy()
    {
        targetReverbTriggers.Remove(info.root);
    }
}
