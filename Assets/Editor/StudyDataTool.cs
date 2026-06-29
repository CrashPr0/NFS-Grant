using System.IO;
using UnityEditor;
using UnityEngine;
using NSFGrant.Session;

namespace NSFGrant.EditorTools
{
    /// <summary>
    /// Convenience menu for locating the collected study data. By default the
    /// logs are written under <see cref="Application.persistentDataPath"/>/
    /// StudyData (an awkward per-app path, especially on Windows under
    /// AppData\LocalLow). If the open scene's SessionController has a
    /// <c>customDataFolder</c> set, that folder is used instead. These items
    /// open whichever is active.
    /// </summary>
    public static class StudyDataTool
    {
        // Mirrors StudyPaths.Root: the custom folder if the open scene's
        // SessionController sets one, else the default StudyData path.
        private static string StudyDataDir
        {
            get
            {
                var controller = Object.FindObjectOfType<SessionController>();
                if (controller != null)
                {
                    var prop = new SerializedObject(controller)
                        .FindProperty("customDataFolder");
                    if (prop != null && !string.IsNullOrEmpty(prop.stringValue))
                    {
                        return prop.stringValue;
                    }
                }
                return Path.Combine(Application.persistentDataPath, "StudyData");
            }
        }

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
