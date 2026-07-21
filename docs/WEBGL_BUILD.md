# WebGL build & share-a-link runbook

Goal: produce a browser-playable build of the Discovery Hall and get a
public URL to share. The WebGL build runs the **desktop rig** (WASD +
mouse-look, click to interact) — the same experience as the laptop
sample. It is NOT the VR experience (that's the Quest APK); a browser
can't drive a headset here. But it's a fully walkable tour of the hall,
which is exactly what a "share a link" demo needs.

## Hard prerequisite: the Editor must compile first

A WebGL build cannot start until the project compiles clean in the Editor.
If you're still seeing the Meta XR / `InputActionProperty` / package
errors, fix those first — see **docs/VERA_SETUP.md → "Troubleshooting the
Unity 6 first open"**. Nothing below matters until the Console is green.

You also need the **WebGL Build Support** module installed for this Editor
version (Unity Hub → your 6000.3.9f1 install → Add Modules → WebGL Build
Support). Without it, WebGL won't appear as a build target.

## Why a browser build needed a code change

Our gaze/interaction scripts call Meta's `OVRInput` / `OVREyeGaze` /
`OVRCameraRig` directly, and Meta's SDK isn't part of a WebGL player — so
the build would fail with "OVRInput not found". `Assets/Scripts/Compat/
OVRWebGLStub.cs` supplies no-op stand-ins **only inside the WebGL player
binary** (`#if UNITY_WEBGL && !UNITY_EDITOR`), so:

- the Editor and the Quest/Android build are completely untouched (they
  use the real SDK — the stub compiles out);
- the WebGL binary compiles against the stubs, which report "no
  controllers / no eye tracking", so the disabled VR rig stays inert and
  the desktop rig carries the experience.

**One thing to watch:** if the WebGL build instead errors with *"duplicate
definition of OVRInput"*, that means this Meta SDK version already
compiles its assembly for WebGL and the stub is redundant — just delete
`Assets/Scripts/Compat/OVRWebGLStub.cs` and rebuild. (Both outcomes are a
one-file toggle; you can't get stuck.)

## Build

Two ways — use whichever is faster for you.

**A. From the Editor (simplest):**
1. `File > Build Profiles` (or Build Settings) → select **WebGL** →
   **Switch Platform**. First switch reimports all assets and is slow
   (10–30 min) — expected, let it finish.
2. `Player Settings > Publishing Settings`: set **Compression Format =
   Disabled** (or leave Gzip/Brotli and tick **Decompression Fallback**).
   Disabled is the most host-agnostic for a quick share and avoids
   Content-Encoding header issues on static hosts.
3. Back in Build Profiles → **Build** → output folder `Builds/WebGL`.

**B. Headless (one command), if Unity is on PATH / `UNITY_PATH` set:**
```bash
./scripts/unity-tasks.sh build-webgl   # -> Builds/WebGL/
```
This regenerates the scene and builds via `CiTools.BuildWebGL`. It does
NOT change compression settings — set those once in the Editor (step A2)
first, they persist in ProjectSettings.

Output is a folder with `index.html`, a `Build/` subfolder, and
`TemplateData/`.

## Host it — fastest paths to a public link tonight

The build folder is static files; any static host works. Fastest:

- **itch.io** (recommended for tonight): zip the **contents** of
  `Builds/WebGL` (so `index.html` is at the zip root, not inside a
  subfolder) → create a new project → Kind = **HTML** → upload the zip →
  tick **"This file will be played in the browser"** → set a viewport
  (e.g. 1280×720) → Save → **View page**. Public URL immediately. Set the
  project to Public (or Restricted with the secret link) to share.
- **Unity Play** (play.unity.com): Unity's own free WebGL hosting — new
  build → upload the zipped `Builds/WebGL` → get a share URL. Native fit
  for Unity WebGL, no compression fiddling.
- **GitHub Pages**: copy `Builds/WebGL` into a `docs/` folder or a
  `gh-pages` branch and enable Pages. Requires **Compression = Disabled**
  (step A2) unless you also configure decompression fallback — otherwise
  the page loads blank.

## What the viewer will see / do

- Mouse to look, WASD to walk, click the exhibit panels/CTAs to interact.
- The full hall: hub with the SDG color ring + skylight + waterfall, the
  three themed exhibit rooms, the pre/post flow (intake → quiz → explore →
  ranking) on the desktop path.
- No headset controls (that's intentional on web).

## Caveats to mention when you share

- Data logging on WebGL writes to the browser's sandboxed storage, not a
  lab folder; treat this build as a **visual/UX demo**, not a data-
  collection run. Real sessions use the Quest APK or a VERA-hosted WebXR
  deployment.
- First load downloads the whole build (tens of MB); give it a moment.
