using System;
using System.IO;
using UnityEngine;
using NSFGrant.Logging;

namespace NSFGrant.EditorTools
{
    /// <summary>
    /// Build-time source of the web-upload endpoint. The scene is regenerated
    /// for every build, so the endpoint can't live in the scene; it lives in
    /// <c>upload.config.json</c> at the repository root (gitignored - copy
    /// upload.config.example.json), or in the NSF_UPLOAD_URL /
    /// NSF_UPLOAD_TOKEN environment variables, which win when set.
    ///
    /// Note: whatever is configured here ships inside the player build; for
    /// the public WebXR site the token is a spam gate, not a secret. The Apps
    /// Script (tools/drive-upload/Code.gs) is what protects stored data.
    /// </summary>
    public static class UploadConfig
    {
        public const string FileName = "upload.config.json";

        [Serializable]
        private class Data
        {
            public string endpointUrl;
            public string sharedToken;
            public float checkpointSeconds;
        }

        /// <summary>Applies the config to an uploader; returns true when an endpoint is set.</summary>
        public static bool ApplyTo(RemoteDataUploader uploader)
        {
            var data = Load();
            uploader.Configure(data.endpointUrl, data.sharedToken, data.checkpointSeconds);
            bool configured = !string.IsNullOrEmpty(data.endpointUrl);
            if (configured)
            {
                Debug.Log($"[UploadConfig] Web uploads -> {Redact(data.endpointUrl)} " +
                          $"(token {(string.IsNullOrEmpty(data.sharedToken) ? "none" : "set")}).");
            }
            else
            {
                Debug.LogWarning("[UploadConfig] No upload endpoint (upload.config.json / NSF_UPLOAD_URL). " +
                                 "Web builds will NOT upload participant data. See docs/GOOGLE_DRIVE_SETUP.md.");
            }
            return configured;
        }

        public static bool IsConfigured() => !string.IsNullOrEmpty(Load().endpointUrl);

        private static Data Load()
        {
            var data = new Data();
            string path = Path.Combine(Directory.GetCurrentDirectory(), FileName);
            if (File.Exists(path))
            {
                try
                {
                    data = JsonUtility.FromJson<Data>(File.ReadAllText(path)) ?? new Data();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UploadConfig] Can't parse {FileName}: {e.Message}");
                }
            }
            string envUrl = Environment.GetEnvironmentVariable("NSF_UPLOAD_URL");
            string envToken = Environment.GetEnvironmentVariable("NSF_UPLOAD_TOKEN");
            if (!string.IsNullOrEmpty(envUrl)) data.endpointUrl = envUrl;
            if (!string.IsNullOrEmpty(envToken)) data.sharedToken = envToken;
            return data;
        }

        // Log the host only; the full /exec URL is effectively a write key.
        private static string Redact(string url)
        {
            try { return new Uri(url).Host + "/..."; }
            catch { return "(invalid URL)"; }
        }
    }
}
