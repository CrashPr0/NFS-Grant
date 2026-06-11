using System;
using UnityEngine;

namespace NSFGrant.Vera
{
    /// <summary>
    /// Integration seam for the VERA (Virtual Experience Research Accelerator)
    /// Unity plugin. Corey (UCF/VERA) confirmed feasibility of a Unity plugin
    /// in the kickoff meeting; until it is delivered, this bridge re-publishes
    /// the study's lifecycle and event stream as C# events so the plugin can
    /// be wired in one place without touching the logging pipeline.
    ///
    /// Expected wiring once the plugin arrives:
    ///   1. Add the VERA package to Packages/manifest.json.
    ///   2. Subscribe the plugin's recorder to SessionStarted / EventLogged /
    ///      SessionEnded below (or replace the bodies of the Notify methods).
    ///   3. Map VERA participant/condition assignment onto
    ///      SessionController.ParticipantId and StudyConditionManager.Condition.
    /// </summary>
    public class VeraBridge : MonoBehaviour
    {
        public static VeraBridge Instance { get; private set; }

        /// <summary>(participantId, platform, condition)</summary>
        public event Action<string, string, string> SessionStarted;

        /// <summary>(eventType, targetId, detail)</summary>
        public event Action<string, string, string> EventLogged;

        /// <summary>(participantId, sessionDurationSeconds)</summary>
        public event Action<string, float> SessionEnded;

        private void Awake()
        {
            Instance = this;
        }

        public void NotifySessionStarted(string participantId, string platform, string condition)
        {
            SessionStarted?.Invoke(participantId, platform, condition);
        }

        public void NotifyEvent(string eventType, string targetId, string detail)
        {
            EventLogged?.Invoke(eventType, targetId, detail);
        }

        public void NotifySessionEnded(string participantId, float duration)
        {
            SessionEnded?.Invoke(participantId, duration);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
