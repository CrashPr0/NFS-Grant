using System.IO;
using UnityEditor;
using UnityEngine;

namespace NSFGrant.EditorTools
{
    /// <summary>
    /// Convenience menu for locating the collected study data. The logs are
    /// written under <see cref="Application.persistentDataPath"/>/StudyData,
    /// which is an awkward per-app path to find by hand (especially on
    /// Windows under AppData\LocalLow). These items open it directly.
    /// In the editor this is the same folder Play-mode sessions write to.
    /// </summary>
    public static class StudyDataTool
    {
        private static string StudyDataDir =>
            Path.Combine(Application.persistentDataPath, "StudyData");

        [MenuItem("NSF Grant/Analysis/Open Study Data Folder")]
        public static void OpenStudyDataFolder()
        {
            string dir = StudyDataDir;
            if (!Directory.Exists(dir))
            {
                // Create it so the reveal lands somewhere, and tell the user
                // why it's empty.
                Directory.CreateDirectory(dir);
                Debug.Log($"[StudyDataTool] No data yet — created {dir}. " +
                          "Run a session (Play mode or a build) to populate it.");
            }
            EditorUtility.RevealInFinder(dir);
            Debug.Log($"[StudyDataTool] Study data folder: {dir}");
        }

        [MenuItem("NSF Grant/Analysis/Log Study Data Path")]
        public static void LogStudyDataPath()
        {
            // Prints the exact path to the Console (copy/paste friendly), and
            // lists what's there so far.
            string dir = StudyDataDir;
            Debug.Log($"[StudyDataTool] persistentDataPath = {Application.persistentDataPath}");
            Debug.Log($"[StudyDataTool] StudyData = {dir}");
            if (Directory.Exists(dir))
            {
                string[] files = Directory.GetFiles(dir, "*",
                    SearchOption.AllDirectories);
                Debug.Log($"[StudyDataTool] {files.Length} file(s) present:\n  " +
                          string.Join("\n  ", files));
            }
            else
            {
                Debug.Log("[StudyDataTool] Folder does not exist yet — no session has run.");
            }
        }
    }
}
