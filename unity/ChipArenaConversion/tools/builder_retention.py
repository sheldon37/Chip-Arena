"""Install the post-publication worktree retention hook in the standalone Windows builder."""
import argparse
import importlib.util
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile

WORK_NAME = re.compile(r'[0-9a-f]{12}-[0-9a-f]{8}')
REPO = 'https://github.com/sheldon37/Chip-Arena.git'
ANCHOR = "    save(root / 'last-publication.json', dict(result, selectedSourceCommit=record['sourceCommit']))"
HOOK = """
    # Still inside the builder lock, after the publication receipt is durable.
    helper = project / 'tools/builder_retention.py'
    if helper.is_file():
        retention = importlib.util.spec_from_file_location('chip_builder_retention', helper)
        try:
            module = importlib.util.module_from_spec(retention)
            retention.loader.exec_module(module)
            module.after_publish(root, project, git, plain_path, diagnostic_file)
        except (OSError, ValueError, subprocess.CalledProcessError) as error:
            print('Published; old worktree cleanup deferred: ' + str(error), file=sys.stderr)
"""


def after_publish(root, project, git, plain_path, diagnostic_file):
    work, repo = root / 'Work', root / 'Cache/repository.git'
    current = project.parent.parent
    if current.parent != work or not WORK_NAME.fullmatch(current.name):
        raise ValueError('Unexpected published worktree')
    plain_path(work)
    plain_path(repo)
    if git(repo, 'remote', 'get-url', 'origin') != REPO:
        raise ValueError('Unexpected worktree repository')
    for line in git(repo, 'worktree', 'list', '--porcelain').splitlines():
        if not line.startswith('worktree '):
            continue
        folder = Path(line[9:])
        if folder == current or folder.parent != work or not WORK_NAME.fullmatch(folder.name):
            continue
        plain_path(folder)
        build = folder / 'unity/ChipArenaConversion'
        if (build / 'Temp/UnityLockfile').exists():
            continue
        # A registration can be an operator checkout; require its original detached commit.
        if not git(folder, 'rev-parse', 'HEAD').startswith(folder.name[:12]):
            continue
        if git(folder, 'rev-parse', '--abbrev-ref', 'HEAD') != 'HEAD' or git(folder, 'status', '--porcelain', '--untracked-files=normal'):
            continue
        log = build / 'Builds/build.log'
        if log.is_file():
            history = root / 'Build-History'
            plain_path(log)
            plain_path(history)
            history.mkdir(exist_ok=True)
            diagnostic_file(history / (folder.name + '.log'), source=log)
        # Git refuses locked worktrees. No --force: preserve tracked edits and unfamiliar files.
        git(repo, 'worktree', 'remove', str(folder))
        print('Removed inactive builder worktree ' + folder.name, flush=True)


def install_hook(root):
    path = root / 'builder.py'
    spec = importlib.util.spec_from_file_location('local_chip_builder', path)
    builder = importlib.util.module_from_spec(spec)
    sys.path.insert(0, str(root))
    spec.loader.exec_module(builder)
    with builder.locked(root):
        source = path.read_bytes()
        newline = '\r\n' if b'\r\n' in source else '\n'
        original = source.decode('utf-8').replace('\r\n', '\n')
        if ANCHOR + HOOK in original:
            return
        if original.count(ANCHOR + '\n') != 1:
            raise ValueError('Unrecognized publication receipt; builder.py was left intact')
        updated = original.replace(ANCHOR, ANCHOR + HOOK)
        compile(updated, str(path), 'exec')
        backup = root / 'builder.before-worktree-retention.py'
        if not backup.exists():
            shutil.copy2(path, backup)
        with tempfile.NamedTemporaryFile(dir=root, delete=False) as stage:
            stage.write(updated.replace('\n', newline).encode('utf-8'))
        os.replace(stage.name, path)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install-hook', type=Path, required=True, metavar='BUILDER_ROOT')
    install_hook(parser.parse_args().install_hook.resolve())
