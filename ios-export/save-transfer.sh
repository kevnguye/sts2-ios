#!/usr/bin/env bash
# Shared by the two save senders. Publish completion only after payload transfer.
stage_and_upload_save() {
  local source="$1" stage="$2" device="$3" bundle="$4"
  local manifest_tool="$SCRIPT_DIR/../tools/save_manifest.py"
  local timestamp version
  timestamp=$(python3 "$manifest_tool" stage "$source" "$stage/transfer") || return 1
  version="$timestamp-$(basename "$stage")"
  # Marker is outside transfer/, so it cannot arrive with an incomplete payload.
  shasum -a 256 "$stage/transfer/manifest.json" | awk '{print $1}' > "$stage/complete" || return 1
  xcrun devicectl device copy to --device "$device" \
    --domain-type appDataContainer --domain-identifier "$bundle" \
    --source "$stage/transfer" --destination "Documents/sync_inbox/$version" \
    >"$stage/payload.log" 2>&1 || { cat "$stage/payload.log" >&2; return 1; }
  xcrun devicectl device copy to --device "$device" \
    --domain-type appDataContainer --domain-identifier "$bundle" \
    --source "$stage/complete" --destination "Documents/sync_inbox/$version/complete" \
    >"$stage/complete.log" 2>&1 || { cat "$stage/complete.log" >&2; return 1; }
  echo "Validated transfer $version uploaded; import occurs on next game start."
}
