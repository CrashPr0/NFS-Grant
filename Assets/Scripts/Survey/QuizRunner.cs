using UnityEngine;
using NSFGrant.Logging;

namespace NSFGrant.Survey
{
    /// <summary>
    /// Administers a <see cref="QuizDefinition"/> and records every response
    /// to the event log (event_type "quiz_response", detail includes the
    /// selected option and correctness) so pre/post knowledge change can be
    /// computed during analysis.
    ///
    /// Rendering: a simple IMGUI panel is provided for the desktop/WebGL
    /// build (call <see cref="Show"/>). In VR, IMGUI does not render — call
    /// <see cref="RecordResponse"/> from your world-space quiz UI instead,
    /// or administer questionnaires through VERA's survey tools.
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
            IsComplete = false;
            _visible = quiz != null && quiz.questions != null && quiz.questions.Length > 0;
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
            if (correct)
            {
                _correctCount++;
            }

            StudyEventLogger.Instance?.LogEvent(
                "quiz_response", questionId,
                $"stage={stage};selected={selectedIndex};correct={(scored ? (correct ? "1" : "0") : "na")}");
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            const float width = 520f;
            float x = (Screen.width - width) * 0.5f;
            GUILayout.BeginArea(new Rect(x, 80f, width, Screen.height - 160f), GUI.skin.box);

            var question = quiz.questions[_currentQuestion];
            GUILayout.Label($"Question {_currentQuestion + 1} of {quiz.questions.Length}");
            GUILayout.Space(8f);
            GUILayout.Label(question.prompt);
            GUILayout.Space(8f);

            for (int i = 0; i < question.options.Length; i++)
            {
                if (GUILayout.Button(question.options[i], GUILayout.Height(32f)))
                {
                    RecordResponse(question.questionId, i, question.correctIndex);
                    NextQuestion();
                }
            }

            GUILayout.EndArea();
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
