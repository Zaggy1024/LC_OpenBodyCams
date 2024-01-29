using UnityEngine;

namespace OpenBodyCams
{
    public static class Utilities
    {
        public static bool IsRendererVisibleToAnyCameraExcept(Renderer renderer, Camera cameraToSkip, bool debug = false)
        {
            foreach (var camera in Camera.allCameras)
            {
                if ((object)camera == cameraToSkip)
                    continue;
                if (!camera.isActiveAndEnabled)
                    continue;
                if ((camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                    continue;

                if (GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), renderer.bounds))
                    return true;
            }

            return false;
        }
    }
}
