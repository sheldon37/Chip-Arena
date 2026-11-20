"""Cloudbreak's set: a summit relay station above a sea of cloud at dawn (docs/INTROS-GENERIC.md "Cloudbreak"). Unity space; the pad's
centre is the hero mark (the origin), a flat side of the octagon facing +z (the camera). Pieces (generic_sets.py writes them):
  cloudbreak/summit     the pad, its paint, the rock, the boulders, the mast and its dishes, the hut, the solar array, the railing,
                        the windsock poles, the conduit (world frame)
  cloudbreak/padlights  the pad's eight edge-light lenses (glow; world frame)
  cloudbreak/beacon     the mast's red beacon lens (glow; world frame)
  cloudbreak/sock       one windsock, its mouth at the pivot (the pole top, CB_SOCK_TOP), streaming along +z
  cloudbreak/peaks      the far peaks (world frame)        cloudbreak/clouds   the cloud sea and far cumulus (world frame)
  cloudbreak/sky        the dawn sky dome (glow)           cloudbreak/sun      the sun's disc, facing +z, radius 8 (glow)
"""
import math

import stagekit as sk
from stagekit import Geo, _add, _sub, _norm, _cross
from generic_kit import (PI, TAU, V, mul, lerp, M, place, noise2, polar_field, rockball, mound, hash01, dome, disc, ribbed, lattice_tower, dish,
                         flipped, sky_fn, mix_hex, Piece, smoothstep, ang_diff)

CB_R = 3.9                       # the pad's vertex radius
CB_MAST = (3.9, -.62, -4.9)      # the mast's foot (its shelf)
CB_HUT = (-4.9, -.62, -3.7)
CB_SOCKS = ((3.95, 1.52), (-3.95, 1.52))   # the windsock poles (x, z)
CB_SOCK_TOP = 3.2                # the sock's pivot height (Unity y)
CB_BEACON_Y = 15.72
CB_SUN_DIR = (0.0, -1.0)         # the sun's azimuth (Unity x, z), low behind the pilot (behind its shoulder in the hero frame)


def octo(th):
    a = (th - PI / 8) % (PI / 4) - PI / 8
    return CB_R * math.cos(PI / 8) / math.cos(a)


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
    # ---- the pad: an octagon of concrete, a flat side to the camera ----
    seg = 3 if fine else 1
    nth = 8 * seg
    rhos = (0, .3, .55, .75, .9, 1.0) if fine else (0, .6, 1.0)
    ths = [PI / 8 + TAU * k / nth for k in range(nth)]
    Y = .012

    def pp(rho, th, y=Y):
        r = rho * octo(th)
        return V(math.cos(th) * r, y, math.sin(th) * r)
    for i in range(len(rhos) - 1):
        for k in range(nth):
            t0, t1 = ths[k], ths[(k + 1) % nth]
            if rhos[i] == 0:
                g.face((V(0, Y, 0), pp(rhos[i + 1], t0), pp(rhos[i + 1], t1)), F['concrete'], normal=(0, 1, 0))
            else:
                g.face((pp(rhos[i], t0), pp(rhos[i + 1], t0), pp(rhos[i + 1], t1), pp(rhos[i], t1)), F['concrete'], normal=(0, 1, 0))
    # the skirt: the pad's sides down into the rock, leaning out a little
    for k in range(nth):
        t0, t1 = ths[k], ths[(k + 1) % nth]
        a, b = pp(1, t0), pp(1, t1)
        c, d = pp(1.04, t1, -.75), pp(1.04, t0, -.75)
        out = _norm((math.cos((t0 + t1) / 2), 0, math.sin((t0 + t1) / 2)))
        g.face((a, b, c, d), F['skirt'], normal=out)
    # paint (lifted clear of the slab): the hazard band round the edge, the white ring, three chevrons, the touchdown ring
    lift = .016
    stripes = nth * (2 if fine else 1) * 2
    for k in range(stripes):
        t0 = PI / 8 + TAU * k / stripes
        t1 = PI / 8 + TAU * (k + 1) / stripes
        f = F['yellow'] if k % 2 == 0 else F['black']
        g.face((pp(.905, t0, lift), pp(.985, t0, lift), pp(.985, t1, lift), pp(.905, t1, lift)), f, normal=(0, 1, 0))
    disc(g, V(0, lift, 0), 2.42, F['white'], n=48 if fine else 16, r_in=2.18)
    disc(g, V(0, lift, 0), .62, F['yellow'], n=32 if fine else 12, r_in=.48)
    for k in range(3):
        a = PI / 2 + TAU * k / 3
        tip = V(math.cos(a) * 1.1, lift + .001, math.sin(a) * 1.1)
        tang = (-math.sin(a), 0, math.cos(a))
        for side in (-1, 1):
            outer = _add(_add(tip, (math.cos(a) * .42, 0, math.sin(a) * .42)), tang, side * .5)
            nrm = _norm(_cross(_norm(_sub(outer, tip)), (0, 1, 0)))
            g.face((_add(tip, nrm, -.09), _add(tip, nrm, .09), _add(outer, nrm, .09), _add(outer, nrm, -.09)), F['yellow'], normal=(0, 1, 0))
    # the edge lights' housings (their lenses are cloudbreak/padlights)
    lights = Geo()
    for k in range(8):
        th = PI / 8 + TAU * k / 8
        c = pp(.95, th, Y)
        sk.lathe(g, [(.11, -.01), (.11, .02), (.085, .03)], c, F['steel'], 10 if fine else 6, cap1=True)
        sk.lathe(lights, [(.075, .03), (.068, .055), (.04, .075), (.001, .082)], c, F['padlight'], 10 if fine else 6)

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
    rail = [pp(.975, PI / 8 + TAU * k / 8, Y) for k in range(4, 8)]
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

    # ---- far peaks out of the clouds ----
    peaks = Geo()
    for i, (px, pz, H, Rb) in enumerate(((-60, -108, 46, 52), (40, -112, 58, 50), (105, -62, 38, 45), (-118, -25, 34, 40))):
        base = -24.0

        def ph(r, t, x, z, H=H, Rb=Rb, i=i):
            u = min(1.0, r / Rb)
            y = base + H * (1 - u) ** 1.5
            y += (noise2(x, z, .045, 40 + i) - .5) * H * .35 * (1 - u) * (.3 + u)
            y += math.sin(t * 3 + i) * H * .06 * u * (1 - u) * 4
            return y

        def pf(x, y, z, n, H=H):
            return F['peaksnow'] if (y > base + H * .5 or (n[1] > .62 and y > base + H * .28)) else F['peakrock']
        polar_field(peaks, (px, pz), 0.0, Rb, 10 if fine else 5, 26 if fine else 12, ph, pf, smooth=False, rpow=1.0)

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
    disc(sun, V(0, 0, 0), 7.2, F['sun'], 24 if fine else 12, normal=(0, 0, 1))
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
        Piece('cloudbreak/sun', sun, False, (-500.0,), preview=[M(sun_at, yaw=sun_yaw)]),
    ]


# The preview's views (name, eye, target, fov) in Unity space.
VIEWS = [
    ('cb-hero', (-1.2, 1.0, 3.6), (0, 1.1, 0), 40),
    ('cb-up', (1.4, .4, 2.6), (2.4, 9, -4), 62),
    ('cb-wide', (6, 3.5, 9), (0, 0, -3), 58),
    ('cb-high', (0, 40, 26), (0, -10, -30), 60),
    ('cb-sun', (.8, 1.3, 2.4), (-30, 6, -95), 60),
]
