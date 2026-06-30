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
> a procedural radial-ring + room-spoke inlay floor (`NSFGrant/RadialFloor`,
> a *Standard surface shader* - unlike the other NSFGrant shaders this one
> is lit, so it keeps catching the key light, ambient trilight, and a new
> realtime `HubReflectionProbe` at the hub center, refreshed once on
> `OnAwake` rather than every frame); a wainscoting baseboard on the solid
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
