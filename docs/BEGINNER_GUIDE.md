# Set up Slay the Spire 2 on your own iPhone

This is the do-it-yourself guide for building this unofficial port from **your
own purchased macOS Steam copy**. You will prepare a Mac, build the required
libraries, create a local Xcode project, then sign/install it on your iPhone.
No coding assistant is required. An optional assistant prompt is at the bottom.

The repository contains tools and patches, **not the game or an installable IPA**.
It is not affiliated with MegaCrit. Keep your game files, SDKs, generated builds,
saves and signing details private. See [publication boundaries](PUBLICATION.md)
and the applicable FMOD/Spine licenses; the toolchain's license does not grant
rights to redistribute the game or those dependencies.

**What has actually been tested:** Apple Silicon Mac, Xcode 26, .NET 9,
Godot 4.5.1 .NET/mono, an iPhone 15 Pro, and a **paid Apple developer team**.
Combat dragging and the Extras controls worked in that test. Recurring hitches
remain, and audio still needs device verification. Other phones/game versions
and the complete **free Personal Team route have not been verified** by this fork.

## Read this first: free Apple Account or paid membership?

A subscription is not required to download/build these source tools. Apple also
allows personal-device testing through Xcode with a free account. Whether **this
specific game build** works with your free signing profile still needs checking,
especially its Increased Memory Limit capability.

| Difference | Free Apple Account | Paid Apple Developer Program |
|---|---|---|
| Apple membership fee | No paid membership | Apple lists **US$99 per membership year**; local pricing varies |
| Team to select in Xcode | Your **Personal Team** | Your enrolled individual/organization team |
| Personal iPhone installation through Xcode | Apple supports it, subject to free-account limits | Apple supports it with your team's provisioning |
| Renewal | Personal Team profiles expire **7 days after issuance** | Use the actual profile/certificate expiry; do not assume a weekly deadline or a permanent installation |
| Free-account limits | Up to **3 apps per device**, **3 registered devices**, **10 App IDs**; Apple describes a seven-day registration cycle | Separate program/device limits apply; the free Personal Team allowance is not the paid-team rule |
| This port's Increased Memory Limit | **Must be checked; not verified here.** A blank app installing successfully does not prove the game will work | Worked with the profile used in this fork's paid-team test; your own profile still needs to support it |
| Status of this guide's complete port installation | An unverified route you can check before building | The route used for the successful local installation |

Apple sources, checked **October 31, 2026**:
[free-account limits](https://developer.apple.com/help/account/basics/about-your-developer-account/),
[membership price](https://developer.apple.com/programs/enroll/),
and [provisioning profile purpose](https://developer.apple.com/help/glossary/development-provisioning-profile/).
The membership fee is separate from buying the game, obtaining licensed
third-party dependencies and having a suitable Mac.

**Do not buy a subscription on the assumption that it will fix every port issue.**
Signing, memory, audio and game compatibility are separate concerns. A subscription
also does not grant permission to upload or distribute a compiled copy of the game.

## Before starting

Have these ready:

- An **Apple Silicon Mac** capable of running your chosen Xcode/iOS SDK. The
  script currently expects the game's `data_sts2_macos_arm64` directory; an Intel
  Mac/Windows game install is not a drop-in replacement for this guide.
- Your purchased **macOS Steam version** of Slay the Spire 2. Install it, launch
  it once, then close the game before building.
- An iPhone, its passcode, a USB **data** cable, and your own Apple Account.
- Internet access and enough disk space for Xcode, templates, SDKs, AOT outputs
  and several private game copies. The tested content pack alone is about 2GB.
- Time for native dependency compilation. This is the most involved setup step.

The tested versions are Godot **4.5.1 .NET/mono**, .NET **9**, Xcode **26**,
FMOD iOS **2.03.15**, Spine **4.2**, and the source revisions in
[Native Libraries](NATIVE_LIBRARIES.md). Keep those versions aligned; newer is not
automatically compatible. Future Steam updates may require patch changes.

### Guide map

1. [Get the repository](#1-get-the-source).
2. [Install tools and check your Apple signing route](#2-install-the-mac-prerequisites).
3. [Build the audio/animation dependencies](#3-prepare-the-native-audio-and-animation-libraries).
4. [Fill in private paths and identifiers](#4-fill-in-your-private-configuration).
5. [Generate the Xcode project](#5-build-the-local-xcode-project).
6. [Install using your free or paid team](#6-sign-and-install-on-your-iphone).
7. [Check gameplay](#7-check-the-game).
8. [Renew/update without losing data](#keeping-the-app-installed-and-updating-it).

## 1. Get the source

Open **Terminal** on the Mac. Run these commands one line at a time:

```bash
mkdir -p "$HOME/Projects"
cd "$HOME/Projects"
git clone https://github.com/kevnguye/sts2-ios.git
cd sts2-ios
```

If you already have this checkout, open its folder instead of cloning over it.
If `git` asks for Apple's command-line tools, complete that installation first.

**Checkpoint:** `pwd` shows your `sts2-ios` folder, and `ls` shows `README.md`,
`docs`, `src`, `tools` and `ios-export`. Unless a step explicitly changes folders,
run this guide's commands from this repository root. This guide targets `main`;
a draft feature branch can have changes that are not yet released there.

## 2. Install the Mac prerequisites

### A. Install the tools (both account types)

1. Install **Xcode** from the Mac App Store. Open it and complete its initial
   setup, iOS platform downloads and any license prompts yourself.
2. In **Xcode → Settings → Locations**, select the installed Xcode under
   **Command Line Tools** if it is not already selected.
3. Install the **.NET 9 SDK** for macOS **Arm64** from
   [Microsoft](https://dotnet.microsoft.com/download/dotnet/9.0). Select the SDK,
   not just the runtime. Complete the installer.
4. Download the **Godot 4.5.1 .NET** macOS build from the
   [official archive](https://godotengine.org/download/archive/4.5.1-stable/).
   Extract it and move `Godot_mono.app` to **Applications**. Choose the C#/.NET
   edition; the standard Godot editor is not a substitute.
5. Open that Godot editor. In **Editor → Manage Export Templates**, install the
   matching **4.5.1 stable mono** templates, including iOS. If you downloaded a
   `.tpz`, use **Install from File**. Do not leave it as an unused download.
6. Install [Homebrew](https://brew.sh/) if needed, then:

```bash
brew install scons cmake python
```

Check the tools:

```bash
xcodebuild -version
xcode-select -p
dotnet --version
python3 --version
scons --version
cmake --version
"/Applications/Godot_mono.app/Contents/MacOS/Godot" --version
```

**Checkpoint:** .NET reports a 9.x SDK; Godot reports 4.5.1 mono; the Xcode path
points inside the full Xcode app, not only `/Library/Developer/CommandLineTools`.
Python 3 and the native build tools must be available. If you renamed/moved Godot,
use its actual path here and later in `config.sh`.

If `dotnet` is missing, open a new Terminal after installation. Microsoft's Mac
installer commonly uses `/usr/local/share/dotnet`; set `STS2_DOTNET_ROOT` to the
actual directory later. Do not replace a mismatched version merely to hide an error.

### B. Sign in and select the account type

Open **Xcode → Settings → Accounts**, click **+**, and add your Apple Account.
Complete password, two-factor authentication and agreements yourself.

- **Free:** expect a team labeled **Personal Team**. If it is absent, finish
  Apple's free developer registration/agreement through your own account and
  check Xcode again. This does not require purchasing membership.
- **Paid:** select the enrolled team you intend to use. If your account lists
  several teams, use that same team throughout this guide. Membership must be
  active and your team role must allow the required development signing.

### C. If this is your first Xcode app: check signing before the long build

You need a real **Team ID** before this port's export. Paid members can find it
in their Apple Developer account's [Membership details](https://developer.apple.com/help/glossary/team-id/). The following temporary
project also provides a way to identify the selected team and check provisioning,
including for a free account:

1. Connect/unlock the iPhone, accept **Trust This Computer**, and enable
   **Settings → Privacy & Security → Developer Mode** when required. Complete
   the phone's restart/confirmation prompts yourself.
2. In Xcode, choose **File → New → Project → iOS → App**. Name it `SigningCheck`,
   choose Swift/SwiftUI, and save it **outside this repository**.
3. Select that project's app target → **Signing & Capabilities**. Enable
   **Automatically manage signing** and select your Personal Team or paid team.
4. Choose a unique bundle identifier for the **temporary** app. Keep it different
   from the eventual game bundle so it cannot overwrite an existing game.
5. Select the physical iPhone at the top and run the blank app. Resolve account,
   trust, Developer Mode or device-registration errors here first. This temporary
   app counts against a free account's installed-app allowance, and its ID counts
   against the App ID allowance.
6. In **Signing & Capabilities → + Capability**, look for **Increased Memory Limit**
   add it, then choose **Product → Run** again to check provisioning. If unavailable
   or rejected, stop and investigate the exact error before compiling the game. Do not remove
   the port's entitlement merely to get a green signing result. This small app
   checks signing only; it does not reproduce the game's memory/runtime behavior.

To find the selected Team ID without relying on a paid membership page, open
`SigningCheck.xcodeproj` in Finder with **Show Package Contents**, open
`project.pbxproj` in a text editor and find `DEVELOPMENT_TEAM =`. Copy the actual
10-character value, not the placeholder and not a certificate-name suffix. Do not
edit the temporary project's values just to make the ID look correct.

Alternatively, this read-only command displays the selected build setting. Replace
the project path with your own:

```bash
xcodebuild -project "/path/to/SigningCheck.xcodeproj" -scheme SigningCheck -showBuildSettings \
  | sed -n 's/^[[:space:]]*DEVELOPMENT_TEAM = //p' | sort -u
```

If needed, **Xcode → Settings → Accounts → your team → Manage Certificates → + →
Apple Development** creates a development certificate. Reuse an existing valid
identity/private key when available; do not revoke certificates to troubleshoot
this guide. The certificate's parenthesized suffix can differ from the Team ID.

Once finished, you can remove **only the disposable SigningCheck app** from your
phone to free an app slot. Do not remove StS2. Deleting that temporary app does not
immediately release its registered App ID; avoid making lots of new test IDs.

## 3. Prepare the native audio and animation libraries

Follow **all steps** in [Native Libraries](NATIVE_LIBRARIES.md), then return here.
That page contains the exact clone, version-pin, build and packaging commands.
This step is the same for free and paid Apple Accounts.

When you download the FMOD SDK, open its archive/disk image or official installer
as appropriate. Find the complete SDK directory containing **`api/core/inc`** and
**`api/studio/inc`**. Keep the entire `api` tree at
`~/sts2-ios-userlibs/fmod-ios/api`. Do not put the installer itself in `ios-libs`,
and do not copy just the two `.a` files and discard the headers.

The native-library recipe then compiles the Godot extensions. At the end you need:

```text
~/sts2-ios-userlibs/
  fmod-ios/
    api/core/inc/                              # Keep the complete SDK tree.
    api/studio/inc/
    lib/libfmod_iphoneos.a                     # Device SDK archives.
    lib/libfmodstudio_iphoneos.a
  ios-libs/
    libGodotFmod.ios.template_release.xcframework/  # Static iOS arm64 extension.
    libspine_godot.ios.template_release.framework/
```

**Checkpoint:** all four listed native outputs exist. The SDK download folder is
not itself a compiled Godot extension. The FMOD XCFramework must be **static** for
this route; a simulator archive or the extension's default dynamic output will
not satisfy the documented build.

The export also reads matching macOS extension libraries from your owned Steam
app's `Contents/Frameworks`. Do not supply unrelated downloaded binaries if your
installed game version/layout differs.

## 4. Fill in your private configuration

Return to the repository root after the native-library recipe. If you cloned
somewhere else, use that folder instead of the default below:

```bash
cd "$HOME/Projects/sts2-ios"
if [ ! -e ios-export/config.sh ]; then
  cp ios-export/config.example.sh ios-export/config.sh
fi
chmod 600 ios-export/config.sh
open -e ios-export/config.sh
```

This preserves an existing `config.sh` instead of copying over your working
settings. If you already have one, edit its values only as needed.

The file is shell configuration, not rich text. Keep the quotes and variable
names. Replace placeholders with **your own values**:

| Required for the full build | What to enter |
|---|---|
| `STS2_TEAM_ID` | The actual 10-character ID for the team selected above |
| `STS2_BUNDLE_ID` | Your unique game bundle ID, such as `com.yourhandle.sts2`; replace the template's `com.yourname.sts2` |
| `STS2_GAME_APP` | Your Steam game's `Contents/Resources` directory |
| `STS2_GODOT_BIN` | The actual Godot mono executable; if installed as above, `/Applications/Godot_mono.app/Contents/MacOS/Godot` |
| `STS2_DOTNET_ROOT` | Your .NET directory, commonly `/usr/local/share/dotnet`; it must contain `dotnet` and `shared/` |
| `STS2_IOS_LIBS_DIR` | `$HOME/sts2-ios-userlibs/ios-libs` from step 3 |
| `STS2_FMOD_STATIC_DIR` | `$HOME/sts2-ios-userlibs/fmod-ios/lib` from step 3 |

If you chose the default Steam location, the game path is:

```text
~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources
```

For a custom Steam library, use Steam's **Manage → Browse local files**, then
Finder's **Show Package Contents** on `SlayTheSpire2.app` and open
**Contents → Resources**. Keep the whole installed app; the export also needs its
Frameworks directory. This route expects `data_sts2_macos_arm64/sts2.dll` and the
owned pack in that installation.

These fields support optional deployment/save scripts; Xcode's automatic signing
and destination picker handle them for the normal full-content install:

| Optional script setting | How to find/use it |
|---|---|
| `STS2_SIGN_IDENTITY` | Full valid Apple Development identity from `security find-identity -v -p codesigning`; needed by `deploy-slim.sh` |
| `STS2_DEVICE_UDID` | Device identifier from `xcrun devicectl list devices`; needed by USB transfer/deploy scripts |
| `STS2_BUNDLE_ID_SIGNED` | Keep equal to `STS2_BUNDLE_ID` for the ordinary Xcode route; advanced signing tools may change it |
| `STS2_STEAM_ID` | Needed only if you separately choose save transfer |

Leave `STS2_ALLOW_PHONE_PULL=0` and `STS2_ALLOW_DESKTOP_PUSH=0`. Starting a fresh
game needs no Steam ID or save migration. `config.sh` is ignored by Git, but you
must still avoid sharing it, its values, credentials or signing files publicly.

**Checkpoint:** `XXXXXXXXXX` and `com.yourname.sts2` have been replaced, and paths
point to actual local files. Keep the chosen game bundle ID/team for future updates.
Changing either can create a different app/container rather than update this one.

## 5. Build the local Xcode project

Close the desktop game and close any open **StS2 Xcode project window** before
regenerating it. Leave unrelated projects alone. From the repository root:

```bash
bash ios-export/build-ios.sh
```

The script reads the owned game, builds the patch library, weaves hook calls,
prepares managed dependencies, compiles NativeAOT, creates a private Godot export
driver, generates Xcode, and prepares the local game pack. It does not replace
Steam's original files. The first build can take time and print many AOT/reflection
warnings; a completion message is not proof those runtime concerns are resolved.

**Checkpoint:** the script reaches **Export complete** and these exist:

```text
ios-export/build/StS2.xcodeproj
ios-export/build/StS2.pck
```

An Xcode project here is **not yet an installed phone app**. If the script stops,
read the error and its named log under `ios-export/.work/`. Do not delete checks,
ignore missing hooks or strip memory entitlements to force success. Never upload
`build/`, `.work/`, an IPA or a generated app to GitHub.

## 6. Sign and install on your iPhone

### Common steps

1. Connect and unlock the phone; keep it awake. Confirm Trust/Developer Mode as in
   step 2. Open the generated project:

   ```bash
   open ios-export/build/StS2.xcodeproj
   ```

2. In Xcode's left navigator, select the **StS2 project**, then the **StS2 app
   target**. Open **Signing & Capabilities** and enable **Automatically manage signing**.
3. Check **Bundle Identifier** exactly matches `STS2_BUNDLE_ID` in your private
   config. Select the account-specific team below.
4. Confirm **Increased Memory Limit** is present. If needed, add it through
   **+ Capability**. Wait for Xcode to resolve signing; if it rejects the
   entitlement/profile, address that error before installation. Apple notes that
   extra memory is available only on supported devices and is not guaranteed:
   [entitlement documentation](https://developer.apple.com/documentation/bundleresources/entitlements/com.apple.developer.kernel.increased-memory-limit).
5. Select your **physical iPhone** as the destination at the top. These native
   libraries target a device; choosing the simulator is not this setup route.
6. Choose **Product → Scheme → Edit Scheme → Run** and confirm the build
   configuration is **Release** (the export helper sets it). Close the sheet,
   then choose **Product → Run**. Keep the phone unlocked during transfer.

### If you do not have a paid subscription

Choose **your name (Personal Team)**. This must be the team whose ID you put in
`config.sh`. Xcode handles free-account provisioning when supported. You may need
to approve the developer under **Settings → General → VPN & Device Management**
on the phone if iOS prompts for it; trust only your own development identity.

Watch for the three-app/App ID/device limits in the comparison table. An error
saying the Personal Team cannot provision Increased Memory Limit means this
specific route is blocked for your current setup; capture the exact error, keep
private values out of public logs, and investigate rather than removing the
entitlement. A successful minimal signing check is not a full port test.

If the game installs, record the profile's expiry and plan to re-sign within the
seven-day window. Treat this as a **free-account test**, not a route already
validated by the maintainers. Do not report success until gameplay has been checked.

### If you have a paid subscription

Choose your **enrolled team**, not a different Personal Team entry. Let Xcode
register your own device if prompted and if your role permits it. Resolve pending
Apple agreements yourself in [your developer account](https://developer.apple.com/account/),
then select **Try Again** in Xcode. An organization member may need their team
administrator to resolve access or agreement problems.

Your profile/certificate can last longer than the free seven-day window, but do
not assume the installation is permanent or lasts exactly one year. Record the
actual dates below. The paid team used in this fork successfully provisioned the
memory entitlement; that is not a guarantee for every team/device/game combination.

**Checkpoint for either account type:** Xcode finishes building/signing/installing,
StS2 appears on the phone, and the game reaches its menu. If it only built on the
Mac or remains at the logo, setup is not complete.

This route installs the **full pack inside the app**. Do not also run
`deploy-slim.sh` or `push-pck.sh` to “finish” it. Split-content/SideStore deployment
is an optional advanced route in [Renewal](RENEWAL.md), with separate unverified
signing considerations. It is not necessary for the Xcode instructions here.

## 7. Check the game

Before relying on the installation:

- Start a fresh run, enter combat, drag a card onto an enemy and play it. Try
  canceling a drag as well.
- Check music/effects and volume settings, then background/resume the app.
- Use the game's normal save/quit/continue flow and verify progress returns.
- In **Settings → General → Extras**, confirm Run Save Slots, Card Suggestions
  and Restart Room start off. Enable them only if you want those optional features.
- For more room near the edges, try **Settings → Graphics → Horizontal Screen
  Margin / Vertical Screen Margin**. They default to 0% and range up to 15% per
  side; set both to 0% to restore the full frame. They do not disable iOS Home
  gestures. Check card targeting after adjustment; physical-device verification
  of these newer controls is still pending. See [audio/margin notes](AUDIO_SCREEN_MARGINS.md).

The underlying game and patches can fail on paths not exercised by a successful
launch. Initial loading/warmup can be slow, recurring hitches remain, and audio
still needs device verification. See [performance checks](IOS_PERFORMANCE.md).
A paid account does not fix these compatibility/performance issues.

## Keeping the app installed and updating it

### Know when your signing expires

**Free:** Apple documents seven-day Personal Team profiles. Set your own reminder
before expiry; this repository does not refresh the app in the background.

**Paid:** inspect your actual installed build's provisioning profile **and**
signing certificate. A longer profile does not extend an earlier-expiring or
revoked certificate. Renewing membership does not itself rebuild/re-sign the app
already on your phone.

To inspect the build you actually installed, find `StS2.app` through Xcode's
**Products → StS2.app → Show in Finder** (the full app, not a framework). Set
`STS2_APP_PATH` to its real path. This reads the local profile and prints its date:

```bash
STS2_APP_PATH="/absolute/path/to/StS2.app"
security cms -D -i "$STS2_APP_PATH/embedded.mobileprovision" \
  | python3 -c 'import plistlib,sys; p=plistlib.load(sys.stdin.buffer); print("Profile expires (UTC):", p["ExpirationDate"])'
```

Find the selected **Apple Development** certificate in **Keychain Access → My
Certificates**, open its information and check the expiry date. Keep both dates
private; do not upload certificates or profiles. If the app path/profile is
missing, inspect the built **device** app from the correct configuration.

### Refresh an existing installation

1. Back up important saves first; see [Save Transfer and Recovery](SAVE_SYNC.md).
2. Connect/unlock the same phone and open your existing StS2 Xcode project.
3. Use the **same team and bundle ID**, with automatic Development signing.
4. Let Xcode update signing as needed, then **Product → Run** to install over the
   existing app. Check launch and saved progress afterwards.

You normally do not need to rebuild FMOD/Spine or update the owned game merely to
renew signing. **Do not uninstall StS2 or replace its container**: those actions
can remove your progress. If the app has already expired, re-sign/install over it
rather than deleting it. Switching from free to paid signing or changing teams
may change the app/container identity; back up first and do not assume progress
will automatically migrate.

### Update the game or port tools

Steam does **not** update the phone build automatically. Back up saves, close the
phone/desktop game and update your purchased Steam installation. For an unmodified
checkout, get tool updates from the repository root:

```bash
git status --short
git pull --ff-only
```

If you have local changes or the pull fails, review them instead of resetting or
forcing an update. Re-read release/setup notes for changed dependencies, run
`bash ios-export/build-ios.sh` again, then sign/install over the same app.
Stop if the weaver reports missing targets; that can indicate an unsupported
game update. Re-check gameplay and save loading after an update.

## Troubleshooting

| What you see | What to check next |
|---|---|
| `dotnet` or Godot not found | Correct installation/PATH and `STS2_DOTNET_ROOT` / `STS2_GODOT_BIN`; use .NET 9 and Godot 4.5.1 mono |
| SDK downloaded, but FMOD library missing | Keep SDK headers **and** compile/package the extension using the native-library recipe; copying an installer is not enough |
| Missing `data_sts2_macos_arm64` or a host framework | Confirm the purchased macOS Apple Silicon installation and its full app layout; Windows files are not substitutes |
| Missing/weaver-ambiguous game method | Stop and check game/tool version compatibility; do not skip the hook |
| Team ID rejected or signing uses the wrong team | Recheck the actual selected `DEVELOPMENT_TEAM`; certificate suffixes may differ |
| “No accounts”, agreement or provisioning error | Resolve Xcode Accounts, team selection, active membership/role where relevant, and user-completed agreements |
| Personal Team limit reached | Check installed development apps and recent App IDs/devices; deleting an app does not immediately free its registered ID |
| Increased Memory Limit unavailable/rejected | Check that team's capability/profile. Free signing for this port is still unverified; do not remove the requirement to force installation |
| Xcode says project changed/reloading | Close the StS2 project before regeneration, keep the exported version on disk, then reopen after export completes |
| Untrusted developer / Developer Mode disabled | Complete your own identity trust and Developer Mode prompts on the phone |
| App expires or will not open after its signing period | Refresh over the same app/team/bundle; do not uninstall |
| Black screen and Xcode shows `Thread 1: signal SIGUSR1` | The debugger paused the runtime. Use the project-local LLDB setup in [startup troubleshooting](IOS_PERFORMANCE.md#black-screen-when-launching-from-xcode), then resume; do not delete the app |
| Logo hang, missing pack, no sound or combat glitches | Distinguish successful signing from runtime compatibility; use current known-issues notes and reviewed private logs |

For a GitHub issue, share the error message and tool/game version details after
reviewing them for private information. Do not attach the game pack, assemblies,
SDKs, IPAs, signing files or device save container.

## Quick Start prompt for Codex / Claude

Clone the repository first, open that folder in your coding assistant, and paste:

```text
Help me build and install this unofficial Slay the Spire 2 iOS port on my own
iPhone using my own purchased macOS Steam game files.

Read AGENTS.md, docs/BEGINNER_GUIDE.md, docs/NATIVE_LIBRARIES.md,
docs/PUBLICATION.md and docs/IOS_PERFORMANCE.md before changing anything.

First establish whether I am using a free Personal Team or an enrolled paid
developer team. Explain that this port was tested with a paid team and that
free-account installation still needs verification, especially Increased Memory
Limit. Check signing/capability availability before the expensive native build.

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
