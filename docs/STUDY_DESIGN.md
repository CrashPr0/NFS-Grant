# UN SDG Discovery Hall — Study Design ↔ Implementation Map

This document maps the research plan (Dr. Chow's proposal + the VERA
kickoff meeting with Greg Welch's team) onto the Unity implementation, so
the team (Chris Velez, undergraduate assistant, Erica, Michelle) and the
VERA developers (Corey, Ali) can see where each requirement lives.

## Study at a glance

- **Context:** VR resource center for the ALA IRC UN Subcommittee; research
  on information-seeking/attention in immersive environments. First project
  funded under the NSF grant; VERA provides infrastructure.
- **Prototype scope:** 3 stations — SDG 4 (Quality Education),
  SDG 11 (Sustainable Cities), SDG 13 (Climate Action). SDG 13 (and possibly
  SDG 4) is also the focus of the July 15 UN demonstration.
- **Samples:** ~30 in-lab headset participants (LTI Lab, loaned headsets) +
  up to ~150 self-paced web participants (same Unity build, WebGL).
- **Venue target:** iConference 2027 (New Zealand, ~April), submission
  deadline expected ~Sept 15, 2026 — trials in Fall 2026.

## Conditions

| Condition | Description | Implementation |
|---|---|---|
| A — Passive | Static text/images/video only | `StudyConditionManager.Passive`: `InteractableObject` logs attempted clicks but suppresses content responses; docent disabled |
| B — Interactive | Manipulable data/objects/simulations | `StudyConditionManager.Interactive`: interactables active, docent disabled |
| C — Guided | AI/virtual docent suggests pathways | `StudyConditionManager.Guided`: `DocentGuide` routes the participant station-to-station via a beacon (replace with avatar + dialogue later) |

Set the condition on the `StudyConditionManager` component before the
session, or programmatically (URL parameter / VERA assignment).

## Measures (from the meeting) and where they come from

| Measure | Source |
|---|---|
| Gaze duration per object | `AttentionTarget` dwell stats + per-frame `gaze_*.csv` |
| First fixation / what users look at first | `time_to_first_look` per target; fixation IDs in `gaze_*.csv` |
| Camera position & orientation (continuous) | `head_pos_*` / `head_rot_*` columns in `gaze_*.csv` (both rigs) |
| Look-at relationships / attention to agents & objects | `hit_target` column (gaze raycast), incl. docent placeholders |
| Clicks with 2D screen coords + 3D world position (laptop) | `DesktopInteractor` → `events_*.csv` (`click`, `object_activated`) |
| Key presses (laptop) | `DesktopInteractor` → `key_press` events |
| Object selection in VR | `VRInteractor` (gaze + trigger) → `object_activated` events |
| Interactivity rates per object | `InteractableObject.ActivationCount` (summary CSV) |
| Time spent per SDG station | `SdgStation` (`station_enter`/`station_exit` events + summary) |
| Navigation path | Ordered `station_enter` events + continuous head positions |
| Use of help/avatar guidance | `docent_suggest` / `docent_target_reached` events |
| Pre/post knowledge questions | `QuizDefinition` + `QuizRunner` (`quiz_response` events) — or VERA's survey tools |
| Low-rate screenshots (video too heavy) | `ScreenshotCapture` (off by default; 10 s interval, downscaled) |
| Think-aloud recordings | Out of scope in-app — record via Zoom/room mic per protocol |
| Biometrics (HRV, pupil dilation) | Not available on Quest hardware via public APIs; revisit with VERA team |

## Platform split (one build, two audiences)

`PlatformRigSwitcher` activates the **OVRCameraRig** when a headset is
present, otherwise the **DesktopPlayer** (WASD + hold-right-mouse look,
left-click to select). `PlatformDetector.PlatformTag` stamps every data file
with `headset` or `desktop` so the groups separate cleanly in analysis.

- Headset: eye gaze (Quest Pro) or head gaze (Quest 3) + controller-trigger selection.
- Desktop/WebGL: camera-forward "head gaze" proxy + full pointer/keyboard stream.
- Web data return: `RemoteDataUploader` POSTs the session CSVs to a
  configurable endpoint at session end (or hand off to VERA ingestion).

> **Eye-tracking due diligence (Corey's action item):** despite what was
> said in the meeting, the **Quest 3 does not have eye-tracking hardware** —
> consumer Quest devices with eye tracking are the **Quest Pro**
> (discontinued but available) . Alternatives with eye tracking include
> Vive Focus Vision and Pico 4 Enterprise/Pro. This project automatically
> falls back to head gaze on devices without eye tracking, and records
> `gaze_source` per sample either way.

## VERA integration (Corey / Ali)

`VeraBridge` (Assets/Scripts/Vera) is the single integration seam: it
re-publishes session start/end and the event stream as C# events. When the
VERA Unity plugin is available, subscribe it there and map VERA's
participant/condition assignment onto `SessionController.ParticipantId` and
`StudyConditionManager.Condition`. Survey/questionnaire collection can go
through VERA's portal tools; the local `QuizRunner` is the self-contained
fallback.

## Data files (per session)

| File | Contents |
|---|---|
| `gaze_<id>_<utc>.csv` | Continuous per-frame samples: head pose, gaze ray, source, confidence, fixations, gaze hits |
| `events_<id>_<utc>.csv` | Discrete events: clicks (screen + world coords), key presses, station enter/exit, docent guidance, quiz responses, session lifecycle |
| `summary_<id>_<utc>.csv` | Per-target dwell/looks (with format + station), per-station time/visits, per-object activation counts |
| `screenshots/*.png` | Optional low-rate stills (off by default) |

## Station content

Station copy is no longer placeholder: `SdgContentLibrary`
(Assets/Scripts/Content) carries the student team's curated material from
the Drive folder — overview texts (including the FrameVR demo-room SDG 13
copy), the Oodi/Thammasat/seed-library case studies, resource links
(2025 progress reports, toolkits, videos, 360 tour), call-to-action
options, per-station citation boards, and the FrameVR docent personas
(MINERVA / HINA / BHUMI). The 8-question knowledge quiz is generated into
`Assets/StudyContent/SdgKnowledgeQuiz.asset` by the scene builder.
The official goal icons and SDG 13 photos are committed in
`Assets/StudyContent/Textures/` (attribution in
`Assets/StudyContent/CONTENT_SOURCES.md`); content was verified against
the live Drive folder (Asset Tracker, Hand Off Report, SDG-13 reference
list) on 2026-06-12. **NSF Grant > Download SDG Media Assets** re-fetches
the media from Drive if it is ever updated.

## Session flow & assignment (StudyIntake)

One flow serves both samples: intake → pre-quiz → exploration →
(F10) → post-quiz → done. Assignment priority:

1. **URL parameters** (web/desktop): `?pid=P123&cond=guided`
   (`cond` accepts `passive|interactive|guided` or `a|b|c`) — encode
   assignment directly in recruitment/VERA links.
2. **On-screen intake panel** (desktop lab sessions without URL params).
3. **VR**: starts immediately with the Inspector participant ID;
   quizzes are skipped until a world-space quiz UI exists (or are
   administered outside the headset / via VERA).

Counterbalancing keys off the participant ID either way.

## Suggested next steps for the team

1. **Storyboard → scene:** run `NSF Grant > Build Discovery Hall Scene`,
   then replace placeholder primitives with real content (keep the attached
   `AttentionTarget`/`InteractableObject`/`SdgStation` components and IDs).
2. **SDG 13 + SDG 4 content first** (UN demo July 15; video demo fallback).
3. **WebGL build** for the self-paced sample; stand up the upload endpoint
   (or wait for VERA ingestion) and test end-to-end data return.
4. **Quiz content:** create pre/post `QuizDefinition` assets per SDG.
5. **Docent:** replace the beacon with an avatar + dialogue (the FrameVR
   chat-agent personas from the student notes are a good starting brief).
6. **Pilot:** run 2–3 lab pilots, inspect the CSVs, and lock the schema
   before Fall 2026 trials (iConference deadline ~Sept 15, 2026).
