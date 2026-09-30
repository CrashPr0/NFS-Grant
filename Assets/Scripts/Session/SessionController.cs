using System;
using System.Globalization;
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

        [Tooltip("Optional folder where ALL session files are written (CSVs + " +
                 "screenshots). Empty = the default persistentDataPath/StudyData. " +
                 "Point it at any folder, including an OS-encrypted volume.")]
        [SerializeField] private string customDataFolder = "";

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

        private DateTime _sessionStartUtc;

        // A browser participant can enter/leave VR mid-session (WebXR).
        private bool _usedHeadset;
        private bool _inXrSegment;
        private float _xrStartTime;
        private string _xrControllerProfile;

        public string ParticipantId
        {
            get => participantId;
            set => participantId = value;
        }

        /// <summary>
        /// Pins number formatting to the invariant culture before any scene
        /// code runs. Event details are built with interpolation such as
        /// $"duration_s={t:F1}"; on a comma-decimal OS locale that yields
        /// "3,5", which the CSV sanitizer turns into "3;5" - corrupting the
        /// value in the research data.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void UseInvariantCulture()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
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

        private void OnEnable()
        {
            PlatformRigSwitcher.XRSessionChanged += OnXRSessionChanged;
        }

        private void OnDisable()
        {
            PlatformRigSwitcher.XRSessionChanged -= OnXRSessionChanged;
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

            // Redirect every logger/screenshot/upload path to the chosen
            // folder before any of them open files. Empty = default.
            StudyPaths.OverrideRoot = customDataFolder;

            SessionTime = 0f;
            _sessionStartUtc = DateTime.UtcNow;
            foreach (var target in FindObjectsByType<AttentionTarget>(FindObjectsSortMode.None))
            {
                target.ResetStats();
            }
            foreach (var station in FindObjectsByType<SdgStation>(FindObjectsSortMode.None))
            {
                station.ResetStats();
            }
            foreach (var interactable in FindObjectsByType<InteractableObject>(FindObjectsSortMode.None))
            {
                interactable.ResetStats();
            }

            string platform = PlatformDetector.PlatformTag;
            string condition = CurrentConditionName();
            _usedHeadset = PlatformDetector.IsXRActive;
            _inXrSegment = _usedHeadset;
            _xrStartTime = 0f;
            _xrControllerProfile = null;

            dataLogger.StartSession(participantId);
            // Starts in-session checkpoint uploads (web build).
            uploader?.BeginSession(_sessionStartUtc, participantId);
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
                PlatformDetector.TagFor(_usedHeadset),
                CurrentConditionName(),
                SessionTime,
                FindObjectsByType<AttentionTarget>(FindObjectsSortMode.None),
                fixationDetector != null ? fixationDetector.FixationCount : 0,
                FindObjectsByType<SdgStation>(FindObjectsSortMode.None),
                FindObjectsByType<InteractableObject>(FindObjectsSortMode.None));

            VeraBridge.Instance?.NotifySessionEnded(participantId, SessionTime);
            uploader?.UploadSessionFiles(_sessionStartUtc);

            Debug.Log($"[SessionController] Session stopped after {SessionTime:F1}s.");
        }

        /// <summary>
        /// WebXR: the session started flat at page load, then the participant
        /// pressed "Enter VR" (or left VR). Records the switch and re-stamps
        /// the platform of later rows. Both xr_session_* rows carry
        /// "headset", so they bracket the VR segment. Never fires on the
        /// native Quest build, which is in VR from the first frame.
        /// </summary>
        private void OnXRSessionChanged(bool entered)
        {
            if (!SessionRunning)
            {
                return; // StartSession stamps whatever platform is active then.
            }

            string platform = PlatformDetector.TagFor(entered);
            if (entered)
            {
                _usedHeadset = true;
                _inXrSegment = true;
                _xrStartTime = SessionTime;
                // Often still unknown here: controllers connect a few frames
                // after the session starts. Update() keeps looking, so
                // xr_session_end reports it.
                _xrControllerProfile = PlatformDetector.XRControllerProfile;
                SetPlatform(platform);
                eventLogger?.LogEvent("xr_session_start", "",
                    $"rig=vr;xr_device={PlatformDetector.XRDeviceName};" +
                    $"controller_profile={_xrControllerProfile ?? "unknown"}");
            }
            else
            {
                _inXrSegment = false;
                eventLogger?.LogEvent("xr_session_end", "",
                    $"rig=desktop;xr_duration_s={SessionTime - _xrStartTime:F1};" +
                    $"controller_profile={_xrControllerProfile ?? "unknown"}");
                SetPlatform(platform);
            }
        }

        private void SetPlatform(string platform)
        {
            eventLogger?.SetPlatform(platform);
            VeraBridge.Instance?.NotifyPlatformChanged(platform);
        }

        private void Update()
        {
            if (!SessionRunning)
            {
                return;
            }

            SessionTime += Time.unscaledDeltaTime;
            if (_inXrSegment && _xrControllerProfile == null)
            {
                _xrControllerProfile = PlatformDetector.XRControllerProfile;
            }
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
