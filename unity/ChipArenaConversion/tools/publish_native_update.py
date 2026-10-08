"""Validate/stage a built Unity feed. --publish explicitly commits/pushes only its isolated native path."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import zipfile

CHANNEL_REPO = 'https://github.com/sheldon37/chip-arena-test-updates.git'
PRODUCT = ('chip-arena-unity', 'native-test', 'windows-x64')
HEX = re.compile(r'^[0-9a-f]{64}$')
MAX_PART = 32 * 1024 * 1024


def validate(feed):
    metadata = feed / 'latest.json'
    if metadata.stat().st_size > 128 * 1024:
        raise ValueError('Manifest too large')
    m = json.loads(metadata.read_text(encoding='utf-8-sig'))
    b = m['build']
    if m['schema'] != 1 or b['schema'] != 1 or tuple(b[k] for k in ('product', 'channel', 'platform')) != PRODUCT or b['bootstrap'] != 1 or b['revision'] <= 0 or not HEX.fullmatch(b['version']):
        raise ValueError('Wrong native build identity')
    if not HEX.fullmatch(m['sha256']) or not 0 < m['bytes'] <= 2 * 1024**3 or not 0 < m['unpackedBytes'] <= 4 * 1024**3 or not 3 <= m['files'] <= 20000 or not 1 <= len(m['parts']) <= 64:
        raise ValueError('Invalid archive bounds')
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
    if current.exists():
        old = json.loads(current.read_text())['build']
        if old['version'] == m['build']['version']:
            raise ValueError('This source version is already published')
        if old['revision'] >= m['build']['revision']:
            raise ValueError('Refusing to publish an older build')
    (destination / 'objects').mkdir(parents=True, exist_ok=True)
    for part in m['parts']:
        target = destination / 'objects' / part['name']
        shutil.copyfile(feed / 'objects' / part['name'], target.with_suffix('.tmp'))
        target.with_suffix('.tmp').replace(target)
    shutil.copyfile(feed / 'latest.json', destination / 'latest.tmp')
    (destination / 'latest.tmp').replace(current)
    return m


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--feed', type=Path, required=True)
    p.add_argument('--channel-checkout', type=Path, required=True)
    p.add_argument('--publish', action='store_true', help='Explicitly commit/push the built player to the native test feed')
    args = p.parse_args()
    validate(args.feed)
    if args.publish:
        if not args.channel_checkout.exists():
            subprocess.run(['git', 'clone', '--branch', 'main', CHANNEL_REPO, str(args.channel_checkout)], check=True)
        def git(*parts):
            return subprocess.check_output(['git', '-C', str(args.channel_checkout), *parts], text=True).strip()
        remote = git('remote', 'get-url', 'origin')
        if remote not in (CHANNEL_REPO, CHANNEL_REPO[:-4], 'git@github.com:sheldon37/chip-arena-test-updates.git'):
            raise ValueError('The checkout is not the approved test-channel repository')
        if git('status', '--porcelain') or git('branch', '--show-current') != 'main':
            raise ValueError('Use a clean main checkout of the channel repository')
        git('fetch', 'origin', 'main')
        if git('rev-parse', 'HEAD') != git('rev-parse', 'origin/main'):
            raise ValueError('Update the channel checkout to origin/main first; no automatic merge or reset is performed')
    result = stage(args.feed, args.channel_checkout)
    if args.publish:
        git('add', '--', 'native/windows-x64')
        git('commit', '-m', 'Publish Unity native test build ' + result['build']['version'][:12])
        git('push', 'origin', 'HEAD:main')
        print('Published native Unity build ' + result['build']['version'])
    else:
        print('Prepared native/windows-x64 only; nothing committed or published.')


if __name__ == '__main__':
    main()
