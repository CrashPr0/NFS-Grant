using UnityEngine;
using NSFGrant.Gaze;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// Gaze-and-commit selection for the headset group: pressing either
    /// index trigger activates the InteractableObject currently under the
    /// participant's gaze ray (eye gaze on Quest Pro, head gaze on Quest 3).
    /// Keeping selection on the gaze ray means the attention signal and the
    /// interaction signal share one coordinate frame in the data.
    /// </summary>
    public class VRInteractor : MonoBehaviour
    {
        [SerializeField] private GazeProvider gazeProvider;
        [SerializeField] private float maxRayDistance = 50f;
        [SerializeField] private LayerMask layerMask = ~0;

        [Tooltip("Short controller vibration pulse on a successful selection - VR has no click sound/cursor feedback otherwise.")]
        [SerializeField] private bool hapticsEnabled = true;
        [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.6f;
        [SerializeField] private float hapticSeconds = 0.08f;

        private void Awake()
        {
            if (gazeProvider == null)
            {
                gazeProvider = FindObjectOfType<GazeProvider>();
            }
        }

        private void Update()
        {
            if (gazeProvider == null || gazeProvider.Source == GazeProvider.GazeSource.None)
            {
                return;
            }

            bool rightDown = XRInputBridge.GetTriggerDown(XRInputBridge.Hand.Right);
            bool leftDown = XRInputBridge.GetTriggerDown(XRInputBridge.Hand.Left);
            if (!rightDown && !leftDown)
            {
                return;
            }

            if (Physics.Raycast(gazeProvider.GazeRay, out RaycastHit hit, maxRayDistance, layerMask))
            {
                var interactable = hit.collider.GetComponentInParent<InteractableObject>();
                if (interactable != null)
                {
                    interactable.Activate("vr_trigger", hit.point);
                    Pulse(rightDown ? XRInputBridge.Hand.Right : XRInputBridge.Hand.Left);
                }
            }
        }

        private void Pulse(XRInputBridge.Hand hand)
        {
            if (!hapticsEnabled)
            {
                return;
            }
            StopAllCoroutines();
            StartCoroutine(PulseRoutine(hand));
        }

        private System.Collections.IEnumerator PulseRoutine(XRInputBridge.Hand hand)
        {
            // On the Unity XR / WebXR path the impulse carries its own
            // duration and the stop call is a no-op; on the OVR path the
            // vibration runs until stopped, so the wait still matters.
            XRInputBridge.SendHaptic(hand, hapticAmplitude, hapticSeconds);
            yield return new WaitForSeconds(hapticSeconds);
            XRInputBridge.StopHaptic(hand);
        }
    }
}
