"""The stage kit: modelling, finishing and banking a stage's art in Blender (docs/arenas/STAGE-ART.md). Run inside Blender 5.x
(bpy, numpy); the bank is tools/pack_bank/bank.py's format (Core/NativePackBank.cs), read by Runtime/NativeStageArt.cs and drawn
with Resources/NativeStageLit.shader.

Everything is authored in the GAME's space (Unity: metres, +y up), straight from the stage layout the CoreTests flag
`--stage-layout <map> <out.json>` writes (the map's blocks and its Core kit's parts), so the models land exactly on today's pieces
and colliders. Geometry is corner based: every face (3 or 4 corners, or a convex polygon) has its own corners, each with a
position and a normal, and one finish. Winding: the numeric cross product (p1 - p0) x (p2 - p0) points out of the face (Unity's
front faces, as the bank stores them); `Geo.face` turns a face round when its corners disagree with the normal it is given.

A finish (`fin`) is a material in the stage shader's terms, carried per vertex:
- colour.rgb  the albedo (sRGB), with the dirt, grime, moss, soot and wear baked in (`bake_colours`);
- colour.a    the ambient occlusion, baked by Cycles over the whole stage (`bake_ao`);
- uv0         (smoothness, metallic);
- uv1         (detail kind, parameter): the shader's fine procedural surface (DETAIL_*: ashlar courses and mortar, flagstones,
              carved stone grain, marble veins, wood grain along an axis (parameter 0 x, 1 y, 2 z), hammered iron, polished brass,
              cloth weave, wax, plaster); on the glow sub-mesh the parameter is the emission gain (HDR, above 1 for the bloom).
Glowing finishes go on their own sub-mesh (the bank's second material).

Levels of detail: every stage is built three times, `Tier(0)` Fine, `Tier(1)` Mid, `Tier(2)` Low: arcs, round sections, bevels and
the smallest details scale with the tier. Static scenery is cut into world-space chunks (`Chunks`) for culling and batching;
moving parts (rigs) and breakable blocks are props in their own frames.

Bank naming (Core NativeStageArt reads it):
  static/<chunk>/<tier>   a chunk of static scenery (world space)               group "static"
  block/<block id>/<tier> the model drawn in a map block's place (block space)   group "block"
  rig/<name>/<tier>       a moving part in its own frame (the stage's Core art pose places it)   group "rig"
  cover/<block id>        no parts: the map block whose box the art replaces    group "cover"
"""
import array
import json
import math
import os
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'pack_bank'))
import bank as bankfmt  # noqa: E402

RESOURCES = 'ChipArenaLocal/Resources'
TIER_NAMES = ('fine', 'mid', 'low')

# The shader's fine surfaces (Resources/NativeStageLit.shader DETAIL_*): uv1.x.
DETAIL_NONE, DETAIL_ASHLAR, DETAIL_FLAG, DETAIL_CARVED, DETAIL_MARBLE, DETAIL_WOOD, DETAIL_IRON, DETAIL_BRASS, DETAIL_CLOTH, \
    DETAIL_WAX, DETAIL_PLASTER, DETAIL_GLOW = range(12)
DETAIL_TILE = 16  # glazed tiles on the world's grid; the parameter is the tile's size (m)


# ---------------------------------------------------------------------------------------------------------------------------
# Finishes
# ---------------------------------------------------------------------------------------------------------------------------
class Fin:
    def __init__(self, name, hexcol, detail, smooth, metal, dirt, moss, glow, param, wear):
        self.name, self.hex, self.detail, self.smooth, self.metal = name, hexcol, detail, smooth, metal
        self.dirt, self.moss, self.glow, self.param, self.wear = dirt, moss, glow, param, wear
        self.rgb = srgb(hexcol)


FINS = []
_FIN = {}


def srgb(h):
    return ((h >> 16 & 255) / 255.0, (h >> 8 & 255) / 255.0, (h & 255) / 255.0)


def fin(name, hexcol, detail=DETAIL_NONE, smooth=.2, metal=0.0, dirt=1.0, moss=0.0, glow=0.0, param=0.0, wear=0.0):
    """A finish by name (made once): its sRGB colour, the shader's detail kind and parameter, smoothness, metallic, how much
    dirt, grime and moss it takes, its glow gain (>0: the glow sub-mesh) and its edge wear (brighter worn edges)."""
    if name in _FIN:
        return _FIN[name]
    _FIN[name] = len(FINS)
    FINS.append(Fin(name, hexcol, detail, smooth, metal, dirt, moss, glow, param, wear))
    return _FIN[name]


# ---------------------------------------------------------------------------------------------------------------------------
# Tiers
# ---------------------------------------------------------------------------------------------------------------------------
class Tier:
    """A level of detail: 0 Fine (near), 1 Mid, 2 Low (far, and the Low graphics tier)."""

    def __init__(self, i):
        self.i = i
        self.name = TIER_NAMES[i]
        self.arc = (24, 12, 6)[i]          # segments of a half circle or a pointed arch
        self.round = (14, 8, 6)[i]         # sides of a round section (columns, candles' bodies use their own)
        self.small = (8, 5, 4)[i]          # sides of a small round (bars, pipes, wicks)
        self.cell = (.75, 3.0, 5.0)[i]     # the subdivision of a flat face (the vertex colours' and AO's resolution)
        self.bevel = (1, 1, 0)[i]          # chamfers on boxes (0: square)
        self.fine = i == 0
        self.detail = i < 2                # small carved details
        self.tiny = i == 0                 # the tiniest (drips, rivets, leaves' veins)


# ---------------------------------------------------------------------------------------------------------------------------
# Corner geometry
# ---------------------------------------------------------------------------------------------------------------------------
def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _norm(a):
    l = math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2])
    return (a[0] / l, a[1] / l, a[2] / l) if l > 1e-12 else (0.0, 1.0, 0.0)


def _add(a, b, k=1.0):
    return (a[0] + b[0] * k, a[1] + b[1] * k, a[2] + b[2] * k)


def _lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


class Geo:
    """Faces with their own corners (positions and normals, Unity space) and a finish each."""

    def __init__(self):
        self.p = array.array('f')
        self.n = array.array('f')
        self.size = array.array('B')
        self.fin = array.array('H')

    @property
    def faces(self):
        return len(self.size)

    def face(self, pts, f, normal=None, normals=None):
        """A convex face. normal: the outward normal (flat shading; None: from the corners); normals: per corner (smooth)."""
        k = len(pts)
        if k < 3:
            return
        g = (0.0, 0.0, 0.0)
        for i in range(1, k - 1):
            g = _add(g, _cross(_sub(pts[i], pts[0]), _sub(pts[i + 1], pts[0])))
        if g[0] * g[0] + g[1] * g[1] + g[2] * g[2] < 1e-16:
            return
        want = normal
        if want is None and normals is not None:
            want = (0.0, 0.0, 0.0)
            for nn in normals:
                want = _add(want, nn)
        if want is not None and _dot(g, want) < 0:
            pts = pts[::-1]
            if normals is not None:
                normals = normals[::-1]
            g = (-g[0], -g[1], -g[2])
        if k > 255:
            raise ValueError('face too large')
        fn = _norm(g) if normal is None else _norm(normal)
        for i in range(k):
            self.p.extend(pts[i])
            self.n.extend(normals[i] if normals is not None else fn)
        self.size.append(k)
        self.fin.append(f)

    def quad(self, a, b, c, d, f, normal=None):
        self.face((a, b, c, d), f, normal)

    def extend(self, other):
        self.p.extend(other.p)
        self.n.extend(other.n)
        self.size.extend(other.size)
        self.fin.extend(other.fin)

    # -- transforms ----------------------------------------------------------------------------------------------------
    def transformed(self, m):
        """A copy through a 3x4 affine (rows; Unity space): normals through its inverse transpose."""
        g = Geo()
        P = np.frombuffer(self.p, dtype=np.float32).reshape(-1, 3).astype(np.float64)
        N = np.frombuffer(self.n, dtype=np.float32).reshape(-1, 3).astype(np.float64)
        A = np.array(m, dtype=np.float64).reshape(3, 4)
        R, t = A[:, :3], A[:, 3]
        P2 = P @ R.T + t
        Ni = np.linalg.inv(R).T
        N2 = N @ Ni.T
        N2 /= np.maximum(np.linalg.norm(N2, axis=1, keepdims=True), 1e-12)
        if np.linalg.det(R) < 0:
            raise ValueError('mirroring transforms would turn the faces over')
        g.p.frombytes(P2.astype(np.float32).tobytes())
        g.n.frombytes(N2.astype(np.float32).tobytes())
        g.size.extend(self.size)
        g.fin.extend(self.fin)
        return g

    def arrays(self):
        P = np.frombuffer(self.p, dtype=np.float32).reshape(-1, 3)
        N = np.frombuffer(self.n, dtype=np.float32).reshape(-1, 3)
        S = np.frombuffer(self.size, dtype=np.uint8).astype(np.int64)
        F = np.frombuffer(self.fin, dtype=np.uint16).astype(np.int64)
        return P, N, S, F


# ---------------------------------------------------------------------------------------------------------------------------
# Primitives (all in Unity space)
# ---------------------------------------------------------------------------------------------------------------------------
def grid_face(g, origin, u, v, nu, nv, f, normal, cell):
    """A flat rectangle origin + s u + t v (s, t in 0..1) cut into cells of about `cell` metres."""
    lu = math.sqrt(_dot(u, u))
    lv = math.sqrt(_dot(v, v))
    cu = max(1, int(math.ceil(lu / cell)))
    cv = max(1, int(math.ceil(lv / cell)))
    for i in range(cu):
        for j in range(cv):
            a = _add(_add(origin, u, i / cu), v, j / cv)
            b = _add(_add(origin, u, (i + 1) / cu), v, j / cv)
            c = _add(_add(origin, u, (i + 1) / cu), v, (j + 1) / cv)
            d = _add(_add(origin, u, i / cu), v, (j + 1) / cv)
            g.face((a, b, c, d), f, normal)


def box(g, x0, x1, y0, y1, z0, z1, f, tier=None, bevel=.02, cell=None, skip=()):
    """An axis-aligned box, its edges chamfered by `bevel` (none on the Low tier), its faces subdivided to `cell` (None: the
    tier's; 0: none). skip: faces left out ('-x', '+x', '-y', '+y', '-z', '+z')."""
    if x1 < x0:
        x0, x1 = x1, x0
    if y1 < y0:
        y0, y1 = y1, y0
    if z1 < z0:
        z0, z1 = z1, z0
    c = (tier.cell if tier is not None else 0) if cell is None else cell
    b = bevel if (tier is None or tier.bevel) else 0.0
    b = min(b, (x1 - x0) * .3, (y1 - y0) * .3, (z1 - z0) * .3)
    ix0, ix1, iy0, iy1, iz0, iz1 = x0 + b, x1 - b, y0 + b, y1 - b, z0 + b, z1 - b

    def F(o, u, v, n, key):
        if key in skip:
            return
        if c and c > 0:
            grid_face(g, o, u, v, 0, 0, f, n, c)
        else:
            g.face((o, _add(o, u), _add(_add(o, u), v), _add(o, v)), f, n)
    F((ix0, iy0, z0), (ix1 - ix0, 0, 0), (0, iy1 - iy0, 0), (0, 0, -1), '-z')
    F((ix0, iy0, z1), (ix1 - ix0, 0, 0), (0, iy1 - iy0, 0), (0, 0, 1), '+z')
    F((x0, iy0, iz0), (0, 0, iz1 - iz0), (0, iy1 - iy0, 0), (-1, 0, 0), '-x')
    F((x1, iy0, iz0), (0, 0, iz1 - iz0), (0, iy1 - iy0, 0), (1, 0, 0), '+x')
    F((ix0, y0, iz0), (ix1 - ix0, 0, 0), (0, 0, iz1 - iz0), (0, -1, 0), '-y')
    F((ix0, y1, iz0), (ix1 - ix0, 0, 0), (0, 0, iz1 - iz0), (0, 1, 0), '+y')
    if b <= 0:
        return
    # the twelve chamfers and eight corners: each corner of a chamfer takes the normal of the face it lies on, so the faces stay
    # flat, the edges shade round between them, and the box needs only its 24 corners (they merge on export)
    for (yy, ny) in ((y0, -1), (y1, 1)):
        iy = iy0 if ny < 0 else iy1
        if ('-y' if ny < 0 else '+y') in skip:
            continue
        Ny = (0, ny, 0)
        g.face(((ix0, yy, iz0), (ix1, yy, iz0), (ix1, iy, z0), (ix0, iy, z0)), f, normals=(Ny, Ny, (0, 0, -1), (0, 0, -1)))
        g.face(((ix0, yy, iz1), (ix1, yy, iz1), (ix1, iy, z1), (ix0, iy, z1)), f, normals=(Ny, Ny, (0, 0, 1), (0, 0, 1)))
        g.face(((ix0, yy, iz0), (ix0, yy, iz1), (x0, iy, iz1), (x0, iy, iz0)), f, normals=(Ny, Ny, (-1, 0, 0), (-1, 0, 0)))
        g.face(((ix1, yy, iz0), (ix1, yy, iz1), (x1, iy, iz1), (x1, iy, iz0)), f, normals=(Ny, Ny, (1, 0, 0), (1, 0, 0)))
    for (xx, nx) in ((x0, -1), (x1, 1)):
        ix = ix0 if nx < 0 else ix1
        for (zz, nz) in ((z0, -1), (z1, 1)):
            iz = iz0 if nz < 0 else iz1
            g.face(((xx, iy0, iz), (xx, iy1, iz), (ix, iy1, zz), (ix, iy0, zz)), f, normals=((nx, 0, 0), (nx, 0, 0), (0, 0, nz), (0, 0, nz)))
    for (xx, nx, ix) in ((x0, -1, ix0), (x1, 1, ix1)):
        for (yy, ny, iy) in ((y0, -1, iy0), (y1, 1, iy1)):
            for (zz, nz, iz) in ((z0, -1, iz0), (z1, 1, iz1)):
                if ('-y' if ny < 0 else '+y') in skip:
                    continue
                g.face(((xx, iy, iz), (ix, yy, iz), (ix, iy, zz)), f, normals=((nx, 0, 0), (0, ny, 0), (0, 0, nz)))


def obox(g, centre, half, quat, f, tier=None, bevel=.015):
    """A box turned by a Unity quaternion (x, y, z, w) about its centre (the kit's parts)."""
    t = Geo()
    box(t, -half[0], half[0], -half[1], half[1], -half[2], half[2], f, tier, bevel, cell=0 if (tier is None or tier.i > 0) else max(tier.cell, .6))
    g.extend(t.transformed(affine(quat, centre)))


def affine(q, t=(0, 0, 0), s=(1, 1, 1)):
    """The 3x4 rows of translate(t) * rotate(q) * scale(s) (Unity quaternion x, y, z, w)."""
    x, y, z, w = q
    r = ((1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)),
         (2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)),
         (2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)))
    return [r[0][0] * s[0], r[0][1] * s[1], r[0][2] * s[2], t[0],
            r[1][0] * s[0], r[1][1] * s[1], r[1][2] * s[2], t[1],
            r[2][0] * s[0], r[2][1] * s[1], r[2][2] * s[2], t[2]]


def quat_axis(ax, ay, az, angle):
    l = math.sqrt(ax * ax + ay * ay + az * az)
    s = math.sin(angle / 2) / l
    return (ax * s, ay * s, az * s, math.cos(angle / 2))


def frame(p0, p1, up=None):
    """An orthonormal frame (u, v, w) with w along p0 -> p1."""
    w = _norm(_sub(p1, p0))
    ref = up if up is not None else ((0, 1, 0) if abs(w[1]) < .95 else (1, 0, 0))
    u = _norm(_cross(ref, w))
    v = _cross(w, u)
    return u, v, w


def lathe(g, profile, centre, f, sides, axis=(0, 1, 0), smooth=True, cap0=False, cap1=False, phase=0.0, scale=(1, 1)):
    """A surface of revolution about `axis` through `centre`: profile [(radius, height)], bottom to top; normals smooth round
    and from the profile's slope. scale stretches the section (x, z) into an ellipse."""
    u, v, w = frame((0, 0, 0), axis)
    rings = []
    norms = []
    n = len(profile)
    for i, (r, h) in enumerate(profile):
        a0 = profile[max(0, i - 1)]
        a1 = profile[min(n - 1, i + 1)]
        dr, dh = a1[0] - a0[0], a1[1] - a0[1]
        ring, nr = [], []
        for k in range(sides):
            ang = 2 * math.pi * k / sides + phase
            cx, cz = math.cos(ang) * scale[0], math.sin(ang) * scale[1]
            ring.append(_add(_add(_add(centre, w, h), u, r * cx), v, r * cz))
            # outward normal: perpendicular to the profile's tangent (dr, dh) in the (radial, axial) plane
            radial = _norm(_add(_lerp((0, 0, 0), u, cx), v, cz))
            nn = _norm(_add(_lerp((0, 0, 0), radial, dh), w, -dr))
            nr.append(nn)
        rings.append(ring)
        norms.append(nr)
    for i in range(n - 1):
        for k in range(sides):
            k1 = (k + 1) % sides
            pts = (rings[i][k], rings[i][k1], rings[i + 1][k1], rings[i + 1][k])
            if smooth:
                g.face(pts, f, normals=(norms[i][k], norms[i][k1], norms[i + 1][k1], norms[i + 1][k]))
            else:
                g.face(pts, f, normal=_norm(_add(_add(norms[i][k], norms[i][k1]), _add(norms[i + 1][k1], norms[i + 1][k]))))
    if cap0 and profile[0][0] > 1e-4:
        g.face(rings[0][::-1], f, normal=(-w[0], -w[1], -w[2]))
    if cap1 and profile[-1][0] > 1e-4:
        g.face(rings[-1], f, normal=w)


def cylinder(g, p0, p1, r, f, sides, caps=True, r1=None):
    w = _norm(_sub(p1, p0))
    L = math.sqrt(_dot(_sub(p1, p0), _sub(p1, p0)))
    lathe(g, [(r, 0.0), (r if r1 is None else r1, L)], p0, f, sides, axis=w, cap0=caps, cap1=caps)


def sweep(g, section, path, f, closed=False, up=None, caps=True, smooth_u=True, scale=None, path_normals=None):
    """A section [(x, y)] swept along a path of points (Unity space): x across (the frame's u), y out (its v). The frames are
    rotation minimising from `up` (or the path's first normal); `scale` per point scales the section; `path_normals` per point
    sets v directly. smooth_u: the section's corners shaded round (False: each side flat)."""
    m = len(path)
    if m < 2:
        return
    T = []
    for i in range(m):
        a = path[(i - 1) % m] if (closed or i > 0) else path[i]
        b = path[(i + 1) % m] if (closed or i < m - 1) else path[i]
        T.append(_norm(_sub(b, a)))
    frames = []
    if path_normals is not None:
        for i in range(m):
            v = _norm(path_normals[i])
            u = _norm(_cross(v, T[i]))
            v = _cross(T[i], u)
            frames.append((u, v))
    else:
        ref = up if up is not None else ((0, 1, 0) if abs(T[0][1]) < .95 else (1, 0, 0))
        u = _norm(_cross(ref, T[0]))
        v = _cross(T[0], u)
        frames.append((u, v))
        for i in range(1, m):
            # double reflection (Wang et al.)
            v1 = _sub(path[i], path[i - 1])
            c1 = _dot(v1, v1)
            if c1 < 1e-14:
                frames.append(frames[-1])
                continue
            uL = _add(u, v1, -2.0 / c1 * _dot(v1, u))
            tL = _add(T[i - 1], v1, -2.0 / c1 * _dot(v1, T[i - 1]))
            v2 = _sub(T[i], tL)
            c2 = _dot(v2, v2)
            u = _add(uL, v2, -2.0 / c2 * _dot(v2, uL)) if c2 > 1e-14 else uL
            u = _norm(u)
            v = _cross(T[i], u)
            frames.append((u, v))
    k = len(section)
    # section normals (2D, outward): from neighbours
    sn = []
    for j in range(k):
        a = section[(j - 1) % k]
        b = section[(j + 1) % k]
        d = (b[0] - a[0], b[1] - a[1])
        sn.append(_norm((d[1], -d[0], 0.0)))
    # orientation of the section (clockwise or not) decides the outward side
    area = 0.0
    for j in range(k):
        a, b = section[j], section[(j + 1) % k]
        area += a[0] * b[1] - b[0] * a[1]
    sgn = -1.0 if area > 0 else 1.0
    rings, nrings = [], []
    for i in range(m):
        u, v = frames[i]
        s = scale[i] if scale is not None else 1.0
        ring, nr = [], []
        for j in range(k):
            x, y = section[j]
            ring.append(_add(_add(path[i], u, x * s), v, y * s))
            nr.append(_norm(_add(_lerp((0, 0, 0), u, sgn * sn[j][0]), v, sgn * sn[j][1])))
        rings.append(ring)
        nrings.append(nr)
    count = m if closed else m - 1
    for i in range(count):
        i1 = (i + 1) % m
        for j in range(k):
            j1 = (j + 1) % k
            pts = (rings[i][j], rings[i][j1], rings[i1][j1], rings[i1][j])
            if smooth_u:
                g.face(pts, f, normals=(nrings[i][j], nrings[i][j1], nrings[i1][j1], nrings[i1][j]))
            else:
                mid = _norm(_add(_add(nrings[i][j], nrings[i][j1]), _add(nrings[i1][j1], nrings[i1][j])))
                g.face(pts, f, normal=mid)
    if caps and not closed:
        g.face(rings[0], f, normal=(-T[0][0], -T[0][1], -T[0][2]))
        g.face(rings[-1], f, normal=T[-1])


def circle_section(r, n, phase=0.0):
    return [(r * math.cos(2 * math.pi * i / n + phase), r * math.sin(2 * math.pi * i / n + phase)) for i in range(n)]


def rect_section(w, h, chamfer=0.0):
    a, b = w / 2, h / 2
    if chamfer <= 0:
        return [(-a, -b), (a, -b), (a, b), (-a, b)]
    c = min(chamfer, a * .45, b * .45)
    return [(-a + c, -b), (a - c, -b), (a, -b + c), (a, b - c), (a - c, b), (-a + c, b), (-a, b - c), (-a, -b + c)]


def tube(g, path, r, f, sides, closed=False, caps=True):
    sweep(g, circle_section(r, sides), path, f, closed=closed, caps=caps)


def pointed_arch(L, H, n):
    """A two-centred pointed arch's half from its springer (0, 0) to its apex (L, H): n + 1 points (offset from the springer,
    height). H < L gives a segmental arch (one circle through both springers and the apex)."""
    if H <= 1e-6:
        return [(L * i / n, 0.0) for i in range(n + 1)]
    if H < L:
        return [(L * i / n, arch_height(L, H, L * i / n)) for i in range(n + 1)]
    c = (H * H - L * L) / (2 * L)
    R = L + c
    th = math.acos(max(-1.0, min(1.0, c / R)))
    out = []
    for i in range(n + 1):
        t = th * i / n
        out.append(((L + c) - R * math.cos(t), R * math.sin(t)))
    out[-1] = (L, H)
    return out


def arch_height(L, H, d):
    """The arch's height at distance d from its springer (half span L, rise H; pointed when H >= L, segmental below)."""
    if d <= 0:
        return 0.0
    if d >= L:
        return H
    if H < L:
        R = (L * L + H * H) / (2 * H)
        return max(0.0, math.sqrt(max(0.0, R * R - (L - d) ** 2)) - (R - H))
    c = (H * H - L * L) / (2 * L)
    R = L + c
    return math.sqrt(max(0.0, R * R - (R - d) ** 2))


def catmull(pts, per=6, closed=False):
    """A Catmull-Rom curve through the points, `per` samples a span."""
    n = len(pts)
    out = []
    spans = n if closed else n - 1
    for i in range(spans):
        p0 = pts[(i - 1) % n] if (closed or i > 0) else pts[0]
        p1, p2 = pts[i], pts[(i + 1) % n]
        p3 = pts[(i + 2) % n] if (closed or i + 2 < n) else pts[-1]
        for k in range(per):
            t = k / per
            t2, t3 = t * t, t * t * t
            out.append(tuple(.5 * (2 * p1[j] + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2 + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in range(3)))
    if not closed:
        out.append(tuple(pts[-1]))
    return out


def arch_width(L, H, h):
    """The arch's half opening at height h over its springing (L at the springing, 0 at the apex)."""
    if h <= 0:
        return L
    if h >= H:
        return 0.0
    if H < L:
        R = (L * L + H * H) / (2 * H)
        return math.sqrt(max(0.0, R * R - (h + R - H) ** 2))
    c = (H * H - L * L) / (2 * L)
    R = L + c
    return max(0.0, math.sqrt(max(0.0, R * R - h * h)) - c)


# ---------------------------------------------------------------------------------------------------------------------------
# Noise (numpy, deterministic)
# ---------------------------------------------------------------------------------------------------------------------------
def _hash3(ix, iy, iz, seed):
    h = (ix * 73856093) ^ (iy * 19349663) ^ (iz * 83492791) ^ (seed * 2654435761)
    h = (h ^ (h >> 13)) * 1274126177
    h = h ^ (h >> 16)
    return (h & 0xFFFF).astype(np.float64) / 65535.0


def vnoise(p, freq=1.0, seed=0):
    """Value noise 0..1 at points p (N, 3)."""
    q = p * freq
    i = np.floor(q).astype(np.int64)
    t = q - i
    t = t * t * (3 - 2 * t)
    out = np.zeros(len(p))
    for dx in (0, 1):
        wx = t[:, 0] if dx else 1 - t[:, 0]
        for dy in (0, 1):
            wy = t[:, 1] if dy else 1 - t[:, 1]
            for dz in (0, 1):
                wz = t[:, 2] if dz else 1 - t[:, 2]
                out += wx * wy * wz * _hash3(i[:, 0] + dx, i[:, 1] + dy, i[:, 2] + dz, seed)
    return out


def fbm(p, freq=1.0, octaves=3, seed=0):
    s, a, tot = np.zeros(len(p)), 1.0, 0.0
    for o in range(octaves):
        s += a * vnoise(p, freq * (2.03 ** o), seed + o * 17)
        tot += a
        a *= .5
    return s / tot


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------------------------------------------------------------------
# Baking: ambient occlusion (Cycles) and colours (numpy)
# ---------------------------------------------------------------------------------------------------------------------------
def bake_ao(geos, distance=1.2, samples=24, occluders=()):
    """Cycles' ambient occlusion per corner for each Geo in `geos` (all in one scene, so they occlude each other; `occluders`
    take part without being baked). Returns one float array (corners) per Geo, 1 open .. 0 shut."""
    import bpy
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)
    obs = []

    def make(g, name):
        P, N, S, F = g.arrays()
        me = bpy.data.meshes.new(name)
        n = len(P)
        me.vertices.add(n)
        me.vertices.foreach_set('co', P.astype(np.float32).ravel())
        me.loops.add(n)
        me.loops.foreach_set('vertex_index', np.arange(n, dtype=np.int32))
        me.polygons.add(len(S))
        starts = np.zeros(len(S), dtype=np.int32)
        if len(S) > 1:
            starts[1:] = np.cumsum(S[:-1])
        me.polygons.foreach_set('loop_start', starts)
        me.update()
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob
    for i, g in enumerate(geos):
        ob = make(g, 'bake%d' % i)
        attr = ob.data.color_attributes.new('AO', 'BYTE_COLOR', 'CORNER')
        ob.data.color_attributes.active_color = attr
        obs.append(ob)
    for i, g in enumerate(occluders):
        make(g, 'occ%d' % i)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    if sc.world is None:
        sc.world = bpy.data.worlds.new('w')
    sc.world.light_settings.distance = distance
    for ob in obs:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    t = time.time()
    bpy.ops.object.bake(type='AO', target='VERTEX_COLORS')
    out = []
    for ob in obs:
        a = np.zeros(len(ob.data.loops) * 4, dtype=np.float32)
        ob.data.color_attributes['AO'].data.foreach_get('color', a)
        out.append(a.reshape(-1, 4)[:, 0].copy())
    print('ao: %d corners in %.1f s' % (sum(len(a) for a in out), time.time() - t))
    return out


class Bake:
    """The colours and channels of a Geo's corners: `colour` (N, 4) uint8 (sRGB albedo, AO), uv0 (N, 2), uv1 (N, 2), glow (N)
    bool. `world` is the corners' place in the stage (for a prop, its rest pose) for the patterns: dirt, moss and soot are
    the stage's, not the prop's own."""

    def __init__(self, g, ao=None, world=None, soot=None, floors=(0.0,), seed=0, moss_zone=None):
        P, N, S, F = g.arrays()
        W = P if world is None else world
        fc = np.repeat(F, S)
        n = len(P)
        # corners that meet (the same place, normal and finish) share one occlusion, so the export can merge them
        if ao is not None and n:
            key = np.concatenate([np.round(P * 2000).astype(np.int64), np.round(N * 200).astype(np.int64), fc[:, None]], axis=1)
            _, inv = np.unique(key, axis=0, return_inverse=True)
            inv = inv.ravel()
            ao = (np.bincount(inv, weights=ao.astype(np.float64)) / np.bincount(inv))[inv]
        base = np.array([FINS[i].rgb for i in range(len(FINS))], dtype=np.float64)
        col = base[fc].copy()
        dirt = np.array([f.dirt for f in FINS])[fc]
        moss = np.array([f.moss for f in FINS])[fc]
        glow = np.array([f.glow > 0 for f in FINS])[fc]
        detail = np.array([f.detail for f in FINS])[fc]
        smooth_ = np.array([f.smooth for f in FINS])[fc]
        metal = np.array([f.metal for f in FINS])[fc]
        param = np.array([f.glow if f.glow > 0 else f.param for f in FINS])[fc]
        wear = np.array([f.wear for f in FINS])[fc]
        Wd = W.astype(np.float64)
        Nd = N.astype(np.float64)
        aov = np.ones(n) if ao is None else np.clip(ao.astype(np.float64), 0, 1)
        # broad mottling: every finish varies a little across the stage (painted, not printed)
        mott = fbm(Wd, .35, 3, seed + 1) - .5
        fine = fbm(Wd, 2.2, 2, seed + 2) - .5
        col *= (1.0 + (mott * .26 + fine * .1) * dirt)[:, None]
        # height over the nearest floor below (grime and moss rise from the floor)
        fl = np.array(sorted(floors))
        above = np.full(n, 99.0)
        for y in fl:
            h = Wd[:, 1] - y
            above = np.where((h >= -.05) & (h < above), h, above)
        grime = (1 - smooth(0.0, 1.5, above)) * .3 * dirt
        col = col * (1 - grime[:, None]) + np.array([.16, .13, .1]) * grime[:, None]
        # crevices: the occlusion darkens and browns the albedo a little (the dust that settles there)
        crev = (1 - aov) * .35 * dirt
        col = col * (1 - crev[:, None]) + np.array([.12, .1, .08]) * crev[:, None]
        # moss: on finishes that take it, near the floor, in the shade and in the joints, in patches
        if moss.max() > 0:
            zone = np.ones(n) if moss_zone is None else moss_zone(Wd)
            patch = smooth(.45, .75, fbm(Wd, 1.3, 3, seed + 3))
            m = moss * zone * patch * np.clip((1 - smooth(0.0, 1.4, above)) + (1 - aov) * 1.4, 0, 1)
            m *= np.clip(.4 + .6 * (Nd[:, 1] > -.3), 0, 1)
            m = np.clip(m, 0, .8)
            mc = np.array(srgb(0x55663A))
            col = col * (1 - m[:, None]) + mc * m[:, None]
        # soot: plumes above the candles and the lamps (a sampler of the stage's soot volume)
        if soot is not None:
            s = soot(Wd) * dirt
            col *= (1 - np.clip(s, 0, .7))[:, None]
        # worn edges: bright where the normal turns from its neighbours' (only the finishes that take wear; the bevels carry it)
        if wear.max() > 0:
            tilt = 1 - np.abs(Nd).max(axis=1)
            w = wear * smooth(.15, .4, tilt)
            col = col * (1 - w[:, None]) + np.minimum(1, col * 1.35 + .06) * w[:, None]
        col = np.where(glow[:, None], base[fc], col)
        col = np.clip(col, 0, 1)
        self.colour = np.empty((n, 4), dtype=np.uint8)
        self.colour[:, :3] = np.round(col * 255)
        self.colour[:, 3] = np.round(np.where(glow, 1.0, aov) * 255)
        self.uv0 = np.stack([smooth_, metal], axis=1).astype(np.float32)
        self.uv1 = np.stack([detail.astype(np.float64), param], axis=1).astype(np.float32)
        self.glow = glow


class SootField:
    """A coarse volume of soot: plumes rising from each source (x, y, z, strength) : candles, lamps, braziers."""

    def __init__(self, lo, hi, cell=.2):
        self.lo = np.array(lo, dtype=np.float64)
        self.cell = cell
        self.dim = np.maximum(1, np.ceil((np.array(hi) - self.lo) / cell).astype(int))
        self.v = np.zeros(self.dim, dtype=np.float32)

    def plume(self, x, y, z, k=1.0, rise=1.4, width=.22):
        lo = np.floor((np.array([x - width * 2, y, z - width * 2]) - self.lo) / self.cell).astype(int)
        hi = np.ceil((np.array([x + width * 2, y + rise, z + width * 2]) - self.lo) / self.cell).astype(int)
        lo = np.clip(lo, 0, self.dim - 1)
        hi = np.clip(hi, 0, self.dim - 1)
        for i in range(lo[0], hi[0] + 1):
            for j in range(lo[1], hi[1] + 1):
                for l in range(lo[2], hi[2] + 1):
                    p = self.lo + (np.array([i, j, l]) + .5) * self.cell
                    up = (p[1] - y) / rise
                    if up < 0 or up > 1:
                        continue
                    wdt = width * (1 + up * 1.6)
                    d2 = ((p[0] - x) ** 2 + (p[2] - z) ** 2) / (wdt * wdt)
                    self.v[i, j, l] += k * .55 * math.exp(-d2) * (1 - up) ** 1.2

    def __call__(self, W):
        idx = np.floor((W - self.lo) / self.cell).astype(int)
        ok = np.all((idx >= 0) & (idx < self.dim), axis=1)
        out = np.zeros(len(W))
        ii = idx[ok]
        out[ok] = self.v[ii[:, 0], ii[:, 1], ii[:, 2]]
        return np.clip(out, 0, 1)


# ---------------------------------------------------------------------------------------------------------------------------
# Export: chunks and props into the bank
# ---------------------------------------------------------------------------------------------------------------------------
class OutMesh:
    """bank.py's mesh fields (Unity space), two sub-meshes: solid and glow."""

    def __init__(self, name):
        self.name = name
        self.positions, self.normals, self.uv0, self.uv1, self.colors = [], [], [], [], []
        self.submeshes = [[], []]
        self._lo = self._hi = (0, 0, 0)

    def bounds(self):
        return self._lo, self._hi


def to_mesh(name, g, bk, faces=None):
    """The Geo's faces (all, or the boolean mask `faces`) as one OutMesh: corners merged where every channel agrees,
    polygons fanned into triangles, glowing faces on the second sub-mesh."""
    P, N, S, F = g.arrays()
    starts = np.zeros(len(S), dtype=np.int64)
    if len(S) > 1:
        starts[1:] = np.cumsum(S[:-1])
    sel = np.arange(len(S)) if faces is None else np.nonzero(faces)[0]
    om = OutMesh(name)
    if len(sel) == 0:
        return None
    # the corners of the selected faces
    counts = S[sel]
    cidx = np.concatenate([np.arange(starts[f], starts[f] + S[f]) for f in sel]) if len(sel) < 2000 else \
        np.repeat(starts[sel], counts) + (np.arange(counts.sum()) - np.repeat(np.cumsum(counts) - counts, counts))
    key = np.concatenate([np.round(P[cidx] * 5000).astype(np.int64), np.round(N[cidx] * 500).astype(np.int64),
                          bk.colour[cidx].astype(np.int64), np.round(bk.uv0[cidx] * 1000).astype(np.int64),
                          np.round(bk.uv1[cidx] * 1000).astype(np.int64)], axis=1)
    uniq, first, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
    inv = inv.ravel()
    src = cidx[first]
    om.positions = P[src].astype(np.float64).tolist()
    nn = N[src].astype(np.float64)
    nn /= np.maximum(np.linalg.norm(nn, axis=1, keepdims=True), 1e-9)
    om.normals = nn.tolist()
    om.uv0 = bk.uv0[src].astype(np.float64).tolist()
    om.uv1 = bk.uv1[src].astype(np.float64).tolist()
    om.colors = (bk.colour[src].astype(np.float64) / 255.0).tolist()
    # triangles: fan each face
    pos = 0
    solid, glow = [], []
    gl = bk.glow
    for f, k in zip(sel, counts):
        ids = inv[pos:pos + k]
        target = glow if gl[starts[f]] else solid
        for t in range(1, k - 1):
            target.extend((int(ids[0]), int(ids[t]), int(ids[t + 1])))
        pos += k
    om.submeshes = [solid, glow] if glow else [solid]
    lo = P[src].min(axis=0)
    hi = P[src].max(axis=0)
    om._lo, om._hi = tuple(float(x) for x in lo), tuple(float(x) for x in hi)
    return om


class StageBank:
    """A stage's bank: static chunks, block props, rig props and the covered blocks."""

    def __init__(self):
        self.bank = bankfmt.Bank()
        self.solid = self.bank.material('stage')
        self.glowm = self.bank.material('stage-glow', emission_color=(1, 1, 1))
        self.stats = {}

    def mesh(self, om):
        mats = [self.solid, self.glowm][:len(om.submeshes)]
        return self.bank.mesh(om.name, om, mats)

    def add(self, name, group, om, place=None):
        if om is None:
            return
        mi = self.mesh(om)
        self.bank.prop(name, group, [(mi, place or [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0])])
        tris = sum(len(s) for s in om.submeshes) // 3
        tier = name.rsplit('/', 1)[-1]
        st = self.stats.setdefault((group, tier), [0, 0, 0])
        st[0] += 1
        st[1] += tris
        st[2] += len(om.positions)

    def share(self, name, group, mesh_index):
        self.bank.prop(name, group, [(mesh_index, [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0])])

    def cover(self, block_id):
        self.bank.prop('cover/' + block_id, 'cover', [])

    def write(self, assets, name):
        sys.path.insert(0, os.path.join(HERE, '..', 'pack_bank'))
        from build_pack_bank import bytes_meta
        folder = os.path.join(assets, RESOURCES)
        os.makedirs(folder, exist_ok=True)
        path = os.path.join(folder, name + '.bytes')
        data = self.bank.write()
        with open(path, 'wb') as fh:
            fh.write(data)
        meta = path + '.meta'
        if not os.path.exists(meta):
            with open(meta, 'w', newline='\n') as fh:
                fh.write(bytes_meta(RESOURCES + '/' + name + '.bytes'))
        for (group, tier), (n, tris, verts) in sorted(self.stats.items()):
            print('bank %-7s %-5s %4d props %8d triangles %8d vertices' % (group, tier, n, tris, verts))
        print('wrote', path, len(data), 'bytes')
        return path


# ---------------------------------------------------------------------------------------------------------------------------
# Coplanar faces (owner 2026-11-10: "We need to fix the Z-Fighting on the new maps")
# ---------------------------------------------------------------------------------------------------------------------------
# m a stacked face stands proud of the face under it, per layer, by tier: clear of the depth buffer's step where the tier draws
# (24 bits and the camera's .06 m near plane: about z^2 x 1e-6 m, under a millimetre at the Fine tier's 30 m, 3 mm at the Mid's
# 55 m, 14 mm at 120 m) and under a pixel there
LIFT_TIER = (.006, .012, .02)
# m apart (or closer) two same-facing overlapping faces still fight at the tier's range, by tier
NEAR_TIER = (.003, .005, .008)
LIFT = LIFT_TIER[0]
LIFT_MIN = 4e-3  # m2 of overlap (or a share LIFT_SHARE of the smaller face) below which two faces are left as they are
LIFT_SHARE = .02


def _poly2(pts, ax):
    keep = [i for i in range(3) if i != ax]
    return [(float(p[keep[0]]), float(p[keep[1]])) for p in pts]


def _area2(poly):
    s = 0.0
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        s += a[0] * b[1] - b[0] * a[1]
    return s / 2


def _clip(subject, clipper):
    """Sutherland-Hodgman: the part of convex `subject` inside convex `clipper` (any winding)."""
    if _area2(clipper) < 0:
        clipper = clipper[::-1]
    out = subject
    for i in range(len(clipper)):
        a, b = clipper[i], clipper[(i + 1) % len(clipper)]
        inp, out = out, []
        if not inp:
            break
        ex, ey = b[0] - a[0], b[1] - a[1]

        def side(p):
            return ex * (p[1] - a[1]) - ey * (p[0] - a[0])
        for j in range(len(inp)):
            p, q = inp[j], inp[(j + 1) % len(inp)]
            sp, sq = side(p), side(q)
            if sq >= -1e-12:
                if sp < -1e-12:
                    t = sp / (sp - sq)
                    out.append((p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1])))
                out.append(q)
            elif sp >= -1e-12:
                t = sp / (sp - sq)
                out.append((p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1])))
    return out


def lift_coplanar(g, eps=None, report=None, lift=None, passes=3):
    """Settles the z-fighting of a static scene: wherever two faces turned the same way lie in one plane (within `eps`) and
    overlap (the dance floor's top on the hall floor's, a panel flush on its wall, a walkway's trim on its deck: `eps` apart or
    less, NEAR_TIER by tier, too close for the depth buffer to part at the tier's range), the smaller is set `lift` (LIFT_TIER by
    tier) over the larger along its normal (moved only by what it lacks), a third over both again, and so on (by area, largest at
    the bottom). Only the
    overlapping faces move, along their own plane's normal, so nothing opens: the larger face is right under the gap. In place;
    returns the first pass's (pairs, m2 overlapped, faces lifted, the largest lift in mm). A lift can land a face on another's
    plane: up to `passes` passes run, until none overlap. Faces turned the other way (a panel's back on the wall) never fight."""
    step = LIFT if lift is None else lift
    eps = NEAR_TIER[0] if eps is None else eps
    P, N, S, F = g.arrays()
    nf = len(S)
    if nf == 0:
        return 0, 0.0, 0
    starts = np.zeros(nf, dtype=np.int64)
    if nf > 1:
        starts[1:] = np.cumsum(S[:-1])
    P64 = P.astype(np.float64)
    # Newell normals and areas, per face
    face_of = np.repeat(np.arange(nf), S)
    nxt = np.arange(len(P64)) + 1
    nxt[starts + S - 1] = starts
    a, b = P64, P64[nxt]
    cr = np.stack([(a[:, 1] - b[:, 1]) * (a[:, 2] + b[:, 2]), (a[:, 2] - b[:, 2]) * (a[:, 0] + b[:, 0]),
                   (a[:, 0] - b[:, 0]) * (a[:, 1] + b[:, 1])], axis=1)
    nrm = np.zeros((nf, 3))
    np.add.at(nrm, face_of, cr)
    area = np.linalg.norm(nrm, axis=1) / 2
    ok = area > 1e-6
    unit = np.zeros_like(nrm)
    unit[ok] = nrm[ok] / (2 * area[ok][:, None])
    d = np.einsum('ij,ij->i', unit, P64[starts])
    cen = np.add.reduceat(P64, starts, axis=0) / S[:, None]
    lo = np.minimum.reduceat(P64, starts, axis=0)
    hi = np.maximum.reduceat(P64, starts, axis=0)
    ax = np.argmax(np.abs(unit), axis=1)
    # planes: the normal to 1/400 and the offset to eps; in each, a grid of 3 m cells holding every face its bounds reach (a big
    # floor meets the small faces on it however far they are from its middle)
    kn = np.round(unit * 400).astype(np.int64)
    kd = np.floor(d / eps).astype(np.int64)
    clo = np.floor(lo / 3.0).astype(np.int64)
    chi = np.floor(hi / 3.0).astype(np.int64)
    planes = {}
    for f in np.nonzero(ok)[0].tolist():
        grid = planes.setdefault((int(kn[f, 0]), int(kn[f, 1]), int(kn[f, 2]), int(kd[f])), {})
        for cx in range(int(clo[f, 0]), int(chi[f, 0]) + 1):
            for cy in range(int(clo[f, 1]), int(chi[f, 1]) + 1):
                for cz in range(int(clo[f, 2]), int(chi[f, 2]) + 1):
                    grid.setdefault((cx, cy, cz), []).append(f)
    U, D, AR, LO, HI, AX = unit.tolist(), d.tolist(), area.tolist(), lo.tolist(), hi.tolist(), ax.tolist()
    over = {}  # face -> the larger faces it overlaps
    pairs, total = 0, 0.0
    seen = set()
    for pk, grid in planes.items():
        for dd in (0, 1):
            other = planes.get((pk[0], pk[1], pk[2], pk[3] + dd))
            if other is None:
                continue
            for c, fs in grid.items():
                near = other.get(c)
                if not near:
                    continue
                for i in fs:
                    ui, di, li, hi_ = U[i], D[i], LO[i], HI[i]
                    for j in near:
                        if j == i:
                            continue
                        pair = (i, j) if i < j else (j, i)
                        if pair in seen:
                            continue
                        seen.add(pair)
                        uj = U[j]
                        if abs(di - D[j]) > eps or ui[0] * uj[0] + ui[1] * uj[1] + ui[2] * uj[2] < .9995:
                            continue
                        lj, hj = LO[j], HI[j]
                        if li[0] > hj[0] + eps or li[1] > hj[1] + eps or li[2] > hj[2] + eps or lj[0] > hi_[0] + eps or lj[1] > hi_[1] + eps or lj[2] > hi_[2] + eps:
                            continue
                        axis = AX[i]
                        ov = _clip(_poly2(P64[starts[i]:starts[i] + S[i]], axis), _poly2(P64[starts[j]:starts[j] + S[j]], axis))
                        if len(ov) < 3:
                            continue
                        o = abs(_area2(ov))
                        small, big = (i, j) if AR[i] < AR[j] or AR[i] == AR[j] and i > j else (j, i)
                        if o < LIFT_MIN and o < LIFT_SHARE * AR[small]:
                            continue
                        over.setdefault(small, []).append(big)
                        pairs += 1
                        total += o
    # Stacked by area, the largest at the bottom: each face moved out along its normal just far enough to stand `step` over every
    # larger face it overlaps (where that one ended up), never back. layer: how many faces it stands over, at most.
    off, layer = {}, {}
    for f in sorted(set(over) | {x for v in over.values() for x in v}, key=lambda f: -AR[f]):
        need, lv = 0.0, 0
        for bf in over.get(f, ()):
            need = max(need, D[bf] + off.get(bf, 0.0) + step - D[f])
            lv = max(lv, layer.get(bf, 0) + 1)
        off[f], layer[f] = need, lv
    moved = 0
    if over:
        Pn = P64.copy()
        for f, o in off.items():
            if o <= 0:
                continue
            Pn[starts[f]:starts[f] + S[f]] += unit[f] * o
            moved += 1
        g.p = array.array('f')
        g.p.frombytes(Pn.astype(np.float32).tobytes())
    deepest = max(off.values(), default=0.0) * 1000
    if pairs and passes > 1:
        lift_coplanar(g, eps, None, lift, passes - 1)
    if report is not None:
        report.append((pairs, total, moved, deepest))
    return pairs, total, moved, deepest


class Chunks:
    """World-space cells (size x size in plan, split at the heights `bands`) a static face falls into by its centre."""

    def __init__(self, size=12.0, origin=(-36.0, -24.0), bands=(-1.0, 9.0)):
        self.size, self.origin, self.bands = size, origin, bands

    def ids(self, g):
        P, N, S, F = g.arrays()
        starts = np.zeros(len(S), dtype=np.int64)
        if len(S) > 1:
            starts[1:] = np.cumsum(S[:-1])
        csum = np.add.reduceat(P.astype(np.float64), starts, axis=0) / S[:, None]
        ix = np.floor((csum[:, 0] - self.origin[0]) / self.size).astype(int)
        iz = np.floor((csum[:, 2] - self.origin[1]) / self.size).astype(int)
        iy = np.searchsorted(np.array(self.bands), csum[:, 1])
        return ix * 10000 + iz * 10 + iy


def load_layout(path):
    with open(path, encoding='utf-8') as fh:
        return json.load(fh)


# ---------------------------------------------------------------------------------------------------------------------------
# Preview: the baked stage rendered in Blender (workbench, vertex colours times the occlusion), to judge the models quickly
# ---------------------------------------------------------------------------------------------------------------------------
def preview(geo_bakes, views, out_dir, size=(960, 540)):
    """geo_bakes: [(Geo, Bake)]; views: [(name, eye, target, fov)] in Unity space. Writes out_dir/<name>.png."""
    import bpy
    from mathutils import Matrix, Vector
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for i, (g, bk) in enumerate(geo_bakes):
        P, N, S, F = g.arrays()
        me = bpy.data.meshes.new('pv%d' % i)
        n = len(P)
        me.vertices.add(n)
        Pm = P.astype(np.float32) * np.array([-1, 1, 1], np.float32)
        me.vertices.foreach_set('co', Pm.ravel())
        me.loops.add(n)
        me.loops.foreach_set('vertex_index', np.arange(n, dtype=np.int32))
        me.polygons.add(len(S))
        starts = np.zeros(len(S), dtype=np.int32)
        if len(S) > 1:
            starts[1:] = np.cumsum(S[:-1])
        me.polygons.foreach_set('loop_start', starts)
        me.update()
        attr = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
        c = bk.colour.astype(np.float32) / 255.0
        lin = np.where(c[:, :3] <= .04045, c[:, :3] / 12.92, ((c[:, :3] + .055) / 1.055) ** 2.4)
        rgb = lin * (.35 + .65 * c[:, 3:4])
        rgba = np.concatenate([rgb, np.ones((n, 1), np.float32)], axis=1)
        attr.data.foreach_set('color', rgba.ravel())
        me.color_attributes.active_color = attr
        ob = bpy.data.objects.new('pv%d' % i, me)
        bpy.context.scene.collection.objects.link(ob)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'VERTEX'
    sc.display.shading.show_cavity = True
    sc.display.shading.cavity_type = 'WORLD'
    sc.render.resolution_x, sc.render.resolution_y = size
    sc.view_settings.view_transform = 'Standard'
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    sc.collection.objects.link(cam)
    sc.camera = cam
    os.makedirs(out_dir, exist_ok=True)
    for name, eye, target, fov in views:
        e, t = Vector((-eye[0], eye[1], eye[2])), Vector((-target[0], target[1], target[2]))
        fwd = (t - e).normalized()
        up = Vector((0, 1, 0))
        right = fwd.cross(up).normalized()
        upv = right.cross(fwd)
        m = Matrix(((right.x, upv.x, -fwd.x), (right.y, upv.y, -fwd.y), (right.z, upv.z, -fwd.z)))
        cam.matrix_world = Matrix.Translation(e) @ m.to_4x4()
        cam.data.angle = math.radians(fov)
        cam.data.clip_start, cam.data.clip_end = .05, 400
        sc.render.filepath = os.path.join(out_dir, name + '.png')
        bpy.ops.render.render(write_still=True)
