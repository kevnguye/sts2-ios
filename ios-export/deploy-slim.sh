#!/usr/bin/env bash
# Incremental install: replace sts2.dylib in a built app and remove its bundled pack.
# Add --main-pack user://StS2.pck, sign with memory entitlements, then install over USB while preserving Documents.
# The pack remains in Documents across same-bundle updates; this script does not upload it again.
# Requires a complete build-ios.sh and Xcode build first, leaving a built .app in DerivedData.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
[ -f "$SCRIPT_DIR/config.sh" ] || { echo "❌ Missing ios-export/config.sh; copy config.example.sh to config.sh and fill it in first" >&2; exit 1; }
# shellcheck disable=SC1091
source "$SCRIPT_DIR/config.sh"
: "${STS2_DEVICE_UDID:?config.sh does not set STS2_DEVICE_UDID}"
: "${STS2_SIGN_IDENTITY:?config.sh does not set STS2_SIGN_IDENTITY}"
: "${STS2_BUNDLE_ID:?config.sh does not set STS2_BUNDLE_ID}"

DEV="$STS2_DEVICE_UDID"
IDENT="$STS2_SIGN_IDENTITY"
# Find the newest built device app; DerivedData hashes vary, and either Debug or Release can be present.
APP=$(find "$HOME/Library/Developer/Xcode/DerivedData" -maxdepth 4 -type d -path "*-iphoneos/StS2.app" 2>/dev/null \
      | xargs -I{} stat -f '%m %N' {} 2>/dev/null | sort -rn | head -1 | cut -d' ' -f2-)
PUB="$SCRIPT_DIR/.godot/mono/temp/bin/ExportRelease/ios-arm64/publish/sts2.dylib"
STG="$(mktemp -d /tmp/sts2slim.XXXXXX)/StS2.app"
ENT="$SCRIPT_DIR/.work/ent_mem.plist"
fail(){ echo "❌ $1"; exit 1; }

[ -f "$PUB" ] || fail "New dylib missing: $PUB (run build-ios.sh first)"
[ -n "$APP" ] && [ -d "$APP" ] || fail "No built StS2.app in DerivedData (complete a build in Xcode first)"
[ -f "$ENT" ] || fail "Entitlements missing: $ENT (run build-ios.sh first)"
# 1. Replace the woven dylib in the app.
cp "$PUB" "$APP/Frameworks/sts2.framework/sts2" || fail "Replacing the dylib failed"
echo "✅ New dylib copied ($(shasum "$PUB" | cut -c1-12))"
# 2. Create a slim staging app.
mkdir -p "$(dirname "$STG")"; cp -R "$APP" "$STG"
rm -f "$STG/StS2.pck"
plutil -remove godot_cmdline "$STG/Info.plist" 2>/dev/null
plutil -insert godot_cmdline -json '["--main-pack", "user://StS2.pck"]' "$STG/Info.plist" || fail "Adding godot_cmdline failed"
echo "✅ Slim app $(du -sh "$STG" | cut -f1), Added --main-pack user://StS2.pck"
# 3. Sign it.
for fw in "$STG/Frameworks/"*.framework; do
  codesign -f -s "$IDENT" --generate-entitlement-der "$fw" >/dev/null 2>&1 || fail "Framework signing failed $fw"
done
codesign -f -s "$IDENT" --generate-entitlement-der --entitlements "$ENT" "$STG" >/dev/null 2>&1 || fail "App signing failed"
codesign -d --entitlements - "$STG" 2>/dev/null | grep -q increased-memory && echo "✅ Increased-memory entitlement embedded" || fail "Increased-memory entitlement missing"
codesign -vv "$STG" 2>&1 | grep -q "satisfies its Designated Requirement" && echo "✅ Signature valid"
# 4. Install while preserving Documents. Check the actual exit code; do not report success after an installation failure.
echo "▶ Install over USB (preserving Documents/StS2.pck and saves)..."
INSTALL_OUT=$(xcrun devicectl device install app --device "$DEV" "$STG" 2>&1)
RC=$?
echo "$INSTALL_OUT" | grep -iE "installationURL|error" | head -3
[ "$RC" = "0" ] || fail "Installation failed (exit code $RC)"
echo "$INSTALL_OUT" | grep -q "installationURL" || fail "Unexpected installation output (no installationURL)"
# Verify that the Documents pack remains; an update should preserve it, but a new container will not contain it.
if ! xcrun devicectl device info files --device "$DEV" \
     --domain-type appDataContainer --domain-identifier "$STS2_BUNDLE_ID" 2>/dev/null \
     | grep -q "Documents/StS2.pck"; then
  echo "⚠️ Warning: Documents/StS2.pck is missing (installation may have used a new container); upload the pack again!"
fi
echo "✅ Installation complete"
