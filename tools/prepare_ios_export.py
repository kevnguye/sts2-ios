#!/usr/bin/env python3
"""Prepare a private Godot export driver from the user's locally owned files."""
import argparse
import plistlib
import re
import shutil
import subprocess
import tempfile
from pathlib import Path

from pck_ls import read_dir


def validate_identity(team, bundle):
    if not re.fullmatch(r'[A-Z0-9]{10}', team) or team == 'XXXXXXXXXX':
        raise ValueError('Set your actual Apple Team ID in config.sh (not the certificate suffix)')
    if not re.fullmatch(r'[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+', bundle) or bundle == 'com.yourname.sts2':
        raise ValueError('Choose your own bundle identifier in config.sh')


def set_option(text, key, value):
    # Godot CFG string values use compatible quoting for these validated values.
    line = key + '=' + value
    pattern = r'^' + re.escape(key) + r'=.*$'
    return re.sub(pattern, lambda _: line, text, flags=re.M) if re.search(pattern, text, re.M) else text.rstrip() + '\n' + line + '\n'


def prepare(repo, game, libraries, fmod, team, bundle):
    validate_identity(team, bundle)
    export = repo / 'ios-export'
    work = export / '.work'
    work.mkdir(exist_ok=True)
    native = libraries / 'libGodotFmod.ios.template_release.xcframework'
    with (native / 'Info.plist').open('rb') as stream:
        slices = plistlib.load(stream)['AvailableLibraries']
    if not any(s.get('SupportedPlatform') == 'ios' and not s.get('SupportedPlatformVariant')
               and s.get('LibraryPath', '').endswith('.a') for s in slices):
        raise ValueError('FMOD must be a static device XCFramework; see docs/NATIVE_LIBRARIES.md')
    host = game.parent / 'Frameworks'
    sources = {
        native: 'addons/fmod/libs/ios/' + native.name,
        libraries / 'libspine_godot.ios.template_release.framework': 'addons/spine/ios/libspine_godot.ios.template_release.framework',
        fmod / 'libfmod_iphoneos.a': 'addons/fmod/libs/ios/libfmod_iphoneos.a',
        fmod / 'libfmodstudio_iphoneos.a': 'addons/fmod/libs/ios/libfmodstudio_iphoneos.a',
        host / 'libGodotFmod.macos.template_release.framework': 'addons/fmod/libs/macos/libGodotFmod.macos.template_release.framework',
        host / 'libspine_godot.macos.template_release.framework': 'addons/spine/macos/libspine_godot.macos.template_release.framework',
        host / 'libfmod.dylib': 'addons/fmod/libs/macos/libfmod.dylib',
        host / 'libfmodstudio.dylib': 'addons/fmod/libs/macos/libfmodstudio.dylib',
    }
    for source in sources:
        if not source.exists():
            raise FileNotFoundError(f'Required local dependency is missing: {source}')
    # Never modify the checked-in project/preset or the user's original game.
    driver = Path(tempfile.mkdtemp(prefix='export-driver-', dir=work))
    for name in ('sts2.csproj', 'project.godot', 'export_presets.cfg'):
        shutil.copy2(export / name, driver / name)
    project = (driver / 'project.godot').read_text().replace(
        'run/main_scene="res://scenes/game.tscn"', 'run/main_scene="res://bootstrap.tscn"')
    (driver / 'project.godot').write_text(project)
    (driver / 'bootstrap.tscn').write_text('[gd_scene format=3]\n\n[node name="Bootstrap" type="Node"]\n')
    preset = (driver / 'export_presets.cfg').read_text()
    for key, value in {
        'application/app_store_team_id': '"' + team + '"',
        'application/bundle_identifier': '"' + bundle + '"',
        'application/export_project_only': 'true',
        'application/code_sign_identity_debug': '"Apple Development"',
        'application/code_sign_identity_release': '"Apple Development"',
    }.items():
        preset = set_option(preset, key, value)
    (driver / 'export_presets.cfg').write_text(preset)
    (driver / 'export_presets.cfg').chmod(0o600)
    for name in ('prebuilt', '.godot'):
        (driver / name).symlink_to(export / name, target_is_directory=True)
    for source, relative in sources.items():
        target = driver / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        if source.is_dir(): shutil.copytree(source, target)
        else: shutil.copy2(source, target)
    (driver / 'addons/fmod/fmod.gdextension').write_text('''[configuration]
entry_symbol = "fmod_library_init"
compatibility_minimum = "4.5"
[libraries]
macos = "libs/macos/libGodotFmod.macos.template_release.framework"
ios.release = "res://addons/fmod/libs/ios/libGodotFmod.ios.template_release.xcframework"
[dependencies]
ios.release = {
"libs/ios/libfmod_iphoneos.a": "",
"libs/ios/libfmodstudio_iphoneos.a": ""
}
''')
    (driver / 'addons/spine/spine_godot_extension.gdextension').write_text('''[configuration]
entry_symbol = "spine_godot_library_init"
compatibility_minimum = "4.1"
[libraries]
macos = "macos/libspine_godot.macos.template_release.framework"
ios.release = "ios/libspine_godot.ios.template_release.framework"
''')
    with (game / 'Slay the Spire 2.pck').open('rb') as stream:
        icon = next((e for e in read_dir(stream) if e[0] == 'images/icon_1024.png'), None)
        if not icon or icon[3] & 1 or not 0 < icon[2] < 16 * 1024 * 1024:
            raise ValueError('Expected unencrypted game icon missing; game version may be incompatible')
        if icon[1] > stream.seek(0, 2) - icon[2]: raise ValueError('Icon lies outside the pack')
        stream.seek(icon[1]); data = stream.read(icon[2])
        if not data.startswith(b'\x89PNG\r\n\x1a\n'): raise ValueError('Unexpected icon format')
        (driver / 'icon.png').write_bytes(data)
    subprocess.run(['dotnet', 'new', 'sln', '--name', 'sts2'], cwd=driver, check=True, stdout=subprocess.DEVNULL)
    subprocess.run(['dotnet', 'sln', 'sts2.sln', 'add', 'sts2.csproj'], cwd=driver, check=True, stdout=subprocess.DEVNULL)
    return driver


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ('game', 'libraries', 'fmod'): parser.add_argument('--' + key, type=Path, required=True)
    parser.add_argument('--repo', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--team', required=True)
    parser.add_argument('--bundle', required=True)
    args = parser.parse_args()
    try:
        print(prepare(args.repo.resolve(), args.game.resolve(), args.libraries.resolve(),
                      args.fmod.resolve(), args.team, args.bundle))
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        parser.exit(1, f'Export preparation failed: {error}\n')
