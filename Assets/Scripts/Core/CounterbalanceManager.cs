using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NSFGrant.Logging;
using NSFGrant.Stations;

namespace NSFGrant.Core
{
    /// <summary>
    /// Removes the format-vs-position confound (see docs/AESTHETICS_PLAN.md,
    /// Part A). Without this, the video kiosk is always centered and every
    /// participant spawns facing the center station, so positional salience
    /// is correlated with information format and first fixations are
    /// predetermined.
    ///
    /// At session start the manager deterministically derives, from the
    /// participant ID:
    ///   1. a Latin-square row that permutes the five format zones across
    ///      the position slots within each station (offset per station),
    ///   2. a permutation of the station arc positions,
    ///   3. a spawn heading for the rig.
    /// The full assignment is written to the event log so analysis can model
    /// position as a covariate. Runs at runtime (not scene-bake) because one
    /// build serves many participants.
    /// </summary>
    public class CounterbalanceManager : MonoBehaviour
    {
        [Tooltip("Disable for demos where a fixed, curated layout is preferred.")]
        [SerializeField] private bool counterbalancingEnabled = true;

        [Tooltip("Root of the camera rigs; rotated to vary the spawn heading.")]
        [SerializeField] private Transform rigRoot;

        [Tooltip("Spawn headings (deg) participants are counterbalanced across.")]
        [SerializeField] private float[] spawnHeadings = { -55f, -27f, 0f, 27f, 55f };

        /// <summary>Human-readable assignment string, logged at session start.</summary>
        public string AssignmentDescription { get; private set; } = "disabled";

        public void Apply(string participantId)
        {
            if (!counterbalancingEnabled)
            {
                AssignmentDescription = "disabled";
                return;
            }

            int seed = StableHash(participantId);
            var parts = new List<string> { $"seed={seed}" };

            // Stations sorted by ID so the procedure is deterministic.
            var stations = FindObjectsOfType<SdgStation>()
                .OrderBy(s => s.StationId).ToArray();

            PermuteStationPositions(stations, seed, parts);
            PermuteZonesWithinStations(stations, seed, parts);
            ApplySpawnHeading(seed, parts);

            AssignmentDescription = string.Join("|", parts);
            StudyEventLogger.Instance?.LogEvent(
                "counterbalance_assignment", participantId, AssignmentDescription);
            Debug.Log($"[CounterbalanceManager] {AssignmentDescription}");
        }

        private void PermuteStationPositions(SdgStation[] stations, int seed, List<string> parts)
        {
            if (stations.Length < 2)
            {
                return;
            }

            // The arc slots are wherever the builder placed the stations.
            var slots = stations
                .Select(s => (s.transform.position, s.transform.rotation)).ToArray();
            int[] order = Permutation(stations.Length, seed);

            for (int i = 0; i < stations.Length; i++)
            {
                stations[i].transform.SetPositionAndRotation(
                    slots[order[i]].position, slots[order[i]].rotation);
            }

            parts.Add("stations=" + string.Join(
                ",", stations.Select((s, i) => $"{s.StationId}:slot{order[i]}")));
        }

        private void PermuteZonesWithinStations(SdgStation[] stations, int seed, List<string> parts)
        {
            for (int s = 0; s < stations.Length; s++)
            {
                var zones = stations[s].GetComponentsInChildren<CounterbalancedZone>(true)
                    .OrderBy(z => z.SlotIndex).ToArray();
                if (zones.Length < 2)
                {
                    continue;
                }

                Vector3[] slotPositions = zones.Select(z => z.transform.localPosition).ToArray();

                // Cyclic Latin-square row, offset per station so a participant
                // does not see the same arrangement at every station.
                int row = Mod(seed + s, zones.Length);
                for (int j = 0; j < zones.Length; j++)
                {
                    zones[j].transform.localPosition = slotPositions[(row + j) % zones.Length];
                }

                parts.Add($"{stations[s].StationId}:row{row}");
            }
        }

        private void ApplySpawnHeading(int seed, List<string> parts)
        {
            if (rigRoot == null || spawnHeadings == null || spawnHeadings.Length == 0)
            {
                return;
            }

            float heading = spawnHeadings[Mod(seed / 7, spawnHeadings.Length)];
            rigRoot.rotation = Quaternion.Euler(0f, heading, 0f);
            parts.Add($"heading={heading:F0}");
        }

        /// <summary>Seeded Fisher-Yates permutation of 0..n-1.</summary>
        private static int[] Permutation(int n, int seed)
        {
            var result = Enumerable.Range(0, n).ToArray();
            var rng = new System.Random(seed);
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
            return result;
        }

        /// <summary>
        /// FNV-1a hash — stable across platforms and runs, unlike
        /// string.GetHashCode, so web and headset assign identically.
        /// </summary>
        private static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in value ?? "")
                {
                    hash = (hash ^ c) * 16777619;
                }
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        private static int Mod(int a, int n) => ((a % n) + n) % n;
    }
}
