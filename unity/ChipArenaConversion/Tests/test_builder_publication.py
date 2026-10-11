"""Real-Git publication/retry tests against a local bare remote; never use live feeds."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import unittest
from unittest.mock import patch

PROJECT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PROJECT / 'Builder'))
import builder
import native_publish
import test_native_updater as fixtures


class BuilderPublicationTests(unittest.TestCase):
    def setUp(self):
        self.fixture = fixtures.NativePublisherTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.root = self.fixture.root
        self.writer = self.root / 'remote writer'
        self.writer.mkdir()
        self.remote = self.root / 'remote.git'
        self.cache = self.root / 'Cache/native-publisher-v2.git'
        self.old = self.fixture.payload('a', 1)
        fixtures.publisher.stage(self.fixture.feed, self.writer)
        (self.writer / 'unrelated.txt').write_text('keep this')
        (self.writer / 'unicode-\u03bb.txt').write_text('keep Unicode filename')
        (self.writer / 'feed').mkdir()
        (self.writer / 'feed/latest.json').write_text('legacy feed')
        self.git(self.writer, 'init', '-b', 'main')
        self.git(self.writer, 'config', 'user.name', 'Fixture')
        self.git(self.writer, 'config', 'user.email', 'fixture@example.invalid')
        self.git(self.writer, 'add', '.')
        self.git(self.writer, 'update-index', '--chmod=+x', 'unrelated.txt')
        self.git(self.writer, 'commit', '-m', 'Existing channel')
        self.git(self.root, 'clone', '--bare', str(self.writer), str(self.remote))
        self.git(self.remote, 'config', 'uploadpack.allowFilter', 'true')
        self.git(self.writer, 'remote', 'add', 'origin', str(self.remote))
        self.base = self.head()
        self.local = self.fixture.payload('b', 2)
        patcher = patch.object(native_publish, 'CHANNEL_REPO', self.remote.as_uri())
        patcher.start()
        self.addCleanup(patcher.stop)
        public = patch.object(fixtures.publisher, 'remote_manifest', side_effect=self.current)
        public.start()
        self.addCleanup(public.stop)

    def git(self, folder, *args):
        return subprocess.check_output(['git', '-C', str(folder), *args], stderr=subprocess.STDOUT).decode().strip()

    def head(self):
        return self.git(self.remote, 'rev-parse', 'refs/heads/main')

    def current(self):
        return json.loads(self.git(self.remote, 'show', 'main:' + native_publish.FEED_PATH))

    def publish(self, run=builder.run):
        return native_publish.publish(self.fixture.feed, self.cache, self.local, fixtures.publisher, run)

    def test_publishes_exact_payload_and_preserves_unrelated_trees_and_old_objects(self):
        legacy = self.root / 'Cache/native-channel'
        legacy.mkdir(parents=True)
        (legacy / 'dirty-owner-work.txt').write_text('never reset or rename')
        result = self.publish()
        self.assertEqual(result['status'], 'published')
        self.assertTrue(result['publicFeedVerified'])
        self.assertEqual(self.current(), self.local)
        for path in ('unrelated.txt', 'unicode-\u03bb.txt', 'feed', 'native/windows-x64/objects/' + self.old['parts'][0]['name']):
            self.assertEqual(self.git(self.remote, 'ls-tree', self.base, path),
                             self.git(self.remote, 'ls-tree', self.head(), path))
        part = self.local['parts'][0]
        saved = subprocess.check_output(['git', '-C', str(self.remote), 'show', 'main:native/windows-x64/objects/' + part['name']])
        self.assertEqual(saved, (self.fixture.feed / 'objects' / part['name']).read_bytes())
        self.assertEqual((legacy / 'dirty-owner-work.txt').read_text(), 'never reset or rename')
        self.assertEqual(self.git(self.remote, 'show', '-s', '--format=%an <%ae>', 'main'),
                         'Chip Arena Builder <builder@chip-arena.invalid>')
        old_blob = self.git(self.remote, 'rev-parse', 'main:native/windows-x64/objects/' + self.old['parts'][0]['name'])
        missing = self.git(self.cache, 'rev-list', '--objects', '--all', '--missing=print')
        self.assertIn('?' + old_blob, missing)  # old binary was never downloaded

    def test_already_published_source_is_success_without_a_duplicate_commit(self):
        self.publish()
        head = self.head()
        self.local = self.fixture.payload('b', 3)  # same source rebuilt later
        result = self.publish()
        self.assertEqual(result['status'], 'already-published')
        self.assertTrue(result['publicFeedVerified'])
        self.assertEqual(self.head(), head)

    def test_failed_upload_can_retry_with_the_same_cache_and_saved_feed(self):
        def fail_push(args, **kwargs):
            if 'push' in args:
                self.assertIn('http.version=HTTP/1.1', args)
                self.assertIn('http.postBuffer=134217728', args)
                raise subprocess.CalledProcessError(1, args)
            return builder.run(args, **kwargs)
        with self.assertRaisesRegex(ValueError, 'no rebuild is needed'):
            self.publish(fail_push)
        self.assertEqual(self.head(), self.base)
        self.assertTrue(self.cache.exists())
        self.assertEqual(self.publish()['status'], 'published')

    def test_lost_acknowledgement_after_accepted_upload_is_verified(self):
        def lost_response(args, **kwargs):
            result = builder.run(args, **kwargs)
            if 'push' in args:
                raise subprocess.CalledProcessError(1, args)
            return result
        self.assertEqual(self.publish(lost_response)['status'], 'published')
        self.assertEqual(self.current(), self.local)

    def test_concurrent_channel_change_is_not_overwritten_or_force_pushed(self):
        def competing_publisher(args, **kwargs):
            if 'push' in args:
                self.assertFalse(any(str(arg).startswith('--force') for arg in args))
                (self.writer / 'unrelated.txt').write_text('concurrent owner change')
                self.git(self.writer, 'add', 'unrelated.txt')
                self.git(self.writer, 'commit', '-m', 'Concurrent update')
                self.git(self.writer, 'push', 'origin', 'main')
            return builder.run(args, **kwargs)
        with self.assertRaisesRegex(ValueError, 'no rebuild is needed'):
            self.publish(competing_publisher)
        self.assertEqual(self.git(self.remote, 'show', 'main:unrelated.txt'), 'concurrent owner change')
        self.assertEqual(self.current(), self.old)
        self.assertEqual(self.publish()['status'], 'published')
        self.assertEqual(self.git(self.remote, 'show', 'main:unrelated.txt'), 'concurrent owner change')

    def test_old_build_and_corrupt_feed_cannot_publish(self):
        self.publish()
        head = self.head()
        self.local = self.fixture.payload('c', 1)
        with self.assertRaisesRegex(ValueError, 'older build'):
            self.publish()
        self.local = self.fixture.payload('c', 3)
        part = self.fixture.feed / 'objects' / self.local['parts'][0]['name']
        part.write_bytes(b'corrupted')
        with self.assertRaises(ValueError):
            self.publish()
        self.assertEqual(self.head(), head)

    def test_wrong_cache_remote_is_preserved_and_refused(self):
        self.publish()
        self.git(self.cache, 'remote', 'set-url', 'origin', 'https://example.invalid/other.git')
        with self.assertRaisesRegex(ValueError, 'another repository'):
            self.publish()
        self.assertEqual(self.git(self.cache, 'remote', 'get-url', 'origin'), 'https://example.invalid/other.git')

    def test_public_feed_lag_does_not_report_public_readiness(self):
        with patch.object(fixtures.publisher, 'remote_manifest', return_value=self.old):
            result = self.publish()
        self.assertFalse(result['publicFeedVerified'])
        self.assertEqual(result['status'], 'published')

    def test_silent_git_launcher_is_rejected_even_when_exit_code_is_zero(self):
        wrapper = self.root / 'silent-git.exe'
        actual = self.root / 'real-git.exe'
        wrapper.touch()
        actual.touch()
        results = [subprocess.CompletedProcess([], 0, stdout='', stderr=''),
                   subprocess.CompletedProcess([], 0, stdout='git version 2.56.0.windows.2\n', stderr='')]
        with patch.object(builder.subprocess, 'run', side_effect=results):
            self.assertEqual(builder.select_git([wrapper, actual]), str(actual))
        with patch.object(builder.subprocess, 'run', return_value=results[0]):
            with self.assertRaisesRegex(ValueError, 'No working Git'):
                builder.select_git([wrapper])


if __name__ == '__main__':
    unittest.main()
