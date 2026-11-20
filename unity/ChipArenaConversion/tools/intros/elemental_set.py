"""The elemental weapon intros' sets (docs/INTROS-ELEMENTAL.md): four modelled backdrops for Forgeborn (fire), Still Tide
(water), Strike Point (lightning) and Black Sun (darkness), written into one local bank, ChipArenaIntroElemental (never
committed; the intros draw the framework's dark lit void without it). Run inside Blender 5.x (bpy, numpy) with the stage kit:

  blender --background --factory-startup --python tools/intros/elemental_sets.py -- --out DIR
      [--sets fire,water,lightning,darkness] [--tiers 0,1,2] [--no-ao] [--preview DIR] [--stats]

Everything is in the game's space (Unity: metres, +y up), the pilot's hero mark at the origin facing +z (the intros' cameras
mostly stand on +z looking back at -z, so each set's best side is its -z side). The floor the pilot stands on is
`FLOOR` (Core NativeEntranceStage.FloorY). Pieces that Core moves or lights over time are props of their own ('rig'): the lava
rings, the frozen and liquid walls, the ice sheet's rings, the spires' seams, the monoliths' glyphs, the sun and the moon. Core
`NativeElementalIntros` holds the same numbers (river paths, ring radii, spire and monolith places): change both together.

Bank naming (one bank, four sets): `<set>/static/<chunk>/<tier>` for the scenery, `<set>/rig/<name>/<tier>` for the parts Core
poses or lights. Finishes and the bank format are the stage kit's (tools/stages/stagekit.py, tools/pack_bank/bank.py).
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
    sk.lathe(near, [(.64, -.9), (.52, -1.7)], (vx, 0, vz), F['lava-hot'], sides, cap1=True)
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
        k = (16, 10, 6)[T.i]
        for j in range(k):
            pts = []
            for u, w in ((j / k, -.7), (j / k, .7), ((j + 1) / k, .7), ((j + 1) / k, -.7)):
                y = FIRE_FLOOR_Y + 15 * (1 - u)
                r = 24.6 + 3.4 * (1 - u) ** 1.4 - .6
                aa = a + w * (.55 + .5 * u) / r
                pts.append((math.cos(aa) * r, y, math.sin(aa) * r))
            falls.face(pts, F['lava'], normal=(-math.cos(a), .3, -math.sin(a)))
    rigs['falls'] = falls
    return {'near': near, 'mid': mid, 'far': far}, rigs


# ---------------------------------------------------------------------------------------------------------------------------
# Bank writing and previews
# ---------------------------------------------------------------------------------------------------------------------------
BUILDERS = {'fire': fire_set}


def bake(geos, ao, floors):
    """Bake (colours, channels) per Geo; AO over the set's whole scene when `ao`."""
    keys = [k for k, g in geos.items() if g.faces]
    aos = sk.bake_ao([geos[k] for k in keys], distance=1.4, samples=20) if ao else [None] * len(keys)
    return {k: sk.Bake(geos[k], a, floors=floors, seed=hash(k) & 1023) for k, a in zip(keys, aos)}


def build(sets, tiers, ao, preview_dir=None, out=None, stats=False):
    bank = sk.StageBank()
    bank.far_layers = False
    t0 = time.time()
    for name in sets:
        for ti in tiers:
            T = sk.Tier(ti)
            statics, rigs = BUILDERS[name](T)
            geos = {('static', k): g for k, g in statics.items()}
            geos.update({('rig', k): g for k, g in rigs.items()})
            floors = (FLOOR, FIRE_FLOOR_Y) if name == 'fire' else (FLOOR,)
            bakes = bake(geos, ao, floors)
            for (group, k), bk in bakes.items():
                om = sk.to_mesh('%s/%s/%s/%s' % (name, group, k, T.name), geos[(group, k)], bk)
                bank.add('%s/%s/%s/%s' % (name, group, k, T.name), group, om)
                if stats and om is not None:
                    print('  %-34s %7d vertices %7d triangles' % (om.name, len(om.positions), sum(len(x) for x in om.submeshes) // 3))
            if preview_dir and ti == tiers[0]:
                views = PREVIEWS.get(name, [])
                sk.preview([(geos[k], bakes[k]) for k in bakes], views, os.path.join(preview_dir, name), size=(960, 540))
            print('%s %s: %.1f s' % (name, T.name, time.time() - t0))
    path = None
    if out:
        path = write(bank, out)
    if stats or not out:
        for (group, tier), (n, tris, verts) in sorted(bank.stats.items()):
            print('%-7s %-5s %4d props %8d triangles %8d vertices' % (group, tier, n, tris, verts))
    return path


def write(bank, out):
    """The bank, straight into `out` (a folder; not the project's Assets: it is never committed)."""
    bank.layer()
    os.makedirs(out, exist_ok=True)
    path = os.path.join(out, BANK + '.bytes')
    data = bank.bank.write()
    with open(path, 'wb') as fh:
        fh.write(data)
    print('wrote', path, len(data), 'bytes')
    return path


PREVIEWS = {
    'fire': [('hero', (1.2, 1.1, 4.2), (0, 1.2, -2), 40), ('wide', (6, 4.5, 14), (0, 2, -6), 55), ('low', (0.4, .3, 2.5), (0, 3.5, -8), 50),
             ('back', (-3, 2.5, -6), (0, 1, 6), 55), ('top', (0, 40, 1), (0, 0, 0), 60)],
}


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out')
    ap.add_argument('--sets', default=','.join(SETS))
    ap.add_argument('--tiers', default='0,1,2')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--preview')
    ap.add_argument('--stats', action='store_true')
    a = ap.parse_args(argv)
    sets = [s for s in a.sets.split(',') if s]
    tiers = [int(t) for t in a.tiers.split(',')]
    build(sets, tiers, not a.no_ao, a.preview, a.out, a.stats)


if __name__ == '__main__':
    main()
