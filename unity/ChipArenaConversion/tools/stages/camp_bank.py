"""Belvedere Camp's bank: ChipArenaCamp (Core NativeCamp places it; Runtime NativeCampDraw draws it; docs/expedition/BELVEDERE-CAMP.md).

Run inside Blender 5.x (bpy, numpy), headless:

  blender --background --factory-startup --python tools/stages/camp_bank.py -- \
      --plan PLAN.json --out OUT_DIR [--no-ao] [--only PREFIX,...] [--preview DIR]

PLAN.json is what the CoreTests flag `--camp-dump PLAN.json` writes: the live camp's pieces with their places, envelopes and bases,
the strings, bulbs and sails, the hub plan table, the survivors' posts, the preview's pieces and the five plans' maps (for the war
board). The bank goes to OUT_DIR/ChipArenaLocal/Resources/ChipArenaCamp.bytes (keep it out of the repository; the integrator carries
it in the Drive zip).

Bank names (the runtime reads them): <key>/0 the Fine tier, <key>/2 the Low tier (only where the Fine mesh is big), group "camp".
Keys: camp/lamp/fence, camp/mast/{sail,corner,gate,gate-pa}, camp/fire/drum, camp/board/{war-0..4,war-lamp,missing,cross,poster-*},
camp/box/junction, camp/notice/*, camp/sign/{north,gate-E,gate-S,gate-W}, camp/static/{strands,sails,cables} (world space),
camp/row/* and camp/glass/* (the preview's structures and their see-through panes), camp/figure/<pose>/<0..2>.

The art rules, checked here on the built meshes (Tests/CampChecks.cs checks the same with CHIPARENA_CAMP_BANK): every fence piece's
mesh inside its envelope, and below the gates' track (3.4 m) inside its base, which Core holds within 0.25 m of the fence and out of
the gates' leaf pockets; the strings and sails from 3.5 m; the cables flat on the fence. Presentation only.
"""
import json
import math
import os
import sys
import time
import zlib

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
os.environ.setdefault('CHIP_STAGES', HERE)
BANK = 'ChipArenaCamp'


def _args():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    a = {'plan': None, 'out': None, 'ao': True, 'only': None, 'preview': None}
    i = 0
    while i < len(argv):
        k = argv[i]
        if k == '--no-ao':
            a['ao'] = False
        else:
            a[k.lstrip('-')] = argv[i + 1]
            i += 1
        i += 1
    return a


ARGS = _args()

import numpy as np  # noqa: E402
import stagekit as sk  # noqa: E402
import meridian_gates_kit as mk  # noqa: E402
import camp_kit as ck  # noqa: E402
import camp_parts as cp  # noqa: E402

LOW_FROM = 1500


def registry(plan):
    """key -> (builder(T), AO reach (0: none))."""
    reg = {
        'camp/lamp/fence': (cp.fence_lamp, 0), 'camp/mast/sail': (cp.sail_mast, 0), 'camp/mast/corner': (cp.corner_mast, 0),
        'camp/mast/gate': (lambda T: cp.gate_mast(T, False), 0), 'camp/mast/gate-pa': (lambda T: cp.gate_mast(T, True), 0),
        'camp/fire/drum': (cp.fire_drum, .5), 'camp/board/war-lamp': (cp.war_lamp, 0), 'camp/board/missing': (cp.missing_board, 0),
        'camp/board/cross': (cp.cross, 0), 'camp/box/junction': (cp.junction, 0),
        'camp/static/strands': (lambda T: cp.strands(T, plan), 0), 'camp/static/sails': (lambda T: cp.sails(T, plan), 0),
        'camp/static/cables': (lambda T: cp.cables(T, plan), 0),
        'camp/row/ring': (cp.ring, 1.2), 'camp/row/fountain': (cp.fountain, .8), 'camp/row/bench': (cp.bench, .4),
        'camp/row/bench-waiting': (cp.bench_waiting, .4), 'camp/row/kiosk': (cp.kiosk, 1.0), 'camp/row/infirmary': (cp.infirmary, 1.0),
        'camp/row/lookout': (cp.lookout, 1.2), 'camp/row/sentry': (cp.sentry, .8), 'camp/row/radio': (cp.radio, 1.0),
        'camp/row/supply': (cp.supply, 1.0), 'camp/row/generator': (cp.generator, .5), 'camp/row/sandbag': (cp.sandbag, .4),
        'camp/glass/infirmary': (cp.glass_infirmary, 0), 'camp/glass/lookout': (cp.glass_lookout, 0), 'camp/glass/radio': (cp.glass_radio, 0),
        'camp/glass/supply': (cp.glass_supply, 0),
    }
    for p in plan['plans']:
        reg['camp/board/war-%d' % p['index']] = ((lambda T, p=p: cp.war_board(T, p)), 0)
    for fam in cp.POSTERS:
        reg['camp/board/poster-' + fam] = ((lambda T, fam=fam: cp.poster(T, fam)), 0)
    for which in cp.NOTICES:
        reg['camp/notice/' + which] = ((lambda T, w=which: cp.notice(T, w)), 0)
    reg['camp/sign/north'] = ((lambda T: cp.gate_sign(T, 'north')), 0)
    for s in 'ESW':
        reg['camp/sign/gate-' + s] = ((lambda T, s=s: cp.gate_sign(T, s)), 0)
    for pose in ck.POSES:
        for v in range(3):
            reg['camp/figure/%s/%d' % (pose, v)] = ((lambda T, pose=pose, v=v: ck.figure(T, pose, v)), 0)
    # the survivors' props on the pilot model (Core NativeCampSurvivors)
    for name in ('mug', 'bowl', 'hammer', 'headset'):
        reg['camp/prop/' + name] = (getattr(cp, 'prop_' + name), 0)
    return reg


def build_all(plan, only=None):
    ck.finishes()
    reg = registry(plan)
    sb = sk.StageBank()
    stats, built = {}, {}
    for key in sorted(reg):
        if only and not any(key.startswith(o) for o in only):
            continue
        fn, reach = reg[key]
        for lod in (0, 2):
            if lod == 2 and stats.get(key, {}).get('0', 0) < LOW_FROM:
                continue
            t0 = time.time()
            T = mk.tier(lod)
            g = fn(T)
            if g is None or not g.faces:
                continue
            ao = sk.bake_ao([g], distance=reach, samples=10)[0] if (ARGS['ao'] and reach > 0 and lod == 0) else None
            bk = sk.Bake(g, ao=ao, floors=(0.0, 3.0, 9.0), seed=zlib.crc32(key.encode()) & 0xFFFF)
            om = sk.to_mesh(key + '/' + str(lod), g, bk)
            if om is None:
                continue
            sb.add(key + '/' + str(lod), 'camp', om)
            tris = sum(len(s) for s in om.submeshes) // 3
            stats.setdefault(key, {})[str(lod)] = tris
            if lod == 0:
                built[key] = (g, bk)
            print('built %-40s %s %7d tris  %.1f s' % (key, lod, tris, time.time() - t0))
    return sb, stats, built


# ---------------------------------------------------------------------------------------------------------------------------
# The art rules on the built meshes
# ---------------------------------------------------------------------------------------------------------------------------
def check(plan, built):
    bad = []
    for p in plan['pieces']:
        key = p['key'] if p['kind'] != 'WarBoard' else 'camp/board/war-0'
        if key not in built:
            continue
        g, _ = built[key]
        P = np.frombuffer(g.p, dtype=np.float32).reshape(-1, 3).astype(np.float64)
        A = np.array(p['place'], dtype=np.float64).reshape(3, 4)
        W = P @ A[:, :3].T + A[:, 3]
        corners = np.array([[(p['lo'] if i & 1 == 0 else p['hi'])[0], (p['lo'] if i & 2 == 0 else p['hi'])[1], (p['lo'] if i & 4 == 0 else p['hi'])[2]] for i in range(8)])
        C = corners @ A[:, :3].T + A[:, 3]
        lo, hi = C.min(axis=0) - .02, C.max(axis=0) + .02
        out = np.any((W < lo) | (W > hi), axis=1)
        if out.any():
            bad.append('%s at %s: %d vertices outside its envelope, e.g. %s' % (p['key'], np.round(A[:, 3], 2).tolist(), int(out.sum()), np.round(W[out][0], 3).tolist()))
        bl, bh = p['baseLo'], p['baseHi']
        low = W[:, 1] < 3.39
        if low.any():
            if bl[1] > bh[1]:
                bad.append('%s: %d vertices below 3.4 m but it has no base' % (p['key'], int(low.sum())))
            else:
                bc = np.array([[(bl if i & 1 == 0 else bh)[0], (bl if i & 2 == 0 else bh)[1], (bl if i & 4 == 0 else bh)[2]] for i in range(8)])
                B = bc @ A[:, :3].T + A[:, 3]
                blo, bhi = B.min(axis=0) - .015, B.max(axis=0) + .015
                o2 = low & np.any((W[:, [0, 2]] < blo[[0, 2]]) | (W[:, [0, 2]] > bhi[[0, 2]]), axis=1)
                if o2.any():
                    bad.append('%s at %s: %d vertices below 3.4 m outside its base, e.g. %s' % (p['key'], np.round(A[:, 3], 2).tolist(), int(o2.sum()), np.round(W[o2][0], 3).tolist()))
    for key, floor in (('camp/static/strands', 3.5), ('camp/static/sails', 3.5)):
        if key in built:
            P = np.frombuffer(built[key][0].p, dtype=np.float32).reshape(-1, 3)
            if P[:, 1].min() < floor - .02:
                bad.append('%s reaches down to %.2f m' % (key, float(P[:, 1].min())))
    if 'camp/static/cables' in built:
        P = np.frombuffer(built['camp/static/cables'][0].p, dtype=np.float32).reshape(-1, 3)
        far = np.maximum(np.abs(P[:, 0]), np.abs(P[:, 2])) < plan['inner'] - .1
        if far.any():
            bad.append('the cables leave the fence at %d vertices' % int(far.sum()))
    return bad


# ---------------------------------------------------------------------------------------------------------------------------
# Preview: the camp in Blender's workbench, the fence as grey boxes (to judge the models quickly; the look is the game's)
# ---------------------------------------------------------------------------------------------------------------------------
def fence_geo(plan):
    g = sk.Geo()
    F = ck.finishes()
    inner = plan['inner']
    for s, (n, r) in enumerate((((0, 0, 1), (1, 0, 0)), ((1, 0, 0), (0, 0, -1)), ((0, 0, -1), (-1, 0, 0)), ((-1, 0, 0), (0, 0, 1)))):
        for sign in (-1, 1):
            a0, a1 = sorted((sign * 5.0, sign * 25.0))
            c = [n[i] * (inner + .1) + r[i] * (a0 + a1) / 2 for i in range(3)]
            half = [abs(n[i]) * .1 + abs(r[i]) * (a1 - a0) / 2 for i in range(3)]
            sk.box(g, c[0] - half[0], c[0] + half[0], 0, 3.2, c[2] - half[2], c[2] + half[2], F['concrete'])
    sk.box(g, -40, 40, -.05, 0, -40, 40, F['paving'])
    return g


def placed(g, place):
    A = list(place)
    return g.transformed(A)


def preview(plan, built, out_dir):
    views = []
    geos = []
    F = ck.finishes()
    fg = fence_geo(plan)
    geos.append((fg, sk.Bake(fg)))
    for p in plan['pieces']:
        key = p['key'] if p['kind'] != 'WarBoard' else 'camp/board/war-0'
        if key in built:
            g = placed(built[key][0], p['place'])
            geos.append((g, sk.Bake(g)))
    for key in ('camp/static/strands', 'camp/static/sails', 'camp/static/cables'):
        if key in built:
            geos.append(built[key])
    live = list(geos)
    for s in plan['preview']:
        if s['key'] not in built:
            continue
        a = math.radians(s['yaw'])
        c, si = math.cos(a), math.sin(a)
        at = s['at']
        g = built[s['key']][0].transformed([c, 0, si, at[0], 0, 1, 0, at[1], -si, 0, c, at[2]])
        geos.append((g, sk.Bake(g)))
        gk = 'camp/glass/' + s['key'].split('/')[-1]
        if gk in built:
            gg = built[gk][0].transformed([c, 0, si, at[0], 0, 1, 0, at[1], -si, 0, c, at[2]])
            geos.append((gg, sk.Bake(gg)))
    sk.preview(live, [('live-high', (34, 30, -34), (0, 0, 0), 50), ('live-ground', (0, 1.7, -14), (0, 3, 20), 70),
                      ('live-ne', (6, 1.7, 6), (24, 3, 24), 70), ('live-gate-n', (0, 2, 12), (0, 4, 25), 70),
                      ('live-war', (17.2, 1.7, 21.5), (17.2, 1.9, 25), 60), ('live-missing', (-14.75, 1.7, 21.8), (-14.75, 1.8, 25), 60),
                      ('live-fire-e', (20.5, 1.6, 16.5), (24.8, 1.0, 16.5), 60)], out_dir, size=(1280, 720))
    sk.preview(geos, [('full-high', (34, 30, -34), (0, 0, 0), 50), ('full-ground', (0, 1.7, -14), (0, 2, 10), 75),
                      ('full-ring', (6.5, 2.0, -6.5), (0, 1, 0), 65), ('full-ne', (8, 2, 8), (16, 2, 16), 70),
                      ('full-sw', (-8, 2, -8), (-16, 2, -16), 70), ('full-se', (6, 4, -6), (13.5, 8, -13.5), 70),
                      ('full-n', (0, 2, 16), (0, 3, 25), 75)], out_dir, size=(1280, 720))
    # the survivors in a row
    row = []
    poses = sorted(ck.POSES)
    for i, pose in enumerate(poses):
        k = 'camp/figure/%s/%d' % (pose, i % 3)
        if k in built:
            g = built[k][0].transformed([1, 0, 0, (i - len(poses) / 2) * .8, 0, 1, 0, 0, 0, 0, 1, 0])
            row.append((g, sk.Bake(g)))
    if row:
        sk.preview(row, [('figures', (0, 1.2, 9), (0, .9, 0), 55)], out_dir, size=(1600, 600))


def clearance(plan, out_dir):
    """The table's clearances from above for the landing field's four worst draws (the review's diagram; the walk's K overlay draws
    the same boxes): A1's blocks amber, the gates' leaf travel and track red, the 4 m lanes green, the ring's walkway cyan, the pods
    white with their skid aprons yellow and the ExitReach 2.4 m capsule blue."""
    def fin(name, hexc):
        return sk.fin('camp-diagram-' + name, hexc, sk.DETAIL_NONE, smooth=.1, dirt=0)
    inner = plan['inner']
    base = sk.Geo()
    sk.box(base, -27, 27, -.05, 0, -27, 27, fin('floor', 0x2A2C30))
    for s_, (n, r) in enumerate((((0, 0, 1), (1, 0, 0)), ((1, 0, 0), (0, 0, -1)), ((0, 0, -1), (-1, 0, 0)), ((-1, 0, 0), (0, 0, 1)))):
        for sign in (-1, 1):
            a0, a1 = sorted((sign * 5.0, sign * 25.0))
            c = [n[i] * (inner + .1) + r[i] * (a0 + a1) / 2 for i in range(3)]
            half = [abs(n[i]) * .1 + abs(r[i]) * (a1 - a0) / 2 for i in range(3)]
            sk.box(base, c[0] - half[0], c[0] + half[0], 0, 3.2, c[2] - half[2], c[2] + half[2], fin('fence', 0x8A8C90))
            p0 = [n[i] * (inner - .25) + r[i] * sign * 5.0 for i in range(3)]
            p1 = [n[i] * inner + r[i] * sign * 10.3 for i in range(3)]
            sk.box(base, min(p0[0], p1[0]), max(p0[0], p1[0]), 0, 3.4, min(p0[2], p1[2]), max(p0[2], p1[2]), fin('pocket', 0xE0302A))
        q0 = [n[i] * 9 + r[i] * -2 for i in range(3)]
        q1 = [n[i] * inner + r[i] * 2 for i in range(3)]
        sk.box(base, min(q0[0], q1[0]), max(q0[0], q1[0]), 0, .02, min(q0[2], q1[2]), max(q0[2], q1[2]), fin('lane', 0x36C060))
    for (x0, x1, z0, z1) in ((-7.1, 7.1, 4.6, 7.1), (-7.1, 7.1, -7.1, -4.6), (4.6, 7.1, -4.6, 4.6), (-7.1, -4.6, -4.6, 4.6)):
        sk.box(base, x0, x1, 0, .015, z0, z1, fin('walk', 0x34B2D0))
    for row in plan['table']:
        lo, hi = row['min'], row['max']
        sk.box(base, lo[0], hi[0], lo[1], hi[1], lo[2], hi[2], fin('block', 0xFFB040))
    for (x, z) in ((13.5, 13.5), (-13.5, -13.5)):
        sk.box(base, x - .65, x + .65, 0, 2.2, z - .65, z + .65, fin('machine', 0xC07A20))
    sk.box(base, -13.5 - 2.1, -13.5 + 2.1, 0, 3.5, 13.5 - 2.1, 13.5 + 2.1, fin('revive', 0xB06A30))
    for k, field in enumerate(plan.get('fields', [])):
        g = sk.Geo()
        g.extend(base)
        for (x, z, yaw) in field['pods']:
            a = math.radians(yaw)
            fx, fz = math.sin(a), math.cos(a)
            sk.lathe(g, [(1.17, 0.0), (1.17, 3.0)], (x, 0, z), fin('pod', 0xF1F5F9), 8, cap1=True)
            bx_, bz_ = x - fx * 2.5, z - fz * 2.5
            for t in range(6):
                cx, cz = x - fx * 2.5 * t / 5, z - fz * 2.5 * t / 5
                sk.box(g, cx - 1.17, cx + 1.17, 0, .03, cz - 1.17, cz + 1.17, fin('apron', 0xC8B25A))
            cx, cz = x + fx * 2.4, z + fz * 2.4
            sk.box(g, cx - .42, cx + .42, 0, 1.7, cz - .42, cz + .42, fin('capsule', 0x70B5FF))
        sk.preview([(g, sk.Bake(g))], [('clearance-%d' % k, (0.0, 120.0, -.5), (0.0, 0.0, 0.0), 25)], out_dir, size=(1000, 1000))
        print('clearance diagram %d: seed %s, nearest pod %.2f m from a block' % (k, field['seed'], field['gap']))


def main():
    plan = json.load(open(ARGS['plan']))
    only = ARGS['only'].split(',') if ARGS['only'] else None
    sb, stats, built = build_all(plan, only)
    bad = check(plan, built)
    for b in bad:
        print('ART RULE:', b)
    out = ARGS['out']
    path = sb.write(out, BANK)
    report = {'bank': path, 'bytes': os.path.getsize(path), 'stats': stats, 'art_rule_failures': bad}
    with open(os.path.join(out, 'camp-report.json'), 'w') as fh:
        json.dump(report, fh, indent=1)
    if ARGS.get('preview'):
        preview(plan, built, ARGS['preview'])
        clearance(plan, ARGS['preview'])
    print('camp bank', path, os.path.getsize(path), 'bytes;', len(bad), 'art rule failures')


if __name__ == '__main__':
    main()
