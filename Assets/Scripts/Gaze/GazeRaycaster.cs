using UnityEngine;

namespace NSFGrant.Gaze
{
    /// <summary>
    /// Raycasts the current gaze ray into the scene every frame and tracks
    /// which <see cref="AttentionTarget"/> (if any) is being looked at,
    /// firing enter/stay/exit bookkeeping used for dwell-time statistics.
    /// </summary>
    [RequireComponent(typeof(GazeProvider))]
    public class GazeRaycaster : MonoBehaviour
    {
        [Tooltip("Maximum gaze raycast distance in meters.")]
        [SerializeField] private float maxDistance = 50f;

        [Tooltip("Layers considered by the gaze raycast.")]
        [SerializeField] private LayerMask layerMask = ~0;

        private GazeProvider _gazeProvider;

        /// <summary>Target currently under the gaze ray, or null.</summary>
        public AttentionTarget CurrentTarget { get; private set; }

        /// <summary>World-space point the gaze ray hit this frame (valid when HasHit).</summary>
        public Vector3 HitPoint { get; private set; }

        /// <summary>Distance to the gaze hit this frame (valid when HasHit).</summary>
        public float HitDistance { get; private set; }

        /// <summary>True when the gaze ray hit any collider this frame.</summary>
        public bool HasHit { get; private set; }

        /// <summary>Seconds since session start; set by the SessionController.</summary>
        public float SessionTime { get; set; }

        private void Awake()
        {
            _gazeProvider = GetComponent<GazeProvider>();
        }

        private void Update()
        {
            HasHit = false;
            AttentionTarget hitTarget = null;

            if (_gazeProvider.Source != GazeProvider.GazeSource.None &&
                Physics.Raycast(_gazeProvider.GazeRay, out RaycastHit hit, maxDistance, layerMask))
            {
                HasHit = true;
                HitPoint = hit.point;
                HitDistance = hit.distance;
                hitTarget = hit.collider.GetComponentInParent<AttentionTarget>();
            }

            if (hitTarget != CurrentTarget)
            {
                if (hitTarget != null)
                {
                    hitTarget.OnGazeEnter(SessionTime);
                }
                CurrentTarget = hitTarget;
            }

            if (CurrentTarget != null)
            {
                CurrentTarget.OnGazeStay(Time.unscaledDeltaTime);
            }
        }
    }
}
