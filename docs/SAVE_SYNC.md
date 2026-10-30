# Save transfer and recovery / 存档传输与恢复

This fork changes the transfer format. Update the phone patch and desktop scripts together. Old inbox folders without a manifest and completion marker are ignored and left untouched. Re-send with the updated `push-save.sh`; do not add a marker to an old or partially transferred folder.

本分支采用新传输格式：手机补丁与电脑脚本必须一起更新。旧收件箱不会自动导入，也不会删除。请用新版脚本重新传输。

## Locations

| Data | Location |
|---|---|
| Desktop Steam profile | `~/Library/Application Support/SlayTheSpire2/steam/<SteamID64>/` |
| Phone active profile | App `Documents/default/1/` |
| Phone previous profile | App `Documents/default/1.previous/` |
| Phone import payload | `Documents/sync_inbox/<unique-transfer>/1/` |
| Desktop retained phone candidates | `ios-export/.work/save_backups/phone_<fingerprint>/` |

Import replaces only profile `1`. Other phone profiles remain in place. A previous profile is retained until the next replacement has finished staging. Snapshot restore still deliberately restores the whole snapshot save tree.

## Manual desktop-to-phone transfer

Close both games, back up valuable progress independently, configure your numeric Steam ID/device/bundle in ignored `ios-export/config.sh`, then run:

```bash
bash ios-export/push-save.sh
```

The sender refuses missing/empty `profile.save` and symlinks. It compares file hashes, sizes, paths and modification times before and after staging, and writes `manifest.json` only after a stable copy. The payload upload must succeed before a separate completion marker is sent. On the next phone game start, the importer checks the manifest digest, every file's SHA-256 and size, and the exact file set. The transfer must be newer than the local profile. An incomplete or changed transfer is left untouched for retry/investigation.

Hashes establish transfer integrity, not who authored the saves. Only import your own trusted files. This is not full semantic validation of the game's proprietary save format. Modification times are a heuristic; clock skew and divergent progress can require manual conflict resolution.

## Scheduled comparison is conservative by default

```bash
bash ios-export/sts2_save_sync.sh
```

The script skips while the desktop game is running and requires two independent phone downloads to agree. Close the phone game too. Matching trees need no action; differing trees with similar timestamps produce a conflict and no replacement.

By default, a newer phone tree is retained as a candidate, and a newer desktop tree is reported without upload. No scheduled replacement occurs unless you explicitly set the corresponding value in `config.sh`:

```bash
STS2_ALLOW_PHONE_PULL=1      # choose phone as authoritative when its mtime is newer
STS2_ALLOW_DESKTOP_PUSH=1   # permit uploads when desktop mtime is newer
```

These switches opt into a timestamp policy, not a guarantee that the most progress wins. Leave them at `0` until you have reviewed the direction and recovery procedure. `push-save.sh` remains the deliberate manual send command. There is no automatic installation of launchd/cron jobs.

Phone candidates are deduplicated by fingerprint and retained for manual review; they are not automatically pruned. Check disk usage before scheduling frequent runs. A process lock prevents overlapping senders. A force-killed sender can leave `.work/save_sync.lock`; after confirming no sender is active, remove that empty lock directory to resume.

## Recoverable replacement, not an atomic directory exchange

Phone imports, snapshots and desktop pulls copy and verify into a sibling `.pending` directory, retain the current directory as `.previous`, then rename the replacement into place. A caught commit failure restores the previous location. If the process stops between renames, startup recovery restores `.previous` when the active path is missing. There can be a brief gap between renames; this is not a single atomic exchange or a guarantee against power-loss/filesystem corruption.

On the phone, recovery runs at the earliest platform-initialization hook before normal save reads, including when no inbox exists. The desktop scripts recover an interrupted desktop replacement before reading it. Avoid running games or another file manager against saves during replacement. The game's own background writes are not controlled by these helpers; hash checks catch changes observed during staging, but are not a game-level save lock.

Keep an independent backup before restoring snapshots or choosing a sync direction. The last `.previous` is only one generation and is replaced by a later transaction. Never uninstall the app to update it.

## Windows/Linux manual staging

Python 3.11+ can prepare the same payload on any platform:

```bash
python3 tools/save_manifest.py stage /path/to/desktop-save /path/to/new-transfer
```

Use a trusted device-file tool to place the contents of `new-transfer/` at `Documents/sync_inbox/<unique-transfer>/`. After the payload finishes copying, create a `complete` file containing the lowercase SHA-256 of `manifest.json` and copy it into that transfer directory last. Start the updated phone app to validate and import. Simply copying a `1/` folder or using an arbitrary large version number no longer enables import.

Use `Documents/default/1.previous/` for manual recovery only with the game closed and after copying both active and previous trees to independent backup locations. Preserve the bundle identifier across app upgrades.
