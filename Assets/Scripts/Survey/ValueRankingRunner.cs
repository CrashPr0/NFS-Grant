using System.Collections.Generic;
using UnityEngine;
using NSFGrant.Logging;
using NSFGrant.UI;

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

            float baseUnit = StudyGuiKit.BaseUnit();

            float pad         = 30f * baseUnit;
            float headerH     = 58f * baseUnit;
            float progressH   = 5f  * baseUnit;
            float promptH     = 56f * baseUnit;
            float instructH   = 30f * baseUnit;
            float btnH        = 46f * baseUnit;
            float btnGap      = 10f * baseUnit;

            int count = definition.values.Length;

            float cardW = Mathf.Min(620f * baseUnit, Screen.width - 60f);
            float cardH = Mathf.Min(
                headerH + progressH + pad + promptH + instructH + 10f * baseUnit
                    + count * (btnH + btnGap) - btnGap + pad,
                Screen.height - 60f);
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            StudyGuiKit.DrawOverlay();
            StudyGuiKit.DrawCard(new Rect(cardX, cardY, cardW, cardH));
            StudyGuiKit.DrawAccentHeader(new Rect(cardX, cardY, cardW, headerH));

            GUI.Label(new Rect(cardX + pad, cardY, cardW * 0.65f, headerH),
                      "RANK YOUR VALUES", StudyGuiKit.HeaderStyle(baseUnit));
            GUI.Label(new Rect(cardX, cardY, cardW - pad * 0.8f, headerH),
                      $"{_ranked.Count} / {count}", StudyGuiKit.CounterStyle(baseUnit));

            float pY = cardY + headerH;
            StudyGuiKit.DrawProgressBar(new Rect(cardX, pY, cardW, progressH),
                                         count > 0 ? (float)_ranked.Count / count : 0f);

            float qX = cardX + pad;
            float qW = cardW - pad * 2f;
            float qY = pY + progressH + pad;

            var promptStyle = StudyGuiKit.BodyStyle(baseUnit);
            promptStyle.fontSize = Mathf.RoundToInt(19 * baseUnit);
            GUI.Label(new Rect(qX, qY, qW, promptH), definition.prompt, promptStyle);

            var instructStyle = StudyGuiKit.BodyStyle(baseUnit);
            instructStyle.fontSize = Mathf.RoundToInt(13 * baseUnit);
            GUI.Label(new Rect(qX, qY + promptH, qW, instructH),
                      "Click in order of importance — most important first.", instructStyle);

            float bY = qY + promptH + instructH + 10f * baseUnit;
            for (int i = 0; i < count; i++)
            {
                int rankPos = _ranked.IndexOf(i);
                bool chosen = rankPos >= 0;
                var rect = new Rect(qX, bY, qW, btnH);
                if (chosen)
                {
                    StudyGuiKit.DoneButton(rect, $"{rankPos + 1}.  {definition.values[i]}", baseUnit);
                }
                else if (GUI.Button(rect, definition.values[i], StudyGuiKit.ButtonStyle(baseUnit)))
                {
                    Assign(i);
                }
                bY += btnH + btnGap;
            }
        }
    }
}
