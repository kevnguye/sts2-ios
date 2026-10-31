# Building without owning a Mac (remote macOS)

> Translated historical guidance. Follow the [current beginner guide](BEGINNER_GUIDE.md)
> for the tested workflow. Remote signing and free-account support have not been
> verified by this fork. Provider offerings and signing-tool behavior can change.

The build stage requires macOS: Xcode, Godot iOS export, NativeAOT, native iOS
libraries, and initial signing/capability setup. If you do not own a Mac, the
original guide describes two remote macOS approaches.

## Keep private files private

Use your own purchased game files (`sts2.dll`, `Slay the Spire 2.pck` and its
managed dependencies). Never put them in a public repository or public CI output.
For remote work, use private, temporary transfer/storage and remove private copies
when finished. Obtain FMOD/Spine dependencies separately from their official
sources; see [Native Libraries](NATIVE_LIBRARIES.md).

## Option A: rent a remote Mac

Connect to a remote macOS desktop and follow [AGENTS.md](../AGENTS.md) and the
beginner guide: install prerequisites, configure local paths, sign in to your own
Apple Account in Xcode, select a team, provision Increased Memory Limit, and build.

The original guide listed AWS EC2 Mac, MacStadium, MacinCloud and Scaleway Mac mini
as possible providers. Verify current availability, minimum rental periods and
pricing yourself. Choose a service with the remote desktop/SSH access you need.
Transfer your owned macOS Steam app and dependencies privately, for example using
`scp`, and point ignored `ios-export/config.sh` at their locations. Account login
and legal agreements must be completed by you.

A desktop session provides the interactive Xcode signing workflow. Rental costs
and initial setup take additional time. Match the documented tool versions.
A remote Mac also needs a separately planned way to deliver the signed build to
your physical iPhone; it does not automatically have access to a locally attached USB device.

## Option B: automate the command-line stages on a private macOS runner

The original proposal used a GitHub Actions macOS runner with Xcode to run the
build stages: weaving, AOT, export and pack preparation. This produces a local
Xcode project, content pack and native game library. It is an advanced template,
not a configured or tested workflow in this repository.

1. Keep game files/dependencies in private storage and use narrowly scoped CI
   secrets, or use your own private/self-hosted Mac runner. Do not add these files
   to Git or publish generated game-containing artifacts.
2. Signing still needs your own Apple identity. The original proposal separated
   an unsigned/ad-hoc build from later signing with SideStore; this requires its
   own working provisioning/install workflow. See [Renewal](RENEWAL.md).
3. Increased Memory Limit must be supported by the actual App ID/profile. The
   original workflow used interactive Xcode setup; headless CI does not remove
   that requirement or prove the free-account route works.

Illustrative skeleton only; the dependency and private-input steps are deliberately
incomplete. Keep any artifact retention private and access-controlled:

```yaml
name: build-ios-template
on: workflow_dispatch
jobs:
  build:
    runs-on: macos-latest # Pin a compatible Xcode/macOS environment for a real setup.
    steps:
      - uses: actions/checkout@v4
      - name: Install dependencies
        run: |
          # Install .NET 9, Godot 4.5.1 mono and matching iOS export templates.
      - name: Fetch privately owned game files and licensed native libraries
        env:
          FETCH_TOKEN: ${{ secrets.FETCH_TOKEN }}
        run: |
          # Fetch sts2.dll, the pack and native libraries from private storage.
          # Create ignored config.sh with the runner's actual paths/signing values.
      - name: Prepare local export
        run: bash ios-export/build-ios.sh
      # Add your private signing/delivery steps; never publish game-containing outputs.
```

A remote desktop offers the closest match to the interactive Mac instructions.
A private runner can automate repeated exports, with additional work for private
inputs, signing and delivery. In both cases, game content must stay private.
