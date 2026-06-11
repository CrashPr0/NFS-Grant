using UnityEngine;

namespace NSFGrant.Core
{
    /// <summary>
    /// Enables exactly one camera rig at startup: the OVRCameraRig when a
    /// headset is active, otherwise the desktop player (WASD + mouse) used
    /// for the laptop/WebGL sample. This is what lets the same build/URL
    /// serve both participant groups.
    /// </summary>
    public class PlatformRigSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject vrRig;
        [SerializeField] private GameObject desktopRig;

        private void Awake()
        {
            bool xr = PlatformDetector.IsXRActive;
            if (vrRig != null)
            {
                vrRig.SetActive(xr);
            }
            if (desktopRig != null)
            {
                desktopRig.SetActive(!xr);
            }
            Debug.Log($"[PlatformRigSwitcher] Active rig: {(xr ? "VR" : "desktop")}");
        }
    }
}
