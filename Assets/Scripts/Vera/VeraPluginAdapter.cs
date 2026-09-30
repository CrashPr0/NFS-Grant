using System;
using UnityEngine;
#if VERAFile_Gaze_Samples || VERAFile_StudyEvents
using VERA;
#endif

namespace NSFGrant.Vera
{
    /// <summary>
    /// Sends the study's data to the VERA portal alongside the local CSVs and
    /// the Drive uploads (both stay on as the backup): every AttentionDataLogger
    /// row to the portal's Gaze_Samples file type and every StudyEventLogger
    /// row to StudyEvents, in the column order registered on the portal
    /// (tools/vera/columns/*.json). VERA adds pID, conditions and ts itself.
    /// At the end it finalizes VERA's session so the participant is marked
    /// Completed.
    ///
    /// Each part compiles only once VERA's code generation has produced the
    /// matching define (VERA > Settings: sign in, select the experiment), so
    /// an unconnected checkout builds and runs exactly as before.
    ///
    /// Portal caveat: timestamp_utc_ms is a Float column there. A 32-bit
    /// float can't hold epoch milliseconds (~2 min resolution today), so that
    /// column is only approximate until its type changes to String; VERA's own
    /// ts column carries the precise session time.
    /// </summary>
    [RequireComponent(typeof(VeraBridge))]
    public class VeraPluginAdapter : MonoBehaviour
    {
        private const string GazeFile = "Gaze_Samples";
        private const string EventsFile = "StudyEvents";

        private VeraBridge _bridge;
        private string _platform = "";

        private void OnEnable()
        {
            _bridge = GetComponent<VeraBridge>();
            _bridge.SessionStarted += OnSessionStarted;
            _bridge.PlatformChanged += OnPlatformChanged;
            _bridge.EventLogged += OnEventLogged;
            _bridge.SessionEnded += OnSessionEnded;
#if VERAFile_Gaze_Samples
            // Only listen when there is somewhere to send it: the gaze logger
            // skips building samples when nobody is subscribed.
            _bridge.GazeSampled += OnGazeSampled;
#endif
        }

        private void OnDisable()
        {
            _bridge.SessionStarted -= OnSessionStarted;
            _bridge.PlatformChanged -= OnPlatformChanged;
            _bridge.EventLogged -= OnEventLogged;
            _bridge.SessionEnded -= OnSessionEnded;
#if VERAFile_Gaze_Samples
            _bridge.GazeSampled -= OnGazeSampled;
#endif
        }

        private void OnSessionStarted(string participantId, string platform, string condition)
        {
            _platform = platform;
            string vera = VeraSession.IsReady
                ? $"recording to '{VeraSession.ExperimentName}' as VERA pID {VeraSession.ParticipantId}"
                : VeraSession.BuildConnected
                    ? $"connected to '{VeraSession.ExperimentName}' but no VERA session (not launched from a VERA link?)"
                    : "not connected in this build";
            Debug.Log($"[VeraPluginAdapter] Session start (participant={participantId}, " +
                      $"platform={platform}, condition={condition}); VERA {vera}.");
        }

        private void OnPlatformChanged(string platform)
        {
            _platform = platform;
        }

        private void OnEventLogged(string eventType, string targetId, string detail,
            Vector3? worldPos, Vector2? screenPos)
        {
#if VERAFile_StudyEvents
            if (!VeraSession.IsCollecting)
            {
                return;
            }
            // NaN marks "no position" (the local CSV leaves those cells empty).
            Vector3 w = worldPos ?? new Vector3(float.NaN, float.NaN, float.NaN);
            Vector2 s = screenPos ?? new Vector2(float.NaN, float.NaN);
            VERASessionManager.CreateArbitraryCsvEntry(EventsFile,
                UtcMs(), _platform, eventType, targetId ?? "", detail ?? "",
                w.x, w.y, w.z, s.x, s.y);
#endif
        }

#if VERAFile_Gaze_Samples
        private void OnGazeSampled(VeraBridge.GazeSample g)
        {
            if (!VeraSession.IsCollecting)
            {
                return;
            }
            Vector3 hit = g.HasHit ? g.HitPoint : new Vector3(float.NaN, float.NaN, float.NaN);
            VERASessionManager.CreateArbitraryCsvEntry(GazeFile,
                UtcMs(), g.Frame,
                g.HeadPos.x, g.HeadPos.y, g.HeadPos.z,
                g.HeadRot.x, g.HeadRot.y, g.HeadRot.z, g.HeadRot.w,
                g.GazeOrigin.x, g.GazeOrigin.y, g.GazeOrigin.z,
                g.GazeDir.x, g.GazeDir.y, g.GazeDir.z,
                g.GazeSource ?? "", g.Confidence, g.AngularVelocity,
                g.IsFixating ? 1 : 0, g.FixationId, g.HitTarget ?? "",
                hit.x, hit.y, hit.z, g.HasHit ? g.HitDistance : float.NaN);
        }
#endif

        private void OnSessionEnded(string participantId, float duration)
        {
            bool finalize = VeraSession.IsCollecting;
            Debug.Log($"[VeraPluginAdapter] Session end (participant={participantId}, " +
                      $"duration={duration:F1}s){(finalize ? "; finalizing VERA session." : ".")}");
            VeraSession.FinalizeSession();
        }

        private static float UtcMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
