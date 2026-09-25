# Scene screenshots

Still PNGs of the study locations for the IRB report. The capture tool
does not save scenes or other assets.

**Unity version:** 6000.3.9f1 (`ProjectSettings/ProjectVersion.txt`).

No PNGs are checked in with this change. Producing them requires a local
Unity editor with a graphics device (see below). This environment did not
have Unity installed.

## How to run

Generate the scene first. Scene files are not in the repository.

From the menu, in Unity 6000.3.9f1:

1. `NSF Grant > Build Discovery Hall Scene` (writes `Assets/Scenes/DiscoveryHall.unity`).
2. Optionally `NSF Grant > Build Sample Scene` (writes `Assets/Scenes/AttentionStudy.unity`, the minimal gaze-test scene).
3. `Tools > Capture Scene Screenshots`.

For the lighting the study build actually uses, run `NSF Grant > Setup URP Pipeline` before building the scene, or use the full headless setup (URP, media, Discovery Hall scene, Build Settings, Android player settings):

```bash
./scripts/unity-tasks.sh setup
```

Then capture. This needs a GPU, so the command leaves graphics enabled
(the other tasks in that script pass `-nographics`, which cannot render cameras):

```bash
./scripts/unity-tasks.sh screenshots
```

The same step without the wrapper, from the repo root (macOS Hub default; set `UNITY_PATH` or substitute the executable on Linux/Windows — the script looks in the Hub locations for 6000.3.9f1):

```bash
"/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit \
  -projectPath "$(pwd)" \
  -executeMethod NSFGrant.EditorTools.SceneScreenshotCapture.CaptureAll \
  -logFile -
```

Batch mode exits with code 1 if it wrote nothing. PNGs land in
`Screenshots/` at 1920×1080, named `<scene>_<location>.png`.

On a Linux machine with no display, run that same Unity command under
`xvfb-run -a` so the editor still gets a graphics device.

Headless scene generation, if you do not want the full setup:

```bash
Unity -batchmode -nographics -quit -projectPath . \
  -executeMethod NSFGrant.EditorTools.DiscoveryHallBuilder.BuildDiscoveryHall \
  -logFile -
```

## Scenes

The tool opens, in order:

1. Every scene listed in **File > Build Settings** whose file exists (enabled or not). A listed path that is missing on disk is logged and skipped.
2. Then, if they exist and were not already listed:
   - `Assets/Scenes/DiscoveryHall.unity` — the study scene (`CiTools` puts only this scene in Build Settings).
   - `Assets/Scenes/AttentionStudy.unity` — the original minimal gaze-test scene.

## Locations

Shot list is discovered from the open scene. Nothing is moved in the scene; a temporary camera (not saved) is placed at each spot.

| Location | When | Camera |
|---|---|---|
| `desktop_spawn` | A camera on `DesktopPlayer` / `DesktopCamera` | That camera's pose. In Discovery Hall this is world `(0, 1.6, -2)`, looking +Z across the hub toward the SDG 11 doorway and the welcome plinth. |
| `vr_spawn` | A camera under `OVRCameraRig` that is not a left/right eye | That camera's pose. If the eye anchor is still on the floor (no tracking in the editor), the shot is raised to 1.6 m and keeps the rig's facing. Left and right eye cameras are skipped. |
| `CentralHub` | An object named `CentralHub` | Standing in the hub court at local `(1.6, 1.6, 1.2)`, looking toward the waterfall on the south wall (local target `(0, 1.7, -5)`). |
| `<station id>` | Each `SdgStation` | Just inside the doorway (local `(0, 1.6, 5)`), looking at the exhibit wall (local target `(0, 1.7, 0)`). The builder faces stations back at the hub, so local +Z points at the hub. |
| `<Spawn name>` | A transform whose name contains `Spawn`, or is `PlayerStart` / `StartPoint` / `StartPosition`, and that does not already have a camera | Eye height on that transform, looking along its forward. Skipped when it matches a camera shot already taken. |
| `overview` | None of the above, but the scene has renderers | One framing shot of the renderer bounds. |

Discovery Hall, after `Build Discovery Hall Scene`, produces:

- `Screenshots/DiscoveryHall_desktop_spawn.png`
- `Screenshots/DiscoveryHall_vr_spawn.png`
- `Screenshots/DiscoveryHall_CentralHub.png`
- `Screenshots/DiscoveryHall_SDG04_QualityEducation.png` — room at yaw -120°, 16 m from the hub (MINERVA / Quality Education)
- `Screenshots/DiscoveryHall_SDG11_SustainableCities.png` — room at yaw 0° (HINA)
- `Screenshots/DiscoveryHall_SDG13_ClimateAction.png` — room at yaw +120° (BHUMI)

The sample scene, if generated, produces `Screenshots/AttentionStudy_vr_spawn.png` (the rig looking at the four attention targets). It has no stations and no hub. `vr_spawn` is omitted if the Meta XR camera rig did not build (package not imported yet); the desktop spawn is still captured in Discovery Hall.

## Setup gaps

These block a first open or a first capture. They were left as-is because fixing them means generating Unity assets, not a small hand edit.

- **No scene files are committed.** Until the builder runs, Build Settings has nothing to open and this tool writes nothing.
- **`ProjectSettings/` only contains `ProjectVersion.txt`.** There is no `EditorBuildSettings.asset`. Unity recreates default project settings on first open; the scene list stays empty until `./scripts/unity-tasks.sh setup` or you add `Assets/Scenes/DiscoveryHall.unity` yourself. Do not hand-write Build Settings: the scene file and its GUID do not exist yet.
- **Packages need a network on first import.** `Packages/manifest.json` pulls `com.meta.xr.sdk.core` from Meta's scoped npm registry and `com.vera.vera` from GitHub. A Meta registry timeout is a known first-open failure (see the repo history). There is no committed `packages-lock.json`; Unity regenerates it.
- **URP is a dependency, not a configured pipeline.** The pipeline asset is created by `NSF Grant > Setup URP Pipeline` (or `setup`) under `Assets/Settings/`. Before that, the editor uses the built-in pipeline. Capture renders with whichever pipeline is active, so run setup before capturing if the stills should match the study build.
- The layout tree in `README.md` still says ProjectVersion.txt is Unity 2022.3 LTS. The file itself, and the Requirements section, say **6000.3.9f1**. Open the project with that editor.
