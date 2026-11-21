"""The melee intros' set kit (docs/INTROS-MELEE.md): shapes the seven sets share, on top of the stage kit (tools/stages/stagekit.py:
finishes, tiers, corner geometry, the AO and colour bakes, the bank). Run inside Blender 5.x (bpy, numpy) through melee_bank.py.

Every set is authored in its own SET FRAME, Unity space (metres, +y up): the origin is the pilot's hero spot on the floor (y 0 the
floor's top), the pilot faces +z and the intro's opening camera stands on the +z side; seen from there, -x is to the right (Unity is
left handed; the entrance scripts' source frame is this with x negated).
"""
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'stages'))
import stagekit as sk  # noqa: E402

Geo, Tier, fin = sk.Geo, sk.Tier, sk.fin


def rng(seed):
    return np.random.default_rng(seed)


# ---------------------------------------------------------------------------------------------------------------------------
# Rocks and broken stone: a displaced icosphere, flat shaded (each face its own normal: carved, not blobby)
# ---------------------------------------------------------------------------------------------------------------------------
_ICO = {}


def icosphere(level):
    if level in _ICO:
        return _ICO[level]
    t = (1 + math.sqrt(5)) / 2
    v = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t), (t, 0, -1), (t, 0, 1),
         (-t, 0, -1), (-t, 0, 1)]
    f = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
         (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    v = [np.array(p, dtype=np.float64) / np.linalg.norm(p) for p in v]
    for _ in range(level):
        mid = {}
        nf = []

        def m(a, b):
            k = (min(a, b), max(a, b))
            if k not in mid:
                p = v[a] + v[b]
                v.append(p / np.linalg.norm(p))
                mid[k] = len(v) - 1
            return mid[k]
        for a, b, c in f:
            ab, bc, ca = m(a, b), m(b, c), m(c, a)
            nf += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        f = nf
    _ICO[level] = (np.array(v), np.array(f))
    return _ICO[level]


def rock(g, centre, size, f, T, seed=0, rough=.28, flat_bottom=True, sink=.0, turn=0.0, level=None, facets=True):
    """A rock (size: half extents x, y, z), its surface pushed in and out by noise; flat bottomed at its centre's height minus
    `sink` (so it sits on the floor). Flat shaded facets on Fine and Mid; the Low tier one subdivision coarser."""
    if level is None:
        big = max(size) > .45
        lv = (2 if big else 1, 1, 1 if big else 0)[T.i]
    else:
        lv = level
    V, F = icosphere(lv)
    V = V.copy()
    n = sk.fbm(V * 1.7 + seed * 3.1, 1.0, 3, seed) - .5
    k = 1 + rough * 2 * n
    V = V * k[:, None]
    c, s = math.cos(turn), math.sin(turn)
    P = np.stack([V[:, 0] * size[0], V[:, 1] * size[1], V[:, 2] * size[2]], axis=1)
    P = np.stack([P[:, 0] * c + P[:, 2] * s, P[:, 1], -P[:, 0] * s + P[:, 2] * c], axis=1)
    if flat_bottom:
        P[:, 1] = np.maximum(P[:, 1], -sink)
    P = P + np.array(centre)
    for a, b, cc in F:
        pa, pb, pc = tuple(P[a]), tuple(P[b]), tuple(P[cc])
        if flat_bottom and P[a][1] <= centre[1] - sink + 1e-6 and P[b][1] <= centre[1] - sink + 1e-6 and P[cc][1] <= centre[1] - sink + 1e-6:
            continue
        if facets:
            g.face((pa, pb, pc), f)
        else:
            na, nb, nc = (tuple(_unit(P[i] - np.array(centre))) for i in (a, b, cc))
            g.face((pa, pb, pc), f, normals=(na, nb, nc))


def _unit(v):
    l = float(np.linalg.norm(v))
    return v / l if l > 1e-9 else np.array([0.0, 1.0, 0.0])


def slab(g, poly, y0, y1, f, T, bevel=.025, top_f=None, tilt=None):
    """A prism over a convex polygon `poly` [(x, z)] (counter-clockwise or not) from y0 to y1, its top edge chamfered by `bevel`
    (none on Low). tilt: (axis point (x, y, z), axis (x, y, z), angle) turns it (a lifted floor slab)."""
    pts = list(poly)
    area = sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))
    if area < 0:
        pts = pts[::-1]
    cx = sum(p[0] for p in pts) / len(pts)
    cz = sum(p[1] for p in pts) / len(pts)
    b = bevel if T.bevel else 0.0
    inner = [(cx + (p[0] - cx) * (1 - b / max(.05, math.hypot(p[0] - cx, p[1] - cz))), cz + (p[1] - cz) * (1 - b / max(.05, math.hypot(p[0] - cx, p[1] - cz)))) for p in pts]
    t = Geo()
    tf = f if top_f is None else top_f
    yt = y1 - b
    top = [(p[0], y1, p[1]) for p in inner]
    t.face(top, tf, normal=(0, 1, 0))
    k = len(pts)
    for i in range(k):
        j = (i + 1) % k
        a0, a1 = pts[i], pts[j]
        e = (a1[0] - a0[0], a1[1] - a0[1])
        nrm = sk._norm((e[1], 0, -e[0]))
        if sk._dot(nrm, (a0[0] - cx, 0, a0[1] - cz)) < 0:
            nrm = (-nrm[0], 0, -nrm[2])
        t.face(((a0[0], y0, a0[1]), (a1[0], y0, a1[1]), (a1[0], yt, a1[1]), (a0[0], yt, a0[1])), f, normal=nrm)
        if b > 0:
            i0, i1 = inner[i], inner[j]
            t.face(((a0[0], yt, a0[1]), (a1[0], yt, a1[1]), (i1[0], y1, i1[1]), (i0[0], y1, i0[1])), f,
                   normals=(nrm, nrm, (0, 1, 0), (0, 1, 0)))
    if tilt is not None:
        (px, py, pz), ax, ang = tilt
        t = t.transformed(about((px, py, pz), sk.quat_axis(ax[0], ax[1], ax[2], ang)))
    g.extend(t)


def about(p, q):
    """The 3x4 affine turning by quaternion q about the point p."""
    m = sk.affine(q)
    R = np.array(m, dtype=np.float64).reshape(3, 4)[:, :3]
    off = np.array(p, dtype=np.float64) - R @ np.array(p, dtype=np.float64)
    m[3], m[7], m[11] = float(off[0]), float(off[1]), float(off[2])
    return m


def voronoi_cells(points, bound):
    """Convex cells (lists of (x, z)) of 2D points clipped to the convex polygon `bound`: half-plane clipping, small n."""
    cells = []
    for i, p in enumerate(points):
        cell = list(bound)
        for j, q in enumerate(points):
            if i == j:
                continue
            mx, mz = (p[0] + q[0]) / 2, (p[1] + q[1]) / 2
            nx, nz = q[0] - p[0], q[1] - p[1]
            out = []
            for k in range(len(cell)):
                a, b = cell[k], cell[(k + 1) % len(cell)]
                da = (a[0] - mx) * nx + (a[1] - mz) * nz
                db = (b[0] - mx) * nx + (b[1] - mz) * nz
                if da <= 0:
                    out.append(a)
                if (da <= 0) != (db <= 0):
                    u = da / (da - db)
                    out.append((a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u))
            cell = out
            if len(cell) < 3:
                break
        cells.append(cell)
    return cells


def shrink(poly, d):
    """The convex polygon moved in by d (a joint's half width) toward its centroid along each corner's bisector (approximate)."""
    cx = sum(p[0] for p in poly) / len(poly)
    cz = sum(p[1] for p in poly) / len(poly)
    out = []
    for p in poly:
        r = math.hypot(p[0] - cx, p[1] - cz)
        k = max(0.0, 1 - d / max(r, 1e-6))
        out.append((cx + (p[0] - cx) * k, cz + (p[1] - cz) * k))
    return out


def circle(cx, cz, r, n, phase=0.0):
    return [(cx + r * math.cos(2 * math.pi * i / n + phase), cz + r * math.sin(2 * math.pi * i / n + phase)) for i in range(n)]


# ---------------------------------------------------------------------------------------------------------------------------
# Plants and cloth
# ---------------------------------------------------------------------------------------------------------------------------
def blade_leaf(g, base, direction, length, width, f, droop=.35, twist=0.0):
    """A long narrow leaf (two-sided, three segments) from `base` along `direction`, drooping under its weight."""
    d = sk._norm(direction)
    side = sk._norm(sk._cross(d, (0, 1, 0))) if abs(d[1]) < .95 else (1, 0, 0)
    side = (side[0] * math.cos(twist), side[1], side[2] * math.cos(twist))
    pts = []
    for i in range(4):
        u = i / 3
        p = sk._add(base, d, length * u)
        p = (p[0], p[1] - droop * length * u * u, p[2])
        w = width * math.sin(math.pi * min(.999, .15 + .85 * u)) * (1 - .6 * u)
        pts.append((p, w))
    for i in range(3):
        (p0, w0), (p1, w1) = pts[i], pts[i + 1]
        a, b = sk._add(p0, side, -w0), sk._add(p0, side, w0)
        c, e = sk._add(p1, side, w1), sk._add(p1, side, -w1)
        up = sk._norm(sk._cross(sk._sub(b, a), sk._sub(e, a)))
        if up[1] < 0:
            up = (-up[0], -up[1], -up[2])
        g.face((a, b, c, e), f, normal=up)
        g.face((e, c, b, a), f, normal=(-up[0], -up[1], -up[2]))


def cloth(g, top_a, top_b, length, f, T, seed=0, sway=.12, tear=.0, cells=None):
    """A hanging cloth (a banner) from the line top_a..top_b, `length` down, gently waved; two-sided; its lower edge ragged by
    `tear` (m)."""
    nu = (8, 5, 3)[T.i] if cells is None else cells[0]
    nv = (10, 6, 3)[T.i] if cells is None else cells[1]
    r = rng(seed)
    rag = r.uniform(0, tear, nu + 1)
    across = sk._sub(top_b, top_a)
    nrm0 = sk._norm(sk._cross(across, (0, -1, 0)))
    grid = []
    for j in range(nv + 1):
        v = j / nv
        row = []
        for i in range(nu + 1):
            u = i / nu
            p = sk._add(top_a, across, u)
            down = length * v - (rag[i] * v * v)
            wave = sway * math.sin(u * 5.2 + v * 3.1 + seed) * v
            p = (p[0] + nrm0[0] * wave, p[1] - down, p[2] + nrm0[2] * wave)
            row.append(p)
        grid.append(row)
    for j in range(nv):
        for i in range(nu):
            a, b, c, d = grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]
            n = sk._norm(sk._cross(sk._sub(b, a), sk._sub(d, a)))
            if sk._dot(n, nrm0) < 0:
                n = (-n[0], -n[1], -n[2])
            g.face((a, b, c, d), f, normal=n)
            g.face((d, c, b, a), f, normal=(-n[0], -n[1], -n[2]))


# ---------------------------------------------------------------------------------------------------------------------------
# A set: its static scene and its moving parts (rigs) per tier, banked under its intro's name
# ---------------------------------------------------------------------------------------------------------------------------
class SetPiece:
    """One set's tiers: static[tier] a Geo (the set frame), rigs[name][tier] (Geo in the rig's own frame, rest[name] its rest pose:
    a 3x4 affine from the rig frame into the set frame)."""

    def __init__(self, name):
        self.name = name
        self.static = {}
        self.rigs = {}
        self.rest = {}
        self.views = []

    def rig(self, name, ti, g, rest=None):
        self.rigs.setdefault(name, {})[ti] = g
        if rest is not None:
            self.rest[name] = rest


def translate(x, y, z):
    return [1.0, 0, 0, x, 0, 1.0, 0, y, 0, 0, 1.0, z]


def moved(g, m):
    return g.transformed(m)


class Tally:
    """Corners added per section of a set (for the budget): call it after each section with its name."""

    def __init__(self, g):
        self.g = g
        self.last = 0
        self.rows = []

    def __call__(self, name):
        n = len(self.g.p) // 3
        self.rows.append((name, n - self.last))
        self.last = n

    def report(self):
        return ', '.join('%s %d' % r for r in sorted(self.rows, key=lambda r: -r[1]))
