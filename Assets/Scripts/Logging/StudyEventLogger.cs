using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using NSFGrant.Core;
using NSFGrant.Vera;

namespace NSFGrant.Logging
{
    /// <summary>
    /// Discrete-event log complementing the continuous gaze stream in
    /// <see cref="AttentionDataLogger"/>. Captures the interaction data the
    /// VERA team asked for: clicks/selections with both 2D screen coordinates
    /// (laptop) and 3D world positions, key presses, station enter/exit,
    /// docent guidance, quiz responses, and session lifecycle events.
    ///
    /// One CSV per session: events_&lt;participant&gt;_&lt;timestamp&gt;.csv
    /// </summary>
    public class StudyEventLogger : MonoBehaviour
    {
        private const string Header =
            "timestamp_utc_ms,session_time_s,platform,condition," +
            "event_type,target_id,detail," +
            "world_x,world_y,world_z,screen_x,screen_y";

        [SerializeField] private float flushIntervalSeconds = 5f;

        private StreamWriter _writer;
        private float _lastFlushTime;
        private string _platform = "";
        private string _condition = "";

        public static StudyEventLogger Instance { get; private set; }

        /// <summary>Seconds since session start; set by the SessionController.</summary>
        public float SessionTime { get; set; }

        public bool IsLogging => _writer != null;

        private void Awake()
        {
            Instance = this;
        }

        public void StartSession(string participantId, string platform, string condition)
        {
            StopSession();
            _platform = Sanitize(platform);
            _condition = Sanitize(condition);

            string dir = StudyPaths.Root;
            Directory.CreateDirectory(dir);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, $"events_{Sanitize(participantId)}_{stamp}.csv");

            _writer = new StreamWriter(path, false, Encoding.UTF8);
            _writer.WriteLine(Header);
            _lastFlushTime = Time.unscaledTime;
            Debug.Log($"[StudyEventLogger] Logging events to {path}");
        }

        /// <summary>Logs an event with neither world nor screen position.</summary>
        public void LogEvent(string eventType, string targetId = "", string detail = "")
        {
            WriteRow(eventType, targetId, detail, null, null);
        }

        /// <summary>Logs an event with a 3D world position (e.g., VR selection).</summary>
        public void LogWorldEvent(string eventType, string targetId, string detail, Vector3 worldPos)
        {
            WriteRow(eventType, targetId, detail, worldPos, null);
        }

        /// <summary>
        /// Logs an event with both world and 2D screen coordinates
        /// (e.g., a mouse click in the desktop/web build).
        /// </summary>
        public void LogPointerEvent(string eventType, string targetId, string detail,
            Vector3 worldPos, Vector2 screenPos)
        {
            WriteRow(eventType, targetId, detail, worldPos, screenPos);
        }

        private void WriteRow(string eventType, string targetId, string detail,
            Vector3? worldPos, Vector2? screenPos)
        {
            if (_writer == null)
            {
                return;
            }

            // Re-publish on the VERA seam so the plugin (once installed)
            // receives the identical event stream the CSV records.
            VeraBridge.Instance?.NotifyEvent(eventType, targetId, detail);

            var inv = CultureInfo.InvariantCulture;
            var row = new StringBuilder(256);
            row.Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).Append(',');
            row.Append(SessionTime.ToString("F4", inv)).Append(',');
            row.Append(_platform).Append(',');
            row.Append(_condition).Append(',');
            row.Append(Sanitize(eventType)).Append(',');
            row.Append(Sanitize(targetId)).Append(',');
            row.Append(Sanitize(detail)).Append(',');

            if (worldPos.HasValue)
            {
                row.Append(worldPos.Value.x.ToString("F4", inv)).Append(',');
                row.Append(worldPos.Value.y.ToString("F4", inv)).Append(',');
                row.Append(worldPos.Value.z.ToString("F4", inv)).Append(',');
            }
            else
            {
                row.Append(",,,");
            }

            if (screenPos.HasValue)
            {
                row.Append(screenPos.Value.x.ToString("F1", inv)).Append(',');
                row.Append(screenPos.Value.y.ToString("F1", inv));
            }
            else
            {
                row.Append(',');
            }

            _writer.WriteLine(row.ToString());

            if (Time.unscaledTime - _lastFlushTime >= flushIntervalSeconds)
            {
                _writer.Flush();
                _lastFlushTime = Time.unscaledTime;
            }
        }

        public void StopSession()
        {
            if (_writer == null)
            {
                return;
            }
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            return value.Replace(',', ';').Replace('\n', ' ').Replace('\r', ' ');
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _writer?.Flush();
            }
        }

        private void OnDestroy()
        {
            StopSession();
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
