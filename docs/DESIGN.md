# STS2 iOS port: overall design

> Translated historical design record, originally dated July 8-13, 2026. It
> includes plans, claims and old observations that are not current guarantees.
> For today's tested build route and limitations, start with
> [the beginner guide](BEGINNER_GUIDE.md) and [performance notes](IOS_PERFORMANCE.md).
> In particular, this fork uses normal 1x engine time, Extras default off, and
> has not verified the free-account/SideStore route described below.

The original scope accepted rebuilding after every game update and personal use
of the user's own purchased copy.

## Distribution model

The initial “Balatro-style” plan bundled the user's local Steam pack and assemblies
into a privately built IPA. It excluded LauncherPatches, Steam login/download/cloud
save infrastructure (SteamKit2), ModLoaderPatches and, initially, LanMultiplayerPatcher.
Local saves plus `UIFileSharingEnabled` allowed Finder-based backups.

## Technical approach

- **Engine:** official Godot 4.5.1-stable mono iOS templates. The game used the
  private MegaDot 4.5.1.m.12 fork. The proposed compatibility test (M1.5) replaced
  MegaDot in a private `testbed.app` copy with the official desktop engine. If
  that failed, investigating fork differences remained a risk.
- **.NET:** NativeAOT because the supported iOS path has no JIT. Replace runtime
  Harmony patching with static IL weaving. See the [pipeline research](design-ios-dotnet-export.md).
- **STS2Weaver:** Mono.Cecil console tool taking `sts2.dll`, `STS2MobileIos.dll`
  and `manifest.json`. A bool prefix returning false skips the original body,
  with optional ref `__result`; a void prefix runs at entry; postfix calls use
  a common exit. Bind `__instance`, return values and original named/ref parameters,
  including property getters such as `get_UploadData`.
- **Patch port:** the initial plan ported 11 required Android patch files, removed
  HarmonyLib/AccessTools dependencies and used ordinary reflection. Lifecycle
  behavior needed iOS semantics rather than Android restart logic. Early plans
  proposed permanent Contains weaving with a `_suppressContains` gate for ModelDb;
  later runtime investigation removed that workaround. Proposed order: ModelDb,
  Platform, Settings, UI scale, layout, lifecycle, touch.
- **Reflection preservation:** root the main game assembly so reflected types,
  `GetUninitializedObject` and private constructors remain available. Do not assume
  that rooting all desktop dependencies is safe; see later build guidance.
- **GDExtensions:** compile Spine from the 4.2 branch to the required
  `ios/libspine_godot.ios.template_release.framework`; compile FMOD's extension
  and obtain the separate iOS Engine archives from FMOD. Account registration and
  license acceptance are user steps. Disable the unmatched Sentry extension in
  the privately prepared pack; PlatformPatches stubs reporting paths.
- **Pack processing:** modify `extension_list.cfg` via repacking or a proposed
  override pack. `tools/pck_ls.py` provides extraction; early writer options were
  official headless `--export-pack` or a custom writer.

## Milestones and evidence

1. **M0:** install Xcode, .NET 9, Godot 4.5.1 editor/templates.
2. **M1:** run a Godot .NET hello-world on a physical iPhone to check signing/AOT.
3. **M1.5:** run the desktop game with the official engine to test MegaDot compatibility.
4. **M2:** replace the DLL in a private desktop `testbed.app`, verifying weaving
   and UI scale under CoreCLR independently of AOT.
5. **M3:** combine the iOS template, owned pack, woven NativeAOT assembly,
   GDExtensions and Metal rendering.
6. **M4:** test touch, lifecycle, performance and saves on-device. The early plan
   mentioned Sideloadly and free-account renewal automation.

The original requirement was to obtain runnable evidence at each step before
moving on; a plan or successful compile was insufficient.

## Original recorded observations

- A private, ad-hoc re-signed game copy ran outside Steam, retrying Steam
  initialization instead of quitting. Adding `steam_appid.txt` with app ID
  `2868840` resolved that initialization path in the original test.
- Apple Silicon needed at least ad-hoc signing; otherwise the app exited immediately.
- The banner identified `MegaDot v4.5.1.m.12.mono.custom_build`, a private fork.
- The original unencrypted v3 pack contained 12,328 extracted files.
- The author initially classified all 51 `.gdc` files as editor-only; treat this
  as an early observation, not a rule for later packs or runtime autoloads.

## Environment notes

- zsh does not split an unquoted variable on spaces like some shell assumptions.
- Installing the Homebrew .NET cask requested sudo; the initial alternative used
  `dotnet-install.sh` in `~/.dotnet`.
- Large GitHub release downloads were slow/interrupted in the original environment;
  resumable `curl -C -` helped. A local proxy failed to reach GitHub object storage.

## Deployment architecture v2: split content and phone-based renewal

July 12, 2026 record. The original author reported that free signing expired after
7 days and SideStore's whole-package memory processing could kill a ~2GB install
through jetsam or the CPU watchdog, repeating on refresh.

The proposed solution kept game assets out of the repeatedly signed package:

- Executable/frameworks/Info.plist only: roughly 165MB app / 54MB IPA in that build.
- Upload the roughly 1.77GB pack once to `Documents/StS2.pck`.
- Set `godot_cmdline = ["--main-pack", "user://StS2.pck"]` in Info.plist.
  `drivers/apple_embedded/main_utilities.mm` reads it; iOS user data maps to Documents.
- C# code stays in `sts2.framework`; separating the asset pack does not add dynamic
  managed-code loading.

### Accounts and identities

Use your own developer identity, never another person's signing values.

- The original author used their own Apple Account with SideStore/iLoader and
  reported free signing; that route remains unverified by this fork.
- SideStore may append `<SideStoreTeamID>` to the chosen bundle ID; a USB test
  copy used the original unsuffixed ID. They are different app containers.
- Increased Memory Limit must be present in the actual App ID/profile. The
  original setup used Xcode's Signing & Capabilities UI.
- Original phone-side combination: SideStore 0.6.3, LocalDevVPN and an iLoader
  pairing file. For slow DDI downloads, the author suggested privately preparing
  the image in SideStore's `Documents/DMG/`.

### Tools

- `ios-export/deploy-slim.sh`: replace woven dylib, strip the bundled pack, add
  command-line settings, re-sign and install over USB for testing.
- SideStore delivery: package a private slim IPA and import through My Apps +.
- `sts2_save_sync.sh`: set `STS2_BUNDLE_ID_SIGNED` to the actual suffixed bundle.

### Observations reported on July 12

- Refresh preserved Documents, pack, saves, marker files and their modification times.
- The signed increased-memory entitlement reportedly gave a 5,436MB available
  budget versus about 3GB without it. These figures are device/state-specific.
- Same-bundle, same-version IPA caching required incrementing `CFBundleVersion`
  for a new build; the recorded version was 0.107.2.
- Memory diagnostics were off by default from 0.107.3. Add `memcheck_on` in the
  container to append startup data to `memcheck.txt`; remove it to disable.

### Problems recorded in the original development

- Hit-stop wrote TimeScale back to 1.0, overriding the old 4x patch; weaving
  `NHitStop.SetTimeScale` applied `TARGET_SCALE`. **The current fork uses 1x**
  because forced acceleration breaks hand-card interpolation at low frame rates.
- Cold-pack shader collection could block for over 10 seconds and trigger
  watchdog `0x8BADF00D`. Yielding every four resources and deduplicating immediately
  avoided later access to disposed materials (`ObjectDisposedException`).
- Force-killing during FMOD pack reads could cause SIGSEGV; the original record
  left this unresolved and described it as limited to externally forced termination.
- Memory diagnostics used `os_proc_available_memory` from `libSystem.B.dylib`;
  the attempted `__Internal` import did not work in that test.

## Desktop Mac patch experiment

July 13, 2026 request: old 4x timing plus Restart Room on the owned Steam Mac
version, without mobile touch/UI scale/network or six snapshot buttons.

- The desktop DLL matched the then-current iOS reference hash (`e424ace`), allowing
  patch reuse. `manifest.mac.json` selected three hooks: time-scale Ready,
  hit-stop prefix and `QuickRestart.RestartOnlyPostfix`.
- `AddRestartButton()` was shared; mobile Ready added restart/snapshots and the
  desktop variant added only Restart Room. Current mobile Extras gate these independently.
- A separate patch DLL caused FileNotFoundException. Desktop GodotSharp used
  PluginLoadContext/AssemblyDependencyResolver with empty deps.json targets;
  dependency entries did not fix it, and directory fallback was absent. The C#
  failure left an English menu with inactive buttons in the original test.
- ILRepack merged STS2MobileIos into sts2.dll. The original `merge.csproj` used
  ILRepack.Lib 2.0.34; keep the output identity/filename `sts2`. Weave first, then
  merge/rewrite references. The original temporary output was `/tmp/sts2-merged-out/sts2.dll`.
- The original app used ad-hoc hardened-runtime signing with
  `disable-library-validation` and `allow-jit`. Re-signing after a DLL edit
  preserved its original entitlements and rebuilt CodeResources to avoid a
  damaged-app warning. The original entitlement copy was `/tmp/sts2_mac_ent.plist`.
- The record referenced `tools/deploy-mac.sh` with an `.orig` backup and `--revert`.
  Those historical helper files are not all shipped in this repository. Steam
  verification/updates can restore originals; the Apple Silicon data path was
  `data_sts2_macos_arm64`.
