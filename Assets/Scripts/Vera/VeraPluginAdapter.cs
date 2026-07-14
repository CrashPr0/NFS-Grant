using UnityEngine;

namespace NSFGrant.Vera
{
    /// <summary>
    /// The one place the actual VERA Unity plugin gets wired in. Subscribes
    /// to <see cref="VeraBridge"/>'s lifecycle + event stream and forwards to
    /// the plugin when it is installed; until then it validates the pipeline
    /// by logging what *would* be sent.
    ///
    /// To activate once the plugin package is added to Packages/manifest.json:
    ///   1. Add VERA_PLUGIN_PRESENT to Project Settings > Player >
    ///      Scripting Define Symbols (all build targets).
    ///   2. Fill in the three #if blocks below with the plugin's real API
    ///      calls (recorder init, event record, session close).
    ///   3. Drop vera_credentials.json into the study data folder
    ///      (see VeraConfig for the format) — never commit it.
    ///
    /// VERA assigns participants and conditions from its portal when an
    /// experiment is distributed through it; when that path is live, map the
    /// assignment onto SessionController.ParticipantId and
    /// StudyConditionManager before StartSession() (see StudyIntake, which
    /// already does this for URL parameters on WebGL — the VERA assignment
    /// should flow through the same entry point).
    /// </summary>
    [RequireComponent(typeof(VeraBridge))]
    [RequireComponent(typeof(VeraConfig))]
    public class VeraPluginAdapter : MonoBehaviour
    {
        [Tooltip("Log every forwarded event to the console (editor debugging; " +
                 "leave off for device builds — the event rate is high).")]
        [SerializeField] private bool verboseLogging = false;

        private VeraBridge _bridge;
        private VeraConfig _config;

        private void OnEnable()
        {
            _bridge = GetComponent<VeraBridge>();
            _config = GetComponent<VeraConfig>();
            _bridge.SessionStarted += OnSessionStarted;
            _bridge.EventLogged += OnEventLogged;
            _bridge.SessionEnded += OnSessionEnded;
        }

        private void OnDisable()
        {
            _bridge.SessionStarted -= OnSessionStarted;
            _bridge.EventLogged -= OnEventLogged;
            _bridge.SessionEnded -= OnSessionEnded;
        }

        private void OnSessionStarted(string participantId, string platform, string condition)
        {
#if VERA_PLUGIN_PRESENT
            // TODO(vera): initialize the plugin recorder here, e.g.
            //   VeraRecorder.Begin(_config.ExperimentId, _config.ApiKey,
            //                      participantId, condition);
#else
            Debug.Log($"[VeraPluginAdapter] Session start (participant={participantId}, " +
                      $"platform={platform}, condition={condition}); VERA plugin not " +
                      $"installed, configured={_config.IsConfigured}.");
#endif
        }

        private void OnEventLogged(string eventType, string targetId, string detail)
        {
#if VERA_PLUGIN_PRESENT
            // TODO(vera): forward to the plugin's event recorder here.
#else
            if (verboseLogging)
            {
                Debug.Log($"[VeraPluginAdapter] Event {eventType} target={targetId} {detail}");
            }
#endif
        }

        private void OnSessionEnded(string participantId, float duration)
        {
#if VERA_PLUGIN_PRESENT
            // TODO(vera): flush + close the plugin recorder here.
#else
            Debug.Log($"[VeraPluginAdapter] Session end (participant={participantId}, " +
                      $"duration={duration:F1}s); VERA plugin not installed.");
#endif
        }
    }
}
