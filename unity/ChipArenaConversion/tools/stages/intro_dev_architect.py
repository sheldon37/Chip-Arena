"""DEV · Architect of Reality's set (docs/INTRO-DEV-ARCHITECT.md "The set"): the owner's dev intro's modelled kit, every piece
in its own frame, written into the local bank ChipArenaIntroDev.bytes (never committed). Core NativeDevArchitectIntro places,
flies, dissolves and builds the pieces (its Kit layout is this file's numbers: keep the two in step).

Run inside Blender 5.x, headless:
  blender -b --factory-startup --python-exit-code 1 --python tools/stages/intro_dev_architect.py -- --out <dir> [--preview <dir>]
`--out` names the folder the bank is written to (the stage kit writes <out>/ChipArenaLocal/Resources/<bank>.bytes); `--bank`
names a file to copy it to as well.

Two worlds share the kit:
- the old arena (the entrance stage every intro plays on, modelled): deck plates on a 2 m grid, the hexagonal dais in six wedges,
  twelve neon pylons 7.5 m out, the dark skyline's towers. It comes apart.
- the architect's: a floating platform of obsidian (the round dais and three rings of tiles to 7.4 m), eight monoliths 9.3 m
  out on floating plinths, the crown (a ring of sixteen arcs 15 m up), the far spires, shards, and the void's grid. It is built.
Every solid piece has a `.wire` twin: its edges as thin glowing bars (the wireframe a piece falls away to, and is drawn in).

Frames (Unity space: metres, +y up; a piece's frame is where Core places it):
  old-plate          a 2 m deck plate's top centre                 old-dais-wedge   the hexagon's centre at its top; the wedge
                                                                                     toward +z (corners at +-30 degrees)
  old-pylon-base     the base's bottom centre                      old-pylon-shaft  the shaft's bottom centre (its neon on -z)
  old-tower-a/b/c    the tower's foot (its windows face -z)        dais             the round dais's top centre (its keel below)
  tile-1/2/3         the tile's top at its middle radius (the ring's centre at z = -rm; the tile toward +z)
  mono-base          the plinth's top centre (a shaft stands on it) mono-shaft-0..3 the segment's bottom centre
  mono-cap           the capstone's bottom centre                  crown-arc        the arc's middle on its 11.5 m radius, at the
                                                                                     section's centre (the axis at z = -11.5)
  spire-a/b/c        the foot                                      shard-a..d       the middle (about 1 m across)
  grid               the void's grid plane (y 0)
"""
import argparse
import math
import os
import shutil
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import stagekit as sk  # noqa: E402
from stagekit import Geo  # noqa: E402

BANK = 'ChipArenaIntroDev'

# ---- the layout's numbers (Core NativeDevArchitectIntro.Kit) ----------------------------------------------------------
PLATE = 4.0
PLATE_T = .12
HEX_R = 2.6                      # the old dais's circumradius (NativeEntranceStage.DaisRadius)
DAIS_H = .132                    # FloorY - GroundY
PYLON_H = 3.6
DAIS_NEW_R = 1.40
TILE_T = .40
RINGS = ((1.44, 3.20, 8), (3.24, 5.20, 12), (5.24, 7.40, 16))
SEAM = .04
MONO_W = (1.10, 1.00, .90, .80, .70)
MONO_SEG = 2.4
CAP_H = 1.6
HALO_R, HALO_ARCS, HALO_W, HALO_H, HALO_GAP = 11.5, 8, 1.1, .8, .3
CROWN_Y = 13.2

FIN = {}


def finishes():
    f = sk.fin
    D = sk
    FIN.update({
        # the old arena: the entrance stage's dark steel and its neon
        'deck': f('deck', 0x182030, D.DETAIL_IRON, smooth=.38, metal=.55, dirt=.7, wear=.35),
        'deck-dark': f('deck-dark', 0x0c111c, D.DETAIL_IRON, smooth=.3, metal=.5, dirt=.6),
        'bolt': f('bolt', 0x3a4152, D.DETAIL_IRON, smooth=.5, metal=.8, dirt=.4, wear=.5),
        'old-dais': f('old-dais', 0x1b2236, D.DETAIL_PLASTER, smooth=.32, metal=.1, dirt=.5, wear=.25),
        'old-skirt': f('old-skirt', 0x0a0e19, D.DETAIL_IRON, smooth=.3, metal=.4, dirt=.5),
        'pylon': f('pylon', 0x1a2032, D.DETAIL_IRON, smooth=.42, metal=.6, dirt=.5, wear=.4),
        'pylon-dark': f('pylon-dark', 0x0e1220, D.DETAIL_IRON, smooth=.35, metal=.6, dirt=.5),
        'neon': f('neon', 0xbfe8ff, D.DETAIL_GLOW, glow=2.2),
        'old-grid': f('old-grid', 0x4b86b8, D.DETAIL_GLOW, glow=.32),
        'tower': f('tower', 0x0b0e18, D.DETAIL_PLASTER, smooth=.2, dirt=.4),
        'tower-trim': f('tower-trim', 0x161b2a, D.DETAIL_IRON, smooth=.3, metal=.4, dirt=.4),
        'window-warm': f('window-warm', 0xff8a3a, D.DETAIL_GLOW, glow=.7),
        'window-cool': f('window-cool', 0x5a8cff, D.DETAIL_GLOW, glow=.6),
        'beacon': f('beacon', 0xff2a18, D.DETAIL_GLOW, glow=1.6),
        # the architect's: obsidian, basalt, gold, and light
        'obsidian': f('obsidian', 0x0e1015, D.DETAIL_MARBLE, smooth=.5, metal=.05, dirt=.15, wear=.25),
        'obsidian-dark': f('obsidian-dark', 0x08090c, D.DETAIL_MARBLE, smooth=.6, metal=.05, dirt=.15),
        'basalt': f('basalt', 0x1a1d24, D.DETAIL_CARVED, smooth=.4, metal=.05, dirt=.3, wear=.35),
        'basalt-dark': f('basalt-dark', 0x0d0f14, D.DETAIL_CARVED, smooth=.3, dirt=.2),
        'gold': f('gold', 0xc9a253, D.DETAIL_BRASS, smooth=.78, metal=.95, dirt=.1, wear=.5),
        'vein': f('vein', 0xc8f6ff, D.DETAIL_GLOW, glow=2.6),
        'vein-soft': f('vein-soft', 0x7fe2ff, D.DETAIL_GLOW, glow=1.2),
        'code': f('code', 0x6ff4ff, D.DETAIL_GLOW, glow=1.7),
        'crown': f('crown', 0xffd690, D.DETAIL_GLOW, glow=1.1),
        'grid': f('grid', 0x3fb8e0, D.DETAIL_GLOW, glow=.9),
        'wire': f('wire', 0xd8fbff, D.DETAIL_GLOW, glow=1.4),
    })


# ---------------------------------------------------------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------------------------------------------------------
def _n(v):
    l = math.sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2])
    return (v[0] / l, v[1] / l, v[2] / l) if l > 1e-12 else (0.0, 1.0, 0.0)


def _a(a, b, k=1.0):
    return (a[0] + b[0] * k, a[1] + b[1] * k, a[2] + b[2] * k)


def _s(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _x(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def polar(r, a, y, rm=0.0):
    """A point at radius r and angle a (radians from +z toward +x) about the ring's centre at z = -rm."""
    return (r * math.sin(a), y, r * math.cos(a) - rm)


def quad(g, a, b, c, d, f, normal=None):
    g.face((a, b, c, d), f, normal)


def rect_on(g, centre, u, v, normal, f, lift=0.0):
    """A rectangle centre +- u +- v (u, v half extents) on a face with `normal`, lifted along it."""
    c = _a(centre, normal, lift)
    g.face((_a(_a(c, u, -1), v, -1), _a(_a(c, u, 1), v, -1), _a(_a(c, u, 1), v, 1), _a(_a(c, u, -1), v, 1)), f, normal)


def bar(g, p0, p1, w, f):
    """A glowing bar of square section w from p0 to p1 (overrunning each end by w/2, so corners close): four sides."""
    d = _s(p1, p0)
    L = math.sqrt(d[0] ** 2 + d[1] ** 2 + d[2] ** 2)
    if L < 1e-6:
        return
    t = (d[0] / L, d[1] / L, d[2] / L)
    ref = (0, 1, 0) if abs(t[1]) < .9 else (1, 0, 0)
    u = _n(_x(ref, t))
    v = _x(t, u)
    h = w * .5
    a, b = _a(p0, t, -h), _a(p1, t, h)
    for (su, sv) in ((1, 0), (0, 1), (-1, 0), (0, -1)):
        n = _a((0, 0, 0), u, su) if su else _a((0, 0, 0), v, sv)
        side = _x(t, n)
        c0 = _a(a, n, h)
        c1 = _a(b, n, h)
        g.face((_a(c0, side, -h), _a(c1, side, -h), _a(c1, side, h), _a(c0, side, h)), f, n)


def poly_wire(g, pts, w, f, closed=True):
    m = len(pts)
    for i in range(m if closed else m - 1):
        bar(g, pts[i], pts[(i + 1) % m], w, f)


def frustum(g, w0, w1, y0, y1, f, cham=.05, levels=1, d0=None, d1=None, caps=(True, True), cap_f=None):
    """A rectangular frustum (w x d at y0 to w1 x d1 at y1) with its vertical corners chamfered (an octagonal section),
    split into `levels` bands up its height."""
    d0 = w0 if d0 is None else d0
    d1 = w1 if d1 is None else d1

    def ring(w, d, y):
        a, b = w / 2, d / 2
        c = min(cham, a * .4, b * .4)
        return [(a - c, y, b), (a, y, b - c), (a, y, -b + c), (a - c, y, -b), (-a + c, y, -b), (-a, y, -b + c), (-a, y, b - c), (-a + c, y, b)]
    rings = []
    for k in range(levels + 1):
        u = k / levels
        rings.append(ring(w0 + (w1 - w0) * u, d0 + (d1 - d0) * u, y0 + (y1 - y0) * u))
    for k in range(levels):
        r0, r1 = rings[k], rings[k + 1]
        for i in range(8):
            j = (i + 1) % 8
            mid = ((r0[i][0] + r0[j][0]) * .5, 0.0, (r0[i][2] + r0[j][2]) * .5)
            nrm = _x(_s(r0[j], r0[i]), _s(r1[i], r0[i]))
            if nrm[0] * mid[0] + nrm[2] * mid[2] < 0:
                nrm = (-nrm[0], -nrm[1], -nrm[2])
            quad(g, r0[i], r0[j], r1[j], r1[i], f, _n(nrm))
    cf = f if cap_f is None else cap_f
    if caps[0]:
        g.face(rings[0][::-1], cf, (0, -1, 0))
    if caps[1]:
        g.face(rings[-1], cf, (0, 1, 0))
    return rings


def box(g, x0, x1, y0, y1, z0, z1, f, T, bevel=.02, skip=()):
    sk.box(g, x0, x1, y0, y1, z0, z1, f, T, bevel=bevel, cell=0 if T.i else max(T.cell, .6), skip=skip)


def box_wire(g, x0, x1, y0, y1, z0, z1, w, f):
    c = [(x, y, z) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
    for i in range(8):
        for j in range(i + 1, 8):
            diff = sum(1 for k in range(3) if c[i][k] != c[j][k])
            if diff == 1:
                bar(g, c[i], c[j], w, f)


def sector(g, r0, r1, ah, y0, y1, f, T, rm, bevel=.025, gap_m=0.0, rings=2, top_f=None, side_f=None, bot_f=None):
    """An annular sector slab about the ring's centre (z = -rm): radius r0..r1, angles -ah..ah less half the seam gap_m at
    each radial edge (as an arc length at each radius), its top at y1 and bottom at y0, the top and bottom edges chamfered."""
    n = max(2, int(math.ceil(2 * ah / (math.pi / T.arc) * 1.0)))
    b = bevel if T.bevel else 0.0
    top_f = f if top_f is None else top_f
    side_f = f if side_f is None else side_f
    bot_f = f if bot_f is None else bot_f

    def edge(r, d):
        return ah - (gap_m * .5 + d) / r

    def outline(d, y):
        pts = []
        ea = edge(r1 - d, d)
        for i in range(n + 1):
            pts.append(polar(r1 - d, -ea + 2 * ea * i / n, y, rm))
        ea = edge(r0 + d, d)
        for i in range(n + 1):
            pts.append(polar(r0 + d, ea - 2 * ea * i / n, y, rm))
        return pts
    levels = [(b, y1), (0.0, y1 - b), (0.0, y0 + b), (b, y0)] if b > 0 else [(0.0, y1), (0.0, y0)]
    loops = [outline(d, y) for d, y in levels]
    m = len(loops[0])
    # bands between the loops: smooth round the arcs, flat on the radial edges
    for k in range(len(loops) - 1):
        L0, L1 = loops[k], loops[k + 1]
        ff = side_f
        for i in range(m):
            j = (i + 1) % m
            radial = i == n or j == 0
            pts = (L0[i], L0[j], L1[j], L1[i])
            if radial:
                # the radial edge's outward normal: along the arc's tangent at its angle (+ at +ah, - at -ah)
                a = ah if i == n else -ah
                tn = (math.cos(a), 0.0, -math.sin(a)) if i == n else (-math.cos(a), 0.0, math.sin(a))
                tilt = 1.0 if (b > 0 and k == 0) else -1.0 if (b > 0 and k == 2) else 0.0
                g.face(pts, ff, _n((tn[0], tilt, tn[2])))
            else:
                # outward horizontal normals at each corner (outer arc out, inner arc in), tilted for the chamfers
                def nrm(p, outer):
                    h = _n((p[0], 0.0, p[2] + rm))
                    h = h if outer else (-h[0], 0.0, -h[2])
                    if b > 0 and k == 0:
                        return _n((h[0], 1.0, h[2]))
                    if b > 0 and k == 2:
                        return _n((h[0], -1.0, h[2]))
                    return h
                outer = i < n
                g.face(pts, ff, normals=(nrm(L0[i], outer), nrm(L0[j], outer), nrm(L1[j], outer), nrm(L1[i], outer)))
    # caps: a grid between the inner and outer arcs
    for (d, y, nrm, cf, flip) in ((levels[0][0], levels[0][1], (0, 1, 0), top_f, False), (levels[-1][0], levels[-1][1], (0, -1, 0), bot_f, True)):
        rr = [r0 + d + (r1 - r0 - 2 * d) * k / rings for k in range(rings + 1)]
        for k in range(rings):
            ra, rb = rr[k], rr[k + 1]
            ea, eb = edge(ra, d), edge(rb, d)
            for i in range(n):
                a0 = -ea + 2 * ea * i / n
                a1 = -ea + 2 * ea * (i + 1) / n
                b0 = -eb + 2 * eb * i / n
                b1 = -eb + 2 * eb * (i + 1) / n
                g.face((polar(ra, a0, y, rm), polar(ra, a1, y, rm), polar(rb, b1, y, rm), polar(rb, b0, y, rm)), cf, nrm)


def sector_wire(g, r0, r1, ah, y0, y1, rm, w, f, gap_m=0.0, n=6):
    """A tile's wireframe: its radial edges, top and bottom, and its four corners' uprights. No arcs: laid side by side, a
    ring of tiles' arcs would draw circles round the floor (owner: no ring outlines)."""
    def edge(r):
        return ah - gap_m * .5 / r
    for y in (y0, y1):
        eo, ei = edge(r1), edge(r0)
        bar(g, polar(r1, -eo, y, rm), polar(r0, -ei, y, rm), w, f)
        bar(g, polar(r1, eo, y, rm), polar(r0, ei, y, rm), w, f)
    for (r, s) in ((r1, -1), (r1, 1), (r0, -1), (r0, 1)):
        e = edge(r)
        bar(g, polar(r, s * e, y0, rm), polar(r, s * e, y1, rm), w, f)


def hashf(*k):
    h = 2166136261
    for v in k:
        h = ((h ^ (int(v) & 0xffffffff)) * 16777619) & 0xffffffff
    h ^= h >> 13
    h = (h * 0x5bd1e995) & 0xffffffff
    h ^= h >> 15
    return (h & 0xffffff) / float(0x1000000)


# The code glyphs engraved on the kit: 3 x 5 dot matrices (an original set; columns bit 2..0, rows top to bottom).
GLYPHS = [
    (7, 5, 5, 5, 7), (2, 6, 2, 2, 7), (7, 1, 7, 4, 7), (7, 1, 3, 1, 7), (5, 5, 7, 1, 1), (7, 4, 7, 1, 7), (4, 4, 7, 5, 7),
    (7, 1, 2, 2, 2), (7, 5, 7, 5, 7), (7, 5, 7, 1, 1), (2, 5, 7, 5, 5), (6, 5, 6, 5, 6), (3, 4, 4, 4, 3), (6, 5, 5, 5, 6),
    (5, 2, 5, 2, 5), (7, 2, 2, 2, 7), (1, 2, 4, 2, 1), (4, 2, 1, 2, 4), (0, 7, 0, 7, 0), (2, 2, 7, 2, 2),
]


def engrave(g, origin, u, v, normal, cols, rows, cell, f, seed, lift=.004, fill=.62):
    """A block of code glyphs on a face: `cols` x `rows` glyphs of 3 x 5 dots, each dot a small lit square. origin: the
    block's top-left corner; u: one cell (a dot's step) to the right; v: one cell down."""
    for gy in range(rows):
        for gx in range(cols):
            gl = GLYPHS[int(hashf(seed, gx, gy) * len(GLYPHS))]
            if hashf(seed + 7, gx, gy) > fill:
                continue
            for r in range(5):
                for c in range(3):
                    if not (gl[r] >> (2 - c)) & 1:
                        continue
                    cu = gx * 4 + c + .5
                    cv = gy * 6.5 + r + .5
                    centre = _a(_a(origin, u, cu), v, cv)
                    rect_on(g, centre, _a((0, 0, 0), u, .38), _a((0, 0, 0), v, .38), normal, f, lift)


# ---------------------------------------------------------------------------------------------------------------------------
# The old arena
# ---------------------------------------------------------------------------------------------------------------------------
def old_plate(T):
    """A 4 m steel deck plate of the old stage: a faint lit line every metre (the stage's 1 m grid), bolts where they cross."""
    g = Geo()
    h = PLATE * .5 - .008
    box(g, -h, h, -PLATE_T, 0, -h, h, FIN['deck'], T, bevel=.018)
    w = .009
    for k in (-1.0, 0.0, 1.0):
        rect_on(g, (k, 0, 0), (w, 0, 0), (0, 0, h - .04), (0, 1, 0), FIN['old-grid'], .003)
        rect_on(g, (0, 0, k), (h - .04, 0, 0), (0, 0, w), (0, 1, 0), FIN['old-grid'], .003)
    for s_ in (-1, 1):
        rect_on(g, (s_ * (h - .02), 0, 0), (w, 0, 0), (0, 0, h - .03), (0, 1, 0), FIN['old-grid'], .003)
        rect_on(g, (0, 0, s_ * (h - .02)), (h - .03, 0, 0), (0, 0, w), (0, 1, 0), FIN['old-grid'], .003)
    if T.detail:
        # bolt heads beside the crossings, and a recessed hatch in one quarter
        for x in (-1.5, -.5, .5, 1.5):
            for z in (-1.5, -.5, .5, 1.5):
                if hashf(int(x * 4), int(z * 4), 9) > .55:
                    continue
                box(g, x - .03, x + .03, 0, .012, z - .03, z + .03, FIN['bolt'], T, bevel=.006)
        box(g, .25, .85, -.006, .004, -.85, -.25, FIN['deck-dark'], T, bevel=.004)
    return g


def old_plate_wire(T, w):
    g = Geo()
    h = PLATE * .5 - .008
    box_wire(g, -h, h, -PLATE_T, 0, -h, h, w, FIN['wire'])
    return g


def hex_corner(k):
    a = math.radians(30 + 60 * k)
    return (HEX_R * math.sin(a), HEX_R * math.cos(a))


def old_dais_wedge(T):
    """One sixth of the old hexagonal dais: the triangle centre, corner -30, corner +30 degrees (from +z), and its skirt."""
    g = Geo()
    c0, c1 = hex_corner(-1), hex_corner(0)
    top, bot = 0.0, -DAIS_H
    b = .02 if T.bevel else 0.0
    # top: in from the outer edge by the chamfer
    k = (HEX_R - b * 1.2) / HEX_R
    a = (0.0, top, 0.0)
    p0, p1 = (c0[0] * k, top, c0[1] * k), (c1[0] * k, top, c1[1] * k)
    # subdivide the top into strips for its colour and occlusion
    steps = 4 if T.fine else 2
    for i in range(steps):
        u0, u1 = i / steps, (i + 1) / steps
        q0 = (p0[0] * u0, top, p0[2] * u0)
        q1 = (p1[0] * u0, top, p1[2] * u0)
        q2 = (p1[0] * u1, top, p1[2] * u1)
        q3 = (p0[0] * u1, top, p0[2] * u1)
        if i == 0:
            g.face((a, q3, q2), FIN['old-dais'], (0, 1, 0))
        else:
            g.face((q0, q3, q2, q1), FIN['old-dais'], (0, 1, 0))
    o0, o1 = (c0[0], top - b, c0[1]), (c1[0], top - b, c1[1])
    nrm = _n((0, 0, 1))
    if b > 0:
        g.face((p0, o0, o1, p1), FIN['old-dais'], _n((0, 1, 1)))
    g.face((o0, (c0[0], bot, c0[1]), (c1[0], bot, c1[1]), o1), FIN['old-dais'], nrm)
    # the skirt: a darker bevelled band round the foot
    sk_ = 1 + .12 / HEX_R
    s0, s1 = (c0[0] * sk_, bot + .06, c0[1] * sk_), (c1[0] * sk_, bot + .06, c1[1] * sk_)
    g.face(((c0[0], bot + .03, c0[1]), s0, s1, (c1[0], bot + .03, c1[1])), FIN['old-skirt'], _n((0, 1, 1)))
    g.face((s0, (s0[0], bot, s0[2]), (s1[0], bot, s1[2]), s1), FIN['old-skirt'], nrm)
    # the inner edges (between wedges) are never seen together; give each wedge its own two inner walls for when it lifts
    for (cx, s) in ((c0, 1), (c1, -1)):
        pts = ((0.0, top, 0.0), (0.0, bot, 0.0), (cx[0], bot, cx[1]), (cx[0], top - b, cx[1]))
        g.face(pts, FIN['old-skirt'])
    g.face(((0.0, bot, 0.0), (c1[0], bot, c1[1]), (c0[0], bot, c0[1])), FIN['old-skirt'], (0, -1, 0))
    if T.detail:
        # an inlaid steel strip along the outer edge's top, and the wedge's bolts
        for i in range(3):
            u = (i + .5) / 3
            p = (p0[0] + (p1[0] - p0[0]) * u, top, p0[2] + (p1[2] - p0[2]) * u)
            p = (p[0] * .93, top, p[2] * .93)
            box(g, p[0] - .03, p[0] + .03, 0, .01, p[2] - .03, p[2] + .03, FIN['bolt'], T, bevel=.006)
    return g


def old_dais_wire(T, w):
    g = Geo()
    c0, c1 = hex_corner(-1), hex_corner(0)
    for y in (0.0, -DAIS_H):
        poly_wire(g, [(0.0, y, 0.0), (c0[0], y, c0[1]), (c1[0], y, c1[1])], w, FIN['wire'])
    for c in (c0, c1):
        bar(g, (c[0], 0, c[1]), (c[0], -DAIS_H, c[1]), w, FIN['wire'])
    return g


def old_pylon_base(T):
    g = Geo()
    box(g, -.35, .35, 0, .18, -.35, .35, FIN['pylon-dark'], T, bevel=.025)
    if T.detail:
        for (x, z) in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
            box(g, x * .27 - .03, x * .27 + .03, .18, .2, z * .27 - .03, z * .27 + .03, FIN['bolt'], T, bevel=.006)
    return g


def old_pylon_shaft(T):
    """The neon pylon's shaft: a steel post 3.6 m tall (from the base's foot) with its neon strip on the -z (arena) face, a
    capped top that glows, panel lines and a vent."""
    g = Geo()
    y0, y1 = .18, PYLON_H
    box(g, -.21, .21, y0, y1 - .06, -.21, .21, FIN['pylon'], T, bevel=.02)
    box(g, -.225, .225, y1 - .06, y1, -.225, .225, FIN['pylon-dark'], T, bevel=.012)
    # the cap's glow and the neon strip in its channel
    rect_on(g, (0, y1, 0), (.2, 0, 0), (0, 0, .2), (0, 1, 0), FIN['neon'], .004)
    box(g, -.055, .055, .45, y1 - .25, -.235, -.21, FIN['pylon-dark'], T, bevel=.006)
    rect_on(g, (0, (.45 + y1 - .25) * .5 + .05, -.235), (.035, 0, 0), (0, (y1 - .7) * .5 - .05, 0), (0, 0, -1), FIN['neon'], .003)
    if T.detail:
        # panel seams round the post, a vent grille low on the back
        for y in (1.1, 2.3):
            for (n, u) in (((0, 0, 1), (.2, 0, 0)), ((1, 0, 0), (0, 0, .2)), ((-1, 0, 0), (0, 0, .2))):
                rect_on(g, (n[0] * .21, y, n[2] * .21), u, (0, .006, 0), n, FIN['pylon-dark'], .002)
        for i in range(5):
            rect_on(g, (0, .6 + i * .05, .21), (.12, 0, 0), (0, .012, 0), (0, 0, 1), FIN['pylon-dark'], .002)
    return g


def old_pylon(T):
    g = old_pylon_base(T)
    g.extend(old_pylon_shaft(T))
    return g


def old_pylon_wire(T, w):
    g = Geo()
    box_wire(g, -.35, .35, 0, .18, -.35, .35, w, FIN['wire'])
    box_wire(g, -.21, .21, .18, PYLON_H, -.21, .21, w, FIN['wire'])
    for y in (1.1, 2.3):
        poly_wire(g, [(-.21, y, -.21), (.21, y, -.21), (.21, y, .21), (-.21, y, .21)], w, FIN['wire'])
    return g


TOWERS = {
    # name: [(half width x, half depth z, y0, y1)] stacked blocks; the windows face -z (toward the arena)
    'old-tower-a': [(2.0, 1.6, 0, 11.5), (1.5, 1.2, 11.5, 14.0)],
    'old-tower-b': [(2.6, 2.0, 0, 6.0), (1.9, 1.5, 6.0, 9.0)],
    'old-tower-c': [(1.4, 1.2, 0, 16.0), (1.0, .9, 16.0, 18.5), (.25, .25, 18.5, 20.5)],
}


def old_tower(name, T):
    g = Geo()
    blocks = TOWERS[name]
    for k, (hx, hz, y0, y1) in enumerate(blocks):
        box(g, -hx, hx, y0, y1, -hz, hz, FIN['tower'], T, bevel=.05)
        box(g, -hx - .06, hx + .06, y1 - .25, y1, -hz - .06, hz + .06, FIN['tower-trim'], T, bevel=.02)
        if hx < .5:
            continue
        # windows: rows on each face, a few lit (warm or cool), most dark glass
        rows = int((y1 - y0 - 1.0) / 1.2)
        for face in range(4):
            n = ((0, 0, -1), (1, 0, 0), (0, 0, 1), (-1, 0, 0))[face]
            half = hx if face % 2 == 0 else hz
            cols = max(1, int((2 * half - .6) / 1.1))
            for r in range(rows):
                for c in range(cols):
                    lit = hashf(k * 97 + face, r, c, len(name))
                    if lit > .32 and face != 0:
                        continue
                    if lit > .55:
                        continue
                    x = -half + .3 + (c + .5) * (2 * half - .6) / cols
                    y = y0 + .8 + r * 1.2
                    fin = FIN['window-warm'] if lit < .2 else FIN['window-cool'] if lit < .34 else FIN['tower-trim']
                    if face % 2 == 0:
                        centre = (x, y, n[2] * hz)
                        u = (.32, 0, 0)
                    else:
                        centre = (n[0] * hx, y, x)
                        u = (0, 0, .32)
                    rect_on(g, centre, u, (0, .26, 0), n, fin, .004)
    top = blocks[-1]
    rect_on(g, (0, top[3] + .06, 0), (.12, 0, 0), (0, 0, .12), (0, 1, 0), FIN['beacon'], .002)
    return g


def old_tower_wire(name, T, w):
    g = Geo()
    for (hx, hz, y0, y1) in TOWERS[name]:
        box_wire(g, -hx, hx, y0, y1, -hz, hz, w, FIN['wire'])
    return g


def skyline_spots(k):
    """The six towers of skyline quadrant k (0..3): (name, x, z, yaw, scale) about the arena's centre, their windows facing it."""
    out = []
    for j in range(6):
        i = k * 6 + j
        a = (i + .35 * hashf(77, i)) / 24 * 2 * math.pi
        r = 22 + 7 * hashf(78, i)
        name = ('old-tower-a', 'old-tower-b', 'old-tower-c')[int(hashf(79, i) * 3)]
        out.append((name, math.sin(a) * r, math.cos(a) * r, a, .8 + .4 * hashf(80, i)))
    return out


def old_skyline(k, T, wire=False, w=.05):
    g = Geo()
    for (name, x, z, a, sc) in skyline_spots(k):
        t = old_tower_wire(name, T, w / sc) if wire else old_tower(name, T)
        g.extend(t.transformed(sk.affine(sk.quat_axis(0, 1, 0, a), (x, 0, z), (sc, sc, sc))))
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The architect's world
# ---------------------------------------------------------------------------------------------------------------------------
def dais(T):
    """The round obsidian dais the architect lands on: a slab with a sixteen-pointed sigil of light inlaid in its top (rays,
    not rings), a boss at its middle, and a keel hanging under it into the void."""
    g = Geo()
    sides = (48, 24, 12)[T.i]
    b = .03 if T.bevel else 0.0
    prof = [(.0, -TILE_T), (DAIS_NEW_R - b, -TILE_T), (DAIS_NEW_R, -TILE_T + b), (DAIS_NEW_R, -b), (DAIS_NEW_R - b, 0.0)]
    sk.lathe(g, [(r, y) for (r, y) in prof[1:]], (0, 0, 0), FIN['basalt'], sides, smooth=True)
    # the top: rings of quads from the rim in (the colour's resolution), a fan at the middle
    rr = [DAIS_NEW_R - b, 1.0, .62, .3]
    for k in range(len(rr) - 1):
        for i in range(sides):
            a0, a1 = 2 * math.pi * i / sides, 2 * math.pi * (i + 1) / sides
            g.face((polar(rr[k], a0, 0), polar(rr[k], a1, 0), polar(rr[k + 1], a1, 0), polar(rr[k + 1], a0, 0)), FIN['obsidian'], (0, 1, 0))
    sk.lathe(g, [(.3, 0.0), (.26, .025), (0.0, .03)], (0, 0, 0), FIN['gold'], sides, cap1=False)
    # the keel: an inverted cone of dark stone under the slab
    sk.lathe(g, [(.02, -2.4), (.5, -1.5), (1.05, -.75), (DAIS_NEW_R - .06, -TILE_T)], (0, 0, 0), FIN['basalt-dark'], sides)
    # the sigil: sixteen rays, alternately long and short, each a lit strip in a dark bezel
    for i in range(16):
        a = 2 * math.pi * i / 16
        r0, r1 = .36, (1.22 if i % 2 == 0 else .9)
        c = polar((r0 + r1) * .5, a, 0)
        u = _a((0, 0, 0), (math.sin(a), 0, math.cos(a)), (r1 - r0) * .5)
        v = (math.cos(a) * .028, 0, -math.sin(a) * .028)
        rect_on(g, c, u, v, (0, 1, 0), FIN['obsidian-dark'], .002)
        rect_on(g, c, _a((0, 0, 0), u, .97), _a((0, 0, 0), v, .42), (0, 1, 0), FIN['vein'], .004)
        if T.detail and i % 2 == 0:
            # a chevron of light at the long ray's tip
            for s in (-1, 1):
                tip = polar(r1 + .06, a, 0)
                back = polar(r1 - .08, a + s * .05, 0)
                mid = ((tip[0] + back[0]) / 2, 0, (tip[2] + back[2]) / 2)
                d = _s(tip, back)
                L = math.sqrt(d[0] ** 2 + d[2] ** 2)
                uu = (d[0] / 2, 0, d[2] / 2)
                vv = (-d[2] / L * .012, 0, d[0] / L * .012)
                rect_on(g, mid, uu, vv, (0, 1, 0), FIN['vein-soft'], .004)
    # veins of light down the keel
    for i in range(8):
        a = 2 * math.pi * (i + .5) / 8
        pts = [polar(DAIS_NEW_R - .07, a, -TILE_T - .02), polar(1.05, a, -.77), polar(.5, a, -1.52), polar(.04, a, -2.38)]
        for k in range(3):
            p0, p1 = pts[k], pts[k + 1]
            d = _s(p1, p0)
            nrm = _n((math.sin(a), .6, math.cos(a)))
            mid = _a(p0, d, .5)
            side = _n(_x(d, nrm))
            g.face((_a(_a(p0, side, -.018), nrm, .01), _a(_a(p0, side, .018), nrm, .01), _a(_a(p1, side, .018), nrm, .01), _a(_a(p1, side, -.018), nrm, .01)), FIN['vein-soft'], nrm)
    return g


def dais_wire(T, w):
    """The dais's wireframe: its eight spokes and its keel's lines (no rim circles: no ring outlines)."""
    g = Geo()
    for i in range(8):
        a = 2 * math.pi * i / 8
        bar(g, polar(.05, a, 0), polar(DAIS_NEW_R, a, 0), w, FIN['wire'])
        bar(g, polar(DAIS_NEW_R, a, -TILE_T), polar(.02, a, -2.4), w, FIN['wire'])
    return g


def ring_tile(k, T):
    r0, r1, count = RINGS[k]
    rm = (r0 + r1) * .5
    ah = math.pi / count
    g = Geo()
    sector(g, r0, r1, ah, -TILE_T, 0.0, FIN['basalt'], T, rm, bevel=.025, gap_m=SEAM, rings=(3, 2, 1)[T.i], top_f=FIN['obsidian'], bot_f=FIN['basalt-dark'])
    # the seam's light: a lit strip along each radial edge of the top, half the line between two tiles
    for s in (-1, 1):
        e0, e1 = ah - (SEAM * .5 + .018) / (r0 + .03), ah - (SEAM * .5 + .018) / (r1 - .03)
        p0, p1 = polar(r0 + .03, s * e0, 0, rm), polar(r1 - .03, s * e1, 0, rm)
        d = _s(p1, p0)
        L = math.sqrt(d[0] ** 2 + d[2] ** 2)
        v = (-d[2] / L * .007, 0, d[0] / L * .007)
        rect_on(g, _a(p0, d, .5), (d[0] / 2, 0, d[2] / 2), v, (0, 1, 0), FIN['vein'], .003)
    # the inscription: a line of code glyphs along the tile's middle (Fine and Mid), its bezel
    if T.detail:
        cell = (.034, .03, .028)[k]
        cols = (3, 4, 4)[k]
        width = cols * 4 * cell
        a_c = 0.0
        r_c = rm + (r1 - r0) * .12
        tang = (math.cos(a_c), 0, -math.sin(a_c))
        rad = (math.sin(a_c), 0, math.cos(a_c))
        centre = polar(r_c, a_c, 0, rm)
        origin = _a(_a(centre, tang, -width * .5), rad, 5 * cell * .5)
        rect_on(g, centre, _a((0, 0, 0), tang, width * .5 + cell), _a((0, 0, 0), rad, 3.2 * cell), (0, 1, 0), FIN['obsidian-dark'], .002)
        engrave(g, origin, _a((0, 0, 0), tang, cell), _a((0, 0, 0), rad, -cell), (0, 1, 0), cols, 1, cell, FIN['code'], 31 + k, lift=.004, fill=1.0)
        # a groove across the tile, nearer the inner edge
        rr = r0 + (r1 - r0) * .3
        ea = ah - (SEAM * .5 + .05) / rr
        pa, pb = polar(rr, -ea, 0, rm), polar(rr, ea, 0, rm)
        d = _s(pb, pa)
        rect_on(g, _a(pa, d, .5), (d[0] / 2, 0, d[2] / 2), (0, 0, .012), (0, 1, 0), FIN['obsidian-dark'], .002)
    if k == 2:
        # the platform's rim: a gold band round the outer ring's face
        e = ah - SEAM * .5 / r1
        n = 4
        for i in range(n):
            a0, a1 = -e + 2 * e * i / n, -e + 2 * e * (i + 1) / n
            for (ya, yb) in ((-.09, -.15),):
                pa0, pa1 = polar(r1 + .006, a0, ya, rm), polar(r1 + .006, a1, ya, rm)
                pb0, pb1 = polar(r1 + .006, a0, yb, rm), polar(r1 + .006, a1, yb, rm)
                na, nb = _n((math.sin(a0), 0, math.cos(a0))), _n((math.sin(a1), 0, math.cos(a1)))
                g.face((pa0, pb0, pb1, pa1), FIN['gold'], normals=(na, na, nb, nb))
    return g


def ring_tile_wire(k, T, w):
    r0, r1, count = RINGS[k]
    rm = (r0 + r1) * .5
    g = Geo()
    sector_wire(g, r0, r1, math.pi / count, -TILE_T, 0.0, rm, w, FIN['wire'], gap_m=SEAM, n=(6, 4, 3)[T.i])
    return g


def mono_base(T):
    """The monolith's floating plinth: a stepped block (its top at the frame's origin) on an inverted pyramid keel."""
    g = Geo()
    lv = (3, 2, 1)[T.i]
    frustum(g, 1.6, 1.6, -.7, 0.0, FIN['obsidian'], cham=.06, levels=lv)
    frustum(g, 2.0, 2.0, -1.3, -.7, FIN['basalt'], cham=.08, levels=1, caps=(False, True))
    frustum(g, 2.0, .16, -3.3, -1.3, FIN['basalt-dark'], cham=.08, levels=lv, caps=(True, False))
    # the gold collar at the top and the vein of light at each keel corner
    frustum(g, 1.64, 1.64, -.1, -.04, FIN['gold'], cham=.06, caps=(False, False))
    for (sx, sz) in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        p0, p1 = (sx * .97, -1.32, sz * .97), (sx * .09, -3.28, sz * .09)
        d = _s(p1, p0)
        nrm = _n((sx, .5, sz))
        side = _n(_x(d, nrm))
        g.face((_a(_a(p0, side, -.02), nrm, .015), _a(_a(p0, side, .02), nrm, .015), _a(_a(p1, side, .02), nrm, .015), _a(_a(p1, side, -.02), nrm, .015)), FIN['vein-soft'], nrm)
    if T.detail:
        # a lit slot in each face of the upper block
        for n in ((0, 0, -1), (1, 0, 0), (0, 0, 1), (-1, 0, 0)):
            u = (.5, 0, 0) if n[2] else (0, 0, .5)
            rect_on(g, (n[0] * .8, -.38, n[2] * .8), u, (0, .02, 0), n, FIN['obsidian-dark'], .002)
            rect_on(g, (n[0] * .8, -.38, n[2] * .8), _a((0, 0, 0), u, .96), (0, .008, 0), n, FIN['vein'], .004)
    return g


def mono_base_wire(T, w):
    g = Geo()
    box_wire(g, -.8, .8, -.7, 0, -.8, .8, w, FIN['wire'])
    box_wire(g, -1, 1, -1.3, -.7, -1, 1, w, FIN['wire'])
    for (sx, sz) in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        bar(g, (sx * 1.0, -1.3, sz * 1.0), (0, -3.3, 0), w, FIN['wire'])
    return g


def mono_shaft(k, T):
    """Monolith segment k: a square frustum MONO_W[k] to MONO_W[k+1] wide and MONO_SEG tall, its corners chamfered, a vein of
    light down every face in a dark bezel, a gold collar at its top, and columns of code glyphs on two faces (Fine)."""
    g = Geo()
    w0, w1 = MONO_W[k], MONO_W[k + 1]
    lv = (4, 2, 1)[T.i]
    frustum(g, w0, w1, 0.0, MONO_SEG, FIN['obsidian'], cham=.05, levels=lv, caps=(k == 0, False))
    frustum(g, w1 + .05, w1 + .04, MONO_SEG - .14, MONO_SEG, FIN['gold'], cham=.05, caps=(True, True))
    slope = (w0 - w1) * .5 / MONO_SEG
    for face in range(4):
        n = ((0, 0, -1), (1, 0, 0), (0, 0, 1), (-1, 0, 0))[face]
        nn = _n((n[0], slope, n[2]))

        def at(y, x):
            half = (w0 + (w1 - w0) * y / MONO_SEG) * .5
            if n[2]:
                return (x, y, n[2] * half)
            return (n[0] * half, y, x)
        y0, y1 = .08, MONO_SEG - .2
        top, bot = at(y1, 0), at(y0, 0)
        mid = ((top[0] + bot[0]) / 2, (top[1] + bot[1]) / 2, (top[2] + bot[2]) / 2)
        up = ((top[0] - bot[0]) / 2, (top[1] - bot[1]) / 2, (top[2] - bot[2]) / 2)
        across = (1, 0, 0) if n[2] else (0, 0, 1)
        rect_on(g, mid, _a((0, 0, 0), across, .07), up, nn, FIN['obsidian-dark'], .002)
        rect_on(g, mid, _a((0, 0, 0), across, .018), _a((0, 0, 0), up, .995), nn, FIN['vein'], .004)
        if T.fine and k in (1, 3) and face in (0, 2):
            cell = .022
            origin = _a(at(y1 - .05, -.42 * w1), (0, 0, 0))
            engrave(g, origin, _a((0, 0, 0), across, cell * (1 if face == 0 else -1)), (0, -cell, 0), nn, 2, 9, cell, FIN['code'], 91 + k * 4 + face, lift=.004, fill=.75)
            origin = at(y1 - .05, .42 * w1 - 2 * 4 * cell)
            engrave(g, origin, _a((0, 0, 0), across, cell * (1 if face == 0 else -1)), (0, -cell, 0), nn, 2, 9, cell, FIN['code'], 93 + k * 4 + face, lift=.004, fill=.75)
    return g


def mono_shaft_wire(k, T, w):
    g = Geo()
    w0, w1 = MONO_W[k] * .5, MONO_W[k + 1] * .5
    b = [(-w0, 0, -w0), (w0, 0, -w0), (w0, 0, w0), (-w0, 0, w0)]
    t = [(-w1, MONO_SEG, -w1), (w1, MONO_SEG, -w1), (w1, MONO_SEG, w1), (-w1, MONO_SEG, w1)]
    poly_wire(g, b, w, FIN['wire'])
    poly_wire(g, t, w, FIN['wire'])
    for i in range(4):
        bar(g, b[i], t[i], w, FIN['wire'])
    return g


def mono_cap(T):
    """The capstone: a gold pyramidion from the top segment's width to a point CAP_H up, a crystal of light at its tip."""
    g = Geo()
    w = MONO_W[-1] * .5 + .02
    base = [(-w, 0, -w), (w, 0, -w), (w, 0, w), (-w, 0, w)]
    tip = (0, CAP_H, 0)
    lv = (3, 2, 1)[T.i]
    for i in range(4):
        a, b = base[i], base[(i + 1) % 4]
        for k in range(lv):
            u0, u1 = k / lv, (k + 1) / lv
            p = [_a(a, _s(tip, a), u0), _a(b, _s(tip, b), u0), _a(b, _s(tip, b), u1), _a(a, _s(tip, a), u1)]
            if k == lv - 1:
                g.face((p[0], p[1], tip), FIN['gold'])
            else:
                g.face(p, FIN['gold'])
    g.face(base[::-1], FIN['obsidian'], (0, -1, 0))
    # the tip's crystal (lit), set a little proud of the point
    c = (0, CAP_H - .12, 0)
    s = .07
    pts = [(s, CAP_H - .12, 0), (0, CAP_H - .12, s), (-s, CAP_H - .12, 0), (0, CAP_H - .12, -s)]
    for i in range(4):
        g.face((pts[i], pts[(i + 1) % 4], (0, CAP_H + .1, 0)), FIN['crown'])
    return g


def mono_cap_wire(T, w):
    g = Geo()
    h = MONO_W[-1] * .5 + .02
    base = [(-h, 0, -h), (h, 0, -h), (h, 0, h), (-h, 0, h)]
    poly_wire(g, base, w, FIN['wire'])
    for p in base:
        bar(g, p, (0, CAP_H, 0), w, FIN['wire'])
    return g


def mono_obelisk(T):
    """The monolith's obelisk: the four shaft segments stacked and the capstone (its foot at the frame's origin)."""
    g = Geo()
    for k in range(4):
        g.extend(mono_shaft(k, T).transformed([1, 0, 0, 0, 0, 1, 0, k * MONO_SEG, 0, 0, 1, 0]))
    g.extend(mono_cap(T).transformed([1, 0, 0, 0, 0, 1, 0, 4 * MONO_SEG, 0, 0, 1, 0]))
    return g


def mono_obelisk_wire(T, w):
    g = Geo()
    for k in range(4):
        g.extend(mono_shaft_wire(k, T, w).transformed([1, 0, 0, 0, 0, 1, 0, k * MONO_SEG, 0, 0, 1, 0]))
    g.extend(mono_cap_wire(T, w).transformed([1, 0, 0, 0, 0, 1, 0, 4 * MONO_SEG, 0, 0, 1, 0]))
    return g


def halo_arc(T):
    """One of the crown's sixteen arcs: a chamfered beam of obsidian on a 10 m radius, a channel of light along its inner face,
    two lines under it, ribs on its outer face and a gold cap strip on top; frame: its middle, the axis at z = -HALO_R."""
    g = Geo()
    ah = math.pi / HALO_ARCS - HALO_GAP * .5 / HALO_R
    n = (24, 12, 6)[T.i]
    rm = HALO_R
    hw, hh = HALO_W * .5, HALO_H * .5
    c = .06 if T.bevel else 0.0
    sec = [(hw - c, -hh), (hw, -hh + c), (hw, hh - c), (hw - c, hh), (-hw + c, hh), (-hw, hh - c), (-hw, -hh + c), (-hw + c, -hh)]
    if c == 0:
        sec = [(hw, -hh), (hw, hh), (-hw, hh), (-hw, -hh)]
    rings = []
    for i in range(n + 1):
        a = -ah + 2 * ah * i / n
        rings.append([polar(rm + x, a, y, rm) for (x, y) in sec])
    m = len(sec)
    for i in range(n):
        for j in range(m):
            j1 = (j + 1) % m
            pts = (rings[i][j], rings[i][j1], rings[i + 1][j1], rings[i + 1][j])
            # outward normal from the section's edge, turned to this angle
            x0, y0 = sec[j]
            x1, y1 = sec[j1]
            en = _n((y1 - y0, -(x1 - x0), 0))  # (radial, vertical)
            a0 = -ah + 2 * ah * i / n
            a1 = -ah + 2 * ah * (i + 1) / n

            def turn(a):
                return _n((math.sin(a) * en[0], en[1], math.cos(a) * en[0]))
            fin = FIN['obsidian'] if en[1] < .7 else FIN['obsidian']
            g.face(pts, fin, normals=(turn(a0), turn(a0), turn(a1), turn(a1)))
    for i, s in ((0, -1), (n, 1)):
        ring = rings[i]
        a = -ah + 2 * ah * i / n
        g.face(ring if s > 0 else ring[::-1], FIN['obsidian-dark'], (s * math.cos(a), 0, -s * math.sin(a)))
    # the inner channel of light, the lines underneath, the gold cap strip
    for i in range(n):
        a0 = -ah + 2 * ah * i / n
        a1 = -ah + 2 * ah * (i + 1) / n
        ni0, ni1 = _n((-math.sin(a0), 0, -math.cos(a0))), _n((-math.sin(a1), 0, -math.cos(a1)))
        r = rm - hw - .004
        g.face((polar(r, a0, -.06, rm), polar(r, a1, -.06, rm), polar(r, a1, .06, rm), polar(r, a0, .06, rm)), FIN['crown'], normals=(ni0, ni1, ni1, ni0))
        for rr in (rm - .22, rm + .22):
            y = -hh - .004
            g.face((polar(rr - .015, a0, y, rm), polar(rr - .015, a1, y, rm), polar(rr + .015, a1, y, rm), polar(rr + .015, a0, y, rm)), FIN['vein-soft'], (0, -1, 0))
        y = hh + .004
        g.face((polar(rm - .14, a0, y, rm), polar(rm - .14, a1, y, rm), polar(rm + .14, a1, y, rm), polar(rm + .14, a0, y, rm)), FIN['gold'], (0, 1, 0))
    if T.detail:
        ribs = 12
        for i in range(ribs):
            a = -ah + 2 * ah * (i + .5) / ribs
            out = (math.sin(a), 0, math.cos(a))
            tan = (math.cos(a), 0, -math.sin(a))
            centre = polar(rm + hw + .07, a, 0, rm)
            t = Geo()
            sk.box(t, -.05, .05, -hh * .8, hh * .8, -.07, .07, FIN['gold'], T, bevel=.012, cell=0)
            rot = math.atan2(out[0], out[2])
            q = sk.quat_axis(0, 1, 0, rot)
            g.extend(t.transformed(sk.affine(q, centre)))
    return g


def halo_wire(T, w):
    g = Geo()
    ah = math.pi / HALO_ARCS - HALO_GAP * .5 / HALO_R
    n = (16, 10, 6)[T.i]
    hw, hh = HALO_W * .5, HALO_H * .5
    corners = [(hw, -hh), (hw, hh), (-hw, hh), (-hw, -hh)]
    for (x, y) in corners:
        poly_wire(g, [polar(HALO_R + x, -ah + 2 * ah * i / n, y, HALO_R) for i in range(n + 1)], w, FIN['wire'], closed=False)
    for a in (-ah, ah):
        poly_wire(g, [polar(HALO_R + x, a, y, HALO_R) for (x, y) in corners], w, FIN['wire'])
    return g


SPIRES = {
    # name: [(half width, y0, y1, cham)] stacked from the foot
    'spire-a': [(3.0, 0, 8, .3), (2.2, 8, 30, .25), (1.6, 30, 48, .2), (.9, 48, 58, .12)],
    'spire-b': [(6.0, 0, 5, .4), (4.6, 5, 11, .35), (3.4, 11, 17, .3), (2.3, 17, 23, .25), (1.2, 23, 30, .15)],
    'spire-c': [(2.2, 0, 9, .2), (1.8, 11.5, 19, .2), (1.3, 21.5, 26, .15)],
}


def spire(name, T):
    g = Geo()
    for k, (hw, y0, y1, c) in enumerate(SPIRES[name]):
        frustum(g, hw * 2, hw * 1.8, y0, y1, FIN['obsidian'], cham=c, levels=max(1, int((y1 - y0) / 6)))
        frustum(g, hw * 1.86, hw * 1.86, y1 - .3, y1, FIN['gold'], cham=c, caps=(False, True))
        for n in ((0, 0, -1), (1, 0, 0), (0, 0, 1), (-1, 0, 0)):
            u = (0, (y1 - y0) * .42, 0)
            across = (1, 0, 0) if n[2] else (0, 0, 1)
            centre = (n[0] * hw * .95, (y0 + y1) * .5, n[2] * hw * .95)
            rect_on(g, centre, _a((0, 0, 0), across, .05), u, n, FIN['vein'], .02)
    top = SPIRES[name][-1]
    rect_on(g, (0, top[2] + .02, 0), (.3, 0, 0), (0, 0, .3), (0, 1, 0), FIN['crown'], .01)
    return g


def shard(k, T):
    """An obsidian shard: a jittered, squashed icosphere about 1 m across, flat shaded."""
    g = Geo()
    t = (1 + 5 ** .5) / 2
    V = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t), (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    F = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    sx, sy, sz = ((.55, .9, .4), (.7, .45, .5), (.4, 1.1, .35), (.6, .6, .6))[k]
    P = []
    for i, v in enumerate(V):
        l = math.sqrt(v[0] ** 2 + v[1] ** 2 + v[2] ** 2)
        j = .7 + .5 * hashf(k * 31 + 5, i)
        P.append((v[0] / l * j * sx, v[1] / l * j * sy, v[2] / l * j * sz))
    for i, (a, b, c) in enumerate(F):
        fin = FIN['obsidian'] if hashf(k, i, 3) > .15 else FIN['vein-soft']
        g.face((P[a], P[b], P[c]), fin)
    return g


def grid(T):
    """The void's grid: lines every 2 m (4 m on Low) out to 80 m, wider and dimmer with distance, every fifth one brighter."""
    g = Geo()
    step = (2.0, 2.0, 4.0)[T.i]
    seg = (4.0, 6.0, 8.0)[T.i]
    reach = 80.0
    lines = int(reach / step)
    for axis in range(2):
        for li in range(-lines, lines + 1):
            c = li * step
            major = li % 5 == 0
            x = -reach
            while x < reach - 1e-6:
                x1 = min(reach, x + seg)
                mid = (x + x1) * .5
                dist = math.sqrt(mid * mid + c * c)
                fade = max(0.0, 1 - dist / reach)
                if fade <= .02:
                    x = x1
                    continue
                w = (.014 + dist * .0011) * (1.6 if major else 1.0)
                fin = FIN['grid'] if major else FIN['vein-soft']
                # fade is carried into the finish by splitting into bands: three levels are enough at these sizes
                if not major and fade < .45:
                    fin = FIN['old-grid']
                if axis == 0:
                    pts = ((c - w, 0, x), (c + w, 0, x), (c + w, 0, x1), (c - w, 0, x1))
                else:
                    pts = ((x, 0, c - w), (x1, 0, c - w), (x1, 0, c + w), (x, 0, c + w))
                g.face(pts, fin, (0, 1, 0))
                x = x1
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The kit
# ---------------------------------------------------------------------------------------------------------------------------
def kit(T):
    """[(name, solid Geo, wire Geo or None)] for tier T (the bank keeps Fine as "<name>" and Low as "<name>/low")."""
    w = (.022, .03, .045)[T.i]
    out = [('old-plate', old_plate(T), old_plate_wire(T, w)), ('old-dais-wedge', old_dais_wedge(T), old_dais_wire(T, w)),
           ('old-pylon', old_pylon(T), old_pylon_wire(T, w))]
    for k in range(4):
        out.append(('old-skyline-%d' % k, old_skyline(k, T), old_skyline(k, T, True, w * 3)))
    out.append(('dais', dais(T), dais_wire(T, w)))
    for k in range(3):
        out.append(('tile-%d' % (k + 1), ring_tile(k, T), ring_tile_wire(k, T, w)))
    out.append(('mono-plinth', mono_base(T), mono_base_wire(T, w * 1.4)))
    out.append(('mono-obelisk', mono_obelisk(T), mono_obelisk_wire(T, w * 1.4)))
    out.append(('crown-arc', halo_arc(T), halo_wire(T, w * 2)))
    if T.i == 0:
        for name in SPIRES:
            out.append((name, spire(name, T), None))
        for k in range(4):
            out.append(('shard-' + 'abcd'[k], shard(k, T), None))
    out.append(('grid', grid(T), None))
    return out


def bake(items, T, no_ao):
    """Each piece's colours: its occlusion baked by Cycles with every piece laid out apart (none shades another)."""
    geos = [it[1] for it in items]
    ao = [None] * len(geos)
    if not no_ao:
        moved = []
        for k, g in enumerate(geos):
            moved.append(g.transformed([1, 0, 0, 120.0 * k, 0, 1, 0, 0, 0, 0, 1, 0]))
        got = sk.bake_ao(moved, distance=(.6, .6, .7)[T.i], samples=(24, 12, 8)[T.i])
        ao = [1.0 - (1.0 - a) * .8 for a in got]
    out = []
    for k, g in enumerate(geos):
        P, N, S, F = g.arrays()
        world = P.astype(np.float64) + np.array([17.0 * k, 0, 9.0 * k])
        out.append(sk.Bake(g, ao[k], world=world, floors=(0.0, -.12), seed=101 + k))
    return out


def plain(g):
    return sk.Bake(g, None, floors=(), seed=3)


VIEWS = [('old-arena', (2.5, 1.2, 9.5), (0, .9, 0), 55), ('new-wide', (0, 6, 24), (0, 6, 0), 55), ('new-low', (2.2, .5, 5.5), (0, 3.5, -4), 75),
         ('crown-up', (1, 1.5, 3), (0, 15, -2), 80), ('top', (0, 40, .1), (0, 0, 0), 60), ('mono-close', (6.2, 2, 5.4), (9.3 * .38, 3, 9.3 * .92), 50),
         ('floor-close', (1.2, 1.6, 3.4), (0, 0, 1), 60), ('old-top', (0, 14, 6), (0, 0, 0), 60), ('hero', (1.3, .9, 4.6), (0, 2.4, 0), 52), ('void', (6, 3, 14), (0, 1, 0), 60)]
FLOOR = .012
GROUND = FLOOR - DAIS_H


def layout(new=True):
    """The pieces where Core NativeIntroArchitect rests them: [(name, x, y, z, yaw)] (yaw about +y; scale 1)."""
    out = []
    if not new:
        for i in range(-2, 2):
            for j in range(-2, 2):
                out.append(('old-plate', i * PLATE + PLATE / 2, GROUND, j * PLATE + PLATE / 2, 0.0))
        for k in range(6):
            out.append(('old-dais-wedge', 0.0, FLOOR, 0.0, math.radians(60 * k)))
        for i in range(12):
            a = (i + .5) / 12 * 2 * math.pi
            out.append(('old-pylon', math.sin(a) * 7.5, GROUND, math.cos(a) * 7.5, a))
        for k in range(4):
            out.append(('old-skyline-%d' % k, 0.0, GROUND, 0.0, 0.0))
        return out
    out.append(('dais', 0.0, FLOOR, 0.0, 0.0))
    for k, (r0, r1, count) in enumerate(RINGS):
        rm = (r0 + r1) * .5
        for i in range(count):
            a = (i + (.5 if k == 1 else 0)) / count * 2 * math.pi
            out.append(('tile-%d' % (k + 1), math.sin(a) * rm, FLOOR, math.cos(a) * rm, a))
    for i in range(8):
        a = (i + .5) / 8 * 2 * math.pi
        x, z = math.sin(a) * 9.3, math.cos(a) * 9.3
        out.append(('mono-plinth', x, FLOOR + .6, z, a))
        out.append(('mono-obelisk', x, FLOOR + .6, z, a))
    for i in range(HALO_ARCS):
        a = (i + .5) / HALO_ARCS * 2 * math.pi
        out.append(('crown-arc', math.sin(a) * HALO_R, CROWN_Y, math.cos(a) * HALO_R, a))
    for i in range(6):
        a = (i + .3 * hashf(5, i)) / 6 * 2 * math.pi + .4
        r = 48 + 22 * hashf(6, i)
        out.append((('spire-a', 'spire-b', 'spire-c')[i % 3], math.sin(a) * r, -26 + 12 * hashf(7, i), math.cos(a) * r, a))
    out.append(('grid', 0.0, FLOOR - .42, 0.0, 0.0))
    return out


def layout_preview(T, items):
    """The two worlds laid out as Core lays them, for the preview renders (the old arena at x - 60, the architect's at x 0)."""
    byname = {it[0]: it[1] for it in items}
    old, new = Geo(), Geo()
    for dst, isnew in ((old, False), (new, True)):
        for (name, x, y, z, yaw) in layout(isnew):
            if name in byname:
                dst.extend(byname[name].transformed(sk.affine(sk.quat_axis(0, 1, 0, yaw), (x, y, z))))
    oldg = old.transformed([1, 0, 0, -60, 0, 1, 0, 0, 0, 0, 1, 0])
    return [(oldg, plain(oldg)), (new, plain(new))]


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', help='the folder the bank is written into (as <out>/ChipArenaLocal/Resources/<bank>.bytes)')
    ap.add_argument('--bank', help='also copy the bank to this file')
    ap.add_argument('--preview', help='render the laid-out worlds of the preview tier into this folder')
    ap.add_argument('--preview-tier', type=int, default=0)
    ap.add_argument('--views', default='')
    ap.add_argument('--tiers', default='0,2')
    ap.add_argument('--no-ao', action='store_true')
    args = ap.parse_args(argv)
    finishes()
    sb = sk.StageBank()
    sb.far_layers = False
    t0 = time.time()
    shared = {}
    for ti in [int(x) for x in args.tiers.split(',')]:
        T = sk.Tier(ti)
        items = kit(T)
        bakes = bake(items, T, args.no_ao)
        suffix = '' if ti == 0 else '/low'
        for (name, g, wire), bk in zip(items, bakes):
            sb.add(name + suffix, 'kit', sk.to_mesh(name + suffix, g, bk))
            if wire is not None:
                sb.add(name + '.wire' + suffix, 'kit', sk.to_mesh(name + '.wire' + suffix, wire, plain(wire)))
        print('tier %s: %d pieces, %d faces, %.1f s' % (T.name, len(items), sum(it[1].faces for it in items), time.time() - t0))
        if args.preview and ti == args.preview_tier:
            views = [v for v in VIEWS if not args.views or v[0] in args.views.split(',')]
            scene = layout_preview(T, items)
            vv = []
            for (name, eye, tgt, fov) in views:
                if name.startswith('old-'):
                    vv.append((name, (eye[0] - 60, eye[1], eye[2]), (tgt[0] - 60, tgt[1], tgt[2]), fov))
                else:
                    vv.append((name, eye, tgt, fov))
            sk.preview(scene, vv, args.preview)
    if args.out:
        path = sb.write(args.out, BANK)
        if args.bank:
            os.makedirs(os.path.dirname(args.bank), exist_ok=True)
            shutil.copyfile(path, args.bank)
            print('copied to', args.bank)
    else:
        for (group, tier), (n, tris, verts) in sorted(sb.stats.items()):
            print('bank %-7s %-5s %4d props %8d triangles %8d vertices' % (group, tier, n, tris, verts))
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
