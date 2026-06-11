using UnityEngine;
using UnityEngine.XR;

namespace NSFGrant.Core
{
    /// <summary>
    /// Distinguishes the two deployment targets discussed with the VERA team:
    /// VR headset (Quest in the LTI Lab) vs. laptop/browser (WebGL build for
    /// the self-paced web sample). The same scene and URL serve both; data
    /// collection adapts per platform.
    /// </summary>
    public static class PlatformDetector
    {
        /// <summary>True when an XR device (headset) is active.</summary>
        public static bool IsXRActive => XRSettings.isDeviceActive;

        /// <summary>Short platform tag written into every data file.</summary>
        public static string PlatformTag => IsXRActive ? "headset" : "desktop";
    }
}
