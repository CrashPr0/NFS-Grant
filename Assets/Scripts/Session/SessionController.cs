using UnityEngine;
using NSFGrant.Gaze;
using NSFGrant.Logging;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace NSFGrant.Session
{
    /// <summary>
    /// Orchestrates a data-collection session: requests the eye-tracking
    /// permission (Quest Pro), starts/stops the logger, drives per-frame
    /// sampling, and writes the per-target summary when the session ends.
    /// </summary>
    public class SessionController : MonoBehaviour
    {
        private const string EyeTrackingPermission = "com.oculus.permission.EYE_TRACKING";

        [Tooltip("Identifier recorded in the data files. Set per participant before each build/run, or via a launch UI.")]
        [SerializeField] private string participantId = "P000";

        [Tooltip("Begin logging as soon as the scene loads.")]
        [SerializeField] private bool autoStart = true;

        [SerializeField] private GazeProvider gazeProvider;
        [SerializeField] private GazeRaycaster gazeRaycaster;
        [SerializeField] private FixationDetector fixationDetector;
        [SerializeField] private AttentionDataLogger dataLogger;

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

            dataLogger.StartSession(participantId);
            SessionRunning = true;
            Debug.Log($"[SessionController] Session started for participant '{participantId}'.");
        }

        public void StopSession()
        {
            if (!SessionRunning)
            {
                return;
            }

            SessionRunning = false;
            dataLogger.StopSession();
            dataLogger.WriteSummary(
                participantId,
                SessionTime,
                FindObjectsOfType<AttentionTarget>(),
                fixationDetector != null ? fixationDetector.FixationCount : 0);
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
