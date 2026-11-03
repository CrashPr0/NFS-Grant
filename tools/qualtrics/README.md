# Qualtrics surveys (IRB 26-285)

Two files in Qualtrics "Advanced Format" (Survey → Tools → Import/Export →
Import Survey, choose the .txt). The item wording is verbatim from the
approved Survey Questionnaire (10.12.26) and SDG Knowledge Test (9.10.26);
don't edit it without an IRB modification.

| File | Survey | Items |
|---|---|---|
| `IRB-26-285_PreStudy_AdvancedFormat.txt` | Pre-study | Consent, P1, P2–P9, R1–R3, K1–K3 |
| `IRB-26-285_PostStudy_AdvancedFormat.txt` | Post-study | K1–K3, K4, E1–E8, A1–A2, F1–F2, AI0–AI2, B1–B4, T1–T3, O1–O5 |

The import format can't carry logic, embedded data or redirects. Set
these up in the Qualtrics editor after importing:

## Pre-study survey

1. **Consent text** (`CONSENT_TEXT`): replace the placeholder with the
   stamped consent text, word for word, plus a link to the stamped PDF.
   Browser participants get the Browser consent; lab participants get the
   Meta Quest Pro consent (a copy of this survey, or display logic on a
   question asking which one).
2. **Skip logic on `CONSENT`:** if "I do not agree" → End of Survey.
3. **Skip logic on `P1`:** if "No" → End of Survey. P1 is a gate only;
   per the protocol, nothing is kept for under-18s.
4. **Display logic on `P9`:** show only if P8 is not "Meta Quest Pro".
5. **Study ID:** Survey flow → add Embedded Data `pid` at the top, set
   to the study code you assign (e.g. from a contact list or a field the
   team fills in).
6. **End of survey:** Survey options → "Redirect to a URL":
   `https://crashpr0.github.io/NFS-Grant/?pid=${e://Field/pid}`

## Post-study survey

1. **Study ID:** Survey flow → Embedded Data `pid` at the very top, with
   no value. Qualtrics fills it from the `?pid=` in the link the app
   opens.
2. **Display logic:**
   - `AI1`, `AI2`: only if AI0 is "Yes, once" or "Yes, more than once".
   - Block "Browser Experience Self-Report" (B1–B4) and `T3`: browser
     participants only. The app doesn't pass the device, so either add
     `device` to the link or ask the researcher to use a lab copy without
     them.
   - `T2`: only if T1 is not "No".
3. **Payment core items:** K4, E1, E2, E3, E4, E5 and E7 must be answered
   (or "Prefer not to answer"). Turn on "Force response" for those seven.
4. Copy the anonymous survey link with `?pid={pid}` added, and put it in
   the app's StudyIntake **Post Survey Url** field, e.g.
   `https://sjsu.qualtrics.com/jfe/form/SV_xxxx?pid={pid}`.
