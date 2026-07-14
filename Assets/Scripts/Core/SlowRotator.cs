using UnityEngine;

namespace NSFGrant.Core
{
    /// <summary>
    /// Rotates the object slowly and steadily around the world up axis.
    /// Used for the hub's SDG color-wheel ring - one transform rotation
    /// per frame, so it is effectively free even on Quest/WebGL (the GPU
    /// shader-animation rule in docs/AESTHETICS_PLAN.md exists to avoid
    /// per-vertex CPU work, not a single Rotate call).
    ///
    /// Research note: only ever attach this to environment decor at the
    /// hub center - never to an AttentionTarget or anything room-specific.
    /// Motion attracts gaze; equidistant symmetric motion cannot favor a
    /// condition, but a rotating element inside one room would.
    /// </summary>
    public class SlowRotator : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 2f;

        private void Update()
        {
            transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
        }
    }
}
