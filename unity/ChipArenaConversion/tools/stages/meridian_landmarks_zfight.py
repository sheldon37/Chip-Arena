"""zfight.py for Meridian's landmarks (the bank ChipArenaCityLandmarks): every piece Core places in a layout (LAYOUTS.json from the
CoreTests flag --meridian-landmarks-dump), at one tier, with the greybox boxes the landmarks do not replace drawn beside them.
Plain Python with numpy (no Blender):
  python -I meridian_landmarks_zfight.py BANK LAYOUTS.json [--tier 0] [--floors] [--families] [--out REPORT.txt]
--families draws every instance's front on every hall in turn (all five families), not only the dump's deal. The pieces are
StageLit models drawn as they are (no lift), so a model lying on a greybox box or on another piece counts like zfight's 'block'."""
import json
import os
import sys

import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zfight  # noqa: E402

FAMILIES = ('level0', 'poolrooms', 'wardzero', 'station', 'ironworks')


def main():
    a = sys.argv[1:]
    bank_path, layouts_path = a[0], a[1]
    tier = int(a[a.index('--tier') + 1]) if '--tier' in a else 0
    floors = '--floors' in a
    out = a[a.index('--out') + 1] if '--out' in a else None
    eps = .03 if floors else .004
    meshes, props = zfight.read_bank(bank_path)
    by = {p[0]: p for p in props}
    D = json.load(open(layouts_path, encoding='utf-8'))
    lines = []

    def say(*v):
        lines.append(' '.join(str(x) for x in v))
    totals = [0.0, 0.0]
    for L in D['layouts']:
        deals = [None] + (list(FAMILIES) if '--families' in a else [])
        for fam in deals:
            sources = []
            covers = set(L['covers'])
            for p in L['pieces']:
                key = p['key']
                if fam is not None and p['kind'] in ('Front', 'FrontRig'):
                    key = key.replace('/' + key.split('/')[1] + '/', '/' + fam + '/')
                elif fam is not None:
                    continue
                prop = None
                for t in ([str(tier)] + (['0'] if tier != 0 else []) + ['far']):
                    prop = by.get('%s/%s' % (key, t))
                    if prop is not None and prop[2]:
                        break
                if prop is None or not prop[2]:
                    continue
                place = np.array(p['place'], dtype=np.float64).reshape(3, 4)
                tris, lay = zfight.prop_tris(meshes, prop, place)
                sources.append(('piece', key + '@%.0f,%.0f' % (place[0, 3], place[2, 3]), tris, lay))
            if fam is None:
                for b in L['blocks']:
                    if b[0] in covers:
                        continue
                    x0, x1, y0, y1, z0, z1 = b[1:7]
                    if x1 - x0 > 1000:
                        continue
                    sources.append(('dress', b[0], zfight.box_tris(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), ((x1 - x0) / 2, (y1 - y0) / 2, (z1 - z0) / 2), (0, 0, 0, 1)), np.zeros(12)))
            if floors:
                def keep(t):
                    n = np.cross(t[:, 1] - t[:, 0], t[:, 2] - t[:, 0])
                    l = np.linalg.norm(n, axis=1)
                    return (l > 1e-9) & (n[:, 1] > .97 * np.maximum(l, 1e-12))
                sources = [(k, nm, t[keep(t)], ly[keep(t)]) for k, nm, t, ly in sources]
                sources = [x for x in sources if len(x[2])]

            def skip(i, j):
                return sources[i][0] == 'dress' and sources[j][0] == 'dress'
            say('== %s %s tier %d %s' % (L['name'], 'deal ' + fam if fam else 'the dump\'s deal with the greybox', tier, 'floors' if floors else 'all faces'))
            pairs, parted = zfight.measure(sources, eps, 2e-4, skip=skip)
            say('parted by layer: %.2f m2' % parted)
            ex, nr = zfight.report(sources, pairs, 12, say, hist=floors)
            totals[0] += ex
            totals[1] += nr
    say('ALL: exact %.3f m2, near %.3f m2' % tuple(totals))
    text = '\n'.join(lines)
    print(text)
    if out:
        open(out, 'w', encoding='utf-8').write(text + '\n')


if __name__ == '__main__':
    main()
