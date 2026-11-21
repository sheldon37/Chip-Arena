"""Hold the Line's set (the shield; docs/INTROS-MELEE.md): a broken fortress gate at dusk, an immovable wall taking a barrage.

The pilot stands in the mouth of a breach in a thick limestone curtain wall: the wall runs along x centred at z -1.8 (2.5 m
thick, faces at z -0.55 and -3.05), 7.15 m to its merlons' tops (the walk at 5.6, a corbelled parapet with arrow-slit merlons, a
battered foot, stepped buttresses, timber hoardings over the pilot's left, burnt at their end); the breach is about 4.5 m wide
at the ground (x -2.25..2.25), widening raggedly to 6.4-7.2 m at the walk, its edges broken course by course (the ashlar facings
of both faces, out of true, cracked, some fallen, and the rubble core between them in a ragged face), scree heaps and fallen
blocks against both sides (the middle left clear, a few broken paving stones lying flat), two braziers of glowing coals in it
behind the pilot. On the pilot's right (x -10.4..-5.2) the gatehouse tower stands proud of the wall, its pointed gate arch
gaping, a burnt door leaf hanging, the portcullis fallen out on the road. In front (+z) the trampled earth and broken cobbles
of the approach run level for a few metres, then the field falls away past scorched craters (fires still burning in three),
arrows stuck in the ground, sharpened stakes and plank barricades, dropped shields and spears, a broken cart, a burnt siege
tower, the ram's abandoned shed, a scaling ladder against the wall and another in the field, trebuchet stones, to a valley and
a far ridge (z 56-96) where three trebuchets stand against the dusk. Through the breach: the cobbled ward, the smithy's forge
glow, the hall's lit windows, the well, the keep far back on the left and the ward's far curtain with its inner gate tower.
Torn banners hang from poles on the wall and the gatehouse and down the wall's face.

Rigs: four loose stone chunks `debris-0..3` (rocks 0.2-0.3 m, each in its own frame centred on it; rest translations near the
pilot's front at about (+-1.2, 0.1, 1.5..2.5)); the intro throws them. Set frame: melee_kit's (the pilot at the origin facing
+z; -x is the opening camera's right)."""
import math
import os

import numpy as np

import melee_kit as mk
from melee_kit import sk

# ---------------------------------------------------------------------------------------------------------------------------
# Finishes
# ---------------------------------------------------------------------------------------------------------------------------
F_ASHLAR = mk.fin('breach.ashlar', 0x7c7468, sk.DETAIL_ASHLAR, smooth=.3, dirt=1.0, moss=.2, wear=.35)
F_ASHLAR_DK = mk.fin('breach.ashlar.dark', 0x655e54, sk.DETAIL_ASHLAR, smooth=.3, dirt=1.1, moss=.25, wear=.3)
F_BLOCK = mk.fin('breach.block', 0x776f62, sk.DETAIL_CARVED, smooth=.3, dirt=1.0, moss=.15, wear=.45)
F_BLOCK_LT = mk.fin('breach.block.fresh', 0x887f6f, sk.DETAIL_CARVED, smooth=.3, dirt=.8, moss=.05, wear=.45)
F_CORE = mk.fin('breach.core', 0x5f5950, sk.DETAIL_CARVED, smooth=.2, dirt=1.0, moss=.1)
F_WALK = mk.fin('breach.walk', 0x7a7367, sk.DETAIL_FLAG, smooth=.35, dirt=1.0, moss=.3)
F_SLIT = mk.fin('breach.slit', 0x2b2927, smooth=.2, dirt=.2)
F_EARTH = mk.fin('breach.earth', 0x5b4d3f, smooth=.15, dirt=1.0, moss=.25)
F_SCORCH = mk.fin('breach.scorch', 0x342d28, smooth=.15, dirt=.5)
F_COBBLE = mk.fin('breach.cobble', 0x6b6458, sk.DETAIL_FLAG, smooth=.35, dirt=1.0, moss=.3)
F_TIMBER = mk.fin('breach.timber', 0x5c4634, sk.DETAIL_WOOD, smooth=.3, dirt=.9, param=1, wear=.3)
F_TIMBER_X = mk.fin('breach.timber.x', 0x5c4634, sk.DETAIL_WOOD, smooth=.3, dirt=.9, param=0, wear=.3)
F_TIMBER_Z = mk.fin('breach.timber.z', 0x5c4634, sk.DETAIL_WOOD, smooth=.3, dirt=.9, param=2, wear=.3)
F_PLANK = mk.fin('breach.plank', 0x6d5741, sk.DETAIL_WOOD, smooth=.3, dirt=.9, param=1, wear=.3)
F_CHARRED = mk.fin('breach.charred', 0x302925, sk.DETAIL_WOOD, smooth=.2, dirt=.4, param=1)
F_IRON = mk.fin('breach.iron', 0x3a3735, sk.DETAIL_IRON, smooth=.4, metal=.6, dirt=.7)
F_BANNER = mk.fin('breach.banner', 0x7d2a22, sk.DETAIL_CLOTH, smooth=.15, dirt=.6)
F_BANNER2 = mk.fin('breach.banner.blue', 0x2e3c5c, sk.DETAIL_CLOTH, smooth=.15, dirt=.6)
F_COALS = mk.fin('breach.coals.glow', 0xff7a2c, glow=2.6)
F_WINDOW = mk.fin('breach.window.glow', 0xffb85c, glow=1.5)
F_SHAFT = mk.fin('breach.shaft', 0x7a6248, sk.DETAIL_WOOD, smooth=.3, dirt=.5, param=1)
F_FLETCH = mk.fin('breach.fletch', 0xa49c8c, smooth=.2, dirt=.4)
F_ROPE = mk.fin('breach.rope', 0x7a6a50, sk.DETAIL_CLOTH, smooth=.15, dirt=.6)
F_ROOF = mk.fin('breach.roof', 0x3e3f44, smooth=.4, dirt=.8, moss=.3)
F_FAR = mk.fin('breach.far', 0x29252e, smooth=.1, dirt=0)
F_FAR2 = mk.fin('breach.far.near', 0x332f38, smooth=.1, dirt=0)
F_ENGINE = mk.fin('breach.engine', 0x221f24, smooth=.1, dirt=0)

# ---------------------------------------------------------------------------------------------------------------------------
# The plan (set frame, metres)
# ---------------------------------------------------------------------------------------------------------------------------
ZF, ZB = -.55, -3.05          # the curtain wall's faces: front (toward the barrage, +z) and back (the ward)
WALK_Y, PARA_Y, MERLON_Y = 5.6, 6.3, 7.15
CORBEL_Y = 5.15               # the corbel table under the parapet
BATTER = .4                   # the battered foot stands out this far at the ground, up to 1.3 m
EDGE_X = 5.2                  # the wall's courses are laid block by block between the breach and this far out
BREACH = {1: (2.25, 3.6), -1: (2.25, 3.2)}   # the breach's half width at the foot and at the walk, each side
GH_X = (-10.4, -5.2)          # the gatehouse tower
GH_Z = 1.25                   # its front
GH_Y = 10.4                   # its walk
GATE_HW, GATE_SPRING, GATE_APEX = 1.3, 3.0, 4.6
WALL_END = 26.0
CRATERS = ((4.6, 4.3, 1.3), (-5.6, 7.6, 1.6), (1.6, 11.8, 1.9), (-10.0, 13.5, 1.4), (8.8, 15.5, 2.0), (-2.2, 18.5, 1.7))
SCORCH = ((3.6, 2.6, 1.0), (-3.6, 3.8, 1.2), (7.0, 7.5, 1.4), (-8.0, 4.0, 1.1), (.4, 6.2, .9), (5.4, -6.5, 1.5))
DEBRIS = (((-1.2, .1, 1.55), (.15, .1, .12), 61), ((1.25, .09, 1.85), (.12, .09, .14), 62), ((-1.15, .1, 2.45), (.13, .1, .11), 63),
          ((1.2, .1, 2.3), (.14, .1, .13), 64))
BRAZIERS = ((1.75, -2.05), (-1.8, -2.0))
CART = (7.6, 10.4)
KEEP = (8.5, -30.0)
HALL = (-7.6, -13.0)
SMITHY = (5.6, -11.2)
WELL = (2.4, -8.2)
HOARD_X = (6.6, 15.4)
FIRES = ((1.6, 11.8), (-5.6, 7.6), (8.8, 15.5))
BUTTRESS_X = (11.6, 19.4, -17.6, -23.0)


def mul(*ms):
    """The product of 3x4 affines (the last one applies first)."""
    a = np.eye(4)
    for m in ms:
        b = np.eye(4)
        b[:3, :] = np.array(m, dtype=np.float64).reshape(3, 4)
        a = a @ b
    return [float(v) for v in a[:3, :].reshape(-1)]


def rot(ax, ay, az, ang, at=(0.0, 0.0, 0.0)):
    return mk.about(at, sk.quat_axis(ax, ay, az, ang))


def _quad(g, pts, f, want):
    """A face whose shading normal is its own (Newell's), turned to agree with `want`."""
    nx = ny = nz = 0.0
    k = len(pts)
    for i in range(k):
        a, b = pts[i], pts[(i + 1) % k]
        nx += (a[1] - b[1]) * (a[2] + b[2])
        ny += (a[2] - b[2]) * (a[0] + b[0])
        nz += (a[0] - b[0]) * (a[1] + b[1])
    n = sk._norm((nx, ny, nz))
    if n[0] * want[0] + n[1] * want[1] + n[2] * want[2] < 0:
        n = (-n[0], -n[1], -n[2])
    g.face(pts, f, normal=n)


def obox(g, T, centre, half, yaw, f, bevel=.03, pitch=0.0, roll=0.0, skip=()):
    """A bevelled box turned about its centre (a fallen block, a plank)."""
    t = mk.Geo()
    sk.box(t, -half[0], half[0], -half[1], half[1], -half[2], half[2], f, T, bevel=bevel, cell=0, skip=skip)
    g.extend(t.transformed(mul(mk.translate(*centre), rot(0, 1, 0, yaw), rot(0, 0, 1, roll), rot(1, 0, 0, pitch))))


# ---------------------------------------------------------------------------------------------------------------------------
# The ground: level at the wall and in the ward, the field falling away past its craters
# ---------------------------------------------------------------------------------------------------------------------------
def _sstep(e0, e1, x):
    t = min(1.0, max(0.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def ground_y(x, z):
    y = 0.0
    if z > 2.5:
        y = -3.4 * _sstep(2.5, 30.0, z) - .9 * _sstep(30.0, 46.0, z)
    k = _sstep(1.5, 5.0, z) + .3 * _sstep(-4.0, -7.0, z)
    if k > 0:
        v = sk.fbm(np.array([[x * .21, 0.0, z * .21]]), 1.0, 2, 41)[0] - .5
        y += v * .5 * k
    for (cx, cz, r) in CRATERS:
        d = math.hypot(x - cx, z - cz)
        if d < r * 1.8:
            y += -.42 * math.exp(-(d / (.55 * r)) ** 2) + .16 * math.exp(-((d - r) / (.28 * r)) ** 2)
    # the pilot's ground stays level
    c = math.hypot(x, z)
    if c < 3.2:
        y *= _sstep(1.6, 3.2, c)
    return y


def _lines(segs):
    out = []
    for (a, b, step) in segs:
        n = max(1, int(round((b - a) / step)))
        for i in range(n):
            out.append(a + (b - a) * i / n)
    out.append(segs[-1][1])
    return out


def ground(g, T):
    """One grid over the field, the breach and the ward (fine near the pilot and the craters), earth outside, cobbles in the
    ward; scorch patches and the gate road's broken cobbles lie on it."""
    if T.i == 0:
        xs = _lines(((-44, -20, 8.0), (-20, -12, 4.0), (-12, 12, .8), (12, 20, 4.0), (20, 44, 8.0)))
        zs = _lines(((-34, -14, 6.0), (-14, -6, 2.0), (-6, ZB, 1.0), (ZB, ZF, 1.25), (ZF, 17, .8), (17, 30, 3.0), (30, 52, 7.0)))
    else:
        xs = _lines(((-44, -12, 10.0), (-12, 12, 3.0), (12, 44, 10.0)))
        zs = _lines(((-34, ZB, 7.0), (ZB, ZF, 2.5), (ZF, 17, 2.8), (17, 52, 9.0)))
    Y = [[ground_y(x, z) for x in xs] for z in zs]
    nx_, nz_ = len(xs), len(zs)
    for j in range(nz_ - 1):
        for i in range(nx_ - 1):
            cz = (zs[j] + zs[j + 1]) / 2
            f = F_COBBLE if cz < ZB else F_EARTH
            q = ((xs[i], Y[j][i], zs[j]), (xs[i + 1], Y[j][i + 1], zs[j]), (xs[i + 1], Y[j + 1][i + 1], zs[j + 1]),
                 (xs[i], Y[j + 1][i], zs[j + 1]))
            ns = []
            for (ii, jj) in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)):
                ia, ib = max(0, ii - 1), min(nx_ - 1, ii + 1)
                ja, jb = max(0, jj - 1), min(nz_ - 1, jj + 1)
                gx = (Y[jj][ib] - Y[jj][ia]) / max(1e-6, xs[ib] - xs[ia])
                gz = (Y[jb][ii] - Y[ja][ii]) / max(1e-6, zs[jb] - zs[ja])
                ns.append(sk._norm((-gx, 1.0, -gz)))
            g.face(q, f, normals=tuple(ns))
    r = mk.rng(13)
    # scorch: the craters' bowls and the burnt patches, lying on the ground
    for (cx, cz, rad) in tuple((c[0], c[1], c[2] * .75) for c in CRATERS):
        patch(g, T, cx, cz, rad, F_SCORCH, r, lift=.1)
    for (cx, cz, rad) in SCORCH:
        patch(g, T, cx, cz, rad, F_SCORCH, r, lift=.03)
    # broken paving in the breach (flat, the pilot's ground)
    if T.i < 2:
        for k in range((9, 4)[T.i]):
            x, z = float(r.uniform(-1.3, 1.3)), float(r.uniform(-3.0, -.7))
            a = float(r.uniform(0, 3))
            obox(g, T, (x, .012, z), (float(r.uniform(.18, .32)), .02, float(r.uniform(.15, .28))), a, F_BLOCK, bevel=.008)
    # the gate road: broken cobbles from the gatehouse out over the field
    road(g, T, r)


def patch(g, T, cx, cz, rad, f, r, lift=.02, sides=None):
    """A ragged patch lying on the ground (a fan whose corners follow the ground)."""
    m = sides or (9, 7, 5)[T.i]
    c = (cx, ground_y(cx, cz) + lift, cz)
    ring = []
    for i in range(m):
        a = 2 * math.pi * i / m + float(r.uniform(-.2, .2))
        d = rad * float(r.uniform(.6, 1.1))
        x, z = cx + math.cos(a) * d, cz + math.sin(a) * d
        ring.append((x, ground_y(x, z) + lift, z))
    for i in range(m):
        _quad(g, [c, ring[(i + 1) % m], ring[i]], f, (0, 1, 0))


def road(g, T, r):
    """The cobbled road out of the gate: a broken strip lying on the ground (gaps where the barrage tore it up), loose
    cobbles about."""
    gx = (GH_X[0] + GH_X[1]) / 2
    pts = [(gx, GH_Z + .1), (gx + .2, 4.0), (gx + 1.4, 8.0), (gx + 3.4, 12.0), (gx + 5.6, 16.5)]
    path = sk.catmull([(p[0], 0, p[1]) for p in pts], per=(4, 3, 2)[T.i])
    w = 1.6
    for i in range(len(path) - 1):
        a, b = path[i], path[i + 1]
        if r.random() < .22:
            continue
        dx, dz = b[0] - a[0], b[2] - a[2]
        L = math.hypot(dx, dz) or 1.0
        sx, sz = -dz / L, dx / L
        wa, wb = w * float(r.uniform(.75, 1.0)), w * float(r.uniform(.75, 1.0))
        q = []
        for (p, ww, sd) in ((a, wa, -1), (b, wb, -1), (b, wb, 1), (a, wa, 1)):
            x, z = p[0] + sx * ww * sd, p[2] + sz * ww * sd
            q.append((x, ground_y(x, z) + .025, z))
        _quad(g, q, F_COBBLE, (0, 1, 0))
    if T.i < 2:
        for k in range((10, 5)[T.i]):
            p = path[int(r.integers(len(path)))]
            x, z = p[0] + float(r.uniform(-2.2, 2.2)), p[2] + float(r.uniform(-.8, .8))
            sz = float(r.uniform(.07, .12))
            mk.rock(g, (x, ground_y(x, z) - .01, z), (sz, sz * .55, sz * .9), F_COBBLE, T, seed=300 + k, rough=.2, level=0,
                    turn=float(r.uniform(0, 3)))


# ---------------------------------------------------------------------------------------------------------------------------
# The curtain wall: its body either side, the breach's edges course by course, the parapet and its merlons
# ---------------------------------------------------------------------------------------------------------------------------
def wall_span(g, T, xa, xb, r, end_a=False, end_b=False):
    """The wall's solid body from xa to xb (xa < xb): battered foot, front face, corbel table, parapet and merlons, the walk
    and the inner parapet, the back face; end faces where asked."""
    cf = (1.6, 4.0, 99.0)[T.i]
    L = xb - xa
    # the battered foot
    n = max(1, int(L / cf))
    for i in range(n):
        u0, u1 = i / n, (i + 1) / n
        x0, x1 = xa + L * u0, xa + L * u1
        _quad(g, [(x0, 0.0, ZF + BATTER), (x1, 0.0, ZF + BATTER), (x1, 1.3, ZF), (x0, 1.3, ZF)], F_ASHLAR_DK, (0, .3, 1))
    sk.grid_face(g, (xa, 1.3, ZF), (L, 0, 0), (0, CORBEL_Y - 1.3, 0), 0, 0, F_ASHLAR, (0, 0, 1), cf)
    # the corbel table and the parapet over it
    zc = ZF + .28
    _quad(g, [(xa, CORBEL_Y, ZF), (xb, CORBEL_Y, ZF), (xb, CORBEL_Y + .3, zc), (xa, CORBEL_Y + .3, zc)], F_ASHLAR_DK, (0, -1, 1))
    sk.grid_face(g, (xa, CORBEL_Y + .3, zc), (L, 0, 0), (0, PARA_Y - CORBEL_Y - .3, 0), 0, 0, F_ASHLAR, (0, 0, 1), cf * 2)
    g.face([(xa, PARA_Y, zc), (xb, PARA_Y, zc), (xb, PARA_Y, ZF - .45), (xa, PARA_Y, ZF - .45)], F_WALK, normal=(0, 1, 0))
    g.face([(xa, WALK_Y, ZF - .45), (xb, WALK_Y, ZF - .45), (xb, PARA_Y, ZF - .45), (xa, PARA_Y, ZF - .45)], F_ASHLAR, normal=(0, 0, -1))
    # the walk and the inner parapet
    sk.grid_face(g, (xa, WALK_Y, ZB + .5), (L, 0, 0), (0, 0, ZF - .45 - ZB - .5), 0, 0, F_WALK, (0, 1, 0), (2.0, 5.0, 99.0)[T.i])
    g.face([(xa, WALK_Y, ZB + .5), (xb, WALK_Y, ZB + .5), (xb, WALK_Y + .8, ZB + .5), (xa, WALK_Y + .8, ZB + .5)], F_ASHLAR,
           normal=(0, 0, 1))
    g.face([(xa, WALK_Y + .8, ZB), (xb, WALK_Y + .8, ZB), (xb, WALK_Y + .8, ZB + .5), (xa, WALK_Y + .8, ZB + .5)], F_WALK,
           normal=(0, 1, 0))
    sk.grid_face(g, (xa, 0.0, ZB), (L, 0, 0), (0, WALK_Y + .8, 0), 0, 0, F_ASHLAR, (0, 0, -1), (3.0, 6.0, 99.0)[T.i])
    for (x, end) in ((xa, end_a), (xb, end_b)):
        if end:
            sgn = -1 if x == xa else 1
            g.face([(x, 0, ZB), (x, 0, ZF + BATTER), (x, 1.3, ZF), (x, PARA_Y, zc), (x, PARA_Y, ZB)], F_CORE, normal=(sgn, 0, 0))
    # merlons along the parapet (a few broken), arrow slits in them
    w, gap = 1.05, .62
    x = xa + float(r.uniform(.1, .5))
    while x + w <= xb - .05:
        top = MERLON_Y
        q = r.random()
        if q < .08:
            top = PARA_Y + float(r.uniform(.15, .45))
        if q > .97:
            x += w + gap
            continue
        sk.box(g, x, x + w, PARA_Y, top, ZF - .45, zc, F_ASHLAR, T, bevel=.03, cell=0, skip=('-y',))
        if T.i == 0 and top == MERLON_Y:
            xm = x + w / 2
            g.face([(xm - .04, PARA_Y + .15, zc + .004), (xm + .04, PARA_Y + .15, zc + .004), (xm + .04, top - .2, zc + .004),
                    (xm - .04, top - .2, zc + .004)], F_SLIT, normal=(0, 0, 1))
        x += w + gap


def courses():
    """The heights of the wall's courses at the breach (big blocks, a little uneven)."""
    r = mk.rng(17)
    ys = [0.0]
    while ys[-1] < WALK_Y - .9:
        ys.append(ys[-1] + float(r.uniform(.6, .76)))
    ys.append(WALK_Y)
    return ys


def edge_x(s, y):
    foot, top = BREACH[s]
    u = max(0.0, y) / WALK_Y
    return foot + (top - foot) * u ** 1.3 + .32 * math.sin(y * 1.7 + (1.1 if s > 0 else 2.6)) * min(1.0, y / 1.2)


def breach_edge(g, T, s):
    """One side of the breach (s: +1 the pilot's left, -1 its right): course by course the front and back facing blocks
    (each a little out of true, one standing out, one gone) and the rubble core between them, fallen back in a ragged face,
    end raggedly; the body's faces run on beyond them to EDGE_X; the parapet's broken end and the walk above."""
    r = mk.rng(20 + s)
    ys = courses()
    xo = EDGE_X
    zc = ZF + .28
    zf0, zb0 = ZF - .62, ZB + .58     # the core between the facings
    for j in range(len(ys) - 1):
        ya, yb = ys[j], ys[j + 1]
        ym = (ya + yb) / 2
        xe = edge_x(s, ym) + float(r.uniform(-.28, .28))
        if ym < 2.4:
            xe = max(xe, 2.2)
        nxt = edge_x(s, ys[min(j + 1, len(ys) - 2)] + .3) + .25
        zf = ZF + BATTER * max(0.0, 1 - ym / 1.3)
        if T.i == 2:
            sk.box(g, s * xe, s * xo, ya, yb, ZB, zf, F_BLOCK, T, bevel=0, cell=0, skip=('-y',))
            continue
        # the front facing: the breach-end block (a little out of true), then the course's face on to the body
        xf = xe + float(r.uniform(-.3, .18))
        if ym < 2.4:
            xf = max(xf, 2.1)
        lf = max(float(r.uniform(.6, 1.6)), nxt - xf + .3)
        lf = min(lf, xo - xf - .05)
        out = float(r.uniform(.02, .14)) if r.random() < .35 else 0.0
        if r.random() > .12:
            h = (yb - ya) / 2 - .008
            d = (zf + out - zf0) / 2
            push = min(float(r.uniform(.05, .22)), max(0.0, xf - 2.1) if ym < 2.4 else 1.0) if r.random() < .25 else 0.0
            f = F_BLOCK if r.random() < .7 else F_BLOCK_LT
            parts = [(xf - push, xf + lf)]
            if lf > 1.0 and r.random() < .35:
                cut = xf + lf * float(r.uniform(.35, .6))
                parts = [(xf - push, cut - .025), (cut + .025, xf + lf)]
            for (pa, pb) in parts:
                obox(g, T, (s * ((pa + pb) / 2 - .01), ym + float(r.uniform(-.02, .0)), zf0 + d), ((pb - pa) / 2 - .01, h, d),
                     float(r.uniform(-.06, .06)), f, bevel=.045, pitch=float(r.uniform(-.05, .05)), roll=float(r.uniform(-.05, .05)),
                     skip=('-y', '-z'))
            if T.i == 0 and r.random() < .35:
                sz = float(r.uniform(.07, .13))
                mk.rock(g, (s * (xf - push + float(r.uniform(.05, .3))), yb + sz * .3, ZF - float(r.uniform(.1, .5))), (sz, sz * .7, sz),
                        F_BLOCK_LT, T, seed=440 + j + (s > 0) * 40, rough=.3, level=0, turn=float(r.uniform(0, 3)))
        else:
            lf = 0.0
        x1 = xf + lf
        if x1 < xo:
            _quad(g, [(s * x1, ya, zf), (s * xo, ya, zf), (s * xo, yb, ZF if ym > 1.3 else zf), (s * x1, yb, ZF if ym > 1.3 else zf)],
                  F_ASHLAR, (0, .2 if ym < 1.3 else 0, 1))
        # the back facing
        xb_ = xe + float(r.uniform(-.1, .45))
        lb = min(max(float(r.uniform(.8, 1.4)), nxt - xb_ + .3), xo - xb_ - .05)
        obox(g, T, (s * (xb_ + lb / 2 - .01), ym, ZB + .29), (lb / 2 - .01, (yb - ya) / 2 - .008, .29), float(r.uniform(-.03, .03)), F_BLOCK,
             bevel=.04, pitch=float(r.uniform(-.02, .02)), skip=('-y', '+z'))
        if xb_ + lb < xo:
            _quad(g, [(s * (xb_ + lb), ya, ZB), (s * xo, ya, ZB), (s * xo, yb, ZB), (s * (xb_ + lb), yb, ZB)], F_ASHLAR, (0, 0, -1))
        # the core: mortared rubble fallen back from the facings, its broken face ragged
        xc = xe + float(r.uniform(.15, .55))
        sk.box(g, s * xc, s * xo, ya, yb, zb0, zf0, F_CORE, T, bevel=0, cell=0, skip=('-y', '-x' if s > 0 else '+x'))
        nz, ny = (3, 2) if T.i == 0 else (2, 1)
        grid = [[(s * (xc + (float(r.uniform(-.12, .16)) if 0 < a < nz and 0 < b < ny else float(r.uniform(-.03, .03)))),
                  ya + (yb - ya) * b / ny, zb0 + (zf0 - zb0) * a / nz) for a in range(nz + 1)] for b in range(ny + 1)]
        for b in range(ny):
            for a in range(nz):
                _quad(g, [grid[b][a], grid[b][a + 1], grid[b + 1][a + 1], grid[b + 1][a]], F_CORE, (-s, 0, 0))
        if T.i == 0 and j % 3 == 1:
            sz = float(r.uniform(.12, .2))
            mk.rock(g, (s * (xc - sz * .2), ym, float(r.uniform(zb0 + .3, zf0 - .3))), (sz, sz * .8, sz), F_BLOCK, T, seed=400 + j + (s > 0) * 50,
                    rough=.3, flat_bottom=False, level=0, turn=float(r.uniform(0, 3)))
    # above the walk: the parapet's broken end, a broken merlon, the inner parapet
    xt = edge_x(s, WALK_Y) + float(r.uniform(.5, .9))
    if T.i < 2:
        sk.box(g, s * xt, s * xo, WALK_Y, PARA_Y - float(r.uniform(0, .2)), ZF - .45, zc, F_BLOCK, T, bevel=.03, cell=0, skip=('-y',))
        sk.box(g, s * (xo - 1.0), s * (xo - .1), PARA_Y, PARA_Y + float(r.uniform(.3, .6)), ZF - .45, zc, F_ASHLAR, T, bevel=.03, cell=0,
               skip=('-y',))
        sk.box(g, s * (xt + .2), s * xo, WALK_Y, WALK_Y + .5, ZB, ZB + .5, F_BLOCK, T, bevel=.03, cell=0, skip=('-y',))
    # the corbel table's broken end
    _quad(g, [(s * xt, CORBEL_Y, ZF), (s * xo, CORBEL_Y, ZF), (s * xo, CORBEL_Y + .3, zc), (s * xt, CORBEL_Y + .3, zc)], F_ASHLAR_DK,
          (0, -1, 1))
    _quad(g, [(s * xt, CORBEL_Y + .3, zc), (s * xo, CORBEL_Y + .3, zc), (s * xo, WALK_Y, zc), (s * xt, WALK_Y, zc)], F_ASHLAR, (0, 0, 1))


def wall(g, T):
    r = mk.rng(9)
    wall_span(g, T, EDGE_X, WALL_END, r, end_b=True)
    wall_span(g, T, -WALL_END, GH_X[0], r, end_a=True)
    for s in (1, -1):
        breach_edge(g, T, s)
    damage(g, T, r)


def damage(g, T, r):
    """Blocks knocked loose from the wall's face by the stones."""
    if T.i < 2:
        for (x, y) in ((7.4, 3.1), (11.6, 1.7), (16.2, 3.6)):
            for k in range(3):
                bx = x + float(r.uniform(-.5, .5))
                by = y + float(r.uniform(-.4, .4))
                obox(g, T, (bx, by, ZF + float(r.uniform(.02, .12))), (float(r.uniform(.25, .4)), float(r.uniform(.18, .28)), .2),
                     float(r.uniform(-.1, .1)), F_BLOCK_LT, bevel=.03, roll=float(r.uniform(-.12, .12)), pitch=float(r.uniform(-.1, .1)))


def rubble(g, T):
    """The breach's scree: heaps against both edges, spilling out over the foot of the wall and back into the ward, fallen
    blocks and stones on them; the middle of the breach left clear."""
    r = mk.rng(23)
    for s in (1, -1):
        # the heaps (the inner one kept back from the pilot's path)
        for (cx, cz, sx, sy, sz, seed) in ((3.15, -1.75, 1.05, 1.05, 1.6, 1), (3.4, .6, 1.0, .55, .85, 2), (2.9, -3.95, 1.05, .6, 1.0, 3)):
            mk.rock(g, (s * cx, -.05, cz), (sx, sy, sz), F_CORE if seed != 2 else F_BLOCK, T, seed=500 + seed + (s > 0) * 10, rough=.3,
                    level=(1, 1, 0)[T.i], turn=float(r.uniform(0, 3)))
        # fallen blocks
        for k in range((7, 4, 2)[T.i]):
            x = s * float(r.uniform(2.0, 4.2))
            z = float(r.uniform(-4.2, 1.6))
            if math.hypot(x, z) < 2.7:
                continue
            y = ground_y(x, z) + float(r.uniform(.1, .5))
            obox(g, T, (x, y, z), (float(r.uniform(.3, .55)), float(r.uniform(.2, .3)), float(r.uniform(.25, .4))), float(r.uniform(0, 3)),
                 F_BLOCK if k % 3 else F_BLOCK_LT, bevel=.035, pitch=float(r.uniform(-.4, .4)), roll=float(r.uniform(-.4, .4)))
        # stones
        if T.i == 0:
            for k in range(8):
                x = s * float(r.uniform(1.5, 4.5))
                z = float(r.uniform(-4.5, 2.4))
                if math.hypot(x, z) < 2.6:
                    continue
                sz = float(r.uniform(.08, .2))
                mk.rock(g, (x, ground_y(x, z) - .02, z), (sz, sz * .7, sz), F_BLOCK, T, seed=520 + k + (s > 0) * 20, rough=.3, level=0,
                        turn=float(r.uniform(0, 3)))


# ---------------------------------------------------------------------------------------------------------------------------
# The gatehouse: a tower standing out of the wall, its gate arch gaping, the portcullis fallen out on the road
# ---------------------------------------------------------------------------------------------------------------------------
def gatehouse(g, T):
    r = mk.rng(29)
    x0, x1 = GH_X
    gx = (x0 + x1) / 2
    zf, zb = GH_Z, ZB - .2
    cf = (1.6, 4.0, 99.0)[T.i]
    gl, gr = gx - GATE_HW, gx + GATE_HW
    # the front: battered foot, the face round the gate arch, string course, corbelled parapet, merlons
    for (xa, xb) in ((x0, gl), (gr, x1)):
        _quad(g, [(xa, 0, zf + BATTER), (xb, 0, zf + BATTER), (xb, 1.3, zf), (xa, 1.3, zf)], F_ASHLAR_DK, (0, .3, 1))
        sk.grid_face(g, (xa, 1.3, zf), (xb - xa, 0, 0), (0, GATE_SPRING + .1 - 1.3, 0), 0, 0, F_ASHLAR, (0, 0, 1), cf)
    sk.grid_face(g, (x0, GATE_SPRING + .1, zf), (gl - x0, 0, 0), (0, GH_Y - .4 - GATE_SPRING - .1, 0), 0, 0, F_ASHLAR, (0, 0, 1), cf)
    sk.grid_face(g, (gr, GATE_SPRING + .1, zf), (x1 - gr, 0, 0), (0, GH_Y - .4 - GATE_SPRING - .1, 0), 0, 0, F_ASHLAR, (0, 0, 1), cf)
    # over the arch: the face down to the arch's curve, the voussoirs standing proud round it
    n = (10, 6, 3)[T.i]
    half = sk.pointed_arch(GATE_HW, GATE_APEX - GATE_SPRING, n)
    curve = [(gl + u, GATE_SPRING + h) for u, h in half] + [(gr - u, GATE_SPRING + h) for u, h in reversed(half[:-1])]
    top_y = GH_Y - .4
    for i in range(len(curve) - 1):
        (xa, ya), (xb, yb) = curve[i], curve[i + 1]
        _quad(g, [(xa, ya, zf), (xb, yb, zf), (xb, top_y, zf), (xa, top_y, zf)], F_ASHLAR, (0, 0, 1))
    vous = [(p[0], p[1], zf) for p in curve]
    if T.i < 2:
        sk.sweep(g, sk.rect_section(.42, .12), [(p[0], p[1], zf + .05) for p in vous], F_BLOCK, up=(0, 0, 1), smooth_u=False)
    # the passage: its walls, the intrados, the floor's cobbles, a burnt door leaf hanging
    for (x, sgn) in ((gl, 1), (gr, -1)):
        sk.grid_face(g, (x, 0, zb), (0, 0, zf - zb), (0, GATE_SPRING, 0), 0, 0, F_ASHLAR_DK, (sgn, 0, 0), (2.0, 5.0, 99.0)[T.i])
    for i in range(len(curve) - 1):
        (xa, ya), (xb, yb) = curve[i], curve[i + 1]
        _quad(g, [(xa, ya, zf), (xb, yb, zf), (xb, yb, zb), (xa, ya, zb)], F_ASHLAR_DK, (gx - (xa + xb) / 2, -1, 0))
    leaf = mk.Geo()
    sk.box(leaf, 0, 1.2, .05, 3.3, -.06, .06, F_CHARRED, T, bevel=.01, cell=0)
    if T.i == 0:
        for y in (.6, 1.6, 2.6):
            sk.box(leaf, .02, 1.18, y, y + .1, .06, .075, F_IRON, T, bevel=0, cell=0)
    g.extend(leaf.transformed(mul(mk.translate(gl + .05, 0, zf - 1.9), rot(0, 1, 0, -1.1), rot(0, 0, 1, .05))))
    # the tower's sides and back
    for (x, sgn) in ((x0, -1), (x1, 1)):
        sk.grid_face(g, (x, 0, zb), (0, 0, zf - zb), (0, GH_Y - .4, 0), 0, 0, F_ASHLAR, (sgn, 0, 0), (2.0, 5.0, 99.0)[T.i])
    sk.grid_face(g, (x0, 0, zb), (x1 - x0, 0, 0), (0, GH_Y - .4, 0), 0, 0, F_ASHLAR, (0, 0, -1), (3.0, 6.0, 99.0)[T.i])
    # string course, corbel table, parapet, merlons, the roof of the tower
    sk.box(g, x0 - .12, x1 + .12, 6.0, 6.22, zb, zf + .12, F_ASHLAR_DK, T, bevel=.02, cell=0, skip=('-z',))
    _quad(g, [(x0 - .25, GH_Y - .4, zf), (x1 + .25, GH_Y - .4, zf), (x1 + .25, GH_Y - .1, zf + .25), (x0 - .25, GH_Y - .1, zf + .25)],
          F_ASHLAR_DK, (0, -1, 1))
    sk.box(g, x0 - .25, x1 + .25, GH_Y - .1, GH_Y + .6, zf - .3, zf + .25, F_ASHLAR, T, bevel=.02, cell=0, skip=('-y',))
    for (xa, xb) in ((x0 - .25, x0 + .2), (x1 - .2, x1 + .25)):
        sk.box(g, xa, xb, GH_Y - .1, GH_Y + .6, zb, zf - .3, F_ASHLAR, T, bevel=.02, cell=0, skip=('-y',))
    g.face([(x0 + .2, GH_Y, zb), (x1 - .2, GH_Y, zb), (x1 - .2, GH_Y, zf - .3), (x0 + .2, GH_Y, zf - .3)], F_WALK, normal=(0, 1, 0))
    x = x0 - .15
    while x + .9 <= x1 + .2:
        sk.box(g, x, x + .9, GH_Y + .6, GH_Y + 1.45, zf - .3, zf + .25, F_ASHLAR, T, bevel=.03, cell=0, skip=('-y',))
        x += 1.45
    # arrow slits, one lit
    if T.i < 2:
        for (sx, sy, f) in ((gx - 1.6, 7.4, F_SLIT), (gx + 1.7, 7.6, F_WINDOW), (gx, 8.4, F_SLIT), (x0 + .5, 2.2, F_SLIT)):
            g.face([(sx - .07, sy - .55, zf + .005), (sx + .07, sy - .55, zf + .005), (sx + .07, sy + .55, zf + .005), (sx - .07, sy + .55, zf + .005)],
                   f, normal=(0, 0, 1))
    # the fallen portcullis: its oak grid lies tipped out on the road, its spikes toward the field
    portcullis(g, T, (gx + .3, .0, zf + 2.2))


def portcullis(g, T, at):
    x, _, z = at
    t = mk.Geo()
    W, H = 2.7, 3.9
    nv, nh = (7, 6, 4)[T.i], (6, 5, 3)[T.i]
    for i in range(nv):
        u = -W / 2 + W * i / (nv - 1)
        sk.box(t, u - .065, u + .065, 0, H, -.065, .065, F_CHARRED if i % 3 == 1 else F_TIMBER, T, bevel=.01 if T.i == 0 else 0, cell=0)
        if T.i == 0:
            sk.lathe(t, [(.05, 0), (0, -.22)], (u, 0, 0), F_IRON, 4, smooth=False)
    for j in range(nh):
        v = .35 + (H - .6) * j / (nh - 1)
        sk.box(t, -W / 2 - .05, W / 2 + .05, v - .06, v + .06, -.14, -.065, F_TIMBER_X, T, bevel=.01 if T.i == 0 else 0, cell=0)
        if T.i == 0 and j % 2 == 0:
            sk.box(t, -W / 2 - .05, W / 2 + .05, v - .03, v + .03, .065, .085, F_IRON, T, bevel=0, cell=0)
    # it fell outward: lying on its face, its top end propped on the fallen stones
    g.extend(t.transformed(mul(mk.translate(x, .24, z - 1.8), rot(0, 1, 0, .18), rot(1, 0, 0, math.pi / 2 - .1))))
    for k in range((3, 2, 0)[T.i]):
        mk.rock(g, (x - .6 + k * .7, .0, z - 1.95), (.25, .2, .22), F_BLOCK, T, seed=600 + k, rough=.25, level=0)


# ---------------------------------------------------------------------------------------------------------------------------
# The field: arrows, stakes, barricades, the broken cart, siege debris
# ---------------------------------------------------------------------------------------------------------------------------
def arrow(g, T, base, yaw, tilt, L, r):
    """An arrow stuck in the ground (base) leaning by `tilt` toward `yaw`: its shaft and three fletches near its end."""
    d = (math.sin(yaw) * math.sin(tilt), math.cos(tilt), math.cos(yaw) * math.sin(tilt))
    end = sk._add(base, d, L)
    sk.cylinder(g, base, end, .0065, F_SHAFT, 3, caps=False)
    if T.i == 0:
        a = sk._add(end, d, -.04)
        b = sk._add(end, d, -.17)
        side = sk._norm(sk._cross(d, (0, 1, 0))) if abs(d[1]) < .99 else (1, 0, 0)
        up = sk._cross(side, d)
        for k in range(2):
            ang = k * math.pi / 2 + float(r.uniform(0, .5))
            v = sk._add(sk._lerp((0, 0, 0), side, math.cos(ang)), up, math.sin(ang))
            p = [sk._add(a, v, .022), sk._add(b, v, .03), sk._add(b, v, -.03), sk._add(a, v, -.022)]
            nn = sk._norm(sk._cross(d, v))
            g.face(p, F_FLETCH, normal=nn)
            g.face(p[::-1], F_FLETCH, normal=(-nn[0], -nn[1], -nn[2]))


def stake(g, T, base, yaw, lean, L, rad, f=F_TIMBER, broken=False):
    """A sharpened stake driven slanting into the ground (its point toward the field), or snapped off."""
    t = mk.Geo()
    if broken:
        sk.lathe(t, [(rad, 0), (rad, L * .55), (rad * .7, L * .62), (rad * .3, L * .66)], (0, 0, 0), f, (5, 4, 3)[T.i], smooth=False)
    else:
        sk.lathe(t, [(rad, 0), (rad, L * .78), (0, L)], (0, 0, 0), f, (5, 4, 3)[T.i], smooth=False)
    g.extend(t.transformed(mul(mk.translate(*base), rot(0, 1, 0, yaw), rot(1, 0, 0, lean))))


def field(g, T):
    r = mk.rng(31)
    # arrows: volleys stuck in the ground before the wall and about the pilot (outside his ground), a few in the timber
    n = (46, 22, 10)[T.i]
    k = 0
    while k < n:
        x = float(r.uniform(-9, 9))
        z = float(r.uniform(-.2, 9.5))
        if math.hypot(x, z) < 2.75 or (abs(x) < 2.4 and z < .2):
            continue
        k += 1
        yaw = float(r.uniform(-.5, .5)) + math.pi
        arrow(g, T, (x, ground_y(x, z) - .05, z), yaw, float(r.uniform(.25, .7)), float(r.uniform(.55, .72)), r)
    # the stakes: a slanting row before the wall, many broken, a gap where the attack came through
    for s in (1, -1):
        for i in range((9, 6, 3)[T.i]):
            x = s * (3.0 + i * .82 + float(r.uniform(-.15, .15)))
            z = 5.4 + float(r.uniform(-.25, .25)) + .25 * math.sin(i)
            q = r.random()
            if q < .12:
                continue
            stake(g, T, (x, ground_y(x, z) - .05, z), float(r.uniform(-.2, .2)), .62 + float(r.uniform(-.12, .12)),
                  float(r.uniform(1.3, 1.8)), .06, broken=q < .45)
        # a fallen stake
        if T.i < 2:
            stake(g, T, (s * 5.2, ground_y(s * 5.2, 6.3) + .05, 6.3), s * 1.3, 1.5, 1.6, .06)
    # the timber barricades (mantlets of planks on struts) either side of the stakes' gap
    for (x, z, yaw) in ((-3.9, 6.6, .2), (4.2, 7.0, -.25)):
        barricade(g, T, (x, ground_y(x, z), z), yaw, r)
    # the broken cart, its load of stones spilled
    cart(g, T, r)
    # siege debris: a scaling ladder against the wall, another broken on the ground, a mantlet, trebuchet stones
    ladder(g, T, (12.2, ZF + BATTER + 1.8), 5.4, .33, r)
    ladder(g, T, (-3.6, 9.0), 4.2, -1.45, r, lying=True)
    for (x, z, rad) in ((5.6, 6.2, .26), (-8.6, 9.8, .3), (3.4, 14.5, .28), (-4.4, 12.2, .25), (9.6, 4.0, .3)):
        mk.rock(g, (x, ground_y(x, z) - .05, z), (rad, rad * .92, rad), F_BLOCK, T, seed=700 + int(x * 10), rough=.08, flat_bottom=False,
                level=(1, 1, 0)[T.i] if rad > .27 else (1, 0, 0)[T.i])


def barricade(g, T, at, yaw, r):
    """A mantlet: upright planks on two rails, propped from behind by struts; arrows stuck in it, a plank split off."""
    x, y, z = at
    t = mk.Geo()
    W, H = 2.6, 1.7
    k = (8, 5, 3)[T.i]
    for i in range(k):
        u = -W / 2 + W * (i + .5) / k
        h = H - (float(r.uniform(.2, .7)) if r.random() < .3 else 0.0)
        sk.box(t, u - W / k / 2 + .01, u + W / k / 2 - .01, -.1, h, -.03, .03, F_PLANK if i % 3 else F_TIMBER, T, bevel=.008 if T.i == 0 else 0, cell=0)
    for v in (.35, 1.25):
        sk.box(t, -W / 2 - .05, W / 2 + .05, v, v + .12, -.1, -.03, F_TIMBER_X, T, bevel=.01 if T.i == 0 else 0, cell=0)
    for u in (-.8, .8):
        sk.cylinder(t, (u, 1.15, -.1), (u, -.1, -1.2), .05, F_TIMBER, 4)
    g.extend(t.transformed(mul(mk.translate(x, y, z), rot(0, 1, 0, yaw), rot(1, 0, 0, -.1))))
    if T.i == 0:
        for q in range(4):
            u = float(r.uniform(-1.1, 1.1))
            v = float(r.uniform(.4, 1.4))
            p = mul(mk.translate(x, y, z), rot(0, 1, 0, yaw), rot(1, 0, 0, -.1))
            A = np.array(p).reshape(3, 4)
            base = tuple(A[:, :3] @ np.array([u, v, .03]) + A[:, 3])
            arrow(g, T, base, yaw + float(r.uniform(-.3, .3)), float(r.uniform(1.25, 1.5)), .45, r)


def cart(g, T, r):
    """A two-wheeled supply cart on its side, one wheel off, its stones spilled."""
    x, z = CART
    y = ground_y(x, z)
    t = mk.Geo()
    sk.box(t, -.8, .8, .45, .52, -1.2, 1.2, F_PLANK, T, bevel=.01, cell=0)
    for sx in (-1, 1):
        sk.box(t, sx * .8 - .04, sx * .8 + .04, .52, .9, -1.2, 1.2, F_PLANK, T, bevel=.01, cell=0)
    sk.box(t, -.8, .8, .52, .85, 1.16, 1.22, F_PLANK, T, bevel=.01, cell=0)
    for sx in (-.3, .3):
        sk.box(t, sx - .05, sx + .05, .38, .46, -1.2, 3.0, F_TIMBER_Z, T, bevel=.01, cell=0)
    nw = (12, 8, 6)[T.i]
    wheel = [(.06, -.06), (.62, -.06), (.62, .06), (.06, .06)]
    sk.lathe(t, [(p[0], p[1]) for p in wheel], (-.92, .62, 0), F_TIMBER, nw, axis=(1, 0, 0), smooth=False)
    sk.cylinder(t, (-1.0, .62, 0), (.95, .62, 0), .05, F_IRON, 4)
    g.extend(t.transformed(mul(mk.translate(x, y + .05, z), rot(0, 1, 0, .7), rot(0, 0, 1, -1.25))))
    w = mk.Geo()
    sk.lathe(w, wheel, (0, 0, 0), F_TIMBER, nw, axis=(0, 1, 0), smooth=False)
    g.extend(w.transformed(mk.translate(x + 1.6, y + .06, z + 1.1)))
    for k in range((5, 3, 1)[T.i]):
        a = float(r.uniform(0, 6.28))
        d = float(r.uniform(.6, 1.8))
        px, pz = x - 1.0 + math.cos(a) * d, z + math.sin(a) * d
        sz = float(r.uniform(.16, .26))
        mk.rock(g, (px, ground_y(px, pz) - .03, pz), (sz, sz * .85, sz), F_BLOCK, T, seed=800 + k, rough=.1, level=0, turn=a)


def ladder(g, T, at, L, ang, r, lying=False):
    """A scaling ladder: leaning on the wall (ang: its lean back from upright) or lying broken in the field (ang: its yaw)."""
    x, z = at
    t = mk.Geo()
    for sx in (-.3, .3):
        sk.box(t, sx - .045, sx + .045, 0, L if sx < 0 or not lying else L * .6, -.04, .04, F_TIMBER, T, bevel=.008 if T.i == 0 else 0, cell=0)
    k = int(L / (.4, .4, .8)[T.i])
    for i in range(1, k):
        y = i * L / k
        if lying and y > L * .6 and i % 2:
            continue
        sk.box(t, -.3, .3, y - .025, y + .025, -.025, .025, F_TIMBER_X, T, bevel=0, cell=0)
    if lying:
        g.extend(t.transformed(mul(mk.translate(x, ground_y(x, z) + .05, z), rot(0, 1, 0, ang), rot(1, 0, 0, math.pi / 2))))
    else:
        g.extend(t.transformed(mul(mk.translate(x, 0, z), rot(1, 0, 0, -ang))))


# ---------------------------------------------------------------------------------------------------------------------------
# Banners and braziers
# ---------------------------------------------------------------------------------------------------------------------------
def banners(g, T):
    r = mk.rng(37)
    cells = ((6, 9), (4, 6), (3, 3))[T.i]
    # banners on poles on the walk and the gatehouse
    for (x, y, z, h, f, seed) in ((7.2, WALK_Y, ZB + 1.0, 4.2, F_BANNER, 1), (13.8, WALK_Y, ZB + 1.0, 3.6, F_BANNER2, 2),
                                  ((GH_X[0] + GH_X[1]) / 2 + 1.4, GH_Y, ZB + .9, 3.4, F_BANNER, 3)):
        sk.cylinder(g, (x, y, z), (x, y + h, z), .045, F_TIMBER, (6, 4, 3)[T.i])
        sk.box(g, x - .03, x + .03, y + h - .25, y + h - .19, z - .03, z + 1.05, F_TIMBER_Z, T, bevel=0, cell=0)
        mk.cloth(g, (x, y + h - .25, z + .05), (x, y + h - .25, z + 1.0), float(r.uniform(1.6, 2.1)), f, T, seed=seed, sway=.1, tear=.35,
                 cells=cells)
    # the long banner hung down the wall's face, burnt and torn
    for (xa, xb, f, seed) in ((17.2, 18.2, F_BANNER, 4), (-13.2, -12.2, F_BANNER2, 5)):
        mk.cloth(g, (xa, PARA_Y - .05, ZF + .33), (xb, PARA_Y - .05, ZF + .33), float(r.uniform(3.2, 3.8)), f, T, seed=seed, sway=.05,
                 tear=.6, cells=cells)


def brazier(g, T, at):
    """An iron brazier on its tripod in the breach, heaped with glowing coals."""
    x, z = at
    y = ground_y(x, z)
    for k in range(3):
        a = 2 * math.pi * k / 3 + .3
        foot = (x + math.cos(a) * .38, y - .02, z + math.sin(a) * .38)
        sk.cylinder(g, foot, (x + math.cos(a) * .2, y + .78, z + math.sin(a) * .2), .022, F_IRON, (4, 3, 3)[T.i], caps=False)
    n = (12, 8, 6)[T.i]
    sk.lathe(g, [(.05, .7), (.21, .77), (.29, .88), (.31, .94), (.28, .95)], (x, y, z), F_IRON, n, smooth=T.i < 2)
    sk.lathe(g, [(.27, .91), (.2, .99), (.1, 1.04), (0, 1.05)], (x, y, z), F_COALS, n, smooth=False)
    if T.i == 0:
        for k in range(5):
            a = 2 * math.pi * k / 5
            mk.rock(g, (x + math.cos(a) * .12, y + .96, z + math.sin(a) * .12), (.05, .04, .05), F_CHARRED if k % 2 else F_COALS, T,
                    seed=900 + k, rough=.3, flat_bottom=False, level=0)


# ---------------------------------------------------------------------------------------------------------------------------
# Through the breach: the ward, the hall, the well, the keep
# ---------------------------------------------------------------------------------------------------------------------------
def ward(g, T):
    """Through the breach: the cobbled ward, the smithy with its forge's glow, the hall's lit windows, the well, barrels,
    sheds against the wall's back, the keep far back on the left and the ward's far curtain closing it."""
    r = mk.rng(41)
    # the keep: a square tower with a battered foot, corner quoins, slits and a lit window, its crenellated top, a stair turret
    kx, kz = KEEP
    W, H = 6.5, 16.0
    x0, x1, z0, z1 = kx - W / 2, kx + W / 2, kz - W / 2, kz + W / 2
    cf = (3.0, 6.0, 99.0)[T.i]
    for (o, u, v, nrm) in (((x0, 0, z1), (W, 0, 0), (0, H, 0), (0, 0, 1)), ((x0, 0, z0), (0, 0, W), (0, H, 0), (-1, 0, 0)),
                           ((x1, 0, z0), (0, 0, W), (0, H, 0), (1, 0, 0))):
        sk.grid_face(g, o, u, v, 0, 0, F_ASHLAR, nrm, cf)
    sk.box(g, x0 - .4, x1 + .4, 0, 1.6, z0 - .4, z1 + .4, F_ASHLAR_DK, T, bevel=.05, cell=0, skip=('-y', '-z'))
    if T.i < 2:
        for (cx, cz) in ((x0, z1), (x1, z1)):
            sk.box(g, cx - .35, cx + .35, 1.6, H, cz - .35, cz + .35, F_BLOCK, T, bevel=.03, cell=0, skip=('-y',))
        sk.box(g, x0 - .1, x1 + .1, 9.6, 9.85, z1 - .1, z1 + .14, F_ASHLAR_DK, T, bevel=.02, cell=0, skip=('-z',))
    sk.box(g, x0 - .3, x1 + .3, H, H + .7, z0 - .3, z1 + .3, F_ASHLAR, T, bevel=.03, cell=0, skip=('-y',))
    x = x0 - .3
    while x + .9 <= x1 + .3:
        sk.box(g, x, x + .9, H + .7, H + 1.5, z1 - .2, z1 + .3, F_ASHLAR, T, bevel=.03, cell=0, skip=('-y',))
        x += 1.5
    for (sx, sy, f) in ((kx - 1.6, 5.0, F_SLIT), (kx + 1.4, 8.4, F_SLIT), (kx - .4, 12.2, F_WINDOW), (kx + 1.8, 12.8, F_SLIT)):
        g.face([(sx - .1, sy - .6, z1 + .005), (sx + .1, sy - .6, z1 + .005), (sx + .1, sy + .6, z1 + .005), (sx - .1, sy + .6, z1 + .005)],
               f, normal=(0, 0, 1))
    sk.lathe(g, [(1.2, 0), (1.2, H + 3.0), (1.4, H + 3.2), (0, H + 5.6)], (x0 + .5, 0, z1 - 1.0), F_ASHLAR_DK, (10, 7, 5)[T.i], smooth=False)
    # the smithy: rubble walls open to the ward, a lean-to roof, the forge glowing in the dark inside
    sx_, sz_ = SMITHY
    w, d, h = 4.2, 3.0, 2.9
    for (xa, xb, za, zb) in ((sx_ - w / 2, sx_ + w / 2, sz_ - d / 2, sz_ - d / 2 + .35), (sx_ - w / 2, sx_ - w / 2 + .35, sz_ - d / 2, sz_ + d / 2),
                             (sx_ + w / 2 - .35, sx_ + w / 2, sz_ - d / 2, sz_ + d / 2)):
        sk.box(g, xa, xb, 0, h, za, zb, F_CORE, T, bevel=.03, cell=0, skip=('-y',))
    _quad(g, [(sx_ - w / 2 - .3, h + .6, sz_ - d / 2 - .1), (sx_ + w / 2 + .3, h + .6, sz_ - d / 2 - .1), (sx_ + w / 2 + .3, h - .1, sz_ + d / 2 + .5),
              (sx_ - w / 2 - .3, h - .1, sz_ + d / 2 + .5)], F_ROOF, (0, 1, .3))
    for xp in (sx_ - w / 2 + .2, sx_ + w / 2 - .2, sx_):
        sk.box(g, xp - .08, xp + .08, 0, h - .05, sz_ + d / 2 + .2, sz_ + d / 2 + .36, F_TIMBER, T, bevel=.01, cell=0)
    sk.box(g, sx_ - .7, sx_ + .7, 0, .75, sz_ - d / 2 + .35, sz_ - d / 2 + 1.1, F_CORE, T, bevel=.03, cell=0, skip=('-y',))
    patch(g, T, sx_, sz_ - d / 2 + .72, .45, F_COALS, r, lift=.78, sides=6)
    if T.i < 2:
        sk.lathe(g, [(.16, 0), (.2, .25), (.12, .5), (.18, .6)], (sx_ + 1.1, 0, sz_ + .6), F_IRON, 6, smooth=True)
    # the hall: stone walls, a slate roof, lit windows
    hx, hz = HALL
    hw, hd, hh = 8.0, 4.4, 4.2
    sk.box(g, hx - hw / 2, hx + hw / 2, 0, hh, hz - hd / 2, hz + hd / 2, F_ASHLAR_DK, T, bevel=.04, cell=(2.0, 4.0, 0)[T.i], skip=('-y',))
    ridge = hh + 2.6
    for sgn in (1, -1):
        q = [(hx - hw / 2 - .3, hh - .1, hz + sgn * (hd / 2 + .35)), (hx + hw / 2 + .3, hh - .1, hz + sgn * (hd / 2 + .35)),
             (hx + hw / 2 + .3, ridge, hz), (hx - hw / 2 - .3, ridge, hz)]
        _quad(g, q, F_ROOF, (0, 1, sgn))
    for sgn in (1, -1):
        xg = hx + sgn * hw / 2
        g.face([(xg, hh, hz - hd / 2), (xg, hh, hz + hd / 2), (xg, ridge - .1, hz)], F_ASHLAR_DK, normal=(sgn, 0, 0))
    for k in range(3):
        wx = hx - hw / 2 + 1.6 + k * 2.4
        g.face([(wx - .3, 1.4, hz + hd / 2 + .005), (wx + .3, 1.4, hz + hd / 2 + .005), (wx + .3, 2.8, hz + hd / 2 + .005),
                (wx - .3, 2.8, hz + hd / 2 + .005)], F_WINDOW if k != 1 else F_SLIT, normal=(0, 0, 1))
    sk.box(g, hx + 2.6, hx + 3.2, ridge - 1.2, ridge + .9, hz - .3, hz + .3, F_ASHLAR_DK, T, bevel=.02, cell=0, skip=('-y',))
    # the well
    wx_, wz_ = WELL
    sk.lathe(g, [(.8, 0), (.8, .85), (.62, .85), (.62, .3)], (wx_, 0, wz_), F_BLOCK, (12, 8, 5)[T.i], smooth=T.i < 2)
    if T.i < 2:
        for sx in (-.75, .75):
            sk.box(g, wx_ + sx - .06, wx_ + sx + .06, .8, 2.3, wz_ - .06, wz_ + .06, F_TIMBER, T, bevel=.01, cell=0)
        sk.cylinder(g, (wx_ - .8, 2.0, wz_), (wx_ + .8, 2.0, wz_), .08, F_TIMBER_X, 6)
        sk.tube(g, [(wx_, 2.0, wz_), (wx_, .95, wz_)], .012, F_ROPE, 3)
    # sheds against the wall's back, barrels by the smithy
    for (x, w_) in ((-14.0, 4.0), (9.5, 3.6)):
        sk.box(g, x - w_ / 2, x + w_ / 2, 0, 2.4, ZB - 2.4, ZB, F_PLANK, T, bevel=.02, cell=0, skip=('-y', '+z'))
        _quad(g, [(x - w_ / 2 - .2, 2.4, ZB - 2.6), (x + w_ / 2 + .2, 2.4, ZB - 2.6), (x + w_ / 2 + .2, 3.2, ZB), (x - w_ / 2 - .2, 3.2, ZB)],
              F_ROOF, (0, 1, -1))
    for k in range((5, 3, 1)[T.i]):
        bx, bz = sx_ - 2.8 + float(r.uniform(-.8, .8)), sz_ + float(r.uniform(-.4, 2.0))
        sk.lathe(g, [(.3, 0), (.36, .45), (.3, .9), (0, .9)], (bx, 0, bz), F_TIMBER, (8, 6, 4)[T.i], smooth=T.i < 2)
    # the ward's far curtain, crenellated, a round corner tower on it
    zc = -38.0
    sk.grid_face(g, (-40, 0, zc), (80, 0, 0), (0, 7.0, 0), 0, 0, F_ASHLAR_DK, (0, 0, 1), (10.0, 30.0, 99.0)[T.i])
    x = -40.0
    while x < 40 and T.i < 2:
        sk.box(g, x, x + 1.0, 7.0, 7.8, zc - .3, zc + .3, F_ASHLAR_DK, T, bevel=0, cell=0, skip=('-y',))
        x += 1.7
    sk.lathe(g, [(3.4, 0), (3.4, 11.0), (3.7, 11.3), (3.7, 12.1), (0, 12.1)], (-15.0, 0, zc), F_ASHLAR_DK, (14, 9, 6)[T.i], smooth=T.i < 2)
    # the inner gate's squat tower on the far curtain, its windows lit, a timber gallery round its top
    tx = 3.6
    sk.box(g, tx - 2.6, tx + 2.6, 0, 10.5, zc - 1.0, zc + 2.2, F_ASHLAR_DK, T, bevel=.04, cell=(3.0, 6.0, 0)[T.i], skip=('-y',))
    for (wx, wy) in ((tx - 1.1, 4.6), (tx + 1.2, 6.2), (tx, 8.4)):
        g.face([(wx - .22, wy - .45, zc + 2.205), (wx + .22, wy - .45, zc + 2.205), (wx + .22, wy + .45, zc + 2.205), (wx - .22, wy + .45, zc + 2.205)],
               F_WINDOW, normal=(0, 0, 1))
    sk.box(g, tx - 2.9, tx + 2.9, 10.5, 11.9, zc - 1.3, zc + 2.5, F_PLANK, T, bevel=.02, cell=0, skip=('-y',))
    for sgn in (1, -1):
        _quad(g, [(tx - 3.1, 11.9, zc + .6 + sgn * 2.1), (tx + 3.1, 11.9, zc + .6 + sgn * 2.1), (tx + 3.1, 13.4, zc + .6), (tx - 3.1, 13.4, zc + .6)],
              F_ROOF, (0, 1, sgn))
    # clutter by the hall: crates, a hay cart
    if T.i < 2:
        hx, hz = HALL
        for (cx, cz, sz_, yaw) in ((hx + 4.6, hz + 3.0, .5, .3), (hx + 5.2, hz + 3.4, .42, -.2), (hx + 4.8, hz + 3.1, .38, .6)):
            obox(g, T, (cx, sz_ if sz_ < .45 else sz_ * .9, cz), (sz_, sz_ * .9 if sz_ < .45 else sz_ * .9, sz_), yaw, F_PLANK, bevel=.02)
        obox(g, T, (hx + 1.5, .55, hz + 3.6), (1.0, .08, .6), .15, F_PLANK, bevel=.01)
        mk.rock(g, (hx + 1.5, .55, hz + 3.6), (.9, .45, .55), F_ROPE, T, seed=77, rough=.25, level=(1, 0)[T.i], turn=.15)
        for sx in (-1, 1):
            sk.lathe(g, [(.04, -.05), (.45, -.05), (.45, .05), (.04, .05)], (hx + 1.5 + sx * .2, .45, hz + 3.6 + sx * .66), F_TIMBER, 10,
                     axis=(sx * .15, 0, 1), smooth=False)


def hoardings(g, T):
    """Timber hoardings on the wall top over the pilot's left: brackets out through the parapet carry a floor, a plank
    screen and a pent roof over the field; burnt and broken at the end toward the breach."""
    r = mk.rng(47)
    xa, xb = HOARD_X
    z0, z1 = ZF + .28, ZF + 1.45
    yf = WALK_Y + .35
    yt = MERLON_Y + .65
    # brackets under the floor
    x = xa + .3
    while x < xb:
        sk.box(g, x - .09, x + .09, yf - .26, yf - .02, ZF - .2, z1 + .1, F_TIMBER_Z, T, bevel=.01 if T.i == 0 else 0, cell=0, skip=('-z',))
        if T.i < 2:
            sk.cylinder(g, (x, yf - .2, z1 - .1), (x, CORBEL_Y - .6, ZF + .02), .055, F_TIMBER, 4)
        x += 1.55
    # the floor and the screen: planks, the first bay charred and broken
    sk.box(g, xa + .9, xb, yf - .02, yf + .05, z0, z1, F_PLANK, T, bevel=0, cell=0)
    k = (12, 6, 3)[T.i]
    for i in range(k):
        u0 = xa + (xb - xa) * i / k
        u1 = xa + (xb - xa) * (i + 1) / k
        top = yt - (float(r.uniform(.5, 1.4)) if i < 2 else 0.0)
        f = F_CHARRED if i < 3 else F_PLANK
        sk.box(g, u0 + .01, u1 - .01, yf, top, z1 - .06, z1, f, T, bevel=0, cell=0, skip=('-y',))
        if T.i == 0 and i >= 2 and i % 2:
            um = (u0 + u1) / 2
            g.face([(um - .07, yf + .7, z1 + .004), (um + .07, yf + .7, z1 + .004), (um + .07, yf + 1.2, z1 + .004), (um - .07, yf + 1.2, z1 + .004)],
                   F_SLIT, normal=(0, 0, 1))
    # the pent roof (its first bay gone)
    _quad(g, [(xa + 1.8, yt, z1 + .25), (xb + .2, yt, z1 + .25), (xb + .2, yt + .75, ZF - .45), (xa + 1.8, yt + .75, ZF - .45)], F_ROOF, (0, 1, 1))
    _quad(g, [(xa + 1.8, yt, z1 + .25), (xa + 1.8, yt + .75, ZF - .45), (xb + .2, yt + .75, ZF - .45), (xb + .2, yt, z1 + .25)], F_TIMBER_X, (0, -1, -1))
    for xe in (xb, xa + 1.8):
        g.face([(xe, yf, z1), (xe, yt, z1), (xe, yt + .7, ZF - .4), (xe, yf, ZF - .4)], F_PLANK if xe == xb else F_CHARRED,
               normal=(1 if xe == xb else -1, 0, 0))


def siege_tower(g, T, at, yaw):
    """A burnt siege tower that never reached the wall: its charred corner posts and floors, half its frame fallen."""
    x, z = at
    y = ground_y(x, z)
    t = mk.Geo()
    W, H = 3.0, 6.4
    for (px, pz, h) in ((-1, -1, H), (1, -1, H * .62), (-1, 1, H * .8), (1, 1, H * .45)):
        sk.box(t, px * W / 2 - .14, px * W / 2 + .14, 0, h, pz * W / 2 - .14, pz * W / 2 + .14, F_CHARRED, T, bevel=.02 if T.i == 0 else 0, cell=0)
    for fy in (1.8, 3.6):
        sk.box(t, -W / 2, W / 2, fy, fy + .12, -W / 2, W / 2 - (.9 if fy > 3 else 0), F_CHARRED, T, bevel=0, cell=0)
    for (a0, a1) in (((-W / 2, 0, -W / 2), (W / 2, 3.6, -W / 2)), ((-W / 2, 3.6, -W / 2), (W / 2, 1.8, -W / 2)),
                     ((-W / 2, 0, W / 2), (-W / 2, 3.6, -W / 2))):
        sk.cylinder(t, a0, a1, .07, F_CHARRED, 4)
    if T.i < 2:
        for k in range(5):
            u = -W / 2 + .3 + k * .6
            sk.box(t, u - .12, u + .12, .3, 1.7 - .2 * (k % 2), W / 2 + .14, W / 2 + .2, F_CHARRED if k % 2 else F_PLANK, T, bevel=0, cell=0)
        for k in range(4):
            sk.lathe(t, [(.06, -.08), (.5, -.08), (.5, .08), (.06, .08)], (-W / 2 - .1 if k < 2 else W / 2 + .1, .5, -.9 + 1.8 * (k % 2)), F_TIMBER, 8,
                     axis=(1, 0, 0), smooth=False)
    g.extend(t.transformed(mul(mk.translate(x, y - .05, z), rot(0, 1, 0, yaw), rot(0, 0, 1, .06))))


def ram(g, T, at, yaw):
    """The ram's shed (a "cat") abandoned in the field: a ridged roof of planks on a wheeled frame, the iron-capped log in it."""
    x, z = at
    y = ground_y(x, z)
    t = mk.Geo()
    L, W = 4.2, 1.8
    for sx in (-1, 1):
        sk.box(t, sx * W / 2 - .1, sx * W / 2 + .1, .4, .6, -L / 2, L / 2, F_TIMBER_Z, T, bevel=.01 if T.i == 0 else 0, cell=0)
        for k in range(3):
            zz = -L / 2 + .2 + k * (L - .4) / 2
            sk.box(t, sx * W / 2 - .08, sx * W / 2 + .08, .6, 1.9, zz - .08, zz + .08, F_TIMBER, T, bevel=0, cell=0)
            if T.i < 2 and k != 1:
                sk.lathe(t, [(.05, -.07), (.38, -.07), (.38, .07), (.05, .07)], (sx * (W / 2 + .16), .38, zz), F_TIMBER, 8, axis=(1, 0, 0), smooth=False)
    for sgn in (1, -1):
        _quad(t, [(sgn * (W / 2 + .3), 1.6, -L / 2 - .1), (sgn * (W / 2 + .3), 1.6, L / 2 + .1), (0, 2.6, L / 2 + .1), (0, 2.6, -L / 2 - .1)],
              F_PLANK, (sgn, 1, 0))
    sk.cylinder(t, (0, 1.05, -L / 2 - .7), (0, 1.05, L / 2 - .2), .22, F_TIMBER_Z, (8, 6, 4)[T.i])
    sk.cylinder(t, (0, 1.05, -L / 2 - .95), (0, 1.05, -L / 2 - .7), .25, F_IRON, (8, 6, 4)[T.i], r1=.22)
    g.extend(t.transformed(mul(mk.translate(x, y - .05, z), rot(0, 1, 0, yaw), rot(1, 0, 0, -.04))))


def buttresses(g, T):
    """Shallow buttresses on the wall's face, stepped back twice up its height."""
    for x in BUTTRESS_X:
        for (y0, y1, out) in ((0, 2.2, .75), (2.2, 3.9, .5), (3.9, CORBEL_Y, .28)):
            sk.box(g, x - .55, x + .55, y0, y1, ZF - .1, ZF + out, F_ASHLAR_DK, T, bevel=.04, cell=0, skip=('-y', '-z'))
            _quad(g, [(x - .55, y1, ZF + out), (x + .55, y1, ZF + out), (x + .55, y1 + .18, ZF + out - .22), (x - .55, y1 + .18, ZF + out - .22)],
                  F_ASHLAR_DK, (0, 1, 1))


def field_extras(g, T):
    """Fires still burning in the craters, dropped shields and spears."""
    r = mk.rng(53)
    for (x, z) in FIRES:
        patch(g, T, x, z, .5, F_COALS, r, lift=.1, sides=(7, 5, 4)[T.i])
        if T.i < 2:
            for k in range(3):
                a = float(r.uniform(0, 6.28))
                p0 = (x + math.cos(a) * .45, ground_y(x, z) + .1, z + math.sin(a) * .45)
                p1 = (x - math.cos(a) * .2, ground_y(x, z) + .32, z - math.sin(a) * .2)
                sk.cylinder(g, p0, p1, .04, F_CHARRED, 3)
    if T.i < 2:
        for k, (x, z, kind) in enumerate(((4.4, 7.6, 0), (-3.2, 8.4, 1), (6.6, 12.6, 0), (-6.6, 10.6, 1), (2.6, 9.2, 2), (-1.6, 13.4, 2))):
            y = ground_y(x, z)
            a = float(r.uniform(0, 6.28))
            if kind == 0:
                t = mk.Geo()
                sk.lathe(t, [(.0, .0), (.32, 0), (.34, .03), (.0, .07)], (0, 0, 0), F_PLANK, (10, 7)[T.i], smooth=False)
                sk.lathe(t, [(.0, .07), (.08, .07), (.0, .1)], (0, 0, 0), F_IRON, 6)
                g.extend(t.transformed(mul(mk.translate(x, y + .05, z), rot(0, 1, 0, a), rot(1, 0, 0, .12))))
            elif kind == 1:
                obox(g, T, (x, y + .04, z), (.24, .03, .38), a, F_BANNER2, bevel=.01, pitch=.1)
            else:
                sk.cylinder(g, (x, y + .03, z), (x + math.cos(a) * 2.1, y + .05, z + math.sin(a) * 2.1), .02, F_SHAFT, 3)


# ---------------------------------------------------------------------------------------------------------------------------
# Far: the valley's far ridge and the trebuchets on it
# ---------------------------------------------------------------------------------------------------------------------------
def trebuchet(g, T, at, yaw, s):
    """A trebuchet in silhouette: its A-frames, the throwing arm cocked, the counterweight box."""
    x, y, z = at
    t = mk.Geo()
    for sx in (-1.4, 1.4):
        for (a, b) in (((-3.2, 0, 0), (0, 6.5, 0)), ((3.2, 0, 0), (0, 6.5, 0)), ((-3.6, .3, 0), (3.6, .3, 0))):
            sk.cylinder(t, (a[0], a[1], sx), (b[0], b[1], sx), .22, F_ENGINE, (4, 4, 3)[T.i], caps=False)
    sk.cylinder(t, (0, 6.5, -1.6), (0, 6.5, 1.6), .2, F_ENGINE, 4, caps=False)
    sk.cylinder(t, (-6.5, 9.8, 0), (2.6, 4.7, 0), .2, F_ENGINE, 4, caps=False)
    sk.box(t, 1.9, 4.0, 1.2, 3.6, -1.0, 1.0, F_ENGINE, T, bevel=0, cell=0)
    sk.tube(t, [(-6.5, 9.8, 0), (-6.2, 7.0, 0)], .05, F_ENGINE, 3)
    g.extend(t.transformed(mul(mk.translate(x, y, z), rot(0, 1, 0, yaw), [s, 0, 0, 0, 0, s, 0, 0, 0, 0, s, 0])))


def far(g, T):
    """The valley's far side: a ridge of hills (sloped, not a wall) with the three trebuchets standing on its crest."""
    n = (28, 18, 10)[T.i]
    span = 140.0
    xs = [-span + 2 * span * i / n for i in range(n + 1)]
    hs, zz = [], []
    for x in xs:
        a = float(sk.fbm(np.array([[x * .02, 0, 3.7]]), 1.0, 3, 5)[0])
        hs.append(3.0 + 13.0 * max(0.0, a - .25) + 2.0 * math.sin(x * .07))
        zz.append(56.0 + 8 * math.sin(x * .035 + 1.0))
    for i in range(n):
        for (za, zb, k) in ((0.0, 16.0, 0), (16.0, 40.0, 1)):
            pa0 = (xs[i], -4.6 if k == 0 else hs[i], zz[i] + za)
            pb0 = (xs[i + 1], -4.6 if k == 0 else hs[i + 1], zz[i + 1] + za)
            pb1 = (xs[i + 1], hs[i + 1] if k == 0 else hs[i + 1] * .55, zz[i + 1] + zb)
            pa1 = (xs[i], hs[i] if k == 0 else hs[i] * .55, zz[i] + zb)
            _quad(g, [pa0, pb0, pb1, pa1], F_FAR if k == 0 else F_FAR2, (0, 1, -1) if k == 0 else (0, 1, 0))
    # the valley floor up to the ridge's foot
    g.face(((-span, -4.3, 48.0), (span, -4.3, 48.0), (span, -4.6, 64.0), (-span, -4.6, 64.0)), F_FAR2, normal=(0, 1, 0))
    for (x, yaw) in ((-14.0, .2), (9.0, -.15), (26.0, -.35)):
        i = min(n - 1, max(0, int((x + span) / (2 * span) * n)))
        u = (x - xs[i]) / (xs[i + 1] - xs[i])
        y = hs[i] + (hs[i + 1] - hs[i]) * u - .4
        trebuchet(g, T, (x, y, zz[i] + 16.0), yaw, 1.0)


# ---------------------------------------------------------------------------------------------------------------------------
# The set
# ---------------------------------------------------------------------------------------------------------------------------
def build(T):
    s = mk.SetPiece('breach')
    g = mk.Geo()
    tl = mk.Tally(g)
    ground(g, T)
    tl('ground')
    wall(g, T)
    buttresses(g, T)
    tl('wall')
    rubble(g, T)
    tl('rubble')
    gatehouse(g, T)
    tl('gatehouse')
    field(g, T)
    field_extras(g, T)
    siege_tower(g, T, (-7.4, 16.6), .5)
    ram(g, T, (5.6, 13.2), -.35)
    tl('field')
    hoardings(g, T)
    tl('hoardings')
    banners(g, T)
    for at in BRAZIERS:
        brazier(g, T, at)
    tl('banners+braziers')
    ward(g, T)
    tl('ward')
    far(g, T)
    tl('far')
    s.tally = tl
    s.static[T.i] = g
    rigs = []
    for k, (at, size, seed) in enumerate(DEBRIS):
        geo = mk.Geo()
        mk.rock(geo, (0.0, 0.0, 0.0), size, F_BLOCK_LT if k % 2 else F_BLOCK, T, seed=seed, rough=.26, flat_bottom=False,
                level=(1, 1, 0)[T.i], turn=k * 1.3)
        s.rig('debris-%d' % k, T.i, geo, mk.translate(*at))
        rigs.append(geo)
    s.views = [
        ('front', (0.0, 1.6, 6.5), (0.0, 1.8, -2.0), 48),
        ('behind', (0.8, 1.8, -3.2), (0.0, 1.2, 20.0), 52),
        ('low-side', (9.8, .7, 5.4), (-3.0, 2.6, -1.4), 56),
        ('gate', (-2.4, 1.7, 8.0), (-7.8, 3.2, 0.0), 54),
        ('wide', (7.0, 8.0, 17.0), (-1.5, 2.5, -2.0), 60),
        ('ward', (.4, 1.9, 3.0), (2.0, 4.5, -18.0), 50),
    ]
    if os.environ.get('RIFT_VSTATS'):
        vstats(g, tl, rigs, T)
    return s


def vstats(g, tl, rig_geos, T):
    """Diagnostics (RIFT_VSTATS=1): each section's vertices once corners that agree merge (position, normal, finish)."""
    P, N, S, F = g.arrays()
    fc = np.repeat(F, S)
    key = np.concatenate([np.round(P * 2000).astype(np.int64), np.round(N * 200).astype(np.int64), fc[:, None]], axis=1)
    at = 0
    rows = []
    for name, n in tl.rows:
        k = key[at:at + n]
        rows.append((name, len(np.unique(k, axis=0)) if n else 0))
        at += n
    rv = 0
    for rg in rig_geos:
        P2, N2, S2, F2 = rg.arrays()
        k2 = np.concatenate([np.round(P2 * 2000).astype(np.int64), np.round(N2 * 200).astype(np.int64), np.repeat(F2, S2)[:, None]],
                            axis=1)
        rv += len(np.unique(k2, axis=0))
    rows.append(('rigs', rv))
    print('breach %s vertices: %s; total %d' % (T.name, ', '.join('%s %d' % r for r in sorted(rows, key=lambda r: -r[1])),
                                                sum(r[1] for r in rows)))
