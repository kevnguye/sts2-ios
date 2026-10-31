# What may be published from this repository

This is a source-tool repository, not a distribution of Slay the Spire 2. Users
must obtain their own legitimate game files and the rights required for their
use. Keeping game files out of the repository does **not** by itself establish
that every possible use or redistribution is legally permitted.

## Keep public

- The patch/tool source, build scripts, synthetic tests and setup documentation.
- The upstream copyright/license notices and source/dependency attribution.
- Configuration templates containing placeholders only.

## Keep private and out of Git, issues, releases and CI artifacts

- Original or patched game DLLs, PCKs, extracted scenes/resources, artwork, fonts,
  music, sound banks, animation data and decompiled game source.
- Generated IPAs/apps, game-containing Xcode builds, device containers and saves.
- FMOD SDKs and compiled FMOD/Spine extensions or other locally obtained binaries.
- Apple certificates/private keys/provisioning profiles, local signing/device
  configuration, credentials, access tokens and diagnostic dumps/screenshots.

Use ignored `ios-export/config.sh`, `game-refs/`, `ios-export/.work/`,
`ios-export/build/` and local dependency folders outside the repository. An ignore
rule does not remove something already committed, so inspect history too.

Before publishing changes, run:

```bash
python3 tools/check_publication.py --history
git diff --cached --stat
git diff --cached
```

The checker is a guardrail for excluded files, binary signatures and obvious
credential patterns, not proof of code provenance or copyright compliance.
Review any new documentation images, attachments and third-party material.
The inherited technical-report PDF is hash-allowlisted after visual inspection;
changing it requires a fresh review. Do not add screenshots of the owned game to
this source-only publication without separately reviewing permission.

## Licenses and attribution

- This fork retains the upstream [MIT license](../LICENSE) and attribution to
  [jhaizhou-ops/sts2-ios](https://github.com/jhaizhou-ops/sts2-ios).
- The Android precedent and porting reference is
  [Ekyso/StS2-Launcher](https://github.com/Ekyso/StS2-Launcher), whose
  [MIT notice](https://github.com/Ekyso/StS2-Launcher/blob/main/LICENSE) is retained
  in [Third-Party Notices](../THIRD_PARTY_NOTICES.md).
- FMOD is obtained from its vendor and governed by the
  [FMOD terms](https://www.fmod.com/legal) and [licensing information](https://www.fmod.com/licensing).
- Spine is governed by the [Spine Runtimes license](https://github.com/EsotericSoftware/spine-runtimes/blob/4.2/LICENSE)
  and linked [Spine Editor license](https://esotericsoftware.com/spine-editor-license).
  Its licensing conditions are separate from this repository's MIT license.
- Godot, godot-cpp, Mono.Cecil, .NET and other dependencies retain their respective
  licenses. Their official packages/source repositories provide the applicable notices.
- Inherited advisor tables contain community-sourced ratings and game identifiers;
  their source comments preserve the upstream provenance claims. The publication
  scan does not establish rights in every third-party contribution.

No MegaCrit game license, trademark permission or SDK redistribution right is
granted by this fork's MIT notice. No affiliation or endorsement is implied.

## Review recorded for this fork

On October 31, 2026, the local review covered the candidate source tree and all
125 existing history blobs across 10 commits. The scan found no game DLLs/PCKs,
IPAs, native SDK binaries, signing files or obvious tokens/private keys. The
inherited seven-page report was rendered and inspected as technical documentation;
no game artwork/screenshots were observed. That review is scoped to those files
and does not replace checks on later commits, branches, releases or attachments.

## English technical-report replacement

The Chinese report was replaced in the current tree by
[the English PDF](../share/technical_record.pdf) and
[English HTML source](../share/technical_doc.html). The original remains in Git
history. The translated seven-page PDF was rendered and visually inspected; it
contains text/tables and no embedded images or game assets. Both individually
reviewed report hashes are accepted by the publication guard so history checks
continue to cover the original. Other PDFs still require manual review.

To reproduce the English PDF, install ReportLab in a separate local Python
environment, then run `python3 tools/render_technical_report.py` from the repo root.
The renderer reads only the repository HTML and generates no external images.
