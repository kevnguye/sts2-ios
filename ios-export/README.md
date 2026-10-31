# STS2 iOS export project

Start with the [beginner guide](../docs/BEGINNER_GUIDE.md). This directory is an
**export driver**, with little content of its own. It prepares an iPhone build by:

1. Compiling the locally woven `sts2.dll` (game assembly plus patch calls) into
   `sts2.framework` with NativeAOT.
2. Using Godot 4.5.1's iOS template for the native application and renderer.
3. Adding locally built FMOD/Spine GDExtensions and your own game content pack.

The actual scenes, cards and assets come from your owned game pack, never from
this repository. Essential settings include `dotnet/project/assembly_name=sts2`
and the game's main scene. The private bootstrap export is followed by replacement
with the locally prepared full game pack.

## Precompiled main-assembly injection

Godot normally runs `dotnet publish sts2.csproj`: csc compiles C# source and ILC
then performs NativeAOT compilation. Here, only the already compiled, locally
woven game assembly is available.

- `EnableDefaultCompileItems=false` gives csc an empty placeholder `sts2.dll`.
- After `CoreCompile` and before `IlcCompile`, an MSBuild target replaces that
  intermediate DLL with the woven game assembly.
- Managed implementation dependencies are provided as references. Framework
  libraries come from the installed SDK/runtime pack rather than copied duplicates.
- Godot roots the main assembly and GodotSharp; the patch assembly is rooted too.
  Do not root every dependency: some desktop-only layouts fail NativeAOT analysis.

ILC therefore compiles the woven assembly. The `sts2` assembly name remains
consistent through export checks, framework packaging and runtime loading.
See [the historical pipeline research](../docs/design-ios-dotnet-export.md) for
source references, including alternatives considered before this injection route.

## Files

- `project.godot`: driver settings.
- `sts2.csproj` / `sts2.sln`: empty-source project and assembly injection target.
- `export_presets.cfg`: baseline iOS export settings.
- `build-ios.sh`: local input checks, weaving, dependencies, AOT, private export
  and pack preparation. Signing/installation is a separate Xcode step.
- `Directory.Build.props`: build and rooting configuration.
- `config.example.sh`: template for ignored, private `config.sh`.

## Library mapping

| Extension | Game/export path | Local output | Initializer |
|---|---|---|---|
| Spine | `ios/libspine_godot.ios.template_release.framework` | Same-named framework in the configured library directory | `spine_godot_library_init` |
| FMOD | `addons/fmod/libs/ios/libGodotFmod.ios.template_release.xcframework` | Same-named static XCFramework | `fmod_library_init` |
| FMOD SDK dependencies | `libs/ios/libfmod_iphoneos.a`, `libfmodstudio_iphoneos.a` | Separate SDK device archives | Static link |
| Sentry | No matching iOS library in this route | Disabled in the prepared local pack | None |
