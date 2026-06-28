using System.Collections.Generic;
using UnityEngine;
using NSFGrant.Logging;
using NSFGrant.Session;
using NSFGrant.Stations;

namespace NSFGrant.Core
{
    /// <summary>
    /// Light "gamification" (the team's request): a non-intrusive
    /// rooms-explored progress indicator and a completion badge once every SDG
    /// room has been visited. It reads <see cref="SdgStation.VisitCount"/>, logs
    /// "room_progress" / "exploration_complete" events, and draws a small
    /// screen-space HUD on desktop/WebGL.
    ///
    /// Deliberately kept minimal and symmetric across conditions: the HUD is
    /// screen-space (never a world AOI, so the gaze raycaster can't hit it) and
    /// rewards only completion, not interaction, so it does not bias the
    /// attention measures.
    /// </summary>
    public class ProgressTracker : MonoBehaviour
    {
        [SerializeField] private SessionController session;
        [Tooltip("Seconds the completion badge stays on screen after the last room.")]
        [SerializeField] private float badgeDurationSeconds = 6f;

        private SdgStation[] _stations;
        private readonly HashSet<string> _visited = new HashSet<string>();
        private bool _complete;
        private float _completeTime = -1f;

        private void Awake()
        {
            if (session == null) session = GetComponent<SessionController>();
        }

        private void Start()
        {
            _stations = FindObjectsOfType<SdgStation>();
        }

        private void Update()
        {
            // Only count while a session is live; reset cleanly if it restarts.
            if (session != null && !session.SessionRunning)
            {
                if (_visited.Count > 0 && !_complete)
                {
                    _visited.Clear();
                }
                return;
            }
            if (_stations == null || _stations.Length == 0 || _complete)
            {
                return;
            }

            foreach (var station in _stations)
            {
                if (station.VisitCount > 0 && _visited.Add(station.StationId))
                {
                    StudyEventLogger.Instance?.LogEvent("room_progress", station.StationId,
                        $"explored={_visited.Count}/{_stations.Length}");
                }
            }

            if (_visited.Count >= _stations.Length)
            {
                _complete = true;
                _completeTime = Time.unscaledTime;
                StudyEventLogger.Instance?.LogEvent("exploration_complete", "",
                    $"rooms={_stations.Length}");
            }
        }

        private void OnGUI()
        {
            if (_stations == null || _stations.Length == 0)
            {
                return;
            }
            // Only while a session is live, so the HUD doesn't linger on the
            // intake or completion panels.
            if (session != null && !session.SessionRunning)
            {
                return;
            }

            const float width = 260f;
            var area = new Rect((Screen.width - width) * 0.5f, 12f, width, 56f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label($"Rooms explored: {_visited.Count} / {_stations.Length}");
            if (_complete && Time.unscaledTime - _completeTime < badgeDurationSeconds)
            {
                GUILayout.Label("All rooms explored - nicely done!");
            }
            GUILayout.EndArea();
        }
    }
}
