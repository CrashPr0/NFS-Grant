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

        public bool IsComplete { get; private set; }

        /// <summary>Shows the IMGUI quiz panel (desktop/WebGL builds).</summary>
        public void Show(string quizStage)
        {
            stage = quizStage;
            _currentQuestion = 0;
            _correctCount = 0;
            _visible = quiz != null && quiz.questions != null && quiz.questions.Length > 0;
            IsComplete = !_visible;
            StudyEventLogger.Instance?.LogEvent("quiz_start", quiz != null ? quiz.name : "", stage);
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
            if (!_visible) return;

            float baseUnit = StudyGuiKit.BaseUnit();

            float pad           = 30f * baseUnit;
            float headerH       = 58f * baseUnit;
            float progressH     = 5f  * baseUnit;
            float questionAreaH = 150f * baseUnit;
            float btnH          = 52f * baseUnit;
            float btnGap        = 10f * baseUnit;

            var question = quiz.questions[_currentQuestion];
            int numOpts = question.options.Length;
            int total = quiz.questions.Length;

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

            string stageLabel = stage?.ToLowerInvariant() == "pre" ? "PRE-SURVEY"
                              : stage?.ToLowerInvariant() == "post" ? "POST-SURVEY"
                              : (stage?.ToUpperInvariant() ?? "SURVEY");
            GUI.Label(new Rect(cardX + pad, cardY, cardW * 0.65f, headerH),
                      stageLabel, StudyGuiKit.HeaderStyle(baseUnit));
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
                    RecordResponse(question.questionId, i, question.correctIndex);
                    NextQuestion();
                }
                bY += btnH + btnGap;
            }
        }

        private void NextQuestion()
        {
            _currentQuestion++;
            if (_currentQuestion >= quiz.questions.Length)
            {
                _visible = false;
                IsComplete = true;
                StudyEventLogger.Instance?.LogEvent(
                    "quiz_complete", quiz.name,
                    $"stage={stage};correct={_correctCount}/{quiz.questions.Length}");
            }
        }
    }
}
