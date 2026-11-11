"""Cyber City's stage art (docs/arenas/STAGE-ART.md, docs/arenas/CYBERCITY-ART.md): the neon rain street modelled on today's
blocks and kit parts, into its own bank ChipArenaStageCyberCity.

  dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout cybercity LAYOUT.json
  blender --background --factory-startup --python tools/stages/cybercity_art.py -- --layout LAYOUT.json --out <Assets>/LocalAssets
  (--preview DIR [--views a,b] renders the baked Fine tier; --tiers 0,1,2; --no-ao; --static-only; --stats)

The bank:
  static/<chunk>/<tier>     the street, the Cut, the frontage, the podiums, the station, the markets and the yards on their static
                            blocks (cybercity_street.py, cybercity_city.py), with the kit's steady parts (neon, signs, screens)
  static/near-*/<tier>      the backdrop's near ring and the Helix Line's tunnels (cybercity_city.py)
  block/<block id>/<tier>   the breakable and carried blocks (cybercity_props.py), in the block's own frame
  rig/train-nose|mid|tail   the maglev's cars (cybercity_props.py): +x the travel direction, y 0 on the guideway's top, each
                            car's origin at its own front end (Core NativeCyberCityArt.Pose)
  cover/<block id>          every map block

The kit's driven parts (the edge strips, coils, SAFE and EXIT lights, the couplings' rails, lamps and deck plates, the
flickering signs) stay the kit's boxes (Core NativeCyberCityArt.Kept): the art leaves their places clear.
"""
import argparse
import os
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import stagekit as sk  # noqa: E402
import cybercity_kit as ck  # noqa: E402
from cybercity_kit import F, Blk  # noqa: E402

BANK = 'ChipArenaStageCyberCity'
EPS = (.0055, .0065, .0125)   # m: nearer than this two same-facing faces are one plane (stagekit.NEAR_TIER, over the scripts' own offsets)
STATS = {}
COUNT = False


def tally(g, key, mark):
    if COUNT:
        STATS[key] = STATS.get(key, 0) + (len(g.p) // 3 - mark)
    return len(g.p) // 3


class Scene:
    """The layout as the builders read it: the blocks by id, each block's steady and driven kit parts, the exits."""

    def __init__(self, L):
        self.L = L
        self.blocks = [Blk(b) for b in L['blocks']]
        self.by = {b.id: b for b in self.blocks}
        self.steady, self.driven = {}, {}
        for p in L['parts']:
            (self.steady if p['drive'] == 0 else self.driven).setdefault(p['o'], []).append(p)
        self.city = L['city']

    def parts(self, blk):
        return self.steady.get(blk.id, [])

    def tops_below(self, x, z, y):
        """The highest block top at or under y at (x, z), or None."""
        best = None
        for b in self.blocks:
            if b.x0 - 1e-3 <= x <= b.x1 + 1e-3 and b.z0 - 1e-3 <= z <= b.z1 + 1e-3 and b.y1 <= y + 1e-3 and (best is None or b.y1 > best):
                best = b.y1
        return best


def static_scene(L, T):
    import cybercity_street as street
    import cybercity_city as city
    S = Scene(L)
    g = sk.Geo()
    m = 0
    for blk in S.blocks:
        if blk.role != 'static':
            continue
        fam = street.build(g, S, blk, T) or city.build(g, S, blk, T)
        if not fam:
            city.fallback(g, S, blk, T)
            fam = 'other'
        m = tally(g, fam, m)
    city.backdrop(g, S, T)
    tally(g, 'backdrop', m)
    return g, S


def views():
    return [('plaza', (-14, 2.2, -9), (2, 4.5, 3), 70), ('cut', (-30, -2.3, .6), (0, -2.6, 0), 70), ('portal', (22, -2.2, 1.2), (39, -.5, 0), 65),
            ('nova', (-3, 1.8, 13), (15, 6, 15.5), 70), ('market', (-20, 1.9, 9), (-30, 2.2, 18), 70), ('billboard', (-3, 1.8, 15), (-3, 10, 29), 70),
            ('rank', (22, 1.8, 24), (33, 2, 15), 70), ('station', (-7, 8.6, 3.2), (6, 8, -2), 70), ('avenue', (-36, 1.8, 26), (10, 3, 25), 70),
            ('coupling', (-18, 2.0, 6.5), (-24, 1, 0), 65), ('roof', (8, 12.6, 12), (-20, 8, -25), 70), ('overview', (0, 30, -44), (0, 0, 0), 60),
            ('frontage', (0, 1.8, 14), (-14, 5, 30), 70), ('frontage-close', (-20, 1.8, 26.5), (-26, 2.5, 30), 60), ('wall', (-20, -2.4, -1.5), (-14, -2, 3), 65),
            ('pit', (-25, 1.7, 8.5), (-33, -3, 5), 65), ('nova-in', (9, 1.7, 12), (20, 2, 18), 75), ('podium', (-2, 5, 5), (14, 6, 12), 60),
            ('stall', (-30, 1.7, 14.5), (-33.4, .8, 12.6), 55), ('van', (-22, 1.7, 27.5), (-28, 1.2, 24.2), 55), ('cab', (27, 1.7, 23), (29.75, .8, 26.6), 50),
            ('grid', (-19.5, 1.7, 5.5), (-25, 1.6, 3), 50), ('lift', (-5, 8.4, 3), (0, 8.4, 0), 60), ('high', (-30, 22, 28), (10, 0, -10), 65),
            ('train', (-4, 1.6, 5.5), (-20, -2.2, 0), 70), ('near', (0, 12, 0), (30, 20, 40), 70), ('tunnel', (30, -2.4, 0), (60, -2.4, 0), 60)]


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--layout', required=True)
    ap.add_argument('--out', help='Assets/LocalAssets to write the bank into')
    ap.add_argument('--preview', help='render views of the baked Fine tier')
    ap.add_argument('--views', help='only these views (comma separated)')
    ap.add_argument('--tiers', default='0,1,2')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--static-only', action='store_true')
    ap.add_argument('--own-mid', action='store_true', help='a Mid tier of its own for the static scenery (default: the Fine meshes again)')
    ap.add_argument('--stats', action='store_true', help='print where the vertices of each tier go')
    args = ap.parse_args(argv)
    L = sk.load_layout(args.layout)
    global COUNT
    COUNT = args.stats
    ck.finishes()
    import cybercity_props as props
    import cybercity_city as city
    sb = sk.StageBank()
    # the block models are the stage's own building: layered where they stand, with the chunks and with one another
    for b in L['blocks']:
        sb.at(b['id'], b['pos'], b['rot'])
    chunks = sk.Chunks(16.0, (-40.0, -32.0), (-.6, 6.0, 16.5))
    floors = (-4.0, 0.0, 3.6, 7.2, 10.8)
    tiers = [int(x) for x in args.tiers.split(',')]
    t0 = time.time()
    fine = {}
    for ti in tiers:
        T = sk.Tier(ti)
        if ti == 1 and not args.own_mid and 0 in tiers:
            # the Mid props share the Fine meshes: nothing is stored twice
            for name, mi in fine.items():
                sb.share(name[:-1] + '1', 'static', mi)
            if not args.static_only:
                props.bank_props(sb, L, T, floors, args.no_ao)
            continue
        t = time.time()
        g, S = static_scene(L, T)
        # the Fine meshes also draw as the Mid tier (out to 38 m): settled for that range. Faces closer than EPS count as one
        # plane (the scripts' own small offsets among them) and are stacked the tier's lift apart
        lt = 1 if (ti == 0 and not args.own_mid) else ti
        print('  coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % sk.lift_coplanar(g, eps=EPS[lt], lift=sk.LIFT_TIER[lt], passes=4))
        far = city.far_scene(S, T)
        print('  far coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % sk.lift_coplanar(far, eps=.02, lift=.05))
        scene = props.rest_scene(L, T, train=False)
        print('tier %s: static %d faces, far %d, %.1f s' % (T.name, g.faces, far.faces, time.time() - t))
        if args.stats:
            print('  ' + ', '.join('%s %d' % kv for kv in sorted(STATS.items(), key=lambda kv: -kv[1]) if kv[1] > 0))
        STATS.clear()
        ao = aof = None
        if not args.no_ao:
            ao, aof = sk.bake_ao([g, far], distance=(1.6, 1.6, 2.0)[ti], samples=(32, 16, 12)[ti], occluders=[scene])
            # an open-air street at night: the occlusion eased, so the walls and floors keep their light
            ao = 1 - (1 - ao) * .8
            aof = 1 - (1 - aof) * .6
        bk = sk.Bake(g, ao, soot=None, floors=floors)
        city.wet(g, bk)
        ids = chunks.ids(g)
        for cid in np.unique(ids):
            name = 'static/%d/%d' % (cid, ti)
            om = sk.to_mesh(name, g, bk, ids == cid)
            sb.add(name, 'static', om)
            if ti == 0 and om is not None:
                fine[name] = sb.bank._mesh[name]
        # the near ring and the tunnels: named chunks by side
        bkf = sk.Bake(far, aof, soot=None, floors=(0.0,))
        fid = city.far_ids(far)
        for cid in np.unique(fid):
            name = 'static/near-%s/%d' % (city.FAR_NAMES[cid], ti)
            om = sk.to_mesh(name, far, bkf, fid == cid)
            sb.add(name, 'static', om)
            if ti == 0 and om is not None:
                fine[name] = sb.bank._mesh[name]
        if args.preview and ti == 0:
            vs = views()
            if args.views:
                vs = [v for v in vs if v[0] in args.views.split(',')]
            shown = props.rest_scene(L, T, train=any(v[0] == 'train' for v in vs))
            sk.preview([(g, bk), (far, bkf), (shown, sk.Bake(shown, None))], vs, args.preview)
        if not args.static_only:
            props.bank_props(sb, L, T, floors, args.no_ao)
        del g
    if args.out:
        for b in L['blocks']:
            sb.cover(b['id'])
        sb.write(args.out, BANK)
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
