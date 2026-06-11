using System;
using UnityEngine;

namespace NSFGrant.Survey
{
    /// <summary>
    /// A multiple-choice knowledge quiz, administered pre and post session to
    /// measure SDG learning/recall. Create instances via
    /// Assets &gt; Create &gt; NSF Grant &gt; Quiz Definition.
    ///
    /// Note: VERA also provides survey/questionnaire infrastructure with
    /// real-time collection; this local quiz is the standalone fallback and
    /// keeps the web pilot self-contained. Responses go to the event log and
    /// a per-session quiz CSV either way.
    /// </summary>
    [CreateAssetMenu(fileName = "Quiz", menuName = "NSF Grant/Quiz Definition")]
    public class QuizDefinition : ScriptableObject
    {
        [Serializable]
        public class Question
        {
            [Tooltip("Stable identifier used in the data files, e.g. sdg13_q1.")]
            public string questionId;

            [TextArea]
            public string prompt;

            public string[] options;

            [Tooltip("Index into options of the correct answer; -1 for opinion/Likert items.")]
            public int correctIndex = -1;
        }

        public Question[] questions;
    }
}
