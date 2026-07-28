using UnityEngine;
using UnityEngine.XR;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// Drives a transform from a tracked XR node (head or a controller) using
    /// Unity's built-in XR input.
    ///
    /// WHY: on the native Quest build, `OVRCameraRig` moves its own anchors,
    /// so nothing else is needed. In a **WebXR** build Meta's SDK isn't
    /// present — the rig is an inert stub — so without this the head and both
    /// hands would sit frozen at the origin: the hands, laser and teleport arc
    /// would never move, and the participant couldn't look around. This is the
    /// component that makes VR-in-the-browser actually track.
    ///
    /// It is the same idea as the "Tracked Pose Driver" VERA's WebXR guide
    /// recommends, written against `UnityEngine.XR` directly so it needs no
    /// extra package (the legacy TrackedPoseDriver lives in
    /// com.unity.xr.legacyinputhelpers, which this project doesn't have).
    ///
    /// Self-disables on the OVR path so it can never fight `OVRCameraRig`,
    /// which means the same generated scene is safe to ship to both targets.
    /// </summary>
    public class XRPoseDriver : MonoBehaviour
    {
        public enum TrackedNode
        {
            Head,
            LeftHand,
            RightHand
        }

        [Tooltip("Which tracked node drives this transform.")]
        [SerializeField] private TrackedNode node = TrackedNode.Head;

        [Tooltip("Apply rotation as well as position. Turn off for the head " +
                 "if the headset view ends up double-rotated on some runtime.")]
        [SerializeField] private bool applyRotation = true;

        [Tooltip("Hide the object's renderers while its controller is absent " +
                 "(hands shouldn't float in space when a controller is off).")]
        [SerializeField] private bool hideWhenUntracked = false;

        private void Awake()
        {
            // On the native Meta path OVRCameraRig owns these anchors.
            if (!XRInputBridge.UsesUnityXR)
            {
                enabled = false;
            }
        }

        private void Update()
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(ToXRNode(node));
            bool tracked = device.isValid;

            if (tracked &&
                device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            {
                transform.localPosition = position;
            }
            else
            {
                tracked = false;
            }

            if (applyRotation &&
                device.isValid &&
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                transform.localRotation = rotation;
            }

            if (hideWhenUntracked)
            {
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = tracked;
                }
            }
        }

        private static XRNode ToXRNode(TrackedNode value)
        {
            switch (value)
            {
                case TrackedNode.LeftHand: return XRNode.LeftHand;
                case TrackedNode.RightHand: return XRNode.RightHand;
                default: return XRNode.CenterEye;
            }
        }
    }
}
