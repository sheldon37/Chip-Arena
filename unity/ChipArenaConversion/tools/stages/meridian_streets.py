"""Meridian's street life bank (docs/expedition/MERIDIAN-STREETS.md): every kind of Core NativeMeridianStreetLife.Kinds, the
pieces that make the streets lived in (a zombie-outbreak city at night, abandoned but recently lived in) without a collider:
road markings, drains, litter, blood and chalk flat on the floor; posters, graffiti and boards flat on the walls; bin bags,
bicycles and fires within 0.25 m of a wall; air conditioners, awnings, signs, banners and floods over 3.1 m; wires, washing,
bunting and festoons across the streets; signals stuck on amber. Modelled from script on stagekit and the bleed's kit
(meridian_bleed.py: its finishes, glyphs and helpers, so the two banks share one look).

  blender --background --factory-startup --python meridian_streets.py -- --out ASSETS [--preview DIR] [--no-ao] [--only KEY,...]

Writes ASSETS/ChipArenaLocal/Resources/ChipArenaCityStreets.bytes (bank.py's format; never committed: it travels in the Drive
zip). Every kind is built once per tier: `rig/streets/<key>/0` (LOD0), `/1` (Mid, sharing LOD0's mesh) and `/2` (LOD1,
stagekit's Low), group "rig", in the kind's own frame:
  Floor        centred on the floor's top (y 0 up), z along the street;
  Wall, Low    the mount on the collider face at the floor, +z out of the wall, the viewer's right -x (authored in the city
               kit's facade frame, facing -z with +x to the viewer's right, and turned half round, as the bleed's);
  Upper        the same, its origin at the mount's height (the planner holds it 3.1 m or more over the floor);
  Span         from the cable's start, x across the street (scaled to the span by the planner), y about the cable;
  Hung         from the hanging point, y down.
Each piece is checked against its kind's envelope, read from the Core source (the one table both sides keep), so a piece can
never stand prouder or lower than the planner placed it for. Parts the runtime tints (graffiti's paint, chalk, awnings' canvas,
banners, flags) are banked white or pale, their outlines black; glowing parts are white on the glow sub-mesh (the runtime gives
each light its colour and state).
"""
import argparse
import math
import os
import re
import sys
import time

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import numpy as np  # noqa: E402

import stagekit as sk  # noqa: E402
from stagekit import Geo  # noqa: E402
import meridian_bleed as mb  # noqa: E402
from meridian_bleed import F, bx, panel, wall_rect, top_rect, slab, text_pix, pix_width, rod, h01, glow, fin, tier, part  # noqa: E402

BANK = 'ChipArenaCityStreets'
CORE = os.path.join(HERE, '..', '..', 'Assets', 'ChipArena', 'Core', 'NativeMeridianStreetLife.cs')
PI = math.pi
WHITE = 0xFFFFFF


# ---------------------------------------------------------------------------------------------------------------------------
# The kinds and their envelopes (Core NativeMeridianStreetLife.Kinds)
# ---------------------------------------------------------------------------------------------------------------------------
def kinds():
    src = open(CORE, encoding='utf-8').read()
    names = {'WallIn': .062, 'WallOut': .10, 'Reach': .25}

    def num(t):
        t = t.strip()
        if t in names:
            return names[t]
        return float(t.rstrip('f'))
    out = {}
    for m in re.finditer(r'new Kind\("([^"]+)",Mount\.(\w+),V\(([^)]*)\),V\(([^)]*)\)', src):
        out[m.group(1)] = (m.group(2), tuple(num(v) for v in m.group(3).split(',')), tuple(num(v) for v in m.group(4).split(',')))
    if len(out) < 60:
        raise SystemExit('read only %d street kinds from %s' % (len(out), CORE))
    return out


def finishes():
    mb.finishes()
    N, W, CL, P, I = sk.DETAIL_NONE, sk.DETAIL_WOOD, sk.DETAIL_CLOTH, sk.DETAIL_PLASTER, sk.DETAIL_IRON
    a = fin
    a('road-white', 0xE6E0C8, N, .30, 0, .5, 0, .6)
    a('road-yellow', 0xE0B030, N, .30, 0, .5, 0, .6)
    a('asphalt-patch', 0x26282C, N, .30, 0, .6, 0, 0)
    a('asphalt-seam', 0x141518, N, .55, 0, .3, 0, 0)
    a('manhole', 0x34343A, I, .45, .55, .6, 0, .3)
    a('manhole-rim', 0x26262A, I, .40, .5, .5, 0, .2)
    a('void', 0x08080A, N, .1, 0, 0, 0, 0)
    a('blood-dry', 0x3E0808, N, .45, 0, .2, 0, 0)
    a('blood', 0x5A0A0C, N, .70, 0, .2, 0, 0)
    a('chalk', 0xF2EEE6, N, .10, 0, .3, 0, 0)
    a('leaf-brown', 0x6A4220, CL, .25, 0, .4, 0, 0)
    a('leaf-ochre', 0x9A6A26, CL, .25, 0, .4, 0, 0)
    a('leaf-red', 0x84341A, CL, .25, 0, .4, 0, 0)
    a('cardboard', 0x9A7A4E, P, .10, 0, .7, 0, 0)
    a('cardboard-dark', 0x7A5E3A, P, .10, 0, .7, 0, 0)
    a('newsprint', 0xCFCABC, N, .12, 0, .6, 0, 0)
    a('ink', 0x2A2A2E, N, .15, 0, .3, 0, 0)
    a('glass-shard', 0x8EA6AE, N, .92, 0, .1, 0, 0)
    a('can-red', 0xA82222, N, .55, .6, .4, 0, .3)
    a('can-blue', 0x284A8A, N, .55, .6, .4, 0, .3)
    a('bottle', 0x2A4A2A, N, .85, 0, .2, 0, 0)
    a('cloth-blue', 0x2E4A7A, CL, .12, 0, .7, 0, 0)
    a('cloth-grey', 0x5A5A62, CL, .12, 0, .7, 0, 0)
    a('cloth-red', 0x8A2A2A, CL, .12, 0, .7, 0, 0)
    a('cloth-beige', 0xB0A07E, CL, .12, 0, .7, 0, 0)
    a('cloth-green', 0x3E5A3A, CL, .12, 0, .7, 0, 0)
    a('cloth-white', 0xE4E0D6, CL, .12, 0, .6, 0, 0)
    a('cloth-yellow', 0xD8B040, CL, .12, 0, .6, 0, 0)
    a('scorch', 0x121010, N, .10, 0, .2, 0, 0)
    a('tent-ground', 0x4A4A48, N, .20, 0, .6, 0, 0)
    a('rope-blue', 0x2A5A9A, CL, .2, 0, .4, 0, 0)
    a('stone-ring', 0x6A6660, sk.DETAIL_FLAG, .2, 0, .8, .1, .1)
    a('char', 0x1A1410, W, .15, 0, .3, 0, 0, param=0)
    a('flare-red', 0xC02018, N, .45, 0, .3, 0, .2)
    a('flare-cap', 0x2A2A2A, N, .3, 0, .3, 0, 0)
    a('cd-orange', 0xEE7420, N, .30, 0, .5, 0, .5)
    a('cd-olive', 0x5B6B3A, N, .40, .2, .8, 0, .3)
    a('stencil-white', 0xECEBE2, N, .30, 0, .5, 0, .5)
    a('poster-cream', 0xE6DCC2, N, .15, 0, .6, 0, 0)
    a('poster-red', 0xB82E28, N, .20, 0, .5, 0, 0)
    a('poster-blue', 0x2A468A, N, .20, 0, .5, 0, 0)
    a('poster-yellow', 0xE0BE40, N, .20, 0, .5, 0, 0)
    a('poster-green', 0x3A6E46, N, .20, 0, .5, 0, 0)
    a('photo', 0x3A3A40, N, .35, 0, .2, 0, 0)
    a('spray', 0xF2F0EA, N, .35, 0, .3, 0, 0)
    a('spray-black', 0x141416, N, .35, 0, .3, 0, 0)
    a('board-wood', 0x8A6A44, W, .20, 0, 1.0, .1, .3, param=0)
    a('board-wood-b', 0x725438, W, .20, 0, 1.0, .1, .3, param=0)
    a('nail', 0x3A3A3A, I, .4, .6, .3, 0, 0)
    a('tape-grey', 0xB8B6AE, N, .4, 0, .4, 0, 0)
    a('hazard-yellow', 0xE6BE22, N, .30, 0, .7, 0, .5)
    a('binbag', 0x18181C, N, .62, 0, .4, 0, 0)
    a('binbag-green', 0x1E2A20, N, .62, 0, .4, 0, 0)
    a('binbag-tie', 0x9A9A40, N, .4, 0, .3, 0, 0)
    a('bike-paint', 0x2A5A8A, N, .55, .2, .5, 0, .4)
    a('chrome', 0xC8CCD0, N, .85, 1.0, .3, 0, .1)
    a('tyre', 0x1C1C1E, N, .25, 0, .4, 0, 0)
    a('saddle', 0x2A1E16, N, .35, 0, .4, 0, 0)
    a('sandbag', 0x8A7A58, CL, .12, 0, 1.0, .1, 0)
    a('drum-rust', 0x5E3420, I, .35, .5, 1.2, 0, .3)
    a('drum-paint', 0x2E4A6A, I, .35, .4, 1.0, 0, .5)
    a('log', 0x4A3422, W, .15, 0, .6, 0, 0, param=0)
    a('meter-grey', 0x9A9A92, N, .40, .3, 1.0, 0, .4)
    a('phone-green', 0x24463A, N, .45, .2, .8, 0, .4)
    a('ac-white', 0xC6C4BA, N, .40, .1, 1.2, 0, .3)
    a('ac-grille', 0x3A3A3C, I, .35, .5, .6, 0, 0)
    a('trough-green', 0x2E4A30, W, .35, 0, .9, .2, .3, param=0)
    a('soil', 0x2A2016, N, .15, 0, .6, 0, 0)
    a('plant-dead', 0x6A5A38, CL, .2, 0, .5, 0, 0)
    a('plant-green', 0x3A5A2A, CL, .2, 0, .5, 0, 0)
    a('canvas', 0xEEEAE0, CL, .12, 0, .8, 0, 0)
    a('canvas-b', 0xC8C0AE, CL, .12, 0, .8, 0, 0)
    a('sign-board', 0x22222A, N, .45, .1, .7, 0, .3)
    a('gilt', 0xC8A040, sk.DETAIL_BRASS, .7, 1, .3, 0, .2)
    a('sheet', 0xEAE6DC, CL, .12, 0, .8, 0, 0)
    a('paint-red', 0xA81E18, N, .3, 0, .4, 0, 0)
    a('paint-black-b', 0x16161A, N, .3, 0, .4, 0, 0)
    a('signal-black', 0x18181C, N, .45, .2, .6, 0, .3)
    a('lens-red', 0x3A0C0A, N, .85, 0, .2, 0, 0)
    a('lens-green', 0x0A2A16, N, .85, 0, .2, 0, 0)
    a('bulb-dead', 0x3A3830, N, .7, 0, .2, 0, 0)
    a('hivis', 0xF07A18, CL, .2, 0, .5, 0, 0)
    a('hivis-band', 0xC8CCD0, N, .7, .3, .3, 0, 0)
    a('glove-blue', 0x5A8AC8, N, .55, 0, .3, 0, 0)
    a('mask-white', 0xE8ECEA, CL, .2, 0, .3, 0, 0)
    a('bio-yellow', 0xE8C820, N, .55, 0, .3, 0, 0)
    a('ticket', 0xE4D8B0, N, .2, 0, .4, 0, 0)
    a('office-paper', 0xD8CC88, N, .15, 0, .5, 0, 0)
    a('ceiling-tile-b', 0xD0CBB6, sk.DETAIL_PLASTER, .1, 0, .6, 0, 0)
    a('carpet-scrap', 0x7E7048, CL, .05, 0, .9, .1, 0)
    a('wet', 0x101418, N, .92, 0, .1, 0, 0)
    a('chlorine', 0x6AA8A0, N, .6, 0, .2, 0, 0)
    a('soot', 0x161412, N, .1, 0, .2, 0, 0)
    a('flag-pole', 0x9AA0A4, N, .5, .8, .5, 0, .1)
    a('flood-lens', 0xDDE8F0, N, .9, 0, .2, 0, 0)
    return F


# ---------------------------------------------------------------------------------------------------------------------------
# Small tools (frames as the bleed's: floor pieces centred on y 0; facade pieces facing -z, x to the viewer's right)
# ---------------------------------------------------------------------------------------------------------------------------
def poly_up(g, pts, y, f):
    """A flat polygon on the plane y facing up, from (x, z) points (any winding; convex or near it)."""
    p3 = [(x, y, z) for (x, z) in pts]
    g.face(p3, f, (0, 1, 0))


def strip(g, a, b, w, y, f):
    """A flat strip w wide from a to b on the floor at y (x, z points)."""
    dx, dz = b[0] - a[0], b[1] - a[1]
    l = math.hypot(dx, dz) or 1
    nx, nz = -dz / l * w / 2, dx / l * w / 2
    g.face(((a[0] - nx, y, a[1] - nz), (b[0] - nx, y, b[1] - nz), (b[0] + nx, y, b[1] + nz), (a[0] + nx, y, a[1] + nz)), f, (0, 1, 0))


def blob(g, cx, cz, r, y, f, seed, m=10, stretch=1.0, rot=0.0, lim=.98):
    pts = []
    c, s = math.cos(rot), math.sin(rot)
    for k in range(m):
        a = 2 * PI * k / m
        rr = r * (.7 + .55 * h01(seed, k))
        x, z = math.cos(a) * rr * stretch, math.sin(a) * rr
        pts.append((max(-lim, min(lim, cx + x * c - z * s)), max(-lim, min(lim, cz + x * s + z * c))))
    poly_up(g, pts, y, f)


def rect_up(g, cx, cz, w, l, ang, y, f):
    c, s = math.cos(ang), math.sin(ang)
    pts = [(cx + c * u - s * v, cz + s * u + c * v) for u, v in ((-w / 2, -l / 2), (w / 2, -l / 2), (w / 2, l / 2), (-w / 2, l / 2))]
    poly_up(g, pts, y, f)


def floor_text(g, s, cx, cz, px, f, y=.004, along=1.0, tall=1.0):
    """5 x 7 lettering laid on the floor, read walking toward +z (along -1: toward -z), centred on (cx, cz), its letters `tall`
    times longer along the street than wide (road paint is drawn long, to read at a grazing angle)."""
    t = Geo()
    w = text_pix(t, s, 0, 0, 0, px, f, depth=0.0)
    h = 7 * px * tall
    # the glyph frame (x right, y up, facing -z) onto the floor: x, up along +z, the face's -z up
    if along > 0:
        m = [1.0, 0, 0, cx, 0, 0, -1.0, y, 0, tall, 0, cz - h / 2]
    else:
        m = [-1.0, 0, 0, cx, 0, 0, -1.0, y, 0, -tall, 0, cz + h / 2]
    g.extend(t.transformed(m))
    return w


def spray_text(g, T, s, x, y, h, f, seed, z=-.064, slant=.12, drips=True):
    """Sprayed lettering on a wall (facade frame), `h` tall, centred on x, its foot at y: the 5 x 7 face's strokes made rough and
    leaning, drips running down under some strokes."""
    px = h / 7.0
    w = pix_width(s, px)
    cx = x - w / 2
    k = 0
    for ch in s:
        rows = mb.PIX.get(ch, mb.PIX[' ']).split(',')
        cw = len(rows[0])
        for r, row in enumerate(rows):
            yy = y + (6 - r) * px
            for (i0, i1) in mb._runs(row):
                j = h01(seed, ch, k, r, i0)
                a0, a1 = cx + i0 * px - .1 * px, cx + i1 * px + .1 * px
                b0, b1 = yy - .08 * px, yy + px * (1.06 + .1 * j)
                sl0, sl1 = slant * (b0 - y), slant * (b1 - y)
                g.face(((a0 + sl0, b0, z), (a1 + sl0, b0, z), (a1 + sl1, b1, z), (a0 + sl1, b1, z)), f, (0, 0, -1))
                if drips and T.tiny and r == 6 and j < .45:
                    dl = (.04 + .14 * j) * h
                    dx = a0 + (a1 - a0) * (.2 + .6 * h01(seed, 'd', k, i0))
                    g.face(((dx - .006, b0 - dl, z), (dx + .006, b0 - dl, z), (dx + .008, b0, z), (dx - .008, b0, z)), f, (0, 0, -1))
                k += 1
        cx += (cw + 1) * px
    return w


def sheet(g, x0, x1, y0, y1, z, f, torn=0.0, seed=0, n=8):
    """A paper sheet on a wall (facade frame) with a ragged lower edge (torn > 0)."""
    if torn <= 0:
        g.face(((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)), f, (0, 0, -1))
        return
    pts = [(x0, y1, z), (x1, y1, z)]
    for k in range(n + 1):
        t = k / n
        pts.append((x1 + (x0 - x1) * t, y0 + torn * h01(seed, k), z))
    g.face(pts, f, (0, 0, -1))


def lines(g, x0, x1, ytop, rows, pitch, thick, z, f, seed=0):
    """Text lines (flat strokes) on a sheet, ragged at their right ends."""
    for r in range(rows):
        y = ytop - r * pitch
        e = x1 - (x1 - x0) * (.05 + .35 * h01(seed, 'l', r))
        g.face(((x0, y - thick, z), (e, y - thick, z), (e, y, z), (x0, y, z)), f, (0, 0, -1))


def tape(g, x, y, z, ang, f):
    c, s = math.cos(ang), math.sin(ang)
    w, h = .05, .022
    pts = [(x + c * u - s * v, y + s * u + c * v, z) for u, v in ((-w, -h), (w, -h), (w, h), (-w, h))]
    g.face(pts, f, (0, 0, -1))


def turn_side(sub, side):
    """Text drawn facing -z (x right) onto a plane facing +x (side 1, letters running along +z) or -x (side -1, along -z)."""
    if side > 0:
        return sub.transformed([0, 0, -1.0, 0, 0, 1.0, 0, 0, 1.0, 0, 0, 0])
    return sub.transformed([0, 0, 1.0, 0, 0, 1.0, 0, 0, -1.0, 0, 0, 0])


def ellipsoid(g, c, r, f, n=10, m=6):
    """A closed ellipsoid (lat-long), radii r = (rx, ry, rz), smooth."""
    rows = []
    for j in range(m + 1):
        b = -PI / 2 + PI * j / m
        rows.append([(c[0] + r[0] * math.cos(b) * math.cos(2 * PI * i / n), c[1] + r[1] * math.sin(b), c[2] + r[2] * math.cos(b) * math.sin(2 * PI * i / n)) for i in range(n)])

    def nrm(q):
        return sk._norm(((q[0] - c[0]) / max(r[0], 1e-4) ** 2, (q[1] - c[1]) / max(r[1], 1e-4) ** 2, (q[2] - c[2]) / max(r[2], 1e-4) ** 2))
    for j in range(m):
        for i in range(n):
            p0, p1, p2, p3 = rows[j][i], rows[j][(i + 1) % n], rows[j + 1][(i + 1) % n], rows[j + 1][i]
            face = (p3, p2, p0) if j == 0 else (p3, p1, p0) if j == m - 1 else (p3, p2, p1, p0)
            g.face(face, f, normals=tuple(nrm(q) for q in face))


# ---------------------------------------------------------------------------------------------------------------------------
# Floor: the layout's markings and fittings
# ---------------------------------------------------------------------------------------------------------------------------
def worn_strip(g, T, x0, x1, z0, z1, y, f, seed, gaps=3):
    """A painted strip worn into pieces along z (the traffic's wear), with chipped ends."""
    n = gaps + 1 if T.lod == 0 else 1
    edges = sorted([z0, z1] + [z0 + (z1 - z0) * h01(seed, 'g', k) for k in range(n - 1)])
    for k in range(len(edges) - 1):
        a, b = edges[k] + (.02 if k else 0), edges[k + 1] - (.02 if k < len(edges) - 2 else 0)
        if b - a < .05 or (T.lod == 0 and h01(seed, 'w', k) < .12):
            continue
        top_rect(g, T, x0, x1, a, b, y, f, cell=0)


def floor_dash(g, T):
    worn_strip(g, T, -.075, .075, -1, 1, .004, F['road-white'], 'dash')


def floor_yellow(g, T):
    for x, s in ((-.1, 'ya'), (.1, 'yb')):
        worn_strip(g, T, x - .05, x + .05, -1, 1, .004, F['road-yellow'], s, 2)


def floor_zebra(g, T):
    for x, s in ((-.5, 'za'), (.5, 'zb')):
        worn_strip(g, T, x - .25, x + .25, -.98, .98, .004, F['road-white'], s, 2)


def floor_box(g, T):
    # the box junction's yellow hatch: lines at 45 degrees both ways, 0.7 m apart, clipped to the 2 m tile
    def clip(a, b):
        # a segment a-b (x, z) clipped to [-.98, .98]^2 (Liang-Barsky)
        t0, t1 = 0.0, 1.0
        dx, dz = b[0] - a[0], b[1] - a[1]
        for p, q in ((-dx, a[0] + .92), (dx, .92 - a[0]), (-dz, a[1] + .92), (dz, .92 - a[1])):
            if abs(p) < 1e-9:
                if q < 0:
                    return None
                continue
            r = q / p
            if p < 0:
                t0 = max(t0, r)
            else:
                t1 = min(t1, r)
        if t0 >= t1:
            return None
        return (a[0] + dx * t0, a[1] + dz * t0), (a[0] + dx * t1, a[1] + dz * t1)
    for sign in (1, -1):
        for k in range(-3, 4):
            c = k * .7
            seg = clip((c - 2, -2 * sign), (c + 2, 2 * sign))
            if seg:
                strip(g, seg[0], seg[1], .1, .004 if sign > 0 else .005, F['road-yellow'])


def floor_slow(g, T):
    floor_text(g, 'SLOW', 0, 0, .068, F['road-white'], .004, tall=3.6)


def floor_bus(g, T):
    floor_text(g, 'BUS', 0, .1, .09, F['road-yellow'], .004, tall=2.2)
    for (a, b) in (((-.9, -.95), (.9, -.95)), ((-.9, .95), (.9, .95)), ((-.9, -.95), (-.9, .95)), ((.9, -.95), (.9, .95))):
        strip(g, a, b, .08, .0045, F['road-yellow'])


def floor_manhole(g, T):
    n = 20 if T.lod == 0 else 8
    ring = [(math.cos(2 * PI * k / n) * .4, math.sin(2 * PI * k / n) * .4) for k in range(n)]
    poly_up(g, ring, .006, F['manhole-rim'])
    poly_up(g, [(x * .88, z * .88) for x, z in ring], .012, F['manhole'])
    if T.detail:
        # the city's crest (a clock face over three stepped hills) and the radial ribs
        for k in range(12):
            a = 2 * PI * k / 12
            strip(g, (math.cos(a) * .12, math.sin(a) * .12), (math.cos(a) * .33, math.sin(a) * .33), .02, .016, F['manhole-rim'])
        m = 12
        poly_up(g, [(math.cos(2 * PI * k / m) * .1, math.sin(2 * PI * k / m) * .1) for k in range(m)], .017, F['manhole-rim'])
        floor_text(g, 'MCC', 0, -.22, .018, F['manhole-rim'], .0175)


def floor_drain(g, T):
    slab(g, T, -.3, .3, -.2, .2, 0, .008, F['iron-black'], cell=0)
    top_rect(g, T, -.26, .26, -.16, .16, .009, F['void'], cell=0)
    n = 8 if T.lod == 0 else 3
    for i in range(n):
        x = -.24 + .48 * (i + .5) / n
        slab(g, T, x - .015, x + .015, -.17, .17, .008, .015, F['manhole'], cell=0)


def floor_grate(g, T):
    slab(g, T, -.7, .7, -.45, .45, 0, .01, F['manhole-rim'], cell=0)
    top_rect(g, T, -.64, .64, -.39, .39, .011, F['void'], cell=0)
    n = 14 if T.lod == 0 else 5
    for i in range(n):
        x = -.6 + 1.2 * (i + .5) / n
        slab(g, T, x - .018, x + .018, -.4, .4, .01, .02, F['manhole'], cell=0)
    if T.detail:
        for z in (-.2, .2):
            slab(g, T, -.65, .65, z - .015, z + .015, .01, .019, F['manhole'], cell=0)


# ---------------------------------------------------------------------------------------------------------------------------
# Floor: the long night's wear and the story
# ---------------------------------------------------------------------------------------------------------------------------
def crack_path(seed, n=6, spread=.9):
    pts = [(-spread + 2 * spread * h01(seed, 'x0'), -.95)]
    for k in range(1, n):
        x = pts[-1][0] + (h01(seed, 'dx', k) - .5) * .5
        pts.append((max(-.95, min(.95, x)), -.95 + 1.9 * k / (n - 1)))
    return pts


def floor_crack(g, T, v):
    pts = crack_path(('crack', v), 7)
    mb.crackline(g, pts, .003, F['crack'], .014)
    if T.detail:
        k = 2 + v
        a = pts[k]
        b = (max(-.95, min(.95, a[0] + (.5 if v else -.5))), max(-.95, min(.95, a[1] + .3)))
        mb.crackline(g, [a, ((a[0] + b[0]) / 2 + .08, (a[1] + b[1]) / 2), b], .0032, F['crack'], .01)
        blob(g, pts[4][0], pts[4][1], .18, .002, F['oil'], ('cs', v), 8, 1.3)


def floor_patch(g, T):
    pts = [(-.75, -.6), (.7, -.7), (.8, .55), (-.6, .72)]
    poly_up(g, pts, .003, F['asphalt-patch'])
    for a, b in zip(pts, pts[1:] + pts[:1]):
        strip(g, a, b, .03, .0045, F['asphalt-seam'])


def floor_oil(g, T):
    blob(g, .1, .05, .5, .003, F['oil'], 'oil', 14 if T.lod == 0 else 7, 1.4)
    blob(g, -.35, .4, .22, .004, F['oil'], 'oil2', 10 if T.lod == 0 else 5, 1.2)
    if T.detail:
        for k in range(5):
            blob(g, -.7 + 1.4 * h01('od', k), -.7 + 1.4 * h01('oz', k), .04 + .03 * h01('or', k), .0042, F['oil'], ('od', k), 6, 1.0)


def floor_skid(g, T):
    n = 10 if T.lod == 0 else 4
    for x0 in (-.45, .45):
        pts = [(x0 + .25 * math.sin(1.2 * (-1 + 2 * k / n)), -.98 + 1.96 * k / n) for k in range(n + 1)]
        for a, b in zip(pts, pts[1:]):
            strip(g, a, b, .2, .003, F['tyre'])


def floor_scorch(g, T):
    blob(g, 0, 0, .8, .003, F['scorch'], 'sc', 16 if T.lod == 0 else 8, 1.15)
    blob(g, .1, -.1, .45, .005, F['ash'], 'sc2', 12 if T.lod == 0 else 6, 1.1)
    if T.detail:
        for k in range(9):
            x, z = -.6 + 1.2 * h01('sb', k), -.6 + 1.2 * h01('sz', k)
            a = 2 * PI * h01('sa', k)
            bx(g, T, x - .06, x + .06, .005, .02 + .02 * h01('sh', k), z - .03, z + .03, F['char'], bevel=0) if k % 2 else \
                rect_up(g, x, z, .12, .04, a, .007, F['ash-dark'])


def floor_paper(g, T):
    mb.floor_litter(g, T)


def floor_news(g, T):
    n = 6 if T.lod == 0 else 3
    for i in range(n):
        x, z = -.7 + 1.4 * h01('nx', i), -.7 + 1.4 * h01('nz', i)
        a = 2 * PI * h01('na', i)
        y = .004 + .002 * i
        rect_up(g, x, z, .42, .3, a, y, F['newsprint'])
        if T.detail:
            c, s = math.cos(a), math.sin(a)
            for r in range(4):
                v = -.1 + r * .06
                u0, u1 = -.17, .17 - .1 * h01('nl', i, r)
                strip(g, (x + c * u0 - s * v, z + s * u0 + c * v), (x + c * u1 - s * v, z + s * u1 + c * v), .018, y + .0006, F['ink'])
            strip(g, (x + c * -.17 - s * .1, z + s * -.17 + c * .1), (x + c * .17 - s * .1, z + s * .17 + c * .1), .05, y + .0007, F['ink'])


def floor_leaves(g, T):
    n = 60 if T.lod == 0 else 14
    fs = (F['leaf-brown'], F['leaf-ochre'], F['leaf-red'])
    for i in range(n):
        # a drift: thicker toward -x (the kerb or the wall side)
        u = h01('lu', i) ** 1.8
        x, z = -.86 + 1.72 * u, -.86 + 1.72 * h01('lz', i)
        a = 2 * PI * h01('la', i)
        y = .003 + .0004 * (i % 25)
        c, s = math.cos(a), math.sin(a)
        L, W = .07 + .04 * h01('ll', i), .045
        pts = [(x + c * u_ - s * v_, z + s * u_ + c * v_) for u_, v_ in ((-L, 0), (0, -W), (L, 0), (0, W))]
        poly_up(g, pts, y, fs[i % 3])


def floor_glass(g, T):
    n = 26 if T.lod == 0 else 8
    for i in range(n):
        x, z = -.8 + 1.6 * h01('gx', i), -.8 + 1.6 * h01('gz', i)
        r = .03 + .07 * h01('gr', i)
        a = 2 * PI * h01('ga', i)
        pts = [(x + math.cos(a + 2.1 * k) * r * (.6 + .5 * h01('gk', i, k)), z + math.sin(a + 2.1 * k) * r) for k in range(3)]
        poly_up(g, pts, .003 + .0005 * (i % 6), F['glass-shard'])
        if i % 5 == 0:
            poly_up(g, [(p[0] * .6 + x * .4, p[1] * .6 + z * .4) for p in pts], .0065, glow(0xBFD8E8, .7))


def floor_cans(g, T):
    for i in range(4 if T.lod == 0 else 2):
        x, z = -.6 + 1.2 * h01('cx', i), -.6 + 1.2 * h01('cz', i)
        a = 2 * PI * h01('ca', i)
        c, s = math.cos(a), math.sin(a)
        f = F['can-red'] if i % 2 else F['can-blue']
        bx_ = Geo()
        bx(bx_, T, -.06, .06, 0, .03, -.033, .033, f, bevel=.01)
        g.extend(bx_.transformed([c, 0, s, x, 0, 1, 0, 0, -s, 0, c, z]))
    # a bottle lying, flattened to the paint's height, and a bottle top
    sub = Geo()
    bx(sub, T, -.12, .1, 0, .05, -.035, .035, F['bottle'], bevel=.015)
    bx(sub, T, .1, .17, .01, .04, -.013, .013, F['bottle'], bevel=0)
    a = 2 * PI * h01('bt', 0)
    c, s = math.cos(a), math.sin(a)
    g.extend(sub.transformed([c, 0, s, .3, 0, 1, 0, 0, -s, 0, c, -.35]))


def floor_clothes(g, T):
    # a dropped coat, its sleeves out, and a scarf
    a = 2 * PI * h01('coat', 0)
    c, s = math.cos(a), math.sin(a)
    poly_up(g, [(c * x - s * z, s * x + c * z) for x, z in [(-.32, -.45), (.32, -.45), (.36, .25), (-.36, .25)]], .01, F['cloth-blue'])
    poly_up(g, [(c * x - s * z, s * x + c * z) for x, z in [(-.36, .25), (.36, .25), (.12, .45), (-.12, .45)]], .012, F['cloth-blue'])
    for sx in (1, -1):
        poly_up(g, [(c * x * sx - s * z, s * x * sx + c * z) for x, z in [(.3, .2), (.4, .3), (.62, .58), (.52, .64), (.28, .36)]], .011, F['cloth-blue'])
    if T.detail:
        poly_up(g, [(c * x - s * z, s * x + c * z) for x, z in [(-.02, -.4), (.02, -.4), (.02, .36), (-.02, .36)]], .0125, F['ink'])
    pts = [(-.7, -.8), (-.4, -.55), (-.1, -.7), (.2, -.5), (.5, -.75)]
    for p, q in zip(pts, pts[1:]):
        strip(g, p, q, .11, .006, F['cloth-red'])


def floor_card(g, T):
    for i in range(3 if T.lod == 0 else 2):
        x, z = -.4 + .8 * h01('kx', i), -.4 + .8 * h01('kz', i)
        a = 2 * PI * h01('ka', i)
        rect_up(g, x, z, .7, .5, a, .006 + .006 * i, F['cardboard'] if i % 2 == 0 else F['cardboard-dark'])
        if T.detail:
            c, s = math.cos(a), math.sin(a)
            strip(g, (x + c * -.35 - s * 0, z + s * -.35), (x + c * .35, z + s * .35), .012, .0062 + .006 * i, F['cardboard-dark'])


def floor_blood_pool(g, T):
    blob(g, 0, 0, .45, .003, F['blood-dry'], 'bp', 14 if T.lod == 0 else 7, 1.3)
    blob(g, .05, -.05, .3, .0045, F['blood'], 'bp2', 12 if T.lod == 0 else 6, 1.2)
    if T.detail:
        for k in range(10):
            a = 2 * PI * h01('bs', k)
            r = .55 + .35 * h01('br', k)
            blob(g, math.cos(a) * r, math.sin(a) * r * .8, .025 + .03 * h01('bz', k), .0035, F['blood-dry'], ('bs', k), 6)


def floor_blood_trail(g, T):
    n = 9 if T.lod == 0 else 4
    for k in range(n):
        z0, z1 = -.95 + 1.9 * k / n, -.95 + 1.9 * (k + 1) / n
        w = .32 * (1 - .5 * k / n) * (.7 + .5 * h01('tw', k))
        x = .08 * math.sin(k * 1.3)
        top_rect(g, T, x - w / 2, x + w / 2, z0, z1 + .02, .003 + .0003 * (k % 2), F['blood-dry'], cell=0)
        if T.detail and k % 2 == 0:
            top_rect(g, T, x - w / 5, x + w / 5, z0, z1, .0045, F['blood'], cell=0)


def floor_blood_prints(g, T):
    for k in range(6):
        side = 1 if k % 2 else -1
        x, z = side * .12, -.85 + .32 * k
        f = F['blood'] if k < 3 else F['blood-dry']
        rect_up(g, x, z, .1, .2 - .015 * k, .05 * side, .003, f)
        if T.detail:
            rect_up(g, x, z + .14, .09, .07, 0, .003, f)


def floor_chalk_hop(g, T):
    # hopscotch: 1, 2, 3-4, 5, 6-7, 8 and the HOME arc, chalk (tinted)
    f = F['chalk']
    cells = [(0, -.8), (0, -.5), (-.15, -.2), (.15, -.2), (0, .1), (-.15, .4), (.15, .4), (0, .7)]
    for (x, z) in cells:
        w = .3
        for a, b in (((x - w / 2, z - .15), (x + w / 2, z - .15)), ((x - w / 2, z + .15), (x + w / 2, z + .15)), ((x - w / 2, z - .15), (x - w / 2, z + .15)), ((x + w / 2, z - .15), (x + w / 2, z + .15))):
            strip(g, a, b, .025, .003, f)
    for k, (x, z) in enumerate(cells):
        if T.detail:
            floor_text(g, str(k + 1), x, z, .025, f, .0034)


def floor_chalk_note(g, T):
    f = F['chalk']
    floor_text(g, 'WATER', -.15, .35, .045, f, .003)
    # the arrow, and a tally under it
    strip(g, (-.6, -.05), (.4, -.05), .04, .003, f)
    strip(g, (.4, -.05), (.25, .07), .04, .0031, f)
    strip(g, (.4, -.05), (.25, -.17), .04, .0031, f)
    for k in range(5):
        if k < 4:
            strip(g, (-.5 + k * .08, -.5), (-.5 + k * .08, -.3), .025, .003, f)
        else:
            strip(g, (-.55, -.45), (-.2, -.35), .025, .0032, f)


def floor_cd_assembly(g, T):
    for (a, b) in (((-.95, -.95), (.95, -.95)), ((-.95, .95), (.95, .95)), ((-.95, -.95), (-.95, .95)), ((.95, -.95), (.95, .95))):
        strip(g, a, b, .07, .004, F['cd-orange'])
    floor_text(g, 'ASSEMBLY', 0, .25, .031, F['stencil-white'], .0045)
    floor_text(g, 'POINT', 0, -.1, .038, F['stencil-white'], .0045)
    floor_text(g, 'CD', 0, -.6, .05, F['cd-orange'], .0045)


def floor_cd_lane(g, T):
    # the queue's lane: the orange arrow toward the gate (+z), the lane's edges
    strip(g, (0, -.9), (0, .3), .16, .004, F['cd-orange'])
    poly_up(g, [(-.32, .3), (.32, .3), (0, .85)], .004, F['cd-orange'])
    for x in (-.95, .95):
        worn_strip(g, T, x - .03, x + .03, -1, 1, .0042, F['stencil-white'], ('lane', x), 2)


def floor_tent(g, T):
    # where a tent stood: a paler, drier rectangle, its pegs' holes and a guy line left lying
    top_rect(g, T, -.85, .85, -.75, .75, .003, F['tent-ground'], cell=0)
    if T.detail:
        for (x, z) in ((-.88, -.78), (.88, -.78), (.88, .78), (-.88, .78), (0, -.95), (0, .95)):
            top_rect(g, T, x - .02, x + .02, z - .02, z + .02, .0045, F['void'], cell=0)
        strip(g, (-.6, .9), (.3, .55), .012, .006, F['rope-blue'])
        strip(g, (.3, .55), (.7, .8), .012, .006, F['rope-blue'])


def floor_ash(g, T):
    blob(g, 0, 0, .55, .004, F['ash'], 'ash', 14 if T.lod == 0 else 7, 1.0)
    blob(g, 0, 0, .3, .006, F['ash-dark'], 'ash2', 10 if T.lod == 0 else 5, 1.0)
    n = 9 if T.lod == 0 else 5
    for k in range(n):
        a = 2 * PI * k / n
        r = .66
        bx(g, T, math.cos(a) * r - .06, math.cos(a) * r + .06, 0, .025 + .012 * h01('st', k), math.sin(a) * r - .05, math.sin(a) * r + .05, F['stone-ring'], bevel=.012)
    if T.detail:
        for k in range(4):
            a = PI * h01('lg', k)
            sub = Geo()
            bx(sub, T, -.22, .22, 0, .035, -.025, .025, F['char'], bevel=.008)
            g.extend(sub.transformed([math.cos(a), 0, math.sin(a), .1 * (k - 1.5), 0, 1, 0, .002, -math.sin(a), 0, math.cos(a), .05 * (k % 2)]))


def floor_flare(g, T, lit):
    # a road flare: the red stick lying, its cap, the burning tip (white on the glow mesh: the runtime gives it its red), and
    # the scorch it lays
    blob(g, 0, .14, .14, .002, F['scorch'], 'flr', 10 if T.lod == 0 else 6, 1.0)
    sub = Geo()
    rod(sub, (0, .016, -.14), (0, .016, .12), .016, F['flare-red'] if lit else F['char'], T.small)
    rod(sub, (0, .016, -.17), (0, .016, -.14), .017, F['flare-cap'], T.small)
    if lit:
        rod(sub, (0, .016, .12), (0, .016, .16), .013, glow(WHITE, 3.0), T.small)
    g.extend(sub)


def rumour_level0(g, T):
    # Level 0's: yellowed office paper, a fallen ceiling tile, a scrap of its carpet
    for i in range(7 if T.lod == 0 else 3):
        x, z = -.7 + 1.4 * h01('l0x', i), -.7 + 1.4 * h01('l0z', i)
        rect_up(g, x, z, .21, .297, 2 * PI * h01('l0a', i), .004 + .0012 * i, F['office-paper'])
    sub = Geo()
    bx(sub, T, -.3, .3, 0, .015, -.3, .3, F['ceiling-tile-b'], bevel=0)
    a = .4
    g.extend(sub.transformed([math.cos(a), 0, math.sin(a), .35, 0, 1, 0, .012, -math.sin(a), 0, math.cos(a), .4]))
    rect_up(g, -.4, -.45, .5, .35, .3, .013, F['carpet-scrap'])


def rumour_pool(g, T):
    # the Poolrooms': wet footprints walking out of the water, and a chlorine stain
    blob(g, .3, -.4, .4, .002, F['chlorine'], 'pc', 12 if T.lod == 0 else 6, 1.4)
    for k in range(7):
        side = 1 if k % 2 else -1
        rect_up(g, -.3 + side * .1, -.9 + .28 * k, .09, .2, 0, .004, F['wet'])
        if T.detail:
            rect_up(g, -.3 + side * .1, -.77 + .28 * k, .08, .06, 0, .004, F['wet'])


def rumour_ward(g, T):
    # Ward Zero's: dropped gloves and masks, a flattened biohazard bag
    rect_up(g, .25, .2, .5, .6, .3, .004, F['bio-yellow'])
    if T.detail:
        floor_text(g, 'BIO', .25, .2, .03, F['ink'], .0045)
    for i in range(4):
        x, z = -.6 + .5 * h01('gx', i), -.5 + 1.0 * h01('gz', i)
        a = 2 * PI * h01('ga', i)
        c, s = math.cos(a), math.sin(a)
        pts = [(x + c * u - s * v, z + s * u + c * v) for u, v in ((-.05, -.12), (.05, -.12), (.07, .02), (.03, .12), (-.06, .08))]
        poly_up(g, pts, .005 + .001 * i, F['glove-blue'])
    for i in range(2):
        rect_up(g, -.5 + .6 * i, .7 - .2 * i, .17, .1, .5 * i, .006, F['mask-white'])


def rumour_station(g, T):
    # Central Station's: tickets, a luggage tag on its string, a dropped timetable
    for i in range(9 if T.lod == 0 else 4):
        x, z = -.8 + 1.6 * h01('tx', i), -.8 + 1.6 * h01('tz', i)
        rect_up(g, x, z, .06, .03, 2 * PI * h01('ta', i), .004 + .0007 * i, F['ticket'])
    rect_up(g, .3, -.3, .3, .42, .2, .005, F['paper'])
    if T.detail:
        for r in range(6):
            rect_up(g, .3 - .02 * r, -.3 + .14 - .05 * r, .22, .012, .2, .0056, F['ink'])
    rect_up(g, -.4, .4, .07, .12, .8, .006, F['cardboard'])
    strip(g, (-.4, .46), (-.2, .62), .006, .0062, F['ink'])


def rumour_iron(g, T):
    # the Ironworks': soot smears, a dropped hi-vis vest, rust flakes
    blob(g, -.3, .2, .5, .002, F['soot'], 'is', 12 if T.lod == 0 else 6, 1.5)
    vest = [(-.25, -.3), (.25, -.3), (.27, .25), (.12, .32), (0, .1), (-.12, .32), (-.27, .25)]
    poly_up(g, [(x + .3, z - .3) for x, z in vest], .006, F['hivis'])
    for zz in (-.15, 0.0):
        rect_up(g, .3, zz - .3, .5, .04, 0, .007, F['hivis-band'])
    for k in range(8 if T.lod == 0 else 3):
        rect_up(g, -.8 + 1.6 * h01('rf', k), -.8 + 1.6 * h01('rz', k), .05, .03, 2 * PI * h01('ra', k), .0035, F['rust'])


# ---------------------------------------------------------------------------------------------------------------------------
# Walls: flat art (facade frame: the face at z 0, the art 6.2-10 cm toward -z, x to the viewer's right, y up from the floor)
# ---------------------------------------------------------------------------------------------------------------------------
Z0 = -.064   # a wall piece's paper lies just past the facades' 6 cm hugging band


def poster_stay(g, T):
    sheet(g, -.4, .4, 1.22, 2.33, Z0, F['poster-cream'], .05, 'ps')
    wall_rect(g, T, -.36, .36, 2.0, 2.27, Z0 - .002, F['poster-red'], -1, 0)
    text_pix(g, 'STAY', 0, 1.74, Z0 - .002, .028, F['stencil-black'], depth=.0015)
    text_pix(g, 'INDOORS', 0, 1.52, Z0 - .002, .019, F['stencil-black'], depth=.0015)
    if T.detail:
        text_pix(g, 'BY ORDER', 0, 2.08, Z0 - .0035, .012, F['poster-cream'], depth=.0015)
        lines(g, -.32, .32, 1.42, 3, .05, .012, Z0 - .002, F['ink'], 'ps')
        for (x, y) in ((-.38, 2.3), (.38, 2.3)):
            tape(g, x, y, Z0 - .004, .5 if x < 0 else -.5, F['tape-grey'])


def poster_missing(g, T):
    sheet(g, -.25, .25, 1.32, 2.02, Z0, F['paper'], .03, 'pm')
    text_pix(g, 'MISSING', 0, 1.9, Z0 - .002, .011, F['poster-red'], depth=.0015)
    wall_rect(g, T, -.13, .13, 1.58, 1.86, Z0 - .002, F['photo'], -1, 0)
    if T.detail:
        lines(g, -.2, .2, 1.52, 3, .04, .01, Z0 - .002, F['ink'], 'pm')
        tape(g, 0, 2.0, Z0 - .004, 0, F['tape-grey'])


def poster_cd(g, T):
    sheet(g, -.44, .44, 1.27, 2.28, Z0, F['poster-cream'], 0, 'pcd')
    wall_rect(g, T, -.44, .44, 2.12, 2.28, Z0 - .002, F['cd-olive'], -1, 0)
    text_pix(g, 'CIVIL DEFENCE', 0, 2.16, Z0 - .003, .009, F['stencil-white'], depth=.0012)
    text_pix(g, 'NOTICE', 0, 1.96, Z0 - .002, .019, F['stencil-black'], depth=.0015)
    lines(g, -.38, .38, 1.88, 7 if T.lod == 0 else 3, .07, .014, Z0 - .002, F['ink'], 'pcd')
    if T.detail:
        text_pix(g, 'ROUTE 647', 0, 1.33, Z0 - .002, .012, F['cd-orange'], depth=.0015)


def poster_bills(g, T):
    # bills in layers on a hoarding: a gig, the Rialto, the fair, a lost cat; torn, pasted over
    layers = [(-1.05, -.25, .95, 2.05, 'poster-yellow', 'THE 647S', 'ink'), (-.45, .3, 1.15, 2.45, 'poster-blue', 'RIALTO', 'poster-cream'),
              (.2, 1.05, .92, 2.2, 'poster-green', 'FAIR', 'poster-cream'), (-.85, -.25, 1.4, 1.95, 'paper', 'LOST CAT', 'ink'),
              (.45, .95, 1.5, 2.3, 'poster-red', 'STAY IN', 'poster-cream')]
    z = Z0
    for k, (x0, x1, y0, y1, fn, title, tf) in enumerate(layers):
        if T.lod > 0 and k == 3:
            continue
        sheet(g, x0, x1, y0, y1, z, F[fn], .08 * h01('bt', k), ('bl', k))
        px = min((x1 - x0) * .8 / max(pix_width(title, 1.0), 1), .03)
        if T.lod == 0 or k == 1:
            text_pix(g, title, (x0 + x1) / 2, y1 - .14 - 7 * px, z - .0015, px, F[tf], depth=.0012)
        if T.detail:
            lines(g, x0 + .06, x1 - .06, y1 - .3 - 7 * px, 3, .05, .012, z - .0015, F[tf], ('bl', k))
        z -= .0045


def tag(g, T, seed):
    # a tag: three swooping strokes, white (the runtime tints the paint), a black outline under them
    pts_sets = []
    for s in range(3):
        n = 7
        x0 = -.95 + .6 * s + .1 * h01(seed, 'x', s)
        pts = [(x0 + .3 * k / n + .12 * math.sin(k * 1.7 + s), .7 + 1.1 * h01(seed, 'y', s, k)) for k in range(n)]
        pts_sets.append(pts)
    pts_sets.append([(-1.0 + 2.0 * k / 8, 1.0 + .25 * math.sin(k * 1.1)) for k in range(9)])
    for lay, (f, w, z) in enumerate(((F['spray-black'], .1, Z0), (F['spray'], .06, Z0 - .002))):
        for pts in pts_sets:
            for a, b in zip(pts, pts[1:]):
                dx, dy = b[0] - a[0], b[1] - a[1]
                l = math.hypot(dx, dy) or 1
                nx, ny = -dy / l * w / 2, dx / l * w / 2
                g.face(((a[0] - nx, a[1] - ny, z), (b[0] - nx, b[1] - ny, z), (b[0] + nx, b[1] + ny, z), (a[0] + nx, a[1] + ny, z)), f, (0, 0, -1))
    if T.detail:
        for k in range(4):
            x = -.8 + 1.6 * h01(seed, 'dr', k)
            y = .6 + .2 * h01(seed, 'dy', k)
            g.face(((x - .008, y - .2, Z0 - .003), (x + .008, y - .2, Z0 - .003), (x + .01, y, Z0 - .003), (x - .01, y, Z0 - .003)), F['spray'], (0, 0, -1))


MESSAGES = ('THEY HEAR YOU', 'NO WAY OUT', 'GONE TO CAMP', 'DONT OPEN')


def graffiti_msg(g, T, k):
    s = MESSAGES[k]
    words = s.split(' ')
    rows = [' '.join(words[:len(words) // 2 + (len(words) % 2)]), ' '.join(words[len(words) // 2 + (len(words) % 2):])] if len(s) > 10 else [s]
    rows = [r for r in rows if r]
    h = .3 if len(rows) == 2 else .36
    for i, r in enumerate(rows):
        y = 2.1 - i * (h + .14) - h
        w = pix_width(r, h / 7)
        hh = h * min(1.0, 2.2 / max(w, 1e-3))
        spray_text(g, T, r, 0, y + (h - hh), hh, F['spray'], ('m', k, i), z=Z0 - .002)
        if T.detail:
            spray_text(g, T, r, .018, y + (h - hh) - .018, hh, F['spray-black'], ('m', k, i), z=Z0, drips=False)


def graffiti_x(g, T):
    # the search mark: a big X, the date above, the team at the left, the dead below, the hazard at the right
    f = F['spray']
    for (a, b) in (((-.45, 1.15), (.45, 2.2)), ((-.45, 2.2), (.45, 1.15))):
        dx, dy = b[0] - a[0], b[1] - a[1]
        l = math.hypot(dx, dy)
        nx, ny = -dy / l * .035, dx / l * .035
        g.face(((a[0] - nx, a[1] - ny, Z0), (b[0] - nx, b[1] - ny, Z0), (b[0] + nx, b[1] + ny, Z0), (a[0] + nx, a[1] + ny, Z0)), f, (0, 0, -1))
    spray_text(g, T, '11-7', 0, 2.0, .1, f, 'xd', z=Z0 - .002, drips=False)
    spray_text(g, T, 'CD', -.32, 1.6, .09, f, 'xt', z=Z0 - .002, drips=False)
    spray_text(g, T, '2', 0, 1.2, .11, f, 'xn', z=Z0 - .002, drips=False)
    spray_text(g, T, '!', .33, 1.6, .1, f, 'xh', z=Z0 - .002, drips=False)


def wall_boards(g, T):
    # planks nailed over an opening: six across, gaps between, a little askew, nail heads
    n = 7
    for k in range(n):
        y0 = .3 + k * .33
        y1 = y0 + .28
        a = (h01('bd', k) - .5) * .05
        f = F['board-wood'] if k % 2 else F['board-wood-b']
        sub = Geo()
        panel(sub, T, -1.0, 1.0, -.14, .14, .022, f, cell=0, z=-.07)
        g.extend(sub.transformed([math.cos(a), -math.sin(a), 0, 0, math.sin(a), math.cos(a), 0, (y0 + y1) / 2, 0, 0, 1, 0]))
        if T.detail:
            for x in (-.85, .85):
                bx(g, T, x - .012, x + .012, (y0 + y1) / 2 - .012, (y0 + y1) / 2 + .012, -.096, -.092, F['nail'], bevel=0)
    # two uprights behind the planks (the battens)
    for x in (-.85, .85):
        panel(g, T, x - .05, x + .05, .25, 2.58, .006, F['board-wood-b'], cell=0, z=Z0)


def wall_notice(g, T):
    sheet(g, -.2, .2, 1.32, 1.83, Z0, F['paper'], 0)
    text_pix(g, 'KEEP OUT', 0, 1.7, Z0 - .002, .008, F['poster-red'], depth=.0012)
    lines(g, -.16, .16, 1.62, 5, .045, .01, Z0 - .002, F['ink'], 'wn')
    for (x, y) in ((-.18, 1.82), (.18, 1.82)):
        tape(g, x, y, Z0 - .004, .6 if x < 0 else -.6, F['tape-grey'])


def wall_hand(g, T):
    f = F['blood']
    for k, (x, y, a) in enumerate(((-.2, 1.5, .2), (.15, 1.35, -.15), (-.05, 1.1, .1))):
        c, s = math.cos(a), math.sin(a)
        palm = [(-.05, -.06), (.05, -.06), (.06, .05), (-.06, .05)]
        g.face([(x + c * u - s * v, y + s * u + c * v, Z0 - .0005 * k) for u, v in palm], f if k < 2 else F['blood-dry'], (0, 0, -1))
        for j in range(4):
            u = -.045 + .03 * j
            g.face([(x + c * uu - s * vv, y + s * uu + c * vv, Z0 - .0005 * k) for uu, vv in ((u - .01, .05), (u + .01, .05), (u + .01, .13 - .02 * abs(j - 1.5)), (u - .01, .13 - .02 * abs(j - 1.5)))], f, (0, 0, -1))
    # the smear dragged down from the lowest print
    g.face(((-.1, .7, Z0 - .001), (.02, .7, Z0 - .001), (0, 1.05, Z0 - .001), (-.1, 1.05, Z0 - .001)), F['blood-dry'], (0, 0, -1))


def wall_hazard(g, T):
    wall_rect(g, T, -1.25, 1.25, 0, .6, Z0, F['hazard-black'], -1, 0)
    n = 9
    for k in range(n):
        x = -1.25 + 2.5 * k / (n - 1) - .15
        pts = [(x, 0), (x + .14, 0), (x + .14 + .3, .6), (x + .3, .6)]
        pts = [(max(-1.25, min(1.25, px_)), py) for px_, py in pts]
        if pts[1][0] - pts[0][0] < .01 and pts[2][0] - pts[3][0] < .01:
            continue
        g.face([(p[0], p[1], Z0 - .002) for p in pts], F['hazard-yellow'], (0, 0, -1))


def wall_stencil(g, T):
    text_pix(g, 'NO PARKING', 0, 1.45, Z0, .03, F['stencil-white'], depth=.002)
    if T.detail:
        text_pix(g, 'LOADING BAY', 0, 1.2, Z0, .018, F['stencil-white'], depth=.002)


# ---------------------------------------------------------------------------------------------------------------------------
# Low clutter against a wall (facade frame; every part within 6.2 cm .. 25 cm of the face)
# ---------------------------------------------------------------------------------------------------------------------------
def low_binbags(g, T):
    n, m = (10, 6) if T.lod == 0 else (5, 2)
    bags = [(-.6, .3, .22, .3, .085, 'binbag'), (-.15, .33, .25, .33, .09, 'binbag-green'), (.4, .28, .22, .28, .085, 'binbag'),
            (.1, .62, .2, .18, .08, 'binbag'), (-.4, .6, .18, .16, .075, 'binbag-green')]
    for (x, y, rx, ry, rz, f) in bags:
        ellipsoid(g, (x, y, -.156), (rx, ry, rz), F[f], n, m)
        if T.detail:
            rod(g, (x, y + ry * .9, -.156), (x, y + ry * 1.25, -.156), .02, F['binbag-tie'], 4)


def low_bike(g, T):
    # a bicycle leaning on the wall, its handlebars turned along it; every part between 6.5 and 24 cm out
    z = -.15
    n = 20 if T.lod == 0 else 8
    for cx in (-.55, .55):
        for k in range(n):
            a0, a1 = 2 * PI * k / n, 2 * PI * (k + 1) / n
            for (r0, r1, f) in (((.3, .335, F['tyre']), (.27, .3, F['chrome'])) if T.lod == 0 else ((.27, .335, F['tyre']),)):
                g.face(((cx + math.cos(a0) * r0, .335 + math.sin(a0) * r0, z), (cx + math.cos(a1) * r0, .335 + math.sin(a1) * r0, z),
                        (cx + math.cos(a1) * r1, .335 + math.sin(a1) * r1, z), (cx + math.cos(a0) * r1, .335 + math.sin(a0) * r1, z)), f, (0, 0, -1))
                g.face(((cx + math.cos(a0) * r1, .335 + math.sin(a0) * r1, z + .004), (cx + math.cos(a1) * r1, .335 + math.sin(a1) * r1, z + .004),
                        (cx + math.cos(a1) * r0, .335 + math.sin(a1) * r0, z + .004), (cx + math.cos(a0) * r0, .335 + math.sin(a0) * r0, z + .004)), f, (0, 0, 1))
        if T.detail:
            for k in range(8):
                a = PI * k / 8
                rod(g, (cx - math.cos(a) * .27, .335 - math.sin(a) * .27, z), (cx + math.cos(a) * .27, .335 + math.sin(a) * .27, z), .003, F['chrome'], 3, caps=False)
    s = T.small
    frame = [((-.55, .335), (-.05, .36)), ((-.05, .36), (.45, .78)), ((-.12, .78), (.45, .78)), ((-.05, .36), (-.15, .82)), ((-.55, .335), (-.15, .82)),
             ((.55, .335), (.45, .78))]
    for (a, b) in frame:
        rod(g, (a[0], a[1], z), (b[0], b[1], z), .018, F['bike-paint'], s)
    rod(g, (.45, .78, z), (.42, .98, z), .016, F['chrome'], s)
    rod(g, (.42, .98, -.08), (.42, .98, -.23), .014, F['chrome'], s)   # the bars, turned along the wall
    bx(g, T, -.26, -.06, .82, .87, z - .045, z + .045, F['saddle'], bevel=.015)


def low_card(g, T):
    for k, (x0, x1, h, lean) in enumerate(((-.58, .1, 1.08, .17), (-.2, .55, .92, .15), (.15, .6, .7, .13))):
        f = F['cardboard'] if k % 2 == 0 else F['cardboard-dark']
        zb, zt = -.24 + .01 * k, -.24 + lean
        g.face(((x0, .01, zb), (x1, .01, zb), (x1, h, zt), (x0, h, zt)), f, (0, .2, -.98))
        if T.detail:
            g.face(((x0 + .02, .45, zb + (zt - zb) * .41 - .002), (x1 - .02, .45, zb + (zt - zb) * .41 - .002), (x1 - .02, .47, zb + (zt - zb) * .43 - .002), (x0 + .02, .47, zb + (zt - zb) * .43 - .002)), F['cardboard-dark'], (0, .2, -.98))


def low_sandbags(g, T):
    for row in range(3):
        n = 4 if row < 2 else 3
        for k in range(n):
            x0 = -1.18 + (2.36 / n) * k + (.15 if row == 1 else 0) + (.3 if row == 2 else 0)
            x1 = min(1.2, x0 + 2.36 / n - .03)
            y0 = row * .21
            bx(g, T, x0, x1, y0, y0 + .2, -.24, -.07, F['sandbag'], bevel=.05)
    if T.detail:
        text_pix(g, 'CD', -.5, .26, -.241, .02, F['stencil-black'], depth=.001)


def low_drum(g, T):
    # a half drum, its cut face to the wall (an oval half 0.19 deep), coals and two logs glowing in it (white: the runtime's fire)
    n = 14 if T.lod == 0 else 8
    rx, rz = .32, .186
    for k in range(n):
        a0, a1 = PI * k / n, PI * (k + 1) / n
        p = [(math.cos(a0) * rx, 0, -.062 - math.sin(a0) * rz), (math.cos(a1) * rx, 0, -.062 - math.sin(a1) * rz)]
        g.face(((p[0][0], 0, p[0][2]), (p[1][0], 0, p[1][2]), (p[1][0], .82, p[1][2]), (p[0][0], .82, p[0][2])), F['drum-paint'] if k == n // 2 else F['drum-rust'],
               normals=(sk._norm((math.cos(a0) / rx, 0, -math.sin(a0) / rz)), sk._norm((math.cos(a1) / rx, 0, -math.sin(a1) / rz)),
                        sk._norm((math.cos(a1) / rx, 0, -math.sin(a1) / rz)), sk._norm((math.cos(a0) / rx, 0, -math.sin(a0) / rz))))
        q = [(math.cos(a) * rx * .93, -.062 - math.sin(a) * rz * .88) for a in (a0, a1)]
        g.face(((q[1][0], .82, q[1][1]), (q[0][0], .82, q[0][1]), (q[0][0], .1, q[0][1]), (q[1][0], .1, q[1][1])), F['drum-rust'])
    # the coals' bed and the logs over it
    pts = [(math.cos(PI * k / n) * rx * .92, .78, -.062 - math.sin(PI * k / n) * rz * .86) for k in range(n + 1)]
    g.face(pts[::-1], glow(WHITE, 1.6), (0, 1, 0))
    if T.detail:
        for k in range(2):
            a = .4 * (1 if k else -1)
            sub = Geo()
            bx(sub, T, -.2, .2, -.04, .04, -.04, .04, F['log'], bevel=.01)
            g.extend(sub.transformed([math.cos(a), -math.sin(a), 0, .05 * k, math.sin(a), math.cos(a), 0, .86, 0, 0, 1, -.15]))
        for k in range(3):
            bx(g, T, -.18 + .15 * k, -.12 + .15 * k, .79, .83, -.13, -.09, glow(WHITE, 2.4), bevel=0)


def low_meter(g, T):
    bx(g, T, -.27, .27, .88, 1.57, -.2, -.062, F['meter-grey'], bevel=.015)
    wall_rect(g, T, -.22, .22, .95, 1.5, -.201, F['ac-grille'] if T.detail else F['meter-grey'], -1, 0)
    if T.detail:
        text_pix(g, 'DANGER', 0, 1.38, -.202, .008, F['poster-red'], depth=.001)
        bx(g, T, .2, .25, 1.15, 1.25, -.215, -.2, F['nail'], bevel=0)
        rod(g, (0, .0, -.13), (0, .88, -.13), .025, F['iron-black'], T.small)


def low_phone(g, T):
    # the wall payphone's hood (bible 6.4: 0.25 m deep or less), municipal green, the handset on its hook, TELEPHONE
    bx(g, T, -.38, .38, 1.48, 1.58, -.24, -.062, F['phone-green'], bevel=.015)
    for x in (-.38, .34):
        bx(g, T, x, x + .04, .62, 1.48, -.24, -.062, F['phone-green'], bevel=.008)
    wall_rect(g, T, -.34, .34, .62, 1.48, -.064, F['enamel-cream'], -1, 0)
    bx(g, T, -.14, .14, 1.0, 1.38, -.16, -.064, F['signal-black'], bevel=.01)
    if T.detail:
        bx(g, T, .02, .1, 1.08, 1.32, -.19, -.16, F['signal-black'], bevel=.015)
        text_pix(g, 'TELEPHONE', 0, 1.5, -.241, .006, F['enamel-cream'], depth=.001)


def low_pipe(g, T):
    rod(g, (0, 0, -.13), (0, 1, -.13), .055, F['iron-black'], T.small + 2)


# ---------------------------------------------------------------------------------------------------------------------------
# Upper: on walls over 3.1 m (facade frame; the origin at the mount)
# ---------------------------------------------------------------------------------------------------------------------------
def up_ac(g, T):
    bx(g, T, -.42, .42, -.52, -.06, -.62, -.14, F['ac-white'], bevel=.02)
    n = 16 if T.lod == 0 else 8
    pts = [(-.15 + math.cos(2 * PI * k / n) * .17, -.29 + math.sin(2 * PI * k / n) * .17, -.625) for k in range(n)]
    g.face(pts, F['ac-grille'], (0, 0, -1))
    if T.detail:
        for k in range(5):
            y = -.45 + .07 * k
            bx(g, T, .1, .38, y, y + .025, -.63, -.62, F['ac-grille'], bevel=0)
        rod(g, (.3, -.52, -.3), (.3, -.56, -.3), .006, F['ac-grille'], 3)
    for x in (-.3, .3):
        bx(g, T, x - .02, x + .02, -.56, -.52, -.62, -.062, F['iron-black'], bevel=0)
        rod(g, (x, -.54, -.6), (x, -.06, -.07), .012, F['iron-black'], T.small)


def up_box(g, T):
    # a window box on its sill: a trough on brackets, soil, the plants dead from the long night (a few still green)
    bx(g, T, -.58, .58, -.08, .2, -.44, -.14, F['trough-green'], bevel=.015)
    top_rect(g, T, -.55, .55, -.41, -.17, .18, F['soil'], cell=0)
    for x in (-.4, .4):
        bx(g, T, x - .02, x + .02, -.11, -.08, -.42, -.13, F['iron-black'], bevel=0)
    n = 10 if T.lod == 0 else 4
    for k in range(n):
        x = -.5 + 1.0 * (k + .5) / n
        h = .12 + .2 * h01('pl', k)
        a = (h01('pa', k) - .5) * .9
        f = F['plant-green'] if h01('pg', k) < .25 else F['plant-dead']
        rod(g, (x, .18, -.29), (x + math.sin(a) * h, .18 + math.cos(a) * h, -.29 - .06 * h01('pz', k)), .012, f, 3)


def up_awning(g, T, torn):
    # a striped canvas awning, half out, from under the fascia (its valance's foot 0.3 m under the mount: 3.12 m)
    y0, y1, c1 = .0, -.08, -1.2
    n = 8 if T.lod == 0 else 2
    bx(g, T, -1.2, 1.2, -.03, .03, -.1, -.062, F['iron-black'], bevel=.005)
    for i in range(n):
        a, b = -1.2 + 2.4 * i / n, -1.2 + 2.4 * (i + 1) / n
        f = F['canvas'] if i % 2 == 0 else F['canvas-b']
        if torn and i in (2, 5) and T.lod == 0:
            # a rent: the stripe hangs in two flaps
            g.face(((a, y0, -.1), (b, y0, -.1), (b, -.17, -.6), (a, -.17, -.6)), f, (0, .8, -.6))
            g.face(((a, -.17, -.6), (b, -.17, -.6), (b, y0, -.1), (a, y0, -.1)), f, (0, -.8, .6))
            continue
        top = ((a, y0, -.1), (b, y0, -.1), (b, y1, c1), (a, y1, c1))
        g.face(top, f, (0, .99, -.07))
        g.face(top[::-1], f, (0, -.99, .07))
        vb = -.29 if not (torn and i % 3 == 1) else -.2
        g.face(((a, vb, c1), (b, vb, c1), (b, y1, c1), (a, y1, c1)), f, (0, 0, -1))
        g.face(((a, y1, c1 + .005), (b, y1, c1 + .005), (b, vb, c1 + .005), (a, vb, c1 + .005)), f, (0, 0, 1))
    if T.detail:
        for x in (-1.1, 1.1):
            rod(g, (x, -.26, -.07), (x, y1 - .02, c1 + .03), .016, F['iron-black'], T.small)
        rod(g, (-1.2, y1 - .01, c1 + .02), (1.2, y1 - .01, c1 + .02), .022, F['iron-black'], T.small)


SIGNS = (('HOTEL', 'tungsten'), ('BAR', 'neon'), ('CAFE', 'neon'), ('PAWN', 'tungsten'))


def up_sign(g, T, k):
    # a blade sign standing out from the wall on its bracket, lettered both sides (white on the glow mesh: the runtime lights it)
    word, kind = SIGNS[k]
    rod(g, (0, .2, -.064), (0, .2, -1.12), .022, F['iron-black'], T.small)
    rod(g, (0, -.05, -.085), (0, .2, -.45), .016, F['iron-black'], T.small)
    bx(g, T, -.06, .06, -.8, .1, -1.12, -.2, F['sign-board'], bevel=.012)
    bx(g, T, -.045, .045, -.08, .2, -.8, -.8 + .08, F['iron-black'], bevel=0)
    bx(g, T, -.045, .045, -.08, .2, -.4, -.32, F['iron-black'], bevel=0)
    for side in (1, -1):
        sub = Geo()
        h = .58 / 7 if len(word) <= 4 else .5 / 7
        px = min(h, .8 / max(pix_width(word, 1.0), 1))
        text_pix(sub, word, 0, -.35 - 3.5 * px, 0, px, glow(WHITE, 2.6 if kind == 'neon' else 2.0), depth=0.0)
        moved = turn_side(sub, side).transformed([1, 0, 0, side * .0605, 0, 1, 0, 0, 0, 0, 1, -.66])
        g.extend(moved)
        if T.detail:
            # the frame's lit border (tungsten bulbs or the neon tube)
            for (y, a, b) in ((-.76, -1.08, -.24), (.06, -1.08, -.24)):
                g.face(((side * .061, y - .015, a), (side * .061, y - .015, b), (side * .061, y + .015, b), (side * .061, y + .015, a))[::-1 if side > 0 else 1],
                       glow(WHITE, 1.6), (side, 0, 0))
    if word == 'PAWN' and T.detail:
        for (dy, dz) in ((-.95 + .76, -1.0), (-.95 + .76, -.85), (-.95 + .62, -.925)):
            ellipsoid(g, (0, dy - .08, dz), (.05, .05, .05), F['gilt'], 8, 5)


BANNERS = ('HELP', '3 ALIVE', 'SOS')


def up_banner(g, T, k):
    # a bedsheet hung from a window sill, its message painted by hand (the sheet white: the runtime tints it)
    nx, ny = (6, 8) if T.lod == 0 else (2, 2)
    for j in range(ny):
        for i in range(nx):
            x0, x1 = -1.0 + 2.0 * i / nx, -1.0 + 2.0 * (i + 1) / nx
            y0, y1 = -2.6 * (j + 1) / ny, -2.6 * j / ny
            def z(x, y):
                return -.162 - .025 * math.sin(x * 3.1 + y * 1.7) - .015 * math.sin(y * 4.3)
            g.face(((x0, y0, z(x0, y0)), (x1, y0, z(x1, y0)), (x1, y1, z(x1, y1)), (x0, y1, z(x0, y1))), F['sheet'], (0, 0, -1))
    word = BANNERS[k]
    h = min(.32, 1.7 / max(pix_width(word, 1.0) * 7 / 7, 1e-3))
    rows = word.split(' ') if ' ' in word else [word]
    for i, r in enumerate(rows):
        spray_text(g, T, r, 0, -.8 - i * (h + .15) - h * (1 if len(rows) == 1 else 0), h, F['paint-red'], ('bn', k, i), z=-.212, slant=.0, drips=True)
    rod(g, (-1.0, .02, -.14), (1.0, .02, -.14), .015, F['iron-black'], 4)


def up_flood(g, T):
    # a Civil Defence flood on its wall arm, aimed down over the plaza (its lens white: the runtime's cold flood)
    bx(g, T, -.14, .14, -.2, .3, -.1, -.062, F['cd-olive'], bevel=.01)
    rod(g, (0, .05, -.08), (0, -.2, -.7), .03, F['cd-olive'], T.small)
    sub = Geo()
    bx(sub, T, -.32, .32, -.22, .22, -.22, .18, F['cd-olive'], bevel=.025)
    sk.grid_face(sub, (-.26, -.18, -.225), (.52, 0, 0), (0, .36, 0), 0, 0, glow(WHITE, 3.2), (0, 0, -1), 1e9)
    if T.detail:
        bx(sub, T, -.33, .33, .18, .22, -.3, .18, F['cd-orange'], bevel=0)
    a = math.radians(38)
    g.extend(sub.transformed([1, 0, 0, 0, 0, math.cos(a), math.sin(a), -.3, 0, -math.sin(a), math.cos(a), -.95]))


def up_flag(g, T):
    rod(g, (0, .1, -.064), (0, .1, -1.6), .025, F['flag-pole'], T.small)
    nx, ny = (5, 6) if T.lod == 0 else (1, 1)
    for j in range(ny):
        for i in range(nx):
            z0, z1 = -.4 - 1.1 * i / nx, -.4 - 1.1 * (i + 1) / nx
            y0, y1 = .08 - 1.5 * (j + 1) / ny, .08 - 1.5 * j / ny

            def x(z, y):
                return .03 * math.sin(z * 4.0 + y * 2.0)
            for side, nrm in ((1, (1, 0, 0)), (-1, (-1, 0, 0))):
                q = ((x(z0, y0), y0, z0), (x(z1, y0), y0, z1), (x(z1, y1), y1, z1), (x(z0, y1), y1, z0))
                g.face(q if side < 0 else q[::-1], F['sheet'], nrm)
    if T.detail:
        # the city's crest (a clock face over three stepped hills), black on both sides (tinting leaves black black)
        for side in (1, -1):
            for (dz, dy, w, h) in ((-.75, -.55, .3, .08), (-.95, -.5, .2, .13), (-1.1, -.45, .1, .18)):
                q = ((side * .045, dy, dz - w / 2), (side * .045, dy, dz + w / 2), (side * .045, dy + h, dz + w / 2), (side * .045, dy + h, dz - w / 2))
                g.face(q if side < 0 else q[::-1], F['paint-black-b'], (side, 0, 0))


def signal_head(g, T, cx, cy, cz):
    """A three-lamp signal head hanging from (cx, cy, cz), lenses both ways along x: the amber's lens white on the glow mesh."""
    bx(g, T, cx - .17, cx + .17, cy - 1.0, cy - .1, cz - .16, cz + .16, F['signal-black'], bevel=.02)
    rod(g, (cx, cy, cz), (cx, cy - .1, cz), .03, F['signal-black'], T.small)
    n = 14 if T.lod == 0 else 8
    for side in (1, -1):
        for (y, f) in ((-.28, F['lens-red']), (-.55, glow(WHITE, 3.0)), (-.82, F['lens-green'])):
            pts = [(cx + side * .172, cy + y + math.sin(2 * PI * k / n) * .1, cz + math.cos(2 * PI * k / n) * .1) for k in range(n)]
            g.face(pts if side < 0 else pts[::-1], f, (side, 0, 0))
            if T.detail:
                # the visor over each lens
                bx(g, T, cx + side * .17 - (0 if side > 0 else .07), cx + side * .17 + (.07 if side > 0 else 0), cy + y + .1, cy + y + .12, cz - .12, cz + .12, F['signal-black'], bevel=0)


def up_signal(g, T):
    # the high arm off the wall (4.8 m), its head hanging over the road 2.35 m out
    bx(g, T, -.1, .1, -.12, .25, -.12, -.062, F['iron-black'], bevel=.01)
    bx(g, T, -.05, .05, .12, .2, -2.45, -.06, F['signal-black'], bevel=.008)
    rod(g, (0, -.08, -.1), (0, .14, -.9), .02, F['signal-black'], T.small)
    signal_head(g, T, 0, .12, -2.36)


def hung_signal(g, T):
    signal_head(g, T, 0, 0, 0)


# ---------------------------------------------------------------------------------------------------------------------------
# Spans (x across from the cable's start; scaled to fit) and hung pieces
# ---------------------------------------------------------------------------------------------------------------------------
def cable(g, pts, r, f):
    """A cable through points (x along the span, stretched by the planner): two crossed ribbons, a vertical one 2r tall and a
    flat one 2r wide, so stretching x never flattens its section (a tube's rings would turn to blades on the slopes)."""
    for a, b in zip(pts, pts[1:]):
        g.face(((a[0], a[1] - r, a[2]), (b[0], b[1] - r, b[2]), (b[0], b[1] + r, b[2]), (a[0], a[1] + r, a[2])), f, (0, 0, -1))
        g.face(((a[0], a[1] + r, a[2]), (b[0], b[1] + r, b[2]), (b[0], b[1] - r, b[2]), (a[0], a[1] - r, a[2])), f, (0, 0, 1))
        g.face(((a[0], a[1], a[2] - r), (b[0], b[1], b[2] - r), (b[0], b[1], b[2] + r), (a[0], a[1], a[2] + r)), f, (0, 1, 0))
        g.face(((a[0], a[1], a[2] + r), (b[0], b[1], b[2] + r), (b[0], b[1], b[2] - r), (a[0], a[1], a[2] - r)), f, (0, -1, 0))


def span_wires(g, T):
    n = 12 if T.lod == 0 else 4
    for (z, sag, r) in (((-.1, .45, .012), (0.0, .58, .014), (.1, .5, .012)) if T.lod == 0 else ((0.0, .55, .02),)):
        pts = [(k / n, -sag * 4 * (k / n) * (1 - k / n), z) for k in range(n + 1)]
        cable(g, pts, r, F['cable'])


CLOTHES = ('cloth-white', 'cloth-blue', 'cloth-red', 'cloth-beige', 'cloth-green', 'cloth-grey', 'cloth-yellow')


def span_washing(g, T):
    cable(g, [(0, 0, 0), (2, 0, 0)], .008, F['rope'])
    x = .08
    k = 0
    while x < 1.9:
        kind = int(h01('wk', k) * 4)
        f = F[CLOTHES[int(h01('wc', k) * len(CLOTHES))]]
        if kind == 0:   # a shirt
            w, h = .45, .62
            pts = [(x, 0), (x + w, 0), (x + w, -.18), (x + w - .08, -.18), (x + w - .08, -h), (x + .08, -h), (x + .08, -.18), (x, -.18)]
        elif kind == 1:  # a sheet
            w, h = .7, 1.05
            pts = [(x, 0), (x + w, 0), (x + w, -h), (x, -h + .05)]
        elif kind == 2:  # trousers
            w, h = .38, .9
            pts = [(x, 0), (x + w, 0), (x + w, -h), (x + w * .55, -h), (x + w / 2, -.3), (x + w * .45, -h), (x, -h)]
        else:            # a towel
            w, h = .3, .5
            pts = [(x, 0), (x + w, 0), (x + w, -h), (x, -h)]
        if x + w > 1.96:
            break
        zz = .02 * math.sin(k * 2.1)
        front = [(p[0], p[1] - .01, zz) for p in pts]
        g.face(front[::-1], f, (0, 0, -1))
        g.face([(p[0], p[1], zz + .004) for p in pts[::-1]][::-1], f, (0, 0, 1))
        if T.detail:
            for px_ in (x + .03, x + w - .03):
                bx(g, T, px_ - .012, px_ + .012, -.03, .015, zz - .012, zz + .012, F['board-wood'], bevel=0)
        x += w + .08 + .1 * h01('wg', k)
        k += 1


BUNTING = ('poster-red', 'poster-blue', 'poster-yellow', 'cloth-white', 'poster-green')


def span_bunting(g, T):
    cable(g, [(0, 0, 0), (2, 0, 0)], .007, F['rope'])
    n = 7
    for k in range(n):
        x0 = .04 + 1.92 * k / n
        x1 = x0 + 1.92 / n - .04
        f = F[BUNTING[k % len(BUNTING)]]
        tri = ((x0, -.005, 0), (x1, -.005, 0), ((x0 + x1) / 2, -.42, .01 * math.sin(k)))
        g.face(tri[::-1], f, (0, 0, -1))
        g.face(tri, f, (0, 0, 1))


def span_festoon(g, T):
    n = 8 if T.lod == 0 else 2
    pts = [(k / n * 2, -.12 * math.sin(PI * k / n), 0) for k in range(n + 1)]
    cable(g, pts, .008, F['cable'])
    for k in range(4):
        x = .25 + .5 * k
        y = -.12 * math.sin(PI * x / 2)
        rod(g, (x, y, 0), (x, y - .1, 0), .006, F['cable'], 3, caps=False)
        f = glow(WHITE, 2.4) if k != 2 else F['bulb-dead']
        if T.lod == 0:
            ellipsoid(g, (x, y - .17, 0), (.045, .06, .045), f, 8, 5)
        else:
            bx(g, T, x - .045, x + .045, y - .23, y - .11, -.045, .045, f, bevel=0)


# ---------------------------------------------------------------------------------------------------------------------------
# The table: each kind's builder, and whether it is authored in the facade frame (turned half round)
# ---------------------------------------------------------------------------------------------------------------------------
BUILD = {
    'road.dash': floor_dash, 'road.yellow': floor_yellow, 'road.zebra': floor_zebra, 'road.box': floor_box, 'road.slow': floor_slow,
    'road.bus': floor_bus, 'ground.manhole': floor_manhole, 'ground.drain': floor_drain, 'ground.grate': floor_grate,
    'ground.crack.0': lambda g, T: floor_crack(g, T, 0), 'ground.crack.1': lambda g, T: floor_crack(g, T, 1), 'ground.patch': floor_patch,
    'ground.oil': floor_oil, 'ground.skid': floor_skid, 'ground.scorch': floor_scorch,
    'litter.paper': floor_paper, 'litter.news': floor_news, 'litter.leaves': floor_leaves, 'litter.glass': floor_glass, 'litter.cans': floor_cans,
    'litter.clothes': floor_clothes, 'litter.card': floor_card,
    'blood.pool': floor_blood_pool, 'blood.trail': floor_blood_trail, 'blood.prints': floor_blood_prints,
    'mark.chalk.0': floor_chalk_hop, 'mark.chalk.1': floor_chalk_note, 'mark.cd.0': floor_cd_assembly, 'mark.cd.1': floor_cd_lane,
    'ground.tent': floor_tent, 'ground.ash': floor_ash, 'flare': lambda g, T: floor_flare(g, T, True), 'flare.spent': lambda g, T: floor_flare(g, T, False),
    'rumour.level0': rumour_level0, 'rumour.poolrooms': rumour_pool, 'rumour.wardzero': rumour_ward, 'rumour.station': rumour_station,
    'rumour.ironworks': rumour_iron,
    'poster.stay': poster_stay, 'poster.missing': poster_missing, 'poster.cd': poster_cd, 'poster.bills': poster_bills,
    'graffiti.tag.0': lambda g, T: tag(g, T, 't0'), 'graffiti.tag.1': lambda g, T: tag(g, T, 't1'),
    'graffiti.msg.0': lambda g, T: graffiti_msg(g, T, 0), 'graffiti.msg.1': lambda g, T: graffiti_msg(g, T, 1),
    'graffiti.msg.2': lambda g, T: graffiti_msg(g, T, 2), 'graffiti.msg.3': lambda g, T: graffiti_msg(g, T, 3),
    'graffiti.x': graffiti_x, 'wall.boards': wall_boards, 'wall.notice': wall_notice, 'wall.hand': wall_hand, 'wall.hazard': wall_hazard,
    'wall.stencil': wall_stencil,
    'low.binbags': low_binbags, 'low.bike': low_bike, 'low.card': low_card, 'low.sandbags': low_sandbags, 'low.drum': low_drum,
    'low.meter': low_meter, 'low.phone': low_phone, 'low.pipe': low_pipe,
    'upper.ac': up_ac, 'upper.box': up_box, 'upper.awning': lambda g, T: up_awning(g, T, False), 'upper.awning.torn': lambda g, T: up_awning(g, T, True),
    'upper.sign.0': lambda g, T: up_sign(g, T, 0), 'upper.sign.1': lambda g, T: up_sign(g, T, 1), 'upper.sign.2': lambda g, T: up_sign(g, T, 2),
    'upper.sign.3': lambda g, T: up_sign(g, T, 3), 'upper.banner.0': lambda g, T: up_banner(g, T, 0), 'upper.banner.1': lambda g, T: up_banner(g, T, 1),
    'upper.banner.2': lambda g, T: up_banner(g, T, 2), 'upper.flood': up_flood, 'upper.flag': up_flag, 'upper.signal': up_signal,
    'span.wires': span_wires, 'span.washing': span_washing, 'span.bunting': span_bunting, 'span.festoon': span_festoon,
    'hung.signal': hung_signal,
}
TURN = mb.TURN
FACADE = ('Wall', 'Low', 'Upper')
# pieces worth a Cycles occlusion bake (relief); the flat paint takes none
AO = ('low.', 'upper.', 'hung.', 'ground.ash')


def build(key, mount, T):
    g = Geo()
    BUILD[key](g, T)
    if mount in FACADE:
        g = g.transformed(TURN)
    return g


def check(key, env, g):
    mount, lo, hi = env
    P, _, _, _ = g.arrays()
    if not len(P):
        raise SystemExit('%s: no geometry' % key)
    a, b = P.min(axis=0), P.max(axis=0)
    eps = 2e-3
    bad = [i for i in range(3) if a[i] < lo[i] - eps or b[i] > hi[i] + eps]
    if bad:
        return '%s: outside its envelope on %s: %s..%s against %s..%s' % (key, 'xyz'[bad[0]], a.round(3), b.round(3), lo, hi)
    return None


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', help='the assets folder (the bank lands in ChipArenaLocal/Resources)')
    ap.add_argument('--preview', help='a folder for a contact sheet of every piece (Workbench renders)')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--only')
    args = ap.parse_args(argv)
    K = kinds()
    missing = sorted(set(K) - set(BUILD))
    if missing:
        raise SystemExit('no builder for %s' % ', '.join(missing))
    keys = sorted(K) if not args.only else args.only.split(',')
    finishes()
    sb = sk.StageBank()
    t0 = time.time()
    counts = {}
    for lod, ti in ((0, 0), (1, 2)):
        T = tier(lod)
        geos = []
        bad = []
        for key in keys:
            g = build(key, K[key][0], T)
            why = check(key, K[key], g)
            if why:
                bad.append(why)
            geos.append(g)
            _, _, S, _ = g.arrays()
            counts.setdefault(key, [0, 0])[lod] = int((S - 2).sum())
        if bad:
            raise SystemExit(chr(10).join(bad))
        aos = [None] * len(geos)
        if not args.no_ao:
            idx = [i for i, k in enumerate(keys) if any(k.startswith(s) for s in AO)]
            if idx:
                moved = [geos[i].transformed([1, 0, 0, 30.0 * n, 0, 1, 0, 0, 0, 0, 1, 0]) for n, i in enumerate(idx)]
                baked = sk.bake_ao(moved, distance=.6, samples=24 if lod == 0 else 10)
                for i, a in zip(idx, baked):
                    aos[i] = a
        pv = []
        for key, g, ao in zip(keys, geos, aos):
            mount = K[key][0]
            floors = (0.0,) if mount in ('Wall', 'Low') else (-50.0,)
            bk = sk.Bake(g, ao, floors=floors, seed=int(h01(key) * 1000))
            name = 'rig/streets/%s/%d' % (key, ti)
            om = sk.to_mesh(name, g, bk)
            sb.add(name, 'rig', om)
            if lod == 0:
                sb.share('rig/streets/%s/1' % key, 'rig', len(sb.bank.meshes) - 1)
            pv.append((g, bk))
        print('tier %d: %d pieces, %d faces, %.1f s' % (ti, len(keys), sum(g.faces for g in geos), time.time() - t0))
        if args.preview and lod == 0:
            sheet_render(pv, keys, K, args.preview)
    for key in keys:
        print('  %-22s %6d / %5d' % (key, counts[key][0], counts[key][1]))
    if args.out:
        sb.write(args.out, BANK)
    print('done in %.1f s' % (time.time() - t0))


def sheet_render(pv, keys, K, out):
    """Every piece laid out on a grid (wall pieces stood on a backing plane), rendered from the front and from above."""
    laid = []
    cols = 8
    for n, ((g, bk), key) in enumerate(zip(pv, keys)):
        cx, cz = (n % cols) * 3.2, -(n // cols) * 4.0
        mount = K[key][0]
        if mount in FACADE:
            m = [-1.0, 0, 0, cx, 0, 1.0, 0, 0 if mount != 'Upper' else 3.2, 0, 0, -1.0, cz]
        elif mount == 'Span':
            m = [1.0, 0, 0, cx - 1, 0, 1.0, 0, 3.0, 0, 0, 1.0, cz]
        elif mount == 'Hung':
            m = [1.0, 0, 0, cx, 0, 1.0, 0, 3.0, 0, 0, 1.0, cz]
        else:
            m = [1.0, 0, 0, cx, 0, 1.0, 0, 0, 0, 0, 1.0, cz]
        laid.append((g.transformed(m), bk))
    rows = (len(keys) + cols - 1) // cols
    views = [('front-%d%s' % (r, h), (cx, 2.4, -(r * 4.0) - 8.5), (cx, 1.6, -(r * 4.0)), 50) for r in range(rows) for (h, cx) in (('a', 4.8), ('b', 17.6))]
    views.append(('above', (11.0, 34.0, -14.0), (11.0, 0.0, -13.0), 60))
    sk.preview(laid, views, out, size=(1400, 700))


if __name__ == '__main__':
    main()
