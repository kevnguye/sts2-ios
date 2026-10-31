"""Check exported-project repairs against a disposable synthetic Xcode fixture."""
import plistlib
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
from finalize_ios_project import finalize

class IosProjectTests(unittest.TestCase):
    def test_repeatable_native_audio_and_signing_setup(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            project = root / 'StS2.xcodeproj'
            scheme = project / 'xcshareddata/xcschemes/StS2.xcscheme'
            scheme.parent.mkdir(parents=True)
            scheme.write_text('<Scheme><LaunchAction buildConfiguration="Debug" /></Scheme>')
            pbx = project / 'project.pbxproj'
            pbx.write_text('DEVELOPMENT_TEAM = OLD; PRODUCT_BUNDLE_IDENTIFIER = old.app; '
                'OTHER_LDFLAGS = "-Wl,-U,_fmod_library_init"; '
                'lastKnownFileType = sourcecode.cpp.cpp; path = dummy.cpp;')
            app = root / 'StS2'; app.mkdir()
            (app / 'dummy.cpp').write_text('register_dynamic_symbol((char *)"fmod_library_init", 0);')
            for name, values in [('StS2.entitlements', {}), ('StS2-Info.plist', {
                'NSCameraUsageDescription':'', 'NSMicrophoneUsageDescription':'Existing meaningful explanation'})]:
                (app / name).write_bytes(plistlib.dumps(values))
            finalize(root,'ABCDE12345','org.test.mobile')
            once = pbx.read_text()
            finalize(root,'ABCDE12345','org.test.mobile')
            self.assertEqual(once, pbx.read_text())
            self.assertIn('sourcecode.cpp.objcpp', once)
            self.assertNotIn('-Wl,-U,_fmod_library_init', once)
            self.assertIn('-framework AVFoundation', once)
            code = (app / 'dummy.cpp').read_text()
            self.assertEqual(code.count('#include "MobileAudio.h"'), 1)
            self.assertEqual(code.count('unsigned int *load_all_fmod_plugins'), 1)
            self.assertTrue((app / 'MobileAudio.h').exists())
            info = plistlib.loads((app / 'StS2-Info.plist').read_bytes())
            self.assertNotIn('NSCameraUsageDescription', info)
            self.assertEqual(info['NSMicrophoneUsageDescription'], 'Existing meaningful explanation')
            self.assertNotIn('UIBackgroundModes', info)
            self.assertTrue(plistlib.loads((app / 'StS2.entitlements').read_bytes())['get-task-allow'])
            self.assertIn('buildConfiguration="Release"', scheme.read_text())

            launch = ET.parse(scheme).getroot().find('LaunchAction')
            self.assertEqual(launch.get('customLLDBInitFile'), '$(SRCROOT)/StS2/NativeAot.lldbinit')
            init = app / 'NativeAot.lldbinit'
            self.assertEqual(init.read_bytes(), (Path(__file__).resolve().parents[1] /
                'ios-export/native/NativeAot.lldbinit').read_bytes())
            commands = [line for line in init.read_text().splitlines()
                        if line.strip() and not line.lstrip().startswith('#')]
            self.assertEqual(commands, ['process handle --pass true --stop false --notify false SIGUSR1'])
