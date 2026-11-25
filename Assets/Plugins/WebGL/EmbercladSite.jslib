// Calls into the website page that hosts the WebGL build (emberclad.org/play/).
// The site defines window.EmbercladExit(), which tears the game down and shows the
// page's original Play screen again. If it is missing, reloading the page does the same.
//
// The rest is what phones need from the page (see WebBridge.cs):
//  - touch detection, so the touch controls only appear on touch devices;
//  - a real <input> laid over the nickname field, because a phone only opens its
//    keyboard for a focused DOM text field, never for a canvas;
//  - fullscreen, through the site's window.EmbercladFullscreen when it exists.
mergeInto(LibraryManager.library, {
  EmbercladExitToSite: function () {
    try {
      if (typeof window.EmbercladExit === 'function') { window.EmbercladExit(); return; }
      if (window.parent && window.parent !== window && typeof window.parent.EmbercladExit === 'function') { window.parent.EmbercladExit(); return; }
    } catch (e) { console.warn('EmbercladExit failed', e); }
    window.location.reload();
  },

  // 1 on phones and tablets. ?touch=1 / ?touch=0 on the page URL forces it either way.
  EmbercladIsTouch: function () {
    try {
      var q = (window.location.search || '').match(/[?&]touch=([01])/);
      if (q) return q[1] === '1' ? 1 : 0;
      var mm = window.matchMedia;
      if (mm && mm('(pointer: coarse)').matches) return 1;
      return (navigator.maxTouchPoints > 0 && !(mm && mm('(pointer: fine)').matches)) ? 1 : 0;
    } catch (e) { return 0; }
  },

  // x, y, w, h: the field's rectangle as fractions of the canvas, measured from its top-left.
  EmbercladTextOpen: function (x, y, w, h, maxLen) {
    var S = window.__emberText || (window.__emberText = { el: null, ver: 0, val: '', submit: 0, rect: null });
    if (!S.el) {
      var el = document.createElement('input');
      el.type = 'text';
      el.setAttribute('autocomplete', 'off');
      el.setAttribute('autocorrect', 'off');
      el.setAttribute('autocapitalize', 'none');
      el.setAttribute('spellcheck', 'false');
      el.setAttribute('enterkeyhint', 'go');
      el.setAttribute('aria-label', 'Knight name');
      // Invisible but tappable: the game draws the field, the browser owns the keyboard.
      // 16px stops iOS zooming the page when the field takes focus.
      el.style.cssText = 'position:absolute;z-index:60;margin:0;padding:0;border:0;outline:none;box-shadow:none;' +
        'background:transparent;color:transparent;caret-color:transparent;font-size:16px;text-align:center;' +
        '-webkit-appearance:none;appearance:none;-webkit-tap-highlight-color:transparent;-webkit-user-select:text;user-select:text;';
      el.addEventListener('input', function () { S.val = el.value; S.ver++; });
      // Keys typed here belong to the field, not to the game underneath.
      el.addEventListener('keydown', function (e) {
        e.stopPropagation();
        if (e.key === 'Enter') { e.preventDefault(); S.submit = 1; el.blur(); }
      });
      el.addEventListener('keyup', function (e) { e.stopPropagation(); });
      el.addEventListener('keypress', function (e) { e.stopPropagation(); });
      S.el = el;
      S.place = function () {
        var r = S.rect; if (!r || !S.el.parentElement) return;
        var canvas = Module.canvas, host = S.el.parentElement;
        var cr = canvas.getBoundingClientRect(), hr = host.getBoundingClientRect();
        var st = S.el.style;
        st.left = (cr.left - hr.left - host.clientLeft + r[0] * cr.width) + 'px';
        st.top = (cr.top - hr.top - host.clientTop + r[1] * cr.height) + 'px';
        st.width = Math.max(1, r[2] * cr.width) + 'px';
        st.height = Math.max(1, r[3] * cr.height) + 'px';
      };
    }
    var el2 = S.el;
    el2.maxLength = maxLen > 0 ? maxLen : 524288;
    el2.value = ''; S.val = ''; S.ver++; S.submit = 0;
    // Inside the canvas's parent, so it stays visible when that element is fullscreen.
    var host = Module.canvas.parentElement || document.body;
    if (window.getComputedStyle(host).position === 'static') host.style.position = 'relative';
    if (el2.parentElement !== host) host.appendChild(el2);
    S.rect = [x, y, w, h]; S.place();
  },

  EmbercladTextPlace: function (x, y, w, h) {
    var S = window.__emberText; if (!S || !S.el) return;
    S.rect = [x, y, w, h]; S.place();
  },

  EmbercladTextClose: function () {
    var S = window.__emberText; if (!S || !S.el) return;
    try { S.el.blur(); } catch (e) {}
    if (S.el.parentElement) S.el.parentElement.removeChild(S.el);
    S.submit = 0;
  },

  EmbercladTextVersion: function () { var S = window.__emberText; return S ? S.ver : 0; },

  EmbercladTextValue: function () {
    var s = (window.__emberText && window.__emberText.val) || '';
    var n = lengthBytesUTF8(s) + 1, b = _malloc(n);
    stringToUTF8(s, b, n);
    return b;
  },

  // The game filters what was typed (allowed characters, length) and writes the result back.
  EmbercladTextSet: function (p) {
    var S = window.__emberText; if (!S || !S.el) return;
    var s = UTF8ToString(p);
    S.val = s;
    if (S.el.value !== s) S.el.value = s;
  },

  EmbercladTextTakeSubmit: function () {
    var S = window.__emberText; if (!S) return 0;
    var v = S.submit; S.submit = 0; return v;
  },

  EmbercladTextFocused: function () {
    var S = window.__emberText;
    return (S && S.el && document.activeElement === S.el) ? 1 : 0;
  },

  // Closing or hiding the page (tab closed, browser quit, phone locked or switched app). The game
  // saves the run here, because WebGL never gets OnApplicationQuit for a closed tab. 'hidden'
  // comes first and leaves time for the save to reach IndexedDB; 'pagehide' is the last chance.
  EmbercladOnPageLeave: function (callback) {
    // The site can tear the game down and start a new one, so always call the newest game's callback.
    window.__emberLeave = function () { try { {{{ makeDynCall('v', 'callback') }}}(); } catch (e) { console.warn('save on leave failed', e); } };
    if (window.__emberLeaveHooked) return;
    window.__emberLeaveHooked = true;
    var fire = function () { if (window.__emberLeave) window.__emberLeave(); };
    document.addEventListener('visibilitychange', function () { if (document.visibilityState === 'hidden') fire(); });
    window.addEventListener('pagehide', fire);
  },

  // 0 windowed, 1 fullscreen, 2 the browser cannot (iPhone Safari), 3 launched from the
  // home screen, which already has no browser chrome.
  EmbercladFullscreenState: function () {
    try {
      if (window.EmbercladFullscreen && window.EmbercladFullscreen.state) return window.EmbercladFullscreen.state();
      var d = document;
      if (d.fullscreenElement || d.webkitFullscreenElement) return 1;
      var el = Module.canvas;
      return (el.requestFullscreen || el.webkitRequestFullscreen) ? 0 : 2;
    } catch (e) { return 0; }
  },

  // Called from a button tap. Browsers allow fullscreen for a short while after a tap,
  // and the game handles the tap a frame later, well inside that window.
  EmbercladFullscreenToggle: function () {
    try {
      if (window.EmbercladFullscreen && window.EmbercladFullscreen.toggle) { window.EmbercladFullscreen.toggle(); return; }
      var d = document;
      if (d.fullscreenElement || d.webkitFullscreenElement) {
        var ex = d.exitFullscreen || d.webkitExitFullscreen; if (ex) ex.call(d);
        return;
      }
      var el = Module.canvas.parentElement || Module.canvas;
      var rq = el.requestFullscreen || el.webkitRequestFullscreen;
      if (rq) { var p = rq.call(el); if (p && p.catch) p.catch(function () {}); }
    } catch (e) { console.warn('fullscreen failed', e); }
  }
});
