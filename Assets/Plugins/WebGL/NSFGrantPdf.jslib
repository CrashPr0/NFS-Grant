// Draws PDF pages onto a Unity texture so a PDF can be read INSIDE a WebXR
// headset session (the browser can't show a PDF viewer there; see
// BrowserTab). pdf.js renders a page to an offscreen 2D canvas; C# polls
// NSFGrantPdfPageReady each frame and, when a page is ready, calls
// NSFGrantPdfUpload from inside Unity's own frame so the GL texture is only
// touched from the render loop.
//
// pdf.js (StreamingAssets/pdfjs) is fetched on first use and loaded from blob
// URLs, so it works however the host serves .mjs files. The PDF is read from
// StreamingAssets/exhibit-pdfs/<mirror name> first (same origin), then from
// its original URL (which needs the publisher's CORS header).
//
// State (NSFGrantPdfState): 0 idle, 1 loading, 2 ready, 3 error.
mergeInto(LibraryManager.library, {
  NSFGrantPdfOpen: function (urlPtr, mirrorPtr, texW, texH) {
    var url = UTF8ToString(urlPtr);
    var mirror = UTF8ToString(mirrorPtr);
    var S = window.nsfPdf = window.nsfPdf || { token: 0, state: 0 };
    var base = (typeof Module !== 'undefined' && Module.streamingAssetsUrl) ? Module.streamingAssetsUrl : 'StreamingAssets';

    function dispose() {
      if (S.task) { try { S.task.cancel(); } catch (e) {} }
      if (S.doc) { try { S.doc.destroy(); } catch (e) {} }
      S.task = null; S.doc = null;
    }
    dispose();
    S.token++;
    var token = S.token;
    S.state = 1; S.pages = 0; S.page = 0; S.ready = 0; S.err = '';
    S.zoom = 1; S.cx = 0.5; S.cy = 0; S.pw = 0; S.ph = 0; // view: zoom, centre in page fractions
    S.w = texW; S.h = texH;
    S.canvas = document.createElement('canvas');
    S.canvas.width = texW; S.canvas.height = texH;

    // keepView: re-draw the same page after a zoom/pan; otherwise a new page
    // starts at its top, centred, at the current zoom.
    S.render = function (n, keepView) {
      S.page = n; S.ready = 0;
      if (!keepView) { S.cx = 0.5; S.cy = 0; }
      var prev = Promise.resolve();
      if (S.task) { try { S.task.cancel(); } catch (e) {} prev = S.task.promise.catch(function () {}); }
      prev.then(function () {
        if (token !== S.token || S.page !== n || !S.doc) return;
        return S.doc.getPage(n).then(function (page) {
          if (token !== S.token || S.page !== n) return;
          var v1 = page.getViewport({ scale: 1 });
          var fit = Math.min(S.w / v1.width, S.h / v1.height);
          var vp = page.getViewport({ scale: fit * S.zoom });
          // Keep the view inside the page; a page smaller than the texture
          // along an axis stays centred on it.
          S.pw = vp.width; S.ph = vp.height;
          var hx = S.w / 2 / S.pw, hy = S.h / 2 / S.ph;
          S.cx = S.pw <= S.w ? 0.5 : Math.min(Math.max(S.cx, hx), 1 - hx);
          S.cy = S.ph <= S.h ? 0.5 : Math.min(Math.max(S.cy, hy), 1 - hy);
          var ctx = S.canvas.getContext('2d');
          ctx.setTransform(1, 0, 0, 1, 0, 0);
          ctx.fillStyle = '#ffffff';
          ctx.fillRect(0, 0, S.w, S.h);
          // Put the view centre (cx, cy) mid-texture; pdf.js applies this before the viewport transform.
          S.task = page.render({
            canvasContext: ctx, viewport: vp,
            transform: [1, 0, 0, 1, S.w / 2 - S.cx * S.pw, S.h / 2 - S.cy * S.ph]
          });
          return S.task.promise.then(function () {
            if (token === S.token && S.page === n) S.ready = 1;
          });
        });
      }).catch(function (e) {
        if (e && e.name === 'RenderingCancelledException') return;
        if (token === S.token) { S.state = 3; S.err = String(e); }
      });
    };

    function loadLib() {
      if (S.lib) return S.lib;
      function blobUrl(file) {
        return fetch(base + '/pdfjs/' + file).then(function (r) {
          if (!r.ok) throw new Error(file + ' ' + r.status);
          return r.text();
        }).then(function (text) {
          return URL.createObjectURL(new Blob([text], { type: 'text/javascript' }));
        });
      }
      var dynImport = new Function('u', 'return import(u)');
      S.lib = Promise.all([blobUrl('pdf.min.mjs'), blobUrl('pdf.worker.min.mjs')]).then(function (u) {
        return dynImport(u[0]).then(function (lib) {
          lib.GlobalWorkerOptions.workerSrc = u[1];
          return lib;
        });
      });
      S.lib.catch(function () { S.lib = null; }); // retry next time
      return S.lib;
    }

    loadLib().then(function (lib) {
      var candidates = [];
      if (mirror) candidates.push(base + '/exhibit-pdfs/' + encodeURIComponent(mirror));
      candidates.push(url);
      return candidates.reduce(function (chain, u) {
        return chain.catch(function () { return lib.getDocument({ url: u }).promise; });
      }, Promise.reject(new Error('start')));
    }).then(function (doc) {
      if (token !== S.token) { try { doc.destroy(); } catch (e) {} return; }
      S.doc = doc; S.pages = doc.numPages; S.state = 2;
      S.render(1);
    }).catch(function (e) {
      if (token === S.token) { S.state = 3; S.err = String(e); }
    });
  },

  NSFGrantPdfState: function () { return window.nsfPdf ? (window.nsfPdf.state | 0) : 0; },
  NSFGrantPdfPageCount: function () { return window.nsfPdf ? (window.nsfPdf.pages | 0) : 0; },
  NSFGrantPdfPageReady: function () { return window.nsfPdf && window.nsfPdf.ready ? 1 : 0; },

  NSFGrantPdfGoTo: function (n) {
    var S = window.nsfPdf;
    if (S && S.doc && n >= 1 && n <= S.pages) S.render(n);
  },

  // Zoom (1 = whole page) about the current view centre; re-draws the page.
  NSFGrantPdfSetZoom: function (z) {
    var S = window.nsfPdf;
    if (S && S.doc && z >= 1) { S.zoom = z; S.render(S.page, true); }
  },

  // Moves the view by half a window per step (dx, dy in -1, 0, 1; y down).
  NSFGrantPdfPan: function (dx, dy) {
    var S = window.nsfPdf;
    if (!S || !S.doc || !S.pw || !S.ph) return;
    S.cx += dx * 0.5 * S.w / S.pw;
    S.cy += dy * 0.5 * S.h / S.ph;
    S.render(S.page, true);
  },

  // Copies the rendered page into the Unity texture (RGBA32, texW x texH).
  // texSubImage2D keeps Unity's own allocation; flipY because Unity's
  // texture origin is bottom-left.
  NSFGrantPdfUpload: function (texId) {
    var S = window.nsfPdf;
    if (!S || !S.ready || !S.canvas) return 0;
    var tex = GL.textures[texId];
    if (!tex) return 0;
    GLctx.bindTexture(GLctx.TEXTURE_2D, tex);
    GLctx.pixelStorei(GLctx.UNPACK_FLIP_Y_WEBGL, true);
    GLctx.texSubImage2D(GLctx.TEXTURE_2D, 0, 0, 0, GLctx.RGBA, GLctx.UNSIGNED_BYTE, S.canvas);
    GLctx.pixelStorei(GLctx.UNPACK_FLIP_Y_WEBGL, false);
    GLctx.bindTexture(GLctx.TEXTURE_2D, null);
    S.ready = 0;
    return 1;
  },

  NSFGrantPdfClose: function () {
    var S = window.nsfPdf;
    if (!S) return;
    S.token++;
    if (S.task) { try { S.task.cancel(); } catch (e) {} }
    if (S.doc) { try { S.doc.destroy(); } catch (e) {} }
    S.task = null; S.doc = null; S.canvas = null; S.state = 0; S.ready = 0;
  }
});
