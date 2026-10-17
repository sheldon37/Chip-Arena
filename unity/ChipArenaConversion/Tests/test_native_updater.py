"""Release-channel validation uses real ZIP payloads; never publishes to a remote."""
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import HTTPError, URLError
import zipfile

PROJECT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('publish_native_update', PROJECT / 'tools/publish_native_update.py')
publisher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publisher)


class NativePublisherTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.feed = self.root / 'feed'
        (self.feed / 'objects').mkdir(parents=True)
        self.channel = self.root / 'channel'

    def payload(self, char='b', revision=2, extra=None, embedded=None):
        build = dict(schema=1, product='chip-arena-unity', channel='native-test', platform='windows-x64', version=char * 64, revision=revision, bootstrap=2)
        files = {'native-build.json': json.dumps(embedded or build).encode(),
                 'Chip Arena.exe': b'fixture', 'UnityPlayer.dll': b'fixture',
                 'Chip Arena_Data/globalgamemanagers': b'fixture'}
        if extra:
            files.update(extra)
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, 'w', zipfile.ZIP_DEFLATED) as z:
            for name, data in files.items():
                # Keep hostile names byte-for-byte; ZipInfo's constructor otherwise
                # sanitizes Windows separators and NULs before the validator sees them.
                row = zipfile.ZipInfo(name)
                row.filename = name
                row.compress_type = zipfile.ZIP_DEFLATED
                z.writestr(row, data)
        data = stream.getvalue()
        digest = hashlib.sha256(data).hexdigest()
        part = dict(name=digest + '-0000.part', sha256=digest, bytes=len(data))
        m = dict(schema=1, build=build, sha256=digest, bytes=len(data), unpackedBytes=sum(map(len, files.values())), files=len(files), parts=[part])
        (self.feed / 'objects' / part['name']).write_bytes(data)
        self.manifest(m)
        return m

    def manifest(self, m):
        (self.feed / 'latest.json').write_text(json.dumps(m))

    def test_stage_preserves_electron_and_previous_native_objects(self):
        (self.channel / 'feed').mkdir(parents=True)
        electron = self.channel / 'feed/latest.json'
        electron.write_text('untouched Electron feed')
        first = self.payload('a', 1)
        publisher.stage(self.feed, self.channel)
        second = self.payload('b', 2)
        publisher.stage(self.feed, self.channel)
        native = self.channel / 'native/windows-x64'
        self.assertTrue((native / 'objects' / first['parts'][0]['name']).is_file())
        self.assertEqual(json.loads((native / 'latest.json').read_text()), second)
        self.assertEqual(electron.read_text(), 'untouched Electron feed')

    def test_reject_same_or_older_publication(self):
        self.payload('b', 2)
        publisher.stage(self.feed, self.channel)
        for char, revision in [('b', 3), ('c', 1)]:
            with self.subTest(char=char):
                self.payload(char, revision)
                with self.assertRaises(ValueError):
                    publisher.stage(self.feed, self.channel)

    def test_reject_tampering_before_staging(self):
        m = self.payload()
        part = self.feed / 'objects' / m['parts'][0]['name']
        data = bytearray(part.read_bytes())
        data[-1] ^= 1
        part.write_bytes(data)
        with self.assertRaisesRegex(ValueError, 'checksum'):
            publisher.stage(self.feed, self.channel)
        self.assertFalse(self.channel.exists())

    def test_reject_unsafe_archive_paths(self):
        for name in ('../escape', '/absolute', 'C:/escape', 'CON.dll', 'bad\\path', 'bad\0suffix', 'UnityPlayer.DLL', 'trailing. '):
            with self.subTest(name=name):
                self.payload(extra={name: b'bad'})
                with self.assertRaises(ValueError):
                    publisher.validate(self.feed)

    def test_reject_wrong_channel_and_embedded_identity(self):
        m = self.payload()
        m['build']['channel'] = 'live'
        self.manifest(m)
        with self.assertRaises(ValueError):
            publisher.validate(self.feed)
        self.payload(embedded=m['build'])
        with self.assertRaisesRegex(ValueError, 'Embedded'):
            publisher.validate(self.feed)

    def test_reject_part_path_and_declared_sizes(self):
        for field, value in [('name', '../outside'), ('bytes', 1)]:
            m = self.payload()
            m['parts'][0][field] = value
            self.manifest(m)
            with self.assertRaises(ValueError):
                publisher.validate(self.feed)

    def test_status_distinguishes_missing_same_older_and_newer_without_writes(self):
        local = self.payload('b', 2)
        for current, expected in [(None, 'HTTP 404'), (local, 'already published'),
                                  (dict(local, build=dict(local['build'], version='c' * 64, revision=3)), 'older build'),
                                  (dict(local, build=dict(local['build'], version='a' * 64, revision=1)), 'can be published')]:
            with self.subTest(expected=expected), patch.object(publisher, 'remote_manifest', return_value=current), patch('sys.stdout', new_callable=io.StringIO) as output:
                self.assertEqual(publisher.main(['--feed', str(self.feed), '--status']), 0)
                self.assertIn(expected, output.getvalue())
                self.assertFalse(self.channel.exists())

    def test_remote_errors_are_not_reported_as_empty_feed(self):
        for code in (404, 403, 500):
            error = HTTPError(publisher.FEED_URL, code, 'fixture', {}, None)
            with self.subTest(code=code), patch.object(publisher, 'urlopen', side_effect=error):
                if code == 404:
                    self.assertIsNone(publisher.remote_manifest())
                else:
                    with self.assertRaises(HTTPError):
                        publisher.remote_manifest()
        with patch.object(publisher, 'urlopen', side_effect=URLError('offline')):
            with self.assertRaises(URLError):
                publisher.remote_manifest()

    def test_remote_read_is_bounded_and_validates_identity(self):
        m = self.payload()
        for data, expected in [(b'x' * (128 * 1024 + 1), 'Manifest too large'),
                               (json.dumps(dict(m, build=dict(m['build'], product='wrong'))).encode(), 'Wrong native')]:
            with self.subTest(expected=expected), patch.object(publisher, 'urlopen', return_value=io.BytesIO(data)) as opening:
                with self.assertRaisesRegex(ValueError, expected):
                    publisher.remote_manifest()
                self.assertEqual(opening.call_args.kwargs['timeout'], 20)
                self.assertEqual(opening.call_args.args[0].full_url, publisher.FEED_URL)

    def build_log(self, build):
        folder = self.root / 'Builds'
        (folder / 'Windows/app').mkdir(parents=True)
        (folder / 'Windows/app/native-build.json').write_text(json.dumps(build))
        log = folder / 'build.log'
        log.write_text('NATIVE_UPDATE_FEED_READY: fixture\nUNITY_TEST_BUILD_READY: fixture\n')
        return log

    def test_missing_failed_or_mismatched_windows_build_stops_before_git(self):
        m = self.payload()
        log = self.build_log(m['build'])
        publisher.validate_build_log(self.feed, log)
        for text in ('', 'NATIVE_UPDATE_FEED_READY: fixture', 'UNITY_TEST_BUILD_READY: fixture'):
            log.write_text(text)
            with patch.object(publisher, 'prepare_channel') as prepare:
                with self.assertRaisesRegex(ValueError, 'both package gates'):
                    publisher.main(['--feed', str(self.feed), '--publish', '--build-log', str(log)])
                prepare.assert_not_called()
        log.write_text('NATIVE_UPDATE_FEED_READY: fixture\nUNITY_TEST_BUILD_READY: fixture\n')
        self.payload('c', 3)
        with self.assertRaisesRegex(ValueError, 'does not match'):
            publisher.validate_build_log(self.feed, log)
        log.unlink()
        with self.assertRaisesRegex(ValueError, 'missing'):
            publisher.validate_build_log(self.feed, log)

    def test_confirmation_cancel_and_eof_do_not_stage_or_push(self):
        self.payload()
        for answer in ('', 'PUBLISH wrong', EOFError()):
            with self.subTest(answer=answer), patch.object(publisher, 'prepare_channel', return_value=None), patch.object(publisher, 'git') as git:
                kwargs = {'side_effect': answer} if isinstance(answer, Exception) else {'return_value': answer}
                with patch('builtins.input', **kwargs):
                    self.assertEqual(publisher.main(['--feed', str(self.feed), '--channel-checkout', str(self.channel), '--publish', '--confirm']), 0)
                git.assert_not_called()
                self.assertFalse(self.channel.exists())

    def test_feed_advanced_during_confirmation_is_rechecked(self):
        m = self.payload()
        with patch.object(publisher, 'prepare_channel', side_effect=[None, m]), patch.object(publisher, 'git') as git, patch('builtins.input', return_value='PUBLISH ' + m['build']['version'][:12]):
            with self.assertRaisesRegex(ValueError, 'already published'):
                publisher.main(['--feed', str(self.feed), '--channel-checkout', str(self.channel), '--publish', '--confirm'])
            git.assert_not_called()

    def test_dry_run_never_calls_git_or_network(self):
        self.payload()
        with patch.object(publisher, 'git') as git, patch.object(publisher, 'urlopen') as network:
            publisher.main(['--feed', str(self.feed), '--channel-checkout', str(self.channel)])
            git.assert_not_called()
            network.assert_not_called()

    def git(self, folder, *args):
        return subprocess.check_output(['git', '-C', str(folder), *args], text=True, stderr=subprocess.PIPE).strip()

    def local_remote(self):
        remote = self.root / 'remote.git'
        remote.mkdir()
        self.git(remote, 'init', '--bare', '--initial-branch=main')
        writer = self.root / 'writer'
        writer.mkdir()
        self.git(writer, 'init', '--initial-branch=main')
        self.git(writer, 'config', 'user.name', 'Native Publisher Test')
        self.git(writer, 'config', 'user.email', 'fixture@example.invalid')
        self.git(writer, 'remote', 'add', 'origin', str(remote))
        (writer / 'feed').mkdir()
        (writer / 'feed/latest.json').write_text('Electron fixture - preserve exactly')
        self.git(writer, 'add', '.')
        self.git(writer, 'commit', '-m', 'Initial Electron fixture')
        self.git(writer, 'push', 'origin', 'main')
        return remote, writer

    def test_real_git_publication_preserves_electron_and_serves_exact_manifest(self):
        remote, writer = self.local_remote()
        m = self.payload()
        identity = {'GIT_AUTHOR_NAME': 'Fixture', 'GIT_AUTHOR_EMAIL': 'fixture@example.invalid', 'GIT_COMMITTER_NAME': 'Fixture', 'GIT_COMMITTER_EMAIL': 'fixture@example.invalid'}
        with patch.object(publisher, 'CHANNEL_REPO', str(remote)), patch.object(publisher, 'remote_manifest', return_value=m), patch.dict(os.environ, identity), patch('builtins.input', return_value='PUBLISH ' + m['build']['version'][:12]):
            self.assertEqual(publisher.main(['--feed', str(self.feed), '--channel-checkout', str(self.channel), '--publish', '--confirm']), 0)
        published = json.loads(self.git(remote, 'show', 'main:native/windows-x64/latest.json'))
        self.assertEqual(published, m)
        self.assertEqual(self.git(remote, 'show', 'main:feed/latest.json'), 'Electron fixture - preserve exactly')
        self.assertEqual(self.git(self.channel, 'status', '--porcelain'), '')
        self.assertEqual(self.git(remote, 'rev-parse', 'main'), self.git(self.channel, 'rev-parse', 'HEAD'))
        changed = self.git(remote, 'diff-tree', '--no-commit-id', '--name-only', '-r', 'main').splitlines()
        self.assertTrue(all(p.startswith('native/windows-x64/') for p in changed))

    def test_real_git_fast_forward_preserves_remote_and_rejects_local_work(self):
        remote, writer = self.local_remote()
        with patch.object(publisher, 'CHANNEL_REPO', str(remote)):
            publisher.prepare_channel(self.channel)
            (writer / 'README.md').write_text('Remote advanced')
            self.git(writer, 'add', '.')
            self.git(writer, 'commit', '-m', 'Advance channel')
            self.git(writer, 'push', 'origin', 'main')
            publisher.prepare_channel(self.channel)
            self.assertEqual(self.git(self.channel, 'rev-parse', 'HEAD'), self.git(writer, 'rev-parse', 'HEAD'))
            (self.channel / 'local-work.txt').write_text('keep me')
            with self.assertRaisesRegex(ValueError, 'clean main'):
                publisher.prepare_channel(self.channel)
            self.assertEqual((self.channel / 'local-work.txt').read_text(), 'keep me')
            self.git(self.channel, 'config', 'user.name', 'Fixture')
            self.git(self.channel, 'config', 'user.email', 'fixture@example.invalid')
            self.git(self.channel, 'add', '.')
            self.git(self.channel, 'commit', '-m', 'Local work')
            local = self.git(self.channel, 'rev-parse', 'HEAD')
            with self.assertRaisesRegex(ValueError, 'unpublished or divergent'):
                publisher.prepare_channel(self.channel)
            self.assertEqual(self.git(self.channel, 'rev-parse', 'HEAD'), local)
            self.git(self.channel, 'remote', 'set-url', 'origin', str(self.root / 'wrong.git'))
            with self.assertRaisesRegex(ValueError, 'approved test-channel'):
                publisher.prepare_channel(self.channel)

    @unittest.skipUnless(os.name == 'nt', 'cmd.exe entrypoint requires Windows')
    def test_windows_wrapper_runs_from_path_with_spaces_and_reports_missing_build(self):
        # Copy only the tools to a clean path, so this cannot publish or consume a real build.
        import shutil
        project = self.root / 'Native project with spaces'
        (project / 'tools').mkdir(parents=True)
        shutil.copyfile(PROJECT / 'Publish-Native.cmd', project / 'Publish-Native.cmd')
        shutil.copyfile(PROJECT / 'tools/publish_native_update.py', project / 'tools/publish_native_update.py')
        for args in ([], ['--status']):
            result = subprocess.run(['cmd.exe', '/d', '/c', 'Publish-Native.cmd', *args], cwd=project, input='\n', capture_output=True, text=True, timeout=30)
            self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
            self.assertIn('No compiled native update package was found', result.stdout + result.stderr)
            self.assertFalse((project / 'Builds').exists())


if __name__ == '__main__':
    unittest.main()
