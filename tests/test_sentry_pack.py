import importlib.util
from pathlib import Path
import struct
import unittest

spec = importlib.util.spec_from_file_location('pck_patch_sentry',
    Path(__file__).resolve().parents[1] / 'tools/pck_patch_sentry.py')
patcher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(patcher)


def config(properties):
    data = b'ECFG' + struct.pack('<I', len(properties))
    for name, value in properties:
        name = name.encode()
        data += struct.pack('<I', len(name)) + name + struct.pack('<I', len(value)) + value
    return data


class SentryConfigTests(unittest.TestCase):
    def test_current_and_legacy_names_are_disabled(self):
        for name in ('SentryInit', 'SentryBootstrap'):
            with self.subTest(name=name):
                original = config([('autoload/' + name, b'*res://addons/sentry/boot.cs')])
                result, disabled = patcher.disable_sentry_autoloads(original)
                self.assertEqual(['autoload/' + name], disabled)
                self.assertEqual(len(original), len(result))
                self.assertIn(('xutoload/' + name).encode(), result)

    def test_fmod_and_other_values_are_unchanged(self):
        unchanged = [('autoload/FmodManager', b'*res://addons/fmod/FmodManager.cs'),
                     ('description', b'autoload/SentryInit')]
        original = config(unchanged)
        result, disabled = patcher.disable_sentry_autoloads(original)
        self.assertEqual([], disabled)
        self.assertEqual(original, result)

    def test_custom_name_with_sentry_script_is_disabled(self):
        result, disabled = patcher.disable_sentry_autoloads(
            config([('autoload/CrashReporter', b'*res://addons/sentry/boot.cs')]))
        self.assertEqual(['autoload/CrashReporter'], disabled)
        self.assertIn(b'xutoload/CrashReporter', result)

    def test_truncated_config_is_rejected(self):
        original = config([('autoload/SentryBootstrap', b'payload')])
        for length in (0, 7, 9, len(original) - 1):
            with self.subTest(length=length):
                with self.assertRaises(ValueError):
                    patcher.disable_sentry_autoloads(original[:length])

    def test_unknown_format_is_rejected(self):
        with self.assertRaises(ValueError):
            patcher.disable_sentry_autoloads(b'NOPE' + bytes(4))


if __name__ == '__main__':
    unittest.main()
