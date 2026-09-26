using UnityEngine;
using WebXR;

namespace NSFGrant.WebXRAdapter
{
    /// <summary>
    /// Thin facade over the De-Panther WebXR Export package (assembly
    /// "WebXR", which is not auto-referenced, hence this small adapter
    /// assembly that Assembly-CSharp can see).
    ///
    /// Why it exists: WebXR Export publishes the HEADSET as a standard
    /// Unity XR input device (so XRPoseDriver keeps working for the head),
    /// but it does NOT publish controllers there - its own source notes
    /// the missing XRInputSubsystem. Controllers are only available via
    /// its WebXRController component. XRInputBridge routes to this class
    /// in WebGL/WebXR player builds.
    /// </summary>
    public static class WebXRInput
    {
        private static WebXRController _left;
        private static WebXRController _right;

        internal static void Register(WebXRController controller, bool left)
        {
            if (left) _left = controller; else _right = controller;
        }

        private static WebXRController Get(bool left)
        {
            var c = left ? _left : _right;
            return c != null && c.isActiveAndEnabled ? c : null;
        }

        /// <summary>True while an immersive VR session is running.</summary>
        public static bool IsVRActive =>
            WebXRManager.Instance != null && WebXRManager.Instance.XRState == WebXRState.VR;

        public static bool IsConnected(bool left)
        {
            var c = Get(left);
            return c != null && c.isControllerActive;
        }

        public static Vector2 GetThumbstick(bool left)
        {
            var c = Get(left);
            return c != null ? c.GetAxis2D(WebXRController.Axis2DTypes.Thumbstick) : Vector2.zero;
        }

        public static float GetTrigger(bool left)
        {
            var c = Get(left);
            return c != null ? c.GetAxis(WebXRController.AxisTypes.Trigger) : 0f;
        }

        public static bool GetTriggerDown(bool left)
        {
            var c = Get(left);
            return c != null && c.GetButtonDown(WebXRController.ButtonTypes.Trigger);
        }

        public static void Pulse(bool left, float amplitude, float seconds)
        {
            var c = Get(left);
            if (c != null)
            {
                c.Pulse(amplitude, seconds * 1000f);
            }
        }

        /// <summary>
        /// Editor/builder helper: adds a (disabled) WebXRController plus its
        /// gate to a controller anchor. The gate enables it only in a WebXR
        /// player, so on the native Quest build it can never fight
        /// OVRCameraRig over the anchor's pose.
        /// </summary>
        public static void AddControllerTo(GameObject anchor, bool left)
        {
            var controller = anchor.AddComponent<WebXRController>();
            controller.hand = left ? WebXRControllerHand.LEFT : WebXRControllerHand.RIGHT;
            controller.enabled = false;
            anchor.AddComponent<WebXRControllerGate>();
        }
    }

    /// <summary>
    /// Enables the sibling WebXRController only in WebGL player builds
    /// (where the browser's WebXR API is the tracking source) and
    /// registers it with <see cref="WebXRInput"/>. Everywhere else the
    /// controller stays disabled.
    /// </summary>
    public class WebXRControllerGate : MonoBehaviour
    {
        private void Awake()
        {
            var controller = GetComponent<WebXRController>();
            if (controller == null)
            {
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            controller.enabled = true;
            // Hand comes from the controller itself - a separate flag on this
            // gate didn't survive into the build, so both gates registered as
            // "right" and the left controller overwrote it (found via the
            // ?debug=1 telemetry under IWER).
            WebXRInput.Register(controller, controller.hand == WebXRControllerHand.LEFT);
#else
            controller.enabled = false;
#endif
        }
    }
}
