#!/usr/bin/env python3
"""Package locally compiled iOS FMOD extension objects as a static XCFramework."""
import argparse
import subprocess
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('source', type=Path, help='A fresh fmod-gdextension source checkout built only for iOS arm64 release')
parser.add_argument('output', type=Path, help='New output directory; existing directories are refused')
args = parser.parse_args()
objects = sorted((args.source / 'src').rglob('*.os'))
bindings = args.source / 'godot-cpp/bin/libgodot-cpp.ios.template_release.arm64.a'
if not objects or not bindings.is_file(): parser.error('Build the iOS arm64 template_release objects first')
for obj in objects:
    info = subprocess.check_output(['xcrun', 'vtool', '-show-build', str(obj)], text=True)
    if 'platform IOS\n' not in info: parser.error(f'Non-iOS object found: {obj}')
args.output.mkdir(parents=True, exist_ok=False)
archive = args.output / 'libGodotFmod.ios.template_release.a'
subprocess.run(['xcrun', 'libtool', '-static', '-o', str(archive), *map(str, objects), str(bindings)], check=True)
framework = args.output / 'libGodotFmod.ios.template_release.xcframework'
subprocess.run(['xcrun', 'xcodebuild', '-create-xcframework', '-library', str(archive), '-output', str(framework)], check=True)
print(framework)
