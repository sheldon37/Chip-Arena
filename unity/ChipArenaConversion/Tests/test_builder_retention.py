"""Exercise retention against actual Git worktrees and Unity-generated files."""
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import builder_retention as retention


def git(path, *args):
    return subprocess.check_output(['git', '-C', str(path), *args], text=True, stderr=subprocess.PIPE).strip()


class BuilderRetentionTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name).resolve()
        self.repo = self.root / 'Cache/repository.git'
        self.repo.mkdir(parents=True)
        git(self.repo, 'init', '--bare')
        git(self.repo, 'remote', 'add', 'origin', retention.REPO)
        seed = self.root / 'seed'
        seed.mkdir()
        git(seed, 'init')
        self.write(seed, '.gitignore', 'Builds/\nTemp/\nLocalAssets/\n*.bytes\n')
        self.assets = ['Assets/ChipArena/Core/NativeViewArms.cs',
                       'Assets/ChipArena/Runtime/NativeViewArmsPresentation.cs']
        for path in ['ProjectSettings/ProjectSettings.asset', *self.assets]:
            self.write(seed, retention.PROJECT + path, 'tracked')
        git(seed, 'add', '.')
        git(seed, '-c', 'user.name=test', '-c', 'user.email=test@example.org', 'commit', '-m', 'fixture')
        git(seed, 'push', str(self.repo), 'HEAD:refs/heads/seed')
        self.sha = git(seed, 'rev-parse', 'HEAD')

    def write(self, folder, name, content):
        path = folder / name
        path.parent.mkdir(exist_ok=True, parents=True)
        path.write_text(content)

    def worktree(self, number):
        folder = self.root / 'Work' / (self.sha[:12] + '-' + f'{number:08x}')
        git(self.repo, 'worktree', 'add', '--detach', str(folder), self.sha)
        return folder

    def unity_output(self, folder):
        project = folder / retention.PROJECT
        self.write(project, 'Assets/ChipArena/Resources/DeployCards/back.bytes', 'packed input')
        for name in ['Assets/ChipArena/Resources/DeployCards/back.bytes.meta', 'ProjectSettings/ProjectSettings.asset', 'ProjectSettings/AudioManager.asset',
                     'Packages/packages-lock.json', 'Assets/ChipArena/Resources/DeployCards.meta',
                     *(asset + '.meta' for asset in self.assets)]:
            self.write(project, name, 'generated')
        self.write(project, 'Builds/build.log', 'build diagnostics')

    def test_removes_unity_output_and_keeps_protected_trees_intact(self):
        old, current, active, locked, dirty, staged, unknown, deleted = map(self.worktree, range(8))
        for path in (old, current, active, locked, dirty, staged, unknown, deleted):
            self.unity_output(path)
        self.write(active / retention.PROJECT, 'Temp/UnityLockfile', '')
        git(self.repo, 'worktree', 'lock', str(locked))
        self.write(dirty, 'notes with spaces.txt', 'operator')
        git(staged, 'add', retention.SETTINGS)
        self.write(unknown / retention.PROJECT, 'Assets/ChipArena/Resources/DeployCards/orphan.meta', 'operator')
        (deleted / retention.SETTINGS).unlink()
        retention.after_publish(self.root, current / retention.PROJECT, git, lambda _: None,
                                lambda path, source: shutil.copyfile(source, path))
        self.assertFalse(old.exists())
        self.assertEqual((self.root / 'Build-History' / (old.name + '.log')).read_text(), 'build diagnostics')
        for path in (current, active, locked, dirty, staged, unknown):
            self.assertEqual((path / retention.SETTINGS).read_text(), 'generated')
        self.assertTrue(deleted.exists())

    def test_registration_only_sweep_is_scoped_and_repeatable(self):
        keep, missing, locked = map(self.worktree, range(3))
        git(self.repo, 'worktree', 'lock', str(locked))
        outside = self.root / missing.name
        git(self.repo, 'worktree', 'add', '--detach', str(outside), self.sha)
        for path in (outside, missing, locked):
            shutil.rmtree(path)
        for _ in range(2):
            retention.sweep_worktrees(self.root, None, git, lambda _: None)
        listing = git(self.repo, 'worktree', 'list', '--porcelain')
        for path in (outside, keep, locked):
            self.assertIn('worktree ' + path.as_posix(), listing)
        self.assertNotIn('worktree ' + missing.as_posix(), listing)
