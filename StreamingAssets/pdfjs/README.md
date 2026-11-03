# pdf.js (vendored)

Mozilla pdf.js 4.10.38 (`pdfjs-dist`), Apache-2.0 (see LICENSE-pdfjs-Apache-2.0.txt).
Used by `NSFGrantPdf.jslib` to draw exhibit PDFs onto a texture inside a WebXR
headset session (the browser can't show a PDF viewer inside VR). Loaded lazily
the first time a PDF is opened in VR; not part of the initial download.
Update: `npm pack pdfjs-dist@<version>` and copy `build/pdf.min.mjs` and
`build/pdf.worker.min.mjs`.
