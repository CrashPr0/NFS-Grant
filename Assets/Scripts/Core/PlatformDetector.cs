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
        public static bool IsXRActive =>
#if UNITY_WEBGL && !UNITY_EDITOR
            // In the browser the WebXR session state is authoritative: the
            // page loads flat and only becomes VR after "Enter VR".
            NSFGrant.WebXRAdapter.WebXRInput.IsVRActive || XRSettings.isDeviceActive;
#else
            XRSettings.isDeviceActive;
#endif

        /// <summary>Short platform tag written into every data file.</summary>
        public static string PlatformTag => TagFor(IsXRActive);

        public static string TagFor(bool xr) => xr ? "headset" : "desktop";

        /// <summary>Unity's loaded XR device (e.g. "WebXR Display").</summary>
        public static string XRDeviceName => XRSettings.loadedDeviceName;

        /// <summary>
        /// WebXR: the controllers' input profile (e.g. "meta-quest-touch-plus"),
        /// the closest thing a browser reports to the headset model; null until
        /// the controllers connect, and always null outside WebGL.
        /// </summary>
        public static string XRControllerProfile =>
#if UNITY_WEBGL && !UNITY_EDITOR
            NSFGrant.WebXRAdapter.WebXRInput.GetProfile(false) ??
            NSFGrant.WebXRAdapter.WebXRInput.GetProfile(true);
#else
            null;
#endif
    }
}
