using System;
using System.Collections.Generic;
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
    ///
    /// On-device diagnostics: every step logs with a "[VRWebPanel]" prefix,
    /// so `adb logcat -s Unity | grep VRWebPanel` shows the WebView start-up
    /// (state, graphics API, capture mode), the first frame, page
    /// start/finish and any fallback with its reason.
    /// </summary>
    public class VRWebPanel : MonoBehaviour
    {
        /// <summary>True while the browser panel is shown (blocks exhibit selection).</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>
        /// True while open and on the frame it closed: the trigger press on
        /// Close must not also select the exhibit behind the panel.
        /// </summary>
        public static bool OwnsTrigger => IsOpen || Time.frameCount == _closedFrame;
        private static int _closedFrame = -1;

        /// <summary>Whether links open here rather than in an external browser.</summary>
        public static bool Supported =>
#if UNITY_ANDROID && !UNITY_EDITOR
            PlatformDetector.IsXRActive && !_failed;
#else
            false;
#endif

        private const bool ClearBrowserDataOnClose = true;

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

        // Set when the browser can't start, never draws, or throws: links
        // then go back to the system browser (ContentLink checks Supported),
        // so a participant never gets a dead panel.
        private static bool _failed;
        // Time from opening for the WebView to start and hand over its first
        // frame. It draws about:blank as soon as it starts, so this doesn't
        // depend on the network; a page still loading shows as white, not
        // as "no frame".
        private const float FirstFrameTimeoutSeconds = 10f;
        // Once a frame has arrived the plugin works on this device.
        private static bool _hadFrame;
        private const int MaxBlocksBeforeClose = 5;
        private const float BlockWindowSeconds = 10f;
        private readonly List<float> _blockTimes = new List<float>();

        private Transform _root;
        private RectTransform _page;
        private BoxCollider _pageCollider;
        private WebView _browser;
        private RawImage _view;
        private FragmentCapture.State _loggedState = FragmentCapture.State.None;
        private float _initStartedAt;
        private bool _backPressed;
        private int _shownFrame = -1;
        private string _pendingUrl;
        private (BoxCollider collider, Renderer renderer, Action onClick)[] _buttons;
        private Material _buttonMat, _hoverMat;
        private TMPro.TextMeshPro _title;

        private string _sourceId;
        private float _openedAt;

        // Navigation is limited to the sites the exhibits link to: the page
        // has no address bar, but links on a page could otherwise lead the
        // participant anywhere. The plugin has no navigation callback, so the
        // current URL is polled and an off-list page is sent back.
        private const float UrlCheckSeconds = 0.4f;
        private const float NoticeSeconds = 3f;
        private HashSet<string> _allowedHosts;
        private string _homeUrl, _lastAllowedUrl, _lastCheckedUrl;
        private float _nextUrlCheck, _noticeUntil;

        // Laser pointers, looked up once a second rather than every frame.
        private VRLaserPointer[] _pointers = Array.Empty<VRLaserPointer>();
        private float _nextPointerRefresh;

        private XRInputBridge.Hand? _dragHand;
        private long _downTime;
        private Vector2Int _lastTouch;

        /// <summary>Shows the panel in front of the participant and loads the link.</summary>
        public static void Open(string url, string sourceId)
        {
            // Only web pages: no javascript:, file:, intent: and so on.
            if (!IsWebUrl(url))
            {
                Debug.LogWarning($"[VRWebPanel] Refusing to open non-web link: {url}");
                StudyEventLogger.Instance?.LogEvent("content_browser_refused", sourceId, url);
                return;
            }
            try
            {
                if (_instance == null)
                {
                    _instance = new GameObject("VRWebPanel").AddComponent<VRWebPanel>();
                }
                _instance.Show(url, sourceId);
            }
            catch (Exception e)
            {
                Fail(e, url, sourceId);
            }
        }

        private static bool IsWebUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                   && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        }

        /// <summary>
        /// The browser is unusable: hide the panel, remember that, log it, and
        /// open the link in the system browser instead (the previous behaviour).
        /// </summary>
        private static void Fail(Exception e, string url, string sourceId)
        {
            _failed = true;
            if (IsOpen) _closedFrame = Time.frameCount;
            IsOpen = false;
            if (_instance != null && _instance._root != null)
            {
                _instance._root.gameObject.SetActive(false);
            }
            Debug.LogError($"[VRWebPanel] Browser failed ({e.GetType().Name}: {e.Message}); " +
                           "falling back to the system browser.");
            StudyEventLogger.Instance?.LogEvent("content_browser_error", sourceId,
                $"{e.GetType().Name}: {e.Message}");
            if (!string.IsNullOrEmpty(url))
            {
                Application.OpenURL(url);
            }
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
            BuildAllowedHosts(url);
            url = ViewableUrl(url);
            _homeUrl = url;
            _lastAllowedUrl = url;
            _lastCheckedUrl = null;
            _noticeUntil = 0f;
            _blockTimes.Clear();
            _backPressed = false;
            _nextUrlCheck = Time.unscaledTime + 1.5f;
            Debug.Log($"[VRWebPanel] Open {url} (webview state={_browser.state}, had frame={_hadFrame})");
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
            // The trigger press that opened the link must not also tap the page.
            _shownFrame = Time.frameCount;
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
                // Nothing a participant opened (cookies, logins, history,
                // cached pages) carries over to the next tab or participant.
                // Set ClearBrowserDataOnClose to false to keep them.
                if (ClearBrowserDataOnClose)
                {
                    _browser.ClearCookie();
                    _browser.ClearCache(true);
                    _browser.ClearHistory();
                }
            }
            _pendingUrl = null;
            _root.gameObject.SetActive(false);
            IsOpen = false;
            _closedFrame = Time.frameCount;
        }

        private void LogClose()
        {
            float seconds = Time.unscaledTime - _openedAt;
            StudyEventLogger.Instance?.LogEvent("content_browser_close", _sourceId,
                $"seconds={seconds:F1}");
        }

        private void Update()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                Fail(e, _homeUrl, _sourceId);
            }
        }

        private void Tick()
        {
            if (_browser == null)
            {
                return;
            }
            FragmentCapture.GarbageCollect();
            LogStateChange();
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
                    if (!_hadFrame && HasFrame())
                    {
                        _hadFrame = true;
                        Debug.Log($"[VRWebPanel] First frame after {Time.unscaledTime - _initStartedAt:F1}s " +
                                  $"({_view.texture.width}x{_view.texture.height})");
                    }
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
            // A survey or confirmation takes over the participant's view; the
            // exhibit link can be opened again afterwards.
            if (VRSurveyPanel.IsOpen)
            {
                Close();
                return;
            }
            // The plugin never came up, or came up but never drew (a blank
            // panel): hand the link to the system browser.
            if (!_hadFrame && Time.unscaledTime - _openedAt > FirstFrameTimeoutSeconds)
            {
                string reason = _browser.state != FragmentCapture.State.Initialized
                    ? $"WebView did not start (state={_browser.state})"
                    : "WebView started but produced no frame";
                Fail(new TimeoutException(reason), _homeUrl, _sourceId);
                return;
            }
            KeepInView();
            EnforceAllowedSites();
            // A survey/confirm panel owns the trigger while it's open.
            if (!VRSurveyPanel.IsOpen)
            {
                HandlePointer();
            }
        }

        private bool HasFrame()
        {
            // Before the first frame the plugin shows a small grey loading
            // texture; frames arrive in a texture of TexSize.
            Texture t = _view != null ? _view.texture : null;
            return t != null && t.width == TexSize.x && t.height == TexSize.y;
        }

        private void LogStateChange()
        {
            if (_browser.state == _loggedState) return;
            _loggedState = _browser.state;
            Debug.Log($"[VRWebPanel] WebView state {_loggedState} after {Time.unscaledTime - _initStartedAt:F1}s " +
                      $"(gfx={SystemInfo.graphicsDeviceType}, render thread={SystemInfo.renderingThreadingMode})");
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsOpen = false;
            }
        }

        // ------------------------------------------------------------- sites

        // Always allowed besides the exhibits' own sites: Google's document
        // viewer, which shows the PDFs (ViewableUrl).
        private static readonly string[] ExtraHosts = { "docs.google.com" };

        private void BuildAllowedHosts(string openedUrl)
        {
            _allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddHost(openedUrl);
            foreach (var link in FindObjectsByType<ContentLink>(FindObjectsSortMode.None))
            {
                AddHost(link.Url);
            }
            foreach (string h in ExtraHosts)
            {
                _allowedHosts.Add(h);
            }
        }

        private void AddHost(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return;
            string host = StripWww(uri.Host);
            _allowedHosts.Add(host);
            // youtu.be short links resolve to YouTube.
            if (host == "youtu.be") _allowedHosts.Add("youtube.com");
        }

        private static string StripWww(string host)
        {
            return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host.Substring(4) : host;
        }

        /// <summary>A host is allowed if it, or a parent domain, is on the list (m.youtube.com for youtube.com).</summary>
        private bool IsAllowed(string url)
        {
            if (string.IsNullOrEmpty(url) || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                return false;
            }
            string host = StripWww(uri.Host);
            while (true)
            {
                if (_allowedHosts.Contains(host)) return true;
                int dot = host.IndexOf('.');
                if (dot < 0 || host.IndexOf('.', dot + 1) < 0) return false; // keep at least two labels
                host = host.Substring(dot + 1);
            }
        }

        private void EnforceAllowedSites()
        {
            if (_browser.state != FragmentCapture.State.Initialized)
            {
                return;
            }
            if (_noticeUntil > 0f && Time.unscaledTime >= _noticeUntil)
            {
                _noticeUntil = 0f;
                _title.text = HostOf(_lastAllowedUrl ?? _homeUrl ?? "");
            }
            if (Time.unscaledTime < _nextUrlCheck)
            {
                return;
            }
            _nextUrlCheck = Time.unscaledTime + UrlCheckSeconds;

            string current = _browser.GetUrl();
            if (string.IsNullOrEmpty(current) || current == _lastCheckedUrl)
            {
                return;
            }
            // Back from the first page lands on the about:blank the panel
            // starts on; show the exhibit page again instead of a blank panel.
            if (_backPressed && current.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            {
                _backPressed = false;
                _lastCheckedUrl = null;
                _browser.LoadUrl(_homeUrl);
                return;
            }
            _lastCheckedUrl = current;
            _backPressed = false;
            if (IsAllowed(current))
            {
                if (!current.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                {
                    _lastAllowedUrl = current;
                    _title.text = HostOf(current);
                }
                return;
            }

            StudyEventLogger.Instance?.LogEvent("content_browser_blocked", _sourceId, current);
            _blockTimes.Add(Time.unscaledTime);
            _blockTimes.RemoveAll(t => Time.unscaledTime - t > BlockWindowSeconds);
            if (_blockTimes.Count >= MaxBlocksBeforeClose)
            {
                // The page keeps sending the participant off-site; give up on it.
                _blockTimes.Clear();
                Close();
                return;
            }
            _title.text = "That link isn't part of the study";
            _noticeUntil = Time.unscaledTime + NoticeSeconds;
            _browser.LoadUrl(_lastAllowedUrl ?? _homeUrl);
        }

        // ------------------------------------------------------------- input

        private void HandlePointer()
        {
            if (_browser == null || _pageCollider == null || _buttons == null)
            {
                return;
            }
            int hovered = -1;
            bool acceptPress = Time.frameCount != _shownFrame;
            if (Time.unscaledTime >= _nextPointerRefresh)
            {
                _pointers = FindObjectsByType<VRLaserPointer>(FindObjectsSortMode.None);
                _nextPointerRefresh = Time.unscaledTime + 1f;
            }
            foreach (var pointer in _pointers)
            {
                if (pointer == null) continue;
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
                if (!acceptPress || !XRInputBridge.GetTriggerDown(hand))
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
                Button("Back", PageWidth / 2f - ButtonW * 1.5f - 0.03f, barY, font, textMat, GoBack),
                Button("Close", PageWidth / 2f - ButtonW / 2f - 0.015f, barY, font, textMat, Close),
            };
            _root.gameObject.SetActive(false);
        }

        private void GoBack()
        {
            _backPressed = true;
            _browser.GoBack();
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
            _view = viewGo.GetComponent<RawImage>();
            _view.color = Color.white;

            // The WebView lives on this (always active) object, not in the
            // panel: Init() runs the plugin's start-up as a coroutine, and
            // hiding the panel right after building it stopped that
            // coroutine, so the WebView never started and the page stayed
            // blank. It only needs the RawImage to draw into.
            _browser = gameObject.AddComponent<WebView>();
            // The plugin only exposes these as serialized fields (normally
            // set on its prefab). ByteBuffer capture: the HardwareBuffer
            // default is reported blank on Quest with Unity 6 / Vulkan.
            SetField(typeof(FragmentCapture), "m_rawImage", _view);
            SetField(typeof(FragmentCapture), "m_captureMode", CaptureMode.ByteBuffer);
            // A prefab would serialize these; added from code they are null,
            // which breaks start-up (SetIntentFilters(null)) and every page
            // event (DispatchMessageQueue invokes the callbacks unchecked).
            SetField(typeof(Browser), "m_intentFilters", Array.Empty<string>());
            SetField(typeof(Browser), "m_eventCallback", MakeEventCallback());
            _initStartedAt = Time.unscaledTime;
            Debug.Log($"[VRWebPanel] Starting WebView (view {ViewSize}, tex {TexSize}, capture ByteBuffer, " +
                      $"gfx={SystemInfo.graphicsDeviceType})");
            _browser.Init(ViewSize, TexSize, "about:blank", Fps, new Download.Option());
        }

        private EventCallback MakeEventCallback()
        {
            var callback = new EventCallback
            {
                onPageStart = new UnityEngine.Events.UnityEvent<string>(),
                onPageFinish = new UnityEngine.Events.UnityEvent<string>(),
                onDownload = new UnityEngine.Events.UnityEvent<Download.Request>(),
                onDownloadError = new UnityEngine.Events.UnityEvent<Download.EventInfo>(),
                onDownloadStart = new UnityEngine.Events.UnityEvent<Download.EventInfo>(),
                onDownloadFinish = new UnityEngine.Events.UnityEvent<Download.EventInfo>(),
                onDialog = new UnityEngine.Events.UnityEvent<TLab.WebView.Widget.AlertDialog.Init,
                    TLab.WebView.Widget.AlertDialog>(),
            };
            callback.onPageStart.AddListener(url => Debug.Log($"[VRWebPanel] Page start {url}"));
            callback.onPageFinish.AddListener(url => Debug.Log($"[VRWebPanel] Page finish {url}"));
            callback.onDownload.AddListener(req => Debug.Log($"[VRWebPanel] Download ignored {req.url}"));
            return callback;
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
