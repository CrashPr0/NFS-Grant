using System;
using UnityEngine;
using NSFGrant.Core;
using NSFGrant.Survey;
using NSFGrant.UI;

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
            float baseUnit = StudyGuiKit.BaseUnit();

            float pad     = 30f * baseUnit;
            float headerH = 64f * baseUnit;
            float labelH  = 24f * baseUnit;
            float fieldH  = 46f * baseUnit;
            float gap     = 14f * baseUnit;
            float btnH    = 52f * baseUnit;

            float cardW = Mathf.Min(520f * baseUnit, Screen.width - 60f);
            float cardH = headerH + pad + labelH + fieldH + gap + btnH + pad;
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            StudyGuiKit.DrawOverlay();
            StudyGuiKit.DrawCard(new Rect(cardX, cardY, cardW, cardH));
            StudyGuiKit.DrawHeader(new Rect(cardX, cardY, cardW, headerH));

            var titleStyle = StudyGuiKit.TitleStyle(baseUnit);
            GUI.Label(new Rect(cardX + pad, cardY, cardW - pad * 2f, headerH * 0.6f),
                      "UN SDG Discovery Hall", titleStyle);
            GUI.Label(new Rect(cardX + pad, cardY + headerH * 0.55f, cardW - pad * 2f, headerH * 0.45f),
                      "Study Session", StudyGuiKit.SubtitleStyle(baseUnit));

            float x = cardX + pad;
            float w = cardW - pad * 2f;
            float y = cardY + headerH + pad;

            GUI.Label(new Rect(x, y, w, labelH), "Participant ID", StudyGuiKit.BodyStyle(baseUnit));
            y += labelH;
            GUI.SetNextControlName("ParticipantIdField");
            _enteredId = GUI.TextField(new Rect(x, y, w, fieldH), _enteredId, 32, StudyGuiKit.FieldStyle(baseUnit));
            y += fieldH + gap;

            bool canStart = !string.IsNullOrWhiteSpace(_enteredId);
            var prevColor = GUI.color;
            if (!canStart) GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.enabled = canStart;
            if (GUI.Button(new Rect(x, y, w, btnH), "Start", StudyGuiKit.ButtonStyle(baseUnit)))
            {
                Begin(_enteredId.Trim(), null, skipQuizzes: false);
            }
            GUI.enabled = true;
            GUI.color = prevColor;
        }

        private void DrawDonePanel()
        {
            float baseUnit = StudyGuiKit.BaseUnit();

            float pad     = 30f * baseUnit;
            float headerH = 64f * baseUnit;
            float bodyH   = 80f * baseUnit;

            float cardW = Mathf.Min(520f * baseUnit, Screen.width - 60f);
            float cardH = headerH + pad + bodyH + pad;
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            StudyGuiKit.DrawOverlay();
            StudyGuiKit.DrawCard(new Rect(cardX, cardY, cardW, cardH));
            StudyGuiKit.DrawAccentHeader(new Rect(cardX, cardY, cardW, headerH));
            GUI.Label(new Rect(cardX + pad, cardY, cardW - pad * 2f, headerH),
                      "Session Complete", StudyGuiKit.TitleStyle(baseUnit));

            var bodyStyle = StudyGuiKit.BodyStyle(baseUnit);
            GUI.Label(new Rect(cardX + pad, cardY + headerH + pad, cardW - pad * 2f, bodyH),
                      "Thank you for participating! Your responses have been recorded. " +
                      "You may close this window.", bodyStyle);
        }
    }
}
