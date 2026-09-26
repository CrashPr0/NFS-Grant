using System;
using UnityEngine;
using NSFGrant.Interaction;

namespace NSFGrant.Core
{
    /// <summary>
    /// Opt-in XR telemetry for testing builds without guessing from
    /// screenshots: once a second it logs the active rig, rig/head world
    /// position, XR state, and controller connection + stick/trigger values
    /// to the console (the browser console in WebGL).
    ///
    /// Off by default. Enable with <c>?debug=1</c> on the WebGL/WebXR page
    /// URL (e.g. in the Quest browser via remote DevTools) or by ticking
    /// <see cref="forceEnabled"/>. Reads state only; changes nothing.
    /// </summary>
    public class XRDiagnostics : MonoBehaviour
    {
        [SerializeField] private bool forceEnabled;
        [SerializeField] private Transform vrRig;
        [SerializeField] private float intervalSeconds = 1f;

        private bool _enabled;
        private float _next;

        public void Configure(Transform rig) => vrRig = rig;

        private void Awake()
        {
            _enabled = forceEnabled || UrlHasDebugFlag();
            if (_enabled)
            {
                Debug.Log("[XRDiag] enabled");
            }
        }

        private void Update()
        {
            if (!_enabled || Time.unscaledTime < _next)
            {
                return;
            }
            _next = Time.unscaledTime + intervalSeconds;

            var cam = Camera.main;
            Vector3 head = cam != null ? cam.transform.position : Vector3.zero;
            Vector3 rig = vrRig != null ? vrRig.position : Vector3.zero;
            Vector2 ls = XRInputBridge.GetThumbstick(XRInputBridge.Hand.Left);
            Vector2 rs = XRInputBridge.GetThumbstick(XRInputBridge.Hand.Right);
            Debug.Log(
                $"[XRDiag] xr={PlatformDetector.IsXRActive} cam={(cam != null ? cam.name : "none")} " +
                $"head=({head.x:F2},{head.y:F2},{head.z:F2}) rig=({rig.x:F2},{rig.y:F2},{rig.z:F2}) " +
                $"L={XRInputBridge.IsConnected(XRInputBridge.Hand.Left)} ls=({ls.x:F2},{ls.y:F2}) " +
                $"R={XRInputBridge.IsConnected(XRInputBridge.Hand.Right)} rs=({rs.x:F2},{rs.y:F2}) " +
                $"rt={XRInputBridge.GetTrigger(XRInputBridge.Hand.Right):F2} " +
                $"survey[{VRSurveyPanel.DebugState}]");
        }

        private static bool UrlHasDebugFlag()
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }
            int q = url.IndexOf('?');
            if (q < 0)
            {
                return false;
            }
            foreach (string pair in url.Substring(q + 1).Split('&'))
            {
                if (pair.Equals("debug=1", StringComparison.OrdinalIgnoreCase) ||
                    pair.Equals("debug=true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
