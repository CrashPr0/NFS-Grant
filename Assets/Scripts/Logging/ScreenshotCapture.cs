using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace NSFGrant.Logging
{
    /// <summary>
    /// Low-rate screenshot capture, the compromise agreed in the VERA meeting
    /// (full video is too heavy; periodic stills are viable). Saves PNGs to
    /// StudyData/screenshots/ with the session time in the filename so frames
    /// can be aligned with the gaze and event logs.
    ///
    /// The whole pipeline is asynchronous: capture and downscale happen on
    /// the GPU, the pixels come back via AsyncGPUReadback (no pipeline
    /// stall), and PNG encoding + the file write run on a worker thread
    /// (ImageConversion.EncodeArrayToPNG is thread-safe). The original
    /// implementation did all of this synchronously on the main thread,
    /// which froze the app for seconds per capture on Quest - and a frozen
    /// VR app keeps compositing with orientation-only reprojection, which
    /// participants experienced as "tracking lost, view zooms and tilts."
    /// Platforms without async readback (WebGL) fall back to a synchronous
    /// read of the ALREADY-DOWNSCALED texture, which is far cheaper than
    /// the old full-resolution path and not headset-critical there anyway.
    ///
    /// Budget storage: at the default 10 s interval a 20-minute session
    /// produces ~120 frames.
    /// </summary>
    public class ScreenshotCapture : MonoBehaviour
    {
        [SerializeField] private bool captureEnabled = false;

        [Tooltip("Seconds between captures.")]
        [SerializeField] private float intervalSeconds = 10f;

        [Tooltip("Downscale factor (1 = full resolution, 2 = half, ...).")]
        [Range(1, 4)]
        [SerializeField] private int downscale = 2;

        private string _directory;
        private string _participantId = "P000";
        private Coroutine _loop;

        public void StartCapture(string participantId)
        {
            if (!captureEnabled)
            {
                return;
            }

            _participantId = participantId;
            _directory = StudyPaths.ScreenshotsDir;
            Directory.CreateDirectory(_directory);
            _loop = StartCoroutine(CaptureLoop());
        }

        public void StopCapture()
        {
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }
        }

        private IEnumerator CaptureLoop()
        {
            var wait = new WaitForSecondsRealtime(intervalSeconds);
            while (true)
            {
                // Must capture at end-of-frame so the composited image exists.
                yield return new WaitForEndOfFrame();
                CaptureFrame();
                yield return wait;
            }
        }

        private void CaptureFrame()
        {
            int w = Screen.width;
            int h = Screen.height;
            var full = RenderTexture.GetTemporary(w, h, 0);
            ScreenCapture.CaptureScreenshotIntoRenderTexture(full);

            int sw = Mathf.Max(1, w / downscale);
            int sh = Mathf.Max(1, h / downscale);
            var small = RenderTexture.GetTemporary(sw, sh, 0);
            // Downscale on the GPU. On top-origin graphics APIs
            // (Vulkan/Metal/DX) the captured RT is vertically flipped
            // relative to what readback returns; the mirrored blit
            // compensates so files come out right-side up.
            if (SystemInfo.graphicsUVStartsAtTop)
            {
                Graphics.Blit(full, small, new Vector2(1f, -1f), new Vector2(0f, 1f));
            }
            else
            {
                Graphics.Blit(full, small);
            }
            RenderTexture.ReleaseTemporary(full);

            string path = Path.Combine(_directory,
                $"shot_{_participantId}_{Time.unscaledTime:F1}s.png");

            if (SystemInfo.supportsAsyncGPUReadback)
            {
                AsyncGPUReadback.Request(small, 0, TextureFormat.RGBA32, request =>
                {
                    RenderTexture.ReleaseTemporary(small);
                    if (request.hasError)
                    {
                        Debug.LogWarning("[ScreenshotCapture] Async GPU readback failed; frame skipped.");
                        return;
                    }

                    byte[] raw = request.GetData<byte>().ToArray();
                    uint rw = (uint)request.width;
                    uint rh = (uint)request.height;
                    Task.Run(() =>
                    {
                        try
                        {
                            byte[] png = ImageConversion.EncodeArrayToPNG(
                                raw, GraphicsFormat.R8G8B8A8_UNorm, rw, rh);
                            File.WriteAllBytes(path, png);
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"[ScreenshotCapture] Encode/write failed: {e.Message}");
                        }
                    });
                });
            }
            else
            {
                // WebGL etc: synchronous, but only over the small texture.
                var prev = RenderTexture.active;
                RenderTexture.active = small;
                var tex = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, sw, sh), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(small);

                File.WriteAllBytes(path, tex.EncodeToPNG());
                Destroy(tex);
            }
        }
    }
}
