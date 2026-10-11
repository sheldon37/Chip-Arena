"""Package the small fetching builder, excluding source caches and local build state."""
import hashlib
from pathlib import Path
import sys
import zipfile

PROJECT = Path(__file__).resolve().parents[1]
FILES = ('Build-and-Publish.cmd', 'Build-Windows.cmd', 'Publish-Native.cmd',
         'Check-Update-Status.cmd', 'Open-Latest-Log.cmd', 'Run-Builder.cmd',
         'builder.py', 'native_publish.py', 'READ-ME.txt')


def package(destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(destination, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name in FILES:
            path = PROJECT / 'Builder' / name
            if path.is_symlink() or not path.is_file():
                raise ValueError('Missing or invalid builder file: ' + name)
            archive.writestr('Chip Arena Builder/' + name, path.read_bytes())
    checksum = hashlib.sha256(destination.read_bytes()).hexdigest()
    destination.with_suffix('.zip.sha256').write_text(checksum + '  ' + destination.name + '\n')
    print(str(destination))
    print('SHA256 ' + checksum)


if __name__ == '__main__':
    package(Path(sys.argv[1]).absolute() if len(sys.argv) > 1 else PROJECT / 'Builds/Chip-Arena-Builder.zip')
