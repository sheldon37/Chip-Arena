"""Exercise real Git fetch/sparse worktrees and builder failure boundaries without Unity."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import unittest
from unittest.mock import patch

PROJECT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PROJECT / 'Builder'))
import builder
import test_cloud_build as fixtures


class FetchBuilderTests(unittest.TestCase):
    def setUp(self):
        self.fixture = fixtures.CloudBuildTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.folder = self.fixture.project
        self.root = self.folder / 'Builder paths & spaces'
        self.root.mkdir()
        self.writer = self.folder / 'writer'
        self.writer.mkdir()
        self.remote = self.folder / 'remote.git'
        self.git(self.writer, 'init', '-b', builder.BRANCH)
        self.git(self.writer, 'config', 'user.name', 'Builder fixture')
        self.git(self.writer, 'config', 'user.email', 'builder@example.invalid')
        self.source = self.writer / builder.PROJECT
        (self.source / 'Assets').mkdir(parents=True)
        (self.source / 'Assets/content.txt').write_text('first version')
        (self.source / 'tools').mkdir()
        for file in ('publish_native_update.py', 'package_cloud_build.py'):
            shutil.copyfile(PROJECT / 'tools' / file, self.source / 'tools' / file)
        (self.writer / 'unrelated').mkdir()
        (self.writer / 'unrelated/large-other-project.txt').write_text('exclude from checkout')
        self.git(self.writer, 'add', '.')
        self.git(self.writer, 'commit', '-m', 'First source')
        self.git(self.folder, 'clone', '--bare', str(self.writer), str(self.remote))
        self.git(self.writer, 'remote', 'add', 'origin', str(self.remote))
        self.patch = patch.object(builder, 'REPOSITORY', str(self.remote))
        self.patch.start()
        self.addCleanup(self.patch.stop)
        self.addCleanup(patch.stopall)
        patch.object(builder, 'editor', return_value='Unity fixture.exe').start()
        patch.object(builder, 'reveal').start()
        patch.object(builder, 'open_error_report').start()
        self.builds = []

    def git(self, cwd, *args):
        return subprocess.check_output(['git', '-C', str(cwd), *args], text=True, stderr=subprocess.STDOUT).strip()

    def advance(self):
        (self.source / 'Assets/content.txt').write_text('second version')
        self.git(self.writer, 'add', '.')
        self.git(self.writer, 'commit', '-m', 'Shared source update')
        self.git(self.writer, 'push', 'origin', builder.BRANCH)
        return self.git(self.writer, 'rev-parse', 'HEAD')

    def compile(self, project, unity):
        self.builds.append(project)
        shutil.copytree(self.fixture.builds, project / 'Builds', dirs_exist_ok=True)
        shutil.copyfile(self.fixture.log, project / 'Builds/build.log')
        shutil.copyfile(self.fixture.builds / fixtures.cloud.SEED,
                        project / 'Builds' / builder.PLAYER_ZIP)
        return builder.validate(project)

    def build(self, rebuild=False):
        with builder.locked(self.root), patch.object(builder, 'build_project', side_effect=self.compile):
            return builder.build(self.root, rebuild)

    def add_installer(self):
        # A real subprocess fixture checks argv, working directory and pre-build order.
        (self.source / 'tools/install_local_assets.py').write_text(
            "from pathlib import Path\nimport sys\n"
            "p=Path(sys.argv[1]); assert p.is_absolute()\n"
            "Path('Assets/installed.txt').write_text(p.name)\n")
        self.advance()

    def test_local_inputs_invalidate_reuse_and_install_before_compiling(self):
        self.add_installer()
        fallback = self.build()
        archive = self.root / 'LocalAssets.zip'
        archive.write_bytes(b'synthetic fixture zip input')
        original_compile = self.compile
        def compile_with_assets(project, unity):
            self.assertEqual((project / 'Assets/installed.txt').read_text(), 'LocalAssets.zip')
            return original_compile(project, unity)
        with patch.object(builder, 'build_project', side_effect=compile_with_assets):
            first = builder.build(self.root)
        self.assertNotEqual(first, fallback)
        self.assertEqual(self.build(), first)
        archive.write_bytes(b'changed synthetic input')
        self.assertNotEqual(self.build(), first)
        folder = self.root / 'LocalAssets'
        folder.mkdir()
        sound = folder / 'synthetic.txt'
        sound.write_text('one')
        folder_build = self.build()
        self.assertEqual((folder_build / 'Assets/installed.txt').read_text(), 'LocalAssets')
        archive.write_bytes(b'ignored because folder wins')
        self.assertEqual(self.build(), folder_build)
        sound.write_text('two')
        self.assertNotEqual(self.build(), folder_build)
        shutil.rmtree(folder)
        archive.unlink()
        clean = self.build()
        self.assertFalse((clean / 'Assets/installed.txt').exists())
        self.assertIsNone(builder.previous(self.root)['localAssetsSha256'])

    def test_missing_installer_and_failed_installer_block_build_and_publication(self):
        self.build()
        (self.root / 'LocalAssets').mkdir()
        with self.assertRaisesRegex(ValueError, 'sound-system commit'):
            self.build()
        self.assertEqual(len(self.builds), 1)
        with self.assertRaisesRegex(ValueError, 'latest build attempt'):
            builder.publish(self.root)
        (self.source / 'tools/install_local_assets.py').write_text(
            "import sys; sys.stderr.write('synthetic installer failure'); sys.exit(9)")
        self.advance()
        with self.assertRaises(subprocess.CalledProcessError):
            self.build()
        self.assertEqual(len(self.builds), 1)
        report = (self.root / 'Latest-Build-Error.txt').read_text()
        self.assertIn('Install optional local assets', report)
        self.assertIn('synthetic installer failure', report)
        with self.assertRaisesRegex(ValueError, 'latest build attempt'):
            builder.publish(self.root)

    def test_fetches_new_shared_commit_and_preserves_prior_source_edits_and_build(self):
        first = self.build()
        first_manifest = (first / 'Builds/NativeUpdateFeed/latest.json').read_bytes()
        (first / 'Assets/content.txt').write_text('keep this local edit')
        second_sha = self.advance()
        second = self.build()
        self.assertNotEqual(first, second)
        self.assertEqual((first / 'Assets/content.txt').read_text(), 'keep this local edit')
        self.assertEqual((first / 'Builds/NativeUpdateFeed/latest.json').read_bytes(), first_manifest)
        self.assertEqual((second / 'Assets/content.txt').read_text(), 'second version')
        self.assertEqual(builder.previous(self.root)['sourceCommit'], second_sha)
        self.assertFalse((second.parents[1] / 'unrelated').exists())
        self.assertEqual(len(self.builds), 2)

    def test_same_source_reuses_verified_success_unless_rebuild_requested(self):
        first = self.build()
        self.assertEqual(self.build(), first)
        self.assertEqual(len(self.builds), 1)
        self.assertNotEqual(self.build(True), first)
        self.assertEqual(len(self.builds), 2)

    def test_unavailable_remote_does_not_build_stale_source_or_publish_old_success(self):
        self.build()
        good = (self.root / 'last-success.json').read_bytes()
        self.remote.rename(self.remote.with_name('offline.git'))
        with self.assertRaises(subprocess.CalledProcessError):
            self.build()
        self.assertEqual((self.root / 'last-success.json').read_bytes(), good)
        self.assertEqual(len(self.builds), 1)
        with self.assertRaisesRegex(ValueError, 'latest build attempt'):
            builder.publish(self.root)

    def test_failed_unity_build_retains_previous_build_and_blocks_publication(self):
        self.build()
        good = builder.previous(self.root)
        self.advance()
        with patch.object(builder, 'build_project', side_effect=ValueError('Unity compilation failed')), self.assertRaisesRegex(ValueError, 'compilation failed'):
            builder.build(self.root)
        self.assertEqual(builder.previous(self.root), good)
        with self.assertRaisesRegex(ValueError, 'latest build attempt'):
            builder.publish(self.root)

    def test_failed_process_reports_current_log_then_success_clears_error(self):
        self.build()
        sha = self.advance()
        def fail(project, unity):
            log = project / 'Builds/build.log'
            log.parent.mkdir()
            script = ('from pathlib import Path; import sys; '
                      'Path(sys.argv[1]).write_text("PASS native locomotion\\n'
                      'Exception: Native DEV combat: held E resumes after hook\\n'
                      '  at NativeArena.ValidateDevCombatParity()\\n", encoding="utf-8"); '
                      'sys.exit(7)')
            subprocess.run([sys.executable, '-c', script, str(log)], check=True)
        with patch.object(builder, 'build_project', side_effect=fail), \
                self.assertRaises(subprocess.CalledProcessError) as failed:
            builder.build(self.root)
        self.assertEqual(failed.exception.returncode, 7)
        report = (self.root / 'Latest-Build-Error.txt').read_text(encoding='utf-8')
        self.assertIn(sha, report)
        self.assertIn('held E resumes after hook', report)
        self.assertIn('PASS native locomotion', report)
        attempt = json.loads((self.root / 'last-attempt.json').read_text())
        original = self.root / attempt['project'] / 'Builds/build.log'
        self.assertEqual((self.root / 'Latest-Build.log').read_bytes(), original.read_bytes())
        with self.assertRaisesRegex(ValueError, 'latest build attempt'):
            builder.publish(self.root)
        good = self.build()
        self.assertIn('No build error.', (self.root / 'Latest-Build-Error.txt').read_text())
        self.assertEqual((self.root / 'Latest-Build.log').read_bytes(), (good / 'Builds/build.log').read_bytes())
        self.remote.rename(self.remote.with_name('offline.git'))
        with self.assertRaises(subprocess.CalledProcessError):
            self.build()
        self.assertIn('No Unity log was produced', (self.root / 'Latest-Build.log').read_text())
        self.assertNotIn('held E resumes after hook', (self.root / 'Latest-Build-Error.txt').read_text())

    def test_corrupt_package_is_not_accepted_or_published(self):
        project = self.build()
        (project / 'Builds' / builder.PLAYER_ZIP).write_bytes(b'broken')
        with self.assertRaises(subprocess.CalledProcessError):
            self.build()
        with self.assertRaisesRegex(ValueError, 'latest build attempt'):
            builder.publish(self.root)

    def test_publish_selects_last_success_after_a_new_remote_commit(self):
        first = self.build()
        self.advance()  # Publishing does not fetch or silently select unbuilt source.
        with builder.locked(self.root), patch.object(builder.native_publish, 'publish', return_value={'status': 'published'}) as publish:
            builder.publish(self.root)
        self.assertEqual(publish.call_args.args[0], first / 'Builds/NativeUpdateFeed')
        self.assertEqual(publish.call_args.args[1], self.root / 'Cache/native-publisher-v2.git')
        self.assertEqual(publish.call_args.args[2]['build'], builder.previous(self.root)['build'])
        self.assertEqual(json.loads((self.root / 'last-publication.json').read_text())['selectedSourceCommit'], builder.previous(self.root)['sourceCommit'])

    def test_combined_command_builds_before_publishing_and_stops_on_failed_build(self):
        with patch.object(builder, 'build_project', side_effect=self.compile), \
                patch.object(builder.native_publish, 'publish', return_value={'status': 'published'}) as publish:
            builder.perform(self.root, 'build-publish')
            self.assertEqual(publish.call_count, 1)
            builder.perform(self.root, 'build-publish')
            self.assertEqual(len(self.builds), 1)  # retry reuses the validated build
            self.advance()
            with patch.object(builder, 'build_project', side_effect=ValueError('bad build')):
                with self.assertRaisesRegex(ValueError, 'bad build'):
                    builder.perform(self.root, 'build-publish')
            self.assertEqual(publish.call_count, 2)

    def test_upload_failure_keeps_successful_build_available_for_retry(self):
        with patch.object(builder, 'build_project', side_effect=self.compile), \
                patch.object(builder.native_publish, 'publish', side_effect=ValueError('network failed')):
            with self.assertRaisesRegex(ValueError, 'network failed'):
                builder.perform(self.root, 'build-publish')
        self.assertEqual(json.loads((self.root / 'last-attempt.json').read_text())['status'], 'success')
        with patch.object(builder.native_publish, 'publish', return_value={'status': 'published'}):
            builder.publish(self.root)
        self.assertEqual(len(self.builds), 1)

    def test_changed_cache_origin_is_refused_without_rewriting_it(self):
        self.build()
        cache = self.root / 'Cache/repository.git'
        self.git(cache, 'remote', 'set-url', 'origin', 'https://example.invalid/not-the-game.git')
        with self.assertRaisesRegex(ValueError, 'expected Chip Arena'):
            self.build()
        self.assertEqual(self.git(cache, 'remote', 'get-url', 'origin'), 'https://example.invalid/not-the-game.git')

    def test_lock_prevents_two_builds_and_releases_after_failure(self):
        with builder.locked(self.root):
            with self.assertRaisesRegex(ValueError, 'already running'):
                with builder.locked(self.root):
                    self.fail('Second lock was acquired')
        with builder.locked(self.root):
            pass

    def test_saved_path_cannot_escape_builder(self):
        for path in ('../other', '/tmp/other', 'Work/123456789abc-12345678/../outside', 'Work/link/unity/ChipArenaConversion'):
            with self.subTest(path=path), self.assertRaisesRegex(ValueError, 'saved build path'):
                builder.project_for(self.root, {'project': path})

    def test_unity_command_uses_exact_project_and_validation_gates(self):
        repository, sha = builder.fetch(self.root)
        project = builder.checkout(self.root, repository, sha)
        original = builder.run
        seen = []
        def run(args, **kwargs):
            if args[0] == 'Unity fixture.exe':
                seen.extend(args)
                self.compile(project, args[0])
                return subprocess.CompletedProcess(args, 0)
            return original(args, **kwargs)
        with patch.object(builder, 'run', side_effect=run):
            identity = builder.build_project(project, 'Unity fixture.exe')
        self.assertEqual(identity, self.fixture.manifest['build'])
        self.assertEqual(seen[seen.index('-projectPath') + 1], project)
        self.assertEqual(seen[seen.index('-executeMethod') + 1], 'ChipArena.UnityConversion.Editor.PrototypeBuild.BuildWindows')
        self.assertEqual(seen[seen.index('-logFile') + 1], project / 'Builds/build.log')

    @unittest.skipUnless(os.name == 'nt', 'Windows command entrypoints')
    def test_double_click_entrypoint_handles_spaces_and_other_working_directory(self):
        for path in (PROJECT / 'Builder').iterdir():
            if path.is_file():
                shutil.copyfile(path, self.root / path.name)
        for name in ('Build-Windows.cmd', 'Build-and-Publish.cmd'):
            result = subprocess.run(['cmd.exe', '/d', '/c', 'call', str(self.root / name), '--help'],
                                    cwd=self.folder, input='\n', text=True, capture_output=True, timeout=30)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('Fetch the shared Unity source', result.stdout)
        self.assertFalse((self.root / 'Cache').exists())


if __name__ == '__main__':
    unittest.main()
