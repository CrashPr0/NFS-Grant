// Opens exhibit links (ContentLink) in a NEW browser tab. Unity's
// Application.OpenURL navigated the study page itself in this build, which
// ended the participant's session. If the popup blocker refuses (the click
// is handled a frame after the browser event, so the gesture can have
// lapsed), the link opens on the participant's next click instead.
mergeInto(LibraryManager.library, {
  NSFGrantOpenUrlInNewTab: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    function open() {
      var w = window.open(url, '_blank');
      if (w) { try { w.opener = null; } catch (e) {} }
      return !!w;
    }
    if (open()) return;
    var retry = function () {
      document.removeEventListener('pointerup', retry, true);
      open();
    };
    document.addEventListener('pointerup', retry, true);
  }
});
