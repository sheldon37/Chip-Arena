"""Real delivery ZIPs and failure injection; no remote requests or publication."""
import io
import json
import os
from pathlib import Path
import sys
import shutil
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import auto_publish as auto
import test_cloud_build as cloud_fixtures


class AutoPublishTests(unittest.TestCase):
    def setUp(self):
        self.fixture = cloud_fixtures.CloudBuildTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.sha = 'a' * 40
        self.build = dict(build=42, lastBuiltRevision=self.sha, scmBranch=auto.BRANCH,
                          buildtargetid=auto.TARGET, buildStatus='success',
                          platform='standalonewindows64', unityVersion=auto.UNITY)
        f = self.fixture
        f.env['BUILD_TARGET'] = auto.TARGET
        f.receipt.update(buildTarget=auto.TARGET, colorSpace='Linear')
        f.save_receipt()
        f.log.write_text(f.log.read_text() + 'NATIVE_COLOR_SPACE: Linear\n')
        self.delivery_zip = f.run_package()
        self.outer = f.project / 'provider.zip'
        self.repack_outer()
        self.work = f.project / 'work'
        self.work.mkdir()
        self.provider = self.work / 'provider.log'
        self.provider.write_text(f.log.read_text() + 'NATIVE_CLOUD_DELIVERY_READY: fixture\n')

    def repack_outer(self):
        with zipfile.ZipFile(self.outer, 'w') as z:
            for p in (self.delivery_zip, self.delivery_zip.with_suffix('.zip.sha256')):
                z.write(p, 'windows/' + p.name)
            z.writestr('windows/raw-player.exe', 'unexecuted')

    def unpack(self):
        return auto.unpack_delivery(self.outer, self.work)

    def test_accepts_complete_receipt_and_real_archives(self):
        result, receipt = auto.validate_delivery(self.unpack(), self.build, self.provider)
        self.assertEqual(result, self.fixture.manifest)
        self.assertEqual(receipt['sourceCommit'], self.sha)

    def test_rejects_wrong_branch_commit_status_platform_or_unity(self):
        for key, value in dict(scmBranch='claude/work', lastBuiltRevision='b'*40, buildStatus='failure',
                               platform='webgl', unityVersion='6000.3.25f1', deleted=True,
                               deletedArtifacts=True, deletedLogs=True, headless=True).items():
            with self.subTest(key=key), self.assertRaises(ValueError):
                auto.check_build(dict(self.build, **{key: value}), self.sha)

    def test_rejects_unity_test_and_build_errors(self):
        for extra in ({'testResults': {'unit_test': {'failed': 1}}}, {'buildReport': {'errors': 1}}):
            with self.subTest(extra=extra), self.assertRaises(ValueError):
                auto.check_build(dict(self.build, **extra), self.sha)

    def test_metadata_diagnostic_excludes_links_and_credentials(self):
        build = dict(self.build, unityVersion='latest', links={'download': 'secret-url'},
                     environment={'TOKEN': 'secret-value'})
        with patch('builtins.print') as printed:
            auto.describe_build(build, self.sha, 'list')
        diagnostic = printed.call_args.args[0]
        self.assertNotIn('secret', diagnostic)
        self.assertEqual(auto.build_rejections(build, self.sha), ['unityVersion'])
        self.assertIn('latest', diagnostic)

    def test_accepts_real_uba_version_id_but_rejects_other_editor_versions(self):
        # UBA #7 list and detail responses observed on 2026-10-09.
        build = dict(self.build, unityVersion='6000_3_24f1', localUnityVersion='6000.3.24f1')
        auto.check_build(build, self.sha)
        for extra in ({'unityVersion': '6000_3_25f1'}, {'unityVersion': 'latest'},
                      {'unityVersion': '6000_3.24f1'}, {'localUnityVersion': '6000.3.25f1'}):
            with self.subTest(extra=extra), self.assertRaises(ValueError):
                auto.check_build(dict(build, **extra), self.sha)

    def test_receipt_rejects_wrong_context_and_gamma(self):
        delivery = self.unpack()
        path = delivery / 'cloud-build-receipt.json'
        original = json.loads(path.read_text())
        for key, value in dict(sourceCommit='b'*40, buildTarget='other', buildNumber='43', colorSpace='Gamma', published=True).items():
            path.write_text(json.dumps(dict(original, **{key: value})))
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, 'receipt mismatch'):
                auto.validate_delivery(delivery, self.build, self.provider)
        path.write_text(json.dumps(original))

    def test_refuses_green_provider_without_packaging_marker(self):
        delivery = self.unpack()
        self.provider.write_text(self.fixture.log.read_text() + 'Finished: SUCCESS\n')
        with self.assertRaisesRegex(ValueError, 'packaging gate'):
            auto.validate_delivery(delivery, self.build, self.provider)

    def test_rejects_compiler_and_shader_errors_in_provider_log(self):
        delivery = self.unpack()
        original = self.provider.read_text()
        for text in ("Shader error in 'NativePortrait': syntax error", 'file.cs: error CS0103: unknown'):
            self.provider.write_text(original + text + '\n')
            with self.subTest(text=text), self.assertRaisesRegex(ValueError, 'Compiler/shader'):
                auto.validate_delivery(delivery, self.build, self.provider)

    def test_rejects_modified_seed_log_and_feed_parts(self):
        delivery = self.unpack()
        paths = [delivery / auto.cloud.SEED, delivery / 'build.log',
                 delivery / 'NativeUpdateFeed/objects' / self.fixture.manifest['parts'][0]['name']]
        for path in paths:
            data = path.read_bytes()
            path.write_bytes(data + b'changed')
            with self.subTest(path=path.name), self.assertRaises(ValueError):
                auto.validate_delivery(delivery, self.build, self.provider)
            path.write_bytes(data)

    def test_rejects_extra_delivery_content(self):
        delivery = self.unpack()
        (delivery / 'script.py').write_text('must never run')
        with self.assertRaisesRegex(ValueError, 'inventory'):
            auto.validate_delivery(delivery, self.build, self.provider)

    def test_rejects_bad_outer_checksum(self):
        self.delivery_zip.with_suffix('.zip.sha256').write_text('0'*64 + '  ' + auto.cloud.DELIVERY)
        self.repack_outer()
        with self.assertRaisesRegex(ValueError, 'checksum mismatch'):
            self.unpack()

    def test_rejects_unsafe_duplicate_and_symlink_zip_members(self):
        for name in ('../escape', '/absolute', 'C:/escape', 'bad\\path', 'CON.txt'):
            with self.subTest(name=name):
                stream = io.BytesIO()
                with zipfile.ZipFile(stream, 'w') as z:
                    # Preserve the hostile bytes: ZipInfo otherwise repairs Windows
                    # backslashes before the fixture ever reaches the validator.
                    row = zipfile.ZipInfo(name)
                    row.filename = name
                    z.writestr(row, 'bad')
                with zipfile.ZipFile(stream) as z, self.assertRaises(ValueError):
                    auto.safe_entries(z)
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, 'w') as z:
            z.writestr('a', 'one')
            z.writestr('A', 'two')
        with zipfile.ZipFile(stream) as z, self.assertRaisesRegex(ValueError, 'Duplicate'):
            auto.safe_entries(z)
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, 'w') as z:
            row = zipfile.ZipInfo('link')
            row.external_attr = 0o120777 << 16
            z.writestr(row, '../outside')
        with zipfile.ZipFile(stream) as z, self.assertRaisesRegex(ValueError, 'Non-file'):
            auto.safe_entries(z)

    def test_download_handles_303_json_without_forwarding_credentials(self):
        responses = [(303, {}, b'{"url":"https://storage.googleapis.com/bucket/signed"}'), (200, {}, b'')]
        with patch.object(auto, 'request', side_effect=responses) as request:
            auto.download_api(auto.BASE + '/42/download/file.zip', self.work / 'test.zip', 'Basic secret', 123)
        self.assertEqual(request.call_args_list[0].args[1], 'Basic secret')
        self.assertNotIn('authorization', request.call_args_list[1].kwargs)
        self.assertEqual(len(request.call_args_list[1].args), 1)

    def test_download_handles_log_location_without_forwarding_credentials(self):
        responses = [(307, {'Location': 'https://storage.googleapis.com/bucket/log'}, b''), (200, {}, b'')]
        with patch.object(auto, 'request', side_effect=responses):
            auto.download_api(auto.BASE + '/42/log', self.work / 'test.log', 'Basic secret', 123)

    def test_download_handles_real_uba_json_string_without_forwarding_credentials(self):
        responses = [(303, {}, b'"https://storage.googleapis.com/bucket/signed"'), (200, {}, b'')]
        with patch.object(auto, 'request', side_effect=responses) as request:
            auto.download_api(auto.BASE + '/42/download/file.zip', self.work / 'test.zip', 'Basic secret', 123)
        self.assertEqual(request.call_args_list[0].args[1], 'Basic secret')
        self.assertEqual(len(request.call_args_list[1].args), 1)
        self.assertNotIn('authorization', request.call_args_list[1].kwargs)

    def test_download_rejects_non_url_json_and_untrusted_strings(self):
        for data in (b'null', b'[]', b'42', b'{}', b'"https://evil.test/file"'):
            with self.subTest(data=data), patch.object(auto, 'request', return_value=(303, {}, data)) as request:
                with self.assertRaises(ValueError):
                    auto.download_api(auto.BASE + '/42/download/file.zip', self.work / 'test.zip', 'Basic secret', 123)
                self.assertEqual(request.call_count, 1)

    def test_refuses_untrusted_download_and_auth_destinations(self):
        for url in ('http://storage.googleapis.com/a', 'https://evil.test/a', 'https://storage.googleapis.com.evil.test/a',
                    'https://user:pass@storage.googleapis.com/a', 'https://127.0.0.1/a'):
            with self.subTest(url=url), self.assertRaises(ValueError):
                auto.storage_url(url)
        with self.assertRaisesRegex(ValueError, 'Credential destination'):
            auto.request('https://storage.googleapis.com/a', 'Basic secret')

    def test_requires_both_real_native_jobs_latest_run(self):
        run = dict(id=20, head_sha=self.sha, head_branch=auto.BRANCH, path='.github/workflows/unity-conversion.yml', event='push', status='completed', conclusion='success')
        jobs = [{'name': name, 'status': 'completed', 'conclusion': 'success'} for name in ('native-source', 'native-updater-windows')]
        for selected, expected in ((jobs, True), (jobs[:1], False), ([dict(jobs[0], conclusion='skipped'), jobs[1]], False)):
            with patch.object(auto, 'github', side_effect=[{'workflow_runs': [run]}, {'jobs': selected}]):
                self.assertEqual(auto.native_checks(self.sha, 'token'), expected)
        with patch.object(auto, 'github', return_value={'workflow_runs': [run, dict(run, id=21, conclusion='failure')]}):
            self.assertFalse(auto.native_checks(self.sha, 'token'))

    def test_artifact_selection_is_unambiguous(self):
        primary = dict(primary=True, files=[dict(filename='game.zip')])
        self.assertEqual(auto.artifact_filename([primary]), 'game.zip')
        for artifacts in ([], [primary, primary], [dict(primary=True, files=[dict(filename='source.cs')])]):
            with self.assertRaises(ValueError):
                auto.artifact_filename(artifacts)

    def test_branch_movement_stops_publish_before_any_channel_write(self):
        (self.work / 'publication.json').write_text(json.dumps({'sourceCommit': self.sha}))
        with patch.object(auto, 'current_head', return_value='b'*40), patch.object(auto.publisher, 'prepare_channel') as write:
            with self.assertRaisesRegex(ValueError, 'changed before publication'):
                auto.publish(self.work, self.fixture.project / 'channel', 'token')
            write.assert_not_called()

    def test_unverified_existing_publication_resumes_instead_of_republishing(self):
        manifest = self.fixture.manifest
        record = dict(sourceCommit=self.sha, buildNumber=42, version=manifest['build']['version'], sha256=manifest['sha256'])
        with patch.object(auto, 'current_head', return_value=self.sha), patch.object(auto, 'native_checks', return_value=True), \
             patch.object(auto, 'api_json', side_effect=[[self.build], self.build]), \
             patch.object(auto.publisher, 'remote_manifest', return_value=manifest), \
             patch.object(auto, 'request', return_value=(200, {}, json.dumps(record).encode())), patch.object(auto, 'output') as output:
            destination = self.work / 'retry'
            auto.prepare(destination, 'token', 'auth')
            self.assertTrue(json.loads((destination / 'publication.json').read_text())['verificationOnly'])
            self.assertTrue(output.call_args.args[0])

    def test_verified_existing_publication_avoids_archive_downloads(self):
        manifest = self.fixture.manifest
        record = dict(sourceCommit=self.sha, buildNumber=42, version=manifest['build']['version'], sha256=manifest['sha256'], publicDownloadsVerified=True)
        with patch.object(auto, 'current_head', return_value=self.sha), patch.object(auto, 'native_checks', return_value=True), \
             patch.object(auto, 'api_json', side_effect=[[self.build], self.build]), \
             patch.object(auto.publisher, 'remote_manifest', return_value=manifest), \
             patch.object(auto, 'request', return_value=(200, {}, json.dumps(record).encode())), \
             patch.object(auto, 'download_api') as download, patch.object(auto, 'output') as output:
            auto.prepare(self.work / 'no-download', 'token', 'auth')
            download.assert_not_called()
            self.assertFalse(output.call_args.args[0])

    def prepared_candidate(self):
        destination = self.work / 'candidate'
        def download(url, path, *_):
            shutil.copyfile(self.provider if url.endswith('/log') else self.outer, path)
        with patch.object(auto, 'current_head', return_value=self.sha), patch.object(auto, 'native_checks', return_value=True), \
             patch.object(auto, 'api_json', side_effect=[[dict(self.build, unityVersion='6000_3_24f1', localUnityVersion=auto.UNITY)], dict(self.build, unityVersion='6000_3_24f1', localUnityVersion=auto.UNITY), [{'primary': True, 'files': [{'filename': 'game.zip'}]}]]), \
             patch.object(auto.publisher, 'remote_manifest', return_value=None), \
             patch.object(auto, 'request', side_effect=ValueError('HTTP 404 from raw.githubusercontent.com')), \
             patch.object(auto, 'download_api', side_effect=download), patch.object(auto, 'output') as output:
            auto.prepare(destination, 'token', 'auth')
            self.assertTrue(output.call_args.args[0])
        return destination

    def public_download(self, url, **kwargs):
        source = self.fixture.builds / 'NativeUpdateFeed/objects' / url.rsplit('/', 1)[1]
        shutil.copyfile(source, kwargs['destination'])
        return 200, {}, b''

    def test_real_git_full_prepare_publish_and_public_verification(self):
        destination = self.prepared_candidate()
        fixture = self.fixture.fixture
        remote, writer = fixture.local_remote()
        identity = {'GIT_AUTHOR_NAME': 'Fixture', 'GIT_AUTHOR_EMAIL': 'fixture@example.invalid',
                    'GIT_COMMITTER_NAME': 'Fixture', 'GIT_COMMITTER_EMAIL': 'fixture@example.invalid'}
        with patch.object(auto.publisher, 'CHANNEL_REPO', str(remote)), patch.dict(os.environ, identity), \
             patch.object(auto, 'current_head', return_value=self.sha), patch.object(auto, 'native_checks', return_value=True), \
             patch.object(auto.publisher, 'remote_manifest', return_value=self.fixture.manifest), \
             patch.object(auto, 'request', side_effect=self.public_download):
            auto.publish(destination, fixture.channel, 'token')
        published = json.loads(fixture.git(remote, 'show', 'main:native/windows-x64/latest.json'))
        receipt = json.loads(fixture.git(remote, 'show', 'main:' + auto.PUBLICATION))
        self.assertEqual(published, self.fixture.manifest)
        self.assertTrue(receipt['publicDownloadsVerified'])
        self.assertEqual(receipt['sourceCommit'], self.sha)
        self.assertEqual(fixture.git(remote, 'show', 'main:feed/latest.json'), 'Electron fixture - preserve exactly')
        self.assertEqual(fixture.git(fixture.channel, 'status', '--porcelain'), '')
        self.assertEqual(fixture.git(remote, 'rev-list', '--count', 'main'), '3')

    def test_public_cache_delay_resumes_verification_without_republishing_payload(self):
        destination = self.prepared_candidate()
        fixture = self.fixture.fixture
        remote, writer = fixture.local_remote()
        identity = {'GIT_AUTHOR_NAME': 'Fixture', 'GIT_AUTHOR_EMAIL': 'fixture@example.invalid',
                    'GIT_COMMITTER_NAME': 'Fixture', 'GIT_COMMITTER_EMAIL': 'fixture@example.invalid'}
        with patch.object(auto.publisher, 'CHANNEL_REPO', str(remote)), patch.dict(os.environ, identity), \
             patch.object(auto, 'current_head', return_value=self.sha), patch.object(auto, 'native_checks', return_value=True):
            with patch.object(auto.publisher, 'remote_manifest', return_value=None):
                with self.assertRaisesRegex(ValueError, 'has not refreshed'):
                    auto.publish(destination, fixture.channel, 'token')
            self.assertEqual(fixture.git(remote, 'rev-list', '--count', 'main'), '2')
            record = json.loads((destination / 'publication.json').read_text())
            record['verificationOnly'] = True
            (destination / 'publication.json').write_text(json.dumps(record))
            with patch.object(auto.publisher, 'remote_manifest', return_value=self.fixture.manifest), \
                 patch.object(auto, 'request', side_effect=self.public_download):
                auto.publish(destination, fixture.channel, 'token')
        self.assertEqual(fixture.git(remote, 'rev-list', '--count', 'main'), '3')
        self.assertEqual(fixture.git(remote, 'diff-tree', '--no-commit-id', '--name-only', '-r', 'main'), auto.PUBLICATION)


if __name__ == '__main__':
    unittest.main()
