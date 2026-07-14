#!/usr/bin/env node
// Fill the VERA portal's "CSV Column Metadata Configuration" modal from a
// JSON column spec, so you don't hand-type 20+ rows.
//
// This drives YOUR browser where you're already logged into VERA — it does
// not handle auth, hit any private API, or touch the network beyond the page
// you have open. You watch it run and can stop it any time.
//
// ─── How to run ──────────────────────────────────────────────────────────
// 1. Install Playwright once (anywhere):   npm i -g playwright   (or: npx playwright)
// 2. Launch Chrome with remote debugging so this script can attach to your
//    real, logged-in session:
//      macOS:   /Applications/Google\ Chrome.app/Contents/MacOS/Google\ Chrome --remote-debugging-port=9222
//      Windows: "C:\Program Files\Google\Chrome\Application\chrome.exe" --remote-debugging-port=9222
//      Linux:   google-chrome --remote-debugging-port=9222
// 3. In that Chrome: log into vera-xr.io, open your experiment, click
//    "Add a file" (or edit an existing CSV file type) so the
//    "File Configuration" modal with the column table is ON SCREEN.
//    Set the Filename + Extension yourself; leave the cursor on the modal.
// 4. Run:
//      node fill-vera-columns.mjs columns/gaze_samples.json
//      node fill-vera-columns.mjs columns/study_events.json
//
// The script adds each column via the "Add a column" button, filling Column
// Name / Data Type / Description. It does NOT click the modal's final "Save"
// — you review the table and save yourself.
//
// ─── IMPORTANT — selectors are best-effort ───────────────────────────────
// These selectors were written from a single screenshot of the modal, not a
// live portal (the portal is not reachable from the dev environment). If the
// portal's DOM differs, the script will pause and print what it was looking
// for; open DevTools, grab the real label/role, and adjust the SELECTORS
// block below. Running headed (visible) is deliberate so you can see exactly
// where it stops.

import { chromium } from 'playwright';
import { readFileSync } from 'node:fs';

const specPath = process.argv[2];
if (!specPath) {
  console.error('Usage: node fill-vera-columns.mjs <columns/spec.json>');
  process.exit(1);
}
const spec = JSON.parse(readFileSync(specPath, 'utf8'));

// ─── Adjust here if the portal DOM differs from the screenshot ────────────
const SELECTORS = {
  addColumnButton: 'text=Add a column',
  // Within the row being edited, these are the input labels/placeholders.
  columnNameInput: 'input[placeholder*="Column" i], input[name*="name" i]',
  dataTypeControl: 'text=/Integer|String|Float/',        // the type cell/dropdown
  descriptionInput: 'input[placeholder*="descr" i], textarea[placeholder*="descr" i]',
  // Some UIs need a per-row confirm/checkmark; leave null if edits auto-commit.
  rowConfirmButton: null,                                 // e.g. 'button[aria-label="Confirm"]'
};
const CDP_URL = process.env.VERA_CDP_URL || 'http://localhost:9222';
const STEP_PAUSE_MS = Number(process.env.VERA_STEP_MS || 350);

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  console.log(`Attaching to Chrome at ${CDP_URL} …`);
  const browser = await chromium.connectOverCDP(CDP_URL);
  const context = browser.contexts()[0];
  if (!context) throw new Error('No browser context. Is Chrome running with --remote-debugging-port=9222?');

  // Pick the tab that has the modal open.
  const pages = context.pages();
  let page = null;
  for (const p of pages) {
    if (await p.locator(SELECTORS.addColumnButton).count().catch(() => 0)) { page = p; break; }
  }
  if (!page) {
    throw new Error(
      'Could not find a tab showing the "Add a column" button. Open the ' +
      'File Configuration modal in your logged-in VERA tab, then rerun.');
  }

  console.log(`\nFile type: ${spec.fileType}  (${spec.columns.length} custom columns)`);
  console.log('Auto columns pID / conditions / ts are handled by VERA and skipped.\n');

  for (let i = 0; i < spec.columns.length; i++) {
    const col = spec.columns[i];
    process.stdout.write(`  [${i + 1}/${spec.columns.length}] ${col.name} (${col.type}) … `);

    await page.locator(SELECTORS.addColumnButton).last().click();
    await sleep(STEP_PAUSE_MS);

    // Fill the newest (last) name + description inputs in the table.
    const nameInputs = page.locator(SELECTORS.columnNameInput);
    await nameInputs.last().fill(col.name);

    // Data type: click the type cell in the last row, then the matching option.
    // If your portal uses a native <select>, replace this with selectOption.
    try {
      await page.locator(SELECTORS.dataTypeControl).last().click({ timeout: 1500 });
      await page.locator(`text="${col.type}"`).last().click({ timeout: 1500 });
    } catch {
      process.stdout.write('(set Data Type manually) ');
    }

    const descInputs = page.locator(SELECTORS.descriptionInput);
    if (await descInputs.count()) {
      await descInputs.last().fill(col.description || '');
    }

    if (SELECTORS.rowConfirmButton) {
      await page.locator(SELECTORS.rowConfirmButton).last().click().catch(() => {});
    }
    await sleep(STEP_PAUSE_MS);
    console.log('done');
  }

  console.log(
    `\nAll ${spec.columns.length} columns entered. Review the table, fix any ` +
    `Data Type cells the script couldn't set, then click the modal's Save.`);
  // Leave the browser open; we only attached to it.
  await browser.close();
}

main().catch((err) => {
  console.error(`\n✗ ${err.message}`);
  console.error('The browser was left as-is. Adjust the SELECTORS block if the DOM differs.');
  process.exit(1);
});
