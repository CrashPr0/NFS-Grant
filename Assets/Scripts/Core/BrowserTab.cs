using System.Runtime.InteropServices;
using UnityEngine;

namespace NSFGrant.Core
{
    /// <summary>
    /// Opens a URL in a NEW browser tab so the study page (and its session)
    /// stays open. Unity's Application.OpenURL navigated the study page
    /// itself in the WebGL build, which ended the participant's session.
    /// Elsewhere (editor, desktop, Quest) it falls back to Application.OpenURL.
    ///
    /// In a browser the tab can only open from a user gesture; the jslib
    /// retries on the participant's next click if the popup blocker refuses
    /// (e.g. a headset participant finishing inside an immersive session).
    /// </summary>
    public static class BrowserTab
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void NSFGrantOpenUrlInNewTab(string url);
#endif

        /// <summary>True where Open really creates a separate browser tab.</summary>
        public static bool IsSupported => Application.platform == RuntimePlatform.WebGLPlayer;

        public static void Open(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            NSFGrantOpenUrlInNewTab(url);
#else
            Application.OpenURL(url);
#endif
        }
    }
}
