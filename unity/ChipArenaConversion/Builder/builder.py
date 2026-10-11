"""Fetch the shared Unity source, build locally, and publish only a successful build."""
import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import uuid

import native_publish

REPOSITORY = 'https://github.com/sheldon37/Chip-Arena.git'
BRANCH = 'codex/unity-cloud-current'
PROJECT = Path('unity/ChipArenaConversion')
VERSION = '6000.3.24f1'
PLAYER_ZIP = 'Chip-Arena-Windows.zip'
GIT_EXECUTABLE = None
# Reuse the source project's complete feed/archive/identity validators. This code
# executes only in the local owner's build environment, never in credentialed CI.
VALIDATE = '''import json,sys
from pathlib import Path
p=Path(sys.argv[1]); sys.path.insert(0,str(p/'tools'))
import publish_native_update as publish
import package_cloud_build as cloud
feed=p/'Builds/NativeUpdateFeed'
m=publish.validate(feed)
publish.validate_build_log(feed,p/'Builds/build.log')
cloud.validate_seed(p/'Builds/Chip-Arena-Windows.zip',feed,m)
print(json.dumps(m['build']))
'''


def run(args, **kwargs):
    command = [str(a) for a in args]
    if command[0] == 'git' and GIT_EXECUTABLE:
        command[0] = GIT_EXECUTABLE
    return subprocess.run(command, check=True, **kwargs)


def git_candidates():
    found = shutil.which('git')
    if os.name == 'nt':
        roots = []
        if found:
            roots.append(Path(found).parent.parent)
        for variable, suffix in (('ProgramW6432', 'Git'), ('ProgramFiles', 'Git'),
                                 ('LOCALAPPDATA', 'Programs/Git')):
            if os.environ.get(variable):
                roots.append(Path(os.environ[variable]) / suffix)
        for root in roots:
            for flavor in ('ucrt64', 'mingw64', 'clangarm64', 'mingw32'):
                yield root / flavor / 'bin/git.exe'
    if found:
        yield Path(found)


def select_git(candidates=None):
    # cmd/git.exe can silently return zero without invoking Git on the owner's
    # laptop. Prefer the supported real binary and require actual version text.
    for path in dict.fromkeys(git_candidates() if candidates is None else candidates):
        if not Path(path).is_file():
            continue
        try:
            result = subprocess.run([str(path), '--version'], capture_output=True,
                                    text=True, timeout=15)
        except (OSError, subprocess.TimeoutExpired):
            continue
        if result.returncode == 0 and re.match(r'^git version \d+\.', result.stdout):
            print('Using ' + result.stdout.strip() + ': ' + str(path), flush=True)
            return str(path)
    raise ValueError('No working Git executable was found. Install or repair Git for Windows, then reopen the builder.')


def git(repo, *args):
    return run(['git', '-C', repo, *args], stdout=subprocess.PIPE, text=True).stdout.strip()


def plain_path(path):
    for item in (path, *path.parents):
        if item.exists():
            info = item.lstat()
            if stat.S_ISLNK(info.st_mode) or getattr(info, 'st_file_attributes', 0) & 0x400:
                raise ValueError('Use a normal folder, not a symbolic link or junction: ' + str(item))


@contextmanager
def locked(root):
    plain_path(root)
    root.mkdir(parents=True, exist_ok=True)
    path = root / 'builder.lock'
    plain_path(path)
    with path.open('a+b') as stream:
        stream.seek(0, 2)
        if not stream.tell():
            stream.write(b'0')
            stream.flush()
        stream.seek(0)
        try:
            if os.name == 'nt':
                import msvcrt
                msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError:
            raise ValueError('Another build or publication is already running in this builder folder.') from None
        try:
            yield
        finally:
            stream.seek(0)
            if os.name == 'nt':
                msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(stream, fcntl.LOCK_UN)


def save(path, value):
    plain_path(path)
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    temporary.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
    temporary.replace(path)


def diagnostic_file(path, text=None, source=None):
    plain_path(path)
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    try:
        if source is not None:
            plain_path(source)
            shutil.copyfile(source, temporary)
        else:
            temporary.write_text(text, encoding='utf-8')
        temporary.replace(path)
    finally:
        if temporary.exists():
            temporary.unlink()


def unity_errors(log):
    """Prefer compiler/exception causes to shutdown wrappers or early license noise."""
    lines = log.lstrip('\ufeff').splitlines()
    patterns = (
        r'\berror (?:CS|BC)\d+\b',
        r'^\s*(?:[\w.]*Exception|Fatal error)\s*:',
        r'\b(?:error|failed|failure|aborting|crash)\b',
    )
    indices = []
    for pattern in patterns:
        indices = [i for i, line in enumerate(lines) if re.search(pattern, line, re.I)]
        if indices:
            break
    if not indices:
        return 'No recognized error marker; last log lines:\n' + '\n'.join(lines[-35:])[-12000:]
    blocks = []
    seen = set()
    for i in indices[-6:]:
        if lines[i] in seen:
            continue
        seen.add(lines[i])
        block = [lines[i]]
        for line in lines[i + 1:i + 13]:
            if not line.strip() or not (line.startswith((' ', '\t', '(Filename:')) or re.match(r'^[\w.]+:[\w.<>]+\s*\(', line)):
                break
            block.append(line)
        blocks.append('\n'.join(block))
    return '\n\n'.join(blocks)[:12000]


def open_error_report(path):
    if os.name == 'nt':
        subprocess.Popen(['notepad.exe', str(path)])


def build_diagnostics(root, attempt, project=None, error=None):
    # Diagnostic I/O and Notepad must never turn a good build into a failure or
    # replace the actual exception. Original per-worktree logs remain untouched.
    try:
        latest = root / 'Latest-Build.log'
        report = root / 'Latest-Build-Error.txt'
        log = project / 'Builds/build.log' if project is not None else None
        heading = ('Chip Arena build: ' + attempt['status'].upper() + '\n'
                   + 'Started (UTC): ' + attempt['at'] + '\n'
                   + 'Source: ' + attempt.get('sourceCommit', 'not fetched') + '\n'
                   + 'Stage: ' + attempt.get('stage', 'starting') + '\n')
        contents = ''
        if log is not None and log.is_file():
            diagnostic_file(latest, source=log)
            contents = latest.read_text(encoding='utf-8-sig', errors='replace')
            heading += 'Original log: ' + str(log) + '\n'
        else:
            diagnostic_file(latest, text=heading + '\nNo Unity log was produced by this attempt.\n')
        if error is None:
            diagnostic_file(report, text=heading + '\n' + ('Build in progress.\n' if attempt['status'] == 'started' else 'No build error.\n'))
            return
        if isinstance(error, subprocess.CalledProcessError):
            detail = 'Process exited with code ' + str(error.returncode) + '.'
            if error.stderr:
                detail += '\n' + str(error.stderr)[-8000:]
        else:
            detail = str(error) or type(error).__name__
        summary = heading + '\n' + detail + '\n'
        if contents:
            summary += '\nUnity error details:\n' + unity_errors(contents) + '\n'
            passed = [line.strip() for line in contents.splitlines() if line.startswith('PASS ')][-5:]
            if passed:
                summary += '\nLast completed checks:\n' + '\n'.join(passed) + '\n'
        else:
            summary += '\nNo Unity log was produced by this attempt.\n'
        summary += '\nFull log: ' + str(latest) + '\nError report: ' + str(report) + '\n'
        summary += '\nShare this error report first; the full log is beside it if needed.\n'
        diagnostic_file(report, text=summary)
        # Preserve Unicode in the file even on a legacy Windows console encoding.
        encoding = getattr(sys.stdout, 'encoding', None) or 'utf-8'
        print('\n' + summary.encode(encoding, errors='replace').decode(encoding), flush=True)
        try:
            open_error_report(report)
        except OSError:
            print('Could not open Notepad. Open the error report shown above.', flush=True)
    except (OSError, ValueError) as failure:
        print('Could not save build diagnostics: ' + str(failure), file=sys.stderr)


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(block)
    return result.hexdigest()


def local_assets(root):
    """Select optional owner-only inputs and fingerprint them for build reuse."""
    folder, archive = root / 'LocalAssets', root / 'LocalAssets.zip'
    source = folder if folder.is_dir() else archive if archive.is_file() else None
    if source is None:
        return None, None
    plain_path(source)
    fingerprint = hashlib.sha256()
    if source.is_file():
        fingerprint.update(('zip:' + digest(source)).encode('ascii'))
    else:
        fingerprint.update(b'folder:')
        for path in sorted(source.rglob('*')):
            plain_path(path)
            if path.is_file():
                fingerprint.update(path.relative_to(source).as_posix().encode('utf-8') + b'\0')
                fingerprint.update(digest(path).encode('ascii') + b'\0')
    return source, fingerprint.hexdigest()


def install_local_assets(project, source):
    if source is None:
        print('No LocalAssets folder or ZIP beside the builder; using current fallback sounds.', flush=True)
        return
    installer = project / 'tools/install_local_assets.py'
    if not installer.is_file():
        raise ValueError('LocalAssets supplied, but this shared source has no tools/install_local_assets.py. '
                         'Wait for the sound-system commit to reach ' + BRANCH + ', or move LocalAssets away to build fallback sounds.')
    print('Installing local licensed assets from: ' + str(source), flush=True)
    # Run the same Python 3 selected by Run-Builder.cmd (Windows may not expose python3).
    run([sys.executable, installer, source], cwd=project, stdout=subprocess.PIPE,
        stderr=subprocess.PIPE, text=True)


def fetch(root):
    cache = root / 'Cache'
    repository = cache / 'repository.git'
    plain_path(repository)
    cache.mkdir(exist_ok=True)
    if not repository.exists():
        temporary = cache / ('download-' + uuid.uuid4().hex + '.git')
        # Failed clones stay in their uniquely named folder for diagnostics. Never
        # remove/reset a pre-existing checkout or anyone's local source edits.
        run(['git', 'clone', '--bare', '--depth=1', '--filter=blob:none', '--single-branch',
             '--branch', BRANCH, REPOSITORY, temporary])
        temporary.replace(repository)
    if git(repository, 'rev-parse', '--is-bare-repository') != 'true' or git(repository, 'remote', 'get-url', 'origin') != REPOSITORY:
        raise ValueError('Cache is not the expected Chip Arena source repository; left unchanged.')
    git(repository, 'config', 'core.longpaths', 'true')
    # Only this owned cache's remote-tracking reference advances, never a user's branch.
    ref = 'refs/remotes/origin/' + BRANCH
    git(repository, 'fetch', '--depth=1', 'origin', '+refs/heads/' + BRANCH + ':' + ref)
    sha = git(repository, 'rev-parse', ref + '^{commit}')
    if not re.fullmatch('[0-9a-f]{40}', sha):
        raise ValueError('Unexpected source commit from Git')
    print('Shared source: ' + BRANCH + ' @ ' + sha, flush=True)
    return repository, sha


def checkout(root, repository, sha):
    name = sha[:12] + '-' + uuid.uuid4().hex[:8]
    destination = root / 'Work' / name
    plain_path(destination)
    destination.parent.mkdir(exist_ok=True)
    git(repository, 'worktree', 'add', '--detach', '--no-checkout', str(destination), sha)
    git(destination, 'sparse-checkout', 'set', '--cone', PROJECT.as_posix())
    git(destination, 'checkout', '--detach', sha)
    project = destination / PROJECT
    if not (project / 'Assets').is_dir() or not (project / 'tools/publish_native_update.py').is_file():
        raise ValueError('Shared source does not contain the expected Unity project')
    return project


def editor():
    if os.name != 'nt':
        raise ValueError('Build-Windows requires Windows and an activated Unity Editor.')
    default = Path(os.environ.get('ProgramFiles', r'C:\Program Files')) / 'Unity/Hub/Editor' / VERSION / 'Editor/Unity.exe'
    path = Path(os.environ.get('UNITY_EDITOR', str(default)))
    if not path.is_file():
        raise ValueError('Install Unity ' + VERSION + ' with Windows Build Support in Unity Hub, or set UNITY_EDITOR to Unity.exe.')
    return path


def validate(project):
    result = run([sys.executable, '-c', VALIDATE, project], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    return json.loads(result.stdout)


def build_project(project, unity):
    output = project / 'Builds'
    output.mkdir(exist_ok=True)
    log = output / 'build.log'
    print('Building locally. Log: ' + str(log), flush=True)
    run([unity, '-batchmode', '-nographics', '-projectPath', project, '-buildTarget', 'Win64',
         '-executeMethod', 'ChipArena.UnityConversion.Editor.PrototypeBuild.BuildWindows', '-quit', '-logFile', log])
    return validate(project)


def previous(root):
    path = root / 'last-success.json'
    plain_path(path)
    return json.loads(path.read_text(encoding='utf-8')) if path.is_file() else None


def project_for(root, record):
    relative = record.get('project', '')
    if not re.fullmatch(r'Work/[0-9a-f]{12}-[0-9a-f]{8}/unity/ChipArenaConversion', relative):
        raise ValueError('Invalid saved build path')
    project = root / relative
    plain_path(project)
    return project


def check_saved(root, record):
    project = project_for(root, record)
    if validate(project) != record['build'] or digest(project / 'Builds' / PLAYER_ZIP) != record['zipSha256']:
        raise ValueError('The saved build has changed. Build again before publishing.')
    return project


def build(root, rebuild=False, publish_after=False):
    attempt = {'id': uuid.uuid4().hex, 'status': 'started', 'at': datetime.now(timezone.utc).isoformat()}
    save(root / 'last-attempt.json', attempt)
    project = None
    build_diagnostics(root, attempt)
    try:
        attempt['stage'] = 'Locate Unity Editor'
        unity = editor()
        attempt['stage'] = 'Fetch shared source'
        repository, sha = fetch(root)
        attempt['sourceCommit'] = sha
        attempt['stage'] = 'Inspect optional local assets'
        source, assets_digest = local_assets(root)
        last = previous(root)
        if last and last['sourceCommit'] == sha and last.get('localAssetsSha256') == assets_digest and not rebuild:
            attempt['stage'] = 'Validate saved build'
            project = project_for(root, last)
            project = check_saved(root, last)
            record = dict(last, attempt=attempt['id'])
            print('No new shared changes. Reusing the verified successful build.', flush=True)
        else:
            attempt['stage'] = 'Prepare source worktree'
            project = checkout(root, repository, sha)
            attempt['stage'] = 'Install optional local assets'
            attempt['project'] = project.relative_to(root).as_posix()
            save(root / 'last-attempt.json', attempt)
            install_local_assets(project, source)
            if local_assets(root)[1] != assets_digest:
                raise ValueError('LocalAssets changed during installation. Rerun the build.')
            attempt['stage'] = 'Unity build and package validation'
            save(root / 'last-attempt.json', attempt)
            identity = build_project(project, unity)
            record = dict(sourceCommit=sha, localAssetsSha256=assets_digest, project=project.relative_to(root).as_posix(), build=identity,
                          zipSha256=digest(project / 'Builds' / PLAYER_ZIP), attempt=attempt['id'])
        # Promote only after Unity and all packaging checks succeeded. Prior work,
        # build output and user edits remain in their original version folders.
        save(root / 'last-success.json', record)
        attempt['status'] = 'success'
        save(root / 'last-attempt.json', attempt)
        build_diagnostics(root, attempt, project)
        print('Ready: ' + str(project / 'Builds' / PLAYER_ZIP))
        if not publish_after:
            print('Use Publish-Native.cmd to send this build to the Update button.')
            reveal(project)
        return project
    except BaseException as error:
        attempt['status'] = 'failed'
        save(root / 'last-attempt.json', attempt)
        build_diagnostics(root, attempt, project, error)
        raise


def reveal(project):
    if os.name == 'nt':
        try:
            os.startfile(str(project / 'Builds'))
        except OSError:
            print('Open the output folder shown above to test or share the build.')


def publish(root, status=False):
    record = previous(root)
    attempt_path = root / 'last-attempt.json'
    plain_path(attempt_path)
    attempt = json.loads(attempt_path.read_text(encoding='utf-8')) if attempt_path.is_file() else {}
    if not record or attempt.get('status') != 'success' or attempt.get('id') != record.get('attempt'):
        raise ValueError('The latest build attempt did not succeed. Run Build-Windows.cmd successfully before publishing.')
    project = check_saved(root, record)
    print('Selected successful source: ' + record['sourceCommit'], flush=True)
    spec = importlib.util.spec_from_file_location('chip_native_validator', project / 'tools/publish_native_update.py')
    validator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(validator)
    feed = project / 'Builds/NativeUpdateFeed'
    local = validator.read_manifest((feed / 'latest.json').read_bytes())
    if status:
        validator.show_status(local, validator.remote_manifest())
        return
    channel = root / 'Cache/native-publisher-v2.git'
    plain_path(channel)
    result = native_publish.publish(feed, channel, local, validator, run)
    save(root / 'last-publication.json', dict(result, selectedSourceCommit=record['sourceCommit']))


def perform(root, action, rebuild=False):
    if action in ('build', 'build-publish'):
        build(root, rebuild, publish_after=action == 'build-publish')
        if action == 'build-publish':
            print('Build checks passed. Publishing to the existing test Update channel...', flush=True)
            publish(root)
    else:
        publish(root, action == 'status')


def main():
    global GIT_EXECUTABLE
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('build', 'build-publish', 'publish', 'status'))
    parser.add_argument('--rebuild', action='store_true', help='Rebuild even when the shared source has not changed')
    args = parser.parse_args()
    GIT_EXECUTABLE = select_git()
    root = Path(__file__).absolute().parent
    with locked(root):
        perform(root, args.action, args.rebuild)


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        detail = ('Process exited with code ' + str(error.returncode) + '. See the error details above.'
                  if isinstance(error, subprocess.CalledProcessError) else str(error))
        print('Builder stopped: ' + detail, file=sys.stderr)
        if isinstance(error, subprocess.CalledProcessError) and error.stderr:
            print(str(error.stderr)[-8000:], file=sys.stderr)
        print('Source folders and successful builds were kept. If only uploading failed, rerun Publish-Native.cmd to retry without rebuilding.', file=sys.stderr)
        sys.exit(1)
    except KeyboardInterrupt:
        print('Stopped. Previous builds were kept. Rerun the same command to continue.', file=sys.stderr)
        sys.exit(1)
