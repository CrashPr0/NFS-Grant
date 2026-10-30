# VERA Setup — Connecting the Discovery Hall to the VERA Portal

VERA (Virtual Experience Research Accelerator, UCF/SREAL) launched its
early-access platform: a web portal at **https://vera-xr.io** plus a Unity
plugin. Together they handle experiment definition, participant
recruitment/assignment, WebXR deployment, and remote data collection.
This document covers what is already wired in this project and the exact
steps to finish the hookup once you have portal access. For the current
live status of portal-side configuration (which columns/IVs/surveys are
actually saved), see `docs/VERA_PORTAL_PROGRESS.md` — keep that file
updated as you go, since portal state isn't visible from the repo.

## What is already in place (no portal needed)

| Piece | File | Status |
|---|---|---|
| Lifecycle + event seam | `Assets/Scripts/Vera/VeraBridge.cs` | Done — session start/end and **every discrete event** (clicks, stations, quiz, teleports) are re-published as C# events |
| Credentials loader | `Assets/Scripts/Vera/VeraConfig.cs` | Done — reads `vera_credentials.json` from the study data folder; never committed (gitignored) |
| Plugin wiring point | `Assets/Scripts/Vera/VeraPluginAdapter.cs` | Done — single seam with `#if VERA_PLUGIN_PRESENT` blocks; logs the would-be traffic until the plugin is installed |
| Scene wiring | `Assets/Editor/DiscoveryHallBuilder.cs` | Done — all three components on the `AttentionStudy` object every rebuild |
| WebXR readiness | whole project | The WebGL build already runs the full study; VERA's build tools convert Unity WebGL projects to WebXR for portal deployment |

Until the plugin is installed, the adapter logs what it would send, so the
pipeline is testable today: run a session in the editor and watch for
`[VeraPluginAdapter]` lines in the Console.

## Step 1 — Portal account & experiment (human, ~15 min)

1. Go to **https://vera-xr.io** and register a researcher account
   (institutional email; the team is at UCF/SREAL if approval is manual).
2. Create an experiment for the Discovery Hall study. Map our design onto
   VERA's model:
   - **Conditions:** Passive / Interactive / Guided (matches
     `StudyConditionManager`).
   - **Dependent variables:** per-AOI dwell time, time-to-first-look,
     fixation counts, station visit order/duration, quiz responses —
     i.e., the columns documented in `docs/STUDY_DESIGN.md`.
3. From the experiment's settings page, note the **experiment ID** and
   generate an **API key**.

## Step 2 — Credentials file (never commit this)

Create `vera_credentials.json` in the study data folder (the folder that
`NSF Grant > Analysis > Open Study Data Folder` opens, or
`Application.persistentDataPath` if no custom folder is set):

```json
{
  "portalBaseUrl": "https://vera-xr.io",
  "experimentId": "<from step 1>",
  "apiKey": "<from step 1>"
}
```

The filename is in `.gitignore`. `VeraConfig` logs at startup whether it
found and parsed the file. Do not paste the API key into any Inspector
field or scene — scenes are committed.

## Step 3 — Install the Unity plugin

The plugin is a public UPM package (already added to
`Packages/manifest.json` in this project):

```
https://github.com/ucf-research/vera-package.git#v0.4.3
```

(It is also embedded at `Packages/com.vera.vera`, which takes precedence —
see "VERA package version" below.)

It requires **Unity 6000.0+** — this project targets **6000.3.9f1**
(see `ProjectSettings/ProjectVersion.txt`). Authentication is interactive:
open `VERA > Settings` in the menu bar, click **Authenticate**, log in on
the portal page that opens, then pick the experiment from the
**Your Experiment** dropdown (this generates the typed `VERAIV_*` /
`VERAFile_*` classes under `Assets/VERA/`).

Then add `VERA_PLUGIN_PRESENT` to
`Project Settings > Player > Scripting Define Symbols` for **every build
target** (Android, Web, Standalone) to activate our adapter.

See `docs/VERA_PLUGIN_REFERENCE.md` for the full plugin API.

## Step 4 — Fill in the adapter

Open `Assets/Scripts/Vera/VeraPluginAdapter.cs` and replace the three
`TODO(vera)` blocks with the plugin's real API calls:

- `OnSessionStarted` → initialize/begin the plugin recorder with
  `_config.ExperimentId` / `_config.ApiKey`, the participant ID, and the
  condition.
- `OnEventLogged` → forward each event to the plugin recorder.
- `OnSessionEnded` → flush and close the recorder.

If VERA assigns participants/conditions from the portal (its normal
recruitment flow), map the assignment onto
`SessionController.ParticipantId` and `StudyConditionManager` **before**
`StartSession()` — `StudyIntake` already does exactly this for WebGL URL
parameters, so route the VERA assignment through the same entry point.

## Step 5 — Verify

1. Editor play-mode session: `[VeraConfig] Loaded credentials…` appears,
   no `[VeraPluginAdapter] … plugin not installed` lines (they compile
   out under `VERA_PLUGIN_PRESENT`).
2. Portal dashboard shows the session and live events.
3. Confirm the local CSVs are unchanged — VERA upload is additive; the
   local logging pipeline stays the source of truth for analysis.

## Troubleshooting the Unity 6 first open

**"Cannot connect to 'npm.developer.oculus.com' (ETIMEDOUT)" + VERA
`InputActionProperty` CS0246 errors (seen 2026-07-14).** One root cause:
the manifest pulls `com.meta.xr.sdk.core` from Meta's npm scoped registry,
and when that host is unreachable Unity aborts the whole package
resolution — so VERA's own dependencies (`com.unity.inputsystem`,
`com.unity.xr.interaction.toolkit 3.0.3`, both declared in VERA's
package.json and referenced by its asmdef) never install, and VERA's
telemetry logger fails to compile with `InputActionProperty` not found.
The VERA errors are a symptom, not a separate problem.

Fix, in order:

1. **Rule out local network** — on campus Wi-Fi/VPN, try a personal
   hotspot and click *Retry*. Note Meta has been migrating SDK
   distribution off this npm registry to the Unity Asset Store, so the
   timeout may be permanent no matter the network.
2. **Switch to the Asset Store distribution (Meta's current official
   path):**
   - In a browser: Unity Asset Store → search **"Meta XR Core SDK"**
     (free, publisher Meta) → *Add to My Assets*.
   - In Unity: `Window > Package Manager` → registry dropdown →
     **My Assets** → *Meta XR Core SDK* → Download/Install. This installs
     from a local tarball and rewrites `Packages/manifest.json` itself —
     no npm registry involved.
   - Afterwards, delete the now-unused `scopedRegistries` block (the
     Meta XR entry) from `Packages/manifest.json` — nothing else uses it.
   - The Asset Store serves a newer SDK than the previously pinned
     v71; the APIs this project uses (`OVRCameraRig`, `OVRInput`,
     `OVREyeGaze`) are stable across those versions.
3. **Reopen / Retry.** The rest of the graph (VERA's XRI + Input System
   deps) comes from Unity's own registry and should now resolve; the
   `InputActionProperty` errors disappear without any code change.
4. **Still `InputActionProperty` CS0246 with every package installed
   (seen 2026-09-24)?** Then it's the input backend, not the network.
   VERA's `VERABaselineDataLogger.cs` imports `UnityEngine.InputSystem`
   only under `#if ENABLE_INPUT_SYSTEM`, but uses `InputActionProperty`
   outside that guard. Unity defines `ENABLE_INPUT_SYSTEM` only when
   `Project Settings > Player > Other Settings > Active Input Handling` is
   **Input System Package** or **Both**. Set it to **Both** (our scripts
   use the legacy `Input` class, so not "Input System Package" alone) and
   let the editor restart. `ProjectSettings.asset` isn't committed, so a
   fresh clone starts on "Input Manager (Old)" again;
   `CiTools.SetupProject` now sets Both automatically.

   **VERA is now embedded and patched** at `Packages/com.vera.vera`
   (v0.4.3, copied from the upstream `v0.4.3` tag minus the `Samples~`
   demo; see "VERA package version" below). The three methods taking
   `InputActionProperty`
   (`GetFloatInputState`, `GetInputState`, `GetVector2InputState`) are
   wrapped whole in `#if ENABLE_INPUT_SYSTEM`; they are only called from
   code already under that guard, so it compiles with any input setting.
   Upstream 0.4.3 still guards only the method *bodies*, so the patch is
   still needed. An embedded package overrides the git URL in
   `manifest.json` (now pinned to `#v0.4.3`).
   **To update VERA:** follow "Updating the embedded package" below.
   Worth reporting upstream so the patch can be dropped.
5. **If duplicate-TextMeshPro type errors appear next:** VERA declares
   `com.unity.textmeshpro 3.0.6`, but Unity 6 merged TMP into
   `com.unity.ugui 2.0.0`. If Unity doesn't reconcile this on its own,
   report it — a single manifest override addresses it (do not download
   anything extra for this).

## VERA package version (0.4.3, updated 2026-10-29)

**Embedded version:** upstream tag `v0.4.3` (released 2026-10-23,
commit `5a4ad95`). The previous embed was labelled 0.4.2 but was actually
the unreleased upstream commit `3235047` (2026-09-23).
`Packages/manifest.json` pins the git URL to `#v0.4.3`. So if the embed
is ever deleted, Unity fetches the same version instead of `main`.

**Decision: pin to 0.4.3; don't take the post-0.4.3 commits on `main`.**
On 2026-10-27, upstream commit `b023b66` ("SwapOculusDependency")
replaced VERA's `com.unity.xr.oculus 4.2.0` dependency with
`com.unity.xr.openxr 1.15.1` and bumped `com.unity.inputsystem` to
1.14.2. This project runs Quest through the **Oculus XR plugin**:
`Assets/XR/Loaders/OculusLoader.asset`, plus `com.unity.xr.oculus` and
`com.meta.xr.sdk.core 71` in the manifest. Our gaze pipeline also uses OVR
APIs (`OVRCameraRig`, `OVREyeGaze`). Taking `main` would install OpenXR
next to the Oculus plugin. Unity supports only one active loader per
platform, and switching the gaze/eye-tracking path to OpenXR is a
separate, device-tested migration. It shouldn't ride along with an SDK
bump. The Input System bump alone is harmless: Unity 6.3 already
resolves 1.18.0.
VERA's runtime code doesn't reference OpenXR at all. The swap only
touches `package.json` and the sandbox sample. So nothing in 0.4.3 depends
on Oculus vs OpenXR either way.

What we skip by pinning, all from 2026-10-26 to 2026-10-29: experiment-wide
memory (`VERAExperimentMemory`), success/error callbacks on generated
`VERAFile_*.UploadFile`, a 50 → 500 MB upload limit, and a 30-minute
`HttpClient` timeout for WebXR build uploads. We use none of these APIs.
If WebXR build uploads time out at about 100 s, backport that one-line
timeout from `Editor/VERABuildUploader.cs` on upstream `main`.

**Local patches on top of v0.4.3** (search `NSF-Grant patch`):

1. `Runtime/Scripts/Logger/ExperimentTelemetry/VERABaselineDataLogger.cs`:
   the `InputActionProperty` guard described above. Still needed in
   0.4.3.
2. `Runtime/Scripts/Logger/WebXRQuery.cs.meta`: the v0.4.3 tag shipped
   `WebXRQuery.cs` without a `.meta`. This copies upstream's own fix
   (commit `cf41e83`), so the GUID matches future versions.

`Samples~` (the 404 MB demo), `.gitignore` and `.vscode` are left out of
the embed, as before.

**What changed in 0.4.3 that touches this project:**

- *API we call:* unchanged. `VERASessionManager.initialized`,
  `collecting`, `participantID`, `GetSelectedIVValue`,
  `CreateArbitraryCsvEntry` and `FinalizeSession` keep the same
  signatures. The `VERABuildAuthentication` resource still has
  `authenticated` / `activeExperimentName`. `VeraSession`,
  `VeraPluginAdapter`, `VeraBridge` and `StudyIntake` need no code
  changes.
- *Generated code:* VERA no longer generates a wrapper for
  `Experiment_Telemetry`. Its own `VERABaselineDataLogger` writes that
  file. On the next experiment sync it **deletes**
  `Assets/VERA/Filetypes/GeneratedCode/VERAFile_Experiment_Telemetry.cs`.
  This is expected; commit the deletion. The
  `VERAFile_Experiment_Telemetry` **scripting define** is still set, so
  `VeraSession`'s `#if VERAFile_Experiment_Telemetry` gates keep working.
  Nothing in our code calls the wrapper class.
- *Auth:* the "silent authentication failure" fix (PR #52) is editor-side
  (`VERAAuthenticator`, `VERASettingsWindow`). A stale sign-in now shows
  as signed out instead of silently producing an unconnected build.
- *New, additive:* participant memory keys, survey results callbacks
  (`VERASurveyResults`), and WebXR URL-parameter forwarding
  (`VERASessionManager.GetUrlParameter` / `onUrlParametersReceived`).
  `StudyIntake` still reads `?pid=` / `?cond=` itself. That's unchanged,
  but the VERA accessor is now an option for VERA-hosted links.
- *Surveys:* open-response questions and a spatial keyboard. On first
  editor load, VERA imports XRI's **Spatial Keyboard** sample into
  `Assets/Samples/XR Interaction Toolkit/…`. It also copies
  `XRI Global Keyboard Manager.prefab` into `Assets/VERA/Resources/`.
  Commit both.

**Risks**

- *Upstream is moving to OpenXR.* Future VERA releases will likely
  require it, so pinning delays the Oculus → OpenXR migration rather than
  avoiding it. Plan that migration separately, with Quest Pro eye-tracking
  tests for `OVREyeGaze` under OpenXR.
- *Survey UI changes* (prefab, keyboard, open-response) are untested
  here. Re-run any VERA questionnaire flow (`docs/VERA_QUESTIONNAIRES.md`).
- *WebXR session start* now waits for the hosting page's URL parameters,
  then auto-starts. Check that a VERA participant link still reaches
  `[StudyIntake] VERA session: pID …` within `veraWaitSeconds`.
- The embed is a fork, so every update means re-applying the patches
  above.

**What Chris must do in the Unity editor after merging**

1. Pull, then open in **Unity 6000.3.9f1**. Let packages resolve; the
   Meta npm registry caveats above still apply. In Package Manager, VERA
   should show **0.4.3** (embedded/Custom).
2. Wait for VERA's first-load automation (the XRI sample import and
   keyboard prefab copy), then confirm the Console has **no compile
   errors**.
3. Leave `Project Settings > Player > Active Input Handling` on **Both**.
4. In `Project Settings > XR Plug-in Management`, the **Android** tab
   must still have only **Oculus** checked. **OpenXR** must be absent or
   unchecked.
5. Open `VERA > Settings`. If it shows signed out, click
   **Authenticate** again (expected after the auth fix). Then re-select
   the Discovery Hall experiment so VERA re-syncs file types and defines.
6. In `Project Settings > Player > Scripting Define Symbols`, check
   Android, Web and Standalone. Each should still list
   `VERAFile_Experiment_Telemetry`, `VERAFile_Gaze_Samples`,
   `VERAFile_StudyEvents` and `VERAIV_Condition`.
7. Commit the editor-generated changes: deletion of
   `VERAFile_Experiment_Telemetry.cs(.meta)`, the Spatial Keyboard
   sample, the keyboard prefab in `Assets/VERA/Resources/`, and any
   `packages-lock.json` rewrite.
8. Run the Unity + Quest checklist in the PR description
   (`SUMMARY.md`).

### Updating the embedded package

1. Clone `https://github.com/ucf-research/vera-package` and check out the
   wanted tag.
2. Replace `Packages/com.vera.vera` with that tree minus `Samples~`,
   `.gitignore` and `.vscode`.
3. Re-apply each `NSF-Grant patch` that upstream hasn't fixed. Drop the
   `.meta` backport once the tag includes it.
4. Update the `#vX.Y.Z` pin in `Packages/manifest.json`.
5. Diff `package.json` dependencies against our manifest. Watch for
   `com.unity.xr.openxr` or any change to the XR loader.

## Note for Claude Code sessions

The remote environment's network policy currently **blocks
`vera-xr.io` and `vera.research.ucf.edu`** (verified 2026-07-14: 403 at
CONNECT). To let a Claude session download the plugin or exercise the
portal API, add those domains to the environment's allowed-domains list
in the Claude Code environment settings. Steps 1–2 (account, experiment,
credentials) need a human in a browser regardless.
