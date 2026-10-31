# Security hardening in this fork

The fork retains the upstream license and attribution. The hardening branch addresses four source-level findings from upstream commit `be1144212c7d5ac5d6c78fa56f7cb1d969397389`:

- PCK extraction rejects traversal/absolute paths and uses directory descriptors with no symlink following. Existing hard links are replaced rather than truncated, and file data is copied in bounded chunks.
- Save transfers require a complete manifest and a separately uploaded digest marker. Import verifies each file and preserves other profiles. Scheduled writes require explicit opt-in.
- Save/snapshot replacement retains `.previous`, rolls back a failed commit, and recovers interrupted renames at startup.
- Transfer errors use private temporary directories, avoiding fixed `/tmp` filenames.

See [SAVE_SYNC.md](SAVE_SYNC.md) for the changed transfer contract, migration, recovery and limits. No game assets or credentials are needed for the regression checks in [tests/README.md](../tests/README.md).

## Remaining boundaries

These changes do not certify the project, game binaries or downloaded dependencies as malware-free. The port still uses upstream's Godot 4.5.1/.NET 9 build requirements. The Godot version has public resource-parser reports (CVE-2026-110735 and CVE-2026-110736); upstream disputed the threat classification and merged parser fixes into master on October 20, 2026. A compatible patched engine and iOS templates require a separate build/device assessment. Do not substitute an arbitrary newer engine without testing it.

Sources: [CVE-2026-110735](https://github.com/CVEProject/cvelistV5/blob/main/cves/2026/110xxx/CVE-2026-110735.json), [CVE-2026-110736](https://github.com/CVEProject/cvelistV5/blob/main/cves/2026/110xxx/CVE-2026-110736.json), [Godot binary-loader fix](https://github.com/godotengine/godot/pull/124699).

SideStore, iLoader, LocalDevVPN and their handling of signing accounts or device pairing files have not been audited here. Local Xcode signing avoids adding those optional components to initial testing.

The slim app still loads a writable external PCK. Obtain it from your own trusted Steam copy; directory containment does not make malicious Godot resources safe to execute. Save manifest hashes are integrity checks, not signatures or proof that progression is valid.
