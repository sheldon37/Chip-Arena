"""Afterimage's set (the daggers; docs/INTROS-MELEE.md): Lantern Alley, a narrow night market lane, wet after rain. The lane runs
along z (4.5 m between the shopfronts at x = +-2.25) from z -19 back past the camera to z +16.5: wet setts in rows (modelled near
the pilot, z -3.5..2.3; the shader's cobble courses further off), a central drain of long kerb stones with iron grates (steam
rises from them), gutters along both fronts; two-storey timber shopfronts on both sides, each bay its own: shutters (some painted,
some open on dim lit interiors with counters and shelves, noren curtains), plaster upper floors with paper windows (warm or teal
lit, some dark), balconies or tiled pent roofs, deep tiled eaves, blade signboards with blank lit glyph panels (teal, magenta),
cloth awnings, bunting, crates, baskets and barrels; a noodle cart with its canopy, pot and stools on the pilot's left; a red
wooden gate across the lane at z -16, past it a cross street and a low lit house (z -23.5) under the rooftops beyond; at +z the
lane turns (the +x fronts end at z 14, a lit house closes it at z 16.5); rooftops all round.

The moving parts: eight paper lanterns strung across the lane on static cords (rigs `lantern-0..7`, each in its own frame with its
origin at the cord's attachment point at its top, hanging down -y; at rest from z +6 to -12, 3.0 to 3.6 m up).
Set frame: melee_kit's (the pilot at the origin facing +z; y 0 the drain stone's top)."""
import math

import melee_kit as mk
from melee_kit import sk

TAU = 2 * math.pi

# ---- finishes ----
F_SETT = mk.fin('alley.sett', 0x4e5157, sk.DETAIL_CARVED, smooth=.82, dirt=.7, moss=.15, wear=.25)
F_SETT_DK = mk.fin('alley.sett.dark', 0x44474d, sk.DETAIL_CARVED, smooth=.86, dirt=.7, moss=.2, wear=.2)
F_COBBLE = mk.fin('alley.cobble', 0x4b4e54, sk.DETAIL_ASHLAR, smooth=.8, dirt=.7, moss=.15, param=.22)
F_BED = mk.fin('alley.bed', 0x302e2c, smooth=.7, dirt=.5)
F_WATER = mk.fin('alley.water', 0x1c232b, smooth=.97, dirt=0)
F_KERB = mk.fin('alley.kerb', 0x67686b, sk.DETAIL_CARVED, smooth=.65, dirt=.9, moss=.25, wear=.3)
F_IRON = mk.fin('alley.iron', 0x35363a, sk.DETAIL_IRON, smooth=.5, metal=.6, dirt=.6)
F_TIMBER = mk.fin('alley.timber', 0x4a3426, sk.DETAIL_WOOD, smooth=.35, dirt=.8, param=1, wear=.25)
F_TIMBER_Z = mk.fin('alley.timber.z', 0x4a3426, sk.DETAIL_WOOD, smooth=.35, dirt=.8, param=2, wear=.25)
F_TIMBER_X = mk.fin('alley.timber.x', 0x4a3426, sk.DETAIL_WOOD, smooth=.35, dirt=.8, param=0, wear=.25)
F_LACQUER = mk.fin('alley.lacquer', 0x7a2a24, sk.DETAIL_WOOD, smooth=.55, dirt=.7, param=1, wear=.35)
F_LACQUER_X = mk.fin('alley.lacquer.x', 0x7a2a24, sk.DETAIL_WOOD, smooth=.55, dirt=.7, param=0, wear=.35)
F_PLASTER = mk.fin('alley.plaster', 0xa39b8b, sk.DETAIL_PLASTER, smooth=.15, dirt=1.3, moss=.1)
F_PLASTER_DK = mk.fin('alley.plaster.dark', 0x7e776b, sk.DETAIL_PLASTER, smooth=.15, dirt=1.3, moss=.15)
F_TILE = mk.fin('alley.tile', 0x363b42, smooth=.6, metal=.1, dirt=.6, moss=.1)
F_SHUTTER = mk.fin('alley.shutter', 0x5d4a39, sk.DETAIL_WOOD, smooth=.3, dirt=1.0, param=1, wear=.3)
F_SHUTTER_TEAL = mk.fin('alley.shutter.teal', 0x2f5753, sk.DETAIL_WOOD, smooth=.35, dirt=1.0, param=1, wear=.5)
F_INTERIOR = mk.fin('alley.interior.glow', 0xffad6a, glow=.55)
F_INSIDE = mk.fin('alley.inside', 0x3a302a, smooth=.3, dirt=.6)
F_PAPER_LIT = mk.fin('alley.paper.glow', 0xffcf96, glow=.8)
F_PAPER_TEAL = mk.fin('alley.paper.teal.glow', 0x7ff0e6, glow=.6)
F_PAPER = mk.fin('alley.paper', 0x8a8170, smooth=.2, dirt=.6)
F_SIGN = mk.fin('alley.sign', 0x3a2a20, sk.DETAIL_WOOD, smooth=.4, dirt=.7, param=1)
F_GLYPH_TEAL = mk.fin('alley.glyph.teal.glow', 0x4ff0dc, glow=.9)
F_GLYPH_MAG = mk.fin('alley.glyph.magenta.glow', 0xff4fc4, glow=.9)
F_LANTERNS = (mk.fin('alley.lantern.red.glow', 0xff5a3a, glow=1.8), mk.fin('alley.lantern.amber.glow', 0xffa24a, glow=1.7),
              mk.fin('alley.lantern.magenta.glow', 0xff4fa8, glow=1.7))
F_CAP = mk.fin('alley.lantern.cap', 0x34291f, sk.DETAIL_WOOD, smooth=.6, dirt=.3, param=1)
F_TASSEL = mk.fin('alley.tassel', 0x8a2626, sk.DETAIL_CLOTH, smooth=.2, dirt=.2)
F_CORD = mk.fin('alley.cord', 0x3c3430, smooth=.2, dirt=.3)
F_NOREN = mk.fin('alley.noren', 0x2f3b5e, sk.DETAIL_CLOTH, smooth=.15, dirt=.6)
F_NOREN_RED = mk.fin('alley.noren.red', 0x7a2c2c, sk.DETAIL_CLOTH, smooth=.15, dirt=.6)
F_CANVAS = mk.fin('alley.canvas', 0x9c907a, sk.DETAIL_CLOTH, smooth=.15, dirt=1.0)
F_CRATE = mk.fin('alley.crate', 0x7a6248, sk.DETAIL_WOOD, smooth=.3, dirt=1.0, param=0, wear=.3)
F_BASKET = mk.fin('alley.basket', 0x8a7346, sk.DETAIL_CLOTH, smooth=.25, dirt=.8)
F_BARREL = mk.fin('alley.barrel', 0x5e4630, sk.DETAIL_WOOD, smooth=.35, dirt=.9, param=1, wear=.3)
F_CERAMIC = mk.fin('alley.ceramic', 0x9a8e78, smooth=.7, dirt=.5)
F_CERAMIC_BLUE = mk.fin('alley.ceramic.blue', 0x3e5a7a, smooth=.75, dirt=.4)
F_STONE = mk.fin('alley.stone', 0x6e6c68, sk.DETAIL_CARVED, smooth=.4, dirt=1.0, moss=.3, wear=.3)
F_FAR = mk.fin('alley.far', 0x2c313b, smooth=.2, dirt=0)
F_FAR_LIT = mk.fin('alley.far.glow', 0xffb070, glow=.5)

# ---- the plan ----
LANE = 2.25            # the shopfronts' line (|x|)
GUTTER = 1.97          # the gutters run between it and the shopfronts
DRAIN = .2             # the central drain's half width
Z0, Z1 = -19.0, 16.5   # the lane's modelled length (at +z it turns: the +x fronts end at CORNER_Z, a house closes it)
CORNER_Z = 14.0
SETTS_Z = (-3.5, 2.3)  # the stretch of modelled setts (the rest: the shader's cobbles)
GATE_Z = -16.0
END_Z = -23.5          # past the fronts' end a cross street; a low house closes the view there
EAVE_Y = 5.62
H = 'hero'

# the bays: (from z, to z, ground, upper, window, sign, extra). ground: shut / tshut (teal) / open / half / stall; upper: pent /
# balcony; window: lit / teal / dark; sign: None / teal / mag; extra: awning / noren / goods / None
BAYS = {
    1: [(14.0, 10.8, 'half', 'pent', 'lit', 'mag', None), (10.8, 7.4, 'shut', 'pent', 'dark', None, 'awning'),
        (7.4, 4.2, 'open', 'balcony', 'lit', None, 'noren'), (4.2, 1.2, 'tshut', 'pent', 'lit', 'teal', None),
        (1.2, -1.9, 'half', 'balcony', 'teal', None, None), (-1.9, -5.0, 'open', 'pent', 'lit', 'mag', 'noren'),
        (-5.0, -8.3, 'half', 'balcony', 'dark', None, None), (-8.3, -11.6, 'stall', 'pent', 'lit', 'teal', 'awning'),
        (-11.6, -14.4, 'shut', 'pent', 'teal', None, None), (-14.4, -17.0, 'open', 'balcony', 'lit', None, None),
        (-17.0, -19.0, 'shut', 'pent', 'dark', None, None)],
    -1: [(16.5, 13.3, 'shut', 'balcony', 'teal', None, None), (13.3, 10.0, 'open', 'pent', 'lit', None, None),
         (10.0, 6.8, 'stall', 'pent', 'lit', None, 'awning'), (6.8, 3.6, 'shut', 'pent', 'teal', 'mag', None),
         (3.6, .5, 'open', 'pent', 'lit', None, 'noren'), (.5, -2.6, 'tshut', 'balcony', 'dark', None, None),
         (-2.6, -5.9, 'half', 'balcony', 'lit', None, None), (-5.9, -9.1, 'stall', 'pent', 'teal', 'teal', 'goods'),
         (-9.1, -12.4, 'shut', 'balcony', 'lit', None, None), (-12.4, -15.6, 'open', 'pent', 'dark', 'mag', 'noren'),
         (-15.6, -19.0, 'shut', 'pent', 'lit', None, None)],
}
# the lanterns: (z, x on the cord, attachment height, cord height at the fronts left, right, shape, colour)
LANTERNS = ((6.0, .1, 3.35, 4.55, 4.72, 0, 0), (3.4, -.35, 3.55, 4.6, 4.5, 1, 1), (.8, .25, 3.2, 4.5, 4.78, 2, 0),
            (-1.8, -.15, 3.45, 4.5, 4.55, 0, 2), (-4.4, .4, 3.6, 4.65, 4.5, 1, 0), (-7.0, -.3, 3.3, 4.5, 4.6, 2, 1),
            (-9.5, .15, 3.5, 4.55, 4.5, 0, 2), (-12.0, -.4, 3.25, 4.78, 4.5, 1, 0))
# bunting strung higher between some of the lanterns: (z, height left, right, sag)
BUNTING = ((4.75, 4.95, 5.05, .3), (-3.05, 5.0, 4.9, .28), (-10.75, 4.95, 5.0, .32))


def hero(z0, z1):
    """Whether a stretch lies where the cameras look closely."""
    return max(z0, z1) > -11.5 and min(z0, z1) < 8.5


def build(T):
    s = mk.SetPiece('alley')
    g = mk.Geo()
    tl = mk.Tally(g)
    lane(g, T)
    tl('lane')
    for side in (1, -1):
        for b in BAYS[side]:
            bay(g, T, side, *b)
    tl('fronts')
    skyline(g, T)
    corner(g, T)
    tl('skyline')
    gate(g, T)
    tl('gate')
    cart(g, T, (1.42, -3.55))
    props(g, T)
    tl('props')
    if T.detail:
        for b in BUNTING:
            bunting(g, T, *b)
        drape(g, T, -1, -10.75, F_NOREN)
        drape(g, T, 1, -6.4, F_CANVAS)
    for k, L in enumerate(LANTERNS):
        cord(g, T, L)
        s.rig('lantern-%d' % k, T.i, lantern(T, L[5], F_LANTERNS[L[6]], k), mk.translate(L[1], L[2], L[0]))
    tl('cords')
    s.tally = tl
    s.static[T.i] = g
    s.views = [
        ('front', (0.0, 1.6, 6.0), (0.0, 1.6, -6.0), 55),
        ('low', (.5, .18, 2.6), (0.0, 1.4, -6.0), 72),
        ('balcony', (-1.95, 4.35, -2.6), (.6, 0.0, 1.2), 72),
        ('reverse', (0.0, 1.6, -6.0), (0.0, 1.8, 6.0), 55),
        ('gate', (.4, 1.7, -8.5), (0.0, 2.8, -17.0), 58),
        ('stall', (-.9, 1.6, -.4), (1.5, 1.0, -3.7), 62),
        ('high', (0.0, 10.0, 7.5), (0.0, 0.0, -5.0), 60),
    ]
    return s


# ---------------------------------------------------------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------------------------------------------------------
def sbox(g, T, s, u0, u1, y0, y1, z0, z1, f, bevel=.015, skip=(), cell=0):
    """A box on side s of the lane, u (|x|) from u0 to u1."""
    sk.box(g, s * u0, s * u1, y0, y1, z0, z1, f, T, bevel=bevel, cell=cell, skip=skip)


def out_face(s):
    return '+x' if s > 0 else '-x'


def in_face(s):
    return '-x' if s > 0 else '+x'


def flat(g, pts, f, toward=None):
    n = (0.0, 0.0, 0.0)
    for i in range(1, len(pts) - 1):
        n = sk._add(n, sk._cross(sk._sub(pts[i], pts[0]), sk._sub(pts[i + 1], pts[0])))
    if toward is not None and sk._dot(n, toward) < 0:
        n = (-n[0], -n[1], -n[2])
    g.face(pts, f, normal=n)


def two_sided(g, pts, f, normal):
    g.face(pts, f, normal=normal)
    g.face(pts[::-1], f, normal=(-normal[0], -normal[1], -normal[2]))


def put(g, t, q, at):
    g.extend(t.transformed(sk.affine(q, at)))


def tiles(g, T, s, u_edge, y_edge, u_top, y_top, z0, z1, step=.27, lift=.03):
    """Rolled tiles down a slope on side s (from the eave's edge (u_edge, y_edge) up toward (u_top, y_top)) every `step` along z."""
    n = max(1, int((z0 - z1) / step))
    sec = [(-.05, 0), (-.035, .03), (0, .045), (.035, .03), (.05, 0)]
    for i in range(n + 1):
        z = z1 + (z0 - z1) * (i + .5) / (n + 1)
        p0 = (s * u_edge, y_edge + lift, z)
        p1 = (s * u_top, y_top + lift, z)
        sk.sweep(g, sec, [p0, p1], F_TILE, caps=False, up=(0, 1, 0))


# ---------------------------------------------------------------------------------------------------------------------------
# The lane: the drain, the setts, the gutters
# ---------------------------------------------------------------------------------------------------------------------------
def lane(g, T):
    r = mk.rng(3)
    # the central drain: long kerb stones, iron grates over the drain here and there (the steam's vents)
    grates = (1.2, -4.6, -9.4, 6.4) if T.i < 2 else ()
    z = Z1
    while z > Z0:
        L = float(r.uniform(.9, 1.5)) if hero(z, z - 1.5) else 4.0
        z1 = max(Z0, z - L)
        hit = [gz for gz in grates if z1 - .3 < gz < z + .3]
        if hit:
            gz = hit[0]
            if z > gz + .3:
                mk.slab(g, [(-DRAIN, gz + .3), (DRAIN, gz + .3), (DRAIN, z - .006), (-DRAIN, z - .006)], -.2, 0.0, F_KERB, T, bevel=.012)
            grate(g, T, gz)
            z = gz - .3
            continue
        y = float(r.uniform(-.004, .002)) if T.fine else 0.0
        mk.slab(g, [(-DRAIN, z1 + .006), (DRAIN, z1 + .006), (DRAIN, z - .006), (-DRAIN, z - .006)], -.2, y, F_KERB, T,
                bevel=.012 if hero(z, z1) else 0.0)
        z = z1
    sk.box(g, -DRAIN - .01, DRAIN + .01, -.24, -.025, Z0, Z1, F_BED, T, bevel=0, cell=0, skip=('-y',))
    # the cobbles either side of the drain: modelled setts by the pilot, the shader's courses elsewhere
    for side in (1, -1):
        lo, hi = DRAIN, GUTTER
        if T.fine:
            setts(g, T, side, lo + .01, hi - .01, SETTS_Z[0], SETTS_Z[1], seed=11 + side)
            spans = ((Z0, SETTS_Z[0]), (SETTS_Z[1], Z1))
            sk.box(g, side * lo, side * hi, -.06, -.022, SETTS_Z[0], SETTS_Z[1], F_BED, T, bevel=0, cell=0, skip=('-y',))
        else:
            spans = ((Z0, Z1),)
        for z0, z1 in spans:
            sk.grid_face(g, (side * lo if side > 0 else side * hi, 0.0, z0), ((hi - lo), 0, 0), (0, 0, z1 - z0), 0, 0, F_COBBLE, (0, 1, 0),
                         (2.0, 4.0, 8.0)[T.i])
        # the stone sill along the fronts, the gutter in front of it: a kerb lip, a wet channel
        ze = Z1 if side < 0 else CORNER_Z
        sbox(g, T, side, LANE, LANE + .32, -.05, .015, Z0, ze, F_STONE, bevel=.01, skip=('-y',), cell=(5.0, 8.0, 0)[T.i])
        sbox(g, T, side, GUTTER, GUTTER + .07, -.06, .012, Z0, ze, F_KERB, bevel=.01, skip=('-y',), cell=(5.0, 8.0, 0)[T.i])
        sk.grid_face(g, (side * (GUTTER + .07) if side > 0 else side * LANE, -.045, Z0), (LANE - GUTTER - .07, 0, 0), (0, 0, ze - Z0), 0, 0,
                     F_WATER, (0, 1, 0), (5.0, 8.0, 12.0)[T.i])
    # puddles on the cobbles, beyond the setts
    for k, (x, z, a, b) in enumerate(((-.9, -6.4, .7, .45), (1.1, 4.6, .55, .4), (-1.2, 7.8, .8, .5), (.8, -8.9, .6, .35),
                                      (-.7, -12.5, .9, .5), (1.2, 10.5, .7, .4))):
        if T.i == 2 and k > 2:
            break
        n = (10, 8, 6)[T.i]
        pts = []
        for i in range(n):
            t = TAU * i / n
            w = 1 + .18 * math.sin(3 * t + k) + .1 * math.cos(5 * t + 2 * k)
            pts.append((x + a * w * math.cos(t), .004, z + b * w * math.sin(t)))
        g.face(pts, F_WATER, normal=(0, 1, 0))


def setts(g, T, side, u0, u1, z0, z1, seed=0):
    """Small rounded setts in rows across the lane (each a top whose corner normals lean out: the light rolls over it), their
    sizes, heights and tilts jittered, the joints' bed under them."""
    r = mk.rng(seed)
    pz, pw = .175, .25     # the rows' pitch along z, the setts' pitch across
    jz, jw = .018, .018    # the joints
    z = z1
    row = 0
    while z - pz > z0 - 1e-6:
        zz0, zz1 = z - pz + jz / 2, z - jz / 2
        u = u0 - (pw / 2 if row % 2 else 0.0)
        while u < u1 - .03:
            a, b = max(u0, u + jw / 2), min(u1, u + pw - jw / 2)
            if b - a > .05:
                y = float(r.uniform(-.008, .002))
                lean = .45
                dz = float(r.uniform(-.01, .01))
                x0, x1 = side * a, side * b
                pts = ((x0, y, zz0 + dz), (x1, y, zz0 - dz), (x1, y, zz1 - dz), (x0, y, zz1 + dz))
                sx = 1.0 if x1 > x0 else -1.0
                nrm = (sk._norm((-lean * sx, 1, -lean)), sk._norm((lean * sx, 1, -lean)), sk._norm((lean * sx, 1, lean)),
                       sk._norm((-lean * sx, 1, lean)))
                f = F_SETT if (row + int(u * 7)) % 5 else F_SETT_DK
                if T.fine:
                    # the crown: the middle stands up, the four corners round down to the joints
                    top = ((x0 + x1) / 2 + float(r.uniform(-.015, .015)), y + float(r.uniform(.009, .015)), (zz0 + zz1) / 2)
                    for i in range(4):
                        g.face((pts[i], pts[(i + 1) % 4], top), f, normals=(nrm[i], nrm[(i + 1) % 4], (0.0, 1.0, 0.0)))
                else:
                    g.face(pts, f, normals=nrm)
            u += pw
        z -= pz
        row += 1


def grate(g, T, zc):
    """An iron grate over the drain: a frame, bars across, the dark drain under."""
    sk.box(g, -DRAIN, DRAIN, -.2, 0.0, zc - .3, zc - .24, F_IRON, T, bevel=.006, cell=0, skip=('-y',))
    sk.box(g, -DRAIN, DRAIN, -.2, 0.0, zc + .24, zc + .3, F_IRON, T, bevel=.006, cell=0, skip=('-y',))
    for x0 in (-DRAIN, DRAIN - .05):
        sk.box(g, x0, x0 + .05, -.2, 0.0, zc - .24, zc + .24, F_IRON, T, bevel=.006, cell=0, skip=('-y', '-z', '+z'))
    for k in range(6):
        z = zc - .2 + .4 * k / 5
        g.face(((-DRAIN + .05, -.004, z - .014), (DRAIN - .05, -.004, z - .014), (DRAIN - .05, -.004, z + .014), (-DRAIN + .05, -.004, z + .014)),
               F_IRON, normal=(0, 1, 0))
    g.face(((-DRAIN + .05, -.16, zc - .24), (DRAIN - .05, -.16, zc - .24), (DRAIN - .05, -.16, zc + .24), (-DRAIN + .05, -.16, zc + .24)),
           F_BED, normal=(0, 1, 0))


# ---------------------------------------------------------------------------------------------------------------------------
# The shopfronts: a bay between two posts, two storeys
# ---------------------------------------------------------------------------------------------------------------------------
def bay(g, T, s, za, zb, ground, upper, window, sign, extra):
    near = hero(za, zb) and T.i < 2
    r = mk.rng(int(abs(za * 100)) + (7 if s > 0 else 0))
    zl, zh = zb + .11, za - .11           # between the posts
    # the posts (one at the bay's start; the last bay its end too), on stone footings
    for zp in ((za, zb) if zb <= Z0 + .01 else (za,)):
        sbox(g, T, s, LANE, LANE + .22, 0.0, EAVE_Y + .2, zp - .11, zp + .11, F_TIMBER, bevel=.015, skip=('-y',))
        if near:
            sbox(g, T, s, LANE - .04, LANE + .26, -.04, .1, zp - .15, zp + .15, F_STONE, bevel=.015, skip=('-y',))
    # the beams over the ground floor and under the eave
    sbox(g, T, s, LANE, LANE + .22, 2.84, 3.02, zb, za, F_TIMBER_Z, bevel=.012, skip=(out_face(s),))
    if T.i < 2:
        sbox(g, T, s, LANE, LANE + .22, EAVE_Y, EAVE_Y + .2, zb, za, F_TIMBER_Z, bevel=.012, skip=(out_face(s),))
    # the ground floor
    if ground in ('shut', 'tshut'):
        shutters(g, T, s, zl, zh, F_SHUTTER_TEAL if ground == 'tshut' else F_SHUTTER, near, r)
    elif ground == 'half':
        zm = (zl + zh) / 2
        shutters(g, T, s, zl, zm, F_SHUTTER, near, r)
        interior(g, T, s, zm, zh, near, r, counter=False)
        # the open shutter folded back against the post
        sbox(g, T, s, LANE - .055, LANE - .005, .06, 2.78, zh - .02 - (zh - zm) * .45, zh - .02, F_SHUTTER, bevel=.006)
    else:
        interior(g, T, s, zl, zh, near, r, counter=ground == 'open')
    # the upper floor: plaster between the posts, a paper window (or the balcony's door), a rail under the window
    sbox(g, T, s, LANE + .08, LANE + .18, 3.02, EAVE_Y, zl, zh, F_PLASTER if (za * 3) % 2 < 1 else F_PLASTER_DK, bevel=.0,
         skip=(out_face(s), '+y', '-y'), cell=(1.2, 3.0, 0)[T.i])
    door = upper == 'balcony'
    wy0, wy1 = (3.36, 5.2) if door else (3.95, 5.05)
    ww = min(1.6, (zh - zl) * (.62 if door else .5))
    zc = (zl + zh) / 2 + float(r.uniform(-.15, .15))
    paper_window(g, T, s, zc - ww / 2, zc + ww / 2, wy0, wy1, {'lit': F_PAPER_LIT, 'teal': F_PAPER_TEAL, 'dark': F_PAPER}[window], near)
    # a balcony, or a tiled pent roof over the shop
    if upper == 'balcony':
        balcony(g, T, s, zl, zh, near)
    else:
        pent(g, T, s, zb, za, near)
    # the deep eave
    eave(g, T, s, zb, za, near, r)
    if sign and upper == 'pent':
        zs = max((za - .45, zb + .45, (za + zb) / 2), key=lambda z: min(abs(z - L[0]) for L in LANTERNS))
        blade_sign(g, T, s, zs, F_GLYPH_TEAL if sign == 'teal' else F_GLYPH_MAG, near, r)
    if extra == 'awning':
        awning(g, T, s, zl, zh, near, r)
    elif extra == 'noren' and ground in ('open', 'half', 'stall'):
        noren(g, T, s, zl, zh, near, r)
    elif extra == 'goods':
        goods(g, T, s, (zl + zh) / 2, near, r)


def shutters(g, T, s, z0, z1, f, near, r):
    """Board shutters across the shop's front, two battens, an iron bar."""
    sbox(g, T, s, LANE + .05, LANE + .12, 0.0, 2.84, z0, z1, f, bevel=.008, skip=(out_face(s), '-y'), cell=(1.2, 3.0, 0)[T.i])
    if not near:
        return
    for y in (.55, 2.1):
        sbox(g, T, s, LANE + .02, LANE + .05, y, y + .12, z0 + .04, z1 - .04, f, bevel=.006, skip=(out_face(s),))
    yb = float(r.uniform(1.1, 1.4))
    sbox(g, T, s, LANE - .005, LANE + .02, yb, yb + .05, z0 + .1, z1 - .1, F_IRON, bevel=0, skip=(out_face(s),))


def interior(g, T, s, z0, z1, near, r, counter=True):
    """An open shop: a dim lit back wall, dark sides and ceiling, a raised floor and its step, a counter, shelves with jars."""
    back = LANE + 1.35
    out = (-s, 0, 0)
    flat(g, [(s * back, .2, z0), (s * back, .2, z1), (s * back, 2.84, z1), (s * back, 2.84, z0)], F_INTERIOR, toward=out)
    for zz, nz in ((z0, 1), (z1, -1)):
        flat(g, [(s * (LANE + .12), .2, zz), (s * back, .2, zz), (s * back, 2.84, zz), (s * (LANE + .12), 2.84, zz)], F_INSIDE, toward=(0, 0, nz))
    flat(g, [(s * (LANE + .05), 2.84, z0), (s * back, 2.84, z0), (s * back, 2.84, z1), (s * (LANE + .05), 2.84, z1)], F_INSIDE, toward=(0, -1, 0))
    flat(g, [(s * (LANE + .25), .2, z0), (s * back, .2, z0), (s * back, .2, z1), (s * (LANE + .25), .2, z1)], F_TIMBER_Z, toward=(0, 1, 0))
    sbox(g, T, s, LANE + .02, LANE + .26, -.03, .2, z0, z1, F_STONE, bevel=.012, skip=(out_face(s), '-y'))
    if not near:
        return
    if counter:
        zc0, zc1 = z0 + (z1 - z0) * .12, z0 + (z1 - z0) * .7
        sbox(g, T, s, LANE + .3, LANE + .7, .2, 1.0, zc0, zc1, F_TIMBER_Z, bevel=.012, skip=('-y',))
        sbox(g, T, s, LANE + .26, LANE + .74, 1.0, 1.05, zc0 - .03, zc1 + .03, F_TIMBER_Z, bevel=.008)
        # bowls on the counter
        for k in range(2):
            zz = zc0 + .3 + k * .35
            sk.lathe(g, [(.0, 1.05), (.05, 1.06), (.075, 1.11)], (s * (LANE + .5), 0.0, zz), F_CERAMIC_BLUE if k % 2 else F_CERAMIC, 6)
    # shelves on the back wall, jars on them
    for y in (1.35, 1.95):
        sbox(g, T, s, back - .32, back - .01, y, y + .04, z0 + .05, z1 - .05, F_TIMBER_Z, bevel=0, skip=(out_face(s),))
        n = int((z1 - z0) / .45)
        put_ = 0
        for k in range(n):
            if r.uniform() < .45 or put_ >= 2:
                continue
            put_ += 1
            zz = z0 + .25 + k * .45
            h = float(r.uniform(.14, .3))
            rad = float(r.uniform(.05, .09))
            sk.lathe(g, [(rad, y + .04), (rad * 1.1, y + .04 + h * .6), (rad * .5, y + .04 + h)], (s * (back - .17), 0.0, zz),
                     F_CERAMIC if k % 3 else F_CERAMIC_BLUE, 5, cap1=True)


def paper_window(g, T, s, z0, z1, y0, y1, f, near):
    """A paper window (or sliding door) in the upper wall: a timber frame, the paper, a lattice."""
    sbox(g, T, s, LANE + .03, LANE + .1, y0 - .07, y1 + .07, z0 - .07, z1 + .07, F_TIMBER_Z, bevel=.008, skip=(out_face(s),))
    u = LANE + .027
    out = (-s, 0, 0)
    g.face([(s * u, y0, z0), (s * u, y0, z1), (s * u, y1, z1), (s * u, y1, z0)], f, normal=out)
    if not near:
        return
    # the lattice: thin bars across the paper
    ub = u - .004
    nz = max(2, int((z1 - z0) / .22))
    for k in range(1, nz):
        z = z0 + (z1 - z0) * k / nz
        g.face([(s * ub, y0, z - .011), (s * ub, y0, z + .011), (s * ub, y1, z + .011), (s * ub, y1, z - .011)], F_TIMBER, normal=out)
    ny = max(2, int((y1 - y0) / .3))
    for k in range(1, ny):
        y = y0 + (y1 - y0) * k / ny
        g.face([(s * (ub - .002), y - .011, z0), (s * (ub - .002), y - .011, z1), (s * (ub - .002), y + .011, z1), (s * (ub - .002), y + .011, z0)],
               F_TIMBER, normal=out)


def balcony(g, T, s, z0, z1, near):
    """A timber balcony on the upper floor: its floor on two struts, a railing of round balusters."""
    sbox(g, T, s, 1.72, LANE + .1, 3.18, 3.3, z0, z1, F_TIMBER_Z, bevel=.012)
    if T.i == 2:
        sbox(g, T, s, 1.72, 1.78, 3.3, 4.05, z0, z1, F_TIMBER_Z, bevel=0, skip=('-y',))
        return
    for zz in (z0 + .12, z1 - .12):
        sk.cylinder(g, (s * (LANE + .05), 2.72, zz), (s * 1.86, 3.2, zz), .04, F_TIMBER, (5, 4, 4)[T.i], caps=False)
    sbox(g, T, s, 1.72, 1.8, 3.98, 4.05, z0, z1, F_TIMBER_Z, bevel=.008)
    for zz in (z0, z1):
        sbox(g, T, s, 1.72, LANE + .02, 3.98, 4.05, zz - .035 if zz == z1 else zz, zz + .035 if zz == z0 else zz, F_TIMBER_X, bevel=.006)
        sbox(g, T, s, 1.72, 1.8, 3.3, 3.98, (zz - .08) if zz == z1 else zz, zz + .08 if zz == z0 else zz, F_TIMBER, bevel=.008)
    n = int((z1 - z0) / (.35 if near else .6))
    for k in range(1, n):
        z = z0 + (z1 - z0) * k / n
        sk.cylinder(g, (s * 1.76, 3.3, z), (s * 1.76, 3.98, z), .02, F_TIMBER, 4, caps=False)


def pent(g, T, s, z0, z1, near):
    """A tiled pent roof over the shop, out over the gutter: its tiles, the underside, a fascia board."""
    ue, ye, ut, yt = 1.62, 3.0, LANE + .1, 3.42
    n = sk._norm((-(yt - ye) * s, (ut - ue), 0))
    n = n if n[1] > 0 else (-n[0], -n[1], -n[2])
    g.face([(s * ue, ye, z0), (s * ue, ye, z1), (s * ut, yt, z1), (s * ut, yt, z0)], F_TILE, normal=n)
    g.face([(s * ue, ye - .02, z0), (s * ue, ye - .02, z1), (s * ut, yt - .02, z1), (s * ut, yt - .02, z0)], F_TIMBER_Z, normal=(-n[0], -n[1], -n[2]))
    if T.i < 2:
        sbox(g, T, s, ue - .04, ue + .02, ye - .09, ye + .02, z0, z1, F_TIMBER_Z, bevel=.006)
    if near and T.fine:
        tiles(g, T, s, ue, ye, ut, yt, z1, z0, step=.34)


def eave(g, T, s, z0, z1, near, r):
    """The deep eave and the roof going up behind it: tiles, the underside with its rafters, a fascia, the ridge."""
    ue, ye = 1.5, EAVE_Y + .02 + float(r.uniform(-.02, .02))
    ur, yr = LANE + 2.4, 7.3
    n = sk._norm((-(yr - ye) * s, (ur - ue), 0))
    n = n if n[1] > 0 else (-n[0], -n[1], -n[2])
    g.face([(s * ue, ye, z0), (s * ue, ye, z1), (s * ur, yr, z1), (s * ur, yr, z0)], F_TILE, normal=n)
    g.face([(s * ue, ye - .03, z0), (s * ue, ye - .03, z1), (s * (LANE + .18), EAVE_Y + .2, z1), (s * (LANE + .18), EAVE_Y + .2, z0)],
           F_TIMBER_X, normal=(0, -1, 0))
    if T.i < 2:
        sbox(g, T, s, ue - .05, ue + .03, ye - .13, ye + .03, z0, z1, F_TIMBER_Z, bevel=.008)
    if near:
        n_r = int((z1 - z0) / .45)

        def yu(u):
            return ye - .035 + (EAVE_Y + .2 - .005 - (ye - .035)) * (u - ue) / (LANE + .18 - ue)
        for k in range(n_r):
            z = z0 + (z1 - z0) * (k + .5) / n_r
            g.face([(s * (ue + .03), yu(ue + .03), z - .035), (s * (ue + .03), yu(ue + .03), z + .035), (s * (LANE + .15), yu(LANE + .15), z + .035),
                    (s * (LANE + .15), yu(LANE + .15), z - .035)], F_TIMBER_X, normal=(0, -1, 0))
        if T.fine:
            tiles(g, T, s, ue, ye, ue + 1.0, ye + (yr - ye) * 1.0 / (ur - ue), z1, z0, step=.34)
        sbox(g, T, s, ur - .12, ur + .12, yr - .06, yr + .12, z0, z1, F_TILE, bevel=.02)


def blade_sign(g, T, s, z, f, near, r):
    """A signboard hung out over the lane from an iron arm, blank lit glyph panels on both faces."""
    h = float(r.uniform(1.2, 1.5))
    y1 = 4.95
    y0 = y1 - h
    sbox(g, T, s, 1.66, 2.14, y0, y1, z - .035, z + .035, F_SIGN, bevel=.01)
    sk.cylinder(g, (s * LANE, 5.15, z), (s * 1.6, 5.15, z), .015, F_IRON, (5, 4, 3)[T.i], caps=False)
    for uu in (1.72, 2.08):
        sk.cylinder(g, (s * uu, 5.15, z), (s * uu, y1, z), .006, F_IRON, 3, caps=False)
    n = 3 if near else 1
    for k in range(n):
        gy1 = y1 - .1 - k * (h - .12) / n
        gy0 = gy1 - (h - .12) / n + .06
        for nz in (1, -1):
            zz = z + nz * .037
            g.face([(s * 1.74, gy0, zz), (s * 2.06, gy0, zz), (s * 2.06, gy1, zz), (s * 1.74, gy1, zz)], f, normal=(0, 0, nz))


def awning(g, T, s, z0, z1, near, r):
    """A canvas awning out from the ground beam, sagging between its two poles."""
    nu, nv = ((6, 3), (4, 2), (2, 1))[T.i]
    u_in, y_in, u_out, y_out = LANE + .02, 2.82, 1.25, 2.38
    grid = []
    for j in range(nv + 1):
        v = j / nv
        row = []
        for i in range(nu + 1):
            t = i / nu
            sag = .1 * math.sin(math.pi * t) * v
            row.append((s * (u_in + (u_out - u_in) * v), y_in + (y_out - y_in) * v - sag, z0 + (z1 - z0) * t))
        grid.append(row)
    nrm = []
    for j in range(nv + 1):
        row = []
        for i in range(nu + 1):
            du = sk._sub(grid[j][min(nu, i + 1)], grid[j][max(0, i - 1)])
            dv = sk._sub(grid[min(nv, j + 1)][i], grid[max(0, j - 1)][i])
            nn = sk._norm(sk._cross(du, dv))
            row.append(nn if nn[1] > 0 else (-nn[0], -nn[1], -nn[2]))
        nrm.append(row)
    for j in range(nv):
        for i in range(nu):
            q = [(j, i), (j, i + 1), (j + 1, i + 1), (j + 1, i)]
            pts = [grid[a][b] for a, b in q]
            ns = [nrm[a][b] for a, b in q]
            g.face(pts, F_CANVAS, normals=ns)
            g.face(pts[::-1], F_CANVAS, normals=[(-n[0], -n[1], -n[2]) for n in ns[::-1]])
    # the valance along the front, a little ragged
    if near:
        mk.cloth(g, grid[-1][0], grid[-1][-1], .22, F_CANVAS, T, seed=int(abs(z0) * 10), sway=.02, tear=.05, cells=((6, 4, 2)[T.i], 1))
    for zz in (z0 + .05, z1 - .05):
        sk.cylinder(g, (s * (u_out + .04), 0.0, zz), (s * (u_out + .04), y_out, zz), .025, F_TIMBER, (5, 4, 3)[T.i], caps=False)


def noren(g, T, s, z0, z1, near, r):
    """Split curtains hanging in the shop's doorway from the beam."""
    n = 3 if near else 1
    f = F_NOREN if r.uniform() < .6 else F_NOREN_RED
    w = (z1 - z0) / n
    for k in range(n):
        a = (s * (LANE - .02), 2.83, z0 + k * w + .02)
        b = (s * (LANE - .02), 2.83, z0 + (k + 1) * w - .02)
        mk.cloth(g, a, b, .78, f, T, seed=k + int(abs(z0) * 7), sway=.04, tear=.03, cells=((2, 2), (1, 2), (1, 1))[T.i])


def goods(g, T, s, z, near, r):
    """A stall's goods out front: a trestle of baskets, crates under it."""
    sbox(g, T, s, 1.5, LANE - .02, .72, .78, z - .9, z + .9, F_TIMBER_Z, bevel=.008)
    for zz in (z - .8, z + .8):
        for uu in (1.56, LANE - .08):
            sk.cylinder(g, (s * uu, -.03, zz), (s * uu, .72, zz), .022, F_TIMBER, 4, caps=False)
    for k in range(3):
        basket(g, T, (s * 1.86, .78, z - .55 + k * .55), .19, .14, F_BASKET, cap=k == 1)
    crate(g, T, (s * 1.86, 0.0, z - .45), .5, .32, .34, .2)
    crate(g, T, (s * 1.86, 0.0, z + .35), .5, .32, .34, -.15)


# ---------------------------------------------------------------------------------------------------------------------------
# Props
# ---------------------------------------------------------------------------------------------------------------------------
def crate(g, T, at, w, h, d, turn):
    """A slatted crate: the box, two bands of slats across each long face."""
    t = mk.Geo()
    sk.box(t, -w / 2, w / 2, 0.0, h, -d / 2, d / 2, F_CRATE, T, bevel=.012, cell=0, skip=('-y',))
    if T.fine:
        for y in (h * .25, h * .75):
            t.face([(-w / 2 + .01, y - .03, -d / 2 - .01), (w / 2 - .01, y - .03, -d / 2 - .01), (w / 2 - .01, y + .03, -d / 2 - .01),
                    (-w / 2 + .01, y + .03, -d / 2 - .01)], F_CRATE, normal=(0, 0, -1))
    put(g, t, sk.quat_axis(0, 1, 0, turn), at)


def basket(g, T, at, rad, h, f, cap=False):
    sides = (7, 6, 5)[T.i]
    x, y, z = at
    sk.lathe(g, [(rad * .7, 0.0), (rad * .95, h * .3), (rad * 1.06, h), (rad * .96, h * 1.02)], (x, y, z), f, sides)
    sk.lathe(g, [(rad * .96, h * .9), (0.0, h * .9 + .01)], (x, y, z), F_CRATE if not cap else F_CERAMIC, sides)


def barrel(g, T, at, rad, h):
    x, y, z = at
    sides = (10, 8, 6)[T.i]
    sk.lathe(g, [(rad * .85, 0.0), (rad, h * .5), (rad * .85, h), (rad * .8, h)], (x, y, z), F_BARREL, sides, cap1=True)
    if T.detail:
        for yy in (h * .15, h * .85):
            k = .85 + .15 * math.sin(math.pi * yy / h)
            sk.lathe(g, [(rad * k + .006, yy - .025), (rad * k + .006, yy + .025)], (x, y, z), F_IRON, sides)


def props(g, T):
    """Crates, baskets and barrels against the fronts, away from the pilot's ground."""
    stacks = ((-1, 4.1, 'barrels'), (1, 5.4, 'baskets'), (-1, -6.6, 'crates'), (1, -9.0, 'crates'), (-1, -10.6, 'baskets'),
              (1, -12.6, 'barrels'))
    r = mk.rng(57)
    for k, (s, z, kind) in enumerate(stacks):
        if T.i == 2 and k > 3:
            break
        u = 1.72
        if kind == 'crates':
            crate(g, T, (s * u, 0.0, z), .52, .36, .38, float(r.uniform(-.2, .2)))
            crate(g, T, (s * (u - .05), 0.0, z + .55), .5, .34, .36, float(r.uniform(-.2, .2)))
            crate(g, T, (s * (u + .02), .36, z + .2), .48, .32, .36, float(r.uniform(-.3, .3)))
            basket(g, T, (s * (u - .3), 0.0, z - .5), .2, .3, F_BASKET)
        elif kind == 'baskets':
            for j in range(3 if T.i < 2 else 2):
                basket(g, T, (s * (u + float(r.uniform(-.15, .1))), 0.0, z + j * .42 - .6), float(r.uniform(.17, .23)), float(r.uniform(.22, .34)), F_BASKET,
                       cap=j % 2 == 1)
            basket(g, T, (s * u, .3, z - .4), .17, .2, F_BASKET)
        else:
            barrel(g, T, (s * (u + .05), 0.0, z), .27, .8)
            barrel(g, T, (s * (u + .02), 0.0, z + .62), .25, .74)
            if T.i < 2:
                sk.box(g, s * (u - .2), s * (u + .25), .8, .84, z - .25, z + .25, F_TIMBER_Z, T, bevel=.006, cell=0)


def cart(g, T, at):
    """The noodle cart: a timber cart on two wheels, its counter and pot, a small tiled canopy on four posts with a valance and a
    lantern, a hanging lit menu, two stools."""
    x, z = at
    t = mk.Geo()
    L, W = 1.7, .78
    # (built along local x = the lane's z: the cart stands lengthwise against the front)
    sk.box(t, -L / 2, L / 2, .35, .95, -W / 2, W / 2, F_LACQUER_X, T, bevel=.015, cell=0)
    sk.box(t, -L / 2 - .04, L / 2 + .04, .95, 1.0, -W / 2 - .06, W / 2 + .03, F_TIMBER_X, T, bevel=.01, cell=0)
    if T.detail:
        for k in range(4):
            xx = -L / 2 + .2 + k * (L - .4) / 3
            sk.box(t, xx - .03, xx + .03, .4, .9, -W / 2 - .012, -W / 2, F_TIMBER, T, bevel=0, cell=0, skip=('+z',))
    # the wheels (on the cart's long sides) and the shafts
    for sz in (-1, 1):
        wc = (.25, .42, sz * (W / 2 + .06))
        sk.lathe(t, [(.0, -.03), (.38, -.03), (.42, 0.0), (.38, .035), (.08, .035), (0.0, .05)], wc, F_TIMBER, (14, 10, 6)[T.i], axis=(0, 0, sz))
        sk.cylinder(t, (L / 2 - .1, .7, sz * (W / 2 - .08)), (L / 2 + .75, .55, sz * (W / 2 - .02)), .025, F_TIMBER, (5, 4, 3)[T.i])
    sk.box(t, -L / 2 + .05, -L / 2 + .12, 0.0, .35, -W / 2 + .05, W / 2 - .05, F_TIMBER, T, bevel=.008, cell=0, skip=('-y',))
    # the pot and its lid, a ladle, bowls stacked
    sk.lathe(t, [(.0, 1.0), (.2, 1.0), (.23, 1.08), (.22, 1.24), (.24, 1.27), (.2, 1.27)], (-.35, 0.0, 0.0), F_IRON, (12, 8, 6)[T.i])
    sk.lathe(t, [(.2, 1.26), (.18, 1.3), (.04, 1.34), (.03, 1.38), (0.0, 1.39)], (-.35, 0.0, 0.0), F_IRON, (12, 8, 6)[T.i])
    if T.detail:
        sk.cylinder(t, (-.25, 1.3, .1), (-.02, 1.62, .25), .01, F_TIMBER, 4)
        for k in range(4):
            sk.lathe(t, [(.0, 1.0 + k * .045), (.05, 1.0 + k * .045), (.075, 1.045 + k * .045)], (.25, 0.0, -.1), F_CERAMIC_BLUE, 7)
        sk.lathe(t, [(.0, 1.0), (.05, 1.0), (.055, 1.18), (.035, 1.24), (.03, 1.27)], (.55, 0.0, .15), F_CERAMIC, 7, cap1=True)
    # the canopy: four posts, a small gabled tile roof, a valance, a lantern at its corner
    for px in (-L / 2 + .05, L / 2 - .05):
        for pz in (-W / 2 + .05, W / 2 - .05):
            sk.cylinder(t, (px, .95, pz), (px, 2.25, pz), .025, F_TIMBER, (5, 4, 3)[T.i], caps=False)
    rx0, rx1 = -L / 2 - .25, L / 2 + .25
    for sz in (-1, 1):
        p0, p1 = (rx0, 2.52, 0.0), (rx1, 2.52, 0.0)
        p2, p3 = (rx1, 2.22, sz * (W / 2 + .3)), (rx0, 2.22, sz * (W / 2 + .3))
        nn = sk._norm(sk._cross(sk._sub(p1, p0), sk._sub(p3, p0)))
        nn = nn if nn[1] > 0 else (-nn[0], -nn[1], -nn[2])
        t.face([p0, p1, p2, p3], F_TILE, normal=nn)
        t.face([p3, p2, p1, p0], F_TIMBER_X, normal=(-nn[0], -nn[1], -nn[2]))
        if T.detail:
            mk.cloth(t, (rx0, 2.2, sz * (W / 2 + .3)), (rx1, 2.2, sz * (W / 2 + .3)), .3, F_NOREN_RED, T, seed=5 + sz, sway=.02, tear=.06,
                     cells=((5, 2), (3, 1), (2, 1))[T.i])
    sk.box(t, rx0 - .02, rx1 + .02, 2.5, 2.58, -.06, .06, F_TILE, T, bevel=.012, cell=0)
    sk.box(t, -L / 2 + .05 - .03, L / 2 - .05 + .03, 2.2, 2.26, -W / 2 + .02, W / 2 - .02, F_TIMBER_X, T, bevel=.006, cell=0, skip=('+y',))
    # a lantern at the canopy's corner, a hanging menu board lit
    lp = (rx1 - .05, 2.0, -W / 2 - .2)
    sk.lathe(t, [(.0, -.36), (.09, -.33), (.12, -.2), (.09, -.07), (0.0, -.04)], lp, F_LANTERNS[0], (8, 6, 5)[T.i])
    sk.cylinder(t, (lp[0], lp[1] - .04, lp[2]), (lp[0], 2.22, lp[2]), .006, F_CORD, 3, caps=False)
    sk.box(t, -.4, .25, 1.62, 2.0, -W / 2 - .05, -W / 2 - .02, F_SIGN, T, bevel=.006, cell=0)
    t.face([(-.36, 1.66, -W / 2 - .052), (.21, 1.66, -W / 2 - .052), (.21, 1.96, -W / 2 - .052), (-.36, 1.96, -W / 2 - .052)], F_GLYPH_MAG,
           normal=(0, 0, -1))
    # stools in front
    for sx in (-.45, .35):
        sk.lathe(t, [(.0, .45), (.17, .45), (.17, .5), (0.0, .5)], (sx, 0.0, -W / 2 - .45), F_TIMBER_X, (8, 6, 5)[T.i])
        for k in range(3):
            a = k * TAU / 3
            sk.cylinder(t, (sx + .13 * math.cos(a), 0.0, -W / 2 - .45 + .13 * math.sin(a)), (sx + .1 * math.cos(a), .45, -W / 2 - .45 + .1 * math.sin(a)),
                        .016, F_TIMBER, 3, caps=False)
    # standing lengthwise along the lane, its counter side facing the lane (local -z toward -x)
    put(g, t, sk.quat_axis(0, 1, 0, math.pi / 2), (x, 0.0, z))


# ---------------------------------------------------------------------------------------------------------------------------
# The far gate, the lane beyond, the rooftops
# ---------------------------------------------------------------------------------------------------------------------------
def gate(g, T):
    """A red wooden gate across the lane: round posts on stone bases, a tie beam and a lintel with upturned ends, a small tiled
    roof, a lit plaque between the beams, braces, two small lanterns."""
    z = GATE_Z
    for sx in (-1, 1):
        x = sx * 1.92
        mk.slab(g, mk.circle(x, z, .3, (8, 6, 6)[T.i], .4), -.04, .35, F_STONE, T, bevel=.03)
        sk.lathe(g, [(.19, .35), (.17, .55), (.16, 4.5)], (x, 0.0, z), F_LACQUER, (10, 8, 6)[T.i], cap1=True)
        if T.detail:
            sk.cylinder(g, (x, 3.2, z), (x - sx * .55, 3.72, z), .045, F_LACQUER, 5, caps=False)
    sk.box(g, -2.45, 2.45, 3.62, 3.84, z - .11, z + .11, F_LACQUER_X, T, bevel=.015, cell=0)
    # the lintel: its ends sweep up
    path = []
    n = (9, 7, 5)[T.i]
    for i in range(n):
        x = -3.0 + 6.0 * i / (n - 1)
        path.append((x, 4.5 + .32 * (abs(x) / 3.0) ** 3, z))
    sk.sweep(g, sk.rect_section(.24, .3, .03), path, F_LACQUER_X, up=(0, 0, 1))
    # the roof over it
    for sz in (-1, 1):
        p0, p1 = (-3.1, 5.05, z), (3.1, 5.05, z)
        p2, p3 = (3.25, 4.72, z + sz * .55), (-3.25, 4.72, z + sz * .55)
        nn = sk._norm(sk._cross(sk._sub(p1, p0), sk._sub(p3, p0)))
        nn = nn if nn[1] > 0 else (-nn[0], -nn[1], -nn[2])
        g.face([p0, p1, p2, p3], F_TILE, normal=nn)
        g.face([p3, p2, p1, p0], F_TIMBER_X, normal=(-nn[0], -nn[1], -nn[2]))
    sk.box(g, -3.15, 3.15, 5.0, 5.12, z - .08, z + .08, F_TILE, T, bevel=.015, cell=0)
    # the plaque between the beams, its glyphs lit
    sk.box(g, -.5, .5, 3.86, 4.42, z - .05, z + .05, F_SIGN, T, bevel=.01, cell=0)
    for nz in (1, -1):
        g.face([(-.42, 3.93, z + nz * .052), (.42, 3.93, z + nz * .052), (.42, 4.35, z + nz * .052), (-.42, 4.35, z + nz * .052)], F_GLYPH_MAG,
               normal=(0, 0, nz))
    # two small lanterns under the tie beam
    for sx in (-1, 1):
        lp = (sx * 1.3, 3.62, z + .02)
        sk.cylinder(g, lp, (lp[0], lp[1] - .12, lp[2]), .005, F_CORD, 3, caps=False)
        sk.lathe(g, [(.0, -.46), (.1, -.42), (.13, -.3), (.1, -.16), (0.0, -.12)], lp, F_LANTERNS[1], (8, 6, 5)[T.i])


def skyline(g, T):
    """Beyond the fronts: rooftops of the taller houses behind, the house front closing the lane past the gate (a lit doorway),
    the dark roofs beyond both ends."""
    r = mk.rng(71)
    far_end(g, T)
    # rooftops behind the fronts both sides: taller houses, gables, a tower or two
    for side in (1, -1):
        z = Z1 if side < 0 else CORNER_Z - .3
        while z > Z0 + 1.5:
            L = float(r.uniform(4.0, 7.5))
            u0 = float(r.uniform(5.2, 7.0))
            h = float(r.uniform(7.6, 10.5))
            house(g, T, side, u0, u0 + float(r.uniform(3.5, 6.0)), z - L, z, h, r)
            z -= L + float(r.uniform(-.5, .8))
    # beyond both ends of the lane: dark roofs piled up
    for zc, sgn in ((END_Z - 4.0, -1), (Z1 + 6.0, 1)):
        for k in range((5, 4, 3)[T.i]):
            x = -9.0 + 18.0 * k / 4 + float(r.uniform(-1.0, 1.0))
            zz = zc + sgn * float(r.uniform(0, 8.0))
            w, d, h = float(r.uniform(3.0, 5.0)), float(r.uniform(3.0, 5.0)), float(r.uniform(6.0, 11.0))
            block_roof(g, T, x, zz, w, d, h, r)


def corner(g, T):
    """Where the lane turns at +z: the +x fronts end at CORNER_Z (their end wall and roof's gable), a side street goes off to +x,
    and a house front closes the lane at Z1 (its lit doorway and window, its eave), so the lane reads as going on round it."""
    # the side street's cobbles
    sk.grid_face(g, (LANE, 0.0, CORNER_Z), (5.5, 0, 0), (0, 0, Z1 - CORNER_Z), 0, 0, F_COBBLE, (0, 1, 0), (2.0, 4.0, 8.0)[T.i])
    # the end wall of the +x house at the corner, its gable under the roof
    zc = CORNER_Z - .11
    g.face([(LANE, 0.0, zc), (LANE + 2.4, 0.0, zc), (LANE + 2.4, EAVE_Y + .2, zc), (LANE, EAVE_Y + .2, zc)], F_PLASTER_DK, normal=(0, 0, 1))
    g.face([(1.5, EAVE_Y + .02, zc), (LANE + 2.4, EAVE_Y + .2, zc), (LANE + 2.4, 7.3, zc)], F_TIMBER_X, normal=(0, 0, 1))
    sbox(g, T, 1, LANE + 2.3, LANE + 2.52, 0.0, EAVE_Y + .2, zc - .11, zc + .11, F_TIMBER, bevel=.015, skip=('-y',))
    # the house closing the lane: its front across from the -x fronts to past the side street
    zf = Z1 + .02
    x0, x1 = -LANE - .3, LANE + 5.5
    g.face([(x0, -.05, zf), (x1, -.05, zf), (x1, EAVE_Y + .2, zf), (x0, EAVE_Y + .2, zf)], F_PLASTER, normal=(0, 0, -1))
    for xp in (-1.2, 1.6, 4.4):
        sk.box(g, xp - .11, xp + .11, 0.0, EAVE_Y + .2, zf - .2, zf, F_TIMBER, T, bevel=.015, cell=0, skip=('-y', '+z'))
    sk.box(g, x0, x1, 2.84, 3.02, zf - .2, zf, F_TIMBER_X, T, bevel=.012, cell=0, skip=('+z',))
    g.face([(-.9, .02, zf - .01), (1.3, .02, zf - .01), (1.3, 2.4, zf - .01), (-.9, 2.4, zf - .01)], F_INTERIOR, normal=(0, 0, -1))
    if T.i < 2:
        mk.cloth(g, (-.9, 2.82, zf - .03), (1.3, 2.82, zf - .03), .7, F_NOREN_RED, T, seed=31, sway=.03, tear=.03, cells=((3, 2), (2, 1), (1, 1))[T.i])
    g.face([(2.4, 3.7, zf - .01), (3.6, 3.7, zf - .01), (3.6, 4.8, zf - .01), (2.4, 4.8, zf - .01)], F_PAPER_LIT, normal=(0, 0, -1))
    g.face([(-1.9, 3.7, zf - .01), (-.6, 3.7, zf - .01), (-.6, 4.8, zf - .01), (-1.9, 4.8, zf - .01)], F_PAPER_TEAL, normal=(0, 0, -1))
    # its eave and roof
    g.face([(x0 - .3, EAVE_Y + .02, zf - 1.05), (x1 + .3, EAVE_Y + .02, zf - 1.05), (x1 + .3, 7.4, zf + 1.8), (x0 - .3, 7.4, zf + 1.8)], F_TILE,
           normal=(0, .9, -.45))
    g.face([(x0 - .3, EAVE_Y - .01, zf - 1.05), (x1 + .3, EAVE_Y - .01, zf - 1.05), (x1 + .3, EAVE_Y + .2, zf), (x0 - .3, EAVE_Y + .2, zf)],
           F_TIMBER_X, normal=(0, -1, 0))
    sk.box(g, x0 - .3, x1 + .3, EAVE_Y - .1, EAVE_Y + .05, zf - 1.1, zf - 1.0, F_TIMBER_X, T, bevel=.008, cell=0)
    if T.fine:
        n = int((x1 - x0) / .3)
        sec = [(-.05, 0), (-.035, .03), (0, .045), (.035, .03), (.05, 0)]
        for i in range(n):
            x = x0 + (x1 - x0) * (i + .5) / n
            sk.sweep(g, sec, [(x, EAVE_Y + .05, zf - 1.05), (x, EAVE_Y + .05 + .6 * (7.4 - EAVE_Y) / 2.85, zf - 1.05 + .6)], F_TILE, caps=False,
                     up=(0, 1, 0))


def far_end(g, T):
    """Past the gate: the fronts end at Z0 (their end walls), a cross street, and a low house across it at END_Z: posts, a lit
    doorway with its noren, a lattice window, a pent roof and its eave; the taller roofs beyond rise over it."""
    # the cross street's cobbles
    sk.grid_face(g, (-8.0, 0.0, END_Z), (16.0, 0, 0), (0, 0, Z0 - END_Z), 0, 0, F_COBBLE, (0, 1, 0), (3.0, 6.0, 12.0)[T.i])
    # the fronts' end walls
    for side in (1, -1):
        zc = Z0 + .11
        x0, x1 = side * LANE, side * (LANE + 2.4)
        flat(g, [(x0, 0.0, zc), (x1, 0.0, zc), (x1, EAVE_Y + .2, zc), (x0, EAVE_Y + .2, zc)], F_PLASTER_DK, toward=(0, 0, -1))
        flat(g, [(side * 1.5, EAVE_Y + .02, zc), (x1, EAVE_Y + .2, zc), (x1, 7.3, zc)], F_TIMBER_X, toward=(0, 0, -1))
    # the low house across
    z = END_Z
    x0, x1 = -7.5, 7.5
    ye = 3.55
    g.face([(x0, -.05, z), (x1, -.05, z), (x1, ye, z), (x0, ye, z)], F_PLASTER_DK, normal=(0, 0, 1))
    for xp in (-5.0, -1.6, 1.6, 5.0):
        sk.box(g, xp - .11, xp + .11, 0.0, ye, z, z + .2, F_TIMBER, T, bevel=.012, cell=0, skip=('-y', '-z'))
    sk.box(g, x0, x1, 2.5, 2.66, z, z + .2, F_TIMBER_X, T, bevel=.01, cell=0, skip=('-z',))
    g.face([(-1.3, .02, z + .01), (1.3, .02, z + .01), (1.3, 2.45, z + .01), (-1.3, 2.45, z + .01)], F_INTERIOR, normal=(0, 0, 1))
    if T.i < 2:
        mk.cloth(g, (-1.3, 2.48, z + .05), (1.3, 2.48, z + .05), .62, F_NOREN, T, seed=41, sway=.03, tear=.03, cells=((3, 2), (2, 1), (1, 1))[T.i])
    for wx0, f in ((2.4, F_PAPER_LIT), (-4.2, F_PAPER_TEAL)):
        sk.box(g, wx0 - .06, wx0 + 1.36, .95, 2.2, z, z + .06, F_TIMBER_X, T, bevel=.006, cell=0, skip=('-z',))
        g.face([(wx0, 1.01, z + .063), (wx0 + 1.3, 1.01, z + .063), (wx0 + 1.3, 2.14, z + .063), (wx0, 2.14, z + .063)], f, normal=(0, 0, 1))
        if T.fine:
            for k in range(1, 5):
                xx = wx0 + 1.3 * k / 5
                g.face([(xx - .01, 1.01, z + .066), (xx + .01, 1.01, z + .066), (xx + .01, 2.14, z + .066), (xx - .01, 2.14, z + .066)], F_TIMBER_X,
                       normal=(0, 0, 1))
    # its eave and roof, a lantern by the door
    flat(g, [(x0 - .3, ye - .05, z + 1.0), (x1 + .3, ye - .05, z + 1.0), (x1 + .3, 5.3, z - 2.4), (x0 - .3, 5.3, z - 2.4)], F_TILE, toward=(0, 1, 1))
    flat(g, [(x0 - .3, ye - .08, z + 1.0), (x1 + .3, ye - .08, z + 1.0), (x1 + .3, ye, z), (x0 - .3, ye, z)], F_TIMBER_X, toward=(0, -1, 0))
    sk.box(g, x0 - .3, x1 + .3, ye - .18, ye - .04, z + .95, z + 1.05, F_TIMBER_X, T, bevel=.008, cell=0)
    lp = (1.8, 2.45, z + .35)
    sk.cylinder(g, lp, (lp[0], lp[1] - .1, lp[2]), .005, F_CORD, 3, caps=False)
    sk.lathe(g, [(.0, -.44), (.1, -.4), (.13, -.28), (.1, -.14), (0.0, -.1)], lp, F_LANTERNS[0], (8, 6, 5)[T.i])


def house(g, T, side, u0, u1, z0, z1, h, r):
    """A dark house behind the fronts: a wall face toward the lane, a hipped tile roof, a lit window sometimes."""
    s = side
    g.face([(s * u0, 5.0, z0), (s * u0, 5.0, z1), (s * u0, h, z1), (s * u0, h, z0)], F_FAR, normal=(-s, 0, 0))
    if r.uniform() < .45:
        wz = (z0 + z1) / 2 + float(r.uniform(-1.0, 1.0))
        wy = h - float(r.uniform(1.2, 2.0))
        g.face([(s * (u0 - .01), wy, wz - .35), (s * (u0 - .01), wy, wz + .35), (s * (u0 - .01), wy + .7, wz + .35), (s * (u0 - .01), wy + .7, wz - .35)],
               F_FAR_LIT, normal=(-s, 0, 0))
    um = (u0 + u1) / 2
    top = h + float(r.uniform(1.4, 2.4))
    eo = .6
    a, b = (s * (u0 - eo), h - .2, z0 - eo), (s * (u0 - eo), h - .2, z1 + eo)
    c, d = (s * um, top, z1 - .8), (s * um, top, z0 + .8)
    flat(g, [a, b, c, d], F_TILE, toward=(-s, 1, 0))
    e, f_ = (s * (u1 + eo), h - .2, z0 - eo), (s * (u1 + eo), h - .2, z1 + eo)
    flat(g, [a, d, e], F_TILE, toward=(0, 1, -1))
    flat(g, [b, f_, c], F_TILE, toward=(0, 1, 1))


def block_roof(g, T, x, z, w, d, h, r):
    """A dark house far off: its walls, a gabled roof with its eaves lifted at the corners."""
    sk.box(g, x - w / 2, x + w / 2, 0.0, h, z - d / 2, z + d / 2, F_FAR, T, bevel=0, cell=0, skip=('-y',))
    if r.uniform() < .5:
        wy = h * float(r.uniform(.4, .7))
        g.face([(x - .3, wy, z + d / 2 + .01), (x + .3, wy, z + d / 2 + .01), (x + .3, wy + .6, z + d / 2 + .01), (x - .3, wy + .6, z + d / 2 + .01)],
               F_FAR_LIT, normal=(0, 0, 1))
        g.face([(x - .3, wy, z - d / 2 - .01), (x + .3, wy, z - d / 2 - .01), (x + .3, wy + .6, z - d / 2 - .01), (x - .3, wy + .6, z - d / 2 - .01)],
               F_FAR_LIT, normal=(0, 0, -1))
    rise = float(r.uniform(1.5, 2.6))
    for sz in (-1, 1):
        a, b = (x - w / 2 - .6, h + .25, z + sz * (d / 2 + .7)), (x + w / 2 + .6, h + .25, z + sz * (d / 2 + .7))
        c, dd = (x + w / 2 + .3, h + rise, z), (x - w / 2 - .3, h + rise, z)
        flat(g, [a, b, c, dd], F_TILE, toward=(0, 1, sz))
    for sx in (-1, 1):
        flat(g, [(x + sx * (w / 2), h, z - d / 2), (x + sx * (w / 2), h, z + d / 2), (x + sx * (w / 2 + .2), h + rise, z)], F_FAR, toward=(sx, 0, 0))


# ---------------------------------------------------------------------------------------------------------------------------
# The lanterns: their cords (static) and the lanterns themselves (rigs)
# ---------------------------------------------------------------------------------------------------------------------------
def cord(g, T, L):
    """A lantern's cord across the lane: from the left front down to the lantern's ring and up to the right front, sagging."""
    z, xk, yk, hl, hr = L[:5]
    n = (5, 4, 3)[T.i]
    for x0, y0 in ((-LANE - .1, hl), (LANE + .1, hr)):
        pts = []
        for i in range(n + 1):
            t = i / n
            x = x0 + (xk - x0) * t
            y = y0 + (yk - y0) * t - .12 * math.sin(math.pi * t)
            pts.append((x, y, z))
        sk.tube(g, pts, .008, F_CORD, 3, caps=False)
        if T.detail:
            # a hook in the wall
            sk.cylinder(g, (x0 * 1.02, y0, z), (x0 * .975, y0 - .02, z), .012, F_IRON, 3, caps=False)


def bunting(g, T, z, hl, hr, sag):
    """A wire across the lane hung with little paper pennants, cream, red and teal by turns."""
    n = 6
    pts = []
    for i in range(n + 1):
        t = i / n
        pts.append((-LANE - .1 + (2 * LANE + .2) * t, hl + (hr - hl) * t - sag * math.sin(math.pi * t), z))
    sk.tube(g, pts, .005, F_CORD, 3, caps=False)
    fins = (F_PAPER, F_NOREN_RED, F_SHUTTER_TEAL)
    k = 0
    x = -LANE + .25
    while x < LANE - .2:
        t = (x + LANE + .1) / (2 * LANE + .2)
        y = hl + (hr - hl) * t - sag * math.sin(math.pi * t)
        w = .09
        a, b, c = (x - w, y, z), (x + w, y, z), (x + .01 * math.sin(k), y - .2, z + .02 * math.cos(k * 1.7))
        nn = sk._norm(sk._cross(sk._sub(b, a), sk._sub(c, a)))
        g.face((a, b, c), fins[k % 3], normal=nn)
        g.face((c, b, a), fins[k % 3], normal=(-nn[0], -nn[1], -nn[2]))
        x += .38
        k += 1


def drape(g, T, s, z, f):
    """A cloth hung out to air over a balcony's rail."""
    a = (s * 1.68, 4.04, z - .55)
    b = (s * 1.68, 4.04, z + .55)
    mk.cloth(g, a, b, .75, f, T, seed=int(abs(z) * 3), sway=.03, tear=.04, cells=((3, 2), (2, 1), (1, 1))[T.i])
    # over the rail's top and down its inner side a little
    g.face([(s * 1.68, 4.05, z - .55), (s * 1.68, 4.05, z + .55), (s * 1.84, 4.07, z + .55), (s * 1.84, 4.07, z - .55)], f, normal=(0, 1, 0))


def lantern(T, shape, f, k):
    """A paper lantern in its own frame: the origin at its ring on the cord, hanging down -y. A wire bail, a lacquered top cap,
    the lit paper body (round, tall or gourd) with dark bamboo ribs, a bottom cap, a red tassel."""
    g = mk.Geo()
    sides = (10, 7, 5)[T.i]
    # the bail: a short wire to the cap
    sk.cylinder(g, (0.0, 0.0, 0.0), (0.0, -.09, 0.0), .006, F_CORD, 3, caps=False)
    if T.detail:
        sk.lathe(g, [(.02, -.004), (.022, -.016), (0.0, -.022)], (0, 0, 0), F_CAP, 5)
    if shape == 0:      # round
        body = [(.11, -.15), (.17, -.19), (.205, -.27), (.215, -.35), (.205, -.43), (.17, -.51), (.11, -.55)]
    elif shape == 1:    # tall
        body = [(.1, -.15), (.15, -.18), (.165, -.27), (.17, -.45), (.165, -.6), (.15, -.67), (.1, -.7)]
    else:               # gourd
        body = [(.09, -.15), (.13, -.18), (.14, -.24), (.12, -.29), (.17, -.35), (.2, -.43), (.18, -.52), (.1, -.57)]
    if T.i == 2:
        body = body[::2] + ([body[-1]] if len(body) % 2 == 0 else [])
    y_top, y_bot = body[0][1], body[-1][1]
    sk.lathe(g, body, (0, 0, 0), f, sides)
    # the caps
    sk.lathe(g, [(.03, -.085), (body[0][0] + .015, y_top + .003), (body[0][0] - .01, y_top - .014)], (0, 0, 0), F_CAP, sides)
    sk.lathe(g, [(body[-1][0] - .01, y_bot + .014), (body[-1][0] + .015, y_bot - .004), (0.0, y_bot - .035)], (0, 0, 0), F_CAP, sides)
    # the ribs: dark bands round the paper
    if T.detail:
        for j in ((2, len(body) - 3) if T.fine else (len(body) // 2,)):
            rr, yy = body[j]
            sk.lathe(g, [(rr + .005, yy - .006), (rr + .005, yy + .006)], (0, 0, 0), F_CAP, sides)
    # the tassel
    yt = y_bot - .035
    sk.cylinder(g, (0.0, yt, 0.0), (0.0, yt - .06, 0.0), .005, F_TASSEL, 3, caps=False)
    sk.lathe(g, [(.0, yt - .055), (.022, yt - .07), (.03, yt - .24), (0.0, yt - .25)], (0, 0, 0), F_TASSEL, (6, 4, 3)[T.i])
    return g
