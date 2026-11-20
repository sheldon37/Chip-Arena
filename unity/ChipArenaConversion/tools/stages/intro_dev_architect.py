"""DEV · Architect of Reality's set (docs/INTRO-DEV-ARCHITECT.md "The set"): the owner's dev intro's modelled kit, every piece
in its own frame, written into the local bank ChipArenaIntroDev.bytes (never committed). Core NativeIntroArchitect places,
flies, dissolves and builds the pieces (its kit layout is this file's numbers: keep the two in step).

Run inside Blender 5.x, headless:
  blender -b --factory-startup --python-exit-code 1 --python tools/stages/intro_dev_architect.py -- --out <dir> [--preview <dir>]
`--out` names the folder the bank is written to (the stage kit writes <out>/ChipArenaLocal/Resources/<bank>.bytes); `--bank`
names a file to copy it to as well.

Two worlds share the kit:
- the old arena (the entrance stage every intro plays on, modelled): deck plates on a 2 m grid, the hexagonal dais in six wedges,
  twelve neon pylons 7.5 m out, the dark skyline's towers. It comes apart.
- the architect's: a floating platform of black glass-smooth obsidian (a sixteen-sided dais and sixteen wedges to 7.4 m, each
  cut once across at a staggered radius, so no seam runs round the floor: owner, no ring outlines), its sixteen radial seams lit
  from cyan at the dais to gold at the rim and carried into the dais's sigil as one sunburst; eight monoliths 9.3 m out on
  floating plinths; the crown (eight gold spires leaning in over the platform toward one point: rays, not a ring), the far
  spires, shards, and the void's grid. It is built.
Every solid piece has a `.wire` twin: its edges as thin glowing bars (the wireframe a piece falls away to, and is drawn in).
The obsidian's baked occlusion is pressed low (OCCLUDED) so the arena's reflection probe, which the intro's set reads wherever the
match is staged, cannot wash the black glass grey: its gloss is the intro's own lights' highlights.

Frames (Unity space: metres, +y up; a piece's frame is where Core places it):
  old-plate          a 2 m deck plate's top centre                 old-dais-wedge   the hexagon's centre at its top; the wedge
                                                                                     toward +z (corners at +-30 degrees)
  old-pylon-base     the base's bottom centre                      old-pylon-shaft  the shaft's bottom centre (its neon on -z)
  old-tower-a/b/c    the tower's foot (its windows face -z)        dais             the dais's top centre (its keel below; its
                                                                                     corners on the seams, at +-11.25 degrees)
  slab-P S           a floor slab's top at its middle apothem: P 0 the inner slab, 1 the outer; S the cut (0 at SPLITS[0],
                     1 at SPLITS[1]); the platform's centre at z = -rm, the wedge toward +z, its seams at +-11.25 degrees
  mono-base          the plinth's top centre (a shaft stands on it) mono-shaft-0..3 the segment's bottom centre
  mono-cap           the capstone's bottom centre                  crown-spire      the spire's root (its point SPIRE_L up +y,
                                                                                     its ridges toward +-z)
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

# ---- the layout's numbers (Core NativeIntroArchitect's kit) -----------------------------------------------------------
PLATE = 4.0
PLATE_T = .12
HEX_R = 2.6                      # the old dais's circumradius (NativeEntranceStage.DaisRadius)
DAIS_H = .132                    # FloorY - GroundY
PYLON_H = 3.6
# the floor: sixteen wedges round a sixteen-sided dais; each wedge cut once across at SPLITS (apothems), alternating round the
# floor, so no cut lines up with its neighbours' into a ring (owner: no ring outlines)
WEDGES = 16
WEDGE_HALF = math.pi / WEDGES     # 11.25 degrees: the seams lie at (k + .5) x 22.5 degrees
DAIS_R = 1.50                     # the dais's circumradius (its corners on the seams)
FLOOR_R = 7.40                    # the rim's circumradius (its corners on the seams)
SPLITS = (3.50, 4.60)
SLAB_T = .40
SEAM = .04                        # a radial seam's width (lit: its strip fills it)
CUT = .016                        # the cuts' and the dais's joints (dark)
SEAM_STEPS = 12                   # the seam's colour from cyan at the dais to gold at the rim, in this many steps
MONO_W = (1.10, 1.00, .90, .80, .70)
MONO_SEG = 2.4
CAP_H = 1.6
# the crown: eight gold spires, each rooted above a monolith and leaning in toward one point over the platform (Core places them)
SPIRE_L, SPIRE_W, SPIRE_D = 9.0, .46, .17
# the black glass's occlusion scale: the env reflection (the arena's probe) is occluded by the vertex alpha, the lights' highlights
# are not, so the obsidian stays black and glossy on every map (the gold keeps some of the probe: a metal needs a reflection)
OCCLUDED = {'obsidian': .06, 'obsidian-dark': .06, 'basalt': .2, 'basalt-dark': .25, 'gold': .55}

FIN = {}


def apothem(r):
    return r * math.cos(WEDGE_HALF)


def slab_span(part, split):
    """A slab's inner and outer apothems: part 0 the inner slab (the dais to the cut), 1 the outer (the cut to the rim)."""
    hs = SPLITS[split]
    return (apothem(DAIS_R), hs) if part == 0 else (hs, apothem(FLOOR_R))


def _hexlerp(a, b, u):
    ca, cb = sk.srgb(a), sk.srgb(b)
    c = [ca[i] + (cb[i] - ca[i]) * u for i in range(3)]
    return (int(round(c[0] * 255)) << 16) | (int(round(c[1] * 255)) << 8) | int(round(c[2] * 255))


def seam_colour(u):
    """The seams' light at share u of the way from the dais (0) to the rim (1): cyan, through white light, to gold."""
    u = max(0.0, min(1.0, u))
    return _hexlerp(0x6feaff, 0xeefaff, u / .45) if u < .45 else _hexlerp(0xeefaff, 0xffc25a, (u - .45) / .55)


def finishes():
    f = sk.fin
    D = sk
    for k in range(SEAM_STEPS):
        FIN['seam-%d' % k] = f('seam-%d' % k, seam_colour(k / (SEAM_STEPS - 1)), D.DETAIL_GLOW, glow=2.6)
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
        # the architect's: black glass-smooth obsidian (its gloss the lights' highlights: OCCLUDED), basalt, gold, and light
        'obsidian': f('obsidian', 0x08090c, D.DETAIL_NONE, smooth=.66, metal=0.0, dirt=.04, wear=0.0),
        'obsidian-dark': f('obsidian-dark', 0x040405, D.DETAIL_NONE, smooth=.72, metal=0.0, dirt=.04),
        'basalt': f('basalt', 0x0c0d11, D.DETAIL_CARVED, smooth=.34, metal=.05, dirt=.2, wear=.08),
        'basalt-dark': f('basalt-dark', 0x07080b, D.DETAIL_CARVED, smooth=.28, dirt=.15),
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


def inset2(poly, dists):
    """A convex outline [(x, z)] moved in by dists[i] along each edge i (from corner i to corner i + 1): its new corners."""
    m = len(poly)
    cx = sum(p[0] for p in poly) / m
    cz = sum(p[1] for p in poly) / m
    lines = []
    for i in range(m):
        a, b = poly[i], poly[(i + 1) % m]
        dx, dz = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dz)
        nx, nz = -dz / L, dx / L
        if (cx - a[0]) * nx + (cz - a[1]) * nz < 0:
            nx, nz = -nx, -nz
        lines.append(((a[0] + nx * dists[i], a[1] + nz * dists[i]), (dx / L, dz / L)))
    out = []
    for i in range(m):
        (p0, d0), (p1, d1) = lines[i - 1], lines[i]
        den = d0[0] * d1[1] - d0[1] * d1[0]
        if abs(den) < 1e-12:
            out.append(p1)
            continue
        t = ((p1[0] - p0[0]) * d1[1] - (p1[1] - p0[1]) * d1[0]) / den
        out.append((p0[0] + d0[0] * t, p0[1] + d0[1] * t))
    return out


def _bilerp(q, u, v):
    """A point in the quad q (corners 0 inner-left, 1 inner-right, 2 outer-right, 3 outer-left): u across, v out."""
    a = (q[0][0] + (q[1][0] - q[0][0]) * u, q[0][1] + (q[1][1] - q[0][1]) * u)
    b = (q[3][0] + (q[2][0] - q[3][0]) * u, q[3][1] + (q[2][1] - q[3][1]) * u)
    return (a[0] + (b[0] - a[0]) * v, a[1] + (b[1] - a[1]) * v)


def quad_slab(g, q, y0, y1, top_f, side_f, bot_f, T, bevel=.025, sub=(2, 3)):
    """A slab over the convex quad q [(x, z)] (0 inner-left, 1 inner-right, 2 outer-right, 3 outer-left): its top at y1 split into
    sub[0] x sub[1] cells (the colours' and occlusion's resolution), its top edge chamfered by `bevel`, sides down to y0, a floor."""
    b = bevel if T.bevel else 0.0
    top = inset2(q, [b] * 4) if b > 0 else list(q)
    nu, nv = sub
    for i in range(nu):
        for j in range(nv):
            c = [_bilerp(top, i / nu, j / nv), _bilerp(top, (i + 1) / nu, j / nv), _bilerp(top, (i + 1) / nu, (j + 1) / nv), _bilerp(top, i / nu, (j + 1) / nv)]
            g.face([(p[0], y1, p[1]) for p in c], top_f, (0, 1, 0))
    m = len(q)
    cx = sum(p[0] for p in q) / m
    cz = sum(p[1] for p in q) / m
    for i in range(m):
        a, c = q[i], q[(i + 1) % m]
        dx, dz = c[0] - a[0], c[1] - a[1]
        L = math.hypot(dx, dz)
        nx, nz = dz / L, -dx / L
        if (a[0] - cx) * nx + (a[1] - cz) * nz < 0:
            nx, nz = -nx, -nz
        if b > 0:
            ta, tc = top[i], top[(i + 1) % m]
            g.face(((ta[0], y1, ta[1]), (tc[0], y1, tc[1]), (c[0], y1 - b, c[1]), (a[0], y1 - b, a[1])), side_f, _n((nx, 1.0, nz)))
        g.face(((a[0], y1 - b, a[1]), (c[0], y1 - b, c[1]), (c[0], y0, c[1]), (a[0], y0, a[1])), side_f, (nx, 0.0, nz))
    g.face([(p[0], y0, p[1]) for p in q][::-1], bot_f, (0, -1, 0))


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
def dais_corner(k, r):
    """Corner k of the dais (and of the floor's sixteen-sided outlines) at circumradius r: on seam k, (k + .5) x 22.5 degrees."""
    a = (k + .5) * 2 * WEDGE_HALF
    return (r * math.sin(a), r * math.cos(a))


def dais(T):
    """The dais the architect hovers over: a sixteen-sided slab of black glass, its corners on the floor's seams, a gold star at
    its middle and sixteen rays of light from the star to its corners (the floor's seams run on from them: one sunburst, no
    rings), and a keel of dark stone hanging under it into the void."""
    g = Geo()
    b = .03 if T.bevel else 0.0
    R = DAIS_R - CUT * .5 / math.cos(WEDGE_HALF)
    top = [dais_corner(k, R - b / math.cos(WEDGE_HALF)) for k in range(WEDGES)]
    rim = [dais_corner(k, R) for k in range(WEDGES)]
    # the top: bands of quads from the rim in (the colours' resolution), a fan in the middle
    rr = (1.0, .66, .36, .14)
    for k in range(len(rr) - 1):
        for i in range(WEDGES):
            j = (i + 1) % WEDGES
            pa, pb = top[i], top[j]
            g.face(((pa[0] * rr[k], 0.0, pa[1] * rr[k]), (pb[0] * rr[k], 0.0, pb[1] * rr[k]), (pb[0] * rr[k + 1], 0.0, pb[1] * rr[k + 1]),
                    (pa[0] * rr[k + 1], 0.0, pa[1] * rr[k + 1])), FIN['obsidian'], (0, 1, 0))
    g.face([(p[0] * rr[-1], 0.0, p[1] * rr[-1]) for p in top], FIN['obsidian'], (0, 1, 0))
    # the sides: the chamfer and the face down to the slab's foot, flat per side
    for i in range(WEDGES):
        j = (i + 1) % WEDGES
        mid = ((rim[i][0] + rim[j][0]) * .5, (rim[i][1] + rim[j][1]) * .5)
        n = _n((mid[0], 0.0, mid[1]))
        if b > 0:
            g.face(((top[i][0], 0.0, top[i][1]), (top[j][0], 0.0, top[j][1]), (rim[j][0], -b, rim[j][1]), (rim[i][0], -b, rim[i][1])), FIN['basalt'], _n((n[0], 1.0, n[2])))
        g.face(((rim[i][0], -b, rim[i][1]), (rim[j][0], -b, rim[j][1]), (rim[j][0], -SLAB_T, rim[j][1]), (rim[i][0], -SLAB_T, rim[i][1])), FIN['basalt'], n)
    g.face([(p[0], -SLAB_T, p[1]) for p in rim][::-1], FIN['basalt-dark'], (0, -1, 0))
    # the gold star at the middle: sixteen points on the rays, a thin plate
    star = []
    for k in range(WEDGES * 2):
        a = (k * .5 + .5) * 2 * WEDGE_HALF
        r = .30 if k % 2 == 0 else .13
        star.append((r * math.sin(a), .014, r * math.cos(a)))
    for k in range(len(star)):
        p0, p1 = star[k], star[(k + 1) % len(star)]
        g.face(((0.0, .018, 0.0), p0, p1), FIN['gold'], (0, 1, 0))
        mid = ((p0[0] + p1[0]) * .5, 0.0, (p0[2] + p1[2]) * .5)
        g.face((p0, p1, (p1[0], .0, p1[2]), (p0[0], .0, p0[2])), FIN['gold'], _n(mid))
    # the rays: sixteen strips of light from the star to the corners, in the seams' first colour
    for k in range(WEDGES):
        a = (k + .5) * 2 * WEDGE_HALF
        r0, r1 = .3, R - .005
        c = polar((r0 + r1) * .5, a, 0)
        u = (math.sin(a) * (r1 - r0) * .5, 0.0, math.cos(a) * (r1 - r0) * .5)
        v = (math.cos(a) * SEAM * .5, 0.0, -math.sin(a) * SEAM * .5)
        rect_on(g, c, u, v, (0, 1, 0), FIN['seam-0'], .003)
    # the keel: an inverted cone of dark stone under the slab, veins of light down it under every other ray
    sk.lathe(g, [(.02, -2.4), (.5, -1.5), (1.05, -.75), (R * .96, -SLAB_T)], (0, 0, 0), FIN['basalt-dark'], (32, 16, 16)[T.i])
    for i in range(8):
        a = (2 * i + .5) * 2 * WEDGE_HALF
        pts = [polar(R * .96 - .02, a, -SLAB_T - .02), polar(1.05, a, -.77), polar(.5, a, -1.52), polar(.04, a, -2.38)]
        for k in range(3):
            p0, p1 = pts[k], pts[k + 1]
            d = _s(p1, p0)
            nrm = _n((math.sin(a), .6, math.cos(a)))
            side = _n(_x(d, nrm))
            g.face((_a(_a(p0, side, -.018), nrm, .01), _a(_a(p0, side, .018), nrm, .01), _a(_a(p1, side, .018), nrm, .01), _a(_a(p1, side, -.018), nrm, .01)), FIN['vein-soft'], nrm)
    return g


def dais_wire(T, w):
    """The dais's wireframe: its sixteen rays and its keel's lines (no rim: no ring outlines)."""
    g = Geo()
    R = DAIS_R - CUT * .5 / math.cos(WEDGE_HALF)
    for k in range(WEDGES):
        a = (k + .5) * 2 * WEDGE_HALF
        bar(g, polar(.05, a, 0), polar(R, a, 0), w, FIN['wire'])
        if k % 2 == 0:
            bar(g, polar(R * .96, a, -SLAB_T), polar(.02, a, -2.4), w, FIN['wire'])
    return g


def slab_quad(part, split):
    """A floor slab's outline in its frame (the platform's centre at z = -rm), after and before its joints: (inset, full, rm)."""
    h0, h1 = slab_span(part, split)
    rm = (h0 + h1) * .5
    t = math.tan(WEDGE_HALF)
    q = [(-h0 * t, h0 - rm), (h0 * t, h0 - rm), (h1 * t, h1 - rm), (-h1 * t, h1 - rm)]
    return inset2(q, [CUT * .5, SEAM * .5, CUT * .5 if part == 0 else 0.0, SEAM * .5]), q, rm


def floor_slab(part, split, T):
    """A slab of the floor: the inner (part 0: the dais to the cut) or the outer (1: the cut to the rim) piece of a wedge whose cut
    lies at SPLITS[split], black glass on top, basalt sides, the light of its left seam filling the seam (each seam is lit once),
    graded from cyan at the dais to gold at the rim, and a line of code glyphs across it (Fine and Mid)."""
    g = Geo()
    q, full, rm = slab_quad(part, split)
    h0, h1 = slab_span(part, split)
    quad_slab(g, q, -SLAB_T, 0.0, FIN['obsidian'], FIN['basalt'], FIN['basalt-dark'], T, bevel=.02, sub=((2, 4), (2, 2), (1, 1))[T.i])
    # the left seam's light, filling the seam from the inner chord (or the dais's corner) to the outer one (or the cut, meeting
    # the next slab's), in pieces of about half a metre, each its own step of the cyan-to-gold grade
    t = math.tan(WEDGE_HALF)
    e0, e1 = (-h0 * t, h0 - rm), (-h1 * t, h1 - rm)
    dx, dz = e1[0] - e0[0], e1[1] - e0[1]
    L = math.hypot(dx, dz)
    nx, nz = dz / L * SEAM * .5, -dx / L * SEAM * .5
    pieces = max(1, int(round(L / .5)))
    for i in range(pieces):
        u0, u1 = i / pieces, (i + 1) / pieces
        a = (e0[0] + dx * u0, e0[1] + dz * u0)
        c = (e0[0] + dx * u1, e0[1] + dz * u1)
        r_mid = (h0 + (h1 - h0) * (u0 + u1) * .5) / math.cos(WEDGE_HALF)
        k = int(round((r_mid - DAIS_R) / (FLOOR_R - DAIS_R) * (SEAM_STEPS - 1)))
        k = max(0, min(SEAM_STEPS - 1, k))
        y = -.002
        g.face(((a[0] - nx, y, a[1] - nz), (c[0] - nx, y, c[1] - nz), (c[0] + nx, y, c[1] + nz), (a[0] + nx, y, a[1] + nz)), FIN['seam-%d' % k], (0, 1, 0))
    if T.detail:
        # a line of code glyphs across the slab's middle, in a dark bezel
        cell = (.03, .03, .028)[T.i]
        cols = 3 if (h1 - h0) < 2.5 else 4
        width = cols * 4 * cell
        z_c = (h0 + h1) * .5 - rm + (h1 - h0) * .08
        centre = (0.0, 0.0, z_c)
        origin = (-width * .5, 0.0, z_c + 5 * cell * .5)
        rect_on(g, centre, (width * .5 + cell, 0, 0), (0, 0, 3.2 * cell), (0, 1, 0), FIN['obsidian-dark'], .002)
        engrave(g, origin, (cell, 0, 0), (0, 0, -cell), (0, 1, 0), cols, 1, cell, FIN['code'], 31 + part * 7 + split * 3, lift=.004, fill=1.0)
    return g


def floor_slab_wire(part, split, T, w):
    """A slab's wireframe: its radial edges, top and bottom, and its corners' uprights. No chords: laid side by side, the slabs'
    chords would draw polygons round the floor (owner: no ring outlines)."""
    g = Geo()
    q, full, rm = slab_quad(part, split)
    for y in (-SLAB_T, 0.0):
        bar(g, (q[1][0], y, q[1][1]), (q[2][0], y, q[2][1]), w, FIN['wire'])
        bar(g, (q[3][0], y, q[3][1]), (q[0][0], y, q[0][1]), w, FIN['wire'])
    for p in q:
        bar(g, (p[0], -SLAB_T, p[1]), (p[0], 0.0, p[1]), w, FIN['wire'])
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


def crown_spire(T):
    """One of the crown's eight gold spires (no ring: they lean in toward one point over the platform, rays round the architect):
    a blade of diamond section from its root (the frame's origin) to its point SPIRE_L up +y, its ridges toward +-z, a vein of
    light along both ridges, a square obsidian collar at the root with a gold band, and a crystal of light at the point."""
    g = Geo()
    lv = (10, 6, 3)[T.i]

    def sec(u):
        k = (1 - u) ** .9
        y = SPIRE_L * u
        return [(SPIRE_W * k, y, 0.0), (0.0, y, SPIRE_D * k), (-SPIRE_W * k, y, 0.0), (0.0, y, -SPIRE_D * k)]
    rings = [sec(k / lv) for k in range(lv)]
    tip = (0.0, SPIRE_L, 0.0)
    for k in range(lv):
        r0 = rings[k]
        r1 = rings[k + 1] if k + 1 < lv else None
        for i in range(4):
            j = (i + 1) % 4
            pts = (r0[i], r0[j], tip) if r1 is None else (r0[i], r0[j], r1[j], r1[i])
            mid = ((r0[i][0] + r0[j][0]) * .5, 0.0, (r0[i][2] + r0[j][2]) * .5)
            nrm = _x(_s(r0[j], r0[i]), _s(pts[-1], r0[i]))
            if nrm[0] * mid[0] + nrm[2] * mid[2] < 0:
                nrm = (-nrm[0], -nrm[1], -nrm[2])
            g.face(pts, FIN['gold'], _n(nrm))
    g.face(rings[0][::-1], FIN['gold'], (0, -1, 0))
    # the veins along the two ridges (z = +-depth), standing a hair proud of them
    for s_ in (1, -1):
        for k in range(lv - 1):
            u0, u1 = max(.04, k / lv), min(.9, (k + 1) / lv)
            if u1 <= u0:
                continue
            d0, d1 = SPIRE_D * (1 - u0) ** .9 + .004, SPIRE_D * (1 - u1) ** .9 + .004
            w0, w1 = .022 * (1 - u0 * .6), .022 * (1 - u1 * .6)
            g.face(((-w0, SPIRE_L * u0, s_ * d0), (w0, SPIRE_L * u0, s_ * d0), (w1, SPIRE_L * u1, s_ * d1), (-w1, SPIRE_L * u1, s_ * d1)), FIN['crown'], (0, 0, s_))
    # the collar: a square block of obsidian round the root, a gold band at its top
    box(g, -.34, .34, -.62, .2, -.24, .24, FIN['obsidian'], T, bevel=.03)
    box(g, -.36, .36, .14, .24, -.26, .26, FIN['gold'], T, bevel=.012)
    # the crystal at the point
    c = SPIRE_L + .02
    s = .09
    pts = [(s, c - .1, 0), (0, c - .1, s), (-s, c - .1, 0), (0, c - .1, -s)]
    for i in range(4):
        g.face((pts[i], pts[(i + 1) % 4], (0, c + .16, 0)), FIN['crown'])
        g.face((pts[(i + 1) % 4], pts[i], (0, c - .3, 0)), FIN['crown'])
    return g


def crown_spire_wire(T, w):
    g = Geo()
    for (x, z) in ((SPIRE_W, 0.0), (0.0, SPIRE_D), (-SPIRE_W, 0.0), (0.0, -SPIRE_D)):
        bar(g, (x, 0.0, z), (0.0, SPIRE_L, 0.0), w, FIN['wire'])
    box_wire(g, -.34, .34, -.62, .2, -.24, .24, w, FIN['wire'])
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
    for part in (0, 1):
        for split in (0, 1):
            out.append(('slab-%d%d' % (part, split), floor_slab(part, split, T), floor_slab_wire(part, split, T, w)))
    out.append(('mono-plinth', mono_base(T), mono_base_wire(T, w * 1.4)))
    out.append(('mono-obelisk', mono_obelisk(T), mono_obelisk_wire(T, w * 1.4)))
    out.append(('crown-spire', crown_spire(T), crown_spire_wire(T, w * 1.6)))
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
        bk = sk.Bake(g, ao[k], world=world, floors=(0.0, -.12), seed=101 + k)
        press(g, bk)
        out.append(bk)
    return out


def press(g, bk):
    """The black glass's occlusion pressed down (OCCLUDED): the shader occludes the probe's reflection by the vertex alpha but not
    the lights' highlights, so the obsidian keeps its gloss without taking the arena's sky (it read pale grey on Brickfall)."""
    P, N, S, F = g.arrays()
    fc = np.repeat(F, S)
    for name, k in OCCLUDED.items():
        m = fc == FIN[name]
        if m.any():
            bk.colour[m, 3] = np.round(bk.colour[m, 3].astype(np.float64) * k).astype(np.uint8)


def plain(g):
    return sk.Bake(g, None, floors=(), seed=3)


VIEWS = [('old-arena', (2.5, 1.2, 9.5), (0, .9, 0), 55), ('new-wide', (0, 6, 24), (0, 6, 0), 55), ('new-low', (2.2, .5, 5.5), (0, 3.5, -4), 75),
         ('crown-up', (1, .4, 4), (0, 12, -2), 80), ('top', (0, 40, .1), (0, 0, 0), 60), ('mono-close', (6.2, 2, 5.4), (9.3 * .38, 3, 9.3 * .92), 50),
         ('floor-close', (1.2, 1.6, 3.4), (0, 0, 1), 60), ('old-top', (0, 14, 6), (0, 0, 0), 60), ('hero', (1.0, .3, 4.2), (0, 2.9, -1), 52), ('void', (6, 3, 14), (0, 1, 0), 60)]
FLOOR = .012
GROUND = FLOOR - DAIS_H
# the crown's spires: rooted CROWN_R out and CROWN_Y up over each monolith, leaning in toward the point CROWN_APEX over the dais
CROWN_R, CROWN_Y, CROWN_APEX = 10.2, 14.0, 23.0


def crown_tilt():
    """How far each spire leans in from upright (radians): toward the apex."""
    return math.atan2(CROWN_R, CROWN_APEX - CROWN_Y)


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx, aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def layout(new=True):
    """The pieces where Core NativeIntroArchitect rests them: [(name, x, y, z, yaw[, tilt])] (yaw about +y, then the tilt about the
    piece's own x; scale 1)."""
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
    for k in range(WEDGES):
        a = k * 2 * WEDGE_HALF
        for part in (0, 1):
            h0, h1 = slab_span(part, k & 1)
            rm = (h0 + h1) * .5
            out.append(('slab-%d%d' % (part, k & 1), math.sin(a) * rm, FLOOR, math.cos(a) * rm, a))
    for i in range(8):
        a = (i + .5) / 8 * 2 * math.pi
        x, z = math.sin(a) * 9.3, math.cos(a) * 9.3
        out.append(('mono-plinth', x, FLOOR + .6, z, a))
        out.append(('mono-obelisk', x, FLOOR + .6, z, a))
    for i in range(8):
        a = (i + .5) / 8 * 2 * math.pi
        out.append(('crown-spire', math.sin(a) * CROWN_R, CROWN_Y, math.cos(a) * CROWN_R, a, -crown_tilt()))
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
        for it in layout(isnew):
            name, x, y, z, yaw = it[:5]
            q = sk.quat_axis(0, 1, 0, yaw)
            if len(it) > 5:
                q = qmul(q, sk.quat_axis(1, 0, 0, it[5]))
            if name in byname:
                dst.extend(byname[name].transformed(sk.affine(q, (x, y, z))))
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
