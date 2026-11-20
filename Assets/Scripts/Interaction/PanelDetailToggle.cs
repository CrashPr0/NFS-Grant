using UnityEngine;
using NSFGrant.Logging;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// A panel with no link of its own prints one short, large summary; clicking
    /// it swaps in the fuller text (and clicking again swaps back), so the panel
    /// stays legible from a distance without losing the information. Works only
    /// when interaction is enabled (InteractableObject.Activated), so in the
    /// Passive condition the panel stays on its summary like any static sign.
    /// </summary>
    [RequireComponent(typeof(InteractableObject))]
    public class PanelDetailToggle : MonoBehaviour
    {
        [SerializeField] private GameObject summary;
        [SerializeField] private GameObject detail;

        private InteractableObject _interactable;
        private bool _showingDetail;

        private void Awake()
        {
            _interactable = GetComponent<InteractableObject>();
            Apply();
        }

        private void OnEnable()
        {
            _interactable.Activated += OnActivated;
        }

        private void OnDisable()
        {
            _interactable.Activated -= OnActivated;
        }

        private void OnActivated(InteractableObject source)
        {
            _showingDetail = !_showingDetail;
            Apply();
            StudyEventLogger.Instance?.LogEvent("panel_detail", source.ObjectId,
                _showingDetail ? "open" : "close");
        }

        private void Apply()
        {
            if (summary != null) summary.SetActive(!_showingDetail);
            if (detail != null) detail.SetActive(_showingDetail);
        }
    }
}
