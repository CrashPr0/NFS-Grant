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

            bool triggerDown =
                OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch) ||
                OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);

            if (!triggerDown)
            {
                return;
            }

            if (Physics.Raycast(gazeProvider.GazeRay, out RaycastHit hit, maxRayDistance, layerMask))
            {
                var interactable = hit.collider.GetComponentInParent<InteractableObject>();
                if (interactable != null)
                {
                    interactable.Activate("vr_trigger", hit.point);
                }
            }
        }
    }
}
