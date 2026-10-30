// Reports whether the browser really holds pointer lock on the page.
// Unity's Cursor.lockState says Locked as soon as the game asks, even when
// the browser refuses (an iframe without allow="pointer-lock", a lapsed
// click gesture) or has already released it (Esc), and nothing tells the
// game. While it believes the mouse is captured, every click aims from the
// center dot, so on-screen buttons like "Finish exploring" can't be hit.
mergeInto(LibraryManager.library, {
  NSFGrantPointerLocked: function () {
    return document.pointerLockElement ? 1 : 0;
  }
});
