using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NSFGrant.EditorTools
{
    /// <summary>
    /// Offline analysis tool: turns a session's events CSV into a click
    /// heatmap. Reads the discrete-event log written by
    /// <see cref="NSFGrant.Logging.StudyEventLogger"/> (events_*.csv),
    /// keeps the click / selection events, and renders:
    ///   - a top-down (world X/Z) spatial heatmap PNG over a fixed hall
    ///     extent, so maps are comparable across participants,
    ///   - a screen-space heatmap PNG when 2D click coords are present
    ///     (desktop / WebGL sessions),
    ///   - a per-AOI click-count summary (.txt).
    ///
    /// Purely a post-processing utility: it does not touch the study scene
    /// or the data-collection runtime, so it cannot affect a session.
    /// Menu: NSF Grant &gt; Analysis &gt; Generate Click Heatmap...
    /// </summary>
    public static class ClickHeatmapTool
    {
        // Event types that count as a click/selection.
        private static readonly HashSet<string> ClickEvents =
            new HashSet<string> { "click", "object_activated" };

        // Fixed top-down extent (metres from hall centre) so every session's
        // world heatmap shares the same frame. The hub+rooms span ~+/-20 m.
        private const float HallExtent = 22f;
        private const int WorldSize = 1024;
        private const int ScreenSize = 1024;

        [MenuItem("NSF Grant/Analysis/Generate Click Heatmap...")]
        public static void GenerateHeatmap()
        {
            string startDir = Directory.Exists(Application.persistentDataPath)
                ? Application.persistentDataPath
                : "";
            string csvPath = EditorUtility.OpenFilePanel(
                "Select an events_*.csv session log", startDir, "csv");
            if (string.IsNullOrEmpty(csvPath))
            {
                return;
            }

            string[] lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2)
            {
                EditorUtility.DisplayDialog("Click Heatmap",
                    "That CSV has no data rows.", "OK");
                return;
            }

            var cols = IndexColumns(lines[0]);
            foreach (string required in new[]
                     { "event_type", "target_id", "world_x", "world_z" })
            {
                if (!cols.ContainsKey(required))
                {
                    EditorUtility.DisplayDialog("Click Heatmap",
                        $"Column '{required}' not found — is this an events_*.csv?", "OK");
                    return;
                }
            }

            var worldPoints = new List<Vector2>();   // (x, z)
            var screenPoints = new List<Vector2>();   // (screen_x, screen_y)
            var perTarget = new Dictionary<string, int>();

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }
                string[] f = lines[i].Split(',');
                string type = Field(f, cols, "event_type");
                if (!ClickEvents.Contains(type))
                {
                    continue;
                }

                string target = Field(f, cols, "target_id");
                if (!string.IsNullOrEmpty(target))
                {
                    perTarget.TryGetValue(target, out int c);
                    perTarget[target] = c + 1;
                }

                if (TryFloat(Field(f, cols, "world_x"), out float wx) &&
                    TryFloat(Field(f, cols, "world_z"), out float wz))
                {
                    worldPoints.Add(new Vector2(wx, wz));
                }

                if (cols.ContainsKey("screen_x") && cols.ContainsKey("screen_y") &&
                    TryFloat(Field(f, cols, "screen_x"), out float sx) &&
                    TryFloat(Field(f, cols, "screen_y"), out float sy))
                {
                    screenPoints.Add(new Vector2(sx, sy));
                }
            }

            if (worldPoints.Count == 0 && screenPoints.Count == 0)
            {
                EditorUtility.DisplayDialog("Click Heatmap",
                    "No click/selection events with coordinates were found in this log.", "OK");
                return;
            }

            string dir = Path.GetDirectoryName(csvPath);
            string baseName = Path.GetFileNameWithoutExtension(csvPath);
            var outputs = new List<string>();

            if (worldPoints.Count > 0)
            {
                // Map world (x, z) into [0,1] over the fixed hall extent.
                // Flip Z so +Z (forward, toward the rooms) renders upward.
                var uv = new List<Vector2>(worldPoints.Count);
                foreach (Vector2 p in worldPoints)
                {
                    uv.Add(new Vector2(
                        Mathf.InverseLerp(-HallExtent, HallExtent, p.x),
                        Mathf.InverseLerp(-HallExtent, HallExtent, p.y)));
                }
                string p1 = Path.Combine(dir, $"heatmap_world_{baseName}.png");
                RenderHeatmap(uv, WorldSize, WorldSize, p1);
                outputs.Add($"{p1}  ({worldPoints.Count} world clicks, top-down +/-{HallExtent} m)");
            }

            if (screenPoints.Count > 0)
            {
                // Screen capture resolution varies per session, so normalise
                // to the observed click bounds (clusters stay meaningful).
                Bounds2D b = Bounds2D.Of(screenPoints, 0.05f);
                var uv = new List<Vector2>(screenPoints.Count);
                foreach (Vector2 p in screenPoints)
                {
                    uv.Add(new Vector2(b.NormalizeX(p.x), b.NormalizeY(p.y)));
                }
                string p2 = Path.Combine(dir, $"heatmap_screen_{baseName}.png");
                RenderHeatmap(uv, ScreenSize, ScreenSize, p2);
                outputs.Add($"{p2}  ({screenPoints.Count} on-screen clicks, normalised to click bounds)");
            }

            string summaryPath = Path.Combine(dir, $"clickcounts_{baseName}.csv");
            WriteTargetSummary(summaryPath, perTarget);
            outputs.Add(summaryPath);

            AssetDatabase.Refresh();
            Debug.Log("[ClickHeatmapTool] Wrote:\n  " + string.Join("\n  ", outputs));
            EditorUtility.RevealInFinder(outputs[0].Split(' ')[0]);
        }

        private static Dictionary<string, int> IndexColumns(string header)
        {
            var map = new Dictionary<string, int>();
            string[] names = header.Split(',');
            for (int i = 0; i < names.Length; i++)
            {
                map[names[i].Trim()] = i;
            }
            return map;
        }

        private static string Field(string[] fields, Dictionary<string, int> cols, string name)
        {
            int idx = cols[name];
            return idx < fields.Length ? fields[idx].Trim() : "";
        }

        private static bool TryFloat(string s, out float value)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Splats each point with a Gaussian kernel into a density grid, then
        /// colourises with a blue-&gt;cyan-&gt;green-&gt;yellow-&gt;red ramp and writes a PNG.
        /// </summary>
        private static void RenderHeatmap(List<Vector2> uv, int width, int height, string path)
        {
            var density = new float[width * height];
            int radius = Mathf.Max(8, width / 40);
            float sigma = radius / 2f;
            float twoSigmaSq = 2f * sigma * sigma;

            foreach (Vector2 p in uv)
            {
                int cx = Mathf.Clamp(Mathf.RoundToInt(p.x * (width - 1)), 0, width - 1);
                int cy = Mathf.Clamp(Mathf.RoundToInt(p.y * (height - 1)), 0, height - 1);
                int minX = Mathf.Max(0, cx - radius), maxX = Mathf.Min(width - 1, cx + radius);
                int minY = Mathf.Max(0, cy - radius), maxY = Mathf.Min(height - 1, cy + radius);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                        density[y * width + x] += Mathf.Exp(-d2 / twoSigmaSq);
                    }
                }
            }

            float max = 0f;
            for (int i = 0; i < density.Length; i++)
            {
                if (density[i] > max) max = density[i];
            }
            if (max <= 0f) max = 1f;

            var bg = new Color(0.05f, 0.05f, 0.08f);
            var pixels = new Color32[width * height];
            for (int i = 0; i < density.Length; i++)
            {
                // sqrt spreads the low end so sparse clicks stay visible.
                float t = Mathf.Sqrt(density[i] / max);
                Color c = t < 0.02f ? bg : Color.Lerp(bg, Ramp(t), Mathf.Clamp01(t * 1.4f));
                pixels[i] = c;
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static Color Ramp(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.35f) return Color.Lerp(new Color(0f, 0f, 0.55f), new Color(0f, 0.45f, 1f), t / 0.35f);
            if (t < 0.55f) return Color.Lerp(new Color(0f, 0.45f, 1f), new Color(0f, 1f, 0.55f), (t - 0.35f) / 0.2f);
            if (t < 0.75f) return Color.Lerp(new Color(0f, 1f, 0.55f), new Color(1f, 1f, 0f), (t - 0.55f) / 0.2f);
            return Color.Lerp(new Color(1f, 1f, 0f), new Color(1f, 0f, 0f), (t - 0.75f) / 0.25f);
        }

        private static void WriteTargetSummary(string path, Dictionary<string, int> perTarget)
        {
            var sorted = new List<KeyValuePair<string, int>>(perTarget);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            using var w = new StreamWriter(path, false);
            w.WriteLine("clicks,target_id");
            foreach (var kv in sorted)
            {
                w.WriteLine($"{kv.Value},{kv.Key}");
            }
        }

        private struct Bounds2D
        {
            private float _minX, _maxX, _minY, _maxY;

            public static Bounds2D Of(List<Vector2> pts, float padFraction)
            {
                var b = new Bounds2D
                {
                    _minX = float.MaxValue, _maxX = float.MinValue,
                    _minY = float.MaxValue, _maxY = float.MinValue
                };
                foreach (Vector2 p in pts)
                {
                    b._minX = Mathf.Min(b._minX, p.x); b._maxX = Mathf.Max(b._maxX, p.x);
                    b._minY = Mathf.Min(b._minY, p.y); b._maxY = Mathf.Max(b._maxY, p.y);
                }
                float padX = Mathf.Max(1f, (b._maxX - b._minX) * padFraction);
                float padY = Mathf.Max(1f, (b._maxY - b._minY) * padFraction);
                b._minX -= padX; b._maxX += padX; b._minY -= padY; b._maxY += padY;
                return b;
            }

            public float NormalizeX(float x) => Mathf.InverseLerp(_minX, _maxX, x);
            public float NormalizeY(float y) => Mathf.InverseLerp(_minY, _maxY, y);
        }
    }
}
