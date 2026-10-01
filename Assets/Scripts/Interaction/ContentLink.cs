using System.Runtime.InteropServices;
using UnityEngine;
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

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void NSFGrantOpenUrlInNewTab(string url);
#endif

        public void Open()
        {
            var interactable = GetComponent<InteractableObject>();
            StudyEventLogger.Instance?.LogEvent(
                "content_link_open",
                interactable != null ? interactable.ObjectId : gameObject.name,
                url);

            if (!string.IsNullOrEmpty(url))
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // New tab, so the study page (and session) stays open.
                NSFGrantOpenUrlInNewTab(url);
#else
                Application.OpenURL(url);
#endif
            }
        }
    }
}
