"""Install the owner's licensed effects audio (and the visual body, when present) into Assets/LocalAssets (git-ignored, never
committed or packaged).

usage: python3 -I tools/install_local_assets.py SOURCE [--project DIR] [--check | --remove]

SOURCE is a checkout of the owner's private licensed-assets repository, or a .zip of its content. Either one holds
Resources/EffectsAudio/manifest.json at its top level or inside a single top folder. This script only reads and copies
files. It never runs anything from SOURCE, and a zip is read member by member (no absolute paths, no "..", no links,
bounded sizes).

It checks the manifest against the key contract in Assets/ChipArena/Core/NativeSoundBank.cs (the same rules as
NativeSoundBank.Parse), effect keys and the intro.<style> entrance-music keys alike (2D, so stereo, up to 4.8 s). Every listed clip must exist as Ogg Vorbis with a .meta, at 44.1 kHz, mono when positional and
within its hard length cap. Every folder needs its .meta, no unlisted file is allowed, and EffectsAudio must stay under
the 16 MB budget. It then replaces Assets/LocalAssets/Resources/EffectsAudio with the source's copy. Unity merges every
Resources folder, so the game loads it as Resources "EffectsAudio/...". Without it the game plays today's sounds.

The visual body (docs/BOZO-INTEGRATION.md) is optional: visual-body.json at the content root (never installed itself) lists
every file the baked body needs (tools/pack_visual_body.py writes it from the owner's bake) with its SHA-256. Only meshes,
materials, textures, prefabs and their .meta files, every folder's .meta, no scripts or shaders, nothing under the effects
bank, within VISUAL_BODY_BUDGET; the prefab is ChipArenaVisualBody.prefab in a Resources folder, which the game loads in place
of the voxel pilot body. They are copied over Assets/LocalAssets at their paths and nothing there is deleted, so installing
over a full local pack only rewrites the same files. Without it the pilots draw the voxel body.
--check verifies only; --remove deletes the installed EffectsAudio folder (the visual body stays). Exit status 1 on any
problem.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
BANK_SOURCE = ROOT / 'Assets/ChipArena/Core/NativeSoundBank.cs'
FIELDS = {'clips', 'gain', 'rate', 'jitter', 'priority', 'space', 'maxLength', 'cooldown', 'note'}
TOP_FIELDS = {'schema', 'events', 'generated', 'note'}
SPACES = {'P': '3d', 'F': '2d', 'O': 'loop'}
CLIP_NAME = re.compile(r'^[a-z0-9_-]{1,40}$')
GUID = re.compile(r'^guid: ([0-9a-f]{32})$', re.M)
MAX_ZIP_FILES, MAX_ZIP_BYTES, MAX_FILE_BYTES = 5000, 224 * 1024 * 1024, 16 * 1024 * 1024
# The visual body's budget (raised from 40 MB on 2026-10-25: the animation pack's motion bank grew it to 38.7 MB; to 72 MB on
# 2026-10-26: the wardrobe's pieces and the prefab that carries them grew it to 58.2 MB; to 160 MB on 2026-10-28: the pack
# banks beside it, docs/PACK-BANK.md, the stage kit alone 12.7 MB); the zip's own MAX_ZIP_BYTES (its members' total, raised
# from 64 MiB on 2026-10-26 to hold the wardrobe beside the effects audio, to 224 MiB on 2026-10-28 for the pack banks) still
# bounds the download.
VISUAL_BODY_MANIFEST, VISUAL_BODY_PREFAB, VISUAL_BODY_BUDGET = 'visual-body.json', 'ChipArenaVisualBody.prefab', 160 * 1000 * 1000
VISUAL_BODY_KINDS = {'.prefab', '.mat', '.fbx', '.png', '.tga', '.jpg', '.jpeg', '.asset', '.controller', '.bytes', '.meta'}
SHA256 = re.compile(r'^[0-9a-f]{64}$')


class Problem(Exception):
    pass


def contract(source=BANK_SOURCE):
    """The key contract (key -> (space, cap)), schema, clip limit and byte budget from NativeSoundBank.cs."""
    text = source.read_text(encoding='utf-8')
    keys = {m[1]: (SPACES[m[2]], float(m[3])) for m in re.finditer(r'K\("([a-z0-9.-]+)",([PFO]),([0-9.]+)f,[HNL]\)', text)}
    schema = int(re.search(r'Schema=(\d+)', text)[1])
    clips = int(re.search(r'MaxClips=(\d+)', text)[1])
    budget = int(re.search(r'BudgetBytes=(\d+)', text)[1])
    if len(keys) < 100:
        raise Problem('could not read the key contract from ' + str(source))
    return keys, schema, clips, budget


def ogg_info(data):
    """(seconds, channels, rate) of an Ogg Vorbis stream, walking its pages; None when it is not one."""
    if len(data) < 58 or data[:4] != b'OggS':
        return None
    segments = data[26]
    body = 27 + segments
    if data[body:body + 7] != b'\x01vorbis':
        return None
    channels, rate = data[body + 11], int.from_bytes(data[body + 12:body + 16], 'little')
    at, granule = 0, 0
    while at + 27 <= len(data) and data[at:at + 4] == b'OggS':
        count = data[at + 26]
        if at + 27 + count > len(data):
            return None
        size = 27 + count + sum(data[at + 27:at + 27 + count])
        granule = int.from_bytes(data[at + 6:at + 14], 'little', signed=True)
        at += size
    if at != len(data) or rate <= 0 or granule <= 0:
        return None
    return granule / rate, channels, rate


def number(entry, field, fallback, low, high, problems, key):
    if field not in entry:
        return fallback
    value = entry[field]
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not (low <= value <= high):
        problems.append(f'{key}: {field} must be a number in [{low}, {high}]')
        return fallback
    return float(value)


def verify(content):
    """Checks a licensed-assets content root; returns (keys, clips, bytes) or raises Problem with every issue found."""
    keys, schema, max_clips, budget = contract()
    bank = content / 'Resources/EffectsAudio'
    problems = []
    for meta in (content / 'Resources.meta', content / 'Resources/EffectsAudio.meta', bank / 'manifest.json.meta'):
        if not meta.is_file():
            problems.append('missing ' + str(meta.relative_to(content)))
    try:
        manifest = json.loads((bank / 'manifest.json').read_text(encoding='utf-8'))
    except (OSError, ValueError) as error:
        raise Problem(f'manifest: {error}')
    if not isinstance(manifest, dict) or manifest.get('schema') != schema or not isinstance(manifest.get('events'), dict):
        raise Problem(f'manifest: schema must be {schema} with an events object')
    problems += [f'manifest: unknown field {f}' for f in manifest if f not in TOP_FIELDS]
    listed, clips = set(), 0
    for key, entry in manifest['events'].items():
        if key not in keys:
            problems.append(f'{key}: unknown key')
            continue
        space, cap = keys[key]
        if not isinstance(entry, dict):
            problems.append(f'{key}: entry must be an object')
            continue
        problems += [f'{key}: unknown field {f}' for f in entry if f not in FIELDS]
        names = entry.get('clips', [])
        if not isinstance(names, list) or not all(isinstance(n, str) and CLIP_NAME.match(n) for n in names) or len(set(names)) != len(names) or len(names) > max_clips:
            problems.append(f'{key}: clips must be up to {max_clips} unique names of [a-z0-9_-]')
            continue
        if entry.get('space', space) != space:
            problems.append(f'{key}: space must be {space}')
        if entry.get('priority', 'normal') not in ('high', 'normal', 'low'):
            problems.append(f'{key}: priority must be high, normal or low')
        for field, low, high in (('gain', 0, 4), ('rate', .25, 4), ('jitter', 0, .25), ('cooldown', 0, 10)):
            number(entry, field, 0, low, high, problems, key)
        longest = number(entry, 'maxLength', cap, .01, cap + .001, problems, key)
        if names and not (bank / (key + '.meta')).is_file():
            problems.append(f'{key}: missing folder meta')
        for name in names:
            clip = bank / key / (name + '.ogg')
            listed.update({clip, clip.with_name(clip.name + '.meta')})
            if not clip.is_file() or not clip.with_name(clip.name + '.meta').is_file():
                problems.append(f'{key}/{name}.ogg: missing clip or .meta')
                continue
            info = ogg_info(clip.read_bytes())
            if info is None:
                problems.append(f'{key}/{name}.ogg: not Ogg Vorbis')
                continue
            seconds, channels, rate = info
            if seconds > longest + .02:
                problems.append(f'{key}/{name}.ogg: {seconds:.3f} s over its {longest} s cap')
            if rate != 44100 or channels > (2 if space == '2d' else 1):
                problems.append(f'{key}/{name}.ogg: {channels} ch {rate} Hz (44.1 kHz, mono when positional)')
            clips += 1
    allowed = listed | {bank / 'manifest.json', bank / 'manifest.json.meta'} | {bank / (k + '.meta') for k in manifest['events']}
    total, guids = 0, set()
    for path in sorted(bank.rglob('*')):
        if path.is_symlink():
            problems.append('link not allowed: ' + str(path.relative_to(content)))
            continue
        if path.is_dir():
            if path.name not in manifest['events'] or path.parent != bank:
                problems.append('unexpected folder ' + str(path.relative_to(content)))
            continue
        total += path.stat().st_size
        if path not in allowed:
            problems.append('unlisted file ' + str(path.relative_to(content)))
        if path.suffix == '.meta':
            match = GUID.search(path.read_text(encoding='utf-8', errors='replace'))
            if not match or match[1] in guids:
                problems.append('invalid or duplicate guid ' + str(path.relative_to(content)))
            else:
                guids.add(match[1])
    if total >= budget:
        problems.append(f'EffectsAudio is {total} bytes, over the {budget} byte budget')
    if problems:
        raise Problem('\n'.join(problems))
    return len(manifest['events']), clips, total


def sha256(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for block in iter(lambda: f.read(1 << 20), b''):
            h.update(block)
    return h.hexdigest()


def verify_visual_body(content):
    """Checks the optional visual body; returns {path: sha256} (empty without one) or raises Problem with every issue found."""
    listing = content / VISUAL_BODY_MANIFEST
    if not listing.is_file():
        return {}
    try:
        manifest = json.loads(listing.read_text(encoding='utf-8'))
    except (OSError, ValueError) as error:
        raise Problem(f'visual body: {error}')
    if not isinstance(manifest, dict) or manifest.get('schema') != 1 or not isinstance(manifest.get('files'), dict) or not isinstance(manifest.get('prefab'), str):
        raise Problem('visual body: schema must be 1 with a prefab and a files object')
    problems = [f'visual body: unknown field {f}' for f in manifest if f not in ('schema', 'prefab', 'files', 'generated', 'note')]
    files, prefab = manifest['files'], manifest['prefab']
    paths = set()
    for rel, digest in files.items():
        parts = PurePosixPath(rel).parts if isinstance(rel, str) else ()
        if not parts or '\\' in rel or rel.startswith('/') or ':' in rel or '..' in parts or '.' in parts:
            problems.append(f'visual body: unsafe path {rel!r}')
            continue
        if parts[0] == 'Resources':
            problems.append(f'visual body: {rel} is in the effects bank\'s Resources folder')
            continue
        if PurePosixPath(rel).suffix.lower() not in VISUAL_BODY_KINDS:
            problems.append(f'visual body: {rel} is not a mesh, material, texture, prefab, animator controller or .meta')
            continue
        if not isinstance(digest, str) or not SHA256.match(digest):
            problems.append(f'visual body: {rel}: bad sha256')
            continue
        paths.add(rel)
    if PurePosixPath(prefab).name != VISUAL_BODY_PREFAB or 'Resources' not in PurePosixPath(prefab).parts[:-1] or prefab not in paths:
        problems.append(f'visual body: the prefab must be a listed {VISUAL_BODY_PREFAB} in a Resources folder')
    total, guids = 0, set()
    for rel in sorted(paths):
        path = content.joinpath(*PurePosixPath(rel).parts)
        if path.is_symlink() or not path.is_file():
            problems.append(f'visual body: missing {rel}')
            continue
        total += path.stat().st_size
        if sha256(path) != files[rel]:
            problems.append(f'visual body: {rel} does not match its sha256')
        if rel.endswith('.meta'):
            match = GUID.search(path.read_text(encoding='utf-8', errors='replace'))
            if not match or match[1] in guids:
                problems.append(f'visual body: invalid or duplicate guid in {rel}')
            else:
                guids.add(match[1])
        else:
            if rel + '.meta' not in paths:
                problems.append(f'visual body: {rel} has no listed .meta')
        parents = PurePosixPath(rel).parents
        for folder in list(parents)[:-1]:
            if folder.as_posix() + '.meta' not in paths:
                problems.append(f'visual body: folder {folder.as_posix()} has no listed .meta')
    if total > VISUAL_BODY_BUDGET:
        problems.append(f'visual body is {total} bytes, over the {VISUAL_BODY_BUDGET} byte budget')
    if problems:
        raise Problem('\n'.join(sorted(set(problems))))
    return {rel: files[rel] for rel in paths}


def install_visual_body(content, project, files):
    """Copies the visual body's files over Assets/LocalAssets at their paths; deletes nothing."""
    local = project / 'Assets/LocalAssets'
    for rel in sorted(files):
        source = content.joinpath(*PurePosixPath(rel).parts)
        target = local.joinpath(*PurePosixPath(rel).parts)
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.is_file() and sha256(target) == files[rel]:
            continue
        staged = target.with_name(target.name + '.installing')
        shutil.copyfile(source, staged)
        os.replace(staged, target)
    wrong = [rel for rel in sorted(files) if sha256(local.joinpath(*PurePosixPath(rel).parts)) != files[rel]]
    if wrong:
        raise Problem('visual body: installed copies differ: ' + ', '.join(wrong[:5]))


def safe_extract(archive, destination):
    """Extracts a zip member by member, refusing absolute or parent paths, links and oversized content."""
    with zipfile.ZipFile(archive) as z:
        members = [m for m in z.infolist() if not m.is_dir()]
        if len(members) > MAX_ZIP_FILES or sum(m.file_size for m in members) > MAX_ZIP_BYTES:
            raise Problem('zip is too large')
        for m in members:
            name = m.filename
            parts = PurePosixPath(name).parts
            if '\\' in name or name.startswith('/') or ':' in name or '..' in parts or not parts:
                raise Problem('unsafe zip path: ' + name)
            if stat.S_ISLNK(m.external_attr >> 16):
                raise Problem('zip holds a link: ' + name)
            if parts[0] in ('__MACOSX', '.git') or m.file_size > MAX_FILE_BYTES:
                if m.file_size > MAX_FILE_BYTES:
                    raise Problem('zip member too large: ' + name)
                continue
            target = destination.joinpath(*parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            with z.open(m) as source:
                data = source.read(MAX_FILE_BYTES + 1)
            if len(data) > MAX_FILE_BYTES:
                raise Problem('zip member too large: ' + name)
            target.write_bytes(data)


def content_root(base):
    if (base / 'Resources/EffectsAudio/manifest.json').is_file():
        return base
    tops = [p for p in base.iterdir() if p.is_dir() and p.name not in ('.git', '__MACOSX')]
    if len(tops) == 1 and (tops[0] / 'Resources/EffectsAudio/manifest.json').is_file():
        return tops[0]
    raise Problem('no Resources/EffectsAudio/manifest.json in ' + str(base))


def folder_meta(seed):
    guid = hashlib.md5(seed.encode()).hexdigest()
    return f'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'


def install(content, project):
    local = project / 'Assets/LocalAssets'
    resources = local / 'Resources'
    resources.mkdir(parents=True, exist_ok=True)
    if not (project / 'Assets/LocalAssets.meta').is_file():
        (project / 'Assets/LocalAssets.meta').write_text(folder_meta('ChipArena Assets/LocalAssets'))
    if not (local / 'Resources.meta').is_file():
        shutil.copyfile(content / 'Resources.meta', local / 'Resources.meta')
    staging = Path(tempfile.mkdtemp(prefix='.effects-audio-', dir=project))
    try:
        shutil.copytree(content / 'Resources/EffectsAudio', staging / 'EffectsAudio', copy_function=shutil.copyfile)
        shutil.copyfile(content / 'Resources/EffectsAudio.meta', staging / 'EffectsAudio.meta')
        remove(project)
        os.replace(staging / 'EffectsAudio', resources / 'EffectsAudio')
        os.replace(staging / 'EffectsAudio.meta', resources / 'EffectsAudio.meta')
    finally:
        shutil.rmtree(staging, ignore_errors=True)


def remove(project):
    resources = project / 'Assets/LocalAssets/Resources'
    shutil.rmtree(resources / 'EffectsAudio', ignore_errors=True)
    (resources / 'EffectsAudio.meta').unlink(missing_ok=True)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    parser.add_argument('source', nargs='?', help='licensed-assets folder or .zip')
    parser.add_argument('--project', default=str(ROOT), help='Unity project root (default: this project)')
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--check', action='store_true', help='verify the source only')
    mode.add_argument('--remove', action='store_true', help='delete the installed EffectsAudio folder')
    args = parser.parse_args(argv)
    project = Path(args.project).resolve()
    if not (project / 'Assets').is_dir():
        print('not a Unity project: ' + str(project), file=sys.stderr)
        return 1
    if project == ROOT and '/Assets/LocalAssets/' not in (ROOT / '.gitignore').read_text():
        print('Assets/LocalAssets must stay git-ignored', file=sys.stderr)
        return 1
    if args.remove:
        remove(project)
        print('Removed Assets/LocalAssets/Resources/EffectsAudio; the game uses its built-in sounds.')
        return 0
    if not args.source:
        parser.error('SOURCE is required')
    source = Path(args.source).resolve()
    with tempfile.TemporaryDirectory(prefix='licensed-assets-') as scratch:
        try:
            if source.is_file() and zipfile.is_zipfile(source):
                safe_extract(source, Path(scratch))
                content = content_root(Path(scratch))
            elif source.is_dir():
                content = content_root(source)
            else:
                raise Problem('SOURCE must be a folder or a .zip: ' + str(source))
            keys, clips, total = verify(content)
            body = verify_visual_body(content)
            body_bytes = sum(content.joinpath(*PurePosixPath(rel).parts).stat().st_size for rel in body)
            if not args.check:
                install(content, project)
                verify(project / 'Assets/LocalAssets')
                install_visual_body(content, project, body)
        except Problem as problem:
            print('Licensed assets rejected:\n' + str(problem), file=sys.stderr)
            return 1
    action = 'Verified' if args.check else 'Installed'
    print(f'{action} {keys} keys, {clips} clips, {total / 1e6:.2f} MB of licensed effects audio' + ('' if args.check else ' into Assets/LocalAssets/Resources/EffectsAudio (git-ignored).'))
    if body:
        print(f'{action} the visual body: {len(body)} files, {body_bytes / 1e6:.2f} MB' + ('' if args.check else ' into Assets/LocalAssets (git-ignored).'))
    else:
        print('No visual body in the source; the pilots draw the voxel body.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
