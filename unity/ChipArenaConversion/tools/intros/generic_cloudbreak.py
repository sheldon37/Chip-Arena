"""Cloudbreak's set: a summit relay station above a sea of cloud at dawn (docs/INTROS-GENERIC.md "Cloudbreak"). Unity space; the pad's
centre is the hero mark (the origin), a side of the square pad facing +z (the camera). Pieces (generic_sets.py writes them):
  cloudbreak/summit     the pad, its paint, the rock, the boulders, the mast and its dishes, the hut, the solar array, the railing,
                        the windsock poles, the conduit (world frame)
  cloudbreak/padlights  the pad's eight edge-light lenses (glow; world frame)
  cloudbreak/beacon     the mast's red beacon lens (glow; world frame)
  cloudbreak/sock       one windsock, its mouth at the pivot (the pole top, CB_SOCK_TOP), streaming along +z
  cloudbreak/peaks      the far peaks (world frame)        cloudbreak/clouds   the cloud sea and far cumulus (world frame)
  cloudbreak/sky        the dawn sky dome (glow)
  cloudbreak/sun        the sun's face, facing +z, radius 7.2: rings white-hot at the core, gold at the limb (glow)
"""
import math

import stagekit as sk
from stagekit import Geo, _add, _sub, _norm, _cross
from generic_kit import (PI, TAU, V, mul, lerp, M, place, noise2, polar_field, rockball, mound, hash01, dome, disc, ribbed, lattice_tower, dish,
                         flipped, sky_fn, mix_hex, Piece, smoothstep, ang_diff)

CB_R = 3.9                       # the pad's half width (a square pad, a side to the camera)
CB_MAST = (3.9, -.62, -4.9)      # the mast's foot (its shelf)
CB_HUT = (-4.9, -.62, -3.7)
CB_SOCKS = ((3.95, 1.52), (-3.95, 1.52))   # the windsock poles (x, z)
CB_SOCK_TOP = 3.2                # the sock's pivot height (Unity y)
CB_BEACON_Y = 15.72
CB_SUN_DIR = (0.0, -1.0)         # the sun's azimuth (Unity x, z), low behind the pilot (behind its shoulder in the hero frame)


def sq(th):
    """The square pad's edge distance at heading th (its sides face +-x and +-z)."""
    return CB_R / max(abs(math.cos(th)), abs(math.sin(th)))


def build(T):
    F = dict(
        concrete=sk.fin('cb-concrete', 0x9b9a93, sk.DETAIL_PLASTER, smooth=.12, dirt=1.1, wear=.35),
        skirt=sk.fin('cb-skirt', 0x85837c, sk.DETAIL_PLASTER, smooth=.1, dirt=1.3),
        white=sk.fin('cb-white', 0xe9e7de, smooth=.3, dirt=.5, wear=.5),
        yellow=sk.fin('cb-yellow', 0xe7b12c, smooth=.3, dirt=.6, wear=.5),
        black=sk.fin('cb-black', 0x26272a, smooth=.25, dirt=.5),
        rock=sk.fin('cb-rock', 0x6e6862, sk.DETAIL_CARVED, smooth=.08, dirt=.7),
        rockd=sk.fin('cb-rockd', 0x58534e, sk.DETAIL_CARVED, smooth=.06, dirt=.8),
        snow=sk.fin('cb-snow', 0xf0f3f8, smooth=.5, dirt=.08),
        steel=sk.fin('cb-steel', 0x8d959d, sk.DETAIL_IRON, smooth=.45, metal=.7, dirt=.5, wear=.4),
        orange=sk.fin('cb-orange', 0xd9542b, smooth=.35, metal=.3, dirt=.5, wear=.3),
        mastw=sk.fin('cb-mastw', 0xe6e4dc, smooth=.35, metal=.3, dirt=.5, wear=.3),
        dish=sk.fin('cb-dish', 0xdedcd5, smooth=.35, dirt=.4),
        dishb=sk.fin('cb-dishb', 0xa9a7a0, smooth=.3, dirt=.6),
        hut=sk.fin('cb-hut', 0x6d7f86, sk.DETAIL_IRON, smooth=.35, metal=.55, dirt=.9, wear=.4),
        hutd=sk.fin('cb-hutd', 0x3d474d, sk.DETAIL_IRON, smooth=.3, metal=.5, dirt=.9),
        solar=sk.fin('cb-solar', 0x1d2b4c, smooth=.85, metal=.25, dirt=.15),
        alu=sk.fin('cb-alu', 0xbcc0c4, smooth=.55, metal=.8, dirt=.4),
        sockr=sk.fin('cb-sockr', 0xe2552a, sk.DETAIL_CLOTH, smooth=.15, dirt=.3),
        sockw=sk.fin('cb-sockw', 0xece8e0, sk.DETAIL_CLOTH, smooth=.15, dirt=.3),
        cloud=sk.fin('cb-cloud', 0xf6efe9, smooth=.02, dirt=0),
        peakrock=sk.fin('cb-peakrock', 0x6c6772, smooth=.05, dirt=.15),
        peaksnow=sk.fin('cb-peaksnow', 0xf3f2f6, smooth=.4, dirt=0),
        sky=sk.fin('cb-sky', 0xffffff, glow=1.0, dirt=0),
        sun=sk.fin('cb-sun', 0xfff1d2, glow=1.0, dirt=0),
        beacon=sk.fin('cb-beacon', 0xff3524, glow=1.0, dirt=0),
        padlight=sk.fin('cb-padlight', 0xcdeeff, glow=1.0, dirt=0),
    )
    fine = T.i == 0
    g = Geo()
    # ---- the pad: a square of concrete, a side to the camera (no circles anywhere: a painted H, hazard-striped corners, edge lights
    # along the sides, scuffs and thruster scorch) ----
    Y = .012
    R = CB_R
    nq = 8 if fine else 2

    def pp(u, v, y=Y):
        return V(u * R, y, v * R)
    for i in range(nq):
        for j in range(nq):
            u0, u1 = -1 + 2 * i / nq, -1 + 2 * (i + 1) / nq
            v0, v1 = -1 + 2 * j / nq, -1 + 2 * (j + 1) / nq
            g.face((pp(u0, v0), pp(u0, v1), pp(u1, v1), pp(u1, v0)), F['concrete'], normal=(0, 1, 0))
    # the skirt: the pad's sides down into the rock, leaning out a little
    corners = ((-1, -1), (1, -1), (1, 1), (-1, 1))
    for k in range(4):
        (u0, v0), (u1, v1) = corners[k], corners[(k + 1) % 4]
        a, b = pp(u0, v0), pp(u1, v1)
        c, d = pp(u1 * 1.04, v1 * 1.04, -.75), pp(u0 * 1.04, v0 * 1.04, -.75)
        out = _norm(((u0 + u1) / 2, 0, (v0 + v1) / 2))
        g.face((a, d, c, b), F['skirt'], normal=out)
    # paint, lifted clear of the slab
    lift = .016

    def quad(x0, z0, x1, z1, fin, y=lift):
        g.face((V(x0, y, z0), V(x0, y, z1), V(x1, y, z1), V(x1, y, z0)), fin, normal=(0, 1, 0))
    # the H (its crossbar under the hero mark), facing the camera
    quad(-.95, -1.15, -.63, 1.15, F['white'])
    quad(.63, -1.15, .95, 1.15, F['white'])
    quad(-.63, -.16, .63, .16, F['white'])
    # hazard-striped corners: a square patch of diagonal yellow and black stripes in each corner
    nstr = 6 if fine else 3
    for cu, cv in corners:
        x0, z0 = cu * R, cv * R
        x1, z1 = cu * (R - 1.0), cv * (R - 1.0)
        for k in range(nstr):
            # stripes across the patch along its diagonal: trapezoids between lines x+z = c (in the corner's own frame)
            for half in range(2):
                a0, a1 = k / nstr, (k + 1) / nstr
                f = F['yellow'] if (k + half) % 2 == 0 else F['black']
                if half == 0:
                    pts = [(a0, 0), (a1, 0), (0, a1), (0, a0)]
                else:
                    pts = [(1 - a1, 1), (1 - a0, 1), (1, 1 - a0), (1, 1 - a1)]
                q = [V(x0 + (x1 - x0) * p, lift + .0005, z0 + (z1 - z0) * r) for p, r in pts]
                g.face(q, f, normal=(0, 1, 0))
    # scuffs and scorch: dark irregular marks from landings, thickest round the H
    for i in range(14 if fine else 6):
        cx = (hash01('sx', i) - .5) * 4.6
        cz = (hash01('sz', i) - .5) * 4.6
        w, d = .25 + .55 * hash01('sw', i), .08 + .14 * hash01('sd', i)
        a = TAU * hash01('sa', i)
        ca, sa = math.cos(a), math.sin(a)
        q = [V(cx + ca * x - sa * z, lift + .0008, cz + sa * x + ca * z) for x, z in ((-w, -d), (-w, d), (w, d * .6), (w, -d * .6))]
        g.face(q, F['black'], normal=(0, 1, 0))
    # the edge lights along the four sides (housings here, their lenses are cloudbreak/padlights)
    lights = Geo()
    edge_pts = []
    for t in (-.7, 0.0, .7):
        edge_pts += [(t, -1), (t, 1), (-1, t), (1, t)]
    edge_pts += [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    for u, v in edge_pts:
        c = pp(u * .95, v * .95, Y)
        sk.box(g, c[0] - .11, c[0] + .11, Y - .01, Y + .03, c[2] - .11, c[2] + .11, F['steel'], None, .01, cell=0)
        sk.box(lights, c[0] - .07, c[0] + .07, Y + .03, Y + .065, c[2] - .07, c[2] + .07, F['padlight'], None, .008, cell=0)

    # ---- the summit: a faceted rock falling away to the clouds, ridged, with shelves for the mast and the hut ----
    ridges = ((.45, 2.6, .34), (2.15, 3.6, .3), (3.75, 2.2, .42), (5.15, 3.2, .3))

    def rock_h(r, t, x, z):
        s = max(0.0, r - 3.55)
        y = -.5 - .085 * s ** 1.72
        rg = 0.0
        for tk, a, w in ridges:
            rg += a * math.exp(-(ang_diff(t, tk) / w) ** 2)
        y += rg * smoothstep(1.0, 8.0, s) * (1 + .04 * s)
        y += (noise2(x, z, .18, 11) - .5) * (.4 + .16 * s) * smoothstep(.3, 2.5, s)
        for (sx, sy, sz), rad in ((CB_MAST, 2.1), (CB_HUT, 2.3)):
            d = math.hypot(x - sx, z - sz)
            k = 1 - smoothstep(rad * .6, rad * 1.4, d)
            y = y * (1 - k) + (sy - .05) * k
        return y

    def rock_fin(x, y, z, n):
        if n[1] > .8 and y < -1.0 and noise2(x, z, .3, 5) > .45:
            return F['snow']
        return F['rockd'] if n[1] < .5 else F['rock']
    polar_field(g, (0, 0), 3.5, 30, 22 if fine else 9, 64 if fine else 24, rock_h, rock_fin, smooth=False, rpow=1.4)
    # boulders on the slopes (none where the camera runs low in front)
    for i, (a, r, s) in enumerate(((2.9, 5.0, .7), (3.3, 6.2, .45), (4.3, 4.6, .55), (5.6, 5.4, .8), (6.0, 7.8, 1.1), (.9, 6.6, .9),
                                   (1.25, 8.5, .6), (3.6, 9.0, 1.2), (4.95, 8.2, .5))):
        x, z = math.cos(a) * r, math.sin(a) * r
        y = rock_h(r, a, x, z)
        rockball(g, V(x, y + s * .25, z), (s * 1.2, s * .8, s), F['rock'], 7 + i, 5 if fine else 3, 8 if fine else 5, .25, .45)

    # ---- the relay mast: a red-and-white lattice, two dishes, a beacon ----
    mx, my, mz = CB_MAST
    sk.box(g, mx - 1.05, mx + 1.05, my - .7, my + .12, mz - 1.05, mz + 1.05, F['concrete'], T, .04)
    lattice_tower(g, (mx, my + .12, mz), 15.4, .95, .3, 1.28, T, F['orange'], legs=3, yaw=.35,
                  band=lambda i: F['orange'] if i % 2 == 0 else F['mastw'], leg_r=.06, tie_r=.032)
    ty = my + .12 + 15.4
    sk.cylinder(g, V(mx, ty - .05, mz), V(mx, ty + .08, mz), .38, F['steel'], 12 if fine else 6)
    sk.cylinder(g, V(mx, ty + .08, mz), V(mx, CB_BEACON_Y - .02, mz), .07, F['steel'], 8 if fine else 5)
    sk.cylinder(g, V(mx + .18, ty + .08, mz), V(mx + .18, ty + 2.6, mz), .016, F['steel'], 5 if fine else 3, caps=False)
    beacon = Geo()
    sk.lathe(beacon, [(.11, 0), (.12, .08), (.1, .17), (.05, .22), (.001, .235)], V(mx, CB_BEACON_Y - .02, mz), F['beacon'], 12 if fine else 6)
    for h, rr in ((9.4, .55), (12.9, .42)):
        yy = my + .12 + h
        tri = [V(mx + rr * 1.5 * math.sin(.35 + TAU * k / 3), yy, mz + rr * 1.5 * math.cos(.35 + TAU * k / 3)) for k in range(3)]
        g.face(tri, F['steel'], normal=(0, 1, 0))
        g.face(tri[::-1], F['steel'], normal=(0, -1, 0))
        if fine:
            for k in range(3):
                a, b = tri[k], tri[(k + 1) % 3]
                sk.cylinder(g, _add(a, (0, .9, 0)), _add(b, (0, .9, 0)), .02, F['yellow'], 4, caps=False)
                sk.cylinder(g, a, _add(a, (0, .9, 0)), .02, F['yellow'], 4, caps=False)
    dish(g, V(mx + .75 * math.sin(.9), my + 10.3, mz + .75 * math.cos(.9)), .9, .12, .78, F['dish'], F['dishb'], F['steel'], T, .3)
    dish(g, V(mx + .62 * math.sin(-1.4), my + 12.7, mz + .62 * math.cos(-1.4)), -1.4, .22, .56, F['dish'], F['dishb'], F['steel'], T, .22)
    # the conduit from the hut to the mast, along the ground
    sk.tube(g, sk.catmull([V(CB_HUT[0] + .9, -.5, CB_HUT[2] - .6), V(-2.6, -.6, -4.9), V(.6, -.66, -5.6), V(2.7, -.58, -5.2), V(mx - .7, my + .05, mz)], 4),
            .055, F['black'], 6 if fine else 4)

    # ---- the equipment hut: corrugated, a door, a vent, a sign, a stack ----
    hut = Geo()
    sk.box(hut, -1.75, 1.75, -.45, .12, -1.35, 1.35, F['concrete'], T, .03)
    W, D, Hh = 1.4, 1.0, 2.3
    ribs = 16 if fine else 6
    amp = .022 if fine else 0.0
    ribbed(hut, V(-W, .12, D), V(2 * W, 0, 0), V(0, Hh, 0), ribs, amp, F['hut'], 1, normal=(0, 0, 1))
    ribbed(hut, V(W, .12, -D), V(-2 * W, 0, 0), V(0, Hh, 0), ribs, amp, F['hut'], 1, normal=(0, 0, -1))
    ribbed(hut, V(W, .12, D), V(0, 0, -2 * D), V(0, Hh, 0), ribs * 2 // 3, amp, F['hut'], 1, normal=(1, 0, 0))
    ribbed(hut, V(-W, .12, -D), V(0, 0, 2 * D), V(0, Hh, 0), ribs * 2 // 3, amp, F['hut'], 1, normal=(-1, 0, 0))
    sk.box(hut, -W - .14, W + .14, Hh + .1, Hh + .24, -D - .16, D + .16, F['hutd'], T, .03)
    sk.box(hut, -.46, .46, .14, 2.08, D, D + .05, F['hutd'], T, .015)
    sk.cylinder(hut, V(.32, 1.1, D + .05), V(.32, 1.1, D + .13), .025, F['steel'], 6)
    sk.box(hut, .72, 1.18, 1.48, 1.8, D + .02, D + .04, F['yellow'], None, .003, cell=0)
    for k in range(3):
        sk.box(hut, .78 + k * .14, .86 + k * .14, 1.53, 1.75, D + .04, D + .05, F['black'], None, 0, cell=0)
    sk.box(hut, W, W + .12, 1.1, 1.8, -.45, .45, F['hutd'], T, .01)
    if fine:
        for k in range(6):
            sk.box(hut, W + .1, W + .16, 1.15 + k * .11, 1.2 + k * .11, -.42, .42, F['steel'], None, 0, cell=0)
    sk.cylinder(hut, V(-.7, Hh + .24, -.3), V(-.7, Hh + .95, -.3), .12, F['steel'], 8 if fine else 5)
    sk.cylinder(hut, V(-.7, Hh + .95, -.3), V(-.7, Hh + 1.02, -.3), .2, F['hutd'], 8 if fine else 5)
    place(g, hut, M(CB_HUT, yaw=.55))

    # ---- the solar array on the slope, facing the sunrise ----
    sol = Geo()
    for x in (-1.0, 1.0):
        sk.cylinder(sol, V(x, -1.1, .35), V(x, .55, .35), .05, F['alu'], 6 if fine else 4)
        sk.cylinder(sol, V(x, -1.1, -.4), V(x, .05, -.4), .05, F['alu'], 6 if fine else 4)
    pan = Geo()
    sk.box(pan, -1.3, 1.3, -.03, .03, -.65, .65, F['alu'], None, .01, cell=0)
    pan.face((V(-1.25, .032, -.6), V(1.25, .032, -.6), V(1.25, .032, .6), V(-1.25, .032, .6)), F['solar'], normal=(0, 1, 0))
    if fine:
        for k in range(1, 6):
            sk.box(pan, -1.3 + 2.6 * k / 6 - .008, -1.3 + 2.6 * k / 6 + .008, .03, .036, -.62, .62, F['alu'], None, 0, cell=0)
        sk.box(pan, -1.26, 1.26, .03, .036, -.008, .008, F['alu'], None, 0, cell=0)
    place(sol, pan, M((0, .3, 0), pitch=-.62))
    sx, sz = -6.9, 1.2
    place(g, sol, M((sx, rock_h(math.hypot(sx, sz), math.atan2(sz, sx), sx, sz) + 1.0, sz), yaw=PI + .3))

    # ---- the railing round the pad's back, the windsock poles ----
    rail = [pp(-.985, .1, Y), pp(-.985, -.985, Y), pp(.985, -.985, Y), pp(.985, .1, Y)]
    pts = []
    for i in range(len(rail) - 1):
        pts += [rail[i], lerp(rail[i], rail[i + 1], .5)]
    pts.append(rail[-1])
    for p in pts:
        sk.cylinder(g, p, _add(p, (0, 1.08, 0)), .028, F['yellow'], 6 if fine else 4)
    for h in ((.55, 1.06) if fine else (1.06,)):
        sk.tube(g, [_add(p, (0, h, 0)) for p in pts], .024, F['yellow'], 6 if fine else 4)
    for (x, z) in CB_SOCKS:
        y = -.25
        sk.cylinder(g, V(x, y - .3, z), V(x, CB_SOCK_TOP + .05, z), .045, F['steel'], 6 if fine else 4)
        for k in range(4):
            y0 = y + .5 + k * .7
            sk.cylinder(g, V(x, y0, z), V(x, y0 + .35, z), .049, F['orange'], 6 if fine else 4, caps=False)
        sk.cylinder(g, V(x, CB_SOCK_TOP + .05, z), V(x, CB_SOCK_TOP + .12, z), .07, F['steel'], 6 if fine else 4)
    # the windsock: its mouth at the pivot, streaming along +z (Core turns it downwind and droops it)
    sock = Geo()
    n = 5
    for k in range(n):
        z0, z1 = 1.3 * k / n, 1.3 * (k + 1) / n
        r0, r1 = .2 - .09 * k / n, .2 - .09 * (k + 1) / n
        sk.lathe(sock, [(r0, z0), (r1, z1)], (0, 0, 0), F['sockr'] if k % 2 == 0 else F['sockw'], 12 if fine else 6, axis=(0, 0, 1))
    sock.extend(flipped(sock))
    sk.tube(sock, [V(.2 * math.cos(TAU * k / 12), .2 * math.sin(TAU * k / 12), 0) for k in range(12)], .012, F['steel'], 4, closed=True)
    sk.cylinder(sock, V(0, -.2, 0), V(0, -.32, -.08), .015, F['steel'], 4)

    # ---- far ranges out of the clouds: layered ridgelines (ridged noise along each crest, snow on the heights and the steep
    # faces), each farther range paler toward the dawn haze; an open gap of cloud sea toward the sun so it rises from the clouds ----
    peaks = Geo()
    sun_a = math.atan2(CB_SUN_DIR[1], CB_SUN_DIR[0])
    haze = 0xc8b4c4
    RANGES = (  # (from, to: angles off the sun's azimuth; distance; depth half-width; crest height; haze share; seed)
        (-1.55, -.34, 96.0, 13.0, 44.0, .12, 1), (.34, 1.45, 100.0, 13.0, 48.0, .14, 2),
        (-1.95, -.28, 128.0, 15.0, 58.0, .36, 3), (.28, 1.9, 132.0, 15.0, 56.0, .38, 4),
        (-2.3, -.22, 160.0, 12.0, 70.0, .6, 5), (.22, 2.2, 162.0, 12.0, 72.0, .62, 6))
    base = -26.0
    crest_cache = {}

    def crest(t, k):
        """The crest's height above the base at angle t on range k: ridged noise (sharp peaks, saddles), tapered at the ends."""
        key = (k, round(t, 4))
        if key in crest_cache:
            return crest_cache[key]
        a0, a1, d, w, H, hz, seed = RANGES[k]
        u = (t - sun_a - a0) / (a1 - a0)
        x = (t - sun_a) * d * .055
        r, amp, tot = 0.0, 1.0, 0.0
        for o in range(4):
            nv = noise2(x * (2 ** o), seed * 7.0 + o, 1.0, 60 + seed * 4 + o, octaves=1)
            r += amp * (1 - abs(2 * nv - 1))
            tot += amp
            amp *= .5
        r /= tot
        taper = smoothstep(0, .12, u) * smoothstep(1, .88, u)
        v = H * (.3 + .7 * r ** 1.7) * (.35 + .65 * taper)
        crest_cache[key] = v
        return v
    for k, (a0, a1, d, w, H, hz, seed) in enumerate(RANGES):
        rockk = sk.fin('cb-range-rock%d' % k, int(''.join('%02x' % round(c * 255) for c in mix_hex(0x6c6772, haze, hz)), 16),
                       smooth=.05, dirt=.1)
        snowk = sk.fin('cb-range-snow%d' % k, int(''.join('%02x' % round(c * 255) for c in mix_hex(0xf3f2f6, haze, hz * .8)), 16),
                       smooth=.3, dirt=0)

        def rh(r, t, x, z, k=k, d=d, w=w):
            across = abs(r - d) / w
            y = base + crest(t, k) * max(0.0, 1 - across ** 1.6)
            return y + (noise2(x, z, .09, 80 + k) - .5) * 3.0 * (1 - across)

        def rf(x, y, z, n, k=k, rockk=rockk, snowk=snowk):
            c = crest(math.atan2(z, x), k)
            hgt = y - base
            return snowk if (hgt > c * .66 or (n[1] > .72 and hgt > c * .42)) else rockk
        polar_field(peaks, (0, 0), d - w, d + w, 7 if fine else 3, 120 if fine else 36, rh, rf, smooth=False, rpow=1.0,
                    theta0=sun_a + a0, theta1=sun_a + a1)

    # ---- the cloud sea, and towers of cumulus far off ----
    clouds = Geo()

    def ch(r, t, x, z):
        y = -21.0 + 2.6 * (noise2(x, z, .022, 3) - .5) + 1.3 * (noise2(x, z, .07, 4) - .5) + .5 * (noise2(x, z, .18, 9) - .5)
        return y - 2.5 * (1 - smoothstep(18, 34, r))

    polar_field(clouds, (0, 0), 16, 176, 26 if fine else 10, 80 if fine else 30, ch, lambda x, y, z, n: F['cloud'], smooth=True, rpow=1.65)
    for i, (a, d, h, w) in enumerate(((-1.25, 140, 24, 26), (-2.1, 145, 30, 28), (-.55, 142, 20, 24), (2.4, 140, 16, 26), (-2.75, 130, 15, 20))):
        cx, cz = math.cos(a) * d, math.sin(a) * d
        # a cumulus tower: puffs heaped on a wide base, smaller and higher toward its crown, leaning a little downwind
        puffs = 9 if fine else 4
        for k in range(puffs):
            u = k / (puffs - 1)
            r = w * (.62 - .38 * u) * (.85 + .3 * hash01('cu', i, k))
            ang = TAU * hash01('ca', i, k)
            off = w * .45 * (1 - u) * hash01('co', i, k)
            px, pz = cx + math.cos(ang) * off + u * 4, cz + math.sin(ang) * off
            py = -24 + h * (.15 + .75 * u) * (.9 + .2 * hash01('cy', i, k))
            mound(clouds, V(px, py, pz), (r, r * .78, r), F['cloud'], 300 + i * 20 + k, 6 if fine else 4, 12 if fine else 7, .06, .35)

    # ---- the dawn sky and the sun ----
    sky = Geo()
    dome(sky, 172, F['sky'], 12 if fine else 6, 36 if fine else 16, -16, 90)
    sun = Geo()
    # The sun's face in rings, so its colour can fall from a white-hot core to a gold limb (limb darkening: it reads as a burning
    # body, not a paper disc); the Core script's halo glow softens its edge against the sky.
    rings = (0.0, .3, .55, .74, .87, .95, 1.0) if fine else (0.0, .55, .87, 1.0)
    nseg = 40 if fine else 16
    disc(sun, V(0, 0, 0), 7.2 * rings[1], F['sun'], nseg, normal=(0, 0, 1))
    for r0, r1 in zip(rings[1:], rings[2:]):
        disc(sun, V(0, 0, 0), 7.2 * r1, F['sun'], nseg, normal=(0, 0, 1), r_in=7.2 * r0)

    def sunfn(P):
        import numpy as np
        r = np.clip(np.hypot(P[:, 0], P[:, 1]) / 7.2, 0, 1)
        core = mix_hex(0xfffbf0, 0xffe2a8, np.clip(r / .6, 0, 1))
        limb = mix_hex(0xffe2a8, 0xffa44e, np.clip((r - .6) / .4, 0, 1) ** 1.4)
        return np.where((r < .6)[:, None], core, limb)
    skyfn = sky_fn(0x3d6db3, 0xf3b78e, CB_SUN_DIR, 0xffcf96, .75, below=0xd4c4cc)

    def cloudfn(P):
        # the cloud tops warm and bright toward the sun, lavender in the hollows and away from it (smooth, per corner)
        import numpy as np
        y = P[:, 1]
        up = np.clip((y + 23.0) / 4.5, 0, 1)
        xz = P[:, [0, 2]] / np.maximum(np.linalg.norm(P[:, [0, 2]], axis=1, keepdims=True), 1e-6)
        sun = np.clip(xz @ np.array(CB_SUN_DIR) * .5 + .5, 0, 1)
        shade = mix_hex(0xb9aecb, 0xf2ece8, up)
        warm = mix_hex(0xf2ece8, 0xffd9b8, sun * up)
        return shade * (1 - up[:, None] * .6) + warm * (up[:, None] * .6)
    sun_at = (CB_SUN_DIR[0] * 160, 4.0, CB_SUN_DIR[1] * 160)
    sun_yaw = math.atan2(-CB_SUN_DIR[0], -CB_SUN_DIR[1])
    return [
        Piece('cloudbreak/summit', g, True, (Y, -.62), seed=1),
        Piece('cloudbreak/padlights', lights, False),
        Piece('cloudbreak/beacon', beacon, False),
        Piece('cloudbreak/sock', sock, False, (-50.0,), preview=[M((x, CB_SOCK_TOP + .08, z), yaw=2.4, pitch=1.1) for x, z in CB_SOCKS]),
        Piece('cloudbreak/peaks', peaks, False, (-50.0,)),
        Piece('cloudbreak/clouds', clouds, False, (-50.0,), recolour=({F['cloud']}, cloudfn)),
        Piece('cloudbreak/sky', sky, False, (-500.0,), recolour=({F['sky']}, skyfn)),
        Piece('cloudbreak/sun', sun, False, (-500.0,), recolour=({F['sun']}, sunfn), preview=[M(sun_at, yaw=sun_yaw)]),
    ]


# The preview's views (name, eye, target, fov) in Unity space.
VIEWS = [
    ('cb-hero', (-1.2, 1.0, 3.6), (0, 1.1, 0), 40),
    ('cb-up', (1.4, .4, 2.6), (2.4, 9, -4), 62),
    ('cb-wide', (6, 3.5, 9), (0, 0, -3), 58),
    ('cb-high', (0, 40, 26), (0, -10, -30), 60),
    ('cb-sun', (.8, 1.3, 2.4), (-30, 6, -95), 60),
]
