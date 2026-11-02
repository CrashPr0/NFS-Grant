using UnityEngine;
using NSFGrant.Core;
using NSFGrant.Logging;

namespace NSFGrant.Interaction
{
    /// <summary>
    /// Attaches an external resource URL to an interactable exhibit
    /// (video kiosk, report display, 360 tour, toolkit). Opening is logged
    /// as a content_link_open event; on desktop the link opens in the
    /// browser, on WebGL in a new tab (NSFGrantOpenUrl.jslib), on Quest in the system browser overlay.
    /// </summary>
    public class ContentLink : MonoBehaviour
    {
        [SerializeField] private string url;

        public string Url
        {
            get => url;
            set => url = value;
        }

        public void Open()
        {
            var interactable = GetComponent<InteractableObject>();
            StudyEventLogger.Instance?.LogEvent(
                "content_link_open",
                interactable != null ? interactable.ObjectId : gameObject.name,
                url);

            // New tab on WebGL, so the study page (and session) stays open.
            BrowserTab.Open(url);
        }
    }
}
