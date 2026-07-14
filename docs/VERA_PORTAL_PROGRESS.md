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
| `Gaze_Samples` | ✅ Saved, confirmed correct 2026-07-14 | All 25 columns match `tools/vera/columns/gaze_samples.json`; row 19 `gaze_source` confirmed fixed to String. |
| `StudyEvents` | ✅ Saved 2026-07-14 | All 10 columns match `tools/vera/columns/study_events.json`. |
| `Experiment_Telemetry` | ✅ Auto-created by VERA, not editable | 29 columns of per-frame headset/controller pose + input, added automatically to every experiment. See `docs/VERA_PLUGIN_REFERENCE.md` for the full column list and its intentional overlap with `GazeSamples` head pose columns. |
| `Summary` | Not defined — intentionally skipped (derive from the two streams above during analysis; see reference doc) | |

Confirmed via the experiment dashboard screenshot 2026-07-14 (Files & Data
Recording panel lists all three, plus Questionnaires panel shows all 3
imports landed).

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
| SDG Knowledge Quiz (7 questions) | Pre-VR | ✅ Imported & saved 2026-07-14 |
| SDG Knowledge Quiz (same 7 questions) | Post-VR | ✅ Imported & saved 2026-07-14 |
| Comfort Check-In | Mid-VR | ✅ Imported & saved 2026-07-14 — **content is still a draft needing team sign-off before real use** |

Confirmed via dashboard: Questionnaires panel shows Pre-VR (1), Mid-VR (1),
Post-VR (1), each listing the correct survey name. VQF import worked
cleanly — no QSF fallback needed.

## Participant distribution

**Decision (2026-07-14): in-person lab sessions**, researcher hands the
participant a pre-configured headset — not a self-serve remote link.

The dashboard's "Issues to address before Collection Mode" lists
*"Participant distribution method not configured"* — the Experiment Flow
panel shows a node **"Participants come from an unspecified source (Setup
required)"**. This still needs to be clicked/configured on the portal to
select whatever VERA's in-person/assisted distribution option is called.
**Not yet done — next portal action item.**

Once configured, reconcile with how participant ID / condition assignment
already works in this codebase: `StudyIntake.cs` supports `?pid=&cond=`
URL params, and `CounterbalanceManager` assigns from participant ID. For
lab sessions, the researcher likely needs to enter/confirm the participant
ID in VERA (or open a VERA-generated session URL in the headset browser)
before handing it over — exact mechanism TBD until the portal's assisted
distribution flow is inspected.

`QuizRunner` (existing in-project quiz) stays the fallback/self-contained
path until these are confirmed live on the portal.

## Draft → Collection Mode gate

Portal's own "Issues to address before Collection Mode" (as of 2026-07-14,
3 issues):

| Issue | Status |
|---|---|
| No IRB information provided | 🔲 **Blocked on external process.** SJSU IRB submission in progress (Dr. Chow is PI, via Lea Lim); no approved protocol number exists yet. Do not enter placeholder text — wait for the real number. |
| Consent form not added | 🔲 **Likely also IRB-gated** — the consent form text should be the IRB-approved version. Hold unless interim consent language is cleared for pilot testing. |
| Participant distribution method not configured | 🔲 Decision made (in-person lab sessions), portal configuration not yet done — see "Participant distribution" section above. |

All file types (✅), the `Condition` IV (✅), and all 3 questionnaires (✅)
are done — everything else is 🔲.

Do **not** switch this experiment to Collection Mode until all 3 issues
above clear, **and**:
- A pilot session in Draft Mode has been run end-to-end and the data
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
- 2026-07-14 — Dashboard reviewed: all 3 questionnaires imported and
  saved, `Gaze_Samples`/`StudyEvents` confirmed saved (row 19 `gaze_source`
  fix confirmed), discovered auto-created `Experiment_Telemetry` file type
  (documented in `VERA_PLUGIN_REFERENCE.md`). Participant distribution
  decided as in-person lab sessions (researcher-handed headset) but not
  yet configured on the portal. 3 blocking issues remain before Collection
  Mode: IRB info, consent form (both IRB-gated), participant distribution
  config (actionable now).
