#!/usr/bin/env bash
# Upload StS2.pck once to the installed app's Documents/StS2.pck.
#
# The split-content architecture keeps the repeatedly signed app small:
# - The original slim app was ~165MB (~55MB IPA), with executable/frameworks/Info.plist and no pack.
# - SideStore processes that smaller package instead of loading a ~2GB bundle into memory.
# - Upload the original ~1.77GB content pack to Documents separately; it is outside the signed package.
# - Same-bundle updates/refreshes should preserve Documents and its pack.
# Run again when game content changes; do not assume every version preserves the same signing behavior.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
[ -f "$SCRIPT_DIR/config.sh" ] || { echo "❌ Missing ios-export/config.sh; copy config.example.sh to config.sh and fill it in first" >&2; exit 1; }
# shellcheck disable=SC1091
source "$SCRIPT_DIR/config.sh"
: "${STS2_DEVICE_UDID:?config.sh does not set STS2_DEVICE_UDID}"
: "${STS2_BUNDLE_ID_SIGNED:?config.sh does not set STS2_BUNDLE_ID_SIGNED (installed app bundle ID; SideStore signing may add a team suffix)}"

DEV="$STS2_DEVICE_UDID"
BUNDLE="$STS2_BUNDLE_ID_SIGNED"
STAGE=$(mktemp -d "${TMPDIR:-/tmp}/sts2pck.XXXXXX") || exit 1
trap 'rm -rf "$STAGE"' EXIT
PCK="${1:-$SCRIPT_DIR/build/StS2.pck}"      # Default to build-ios.sh's pack, or use an explicitly supplied path.
fail(){ echo "❌ $1"; exit 1; }

[ -f "$PCK" ] || fail "Content pack missing: $PCK (run build-ios.sh to generate build/StS2.pck first)"
[ -n "$DEV" ] || fail "STS2_DEVICE_UDID is empty (get it with xcrun devicectl list devices)"

# Install the slim app with SideStore or Xcode first so its container exists.
echo "▶ Target container: $BUNDLE Documents/ (device $DEV)"
echo "▶ Uploading content pack $(du -h "$PCK" | cut -f1) → Documents/StS2.pck  (USB; a ~1.77GB pack can take several minutes; keep the cable connected)…"

# Copy file-to-file into the app sandbox with devicectl; specify the complete destination filename.
if ! xcrun devicectl device copy to --device "$DEV" \
      --domain-type appDataContainer --domain-identifier "$BUNDLE" \
      --source "$PCK" --destination "Documents/StS2.pck" 2>"$STAGE/pushpck.err"; then
  cat "$STAGE/pushpck.err" >&2
  echo "── If the container is missing, install the slim app with SideStore or Xcode first."
  echo "── If devicectl fails because of device/pairing issues, use Finder: connect -> iPhone -> Files ->"
  echo "   Find the app -> drag StS2.pck into its documents (keep the filename StS2.pck)."
  fail "Upload failed"
fi

# Verify that Documents/StS2.pck exists; a successful command alone is not sufficient evidence.
if xcrun devicectl device info files --device "$DEV" \
     --domain-type appDataContainer --domain-identifier "$BUNDLE" 2>/dev/null \
     | grep -q "Documents/StS2.pck"; then
  echo "✅ Content pack ready: $BUNDLE : Documents/StS2.pck"
  echo "   The app should now load user://StS2.pck via godot_cmdline in Info.plist."
else
  fail "The upload command succeeded, but Documents/StS2.pck was not found; retry or copy it with Finder"
fi
