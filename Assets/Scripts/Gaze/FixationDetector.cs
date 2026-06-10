using UnityEngine;

namespace NSFGrant.Gaze
{
    /// <summary>
    /// Velocity-threshold (I-VT) fixation classifier.
    ///
    /// Each frame, the angular velocity of the gaze direction is compared
    /// against <see cref="velocityThresholdDegPerSec"/>. Samples below the
    /// threshold that persist for at least <see cref="minFixationDurationMs"/>
    /// are classified as part of a fixation. Fixations are assigned
    /// monotonically increasing IDs so they can be grouped during analysis.
    ///
    /// Defaults follow Salvucci &amp; Goldberg (2000): 30 deg/s threshold and
    /// a 100 ms minimum fixation duration.
    /// </summary>
    [RequireComponent(typeof(GazeProvider))]
    public class FixationDetector : MonoBehaviour
    {
        [Tooltip("Angular velocity below which a sample counts toward a fixation (deg/s).")]
        [SerializeField] private float velocityThresholdDegPerSec = 30f;

        [Tooltip("Minimum duration before a candidate is promoted to a fixation (ms).")]
        [SerializeField] private float minFixationDurationMs = 100f;

        private GazeProvider _gazeProvider;
        private Vector3 _previousDirection;
        private bool _hasPreviousSample;
        private float _candidateStartTime;
        private bool _inCandidate;

        /// <summary>True while the current gaze sample belongs to a fixation.</summary>
        public bool IsFixating { get; private set; }

        /// <summary>ID of the current fixation; -1 when not fixating.</summary>
        public int CurrentFixationId { get; private set; } = -1;

        /// <summary>Duration of the current fixation in seconds; 0 when not fixating.</summary>
        public float CurrentFixationDuration =>
            IsFixating ? Time.unscaledTime - _candidateStartTime : 0f;

        /// <summary>Most recent gaze angular velocity in deg/s.</summary>
        public float AngularVelocityDegPerSec { get; private set; }

        /// <summary>Total number of fixations detected this session.</summary>
        public int FixationCount { get; private set; }

        private void Awake()
        {
            _gazeProvider = GetComponent<GazeProvider>();
        }

        private void Update()
        {
            if (_gazeProvider.Source == GazeProvider.GazeSource.None)
            {
                ResetState();
                return;
            }

            Vector3 direction = _gazeProvider.GazeDirection;

            if (!_hasPreviousSample)
            {
                _previousDirection = direction;
                _hasPreviousSample = true;
                return;
            }

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
            {
                return;
            }

            AngularVelocityDegPerSec = Vector3.Angle(_previousDirection, direction) / dt;
            _previousDirection = direction;

            if (AngularVelocityDegPerSec <= velocityThresholdDegPerSec)
            {
                if (!_inCandidate)
                {
                    _inCandidate = true;
                    _candidateStartTime = Time.unscaledTime;
                }

                if (!IsFixating &&
                    (Time.unscaledTime - _candidateStartTime) * 1000f >= minFixationDurationMs)
                {
                    IsFixating = true;
                    FixationCount++;
                    CurrentFixationId = FixationCount;
                }
            }
            else
            {
                _inCandidate = false;
                IsFixating = false;
                CurrentFixationId = -1;
            }
        }

        private void ResetState()
        {
            _hasPreviousSample = false;
            _inCandidate = false;
            IsFixating = false;
            CurrentFixationId = -1;
            AngularVelocityDegPerSec = 0f;
        }
    }
}
