namespace NSFGrant.Content
{
    /// <summary>
    /// One SDG station's content for the Discovery Hall, curated by the
    /// SJSU LTI Lab UN SDG student team (see Drive: "Project 1 - UN SDGs" >
    /// Project1_Resources_StudentNotes, Asset Tracker, and the SDG-13
    /// prototype room). Placeholder-free text lives here so designers and
    /// researchers can review station copy in one place.
    /// </summary>
    public class SdgStationContent
    {
        public string StationId;
        public string Title;

        /// <summary>Official UN goal color (hex), used for station theming.</summary>
        public string ThemeColorHex;

        /// <summary>Goal icon file name (downloaded via NSF Grant > Download SDG Media Assets).</summary>
        public string IconFileName;

        /// <summary>Text panel: overview + key facts.</summary>
        public string OverviewText;

        /// <summary>Data visualization display: caption + source link.</summary>
        public string DataVizText;
        public string DataVizUrl;

        /// <summary>Video/audio story kiosk.</summary>
        public string VideoText;
        public string VideoUrl;

        /// <summary>Interactive object / simulation seed content.</summary>
        public string InteractiveText;
        public string InteractiveUrl;

        /// <summary>Call-to-action wall: actions a visitor can pick.</summary>
        public string[] CallToActionOptions;

        /// <summary>Conversational agent persona (from the FrameVR chat agents).</summary>
        public string DocentName;
        public string DocentGreeting;
        public string DocentInstructions;

        /// <summary>Citation list for the station's reference display.</summary>
        public string[] References;
    }

    /// <summary>
    /// Curated content for the three-station prototype (SDG 4, 11, 13).
    /// Sources: UN SDG official pages, the 2025 SDG Progress Report, IFLA
    /// Green Library Award case studies, and the student team's research.
    /// </summary>
    public static class SdgContentLibrary
    {
        public static readonly SdgStationContent[] Stations =
        {
            new SdgStationContent
            {
                StationId = "SDG04_QualityEducation",
                Title = "SDG 4 - Quality Education",
                ThemeColorHex = "#C5192D",
                IconFileName = "E-WEB-Goal-04.png",

                OverviewText =
                    "Goal 4 Overview\n" +
                    "Ensure inclusive and equitable quality education and promote\n" +
                    "lifelong learning opportunities for all.\n\n" +
                    "\"Accelerating progress towards achieving Goal 4 must be\n" +
                    "prioritized, as it would have a catalytic effect on the\n" +
                    "realization of the 2030 Agenda overall.\"\n\n" +
                    "Key targets: free, equitable primary and secondary education\n" +
                    "(4.1); skills for employment and entrepreneurship (4.4);\n" +
                    "eliminating gender disparities (4.5); universal literacy and\n" +
                    "numeracy (4.6); education for sustainable development (4.7).",

                DataVizText = "SDG 4 progress data\n2025 SDG Progress Report - Goal 4 extended report",
                DataVizUrl = "https://unstats.un.org/sdgs/report/2025/extended-report/Extended-Report-2025_Goal-4.pdf",

                VideoText = "Story kiosk\nSDG learning resources:\n\"SDG Learners today, SDG Leaders tomorrow!\"",
                VideoUrl = "https://www.unsdglearn.org/",

                InteractiveText =
                    "How libraries support Goal 4\n" +
                    "\"The public library, the local gateway to knowledge, provides\n" +
                    "a basic condition for lifelong learning, independent\n" +
                    "decision-making and cultural development.\"\n- IFLA-UNESCO Public Library Manifesto",
                InteractiveUrl = "https://sdgs.un.org/goals/goal4",

                CallToActionOptions = new[]
                {
                    "Start a family literacy program",
                    "Offer digital & media literacy workshops",
                    "Host lifelong-learning courses for adults",
                    "Partner with local schools on reading habits"
                },

                DocentName = "MINERVA",
                DocentGreeting =
                    "Hello! I am MINERVA, named after the Roman goddess of wisdom.\n" +
                    "Ask me about SDG 4, quality education, and how libraries\n" +
                    "support lifelong learning.",
                DocentInstructions =
                    "You are a helpful assistant trying to help users with simple " +
                    "questions about UN SDGs, the 4th UN SDG, and related case studies. " +
                    "Personality: helpful, friendly, knowledgeable. Restricted to uploaded knowledge.",

                References = new[]
                {
                    "United Nations. (n.d.). Goal 4 | Department of Economic and Social Affairs. https://sdgs.un.org/goals/goal4",
                    "United Nations Statistics Division. (2025). Extended report 2025: Goal 4. https://unstats.un.org/sdgs/report/2025/extended-report/Extended-Report-2025_Goal-4.pdf",
                    "IFLA/UNESCO. (2022). IFLA-UNESCO Public Library Manifesto 2022 and the UN SDGs.",
                    "UN SDG:Learn. (n.d.). https://www.unsdglearn.org/"
                }
            },

            new SdgStationContent
            {
                StationId = "SDG11_SustainableCities",
                Title = "SDG 11 - Sustainable Cities and Communities",
                ThemeColorHex = "#FD9D24",
                IconFileName = "E-WEB-Goal-11.png",

                OverviewText =
                    "Goal 11 Overview\n" +
                    "Make cities and human settlements inclusive, safe, resilient\n" +
                    "and sustainable.\n\n" +
                    "Target 11.7: by 2030, provide universal access to safe,\n" +
                    "inclusive and accessible, green and public spaces, in\n" +
                    "particular for women and children, older persons and\n" +
                    "persons with disabilities.",

                DataVizText = "City progress reporting\nStuttgart - a Livable City (voluntary local review)",
                DataVizUrl = "https://sdgs.un.org/sites/default/files/vlrs/2024-04/stuttgart-a_livable_city_1.pdf",

                VideoText = "Case study: Helsinki Central Library Oodi\n\"Oodi - the library of a new era\"",
                VideoUrl = "https://www.youtube.com/watch?v=f3MPmnpl1lI",

                InteractiveText =
                    "Oodi, Helsinki (Finland)\n" +
                    "A public, open-to-everyone, safe and free city space at the\n" +
                    "heart of the city. Designed with input from residents; nearly\n" +
                    "a zero-energy building (nZEB). Explore the 360 tour.",
                InteractiveUrl = "https://360.northmanvr.com/F1utgPQ3E0/12117832p&350.77h&73.94t",

                CallToActionOptions = new[]
                {
                    "Co-design library spaces with residents",
                    "Open green, accessible public space",
                    "Audit your building's energy efficiency",
                    "Host civic events and community organizing"
                },

                DocentName = "HINA",
                DocentGreeting =
                    "Welcome! I am HINA, named after the Polynesian deity associated\n" +
                    "with creation and resilience. Ask me about sustainable cities,\n" +
                    "infrastructure, and the libraries that anchor them.",
                DocentInstructions =
                    "You are a helpful assistant trying to help users with simple " +
                    "questions about UN SDGs, the 11th UN SDG, and related case studies. " +
                    "Personality: helpful, friendly, knowledgeable. Restricted to uploaded knowledge.",

                References = new[]
                {
                    "United Nations. (n.d.). Goal 11 | Department of Economic and Social Affairs. https://sdgs.un.org/goals/goal11",
                    "ALA Architects. (n.d.). Helsinki Central Library Oodi. https://ala.fi/work/helsinki-central-library/",
                    "Oodi. (n.d.). What is Oodi? https://oodihelsinki.fi/en/what-is-oodi/",
                    "City of Stuttgart. (2024). Stuttgart - a Livable City. https://sdgs.un.org/sites/default/files/vlrs/2024-04/stuttgart-a_livable_city_1.pdf"
                }
            },

            new SdgStationContent
            {
                StationId = "SDG13_ClimateAction",
                Title = "SDG 13 - Climate Action",
                // Student team's prototype room palette: #3d7a42 (light), #1f3f22 (dark).
                ThemeColorHex = "#3D7A42",
                IconFileName = "E-WEB-Goal-13.png",

                OverviewText =
                    "Climate Action SDG Overview\n" +
                    "Take Urgent Action To Tackle Climate Change and its Impacts\n\n" +
                    "Climate change is an undeniable threat to our civilization.\n" +
                    "Through education, innovation and adherence to our climate\n" +
                    "commitments, we can make changes to protect the planet.\n" +
                    "These changes also provide huge opportunities to modernize\n" +
                    "our infrastructure, which will create new jobs and promote\n" +
                    "greater prosperity across the globe.",

                DataVizText = "SDG 13 progress data\n2025 SDG Progress Report - Goal 13",
                DataVizUrl = "https://unstats.un.org/sdgs/report/2025/Goal-13/",

                VideoText = "Why it matters\nWhy taking action to fight climate change matters (UN)",
                VideoUrl = "https://www.youtube.com/watch?v=HXdtxvC00vo",

                InteractiveText =
                    "Case study: Thammasat University Library, Bangkok\n" +
                    "\"From Waste to Wealth: Green Library through Circular Economy\"\n" +
                    "1st place, IFLA Green Library Project Award 2025. Modular\n" +
                    "programs around consumption, recycling/reuse and manufacturing.",
                InteractiveUrl = "https://www.youtube.com/watch?v=x2-hCx2bdMQ",

                CallToActionOptions = new[]
                {
                    "Build a seed library for your community",
                    "Run a climate action program (ALA guide)",
                    "Start a community garden or tool library",
                    "Use the Climate Action Toolkit (West Vancouver)"
                },

                DocentName = "BHUMI",
                DocentGreeting =
                    "Greetings! I am BHUMI, named after the Hindu goddess\n" +
                    "personifying Mother Earth. Ask me about climate action and\n" +
                    "the green libraries leading the way.",
                DocentInstructions =
                    "You are a helpful assistant trying to help users with simple " +
                    "questions about UN SDGs, the 13th UN SDG, and related case studies. " +
                    "Personality: helpful, friendly, knowledgeable. Voice: Shimmer. " +
                    "Restricted to uploaded knowledge.",

                References = new[]
                {
                    "The Global Goals. (n.d.). Goal 13: Climate action. https://globalgoals.org/goals/13-climate-action/",
                    "United Nations. (2019). Why taking action to fight climate change matters - SDG 13 [Video]. https://www.youtube.com/watch?v=oSqmCNNV2dQ",
                    "United Nations Statistics Division. (2025). Goal 13: Climate action. In The Sustainable Development Goals Report 2025. https://unstats.un.org/sdgs/report/2025/Goal-13/",
                    "American Library Association. (2020). Resilient communities: A programming guide for libraries. https://www.ala.org/sites/default/files/tools/content/ResComm_ProgGuide%20FINAL100820.pdf",
                    "West Vancouver Memorial Library. (2022). Climate future action toolkit. https://westvanlibrary.ca/wp-content/uploads/2022/04/cf-tool-kit-interactive-website-interactive.pdf",
                    "Seed Library Census & Map. (n.d.). https://seedlibraries.weebly.com/map.html",
                    "ENSULIB. (2025). IFLA Green Library Award Ceremony: Thammasat University Library [Video]. https://www.youtube.com/watch?v=x2-hCx2bdMQ"
                }
            }
        };
    }
}
