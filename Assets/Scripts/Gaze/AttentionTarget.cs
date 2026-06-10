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
        [Tooltip("Stable label used in the data files. Defaults to the GameObject name.")]
        [SerializeField] private string targetId;

        /// <summary>Stable identifier written to the CSV logs.</summary>
        public string TargetId => string.IsNullOrEmpty(targetId) ? gameObject.name : targetId;

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
