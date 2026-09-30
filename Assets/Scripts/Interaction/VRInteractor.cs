using UnityEngine;
using NSFGrant.Gaze;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// Trigger selection for the headset group. Pulling a hand's index
    /// trigger activates what that hand's laser is pointing at
    /// (source=vr_laser); if the beam is on nothing selectable, it falls
    /// back to the object under the gaze ray (eye gaze on Quest Pro, head
    /// gaze on Quest 3; source=vr_gaze), which was the only path before and
    /// made the visible beam look broken. Gaze keeps being sampled every
    /// frame for the attention data either way.
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

        private VRLaserPointer[] _lasers;

        private void Awake()
        {
            if (gazeProvider == null)
            {
                gazeProvider = FindAnyObjectByType<GazeProvider>();
            }
        }

        private void Start()
        {
            // The lasers live on the controller anchors inside this rig.
            _lasers = GetComponentsInChildren<VRLaserPointer>(true);
        }

        private void Update()
        {
            // A survey/confirm panel owns the trigger while it's open, so an
            // answer click can't also activate the exhibit behind it.
            if (VRSurveyPanel.IsOpen)
            {
                return;
            }

            bool rightDown = XRInputBridge.GetTriggerDown(XRInputBridge.Hand.Right);
            bool leftDown = XRInputBridge.GetTriggerDown(XRInputBridge.Hand.Left);
            if (!rightDown && !leftDown)
            {
                return;
            }

            var hand = rightDown ? XRInputBridge.Hand.Right : XRInputBridge.Hand.Left;
            var laser = LaserFor(hand);
            if (laser != null && laser.HoverTarget != null)
            {
                laser.HoverTarget.Activate("vr_laser", laser.HitPoint);
                Pulse(hand);
                return;
            }

            if (gazeProvider == null || gazeProvider.Source == GazeProvider.GazeSource.None)
            {
                return;
            }
            if (Physics.Raycast(gazeProvider.GazeRay, out RaycastHit hit, maxRayDistance, layerMask,
                    QueryTriggerInteraction.Ignore))
            {
                var interactable = hit.collider.GetComponentInParent<InteractableObject>();
                if (interactable != null)
                {
                    interactable.Activate("vr_gaze", hit.point);
                    Pulse(hand);
                }
            }
        }

        private VRLaserPointer LaserFor(XRInputBridge.Hand hand)
        {
            if (_lasers == null)
            {
                return null;
            }
            foreach (var laser in _lasers)
            {
                if (laser != null && laser.Hand == hand && laser.isActiveAndEnabled)
                {
                    return laser;
                }
            }
            return null;
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
