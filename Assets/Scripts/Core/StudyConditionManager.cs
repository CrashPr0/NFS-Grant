using UnityEngine;

namespace NSFGrant.Core
{
    /// <summary>
    /// The three experimental versions from Dr. Chow's research proposal.
    /// </summary>
    public enum StudyCondition
    {
        /// <summary>Condition A: text, images, videos, static displays only.</summary>
        Passive,

        /// <summary>Condition B: participants manipulate data, objects, simulations.</summary>
        Interactive,

        /// <summary>Condition C: an AI/virtual docent highlights key information and suggests pathways.</summary>
        Guided
    }

    /// <summary>
    /// Holds the active study condition for the session. Set it in the
    /// Inspector before a lab session, or assign it programmatically
    /// (e.g., from a URL parameter on WebGL or from the VERA plugin)
    /// before the session starts.
    /// </summary>
    public class StudyConditionManager : MonoBehaviour
    {
        [SerializeField] private StudyCondition condition = StudyCondition.Interactive;

        public static StudyConditionManager Instance { get; private set; }

        public StudyCondition Condition
        {
            get => condition;
            set => condition = value;
        }

        /// <summary>Interactable content is active in Interactive and Guided conditions.</summary>
        public bool InteractionEnabled => condition != StudyCondition.Passive;

        /// <summary>The docent is only active in the Guided condition.</summary>
        public bool DocentEnabled => condition == StudyCondition.Guided;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
