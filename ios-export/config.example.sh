# STS2 iOS port: local configuration.
# ─────────────────────────────────────────────────────────────
# Usage: copy this file to config.sh and enter your own values.
#   cp ios-export/config.example.sh ios-export/config.sh
# config.sh is ignored by Git to keep signing details private.
# build-ios.sh, deploy-slim.sh and sts2_save_sync.sh source it automatically.
# ─────────────────────────────────────────────────────────────

# Apple signing identity (your own developer account; free signing needs separate verification).
# Team ID: Xcode -> Settings -> Accounts -> your account/team, or Apple Developer membership information.
STS2_TEAM_ID="XXXXXXXXXX"
# Choose your own bundle ID, preferably using a reverse-domain name you control.
STS2_BUNDLE_ID="com.yourname.sts2"
# Full codesign identity name. List available identities with: security find-identity -v -p codesigning
STS2_SIGN_IDENTITY="Apple Development: you@example.com (XXXXXXXXXX)"
# For SideStore only: signing may append a team suffix, such as ${STS2_BUNDLE_ID}.<SideStoreTeamID>.
# For Xcode/USB installation, keep STS2_BUNDLE_ID. Save-sync scripts use this to locate the app container.
STS2_BUNDLE_ID_SIGNED="${STS2_BUNDLE_ID}"

# Target device.
# Your iPhone UDID. List connected devices with: xcrun devicectl list devices
STS2_DEVICE_UDID=""

# Local paths.
# Game Contents/Resources directory. Adjust this default macOS Steam path as needed.
STS2_GAME_APP="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources"
# Path to the Godot 4.5.1 mono executable.
STS2_GODOT_BIN="$HOME/Godot_mono.app/Contents/MacOS/Godot"
# .NET 9 SDK directory; adjust ~/.dotnet if installed elsewhere, such as /usr/local/share/dotnet.
STS2_DOTNET_ROOT="$HOME/.dotnet"

# Third-party iOS libraries: official sources/versions are in docs/THIRD_PARTY_LIBS.md; no binaries are supplied here.
# Must contain libspine_godot.ios.template_release.framework (Spine 4.2 / Godot 4.5.1).
#            libGodotFmod.ios.template_release.xcframework    (FMOD 2.03 / Godot 4.5.1)
STS2_IOS_LIBS_DIR="$HOME/sts2-ios-userlibs/ios-libs"
# FMOD iOS static libraries: libfmod_iphoneos.a and libfmodstudio_iphoneos.a.
STS2_FMOD_STATIC_DIR="$HOME/sts2-ios-userlibs/fmod-ios/lib"

# Optional save synchronization, used by sts2_save_sync.sh only.
# Your SteamID64 (desktop save-directory name); find it in Steam/game account information or steamid.io.
STS2_STEAM_ID=""
# Divergent phone saves are retained as candidates by default, never mirrored with --delete.
# Set to 1 only after reviewing the candidate and choosing the phone as authoritative.
STS2_ALLOW_PHONE_PULL=0
# Likewise, scheduled desktop-to-phone sends require an explicit choice.
# push-save.sh remains the manual, deliberate desktop-to-phone transfer command.
STS2_ALLOW_DESKTOP_PUSH=0
