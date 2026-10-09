"""Validate a completed UBA export and add a delivery ZIP beside its player. Never publishes."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PureWindowsPath
import re
import subprocess
import sys
import tempfile
import zipfile

import publish_native_update as publisher

SEED = 'Chip-Arena-Native-Conversion-TEST-Windows.zip'
DELIVERY = 'Chip-Arena-Native-Cloud-Delivery.zip'


def editor_log_path(value):
    # Unity records a native Windows path even when the post-build Python is
    # Cygwin. Convert before pathlib interprets separators and the drive name.
    if sys.platform == 'cygwin' and PureWindowsPath(value).is_absolute():
        try:
            value = subprocess.check_output(['cygpath', '-ua', value], text=True).strip()
        except subprocess.CalledProcessError as error:
            raise ValueError('Could not convert the Unity Editor log path') from error
        if not value:
            raise ValueError('Empty converted Unity Editor log path')
    return Path(value)


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        while block := stream.read(1024 * 1024):
            value.update(block)
    return value.hexdigest()


def validate_seed(seed, feed, manifest):
    with tempfile.TemporaryFile() as stream:
        for part in manifest['parts']:
            with (feed / 'objects' / part['name']).open('rb') as source:
                while block := source.read(1024 * 1024):
                    stream.write(block)
        stream.seek(0)
        with zipfile.ZipFile(stream) as player, zipfile.ZipFile(seed) as starter:
            required = {'ChipArena-Native-Conversion-TEST.exe', 'ChipArena-Native-Conversion-TEST.exe.config',
                        'native-install.txt', 'READ-ME.txt'}
            names = starter.namelist()
            if len(set(names)) != len(names) or set(names) != required | {'app/' + name for name in player.namelist()}:
                raise ValueError('Incomplete or unexpected starter ZIP inventory')
            if starter.read('native-install.txt') != b'chip-arena-unity/1\n' or starter.testzip() is not None:
                raise ValueError('Invalid or corrupt starter ZIP')
            for row in player.infolist():
                other = starter.getinfo('app/' + row.filename)
                if other.file_size != row.file_size:
                    raise ValueError('Starter ZIP differs from the native feed')
                with player.open(row) as left, starter.open(other) as right:
                    while block := left.read(1024 * 1024):
                        if block != right.read(len(block)):
                            raise ValueError('Starter ZIP differs from the native feed')


def package(project, player, environment):
    builds = project / 'Builds'
    receipt = json.loads((builds / 'cloud-build-receipt.json').read_text(encoding='utf-8-sig'))
    for field, variable in [('sourceCommit', 'BUILD_REVISION'), ('buildTarget', 'BUILD_TARGET'), ('buildNumber', 'UCB_BUILD_NUMBER')]:
        if not environment.get(variable) or receipt.get(field) != environment[variable]:
            raise ValueError('Missing or stale cloud build context: ' + variable)
    if receipt.get('schema') != 1 or not re.fullmatch('[0-9a-f]{40}', receipt['sourceCommit']) or receipt.get('unityVersion') != '6000.3.24f1':
        raise ValueError('Unexpected cloud receipt schema, commit or Editor version')
    if not player.is_file() or player.suffix.lower() != '.exe':
        raise ValueError('UNITY_PLAYER_PATH must identify the exported Windows executable')
    feed = builds / 'NativeUpdateFeed'
    manifest = publisher.validate(feed)
    if receipt.get('build') != manifest['build']:
        raise ValueError('Cloud receipt differs from the native feed')
    identity = builds / 'Windows/app/native-build.json'
    if json.loads(identity.read_text(encoding='utf-8-sig')) != manifest['build']:
        raise ValueError('Cloud player identity differs from the native feed')
    log = editor_log_path(receipt['editorLog'])
    log_data = log.read_bytes()
    log_text = log_data.decode('utf-8-sig', errors='replace')
    for marker in ('NATIVE_UPDATE_FEED_READY:', 'UNITY_TEST_BUILD_READY:',
                   f"NATIVE_CLOUD_EXPORT_READY: {receipt['sourceCommit']} / {receipt['buildNumber']}"):
        if marker not in log_text:
            raise ValueError('Cloud Editor log did not finish package gates: ' + marker)
    seed = builds / SEED
    validate_seed(seed, feed, manifest)
    # The source receipt has a local log path; the portable receipt contains only provenance.
    receipt.pop('editorLog')
    receipt['seedSha256'] = digest(seed)
    receipt['updateSha256'] = manifest['sha256']
    receipt['buildLogSha256'] = hashlib.sha256(log_data).hexdigest()
    receipt['published'] = False
    output = player.parent / DELIVERY
    if output.exists():
        raise ValueError('Delivery already exists; use a fresh cloud export')
    temporary = output.with_suffix('.zip.tmp')
    try:
        # Player ZIP/parts are already compressed. Avoid another expensive compression pass.
        with zipfile.ZipFile(temporary, 'w', zipfile.ZIP_STORED) as archive:
            archive.write(seed, SEED)
            archive.write(feed / 'latest.json', 'NativeUpdateFeed/latest.json')
            for part in manifest['parts']:
                archive.write(feed / 'objects' / part['name'], 'NativeUpdateFeed/objects/' + part['name'])
            archive.write(identity, 'Windows/app/native-build.json')
            archive.writestr('build.log', log_data)
            archive.writestr('cloud-build-receipt.json', json.dumps(receipt, indent=2) + '\n')
        temporary.replace(output)
    finally:
        temporary.unlink(missing_ok=True)
    output.with_suffix('.zip.sha256').write_text(digest(output) + '  ' + output.name + '\n')
    print('NATIVE_CLOUD_DELIVERY_READY: ' + str(output))
    print('Source commit: ' + receipt['sourceCommit'] + '; candidate only; no feed publication.')
    return output


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True,
                        help='Unity project root, in the Python interpreter\'s path format')
    parser.add_argument('--player', type=Path, required=True)
    args = parser.parse_args()
    print('Cloud packaging Python: ' + sys.platform + '; project: ' + str(args.project), flush=True)
    package(args.project, args.player, os.environ)


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError, KeyError, zipfile.BadZipFile) as error:
        print('Cloud packaging failed: ' + str(error), file=sys.stderr)
        sys.exit(1)
