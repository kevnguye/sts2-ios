# Build your own Slay the Spire 2 iPhone app

This guide builds an unofficial personal iOS port from **your own Steam copy**.
The repository provides source tools and patches. It does not provide a game
download, prebuilt IPA, game DLL, asset pack, artwork, music or proprietary SDK.
This project is not affiliated with or endorsed by MegaCrit.

You need a Mac for this route. Expect the first setup to take a while: compiling
the audio/animation dependencies is the most technical part. A coding assistant
can help; a ready-to-paste prompt is at the bottom.

## Before starting

- Install and launch your purchased **macOS Steam version** of Slay the Spire 2.
- Have an iPhone, a USB data cable and an Apple Account for development signing.
- Keep enough free disk space for Xcode, export templates and multiple local game
  copies/builds. The game content alone is roughly 2 GB in the tested version.
- Review the [rights and sharing rules](PUBLICATION.md) and the applicable
  [FMOD](https://www.fmod.com/legal) and [Spine](https://esotericsoftware.com/spine-editor-license)
  licenses. Buying the game is not a blanket license to redistribute its contents
  or third-party software. This guide is not a legal determination about your use.

The tested setup uses Apple Silicon, Godot **4.5.1 .NET/mono**, .NET **9**, Xcode
**26**, FMOD iOS **2.03.15**, and Spine **4.2**. The port depends on the game's
internal APIs. Future Steam updates can require patch changes; success on every
game version or iPhone is not guaranteed.

## 1. Get the source

Open Terminal and run:

```bash
git clone https://github.com/kevnguye/sts2-ios.git
cd sts2-ios
```

Run subsequent commands from this folder unless a step says otherwise. If you are
reading a pull-request branch, use that branch until its changes are on `main`.

## 2. Install the Mac prerequisites

1. Install **Xcode** from the Mac App Store. Open it and complete Apple's setup
   yourself, including the iOS platform components and any license prompts.
2. In **Xcode → Settings → Accounts**, add your Apple Account yourself.
3. Install the **.NET 9 SDK** from [Microsoft](https://dotnet.microsoft.com/download/dotnet/9.0).
4. Download **Godot 4.5.1 .NET** from the [official archive](https://godotengine.org/download/archive/4.5.1-stable/).
   Choose the C#/.NET version, not the standard editor. Move the app into Applications.
5. Open that Godot editor. Go to **Editor → Manage Export Templates** and install
   the matching **4.5.1 stable mono** templates, including iOS.
6. Install [Homebrew](https://brew.sh/) if needed, then the native build tools:

```bash
brew install scons cmake
```

Check the installation:

```bash
xcodebuild -version
dotnet --version
scons --version
```

If `dotnet` is not found, use the installed executable's path and put its parent
directory in `STS2_DOTNET_ROOT` in step 4. Do not substitute a different Godot
version just because it is newer.

## 3. Prepare the native audio and animation libraries

Follow [Native Libraries](NATIVE_LIBRARIES.md). You obtain the FMOD SDK from its
official source and compile the extension libraries locally. No SDK or game
binary is downloaded from this repository.

At the end, you must have:

```text
~/sts2-ios-userlibs/ios-libs/
  libGodotFmod.ios.template_release.xcframework/   (static, iOS arm64)
  libspine_godot.ios.template_release.framework/

~/sts2-ios-userlibs/fmod-ios/lib/
  libfmod_iphoneos.a
  libfmodstudio_iphoneos.a
```

An SDK download folder is not itself the completed extension build. Keep the
SDK's headers and folder structure too; the native build needs them.

## 4. Fill in your private configuration

```bash
cp ios-export/config.example.sh ios-export/config.sh
```

Open `ios-export/config.sh` in a text editor. Set these values:

| Setting | What to enter |
|---|---|
| `STS2_TEAM_ID` | Your Team ID from Xcode's account/team information or your Apple Developer account |
| `STS2_BUNDLE_ID` | A unique identifier you choose, for example `com.yourname.sts2` |
| `STS2_SIGN_IDENTITY` | Your existing Apple Development certificate name |
| `STS2_DEVICE_UDID` | Your connected iPhone's identifier |
| `STS2_GAME_APP` | Your Steam app's `Contents/Resources` directory |
| `STS2_GODOT_BIN` | The Godot .NET app's executable, usually `/Applications/Godot_mono.app/Contents/MacOS/Godot` |
| `STS2_DOTNET_ROOT` | Your .NET installation directory; commonly `/usr/local/share/dotnet` |
| Native-library paths | The folders prepared in step 3 |

These commands help find the certificate name and device:

```bash
security find-identity -v -p codesigning
xcrun devicectl list devices
```

**The suffix in parentheses on a certificate name is not necessarily your Team
ID.** Get the Team ID from Apple's team/account information.

Leave save synchronization opt-ins at `0`. The Steam ID is optional unless you
separately choose to transfer saves. `config.sh` is ignored by Git; keep it private.

## 5. Build the local Xcode project

Close the desktop game, then run:

```bash
bash ios-export/build-ios.sh
```

The script checks inputs, copies local assembly references into ignored folders,
weaves the patches, compiles NativeAOT, prepares a private export driver, exports
Xcode, and creates the patched local content pack. It reads your Steam originals
without replacing them. Personal signing values and copied assets stay in
ignored local directories.

Success should produce:

```text
ios-export/build/StS2.xcodeproj
ios-export/build/StS2.pck
```

If it stops, read the log named in the error under `ios-export/.work/`. Do not
remove validation checks or ignore missing hooks to force a successful build.
Never upload this build folder or the local `.work/` directory.

## 6. Sign and install on your iPhone

1. Connect and unlock the iPhone. Accept **Trust This Computer** if prompted.
2. Enable **Settings → Privacy & Security → Developer Mode** on the phone if
   required; follow Apple's restart/confirmation prompts yourself.
3. Open `ios-export/build/StS2.xcodeproj` in Xcode.
4. Select the **StS2** target, then **Signing & Capabilities**. Enable automatic
   signing and choose your own team. Confirm **Increased Memory Limit** is present
   and that Xcode can provision it for your account.
5. Choose your **physical iPhone** at the top of Xcode. These extension libraries
   are for a device, not the simulator.
6. Click **Run**. Keep the phone unlocked during transfer and first launch.

If Apple reports a pending developer agreement, review and accept it yourself in
your [Apple Developer account](https://developer.apple.com/account/), then use
**Try Again** in Xcode. If Xcode asks to register your own device, register it.

This simple route installs the content pack inside the app. The separate slim-app
deployment documented in [Renewal](RENEWAL.md) is an advanced alternative, not a
required extra step after this full-content installation.

## 7. Check the game

Confirm the menu opens, start combat, drag/play a card, then save, quit and resume.
Also check sound and background/resume behavior. Initial loading and shader work
can be slow; recurring stalls are a known area of investigation. See
[Performance Checks](IOS_PERFORMANCE.md). Do not treat compilation as proof that
every gameplay path works.

Optional **Run Save Slots**, **Card Suggestions** and **Restart Room** are off by
default. Find their native checkboxes and explanations under
**Settings → General → Extras**. Normal autosaves work without enabling these.

## Keeping the app installed and updating it

- Free **Personal Team** provisioning normally needs renewal after **7 days**.
  Other development teams can have longer validity. Inspect the actual profile
  and signing certificate; do not assume everyone has the same expiry.
  [Apple's account/signing overview](https://developer.apple.com/help/account/basics/about-your-developer-account/)
- To renew through Xcode, connect the phone and build/sign/install over the same
  app using the same bundle ID and team. **Do not uninstall it**: that deletes its
  local container and saves.
- Steam updates do not automatically update this app. Back up saves, update your
  owned Steam installation, obtain compatible toolchain changes, rebuild and
  install over the existing app. Stop if the weaver reports missing targets.
- Keep generated IPAs, asset packs, extracted scenes, logs and saves private.
  Do not attach them to GitHub issues or releases.

## Quick Start prompt for Codex / Claude

Clone the repository first, open that folder in your coding assistant, and paste:

```text
Help me build and install this unofficial Slay the Spire 2 iOS port on my own
iPhone using my own purchased macOS Steam game files.

Read AGENTS.md, docs/BEGINNER_GUIDE.md, docs/NATIVE_LIBRARIES.md,
docs/PUBLICATION.md and docs/IOS_PERFORMANCE.md before changing anything.

Check my Mac, Xcode, .NET 9, Godot 4.5.1 .NET/mono export templates, connected
iPhone, and locally obtained FMOD/Spine dependencies. Explain missing
prerequisites in plain language. Use available connected Xcode/editor tools.
Let me complete account login, license agreements and device trust prompts.

Use only my local, legitimately obtained game/SDK files. Keep originals intact.
Keep all game binaries/assets, native SDKs, generated apps, extracted material,
saves, logs and personal signing values out of Git and public uploads.
Use ignored config.sh and build/.work directories. Do not obtain the game from
unofficial download sites or publish an IPA/content pack.

Validate my actual Team ID, bundle ID and paths; do not guess them. Build with
the documented scripts, fix errors without weakening checks, and report failed
tests honestly. Never uninstall the existing app or overwrite my saves.
Leave all Extras off and save synchronization opt-ins at zero unless I ask.

Verify the physical-device build, signing and first launch. Ask me to check
card dragging, sound, save/resume and any changed settings on the phone.
Explain my actual signing expiration and how to renew without losing saves.
Do not claim that a successful build proves full compatibility or legal rights.
```
