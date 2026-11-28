"""Faultline's set (the greatsword; docs/INTROS-MELEE.md): a basalt quarry at night, the weight that cracks the ground.

The pit's floor is saw-cut basalt slabs with chisel scars and dusty joints (two lifted out, two left standing) out to the
stepped quarry walls: five benches (2.3-2.7 m high, set back 1.6-2.0 m) of columnar basalt (polygonal columns with slanted
joints, broken along each bench's edge in notches and slopes, their tops broken in ridges, cross-cracked, leaning and tapering a
little, scree piled at their feet) rise behind the pilot (-z) and on both sides to 12.5 m; behind the pilot the first two
benches are narrow organ-pipe columns, the first (the working face) with drill holes' half casts down them. The side walls
swing in at shoulders to the open front (+z), where a low broken ledge of columns and a ramp road lead out past the quarry hut,
blocks waiting to be carted, a timber tripod hoisting a block over the road and spoil heaps to a far ridge. The back wall
comes forward at a buttress on the pilot's left (x > 4.4); up the inside corner between them (x 4.4, z -9.6) climbs a molten
seam (a jagged crack glowing in a slot between the columns, stones jammed in it, the glow seeping through the columns'
joints, a fissure over each bench's top), and a fault runs from its foot across the floor to the fracture disc; two more
fissures smoulder in the far floor (glowing inset cracks between sagging broken slabs).

The fracture disc (the hero): the floor within about 4.6 m of the origin is fourteen rig slabs `slab-0..13`: Voronoi cells of
jittered points (one at the origin, five round it at about 2 m, eight at about 3.6 m; the cracked collar's points bound them),
shrunk by 1.5 cm, 0.35 m thick with a rough underside, top at y 0, each in its own frame centred on its cell's centroid at y 0;
`slab-0` is the centre cell the pilot stands on. Under them a static glowing seam at y -0.06 (dark crust islands float on it,
inside the slabs at rest, so the molten bed only shows when they lift). Props: a guyed timber derrick with its boom, hoist,
hook block, hook and chain by the back wall on the pilot's right, cut blocks (a stack on skids, one tipped on a chock, one split
by iron wedges), a derailed mine cart by its broken spur rail, a ladder up the first bench, work lanterns, rubble. Set frame:
melee_kit's (the pilot at the origin facing +z; -x is the opening camera's right)."""
import math
import os

import numpy as np

import melee_kit as mk
from melee_kit import sk

# ---------------------------------------------------------------------------------------------------------------------------
# Finishes
# ---------------------------------------------------------------------------------------------------------------------------
F_SLAB = mk.fin('rift.slab', 0x4a4d52, sk.DETAIL_CARVED, smooth=.42, dirt=.85, wear=.35)
F_SLAB_DK = mk.fin('rift.slab.dark', 0x3d4045, sk.DETAIL_CARVED, smooth=.45, dirt=.9, wear=.3)
F_SLAB_LT = mk.fin('rift.slab.light', 0x56575b, sk.DETAIL_CARVED, smooth=.38, dirt=.8, wear=.4)
F_FRACTURE = mk.fin('rift.fracture', 0x525459, sk.DETAIL_CARVED, smooth=.3, dirt=.5, wear=.25)
F_UNDER = mk.fin('rift.underside', 0x46403b, sk.DETAIL_CARVED, smooth=.25, dirt=.4)
F_GRIT = mk.fin('rift.grit', 0x5a554e, smooth=.12, dirt=.6)
F_JOINTLINE = mk.fin('rift.jointline', 0x37342f, smooth=.15, dirt=.3)
F_SCAR = mk.fin('rift.scar', 0x6a6b6d, smooth=.3, dirt=.35)
F_COL = mk.fin('rift.column', 0x484a4f, sk.DETAIL_CARVED, smooth=.32, dirt=1.0, moss=.08, wear=.35)
F_COL_DK = mk.fin('rift.column.dark', 0x404247, sk.DETAIL_CARVED, smooth=.3, dirt=1.1, moss=.12, wear=.3)
F_COL_LT = mk.fin('rift.column.light', 0x505257, sk.DETAIL_CARVED, smooth=.3, dirt=.9, moss=.05, wear=.4)
F_CUT = mk.fin('rift.cut', 0x56585c, sk.DETAIL_CARVED, smooth=.4, dirt=.7, wear=.3)
F_RECESS = mk.fin('rift.recess', 0x303134, sk.DETAIL_CARVED, smooth=.2, dirt=1.0)
F_DRILL = mk.fin('rift.drill', 0x2e2f32, smooth=.2, dirt=.3)
F_BENCH = mk.fin('rift.bench', 0x4a4640, smooth=.15, dirt=1.0, moss=.3)
F_SCREE = mk.fin('rift.scree', 0x504d48, sk.DETAIL_CARVED, smooth=.25, dirt=.9, moss=.15)
F_RUBBLE = mk.fin('rift.rubble', 0x505256, sk.DETAIL_CARVED, smooth=.3, dirt=.9, wear=.3)
F_MAGMA = mk.fin('rift.magma.glow', 0xff6a1c, glow=3.0)
F_FISSURE = mk.fin('rift.fissure.glow', 0xff6a1c, glow=3.0)
F_SEAM = mk.fin('rift.seam.glow', 0xff6a1c, glow=2.5)
F_EMBER = mk.fin('rift.ember.glow', 0xff5a14, glow=2.2)
F_CHAR = mk.fin('rift.char', 0x302a26, smooth=.2, dirt=.3)
F_CRUST = mk.fin('rift.crust', 0x3a322d, sk.DETAIL_CARVED, smooth=.3, dirt=.2)
F_TIMBER = mk.fin('rift.timber', 0x5b4634, sk.DETAIL_WOOD, smooth=.3, dirt=.9, param=1, wear=.3)
F_TIMBER_X = mk.fin('rift.timber.x', 0x5b4634, sk.DETAIL_WOOD, smooth=.3, dirt=.9, param=0, wear=.3)
F_TIMBER_Z = mk.fin('rift.timber.z', 0x5b4634, sk.DETAIL_WOOD, smooth=.3, dirt=.9, param=2, wear=.3)
F_PLANK = mk.fin('rift.plank', 0x6b5640, sk.DETAIL_WOOD, smooth=.3, dirt=.9, param=0, wear=.3)
F_IRON = mk.fin('rift.iron', 0x3b3836, sk.DETAIL_IRON, smooth=.4, metal=.6, dirt=.7)
F_RUST = mk.fin('rift.rust', 0x6b4532, sk.DETAIL_IRON, smooth=.3, metal=.35, dirt=.9, wear=.3)
F_ROPE = mk.fin('rift.rope', 0x76664c, sk.DETAIL_CLOTH, smooth=.15, dirt=.6)
F_LAMP = mk.fin('rift.lamp.glow', 0xffb35c, glow=2.4)
F_WINDOW = mk.fin('rift.window.glow', 0xffc27a, glow=1.3)
F_ROAD = mk.fin('rift.road', 0x524c45, smooth=.15, dirt=1.0, moss=.1)
F_RUT = mk.fin('rift.rut', 0x3d3935, smooth=.3, dirt=.8)
F_GROUND = mk.fin('rift.ground', 0x46443e, smooth=.12, dirt=1.0, moss=.35)
F_SPOIL = mk.fin('rift.spoil', 0x55534e, sk.DETAIL_CARVED, smooth=.2, dirt=.9)
F_ROOF = mk.fin('rift.roof', 0x3a3a3c, sk.DETAIL_IRON, smooth=.35, metal=.3, dirt=1.0)
F_FAR = mk.fin('rift.far', 0x1a1c20, smooth=.1, dirt=0)
F_FAR2 = mk.fin('rift.far.near', 0x23252a, smooth=.1, dirt=0)
F_PINE = mk.fin('rift.pine', 0x1f2822, smooth=.2, dirt=0)

SLAB_FINS = (F_SLAB, F_SLAB, F_SLAB_DK, F_SLAB_LT, F_SLAB, F_SLAB_DK)
COL_FINS = (F_COL, F_COL, F_COL_DK, F_COL_LT)

# ---------------------------------------------------------------------------------------------------------------------------
# The plan (set frame, metres)
# ---------------------------------------------------------------------------------------------------------------------------
SX = 10.6            # the side walls' faces at the floor (|x|)
BZ = -9.6            # the back wall's face (x < JOG_X)
BZ_L = -7.4          # the buttress's face (x > JOG_X): the back wall comes forward on the pilot's left
JOG_X = 4.4          # the inside corner the molten seam climbs
RECESS = .5          # the benches' bodies stand this far behind their faces; their columns stand out of them
OUT_Y = 1.2          # the ground outside the pit (beyond the ledge)
LEDGE_Z = 11.0       # the front ledge's body
BY = (0.0, 2.6, 5.0, 7.7, 10.0, 12.5)    # the benches' feet and tops
BO = (0.0, 1.8, 3.6, 5.6, 7.2)           # each bench's setback from the first
ZS = (10.6, 8.2, 5.4, 2.6, -.4)          # each bench's front end (the benches step down toward the mouth)
X_FAR = 24.0
ZB_FAR = -24.3
RAMP_X = (1.6, 5.4)
RAMP_Z = (6.6, 13.8)
R_COLLAR = 5.6       # the field's slabs nearer the origin than this are cracked into the collar round the disc
X_LO, X_HI = -SX - .8, SX + .8
Z_LO, Z_HI = BZ - .7, 10.8
SEAM_K = 3           # benches whose inside corner at the jog carries the seam's slot (0..SEAM_K-1); cracks fray above
BED_Y = -.07         # the grit the floor's slabs lie in (under the disc's seam, which hides it there)

# the pit's foot line (the faces at the floor), from the mouth's left end round the back to its right end, each corner tagged
B0 = (((7.4, 10.6), 'L'), ((8.9, 7.1), 'L'), ((10.6, 3.6), 'L'), ((10.6, .2), 'L'), ((10.6, -3.2), 'L'),
      ((10.6, BZ_L + 1.2), 'cL0'), ((10.6 - 1.2, BZ_L), 'cL1'), ((JOG_X, BZ_L), 'J0'), ((JOG_X, BZ), 'J1'),
      ((1.4, BZ), 'B'), ((-2.4, BZ), 'B'), ((-6.3, BZ), 'B'), ((-10.6 + 1.5, BZ), 'cR0'), ((-10.6, BZ + 1.5), 'cR1'),
      ((-10.6, -4.6), 'R'), ((-10.6, -1.0), 'R'), ((-10.6, 3.2), 'R'), ((-9.1, 6.9), 'R'), ((-7.6, 10.6), 'R'))

# the faults: glowing inset fissures from the disc's edge (polylines in plan)
FAULTS = (
    ((1.72, -4.25), (2.15, -5.25), (2.55, -6.1), (2.95, -6.95), (3.55, -7.75), (3.9, -8.6), (4.45, -9.6)),
    ((-1.9, -4.2), (-2.55, -5.4), (-2.45, -6.6), (-3.15, -7.6), (-3.6, -8.7), (-4.05, -9.45)),
    ((-4.15, 2.0), (-5.3, 2.45), (-6.2, 3.3), (-7.4, 3.6), (-8.3, 4.5)),
)
PITS = ((8.0, 4.4), (-7.9, -2.4))        # slabs lifted out (their beds lie lower)
RAISED = ((9.6, -1.0), (-9.5, 1.4))      # uncut blocks left standing
DERRICK = (-5.6, -7.3)
RAIL = ((-3.4, 8.0), (-8.6, 7.6))
CART = (-6.3, 6.9)


# ---------------------------------------------------------------------------------------------------------------------------
# Plan geometry helpers (convex polygons [(x, z)])
# ---------------------------------------------------------------------------------------------------------------------------
def _area(poly):
    s = 0.0
    n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        s += a[0] * b[1] - b[0] * a[1]
    return s * .5


def _centroid(poly):
    a = _area(poly)
    if abs(a) < 1e-9:
        return (sum(p[0] for p in poly) / len(poly), sum(p[1] for p in poly) / len(poly))
    cx = cz = 0.0
    n = len(poly)
    for i in range(n):
        p, q = poly[i], poly[(i + 1) % n]
        c = p[0] * q[1] - q[0] * p[1]
        cx += (p[0] + q[0]) * c
        cz += (p[1] + q[1]) * c
    return (cx / (6 * a), cz / (6 * a))


def _ccw(poly):
    return list(poly) if _area(poly) > 0 else list(poly)[::-1]


def _clean(poly, eps=.004):
    out = []
    for p in poly:
        if not out or math.hypot(p[0] - out[-1][0], p[1] - out[-1][1]) > eps:
            out.append((float(p[0]), float(p[1])))
    while len(out) > 2 and math.hypot(out[0][0] - out[-1][0], out[0][1] - out[-1][1]) <= eps:
        out.pop()
    return out


def _merge_short(poly, least=.05):
    """Consecutive corners closer than `least` become their midpoint (a cell's tiny edge; its neighbour does the same)."""
    pts = list(poly)
    changed = True
    while changed and len(pts) > 3:
        changed = False
        n = len(pts)
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            if math.hypot(a[0] - b[0], a[1] - b[1]) < least:
                m = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
                if i + 1 < n:
                    pts[i:i + 2] = [m]
                else:
                    pts[i] = m
                    pts.pop(0)
                changed = True
                break
    return pts


def _clip(poly, nx, nz, c):
    """The part of a convex polygon where nx x + nz z <= c."""
    out = []
    n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        da = a[0] * nx + a[1] * nz - c
        db = b[0] * nx + b[1] * nz - c
        if da <= 0:
            out.append(a)
        if (da <= 0) != (db <= 0):
            u = da / (da - db)
            out.append((a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u))
    return out


def _clip_rect(poly, r):
    x0, x1, z0, z1 = r
    p = list(poly)
    for nx, nz, c in ((1, 0, x1), (-1, 0, -x0), (0, 1, z1), (0, -1, -z0)):
        p = _clip(p, nx, nz, c)
        if len(p) < 3:
            return []
    return _clean(p)


def _clip_poly(poly, conv):
    """`poly` clipped to the counter-clockwise convex polygon `conv`."""
    p = list(poly)
    n = len(conv)
    for i in range(n):
        a, b = conv[i], conv[(i + 1) % n]
        nx, nz = b[1] - a[1], -(b[0] - a[0])
        p = _clip(p, nx, nz, nx * a[0] + nz * a[1])
        if len(p) < 3:
            return []
    return _clean(p)


def _inset(poly, ds):
    """A counter-clockwise convex polygon with each edge i moved in by ds[i] (exact, so joints are even); falls back on
    mk.shrink when a short edge would turn over."""
    pts = list(poly)
    n = len(pts)
    if not isinstance(ds, (list, tuple)):
        ds = [ds] * n
    lines = []
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        dx, dz = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dz)
        nx, nz = -dz / L, dx / L
        lines.append((nx, nz, nx * a[0] + nz * a[1] + ds[i]))
    out = []
    for i in range(n):
        (n1x, n1z, c1), (n2x, n2z, c2) = lines[i - 1], lines[i]
        det = n1x * n2z - n1z * n2x
        if abs(det) < 1e-6:
            p = pts[i]
            out.append((p[0] + n2x * ds[i], p[1] + n2z * ds[i]))
        else:
            out.append(((c1 * n2z - n1z * c2) / det, (n1x * c2 - c1 * n2x) / det))
    ok = _area(out) > 1e-4
    if ok:
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            c, d = out[i], out[(i + 1) % n]
            if (b[0] - a[0]) * (d[0] - c[0]) + (b[1] - a[1]) * (d[1] - c[1]) <= 0:
                ok = False
                break
    return out if ok else mk.shrink(pts, max(ds))


def _seg_dist(p, a, b):
    dx, dz = b[0] - a[0], b[1] - a[1]
    L2 = dx * dx + dz * dz
    t = 0.0 if L2 < 1e-12 else max(0.0, min(1.0, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dz) / L2))
    return math.hypot(p[0] - a[0] - dx * t, p[1] - a[1] - dz * t)


def _walk(L, step, r):
    """Points along a polyline about every `step` (jittered), each with its unit tangent."""
    out = []
    for i in range(len(L) - 1):
        a, b = L[i], L[i + 1]
        dx, dz = b[0] - a[0], b[1] - a[1]
        seg = math.hypot(dx, dz)
        t = (dx / seg, dz / seg)
        n = max(1, int(round(seg / step)))
        for k in range(n):
            u = (k + .5 + float(r.uniform(-.15, .15))) / n
            out.append(((a[0] + dx * u, a[1] + dz * u), t))
    return out


def _in_rect(p, R, m=0.0):
    return R[0] + m <= p[0] <= R[1] - m and R[2] + m <= p[1] <= R[3] - m


def _rect_poly(R):
    return [(R[0], R[2]), (R[1], R[2]), (R[1], R[3]), (R[0], R[3])]


def _near(R):
    dx = max(R[0], -R[1], 0.0)
    dz = max(R[2], -R[3], 0.0)
    return math.hypot(dx, dz)


def _fault_edge(a, b):
    """Whether a crack edge a..b runs along one of the faults (it then gapes and glows)."""
    m = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
    ex, ez = b[0] - a[0], b[1] - a[1]
    el = math.hypot(ex, ez)
    if el < 1e-6:
        return False
    for L in FAULTS:
        for i in range(len(L) - 1):
            p, q = L[i], L[i + 1]
            if _seg_dist(m, p, q) < .14:
                qx, qz = q[0] - p[0], q[1] - p[1]
                if abs(ex * qx + ez * qz) / (el * math.hypot(qx, qz)) > .5:
                    return True
    return False


def _on_rect(a, b, R):
    e = 1e-4
    return (abs(a[0] - R[0]) < e and abs(b[0] - R[0]) < e) or (abs(a[0] - R[1]) < e and abs(b[0] - R[1]) < e) or \
        (abs(a[1] - R[2]) < e and abs(b[1] - R[2]) < e) or (abs(a[1] - R[3]) < e and abs(b[1] - R[3]) < e)


def _pit_dist(p):
    """How far a plan point lies outside the pit's foot line (negative inside)."""
    pts = [q for q, _ in B0]
    inside = False
    d = 1e9
    for i in range(len(pts)):
        a, b = pts[i], pts[(i + 1) % len(pts)]
        d = min(d, _seg_dist(p, a, b))
        if (a[1] > p[1]) != (b[1] > p[1]):
            x = a[0] + (p[1] - a[1]) / (b[1] - a[1]) * (b[0] - a[0])
            if x > p[0]:
                inside = not inside
    return -d if inside else d


# ---------------------------------------------------------------------------------------------------------------------------
# The floor's layout (the same at both tiers)
# ---------------------------------------------------------------------------------------------------------------------------
class Plan:
    """The saw-cut slab field, the disc's cells, the cracked collar round them and the slabs the faults run through."""

    def __init__(self):
        r = mk.rng(5)
        rects = []
        z = Z_LO
        while z < Z_HI - .05:
            dz = float(r.uniform(1.5, 2.1))
            if Z_HI - (z + dz) < .9:
                dz = Z_HI - z
            x = X_LO
            first = True
            while x < X_HI - .05:
                w = float(r.uniform(.8, 2.0)) if first else float(r.uniform(1.7, 2.9))
                first = False
                if X_HI - (x + w) < 1.0:
                    w = X_HI - x
                rects.append((x, x + w, z, z + dz))
                x += w
            z += dz
        self.rects = rects
        self.fin = [SLAB_FINS[int(r.integers(len(SLAB_FINS)))] for _ in rects]
        self.removed = [i for i, R in enumerate(rects) if _near(R) < R_COLLAR]

        def hidden(R):
            pts = [(R[0], R[2]), (R[1], R[2]), (R[1], R[3]), (R[0], R[3]), ((R[0] + R[1]) / 2, (R[2] + R[3]) / 2)]
            return min(_pit_dist(p) for p in pts) > .75
        self.field = [i for i in range(len(rects)) if i not in self.removed and not hidden(rects[i])]
        self.top = {}
        for i in self.field:
            self.top[i] = float(r.uniform(-.006, .006))
        for p in PITS:
            for i in self.field:
                if _in_rect(p, rects[i]):
                    self.top[i] = -.34
        for p in RAISED:
            for i in self.field:
                if _in_rect(p, rects[i]):
                    self.top[i] = .52
        # the fracture disc and the collar: one Voronoi of the disc's points and the collar's
        disc = self.disc_seeds()
        collar = self.collar_seeds(disc)
        rem = [rects[i] for i in self.removed]
        bx0 = min(R[0] for R in rem) - .3
        bx1 = max(R[1] for R in rem) + .3
        bz0 = min(R[2] for R in rem) - .3
        bz1 = max(R[3] for R in rem) + .3
        cells = mk.voronoi_cells(disc + collar, [(bx0, bz0), (bx1, bz0), (bx1, bz1), (bx0, bz1)])
        self.disc = [_clean(_ccw(c)) for c in cells[:len(disc)]]
        reach = max(math.hypot(p[0], p[1]) for c in self.disc for p in c)
        if reach > R_COLLAR - .1:
            print('rift: the disc reaches %.2f m, past the collar' % reach)
        self.disc_reach = reach
        self.pieces = []
        for ci, cell in enumerate(cells[len(disc):]):
            for i in self.removed:
                p = _clip_rect(_ccw(cell), rects[i])
                if len(p) >= 3 and _area(p) > 1e-4:
                    self.pieces.append(self.piece(p, rects[i], self.fin[i], 0.0, r))
        # the field's slabs a fault runs through break along it
        self.broken = {}
        for i in self.field:
            R = rects[i]
            hits = []
            for L in FAULTS:
                for (p, t) in _walk(L, .6, r):
                    if _in_rect(p, R, .12):
                        hits.append((p, t))
            if not hits:
                continue
            seeds = []
            for (p, t) in hits:
                nx, nz = -t[1], t[0]
                for sgn in (-1, 1):
                    off = float(r.uniform(.24, .36))
                    j = float(r.uniform(-.1, .1))
                    q = (p[0] + sgn * nx * off + t[0] * j, p[1] + sgn * nz * off + t[1] * j)
                    if _in_rect(q, R, .04):
                        seeds.append(q)
            for _ in range(60):
                if len(seeds) >= len(hits) * 2 + 2:
                    break
                q = (float(r.uniform(R[0] + .2, R[1] - .2)), float(r.uniform(R[2] + .2, R[3] - .2)))
                if min(math.hypot(q[0] - s[0], q[1] - s[1]) for s in seeds) > .75:
                    seeds.append(q)
            pcs = []
            for c in mk.voronoi_cells(seeds, _rect_poly(R)):
                c = _clean(_ccw(c))
                if len(c) >= 3 and _area(c) > 1e-4:
                    pcs.append(self.piece(c, R, self.fin[i], self.top[i], r))
            self.broken[i] = pcs

    @staticmethod
    def disc_seeds():
        r = mk.rng(23)
        pts = [(0.0, 0.0)]
        a0 = float(r.uniform(0, 2 * math.pi))
        for k in range(5):
            a = a0 + 2 * math.pi * k / 5 + float(r.uniform(-.2, .2))
            d = float(r.uniform(1.85, 2.15))
            pts.append((d * math.cos(a), d * math.sin(a)))
        a1 = a0 + math.pi / 8
        for k in range(8):
            a = a1 + 2 * math.pi * k / 8 + float(r.uniform(-.14, .14))
            d = float(r.uniform(3.55, 3.85))
            pts.append((d * math.cos(a), d * math.sin(a)))
        return pts

    def in_removed(self, p, m=0.0):
        return any(_in_rect(p, self.rects[i], m) for i in self.removed)

    def collar_seeds(self, disc):
        r = mk.rng(29)
        pts = []
        for L in FAULTS:
            for (p, t) in _walk(L, .62, r):
                if math.hypot(p[0], p[1]) < 5.15 or not self.in_removed(p):
                    continue
                nx, nz = -t[1], t[0]
                for sgn in (-1, 1):
                    off = float(r.uniform(.24, .36))
                    j = float(r.uniform(-.1, .1))
                    pts.append((p[0] + sgn * nx * off + t[0] * j, p[1] + sgn * nz * off + t[1] * j))
        for k in range(16):
            a = 2 * math.pi * k / 16 + float(r.uniform(-.1, .1))
            d = float(r.uniform(5.3, 5.5))
            q = (d * math.cos(a), d * math.sin(a))
            if all(math.hypot(q[0] - p[0], q[1] - p[1]) > .42 for p in pts):
                pts.append(q)
        rem = [self.rects[i] for i in self.removed]
        x0, x1 = min(R[0] for R in rem), max(R[1] for R in rem)
        z0, z1 = min(R[2] for R in rem), max(R[3] for R in rem)
        for _ in range(900):
            q = (float(r.uniform(x0, x1)), float(r.uniform(z0, z1)))
            if math.hypot(q[0], q[1]) < 6.1 or not self.in_removed(q):
                continue
            if all(math.hypot(q[0] - p[0], q[1] - p[1]) > 1.45 for p in pts + disc):
                pts.append(q)
        return pts

    @staticmethod
    def piece(poly, R, f, top, r):
        """A cracked slab's piece: its joints (saw joints on its slab's edges, hairline cracks inside, the fault gaping), the
        faces that glow (along a fault), which side faces show, and how it sags toward the fault."""
        p = _merge_short(_ccw(poly))
        n = len(p)
        ds, glow, side = [], [], []
        for i in range(n):
            a, b = p[i], p[(i + 1) % n]
            if _on_rect(a, b, R):
                ds.append(.015)
                glow.append(False)
                side.append(True)
            elif _fault_edge(a, b):
                ds.append(float(r.uniform(.03, .042)))
                glow.append(True)
                side.append(True)
            else:
                ds.append(.006)
                glow.append(False)
                side.append(False)
        cut = _inset(p, ds)
        if len(cut) != n:
            glow = [False] * len(cut)
            side = [True] * len(cut)
        tilt = None
        if any(glow):
            # sag toward the fault: the edge along it drops a little
            fx = sum((p[i][0] + p[(i + 1) % n][0]) / 2 for i in range(n) if glow[i]) / sum(glow)
            fz = sum((p[i][1] + p[(i + 1) % n][1]) / 2 for i in range(n) if glow[i]) / sum(glow)
            cx, cz = _centroid(p)
            vx, vz = fx - cx, fz - cz
            vl = math.hypot(vx, vz)
            if vl > .05:
                tilt = ((vz / vl, 0.0, -vx / vl), math.atan(float(r.uniform(.012, .022)) / vl))
        return dict(poly=p, cut=cut, glow=glow, side=side, fin=f, top=top + float(r.uniform(-.006, .002)), tilt=tilt,
                    rect=R)


_PLAN = []


def plan():
    if not _PLAN:
        _PLAN.append(Plan())
    return _PLAN[0]


# ---------------------------------------------------------------------------------------------------------------------------
# Shape helpers
# ---------------------------------------------------------------------------------------------------------------------------
def _m4(m):
    a = np.eye(4)
    a[:3, :] = np.array(m, dtype=np.float64).reshape(3, 4)
    return a


def mul(*ms):
    """The product of 3x4 affines (the last one applies first)."""
    a = np.eye(4)
    for m in ms:
        a = a @ _m4(m)
    return [float(v) for v in a[:3, :].reshape(-1)]


def rot(ax, ay, az, ang, at=(0.0, 0.0, 0.0)):
    return mk.about(at, sk.quat_axis(ax, ay, az, ang))


def prism(g, poly, y1, y0, f, T, bevel=.025, side_f=None, glow=None, keel=0.0, bottom=False, top_f=None, under_f=None,
          sides=True):
    """A prism over the counter-clockwise convex polygon `poly` [(x, z)] from y0 up to y1, its top edge chamfered by `bevel`
    (Fine). glow: per edge, whether its side face glows below a charred lip (a fault's gaping crack); sides: all, none or per
    edge; keel: the underside pulled down to a point under the centre (a slab torn from its bed); bottom: a flat underside."""
    pts = list(poly)
    n = len(pts)
    if n < 3 or _area(pts) < 2e-4:
        return
    b = bevel if T.bevel else 0.0
    inner = _inset(pts, b) if b > 0 else pts
    if len(inner) != n:
        b, inner = 0.0, pts
    tf = f if top_f is None else top_f
    yt = y1 - b
    g.face([(p[0], y1, p[1]) for p in inner], tf, normal=(0, 1, 0))
    for i in range(n):
        j = (i + 1) % n
        a0, a1 = pts[i], pts[j]
        dx, dz = a1[0] - a0[0], a1[1] - a0[1]
        L = math.hypot(dx, dz)
        if L < 1e-6:
            continue
        nrm = (dz / L, 0.0, -dx / L)
        if b > 0:
            i0, i1 = inner[i], inner[j]
            g.face(((a0[0], yt, a0[1]), (a1[0], yt, a1[1]), (i1[0], y1, i1[1]), (i0[0], y1, i0[1])), f,
                   normals=(nrm, nrm, (0, 1, 0), (0, 1, 0)))
        if sides is False or (isinstance(sides, (list, tuple)) and not sides[i]):
            continue
        if glow is not None and glow[i]:
            ym = yt - .022
            g.face(((a0[0], ym, a0[1]), (a1[0], ym, a1[1]), (a1[0], yt, a1[1]), (a0[0], yt, a0[1])), F_CHAR, normal=nrm)
            g.face(((a0[0], y0, a0[1]), (a1[0], y0, a1[1]), (a1[0], ym, a1[1]), (a0[0], ym, a0[1])), F_FISSURE, normal=nrm)
        else:
            g.face(((a0[0], y0, a0[1]), (a1[0], y0, a1[1]), (a1[0], yt, a1[1]), (a0[0], yt, a0[1])), side_f or f, normal=nrm)
    uf = under_f or side_f or f
    if keel > 0:
        cx, cz = _centroid(pts)
        c = (cx, y0 - keel, cz)
        for i in range(n):
            a0, a1 = pts[i], pts[(i + 1) % n]
            pa, pb = (a0[0], y0, a0[1]), (a1[0], y0, a1[1])
            nn = sk._norm(sk._cross(sk._sub(pb, pa), sk._sub(c, pa)))
            if nn[1] > 0:
                nn = (-nn[0], -nn[1], -nn[2])
            g.face((pa, pb, c), uf, normal=nn)
    elif bottom:
        g.face([(p[0], y0, p[1]) for p in pts], uf, normal=(0, -1, 0))


def strip(g, a, b, w0, w1, y, f, clip_to=None):
    """A flat strip lying on a floor at height y from plan point a to b, w0..w1 wide, clipped to a convex polygon."""
    dx, dz = b[0] - a[0], b[1] - a[1]
    L = math.hypot(dx, dz)
    if L < 1e-6:
        return
    nx, nz = -dz / L, dx / L
    q = [(a[0] - nx * w0 / 2, a[1] - nz * w0 / 2), (b[0] - nx * w1 / 2, b[1] - nz * w1 / 2),
         (b[0] + nx * w1 / 2, b[1] + nz * w1 / 2), (a[0] + nx * w0 / 2, a[1] + nz * w0 / 2)]
    q = _ccw(q)
    if clip_to is not None:
        q = _clip_poly(q, clip_to)
    if len(q) >= 3 and abs(_area(q)) > 1e-5:
        g.face([(p[0], y, p[1]) for p in q], f, normal=(0, 1, 0))


def scars(g, T, poly, y, r, groups=1):
    """Chisel scars on a slab's top: short parallel strokes, a little paler than the stone."""
    if T.i > 0:
        return
    cx, cz = _centroid(poly)
    for _ in range(groups):
        ox = cx + float(r.uniform(-.35, .35))
        oz = cz + float(r.uniform(-.35, .35))
        a = float(r.uniform(0, math.pi))
        d = (math.cos(a), math.sin(a))
        s = (-d[1], d[0])
        for k in range(int(r.integers(3, 5))):
            L = float(r.uniform(.16, .38))
            o = (k - 1.5) * .05 + float(r.uniform(-.01, .01))
            sh = float(r.uniform(-.06, .06))
            p0 = (ox + s[0] * o + d[0] * (sh - L / 2), oz + s[1] * o + d[1] * (sh - L / 2))
            p1 = (ox + s[0] * o + d[0] * (sh + L / 2), oz + s[1] * o + d[1] * (sh + L / 2))
            strip(g, p0, p1, .016, .008, y + .004, F_SCAR, poly)


def zigzag(r, a, b, n, amp):
    """A jagged line from a to b (plan points), n segments, its inner points pushed sideways by up to amp."""
    pts = [a]
    dx, dz = b[0] - a[0], b[1] - a[1]
    L = math.hypot(dx, dz) or 1.0
    sx, sz = -dz / L, dx / L
    for i in range(1, n):
        u = i / n + float(r.uniform(-.25, .25)) / n
        o = float(r.uniform(-amp, amp))
        pts.append((a[0] + dx * u + sx * o, a[1] + dz * u + sz * o))
    pts.append(b)
    return pts


def ribbon_floor(g, pts, y, w0, w1, f):
    """A jagged ribbon lying on a floor (plan points), w0 wide at its start to w1 at its end."""
    n = len(pts)
    for i in range(n - 1):
        u0, u1 = i / (n - 1), (i + 1) / (n - 1)
        strip(g, pts[i], pts[i + 1], w0 + (w1 - w0) * u0, w0 + (w1 - w0) * u1, y, f)


# ---------------------------------------------------------------------------------------------------------------------------
# The set
# ---------------------------------------------------------------------------------------------------------------------------
def build(T):
    s = mk.SetPiece('rift')
    g = mk.Geo()
    tl = mk.Tally(g)
    P = plan()
    floor(g, T, P)
    tl('floor')
    collar(g, T, P)
    tl('collar')
    seam_bed(g, T, P)
    tl('seam bed')
    faults(g, T)
    tl('faults')
    walls(g, T)
    tl('walls')
    front(g, T)
    tl('front')
    derrick(g, T)
    tl('derrick')
    props(g, T)
    tl('props')
    rubble(g, T)
    tl('rubble')
    far(g, T)
    tl('far')
    s.tally = tl
    s.static[T.i] = g
    rigs = disc_slabs(T, P)
    for k, (geo, rest) in enumerate(rigs):
        s.rig('slab-%d' % k, T.i, geo, rest)
    if os.environ.get('RIFT_VSTATS'):
        vstats(g, tl, [geo for geo, _ in rigs], T)
    s.views = [
        ('front', (0.0, 1.6, 6.5), (0.0, 1.2, -2.0), 48),
        ('low', (0.9, .32, -5.6), (0.0, .9, 8.0), 58),
        ('top', (0.0, 14.0, 3.2), (0.0, 0.0, -.4), 52),
        ('behind', (0.8, 1.8, -3.2), (0.0, 1.2, 9.0), 52),
        ('derrick', (1.6, 2.3, 4.2), (-5.0, 4.0, -6.0), 60),
        ('seam', (.8, 1.7, -2.2), (4.6, 3.2, -9.8), 50),
        ('wide', (5.5, 7.5, 15.0), (0.0, 1.5, -4.0), 62),
    ]
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
    print('rift %s vertices: %s; total %d' % (T.name, ', '.join('%s %d' % r for r in sorted(rows, key=lambda r: -r[1])),
                                              sum(r[1] for r in rows)))


# ---- the floor: saw-cut slabs on a dusty bed, a few lifted out, a few left standing ----
def floor(g, T, P):
    r = mk.rng(41)
    # the beds of grit: one under each row's run of slabs (a lifted slab's bed lies lower)
    runs = {}
    for i in P.field:
        R = P.rects[i]
        y = P.top[i] - .04 if P.top[i] < -.1 else BED_Y
        runs.setdefault((round(R[2], 4), round(R[3], 4)), []).append((R[0], R[1], y))
    for (z0, z1), xs in runs.items():
        xs.sort()
        cur = None
        for (x0, x1, y) in xs + [(None, None, None)]:
            if cur is not None and (x0 is None or abs(x0 - cur[1]) > 1e-4 or y != cur[2]):
                g.face([(cur[0], cur[2], z0), (cur[1], cur[2], z0), (cur[1], cur[2], z1), (cur[0], cur[2], z1)], F_GRIT, normal=(0, 1, 0))
                cur = None
            if x0 is None:
                break
            cur = [x0, x1, y] if cur is None else [cur[0], x1, y]
    for i in P.field:
        R = P.rects[i]
        top = P.top[i]
        f = P.fin[i]
        if i in P.broken:
            for pc in P.broken[i]:
                piece_geo(g, T, pc, r, deep=-.5)
            continue
        j = .015
        poly = [(R[0] + j, R[2] + j), (R[1] - j, R[2] + j), (R[1] - j, R[3] - j), (R[0] + j, R[3] - j)]
        if T.i == 2:
            prism(g, poly, top, top - .5, f, T, bevel=0, sides=top > .1 or top < -.1)
            continue
        prism(g, poly, top, -.5, f, T, bevel=.03, side_f=F_FRACTURE if top > .1 else None)
        if _near(R) < 8.0 and r.random() < .3:
            scars(g, T, _inset(poly, .05), top, r)
        if top < -.1 and T.i == 0:
            # the bed of a lifted slab: rubble
            cx, cz = (R[0] + R[1]) / 2, (R[2] + R[3]) / 2
            for k in range(3):
                mk.rock(g, (cx + float(r.uniform(-.6, .6)), top - .01, cz + float(r.uniform(-.5, .5))),
                        (float(r.uniform(.1, .2)), float(r.uniform(.05, .1)), float(r.uniform(.08, .16))), F_RUBBLE, T,
                        seed=700 + i * 7 + k, rough=.3, level=0, turn=float(r.uniform(0, 3)))


def piece_geo(g, T, pc, r, deep=-.5):
    """A cracked slab's piece (sagging toward its fault, its fault faces glowing); its slab's bed of grit lies under it."""
    cut = pc['cut']
    top = pc['top']
    t = mk.Geo()
    if T.i == 2:
        prism(t, cut, top, top - .3, pc['fin'], T, bevel=0, glow=pc['glow'], sides=[gl for gl in pc['glow']])
    else:
        prism(t, cut, top, deep, pc['fin'], T, bevel=.02, side_f=F_FRACTURE, glow=pc['glow'], sides=pc['side'])
        if r.random() < .2 and _area(cut) > .6:
            scars(t, T, _inset(cut, .05), top, r)
    if pc['tilt'] is not None:
        (ax, ay, az), ang = pc['tilt']
        cx, cz = _centroid(cut)
        t = t.transformed(rot(ax, ay, az, ang, (cx, top, cz)))
    g.extend(t)


def collar(g, T, P):
    r = mk.rng(43)
    for pc in P.pieces:
        piece_geo(g, T, pc, r)
    # the beds of grit under the cracked slabs (below the disc's seam, which covers them inside the disc)
    for i in P.removed:
        R = P.rects[i]
        g.face([(R[0], BED_Y, R[2]), (R[1], BED_Y, R[2]), (R[1], BED_Y, R[3]), (R[0], BED_Y, R[3])], F_GRIT, normal=(0, 1, 0))


def faults(g, T):
    """The molten floor under each fault's gaping crack (under the pieces, so it shows only in the gap)."""
    for L in FAULTS:
        n = len(L)
        for i in range(n - 1):
            u0, u1 = i / (n - 1), (i + 1) / (n - 1)
            strip(g, L[i], L[i + 1], .2 - .09 * u0, .2 - .09 * u1, -.026, F_FISSURE)


def seam_bed(g, T, P):
    """The glowing seam under the disc (y -0.06) and the dark crust islands floating on it (inside the slabs at rest)."""
    r = mk.rng(47)
    for cell in P.disc:
        g.face([(p[0], -.06, p[1]) for p in cell], F_SEAM, normal=(0, 1, 0))
        inner = _inset(cell, .24)
        if len(inner) < 3 or _area(inner) < .05:
            continue
        cx, cz = _centroid(inner)
        for k in range((3, 2, 1)[T.i]):
            ox = cx + float(r.uniform(-.5, .5))
            oz = cz + float(r.uniform(-.5, .5))
            rad = float(r.uniform(.22, .5))
            m = int(r.integers(5, 8))
            ring = [(ox + math.cos(2 * math.pi * i / m) * rad * float(r.uniform(.7, 1.15)),
                     oz + math.sin(2 * math.pi * i / m) * rad * float(r.uniform(.7, 1.15))) for i in range(m)]
            ring = _clip_poly(_ccw(ring), inner)
            if len(ring) < 3 or _area(ring) < .02:
                continue
            ccx, ccz = _centroid(ring)
            top = (ccx, -.032, ccz)
            for i in range(len(ring)):
                a, b = ring[i], ring[(i + 1) % len(ring)]
                pa, pb = (a[0], -.058, a[1]), (b[0], -.058, b[1])
                na = sk._norm((a[0] - ccx, 2.5, a[1] - ccz))
                nb = sk._norm((b[0] - ccx, 2.5, b[1] - ccz))
                g.face((pa, pb, top), F_CRUST, normals=(na, nb, (0, 1, 0)))


def disc_slabs(T, P):
    """The fracture disc's fourteen slabs (rigs): each cell shrunk 1.5 cm, 0.35 m thick, a rough underside; the saw joints of
    the floor it was cut from run across it; each in its own frame (its cell's centroid at y 0)."""
    r = mk.rng(53)
    out = []
    joints = set()
    for i in P.removed:
        R = P.rects[i]
        for a, b in (((R[0], R[2]), (R[1], R[2])), ((R[1], R[2]), (R[1], R[3])), ((R[0], R[3]), (R[1], R[3])),
                     ((R[0], R[2]), (R[0], R[3]))):
            joints.add((round(a[0], 4), round(a[1], 4), round(b[0], 4), round(b[1], 4)))
    joints = sorted(joints)
    for k, cell in enumerate(P.disc):
        cx, cz = _centroid(cell)
        poly = _ccw(mk.shrink(cell, .015))
        f = SLAB_FINS[(k * 5 + 1) % len(SLAB_FINS)]
        t = mk.Geo()
        prism(t, poly, 0.0, -.35, f, T, bevel=.025, side_f=F_FRACTURE, keel=(.07, .05, 0)[T.i], bottom=T.i == 2,
              under_f=F_UNDER)
        top = _inset(poly, .035 if T.bevel else .01)
        if len(top) >= 3 and T.i < 2:
            for (ax, az, bx, bz) in joints:
                strip(t, (ax, az), (bx, bz), .026, .026, .003, F_JOINTLINE, top)
            if T.i == 0 and k % 2 == 0:
                scars(t, T, _inset(top, .04), 0.0, r)
        out.append((t.transformed(mk.translate(-cx, 0, -cz)), mk.translate(cx, 0, cz)))
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# The walls: five benches of columnar basalt over their bodies, terraces between, the molten seam up the jog's corner
# ---------------------------------------------------------------------------------------------------------------------------
def _seg_n(a, b):
    """A step's unit direction and its normal toward the pit (n = (dz, -dx))."""
    dx, dz = b[0] - a[0], b[1] - a[1]
    L = math.hypot(dx, dz)
    d = (dx / L, dz / L)
    return d, (d[1], -d[0]), L


def offset_line(pts, o):
    """The polyline moved `o` away from the pit (mitred corners)."""
    n = len(pts)
    out = []
    for i in range(n):
        ns = []
        if i > 0:
            ns.append(_seg_n(pts[i - 1], pts[i])[1])
        if i < n - 1:
            ns.append(_seg_n(pts[i], pts[i + 1])[1])
        if len(ns) == 1:
            m = ns[0]
        else:
            k = 1 + ns[0][0] * ns[1][0] + ns[0][1] * ns[1][1]
            m = ((ns[0][0] + ns[1][0]) / k, (ns[0][1] + ns[1][1]) / k)
        out.append((pts[i][0] - m[0] * o, pts[i][1] - m[1] * o))
    return out


def _clip_line(pts, tags, zmax):
    """The polyline's part with z <= zmax (the line runs out over zmax only at its two ends)."""
    out, ot = [], []
    n = len(pts)
    for i in range(n):
        p = pts[i]
        if p[1] <= zmax:
            if i > 0 and pts[i - 1][1] > zmax and not out:
                a = pts[i - 1]
                u = (zmax - a[1]) / (p[1] - a[1])
                out.append((a[0] + (p[0] - a[0]) * u, zmax))
                ot.append('end')
            out.append(p)
            ot.append(tags[i])
        elif out:
            a = out[-1]
            u = (zmax - a[1]) / (p[1] - a[1])
            out.append((a[0] + (p[0] - a[0]) * u, zmax))
            ot.append('end')
            break
    return out, ot


class Bench:
    """Bench k's body line (the full offset line, and its run: clipped at the bench's front end, out to the hill both ways)."""

    def __init__(self, k):
        r = mk.rng(310 + k)
        o = BO[k] + RECESS
        base = [p for p, _ in B0]
        tags = [t for _, t in B0]
        full = offset_line(base, o)
        n = len(full)
        for i in range(1, n - 1):
            if tags[i] in ('L', 'R', 'B') and i not in (2, n - 3):
                d, nn, _ = _seg_n(full[i - 1], full[i + 1])
                j = float(r.uniform(-.25, .25))
                full[i] = (full[i][0] + nn[0] * j, full[i][1] + nn[1] * j)
        self.k = k
        self.full = full
        self.tags = tags
        run, rt = _clip_line(full, tags, ZS[k])
        self.run = [(X_FAR, ZS[k])] + run + [(-X_FAR, ZS[k])]
        self.rtags = ['far'] + rt + ['far']


def walls(g, T):
    r = mk.rng(31)
    benches = [Bench(k) for k in range(5)]
    for k, bn in enumerate(benches):
        columns_run(g, T, bn.run, bn.rtags, BY[k], BY[k + 1], k, r, ends_on=OUT_Y if k == 0 else BY[k],
                    talus='sides' if k == 0 else 'all')
        terrace(g, T, benches, k, r)
    seam(g, T, benches)
    return benches


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


def _turn(p, q, s):
    """The signed angle the line p..q..s turns at q (negative: an inside corner, the rock folding round the pit)."""
    d1, _, _ = _seg_n(p, q)
    d2, _, _ = _seg_n(q, s)
    return math.atan2(d1[0] * d2[1] - d1[1] * d2[0], d1[0] * d2[0] + d1[1] * d2[1])


def column(g, T, poly, y0, top, grad, out, f, splits=(), shift=None, cull=-.3, lean=(0.0, 0.0), ridge=0.0, taper=0.0,
           drill=False):
    """A basalt column over a counter-clockwise convex polygon from y0 to its broken top (the plane top + grad . (p - c),
    folded into a ridge across it falling `ridge` per metre either side), leaning by `lean` (m across per m up) and narrowing
    by `taper` toward the top: its faces that turn away from the pit (`out`) are left off; split in courses at `splits` (the
    upper courses shifted by `shift`); drill: the half cast of a drill hole down its most forward face."""
    pts = list(poly)
    n = len(pts)
    if n < 3:
        return
    cx, cz = _centroid(pts)
    front = -1
    if drill:
        best = .55
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            L = math.hypot(b[0] - a[0], b[1] - a[1])
            if L > .16:
                dt = ((b[1] - a[1]) * out[0] - (b[0] - a[0]) * out[1]) / L
                if dt > best:
                    best, front = dt, i
    k2 = n // 2
    ca, cb = pts[0], pts[k2]
    ex, ez = cb[0] - ca[0], cb[1] - ca[1]
    el = math.hypot(ex, ez) or 1.0

    def ytop(i):
        p = pts[i]
        y = top + grad[0] * (p[0] - cx) + grad[1] * (p[1] - cz)
        if ridge > 0 and i not in (0, k2):
            y -= ridge * abs(((p[0] - ca[0]) * ez - (p[1] - ca[1]) * ex) / el)
        return y
    span = max(.1, top - y0)

    def at(p, y, sh):
        s = 1 - taper * (y - y0) / span
        return (cx + (p[0] - cx) * s + sh[0] + lean[0] * (y - y0), y, cz + (p[1] - cz) * s + sh[1] + lean[1] * (y - y0))
    bounds = [y0] + [s for s in splits]
    for si in range(len(bounds)):
        yb = bounds[si] + (.016 if si else 0.0)
        last = si == len(bounds) - 1
        sh = shift[si] if (shift is not None and si) else (0.0, 0.0)
        tops = [ytop(i) if last else bounds[si + 1] - .016 for i in range(n)]
        if last:
            if ridge > 0 and n > 3:
                _quad(g, [at(pts[i], tops[i], sh) for i in range(0, k2 + 1)], f, (0, 1, 0))
                _quad(g, [at(pts[i % n], tops[i % n], sh) for i in range(k2, n + 1)], f, (0, 1, 0))
            else:
                _quad(g, [at(pts[i], tops[i], sh) for i in range(n)], f, (0, 1, 0))
        for i in range(n):
            j = (i + 1) % n
            a, b = pts[i], pts[j]
            dx, dz = b[0] - a[0], b[1] - a[1]
            L = math.hypot(dx, dz)
            if L < 1e-4:
                continue
            nrm = (dz / L, 0.0, -dx / L)
            if nrm[0] * out[0] + nrm[2] * out[1] < cull:
                continue
            _quad(g, [at(a, yb, sh), at(b, yb, sh), at(b, tops[j], sh), at(a, tops[i], sh)], f, nrm)
            if i == front:
                u = .5 + (hash((round(a[0], 3), round(a[1], 3))) % 100 - 50) / 400.0
                m0 = (a[0] + (b[0] - a[0]) * (u - .03 / L), a[1] + (b[1] - a[1]) * (u - .03 / L))
                m1 = (a[0] + (b[0] - a[0]) * (u + .03 / L), a[1] + (b[1] - a[1]) * (u + .03 / L))
                ya_, yb_ = yb + (.02 if si else .15), (tops[i] + tops[j]) / 2 - .05
                if yb_ > ya_ + .1:
                    o = (nrm[0] * .005, nrm[2] * .005)
                    pa, pb = at(m0, ya_, sh), at(m1, ya_, sh)
                    pc, pd = at(m1, yb_, sh), at(m0, yb_, sh)
                    _quad(g, [(pa[0] + o[0], pa[1], pa[2] + o[1]), (pb[0] + o[0], pb[1], pb[2] + o[1]), (pc[0] + o[0], pc[1], pc[2] + o[1]),
                              (pd[0] + o[0], pd[1], pd[2] + o[1])], F_DRILL, nrm)


def columns_run(g, T, run, tags, y0, y1, k, r, ends_on=None, ledge=False, talus=None):
    """Basalt columns standing out of a body line `run` (plan points; the pit on the side n = (dz, -dx) of each step): the
    body faces behind, columns of jittered widths with slanted joints and faceted fronts, broken along the bench's edge in
    runs (notches, slopes), a few toppled short or standing proud, leaning, tapering, their tops broken in ridges,
    cross-cracked; corner columns at inside corners; the first bench's back wall is the blasted working face instead; the
    seam's slot at the jog's corner left open. talus: scree piled against the columns' feet ('sides': not on the back)."""
    n = len(run)
    near_k = k < 2 or ledge
    for i in range(n - 1):
        a, b = run[i], run[i + 1]
        d, nn, L = _seg_n(a, b)
        if L < .3:
            continue
        end_seg = tags[i] == 'far' or tags[i + 1] == 'far'
        ya = ends_on if (end_seg and ends_on is not None) else y0
        # the body face
        sk.grid_face(g, (a[0], ya, a[1]), (b[0] - a[0], 0, b[1] - a[1]), (0, y1 - ya, 0), 0, 0, F_RECESS, (nn[0], 0, nn[1]),
                     (6.0, 9.0, 99.0)[T.i])
        # where the columns run: short of inside corners, to outside corners; the seam's slot
        s0, s1 = 0.0, L
        if i > 0:
            t0 = _turn(run[i - 1], a, b)
            if t0 < 0:
                s0 = .62 * min(1.0, -t0 / .5)
        if i < n - 2:
            t1 = _turn(a, b, run[i + 2])
            if t1 < 0:
                s1 = L - .62 * min(1.0, -t1 / .5)
        seam_here = k < SEAM_K and not ledge
        if seam_here and tags[i + 1] == 'J1':
            s1 = L - (.5, .45, .4)[k]
        if seam_here and tags[i] == 'J1':
            s0 = (.5, .45, .4)[k]
        is_back = tags[i] in ('J1', 'B') and tags[i + 1] in ('B', 'cR0')
        back_zone = tags[i] in ('cL0', 'cL1', 'J0', 'J1', 'B', 'cR0') and tags[i + 1] in ('cL1', 'J0', 'J1', 'B', 'cR0', 'cR1')
        working = k == 0 and is_back and not ledge
        mid_z = (a[1] + b[1]) / 2
        near = ledge or (near_k and not end_seg and mid_z < 6.5)
        if T.i == 2:
            S = (2.0, 2.8) if ledge else (5.0, 7.0) if end_seg else (4.0, 5.5)
            cull = .25
        elif near and back_zone:
            S, cull = (.42, .8), -.3
        elif near and not ledge:
            S, cull = (.9, 1.5), -.3
        elif near:
            S, cull = (.38, .78), -.3
        elif k == 2 and not end_seg and mid_z < -8:
            S, cull = (1.0, 1.6), -.1
        elif end_seg:
            S, cull = (2.8, 4.0), .2
        else:
            S, cull = (2.2, 3.2), 0.0
        span = s1 - s0
        if span < .3:
            continue
        m = max(1, int(round(span / ((S[0] + S[1]) / 2))))
        us = [s0 + span * j / m + (float(r.uniform(-.2, .2)) * span / m if 0 < j < m else 0.0) for j in range(m + 1)]
        dl = [0.0] + [float(r.uniform(-.14, .14)) for _ in range(m - 1)] + [0.0]
        vf = [float(r.uniform(.14, .42)) for _ in range(m + 1)]
        base_f = COL_FINS[int(r.integers(len(COL_FINS)))]
        # the broken edge's profile along the run: notches and slopes a few columns long
        ph1, ph2 = float(r.uniform(0, 6.3)), float(r.uniform(0, 6.3))
        f1, f2 = float(r.uniform(.5, 1.2)), float(r.uniform(1.6, 3.0))
        amp = (y1 - ya) * .16
        for j in range(m):
            ua, ub = us[j], us[j + 1]
            mid = (ua + ub) / 2
            if not ledge and m > 2 and 0 < j < m - 1 and r.random() < .05:
                continue
            apex_u = ua + (ub - ua) * float(r.uniform(.22, .78))
            apex_v = max(vf[j], vf[j + 1]) + float(r.uniform(.12, .34)) * min(1.0, (ub - ua) / .9)
            q = r.random()
            push = .22 if q < .08 else (-.12 if q < .16 else 0.0)
            vb = -.45
            loc = [(ua + dl[j], vb), (ub + dl[j + 1], vb), (ub - dl[j + 1], vf[j + 1] + push), (apex_u, apex_v + push),
                   (ua - dl[j], vf[j] + push)]
            if r.random() < .2:
                del loc[3]
            poly = [(a[0] + d[0] * u + nn[0] * v, a[1] + d[1] * u + nn[1] * v) for u, v in loc]
            poly = _inset(_ccw(poly), .018)
            h = y1 - ya
            deficit = .18 * h / 2.5 + amp * (math.sin(mid * f1 + ph1) + .55 * math.sin(mid * f2 + ph2)) + abs(float(r.normal(0, .1)))
            top = y1 - min(h * .45, max(.035, deficit))
            q = r.random()
            if q < .06:
                top = ya + h * float(r.uniform(.3, .6))
            elif q < .13 and not ledge and near_k:
                top = y1 + float(r.uniform(.12, .55))
            if ledge:
                top = y1 + (float(r.uniform(-.65, .08)) if r.random() < .88 else float(r.uniform(.35, .9)))
                if abs(top - y1) < .035:
                    top = y1 - .05
            sl = float(r.uniform(0, .18))
            grad = (-nn[0] * sl + d[0] * float(r.uniform(-.08, .08)), -nn[1] * sl + d[1] * float(r.uniform(-.08, .08)))
            la, lb = float(r.uniform(-.035, .035)), float(r.uniform(-.045, .012))
            lean = (d[0] * la + nn[0] * lb, d[1] * la + nn[1] * lb)
            f = base_f if r.random() < .7 else COL_FINS[int(r.integers(len(COL_FINS)))]
            splits, shift = [], None
            if T.i < 2 and near and top - ya > 1.3 and r.random() < .26:
                splits = [ya + (top - ya) * float(r.uniform(.35, .65))]
                if r.random() < .3 and top - ya > 2.0:
                    splits.append(splits[0] + (top - splits[0]) * float(r.uniform(.4, .6)))
                shift = [(0.0, 0.0)] + [(nn[0] * float(r.uniform(-.06, .05)), nn[1] * float(r.uniform(-.06, .05))) for _ in splits]
            ridge = float(r.uniform(.12, .4)) if (T.i == 0 and near and r.random() < .75) else 0.0
            column(g, T, poly, ya, top, grad, nn, f, splits, shift, cull=cull, lean=lean, ridge=ridge,
                   taper=float(r.uniform(.02, .07)) if T.i < 2 else 0.0, drill=working and T.i == 0 and r.random() < .35)
        # scree piled against the columns' feet (on the bench below), its crest ragged
        no_talus = (talus == 'sides' and is_back) or (seam_here and 'J1' in (tags[i], tags[i + 1]))
        if talus is not None and T.i < 2 and not end_seg and not no_talus and r.random() < .7:
            ty = ya + (.012 if k == 0 else 0.0)
            steps = max(1, int(L / (1.6, 3.0, 9.0)[T.i]))
            for j in range(steps):
                u0, u1 = L * j / steps, L * (j + 1) / steps
                if (u1 < s0 and s0 > .5) or (u0 > s1 and s1 < L - .5):
                    continue
                h0 = float(r.uniform(.25, .75)) if j else .3
                h1 = float(r.uniform(.25, .75))
                toe0, toe1 = .55 + h0 * 1.5, .55 + h1 * 1.5
                pa0 = (a[0] + d[0] * u0 + nn[0] * toe0, a[1] + d[1] * u0 + nn[1] * toe0)
                pb0 = (a[0] + d[0] * u1 + nn[0] * toe1, a[1] + d[1] * u1 + nn[1] * toe1)
                pa1 = (a[0] + d[0] * u0 + nn[0] * .5, a[1] + d[1] * u0 + nn[1] * .5)
                pb1 = (a[0] + d[0] * u1 + nn[0] * .5, a[1] + d[1] * u1 + nn[1] * .5)
                _quad(g, [(pa0[0], ty + .005, pa0[1]), (pb0[0], ty + .005, pb0[1]), (pb1[0], ty + h1, pb1[1]), (pa1[0], ty + h0, pa1[1])],
                      F_SCREE, (nn[0], 1.5, nn[1]))
        # a corner column at an inside corner (none at the seam's slot)
        if T.i < 2 and i < n - 2 and not (k < SEAM_K and not ledge and tags[i + 1] == 'J1'):
            c = run[i + 2]
            if _turn(a, b, c) < -.3:
                d2, n2, _ = _seg_n(b, c)
                mx, mz = nn[0] + n2[0], nn[1] + n2[1]
                ml = math.hypot(mx, mz)
                mx, mz = mx / ml, mz / ml
                tx, tz = -mz, mx
                loc = [(-.42, -.6), (.42, -.6), (.44, .42), (0.0, .78), (-.44, .42)]
                poly = _ccw([(b[0] + tx * u + mx * v, b[1] + tz * u + mz * v) for u, v in loc])
                top = y1 - min(.9, .035 + abs(float(r.normal(0, .25)))) if not ledge else y1 - float(r.uniform(.05, .4))
                column(g, T, _inset(poly, .018), y0, top, (-mx * .08, -mz * .08), (mx, mz),
                       COL_FINS[int(r.integers(len(COL_FINS)))], cull=(-.3 if T.i < 2 else .25), taper=.04 if T.i < 2 else 0.0)


def terrace(g, T, benches, k, r):
    """Bench k's top (y BY[k+1]): between its body line and the next bench's, out to the hill beyond the next bench's end;
    the top bench's runs back to the hill's crest. Scree lies along the foot of the bench above."""
    y = BY[k + 1]
    A = benches[k].full
    tags = benches[k].tags
    n = len(A)

    def face(poly):
        poly = _clean(_ccw(poly))
        if len(poly) >= 3 and abs(_area(poly)) > 1e-3:
            g.face([(p[0], y, p[1]) for p in poly], F_BENCH, normal=(0, 1, 0))
    if k < 4:
        Bn = benches[k + 1].full
        for i in range(n - 1):
            q = _clip([A[i], A[i + 1], Bn[i + 1], Bn[i]], 0, 1, ZS[k + 1])
            if len(q) >= 3:
                face(q)
            # beyond the next bench's end the terrace runs out to the hill's flank
            if A[i][1] > ZS[k + 1] - 1e-6 or A[i + 1][1] > ZS[k + 1] - 1e-6:
                xs = X_FAR if A[i][0] > 0 else -X_FAR
                q = [A[i], A[i + 1], (xs, A[i + 1][1]), (xs, A[i][1])]
                q = _clip(_clip(q, 0, 1, ZS[k]), 0, -1, -ZS[k + 1])
                if len(q) >= 3:
                    face(q)
        for sgn in (1, -1):
            x = sgn * X_FAR
            g.face(((x, OUT_Y - 1, ZS[k + 1]), (x, OUT_Y - 1, ZS[k]), (x, y, ZS[k]), (x, y, ZS[k + 1])), F_SCREE,
                   normal=(sgn, 0, 0))
    else:
        far_pt = []
        for i in range(n):
            px, pz = A[i]
            t = tags[i]
            if t == 'L' or t == 'cL0':
                far_pt.append((X_FAR, pz))
            elif t == 'R' or t == 'cR1':
                far_pt.append((-X_FAR, pz))
            elif t == 'cL1':
                far_pt.append((X_FAR, ZB_FAR))
            elif t == 'cR0':
                far_pt.append((-X_FAR, ZB_FAR))
            else:
                far_pt.append((px, ZB_FAR))
        for i in range(n - 1):
            q = _clip([A[i], A[i + 1], far_pt[i + 1], far_pt[i]], 0, 1, ZS[4])
            if len(q) >= 3:
                face(q)
        for sgn in (1, -1):
            x = sgn * X_FAR
            g.face(((x, OUT_Y - 1, ZB_FAR), (x, OUT_Y - 1, ZS[4]), (x, y, ZS[4]), (x, y, ZB_FAR)), F_SCREE, normal=(sgn, 0, 0))
        g.face(((-X_FAR, y, ZB_FAR), (X_FAR, y, ZB_FAR), (X_FAR, OUT_Y - 1, ZB_FAR), (-X_FAR, OUT_Y - 1, ZB_FAR)), F_SCREE,
               normal=(0, 0, -1))
    # scree along the foot of the bench above (the back's near terraces)
    if T.i == 0 and k < 2:
        Bn = benches[k + 1].full
        for i in range(n - 1):
            if tags[i] not in ('J1', 'B'):
                continue
            a, b = Bn[i], Bn[i + 1]
            d, nn, L = _seg_n(a, b)
            for q in range(max(1, int(L / 2.2))):
                u = float(r.uniform(.1, .9)) * L
                x = a[0] + d[0] * u + nn[0] * float(r.uniform(.85, 1.1))
                z = a[1] + d[1] * u + nn[1] * float(r.uniform(.85, 1.1))
                mk.rock(g, (x, y - .02, z), (float(r.uniform(.12, .26)), float(r.uniform(.07, .14)), float(r.uniform(.1, .22))),
                        F_SCREE, T, seed=900 + k * 31 + i * 7 + q, rough=.3, level=0, turn=float(r.uniform(0, 3)))


def seam(g, T, benches):
    """The molten seam up the jog's inside corner: a jagged crack glows down the slot's corner, widest low; further along
    the bodies glow behind the columns (it shows through their joints and over their broken tops); stones jam the slot; the
    seam crosses each bench's top to the next corner and frays out on the fourth bench; embers at its foot."""
    r = mk.rng(61)
    for k in range(SEAM_K + 1):
        bn = benches[k]
        i = bn.tags.index('J1')
        C = bn.full[i]
        A = bn.full[i - 1]
        Bp = bn.full[i + 1]
        y0, y1 = BY[k], BY[k + 1]
        top = y1 - .02 if k < SEAM_K else y0 + (y1 - y0) * .75
        dirs = []
        for (P0, side) in ((A, 0), (Bp, 1)):
            d = _seg_n(C, P0)[0]
            nf = _seg_n(P0, C)[1] if side == 0 else _seg_n(C, P0)[1]
            dirs.append(d)
            o = (nf[0] * .006, nf[1] * .006)
            steps = (10, 7, 3)[T.i]
            wmax = (.3, .22, .15, .07)[k]
            prev = None
            for j in range(steps + 1):
                y = y0 + .02 + (top - y0 - .02) * j / steps
                w = wmax * float(r.uniform(.3, 1.0)) * (1 - .45 * j / steps) + .02
                if prev is not None:
                    yp, wp = prev
                    q = [(C[0] + o[0], yp, C[1] + o[1]), (C[0] + d[0] * wp + o[0], yp, C[1] + d[1] * wp + o[1]),
                         (C[0] + d[0] * w + o[0], y, C[1] + d[1] * w + o[1]), (C[0] + o[0], y, C[1] + o[1])]
                    g.face(q, F_MAGMA, normal=(nf[0], 0, nf[1]))
                prev = (y, w)
            if k < SEAM_K:
                # the seep behind the columns
                reach = (1.8, 1.5, 1.1)[k]
                n2 = (6, 4, 2)[T.i]
                for j in range(n2):
                    u0 = .25 + (reach - .25) * j / n2
                    u1 = .25 + (reach - .25) * (j + 1) / n2
                    h0 = y0 + (top - y0) * (1 - (u0 / reach) ** 1.3) * float(r.uniform(.45, 1.0))
                    h1 = y0 + (top - y0) * (1 - (u1 / reach) ** 1.3) * float(r.uniform(.45, 1.0))
                    q = [(C[0] + d[0] * u0 + o[0], y0 + .02, C[1] + d[1] * u0 + o[1]), (C[0] + d[0] * u1 + o[0], y0 + .02, C[1] + d[1] * u1 + o[1]),
                         (C[0] + d[0] * u1 + o[0], max(y0 + .05, h1), C[1] + d[1] * u1 + o[1]),
                         (C[0] + d[0] * u0 + o[0], max(y0 + .05, h0), C[1] + d[1] * u0 + o[1])]
                    g.face(q, F_MAGMA, normal=(nf[0], 0, nf[1]))
        # stones jammed in the slot, breaking the glow's line
        if k < SEAM_K and T.i < 2:
            mx, mz = dirs[0][0] + dirs[1][0], dirs[0][1] + dirs[1][1]
            for q in range((3, 2)[T.i]):
                u = float(r.uniform(.12, .3))
                y = y0 + (y1 - y0) * float(r.uniform(.15, .85))
                sz = float(r.uniform(.1, .17))
                mk.rock(g, (C[0] + mx * u, y, C[1] + mz * u), (sz, sz * 1.3, sz), F_COL_DK, T, seed=1020 + k * 5 + q, rough=.25,
                        flat_bottom=False, level=0, turn=float(r.uniform(0, 3)))
        # across the bench's top to the next bench's corner
        if k < SEAM_K:
            C2 = benches[k + 1].full[benches[k + 1].tags.index('J1')]
            pts = zigzag(r, (C[0] - .2, C[1] + .2), (C2[0] - .05, C2[1] + .05), 5, .12)
            ribbon_floor(g, pts, y1 + .003, .16, .09, F_CHAR)
            ribbon_floor(g, pts, y1 + .006, .06, .035, F_MAGMA)
    # embers at the seam's foot (where the floor's fault meets it)
    C = benches[0].full[benches[0].tags.index('J1')]
    for k in range((7, 4, 2)[T.i]):
        a = float(r.uniform(.6, 2.6))
        d = float(r.uniform(.3, .8))
        x, z = C[0] - .45 - math.cos(a) * d, C[1] + .45 + math.sin(a) * d * .8
        sz = float(r.uniform(.05, .12))
        mk.rock(g, (x, -.01, z), (sz, sz * .6, sz), F_EMBER if k % 2 == 0 else F_RUBBLE, T, seed=1000 + k, rough=.3, level=0,
                turn=float(r.uniform(0, 3)))


# ---------------------------------------------------------------------------------------------------------------------------
# The front: the low broken ledge, the ramp road out, the ground beyond
# ---------------------------------------------------------------------------------------------------------------------------
def ramp_y(z):
    return OUT_Y * min(1.0, max(0.0, (z - RAMP_Z[0]) / (RAMP_Z[1] - RAMP_Z[0])))


def road_x(z):
    """The haul road's centre beyond the ramp (it bends away to the left)."""
    mid = (RAMP_X[0] + RAMP_X[1]) / 2
    u = max(0.0, z - RAMP_Z[1])
    return mid + .012 * u * u


def ground_y(x, z):
    """The ground outside the pit: level at the ledge and the road, rolling further out."""
    v = sk.fbm(np.array([[x * .07, 0.0, z * .07]]), 1.0, 3, 77)[0] - .5
    k = min(1.0, max(0.0, (z - LEDGE_Z - 1.0) / 8.0))
    road = math.exp(-((x - road_x(z)) / 4.5) ** 2) if z > RAMP_Z[1] - 1 else 0.0
    return OUT_Y + v * 2.2 * k * (1 - .8 * road)


def front(g, T):
    r = mk.rng(71)
    # the ledge: low broken basalt columns across the mouth, the ramp's cutting through them
    xl = 8.6
    for (xa, xb) in ((-xl, RAMP_X[0] - .2), (RAMP_X[1] + .2, xl)):
        columns_run(g, T, [(xa, LEDGE_Z), (xb, LEDGE_Z)], ['far', 'far'], 0.0, OUT_Y, 0, r, ledge=True)
    # columns fallen off the ledge, lying on the floor in front of it
    for (x, z, yaw) in ((-6.0, 9.4, .3), (-1.6, 9.7, -.5), (7.0, 9.0, 1.1))[:(3, 2, 0)[T.i]]:
        fallen_column(g, T, (x, z), yaw, float(r.uniform(1.0, 1.6)), r)
    ramp(g, T, r)
    outside(g, T, r)


def fallen_column(g, T, at, yaw, length, r):
    """A basalt column lying on its side (a five-sided prism, one face down)."""
    rad = float(r.uniform(.28, .36))
    t = mk.Geo()
    ring = [(math.cos(2 * math.pi * i / 5 + math.pi / 2 + float(r.uniform(-.1, .1))) * rad * float(r.uniform(.85, 1.1)),
             math.sin(2 * math.pi * i / 5 + math.pi / 2 + float(r.uniform(-.1, .1))) * rad * float(r.uniform(.85, 1.1)))
            for i in range(5)]
    poly = _ccw(ring)
    prism(t, poly, length / 2, -length / 2, COL_FINS[int(r.integers(len(COL_FINS)))], T, bevel=.03, bottom=True)
    lo = min(p[1] for p in poly)
    m = mul(mk.translate(at[0], 0, at[1]), rot(0, 1, 0, yaw), rot(1, 0, 0, -math.pi / 2), mk.translate(0, 0, -lo))
    g.extend(t.transformed(m))


def ramp(g, T, r):
    """The ramp road: rutted dirt from the pit floor up through the ledge's cutting to the ground outside."""
    x0, x1 = RAMP_X
    nz = (6, 4, 2)[T.i]
    zs = [RAMP_Z[0] + (RAMP_Z[1] - RAMP_Z[0]) * i / nz for i in range(nz + 1)]
    xs = [x0, (x0 + x1) / 2 - .9, (x0 + x1) / 2 + .9, x1]
    crown = [0.0, .04, .04, 0.0]
    for i in range(nz):
        za, zb = zs[i], zs[i + 1]
        ya, yb = ramp_y(za), ramp_y(zb)
        nr = sk._norm((0, 1, -(yb - ya) / (zb - za)))
        for j in range(3):
            q = ((xs[j], ya + crown[j], za), (xs[j + 1], ya + crown[j + 1], za), (xs[j + 1], yb + crown[j + 1], zb),
                 (xs[j], yb + crown[j], zb))
            g.face(q, F_ROAD, normal=nr)
        if T.i < 2:
            for xc in (x0 + .9, x1 - .9):
                q = ((xc - .17, ya + .028, za), (xc + .17, ya + .028, za), (xc + .17, yb + .028, zb), (xc - .17, yb + .028, zb))
                g.face(q, F_RUT, normal=nr)
    # the embankment's sides inside the pit, the cutting's walls through the ledge
    for sgn, xe in ((-1, x0), (1, x1)):
        for i in range(nz):
            za, zb = zs[i], zs[i + 1]
            ya, yb = ramp_y(za), ramp_y(zb)
            if zb <= LEDGE_Z - .4 + .01:
                q = ((xe, ya, za), (xe, yb, zb), (xe + sgn * yb * 1.1, 0.0, zb), (xe + sgn * ya * 1.1, 0.0, za))
                g.face(q, F_SCREE, normal=sk._norm((sgn, 1.1, 0)))
            else:
                za2 = max(za, LEDGE_Z - .4)
                ya2 = ramp_y(za2)
                q = ((xe - sgn * .02, ya2, za2), (xe - sgn * .02, yb, zb), (xe - sgn * .02, OUT_Y, zb), (xe - sgn * .02, OUT_Y, za2))
                g.face(q, F_COL_DK, normal=(-sgn, 0, 0))
        # kerb stones along the road's edges
        for k in range((4, 2, 0)[T.i]):
            z = RAMP_Z[0] + .8 + k * 1.7 + float(r.uniform(-.3, .3))
            sz = float(r.uniform(.18, .3))
            mk.rock(g, (xe - sgn * .15, ramp_y(z) - .02, z), (sz, sz * .7, sz * 1.1), F_RUBBLE, T, seed=1100 + k * 3 + sgn,
                    rough=.25, level=0, turn=float(r.uniform(0, 3)))


def grid_ground(g, T, x0, x1, z0, z1, cell, f=F_GROUND):
    nx = max(1, int(math.ceil((x1 - x0) / cell)))
    nz = max(1, int(math.ceil((z1 - z0) / cell)))
    xs = [x0 + (x1 - x0) * i / nx for i in range(nx + 1)]
    zs = [z0 + (z1 - z0) * j / nz for j in range(nz + 1)]
    Y = [[ground_y(x, z) for x in xs] for z in zs]
    N = []
    for j in range(nz + 1):
        row = []
        for i in range(nx + 1):
            ia, ib = max(0, i - 1), min(nx, i + 1)
            ja, jb = max(0, j - 1), min(nz, j + 1)
            gx = (Y[j][ib] - Y[j][ia]) / max(1e-6, xs[ib] - xs[ia])
            gz = (Y[jb][i] - Y[ja][i]) / max(1e-6, zs[jb] - zs[ja])
            row.append(sk._norm((-gx, 1.0, -gz)))
        N.append(row)
    for j in range(nz):
        for i in range(nx):
            q = ((xs[i], Y[j][i], zs[j]), (xs[i + 1], Y[j][i + 1], zs[j]), (xs[i + 1], Y[j + 1][i + 1], zs[j + 1]),
                 (xs[i], Y[j + 1][i], zs[j + 1]))
            g.face(q, f, normals=(N[j][i], N[j][i + 1], N[j + 1][i + 1], N[j + 1][i]))


def outside(g, T, r):
    """The ground beyond the ledge: the road on to the spoil heaps, the quarry's hut and its lamps."""
    cell = (3.4, 5.0, 8.0)[T.i]
    xl = 8.6
    grid_ground(g, T, -X_FAR - 12, -xl, ZS[0], 50.0, cell)
    grid_ground(g, T, xl, X_FAR + 12, ZS[0], 50.0, cell)
    grid_ground(g, T, -xl, RAMP_X[0], LEDGE_Z, 50.0, cell)
    grid_ground(g, T, RAMP_X[1], xl, LEDGE_Z, 50.0, cell)
    grid_ground(g, T, RAMP_X[0], RAMP_X[1], RAMP_Z[1], 50.0, cell)
    # the road beyond the ramp
    zs = [RAMP_Z[1] + i * 2.6 for i in range((13, 8, 5)[T.i])]
    for i in range(len(zs) - 1):
        za, zb = zs[i], zs[i + 1]
        xa, xb = road_x(za), road_x(zb)
        q = ((xa - 1.8, ground_y(xa - 1.8, za) + .02, za), (xa + 1.8, ground_y(xa + 1.8, za) + .02, za),
             (xb + 1.8, ground_y(xb + 1.8, zb) + .02, zb), (xb - 1.8, ground_y(xb - 1.8, zb) + .02, zb))
        g.face(q, F_ROAD, normal=(0, 1, 0))
    # spoil heaps of waste rock
    for k, (x, z, s) in enumerate(((-11.5, 19.0, 3.6), (15.5, 23.5, 4.2), (-3.5, 31.0, 3.0), (-21.0, 27.0, 4.6), (24.0, 14.5, 3.0))):
        if T.i == 2 and k > 2:
            break
        y = ground_y(x, z)
        mk.rock(g, (x, y - .2, z), (s, s * .48, s * .85), F_SPOIL, T, seed=1200 + k, rough=.3, level=(1 if k < 2 else 0, 0, 0)[T.i],
                turn=float(r.uniform(0, 3)))
        if T.i == 0 and k < 3:
            for q in range(1):
                a = float(r.uniform(0, 6.28))
                mk.rock(g, (x + math.cos(a) * s * 1.05, y - .02, z + math.sin(a) * s * .9), (s * .14, s * .08, s * .12), F_SPOIL, T,
                        seed=1210 + k * 5 + q, rough=.3, level=0, turn=a)
    hut(g, T, (-6.4, 16.2), .25)
    for (x, z) in ((6.6, 14.6), (9.4, 22.5)):
        lamp_post(g, T, x, ground_y(x, z), z)
    shear_legs(g, T, (road_x(18.6), 18.6))
    # blocks waiting by the road to be carted off
    for (x, z, sz, yaw, yy) in ((-1.5, 15.2, (1.6, .9, 1.0), .1, 0.0), (-.2, 15.4, (1.3, .85, 1.05), -.06, 0.0),
                                (-.9, 15.3, (1.4, .8, .95), .35, .87)):
        cut_block(g, T, (x, z), sz, yaw, F_CUT, r, y=ground_y(x, z) + yy + sz[1] / 2 - .03)


def shear_legs(g, T, at):
    """A timber tripod over the haul road at the ramp's head, a block hanging from its chain in a sling."""
    x, z = at
    y0 = ground_y(x, z)
    apex = (x, y0 + 6.2, z + .3)
    feet = ((x - 2.5, z - .4), (x + 2.4, z - .2), (x + .2, z + 3.2))
    for (fx, fz) in feet:
        foot = (fx, ground_y(fx, fz) - .05, fz)
        sk.sweep(g, sk.circle_section(.11, (6, 5, 4)[T.i]), [foot, sk._lerp(foot, apex, .5), apex], F_TIMBER, scale=[1, .9, .78])
    sk.cylinder(g, sk._add(apex, (0, -.25, 0)), sk._add(apex, (0, .1, 0)), .16, F_ROPE, (6, 5, 4)[T.i])
    low = (apex[0], y0 + 2.7, apex[2])
    sk.tube(g, [sk._add(apex, (0, -.25, 0)), low], .02, F_IRON, (4, 3, 3)[T.i], caps=False)
    if T.i < 2:
        sk.sweep(g, sk.circle_section(.03, 5), [low, (low[0] + .08, low[1] - .1, low[2]), (low[0] + .02, low[1] - .2, low[2]),
                                               (low[0] - .06, low[1] - .14, low[2])], F_IRON, up=(0, 0, 1))
        for sx in (-.4, .4):
            sk.tube(g, [(low[0], low[1] - .18, low[2]), (low[0] + sx, low[1] - .75, low[2])], .014, F_IRON, 3, caps=False)
    t = mk.Geo()
    sk.box(t, -.5, .5, -.4, 0, -.35, .35, F_CUT, T, bevel=.03, cell=0)
    g.extend(t.transformed(mul(mk.translate(low[0], low[1] - .72, low[2]), rot(0, 1, 0, .4))))


def hut(g, T, at, yaw):
    """The quarry's timber office: plank walls, a tin roof, a lit window, a door."""
    x, z = at
    y = ground_y(x, z) - .05
    t = mk.Geo()
    W, D, H = 3.2, 2.4, 2.4
    sk.box(t, -W / 2, W / 2, 0, .3, -D / 2, D / 2, F_COL_DK, T, bevel=.02, cell=0)
    sk.box(t, -W / 2 + .05, W / 2 - .05, .3, H, -D / 2 + .05, D / 2 - .05, F_PLANK, T, bevel=.02, cell=0)
    for sx in (-1, 1):
        for sz in (-1, 1):
            sk.box(t, sx * W / 2 - .09, sx * W / 2 + .09, .3, H, sz * D / 2 - .09, sz * D / 2 + .09, F_TIMBER, T, bevel=.01, cell=0)
    hi, lo = H + .55, H + .1
    q = ((-W / 2 - .35, lo, D / 2 + .4), (W / 2 + .35, lo, D / 2 + .4), (W / 2 + .35, hi, -D / 2 - .3), (-W / 2 - .35, hi, -D / 2 - .3))
    nn = sk._norm(sk._cross(sk._sub(q[1], q[0]), sk._sub(q[3], q[0])))
    if nn[1] < 0:
        nn = (-nn[0], -nn[1], -nn[2])
    t.face(q, F_ROOF, normal=nn)
    t.face(q[::-1], F_TIMBER_X, normal=(-nn[0], -nn[1], -nn[2]))
    for sx in (-1, 1):
        xx = sx * (W / 2 - .05)
        t.face(((xx, H, -D / 2 + .05), (xx, H, D / 2 - .05), (xx, lo + .05, D / 2 - .05), (xx, hi - .05, -D / 2 + .05)), F_PLANK,
               normal=(sx, 0, 0))
    t.face(((-.9, 1.1, -D / 2 + .04), (-.2, 1.1, -D / 2 + .04), (-.2, 1.75, -D / 2 + .04), (-.9, 1.75, -D / 2 + .04)), F_WINDOW,
           normal=(0, 0, -1))
    if T.i < 2:
        sk.box(t, -.95, -.15, 1.05, 1.1, -D / 2 - .02, -D / 2 + .06, F_TIMBER_X, T, bevel=0, cell=0)
        sk.box(t, -.58, -.52, 1.1, 1.75, -D / 2 + .02, -D / 2 + .06, F_TIMBER, T, bevel=0, cell=0)
    sk.box(t, .45, 1.2, .3, 2.05, -D / 2 - .01, -D / 2 + .06, F_TIMBER, T, bevel=.01, cell=0)
    g.extend(t.transformed(mul(mk.translate(x, y, z), rot(0, 1, 0, yaw))))


def lamp_post(g, T, x, y, z):
    """A timber post with an arm and a hanging work lantern."""
    sk.box(g, x - .08, x + .08, y - .1, y + 3.4, z - .08, z + .08, F_TIMBER, T, bevel=.015, cell=0)
    sk.box(g, x - .06, x + .06, y + 3.15, y + 3.27, z - .06, z - .75, F_TIMBER_Z, T, bevel=.01, cell=0)
    lantern(g, T, (x, y + 3.15, z - .66))


def lantern(g, T, top):
    """A work lantern hanging at `top`: a wire bail, an iron cap, the glowing glass, a base."""
    x, y, z = top
    n = (8, 6, 4)[T.i]
    sk.cylinder(g, (x, y, z), (x, y - .1, z), .008, F_IRON, 3, caps=False)
    sk.lathe(g, [(.0, -.06), (.07, -.1), (.09, -.15), (.0, -.13)], (x, y - .04, z), F_IRON, n)
    sk.lathe(g, [(.06, -.15), (.075, -.22), (.07, -.32), (.055, -.36)], (x, y - .04, z), F_LAMP, n, cap1=True)
    sk.lathe(g, [(.08, -.36), (.08, -.4), (.0, -.41)], (x, y - .04, z), F_IRON, n)


# ---------------------------------------------------------------------------------------------------------------------------
# The derrick: a guyed timber mast with its boom, hoist, hook and chain; the winch at its foot
# ---------------------------------------------------------------------------------------------------------------------------
def derrick(g, T):
    mx, mz = DERRICK
    H = 8.4
    # the footing and the crossed sills
    mk.slab(g, mk.circle(mx, mz, .55, (8, 7, 5)[T.i], .2), -.05, .22, F_COL_DK, T, bevel=.03)
    sk.box(g, mx - 1.9, mx + 1.9, 0, .26, mz - .14, mz + .14, F_TIMBER_X, T, bevel=.02, cell=0)
    sk.box(g, mx - .14, mx + .14, .26, .5, mz - 1.7, mz + 1.7, F_TIMBER_Z, T, bevel=.02, cell=0)
    # the mast, iron banded
    sk.box(g, mx - .17, mx + .17, .5, H, mz - .17, mz + .17, F_TIMBER, T, bevel=.025, cell=0)
    for y in ((1.1, 2.9, 4.8, 6.6, 8.15) if T.i < 2 else (1.1, 8.15)):
        sk.box(g, mx - .19, mx + .19, y, y + .08, mz - .19, mz + .19, F_IRON, T, bevel=.006, cell=0)
    # the mast head: a cap plate, the gudgeon
    sk.box(g, mx - .26, mx + .26, H, H + .07, mz - .26, mz + .26, F_IRON, T, bevel=.01, cell=0)
    sk.cylinder(g, (mx, H + .07, mz), (mx, H + .32, mz), .05, F_IRON, (6, 5, 4)[T.i])
    head = (mx, H + .2, mz)
    # the boom: its heel on the mast's foot, raised toward the floor's middle
    yaw, elev, Lb = .3, .74, 7.4
    dirb = (math.sin(yaw) * math.cos(elev), math.sin(elev), math.cos(yaw) * math.cos(elev))
    heel = (mx + .3 * math.sin(yaw), 1.05, mz + .3 * math.cos(yaw))
    tip = sk._add(heel, dirb, Lb)
    mid = sk._add(heel, dirb, Lb * .5)
    sk.sweep(g, sk.rect_section(.26, .26, .03 if T.bevel else 0), [heel, mid, tip], F_TIMBER_Z, up=(0, 1, 0), scale=[1, .92, .76],
             smooth_u=False)
    for u in ((.08, .5, .93) if T.i < 2 else (.93,)):
        c = sk._add(heel, dirb, Lb * u)
        sk.sweep(g, sk.rect_section(.29 - .06 * u, .29 - .06 * u), [sk._add(c, dirb, -.05), sk._add(c, dirb, .05)], F_IRON,
                 up=(0, 1, 0), smooth_u=False)
    side = sk._norm(sk._cross(dirb, (0, 1, 0)))
    sk.cylinder(g, sk._add(heel, side, -.2), sk._add(heel, side, .2), .05, F_IRON, (6, 5, 4)[T.i])
    sk.cylinder(g, sk._add(tip, side, -.09), sk._add(tip, side, .09), .14, F_IRON, (10, 7, 5)[T.i])
    # the topping lift (two wire ropes, mast head to boom tip) and the guys to the first bench and the floor
    for o in ((-.06, .06) if T.i < 2 else (0.0,)):
        sk.tube(g, [sk._add(head, side, o), sk._add(sk._lerp(head, tip, .5), (0, -1, 0), .12), sk._add(tip, side, o)], .012, F_IRON,
                (4, 3, 3)[T.i], caps=False)
    for anchor in ((-3.3, BY[1], -10.6), (-8.6, BY[1], -10.45), (-10.0, 0.0, -2.4)):
        sag = sk._add(sk._lerp(head, anchor, .5), (0, -1, 0), .18)
        sk.tube(g, [head, sag, anchor], .011, F_IRON, 3, caps=False)
        if T.i < 2:
            sk.box(g, anchor[0] - .14, anchor[0] + .14, anchor[1] - .05, anchor[1] + .14, anchor[2] - .14, anchor[2] + .14, F_IRON, T,
                   bevel=.01, cell=0)
    # the hoist: from the winch up along the boom, over the tip sheave, two falls down to the hook block
    winch_at = (mx + 1.0, mz + .55)
    drum = (winch_at[0], .55, winch_at[1])
    along = [drum, sk._add(heel, (0, 1, 0), .2), sk._add(sk._add(heel, dirb, Lb * .5), (0, 1, 0), .2), sk._add(tip, (0, 1, 0), .14)]
    sk.tube(g, along, .014, F_ROPE, (4, 3, 3)[T.i], caps=False)
    hook_y = 3.6
    blk = (tip[0], hook_y + .42, tip[2])
    for o in (-.07, .07):
        sk.tube(g, [sk._add(sk._add(tip, side, o), (0, -1, 0), .14), sk._add(blk, side, o)], .014, F_ROPE, (4, 3, 3)[T.i], caps=False)
    hook_block(g, T, blk, side)
    winch(g, T, winch_at)
    # a work lantern hung from the mast
    sk.box(g, mx - .03, mx + .03, 2.35, 2.42, mz + .17, mz + .5, F_IRON, T, bevel=0, cell=0)
    lantern(g, T, (mx, 2.35, mz + .46))


def hook_block(g, T, at, side):
    """The hook block (cheek plates, a sheave), the hook under it and a lifting chain hanging from the hook."""
    x, y, z = at
    t = mk.Geo()
    sk.box(t, -.13, .13, -.2, .2, -.06, .06, F_IRON, T, bevel=.02, cell=0)
    sk.cylinder(t, (0, 0, -.09), (0, 0, .09), .1, F_RUST, (8, 6, 5)[T.i])
    sk.cylinder(t, (0, -.2, 0), (0, -.3, 0), .03, F_IRON, (5, 4, 4)[T.i])
    path = [(0, -.3, 0), (0, -.42, 0), (.0, -.56, 0), (.07, -.64, 0), (.16, -.6, 0), (.19, -.5, 0), (.15, -.44, 0)]
    if T.i == 2:
        path = path[::2]
    sk.sweep(t, sk.circle_section(.034, (6, 5, 4)[T.i]), path, F_IRON, up=(0, 0, 1), scale=[1, 1.15, 1.2, 1.1, .9, .7, .55][:len(path)])
    if T.i < 2:
        y0 = -.62
        nlinks = (8, 5)[T.i]
        for k in range(nlinks):
            cy = y0 - .03 - k * .085
            loop = [(.06 * math.cos(2 * math.pi * i / 6) * .55, cy + .055 * math.sin(2 * math.pi * i / 6), 0.0) for i in range(6)]
            if k % 2:
                loop = [(0.0, p[1], p[0]) for p in loop]
            loop = [(p[0] + .05, p[1], p[2]) for p in loop]
            sk.sweep(t, sk.circle_section(.011, 3), loop, F_RUST, closed=True)
        sk.sweep(t, sk.circle_section(.014, 3), [(.05 + .08 * math.cos(2 * math.pi * i / 7), y0 - .1 - nlinks * .085 + .08 * math.sin(2 * math.pi * i / 7), 0.0)
                                               for i in range(7)], F_RUST, closed=True)
    else:
        sk.tube(t, [(.05, -.62, 0), (.05, -1.2, 0)], .02, F_RUST, 3)
    yaw = math.atan2(side[0], side[2])
    g.extend(t.transformed(mul(mk.translate(x, y, z), rot(0, 1, 0, yaw))))


def winch(g, T, at):
    """A crab winch: two timber cheeks on a sill, the drum wound with rope, a gear wheel and crank handles."""
    x, z = at
    t = mk.Geo()
    for sx in (-.42, .42):
        sk.box(t, sx - .05, sx + .05, 0, .85, -.32, .32, F_TIMBER, T, bevel=.012, cell=0)
    if T.i == 2:
        sk.cylinder(t, (-.37, .55, 0), (.37, .55, 0), .19, F_IRON, 5)
        g.extend(t.transformed(mul(mk.translate(x, 0, z), rot(0, 1, 0, .3))))
        return
    sk.box(t, -.5, .5, 0, .12, -.4, -.28, F_TIMBER_X, T, bevel=.01, cell=0)
    sk.box(t, -.5, .5, 0, .12, .28, .4, F_TIMBER_X, T, bevel=.01, cell=0)
    nd = (9, 6, 4)[T.i]
    sk.lathe(t, [(.16, -.37), (.2, -.37), (.2, -.33), (.17, -.33), (.17, .33), (.2, .33), (.2, .37), (.16, .37)], (0, .55, 0), F_IRON,
             nd, axis=(1, 0, 0))
    if T.i < 2:
        sk.lathe(t, [(.172, -.32), (.18, -.2), (.176, 0), (.18, .2), (.172, .32)], (0, .55, 0), F_ROPE, nd, axis=(1, 0, 0))
    sk.cylinder(t, (-.62, .55, 0), (.62, .55, 0), .03, F_IRON, 4)
    sk.lathe(t, [(.05, -.03), (.32, -.03), (.32, .03), (.05, .03)], (.52, .55, 0), F_IRON, (12, 8, 6)[T.i], axis=(1, 0, 0),
             smooth=False)
    for sx, ang in ((-1, .6), (1, 2.2)):
        e = (sx * .62, .55, 0)
        c = (sx * .62, .55 + math.sin(ang) * .3, math.cos(ang) * .3)
        sk.cylinder(t, e, c, .022, F_IRON, 4, caps=False)
        sk.cylinder(t, c, (c[0] + sx * .2, c[1], c[2]), .025, F_TIMBER_X, 4)
    g.extend(t.transformed(mul(mk.translate(x, 0, z), rot(0, 1, 0, .3))))


# ---------------------------------------------------------------------------------------------------------------------------
# Props: cut blocks, the mine cart and its rail, a ladder
# ---------------------------------------------------------------------------------------------------------------------------
def cut_block(g, T, at, size, yaw, f, r, roll=0.0, pitch=0.0, y=None, drill=True, wedges=0):
    """A cut quarry block (bevelled), line-drill traces down one long face; `wedges` iron wedges driven into a split line
    across its top. at: (x, z) of its centre over the floor (it rests on its lowest corner when turned)."""
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    t = mk.Geo()
    sk.box(t, -hx, hx, -hy, hy, -hz, hz, f, T, bevel=.035, cell=0)
    if T.i == 0 and drill:
        m = int(2 * hx / .2)
        for q in range(m):
            xg = -hx + .1 + (2 * hx - .2) * (q + .5) / m
            t.face(((xg - .015, -hy + .06, hz + .004), (xg + .015, -hy + .06, hz + .004), (xg + .015, hy - .04, hz + .004),
                    (xg - .015, hy - .04, hz + .004)), F_DRILL, normal=(0, 0, 1))
    if wedges:
        t.face(((-hx + .04, hy + .004, -.02), (hx - .04, hy + .004, -.02), (hx - .04, hy + .004, .02), (-hx + .04, hy + .004, .02)),
               F_CHAR, normal=(0, 1, 0))
        for q in range(wedges):
            xw = -hx + .2 + (2 * hx - .4) * q / max(1, wedges - 1)
            wedge(t, T, (xw, hy, 0.0), float(r.uniform(.05, .09)))
    m = mul(rot(0, 1, 0, yaw), rot(0, 0, 1, roll), rot(1, 0, 0, pitch))
    A = np.array(m).reshape(3, 4)
    corners = np.array([[sx * hx, sy * hy, sz * hz] for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)])
    low = (corners @ A[:, :3].T)[:, 1].min()
    yy = (-low if y is None else y)
    g.extend(t.transformed(mul(mk.translate(at[0], yy, at[1]), m)))


def wedge(t, T, at, up):
    """An iron wedge driven into a split line (its head `up` proud of the stone) between its two feathers."""
    x, y, z = at
    w, L = .028, .1
    a = [(x - L / 2, y + up, z - w), (x + L / 2, y + up, z - w), (x + L / 2, y + up, z + w), (x - L / 2, y + up, z + w)]
    b = [(x - L / 2 + .01, y - .02, z), (x + L / 2 - .01, y - .02, z)]
    t.face(a, F_IRON, normal=(0, 1, 0))
    t.face((a[0], a[1], b[1], b[0]), F_IRON, normal=(0, .3, -1))
    t.face((a[3], b[0], b[1], a[2]), F_IRON, normal=(0, .3, 1))
    t.face((a[0], b[0], a[3]), F_IRON, normal=(-1, 0, 0))
    t.face((a[1], a[2], b[1]), F_IRON, normal=(1, 0, 0))
    if T.i == 0 and up > .01:
        for sz in (-1, 1):
            sk.box(t, x - L / 2 - .01, x + L / 2 + .01, y - .01, y + up * .55, z + sz * w - .005, z + sz * (w + .008), F_RUST, None,
                   bevel=0, cell=0)


def props(g, T):
    r = mk.rng(91)
    # the stack on its skids, by the derrick
    sx, sz = -8.3, -6.4
    for dz in (-.55, .55):
        sk.box(g, sx - 1.3, sx + 1.3, 0, .16, sz + dz - .09, sz + dz + .09, F_TIMBER_X, T, bevel=.012, cell=0)
    cut_block(g, T, (sx - .62, sz), (1.15, .95, 1.6), .03, F_CUT, r, y=.16 + .475)
    cut_block(g, T, (sx + .62, sz + .05), (1.1, .9, 1.55), -.04, F_COL, r, y=.16 + .45)
    cut_block(g, T, (sx - .1, sz - .05), (1.5, .8, 1.25), .5, F_CUT, r, y=.16 + .95 + .4 + .005)
    # the tipped block, resting on a chock in front of the buttress
    cut_block(g, T, (7.6, -5.4), (.7, .5, .6), .4, F_COL_DK, r, drill=False)
    cut_block(g, T, (8.2, -5.1), (1.9, 1.0, 1.1), -.35, F_COL, r, roll=.42, y=.72)
    # the block being split by its wedges, a sledge leaning on it, loose wedges
    cut_block(g, T, (-1.7, -7.9), (1.8, .95, 1.05), .12, F_CUT, r, wedges=(5, 5, 3)[T.i], y=.475)
    if T.i < 2:
        sk.cylinder(g, (-.75, .02, -7.3), (-.55, .9, -7.52), .02, F_TIMBER, 4)
        sk.box(g, -.66, -.44, .86, .98, -7.59, -7.45, F_IRON, T, bevel=.01, cell=0)
        for k, (x, z, a) in enumerate(((-.4, -6.9, .4), (-.9, -6.7, 1.9), (-2.9, -7.2, 2.6))):
            t = mk.Geo()
            wedge(t, T, (0, 0, 0), .0)
            g.extend(t.transformed(mul(mk.translate(x, .025, z), rot(0, 1, 0, a), rot(0, 0, 1, 1.45))))
    rail(g, T, r)
    cart(g, T, r)
    ladder(g, T, (2.6, BZ + .1), BY[1] + .9)


def rail(g, T, r):
    """A short spur of narrow-gauge track: timber sleepers, two iron rails (one torn up and bent at the broken end)."""
    (ax, az), (bx, bz) = RAIL
    L = math.hypot(bx - ax, bz - az)
    d = ((bx - ax) / L, (bz - az) / L)
    n = (-d[1], d[0])
    k = int(L / .62)
    for i in range(k):
        if T.i == 2 and i % 2:
            continue
        u = (i + .5) * L / k + float(r.uniform(-.06, .06))
        c = (ax + d[0] * u, az + d[1] * u)
        a = math.atan2(d[0], d[1]) + float(r.uniform(-.08, .08))
        t = mk.Geo()
        sk.box(t, -.52, .52, -.02, .09, -.08, .08, F_TIMBER_X, T, bevel=.01 if T.i == 0 else 0, cell=0)
        g.extend(t.transformed(mul(mk.translate(c[0], 0, c[1]), rot(0, 1, 0, a + math.pi / 2))))
    for side in (-1, 1):
        off = side * .3
        p0 = (ax + n[0] * off, .14, az + n[1] * off)
        if side < 0:
            pts = [p0, (bx + n[0] * off - d[0] * 1.2, .14, bz + n[1] * off - d[1] * 1.2),
                   (bx + n[0] * off - d[0] * .55, .3, bz + n[1] * off - d[1] * .55), (bx + n[0] * (off - .2), .62, bz + n[1] * (off - .2))]
        else:
            pts = [p0, (bx + n[0] * off - d[0] * .4, .14, bz + n[1] * off - d[1] * .4)]
        sk.sweep(g, [(-.026, -.05), (.026, -.05), (.012, -.025), (.012, .03), (.028, .05), (-.028, .05), (-.012, .03), (-.012, -.025)]
                 if T.i == 0 else sk.rect_section(.05, .1), pts,
                 F_IRON, up=(0, 1, 0), smooth_u=False)


def cart(g, T, r):
    """A tipping mine cart off its rails: an iron tub on its frame, one wheel gone, rubble spilled."""
    x, z = CART
    t = mk.Geo()
    Lo, Wo, H = .66, .46, .62
    Lb, Wb = .54, .36
    th = .03
    y0 = .3

    def ring(l, w, y):
        return [(-w, y, -l), (w, y, -l), (w, y, l), (-w, y, l)]
    ob, ot = ring(Lb, Wb, y0), ring(Lo, Wo, y0 + H)
    ib, it = ring(Lb - th, Wb - th, y0 + th), ring(Lo - th, Wo - th, y0 + H)
    for i in range(4):
        j = (i + 1) % 4
        q = (ob[i], ob[j], ot[j], ot[i])
        nn = sk._norm(sk._cross(sk._sub(q[1], q[0]), sk._sub(q[3], q[0])))
        mid = sk._lerp(ob[i], ot[j], .5)
        if sk._dot(nn, (mid[0], 0, mid[2])) < 0:
            nn = (-nn[0], -nn[1], -nn[2])
        t.face(q, F_RUST, normal=nn)
        t.face((ib[i], ib[j], it[j], it[i]), F_RUST, normal=(-nn[0], -nn[1], -nn[2]))
        t.face((ot[i], ot[j], it[j], it[i]), F_IRON, normal=(0, 1, 0))
    t.face(ob, F_RUST, normal=(0, -1, 0))
    t.face(ib, F_RUST, normal=(0, 1, 0))
    if T.i == 0:
        for i in range(4):
            j = (i + 1) % 4
            a, b = ot[i], ot[j]
            sk.sweep(t, sk.rect_section(.035, .06), [(a[0] * 1.02, a[1] - .05, a[2] * 1.02), (b[0] * 1.02, b[1] - .05, b[2] * 1.02)],
                     F_IRON, up=(0, 1, 0), smooth_u=False)
    for sx in (-1, 1):
        sk.box(t, sx * .3 - .04, sx * .3 + .04, .16, .3, -.62, .62, F_IRON, T, bevel=.008, cell=0)
    nw = (8, 6, 5)[T.i]
    prof = [(.03, -.04), (.15, -.04), (.17, .015), (.17, .045), (.03, .045)] if T.i == 0 else [(.03, -.04), (.16, -.04), (.16, .045), (.03, .045)]
    for (sx, sz) in ((-1, -1), (1, -1), (-1, 1)):
        sk.lathe(t, prof if sx < 0 else [(p[0], -p[1]) for p in prof][::-1], (sx * .4, .16, sz * .42), F_IRON, nw, axis=(1, 0, 0),
                 smooth=False)
    sk.cylinder(t, (-.45, .16, -.42), (.45, .16, -.42), .025, F_IRON, 4)
    g.extend(t.transformed(mul(mk.translate(x, -.06, z), rot(0, 1, 0, .45), rot(0, 0, 1, .2), rot(1, 0, 0, -.06))))
    if T.i < 2:
        w = mk.Geo()
        sk.lathe(w, prof, (0, 0, 0), F_IRON, nw, axis=(0, 1, 0), smooth=False)
        g.extend(w.transformed(mk.translate(x + .95, .04, z + .7)))
    for k in range((5, 3, 1)[T.i]):
        a = float(r.uniform(-.6, .9))
        d = float(r.uniform(.5, 1.3))
        mk.rock(g, (x + .6 + math.cos(a) * d * .5, -.01, z - .5 - math.sin(a) * d), (float(r.uniform(.08, .2)), float(r.uniform(.05, .12)),
                float(r.uniform(.07, .18))), F_RUBBLE, T, seed=1300 + k, rough=.3, level=0, turn=a)


def ladder(g, T, at, top):
    """A timber ladder leaning on the first bench."""
    x, z = at
    lean = .32
    t = mk.Geo()
    for sx in (-.24, .24):
        sk.box(t, sx - .035, sx + .035, 0, top, -.03, .03, F_TIMBER, T, bevel=.008 if T.i == 0 else 0, cell=0)
    k = int(top / (.3, .3, .6)[T.i])
    for i in range(1, k):
        y = i * top / k
        sk.cylinder(t, (-.24, y, 0), (.24, y, 0), .018, F_TIMBER_X, (4, 3, 3)[T.i], caps=False)
    g.extend(t.transformed(mul(mk.translate(x, 0, z + lean * top * .95), rot(1, 0, 0, -lean))))


# ---------------------------------------------------------------------------------------------------------------------------
# Rubble at the walls' feet
# ---------------------------------------------------------------------------------------------------------------------------
def rubble(g, T):
    r = mk.rng(101)
    piles = ((-6.9, -9.0, .8, 5), (-10.0, -1.6, 1.0, 5), (10.0, 3.0, .9, 4), (8.9, -6.85, .7, 4),
             (-2.6, 10.0, .7, 3), (.9, -9.2, .55, 3), (10.0, -6.0, .6, 3), (-9.4, 6.1, .7, 3))
    for k, (x, z, s, m) in enumerate(piles):
        if T.i == 2 and s < .8:
            continue
        mk.rock(g, (x, -.03, z), (s, s * .42, s * .75), F_SCREE, T, seed=1400 + k, rough=.3, level=(1 if s > .75 else 0, 0, 0)[T.i],
                turn=float(r.uniform(0, 3)))
        for q in range((m - 1, 1, 0)[T.i]):
            a = float(r.uniform(0, 2 * math.pi))
            d = s * float(r.uniform(.7, 1.5))
            sz = float(r.uniform(.1, .28))
            mk.rock(g, (x + math.cos(a) * d, -.02, z + math.sin(a) * d * .8), (sz, sz * float(r.uniform(.5, .8)), sz * float(r.uniform(.7, 1.1))),
                    F_RUBBLE, T, seed=1450 + k * 11 + q, rough=.3, level=0, turn=a)


# ---------------------------------------------------------------------------------------------------------------------------
# Far: the ridge, nearer hills under a ragged forest edge, pines on the quarry's rim
# ---------------------------------------------------------------------------------------------------------------------------
def pine(g, T, x, y, z, h, rad):
    """A spruce in silhouette: a short trunk, two drooping tiers and a spire."""
    sides = (5, 4, 3)[T.i]
    sk.lathe(g, [(rad * .1, 0), (rad * .1, h * .12), (rad, h * .2), (rad * .45, h * .48), (rad * .72, h * .5), (0, h)], (x, y, z),
             F_PINE, sides, smooth=False)


def far(g, T):
    r = mk.rng(111)
    for (z0, hlo, hhi, span, n, f, seed, trees) in ((78.0, 9.0, 26.0, 140.0, (30, 20, 12)[T.i], F_FAR, 3, 0),
                                                    (50.0, 2.5, 8.5, 90.0, (22, 14, 8)[T.i], F_FAR2, 5, (44, 22, 0)[T.i])):
        xs = [-span + 2 * span * i / n + (float(r.uniform(-.3, .3)) * span / n if 0 < i < n else 0.0) for i in range(n + 1)]
        hs = []
        for x in xs:
            a = float(sk.fbm(np.array([[x * .025, 0, seed * 1.3]]), 1.0, 3, seed)[0])
            b = float(sk.fbm(np.array([[x * .11, 0, seed * 2.1]]), 1.0, 2, seed + 9)[0])
            hs.append(hlo + (hhi - hlo) * max(0.0, (a - .3) * 1.9 + (b - .5) * .7 + .15))
        zz = [z0 + 7 * math.sin(x * .045 + seed) for x in xs]
        for i in range(n):
            a = (xs[i], -1.5, zz[i])
            b = (xs[i + 1], -1.5, zz[i + 1])
            c = (xs[i + 1], hs[i + 1], zz[i + 1])
            d = (xs[i], hs[i], zz[i])
            g.face((a, b, c, d), f, normal=(0, 0, -1))
            g.face((d, c, (c[0], c[1] * .7, c[2] + 20), (d[0], d[1] * .7, d[2] + 20)), f, normal=(0, 1, 0))
        # a ragged forest edge along the nearer hills: flat silhouettes turned to the pit
        for k in range(trees):
            i = int(r.integers(n))
            u = float(r.uniform(0, 1))
            x = xs[i] + (xs[i + 1] - xs[i]) * u
            y = hs[i] + (hs[i + 1] - hs[i]) * u - .3
            z = zz[i] + (zz[i + 1] - zz[i]) * u + .5
            h = float(r.uniform(3.5, 8.0))
            w = h * float(r.uniform(.28, .4))
            g.face(((x - w / 2, y, z), (x + w / 2, y, z), (x, y + h, z)), F_PINE, normal=(0, 0, -1))
    # pines along the quarry's rim (the top bench's terrace)
    for k in range((5, 3, 0)[T.i]):
        side = k % 3
        if side == 0:
            x, z = float(r.uniform(-X_FAR + 2, X_FAR - 2)), float(r.uniform(ZB_FAR + 2, BZ - BO[4] - 3.0))
        elif side == 1:
            x, z = float(r.uniform(SX + BO[4] + 2.5, X_FAR - 1)), float(r.uniform(ZB_FAR + 2, ZS[4] - 1))
        else:
            x, z = -float(r.uniform(SX + BO[4] + 2.5, X_FAR - 1)), float(r.uniform(ZB_FAR + 2, ZS[4] - 1))
        pine(g, T, x, BY[5] - .2, z, float(r.uniform(6.0, 9.5)), float(r.uniform(1.5, 2.2)))
