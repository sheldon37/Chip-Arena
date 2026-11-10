"""Top Dog Emporium's rig props and breakable blocks (docs/arenas/EMPORIUM.md "Stage art"), each in its own frame; Core
NativeEmporiumArt poses the rig props from the replicated state, Runtime NativeStageArtDraw draws the blocks' models where their
blocks stand.

Rig frames (Unity axes, metres):
  wiener            the frankfurter: its centre at the origin, its length along z (the lane's width), radius .9; it rolls about z
  wiener-face       the same centre, not rolling: the googly eyes, brows and grin on +x (the way it goes), the mustard on top
  wiener-pupil      a pupil at the origin, looking along +x
  flap              an oven's pet-door flap: the hinge along z through the origin, the door hanging 2.1 m down, 3.3 m wide
  bun               a press's sesame bun: its cut face's centre at the origin, .94 tall, 3 m square (scaled by the squash)
  ram               the chrome ram: a unit length up y from the origin
  bell              the press's bell: its middle at the origin (it wobbles about x)
  popcorn           a griddle's little sausage: its centre at the origin, along z
  geyser-cap-k/-m   the sauce cap over a geyser's nozzle: a dome one unit tall (scaled to its bulge), ketchup / mustard
  pump-rod          the pump's rod: a unit length up y
  pump-head-k/-m    the pump's plunger head: its foot at the origin, the spout toward +x
  sauce-k/-m        the sauce column: a unit length up y from the grate (scaled to the burst's height)
  sauce-head-k/-m   the column's splashing head: its foot at the origin
  sign              the big turning sign: its centre at the origin, the frankfurter along x, the marquee in the x-y plane
  sign-bulbs-0..2   the marquee's bulbs by their chase group, in the sign's frame
  bun-hanger        a conveyor hanger: the rail point at the origin, the tray of buns hanging below
  grill-sausage-c/-p  a roller grill's sausage (a counter's, a prep table's): its centre at the origin, along z
  bigdog-arm        the Big Dog's waving forearm and glove: the elbow at the origin, up y
  bigdog-pupils, bigdog-lids   in the mascot's frame (the plinth's top centre): the eyes open, or shut
  juke-tube         a jukebox's bubble tube: a unit length up y
  bubble            a bubble at the origin
  griddle-<hazard>-<zone>   a griddle zone's grate and glowing bed, in the world's frame (the pose tints the bed by its heat)
Blocks (their own frames, the centre at the origin): the bun crates (one mesh for all) and the pickle barrels (one mesh).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
import emporium_kit as ek  # noqa: E402
from stagekit import Geo, box, lathe  # noqa: E402
from emporium_kit import ext, kmat, mat, capsule, ellipsoid, loft, part_round, rrect  # noqa: E402

BL = {}
RIG = {}
Y = (0.0, 1.0, 0.0)
GLYPH = {'T': '0,6 4,6|2,6 2,0', 'O': '0,0 0,6 4,6 4,0 0,0', 'P': '0,0 0,6 4,6 4,3 0,3', 'D': '0,0 0,6 2,6 4,4 4,2 2,0 0,0', 'G': '4,5 4,6 0,6 0,0 4,0 4,3 2,3'}


def sausage_fin():
    return mat('frank-skin', 0xA8442A, smooth=.66, dirt=.12)


def rot_y(g, a, t=(0, 0, 0)):
    return g.transformed(sk.affine(sk.quat_axis(0, 1, 0, a), t))


def moved(g, t):
    return g.transformed(sk.affine((0, 0, 0, 1), t))


# ---------------------------------------------------------------------------------------------------------------------------
# The Wiener Run
# ---------------------------------------------------------------------------------------------------------------------------
def wiener(T, r=.9, L=1.5):
    g = Geo()
    skin, dark = sausage_fin(), kmat('sausage-dark')
    half = L + r * .2
    sides = (20, 12, 8)[T.i]
    prof = [(.0, -half - .12), (.1, -half - .11), (.14, -half - .04), (.1, -half + .0)]
    lathe(g, prof, (0, 0, 0), dark, max(6, sides // 2), axis=(0, 0, 1))
    body = [(.1, -half), (r * .45, -half + .03), (r * .78, -half + .2), (r * .95, -half + .46), (r, -half + .8), (r, half - .8), (r * .95, half - .46), (r * .78, half - .2), (r * .45, half - .03), (.1, half)]
    lathe(g, body, (0, 0, 0), skin, sides, axis=(0, 0, 1))
    lathe(g, [(.1, half), (.14, half + .04), (.1, half + .11), (.0, half + .12)], (0, 0, 0), dark, max(6, sides // 2), axis=(0, 0, 1))
    # grill marks: three broad and three fine, charred along it
    if T.i < 2:
        for k in range(6):
            a = k * math.pi / 3
            w = .1 if k % 2 == 0 else .045
            zs = L - r * .4 if k % 2 == 0 else L - r * .7
            rings = []
            for da in (-w, 0.0, w):
                rr = r + .006
                rings.append([(rr * math.cos(a + da / r + math.pi / 2), rr * math.sin(a + da / r + math.pi / 2), -zs + 2 * zs * i / 4) for i in range(5)])
            ek.skin(g, rings, dark, closed=False, flip=False)
    return g


def wiener_face(T, r=.9, L=1.5):
    g = Geo()
    white, dark = mat('eye-white', 0xFFFFFF, smooth=.85, dirt=0), mat('face-dark', 0x1E1A22, smooth=.5, dirt=0)
    n = (12, 8, 6)[T.i]
    for e in (-1, 1):
        # the eye: a dome looking forward (its axis along x)
        rings = []
        st = (4, 3, 2)[T.i]
        for i in range(st + 1):
            a = math.pi / 2 * i / st
            rr, h = .28 * math.cos(a), .13 * math.sin(a)
            rings.append([(r * .78 - .02 + h, r * .55 + rr * math.cos(2 * math.pi * k / n), e * .42 + rr * math.sin(2 * math.pi * k / n)) for k in range(n)])
        ek.skin(g, rings, white)
        sk.tube(g, rings[0], .022, dark, 4, closed=True) if T.i == 0 else None
        # a brow, set for trouble
        capsule(g, (r * .78 + .1, r * .55 + .4, e * .2), (r * .78 + .06, r * .55 + .3 + .16, e * .66), .05, dark, (6, 5, 4)[T.i], caps=1)
    # the grin, low on the leading side
    pts = []
    for i in range(9):
        z = -.55 + 1.1 * i / 8
        y = -.02 - .2 * (1 - (z / .55) ** 2)
        pts.append((math.sqrt(max(.01, r * r - y * y)) + .03, y, z))
    sk.tube(g, pts, .045, dark, (5, 4, 3)[T.i])
    # the mustard: one squiggle along the top (it stays up while the wiener turns under it)
    m = kmat('mustard')
    zig = [(.22 if k % 2 else -.22, r + .035, -L * .8 + k * L * 1.6 / 7) for k in range(8)]
    sk.sweep(g, [(-.09, -.03), (-.06, .03), (0, .05), (.06, .03), (.09, -.03)], sk.catmull(zig, (4, 3, 2)[T.i]), m, up=(0, 1, 0), caps=True)
    return g


def pupil(T):
    g = Geo()
    ellipsoid(g, (0, 0, 0), .1, .1, .1, mat('face-dark', 0x1E1A22, smooth=.5, dirt=0), (8, 6, 5)[T.i], 3)
    gg = Geo()
    gg.extend(g.transformed([.4, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0]))
    if T.i == 0:
        ellipsoid(gg, (.035, .04, -.035), .012, .028, .028, mat('eye-white', 0xFFFFFF, smooth=.85, dirt=0), 5, 2)
    return gg


def flap(T, half=1.65, tall=2.1):
    g = Geo()
    red, cream, yellow, dark, chrome = kmat('red'), kmat('cream'), kmat('yellow'), kmat('dark'), kmat('chrome')
    box(g, -.03, .03, -tall, -.04, -half, half, red, T, .02, cell=0)
    sk.cylinder(g, (0, 0, -half), (0, 0, half), .045, chrome, T.small)
    for s in (-1, 1):
        n = (s, 0, 0)
        x = s * .034
        g.face(((x, -1.95, -half + .1), (x, -1.95, half - .1), (x, -.15, half - .1), (x, -.15, -half + .1)), cream, n)
        x2 = s * .04
        g.face(((x2, -.88, -half * .55), (x2, -.88, half * .55), (x2, -.52, half * .55), (x2, -.52, -half * .55)), yellow, n)
        g.face(((x2, -1.83, -half + .1), (x2, -1.83, half - .1), (x2, -1.67, half - .1), (x2, -1.67, -half + .1)), dark, n)
        if T.i < 2:
            # a little sausage on the plate, and the frame's beading
            capsule(g, (x2 + s * .01, -.7, -.5), (x2 + s * .01, -.7, .5), .07, sausage_fin(), 6, caps=1, squash=(1, .4), up=(1, 0, 0))
            sk.tube(g, [(x2, -1.95, -half + .1), (x2, -1.95, half - .1), (x2, -.15, half - .1), (x2, -.15, -half + .1)], .02, chrome, 4, closed=True)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The bun presses
# ---------------------------------------------------------------------------------------------------------------------------
BUN = [(0.0, 2.84, .5), (.04, 2.98, .58), (.1, 3.0, .62), (.3, 2.97, .78), (.48, 2.86, .95), (.64, 2.6, 1.05), (.77, 2.16, 1.0), (.87, 1.56, .76), (.92, .96, .47), (.94, .36, .17)]


def bun(T):
    g = Geo()
    n = (5, 3, 2)[T.i]
    cut, side, crust = mat('bun-cut', 0xF6DDB0, smooth=.2, dirt=.15), mat('bun-side', 0xEDAE60, smooth=.3, dirt=.2), mat('bun-crust', 0xC2702A, smooth=.42, dirt=.2)
    loft(g, (0, 0), [(y, w, w, r) for y, w, r in BUN[:3]], cut, n, cap0=True, cap1=False)
    loft(g, (0, 0), [(y, w, w, r) for y, w, r in BUN[2:5]], side, n, cap1=False)
    loft(g, (0, 0), [(y, w, w, r) for y, w, r in BUN[4:]], crust, n, cap1=True)
    if T.i < 2:
        seed = mat('sesame', 0xFFF4DA, smooth=.35, dirt=.05)
        rs = np.random.RandomState(7)
        for s in range(34):
            a, rr = rs.uniform(0, 2 * math.pi), math.sqrt(rs.uniform(0, 1)) * 1.32
            x, z = math.cos(a) * rr, math.sin(a) * rr
            reach = max(abs(x), abs(z))
            y = .94
            for (y0, w0, _), (y1, w1, _) in zip(BUN[4:-1], BUN[5:]):
                if w1 / 2 <= reach <= w0 / 2:
                    y = y0 + (y1 - y0) * (w0 / 2 - reach) / max(1e-6, (w0 - w1) / 2)
            if reach > BUN[4][1] / 2:
                continue
            sg = Geo()
            ellipsoid(sg, (0, 0, 0), .1, .028, .05, seed, 5, 2)
            g.extend(sg.transformed(sk.affine(sk.quat_axis(0, 1, 0, a * 3), (x, y + .012, z))))
    return g


def ram(T):
    g = Geo()
    sk.cylinder(g, (0, 0, 0), (0, 1, 0), .2, mat('ram', 0xE6ECF2, smooth=.92, metal=1.0, dirt=.05), T.round, caps=False)
    return g


def bell(T):
    g = Geo()
    brass = mat('bell-brass', 0xF6B821, sk.DETAIL_BRASS, smooth=.8, metal=1.0, dirt=.15)
    lathe(g, [(.0, .3), (.09, .295), (.19, .23), (.235, .08), (.26, -.12), (.31, -.25), (.37, -.3), (.36, -.33), (.3, -.3)], (0, 0, 0), brass, T.round)
    lathe(g, [(.3, -.3), (.24, -.12), (.0, .1)], (0, 0, 0), mat('bell-inside', 0x6A4A12, smooth=.3, metal=.6, dirt=.3), T.round)
    ellipsoid(g, (0, -.27, 0), .07, .07, .07, kmat('dark'), 6, 3)
    sk.tube(g, [(.06 * math.cos(a), .32 + .06 * math.sin(a), 0) for a in [math.pi * i / 5 for i in range(6)]], .018, brass, 4)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The griddles
# ---------------------------------------------------------------------------------------------------------------------------
def griddle_zone(T, z):
    g = Geo()
    c, s = z['center'], z['size']
    x0, x1, z0, z1, y = c[0] - s[0] / 2 + .08, c[0] + s[0] / 2 - .08, c[2] - s[2] / 2 + .08, c[2] + s[2] / 2 - .08, c[1] - s[1] / 2
    bed = mat('griddle-bed', 0xFFFFFF, glow=1.0)
    iron = mat('grate-iron', 0x2B2A30, smooth=.55, metal=.8, dirt=.15)
    g.face(((x0, y + .008, z0), (x1, y + .008, z0), (x1, y + .008, z1), (x0, y + .008, z1)), bed, Y)
    n = (16, 10, 6)[T.i]
    k = (3, 2, 1)[T.i]
    for i in range(n + 1):
        x = x0 + (x1 - x0) * i / n
        part_round(g, (x, y + .008, z0), (x, y + .008, z1), .03 if T.i < 2 else .05, iron, (1, 0, 0), Y, 0, 180, k, .03)
    for zz in (z0, (z0 + z1) / 2, z1):
        part_round(g, (x0, y + .008, zz), (x1, y + .008, zz), .04, iron, (0, 0, 1), Y, 0, 180, k, .034)
    return g


def popcorn(T, half=.24, r=.075):
    g = Geo()
    capsule(g, (0, 0, -half + r), (0, 0, half - r), r, sausage_fin(), (8, 6, 4)[T.i], caps=2 if T.i == 0 else 1)
    return g


def grill_sausage(T, half):
    g = Geo()
    r = .055
    capsule(g, (0, 0, -half + r), (0, 0, half - r), r, sausage_fin(), (8, 6, 4)[T.i], caps=2 if T.i == 0 else 1)
    if T.i == 0:
        dark = kmat('sausage-dark')
        for a in (0.0, math.pi):
            rings = [[((r + .003) * math.cos(a + da + math.pi / 2), (r + .003) * math.sin(a + da + math.pi / 2), zz) for zz in (-half + .08, half - .08)] for da in (-.3, 0, .3)]
            ek.skin(g, rings, dark, closed=False)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The geysers
# ---------------------------------------------------------------------------------------------------------------------------
def sauce_fin(k):
    col = 0xE3241C if k else 0xFFC81E
    return mat('sauce-%06x' % col, col, smooth=.86, dirt=.05)


def geyser_cap(T, k):
    g = Geo()
    ellipsoid(g, (0, 0, 0), .46, 1.0, .46, sauce_fin(k), (14, 10, 6)[T.i], (4, 3, 2)[T.i], 0, 1)
    return g


def pump_rod(T):
    g = Geo()
    sk.cylinder(g, (0, 0, 0), (0, 1, 0), .1, mat('pump-white', 0xFFFFFF, smooth=.7, dirt=.1), T.round, caps=False)
    return g


def pump_head(T, k):
    g = Geo()
    white = mat('pump-white', 0xFFFFFF, smooth=.7, dirt=.1)
    lathe(g, [(.0, 0), (.36, 0), (.43, .04), (.44, .18), (.4, .22)], (0, 0, 0), sauce_fin(k), T.round + 2)
    lathe(g, [(.4, .22), (.47, .23), (.47, .28), (.34, .32), (.0, .335)], (0, 0, 0), white, T.round + 2)
    capsule(g, (.3, .13, 0), (.74, -.03, 0), .085, white, (8, 6, 4)[T.i], caps=1, r1=.065)
    lathe(g, [(.05, 0), (.05, .1), (.0, .1)], (.76, -.14, 0), white, 6)
    return g


def sauce_column(T, k):
    g = Geo()
    n, m = (14, 10, 6)[T.i], (10, 6, 3)[T.i]
    rings = []
    for i in range(m + 1):
        h = i / m
        rings.append([((.58 + .07 * h + .06 * math.sin(3 * a + h * 9) + .05 * math.sin(h * 14)) * math.cos(a), h, (.58 + .07 * h + .06 * math.sin(3 * a + h * 9) + .05 * math.sin(h * 14)) * math.sin(a)) for a in [2 * math.pi * j / n for j in range(n)]])
    ek.skin(g, rings, sauce_fin(k))
    return g


def sauce_head(T, k):
    g = Geo()
    f = sauce_fin(k)
    lathe(g, [(.5, -.16), (.8, -.1), (.98, .02), (1.0, .14), (.86, .3), (.6, .42), (.3, .5), (.0, .54)], (0, 0, 0), f, (14, 10, 6)[T.i])
    if T.i < 2:
        for i in range(7):
            a = i * 2 * math.pi / 7 + .4
            ellipsoid(g, (1.22 * math.cos(a), .1 + .18 * math.sin(i * 2.3), 1.22 * math.sin(a)), .11, .14, .11, f, 6, 3)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The big sign
# ---------------------------------------------------------------------------------------------------------------------------
def sign(T, bulbs=None):
    """bulbs None: the sign without its bulbs; 0..2: that chase group's bulbs only."""
    g = Geo()
    L = RIG['signLength'] / 2
    rx, ry = L + 1.4, 2.9
    if bulbs is not None:
        f = mat('marquee-bulb-hot', 0xFFF2C0, glow=3.0)
        for k in range(36):
            if k % 3 != bulbs:
                continue
            a = k * 2 * math.pi / 36
            ellipsoid(g, (math.cos(a) * rx, math.sin(a) * ry, 0), .17, .17, .17, f, (8, 6, 5)[T.i], (4, 3, 2)[T.i])
        return g
    n = (16, 10, 6)[T.i]
    capsule(g, (-(L - .75), 0, 0), (L - .75, 0, 0), .9, sausage_fin(), n, caps=(4, 3, 2)[T.i], body=3, bulge=.03)
    for e in (-1, 1):
        capsule(g, (-(L - 1.6), -.5, e * .8), (L - 1.6, -.5, e * .8), .78, kmat('bun'), n, caps=(3, 2, 1)[T.i], squash=(.8, 1.0), up=(0, 1, 0), body=2, bulge=.03)
        capsule(g, (-(L - 2.0), -.92, e * .7), (L - 2.0, -.92, e * .7), .5, kmat('bun-top'), max(6, n - 4), caps=(3, 2, 1)[T.i], squash=(1.0, .8), up=(0, 1, 0))
        lathe(g, [(.0, 0), (.16, .04), (.12, .16), (.0, .2)], (e * (L + .14), 0, 0), kmat('sausage-dark'), 8, axis=(e, 0, 0))
    zig = [(-L + 1.1 + k * (2 * L - 2.2) / 14, .93, -.4 if k % 2 == 0 else .4) for k in range(15)]
    sk.tube(g, sk.catmull(zig, (4, 2, 1)[T.i]), .075, mat('neon-lemon', 0xFFE84A, glow=2.6), (6, 4, 3)[T.i])
    m = (72, 36, 18)[T.i]
    iron = mat('sign-iron', 0x2C2630, smooth=.5, metal=.6, dirt=.2)
    sk.tube(g, [(math.cos(2 * math.pi * i / m) * rx, math.sin(2 * math.pi * i / m) * ry, 0) for i in range(m)], .09, iron, (6, 4, 3)[T.i], closed=True)
    sk.tube(g, [(math.cos(2 * math.pi * i / m) * (rx + .22), math.sin(2 * math.pi * i / m) * (ry + .22), 0) for i in range(m)], .055, mat('neon-pink', 0xFF3FA4, glow=2.8), (6, 4, 3)[T.i], closed=True)
    for a in (.5, math.pi - .5, math.pi + .5, -.5):
        sk.cylinder(g, (math.cos(a) * (L - .4), math.sin(a) * .6, 0), (math.cos(a) * rx, math.sin(a) * ry, 0), .05, iron, T.small, caps=False)
    for e in (-1, 1):
        sk.cylinder(g, (e * .001, .9, 0), (e * .001, ry, 0), .05, iron, T.small, caps=False) if e > 0 else None
        sk.cylinder(g, (e * (L - .6) * .82, ry * .7, 0), (e * (L - .6) * .5, RIG['ridge'] - .6 - RIG['sign'][1], 0), .03, iron, T.small, caps=False)
    # the board over it: TOP DOG in glowing tubes on both faces
    by = ry + .7
    loft(g, (0, 0), [(by - .55, 5.2, .2, .09), (by + .55, 5.2, .2, .09)], kmat('red'), (3, 2, 1)[T.i], cap0=True, cap1=True)
    sk.tube(g, [(x, by + y, .0) for x, y in rrect(5.34, 1.24, .2, 3)], .05, kmat('chrome'), T.small, closed=True) if T.i < 2 else None
    sk.cylinder(g, (0, by + .55, 0), (0, by + 1.1, 0), .16, kmat('chrome'), T.round)
    text, h = 'TOP DOG', .62
    adv, s = h * (4.0 / 6 + .28), h / 6
    width = len(text) * adv - h * .28
    tubef = mat('neon-cream', 0xFFF2C0, glow=2.6)
    for face in (1, -1):
        u = face * width / 2
        for ch in text:
            for stroke in GLYPH.get(ch, '').split('|'):
                if not stroke:
                    continue
                pts = [tuple(float(v) for v in q.split(',')) for q in stroke.split(' ')]
                path = [(u - face * px * s, by - h / 2 + py * s, face * .135) for px, py in pts]
                for a, b in zip(path[:-1], path[1:]):
                    capsule(g, a, b, .032, tubef, (6, 4, 3)[T.i], caps=1 if T.i == 0 else 0)
            u -= face * adv
    return g


def bun_hanger(T):
    g = Geo()
    chrome = kmat('chrome')
    sk.cylinder(g, (0, -.05, 0), (0, -.72, 0), .03, chrome, T.small, caps=False)
    lathe(g, [(.07, -.06), (.07, .04), (.03, .07)], (0, 0, 0), chrome, T.small + 2, cap0=True)
    box(g, -.5, .5, -.8, -.74, -.32, .32, chrome, T, .02, cell=0)
    if T.i < 2:
        sk.tube(g, [(x, -.7, z) for x, z in rrect(1.0, .64, .08, 2)], .015, chrome, 3, closed=True)
    for e in (-1, 0, 1):
        capsule(g, (e * .3, -.62, -.14), (e * .3, -.62, .14), .13, kmat('bun'), (8, 6, 4)[T.i], caps=2 if T.i == 0 else 1, squash=(1.0, .82), up=(1, 0, 0))
        capsule(g, (e * .3, -.55, -.13), (e * .3, -.55, .13), .1, kmat('bun-top'), (8, 6, 4)[T.i], caps=1, squash=(1.05, .6), up=(1, 0, 0))
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The Big Dog (it faces -z)
# ---------------------------------------------------------------------------------------------------------------------------
def bigdog(T):
    """The mascot's still body, in the mascot's frame (the plinth's top centre)."""
    g = Geo()
    n = (16, 10, 6)[T.i]
    white, dark = mat('glove', 0xFFFFFF, smooth=.6, dirt=.1), mat('face-dark', 0x1E1A22, smooth=.5, dirt=0)
    capsule(g, (0, .72, 0), (0, 2.85, 0), .6, sausage_fin(), n, caps=(4, 3, 2)[T.i], body=3, bulge=.04)
    for e in (-1, 1):
        capsule(g, (e * .56, .5, .06), (e * .56, 2.1, .06), .46, kmat('bun'), max(6, n - 4), caps=(3, 2, 1)[T.i], squash=(1.25, .8), up=(0, 0, 1), body=2, bulge=.05)
        # shoes
        ellipsoid(g, (e * .3, .09, -.22), .2, .13, .36, mat('sneaker', 0xD8322A, smooth=.6, dirt=.2), (10, 7, 5)[T.i], 3)
        ellipsoid(g, (e * .3, .04, -.22), .21, .05, .37, white, (10, 7, 5)[T.i], 2)
        # the eyes
        rings = []
        st, k = (4, 3, 2)[T.i], (12, 8, 6)[T.i]
        for i in range(st + 1):
            a = math.pi / 2 * i / st
            rr, h = .18 * math.cos(a), .09 * math.sin(a)
            rings.append([(e * .24 + rr * math.cos(2 * math.pi * j / k), 2.6 + rr * math.sin(2 * math.pi * j / k), -.555 - h) for j in range(k)])
        ek.skin(g, rings, mat('eye-white', 0xFFFFFF, smooth=.85, dirt=0))
    # the mustard, in neon, down its front; the smile
    zig = [(-.3 if k % 2 == 0 else .3, .5 + k * .5, -.61 - .02 * math.sin(k)) for k in range(5)]
    sk.tube(g, sk.catmull(zig, (4, 2, 1)[T.i]), .05, mat('neon-lemon-soft', 0xFFE84A, glow=2.2), (6, 4, 3)[T.i])
    smile = [(x, 2.18 - .1 * (1 - (x / .3) ** 2) + .1, -math.sqrt(max(.01, .6 * .6 - x * x)) - .03) for x in [-.3 + .6 * i / 6 for i in range(7)]]
    sk.tube(g, smile, .035, dark, (5, 4, 3)[T.i])
    # the chef's hat
    lathe(g, [(.5, 3.38), (.6, 3.42), (.6, 3.6), (.52, 3.62)], (0, 0, 0), white, n)
    lathe(g, [(.52, 3.62), (.62, 3.8), (.7, 4.02), (.66, 4.24), (.46, 4.38), (.0, 4.42)], (0, 0, 0), white, n)
    if T.i == 0:
        for k in range(7):
            a = k * 2 * math.pi / 7
            ellipsoid(g, (.5 * math.cos(a), 4.12, .5 * math.sin(a)), .26, .28, .26, white, 8, 4)
    # the arms: the left on its hip, the right raised to the elbow
    capsule(g, (-.6, 1.9, 0), (-1.15, 1.2, -.2), .075, white, (7, 5, 4)[T.i], caps=1)
    ellipsoid(g, (-1.02, 1.12, -.26), .15, .13, .12, white, (8, 6, 5)[T.i], 3)
    capsule(g, (.6, 1.9, 0), (1.15, 2.25, -.1), .075, white, (7, 5, 4)[T.i], caps=1)
    return g


def bigdog_arm(T):
    g = Geo()
    white = mat('glove', 0xFFFFFF, smooth=.6, dirt=.1)
    capsule(g, (0, 0, 0), (0, .5, 0), .07, white, (7, 5, 4)[T.i], caps=1)
    lathe(g, [(.085, .44), (.11, .46), (.11, .52), (.085, .54)], (0, 0, 0), mat('cuff', 0xD8322A, smooth=.6, dirt=.1), max(6, T.small + 2))
    ellipsoid(g, (0, .66, 0), .17, .19, .09, white, (10, 7, 5)[T.i], (4, 3, 2)[T.i])
    capsule(g, (-.12, .58, 0), (-.24, .68, 0), .05, white, (6, 5, 4)[T.i], caps=1)
    if T.i < 2:
        for x in (-.09, 0.0, .09):
            capsule(g, (x, .78, 0), (x * 1.3, .88, 0), .048, white, (6, 5, 4)[T.i], caps=1)
    return g


def bigdog_eyes(T, shut):
    g = Geo()
    dark = mat('face-dark', 0x1E1A22, smooth=.5, dirt=0)
    for e in (-1, 1):
        if not shut:
            ellipsoid(g, (e * .24, 2.53, -.65), .075, .085, .03, dark, (8, 6, 5)[T.i], 3)
            if T.i == 0:
                ellipsoid(g, (e * .24 + .025, 2.56, -.675), .02, .02, .012, mat('eye-white', 0xFFFFFF, smooth=.85, dirt=0), 5, 2)
        else:
            ellipsoid(g, (e * .24, 2.6, -.56), .2, .2, .12, sausage_fin(), (10, 7, 5)[T.i], 3)
            sk.tube(g, [(e * .24 + .17 * math.cos(a), 2.6 - .05 * math.sin(a), -.675 + .03 * (1 - math.sin(a))) for a in [math.pi * i / 6 for i in range(7)]], .016, dark, 4)
    return g


def juke_tube(T):
    g = Geo()
    sk.cylinder(g, (0, 0, 0), (0, 1, 0), .055, mat('juke-glow', 0xFFFFFF, glow=2.2), (8, 6, 4)[T.i], caps=False)
    return g


def bubble(T):
    g = Geo()
    ellipsoid(g, (0, 0, 0), .035, .035, .035, mat('bubble', 0xFFFFFF, glow=1.5), (6, 5, 4)[T.i], (3, 2, 2)[T.i])
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The breakable blocks
# ---------------------------------------------------------------------------------------------------------------------------
def crate(T, size=1.2):
    g = Geo()
    h = size / 2
    wx, wy, wz = [mat('crate-%s' % a, 0xC89858, sk.DETAIL_WOOD, smooth=.3, dirt=.5, param=i, wear=.25) for i, a in enumerate('xyz')]
    inner = mat('crate-inside', 0x3A2A1C, smooth=.15, dirt=.5)
    box(g, -h + .05, h - .05, -h + .03, h - .05, -h + .05, h - .05, inner, T, 0, cell=0)
    t = .035
    # the corner posts and the slats on each side
    for sx in (-1, 1):
        for sz in (-1, 1):
            box(g, sx * h - (.1 if sx > 0 else 0), sx * h + (.1 if sx < 0 else 0), -h, h, sz * h - (.1 if sz > 0 else 0), sz * h + (.1 if sz < 0 else 0), wy, T, .012, cell=0)
    for k in range(4):
        y0 = -h + .04 + k * .29
        y1 = y0 + .22
        for sz in (-1, 1):
            box(g, -h + .09, h - .09, y0, y1, sz * h - (t if sz > 0 else 0) - .005 * sz, sz * h + (t if sz < 0 else 0) - .005 * sz, wx, T, .008, cell=0)
        for sx in (-1, 1):
            box(g, sx * h - (t if sx > 0 else 0) - .005 * sx, sx * h + (t if sx < 0 else 0) - .005 * sx, y0, y1, -h + .09, h - .09, wz, T, .008, cell=0)
    red = mat('crate-stencil', 0xD8322A, smooth=.3, dirt=.5)
    y0 = -h + .04 + .29 + .01
    for sz in (-1, 1):
        g.face(((-.3, y0, sz * (h + .001)), (.3, y0, sz * (h + .001)), (.3, y0 + .2, sz * (h + .001)), (-.3, y0 + .2, sz * (h + .001))), red, (0, 0, sz))
    for sx in (-1, 1):
        g.face(((sx * (h + .001), y0, -.3), (sx * (h + .001), y0, .3), (sx * (h + .001), y0 + .2, .3), (sx * (h + .001), y0 + .2, -.3)), red, (sx, 0, 0))
    # the lid's planks, three buns left on it
    for k in range(4):
        x0 = -h + .02 + k * (size - .04) / 4
        box(g, x0 + .008, x0 + (size - .04) / 4 - .008, h - .05, h, -h + .01, h - .01, wz, T, .008, cell=0)
    if T.i < 2:
        for k in range(3):
            x = -.3 + k * .3
            capsule(g, (x, h + .05, -.16), (x, h + .05, .16), .1, kmat('bun'), (8, 6, 4)[T.i], caps=1, squash=(1.0, .6), up=(1, 0, 0))
    return g


def barrel(T):
    g = Geo()
    n = (16, 10, 8)[T.i]
    green = mat('barrel', 0x4E9A34, sk.DETAIL_WOOD, smooth=.4, dirt=.4, param=1, wear=.2)
    hoop = mat('barrel-hoop', 0x24222A, smooth=.5, metal=.8, dirt=.3)
    prof = [(.5, -.5), (.56, -.33), (.595, -.12), (.6, 0.0), (.595, .12), (.56, .33), (.5, .5)]
    lathe(g, prof, (0, 0, 0), green, n)
    lathe(g, [(.5, .5), (.47, .5), (.46, .45), (.0, .45)], (0, 0, 0), mat('barrel-lid', 0x8A6A3C, sk.DETAIL_WOOD, smooth=.3, dirt=.5, param=0), n)
    g.face([(.5 * math.cos(2 * math.pi * i / n), -.5, .5 * math.sin(2 * math.pi * i / n)) for i in range(n)], hoop, (0, -1, 0))
    for y, r in ((-.3, .575), (.3, .575)):
        lathe(g, [(r - .012, y - .05), (r + .012, y - .045), (r + .016, y + .045), (r - .008, y + .05)], (0, 0, 0), hoop, n)
    # labels fore and aft, a pickle on the lid
    cream = mat('barrel-label', 0xF7EEDC, smooth=.4, dirt=.3)
    for a0 in (math.pi / 2, -math.pi / 2):
        rings = [[(.607 * math.cos(a0 + da), y, .607 * math.sin(a0 + da)) for da in [-.55 + 1.1 * i / 6 for i in range(7)]] for y in (-.16, 0.0, .16)]
        ek.skin(g, rings, cream, closed=False)
    pickle = mat('pickle', 0x4E9A34, smooth=.6, dirt=.15)
    capsule(g, (-.3 * math.cos(.6), .54, .3 * math.sin(.6)), (.3 * math.cos(.6), .54, -.3 * math.sin(.6)), .09, mat('pickle-skin', 0x5FA83A, smooth=.6, dirt=.15), (8, 6, 4)[T.i], caps=2 if T.i == 0 else 1, bulge=.15, body=2)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# What stands still beside the rigs (static scenery), the scenes for the bake and the preview, the bank
# ---------------------------------------------------------------------------------------------------------------------------
def fittings(g, L, T):
    base = RIG['mascotBase']
    g.extend(moved(bigdog(T), (0, base, 0)))
    sx, sy, sz = RIG['sign']
    chrome = kmat('chrome')
    # the sign's drive: a shaft from the ridge and its motor's drum
    sk.cylinder(g, (sx, sy + 2.9 + 1.7, sz), (sx, RIG['ridge'] - .3, sz), .12, chrome, T.round, caps=False)
    lathe(g, [(.0, 0), (.42, .02), (.46, .1), (.46, .5), (.3, .62), (.12, .66)], (sx, sy + 2.9 + 1.8, sz), mat('sign-motor', 0xD8322A, smooth=.6, dirt=.25), T.round)


def blocks_of(L):
    return [b for b in L['blocks'] if b['role'] != 'static']


def block_scene(L, T):
    g = Geo()
    for b in blocks_of(L):
        m = barrel(T) if '-barrel-' in b['id'] else crate(T, b['size'][0])
        g.extend(moved(m, tuple(b['pos'])))
    return g


def rig_items(L, T):
    """(slot name, Geo, frame 'world' or None, rest place for the bake's patterns)."""
    items = [('wiener', wiener(T), (0, .9, 10)), ('wiener-face', wiener_face(T), (0, .9, 10)), ('wiener-pupil', pupil(T), (0, 1.4, 10)), ('flap', flap(T), (-18, 2.2, 10)),
             ('bun', bun(T), (-6.5, 4.1, 0)), ('ram', ram(T), (-6.5, 5.1, 0)), ('bell', bell(T), (-6.5, 7.9, 1.2)), ('popcorn', popcorn(T), (-13.6, .1, 0)),
             ('geyser-cap-k', geyser_cap(T, True), (-10, 0, 17.4)), ('geyser-cap-m', geyser_cap(T, False), (10, 0, 17.4)), ('pump-rod', pump_rod(T), (-11.6, 3, 17.4)),
             ('pump-head-k', pump_head(T, True), (-11.6, 3.5, 17.4)), ('pump-head-m', pump_head(T, False), (11.6, 3.5, 17.4)),
             ('sauce-k', sauce_column(T, True), (-10, 0, 17.4)), ('sauce-m', sauce_column(T, False), (10, 0, 17.4)),
             ('sauce-head-k', sauce_head(T, True), (-10, 5, 17.4)), ('sauce-head-m', sauce_head(T, False), (10, 5, 17.4)),
             ('sign', sign(T), tuple(RIG['sign'])), ('sign-bulbs-0', sign(T, 0), tuple(RIG['sign'])), ('sign-bulbs-1', sign(T, 1), tuple(RIG['sign'])), ('sign-bulbs-2', sign(T, 2), tuple(RIG['sign'])),
             ('bun-hanger', bun_hanger(T), (12, RIG['conveyorY'], 5)), ('grill-sausage-c', grill_sausage(T, .31), (4.7, 1.26, 14.9)), ('grill-sausage-p', grill_sausage(T, .235), (-16.2, 1.26, 5)),
             ('bigdog-arm', bigdog_arm(T), (1.15, 8.25, -.1)), ('bigdog-pupils', bigdog_eyes(T, False), (0, 6, 0)), ('bigdog-lids', bigdog_eyes(T, True), (0, 6, 0)),
             ('juke-tube', juke_tube(T), (-12.5, .4, 13.2)), ('bubble', bubble(T), (-12.5, 1.4, 13.2))]
    out = [(n, g, None, r) for n, g, r in items]
    for h in L['hazards']:
        if h['kind'] == 'burner':
            for zi, z in enumerate(h['zones'][:2]):
                out.append(('griddle-%s-%d' % (h['id'], zi), griddle_zone(T, z), 'world', None))
    return out


def preview_rig(L, T):
    """The rig at a telling moment, for the Blender preview: a wiener rolling in the north lane, a bun hanging, a geyser
    bursting, the sign, the conveyors, the grills."""
    pv = []
    items = {n: g for n, g, _, _ in rig_items(L, T)}

    def put(name, m):
        g = items[name].transformed(m)
        pv.append((g, sk.Bake(g, None)))
    T3 = sk.affine
    I = (0, 0, 0, 1)
    put('wiener', T3(sk.quat_axis(0, 0, 1, .7), (-4, .95, 10)))
    put('wiener-face', T3(I, (-4, .95, 10)))
    for e in (-1, 1):
        put('wiener-pupil', T3(I, (-4 + .9 * .78 + .1, .95 + .9 * .55 - .12, 10 + e * .42)))
    put('flap', T3(sk.quat_axis(0, 0, 1, .25), (-18 + .03, 2.2, 10)))
    put('flap', T3(I, (18 - .03, 2.2, 10)))
    put('flap', T3(I, (-18 + .03, 2.2, -10)))
    put('flap', T3(I, (18 - .03, 2.2, -10)))
    for x, yb in ((-6.5, 4.1), (6.5, 1.6)):
        put('bun', T3(I, (x, yb, 0)))
        put('ram', T3(I, (x, yb + .94, 0), (1, 6.6 - yb - .94, 1)))
        put('bell', T3(I, (x, 7.9, 1.2)))
    for h in L['hazards']:
        if h['kind'] == 'burner':
            for zi, z in enumerate(h['zones'][:2]):
                put('griddle-%s-%d' % (h['id'], zi), T3(I))
                for j in range(3):
                    put('popcorn', T3(sk.quat_axis(0, 1, 0, j * 1.1 + zi), (z['center'][0] - .5 + j * .5, .08, z['center'][2] + .3 * (j - 1))))
        if h['kind'] == 'vent':
            a = h['a']
            k = 'k' if a[0] < 0 else 'm'
            put('geyser-cap-' + k, T3(I, (a[0], .02, a[2]), (1, .08, 1)))
            px = -11.6 if a[0] < 0 else 11.6
            put('pump-rod', T3(I, (px, 3, a[2]), (1, .6, 1)))
            put('pump-head-' + k, T3(I if a[0] >= px else sk.quat_axis(0, 1, 0, math.pi), (px, 3.6, a[2])))
            if a[0] < 0 and a[2] > 0:
                put('sauce-' + k, T3(I, (a[0], 0, a[2]), (1, 4.6, 1)))
                put('sauce-head-' + k, T3(I, (a[0], 4.6, a[2])))
    sgn = RIG['sign']
    q = sk.quat_axis(0, 1, 0, .5)
    for n in ('sign', 'sign-bulbs-0', 'sign-bulbs-1', 'sign-bulbs-2'):
        put(n, T3(q, tuple(sgn)))
    x0, x1, z = RIG['conveyorX0'], RIG['conveyorX1'], RIG['conveyorZ']
    for s in (-1, 1):
        for k in range(4):
            put('bun-hanger', T3(I, (s * (x0 + 1 + k * 2.2), RIG['conveyorY'], s * z)))
            put('bun-hanger', T3(I, (s * (x0 + 1 + k * 2.2), RIG['conveyorY'], -s * z)))
    for b in L['blocks']:
        i = b['id']
        if '-counter-' in i or '-prep-' in i:
            along_x = b['size'][0] >= b['size'][2]
            length = min(3.0, (b['size'][0] if along_x else b['size'][2]) - .6)
            n = int(math.floor(length / .26))
            for k in range(n):
                u = -length / 2 + (k + .5) * length / n
                pos = (b['pos'][0] + u, b['pos'][1] + b['size'][1] / 2 + .16, b['pos'][2]) if along_x else (b['pos'][0], b['pos'][1] + b['size'][1] / 2 + .16, b['pos'][2] + u)
                put('grill-sausage-c' if '-counter-' in i else 'grill-sausage-p', T3(I if along_x else sk.quat_axis(0, 1, 0, math.pi / 2), pos))
        if '-jukebox-' in i:
            f = -1 if b['pos'][2] > 0 else 1
            face = b['pos'][2] + f * b['size'][2] / 2
            for k in (-1, 1):
                px = b['pos'][0] + k * (b['size'][0] / 2 - .12)
                put('juke-tube', T3(I, (px, .4, face + f * .03), (1, 1.9, 1)))
                for j in range(4):
                    put('bubble', T3(I, (px, .6 + j * .42, face + f * .0375)))
    base = RIG['mascotBase']
    put('bigdog-arm', T3(sk.quat_axis(0, 0, 1, -.6), (1.15, base + 2.25, -.1)))
    put('bigdog-pupils', T3(I, (0, base, 0)))
    return pv


def bank_props(sb, L, T, floors, no_ao):
    ti = T.i
    items = rig_items(L, T)
    blocks = blocks_of(L)
    cr = next(b for b in blocks if '-crate-' in b['id'])
    br = next(b for b in blocks if '-barrel-' in b['id'])
    items.append(('crate', crate(T, cr['size'][0]), None, tuple(cr['pos'])))
    items.append(('barrel', barrel(T), None, tuple(br['pos'])))
    geos = [it[1] for it in items]
    aos = [None] * len(geos)
    if not no_ao:
        # each prop on its own, well apart, so they do not shade each other
        spread = [g.transformed(sk.affine((0, 0, 0, 1), (i * 40.0, 0, 0))) if it[2] != 'world' else g for i, (g, it) in enumerate(zip(geos, items))]
        aos = sk.bake_ao(spread, distance=.35, samples=(24, 12, 8)[ti])
        aos = [.3 + .7 * a for a in aos]
    for (name, g, frame, rest), ao in zip(items, aos):
        if g.faces == 0:
            continue
        P = g.arrays()[0].astype(np.float64)
        world = P if frame == 'world' else P + np.array(rest)
        bk = sk.Bake(g, ao, world=world, floors=(-99.0,), seed=7)
        om = sk.to_mesh('%s/%d' % (name, ti), g, bk)
        if name in ('crate', 'barrel'):
            mi = sb.mesh(om)
            for b in blocks:
                if ('-' + name + '-') in b['id']:
                    sb.share('block/%s/%d' % (b['id'], ti), 'block', mi)
            st = sb.stats.setdefault(('block', str(ti)), [0, 0, 0])
            st[0] += 1
            st[1] += sum(len(s) for s in om.submeshes) // 3
            st[2] += len(om.positions)
        else:
            sb.add('rig/%s/%d' % (name, ti), 'rig', om)
