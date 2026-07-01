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

            bool rightDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
            bool leftDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
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
                    Pulse(rightDown ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch);
                }
            }
        }

        private void Pulse(OVRInput.Controller controller)
        {
            if (!hapticsEnabled)
            {
                return;
            }
            StopAllCoroutines();
            StartCoroutine(PulseRoutine(controller));
        }

        private System.Collections.IEnumerator PulseRoutine(OVRInput.Controller controller)
        {
            OVRInput.SetControllerVibration(1f, hapticAmplitude, controller);
            yield return new WaitForSeconds(hapticSeconds);
            OVRInput.SetControllerVibration(0f, 0f, controller);
        }
    }
}
