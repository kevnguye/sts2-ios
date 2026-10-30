# Security regression checks

Requires Python 3.11+ and .NET 9 SDK for the standalone C# checks. The C# project has no NuGet test-framework or game dependencies. It links the production save-store/import files directly and stubs only Godot's user-directory lookup and logging. All saves/packages are synthetic and stay in temporary directories.

From the repository root:

```bash
python3 tests/verify_extractor.py
python3 -m unittest discover -s tests -p 'test_*.py' -v
DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet run --project tests/SaveSafety -- "$PWD"
```

The extractor's original control and three escape checks retain the audit's acceptance criteria; only the repository/output locations changed. The vulnerable upstream fails the three containment checks. Do not weaken them to accept extraction outside the selected output directory.

The save tests cover cross-language manifests, incomplete/changed payloads, symlinks, missing completion, preservation of other profiles, stale transfers, interrupted-rename recovery, previous-tree retention, copy failure, desktop rollback, and completion upload order/failure. Shell transfer tests substitute a local `xcrun` fixture and never contact a device.

## Human verification on a disposable profile

1. Independently back up active saves on both devices. Use your own game files and development signing identity.
2. Build the updated patch and deploy over the existing app with the same bundle ID. Do not uninstall. Full game/iOS compilation and NativeAOT compatibility are separate from these standalone tests.
3. Close both games. Run `push-save.sh`, then launch the phone game. Confirm profile 1 imports and a different local profile remains intact. Confirm `default/1.previous` contains the old profile.
4. On a disposable inbox transfer, remove the completion marker or change one file. Launch again: the active save must remain unchanged and the invalid inbox must remain present.
5. Save and restore each snapshot slot. Confirm gameplay reload and progression. With the game closed and both trees independently backed up, simulate the interrupted-rename layout by keeping only `<target>.previous`; the next launch should restore `<target>` before normal save reads.
6. Leave both scheduled-write flags at zero and run `sts2_save_sync.sh` with divergent saves: no active save should be overwritten. Review a phone candidate before opting into a direction.
7. Disconnect during a payload upload: the sender must fail without publishing completion. Retry with both games closed.

No physical-device, complete game-build, or real-save verification has been performed by the standalone suite. The rename protocol is recoverable rather than one atomic exchange; keep independent backups.
