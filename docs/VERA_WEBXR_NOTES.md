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

### 2. Our controls were Meta-native (OVRInput) — PORTED 2026-07-14

**Status: done, but unverified — needs one in-headset browser test.**
See "The OVR → Unity XR port" below for what changed and how to check it.

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

## The OVR → Unity XR port (2026-07-14)

Goal: the same generated scene works as a native Quest APK **and** as a
VERA WebXR link with working head/hand tracking and controls.

**Design:** one facade, `Assets/Scripts/Interaction/XRInputBridge.cs`.
It routes to `OVRInput` on the native Meta path and to Unity's built-in
`UnityEngine.XR.InputDevices` on the WebXR / Meta-free path, chosen by the
same `#if (UNITY_WEBGL && !UNITY_EDITOR) || NSFGRANT_NO_META` switch as the
OVR shim, so the two can't disagree. `UnityEngine.XR` is a core module
(already in the manifest) — the port adds **no package dependency**, which
is why it's used instead of XRI or Input System actions.

Both branches always compile (OVR calls resolve against the shim on WebGL;
Unity XR calls are core API everywhere), so a mistake shows up as a
compile error rather than a silently dead control path.

| File | Change |
|---|---|
| `Interaction/XRInputBridge.cs` | **New.** Facade: `IsConnected`, `GetThumbstick`, `GetTrigger`, `GetTriggerDown`, `SendHaptic`/`StopHaptic`. Derives trigger *edges* for Unity XR (which reports level only), cached per frame so several callers see one consistent edge. |
| `Interaction/XRPoseDriver.cs` | **New.** Drives a transform from `XRNode.CenterEye/LeftHand/RightHand`. This is what makes head + hands actually track in WebXR, where the stubbed `OVRCameraRig` moves nothing. Self-disables on the Meta path so it can never fight the real rig. Equivalent to the "Tracked Pose Driver" VERA's guide recommends, written against `UnityEngine.XR` so no extra package is needed. |
| `VRInteractor` | Trigger-down + haptics now via the bridge. |
| `VRHandVisual`, `VRLaserPointer` | Serialized `OVRInput.Controller controller` → `XRInputBridge.Hand hand`; connection + squeeze via the bridge. |
| `VRLocomotion` | All three thumbstick reads (walk, teleport aim, snap turn) via the bridge. |
| `Core/PlatformRigSwitcher` | **Behaviour fix for WebXR:** a browser page loads *flat* and the participant clicks "Enter VR" later, so startup-only rig switching would strand them in the desktop rig inside the headset. On the Unity XR path it now re-evaluates while running and swaps rigs live. Quest keeps startup-only behaviour. |
| `Editor/DiscoveryHallBuilder` | Writes the renamed `hand` field, and attaches `XRPoseDriver` to the head + both controller anchors so **one** generated scene serves both targets. |

**Native Quest behaviour is intentionally unchanged** — on that path every
call still lands on `OVRInput` exactly as before, and the pose drivers
disable themselves.

### What still does NOT work in WebXR (platform limits, not fixable here)
- **Eye tracking** — finding #1 above. Still head-gaze only in a browser.
- **Haptics** — the bridge sends an impulse where supported and silently
  skips where not; browsers commonly don't support it.

### How to verify (the test I could not run)
1. Native regression first: build/run the Quest APK — locomotion, teleport,
   snap turn, hands, laser, trigger select should behave exactly as before.
2. `VERA > Settings > Build and Upload Experiment`, open the link on a
   desktop browser: flat mouse/keyboard tour works.
3. Open the same link in the **Quest browser** → "Enter VR": head and hands
   should track, left stick walks, right stick teleports/snap-turns,
   trigger selects. If controllers don't track, apply the guide's fix —
   switch the XR provider from OpenXR to **Oculus**.

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
