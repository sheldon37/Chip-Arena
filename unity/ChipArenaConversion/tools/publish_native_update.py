"""Validate/stage a built Unity feed. --publish explicitly commits/pushes only its isolated native path."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
from urllib.error import HTTPError
from urllib.request import Request, urlopen
import zipfile

CHANNEL_REPO = 'https://github.com/sheldon37/chip-arena-test-updates.git'
PRODUCT = ('chip-arena-unity', 'native-test', 'windows-x64')
HEX = re.compile(r'^[0-9a-f]{64}$')
MAX_PART = 32 * 1024 * 1024
FEED_URL = 'https://raw.githubusercontent.com/sheldon37/chip-arena-test-updates/main/native/windows-x64/latest.json'
PROJECT = Path(__file__).resolve().parents[1]


def read_manifest(data):
    if len(data) > 128 * 1024:
        raise ValueError('Manifest too large')
    m = json.loads(data.decode('utf-8-sig'))
    b = m['build']
    if m['schema'] != 1 or b['schema'] != 1 or tuple(b[k] for k in ('product', 'channel', 'platform')) != PRODUCT or b['bootstrap'] != 1 or b['revision'] <= 0 or not HEX.fullmatch(b['version']):
        raise ValueError('Wrong native build identity')
    if not HEX.fullmatch(m['sha256']) or not 0 < m['bytes'] <= 2 * 1024**3 or not 0 < m['unpackedBytes'] <= 4 * 1024**3 or not 3 <= m['files'] <= 20000 or not 1 <= len(m['parts']) <= 64:
        raise ValueError('Invalid archive bounds')
    return m


def remote_manifest():
    request = Request(FEED_URL, headers={'Cache-Control': 'no-cache', 'User-Agent': 'ChipArena-Native-Publisher/1'})
    try:
        with urlopen(request, timeout=20) as response:
            return read_manifest(response.read(128 * 1024 + 1))
    except HTTPError as error:
        if error.code == 404:
            return None
        raise


def publication_problem(local, current):
    if current is None:
        return None
    if current['build']['version'] == local['build']['version']:
        return 'This source version is already published; build changed source before publishing again.'
    if current['build']['revision'] >= local['build']['revision']:
        return 'Refusing to publish an older build; check the build PC clock and rebuild changed source.'
    return None


def show_status(local, current):
    for label, manifest in [('Local build', local), ('Published', current)]:
        if manifest is None:
            print(label + ': no Unity update has been published yet (HTTP 404).')
        else:
            b = manifest['build']
            print(f"{label}: {b['version']} | revision {b['revision']} | {manifest['bytes'] / 1024**2:.1f} MiB")
    print(publication_problem(local, current) or 'This build can be published to the native test channel.')
    print('An installed player offers it only when its source version differs and its revision is older.')


def validate_build_log(feed, log):
    if not log.is_file():
        raise ValueError('Builds/build.log is missing. Finish Build-Windows.cmd first.')
    text = log.read_text(encoding='utf-8-sig', errors='replace')
    if 'NATIVE_UPDATE_FEED_READY:' not in text or 'UNITY_TEST_BUILD_READY:' not in text:
        raise ValueError('The last Windows build did not finish both package gates. See Builds/build.log.')
    identity = log.parent / 'Windows/app/native-build.json'
    if not identity.is_file() or json.loads(identity.read_text(encoding='utf-8-sig')) != read_manifest((feed / 'latest.json').read_bytes())['build']:
        raise ValueError('The feed does not match the packaged Windows player. Run Build-Windows.cmd again.')


def git(channel, *parts):
    return subprocess.check_output(['git', '-C', str(channel), *parts], text=True).strip()


def prepare_channel(channel):
    if not channel.exists():
        subprocess.run(['git', 'clone', '--branch', 'main', CHANNEL_REPO, str(channel)], check=True)
    if Path(git(channel, 'rev-parse', '--show-toplevel')).resolve() != channel.resolve():
        raise ValueError('The channel checkout must be the repository root, not a folder inside another repository.')
    remote = git(channel, 'remote', 'get-url', 'origin')
    if remote not in (CHANNEL_REPO, CHANNEL_REPO[:-4], 'git@github.com:sheldon37/chip-arena-test-updates.git'):
        raise ValueError('The checkout is not the approved test-channel repository')
    if git(channel, 'status', '--porcelain') or git(channel, 'branch', '--show-current') != 'main':
        raise ValueError('Use a clean main checkout of the channel repository; no files were reset.')
    git(channel, 'fetch', 'origin', 'main')
    if git(channel, 'rev-parse', 'HEAD') != git(channel, 'rev-parse', 'origin/main'):
        ancestor = subprocess.run(['git', '-C', str(channel), 'merge-base', '--is-ancestor', 'HEAD', 'origin/main'])
        if ancestor.returncode != 0:
            raise ValueError('The channel checkout has unpublished or divergent commits. Resolve them or choose a fresh --channel-checkout; no reset was performed.')
        git(channel, 'merge', '--ff-only', 'origin/main')
    current = channel / 'native/windows-x64/latest.json'
    return read_manifest(current.read_bytes()) if current.exists() else None


def validate(feed):
    metadata = feed / 'latest.json'
    if metadata.stat().st_size > 128 * 1024:
        raise ValueError('Manifest too large')
    m = read_manifest(metadata.read_bytes())
    b = m['build']
    size = 0
    digest = hashlib.sha256()
    with tempfile.TemporaryFile() as assembled:
        for index, part in enumerate(m['parts']):
            if part['name'] != f"{m['sha256']}-{index:04d}.part" or not 0 < part['bytes'] <= MAX_PART or not HEX.fullmatch(part['sha256']):
                raise ValueError('Invalid update part')
            path = feed / 'objects' / part['name']
            if path.is_symlink() or path.stat().st_size != part['bytes']:
                raise ValueError('Part size/link mismatch')
            h = hashlib.sha256()
            with path.open('rb') as f:
                while data := f.read(1024 * 1024):
                    h.update(data)
                    digest.update(data)
                    assembled.write(data)
                    size += len(data)
            if h.hexdigest() != part['sha256']:
                raise ValueError('Part checksum mismatch')
        if size != m['bytes'] or digest.hexdigest() != m['sha256']:
            raise ValueError('Archive checksum mismatch')
        assembled.seek(0)
        with zipfile.ZipFile(assembled) as archive:
            rows = archive.infolist()
            if len(rows) != m['files'] or sum(p.file_size for p in rows) != m['unpackedBytes']:
                raise ValueError('Archive inventory mismatch')
            names = set()
            for row in rows:
                path = row.filename
                segments = path.split('/')
                if not path or len(path) > 220 or '\\' in path or any(not s or s in ('.', '..') or s.endswith(('.', ' ')) or re.search(r'[\x00-\x1f\x7f<>:"|?*]', s) or re.fullmatch(r'(CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|CLOCK\$|COM[0-9¹²³]|LPT[0-9¹²³])', s.split('.')[0], re.I) for s in segments):
                    raise ValueError('Unsafe archive path')
                if path.lower() in names or (row.external_attr >> 16) & 0xf000 not in (0, 0x8000):
                    raise ValueError('Duplicate, link or non-file archive entry')
                names.add(path.lower())
            if not {'native-build.json', 'unityplayer.dll', 'chiparena-native-conversion-test.exe'} <= names or not any(p.startswith('chiparena-native-conversion-test_data/') for p in names):
                raise ValueError('Incomplete player')
            if json.loads(archive.read('native-build.json')) != b:
                raise ValueError('Embedded build differs from manifest')
            if archive.testzip() is not None:
                raise ValueError('Corrupt ZIP entry')
    return m


def stage(feed, channel):
    m = validate(feed)
    destination = channel / 'native' / 'windows-x64'
    current = destination / 'latest.json'
    problem = publication_problem(m, read_manifest(current.read_bytes()) if current.exists() else None)
    if problem:
        raise ValueError(problem)
    (destination / 'objects').mkdir(parents=True, exist_ok=True)
    for part in m['parts']:
        target = destination / 'objects' / part['name']
        shutil.copyfile(feed / 'objects' / part['name'], target.with_suffix('.tmp'))
        target.with_suffix('.tmp').replace(target)
    shutil.copyfile(feed / 'latest.json', destination / 'latest.tmp')
    (destination / 'latest.tmp').replace(current)
    return m


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--feed', type=Path, default=PROJECT / 'Builds/NativeUpdateFeed')
    p.add_argument('--channel-checkout', type=Path)
    mode = p.add_mutually_exclusive_group()
    mode.add_argument('--publish', action='store_true', help='Explicitly commit/push the built player to the native test feed')
    mode.add_argument('--status', action='store_true', help='Compare the validated local build with the public feed; write nothing')
    p.add_argument('--confirm', action='store_true', help='Require typed confirmation of this build before publication')
    p.add_argument('--build-log', type=Path, help='Require successful Windows build markers and matching packaged identity')
    args = p.parse_args(argv)
    if args.confirm and not args.publish:
        p.error('--confirm requires --publish')
    if not args.status and args.channel_checkout is None:
        if args.publish:
            args.channel_checkout = PROJECT / 'Builds/native-channel-checkout'
        else:
            p.error('Dry run requires --channel-checkout pointing to a scratch folder, or use --status')
    if not (args.feed / 'latest.json').is_file():
        raise ValueError('No compiled native update package was found. Run Build-Windows.cmd first; a source ZIP cannot update the game.')
    if args.build_log is not None:
        validate_build_log(args.feed, args.build_log)
    local = validate(args.feed)
    if args.status:
        show_status(local, remote_manifest())
        return 0
    if not args.publish:
        stage(args.feed, args.channel_checkout)
        print('Prepared native/windows-x64 only; nothing committed or published.')
        return 0

    current = prepare_channel(args.channel_checkout)
    show_status(local, current)
    problem = publication_problem(local, current)
    if problem:
        raise ValueError(problem)
    # Verify the exact staging path before asking the owner to publish this build.
    with tempfile.TemporaryDirectory(prefix='chip-native-preview-') as scratch:
        stage(args.feed, Path(scratch))
    if args.confirm:
        phrase = 'PUBLISH ' + local['build']['version'][:12]
        print('Publish the compiled player to ' + CHANNEL_REPO)
        print('Continue after testing this Windows build. The compiled files will be publicly downloadable.')
        try:
            answer = input('Type ' + phrase + ' to publish, or press Enter to cancel: ')
        except EOFError:
            answer = ''
        if answer.strip() != phrase:
            print('Cancelled. Nothing published.')
            return 0
    # Recheck after confirmation: another publisher may have advanced main.
    current = prepare_channel(args.channel_checkout)
    problem = publication_problem(local, current)
    if problem:
        raise ValueError(problem)
    result = stage(args.feed, args.channel_checkout)
    if result != local:
        raise ValueError('The feed changed during publication. Nothing committed or pushed; inspect the channel checkout.')
    git(args.channel_checkout, 'add', '--', 'native/windows-x64')
    git(args.channel_checkout, 'commit', '-m', 'Publish Unity native test build ' + result['build']['version'][:12])
    git(args.channel_checkout, 'push', 'origin', 'HEAD:main')
    print('Published native Unity build ' + result['build']['version'])
    print('Channel commit: ' + git(args.channel_checkout, 'rev-parse', 'HEAD'))
    print('Feed: ' + FEED_URL)
    try:
        served = remote_manifest()
        if served == result:
            print('The public feed serves this build. Open the older installed copy and choose Check for updates.')
        else:
            print('Git push succeeded; the public feed has not refreshed to this build yet. Recheck with --status in a few minutes.')
    except (OSError, ValueError, KeyError, TypeError) as error:
        print('Git push succeeded; public feed verification is pending: ' + str(error))
        print('Use --status to recheck before testing the in-game update.')
    return 0


def cli():
    try:
        return main()
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile, subprocess.CalledProcessError) as error:
        print('Native update: ' + str(error), file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print('Interrupted. Inspect the channel checkout before retrying.', file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(cli())
