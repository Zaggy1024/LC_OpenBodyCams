using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenBodyCams
{
    public class SyncBodyCamToRadarMap : MonoBehaviour
    {
        public static SyncBodyCamToRadarMap[] AllSynchronizedCams = new SyncBodyCamToRadarMap[0];

        public static readonly FieldInfo f_ManualCameraRenderer_isScreenOn = AccessTools.Field(typeof(ManualCameraRenderer), "isScreenOn");

        public ManualCameraRenderer MapRenderer;
        public BodyCamComponent BodyCam;

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

        public void UpdateBodyCamTarget()
        {
            if (MapRenderer.targetedPlayer != null)
                BodyCam.SetTargetToPlayer(MapRenderer.targetedPlayer);
            else
                BodyCam.SetTargetToTransform(MapRenderer.radarTargets[MapRenderer.targetTransformIndex].transform);
        }

        public void StartTargetTransition()
        {
            BodyCam.StartTargetTransition();
        }

        void OnDestroy()
        {
            AllSynchronizedCams = AllSynchronizedCams.Where(syncedCam => (object)syncedCam != this).ToArray();
        }
    }
}
