using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace NSFGrant.EditorTools
{
    /// <summary>
    /// Downloads the study's media assets from the team's shared Google
    /// Drive folder ("Project 1 - UN SDGs" > Assets Library) into
    /// Assets/StudyContent/Textures:
    ///   - the 17 official UN SDG goal icons (E-WEB-Goal-XX.png),
    ///   - the SDG 13 photos (UN Photo / Mark Garten) and the 2025 Goal 13
    ///     progress card.
    ///
    /// Run NSF Grant &gt; Download SDG Media Assets once after cloning; the
    /// Discovery Hall builder picks the textures up automatically. Files
    /// must remain link-shared in Drive for the download to work.
    /// </summary>
    public static class SdgAssetDownloader
    {
        private const string OutputDir = "Assets/StudyContent/Textures";

        // Google Drive file IDs from the shared Assets Library folder.
        private static readonly Dictionary<string, string> Files = new Dictionary<string, string>
        {
            { "E-WEB-Goal-01.png", "1yLhk6vCq8bTlN-_OKLKRtLfuYGkGrBiP" },
            { "E-WEB-Goal-02.png", "1uEr8BOkxgyAsFlBexBPN2tkMkX5wWS8c" },
            { "E-WEB-Goal-03.png", "12SNYuJdteM_ZNHmBMfRooTd1b1afvqN8" },
            { "E-WEB-Goal-04.png", "1BtOiFtg8wOnlv4IZTz7EGoldh6e6kOyM" },
            { "E-WEB-Goal-05.png", "1kEeoEY5R8tnuc-ZYyXRAmpCpsdu7dBWf" },
            { "E-WEB-Goal-06.png", "1uBUf6p3JuSwRDF9KmAkHy-9ZUjoXPN7p" },
            { "E-WEB-Goal-07.png", "1_KUWB76DwSNVOvAuzPmnGF2wtfIjAthw" },
            { "E-WEB-Goal-08.png", "1PJOMhlAsptbtZ7ylOIraZHrq9wS6N5j_" },
            { "E-WEB-Goal-09.png", "1qIyhr50a06gXnWezJdXASRNUXHnj7NeS" },
            { "E-WEB-Goal-10.png", "15NbBa-gwnW-bfK1DR_3Vohd0_Ejs_2c_" },
            { "E-WEB-Goal-11.png", "1Aj7QCcN2ZTVElIkmw5IOjYUmaUA7SuYU" },
            { "E-WEB-Goal-12.png", "1Y5ht_4fh3nPoTF59YHJqe7hbBIOm4mSg" },
            { "E-WEB-Goal-13.png", "1lmX5hxGdBOWKZNJUXRMOW_K8hxeBdsEw" },
            { "E-WEB-Goal-14.png", "1isuRGPJRYRq_AzJS_lu2-hrMy4KCRSlz" },
            { "E-WEB-Goal-15.png", "1EtQ9hbubZy0EQX9KigLpFO8tsVYhgtKP" },
            { "E-WEB-Goal-16.png", "1_lQdoNWd9f58eLnfJ9aLvLTkfE7HBu81" },
            { "E-WEB-Goal-17.png", "1QqkqoAWMfuBSH0z89yvRFX8By8FuyqtK" },
            // SDG 13 station media (UN Photo / Mark Garten; see Drive file
            // descriptions for attribution).
            { "SDG13_HurricaneDorian_UN730286.jpg", "1JqXVkMLzTDzHRlkyjruHUbpr-VMbosjP" },
            { "SDG13_Forest_UN7860718.jpg", "1ZdlulAQVpb9VyAjuHHgVoWx19wKNxd1f" },
            { "SDG13_ProgressCard_2025.png", "1ykvjdsLaTq7sE_l3Yh3el6JVSHPWWgMu" }
        };

        [MenuItem("NSF Grant/Download SDG Media Assets")]
        public static void DownloadAll()
        {
            Directory.CreateDirectory(OutputDir);
            int done = 0, failed = 0, index = 0;

            try
            {
                foreach (var entry in Files)
                {
                    index++;
                    EditorUtility.DisplayProgressBar("Downloading SDG media assets",
                        entry.Key, (float)index / Files.Count);

                    string path = Path.Combine(OutputDir, entry.Key);
                    if (File.Exists(path))
                    {
                        done++;
                        continue;
                    }

                    if (Download(entry.Value, path))
                    {
                        done++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[SdgAssetDownloader] {done}/{Files.Count} assets ready in {OutputDir}" +
                      (failed > 0 ? $" ({failed} failed - check Drive link sharing and network)" : ""));
        }

        private static bool Download(string fileId, string outputPath)
        {
            string url = $"https://drive.google.com/uc?export=download&id={fileId}";
            using var request = UnityWebRequest.Get(url);
            request.SendWebRequest();
            while (!request.isDone)
            {
                System.Threading.Thread.Sleep(50);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SdgAssetDownloader] {outputPath}: {request.error}");
                return false;
            }

            byte[] data = request.downloadHandler.data;
            // An HTML response means Drive served an interstitial page
            // instead of the file (not link-shared, or quota exceeded).
            if (data == null || data.Length == 0 || data[0] == (byte)'<')
            {
                Debug.LogWarning($"[SdgAssetDownloader] {outputPath}: Drive returned a page instead of the file. " +
                                 "Make sure the file is shared as 'Anyone with the link'.");
                return false;
            }

            File.WriteAllBytes(outputPath, data);
            return true;
        }
    }
}
