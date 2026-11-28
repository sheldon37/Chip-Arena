"""Freeze the repository files outside the Unity project that the look checks compare against into Tests/look-references.json.

The native CI checks out only unity/ChipArenaConversion (sparse) and the source package holds only the Unity project, so
the managed and python tests read this fixture instead of the repository around the project:
  - draft: the Brickfall draft blockout (docs/arenas/scripts/brickfall_m1.py, ARENA_PROPOSAL.md 3.3): every element the
    script lists (name, x0, x1, z0, z1, top, paint char; mirrors included, as the script appends them) and its painted-only
    areas (x0, x1, z0, z1, char; '/' marks a loading-dock ramp). Read by Tests/test_brickfall_scraps.py (scrap runs clear the
    cover) and Tests/BlockLookChecks.cs (every element resolves to a Brickfall look, every pier is banded and sized as its
    member). The script's blob is recorded, not pinned: the draft is a living design file.
  - electron: the Electron sky and moon keys (src/client/night-world.ts, src/client/map-scenery.ts at the source lock's
    commit, blobs checked against tools/source-lock.json): the sealed theme with no sky, the cold themes, the volcanic key
    and value and the horizon/zenith colours, and the moon colour by map id, each with the statement it was read from.
    Read by Tests/MapLookChecks.cs.
Run it from a full checkout when the draft changes (then re-check the scrap runs and the Brickfall looks); --check compares
a fresh export with the committed fixture and exits non-zero on a difference.

usage: python3 tools/export_look_references.py [repository checkout (default: the one around this project)] [--check]
"""
import contextlib
import hashlib
import io
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Tests/look-references.json'
DRAFT = 'docs/arenas/scripts/brickfall_m1.py'
WORLD, SCENERY = 'src/client/night-world.ts', 'src/client/map-scenery.ts'


def blob(data):
    return hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()


def draft(repo):
    path = repo / DRAFT
    data = path.read_bytes()
    # The script's definitions only (its report sits under __main__); compiled from text, so no bytecode cache lands in docs/.
    ns = {'__name__': 'brickfall_m1_draft', '__file__': str(path)}
    with contextlib.redirect_stdout(io.StringIO()):
        exec(compile(data.decode('utf-8'), str(path), 'exec'), ns)
    elements = [[e[0], e[1], e[2], e[3], e[4], e[5], e[6]] for e in ns['els']]
    paint = [[p[0], p[1], p[2], p[3], p[4]] for p in ns['paint_only']]
    if len(elements) < 100 or not any('-pier-' in e[0] for e in elements):
        raise SystemExit(DRAFT + ' no longer lists its elements and piers')
    return {'path': DRAFT, 'blob': blob(data), 'elements': elements, 'paint': paint}


def electron(repo):
    lock = json.loads((ROOT / 'tools/source-lock.json').read_text(encoding='utf-8'))
    pinned = {f['path']: f['git_blob_sha'] for f in lock['files']}
    text = {}
    for rel in (WORLD, SCENERY):
        data = (repo / rel).read_bytes()
        if blob(data) != pinned[rel]:
            raise SystemExit('Source blob mismatch (tools/source-lock.json): ' + rel)
        text[rel] = data.decode('utf-8')
    world, scenery = text[WORLD], text[SCENERY]

    def find(pattern, source, what):
        m = re.search(pattern, source)
        if not m:
            raise SystemExit(what + ' not found')
        return m
    sealed = find(r"if\(m\.theme==='([a-z]+)'\)return;", world, 'the sealed theme')
    sky = find(r"const cold=\[([^\]]*)\]\.includes\(m\.theme\),volcanic=m\.(theme|id)==='([a-z]+)',[^;]*?"
               r"horizon=new T\.Color\(volcanic\?(0x[0-9a-f]{6}):cold\?(0x[0-9a-f]{6}):(0x[0-9a-f]{6})\),"
               r"zenith=new T\.Color\(volcanic\?(0x[0-9a-f]{6}):(0x[0-9a-f]{6})\);", world, 'the sky colours')
    moon = find(r"moon\?\.color\.set\(((?:id==='[a-z-]+'\?0x[0-9a-f]{6}:)+)(0x[0-9a-f]{6})\)", scenery, 'the moon colours')
    return {'commit': lock['commit'], 'files': {WORLD: pinned[WORLD], SCENERY: pinned[SCENERY]},
            'sky': {'statement': sky.group(0), 'sealedTheme': sealed.group(1), 'cold': re.findall(r"'([a-z]+)'", sky.group(1)),
                    'volcanicKey': sky.group(2), 'volcanic': sky.group(3),
                    'horizon': {'volcanic': int(sky.group(4), 16), 'cold': int(sky.group(5), 16), 'other': int(sky.group(6), 16)},
                    'zenith': {'volcanic': int(sky.group(7), 16), 'other': int(sky.group(8), 16)}},
            'moon': {'statement': moon.group(0), 'key': 'id', 'byId': {k: int(v, 16) for k, v in re.findall(r"id==='([a-z-]+)'\?(0x[0-9a-f]{6})", moon.group(1))},
                     'other': int(moon.group(2), 16)}}


def dump(value, depth=0):
    # One row per element; scalars and short lists inline.
    if isinstance(value, dict):
        pad = ' ' * (depth + 1)
        return '{\n' + ',\n'.join(pad + json.dumps(k) + ': ' + dump(v, depth + 1) for k, v in value.items()) + '\n' + ' ' * depth + '}'
    if isinstance(value, list) and value and isinstance(value[0], list):
        pad = ' ' * (depth + 1)
        return '[\n' + ',\n'.join(pad + json.dumps(v, separators=(',', ':')) for v in value) + '\n' + ' ' * depth + ']'
    return json.dumps(value, separators=(',', ':'))


def main(argv):
    check = '--check' in argv
    args = [a for a in argv if a != '--check']
    repo = Path(args[0]).resolve() if args else ROOT.parents[1]
    text = dump({'generator': 'tools/export_look_references.py', 'draft': draft(repo), 'electron': electron(repo)}) + '\n'
    if check:
        same = OUT.is_file() and OUT.read_text(encoding='utf-8') == text
        print(('up to date: ' if same else 'differs from a fresh export: ') + OUT.relative_to(ROOT).as_posix())
        return 0 if same else 1
    OUT.write_text(text, encoding='utf-8', newline='\n')
    print('wrote', OUT.relative_to(ROOT).as_posix())
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
