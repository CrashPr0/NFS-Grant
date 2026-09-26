/**
 * NFS-Grant study upload endpoint (Google Apps Script Web App).
 *
 * Receives session files from Unity's RemoteDataUploader and stores them in
 * a Drive folder you own, one subfolder per participant. Setup:
 * docs/GOOGLE_DRIVE_SETUP.md. Deploy as a Web App:
 *   Execute as: Me   |   Who has access: Anyone
 *
 * Request (a CORS "simple request" - Apps Script can't answer preflights):
 *   POST ?name=<file>&type=<mime>&pid=<participant>&enc=gzip|none&token=<TOKEN>
 *   body: base64 of the file bytes (gzipped when enc=gzip)
 *
 * SECURITY MODEL - read this. The token ships inside the public web build,
 * so treat it as a spam gate, not a secret. What protects the data is here:
 *   - Only study file names are accepted (gaze_/events_/summary_ CSVs,
 *     shot_ PNGs), matching the participant folder, under a size cap.
 *   - CSVs are APPEND-ONLY: an upload may replace a stored file only if it
 *     starts with the stored content (the app's checkpoints are prefixes of
 *     the final log). Nobody can shorten, alter or erase stored data.
 *   - PNGs are write-once.
 *   - The script only ever writes into FOLDER_ID; it never reads data back
 *     out to callers.
 */

var FOLDER_ID = 'PUT_YOUR_DRIVE_FOLDER_ID_HERE';
var TOKEN = 'PUT_A_RANDOM_STRING_HERE'; // must match sharedToken in upload.config.json

var MAX_BODY_CHARS = 45 * 1024 * 1024; // base64 chars (Apps Script caps POSTs near 50 MB)
var PID_RE = /^[A-Za-z0-9_-]{1,64}$/;
var CSV_RE = /^(gaze|events|summary)_([A-Za-z0-9_-]+)_\d{8}_\d{6}\.csv$/;
var PNG_RE = /^shot_([A-Za-z0-9_-]+)_\d+(\.\d+)?s\.png$/;

function doGet() {
  return text_('ok: NFS-Grant upload endpoint is running');
}

function doPost(e) {
  try {
    var p = (e && e.parameter) || {};
    if (TOKEN && p.token !== TOKEN) return text_('error: forbidden');

    var name = String(p.name || '');
    var pid = String(p.pid || '');
    var enc = String(p.enc || 'none');
    if (!PID_RE.test(pid)) return text_('error: bad pid');

    var csv = CSV_RE.exec(name);
    var png = PNG_RE.exec(name);
    if (!csv && !png) return text_('error: bad name');
    // The file must belong to the participant folder it's written into.
    if (name.indexOf('_' + pid + '_') < 0) return text_('error: pid mismatch');

    var body = (e.postData && e.postData.contents) || '';
    if (!body || body.length > MAX_BODY_CHARS) return text_('error: bad size');

    var bytes = Utilities.base64Decode(body);
    if (enc === 'gzip') {
      bytes = Utilities.ungzip(Utilities.newBlob(bytes, 'application/x-gzip')).getBytes();
    } else if (enc !== 'none') {
      return text_('error: bad enc');
    }

    var lock = LockService.getScriptLock();
    lock.waitLock(20000);
    try {
      var folder = participantFolder_(pid);
      var existing = folder.getFilesByName(name);
      var file = existing.hasNext() ? existing.next() : null;

      if (png) {
        if (file) return text_('ok: exists ' + name);
        folder.createFile(Utilities.newBlob(bytes, 'image/png', name));
        return text_('ok: saved ' + name);
      }

      var content = Utilities.newBlob(bytes).getDataAsString('UTF-8');
      if (!file) {
        folder.createFile(name, content, 'text/csv');
        return text_('ok: saved ' + name);
      }
      var stored = file.getBlob().getDataAsString('UTF-8');
      if (content === stored) return text_('ok: unchanged ' + name);
      if (content.length < stored.length || content.indexOf(stored) !== 0) {
        // Not an extension of what we have: refuse rather than overwrite.
        return text_('error: not an append of stored ' + name);
      }
      file.setContent(content);
      return text_('ok: extended ' + name);
    } finally {
      lock.releaseLock();
    }
  } catch (err) {
    return text_('error: ' + err);
  }
}

function participantFolder_(pid) {
  var root = DriveApp.getFolderById(FOLDER_ID);
  var it = root.getFoldersByName(pid);
  return it.hasNext() ? it.next() : root.createFolder(pid);
}

function text_(s) {
  return ContentService.createTextOutput(s).setMimeType(ContentService.MimeType.TEXT);
}
