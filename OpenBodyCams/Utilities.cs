using UnityEngine;

namespace OpenBodyCams
{
    public static class Utilities
    {
        private static readonly Plane[] frustumPlanes = new Plane[6];

        public static bool IsVisibleToAnyCameraExcept(this Renderer renderer, Camera cameraToSkip, bool debug = false)
        {
            var bounds = renderer.bounds;
            var layer = renderer.gameObject.layer;

            foreach (var camera in Camera.allCameras)
            {
                if ((object)camera == cameraToSkip)
                    continue;
                if (!camera.isActiveAndEnabled)
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
