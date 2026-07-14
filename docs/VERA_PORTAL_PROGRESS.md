# VERA Portal Progress Log

The portal (vera-xr.io) is a separate system from this repo — nothing here
is enforced by git, and a fresh Claude session has no way to see the
portal's current state except what's recorded here. **Update this file
whenever portal configuration changes**, so the next session (human or
Claude) picks up accurately instead of re-deriving it from screenshots.

Cross-reference: `docs/VERA_PLUGIN_REFERENCE.md` (API/column specs this
progress is implementing), `docs/VERA_SETUP.md` (the linear setup guide).

## Experiment

- **Name:** Discovery Hall (SDG attention study)
- **Mode:** Draft (must stay Draft until IRB protocol number is issued —
  see `docs/CHECKLIST.md` / IRB section)
- **Portal account owner:** TBD — confirm who holds researcher login

## File types (CSV Column Metadata Configuration)

| File type | Status | Notes |
|---|---|---|
| `GazeSamples` | ✅ Entered 2026-07-14, corrected, **pending final Save click** | All 25 columns match `tools/vera/columns/gaze_samples.json`. Last known error (row 19 `gaze_source` typed Float instead of String) was flagged 2026-07-14 — confirm it was changed to String before/when Save was clicked. |
| `StudyEvents` | ✅ Entered 2026-07-14, corrected, **pending final Save click** | All 10 columns match `tools/vera/columns/study_events.json`. |
| `Summary` | Not defined — intentionally skipped (derive from the two streams above during analysis; see reference doc) | |

**Action for next session:** ask the user to confirm both file types were
actually saved (not just corrected on screen) before assuming this is done.

## Independent variables

| IV | Status | Levels |
|---|---|---|
| `Condition` | ✅ Saved 2026-07-14 | Passive (Ps) / Interactive (Int) / Guided (Guid) |

Confirmed via screenshot: "Conditions (3)" panel showing all three levels,
navigation advanced to the Surveys & Questionnaires screen afterward.

## Questionnaires / surveys

See `docs/VERA_QUESTIONNAIRES.md` for full content and the JSON specs in
`tools/vera/questionnaires/`.

| Survey | Column | Status |
|---|---|---|
| SDG Knowledge Quiz (7 questions) | Pre-VR | 🔲 Ready to import — `tools/vera/questionnaires/sdg_knowledge_quiz_pre.vqf` |
| SDG Knowledge Quiz (same 7 questions) | Post-VR | 🔲 Ready to import — `tools/vera/questionnaires/sdg_knowledge_quiz_post.vqf` |
| Comfort Check-In | Mid-VR | 🔲 Ready to import, **but content is a draft needing team sign-off** — `tools/vera/questionnaires/comfort_check_in.vqf` |

**Custom Questionnaire Importer confirmed to exist** (2026-07-14) — supports
VQF (VERA's native YAML format, used here) and QSF (Qualtrics export). All
3 questionnaires are authored as `.vqf` files and just need drag-drop +
Import & Preview + confirm. See `docs/VERA_QUESTIONNAIRES.md` for exact
column mapping and the scoring caveat (VQF has no correct-answer field —
knowledge quiz scoring must happen during analysis, not on the portal).

`QuizRunner` (existing in-project quiz) stays the fallback/self-contained
path until these are confirmed live on the portal.

## Draft → Collection Mode gate

Do **not** switch this experiment to Collection Mode until:
1. All file types + IVs + surveys above are ✅
2. IRB protocol number is issued and entered (see `docs/VERA_SETUP.md` /
   `docs/CHECKLIST.md`)
3. A pilot session in Draft Mode has been run end-to-end and the data
   shape on the portal has been spot-checked against
   `tools/vera/columns/*.json`

## Unity-side plugin status (separate from portal state above)

| Step | Status |
|---|---|
| Project migrated to Unity 6000.3.9f1 | ✅ `ProjectVersion.txt` / `Packages/manifest.json` updated |
| VERA plugin installed via UPM git URL | ✅ in `Packages/manifest.json`; not yet confirmed resolved in-editor |
| `VERA > Settings > Authenticate` run | 🔲 requires a human in the Unity editor (interactive OAuth, can't be done from a headless session) |
| Experiment selected in VERA Settings (triggers code-gen) | 🔲 blocked on the above |
| `VERA_PLUGIN_PRESENT` added to Scripting Define Symbols (all targets) | 🔲 blocked on plugin resolving + code-gen existing |
| `VeraPluginAdapter.cs` TODOs replaced with real `VERAFile_*` / `VERASessionManager` calls | 🔲 blocked on code-gen |

## Change log

- 2026-07-14 — Portal column entry for `GazeSamples`/`StudyEvents` reviewed
  twice; last known outstanding issue was row 19 `gaze_source` type.
  Experimental Design tab confirmed empty (no IVs set) via screenshot.
- 2026-07-14 — `Condition` IV (Passive/Interactive/Guided) saved. Moved to
  Surveys & Questionnaires screen; quiz content pulled from
  `DiscoveryHallBuilder.CreateQuizAsset()` and documented in
  `docs/VERA_QUESTIONNAIRES.md` + `tools/vera/questionnaires/*.json`.
  Flagged: `STUDY_DESIGN.md` claims an 8-question quiz, code has 7.
- 2026-07-14 — Confirmed portal's Custom Questionnaire Importer supports
  VQF/QSF. Authored `.vqf` files for all 3 cards (Pre-VR/Post-VR quiz,
  Mid-VR comfort draft); ready to import, not yet confirmed saved.
