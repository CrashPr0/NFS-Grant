# NSF Grant — VR Attention Data Collection (Quest 3 / Quest Pro)

Unity project for collecting attention data in virtual reality on Meta
Quest headsets. It records per-frame gaze samples (eye tracking on Quest
Pro, head-gaze on Quest 3), classifies fixations, tracks dwell time on
labeled areas of interest (AOIs), and exports research-ready CSV files.

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
5. **Build the sample scene**: from the menu bar run
   **`NSF Grant > Build Sample Scene`**. This generates
   `Assets/Scenes/AttentionStudy.unity` containing:
   - an `OVRCameraRig` with left/right `OVREyeGaze` components,
   - the data-collection stack (`GazeProvider`, `FixationDetector`,
     `GazeRaycaster`, `AttentionDataLogger`, `SessionController`),
   - four colored AOI objects (`AttentionTarget`) in front of the participant.
6. Add the scene to `Build Settings > Scenes in Build`, connect the headset,
   and **Build And Run**.

## How it works

| Script | Responsibility |
|---|---|
| `GazeProvider` | Produces one gaze ray per frame: averaged binocular eye gaze on Quest Pro (via `OVREyeGaze`), head gaze fallback on Quest 3. |
| `FixationDetector` | I-VT fixation classification (30°/s velocity threshold, 100 ms minimum duration — Salvucci & Goldberg, 2000). Thresholds are editable in the Inspector. |
| `GazeRaycaster` | Raycasts the gaze ray into the scene and tracks which `AttentionTarget` is being looked at. |
| `AttentionTarget` | Marks any object (with a Collider) as an AOI; accumulates dwell time, look count, and time-to-first-look. |
| `AttentionDataLogger` | Streams per-frame samples to CSV and writes a per-target summary at session end. |
| `SessionController` | Requests the eye-tracking permission, sets the participant ID, starts/stops sessions, drives sampling. |

To instrument your own stimuli, add an `AttentionTarget` component to any
object with a Collider and give it a stable **Target Id**.

Set the **Participant Id** on the `SessionController` component (on the
`AttentionStudy` object) before each run, or set it from your own intake UI
via `SessionController.ParticipantId` before calling `StartSession()`.

## Data output

Files are written to the app's private storage on the headset:

```
/sdcard/Android/data/<your.package.name>/files/StudyData/
├── gaze_P000_20260610_153000.csv      # per-frame samples
└── summary_P000_20260610_154500.csv   # per-AOI dwell statistics
```

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
├── Editor/SampleSceneBuilder.cs    # NSF Grant > Build Sample Scene menu item
└── Scripts/
    ├── Gaze/        # GazeProvider, FixationDetector, GazeRaycaster, AttentionTarget
    ├── Logging/     # AttentionDataLogger (CSV export)
    └── Session/     # SessionController (permissions, session lifecycle)
Packages/manifest.json              # Meta XR Core SDK + Unity dependencies
ProjectSettings/ProjectVersion.txt  # Unity 2022.3 LTS
```
