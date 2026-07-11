# NSF Grant — UN SDG Discovery Hall (VR Attention Study)

Unity project for the NSF-funded study of information-seeking and
attention in immersive environments, built for the SJSU LTI Lab /
ALA IRC UN Subcommittee project in collaboration with the VERA
(Virtual Experience Research Accelerator) team. One build serves two
participant groups: Meta Quest headsets in the lab, and a desktop/WebGL
version for the self-paced web sample.

It records per-frame gaze samples (eye tracking on Quest Pro, head-gaze
on Quest 3 and desktop), classifies fixations, tracks dwell time on
labeled areas of interest, logs clicks/key presses/station visits/
navigation paths as discrete events, supports the three study conditions
(Passive / Interactive / Guided), and exports research-ready CSV files.

**Start here: [docs/STUDY_DESIGN.md](docs/STUDY_DESIGN.md)** maps every
study measure and meeting decision to its implementation.

> **Important hardware note:** the **Quest Pro has eye-tracking
> hardware; the Quest 3 does not.** On Quest 3 this project automatically
> falls back to **head gaze** (the forward direction of the headset),
> which is a widely used proxy for overt attention. The `gaze_source`
> column in the data records which signal was used for every sample, so
> the two can be separated during analysis.

## Requirements

- **Unity 2022.3 LTS** (project was created with 2022.3.55f1) with the
  **Android Build Support** module (including OpenJDK and Android SDK/NDK)
  installed via Unity Hub.
- **Meta Quest 3 or Quest Pro** with [developer mode enabled](https://developers.meta.com/horizon/documentation/native/android/mobile-device-setup/).
- A [Meta developer account](https://developers.meta.com/) (required for
  device builds and for the eye-tracking permission).
- USB-C cable and `adb` (installed with Unity's Android module).

## Headless setup (no editor UI)

With Unity 2022.3 + Android module installed via Unity Hub, the whole
setup can run from the command line:

```bash
./scripts/unity-tasks.sh setup        # URP + media download + scene + XR config
./scripts/unity-tasks.sh build-quest  # -> Builds/SDGDiscoveryHall.apk (adb install)
./scripts/unity-tasks.sh build-webgl  # -> Builds/WebGL/
```

(Windows: `scripts\unity-tasks.bat setup` etc. Set `UNITY_PATH` if Unity
isn't in the default Hub location.) The first run imports all packages
and takes a while. One thing batch mode can't fully replace: run the
**Meta Project Setup Tool** once in the editor before shipping lab
builds — it validates manifest/permission details.

The SDG goal icons and SDG 13 photos are committed in
`Assets/StudyContent/Textures/` (attribution in
`Assets/StudyContent/CONTENT_SOURCES.md`), so setup needs no Google
Drive access; the media-download step only re-fetches missing files.

## First-time setup

1. **Open the project** in Unity Hub. On first open, Unity resolves the
   packages in `Packages/manifest.json`, including the
   **Meta XR Core SDK** from Meta's scoped registry (this requires
   internet access and may take a few minutes).
2. **Switch platform**: `File > Build Settings > Android > Switch Platform`.
3. **Run the Meta Project Setup Tool**:
   `Edit > Project Settings > Meta XR` (or the popup that appears) and click
   **Fix All / Apply All**. This configures XR Plug-in Management, graphics
   settings, and the Android manifest for Quest.
4. **Enable eye tracking (Quest Pro)**: in `Edit > Project Settings > Meta XR`
   (or on the `OVRManager` component's *Quest Features > Permission Requests*),
   set **Eye Tracking Support** to **Supported**. This adds
   `com.oculus.permission.EYE_TRACKING` to the manifest; the app requests it
   at runtime.
5. **Build the study scene**: from the menu bar run
   **`NSF Grant > Build Discovery Hall Scene`**. This generates
   `Assets/Scenes/DiscoveryHall.unity` containing:
   - both rigs — an `OVRCameraRig` (with left/right `OVREyeGaze`) and a
     `DesktopPlayer` (WASD + mouse) — switched automatically at runtime,
   - the full data-collection stack (gaze, fixations, events, screenshots,
     uploader, condition manager, VERA bridge, session controller),
   - three placeholder SDG stations (SDG 4, SDG 11, SDG 13), each with
     text panel, data visualization, video kiosk, interactive object,
     call-to-action wall, and docent placeholder — all instrumented,
   - a docent beacon route for the Guided condition.

   (`NSF Grant > Build Sample Scene` still generates the original minimal
   gaze-test scene.)
6. Add the scene to `Build Settings > Scenes in Build`, connect the headset,
   and **Build And Run**. For the web sample, switch platform to **WebGL**
   and build the same scene; configure the upload endpoint on the
   `RemoteDataUploader` component first.

### Desktop / web controls

WASD or arrow keys to move; hold the **right mouse button** to look around;
**left-click** to select content. Set **Active Input Handling** to
"Input Manager (Old)" or "Both" in Player settings (the scripts use the
classic Input API).

### VR (Quest) controls

Standard Quest scheme. **Left thumbstick** walks smoothly in the direction
you're facing (with real wall collision and a soft peripheral comfort
vignette while moving). Push the **right thumbstick forward** to aim a
teleport arc and release to jump to the ring reticle (green = valid,
red = out of bounds); flick the **right thumbstick** left/right to
snap-turn in place. Teleports and turns fade briefly to black, and the
session itself fades in from black. Gaze at content and pull **either
trigger** to select (a short haptic pulse confirms the hit). Your
controllers appear as stylized hands that close as you squeeze the
trigger, each projecting a laser pointer as an aim aid (selection stays
on the gaze ray).

> **Rebuild the scene before every headset build.** The scene file is
> generated output — `scripts/unity-tasks.sh build-quest` now regenerates
> it automatically, but if you Build And Run from the editor, run
> **`NSF Grant > Build Discovery Hall Scene`** first or the APK ships
> whatever old scene is on disk.

## How it works

| Script | Responsibility |
|---|---|
| `GazeProvider` | Produces one gaze ray per frame: averaged binocular eye gaze on Quest Pro (via `OVREyeGaze`), head/camera gaze fallback on Quest 3 and desktop. |
| `FixationDetector` | I-VT fixation classification (30°/s velocity threshold, 100 ms minimum duration — Salvucci & Goldberg, 2000). Thresholds are editable in the Inspector. |
| `GazeRaycaster` | Raycasts the gaze ray into the scene and tracks which `AttentionTarget` is being looked at. |
| `AttentionTarget` | Marks any object (with a Collider) as an AOI, tagged with its information format (text / data-viz / video / interactive / docent / call-to-action) and station; accumulates dwell time, look count, time-to-first-look. |
| `SdgStation` | Trigger volume per SDG station; tracks visits, time inside, and first entry — the navigation-path backbone. |
| `InteractableObject` | Clickable content; every activation logged with world (and on desktop, screen) coordinates. Content response suppressed in the Passive condition. |
| `DesktopInteractor` / `DesktopPlayerController` | Laptop/WebGL input: WASD + mouse-look navigation, click logging with 2D screen coords, key-press logging. |
| `VRInteractor` | Gaze-and-commit selection with the controller trigger in VR; short haptic pulse on a successful selection. |
| `VRLocomotion` | Hand-rolled VR locomotion: left-stick smooth walk (CharacterController collision), right-stick teleport arc + reticle, right-stick snap turn pivoting on the head. Fades to black around jumps; logs every teleport/turn. |
| `ProceduralAmbience` | Runtime-synthesized spatial waterfall loop in the hub (no audio asset); fixed seed so every participant hears the same sound. |
| `VRHandVisual` | Controller-tracked stylized hands (procedural primitives, stereo-safe `NSFGrant/HandShaded`); fingers close and warm in color as the trigger squeezes, hidden when the controller disconnects. |
| `VRLaserPointer` | Laser line + endpoint dot from each controller (visual aim aid; selection stays on the gaze ray). |
| `TeleportSurface` | Marks a collider as a valid teleport destination (the hall's base floor plane). |
| `StudyConditionManager` | Holds the active condition (Passive / Interactive / Guided). |
| `DocentGuide` | Guided-condition route: beacon highlights the next suggested station; all guidance logged. |
| `QuizDefinition` / `QuizRunner` | Pre/post knowledge quiz; responses logged as events (IMGUI panel for desktop, API for VR/world-space UI). |
| `AttentionDataLogger` | Streams per-frame gaze samples to CSV; writes the session summary (targets, stations, interactions). |
| `StudyEventLogger` | Discrete-event CSV: clicks, key presses, station enter/exit, docent guidance, quiz responses, session lifecycle. |
| `ScreenshotCapture` | Optional low-rate PNG stills (off by default). |
| `RemoteDataUploader` | POSTs session CSVs to a configurable endpoint (web sample). |
| `VeraBridge` | Integration seam for the VERA Unity plugin (session + event stream as C# events). |
| `SessionController` | Permission, participant ID, condition/platform stamping, session lifecycle, drives sampling and upload. |

To instrument your own stimuli, add an `AttentionTarget` component to any
object with a Collider and give it a stable **Target Id**.

Set the **Participant Id** on the `SessionController` component (on the
`AttentionStudy` object) before each run, or set it from your own intake UI
via `SessionController.ParticipantId` before calling `StartSession()`.

## Data output

Files are written to the app's private storage on the headset:

```
/sdcard/Android/data/<your.package.name>/files/StudyData/
├── gaze_P000_20260610_153000.csv      # per-frame gaze/head samples
├── events_P000_20260610_153000.csv    # clicks, key presses, stations, docent, quiz
├── summary_P000_20260610_154500.csv   # per-AOI / per-station / per-object statistics
└── screenshots/                       # optional low-rate stills (off by default)
```

On desktop the same files land in the OS-specific
`Application.persistentDataPath`; on WebGL they are uploaded via
`RemoteDataUploader` (configure the endpoint before building).

**One folder for everything:** set **Custom Data Folder** on the
`SessionController` (the `AttentionStudy` object) to write every file —
CSVs and screenshots — into a folder you choose instead of the buried
per-app path. Point it at a project folder, an external drive, or an
OS-encrypted volume (BitLocker / FileVault / VeraCrypt) if the data must
be private at rest. `NSF Grant > Analysis > Open Study Data Folder` opens
whichever folder is active. To send copies to Google Drive on top of
this, see [docs/GOOGLE_DRIVE_SETUP.md](docs/GOOGLE_DRIVE_SETUP.md).

Retrieve them with:

```bash
adb pull /sdcard/Android/data/<your.package.name>/files/StudyData ./StudyData
```

### Per-frame CSV schema (`gaze_*.csv`)

| Column(s) | Description |
|---|---|
| `timestamp_utc_ms` | Unix epoch milliseconds (UTC) |
| `session_time_s`, `frame` | Seconds since session start; Unity frame count |
| `head_pos_*`, `head_rot_*` | Head position (m) and rotation (quaternion), world space |
| `gaze_origin_*`, `gaze_dir_*` | Gaze ray origin and normalized direction, world space |
| `gaze_source` | `EyeTracking` (Quest Pro) or `HeadGaze` (fallback) |
| `gaze_confidence` | Eye-tracker confidence (1.0 for head gaze) |
| `angular_velocity_deg_s` | Gaze angular velocity used by the I-VT classifier |
| `is_fixating`, `fixation_id` | Fixation state and monotonically increasing fixation ID |
| `hit_target` | `AttentionTarget` ID under the gaze ray (empty if none) |
| `hit_point_*`, `hit_distance` | World-space gaze hit point and distance (empty if no hit) |

The summary file lists, per AOI: total dwell time, look count, and
time-to-first-look, plus session duration and total fixation count.

Sampling occurs once per rendered frame (72–120 Hz depending on the
headset's display refresh rate). For analyses that need the native 30 Hz
eye-tracker timestamps rather than frame-aligned samples, resample on
`timestamp_utc_ms` during post-processing.

## Research & compliance notes

- **IRB / participant privacy:** eye-tracking data is identifiable
  biometric-adjacent data. The logger writes only to the app's private
  storage and `.gitignore` excludes `StudyData/` and `*.csv` so participant
  data is never committed to this repository. Handle exports according to
  your IRB protocol and data management plan.
- **Meta platform policy:** apps using eye tracking must disclose it to
  users; the OS shows a permission prompt the first time the app runs.
  Raw eye images are never exposed — only derived gaze direction.
- **Calibration:** Quest Pro eye tracking should be calibrated per
  participant in the headset's system settings
  (`Settings > Movement Tracking > Eye Tracking`) before each session.

## Project layout

```
Assets/
├── Editor/          # Scene builders (Discovery Hall + minimal sample scene)
└── Scripts/
    ├── Core/        # Platform detection, rig switcher, study conditions
    ├── Gaze/        # GazeProvider, FixationDetector, GazeRaycaster, AttentionTarget
    ├── Interaction/ # InteractableObject, desktop + VR interactors, desktop movement
    ├── Stations/    # SdgStation (per-station visit tracking)
    ├── Docent/      # DocentGuide (Condition C)
    ├── Survey/      # QuizDefinition, QuizRunner
    ├── Logging/     # Gaze CSV, event CSV, screenshots, remote upload
    ├── Vera/        # VeraBridge (VERA Unity plugin integration seam)
    └── Session/     # SessionController (lifecycle orchestration)
docs/STUDY_DESIGN.md # Study design ↔ implementation map (read this first)
Packages/manifest.json              # Meta XR Core SDK + Unity dependencies
ProjectSettings/ProjectVersion.txt  # Unity 2022.3 LTS
```
