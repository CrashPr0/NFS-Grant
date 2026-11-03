using System;
using System.Runtime.InteropServices;
using UnityEngine;
using NSFGrant.Logging;

namespace NSFGrant.Core
{
    /// <summary>
    /// Shows a link or PDF to the participant, wherever they are.
    ///
    /// Desktop / flat browser: a NEW browser tab, so the study page (and its
    /// session) stays open (Application.OpenURL navigated the study page
    /// itself in the WebGL build). Elsewhere (editor, native Quest, where
    /// the system browser overlays the app): Application.OpenURL.
    ///
    /// Headset in a browser (WebXR): an immersive session draws only the
    /// WebGL scene - no page, PDF or tab can appear inside it, and a tab
    /// opened from VR is invisible. So the immersive session is ended and the
    /// page opens in an in-page reader (NSFGrantReader.jslib) the headset's
    /// flat browser view shows; the participant presses VR to come back.
    /// Because that drops them out of VR, <see cref="Open"/> only QUEUES the
    /// request there; VRSurveyPanel asks "Leave VR to open this?" and calls
    /// <see cref="ConfirmPending"/>. PDFs are the exception: PdfBridge draws
    /// them on a panel inside the headset, so they never leave VR (unless
    /// the PDF can't be loaded - see VRSurveyPanel's "Open in browser").
    /// </summary>
    public static class BrowserTab
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void NSFGrantOpenUrlInNewTab(string url);

        [DllImport("__Internal")]
        private static extern void NSFGrantShowReader(string frameUrl, string openUrl, string title);
#endif

        /// <summary>True in a browser build (new tabs and the reader exist).</summary>
        public static bool IsSupported => Application.platform == RuntimePlatform.WebGLPlayer;

        /// <summary>A headset link request waiting for the participant's OK (else null).</summary>
        public static string PendingUrl { get; private set; }
        public static string PendingTitle { get; private set; }

        public static void Open(string url, string title = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }
            if (IsSupported && PlatformDetector.IsXRActive)
            {
                // A PDF can be read right inside the headset (PdfBridge), so
                // it needs no trip out of VR. Anything else can't.
                if (PdfBridge.CanOpen(url))
                {
                    PdfBridge.Open(url, title);
                    return;
                }
                PendingUrl = url;
                PendingTitle = title;
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            NSFGrantOpenUrlInNewTab(url);
#else
            Application.OpenURL(url);
#endif
        }

        /// <summary>The participant agreed to leave VR for the queued link.</summary>
        public static void ConfirmPending()
        {
            string url = PendingUrl, title = PendingTitle;
            CancelPending();
            OpenReader(url, title);
        }

        public static void CancelPending()
        {
            PendingUrl = null;
            PendingTitle = null;
        }

        /// <summary>
        /// Ends any immersive session and shows <paramref name="url"/> in the
        /// in-page reader (browser builds); elsewhere just opens it.
        /// </summary>
        public static void OpenReader(string url, string title = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }
            string frameUrl = ToFrameable(url);
            StudyEventLogger.Instance?.LogEvent("link_reader_opened", "",
                $"left_vr={(PlatformDetector.IsXRActive ? 1 : 0)};" +
                $"framed={(frameUrl.Length > 0 ? 1 : 0)};url={url}");
#if UNITY_WEBGL && !UNITY_EDITOR
            NSFGrant.WebXRAdapter.WebXRInput.ExitVR();
            NSFGrantShowReader(frameUrl, url, title ?? "");
#else
            Application.OpenURL(url);
#endif
        }

        // Exhibit sites whose pages send X-Frame-Options: SAMEORIGIN, so a
        // frame of them stays blank (checked 2026-11-03 against every link in
        // SdgContentLibrary). The browser hides a refused frame from the page,
        // so this can't be detected at run time; re-check when links change.
        // PDFs on these hosts are fine (no header on the files themselves).
        private static readonly string[] FrameBlockingHosts =
        {
            "sdgs.un.org", "unsdglearn.org", "unrisd.org", "ala.fi",
        };

        /// <summary>
        /// The URL to show in the reader's frame. YouTube watch pages refuse
        /// to be framed; their /embed/ form works. Sites known to refuse get
        /// "" so the reader shows an "Open in new tab" notice instead of a
        /// blank frame. Everything else is framed as given.
        /// </summary>
        internal static string ToFrameable(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return url;
            }
            string host = uri.Host.ToLowerInvariant();
            if (!uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string blocked in FrameBlockingHosts)
                {
                    if (host == blocked || host.EndsWith("." + blocked, StringComparison.Ordinal))
                    {
                        return "";
                    }
                }
            }
            string id = null;
            if (host == "youtu.be")
            {
                id = uri.AbsolutePath.Trim('/');
            }
            else if (host.EndsWith("youtube.com") && uri.AbsolutePath == "/watch")
            {
                foreach (string pair in uri.Query.TrimStart('?').Split('&'))
                {
                    if (pair.StartsWith("v=", StringComparison.Ordinal))
                    {
                        id = pair.Substring(2);
                        break;
                    }
                }
            }
            return string.IsNullOrEmpty(id) ? url
                : "https://www.youtube.com/embed/" + Uri.EscapeDataString(id);
        }
    }
}
