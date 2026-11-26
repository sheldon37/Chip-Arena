"""Replace only the effects bank in a candidate LocalAssets zip."""
import argparse
import copy
import json
import os
from pathlib import Path
import shutil
import tempfile
import zipfile

import effects_bank as fx
import install_local_assets as installer
from pack_visual_body import base_prefix, entry


def pack(base, output, project):
    if base.resolve() == output.resolve():
        raise installer.Problem('Choose a separate output zip')
    local = project / 'Assets/LocalAssets'
    files = fx.inventory(local)
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=output.parent) as scratch:
        candidate = Path(scratch) / 'candidate.zip'
        with zipfile.ZipFile(base) as source, zipfile.ZipFile(candidate, 'w') as target:
            prefix = base_prefix(source)
            for member in source.infolist():
                if member.filename.startswith(prefix) and fx.owns(member.filename[len(prefix):]):
                    continue
                with source.open(member) as src, target.open(copy.copy(member), 'w') as dst:
                    shutil.copyfileobj(src, dst)
            for rel in sorted(files):
                target.writestr(entry(prefix + rel), (local / rel).read_bytes())
            target.writestr(entry(prefix + fx.MANIFEST), json.dumps({'schema': 1, 'files': files}, sort_keys=True))
        if installer.main([str(candidate), '--project', str(project), '--check']):
            raise installer.Problem('candidate failed installer verification')
        os.replace(candidate, output)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('base', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--project', type=Path, default=installer.ROOT)
    args = parser.parse_args()
    try:
        pack(args.base, args.output, args.project)
    except (installer.Problem, OSError, zipfile.BadZipFile) as error:
        parser.exit(1, str(error) + '\n')
