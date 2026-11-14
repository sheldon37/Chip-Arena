"""Top Dog Emporium's modelling helpers (docs/arenas/STAGE-ART.md, docs/arenas/EMPORIUM.md "Stage art"), on stagekit: the finishes
(by kit kind and colour), skinned and rounded shapes (capsules, ellipsoids, lofts of rounded sections, part rounds), a block's
faces in the kit's own terms (side, u along the face, v up it, d proud of it), hazard stripes, neon tubes, and the fallback that
rebuilds any kit part as a finished piece. Used by emporium_art.py (the hall) and emporium_props.py (the rigs and the breakable
blocks). Everything is in the game's space (Unity: metres, +y up)."""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, obox  # noqa: E402

F = {}
KINDS = []        # kit kind index -> short name ("chrome", "tube", ...)
KCOL = []         # kit kind index -> the look's colour

# kind -> (detail, smooth, metal, dirt, wear, param)
A = sk
SURF = {
    'dark': (A.DETAIL_NONE, .4, 0, .35, 0, 0), 'chrome': (A.DETAIL_NONE, .9, 1, .12, 0, 0), 'red': (A.DETAIL_NONE, .66, 0, .25, 0, 0),
    'yellow': (A.DETAIL_NONE, .66, 0, .25, 0, 0), 'cream': (A.DETAIL_NONE, .5, 0, .3, 0, 0), 'teal': (A.DETAIL_TILE, .78, 0, .25, 0, .15),
    'pink': (A.DETAIL_NONE, .6, 0, .25, 0, 0), 'wood': (A.DETAIL_WOOD, .35, 0, .5, .2, 0), 'sausage': (A.DETAIL_NONE, .62, 0, .2, 0, 0),
    'sausage-dark': (A.DETAIL_NONE, .45, 0, .2, 0, 0), 'bun': (A.DETAIL_NONE, .22, 0, .3, 0, 0), 'bun-top': (A.DETAIL_NONE, .3, 0, .3, 0, 0),
    'sesame': (A.DETAIL_NONE, .3, 0, .1, 0, 0), 'tile': (A.DETAIL_TILE, .7, 0, .3, 0, .3), 'checker': (A.DETAIL_NONE, .82, 0, .2, 0, 0),
    'quarry': (A.DETAIL_TILE, .6, 0, .3, 0, .3), 'roller': (A.DETAIL_NONE, .82, 1, .2, 0, 0), 'grate': (A.DETAIL_IRON, .4, .8, .3, 0, 0),
    'griddle': (A.DETAIL_IRON, .45, .8, .3, 0, 0), 'rubber': (A.DETAIL_NONE, .2, 0, .3, 0, 0), 'pickle': (A.DETAIL_NONE, .6, 0, .2, 0, 0),
    'mustard': (A.DETAIL_NONE, .74, 0, .12, 0, 0), 'ketchup': (A.DETAIL_NONE, .74, 0, .12, 0, 0), 'white': (A.DETAIL_NONE, .6, 0, .2, 0, 0),
    'iron': (A.DETAIL_NONE, .5, .55, .3, .2, 0), 'mint': (A.DETAIL_TILE, .8, 0, .25, 0, .1), 'vinyl': (A.DETAIL_NONE, .66, 0, .25, 0, 0),
    'soda': (A.DETAIL_NONE, .8, 0, .15, 0, 0), 'relish': (A.DETAIL_NONE, .6, 0, .2, 0, 0), 'onion': (A.DETAIL_NONE, .45, 0, .2, 0, 0),
    'stripe': (A.DETAIL_NONE, .45, 0, .3, 0, 0), 'glass-dark': (A.DETAIL_NONE, .92, .2, .1, 0, 0),
}
GLOW = {'tube': 2.4, 'lamp': 2.0, 'glass': 1.15, 'screen': 1.6}


def setup(L):
    KINDS[:] = [k[len('emporium-kit-'):] for k in L['kinds']]
    KCOL[:] = L['kindColors']


def mat(name, hexcol, detail=sk.DETAIL_NONE, smooth=.5, metal=0.0, dirt=.3, wear=0.0, param=0.0, glow=0.0):
    """A finish by name (made once)."""
    if name not in F:
        F[name] = sk.fin(name, hexcol, detail, smooth=smooth, metal=metal, dirt=dirt, glow=glow, param=param, wear=wear)
    return F[name]


def kmat(kind, col=None, gain=None):
    """The finish of a kit kind (its look's colour, or `col`); a glowing kind takes `gain` (the part's HDR gain)."""
    name = KINDS[kind] if isinstance(kind, int) else kind
    if col is None or col < 0:
        col = KCOL[KINDS.index(name)]
    if name in GLOW:
        g = GLOW[name] if (gain is None or gain <= 1.0) else float(gain)
        if name == 'screen':
            g = max(1.3, g)
        return mat('%s-%06x-%.2f' % (name, col, g), col, glow=g)
    d, s, m, dirt, wear, param = SURF.get(name, (sk.DETAIL_NONE, .5, 0, .3, 0, 0))
    return mat('%s-%06x' % (name, col), col, d, s, m, dirt, wear, param)


def pfin(p):
    return kmat(p['k'], p['col'], p.get('w', 1.0))


def shade(hexcol, k):
    return (min(255, int((hexcol >> 16 & 255) * k)) << 16) | (min(255, int((hexcol >> 8 & 255) * k)) << 8) | min(255, int((hexcol & 255) * k))


# ---------------------------------------------------------------------------------------------------------------------------
# Skinned shapes
# ---------------------------------------------------------------------------------------------------------------------------
def skin(g, rings, f, closed=True, flip=False):
    """Quads between successive rings of points (each ring the same count; a ring may collapse to a pole), shaded smooth:
    the normals come from the surface itself (finite differences), turned outward from the rings' centres."""
    P = np.array(rings, dtype=np.float64)                    # (m, k, 3)
    m, k = P.shape[0], P.shape[1]
    if closed:
        du = np.roll(P, -1, axis=1) - np.roll(P, 1, axis=1)
    else:
        du = np.empty_like(P)
        du[:, 1:-1] = P[:, 2:] - P[:, :-2]
        du[:, 0] = P[:, 1] - P[:, 0]
        du[:, -1] = P[:, -1] - P[:, -2]
    dv = np.empty_like(P)
    dv[1:-1] = P[2:] - P[:-2]
    dv[0] = P[1] - P[0]
    dv[-1] = P[-1] - P[-2]
    N = np.cross(du, dv)
    ln = np.linalg.norm(N, axis=2, keepdims=True)
    cen = P.mean(axis=1, keepdims=True)
    good = ln[:, :, 0] > 1e-9
    N = N / np.maximum(ln, 1e-12)
    out = P - cen
    ok = good & (np.linalg.norm(out, axis=2) > 1e-6)
    s = (N * out).sum(axis=2)[ok].sum()
    if (s < 0) != flip:
        N = -N
    # poles and degenerate corners: along the axis, away from the neighbouring ring
    for i in range(m):
        bad = ~good[i]
        if bad.any():
            j = 1 if i == 0 else i - 1
            axis = cen[i, 0] - cen[j, 0]
            la = np.linalg.norm(axis)
            axis = axis / la if la > 1e-9 else np.array([0.0, 1.0, 0.0])
            N[i, bad] = -axis if flip else axis
    cols = k if closed else k - 1
    for i in range(m - 1):
        for j in range(cols):
            j1 = (j + 1) % k
            a, b, c, d = P[i, j], P[i, j1], P[i + 1, j1], P[i + 1, j]
            na, nb, nc, nd = N[i, j], N[i, j1], N[i + 1, j1], N[i + 1, j]
            if np.abs(a - b).max() < 1e-7:
                g.face((tuple(a), tuple(c), tuple(d)), f, normals=(tuple(na), tuple(nc), tuple(nd)))
            elif np.abs(c - d).max() < 1e-7:
                g.face((tuple(a), tuple(b), tuple(c)), f, normals=(tuple(na), tuple(nb), tuple(nc)))
            else:
                g.face((tuple(a), tuple(b), tuple(c), tuple(d)), f, normals=(tuple(na), tuple(nb), tuple(nc), tuple(nd)))


def capsule(g, p0, p1, r, f, sides=8, caps=2, squash=(1.0, 1.0), up=None, r1=None, bulge=0.0, body=1):
    """A round-ended rod from p0 to p1 (the ends reach r beyond them unless caps is 0: then flat-ish cones), its section squashed
    (across the frame's u, v); r1: the far end's radius; bulge: fatter in the middle; body: rings along the straight part."""
    u, v, w = sk.frame(p0, p1, up)
    L = math.sqrt(sk._dot(sk._sub(p1, p0), sk._sub(p1, p0)))
    r1 = r if r1 is None else r1
    prof = []                                               # (radius, along)
    n = max(1, caps)
    for i in range(n + 1):
        a = math.pi / 2 * i / n
        prof.append((r * math.sin(a), -r * math.cos(a) if caps else -r * .25 * (1 - i / n)))
    for i in range(1, body + 1):
        t = i / body
        prof.append(((r + (r1 - r) * t) * (1 + bulge * math.sin(math.pi * t)), L * t))
    for i in range(1, n + 1):
        a = math.pi / 2 * (1 - i / n)
        prof.append((r1 * math.sin(a), L + (r1 * math.cos(a) if caps else r1 * .25 * i / n)))
    rings = []
    for rad, h in prof:
        c = sk._add(p0, w, h)
        rings.append([sk._add(sk._add(c, u, rad * squash[0] * math.cos(2 * math.pi * k / sides)), v, rad * squash[1] * math.sin(2 * math.pi * k / sides)) for k in range(sides)])
    skin(g, rings, f)


def ellipsoid(g, c, rx, ry, rz, f, sides=10, stacks=6, y0=-1.0, y1=1.0):
    """An ellipsoid about c (y its axis); y0..y1 the part kept (in units of ry: a dome is 0..1)."""
    rings = []
    a0, a1 = math.asin(max(-1, min(1, y0))), math.asin(max(-1, min(1, y1)))
    for i in range(stacks + 1):
        a = a0 + (a1 - a0) * i / stacks
        rr, h = math.cos(a), math.sin(a)
        rings.append([(c[0] + rx * rr * math.cos(2 * math.pi * k / sides), c[1] + ry * h, c[2] + rz * rr * math.sin(2 * math.pi * k / sides)) for k in range(sides)])
    skin(g, rings, f)
    if y0 > -1 + 1e-6:
        g.face(rings[0], f, normal=(0, -1, 0))


def rrect(w, h, r, n=3):
    """A rounded rectangle's outline (w x h about the origin, corner radius r, n segments a corner), counter clockwise."""
    r = max(1e-4, min(r, w / 2 - 1e-4, h / 2 - 1e-4))
    out = []
    for cx, cy, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            out.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return out


def loft(g, c, levels, f, n=3, cap0=False, cap1=True):
    """A body of rounded-rectangle sections about the vertical through c = (x, z): levels [(y, width x, depth z, corner radius)],
    bottom to top; capped flat."""
    rings = []
    for y, w, d, r in levels:
        rings.append([(c[0] + x, y, c[1] + z) for x, z in rrect(w, d, r, n)])
    skin(g, rings, f)
    if cap0:
        g.face(rings[0], f, normal=(0, -1, 0))
    if cap1:
        g.face(rings[-1], f, normal=(0, 1, 0))


def part_round(g, p0, p1, r, f, U, V, a0=0.0, a1=180.0, k=4, r2=None):
    """Part of a round along a straight run p0 -> p1: the section sweeps from angle a0 to a1 (degrees) in the plane of the unit
    vectors U (0 degrees) and V (90): a half round is a bullnose or a bead on a face, a quarter a rounded edge. r2: the
    radius along V (an elliptic section)."""
    r2 = r if r2 is None else r2
    prev = None
    for i in range(k + 1):
        a = math.radians(a0 + (a1 - a0) * i / k)
        ca, sa = math.cos(a), math.sin(a)
        off = sk._add(sk._lerp((0, 0, 0), U, r * ca), V, r2 * sa)
        nn = sk._norm(sk._add(sk._lerp((0, 0, 0), U, ca / max(r, 1e-6)), V, sa / max(r2, 1e-6)))
        cur = (sk._add(p0, off), sk._add(p1, off), nn)
        if prev is not None:
            g.face((prev[0], prev[1], cur[1], cur[0]), f, normals=(prev[2], prev[2], cur[2], cur[2]))
        prev = cur


def tube(g, path, r, f, sides=5, closed=False, caps=True):
    sk.tube(g, [tuple(p) for p in path], r, f, sides, closed=closed, caps=caps)


def disc(g, c, r, f, n=12, normal=(0, 1, 0), wobble=0.0, seed=0, dome=0.0):
    """A flat (or slightly domed) disc; wobble makes its rim irregular (a splat)."""
    u, v, w = sk.frame((0, 0, 0), normal)
    rim = []
    for i in range(n):
        a = 2 * math.pi * i / n
        rr = r * (1 + wobble * math.sin(a * 3 + seed * 1.7) * .6 + wobble * math.sin(a * 5 + seed * 2.9) * .4)
        rim.append(sk._add(sk._add(c, u, rr * math.cos(a)), v, rr * math.sin(a)))
    top = sk._add(c, w, dome)
    for i in range(n):
        g.face((top, rim[i], rim[(i + 1) % n]), f, normal=normal if dome == 0 else None)


# ---------------------------------------------------------------------------------------------------------------------------
# Blocks and their faces (the kit's own terms)
# ---------------------------------------------------------------------------------------------------------------------------
def ext(b):
    p, s = b['pos'], b['size']
    return (p[0] - s[0] / 2, p[0] + s[0] / 2, p[1] - s[1] / 2, p[1] + s[1] / 2, p[2] - s[2] / 2, p[2] + s[2] / 2)


class Face:
    """A block's face as NativeEmporiumKit sees it: side 0 +x, 1 -x, 2 +z, 3 -z; u runs along it (z on the x faces, x on the z
    faces), v is the world's y, d is metres proud of it."""

    def __init__(self, b, side):
        x0, x1, y0, y1, z0, z1 = ext(b) if isinstance(b, dict) else b
        self.side = side
        self.y0, self.y1 = y0, y1
        if side == 0:
            self.o, self.U, self.N, self.u0, self.u1 = (x1, 0.0, 0.0), (0.0, 0.0, 1.0), (1.0, 0.0, 0.0), z0, z1
        elif side == 1:
            self.o, self.U, self.N, self.u0, self.u1 = (x0, 0.0, 0.0), (0.0, 0.0, 1.0), (-1.0, 0.0, 0.0), z0, z1
        elif side == 2:
            self.o, self.U, self.N, self.u0, self.u1 = (0.0, 0.0, z1), (1.0, 0.0, 0.0), (0.0, 0.0, 1.0), x0, x1
        else:
            self.o, self.U, self.N, self.u0, self.u1 = (0.0, 0.0, z0), (1.0, 0.0, 0.0), (0.0, 0.0, -1.0), x0, x1
        self.V = (0.0, 1.0, 0.0)
        self.uc = (self.u0 + self.u1) / 2

    def P(self, u, v, d=0.0):
        o, U, N = self.o, self.U, self.N
        return (o[0] + U[0] * u + N[0] * d, v, o[2] + U[2] * u + N[2] * d)

    def quad(self, g, u0, u1, v0, v1, d, f, cell=0.0):
        if cell and cell > 0:
            sk.grid_face(g, self.P(u0, v0, d), sk._sub(self.P(u1, v0, d), self.P(u0, v0, d)), (0, v1 - v0, 0), 0, 0, f, self.N, cell)
        else:
            g.face((self.P(u0, v0, d), self.P(u1, v0, d), self.P(u1, v1, d), self.P(u0, v1, d)), f, self.N)

    def slab(self, g, u0, u1, v0, v1, d0, d1, f, T=None, bevel=.01):
        a, b = self.P(u0, v0, d0), self.P(u1, v1, d1)
        box(g, a[0], b[0], a[1], b[1], a[2], b[2], f, T, bevel, cell=0, skip=())

    def bead(self, g, u0, u1, v, r, f, d=0.0, k=4, r2=None):
        """A half round along the face at height v (a dado, a picture rail, a nosing)."""
        part_round(g, self.P(u0, v, d), self.P(u1, v, d), r, f, (0, 1, 0), self.N, 0, 180, k, r2)

    def stile(self, g, u, v0, v1, r, f, d=0.0, k=4, r2=None):
        """A half round up the face at u."""
        part_round(g, self.P(u, v0, d), self.P(u, v1, d), r, f, self.U, self.N, 0, 180, k, r2)

    def path(self, pts):
        return [self.P(*p) for p in pts]

    def frame(self, g, u0, u1, v0, v1, d, r, f, sides=5, corner=.0, n=3):
        """A round bar round a rectangle on the face (its corners rounded by `corner`)."""
        w, h = u1 - u0, v1 - v0
        pts = [((u0 + u1) / 2 + x, (v0 + v1) / 2 + y, d) for x, y in (rrect(w, h, corner, n) if corner > 0 else [(w / 2, h / 2), (-w / 2, h / 2), (-w / 2, -h / 2), (w / 2, -h / 2)])]
        sk.tube(g, self.path(pts), r, f, sides, closed=True)


def facing(b, tx, tz):
    """The side of a block whose outward normal points most toward (tx, tz) (the kit's Toward)."""
    dx, dz = tx - b['pos'][0], tz - b['pos'][2]
    return (0 if dx > 0 else 1) if abs(dx) > abs(dz) else (2 if dz > 0 else 3)


# ---------------------------------------------------------------------------------------------------------------------------
# Hazard stripes, neon, the fallback
# ---------------------------------------------------------------------------------------------------------------------------
def _clip(poly, a, b, c):
    """The part of a 2D polygon where a x + b y <= c."""
    out = []
    n = len(poly)
    for i in range(n):
        p, q = poly[i], poly[(i + 1) % n]
        dp, dq = a * p[0] + b * p[1] - c, a * q[0] + b * q[1] - c
        if dp <= 0:
            out.append(p)
        if (dp < 0 < dq) or (dq < 0 < dp):
            t = dp / (dp - dq)
            out.append((p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t))
    return out


def stripes(g, o, U, V, lu, lv, normal, band=.22, phase=0.0, fa=None, fb=None):
    """Slanted warning stripes over the rectangle o + s U + t V (U, V unit; lu x lv metres)."""
    fa = fa if fa is not None else mat('stripe-yellow', 0xF6B821, smooth=.5, dirt=.3)
    fb = fb if fb is not None else mat('stripe-black', 0x1E1A22, smooth=.4, dirt=.3)
    k0 = int(math.floor((-lv - phase) / band)) - 1
    k1 = int(math.ceil((lu - phase) / band)) + 1
    rect = [(0, 0), (lu, 0), (lu, lv), (0, lv)]
    for k in range(k0, k1):
        lo, hi = k * band + phase, (k + 1) * band + phase
        poly = _clip(_clip(rect, 1, -1, hi), -1, 1, -lo)       # lo <= u - v <= hi
        if len(poly) >= 3:
            g.face([sk._add(sk._add(o, U, p[0]), V, p[1]) for p in poly], fa if k % 2 == 0 else fb, normal)


def rotate(q, v):
    x, y, z, w = q
    vx, vy, vz = v
    tx, ty, tz = 2 * (y * vz - z * vy), 2 * (z * vx - x * vz), 2 * (x * vy - y * vx)
    return (vx + w * tx + (y * tz - z * ty), vy + w * ty + (z * tx - x * tz), vz + w * tz + (x * ty - y * tx))


def long_axis(p):
    """A part's longest axis in the world (unit), its half length, and the other two half extents."""
    h = p['h']
    a = int(np.argmax(h))
    ax = rotate(p['q'], tuple(1.0 if i == a else 0.0 for i in range(3)))
    others = [h[i] for i in range(3) if i != a]
    return ax, h[a], others


def neon(g, p, T, f=None):
    """A kit tube as a round glass tube with pointed-round ends (overlapping strokes join into bent neon)."""
    ax, half, others = long_axis(p)
    f = pfin(p) if f is None else f
    r = max(.012, min(others)) * 1.05
    c = p['c']
    a, b = sk._add(c, ax, -half), sk._add(c, ax, half)
    sides = (6, 4, 3)[T.i]
    capsule(g, a, b, r, f, sides, caps=1 if T.i == 0 else 0)


def flat_out(p, owner):
    """A thin plate's outward normal (unit, world): away from the block it dresses, or up."""
    h = p['h']
    a = int(np.argmin(h))
    n = rotate(p['q'], tuple(1.0 if i == a else 0.0 for i in range(3)))
    if owner is not None:
        d = sk._sub(p['c'], owner['pos'])
        # the plate's centre relative to the owner's nearest face along n
        if sk._dot(d, n) < 0:
            n = (-n[0], -n[1], -n[2])
    elif n[1] < 0:
        n = (-n[0], -n[1], -n[2])
    return a, n


def kit_part(g, p, T, owner=None, f=None):
    """Any kit part as a finished piece: a tube round, a decal a single face, a hazard plate striped, a bar a rod, the rest a
    chamfered box."""
    hx, hy, hz = p['h']
    lo, hi = min(hx, hy, hz), max(hx, hy, hz)
    name = KINDS[p['k']]
    if T.i == 2 and hi < .06 and name not in GLOW:
        return
    f = pfin(p) if f is None else f
    q = p['q']
    mid = sorted((hx, hy, hz))[1]
    if name == 'tube' and hi > 2.2 * mid and mid < .09:
        neon(g, p, T, f)
        return
    if name == 'stripe' and lo <= .02:
        a, n = flat_out(p, owner)
        axes = [i for i in range(3) if i != a]
        if p['h'][axes[0]] < p['h'][axes[1]]:
            axes = axes[::-1]
        U = rotate(q, tuple(1.0 if i == axes[0] else 0.0 for i in range(3)))
        V = rotate(q, tuple(1.0 if i == axes[1] else 0.0 for i in range(3)))
        lu, lv = 2 * p['h'][axes[0]], 2 * p['h'][axes[1]]
        o = sk._add(sk._add(sk._add(p['c'], n, p['h'][a]), U, -lu / 2), V, -lv / 2)
        stripes(g, o, U, V, lu, lv, n)
        return
    if lo <= .0125:
        # a decal or a thin plate: its outward face (and, near, its edges)
        a, n = flat_out(p, owner)
        axes = [i for i in range(3) if i != a]
        U = rotate(q, tuple(1.0 if i == axes[0] else 0.0 for i in range(3)))
        V = rotate(q, tuple(1.0 if i == axes[1] else 0.0 for i in range(3)))
        c = sk._add(p['c'], n, p['h'][a])
        lu, lv = p['h'][axes[0]], p['h'][axes[1]]
        pts = [sk._add(sk._add(c, U, su * lu), V, sv * lv) for su, sv in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        if T.i == 0 and max(lu, lv) > 1.5:
            sk.grid_face(g, pts[0], sk._sub(pts[1], pts[0]), sk._sub(pts[3], pts[0]), 0, 0, f, n, 1.5)
        else:
            g.face(pts, f, n)
        return
    if abs(q[3]) < .9999 or True:
        # a bar (square section, long): a rod
        s = sorted((hx, hy, hz))
        if abs(s[0] - s[1]) < .006 and s[2] > 3 * s[0] and s[0] <= .09 and name != 'tube':
            ax, half, _ = long_axis(p)
            sk.cylinder(g, sk._add(p['c'], ax, -half), sk._add(p['c'], ax, half), s[0] * 1.08, f, T.small if s[0] < .05 else T.round)
            return
    obox(g, p['c'], (hx, hy, hz), q, f, T, bevel=min(.02, lo * .35))


def body(g, b, f, T, bevel=.03, skip=('-y',), cell=None):
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, f, T, bevel, cell=cell, skip=skip)
