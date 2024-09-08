namespace OpenBodyCams
{
    public enum CameraRenderingStatus
    {
        // The camera is currently rendering to its target texture.
        Rendering,
        // The camera is not rendering due to the EnableCamera flag.
        Disabled,
        // The target not able to be viewed (dead with no ragdoll, despawned, etc).
        TargetInvalid,
        // The camera is disabled to save resources while its target is onboard the ship.
        TargetDisabledOnShip,
    }
}
