using System.Collections.Generic;
using UnityEngine;
using NSFGrant.Core;
using NSFGrant.Interaction;
using NSFGrant.Logging;
using NSFGrant.Stations;
using NSFGrant.Survey;

namespace NSFGrant.Session
{
    /// <summary>
    /// The IRB 26-285 Mid-Study Questionnaire (M1-M4): "presented once after
    /// the participant has entered and explored a second distinct SDG zone."
    /// A zone counts as explored after StudyProtocol.ZoneExploredSeconds
    /// inside it, or once the participant interacts with information in it.
    /// Runs on the shared QuizRunner, so it shows as the desktop panel or,
    /// in the headset, on VRSurveyPanel. Each item offers "Prefer not to
    /// answer", which the browser payment rule counts as answered.
    ///
    /// Also tracks the zones a participant interacted in, for the end-of-
    /// exploration payment-eligibility row (StudyIntake logs it).
    ///
    /// Added at runtime by StudyIntake; nothing to place in the scene.
    /// </summary>
    public class MidStudyCheckIn : MonoBehaviour
    {
        private StudyIntake _intake;
        private QuizRunner _quiz;
        private SdgStation[] _stations;
        private QuizDefinition _instrument;

        private readonly HashSet<string> _explored = new HashSet<string>();
        private readonly HashSet<string> _interacted = new HashSet<string>();
        private readonly List<InteractableObject> _subscribed = new List<InteractableObject>();
        private bool _shown;

        /// <summary>True once M1-M4 have all been answered or declined.</summary>
        public bool Completed { get; private set; }

        /// <summary>True while the M1-M4 panel is up.</summary>
        public bool IsShowing => _shown && !Completed && _quiz != null && _quiz.IsVisible;

        /// <summary>Distinct SDG zones the participant interacted with information in.</summary>
        public int ZonesInteracted => _interacted.Count;

        private void Awake()
        {
            _intake = GetComponent<StudyIntake>();
            _quiz = GetComponent<QuizRunner>();
            _instrument = ApprovedInstruments.MidStudy();
        }

        private void Start()
        {
            _stations = FindObjectsByType<SdgStation>(FindObjectsSortMode.None);
            foreach (var interactable in FindObjectsByType<InteractableObject>(FindObjectsSortMode.None))
            {
                interactable.Activated += OnActivated;
                _subscribed.Add(interactable);
            }
        }

        private void OnDestroy()
        {
            foreach (var interactable in _subscribed)
            {
                if (interactable != null) interactable.Activated -= OnActivated;
            }
            if (_instrument != null) Destroy(_instrument);
        }

        private void OnActivated(InteractableObject interactable)
        {
            if (_intake == null || !_intake.IsExploring) return;
            var station = interactable.GetComponentInParent<SdgStation>();
            if (station == null) return;
            _interacted.Add(station.StationId);
            MarkExplored(station.StationId, "interaction");
        }

        private void Update()
        {
            if (_intake == null || _quiz == null || !_intake.IsExploring) return;

            if (_shown)
            {
                if (!Completed && _quiz.IsComplete)
                {
                    Completed = true;
                }
                return;
            }

            if (_stations != null)
            {
                foreach (var station in _stations)
                {
                    if (station.TotalTimeIncludingCurrentVisit >= StudyProtocol.ZoneExploredSeconds)
                    {
                        MarkExplored(station.StationId, "time");
                    }
                }
            }

            // Wait for a calm moment: not over the VR "end exploring?"
            // confirmation.
            if (_explored.Count >= 2 && !VRSurveyPanel.IsOpen && !_quiz.IsVisible)
            {
                _shown = true;
                StudyEventLogger.Instance?.LogEvent("mid_survey_trigger", "",
                    $"zones_explored={string.Join("|", _explored)}");
                _quiz.Show("mid", _instrument);
            }
        }

        private void MarkExplored(string stationId, string reason)
        {
            if (_explored.Add(stationId))
            {
                StudyEventLogger.Instance?.LogEvent("zone_explored", stationId,
                    $"reason={reason};count={_explored.Count}");
            }
        }
    }
}
