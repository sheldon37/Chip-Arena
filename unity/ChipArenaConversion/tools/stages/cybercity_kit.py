"""Cyber City's art: what its scripts share (cybercity_art.py runs them): the finishes, the map's blocks as boxes with face
frames, and the modelling helpers on stagekit (walls with openings, neon tube lettering, LED strips, screens, hazard stripes,
rounded boxes, railings). Everything is in the game's space (Unity: metres, +y up; +z north, +x east; the Cut runs along x)."""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, sweep, lathe, cylinder


def grid_face(g, origin, u, v, nu, nv, f, normal, cell):
    """stagekit's grid face; a cell of 0 (or None) leaves the face whole."""
    sk.grid_face(g, origin, u, v, nu, nv, f, normal, cell if cell and cell > 0 else 1e9)

F = {}
Y = (0.0, 1.0, 0.0)
CYAN, MAGENTA, VIOLET, ICE, WARM, AMBER, RED, LIME, EXIT = 0x22E6FF, 0xFF3DCF, 0x8B5CFF, 0xC8F4FF, 0xFF8A4D, 0xFFB000, 0xFF2A3D, 0x7DFF8A, 0xF2F6FF
NEONS = (CYAN, MAGENTA, VIOLET, ICE, 0xFF5A8A, 0x5AFFD2)
PREFIX = 'cybercity-'


def finishes():
    A = sk

    def add(name, hexcol, detail=A.DETAIL_NONE, **kw):
        F[name] = A.fin(name, hexcol, detail, **kw)
    # the street
    add('asphalt', 0x24252D, A.DETAIL_PLASTER, smooth=.72, dirt=.5)
    add('puddle', 0x1C1E28, A.DETAIL_NONE, smooth=.95, dirt=.1)
    add('paver', 0x40424D, A.DETAIL_FLAG, smooth=.6, param=1.0, dirt=.6)
    add('paverdark', 0x33353F, A.DETAIL_FLAG, smooth=.62, param=1.8, dirt=.6)
    add('kerb', 0x5A5E6C, A.DETAIL_PLASTER, smooth=.4, dirt=.7, wear=.3)
    add('paint', 0xD0D5DE, A.DETAIL_NONE, smooth=.45, dirt=.5)
    add('hazardy', 0xF4B321, A.DETAIL_NONE, smooth=.4, dirt=.5)
    add('hazardk', 0x16171C, A.DETAIL_NONE, smooth=.4, dirt=.3)
    # concrete and cladding
    add('concrete', 0x4C505E, A.DETAIL_PLASTER, smooth=.3, dirt=.8, wear=.2)
    add('concretedark', 0x363843, A.DETAIL_PLASTER, smooth=.3, dirt=.8, wear=.15)
    add('concretewet', 0x2E2F38, A.DETAIL_PLASTER, smooth=.6, dirt=.6)
    add('cut', 0x3C3E4B, A.DETAIL_PLASTER, smooth=.35, dirt=.9, wear=.15)
    add('cutfloor', 0x2C2C35, A.DETAIL_PLASTER, smooth=.62, dirt=.5)
    for i, c in enumerate((0x4A4E5C, 0x3B3E4D, 0x55515F, 0x3E4759, 0x4B4456, 0x333644)):
        add('clad%d' % i, c, A.DETAIL_PLASTER, smooth=.34, dirt=.8, wear=.12)
    add('trim', 0x2A2D3A, A.DETAIL_NONE, smooth=.55, metal=.4, dirt=.4, wear=.35)
    add('trimlight', 0x8A93A8, A.DETAIL_NONE, smooth=.62, metal=.5, dirt=.3, wear=.3)
    # metals
    add('metal', 0x2E3342, A.DETAIL_NONE, smooth=.6, metal=.5, dirt=.4, wear=.4)
    add('metaldark', 0x1D2029, A.DETAIL_NONE, smooth=.55, metal=.45, dirt=.3, wear=.4)
    add('steel', 0x6B7385, A.DETAIL_NONE, smooth=.66, metal=.5, dirt=.35, wear=.3)
    add('chrome', 0xA6AEC0, A.DETAIL_NONE, smooth=.85, metal=.5, dirt=.15, wear=.1)
    add('gunmetal', 0x3A3F4E, A.DETAIL_NONE, smooth=.58, metal=.5, dirt=.4, wear=.35)
    add('grate', 0x555B6B, A.DETAIL_NONE, smooth=.55, metal=.5, dirt=.5, wear=.3)
    add('void', 0x07080B, A.DETAIL_NONE, smooth=.2, dirt=0)
    add('rubber', 0x0E0F13, A.DETAIL_NONE, smooth=.3, dirt=.2)
    add('cable', 0x12131A, A.DETAIL_NONE, smooth=.4, dirt=.2)
    add('copper', 0xB8703C, A.DETAIL_NONE, smooth=.7, metal=.5, dirt=.3)
    add('ceramic', 0xC9CED8, A.DETAIL_NONE, smooth=.8, dirt=.3)
    # glass
    add('glass', 0x121829, A.DETAIL_NONE, smooth=.93, metal=.35, dirt=.15)
    add('glassblue', 0x1D2540, A.DETAIL_NONE, smooth=.93, metal=.35, dirt=.15)
    add('glasstower', 0x18203A, A.DETAIL_NONE, smooth=.9, metal=.4, dirt=.1)
    add('screenoff', 0x06070C, A.DETAIL_NONE, smooth=.9, metal=.2, dirt=.05)
    # the markets and yards
    add('timberx', 0x8A5A3A, A.DETAIL_WOOD, smooth=.3, param=0, dirt=.8, wear=.2)
    add('timbery', 0x8A5A3A, A.DETAIL_WOOD, smooth=.3, param=1, dirt=.8, wear=.2)
    add('timberz', 0x8A5A3A, A.DETAIL_WOOD, smooth=.3, param=2, dirt=.8, wear=.2)
    add('timberdarkx', 0x4E3626, A.DETAIL_WOOD, smooth=.3, param=0, dirt=.8, wear=.2)
    add('timberdarkz', 0x4E3626, A.DETAIL_WOOD, smooth=.3, param=2, dirt=.8, wear=.2)
    add('awningred', 0x9A2C38, A.DETAIL_CLOTH, smooth=.2, dirt=.7)
    add('awningteal', 0x1F6F78, A.DETAIL_CLOTH, smooth=.2, dirt=.7)
    add('awningdark', 0x2A2440, A.DETAIL_CLOTH, smooth=.2, dirt=.7)
    add('tarp', 0x2B4A5E, A.DETAIL_CLOTH, smooth=.25, dirt=.7)
    add('leaf', 0x1F4A34, A.DETAIL_NONE, smooth=.35, dirt=.3)
    add('leaflight', 0x2F6A44, A.DETAIL_NONE, smooth=.35, dirt=.3)
    add('soil', 0x211A17, A.DETAIL_PLASTER, smooth=.15, dirt=.2)
    add('seat', 0x3A2E52, A.DETAIL_CLOTH, smooth=.3, dirt=.5)
    add('plastic', 0xC8CCD6, A.DETAIL_NONE, smooth=.6, dirt=.5)
    # vehicles and the train
    add('vanwhite', 0xB9BEC9, A.DETAIL_NONE, smooth=.78, metal=.25, dirt=.5, wear=.1)
    add('vanblue', 0x2C4A78, A.DETAIL_NONE, smooth=.8, metal=.3, dirt=.5, wear=.1)
    add('vanteal', 0x1D5A5E, A.DETAIL_NONE, smooth=.8, metal=.3, dirt=.5, wear=.1)
    add('cab', 0xE0B02A, A.DETAIL_NONE, smooth=.82, metal=.3, dirt=.4, wear=.1)
    add('cabdark', 0x1B1C22, A.DETAIL_NONE, smooth=.8, metal=.35, dirt=.3)
    add('bike', 0xB02A45, A.DETAIL_NONE, smooth=.82, metal=.35, dirt=.3)
    add('train', 0x2B303D, A.DETAIL_NONE, smooth=.82, metal=.45, dirt=.25, wear=.15)
    add('trainroof', 0x3A3F4E, A.DETAIL_NONE, smooth=.6, metal=.45, dirt=.4)
    add('trainskirt', 0x1A1D26, A.DETAIL_NONE, smooth=.6, metal=.45, dirt=.3)
    add('trainstripe', 0xC8CCD6, A.DETAIL_NONE, smooth=.8, metal=.3, dirt=.2)
    add('container0', 0x2F6E64, A.DETAIL_NONE, smooth=.5, metal=.3, dirt=.8, wear=.4)
    add('container1', 0x8A3B3B, A.DETAIL_NONE, smooth=.5, metal=.3, dirt=.8, wear=.4)
    add('container2', 0x3B4E8A, A.DETAIL_NONE, smooth=.5, metal=.3, dirt=.8, wear=.4)
    add('container3', 0x9A7A2E, A.DETAIL_NONE, smooth=.5, metal=.3, dirt=.8, wear=.4)
    add('bin', 0x2E5A4A, A.DETAIL_NONE, smooth=.5, metal=.3, dirt=.9, wear=.5)


def glow(hexcol, gain):
    """A glowing finish of this colour and HDR gain (its own sub-mesh; the bloom takes gains above 1)."""
    gain = round(max(.2, gain), 2)
    name = 'glow-%06x-%s' % (hexcol & 0xFFFFFF, gain)
    if name not in F:
        F[name] = sk.fin(name, hexcol & 0xFFFFFF, sk.DETAIL_GLOW, glow=gain)
    return F[name]


def mix(a, b, t):
    """Two sRGB hex colours mixed."""
    return (int((a >> 16 & 255) * (1 - t) + (b >> 16 & 255) * t) << 16) | (int((a >> 8 & 255) * (1 - t) + (b >> 8 & 255) * t) << 8) | int((a & 255) * (1 - t) + (b & 255) * t)


# ---------------------------------------------------------------------------------------------------------------------------
# Hashing (deterministic variety)
# ---------------------------------------------------------------------------------------------------------------------------
def _mix(x):
    x &= 0xFFFFFFFF
    x ^= x >> 16
    x = (x * 0x7feb352d) & 0xFFFFFFFF
    x ^= x >> 15
    x = (x * 0x846ca68b) & 0xFFFFFFFF
    x ^= x >> 16
    return x


def h01(*k):
    """Deterministic [0, 1) from integers and strings."""
    h = 2166136261
    for v in k:
        if isinstance(v, str):
            for ch in v:
                h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
        else:
            h = _mix(h ^ _mix(int(v) * 0x9E3779B9 + 0x85EBCA6B))
    return _mix(h) / 4294967296.0


def pick(lst, *k):
    return lst[min(len(lst) - 1, int(h01(*k) * len(lst)))]


# ---------------------------------------------------------------------------------------------------------------------------
# Blocks and face frames
# ---------------------------------------------------------------------------------------------------------------------------
class Blk:
    """A map block: its extents, centre, size and the id's stem (the map prefix removed)."""

    def __init__(self, b):
        self.b = b
        self.id = b['id']
        self.stem = self.id[len(PREFIX):] if self.id.startswith(PREFIX) else self.id
        p, s = b['pos'], b['size']
        self.c = tuple(p)
        self.s = tuple(s)
        self.x0, self.x1 = p[0] - s[0] / 2, p[0] + s[0] / 2
        self.y0, self.y1 = p[1] - s[1] / 2, p[1] + s[1] / 2
        self.z0, self.z1 = p[2] - s[2] / 2, p[2] + s[2] / 2
        self.role = b['role']

    @property
    def ext(self):
        return (self.x0, self.x1, self.y0, self.y1, self.z0, self.z1)

    def face(self, axis, sign):
        """The frame of a side face (axis 0: an x face, 2: a z face; sign: which)."""
        if axis == 0:
            return Fr((self.x1 if sign > 0 else self.x0, self.y0, self.c[2]), (sign, 0, 0), self.s[2] / 2, self.s[1])
        return Fr((self.c[0], self.y0, self.z1 if sign > 0 else self.z0), (0, 0, sign), self.s[0] / 2, self.s[1])

    def inward(self):
        """The long face that looks toward the arena's middle (the kit's rule)."""
        if self.s[0] < self.s[2]:
            return self.face(0, -1 if self.c[0] > 0 else 1)
        return self.face(2, -1 if self.c[2] > 0 else 1)


class Fr:
    """A vertical face's frame: origin at the face's bottom middle, n its outward normal, u along the face in reading order
    (left to right seen from outside), v up from the origin, d out along n. half: the face's half width; tall: its height."""

    def __init__(self, o, n, half=0.0, tall=0.0):
        self.o, self.n, self.half, self.tall = o, n, half, tall
        self.ud = (-n[2], 0.0, n[0])   # up x forward with forward = -n (Unity's left-handed frame): the reader's right

    def at(self, u, v, d=0.0):
        return (self.o[0] + self.ud[0] * u + self.n[0] * d, self.o[1] + v, self.o[2] + self.ud[2] * u + self.n[2] * d)

    def world_u(self, w):
        """The frame's u of a world offset along the face (x on a z face, z on an x face) from the origin."""
        return w * (self.ud[0] + self.ud[2])

    def box(self, g, u0, u1, v0, v1, d0, d1, f, T=None, bevel=.01, cell=0, skip=()):
        a, b = self.at(u0, v0, d0), self.at(u1, v1, d1)
        box(g, a[0], b[0], a[1], b[1], a[2], b[2], f, T, bevel, cell=cell, skip=skip)

    def rbox(self, g, u0, u1, v0, v1, d0, d1, r, f, segs=2):
        a, b = self.at(u0, v0, d0), self.at(u1, v1, d1)
        rbox(g, min(a[0], b[0]), max(a[0], b[0]), min(a[1], b[1]), max(a[1], b[1]), min(a[2], b[2]), max(a[2], b[2]), r, f, segs)

    def quad(self, g, u0, u1, v0, v1, d, f, cell=0):
        if u1 - u0 < 1e-5 or v1 - v0 < 1e-5:
            return
        o = self.at(u0, v0, d)
        U = (self.ud[0] * (u1 - u0), 0, self.ud[2] * (u1 - u0))
        if cell and cell > 0:
            grid_face(g, o, U, (0, v1 - v0, 0), 0, 0, f, self.n, cell)
        else:
            g.face((o, sk._add(o, U), sk._add(sk._add(o, U), (0, v1 - v0, 0)), sk._add(o, (0, v1 - v0, 0))), f, self.n)

    def poly(self, g, pts, d, f):
        """A convex polygon [(u, v)] on the face at depth d."""
        g.face([self.at(u, v, d) for (u, v) in pts], f, self.n)

    def shifted(self, du=0.0, dv=0.0, dd=0.0):
        return Fr(self.at(du, dv, dd), self.n, self.half, self.tall)


def holed(g, fr, u0, u1, v0, v1, holes, f, d=0.0, cell=2.0, rev=0.0, frev=None):
    """A wall face u0..u1 x v0..v1 at depth d with rectangular holes [(ua, ub, va, vb)] cut through it, each with reveals `rev`
    deep (finish frev) going in."""
    hs = [(max(u0, a), min(u1, b), max(v0, c), min(v1, e)) for (a, b, c, e) in holes]
    hs = [h for h in hs if h[1] - h[0] > 1e-4 and h[3] - h[2] > 1e-4]
    us = sorted(set([u0, u1] + [h[0] for h in hs] + [h[1] for h in hs]))
    vs = sorted(set([v0, v1] + [h[2] for h in hs] + [h[3] for h in hs]))
    for j in range(len(vs) - 1):
        vm = (vs[j] + vs[j + 1]) / 2
        run = None
        for i in range(len(us) - 1):
            um = (us[i] + us[i + 1]) / 2
            solid = not any(h[0] < um < h[1] and h[2] < vm < h[3] for h in hs)
            if solid and run is None:
                run = us[i]
            if run is not None and (not solid or i == len(us) - 2):
                end = us[i] if not solid else us[i + 1]
                fr.quad(g, run, end, vs[j], vs[j + 1], d, f, cell)
                run = None
    if rev > 0:
        fv = f if frev is None else frev
        for (a, b, c, e) in hs:
            g.face((fr.at(a, c, d), fr.at(a, e, d), fr.at(a, e, d - rev), fr.at(a, c, d - rev)), fv, fr.ud)
            g.face((fr.at(b, c, d), fr.at(b, e, d), fr.at(b, e, d - rev), fr.at(b, c, d - rev)), fv, (-fr.ud[0], 0, -fr.ud[2]))
            g.face((fr.at(a, c, d), fr.at(b, c, d), fr.at(b, c, d - rev), fr.at(a, c, d - rev)), fv, (0, 1, 0))
            g.face((fr.at(a, e, d), fr.at(b, e, d), fr.at(b, e, d - rev), fr.at(a, e, d - rev)), fv, (0, -1, 0))


# ---------------------------------------------------------------------------------------------------------------------------
# Shapes
# ---------------------------------------------------------------------------------------------------------------------------
def rod(g, a, b, r, f, sides, caps=True):
    if sk._dot(sk._sub(b, a), sk._sub(b, a)) < 1e-10:
        return
    cylinder(g, a, b, r, f, sides, caps=caps)


def rbox(g, x0, x1, y0, y1, z0, z1, r, f, segs=2):
    """A box with every edge and corner rounded to radius r: each face a grid whose rim rolls round the edge; smooth normals."""
    r = max(1e-4, min(r, (x1 - x0) * .49, (y1 - y0) * .49, (z1 - z0) * .49))
    lo, hi = (x0, y0, z0), (x1, y1, z1)
    offs = [r * (1 - math.tan(math.pi / 4 * (1 - j / segs))) for j in range(segs + 1)]

    def coords(a):
        return [lo[a] + o for o in offs] + [hi[a] - o for o in reversed(offs)]

    def proj(p):
        inner = tuple(min(max(p[a], lo[a] + r), hi[a] - r) for a in range(3))
        n = sk._norm(sk._sub(p, inner))
        return sk._add(inner, n, r), n
    for a in range(3):
        b, c = (a + 1) % 3, (a + 2) % 3
        cb, cc = coords(b), coords(c)
        for side in (0, 1):
            va = lo[a] if side == 0 else hi[a]
            for i in range(len(cb) - 1):
                for j in range(len(cc) - 1):
                    pts, nrm = [], []
                    for (bi, cj) in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)):
                        p = [0.0, 0.0, 0.0]
                        p[a], p[b], p[c] = va, cb[bi], cc[cj]
                        P, N = proj(tuple(p))
                        pts.append(P)
                        nrm.append(N)
                    g.face(pts, f, normals=nrm)


def disc(g, c, r, f, sides, normal):
    u, v, w = sk.frame((0, 0, 0), normal)
    g.face([sk._add(sk._add(c, u, r * math.cos(2 * math.pi * i / sides)), v, r * math.sin(2 * math.pi * i / sides)) for i in range(sides)], f, normal)


def ring(g, c, r0, r1, f, sides, normal):
    """A flat annulus facing `normal`."""
    u, v, w = sk.frame((0, 0, 0), normal)
    for i in range(sides):
        a0, a1 = 2 * math.pi * i / sides, 2 * math.pi * (i + 1) / sides
        g.face((sk._add(sk._add(c, u, r0 * math.cos(a0)), v, r0 * math.sin(a0)), sk._add(sk._add(c, u, r1 * math.cos(a0)), v, r1 * math.sin(a0)),
                sk._add(sk._add(c, u, r1 * math.cos(a1)), v, r1 * math.sin(a1)), sk._add(sk._add(c, u, r0 * math.cos(a1)), v, r0 * math.sin(a1))), f, normal)


def ball(g, c, r, f, sides, rings, squash=1.0):
    prof = [(max(1e-4, r * math.sin(math.pi * i / rings)), -r * squash * math.cos(math.pi * i / rings)) for i in range(rings + 1)]
    lathe(g, prof, c, f, sides)


def dome(g, c, r, f, sides, rings=3, axis=Y, squash=1.0):
    prof = [(r * math.cos(math.pi / 2 * i / rings), r * squash * math.sin(math.pi / 2 * i / rings)) for i in range(rings + 1)]
    prof[-1] = (1e-4, r * squash)
    lathe(g, prof, c, f, sides, axis=axis)


def pipe(g, pts, r, f, sides, bend=.12, per=3, caps=False):
    """A pipe through corner points with rounded bends of radius `bend`."""
    path = [pts[0]]
    for i in range(1, len(pts) - 1):
        a, b, c = pts[i - 1], pts[i], pts[i + 1]
        la, lc = math.dist(a, b), math.dist(b, c)
        k = min(bend, la * .45, lc * .45)
        p0 = sk._lerp(b, a, k / la)
        p1 = sk._lerp(b, c, k / lc)
        for j in range(per + 1):
            t = j / per
            q = sk._lerp(sk._lerp(p0, b, t), sk._lerp(b, p1, t), t)
            path.append(q)
    path.append(pts[-1])
    sweep(g, sk.circle_section(r, sides), path, f, caps=caps)


def prism(g, pts, y0, y1, f, top=True, bottom=False, ftop=None, smooth=False):
    """A vertical prism over the convex outline pts [(x, z)] from y0 to y1."""
    n = len(pts)
    a2 = sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n))
    for i in range(n):
        p, q = pts[i], pts[(i + 1) % n]
        nx, nz = (q[1] - p[1]), -(q[0] - p[0])
        if a2 < 0:
            nx, nz = -nx, -nz
        g.face(((p[0], y0, p[1]), (q[0], y0, q[1]), (q[0], y1, q[1]), (p[0], y1, p[1])), f, (nx, 0, nz))
    if top:
        g.face([(p[0], y1, p[1]) for p in pts], f if ftop is None else ftop, (0, 1, 0))
    if bottom:
        g.face([(p[0], y0, p[1]) for p in pts], f, (0, -1, 0))


def extrude(g, outline, o, U, V, W, f, caps=True, fcap=None):
    """A convex outline [(a, b)] in the plane o + a U + b V, extruded along W (U, V, W vectors; W the thickness)."""
    n = len(outline)
    P0 = [sk._add(sk._add(o, U, a), V, b) for (a, b) in outline]
    P1 = [sk._add(p, W) for p in P0]
    cen = (sum(p[0] for p in P0) / n, sum(p[1] for p in P0) / n, sum(p[2] for p in P0) / n)
    for i in range(n):
        j = (i + 1) % n
        mid = sk._lerp(P0[i], P0[j], .5)
        g.face((P0[i], P0[j], P1[j], P1[i]), f, sk._sub(mid, cen))
    if caps:
        fc = f if fcap is None else fcap
        g.face(P0, fc, (-W[0], -W[1], -W[2]))
        g.face(P1, fc, W)


def skin(g, rings, f, close0=True, close1=True, fins=None):
    """A lofted skin through rings of points (each the same count, wound the same way), with smooth normals from the grid's
    own neighbours; the ends closed by fans to the ring's centre. fins: a finish per ring span (default f)."""
    m, k = len(rings), len(rings[0])
    R = np.array(rings, dtype=np.float64)
    du = np.roll(R, -1, axis=1) - np.roll(R, 1, axis=1)
    dv = np.empty_like(R)
    dv[1:-1] = R[2:] - R[:-2]
    dv[0] = R[1] - R[0]
    dv[-1] = R[-1] - R[-2]
    N = np.cross(du, dv)
    ctr = R.mean(axis=1, keepdims=True)
    flip = np.sign(np.sum(N * (R - ctr), axis=2, keepdims=True).sum(axis=1, keepdims=True))
    flip[flip == 0] = 1
    N = N * flip
    N /= np.maximum(np.linalg.norm(N, axis=2, keepdims=True), 1e-12)
    for i in range(m - 1):
        ff = f if fins is None else fins[i]
        for j in range(k):
            j1 = (j + 1) % k
            g.face((tuple(R[i, j]), tuple(R[i, j1]), tuple(R[i + 1, j1]), tuple(R[i + 1, j])), ff,
                   normals=(tuple(N[i, j]), tuple(N[i, j1]), tuple(N[i + 1, j1]), tuple(N[i + 1, j])))
    for (i, on, s) in ((0, close0, -1.0), (m - 1, close1, 1.0)):
        if not on:
            continue
        c = tuple(ctr[i, 0])
        axis = sk._norm(tuple((ctr[i, 0] - ctr[i + (1 if i == 0 else -1), 0])))
        for j in range(k):
            j1 = (j + 1) % k
            g.face((tuple(R[i, j]), tuple(R[i, j1]), c), f, normals=(tuple(N[i, j]), tuple(N[i, j1]), axis))


def section_yz(x, y0, y1, hw, n, power=4.0, cy=None):
    """A ring of n points in the plane x: a superellipse between y0 and y1, half width hw (z), from +z counter-clockwise."""
    cy = (y0 + y1) / 2 if cy is None else cy
    hy = (y1 - y0) / 2
    out = []
    e = 2.0 / power
    for i in range(n):
        a = 2 * math.pi * i / n
        ca, sa = math.cos(a), math.sin(a)
        out.append((x, cy + hy * (abs(sa) ** e) * (1 if sa >= 0 else -1), hw * (abs(ca) ** e) * (1 if ca >= 0 else -1)))
    return out


def stripes(g, o, U, V, n, w, fa, fb, lean=1.0):
    """Hazard stripes on the rectangle o + s U + t V (s, t in 0..1), facing n: bands `w` wide leaning along U by the
    rectangle's height times `lean`, alternating finishes fa and fb."""
    L, H = math.sqrt(sk._dot(U, U)), math.sqrt(sk._dot(V, V))
    if L < 1e-5 or H < 1e-5:
        return
    rect = [(0, 0), (L, 0), (L, H), (0, H)]
    k0 = int(math.floor(-H * abs(lean) / w)) - 1
    k1 = int(math.ceil(L / w)) + 1
    for k in range(k0, k1 + 1):
        s = k * w
        para = [(s, 0), (s + w, 0), (s + w + H * lean, H), (s + H * lean, H)]
        ov = sk._clip(para, rect)
        if len(ov) < 3 or abs(sk._area2(ov)) < 1e-6:
            continue
        g.face([sk._add(sk._add(o, U, a / L), V, b / H) for (a, b) in ov], fa if k % 2 == 0 else fb, n)


# ---------------------------------------------------------------------------------------------------------------------------
# Neon tube lettering (a stroke font on a 4 x 6 grid) and signs
# ---------------------------------------------------------------------------------------------------------------------------
_P = [(0, 0), (0, 6), (3, 6), (4, 5), (4, 4), (3, 3), (0, 3)]
STROKES = {
    'A': [[(0, 0), (0, 4), (2, 6), (4, 4), (4, 0)], [(0, 2.4), (4, 2.4)]],
    'B': [_P, [(3, 3), (4, 2), (4, 1), (3, 0), (0, 0)]],
    'C': [[(4, 5), (3, 6), (1, 6), (0, 5), (0, 1), (1, 0), (3, 0), (4, 1)]],
    'D': [[(0, 0), (0, 6), (2.6, 6), (4, 4.6), (4, 1.4), (2.6, 0), (0, 0)]],
    'E': [[(4, 6), (0, 6), (0, 0), (4, 0)], [(0, 3), (3, 3)]],
    'F': [[(4, 6), (0, 6), (0, 0)], [(0, 3), (3, 3)]],
    'G': [[(4, 5), (3, 6), (1, 6), (0, 5), (0, 1), (1, 0), (3, 0), (4, 1), (4, 3), (2.2, 3)]],
    'H': [[(0, 0), (0, 6)], [(4, 0), (4, 6)], [(0, 3), (4, 3)]],
    'I': [[(2, 0), (2, 6)], [(.8, 0), (3.2, 0)], [(.8, 6), (3.2, 6)]],
    'K': [[(0, 0), (0, 6)], [(4, 6), (0, 2.6)], [(1.5, 3.7), (4, 0)]],
    'L': [[(0, 6), (0, 0), (4, 0)]],
    'M': [[(0, 0), (0, 6), (2, 3), (4, 6), (4, 0)]],
    'N': [[(0, 0), (0, 6), (4, 0), (4, 6)]],
    'O': [[(1, 0), (0, 1), (0, 5), (1, 6), (3, 6), (4, 5), (4, 1), (3, 0), (1, 0)]],
    'P': [_P],
    'R': [_P, [(2, 3), (4, 0)]],
    'S': [[(4, 5), (3, 6), (1, 6), (0, 5), (0, 4), (1, 3), (3, 3), (4, 2), (4, 1), (3, 0), (1, 0), (0, 1)]],
    'T': [[(0, 6), (4, 6)], [(2, 6), (2, 0)]],
    'U': [[(0, 6), (0, 1), (1, 0), (3, 0), (4, 1), (4, 6)]],
    'V': [[(0, 6), (2, 0), (4, 6)]],
    'W': [[(0, 6), (1, 0), (2, 4), (3, 0), (4, 6)]],
    'X': [[(0, 0), (4, 6)], [(0, 6), (4, 0)]],
    'Y': [[(0, 6), (2, 3), (4, 6)], [(2, 3), (2, 0)]],
    'Z': [[(0, 6), (4, 6), (0, 0), (4, 0)]],
    '2': [[(0, 5), (1, 6), (3, 6), (4, 5), (4, 4), (0, 0), (4, 0)]],
    '4': [[(3, 0), (3, 6), (0, 2), (4, 2)]],
    '-': [[(.6, 3), (3.4, 3)]],
    ' ': [],
}


def neon_text(g, fr, text, u, v, cell, col, gain, T, d=.05, weight=.4):
    """A word in neon tube on a face: centred at u, its baseline v up, the kit's letter cell `cell` (a letter 5 x 7 cells, one
    cell apart), the tubes' axis d out from the face, with stand-offs back to it. Returns the word's (u0, u1, v0, v1)."""
    f = glow(col, gain)
    width = (len(text) * 6 - 1) * cell
    start = u - width / 2
    r = cell * weight
    sides = (7, 5, 4)[T.i]
    for i, ch in enumerate(text):
        x0 = start + i * 6 * cell + cell * .35
        sx, sy = (5 * cell - cell * .7) / 4.0, (7 * cell - cell * .6) / 6.0
        y0 = v + cell * .3
        for st in STROKES.get(ch.upper(), []):
            pts = [fr.at(x0 + p[0] * sx, y0 + p[1] * sy, d) for p in st]
            for a, b in zip(pts[:-1], pts[1:]):
                w = sk._norm(sk._sub(b, a))
                rod(g, sk._add(a, w, -r * .55), sk._add(b, w, r * .55), r, f, sides, caps=T.i == 0)
            if T.tiny:
                # stand-offs: a short dark post behind the first and last point of each stroke
                for p in (st[0], st[-1]):
                    q = fr.at(x0 + p[0] * sx, y0 + p[1] * sy, 0)
                    rod(g, q, sk._add(q, fr.n, d - r * .6), r * .35, F['metaldark'], 4, caps=False)
    return (start, start + width, v, v + 7 * cell)


def sign_board(g, fr, u0, u1, v0, v1, T, d=.025, f=None, edge=None):
    """A sign's backing board with a raised rim, d proud of the face."""
    f = F['metaldark'] if f is None else f
    fr.box(g, u0, u1, v0, v1, 0, d, f, T, bevel=.008)
    if T.detail:
        e = F['trim'] if edge is None else edge
        t = .035
        for (a, b, c, dd) in ((u0, u1, v0, v0 + t), (u0, u1, v1 - t, v1), (u0, u0 + t, v0 + t, v1 - t), (u1 - t, u1, v0 + t, v1 - t)):
            fr.box(g, a, b, c, dd, d, d + .012, e, None, bevel=0)


def led(g, fr, u0, u1, v0, v1, col, gain, T, d=.05, f=None, housing=True):
    """An LED strip on a face: a slim dark channel with the glowing diffuser standing proud of it."""
    w, h = u1 - u0, v1 - v0
    if housing and T.detail and min(w, h) > .03:
        m = min(.012, min(w, h) * .2)
        fr.box(g, u0 - m, u1 + m, v0 - m, v1 + m, 0, d * .55, F['metaldark'] if f is None else f, None, bevel=0)
    fr.box(g, u0, u1, v0, v1, 0, d, glow(col, gain), None, bevel=0, skip=())


def screen(g, fr, u0, u1, v0, v1, col, gain, T, seed, d=.08, bezel=.06, marks=True):
    """An LED / holo screen on a face: a dark bezel d proud, its picture a field of the screen's colour with an abstract
    poster laid out on it (bands, a disc, bars of glyph marks) in neighbouring hues."""
    fr.box(g, u0, u1, v0, v1, 0, d, F['metaldark'], T, bevel=.012)
    a, b, c, e = u0 + bezel, u1 - bezel, v0 + bezel, v1 - bezel
    if b - a < .05 or e - c < .05:
        return
    w, h = b - a, e - c
    base = glow(mix(col, 0x101020, .45), gain * .75)
    fr.quad(g, a, b, c, e, d + .004, base)
    if not marks or T.i == 2:
        return
    z = d + .012
    hot = glow(col, gain * 1.25)
    alt = glow(pick(NEONS, seed, 1), gain * 1.1)
    white = glow(mix(col, 0xFFFFFF, .6), gain * 1.35)
    style = int(h01(seed, 2) * 4)
    wide = w > h * 1.3
    if style == 0:
        # a band across and a row of glyph marks under it
        fr.quad(g, a + w * .06, b - w * .06, c + h * .56, c + h * .86, z, hot)
        glyph_row(g, fr, a + w * .08, b - w * .08, c + h * .16, c + h * .42, z, white, seed)
    elif style == 1:
        # a disc and bars
        r = min(w, h) * .3
        cu, cv = (a + r * 1.25) if wide else (a + b) / 2, (c + e) / 2 if wide else e - r * 1.25
        fr.poly(g, [(cu + r * math.cos(2 * math.pi * i / 12), cv + r * math.sin(2 * math.pi * i / 12)) for i in range(12)], z, alt)
        if wide:
            for k in range(3):
                fr.quad(g, cu + r * 1.4, b - w * .06 - k * w * .08, c + h * (.24 + k * .2), c + h * (.34 + k * .2), z, white if k == 1 else hot)
        else:
            glyph_row(g, fr, a + w * .1, b - w * .1, c + h * .1, c + h * .3, z, white, seed)
    elif style == 2:
        # a tall block and two columns of marks
        fr.quad(g, a + w * .07, a + w * .4, c + h * .12, c + h * .88, z, alt)
        glyph_row(g, fr, a + w * .48, b - w * .07, c + h * .56, c + h * .84, z, white, seed)
        fr.quad(g, a + w * .48, b - w * .07, c + h * .2, c + h * .3, z, hot)
    else:
        # chevrons
        n = 3
        for k in range(n):
            x = a + w * (.14 + k * .26)
            s = min(w * .1, h * .3)
            fr.poly(g, [(x, c + h * .5 - s), (x + s * .5, c + h * .5 - s), (x + s * 1.3, c + h * .5), (x + s * .8, c + h * .5)], z, hot if k % 2 == 0 else alt)
            fr.poly(g, [(x + s * .8, c + h * .5), (x + s * 1.3, c + h * .5), (x + s * .5, c + h * .5 + s), (x, c + h * .5 + s)], z, hot if k % 2 == 0 else alt)


def glyph_row(g, fr, u0, u1, v0, v1, d, f, seed):
    """A row of abstract glyph marks (blocks and bars, no real script) filling u0..u1 x v0..v1."""
    h = v1 - v0
    n = max(1, int((u1 - u0) / (h * .95)))
    step = (u1 - u0) / n
    for i in range(n):
        a = u0 + i * step
        s = step * .78
        k = int(h01(seed, i, 7) * 5)
        if k == 0:
            fr.quad(g, a, a + s, v0, v0 + h * .28, d, f)
            fr.quad(g, a + s * .36, a + s * .64, v0 + h * .28, v1, d, f)
        elif k == 1:
            fr.quad(g, a, a + s * .28, v0, v1, d, f)
            fr.quad(g, a + s * .28, a + s, v0 + h * .36, v0 + h * .64, d, f)
            fr.quad(g, a + s * .72, a + s, v0, v0 + h * .36, d, f)
        elif k == 2:
            fr.quad(g, a, a + s, v1 - h * .28, v1, d, f)
            fr.quad(g, a, a + s, v0, v0 + h * .28, d, f)
            fr.quad(g, a + s * .36, a + s * .64, v0 + h * .28, v1 - h * .28, d, f)
        elif k == 3:
            fr.quad(g, a, a + s * .3, v0, v1, d, f)
            fr.quad(g, a + s * .7, a + s, v0, v1, d, f)
            fr.quad(g, a + s * .3, a + s * .7, v1 - h * .28, v1, d, f)
        else:
            fr.quad(g, a, a + s, v0 + h * .36, v0 + h * .64, d, f)
            fr.quad(g, a + s * .36, a + s * .64, v0, v1, d, f)


# ---------------------------------------------------------------------------------------------------------------------------
# Rails, slats, grilles
# ---------------------------------------------------------------------------------------------------------------------------
def glass_rail(g, x0, x1, y0, y1, z0, z1, T, accent=None, gain=1.5, post_every=1.6, cap=None):
    """A parapet of the block's size as a balustrade: a kerb, steel posts, smoked glass between them and a handrail (the lit
    cap strip is the kit's: the caller adds it). Runs along the block's longer plan axis."""
    alongx = (x1 - x0) >= (z1 - z0)
    L = (x1 - x0) if alongx else (z1 - z0)
    w = (z1 - z0) if alongx else (x1 - x0)
    cm = (z0 + z1) / 2 if alongx else (x0 + x1) / 2

    def bx(a, b, ya, yb, half, f, bevel=.008):
        if alongx:
            box(g, x0 + a, x0 + b, ya, yb, cm - half, cm + half, f, T, bevel, cell=0)
        else:
            box(g, cm - half, cm + half, ya, yb, z0 + a, z0 + b, f, T, bevel, cell=0)
    kerb = min(.12, (y1 - y0) * .2)
    bx(0, L, y0, y0 + kerb, w / 2, F['gunmetal'], .012)
    rail = .06
    bx(0, L, y1 - rail, y1, w / 2, F['steel'] if cap is None else cap, .012)
    n = max(1, int(round(L / post_every)))
    pw = .05
    for i in range(n + 1):
        a = min(max(i * L / n - pw / 2, 0), L - pw)
        bx(a, a + pw, y0 + kerb, y1 - rail, w / 2 * .8, F['steel'], .006)
    bx(pw * .5, L - pw * .5, y0 + kerb, y1 - rail, min(.018, w * .2), F['glassblue'], 0)


def slats(g, fr, u0, u1, v0, v1, d0, d1, pitch, f, T, lean=.6):
    """Horizontal louvres between d0 and d1 on a face (a shutter, a vent, a blind)."""
    n = max(1, int((v1 - v0) / pitch))
    if T.i == 2:
        fr.box(g, u0, u1, v0, v1, d0, (d0 + d1) / 2, f, None, bevel=0)
        return
    if T.i == 1:
        n = max(1, n // 2)
    p = (v1 - v0) / n
    for i in range(n):
        a = v0 + i * p
        g.face((fr.at(u0, a, d1), fr.at(u1, a, d1), fr.at(u1, a + p * lean, d0), fr.at(u0, a + p * lean, d0)), f, sk._norm(sk._add(fr.n, (0, 1, 0), .9)))
        g.face((fr.at(u0, a + p * lean, d0), fr.at(u1, a + p * lean, d0), fr.at(u1, a + p, d1), fr.at(u0, a + p, d1)), f, sk._norm(sk._add(fr.n, (0, -1, 0), .5)))


def fan_grille(g, fr, u, v, r, d, T):
    """A condenser's round fan grille on a face."""
    c = fr.at(u, v, d)
    sides = (14, 8, 6)[T.i]
    disc(g, sk._add(c, fr.n, -.03), r * .96, F['void'], sides, fr.n)
    ring(g, c, r * .92, r * 1.05, F['steel'], sides, fr.n)
    if T.detail:
        disc(g, c, r * .22, F['steel'], 6, fr.n)
        for k in range(4 if T.i == 0 else 2):
            a = math.pi * k / (4 if T.i == 0 else 2)
            du, dv = math.cos(a) * r, math.sin(a) * r
            p0, p1 = fr.at(u - du, v - dv, d), fr.at(u + du, v + dv, d)
            rod(g, p0, p1, .012, F['steel'], 3, caps=False)


def cables(g, a, b, sag, r, f, T, n=6):
    """A hanging cable from a to b sagging by `sag` at its middle."""
    n = (n, max(3, n // 2), 2)[T.i]
    pts = []
    for i in range(n + 1):
        t = i / n
        p = sk._lerp(a, b, t)
        pts.append((p[0], p[1] - sag * 4 * t * (1 - t), p[2]))
    sweep(g, sk.circle_section(r, (4, 3, 3)[T.i]), pts, f, caps=False)
