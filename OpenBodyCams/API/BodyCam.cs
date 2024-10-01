using System;
using System.Linq;

using UnityEngine;

#nullable enable

namespace OpenBodyCams.API
{
    public static class BodyCam
    {
        public delegate void MainBodyCamStatusUpdate();

        public static event MainBodyCamStatusUpdate? OnBodyCamReceiverBecameEnabled;
        public static event MainBodyCamStatusUpdate? OnBodyCamReceiverBecameDisabled;

        public delegate void BodyCamStatusUpdate(BodyCamComponent bodyCam);

        public static event BodyCamStatusUpdate? OnBodyCamInstantiated;
        public static event BodyCamStatusUpdate? OnBodyCamDestroyed;

        public static bool BodyCamsAreAvailable
        {
            get
            {
                if (ShipUpgrades.BodyCamUnlockable == null)
                    return true;
                return ShipUpgrades.BodyCamUnlockableIsPlaced;
            }
        }

        public static BodyCamComponent? MainBodyCam => ShipObjects.MainBodyCam;

        public static BodyCamComponent CreateBodyCam(GameObject objectToAttachComponentTo, Renderer? displayedOnRenderer, int displayMaterialIndex, ManualCameraRenderer? mapRendererToSyncTo = null)
        {
            if (mapRendererToSyncTo != null && mapRendererToSyncTo.cam != mapRendererToSyncTo.mapCamera)
                throw new ArgumentException("The camera must be a map renderer", nameof(mapRendererToSyncTo));

            var bodyCam = objectToAttachComponentTo.AddComponent<BodyCamComponent>();
            if (displayedOnRenderer != null)
            {
                bodyCam.MonitorRenderer = displayedOnRenderer;
                bodyCam.MonitorMaterialIndex = displayMaterialIndex;
                bodyCam.MonitorOffMaterial = displayedOnRenderer.sharedMaterials[displayMaterialIndex];
            }
            bodyCam.SetTargetToPlayer(StartOfRound.Instance?.allPlayerScripts.FirstOrDefault(player => player.isPlayerControlled));

            if (mapRendererToSyncTo != null)
            {
                var synchronizer = objectToAttachComponentTo.AddComponent<SyncBodyCamToRadarMap>();
                synchronizer.BodyCam = bodyCam;
                synchronizer.MapRenderer = mapRendererToSyncTo;
            }

            return bodyCam;
        }

        public static BodyCamComponent CreateBodyCam(GameObject objectToAttachComponentTo, Material screenMaterial, ManualCameraRenderer? mapRendererToSyncTo = null)
        {
            var bodyCam = CreateBodyCam(objectToAttachComponentTo, null, -1, mapRendererToSyncTo);
            bodyCam.MonitorOnMaterial = screenMaterial;
            return bodyCam;
        }

        internal static void BodyCamReceiverBecameEnabled()
        {
            OnBodyCamReceiverBecameEnabled?.Invoke();
        }

        internal static void BodyCamReceiverBecameDisabled()
        {
            OnBodyCamReceiverBecameDisabled?.Invoke();
        }

        internal static void BodyCamInstantiated(BodyCamComponent bodyCam)
        {
            OnBodyCamInstantiated?.Invoke(bodyCam);
        }

        internal static void BodyCamDestroyed(BodyCamComponent bodyCam)
        {
            OnBodyCamDestroyed?.Invoke(bodyCam);
        }
    }
}
