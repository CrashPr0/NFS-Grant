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
//   3. Target frame rate - XRSession.updateTargetFrameRate: 72 Hz gives
//      each frame 25 % more time than 90 Hz; plenty for a slow museum walk.
//
// All three are feature-detected (no-ops where unsupported). URL switches
// for comparing on the headset without rebuilding:
//   ?dynres=0          dynamic resolution off
//   ?dynresmin=0.75    lowest viewport scale (0.5 - 1)
//   ?fov=0.5           foveation level 0 - 1 (0 = off)
//   ?hz=72             target frame rate (0 = browser default)
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
    hz: num('hz', 72)
  };
  var state = window.nsfXrPerf = {
    config: cfg, scale: 1, recommended: null, targetHz: null, lateFraction: 0,
    foveation: null   // what the layer actually accepted
  };

  function effectiveScale() {
    if (!cfg.dynres) return 1;
    var s = state.scale;
    if (typeof state.recommended === 'number') {
      s = Math.min(s, Math.max(cfg.minScale, state.recommended));
    }
    return s;
  }

  // ---- governor: late frames per half-second window
  var lastT = 0, windowStart = 0, windowFrames = 0, windowLate = 0, calmWindows = 0, lastLog = 0;
  function onFrame(t) {
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

  // ---- 3. frame rate, once per session; frame hook for the governor
  var configured = typeof WeakSet === 'function' ? new WeakSet() : null;
  function configureSession(session) {
    if (!configured || configured.has(session)) return;
    configured.add(session);
    state.scale = 1;
    lastT = windowStart = calmWindows = 0;
    if (typeof session.frameRate === 'number') state.targetHz = session.frameRate;
    if (cfg.hz > 0 && typeof session.updateTargetFrameRate === 'function' && session.supportedFrameRates) {
      var rates = Array.prototype.slice.call(session.supportedFrameRates);
      if (rates.indexOf(cfg.hz) >= 0) {
        session.updateTargetFrameRate(cfg.hz).then(function () { state.targetHz = cfg.hz; }, function () {});
      }
    }
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
