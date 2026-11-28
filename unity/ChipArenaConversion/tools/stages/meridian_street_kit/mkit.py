"""Meridian's street kit: what the street furniture and lamp scripts share, on stagekit (tools/stages/stagekit.py).

The art bible (meridian/art-bible.md) sets the look: the finishes of its section 4.2, the lamp families of 6.2, the furniture of
6.3-6.6, the LOD policy of 10.2 and the caps of 10.3. Everything is authored in the game's space (Unity: metres, +y up, left
handed), in each module's own frame:

  free-standing   origin on the floor at the middle of the module's prop block; +z its front (the street side, the way a
                  seated person faces, a kiosk's door, a car's nose)
  wall-mounted    origin on the wall's face at the mount point; +z out of the wall; the placement gives the height
  span            origin at the suspension point on the wires; the lamp hangs below it

A module is built per LOD (`LOD(0)` near: arcs 12, rounds 10, small 6, chamfers and the small details; `LOD(1)` far: arcs 6,
rounds 6, small 4, no chamfers or details) as named parts (`body`, and `lamp` for a glow the runtime sets per draw: flicker,
dead, tier), with the light markers its lamps carry. Nothing here is licensed: every shape is drawn from the bible's words.
"""
import math
import os
import sys

STAGES = os.environ.get('CHIP_ARENA_STAGES', os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
if STAGES not in sys.path:
    sys.path.insert(0, STAGES)

import numpy as np  # noqa: E402

import stagekit as sk  # noqa: E402
from stagekit import Geo  # noqa: E402

F = {}
UP = (0.0, 1.0, 0.0)


# ---------------------------------------------------------------------------------------------------------------------------
# LODs (bible 10.2)
# ---------------------------------------------------------------------------------------------------------------------------
class LOD:
    """A street module's level of detail: 0 near (to 38 m), 1 far (beyond, and the Low graphics tier). Carries stagekit's
    Tier fields (i, cell, bevel, ...) so its primitives take it."""

    def __init__(self, lod):
        self.lod = lod
        self.i = 0 if lod == 0 else 2
        self.name = ('fine', 'low')[lod]
        self.arc = (12, 6)[lod]
        self.round = (10, 6)[lod]
        self.small = (6, 4)[lod]
        self.cell = (.75, 5.0)[lod]
        self.bevel = (1, 0)[lod]
        self.fine = lod == 0
        self.detail = lod == 0
        self.tiny = lod == 0

    def pick(self, near, far):
        return near if self.lod == 0 else far


# ---------------------------------------------------------------------------------------------------------------------------
# Finishes (bible 4.2; the street subset, plus the ground the renders stand the modules on)
# ---------------------------------------------------------------------------------------------------------------------------
def finishes():
    if F:
        return F

    def add(name, hexcol, detail=sk.DETAIL_NONE, **kw):
        F[name] = sk.fin(name, hexcol, detail, **kw)
    # painted and raw metals
    add('iron-green', 0x24463A, smooth=.40, metal=.25, dirt=.8, moss=.10, wear=.35)
    add('iron-black', 0x26262C, smooth=.45, metal=.30, dirt=.7, wear=.30)
    add('iron-raw', 0x4A4E52, sk.DETAIL_IRON, smooth=.35, metal=.7, dirt=1.2, wear=.20)
    add('galv', 0x9AA0A4, smooth=.50, metal=.8, dirt=.6, wear=.10)
    add('alu', 0x7E868C, smooth=.45, metal=.7, dirt=.7, wear=.10)          # a cobra head's cast aluminium (galv's darker kin)
    add('chrome', 0xD8DCE0, smooth=.85, metal=1.0, dirt=.2, wear=.05)
    add('brass', 0xD2A840, sk.DETAIL_BRASS, smooth=.70, metal=1.0, dirt=.3, wear=.30)
    add('paint-maroon', 0x6A2626, smooth=.45, metal=.2, dirt=.8, wear=.30)
    add('paint-cream', 0xE4D9BC, smooth=.45, metal=.1, dirt=.8, wear=.25)
    add('cd-olive', 0x5B6B3A, smooth=.35, metal=.2, dirt=.9, wear=.30)
    add('cd-orange', 0xEE7420, smooth=.40, metal=.1, dirt=.7, wear=.25)
    add('cd-white', 0xECEBE2, smooth=.35, dirt=.6, wear=.1)
    # enamel (chipped to black iron)
    add('enamel-cream', 0xEFE6CC, smooth=.75, dirt=.3, wear=.20)
    add('enamel-green', 0x1F6A4A, smooth=.75, dirt=.3, wear=.20)
    add('enamel-red', 0xB0232A, smooth=.75, dirt=.3, wear=.20)
    add('enamel-cobalt', 0x1E3C96, smooth=.75, dirt=.3, wear=.20)
    add('enamel-teal', 0x1F7F7A, smooth=.75, dirt=.3, wear=.20)
    add('ink', 0x1C1C22, smooth=.4, dirt=.2)                              # printed letters and timetable rules
    # timber, cloth, rubber
    add('timber-old', 0x8A6E52, sk.DETAIL_WOOD, smooth=.15, dirt=1.0, moss=.30, wear=.10, param=0)
    add('timber-old-z', 0x8A6E52, sk.DETAIL_WOOD, smooth=.15, dirt=1.0, moss=.30, wear=.10, param=2)
    add('rag', 0xECEBE2, sk.DETAIL_CLOTH, smooth=.10, dirt=.9)
    add('blanket', 0x7A3A30, sk.DETAIL_CLOTH, smooth=.10, dirt=.8)
    add('blanket-b', 0xC9A86A, sk.DETAIL_CLOTH, smooth=.10, dirt=.8)
    add('tarp', 0x4A5A6E, sk.DETAIL_CLOTH, smooth=.25, dirt=.7)
    add('rubber', 0x1A1A1E, smooth=.30, dirt=.3)
    add('leather', 0x5A3A26, smooth=.35, dirt=.6, wear=.2)
    # glass
    add('glass-dark', 0x1A2230, smooth=.90, dirt=.2)
    add('glass-frosted', 0xB8C8CC, smooth=.30, dirt=.3)
    add('glass-dead', 0x3A3430, smooth=.80, dirt=.4)                      # a dead lamp's dark bowl
    add('glass-crazed', 0xA9B4BA, smooth=.55, dirt=.3)                    # a smashed windscreen
    add('lens', 0xD9DFE2, smooth=.90, dirt=.2)                            # an unlit headlamp
    add('lens-red', 0x6E1418, smooth=.85, dirt=.2)
    add('lens-amber', 0x8A5414, smooth=.85, dirt=.2)
    add('reflector', 0xE8ECEE, smooth=.6, dirt=.3)
    add('void', 0x0A0B0E, smooth=.2, dirt=0)
    # car paints (bible 6.6) and the transit livery
    for k, c in (('blue', 0x8FB1C4), ('cream', 0xE2D6B4), ('maroon', 0x6E2430), ('green', 0x1F4A36), ('mustard', 0xC9A03A),
                 ('white', 0xE6E6E0)):
        add('car-' + k, c, smooth=.80, metal=.30, dirt=.5, wear=.10)
    add('transit-green', 0x2C5A48, smooth=.6, metal=.2, dirt=.6, wear=.2)
    # the ground the renders stand on (bible 4.2)
    add('asphalt', 0x2E3038, smooth=.50, dirt=.5)
    add('puddle', 0x23252C, smooth=.88, dirt=0)
    add('paving', 0x6E6A64, sk.DETAIL_FLAG, smooth=.40, dirt=.9, moss=.20, wear=.20, param=.62)
    add('setts', 0x5A5652, sk.DETAIL_FLAG, smooth=.45, dirt=.9, moss=.30, wear=.20, param=.22)
    add('granite', 0x7A7670, sk.DETAIL_FLAG, smooth=.35, dirt=.8, moss=.10, wear=.30, param=.5)
    add('road-paint', 0xE6E0C8, smooth=.30, dirt=.4, wear=.6)
    add('road-yellow', 0xD8AC32, smooth=.30, dirt=.4, wear=.6)
    add('concrete', 0x8A8882, sk.DETAIL_PLASTER, smooth=.10, dirt=1.4, moss=.20, wear=.10)
    for k, c in (('a', 0xD9B24A), ('b', 0x5F8FA3), ('c', 0xC8553F), ('d', 0xE6DCC2)):
        add('paper-' + k, c, smooth=.2, dirt=.9)
    return F


def glow(hexcol, gain):
    """A glowing finish (the glow sub-mesh): albedo times gain times the draw's _StageGlow."""
    gain = round(max(.2, gain), 2)
    name = 'glow-%06x-%s' % (hexcol & 0xFFFFFF, gain)
    if name not in F:
        F[name] = sk.fin(name, hexcol & 0xFFFFFF, sk.DETAIL_GLOW, glow=gain)
    return F[name]


# the bible's light colours (3.1, 3.2)
SODIUM_LAMP, SODIUM_GLASS = 0xFFA14A, 0xFFC27A
ROUTE_LAMP, ROUTE_GLASS = 0xFF8E3A, 0xFFB866
TUNGSTEN, BRIGHT_ROOM = 0xFFB062, 0xFFD08A
FLOOD = 0xFFD9A8
TROFFER = 0xF2EE9A
POOL_AQUA = 0x52E4E0
TEAL_SIGN = 0x2EC4B6
OPAL = 0xF2E6C6


# ---------------------------------------------------------------------------------------------------------------------------
# Vectors
# ---------------------------------------------------------------------------------------------------------------------------
add, sub, cross, dot, norm, lerp = sk._add, sk._sub, sk._cross, sk._dot, sk._norm, sk._lerp


def scale(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def length(a):
    return math.sqrt(dot(a, a))


def rot_y(p, a):
    """p turned by a radians about +y (Unity: from +z toward +x)."""
    c, s = math.cos(a), math.sin(a)
    return (p[0] * c + p[2] * s, p[1], -p[0] * s + p[2] * c)


def lathe(g, profile, centre, f, sides, axis=(0, 1, 0), smooth=True, cap0=False, cap1=False, phase=0.0, scale=(1, 1)):
    """stagekit's lathe (profile [(radius, height)] about `axis` through `centre`), except that an end of the profile on the
    axis (radius under 2 mm) closes in one triangle a side instead of a quad with a point at its tip."""
    u, v, w = sk.frame((0, 0, 0), axis)
    n = len(profile)
    rings, norms = [], []

    def nrm(i, ang):
        a0 = profile[max(0, i - 1)]
        a1 = profile[min(n - 1, i + 1)]
        dr, dh = a1[0] - a0[0], a1[1] - a0[1]
        cx, cz = math.cos(ang) * scale[0], math.sin(ang) * scale[1]
        radial = norm(add(scale_(u, cx), v, cz))
        return norm(add(scale_(radial, dh), w, -dr))
    for i, (r, h) in enumerate(profile):
        ring, nr = [], []
        for k in range(sides):
            ang = 2 * math.pi * k / sides + phase
            cx, cz = math.cos(ang) * scale[0], math.sin(ang) * scale[1]
            ring.append(add(add(add(centre, w, h), u, r * cx), v, r * cz))
            nr.append(nrm(i, ang))
        rings.append(ring)
        norms.append(nr)
    tip = [r < 2e-3 for (r, h) in profile]
    for i in range(n - 1):
        if tip[i] and tip[i + 1]:
            continue
        for k in range(sides):
            k1 = (k + 1) % sides
            mid = 2 * math.pi * (k + .5) / sides + phase
            if tip[i + 1]:
                pts = (rings[i][k], rings[i][k1], rings[i + 1][k])
                nn = (norms[i][k], norms[i][k1], nrm(i + 1, mid))
            elif tip[i]:
                pts = (rings[i][k], rings[i + 1][k1], rings[i + 1][k])
                nn = (nrm(i, mid), norms[i + 1][k1], norms[i + 1][k])
            else:
                pts = (rings[i][k], rings[i][k1], rings[i + 1][k1], rings[i + 1][k])
                nn = (norms[i][k], norms[i][k1], norms[i + 1][k1], norms[i + 1][k])
            if smooth:
                g.face(pts, f, normals=nn)
            else:
                s_ = (0.0, 0.0, 0.0)
                for x in nn:
                    s_ = add(s_, x)
                g.face(pts, f, normal=norm(s_))
    if cap0 and not tip[0]:
        g.face(rings[0][::-1], f, normal=(-w[0], -w[1], -w[2]))
    if cap1 and not tip[-1]:
        g.face(rings[-1], f, normal=w)


def scale_(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def spike(g, base, r, h, sides, f, axis=(0, 1, 0), phase=0.0):
    """A pointed cone (a spear's head, a finial's spike): one triangle a side, its base open."""
    lathe(g, [(r, 0.0), (0.0, h)], base, f, sides, axis=axis, smooth=False, phase=phase)


def place(g, yaw=0.0, at=(0.0, 0.0, 0.0)):
    """A copy of g turned by yaw about +y and moved to `at`."""
    q = sk.quat_axis(0, 1, 0, yaw) if yaw else (0.0, 0.0, 0.0, 1.0)
    return g.transformed(sk.affine(q, at))


# ---------------------------------------------------------------------------------------------------------------------------
# Face frames
# ---------------------------------------------------------------------------------------------------------------------------
class Fr:
    """A face's frame: P(a, b, c) = o + a r + b v + c n, with n the face's outward normal, v up the face and r the reader's
    right seen from outside (Unity is left handed: r = n x v numerically). Lettering laid along +a reads left to right."""

    def __init__(self, o, n, v=UP):
        self.o = tuple(float(x) for x in o)
        self.n = norm(n)
        vv = sub(v, scale(self.n, dot(v, self.n)))
        self.v = norm(vv)
        self.r = norm(cross(self.n, self.v))

    def P(self, a, b, c=0.0):
        o, r, v, n = self.o, self.r, self.v, self.n
        return (o[0] + a * r[0] + b * v[0] + c * n[0], o[1] + a * r[1] + b * v[1] + c * n[1], o[2] + a * r[2] + b * v[2] + c * n[2])

    def D(self, a, b, c):
        r, v, n = self.r, self.v, self.n
        return (a * r[0] + b * v[0] + c * n[0], a * r[1] + b * v[1] + c * n[1], a * r[2] + b * v[2] + c * n[2])

    def rows(self):
        """The 3x4 affine of local (x, y, z) -> o - x r + y v + z n (det +1: x runs to the reader's left)."""
        l, v, n, o = scale(self.r, -1), self.v, self.n, self.o
        return [l[0], v[0], n[0], o[0], l[1], v[1], n[1], o[1], l[2], v[2], n[2], o[2]]

    def at(self, a, b, c=0.0):
        return Fr(self.P(a, b, c), self.n, self.v)


def front(o=(0, 0, 0), yaw=0.0):
    """The frame of a face looking toward +z turned by yaw (its reader stands at +z)."""
    return Fr(o, rot_y((0, 0, 1), yaw))


def fbox(g, T, fr, a0, a1, b0, b1, c0, c1, f, bevel=.012, skip=()):
    """A box in a frame (a across to the reader's right, b up, c out). skip: '-z' its back (c0), '-y' its bottom (b0),
    '+y' its top, '+z' its front."""
    t = Geo()
    if T is not None and T.lod > 0:
        bevel = 0.0
    sk.box(t, -a1, -a0, b0, b1, c0, c1, f, T if bevel else None, bevel, cell=0, skip=skip)
    g.extend(t.transformed(fr.rows()))


def quad(g, fr, a0, a1, b0, b1, c, f):
    g.face((fr.P(a0, b0, c), fr.P(a1, b0, c), fr.P(a1, b1, c), fr.P(a0, b1, c)), f, fr.n)


def poly(g, fr, pts, c, f):
    """A convex polygon [(a, b)] on the frame's plane at depth c."""
    g.face([fr.P(a, b, c) for (a, b) in pts], f, fr.n)


def ngon_pts(r, n, phase=0.0, cx=0.0, cy=0.0, sx=1.0, sy=1.0):
    return [(cx + r * sx * math.cos(phase + 2 * math.pi * i / n), cy + r * sy * math.sin(phase + 2 * math.pi * i / n)) for i in range(n)]


# ---------------------------------------------------------------------------------------------------------------------------
# Shapes
# ---------------------------------------------------------------------------------------------------------------------------
def box(g, T, x0, x1, y0, y1, z0, z1, f, bevel=.012, skip=()):
    """An axis box in module space, chamfered at LOD0, never subdivided."""
    if T is not None and T.lod > 0:
        bevel = 0.0
    sk.box(g, x0, x1, y0, y1, z0, z1, f, T if bevel else None, bevel, cell=0, skip=skip)


def rod(g, a, b, r, f, sides, caps=True, r1=None):
    if dot(sub(b, a), sub(b, a)) < 1e-10:
        return
    sk.cylinder(g, a, b, r, f, sides, caps=caps, r1=r1)


def tube(g, path, r, f, sides, caps=True, radii=None, up=None, smooth=True):
    """A round section swept along a path (radii: per point, scales the section)."""
    if len(path) < 2:
        return
    sc = None if radii is None else [x / r for x in radii]
    sweep(g, sk.circle_section(r, sides, math.pi / sides), path, f, caps=caps, scale=sc, up=up, smooth_u=smooth)


def bar(g, path, w, h, f, caps=True, up=None):
    """A rectangular section (w across the frame's u, h along its v) swept along a path: flat bar, cast ribs."""
    if len(path) < 2:
        return
    sweep(g, sk.rect_section(w, h), path, f, caps=caps, up=up, smooth_u=False)


def disc(g, c, r, f, sides, normal, phase=0.0):
    u, v, w = sk.frame((0, 0, 0), normal)
    g.face([add(add(c, u, r * math.cos(phase + 2 * math.pi * i / sides)), v, r * math.sin(phase + 2 * math.pi * i / sides))
            for i in range(sides)], f, normal)


def annulus(g, c, r0, r1, f, sides, normal, a0=0.0, a1=2 * math.pi, axes=None):
    """A flat ring facing `normal` from angle a0 to a1 (axes: the (u, v) its angles are measured in)."""
    u, v = axes if axes is not None else sk.frame((0, 0, 0), normal)[:2]
    n = max(1, int(math.ceil(sides * (a1 - a0) / (2 * math.pi))))
    for i in range(n):
        t0, t1 = a0 + (a1 - a0) * i / n, a0 + (a1 - a0) * (i + 1) / n
        pa = add(add(c, u, r0 * math.cos(t0)), v, r0 * math.sin(t0))
        pb = add(add(c, u, r1 * math.cos(t0)), v, r1 * math.sin(t0))
        pc = add(add(c, u, r1 * math.cos(t1)), v, r1 * math.sin(t1))
        pd = add(add(c, u, r0 * math.cos(t1)), v, r0 * math.sin(t1))
        g.face((pa, pb, pc, pd), f, normal)


def prism(g, pts, y0, y1, f, top=True, bottom=False, ftop=None, smooth=False, taper=1.0):
    """A vertical prism over the convex outline pts [(x, z)] from y0 to y1 (taper scales the top outline about its centre)."""
    n = len(pts)
    cx = sum(p[0] for p in pts) / n
    cz = sum(p[1] for p in pts) / n
    tp = [(cx + (p[0] - cx) * taper, cz + (p[1] - cz) * taper) for p in pts]
    for i in range(n):
        p, q = pts[i], pts[(i + 1) % n]
        pt, qt = tp[i], tp[(i + 1) % n]
        mid = ((p[0] + q[0]) / 2 - cx, 0.0, (p[1] + q[1]) / 2 - cz)
        g.face(((p[0], y0, p[1]), (q[0], y0, q[1]), (qt[0], y1, qt[1]), (pt[0], y1, pt[1])), f, mid)
    if top:
        g.face([(p[0], y1, p[1]) for p in tp], f if ftop is None else ftop, (0, 1, 0))
    if bottom:
        g.face([(p[0], y0, p[1]) for p in pts], f, (0, -1, 0))


def loft(g, rings, f, close0=False, close1=False, closed=True, fins=None, smooth=True):
    """Faces between rings of points (each ring the same count), normals from the grid (smooth) or per face.
    fins: a function (i, j) -> finish for the face between ring i and i+1, points j and j+1."""
    m, k = len(rings), len(rings[0])
    R = np.array(rings, dtype=np.float64)
    if closed:
        du = np.roll(R, -1, axis=1) - np.roll(R, 1, axis=1)
    else:
        du = np.empty_like(R)
        du[:, 1:-1] = R[:, 2:] - R[:, :-2]
        du[:, 0] = R[:, 1] - R[:, 0]
        du[:, -1] = R[:, -1] - R[:, -2]
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
    jn = k if closed else k - 1
    for i in range(m - 1):
        for j in range(jn):
            j1 = (j + 1) % k
            ff = f if fins is None else fins(i, j)
            pts = (tuple(R[i, j]), tuple(R[i, j1]), tuple(R[i + 1, j1]), tuple(R[i + 1, j]))
            nn = (tuple(N[i, j]), tuple(N[i, j1]), tuple(N[i + 1, j1]), tuple(N[i + 1, j]))
            if smooth:
                g.face(pts, ff, normals=nn)
            else:
                avg = tuple(sum(x[q] for x in nn) for q in range(3))
                g.face(pts, ff, normal=avg)
    for (i, on, nb) in ((0, close0, 1), (m - 1, close1, m - 2)):
        if not on:
            continue
        c = tuple(ctr[i, 0])
        axis = norm(tuple(ctr[i, 0] - ctr[nb, 0]))
        g.face([tuple(p) for p in R[i]], f, axis)


def ellipsoid(g, c, axes, radii, f, sides, rings, top=1.0, bottom=-1.0, smooth=True):
    """Part of an ellipsoid between latitudes sin(top) .. sin(bottom) (1 the pole along +w): axes (u, v, w) unit vectors,
    radii (ru, rv, rw)."""
    u, v, w = axes
    ru, rv, rw = radii
    lat0, lat1 = math.asin(max(-1, min(1, bottom))), math.asin(max(-1, min(1, top)))
    ringsP, ringsN = [], []
    for i in range(rings + 1):
        la = lat0 + (lat1 - lat0) * i / rings
        cl, sl = math.cos(la), math.sin(la)
        rp, rn = [], []
        for j in range(sides):
            a = 2 * math.pi * j / sides
            ca, sa = math.cos(a), math.sin(a)
            p = add(add(add(c, u, ru * cl * ca), v, rv * cl * sa), w, rw * sl)
            nn = norm(add(add(scale(u, cl * ca / ru), v, cl * sa / rv), w, sl / rw))
            rp.append(p)
            rn.append(nn)
        ringsP.append(rp)
        ringsN.append(rn)
    for i in range(rings):
        for j in range(sides):
            j1 = (j + 1) % sides
            pts = (ringsP[i][j], ringsP[i][j1], ringsP[i + 1][j1], ringsP[i + 1][j])
            nn = (ringsN[i][j], ringsN[i][j1], ringsN[i + 1][j1], ringsN[i + 1][j])
            if abs(math.cos(lat0 + (lat1 - lat0) * (i + 1) / rings)) < 1e-6:
                tri = (pts[0], pts[1], pts[2])
                g.face(tri, f, normals=nn[:3] if smooth else None, normal=None if smooth else nn[0])
            elif abs(math.cos(lat0 + (lat1 - lat0) * i / rings)) < 1e-6:
                g.face((pts[0], pts[2], pts[3]), f, normals=(nn[0], nn[2], nn[3]) if smooth else None)
            elif smooth:
                g.face(pts, f, normals=nn)
            else:
                g.face(pts, f, normal=norm(add(add(nn[0], nn[1]), add(nn[2], nn[3]))))
    return ringsP


def sweep(g, section, path, f, closed=False, up=None, caps=True, smooth_u=True, scale=None):
    """A section [(x, y)] swept along a path (as stagekit.sweep: rotation-minimising frames from `up`, `scale` per point), its
    faces turned OUT of the section whichever way the section winds. (stagekit.sweep turns them inward: a tube from it is
    inside out under back-face culling; measured 2026-11-18, every face of a circle or rect section.)"""
    m = len(path)
    if m < 2:
        return
    T = []
    for i in range(m):
        a = path[(i - 1) % m] if (closed or i > 0) else path[i]
        b = path[(i + 1) % m] if (closed or i < m - 1) else path[i]
        T.append(norm(sub(b, a)))
    ref = up if up is not None else ((0, 1, 0) if abs(T[0][1]) < .95 else (1, 0, 0))
    u = norm(cross(ref, T[0]))
    v = cross(T[0], u)
    frames = [(u, v)]
    for i in range(1, m):
        v1 = sub(path[i], path[i - 1])
        c1 = dot(v1, v1)
        if c1 < 1e-14:
            frames.append(frames[-1])
            continue
        uL = add(u, v1, -2.0 / c1 * dot(v1, u))
        tL = add(T[i - 1], v1, -2.0 / c1 * dot(v1, T[i - 1]))
        v2 = sub(T[i], tL)
        c2 = dot(v2, v2)
        u = norm(add(uL, v2, -2.0 / c2 * dot(v2, uL)) if c2 > 1e-14 else uL)
        v = cross(T[i], u)
        frames.append((u, v))
    k = len(section)
    area = sum(section[j][0] * section[(j + 1) % k][1] - section[(j + 1) % k][0] * section[j][1] for j in range(k))
    sgn = 1.0 if area > 0 else -1.0          # (dy, -dx) of a counter-clockwise outline points out of it
    sn = []
    for j in range(k):
        a, b = section[(j - 1) % k], section[(j + 1) % k]
        d = (b[0] - a[0], b[1] - a[1])
        ln = math.hypot(d[0], d[1]) or 1.0
        sn.append((sgn * d[1] / ln, -sgn * d[0] / ln))
    rings, nrings = [], []
    for i in range(m):
        u, v = frames[i]
        s_ = scale[i] if scale is not None else 1.0
        rings.append([add(add(path[i], u, x * s_), v, y * s_) for (x, y) in section])
        nrings.append([norm(add(scale_(u, nx), v, ny)) for (nx, ny) in sn])
    count = m if closed else m - 1
    for i in range(count):
        i1 = (i + 1) % m
        for j in range(k):
            j1 = (j + 1) % k
            pts = (rings[i][j], rings[i][j1], rings[i1][j1], rings[i1][j])
            nn = (nrings[i][j], nrings[i][j1], nrings[i1][j1], nrings[i1][j])
            if smooth_u:
                g.face(pts, f, normals=nn)
            else:
                # the side's own outward normal: the section's edge normal between its two corners
                ex, ey = section[j1][0] - section[j][0], section[j1][1] - section[j][1]
                el = math.hypot(ex, ey) or 1.0
                nx, ny = sgn * ey / el, -sgn * ex / el
                u0, v0 = frames[i]
                u1, v1_ = frames[i1]
                g.face(pts, f, normal=norm(add(add(scale_(u0, nx), v0, ny), add(scale_(u1, nx), v1_, ny))))
    if caps and not closed:
        g.face(rings[0], f, normal=scale_(T[0], -1.0))
        g.face(rings[-1], f, normal=T[-1])


def catmull(pts, per):
    return sk.catmull(pts, per=per)


def arc_pts(c, r, a0, a1, n, plane=('z', 'y')):
    """Points on a circular arc in a coordinate plane of module space: angle 0 along the plane's first axis."""
    ia = {'x': 0, 'y': 1, 'z': 2}
    out = []
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        p = list(c)
        p[ia[plane[0]]] += r * math.cos(a)
        p[ia[plane[1]]] += r * math.sin(a)
        out.append(tuple(p))
    return out


def catenary(a, b, sag, n):
    """n + 1 points on a hanging wire from a to b, `sag` metres at mid span (a parabola: close enough for a span wire)."""
    out = []
    for i in range(n + 1):
        t = i / n
        p = lerp(a, b, t)
        out.append((p[0], p[1] - 4 * sag * t * (1 - t), p[2]))
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# Lettering: one 5 x 7 stroke font (bible 4.3 rule 6; 10.5 the glyph kit), drawn as merged rectangles
# ---------------------------------------------------------------------------------------------------------------------------
GLYPHS = {
    'A': '01110,10001,10001,11111,10001,10001,10001', 'B': '11110,10001,10001,11110,10001,10001,11110',
    'C': '01110,10001,10000,10000,10000,10001,01110', 'D': '11100,10010,10001,10001,10001,10010,11100',
    'E': '11111,10000,10000,11110,10000,10000,11111', 'F': '11111,10000,10000,11110,10000,10000,10000',
    'G': '01110,10001,10000,10111,10001,10001,01111', 'H': '10001,10001,10001,11111,10001,10001,10001',
    'I': '111,010,010,010,010,010,111', 'J': '00111,00010,00010,00010,00010,10010,01100',
    'K': '10001,10010,10100,11000,10100,10010,10001', 'L': '10000,10000,10000,10000,10000,10000,11111',
    'M': '10001,11011,10101,10101,10001,10001,10001', 'N': '10001,10001,11001,10101,10011,10001,10001',
    'O': '01110,10001,10001,10001,10001,10001,01110', 'P': '11110,10001,10001,11110,10000,10000,10000',
    'Q': '01110,10001,10001,10001,10101,10010,01101', 'R': '11110,10001,10001,11110,10100,10010,10001',
    'S': '01111,10000,10000,01110,00001,00001,11110', 'T': '11111,00100,00100,00100,00100,00100,00100',
    'U': '10001,10001,10001,10001,10001,10001,01110', 'V': '10001,10001,10001,10001,10001,01010,00100',
    'W': '10001,10001,10001,10101,10101,10101,01010', 'X': '10001,10001,01010,00100,01010,10001,10001',
    'Y': '10001,10001,01010,00100,00100,00100,00100', 'Z': '11111,00001,00010,00100,01000,10000,11111',
    '0': '01110,10001,10011,10101,11001,10001,01110', '1': '010,110,010,010,010,010,111',
    '2': '01110,10001,00001,00010,00100,01000,11111', '3': '11111,00010,00100,00010,00001,10001,01110',
    '4': '00010,00110,01010,10010,11111,00010,00010', '5': '11111,10000,11110,00001,00001,10001,01110',
    '6': '00110,01000,10000,11110,10001,10001,01110', '7': '11111,00001,00010,00100,01000,01000,01000',
    '8': '01110,10001,10001,01110,10001,10001,01110', '9': '01110,10001,10001,01111,00001,00010,01100',
    ':': '0,1,0,0,0,1,0', '.': '0,0,0,0,0,0,1', '-': '000,000,000,111,000,000,000', '/': '001,001,010,010,010,100,100',
    "'": '1,1,0,0,0,0,0', '&': '01100,10010,10100,01000,10101,10010,01101', ' ': '00,00,00,00,00,00,00',
    '*': '0,0,0,1,0,0,0',  # the middle dot of BUS STOP * 647
    '>': '1000,0100,0010,0001,0010,0100,1000',
}
_RECTS = {}


def glyph_rects(ch):
    """A glyph's strokes as merged rectangles (col0, col1, row0, row1), rows counted from the top."""
    if ch in _RECTS:
        return _RECTS[ch]
    rows = GLYPHS.get(ch, GLYPHS[' ']).split(',')
    H, W = len(rows), len(rows[0])
    used = [[False] * W for _ in range(H)]
    out = []
    for r in range(H):
        for c in range(W):
            if rows[r][c] != '1' or used[r][c]:
                continue
            c1 = c
            while c1 < W and rows[r][c1] == '1' and not used[r][c1]:
                c1 += 1
            r1 = r + 1
            while r1 < H and all(rows[r1][x] == '1' and not used[r1][x] for x in range(c, c1)):
                r1 += 1
            for rr in range(r, r1):
                for cc in range(c, c1):
                    used[rr][cc] = True
            out.append((c, c1, r, r1))
    _RECTS[ch] = (W, out)
    return _RECTS[ch]


def text_width(s, px):
    return sum((glyph_rects(ch)[0] + 1) * px for ch in s) - px


def text(g, fr, a, b, px, s, f, c=0.0, raised=0.0, align='centre', T=None):
    """Lettering on a frame's face: strokes `px` metres a cell, its foot at height b, at depth c (raised: boxes that deep,
    else flat faces). align: 'centre' (a is its middle), 'left' or 'right'. Returns its (a0, a1)."""
    w = text_width(s, px)
    x = a - w / 2 if align == 'centre' else (a if align == 'left' else a - w)
    x0 = x
    for ch in s:
        W, rects = glyph_rects(ch)
        for (c0, c1, r0, r1) in rects:
            A0, A1 = x + c0 * px, x + c1 * px
            B0, B1 = b + (7 - r1) * px, b + (7 - r0) * px
            if raised > 0:
                fbox(g, T, fr, A0, A1, B0, B1, c, c + raised, f, 0.0, skip=('-z',))
            else:
                quad(g, fr, A0, A1, B0, B1, c, f)
        x += (W + 1) * px
    return x0, x0 + w


def text_on_drum(g, centre, radius, y, px, s, f, mid_angle=0.0, lift=.004):
    """Lettering round a vertical drum (a bin's band): each glyph on its tangent plane, centred on mid_angle (0: +z; the
    angle grows toward the reader's right, which is -x seen from +z)."""
    w = text_width(s, px)
    a = -w / 2
    for ch in s:
        W, rects = glyph_rects(ch)
        gw = W * px
        ang = mid_angle - (a + gw / 2) / radius          # the reader's right is clockwise seen from above (toward -x)
        n = (math.sin(ang), 0.0, math.cos(ang))
        fr = Fr(add(centre, (n[0] * radius, y, n[2] * radius)), n)
        for (c0, c1, r0, r1) in rects:
            quad(g, fr, -gw / 2 + c0 * px, -gw / 2 + c1 * px, (7 - r1) * px, (7 - r0) * px, lift, f)
        a += (W + 1) * px


# ---------------------------------------------------------------------------------------------------------------------------
# Meridian's marks (bible 1.7 rule 4): the 6:47 clock, the city crest, the transit mark
# ---------------------------------------------------------------------------------------------------------------------------
MINUTE_647 = math.radians(282.0)   # from twelve, clockwise as read
HOUR_647 = math.radians(203.5)


def hand(g, fr, a, b, c, angle, L, w, f, tail=0.0):
    """A clock hand on a face: from the centre (a, b) toward `angle` (clockwise from twelve as the reader sees it)."""
    dx, dy = math.sin(angle), math.cos(angle)
    px, py = -dy, dx
    p0 = (a - dx * tail, b - dy * tail)
    p1 = (a + dx * L, b + dy * L)
    pts = [(p0[0] + px * w / 2, p0[1] + py * w / 2), (p1[0] + px * w * .25, p1[1] + py * w * .25),
           (p1[0] - px * w * .25, p1[1] - py * w * .25), (p0[0] - px * w / 2, p0[1] - py * w / 2)]
    poly(g, fr, pts, c, f)


def clock_face(g, T, fr, a, b, r, c, f_face, f_rim, f_hands, ticks=True):
    """A 6:47 dial on a face: its disc, a rim, twelve ticks and the hands (minute at 282 deg, hour at 203.5 deg)."""
    sides = T.pick(16, 8)
    poly(g, fr, ngon_pts(r, sides, 0.0, a, b), c, f_face)
    if T.lod == 0:
        rim = Geo()
        annulus(rim, fr.P(a, b, c + .004), r * .9, r, f_rim, sides, fr.n, axes=(fr.r, fr.v))
        g.extend(rim)
        if ticks:
            for k in range(12):
                ang = 2 * math.pi * k / 12
                L = r * (.16 if k % 3 == 0 else .09)
                hand(g, fr, a + math.sin(ang) * (r * .82 - L), b + math.cos(ang) * (r * .82 - L), c + .004, ang, L, r * .05, f_hands)
    hand(g, fr, a, b, c + .006, HOUR_647, r * .52, r * .1, f_hands, tail=r * .1)
    hand(g, fr, a, b, c + .008, MINUTE_647, r * .78, r * .07, f_hands, tail=r * .12)


def crest(g, T, fr, a, b, r, c, f_disc, f_rim, f_mark, f_hills, sides=None):
    """The city crest: a clock face at 6:47 over three stepped hills, in a medallion of radius r. Layers stand 6 mm apart."""
    sides = sides or T.pick(12, 6)
    poly(g, fr, ngon_pts(r, sides, 0.0, a, b), c, f_disc)
    if T.lod > 0:
        return
    annulus(g, fr.P(a, b, c + .006), r * .84, r, f_rim, sides, fr.n, axes=(fr.r, fr.v))
    # three stepped hills along the foot of the disc (side by side, none over another)
    for (x0, x1, top) in ((-.55, -.18, -.12), (-.18, .18, .0), (.18, .55, -.25)):
        quad(g, fr, a + x0 * r, a + x1 * r, b - .55 * r, b + top * r, c + .006, f_hills)
    # the clock over them, at 6:47
    cy = b + .3 * r
    poly(g, fr, ngon_pts(r * .3, 8, 0.0, a, cy), c + .012, f_rim)
    hand(g, fr, a, cy, c + .018, HOUR_647, r * .17, r * .07, f_mark)
    hand(g, fr, a, cy, c + .024, MINUTE_647, r * .26, r * .05, f_mark)


def slat(g, a, b, w, h, f, chamfer=.008, up=None, T=None):
    """A chamfered rectangular member from a to b (w across, h up): eight sides at LOD0, four at LOD1, capped."""
    sec = sk.rect_section(w, h, chamfer if (T is None or T.lod == 0) else 0.0)
    sweep(g, sec, [a, b], f, caps=True, up=up, smooth_u=False)


def transit_mark(g, T, fr, a, b, r, c, f_ring, f_hand):
    """MERIDIAN TRANSIT: a ring broken by one clock hand at the 47th minute (never a bar across a circle)."""
    gap = math.radians(16)
    # the reader's clockwise angle from twelve -> the frame's (r, v) polar angle: theta = 90 deg - angle
    th = math.pi / 2 - MINUTE_647
    sides = T.pick(20, 10)
    annulus(g, fr.P(a, b, c), r * .78, r, f_ring, sides, fr.n, th + gap, th + 2 * math.pi - gap, axes=(fr.r, fr.v))
    hand(g, fr, a, b, c + .002, MINUTE_647, r * 1.08, r * .16, f_hand, tail=r * .05)


# ---------------------------------------------------------------------------------------------------------------------------
# Modules
# ---------------------------------------------------------------------------------------------------------------------------
class Light:
    """A light marker in the module's frame: where the light field's pool (or a practical slot) sits for this lamp."""

    def __init__(self, pos, colour, radius, kind='pool', circuit=0, gain=1.0, direction=None, part='lamp', note=''):
        self.pos, self.colour, self.radius, self.kind = tuple(pos), colour, radius, kind
        self.circuit, self.gain, self.direction, self.part, self.note = circuit, gain, direction, part, note

    def json(self, at=(0.0, 0.0, 0.0)):
        d = {'pos': [round(self.pos[i] + at[i], 4) for i in range(3)], 'colour': '#%06X' % self.colour, 'radius': self.radius,
             'kind': self.kind, 'circuit': self.circuit, 'gain': self.gain, 'part': self.part}
        if self.direction is not None:
            d['direction'] = [round(x, 4) for x in norm(self.direction)]
        if self.note:
            d['note'] = self.note
        return d


class Module:
    """One street module at one LOD: named parts (Geo), its light markers and how it mounts."""

    def __init__(self, name, lod, mount='ground', rest=(0.0, 0.0, 0.0), block='', cap=None, note=''):
        self.name, self.lod, self.mount, self.rest, self.block, self.cap, self.note = name, lod, mount, rest, block, cap, note
        self.parts = {}
        self.lights = []

    def part(self, key):
        if key not in self.parts:
            self.parts[key] = Geo()
        return self.parts[key]

    def light(self, *a, **kw):
        self.lights.append(Light(*a, **kw))


def tris(g):
    _, _, S, _ = g.arrays()
    return int((S - 2).sum()) if len(S) else 0
