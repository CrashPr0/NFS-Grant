using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TLab.WebView;
using NSFGrant.Core;
using NSFGrant.Logging;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// In-headset browser tab for exhibit links (ContentLink) in the Quest
    /// app, so participants stay in the hall instead of being sent to the
    /// system browser. Android's WebView renders offscreen through the
    /// TLabWebView plugin (MIT, pinned in Packages/manifest.json) into a
    /// RawImage on a floating world-space panel.
    ///
    /// Point either controller's laser at the page and pull the trigger to
    /// tap; hold the trigger and move to drag/scroll; push the thumbstick
    /// up/down while pointing at the page to scroll. Back and Close sit
    /// above the page. Opening and closing are logged (content_browser_open
    /// / content_browser_close with seconds open).
    ///
    /// Android WebView can't show PDFs (it downloads them), so .pdf links go
    /// through Google's public document viewer.
    ///
    /// Quest app only: the plugin needs an Android device (it shows nothing
    /// in the Editor), and a browser's WebXR session can't draw web pages at
    /// all, so ContentLink keeps opening a new tab there.
    /// </summary>
    public class VRWebPanel : MonoBehaviour
    {
        /// <summary>True while the browser panel is shown (blocks exhibit selection).</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>Whether links open here rather than in an external browser.</summary>
        public static bool Supported =>
#if UNITY_ANDROID && !UNITY_EDITOR
            PlatformDetector.IsXRActive;
#else
            false;
#endif

        private const float Distance = 1.5f;
        private const float PageWidth = 1.5f;
        // Page layout size in CSS-ish pixels, and the texture it's drawn to.
        private static readonly Vector2Int ViewSize = new Vector2Int(1200, 900);
        private static readonly Vector2Int TexSize = new Vector2Int(1024, 768);
        private const int Fps = 30;
        private const float ToolbarH = 0.09f;
        private const float ButtonW = 0.24f;
        private const float ScrollPixelsPerSecond = 1400f;

        // TLabWebView touch actions.
        private const int TouchDown = 0, TouchUp = 1, TouchMove = 2;

        private static readonly Color PanelColor = new Color(0.08f, 0.10f, 0.16f, 0.96f);
        private static readonly Color ButtonColor = new Color(0.13f, 0.18f, 0.28f, 1f);
        private static readonly Color HoverColor = new Color(0f, 0.50f, 0.78f, 1f);

        private static VRWebPanel _instance;

        private Transform _root;
        private RectTransform _page;
        private BoxCollider _pageCollider;
        private WebView _browser;
        private string _pendingUrl;
        private (BoxCollider collider, Renderer renderer, Action onClick)[] _buttons;
        private Material _buttonMat, _hoverMat;
        private TMPro.TextMeshPro _title;

        private string _sourceId;
        private float _openedAt;

        private XRInputBridge.Hand? _dragHand;
        private long _downTime;
        private Vector2Int _lastTouch;

        /// <summary>Shows the panel in front of the participant and loads the link.</summary>
        public static void Open(string url, string sourceId)
        {
            if (_instance == null)
            {
                _instance = new GameObject("VRWebPanel").AddComponent<VRWebPanel>();
            }
            _instance.Show(url, sourceId);
        }

        private void Show(string url, string sourceId)
        {
            EnsureBuilt();
            if (IsOpen)
            {
                LogClose();
            }
            _sourceId = sourceId;
            _openedAt = Time.unscaledTime;
            StudyEventLogger.Instance?.LogEvent("content_browser_open", sourceId, url);

            _title.text = HostOf(url);
            url = ViewableUrl(url);
            if (_browser.state == FragmentCapture.State.Initialized)
            {
                _browser.LoadUrl(url);
            }
            else
            {
                // Still starting up; LoadUrl is ignored until it's ready.
                _pendingUrl = url;
            }

            _root.gameObject.SetActive(true);
            PlaceInFrontOfHead();
            IsOpen = true;
        }

        private void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            LogClose();
            EndDrag();
            // Stops any video/audio still playing on the hidden page.
            if (_browser.state == FragmentCapture.State.Initialized)
            {
                _browser.LoadUrl("about:blank");
            }
            _pendingUrl = null;
            _root.gameObject.SetActive(false);
            IsOpen = false;
        }

        private void LogClose()
        {
            float seconds = Time.unscaledTime - _openedAt;
            StudyEventLogger.Instance?.LogEvent("content_browser_close", _sourceId,
                $"seconds={seconds:F1}");
        }

        private void Update()
        {
            if (_browser == null)
            {
                return;
            }
            FragmentCapture.GarbageCollect();
            if (_browser.state == FragmentCapture.State.Initialized)
            {
                if (_pendingUrl != null)
                {
                    _browser.LoadUrl(_pendingUrl);
                    _pendingUrl = null;
                }
                // Skip frame capture while the panel is hidden; the page is
                // blank then and the 30 fps copy only costs battery.
                if (IsOpen)
                {
                    _browser.UpdateFrame();
                }
                _browser.DispatchMessageQueue();
            }

            if (!IsOpen)
            {
                return;
            }
            if (!PlatformDetector.IsXRActive)
            {
                Close();
                return;
            }
            KeepInView();
            // A survey/confirm panel owns the trigger while it's open.
            if (!VRSurveyPanel.IsOpen)
            {
                HandlePointer();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsOpen = false;
            }
        }

        // ------------------------------------------------------------- input

        private void HandlePointer()
        {
            int hovered = -1;
            foreach (var pointer in FindObjectsByType<VRLaserPointer>(FindObjectsSortMode.None))
            {
                XRInputBridge.Hand hand = pointer.Hand;
                if (!XRInputBridge.IsConnected(hand))
                {
                    continue;
                }
                var ray = new Ray(pointer.transform.position, pointer.transform.forward);
                bool onPage = TryHitPage(ray, out Vector2Int px);

                if (_dragHand == hand)
                {
                    if (XRInputBridge.GetTrigger(hand) < 0.3f)
                    {
                        EndDrag();
                    }
                    else if (onPage && px != _lastTouch)
                    {
                        _lastTouch = px;
                        _browser.TouchEvent(px.x, px.y, TouchMove, _downTime);
                    }
                    continue;
                }

                int button = HitButton(ray);
                if (button >= 0)
                {
                    hovered = button;
                }
                if (!XRInputBridge.GetTriggerDown(hand))
                {
                    if (onPage)
                    {
                        Scroll(XRInputBridge.GetThumbstick(hand).y);
                    }
                    continue;
                }

                if (button >= 0)
                {
                    _buttons[button].onClick();
                    XRInputBridge.SendHaptic(hand, 0.4f, 0.05f);
                    if (!IsOpen)
                    {
                        return;
                    }
                }
                else if (onPage && _dragHand == null)
                {
                    _dragHand = hand;
                    _lastTouch = px;
                    _downTime = _browser.TouchEvent(px.x, px.y, TouchDown, 0);
                }
            }

            for (int i = 0; i < _buttons.Length; i++)
            {
                _buttons[i].renderer.sharedMaterial = i == hovered ? _hoverMat : _buttonMat;
            }
        }

        private void EndDrag()
        {
            if (_dragHand == null)
            {
                return;
            }
            _browser.TouchEvent(_lastTouch.x, _lastTouch.y, TouchUp, _downTime);
            _dragHand = null;
        }

        private void Scroll(float stickY)
        {
            if (Mathf.Abs(stickY) < 0.2f)
            {
                return;
            }
            // Stick up scrolls toward the top of the page.
            int dy = Mathf.RoundToInt(-stickY * ScrollPixelsPerSecond * Time.unscaledDeltaTime);
            if (dy != 0)
            {
                _browser.ScrollBy(0, dy);
            }
        }

        /// <summary>Ray onto the page, as page pixels from the top-left.</summary>
        private bool TryHitPage(Ray ray, out Vector2Int px)
        {
            px = default;
            if (!_pageCollider.Raycast(ray, out RaycastHit hit, 10f))
            {
                return false;
            }
            Vector3 local = _page.InverseTransformPoint(hit.point);
            Rect r = _page.rect;
            float u = Mathf.Clamp01((local.x - r.xMin) / r.width);
            float vFromTop = Mathf.Clamp01((r.yMax - local.y) / r.height);
            px = new Vector2Int(Mathf.RoundToInt(u * ViewSize.x), Mathf.RoundToInt(vFromTop * ViewSize.y));
            return true;
        }

        private int HitButton(Ray ray)
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i].collider.Raycast(ray, out _, 10f))
                {
                    return i;
                }
            }
            return -1;
        }

        // ---------------------------------------------------------- placement

        private void PlaceInFrontOfHead()
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return;
            Vector3 fwd = head.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            _root.position = head.position + fwd * Distance;
            _root.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        /// <summary>Re-centre only after a big turn or walk away (never head-locked).</summary>
        private void KeepInView()
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null || _dragHand != null) return;
            Vector3 toPanel = _root.position - head.position;
            toPanel.y = 0f;
            Vector3 fwd = head.forward;
            fwd.y = 0f;
            if (toPanel.sqrMagnitude < 1e-4f || fwd.sqrMagnitude < 1e-4f) return;
            if (Vector3.Angle(fwd, toPanel) > 70f || toPanel.magnitude > Distance * 2.5f)
            {
                PlaceInFrontOfHead();
            }
        }

        // ------------------------------------------------------------ visuals

        private void EnsureBuilt()
        {
            if (_root != null)
            {
                return;
            }
            _root = new GameObject("Panel").transform;
            _root.SetParent(transform, false);

            float pageH = PageWidth * ViewSize.y / ViewSize.x;
            BuildPage(pageH);

            _buttonMat = MakeMaterial(ButtonColor);
            _hoverMat = MakeMaterial(HoverColor);
            var font = TMPro.TMP_Settings.defaultFontAsset;
            // Text must draw after the panel quads (queue 3100).
            var textMat = new Material(font.material) { renderQueue = 3200 };

            float barY = pageH / 2f + ToolbarH / 2f + 0.01f;
            Quad("Toolbar", new Vector3(0f, barY, 0.01f), new Vector2(PageWidth, ToolbarH), MakeMaterial(PanelColor));
            _title = Label("", new Vector3(-PageWidth / 2f + 0.04f, barY, -0.005f), font, textMat,
                TMPro.TextAlignmentOptions.Left);
            _buttons = new[]
            {
                Button("Back", PageWidth / 2f - ButtonW * 1.5f - 0.03f, barY, font, textMat, () => _browser.GoBack()),
                Button("Close", PageWidth / 2f - ButtonW / 2f - 0.015f, barY, font, textMat, Close),
            };
            _root.gameObject.SetActive(false);
        }

        private void BuildPage(float pageH)
        {
            var canvasGo = new GameObject("Page", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(_root, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _page = (RectTransform)canvasGo.transform;
            _page.sizeDelta = ViewSize;
            // Canvas units are page pixels; scale them to PageWidth metres.
            _page.localScale = Vector3.one * (PageWidth / ViewSize.x);
            _pageCollider = canvasGo.AddComponent<BoxCollider>();
            _pageCollider.size = new Vector3(ViewSize.x, ViewSize.y, 4f);

            var viewGo = new GameObject("View", typeof(RectTransform), typeof(RawImage));
            viewGo.transform.SetParent(_page, false);
            var viewRect = (RectTransform)viewGo.transform;
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = viewRect.offsetMax = Vector2.zero;
            var raw = viewGo.GetComponent<RawImage>();
            raw.color = Color.white;

            _browser = viewGo.AddComponent<WebView>();
            // The plugin only exposes these as serialized fields (normally
            // set on its prefab). ByteBuffer capture: the HardwareBuffer
            // default is reported blank on Quest with Unity 6 / Vulkan.
            SetField(typeof(FragmentCapture), "m_rawImage", raw);
            SetField(typeof(FragmentCapture), "m_captureMode", CaptureMode.ByteBuffer);
            _browser.Init(ViewSize, TexSize, "about:blank", Fps, new Download.Option());
        }

        private void SetField(Type owner, string name, object value)
        {
            FieldInfo f = owner.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null)
            {
                Debug.LogError($"[VRWebPanel] TLabWebView field {owner.Name}.{name} not found; " +
                               "was the package version in Packages/manifest.json changed?");
                return;
            }
            f.SetValue(_browser, value);
        }

        private (BoxCollider, Renderer, Action) Button(string label, float x, float y,
            TMPro.TMP_FontAsset font, Material textMat, Action onClick)
        {
            var quad = Quad(label, new Vector3(x, y, 0f), new Vector2(ButtonW, ToolbarH - 0.02f), _buttonMat);
            var box = quad.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 0.02f);
            Label(label, new Vector3(x, y, -0.005f), font, textMat, TMPro.TextAlignmentOptions.Center);
            return (box, quad.GetComponent<Renderer>(), onClick);
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

        private TMPro.TextMeshPro Label(string text, Vector3 localPos, TMPro.TMP_FontAsset font,
            Material textMat, TMPro.TextAlignmentOptions alignment)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TMPro.TextMeshPro>();
            tm.font = font;
            tm.fontSharedMaterial = textMat;
            tm.fontSize = 0.3f;
            tm.color = Color.white;
            tm.richText = false;
            tm.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tm.overflowMode = TMPro.TextOverflowModes.Overflow;
            tm.rectTransform.sizeDelta = Vector2.zero;
            tm.alignment = alignment;
            tm.text = text;
            return tm;
        }

        private static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("NSFGrant/UnlitTransparentColor");
            var m = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            m.SetColor("_Color", color);
            m.renderQueue = 3100;
            return m;
        }

        // -------------------------------------------------------------- urls

        /// <summary>Android WebView downloads PDFs instead of showing them.</summary>
        private static string ViewableUrl(string url)
        {
            string path = url.Split('?', '#')[0];
            if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return "https://docs.google.com/viewer?embedded=true&url=" + Uri.EscapeDataString(url);
            }
            return url;
        }

        private static string HostOf(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ? uri.Host : url;
        }
    }
}
