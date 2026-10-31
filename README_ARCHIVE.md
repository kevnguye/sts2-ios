# Archived upstream overview (English translation)

> Translated from the former Chinese README. Use the [current README](README.md)
> and [beginner guide](docs/BEGINNER_GUIDE.md) for this fork. Original claims about
> being first, performance, licensing, signing and unattended renewal are historical
> claims, not guarantees or new verification. The original remains in Git history.

The original project described an open-source toolchain, patches and documentation
to run your own purchased Slay the Spire 2 Steam copy natively on an iPhone,
rather than by streaming or emulation. Its author presented it as the first
published account of a native iOS port and asked readers to support it with a star.

**No game is supplied.** The repository contains methods/tools, not the game's
binaries, `.pck`, artwork, fonts or audio. Obtain your own purchased files through
Steam. The project is not affiliated with or endorsed by MegaCrit; the game's
trademarks and copyright remain with their owners. The Android precedent was
[Ekyso/StS2-Launcher](https://github.com/Ekyso/StS2-Launcher).

The original README suggested giving the repository to a coding assistant such
as Claude Code or Cursor and reading [AGENTS.md](AGENTS.md), which describes
prerequisite checks, private configuration, building, signing and installation.
The current beginner guide provides a portable prompt and states the tested limits.

## Contents

| Included | Obtain separately / never publish here |
|---|---|
| Cecil static weaver | Game binaries or a cracked/free game download |
| Mobile patch source: touch, UI scale, memory, shaders, lifecycle | Decompiled game source |
| NativeAOT export configuration and pack-processing tools | FMOD/Spine proprietary binaries |
| Porting documentation | Game cards, audio, fonts and scenes |

## Technical approach

Runtime Harmony patching is unsuitable for the supported iOS NativeAOT route.
Mono.Cecil weaves calls into a private copy of the game DLL before NativeAOT turns
it into `sts2.framework`. The export project inserts a precompiled-assembly
replacement point into Godot's build pipeline. See [technical docs](docs/).

The original overview claimed fixes for per-process memory termination,
first-use shader stalls and Save and Quit crashes. The current fork still has
recurring frame hitches and needs audio/device verification; those historical
claims do not establish that every issue or gameplay path is fixed.

## Features

| Feature | Description | Source |
|---|---|---|
| Normal animation timing | 1x engine time avoids interpolation overshoot on slow frames; use native Fast Mode for shorter supported animations | [TimeScalePatch](src/STS2MobileIos/Patches/TimeScalePatch.cs) |
| Restart Room | Pause-menu button reloads the room autosave | [QuickRestartPatch](src/STS2MobileIos/Patches/QuickRestartPatch.cs) |
| Snapshots | Three Save Slot and three Load Slot buttons | [SnapshotPatch](src/STS2MobileIos/Patches/SnapshotPatch.cs) |
| Card advisor | Local archetype ratings/reasons on rewards, events, shops and upgrades; does not change probabilities or combat values | [CardAdvisorPatch](src/STS2MobileIos/Patches/CardAdvisorPatch.cs) |
| Save transfer/comparison | Manifest validation and recoverable replacement; scheduled writes require explicit opt-in | [Save guide](docs/SAVE_SYNC.md) |

Save Slots, Card Suggestions and Restart Room default **off** under
**Settings → General → Extras**. Native checkboxes/explanations persist immediately.
Disabling suggestions removes their timers/overlays; disabling slots preserves
existing snapshots. Ordinary autosaves remain separate. These preferences live
outside progress files, so restoring progress does not change them.

## Repository layout

```text
AGENTS.md                  Setup/maintenance instructions for coding assistants
src/STS2Weaver/            Mono.Cecil static weaver
src/STS2MobileIos/         Mobile patches and manifest.json hook mappings
  Patches/                Touch, layout, scale, shader, snapshot, lifecycle, advisor code
  PatchHelper.cs          Reflection and logging helpers
ios-export/               NativeAOT/export driver and build/deployment scripts
  config.example.sh       Private-configuration template
  build/push-pck/deploy/push-save/sync scripts
                          Export, content upload, install and save transfer
tools/                    Tools for locally owned packs and build inputs
docs/                     Architecture, export pipeline and setup documentation
share/                    Technical report in English HTML/PDF
```

## Prerequisites and platforms

The original requirements were a Mac with Xcode, .NET 9 SDK, Godot 4.5.1 mono,
your purchased macOS Steam files, and locally obtained FMOD 2.03/Spine 4.2 libraries.
It described free Apple Account signing with a 7-day cycle; this fork's successful
installation used a paid team, and free signing still needs verification.

| Stage | Platform described in the original guide |
|---|---|
| Build | macOS/Xcode for iOS export, NativeAOT, native libraries and initial signing; remote-Mac/private-runner proposals in [Building without a Mac](docs/BUILD_WITHOUT_MAC.md) |
| Installation/renewal | Original iLoader/SideStore/LocalDevVPN combination; see the historical [Renewal notes](docs/RENEWAL.md) |
| Save transfer | Repository scripts use macOS devicectl; the original note suggested other device-file tools for Windows/Linux, not a verified cross-platform implementation here |

The original conclusion was that a Mac was needed for building while later
installation/renewal could use other platforms. Follow current tool instructions
and test the actual signing/container behavior rather than assuming that workflow.

## License

The original tools, patches and documentation carry the repository's [MIT license](LICENSE).
It does not license the game or proprietary dependencies. See
[third-party notices](THIRD_PARTY_NOTICES.md) and [publication boundaries](docs/PUBLICATION.md).
