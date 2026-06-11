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
