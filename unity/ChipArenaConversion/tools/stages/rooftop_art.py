"""The main menu's rooftop art (docs/MENU-ROOFTOP-ART.md): the home screen's "Pilot's Rooftop" modelled on the pieces Core
NativeRooftopCity builds, into its own bank ChipArenaStageRooftop.

  dotnet run --project Tests/CoreTests.csproj -c Release -- --rooftop-layout LAYOUT.json
  blender --background --factory-startup --python tools/stages/rooftop_art.py -- --layout LAYOUT.json --out <Assets>/LocalAssets
  (--preview DIR [--views a,b] renders the baked High tier; --tiers 0,2; --no-ao; --stats; --check: the sight lines)

The bank (Runtime NativeRooftopModels reads it; tier 0 the High and reduced-effects tiers, 2 the Low graphics tier):
  static/perch/<tier>        our roof (rooftop_perch.py), solid and glow                          group "static"
  static/city-<n>/<tier>     the towers' built detail, the actors' roofs, the bridge, the huts and the screens' frames
                             (rooftop_city.py)                                                     group "static"
  facade/<perch|city>/<tier> the towers' walls for the menu's facade shader (rooftop_kit.Facade)   group "facade"
  rig/car-<kind>/<tier>      the flying cars (rooftop_cars.py), each in its own frame              group "rig"
  cover/perch, cover/city    no parts: the parts of Core's quads the art stands in for             group "cover"
"""
import argparse
import math
import os
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import stagekit as sk  # noqa: E402
import cybercity_kit as ck  # noqa: E402
import rooftop_kit as rk  # noqa: E402
from rooftop_kit import F  # noqa: E402

BANK = 'ChipArenaStageRooftop'
# m: two same-facing faces nearer than EPS are one plane and are set LIFT apart. The roof is seen from 6 to 30 m; the city from
# 40 to 750 m through a .5 m near plane, where a 24-bit depth buffer parts about z^2 / (near 2^24): 11 mm at 300 m, 67 mm at
# 750 m. The city's faces stand that far apart at least.
PERCH_EPS, PERCH_LIFT = (.003, .005, .008), (.006, .012, .02)
CITY_EPS, CITY_LIFT = (.012, .05), (.03, .1)   # within NEAR m of the camera, and beyond
NEAR = 260.0


def copy(g):
    c = sk.Geo()
    c.extend(g)
    return c


def split(g, eye):
    """The Geo's faces within NEAR m of the eye (in plan), and the rest."""
    P, N, S, Fi = g.arrays()
    out = (sk.Geo(), sk.Geo())
    if len(S) == 0:
        return out
    starts = np.zeros(len(S), dtype=np.int64)
    starts[1:] = np.cumsum(S[:-1])
    cen = np.add.reduceat(P.astype(np.float64), starts, axis=0) / S[:, None]
    far = np.hypot(cen[:, 0] - eye[0], cen[:, 2] - eye[2]) > NEAR
    cm = np.repeat(far, S)
    for k, (fm, m) in enumerate(((~far, ~cm), (far, cm))):
        o = out[k]
        o.p.frombytes(P[m].astype(np.float32).tobytes())
        o.n.frombytes(N[m].astype(np.float32).tobytes())
        o.size.frombytes(S[fm].astype(np.uint8).tobytes())
        o.fin.frombytes(Fi[fm].astype(np.uint16).tobytes())
    return out


def wet(g, bk):
    """The rain on our roof: patches of the membrane, the ballast and the pavers lie wet (smoother, darker), so the sky and the
    neon lie in them."""
    P, N, S_, F_ = g.arrays()
    fc = np.repeat(F_, S_)
    names = ('membrane', 'membranelap', 'pad', 'gravel', 'coping', 'helideck')
    mask = np.isin(fc, [F[n] for n in names]) & (N[:, 1] > .7)
    if not mask.any():
        return
    W = P[mask].astype(np.float64)
    n = sk.fbm(W * np.array([1.0, 0.0, 1.0]), .3, 3, 41)
    w = sk.smooth(.4, .66, n)
    sm = bk.uv0[mask, 0]
    bk.uv0[mask, 0] = (sm + (.95 - sm) * w * .9).astype(np.float32)
    col = bk.colour[mask, :3].astype(np.float64)
    bk.colour[mask, :3] = np.round(col * (1 - .32 * w)[:, None]).astype(np.uint8)


def sight(L, geos):
    """Every composition target stays in clear view from the loop's camera (RooftopChecks' rule: nothing nearer than 2.5 m
    short of it), against the modelled scene. Returns the blocked ones."""
    from mathutils.bvhtree import BVHTree
    from mathutils import Vector
    verts, polys = [], []
    for g in geos:
        P, N, S, Fi = g.arrays()
        base = len(verts)
        verts.extend(map(tuple, P.astype(float)))
        start = 0
        for k in S:
            polys.append(tuple(range(base + start, base + start + int(k))))
            start += int(k)
    tree = BVHTree.FromPolygons(verts, polys, all_triangles=False)
    bad = []
    for (t, eye, fwd) in L.camera:
        e = Vector(eye)
        for (what, p) in L.targets:
            d = Vector(p) - e
            dist = d.length
            hit = tree.ray_cast(e, d / dist, dist)
            if hit[0] is not None and hit[3] < dist - 2.5:
                bad.append((what, t, round(hit[3], 1), round(dist, 1), tuple(round(x, 1) for x in hit[0])))
    return bad


def views(L):
    e, f = L.eye, L.fwd
    at = lambda k: (e[0] + f[0] * k, e[1] + f[1] * k, e[2] + f[2] * k)  # noqa: E731
    b = L.sites['blade']
    return [('menu', e, at(100), 68.6), ('menu-zoom', e, at(100), 36), ('strip', (-4.2, 2.4, 1.6), (3.2, .5, -.9), 55), ('tv', (-1.2, 1.5, 1.4), (1.3, .7, -.5), 50),
            ('pilot', (-2.2, 1.3, -.2), (.6, .4, -1.3), 55), ('hvac', (.2, 1.9, 1.6), (3.2, .6, -.7), 55), ('mast', (2.5, 2.2, 3.0), (7.2, 3.0, -1.2), 60),
            ('roof', (-3, 9, 9), (10, 0, 2), 70), ('back', (10, 3, 0), (20, 2, 8), 70), ('below', (-9, -2, -7), (0, -6, -1.8), 60),
            ('duel', (b[0] - 22, b[1] + 9, b[2] + 26), (b[0] + 4, b[1], b[2] - 4), 50), ('duel-near', (b[0] - 2, b[1] + 3, b[2] + 14), (b[0] + 6, b[1] + 1, b[2] - 6), 60),
            ('near', e, (95, -70, -45), 30), ('near2', e, (150, -80, -80), 22), ('mid', e, (60, -30, -200), 26), ('far', e, (200, 20, -330), 22),
            ('screen', e, (92, 18, -299), 14), ('chip', e, (68.5, -14, -135), 20), ('banner', e, (27.6, -3, -202), 14), ('arena', e, (267, 53, -374), 12),
            ('street', e, (40, -120, -150), 40), ('top', (60, 160, -60), (60, -80, -120), 70)]


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--layout', required=True)
    ap.add_argument('--out', help='Assets/LocalAssets to write the bank into')
    ap.add_argument('--preview', help='render views of the baked High tier')
    ap.add_argument('--views', help='only these views (comma separated)')
    ap.add_argument('--tiers', default='0,2')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--stats', action='store_true')
    ap.add_argument('--check', action='store_true', help='the sight lines against the modelled scene')
    ap.add_argument('--only', help='perch or city (previews)')
    args = ap.parse_args(argv)
    L = rk.Layout(args.layout)
    rk.finishes()
    import rooftop_perch as perch
    import rooftop_city as city
    import rooftop_cars as cars
    sb = sk.StageBank()
    t0 = time.time()
    for ti in [int(x) for x in args.tiers.split(',')]:
        T = sk.Tier(ti)
        t = time.time()
        gp, fp = perch.build(L, T) if args.only != 'city' else (sk.Geo(), rk.Facade())
        gc, fcity = city.build(L, T) if args.only != 'perch' else (sk.Geo(), rk.Facade())
        print('tier %s: perch %d faces, city %d faces, facades %d + %d, %.1f s' % (T.name, gp.faces, gc.faces, fp.g.faces, fcity.g.faces, time.time() - t))
        print('  perch coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % sk.lift_coplanar(gp, eps=PERCH_EPS[ti], lift=PERCH_LIFT[ti], passes=4))
        gcs = split(gc, L.eye)
        fcs = split(fcity.g, L.eye)
        for k in (0, 1):
            print('  city %s coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % ((('near', 'far')[k],) + tuple(sk.lift_coplanar(gcs[k], eps=CITY_EPS[k], lift=CITY_LIFT[k], passes=4))))
        gc = copy(gcs[0])
        gc.extend(gcs[1])
        # what is left, the walls counted with the solids (a copy: the measure moves faces)
        for (name, a, b, eps, lift) in (('perch', gp, fp.g, PERCH_EPS[ti], PERCH_LIFT[ti]), ('city near', gcs[0], fcs[0], CITY_EPS[0], CITY_LIFT[0]), ('city far', gcs[1], fcs[1], CITY_EPS[1], CITY_LIFT[1])):
            c = copy(a)
            c.extend(b)
            r = sk.lift_coplanar(c, eps=eps, lift=lift, passes=1)
            print('  %s left: %d pairs, %.3f m2 of same-facing faces within %.0f mm' % (name, r[0], r[1], eps * 1000))
        if args.check:
            bad = sight(L, [gp, gc, fp.g, fcity.g])
            print('  sight lines: %d targets x %d camera places, %d blocked' % (len(L.targets), len(L.camera), len(bad)))
            for b in bad[:12]:
                print('    blocked:', b)
        aop = aoc = None
        if not args.no_ao:
            aop, aoc = sk.bake_ao([gp, gc], distance=(1.4, 1.4, 1.8)[ti], samples=(32, 16, 12)[ti], occluders=[fp.g, fcity.g])
            # open air at night: the occlusion eased, so the roofs keep the sky
            aop = 1 - (1 - aop) * .85
            aoc = 1 - (1 - aoc) * .7
        bkp = sk.Bake(gp, aop, floors=(0.0,))
        wet(gp, bkp)
        tops = sorted(set(round(l.top, 1) for l in L.city))
        bkc = sk.Bake(gc, aoc, floors=tops)
        wet(gc, bkc)
        sb.add('static/perch/%d' % ti, 'static', sk.to_mesh('static/perch/%d' % ti, gp, bkp))
        # the city in two meshes by distance (each well under the bank's vertex limit)
        ncorner = len(gcs[0].p) // 3
        nface = gcs[0].faces
        mask = np.zeros(gc.faces, dtype=bool)
        mask[:nface] = True
        for k, m in enumerate((mask, ~mask)):
            name = 'static/city-%d/%d' % (k, ti)
            sb.add(name, 'static', sk.to_mesh(name, gc, bkc, m))
        for (name, fc) in (('perch', fp), ('city', fcity)):
            om = fc.mesh('facade/%s/%d' % (name, ti))
            if om is not None:
                mi = sb.bank.mesh(om.name, om, [sb.solid])
                sb.bank.prop(om.name, 'facade', [(mi, [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0])])
                print('bank facade  %-5s %8d triangles %8d vertices (%s)' % (T.name, len(om.submeshes[0]) // 3, len(om.positions), name))
        shown = []
        for kind in cars.KINDS:
            g = cars.car(T, kind)
            ao = None
            if not args.no_ao:
                ao = sk.bake_ao([g], distance=.6, samples=16)[0]
                ao = 1 - (1 - ao) * .6
            bk = sk.Bake(g, ao, floors=(-99.0,))
            name = 'rig/car-%s/%d' % (kind, ti)
            sb.add(name, 'rig', sk.to_mesh(name, g, bk))
            shown.append((g, bk, kind))
        if args.preview and ti == 0:
            vs = views(L)
            if args.views:
                vs = [v for v in vs if v[0] in args.views.split(',')]
            pv = [(gp, bkp), (gc, bkc), (fp.g, sk.Bake(fp.g, None)), (fcity.g, sk.Bake(fcity.g, None))]
            # the cars lined up over the roof for a look
            for k, (g, bk, kind) in enumerate(shown):
                gg = g.transformed(sk.affine((0, 0, 0, 1), (4.0 + k * 3.2, 3.0, 7.0)))
                pv.append((gg, sk.Bake(gg, None)))
            if any(v[0] == 'cars' for v in vs) or not args.views:
                vs.append(('cars', (2, 5.5, 14), (10.5, 3, 7), 60))
            sk.preview(pv, vs, args.preview)
        del gp, gc
    if args.out:
        sb.bank.prop('cover/perch', 'cover', [])
        sb.bank.prop('cover/city', 'cover', [])
        sb.write(args.out, BANK)
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
