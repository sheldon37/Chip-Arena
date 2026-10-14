#!/usr/bin/env python3
"""Import the frozen Expedition drafts with deterministic, supported navigation.

Run from any directory. --check verifies generated resources without writing.
Draft authoring remains in docs/arenas/scripts/expedition_layout.py.
"""
import argparse
import copy
import hashlib
import json
import math
from pathlib import Path
import sys

PROJECT = Path(__file__).resolve().parents[1]
ROOT = PROJECT.parents[1]
sys.path.insert(0, str(ROOT / 'docs/arenas/scripts'))
from expedition_common import Collider, block_hash


def navigation(m):
    off = set(m.get('navHints', {}).get('collisionOff', []))
    col = Collider([b for b in m['blocks'] if b['id'] not in off],
                   m.get('ramps', []), m['bounds'], cell=1)
    nodes, cells, links = [], {}, []
    prefix = "ec" if m["id"] == "expedition-city" else "ew"
    scale = 2 if m['id'] == 'expedition-ward-zero' else 1
    for x in range(math.ceil(m['bounds']['minX']*scale), math.floor(m['bounds']['maxX']*scale)):
        for z in range(math.ceil(m['bounds']['minZ']*scale), math.floor(m['bounds']['maxZ']*scale)):
            px, pz = (x+.5)/scale, (z+.5)/scale
            for y in col.surfaces(px, pz):
                if y < -.01 or y > m['bounds']['ceiling'] - 1 or col.blocked(px, pz, y):
                    continue
                # The city has one street level; building roofs are excluded.
                if m['id'] == 'expedition-city' and y > .05:
                    continue
                n = dict(id=f"{prefix}-n{len(nodes)}",
                         pos=dict(x=px, y=round(y+.9, 4), z=pz), layer=int(y >= 5.99), safe=False)
                nodes.append(n)
                cells.setdefault((x, z), []).append(n)

    def supported(a, b):
        p, q = a['pos'], b['pos']
        if abs(p['y']-q['y']) > .6:
            return False
        for i in range(9):
            t = i/8
            x, y, z = (p[k]+(q[k]-p[k])*t for k in 'xyz')
            if not any(abs(top-(y-.9)) < .18 and not col.blocked(x, z, top)
                       for top in col.surfaces(x, z)):
                return False
        return True

    def link(a, b, kind, identifier=None):
        links.append(dict(id=identifier or f'{prefix}-l{len(links)}', a=a['id'], b=b['id'],
                          kind=kind, requires=[], after=[], hazards=[]))

    for (x, z), rows in cells.items():
        for a in rows:
            for dx, dz in ((1, 0), (0, 1)):
                for b in cells.get((x+dx, z+dz), []):
                    if supported(a, b):
                        link(a, b, 'stair' if abs(a['pos']['y']-b['pos']['y']) > .05 else 'walk')

    def nearest(p):
        eligible = [n for n in nodes if abs(n['pos']['y']-.9-p['y']) < .6]
        if not eligible:
            raise ValueError('No navigation floor for '+str(p))
        n = min(eligible, key=lambda n: sum((n['pos'][k]-p[k]-(.9 if k == 'y' else 0))**2 for k in 'xyz'))
        if math.hypot(n['pos']['x']-p['x'], n['pos']['z']-p['z']) > 2:
            raise ValueError('Navigation hint too far from floor: '+str(p))
        return n

    for drop in m.get('navHints', {}).get('drops', []):
        link(nearest(drop['a']), nearest(drop['b']), 'drop', drop['id'])
    for safe in m.get('navHints', {}).get('safe', []):
        nearest(safe['at'])['safe'] = True
    if not nodes or not links:
        raise ValueError('Empty navigation: '+m['id'])
    return dict(nodes=nodes, links=links)


def generate(check=False):
    resource = PROJECT / 'Assets/Conversion/Resources'
    manifest = json.loads((PROJECT/'Tests/expedition-layout-draft.json').read_text())
    maps = []
    for name in ('city', 'ward-zero'):
        m = copy.deepcopy(json.loads((PROJECT/f'Tests/expedition-{name}-draft.json').read_text())['maps'][0])
        area = next(a for a in manifest['areas'] if a['map'] == m['id'])
        if block_hash(m) != area['blockHash']:
            raise ValueError('Draft blockHash mismatch: '+m['id'])
        m['nav'] = navigation(m)
        maps.append(m)
        print(f"{m['id']}: {len(m['nav']['nodes'])} nodes, {len(m['nav']['links'])} links")
    content = json.loads((resource/'UnityMaps.json').read_text())
    content['maps'] = [m for m in content['maps'] if m['id'] not in {x['id'] for x in maps}]
    at = next(i for i, m in enumerate(content['maps']) if m['id'] == 'siren')+1
    content['maps'][at:at] = maps
    outputs = {resource/'UnityMaps.json': json.dumps(content, separators=(',', ':'))+'\n',
               resource/'ExpeditionLayout.json': (PROJECT/'Tests/expedition-layout-draft.json').read_text()}
    lockpath = PROJECT/'tools/asset-lock.json'
    lock = json.loads(lockpath.read_text())
    for name in ('UnityMaps.json', 'ExpeditionLayout.json', 'PveContent.json'):
        path = resource/name
        raw = outputs[path].encode() if path in outputs else path.read_bytes()
        rel = 'Assets/Conversion/Resources/'+name
        entry = dict(path=rel, bytes=len(raw), gitBlob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest(),
                     source='Generated by tools/generate_expedition.py; preserves other maps')
        index = next((i for i, a in enumerate(lock['assets']) if a['path'] == rel), None)
        if index is None:
            lock['assets'].append(entry)
        else:
            lock['assets'][index] = entry
    outputs[lockpath] = json.dumps(lock, indent=2)+'\n'
    for path, text in outputs.items():
        if check:
            if not path.exists() or path.read_text() != text:
                raise ValueError('Generated file differs: '+str(path))
        else:
            path.write_text(text)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    generate(parser.parse_args().check)
