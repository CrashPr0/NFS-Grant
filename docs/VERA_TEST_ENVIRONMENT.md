# VERA test environment (headset + immersive web)

Goal: run the Discovery Hall against the **VERA portal experiment "Discovery
Hall" in Draft mode**, on both the Quest (native app) and immersive web (a
VERA participant link, desktop browser and Quest browser), with data landing
in VERA *and* in the Drive backup.

## What the code already does

- **Data to VERA** (`Assets/Scripts/Vera/VeraPluginAdapter.cs`): every gaze
  sample goes to the portal file type `Gaze_Samples` and every study event to
  `StudyEvents`, in the portal's column order (`tools/vera/columns/*.json`);
  VERA adds `pID`, `conditions`, `ts`. At the end of the session it calls
  `VERASessionManager.FinalizeSession()` so the participant is marked
  **Completed** (in a browser this also closes the WebXR session).
- **Data to Drive** is unchanged (the backup; see `GOOGLE_DRIVE_SETUP.md`).
- **Participant + condition from VERA** (`StudyIntake`, `VeraSession`): in a
  build made from a VERA-signed-in editor, the app waits up to 20 s for
  VERA's session ("Connecting to the study server...", also in the headset),
  then starts with VERA's participant ID and its `Condition` level.
  - VERA-hosted browser sessions skip the in-app pre/post knowledge quiz -
    VERA runs it as the portal's Pre-VR / Post-VR questionnaires. The value
    ranking still runs in-app.
  - The native Quest app keeps the in-app quiz (in the headset).
  - No VERA session after 20 s (e.g. the build is opened outside VERA): it
    carries on without VERA - Inspector ID in the headset, `?pid=` or the
    intake panel in a browser. An explicit `?pid=` skips the wait entirely.
- **Everything compiles out** until VERA's code generation has run (per-item
  defines `VERAFile_Gaze_Samples`, `VERAFile_StudyEvents`, `VERAIV_Condition`),
  so a checkout that never signed in builds and behaves exactly as before.

## Steps only a person can do

1. **Sign in (Unity editor, this Mac):** `VERA > Settings` -> Authenticate,
   then select the experiment **Discovery Hall**. Code generation adds the
   defines above and the adapter goes live. Never commit
   `Assets/VERA/Authentication/**/*.json` (gitignored; it holds the tokens).
2. **Portal:** configure the participant distribution method (the open item
   in `VERA_PORTAL_PROGRESS.md`) - needed for a participant link.
3. **Portal (recommended):** change `timestamp_utc_ms` in both file types
   from Float to String. A 32-bit float can't hold epoch milliseconds (about
   2 minutes of resolution today); VERA's own `ts` is precise either way.
4. **Immersive web:** `VERA > Settings > Build and Upload Experiment` (VERA
   builds and hosts it; first build can take 20+ min). Open the participant
   link in a desktop browser, then in the Quest browser and press VR.
5. **Headset (native, eye tracking):** with the editor signed in but closed,
   `./scripts/unity-tasks.sh build-quest`, sideload the APK, run a session.

## Checking a test run

- Logs (Unity console / Quest remote debugging):
  `[StudyIntake] VERA session: pID ...` and
  `[VeraPluginAdapter] Session start (...); VERA recording to 'Discovery Hall' as VERA pID ...`,
  then `...finalizing VERA session.` at the end.
- Portal (Draft): the session appears **Completed**; `Gaze_Samples`,
  `StudyEvents` and `Experiment_Telemetry` have rows; the pre/post
  questionnaires recorded answers (web).
- Drive: the same participant's `gaze_`/`events_`/`summary_` CSVs arrived.
- Draft mode has a single pilot slot that resets per test session.

## Known gaps to watch in the first run

- VERA's build pipeline uses its own settings for the hosted WebXR build;
  confirm the headset performance script (`NSFGrantXRPerf.jspre`,
  `[XRPerf]` logs) and the WebXR template still apply there.
- After signing in on a machine, *its* GitHub Pages builds are "connected"
  too: without `?pid=` they show "Connecting to the study server..." for
  20 s before the intake panel. Share Pages links with `?pid=`.
- VERA sends a row per frame (like its own telemetry); watch the headset
  frame rate in `[XRPerf]` during the first pilot.
