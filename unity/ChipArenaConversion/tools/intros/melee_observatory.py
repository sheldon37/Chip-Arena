"""The Glaive's set (Orrery; docs/INTROS-MELEE.md): the Observatory, an open observatory on a mountain summit at twilight. A round
floor of polished stone (radius 7 m) inlaid with brass rings and radial lines (the four cardinal lines faintly lit), a raised dais
at its middle where the pilot stands on a brass compass rose; a stone balustrade on ten columns round the edge (one bay fallen, the
bay toward +z open onto a stair going down the tower's side); on the columns a stone ring beam and ten iron ribs rising to an oculus
ring at 11 m, iron purlins round them and a few glass panes between (one broken, its shards on the floor); a great brass refractor
on a fork mount on the pilot's left (+x), star-chart easels, a celestial globe, a hanging astrolabe and an instrument table;
beyond the balustrade the drop (the tower's cornice and wall, the summit's crags, a sea of cloud) and two mountain ranges.

The orrery's moving parts: four armillary rings (rigs `arm-ring-0..3`: brass hoops of radius 2.6 to 4.4 m with a lit inlay round
their outer and inner faces, pivot bosses and ferrules; each in its own x-z plane, at rest level over the pilot at (0, 2.4, 0)) and
one free ring-blade (`ringblade`: an original look-alike of the Glaive's, its ring in its own x-y plane, at rest at (0, 1.5, -2.5)).
Set frame: melee_kit's (the pilot at the origin facing +z; the dais's top is y 0, the main floor 0.15 m lower)."""
import math

import numpy as np

import melee_kit as mk
from melee_kit import sk

TAU = 2 * math.pi

# ---- finishes ----
F_SLATE = mk.fin('obs.slate', 0x4d5466, sk.DETAIL_MARBLE, smooth=.78, dirt=.45, wear=.3)
F_SLATE_DK = mk.fin('obs.slate.dark', 0x3e4455, sk.DETAIL_MARBLE, smooth=.8, dirt=.5, moss=.25, wear=.25)
F_PALE = mk.fin('obs.pale', 0x948d80, sk.DETAIL_MARBLE, smooth=.72, dirt=.55, wear=.3)
F_LAPIS = mk.fin('obs.lapis', 0x34406c, sk.DETAIL_MARBLE, smooth=.85, dirt=.3, wear=.2)
F_BRASS = mk.fin('obs.brass', 0xb08c4a, sk.DETAIL_BRASS, smooth=.7, metal=.8, dirt=.3, wear=.3)
F_BRASS_DK = mk.fin('obs.brass.dark', 0x866a3c, sk.DETAIL_BRASS, smooth=.6, metal=.75, dirt=.5, wear=.35)
F_VERD = mk.fin('obs.verdigris', 0x4f7d72, sk.DETAIL_BRASS, smooth=.4, metal=.35, dirt=.6, wear=.3)
F_LINE = mk.fin('obs.line.glow', 0x86e0e6, glow=.7)
F_JOINT = mk.fin('obs.joint', 0x303238, smooth=.3, dirt=.4)
F_KERB = mk.fin('obs.kerb', 0x6c6966, sk.DETAIL_CARVED, smooth=.55, dirt=.8, wear=.35)
F_STONE = mk.fin('obs.stone', 0x9a948a, sk.DETAIL_CARVED, smooth=.35, dirt=1.0, moss=.2, wear=.35)
F_STONE_DK = mk.fin('obs.stone.dark', 0x77736c, sk.DETAIL_CARVED, smooth=.3, dirt=1.1, moss=.35, wear=.3)
F_IRON = mk.fin('obs.iron', 0x3e444e, sk.DETAIL_IRON, smooth=.45, metal=.6, dirt=.6, wear=.2)
F_IRON_CAST = mk.fin('obs.iron.cast', 0x363b43, sk.DETAIL_IRON, smooth=.4, metal=.55, dirt=.7, wear=.25)
F_GLASS = mk.fin('obs.glass.glow', 0x5d8fa6, smooth=.9, metal=.1, dirt=0, glow=.3)
F_SHARD = mk.fin('obs.glass.shard', 0x7fa6b8, smooth=.92, metal=.1, dirt=0)
F_LENS = mk.fin('obs.lens', 0x2a3644, smooth=.95, metal=.2, dirt=0)
F_TOWER = mk.fin('obs.tower', 0x6f6b66, sk.DETAIL_ASHLAR, smooth=.25, dirt=1.2, moss=.4)
F_CRAG = mk.fin('obs.crag', 0x4d4c53, sk.DETAIL_CARVED, smooth=.2, dirt=.8, moss=.3)
F_FAR_NEAR = mk.fin('obs.far.near', 0x343a4c, smooth=.1, dirt=0)
F_FAR = mk.fin('obs.far', 0x474e64, smooth=.1, dirt=0)
F_SNOW = mk.fin('obs.far.snow', 0x8d93aa, smooth=.3, dirt=0)
F_CLOUD = mk.fin('obs.cloud', 0x7b7697, smooth=.1, dirt=0)
F_CLOUD_LT = mk.fin('obs.cloud.light', 0x9a92b0, smooth=.1, dirt=0)
F_WOOD = mk.fin('obs.wood', 0x5a3f2c, sk.DETAIL_WOOD, smooth=.45, dirt=.6, param=1, wear=.25)
F_WOOD_H = mk.fin('obs.wood.h', 0x5a3f2c, sk.DETAIL_WOOD, smooth=.45, dirt=.6, param=0, wear=.25)
F_CHART = mk.fin('obs.chart.glow', 0x2a3c74, glow=.35)
F_STAR = mk.fin('obs.star.glow', 0xdfeeff, glow=2.4)
F_CONST = mk.fin('obs.const.glow', 0x7fb4ff, glow=1.1)
F_PAPER = mk.fin('obs.paper', 0xa89f86, smooth=.2, dirt=.4)
F_GLOBE = mk.fin('obs.globe', 0x30406c, smooth=.6, dirt=.3)
F_BOOK = mk.fin('obs.book', 0x6a2f2a, sk.DETAIL_CLOTH, smooth=.3, dirt=.5)
# the orrery's rigs
F_RING = mk.fin('obs.ring.brass', 0xb6924e, sk.DETAIL_BRASS, smooth=.75, metal=.85, dirt=.15, wear=.2)
F_INLAY = mk.fin('obs.ring.inlay.glow', 0x5fe3dc, glow=1.3)
F_BL_STEEL = mk.fin('obs.blade.steel', 0xa9b2bf, smooth=.78, metal=.85, dirt=.1, wear=.2)
F_BL_PLATE = mk.fin('obs.blade.plate', 0x7a8394, smooth=.6, metal=.8, dirt=.15)
F_BL_DARK = mk.fin('obs.blade.dark', 0x33373f, sk.DETAIL_IRON, smooth=.5, metal=.6, dirt=.2)
F_BL_EDGE = mk.fin('obs.blade.edge.glow', 0xa6f2ff, glow=2.4)
F_BL_CORE = mk.fin('obs.blade.core.glow', 0x7fd8ff, glow=1.6)
F_BL_WRAP = mk.fin('obs.blade.wrap', 0x3a2d28, sk.DETAIL_CLOTH, smooth=.2, dirt=.3)

# ---- the plan ----
FY = -.15                     # the main floor's top (the dais's top is y 0)
DAIS_R = 2.2
FLOOR_R = 6.95                # the floor's stones end at the balustrade's plinth
BAL_R = 7.15                  # the balustrade's and the columns' centre line
COLS = 10
COL_A0 = math.radians(72)     # column k at COL_A0 + k * 36 degrees; bay k runs from column k to column k + 1
GAP_BAY = 0                   # 72..108 degrees: open toward +z, the stair
BROKEN_BAY = 4                # 216..252 degrees: fallen
PED = .34                     # the pedestals' half width
RB_Y0, RB_Y1 = 3.48, 3.78     # the ring beam on the columns' abaci
OCU_Y, OCU_R = 11.0, 1.75     # the oculus ring (its centre line)
PURLIN_Y = (6.9, 9.4)
GLAZED = ((2, 1), (3, 0), (5, 1), (6, 0), (8, 0), (9, 1))   # (bay, 0 lower / 1 upper pane)
BROKEN_PANE = 8
RING_RADII = (2.6, 3.2, 3.8, 4.4)
RING_REST_Y = 2.4
BLADE_REST = (0.0, 1.5, -2.5)
TEL_C = (5.2, 1.95, -1.2)     # the refractor's trunnions (its balance point)
TEL_AZ, TEL_EL = math.radians(-60), math.radians(42)
STAIR_HALF = 1.3


def cols():
    return [COL_A0 + k * TAU / COLS for k in range(COLS)]


def build(T):
    s = mk.SetPiece('observatory')
    g = mk.Geo()
    tl = mk.Tally(g)
    dais(g, T)
    tl('dais')
    floor(g, T)
    tl('floor')
    balustrade(g, T)
    tl('balustrade')
    dome(g, T)
    tl('dome')
    telescope(g, T)
    tl('telescope')
    props(g, T)
    tl('props')
    stair(g, T)
    drop(g, T)
    tl('drop+stair')
    far(g, T)
    tl('far')
    s.tally = tl
    s.static[T.i] = g
    for k in range(4):
        s.rig('arm-ring-%d' % k, T.i, arm_ring(T, k), mk.translate(0.0, RING_REST_Y, 0.0))
    s.rig('ringblade', T.i, ringblade(T), mk.translate(*BLADE_REST))
    s.views = [
        ('front', (0.0, 1.6, 6.5), (0.0, 2.0, -2.0), 50),
        ('top', (0.0, 27.0, 1.6), (0.0, 0.0, 0.0), 56),
        ('low', (1.1, .25, 2.4), (-.4, 6.0, -1.2), 80),
        ('gap', (0.0, 1.7, 8.4), (0.0, 1.3, 0.0), 58),
        ('reverse', (.4, 1.7, -4.8), (0.0, 1.0, 8.0), 60),
        ('telescope', (2.2, 1.8, 1.8), (5.4, 2.0, -1.8), 55),
        ('side', (-6.0, 2.1, 2.6), (1.5, 1.7, -1.6), 62),
        ('outside', (15.0, 5.0, 16.0), (0.0, 2.5, 0.0), 50),
    ]
    return s


# ---------------------------------------------------------------------------------------------------------------------------
# Helpers: polar places, rings swept round the vertical axis, sections with their own normals
# ---------------------------------------------------------------------------------------------------------------------------
def P(r, a, y):
    return (r * math.cos(a), y, r * math.sin(a))


def rad(a, k=1.0):
    return (math.cos(a) * k, 0.0, math.sin(a) * k)


def hoop(g, R, strips, n, y=0.0, a0=0.0, a1=TAU, cx=0.0, cz=0.0):
    """A ring round the vertical axis through (cx, y, cz), radius R, swept from angle a0 to a1 in n steps. strips: the
    section's edges [((dr, dh, nr, nh), (dr, dh, nr, nh), finish)], each corner an offset (out, up) from (R, y) with its normal
    in the (out, up) plane: a box's chamfers take their faces' normals, so the faces stay flat and share their corners."""
    cs = [(math.cos(a0 + (a1 - a0) * k / n), math.sin(a0 + (a1 - a0) * k / n)) for k in range(n + 1)]
    for p, q, f in strips:
        for k in range(n):
            (c0, s0), (c1, s1) = cs[k], cs[k + 1]
            g.face(((cx + (R + p[0]) * c0, y + p[1], cz + (R + p[0]) * s0), (cx + (R + p[0]) * c1, y + p[1], cz + (R + p[0]) * s1),
                    (cx + (R + q[0]) * c1, y + q[1], cz + (R + q[0]) * s1), (cx + (R + q[0]) * c0, y + q[1], cz + (R + q[0]) * s0)), f,
                   normals=((p[2] * c0, p[3], p[2] * s0), (p[2] * c1, p[3], p[2] * s1), (q[2] * c1, q[3], q[2] * s1), (q[2] * c0, q[3], q[2] * s0)))


def hoop_caps(g, R, strips, a0, a1, y=0.0):
    """End faces for an arc of hoop() (its section a convex polygon)."""
    pts = convex([(round(p[0], 7), round(p[1], 7)) for p, q, f in strips] + [(round(q[0], 7), round(q[1], 7)) for p, q, f in strips])
    f = strips[0][2]
    for a, s in ((a0, -1), (a1, 1)):
        c, sn = math.cos(a), math.sin(a)
        g.face([((R + x) * c, y + h, (R + x) * sn) for x, h in pts], f, normal=(-sn * s, 0, c * s))


def sec_box(w, h, ch, f, x0=0.0, y0=0.0, skip=()):
    """A rectangle's edges (w out, h up, centred on (x0, y0)) for hoop / sweep_plane, its corners chamfered by ch (the chamfers
    take their faces' normals). skip: faces left out ('bottom', 'top', 'out', 'in')."""
    a, b = w / 2, h / 2
    c = min(ch, a * .45, b * .45)
    pts = [(x0 - a + c, y0 - b, 0, -1), (x0 + a - c, y0 - b, 0, -1), (x0 + a, y0 - b + c, 1, 0), (x0 + a, y0 + b - c, 1, 0),
           (x0 + a - c, y0 + b, 0, 1), (x0 - a + c, y0 + b, 0, 1), (x0 - a, y0 + b - c, -1, 0), (x0 - a, y0 - b + c, -1, 0)]
    names = ('bottom', 'c', 'out', 'c', 'top', 'c', 'in', 'c')
    out = []
    for i in range(8):
        if names[i] in skip:
            continue
        if names[i] == 'c' and (c <= 0 or names[i - 1] in skip or names[(i + 1) % 8] in skip):
            continue
        out.append((pts[i], pts[(i + 1) % 8], f))
    return out


def sec_soft(w, h, f, x0=0.0, y0=0.0):
    """A rectangle's four corners shaded round (the Low tier's cheap beam)."""
    a, b, k = w / 2, h / 2, .7071
    q = [(x0 + a, y0 - b, k, -k), (x0 + a, y0 + b, k, k), (x0 - a, y0 + b, -k, k), (x0 - a, y0 - b, -k, -k)]
    return [(q[i], q[(i + 1) % 4], f) for i in range(4)]


def sec_poly(pts, fins):
    """A polygon's edges (pts [(out, up)] counter-clockwise), each with its own flat normal (crisp corners). fins: a finish or
    one per edge."""
    out = []
    k = len(pts)
    for i in range(k):
        a, b = pts[i], pts[(i + 1) % k]
        dx, dy = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(dx, dy)
        if ln < 1e-9:
            continue
        nx, ny = dy / ln, -dx / ln
        f = fins[i] if isinstance(fins, (list, tuple)) else fins
        out.append(((a[0], a[1], nx, ny), (b[0], b[1], nx, ny), f))
    return out


def sec_round(r, k, f, phase=0.0):
    """A round section of k sides, shaded round."""
    pts = [(r * math.cos(phase + TAU * i / k), r * math.sin(phase + TAU * i / k), math.cos(phase + TAU * i / k), math.sin(phase + TAU * i / k)) for i in range(k)]
    return [(pts[i], pts[(i + 1) % k], f) for i in range(k)]


def sweep_plane(g, strips, path, side, caps=False):
    """A section swept along a path lying in a plane: the section's first axis is across the path in the plane, its second
    `side` (the plane's unit normal). strips as for hoop()."""
    m = len(path)
    across = []
    for i in range(m):
        a, b = path[max(0, i - 1)], path[min(m - 1, i + 1)]
        t = sk._norm(sk._sub(b, a))
        across.append(sk._norm(sk._cross(t, side)))

    def at(i, p):
        return sk._add(sk._add(path[i], across[i], p[0]), side, p[1])

    def nrm(i, p):
        return sk._norm(sk._add(sk._lerp((0, 0, 0), across[i], p[2]), side, p[3]))
    for p, q, f in strips:
        for i in range(m - 1):
            g.face((at(i, p), at(i + 1, p), at(i + 1, q), at(i, q)), f, normals=(nrm(i, p), nrm(i + 1, p), nrm(i + 1, q), nrm(i, q)))
    if caps:
        pts = []
        for p, q, f in strips:
            if not pts or abs(pts[-1][0] - p[0]) + abs(pts[-1][1] - p[1]) > 1e-7:
                pts.append(p)
        for i, s in ((0, -1), (m - 1, 1)):
            j = min(m - 1, i + 1) if s < 0 else max(0, i - 1)
            t = sk._norm(sk._sub(path[i], path[j]))
            g.face([at(i, p) for p in pts], strips[0][2], normal=t)


def flat(g, pts, f, toward=None):
    """A flat face, turned to face `toward` (a direction) when given."""
    n = (0.0, 0.0, 0.0)
    for i in range(1, len(pts) - 1):
        n = sk._add(n, sk._cross(sk._sub(pts[i], pts[0]), sk._sub(pts[i + 1], pts[0])))
    if toward is not None and sk._dot(n, toward) < 0:
        n = (-n[0], -n[1], -n[2])
    g.face(pts, f, normal=n)


def yaw(a):
    """The turn about y taking local +x to the direction of angle a ((cos a, 0, sin a)) and local +z to its tangent."""
    return sk.quat_axis(0, 1, 0, -a)


def face_to(dx, dz):
    """The turn about y taking local +z to the direction (dx, 0, dz)."""
    return sk.quat_axis(0, 1, 0, math.atan2(dx, dz))


def put(g, t, q, at):
    g.extend(t.transformed(sk.affine(q, at)))


# ---------------------------------------------------------------------------------------------------------------------------
# The floor: a stone in polar form, the dais, the main floor's bands, the brass between them
# ---------------------------------------------------------------------------------------------------------------------------
def panel(g, r0, r1, ai, ao, y1, yb, f, n, b):
    """A floor stone: its inner edge at radius r0 from angle ai[0] to ai[1], its outer at r1 from ao[0] to ao[1] (its ends
    straight), its top at y1, the top's edges chamfered by b down to yb (where the joints' brass lies; none on Low)."""
    up = (0.0, 1.0, 0.0)
    if b > 0:
        ri, ro = r0 + b, r1 - b
        ti = [ai[0] + b / ri + (ai[1] - ai[0] - 2 * b / ri) * k / n for k in range(n + 1)]
        to = [ao[0] + b / ro + (ao[1] - ao[0] - 2 * b / ro) * k / n for k in range(n + 1)]
    else:
        ri, ro = r0, r1
        ti = [ai[0] + (ai[1] - ai[0]) * k / n for k in range(n + 1)]
        to = [ao[0] + (ao[1] - ao[0]) * k / n for k in range(n + 1)]
    for k in range(n):
        g.face((P(ri, ti[k], y1), P(ri, ti[k + 1], y1), P(ro, to[k + 1], y1), P(ro, to[k], y1)), f, normal=up)
    if b <= 0:
        return
    ei = [ai[0] + (ai[1] - ai[0]) * k / n for k in range(n + 1)]
    eo = [ao[0] + (ao[1] - ao[0]) * k / n for k in range(n + 1)]
    for k in range(n):
        g.face((P(ro, to[k], y1), P(ro, to[k + 1], y1), P(r1, eo[k + 1], yb), P(r1, eo[k], yb)), f,
               normals=(up, up, rad(eo[k + 1]), rad(eo[k])))
        g.face((P(ri, ti[k + 1], y1), P(ri, ti[k], y1), P(r0, ei[k], yb), P(r0, ei[k + 1], yb)), f,
               normals=(up, up, rad(ei[k], -1), rad(ei[k + 1], -1)))
    for (pi_, po_, e_i, e_o, s) in ((ti[0], to[0], ei[0], eo[0], -1), (ti[-1], to[-1], ei[-1], eo[-1], 1)):
        A, B = P(r0, e_i, yb), P(r1, e_o, yb)
        nn = sk._norm(sk._cross(sk._sub(B, A), (0, 1, 0)))
        am = (e_i + e_o) / 2
        tang = (-math.sin(am) * s, 0.0, math.cos(am) * s)
        if sk._dot(nn, tang) < 0:
            nn = (-nn[0], 0.0, -nn[2])
        g.face((P(ri, pi_, y1), P(ro, po_, y1), B, A), f, normals=(up, up, nn, nn))


def strip_ring(g, r0, r1, y, n, f, a0=0.0, a1=TAU):
    hoop(g, 0.0, [((r0, 0.0, 0.0, 1.0), (r1, 0.0, 0.0, 1.0), f)], n, y=y, a0=a0, a1=a1)


def radial_joint(g, a, r0, r1, jw, y, f):
    """The flat strip of a radial joint (half width jw) at angle a between radii r0 and r1."""
    g.face((P(r0, a - jw / r0, y), P(r0, a + jw / r0, y), P(r1, a + jw / r1, y), P(r1, a - jw / r1, y)), f, normal=(0, 1, 0))


def dais(g, T):
    """The dais (top at y 0): a lapis medallion with a brass compass rose under the pilot's feet, a zodiac wheel of twelve
    stones, the brass rings between, a kerb with a rounded nosing down to the main floor."""
    b = .012 if T.bevel else 0.0
    yb = -b
    N = (72, 48, 24)[T.i]
    up = (0.0, 1.0, 0.0)
    rm = .86
    # the medallion, its rim chamfered down to the brass
    g.face([P(rm - b, -TAU * k / N, 0.0) for k in range(N)], F_LAPIS, normal=up)
    if b > 0:
        hoop(g, 0.0, [((rm - b, 0.0, 0.0, 1.0), (rm, yb, 1.0, 0.0), F_LAPIS)], N)
    strip_ring(g, rm, .92, yb, N, F_BRASS)
    # the compass rose: eight points, each a light and a dark half; the long four point along the axes (one toward +z)
    for k in range(8):
        a = math.pi / 2 + k * math.pi / 4
        long_ = k % 2 == 0
        L, w = (.8, .085) if long_ else (.5, .06)
        y = .004 if long_ else .002
        tip = P(L, a, y)
        c = (0.0, y, 0.0)
        sl, sr = P(w * 1.4, a + .5 * math.pi / 2, y), P(w * 1.4, a - .5 * math.pi / 2, y)
        g.face((c, tip, sl), F_BRASS, normal=up)
        g.face((c, sr, tip), F_BRASS_DK, normal=up)
    g.face([P(.07, TAU * k / 8, .006) for k in range(8)], F_BRASS, normal=up)
    # the zodiac wheel: twelve stones, light and dark by turns, thin brass joints between
    n = (6, 4, 2)[T.i]
    jw = .006
    r0, r1 = .92, 1.98
    for k in range(12):
        a0, a1 = k * TAU / 12, (k + 1) * TAU / 12
        panel(g, r0, r1, (a0 + jw / r0, a1 - jw / r0), (a0 + jw / r1, a1 - jw / r1), 0.0, yb, F_PALE if k % 2 else F_SLATE, n, b)
        radial_joint(g, a0, r0, r1, jw, yb if b > 0 else .002, F_BRASS)
        if T.detail:
            # a planet's glyph inlaid in each stone: a brass disc with a lapis eye
            am = (a0 + a1) / 2
            gx, gz = 1.5 * math.cos(am), 1.5 * math.sin(am)
            g.face([(gx + .075 * math.cos(TAU * i / 6), .002, gz + .075 * math.sin(TAU * i / 6)) for i in range(6)], F_BRASS, normal=up)
            g.face([(gx + .03 * math.cos(TAU * i / 6 + .5), .004, gz + .03 * math.sin(TAU * i / 6 + .5)) for i in range(6)], F_LAPIS, normal=up)
            bx, bz = 1.5 * math.cos(am) + .14 * math.cos(am), 1.5 * math.sin(am) + .14 * math.sin(am)
            tx, tz = -math.sin(am), math.cos(am)
            g.face(((bx - tx * .07 - .012 * math.cos(am), .002, bz - tz * .07 - .012 * math.sin(am)),
                    (bx + tx * .07 - .012 * math.cos(am), .002, bz + tz * .07 - .012 * math.sin(am)),
                    (bx + tx * .07 + .012 * math.cos(am), .002, bz + tz * .07 + .012 * math.sin(am)),
                    (bx - tx * .07 + .012 * math.cos(am), .002, bz - tz * .07 + .012 * math.sin(am))), F_BRASS, normal=up)
    strip_ring(g, 1.98, 2.04, yb, N, F_BRASS)
    # the kerb: a darker stone, its nosing rounded down to the riser
    kb = .012 if T.bevel else 0.0
    pts = [(2.04, yb, -1.0, 0.0), (2.04 + kb, 0.0, 0.0, 1.0), (2.15, 0.0, 0.0, 1.0), (DAIS_R, -.045, 1.0, 0.0), (DAIS_R, FY - .03, 1.0, 0.0)]
    hoop(g, 0.0, [(pts[i], pts[i + 1], F_KERB) for i in range(4)], N)


def floor(g, T):
    """The main floor (top at FY): four bands of polished stone between brass rings, cut by brass radial joints (twelve from the
    dais to the edge, twelve more in the outer bands); the four cardinal joints faintly lit. A few stones cracked or sunk."""
    b = .012 if T.bevel else 0.0
    yb = FY - b
    r = mk.rng(5)
    jw = .015
    bands = ((2.24, 3.18, 12, (F_SLATE, F_SLATE)), (3.22, 4.38, 12, (F_PALE, F_PALE)), (4.42, 5.68, 24, (F_SLATE, F_SLATE_DK)),
             (5.72, FLOOR_R, 24, (F_SLATE_DK, F_SLATE_DK)))
    rings = ((2.2, 2.24), (3.18, 3.22), (4.38, 4.42), (5.68, 5.72))
    N = (72, 48, 36)[T.i]
    for r0, r1 in rings:
        strip_ring(g, r0, r1, yb, N, F_BRASS)
    cracked = {(2, 14): .37, (3, 19): .62, (3, 3): .48, (2, 5): .55} if T.fine else {}
    sunk = {(3, 9): .007, (2, 20): .005} if T.fine else {}
    for bi, (r0, r1, ns, fins) in enumerate(bands):
        ns = ns if T.i < 2 else 12
        n = N // ns
        for k in range(ns):
            a0, a1 = k * TAU / ns, (k + 1) * TAU / ns
            f = fins[k % 2]
            y1 = FY + (float(r.uniform(-.002, .003)) if T.fine else 0.0) - sunk.get((bi, k), 0.0)
            if b > 0:
                ai, ao = (a0 + jw / r0, a1 - jw / r0), (a0 + jw / r1, a1 - jw / r1)
            else:
                ai, ao = (a0, a1), (a0, a1)
            if (bi, k) in cracked:
                # a crack across the stone: two pieces, the second a little sunk, the joint's dark bed in the crack
                u = cracked[(bi, k)]
                mi = ai[0] + (ai[1] - ai[0]) * (u - .18)
                mo = ao[0] + (ao[1] - ao[0]) * (u + .18)
                cw = .004
                panel(g, r0, r1, (ai[0], mi - cw / r0), (ao[0], mo - cw / r1), y1, yb, f, max(1, n // 2), b)
                panel(g, r0, r1, (mi + cw / r0, ai[1]), (mo + cw / r1, ao[1]), y1 - .006, yb - .004, f, max(1, n // 2), b)
                g.face((P(r0, mi - cw * 2 / r0, yb - .006), P(r0, mi + cw * 2 / r0, yb - .006), P(r1, mo + cw * 2 / r1, yb - .006),
                        P(r1, mo - cw * 2 / r1, yb - .006)), F_JOINT, normal=(0, 1, 0))
            else:
                panel(g, r0, r1, ai, ao, y1, yb, f, n, b)
            # the joint at the stone's start: brass, or lit on the four cardinal lines
            idx30 = k if ns == 12 else (k // 2 if k % 2 == 0 else None)
            lit = idx30 is not None and idx30 % 3 == 0
            jf = F_LINE if lit else F_BRASS
            if b > 0:
                radial_joint(g, a0, r0, r1, jw, yb, jf)
            else:
                radial_joint(g, a0, r0, r1, jw, FY + .003, jf)
    # the bed under the stones (seen only through the joints and the cracks)
    hoop(g, 0.0, [((2.15, 0.0, 0.0, 1.0), (FLOOR_R + .05, 0.0, 0.0, 1.0), F_JOINT)], (32, 24, 16)[T.i], y=FY - .045)


# ---------------------------------------------------------------------------------------------------------------------------
# The balustrade on its ten columns, the ring beam
# ---------------------------------------------------------------------------------------------------------------------------
BALUSTER = ((.058, 0.0), (.077, .22), (.034, .48), (.05, .62), (.046, .72))
BALUSTER_LOW = ((.06, 0.0), (.065, .26), (.04, .72))


def baluster(g, T, at, prof=None, axis=(0, 1, 0), cap=False):
    sides = (5, 5, 4)[T.i]
    sk.lathe(g, list(prof or (BALUSTER if T.i < 2 else BALUSTER_LOW)), at, F_STONE, sides, axis=axis, phase=.3, cap1=cap)


def balustrade(g, T):
    cs = cols()
    ch = .02 if T.bevel else 0.0
    for k, c in enumerate(cs):
        # the pedestal: a square pier with a base and a cap moulding, turned to face the middle
        t = mk.Geo()
        sk.box(t, -PED, PED, FY, .92, -PED, PED, F_STONE_DK, T, bevel=.02, cell=0, skip=('-y',))
        if T.i < 2:
            sk.box(t, -PED - .05, PED + .05, FY, FY + .16, -PED - .05, PED + .05, F_STONE_DK, T, bevel=.025, cell=0, skip=('-y',))
            sk.box(t, -PED - .07, PED + .07, .92, 1.04, -PED - .07, PED + .07, F_STONE, T, bevel=.025, cell=0)
        else:
            sk.box(t, -PED - .06, PED + .06, .92, 1.04, -PED - .06, PED + .06, F_STONE, T, bevel=0, cell=0, skip=('-y',))
        if T.fine:
            # a raised panel on the pedestal's inner face
            sk.box(t, -PED - .012, -PED + .002, .12, .78, -.2, .2, F_STONE, T, bevel=.008, cell=0)
        put(g, t, yaw(c), P(BAL_R, c, 0.0))
        # the column: base, a shaft with a little entasis, an echinus, the abacus
        prof = [(.25, 1.04), (.265, 1.09), (.205, 1.15), (.19, 1.22), (.17, 3.05), (.19, 3.09), (.18, 3.13), (.255, 3.36)]
        if T.i == 2:
            prof = [(.24, 1.04), (.19, 1.18), (.17, 3.1), (.25, 3.36)]
        sk.lathe(g, prof, P(BAL_R, c, 0.0), F_STONE, (10, 8, 6)[T.i], phase=c)
        if T.i < 2:
            t = mk.Geo()
            sk.box(t, -.3, .3, 3.36, RB_Y0, -.3, .3, F_STONE, T, bevel=.02, cell=0)
            put(g, t, yaw(c), P(BAL_R, c, 0.0))
    # the ring beam on the abaci
    nb = (48, 32, 20)[T.i]
    y0 = RB_Y0 if T.i < 2 else 3.36
    hoop(g, BAL_R, sec_box(.36, RB_Y1 - y0, ch, F_STONE) if T.i < 2 else sec_soft(.36, RB_Y1 - y0, F_STONE), nb, y=(y0 + RB_Y1) / 2)
    # the bays
    for k in range(COLS):
        if k == GAP_BAY:
            continue
        a0 = cs[k] + (PED - .02) / BAL_R
        a1 = cs[k] + TAU / COLS - (PED - .02) / BAL_R
        bay(g, T, a0, a1, broken=k == BROKEN_BAY, seed=k)


def bay(g, T, a0, a1, broken=False, seed=0):
    ch = .02 if T.bevel else 0.0
    n = (8, 5, 3)[T.i]
    if T.i < 2:
        plinth = sec_box(.36, .23, ch, F_STONE_DK, y0=FY + .115, skip=('bottom',))
        rail = sec_box(.34, .15, ch * 1.4, F_STONE, y0=.875)
    else:
        plinth = sec_soft(.36, .23, F_STONE_DK, y0=FY + .115)
        rail = sec_soft(.34, .15, F_STONE, y0=.875)
    r = mk.rng(40 + seed)
    span = (a1 - a0) * BAL_R
    nb = int(span / (.4 if T.i < 2 else .55))
    step = (a1 - a0) / nb
    angs = [a0 + step * (i + .5) for i in range(nb)]
    if not broken:
        hoop(g, BAL_R, plinth, n, a0=a0, a1=a1)
        hoop(g, BAL_R, rail, n, a0=a0, a1=a1)
        gone = {2: 3, 7: 5}.get(seed, -1) if T.i < 2 else -1
        for i, a in enumerate(angs):
            # each a little off true: a lean and a hair short of the rail; one gone here and there
            lean = (float(r.uniform(-.012, .012)), 1.0, float(r.uniform(-.012, .012)))
            if i == gone:
                mk.slab(g, mk.circle(BAL_R * math.cos(a), BAL_R * math.sin(a), .06, 5, a), .07, .1, F_STONE, T, bevel=.008)
                continue
            if T.i < 2:
                k = float(r.uniform(.985, 1.0))
                baluster(g, T, P(BAL_R, a, .08), prof=[(x, y * k) for x, y in BALUSTER], axis=lean)
            else:
                baluster(g, T, P(BAL_R, a, .08))
        return
    # the fallen bay: the plinth cracked through, three balusters and a stub of the rail at one end, two snapped stumps, the
    # rest down on the floor with the rail's broken length and the rubble
    am = a0 + (a1 - a0) * .55
    hoop(g, BAL_R, plinth, max(1, n // 2), a0=a0, a1=am - .004)
    hoop(g, BAL_R, plinth, max(1, n // 2), a0=am + .004, a1=a1)
    if T.detail:
        hoop_caps(g, BAL_R, plinth, am - .004, am + .004)
    stub = a0 + step * 3.1
    hoop(g, BAL_R, rail, max(1, n // 3), a0=a0, a1=stub)
    hoop_caps(g, BAL_R, rail, a0, stub)
    stub2 = a1 - step * .9
    hoop(g, BAL_R, rail, 1, a0=stub2, a1=a1)
    hoop_caps(g, BAL_R, rail, stub2, a1)
    for i, a in enumerate(angs):
        if i < 3 or i == nb - 1:
            baluster(g, T, P(BAL_R, a, .08))
        elif i in (3, 5):
            baluster(g, T, P(BAL_R, a, .08), prof=BALUSTER[:2] + ((.064, .3 + .06 * (i % 2)),) if T.i < 2 else ((.06, 0.0), (.065, .3)), cap=True)
    # the fallen pieces, inside the floor's edge
    ac = a0 + (a1 - a0) * .5
    inward = rad(ac, -1)
    tang = (-math.sin(ac), 0.0, math.cos(ac))
    # the rail's length, one end on the plinth, the other on the floor
    base = P(BAL_R - .75, ac + .02, FY + .12)
    t = mk.Geo()
    sk.box(t, -.17, .17, -.075, .075, -.62, .62, F_STONE, T, bevel=.02, cell=0)
    q = sk.quat_axis(0, 1, 0, math.atan2(tang[0], tang[2]) + .5)
    put(g, t.transformed(sk.affine(sk.quat_axis(1, 0, 0, .14), (0, 0, 0))), q, base)
    # two balusters lying on the floor
    for j, (dr, da, turn) in enumerate(((1.4, -.07, .9), (1.05, .09, -.4))):
        p = P(BAL_R - dr, ac + da, FY + .06)
        ax = (math.cos(ac + turn), 0.0, math.sin(ac + turn))
        baluster(g, T, p, axis=ax)
    # rubble: broken masonry, Voronoi-cut pieces lying about
    for j in range((7, 4, 2)[T.i]):
        d = float(r.uniform(.25, 1.8))
        a = ac + float(r.uniform(-.09, .09))
        cx, cz = (BAL_R - d) * math.cos(a), (BAL_R - d) * math.sin(a)
        sz = float(r.uniform(.08, .2))
        poly = mk.circle(cx, cz, sz, 5, float(r.uniform(0, 6)))
        poly = [(x + float(r.uniform(-.3, .3)) * sz, z + float(r.uniform(-.3, .3)) * sz) for x, z in poly]
        poly = convex(poly)
        h = float(r.uniform(.07, .16))
        mk.slab(g, poly, FY - .02, FY + h, F_STONE if j % 2 else F_STONE_DK, T, bevel=.012,
                tilt=((cx, FY, cz), (float(r.uniform(-1, 1)), 0.0, float(r.uniform(-1, 1))), float(r.uniform(.1, .35))))


def convex(poly):
    """The convex hull of 2D points (monotone chain), counter-clockwise."""
    pts = sorted(set(poly))
    if len(pts) < 3:
        return pts

    def cr(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, hi = [], []
    for p in pts:
        while len(lo) >= 2 and cr(lo[-2], lo[-1], p) <= 0:
            lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(hi) >= 2 and cr(hi[-2], hi[-1], p) <= 0:
            hi.pop()
        hi.append(p)
    return lo[:-1] + hi[:-1]


# ---------------------------------------------------------------------------------------------------------------------------
# The dome: ten iron ribs from the ring beam to the oculus, purlins, a few panes of glass
# ---------------------------------------------------------------------------------------------------------------------------
def rib_point(t, a):
    return P(OCU_R + (BAL_R - OCU_R) * math.cos(t), a, RB_Y1 + (OCU_Y - RB_Y1) * math.sin(t))


def rib_t(y):
    return math.asin(max(0.0, min(1.0, (y - RB_Y1) / (OCU_Y - RB_Y1))))


def dome_normal(t, a):
    A, B = BAL_R - OCU_R, OCU_Y - RB_Y1
    nr, ny = B * math.cos(t), A * math.sin(t)
    ln = math.hypot(nr, ny)
    return (nr / ln * math.cos(a), ny / ln, nr / ln * math.sin(a))


def dome(g, T):
    cs = cols()
    ch = .014 if T.bevel else 0.0
    m = (15, 9, 6)[T.i]
    sec = sec_box(.2, .11, ch, F_IRON) if T.i < 2 else sec_round(.08, 4, F_IRON, phase=math.pi / 4)
    for k, c in enumerate(cs):
        ts = [(math.pi / 2) * (i / (m - 1)) ** .9 for i in range(m)]
        path = [rib_point(t, c) for t in ts]
        side = (-math.sin(c), 0.0, math.cos(c))
        sweep_plane(g, sec, path, side)
        # the rib's shoe on the ring beam, and a brass collar high up where the upper purlin crosses
        if T.i < 2:
            t = mk.Geo()
            sk.box(t, -.2, .2, RB_Y1, RB_Y1 + .2, -.11, .11, F_VERD, T, bevel=.015, cell=0, skip=('-y',))
            put(g, t, yaw(c), P(BAL_R - .02, c, 0.0))
        if T.detail:
            tt = rib_t(PURLIN_Y[1])
            p0, p1 = rib_point(tt - .03, c), rib_point(tt + .03, c)
            sweep_plane(g, sec_box(.25, .15, .01, F_BRASS_DK), [p0, p1], side, caps=True)
    # the oculus ring, a brass band round its inner face
    no = (40, 28, 16)[T.i]
    hoop(g, OCU_R, sec_box(.36, .42, ch * 1.5, F_IRON), no, y=OCU_Y)
    if T.detail:
        hoop(g, OCU_R, [((-.186, -.12, -1.0, 0.0), (-.186, .12, -1.0, 0.0), F_BRASS)], no, y=OCU_Y)
    # the purlins: iron rods round the dome
    npur = (44, 30, 20)[T.i]
    for y in PURLIN_Y[:(2, 2, 1)[T.i]]:
        t = rib_t(y)
        rp = OCU_R + (BAL_R - OCU_R) * math.cos(t)
        hoop(g, rp, sec_round(.04, (5, 5, 4)[T.i], F_IRON), npur, y=y)
    # the glass
    t1, t2 = rib_t(PURLIN_Y[0]), rib_t(PURLIN_Y[1])
    for bay_, upper in GLAZED:
        a0 = cs[bay_] + .06 / BAL_R
        a1 = cs[bay_] + TAU / COLS - .06 / BAL_R
        lo, hi = (t1 + .02, t2 - .02) if upper else (.04, t1 - .02)
        pane(g, T, a0, a1, lo, hi, broken=bay_ == BROKEN_PANE)


def pane(g, T, a0, a1, t0, t1, broken=False):
    nu, nv = (3, 2, 1)[T.i], (3, 2, 1)[T.i]

    def pt(u, v):
        a, t = a0 + (a1 - a0) * u, t0 + (t1 - t0) * v
        p = rib_point(t, a)
        nn = dome_normal(t, a)
        return sk._add(p, nn, -.02), nn
    if broken:
        # only jagged shards left in the frame's corners
        shards_ = [((0, 0), (.62, 0), (.08, .58)), ((1, 1), (.45, 1), (1, .4))]
        if T.i < 2:
            shards_ += [((.62, 0), (.86, 0), (.71, .24)), ((.45, 1), (.18, 1), (.37, .74)), ((1, 0), (1, .22), (.9, .05))]
        for tri in shards_:
            cs_ = [pt(u, v) for u, v in tri]
            ps = [c[0] for c in cs_]
            ns = [c[1] for c in cs_]
            g.face(ps, F_GLASS, normals=ns)
            g.face(ps[::-1], F_GLASS, normals=[(-n[0], -n[1], -n[2]) for n in ns[::-1]])
        return
    for i in range(nu):
        for j in range(nv):
            cs_ = [pt(i / nu, j / nv), pt((i + 1) / nu, j / nv), pt((i + 1) / nu, (j + 1) / nv), pt(i / nu, (j + 1) / nv)]
            ps = [c[0] for c in cs_]
            ns = [c[1] for c in cs_]
            g.face(ps, F_GLASS, normals=ns)
            g.face(ps[::-1], F_GLASS, normals=[(-n[0], -n[1], -n[2]) for n in ns[::-1]])


# ---------------------------------------------------------------------------------------------------------------------------
# The great refractor on its fork mount
# ---------------------------------------------------------------------------------------------------------------------------
def telescope(g, T):
    cx, cy, cz = TEL_C
    d = (math.cos(TEL_EL) * math.cos(TEL_AZ), math.sin(TEL_EL), math.cos(TEL_EL) * math.sin(TEL_AZ))
    dh = sk._norm((d[0], 0.0, d[2]))
    lat = sk._norm(sk._cross((0, 1, 0), dh))
    over = sk._norm(sk._cross(d, lat))
    if over[1] < 0:
        over = (-over[0], -over[1], -over[2])
    sides = (16, 10, 6)[T.i]
    L0 = 1.36
    E = sk._add(TEL_C, d, -L0)
    if T.i < 2:
        prof = [(.02, 0.0), (.026, .09), (.048, .11), (.048, .3), (.085, .33), (.12, .47), (.142, .55), (.15, 1.5), (.158, 2.78),
                (.183, 2.82), (.19, 2.97), (.205, 3.0), (.212, 3.42)]
    else:
        prof = [(.04, 0.0), (.048, .3), (.13, .5), (.158, 2.8), (.2, 2.9), (.21, 3.42)]
    sk.lathe(g, prof, E, F_BRASS, sides, axis=d)
    # the dew cap's inside and the objective, dark glass
    sk.lathe(g, [(.205, 3.42), (.198, 3.0)], E, F_BRASS_DK, sides, axis=d)
    oc = sk._add(E, d, 2.99)
    u, v, _ = sk.frame((0, 0, 0), d)
    g.face([sk._add(sk._add(oc, u, .19 * math.cos(TAU * k / sides)), v, .19 * math.sin(TAU * k / sides)) for k in range(sides)], F_LENS,
           normal=d)
    # brass bands and the cradle at the balance point
    if T.detail:
        for s_, rr in ((.95, .152), (2.2, .157)):
            sk.lathe(g, [(rr, s_ - .035), (rr + .014, s_ - .03), (rr + .014, s_ + .03), (rr, s_ + .035)], E, F_BRASS_DK, sides, axis=d)
    sk.lathe(g, [(.152, L0 - .1), (.178, L0 - .09), (.178, L0 + .09), (.152, L0 + .1)], E, F_IRON_CAST, sides, axis=d)
    # the trunnions through the fork's arms
    sk.cylinder(g, sk._add(TEL_C, lat, -.36), sk._add(TEL_C, lat, .36), .045, F_BRASS_DK, (10, 8, 5)[T.i])
    # the finder: a small scope riding on the tube, two brackets
    if T.detail:
        f0 = sk._add(sk._add(E, d, .35), over, .25)
        f1 = sk._add(sk._add(E, d, 1.05), over, .25)
        sk.lathe(g, [(.03, 0.0), (.036, .06), (.036, .62), (.045, .66), (.045, .72)], f0, F_BRASS, (8, 6, 4)[T.i], axis=d, cap0=True,
                 cap1=True)
        for s_ in (.5, .95):
            a = sk._add(sk._add(E, d, s_), over, .14)
            b_ = sk._add(sk._add(E, d, s_), over, .22)
            sk.cylinder(g, a, b_, .012, F_BRASS_DK, 5)
        # the focuser's knobs at the eyepiece
        kp = sk._add(E, d, .2)
        sk.cylinder(g, sk._add(kp, lat, -.1), sk._add(kp, lat, .1), .011, F_BRASS_DK, 6)
        for s in (-1, 1):
            sk.cylinder(g, sk._add(kp, lat, s * .1), sk._add(kp, lat, s * .125), .028, F_BRASS_DK, 8)
    # the fork: two arms up from a yoke on a turntable
    fq = face_to(dh[0], dh[2])
    t = mk.Geo()
    sk.box(t, -.4, .4, 1.38, 1.5, -.15, .15, F_IRON_CAST, T, bevel=.02, cell=0)
    for s in (-1, 1):
        sk.box(t, s * .29 - .045, s * .29 + .045, 1.45, cy + .1, -.12, .12, F_IRON_CAST, T, bevel=.015, cell=0)
        if T.detail:
            sk.box(t, s * .29 - .055, s * .29 + .055, cy - .07, cy + .13, -.13, .13, F_BRASS_DK, T, bevel=.012, cell=0)
    put(g, t, fq, (cx, 0.0, cz))
    sk.lathe(g, [(.36, 1.3), (.36, 1.38)], (cx, 0.0, cz), F_BRASS_DK, (16, 10, 6)[T.i], cap1=True)
    # the pier: a cast iron column on a stone footing
    if T.i < 2:
        prof = [(.5, FY + .08), (.48, FY + .14), (.35, FY + .23), (.26, .3), (.2, .48), (.17, 1.0), (.19, 1.16), (.25, 1.24), (.25, 1.3)]
    else:
        prof = [(.48, FY + .08), (.3, .2), (.17, 1.0), (.25, 1.3)]
    sk.lathe(g, prof, (cx, 0.0, cz), F_IRON_CAST, (12, 8, 6)[T.i], cap1=True)
    mk.slab(g, mk.circle(cx, cz, .78, 8, .2), FY - .05, FY + .08, F_STONE_DK, T, bevel=.03)
    # the observer's stool by the eyepiece
    if T.i == 2:
        return
    st = mk.Geo()
    sk.box(st, -.24, .24, .44, .5, -.17, .17, F_WOOD_H, T, bevel=.012, cell=0)
    for sx in (-1, 1):
        for sz in (-1, 1):
            sk.box(st, sx * .19 - .025, sx * .19 + .025, FY, .44, sz * .12 - .025, sz * .12 + .025, F_WOOD, T, bevel=.006, cell=0)
    if T.detail:
        sk.box(st, -.19, .19, .12, .16, -.02, .02, F_WOOD_H, T, bevel=0, cell=0)
    put(g, st, sk.quat_axis(0, 1, 0, .4), (4.15, 0.0, .45))


# ---------------------------------------------------------------------------------------------------------------------------
# Props: star-chart easels, the celestial globe, the hanging astrolabe, the instrument table, papers, glass on the floor
# ---------------------------------------------------------------------------------------------------------------------------
def props(g, T):
    chart(g, T, 3.5, -4.9, 1)
    chart(g, T, -5.2, 2.4, 2)
    chart(g, T, -5.7, -1.5, 3)
    globe(g, T, -4.6, -3.7)
    hanging_astrolabe(g, T, -2.3, -5.0)
    table(g, T, 4.5, -3.4)
    papers(g, T)
    shards(g, T)


def chart(g, T, x, z, seed):
    """A star chart on an easel, turned to the middle: a dark blue sky with lit stars and constellation lines, a brass ring."""
    r = mk.rng(300 + seed)
    t = mk.Geo()
    # the easel's legs and its ledge
    sides = 4
    sk.cylinder(t, (-.42, FY, 0.0), (-.36, 1.78, -.27), .022, F_WOOD, sides)
    sk.cylinder(t, (.42, FY, 0.0), (.36, 1.78, -.27), .022, F_WOOD, sides)
    sk.cylinder(t, (0.0, FY, -.85), (0.0, 1.74, -.29), .022, F_WOOD, sides)
    sk.box(t, -.5, .5, .5, .55, -.06, .1, F_WOOD_H, T, bevel=.008, cell=0)
    # the board, leaning back on the legs
    b = mk.Geo()
    sk.box(b, -.48, .48, 0.0, 1.22, -.035, 0.0, F_WOOD_H, T, bevel=.008, cell=0)
    b.face(((-.43, .05, .003), (.43, .05, .003), (.43, 1.17, .003), (-.43, 1.17, .003)), F_CHART, normal=(0, 0, 1))
    # the brass ring of the planisphere
    rc = (0.0, .62)
    nr = (24, 16, 0)[T.i]
    if nr:
        for k in range(nr):
            a0, a1 = TAU * k / nr, TAU * (k + 1) / nr
            p = [(rc[0] + rr * math.cos(a), rc[1] + rr * math.sin(a)) for rr, a in ((.36, a0), (.36, a1), (.38, a1), (.38, a0))]
            b.face([(px, py, .006) for px, py in p], F_BRASS, normal=(0, 0, 1))
    # stars and the lines between some of them
    if T.i < 2:
        stars = []
        for k in range((14, 9, 0)[T.i]):
            a = float(r.uniform(0, TAU))
            d = .33 * math.sqrt(float(r.uniform(0, 1)))
            sx, sy = rc[0] + d * math.cos(a), rc[1] + d * math.sin(a)
            s = float(r.uniform(.008, .02))
            stars.append((sx, sy))
            b.face(((sx, sy - s * 1.5, .008), (sx + s, sy, .008), (sx, sy + s * 1.5, .008), (sx - s, sy, .008)), F_STAR, normal=(0, 0, 1))
        for i0, i1 in ((0, 1), (1, 2), (2, 3), (4, 5), (5, 6), (7, 8))[:(6, 3, 0)[T.i]]:
            (ax, ay), (bx, by) = stars[i0], stars[i1]
            dx, dy = bx - ax, by - ay
            ln = math.hypot(dx, dy) or 1.0
            nx, ny = -dy / ln * .003, dx / ln * .003
            b.face(((ax - nx, ay - ny, .007), (bx - nx, by - ny, .007), (bx + nx, by + ny, .007), (ax + nx, ay + ny, .007)), F_CONST,
                   normal=(0, 0, 1))
    t.extend(b.transformed(sk.affine(sk.quat_axis(1, 0, 0, -.2), (0.0, .53, .04))))
    put(g, t, face_to(-x, -z), (x, 0.0, z))


def globe(g, T, x, z):
    """A celestial globe in a wooden stand: a horizon ring on four posts over a cross base, the globe in its brass meridian."""
    t = mk.Geo()
    yc = .98
    for k in range(2):
        a = k * math.pi / 2 + math.pi / 4
        bar = mk.Geo()
        sk.box(bar, -.56, .56, FY, FY + .08, -.04, .04, F_WOOD_H, T, bevel=.01, cell=0, skip=('-y',))
        t.extend(bar.transformed(sk.affine(sk.quat_axis(0, 1, 0, a), (0, 0, 0))))
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        p0 = (.5 * math.cos(a), FY + .08, .5 * math.sin(a))
        p1 = (.53 * math.cos(a), yc - .02, .53 * math.sin(a))
        sk.cylinder(t, p0, p1, .025, F_WOOD, (5, 4, 4)[T.i], caps=False)
    n = (24, 18, 12)[T.i]
    # the horizon ring: a wooden band, its top a paper calendar
    hoop(t, .56, [((-.06, -.015, -1.0, 0.0), (-.06, .015, -1.0, 0.0), F_WOOD_H), ((-.06, .015, 0.0, 1.0), (.06, .015, 0.0, 1.0), F_PAPER),
                  ((.06, .015, 1.0, 0.0), (.06, -.015, 1.0, 0.0), F_WOOD_H), ((.06, -.015, 0.0, -1.0), (-.06, -.015, 0.0, -1.0), F_WOOD_H)], n, y=yc)
    # the globe, tilted in its meridian
    tilt = sk.quat_axis(1, 0, 0, .62)
    gl = mk.Geo()
    sides, rings = (12, 9, 6)[T.i], (8, 6, 4)[T.i]
    prof = [(.45 * math.sin(math.pi * i / rings), -.45 * math.cos(math.pi * i / rings)) for i in range(rings + 1)]
    sk.lathe(gl, prof, (0, 0, 0), F_GLOBE, sides)
    sk.cylinder(gl, (0, -.53, 0), (0, .53, 0), .012, F_BRASS_DK, 4, caps=False)
    if T.detail:
        rr = mk.rng(77)
        for k in range((10, 6, 0)[T.i]):
            a, e = float(rr.uniform(0, TAU)), float(rr.uniform(-1.1, 1.1))
            c = (math.cos(e) * math.cos(a), math.sin(e), math.cos(e) * math.sin(a))
            u = sk._norm(sk._cross(c, (0, 1, 0)))
            v = sk._cross(u, c)
            s = .016
            base = sk._lerp((0, 0, 0), c, .453)
            gl.face((sk._add(base, v, -s * 1.4), sk._add(base, u, s), sk._add(base, v, s * 1.4), sk._add(base, u, -s)), F_STAR, normal=c)
    # the meridian: a brass ring standing in the horizon ring's notches
    mer = mk.Geo()
    hoop(mer, .5, sec_round(.02, 4, F_BRASS, phase=math.pi / 4), n)
    gl.extend(mer.transformed(sk.affine(sk.quat_axis(0, 0, 1, math.pi / 2), (0, 0, 0))))
    t.extend(gl.transformed(sk.affine(tilt, (0.0, yc, 0.0))))
    put(g, t, face_to(-x, -z), (x, 0.0, z))


def astrolabe(g, T, centre, normal, up, R=.42, rete=True):
    """An astrolabe: the mater (a disc with a raised limb), its dark plate, the openwork rete (an off-centre ring and star
    pointers), the rule across and the throne with its ring, in the plane through `centre` facing `normal` (its throne toward
    `up`). Built lying flat (its face up +y, its throne toward +z), then turned into place."""
    sides = ((24, 16, 10) if R > .3 else (16, 12, 8))[T.i]
    n = sk._norm(normal)
    u = sk._norm(sk._add(up, n, -sk._dot(up, n)))
    w = sk._norm(sk._cross(n, u))
    t = mk.Geo()
    up_ = (0.0, 1.0, 0.0)
    t.face([P(R - .04, -TAU * k / sides, .008) for k in range(sides)], F_BRASS_DK, normal=up_)
    t.face([P(R, TAU * k / sides, -.012) for k in range(sides)], F_BRASS, normal=(0, -1, 0))
    hoop(t, R, [((0.0, -.012, 1.0, 0.0), (0.0, .03, 1.0, 0.0), F_BRASS), ((0.0, .03, 0.0, 1.0), (-.04, .03, 0.0, 1.0), F_BRASS),
                ((-.04, .03, -1.0, 0.0), (-.04, .008, -1.0, 0.0), F_BRASS)], sides)
    if rete and T.detail:
        # the rete: the ecliptic's ring off the middle and star pointers
        hoop(t, R * .5, [((-.014, .018, 0.0, 1.0), (.014, .018, 0.0, 1.0), F_BRASS)], sides, cz=-R * .14)
        rr = mk.rng(int(R * 1000))
        for k in range((7, 4, 0)[T.i]):
            a = float(rr.uniform(0, TAU))
            d0, d1 = R * float(rr.uniform(.3, .5)), R * float(rr.uniform(.62, .86))
            c, s = math.cos(a), math.sin(a)
            t.face(((d0 * c - .022 * s, .019, d0 * s + .022 * c), (d1 * c, .019, d1 * s), (d0 * c + .022 * s, .019, d0 * s - .022 * c)),
                   F_BRASS, normal=up_)
    # the rule across, the pin, the throne and its ring
    rule = mk.Geo()
    sk.box(rule, -R * .95, R * .95, .02, .032, -.02, .02, F_BRASS_DK, T, bevel=0, cell=0, skip=('-y',))
    t.extend(rule.transformed(sk.affine(sk.quat_axis(0, 1, 0, .6), (0, 0, 0))))
    sk.cylinder(t, (0, -.012, 0), (0, .045, 0), .014, F_BRASS_DK, 6, caps=False)
    sk.box(t, -.07, .07, -.012, .028, R - .02, R + .09, F_BRASS, T, bevel=.006, cell=0)
    if T.detail:
        hoop(t, .045, sec_round(.008, 4, F_BRASS_DK), (10, 8, 6)[T.i], y=.008, cz=R + .13)
    m = [w[0], n[0], u[0], centre[0], w[1], n[1], u[1], centre[1], w[2], n[2], u[2], centre[2]]
    g.extend(t.transformed(m))


def hanging_astrolabe(g, T, x, z):
    """A great astrolabe hung from a tripod of poles, turned to the middle."""
    d = sk._norm((-x, 0.0, -z))
    apex = (x, 2.5, z)
    for k in range(3):
        a = math.atan2(d[2], d[0]) + math.pi + (k - 1) * 2.1
        foot = (x + 1.3 * math.cos(a), FY, z + 1.3 * math.sin(a))
        sk.cylinder(g, foot, (apex[0] + .05 * math.cos(a), apex[1] + .08, apex[2] + .05 * math.sin(a)), .028, F_WOOD, (5, 4, 4)[T.i])
    sk.cylinder(g, (apex[0], apex[1], apex[2]), (apex[0], 2.04, apex[2]), .008, F_IRON, 4, caps=False)
    astrolabe(g, T, (apex[0], 1.45, apex[2]), (d[0], 0.0, d[2]), (0, 1, 0), R=.42)


def table(g, T, x, z):
    """A round table with instruments: an astrolabe lying on it, an hourglass, books; a plate of an astrolabe leans on its foot."""
    sides = (12, 10, 6)[T.i]
    mk.slab(g, mk.circle(x, z, .52, sides), .72, .78, F_WOOD_H, T, bevel=.012)
    sk.lathe(g, [(.08, FY), (.06, .2), (.045, .5), (.07, .66), (.09, .72)] if T.i < 2 else [(.07, FY), (.05, .5), (.09, .72)], (x, 0.0, z),
             F_WOOD, (8, 6, 4)[T.i])
    for k in range(3):
        a = k * TAU / 3 + .3
        t = mk.Geo()
        sk.box(t, 0.0, .34, FY, FY + .07, -.03, .03, F_WOOD_H, T, bevel=.008, cell=0, skip=('-y',))
        put(g, t, yaw(a), (x, 0.0, z))
    astrolabe(g, T, (x - .1, .795, z + .05), (0, 1, 0), (.6, 0, .8), R=.2)
    if T.detail:
        # an hourglass
        hx, hz = x + .26, z - .14
        for y0 in (.78, 1.07):
            sk.box(g, hx - .07, hx + .07, y0, y0 + .02, hz - .07, hz + .07, F_WOOD, T, bevel=0, cell=0)
        sk.lathe(g, [(.0, .8), (.055, .83), (.008, .925), (.055, 1.02), (.0, 1.07)], (hx, 0.0, hz), F_SHARD, 6)
        for k in range(3):
            a = k * TAU / 3 + .5
            sk.cylinder(g, (hx + .06 * math.cos(a), .8, hz + .06 * math.sin(a)), (hx + .06 * math.cos(a), 1.07, hz + .06 * math.sin(a)),
                        .008, F_WOOD, 4, caps=False)
        # books
        bk = mk.Geo()
        sk.box(bk, -.13, .13, 0.0, .045, -.09, .09, F_BOOK, T, bevel=.004, cell=0, skip=('-y',))
        sk.box(bk, -.12, .12, .045, .08, -.085, .085, F_BRASS_DK, T, bevel=.004, cell=0, skip=('-y',))
        put(g, bk, sk.quat_axis(0, 1, 0, .4), (x + .05, .78, z - .3))
        # a loose plate on the floor against the pedestal
        astrolabe(g, T, (x + .2, FY + .2, z + .22), (.7, .35, .62), (0, 1, 0), R=.17, rete=False)


def papers(g, T):
    """Loose star charts on the floor, a little curled."""
    r = mk.rng(91)
    spots = ((-5.0, 1.9), (-4.6, 2.5), (3.0, -4.2), (4.0, -2.7), (-3.4, -4.6))
    for k, (x, z) in enumerate(spots[:(5, 3, 0)[T.i]]):
        a = float(r.uniform(0, TAU))
        c, s = math.cos(a), math.sin(a)
        w, h = .21, .3
        y = FY + .003 + .003 * k
        lift = float(r.uniform(.01, .03))

        def p(u, v, lift_=0.0):
            return (x + c * u - s * v, y + lift_, z + s * u + c * v)
        g.face((p(-w / 2, -h / 2), p(w / 2, -h / 2), p(w / 2, h / 4), p(-w / 2, h / 4)), F_PAPER, normal=(0, 1, 0))
        g.face((p(-w / 2, h / 4), p(w / 2, h / 4), p(w / 2, h / 2, lift), p(-w / 2, h / 2, lift)), F_PAPER, normal=(0, 1, 0))


def shards(g, T):
    """Glass from the broken pane, scattered on the floor below it."""
    if not T.detail:
        return
    r = mk.rng(23)
    a_mid = cols()[BROKEN_PANE] + TAU / COLS / 2
    for k in range((9, 5, 0)[T.i]):
        a = a_mid + float(r.uniform(-.1, .1))
        d = float(r.uniform(5.9, 6.7))
        x, z = d * math.cos(a), d * math.sin(a)
        s = float(r.uniform(.05, .16))
        b = float(r.uniform(0, TAU))
        y = FY + .004 + .002 * (k % 3)
        g.face(((x + s * math.cos(b), y, z + s * math.sin(b)), (x + s * .6 * math.cos(b + 2.2), y, z + s * .6 * math.sin(b + 2.2)),
                (x + s * .8 * math.cos(b + 4.0), y, z + s * .8 * math.sin(b + 4.0))), F_SHARD, normal=(0, 1, 0))


# ---------------------------------------------------------------------------------------------------------------------------
# The stair down from the open bay, the drop (the cornice, the tower's wall, the crags) and the far scenery
# ---------------------------------------------------------------------------------------------------------------------------
def stair(g, T):
    cs = cols()
    a0 = cs[GAP_BAY] + (PED - .02) / BAL_R
    a1 = cs[GAP_BAY] + TAU / COLS - (PED - .02) / BAL_R
    b = .012 if T.bevel else 0.0
    yb = FY - b
    # the landing: three stones out to the head of the stair
    r0, r1 = FLOOR_R + .02, 7.8
    n = (3, 2, 1)[T.i]
    for k in range(3):
        s0, s1 = a0 + (a1 - a0) * k / 3, a0 + (a1 - a0) * (k + 1) / 3
        jw = .008
        panel(g, r0, r1, (s0 + jw / r0, s1 - jw / r0), (s0 + jw / r1, s1 - jw / r1), FY, yb, F_STONE_DK, n, b)
    strip_ring(g, FLOOR_R - .006, r0 + .006, yb - .0005, (6, 4, 2)[T.i], F_BRASS, a0=a0, a1=a1)
    hoop(g, 0.0, [((r1, yb, 1.0, 0.0), (r1, FY - .32, 1.0, 0.0), F_STONE_DK)], (6, 4, 2)[T.i], a0=a0, a1=a1)
    hoop(g, 0.0, [((FLOOR_R - .1, FY - .045, 0.0, 1.0), (r1, FY - .045, 0.0, 1.0), F_JOINT)], 4, a0=a0, a1=a1)
    # the steps down toward +z
    steps = 10 if T.i < 2 else 5
    rise, run = (.17, .34) if T.i < 2 else (.34, .68)
    z0 = 7.78
    for i in range(steps):
        zt = z0 + run * i
        yt = FY - rise * (i + 1)
        sk.box(g, -STAIR_HALF, STAIR_HALF, yt - .22, yt, zt, zt + run + .02, F_STONE_DK if i % 3 else F_STONE, T, bevel=.015, cell=0)
    # the cheek walls, sloping with the stair
    slope = math.atan2(rise, run)
    L = run * steps
    zm = z0 + L / 2
    ym = FY - rise * steps / 2 + .32
    for s in (-1, 1):
        sk.obox(g, (s * (STAIR_HALF + .14), ym, zm - .1), (.14, .3, L / 2 / math.cos(slope) + .25), sk.quat_axis(1, 0, 0, slope), F_STONE, T,
                bevel=.02)
        # a newel post at the head of each wall
        x = s * (STAIR_HALF + .14)
        sk.box(g, x - .19, x + .19, FY, .82, 7.42, 7.8, F_STONE_DK, T, bevel=.02, cell=0, skip=('-y',))
        if T.i < 2:
            sk.box(g, x - .23, x + .23, .82, .94, 7.38, 7.84, F_STONE, T, bevel=.02, cell=0)
    # the mass under the stair, down into the crag
    zf = z0 + L
    yf = FY - rise * steps
    sec = [(7.45, FY - .3), (zf + .4, yf - .24), (zf + .4, -9.0), (7.45, -9.0)]
    for s in (-1, 1):
        x = s * (STAIR_HALF + .2)
        flat(g, [(x, y, zz) for zz, y in sec], F_TOWER, toward=(s, 0, 0))
    flat(g, [(-STAIR_HALF - .2, yf - .24, zf + .4), (STAIR_HALF + .2, yf - .24, zf + .4), (STAIR_HALF + .2, -9.0, zf + .4),
             (-STAIR_HALF - .2, -9.0, zf + .4)], F_TOWER, toward=(0, 0, 1))
    # the foot: a landing stone on a rock spur
    mk.slab(g, [(-1.5, zf - .05), (1.5, zf - .05), (1.6, zf + 1.1), (-1.4, zf + 1.2)], yf - .3, yf + .02, F_STONE_DK, T, bevel=.02)
    spire(g, (0.2, yf - .25, zf + .9), ((2.4, 0.0), (3.2, -3.5), (4.2, -8.0)), (8, 6, 5)[T.i], 7, F_CRAG, cap=True)


def spire(g, base, rings, sides, seed, f, cap=False, apex=None, jit0=None):
    """A rough rock mass: rings [(radius, height)] from `base` down or up, jittered, flat faced; an apex (height) closes its top,
    or a flat cap. jit0: the first ring's radial jitter range (so it can meet a wall)."""
    r = mk.rng(seed)
    bx, by, bz = base
    rows = []
    for j, (rr, h) in enumerate(rings):
        row = []
        for k in range(sides):
            a = TAU * k / sides + float(r.uniform(-.15, .15))
            q = rr * float(r.uniform(*(jit0 if (j == 0 and jit0) else (.82, 1.15))))
            row.append((bx + q * math.cos(a), by + h + float(r.uniform(-.25, .25)) * rr * .2, bz + q * math.sin(a)))
        rows.append(row)
    for j in range(len(rows) - 1):
        for k in range(sides):
            k1 = (k + 1) % sides
            pts = [rows[j][k], rows[j][k1], rows[j + 1][k1], rows[j + 1][k]]
            mid = ((pts[0][0] + pts[1][0]) / 2 - bx, 0.0, (pts[0][2] + pts[1][2]) / 2 - bz)
            flat(g, pts[:3], f, toward=mid)
            flat(g, [pts[0], pts[2], pts[3]], f, toward=mid)
    if apex is not None:
        top = (bx, by + apex, bz)
        for k in range(sides):
            k1 = (k + 1) % sides
            mid = ((rows[-1][k][0] + rows[-1][k1][0]) / 2 - bx, .3, (rows[-1][k][2] + rows[-1][k1][2]) / 2 - bz)
            flat(g, [rows[-1][k], rows[-1][k1], top], f, toward=mid)
    if cap:
        flat(g, rows[0], f, toward=(0, 1, 0))


def drop(g, T):
    """Beyond the balustrade: the platform's cornice, the tower's ashlar wall down to the summit's crags, rock spurs."""
    cs = cols()
    a0 = cs[GAP_BAY] + TAU / COLS - (PED - .02) / BAL_R
    a1 = cs[GAP_BAY] + TAU + (PED - .02) / BAL_R
    pts = [(7.33, FY, 0.0, 1.0), (7.52, FY, 0.0, 1.0), (7.62, FY - .07, 1.0, 0.0), (7.62, FY - .2, 1.0, 0.0), (7.5, FY - .32, .7, -.7),
           (7.45, FY - .42, 1.0, 0.0)]
    strips = [(pts[i], pts[i + 1], F_STONE) for i in range(len(pts) - 1)]
    nc = (56, 36, 20)[T.i]
    hoop(g, 0.0, strips, nc, a0=a0, a1=a1)
    hoop_caps(g, 0.0, strips + [(pts[-1], (7.33, FY - .42, 0, 0), F_STONE)], a0, a1)
    # the tower's wall
    hoop(g, 0.0, [((7.45, FY - .42, 1.0, 0.0), (7.45, -7.2, 1.0, 0.0), F_TOWER)], (28, 20, 12)[T.i])
    # the summit's crags round the tower's foot, falling away into the cloud
    sides = (16, 11, 8)[T.i]
    rings = ((7.25, 0.0), (10.5, -2.4), (15.0, -5.5), (23.0, -9.5), (34.0, -13.5)) if T.i < 2 else ((7.25, 0.0), (15.0, -5.5), (34.0, -13.5))
    spire(g, (0.0, -6.6, 0.0), rings, sides, 3, F_CRAG, jit0=(.96, 1.01))
    # spurs standing out of the slope
    for k, (a, d, h) in enumerate(((.4, 12.0, 3.8), (2.2, 13.5, 4.6), (3.6, 11.0, 3.2), (5.0, 14.0, 5.2))):
        if T.i == 2 and k > 1:
            break
        bx, bz = d * math.cos(a), d * math.sin(a)
        by = -6.6 - 2.4 * (d - 7.6) / 2.9 if d < 10.5 else -9.0 - 3.1 * (d - 10.5) / 4.5
        spire(g, (bx, by - 1.5, bz), ((2.2, 0.0), (1.3, h * .6)), (7, 6, 5)[T.i], 50 + k, F_CRAG, apex=h)


def far(g, T):
    """The sea of cloud below, cloud banks, two mountain ranges round the horizon (the far one snow capped)."""
    # the cloud sea: a rolling sheet below the summit
    rings = (9.0, 14.0, 21.0, 32.0, 48.0, 72.0, 110.0)
    ns = (30, 20, 12)[T.i]
    A = np.array([[rr * math.cos(TAU * k / ns), 0.0, rr * math.sin(TAU * k / ns)] for rr in rings for k in range(ns)])

    def height(X):
        return -13.0 + 3.2 * (sk.fbm(X * np.array([1.0, 0.0, 1.0]), .045, 3, 9) - .5)
    H = height(A)
    e = .5
    Hx = height(A + np.array([e, 0, 0]))
    Hz = height(A + np.array([0, 0, e]))
    for i in range(len(rings) - 1):
        for k in range(ns):
            ids = [i * ns + k, i * ns + (k + 1) % ns, (i + 1) * ns + (k + 1) % ns, (i + 1) * ns + k]
            ps = [(float(A[j, 0]), float(H[j]), float(A[j, 2])) for j in ids]
            nn = [sk._norm((-(float(Hx[j]) - float(H[j])) / e, 1.0, -(float(Hz[j]) - float(H[j])) / e)) for j in ids]
            g.face(ps, F_CLOUD, normals=nn)
    # banks of cloud round the summit
    r = mk.rng(13)
    for k in range((8, 6, 4)[T.i]):
        a = TAU * k / 8 + float(r.uniform(-.25, .25))
        d = float(r.uniform(42, 78))
        sx = float(r.uniform(9, 17))
        mk.rock(g, (d * math.cos(a), float(r.uniform(-10.5, -6.5)), d * math.sin(a)), (sx, float(r.uniform(2.2, 3.8)), sx * .4), F_CLOUD_LT,
                T, seed=600 + k, rough=.3, flat_bottom=False, level=(1, 1, 0)[T.i], facets=False, turn=-a + math.pi / 2)
    # the ranges: a dark near one, a paler far one with snow on its peaks
    rad_ = math.radians
    ridge(g, T, 62.0, (34, 26, 13)[T.i], -16.0, -4.0, 6.0, 21, F_FAR_NEAR, hero=((rad_(255), 10.5), (rad_(20), 9.0), (rad_(140), 8.0)))
    ridge(g, T, 105.0, (26, 20, 10)[T.i], -14.0, 5.0, 23.0, 37, F_FAR, snow=17.0,
          hero=((rad_(84), 38.0), (rad_(300), 31.0), (rad_(172), 28.0), (rad_(222), 24.0)))


def ridge(g, T, D, n, base_y, hlo, hhi, seed, f, snow=None, hero=(), slope=(.5, .95)):
    """A mountain range all round at about distance D: n peaks and the hero peaks [(angle, height)], each a tent whose flanks
    fall at a random slope; the ridge is their upper envelope through its peaks and saddles, the peaks a little nearer than the
    saddles (so the faces fold), two rows of flat faces down to base_y; above `snow` the faces are white."""
    r = mk.rng(seed)
    tents = []
    for i in range(n):
        a = TAU * i / n + float(r.uniform(-.4, .4)) * TAU / n
        tents.append((a, hlo + (hhi - hlo) * float(r.uniform(0, 1)) ** 1.4, float(r.uniform(*slope))))
    for a, h in hero:
        tents.append((a, h, float(r.uniform(*slope))))

    def H(a):
        best = base_y
        for ta, th, tk in tents:
            best = max(best, th - tk * D * abs(math.atan2(math.sin(a - ta), math.cos(a - ta))))
        return best
    M = len(tents) * 10
    hs = [H(TAU * i / M) for i in range(M)]
    pts = []
    for i in range(M):
        h0, h1, h2 = hs[i - 1], hs[i], hs[(i + 1) % M]
        if h1 >= h0 and h1 > h2:
            pts.append((TAU * i / M, h1, D * float(r.uniform(.975, .99))))
        elif h1 <= h0 and h1 < h2:
            pts.append((TAU * i / M, h1, D * float(r.uniform(1.01, 1.025))))
    m = len(pts)
    # a shoulder under the middle of each ridge segment, a little forward: the faces fold diagonally (a facet down from each
    # segment of the ridge to its shoulder, a spur down from each peak or saddle between two shoulders), then a skirt to the base
    sh = []
    for i in range(m):
        (a0, h0, d0), (a1, h1, d1) = pts[i], pts[(i + 1) % m]
        if a1 < a0:
            a1 += TAU
        am_ = (a0 + a1) / 2 + float(r.uniform(-.15, .15)) * (a1 - a0)
        sh.append((am_, base_y + (min(h0, h1) - base_y) * float(r.uniform(.4, .6)), (d0 + d1) / 2 * float(r.uniform(.955, .975))))

    def face(poly, toward):
        if snow is None or max(p[1] for p in poly) <= snow:
            flat(g, poly, f, toward=toward)
            return
        lo, hi = [], []
        for j in range(len(poly)):
            p, q = poly[j], poly[(j + 1) % len(poly)]
            (lo if p[1] <= snow else hi).append(p)
            if (p[1] - snow) * (q[1] - snow) < 0:
                c = sk._lerp(p, q, (snow - p[1]) / (q[1] - p[1]))
                lo.append(c)
                hi.append(c)
        if len(lo) >= 3:
            flat(g, lo, f, toward=toward)
        if len(hi) >= 3:
            flat(g, hi, F_SNOW, toward=toward)
    for i in range(m):
        (a0, h0, d0), (a1, h1, d1) = pts[i], pts[(i + 1) % m]
        (sa, sy, sd), (pa, py, pd) = sh[i], sh[i - 1]
        if a1 < a0:
            a1 += TAU
        if sa < a0:
            sa += TAU
        if pa > a0:
            pa -= TAU
        t0, t1 = P(d0, a0, h0), P(d1, a1, h1)
        s0, s1 = P(pd, pa, py), P(sd, sa, sy)
        toward = (-math.cos(a0), 0.0, -math.sin(a0))
        face([t0, t1, s1], (-math.cos(sa), 0.0, -math.sin(sa)))
        face([s0, t0, s1], toward)
        flat(g, [s0, s1, P(D * 1.08, sa, base_y), P(D * 1.08, pa, base_y)], f, toward=toward)


# ---------------------------------------------------------------------------------------------------------------------------
# The orrery's rigs
# ---------------------------------------------------------------------------------------------------------------------------
def arm_ring(T, k):
    """An armillary ring in its own frame (its centre at the origin, the ring in the x-z plane): a brass hoop (0.06 m out by
    0.12 m up, chamfered), a lit inlay along the middle of its outer and inner faces, two pivot bosses and two ferrules."""
    g = mk.Geo()
    R = RING_RADII[k]
    n = ((32, 36, 40, 44), (24, 28, 30, 34), (16, 18, 20, 22))[T.i][k]
    w, h = .06, .12
    a, b = w / 2, h / 2
    c = .008
    il = .011
    if T.i < 2:
        S = [((-a + c, -b, 0, -1), (a - c, -b, 0, -1), F_RING), ((a - c, -b, 0, -1), (a, -b + c, 1, 0), F_RING),
             ((a, -b + c, 1, 0), (a, -il, 1, 0), F_RING), ((a, -il, 1, 0), (a, il, 1, 0), F_INLAY), ((a, il, 1, 0), (a, b - c, 1, 0), F_RING),
             ((a, b - c, 1, 0), (a - c, b, 0, 1), F_RING), ((a - c, b, 0, 1), (-a + c, b, 0, 1), F_RING),
             ((-a + c, b, 0, 1), (-a, b - c, -1, 0), F_RING), ((-a, b - c, -1, 0), (-a, il, -1, 0), F_RING),
             ((-a, il, -1, 0), (-a, -il, -1, 0), F_INLAY), ((-a, -il, -1, 0), (-a, -b + c, -1, 0), F_RING),
             ((-a, -b + c, -1, 0), (-a + c, -b, 0, -1), F_RING)]
    else:
        # Low: the band shaded round from its four corners, the inlay laid on its outer face
        k2 = .7071
        q = [(a, -b, k2, -k2), (a, b, k2, k2), (-a, b, -k2, k2), (-a, -b, -k2, -k2)]
        S = [(q[i], q[(i + 1) % 4], F_RING) for i in range(4)] + [((a + .002, -il, 1, 0), (a + .002, il, 1, 0), F_INLAY)]
    hoop(g, R, S, n, a0=.17 * k, a1=.17 * k + TAU)
    if T.detail:
        # the pivot bosses, a quarter turn apart from ring to ring, and two ferrules across the hoop between them
        for j in range(2):
            ang = k * math.pi / 4 + j * math.pi
            base = P(R + a - .002, ang, 0.0)
            sk.lathe(g, [(.042, 0.0), (.042, .022), (.032, .036), (.0, .042)], base, F_RING, (8, 6, 4)[T.i], axis=rad(ang))
            sk.lathe(g, [(.03, 0.0), (.03, .012), (.0, .016)], P(R - a + .002, ang, 0.0), F_RING, (8, 6, 4)[T.i], axis=rad(ang, -1))
            fa = ang + math.pi / 2
            fs = sec_box(w + .014, h + .014, .004, F_RING)
            d = .035 / R
            hoop(g, R, fs, 1, a0=fa - d, a1=fa + d)
            hoop_caps(g, R, fs, fa - d, fa + d)
    return g


def ringblade(T):
    """A free ring-blade (an original look-alike of the Glaive's) in its own frame: the ring in the x-y plane round the origin.
    A stepped ring (a dark hub with a lit inner rim, steel faces thinning to a lit cutting edge), three hooked fangs sweeping off
    its rim with lit outer edges, lit inlaid arcs on both faces, a wrapped grip bar across the opening with brass collars."""
    g = mk.Geo()
    n = (28, 22, 14)[T.i]
    if T.i < 2:
        pts = [(.2, -.02), (.236, -.02), (.25, -.011), (.305, -.006), (.33, 0.0), (.305, .006), (.25, .011), (.236, .02), (.2, .02)]
        fins = [F_BL_DARK, F_BL_PLATE, F_BL_STEEL, F_BL_EDGE, F_BL_EDGE, F_BL_STEEL, F_BL_PLATE, F_BL_DARK, F_BL_CORE]
    else:
        pts = [(.2, -.02), (.24, -.02), (.305, -.006), (.33, 0.0), (.305, .006), (.24, .02), (.2, .02)]
        fins = [F_BL_DARK, F_BL_STEEL, F_BL_EDGE, F_BL_EDGE, F_BL_STEEL, F_BL_DARK, F_BL_CORE]
    hoop(g, 0.0, sec_poly(pts, fins), n)
    # the fangs: crescents off the rim, sweeping the way the ring spins
    m = (7, 5, 3)[T.i]
    for hh in range(3):
        a0 = hh * TAU / 3 + .35
        sw = 1.25
        outer, inner = [], []
        for i in range(m + 1):
            t = i / m
            th = a0 + sw * t
            ro = .315 + (.455 - .315) * t ** 1.25
            ri = .315 + (.455 - .315) * max(0.0, (t - .5) / .5) ** 1.6
            outer.append((th, ro))
            inner.append((th, ri))
        for i in range(m):
            (ta, oa), (tb, ob) = outer[i], outer[i + 1]
            (_, ia), (_, ib) = inner[i], inner[i + 1]
            for s in (-1, 1):
                y = s * .005
                g.face((P(ia, ta, y), P(ib, tb, y), P(ob, tb, y), P(oa, ta, y)), F_BL_STEEL, normal=(0, s, 0))
                # the lit edge: a bevel from each face out to the sharp rim
                ea, eb = oa + .011, ob + .011
                na = sk._norm((math.cos(ta) * .45, s * 1.0, math.sin(ta) * .45))
                nb = sk._norm((math.cos(tb) * .45, s * 1.0, math.sin(tb) * .45))
                g.face((P(oa, ta, y), P(ob, tb, y), P(eb, tb, 0.0), P(ea, ta, 0.0)), F_BL_EDGE, normals=(na, nb, nb, na))
            # the back edge (the inner curve), plain
            if ib > .33 or ia > .33:
                g.face((P(ia, ta, -.005), P(ib, tb, -.005), P(ib, tb, .005), P(ia, ta, .005)), F_BL_PLATE,
                       normals=(rad(ta, -1), rad(tb, -1), rad(tb, -1), rad(ta, -1)))
    # the inlaid arcs on both faces
    if T.detail:
        mi = (6, 4, 0)[T.i]
        for s in (-1, 1):
            for hh in range(3):
                b0 = hh * TAU / 3 + 1.75
                for i in range(mi):
                    ta, tb = b0 + 1.3 * i / mi, b0 + 1.3 * (i + 1) / mi
                    r0_, r1_ = .266, .278

                    def hgt(rr):
                        return s * (.011 - (rr - .25) / .055 * .005 + .0012)
                    nn = sk._norm((0.0, s * 1.0, 0.0))
                    g.face((P(r0_, ta, hgt(r0_)), P(r0_, tb, hgt(r0_)), P(r1_, tb, hgt(r1_)), P(r1_, ta, hgt(r1_))), F_BL_CORE, normal=(0, s, 0))
    # the grip: a wrapped bar across the opening near its rim, brass collars, lit studs where it meets the hub
    zc = -.13
    bs = (10, 8, 5)[T.i]
    sk.cylinder(g, (-.165, 0.0, zc), (.165, 0.0, zc), .017, F_BL_WRAP, bs, caps=False)
    if T.detail:
        for xw in (-.05, 0.0, .05):
            sk.lathe(g, [(.0185, -.005), (.0192, 0.0), (.0185, .005)], (xw, 0.0, zc), F_BL_DARK, bs, axis=(1, 0, 0))
        for sx in (-1, 1):
            sk.lathe(g, [(.017, 0.0), (.023, .005), (.023, .022), (.017, .027)], (sx * .095, 0.0, zc), F_BL_PLATE, bs, axis=(sx, 0, 0))
            for s in (-1, 1):
                sk.lathe(g, [(.012, 0.0), (.009, .005), (.0, .008)], (sx * .15, s * .0195, zc - .03), F_BL_CORE, 6, axis=(0, s, 0))
    return g.transformed(sk.affine(sk.quat_axis(1, 0, 0, math.pi / 2), (0, 0, 0)))
