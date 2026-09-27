// NSF-Grant headset performance helpers for the WebXR build. Unity prepends
// this to its framework code, so it runs before the WebXR Export package
// starts a session. It wraps three WebXR API entry points the package
// already calls, so the package itself stays untouched:
//
//   1. Dynamic resolution - XRView.requestViewportScale before each
//      XRWebGLLayer.getViewport: each eye renders into a smaller part of
//      the same framebuffer and the headset compositor scales it up (no new
//      render targets, no extra pass). A governor lowers the scale in 5 %
//      steps when frames arrive late and creeps back up once they're on
//      time; the browser's recommendedViewportScale, if it offers one, can
//      only lower it further. Never below dynresmin, so exhibit text stays
//      legible.
//   2. Fixed foveation - XRWebGLLayer.fixedFoveation: the headset renders
//      the edges of each eye's view at lower resolution.
//   3. Adaptive frame rate - XRSession.updateTargetFrameRate. The display
//      runs at fixed rates (Quest 3: 72/80/90/120 Hz; there is no true
//      variable refresh - a late frame is re-shown, reprojected, as judder).
//      Sessions start at 90 Hz for smoothness; only when frames are still
//      late with the resolution already at its floor does the rate drop to
//      72 Hz (25 % more time per frame). After ~10 s with no late frames at
//      full resolution it tries 90 Hz again; each quick fall-back doubles
//      the wait before the next try (up to 5 min), so it can't flip-flop.
//      Switches are at least 10 s apart (each can cause a brief hitch).
//
// All three are feature-detected (no-ops where unsupported). URL switches
// for comparing on the headset without rebuilding:
//   ?dynres=0          dynamic resolution off
//   ?dynresmin=0.75    lowest viewport scale (0.5 - 1)
//   ?fov=0.5           foveation level 0 - 1 (0 = off)
//   ?hz=auto           adaptive 90/72 (default); a number fixes the rate,
//                      0 = browser default
// Live state is in window.nsfXrPerf, and while in VR a summary is logged
// every 5 s ("[XRPerf] ..."), visible over Quest remote debugging.
(function () {
  if (typeof window === 'undefined' || typeof XRSession === 'undefined' ||
      typeof XRWebGLLayer === 'undefined' || window.nsfXrPerf) {
    return;
  }
  var params = new URLSearchParams(window.location.search);
  function num(name, fallback) {
    var v = parseFloat(params.get(name));
    return isNaN(v) ? fallback : v;
  }
  var cfg = {
    dynres: params.get('dynres') !== '0',
    minScale: Math.min(1, Math.max(0.5, num('dynresmin', 0.75))),
    foveation: Math.min(1, Math.max(0, num('fov', 0.5))),
    // 'auto' (default) = adaptive; a number = fixed; 0 = leave the browser's.
    hz: (params.get('hz') || 'auto') === 'auto' ? 'auto' : num('hz', 0)
  };
  var state = window.nsfXrPerf = {
    config: cfg, scale: 1, recommended: null, targetHz: null, lateFraction: 0,
    foveation: null,        // what the layer actually accepted
    supportedHz: null,      // what the headset offers, e.g. [72, 80, 90, 120]
    highHz: null, lowHz: null
  };

  function effectiveScale() {
    if (!cfg.dynres) return 1;
    var s = state.scale;
    if (typeof state.recommended === 'number') {
      s = Math.min(s, Math.max(cfg.minScale, state.recommended));
    }
    return s;
  }

  // ---- adaptive frame rate state
  var session_ = null, switching = false, lastSwitchT = -1e9, raisedAtT = -1e9;
  var strugglingWindows = 0, fullCalmWindows = 0, retryDelayMs = 10000;

  // Switch times are taken from the next frame's timestamp (lastSwitchT =
  // null until then), so spacing is measured on the frame clock itself -
  // emulators don't always share performance.now()'s time origin.
  function switchRate(hz) {
    if (!session_ || switching || hz === state.targetHz) return;
    switching = true;
    var from = state.targetHz;
    session_.updateTargetFrameRate(hz).then(function () {
      state.targetHz = hz;
      console.log('[XRPerf] frame rate ' + from + ' -> ' + hz + ' Hz');
    }, function () {}).then(function () {
      switching = false;
      lastSwitchT = null;
      lastT = windowStart = 0;         // don't count the transition as late frames
      strugglingWindows = fullCalmWindows = 0;
    });
  }

  // Called once per half-second window with that window's late fraction.
  function adaptRate(late, t) {
    if (cfg.hz !== 'auto' || !state.highHz || !state.lowHz || state.highHz === state.lowHz || switching) return;
    var atFloor = !cfg.dynres || state.scale <= cfg.minScale + 1e-6;
    if (state.targetHz === state.highHz) {
      // Resolution can't go lower and frames are still late: drop the rate.
      strugglingWindows = atFloor && late > 0.05 ? strugglingWindows + 1 : 0;
      if (strugglingWindows >= 4 && t - lastSwitchT > 10000) {
        // Fell straight back after going up: wait longer before the next try.
        retryDelayMs = t - raisedAtT < 30000 ? Math.min(retryDelayMs * 2, 300000) : 10000;
        switchRate(state.lowHz);
      }
    } else if (state.targetHz === state.lowHz) {
      // Comfortable at full resolution: try the smoother rate again.
      fullCalmWindows = state.scale >= 1 && late === 0 ? fullCalmWindows + 1 : 0;
      if (fullCalmWindows * 500 >= retryDelayMs && t - lastSwitchT > 10000) {
        raisedAtT = t;
        switchRate(state.highHz);
      }
    }
  }

  // ---- governor: late frames per half-second window
  var lastT = 0, windowStart = 0, windowFrames = 0, windowLate = 0, calmWindows = 0, lastLog = 0;
  function onFrame(t) {
    if (lastSwitchT === null) lastSwitchT = t;
    var budget = 1000 / (state.targetHz || 72);
    if (lastT) {
      var dt = t - lastT;
      if (dt < 250) {                  // ignore pauses (headset off, tab hidden)
        windowFrames++;
        if (dt > budget * 1.5) windowLate++;
      }
    }
    lastT = t;
    if (!windowStart) windowStart = t;
    if (t - windowStart >= 500) {
      var late = windowFrames ? windowLate / windowFrames : 0;
      state.lateFraction = late;
      if (late > 0.05) {
        state.scale = Math.max(cfg.minScale, state.scale - 0.05);
        calmWindows = 0;
      } else if (late === 0 && ++calmWindows >= 4) {
        state.scale = Math.min(1, state.scale + 0.025);
        calmWindows = 0;
      }
      adaptRate(late, t);
      windowStart = t;
      windowFrames = 0;
      windowLate = 0;
    }
    if (t - lastLog > 5000) {
      lastLog = t;
      console.log('[XRPerf] scale=' + effectiveScale().toFixed(2) +
        ' late=' + Math.round(state.lateFraction * 100) + '%' +
        ' hz=' + state.targetHz + ' fov=' + state.foveation +
        (typeof state.recommended === 'number' ? ' recommended=' + state.recommended.toFixed(2) : ''));
    }
  }

  // ---- 3. frame rate: pick the rates once per session
  var configured = typeof WeakSet === 'function' ? new WeakSet() : null;
  function configureSession(session) {
    if (!configured || configured.has(session)) return;
    configured.add(session);
    session_ = null;
    state.scale = 1;
    lastT = windowStart = calmWindows = 0;
    strugglingWindows = fullCalmWindows = 0;
    retryDelayMs = 10000;
    // Count session start as a switch: no rate change in the first 10 s,
    // which often stutter while the scene settles.
    lastSwitchT = null;
    if (typeof session.frameRate === 'number') state.targetHz = session.frameRate;
    if (typeof session.updateTargetFrameRate !== 'function' || !session.supportedFrameRates) return;

    var rates = Array.prototype.slice.call(session.supportedFrameRates).sort(function (a, b) { return a - b; });
    state.supportedHz = rates;
    session_ = session;
    if (cfg.hz === 'auto') {
      // High: 90 if offered, else the fastest at or below 90. Low: 72 if
      // offered, else the slowest. 120 Hz is left alone - it halves the
      // per-frame budget for little gain in a museum walk-through.
      var high = rates.indexOf(90) >= 0 ? 90 : rates.filter(function (r) { return r <= 90; }).pop();
      var low = rates.indexOf(72) >= 0 ? 72 : rates[0];
      state.highHz = high || null;
      state.lowHz = low || null;
      if (state.highHz) switchRate(state.highHz);
    } else if (cfg.hz > 0 && rates.indexOf(cfg.hz) >= 0) {
      switchRate(cfg.hz);
    }
    console.log('[XRPerf] session rates=' + rates.join(',') + ' mode=' +
      (cfg.hz === 'auto' ? 'auto ' + state.lowHz + '-' + state.highHz : cfg.hz || 'browser default'));
  }

  var originalRaf = XRSession.prototype.requestAnimationFrame;
  XRSession.prototype.requestAnimationFrame = function (callback) {
    var session = this;
    configureSession(session);
    return originalRaf.call(session, function (t, frame) {
      if (typeof session.frameRate === 'number') state.targetHz = session.frameRate;
      onFrame(t);
      return callback(t, frame);
    });
  };

  // ---- 2. foveation, whenever a base layer is installed
  var originalUpdateRenderState = XRSession.prototype.updateRenderState;
  XRSession.prototype.updateRenderState = function (renderState) {
    var result = originalUpdateRenderState.apply(this, arguments);
    var layer = renderState && renderState.baseLayer;
    if (layer && 'fixedFoveation' in layer) {
      try { layer.fixedFoveation = cfg.foveation; } catch (e) { /* read-only on some UAs */ }
      state.foveation = layer.fixedFoveation;
    }
    return result;
  };

  // ---- 1. dynamic resolution: request the scale before each viewport read
  var originalGetViewport = XRWebGLLayer.prototype.getViewport;
  XRWebGLLayer.prototype.getViewport = function (view) {
    if (view && typeof view.requestViewportScale === 'function') {
      if (typeof view.recommendedViewportScale === 'number') {
        state.recommended = view.recommendedViewportScale;
      }
      view.requestViewportScale(effectiveScale());
    }
    return originalGetViewport.call(this, view);
  };
})();
