using UnityEngine;
using NSFGrant.Logging;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// Mouse and keyboard capture for the laptop/WebGL participant group.
    /// Per the VERA meeting, desktop participants contribute richer pointer
    /// data than headset users: every click is logged with its 2D screen
    /// coordinates and, when it hits scene geometry, the 3D world position
    /// and object under the cursor. Typed keys are logged as key_press events.
    /// </summary>
    public class DesktopInteractor : MonoBehaviour
    {
        [Tooltip("Camera used for click raycasts. Defaults to the camera on this rig.")]
        [SerializeField] private Camera rigCamera;

        [SerializeField] private float maxRayDistance = 100f;
        [SerializeField] private LayerMask layerMask = ~0;

        private void Awake()
        {
            if (rigCamera == null)
            {
                rigCamera = GetComponentInChildren<Camera>();
            }
        }

        private void Update()
        {
            if (rigCamera == null)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                HandleClick();
            }

            // Input.inputString holds printable characters typed this frame.
            if (!string.IsNullOrEmpty(Input.inputString))
            {
                StudyEventLogger.Instance?.LogEvent("key_press", "", $"keys={Input.inputString}");
            }
        }

        private void HandleClick()
        {
            Vector2 screenPos = Input.mousePosition;
            Ray ray = rigCamera.ScreenPointToRay(screenPos);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, layerMask))
            {
                var interactable = hit.collider.GetComponentInParent<InteractableObject>();
                if (interactable != null)
                {
                    interactable.Activate("mouse", hit.point, screenPos);
                }
                else
                {
                    StudyEventLogger.Instance?.LogPointerEvent(
                        "click", hit.collider.gameObject.name, "non_interactable",
                        hit.point, screenPos);
                }
            }
            else
            {
                StudyEventLogger.Instance?.LogEvent(
                    "click", "", $"miss;screen={screenPos.x:F0}x{screenPos.y:F0}");
            }
        }
    }
}
