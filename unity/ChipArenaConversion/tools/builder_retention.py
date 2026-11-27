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
PROJECT = 'unity/ChipArenaConversion/'
SETTINGS = PROJECT + 'ProjectSettings/ProjectSettings.asset'
GENERATED = re.compile(
    re.escape(PROJECT) + r'(?:ProjectSettings/[^/]+\.asset|Packages/packages-lock\.json'
    r'|Assets/ChipArena/(?:Core/NativeViewArms|Runtime/NativeViewArmsPresentation)\.cs\.meta'
    r'|Assets/ChipArena/Resources/DeployCards(?:/[^/]+)*\.meta)'
)
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
    work = root / 'Work'
    current = project.parent.parent
    if current.parent != work or not WORK_NAME.fullmatch(current.name):
        raise ValueError('Unexpected published worktree')
    sweep_worktrees(root, current, git, plain_path, diagnostic_file)


def sweep_worktrees(root, current, git, plain_path, diagnostic_file=None):
    work, repo = root / 'Work', root / 'Cache/repository.git'
    plain_path(work)
    plain_path(repo)
    if git(repo, 'remote', 'get-url', 'origin') != REPO:
        raise ValueError('Unexpected worktree repository')
    listing = git(repo, 'worktree', 'list', '--porcelain', '-z')
    for record in listing.split('\0\0'):
        fields = record.rstrip('\0').split('\0')
        if not fields[0].startswith('worktree '):
            continue
        folder = Path(fields[0][9:])
        if folder == current or folder.parent != work or not WORK_NAME.fullmatch(folder.name):
            continue
        if 'detached' not in fields or any(f.partition(' ')[0] == 'locked' for f in fields):
            continue
        if not any(f.startswith('HEAD ' + folder.name[:12]) for f in fields):
            continue
        try:
            if any(f.partition(' ')[0] == 'prunable' for f in fields):
                if not os.path.lexists(folder):
                    git(repo, 'worktree', 'remove', str(folder))
                    print('Pruned missing builder worktree ' + folder.name, flush=True)
                continue
            if current is not None:
                remove_inactive(root, repo, folder, git, plain_path, diagnostic_file)
        except (OSError, ValueError, subprocess.CalledProcessError) as error:
            print(f'Kept builder worktree {folder.name}: {error}', file=sys.stderr)


def remove_inactive(root, repo, folder, git, plain_path, diagnostic_file):
    plain_path(folder)
    build = folder / PROJECT
    if (build / 'Temp/UnityLockfile').exists():
        return
    if Path(git(folder, 'rev-parse', '--show-toplevel')) != folder:
        return
    if git(folder, 'rev-parse', '--abbrev-ref', 'HEAD') != 'HEAD':
        return
    if not git(folder, 'rev-parse', 'HEAD').startswith(folder.name[:12]):
        return
    if git(folder, 'diff', '--cached', '--name-only', '-z'):
        return
    changes = git(folder, 'diff', '--name-status', '-z', '--')
    if changes and changes != 'M\0' + SETTINGS + '\0':
        return
    untracked = git(folder, 'ls-files', '--others', '--exclude-standard', '-z')
    outputs = []
    for name in untracked.split('\0')[:-1]:
        if not GENERATED.fullmatch(name):
            return
        # Deploy card payloads can be ignored build inputs. Any unfamiliar
        # non-ignored payload is independently rejected by the full file list.
        if name.endswith('.meta') and not (folder / name[:-5]).exists():
            return
        output = folder / name
        plain_path(output)
        if not output.is_file() or output.is_symlink():
            return
        outputs.append(output)
    log = build / 'Builds/build.log'
    if log.is_file():
        history = root / 'Build-History'
        plain_path(log)
        plain_path(history)
        history.mkdir(exist_ok=True)
        diagnostic_file(history / (folder.name + '.log'), source=log)
    if changes:
        git(folder, 'restore', '--worktree', '--', SETTINGS)
    for output in outputs:
        output.unlink()
    # Recheck cleanliness and locks through Git, including any newly arrived files.
    git(repo, 'worktree', 'remove', str(folder))
    print('Removed inactive builder worktree ' + folder.name, flush=True)


def load_builder(root):
    path = root / 'builder.py'
    spec = importlib.util.spec_from_file_location('local_chip_builder', path)
    builder = importlib.util.module_from_spec(spec)
    sys.path.insert(0, str(root))
    spec.loader.exec_module(builder)
    return builder


def install_hook(root):
    path = root / 'builder.py'
    builder = load_builder(root)
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
    actions = parser.add_mutually_exclusive_group(required=True)
    actions.add_argument('--install-hook', type=Path, metavar='BUILDER_ROOT')
    actions.add_argument('--prune-registrations', type=Path, metavar='BUILDER_ROOT',
                         help='remove missing detached build registrations under the builder lock')
    args = parser.parse_args()
    if args.install_hook:
        install_hook(args.install_hook.resolve())
    else:
        root = args.prune_registrations.resolve()
        builder = load_builder(root)
        with builder.locked(root):
            sweep_worktrees(root, None, builder.git, builder.plain_path)
