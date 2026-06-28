using System.Collections.Generic;
using UnityEngine;
using NSFGrant.Logging;

namespace NSFGrant.Survey
{
    /// <summary>
    /// Administers a <see cref="ValueRankingDefinition"/>: the participant
    /// clicks the values in priority order (first click = rank 1). Each choice
    /// is written to the event log as "value_rank" (detail rank=N), and a
    /// "value_rank_complete" event records the full ordering, so prioritization
    /// can be analysed alongside the gaze/quiz data.
    ///
    /// Mirrors <see cref="QuizRunner"/>: an IMGUI panel for desktop/WebGL, plus
    /// a <see cref="RecordRanking"/> entry point a world-space VR UI (or VERA)
    /// can call instead. In VR, where IMGUI does not render, the task is skipped
    /// like the quizzes and administered outside the headset.
    /// </summary>
    public class ValueRankingRunner : MonoBehaviour
    {
        [SerializeField] private ValueRankingDefinition definition;

        private bool _visible;
        private readonly List<int> _ranked = new List<int>(); // value indices, in chosen order

        public bool IsComplete { get; private set; }

        /// <summary>Shows the ranking panel (desktop/WebGL builds).</summary>
        public void Show()
        {
            _ranked.Clear();
            bool hasValues = definition != null && definition.values != null &&
                             definition.values.Length > 0;
            _visible = hasValues;
            // No task configured counts as complete so the flow doesn't stall.
            IsComplete = !_visible;
            if (_visible)
            {
                StudyEventLogger.Instance?.LogEvent("value_rank_start",
                    definition.rankingId, $"count={definition.values.Length}");
            }
        }

        /// <summary>
        /// Records one value's rank from any UI (IMGUI, world-space VR UI, or an
        /// external bridge). Rank is 1-based.
        /// </summary>
        public void RecordRanking(string value, int rank)
        {
            StudyEventLogger.Instance?.LogEvent("value_rank", value, $"rank={rank}");
        }

        private void Assign(int valueIndex)
        {
            if (_ranked.Contains(valueIndex))
            {
                return;
            }
            _ranked.Add(valueIndex);
            RecordRanking(definition.values[valueIndex], _ranked.Count);

            if (_ranked.Count >= definition.values.Length)
            {
                _visible = false;
                IsComplete = true;
                var ordered = new string[_ranked.Count];
                for (int i = 0; i < _ranked.Count; i++)
                {
                    ordered[i] = definition.values[_ranked[i]];
                }
                StudyEventLogger.Instance?.LogEvent("value_rank_complete",
                    definition.rankingId, "order=" + string.Join(">", ordered));
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            const float width = 520f;
            float x = (Screen.width - width) * 0.5f;
            GUILayout.BeginArea(new Rect(x, 80f, width, Screen.height - 160f), GUI.skin.box);

            GUILayout.Label(definition.prompt);
            GUILayout.Space(6f);
            GUILayout.Label($"Click in order of importance (most important first). " +
                            $"{_ranked.Count} of {definition.values.Length} ranked.");
            GUILayout.Space(8f);

            for (int i = 0; i < definition.values.Length; i++)
            {
                int rankPos = _ranked.IndexOf(i);
                bool chosen = rankPos >= 0;
                GUI.enabled = !chosen;
                string label = chosen
                    ? $"{rankPos + 1}.  {definition.values[i]}"
                    : definition.values[i];
                if (GUILayout.Button(label, GUILayout.Height(34f)))
                {
                    Assign(i);
                }
                GUI.enabled = true;
            }

            GUILayout.EndArea();
        }
    }
}
