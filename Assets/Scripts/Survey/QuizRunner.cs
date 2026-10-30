using System;
using System.Collections.Generic;
using UnityEngine;
using NSFGrant.Logging;
using NSFGrant.UI;

namespace NSFGrant.Survey
{
    /// <summary>
    /// Administers a <see cref="QuizDefinition"/> and records every response
    /// to the event log. Rendering: screen-proportional IMGUI panel for
    /// desktop/WebGL, styled via <see cref="StudyGuiKit"/> to match the
    /// intake and value-ranking panels. In VR, call
    /// <see cref="RecordResponse"/> from world-space UI instead.
    /// </summary>
    public class QuizRunner : MonoBehaviour
    {
        [SerializeField] private QuizDefinition quiz;

        [Tooltip("Tag written with each response, e.g. \"pre\" or \"post\".")]
        [SerializeField] private string stage = "pre";

        private bool _visible;
        private int _currentQuestion;
        private int _correctCount;

        // The definition being administered (the Inspector quiz unless
        // Show was given another) and its questions for the current stage.
        private QuizDefinition _running;
        private readonly List<QuizDefinition.Question> _active = new List<QuizDefinition.Question>();

        public bool IsComplete { get; private set; }

        // --- Read/act API for non-IMGUI front ends (VRSurveyPanel). Same
        // responses and log rows as the desktop panel.
        public bool IsVisible => _visible;
        public string StageLabel => stage?.ToLowerInvariant() == "pre" ? "PRE-SURVEY"
                                  : stage?.ToLowerInvariant() == "post" ? "POST-SURVEY"
                                  : stage?.ToLowerInvariant() == "mid" ? "QUICK CHECK-IN"
                                  : (stage?.ToUpperInvariant() ?? "SURVEY");
        public int QuestionIndex => _currentQuestion;
        public int QuestionCount => _active.Count;
        public string CurrentPrompt => _visible ? _active[_currentQuestion].prompt : "";
        public string[] CurrentOptions => _visible ? _active[_currentQuestion].options : new string[0];

        /// <summary>Answers the current question with option <paramref name="index"/>.</summary>
        public void Choose(int index)
        {
            if (!_visible || index < 0 || index >= CurrentOptions.Length)
            {
                return;
            }
            var question = _active[_currentQuestion];
            RecordResponse(question.questionId, index, question.correctIndex);
            NextQuestion();
        }

        /// <summary>Shows the IMGUI quiz panel (desktop/WebGL builds).</summary>
        public void Show(string quizStage)
        {
            Show(quizStage, quiz);
        }

        /// <summary>
        /// Administers <paramref name="definition"/>, asking only the items
        /// whose stages include <paramref name="quizStage"/>.
        /// </summary>
        public void Show(string quizStage, QuizDefinition definition)
        {
            stage = quizStage;
            _running = definition;
            _active.Clear();
            if (definition != null && definition.questions != null)
            {
                foreach (var question in definition.questions)
                {
                    if (AskedAt(question, quizStage)) _active.Add(question);
                }
            }
            _currentQuestion = 0;
            _correctCount = 0;
            _visible = _active.Count > 0;
            IsComplete = !_visible;
            StudyEventLogger.Instance?.LogEvent("quiz_start", definition != null ? definition.name : "", stage);
        }

        private static bool AskedAt(QuizDefinition.Question question, string quizStage)
        {
            if (string.IsNullOrEmpty(question.stages)) return true;
            foreach (string s in question.stages.Split(','))
            {
                if (string.Equals(s.Trim(), quizStage, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Records a response from any UI (IMGUI panel, world-space VR UI, or
        /// an external survey bridge).
        /// </summary>
        public void RecordResponse(string questionId, int selectedIndex, int correctIndex)
        {
            bool scored = correctIndex >= 0;
            bool correct = scored && selectedIndex == correctIndex;
            if (correct) _correctCount++;
            StudyEventLogger.Instance?.LogEvent(
                "quiz_response", questionId,
                $"stage={stage};selected={selectedIndex};correct={(scored ? (correct ? "1" : "0") : "na")}");
        }

        private void OnGUI()
        {
            // In VR the in-headset VRSurveyPanel shows this instead.
            if (!_visible || Core.PlatformDetector.IsXRActive) return;

            float baseUnit = StudyGuiKit.BaseUnit();

            float pad           = 30f * baseUnit;
            float headerH       = 58f * baseUnit;
            float progressH     = 5f  * baseUnit;
            float questionAreaH = 150f * baseUnit;
            float btnH          = 52f * baseUnit;
            float btnGap        = 10f * baseUnit;

            var question = _active[_currentQuestion];
            int numOpts = question.options.Length;
            int total = _active.Count;

            float cardW = Mathf.Min(660f * baseUnit, Screen.width - 60f);
            float cardH = Mathf.Min(
                headerH + progressH + pad + questionAreaH + 16f * baseUnit
                    + numOpts * (btnH + btnGap) - btnGap + pad,
                Screen.height - 60f);
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            StudyGuiKit.DrawOverlay();
            StudyGuiKit.DrawCard(new Rect(cardX, cardY, cardW, cardH));
            StudyGuiKit.DrawHeader(new Rect(cardX, cardY, cardW, headerH));

            GUI.Label(new Rect(cardX + pad, cardY, cardW * 0.65f, headerH),
                      StageLabel, StudyGuiKit.HeaderStyle(baseUnit));
            GUI.Label(new Rect(cardX, cardY, cardW - pad * 0.8f, headerH),
                      $"Q {_currentQuestion + 1} / {total}", StudyGuiKit.CounterStyle(baseUnit));

            float pY = cardY + headerH;
            StudyGuiKit.DrawProgressBar(new Rect(cardX, pY, cardW, progressH),
                                         (_currentQuestion + 1f) / total);

            float qX = cardX + pad;
            float qW = cardW - pad * 2f;
            float qY = pY + progressH + pad;
            var questionStyle = StudyGuiKit.BodyStyle(baseUnit);
            questionStyle.fontSize = Mathf.RoundToInt(20 * baseUnit);
            GUI.Label(new Rect(qX, qY, qW, questionAreaH), question.prompt, questionStyle);

            float bY = qY + questionAreaH + 16f * baseUnit;
            for (int i = 0; i < numOpts; i++)
            {
                if (GUI.Button(new Rect(qX, bY, qW, btnH), question.options[i], StudyGuiKit.ButtonStyle(baseUnit)))
                {
                    Choose(i);
                }
                bY += btnH + btnGap;
            }
        }

        private void NextQuestion()
        {
            _currentQuestion++;
            if (_currentQuestion >= _active.Count)
            {
                _visible = false;
                IsComplete = true;
                StudyEventLogger.Instance?.LogEvent(
                    "quiz_complete", _running != null ? _running.name : "",
                    $"stage={stage};correct={_correctCount}/{_active.Count}");
            }
        }
    }
}
