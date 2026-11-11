"""Breakwater's surroundings (breakwater_art.py runs it): what lies outside the bounds, in named chunks of one tier each
(static/<name>/2): the sea in faceted swell out to the horizon; the lighthouse on its rock past the east hall (its lantern
open, so the rig's optic shows turning inside it); the mole running west to the shore with its lamps, the far pier head and
its green light, the harbour town on its hill and the headlands."""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, lathe, sweep
import breakwater_kit as bk
from breakwater_kit import F, Y, hashf

SEA = -3.4


def sea(L, T):
    """The sea: a fine grid in standing swell round the mole (the rig's swells run over it), flat rings beyond; its facets
    in two tones along the swell's lines. Its extent is square to the map and a little south, so the art's bounds centre on
    the deck beside the Stone (Runtime NativeStageArtDraw stands its reflection probe there)."""
    g = Geo()
    lim, cell = 64.0, 3.2
    n = int(2 * lim / cell)
    P = np.zeros((n + 1, n + 1, 3))
    for i in range(n + 1):
        for j in range(n + 1):
            x, z = -lim + i * cell, -lim - 3 + j * cell
            r = max(abs(x), abs(z + 3))
            a = .22 * max(0.0, 1 - (r / (lim - 6)) ** 4)
            if abs(x) < 23 and -10.5 < z < 9.5:
                a = 0
            P[i, j] = (x, SEA + a * (math.sin(z * .21 + x * .03) * 1.0 + math.sin(x * .37 + z * .11) * .45), z)

    def tone(i, j):
        x, z = -lim + (i + .5) * cell, -lim - 3 + (j + .5) * cell
        return F['sealight'] if math.sin(z * .21 + x * .03 + .6) + (hashf(3, i, j) - .5) * .9 > .55 else F['sea']
    bk.grid_surface(g, P, F['sea'], inside=(0, -500, 0), fin_fn=tone)
    for (lim0, lim1, c) in ((64.0, 208.0, 16.0), (208.0, 900.0, 69.2)):
        e = list(np.arange(-lim1, -lim0 + 1e-6, c)) + list(np.arange(lim0, lim1 + 1e-6, c))
        for i in range(len(e) - 1):
            for j in range(len(e) - 1):
                xa, xb, za, zb = e[i], e[i + 1], e[j], e[j + 1]
                if xb - xa > c + 1 and zb - za > c + 1:
                    continue                    # the middle: the finer ring's
                f = F['sea']
                g.face(((xa, SEA, za - 3), (xb, SEA, za - 3), (xb, SEA, zb - 3), (xa, SEA, zb - 3)), f, Y)
    return g


def lighthouse(L, T):
    g = Geo()
    R = L['rig']
    x, z, foot, gal, lan = R['lightX'], R['lightZ'], R['lightFoot'], R['galleryY'], R['lanternY']
    s = 24
    # the rock it stands on, the stone neck from the hall, the round plinth
    for i in range(26):
        a = i * 2 * math.pi / 26 + hashf(23, i) * .2
        r = 4.6 + hashf(23, i, 1) * 3.4
        sz = 1.3 + hashf(23, i, 2) * 1.6
        px = x + math.cos(a) * r
        if px - sz * 1.3 < 23.2:
            continue
        q = sk.quat_axis(hashf(23, i, 3) - .5, hashf(23, i, 4) - .5, hashf(23, i, 5) - .5, hashf(23, i, 6) * 6)
        bk.rock(g, (px, SEA + .2 + hashf(23, i, 7) * 1.4 * (1 - (r - 4.6) / 3.4), z + math.sin(a) * r), (sz * 1.3, sz * .9, sz), 300 + i, F['armourwet' if r > 6.4 else 'armour'], n=2, square=.6, rough=.25, quat=q)
    lathe(g, [(6.0, SEA - 2), (5.7, SEA + 1.2), (5.3, -.4), (5.2, foot - .5), (4.9, foot - .5), (4.9, foot), (4.3, foot)], (x, 0, z), F['granite'], s, smooth=False)
    box(g, 22.02, x - 4.6, -3.0, 0.0, -2.6, 2.6, F['granite'], T, bevel=.05, skip=('-x',))
    for sz in (-1, 1):
        box(g, 22.02, x - 4.3, 0, .9, sz * 2.6 - .2, sz * 2.6 + .2, F['dressed'], T, bevel=.05, skip=('-x', '-y'))
        box(g, 22.0, x - 4.3, .9, 1.06, sz * 2.6 - .26, sz * 2.6 + .26, F['coping'], T, bevel=.04)
    for k in range(4):
        box(g, x - 5.2 + k * .3, x - 4.0, k * .3, (k + 1) * .3, -1.4, 1.4, F['dressed'], T, bevel=.03, skip=('-y',))
    # the tower: lime-washed, two red bands, a moulded foot; lit windows up its west face and its door
    h = gal - foot
    r0, r1 = 4.1, 2.8

    def rad(y):
        t = (y - foot) / h
        return r0 + (r1 - r0) * (t ** .82)
    lathe(g, [(r0 + .25, foot), (r0 + .25, foot + .5), (r0 + .1, foot + .7), (r0, foot + .9)], (x, 0, z), F['dressed'], s)
    bands = [(foot + .9, 6.6, 'lime'), (6.6, 9.4, 'band'), (9.4, 14.2, 'lime'), (14.2, 17.0, 'band'), (17.0, gal - .8, 'lime')]
    for ya, yb, f in bands:
        ys = np.linspace(ya, yb, 4)
        lathe(g, [(rad(y), y) for y in ys], (x, 0, z), F[f], s)
    for wy in (4.6, 8.0, 11.8, 15.6, 19.2):
        r = rad(wy) + .02
        for a in (math.pi, math.pi * .5, -math.pi * .5):
            if a != math.pi and wy in (4.6, 11.8, 19.2):
                continue
            d = (math.cos(a), math.sin(a))
            p = (-d[1], d[0])
            c = (x + d[0] * r, wy, z + d[1] * r)
            g.face((sk._add(c, (p[0], 0, p[1]), -.26), sk._add(c, (p[0], 0, p[1]), .26), sk._add(sk._add(c, (p[0], 0, p[1]), .26), Y, .95), sk._add(sk._add(c, (p[0], 0, p[1]), -.26), Y, .95)), F['window'], (d[0], 0, d[1]))
            box(g, c[0] - .42 * abs(p[0]) - .08 * abs(d[0]), c[0] + .42 * abs(p[0]) + .08 * abs(d[0]), wy - .12, wy, c[2] - .42 * abs(p[1]) - .08 * abs(d[1]), c[2] + .42 * abs(p[1]) + .08 * abs(d[1]), F['dressed'], T, bevel=.02)
    box(g, x - r0 - .14, x - r0 + .3, foot, foot + 2.3, -.62, .62, F['door'], T, bevel=.03)
    box(g, x - r0 - .22, x - r0 + .3, foot + 2.3, foot + 2.56, -.8, .8, F['dressed'], T, bevel=.04)
    # the gallery: a corbelled deck, its iron rail; the lantern's murette, its glazing bars (open: the optic shows), the dome
    lathe(g, [(rad(gal - .8), gal - .8), (rad(gal - .8) + .1, gal - .6), (3.3, gal - .25), (3.75, gal - .12), (3.75, gal + .06), (2.2, gal + .06)], (x, 0, z), F['dressed'], s)
    for k in range(20):
        a = k * 2 * math.pi / 20
        sk.cylinder(g, (x + math.cos(a) * 3.6, gal + .06, z + math.sin(a) * 3.6), (x + math.cos(a) * 3.6, gal + 1.16, z + math.sin(a) * 3.6), .035, F['ironplain'], 5)
    for yy, t in ((gal + 1.16, .045), (gal + .62, .03)):
        sk.tube(g, [(x + math.cos(2 * math.pi * k / 20) * 3.6, yy, z + math.sin(2 * math.pi * k / 20) * 3.6) for k in range(20)], t, F['ironplain'], 5, closed=True)
    lathe(g, [(2.2, gal + .06), (2.2, gal + 1.0), (2.3, gal + 1.06), (2.3, gal + 1.16), (2.05, gal + 1.2)], (x, 0, z), F['iron'], s)
    lathe(g, [(2.05, gal + 1.2), (.0, gal + 1.2)], (x, 0, z), F['lead'], s)
    top = gal + 3.5
    for k in range(12):
        a = k * 2 * math.pi / 12
        sk.cylinder(g, (x + math.cos(a) * 2.1, gal + 1.16, z + math.sin(a) * 2.1), (x + math.cos(a) * 2.1, top, z + math.sin(a) * 2.1), .05, F['ironplain'], 5)
    for yy in (gal + 1.95, gal + 2.75):
        sk.tube(g, [(x + math.cos(2 * math.pi * k / 24) * 2.1, yy, z + math.sin(2 * math.pi * k / 24) * 2.1) for k in range(24)], .03, F['ironplain'], 4, closed=True)
    # the lamp's pedestal under the optic
    lathe(g, [(.5, gal + 1.2), (.4, gal + 1.3), (.28, lan - .8), (.5, lan - .78)], (x, 0, z), F['brass'], 10)
    lathe(g, [(2.42, top - .1), (2.5, top), (2.5, top + .16), (2.2, top + .3), (1.9, top + .8), (1.3, top + 1.4), (.5, top + 1.78), (.36, top + 1.9), (.4, top + 2.2), (.24, top + 2.34), (.0, top + 2.4)], (x, 0, z), F['copper'], s)
    lathe(g, [(2.2, top - .1), (.0, top + .5)], (x, 0, z), F['deep'], 12)
    sk.cylinder(g, (x, top + 2.3, z), (x, top + 3.7, z), .03, F['ironplain'], 4)
    box(g, x - .5, x + .36, top + 3.2, top + 3.26, z - .01, z + .01, F['brass'], None, bevel=0)
    return g


def house(g, x, y, z, w, d, h, seed, yaw=0.0):
    """A harbour house: its body, a pitched roof, lit windows on the faces toward the sea."""
    t = Geo()
    f = F['house%d' % int(hashf(seed, 1) * 3)]
    box(t, -w / 2, w / 2, 0, h, -d / 2, d / 2, f, None, bevel=0, skip=('-y', '+y'))
    rh = min(w, d) * .42
    along_x = w >= d
    if along_x:
        for sz in (-1, 1):
            t.face(((-w / 2 - .2, h, sz * (d / 2 + .2)), (w / 2 + .2, h, sz * (d / 2 + .2)), (w / 2 + .2, h + rh, 0), (-w / 2 - .2, h + rh, 0)), F['roof'], (0, d / 2, sz * rh))
        for sx in (-1, 1):
            t.face(((sx * w / 2, h, -d / 2), (sx * w / 2, h, d / 2), (sx * w / 2, h + rh, 0)), f, (sx, 0, 0))
    else:
        for sx in (-1, 1):
            t.face(((sx * (w / 2 + .2), h, -d / 2 - .2), (sx * (w / 2 + .2), h, d / 2 + .2), (0, h + rh, d / 2 + .2), (0, h + rh, -d / 2 - .2)), F['roof'], (sx * rh, w / 2, 0))
        for sz in (-1, 1):
            t.face(((-w / 2, h, sz * d / 2), (w / 2, h, sz * d / 2), (0, h + rh, sz * d / 2)), f, (0, 0, sz))
    floors = max(1, int(h / 3.0))
    for fl in range(floors):
        wy = 1.4 + fl * 3.0
        cols = max(1, int(d / 2.6))
        for c in range(cols):
            if hashf(seed, fl, c) < .45:
                continue
            wz = -d / 2 + d * (c + .5) / cols
            t.face(((w / 2 + .03, wy, wz - .5), (w / 2 + .03, wy, wz + .5), (w / 2 + .03, wy + 1.3, wz + .5), (w / 2 + .03, wy + 1.3, wz - .5)), F['windowfar'], (1, 0, 0))
        cols = max(1, int(w / 2.6))
        for c in range(cols):
            for sz in (-1, 1):
                if hashf(seed, fl, c, sz) < .6:
                    continue
                wx = -w / 2 + w * (c + .5) / cols
                t.face(((wx - .5, wy, sz * (d / 2 + .03)), (wx + .5, wy, sz * (d / 2 + .03)), (wx + .5, wy + 1.3, sz * (d / 2 + .03)), (wx - .5, wy + 1.3, sz * (d / 2 + .03))), F['windowfar'], (0, 0, sz))
    g.extend(t.transformed(sk.affine(sk.quat_axis(0, 1, 0, yaw), (x, y, z))))


def shore(L, T):
    g = Geo()
    # ---- the mole running west to the shore: its deck, the wave wall and its parapet, the quay, a lamp every 17 m
    x0, x1 = -196.0, -22.02
    box(g, x0, x1, -7, 0, -5, 5, F['sett'], T, bevel=0, cell=12.0, skip=('-y', '+x', '-x', '+z'))
    box(g, x0, x1, -7, 2.4, 5, 8.4, F['granite'], T, bevel=0, cell=12.0, skip=('-y', '+x', '-x'))
    box(g, x0, x1, 2.4, 3.5, 8.0, 8.4, F['granite'], T, bevel=0, cell=12.0, skip=('-y', '+x'))
    box(g, x0, x1, -7, -2.4, -9.5, -5, F['quay'], T, bevel=0, cell=12.0, skip=('-y', '+x', '-x', '+z'))
    prof = [(2.7, -7.0), (1.75, -3.95), (.8, -1.9), (.44, .7), (.76, 2.12), (.0, 2.36)]
    xs = np.linspace(x0, x1, 9)
    P = np.array([[(x, y, 8.4 + o) for o, y in prof] for x in xs])
    bk.grid_surface(g, P, F['granite'], inside=(-100, -2, 0), fin_fn=lambda i, j: F['armourwet'] if j < 2 else F['granite'])
    x = -34.0
    while x >= -180:
        lathe(g, [(.16, 0), (.07, .4), (.055, 3.3), (.03, 3.4)], (x, 0, -4.7), F['ironplain'], 5)
        sk.cylinder(g, (x, 3.3, -4.7), (x, 3.36, -4.1), .03, F['ironplain'], 4)
        lathe(g, [(.04, 2.62), (.12, 2.66), (.13, 2.8), (.1, 2.94), (.03, 3.0)], (x, 0, -4.1), F['lamp'], 5)
        if int(x) % 2 == 0:
            bk.bollard(g, x + 6, 0, -4.55, sk.Tier(2))
        x -= 17
    for i in range(34):
        xx = -24 - i * 5.1 + hashf(85, i) * 2
        s = .8 + hashf(85, i, 2) * .9
        bk.rock(g, (xx, SEA - .1 + hashf(85, i, 7) * .5, 10.2 + hashf(85, i, 1) * 2.2), (s * 1.3, s * .8, s), 700 + i, F['armourwet'], n=1, square=.7, rough=.22)
    # ---- the far pier head across the harbour mouth, its little light
    box(g, -180, -58, -7, -1.2, -123, -113, F['granite'], T, bevel=0, cell=20.0, skip=('-y',))
    lathe(g, [(2.4, -1.2), (2.2, -.6), (1.5, -.4), (1.25, 5.6), (1.7, 5.9), (1.7, 6.1), (1.0, 6.2)], (-64, 0, -118), F['lime'], 12)
    lathe(g, [(.7, 6.2), (.75, 7.1), (.9, 7.2), (.5, 7.9), (.0, 8.1)], (-64, 0, -118), F['copper'], 10)
    lathe(g, [(.45, 6.4), (.5, 6.9), (.45, 7.1)], (-64, 0, -118), F['greenlamp'], 8)
    # ---- the shore: the harbour front, the town climbing its hill, a church, moored boats, the headlands
    box(g, -210, -194, -7, -.4, -240, 210, F['granite'], T, bevel=0, cell=30.0, skip=('-y', '-x'))
    box(g, -420, -210, -7, -.2, -260, 240, F['hill'], T, bevel=0, cell=60.0, skip=('-y',))
    for i in range(58):
        a = hashf(31, i)
        hx = -216 - hashf(31, i, 1) ** 1.3 * 150
        hz = -215 + a * 400
        rise = (-hx - 205) * .17
        w, d, hh = 7 + hashf(31, i, 2) * 8, 7 + hashf(31, i, 3) * 8, 6 + hashf(31, i, 4) * 8
        house(g, hx, -.4 + rise, hz, w, d, hh, 500 + i, yaw=(hashf(31, i, 5) - .5) * .5)
    # terraces of earth under the houses (so none floats)
    for k in range(6):
        xa = -216 - k * 26
        box(g, xa - 26, xa, -1, (-xa - 205 + 13) * .17 - .4, -235 + k * 9, 205 - k * 12, F['hill'], T, bevel=0, cell=40.0, skip=('-y', '-x'))
    box(g, -274, -266, 10, 34, -44, -36, F['house2'], T, bevel=0, skip=('-y',))
    lathe(g, [(5.2, 34), (3.0, 39), (.0, 52)], (-270, 0, -40), F['roof'], 4, phase=math.pi / 4, smooth=False)
    g.face(((-265.95, 24, -41.2), (-265.95, 24, -38.8), (-265.95, 29, -38.8), (-265.95, 29, -41.2)), F['windowfar'], (1, 0, 0))
    for i in range(5):
        t = Geo()
        bk.hull(t, 7.0, 2.6, 1.2, sk.Tier(2), F['hull' if i % 2 else 'hullwhite'], sheer=.3, y0=-.5)
        box(t, -.7, .7, .4, 1.9, -2.2, -.8, F['hullwhite'], None, bevel=0, skip=('-y',))
        sk.cylinder(t, (0, .4, .4), (0, 4.6, .4), .06, F['timber'], 4)
        g.extend(t.transformed(sk.affine(sk.quat_axis(0, 1, 0, hashf(41, i) * 6), (-160 - hashf(41, i, 1) * 26, SEA, -60 + i * 28 + hashf(41, i, 2) * 8))))
    for i in range(10):
        hz = -520 + i * 118
        s = 60 + hashf(33, i) * 50
        bk.rock(g, (-520 - hashf(33, i, 1) * 90, 4, hz), (s * 1.7, 22 + hashf(33, i, 2) * 30, s * 1.2), 800 + i, F['hill' if i % 2 else 'hillfar'], n=2, square=.3, rough=.3, flat=False)
    for i in range(8):
        hx = -460 + i * 150
        s = 55 + hashf(34, i) * 50
        bk.rock(g, (hx, 0, -640 - hashf(34, i, 1) * 80), (s * 1.8, 16 + hashf(34, i, 2) * 26, s), 830 + i, F['hillfar'], n=2, square=.3, rough=.3, flat=False)
    return g


def items(L, T):
    return [('sea', sea(L, T)), ('light', lighthouse(L, T)), ('shore', shore(L, T))]


def bank_far(sb, L, no_ao):
    T = sk.Tier(1)
    its = items(L, T)
    # far pieces draw 30 m and more off (the depth buffer's step grows with the square of it): their coplanar faces parted wider
    for name, g in its:
        if name != 'sea':
            print('far %-6s coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % ((name,) + sk.lift_coplanar(g, eps=.02, lift=.05)))
    geos = [g for _, g in its]
    aos = [None] * len(geos)
    if not no_ao:
        aos = sk.bake_ao(geos[1:], distance=3.0, samples=12)
        aos = [None] + list(aos)
    for (name, g), ao in zip(its, aos):
        if ao is not None:
            ao = 1.0 - (1.0 - ao) * .7
        bake = sk.Bake(g, ao, floors=(SEA,) if name != 'sea' else (), seed=5, moss_zone=(lambda W: np.clip(sk.smooth(SEA + 1.9, SEA + .5, W[:, 1]), 0, 1)) if name != 'sea' else None)
        print('far %-6s %7d corners' % (name, len(g.p) // 3))
        sb.add('static/%s/2' % name, 'static', sk.to_mesh('static/%s/2' % name, g, bake))
