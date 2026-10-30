using UnityEngine;

namespace NSFGrant.Survey
{
    /// <summary>
    /// In-app items from the IRB 26-285 approved instruments (Drive folder
    /// "IRB" working folder, files dated 9.10.26 / 10.12.26). Wording is
    /// verbatim from the approved PDFs - do not edit without an IRB
    /// modification.
    ///
    ///   SDG Knowledge Test: R1-R3 pre only, K1-K3 pre and post.
    ///   Survey Questionnaire, Mid-Study: M1-M4, once, after the second
    ///   distinct SDG zone has been explored (MidStudyCheckIn).
    ///
    /// The rest of the questionnaire (P2-P9 pre; K4, E, A, F, AI, B, T, O
    /// post) has free-text and conditional items and runs outside Unity:
    /// VERA portal cards (tools/vera/questionnaires/*.vqf) or Qualtrics.
    /// </summary>
    public static class ApprovedInstruments
    {
        private const string PreferNot = "Prefer not to answer";

        public static QuizDefinition KnowledgeTest()
        {
            string[] sdgs = { "SDG 4", "SDG 11", "SDG 13", "Not sure" };
            var quiz = ScriptableObject.CreateInstance<QuizDefinition>();
            quiz.name = "SdgKnowledgeTest";
            quiz.questions = new[]
            {
                Item("R1", "pre", "Which Sustainable Development Goal is Quality Education?", sdgs, 0),
                Item("R2", "pre", "Which Sustainable Development Goal is Sustainable Cities and Communities?", sdgs, 1),
                Item("R3", "pre", "Which Sustainable Development Goal is Climate Action?", sdgs, 2),
                Item("K1", "pre,post",
                    "SDG 4 - Quality Education. Which statement best describes how libraries can advance Quality Education?",
                    new[]
                    {
                        "By limiting learning services to formally enrolled students",
                        "By expanding equitable access to education, information, digital technologies, and lifelong learning opportunities",
                        "By focusing only on preservation of historical materials",
                        "By providing emergency response services only",
                    }, 1),
                Item("K2", "pre,post",
                    "SDG 11 - Sustainable Cities and Communities. Which library activity is an example of strengthening sustainable and resilient communities?",
                    new[]
                    {
                        "Serving as a resilience hub that provides trusted information, internet connectivity, charging, safe space, and access to essential services during emergencies",
                        "Restricting community partnerships during emergencies",
                        "Reducing access to local cultural and historical information",
                        "Limiting civic participation to government agencies",
                    }, 0),
                Item("K3", "pre,post",
                    "SDG 13 - Climate Action. Which approach is described as a way libraries can advance Climate Action?",
                    new[]
                    {
                        "Avoiding environmental topics because they are outside the library role",
                        "Combining climate-literacy education with practical activities such as repair, reuse, makerspaces, and community partnerships",
                        "Providing climate information only to environmental professionals",
                        "Replacing community programs with building-efficiency projects alone",
                    }, 1),
            };
            return quiz;
        }

        public static QuizDefinition MidStudy()
        {
            var quiz = ScriptableObject.CreateInstance<QuizDefinition>();
            quiz.name = "MidStudyQuestionnaire";
            quiz.questions = new[]
            {
                Item("M1", "", "How engaged do you feel with the experience right now?",
                     Scale("Not at all engaged", "Extremely engaged"), -1),
                Item("M2", "", "How mentally demanding or overwhelming has the experience been so far?",
                     Scale("Not at all", "Extremely"), -1),
                Item("M3", "", "How easy has it been to navigate the environment?",
                     Scale("Very difficult", "Very easy"), -1),
                Item("M4", "", "How interested are you in continuing to explore?",
                     Scale("Not at all interested", "Extremely interested"), -1),
            };
            return quiz;
        }

        /// <summary>1-5 scale with anchored ends, plus "Prefer not to answer" (index 5).</summary>
        private static string[] Scale(string low, string high)
        {
            return new[] { $"1 - {low}", "2", "3", "4", $"5 - {high}", PreferNot };
        }

        private static QuizDefinition.Question Item(string id, string stages, string prompt,
                                                    string[] options, int correctIndex)
        {
            return new QuizDefinition.Question
            {
                questionId = id,
                stages = stages,
                prompt = prompt,
                options = options,
                correctIndex = correctIndex,
            };
        }
    }
}
