using System.Collections.Generic;
using UnityEngine;
using NSFGrant.Core;
using NSFGrant.Interaction;
using NSFGrant.Logging;
using NSFGrant.Stations;
using NSFGrant.Survey;
using NSFGrant.UI;

namespace NSFGrant.Session
{
    /// <summary>
    /// The IRB 26-285 Mid-Study Questionnaire (M1-M4): "presented once after
    /// the participant has entered and explored a second distinct SDG zone."
    /// A zone counts as explored after StudyProtocol.ZoneExploredSeconds
    /// inside it, or once the participant interacts with information in it.
    /// Both are counted from the start of exploration (SdgStation keeps time
    /// from session start, before the pre-survey ends), and an interaction
    /// only counts while the participant is inside that zone: the VR gaze
    /// fallback and the laser reach 50 m, so an exhibit selected from the
    /// hub or another room said nothing about having entered it.
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
        // Each zone's time already on the clock when exploration started.
        private readonly Dictionary<SdgStation, float> _timeBeforeExploring = new Dictionary<SdgStation, float>();
        private bool _baselineTaken;
        private bool _shown;
        // Browser builds run M1-M4 in the team's Qualtrics survey (opened with
        // the participant ID); elsewhere (the native Quest app has no browser)
        // they run in-app on the shared QuizRunner.
        private bool _viaQualtrics;
        private bool _qualtricsOpened;
        private string _qualtricsLink;

        /// <summary>True once M1-M4 have all been answered or declined.</summary>
        public bool Completed { get; private set; }

        /// <summary>True while the M1-M4 panel is up.</summary>
        public bool IsShowing => _shown && !Completed && (_viaQualtrics || (_quiz != null && _quiz.IsVisible));

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
            if (!station.IsOccupied)
            {
                StudyEventLogger.Instance?.LogEvent("zone_interaction_from_outside", station.StationId,
                    $"object={interactable.ObjectId}");
                return;
            }
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

            if (_stations != null && !_baselineTaken)
            {
                _baselineTaken = true;
                foreach (var station in _stations)
                {
                    _timeBeforeExploring[station] = station.TotalTimeIncludingCurrentVisit;
                }
            }

            if (_stations != null)
            {
                foreach (var station in _stations)
                {
                    _timeBeforeExploring.TryGetValue(station, out float before);
                    if (station.TotalTimeIncludingCurrentVisit - before >= StudyProtocol.ZoneExploredSeconds)
                    {
                        MarkExplored(station.StationId, "time");
                    }
                }
            }

            // Wait for a calm moment: not over the VR "end exploring?"
            // confirmation or an open in-headset browser tab.
            if (_explored.Count >= 2 && !VRSurveyPanel.IsOpen && !VRWebPanel.IsOpen && !_quiz.IsVisible)
            {
                _shown = true;
                _qualtricsLink = _intake.MidSurveyLink;
                _viaQualtrics = _qualtricsLink != null && BrowserTab.IsSupported;
                StudyEventLogger.Instance?.LogEvent("mid_survey_trigger", "",
                    $"zones_explored={string.Join("|", _explored)};route={(_viaQualtrics ? "qualtrics" : "in_app")}");
                if (_viaQualtrics)
                {
                    // In a headset only the scene is visible, so leave VR and
                    // show the survey in the page (as for the final survey);
                    // on a flat page the participant clicks to open a new tab.
                    if (PlatformDetector.IsXRActive)
                    {
                        OpenQualtrics(true);
                    }
                }
                else
                {
                    _quiz.Show("mid", _instrument);
                }
            }
        }

        private void OpenQualtrics(bool inReader)
        {
            _qualtricsOpened = true;
            StudyEventLogger.Instance?.LogEvent("mid_survey_opened", "",
                $"source={(inReader ? "headset_reader" : "new_tab")}");
            if (inReader) BrowserTab.OpenReader(_qualtricsLink, "Mid-study questions");
            else BrowserTab.OpenInNewTab(_qualtricsLink);
        }

        private void OnGUI()
        {
            if (!_viaQualtrics || !_shown || Completed || _intake == null || !_intake.IsExploring)
            {
                return;
            }
            float baseUnit = StudyGuiKit.BaseUnit();
            float pad = 30f * baseUnit;
            float headerH = 64f * baseUnit;
            float bodyH = 96f * baseUnit;
            float btnH = 52f * baseUnit;
            float gap = 12f * baseUnit;
            float cardW = Mathf.Min(560f * baseUnit, Screen.width - 60f);
            float cardH = headerH + pad + bodyH + gap + btnH * 2f + gap + pad;
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            StudyGuiKit.DrawOverlay();
            StudyGuiKit.DrawCard(new Rect(cardX, cardY, cardW, cardH));
            StudyGuiKit.DrawHeader(new Rect(cardX, cardY, cardW, headerH));
            GUI.Label(new Rect(cardX + pad, cardY, cardW - pad * 2f, headerH),
                      "Quick questions", StudyGuiKit.TitleStyle(baseUnit));

            float x = cardX + pad;
            float w = cardW - pad * 2f;
            float y = cardY + headerH + pad;
            GUI.Label(new Rect(x, y, w, bodyH),
                      _qualtricsOpened
                          ? "Finish the questions in the other tab, then come back here and continue exploring."
                          : "Please pause to answer four short questions. They open in a new tab; come back here afterwards to continue exploring.",
                      StudyGuiKit.BodyStyle(baseUnit));
            y += bodyH + gap;

            if (GUI.Button(new Rect(x, y, w, btnH),
                           _qualtricsOpened ? "Open the questions again" : "Open the questions",
                           StudyGuiKit.ButtonStyle(baseUnit)))
            {
                OpenQualtrics(false);
            }
            y += btnH + gap;

            var prevColor = GUI.color;
            if (!_qualtricsOpened) GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.enabled = _qualtricsOpened;
            if (GUI.Button(new Rect(x, y, w, btnH), "I'm done - continue", StudyGuiKit.ButtonStyle(baseUnit)))
            {
                Completed = true;
                StudyEventLogger.Instance?.LogEvent("mid_survey_returned", "", "");
            }
            GUI.enabled = true;
            GUI.color = prevColor;
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
