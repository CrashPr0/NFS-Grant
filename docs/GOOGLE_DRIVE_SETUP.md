# Send web-study data to your Google Drive

The web build (GitHub Pages / WebXR) has no lab machine to pull files from,
so `RemoteDataUploader` sends each participant's session files to a small
**Google Apps Script** web app you own, which saves them into a Drive
folder — one subfolder per participant. No Google credentials go into the
build. Lab Quest builds don't upload by default (data is pulled over adb).

## 1. Make a Drive folder

Create a folder (e.g. "NFS-Grant Study Data") and copy its **folder ID**
from the URL: `https://drive.google.com/drive/folders/`**`THIS_PART`**.
Check its sharing matches your IRB / data-management plan.

## 2. Deploy the Apps Script

1. https://script.google.com → **New project**.
2. Paste the contents of [`tools/drive-upload/Code.gs`](../tools/drive-upload/Code.gs).
3. Set `FOLDER_ID` (step 1) and `TOKEN` (any long random string).
4. **Deploy → New deployment → Web app** —
   *Execute as:* **Me**, *Who has access:* **Anyone** (so browsers can POST
   without a Google login).
5. Authorize, then copy the **Web app URL** (ends in `/exec`).
6. Sanity check: open the URL in a browser — it should say
   `ok: NFS-Grant upload endpoint is running`.

After editing the script later: **Deploy → Manage deployments → Edit →
Version: New version** (the `/exec` URL stays the same).

## 3. Point the build at it

Copy `upload.config.example.json` to **`upload.config.json`** (repo root,
gitignored) and fill in:

```json
{
  "endpointUrl": "https://script.google.com/macros/s/…/exec",
  "sharedToken": "the same TOKEN string",
  "checkpointSeconds": 120
}
```

(or set `NSF_UPLOAD_URL` / `NSF_UPLOAD_TOKEN` in the environment). The scene
builder injects this into every build; `build-webxr` warns loudly when it's
missing. Then:

```bash
./scripts/unity-tasks.sh build-webxr
./scripts/deploy-pages.sh
```

## When uploads happen

- **Checkpoints** every `checkpointSeconds` during the session, and whenever
  the tab is hidden / window loses focus / headset is removed — so a
  participant who closes the tab early still leaves most of their data.
- A **final upload** when the session ends, retried every 30 s until it
  succeeds. The completion screen says *"Saving your responses… please keep
  this page open"* until the upload is confirmed (desktop and in-headset).
- Unchanged files are skipped; CSVs are gzipped when the runtime supports
  it (the script un-gzips).

## Security model (read this)

The token ships inside the public web build, so anyone can extract it —
treat it as a **spam gate, not a secret**. What protects the data is the
script:

- only study file names are accepted (`gaze_/events_/summary_…csv`,
  `shot_…png`), and each must match the participant folder it's written to;
- **CSVs are append-only**: an upload replaces a stored file only if it
  starts with the stored content (checkpoints are prefixes of the final
  log), so nobody can shorten, alter, or erase stored data;
- PNGs are write-once; a size cap applies; the script never returns stored
  data to callers.

Worst case with a leaked token is junk *new* files in the folder — not
tampering with real participants' data. Rotate `TOKEN` (script + config,
rebuild, redeploy) if that happens.

## Testing without Google

A local stand-in that mirrors the script's rules and Google's web-app
behaviour (no CORS preflight support; 302 to a CORS-enabled response) was
used during development: point `endpointUrl` at it, build, run a session,
and check the files it writes. Never deploy a build configured for a local
endpoint — delete `upload.config.json` / rebuild first.
