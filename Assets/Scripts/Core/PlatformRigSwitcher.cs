using UnityEngine;
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
    /// </summary>
    public class PlatformRigSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject vrRig;
        [SerializeField] private GameObject desktopRig;

        private bool _lastXrState;

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
        }

        private void Apply(bool xr)
        {
            if (vrRig != null)
            {
                vrRig.SetActive(xr);
            }
            if (desktopRig != null)
            {
                desktopRig.SetActive(!xr);
            }
        }
    }
}
