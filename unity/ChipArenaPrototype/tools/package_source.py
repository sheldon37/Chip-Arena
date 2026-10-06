"""Package only the isolated Unity project; never include commercial assets or editor caches."""
from pathlib import Path
import hashlib
import json
import re
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
TOP = {'Assets', 'Packages', 'ProjectSettings', 'Tests', 'tools'}
FILES = {'README.md', 'PROTOTYPE-SCOPE.md', 'VALIDATION.md', 'Build-Windows.cmd', '.gitignore'}
SKIP = {'bin', 'obj', '__pycache__', 'LocalAssets', 'Library', 'Temp', 'Logs', 'Builds', 'UserSettings'}


def selected():
    for path in sorted(ROOT.rglob('*')):
        if path.is_symlink():
            raise ValueError('Symlinks are not allowed: ' + str(path))
        if not path.is_file():
            continue
        rel = path.relative_to(ROOT)
        if any(part in SKIP for part in rel.parts) or path.name == 'LocalAssets.meta':
            continue
        if rel.parts[0] in TOP or rel.as_posix() in FILES:
            yield path, rel


def check():
    manifest = json.loads((ROOT / 'Packages/manifest.json').read_text())
    assert all(k.startswith('com.unity.modules.') and v == '1.0.0' for k, v in manifest['dependencies'].items())
    guids = set()
    for path, rel in selected():
        assert path.stat().st_size < 1024 * 1024, f'Unexpected large source asset: {rel}'
        if rel.parts[0] == 'Assets' and path.suffix != '.meta':
            assert Path(str(path) + '.meta').is_file(), f'Missing meta: {rel}'
        if path.suffix == '.meta':
            match = re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(), re.M)
            assert match and match[1] not in guids, f'Invalid/duplicate GUID: {rel}'
            guids.add(match[1])
    for name in FILES:
        assert (ROOT / name).is_file(), f'Missing {name}'
    assert (ROOT / 'Assets/ChipArena/Scenes/Prototype.unity').is_file()
    print(f'Project structure passed; {len(list(selected()))} source files; no package downloads or Editor execution.')


if __name__ == '__main__':
    check()
    if '--check' not in sys.argv:
        destination = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT / 'Builds/Chip-Arena-Unity-TEST-Source.zip'
        destination.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(destination, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for path, rel in selected():
                info = zipfile.ZipInfo('ChipArenaPrototype/' + rel.as_posix(), (2026, 10, 6, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o100644 << 16
                archive.writestr(info, path.read_bytes())
        digest = hashlib.sha256(destination.read_bytes()).hexdigest()
        destination.with_suffix(destination.suffix + '.sha256').write_text(digest + '  ' + destination.name + '\n')
        print(str(destination))
        print('SHA256 ' + digest)
