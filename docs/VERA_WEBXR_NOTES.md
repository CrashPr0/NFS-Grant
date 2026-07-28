# VERA WebXR — what it means for this project

Source: VERA docs → Advanced Features → WebXR guide
(https://vera-xr.io/static/docs/sections/advanced-features/webxr.html),
captured 2026-07-14. This file is the project-specific reading of that
page — what applies to the Discovery Hall study, not a copy of the guide.

## How VERA WebXR works (facts from the guide)

- WebXR = immersive XR that runs in a **web browser**, no install. VERA
  uses it as its **primary** participant-distribution method.
- Under the hood VERA drives the **De-Panther Unity WebXR Export plugin**
  (github.com/De-Panther/unity-webxr-export), installed automatically when
  you export from the VERA plugin. Unity has no native WebXR support.
- **Build + upload:** `VERA > Settings` → **Build and Upload Experiment**
  (must be authenticated). VERA installs deps, adjusts settings, builds,
  and uploads to the portal. Builds are **hosted on VERA, not stored
  locally**. First build can take **20+ min**; later builds much faster.
  Only the **active experiment** selected in VERA Settings is built.
- **Participant link:** comes from the experiment's **distribution method**
  on the web interface (e.g. remote manual sharing →
  `vera-xr.io/remote/[yourid]`). Open the link → participant experience
  launches in-browser.
- Render pipeline: **URP** is the happy path (we're on URP 17). Built-in
  works with extra steps.

## The two findings that actually matter for OUR study

### 1. WebXR does NOT support eye tracking (this is the big one)

The guide lists **eye tracking, hand tracking, and some haptics** as
features "not fully supported in WebXR," and says if you rely on them
"you may need to consider alternative distribution methods."

This study's **primary dependent measure is eye-tracked gaze** on Quest
Pro. Over WebXR that measure is unavailable — `GazeProvider` would fall
back to **head gaze** (which it already does gracefully). Consequences:

- WebXR is great for **demos, pilots, desktop/remote participants, and
  head-gaze attention data**.
- WebXR is **NOT** a substitute for the eye-tracking arm. Real
  eye-tracking data still requires the **native Quest build (APK)** on a
  Quest Pro. Plan for both paths, not WebXR-only.
- Haptics (`OVRInput.SetControllerVibration` in `VRInteractor`) also
  won't fire over WebXR — cosmetic, not a blocker.

### 2. Our controls are Meta-native (OVRInput) — dead in WebXR until ported

Confirmed in-repo: the rig is `OVRCameraRig` + `OVRManager`, **no Unity
`XROrigin` / `TrackedPoseDriver`**, and all input goes through `OVRInput`
(VRLocomotion, VRInteractor, VRHandVisual, VRLaserPointer). WebXR gets
controller input through the **browser WebXR API surfaced via Unity XR**,
not OVRInput — the guide's own troubleshooting says to add a
**"Tracked Pose Driver"** (non-Input-System variant, "Generic XR
Controller", Left/Right) and to set the **XR provider to Oculus**.

So in a WebXR build:
- **Desktop rig (mouse/keyboard): works** — no OVR dependency.
- **VR-in-browser: launches but controls are dead** until the input layer
  is ported OVRInput → Unity XR Input (tracking via Tracked Pose Driver,
  buttons/thumbsticks via the Input System / XRI actions VERA already
  depends on). This is the same 5 files we mapped for the WebGL shim
  (GazeProvider + the 4 interaction scripts).

## Pre-empt the guide's known gotchas (checklist for the first WebXR build)

- **Everything pink** → URP material problem. Ensure a URP Asset exists
  (`Create > Rendering > URP Asset (With Universal Renderer)`), assign it
  under `Graphics > Default render pipeline`, then
  `Window > Rendering > Rendering Pipeline Converter > Convert materials`.
  Watch our custom `NSFGrant/*` shaders + baked-texture materials here.
- **Only one eye renders** → `Project Settings > Graphics > Enable
  Compatibility Mode (Render Graph Disabled)`.
- **Participant too tall** → remove manual camera y-offsets; if/when we
  move to an `XROrigin`, set Tracking Origin Mode = **Floor** (XRI and
  WebXR both add a height offset → they double up). We currently set
  `OVRManager.TrackingOrigin.FloorLevel`; revisit at the XR port.
- **Controllers don't track** → XR provider OpenXR → **Oculus**; add the
  non-Input-System **Tracked Pose Driver**.
- **Video:** N/A for us — the "Video story" zones are link/text panels,
  there is **no Unity `VideoPlayer`** in the project, so the WebGL
  "Video Clip source unsupported" limitation doesn't apply. (If we ever
  add real video, use `VideoSource.Url` + `StreamingAssets`.)

## Bottom line / decision

- **Deploy path:** VERA WebXR (Build and Upload → hosted link) is the right
  remote/participant distribution and beats hand-hosting a WebGL build.
  It needs VERA auth + an active experiment first (blocked on the package/
  Meta resolve issue).
- **Two-track reality:** WebXR = accessible demo + head-gaze + surveys, on
  a hosted link. Native Quest APK = the eye-tracking research data. Don't
  let WebXR's convenience hide that it drops the study's core sensor.
- **To make VR-in-browser actually usable:** port input OVR→Unity XR
  (the sprint). Desktop-in-browser needs none of that and works today.
