import importlib.util
import json
from pathlib import Path
import os
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('save_manifest', ROOT / 'tools/save_manifest.py')
save = importlib.util.module_from_spec(spec)
spec.loader.exec_module(save)


class SaveToolsTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix='sts2-tools-test-')
        self.root = Path(self.tmp.name)
        self.source = self.root / 'source'
        self.source.mkdir()
        (self.source / 'profile.save').write_text('original')

    def tearDown(self):
        self.tmp.cleanup()

    def test_stage_and_validate(self):
        target = self.root / 'transfer'
        timestamp = save.stage(self.source, target)
        self.assertEqual(timestamp, save.validate(target))

    def test_missing_profile_does_not_publish_manifest(self):
        (self.source / 'profile.save').unlink()
        with self.assertRaises(ValueError):
            save.stage(self.source, self.root / 'transfer')
        self.assertFalse((self.root / 'transfer/manifest.json').exists())

    def test_symlink_rejected(self):
        (self.source / 'link').symlink_to(self.root, target_is_directory=True)
        with self.assertRaises(ValueError):
            save.stage(self.source, self.root / 'transfer')

    def test_copy_failure_does_not_publish_manifest(self):
        with patch.object(save.shutil, 'copytree', side_effect=OSError('disk full')):
            with self.assertRaises(OSError):
                save.stage(self.source, self.root / 'transfer')
        self.assertFalse((self.root / 'transfer/manifest.json').exists())

    def test_corrupted_payload_rejected(self):
        save.stage(self.source, self.root / 'transfer')
        (self.root / 'transfer/1/profile.save').write_text('damaged')
        with self.assertRaises(ValueError):
            save.validate(self.root / 'transfer')

    def test_replacement_failure_rolls_back(self):
        target = self.root / 'desktop'
        target.mkdir()
        (target / 'profile.save').write_text('old')
        original_rename = Path.rename
        def fail_commit(path, destination):
            if path.name == 'desktop.pending':
                raise OSError('simulated commit failure')
            return original_rename(path, destination)
        with patch.object(Path, 'rename', fail_commit):
            with self.assertRaises(OSError):
                save.replace(self.source, target)
        self.assertEqual('old', (target / 'profile.save').read_text())

    def test_recovery_after_interrupted_rename(self):
        target = self.root / 'desktop'
        shutil.copytree(self.source, self.root / 'desktop.previous')
        save.recover(target)
        self.assertEqual('original', (target / 'profile.save').read_text())

    def test_successful_replace_keeps_previous(self):
        target = self.root / 'desktop'
        target.mkdir()
        (target / 'profile.save').write_text('old')
        save.replace(self.source, target)
        self.assertEqual('old', (self.root / 'desktop.previous/profile.save').read_text())
        self.assertEqual('original', (target / 'profile.save').read_text())

    def test_upload_failure_never_publishes_completion(self):
        scripts = self.root / 'bin'
        scripts.mkdir()
        xcrun = scripts / 'xcrun'
        xcrun.write_text('#!/bin/sh\nprintf "%s\\n" "$*" >> "$CALL_LOG"\nexit 1\n')
        xcrun.chmod(0o700)
        stage = self.root / 'staging'
        stage.mkdir()
        log = self.root / 'calls'
        env = dict(os.environ, PATH=str(scripts) + os.pathsep + os.environ['PATH'], CALL_LOG=str(log))
        result = subprocess.run(['bash', '-c', 'source "$1/save-transfer.sh"; SCRIPT_DIR="$1"; stage_and_upload_save "$2" "$3" fake-device fake-bundle',
                                 'test', str(ROOT / 'ios-export'), str(self.source), str(stage)],
                                env=env, capture_output=True, text=True)
        self.assertNotEqual(0, result.returncode)
        calls = log.read_text().splitlines()
        self.assertEqual(1, len(calls))
        self.assertNotIn('/complete', calls[0])

    def test_completion_is_separate_last_upload(self):
        scripts = self.root / 'bin'
        scripts.mkdir()
        xcrun = scripts / 'xcrun'
        xcrun.write_text('#!/bin/sh\nprintf "%s\\n" "$*" >> "$CALL_LOG"\nexit 0\n')
        xcrun.chmod(0o700)
        stage = self.root / 'staging'
        stage.mkdir()
        log = self.root / 'calls'
        env = dict(os.environ, PATH=str(scripts) + os.pathsep + os.environ['PATH'], CALL_LOG=str(log))
        result = subprocess.run(['bash', '-c', 'source "$1/save-transfer.sh"; SCRIPT_DIR="$1"; stage_and_upload_save "$2" "$3" fake-device fake-bundle',
                                 'test', str(ROOT / 'ios-export'), str(self.source), str(stage)],
                                env=env, capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)
        calls = log.read_text().splitlines()
        self.assertEqual(2, len(calls))
        self.assertNotIn('/complete', calls[0])
        self.assertIn('/complete', calls[1])
        self.assertFalse((stage / 'transfer/complete').exists())


if __name__ == '__main__':
    unittest.main()
