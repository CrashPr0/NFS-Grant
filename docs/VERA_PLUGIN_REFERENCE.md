# VERA Unity Plugin — Reference (condensed from official docs, 2026-07)

Source: https://vera-xr.io/static/docs/ (Sandbox Demo + Core Functionality).
This is the authoritative API. It supersedes guesses in earlier scaffolding.

## Install & authenticate

- **Unity version:** the VERA Sandbox demo targets **Unity 6000.3.9f1**
  with **Web Build Support**. (Our project is currently 2022.3 LTS — see
  "Impact on this project" below; version compatibility must be confirmed.)
- **Plugin package (UPM git URL):**
  `Window > Package Manager > + > Add package from Git URL`:
  ```
  https://github.com/ucf-research/vera-package.git
  ```
- **Sandbox sample project** (reference implementation):
  `git clone https://github.com/CoreyClements1/VERASandbox.git`
  — scene `Assets/Scenes/SandboxScene.unity`,
  example wiring in `Assets/Scripts/ExperimentManager.cs`
  (search `VERA SANDBOX NOTE` comments).
- **Authentication is interactive, in-editor.** Menu bar `VERA > Settings`
  > **Authenticate** → opens the VERA web portal to log in and authorize
  the Unity project. There is **no API-key / credentials file**; the
  handshake is OAuth-style through the Settings window. An unauthenticated
  session cannot record to the portal.

## VERA Settings window

- **Help Guide** — in-editor quick ref.
- **Your Experiment** — dropdown of experiments you can edit; select the
  active one before recording. Selecting it triggers **C# code generation**
  for that experiment's IVs / file types / surveys.
- **Data Recording** — `local and live` (default/recommended), `local only`,
  or `none`.
- **Debug Preferences** — console log frequency.
- **Build Upload** — "Build and Upload Experiment" runs the whole WebXR
  build + portal upload.

## Auto-generated classes (per selected experiment)

Selecting an experiment generates strictly-typed C# under `Assets/VERA/…`.
Each comes with a **preprocessor directive of the same name** so code
compiles only when that item exists in the active experiment.

### Independent variables (conditions) → `VERAIV_<Name>`
Generated to `Assets/VERA/Conditions/GeneratedCode`.
```csharp
#if VERAIV_WeatherType
    VERAIV_WeatherType.IVValue v = VERAIV_WeatherType.GetSelectedValue();
    VERAIV_WeatherType.SetSelectedValue(VERAIV_WeatherType.IVValue.Sunny);
#endif
```
The current value of every IV is auto-appended as a column on every logged
row, so all data is tagged with the participant's condition.

### File types (logs) → `VERAFile_<Name>`
Generated to `Assets/VERA/FileTypes/GeneratedCode`.
Define file types + columns on the web portal; `CreateCsvEntry()` is typed
to those columns, **in the order defined**.
```csharp
#if VERAFile_ParticipantPositions
    VERAFile_ParticipantPositions.CreateCsvEntry(1.23f, 4.56f, 7.89f);
#endif
```
- VERA auto-adds three columns you must **not** pass: `pID`, `conditions`,
  `ts` (seconds since app start).
- Logs upload to the portal every **3 s**; also written locally to
  `Assets/VERA/data` as backup and synced on reconnect. No manual submit.
- File types need not be CSV (txt/png/etc.), but CSV gets the typed helper.

### Questionnaires → `VERASurveyHelper`
```csharp
using VERA;

VERASurveyHelper.StartSurvey(VERASurveyHelper.VERASurveyReference.S_MySurvey);

VERASurveyHelper.StartSurvey(
    VERASurveyHelper.VERASurveyReference.S_MySurvey,
    onSurveyComplete: () => { /* continue experiment */ });

// In-VR (immersive world-space UI) vs. on the participant's web browser:
VERASurveyHelper.StartSurvey(VERASurveyHelper.VERASurveyReference.S_MySurvey, inVR: true);
VERASurveyHelper.StartSurvey(VERASurveyHelper.VERASurveyReference.S_MySurvey, inVR: false);
```
Other optional params seen in the docs: `transportToLobby`, `dimEnvironment`,
`heightOffset`, `distanceOffset` (position the world-space UI relative to
the head). One doc page names the VR/web switch `runInWeb` (inverse sense,
default false = VR) and another names it `inVR` (default unspecified) —
**verify the actual parameter name against the generated
`VERASurveyHelper` code once authenticated** rather than trusting either
snippet blindly. VERA handles display, navigation, and response recording
either way.

#### Questionnaire phases (set per-survey on the portal, not in code)
- **Pre-experiment** — auto-administered in the participant's **web
  browser before** they enter VR. No Unity call needed.
- **Post-experiment** — auto-administered in the **web browser after**
  the VR portion ends. No Unity call needed.
- **Mid-experiment** — **not** automatic. You must call
  `VERASurveyHelper.StartSurvey(...)` yourself at the right point in the
  VR session, choosing `inVR`/`runInWeb` for how it displays.

**Fit for this project:** the existing pre/post knowledge quiz
(`QuizRunner`) is a pre- and post-experiment questionnaire pair — a direct
port to VERA's phase system, freeing `QuizRunner` entirely. A
cybersickness/comfort questionnaire (relevant to the comfort-vignette
work) fits well as **either** a post-experiment survey or a mid-experiment
one triggered right after the first sustained stick-walk, run **in VR**
so it doesn't break presence.

### Sessions → `VERASessionManager` (static)
```csharp
VERASessionManager.onInitialized.AddListener(OnReady);
int pid = VERASessionManager.participantID;   // portal-assigned int pID
bool ready = VERASessionManager.initialized;
bool live  = VERASessionManager.collecting;
VERASessionManager.FinalizeSession();          // MUST call once at the end
```
`FinalizeSession()` stops collection, flushes to portal, closes the WebXR
session, and marks the participant **Completed**. Skipping it → session
marked **Incomplete**.

Session statuses: `Created` → `In Progress` → `Completed` /
`Incomplete` (premature exit / lost connection) / `Withdrawn`.

## Web portal: experiment lifecycle

- **Draft Mode** — editable; only **pilot** data (one free pilot slot that
  resets each test session). Use for setup/validation.
- **Collection Mode** — **locked** (no edits); records real participant data.
  Freely switch back to Draft to edit.
- **Activation states** (within Collection): `Inactive` (ready, blocking new
  joins) → `Active` (recruiting + collecting) → `Paused` (blocks new joins,
  keeps cycle). In-progress sessions always finish. Activate on the
  Experiment Results page; Pause/Deactivate on the Edit Experiment page.

## Portal setup: recommended CSV file types & columns

The portal's "CSV Column Metadata Configuration" always pre-fills three
columns you can't remove — `pID` (Integer), `conditions` (String, JSON of
active IVs), `ts` (Float, seconds since app start) — then lets you
`+ Add a column` for the rest. Below are the columns to add, in order,
mirroring our two existing CSV schemas 1:1 so nothing gets lost (see the
gaze schema in `AttentionDataLogger.cs` and the events schema in
`StudyEventLogger.cs`). Portal data types are limited to roughly
Integer / Float / String (confirm Boolean is available; if not, encode
booleans as Integer 0/1, as our own CSVs already do).

**Caveat on `timestamp_utc_ms`:** it's a 13-digit Unix millisecond epoch
(~1.78 × 10¹²), which overflows a 32-bit `Integer` column. Use `Float`
(and confirm it's double-precision on the portal, not `float32`, which
starts losing whole milliseconds above ~2²⁴ ≈ 16.7M) — or, safer, add it
as a `String` and cast on export. `ts` (auto-provided) already covers
relative timing for most analyses; keep `timestamp_utc_ms` only for
cross-device / cross-session wall-clock alignment.

### File type: `GazeSamples` (extension `csv`)
One row per rendered frame — the `gaze_*.csv` replacement.

| Order | Column | Type | Notes |
|---|---|---|---|
| — | `pID` | Integer | auto |
| — | `conditions` | String | auto |
| — | `ts` | Float | auto |
| 1 | `timestamp_utc_ms` | Float or String | see caveat above |
| 2 | `frame` | Integer | `Time.frameCount` |
| 3–5 | `head_pos_x/y/z` | Float | world space, meters |
| 6–9 | `head_rot_x/y/z/w` | Float | quaternion, world space |
| 10–12 | `gaze_origin_x/y/z` | Float | world space |
| 13–15 | `gaze_dir_x/y/z` | Float | normalized, world space |
| 16 | `gaze_source` | String | `EyeTracking` / `HeadGaze` |
| 17 | `gaze_confidence` | Float | 1.0 for head gaze |
| 18 | `angular_velocity_deg_s` | Float | I-VT classifier input |
| 19 | `is_fixating` | Integer | 0/1 |
| 20 | `fixation_id` | Integer | monotonically increasing |
| 21 | `hit_target` | String | `AttentionTarget` id, empty if none |
| 22–24 | `hit_point_x/y/z` | Float | empty if no hit |
| 25 | `hit_distance` | Float | empty if no hit |

### File type: `StudyEvents` (extension `csv`)
One row per discrete event — the `events_*.csv` replacement.

| Order | Column | Type | Notes |
|---|---|---|---|
| — | `pID` / `conditions` / `ts` | — | auto |
| 1 | `timestamp_utc_ms` | Float or String | see caveat above |
| 2 | `platform` | String | `VR` / `Desktop` / `WebGL` — not an IV, so not covered by `conditions` |
| 3 | `event_type` | String | click, key_press, station_enter, station_exit, teleport, docent_guidance, quiz_response, session_start, session_end, … |
| 4 | `target_id` | String | AttentionTarget / station / object id |
| 5 | `detail` | String | free-text payload |
| 6–8 | `world_x/y/z` | Float | empty when not applicable |
| 9–10 | `screen_x/y` | Float | desktop/WebGL clicks only; empty in VR |

Note: `session_time_s` and `condition` from the original `events_*.csv`
header are dropped here because VERA's auto `ts` and `conditions` already
cover them — don't duplicate.

### File type: `Experiment_Telemetry` (auto-created by VERA, read-only)

Discovered on the portal 2026-07-14 — this file type is **created
automatically for every experiment and cannot be edited or removed**. No
action needed to define it; documenting its columns here so nobody
duplicates them.

Logged automatically every frame: `headsetDetected` (Boolean),
`headsetPosX/Y/Z` (Float), `headsetRot` (Transform: yaw/pitch/roll), then
the same 8-field set (`Detected`/`PosX/Y/Z`/`Rot`) plus
`Trigger`/`Grip`/`PrimaryButton`/`SecondaryButton`/`Primary2DAxisClick`/
`ThumbstickX/Y` for **both** `left` and `right` controllers — 29 custom
columns after the auto `pID`/`conditions`/`ts`.

**Overlap with `GazeSamples`:** `head_pos_x/y/z` and `head_rot_x/y/z/w` in
our `GazeSamples` file type duplicate `headsetPosX/Y/Z`/`headsetRot` here.
This is intentional redundancy, not a bug — `GazeSamples` keeps head pose
alongside the gaze ray so a single row has everything needed for gaze
analysis without joining two files; `Experiment_Telemetry` is the
lower-level per-frame input/pose stream VERA provides regardless. No
change needed to `tools/vera/columns/gaze_samples.json`.

### File type: `Summary` (extension `csv`, optional)
The per-session rollup (`summary_*.csv`) is the one file that's naturally
computed **once at session end**, not streamed. Either keep it purely
local (simplest — it's a derived/redundant view of `GazeSamples` +
`StudyEvents`, reconstructable from them on the portal side), or define
it on the portal with one row per session summarizing per-AOI dwell time,
look count, and time-to-first-look. Recommend **skip defining this on
VERA** initially — derive it from the two streams above during analysis,
and revisit only if the research team wants it live on the dashboard.

### Independent variables to define
- `Condition` — String/enum, 3 levels: `Passive`, `Interactive`, `Guided`
  (mirrors `StudyConditionManager`).
- Optionally, once `CounterbalanceManager` output should be visible on the
  portal: additional IVs for zone-position assignment / spawn heading —
  design these after Part A (counterbalancing) is finalized, not now.

## Impact on this project (action items)

1. **Unity version — RESOLVED 2026-07-14: migrating to Unity 6000.3.9f1.**
   The package.json at ucf-research/vera-package declares `"unity": "6000.0"`
   (hard minimum), so 2022.3 was never an option. ProjectVersion.txt and
   Packages/manifest.json are updated (URP 17.x, test-framework 1.4.x,
   timeline 1.8.x, ugui 2.0.0 with TMP merged in, VERA git package added);
   the first open in the Unity 6 editor runs the asset/API migration.
   Meta XR SDK v71 supports Unity 6. Expect deprecation warnings from
   `FindObjectOfType` (17 uses, 9 files) — warnings only, not errors.
2. **Auth model differs from the scaffold.** The real plugin authenticates
   interactively via the VERA Settings window, not a `vera_credentials.json`
   API key. `VeraConfig.cs` (key loader) is **not** how the real plugin
   connects — keep it only if we still POST to our own Apps Script endpoint;
   otherwise it's dead. Revisit `VeraPluginAdapter` to call the real
   `VERAFile_* / VERASessionManager` APIs instead of forwarding generic
   events.
3. **Logging model differs.** VERA wants **file types defined on the portal**
   → typed `CreateCsvEntry(...)`. Our free-form `AttentionDataLogger` /
   `StudyEventLogger` rows don't map 1:1. Decision needed: define VERA file
   types mirroring our `gaze_*` / `events_*` / `summary_*` schemas and emit
   to both, or treat VERA as the primary sink on the WebXR path only.
4. **Conditions map cleanly.** Our Passive/Interactive/Guided becomes a
   single VERA IV (e.g. `VERAIV_Condition` with 3 levels) → auto-tagged on
   every row. `CounterbalanceManager` output could become additional IVs.
5. **Surveys/quiz overlap.** VERA's questionnaire system could replace
   `QuizRunner` for the pre/post knowledge quiz and add a cybersickness
   survey (relevant to our comfort-vignette work) — its responses land in
   the portal automatically.
6. **Interactive auth is a human-in-Unity step.** A headless Claude session
   can't click "Authenticate"; the portal domains are also network-blocked
   here (see `docs/VERA_SETUP.md`). Plugin install + auth + experiment
   definition are done by a person in the editor.
