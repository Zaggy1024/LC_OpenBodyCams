using UnityEngine;

namespace OpenBodyCams
{
    public static class Utilities
    {
        private static Camera[] allCameras = [];
        private static readonly Plane[] frustumPlanes = new Plane[6];

        public static bool IsVisibleToAnyCameraExcept(this Renderer renderer, Camera cameraToSkip, bool debug = false)
        {
            if (allCameras.Length != Camera.allCamerasCount)
                allCameras = new Camera[Camera.allCamerasCount];
            Camera.GetAllCameras(allCameras);

            var bounds = renderer.bounds;
            var layer = renderer.gameObject.layer;

            foreach (var camera in allCameras)
            {
                if (camera is not null && (object)camera == cameraToSkip)
                    continue;
                if ((camera.cullingMask & (1 << layer)) == 0)
                    continue;

                GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
                if (GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
                    return true;
            }

            return false;
        }
    }
}
