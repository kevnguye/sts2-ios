#!/usr/bin/env python3
"""Apply the native link/signing adjustments to a generated local Xcode project."""
import argparse
import plistlib
import re
import xml.etree.ElementTree as ET
from pathlib import Path

from prepare_ios_export import validate_identity


def finalize(build, team, bundle):
    validate_identity(team, bundle)
    pbx = build / 'StS2.xcodeproj/project.pbxproj'
    text = pbx.read_text().replace('Apple Distribution', 'Apple Development')
    text = re.sub(r'DEVELOPMENT_TEAM = [^;]*;', f'DEVELOPMENT_TEAM = {team};', text)
    text = re.sub(r'PRODUCT_BUNDLE_IDENTIFIER = [^;]*;', f'PRODUCT_BUNDLE_IDENTIFIER = {bundle};', text)
    # Static FMOD must resolve its initializer and Apple's audio frameworks.
    text = text.replace('-Wl,-U,_fmod_library_init', '')
    text = re.sub(r'OTHER_LDFLAGS = "([^"]*)";',
        lambda m: 'OTHER_LDFLAGS = "' + m[1] + (
            '' if '-framework AudioToolbox -framework AVFoundation' in m[1]
            else ' -framework AudioToolbox -framework AVFoundation') + '";', text)
    if '-framework AudioToolbox -framework AVFoundation' not in text:
        raise ValueError('Unexpected linker settings; generated Xcode format may have changed')
    pbx.write_text(text)
    dummy = build / 'StS2/dummy.cpp'
    code = dummy.read_text()
    if 'register_dynamic_symbol((char *)"fmod_library_init"' not in code:
        raise ValueError('The generated project did not register the static FMOD extension')
    if 'load_all_fmod_plugins' not in code:
        dummy.write_text(code + '''
// No custom FMOD plugins are used by this game.
extern "C" __attribute__((visibility("default"))) __attribute__((used))
unsigned int *load_all_fmod_plugins(void *, unsigned int *count) {
    if (count) *count = 0;
    return nullptr;
}
''')
    ent = build / 'StS2/StS2.entitlements'
    with ent.open('rb') as stream: values = plistlib.load(stream)
    values['get-task-allow'] = True
    values['com.apple.developer.kernel.increased-memory-limit'] = True
    with ent.open('wb') as stream: plistlib.dump(values, stream)
    info = build / 'StS2/StS2-Info.plist'
    with info.open('rb') as stream: values = plistlib.load(stream)
    for key in ('NSCameraUsageDescription', 'NSMicrophoneUsageDescription', 'NSPhotoLibraryUsageDescription'):
        if values.get(key) == '': values.pop(key)
    with info.open('wb') as stream: plistlib.dump(values, stream)
    scheme = build / 'StS2.xcodeproj/xcshareddata/xcschemes/StS2.xcscheme'
    tree = ET.parse(scheme)
    launch = tree.find('LaunchAction')
    if launch is None: raise ValueError('Missing Xcode launch configuration')
    launch.set('buildConfiguration', 'Release')
    tree.write(scheme, encoding='utf-8', xml_declaration=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('build', type=Path)
    parser.add_argument('--team', required=True)
    parser.add_argument('--bundle', required=True)
    args = parser.parse_args()
    try: finalize(args.build.resolve(), args.team, args.bundle)
    except (OSError, ValueError, ET.ParseError) as error:
        parser.exit(1, f'Project finalization failed: {error}\n')
