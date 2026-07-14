# VERA portal column automation

VERA has no public API or config-import for defining experiment file types —
the only way in is the web portal's "CSV Column Metadata Configuration"
modal. Typing 20+ columns by hand for `GazeSamples` is the tedious part;
this folder automates it by driving your own logged-in browser.

## What's here

| File | Purpose |
|---|---|
| `columns/gaze_samples.json` | Authoritative column spec for the `GazeSamples` file type (mirrors `gaze_*.csv`). |
| `columns/study_events.json` | Column spec for the `StudyEvents` file type (mirrors `events_*.csv`). |
| `fill-vera-columns.mjs` | Playwright script that fills the modal from a spec. |

The **JSON specs are the real deliverable** — they're generated from the
logger source, so they're correct no matter how you enter them (by hand,
by this script, or by a future import feature). The script is just a
convenience.

## Recommended: try these first

1. **Look for an "Import" / "Duplicate" button** in the portal's file-type
   UI. Many research tools let you clone a file type or upload a config —
   far more reliable than DOM automation. If VERA has it, use the JSON here
   as your source of truth and skip the script.
2. **Ask the VERA team** (UCF/SREAL) whether there's a bulk column import or
   an experiment-config API. If yes, tell us and we'll target it directly.

## Using the script (if you're entering columns by hand otherwise)

It attaches to a Chrome you've already logged into — it never handles your
VERA credentials or hits any private endpoint. You watch it run.

```bash
# 1. Install Playwright (once)
npm i -g playwright

# 2. Launch Chrome with remote debugging
#    macOS:
/Applications/Google\ Chrome.app/Contents/MacOS/Google\ Chrome --remote-debugging-port=9222
#    Windows:
"C:\Program Files\Google\Chrome\Application\chrome.exe" --remote-debugging-port=9222
#    Linux:
google-chrome --remote-debugging-port=9222

# 3. In that Chrome: log into vera-xr.io, open your experiment, and open the
#    "File Configuration" modal (Add a file → set Filename + Extension=csv).

# 4. Fill the columns
node fill-vera-columns.mjs columns/gaze_samples.json
node fill-vera-columns.mjs columns/study_events.json
```

It clicks "Add a column" and fills Column Name / Data Type / Description for
each entry. It does **not** click the modal's final **Save** — you review
the table and save yourself.

### Heads-up: selectors are best-effort

The selectors were written from one screenshot of the modal, not a live
portal (the portal isn't reachable from our dev environment, so this was
never run end-to-end). If the DOM differs, the script pauses and prints
what it was looking for — open DevTools, grab the real label/role, and edit
the `SELECTORS` block at the top of `fill-vera-columns.mjs`. It runs headed
on purpose so you can see where it stops. Env overrides: `VERA_CDP_URL`,
`VERA_STEP_MS`.

## Keeping specs in sync with the code

If the CSV schemas in `AttentionDataLogger.cs` / `StudyEventLogger.cs`
change, update the matching JSON here so the portal and the loggers don't
drift. The column order in each JSON is the order VERA's generated
`CreateCsvEntry(...)` will expect its arguments.
