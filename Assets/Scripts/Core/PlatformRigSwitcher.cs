using UnityEngine;
using UnityEngine.Rendering.Universal;
using NSFGrant.Interaction;

namespace NSFGrant.Core
{
    /// <summary>
    /// Enables exactly one camera rig: the OVRCameraRig when a headset is
    /// active, otherwise the desktop player (WASD + mouse) used for the
    /// laptop/WebGL sample. This is what lets the same build/URL serve both
    /// participant groups.
    ///
    /// WebXR note: in a browser the page loads FLAT and the participant
    /// clicks "Enter VR" afterwards, so the headset only becomes active some
    /// time after Awake. Startup-only switching would strand them in the
    /// desktop rig inside the headset. On the Unity XR path the choice is
    /// therefore re-evaluated while running, so entering (or exiting) an
    /// immersive session swaps rigs live. The native Quest build keeps the
    /// original startup-only behaviour, where XR is active from frame one
    /// and nothing should churn.
    ///
    /// Each live swap raises <see cref="XRSessionChanged"/>; SessionController
    /// turns it into xr_session_start / xr_session_end events and re-stamps
    /// the platform column of later rows.
    ///
    /// Headset cameras render without real-time shadows (a shadow map every
    /// frame is expensive on Quest); soft contact-shadow patches on the
    /// XROnlyVisual layer stand in for them (see NSFGrant/ContactShadow).
    /// </summary>
    public class PlatformRigSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject vrRig;
        [SerializeField] private GameObject desktopRig;

        private bool _lastXrState;

        /// <summary>
        /// Raised when an immersive session starts (true) or ends (false)
        /// after startup, i.e. only on the Unity XR / WebXR path.
        /// </summary>
        public static event System.Action<bool> XRSessionChanged;

        private void Awake()
        {
            _lastXrState = PlatformDetector.IsXRActive;
            Apply(_lastXrState);
            Debug.Log($"[PlatformRigSwitcher] Active rig: {(_lastXrState ? "VR" : "desktop")}");
        }

        private void Update()
        {
            // Only WebXR can flip mid-session; skip the poll on Quest.
            if (!XRInputBridge.UsesUnityXR)
            {
                return;
            }

            bool xr = PlatformDetector.IsXRActive;
            if (xr == _lastXrState)
            {
                return;
            }
            _lastXrState = xr;
            Apply(xr);
            Debug.Log($"[PlatformRigSwitcher] XR session {(xr ? "entered" : "exited")}; " +
                      $"switched to the {(xr ? "VR" : "desktop")} rig.");
            XRSessionChanged?.Invoke(xr);
        }

        private void Apply(bool xr)
        {
            if (vrRig != null)
            {
                vrRig.SetActive(xr);
                if (xr)
                {
                    // After SetActive: the rig creates its eye cameras in Awake.
                    foreach (var cam in vrRig.GetComponentsInChildren<Camera>(true))
                    {
                        cam.GetUniversalAdditionalCameraData().renderShadows = false;
                    }
                }
            }
            if (desktopRig != null)
            {
                desktopRig.SetActive(!xr);
            }
        }
    }
}
