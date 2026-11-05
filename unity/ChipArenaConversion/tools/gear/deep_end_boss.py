"""THE DEEP END, the Poolrooms' boss, modelled in Blender (docs/EXPEDITION-BOSS-BACKROOMS.md section 4). It writes the bank
Resources/ChipArenaPackBossDeepEnd (gearkit.py's format): the body as one smooth skinned mesh per tier, "deepend-body-<tier>", laid out
straight along the spine, mouth at the origin and the tail at z -32 (Unity's axes: x its right, y its back, z the mouth), and the rigid
parts ("deepend-<part>-<tier>") each in its own frame. Runtime/NativeDeepEndBossBody.cs binds and draws them on Core DeepEndBossRig's pose.

usage: blender --background --factory-startup --python tools/gear/deep_end_boss.py -- --out ASSETS/LocalAssets [--sheet DIR] [--tiers 0,1,2] [--sheet-tier N]

A vertex's uv0 is (q, gloss): q the place along the spine in joints (s / 1.0667, Runtime turns it into a cubic B-spline's four weights, so
the skin bends as a curve with no creases); gloss the glaze's strength (0 grout, 1 glazed tile, 2 chrome). The numbers the game must agree
with (the radius along the body, the lamps' and fins' places) are mirrored in Core NativeDeepEndBossLook; Tests/DeepEndBossChecks holds them
to this table's constants.

The look: a thing made of the pool. Real geometry, not a texture: thousands of glazed white tiles, each with its own bevel and a tiny
tilt of its own so highlights break tile by tile as real glaze does, teal grout in the channels, pale turquoise basin tile on the belly.
Black tile lettering on the upper flanks (mirrored, so it reads from either side) counts up toward the head, 3 FT, 5 FT, 9 FT, NO DIVING,
and across the crown DEEP END, with a black lane line down the spine. A pool's bullnose coping runs along each flank carrying the lamps.
No eyes. The mouth is an iris of chrome grate blades over a turning drain, a blue throat behind them, and two pool-ladder rails for mandibles.
"""
import argparse
import math
import os
import random
import sys

import bpy
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gearkit as gk  # noqa: E402
from gearkit import Piece, finish  # noqa: E402

BANK = 'ChipArenaPackBossDeepEnd'
TO_JOINT = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
LENGTH = 32.0
JOINTS = 30
SEG = LENGTH / JOINTS
# The zones round the body (radians from the back): the glazed dorsal tile, the coping, the belly.
THETA_B, THETA_C = 1.46, 1.72
NECK = 3.7
RINGS = (16.0, 25.0)
TAIL_END = 31.7
# The twelve lamps (s along the spine; the same on both sides), the fins' root, the crest's span.
LAMPS = (6.0, 10.0, 14.0, 18.0, 22.0, 26.0)
FIN_S = 4.6
CREST = (5.2, 15.6)


def clamp(x, a=0.0, b=1.0):
    return max(a, min(b, x))


def smooth(a, b, x):
    t = clamp((x - a) / (b - a))
    return t * t * (3 - 2 * t)


def radius(s):
    """The body's radius at s m behind the mouth (Core NativeDeepEndBossLook.Radius): the skull bulges out of the mouth's rim, narrows at
    the neck, the shoulders fill to 1.2 and it tapers to the tail."""
    if s < 1.4:
        u = clamp(1 - s / 1.4)
        return 1.05 + .35 * math.sqrt(max(0.0, 1 - u * u))
    if s < 3.6:
        return 1.40 + (1.14 - 1.40) * smooth(1.4, 3.6, s)
    if s < 9.0:
        return 1.14 + .06 * smooth(3.6, 9.0, s)
    u = clamp((s - 9.0) / (LENGTH - 9.0))
    return .22 + (1.2 - .22) * math.pow(max(0.0, 1 - math.pow(u, 1.7)), .85)


def bump(x, c, w):
    d = (x - c) / w
    return math.exp(-d * d)


def surf(s, th):
    """A point on the skin (design frame): th 0 on the back, +pi/2 the right flank, pi the belly."""
    r = radius(s)
    wx = 1.0 if s < 21 else 1.0 - .34 * smooth(21, 31, s)
    wz = .8 + .2 * smooth(2.6, 6.0, s) + .12 * (1 - smooth(0, 1.6, s))
    zc = -.16 * math.pow(clamp(1 - s / 3.6), 2)
    sn, cs = math.sin(th), math.cos(th)
    # A superellipse: a fuller upper body with a shoulder toward the coping, a flatter belly.
    e = 2.0 / 2.25
    sx = math.copysign(math.pow(abs(sn), e), sn)
    cz = math.copysign(math.pow(abs(cs), e), cs)
    x, z = wx * r * sx, wz * r * cz
    if cs < 0:
        z *= .92
    # The skull: a melon over the crown, cheeks where the rails root, a chin.
    if s < 4.0:
        k = bump(s, 1.9, 1.15)
        z += .10 * k * max(0.0, cs)
        x *= 1 + .05 * bump(s, 1.0, .8) * abs(sn)
        z += -.05 * bump(s, .7, .6) * max(0.0, -cs)
    # A dorsal swell where the crest roots, the faint muscle swell along the shoulders.
    z += .035 * max(0.0, cs) * math.sin((s - 4.0) * .55) * smooth(4, 9, s) * (1 - smooth(20, 28, s))
    return Vector((x, -s, zc + z))


def surf_n(s, th):
    e = .012
    a = surf(s, th)
    du = surf(max(0, s - e), th) - surf(s + e, th)
    dv = surf(s, th + e) - surf(s, th - e)
    n = dv.cross(du)
    if n.length < 1e-9:
        return Vector((math.sin(th), 0, math.cos(th)))
    n.normalize()
    # (Outward.)
    if n.dot(Vector((a.x, 0, a.z + .16 * math.pow(clamp(1 - s / 3.6), 2)))) < 0:
        n = -n
    return n


# ---------------------------------------------------------------------------------------------------------------------------
# The lettering: a stroke font, each letter polylines in a box 1 high, its width in the table
# ---------------------------------------------------------------------------------------------------------------------------
def _arc(cx, cy, rx, ry, a0, a1, n=8):
    return [(cx + rx * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + ry * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


FONT = {
    'D': (.62, [[(0, 0), (0, 1)], [(0, 1), (.28, 1)] + _arc(.28, .5, .34, .5, 90, -90) + [(0, 0)]]),
    'E': (.56, [[(.56, 1), (0, 1), (0, 0), (.56, 0)], [(0, .52), (.44, .52)]]),
    'P': (.58, [[(0, 0), (0, 1), (.3, 1)] + _arc(.3, .73, .28, .27, 90, -90) + [(0, .46)]]),
    'N': (.64, [[(0, 0), (0, 1), (.64, 0), (.64, 1)]]),
    'O': (.68, [_arc(.34, .5, .34, .5, 0, 360, 16)]),
    'I': (.12, [[(.06, 0), (.06, 1)]]),
    'V': (.66, [[(0, 1), (.33, 0), (.66, 1)]]),
    'G': (.68, [_arc(.36, .5, .36, .5, 40, 320, 14) + [(.66, .47), (.4, .47)]]),
    'T': (.6, [[(0, 1), (.6, 1)], [(.3, 1), (.3, 0)]]),
    'F': (.52, [[(.52, 1), (0, 1), (0, 0)], [(0, .54), (.42, .54)]]),
    '3': (.58, [[(.02, 1), (.56, 1), (.26, .6)] + _arc(.26, .3, .3, .3, 90, -130)]),
    '5': (.58, [[(.54, 1), (.06, 1), (.03, .56)] + _arc(.26, .3, .3, .3, 110, -130)]),
    '9': (.6, [_arc(.3, .68, .3, .32, 0, 360, 12), [(.6, .66), (.5, .22), (.2, 0)]]),
    ' ': (.34, []),
}
STROKE = .105


def _seg_dist(px, py, a, b):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    l2 = dx * dx + dy * dy
    t = clamp(((px - ax) * dx + (py - ay) * dy) / l2) if l2 > 1e-9 else 0
    qx, qy = ax + dx * t - px, ay + dy * t - py
    return math.sqrt(qx * qx + qy * qy)


def text_layout(text, gap=.2):
    x = 0.0
    out = []
    for ch in text:
        w, strokes = FONT[ch]
        out.append((x, w, strokes))
        x += w + gap
    return out, x - gap


class Text:
    """A line of text as a mask: `inside(u, v)`, u along the line in em from its start, v up the letters 0..1."""

    def __init__(self, text):
        self.layout, self.width = text_layout(text)

    def inside(self, u, v):
        if v < -STROKE or v > 1 + STROKE or u < -STROKE or u > self.width + STROKE:
            return False
        for x, w, strokes in self.layout:
            if u < x - STROKE or u > x + w + STROKE:
                continue
            for line in strokes:
                for i in range(len(line) - 1):
                    if _seg_dist(u - x, v, line[i], line[i + 1]) < STROKE:
                        return True
        return False


# The flank lines: (text, the centre's s, the band in radians from the back). They count up toward the head.
FLANK_TEXT = [(Text('3 FT'), 22.8, .34, 1.30), (Text('5 FT'), 18.6, .34, 1.30), (Text('9 FT'), 14.6, .34, 1.30), (Text('NO DIVING'), 8.45, .34, 1.30)]
# Across the crown, two lines, read from in front of the mouth (the letters' tops toward the tail): what it says as it comes at you.
CROWN = ((Text('DEEP'), 2.30, 2.98), (Text('END'), 1.38, 2.06))


def ink(s, th):
    """Whether (s, th) is under a letter, the crown's or a flank's."""
    if s < NECK:
        for t, feet, top in CROWN:
            v = (s - feet) / (top - feet)
            if v < -.2 or v > 1.2:
                continue
            em = top - feet
            # Reading to the boss's left (the viewer's right, facing the mouth).
            u = -th * radius(s) * 1.0 / em
            if t.inside(u + t.width * .5, v):
                return True
        return False
    if s > 26:
        return False
    a = abs(th)
    for t, centre, b0, b1 in FLANK_TEXT:
        if a < b0 - .1 or a > b1 + .1:
            continue
        rc = radius(centre)
        em = rc * (b1 - b0)
        half = t.width * em * .5
        if abs(s - centre) > half + em * STROKE:
            continue
        # The letters' tops toward the back. On the right flank the text reads toward the head (s falling), on the left toward the tail.
        along = (centre - s) if th > 0 else (s - centre)
        if t.inside(along / em + t.width * .5, (b1 - a) / (b1 - b0)):
            return True
    return False


# ---------------------------------------------------------------------------------------------------------------------------
# The skin's colours
# ---------------------------------------------------------------------------------------------------------------------------
WHITE = gk.srgb(0xF2F6F4)
BELLY = gk.srgb(0xA8E4E2)
BLACK = gk.srgb(0x12171B)
GROUT = gk.srgb(0x3F9CA0)
GROUT_DARK = gk.srgb(0x1F6A70)
GROUT_BLACK = gk.srgb(0x2E4448)
COPING = gk.srgb(0xFAFBF6)
TRIM = gk.srgb(0x0E4F5C)


def lerp3(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


def scale3(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def h2(a, b):
    v = math.sin(a * 127.1 + b * 311.7) * 43758.5453
    return v - math.floor(v)


class Skin:
    """The body's mesh being built (Unity's axes, the winding Unity's)."""

    def __init__(self, name):
        self.gm = gk.GearMesh(name)
        self.gm.submeshes = [[], []]

    def v(self, p, n, rgb, gloss=1.0, line=True, s=None):
        gm = self.gm
        i = len(gm.positions)
        gm.positions.append((p.x, p.z, p.y))
        gm.normals.append((n.x, n.z, n.y))
        gm.uv0.append(((-p.y if s is None else s) / SEG, gloss))
        gm.colors.append((clamp(rgb[0]), clamp(rgb[1]), clamp(rgb[2]), 1.0 if line else 0.0))
        return i

    def quad(self, a, b, c, d, glow=False):
        """Four indices, clockwise seen from outside (Unity's front)."""
        sub = self.gm.submeshes[1 if glow else 0]
        sub.extend((a, b, c, a, c, d))

    def tri(self, a, b, c, glow=False):
        self.gm.submeshes[1 if glow else 0].extend((a, b, c))


def ao_at(s, th):
    """Depth baked into the colour (the toon flattens big forms): under the coping and toward the belly darker, the channel by the crest."""
    c = math.cos(th)
    k = .90 + .10 * smooth(-.9, .7, c)
    k *= 1 - .10 * bump(abs(th), THETA_C + .12, .14)
    if CREST[0] - .3 < s < CREST[1] + .3:
        k *= 1 - .10 * bump(abs(th), .0, .12)
    return k


class Tile:
    """Tile finishes: how a cell is built at a tier. level 0: bevelled tiles (10 triangles, 6 on the belly), 1: flat cells, 2: the same at half the count."""

    def __init__(self, level):
        self.level = level
        self.tri = 0


def cell(sk, tier, s0, s1, t0, t1, zone, seedx, seedy):
    """One tile over [s0,s1] x [t0,t1]."""
    sc, tc = (s0 + s1) * .5, (t0 + t1) * .5
    a, b, c, d = surf(s0, t0), surf(s0, t1), surf(s1, t1), surf(s1, t0)
    n = surf_n(sc, tc)
    jx, jy, jv = h2(seedx, seedy), h2(seedy + 7.3, seedx + 3.1), h2(seedx * 1.7 + 2.0, seedy * 2.3)
    # The letters and the lane line.
    lane = zone == 'dorsal' and s0 >= NECK and abs(tc) < (t1 - t0) * 1.02
    glaze = 1.0
    base = WHITE
    grout = GROUT
    if zone == 'belly':
        base = BELLY
        grout = GROUT_DARK
    if lane:
        base = BLACK
        grout = GROUT_BLACK
    ao = ao_at(sc, tc)
    tint = 1 + (jv - .5) * .05
    colour = scale3(base, ao * tint)
    if zone != 'belly' and jv > .955 and not lane:
        colour = lerp3(colour, scale3(gk.srgb(0xDCEFF0), ao), .8)  # A slightly bluer batch, now and then.
    # A faint tide line of stain in the grout toward the belly and the tail.
    stain = smooth(.2, 1.0, noise.noise(Vector((sc * .35, tc * 1.3, 2.5))) * .5 + .5) * .45 + .55 * smooth(-.2, -1, math.cos(tc))
    gc = scale3(lerp3(grout, GROUT_DARK, clamp(stain)), ao)
    letters = zone == 'dorsal' and not lane
    mix = None
    if letters:
        samples = [ink(s0 + (s1 - s0) * (i + .5) / 3, t0 + (t1 - t0) * (j + .5) / 3) for i in range(3) for j in range(3)]
        count = sum(samples)
        if count == 9:
            colour = scale3(BLACK, ao)
            gc = scale3(GROUT_BLACK, ao)
        elif count > 0:
            mix = samples
    if tier == 2:
        if mix is not None and sum(mix) >= 4:
            colour = scale3(BLACK, ao)
        i0 = sk.v(a, surf_n(s0, t0), colour)
        i1 = sk.v(b, surf_n(s0, t1), colour)
        i2 = sk.v(c, surf_n(s1, t1), colour)
        i3 = sk.v(d, surf_n(s1, t0), colour)
        sk.quad(i0, i1, i2, i3)
        return
    if mix is not None:
        # An edge of a letter: small mosaic pieces, black or white, the same size as the mask's samples. (No bevel: they read as finer tile.)
        div = 3 if tier == 0 else 2
        for i in range(div):
            for j in range(div):
                u0, u1 = s0 + (s1 - s0) * i / div, s0 + (s1 - s0) * (i + 1) / div
                v0, v1 = t0 + (t1 - t0) * j / div, t0 + (t1 - t0) * (j + 1) / div
                blk = ink((u0 + u1) * .5, (v0 + v1) * .5)
                k = scale3(BLACK if blk else WHITE, ao)
                nn = (n + Vector(((h2(i + seedx, j) - .5) * .06, (h2(j + seedy, i) - .5) * .06, 0))).normalized()
                lift = nn * .010
                i0 = sk.v(surf(u0, v0) + lift, nn, k)
                i1 = sk.v(surf(u0, v1) + lift, nn, k)
                i2 = sk.v(surf(u1, v1) + lift, nn, k)
                i3 = sk.v(surf(u1, v0) + lift, nn, k)
                sk.quad(i0, i1, i2, i3)
        return
    if tier == 1:
        n0 = (n + Vector(((jx - .5) * .05, 0, (jy - .5) * .05))).normalized()
        if zone == 'belly':
            k = scale3(lerp3(colour, gc, .08), 1)
            i0 = sk.v(a, n0, k)
            i1 = sk.v(b, n0, k)
            i2 = sk.v(c, n0, k)
            i3 = sk.v(d, n0, k)
            sk.quad(i0, i1, i2, i3)
            return
        # The grid stays: the top as one quad, grout along two of its sides.
        g = min(.3, .03 / max((a - b).length, .05))
        b_in, d_in, c_in = b.lerp(a, g), d.lerp(a, g), c.lerp(a, g)
        i0, i1, i2, i3 = sk.v(a, n0, colour), sk.v(b_in, n0, colour), sk.v(c_in, n0, colour), sk.v(d_in, n0, colour)
        sk.quad(i0, i1, i2, i3)
        j0, j1, j2, j3 = sk.v(b_in, n, gc, .1), sk.v(b, n, gc, .1), sk.v(c, n, gc, .1), sk.v(c_in, n, gc, .1)
        sk.quad(j0, j1, j2, j3)
        k0, k1, k2, k3 = sk.v(d_in, n, gc, .1), sk.v(c_in, n, gc, .1), sk.v(c, n, gc, .1), sk.v(d, n, gc, .1)
        sk.quad(k0, k1, k2, k3)
        return
    # Level 0: a real tile. An outer ring on the skin in the grout's colour, an inner ring raised and glazed, the top with its own small tilt
    # so the room's lights break tile by tile. The belly's are lean.
    centre = (a + b + c + d) * .25
    size = max((a - c).length, .05)
    inset = min(.42, .021 / (size * .7))
    tilt = Vector(((jx - .5) * .09, (jy - .5) * .09, (jv - .5) * .05))
    nt = (n + tilt).normalized()
    h = .013
    ring = [a.lerp(centre, inset) + n * h, b.lerp(centre, inset) + n * h, c.lerp(centre, inset) + n * h, d.lerp(centre, inset) + n * h]
    # A tile is a little domed: its corners' normals lean out, so a highlight has a shape.
    tops = [sk.v(ring[k], (nt + (ring[k] - centre).normalized() * .16).normalized(), colour, 1.0) for k in range(4)]
    sk.quad(tops[0], tops[1], tops[2], tops[3])
    outer = [a, b, c, d]
    if zone == 'belly':
        o = [sk.v(outer[k] - n * .004, (nt + (outer[k] - centre).normalized() * .9).normalized(), gc, .15) for k in range(4)]
        t2 = [sk.v(ring[k], (nt + (ring[k] - centre).normalized() * .5).normalized(), colour, .8) for k in range(4)]
        sk.quad(o[1], o[2], t2[2], t2[1])
        sk.quad(o[2], o[3], t2[3], t2[2])
        return
    o = [sk.v(outer[k] - n * .004, (nt + (outer[k] - centre).normalized() * .9).normalized(), gc, .1) for k in range(4)]
    t2 = [sk.v(ring[k], (nt + (ring[k] - centre).normalized() * .55).normalized(), colour, .8) for k in range(4)]
    for k in range(4):
        m = (k + 1) % 4
        sk.quad(o[k], o[m], t2[m], t2[k])


def tile_section(sk, tier, s0, s1, cols_top, cols_bot, step=None, top_limit=THETA_B, bot_from=THETA_C):
    """The grid over [s0, s1]: square-ish rows, the dorsal zone's columns between -top_limit and top_limit, the belly's between bot_from and
    2 pi - bot_from at twice the row height (basin tile)."""
    tier_div = 2 if tier == 2 else 1
    cols_top = max(2, cols_top // tier_div)
    cols_bot = max(2, cols_bot // tier_div)
    if cols_top % 2:
        cols_top += 1
    rows = [s0]
    s = s0
    while s < s1 - 1e-3:
        w = radius(s) * 2 * top_limit / cols_top
        ds = step * tier_div if step else clamp(w, .09, .4)
        s = min(s1, s + ds)
        if s1 - s < ds * .4:
            s = s1
        rows.append(s)
    for r in range(len(rows) - 1):
        a, b = rows[r], rows[r + 1]
        for k in range(cols_top):
            t0 = -top_limit + 2 * top_limit * k / cols_top
            t1 = -top_limit + 2 * top_limit * (k + 1) / cols_top
            cell(sk, tier, a, b, t0, t1, 'dorsal', r * 3.1 + s0, k * 1.9)
    span = 2 * math.pi - 2 * bot_from
    for r in range(0, len(rows) - 1, 2):
        a, b = rows[r], rows[min(r + 2, len(rows) - 1)]
        for k in range(cols_bot):
            t0 = bot_from + span * k / cols_bot
            t1 = bot_from + span * (k + 1) / cols_bot
            cell(sk, tier, a, b, t0, t1, 'belly', r * 2.3 + s0, k * 4.7)


def ring_band(sk, s0, s1, segs, rgb, lift=.02, gloss=1.2):
    """A proud band round the body (hiding a change of the tile's grid): a smooth strip, its two edges tucked down."""
    for k in range(segs):
        t0 = 2 * math.pi * k / segs
        t1 = 2 * math.pi * (k + 1) / segs
        prof = ((s0, -.015), (s0 + .02, lift), (s1 - .02, lift), (s1, -.015))
        for j in range(3):
            sa, ha = prof[j]
            sb, hb = prof[j + 1]
            na, nb = surf_n(sa, t0), surf_n(sa, t1)
            nc, nd = surf_n(sb, t1), surf_n(sb, t0)
            i0 = sk.v(surf(sa, t0) + na * ha, na, rgb, gloss)
            i1 = sk.v(surf(sa, t1) + nb * ha, nb, rgb, gloss)
            i2 = sk.v(surf(sb, t1) + nc * hb, nc, rgb, gloss)
            i3 = sk.v(surf(sb, t0) + nd * hb, nd, rgb, gloss)
            sk.quad(i0, i1, i2, i3)


def coping(sk, tier):
    """The bullnose coping down each flank: long glazed segments, proud of the tile, with a dark line under it."""
    seg_len = .62 if tier == 0 else 1.0 if tier == 1 else 1.8
    arcs = 6 if tier == 0 else 3 if tier == 1 else 2
    for side in (1, -1):
        s = NECK + .1
        idx = 0
        while s < 29.5:
            e = min(29.5, s + seg_len)
            for k in range(arcs):
                a0 = THETA_B + (THETA_C - THETA_B) * k / arcs
                a1 = THETA_B + (THETA_C - THETA_B) * (k + 1) / arcs
                m0 = math.sin(math.pi * k / arcs)
                m1 = math.sin(math.pi * (k + 1) / arcs)
                # The nose stands proud, its section a half round; the segment ends tucked for a joint.
                def ptn(ss, aa, mm):
                    n = surf_n(ss, side * aa)
                    return surf(ss, side * aa) + n * (.012 + .06 * mm * min(1.0, radius(ss) / .9)), n
                ca = scale3(COPING, ao_at(s, side * a0) * (1 + .04 * (h2(idx, side) - .5)))
                if k == arcs - 1:
                    ca = scale3(ca, .86)
                p0, n0 = ptn(s + .008, a0, m0)
                p1, n1 = ptn(s + .008, a1, m1)
                p2, n2 = ptn(e - .008, a1, m1)
                p3, n3 = ptn(e - .008, a0, m0)
                # A dome: the normals lean by where they are across the nose.
                def lean(n, aa):
                    u = (aa - THETA_B) / (THETA_C - THETA_B) * 2 - 1
                    return (n + Vector((0, 0, -u * .55 * (1 if side else 1)))).normalized()
                ids = [sk.v(p0, lean(n0, a0), ca, 1.1), sk.v(p1, lean(n1, a1), ca, 1.1), sk.v(p2, lean(n2, a1), ca, 1.1), sk.v(p3, lean(n3, a0), ca, 1.1)]
                if side > 0:
                    sk.quad(ids[0], ids[1], ids[2], ids[3])
                else:
                    sk.quad(ids[3], ids[2], ids[1], ids[0])
            # The shadow line under it (its lower edge down onto the tile).
            lo = side * THETA_C
            na, nb = surf_n(s, lo), surf_n(e, lo)
            dark = scale3(TRIM, .9)
            i0 = sk.v(surf(s, lo) + na * .014, na, dark, .3, False)
            i1 = sk.v(surf(s, side * (THETA_C + .035)) - na * .004, na, dark, .3, False)
            i2 = sk.v(surf(e, side * (THETA_C + .035)) - nb * .004, nb, dark, .3, False)
            i3 = sk.v(surf(e, lo) + nb * .014, nb, dark, .3, False)
            if side > 0:
                sk.quad(i0, i1, i2, i3)
            else:
                sk.quad(i3, i2, i1, i0)
            s = e
            idx += 1


def marker(sk, s, side, r, segs=8):
    """A small round running light in the coping: a chrome ring and a lens (the glow sub-mesh)."""
    th = side * (THETA_B + THETA_C) * .5
    n = surf_n(s, th)
    c = surf(s, th) + n * (.012 + .06 * min(1.0, radius(s) / .9))
    u = n.cross(Vector((0, 1, 0))).normalized()
    v = n.cross(u).normalized()
    chrome = gk.srgb(0xB8C6CC)
    glow = gk.srgb(0xBFFFFF)
    rim0, rim1, lens = [], [], []
    for k in range(segs):
        a = 2 * math.pi * k / segs
        d = u * math.cos(a) + v * math.sin(a)
        rim0.append(sk.v(c + d * r * 1.32 - n * .006, (n + d).normalized(), chrome, 2.0, False))
        rim1.append(sk.v(c + d * r * 1.08 + n * .012, (n + d * .2).normalized(), chrome, 2.0, False))
        lens.append(sk.v(c + d * r + n * .012, n, glow, 1.0, False))
    mid = sk.v(c + n * .03, n, glow, 1.0, False)
    for k in range(segs):
        m = (k + 1) % segs
        # (u, v as built go round clockwise or not by the side: draw whichever faces out.)
        sign = (u.cross(v)).dot(n)
        if sign > 0:
            sk.quad(rim0[m], rim0[k], rim1[k], rim1[m])
            sk.tri(mid, lens[m], lens[k], True)
        else:
            sk.quad(rim0[k], rim0[m], rim1[m], rim1[k])
            sk.tri(mid, lens[k], lens[m], True)


def markers(sk, tier):
    step = 1.0 if tier == 0 else 2.0
    if tier == 2:
        return
    for side in (1, -1):
        s = 5.0
        while s < 29.0:
            if all(abs(s - q) > .62 for q in LAMPS):
                marker(sk, s, side, .058 * min(1.0, radius(s) / .9) + .018, 8 if tier == 0 else 5)
            s += step


def skull_fill(sk, tier):
    """The rim's collar: the short funnel from the last row of tile in to the chrome, closed behind the iris."""
    chrome = gk.srgb(0x9EB0B6)
    n = 40 if tier == 0 else 22 if tier == 1 else 12
    for k in range(n):
        t0, t1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        p0, p1 = surf(.1, t0), surf(.1, t1)
        na, nb = surf_n(.1, t0), surf_n(.1, t1)
        zc = Vector((0, 0, -.16))
        i0 = sk.v(p0, na, chrome, 2.0)
        i1 = sk.v(p1, nb, chrome, 2.0)
        i2 = sk.v(zc + Vector((math.sin(t1) * .96, .02, math.cos(t1) * .96)), Vector((0, 1, 0)), chrome, 2.0)
        i3 = sk.v(zc + Vector((math.sin(t0) * .96, .02, math.cos(t0) * .96)), Vector((0, 1, 0)), chrome, 2.0)
        sk.quad(i3, i2, i1, i0)
    # The rim: a chrome ring, the mouth's edge.
    segs = (56, 28, 14)[tier]
    pts = [(Vector((1.02 * math.sin(2 * math.pi * k / segs), .03, -.16 + 1.02 * math.cos(2 * math.pi * k / segs))), .03) for k in range(segs + 1)]
    tube_on(sk, pts, .085, (10, 6, 4)[tier], gk.srgb(0xC4D0D6), 2.0)
    # A second, thin ring outside it where the tile stops.
    pts = [(Vector((1.145 * math.sin(2 * math.pi * k / segs), -.02, -.16 + 1.045 * math.cos(2 * math.pi * k / segs))), .03) for k in range(segs + 1)]
    if tier < 2:
        tube_on(sk, pts, .026, 5, gk.srgb(0xC4D0D6), 2.0)


def tail_tip(sk, tier):
    n = 16 if tier == 0 else 10
    centre = surf(LENGTH, 0) * .5 + surf(LENGTH, math.pi) * .5
    tip = sk.v(centre, Vector((0, -1, 0)), scale3(WHITE, .9), 1.0)
    # The tail's last stretch closes round to a point.
    rim = [sk.v(surf(TAIL_END, 2 * math.pi * k / n), surf_n(TAIL_END, 2 * math.pi * k / n), scale3(WHITE, .92), 1.0) for k in range(n)]
    for k in range(n):
        sk.tri(rim[k], tip, rim[(k + 1) % n])


def blade_on_spine(sk, s0, s1, height, tier, rgb, rail=None, up=True, thick=.05, shape=None):
    """A thin fibreglass blade standing on the spine (the crest, the tail's fin): a lofted plate, thicker at its root, with an optional chrome
    rail on stanchions along its top."""
    n = int((s1 - s0) / (.34 if tier == 0 else .6 if tier == 1 else 1.1)) + 2
    sgn = 1 if up else -1
    th0 = 0 if up else math.pi
    levels = (0.0, .3, .68, 1.0) if tier == 0 else (0.0, .55, 1.0) if tier == 1 else (0.0, 1.0)
    prof = []
    for i in range(n):
        s = s0 + (s1 - s0) * i / (n - 1)
        u = i / (n - 1)
        h = height * (shape(u) if shape else math.pow(math.sin(math.pi * clamp(u)), .55))
        root = surf(s, th0)
        ringL, ringR = [], []
        for l in levels:
            t = thick * (1 - .75 * l)
            z = root.z + sgn * h * l
            # The top sweeps back a little with height.
            y = root.y - .35 * h * l
            ringL.append(((-t, y, z), l))
            ringR.append(((t, y, z), l))
        prof.append((ringL, ringR, s))
    white = gk.srgb(0xF5F7F2)
    for i in range(n - 1):
        la, ra, sa = prof[i]
        lb, rb, sb = prof[i + 1]
        for j in range(len(levels) - 1):
            for side, (pa, pb) in ((1, (ra, rb)), (-1, (la, lb))):
                a0, _ = pa[j]
                a1, la1 = pa[j + 1]
                b0, _ = pb[j]
                b1, lb1 = pb[j + 1]
                nn = Vector((side, 0, .12)).normalized()
                k0 = scale3(white, .78 + .22 * levels[j])
                k1 = scale3(white, .78 + .22 * levels[j + 1])
                i0 = sk.v(Vector(a0), nn, k0, .8, s=sa)
                i1 = sk.v(Vector(a1), nn, k1, .8, s=sa)
                i2 = sk.v(Vector(b1), nn, k1, .8, s=sb)
                i3 = sk.v(Vector(b0), nn, k0, .8, s=sb)
                # Faces out on either side, whichever way the blade stands.
                if (side > 0) != up:
                    sk.quad(i0, i1, i2, i3)
                else:
                    sk.quad(i3, i2, i1, i0)
        # The top edge's cap.
        a = la[-1][0]
        b = ra[-1][0]
        c = rb[-1][0]
        d = lb[-1][0]
        nn = Vector((0, 0, sgn))
        i0, i1, i2, i3 = sk.v(Vector(a), nn, white, .8, s=sa), sk.v(Vector(b), nn, white, .8, s=sa), sk.v(Vector(c), nn, white, .8, s=sb), sk.v(Vector(d), nn, white, .8, s=sb)
        if up:
            sk.quad(i0, i1, i2, i3)
        else:
            sk.quad(i3, i2, i1, i0)
    if rail:
        spot, r = rail
        segs = 6 if tier == 0 else 4
        chrome = gk.srgb(0xC4D0D6)
        pts = []
        for i in range(n):
            ring, _, s = prof[i][0], prof[i][1], prof[i][2]
            top = Vector(ring[-1][0])
            pts.append((Vector((0, top.y, top.z + sgn * spot)), s))
        tube_on(sk, pts, r, segs, chrome, 2.0)
        if tier < 2:
            for i in range(1, n - 1, 3 if tier == 0 else 4):
                ring, _, s = prof[i][0], prof[i][1], prof[i][2]
                top = Vector(ring[-1][0])
                a = Vector((0, top.y, top.z))
                b = Vector((0, top.y, top.z + sgn * spot))
                tube_on(sk, [(a, s), (b, s)], r * .6, 4, chrome, 2.0)


def tube_on(sk, pts, r, segs, rgb, gloss, q_s=True):
    """A round tube along (point, s) pairs on the skin mesh."""
    n = len(pts)
    prev = None
    for i in range(n):
        p, s = pts[i]
        a = pts[max(0, i - 1)][0]
        b = pts[min(n - 1, i + 1)][0]
        t = (b - a).normalized() if (b - a).length > 1e-6 else Vector((0, 1, 0))
        u = t.cross(Vector((1, 0, 0)))
        if u.length < .1:
            u = t.cross(Vector((0, 0, 1)))
        u.normalize()
        v = t.cross(u)
        ring = []
        for k in range(segs):
            q = 2 * math.pi * k / segs
            d = u * math.cos(q) + v * math.sin(q)
            ring.append(sk.v(p + d * r, d, rgb, gloss, s=s))
        if prev is not None:
            for k in range(segs):
                m = (k + 1) % segs
                sk.quad(prev[k], prev[m], ring[m], ring[k])
        prev = ring


def build_body(tier):
    sk = Skin('deepend-body-%d' % tier)
    level = tier
    head_cols = (30, 18)
    # The skull: finer tile (the crown's lettering is on it), rows ringing the mouth.
    tile_section(sk, level, .1, NECK, head_cols[0], head_cols[1], step=.125, top_limit=1.75, bot_from=1.75)
    # The grid changes at bands: fewer columns as it tapers, so each tile keeps its size; the bands read as depth rings.
    tile_section(sk, level, NECK, RINGS[0], 20, 10)
    tile_section(sk, level, RINGS[0], RINGS[1], 14, 7)
    tile_section(sk, level, RINGS[1], TAIL_END, 8, 4, step=.17)
    trim = scale3(TRIM, .8)
    segs = 48 if tier == 0 else 24 if tier == 1 else 12
    ring_band(sk, NECK - .05, NECK + .16, segs, BLACK, .03)
    for q in RINGS:
        ring_band(sk, q - .07, q + .07, max(12, segs // 2), trim, .02)
    coping(sk, tier)
    markers(sk, tier)
    skull_fill(sk, tier)
    tail_tip(sk, tier)
    # The low dorsal crest with its grab rail, and the tail's fin.
    blade_on_spine(sk, CREST[0], CREST[1], .34, tier, WHITE, rail=(.1, .03), shape=lambda u: math.pow(math.sin(math.pi * clamp(u)), .35))
    tail_shape = lambda u: smooth(0, .55, u) * (1 - .25 * smooth(.8, 1, u))
    blade_on_spine(sk, 26.5, LENGTH + .25, .86, tier, WHITE, rail=(.06, .022) if tier < 2 else None, shape=tail_shape, thick=.035)
    blade_on_spine(sk, 27.6, LENGTH + .25, .66, tier, WHITE, up=False, shape=tail_shape, thick=.03)
    gm = sk.gm
    if not gm.submeshes[1]:
        gm.submeshes = [gm.submeshes[0]]
    return gm


# ---------------------------------------------------------------------------------------------------------------------------
# The rigid parts, on gearkit's pieces (each with its own bevels and bake)
# ---------------------------------------------------------------------------------------------------------------------------
GLOSS = {'chrome': 2.0, 'chrome-dark': 1.6, 'fibre': .9, 'fibre-edge': 1.0, 'tile': 1.0, 'lens': 0.0, 'throat': 0.0, 'black': .3, 'rubber': .2, 'float-w': .7, 'float-r': .7, 'glass-dead': 1.4, 'bolt': 1.5}


def finishes():
    finish('chrome', 0x8C9AA2, 'metal', dirt=.05)
    finish('chrome-dark', 0x4F5B62, 'metal', dirt=.1)
    finish('fibre', 0xF2F4EF, 'plastic', dirt=.12)
    finish('fibre-edge', 0x8FD4D2, 'plastic', dirt=.2)
    finish('tile', 0xEFF4F2, 'plastic', dirt=.15, outline=False)
    finish('lens', 0xBFFFFB, 'glow', outline=False)
    finish('throat', 0x5ED8FF, 'glow', outline=False)
    finish('black', 0x0A0E10, 'rubber', outline=False, dirt=.2)
    finish('rubber', 0x1B2226, 'rubber', outline=False, dirt=.4)
    finish('float-w', 0xF5F3EA, 'plastic', dirt=.25)
    finish('float-r', 0xD8432F, 'plastic', dirt=.25)
    finish('glass-dead', 0x30484E, 'plastic', outline=False, dirt=.4)
    finish('bolt', 0x7A878E, 'metal', outline=False, dirt=.2)


def bake_rigid(name, obs, seed=0.0):
    """gearkit's bake, then the glaze's strength per vertex (uv0.y by the finish)."""
    gm = gk.GearMesh(name)
    deps = bpy.context.evaluated_depsgraph_get()
    index = {}
    for ob in obs:
        ev = ob.evaluated_get(deps)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        cav = gk._cavity(me)
        cn = me.corner_normals
        W = ob.matrix_world
        Wn = W.to_3x3().inverted().transposed()
        M = TO_JOINT @ W
        Mn = M.to_3x3().inverted().transposed()
        for tri in me.loop_triangles:
            mat = ob.material_slots[tri.material_index].material if ob.material_slots else None
            f = gk.FINISHES[mat.name]
            ids = []
            for li in tri.loops:
                vi = me.loops[li].vertex_index
                co = me.vertices[vi].co
                col = gk.shade(f, W @ co, (Wn @ cn[li].vector).normalized(), cav[vi], seed)
                pu = M @ co
                nu = (Mn @ cn[li].vector).normalized()
                rgba = (round(col.x * 255), round(col.y * 255), round(col.z * 255), 255 if f.outline else 0)
                key = (round(pu.x, 5), round(pu.y, 5), round(pu.z, 5), round(nu.x, 3), round(nu.y, 3), round(nu.z, 3), rgba, f.name)
                k = index.get(key)
                if k is None:
                    k = index[key] = len(gm.positions)
                    gm.positions.append((pu.x, pu.y, pu.z))
                    gm.normals.append((nu.x, nu.y, nu.z))
                    gm.uv0.append((0.0, GLOSS.get(f.name, 1.0)))
                    gm.colors.append(tuple(c / 255.0 for c in rgba))
                ids.append(k)
            gm.submeshes[1 if f.glow else 0].extend((ids[0], ids[2], ids[1]))
        ev.to_mesh_clear()
    if not gm.submeshes[1]:
        gm.submeshes = [gm.submeshes[0]]
    return gm


def lamp(tier, broken=False):
    """A round pool light: the frame is its centre on the coping, facing +Y (design), radius about 1 scaled by the game (the bezel's outer
    radius 1.0): a chrome bezel with its screws, the lens's prism rings behind it, the glow."""
    segs = (20, 12, 8)[tier]
    pc = Piece('lamp')
    # The bezel: a stepped chrome ring (real profile, swept).
    prof = [(1.0, -.08), (.98, .05), (.86, .1), (.76, .05), (.74, -.02)] if tier == 0 else [(1.0, -.08), (.9, .1), (.74, -.02)]
    rings = []
    for r, y in prof:
        rings.append([pc.bm.verts.new((r * math.cos(2 * math.pi * k / segs), y, r * math.sin(2 * math.pi * k / segs))) for k in range(segs)])
    faces = []
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for k in range(segs):
            m = (k + 1) % segs
            faces.append(pc.bm.faces.new((a[k], a[m], b[m], b[k])))
    pc.tag(faces, 'chrome')
    # The lens: a shallow dome with prism steps, glowing (or the dead glass, broken).
    lens_prof = [(.75, -.02), (.52, .03), (.5, .01), (.26, .07), (0.0, .1)] if tier == 0 else [(.75, -.02), (.4, .05), (0.0, .1)]
    fin = 'glass-dead' if broken else 'lens'
    lr = []
    for r, y in lens_prof[:-1]:
        lr.append([pc.bm.verts.new((r * math.cos(2 * math.pi * k / segs), y, r * math.sin(2 * math.pi * k / segs))) for k in range(segs)])
    faces = []
    for i in range(len(lr) - 1):
        a, b = lr[i], lr[i + 1]
        for k in range(segs):
            m = (k + 1) % segs
            faces.append(pc.bm.faces.new((a[k], a[m], b[m], b[k])))
    tip = pc.bm.verts.new((0, lens_prof[-1][1], 0))
    for k in range(segs):
        faces.append(pc.bm.faces.new((lr[-1][k], lr[-1][(k + 1) % segs], tip)))
    pc.tag(faces, fin)
    if broken and tier < 2:
        # The shards left standing in the rim, and the black hole behind.
        rnd = random.Random(7)
        for k in range(9):
            a = 2 * math.pi * k / 9 + rnd.random() * .3
            r0, r1 = .7, .28 + rnd.random() * .22
            w = .16 + rnd.random() * .1
            p = (math.cos(a), math.sin(a))
            q = (-math.sin(a), math.cos(a))
            v0 = pc.bm.verts.new((p[0] * r0 + q[0] * w, .1, p[1] * r0 + q[1] * w))
            v1 = pc.bm.verts.new((p[0] * r0 - q[0] * w, .1, p[1] * r0 - q[1] * w))
            v2 = pc.bm.verts.new((p[0] * r1 + q[0] * rnd.uniform(-.05, .05), .16 + rnd.random() * .05, p[1] * r1 + q[1] * rnd.uniform(-.05, .05)))
            pc.tag([pc.bm.faces.new((v0, v1, v2))], 'glass-dead')
        gk.cyl(pc, (0, .07, 0), (0, .075, 0), .45, 'black', 14)
    ob = pc.make()
    return [ob]


def blade_point(u, v):
    """A blade's plate: a crescent from its pivot on the rim (u 0, at (1, 0) in the mouth's plane) sweeping to its tip at the centre (u 1), v 0 its leading edge, 1 its
    trailing edge. The game turns each blade about the pivot to open the iris."""
    p0, c, p1 = (1.0, 0.0), (.72, .86), (-.02, .03)
    x = (1 - u) ** 2 * p0[0] + 2 * u * (1 - u) * c[0] + u * u * p1[0]
    z = (1 - u) ** 2 * p0[1] + 2 * u * (1 - u) * c[1] + u * u * p1[1]
    dx = 2 * (1 - u) * (c[0] - p0[0]) + 2 * u * (p1[0] - c[0])
    dz = 2 * (1 - u) * (c[1] - p0[1]) + 2 * u * (p1[1] - c[1])
    l = math.hypot(dx, dz) or 1
    w = .62 * math.pow(math.sin(math.pi * clamp(u * .92 + .08)), .75)
    return x + (-dz / l) * w * (v - .5), z + (dx / l) * w * (v - .5)


def blade(tier):
    """One iris blade, in the mouth's frame (the origin the mouth's centre, the plate in the XZ plane facing +Y, the pivot at (1, 0)): chrome with a row of drain slots across it,
    so the closed iris reads as a spiral grate. Rigid; nine of them turn about their pivots."""
    pc = Piece('blade')
    # Bars across the blade, along u: wide chrome, a narrow slot, as stations.
    bars = (7, 4, 2)[tier]
    across = (2, 2, 1)[tier]
    stations = []
    for b in range(bars):
        stations.append((b + .02) / bars)
        if tier < 2:
            stations.append((b + .74) / bars)
    stations.append(1.0)
    rows = [[pc.bm.verts.new((blade_point(u, j / across)[0], 0.0, blade_point(u, j / across)[1])) for j in range(across + 1)] for u in stations]
    chrome, slot = [], []
    for i in range(len(rows) - 1):
        is_slot = tier < 2 and i % 2 == 1
        for j in range(across):
            f = pc.bm.faces.new((rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]))
            (slot if is_slot else chrome).append(f)
    pc.tag(chrome, 'chrome')
    pc.tag(slot, 'black')
    pc.solidify(.016)
    pc.sharp = 40
    return [pc.make()]


def grate(tier):
    """The inner drain: concentric chrome rings on radial bars, turning behind the iris, an open centre for the jet."""
    pc = Piece('grate')
    segs = (28, 18, 10)[tier]
    rs = (.96, .66, .36)
    for r in rs:
        gk.torus(pc, (0, 0, 0), (0, 1, 0), r, .034, 'chrome', segs=segs, rsegs=4 if tier == 0 else 3)
    bars = 10 if tier == 0 else 6
    for k in range(bars):
        a = 2 * math.pi * k / bars
        gk.cyl(pc, (.3 * math.cos(a), 0, .3 * math.sin(a)), (.98 * math.cos(a), 0, .98 * math.sin(a)), .024, 'chrome', 4 if tier == 0 else 3)
    return [pc.make()]


def throat(tier):
    """The throat, seen through the open iris: a tube of tile rings receding (narrowing as it goes, so it seems to go back further than the body allows),
    glowing bands in it, a blue light at its end. Its faces look inward; it sits inside the head."""
    pc = Piece('throat')
    segs = (20, 14, 8)[tier]
    prof = []
    depth = 0.0
    r = .98
    count = (14, 8, 4)[tier]
    step = 2.3 / (count - 1)
    for i in range(count):
        prof.append((r, -depth, i))
        depth += step
        r *= (.84, .74, .55)[tier]
    rings = [[pc.bm.verts.new((rr * math.cos(2 * math.pi * k / segs), y, rr * math.sin(2 * math.pi * k / segs))) for k in range(segs)] for rr, y, _ in prof]
    for i in range(count - 1):
        fs = []
        for k in range(segs):
            m = (k + 1) % segs
            fs.append(pc.bm.faces.new((rings[i][m], rings[i][k], rings[i + 1][k], rings[i + 1][m])))
        pc.tag(fs, 'throat' if i % 3 == 2 or i >= count - 3 else 'chrome-dark')
    # The far end: the light itself.
    tip = pc.bm.verts.new((0, -depth - .2, 0))
    fs = []
    for k in range(segs):
        m = (k + 1) % segs
        fs.append(pc.bm.faces.new((rings[-1][m], rings[-1][k], tip)))
    pc.tag(fs, 'throat')
    return [pc.make(recalc=False)]


def rail(tier, side):
    """A mandible: a chrome pool-ladder rail, rooted in an escutcheon on the cheek (the frame's origin), curving forward and in. +Y forward, +X the boss's right."""
    pc = Piece('rail')
    per = (6, 4, 2)[tier]
    segs = (8, 6, 4)[tier]
    s = side
    pts = gk.catmull([(0, -.2, 0), (s * .1, .1, -.01), (s * .14, .6, -.06), (s * .02, 1.25, -.17), (-s * .34, 1.82, -.34), (-s * .72, 2.1, -.5)], per=per)
    gk.tube(pc, pts, .078, 'chrome', segs=segs)
    # The cap, the escutcheon where it enters the head.
    gk.sphere(pc, tuple(pts[-1]), (.092, .092, .092), 'chrome', u=segs, v=max(4, segs // 2))
    gk.torus(pc, (0, 0, 0), (s * .1, 1, 0), .16, .028, 'chrome', segs=segs + 4, rsegs=4 if tier == 0 else 3)
    if tier == 0:
        # A ladder's stay half way, its bracket with a rung stub.
        gk.torus(pc, tuple(pts[per * 2]), (.0, 1, -.1), .1, .018, 'chrome', segs=10, rsegs=4)
    return [pc.make()]


def fin(tier, side):
    """A long side fin: a thin swept foil of white fibreglass, its leading edge carrying a chrome grab rail on stanchions. The frame's origin is its root on
    the flank; at rest it reaches out and back (-Y) from there."""
    pc = Piece('fin')
    s = side
    n = (20, 10, 5)[tier]
    m = (5, 3, 2)[tier]
    rows = []
    for i in range(n + 1):
        u = i / n
        # The leading edge sweeps out and back, the chord narrows to a rounded tip.
        lead = Vector((s * (3.7 * math.pow(u, .85)), .9 - 6.6 * u * (.45 + .55 * u), -.06 - .5 * u * u))
        chord = 3.1 * math.pow(1 - u, .75) * (1 - .1 * u) + .06
        back = Vector((s * .06, -1, -.04)).normalized()
        row = []
        for j in range(m + 1):
            v = j / m
            p = lead + back * chord * v
            # An aerofoil's thickness, thin toward the tip.
            t = .085 * (1 - .8 * u) * math.sqrt(max(0.0, v * (1 - v))) * 2 + .004
            row.append((p, t))
        rows.append(row)
    up = {}
    dn = {}
    # Battens: raised along the chord every few rows, so the big white foil has structure.
    for i, row in enumerate(rows):
        if tier < 2 and i % (4 if tier == 0 else 5) == 2:
            rows[i] = [(p, t + .012) for p, t in row]
    for i, row in enumerate(rows):
        for j, (p, t) in enumerate(row):
            up[i, j] = pc.bm.verts.new((p.x, p.y, p.z + t))
            dn[i, j] = pc.bm.verts.new((p.x, p.y, p.z - t))
    fs = []
    bands = []
    for i in range(n):
        for j in range(m):
            band = j >= m - 1 and tier < 2
            f0 = pc.bm.faces.new((up[i, j], up[i + 1, j], up[i + 1, j + 1], up[i, j + 1]))
            f1 = pc.bm.faces.new((dn[i, j + 1], dn[i + 1, j + 1], dn[i + 1, j], dn[i, j]))
            (bands if band else fs).extend((f0, f1))
    for i in range(n):
        fs.append(pc.bm.faces.new((up[i, 0], dn[i, 0], dn[i + 1, 0], up[i + 1, 0])))
        fs.append(pc.bm.faces.new((up[i + 1, m], dn[i + 1, m], dn[i, m], up[i, m])))
    pc.tag(fs, 'fibre')
    pc.tag(bands, 'fibre-edge')
    # The grab rail along the leading edge, off it by a hand's width.
    per = 1
    edge = [rows[i][0][0] + Vector((s * .05, .17, .02)) for i in range(0, n + 1, 1)]
    gk.tube(pc, edge, .036, 'chrome', segs=(8, 6, 4)[tier])
    gk.sphere(pc, tuple(edge[-1]), (.05, .05, .05), 'chrome', u=8, v=5)
    for i in range(1, n + 1, (3, 4, 8)[tier]):
        gk.cyl(pc, tuple(rows[i][0][0] + Vector((0, .01, 0))), tuple(edge[i]), .02, 'chrome', (6, 4, 3)[tier])
    return [pc.make()]


def debris_tile(tier):
    pc = Piece('debris')
    gk.box(pc, (0, 0, 0), (.15, .15, .018), 'tile')
    pc.bevel(.004, 1)
    return [pc.make()]


def pool_float(tier):
    """A ring buoy: the float that goes round in the whirlpool."""
    pc = Piece('float')
    segs = (32, 20, 12)[tier]
    rs = (12, 8, 6)[tier]
    pts_w = []
    # Four white arcs, four red.
    for q in range(4):
        for fin_name, a0 in (('float-w', q * 90.0), ('float-r', q * 90.0 + 62.0)):
            a1 = a0 + (62.0 if fin_name == 'float-w' else 28.0)
            n = max(3, int((a1 - a0) / 360 * segs))
            arc = [(.42 * math.cos(math.radians(a0 + (a1 - a0) * i / n)), .42 * math.sin(math.radians(a0 + (a1 - a0) * i / n)), 0) for i in range(n + 1)]
            gk.tube(pc, arc, .13, fin_name, segs=rs, caps=False)
    gk.torus(pc, (0, 0, 0), (0, 0, 1), .59, .012, 'rubber', segs=segs, rsegs=4)
    return [pc.make()]


RIGID = [
    ('lamp', lambda t: lamp(t)), ('lamp-broken', lambda t: lamp(t, True)), ('blade', blade), ('grate', grate), ('throat', throat),
    ('rail-r', lambda t: rail(t, 1)), ('rail-l', lambda t: rail(t, -1)), ('fin-r', lambda t: fin(t, 1)), ('fin-l', lambda t: fin(t, -1)),
    ('tile', debris_tile), ('float', pool_float),
]
# How many of each the game draws (for the tier's count).
COUNT = {'lamp': 12, 'lamp-broken': 0, 'blade': 9, 'grate': 1, 'throat': 1, 'rail-r': 1, 'rail-l': 1, 'fin-r': 1, 'fin-l': 1, 'tile': 0, 'float': 0}


def tris_of(gm):
    return sum(len(s) for s in gm.submeshes) // 3


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', help='Assets/LocalAssets to write the bank into')
    ap.add_argument('--sheet', help='render a review sheet of the tier')
    ap.add_argument('--sheet-tier', type=int, default=0)
    ap.add_argument('--tiers', default='0,1,2')
    args = ap.parse_args(argv)
    finishes()
    bank = gk.GearBank()
    built = {}
    for tier in [int(x) for x in args.tiers.split(',')]:
        gk.clear_scene()
        body = build_body(tier)
        bank.add('deepend-body-%d' % tier, 'spine', body)
        built['body', tier] = body
        total = tris_of(body)
        for name, fn in RIGID:
            gk.clear_scene()
            obs = fn(tier)
            gm = bake_rigid('deepend-%s-%d' % (name, tier), obs)
            bank.add('deepend-%s-%d' % (name, tier), name, gm)
            built[name, tier] = gm
            total += tris_of(gm) * COUNT[name]
        print('tier %d: %d triangles as drawn (body %d)' % (tier, total, tris_of(body)))
    if args.sheet:
        import deep_end_boss_sheet as sheet
        sheet.render(built, TO_JOINT, args.sheet, args.sheet_tier, globals())
    if args.out:
        bank.write(args.out, BANK)


if __name__ == '__main__':
    main()
