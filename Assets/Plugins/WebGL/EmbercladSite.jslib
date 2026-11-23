// Calls into the website page that hosts the WebGL build (emberclad.org/play/).
// The site defines window.EmbercladExit(), which tears the game down and shows the
// page's original Play screen again. If it is missing, reloading the page does the same.
mergeInto(LibraryManager.library, {
  EmbercladExitToSite: function () {
    try {
      if (typeof window.EmbercladExit === 'function') { window.EmbercladExit(); return; }
      if (window.parent && window.parent !== window && typeof window.parent.EmbercladExit === 'function') { window.parent.EmbercladExit(); return; }
    } catch (e) { console.warn('EmbercladExit failed', e); }
    window.location.reload();
  }
});
