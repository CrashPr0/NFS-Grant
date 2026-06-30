using UnityEngine;
using NSFGrant.Logging;

namespace NSFGrant.Survey
{
    /// <summary>
    /// Administers a <see cref="QuizDefinition"/> and records every response
    /// to the event log. Rendering: screen-proportional IMGUI panel for
    /// desktop/WebGL (dark overlay + UN-blue header + scaled fonts).
    /// In VR, call <see cref="RecordResponse"/> from world-space UI instead.
    /// </summary>
    public class QuizRunner : MonoBehaviour
    {
        [SerializeField] private QuizDefinition quiz;

        [Tooltip("Tag written with each response, e.g. \"pre\" or \"post\".")]
        [SerializeField] private string stage = "pre";

        private bool _visible;
        private int _currentQuestion;
        private int _correctCount;

        // Styles — created lazily inside OnGUI (GUI.skin is only valid there)
        private GUIStyle _headerStyle;
        private GUIStyle _counterStyle;
        private GUIStyle _questionStyle;
        private GUIStyle _optionStyle;

        // Solid-colour backing textures
        private Texture2D _cardTex;
        private Texture2D _headerTex;
        private Texture2D _btnNormalTex;
        private Texture2D _btnHoverTex;
        private Texture2D _btnActiveTex;
        private Texture2D _progressBgTex;
        private Texture2D _progressFillTex;

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

        private void OnDestroy()
        {
            Destroy(_cardTex);
            Destroy(_headerTex);
            Destroy(_btnNormalTex);
            Destroy(_btnHoverTex);
            Destroy(_btnActiveTex);
            Destroy(_progressBgTex);
            Destroy(_progressFillTex);
        }

        private static Texture2D SolidTex(Color c)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }

        private void EnsureStyles()
        {
            if (_cardTex != null) return;

            _cardTex        = SolidTex(new Color(0.08f, 0.10f, 0.16f, 0.97f));
            _headerTex      = SolidTex(new Color(0f, 0.62f, 0.86f, 1f));           // UN blue
            _btnNormalTex   = SolidTex(new Color(0.13f, 0.18f, 0.28f, 1f));
            _btnHoverTex    = SolidTex(new Color(0f, 0.50f, 0.78f, 1f));
            _btnActiveTex   = SolidTex(new Color(0f, 0.40f, 0.64f, 1f));
            _progressBgTex  = SolidTex(new Color(0.15f, 0.20f, 0.30f, 1f));
            _progressFillTex = SolidTex(new Color(0.21f, 0.78f, 0.55f, 1f));       // SDG green

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = Color.white },
            };

            _counterStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                normal    = { textColor = new Color(1f, 1f, 1f, 0.75f) },
            };

            _questionStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap  = true,
                alignment = TextAnchor.UpperLeft,
                normal    = { textColor = new Color(0.92f, 0.96f, 1f) },
            };

            _optionStyle = new GUIStyle(GUI.skin.button)
            {
                wordWrap  = true,
                alignment = TextAnchor.MiddleLeft,
                border    = new RectOffset(0, 0, 0, 0),
                margin    = new RectOffset(0, 0, 0, 0),
                normal    = { background = _btnNormalTex, textColor = new Color(0.88f, 0.93f, 1f) },
                hover     = { background = _btnHoverTex,  textColor = Color.white },
                active    = { background = _btnActiveTex, textColor = Color.white },
                focused   = { background = _btnNormalTex, textColor = Color.white },
            };
        }

        private void OnGUI()
        {
            if (!_visible) return;

            EnsureStyles();

            // Scale all sizes relative to 1280×720: baseUnit = 1.0 at that resolution.
            float baseUnit = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);

            // Refresh font sizes every frame so they follow window resize.
            _headerStyle.fontSize   = Mathf.RoundToInt(17 * baseUnit);
            _counterStyle.fontSize  = Mathf.RoundToInt(15 * baseUnit);
            _questionStyle.fontSize = Mathf.RoundToInt(20 * baseUnit);
            _optionStyle.fontSize   = Mathf.RoundToInt(16 * baseUnit);
            int btnPad = Mathf.RoundToInt(13 * baseUnit);
            int btnSidePad = Mathf.RoundToInt(20 * baseUnit);
            _optionStyle.padding = new RectOffset(btnSidePad, btnSidePad, btnPad, btnPad);

            // Layout constants in scaled pixels
            float pad         = 30f * baseUnit;
            float headerH     = 58f * baseUnit;
            float progressH   = 5f  * baseUnit;
            float questionAreaH = 150f * baseUnit;
            float btnH        = 52f * baseUnit;
            float btnGap      = 10f * baseUnit;

            var question = quiz.questions[_currentQuestion];
            int  numOpts = question.options.Length;
            int  total   = quiz.questions.Length;

            float cardW = Mathf.Min(660f * baseUnit, Screen.width  - 60f);
            float cardH = Mathf.Min(
                headerH + progressH + pad + questionAreaH + 16f * baseUnit
                    + numOpts * (btnH + btnGap) - btnGap + pad,
                Screen.height - 60f);
            float cardX = (Screen.width  - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            // Dark screen overlay
            var prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.70f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prevColor;

            // Card
            GUI.DrawTexture(new Rect(cardX, cardY, cardW, cardH), _cardTex);

            // Header stripe
            GUI.DrawTexture(new Rect(cardX, cardY, cardW, headerH), _headerTex);

            string stageLabel = stage?.ToLowerInvariant() == "pre"  ? "PRE-SURVEY"
                              : stage?.ToLowerInvariant() == "post" ? "POST-SURVEY"
                              : (stage?.ToUpperInvariant() ?? "SURVEY");
            GUI.Label(new Rect(cardX + pad, cardY, cardW * 0.65f, headerH), stageLabel, _headerStyle);
            GUI.Label(new Rect(cardX, cardY, cardW - pad * 0.8f, headerH),
                      $"Q {_currentQuestion + 1} / {total}", _counterStyle);

            // Progress bar
            float pY = cardY + headerH;
            GUI.DrawTexture(new Rect(cardX, pY, cardW, progressH), _progressBgTex);
            GUI.DrawTexture(
                new Rect(cardX, pY, cardW * ((_currentQuestion + 1f) / total), progressH),
                _progressFillTex);

            // Question prompt
            float qX = cardX + pad;
            float qW = cardW - pad * 2f;
            float qY = pY + progressH + pad;
            GUI.Label(new Rect(qX, qY, qW, questionAreaH), question.prompt, _questionStyle);

            // Answer buttons
            float bY = qY + questionAreaH + 16f * baseUnit;
            for (int i = 0; i < numOpts; i++)
            {
                if (GUI.Button(new Rect(qX, bY, qW, btnH), question.options[i], _optionStyle))
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
