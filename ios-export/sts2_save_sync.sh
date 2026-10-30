#!/usr/bin/env bash
# Stable snapshot comparison; ambiguous changes require manual conflict resolution.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
[ -f "$SCRIPT_DIR/config.sh" ] || { echo 'Missing config.sh' >&2; exit 1; }
source "$SCRIPT_DIR/config.sh"
source "$SCRIPT_DIR/save-transfer.sh"
: "${STS2_DEVICE_UDID:?Missing device}"
: "${STS2_BUNDLE_ID_SIGNED:?Missing bundle}"
: "${STS2_STEAM_ID:?Missing Steam ID}"
[[ "$STS2_STEAM_ID" =~ ^[0-9]+$ ]] || { echo 'Steam ID must be numeric' >&2; exit 1; }
DESK="$HOME/Library/Application Support/SlayTheSpire2/steam/$STS2_STEAM_ID"
WORK="$SCRIPT_DIR/.work"
BACKUPS="$WORK/save_backups"
TOOL="$SCRIPT_DIR/../tools/save_manifest.py"
mkdir -p "$WORK"
mkdir "$WORK/save_sync.lock" 2>/dev/null || { echo 'Another save transfer is running' >&2; exit 1; }
STAGE=$(mktemp -d "${TMPDIR:-/tmp}/sts2sync.XXXXXX") || { rmdir "$WORK/save_sync.lock"; exit 1; }
trap 'rm -rf "$STAGE"; rmdir "$WORK/save_sync.lock"' EXIT
pgrep -f SlayTheSpire2 >/dev/null 2>&1 && { echo 'Desktop game is running; skipped'; exit 0; }
python3 "$TOOL" recover "$DESK"
# Two independent downloads must agree, including paths, hashes and mtimes.
for copy in phone phone_check; do
  xcrun devicectl device copy from --device "$STS2_DEVICE_UDID" \
    --domain-type appDataContainer --domain-identifier "$STS2_BUNDLE_ID_SIGNED" \
    --source Documents/default/1 --destination "$STAGE/$copy" \
    >"$STAGE/$copy.log" 2>&1 || { echo 'Phone unavailable; skipped'; exit 0; }
done
python3 "$TOOL" same "$STAGE/phone" "$STAGE/phone_check"
P=$(python3 "$TOOL" newest "$STAGE/phone")
D=$(python3 "$TOOL" newest "$DESK")
if python3 "$TOOL" same "$STAGE/phone" "$DESK" >/dev/null 2>&1; then
  echo 'Saves agree; no changes'
elif [ "$P" -gt "$((D + 2))" ]; then
  # A newest mtime alone cannot establish which divergent tree is authoritative.
  # Default to retaining a candidate; desktop replacement requires explicit opt-in.
  mkdir -p "$BACKUPS"
  CANDIDATE="$BACKUPS/phone_$(python3 "$TOOL" fingerprint "$STAGE/phone")"
  if [ ! -d "$CANDIDATE" ]; then
    cp -a "$STAGE/phone" "$CANDIDATE"
    python3 "$TOOL" same "$STAGE/phone" "$CANDIDATE"
  fi
  if [ "${STS2_ALLOW_PHONE_PULL:-0}" != 1 ]; then
    echo 'Phone differs: candidate backed up; desktop untouched. Review before enabling STS2_ALLOW_PHONE_PULL=1.'
    exit 0
  fi
  python3 "$TOOL" replace "$STAGE/phone" "$DESK"
  echo 'Phone profile copied to desktop; old desktop retained in the sibling .previous directory'
elif [ "$D" -gt "$((P + 2))" ]; then
  if [ "${STS2_ALLOW_DESKTOP_PUSH:-0}" != 1 ]; then
    echo 'Desktop differs; both saves untouched. Use push-save.sh or explicitly enable STS2_ALLOW_DESKTOP_PUSH=1.'
    exit 0
  fi
  FINGERPRINT=$(python3 "$TOOL" fingerprint "$DESK")
  if [ -f "$WORK/last_save_push" ] && [ "$(cat "$WORK/last_save_push")" = "$FINGERPRINT" ]; then
    echo 'This desktop save was already uploaded; waiting for import'
    exit 0
  fi
  stage_and_upload_save "$DESK" "$STAGE" "$STS2_DEVICE_UDID" "$STS2_BUNDLE_ID_SIGNED"
  printf '%s\n' "$FINGERPRINT" > "$WORK/last_save_push"
else
  echo 'Conflicting saves have similar timestamps; both left unchanged' >&2
  exit 1
fi
