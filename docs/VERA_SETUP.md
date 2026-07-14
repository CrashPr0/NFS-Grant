# VERA Setup — Connecting the Discovery Hall to the VERA Portal

VERA (Virtual Experience Research Accelerator, UCF/SREAL) launched its
early-access platform: a web portal at **https://vera-xr.io** plus a Unity
plugin. Together they handle experiment definition, participant
recruitment/assignment, WebXR deployment, and remote data collection.
This document covers what is already wired in this project and the exact
steps to finish the hookup once you have portal access.

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

VERA distributes the plugin through the portal after login (there is no
public package registry as of July 2026). Once downloaded:

- If it ships as a UPM package: add it to `Packages/manifest.json`
  (git URL or local tarball path).
- If it ships as a `.unitypackage`: import via
  `Assets > Import Package > Custom Package`.

Then add `VERA_PLUGIN_PRESENT` to
`Project Settings > Player > Scripting Define Symbols` for **every build
target** (Android, WebGL, Standalone).

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

## Note for Claude Code sessions

The remote environment's network policy currently **blocks
`vera-xr.io` and `vera.research.ucf.edu`** (verified 2026-07-14: 403 at
CONNECT). To let a Claude session download the plugin or exercise the
portal API, add those domains to the environment's allowed-domains list
in the Claude Code environment settings. Steps 1–2 (account, experiment,
credentials) need a human in a browser regardless.
