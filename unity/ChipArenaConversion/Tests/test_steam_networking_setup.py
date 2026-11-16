import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import configure_steam_multiplayer as configure
import package_steam_playtest as depot
from package_source import validate_dependencies


class SteamNetworkingSetupTests(unittest.TestCase):
    def test_profiles_and_idempotent_package(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'Packages').mkdir()
            (root / 'Assets').mkdir()
            manifest = root / 'Packages/manifest.json'
            manifest.write_text('{"dependencies":{"existing":"1"}}')
            (root / 'Assets/csc.rsp').write_text('-define:EXISTING\n')
            for steam in (True, False):
                configure.configure(root, 123, steam)
                self.assertEqual(json.loads(manifest.read_text())['dependencies'], {'existing':'1', configure.PACKAGE:configure.PIN})
                self.assertEqual(json.loads((root/'Assets/StreamingAssets/ChipArenaSteam.json').read_text()),{'appId':123,'defaultSteam':steam})
            self.assertEqual((root/'Assets/csc.rsp').read_text().splitlines(),['-define:EXISTING','-define:CHIP_ARENA_STEAM'])
            with self.assertRaises(ValueError):
                configure.configure(root,0,True)

    def test_source_packager_accepts_only_pinned_steam_package(self):
        catalog = {'editor_version': 'fixture', 'packages': {'engine': '1'}}
        deps = {'engine': '1', configure.PACKAGE: configure.PIN}
        validate_dependencies(deps, catalog)
        deps[configure.PACKAGE] = 'unversioned'
        with self.assertRaisesRegex(ValueError, 'Unsupported dependency'):
            validate_dependencies(deps, catalog)

    def test_depot_checks_and_removes_development_override(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root/'steam_appid.txt').write_text('123')
            self.assertIsNone(depot.prepare_networking(root,'Chip Arena.exe',None))
            self.assertFalse((root/'steam_appid.txt').exists())
            data = root/'Chip Arena_Data'
            config = data/'StreamingAssets/ChipArenaSteam.json'
            config.parent.mkdir(parents=True)
            config.write_text('{"appId":123}')
            with self.assertRaisesRegex(ValueError,'missing'):
                depot.prepare_networking(root,'Chip Arena.exe','123')
            (data/'steam_api64.dll').touch()
            with self.assertRaisesRegex(ValueError,'differ'):
                depot.prepare_networking(root,'Chip Arena.exe','124')
            self.assertEqual(depot.prepare_networking(root,'Chip Arena.exe',None),'123')
