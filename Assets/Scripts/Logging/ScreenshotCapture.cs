using System.Collections;
using System.IO;
using UnityEngine;

namespace NSFGrant.Logging
{
    /// <summary>
    /// Low-rate screenshot capture, the compromise agreed in the VERA meeting
    /// (full video is too heavy; periodic stills are viable). Saves PNGs to
    /// StudyData/screenshots/ with the session time in the filename so frames
    /// can be aligned with the gaze and event logs.
    ///
    /// Disabled by default — enable per study protocol, and budget storage:
    /// at the default 10 s interval a 20-minute session produces ~120 frames.
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
            _directory = Path.Combine(Application.persistentDataPath, "StudyData", "screenshots");
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
                yield return new WaitForEndOfFrame();

                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture(1);
                try
                {
                    Texture2D output = downscale > 1 ? Downscale(tex, downscale) : tex;
                    byte[] png = output.EncodeToPNG();
                    string name = $"shot_{_participantId}_{Time.unscaledTime:F1}s.png";
                    File.WriteAllBytes(Path.Combine(_directory, name), png);
                    if (output != tex)
                    {
                        Destroy(output);
                    }
                }
                finally
                {
                    Destroy(tex);
                }

                yield return wait;
            }
        }

        private static Texture2D Downscale(Texture2D source, int factor)
        {
            int w = Mathf.Max(1, source.width / factor);
            int h = Mathf.Max(1, source.height / factor);

            var rt = RenderTexture.GetTemporary(w, h);
            Graphics.Blit(source, rt);

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var result = new Texture2D(w, h, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }
    }
}
