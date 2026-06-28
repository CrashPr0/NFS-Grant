using UnityEngine;

namespace NSFGrant.Survey
{
    /// <summary>
    /// Defines a "prioritize these values" ranking task: a prompt and a small
    /// set of values (the team asked for four) the participant orders by
    /// importance. Authored as a ScriptableObject so the exact wording and the
    /// value set can be edited without code changes; the scene builder seeds a
    /// default asset which the content team should confirm/replace.
    /// </summary>
    [CreateAssetMenu(menuName = "NSF Grant/Value Ranking", fileName = "SdgValueRanking")]
    public class ValueRankingDefinition : ScriptableObject
    {
        [Tooltip("Stable id written with each response for analysis.")]
        public string rankingId = "sdg_values";

        [TextArea]
        public string prompt =
            "Rank these from most (1) to least (4) important to you.";

        [Tooltip("The values to rank. The team requested four.")]
        public string[] values;
    }
}
