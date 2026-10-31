# Slay the Spire 2 iOS port tools

Build an unofficial personal iPhone app from **your own purchased macOS Steam
copy** of Slay the Spire 2. This fork maintains the source toolchain from
[jhaizhou-ops/sts2-ios](https://github.com/jhaizhou-ops/sts2-ios), with safer file
handling, normal animation timing, and optional extras that default off.

**No game download or prebuilt IPA is supplied.** Game binaries/assets, native
SDKs, generated apps, saves and signing details stay on each user's computer.
This project is not affiliated with or endorsed by MegaCrit.

## Start here

- **[Beginner setup guide](docs/BEGINNER_GUIDE.md)** — Mac prerequisites, local
  dependencies, configuration, build, iPhone installation and renewal.
- **[Native library recipe](docs/NATIVE_LIBRARIES.md)** — the tested versions and
  commands for the FMOD/Spine extensions.
- **[Publication and licensing rules](docs/PUBLICATION.md)** — what must stay
  private and the limits of the repository's license.
- **[Performance notes](docs/IOS_PERFORMANCE.md)** and **[save-transfer notes](docs/SAVE_SYNC.md)**.

The guide ends with a **[Quick Start prompt for Codex / Claude](docs/BEGINNER_GUIDE.md#quick-start-prompt-for-codex--claude)**.

## What has been checked

The local Apple Silicon/Xcode 26/.NET 9/Godot 4.5.1 mono route has built, signed
and launched on an iPhone 15 Pro. The user confirmed combat card dragging works
with normal engine timing and that the new Extras controls appear. Native
checkbox callbacks and preference persistence have been checked on the phone.

Recurring frame hitches remain under investigation. Audio and other device/game
version combinations still need verification; a successful compile does not
prove every gameplay path works. Steam updates do not automatically update this
port and may change the internal APIs the patches target.

## Optional extras

Under **Settings → General → Extras**, enable Run Save Slots, Card Suggestions,
or Restart Room separately. All are off by default. Disabling the advisor stops
its repeating work; disabling save slots keeps existing slot files. The game's
normal autosave is separate from these optional snapshot slots.

## Contributing without distributing the game

Keep proprietary game files and native dependencies out of Git, issues, releases
and build artifacts. Before publishing:

```bash
python3 tools/check_publication.py --history
```

See [test commands and human verification](tests/README.md). Build logs and
screenshots can contain private data or game content; review them before sharing.

## Attribution and rights

The original toolchain's [MIT license](LICENSE) and [third-party notices](THIRD_PARTY_NOTICES.md)
are retained. That license does not grant rights in the game, FMOD, Spine or
other third-party material. Excluding game assets is not a legal guarantee for
every use. Obtain the rights required for your use and do not redistribute a
compiled game or its asset pack.

[Archived upstream overview (English)](README_ARCHIVE.md) · [Technical background](docs/DESIGN.md) · [English technical report](share/technical_record.pdf)

[Audio and screen-margin update / device verification](docs/AUDIO_SCREEN_MARGINS.md)
