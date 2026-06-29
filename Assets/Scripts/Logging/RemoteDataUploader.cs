using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace NSFGrant.Logging
{
    /// <summary>
    /// Uploads the session's files (the CSV logs, and the screenshot PNGs when
    /// capture is on) to a configurable HTTP endpoint — needed for the
    /// self-paced web sample (up to ~150 participants), where nobody can pull
    /// files off the participant's machine.
    ///
    /// Designed to connect to <b>Google Drive</b> via a Google Apps Script Web
    /// App (see docs/GOOGLE_DRIVE_SETUP.md): each file is POSTed with the name
    /// and content type as query parameters (?name=...&amp;type=...&amp;token=...)
    /// and the bytes base64-encoded in the body — the form Apps Script can read,
    /// since it cannot access custom headers or raw binary. The script runs as
    /// you and writes to your Drive, so no Google credentials live in the build.
    /// A plain HTTP server works too; it just base64-decodes the body.
    ///
    /// Leave the endpoint empty for in-lab sessions (files stay on device and
    /// are pulled over adb). When the VERA Unity plugin lands, its ingestion
    /// path can replace or supplement this (see VeraBridge).
    /// </summary>
    public class RemoteDataUploader : MonoBehaviour
    {
        [Tooltip("Google Apps Script Web App URL (or any HTTP endpoint). Empty disables upload.")]
        [SerializeField] private string endpointUrl = "";

        [Tooltip("Optional shared secret sent as ?token=...; must match the receiver script.")]
        [SerializeField] private string sharedToken = "";

        [Tooltip("Also upload the low-rate screenshot PNGs (if capture is enabled).")]
        [SerializeField] private bool uploadScreenshots = true;

        public bool UploadEnabled => !string.IsNullOrEmpty(endpointUrl);

        /// <summary>Uploads every CSV (and, if enabled, screenshot) in StudyData.</summary>
        public void UploadSessionFiles()
        {
            if (!UploadEnabled)
            {
                return;
            }
            StartCoroutine(UploadAll());
        }

        private IEnumerator UploadAll()
        {
            string dir = StudyPaths.Root;
            if (!Directory.Exists(dir))
            {
                yield break;
            }

            foreach (string path in Directory.GetFiles(dir, "*.csv"))
            {
                yield return UploadFile(path, "text/csv");
            }

            string shotDir = StudyPaths.ScreenshotsDir;
            if (uploadScreenshots && Directory.Exists(shotDir))
            {
                foreach (string path in Directory.GetFiles(shotDir, "*.png"))
                {
                    yield return UploadFile(path, "image/png");
                }
            }
        }

        private IEnumerator UploadFile(string path, string contentType)
        {
            byte[] data = File.ReadAllBytes(path);
            string fileName = Path.GetFileName(path);

            // name/type/token as query params; bytes base64-encoded in the body
            // so a Google Apps Script Web App can read them (it can't see custom
            // headers or raw binary). A plain server just base64-decodes.
            string url = endpointUrl + (endpointUrl.Contains("?") ? "&" : "?")
                + "name=" + UnityWebRequest.EscapeURL(fileName)
                + "&type=" + UnityWebRequest.EscapeURL(contentType);
            if (!string.IsNullOrEmpty(sharedToken))
            {
                url += "&token=" + UnityWebRequest.EscapeURL(sharedToken);
            }

            byte[] body = Encoding.UTF8.GetBytes(Convert.ToBase64String(data));
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "text/plain");
            // Kept for plain servers that prefer a header; Apps Script ignores it.
            request.SetRequestHeader("X-Study-Filename", fileName);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[RemoteDataUploader] Uploaded {Path.GetFileName(path)}");
            }
            else
            {
                Debug.LogWarning(
                    $"[RemoteDataUploader] Failed to upload {Path.GetFileName(path)}: {request.error}");
            }
        }
    }
}
