# Native libraries: local build recipe

Use a Mac with Xcode, SCons and CMake. Read the [FMOD license](https://www.fmod.com/legal)
and [Spine 4.2 license](https://github.com/EsotericSoftware/spine-runtimes/blob/4.2/LICENSE)
and obtain the rights required for your use. Source availability does not itself
mean unrestricted use or redistribution. This repository ships no native binaries.

These are the revisions used in the tested setup:

| Component | Revision |
|---|---|
| Godot editor/export templates | 4.5.1 stable mono |
| FMOD Engine iOS SDK | 2.03.15, obtained directly from FMOD |
| FMOD GDExtension | `6.1.0-4.5.0`, commit `fda1f89a08c0048ed313b333f90b7a7258ce2e61` |
| Spine runtimes | 4.2, commit `5cb78e77d3711ddcd80c7bd043bb11cf70fcc88c` |
| godot-cpp for both builds | `e83fd0904c13356ed1d4c3d09f8bb9132bdc6b77` |

The following commands use fresh folders outside the repository. If they already
exist, inspect/reuse the correct checkout rather than deleting your work.

## 1. Keep the complete FMOD iOS SDK

Download **FMOD Engine for iOS 2.03.15** from [FMOD](https://www.fmod.com/download).
Open the downloaded installer/disk image and copy the complete SDK folder to:

```text
~/sts2-ios-userlibs/fmod-ios/
  api/core/inc/
  api/core/lib/libfmod_iphoneos.a
  api/studio/inc/
  api/studio/lib/libfmodstudio_iphoneos.a
```

Folder names may differ in the downloaded installer; the `api/` structure and
these headers/libraries must be present. Copying only the two `.a` files is not
enough for compilation. Then create the expected build layout and the flat library
folder used by the port:

```bash
mkdir -p "$HOME/sts2-ios-userlibs/fmod-sdk" "$HOME/sts2-ios-userlibs/fmod-ios/lib"
ln -s "$HOME/sts2-ios-userlibs/fmod-ios/api" "$HOME/sts2-ios-userlibs/fmod-sdk/ios"
cp "$HOME/sts2-ios-userlibs/fmod-ios/api/core/lib/libfmod_iphoneos.a" "$HOME/sts2-ios-userlibs/fmod-ios/lib/"
cp "$HOME/sts2-ios-userlibs/fmod-ios/api/studio/lib/libfmodstudio_iphoneos.a" "$HOME/sts2-ios-userlibs/fmod-ios/lib/"
```

## 2. Build the FMOD extension

```bash
mkdir -p "$HOME/sts2-ios-userlibs/src" "$HOME/sts2-ios-userlibs/build" "$HOME/sts2-ios-userlibs/ios-libs"
git clone --branch 6.1.0-4.5.0 --recurse-submodules https://github.com/utopia-rise/fmod-gdextension.git "$HOME/sts2-ios-userlibs/src/fmod-gdextension"
git -C "$HOME/sts2-ios-userlibs/src/fmod-gdextension" checkout fda1f89a08c0048ed313b333f90b7a7258ce2e61
git -C "$HOME/sts2-ios-userlibs/src/fmod-gdextension/godot-cpp" fetch origin e83fd0904c13356ed1d4c3d09f8bb9132bdc6b77
git -C "$HOME/sts2-ios-userlibs/src/fmod-gdextension/godot-cpp" checkout e83fd0904c13356ed1d4c3d09f8bb9132bdc6b77
cd "$HOME/sts2-ios-userlibs/src/fmod-gdextension"
scons platform=ios arch=arm64 target=template_release ios_min_version=14.0 fmod_lib_dir="$HOME/sts2-ios-userlibs/fmod-sdk/" target_path="$HOME/sts2-ios-userlibs/build/fmod/" -j4
```

The extension's default output is a dynamic XCFramework. For this Godot 4.5.1
route, package the already compiled iOS objects into a **static** XCFramework
instead; see the [upstream dynamic-loader issue](https://github.com/godotengine/godot/issues/112783).
From this port repository's root, run:

```bash
python3 tools/package_fmod_static.py "$HOME/sts2-ios-userlibs/src/fmod-gdextension" "$HOME/sts2-ios-userlibs/build/fmod-static"
cp -R "$HOME/sts2-ios-userlibs/build/fmod-static/libGodotFmod.ios.template_release.xcframework" "$HOME/sts2-ios-userlibs/ios-libs/"
```

The output directory must not already exist. Use a new output name for a repeat
build. Use a fresh source checkout built only for iOS release: the packager rejects
non-iOS objects. The two separate FMOD SDK `.a` libraries are still required.

## 3. Build the Spine extension

```bash
git clone --branch 4.2 https://github.com/EsotericSoftware/spine-runtimes.git "$HOME/sts2-ios-userlibs/src/spine-runtimes"
git -C "$HOME/sts2-ios-userlibs/src/spine-runtimes" checkout 5cb78e77d3711ddcd80c7bd043bb11cf70fcc88c
cd "$HOME/sts2-ios-userlibs/src/spine-runtimes/spine-godot"
bash build/setup-extension.sh 4.5.1-stable false
git -C godot-cpp fetch origin e83fd0904c13356ed1d4c3d09f8bb9132bdc6b77
git -C godot-cpp checkout e83fd0904c13356ed1d4c3d09f8bb9132bdc6b77
scons platform=ios arch=arm64 target=template_release ios_min_version=14.0 -j4
cp -R bin/ios/libspine_godot.ios.template_release.framework "$HOME/sts2-ios-userlibs/ios-libs/"
```

`setup-extension.sh` replaces its generated local binding/example directories;
use it in the fresh checkout above. Spine's pinned build currently writes an
iOS 12 minimum in its framework even when the SCons argument requests 14; the
app targets iOS 14 or later. Do not change versions or headers to hide a mismatch.

## 4. Verify the results

The port's configuration should point to the two extension directories and the
two FMOD SDK libraries shown in [the beginner guide](BEGINNER_GUIDE.md#3-prepare-the-native-audio-and-animation-libraries).
Use `file`, `xcrun lipo -info` and `nm` to check device ARM64 binaries and the
exported entry points `fmod_library_init` and `spine_godot_library_init`.

Compiling the libraries is only one step. Full game linkage, sound, animation,
and physical-device behavior require separate verification. Keep these native
outputs outside Git; never attach the SDK or a compiled game to an issue/release.
