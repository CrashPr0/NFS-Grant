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
    /// Writes per-frame gaze/attention samples to a CSV file under
    /// Application.persistentDataPath/StudyData. One file is created per
    /// session, named with the participant ID and a UTC timestamp.
    ///
    /// Under IRB 26-285 (StudyProtocol.CollectGazeMeasures = false) only
    /// head pose is kept: the file is named motion_*.csv, the gaze,
    /// fixation and look-target columns stay empty (same header, so
    /// analysis scripts still parse it), and the summary has no fixation
    /// count or gaze dwell table.
    ///
    /// On Quest, files land in
    ///   /sdcard/Android/data/&lt;package&gt;/files/StudyData/
    /// and can be retrieved with:
    ///   adb pull /sdcard/Android/data/&lt;package&gt;/files/StudyData
    /// </summary>
    public class AttentionDataLogger : MonoBehaviour
    {
        private const string Header =
            "timestamp_utc_ms,session_time_s,frame," +
            "head_pos_x,head_pos_y,head_pos_z," +
            "head_rot_x,head_rot_y,head_rot_z,head_rot_w," +
            "gaze_origin_x,gaze_origin_y,gaze_origin_z," +
            "gaze_dir_x,gaze_dir_y,gaze_dir_z," +
            "gaze_source,gaze_confidence,angular_velocity_deg_s," +
            "is_fixating,fixation_id," +
            "hit_target,hit_point_x,hit_point_y,hit_point_z,hit_distance";

        [Tooltip("Flush buffered samples to disk every N seconds.")]
        [SerializeField] private float flushIntervalSeconds = 5f;

        private StreamWriter _writer;
        private readonly StringBuilder _row = new StringBuilder(512);
        private float _lastFlushTime;

        /// <summary>Full path of the file currently being written, or null.</summary>
        public string CurrentFilePath { get; private set; }

        public bool IsLogging => _writer != null;

        public string DataDirectory => StudyPaths.Root;

        public void StartSession(string participantId)
        {
            StopSession();

            Directory.CreateDirectory(DataDirectory);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string safeId = StudyPaths.FileToken(participantId);
            string prefix = StudyProtocol.CollectGazeMeasures ? "gaze" : "motion";
            CurrentFilePath = Path.Combine(DataDirectory, $"{prefix}_{safeId}_{stamp}.csv");

            _writer = new StreamWriter(CurrentFilePath, false, Encoding.UTF8);
            _writer.WriteLine(Header);
            _lastFlushTime = Time.unscaledTime;

            Debug.Log($"[AttentionDataLogger] Logging to {CurrentFilePath}");
        }

        public void LogSample(
            float sessionTime,
            Vector3 headPos, Quaternion headRot,
            Vector3 gazeOrigin, Vector3 gazeDir,
            string gazeSource, float confidence, float angularVelocity,
            bool isFixating, int fixationId,
            string hitTarget, Vector3 hitPoint, float hitDistance, bool hasHit)
        {
            if (_writer == null)
            {
                return;
            }

            if (!StudyProtocol.CollectGazeMeasures)
            {
                var none = new Vector3(float.NaN, float.NaN, float.NaN);
                gazeOrigin = none; gazeDir = none;
                gazeSource = ""; confidence = float.NaN; angularVelocity = float.NaN;
                isFixating = false; fixationId = -1;
                hitTarget = ""; hasHit = false;
            }

            var inv = CultureInfo.InvariantCulture;
            _row.Clear();
            _row.Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).Append(',');
            _row.Append(sessionTime.ToString("F4", inv)).Append(',');
            _row.Append(Time.frameCount).Append(',');
            AppendVector(headPos, inv);
            _row.Append(headRot.x.ToString("F5", inv)).Append(',');
            _row.Append(headRot.y.ToString("F5", inv)).Append(',');
            _row.Append(headRot.z.ToString("F5", inv)).Append(',');
            _row.Append(headRot.w.ToString("F5", inv)).Append(',');
            if (StudyProtocol.CollectGazeMeasures)
            {
                AppendVector(gazeOrigin, inv);
                AppendVector(gazeDir, inv);
                _row.Append(gazeSource).Append(',');
                _row.Append(confidence.ToString("F3", inv)).Append(',');
                _row.Append(angularVelocity.ToString("F2", inv)).Append(',');
                _row.Append(isFixating ? '1' : '0').Append(',');
                _row.Append(fixationId).Append(',');
                _row.Append(Sanitize(hitTarget)).Append(',');
            }
            else
            {
                // gaze origin/dir, source, confidence, velocity, fixation
                // flag + id, hit target: 12 empty columns.
                _row.Append(',', 12);
            }
            if (hasHit)
            {
                AppendVector(hitPoint, inv);
                _row.Append(hitDistance.ToString("F4", inv));
            }
            else
            {
                _row.Append(",,,");
            }

            _writer.WriteLine(_row.ToString());

            if (Time.unscaledTime - _lastFlushTime >= flushIntervalSeconds)
            {
                Flush();
            }

            // Same row to VERA's Gaze_Samples file (VeraPluginAdapter).
            var bridge = VeraBridge.Instance;
            if (bridge != null && bridge.WantsGaze)
            {
                bridge.NotifyGazeSample(new VeraBridge.GazeSample(
                    Time.frameCount, headPos, headRot, gazeOrigin, gazeDir, gazeSource, confidence,
                    angularVelocity, isFixating, fixationId, hitTarget, hitPoint, hitDistance, hasHit));
            }
        }

        public void WriteSummary(string participantId, string platform, string condition,
            float sessionDuration, Gaze.AttentionTarget[] targets, int fixationCount,
            Stations.SdgStation[] stations, Interaction.InteractableObject[] interactables)
        {
            Directory.CreateDirectory(DataDirectory);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(DataDirectory,
                $"summary_{StudyPaths.FileToken(participantId)}_{stamp}.csv");

            var inv = CultureInfo.InvariantCulture;
            using var summary = new StreamWriter(path, false, Encoding.UTF8);
            summary.WriteLine("participant_id,platform,condition,session_duration_s,total_fixations");
            summary.WriteLine(
                $"{Sanitize(participantId)},{Sanitize(platform)},{Sanitize(condition)}," +
                $"{sessionDuration.ToString("F2", inv)}," +
                (StudyProtocol.CollectGazeMeasures ? fixationCount.ToString(inv) : ""));

            summary.WriteLine();
            summary.WriteLine("target_id,format,station_id,total_dwell_time_s,look_count,time_to_first_look_s");
            foreach (var target in StudyProtocol.CollectGazeMeasures ? targets : new Gaze.AttentionTarget[0])
            {
                summary.WriteLine(
                    $"{Sanitize(target.TargetId)}," +
                    $"{target.Format}," +
                    $"{Sanitize(target.StationId)}," +
                    $"{target.TotalDwellTime.ToString("F3", inv)}," +
                    $"{target.LookCount}," +
                    $"{target.TimeToFirstLook.ToString("F3", inv)}");
            }

            if (stations != null && stations.Length > 0)
            {
                summary.WriteLine();
                summary.WriteLine("station_id,total_time_s,visit_count,first_entry_s");
                foreach (var station in stations)
                {
                    summary.WriteLine(
                        $"{Sanitize(station.StationId)}," +
                        $"{station.TotalTimeIncludingCurrentVisit.ToString("F2", inv)}," +
                        $"{station.VisitCount}," +
                        $"{station.FirstEntryTime.ToString("F2", inv)}");
                }
            }

            if (interactables != null && interactables.Length > 0)
            {
                summary.WriteLine();
                summary.WriteLine("object_id,activation_count");
                foreach (var interactable in interactables)
                {
                    summary.WriteLine($"{Sanitize(interactable.ObjectId)},{interactable.ActivationCount}");
                }
            }

            Debug.Log($"[AttentionDataLogger] Summary written to {path}");
        }

        /// <summary>
        /// Writes buffered rows to the file now. RemoteDataUploader calls
        /// this before a checkpoint reads the file, so the upload includes
        /// rows logged since the last timed flush.
        /// </summary>
        public void Flush()
        {
            if (_writer == null)
            {
                return;
            }
            _writer.Flush();
            _lastFlushTime = Time.unscaledTime;
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
            Debug.Log($"[AttentionDataLogger] Closed {CurrentFilePath}");
        }

        private void AppendVector(Vector3 v, CultureInfo inv)
        {
            _row.Append(v.x.ToString("F4", inv)).Append(',');
            _row.Append(v.y.ToString("F4", inv)).Append(',');
            _row.Append(v.z.ToString("F4", inv)).Append(',');
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            return value.Replace(',', '_').Replace('\n', '_').Replace('\r', '_');
        }

        private void OnApplicationPause(bool paused)
        {
            // Quest apps are paused (not quit) when the headset is removed;
            // flush so no data is lost if the OS kills the process.
            if (paused)
            {
                Flush();
            }
        }

        private void OnDestroy()
        {
            StopSession();
        }
    }
}
