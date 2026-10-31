# Renewing an installation

> English translation of the original split-content/SideStore notes. The original
> author's observations below are historical, not verification by this fork of
> current SideStore versions or free-account support. For the tested Xcode route,
> follow [the beginner guide](BEGINNER_GUIDE.md#keeping-the-app-installed-and-updating-it).

Free Personal Team profiles expire after 7 days. The standard Xcode route is to
connect the phone and re-sign/install over the existing app with the same team
and bundle ID. Other development teams can have longer profile validity; inspect
the actual profile. **Never uninstall merely to renew: that deletes app data.**

The original guide described combining three independent tools to renew from the
phone without reconnecting a Mac every week. This repository supplies no code or
binaries from those tools and does not guarantee unattended renewal.

## The three components

| Tool | Purpose | Official source |
|---|---|---|
| SideStore | Sign/refresh apps on the phone with your own development identity; supports background refresh | [SideStore](https://github.com/SideStore/SideStore) |
| LocalDevVPN | Local tunnel used by the described wireless renewal workflow | [App Store, Coxson Engineering LLC](https://apps.apple.com/us/app/localdevvpn/id6755608044) |
| iLoader | Desktop installation of SideStore and generation/import/backup of device pairing files | [iLoader](https://iloader.site/) |

In that workflow, SideStore signs the app, LocalDevVPN provides a local tunnel,
and the pairing file supplies the device trust relationship. With the required
connection and Wi-Fi, refresh obtains a new signing period. Check each project's
current instructions rather than assuming the historical combination still works.

## Why separate the content pack?

The original author reported that SideStore loaded the whole package into memory
while installing/refreshing, and a roughly 2GB game bundle exceeded device limits.
The split-content layout kept only executable code/frameworks in the signed app.

| Part | Contents | Original observed size | Location | Re-signed? |
|---|---|---|---|---|
| Slim app | Executable, frameworks, `Info.plist` | ~165MB app / ~55MB IPA | Installed app bundle | Yes |
| `StS2.pck` | Scenes, cards, images, audio and other game assets | ~1.77GB | `Documents/StS2.pck` | No |

These sizes refer to the older build. Same-bundle updates/refreshes were reported
to preserve Documents, so the pack needed uploading only once per content version.
Do not assume that changing signing identity or bundle ID keeps the same container.

## Preparing the slim app

The full Xcode build includes the pack. The advanced `deploy-slim.sh` route:

1. Copies the built app to staging and removes its bundled `StS2.pck`.
2. Sets `godot_cmdline = ["--main-pack", "user://StS2.pck"]` in `Info.plist`.
   On iOS, this user-data location is the app's Documents directory. The engine
   reads the key in `drivers/apple_embedded/main_utilities.mm`.
3. Re-signs the staged app and installs it over USB. It does not upload the pack.

A SideStore workflow additionally packages the slim app as an IPA for SideStore.
Keep all generated apps/IPAs and game content private.

## Uploading the content pack

First install the slim app, so its container exists. Then run:

```bash
bash ios-export/push-pck.sh
```

The script reads device/bundle settings from `config.sh`, copies `build/StS2.pck`
to the app's Documents and verifies that the destination exists. It uses:

```text
xcrun devicectl device copy to ... --domain-type appDataContainer
  --domain-identifier <installed-bundle-id> --source StS2.pck
  --destination Documents/StS2.pck
```

Set `STS2_BUNDLE_ID_SIGNED` to the actual installed bundle ID; SideStore may append
a team suffix. If devicectl fails, use Finder: connect iPhone -> Files -> the app,
and drag in `StS2.pck`, keeping that exact filename. See
[deployment architecture](DESIGN.md#deployment-architecture-v2-split-content-and-phone-based-renewal).

## Historical setup sequence

1. On a computer, use iLoader's own instructions to install SideStore and create
   a device pairing file. Import it into the device and retain a private backup.
2. On the phone, install/configure LocalDevVPN from its official source.
3. Import your privately built slim IPA into SideStore.
4. Confirm the actual signed profile retains Increased Memory Limit. The original
   author observed ~5.4GB available after refresh versus ~3GB without it; this
   fork has not reproduced that free-account/SideStore observation.
5. Upload the content pack once with `push-pck.sh`.
6. Maintain the connection required by the tools and refresh as directed. The
   original SideStore UI also offered a manual refresh through its “7 DAYS” item.

## Observations and pitfalls from the original guide

- Refresh reportedly preserved Documents, pack/saves/markers and modification
  times. Still keep backups, and never uninstall to update.
- Same-bundle, same-version IPAs could reuse a cached build. Increment
  `CFBundleVersion` when delivering a different build.
- iOS updates/resets can invalidate pairing files; regenerate/import as directed.
- The LocalDevVPN setup can conflict with another active VPN. Check connection
  status when refresh fails.
- A suffixed bundle ID must also be used by save-transfer scripts to reach the
  correct container (`STS2_BUNDLE_ID_SIGNED`).

Credit belongs to SideStore/AltStore contributors, LocalDevVPN's authors and
iLoader's authors. These notes describe an integration, not distribution of their software.
