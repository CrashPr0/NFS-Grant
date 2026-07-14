# VERA Questionnaires — Content Reference

Content for the three "Surveys & Questionnaires" columns on the portal
(Pre-VR / Mid-VR / Post-VR). Machine-readable versions live in
`tools/vera/questionnaires/*.json`. Cross-reference:
`docs/VERA_PLUGIN_REFERENCE.md` (phase semantics), `docs/VERA_PORTAL_PROGRESS.md`
(live save status).

## Before hand-entering anything

The Surveys & Questionnaires screen has a **"Custom Questionnaire
Importer"** button. Try it first with the JSON specs in
`tools/vera/questionnaires/` (or export one as CSV if the importer wants
that) — if it accepts a bulk format, it saves rebuilding 3 cards by hand.
If the importer expects a specific template, download/inspect it and we
can regenerate the JSON to match.

## Pre-VR and Post-VR: SDG Knowledge Quiz

Source of truth: `Assets/Editor/DiscoveryHallBuilder.cs` →
`CreateQuizAsset()`, which also writes
`Assets/StudyContent/SdgKnowledgeQuiz.asset` in Unity. Create this as
**two separate cards** (one under Pre-VR, one under Post-VR) with
identical content — VERA runs each column's cards once, in order, so the
same quiz needs to exist in both places.

**⚠ Discrepancy:** `docs/STUDY_DESIGN.md` (line 103) says "8-question
knowledge quiz," but the generator only defines **7**. Decide whether to
add an 8th question to the code or correct the doc — flagged, not fixed,
since inventing a question isn't a call to make unilaterally.

| # | ID | Prompt | Correct answer |
|---|---|---|---|
| 1 | `sdg4_q1` | What is the aim of SDG 4? | Ensure inclusive, equitable quality education and lifelong learning for all |
| 2 | `sdg4_q2` | According to the UN, accelerating progress on Goal 4 would have what effect? | A catalytic effect on the whole 2030 Agenda |
| 3 | `sdg11_q1` | SDG 11 aims to make cities and human settlements... | inclusive, safe, resilient and sustainable |
| 4 | `sdg11_q2` | Helsinki's Oodi Central Library is notable as... | a nearly zero-energy public space co-designed with residents |
| 5 | `sdg13_q1` | What does SDG 13 call for? | Urgent action to combat climate change and its impacts |
| 6 | `sdg13_q2` | Thammasat University Library's award-winning green program is built around... | the circular economy (From Waste to Wealth) |
| 7 | `sdg13_q3` | A seed library primarily helps a community by... | sharing and tracking seeds for local growing |

Full 4-option answer sets are in
`tools/vera/questionnaires/pre_post_knowledge_quiz.json`. All are
multiple-choice, single correct answer, no partial credit.

Both cards are auto-administered (browser, no Unity call needed) per
VERA's Pre-VR/Post-VR semantics — this can fully replace the local
`QuizRunner` path if the team commits to VERA for quiz delivery.

## Mid-VR: Comfort Check-In (DRAFT — needs team sign-off)

**This content does not exist anywhere in the repo today.** The only
related code is the `ComfortVignette` shader/vignette effect in
`VRLocomotion.cs` — there is no existing questionnaire to port. What's in
`tools/vera/questionnaires/mid_vr_comfort_check.json` is a minimal
placeholder, **not a validated instrument**:

| ID | Type | Prompt |
|---|---|---|
| `comfort_q1` | 1–7 Likert | Right now, how comfortable do you feel physically (nausea, dizziness, eye strain)? |
| `comfort_q2` | Yes/No | Do you want to pause or stop the session? |

Before using this in a real session, the research team should decide:
1. **Adopt a validated instrument instead** (e.g. a licensed Simulator
   Sickness Questionnaire short-form) if comfort/cybersickness is a
   reported outcome for the study, not just a safety check.
2. **Or keep this lightweight check-in** if its only purpose is letting a
   participant self-pause — in that case it may not need IRB scrutiny as
   a "questionnaire" at all, just a UI affordance. Confirm with IRB either
   way before Collection Mode.

Trigger from Unity (once VERA plugin is authenticated and code-gen has
run):
```csharp
VERASurveyHelper.StartSurvey(
    VERASurveyHelper.VERASurveyReference.S_ComfortCheckIn, inVR: true);
```
Suggested trigger point: right after the participant's first sustained
stick-walk (see `docs/VERA_PLUGIN_REFERENCE.md`).

## After creating the cards

Update the tracking table in `docs/VERA_PORTAL_PROGRESS.md` (Questionnaires
section) with what was actually saved, and note whether the Custom
Questionnaire Importer worked (so future sessions don't re-attempt manual
entry unnecessarily).
