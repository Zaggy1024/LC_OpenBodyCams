using System;
using System.Linq;

using UnityEngine;

namespace OpenBodyCams
{
    public class SyncBodyCamToRadarMap : MonoBehaviour
    {
        private static SyncBodyCamToRadarMap[] AllSynchronizedCams = new SyncBodyCamToRadarMap[0];
        public static SyncBodyCamToRadarMap[] GetAllSynchronizedCams() { return AllSynchronizedCams; }

        internal ManualCameraRenderer MapRenderer;
        public ManualCameraRenderer GetMapRenderer() { return MapRenderer; }
        internal BodyCamComponent BodyCam;
        public BodyCamComponent GetBodyCam() { return BodyCam; }

        private static void DoForMap(ManualCameraRenderer mapRenderer, Action<SyncBodyCamToRadarMap> action)
        {
            foreach (var syncedCam in AllSynchronizedCams)
            {
                if ((object)syncedCam.MapRenderer == mapRenderer)
                    action(syncedCam);
            }
        }

        public static void UpdateBodyCamTargetForMap(ManualCameraRenderer mapRenderer)
        {
            DoForMap(mapRenderer, syncedCam => syncedCam.UpdateBodyCamTarget());
        }

        public static void StartTargetTransitionForMap(ManualCameraRenderer mapRenderer)
        {
            DoForMap(mapRenderer, syncedCam => syncedCam.StartTargetTransition());
        }

        private static void DoForCam(BodyCamComponent bodyCam, Action<SyncBodyCamToRadarMap> action)
        {
            foreach (var syncedCam in AllSynchronizedCams)
            {
                if ((object)syncedCam.BodyCam == bodyCam)
                    action(syncedCam);
            }
        }

        public static void UpdateBodyCamTarget(BodyCamComponent bodyCam)
        {
            DoForCam(bodyCam, syncedCam => syncedCam.UpdateBodyCamTarget());
        }

        void Awake()
        {
            AllSynchronizedCams = AllSynchronizedCams.Append(this).ToArray();

            if (MapRenderer == null)
                MapRenderer = GetComponentsInChildren<ManualCameraRenderer>()?.FirstOrDefault(renderer => renderer.cam == renderer.mapCamera);
            if (BodyCam == null)
                BodyCam = GetComponent<BodyCamComponent>();
        }

        void Start()
        {
            UpdateBodyCamTarget();
        }

        private void OnEnable()
        {
            if (BodyCam != null && MapRenderer != null)
                UpdateBodyCamTarget();
        }

        public void UpdateBodyCamTarget()
        {
            if (!isActiveAndEnabled)
                return;

            if (MapRenderer.targetedPlayer != null)
                BodyCam.SetTargetToPlayer(MapRenderer.targetedPlayer);
            else
                BodyCam.SetTargetToTransform(MapRenderer.radarTargets[MapRenderer.targetTransformIndex].transform);

            BodyCam.SetScreenPowered(MapRenderer.isScreenOn);
        }

        public void StartTargetTransition()
        {
            if (!isActiveAndEnabled)
                return;

            BodyCam.StartTargetTransition();
        }

        internal static void OnBodyCamDestroyed(BodyCamComponent bodyCam)
        {
            DoForCam(bodyCam, Destroy);
        }

        void OnDestroy()
        {
            AllSynchronizedCams = AllSynchronizedCams.Where(syncedCam => (object)syncedCam != this).ToArray();
        }
    }
}
