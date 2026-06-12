using UnityEngine;
using NSFGrant.Core;
using NSFGrant.Gaze;
using NSFGrant.Interaction;
using NSFGrant.Logging;
using NSFGrant.Stations;
using NSFGrant.Vera;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace NSFGrant.Session
{
    /// <summary>
    /// Orchestrates a data-collection session across both platforms:
    /// requests the eye-tracking permission (Quest Pro), starts/stops the
    /// continuous gaze logger and the discrete event logger, drives per-frame
    /// sampling, captures optional screenshots, writes the summary, notifies
    /// the VERA bridge, and (web builds) uploads the session files.
    /// </summary>
    public class SessionController : MonoBehaviour
    {
        private const string EyeTrackingPermission = "com.oculus.permission.EYE_TRACKING";

        [Tooltip("Identifier recorded in the data files. Set per participant before each run, or via a launch UI / VERA assignment.")]
        [SerializeField] private string participantId = "P000";

        [Tooltip("Begin logging as soon as the scene loads.")]
        [SerializeField] private bool autoStart = true;

        [SerializeField] private GazeProvider gazeProvider;
        [SerializeField] private GazeRaycaster gazeRaycaster;
        [SerializeField] private FixationDetector fixationDetector;
        [SerializeField] private AttentionDataLogger dataLogger;
        [SerializeField] private StudyEventLogger eventLogger;
        [SerializeField] private ScreenshotCapture screenshotCapture;
        [SerializeField] private RemoteDataUploader uploader;
        [SerializeField] private CounterbalanceManager counterbalance;

        public bool SessionRunning { get; private set; }
        public float SessionTime { get; private set; }

        public string ParticipantId
        {
            get => participantId;
            set => participantId = value;
        }

        private void Awake()
        {
            if (gazeProvider == null) gazeProvider = GetComponentInChildren<GazeProvider>();
            if (gazeRaycaster == null) gazeRaycaster = GetComponentInChildren<GazeRaycaster>();
            if (fixationDetector == null) fixationDetector = GetComponentInChildren<FixationDetector>();
            if (dataLogger == null) dataLogger = GetComponentInChildren<AttentionDataLogger>();
            if (eventLogger == null) eventLogger = GetComponentInChildren<StudyEventLogger>();
            if (screenshotCapture == null) screenshotCapture = GetComponentInChildren<ScreenshotCapture>();
            if (uploader == null) uploader = GetComponentInChildren<RemoteDataUploader>();
            if (counterbalance == null) counterbalance = GetComponentInChildren<CounterbalanceManager>();
        }

        private void Start()
        {
            RequestEyeTrackingPermission();

            if (autoStart)
            {
                StartSession();
            }
        }

        public void StartSession()
        {
            if (SessionRunning)
            {
                return;
            }

            SessionTime = 0f;
            foreach (var target in FindObjectsOfType<AttentionTarget>())
            {
                target.ResetStats();
            }
            foreach (var station in FindObjectsOfType<SdgStation>())
            {
                station.ResetStats();
            }
            foreach (var interactable in FindObjectsOfType<InteractableObject>())
            {
                interactable.ResetStats();
            }

            string platform = PlatformDetector.PlatformTag;
            string condition = CurrentConditionName();

            dataLogger.StartSession(participantId);
            eventLogger?.StartSession(participantId, platform, condition);
            eventLogger?.LogEvent("session_start", participantId,
                $"platform={platform};condition={condition};gaze={gazeProvider.Source}");
            // Counterbalance after the event logger is live so the
            // assignment lands in the log.
            counterbalance?.Apply(participantId);
            screenshotCapture?.StartCapture(participantId);
            VeraBridge.Instance?.NotifySessionStarted(participantId, platform, condition);

            SessionRunning = true;
            Debug.Log($"[SessionController] Session started: participant='{participantId}', " +
                      $"platform={platform}, condition={condition}");
        }

        public void StopSession()
        {
            if (!SessionRunning)
            {
                return;
            }

            SessionRunning = false;
            screenshotCapture?.StopCapture();
            eventLogger?.LogEvent("session_end", participantId, $"duration_s={SessionTime:F1}");
            eventLogger?.StopSession();
            dataLogger.StopSession();
            dataLogger.WriteSummary(
                participantId,
                PlatformDetector.PlatformTag,
                CurrentConditionName(),
                SessionTime,
                FindObjectsOfType<AttentionTarget>(),
                fixationDetector != null ? fixationDetector.FixationCount : 0,
                FindObjectsOfType<SdgStation>(),
                FindObjectsOfType<InteractableObject>());

            VeraBridge.Instance?.NotifySessionEnded(participantId, SessionTime);
            uploader?.UploadSessionFiles();

            Debug.Log($"[SessionController] Session stopped after {SessionTime:F1}s.");
        }

        private void Update()
        {
            if (!SessionRunning)
            {
                return;
            }

            SessionTime += Time.unscaledDeltaTime;
            gazeRaycaster.SessionTime = SessionTime;
            if (eventLogger != null)
            {
                eventLogger.SessionTime = SessionTime;
            }

            Transform head = gazeProvider.CenterEyeAnchor;
            Vector3 headPos = head != null ? head.position : Vector3.zero;
            Quaternion headRot = head != null ? head.rotation : Quaternion.identity;

            dataLogger.LogSample(
                SessionTime,
                headPos, headRot,
                gazeProvider.GazeOrigin, gazeProvider.GazeDirection,
                gazeProvider.Source.ToString(), gazeProvider.Confidence,
                fixationDetector != null ? fixationDetector.AngularVelocityDegPerSec : 0f,
                fixationDetector != null && fixationDetector.IsFixating,
                fixationDetector != null ? fixationDetector.CurrentFixationId : -1,
                gazeRaycaster.CurrentTarget != null ? gazeRaycaster.CurrentTarget.TargetId : "",
                gazeRaycaster.HitPoint, gazeRaycaster.HitDistance, gazeRaycaster.HasHit);
        }

        private static string CurrentConditionName()
        {
            return StudyConditionManager.Instance != null
                ? StudyConditionManager.Instance.Condition.ToString()
                : "Unspecified";
        }

        private void RequestEyeTrackingPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(EyeTrackingPermission))
            {
                Permission.RequestUserPermission(EyeTrackingPermission);
            }
#endif
        }

        private void OnApplicationQuit()
        {
            StopSession();
        }
    }
}
