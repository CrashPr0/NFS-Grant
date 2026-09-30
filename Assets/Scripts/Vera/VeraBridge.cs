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

        /// <summary>(platform) - the platform of subsequent StudyEvents rows changed (WebXR enter/exit VR).</summary>
        public event Action<string> PlatformChanged;

        /// <summary>(eventType, targetId, detail, worldPos, screenPos) - one StudyEvents row.</summary>
        public event Action<string, string, string, Vector3?, Vector2?> EventLogged;

        /// <summary>One gaze sample per frame - one Gaze_Samples row.</summary>
        public event Action<GazeSample> GazeSampled;

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

        public void NotifyPlatformChanged(string platform)
        {
            PlatformChanged?.Invoke(platform);
        }

        public void NotifyEvent(string eventType, string targetId, string detail,
            Vector3? worldPos = null, Vector2? screenPos = null)
        {
            EventLogged?.Invoke(eventType, targetId, detail, worldPos, screenPos);
        }

        /// <summary>True when anyone listens, so the logger can skip building samples.</summary>
        public bool WantsGaze => GazeSampled != null;

        public void NotifyGazeSample(in GazeSample sample)
        {
            GazeSampled?.Invoke(sample);
        }

        public void NotifySessionEnded(string participantId, float duration)
        {
            SessionEnded?.Invoke(participantId, duration);
        }

        /// <summary>The values of one AttentionDataLogger row.</summary>
        public readonly struct GazeSample
        {
            public readonly int Frame;
            public readonly Vector3 HeadPos;
            public readonly Quaternion HeadRot;
            public readonly Vector3 GazeOrigin, GazeDir;
            public readonly string GazeSource;
            public readonly float Confidence, AngularVelocity;
            public readonly bool IsFixating;
            public readonly int FixationId;
            public readonly string HitTarget;
            public readonly Vector3 HitPoint;
            public readonly float HitDistance;
            public readonly bool HasHit;

            public GazeSample(int frame, Vector3 headPos, Quaternion headRot, Vector3 gazeOrigin,
                Vector3 gazeDir, string gazeSource, float confidence, float angularVelocity,
                bool isFixating, int fixationId, string hitTarget, Vector3 hitPoint,
                float hitDistance, bool hasHit)
            {
                Frame = frame; HeadPos = headPos; HeadRot = headRot;
                GazeOrigin = gazeOrigin; GazeDir = gazeDir; GazeSource = gazeSource;
                Confidence = confidence; AngularVelocity = angularVelocity;
                IsFixating = isFixating; FixationId = fixationId; HitTarget = hitTarget;
                HitPoint = hitPoint; HitDistance = hitDistance; HasHit = hasHit;
            }
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
