using System.Collections.Generic;
using UnityEngine;
using NSFGrant.Core;
using NSFGrant.Logging;
using NSFGrant.Stations;

namespace NSFGrant.Docent
{
    /// <summary>
    /// Minimal virtual docent for Condition C (guided). Walks the participant
    /// through a suggested route of SDG stations: a beacon highlight hovers
    /// over the current recommended station and advances when the participant
    /// arrives. Every guidance step is logged so "use of help/avatar guidance"
    /// can be analyzed.
    ///
    /// This is the scaffold for the conversational AI docent in the project
    /// brief — replace or extend the beacon with an avatar + dialogue system
    /// without changing the logging contract.
    /// </summary>
    public class DocentGuide : MonoBehaviour
    {
        [Tooltip("Suggested visiting order of stations.")]
        [SerializeField] private List<SdgStation> route = new List<SdgStation>();

        [Tooltip("Visual highlight moved above the recommended station.")]
        [SerializeField] private GameObject beacon;

        [Tooltip("Beacon hover height above the station origin (m).")]
        [SerializeField] private float beaconHeight = 3f;

        private int _routeIndex = -1;
        private bool _conditionChecked;

        private void Start()
        {
            if (beacon != null)
            {
                beacon.SetActive(false);
            }
        }

        /// <summary>
        /// The condition is only final once the session starts: StudyIntake
        /// applies ?cond= in its own Start (order vs. this one is undefined)
        /// or later from the intake panel. Deciding in Start would leave a
        /// Guided participant with no docent while the logs say Guided.
        /// </summary>
        private void TryActivate()
        {
            var logger = StudyEventLogger.Instance;
            if (logger != null && !logger.IsLogging)
            {
                return; // session not started yet
            }
            _conditionChecked = true;

            bool enabledByCondition = StudyConditionManager.Instance != null &&
                                      StudyConditionManager.Instance.DocentEnabled;
            if (!enabledByCondition)
            {
                enabled = false;
                return;
            }

            AdvanceToNextStation();
        }

        private void Update()
        {
            if (!_conditionChecked)
            {
                TryActivate();
                return;
            }

            if (_routeIndex < 0 || _routeIndex >= route.Count)
            {
                return;
            }

            if (route[_routeIndex].IsOccupied)
            {
                StudyEventLogger.Instance?.LogEvent(
                    "docent_target_reached", route[_routeIndex].StationId,
                    $"route_index={_routeIndex}");
                AdvanceToNextStation();
            }
        }

        private void AdvanceToNextStation()
        {
            _routeIndex++;

            if (_routeIndex >= route.Count)
            {
                if (beacon != null)
                {
                    beacon.SetActive(false);
                }
                StudyEventLogger.Instance?.LogEvent("docent_route_complete", "",
                    $"stations={route.Count}");
                enabled = false;
                return;
            }

            var station = route[_routeIndex];
            if (beacon != null)
            {
                beacon.SetActive(true);
                beacon.transform.position = station.transform.position + Vector3.up * beaconHeight;
            }

            StudyEventLogger.Instance?.LogEvent("docent_suggest", station.StationId,
                $"route_index={_routeIndex}");
        }
    }
}
