"""Deepwell's sea floor (rig_art.py builds on it): what stands outside the hull. `near`: the seabed's dunes out to 60 m, rocks
with coral and glowing anemones, the black smokers, the station's legs and their braces, the pipelines leaving the hull, the
lamp buoys moored off the windows (chunks in three tiers). `far`: the plain beyond to 170 m, rock spires on the horizon and the
three far habitats with their lit bands and beacons (named chunks, one tier). Nothing here enters the hull (`inside`, the same
outline as Core NativeDeepwellLooks.Inside), and the smokers and habitats stand where the Core's effects light them
(NativeDeepwellKit.Smoker, NativeDeepwellFx.Far)."""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, lathe, cylinder, tube
import rig_kit as rk
from rig_kit import F, rod, rock, dome, ball, hashf, Y

SEA = rk.SEABED
# Core NativeDeepwellKit.Smoker: place and height
SMOKERS = ((-38, -17, 3.6), (37, 19, 4.4), (-15, 30, 3.2), (17, -31, 4.0), (-44, 24, 4.6))
# Core NativeDeepwellKit.Outside's legs (mirrored in x)
LEGS = ((29.2, 10.2), (29.2, -10.2), (29.2, 0.0), (11.5, 11.5), (11.5, -11.5), (9.2, 22.2), (9.2, -22.2), (25.7, 18.8), (25.7, -18.8), (14.0, 3.8), (14.0, -3.8))
HABITATS = ((-78.0, 52.0, 1.0), (84.0, -40.0, 1.2), (10.0, 96.0, .9))


def inside(x, z, pad=0.0):
    ax, az = abs(x), abs(z)
    return (ax < 10.4 + pad and az < 10.4 + pad) or (17.6 - pad < ax < 28.4 + pad and az < 9.4 + pad) or (ax < 18 and az < 2.9 + pad) or (ax < 8.4 + pad and az < 21.4 + pad) \
        or (20.1 - pad < ax < 24.9 + pad and az < 18 + pad) or (ax < 24.9 + pad and 13.2 - pad < az < 18 + pad)


def height(x, z):
    """The seabed: dunes and a slow swell over the plain, hollowed a little under the station."""
    p = np.array([[x, 0.0, z]])
    h = SEA + .7 * (float(sk.fbm(p, freq=.045, octaves=3, seed=3)[0]) - .5) * 2 + .25 * math.sin(x * .21 + z * .13) + .18 * math.sin(z * .37 - x * .11)
    d = max(abs(x) / 30.0, abs(z) / 23.0)
    if d < 1.4:
        h = h * min(1.0, max(0.0, (d - .9) / .5)) + (SEA - .35) * (1 - min(1.0, max(0.0, (d - .9) / .5)))
    return h


def terrain(g, x0, x1, z0, z1, cell, hole=None):
    """A grid of the seabed with smooth normals; `hole` (x0, x1, z0, z1) leaves the middle to a finer grid."""
    nx, nz = int(round((x1 - x0) / cell)), int(round((z1 - z0) / cell))
    H = [[height(x0 + i * cell, z0 + j * cell) for j in range(nz + 1)] for i in range(nx + 1)]

    def nrm(i, j):
        a = H[min(nx, i + 1)][j] - H[max(0, i - 1)][j]
        b = H[i][min(nz, j + 1)] - H[i][max(0, j - 1)]
        return sk._norm((-a, 2 * cell, -b))
    for i in range(nx):
        for j in range(nz):
            xa, za = x0 + i * cell, z0 + j * cell
            if hole and xa >= hole[0] - 1e-6 and xa + cell <= hole[1] + 1e-6 and za >= hole[2] - 1e-6 and za + cell <= hole[3] + 1e-6:
                continue
            pts = ((xa, H[i][j], za), (xa + cell, H[i + 1][j], za), (xa + cell, H[i + 1][j + 1], za + cell), (xa, H[i][j + 1], za + cell))
            f = F['sand'] if float(sk.vnoise(np.array([[xa + cell / 2, 0.0, za + cell / 2]]), freq=.09, seed=9)[0]) > .42 else F['silt']
            g.face(pts, f, normals=(nrm(i, j), nrm(i + 1, j), nrm(i + 1, j + 1), nrm(i, j + 1)))


def smoker(g, x, z, h, T, seed):
    y = height(x, z) - .3
    top = SEA + h
    sides = (9, 7, 5)[T.i]
    H = top - y
    prof = [(1.5, 0), (1.15, H * .12), (.9, H * .3), (.95, H * .45), (.62, H * .62), (.7, H * .78), (.42, H * .92), (.46, H)]
    lathe(g, prof, (x, y, z), F['rock'], sides, smooth=T.i == 0, phase=hashf(seed, 1) * 3)
    lathe(g, [(.46, H), (.3, H - .02), (.26, H - .5)], (x, y, z), F['vent'], sides)
    if T.i < 2:
        for k in range(3):
            a = hashf(seed, k, 2) * 6.28
            r = 1.2 + hashf(seed, k, 3) * 1.4
            hh = H * (.25 + .3 * hashf(seed, k, 4))
            lathe(g, [(.55, 0), (.4, hh * .5), (.22, hh)], (x + math.sin(a) * r, y, z + math.cos(a) * r), F['rock'], max(5, sides - 2), smooth=False)


def coral(g, x, y, z, s, T, seed):
    kind = int(hashf(seed, 5) * 3)
    if kind == 0:
        dome(g, (x, y, z), s, F['coral'], (8, 6, 4)[T.i], rings=(3, 2, 2)[T.i], squash=.7)
    elif kind == 1:
        n = (5, 4, 3)[T.i]
        for k in range(n):
            a = k * 2.4 + hashf(seed, k) * 2
            lean = .5 + .5 * hashf(seed, k, 1)
            tip = (x + math.sin(a) * s * lean, y + s * (1.0 + .6 * hashf(seed, k, 2)), z + math.cos(a) * s * lean)
            cylinder(g, (x, y - .05, z), tip, s * .14, F['coral2'], (5, 4, 3)[T.i], caps=True, r1=s * .06)
    else:
        # an anemone: a short stalk and a glowing crown
        cylinder(g, (x, y - .05, z), (x, y + s * .5, z), s * .22, F['coral'], (6, 5, 4)[T.i], caps=False)
        dome(g, (x, y + s * .5, z), s * .34, F['coralglow'], (6, 5, 4)[T.i], rings=2, squash=.8)


def near(L, T):
    g = Geo()
    cell = (2.0, 4.0, 6.0)[T.i]
    terrain(g, -60, 60, -60, 60, cell)
    # rocks, the larger with coral on their shoulders
    for i in range(70):
        a, d = hashf(i, 31) * 2 * math.pi, 24 + hashf(i, 32) * 34
        x, z = math.sin(a) * d, math.cos(a) * d
        s = 1.0 + hashf(i, 33) * 2.6
        if inside(x, z, s + 2.5) or abs(x) > 57 or abs(z) > 57:
            continue
        if any(math.hypot(x - sx, z - sz) < 4 for sx, sz, _ in SMOKERS):
            continue
        y = height(x, z)
        hh = s * (.45 + hashf(i, 34) * .7)
        rock(g, (x, y + hh * .35, z), (s, hh, s * (.6 + .5 * hashf(i, 35))), 200 + i, F['rock'] if i % 3 else F['rockpale'], n=(2, 1, 1)[T.i], square=.3, rough=.45,
             quat=sk.quat_axis(.2, 1, .1, hashf(i, 36) * 3))
        if T.i < 2 or i % 2 == 0:
            for k in range(2 if s > 2 else 1):
                ca = hashf(i, 37, k) * 6.28
                coral(g, x + math.sin(ca) * s * .45, y + hh * (.8 + .25 * hashf(i, 38, k)), z + math.cos(ca) * s * .4, .35 + .3 * hashf(i, 39, k), T, i * 7 + k)
    for k, (x, z, h) in enumerate(SMOKERS):
        smoker(g, x, z, h, T, k)
    # the station's legs: a column and a footing each, braced in pairs
    sides = (10, 7, 5)[T.i]
    for (lx, lz) in LEGS:
        for sx in (-1, 1):
            x = sx * lx
            y = height(x, lz) - .4
            cylinder(g, (x, y, lz), (x, -1.05, lz), .42, F['hull'], sides, caps=False)
            lathe(g, [(1.0, 0), (1.0, .35), (.6, .6), (.44, .7)], (x, y, lz), F['dark'], sides)
            lathe(g, [(.44, -1.5), (.56, -1.42), (.56, -1.1), (.5, -1.05)], (x, 0, lz), F['steel'], sides)
    if T.i < 2:
        for (a, b) in (((29.2, 10.2), (29.2, 0.0)), ((29.2, 0.0), (29.2, -10.2)), ((9.2, 22.2), (25.7, 18.8)), ((9.2, -22.2), (25.7, -18.8)), ((11.5, 11.5), (14.0, 3.8)), ((11.5, -11.5), (14.0, -3.8))):
            for sx in (-1, 1):
                rod(g, (sx * a[0], -2.2, a[1]), (sx * b[0], -7.4, b[1]), .12, F['hull'], (6, 5, 4)[T.i], caps=False)
                rod(g, (sx * a[0], -7.4, a[1]), (sx * b[0], -2.2, b[1]), .12, F['hull'], (6, 5, 4)[T.i], caps=False)
    # the pipelines: from under each wing out over the plain toward a habitat
    for (x0, z0, x1, z1) in ((-30.0, 6.0, -59.0, 38.0), (30.0, -6.0, 59.0, -27.0)):
        n = 12
        path = []
        for k in range(n + 1):
            t = k / n
            x, z = x0 + (x1 - x0) * t, z0 + (z1 - z0) * t
            path.append((x, height(x, z) + .55 + .12 * math.sin(t * 9), z))
        tube(g, path, .34, F['hull'], (8, 6, 4)[T.i], caps=True)
        for k in range(1, n, 2):
            p = path[k]
            box(g, p[0] - .5, p[0] + .5, p[1] - .9, p[1] - .2, p[2] - .5, p[2] + .5, F['dark'], T, .04, cell=0)
            if T.detail:
                lathe(g, [(.34, -.08), (.4, -.06), (.4, .06), (.34, .08)], p, F['brass'], (8, 6, 4)[T.i], axis=sk._norm(sk._sub(path[k + 1], path[k - 1])))
    # lamp buoys moored off the great windows: a chain, a float, a warm lamp
    for k, (x, z, y) in enumerate(((-33.5, -5.5, 3.2), (-33.0, 6.0, 1.4), (33.5, 5.5, 3.2), (33.0, -6.0, 1.4), (-5.5, 26.0, 2.6), (6.0, 25.5, .8), (5.5, -26.0, 2.6), (-6.0, -25.5, .8),
                                    (14.5, 8.6, 4.4), (-14.5, -8.6, 4.4), (-14.5, 8.6, 1.2), (14.5, -8.6, 1.2))):
        if inside(x, z, .8):
            continue
        y0 = height(x, z)
        rod(g, (x, y0, z), (x + .3 * math.sin(k), y - .3, z + .3 * math.cos(k)), .025, F['dark'], 3, caps=False)
        bx, bz = x + .3 * math.sin(k), z + .3 * math.cos(k)
        lathe(g, [(.001, -.32), (.2, -.2), (.26, 0), (.2, .16), (.08, .24)], (bx, y, bz), F['yellow'], (8, 6, 5)[T.i])
        lathe(g, [(.08, .24), (.1, .3), (.08, .4), (.001, .44)], (bx, y, bz), F['lamp'], (6, 5, 4)[T.i])
        box(g, x - .3, x + .3, y0 - .1, y0 + .25, z - .3, z + .3, F['dark'], T, .04, cell=0)
    return g


def habitat(g, x, z, s, T, seed):
    """A far habitat: a ribbed dome on a drum with a lit band of windows, a mast with its beacon, a lock tube."""
    y = SEA + 2
    sides = 14
    lathe(g, [(7.4 * s, -3), (7.4 * s, 0), (7.0 * s, .3), (7.0 * s, 3.6 * s)], (x, y, z), F['hull'], sides)
    lathe(g, [(7.02 * s, 1.9 * s), (7.08 * s, 2.0 * s), (7.08 * s, 2.6 * s), (7.02 * s, 2.7 * s)], (x, y, z), F['lamp'], sides)
    dome(g, (x, y + 3.6 * s, z), 7.0 * s, F['steel'], sides, rings=4, squash=.7)
    for k in range(sides // 2):
        a = k * 4 * math.pi / sides
        tube(g, [(x + math.sin(a) * 7.05 * s * math.cos(f), y + 3.6 * s + 4.95 * s * math.sin(f), z + math.cos(a) * 7.05 * s * math.cos(f)) for f in [j * math.pi / 2 / 4 for j in range(5)]], .14, F['frame'], 3, caps=False)
    rod(g, (x, y + 8.4 * s, z), (x, SEA + 16 * s, z), .3, F['dark'], 5)
    ball(g, (x, SEA + 16 * s + .4, z), .5, F['red'], 6, 3)
    a = hashf(seed, 3) * 6.28
    cylinder(g, (x + math.sin(a) * 6.5 * s, y + 1.2, z + math.cos(a) * 6.5 * s), (x + math.sin(a) * 15 * s, y + 1.0, z + math.cos(a) * 15 * s), 1.5 * s, F['hull'], 8)
    dome(g, (x + math.sin(a) * 18 * s, y - 1.5, z + math.cos(a) * 18 * s), 3.6 * s, F['steel'], 8, rings=3, squash=.8)


def far(L, T):
    out = []
    g = Geo()
    terrain(g, -168, 168, -168, 168, 12.0, hole=(-60, 60, -60, 60))
    out.append(('far-plain', g))
    g = Geo()
    for i in range(46):
        a, d = hashf(i, 41) * 2 * math.pi, 64 + hashf(i, 42) * 90
        x, z = math.sin(a) * d, math.cos(a) * d
        if any(math.hypot(x - hx, z - hz) < 24 for hx, hz, _ in HABITATS):
            continue
        s = 3 + hashf(i, 43) * 7
        hh = s * (.6 + .9 * hashf(i, 44))
        rock(g, (x, height(x, z) + hh * .4, z), (s, hh, s * (.6 + .5 * hashf(i, 45))), 400 + i, F['rock'], n=1, square=.25, rough=.5, quat=sk.quat_axis(.15, 1, .1, hashf(i, 46) * 3))
    out.append(('far-rocks', g))
    g = Geo()
    for k, (x, z, s) in enumerate(HABITATS):
        habitat(g, x, z, s, T, k)
    out.append(('far-habitats', g))
    return out


def bank_far(sb, L, no_ao):
    T = sk.Tier(2)
    for name, g in far(L, T):
        if g.faces == 0:
            continue
        sk.lift_coplanar(g, eps=.02, lift=.05)
        bk = sk.Bake(g, None, floors=(), seed=5)
        sb.add('static/%s/2' % name, 'static', sk.to_mesh('static/%s/2' % name, g, bk))
