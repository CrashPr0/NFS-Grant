# Aesthetics & Research-Validity Work Order (for Fable)

This is an implementation brief, not implemented code. It captures the
plan agreed on 2026-06-11 for upgrading the UN SDG Discovery Hall's
visuals **and** closing a measurement confound, while preserving the
existing data-collection contract.

## Locked decisions

| Decision | Choice |
|---|---|
| Render pipeline | **Migrate to URP** (Universal Render Pipeline) |
| Sequencing | **Both, phased** — demo polish (SDG 13/4) first, research-grade hardening before fall trials |
| Asset licensing | **Prototype with anything**; do a licensing cleanup pass before any public WebGL/UN release |
| Docent (Condition C) | **Ready Player Me** avatars (cross-platform, loads on WebGL) |

## Non-negotiable invariants (do not break)

1. **Data contract is sacred.** `AttentionTarget`, `InteractableObject`,
   `SdgStation`, `ContentLink`, and their IDs/format tags must remain on
   every instrumented object. The CSV schemas in
   `AttentionDataLogger` / `StudyEventLogger` must not change without
   updating `docs/STUDY_DESIGN.md`.
2. **Curated content stays in `SdgContentLibrary`.** Visual prefabs
   render it; they do not hard-code copy.
3. **One build serves both samples.** Everything must degrade gracefully
   on WebGL (no Meta-only SDKs on the critical path) and hold frame on
   Quest 3.

## Part A — The confound fix (highest research value)

The study compares attention *across information formats*. The current
builder correlates **format with screen position** (video always center,
text far left) and spawns every participant **facing the center
station**. That confounds format with positional salience and
predetermines first-fixation.

Build a **`CounterbalanceManager`** (Assets/Scripts/Core):

- Deterministically derive, from participant ID (or VERA assignment):
  - **zone→position mapping** within each station (Latin square over the
    five format slots),
  - **station visiting order** / arc placement,
  - **spawn heading** (randomized or counterbalanced, not always center).
- Expose the assignment to the scene builder / runtime and **log it**
  (new `counterbalance_assignment` event + a column/section in the
  summary) so analysis can model position as a covariate.
- Keep luminance, panel size, and framing **matched across format zones**
  so the format itself is the manipulated variable. Environment art is
  shared context and may be rich; the *format panels* must stay matched.

Acceptance: two participant IDs produce different, logged zone/position
layouts; no format is systematically centered; spawn heading varies.

## Part B — Swappable-art architecture

Decouple visuals from instrumentation so art can change freely.

- Define a content-zone prefab per format: `TextPanelZone`,
  `DataVizZone`, `VideoKioskZone`, `InteractiveZone`, `CtaButtonZone`,
  `DocentZone`, `ReferencesZone`. Each prefab carries its visuals +
  TextMeshPro + collider + the instrumentation component.
- Add a `ContentZoneView` MonoBehaviour implementing
  `Bind(SdgStationContent, zone-specific fields)` that populates TMP
  text, icon `RawImage`, chart data, link URL, etc.
- `DiscoveryHallBuilder` stops creating primitives: it **instantiates
  prefabs, calls `Bind(...)`, and attaches/positions per the
  `CounterbalanceManager`**. The procedural builder remains the single
  source of truth for layout + instrumentation wiring.

Acceptance: rebuilding the scene yields the same logged structure as
today, with prefab visuals instead of primitives.

## Part C — Asset & rendering upgrades

> **Status (2026-06-12):** an interim procedural dressing pass landed in
> `DiscoveryHallBuilder` ahead of the full prefab/URP work: framed
> exhibit panels (dark faces, theme header bars, legs), station back
> walls and platform discs, the committed UN photos and 2025 Goal-13
> progress card applied as textures, a primitive docent figure with a
> speech panel, tri-light ambient + linear fog + warm key light, and a
> welcome plinth at spawn. Everything below still applies on top.
>
> **Status (2026-06-13):** restructured into a hub-and-rooms layout
> (hexagonal hub the player spawns in, three walled exhibit rooms off
> corridors at +/-120 degrees), modeled on the team's ReadingNation
> Waterfall FrameVR room; all text sizes raised ~25-30%. Added
> **GPU shader animation** on environment-only elements (deliberately
> not the content AOIs, to avoid a motion confound in the attention
> measures): `NSFGrant/AnimatedWater` drives the hub waterfall sheets
> (scrolling) and basin (rippling); `NSFGrant/EmissivePulse` pulses the
> Condition-C docent beacon. All are `_Time`-driven (no per-frame
> scripts) so they hold up on Quest/WebGL. Custom shaders are plain CG
> passes and will need revisiting in the URP migration below.
>
> **Status (2026-06-13, ambient pass):** added a procedural gradient
> skybox (`NSFGrant/GradientSky`, saved as `DiscoveryHallSky.mat`)
> replacing the default blue sky; a soft, faintly theme-tinted realtime
> point light per room plus a cool accent light on the hub waterfall;
> and steady warm wayfinding trim (`NSFGrant/EmissivePulse` at equal
> min/max) framing the three hub doorways identically. The skybox and
> trim are symmetric across rooms/conditions, so none of this biases the
> attention measures. **The four realtime point lights are placeholders
> and must be baked before Quest trials** (see Part D); ranges are kept
> inside each room so they don't cross-light, but realtime per-pixel
> lights cut against the baked-GI target.
>
> **Status (2026-06-30, hub showcase pass):** the hub is the demo
> centerpiece, so it got a dedicated polish pass. Added a Pantheon-style
> oculus skylight (`HubCeiling` + emissive `SkylightOculus` inset +
> `NSFGrant/LightShaft`, a new additive, height-gradient CG shader) with
> a downward fill light so the beam actually lights the floor below it;
> a procedural radial-ring + room-spoke inlay floor (`NSFGrant/RadialFloor`);
> a new realtime `HubReflectionProbe` at the hub center, refreshed once on
> `OnAwake` rather than every frame; a wainscoting baseboard on the solid
> hub walls; ambient warm motes drifting through the hub and a light mist
> rising off the waterfall basin (both `ParticleSystem`, additive, with a
> procedurally generated soft-dot sprite - no texture asset needed). All
> environment-only (no `AttentionTarget`), so none of it can bias the
> attention measures. The reflection probe is realtime like the point
> lights and should be revisited (baked) alongside them at the URP/GI
> migration in Part D.
>
> **Status (2026-06-30, baked sky + glass ceiling follow-up):**
> `NSFGrant/GradientSky` is now baked into a static Cubemap
> (`DiscoveryHallSkyBaked.asset`) + `Skybox/Cubemap` material at scene-
> build time (`BakeGradientSkybox`, computed purely on the CPU - the
> headless pipeline runs Unity with `-nographics`, where
> `Camera.RenderToCubemap` would not work). The live procedural material
> still gets saved alongside it for tweaking in the editor's Lighting
> window, but the *active* skybox is the baked snapshot - cheaper to
> sample than a custom shader pass on Quest, and it's what the hub
> reflection probe now actually reflects. `HubCeiling` is no longer an
> opaque disc: it's a full transparent glass pane (new
> `NSFGrant/GlassCeiling` shader) that samples the hub's reflection probe
> for a Fresnel-rimmed glint of the baked sky, so the whole hub ceiling
> reads as a real skylight rather than just the central oculus inset.
>
> **Status (2026-06-30, magenta-floor fix):** `NSFGrant/RadialFloor` was
> originally written as a `#pragma surface` Standard surface shader so it
> would react to real lighting - but surface shaders depend on the Built-
> in Render Pipeline's lighting library and fail to compile under URP,
> which Unity reports by swapping in the magenta error material (exactly
> what showed up on the hub floor once URP was active). Rewritten as a
> plain CGPROGRAM pass like every other NSFGrant shader; it renders the
> ring/spoke pattern correctly under either pipeline now, at the cost of
> no longer reacting to the directional key light. Worth remembering for
> the URP migration below: `#pragma surface` is off the table for any
> future NSFGrant shader on this project.
>
> **Status (2026-07-01, headset pass):** first on-device test found VR
> locomotion dead — root causes: (1) `CiTools` player builds reused a
> stale scene generated before `VRLocomotion` existed (builds now ALWAYS
> regenerate the scene); (2) the teleport arc's `Physics.Linecast` hit
> the invisible `SdgStation` trigger volumes, which extend into the
> corridors (now `QueryTriggerInteraction.Ignore`). Locomotion was also
> reworked to the standard Quest scheme: left stick = smooth walk driven
> through a CharacterController (real wall collision), right stick
> forward = teleport, right stick left/right = snap turn pivoting around
> the *head* (pivoting the rig origin translates the user sideways), and
> teleports land the head - not the rig origin - on the reticle. Hub
> beauty pass for the headset: a colonnade at the six hexagon corners
> (warm glowing capitals echoing the door trim; symmetric, so no room is
> privileged), stone benches facing the waterfall, a runtime-synthesized
> spatialized waterfall loop (`ProceduralAmbience`, fixed seed, hub-only
> and equidistant from all rooms so it favors no condition), and the sky
> bake bumped to 256/face with a half-LSB dither because 8-bit gradient
> banding is clearly visible on a headset display.
>
> **Status (2026-07-02, tracking-freeze fix + hands):** on-device
> reports of "6DoF drops out for a few seconds, then the view zooms and
> tilts" were NOT tracking loss - `ScreenshotCapture` (builder-enabled,
> 10 s interval) did a full-res eye-buffer readback + CPU downscale +
> PNG encode + file write synchronously on the main thread, freezing
> the app for seconds; a frozen VR app keeps compositing with
> orientation-only reprojection, which looks exactly like that. Capture
> is now fully async (GPU downscale -> AsyncGPUReadback -> worker-thread
> `EncodeArrayToPNG` + write), with a cheap synchronous small-texture
> fallback for WebGL. Also added `VRHandVisual`: procedural controller-
> tracked hands (trigger squeeze closes/warms them) so selection and the
> teleport arc no longer fire from thin air; colliderless and
> AttentionTarget-free so they stay out of the gaze data.
>
> **Status (2026-07-02, stereo fix + laser):** "each controller renders
> differently to each eye" was the classic Single Pass Instanced
> symptom - none of the custom `NSFGrant/*` CG shaders declared the
> stereo-instancing macros (`UNITY_VERTEX_INPUT_INSTANCE_ID` /
> `UNITY_VERTEX_OUTPUT_STEREO` / `UNITY_SETUP_INSTANCE_ID` +
> `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`), so they rendered with one
> eye's matrix for both eyes. Added the macros to all of them
> (UnlitTransparentColor, TextOccluded, AnimatedWater, EmissivePulse,
> LightShaft, RadialFloor, GlassCeiling) - most visible on near-face
> geometry (the hands, the teleport arc/reticle, the laser), but it was
> wrong scene-wide. The hands were the worst offender because
> `CreatePrimitive` under URP can hand back a non-SPI Built-in Standard
> material; they now use an explicit stereo-safe `NSFGrant/HandShaded`
> (fixed-direction half-Lambert, pipeline-independent). Added
> `VRLaserPointer`: a laser + endpoint dot from each controller, purely a
> visual aim aid - selection stays on the gaze ray so the attention and
> interaction signals remain in one coordinate frame per the study
> design. If per-eye issues persist for any element, check that its
> shader carries these macros.
>
> **Status (2026-07-02, polish pass):** (1) rooms and corridors got
> ceilings (dark neutral, with one identical warm light strip per room -
> symmetric, so no attention bias), which both finishes the architecture
> in the headset (open sky above a room read as unbuilt) and, because
> the ceilings carry colliders, seals a real exploit: the teleport arc
> could clear a 4-5 m wall and land the participant on the void floor
> outside the hall with no way back. The hub ceiling gained a collider
> and the hub walls were raised 4 -> 4.4 m to meet it (the old gap
> showed a sky slice and was arc-threadable). (2) `NSFGrant/
> ComfortVignette` + VRLocomotion: a soft peripheral vignette ramps in
> during stick-walking (standard vection-discomfort mitigation; ~0.15 s
> in, slower out; strength/disable in the Inspector). It masks only the
> periphery, only during self-initiated smooth motion, identically in
> all conditions - note it in the methods write-up. (3) VR sessions now
> open on black and fade in (~0.8 s). (4) The teleport reticle is a
> thin ring + center dot instead of an opaque disc.

> **Status (2026-07-14, big-visual-delta pass, authored headless):** a
> scene-wide upgrade written without editor verification (authored from a
> remote session; run `NSF Grant > Build Discovery Hall Scene` in Unity 6
> and walk it before demoing). (1) **Baked detail textures**: two CPU-baked,
> deterministic, tileable grayscale multiplier textures
> (`DiscoveryHallPlaster.asset`, `DiscoveryHallTerrazzo.asset`, same
> -nographics-safe pattern as the skybox bake) now cover every structural
> surface — every `CreateWall` surface gets plaster grain + seam lines,
> the 60 m hall floor and each room platform get terrazzo tiles — killing
> the flat-primitive look scene-wide. (2) **SDG color ring**: the official
> 17-color UN SDG wheel as a slowly rotating ring of glowing segments
> (new `SlowRotator`, one transform Rotate/frame) encircling the skylight
> beam at hub center — colliderless, AttentionTarget-free, equidistant
> from all rooms. (3) **Hub doorway signs**: room title + theme band above
> each hub doorway, readable from spawn (wayfinding no longer requires
> entering a corridor); identical placement/typography, per-room text and
> color only. (4) **Room interior finish**: baseboards + crown trim,
> theme-tinted corner pilasters with glowing capitals, a theme carpet
> runner down each corridor, and planter pairs inside each door —
> identical layout in all three rooms, format-zone panels untouched.
> (5) **Greenery**: identical planter pairs flanking all hub doorways.
> All additions are symmetric across rooms/conditions per the Part A
> invariants. Texture caveat: `ApplySurfaceTexture` must only run after
> `ApplyColor` (which clones the material); it mutates the clone.

- **URP migration:** convert materials, set Quest-appropriate URP asset
  (single-pass instanced, MSAA 4×, fixed-foveated rendering, baked GI +
  light/reflection probes), update Meta XR settings. Verify the desktop/
  WebGL path renders identically.
- **TextMeshPro** for all panels/labels (replace `TextMesh`).
- **Environment:** a museum/UN-pavilion hall (Synty POLYGON or CC0
  equivalent) replacing the bare plane. Bake lighting. Add a skybox
  (AllSky Free / Poly Haven CC0 HDRI).
- **Data-viz zone:** integrate XCharts (MIT) to render real charts from
  the 2025 SDG progress figures, not a flat image.
- **Docent:** Ready Player Me avatar at each station's `DocentZone`,
  driven by the existing persona data (MINERVA / HINA / BHUMI). Wire to
  `DocentGuide` so it only appears in Condition C. Leave a dialogue/TTS
  seam (Oculus LipSync optional, Quest-only).
- **Interaction polish (Quest):** Meta XR Interaction SDK poke/ray +
  haptics; desktop keeps mouse. Behind platform checks.

## Part D — Performance & WebGL guardrails

- Quest 3 target: 72–90 Hz, ~1M tris, <150 draw calls, baked GI, texture
  atlasing, GPU instancing, transparency/overdraw discipline (watch the
  text panels).
- WebGL: texture compression, build-size budget, Addressables if needed.
  Docent + interaction must fall back cleanly (RPM loads on web; Meta
  SDKs do not).
- Profile both targets; record numbers in this doc.

## Phasing

1. URP migration + TMP swap + prefab-ize zones (Parts B & C-rendering).
2. `CounterbalanceManager` (Part A).
3. Environment art + lighting, applied **evenly** across format zones.
4. RPM docent + XCharts data-viz.
5. WebGL parity + perf profiling (Part D).

## Licensing cleanup (before any public/UN release)

Inventory every imported asset; confirm redistribution rights for the
public WebGL build and UN demo; replace anything non-redistributable;
record attributions in `Assets/StudyContent/CONTENT_SOURCES.md`.
