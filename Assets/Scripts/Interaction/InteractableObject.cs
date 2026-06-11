using UnityEngine;
using UnityEngine.Events;
using NSFGrant.Core;
using NSFGrant.Logging;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// A selectable object in the environment (interactive simulation,
    /// call-to-action choice, data-viz control, docent, etc.). Activated by
    /// mouse click on desktop/web or controller trigger in VR; every
    /// activation is written to the event log with world (and, on desktop,
    /// screen) coordinates — the per-object "interactivity rate" metric from
    /// the VERA meeting.
    ///
    /// In the Passive condition (A), activations are still logged as
    /// attempted clicks but the onActivated content response is suppressed.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class InteractableObject : MonoBehaviour
    {
        [Tooltip("Stable identifier used in the data files. Defaults to the GameObject name.")]
        [SerializeField] private string objectId;

        [Tooltip("Invoked on activation (unless the study condition is Passive).")]
        [SerializeField] private UnityEvent onActivated;

        /// <summary>Stable identifier written to the data files.</summary>
        public string ObjectId => string.IsNullOrEmpty(objectId) ? gameObject.name : objectId;

        /// <summary>Total activations this session (logged + content-suppressed).</summary>
        public int ActivationCount { get; private set; }

        /// <summary>
        /// Activate from VR (no screen coordinates).
        /// </summary>
        public void Activate(string source, Vector3 worldPoint)
        {
            Register(source, worldPoint, null);
        }

        /// <summary>
        /// Activate from a pointer (mouse) with 2D screen coordinates.
        /// </summary>
        public void Activate(string source, Vector3 worldPoint, Vector2 screenPoint)
        {
            Register(source, worldPoint, screenPoint);
        }

        public void ResetStats()
        {
            ActivationCount = 0;
        }

        private void Register(string source, Vector3 worldPoint, Vector2? screenPoint)
        {
            ActivationCount++;

            bool interactionEnabled = StudyConditionManager.Instance == null ||
                                      StudyConditionManager.Instance.InteractionEnabled;
            string detail = $"source={source};enabled={(interactionEnabled ? 1 : 0)}";

            var logger = StudyEventLogger.Instance;
            if (logger != null)
            {
                if (screenPoint.HasValue)
                {
                    logger.LogPointerEvent("object_activated", ObjectId, detail, worldPoint, screenPoint.Value);
                }
                else
                {
                    logger.LogWorldEvent("object_activated", ObjectId, detail, worldPoint);
                }
            }

            if (interactionEnabled)
            {
                onActivated?.Invoke();
            }
        }
    }
}
