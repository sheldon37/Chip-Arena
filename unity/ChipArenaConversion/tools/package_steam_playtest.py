"""Stage the verified Windows player and SteamPipe scripts. Does not contact Steam."""
import argparse
from pathlib import Path
import re
import shutil
import zipfile
import tempfile

import publish_native_update as native


def steam_id(value):
    if not re.fullmatch(r'[1-9][0-9]{0,9}', value) or int(value) > 4294967295:
        raise argparse.ArgumentTypeError('Expected a positive numeric Steam ID')
    return value


def stage(feed, destination, app_id=None, depot_id=None):
    if bool(app_id) != bool(depot_id):
        raise ValueError('Supply both the Playtest App ID and Windows depot ID')
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists():
        raise ValueError('Choose a fresh output directory: ' + str(destination))
    with tempfile.TemporaryDirectory(dir=destination.parent) as temp:
        bundle = Path(temp) / 'bundle'
        content = bundle / 'content'
        content.mkdir(parents=True)
        build = native.validate(feed, extract_to=content)['build']
        exe = 'Chip Arena.exe' if build['bootstrap'] == 2 else 'ChipArena-Native-Conversion-TEST.exe'
        if not (content / exe).is_file() or not (content / (Path(exe).stem + '_Data')).is_dir():
            raise ValueError('Steam launch target or matching Unity data directory missing')
        for debug_folder in content.glob('*_DoNotShip'):
            if debug_folder.is_dir():
                shutil.rmtree(debug_folder)
        (content / 'steam-playtest.txt').write_text('Chip Arena Playtest\n', encoding='ascii')
        app = app_id or 'PLAYTEST_APP_ID'
        depot = depot_id or 'WINDOWS_DEPOT_ID'
        script = '''"AppBuild"
{
    "AppID" "APP_ID"
    "Desc" "Chip Arena Playtest REVISION"
    "ContentRoot" "content"
    "BuildOutput" "steam-cache"
    "Depots"
    {
        "DEPOT_ID"
        {
            "FileMapping"
            {
                "LocalPath" "*"
                "DepotPath" "."
                "Recursive" "1"
            }
        }
    }
}
'''.replace('APP_ID', app).replace('DEPOT_ID', depot).replace('REVISION', str(build['revision']))
        (bundle / 'app_build.vdf').write_text(script, encoding='utf-8')
        (bundle / 'launch.txt').write_text(exe + '\n', encoding='utf-8')
        bundle.rename(destination)
    return exe


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--feed', type=Path, default=native.PROJECT / 'Builds/NativeUpdateFeed')
    parser.add_argument('--output', type=Path)
    parser.add_argument('--app-id', type=steam_id)
    parser.add_argument('--depot-id', type=steam_id)
    args = parser.parse_args()
    try:
        identity = native.read_manifest((args.feed / 'latest.json').read_bytes())['build']
        destination = args.output or native.PROJECT / 'Builds/SteamPlaytest' / str(identity['revision'])
        exe = stage(args.feed, destination, args.app_id, args.depot_id)
    except (ValueError, OSError, KeyError, zipfile.BadZipFile) as error:
        parser.exit(1, f'Steam packaging failed: {error}\n')
    print(f'Staged {destination}\nSteamworks Windows launch executable: {exe}')
    if not args.app_id:
        print('Fill in both placeholder IDs in app_build.vdf before uploading.')


if __name__ == '__main__':
    main()
