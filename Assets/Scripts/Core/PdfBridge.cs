using System;
using System.Runtime.InteropServices;
using UnityEngine;
using NSFGrant.Logging;

namespace NSFGrant.Core
{
    /// <summary>
    /// Reads a PDF inside a WebXR headset session. The browser can't show a
    /// PDF (or any page) inside an immersive session, so pdf.js draws the
    /// current page onto an offscreen canvas (NSFGrantPdf.jslib) and
    /// <see cref="Tick"/> copies it into <see cref="Texture"/>, which
    /// VRSurveyPanel shows on its panel with Previous / Next / Close.
    ///
    /// Only exists in a browser build; everywhere else <see cref="CanOpen"/>
    /// is false and callers use their normal link path.
    /// </summary>
    public static class PdfBridge
    {
        public enum Status { Idle, Loading, Ready, Error }

        /// <summary>Texture size; ~A4-ish portrait, sharp enough for body text on a Quest.</summary>
        public const int TexWidth = 1280;
        public const int TexHeight = 1792;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void NSFGrantPdfOpen(string url, string mirrorName, int w, int h);
        [DllImport("__Internal")] private static extern int NSFGrantPdfState();
        [DllImport("__Internal")] private static extern int NSFGrantPdfPageCount();
        [DllImport("__Internal")] private static extern int NSFGrantPdfPageReady();
        [DllImport("__Internal")] private static extern void NSFGrantPdfGoTo(int page);
        [DllImport("__Internal")] private static extern void NSFGrantPdfSetZoom(float zoom);
        [DllImport("__Internal")] private static extern void NSFGrantPdfPan(int dx, int dy);
        [DllImport("__Internal")] private static extern int NSFGrantPdfUpload(int textureId);
        [DllImport("__Internal")] private static extern void NSFGrantPdfClose();
#endif

        private static Texture2D _texture;

        /// <summary>True while a PDF is open (loading, shown or failed).</summary>
        public static bool IsActive => Url != null;

        public static string Url { get; private set; }
        public static string Title { get; private set; }
        public static int Page { get; private set; }
        public static int PageCount { get; private set; }
        public static Status State { get; private set; } = Status.Idle;

        /// <summary>Zoom steps; 1 shows the whole page.</summary>
        public static readonly float[] ZoomLevels = { 1f, 1.5f, 2f, 3f };
        public static int ZoomIndex { get; private set; }
        public static float Zoom => ZoomLevels[ZoomIndex];
        public static bool CanZoomIn => ZoomIndex < ZoomLevels.Length - 1;
        public static bool CanZoomOut => ZoomIndex > 0;

        /// <summary>The page image; valid once <see cref="State"/> is Ready.</summary>
        public static Texture2D Texture => _texture;

        /// <summary>True when <paramref name="url"/> is a PDF this bridge can show (browser builds only).</summary>
        public static bool CanOpen(string url)
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer ||
                string.IsNullOrEmpty(url) ||
                !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }
            return uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        public static void Open(string url, string title = null)
        {
            if (!CanOpen(url))
            {
                return;
            }
            Close();

            string name = FileName(url);
            Url = url;
            Title = !string.IsNullOrEmpty(title) ? title : name.Replace('_', ' ').Replace('-', ' ');
            Page = 1;
            PageCount = 0;
            ZoomIndex = 0;
            State = Status.Loading;

            if (_texture == null)
            {
                _texture = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false)
                {
                    name = "PdfPage",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var white = new Color32[TexWidth * TexHeight];
                for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
                _texture.SetPixels32(white);
                _texture.Apply(false, false); // keep it GPU-resident; JS writes into it
            }

            StudyEventLogger.Instance?.LogEvent("pdf_open", "", $"url={url}");
#if UNITY_WEBGL && !UNITY_EDITOR
            NSFGrantPdfOpen(url, name, TexWidth, TexHeight);
#endif
        }

        public static void GoTo(int page)
        {
            if (State != Status.Ready || page < 1 || page > PageCount || page == Page)
            {
                return;
            }
            Page = page;
            StudyEventLogger.Instance?.LogEvent("pdf_page", "", $"page={page}/{PageCount};url={Url}");
#if UNITY_WEBGL && !UNITY_EDITOR
            NSFGrantPdfGoTo(page);
#endif
        }

        public static void ZoomIn() => SetZoomIndex(ZoomIndex + 1);
        public static void ZoomOut() => SetZoomIndex(ZoomIndex - 1);

        private static void SetZoomIndex(int index)
        {
            index = Mathf.Clamp(index, 0, ZoomLevels.Length - 1);
            if (State != Status.Ready || index == ZoomIndex)
            {
                return;
            }
            ZoomIndex = index;
            StudyEventLogger.Instance?.LogEvent("pdf_zoom", "", $"zoom={Zoom:0.0};page={Page}/{PageCount};url={Url}");
#if UNITY_WEBGL && !UNITY_EDITOR
            NSFGrantPdfSetZoom(Zoom);
#endif
        }

        /// <summary>Moves the zoomed view half a window; dx right is +1, dy down is +1.</summary>
        public static void Pan(int dx, int dy)
        {
            if (State != Status.Ready || ZoomIndex == 0)
            {
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            NSFGrantPdfPan(dx, dy);
#endif
        }

        public static void Next() => GoTo(Page + 1);
        public static void Previous() => GoTo(Page - 1);

        public static void Close()
        {
            if (Url == null)
            {
                return;
            }
            StudyEventLogger.Instance?.LogEvent("pdf_close", "", $"page={Page}/{PageCount};url={Url}");
            Url = null;
            Title = null;
            Page = 0;
            PageCount = 0;
            State = Status.Idle;
#if UNITY_WEBGL && !UNITY_EDITOR
            NSFGrantPdfClose();
#endif
        }

        /// <summary>
        /// Call once per frame while a PDF is open: follows the loader's
        /// progress and copies a freshly drawn page into the texture.
        /// </summary>
        public static void Tick()
        {
            if (Url == null)
            {
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            if (State == Status.Loading)
            {
                int s = NSFGrantPdfState();
                if (s == 2)
                {
                    PageCount = NSFGrantPdfPageCount();
                    State = Status.Ready;
                }
                else if (s == 3)
                {
                    State = Status.Error;
                    StudyEventLogger.Instance?.LogEvent("pdf_error", "", $"url={Url}");
                }
            }
            else if (State == Status.Ready)
            {
                if (NSFGrantPdfState() == 3)
                {
                    State = Status.Error;
                    StudyEventLogger.Instance?.LogEvent("pdf_error", "", $"url={Url}");
                }
                else if (NSFGrantPdfPageReady() == 1)
                {
                    NSFGrantPdfUpload((int)_texture.GetNativeTexturePtr());
                }
            }
#endif
        }

        private static string FileName(string url)
        {
            Uri.TryCreate(url, UriKind.Absolute, out var uri);
            string last = uri != null ? uri.AbsolutePath.Substring(uri.AbsolutePath.LastIndexOf('/') + 1) : url;
            string name = Uri.UnescapeDataString(last);
            return name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - 4) + ".pdf"
                : name;
        }
    }
}
