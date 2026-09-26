using System;
using System.Collections.Generic;
using UnityEngine;
using NSFGrant.Core;
using NSFGrant.Session;
using NSFGrant.Survey;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// In-headset front end for the study's surveys. IMGUI (the desktop
    /// intake/quiz/ranking panels) never renders into an XR eye buffer, so
    /// in VR this draws the SAME state as a floating world-space panel and
    /// drives the SAME runners (QuizRunner / ValueRankingRunner /
    /// StudyIntake) - responses land in the event log exactly as on desktop,
    /// and a participant can even start a survey flat and finish it in VR.
    ///
    /// Selection: point either controller's laser (or your gaze) at an
    /// option and pull the trigger. The panel spawns ~1.6 m ahead at eye
    /// height and re-centres if you turn more than 60 degrees away (never
    /// head-locked, for comfort).
    ///
    /// Ending exploration in VR (there is no F10 key): hold BOTH triggers
    /// for <see cref="finishHoldSeconds"/> to open an "End exploring?"
    /// Yes/No confirmation.
    ///
    /// Built at runtime from primitives + the NSFGrant/UnlitTransparentColor
    /// shader already in the build; not an AttentionTarget, so it never
    /// counts as an area of interest.
    /// </summary>
    public class VRSurveyPanel : MonoBehaviour
    {
        [SerializeField] private StudyIntake intake;
        [SerializeField] private QuizRunner quiz;
        [SerializeField] private ValueRankingRunner ranking;

        [SerializeField] private float distance = 1.6f;
        [SerializeField] private float panelWidth = 1.3f;
        [SerializeField] private float finishHoldSeconds = 2.5f;
        [SerializeField] private float maxRayDistance = 6f;

        /// <summary>True while any VR panel is shown (blocks exhibit selection).</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>Debug: what the panel shows / which option is hovered (for XRDiagnostics).</summary>
        public static string DebugState { get; private set; } = "closed";

        private Camera _placedWith;

        private static readonly Color PanelColor = new Color(0.08f, 0.10f, 0.16f, 0.96f);
        private static readonly Color HeaderColor = new Color(0f, 0.62f, 0.86f, 1f);
        private static readonly Color ButtonColor = new Color(0.13f, 0.18f, 0.28f, 1f);
        private static readonly Color HoverColor = new Color(0f, 0.50f, 0.78f, 1f);
        private static readonly Color DoneColor = new Color(0.10f, 0.13f, 0.18f, 1f);
        private static readonly Color AccentColor = new Color(0.21f, 0.78f, 0.55f, 1f);

        private struct Option
        {
            public string Label;
            public bool Enabled;
            public Action OnChoose;
        }

        private class Content
        {
            public string Header = "";
            public string Counter = "";
            public float Progress = -1f;
            public string Body = "";
            public readonly List<Option> Options = new List<Option>();
            public string Signature;
        }

        private Transform _root;
        private string _shownSignature;
        private readonly List<(Collider collider, Renderer renderer, Option option)> _buttons =
            new List<(Collider, Renderer, Option)>();
        private Material _panelMat, _headerMat, _buttonMat, _hoverMat, _doneMat, _accentMat, _textMat;
        private Font _font;

        private bool _confirmOpen;
        private float _bothTriggersSince = -1f;

        private void Awake()
        {
            if (intake == null) intake = GetComponent<StudyIntake>();
            if (quiz == null) quiz = GetComponent<QuizRunner>();
            if (ranking == null) ranking = GetComponent<ValueRankingRunner>();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void Update()
        {
            if (!PlatformDetector.IsXRActive)
            {
                _confirmOpen = false;
                Hide();
                return;
            }

            UpdateFinishGesture();

            Content content = BuildContent();
            if (content == null)
            {
                Hide();
                return;
            }

            EnsureBuilt();
            // Re-place when the content changes OR the active camera changes:
            // on the first VR frame Camera.main can still be the desktop
            // camera, which put the panel in the wrong spot.
            if (content.Signature != _shownSignature)
            {
                Rebuild(content);
                PlaceInFrontOfHead();
            }
            else if (Camera.main != _placedWith)
            {
                PlaceInFrontOfHead();
            }
            else
            {
                KeepInView();
            }
            HandlePointer();
            IsOpen = true;
        }

        // ------------------------------------------------------------ content

        private Content BuildContent()
        {
            var c = new Content();
            if (_confirmOpen && intake != null && intake.IsExploring)
            {
                c.Header = "END EXPLORING?";
                c.Body = "Finish exploring the hall and continue to the final questions?";
                c.Options.Add(new Option { Label = "Yes, I'm finished", Enabled = true,
                    OnChoose = () => { _confirmOpen = false; intake.RequestFinishExploration(); } });
                c.Options.Add(new Option { Label = "No, keep exploring", Enabled = true,
                    OnChoose = () => _confirmOpen = false });
            }
            else if (quiz != null && quiz.IsVisible)
            {
                c.Header = quiz.StageLabel;
                c.Counter = $"Q {quiz.QuestionIndex + 1} / {quiz.QuestionCount}";
                c.Progress = quiz.QuestionCount > 0 ? (quiz.QuestionIndex + 1f) / quiz.QuestionCount : 0f;
                c.Body = quiz.CurrentPrompt;
                string[] options = quiz.CurrentOptions;
                for (int i = 0; i < options.Length; i++)
                {
                    int index = i;
                    c.Options.Add(new Option { Label = options[i], Enabled = true,
                        OnChoose = () => quiz.Choose(index) });
                }
            }
            else if (ranking != null && ranking.IsVisible)
            {
                string[] values = ranking.Values;
                c.Header = "RANK YOUR VALUES";
                c.Counter = $"{ranking.RankedCount} / {values.Length}";
                c.Progress = values.Length > 0 ? (float)ranking.RankedCount / values.Length : 0f;
                c.Body = ranking.Prompt + "\nSelect in order of importance - most important first.";
                for (int i = 0; i < values.Length; i++)
                {
                    int index = i;
                    int rank = ranking.RankOf(i);
                    c.Options.Add(new Option
                    {
                        Label = rank > 0 ? $"{rank}.  {values[i]}" : values[i],
                        Enabled = rank == 0,
                        OnChoose = () => ranking.Choose(index)
                    });
                }
            }
            else if (intake != null && intake.IsAwaitingIntake)
            {
                c.Header = "UN SDG DISCOVERY HALL";
                c.Body = "Please take off the headset and enter your participant ID on the " +
                         "page, then press VR again.";
            }
            else if (intake != null && intake.IsDone)
            {
                c.Header = "SESSION COMPLETE";
                c.Body = "Thank you for participating! Your responses have been recorded. " +
                         "You may remove the headset.";
            }
            else
            {
                return null;
            }

            var sig = new System.Text.StringBuilder();
            sig.Append(c.Header).Append('|').Append(c.Counter).Append('|').Append(c.Body);
            foreach (var o in c.Options) sig.Append('|').Append(o.Label).Append(o.Enabled ? '+' : '-');
            c.Signature = sig.ToString();
            return c;
        }

        // ------------------------------------------------------------ visuals

        private void EnsureBuilt()
        {
            if (_root != null)
            {
                return;
            }
            _root = new GameObject("VRSurveyPanel").transform;
            _panelMat = MakeMaterial(PanelColor);
            _headerMat = MakeMaterial(HeaderColor);
            _buttonMat = MakeMaterial(ButtonColor);
            _hoverMat = MakeMaterial(HoverColor);
            _doneMat = MakeMaterial(DoneColor);
            _accentMat = MakeMaterial(AccentColor);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Text must draw after the panel quads (queue 3100) or the
            // near-opaque panel would blend over it.
            _textMat = new Material(_font.material) { renderQueue = 3200 };
        }

        private void Rebuild(Content c)
        {
            for (int i = _root.childCount - 1; i >= 0; i--)
            {
                Destroy(_root.GetChild(i).gameObject);
            }
            _buttons.Clear();

            const float pad = 0.06f, headerH = 0.12f, progressH = 0.012f, buttonH = 0.1f, gap = 0.022f;
            float bodyH = EstimateBodyHeight(c.Body);
            float h = headerH + progressH + pad + bodyH + pad * 0.5f
                      + c.Options.Count * (buttonH + gap) + pad * 0.5f;
            float top = h / 2f;
            float w = panelWidth;

            Quad("Panel", new Vector3(0f, 0f, 0.01f), new Vector2(w, h), _panelMat);
            Quad("Header", new Vector3(0f, top - headerH / 2f, 0f), new Vector2(w, headerH), _headerMat);
            Label(c.Header, new Vector3(-w / 2f + pad, top - headerH / 2f, -0.005f), 0.034f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            if (!string.IsNullOrEmpty(c.Counter))
            {
                Label(c.Counter, new Vector3(w / 2f - pad, top - headerH / 2f, -0.005f), 0.028f,
                    TextAnchor.MiddleRight, FontStyle.Normal);
            }

            float y = top - headerH;
            if (c.Progress >= 0f)
            {
                float p = Mathf.Clamp01(c.Progress);
                Quad("ProgressBg", new Vector3(0f, y - progressH / 2f, 0f), new Vector2(w, progressH), _doneMat);
                if (p > 0f)
                {
                    Quad("ProgressFill", new Vector3(-w / 2f + w * p / 2f, y - progressH / 2f, -0.001f),
                        new Vector2(w * p, progressH), _accentMat);
                }
            }
            y -= progressH + pad;

            Label(Wrap(c.Body, 46), new Vector3(-w / 2f + pad, y, -0.005f), 0.03f,
                TextAnchor.UpperLeft, FontStyle.Normal);
            y -= bodyH + pad * 0.5f;

            foreach (var option in c.Options)
            {
                float cy = y - buttonH / 2f;
                var quad = Quad("Option", new Vector3(0f, cy, 0f), new Vector2(w - pad * 2f, buttonH),
                    option.Enabled ? _buttonMat : _doneMat);
                // Thin box so laser/gaze rays hit it reliably.
                var box = quad.AddComponent<BoxCollider>();
                box.size = new Vector3(1f, 1f, 0.02f);
                Label(Wrap(option.Label, 52), new Vector3(-w / 2f + pad * 1.8f, cy, -0.005f), 0.026f,
                    TextAnchor.MiddleLeft, FontStyle.Normal,
                    option.Enabled ? Color.white : new Color(0.6f, 0.66f, 0.75f));
                if (option.Enabled)
                {
                    _buttons.Add((box, quad.GetComponent<Renderer>(), option));
                }
                y -= buttonH + gap;
            }

            _shownSignature = c.Signature;
            _root.gameObject.SetActive(true);
        }

        private GameObject Quad(string name, Vector3 localPos, Vector2 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private void Label(string text, Vector3 localPos, float lineHeight, TextAnchor anchor,
            FontStyle style, Color? color = null)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = 64;
            // World line height ~= fontSize * characterSize / 10.
            tm.characterSize = lineHeight * 10f / tm.fontSize;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleRight ? TextAlignment.Right : TextAlignment.Left;
            tm.fontStyle = style;
            tm.color = color ?? Color.white;
            tm.text = text;
            // The built-in font material draws on top of everything, so the
            // panel text is never hidden by scene geometry or the hands.
            go.GetComponent<MeshRenderer>().sharedMaterial = _textMat;
        }

        private static float EstimateBodyHeight(string body)
        {
            int lines = 0;
            foreach (string para in (body ?? "").Split('\n'))
            {
                lines += Mathf.Max(1, Mathf.CeilToInt(para.Length / 46f));
            }
            return Mathf.Max(0.08f, lines * 0.036f);
        }

        private static string Wrap(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (string para in text.Split('\n'))
            {
                int lineLen = 0;
                foreach (string word in para.Split(' '))
                {
                    if (lineLen > 0 && lineLen + word.Length + 1 > maxChars)
                    {
                        sb.Append('\n');
                        lineLen = 0;
                    }
                    else if (lineLen > 0)
                    {
                        sb.Append(' ');
                        lineLen++;
                    }
                    sb.Append(word);
                    lineLen += word.Length;
                }
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        private static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("NSFGrant/UnlitTransparentColor");
            var m = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            m.SetColor("_Color", color);
            // Draw after the scene so the panel reads cleanly.
            m.renderQueue = 3100;
            return m;
        }

        private void Hide()
        {
            if (_root != null && _root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(false);
            }
            _shownSignature = null;
            IsOpen = false;
            DebugState = "closed";
        }

        // ---------------------------------------------------------- placement

        private void PlaceInFrontOfHead()
        {
            _placedWith = Camera.main;
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return;
            Vector3 fwd = head.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            _root.position = head.position + fwd * distance + Vector3.down * 0.1f;
            // Quad faces -Z; point that side at the participant.
            _root.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        private void KeepInView()
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return;
            Vector3 toPanel = _root.position - head.position;
            toPanel.y = 0f;
            Vector3 fwd = head.forward;
            fwd.y = 0f;
            if (toPanel.sqrMagnitude < 1e-4f || fwd.sqrMagnitude < 1e-4f) return;
            // Also re-centre on a big height mismatch: on the first VR frame
            // the head can still be at the floor (tracking not applied yet),
            // which put the panel ~1.6 m too low; also covers sit/stand.
            float heightOff = Mathf.Abs(_root.position.y - (head.position.y - 0.1f));
            if (Vector3.Angle(fwd, toPanel) > 60f || toPanel.magnitude > distance * 2.5f ||
                heightOff > 0.4f)
            {
                PlaceInFrontOfHead();
            }
        }

        // ------------------------------------------------------------- input

        private void HandlePointer()
        {
            Option? hovered = null;
            Renderer hoveredRenderer = null;
            bool clicked = false;

            foreach (var pointer in FindObjectsByType<VRLaserPointer>(FindObjectsSortMode.None))
            {
                XRInputBridge.Hand hand = pointer.Hand;
                if (!XRInputBridge.IsConnected(hand)) continue;
                var ray = new Ray(pointer.transform.position, pointer.transform.forward);
                if (TryHit(ray, out var option, out var r))
                {
                    hovered = option;
                    hoveredRenderer = r;
                    clicked |= XRInputBridge.GetTriggerDown(hand);
                }
            }
            // Gaze + either trigger also works (e.g. no laser in view).
            if (hovered == null && Camera.main != null)
            {
                var gaze = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
                if (TryHit(gaze, out var option, out var r))
                {
                    hovered = option;
                    hoveredRenderer = r;
                    clicked = XRInputBridge.GetTriggerDown(XRInputBridge.Hand.Right) ||
                              XRInputBridge.GetTriggerDown(XRInputBridge.Hand.Left);
                }
            }

            foreach (var b in _buttons)
            {
                b.renderer.sharedMaterial = b.renderer == hoveredRenderer ? _hoverMat : _buttonMat;
            }

            DebugState = $"open header='{_shownSignature?.Split('|')[0]}' " +
                         $"counter='{_shownSignature?.Split('|')[1]}' hover='{(hovered.HasValue ? hovered.Value.Label : "")}'";

            if (clicked && hovered.HasValue)
            {
                hovered.Value.OnChoose?.Invoke();
                XRInputBridge.SendHaptic(XRInputBridge.Hand.Right, 0.4f, 0.05f);
            }
        }

        private bool TryHit(Ray ray, out Option option, out Renderer renderer)
        {
            option = default;
            renderer = null;
            float best = maxRayDistance;
            bool found = false;
            foreach (var b in _buttons)
            {
                if (b.collider.Raycast(ray, out RaycastHit hit, best))
                {
                    best = hit.distance;
                    option = b.option;
                    renderer = b.renderer;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Hold both triggers to open the "End exploring?" confirmation.</summary>
        private void UpdateFinishGesture()
        {
            if (intake == null || !intake.IsExploring || _confirmOpen)
            {
                _bothTriggersSince = -1f;
                return;
            }
            bool both = XRInputBridge.GetTrigger(XRInputBridge.Hand.Left) > 0.8f &&
                        XRInputBridge.GetTrigger(XRInputBridge.Hand.Right) > 0.8f;
            if (!both)
            {
                _bothTriggersSince = -1f;
                return;
            }
            if (_bothTriggersSince < 0f)
            {
                _bothTriggersSince = Time.unscaledTime;
            }
            else if (Time.unscaledTime - _bothTriggersSince >= finishHoldSeconds)
            {
                _bothTriggersSince = -1f;
                _confirmOpen = true;
            }
        }
    }
}
