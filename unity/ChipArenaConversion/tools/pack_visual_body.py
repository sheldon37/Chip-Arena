"""Add the owner's baked visual body to a licensed-assets zip (docs/BOZO-INTEGRATION.md), for the builder to install.

usage: python3 -I tools/pack_visual_body.py BASE_ZIP OUTPUT_ZIP [--project DIR]

Reads Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaVisualBody.prefab (the local bake, git-ignored) and follows its
GUID references through the text assets and every .meta under Assets/LocalAssets: the meshes, materials and textures it
uses, each with its .meta, every folder's .meta, the data files (.bytes, the model's motion bank) beside the prefab in its
Resources folder and the textures (.png) in that folder and the ones under it (the wardrobe's own colour maps, the cloth's paint
maps), which the game loads by name. Assets outside LocalAssets (the tracked shaders) are left to the source;
anything that is not a mesh, material, texture, prefab or animator controller (shaders, shader graphs, scripts) is left out
and listed. It writes OUTPUT_ZIP as BASE_ZIP's members (the effects audio and the rest, unchanged) plus those files and
visual-body.json, which lists each with its SHA-256, then checks the result as tools/install_local_assets.py does. BASE_ZIP
is never modified.
The zip is deterministic: the same inputs give the same bytes. Exit status 1 on any problem.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import sys
import tempfile
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
import local_cloth

sys.path.insert(0, str(Path(__file__).resolve().parent))
import install_local_assets as installer  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
PREFAB = 'ChipArenaLocal/Resources/' + installer.VISUAL_BODY_PREFAB
TEXT_ASSETS = {'.prefab', '.mat', '.asset', '.controller'}
GUID_REF = re.compile(rb'guid: ([0-9a-f]{32})')
STAMP = (2026, 1, 1, 0, 0, 0)


def guids(project):
    """guid -> asset path for every .meta under Assets."""
    found = {}
    for meta in (project / 'Assets').rglob('*.meta'):
        with open(meta, 'rb') as f:
            m = GUID_REF.search(f.read(4096))
        if m:
            found[m[1].decode()] = meta.with_suffix('')
    return found


def dependencies(project):
    """(listed LocalAssets-relative paths, skipped notes): the prefab's files and their metas, and every folder's .meta."""
    local = project / 'Assets/LocalAssets'
    start = local / PREFAB
    if not start.is_file():
        raise installer.Problem('no bake at ' + str(start))
    by_guid = guids(project)
    seen, queue, skipped = set(), [start], set()
    while queue:
        path = queue.pop()
        if path in seen:
            continue
        seen.add(path)
        blobs = []
        if path.suffix.lower() in TEXT_ASSETS:
            blobs.append(path.read_bytes())
        meta = path.with_name(path.name + '.meta')
        if meta.is_file():
            blobs.append(meta.read_bytes())
        for blob in blobs:
            for raw in GUID_REF.findall(blob):
                target = by_guid.get(raw.decode())
                if target is None:
                    continue  # built-in or package assets
                try:
                    target.relative_to(local)
                except ValueError:
                    continue  # tracked in the repository
                if target.suffix.lower() not in installer.VISUAL_BODY_KINDS - {'.meta'}:
                    skipped.add(target.relative_to(local).as_posix())
                    continue
                queue.append(target)
    # The bake's data files beside the prefab and the textures in its Resources folders (the game and the local cloth bridge
    # load them from Resources by name; nothing references them).
    for data in sorted(start.parent.glob('*.bytes')):
        seen.add(data)
    for data in sorted(start.parent.rglob('*.png')):
        seen.add(data)
    listed = set()
    for path in seen:
        rel = path.relative_to(local)
        listed.add(rel.as_posix())
        if path.with_name(path.name + '.meta').is_file():
            listed.add(rel.as_posix() + '.meta')
        for folder in list(rel.parents)[:-1]:
            if (local / (folder.as_posix() + '.meta')).is_file():
                listed.add(folder.as_posix() + '.meta')
    return sorted(listed), sorted(skipped)


def base_prefix(archive):
    """'' when BASE_ZIP holds its content at the top, else its single wrapping folder (as the installer accepts)."""
    names = [n for n in archive.namelist() if not n.endswith('/')]
    if 'Resources/EffectsAudio/manifest.json' in names:
        return ''
    tops = {n.split('/', 1)[0] for n in names if '/' in n and not n.startswith('__MACOSX')}
    if len(tops) == 1:
        top = tops.pop()
        if top + '/Resources/EffectsAudio/manifest.json' in names:
            return top + '/'
    raise installer.Problem('BASE_ZIP has no Resources/EffectsAudio/manifest.json')


def entry(name):
    info = zipfile.ZipInfo(name, STAMP)
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = 0o644 << 16
    return info


def pack(base, output, project, with_cloth=False):
    local = project / 'Assets/LocalAssets'
    listed, skipped = dependencies(project)
    files = {rel: installer.sha256(local / rel) for rel in listed}
    cloth = local_cloth.collect(local) if with_cloth else {}
    manifest = {'schema': 1, 'prefab': PREFAB, 'files': files,
                'note': 'Written by tools/pack_visual_body.py from the owner\'s local bake; installed by tools/install_local_assets.py.'}
    with zipfile.ZipFile(base) as source:
        prefix = base_prefix(source)
        replaced = {prefix + rel for rel in files} | {prefix + installer.VISUAL_BODY_MANIFEST}
        if with_cloth:
            replaced |= {prefix + rel for rel in cloth} | {prefix + local_cloth.MANIFEST}
            # Replace the prior cloth listing completely; never leave stale scripts in the bundle.
            if prefix + local_cloth.MANIFEST in source.namelist():
                old = json.loads(source.read(prefix + local_cloth.MANIFEST))
                replaced |= {prefix + rel for rel in old.get('files', {})}
        kept = sorted(n for n in source.namelist() if not n.endswith('/') and n not in replaced)
        output.parent.mkdir(parents=True, exist_ok=True)
        staged = output.with_name(output.name + '.writing')
        with zipfile.ZipFile(staged, 'w') as out:
            for name in kept:
                out.writestr(entry(name), source.read(name))
            for rel in sorted(files.keys() | cloth.keys()):
                out.writestr(entry(prefix + rel), (local / rel).read_bytes())
            if with_cloth:
                out.writestr(entry(prefix + local_cloth.MANIFEST), json.dumps({'schema': 1, 'files': cloth}, indent=1, sort_keys=True) + '\n')
            out.writestr(entry(prefix + installer.VISUAL_BODY_MANIFEST), json.dumps(manifest, indent=1, sort_keys=True) + '\n')
    with tempfile.TemporaryDirectory(prefix='visual-body-check-') as scratch:
        installer.safe_extract(staged, Path(scratch))
        content = installer.content_root(Path(scratch))
        installer.verify(content)
        body = installer.verify_visual_body(content)
        local_cloth.verify(content)
    os.replace(staged, output)
    size = sum((local / rel).stat().st_size for rel in files)
    return len(body), size, skipped, len(kept)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    parser.add_argument('base', help='the current licensed-assets zip (never modified)')
    parser.add_argument('output', help='the zip to write')
    parser.add_argument('--project', default=str(ROOT), help='Unity project root with the local bake (default: this project)')
    parser.add_argument('--with-cloth', action='store_true', help='include trusted local Magica code and the current cloth bridge; exclude examples')
    args = parser.parse_args(argv)
    base, output, project = Path(args.base).resolve(), Path(args.output).resolve(), Path(args.project).resolve()
    if output == base:
        print('OUTPUT_ZIP must differ from BASE_ZIP', file=sys.stderr)
        return 1
    try:
        count, size, skipped, kept = pack(base, output, project, args.with_cloth)
    except (installer.Problem, OSError, zipfile.BadZipFile) as problem:
        print('Visual body not packed:\n' + str(problem), file=sys.stderr)
        return 1
    print(f'Packed the visual body: {count} files, {size / 1e6:.2f} MB, beside {kept} members of {base.name}, into {output}.')
    if skipped:
        print('Left out (not meshes, materials, textures or prefabs): ' + ', '.join(skipped))
    return 0


if __name__ == '__main__':
    sys.exit(main())
