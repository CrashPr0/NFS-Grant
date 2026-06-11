using UnityEngine;
using NSFGrant.Logging;

namespace NSFGrant.Stations
{
    /// <summary>
    /// A region of the Discovery Hall dedicated to one SDG (e.g., SDG 13
    /// Climate Action). Attach to an empty GameObject with a BoxCollider
    /// (Is Trigger) covering the station footprint. Tracks visits by testing
    /// the active camera position against the collider bounds each frame —
    /// works identically for the VR rig and the desktop rig — and writes
    /// station_enter / station_exit events, which together encode the
    /// participant's navigation path through the environment.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SdgStation : MonoBehaviour
    {
        [Tooltip("Stable identifier used in the data files, e.g. SDG13_ClimateAction.")]
        [SerializeField] private string stationId;

        private BoxCollider _bounds;
        private bool _occupied;
        private float _entryTime;

        public string StationId => string.IsNullOrEmpty(stationId) ? gameObject.name : stationId;

        /// <summary>Cumulative time the participant has spent inside this station (s).</summary>
        public float TotalTimeInside { get; private set; }

        /// <summary>Number of distinct visits.</summary>
        public int VisitCount { get; private set; }

        /// <summary>Session time of first entry, or -1 if never visited.</summary>
        public float FirstEntryTime { get; private set; } = -1f;

        /// <summary>True while the participant is currently inside the station.</summary>
        public bool IsOccupied => _occupied;

        private void Awake()
        {
            _bounds = GetComponent<BoxCollider>();
        }

        private void Update()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            bool inside = _bounds.bounds.Contains(cam.transform.position);
            var logger = StudyEventLogger.Instance;
            float sessionTime = logger != null ? logger.SessionTime : Time.unscaledTime;

            if (inside && !_occupied)
            {
                _occupied = true;
                _entryTime = Time.unscaledTime;
                VisitCount++;
                if (FirstEntryTime < 0f)
                {
                    FirstEntryTime = sessionTime;
                }
                logger?.LogWorldEvent("station_enter", StationId,
                    $"visit={VisitCount}", cam.transform.position);
            }
            else if (!inside && _occupied)
            {
                _occupied = false;
                float visitDuration = Time.unscaledTime - _entryTime;
                TotalTimeInside += visitDuration;
                logger?.LogWorldEvent("station_exit", StationId,
                    $"visit_duration_s={visitDuration:F2}", cam.transform.position);
            }
        }

        public void ResetStats()
        {
            TotalTimeInside = 0f;
            VisitCount = 0;
            FirstEntryTime = -1f;
            _occupied = false;
        }

        /// <summary>Includes time from a still-open visit, for end-of-session summaries.</summary>
        public float TotalTimeIncludingCurrentVisit =>
            TotalTimeInside + (_occupied ? Time.unscaledTime - _entryTime : 0f);
    }
}
