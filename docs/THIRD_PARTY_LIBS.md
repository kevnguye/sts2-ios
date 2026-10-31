# Third-party iOS libraries: sources, versions and outputs

Use the [current pinned build recipe](NATIVE_LIBRARIES.md) for complete commands.
This English reference describes the components from the original guide. Each
has separate licensing terms; no third-party binaries are distributed here.

Version compatibility matters: the FMOD Engine SDK, FMOD Godot extension and Spine
Godot extension must match the game and Godot 4.5.1. A mismatch can produce
`FMOD_ERR_HEADER_MISMATCH`, missing initializer symbols, or audio/animation failures.

| Output | Source | Version family | Initializer |
|---|---|---|---|
| `libfmod_iphoneos.a`, `libfmodstudio_iphoneos.a` | FMOD Engine iOS SDK | 2.03.x; tested recipe pins 2.03.15 | Static link |
| `libGodotFmod.ios.template_release.xcframework` | utopia-rise/fmod-gdextension, built locally | FMOD 2.03 / Godot 4.5.1 | `fmod_library_init` |
| `libspine_godot.ios.template_release.framework` | EsotericSoftware/spine-runtimes, built locally | Spine 4.2 / Godot 4.5.1 | `spine_godot_library_init` |

Put extension frameworks in `STS2_IOS_LIBS_DIR` and the two SDK `.a` files in
`STS2_FMOD_STATIC_DIR`; see `ios-export/config.example.sh`.

## FMOD Engine

Get the iOS Engine SDK, including Core and Studio APIs, directly from
[FMOD Downloads](https://www.fmod.com/download). Register/sign in yourself and
review the applicable [FMOD terms](https://www.fmod.com/legal). The original note
mentioned a free indie tier subject to eligibility and registration; this is not
a determination that your use qualifies. Keep the SDK headers and directory
structure, as well as the device libraries. Simulator libraries are not substitutes.

## FMOD Godot extension

Source: [utopia-rise/fmod-gdextension](https://github.com/utopia-rise/fmod-gdextension).
[Documentation](https://fmod-gdextension.readthedocs.io/).
Build `template_release` for iOS with the matching Godot bindings and locally
obtained FMOD SDK. The current recipe packages a **static** XCFramework for the
Godot 4.5.1 export route. iOS compilation requires macOS/Xcode.

## Spine Godot extension

Source: [EsotericSoftware/spine-runtimes](https://github.com/EsotericSoftware/spine-runtimes),
`4.2` branch, `spine-godot` directory.
[Documentation](https://en.esotericsoftware.com/spine-godot).
Build the matching Godot 4.5.1 iOS `template_release` framework.
Review the applicable Spine runtime/editor licenses yourself; source availability
and owning the game do not establish unrestricted integration or redistribution rights.

## After preparation

Enter the paths in ignored `ios-export/config.sh`. The build script checks inputs
and the private export driver copies the required native libraries into its local
export layout. Missing inputs cause a named error. See the
[export-project library mapping](../ios-export/README.md#library-mapping).
