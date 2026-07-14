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
VERASurveyHelper.StartSurvey(
    VERASurveyHelper.VERASurveyReference.S_CybersicknessQuestionnaire,
    onSurveyComplete: () => { /* continue experiment */ });
```
Optional params: `runInWeb` (browser vs immersive world-space UI; default
false = VR), `transportToLobby`, `dimEnvironment`, `heightOffset`,
`distanceOffset`. VERA handles display, navigation, and response recording.

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

## Impact on this project (action items)

1. **Unity version conflict.** Sandbox targets Unity 6000.3.9f1; we're on
   2022.3 LTS. Confirm the `vera-package` supports 2022.3, or plan an
   engine upgrade (large — touches Meta XR SDK, all builds). **Blocker to
   resolve before wiring.**
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
