"""The five generic intros' sets (docs/INTROS-GENERIC.md; the contract is docs/INTROS-V2.md), modelled in Blender from script on
stagekit (tools/stages/stagekit.py): Cloudbreak's summit relay, Whiteout's polar outpost, Slipstream's maglev station, Waygate's
canyon plaza and Flashbulb's premiere.

  blender --background --factory-startup --python generic_sets.py -- --out DIR [--preview DIR] [--no-ao] [--only SET,...]

Writes DIR/ChipArenaLocal/Resources/ChipArenaIntroGeneric.bytes (tools/pack_bank/bank.py's format, Core NativePackBank; never
committed: the integrator packs it into the Drive zip). Every piece is built twice: "<set>/<piece>" (Fine) and "<set>/<piece>/low"
(Low), group "intro", in Unity space (metres, +y up). A set's static pieces are in the set's frame: the pilot's hero mark at the
origin, the pilot facing +z (the Core scripts draw them at the source frame's origin, whose x is Unity's -x). Moving pieces (a door
leaf, a windsock, a ring, a train car) are about their own pivot, named in each set module's notes. Glowing finishes go on the
glow sub-mesh, which the runtime multiplies by the piece's glow. Original work only: every shape is authored in these scripts.
"""
import argparse
import os
import sys
import time

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', 'stages'))

import stagekit as sk  # noqa: E402
import generic_kit as kit  # noqa: E402
import generic_cloudbreak  # noqa: E402

BANK = 'ChipArenaIntroGeneric'
SMOOTH_MAX = .35
SETS = {'cloudbreak': generic_cloudbreak}
for _name in ('whiteout', 'slipstream', 'waygate', 'flashbulb'):
    try:
        SETS[_name] = __import__('generic_' + _name)
    except ImportError:
        pass


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', help='the folder the bank lands under (ChipArenaLocal/Resources/%s.bytes)' % BANK)
    ap.add_argument('--preview', help='a folder for Workbench previews of each set (Fine tier)')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--only')
    args = ap.parse_args(argv)
    keys = list(SETS) if not args.only else args.only.split(',')
    sb = sk.StageBank()
    sb.far_layers = False
    t0 = time.time()
    sizes = {}
    for lod in (0, 1):
        T = kit.tier(lod)
        for key in keys:
            mod = SETS[key]
            pieces = [p for p in mod.build(T) if p.g.faces]
            # No finish glossier than SMOOTH_MAX: the stage shader reflects the arena's own probe, so a glossy intro set would mirror
            # whatever map the match is on (Cyber City's magenta neon on water and ice; I5's finding in the first captures).
            for f in sk.FINS:
                f.smooth = min(f.smooth, SMOOTH_MAX)
            aos = [None] * len(pieces)
            idx = [i for i, p in enumerate(pieces) if p.ao]
            if idx and not args.no_ao:
                baked = sk.bake_ao([pieces[i].g for i in idx], distance=1.1, samples=24 if lod == 0 else 8)
                for i, a in zip(idx, baked):
                    aos[i] = a
            pv = []
            for p, ao in zip(pieces, aos):
                bk = sk.Bake(p.g, ao, floors=p.floors, seed=p.seed or int(kit.hash01(p.name) * 1000))
                if p.recolour:
                    kit.recolour(p.g, bk, p.recolour[0], p.recolour[1])
                name = p.name if lod == 0 else p.name + '/low'
                om = sk.to_mesh(name, p.g, bk)
                if om is None:
                    continue
                if p.places:
                    mi = sb.mesh(om)
                    sb.bank.prop(name, 'intro', [(mi, list(m)) for m in p.places])
                else:
                    sb.add(name, 'intro', om)
                v = len(om.positions)
                tris = sum(len(s) for s in om.submeshes) // 3
                sizes[name] = (v, tris)
                if p.preview is None:
                    pv.append((p.g, bk))
                elif p.preview:
                    pv.extend((p.g.transformed(m), bk) for m in p.preview)
            print('%s %s: %d pieces, %d vertices, %d triangles (%.1f s)' % (key, 'fine' if lod == 0 else 'low', len(pv),
                  sum(sizes[(p.name if lod == 0 else p.name + '/low')][0] for p in pieces if (p.name if lod == 0 else p.name + '/low') in sizes),
                  sum(sizes[(p.name if lod == 0 else p.name + '/low')][1] for p in pieces if (p.name if lod == 0 else p.name + '/low') in sizes),
                  time.time() - t0))
            if args.preview and lod == 0:
                sk.preview(pv, mod.VIEWS, os.path.join(args.preview, key), size=(1280, 720))
    for name in sorted(sizes):
        print('  %-34s %7d v %7d t' % (name, sizes[name][0], sizes[name][1]))
    if args.out:
        sb.write(args.out, BANK)
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
