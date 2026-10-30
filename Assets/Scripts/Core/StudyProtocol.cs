namespace NSFGrant.Core
{
    /// <summary>
    /// Switches fixed by the IRB-approved protocol (SJSU IRB 26-285,
    /// approved 2026-10-30). Changing any of these changes what the study
    /// collects, which needs an IRB modification first - so they live in
    /// code, not in Inspector fields a session could flip by accident.
    /// (The switches are static readonly rather than const so the code
    /// paths they turn off don't trip unreachable-code warnings.)
    /// </summary>
    public static class StudyProtocol
    {
        public const string IrbProtocolId = "26-285";

        /// <summary>
        /// "Eye-gaze, fixation-duration, first-fixation, and eye-scan
        /// measurements will not be collected" (Think-Aloud and Quest
        /// Measures, 10.12.26). When false: no eye tracking, and the
        /// per-frame file keeps head pose only (gaze, fixation and
        /// look-target columns are left empty), with no gaze dwell or
        /// fixation summary. Head gaze still works as a selection pointer;
        /// it just isn't recorded.
        /// </summary>
        public static readonly bool CollectGazeMeasures = false;

        /// <summary>
        /// The post-exploration value-ranking task is not among the
        /// approved instruments, so it is skipped.
        /// </summary>
        public static readonly bool RunValueRanking = false;

        /// <summary>
        /// A zone counts as "explored" for the mid-study questionnaire
        /// trigger after this much time inside it, or after interacting
        /// with information in it.
        /// </summary>
        public const float ZoneExploredSeconds = 30f;

        /// <summary>
        /// Browser payment threshold from the consent form: at least five
        /// minutes of active exploration. Logged, not enforced.
        /// </summary>
        public const float PaymentMinExplorationSeconds = 300f;
    }
}
