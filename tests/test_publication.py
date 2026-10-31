"""Synthetic checks: publication must reject disguised binaries and credentials."""
import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
from check_publication import inspect
from prepare_ios_export import validate_identity

class PublicationTests(unittest.TestCase):
    def test_plain_patch_source(self):
        self.assertEqual([], inspect('src/Patch.cs', b'namespace Mobile; // patch'))
    def test_disguised_pack(self):
        self.assertTrue(inspect('notes.txt', b'GDPC' + bytes(80)))
    def test_signing_material(self):
        self.assertTrue(inspect('notes.txt', b'-----BEGIN ' + b'PRIVATE KEY-----'))
    def test_local_inputs(self):
        for path in ('game-refs/game.dat', 'ios-export/config.sh', 'out/game.bank'):
            self.assertTrue(inspect(path, b'content'), path)
    def test_game_namespace(self):
        self.assertTrue(inspect('src/Owned.cs', b'namespace ' + b'MegaCrit.Example;'))
    def test_placeholder_identity_rejected(self):
        for team, bundle in [('XXXXXXXXXX','com.example.sts2'),('wrong','com.test.game')]:
            with self.assertRaises(ValueError): validate_identity(team,bundle)
    def test_valid_identity(self):
        validate_identity('ABCDE12345','org.test.mobile')
