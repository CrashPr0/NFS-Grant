using System;
using UnityEngine;
#if VERAFile_Experiment_Telemetry || VERAIV_Condition
using VERA;
#endif

namespace NSFGrant.Vera
{
    /// <summary>
    /// Read-only view of the VERA plugin's session for the rest of the study
    /// code (StudyIntake, VeraPluginAdapter), so only the Vera folder touches
    /// the plugin API.
    ///
    /// Compiles against the plugin only where VERA's code generation has run:
    /// selecting an experiment in VERA > Settings writes per-item scripting
    /// defines (VERAFile_&lt;FileType&gt;, VERAIV_&lt;Variable&gt;).
    /// VERAFile_Experiment_Telemetry is auto-created for every experiment, so
    /// it doubles as "the plugin and an experiment are present".
    /// </summary>
    public static class VeraSession
    {
        [Serializable]
        private class BuildAuthentication
        {
            public bool authenticated;
            public string activeExperimentName;
        }

        private static bool? _buildConnected;
        private static string _experimentName;

        /// <summary>
        /// True when this build was made from an editor signed in to VERA with
        /// an active experiment (VERA writes that state into the build as the
        /// VERABuildAuthentication resource). False on unconnected builds,
        /// e.g. a student's checkout that never signed in.
        /// </summary>
        public static bool BuildConnected
        {
            get
            {
                if (_buildConnected == null)
                {
                    var json = Resources.Load<TextAsset>("VERABuildAuthentication");
                    var auth = json != null ? JsonUtility.FromJson<BuildAuthentication>(json.text) : null;
                    _buildConnected = auth != null && auth.authenticated;
                    _experimentName = auth?.activeExperimentName;
                }
                return _buildConnected.Value;
            }
        }

        /// <summary>The build's active VERA experiment, when connected.</summary>
        public static string ExperimentName => BuildConnected ? _experimentName : null;

        /// <summary>VERA has a participant and is ready (portal IDs received).</summary>
        public static bool IsReady
        {
            get
            {
#if VERAFile_Experiment_Telemetry
                return VERASessionManager.initialized;
#else
                return false;
#endif
            }
        }

        /// <summary>VERA is recording this participant's session.</summary>
        public static bool IsCollecting
        {
            get
            {
#if VERAFile_Experiment_Telemetry
                return VERASessionManager.initialized && VERASessionManager.collecting;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// A browser session launched from a VERA participant link. VERA's
        /// WebGL runtime only initializes when the portal hands it site and
        /// participant IDs, so "ready in a WebGL player" means VERA-hosted.
        /// </summary>
        public static bool IsVeraHostedWeb =>
            Application.platform == RuntimePlatform.WebGLPlayer && IsReady;

        /// <summary>VERA's participant ID (portal-assigned), or null.</summary>
        public static string ParticipantId
        {
            get
            {
#if VERAFile_Experiment_Telemetry
                return IsReady ? VERASessionManager.participantID : null;
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// The portal's Condition level for this participant as named on the
        /// portal (e.g. "Passive"), or null. Read as the raw string rather than
        /// the generated VERAIV_Condition enum, whose members are prefixed
        /// ("V_Passive").
        /// </summary>
        public static string Condition
        {
            get
            {
#if VERAIV_Condition
                return IsReady ? VERASessionManager.GetSelectedIVValue("Condition") : null;
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// Ends VERA's session: stops collection, flushes to the portal and
        /// marks the participant Completed (skipping it leaves them
        /// Incomplete). In a browser it also closes the WebXR session, so call
        /// it last. No-op when VERA isn't recording.
        /// </summary>
        public static void FinalizeSession()
        {
#if VERAFile_Experiment_Telemetry
            if (IsCollecting)
            {
                VERASessionManager.FinalizeSession();
            }
#endif
        }
    }
}
