# Morning Checklist — keep the momentum

State as of the evening of 2026-06-12, on branch
`claude/brave-euler-8p3tc3`. Milestones this list is paced against:
**UN demo July 15, 2026** (SDG 13 + SDG 4 first), **Fall 2026 trials**,
**iConference submission ~Sept 15, 2026**.

## 1. Verify last night's changes (15 min, do first)

- [ ] Pull the branch, let scripts recompile, run
      **NSF Grant > Build Discovery Hall Scene**.
- [ ] Walk the hall in Play mode and confirm:
  - [ ] text no longer renders through panels/walls/docent
        (new `NSFGrant/TextOccluded` shader),
  - [ ] hub-and-rooms layout per the director: spawn in the hexagonal
        hub (waterfall feature, welcome plinth), three corridors at
        120 degrees lead to enclosed exhibit rooms with door signs,
  - [ ] text is comfortably readable at conversational distance
        (sizes were bumped ~25-30% across the board),
  - [ ] framed panels, walls, platform discs look right; you cannot
        walk through walls,
  - [ ] SDG 13 shows the 2025 progress card + both UN photos with captions,
  - [ ] hub waterfall sheets scroll and the basin ripples
        (`NSFGrant/AnimatedWater`); with `?cond=guided`, the docent
        beacon pulses (`NSFGrant/EmissivePulse`) — both must animate in
        Play mode, not just the editor,
  - [ ] gradient skybox replaces the flat blue sky
        (`Assets/StudyContent/DiscoveryHallSky.mat` was created), rooms
        have a soft theme-tinted glow, the waterfall has a cool accent,
        and the three hub doorways are framed with steady warm trim,
  - [ ] no console errors from the builder (watch for shader-not-found
        warnings if the `Assets/Shaders` import lagged).
- [ ] **Before any Quest trial:** bake lighting — the four room/hub
      point lights are realtime placeholders (see AESTHETICS_PLAN Part D).
- [ ] Screenshot anything off and iterate before piling on new work.

## 2. First end-to-end pilot session (highest research value)

Nothing validates the instrumentation like one real session.

- [ ] Desktop Play mode: intake panel → pre-quiz → visit all 3 stations →
      click CTAs → F10 → post-quiz.
- [ ] Open the three CSVs in `Application.persistentDataPath/StudyData`
      (`gaze_*`, `events_*`, `summary_*`) and sanity-check: AOI ids look
      right, station enter/exit pair up, quiz responses logged,
      counterbalance assignment line present.
- [ ] Repeat once with `?cond=guided` (or Inspector) to see the docent
      beacon route fire and log `docent_*` events.
- [ ] File issues for any schema problems **now** — the schema must lock
      before Fall trials.

## 3. Device + web builds (the two participant groups)

- [ ] Quest: `scripts\unity-tasks.bat build-quest`, install via adb, run
      once on-device. Run the **Meta Project Setup Tool** first (only
      manual step batch mode can't do). On Quest Pro: calibrate eye
      tracking in system settings and confirm `gaze_source=EyeTracking`
      in the CSV; on Quest 3 confirm the head-gaze fallback.
- [ ] WebGL: `scripts\unity-tasks.bat build-webgl`, host locally, test
      `?pid=P001&cond=interactive` URL assignment.
- [ ] `RemoteDataUploader`: stand up (or stub) the upload endpoint and
      verify a session's CSVs actually arrive. This is the web sample's
      whole data path and still untested.

## 4. Content gaps (quick wins)

- [ ] SDG 4 and SDG 11 have no photos — pick 2 per station from the team
      Drive / UN media library, add to `Assets/StudyContent/Textures/`,
      fill `PhotoFileNames`/`PhotoCaptions` in `SdgContentLibrary`,
      update `CONTENT_SOURCES.md` attribution. (Drive MCP can fetch.)
- [ ] SDG 4 / SDG 11 progress-report cards for their data-viz panels
      (`DataVizImageFileName` — same mechanism as SDG 13's 2025 card).
- [ ] VR quiz: quizzes are skipped in-headset until a world-space quiz UI
      exists. Decide: build a world-space `QuizRunner` UI, or formally
      administer quizzes outside the headset for the lab sample.

## 5. Bigger swings (docs/AESTHETICS_PLAN.md, in phase order)

- [ ] **Part B prefab-ization:** move zone visuals into prefabs with a
      `ContentZoneView.Bind(...)` so artists can restyle without touching
      instrumentation. The procedural pass that landed tonight makes the
      target look concrete.
- [ ] **TextMeshPro swap** (crisper text; removes the legacy TextMesh
      sizing gymnastics and the custom occlusion shader).
- [ ] **URP + Quest rendering config** (single-pass instanced, MSAA 4x,
      fixed foveated rendering), baked lighting.
- [ ] **XCharts** real charts on the data-viz panels.
- [ ] **Ready Player Me docent** replacing the primitive figure; wire to
      `DocentGuide`, Condition C only.

## 6. Coordination / external

- [ ] Demo VERA team (Corey, Ali) the `VeraBridge` seam; agree on
      participant/condition assignment handoff.
- [ ] July 15 UN demo: decide live build vs. video fallback; SDG 13 +
      SDG 4 stations are the showcase.
- [ ] Licensing review of UN icons/photos before any public WebGL link
      (see `CONTENT_SOURCES.md`) and before opening the repo.
- [ ] Open a PR from `claude/brave-euler-8p3tc3` once the scene verifies,
      so the team can review the week's work as one diff.

## 7. Team feedback (2026-06-13) — triage

Relayed from the team. Status: [x] done, [~] needs a decision before build,
[ ] queued.

- [x] **Click heatmap** — `NSF Grant > Analysis > Generate Click Heatmap...`
      reads an events_*.csv and renders a top-down world heatmap + a
      screen-space heatmap + per-AOI click counts. Offline only; no runtime
      impact. (Verify against a real session CSV once we have one.)
- [x] **Recording → Drive (enable screenshots + upload)** — screenshot
      capture is ON in the built scene; `RemoteDataUploader` POSTs the PNG
      stills alongside the CSVs. **Google Drive connector:** deploy the Apps
      Script Web App in `docs/GOOGLE_DRIVE_SETUP.md`, paste its URL + token
      into the `RemoteDataUploader` component (no Google creds in the build).
      Confirm storage budget (~120 frames per 20-min session at 10 s) and
      that the folder's sharing meets the IRB/data plan before real data.
- [x] **Value ranking task** — `ValueRankingDefinition` +
      `ValueRankingRunner`, run post-exploration (Intake → pre-quiz →
      explore → post-quiz → **ranking** → done); logs `value_rank` /
      `value_rank_complete`. **The four values in `SdgValueRanking.asset`
      are PLACEHOLDERS — the team must confirm/replace them** (edit the asset
      or `CreateValueRankingAsset`). Skipped in VR like the quizzes.
- [x] **Gamify (light)** — `ProgressTracker`: rooms-explored HUD +
      completion badge, logs `room_progress` / `exploration_complete`.
      Screen-space only (not a world AOI) and rewards completion, not
      interaction, so it doesn't bias the attention measures.
- [ ] **Beauty vs. information** — research framing (the two linked papers:
      Kunitake, *Potential of VR for the SDGs*; Springer ch.
      10.1007/978-3-031-81322-1_15). Both 403'd to automated fetch (Springer
      paywalled); get PDFs/abstracts from the team. Already pulling in this
      direction with the aesthetic pass — fold the framing into STUDY_DESIGN
      and keep aesthetics even across format zones so it's a study variable,
      not a confound.
