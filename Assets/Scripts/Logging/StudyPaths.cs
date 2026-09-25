using System.IO;
using UnityEngine;

namespace NSFGrant.Logging
{
    /// <summary>
    /// Single source of truth for where session data is written. Every logger,
    /// the screenshot capture, and the uploader resolve their paths through
    /// here, so all of a session's output lands in one folder.
    ///
    /// By default that folder is <c>persistentDataPath/StudyData</c> (app-
    /// private on Quest; under AppData/Library on desktop). Set
    /// <see cref="OverrideRoot"/> — e.g. from the SessionController's
    /// <c>customDataFolder</c> field — to redirect everything to a folder you
    /// choose, including an OS-encrypted volume if the data must be kept
    /// private at rest.
    /// </summary>
    public static class StudyPaths
    {
        /// <summary>
        /// When non-empty, all study files are written under this folder
        /// instead of the default. Set once before a session starts.
        /// </summary>
        public static string OverrideRoot { get; set; }

        /// <summary>The active data root (override if set, else the default).</summary>
        public static string Root =>
            string.IsNullOrEmpty(OverrideRoot)
                ? Path.Combine(Application.persistentDataPath, "StudyData")
                : OverrideRoot;

        /// <summary>Subfolder for the low-rate screenshot stills.</summary>
        public static string ScreenshotsDir => Path.Combine(Root, "screenshots");

        /// <summary>
        /// Makes a value (e.g. a participant ID from the intake field or a
        /// ?pid= URL parameter) safe to embed in a file name: anything other
        /// than letters, digits, '-' and '_' becomes '_'. Without this an ID
        /// containing '/' or '..' would make the loggers throw or write
        /// outside the data folder.
        /// </summary>
        public static string FileToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "unknown";
            }
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }
    }
}
