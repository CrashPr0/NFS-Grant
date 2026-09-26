#!/usr/bin/env bash
# Publish the WebXR build (Builds/WebXR) to GitHub Pages.
#
# Usage:
#   ./scripts/unity-tasks.sh build-webxr   # (or build it from the editor)
#   ./scripts/deploy-pages.sh              # push Builds/WebXR -> gh-pages
#
# How it works: the build is copied into a temporary worktree of the
# `gh-pages` branch (created as an orphan branch on first run), committed
# with your normal git identity, and pushed. Your current branch and
# working tree are never touched. GitHub Pages serves the branch root at
#   https://<owner>.github.io/<repo>/
#
# WebXR needs a secure context; Pages is HTTPS, so "Enter VR" works in the
# Quest browser. The build is uncompressed on purpose (Pages can't send the
# Content-Encoding headers Unity's gzip/brotli output needs; it gzips on
# the wire anyway) - see CiTools.ConfigureWebXR.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD="$ROOT/Builds/WebXR"
BRANCH="gh-pages"
REMOTE="${REMOTE:-origin}"

if [[ ! -f "$BUILD/index.html" ]]; then
  echo "ERROR: $BUILD/index.html not found - run ./scripts/unity-tasks.sh build-webxr first." >&2
  exit 1
fi

# Refuse files over GitHub's 100 MB hard limit before doing anything.
big="$(find "$BUILD" -type f -size +95M)"
if [[ -n "$big" ]]; then
  echo "ERROR: files too large for GitHub (100 MB limit):" >&2
  echo "$big" >&2
  exit 1
fi

WT="$(mktemp -d)"
cleanup() { git -C "$ROOT" worktree remove --force "$WT" >/dev/null 2>&1 || true; rm -rf "$WT"; }
trap cleanup EXIT

git -C "$ROOT" fetch "$REMOTE" "$BRANCH" >/dev/null 2>&1 || true
if git -C "$ROOT" show-ref --verify --quiet "refs/remotes/$REMOTE/$BRANCH"; then
  git -C "$ROOT" worktree add --force -B "$BRANCH" "$WT" "$REMOTE/$BRANCH" >/dev/null
else
  # First deploy: orphan branch with no history from the main line.
  git -C "$ROOT" worktree add --force --detach "$WT" >/dev/null
  git -C "$WT" checkout --orphan "$BRANCH" >/dev/null
fi

# Replace the published site with the new build.
# (--cached -f: a fresh orphan branch starts with the source tree staged.)
git -C "$WT" rm -rqf --cached --ignore-unmatch . >/dev/null
find "$WT" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
cp -R "$BUILD"/. "$WT"/
touch "$WT/.nojekyll"   # serve files/folders as-is (no Jekyll processing)

SRC_SHA="$(git -C "$ROOT" rev-parse --short HEAD)"
git -C "$WT" add -A
if git -C "$WT" diff --cached --quiet; then
  echo "Nothing changed since the last deploy."
  exit 0
fi
git -C "$WT" commit -qm "Deploy WebXR build from $SRC_SHA"
git -C "$WT" push -q "$REMOTE" "$BRANCH"

OWNER_REPO="$(git -C "$ROOT" remote get-url "$REMOTE" | sed -E 's#(git@github.com:|https://github.com/)##; s#\.git$##')"
echo "Deployed $SRC_SHA -> $BRANCH"
echo "Site: https://${OWNER_REPO%%/*}.github.io/${OWNER_REPO#*/}/"
