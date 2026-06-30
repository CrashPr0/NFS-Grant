using UnityEngine;

namespace NSFGrant.UI
{
    /// <summary>
    /// Shared dark-card IMGUI look for the desktop/WebGL intake, quiz, and
    /// value-ranking panels (the three OnGUI surfaces in the study flow).
    /// Centralises the textures/styles so all three render consistently and
    /// scale together via <see cref="BaseUnit"/>, instead of each panel
    /// hand-rolling its own sizing.
    ///
    /// Textures/styles build lazily on first use in OnGUI (GUI.skin is only
    /// valid inside the GUI callback) and live for the process lifetime —
    /// they're a handful of 2x2 textures, not worth tearing down per panel.
    /// </summary>
    public static class StudyGuiKit
    {
        public static readonly Color HeaderBlue = new Color(0f, 0.62f, 0.86f, 1f);
        public static readonly Color AccentGreen = new Color(0.21f, 0.78f, 0.55f, 1f);

        private static Texture2D _cardTex;
        private static Texture2D _headerTex;
        private static Texture2D _accentHeaderTex;
        private static Texture2D _fieldTex;
        private static Texture2D _btnNormalTex;
        private static Texture2D _btnHoverTex;
        private static Texture2D _btnActiveTex;
        private static Texture2D _btnDoneTex;
        private static Texture2D _progressBgTex;
        private static Texture2D _progressFillTex;

        private static GUIStyle _titleStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _headerStyle;
        private static GUIStyle _counterStyle;
        private static GUIStyle _bodyStyle;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _fieldStyle;

        /// <summary>Scale factor relative to a 1280x720 reference window.</summary>
        public static float BaseUnit() => Mathf.Min(Screen.width / 1280f, Screen.height / 720f);

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }

        private static void EnsureBuilt()
        {
            if (_cardTex != null) return;

            _cardTex          = Solid(new Color(0.08f, 0.10f, 0.16f, 0.97f));
            _headerTex        = Solid(HeaderBlue);
            _accentHeaderTex  = Solid(AccentGreen);
            _fieldTex         = Solid(new Color(0.04f, 0.05f, 0.09f, 1f));
            _btnNormalTex     = Solid(new Color(0.13f, 0.18f, 0.28f, 1f));
            _btnHoverTex      = Solid(new Color(0f, 0.50f, 0.78f, 1f));
            _btnActiveTex     = Solid(new Color(0f, 0.40f, 0.64f, 1f));
            _btnDoneTex       = Solid(new Color(0.10f, 0.14f, 0.20f, 1f));
            _progressBgTex    = Solid(new Color(0.15f, 0.20f, 0.30f, 1f));
            _progressFillTex  = Solid(AccentGreen);

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = Color.white },
            };

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(1f, 1f, 1f, 0.8f) },
            };

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

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap  = true,
                alignment = TextAnchor.UpperLeft,
                normal    = { textColor = new Color(0.85f, 0.90f, 0.97f) },
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
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

            _fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleLeft,
                border    = new RectOffset(0, 0, 0, 0),
                normal    = { background = _fieldTex, textColor = Color.white },
                focused   = { background = _fieldTex, textColor = Color.white },
            };
        }

        /// <summary>Full-screen dark scrim behind a panel.</summary>
        public static void DrawOverlay()
        {
            EnsureBuilt();
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.70f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void DrawCard(Rect r) { EnsureBuilt(); GUI.DrawTexture(r, _cardTex); }
        public static void DrawHeader(Rect r) { EnsureBuilt(); GUI.DrawTexture(r, _headerTex); }
        public static void DrawAccentHeader(Rect r) { EnsureBuilt(); GUI.DrawTexture(r, _accentHeaderTex); }

        public static void DrawProgressBar(Rect r, float t01)
        {
            EnsureBuilt();
            GUI.DrawTexture(r, _progressBgTex);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t01), r.height), _progressFillTex);
        }

        /// <summary>Button drawn faded + non-interactive, e.g. an already-chosen option.</summary>
        public static void DoneButton(Rect r, string label, float baseUnit)
        {
            EnsureBuilt();
            var style = ButtonStyle(baseUnit);
            var prevBg = style.normal.background;
            var prevColor = style.normal.textColor;
            style.normal.background = _btnDoneTex;
            style.normal.textColor = new Color(0.55f, 0.62f, 0.72f);
            bool prevEnabled = GUI.enabled;
            GUI.enabled = false;
            GUI.Button(r, label, style);
            GUI.enabled = prevEnabled;
            style.normal.background = prevBg;
            style.normal.textColor = prevColor;
        }

        public static GUIStyle TitleStyle(float baseUnit)
        {
            EnsureBuilt();
            _titleStyle.fontSize = Mathf.RoundToInt(22 * baseUnit);
            return _titleStyle;
        }

        public static GUIStyle SubtitleStyle(float baseUnit)
        {
            EnsureBuilt();
            _subtitleStyle.fontSize = Mathf.RoundToInt(13 * baseUnit);
            return _subtitleStyle;
        }

        public static GUIStyle HeaderStyle(float baseUnit)
        {
            EnsureBuilt();
            _headerStyle.fontSize = Mathf.RoundToInt(17 * baseUnit);
            return _headerStyle;
        }

        public static GUIStyle CounterStyle(float baseUnit)
        {
            EnsureBuilt();
            _counterStyle.fontSize = Mathf.RoundToInt(15 * baseUnit);
            return _counterStyle;
        }

        public static GUIStyle BodyStyle(float baseUnit)
        {
            EnsureBuilt();
            _bodyStyle.fontSize = Mathf.RoundToInt(16 * baseUnit);
            return _bodyStyle;
        }

        public static GUIStyle ButtonStyle(float baseUnit)
        {
            EnsureBuilt();
            _buttonStyle.fontSize = Mathf.RoundToInt(16 * baseUnit);
            int pad = Mathf.RoundToInt(13 * baseUnit);
            int sidePad = Mathf.RoundToInt(20 * baseUnit);
            _buttonStyle.padding = new RectOffset(sidePad, sidePad, pad, pad);
            return _buttonStyle;
        }

        public static GUIStyle FieldStyle(float baseUnit)
        {
            EnsureBuilt();
            _fieldStyle.fontSize = Mathf.RoundToInt(18 * baseUnit);
            int pad = Mathf.RoundToInt(10 * baseUnit);
            _fieldStyle.padding = new RectOffset(pad, pad, pad, pad);
            return _fieldStyle;
        }
    }
}
