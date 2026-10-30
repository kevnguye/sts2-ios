#!/usr/bin/env bash
# Stage a complete desktop save and upload its manifest before a completion marker.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
[ -f "$SCRIPT_DIR/config.sh" ] || { echo 'Missing config.sh' >&2; exit 1; }
source "$SCRIPT_DIR/config.sh"
source "$SCRIPT_DIR/save-transfer.sh"
: "${STS2_DEVICE_UDID:?Missing device}"
: "${STS2_BUNDLE_ID_SIGNED:?Missing bundle}"
: "${STS2_STEAM_ID:?Missing Steam ID}"
[[ "$STS2_STEAM_ID" =~ ^[0-9]+$ ]] || { echo 'Steam ID must be numeric' >&2; exit 1; }
pgrep -f SlayTheSpire2 >/dev/null 2>&1 && { echo 'Close the desktop game before transferring saves' >&2; exit 1; }
DESK="$HOME/Library/Application Support/SlayTheSpire2/steam/$STS2_STEAM_ID"
STAGE=$(mktemp -d "${TMPDIR:-/tmp}/sts2push.XXXXXX")
WORK="$SCRIPT_DIR/.work"
mkdir -p "$WORK"
mkdir "$WORK/save_sync.lock" 2>/dev/null || { rm -rf "$STAGE"; echo 'Another save transfer is running' >&2; exit 1; }
trap 'rm -rf "$STAGE"; rmdir "$WORK/save_sync.lock"' EXIT
python3 "$SCRIPT_DIR/../tools/save_manifest.py" recover "$DESK"
stage_and_upload_save "$DESK" "$STAGE" "$STS2_DEVICE_UDID" "$STS2_BUNDLE_ID_SIGNED"
