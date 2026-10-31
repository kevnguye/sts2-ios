# Godot 4.5.1 .NET iOS export pipeline: technical design brief

> English translation of July 8, 2026 research against the `4.5.1-stable` source
> tag. This records the original analysis and proposals, including approaches
> later superseded. The implemented route injects the woven **main** assembly;
> do not follow the early proposal to root all desktop dependencies. Current
> instructions are in [the beginner guide](BEGINNER_GUIDE.md).

The research goal was to understand C#/.NET iOS export and substitute an already
compiled game assembly (`sts2.dll`, net9.0, approximately 200 bundled DLLs) for
normal editor-driven compilation from source.

## 0. Overview

The supported Godot C# iOS route uses **NativeAOT**. The editor publishes the csproj
for iOS; Godot.NET.Sdk enables AOT; ILC compiles the main assembly and reachable
referenced IL into `{assembly_name}.dylib`. Export packages it in an AOT XCFramework,
embeds it in Xcode, and runtime loading calls `godotsharp_game_main_init`.

There is no supported Mono interpreter fallback in this pipeline, so runtime
Harmony patching is unsuitable. The initial proposal used a thin project with
precompiled references. The final implementation instead replaces the main
placeholder assembly before ILC reads it, retaining the game's script registration.

## 1. Pipeline, with source references

### 1.1 Export entry: GodotTools ExportPlugin

Source: [ExportPlugin.cs](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/GodotTools/GodotTools/Export/ExportPlugin.cs),
`_ExportBeginImpl()`. Line references below are from the original tag review.

- **L27:** `{assembly_name}.sln` must exist at the project root.
  `ProjectContainsDotNet()` checks `GodotSharpDirs.ProjectSlnPath`; otherwise .NET export is skipped.
- **L189-197:** build `PublishConfig` using ExportDebug/ExportRelease, `RidOS=ios`,
  `UseTempDir=false`, `BundleOutputs=true`. Xcode references the publish outputs,
  so they must remain available.
- **L230-241:** add simulator publication for arm64 and x86_64 with
  `RidOS=iossimulator`, `BundleOutputs=false`. Simulator slices enter the framework,
  not the game data pack.
- **L266-287:** publish into
  `{project}/.godot/mono/temp/bin/godot-publish-dotnet/{config}-{rid}/`, calling
  `BuildManager.PublishProjectBlocking(...)`.
- **L292-307:** verify outputs. iOS expects `{ProjectAssemblyName}.dylib`; missing
  both DLL and dylib outputs causes an error.
- **L316-420:** collect outputs. Exclude the main dylib (separate XCFramework path,
  L331-333) and dSYM directories. `.dat` files such as `icudt.dat` use
  `AddAppleEmbeddedPlatformBundleFile()` to enter the app bundle (L407-410).
  Other outputs use `AddSharedObject()` under `data_{project}_{platform}_{arch}`.
- **L430-457:** combine simulator slices with `lipo`, then call
  `xcodebuild -create-xcframework -library <device> -debug-symbols <dSYM> -library <simulator> ...`.
  [BuildSystem.cs L354-373](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/GodotTools/GodotTools/Build/BuildSystem.cs)
  implements framework generation. Output:
  `.godot/mono/temp/bin/{config}/{assembly_name}_aot.xcframework`.
  `AddAppleEmbeddedPlatformEmbeddedFramework()` registers it for embedding.

The Apple export layer hides `dotnet/embed_build_outputs` on iOS as unsupported;
see [Apple export source L217-220](https://github.com/godotengine/godot/blob/4.5.1-stable/editor/export/editor_export_platform_apple_embedded.cpp).

### 1.2 The publish command

Sources: [BuildSystem.cs L192-243](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/GodotTools/GodotTools/Build/BuildSystem.cs)
and [BuildManager.cs L291-314](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/GodotTools/GodotTools/Build/BuildManager.cs).
The original reconstruction of `BuildPublishArguments` for a Release device export:

```text
dotnet publish {project}/{assembly_name}.csproj \
    -c ExportRelease \
    -r ios-arm64 \
    --self-contained true \
    -v normal \
    -l:GodotTools.BuildLogger.GodotBuildLogger,{editor_tools_dir}/GodotTools.BuildLogger.dll;{logs_dir} \
    -p:GodotTargetPlatform=ios \
    -o {project}/.godot/mono/temp/bin/godot-publish-dotnet/ExportRelease-ios-arm64
```

The logger argument above is schematic; quote its semicolon when adapting it to a shell.
Disabling debug symbols adds `-p:DebugType=None -p:DebugSymbols=false`.
A double-precision engine adds `-p:GodotFloat64=true`. AOT properties come from
MSBuild SDK imports, rather than explicit editor command-line flags. Simulator
publication repeats with `iossimulator-arm64` and `iossimulator-x64`.

### 1.3 SDK-injected AOT settings

The project uses `<Project Sdk="Godot.NET.Sdk/4.5.1">`.
[Sdk.props L107](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/Godot.NET.Sdk/Godot.NET.Sdk/Sdk/Sdk.props)
imports iOSNativeAOT.props for `GodotTargetPlatform=ios`; an `ios...` RID also
infers this platform (L65).

[iOSNativeAOT.props](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/Godot.NET.Sdk/Godot.NET.Sdk/Sdk/iOSNativeAOT.props):

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <PublishAotUsingRuntimePack>true</PublishAotUsingRuntimePack>
  <UseNativeAOTRuntime>true</UseNativeAOTRuntime>
  <TrimmerSingleWarn>false</TrimmerSingleWarn>
</PropertyGroup>
```

[iOSNativeAOT.targets](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/Godot.NET.Sdk/Godot.NET.Sdk/Sdk/iOSNativeAOT.targets),
imported by Sdk.targets L38, roots GodotSharp and the main assembly:

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="GodotSharp" />
  <TrimmerRootAssembly Include="$(TargetName)" />
  <LinkerArg Include="-install_name '@rpath/$(TargetName)$(NativeBinaryExt)'" />
</ItemGroup>
```

`PrepareBeforeIlcCompile` copies `icudt.dat` from the runtime pack, finds Xcode
through `xcode-select`, and supplies the minimum-iOS/sysroot linker settings;
simulator RIDs select the simulator SDK. `FixSymbols` renames the dSYM directory
for XCFramework creation.

The original conclusion: iOS AOT is SDK-driven. The reviewed Godot tree had no
`UseMonoRuntime` path. The game output is a dynamic library/XCFramework, distinct
from the engine's static archive.

### 1.4 Xcode linking and embedding

Source: [Apple export implementation](https://github.com/godotengine/godot/blob/4.5.1-stable/editor/export/editor_export_platform_apple_embedded.cpp).

- L1459-1461 collects embedded frameworks registered by export plugins.
- L1326-1360 converts dylib slices within XCFrameworks into `.framework` bundles,
  placing them under the generated project's `dylibs/` layout and Embed Frameworks.
- The engine itself is the static `libgodot.ios.release.xcframework` (L1843).
  The C# library is loaded at runtime rather than linked directly to that archive.
- `icudt.dat` is copied into the app bundle root (L1470-1471).

### 1.5 Runtime loading on a device

Source: [gd_mono.cpp](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/mono_gd/gd_mono.cpp).
`GDMono::initialize()` tries hostfxr, CoreCLR, then NativeAOT. The reviewed iOS
template contains neither of the first two, leaving `try_load_native_aot_library()`
(L489-517):

1. Read `dotnet/project/assembly_name`, falling back to a sanitized application
   name ([path_utils.cpp L235-250](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/utils/path_utils.cpp)).
2. Construct the dylib name; the
   [Apple dynamic-loader implementation](https://github.com/godotengine/godot/blob/4.5.1-stable/drivers/apple_embedded/os_apple_embedded.mm)
   tries paths ending with `<app>/Frameworks/{assembly}.framework/{assembly}` and dlopen.
3. Resolve `godotsharp_game_main_init`.

[GodotPluginsInitializerGenerator](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/Godot.NET.Sdk/Godot.SourceGenerators/GodotPluginsInitializerGenerator.cs)
generates `GodotPlugins.Game.Main.InitializeFromGameProject` with
`[UnmanagedCallersOnly(EntryPoint="godotsharp_game_main_init")]`. It calls
`ScriptManagerBridge.LookupScriptsInAssembly(typeof(GodotPlugins.Game.Main).Assembly)`:
**script discovery is in the main assembly**. This is why keeping the owned game
as the injected main assembly matters.

## 2. Design questions

### A. Command and properties

The editor runs the publish command in section 1.2; the SDK injects the three
AOT settings in section 1.3. Default roots are GodotSharp and the main target.
Simulator publications also run even for an intended device export; failure there
can fail the whole export.

### B. Thin project plus precompiled references: original proposal

ILC compiles implementation IL in the main assembly and its dependency closure.
The original proposal referenced the owned game's DLL from a small project:

```xml
<Project Sdk="Godot.NET.Sdk/4.5.1">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework> <!-- Must match the game TFM. -->
    <TrimMode>partial</TrimMode>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="sts2"><HintPath>libs/sts2.dll</HintPath></Reference>
    <!-- Add implementation references for the required dependencies. -->
  </ItemGroup>
</Project>
```

This is a historical proposal, **not the implemented project file**. It requires
additional handling of script registration/main assembly identity, as noted above.
The original analysis proposed partial trimming or explicit roots for reflection;
rooting every dependency later proved unsuitable for some desktop-specific types.

The original risk checklist:

1. Godot roots its own assembly and the main project, not every reference. Preserve
   metadata required by reflection. The original proposal cited binary size and
   compile-time costs of rooting, including [proposal #9173](https://github.com/godotengine/godot-proposals/issues/9173).
2. Use **implementation assemblies**, not reference-only NuGet `ref/` assemblies:
   ILC needs method bodies.
3. Match the game TFM. This game's net9.0 needs a net9.0 driver even though the
   reviewed [project generator](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/GodotTools/GodotTools.ProjectEditor/ProjectGenerator.cs)
   defaulted to net8.0. The NativeAOT runtime pack supplies the BCL.
4. Native P/Invoke dependencies still need compatible iOS libraries in Xcode;
   ILC does not manufacture them.

### C. Assembly naming and substitution points

`dotnet/project/assembly_name` controls the expected solution/project path,
export output checks, framework name and runtime loader. The original note
recommended retaining `sts2`. The csproj and solution must be at the expected root;
see [GodotSharpDirs.cs L63-87](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/GodotTools/GodotTools/Internals/GodotSharpDirs.cs).
The reviewed editor offered no general skip-publish or alternate-csproj switch.

The output path progresses from the publish directory to `{assembly}_aot.xcframework`,
the generated project's framework layout, then `Frameworks/{assembly}.framework/{assembly}`
in the installed app. Proposed seams were an MSBuild shim/injection target, or
post-export framework replacement with correct dSYM, install name and signing.
The implemented build uses pre-ILC main-assembly injection.

### D. No supported Mono interpreter fallback

The reviewed Godot 4 pipeline uses hostfxr/CoreCLR on desktop and NativeAOT on iOS;
see the [Godot 4.2 C# platform article](https://godotengine.org/article/platform-state-in-csharp-for-godot-4-2/).
The original review found neither `UseMonoRuntime` nor an interpreter path.
[Proposal #2333](https://github.com/godotengine/godot-proposals/issues/2333)
discusses Mono embedding alternatives, rather than an implemented switch here.

Harmony/MonoMod runtime IL patching needs facilities unavailable through this
NativeAOT route. Build-time Mono.Cecil weaving bakes hooks into a private DLL copy
before publication. The original note considered experimental Mono library-mode
ideas but advised against relying on unconnected, unsupported integration.

### E. NativeAOT risks in reflection-heavy game code

| Feature | Original assessment / required verification |
|---|---|
| Reflect types, fields, properties and private constructors | Requires preserved metadata |
| `RuntimeHelpers.GetUninitializedObject` | Preserve required types and test deserialization |
| Reflection.Emit, DynamicMethod, dynamic assembly loading | Unsupported in the described NativeAOT route |
| Expression compilation | May fall back to slower interpretation |
| Runtime Harmony/MonoMod patching | Unsupported in this route |
| MakeGenericType/MakeGenericMethod | Unseen value-type instantiations can fail when no AOT code was generated |
| Newtonsoft.Json | Original analysis expected a reflection fallback when dynamic code is unavailable; rooting and runtime tests remain necessary |
| System.Text.Json reflection | Needs explicit configuration and compatible metadata; source generation is the usual alternative where source is available |

Original issue examples: [#87752](https://github.com/godotengine/godot/issues/87752)
(reflection/asynchronous loading), [#96072](https://github.com/godotengine/godot/issues/96072)
(Release node instantiation), and [#102747](https://github.com/godotengine/godot/issues/102747)
(project-name spaces).

Treat IL2026/IL2067/IL3050-family warnings as investigation items. Test actual save
reads/writes and game-type instantiation on the phone. The original estimate for
~200 DLLs was minutes of compilation and hundreds of MB before stripping, so CI
needs adequate time/storage. A successful build does not clear runtime warnings.

## 7. Version-specific notes from the original research

- .NET 9 iOS exports in older Godot versions hit an `icudt.dat` runtime-pack path
  change ([#100123](https://github.com/godotengine/godot/issues/100123)); the research
  cited [fix #100187](https://github.com/godotengine/godot/pull/100187) in 4.5's targets.
- C# iOS support originated in 4.2 ([#82729](https://github.com/godotengine/godot/pull/82729)).
  The reviewed docs described platform support as experimental. Simulator slices
  and actual native-extension architectures must match the templates in use.
- Export needs macOS/Xcode for lipo, xcodebuild and the iPhone SDK; see
  [Godot iOS export documentation](https://docs.godotengine.org/en/4.5/tutorials/export/exporting_for_ios.html).
- The original iteration note mentioned the Info.plist `godot_path` setting for
  local resources; C# changes still require AOT publication.

## 8. Source and reference index

**4.5.1-stable source** (line numbers refer to this tag)：
- [ExportPlugin.cs](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/GodotTools/GodotTools/Export/ExportPlugin.cs) · [BuildSystem.cs](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/GodotTools/GodotTools/Build/BuildSystem.cs) · [BuildManager.cs](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/GodotTools/GodotTools/Build/BuildManager.cs) · [GodotSharpDirs.cs](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/GodotTools/GodotTools/Internals/GodotSharpDirs.cs)
- [Sdk.props](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/Godot.NET.Sdk/Godot.NET.Sdk/Sdk/Sdk.props) · [Sdk.targets](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/Godot.NET.Sdk/Godot.NET.Sdk/Sdk/Sdk.targets) · [iOSNativeAOT.props](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/Godot.NET.Sdk/Godot.NET.Sdk/Sdk/iOSNativeAOT.props) · [iOSNativeAOT.targets](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/Godot.NET.Sdk/Godot.NET.Sdk/Sdk/iOSNativeAOT.targets)
- [GodotPluginsInitializerGenerator.cs](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/Godot.NET.Sdk/Godot.SourceGenerators/GodotPluginsInitializerGenerator.cs) · [ProjectGenerator.cs](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/editor/GodotTools/GodotTools.ProjectEditor/ProjectGenerator.cs)
- [gd_mono.cpp](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/mono_gd/gd_mono.cpp) · [godotsharp_dirs.cpp](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/godotsharp_dirs.cpp) · [path_utils.cpp](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/mono/utils/path_utils.cpp)
- [editor_export_platform_apple_embedded.cpp](https://github.com/godotengine/godot/blob/4.5.1-stable/editor/export/editor_export_platform_apple_embedded.cpp) · [os_apple_embedded.mm](https://github.com/godotengine/godot/blob/4.5.1-stable/drivers/apple_embedded/os_apple_embedded.mm)

**Documentation / official articles**：[C# platform support (4.5 docs)](https://docs.godotengine.org/en/4.5/tutorials/scripting/c_sharp/index.html) · [iOS export (4.5 docs)](https://docs.godotengine.org/en/4.5/tutorials/export/exporting_for_ios.html) · [Godot 4.2 C# platform status](https://godotengine.org/article/platform-state-in-csharp-for-godot-4-2/)

**Issues / PRs**：[#82729 Add C# iOS support](https://github.com/godotengine/godot/pull/82729) · [#100123 .NET 9 iOS icudt](https://github.com/godotengine/godot/issues/100123) / [fix #100187](https://github.com/godotengine/godot/pull/100187) · [#87752 NativeAOT asynchronous resource loading](https://github.com/godotengine/godot/issues/87752) · [#96072 NativeAOT Release node instantiation](https://github.com/godotengine/godot/issues/96072) · [#102747 project-name spaces](https://github.com/godotengine/godot/issues/102747) · [proposals#9173 reflection-free mode](https://github.com/godotengine/godot-proposals/issues/9173) · [proposals#2333 Mono embedding return proposal](https://github.com/godotengine/godot-proposals/issues/2333)
