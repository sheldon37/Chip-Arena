"""Real player ZIP validation plus release/network failure and resume coverage."""
import copy
import json
from pathlib import Path
import sys
from unittest.mock import patch
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import publish_player_download as player
import test_auto_publish as fixtures


class PlayerDownloadTests(unittest.TestCase):
    def setUp(self):
        self.fixture = fixtures.AutoPublishTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        f = self.fixture
        f.unpack()
        self.work = f.work
        (self.work / 'build.json').write_text(json.dumps(f.build))
        manifest = f.fixture.manifest
        self.record = dict(sourceCommit=f.sha, buildNumber=42, buildTarget=player.auto.TARGET,
                           version=manifest['build']['version'], revision=manifest['build']['revision'],
                           sha256=manifest['sha256'], colorSpace='Linear', unityVersion=player.auto.UNITY)
        self.seed = self.work / 'delivery' / player.auto.cloud.SEED
        self.digest = player.auto.cloud.digest(self.seed)
        self.size = self.seed.stat().st_size
        self.asset = dict(name=player.NAME, state='uploaded', size=self.size, digest='sha256:' + self.digest,
                          browser_download_url=player.download_url(self.record))
        self.release = dict(id=12, tag_name=player.tag(self.record), draft=True, prerelease=True,
                            body=player.release_body(self.record, self.digest, self.size), assets=[])
        self.plan = dict(record=self.record, sha256=self.digest, bytes=self.size)
        (self.work / 'player-download.json').write_text(json.dumps(self.plan))
        self.calls = []

    def api(self, path, token=None, method='GET', body=None, upload=None):
        self.calls.append((path, method, body, upload))
        if method == 'GET':
            return [copy.deepcopy(self.release)] if self.release else []
        if upload:
            self.assertEqual(upload, self.seed)
            self.release['assets'] = [self.asset]
            return self.asset
        if method == 'PATCH':
            self.release.update(body)
            return copy.deepcopy(self.release)
        if method == 'POST' and path == '/releases':
            self.release = dict(body, id=12, assets=[])
            return copy.deepcopy(self.release)
        self.fail('Unexpected API request')

    def run_publish(self, record=None, verify_failure=None):
        with patch.object(player, 'published_record', return_value=record or self.record), \
                patch.object(player, 'release_api', side_effect=self.api), \
                patch.object(player, 'verify_public_download', side_effect=verify_failure) as verify:
            player.publish(self.work, 'source-token', 'scoped-feed-token')
        return verify

    def test_clean_first_install_is_exact_published_player_with_launcher_and_marker(self):
        self.assertEqual(player.validate_package(self.work, self.record), self.seed)
        import zipfile
        with zipfile.ZipFile(self.seed) as archive:
            archive.extractall(self.work / 'fresh install')
        root = self.work / 'fresh install/Chip Arena'
        self.assertTrue((root / 'Chip Arena.exe').is_file())
        self.assertTrue((root / 'Chip Arena.exe.config').is_file())
        self.assertEqual((root / 'native-install.txt').read_text(), 'chip-arena-unity/1\n')
        self.assertEqual(json.loads((root / 'app/native-build.json').read_text())['version'], self.record['version'])
        self.assertTrue((root / 'app/ChipArena-Native-Conversion-TEST.exe').is_file())

    def test_prepare_downloads_and_validates_real_completed_cloud_archive(self):
        import shutil
        target = self.work.parent / 'player preparation'
        def api(url, auth):
            if url.endswith('/artifacts'):
                return [{'primary': True, 'files': [{'filename': 'windows.zip'}]}]
            return self.fixture.build
        def download(url, destination, auth, limit):
            shutil.copyfile(self.fixture.provider if '/log?' in url else self.fixture.outer, destination)
        with patch.object(player, 'published_record', return_value=self.record), \
                patch.object(player, 'release_api', return_value=None), \
                patch.object(player.auto, 'api_json', side_effect=api), \
                patch.object(player.auto, 'download_api', side_effect=download):
            player.prepare(target, 'source-token', 'Basic unity-token')
        self.assertEqual(json.loads((target / 'player-download.json').read_text()), self.plan)
        self.assertEqual(player.validate_package(target, self.record).read_bytes(), self.seed.read_bytes())

    def test_prepare_skips_unity_download_for_an_already_verified_release(self):
        self.release.update(draft=False, assets=[self.asset], body=player.release_body(self.record, self.digest, self.size, True))
        with patch.object(player, 'published_record', return_value=self.record), \
                patch.object(player, 'release_api', return_value=self.release), \
                patch.object(player.auto, 'api_json', side_effect=AssertionError('No repeated Unity download')):
            player.prepare(self.work.parent / 'unused', 'source-token', 'Basic unity-token')
        self.assertFalse((self.work.parent / 'unused').exists())

    def test_draft_upload_publication_and_anonymous_verification(self):
        self.run_publish().assert_called_once_with(self.record, self.work / 'public-player.zip', self.size, self.digest)
        self.assertTrue(player.completed_release(self.release, self.record))
        self.assertEqual([method for _, method, _, _ in self.calls], ['GET', 'POST', 'PATCH', 'PATCH'])

    def test_new_release_is_created_as_draft_test_build(self):
        self.release = None
        self.run_publish()
        creation = self.calls[1][2]
        self.assertTrue(creation['draft'])
        self.assertTrue(creation['prerelease'])
        self.assertEqual(creation['make_latest'], 'false')

    def test_verified_release_skips_all_mutations(self):
        self.release.update(draft=False, assets=[self.asset], body=player.release_body(self.record, self.digest, self.size, True))
        self.run_publish().assert_not_called()
        self.assertEqual(len(self.calls), 1)

    def test_uploaded_draft_resumes_without_duplicate_upload(self):
        self.release['assets'] = [self.asset]
        self.run_publish()
        self.assertFalse(any(method == 'POST' for _, method, _, _ in self.calls))

    def test_public_verification_failure_is_not_marked_ready_and_resumes(self):
        with self.assertRaisesRegex(ValueError, 'download failed'):
            self.run_publish(verify_failure=ValueError('download failed'))
        self.assertFalse(player.completed_release(self.release, self.record))
        self.calls = []
        self.run_publish()
        self.assertEqual([m for _, m, _, _ in self.calls], ['GET', 'PATCH'])

    def test_conflicting_uploaded_asset_is_not_overwritten_or_published(self):
        self.release['assets'] = [dict(self.asset, digest='sha256:' + '0'*64)]
        with self.assertRaisesRegex(ValueError, 'does not match'):
            self.run_publish()
        self.assertEqual(len(self.calls), 1)
        self.assertTrue(self.release['draft'])

    def test_changed_source_or_archive_prevents_any_api_mutation(self):
        with self.assertRaisesRegex(ValueError, 'Published build changed'):
            self.run_publish(record=dict(self.record, sourceCommit='b'*40))
        self.assertEqual(self.calls, [])
        self.seed.write_bytes(self.seed.read_bytes() + b'changed')
        with self.assertRaisesRegex(ValueError, 'checksum mismatch'):
            self.run_publish()
        self.assertEqual(self.calls, [])

    def test_unverified_feed_cannot_produce_player_download(self):
        for receipt in (dict(self.record, publicDownloadsVerified=False), dict(self.record, publicDownloadsVerified=True, sha256='0'*64)):
            with patch.object(player.auto.publisher, 'remote_manifest', return_value=self.fixture.fixture.manifest), \
                    patch.object(player.auto, 'api_json', return_value=receipt), self.assertRaises(ValueError):
                player.published_record('source-token')

    def test_public_download_has_no_credentials_and_verifies_exact_bytes(self):
        calls = []
        def request(url, **kwargs):
            calls.append((url, kwargs))
            self.assertNotIn('authorization', kwargs)
            if len(calls) == 1:
                return 302, {'Location': 'https://release-assets.githubusercontent.com/asset?signed=fixture'}, b''
            kwargs['destination'].write_bytes(self.seed.read_bytes())
            return 200, {}, b''
        with patch.object(player.auto, 'request', side_effect=request):
            player.verify_public_download(self.record, self.work / 'public.zip', self.size, self.digest)
        self.assertEqual(len(calls), 2)
        with patch.object(player.auto, 'request', side_effect=request), self.assertRaisesRegex(ValueError, 'checksum or size'):
            player.verify_public_download(self.record, self.work / 'bad.zip', self.size, '0'*64)

    def test_public_download_rejects_unexpected_redirect(self):
        with patch.object(player.auto, 'request', return_value=(302, {'Location': 'https://attacker.test/file'}, b'')), \
                self.assertRaisesRegex(ValueError, 'redirect'):
            player.verify_public_download(self.record, self.work / 'public.zip', self.size, self.digest)

    def test_release_api_refuses_unscoped_endpoint(self):
        with self.assertRaisesRegex(ValueError, 'API path'):
            player.release_api('/other-repo/releases', 'secret', 'POST')


if __name__ == '__main__':
    unittest.main()
