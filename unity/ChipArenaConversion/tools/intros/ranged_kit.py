"""The ranged intros' set kit (docs/INTROS-RANGED.md): what the four sets share on top of tools/stages/stagekit.py. Run inside
Blender 5.x (bpy, numpy) through ranged_sets.py. Everything is authored in Unity space (metres, +y up), the pilot at the origin
facing +z; the intro draws each set turned by NativeProceduralEntrance.Facing, so a set point (ux, uy, uz) is the source frame's
Local(-ux, uy, uz) in Core (NativeRangedSets lists the ones the intros aim at).

Bank props (ChipArenaIntroRanged, group "intro"): "<set>/<piece>" is the Fine tier, "<set>/<piece>/low" the Low tier.
"""
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'stages'))
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, obox, cylinder, lathe, sweep, tube, quat_axis, affine  # noqa: E402,F401

GROUP = 'intro'
F = {}


def fin(name, hexcol, detail=sk.DETAIL_NONE, **kw):
    F[name] = sk.fin(name, hexcol, detail, **kw)
    return F[name]


# ---------------------------------------------------------------------------------------------------------------------------
# Polygons and prisms
# ---------------------------------------------------------------------------------------------------------------------------
def _area(poly):
    s = 0.0
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        s += a[0] * b[1] - b[0] * a[1]
    return s * .5


def triangulate(poly):
    """Ear clipping of a simple polygon [(x, y)] (either winding): triangles as index triples, counter-clockwise."""
    n = len(poly)
    idx = list(range(n))
    if _area(poly) < 0:
        idx.reverse()
    out = []

    def inside(p, a, b, c):
        def s(p1, p2, p3):
            return (p1[0] - p3[0]) * (p2[1] - p3[1]) - (p2[0] - p3[0]) * (p1[1] - p3[1])
        d1, d2, d3 = s(p, a, b), s(p, b, c), s(p, c, a)
        neg = (d1 < 0) or (d2 < 0) or (d3 < 0)
        pos = (d1 > 0) or (d2 > 0) or (d3 > 0)
        return not (neg and pos)
    guard = 0
    while len(idx) > 3 and guard < 10000:
        guard += 1
        m = len(idx)
        for k in range(m):
            i0, i1, i2 = idx[(k - 1) % m], idx[k], idx[(k + 1) % m]
            a, b, c = poly[i0], poly[i1], poly[i2]
            cross = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
            if cross <= 1e-12:
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


def prism(g, poly, origin, u, v, depth, f, w=None, caps=True):
    """A prism: the polygon [(a, b)] in the plane origin + a u + b v, extruded `depth` along w (default u x v)."""
    plane = sk._norm(sk._cross(u, v))
    if w is None:
        w = plane
    P0 = [sk._add(sk._add(origin, u, a), v, b) for a, b in poly]
    P1 = [sk._add(p, w, depth) for p in P0]
    n = len(poly)
    ccw = _area(poly) > 0
    for i in range(n):
        j = (i + 1) % n
        a0, b0, a1, b1 = P0[i], P0[j], P1[i], P1[j]
        e = sk._sub(b0, a0)
        # the outward side of a counter-clockwise outline (in u, v) is e x (u x v)
        nrm = sk._norm(sk._cross(e, plane) if ccw else sk._cross(plane, e))
        g.face((a0, b0, b1, a1), f, nrm)
    if caps:
        tris = triangulate(poly)
        back = (-w[0], -w[1], -w[2])
        for t in tris:
            g.face((P0[t[0]], P0[t[1]], P0[t[2]]), f, back)
            g.face((P1[t[0]], P1[t[1]], P1[t[2]]), f, w)


def prism_z(g, poly, z0, z1, f):
    """The polygon in x, y extruded along z from z0 to z1."""
    prism(g, poly, (0.0, 0.0, z0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), z1 - z0, f, w=(0.0, 0.0, 1.0))


def prism_x(g, poly, x0, x1, f):
    """The polygon in z, y extruded along x from x0 to x1."""
    prism(g, poly, (x0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, 1.0, 0.0), x1 - x0, f, w=(1.0, 0.0, 0.0))


def beam(g, p0, p1, w, h, f, T=None, bevel=.012, up=None):
    """A square timber from p0 to p1 (width w across, h up)."""
    d = sk._sub(p1, p0)
    L = math.sqrt(sk._dot(d, d))
    if L < 1e-6:
        return
    u, v, wv = sk.frame(p0, p1, up)
    t = Geo()
    box(t, -w / 2, w / 2, -h / 2, h / 2, 0, L, f, T, bevel, cell=0)
    m = [u[0], v[0], wv[0], p0[0], u[1], v[1], wv[1], p0[1], u[2], v[2], wv[2], p0[2]]
    g.extend(t.transformed(m))


def place(g, part, m):
    g.extend(part.transformed(m))


def yaw(a, t=(0.0, 0.0, 0.0), s=1.0):
    return affine(quat_axis(0, 1, 0, a), t, (s, s, s))


def disc(g, centre, r, f, sides, y_up=True):
    pts = [(centre[0] + r * math.cos(2 * math.pi * k / sides), centre[1], centre[2] + r * math.sin(2 * math.pi * k / sides)) for k in range(sides)]
    g.face(pts[::-1] if y_up else pts, f, (0.0, 1.0 if y_up else -1.0, 0.0))


def ground(g, x0, x1, z0, z1, y, f, cell, height=None):
    """A ground sheet cut into cells, each corner lifted by height(x, z) (normals from the slope)."""
    nx = max(1, int(math.ceil((x1 - x0) / cell)))
    nz = max(1, int(math.ceil((z1 - z0) / cell)))
    xs = [x0 + (x1 - x0) * i / nx for i in range(nx + 1)]
    zs = [z0 + (z1 - z0) * j / nz for j in range(nz + 1)]
    H = [[(height(x, z) if height else 0.0) + y for z in zs] for x in xs]

    def nrm(i, j):
        i0, i1 = max(0, i - 1), min(nx, i + 1)
        j0, j1 = max(0, j - 1), min(nz, j + 1)
        dx = (H[i1][j] - H[i0][j]) / max(1e-6, xs[i1] - xs[i0])
        dz = (H[i][j1] - H[i][j0]) / max(1e-6, zs[j1] - zs[j0])
        return sk._norm((-dx, 1.0, -dz))
    for i in range(nx):
        for j in range(nz):
            pts = ((xs[i], H[i][j], zs[j]), (xs[i], H[i][j + 1], zs[j + 1]), (xs[i + 1], H[i + 1][j + 1], zs[j + 1]), (xs[i + 1], H[i + 1][j], zs[j]))
            g.face(pts, f, normals=(nrm(i, j), nrm(i, j + 1), nrm(i + 1, j + 1), nrm(i + 1, j)))


def dome(g, radius, f, rings, sides, below=-.12):
    """A sky dome round the origin (faces inward), from `below` (sin of the elevation) to the zenith."""
    els = [below + (1 - below) * (k / rings) ** 1.4 for k in range(rings + 1)]
    for k in range(rings):
        e0, e1 = math.asin(max(-1, min(1, els[k]))), math.asin(max(-1, min(1, els[k + 1])))
        for s in range(sides):
            a0, a1 = 2 * math.pi * s / sides, 2 * math.pi * (s + 1) / sides
            def P(e, a):
                return (radius * math.cos(e) * math.sin(a), radius * math.sin(e), radius * math.cos(e) * math.cos(a))
            pts = [P(e0, a0), P(e0, a1), P(e1, a1), P(e1, a0)]
            if k == rings - 1:
                pts = [P(e0, a0), P(e0, a1), P(e1, a0)]
            c = (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts), sum(p[2] for p in pts) / len(pts))
            g.face(pts, f, sk._norm((-c[0], -c[1], -c[2])))


def rock(g, centre, r, f, seed, squash=(1.0, .7, 1.0), detail=1):
    """A boulder: an octahedron subdivided `detail` times, its corners pushed in and out by noise; flat faces."""
    V = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]
    Fc = [(0, 2, 4), (4, 2, 1), (1, 2, 5), (5, 2, 0), (4, 3, 0), (1, 3, 4), (5, 3, 1), (0, 3, 5)]
    V = [sk._norm(p) for p in V]
    for _ in range(detail):
        mid = {}
        NF = []

        def m(a, b):
            k = (min(a, b), max(a, b))
            if k not in mid:
                mid[k] = len(V)
                V.append(sk._norm(sk._lerp(V[a], V[b], .5)))
            return mid[k]
        for a, b, c in Fc:
            ab, bc, ca = m(a, b), m(b, c), m(c, a)
            NF += [(a, ab, ca), (ab, b, bc), (ca, bc, c), (ab, bc, ca)]
        Fc = NF
    P = []
    for p in V:
        k = .78 + .44 * sk.vnoise(np.array([[p[0] * 1.7 + seed, p[1] * 1.7, p[2] * 1.7 - seed]]), 1.0, seed)[0]
        P.append((centre[0] + p[0] * r * squash[0] * k, centre[1] + p[1] * r * squash[1] * k, centre[2] + p[2] * r * squash[2] * k))
    for a, b, c in Fc:
        pts = (P[a], P[b], P[c])
        cen = sk._lerp(sk._lerp(pts[0], pts[1], .5), pts[2], 1 / 3)
        g.face(pts, f, sk._norm(sk._sub(cen, centre)))


# A stroke font on a 4 x 6 grid (x right, y up): enough capitals for a sign. Each letter a list of polylines.
STROKES = {
    'S': [[(4, 6), (0, 6), (0, 3), (4, 3), (4, 0), (0, 0)]],
    'A': [[(0, 0), (0, 4), (2, 6), (4, 4), (4, 0)], [(0, 3), (4, 3)]],
    'L': [[(0, 6), (0, 0), (4, 0)]],
    'O': [[(0, 0), (0, 6), (4, 6), (4, 0), (0, 0)]],
    'N': [[(0, 0), (0, 6), (4, 0), (4, 6)]],
    'B': [[(0, 0), (0, 6), (3, 6), (4, 5), (3, 3), (0, 3)], [(3, 3), (4, 2), (4, 1), (3, 0), (0, 0)]],
    'R': [[(0, 0), (0, 6), (4, 6), (4, 3), (0, 3), (4, 0)]],
    'E': [[(4, 6), (0, 6), (0, 0), (4, 0)], [(0, 3), (3, 3)]],
    'T': [[(0, 6), (4, 6)], [(2, 6), (2, 0)]],
    'H': [[(0, 0), (0, 6)], [(4, 0), (4, 6)], [(0, 3), (4, 3)]],
    'I': [[(2, 0), (2, 6)]],
    'F': [[(4, 6), (0, 6), (0, 0)], [(0, 3), (3, 3)]],
    'K': [[(0, 0), (0, 6)], [(4, 6), (0, 3), (4, 0)]],
    'G': [[(4, 6), (0, 6), (0, 0), (4, 0), (4, 3), (2, 3)]],
    'D': [[(0, 0), (0, 6), (3, 6), (4, 5), (4, 1), (3, 0), (0, 0)]],
    'C': [[(4, 6), (0, 6), (0, 0), (4, 0)]],
    'P': [[(0, 0), (0, 6), (4, 6), (4, 3), (0, 3)]],
    'U': [[(0, 6), (0, 0), (4, 0), (4, 6)]],
    'W': [[(0, 6), (1, 0), (2, 4), (3, 0), (4, 6)]],
    'Y': [[(0, 6), (2, 3), (4, 6)], [(2, 3), (2, 0)]],
    'X': [[(0, 0), (4, 6)], [(0, 6), (4, 0)]],
    'M': [[(0, 0), (0, 6), (2, 3), (4, 6), (4, 0)]],
    'V': [[(0, 6), (2, 0), (4, 6)]],
    '0': [[(0, 0), (0, 6), (4, 6), (4, 0), (0, 0)]],
    '1': [[(1, 5), (2, 6), (2, 0)]],
    '7': [[(0, 6), (4, 6), (1, 0)]],
}


def lettering(g, text, origin, right, up, cell, f, r, sides=4):
    """Text as tubes (radius r) on the plane origin + x right + y up; each letter 4 x 6 cells of `cell` m, a 2-cell gap."""
    x = 0.0
    for ch in text:
        if ch == ' ':
            x += 4 * cell
            continue
        for line in STROKES.get(ch, []):
            pts = [sk._add(sk._add(origin, right, (x + px * cell)), up, py * cell) for px, py in line]
            if len(pts) >= 2:
                sweep(g, sk.circle_section(r, sides), pts, f, caps=True, smooth_u=True)
        x += 6 * cell


def pillow(g, centre, half, yaw_, f, nu=8, nv=6, e=.35, sag=.0):
    """A soft box (a superellipsoid, |x|^(2/e) + ... = 1): a sandbag, a cushion. half: its half sizes; sag lowers its middle top."""
    def P(u, v):
        cu, su = math.cos(u), math.sin(u)
        cv, sv = math.cos(v), math.sin(v)

        def sp(c, k):
            return math.copysign(abs(c) ** k, c)
        x = half[0] * sp(cv, e) * sp(cu, e)
        y = half[1] * sp(sv, e)
        z = half[2] * sp(cv, e) * sp(su, e)
        if y > 0:
            y -= sag * (1 - (abs(x) / half[0]) ** 2) * (1 - (abs(z) / half[2]) ** 2)
        return (x, y, z)

    def Nrm(u, v):
        # the normal of the matching ellipsoid (rounder than the shape: soft shading)
        p = P(u, v)
        return sk._norm((p[0] / (half[0] ** 2), p[1] / (half[1] ** 2), p[2] / (half[2] ** 2)))
    us = [2 * math.pi * i / nu for i in range(nu)]
    vs = [-math.pi / 2 + math.pi * j / nv for j in range(nv + 1)]
    m = rk_yaw(yaw_, centre)
    t = Geo()
    for i in range(nu):
        for j in range(nv):
            u0, u1 = us[i], us[(i + 1) % nu]
            v0, v1 = vs[j], vs[j + 1]
            pts = [P(u0, v0), P(u1, v0), P(u1, v1), P(u0, v1)]
            ns = [Nrm(u0, v0), Nrm(u1, v0), Nrm(u1, v1), Nrm(u0, v1)]
            if j == 0:
                pts, ns = [pts[0], pts[2], pts[3]], [ns[0], ns[2], ns[3]]
            elif j == nv - 1:
                pts, ns = pts[:3], ns[:3]
            t.face(pts, f, normals=ns)
    g.extend(t.transformed(m))


def rk_yaw(a, t=(0.0, 0.0, 0.0), s=1.0):
    return affine(quat_axis(0, 1, 0, a), t, (s, s, s))


def paint(g, bk, fn, finish=None):
    """Recolour corners: fn(P (n, 3)) -> sRGB (n, 3) 0..1 for the corners whose finish is `finish` (all when None)."""
    P, N, S, Fi = g.arrays()
    fc = np.repeat(Fi, S)
    mask = np.ones(len(P), dtype=bool) if finish is None else (fc == finish)
    if not mask.any():
        return
    rgb = np.clip(fn(P[mask].astype(np.float64)), 0, 1)
    bk.colour[mask, :3] = np.round(rgb * 255).astype(np.uint8)


def lerp3(a, b, t):
    return a + (b - a) * t[:, None]


def hexrgb(h):
    return np.array(sk.srgb(h), dtype=np.float64)


# ---------------------------------------------------------------------------------------------------------------------------
# The bank
# ---------------------------------------------------------------------------------------------------------------------------
class Out:
    """The set bank and its book-keeping (props, sizes)."""

    def __init__(self):
        self.sb = sk.StageBank()
        self.sb.far_layers = False
        self.sizes = {}
        self.ao = True
        self.preview = []

    def add(self, name, g, tier, ao=None, floors=(0.0,), paint_fn=None, ao_distance=1.0, ao_samples=16, ao_ease=.8, show=None):
        """Bakes Geo `g` (occlusion unless ao is False) and adds it as prop `name` (Fine) or `name/low` (Low tier). show: where the
        previews draw it (a 3x4 placement; None leaves a moving part out of them, True draws it where it is)."""
        if g.faces == 0:
            return None
        a = None
        if (self.ao if ao is None else ao):
            a = sk.bake_ao([g], distance=ao_distance, samples=ao_samples)[0]
            a = 1 - (1 - a) * ao_ease
        bk = sk.Bake(g, a, floors=floors)
        if paint_fn is not None:
            paint_fn(g, bk)
        full = name if tier == 0 else name + '/low'
        om = sk.to_mesh(full, g, bk)
        if om is None:
            return None
        mi = self.sb.mesh(om)
        self.sb.bank.prop(full, GROUP, [(mi, [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0])])
        tris = sum(len(s) for s in om.submeshes) // 3
        self.sizes[full] = (len(om.positions), tris)
        if tier == 0 and show is not None:
            gg = g if show is True else g.transformed(show)
            self.preview.append((gg, bk))
        return mi

    def instances(self, name, mesh_index, mats):
        """A prop of one mesh at many 3x4 placements."""
        self.sb.bank.prop(name, GROUP, [(mesh_index, m) for m in mats])

    def report(self):
        tv = tt = 0
        for k in sorted(self.sizes):
            v, t = self.sizes[k]
            tv += v
            tt += t
            print('  %-34s %7d vertices %7d triangles' % (k, v, t))
        print('  total %d vertices, %d triangles (about %.2f MB)' % (tv, tt, (tv * 44 + tt * 6) / 1e6))

    def write(self, out, name):
        return self.sb.write(out, name)
