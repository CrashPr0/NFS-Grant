# Connect study uploads to your Google Drive

The app's `RemoteDataUploader` POSTs each session file (the CSV logs and,
when enabled, the screenshot PNGs) to a URL you choose. To land them in
**your** Google Drive, deploy the small Google Apps Script Web App below.
It runs as you and writes to a folder you pick — **no Google credentials
go into the Unity build**, and it works the same from desktop, WebGL, and
Quest.

## 1. Make a Drive folder

Create a folder in Google Drive (e.g. "NFS-Grant Study Data"). Open it and
copy the **folder ID** from the URL:
`https://drive.google.com/drive/folders/`**`THIS_PART`**.

## 2. Create the Apps Script Web App

1. Go to https://script.google.com → **New project**.
2. Replace the contents with the script below.
3. Set `FOLDER_ID` to your folder ID, and `TOKEN` to any random string
   (a shared secret so random posts are rejected).
4. **Deploy → New deployment → type: Web app.**
   - *Execute as:* **Me**
   - *Who has access:* **Anyone**  (required so the app can POST without a
     Google login; the `TOKEN` is what gates writes)
5. Authorize when prompted. Copy the **Web app URL** (ends in `/exec`).

```javascript
// Receives study files from the Unity RemoteDataUploader and saves them to
// a Drive folder. Deploy as a Web App (Execute as: Me, Access: Anyone).
var FOLDER_ID = 'PUT_YOUR_DRIVE_FOLDER_ID_HERE';
var TOKEN     = 'PUT_A_SHARED_SECRET_HERE'; // must match the Unity field

function doPost(e) {
  try {
    if (TOKEN && (!e || !e.parameter || e.parameter.token !== TOKEN)) {
      return ContentService.createTextOutput('forbidden');
    }
    var name = (e.parameter && e.parameter.name) || ('upload_' + Date.now());
    var type = (e.parameter && e.parameter.type) || 'application/octet-stream';
    var bytes = Utilities.base64Decode(e.postData.contents); // body is base64
    var blob  = Utilities.newBlob(bytes, type, name);
    DriveApp.getFolderById(FOLDER_ID).createFile(blob);
    return ContentService.createTextOutput('ok: ' + name);
  } catch (err) {
    return ContentService.createTextOutput('error: ' + err);
  }
}
```

## 3. Point the app at it

In the scene, select the **AttentionStudy** GameObject →
**RemoteDataUploader**:

- **Endpoint Url** = the Web app `/exec` URL.
- **Shared Token** = the same `TOKEN` string.
- **Upload Screenshots** = on (default) to include the PNG stills.

Uploads fire automatically at the end of each session (`StopSession`).

## 4. Test it

- **Quick check:** paste the `/exec` URL in a browser — a GET should return
  a short message (or a harmless error), confirming it's deployed.
- **Real check:** run a session (Play mode), end it (F10 → finish), and
  watch the Console for `[RemoteDataUploader] Uploaded ...`. The files
  should appear in your Drive folder within a few seconds.

## Notes & limits

- **Security:** "Anyone" access means anyone with the URL can POST, but the
  `TOKEN` check rejects anything without your secret. Treat the URL + token
  like a password; rotate the token by editing the script and the Unity
  field. The script can only write to the one folder you set.
- **Privacy:** eye-tracking data is sensitive. Make sure this Drive folder's
  sharing matches your IRB/data-management plan before sending real
  participant data (see the README's compliance notes).
- **WebGL:** browser CORS may block the POST depending on deployment; test
  the web build specifically. Desktop and Quest are not subject to CORS.
- **Re-deploy:** after editing the script, use **Deploy → Manage
  deployments → Edit → New version**, or the URL won't pick up changes.
- **Alternative:** any HTTP server works as the endpoint too — it just
  needs to base64-decode the body and read `?name`/`?type`/`?token`.
