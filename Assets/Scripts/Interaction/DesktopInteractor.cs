using UnityEngine;
using NSFGrant.Logging;
using NSFGrant.UI;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// Mouse and keyboard capture for the laptop/WebGL participant group.
    /// Per the VERA meeting, desktop participants contribute richer pointer
    /// data than headset users: every click is logged with its 2D screen
    /// coordinates and, when it hits scene geometry, the 3D world position
    /// and object under the cursor. Typed keys are logged as key_press events.
    /// The object under the cursor is also reported as hovered, so it can
    /// show that it is clickable (<see cref="InteractionHighlight"/>).
    /// While the mouse is captured (DesktopPlayerController) the pointer is
    /// the screen-center aim dot, and that is the logged screen position.
    ///
    /// Rays ignore trigger colliders: each room's SdgStation sensor is a
    /// trigger box around the whole room, and with "queries hit triggers"
    /// on (the project default) a click from the hub or corridor stopped on
    /// its invisible face instead of reaching the exhibit.
    /// </summary>
    public class DesktopInteractor : MonoBehaviour
    {
        [Tooltip("Camera used for click raycasts. Defaults to the camera on this rig.")]
        [SerializeField] private Camera rigCamera;

        [SerializeField] private float maxRayDistance = 100f;
        [SerializeField] private LayerMask layerMask = ~0;

        private InteractableObject _hovered;

        private void Awake()
        {
            if (rigCamera == null)
            {
                rigCamera = GetComponentInChildren<Camera>();
            }
        }

        private void Update()
        {
            // Clicks/keys aimed at an on-screen panel belong to that panel.
            if (rigCamera == null || StudyGuiKit.ModalVisible)
            {
                SetHovered(null);
                return;
            }

            UpdateHover();

            if (Input.GetMouseButtonDown(0) &&
                (DesktopPlayerController.AimFromCenter || !StudyGuiKit.PointerOverHud))
            {
                HandleClick();
            }

            // Input.inputString holds printable characters typed this frame.
            if (!string.IsNullOrEmpty(Input.inputString))
            {
                StudyEventLogger.Instance?.LogEvent("key_press", "", $"keys={Input.inputString}");
            }
        }

        private static Vector2 PointerPosition => DesktopPlayerController.AimFromCenter
            ? new Vector2(Screen.width / 2f, Screen.height / 2f)
            : (Vector2)Input.mousePosition;

        private void OnDisable()
        {
            SetHovered(null);
        }

        private void UpdateHover()
        {
            InteractableObject target = null;
            // Not while right-drag looking: the cursor isn't pointing then.
            if (DesktopPlayerController.AimFromCenter ||
                (!StudyGuiKit.PointerOverHud && !Input.GetMouseButton(1)))
            {
                Ray ray = rigCamera.ScreenPointToRay(PointerPosition);
                if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, layerMask,
                        QueryTriggerInteraction.Ignore))
                {
                    target = hit.collider.GetComponentInParent<InteractableObject>();
                }
            }
            SetHovered(target);
        }

        private void SetHovered(InteractableObject target)
        {
            if (target == _hovered)
            {
                return;
            }
            if (_hovered != null)
            {
                _hovered.SetHovered(this, false);
            }
            _hovered = target;
            if (_hovered != null)
            {
                _hovered.SetHovered(this, true);
            }
        }

        private void HandleClick()
        {
            Vector2 screenPos = PointerPosition;
            Ray ray = rigCamera.ScreenPointToRay(screenPos);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, layerMask,
                    QueryTriggerInteraction.Ignore))
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
