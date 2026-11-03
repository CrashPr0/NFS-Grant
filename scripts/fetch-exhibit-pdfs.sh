#!/usr/bin/env bash
# Downloads every PDF the exhibits link to into Assets/StreamingAssets/exhibit-pdfs/
# so the in-VR PDF viewer can read them same-origin (no CORS needed).
# Check each publisher's reuse terms before shipping the result.
set -euo pipefail
cd "$(dirname "$0")/.."
out=Assets/StreamingAssets/exhibit-pdfs
mkdir -p "$out"
grep -o -E 'https?://[^" ]+\.pdf' Assets/Scripts/Content/SdgContentLibrary.cs | sort -u |
while read -r url; do
  name=$(python3 -c 'import sys,urllib.parse as u;print(u.unquote(u.urlparse(sys.argv[1]).path.rsplit("/",1)[-1]))' "$url")
  echo "-> $name"
  curl -fL --retry 2 -o "$out/$name" "$url" || echo "   FAILED: $url"
done
