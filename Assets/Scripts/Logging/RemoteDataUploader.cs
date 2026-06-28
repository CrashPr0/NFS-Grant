using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace NSFGrant.Logging
{
    /// <summary>
    /// Uploads the session's files to a configurable HTTP endpoint — needed
    /// for the self-paced web sample (up to ~150 participants), where nobody
    /// can pull files off the participant's machine. Each file is sent as a
    /// POST with the filename in the X-Study-Filename header: the CSV logs and,
    /// when screenshot capture is on, the periodic PNG stills.
    ///
    /// To land the files in Google Drive, point <c>endpointUrl</c> at a
    /// Drive-backed receiver (e.g. a Google Apps Script Web App that writes the
    /// POST body to a Drive folder, keyed by X-Study-Filename). No Google
    /// credentials live in the build.
    ///
    /// Leave the endpoint empty for in-lab sessions (files stay on device and
    /// are pulled over adb). When the VERA Unity plugin lands, its ingestion
    /// path can replace or supplement this (see VeraBridge).
    /// </summary>
    public class RemoteDataUploader : MonoBehaviour
    {
        [Tooltip("HTTP(S) endpoint that accepts POSTed session files. Empty disables upload.")]
        [SerializeField] private string endpointUrl = "";

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
            string dir = Path.Combine(Application.persistentDataPath, "StudyData");
            if (!Directory.Exists(dir))
            {
                yield break;
            }

            foreach (string path in Directory.GetFiles(dir, "*.csv"))
            {
                yield return UploadFile(path, "text/csv");
            }

            string shotDir = Path.Combine(dir, "screenshots");
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
            using var request = new UnityWebRequest(endpointUrl, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(data);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", contentType);
            request.SetRequestHeader("X-Study-Filename", Path.GetFileName(path));

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
