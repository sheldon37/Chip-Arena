"""Exercise cloud handoff packaging with real archives, without Unity/account/network access."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
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

    def test_cli_uses_explicit_project_from_an_unrelated_working_directory(self):
        working = self.project / 'Provider working directory'
        working.mkdir()
        result = subprocess.run([sys.executable, str(Path(cloud.__file__).resolve()),
                                 '--project', str(self.project), '--player', str(self.player)],
                                cwd=working, env=dict(os.environ, **self.env),
                                capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        with zipfile.ZipFile(self.player.parent / cloud.DELIVERY) as archive:
            receipt = json.loads(archive.read('cloud-build-receipt.json'))
            self.assertEqual(receipt['sourceCommit'], self.env['BUILD_REVISION'])
            self.assertEqual(receipt['buildLogSha256'], cloud.digest(self.log))

    def test_cygwin_converts_the_native_editor_log_path(self):
        windows_log = r'C:\cloud workspace\Unity Editor.log'
        self.receipt['editorLog'] = windows_log
        self.save_receipt()
        with patch.object(sys, 'platform', 'cygwin'), \
                patch.object(subprocess, 'check_output', return_value=str(self.log) + '\n') as convert:
            output = self.run_package()
        convert.assert_called_once_with(['cygpath', '-ua', windows_log], text=True)
        with zipfile.ZipFile(output) as archive:
            self.assertEqual(archive.read('build.log'), self.log.read_bytes())

    @unittest.skipUnless(os.name == 'posix' and shutil.which('bash'), 'POSIX shell path fixture')
    def test_shell_entrypoint_uses_posix_python_paths_and_propagates_failure(self):
        # Emulate Cygwin's POSIX Python, with paths containing spaces and a provider
        # working directory unrelated to the project. Native tools are not needed.
        tools = self.project / 'tools'
        tools.mkdir()
        for name in ('cloud_post_build.sh', 'package_cloud_build.py', 'publish_native_update.py'):
            shutil.copyfile(Path(cloud.__file__).parent / name, tools / name)
        shims = self.project / 'path shims'
        shims.mkdir()
        converter = shims / 'cygpath'
        converter.write_text('#!/usr/bin/env bash\nset -eu\n'
                             'if [[ "$1" != "-ua" ]]; then echo "Wrong Python path convention" >&2; exit 9; fi\n'
                             'printf "%s\\n" "$2"\n')
        converter.chmod(0o755)
        (shims / 'python').symlink_to(sys.executable)
        working = self.project / 'Provider working directory'
        working.mkdir()
        env = dict(os.environ, **self.env, BUILDER_OS='WINDOWS', UNITY_PLAYER_PATH=str(self.player))
        env['PATH'] = str(shims) + os.pathsep + env['PATH']
        command = ['bash', str(tools / 'cloud_post_build.sh')]
        success = subprocess.run(command, cwd=working, env=env, capture_output=True, text=True)
        self.assertEqual(success.returncode, 0, success.stdout + success.stderr)
        output = self.player.parent / cloud.DELIVERY
        self.assertTrue(output.is_file())
        self.assertIn('NATIVE_CLOUD_DELIVERY_READY:', success.stdout)
        output.unlink()
        output.with_suffix('.zip.sha256').unlink()
        (self.builds / 'cloud-build-receipt.json').unlink()
        failure = subprocess.run(command, cwd=working, env=env, capture_output=True, text=True)
        self.assertNotEqual(failure.returncode, 0)
        self.assertIn('cloud-build-receipt.json', failure.stderr)
        self.assertNotIn('NATIVE_CLOUD_DELIVERY_READY:', failure.stdout)
        self.assertFalse(output.exists())

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
