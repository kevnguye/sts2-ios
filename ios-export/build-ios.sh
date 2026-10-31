#!/usr/bin/env bash
# One-command STS2 -> iPhone build. Fail clearly when a required input or step is missing.
# Signing identity, device and paths come from ios-export/config.sh; see config.example.sh.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
EXPORT_DIR="$SCRIPT_DIR"

# Load local configuration.
[ -f "$SCRIPT_DIR/config.sh" ] || { echo "❌ Missing ios-export/config.sh; copy config.example.sh to config.sh and fill it in first" >&2; exit 1; }
# shellcheck disable=SC1091
source "$SCRIPT_DIR/config.sh"
: "${STS2_TEAM_ID:?config.sh does not set STS2_TEAM_ID}"
: "${STS2_BUNDLE_ID:?config.sh does not set STS2_BUNDLE_ID}"
: "${STS2_GODOT_BIN:?config.sh does not set STS2_GODOT_BIN}"
: "${STS2_GAME_APP:?config.sh does not set STS2_GAME_APP}"
: "${STS2_IOS_LIBS_DIR:?config.sh does not set STS2_IOS_LIBS_DIR}"
: "${STS2_FMOD_STATIC_DIR:?config.sh does not set STS2_FMOD_STATIC_DIR}"

GODOT="$STS2_GODOT_BIN"
GAME="$STS2_GAME_APP"
GAME_DATA="$GAME/data_sts2_macos_arm64"
WORK="$SCRIPT_DIR/.work"                # Intermediate files, ignored by Git.
WOVEN="$WORK/sts2-woven.dll"            # Latest STS2Weaver output.
PREBUILT="$EXPORT_DIR/prebuilt"
mkdir -p "$WORK"
DOTNET_ROOT_DIR="${STS2_DOTNET_ROOT:-$HOME/.dotnet}"   # .NET 9 installation directory; configurable in config.sh.
export PATH="$DOTNET_ROOT_DIR:$PATH"
export DOTNET_ROOT="$DOTNET_ROOT_DIR"

fail(){ echo "❌ $1" >&2; exit 1; }
ok(){ echo "✅ $1"; }
step(){ echo; echo "▶ $1"; }

step "0/6 Check prerequisites"
[ -x "$GODOT" ] || fail "Godot executable missing: $GODOT (set STS2_GODOT_BIN in config.sh to Godot 4.5.1 mono)"
command -v dotnet >/dev/null || fail "dotnet is not in PATH (install the .NET 9 SDK)"
[ -d "$GAME_DATA" ] || fail "Game data directory missing (install the Steam game or correct its path): $GAME_DATA"
TEMPLATES="$HOME/Library/Application Support/Godot/export_templates/4.5.1.stable.mono"
[ -d "$TEMPLATES" ] || fail "iOS export templates missing at $TEMPLATES (import templates.tpz in Godot first)"
[ -d "$STS2_IOS_LIBS_DIR/libspine_godot.ios.template_release.framework" ] || fail "Spine iOS library missing from $STS2_IOS_LIBS_DIR (obtain it separately; see README)"
[ -d "$STS2_IOS_LIBS_DIR/libGodotFmod.ios.template_release.xcframework" ] || fail "FMOD iOS library missing from $STS2_IOS_LIBS_DIR (obtain it separately)"
[ -f "$STS2_FMOD_STATIC_DIR/libfmod_iphoneos.a" ] || fail "FMOD static library missing from $STS2_FMOD_STATIC_DIR (obtain it separately)"
[ -f "$STS2_FMOD_STATIC_DIR/libfmodstudio_iphoneos.a" ] || fail "FMOD Studio static library missing from $STS2_FMOD_STATIC_DIR"
[ -f "$GAME_DATA/sts2.dll" ] && [ -f "$GAME_DATA/GodotSharp.dll" ] || fail "Game managed assemblies are missing"
# References stay local and ignored. Read the installed game without modifying it.
mkdir -p "$ROOT/game-refs"
cp "$GAME_DATA/sts2.dll" "$GAME_DATA/GodotSharp.dll" "$ROOT/game-refs/" || fail "Could not prepare local game references"
ok "Prerequisites found"

step "1/6 Weave patches into the original game assembly"
cd "$ROOT" || fail "Could not change directory"
# Read the original assembly in place so Cecil can resolve its sibling DLLs.
# The weaver opens it read-only and writes only to the separate WOVEN path.
dotnet build src/STS2MobileIos -c Release -o "$WORK/mobilepatch-out" >"$WORK/patch-build.log" 2>&1 \
  || fail "Patch-library build failed; see $WORK/patch-build.log"
dotnet run --project src/STS2Weaver -c Release -- \
  "$GAME_DATA/sts2.dll" "$WORK/mobilepatch-out/STS2MobileIos.dll" \
  src/STS2MobileIos/manifest.json "$WOVEN" 2>&1 | tee "$WORK/weave.log" | grep -E "\[FAIL\]|Complete"
WEAVERC=${PIPESTATUS[0]}
[ "$WEAVERC" = "0" ] || fail "Weaver exited with code $WEAVERC; see $WORK/weave.log"
grep -q "Complete" "$WORK/weave.log" || fail "Weaving failed; see $WORK/weave.log"
ok "Weaving complete: $WOVEN"

step "2/6 Assemble prebuilt/ (woven assembly and managed dependencies)"
rm -rf "$PREBUILT"; mkdir -p "$PREBUILT/deps"
cp "$WOVEN" "$PREBUILT/sts2.dll" || fail "Could not copy the woven DLL"
cp "$WORK/mobilepatch-out/STS2MobileIos.dll" "$PREBUILT/deps/" || fail "Could not copy the patch DLL"
# Copy third-party managed dependencies only, using the installed runtime directory as the authoritative framework list.
# Exclude files present in shared/Microsoft.NETCore.App; the NativeAOT runtime pack provides them.
# Retain other libraries, including NuGet packages such as System.IO.Hashing whose names resemble framework assemblies.
# Also exclude sts2.dll (injected separately) and GodotSharp.dll (provided by the Godot SDK).
SHARED_FW=$(find "$DOTNET_ROOT_DIR/shared/Microsoft.NETCore.App" -maxdepth 1 -type d -name "9.0.*" | sort -V | tail -1)
[ -d "$SHARED_FW" ] || fail "Cannot find the .NET shared framework at $DOTNET_ROOT_DIR/shared to classify dependencies. Set STS2_DOTNET_ROOT in config.sh to your installation"
for dll in "$GAME_DATA"/*.dll; do
  base=$(basename "$dll")
  case "$base" in sts2.dll|GodotSharp.dll) continue;; esac
  [ -f "$SHARED_FW/$base" ] && continue   # Provided by the framework; skip it.
  cp "$dll" "$PREBUILT/deps/$base"
done
DEPCOUNT=$(ls "$PREBUILT/deps" | wc -l | tr -d ' ')
ok "Dependencies ready: $DEPCOUNT third-party managed assemblies (framework libraries excluded)"
echo "  Retained: $(ls "$PREBUILT/deps" | tr '\n' ' ')"

step "3/6 Prepare to copy third-party libraries into the private export directory"
ok "iOS library paths verified; step 5 prepares the private export driver"

step "4/6 Publish NativeAOT (woven assembly -> sts2.framework)"
cd "$EXPORT_DIR" || fail "Could not change directory"
dotnet publish sts2.csproj -c ExportRelease -r ios-arm64 \
  -p:GodotTargetPlatform=ios --self-contained \
  2>&1 | tee "$WORK/publish.log" | grep -E "error CS|error MSB|error :|Inject|-> " | tail -20
# Use the real exit code and output file; trimming warnings can contain the word error without indicating build failure.
PUBRC=${PIPESTATUS[0]}
PUB_DIR="$EXPORT_DIR/.godot/mono/temp/bin/ExportRelease/ios-arm64/publish"
[ "$PUBRC" = "0" ] || fail "dotnet publish exited with code $PUBRC; see $WORK/publish.log"
[ -f "$PUB_DIR/sts2.dylib" ] || fail "NativeAOT did not produce sts2.dylib; see $WORK/publish.log"
file "$PUB_DIR/sts2.dylib" | grep -q "arm64" || fail "sts2.dylib is not arm64"
ok "sts2.framework AOT compilation complete: $(du -h "$PUB_DIR/sts2.dylib" | cut -f1) arm64 native library"

step "5/6 Export the iOS project with Godot"
mkdir -p "$EXPORT_DIR/build"
DRIVER=$(python3 "$ROOT/tools/prepare_ios_export.py" \
  --repo "$ROOT" --game "$GAME" --libraries "$STS2_IOS_LIBS_DIR" \
  --fmod "$STS2_FMOD_STATIC_DIR" --team "$STS2_TEAM_ID" --bundle "$STS2_BUNDLE_ID") \
  || fail "Private export-driver preparation failed"
# Export the Xcode project; --main-pack is a runtime argument, not an export argument.
# Step 5.5 replaces the exported pack with the prepared game content.
"$GODOT" --headless --path "$DRIVER" \
  --export-release "iOS" "$EXPORT_DIR/build/StS2.ipa" 2>&1 | tee "$WORK/export.log" | tail -8
EXPORTRC=${PIPESTATUS[0]}
[ "$EXPORTRC" = "0" ] || fail "Godot export failed (exit code $EXPORTRC); see $WORK/export.log"
[ -d "$EXPORT_DIR/build/StS2.xcodeproj" ] || fail "Godot did not generate an Xcode project; see $WORK/export.log"
ok "Xcode project generated"

step "5.15/6 Configure Development signing, native audio linking and Increased Memory Limit"
python3 "$ROOT/tools/finalize_ios_project.py" "$EXPORT_DIR/build" \
  --team "$STS2_TEAM_ID" --bundle "$STS2_BUNDLE_ID" || fail "Xcode project configuration failed"
ok "Xcode project configuration complete"

step "5.2/6 Add the empty FMOD plugin-loader implementation to regenerated dummy.cpp"
# Static FMOD requires load_all_fmod_plugins, normally generated by the editor plugin rather than command-line export.
# The game uses no custom FMOD plugins; an empty loader avoids a null initialization call.
DUMMY="$EXPORT_DIR/build/StS2/dummy.cpp"
if ! grep -q "load_all_fmod_plugins" "$DUMMY" 2>/dev/null; then
  cat >> "$DUMMY" <<'CPPEOF'

// [STS2-iOS-fix] Static FMOD requires load_all_fmod_plugins; no custom plugins are used, so return an empty list.
extern "C" __attribute__((visibility("default"))) __attribute__((used))
unsigned int *load_all_fmod_plugins(void *p_interface, unsigned int *r_count) {
  if (r_count) { *r_count = 0; }
  return (unsigned int *)0;
}
CPPEOF
fi
grep -q "load_all_fmod_plugins" "$DUMMY" && ok "Empty FMOD plugin loader added" || fail "Adding the FMOD plugin loader failed"

step "5.3/6 Write increased-memory entitlements for memory-intensive loading and warmup"
# The original author reported this entitlement with free signing; this fork has not verified that route.
# The provisioning profile must include it; codesign with an incompatible old profile can fail installation verification.
ENT="$WORK/ent_mem.plist"
cat > "$ENT" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>application-identifier</key><string>${STS2_TEAM_ID}.${STS2_BUNDLE_ID}</string>
<key>com.apple.developer.team-identifier</key><string>${STS2_TEAM_ID}</string>
<key>get-task-allow</key><true/>
<key>com.apple.developer.kernel.increased-memory-limit</key><true/>
</dict></plist>
EOF
ok "Memory entitlements ready: $ENT"

step "5.5/6 Prepare the game pack (remove Sentry and add script placeholders)"
# The private MegaDot pack needs two adjustments for the official engine's script/extension handling:
# 1. Remove sentry.gdextension, which has no matching iOS library in this route.
# 2. Add placeholder .cs resources so scene script paths can resolve to the precompiled types.
GAME_PCK="$GAME/Slay the Spire 2.pck"
python3 "$ROOT/tools/pck_patch_sentry.py" "$GAME_PCK" "$WORK/pck_nosentry.pck" >"$WORK/pck1.log" 2>&1 \
  || fail "Removing Sentry from the pack failed; see $WORK/pck1.log"
strings "$PREBUILT/sts2.dll" | grep -oE 'res://[A-Za-z0-9_/.]+\.cs' | sort -u > "$WORK/cs_paths.txt"
CSN=$(wc -l < "$WORK/cs_paths.txt" | tr -d ' ')
python3 "$ROOT/tools/pck_add_cs.py" "$WORK/pck_nosentry.pck" "$WORK/cs_paths.txt" "$EXPORT_DIR/build/StS2.pck" >"$WORK/pck2.log" 2>&1 \
  || fail "Adding script placeholders to the pack failed; see $WORK/pck2.log"
GOTCS=$(python3 "$ROOT/tools/pck_ls.py" "$EXPORT_DIR/build/StS2.pck" ls 2>/dev/null | grep -c '\.cs$')
GOTSENT=$(python3 "$ROOT/tools/pck_ls.py" "$EXPORT_DIR/build/StS2.pck" ls 2>/dev/null | grep -c 'sentry.gdextension')
[ "$GOTCS" -ge 600 ] || fail "Unexpected script-placeholder count: $GOTCS (expected ~$CSN)"
[ "$GOTSENT" = "0" ] || fail "Pack still contains Sentry: $GOTSENT"
ok "Game content pack ready: $(du -h "$EXPORT_DIR/build/StS2.pck"|cut -f1), $GOTCS script placeholders, 0 sentry"

step "6/6 Open Xcode to sign and install"
cat <<GUIDE
Export ready: ios-export/build/StS2.xcodeproj

1. Open the project in Xcode and select your iPhone as the destination.
2. Under Signing & Capabilities, select your own Team and automatic signing.
3. Confirm Increased Memory Limit is present and supported by your profile.
4. Choose Product > Run. Complete Apple login or agreements yourself if prompted.
5. Install over the existing app using the same bundle ID to preserve its saves.

Do not uninstall or replace the app container. Keep game packs, app builds and
signing files private. See docs/BEGINNER_GUIDE.md for the menu paths,
renewal guidance and the Quick Start assistant prompt.
GUIDE
ok "Export complete; signing and physical-device testing are separate steps"
