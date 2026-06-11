using UnityEngine;

namespace NSFGrant.Gaze
{
    /// <summary>
    /// Marks a GameObject as an area of interest (AOI) for the attention study.
    /// Requires a Collider so the GazeRaycaster can hit it. Accumulates simple
    /// per-target statistics (total dwell time, look count) that are written to
    /// the session summary when the session ends.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AttentionTarget : MonoBehaviour
    {
        /// <summary>
        /// The information formats compared in the study (one per zone at each
        /// SDG station, per the research proposal).
        /// </summary>
        public enum ContentFormat
        {
            Other,
            TextPanel,
            DataVisualization,
            VideoStory,
            InteractiveObject,
            Docent,
            CallToActionWall
        }

        [Tooltip("Stable label used in the data files. Defaults to the GameObject name.")]
        [SerializeField] private string targetId;

        [Tooltip("Information format this object represents, for format-comparison analyses.")]
        [SerializeField] private ContentFormat format = ContentFormat.Other;

        [Tooltip("Station this object belongs to, e.g. SDG13_ClimateAction. Optional.")]
        [SerializeField] private string stationId;

        /// <summary>Stable identifier written to the CSV logs.</summary>
        public string TargetId => string.IsNullOrEmpty(targetId) ? gameObject.name : targetId;

        /// <summary>Information format category for analysis.</summary>
        public ContentFormat Format
        {
            get => format;
            set => format = value;
        }

        /// <summary>Owning station ID (may be empty).</summary>
        public string StationId
        {
            get => stationId;
            set => stationId = value;
        }

        /// <summary>Cumulative time this target has been gazed at (seconds).</summary>
        public float TotalDwellTime { get; private set; }

        /// <summary>Number of distinct gaze entries onto this target.</summary>
        public int LookCount { get; private set; }

        /// <summary>Time of the first gaze onto this target (seconds since session start), or -1.</summary>
        public float TimeToFirstLook { get; private set; } = -1f;

        public void OnGazeEnter(float sessionTime)
        {
            LookCount++;
            if (TimeToFirstLook < 0f)
            {
                TimeToFirstLook = sessionTime;
            }
        }

        public void OnGazeStay(float deltaTime)
        {
            TotalDwellTime += deltaTime;
        }

        public void ResetStats()
        {
            TotalDwellTime = 0f;
            LookCount = 0;
            TimeToFirstLook = -1f;
        }
    }
}
