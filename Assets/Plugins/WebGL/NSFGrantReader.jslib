// In-page reader for links and PDFs opened from inside a headset.
//
// A WebXR immersive session shows ONLY the WebGL scene: the browser cannot
// draw a web page, a PDF, or another tab inside it, so a link opened from VR
// used to land in a tab the participant could not see. C# therefore ends the
// immersive session first (BrowserTab.OpenReader) and this puts the page in a
// full-window reader over the study page, which the headset's flat browser
// view then shows. The study tab, and the session, stay open underneath.
//
//  - "Back to the hall" closes the reader; press the page's VR button to
//    re-enter (a browser only lets a user gesture start an immersive session).
//  - "Open in new tab" is a real anchor click, so popup blockers allow it.
//    Some sites refuse to be framed and the frame would stay blank; C#
//    passes an empty frame URL for the known ones (BrowserTab.ToFrameable)
//    and the reader shows a notice pointing at this button instead.
mergeInto(LibraryManager.library, {
  NSFGrantShowReader: function (frameUrlPtr, openUrlPtr, titlePtr) {
    var frameUrl = UTF8ToString(frameUrlPtr);
    var openUrl = UTF8ToString(openUrlPtr);
    var title = UTF8ToString(titlePtr);

    var old = document.getElementById('nsf-reader');
    if (old && old.parentNode) old.parentNode.removeChild(old);

    var root = document.createElement('div');
    root.id = 'nsf-reader';
    root.setAttribute('role', 'dialog');
    root.style.cssText = 'position:fixed;left:0;top:0;right:0;bottom:0;z-index:2147483000;' +
      'display:flex;flex-direction:column;background:#12161d;color:#eef1f6;' +
      'font-family:system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;';

    var bar = document.createElement('div');
    bar.style.cssText = 'display:flex;flex-wrap:wrap;align-items:center;gap:12px;' +
      'padding:12px 16px;background:#1b2230;border-bottom:2px solid #009edc;';

    var heading = document.createElement('div');
    heading.textContent = title || 'Exhibit link';
    heading.style.cssText = 'flex:1 1 220px;font-size:20px;font-weight:600;' +
      'overflow:hidden;text-overflow:ellipsis;white-space:nowrap;';

    var btnCss = 'font:inherit;font-size:20px;font-weight:600;padding:14px 22px;border-radius:10px;' +
      'border:0;cursor:pointer;color:#fff;text-decoration:none;display:inline-block;';

    var newTab = document.createElement('a');
    newTab.textContent = 'Open in new tab';
    newTab.href = openUrl;
    newTab.target = '_blank';
    newTab.rel = 'noopener noreferrer';
    newTab.style.cssText = btnCss + 'background:#2b3a55;';

    var back = document.createElement('button');
    back.type = 'button';
    back.textContent = 'Back to the hall';
    back.style.cssText = btnCss + 'background:#009edc;';

    bar.appendChild(heading);
    bar.appendChild(newTab);
    bar.appendChild(back);

    var hint = document.createElement('div');
    hint.textContent = 'To go back into the headset, press Back to the hall, then the VR button. ' +
      'If this page stays blank, the site does not allow viewing here: use Open in new tab.';
    hint.style.cssText = 'padding:8px 16px;font-size:15px;color:#aab4c3;background:#171d29;';

    var frame;
    if (frameUrl) {
      frame = document.createElement('iframe');
      frame.src = frameUrl;
      frame.title = title || 'Exhibit link';
      frame.referrerPolicy = 'no-referrer';
      frame.setAttribute('allow', 'fullscreen; autoplay; encrypted-media; picture-in-picture');
      frame.style.cssText = 'flex:1 1 auto;width:100%;border:0;background:#fff;';
    } else {
      // A site known to refuse framing: say so up front rather than show
      // an empty white box.
      hint.textContent = 'To go back into the headset, press Back to the hall, then the VR button.';
      frame = document.createElement('div');
      frame.style.cssText = 'flex:1 1 auto;display:flex;flex-direction:column;align-items:center;' +
        'justify-content:center;gap:24px;padding:32px;text-align:center;';
      var msg = document.createElement('div');
      msg.textContent = 'This website can\'t be shown inside the study page. ' +
        'Open it in a new tab, then come back to this tab when you are done.';
      msg.style.cssText = 'max-width:640px;font-size:22px;line-height:1.45;';
      var bigTab = newTab.cloneNode(true);
      bigTab.style.cssText = btnCss + 'background:#009edc;font-size:24px;padding:18px 30px;';
      var host = document.createElement('div');
      try { host.textContent = new URL(openUrl).hostname; } catch (e) { host.textContent = openUrl; }
      host.style.cssText = 'font-size:16px;color:#aab4c3;';
      frame.appendChild(msg);
      frame.appendChild(bigTab);
      frame.appendChild(host);
    }

    function close() {
      document.removeEventListener('keydown', onKey, true);
      if (root.parentNode) root.parentNode.removeChild(root);
    }
    function onKey(e) { if (e.key === 'Escape') close(); }
    back.addEventListener('click', close);
    document.addEventListener('keydown', onKey, true);

    root.appendChild(bar);
    root.appendChild(hint);
    root.appendChild(frame);
    document.body.appendChild(root);
  }
});
