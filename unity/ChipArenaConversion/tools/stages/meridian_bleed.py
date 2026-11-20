"""The Meridian bleed's bank (docs/MERIDIAN-BLEED.md): the set pieces the approach dressing places near each instance door
(Core NativeCityBleedDress) and the themed module variants for the street planner, modelled in Blender from script on stagekit.

  blender --background --factory-startup --python meridian_bleed.py -- --out ASSETS [--preview DIR] [--no-ao] [--only KEY,...]

Writes ASSETS/ChipArenaLocal/Resources/ChipArenaCityBleed.bytes (bank.py's format; never committed: it travels in the Drive
zip). Every kind of NativeCityBleedDress.Kinds is built once per tier: `rig/bleed/<key>/0` (LOD0, Fine on the city's leaner
counts), `/1` (Mid, sharing LOD0's mesh) and `/2` (LOD1, stagekit's Low), group "rig", in the kind's own frame:
  Floor     centred on the floor's top (y 0 up), z along the street;
  Wall      the bay's foot on the collider face, +z out of the wall, the viewer's right -x (authored in the city kit's facade
            frame, facing -z with +x to the viewer's right, and turned half round);
  Span      from the cable's start, x across the street (scaled to the span by the planner), y about the cable;
  Overhead  hung pieces from their hanging point (y 0 down), the ceiling's cells from their underside (y 0 up).
The themed module variants for the street planner are `module/<key>@<theme>/<tier>` (group "module"; see VARIANT_KEYS).
Each piece is checked against its kind's envelope, read from the Core source (the one table both sides keep), so a piece can
never stand prouder or lower than the planner placed it for. Finishes follow the approved board's (art bible 4.2: the city's
common finishes and each instance's own); glowing parts go on the glow sub-mesh, white where the runtime tints a lamp.
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
from stagekit import Geo, _add, _sub, _norm, _dot, _lerp  # noqa: E402

BANK = 'ChipArenaCityBleed'
CORE = os.path.join(HERE, '..', '..', 'Assets', 'ChipArena', 'Core', 'NativeCityBleedDress.cs')
PI = math.pi


# ---------------------------------------------------------------------------------------------------------------------------
# The kinds and their envelopes (Core NativeCityBleedDress.Kinds)
# ---------------------------------------------------------------------------------------------------------------------------
def kinds():
    src = open(CORE, encoding='utf-8').read()
    names = {'Storey': 3.1, 'Upper': 3.0}

    def num(t):
        t = t.strip()
        if t in names:
            return names[t]
        return float(t.rstrip('f'))
    out = {}
    for m in re.finditer(r'new Kind\("([^"]+)",Mount\.(\w+),V\(([^)]*)\),V\(([^)]*)\)', src):
        out[m.group(1)] = (m.group(2), tuple(num(v) for v in m.group(3).split(',')), tuple(num(v) for v in m.group(4).split(',')))
    if len(out) < 50:
        raise SystemExit('read only %d bleed kinds from %s' % (len(out), CORE))
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# Tiers, finishes and the shared pieces (after the approved board's city kit)
# ---------------------------------------------------------------------------------------------------------------------------
def tier(lod):
    """LOD0 (Fine on the bible's leaner counts) or LOD1 (stagekit's Low)."""
    if lod == 0:
        T = sk.Tier(0)
        T.arc, T.round, T.small, T.cell = 12, 10, 6, .75
    else:
        T = sk.Tier(2)
    T.lod = lod
    return T


F = {}
GLOWS = {}


def fin(name, hexc, detail=sk.DETAIL_NONE, smooth=.2, metal=0.0, dirt=1.0, moss=0.0, wear=0.0, param=0.0):
    F[name] = sk.fin('mer-' + name, hexc, detail, smooth=smooth, metal=metal, dirt=dirt, moss=moss, param=param, wear=wear)
    return F[name]


def glow(hexc, gain):
    """A glowing finish (its own sub-mesh; the gain in uv1.y, above 1 for the bloom)."""
    gain = round(max(.2, gain), 2)
    key = (hexc & 0xFFFFFF, gain)
    if key not in GLOWS:
        GLOWS[key] = sk.fin('mer-glow-%06x-%s' % key, hexc & 0xFFFFFF, sk.DETAIL_GLOW, glow=gain)
    return GLOWS[key]


def finishes():
    if F:
        return F
    B, P, FL, TI, W, I, BR, CL, N = sk.DETAIL_BRICK, sk.DETAIL_PLASTER, sk.DETAIL_FLAG, sk.DETAIL_TILE, sk.DETAIL_WOOD, \
        sk.DETAIL_IRON, sk.DETAIL_BRASS, sk.DETAIL_CLOTH, sk.DETAIL_NONE
    a = fin
    # the city's common finishes (art bible 4.2)
    a('brick-stock', 0x8C4B36, B, .18, 0, 1.0, .25, .10)
    a('brick-blue', 0x4E4450, B, .32, 0, .8, .15, .05, 1.0)
    a('brick-buff', 0xB48A5E, B, .15, 0, 1.3, .10, .10, 1.05)
    a('brick-glazed', 0xE3D8BC, B, .55, 0, .6, 0, .05, 1.3)
    a('brick-soot', 0x4A3A34, B, .12, 0, .6, 0, .05)
    a('brick-fire', 0x9A5A3A, B, .15, 0, 1.2, 0, .10, .9)
    a('iron-green', 0x24463A, N, .40, .25, .8, .10, .35)
    a('iron-black', 0x26262C, N, .45, .30, .7, 0, .30)
    a('iron-raw', 0x4A4E52, I, .35, .7, 1.2, 0, .20)
    a('galv', 0x9AA0A4, N, .50, .8, .6, 0, .10)
    a('chrome', 0xD8DCE0, N, .85, 1.0, .2, 0, .05)
    a('brass', 0xD2A840, BR, .70, 1.0, .3, 0, .30)
    a('enamel-cobalt', 0x1E3C96, N, .75, 0, .3, 0, .20)
    a('enamel-red', 0xB0232A, N, .75, 0, .3, 0, .20)
    a('enamel-cream', 0xEFE6CC, N, .75, 0, .3, 0, .20)
    a('enamel-green', 0x1F6A4A, N, .75, 0, .3, 0, .20)
    a('paint-black', 0x232126, W, .45, 0, .8, .10, .30)
    a('paint-cream', 0xE4D9BC, W, .45, 0, .8, .10, .30)
    a('concrete', 0x8A8882, P, .10, 0, 1.4, .20, .10)
    a('glass-dark', 0x1A2230, N, .90, 0, .2, 0, 0)
    a('steel-dark', 0x34383C, N, .45, .6, .6, 0, .25)
    a('rubber', 0x1E1E22, N, .25, 0, .6, 0, .05)
    a('void', 0x0C0C10, N, .10, 0, 0, 0, 0)
    a('paper', 0xE6E2D6, N, .15, 0, .5, 0, 0)
    a('paper-old', 0xC8C0A8, N, .15, 0, .6, 0, 0)
    a('cable', 0x18181C, N, .40, .2, .3, 0, 0)
    # Level 0 (THE HUM)
    a('veneer', 0xA9814F, W, .40, 0, .5, 0, .10)
    a('stucco-beige', 0xCBB88A, P, .10, 0, .6, 0, 0)
    a('stucco-joint', 0x8E7F5C, N, .10, 0, .6, 0, 0)
    a('glass-brown', 0x2E2418, N, .90, 0, .2, 0, 0)
    a('kickplate', 0xB9BEC2, N, .65, .85, .4, 0, .25)
    a('alu', 0xB4BAC0, N, .55, .8, .4, 0, .10)
    a('carpet', 0x7E7048, CL, .05, 0, .9, .10, 0)
    a('carpet-b', 0x786A44, CL, .05, 0, .9, .10, 0)
    a('carpet-damp', 0x625638, CL, .10, 0, .9, .20, 0)
    a('wallpaper', 0xC8B25A, P, .12, 0, .5, .05, 0)
    a('wallpaper-stripe', 0xB09A48, N, .12, 0, .5, .05, 0)
    a('skirting', 0xE6E0CC, W, .45, 0, .6, .05, .25)
    a('ceiling-tile', 0xD8D4C0, P, .08, 0, .6, 0, 0)
    a('ceiling-stain', 0x9C8A5C, P, .08, 0, .4, 0, 0)
    a('tbar', 0xE8E6DE, N, .50, .2, .4, 0, .10)
    a('stain', 0x9A8656, P, .10, 0, .3, 0, 0)
    a('exit-green', 0x1E7A44, N, .5, 0, .3, 0, 0)
    # the Poolrooms (THE DEEP END)
    a('tile-pool', 0xE8F2F4, TI, .70, 0, .3, 0, 0, .15)
    a('tile-aqua', 0x3FB8B8, TI, .70, 0, .3, 0, 0, .15)
    a('tile-teal', 0x1F8C96, TI, .70, 0, .3, 0, 0, .15)
    a('mosaic-pale', 0x9FD0EE, TI, .60, 0, .3, 0, 0, .04)
    a('mosaic-blue', 0x2F7FD0, TI, .60, 0, .3, 0, 0, .04)
    a('water', 0x1A5C66, N, .92, 0, 0, 0, 0)
    a('coping', 0xF0F2EE, P, .55, 0, .5, .10, .10)
    a('float-pink', 0xE86A8C, N, .55, 0, .3, 0, 0)
    a('float-white', 0xF2EEE6, N, .55, 0, .3, 0, 0)
    a('float-blue', 0x2F6FD0, N, .55, 0, .3, 0, 0)
    a('rope', 0xE8E4D8, CL, .2, 0, .5, 0, 0)
    a('pennant-aqua', 0x3CC2C8, CL, .2, 0, .4, 0, 0)
    a('pennant-navy', 0x233150, CL, .2, 0, .4, 0, 0)
    # Ward Zero (THE SIREN)
    a('cladding-white', 0xDCE6EA, N, .45, 0, .5, 0, .05)
    a('cladding-joint', 0x7C8A92, N, .30, 0, .4, 0, 0)
    a('cladding-teal', 0x1F9C9C, N, .50, 0, .4, 0, .05)
    a('pvc', 0xBFDCD6, N, .60, 0, .2, 0, 0)
    a('sheet', 0xD8E2E2, CL, .30, 0, .4, 0, 0)
    a('strap', 0xC8A030, N, .40, 0, .5, 0, .2)
    a('stencil-black', 0x1E1E24, N, .30, 0, .3, 0, .4)
    a('decon-teal', 0x1F9C9C, N, .35, 0, .4, 0, .5)
    a('decon-white', 0xD6DEDC, N, .35, 0, .5, 0, .3)
    a('growth', 0x3A1E58, N, .55, 0, .2, 0, 0)
    a('growth-skin', 0x5A2C8A, N, .65, 0, .2, 0, 0)
    a('steel-white', 0xE4E8EA, N, .55, .3, .4, 0, .15)
    # Central Station (THE 6:47)
    a('cast-green', 0x2A4A3E, N, .45, .3, .8, .05, .35)
    a('board-black', 0x141418, N, .40, 0, .3, 0, 0)
    a('tile-cream', 0xEADFC2, TI, .60, 0, .5, 0, .05, .15)
    a('tile-green', 0x2E6A4E, TI, .60, 0, .5, 0, .05, .15)
    a('rail', 0x8A8E92, N, .70, .9, .3, 0, .4)
    a('setts', 0x5A5652, FL, .45, 0, .9, .30, .20, .22)
    a('sleeper', 0x3A3028, W, .2, 0, 1.0, .1, .1)
    a('insulator', 0x6A4A3A, N, .7, 0, .3, 0, 0)
    a('clock-face', 0xEFE8D2, N, .4, 0, .3, 0, 0)
    # City Ironworks (THE CONVERTER)
    a('rust', 0x7A3E22, I, .25, .3, .8, 0, .2)
    a('slag', 0x1E1A1C, N, .55, 0, .6, 0, 0)
    a('ash', 0x5A5650, P, .05, 0, .8, 0, 0)
    a('ash-dark', 0x2E2C2A, P, .05, 0, .8, 0, 0)
    a('crane-yellow', 0xC8901E, N, .45, .3, .9, 0, .4)
    a('hazard-black', 0x1E1C1A, N, .45, .2, .7, 0, .3)
    return F


def bx(g, T, x0, x1, y0, y1, z0, z1, f, bevel=.02, cell=0, skip=()):
    """An axis-aligned box: chamfered on LOD0 only; faces cut to `cell` m (0: whole)."""
    sk.box(g, x0, x1, y0, y1, z0, z1, f, T if (bevel and T.bevel) else None, bevel if T.bevel else 0.0, cell=cell, skip=skip)


def part(g, sub, q=(0.0, 0.0, 0.0, 1.0), t=(0.0, 0.0, 0.0)):
    g.extend(sub.transformed(sk.affine(q, t)))


def qy(deg):
    return sk.quat_axis(0, 1, 0, math.radians(deg))


def qx(deg):
    return sk.quat_axis(1, 0, 0, math.radians(deg))


def qz(deg):
    return sk.quat_axis(0, 0, 1, math.radians(deg))


def obx(g, T, c, half, q, f, bevel=.01):
    t = Geo()
    sk.box(t, -half[0], half[0], -half[1], half[1], -half[2], half[2], f, T if (bevel and T.bevel) else None, bevel if T.bevel else 0.0, cell=0)
    part(g, t, q, c)


def rod(g, a, b, r, f, sides, caps=True, r1=None):
    if _dot(_sub(b, a), _sub(b, a)) < 1e-10:
        return
    sk.cylinder(g, a, b, r, f, sides, caps=caps, r1=r1)


def wall_rect(g, T, x0, x1, y0, y1, z, f, n=-1, cell=None):
    """A flat rectangle in the plane z facing -z (n -1) or +z."""
    c = (T.cell if cell is None else cell) or 1e9
    sk.grid_face(g, (x0, y0, z), (x1 - x0, 0, 0), (0, y1 - y0, 0), 0, 0, f, (0, 0, n), c)


def top_rect(g, T, x0, x1, z0, z1, y, f, cell=None):
    """A flat rectangle on the plane y facing up."""
    c = (T.cell if cell is None else cell) or 1e9
    sk.grid_face(g, (x0, y, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, f, (0, 1, 0), c)


def panel(g, T, x0, x1, y0, y1, d, f, cell=None, z=0.0):
    """A flat panel on a facade (kit frame): its face d off the wall toward the viewer, edges back to the wall."""
    wall_rect(g, T, x0, x1, y0, y1, z - d, f, -1, cell)
    if d > .004:
        for (a, b, n) in (((x0, y0), (x1, y0), (0, -1, 0)), ((x1, y1), (x0, y1), (0, 1, 0))):
            g.face(((a[0], a[1], z), (b[0], b[1], z), (b[0], b[1], z - d), (a[0], a[1], z - d)), f, n)
        for (x, n) in ((x0, (-1, 0, 0)), (x1, (1, 0, 0))):
            g.face(((x, y0, z), (x, y1, z), (x, y1, z - d), (x, y0, z - d)), f, n)


def slab(g, T, x0, x1, z0, z1, y0, y1, f, cell=None):
    """A flat floor slab (floor frame): its top at y1, edges down to y0."""
    top_rect(g, T, x0, x1, z0, z1, y1, f, cell)
    if y1 - y0 > .004:
        for (a, b, n) in (((x0, z0), (x1, z0), (0, 0, -1)), ((x1, z1), (x0, z1), (0, 0, 1))):
            g.face(((a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], y1, b[1]), (a[0], y1, a[1])), f, n)
        for (x, n) in ((x0, (-1, 0, 0)), (x1, (1, 0, 0))):
            g.face(((x, y0, z0), (x, y0, z1), (x, y1, z1), (x, y1, z0)), f, n)


def h01(*k):
    h = 2166136261
    for v in k:
        for ch in str(v):
            h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    h ^= h >> 15
    h = (h * 0x2C1B3C6D) & 0xFFFFFFFF
    h ^= h >> 12
    return h / 4294967296.0


# lettering: the city kit's 5 x 7 face
PIX = {
    'A': '.###.,#...#,#...#,#####,#...#,#...#,#...#', 'B': '####.,#...#,#...#,####.,#...#,#...#,####.',
    'C': '.####,#....,#....,#....,#....,#....,.####', 'D': '####.,#...#,#...#,#...#,#...#,#...#,####.',
    'E': '#####,#....,#....,####.,#....,#....,#####', 'F': '#####,#....,#....,####.,#....,#....,#....',
    'G': '.####,#....,#....,#..##,#...#,#...#,.####', 'H': '#...#,#...#,#...#,#####,#...#,#...#,#...#',
    'I': '###,.#.,.#.,.#.,.#.,.#.,###', 'J': '..###,...#.,...#.,...#.,...#.,#..#.,.##..',
    'K': '#...#,#..#.,#.#..,##...,#.#..,#..#.,#...#', 'L': '#....,#....,#....,#....,#....,#....,#####',
    'M': '#...#,##.##,#.#.#,#.#.#,#...#,#...#,#...#', 'N': '#...#,##..#,#.#.#,#..##,#...#,#...#,#...#',
    'O': '.###.,#...#,#...#,#...#,#...#,#...#,.###.', 'P': '####.,#...#,#...#,####.,#....,#....,#....',
    'Q': '.###.,#...#,#...#,#...#,#.#.#,#..#.,.##.#', 'R': '####.,#...#,#...#,####.,#.#..,#..#.,#...#', 'S': '.####,#....,#....,.###.,....#,....#,####.',
    'T': '#####,..#..,..#..,..#..,..#..,..#..,..#..', 'U': '#...#,#...#,#...#,#...#,#...#,#...#,.###.',
    'V': '#...#,#...#,#...#,#...#,.#.#.,.#.#.,..#..', 'W': '#...#,#...#,#...#,#.#.#,#.#.#,##.##,#...#',
    'X': '#...#,.#.#.,..#..,..#..,..#..,.#.#.,#...#', 'Y': '#...#,#...#,.#.#.,..#..,..#..,..#..,..#..',
    'Z': '#####,....#,...#.,..#..,.#...,#....,#####',
    '0': '.###.,#...#,#..##,#.#.#,##..#,#...#,.###.', '1': '..#..,.##..,..#..,..#..,..#..,..#..,.###.',
    '2': '.###.,#...#,....#,...#.,..#..,.#...,#####', '3': '####.,....#,....#,.###.,....#,....#,####.',
    '4': '...#.,..##.,.#.#.,#..#.,#####,...#.,...#.', '5': '#####,#....,####.,....#,....#,#...#,.###.',
    '6': '.###.,#....,#....,####.,#...#,#...#,.###.', '7': '#####,....#,...#.,..#..,.#...,.#...,.#...',
    '8': '.###.,#...#,#...#,.###.,#...#,#...#,.###.', '9': '.###.,#...#,#...#,.####,....#,....#,.###.',
    ':': '.,#,.,.,.,#,.', '.': '.,.,.,.,.,.,#', '·': '.,.,.,#,.,.,.', '-': '...,...,...,###,...,...,...',
    '!': '#,#,#,#,#,.,#', ' ': '...,...,...,...,...,...,...',
    '<': '...#...,..##...,.######,#######,.######,..##...,...#...', '>': '...#...,...##..,######.,#######,######.,...##..,...#...',
}


def pix_width(s, px):
    w = 0.0
    for ch in s:
        w += (len(PIX.get(ch, PIX[' ']).split(',')[0]) + 1) * px
    return w - px


def _runs(row):
    out, i, n = [], 0, len(row)
    while i < n:
        if row[i] != '#':
            i += 1
            continue
        j = i
        while j < n and row[j] == '#':
            j += 1
        out.append((i, j))
        i = j
    return out


def text_pix(g, s, x, y, z, px, f, depth=.003, align='c', dots=False):
    """5 x 7 lettering on a face facing -z (kit frame), its foot at y, centred on x: flat faces `depth` off the face (runs down
    the rows merged), or an LED matrix (`dots`)."""
    w = pix_width(s, px)
    cx = x - (w / 2 if align == 'c' else (w if align == 'r' else 0))
    zz = z - depth
    for ch in s:
        rows = PIX.get(ch, PIX[' ']).split(',')
        cw = len(rows[0])
        if dots:
            for r, row in enumerate(rows):
                yy = y + (6 - r) * px
                for i, c in enumerate(row):
                    if c == '#':
                        m = px * .14
                        a0, a1 = cx + i * px + m, cx + (i + 1) * px - m
                        g.face(((a0, yy + m, zz), (a1, yy + m, zz), (a1, yy + px - m, zz), (a0, yy + px - m, zz)), f, (0, 0, -1))
            cx += (cw + 1) * px
            continue
        rects, open_ = [], {}
        for r, row in enumerate(rows):
            nxt = {}
            for run in _runs(row):
                if run in open_:
                    rects[open_[run]][3] = r
                    nxt[run] = open_[run]
                else:
                    rects.append([run[0], run[1], r, r])
                    nxt[run] = len(rects) - 1
            open_ = nxt
        for (i, j, r0, r1) in rects:
            a0, a1 = cx + i * px, cx + j * px
            b0, b1 = y + (6 - r1) * px, y + (7 - r0) * px
            g.face(((a0, b0, zz), (a1, b0, zz), (a1, b1, zz), (a0, b1, zz)), f, (0, 0, -1))
        cx += (cw + 1) * px
    return w


def plate(g, T, s, x, y, w, h, f_plate, f_text, depth=.015, px=None, border=None, lines=None):
    """An enamel or steel plate (w x h, foot at y, centred on x) on a facade (kit frame) with 5 x 7 lettering (one line, or
    `lines`)."""
    panel(g, T, x - w / 2, x + w / 2, y, y + h, depth, f_plate, cell=0)
    if border is not None:
        e = min(w, h) * .07
        for (a0, a1, b0, b1) in ((x - w / 2 + e, x + w / 2 - e, y + e, y + e * 1.7), (x - w / 2 + e, x + w / 2 - e, y + h - e * 1.7, y + h - e),
                                 (x - w / 2 + e, x - w / 2 + e * 1.7, y + e, y + h - e), (x + w / 2 - e * 1.7, x + w / 2 - e, y + e, y + h - e)):
            wall_rect(g, T, a0, a1, b0, b1, -depth - .002, border, -1, 0)
    rows = lines or [s]
    if px is None:
        px = min(h * .62 / (7 * len(rows) + 3 * (len(rows) - 1)), (w * .86) / max(pix_width(r, 1.0) for r in rows))
    total = 7 * px * len(rows) + 3 * px * (len(rows) - 1)
    for k, r in enumerate(rows):
        text_pix(g, r, x, y + (h - total) / 2 + (len(rows) - 1 - k) * 10 * px, -depth, px, f_text, depth=.002)


# ---------------------------------------------------------------------------------------------------------------------------
# Floors (floor frame: centred, y up from the floor's top, z along the street)
# ---------------------------------------------------------------------------------------------------------------------------
def floor_carpet(g, T):
    # office carpet tiles, 50 cm, two dye lots and the damp creeping through (a stain field across the tile)
    c = F['carpet']
    for i in range(4):
        for j in range(4):
            x0, z0 = -1 + i * .5, -1 + j * .5
            k = h01('carpet', i, j)
            damp = k < .22
            f = F['carpet-damp'] if damp else (F['carpet-b'] if h01('lot', i, j) < .3 else c)
            slab(g, T, x0 + .004, x0 + .496, z0 + .004, z0 + .496, 0, .03 + .005 * k, f, cell=0)


def floor_pool_tile(g, T):
    slab(g, T, -1, 1, -1, 1, 0, .025, F['tile-pool'], cell=1.0 if T.lod == 0 else 0)


def floor_flood(g, T):
    slab(g, T, -1, 1, -1, 1, 0, .02, F['tile-pool'], cell=0)
    top_rect(g, T, -1, 1, -1, 1, .042, F['water'], cell=1.0 if T.lod == 0 else 0)


def floor_drain(g, T):
    slab(g, T, -.6, .6, -.3, .3, 0, .012, F['iron-black'], cell=0)
    n = 9 if T.lod == 0 else 4
    for i in range(n):
        x = -.5 + (i + .5) * 1.0 / n
        top_rect(g, T, x - .03, x + .03, -.22, .22, .02, glow(0x40E0E0, 1.6), cell=0)
        if T.detail:
            slab(g, T, x - .055, x - .03, -.25, .25, .012, .028, F['iron-black'], cell=0)


def floor_ring(g, T):
    # an inflatable ring floating flat in the flood: a flattened torus, pink and white quarters
    n, m = (24, 6) if T.lod == 0 else (10, 4)
    R, r, hy = .32, .12, .025
    for i in range(n):
        a0, a1 = 2 * PI * i / n, 2 * PI * (i + 1) / n
        f = F['float-pink'] if (i * 4 // n) % 2 == 0 else F['float-white']
        for j in range(m):
            b0, b1 = 2 * PI * j / m, 2 * PI * (j + 1) / m

            def P(a, b):
                rr = R + r * math.cos(b)
                return (rr * math.cos(a), .028 + hy * math.sin(b), rr * math.sin(a))

            def Nn(a, b):
                return _norm((math.cos(b) * math.cos(a), math.sin(b) * 2.5, math.cos(b) * math.sin(a)))
            g.face((P(a0, b0), P(a1, b0), P(a1, b1), P(a0, b1)), f, normals=(Nn(a0, b0), Nn(a1, b0), Nn(a1, b1), Nn(a0, b1)))


def floor_litter(g, T):
    # the evacuation's leaflets: STAY INDOORS sheets and pages blown flat against the street
    n = 22 if T.lod == 0 else 8
    for i in range(n):
        x, z = -.85 + 1.7 * h01('lx', i), -.85 + 1.7 * h01('lz', i)
        a = 2 * PI * h01('la', i)
        w, l = .15 + .08 * h01('lw', i), .21 + .08 * h01('ll', i)
        ca, sa = math.cos(a), math.sin(a)
        y = .004 + .0011 * (i % 9)
        pts = [(x + ca * u - sa * v, y, z + sa * u + ca * v) for u, v in ((-w / 2, -l / 2), (w / 2, -l / 2), (w / 2, l / 2), (-w / 2, l / 2))]
        f = F['paper'] if h01('lc', i) < .6 else (F['paper-old'] if h01('lc2', i) < .7 else F['decon-teal'])
        g.face(pts, f, (0, 1, 0))
        if T.detail and f == F['paper']:
            # the leaflet's black heading band
            hp = [(x + ca * u - sa * v, y + .0005, z + sa * u + ca * v) for u, v in ((-w * .4, l * .25), (w * .4, l * .25), (w * .4, l * .42), (-w * .4, l * .42))]
            g.face(hp, F['stencil-black'], (0, 1, 0))


def floor_decon(g, T):
    # the decon line's rubber matting: teal squares on a white grid (never red on the ground)
    slab(g, T, -1, 1, -1, 1, 0, .015, F['decon-white'], cell=0)
    for i in range(4):
        for j in range(4):
            x0, z0 = -1 + i * .5 + .04, -1 + j * .5 + .04
            slab(g, T, x0, x0 + .42, z0, z0 + .42, .015, .026, F['decon-teal'], cell=0)


def floor_rails(g, T):
    # tram rails converging down the road: two grooved rails in a strip of setts
    slab(g, T, -.9, .9, -1, 1, 0, .012, F['setts'], cell=.5 if T.lod == 0 else 0)
    for x in (-.7175, .7175):
        slab(g, T, x - .035, x + .035, -1, 1, .012, .028, F['rail'], cell=0)
        if T.detail:
            top_rect(g, T, x - .012, x + .012, -1, 1, .0285, F['iron-black'], cell=0)


def floor_platform(g, T):
    slab(g, T, -1, 1, -1, 1, 0, .02, F['tile-cream'], cell=1.0 if T.lod == 0 else 0)


def floor_ash(g, T):
    # ash drifted into the street's low places, in soft patches, darker in the middle
    n = 7 if T.lod == 0 else 3
    for i in range(n):
        cx, cz = -.6 + 1.2 * h01('ax', i), -.6 + 1.2 * h01('az', i)
        r = .25 + .2 * h01('ar', i)
        m = 12 if T.lod == 0 else 6
        pts = []
        for k in range(m):
            a = 2 * PI * k / m
            rr = r * (.75 + .5 * h01('aj', i, k))
            pts.append((max(-.98, min(.98, cx + math.cos(a) * rr)), .006 + .002 * i, max(-.98, min(.98, cz + math.sin(a) * rr))))
        g.face(pts[::-1], F['ash'], (0, 1, 0))
        g.face([(cx + (p[0] - cx) * .45, p[1] + .004, cz + (p[2] - cz) * .45) for p in pts][::-1], F['ash-dark'], (0, 1, 0))


def floor_slag(g, T):
    # slag crust in the gutter: dark, with faint steady orange cracks (it never pulses: never a fake hazard)
    slab(g, T, -.35, .35, -1, 1, 0, .03, F['slag'], cell=.5 if T.lod == 0 else 0)
    n = 6 if T.lod == 0 else 2
    for i in range(n):
        z0 = -.8 + 1.6 * h01('sz', i)
        pts = [(-.28 + .56 * k / 4, z0 + .12 * math.sin(k * 1.7 + i)) for k in range(5)]
        for (a, b) in zip(pts, pts[1:]):
            dx, dz = b[0] - a[0], b[1] - a[1]
            l = math.hypot(dx, dz) or 1
            nx, nz = -dz / l * .012, dx / l * .012
            g.face(((a[0] - nx, .032, a[1] - nz), (b[0] - nx, .032, b[1] - nz), (b[0] + nx, .032, b[1] + nz), (a[0] + nx, .032, a[1] + nz)), glow(0xFF7A28, .9), (0, 1, 0))


def floor_plates(g, T):
    slab(g, T, -1, 1, -1, 1, 0, .02, F['iron-raw'], cell=1.0 if T.lod == 0 else 0)
    if T.detail:
        for i in range(-3, 4):
            for j in range(-3, 4):
                x, z = i * .27, j * .27
                g.face(((x - .05, .022, z), (x, .022, z - .02), (x + .05, .022, z), (x, .022, z + .02)), F['steel-dark'], (0, 1, 0))
        for x in (-1, 0, 1):
            slab(g, T, x - .01 if x > -1 else -1, x + .01 if x < 1 else 1, -1, 1, .02, .03, F['rust'], cell=0)


# ---------------------------------------------------------------------------------------------------------------------------
# Facades (kit frame: the face at z 0, faces toward -z, x to the viewer's right, from the bay's foot; turned at the end)
# ---------------------------------------------------------------------------------------------------------------------------
HB = 1.25  # half a bay


def skirting(g, T, d=.025):
    panel(g, T, -HB, HB, 0, .16, d, F['skirting'], cell=0)
    if T.detail:
        panel(g, T, -HB, HB, .16, .18, d * .6, F['skirting'], cell=0)


def wall_wallpaper(g, T, threshold=False):
    # the Backrooms' own wallpaper on an outside wall: stripes, a skirting board, the damp stains (and at the threshold a dado
    # rail and no windows anywhere)
    stripe = .25
    n = int(2.5 / stripe)
    for i in range(n):
        x0 = -HB + i * stripe
        f = F['wallpaper'] if i % 2 == 0 else F['wallpaper-stripe']
        wall_rect(g, T, x0, x0 + stripe, .18, 3.1, -.012, f, -1, .75 if T.lod == 0 else 0)
    skirting(g, T)
    if threshold:
        panel(g, T, -HB, HB, .98, 1.04, .02, F['skirting'], cell=0)
    if T.detail:
        for k in range(3):
            x = -1 + 2 * h01('st', threshold, k)
            y = .3 + 2.2 * h01('sy', threshold, k)
            r = .2 + .25 * h01('sr', threshold, k)
            m = 10
            pts = [(max(-HB, min(HB, x + math.cos(2 * PI * j / m) * r * (.8 + .4 * h01('sj', k, j)))), max(.2, min(3.05, y + math.sin(2 * PI * j / m) * r * 1.4)), -.0135) for j in range(m)]
            g.face(pts, F['stain'], (0, 0, -1))


def upper_wallpaper(g, T):
    stripe = .25
    for i in range(int(2.5 / stripe)):
        x0 = -HB + i * stripe
        wall_rect(g, T, x0, x0 + stripe, 0, 3.0, -.012, F['wallpaper'] if i % 2 == 0 else F['wallpaper-stripe'], -1, 1.5 if T.lod == 0 else 0)
    # a picture rail every storey
    panel(g, T, -HB, HB, 2.6, 2.66, .03, F['skirting'], cell=0)


def wall_pool(g, T, upper=False):
    h = 3.0 if upper else 3.1
    wall_rect(g, T, -HB, HB, 0, h, -.03, F['tile-pool'], -1, 1.0 if T.lod == 0 else 0)
    for (x, n) in ((-HB, (-1, 0, 0)), (HB, (1, 0, 0))):
        g.face(((x, 0, 0), (x, h, 0), (x, h, -.03), (x, 0, -.03)), F['tile-pool'], n)
    if upper:
        panel(g, T, -HB, HB, 2.3, 2.55, .045, F['tile-aqua'], cell=0)
    else:
        panel(g, T, -HB, HB, 0, .15, .045, F['tile-teal'], cell=0)
        panel(g, T, -HB, HB, 1.15, 1.35, .045, F['tile-aqua'], cell=0)
        if T.detail:
            for i in range(10):
                x0 = -HB + i * .25
                panel(g, T, x0 + .01, x0 + .115, 1.38, 1.48, .045, F['mosaic-blue'] if i % 2 else F['mosaic-pale'], cell=0)


def wall_growth(g, T):
    # Ward Zero's containment growth climbing the wall: violet blooms, flat below the clear height, with low nodules
    n = 7 if T.lod == 0 else 3
    for i in range(n):
        cx, cy = -1.0 + 2.0 * h01('gx', i), .15 + 2.2 * h01('gy', i) ** 1.6
        r = .16 + .26 * h01('gr', i)
        m = 12 if T.lod == 0 else 6
        pts = []
        for k in range(m):
            a = 2 * PI * k / m
            rr = r * (.6 + .6 * h01('gj', i, k))
            pts.append((max(-HB, min(HB, cx + math.cos(a) * rr)), max(.02, min(3.08, cy + math.sin(a) * rr * 1.3)), -.008 - .001 * i))
        g.face(pts, F['growth'], (0, 0, -1))
        if T.detail:
            inner = [(cx + (p[0] - cx) * .55, cy + (p[1] - cy) * .55, p[2] - .006) for p in pts]
            g.face(inner, F['growth-skin'], (0, 0, -1))
            for k in range(3):
                a = 2 * PI * h01('gn', i, k)
                px, py = cx + math.cos(a) * r * .35, cy + math.sin(a) * r * .35
                if -HB + .05 < px < HB - .05 and .1 < py < 3.0:
                    sk.lathe(g, [(.045, 0.0), (.04, .02), (.02, .035), (0.0, .04)], (px, py, -.01), glow(0xB070FF, 1.2), 6, axis=(0, 0, -1))


def wall_cladding(g, T, upper=False):
    # the campus's white cladding: panels 1.25 m wide on joints, a teal band at the ground storey's head
    h = 3.0 if upper else 3.1
    for i in range(2):
        x0 = -HB + i * 1.25
        rows = [(0, h)] if upper else [(0, 2.35), (2.65, h)]
        for (y0, y1) in rows:
            panel(g, T, x0 + .012, x0 + 1.238, y0 + .012, y1 - .012, .04, F['cladding-white'], cell=1.0 if T.lod == 0 else 0)
    wall_rect(g, T, -HB, HB, 0, h, -.01, F['cladding-joint'], -1, 0)
    if not upper:
        panel(g, T, -HB, HB, 2.35, 2.65, .045, F['cladding-teal'], cell=0)
    elif T.detail:
        # louvred vents (above the clear height: proud up to 15 cm)
        for x in (-.6, .6):
            bx(g, T, x - .3, x + .3, 1.6, 2.2, -.12, -.03, F['steel-white'], bevel=.01)
            for k in range(5):
                y = 1.66 + k * .11
                obx(g, T, (x, y, -.1), (.28, .012, .03), qx(-35), F['cladding-joint'])


def upper_sheet(g, T):
    # quarantine sheeting draped from above, down to the clear height: vertical folds, tied with yellow straps
    n = 10 if T.lod == 0 else 4
    xs = [-HB + 2.5 * i / n for i in range(n + 1)]
    rows = [0.04, .8, 1.6, 2.4, 3.0] if T.lod == 0 else [0.04, 3.0]
    for i in range(n):
        for j in range(len(rows) - 1):
            def Z(k, y):
                return -.06 - .2 * (.5 + .5 * math.sin(k * PI + y * .7)) * (1 - .3 * h01('sh', k))
            a, b = xs[i], xs[i + 1]
            y0, y1 = rows[j], rows[j + 1]
            p = [(a, y0, Z(i, y0)), (b, y0, Z(i + 1, y0)), (b, y1, Z(i + 1, y1)), (a, y1, Z(i, y1))]
            g.face(p, F['sheet'], (0, 0, -1))
    if T.detail:
        for y in (.9, 2.1):
            bx(g, T, -HB, HB, y, y + .05, -.3, -.27, F['strap'], bevel=0)


def wall_bluebrick(g, T):
    panel(g, T, -HB, HB, 0, 3.1, .03, F['brick-blue'], cell=.75 if T.lod == 0 else 0)
    panel(g, T, -HB, HB, 2.2, 2.6, .045, F['iron-green'], cell=0)
    if T.detail:
        for x in [-HB + .1 + .2 * k for k in range(12)]:
            for y in (2.27, 2.53):
                obx(g, T, (x, y, -.0475), (.012, .012, .0025), (0, 0, 0, 1), F['iron-green'])


def wall_station(g, T, upper=False):
    if upper:
        panel(g, T, -HB, HB, 0, 3.0, .03, F['brick-glazed'], cell=1.0 if T.lod == 0 else 0)
        panel(g, T, -HB, HB, 2.6, 2.8, .1, F['tile-green'], cell=0)
        return
    panel(g, T, -HB, HB, 0, 1.2, .03, F['tile-green'], cell=.75 if T.lod == 0 else 0)
    panel(g, T, -HB, HB, 1.2, 3.1, .03, F['tile-cream'], cell=.75 if T.lod == 0 else 0)
    panel(g, T, -HB, HB, 1.2, 1.3, .045, F['tile-cream'], cell=0)
    panel(g, T, -HB, HB, 2.85, 2.95, .045, F['tile-green'], cell=0)


def wall_plate(g, T):
    # riveted steel plates over the wall, rusting at the seams
    for i in range(2):
        for j in range(2):
            x0, y0 = -HB + i * 1.25, j * 1.55
            panel(g, T, x0 + .01, x0 + 1.24, y0 + .01, y0 + 1.54, .035 + .005 * ((i + j) % 2), F['iron-raw'] if (i + j) % 2 else F['rust'], cell=.75 if T.lod == 0 else 0)
            if T.tiny:
                for k in range(8):
                    x = x0 + .08 + k * (1.09 / 7)
                    for y in (y0 + .07, y0 + 1.48):
                        obx(g, T, (x, y, -.043), (.014, .014, .005), (0, 0, 0, 1), F['steel-dark'])


def wall_furnace(g, T, upper=False):
    h = 3.0 if upper else 3.1
    panel(g, T, -HB, HB, 0, h, .03, F['brick-fire'], cell=.75 if T.lod == 0 else 0)
    # iron straps (proud above the clear height only on the upper storeys)
    d = .12 if upper else .045
    for y in ((.4, 2.6) if upper else (2.9,)):
        panel(g, T, -HB, HB, y, y + .12, d, F['iron-black'], cell=0)
    if upper:
        for x in (-.9, .9):
            panel(g, T, x - .06, x + .06, 0, h, .08, F['iron-black'], cell=0)
    panel(g, T, -HB, HB, h - .5 if upper else 2.5, h if upper else 2.9, .031, F['brick-soot'], cell=0)


def detail_exit(g, T):
    # a door that leads nowhere, and its EXIT sign: the door painted flat on the wall, its kickplate, the green sign above
    panel(g, T, -.55, .55, 0, 2.2, .02, F['alu'], cell=0)
    panel(g, T, -.47, .47, 0, 2.12, .03, F['veneer'], cell=0)
    panel(g, T, -.47, .47, 0, .25, .035, F['kickplate'], cell=0)
    if T.detail:
        obx(g, T, (.34, 1.02, -.04), (.06, .015, .008), (0, 0, 0, 1), F['alu'])
    panel(g, T, -.3, .3, 2.38, 2.7, .03, F['exit-green'], cell=0)
    text_pix(g, 'EXIT', 0, 2.45, -.03, .026, glow(0xF0FFE8, 2.2), depth=.004)


def detail_missing(g, T):
    # a rectangle of brick gone missing, the yellow wallpaper behind it: a texture that failed to load
    wall_rect(g, T, -.62, .62, 1.05, 2.15, -.006, F['wallpaper'], -1, 0)
    if T.detail:
        for i in range(5):
            x0 = -.62 + i * .25
            wall_rect(g, T, x0 + .12, x0 + .25, 1.05, 2.15, -.007, F['wallpaper-stripe'], -1, 0)
    # the ragged brick edge round it (flat, 4 cm)
    for k in range(14):
        a = 2 * PI * k / 14
        x, y = math.cos(a) * .72, 1.6 + math.sin(a) * .62
        w, hh = .2 + .1 * h01('mb', k), .075
        panel(g, T, max(-.8, x - w / 2), min(.8, x + w / 2), max(.9, y - hh / 2), min(2.3, y + hh / 2), .04, F['brick-stock'], cell=0)


def detail_pool_plate(g, T):
    plate(g, T, 'NO RUNNING', 0, 1.52, .58, .36, F['enamel-cream'], F['enamel-cobalt'], depth=.012, border=F['enamel-cobalt'])


def detail_glassblock(g, T):
    panel(g, T, -.8, .8, .8, 2.4, .02, F['tile-pool'], cell=0)
    n = 7 if T.lod == 0 else 1
    s = 1.4 / n
    for i in range(n):
        for j in range(n):
            x0, y0 = -.7 + i * s, .9 + j * s
            m = .012 if n > 1 else 0
            wall_rect(g, T, x0 + m, x0 + s - m, y0 + m, y0 + s - m, -.035, glow(0x60D8F0, .9 + .3 * h01('gb', i, j)), -1, 0)


def detail_doorway(g, T):
    # a tiled doorway with no room behind it: the tile surround, a dark void, coping for a sill
    panel(g, T, -.75, .75, 0, 2.5, .03, F['tile-aqua'], cell=0)
    wall_rect(g, T, -.55, .55, .12, 2.25, -.032, F['void'], -1, 0)
    panel(g, T, -.6, .6, 0, .12, .05, F['coping'], cell=0)
    if T.detail:
        for i in range(12):
            a = PI * i / 11
            x, y = math.cos(a) * .64, 2.25 + math.sin(a) * .2
            panel(g, T, x - .045, x + .045, y - .04, y + .04, .045, F['mosaic-blue'], cell=0)


def detail_poster(g, T):
    panel(g, T, -.4, .4, 1.1, 2.2, .006, F['paper'], cell=0)
    wall_rect(g, T, -.36, .36, 1.88, 2.14, -.0075, F['stencil-black'], -1, 0)
    text_pix(g, 'STAY', 0, 1.62, -.0065, .024, F['stencil-black'], depth=.0012)
    text_pix(g, 'INDOORS', 0, 1.42, -.0065, .017, F['stencil-black'], depth=.0012)
    wall_rect(g, T, -.36, .36, 1.16, 1.26, -.0075, F['decon-teal'], -1, 0)
    if T.detail:
        text_pix(g, 'BY ORDER', 0, 1.96, -.0078, .011, F['paper'], depth=.0012)


def detail_evac(g, T, right):
    # EVACUATION ROUTE, pointing away from the door, toward the camp
    panel(g, T, -.65, .65, 2.1, 2.6, .015, F['enamel-green'], cell=0)
    text_pix(g, 'EVACUATION', -.12 if right else .12, 2.38, -.015, .016, F['enamel-cream'], depth=.003)
    text_pix(g, 'ROUTE', -.12 if right else .12, 2.2, -.015, .016, F['enamel-cream'], depth=.003)
    text_pix(g, '>' if right else '<', .47 if right else -.47, 2.2, -.015, .032, F['enamel-cream'], depth=.003)


def detail_roundel(g, T):
    # the transit mark: a red ring and a blue bar (no station's name: the world, not the signs)
    n = 24 if T.lod == 0 else 10
    R0, R1 = .38, .48
    for i in range(n):
        a0, a1 = 2 * PI * i / n, 2 * PI * (i + 1) / n
        p = [(math.cos(a0) * R1, 2.2 + math.sin(a0) * R1, -.03), (math.cos(a1) * R1, 2.2 + math.sin(a1) * R1, -.03),
             (math.cos(a1) * R0, 2.2 + math.sin(a1) * R0, -.03), (math.cos(a0) * R0, 2.2 + math.sin(a0) * R0, -.03)]
        g.face(p, F['enamel-red'], (0, 0, -1))
        q = [(0, 2.2, -.025), (math.cos(a0) * R0, 2.2 + math.sin(a0) * R0, -.025), (math.cos(a1) * R0, 2.2 + math.sin(a1) * R0, -.025)]
        g.face(q, F['enamel-cream'], (0, 0, -1))
    panel(g, T, -.5, .5, 2.11, 2.29, .04, F['enamel-cobalt'], cell=0)
    text_pix(g, 'WAY OUT', 0, 2.14, -.04, .017, F['enamel-cream'], depth=.002)


def detail_rollerdoor(g, T):
    panel(g, T, -1.2, 1.2, 2.6, 2.9, .05, F['steel-dark'], cell=0)
    n = 26 if T.lod == 0 else 1
    for i in range(n):
        y0 = 2.6 * i / n
        y1 = 2.6 * (i + 1) / n
        if n > 1:
            g.face(((-1.15, y0, -.012), (1.15, y0, -.012), (1.15, (y0 + y1) / 2, -.035), (-1.15, (y0 + y1) / 2, -.035)), F['galv'], (0, -.6, -.8))
            g.face(((-1.15, (y0 + y1) / 2, -.035), (1.15, (y0 + y1) / 2, -.035), (1.15, y1, -.012), (-1.15, y1, -.012)), F['galv'], (0, .6, -.8))
        else:
            wall_rect(g, T, -1.15, 1.15, 0, 2.6, -.03, F['galv'], -1, 0)
    panel(g, T, -1.2, -1.15, 0, 2.6, .045, F['steel-dark'], cell=0)
    panel(g, T, 1.15, 1.2, 0, 2.6, .045, F['steel-dark'], cell=0)
    if T.detail:
        for k in range(4):
            x = -1 + 2 * h01('rd', k)
            wall_rect(g, T, x - .04, x + .04, .2 + 1.6 * h01('rh', k), 2.55, -.036, F['rust'], -1, 0)


def detail_iron_sign(g, T):
    plate(g, T, '', 0, 1.5, .7, .5, F['enamel-cream'], F['enamel-red'], depth=.012, border=F['enamel-red'], lines=['SAFETY', 'FIRST'])


# ---------------------------------------------------------------------------------------------------------------------------
# Fittings above the clear height (kit frame: the wall plate at the origin, out toward -z)
# ---------------------------------------------------------------------------------------------------------------------------
LAMP = 0xFFFFFF  # lamp heads glow white: the runtime tints them with each lamp's light and state


def wallplate(g, T, w=.16, h=.26, f=None):
    bx(g, T, -w / 2, w / 2, -h / 2, h / 2, -.04, 0, f or F['iron-black'], bevel=.008)


def fit_common_lamp(g, T):
    # a cobra head on a swan-neck wall arm (the city's sodium lamp, for the stretches the bleed leaves sodium)
    wallplate(g, T, .14, .3)
    pts = [(0, 0, -.04), (0, .12, -.4), (0, .2, -.8), (0, .16, -1.05)]
    for a, b in zip(pts, pts[1:]):
        rod(g, a, b, .035, F['iron-green'], T.small)
    bx(g, T, -.16, .16, -.02, .14, -1.3, -.95, F['iron-green'], bevel=.03)
    bx(g, T, -.12, .12, -.12, -.02, -1.27, -.98, F['iron-green'], bevel=.01)
    top = Geo()
    sk.grid_face(top, (-.11, -.125, -1.26), (.22, 0, 0), (0, 0, .27), 0, 0, glow(LAMP, 2.6), (0, -1, 0), 1e9)
    g.extend(top)


def fit_level0_lamp(g, T):
    # a fluorescent troffer on two short arms, buzzing: the office's light in the street
    wallplate(g, T, .9, .12, F['alu'])
    for x in (-.45, .45):
        bx(g, T, x - .02, x + .02, -.02, .02, -.75, -.04, F['alu'], bevel=0)
    bx(g, T, -.65, .65, -.2, -.06, -.98, -.62, F['alu'], bevel=.01)
    g.face(((-.6, -.205, -.95), (.6, -.205, -.95), (.6, -.205, -.65), (-.6, -.205, -.65)), glow(LAMP, 2.2), (0, -1, 0))
    if T.detail:
        for k in range(5):
            x = -.48 + k * .24
            bx(g, T, x - .006, x + .006, -.212, -.2, -.95, -.65, F['alu'], bevel=0)


def fit_pool_lamp(g, T):
    # a pool lamp set flush and low in the wall: a chrome ring and the glowing lens (4 cm proud)
    n = 18 if T.lod == 0 else 8
    cy = .7
    ring = [(math.cos(2 * PI * i / n) * .24, cy + math.sin(2 * PI * i / n) * .24, -.022) for i in range(n)]
    lens = [(math.cos(2 * PI * i / n) * .17, cy + math.sin(2 * PI * i / n) * .17, -.036) for i in range(n)]
    for i in range(n):
        j = (i + 1) % n
        g.face((ring[i], ring[j], lens[j], lens[i]), F['chrome'], (0, 0, -1))
        g.face(((ring[i][0], ring[i][1], 0), (ring[j][0], ring[j][1], 0), ring[j], ring[i]), F['chrome'],
               _norm((ring[i][0] + ring[j][0], ring[i][1] + ring[j][1] - 2 * cy, 0)))
    g.face(lens, glow(LAMP, 2.0), (0, 0, -1))


def fit_ward_lamp(g, T):
    # Ward Zero's red turning lamp: a beacon on a bracket (the runtime turns its glow)
    wallplate(g, T, .14, .22)
    bx(g, T, -.05, .05, -.05, .05, -.85, -.04, F['steel-white'], bevel=.01)
    bx(g, T, -.16, .16, -.06, .06, -1.08, -.78, F['steel-white'], bevel=.01)
    sk.lathe(g, [(.13, .06), (.13, .1), (.12, .12)], (0, 0, -.93), F['steel-white'], T.small + 4, cap1=True)
    sk.lathe(g, [(.115, .12), (.115, .3), (.08, .4), (0.0, .44)], (0, 0, -.93), glow(LAMP, 2.4), T.small + 4)


def fit_signal(g, T):
    # a railway signal head on a bracket: one aspect lit (the runtime's red or green), one dark
    wallplate(g, T, .14, .22)
    bx(g, T, -.04, .04, -.04, .04, -.42, -.04, F['iron-black'], bevel=0)
    bx(g, T, -.17, .17, -.58, .48, -.66, -.42, F['board-black'], bevel=.02)
    n = 14 if T.lod == 0 else 8
    for (cy, lit) in ((.2, True), (-.22, False)):
        lens = [(math.cos(2 * PI * i / n) * .1, cy + math.sin(2 * PI * i / n) * .1, -.665) for i in range(n)]
        g.face(lens, glow(LAMP, 2.6) if lit else F['glass-dark'], (0, 0, -1))
        if T.detail:
            obx(g, T, (0, cy + .12, -.675), (.13, .012, .015), (0, 0, 0, 1), F['board-black'])


def fit_worklamp(g, T):
    wallplate(g, T, .16, .2, F['steel-dark'])
    bx(g, T, -.03, .03, -.03, .03, -.5, -.04, F['steel-dark'], bevel=0)
    sub = Geo()
    sides = T.small + 2
    r, length = .14, .3
    sk.lathe(sub, [(r * 1.25, 0.0), (r * 1.25, .05), (r * 1.05, .07)], (0, 0, 0), F['steel-dark'], sides, axis=(0, -1, 0), cap1=True)
    sk.lathe(sub, [(r * .9, .07), (r * .95, .07 + length * .45), (r * .7, .07 + length * .8), (0.0, .07 + length * .92)], (0, 0, 0), glow(LAMP, 2.4), sides, axis=(0, -1, 0))
    if T.detail:
        for k in range(4):
            a = 2 * PI * k / 4 + PI / 4
            p0 = (math.cos(a) * r * 1.12, -.07, math.sin(a) * r * 1.12)
            p1 = (math.cos(a) * r * 1.12, -.07 - length * .55, math.sin(a) * r * 1.12)
            rod(sub, p0, p1, .008, F['iron-black'], 4, caps=False)
            rod(sub, p1, (0, -.07 - length * 1.02, 0), .008, F['iron-black'], 4, caps=False)
    part(g, sub, (0, 0, 0, 1), (0, -.04, -.62))


def fit_highbay(g, T):
    wallplate(g, T, .16, .3, F['steel-dark'])
    pts = [(0, 0, -.04), (0, .15, -.5), (0, .15, -.88)]
    for a, b in zip(pts, pts[1:]):
        rod(g, a, b, .04, F['steel-dark'], T.small)
    # the reflector's dome, mouth down (profile bottom to top: the rim at -0.4, the neck at 0)
    sk.lathe(g, [(.36, -.4), (.34, -.36), (.11, -.08), (.06, 0.0)], (0, .15, -.88), F['galv'], T.round, axis=(0, 1, 0))
    n = T.round
    g.face([(math.cos(2 * PI * i / n) * .33, .15 - .38, -.88 + math.sin(2 * PI * i / n) * .33) for i in range(n)], glow(LAMP, 2.4), (0, -1, 0))


def fit_horn(g, T):
    # a PA horn pair on a bracket: REMAIN INDOORS
    wallplate(g, T, .14, .2, F['steel-white'])
    bx(g, T, -.03, .03, -.03, .03, -.35, -.04, F['steel-white'], bevel=0)
    for (x, yaw) in ((-.16, 12), (.16, -12)):
        sub = Geo()
        sk.lathe(sub, [(.05, 0.0), (.06, .2), (.11, .36), (.16, .4)], (0, 0, 0), F['steel-white'], T.round, axis=(0, 0, -1))
        part(g, sub, qy(yaw), (x * .4, .05, -.36))


def fit_clock(g, T):
    # a bracket clock at 6:47, its two faces along the street (the hands: minute at 282 degrees, hour at 203.5)
    wallplate(g, T, .14, .3, F['cast-green'])
    bx(g, T, -.03, .03, .38, .44, -.6, -.04, F['cast-green'], bevel=0)
    n = 24 if T.lod == 0 else 10
    R = .34
    for side in (-1, 1):
        xs = side * .07
        ring = [(xs, math.cos(2 * PI * i / n) * (R + .05), -.5 + math.sin(2 * PI * i / n) * (R + .05)) for i in range(n)]
        dial = [(xs + side * .004, math.cos(2 * PI * i / n) * R, -.5 + math.sin(2 * PI * i / n) * R) for i in range(n)]
        g.face(ring, F['cast-green'], (side, 0, 0))
        g.face(dial, glow(0xF2E6C8, 1.2), (side, 0, 0))
        for (deg, length, w) in ((282.0, R * .8, .022), (203.5, R * .52, .035)):
            a = math.radians(deg)
            # on the face seen from +x (side 1) twelve is up and the hand turns toward -z; mirrored on the other face
            dz, dy = -math.sin(a) * side, math.cos(a)
            tip = (xs + side * .01, dy * length, -.5 + dz * length)
            nz, ny = -dy, dz
            base = [(xs + side * .01, ny * w / 2, -.5 + nz * w / 2), (xs + side * .01, -ny * w / 2, -.5 - nz * w / 2)]
            g.face((base[0], tip, base[1]), F['board-black'], (side, 0, 0))
    bx(g, T, -.06, .06, -.38, .38, -.88, -.12, F['cast-green'], bevel=.02)


def fit_board(g, T):
    # a departure board on brackets: 6:47 EVACUATION SPECIAL · DELAYED, amber LED dots
    wallplate(g, T, 2.0, .1, F['board-black'])
    for x in (-.9, .9):
        bx(g, T, x - .03, x + .03, -.03, .03, -.5, -.04, F['iron-black'], bevel=0)
    bx(g, T, -1.2, 1.2, -.4, .4, -.69, -.5, F['board-black'], bevel=.02)
    amber = glow(0xFFB030, 2.4)
    text_pix(g, '6:47 EVACUATION', 0, .08, -.69, .022, amber, depth=.004, dots=T.lod == 0)
    text_pix(g, 'SPECIAL', -.42, -.18, -.69, .022, amber, depth=.004, dots=T.lod == 0)
    text_pix(g, 'DELAYED', .55, -.18, -.69, .022, glow(0xFF6A2A, 2.4), depth=.004, dots=T.lod == 0)


def fit_anchor(g, T):
    wallplate(g, T, .2, .2, F['iron-black'])
    if T.detail:
        sk.lathe(g, [(.03, .04), (.03, .08)], (0, 0, 0), F['galv'], 6, axis=(0, 0, -1), cap1=True)


# ---------------------------------------------------------------------------------------------------------------------------
# Spans (x across the street from the cable's start; scaled to fit) and hung pieces (from the hanging point, down)
# ---------------------------------------------------------------------------------------------------------------------------
def span_wire(g, T):
    rod(g, (0, 0, 0), (1, 0, 0), .012, F['cable'], 4 if T.lod == 0 else 3, caps=False)


def span_lanerope(g, T):
    rod(g, (0, 0, 0), (2, 0, 0), .01, F['rope'], 4, caps=False)
    n = 8
    for i in range(n):
        x0 = i * .25 + .02
        f = F['float-blue'] if i % 2 else F['float-white']
        sk.lathe(g, [(.04, 0.0), (.1, .04), (.1, .17), (.04, .21)], (x0, 0, 0), f, T.small + 2, axis=(1, 0, 0), cap0=True, cap1=True)


def span_pennants(g, T):
    rod(g, (0, 0, 0), (2, 0, 0), .008, F['rope'], 4, caps=False)
    for i in range(8):
        x0 = i * .25 + .02
        f = F['pennant-aqua'] if i % 3 == 0 else (F['float-white'] if i % 3 == 1 else F['pennant-navy'])
        g.face(((x0, -.01, .0), (x0 + .2, -.01, 0), (x0 + .1, -.42, 0)), f, (0, 0, 1))
        g.face(((x0 + .2, -.01, 0), (x0, -.01, 0), (x0 + .1, -.42, 0)), f, (0, 0, -1))


def span_tent(g, T):
    # a decon tent's canopy across the street: a shallow ridge of PVC over a galvanised frame, sheets hanging at the sides
    n = 4 if T.lod == 0 else 1
    E = 1.45
    for i in range(n):
        x0, x1 = 2 * i / n, 2 * (i + 1) / n
        for (z0, z1) in ((-E, 0), (0, E)):
            ya, yb = (.1, .45) if z0 < 0 else (.45, .1)
            p = [(x0, ya, z0), (x1, ya, z0), (x1, yb, z1), (x0, yb, z1)]
            g.face(p, F['pvc'], (0, 1, -.24 if z0 < 0 else .24))
            g.face(p[::-1], F['pvc'], (0, -1, .24 if z0 < 0 else -.24))
    for z in (-E, E):
        rod(g, (0, .1, z), (2, .1, z), .025, F['galv'], T.small)
        # the drop sheet to 3.1 m over the street (1.3 m below the cable at the planner's 4.9 m)
        wall_side = Geo()
        k = 6 if T.lod == 0 else 2
        for i in range(k):
            xa, xb = 2 * i / k, 2 * (i + 1) / k
            za, zb = z - math.copysign(.04 * (.5 + .5 * math.sin(i * 2.1)), z), z - math.copysign(.04 * (.5 + .5 * math.sin((i + 1) * 2.1)), z)
            q = [(xa, .1, za), (xb, .1, zb), (xb, -1.25, zb), (xa, -1.25, za)]
            wall_side.face(q, F['sheet'], (0, 0, 1 if z > 0 else -1))
            wall_side.face(q[::-1], F['sheet'], (0, 0, -1 if z > 0 else 1))
        g.extend(wall_side)
    rod(g, (0, .45, 0), (2, .45, 0), .025, F['galv'], T.small)
    for x in (0.05, 1.95):
        rod(g, (x, .1, -E), (x, .45, 0), .02, F['galv'], T.small)
        rod(g, (x, .45, 0), (x, .1, E), .02, F['galv'], T.small)


def span_pipes(g, T):
    # a pipe bridge on its frames: three pipes and a catwalk with its rail (above the street, never a fake hazard)
    for (y, z, r, f) in ((.35, -.55, .22, F['rust']), (.3, .1, .16, F['iron-raw']), (.25, .6, .12, F['galv'])):
        rod(g, (0, y, z), (2, y, z), r, f, T.round, caps=False)
    bx(g, T, 0, 2, -.62, -.56, -1.0, 1.0, F['steel-dark'], bevel=0)
    if T.detail:
        for k in range(10):
            x = .1 + .2 * k
            bx(g, T, x - .01, x + .01, -.56, -.54, -1.0, 1.0, F['iron-black'], bevel=0)
    for z in (-1.0, 1.0):
        bx(g, T, 0, 2, -.56, -.5, z - .03, z + .03, F['crane-yellow'], bevel=0)
        rod(g, (0, 0, z), (2, 0, z), .02, F['crane-yellow'], 4)
        for x in (.2, 1.0, 1.8):
            rod(g, (x, -.56, z), (x, 0, z), .018, F['crane-yellow'], 4, caps=False)
    for x in (0.05, 1.95):
        bx(g, T, x - .04, x + .04, -.85, -.55, -1.05, 1.05, F['iron-black'], bevel=0)


def span_girder(g, T):
    bx(g, T, 0, 2, -.95, -.85, -.45, .45, F['crane-yellow'], bevel=0)
    bx(g, T, 0, 2, .15, .25, -.45, .45, F['crane-yellow'], bevel=0)
    for z in (-.42, .42):
        bx(g, T, 0, 2, -.85, .15, z - .02, z + .02, F['crane-yellow'], bevel=0)
    if T.detail:
        for k in range(5):
            x = .2 + .4 * k
            for z in (-.45, .45):
                bx(g, T, x - .02, x + .02, -.85, .15, z - .005 if z > 0 else z - .03, z + .03 if z > 0 else z + .005, F['hazard-black'], bevel=0)


def hung_troffer(g, T):
    for x in (-.5, .5):
        rod(g, (x, 0, 0), (x, -.26, 0), .006, F['cable'], 3, caps=False)
    bx(g, T, -.65, .65, -.34, -.26, -.18, .18, F['alu'], bevel=.008)
    g.face(((-.6, -.345, -.14), (.6, -.345, -.14), (.6, -.345, .14), (-.6, -.345, .14)), glow(LAMP, 2.2), (0, -1, 0))


def hung_dropper(g, T):
    rod(g, (0, 0, 0), (0, -.42, 0), .008, F['cable'], 4, caps=False)
    if T.detail:
        for k in range(3):
            sk.lathe(g, [(0.0, -.1 - k * .08), (.05, -.09 - k * .08), (.05, -.07 - k * .08), (0.0, -.06 - k * .08)], (0, 0, 0), F['insulator'], 8)
    rod(g, (0, -.46, -.6), (0, -.46, .6), .01, F['rail'], 4)
    bx(g, T, -.03, .03, -.48, -.42, -.05, .05, F['galv'], bevel=0)


def hung_hook(g, T):
    bx(g, T, -.55, .55, -.5, 0, -.5, .5, F['crane-yellow'], bevel=.02)
    for x in (-.3, .3):
        rod(g, (x, -.5, 0), (x, -1.7, 0), .015, F['cable'], 4, caps=False)
    bx(g, T, -.35, .35, -1.95, -1.7, -.22, .22, F['crane-yellow'], bevel=.02)
    sub = Geo()
    pts = [(0, -1.95, 0), (0, -2.05, 0), (0, -2.12, .08), (0, -2.12, .18), (0, -2.05, .22)]
    for a, b in zip(pts, pts[1:]):
        rod(sub, a, b, .035, F['iron-black'], T.small)
    g.extend(sub)


# Level 0's office ceiling: a 2 m cell of a T-bar grid (0.5 m tiles), hung on wires into the dark
def ceiling(g, T, kind):
    tiles = []
    for i in range(4):
        for j in range(4):
            tiles.append((i, j))
    light = kind == 'light'
    for (i, j) in tiles:
        x0, z0 = -1 + i * .5, -1 + j * .5
        k = h01('ct', kind, i, j)
        if light and j in (1, 2) and i in (1, 2):
            g.face(((x0 + .02, .015, z0 + .02), (x0 + .48, .015, z0 + .02), (x0 + .48, .015, z0 + .48), (x0 + .02, .015, z0 + .48)), glow(LAMP, 2.2), (0, -1, 0))
            continue
        gone = kind == 'gap' and k < .7 or kind == 'tile' and k < .12
        if gone:
            continue
        f = F['ceiling-stain'] if h01('cs', kind, i, j) < .2 else F['ceiling-tile']
        tilt = kind == 'gap' and k > .93
        y = .02
        if tilt:
            # a tile hanging off its grid by one edge
            g.face(((x0 + .02, y, z0 + .02), (x0 + .48, y, z0 + .02), (x0 + .48, y - .3, z0 + .45), (x0 + .02, y - .3, z0 + .45)), f, (0, -.83, -.55))
            g.face(((x0 + .02, y + .01, z0 + .02), (x0 + .02, y - .29, z0 + .45), (x0 + .48, y - .29, z0 + .45), (x0 + .48, y + .01, z0 + .02)), f, (0, .83, .55))
            continue
        g.face(((x0 + .01, y, z0 + .01), (x0 + .49, y, z0 + .01), (x0 + .49, y, z0 + .49), (x0 + .01, y, z0 + .49)), f, (0, -1, 0))
        g.face(((x0 + .01, y + .015, z0 + .01), (x0 + .01, y + .015, z0 + .49), (x0 + .49, y + .015, z0 + .49), (x0 + .49, y + .015, z0 + .01)), f, (0, 1, 0))
    # the T-bars: the cell's lines at -1, -0.5, 0, 0.5 in x and z (its +1 edges are the next cell's -1)
    for k in range(4):
        c = -1 + k * .5
        a, b = (c, c + .02) if k == 0 else (c - .012, c + .012)
        bx(g, T, a, b, 0, .04, -1, 1, F['tbar'], bevel=0)
        bx(g, T, -1, 1, 0, .04, a, b, F['tbar'], bevel=0)
    for (x, z) in ((-.5, -.5), (.5, .5)):
        rod(g, (x, .04, z), (x, 1.78, z), .004, F['cable'], 3, caps=False)


# ---------------------------------------------------------------------------------------------------------------------------
# The table: each kind's builder, and whether it is authored in the city kit's facade frame (turned half round)
# ---------------------------------------------------------------------------------------------------------------------------
BUILD = {
    'level0.carpet': floor_carpet, 'poolrooms.tile': floor_pool_tile, 'poolrooms.flood': floor_flood, 'poolrooms.drain': floor_drain,
    'poolrooms.ring': floor_ring, 'wardzero.litter': floor_litter, 'wardzero.decon': floor_decon, 'station.rails': floor_rails,
    'station.platform': floor_platform, 'ironworks.ash': floor_ash, 'ironworks.slag': floor_slag, 'ironworks.plates': floor_plates,
    'level0.wallpaper': lambda g, T: wall_wallpaper(g, T), 'level0.threshold': lambda g, T: wall_wallpaper(g, T, True),
    'poolrooms.threshold': lambda g, T: wall_pool(g, T), 'wardzero.growth': wall_growth, 'wardzero.cladding': lambda g, T: wall_cladding(g, T),
    'station.brick': wall_bluebrick, 'station.threshold': lambda g, T: wall_station(g, T), 'ironworks.plate': wall_plate,
    'ironworks.furnace': lambda g, T: wall_furnace(g, T),
    'level0.threshold.upper': upper_wallpaper, 'poolrooms.threshold.upper': lambda g, T: wall_pool(g, T, True),
    'wardzero.cladding.upper': lambda g, T: wall_cladding(g, T, True), 'wardzero.sheet': upper_sheet,
    'station.threshold.upper': lambda g, T: wall_station(g, T, True), 'ironworks.furnace.upper': lambda g, T: wall_furnace(g, T, True),
    'level0.exit': detail_exit, 'level0.missing': detail_missing, 'poolrooms.plate': detail_pool_plate, 'poolrooms.glassblock': detail_glassblock,
    'poolrooms.doorway': detail_doorway, 'wardzero.poster': detail_poster, 'wardzero.evac.l': lambda g, T: detail_evac(g, T, False),
    'wardzero.evac.r': lambda g, T: detail_evac(g, T, True), 'station.roundel': detail_roundel, 'ironworks.rollerdoor': detail_rollerdoor,
    'ironworks.sign': detail_iron_sign,
    'common.lamp': fit_common_lamp, 'level0.lamp': fit_level0_lamp, 'poolrooms.poollamp': fit_pool_lamp, 'wardzero.lamp': fit_ward_lamp,
    'station.lamp': fit_signal, 'ironworks.worklamp': fit_worklamp, 'ironworks.highbay': fit_highbay, 'wardzero.horn': fit_horn,
    'station.clock': fit_clock, 'station.board': fit_board, 'common.anchor': fit_anchor,
    'common.wire': span_wire, 'poolrooms.lanerope': span_lanerope, 'poolrooms.pennants': span_pennants, 'wardzero.tent': span_tent,
    'ironworks.pipes': span_pipes, 'ironworks.girder': span_girder,
    'level0.troffer': hung_troffer, 'station.dropper': hung_dropper, 'ironworks.hook': hung_hook,
    'level0.ceiling.tile': lambda g, T: ceiling(g, T, 'tile'), 'level0.ceiling.gap': lambda g, T: ceiling(g, T, 'gap'),
    'level0.ceiling.light': lambda g, T: ceiling(g, T, 'light'),
}
TURN = [-1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, -1.0, 0]  # half round about +y: the kit's facade frame to the kind's Wall frame
# pieces worth a Cycles occlusion bake (relief); the flat paint takes none
AO = ('lamp', 'troffer', 'tent', 'pipes', 'girder', 'hook', 'ceiling', 'clock', 'board', 'horn', 'sheet', 'upper', 'highbay', 'rollerdoor')


def build(key, mount, T):
    g = Geo()
    BUILD[key](g, T)
    if mount == 'Wall':
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
        raise SystemExit('%s: outside its envelope on %s: %s..%s against %s..%s' % (key, 'xyz'[bad[0]], a.round(3), b.round(3), lo, hi))


# ---------------------------------------------------------------------------------------------------------------------------
# Themed module variants for the street planner (Core NativeMeridianDress.ThemeAt: a module key plus "@" and the theme). The
# drift band's skins (R3/R12): Level 0's beige stucco offices with ribbon windows lit flat yellow, the Poolrooms' white tile
# and glass block, Ward Zero's white cladding and teal band, the Station's cream and green tile, the Ironworks' buff brick and
# roller doors. In the planner's module frame (the bay's foot and centre on the face, x along it, y up, z out); the ground
# and the hugging upper bays keep every vertex between 0.25 m in and 6 cm out of the face, so they may stand in the actor band.
# Four shapes per theme (a ground bay, a door bay, an upper bay, a roof-line band), each shared by every key of its family:
# the keys are the street planner's (claude/meridian-dress at b56b3e2b: refresh VARIANT_KEYS when its families change).
# ---------------------------------------------------------------------------------------------------------------------------
THEMES = ('level0', 'poolrooms', 'wardzero', 'station', 'ironworks')
TRADES = ('BAKERY', 'GROCER', 'NEWS', 'CHEMIST', 'COBBLER', 'CAFE', 'BUTCHER', 'DAIRY', 'LAUNDRY', 'TAILOR')
_UP = ['u.win.%d.%d' % (v, s) for v in (0, 1) for s in range(3)] + ['u.ten.%d' % s for s in range(3)] + ['u.stair'] + \
    ['u.ware.%d' % s for s in range(2)] + ['u.civic.%d' % s for s in range(2)] + ['u.plain'] + ['u.blind.%d' % s for s in range(2)]
VARIANT_KEYS = {
    'ground': ['g.window.%d.%d' % (v, s) for v in (0, 1) for s in range(3)] + ['g.shop.' + t for t in TRADES] + ['g.shutter', 'g.flats.0',
               'g.flats.1', 'g.ware', 'g.civic.0', 'g.civic.1'] + ['g.blind.%d' % k for k in range(3)],
    'door': ['g.door.%d.%d' % (v, s) for v in (0, 1) for s in range(3)] + ['g.entry', 'g.load'],
    'upper': _UP + [k + '.h' for k in _UP] + ['u.oriel'],
    'top': ['t.brick', 't.render', 't.ten', 't.ware', 't.civic'],
}
# the shapes' envelopes in the module frame (x, y, z): ground and upper bays hug their face, the band hangs from the roof line
VARIANT_BOX = {'ground': ((-1.25, 0, -.25), (1.25, 4.0, .06)), 'door': ((-1.25, 0, -.25), (1.25, 4.0, .06)),
               'upper': ((-1.25, 0, -.25), (1.25, 3.0, .06)), 'top': ((-1.25, -.95, -.25), (1.25, 0, .35))}


def facade(g, T, x0, x1, y0, y1, f, holes=(), z=0.0):
    """A flat wall face (kit frame, facing -z) from x0..x1, y0..y1 with rectangular holes (x0, x1, y0, y1) cut out."""
    ys = sorted({y0, y1} | {h[2] for h in holes} | {h[3] for h in holes})
    ys = [y for y in ys if y0 <= y <= y1]
    for ya, yb in zip(ys, ys[1:]):
        if yb - ya < 1e-4:
            continue
        cuts = sorted((h[0], h[1]) for h in holes if h[2] <= ya + 1e-4 and h[3] >= yb - 1e-4)
        x = x0
        for (a, b) in cuts + [(x1, x1)]:
            if a - x > 1e-4:
                wall_rect(g, T, x, a, ya, yb, z, f, -1)
            x = max(x, b)


def recess(g, T, h, depth, f_reveal, f_back, glow_back=None):
    """A rectangular hole (x0, x1, y0, y1) recessed `depth` into the wall: its four reveals and its back pane."""
    x0, x1, y0, y1 = h
    for (a, b, n) in (((x0, y0), (x1, y0), (0, 1, 0)), ((x1, y1), (x0, y1), (0, -1, 0))):
        g.face(((a[0], a[1], 0), (b[0], b[1], 0), (b[0], b[1], depth), (a[0], a[1], depth)), f_reveal, n)
    for (x, n) in ((x0, (1, 0, 0)), (x1, (-1, 0, 0))):
        g.face(((x, y0, 0), (x, y1, 0), (x, y1, depth), (x, y0, depth)), f_reveal, n)
    wall_rect(g, T, x0, x1, y0, y1, depth, glow_back if glow_back is not None else f_back, -1, 0)


def mullions(g, T, h, n, f, depth=.03, w=.04):
    x0, x1, y0, y1 = h
    for k in range(1, n):
        x = x0 + (x1 - x0) * k / n
        bx(g, T, x - w / 2, x + w / 2, y0, y1, -.0 + .0, depth, f, bevel=0)


def variant(theme, shape, g, T):
    if theme == 'level0':
        st, al = F['stucco-beige'], F['alu']
        room = glow(0xE8D070, 1.1)
        if shape == 'ground':
            h = (-1.2, 1.2, 1.0, 2.5)
            facade(g, T, -1.25, 1.25, 0, 4.0, st, [h])
            recess(g, T, h, .14, al, F['glass-brown'], room)
            mullions(g, T, h, 4, al, depth=.1)
            panel(g, T, -1.25, 1.25, 0, .3, .03, F['stucco-joint'], cell=0)
        elif shape == 'door':
            d, w = (-.5, .5, 0, 2.3), (-1.2, 1.2, 2.7, 3.6)
            facade(g, T, -1.25, 1.25, 0, 4.0, st, [d, w])
            recess(g, T, d, .1, al, F['veneer'])
            recess(g, T, w, .14, al, F['glass-brown'], room)
            mullions(g, T, w, 4, al, depth=.1)
        elif shape == 'upper':
            h = (-1.2, 1.2, .7, 2.3)
            facade(g, T, -1.25, 1.25, 0, 3.0, st, [h])
            recess(g, T, h, .14, al, F['glass-brown'], room)
            mullions(g, T, h, 4, al, depth=.1)
            panel(g, T, -1.25, 1.25, 2.9, 3.0, .02, F['stucco-joint'], cell=0)
        else:
            facade(g, T, -1.25, 1.25, -.95, 0, st)
            bx(g, T, -1.25, 1.25, -.12, 0, -.3, .0, al, bevel=0)
    elif theme == 'poolrooms':
        tile, aqua = F['tile-pool'], F['tile-aqua']
        gb = glow(0x60D8F0, .9)
        if shape in ('ground', 'upper'):
            top = 4.0 if shape == 'ground' else 3.0
            h = (-.8, .8, 1.0, 2.6) if shape == 'ground' else (-.8, .8, .7, 2.1)
            facade(g, T, -1.25, 1.25, 0, top, tile, [h])
            recess(g, T, h, .06, tile, gb)
            if T.lod == 0:
                n = 6
                for k in range(1, n):
                    x = h[0] + (h[1] - h[0]) * k / n
                    bx(g, T, x - .012, x + .012, h[2], h[3], .0, .055, tile, bevel=0)
                    y = h[2] + (h[3] - h[2]) * k / n
                    bx(g, T, h[0], h[1], y - .012, y + .012, .0, .055, tile, bevel=0)
            band = (3.2, 3.45) if shape == 'ground' else (2.55, 2.8)
            panel(g, T, -1.25, 1.25, band[0], band[1], .04, aqua, cell=0)
            if shape == 'ground':
                panel(g, T, -1.25, 1.25, 0, .15, .04, F['tile-teal'], cell=0)
        elif shape == 'door':
            d = (-.6, .6, 0, 2.5)
            facade(g, T, -1.25, 1.25, 0, 4.0, tile, [d])
            recess(g, T, d, .22, aqua, F['void'])
            panel(g, T, -1.25, 1.25, 3.2, 3.45, .04, aqua, cell=0)
        else:
            facade(g, T, -1.25, 1.25, -.95, 0, tile)
            bx(g, T, -1.25, 1.25, -.5, -.3, -.12, 0, aqua, bevel=0)
            bx(g, T, -1.25, 1.25, -.08, 0, -.2, 0, F['coping'], bevel=0)
    elif theme == 'wardzero':
        clad, teal, joint = F['cladding-white'], F['cladding-teal'], F['cladding-joint']
        if shape in ('ground', 'upper', 'door'):
            top = 3.0 if shape == 'upper' else 4.0
            holes = []
            if shape == 'ground':
                holes = [(-1.0, 1.0, 1.2, 1.9)]
            elif shape == 'upper':
                holes = [(-1.0, 1.0, 1.0, 1.7)]
            else:
                holes = [(-.55, .55, 0, 2.3)]
            facade(g, T, -1.25, 1.25, 0, top, joint, holes, z=.0)
            # the panels, a hair proud of their joints
            for hh in holes:
                recess(g, T, hh, .12, F['steel-white'], F['glass-dark'] if shape != 'door' else F['steel-white'])
            band = (2.6, 2.9) if shape != 'upper' else (2.6, 2.85)
            panel(g, T, -1.25, 1.25, band[0], band[1], .045, teal, cell=0)
            for x in (-1.25, 0.0):
                for (y0, y1) in ((0.02, band[0] - .02), (band[1] + .02, top - .02)):
                    segs = [(y0, y1)]
                    for hh in holes:
                        if x + 1.25 > hh[0] and x < hh[1]:
                            segs = [(a, b) for (a, b) in segs for (a, b) in ((a, min(b, hh[2])), (max(a, hh[3]), b)) if b - a > .05]
                    for (a, b) in segs:
                        xs = [(x + .01, x + 1.24)]
                        for hh in holes:
                            if a < hh[3] and b > hh[2]:
                                xs = [(c, d) for (c, d) in xs for (c, d) in ((c, min(d, hh[0])), (max(c, hh[1]), d)) if d - c > .05]
                        for (c, d) in xs:
                            panel(g, T, c, d, a, b, .025, clad, cell=1.0 if T.lod == 0 else 0)
        else:
            facade(g, T, -1.25, 1.25, -.95, 0, clad)
            bx(g, T, -1.25, 1.25, -.15, 0, -.2, 0, F['steel-white'], bevel=0)
            bx(g, T, -1.25, 1.25, -.7, -.55, -.05, 0, teal, bevel=0)
    elif theme == 'station':
        cream, green = F['tile-cream'], F['tile-green']
        if shape in ('ground', 'door', 'upper'):
            top = 3.0 if shape == 'upper' else 4.0
            base = 0.0 if shape == 'upper' else 1.2
            if shape == 'door':
                h = (-.6, .6, 0, 2.6)
            elif shape == 'ground':
                h = (-.85, .85, 1.3, 3.0)
            else:
                h = (-.7, .7, .8, 2.3)
            facade(g, T, -1.25, 1.25, 0, top, F['brick-glazed'] if shape == 'upper' else cream, [h])
            if shape != 'upper':
                lo = [(-1.25, h[0]), (h[1], 1.25)] if h[2] < 1.2 else [(-1.25, 1.25)]
                for (a, b) in lo:
                    panel(g, T, a, b, 0, min(1.2, h[2]) if h[2] >= 1.2 else 1.2, .03, green, cell=0)
            recess(g, T, h, .15, cream, F['glass-dark'] if shape != 'door' else F['void'])
            # the arch's head: a cream tile ring over the opening, flat
            n = 8 if T.lod == 0 else 4
            r0, r1 = (h[1] - h[0]) / 2, (h[1] - h[0]) / 2 + .14
            cx, cy = (h[0] + h[1]) / 2, h[3]
            for k in range(n):
                a0, a1 = PI * k / n, PI * (k + 1) / n
                p = [(cx + math.cos(a0) * r1, cy + math.sin(a0) * r1 * .5, -.03), (cx + math.cos(a1) * r1, cy + math.sin(a1) * r1 * .5, -.03),
                     (cx + math.cos(a1) * r0, cy + math.sin(a1) * r0 * .5, -.03), (cx + math.cos(a0) * r0, cy + math.sin(a0) * r0 * .5, -.03)]
                if max(q[1] for q in p) <= top:
                    g.face(p, green, (0, 0, -1))
        else:
            facade(g, T, -1.25, 1.25, -.95, 0, F['brick-glazed'])
            bx(g, T, -1.25, 1.25, -.4, -.25, -.1, 0, green, bevel=0)
            bx(g, T, -1.25, 1.25, -.06, 0, -.16, 0, cream, bevel=0)
    else:
        brick, iron = F['brick-buff'], F['iron-black']
        if shape == 'ground':
            h = (-1.05, 1.05, 0, 3.2)
            facade(g, T, -1.25, 1.25, 0, 4.0, brick, [h])
            recess(g, T, h, .12, iron, F['galv'])
            if T.lod == 0:
                for k in range(16):
                    y = .1 + k * .19
                    bx(g, T, h[0], h[1], y, y + .03, .1, .12, F['steel-dark'], bevel=0)
            panel(g, T, -1.25, 1.25, 3.25, 3.5, .04, F['brick-soot'], cell=0)
        elif shape == 'door':
            d = (-.5, .5, 0, 2.2)
            facade(g, T, -1.25, 1.25, 0, 4.0, brick, [d])
            recess(g, T, d, .1, iron, F['steel-dark'])
            panel(g, T, -.65, .65, 2.2, 2.4, .04, iron, cell=0)
        elif shape == 'upper':
            h = (-.8, .8, .6, 2.4)
            facade(g, T, -1.25, 1.25, 0, 3.0, brick, [h])
            recess(g, T, h, .12, iron, F['glass-dark'])
            if T.lod == 0:
                for k in range(1, 4):
                    x = h[0] + (h[1] - h[0]) * k / 4
                    bx(g, T, x - .015, x + .015, h[2], h[3], .08, .11, iron, bevel=0)
                for k in range(1, 5):
                    y = h[2] + (h[3] - h[2]) * k / 5
                    bx(g, T, h[0], h[1], y - .015, y + .015, .08, .11, iron, bevel=0)
            panel(g, T, -1.25, 1.25, 2.5, 3.0, .03, F['brick-soot'], cell=0)
        else:
            facade(g, T, -1.25, 1.25, -.95, 0, brick)
            bx(g, T, -1.25, 1.25, -.1, 0, -.18, 0, iron, bevel=0)


def build_variants(sb, args):
    """Every theme's four shapes at both tiers, each checked against its envelope, and a prop per planner key sharing them."""
    count = 0
    for theme in THEMES:
        for shape in ('ground', 'door', 'upper', 'top'):
            for lod, ti in ((0, 0), (1, 2)):
                T = tier(lod)
                g = Geo()
                variant(theme, shape, g, T)
                g = g.transformed(TURN)
                lo, hi = VARIANT_BOX[shape]
                P, _, _, _ = g.arrays()
                a, b = P.min(axis=0), P.max(axis=0)
                if any(a[i] < lo[i] - 2e-3 or b[i] > hi[i] + 2e-3 for i in range(3)):
                    raise SystemExit('%s@%s: outside its envelope: %s..%s' % (shape, theme, a.round(3), b.round(3)))
                bk = sk.Bake(g, None, floors=(0.0,) if shape in ('ground', 'door') else (-50.0,), seed=int(h01(theme, shape) * 1000))
                om = sk.to_mesh('variant/%s.%s/%d' % (theme, shape, ti), g, bk)
                mi = sb.mesh(om)
                for key in VARIANT_KEYS[shape]:
                    sb.share('module/%s@%s/%d' % (key, theme, ti), 'module', mi)
                    if ti == 0:
                        sb.share('module/%s@%s/1' % (key, theme), 'module', mi)
                    count += 1
    print('variants: %d themed keys over %d themes' % (count // 2, len(THEMES)))


def variant_sheet(out):
    """Each theme's skins as a little facade (three bays: a ground bay, a door bay, a ground bay; two storeys; the band),
    side by side, facing the camera."""
    T = tier(0)
    laid = []
    for n, theme in enumerate(THEMES):
        g = Geo()
        for bay, shape in enumerate(('ground', 'door', 'ground')):
            x = n * 9.0 + (bay - 1) * 2.5
            for (sh, y) in ((shape, 0.0), ('upper', 4.0), ('upper', 7.0), ('top', 10.0)):
                sub = Geo()
                variant(theme, sh, sub, T)
                g.extend(sub.transformed([1.0, 0, 0, x, 0, 1.0, 0, y, 0, 0, 1.0, 0]))
        laid.append((g, sk.Bake(g, None, floors=(0.0,), seed=n)))
    views = [('variants-%s' % t, (n * 9.0, 5.5, -13.0), (n * 9.0, 5.0, 0.0), 50) for n, t in enumerate(THEMES)]
    views.append(('variants-all', (18.0, 6.0, -40.0), (18.0, 5.0, 0.0), 50))
    sk.preview(laid, views, out, size=(900, 900))


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
    for lod, ti in ((0, 0), (1, 2)):
        T = tier(lod)
        geos = []
        for key in keys:
            g = build(key, K[key][0], T)
            check(key, K[key], g)
            geos.append(g)
        aos = [None] * len(geos)
        if not args.no_ao:
            idx = [i for i, k in enumerate(keys) if any(s in k for s in AO)]
            if idx:
                # each piece baked by itself: set 30 m apart in one scene
                moved = [geos[i].transformed([1, 0, 0, 30.0 * n, 0, 1, 0, 0, 0, 0, 1, 0]) for n, i in enumerate(idx)]
                baked = sk.bake_ao(moved, distance=.8, samples=24 if lod == 0 else 10)
                for i, a in zip(idx, baked):
                    aos[i] = a
        pv = []
        for key, g, ao in zip(keys, geos, aos):
            mount = K[key][0]
            floors = (0.0,) if mount == 'Wall' and K[key][1][1] >= 0 and K[key][2][1] <= 3.2 else (-50.0,)
            bk = sk.Bake(g, ao, floors=floors, seed=int(h01(key) * 1000))
            name = 'rig/bleed/%s/%d' % (key, ti)
            om = sk.to_mesh(name, g, bk)
            sb.add(name, 'rig', om)
            if lod == 0:
                sb.share('rig/bleed/%s/1' % key, 'rig', len(sb.bank.meshes) - 1)
            pv.append((g, bk))
        print('tier %d: %d pieces, %d faces, %.1f s' % (ti, len(keys), sum(g.faces for g in geos), time.time() - t0))
        if args.preview and lod == 0:
            sheet(pv, keys, K, args.preview)
            if not args.only:
                variant_sheet(args.preview)
    if not args.only:
        build_variants(sb, args)
    if args.out:
        sb.write(args.out, BANK)
    print('done in %.1f s' % (time.time() - t0))


def sheet(pv, keys, K, out):
    """Every piece laid out on a grid (walls stood up on a backing plane), rendered from the front and from above."""
    laid = []
    cols = 8
    for n, ((g, bk), key) in enumerate(zip(pv, keys)):
        cx, cz = (n % cols) * 4.0, -(n // cols) * 5.0
        mount = K[key][0]
        if mount == 'Wall':
            m = [-1.0, 0, 0, cx, 0, 1.0, 0, 0, 0, 0, -1.0, cz]  # face the camera at -z
        elif mount == 'Span':
            m = [1.0, 0, 0, cx - 1, 0, 1.0, 0, 3.0, 0, 0, 1.0, cz]
        elif mount == 'Overhead':
            m = [1.0, 0, 0, cx, 0, 1.0, 0, 3.0 if 'ceiling' not in key else 0, 0, 0, 1.0, cz]
        else:
            m = [1.0, 0, 0, cx, 0, 1.0, 0, 0, 0, 0, 1.0, cz]
        laid.append((g.transformed(m), bk))
    rows = (len(keys) + cols - 1) // cols
    views = [('front-%d%s' % (r, h), (cx, 2.6, -(r * 5.0) - 11.0), (cx, 1.5, -(r * 5.0)), 52) for r in range(rows) for (h, cx) in (('a', 6.0), ('b', 22.0))]
    views.append(('above', (14.0, 40.0, -16.0), (14.0, 0.0, -15.0), 60))
    sk.preview(laid, views, out, size=(1400, 700))


if __name__ == '__main__':
    main()
