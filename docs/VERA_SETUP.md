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
https://github.com/ucf-research/vera-package.git
```

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
4. **If duplicate-TextMeshPro type errors appear next:** VERA declares
   `com.unity.textmeshpro 3.0.6`, but Unity 6 merged TMP into
   `com.unity.ugui 2.0.0`. If Unity doesn't reconcile this on its own,
   report it — a single manifest override addresses it (do not download
   anything extra for this).

## Note for Claude Code sessions

The remote environment's network policy currently **blocks
`vera-xr.io` and `vera.research.ucf.edu`** (verified 2026-07-14: 403 at
CONNECT). To let a Claude session download the plugin or exercise the
portal API, add those domains to the environment's allowed-domains list
in the Claude Code environment settings. Steps 1–2 (account, experiment,
credentials) need a human in a browser regardless.
