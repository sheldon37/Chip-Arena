"""The elemental weapon intros' sets (docs/INTROS-ELEMENTAL.md): four modelled backdrops for Forgeborn (fire), Still Tide
(water), Strike Point (lightning) and Black Sun (darkness), written into one local bank, ChipArenaIntroElemental (never
committed; without it the intros draw the framework's dark lit void). Run inside Blender 5.x (bpy, numpy) with the stage kit:

  blender --background --factory-startup --python tools/intros/elemental_set.py -- --out DIR
      [--sets fire,water,lightning,darkness] [--no-ao] [--preview DIR] [--stats]

Modelled in the intros' SOURCE frame (Core NativeIntroElemental: metres, +y up, +x the rig's left, the pilot's hero mark at the
origin, ending facing Facing = -0.22 rad, its forward (sin F, 0, cos F)), so the numbers here are the Core scripts' own; the
export mirrors x into the bank's Unity space (docs/INTROS-V2.md section 3: bank meshes are drawn as authored in Unity space and
`Piece` mirrors its pose). The floor the pilot stands on is FLOOR (Core NativeEntranceStage.FloorY).

Props (docs/INTROS-V2.md: `<prop>` the Fine tier, `<prop>/low` the Low tier): each set's scenery in a few pieces drawn where they
are, and the parts Core poses or lights on their own (the lava rings and rivers, the forge's seams and hearth, the water's surface,
its liquid and frozen walls and the frost's rings, the spires' seams and the crater, the monoliths' glyphs, the moon). The Core
scripts keep the same numbers (the vent, the rings, the rivers, the hall, the spires, the monoliths): Tests/IntroElementalChecks.cs
reads them from this file. Finishes and the bank format are the stage kit's (tools/stages/stagekit.py, tools/pack_bank/bank.py).
"""
import argparse
import math
import os
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'stages'))
import stagekit as sk  # noqa: E402
from stagekit import Geo  # noqa: E402

BANK = 'ChipArenaIntroElemental'
FLOOR = .012
SETS = ('fire', 'water', 'lightning', 'darkness')


# ---------------------------------------------------------------------------------------------------------------------------
# Small helpers
# ---------------------------------------------------------------------------------------------------------------------------
def H(a, b=0, c=0, d=0):
    """A stable 0..1 hash."""
    h = (int(a) * 73856093) ^ (int(b) * 19349663) ^ (int(c) * 83492791) ^ (int(d) * 2654435761)
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


def S(a, b=0, c=0, d=0):
    return H(a, b, c, d) * 2 - 1


def noise(P, freq, seed, octaves=3):
    return sk.fbm(np.asarray(P, dtype=np.float64).reshape(-1, 3), freq, octaves, seed) - .5


def n1(x, y, z, freq, seed, octaves=3):
    return float(noise(np.array([[x, y, z]]), freq, seed, octaves)[0])


def surf(g, P, f, wrap_i=False, wrap_j=False, facing=None, inside=None, flat=False, pick=None):
    """A grid of points P (ni, nj, 3) as a skin (tools/stages/polar_kit.py's): normals from the grid, turned to agree with
    `facing` or to point away from `inside`; flat: each face its own normal; pick(normal, centre) -> a finish per face."""
    P = np.asarray(P, dtype=np.float64)
    ni, nj = P.shape[:2]

    def d(axis, wrap):
        a = np.roll(P, -1, axis) - np.roll(P, 1, axis)
        if not wrap:
            if axis == 0:
                a[0] = P[1] - P[0]
                a[-1] = P[-1] - P[-2]
            else:
                a[:, 0] = P[:, 1] - P[:, 0]
                a[:, -1] = P[:, -1] - P[:, -2]
        return a
    N = np.cross(d(0, wrap_i), d(1, wrap_j))
    L = np.linalg.norm(N, axis=2, keepdims=True)
    N = np.where(L > 1e-9, N / np.maximum(L, 1e-9), np.array([0.0, 1.0, 0.0]))
    if inside is not None:
        s = np.sum(N * (P - np.asarray(inside, dtype=np.float64)), axis=2).sum()
    else:
        s = np.sum(N * np.asarray(facing if facing is not None else (0, 1, 0), dtype=np.float64), axis=2).sum()
    if s < 0:
        N = -N
    ci = ni if wrap_i else ni - 1
    cj = nj if wrap_j else nj - 1
    for i in range(ci):
        i1 = (i + 1) % ni
        for j in range(cj):
            j1 = (j + 1) % nj
            a, b, c, e = P[i, j], P[i1, j], P[i1, j1], P[i, j1]
            na, nb, nc, ne = N[i, j], N[i1, j], N[i1, j1], N[i, j1]
            hint = na + nb + nc + ne
            fn = np.cross(c - a, e - b)
            ln = np.linalg.norm(fn)
            if ln < 1e-12:
                continue
            fn = fn / ln
            if np.dot(fn, hint) < 0:
                fn = -fn
            ff = f if pick is None else pick(fn, (a + b + c + e) * .25)
            pts = (tuple(a), tuple(b), tuple(c), tuple(e))
            if flat:
                g.face(pts, ff, normal=tuple(fn))
            else:
                g.face(pts, ff, normals=(tuple(na), tuple(nb), tuple(nc), tuple(ne)))


def prism(g, outline, y0, y1, f, f_top=None, top_tilt=(0.0, 0.0), chamfer=0.0, bottom=False, f_side=None):
    """An upright prism over a convex outline [(x, z)] anticlockwise seen from above: flat sides, its top tilted by
    (dy/dx, dy/dz) about the outline's centre, a chamfer ring round the top (m)."""
    n = len(outline)
    cx = sum(p[0] for p in outline) / n
    cz = sum(p[1] for p in outline) / n

    def top_y(x, z):
        return y1 + (x - cx) * top_tilt[0] + (z - cz) * top_tilt[1]
    fs = f if f_side is None else f_side
    ft = f if f_top is None else f_top
    if chamfer > 0:
        inner = [(cx + (x - cx) * (1 - chamfer / max(.05, math.hypot(x - cx, z - cz))), cz + (z - cz) * (1 - chamfer / max(.05, math.hypot(x - cx, z - cz)))) for x, z in outline]
    for k in range(n):
        a, b = outline[k], outline[(k + 1) % n]
        ya, yb = top_y(*a), top_y(*b)
        yc = min(ya, yb) - chamfer
        out = (-(b[1] - a[1]), 0.0, b[0] - a[0])
        if chamfer > 0:
            g.face(((a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], yb - chamfer, b[1]), (a[0], ya - chamfer, a[1])), fs, normal=out)
            ia, ib = inner[k], inner[(k + 1) % n]
            nm = sk._norm((out[0], .9 * math.hypot(out[0], out[2]), out[2]))
            g.face(((a[0], ya - chamfer, a[1]), (b[0], yb - chamfer, b[1]), (ib[0], top_y(*ib), ib[1]), (ia[0], top_y(*ia), ia[1])), fs, normal=nm)
        else:
            g.face(((a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], yb, b[1]), (a[0], ya, a[1])), fs, normal=out)
    tops = inner if chamfer > 0 else outline
    g.face([(x, top_y(x, z), z) for x, z in tops], ft, normal=(-top_tilt[0], 1.0, -top_tilt[1]))
    if bottom:
        g.face([(x, y0, z) for x, z in outline[::-1]], fs, normal=(0, -1, 0))


def shrink(poly, d):
    """A convex polygon pulled in toward its centroid by d metres (approximately, per corner)."""
    n = len(poly)
    cx = sum(p[0] for p in poly) / n
    cz = sum(p[1] for p in poly) / n
    out = []
    for x, z in poly:
        r = math.hypot(x - cx, z - cz)
        k = max(0.0, 1 - d / max(r, 1e-6))
        out.append((cx + (x - cx) * k, cz + (z - cz) * k))
    return out


def ccw(poly):
    a = 0.0
    for i in range(len(poly)):
        x0, z0 = poly[i]
        x1, z1 = poly[(i + 1) % len(poly)]
        a += x0 * z1 - x1 * z0
    # anticlockwise seen from above (+y) in x-z means the x->z cross is negative (x right, z toward the viewer)
    return poly if a < 0 else poly[::-1]


def hex_cells(radius, s, seed, centre=(0.0, 0.0), jitter=.28):
    """A jittered hexagonal tiling over a disc: each cell's corners shared with its neighbours (jittered by their place), so the
    cells tile exactly. Returns [(centre (x, z), polygon [(x, z)] anticlockwise from above)]."""
    cells = []
    R = int(radius / (s * 1.5)) + 2
    cache = {}

    def corner(x, z):
        key = (round(x * 1000), round(z * 1000))
        if key not in cache:
            cache[key] = (x + S(seed, key[0], key[1], 1) * jitter * s, z + S(seed, key[0], key[1], 2) * jitter * s)
        return cache[key]
    for r in range(-R, R + 1):
        for q in range(-R - abs(r), R + abs(r) + 1):
            x = centre[0] + s * math.sqrt(3) * (q + r / 2)
            z = centre[1] + s * 1.5 * r
            if math.hypot(x - centre[0], z - centre[1]) > radius:
                continue
            poly = [corner(x + s * math.cos(math.radians(60 * k + 30)), z + s * math.sin(math.radians(60 * k + 30))) for k in range(6)]
            cells.append(((x, z), ccw(poly)))
    return cells


def rock(g, centre, radius, f, T, seed, squash=1.0, rough=.22, flat=True, sub=None):
    """A faceted boulder: a subdivided cube pushed onto a noisy ellipsoid."""
    n = sub if sub is not None else (3, 2, 2)[T.i]
    cx, cy, cz = centre
    for axis in range(3):
        for sgn in (-1, 1):
            P = np.zeros((n + 1, n + 1, 3))
            for i in range(n + 1):
                for j in range(n + 1):
                    u, v = -1 + 2 * i / n, -1 + 2 * j / n
                    p = [0.0, 0.0, 0.0]
                    p[axis] = sgn
                    p[(axis + 1) % 3] = u
                    p[(axis + 2) % 3] = v
                    d = np.array(p) / np.linalg.norm(p)
                    k = 1 + rough * 2 * n1(d[0] * 1.7, d[1] * 1.7, d[2] * 1.7, 1.0, seed, 2)
                    P[i, j] = (cx + d[0] * radius * k, cy + d[1] * radius * k * squash, cz + d[2] * radius * k)
            surf(g, P, f, inside=centre, flat=flat)


def qmul(a, b):
    """The Unity quaternion a * b (x, y, z, w): b first, then a."""
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx, aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def ring_band(g, y, r0, r1, f, sides, normal=(0, 1, 0), centre=(0.0, 0.0)):
    """A flat annulus (r0 inner, r1 outer) at height y."""
    cx, cz = centre
    for k in range(sides):
        a0, a1 = 2 * math.pi * k / sides, 2 * math.pi * (k + 1) / sides
        pts = [(cx + math.cos(a0) * r1, y, cz + math.sin(a0) * r1), (cx + math.cos(a1) * r1, y, cz + math.sin(a1) * r1)]
        if r0 > 1e-4:
            pts += [(cx + math.cos(a1) * r0, y, cz + math.sin(a1) * r0), (cx + math.cos(a0) * r0, y, cz + math.sin(a0) * r0)]
        else:
            pts += [(cx, y, cz)]
        g.face(pts, f, normal=normal)


def disc_sector(g, y, r0, r1, a0, a1, f, steps, centre=(0.0, 0.0), normal=(0, 1, 0)):
    cx, cz = centre
    for k in range(steps):
        b0, b1 = a0 + (a1 - a0) * k / steps, a0 + (a1 - a0) * (k + 1) / steps
        g.face([(cx + math.cos(b0) * r1, y, cz + math.sin(b0) * r1), (cx + math.cos(b1) * r1, y, cz + math.sin(b1) * r1),
                (cx + math.cos(b1) * r0, y, cz + math.sin(b1) * r0), (cx + math.cos(b0) * r0, y, cz + math.sin(b0) * r0)], f, normal=normal)


# ---------------------------------------------------------------------------------------------------------------------------
# FIRE: the caldera forge
# ---------------------------------------------------------------------------------------------------------------------------
# Core NativeElementalIntros.Forgeborn keeps these: the vent, the lava rings' radii and the rivers.
FIRE_VENT = (0.2837, -1.2687)  # 1.3 m behind the hero mark on the hero facing's line (Facing -.22)
FIRE_DAIS_R = 3.45                   # the raised dais's radius round the vent (its top is FLOOR)
FIRE_FORGE_Z = -11.5                 # the forge arch's centre line
FIRE_FLOOR_Y = -.1                   # the caldera's plates around the dais
FIRE_RINGS = (6.5, 9.5, 13.0, 17.5, 25.0)  # the lava sheet's rings round the origin (rig/lava/k+1; lava/0 is under the dais)
FIRE_RIVERS = (-2.45, -1.0, .55, 2.2)  # the four rivers' bearings (radians from +x toward +z), meandering inward


def fire_river(k, r):
    """River k's centre at radius r (x, z): its bearing wanders with the radius."""
    a = FIRE_RIVERS[k] + .22 * math.sin(r * .31 + k * 1.7) + .08 * math.sin(r * .83 + k)
    return math.cos(a) * r, math.sin(a) * r


def fire_river_half(k, r):
    return .55 + .5 * (r - 6) / 20 + .12 * math.sin(r * .7 + k)


def fire_finishes():
    F = {}
    F['basalt'] = sk.fin('fire-basalt', 0x221c1b, sk.DETAIL_CARVED, smooth=.25, dirt=.6, wear=.3)
    F['basalt-top'] = sk.fin('fire-basalt-top', 0x2f2826, sk.DETAIL_CARVED, smooth=.15, dirt=.8, wear=.2)
    F['ash'] = sk.fin('fire-ash', 0x4a403b, sk.DETAIL_PLASTER, smooth=.05, dirt=.5)
    F['crust'] = sk.fin('fire-crust', 0x140f0e, sk.DETAIL_CARVED, smooth=.35, dirt=.3)
    F['scorch'] = sk.fin('fire-scorch', 0x3a2a22, sk.DETAIL_CARVED, smooth=.2, dirt=.8)
    F['column'] = sk.fin('fire-column', 0x1a1717, sk.DETAIL_CARVED, smooth=.4, dirt=.4, wear=.45)
    F['rim'] = sk.fin('fire-rim', 0x241c19, sk.DETAIL_CARVED, smooth=.12, dirt=1.0)
    F['rim-high'] = sk.fin('fire-rim-high', 0x34261f, sk.DETAIL_CARVED, smooth=.1, dirt=1.0)
    F['forge'] = sk.fin('fire-forge', 0x3b302a, sk.DETAIL_ASHLAR, smooth=.18, dirt=1.0, wear=.5)
    F['forge-dark'] = sk.fin('fire-forge-dark', 0x1e1714, sk.DETAIL_ASHLAR, smooth=.18, dirt=1.0)
    F['iron'] = sk.fin('fire-iron', 0x2a2522, sk.DETAIL_IRON, smooth=.45, metal=.8, dirt=.7, wear=.4)
    F['lava'] = sk.fin('fire-lava', 0xff6a1e, sk.DETAIL_GLOW, glow=2.6)
    F['lava-hot'] = sk.fin('fire-lava-hot', 0xffb040, sk.DETAIL_GLOW, glow=3.4)
    F['lava-deep'] = sk.fin('fire-lava-deep', 0xd83410, sk.DETAIL_GLOW, glow=1.9)
    F['rune'] = sk.fin('fire-rune', 0xff8a30, sk.DETAIL_GLOW, glow=2.2)
    return F


def fire_set(T):
    F = fire_finishes()
    near, mid, far = Geo(), Geo(), Geo()
    rigs = {}
    vx, vz = FIRE_VENT
    dr = FIRE_DAIS_R

    def in_river(x, z, pad):
        r = math.hypot(x, z)
        if r < 5.5:
            return False
        for k in range(len(FIRE_RIVERS)):
            cx, cz = fire_river(k, r)
            if math.hypot(x - cx, z - cz) < fire_river_half(k, r) + pad:
                return True
        return False

    def wedge(r0, r1, a0, a1, arc):
        pts = [(vx + math.cos(a0) * r0, vz + math.sin(a0) * r0)]
        pts += [(vx + math.cos(a0 + (a1 - a0) * i / arc) * r1, vz + math.sin(a0 + (a1 - a0) * i / arc) * r1) for i in range(arc + 1)]
        pts += [(vx + math.cos(a1) * r0, vz + math.sin(a1) * r0)]
        return ccw(pts)
    # ---- the dais: rings of fitted wedge stones round the vent; their joints show the lava under them ----
    arc = (3, 2, 1)[T.i]
    for ring, (r0, r1, n, y0) in enumerate(((1.16, 2.06, 10, FLOOR), (2.1, 3.0, 16, FLOOR), (3.04, dr, 26, FLOOR - .05))):
        for k in range(n):
            gap = .022 if ring < 2 else .03
            a0 = 2 * math.pi * k / n + gap / r1 + ring * .2
            a1 = 2 * math.pi * (k + 1) / n - gap / r1 + ring * .2
            y = y0 + .006 * S(ring, k, 1)
            prism(near, wedge(r0, r1, a0, a1, arc), -.34, y, F['basalt'] if ring < 2 else F['crust'], F['basalt-top'],
                  (S(ring, k, 2) * .006, S(ring, k, 3) * .006), chamfer=.022 if T.bevel else 0)
    # the vent: a raised basalt lip, its throat going down into the fire
    sides = (28, 18, 12)[T.i]
    sk.lathe(near, [(1.12, -.3), (1.12, FLOOR - .005), (1.02, FLOOR + .07), (.88, FLOOR + .1), (.78, FLOOR + .05), (.72, -.25), (.64, -.9)],
             (vx, 0, vz), F['crust'], sides)
    throat = Geo()
    sk.lathe(throat, [(.64, -.9), (.52, -1.7)], (vx, 0, vz), F['lava-hot'], sides, cap1=True)
    rigs['vent'] = throat
    # ---- the caldera floor: the tops of basalt columns, lava in the cracks between them ----
    for region, (rad0, rad1, s, seed) in enumerate(((0, 12.5, 1.0, 23), (12.0, 24.5, 1.9, 29))):
        for i, ((cx, cz), poly) in enumerate(hex_cells(rad1 + s, s, seed, jitter=.36)):
            r = math.hypot(cx, cz)
            if r < rad0 or r >= rad1:
                continue
            if math.hypot(cx - vx, cz - vz) < dr + s * .35:
                continue
            if in_river(cx, cz, .2):
                continue
            # cracks open wider out from the dais; a few columns stand proud or sunk
            gap = .035 + .03 * min(1, r / 14) + .025 * H(seed, i, 9)
            p = shrink(poly, gap)
            u = H(seed, i, 11)
            lift = .025 * S(seed, i, 10) + (.12 * H(seed, i, 12) if u > .9 else (-.05 if u < .06 else 0))
            tilt = (S(seed, i, 13) * .03, S(seed, i, 14) * .03)
            g = near if r < 8.5 else (mid if r < 16 else far)
            fin_top = F['ash'] if H(seed, i, 15) > .75 else (F['scorch'] if r < 6 and H(seed, i, 16) > .5 else F['basalt-top'])
            prism(g, p, FIRE_FLOOR_Y - .26, FIRE_FLOOR_Y + lift, F['basalt'], fin_top, tilt, chamfer=.02 if (T.bevel and r < 7.5) else 0)
    # the lava under the cracks: rings Core lights in turn (rig/lava/k); the first is under the dais
    sides = (64, 40, 24)[T.i]
    lg = Geo()
    ring_band(lg, -.08, 0, dr - .1, F['lava-hot'], sides, centre=(vx, vz))
    rigs['lava/0'] = lg
    r_prev = 0.0
    for k, r in enumerate(FIRE_RINGS):
        lg = Geo()
        ring_band(lg, FIRE_FLOOR_Y - .17, r_prev, r, F['lava'], sides)
        rigs['lava/%d' % (k + 1)] = lg
        r_prev = r
    # ---- the rivers: channels of lava from the rim's falls to the floor, crust floes on them, crusted banks ----
    steps = (56, 32, 18)[T.i]
    for k in range(len(FIRE_RIVERS)):
        rg = Geo()
        pts_l, pts_r, mids = [], [], []
        for s in range(steps + 1):
            r = 6.0 + (25.0 - 6.0) * s / steps
            cx, cz = fire_river(k, r)
            nx, nz = fire_river(k, r + .2)
            tx, tz = nx - cx, nz - cz
            tl = math.hypot(tx, tz)
            tx, tz = tx / tl, tz / tl
            hw = fire_river_half(k, r)
            sx, sz = -tz, tx
            mids.append((cx, cz))
            pts_l.append((cx + sx * hw, cz + sz * hw))
            pts_r.append((cx - sx * hw, cz - sz * hw))
        y = FIRE_FLOOR_Y - .12
        for s in range(steps):
            a, b, c, d = pts_l[s], pts_l[s + 1], pts_r[s + 1], pts_r[s]
            rg.face([(a[0], y, a[1]), (b[0], y, b[1]), (c[0], y, c[1]), (d[0], y, d[1])], F['lava'], normal=(0, 1, 0))
        rigs['river/%d' % k] = rg
        for s in range(2, steps - 1, 2 if T.i == 0 else 3):
            if H(k, s, 1) < .45:
                continue
            cx, cz = mids[s]
            hw = fire_river_half(k, 6.0 + 19.0 * s / steps)
            ox, oz = S(k, s, 2) * hw * .45, S(k, s, 3) * hw * .45
            rr = .16 + .28 * H(k, s, 4)
            m = 5 if T.i < 2 else 4
            poly = ccw([(cx + ox + math.cos(2 * math.pi * j / m + H(k, s, 5)) * rr * (.75 + .4 * H(k, s, j)), cz + oz + math.sin(2 * math.pi * j / m + H(k, s, 5)) * rr * (.75 + .4 * H(k, s, j + 9))) for j in range(m)])
            prism(mid, poly, y - .05, y + .03 + .02 * H(k, s, 6), F['crust'], F['crust'])
        section = [(-.3, -.14), (.3, -.14), (.2, .06), (-.2, .06)]
        for pts in (pts_l, pts_r):
            path = [(p[0], FIRE_FLOOR_Y - .06, p[1]) for p in pts]
            sk.sweep(mid, section, path[::2], F['crust'], smooth_u=True, caps=False)
    # ---- basalt column clusters (organ pipes), the tallest behind the dais ----
    clusters = [(-7.0, -10.0, 2.3, 7.5), (8.0, -9.0, 2.0, 6.0), (-12.5, -3.0, 1.8, 4.6), (12.5, 1.0, 1.6, 3.8), (-10.0, 8.5, 1.6, 3.0), (7.5, 12.5, 1.5, 2.6)]
    for ci, (cx, cz, spread, top) in enumerate(clusters):
        g = mid if math.hypot(cx, cz) > 9 else near
        for (px, pz), poly in hex_cells(spread, .42, 100 + ci, centre=(cx, cz), jitter=.0):
            d = math.hypot(px - cx, pz - cz) / spread
            if d > 1 or H(ci, int(px * 10), int(pz * 10)) < .12:
                continue
            h = top * (1 - .75 * d) * (.7 + .45 * H(ci, int(px * 7), int(pz * 7), 2))
            if h < .3:
                continue
            tilt = (S(ci, int(px * 9), 1) * .14, S(ci, int(pz * 9), 2) * .14)
            prism(g, shrink(poly, .03), FIRE_FLOOR_Y - .3, FIRE_FLOOR_Y + h, F['column'], F['basalt-top'], tilt, chamfer=.04 if (T.i == 0 and h > 4.5) else 0)
    # broken column drums lying about
    hexo = ccw([(math.cos(math.radians(60 * k)) * .4, math.sin(math.radians(60 * k)) * .4) for k in range(6)])
    for i in range(10 if T.i == 0 else 6):
        a = H(i, 40) * 2 * math.pi
        r = 6 + 9 * H(i, 41)
        x, z = math.cos(a) * r, math.sin(a) * r
        if in_river(x, z, .6) or math.hypot(x - vx, z - vz) < dr + .9:
            continue
        L = .8 + 1.4 * H(i, 42)
        t = Geo()
        prism(t, hexo, -L / 2, L / 2, F['column'], F['basalt-top'], chamfer=.03 if T.bevel else 0, bottom=True)
        qx = qmul(sk.quat_axis(0, 1, 0, H(i, 43) * math.pi), sk.quat_axis(1, 0, 0, math.pi / 2))
        (near if r < 9 else mid).extend(t.transformed(sk.affine(qx, (x, FIRE_FLOOR_Y + .28, z))))
    # boulders
    for i in range((16, 10, 8)[T.i]):
        a = H(i, 50) * 2 * math.pi
        r = 8 + 14 * H(i, 51)
        x, z = math.cos(a) * r, math.sin(a) * r
        if in_river(x, z, .9):
            continue
        rad = .35 + .9 * H(i, 52) ** 2
        rock(mid if r < 16 else far, (x, FIRE_FLOOR_Y + rad * .25, z), rad, F['rim'], T, 200 + i, squash=.7)
    # ---- the forge: a half-ruined arch behind the dais, its stones' joints hot (rig/forge-seams), its hearth alight ----
    fz = FIRE_FORGE_Z
    seams = Geo()
    tops = {}
    for side in (-1, 1):
        x0 = side * 3.3
        sk.box(mid, x0 - 1.35, x0 + 1.35, FIRE_FLOOR_Y - .3, FIRE_FLOOR_Y + .45, fz - 1.2, fz + 1.2, F['forge-dark'], T, bevel=.05)
        y = FIRE_FLOOR_Y + .45
        for c in range(5):
            h = 1.55 + .3 * H(side + 5, c, 1)
            w = 1.1 - .06 * c
            d = .95 - .05 * c
            j = .05 * S(side + 5, c, 2)
            sk.box(mid, x0 - w + j, x0 + w + j, y + .03, y + h, fz - d, fz + d, F['forge'], T, bevel=.06)
            # the joint under this course, glowing: a thin band set back into the gap on its four faces
            jw, jd = w - .04, d - .04
            for (ax, az, bx, bz, nx, nz) in ((-jw, jd, jw, jd, 0, 1), (jw, -jd, -jw, -jd, 0, -1), (jw, jd, jw, -jd, 1, 0), (-jw, -jd, -jw, jd, -1, 0)):
                seams.face([(x0 + ax + j, y, fz + az), (x0 + bx + j, y, fz + bz), (x0 + bx + j, y + .03, fz + bz), (x0 + ax + j, y + .03, fz + az)], F['rune'], normal=(nx, 0, nz))
            y += h
        tops[side] = y
    rigs['forge-seams'] = seams
    # the lintel, broken: one beam from the left pillar past the middle, its other half fallen against the right pillar
    top = tops[-1]
    sk.box(mid, -4.55, 1.0, top, top + 1.3, fz - .85, fz + .85, F['forge'], T, bevel=.08)
    piece = Geo()
    sk.box(piece, -1.4, 1.4, -.6, .6, -.75, .75, F['forge'], T, bevel=.08)
    mid.extend(piece.transformed(sk.affine(sk.quat_axis(0, 0, 1, 1.05), (1.25, FIRE_FLOOR_Y + 1.25, fz + .4))))
    # the hearth: a stepped back wall with an arched mouth framed by voussoirs, its fire inside (rig/hearth)
    sk.box(mid, -2.3, 2.3, FIRE_FLOOR_Y - .3, FIRE_FLOOR_Y + 4.6, fz - 1.9, fz - 1.1, F['forge-dark'], T, bevel=.04)
    sk.box(mid, -2.6, 2.6, FIRE_FLOOR_Y - .3, FIRE_FLOOR_Y + .35, fz - 2.0, fz - .7, F['forge-dark'], T, bevel=.04)
    n = (13, 9, 5)[T.i]
    cxm, cym, rx, ry = 0.0, FIRE_FLOOR_Y + 1.25, 1.05, 1.15
    for i in range(n):
        a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
        pts = [(cxm + math.cos(a0) * rx, cym + math.sin(a0) * ry), (cxm + math.cos(a0) * (rx + .42), cym + math.sin(a0) * (ry + .42)),
               (cxm + math.cos(a1) * (rx + .42), cym + math.sin(a1) * (ry + .42)), (cxm + math.cos(a1) * rx, cym + math.sin(a1) * ry)]
        z0, z1 = fz - 1.12, fz - .95
        mid.face([(p[0], p[1], z1) for p in pts], F['forge'], normal=(0, 0, 1))
        cx_, cy_ = sum(p[0] for p in pts) / 4, sum(p[1] for p in pts) / 4
        for (p, q) in ((pts[0], pts[1]), (pts[1], pts[2]), (pts[2], pts[3]), (pts[3], pts[0])):
            mx, my = (p[0] + q[0]) / 2 - cx_, (p[1] + q[1]) / 2 - cy_
            mid.face([(p[0], p[1], z0), (q[0], q[1], z0), (q[0], q[1], z1), (p[0], p[1], z1)], F['forge'], normal=(mx, my, 0))
    mouth = [(cxm - rx, FIRE_FLOOR_Y + .36)] + [(cxm + math.cos(math.pi - math.pi * i / n) * rx, cym + math.sin(math.pi - math.pi * i / n) * ry) for i in range(n + 1)] + [(cxm + rx, FIRE_FLOOR_Y + .36)]
    hearth = Geo()
    hearth.face([(x, y, fz - 1.09) for x, y in mouth], F['lava-hot'], normal=(0, 0, 1))
    rigs['hearth'] = hearth
    # ---- the crater rim: a ring of crags, two lava falls down its inner face (rig/falls) ----
    m = (96, 56, 32)[T.i]
    rows = [(23.5, -.5, 0), (24.6, 1.6, 0), (25.8, .32, 1), (27.0, .62, 1), (28.0, .88, 1), (29.0, 1.0, 1), (31.5, .85, 1), (35.0, .5, 1)]
    P = np.zeros((m, len(rows), 3))
    for i in range(m):
        a = 2 * math.pi * i / m
        Hh = 13 + 9 * n1(math.cos(a) * 3, 0, math.sin(a) * 3, .6, 7, 3) + 3 * math.sin(a * 3 + 1) + 4 * (H(i, 77) - .5) * (i % 3 == 0)
        for j, (r, h, rel) in enumerate(rows):
            y = h * Hh if rel else h
            jag = 2.2 * n1(math.cos(a) * 11, y * .25, math.sin(a) * 11, .7, 9 + j, 2) + (.9 * S(i, j, 78) if 0 < j < len(rows) - 1 else 0)
            P[i, j] = (math.cos(a) * (r + jag), FIRE_FLOOR_Y + y, math.sin(a) * (r + jag))
    surf(far, P, F['rim'], wrap_i=True, inside=(0, -200, 0), flat=True, pick=lambda nn, cc: F['rim-high'] if cc[1] > 10 else F['rim'])
    falls = Geo()
    for a in (FIRE_RIVERS[0] + .05, FIRE_RIVERS[1] + .03):
        i = int(round((a % (2 * math.pi)) / (2 * math.pi) * m)) % m
        inward = (-math.cos(a), 0.0, -math.sin(a))
        side = (-math.sin(a), 0.0, math.cos(a))
        # down the rim's face from its crest row to its foot, a little proud of it
        path = [P[i, j] for j in (5, 4, 3, 2, 1)]
        pts = []
        for j in range(len(path) - 1):
            for k in range(4):
                u = k / 4
                pts.append(tuple(path[j][c] + (path[j + 1][c] - path[j][c]) * u for c in range(3)))
        pts.append(tuple(path[-1]))
        n = len(pts) - 1
        for j in range(n):
            u0, u1 = j / n, (j + 1) / n
            w0, w1 = .22 + .7 * u0, .22 + .7 * u1
            m0 = .25 * math.sin(u0 * 7 + a)
            m1 = .25 * math.sin(u1 * 7 + a)
            q0 = [pts[j][c] + inward[c] * .35 + side[c] * m0 for c in range(3)]
            q1 = [pts[j + 1][c] + inward[c] * .35 + side[c] * m1 for c in range(3)]
            quad = [tuple(q0[c] - side[c] * w0 for c in range(3)), tuple(q0[c] + side[c] * w0 for c in range(3)),
                    tuple(q1[c] + side[c] * w1 for c in range(3)), tuple(q1[c] - side[c] * w1 for c in range(3))]
            falls.face(quad, F['lava'] if (j % 3) else F['lava-deep'], normal=(inward[0], .25, inward[2]))
    rigs['falls'] = falls
    return {'near': near, 'mid': mid, 'far': far}, rigs


# ---------------------------------------------------------------------------------------------------------------------------
# The hero facing (Core NativeProceduralEntrance.Facing): its forward and its left, and a yaw about +y in the source frame
# ---------------------------------------------------------------------------------------------------------------------------
FACING = -.22
FWD = (math.sin(FACING), 0.0, math.cos(FACING))
LEFT = (math.cos(FACING), 0.0, -math.sin(FACING))


def yawed(g, a, t=(0.0, 0.0, 0.0)):
    """`g` turned by `a` about +y (Core NativeEntranceMath.RotateY / Yaw: +z toward +x) and moved by t."""
    return g.transformed(sk.affine(sk.quat_axis(0, 1, 0, a), t))


def on_line(z, y=0.0):
    return (FWD[0] * z, y, FWD[2] * z)


def mirrored(g):
    """`g` with x negated (a reflection: each face's corners reversed so it still faces out): source frame <-> Unity space."""
    out = Geo()
    P, N, S, F = g.arrays()
    P = P.copy()
    N = N.copy()
    P[:, 0] *= -1
    N[:, 0] *= -1
    pos = 0
    for k, f in zip(S, F):
        idx = list(range(pos + k - 1, pos - 1, -1))
        out.p.extend(P[idx].ravel().tolist())
        out.n.extend(N[idx].ravel().tolist())
        out.size.append(int(k))
        out.fin.append(int(f))
        pos += k
    return out


def grid(g, P, f, flat=False, facing=None, inside=None, wrap_j=False, pick=None):
    surf(g, P, f, facing=facing, inside=inside, wrap_j=wrap_j, flat=flat, pick=pick)


# ---------------------------------------------------------------------------------------------------------------------------
# WATER: the drowned hall (built in the hall's frame: x across toward the pilot's left, z along its forward; then turned by
# Facing). Core NativeIntroStillTide keeps these.
# ---------------------------------------------------------------------------------------------------------------------------
WATER_SKYLIGHTS = (-14.0, -6.0, 2.0)          # the broken vault's bays (hall z)
WATER_ICE_RINGS = (1.6, 3.2, 5.5, 8.5, 14.0)  # the frost's rings round the mark (rig water/ice/k)
WATER_LEVEL = (.14, .22)                      # the water over the floor before and after the tide
WATER_HALF = 5.0                              # a wave half's length along its +z
HALL_X, HALL_Z0, HALL_Z1, NAVE_X = 9.2, -24.0, 16.0, 5.2
VAULT_SPRING, VAULT_TOP, ARCADE_TOP, ROSE_Z, ROSE_Y = 12.0, 14.2, 10.6, -23.6, 10.8


def water_finishes():
    F = {}
    F['stone'] = sk.fin('water-stone', 0x4a5458, sk.DETAIL_ASHLAR, smooth=.3, dirt=1.0, moss=.6, wear=.3)
    F['stone-dark'] = sk.fin('water-stone-dark', 0x2c3538, sk.DETAIL_ASHLAR, smooth=.35, dirt=1.0, moss=.8)
    F['marble'] = sk.fin('water-marble', 0x7f8a8c, sk.DETAIL_MARBLE, smooth=.35, dirt=.8, moss=.4, wear=.4)
    F['tile'] = sk.fin('water-tile', 0x3c4a4c, sk.DETAIL_TILE, smooth=.35, dirt=.9, moss=.5, param=.6)
    F['vault'] = sk.fin('water-vault', 0x353d40, sk.DETAIL_ASHLAR, smooth=.2, dirt=1.0, moss=.3)
    F['rib'] = sk.fin('water-rib', 0x59625f, sk.DETAIL_CARVED, smooth=.3, dirt=1.0, wear=.4)
    F['water'] = sk.fin('water-surface', 0x10384a, sk.DETAIL_NONE, smooth=.32, dirt=0.0)
    F['wave'] = sk.fin('water-wave', 0x1d7fa3, sk.DETAIL_NONE, smooth=.3, dirt=0.0)
    F['wave-deep'] = sk.fin('water-wave-deep', 0x0e4664, sk.DETAIL_NONE, smooth=.3, dirt=0.0)
    F['wave-light'] = sk.fin('water-wave-light', 0x3fb6e2, sk.DETAIL_NONE, smooth=.3, dirt=0.0)
    F['foam'] = sk.fin('water-foam', 0xdff6ff, sk.DETAIL_NONE, smooth=.2, dirt=0.0)
    F['ice'] = sk.fin('water-ice', 0x9fd8ec, sk.DETAIL_NONE, smooth=.34, dirt=0.0)
    F['ice-deep'] = sk.fin('water-ice-deep', 0x5aa9cc, sk.DETAIL_NONE, smooth=.34, dirt=0.0)
    F['ice-glow'] = sk.fin('water-ice-glow', 0x7fe6ff, sk.DETAIL_GLOW, glow=1.6)
    F['frost'] = sk.fin('water-frost', 0xd8f2fa, sk.DETAIL_PLASTER, smooth=.25, dirt=0.0)
    F['frost-glow'] = sk.fin('water-frost-glow', 0xbff2ff, sk.DETAIL_GLOW, glow=.9)
    F['window'] = sk.fin('water-window', 0xa9c8ff, sk.DETAIL_GLOW, glow=2.2)
    F['lead'] = sk.fin('water-lead', 0x1c2224, sk.DETAIL_IRON, smooth=.4, metal=.6, dirt=.6)
    return F


def pointed(L, H, n):
    """A pointed (or segmental) arch's half-profile from its springer to its apex (stage kit pointed_arch)."""
    return sk.pointed_arch(L, H, n)


def water_hall(T):
    """The hall's statics in the hall frame: (hall, vault, far)."""
    F = water_finishes()
    hall, vault, far = Geo(), Geo(), Geo()
    side_n = (14, 8, 6)[T.i]
    arc = (10, 6, 3)[T.i]
    # the floor: tiles under the water, a little uneven
    sk.grid_face(hall, (-HALL_X, FLOOR, HALL_Z1), (2 * HALL_X, 0, 0), (0, 0, HALL_Z0 - HALL_Z1), 1, 1, F['tile'], (0, 1, 0), (3.0, 6.0, 12.0)[T.i])
    # the colonnades: columns on plinths, a capital and an abacus each
    zs = [z for z in range(-22, 15, 4)]
    for side in (-1, 1):
        for z in zs:
            x = side * NAVE_X
            sk.box(hall, x - .62, x + .62, FLOOR - .1, FLOOR + .42, z - .62, z + .62, F['marble'], T, bevel=.04)
            sk.lathe(hall, [(.56, FLOOR + .42), (.56, FLOOR + .52), (.5, FLOOR + .62), (.44, FLOOR + .7), (.42, FLOOR + 1.2), (.4, FLOOR + 6.6), (.42, FLOOR + 6.8),
                            (.5, FLOOR + 7.0), (.62, FLOOR + 7.35), (.66, FLOOR + 7.5)], (x, 0, z), F['marble'], side_n, cap1=True)
            sk.box(hall, x - .72, x + .72, FLOOR + 7.5, FLOOR + 7.8, z - .72, z + .72, F['stone'], T, bevel=.03)
    spring = FLOOR + 7.8
    # the arcades: pointed arches between the columns, their spandrels up to the arcade's top, the clerestory over them
    for side in (-1, 1):
        x = side * NAVE_X
        for i in range(len(zs) - 1):
            z0, z1 = zs[i] + .7, zs[i + 1] - .7
            L = (z1 - z0) / 2
            half = pointed(L, 2.2, arc)
            curve = [(z0 + d, spring + h) for d, h in half] + [(z1 - d, spring + h) for d, h in reversed(half[:-1])]
            for xo, nx in ((x - side * .35, -side), (x + side * .35, side)):
                for k in range(len(curve) - 1):
                    (za, ya), (zb, yb) = curve[k], curve[k + 1]
                    hall.face([(xo, ya, za), (xo, yb, zb), (xo, ARCADE_TOP, zb), (xo, ARCADE_TOP, za)], F['stone'], normal=(nx, 0, 0))
            for k in range(len(curve) - 1):
                (za, ya), (zb, yb) = curve[k], curve[k + 1]
                hall.face([(x - .35, ya, za), (x + .35, ya, za), (x + .35, yb, zb), (x - .35, yb, zb)], F['stone'], normal=(0, -1, 0))
            # the clerestory over the bay, a lancet of moonlight in it
            for xo, nx in ((x - side * .3, -side), (x + side * .3, side)):
                hall.face([(xo, ARCADE_TOP, zs[i]), (xo, ARCADE_TOP, zs[i + 1]), (xo, VAULT_SPRING, zs[i + 1]), (xo, VAULT_SPRING, zs[i])], F['stone-dark'], normal=(nx, 0, 0))
            zc = (zs[i] + zs[i + 1]) / 2
            hall.face([(x - side * .31, ARCADE_TOP + .2, zc - .45), (x - side * .31, ARCADE_TOP + .2, zc + .45), (x - side * .31, VAULT_SPRING - .25, zc + .45), (x - side * .31, VAULT_SPRING - .1, zc), (x - side * .31, VAULT_SPRING - .25, zc - .45)],
                      F['window'], normal=(-side, 0, 0))
        # over the columns the wall runs on without a gap
        for z in zs:
            for xo, nx in ((x - side * .35, -side), (x + side * .35, side)):
                hall.face([(xo, spring, z - .72), (xo, spring, z + .72), (xo, ARCADE_TOP, z + .72), (xo, ARCADE_TOP, z - .72)], F['stone'], normal=(nx, 0, 0))
        # the aisle: its outer wall with lancets, a lean-to roof
        xo = side * HALL_X
        for i in range(len(zs) - 1):
            zc = (zs[i] + zs[i + 1]) / 2
            hall.face([(xo, FLOOR, zs[i]), (xo, FLOOR, zs[i + 1]), (xo, 9.0, zs[i + 1]), (xo, 9.0, zs[i])], F['stone-dark'], normal=(-side, 0, 0))
            hall.face([(xo - side * .02, 2.4, zc - .6), (xo - side * .02, 2.4, zc + .6), (xo - side * .02, 6.6, zc + .6), (xo - side * .02, 7.3, zc), (xo - side * .02, 6.6, zc - .6)], F['window'], normal=(-side, 0, 0))
        hall.face([(xo, 9.0, HALL_Z0), (xo, 9.0, HALL_Z1), (x, ARCADE_TOP - .4, HALL_Z1), (x, ARCADE_TOP - .4, HALL_Z0)], F['vault'], normal=(-side * .3, -1, 0))
    # the vault: a segmental barrel over the nave, a rib on every column line, broken open over three bays
    prof = [(-NAVE_X + d, VAULT_SPRING + h) for d, h in sk.pointed_arch(NAVE_X, VAULT_TOP - VAULT_SPRING, (12, 8, 5)[T.i])]
    prof = prof + [(-x, y) for x, y in reversed(prof[:-1])]
    zstep = (2.0, 4.0, 8.0)[T.i]
    zz = [HALL_Z0 + i * zstep for i in range(int((HALL_Z1 - HALL_Z0) / zstep) + 1)]
    for i in range(len(zz) - 1):
        za, zb = zz[i], zz[i + 1]
        zc = (za + zb) / 2
        for k in range(len(prof) - 1):
            (xa, ya), (xb, yb) = prof[k], prof[k + 1]
            xm = (xa + xb) / 2
            if any(abs(zc - s) < 1.6 for s in WATER_SKYLIGHTS) and abs(xm) < 1.6 + .6 * H(int(zc), k):
                continue
            nrm = (-xm, -(VAULT_TOP + 4 - (ya + yb) / 2), 0)
            vault.face([(xa, ya, za), (xb, yb, za), (xb, yb, zb), (xa, ya, zb)], F['vault'], normal=nrm)
    for z in zs:
        path = [(x, y - .05, z) for x, y in prof]
        sk.sweep(vault, sk.rect_section(.32, .3, .06 if T.bevel else 0), path, F['rib'], smooth_u=False)
    # the far wall with its rose window (glass and lead tracery) over a great door, the near wall
    rose_n = (32, 20, 12)[T.i]
    for xo0, xo1 in ((-HALL_X, -3.4), (3.4, HALL_X)):
        far.face([(xo0, FLOOR, HALL_Z0), (xo1, FLOOR, HALL_Z0), (xo1, VAULT_TOP + 1, HALL_Z0), (xo0, VAULT_TOP + 1, HALL_Z0)], F['stone-dark'], normal=(0, 0, 1))
    far.face([(-3.4, FLOOR + 5.4, HALL_Z0), (3.4, FLOOR + 5.4, HALL_Z0), (3.4, ROSE_Y - 3.3, HALL_Z0), (-3.4, ROSE_Y - 3.3, HALL_Z0)], F['stone-dark'], normal=(0, 0, 1))
    far.face([(-3.4, ROSE_Y + 3.3, HALL_Z0), (3.4, ROSE_Y + 3.3, HALL_Z0), (3.4, VAULT_TOP + 1, HALL_Z0), (-3.4, VAULT_TOP + 1, HALL_Z0)], F['stone-dark'], normal=(0, 0, 1))
    # the rose: an annulus of wall round the glass, the glass, the lead: spokes and a ring of tracery
    for k in range(rose_n):
        a0, a1 = 2 * math.pi * k / rose_n, 2 * math.pi * (k + 1) / rose_n
        c0, s0, c1, s1 = math.cos(a0), math.sin(a0), math.cos(a1), math.sin(a1)
        sq = lambda c, s: (max(-1, min(1, c * 1.42)) * 3.4, max(-1, min(1, s * 1.42)) * 3.3)
        q0, q1 = sq(c0, s0), sq(c1, s1)
        far.face([(c0 * 3.0, ROSE_Y + s0 * 3.0, HALL_Z0), (q0[0], ROSE_Y + q0[1], HALL_Z0), (q1[0], ROSE_Y + q1[1], HALL_Z0), (c1 * 3.0, ROSE_Y + s1 * 3.0, HALL_Z0)], F['stone-dark'], normal=(0, 0, 1))
        far.face([(0, ROSE_Y, ROSE_Z - .4), (c0 * 3.0, ROSE_Y + s0 * 3.0, ROSE_Z - .4), (c1 * 3.0, ROSE_Y + s1 * 3.0, ROSE_Z - .4)], F['window'], normal=(0, 0, 1))
    spokes = (12, 8, 6)[T.i]
    for k in range(spokes):
        a = 2 * math.pi * k / spokes
        p0 = (math.cos(a) * .5, ROSE_Y + math.sin(a) * .5, ROSE_Z - .3)
        p1 = (math.cos(a) * 3.0, ROSE_Y + math.sin(a) * 3.0, ROSE_Z - .3)
        sk.cylinder(far, p0, p1, .07, F['lead'], 5)
    sk.lathe(far, [(.5, -.1), (.62, 0), (.5, .1)], (0, ROSE_Y, ROSE_Z - .3), F['lead'], 14, axis=(0, 0, 1))
    sk.lathe(far, [(1.7, -.08), (1.82, 0), (1.7, .08)], (0, ROSE_Y, ROSE_Z - .3), F['lead'], rose_n, axis=(0, 0, 1))
    # the great door below: a dark arched recess
    door = [(-2.2 + 4.4 * i / arc, FLOOR + 3.6 + 1.8 * math.sin(math.pi * i / arc)) for i in range(arc + 1)]
    far.face([(-2.2, FLOOR, HALL_Z0 - 1.2)] + [(x, y, HALL_Z0 - 1.2) for x, y in door] + [(2.2, FLOOR, HALL_Z0 - 1.2)], F['stone-dark'], normal=(0, 0, 1))
    for x0, x1 in ((-3.4, -2.2), (2.2, 3.4)):
        far.face([(x0, FLOOR, HALL_Z0), (x1, FLOOR, HALL_Z0), (x1, FLOOR + 5.4, HALL_Z0), (x0, FLOOR + 5.4, HALL_Z0)], F['stone-dark'], normal=(0, 0, 1))
    for k in range(len(door) - 1):
        (xa, ya), (xb, yb) = door[k], door[k + 1]
        far.face([(xa, ya, HALL_Z0), (xb, yb, HALL_Z0), (xb, FLOOR + 5.4, HALL_Z0), (xa, FLOOR + 5.4, HALL_Z0)], F['stone-dark'], normal=(0, 0, 1))
    far.face([(-HALL_X, FLOOR, HALL_Z1), (-HALL_X, 14.0, HALL_Z1), (HALL_X, 14.0, HALL_Z1), (HALL_X, FLOOR, HALL_Z1)], F['stone-dark'], normal=(0, 0, -1))
    # the fallen: a column's drums, a capital, rubble under the broken bays
    for i, (x, z, a, L) in enumerate(((-2.6, -8.5, .6, 1.4), (3.4, -12.0, -.3, 1.1), (-3.6, 4.5, 1.2, .9))):
        t = Geo()
        sk.lathe(t, [(.4, -L / 2), (.4, L / 2)], (0, 0, 0), F['marble'], side_n, cap0=True, cap1=True)
        q = qmul(sk.quat_axis(0, 1, 0, a), sk.quat_axis(1, 0, 0, math.pi / 2))
        hall.extend(t.transformed(sk.affine(q, (x, FLOOR + .3, z))))
    for s in WATER_SKYLIGHTS:
        if s > -3:
            continue
        for k in range((7, 4, 2)[T.i]):
            r = .2 + .35 * H(int(s), k, 3)
            x = (2.6 + 1.6 * H(int(s), k, 1)) * (1 if k % 2 else -1)
            rock(hall, (x, FLOOR + r * .3, s + S(int(s), k, 2) * 1.4), r, F['stone'], T, 900 + int(s) * 7 + k, squash=.6)
    return hall, vault, far


def ear_clip(poly):
    """Triangles (index triples) of a simple polygon [(x, y)] by ear clipping."""
    idx = list(range(len(poly)))
    area = sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1] for i in range(len(poly)))
    sgn = 1 if area > 0 else -1
    out = []

    def inside(p, a, b, c):
        def cr(o, u, v):
            return (u[0] - o[0]) * (v[1] - o[1]) - (u[1] - o[1]) * (v[0] - o[0])
        d1, d2, d3 = cr(a, b, p), cr(b, c, p), cr(c, a, p)
        return (d1 * sgn > 0) and (d2 * sgn > 0) and (d3 * sgn > 0)
    guard = 0
    while len(idx) > 3 and guard < 2000:
        guard += 1
        n = len(idx)
        for k in range(n):
            i0, i1, i2 = idx[(k - 1) % n], idx[k], idx[(k + 1) % n]
            a, b, c = poly[i0], poly[i1], poly[i2]
            if ((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])) * sgn <= 1e-9:
                continue
            if any(inside(poly[j], a, b, c) for j in idx if j not in (i0, i1, i2)):
                continue
            out.append((i0, i1, i2))
            idx.pop(k)
            break
        else:
            break
    if len(idx) == 3:
        out.append(tuple(idx))
    return out


def wave_colours(bk, g):
    """A liquid wall's colour as a gradient: deep at its foot, lighter up its face and under its lip (the light through it),
    foam white along its crest (smooth, where finishes alone would band)."""
    P, N, S_, F_ = g.arrays()
    y = P[:, 1].astype(np.float64)
    deep, mid, light, foam = np.array(sk.srgb(0x0b3a58)), np.array(sk.srgb(0x1a78a0)), np.array(sk.srgb(0x52c4ea)), np.array(sk.srgb(0xe2f8ff))
    u = np.clip((y + .2) / 4.4, 0, 1)
    col = np.where(u[:, None] < .55, deep + (mid - deep) * (u[:, None] / .55), mid + (light - mid) * ((u[:, None] - .55) / .45))
    up = np.clip(N[:, 1].astype(np.float64), -1, 1)
    col = col + (light - col) * np.clip(-up, 0, 1)[:, None] * .6 * (y > 2.4)[:, None]
    f = np.clip((y - 4.3) / .35, 0, 1)
    col = col + (foam - col) * f[:, None]
    bk.colour[:, :3] = np.round(np.clip(col, 0, 1) * 255).astype(np.uint8)


def wave_env(u):
    """A half's height along its length: near full at the parting end (u 0, where the two halves meet as one wall), tapering
    into the water at the outer end."""
    return max(0.0, math.sin(math.pi / 2 * min(1.0, (1.0 - u) / .32))) ** .6


def water_half(T, frozen):
    """A wave half in its own frame: the wall along +z (0 .. WATER_HALF), its back on -x, its curl toward +x (the right half;
    the left is its mirror). Liquid: smooth water with a foam crest; frozen: faceted ice, icicles off the lip, a lit vein."""
    F = water_finishes()
    g = Geo()
    prof = [(-1.6, -.4), (-1.35, .5), (-1.0, 1.6), (-.62, 2.7), (-.25, 3.6), (.08, 4.25), (.5, 4.62), (.95, 4.68), (1.45, 4.48), (1.85, 4.05),
            (2.02, 3.5), (1.92, 3.0), (1.62, 2.82), (1.25, 3.05), (.92, 3.25), (.66, 3.1), (.5, 2.7), (.48, 2.0), (.58, 1.2), (.8, .5), (1.05, -.4)]
    nz = (22, 12, 7)[T.i]
    P = np.zeros((nz + 1, len(prof), 3))
    for i in range(nz + 1):
        u = i / nz
        z = WATER_HALF * u
        sy = wave_env(u)
        sx = .75 + .25 * sy
        for j, (x, y) in enumerate(prof):
            wob = .06 * math.sin(u * 7 + j * .9) * (y / 4.8) if not frozen else .05 * S(i, j, 7) * (y / 4.8)
            P[i, j] = (x * sx + wob, y * sy + (.0 if y > 0 else 0), z)
    end = [tuple(P[0, j]) for j in range(len(prof))]
    for tri in ear_clip([(q[0], q[1]) for q in end]):
        g.face([end[k] for k in tri], F['ice-deep'] if frozen else F['wave-deep'], normal=(0, 0, -1))
    if frozen:
        pick = lambda nn, cc: F['frost'] if cc[1] > 4.2 else (F['ice'] if nn[0] < .2 else F['ice-deep'])
        grid(g, P, F['ice'], flat=True, inside=(-.1, 2.0, 2.5), wrap_j=True, pick=pick)
        # icicles off the lip, a vein of light inside the curl
        for i in range(1, nz):
            if H(i, 3) < (.35 if T.i == 0 else .6):
                continue
            u = i / nz
            sy = wave_env(u)
            base = (1.92 * (.75 + .25 * sy), 3.0 * sy, WATER_HALF * u)
            L = (.25 + .45 * H(i, 4)) * sy
            if L > .08:
                cone = Geo()
                sk.lathe(cone, [(.07, 0), (0, -L)], base, F['ice'], 5)
                g.extend(cone)
        vein = Geo()
        for i in range(nz):
            u0, u1 = i / nz, (i + 1) / nz
            a = [(1.55 * (.75 + .25 * wave_env(u)), 2.88 * wave_env(u), WATER_HALF * u) for u in (u0, u1)]
            b = [(.95 * (.75 + .25 * wave_env(u)), 3.2 * wave_env(u), WATER_HALF * u) for u in (u0, u1)]
            vein.face([a[0], a[1], b[1], b[0]], F['ice-glow'], normal=(1, -.6, 0))
        g.extend(vein)
    else:
        pick = lambda nn, cc: F['foam'] if cc[1] > 4.5 else F['wave']
        grid(g, P, F['wave'], inside=(-.1, 2.0, 2.5), wrap_j=True, pick=pick)
    return g


def water_set(T):
    F = water_finishes()
    hall, vault, far = water_hall(T)
    props = {'water/hall': yawed(hall, FACING), 'water/vault': yawed(vault, FACING), 'water/far': yawed(far, FACING)}
    # the water's face: a sheet over the hall's floor (Core lifts it to the tide's level)
    surface = Geo()
    sk.grid_face(surface, (-HALL_X + .05, 0, HALL_Z1 - .05), (2 * HALL_X - .1, 0, 0), (0, 0, HALL_Z0 - HALL_Z1 + .1), 1, 1, F['water'], (0, 1, 0), (2.0, 5.0, 10.0)[T.i])
    props['water/surface'] = yawed(surface, FACING)
    # the frost's rings round the mark, in the mark's frame (Core turns them with the facing)
    r0 = 0.0
    sides = (48, 28, 16)[T.i]
    for k, r in enumerate(WATER_ICE_RINGS):
        g = Geo()
        for i in range(sides):
            a0, a1 = 2 * math.pi * i / sides, 2 * math.pi * (i + 1) / sides
            f = F['frost'] if H(k, i, 1) > .3 else F['ice']
            pts = [(math.cos(a0) * r, 0, math.sin(a0) * r), (math.cos(a1) * r, 0, math.sin(a1) * r)]
            pts += [(math.cos(a1) * r0, 0, math.sin(a1) * r0), (math.cos(a0) * r0, 0, math.sin(a0) * r0)] if r0 > 0 else [(0, 0, 0)]
            g.face(pts, f, normal=(0, 1, 0))
            # a lit crack across some cells
            if T.i == 0 and H(k, i, 2) > .7:
                am = (a0 + a1) / 2
                ra, rb = r0 + (r - r0) * .2, r0 + (r - r0) * .85
                w = .015
                d = (math.cos(am), math.sin(am))
                n = (-d[1], d[0])
                g.face([(d[0] * ra + n[0] * w, .004, d[1] * ra + n[1] * w), (d[0] * rb + n[0] * w, .004, d[1] * rb + n[1] * w),
                        (d[0] * rb - n[0] * w, .004, d[1] * rb - n[1] * w), (d[0] * ra - n[0] * w, .004, d[1] * ra - n[1] * w)], F['frost-glow'], normal=(0, 1, 0))
        props['water/ice/%d' % k] = g
        r0 = r
    right = water_half(T, False)
    props['water/wave/R'] = right
    props['water/wave/L'] = mirrored(right)
    right_ice = water_half(T, True)
    props['water/frozen/R'] = right_ice
    props['water/frozen/L'] = mirrored(right_ice)
    return props


# ---------------------------------------------------------------------------------------------------------------------------
# LIGHTNING: the storm summit. Core NativeIntroStrikePoint keeps these.
# ---------------------------------------------------------------------------------------------------------------------------
SPIRE_BEARINGS = (30.0, 90.0, 150.0, -30.0, -90.0, -150.0)  # degrees from the hero facing, + toward the pilot's left
SPIRE_RING, SPIRE_TIP = 7.6, 6.2


def spire_at(i, y=0.0):
    a = FACING + math.radians(SPIRE_BEARINGS[i])
    return (math.sin(a) * SPIRE_RING, y, math.cos(a) * SPIRE_RING)


def storm_finishes():
    F = {}
    F['slate'] = sk.fin('storm-slate', 0x2c3238, sk.DETAIL_FLAG, smooth=.35, dirt=.8, moss=.2, wear=.35)
    F['slate-top'] = sk.fin('storm-slate-top', 0x363d46, sk.DETAIL_FLAG, smooth=.55, dirt=.6, wear=.2)
    F['rock'] = sk.fin('storm-rock', 0x2c3138, sk.DETAIL_CARVED, smooth=.2, dirt=1.0, moss=.3)
    F['obelisk'] = sk.fin('storm-obelisk', 0x2e3036, sk.DETAIL_CARVED, smooth=.3, dirt=.8, wear=.5)
    F['copper'] = sk.fin('storm-copper', 0x4f8a78, sk.DETAIL_BRASS, smooth=.5, metal=.7, dirt=.6, wear=.6)
    F['iron'] = sk.fin('storm-iron', 0x2a2c30, sk.DETAIL_IRON, smooth=.55, metal=.85, dirt=.5, wear=.4)
    F['glass'] = sk.fin('storm-fulgurite', 0x0f1614, sk.DETAIL_NONE, smooth=.35, dirt=.2)
    F['scorch'] = sk.fin('storm-scorch', 0x1a1b1f, sk.DETAIL_FLAG, smooth=.3, dirt=.8)
    F['cloud'] = sk.fin('storm-cloud', 0x5a6478, sk.DETAIL_NONE, smooth=0.0, dirt=0.0)
    F['peak'] = sk.fin('storm-peak', 0x3a4250, sk.DETAIL_CARVED, smooth=.1, dirt=.6)
    F['snow'] = sk.fin('storm-snow', 0x8c97a8, sk.DETAIL_PLASTER, smooth=.3, dirt=.2)
    F['seam'] = sk.fin('storm-seam', 0xc8aaff, sk.DETAIL_GLOW, glow=2.0)
    F['molten'] = sk.fin('storm-molten', 0xe0d0ff, sk.DETAIL_GLOW, glow=2.4)
    return F


def lightning_set(T):
    F = storm_finishes()
    plateau, spires, far = Geo(), Geo(), Geo()
    crater = Geo()
    # the plateau: fractured slate slabs, stacked in places; the crater's ring left open
    for i, ((cx, cz), poly) in enumerate(hex_cells(15.0, (1.15, 1.15, 1.15)[T.i], 41, jitter=.4)):
        r = math.hypot(cx, cz)
        if r < 2.1 or r > 14.6:
            continue
        p = shrink(poly, .025 + .02 * H(i, 2))
        lift = .05 * S(i, 3) + (.1 if H(i, 4) > .88 else 0) - .02 * max(0.0, r - 10)
        tilt = (S(i, 5) * .05, S(i, 6) * .05)
        prism(plateau, p, -.6, FLOOR + lift, F['slate'], F['slate-top'] if H(i, 7) > .2 else F['scorch'], tilt, chamfer=.025 if (T.bevel and r < 8) else 0)
        if H(i, 8) > .9 and r > 4:
            prism(plateau, shrink(p, .3), FLOOR + lift, FLOOR + lift + .12, F['slate'], F['slate-top'], (S(i, 9) * .08, S(i, 10) * .08), chamfer=.02 if T.bevel else 0)
    # the edge: a cliff of broken rock falling away into the cloud sea
    m = (96, 48, 24)[T.i]
    rows = [(14.4, FLOOR - .05), (15.4, -.6), (16.5, -2.6), (17.4, -6.0), (18.6, -10.5), (20.5, -16.0)]
    P = np.zeros((m, len(rows), 3))
    for i in range(m):
        a = 2 * math.pi * i / m
        for j, (r, y) in enumerate(rows):
            jag = (1.4 * n1(math.cos(a) * 6, y * .3, math.sin(a) * 6, .9, 31 + j, 2) + .5 * S(i, j, 33)) * (j > 0)
            P[i, j] = (math.cos(a) * (r + jag), y + (.0 if j == 0 else .6 * S(i, j, 34)), math.sin(a) * (r + jag))
    surf(plateau, P, F['rock'], wrap_i=True, inside=(0, 10, 0), flat=True)
    # the crater: a shallow bowl of shattered slabs and fulgurite glass, molten seams in it (rig lightning/crater)
    n = (14, 9, 6)[T.i]
    for i in range(n):
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
        for r0, r1, y0, y1 in ((.0, .9, FLOOR - .16, FLOOR - .1), (.9, 2.1, FLOOR - .1, FLOOR + .02)):
            pts = [(math.cos(a0) * r1, y1, math.sin(a0) * r1), (math.cos(a1) * r1, y1, math.sin(a1) * r1)]
            pts += [(math.cos(a1) * r0, y0, math.sin(a1) * r0), (math.cos(a0) * r0, y0, math.sin(a0) * r0)] if r0 > 0 else [(0, y0, 0)]
            crater.face(pts, F['glass'], normal=(0, 1, 0))
        # shards heaved up round the rim, splayed outward
        if H(i, 51) > .25:
            am = (a0 + a1) / 2 + S(i, 52) * .1
            d = (math.cos(am), math.sin(am))
            b = (d[0] * 1.9, FLOOR - .05, d[1] * 1.9)
            tip = (d[0] * (2.3 + .3 * H(i, 53)), FLOOR + .25 + .35 * H(i, 54), d[1] * (2.3 + .3 * H(i, 53)))
            slab = Geo()
            w = .25 + .2 * H(i, 55)
            n_ = (-d[1], d[0])
            q0 = (b[0] + n_[0] * w, b[1], b[2] + n_[1] * w)
            q1 = (b[0] - n_[0] * w, b[1], b[2] - n_[1] * w)
            slab.face([q0, q1, tip], F['slate'], normal=(d[0], .4, d[1]))
            slab.face([q1, q0, (b[0] - d[0] * .3, b[1], b[2] - d[1] * .3), ], F['scorch'], normal=(-d[0], .6, -d[1]))
            slab.face([q0, tip, (b[0] - d[0] * .3, b[1], b[2] - d[1] * .3)], F['slate'], normal=(n_[0], .3, n_[1]))
            slab.face([q1, (b[0] - d[0] * .3, b[1], b[2] - d[1] * .3), tip], F['slate'], normal=(-n_[0], .3, -n_[1]))
            plateau.extend(slab)
        # a molten seam out from the centre
        am = (a0 + a1) / 2
        d = (math.cos(am), math.sin(am))
        nn = (-d[1], d[0])
        for r0, r1 in ((.15, .85), (.9, 1.6 + .4 * H(i, 56))):
            yy = FLOOR - .095 if r0 < .5 else FLOOR - .03
            w = .025
            crater.face([(d[0] * r0 + nn[0] * w, yy + .005, d[1] * r0 + nn[1] * w), (d[0] * r1 + nn[0] * w, yy + .005, d[1] * r1 + nn[1] * w),
                         (d[0] * r1 - nn[0] * w, yy + .005, d[1] * r1 - nn[1] * w), (d[0] * r0 - nn[0] * w, yy + .005, d[1] * r0 - nn[1] * w)], F['molten'], normal=(0, 1, 0))
    # the spires: tapered obelisks on stepped bases, copper bands, a coil, an iron spike; their seams lit on their own
    seams = [Geo() for _ in SPIRE_BEARINGS]
    for i in range(len(SPIRE_BEARINGS)):
        base = spire_at(i)
        a = math.atan2(base[0], base[2])
        s = Geo()
        sk.box(s, -.95, .95, -.3, .25, -.95, .95, F['slate'], T, bevel=.05)
        sk.box(s, -.7, .7, .25, .55, -.7, .7, F['obelisk'], T, bevel=.04)
        body = [(.45, .55), (.42, 1.2), (.38, 2.6), (.33, 4.0), (.28, 5.3)]
        prof = [(r * math.sqrt(2), y) for r, y in body]
        sk.lathe(s, prof, (0, 0, 0), F['obelisk'], 4, smooth=False, phase=math.pi / 4)
        for y in (1.2, 2.6, 4.0):
            r = .42 - (y - 1.2) * .03
            sk.lathe(s, [(r * math.sqrt(2) + .04, y - .09), (r * math.sqrt(2) + .06, y), (r * math.sqrt(2) + .04, y + .09)], (0, 0, 0), F['copper'], 4, smooth=False, phase=math.pi / 4)
        sk.lathe(s, [(.36, 5.3), (.26, 5.45), (.12, 5.55), (.06, 5.6)], (0, 0, 0), F['iron'], (8, 6, 4)[T.i])
        sk.cylinder(s, (0, 5.5, 0), (0, SPIRE_TIP, 0), .045, F['iron'], 6, r1=.008)
        if T.i < 2:
            helix = [(math.cos(k * .5) * (.42 - .0055 * k), 3.0 + k * .045, math.sin(k * .5) * (.42 - .0055 * k)) for k in range(46)]
            sk.tube(s, helix, .022, F['copper'], 5)
        for f in range(4):
            fa = f * math.pi / 2
            dx, dz = math.sin(fa), math.cos(fa)
            w = .03
            pts = []
            for y0, y1 in ((.7, 1.1), (1.3, 2.5), (2.7, 3.9)):
                r0 = (.44 - (y0 - .55) * .033) + .012
                r1 = (.44 - (y1 - .55) * .033) + .012
                seams[i].face([(dx * r0 - dz * w, y0, dz * r0 + dx * w), (dx * r0 + dz * w, y0, dz * r0 - dx * w), (dx * r1 + dz * w, y1, dz * r1 - dx * w), (dx * r1 - dz * w, y1, dz * r1 + dx * w)],
                              F['seam'], normal=(dx, 0, dz))
        spires.extend(yawed(s, a, base))
        seams[i] = yawed(seams[i], a, base)
    # broken standing stones between the spires
    for i in range(6):
        a = FACING + math.radians(SPIRE_BEARINGS[i] + 30 + 8 * S(i, 61))
        r = 10.5 + 1.5 * H(i, 62)
        st = Geo()
        h = .8 + 1.4 * H(i, 63)
        prism(st, ccw([(-.35, -.18), (.35, -.18), (.32, .2), (-.3, .2)]), -.3, h, F['obelisk'], F['slate-top'], (S(i, 64) * .25, S(i, 65) * .1), chamfer=.03 if T.bevel else 0)
        plateau.extend(yawed(st, a + .3 * S(i, 66), (math.sin(a) * r, 0, math.cos(a) * r)))
    # the far: peaks through the cloud sea, the cloud sea's rolling top
    for i in range((12, 8, 6)[T.i]):
        a = 2 * math.pi * (i + .5 * H(i, 70)) / 12
        r = 70 + 70 * H(i, 71)
        h = 18 + 30 * H(i, 72)
        base = (math.sin(a) * r, -14, math.cos(a) * r)
        # a jagged massif: a faceted cone pushed about by noise, its summit off-centre, a lower spur beside it
        for spur in range(2 if T.i < 2 else 1):
            pk = Geo()
            rr = (14 + 12 * H(i, 73)) * (1 if spur == 0 else .55)
            hh = h * (1 if spur == 0 else .6)
            sides_k, rings_k = (9, 7, 6)[T.i], (5, 4, 3)[T.i]
            top = (S(i, 78 + spur) * rr * .25, hh, S(i, 79 + spur) * rr * .25)
            Pk = np.zeros((rings_k + 1, sides_k, 3))
            for a_i in range(rings_k + 1):
                u = a_i / rings_k
                for b_i in range(sides_k):
                    ang = 2 * math.pi * b_i / sides_k
                    rad = rr * (1 - u) ** 1.25 * (.7 + .55 * H(i, spur, a_i * 17 + b_i))
                    Pk[a_i, b_i] = (top[0] * u + math.cos(ang) * rad, hh * u + (1.6 * S(i, a_i, b_i + 40) if 0 < a_i < rings_k else 0), top[2] * u + math.sin(ang) * rad)
            surf(pk, Pk, F['peak'], wrap_j=True, inside=(top[0], hh * .3, top[2]), flat=True, pick=lambda nn, cc, hh=hh: F['snow'] if cc[1] > hh * .62 and nn[1] > .35 else F['peak'])
            off = (0, 0, 0) if spur == 0 else (rr * 1.3 * (1 if H(i, 81) > .5 else -1), 0, rr * .4)
            far.extend(pk.transformed(sk.affine((0, 0, 0, 1), (base[0] + off[0], base[1], base[2] + off[2]))))
    nr, na = (14, 8, 6)[T.i], (64, 32, 20)[T.i]
    P = np.zeros((nr, na, 3))
    for i in range(nr):
        r = 19 + (190 - 19) * (i / (nr - 1)) ** 1.6
        for j in range(na):
            a = 2 * math.pi * j / na
            y = -13.0 + 2.2 * n1(math.cos(a) * r * .05, 0, math.sin(a) * r * .05, .7, 81, 3) + .8 * S(i, j, 82)
            P[i, j] = (math.cos(a) * r, y, math.sin(a) * r)
    surf(far, P, F['cloud'], wrap_j=True, facing=(0, 1, 0))
    props = {'lightning/plateau': plateau, 'lightning/spires': spires, 'lightning/far': far, 'lightning/crater': crater}
    for i, g in enumerate(seams):
        props['lightning/seams/%d' % i] = g
    return props


# ---------------------------------------------------------------------------------------------------------------------------
# DARKNESS: the eclipse plain. Core NativeIntroBlackSun keeps these.
# ---------------------------------------------------------------------------------------------------------------------------
DARK_MONOLITHS = (55.0, 95.0, 135.0, -55.0, -95.0, -135.0)  # degrees from the hero facing
DARK_RING, DARK_GATE_Z, DARK_MOON_R = 11.5, -14.0, 5.4


def dark_finishes():
    F = {}
    F['obsidian'] = sk.fin('dark-obsidian', 0x0e0c14, sk.DETAIL_NONE, smooth=.35, dirt=.15, wear=.5)
    F['obsidian-edge'] = sk.fin('dark-obsidian-edge', 0x1c1828, sk.DETAIL_NONE, smooth=.3, dirt=.3)
    F['ash'] = sk.fin('dark-ash', 0x5d5866, sk.DETAIL_PLASTER, smooth=.05, dirt=.4)
    F['ash-dark'] = sk.fin('dark-ash-dark', 0x3a3642, sk.DETAIL_PLASTER, smooth=.05, dirt=.5)
    F['basalt'] = sk.fin('dark-basalt', 0x1f1c26, sk.DETAIL_CARVED, smooth=.35, dirt=.7, wear=.55)
    F['gate'] = sk.fin('dark-gate', 0x2a2632, sk.DETAIL_ASHLAR, smooth=.3, dirt=.8, wear=.6)
    F['mesa'] = sk.fin('dark-mesa', 0x2c2834, sk.DETAIL_CARVED, smooth=.05, dirt=.5)
    F['glyph'] = sk.fin('dark-glyph', 0xb07aff, sk.DETAIL_GLOW, glow=1.8)
    F['crack'] = sk.fin('dark-crack', 0x7a3cd8, sk.DETAIL_GLOW, glow=.8)
    F['moon'] = sk.fin('dark-moon', 0x020104, sk.DETAIL_NONE, smooth=0.0, dirt=0.0)
    return F


def monolith_at(i, y=0.0):
    a = FACING + math.radians(DARK_MONOLITHS[i])
    return (math.sin(a) * DARK_RING, y, math.cos(a) * DARK_RING)


def darkness_set(T):
    F = dark_finishes()
    plain, stones, far = Geo(), Geo(), Geo()
    # the obsidian: plates of black glass, a dim violet in the cracks
    for i, ((cx, cz), poly) in enumerate(hex_cells(21.0, (1.7, 1.7, 1.7)[T.i], 61, jitter=.42)):
        r = math.hypot(cx, cz)
        if r > 20.5:
            continue
        p = shrink(poly, .02 + .015 * H(i, 2))
        prism(plain, p, -.25, FLOOR + .008 * S(i, 3), F['obsidian-edge'], F['obsidian'], (S(i, 4) * .006, S(i, 5) * .006), chamfer=.02 if (T.bevel and r < 9) else 0)
    sides = (48, 28, 16)[T.i]
    ring_band(plain, -.12, 0, 20.8, F['crack'], sides)
    # the ash beyond: low dunes rising to the horizon
    nr, na = (10, 6, 5)[T.i], (64, 32, 20)[T.i]
    P = np.zeros((nr, na, 3))
    for i in range(nr):
        r = 19.5 + (150 - 19.5) * (i / (nr - 1)) ** 1.5
        for j in range(na):
            a = 2 * math.pi * j / na
            y = FLOOR - .2 + (i > 0) * (1.2 * (1 + n1(math.cos(a) * r * .06, 0, math.sin(a) * r * .06, .8, 91, 3) * 2) * min(1, (r - 19.5) / 25) + .02 * r)
            P[i, j] = (math.cos(a) * r, y, math.sin(a) * r)
    surf(plain, P, F['ash'], wrap_j=True, facing=(0, 1, 0), pick=lambda nn, cc: F['ash'] if nn[1] > .9 else F['ash-dark'])
    # the monoliths: leaning slabs on stepped bases, a broken top, glyphs down their inner faces (rig darkness/glyphs/k)
    glyphs = []
    for i in range(len(DARK_MONOLITHS)):
        at = monolith_at(i)
        a = math.atan2(at[0], at[2]) + math.pi  # its inner face toward the centre: local +z
        h = 6.2 + 2.2 * H(i, 11)
        m = Geo()
        sk.box(m, -1.3, 1.3, -.3, .2, -.8, .8, F['basalt'], T, bevel=.05)
        sk.box(m, -1.0, 1.0, .2, .5, -.6, .6, F['basalt'], T, bevel=.04)
        outline = ccw([(-.7, -.3), (.7, -.3), (.7, .3), (-.7, .3)])
        prism(m, outline, .5, h, F['basalt'], F['basalt'], (S(i, 12) * .35, 0), chamfer=.04 if T.bevel else 0)
        g = Geo()
        rows = (9, 6, 4)[T.i]
        zf = .306

        def stroke(x0, y0, x1, y1, w=.022):
            dx, dy = x1 - x0, y1 - y0
            l = math.hypot(dx, dy) or 1.0
            nx, ny = -dy / l * w, dx / l * w
            g.face([(x0 + nx, y0 + ny, zf), (x1 + nx, y1 + ny, zf), (x1 - nx, y1 - ny, zf), (x0 - nx, y0 - ny, zf)], F['glyph'], normal=(0, 0, 1))
        # a single column of runes down the inner face: a stave and its ticks, cut into the stone
        for k in range(rows):
            y0 = 1.3 + k * (h - 2.2) / rows
            hh = (h - 2.2) / rows * .7
            stroke(0, y0, 0, y0 + hh)
            for c in range(1 + int(H(i, k, 1) * 2.4)):
                ty = y0 + hh * (.15 + .7 * H(i, k, c + 2))
                side = 1 if H(i, k, c + 5) > .5 else -1
                up = .08 + .1 * H(i, k, c + 7)
                stroke(0, ty, side * (.16 + .08 * H(i, k, c + 9)), ty + up * (1 if H(i, k, c + 11) > .4 else -1))
        lean = qmul(sk.quat_axis(0, 1, 0, a), sk.quat_axis(1, 0, 0, -.06 - .05 * H(i, 13)))
        stones.extend(m.transformed(sk.affine(lean, at)))
        glyphs.append(g.transformed(sk.affine(lean, at)))
    # the gate behind the mark, framing the sun: two pillars, a broken lintel
    ga = math.atan2(FWD[0], FWD[2])
    gate = Geo()
    for side in (-1, 1):
        x0 = side * 3.3
        y = -.3
        for c in range(6):
            hgt = 1.6 + .25 * H(side + 9, c, 1)
            w = .85 - .03 * c
            sk.box(gate, x0 - w, x0 + w, y, y + hgt - .02, -.75, .75, F['gate'], T, bevel=.05)
            y += hgt
        sk.box(gate, x0 - 1.1, x0 + 1.1, -.3, .4, -1.0, 1.0, F['basalt'], T, bevel=.05)
        if side == -1:
            top = y
    sk.box(gate, -4.3, .6, top, top + 1.2, -.8, .8, F['gate'], T, bevel=.08)
    chunk = Geo()
    sk.box(chunk, -1.2, 1.2, -.5, .5, -.7, .7, F['gate'], T, bevel=.08)
    gate.extend(chunk.transformed(sk.affine(qmul(sk.quat_axis(0, 1, 0, .4), sk.quat_axis(0, 0, 1, .5)), (1.6, .45, 1.2))))
    stones.extend(yawed(gate, ga, on_line(DARK_GATE_Z)))
    # obsidian shards jutting from the ground among the monoliths
    for i in range((16, 9, 5)[T.i]):
        a = 2 * math.pi * H(i, 21)
        r = 7 + 9 * H(i, 22)
        b = (math.sin(a) * r, -.1, math.cos(a) * r)
        if abs(math.atan2(b[0], b[2]) - FACING) < .35:
            continue
        tip = (b[0] + S(i, 23) * .5, .6 + 1.6 * H(i, 24), b[2] + S(i, 25) * .5)
        sh = Geo()
        sk.lathe(sh, [(.22 + .15 * H(i, 26), 0), (0, 1)], (0, 0, 0), F['obsidian'], 5, smooth=False)
        L = math.dist(b, tip)
        d = tuple((tip[k] - b[k]) / L for k in range(3))
        ax = sk._norm(sk._cross((0, 1, 0), d))
        ang = math.acos(max(-1, min(1, d[1])))
        q = sk.quat_axis(*ax, ang) if ang > 1e-4 else (0, 0, 0, 1)
        stones.extend(sh.transformed(sk.affine(q, b, (1, L, 1))))
    # the far mesas
    for i in range((10, 7, 5)[T.i]):
        a = 2 * math.pi * (i + .6 * H(i, 31)) / 10
        r = 95 + 70 * H(i, 32)
        base = (math.sin(a) * r, -1, math.cos(a) * r)
        w = 14 + 16 * H(i, 33)
        hgt = 9 + 14 * H(i, 34)
        me = Geo()
        k = 8
        ring0 = [(math.cos(2 * math.pi * j / k) * w * (.8 + .35 * H(i, j, 35)), 0, math.sin(2 * math.pi * j / k) * w * .55 * (.8 + .35 * H(i, j, 36))) for j in range(k)]
        ring1 = [(x * .82, hgt, z * .82) for x, y, z in ring0]
        for j in range(k):
            j1 = (j + 1) % k
            me.face([ring0[j], ring0[j1], ring1[j1], ring1[j]], F['mesa'], normal=(ring0[j][0] + ring0[j1][0], 1, ring0[j][2] + ring0[j1][2]))
        me.face(ring1[::-1], F['mesa'], normal=(0, 1, 0))
        far.extend(yawed(me, a, base))
    # the moon: a black disc facing +z (Core turns it to face the lens and slides it over the sun)
    moon = Geo()
    n = (48, 32, 20)[T.i]
    moon.face([(math.cos(2 * math.pi * k / n) * DARK_MOON_R, math.sin(2 * math.pi * k / n) * DARK_MOON_R, 0) for k in range(n)], F['moon'], normal=(0, 0, 1))
    props = {'darkness/plain': plain, 'darkness/stones': stones, 'darkness/far': far, 'darkness/moon': moon}
    for i, g in enumerate(glyphs):
        props['darkness/glyphs/%d' % i] = g
    return props


# ---------------------------------------------------------------------------------------------------------------------------
# Bank writing and previews
# ---------------------------------------------------------------------------------------------------------------------------
def fire_props(T):
    statics, rigs = fire_set(T)
    out = {'fire/' + k: g for k, g in statics.items()}
    out.update({'fire/' + k: g for k, g in rigs.items()})
    return out


BUILDERS = {'fire': fire_props, 'water': water_set, 'lightning': lightning_set, 'darkness': darkness_set}
TIERS = ((0, ''), (2, '/low'))   # docs/INTROS-V2.md: "<prop>" the Fine tier, "<prop>/low" the Low tier


def bake(geos, ao, floors):
    """Bake (colours, channels) per Geo; AO over the set's whole scene when `ao` (the moving parts each by itself)."""
    keys = [k for k, g in geos.items() if g.faces]
    statics = [k for k in keys if k.count('/') == 1]
    aos = dict(zip(statics, sk.bake_ao([geos[k] for k in statics], distance=1.4, samples=16))) if ao and statics else {}
    out = {k: sk.Bake(geos[k], aos.get(k), floors=floors, seed=sum(map(ord, k)) & 1023) for k in keys}
    for k in keys:
        if k.startswith('water/wave/'):
            wave_colours(out[k], geos[k])
    return out


def build(sets, ao, preview_dir=None, out=None, stats=False):
    bank = sk.StageBank()
    bank.far_layers = False
    t0 = time.time()
    total = 0
    for name in sets:
        for ti, suffix in TIERS:
            T = sk.Tier(ti)
            props = BUILDERS[name](T)
            floors = (FLOOR, FIRE_FLOOR_Y) if name == 'fire' else (FLOOR,)
            # source frame -> the bank's Unity space
            geos = {k: mirrored(g) for k, g in props.items()}
            bakes = bake(geos, ao, floors)
            for k, bk in bakes.items():
                om = sk.to_mesh(k + suffix, geos[k], bk)
                group = 'static' if k.count('/') == 1 else 'rig'
                bank.add(k + suffix, group, om)
                if om is not None:
                    total += len(om.positions)
                    if stats:
                        print('  %-30s %7d vertices %7d triangles' % (om.name, len(om.positions), sum(len(x) for x in om.submeshes) // 3))
            if preview_dir and ti == 0:
                views = [(v, (-e[0], e[1], e[2]), (-l[0], l[1], l[2]), f) for v, e, l, f in PREVIEWS.get(name, [])]
                placed = [k for k in bakes if not any(k.startswith(x) for x in ('water/wave', 'water/frozen', 'water/ice', 'water/surface', 'darkness/moon'))]
                sk.preview([(geos[k], bakes[k]) for k in placed], views, os.path.join(preview_dir, name), size=(960, 540))
            print('%s %s: %.1f s' % (name, T.name, time.time() - t0))
    print('vertices in all', total)
    path = None
    if out:
        path = write(bank, out)
    for (group, tier), (n, tris, verts) in sorted(bank.stats.items()):
        print('%-7s %-5s %4d props %8d triangles %8d vertices' % (group, tier, n, tris, verts))
    return path


def write(bank, out):
    """The bank, straight into `out` (a folder; never the project's Assets: it is never committed)."""
    bank.layer()
    os.makedirs(out, exist_ok=True)
    path = os.path.join(out, BANK + '.bytes')
    data = bank.bank.write()
    with open(path, 'wb') as fh:
        fh.write(data)
    print('wrote', path, len(data), 'bytes')
    return path


# preview views in the source frame: (name, eye, target, fov)
PREVIEWS = {
    'fire': [('hero', (-.8, 1.1, 4.4), (0, 1.2, -2), 40), ('wide', (-4, 4.5, 14), (0, 2, -6), 55), ('low', (.4, .3, 2.5), (0, 3.5, -8), 50), ('top', (0, 40, 1), (0, 0, 0), 60)],
    'water': [('hero', (-1.0, 1.0, 4.6), (0, 1.3, 0), 34), ('wide', (-1.8, 1.8, 7.5), (0, 3, -10), 56), ('up', (-.5, .4, 3.0), (0, 6, -6), 70), ('back', (0, 2, -18), (0, 3, 6), 60)],
    'lightning': [('hero', (-1.2, 1.0, 3.6), (0, 1.2, 0), 40), ('wide', (-4.5, 1.2, 8), (0, 2.6, 0), 58), ('top', (0, 18, 6), (0, 0, 0), 44), ('low', (-3, .3, 3.5), (0, 3, -6), 60)],
    'darkness': [('hero', (-.5, .9, 4.8), (0, 1.3, 0), 32), ('gate', (-2.7, 1.6, 12.2), (5, 4.5, -20), 46), ('low', (-1.1, .3, 5.0), (3, 4.6, -10), 50), ('wide', (8, 6, 18), (0, 1, -4), 60)],
}


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out')
    ap.add_argument('--sets', default=','.join(SETS))
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--preview')
    ap.add_argument('--stats', action='store_true')
    a = ap.parse_args(argv)
    build([s for s in a.sets.split(',') if s], not a.no_ao, a.preview, a.out, a.stats)


if __name__ == '__main__':
    main()
