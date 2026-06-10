using UnityEngine;

namespace NSFGrant.Gaze
{
    /// <summary>
    /// Supplies a single gaze ray per frame for the attention pipeline.
    ///
    /// On Quest Pro, attach an OVREyeGaze component (from the Meta XR Core SDK)
    /// to a child GameObject and assign it to <see cref="leftEyeGaze"/> /
    /// <see cref="rightEyeGaze"/>; the provider averages both eyes into a
    /// combined gaze ray. On Quest 3 (which has no eye-tracking hardware) the
    /// provider automatically falls back to head gaze (center-eye forward),
    /// which is a well-established proxy for overt attention in HMD studies.
    /// </summary>
    public class GazeProvider : MonoBehaviour
    {
        public enum GazeSource
        {
            None,
            EyeTracking,
            HeadGaze
        }

        [Tooltip("Center eye anchor of the OVRCameraRig (or the main camera transform).")]
        [SerializeField] private Transform centerEyeAnchor;

        [Tooltip("OVREyeGaze component for the left eye (Quest Pro only). Optional.")]
        [SerializeField] private OVREyeGaze leftEyeGaze;

        [Tooltip("OVREyeGaze component for the right eye (Quest Pro only). Optional.")]
        [SerializeField] private OVREyeGaze rightEyeGaze;

        [Tooltip("Minimum OVREyeGaze confidence required to use eye tracking this frame.")]
        [Range(0f, 1f)]
        [SerializeField] private float minEyeConfidence = 0.5f;

        /// <summary>Origin of the current gaze ray in world space.</summary>
        public Vector3 GazeOrigin { get; private set; }

        /// <summary>Normalized direction of the current gaze ray in world space.</summary>
        public Vector3 GazeDirection { get; private set; } = Vector3.forward;

        /// <summary>Confidence reported by the eye tracker (1 when using head gaze).</summary>
        public float Confidence { get; private set; }

        /// <summary>Which signal produced the current gaze ray.</summary>
        public GazeSource Source { get; private set; } = GazeSource.None;

        /// <summary>Current gaze ray, convenient for raycasting.</summary>
        public Ray GazeRay => new Ray(GazeOrigin, GazeDirection);

        public Transform CenterEyeAnchor => centerEyeAnchor;

        private void Awake()
        {
            if (centerEyeAnchor == null && Camera.main != null)
            {
                centerEyeAnchor = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (TryGetEyeGaze(out var origin, out var direction, out var confidence))
            {
                GazeOrigin = origin;
                GazeDirection = direction;
                Confidence = confidence;
                Source = GazeSource.EyeTracking;
                return;
            }

            if (centerEyeAnchor != null)
            {
                GazeOrigin = centerEyeAnchor.position;
                GazeDirection = centerEyeAnchor.forward;
                Confidence = 1f;
                Source = GazeSource.HeadGaze;
                return;
            }

            Source = GazeSource.None;
            Confidence = 0f;
        }

        private bool TryGetEyeGaze(out Vector3 origin, out Vector3 direction, out float confidence)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;
            confidence = 0f;

            bool leftValid = IsEyeValid(leftEyeGaze);
            bool rightValid = IsEyeValid(rightEyeGaze);

            if (!leftValid && !rightValid)
            {
                return false;
            }

            if (leftValid && rightValid)
            {
                origin = (leftEyeGaze.transform.position + rightEyeGaze.transform.position) * 0.5f;
                direction = (leftEyeGaze.transform.forward + rightEyeGaze.transform.forward).normalized;
                confidence = Mathf.Min(leftEyeGaze.Confidence, rightEyeGaze.Confidence);
            }
            else
            {
                var eye = leftValid ? leftEyeGaze : rightEyeGaze;
                origin = eye.transform.position;
                direction = eye.transform.forward;
                confidence = eye.Confidence;
            }

            return true;
        }

        private bool IsEyeValid(OVREyeGaze eye)
        {
            return eye != null
                   && eye.isActiveAndEnabled
                   && eye.EyeTrackingEnabled
                   && eye.Confidence >= minEyeConfidence;
        }
    }
}
