using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace NSFGrant.Logging
{
    /// <summary>
    /// Uploads the session's CSV files to a configurable HTTP endpoint —
    /// needed for the self-paced web sample (up to ~150 participants), where
    /// nobody can pull files off the participant's machine. Each file is sent
    /// as a POST with the filename in the X-Study-Filename header.
    ///
    /// Leave the endpoint empty for in-lab sessions (files stay on device and
    /// are pulled over adb). When the VERA Unity plugin lands, its ingestion
    /// path can replace or supplement this (see VeraBridge).
    /// </summary>
    public class RemoteDataUploader : MonoBehaviour
    {
        [Tooltip("HTTP(S) endpoint that accepts POSTed CSV files. Empty disables upload.")]
        [SerializeField] private string endpointUrl = "";

        public bool UploadEnabled => !string.IsNullOrEmpty(endpointUrl);

        /// <summary>Uploads every CSV in the StudyData directory.</summary>
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
                byte[] data = File.ReadAllBytes(path);
                using var request = new UnityWebRequest(endpointUrl, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = new UploadHandlerRaw(data);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "text/csv");
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
}
