"""Exercise removal against real detached git worktrees, including ignored licensed assets."""
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
    def test_only_clean_inactive_registered_worktrees_are_removed(self):
        with tempfile.TemporaryDirectory() as scratch:
            root = Path(scratch)
            repo = root / 'Cache/repository.git'
            repo.mkdir(parents=True)
            git(repo, 'init', '--bare')
            git(repo, 'remote', 'add', 'origin', retention.REPO)
            seed = root / 'seed'
            git(repo, 'worktree', 'add', '--orphan', '-b', 'seed', str(seed))
            (seed / '.gitignore').write_text('Builds/\nTemp/\nLocalAssets/\n')
            git(seed, 'add', '.gitignore')
            git(seed, '-c', 'user.name=test', '-c', 'user.email=test@example.org', 'commit', '-m', 'fixture')
            sha = git(seed, 'rev-parse', 'HEAD')
            projects = []
            for number in range(4):
                folder = root / 'Work' / (sha[:12] + '-' + f'{number:08x}')
                git(repo, 'worktree', 'add', '--detach', str(folder), sha)
                project = folder / 'unity/ChipArenaConversion'
                (project / 'Builds').mkdir(parents=True)
                (project / 'Builds/build.log').write_text('log')
                projects.append(project)
            (projects[2] / 'Temp').mkdir()
            (projects[2] / 'Temp/UnityLockfile').touch()
            (projects[3].parent.parent / '.gitignore').write_text('edited')
            retention.after_publish(root, projects[1], git, lambda _: None,
                                    lambda path, source: shutil.copyfile(source, path))
            self.assertFalse(projects[0].parent.parent.exists())
            self.assertTrue(all(p.exists() for p in projects[1:]))
            self.assertEqual(len(list((root / 'Build-History').glob('*.log'))), 1)
