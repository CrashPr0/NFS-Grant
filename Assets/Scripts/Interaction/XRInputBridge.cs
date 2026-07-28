using UnityEngine;
using UnityEngine.XR;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// One facade for controller input, so the study's VR scripts work on
    /// BOTH deployment paths:
    ///
    ///   - **Native Quest build (APK).** Routes to Meta's `OVRInput`, exactly
    ///     as before. Behaviour on the headset is unchanged.
    ///   - **WebXR build (VERA's hosted browser experience).** Meta's SDK is
    ///     not part of a WebGL/WebXR player, and `OVRInput` never receives
    ///     input in a browser. Routes instead to Unity's built-in XR input
    ///     (`UnityEngine.XR.InputDevices`), which the WebXR export plugin
    ///     feeds from the browser's WebXR API.
    ///
    /// `UnityEngine.XR` is part of the core engine (`com.unity.modules.xr`,
    /// already in the manifest), so this adds NO package dependency — the
    /// deliberate reason it's used here instead of XRI or Input System
    /// actions, both of which would need asset/package setup.
    ///
    /// Which path compiles is decided by the same switch as the OVR shim
    /// (see Assets/Scripts/Compat/OVRWebGLStub.cs), so the two can never
    /// disagree: Unity XR in a WebGL/WebXR player or a Meta-free build,
    /// OVRInput everywhere else.
    ///
    /// Everything degrades silently: with no XR device connected (a plain
    /// desktop browser session) every query returns "not connected / zero",
    /// which is exactly what the desktop mouse-and-keyboard rig wants.
    /// </summary>
    public static class XRInputBridge
    {
        // NOTE: deliberately `static readonly`, not `const`. Both branches of
        // every method below must still COMPILE on both paths (the OVR calls
        // resolve against the shim on WebGL; the Unity XR calls are core
        // engine API everywhere) - a `const` would mark the unused branch as
        // unreachable and fill the console with CS0162 warnings.
#if (UNITY_WEBGL && !UNITY_EDITOR) || NSFGRANT_NO_META
        /// <summary>True when input comes from Unity XR rather than OVRInput.</summary>
        public static readonly bool UsesUnityXR = true;
#else
        public static readonly bool UsesUnityXR = false;
#endif

        public enum Hand
        {
            Left,
            Right
        }

        /// <summary>Is this hand's controller present and tracking?</summary>
        public static bool IsConnected(Hand hand)
        {
            if (UsesUnityXR)
            {
                return GetDevice(hand).isValid;
            }
            return OVRInput.IsControllerConnected(ToOvr(hand));
        }

        /// <summary>Thumbstick, x = right, y = forward, each in [-1, 1].</summary>
        public static Vector2 GetThumbstick(Hand hand)
        {
            if (UsesUnityXR)
            {
                InputDevice device = GetDevice(hand);
                if (device.isValid &&
                    device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
                {
                    return axis;
                }
                return Vector2.zero;
            }
            return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, ToOvr(hand));
        }

        /// <summary>Analog index-trigger squeeze in [0, 1].</summary>
        public static float GetTrigger(Hand hand)
        {
            if (UsesUnityXR)
            {
                InputDevice device = GetDevice(hand);
                if (device.isValid &&
                    device.TryGetFeatureValue(CommonUsages.trigger, out float value))
                {
                    return value;
                }
                return 0f;
            }
            return OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, ToOvr(hand));
        }

        /// <summary>
        /// True only on the frame the index trigger crosses into "pressed".
        /// Unity XR reports level, not edges, so the edge is derived here
        /// from a per-frame cached previous state (OVRInput has GetDown
        /// natively and is used directly on the Quest path).
        /// </summary>
        public static bool GetTriggerDown(Hand hand)
        {
            if (!UsesUnityXR)
            {
                return OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, ToOvr(hand));
            }

            int index = (int)hand;
            RefreshEdges();
            return _pressed[index] && !_pressedLastFrame[index];
        }

        /// <summary>Short haptic pulse; silently ignored where unsupported (WebXR often is).</summary>
        public static void SendHaptic(Hand hand, float amplitude, float duration)
        {
            if (!UsesUnityXR)
            {
                // Frequency 1 matches the previous OVRInput call; OVR runs
                // the pulse until explicitly stopped by the caller.
                OVRInput.SetControllerVibration(1f, amplitude, ToOvr(hand));
                return;
            }

            InputDevice device = GetDevice(hand);
            if (device.isValid &&
                device.TryGetHapticCapabilities(out HapticCapabilities caps) &&
                caps.supportsImpulse)
            {
                device.SendHapticImpulse(0u, amplitude, duration);
            }
        }

        /// <summary>Stops a running OVR vibration. No-op on the Unity XR path,
        /// where impulses are fire-and-forget with a fixed duration.</summary>
        public static void StopHaptic(Hand hand)
        {
            if (!UsesUnityXR)
            {
                OVRInput.SetControllerVibration(0f, 0f, ToOvr(hand));
            }
        }

        // --- Unity XR plumbing -------------------------------------------------

        private static readonly bool[] _pressed = new bool[2];
        private static readonly bool[] _pressedLastFrame = new bool[2];
        private static int _edgeFrame = -1;

        /// <summary>
        /// Samples both triggers once per frame. Guarded on frameCount so
        /// several callers in the same frame see one consistent edge rather
        /// than the first caller consuming it.
        /// </summary>
        private static void RefreshEdges()
        {
            if (_edgeFrame == Time.frameCount)
            {
                return;
            }
            _edgeFrame = Time.frameCount;

            for (int i = 0; i < 2; i++)
            {
                _pressedLastFrame[i] = _pressed[i];
                InputDevice device = GetDevice((Hand)i);
                bool down = false;
                if (device.isValid &&
                    !device.TryGetFeatureValue(CommonUsages.triggerButton, out down))
                {
                    // Some runtimes expose only the analog axis.
                    down = device.TryGetFeatureValue(CommonUsages.trigger, out float v)
                           && v > 0.6f;
                }
                _pressed[i] = down;
            }
        }

        private static InputDevice GetDevice(Hand hand)
        {
            return InputDevices.GetDeviceAtXRNode(
                hand == Hand.Left ? XRNode.LeftHand : XRNode.RightHand);
        }

        private static OVRInput.Controller ToOvr(Hand hand)
        {
            return hand == Hand.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        }
    }
}
