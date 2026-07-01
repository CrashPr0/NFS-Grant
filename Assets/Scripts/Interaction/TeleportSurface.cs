using UnityEngine;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// Marks a collider as a valid VR teleport destination for
    /// <see cref="VRLocomotion"/>. Attached to the hall's base Floor plane,
    /// which is the one collider spanning the whole hub + rooms + corridors
    /// footprint - the decorative floor tints (room platforms, the hub's
    /// radial-inlay disc) are deliberately colliderless so they don't
    /// intercept gaze rays, so this is the surface teleport actually needs
    /// to check against.
    /// </summary>
    public class TeleportSurface : MonoBehaviour
    {
    }
}
