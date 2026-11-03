# Mirrored exhibit PDFs (optional)

The in-VR PDF viewer (WebXR) tries a copy of the PDF in this folder first,
then the original URL. Most publishers' servers don't send the CORS header a
browser needs to let the page read the file, so the original usually fails;
a same-origin copy here always works.

Fill it with `scripts/fetch-exhibit-pdfs.sh`, THEN rebuild the web build.
The PDFs are not committed (see .gitignore): confirm each publisher's reuse
terms before the build that hosts them goes to participants. A PDF with no
copy here (and no CORS on the original) shows "can't be shown in VR" with a
button that leaves VR and opens it in the browser reader instead.

File names are the original URL's last segment, URL-decoded.
