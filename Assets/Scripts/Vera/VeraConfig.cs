using System;
using System.IO;
using UnityEngine;
using NSFGrant.Logging;

namespace NSFGrant.Vera
{
    /// <summary>
    /// Holds the VERA portal connection settings (experiment ID + API key)
    /// without ever committing credentials to the repository. Settings are
    /// loaded, in priority order, from:
    ///   1. vera_credentials.json in the study data folder (StudyPaths.Root)
    ///   2. vera_credentials.json in Application.persistentDataPath
    ///   3. The Inspector fields below (experiment ID only — never paste the
    ///      API key into a serialized field, it would end up in the scene
    ///      file, which is committed).
    ///
    /// File format:
    ///   { "portalBaseUrl": "https://vera-xr.io",
    ///     "experimentId": "your-experiment-id",
    ///     "apiKey": "your-api-key" }
    ///
    /// vera_credentials.json is in .gitignore; keep it that way.
    /// </summary>
    public class VeraConfig : MonoBehaviour
    {
        private const string CredentialsFileName = "vera_credentials.json";

        [Tooltip("Portal base URL; override only for a staging portal.")]
        [SerializeField] private string portalBaseUrl = "https://vera-xr.io";

        [Tooltip("VERA experiment ID (safe to serialize; it is not a secret).")]
        [SerializeField] private string experimentId = "";

        public string PortalBaseUrl => Loaded?.portalBaseUrl ?? portalBaseUrl;
        public string ExperimentId =>
            string.IsNullOrEmpty(Loaded?.experimentId) ? experimentId : Loaded.experimentId;
        public string ApiKey => Loaded?.apiKey ?? "";

        /// <summary>True once an API key has been found on disk.</summary>
        public bool IsConfigured =>
            !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(ExperimentId);

        [Serializable]
        private class Credentials
        {
            public string portalBaseUrl;
            public string experimentId;
            public string apiKey;
        }

        private Credentials _loaded;
        private bool _loadAttempted;

        /// <summary>
        /// Loaded on first use rather than in Awake: StudyPaths.Root only
        /// reflects SessionController's customDataFolder once StartSession
        /// has run, so an Awake-time lookup would miss a credentials file
        /// placed in the custom data folder.
        /// </summary>
        private Credentials Loaded
        {
            get
            {
                if (!_loadAttempted)
                {
                    _loadAttempted = true;
                    Load();
                }
                return _loaded;
            }
        }

        private void Load()
        {
            foreach (string dir in CandidateDirectories())
            {
                string path = Path.Combine(dir, CredentialsFileName);
                if (!File.Exists(path))
                {
                    continue;
                }
                try
                {
                    _loaded = JsonUtility.FromJson<Credentials>(File.ReadAllText(path));
                    Debug.Log($"[VeraConfig] Loaded credentials from {path} " +
                              $"(experiment '{_loaded?.experimentId ?? experimentId}').");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[VeraConfig] Failed to parse {path}: {e.Message}");
                }
            }
            Debug.Log("[VeraConfig] No vera_credentials.json found; VERA upload " +
                      "disabled for this session (local CSV logging unaffected).");
        }

        private static string[] CandidateDirectories()
        {
            return new[] { StudyPaths.Root, Application.persistentDataPath };
        }
    }
}
