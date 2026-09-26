using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace NSFGrant.Logging
{
    /// <summary>
    /// Uploads the session's files (the CSV logs, and the screenshot PNGs when
    /// capture is on) to a configurable HTTP endpoint - needed for the
    /// self-paced web sample, where nobody can pull files off the
    /// participant's machine.
    ///
    /// Designed for <b>Google Drive</b> via the Google Apps Script Web App in
    /// tools/drive-upload/Code.gs (setup: docs/GOOGLE_DRIVE_SETUP.md). The
    /// script runs as you and writes to your Drive, so no Google credentials
    /// live in the build.
    ///
    /// Request shape (kept to a CORS "simple request" - no custom headers,
    /// Content-Type text/plain - because Apps Script cannot answer a CORS
    /// preflight, so anything else fails from a browser):
    ///   POST {endpoint}?name=&amp;type=&amp;pid=&amp;enc=gzip|none&amp;token=
    ///   body = base64(file bytes, gzipped when enc=gzip)
    ///
    /// When uploads happen:
    ///   - a checkpoint every <see cref="checkpointIntervalSeconds"/> during
    ///     the session, and whenever the page/app loses focus or is paused
    ///     (tab hidden, headset removed) - so a participant who closes the
    ///     tab early still leaves most of their data behind;
    ///   - a final upload when the session stops, retried until it succeeds
    ///     while the page stays open.
    /// CSVs are append-only logs, so every checkpoint is a prefix of the
    /// final file; the script enforces that (an upload may only extend a
    /// stored file), which also means a leaked token can't alter or erase
    /// data. Unchanged files are skipped; PNGs are uploaded once each.
    ///
    /// Configured at scene-build time from upload.config.json (see
    /// DiscoveryHallBuilder / UploadConfig). By default only the WebGL build
    /// uploads; lab Quest data is pulled over adb.
    /// </summary>
    public class RemoteDataUploader : MonoBehaviour
    {
        public enum UploadStatus { Disabled, Idle, Uploading, Done, Failed }

        [Tooltip("Google Apps Script Web App URL (or any HTTP endpoint). Empty disables upload.")]
        [SerializeField] private string endpointUrl = "";

        [Tooltip("Shared token sent as ?token=... (a spam gate, not a secret: it ships in the build).")]
        [SerializeField] private string sharedToken = "";

        [Tooltip("Also upload the low-rate screenshot PNGs (if capture is enabled).")]
        [SerializeField] private bool uploadScreenshots = true;

        [Tooltip("Seconds between in-session checkpoint uploads (0 = final upload only).")]
        [SerializeField] private float checkpointIntervalSeconds = 120f;

        [Header("Which builds upload")]
        [SerializeField] private bool uploadOnWebGL = true;
        [SerializeField] private bool uploadOnAndroid = false;
        [SerializeField] private bool uploadOnStandalone = false;
        [SerializeField] private bool uploadInEditor = false;

        private const int MaxAttempts = 3;
        private const float FinalRetrySeconds = 30f;

        /// <summary>Upload state of the finished session (for the completion screens).</summary>
        public static UploadStatus FinalStatus { get; private set; } = UploadStatus.Disabled;

        public bool UploadEnabled => !string.IsNullOrEmpty(endpointUrl) && PlatformAllowed;

        private bool PlatformAllowed
        {
            get
            {
#if UNITY_EDITOR
                return uploadInEditor;
#elif UNITY_WEBGL
                return uploadOnWebGL;
#elif UNITY_ANDROID
                return uploadOnAndroid;
#else
                return uploadOnStandalone;
#endif
            }
        }

        private DateTime? _sessionStartUtc;
        private string _participantId = "";
        private float _nextCheckpoint = float.MaxValue;
        private bool _busy;
        private bool _finalRequested;
        private bool _finalDone;
        private float _nextFinalRetry = float.MaxValue;
        // Size last uploaded per file; unchanged files are skipped.
        private readonly Dictionary<string, long> _uploadedLength = new Dictionary<string, long>();

        /// <summary>Editor/builder: set the endpoint and token.</summary>
        public void Configure(string url, string token, float checkpointSeconds)
        {
            endpointUrl = url ?? "";
            sharedToken = token ?? "";
            if (checkpointSeconds > 0f)
            {
                checkpointIntervalSeconds = checkpointSeconds;
            }
        }

        /// <summary>Called by SessionController when a session starts.</summary>
        public void BeginSession(DateTime sessionStartUtc, string participantId)
        {
            _sessionStartUtc = sessionStartUtc;
            _participantId = StudyPaths.FileToken(participantId);
            _uploadedLength.Clear();
            _finalRequested = false;
            _finalDone = false;
            _nextFinalRetry = float.MaxValue;
            FinalStatus = UploadEnabled ? UploadStatus.Idle : UploadStatus.Disabled;
            _nextCheckpoint = checkpointIntervalSeconds > 0f
                ? Time.unscaledTime + checkpointIntervalSeconds
                : float.MaxValue;
        }

        /// <summary>
        /// Final upload of the session's files (called at StopSession).
        /// Retries until it succeeds while the page stays open.
        /// </summary>
        public void UploadSessionFiles(DateTime sessionStartUtc)
        {
            if (!UploadEnabled)
            {
                FinalStatus = UploadStatus.Disabled;
                return;
            }
            _sessionStartUtc ??= sessionStartUtc;
            _finalRequested = true;
            _nextCheckpoint = float.MaxValue;
            FinalStatus = UploadStatus.Uploading;
            TryRun();
        }

        private void Update()
        {
            if (!UploadEnabled || _sessionStartUtc == null)
            {
                return;
            }
            if (!_finalRequested && Time.unscaledTime >= _nextCheckpoint)
            {
                _nextCheckpoint = Time.unscaledTime + checkpointIntervalSeconds;
                TryRun();
            }
            if (_finalRequested && !_finalDone && !_busy && Time.unscaledTime >= _nextFinalRetry)
            {
                TryRun();
            }
        }

        // Tab hidden / window blurred (WebGL), headset removed (Quest): the
        // participant may be about to leave, so push what we have now.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) Checkpoint();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Checkpoint();
        }

        private void Checkpoint()
        {
            if (UploadEnabled && _sessionStartUtc != null && !_finalRequested)
            {
                TryRun();
            }
        }

        private void TryRun()
        {
            if (_busy)
            {
                return; // the running pass picks up _finalRequested when it ends
            }
            StartCoroutine(UploadAll());
        }

        private IEnumerator UploadAll()
        {
            _busy = true;
            bool final = _finalRequested;
            bool allOk = true;

            DateTime since = (_sessionStartUtc ?? DateTime.UtcNow).AddSeconds(-1);
            var files = new List<(string path, string type, bool immutable)>();
            string dir = StudyPaths.Root;
            if (Directory.Exists(dir))
            {
                foreach (string path in Directory.GetFiles(dir, "*.csv"))
                {
                    if (File.GetLastWriteTimeUtc(path) >= since) files.Add((path, "text/csv", false));
                }
            }
            string shotDir = StudyPaths.ScreenshotsDir;
            if (uploadScreenshots && Directory.Exists(shotDir))
            {
                foreach (string path in Directory.GetFiles(shotDir, "*.png"))
                {
                    if (File.GetLastWriteTimeUtc(path) >= since) files.Add((path, "image/png", true));
                }
            }

            foreach (var (path, type, immutable) in files)
            {
                long length;
                try { length = new FileInfo(path).Length; }
                catch { continue; }
                if (_uploadedLength.TryGetValue(path, out long done) && (immutable || done == length))
                {
                    continue; // unchanged since the last successful upload
                }

                bool ok = false;
                for (int attempt = 1; attempt <= MaxAttempts && !ok; attempt++)
                {
                    var result = new bool[1];
                    yield return UploadFile(path, type, result);
                    ok = result[0];
                    if (!ok && attempt < MaxAttempts)
                    {
                        yield return new WaitForSecondsRealtime(2f * attempt);
                    }
                }
                if (ok)
                {
                    _uploadedLength[path] = length;
                }
                allOk &= ok;
            }

            _busy = false;

            if (final)
            {
                if (allOk)
                {
                    _finalDone = true;
                    FinalStatus = UploadStatus.Done;
                    _sessionStartUtc = null;
                    Debug.Log("[RemoteDataUploader] Final upload complete.");
                }
                else
                {
                    FinalStatus = UploadStatus.Failed;
                    _nextFinalRetry = Time.unscaledTime + FinalRetrySeconds;
                    Debug.LogWarning($"[RemoteDataUploader] Final upload incomplete; retrying in {FinalRetrySeconds:F0}s.");
                }
            }
            else if (_finalRequested)
            {
                // StopSession arrived while this checkpoint was running.
                TryRun();
            }
        }

        private IEnumerator UploadFile(string path, string contentType, bool[] result)
        {
            byte[] data;
            try
            {
                // Loggers keep their file open for writing; open shared so
                // the read works on desktop OSes too (WebGL has no locking).
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var ms = new MemoryStream();
                fs.CopyTo(ms);
                data = ms.ToArray();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RemoteDataUploader] Can't read {Path.GetFileName(path)}: {e.Message}");
                result[0] = false;
                yield break;
            }

            // Text logs gzip ~5x; fall back to plain if compression isn't
            // available on this platform.
            string enc = "none";
            if (contentType == "text/csv")
            {
                byte[] gz = TryGzip(data);
                if (gz != null)
                {
                    data = gz;
                    enc = "gzip";
                }
            }

            string fileName = Path.GetFileName(path);
            string url = endpointUrl + (endpointUrl.Contains("?") ? "&" : "?")
                + "name=" + UnityWebRequest.EscapeURL(fileName)
                + "&type=" + UnityWebRequest.EscapeURL(contentType)
                + "&pid=" + UnityWebRequest.EscapeURL(_participantId)
                + "&enc=" + enc;
            if (!string.IsNullOrEmpty(sharedToken))
            {
                url += "&token=" + UnityWebRequest.EscapeURL(sharedToken);
            }

            byte[] body = Encoding.ASCII.GetBytes(Convert.ToBase64String(data));
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            // text/plain + no custom headers = no CORS preflight.
            request.SetRequestHeader("Content-Type", "text/plain");
            request.timeout = 120;

            yield return request.SendWebRequest();

            string reply = request.downloadHandler?.text ?? "";
            bool ok = request.result == UnityWebRequest.Result.Success && reply.StartsWith("ok");
            if (ok)
            {
                Debug.Log($"[RemoteDataUploader] Uploaded {fileName} ({data.Length / 1024} KB, {enc}).");
            }
            else
            {
                Debug.LogWarning($"[RemoteDataUploader] Upload of {fileName} failed: " +
                                 $"{request.error} {reply}".Trim());
            }
            result[0] = ok;
        }

        private static byte[] TryGzip(byte[] data)
        {
            try
            {
                using var output = new MemoryStream();
                using (var gz = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                {
                    gz.Write(data, 0, data.Length);
                }
                return output.ToArray();
            }
            catch (Exception e)
            {
                Debug.Log($"[RemoteDataUploader] gzip unavailable ({e.GetType().Name}); sending uncompressed.");
                return null;
            }
        }
    }
}
