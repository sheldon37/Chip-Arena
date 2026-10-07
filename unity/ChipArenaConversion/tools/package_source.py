"""Package only the isolated Unity project; exclude source packs and editor caches."""
from pathlib import Path
import hashlib
import json
import re
import struct
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
TOP = {'Assets', 'Packages', 'ProjectSettings', 'Tests', 'tools'}
FILES = {'README.md', 'CONVERSION-STATUS.md', 'VALIDATION.md', 'ICON.md', 'ANIMATIONS.md', 'Build-Windows.cmd', '.gitignore'}
SKIP = {'bin', 'obj', '__pycache__', 'LocalAssets', 'Library', 'Temp', 'Logs', 'Builds', 'UserSettings'}


def validate_dependencies(dependencies, catalog):
    if not isinstance(dependencies, dict) or not dependencies:
        raise ValueError('The prototype must declare its built-in module dependencies')
    for name, version in dependencies.items():
        if name not in catalog['packages'] or version != catalog['packages'][name]:
            raise ValueError(f'Unsupported dependency for Unity {catalog["editor_version"]}: {name}@{version}')


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
    catalog = json.loads((ROOT / 'tools/builtin-packages-6000.3.24f1.json').read_text())
    project_version = (ROOT / 'ProjectSettings/ProjectVersion.txt').read_text()
    match = re.search(r'^m_EditorVersion: (\S+)$', project_version, re.M)
    if not match or match[1] != catalog['editor_version']:
        raise ValueError('The built-in package catalog must match the pinned Editor version')
    validate_dependencies(manifest.get('dependencies'), catalog)
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
    icon = (ROOT / 'Assets/ChipArena/Branding/CAVoxelIcon.png').read_bytes()
    assert icon[:8] == b'\x89PNG\r\n\x1a\n' and icon[12:16] == b'IHDR', 'Launcher icon must be PNG'
    width, height, depth, color = struct.unpack('>IIBB', icon[16:26])
    assert width == height and width >= 256 and depth == 8 and color == 6, 'Launcher icon must be square RGBA'
    print(f'Project structure passed; {len(list(selected()))} source files; no package downloads or Editor execution.')


if __name__ == '__main__':
    check()
    if '--check' not in sys.argv:
        destination = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT / 'Builds/Chip-Arena-Native-Conversion-Source.zip'
        destination.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(destination, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for path, rel in selected():
                info = zipfile.ZipInfo('ChipArenaConversion/' + rel.as_posix(), (2026, 10, 7, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o100644 << 16
                archive.writestr(info, path.read_bytes())
        digest = hashlib.sha256(destination.read_bytes()).hexdigest()
        destination.with_suffix(destination.suffix + '.sha256').write_text(digest + '  ' + destination.name + '\n')
        print(str(destination))
        print('SHA256 ' + digest)
