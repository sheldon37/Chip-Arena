"""Exercise cloud handoff packaging with real archives, without Unity/account/network access."""
import hashlib
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import package_cloud_build as cloud
import test_native_updater as fixtures


class CloudBuildTests(unittest.TestCase):
    def setUp(self):
        self.fixture = fixtures.NativePublisherTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.project = self.fixture.root
        self.manifest = self.fixture.payload()
        self.builds = self.project / 'Builds'
        self.builds.mkdir()
        self.fixture.feed.rename(self.builds / 'NativeUpdateFeed')
        identity = self.builds / 'Windows/app/native-build.json'
        identity.parent.mkdir(parents=True)
        identity.write_text(json.dumps(self.manifest['build']))
        self.env = dict(BUILD_REVISION='a' * 40, BUILD_TARGET='native-windows', UCB_BUILD_NUMBER='42')
        self.log = self.project / 'Unity Editor.log'
        self.log.write_text('NATIVE_UPDATE_FEED_READY: feed\nUNITY_TEST_BUILD_READY: seed\nNATIVE_CLOUD_EXPORT_READY: ' + 'a' * 40 + ' / 42\n')
        self.receipt = dict(schema=1, sourceCommit=self.env['BUILD_REVISION'], buildTarget='native-windows',
                            buildNumber='42', unityVersion='6000.3.24f1', editorLog=str(self.log), build=self.manifest['build'])
        self.save_receipt()
        self.seed()
        self.player = self.project / 'Cloud Export/Build Name.exe'
        self.player.parent.mkdir()
        self.player.write_bytes(b'fixture player')

    def save_receipt(self):
        (self.builds / 'cloud-build-receipt.json').write_text(json.dumps(self.receipt))

    def seed(self, identity=None):
        with zipfile.ZipFile(self.builds / cloud.SEED, 'w') as archive:
            for name in ('ChipArena-Native-Conversion-TEST.exe', 'ChipArena-Native-Conversion-TEST.exe.config', 'READ-ME.txt'):
                archive.writestr(name, 'fixture')
            archive.writestr('native-install.txt', 'chip-arena-unity/1\n')
            part = self.builds / 'NativeUpdateFeed/objects' / self.manifest['parts'][0]['name']
            with zipfile.ZipFile(part) as player:
                for name in player.namelist():
                    data = json.dumps(identity).encode() if name == 'native-build.json' and identity else player.read(name)
                    archive.writestr('app/' + name, data)

    def run_package(self):
        return cloud.package(self.project, self.player, self.env)

    def test_delivery_is_portable_publishable_and_excludes_old_objects(self):
        (self.builds / 'NativeUpdateFeed/objects/old.part').write_bytes(b'old')
        with patch.object(cloud.publisher, 'urlopen', side_effect=AssertionError('Cloud packaging must not access feed')):
            output = self.run_package()
        self.assertIn(hashlib.sha256(output.read_bytes()).hexdigest(), output.with_suffix('.zip.sha256').read_text())
        with zipfile.ZipFile(output) as archive:
            self.assertNotIn('NativeUpdateFeed/objects/old.part', archive.namelist())
            receipt = json.loads(archive.read('cloud-build-receipt.json'))
            self.assertNotIn('editorLog', receipt)
            self.assertFalse(receipt['published'])
            self.assertEqual(receipt['seedSha256'], cloud.digest(self.builds / cloud.SEED))
            unpacked = self.project / 'download'
            archive.extractall(unpacked)
        cloud.publisher.validate_build_log(unpacked / 'NativeUpdateFeed', unpacked / 'build.log')
        self.assertEqual(cloud.publisher.validate(unpacked / 'NativeUpdateFeed'), self.manifest)

    def test_rejects_missing_or_stale_cloud_context(self):
        for variable in self.env:
            with self.subTest(variable=variable):
                original = self.env[variable]
                self.env[variable] = 'different'
                with self.assertRaisesRegex(ValueError, 'context'):
                    self.run_package()
                self.env[variable] = original

    def test_rejects_wrong_editor_and_schema(self):
        for field, value in [('unityVersion', '6000.0.0f1'), ('schema', 2)]:
            original = self.receipt[field]
            self.receipt[field] = value
            self.save_receipt()
            with self.assertRaisesRegex(ValueError, 'schema, commit or Editor'):
                self.run_package()
            self.receipt[field] = original

    def test_rejects_stale_receipt_identity(self):
        self.receipt['build'] = dict(self.manifest['build'], revision=1)
        self.save_receipt()
        with self.assertRaisesRegex(ValueError, 'receipt differs'):
            self.run_package()

    def test_rejects_failed_or_other_run_log(self):
        for text in ('build failed', self.log.read_text().replace(' / 42', ' / 41')):
            self.log.write_text(text)
            with self.assertRaisesRegex(ValueError, 'package gates'):
                self.run_package()

    def test_rejects_mismatched_starter(self):
        self.seed(dict(self.manifest['build'], revision=1))
        with self.assertRaisesRegex(ValueError, 'Starter ZIP differs'):
            self.run_package()

    def test_does_not_overwrite_existing_delivery(self):
        output = self.run_package()
        before = output.read_bytes()
        with self.assertRaisesRegex(ValueError, 'already exists'):
            self.run_package()
        self.assertEqual(output.read_bytes(), before)

    def test_rejects_starter_player_bytes_even_with_matching_identity(self):
        seed = self.builds / cloud.SEED
        with zipfile.ZipFile(seed) as archive:
            files = {name: archive.read(name) for name in archive.namelist()}
        files['app/UnityPlayer.dll'] = b'tamper!'
        with zipfile.ZipFile(seed, 'w') as archive:
            for name, data in files.items():
                archive.writestr(name, data)
        with self.assertRaisesRegex(ValueError, 'Starter ZIP differs'):
            self.run_package()


if __name__ == '__main__':
    unittest.main()
