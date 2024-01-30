using System;
using System.Linq;

using UnityEngine;

namespace OpenBodyCams.API
{
    public static class BodyCam
    {
        public static BodyCamComponent CreateBodyCam(GameObject objectToAttachComponentTo, Renderer displayedOnRenderer, int displayMaterialIndex, ManualCameraRenderer mapRendererToSyncTo = null)
        {
            if (mapRendererToSyncTo != null && mapRendererToSyncTo.cam != mapRendererToSyncTo.mapCamera)
                throw new ArgumentException("The camera must be a map renderer", nameof(mapRendererToSyncTo));

            var bodyCam = objectToAttachComponentTo.AddComponent<BodyCamComponent>();
            bodyCam.MonitorRenderer = displayedOnRenderer;
            bodyCam.MonitorMaterialIndex = displayMaterialIndex;
            bodyCam.MonitorOffMaterial = displayedOnRenderer.sharedMaterials[displayMaterialIndex];
            bodyCam.SetTargetToPlayer(StartOfRound.Instance?.allPlayerScripts.FirstOrDefault(player => player.isPlayerControlled));

            if (mapRendererToSyncTo != null)
            {
                var synchronizer = objectToAttachComponentTo.AddComponent<SyncBodyCamToRadarMap>();
                synchronizer.BodyCam = bodyCam;
                synchronizer.MapRenderer = mapRendererToSyncTo;
            }

            return bodyCam;
        }
    }
}
