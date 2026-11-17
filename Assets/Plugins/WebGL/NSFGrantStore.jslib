// Tiny synchronous key/value store in the browser's localStorage, so the hall
// can remember a participant's study ID across visits (StudyIntake). Unity's
// PlayerPrefs on WebGL flushes to IndexedDB asynchronously and was lost when
// the page navigated away right after saving.
mergeInto(LibraryManager.library, {
  NSFGrantStoreSet: function (keyPtr, valuePtr) {
    try { window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valuePtr)); } catch (e) {}
  },
  NSFGrantStoreGet: function (keyPtr) {
    var v = '';
    try { v = window.localStorage.getItem(UTF8ToString(keyPtr)) || ''; } catch (e) {}
    var size = lengthBytesUTF8(v) + 1;
    var buf = _malloc(size);
    stringToUTF8(v, buf, size);
    return buf;
  }
});
