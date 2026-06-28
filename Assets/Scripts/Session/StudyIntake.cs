using System;
using UnityEngine;
using NSFGrant.Core;
using NSFGrant.Survey;

namespace NSFGrant.Session
{
    /// <summary>
    /// Participant intake and session flow, so the same build runs unattended
    /// for the self-paced web sample and assisted in the LTI Lab:
    ///
    ///   Intake -> pre-quiz -> exploration -> (End key) -> post-quiz
    ///          -> value-ranking -> done
    ///
    /// Assignment sources, in priority order:
    ///   1. URL parameters on WebGL/desktop: ?pid=P123&amp;cond=guided
    ///      (cond: passive|interactive|guided or a|b|c) — recruitment links
    ///      and VERA can encode assignment directly.
    ///   2. An on-screen intake panel (desktop) for lab sessions.
    ///   3. In VR (no IMGUI), the session starts immediately with the
    ///      SessionController's Inspector participant ID; quizzes are
    ///      skipped until a world-space quiz UI exists.
    /// </summary>
    public class StudyIntake : MonoBehaviour
    {
        private enum Phase { Intake, PreQuiz, Running, PostQuiz, Ranking, Done }

        [SerializeField] private SessionController session;
        [SerializeField] private QuizRunner quiz;
        [SerializeField] private ValueRankingRunner ranking;

        [Tooltip("Key that ends exploration and opens the post-quiz (desktop).")]
        [SerializeField] private KeyCode endSessionKey = KeyCode.F10;

        private Phase _phase = Phase.Intake;
        private string _enteredId = "";
        private bool _skipSurveys;

        private void Awake()
        {
            if (session == null) session = GetComponent<SessionController>();
            if (quiz == null) quiz = GetComponent<QuizRunner>();
            if (ranking == null) ranking = GetComponent<ValueRankingRunner>();
        }

        private void Start()
        {
            if (PlatformDetector.IsXRActive)
            {
                // Lab flow: staff set the participant ID in the Inspector;
                // quizzes are administered outside the headset or via VERA.
                Begin(session.ParticipantId, null, skipQuizzes: true);
                return;
            }

            string pid = GetUrlParameter("pid");
            string cond = GetUrlParameter("cond");
            if (!string.IsNullOrEmpty(pid))
            {
                Begin(pid, cond, skipQuizzes: false);
            }
            // Otherwise wait in Intake; OnGUI shows the entry panel.
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.PreQuiz when quiz == null || quiz.IsComplete:
                    _phase = Phase.Running;
                    break;

                case Phase.Running when Input.GetKeyDown(endSessionKey):
                    if (!_skipSurveys && quiz != null)
                    {
                        quiz.Show("post");
                        _phase = Phase.PostQuiz;
                    }
                    else
                    {
                        ShowRankingOrFinish();
                    }
                    break;

                case Phase.PostQuiz when quiz == null || quiz.IsComplete:
                    ShowRankingOrFinish();
                    break;

                case Phase.Ranking when ranking == null || ranking.IsComplete:
                    FinishSession();
                    break;
            }
        }

        /// <summary>Post-exploration value-ranking task, then finish.</summary>
        private void ShowRankingOrFinish()
        {
            if (!_skipSurveys && ranking != null)
            {
                ranking.Show();
                _phase = Phase.Ranking;
            }
            else
            {
                FinishSession();
            }
        }

        private void Begin(string participantId, string conditionParam, bool skipQuizzes)
        {
            if (!string.IsNullOrEmpty(conditionParam) &&
                StudyConditionManager.Instance != null &&
                TryParseCondition(conditionParam, out var condition))
            {
                StudyConditionManager.Instance.Condition = condition;
            }

            session.ParticipantId = participantId;
            _skipSurveys = skipQuizzes;
            session.StartSession();

            if (!skipQuizzes && quiz != null)
            {
                quiz.Show("pre");
                _phase = Phase.PreQuiz;
            }
            else
            {
                _phase = Phase.Running;
            }
        }

        private void FinishSession()
        {
            session.StopSession();
            _phase = Phase.Done;
        }

        private static bool TryParseCondition(string value, out StudyCondition condition)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "a": case "passive": condition = StudyCondition.Passive; return true;
                case "b": case "interactive": condition = StudyCondition.Interactive; return true;
                case "c": case "guided": condition = StudyCondition.Guided; return true;
                default: condition = StudyCondition.Interactive; return false;
            }
        }

        /// <summary>Reads a query parameter from the launch URL (WebGL).</summary>
        private static string GetUrlParameter(string name)
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            int queryStart = url.IndexOf('?');
            if (queryStart < 0 || queryStart == url.Length - 1)
            {
                return null;
            }

            foreach (string pair in url.Substring(queryStart + 1).Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0 &&
                    string.Equals(pair.Substring(0, eq), name, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pair.Substring(eq + 1));
                }
            }
            return null;
        }

        private void OnGUI()
        {
            if (_phase == Phase.Intake)
            {
                DrawIntakePanel();
            }
            else if (_phase == Phase.Done)
            {
                DrawDonePanel();
            }
        }

        private void DrawIntakePanel()
        {
            const float width = 420f;
            GUILayout.BeginArea(
                new Rect((Screen.width - width) * 0.5f, 120f, width, 240f), GUI.skin.box);
            GUILayout.Label("UN SDG Discovery Hall - Study Session");
            GUILayout.Space(8f);
            GUILayout.Label("Participant ID:");
            _enteredId = GUILayout.TextField(_enteredId, 32);
            GUILayout.Space(8f);

            GUI.enabled = !string.IsNullOrWhiteSpace(_enteredId);
            if (GUILayout.Button("Start", GUILayout.Height(32f)))
            {
                Begin(_enteredId.Trim(), null, skipQuizzes: false);
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void DrawDonePanel()
        {
            const float width = 420f;
            GUILayout.BeginArea(
                new Rect((Screen.width - width) * 0.5f, 120f, width, 120f), GUI.skin.box);
            GUILayout.Label("Session complete - thank you for participating!");
            GUILayout.Label("Your responses have been recorded. You may close this window.");
            GUILayout.EndArea();
        }
    }
}
