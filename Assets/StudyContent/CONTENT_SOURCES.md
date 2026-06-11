# Study Content — Sources & Attribution

Station copy, case studies, docent personas, links, and references in
`Assets/Scripts/Content/SdgContentLibrary.cs` were ported from the SJSU
LTI Lab UN SDG student team's shared Drive folder
("Project 1 - UN SDGs"): `Project1_Resources_StudentNotes`, the
`Asset Tracker` sheet, the `PROTOTYPE_SDG-13 Reference List`, and the
FrameVR demo room text.

## Docent personas (from the FrameVR chat agents)

| Persona | SDGs | Naming |
|---|---|---|
| MINERVA | 4 (also 2, 3, 7) | Roman goddess of wisdom |
| HINA | 11 (also 6, 7, 9) | Polynesian deity of creation and resilience |
| BHUMI | 13 (also 14, 15) | Hindu goddess personifying Mother Earth |

Persona instructions in the content library mirror the FrameVR agent
settings (helpful/friendly/knowledgeable, restricted to uploaded
knowledge) so the Unity docent stays consistent with the FrameVR hub.

## Media assets (`NSF Grant > Download SDG Media Assets`)

Downloaded into `Assets/StudyContent/Textures/` from the team's shared
Drive Assets Library:

- **E-WEB-Goal-01..17.png** — official UN SDG goal icons. Usage per the
  [UN SDG communications materials guidelines](https://www.un.org/sustainabledevelopment/news/communications-material/)
  (non-commercial, informational use; do not alter the icons).
- **SDG13_HurricaneDorian_UN730286.jpg** — "Hurricane Dorian... Abaco
  Island." UN Photo / OCHA / Mark Garten, https://dam.media.un.org/Package/2AM9LOT_4
- **SDG13_Forest_UN7860718.jpg** — "Trees in a forest in New York State
  after a rain storm." UN Photo / Mark Garten, https://dam.media.un.org/Package/2AM9LOT_4
- **SDG13_ProgressCard_2025.png** — UN 2025 SDG Report Goal 13 social
  media card, https://unstats.un.org/sdgs/report/2025/

Downloaded media is git-ignored by default (`Assets/StudyContent/Textures/`
in `.gitignore`); remove that rule to commit the files once licensing for
redistribution is confirmed.

## Per-station citations

Each station has an in-world References board; the full citation lists
live in `SdgContentLibrary` (`References` arrays) and follow the
student team's reference documents.
