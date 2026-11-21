"""The melee intros' set bank (docs/INTROS-MELEE.md, docs/INTROS-V2.md section 3): builds the seven sets (melee_<set>.py, on
melee_kit.py and the stage kit) at the Fine and Low tiers, bakes their occlusion and colours, and writes the local bank
ChipArenaIntroMelee (never committed).

  blender --background --factory-startup --python tools/intros/melee_set.py -- --out DIR [--sets rain,rift,...] [--tiers 0,2]
          [--no-ao] [--preview DIR] [--stats]

--out is a folder (the bank is written as DIR/ChipArenaIntroMelee.bytes); --preview renders each set's views (Fine tier, workbench).
Prop names (the v2 contract): "<set>/static" and "<set>/<rig>" are the Fine tier, "<set>/static/low" and "<set>/<rig>/low" the Low.
Meshes are in Unity space (the set frame, melee_kit.py); the intro places them in the source frame and the runtime mirrors x.
"""
import argparse
import importlib
import os
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import melee_kit as mk  # noqa: E402
from melee_kit import sk  # noqa: E402

BANK = 'ChipArenaIntroMelee'
SETS = ('rain', 'rift', 'canopy', 'lightwell', 'breach', 'alley', 'observatory')


def args_after_dashes():
    a = sys.argv
    return a[a.index('--') + 1:] if '--' in a else []


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out')
    ap.add_argument('--sets', default=','.join(SETS))
    ap.add_argument('--tiers', default='0,2')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--preview')
    ap.add_argument('--views')
    ap.add_argument('--stats', action='store_true')
    ap.add_argument('--extra', help='another folder holding melee_<set>.py modules (searched first)')
    args = ap.parse_args(args_after_dashes())
    if args.extra:
        sys.path.insert(0, args.extra)
    t0 = time.time()
    sb = sk.StageBank()
    tiers = [int(x) for x in args.tiers.split(',')]
    tally = {}

    def count(name, tier, om):
        if om is None:
            return
        v = tally.setdefault((name, tier), [0, 0])
        v[0] += len(om.positions)
        v[1] += sum(len(x) for x in om.submeshes) // 3
    for name in args.sets.split(','):
        try:
            mod = importlib.import_module('melee_' + name)
        except ImportError:
            if args.sets == ','.join(SETS):
                print('(no set %s yet)' % name)
                continue
            raise
        for ti in tiers:
            T = sk.Tier(ti)
            t = time.time()
            piece = mod.build(T)
            g = piece.static[ti]
            print('%s tier %s: static %d faces, %d rigs, %.1f s' % (name, T.name, g.faces, len(piece.rigs), time.time() - t))
            if args.stats and getattr(piece, 'tally', None) is not None:
                print('  corners: ' + piece.tally.report())
            ao = None
            rig_geos = [(rn, tiers_[ti]) for rn, tiers_ in piece.rigs.items() if ti in tiers_]
            placed = [geo.transformed(piece.rest[rn]) for rn, geo in rig_geos]
            if not args.no_ao:
                outs = sk.bake_ao([g] + placed, distance=(1.4, 1.4, 1.8)[ti], samples=(32, 16, 10)[ti])
                ao, rig_ao = outs[0], outs[1:]
                ao = 1 - (1 - ao) * .85
            else:
                rig_ao = [None] * len(placed)
            bk = sk.Bake(g, ao, floors=(0.0,), seed=ti)
            suffix = '' if ti == 0 else '/low' if ti == 2 else '/mid'
            om = sk.to_mesh('%s/static%s' % (name, suffix), g, bk)
            sb.add('%s/static%s' % (name, suffix), 'static', om)
            count(name, ti, om)
            for (rn, geo), wgeo, rao in zip(rig_geos, placed, rig_ao):
                rbk = sk.Bake(geo, rao, world=np.frombuffer(wgeo.p, dtype=np.float32).reshape(-1, 3), floors=(0.0,), seed=ti)
                rom = sk.to_mesh('%s/%s%s' % (name, rn, suffix), geo, rbk)
                sb.add('%s/%s%s' % (name, rn, suffix), 'rig', rom)
                count(name, ti, rom)
            if args.preview and ti == 0:
                vs = piece.views
                if args.views:
                    vs = [v for v in vs if v[0] in args.views.split(',')]
                shown = [(g, bk)] + [(w, sk.Bake(w, a)) for w, a in zip(placed, rig_ao)]
                sk.preview(shown, vs, os.path.join(args.preview, name))
    if not args.out:
        for (nm, ti), (verts, tris) in sorted(tally.items()):
            print('set %-12s tier %d: %7d vertices %7d triangles (about %.2f MB)' % (nm, ti, verts, tris, (verts * 48 + tris * 6) / 1e6))
    if args.out:
        os.makedirs(args.out, exist_ok=True)
        sb.layer()
        data = sb.bank.write()
        path = os.path.join(args.out, BANK + '.bytes')
        with open(path, 'wb') as fh:
            fh.write(data)
        for (nm, ti), (verts, tris) in sorted(tally.items()):
            print('set %-12s tier %d: %7d vertices %7d triangles (about %.2f MB)' % (nm, ti, verts, tris, (verts * 48 + tris * 6) / 1e6))
        print('wrote', path, len(data), 'bytes')
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
