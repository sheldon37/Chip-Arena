"""The Factory's art (docs/arenas/FACTORY-ART.md, docs/arenas/STAGE-ART.md): Corvane Works' Line 3 modelled in
Blender on the map's own blocks and its Core kit's parts, into the stage bank Resources/ChipArenaStageFactory (stagekit.py's
format and bake). Runtime NativeStageArt draws it in place of the boxes; without the bank the box kit draws as before.

usage (from unity/ChipArenaConversion):
  dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout factory LAYOUT.json
  blender --background --factory-startup --python tools/stages/factory_art.py -- --layout LAYOUT.json --out ASSETS/LocalAssets
          [--preview DIR [--views a,b]] [--tiers 0,1,2] [--no-ao] [--static-only] [--stats]

This file: the building (the slab with the slitters' trenches, the shell's plinth, corrugated cladding and clerestory, the bay
walls, the runway columns as H-sections and the runway plate girders, the hall roofs' sheets, trusses and skylight lantern
frames, the turbine vents, the high-bay lamps and dock floods, the grating decks, stairs and pulpits, the presses' heads and
every hazard's fixed fittings). factory_plant.py: the machines, the stock and the docks. factory_rig.py: the rigs (blades,
rail-saw carriages, rams, the crane) and the breakable blocks. Everything else the kit carries is rebuilt from its own parts
(factory_kit.kit_part): rails and pipes round, flanges turned, stripes painted, decals flat, lamps glowing.

The skylight lanterns' glazing is not in the bank: those panes stay the kit's lit boxes (they cast no sun shadow, so the sun still
falls through the slots in stripes); the bank models their frames.
"""
import argparse
import math
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
import factory_kit as fk  # noqa: E402
from stagekit import Geo, box, lathe  # noqa: E402
from factory_kit import mat, tint, lamp, ext, bx, cyl, tube, ibeam, bar, kit_part, kname, body, stripes, striped_box, corrugated, corrugated_top, Y  # noqa: E402

BANK = 'ChipArenaStageFactory'
BL = {}


def rect_grid(g, x0, x1, z0, z1, y, f, cell):
    if x1 - x0 < 1e-6 or z1 - z0 < 1e-6:
        return
    sk.grid_face(g, (x0, y, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, f, Y, cell)


# ---------------------------------------------------------------------------------------------------------------------------
# The slab: polished concrete, the slitters' trenches cut through it
# ---------------------------------------------------------------------------------------------------------------------------
def slots(L):
    out = []
    for h in L['hazards']:
        if h['kind'] == 'saw-slot':
            r = h['radius'] + .2
            out.append((min(h['a'][0], h['b'][0]) - r, max(h['a'][0], h['b'][0]) + r, h['a'][2]))
    return out


def floor(g, L, T, parts):
    f = mat('floor')
    cell = (2.0, 6.0, 12.0)[T.i]
    X0, X1, Z0, Z1 = -38.0, 38.0, -30.0, 30.0
    sl = sorted(slots(L), key=lambda s: s[2])
    hw = .12
    z = Z0
    pit = mat('pit')
    for (s0, s1, c) in sl:
        rect_grid(g, X0, X1, z, c - hw, 0.0, f, cell)
        rect_grid(g, X0, s0, c - hw, c + hw, 0.0, f, cell)
        rect_grid(g, s1, X1, c - hw, c + hw, 0.0, f, cell)
        # the trench: its walls and the dark housing under the blade
        d = -.7
        g.face(((s0, 0, c - hw), (s1, 0, c - hw), (s1, d, c - hw), (s0, d, c - hw)), pit, normal=(0, 0, 1))
        g.face(((s0, 0, c + hw), (s1, 0, c + hw), (s1, d, c + hw), (s0, d, c + hw)), pit, normal=(0, 0, -1))
        g.face(((s0, 0, c - hw), (s0, 0, c + hw), (s0, d, c + hw), (s0, d, c - hw)), pit, normal=(1, 0, 0))
        g.face(((s1, 0, c - hw), (s1, 0, c + hw), (s1, d, c + hw), (s1, d, c - hw)), pit, normal=(-1, 0, 0))
        g.face(((s0, d, c - hw), (s1, d, c - hw), (s1, d, c + hw), (s0, d, c + hw)), pit, normal=Y)
        z = c + hw
    rect_grid(g, X0, X1, z, Z1, 0.0, f, cell)
    for p in parts:
        if kname(p) == 'grate' and T.i < 2:
            drain(g, p, T)
        else:
            kit_part(g, p, T)


def drain(g, p, T):
    """A floor drain: a cast frame and its bars."""
    c, h = p['c'], p['h']
    x0, x1, z0, z1, y = c[0] - h[0], c[0] + h[0], c[2] - h[2], c[2] + h[2], c[1] + h[1]
    fr, dk = mat('iron'), mat('pit')
    g.face(((x0 + .04, y - .004, z0 + .04), (x1 - .04, y - .004, z0 + .04), (x1 - .04, y - .004, z1 - .04), (x0 + .04, y - .004, z1 - .04)), dk, normal=Y)
    for (a0, a1, b0, b1) in ((x0, x1, z0, z0 + .04), (x0, x1, z1 - .04, z1), (x0, x0 + .04, z0 + .04, z1 - .04), (x1 - .04, x1, z0 + .04, z1 - .04)):
        g.face(((a0, y, b0), (a1, y, b0), (a1, y, b1), (a0, y, b1)), fr, normal=Y)
    n = 7 if T.i == 0 else 4
    for k in range(n):
        xa = x0 + .04 + (x1 - x0 - .08) * (k + .25) / n
        xb = x0 + .04 + (x1 - x0 - .08) * (k + .75) / n
        g.face(((xa, y, z0 + .04), (xb, y, z0 + .04), (xb, y, z1 - .04), (xa, y, z1 - .04)), fr, normal=Y)


# ---------------------------------------------------------------------------------------------------------------------------
# The shell: a concrete plinth to 4.4 m, corrugated cladding to 14 m with the clerestory band, the mains
# ---------------------------------------------------------------------------------------------------------------------------
def wall(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    clad = b['id'].endswith('-clad')
    along_x = (x1 - x0) > (z1 - z0)
    if along_x:
        inner = z0 if z0 > 0 else z1
        s = -1 if z0 > 0 else 1
        u0, u1 = max(x0, -38.3), min(x1, 38.3)
        n3, n2 = (0, 0, s), (0, s)

        def P(u, y, d=0.0):
            return (u, y, inner + s * d)

        def plan(u, d=0.0):
            return (u, inner + s * d)
    else:
        inner = x0 if x0 > 0 else x1
        s = -1 if x0 > 0 else 1
        u0, u1 = max(z0, -30.3), min(z1, 30.3)
        n3, n2 = (s, 0, 0), (s, 0)

        def P(u, y, d=0.0):
            return (inner + s * d, y, u)

        def plan(u, d=0.0):
            return (inner + s * d, u)
    cell = (2.2, 6.0, 15.0)[T.i]
    if not clad:
        f = mat('plinth')
        # the plinth's face in board-marked lifts: three lifts with a shadow joint between them, a kerb at its foot
        lifts = [(0.0, .18, .05), (.18, 1.5, 0.0), (1.52, 2.95, 0.0), (2.97, y1 - .14, 0.0)] if T.i < 2 else [(0.0, y1, 0.0)]
        for (ya, yb, d) in lifts:
            sk.grid_face(g, P(u0, ya, d), sk._sub(P(u1, ya, d), P(u0, ya, d)), (0, yb - ya, 0), 0, 0, f, n3, cell)
        if T.i < 2:
            g.face((P(u0, .18, .05), P(u1, .18, .05), P(u1, .18, 0), P(u0, .18, 0)), f, normal=Y)
            for yj in (1.5, 2.95):
                g.face((P(u0, yj, 0), P(u1, yj, 0), P(u1, yj + .02, 0), P(u0, yj + .02, 0)), mat('black'), normal=n3)
        # the ledge the cladding stands back on
        sk.grid_face(g, P(u0, y1, 0), sk._sub(P(u1, y1, 0), P(u0, y1, 0)), sk._sub(P(u0, y1, -.3), P(u0, y1, 0)), 0, 0, f, Y, cell * 2)
        for p in parts:
            name = kname(p)
            if name == 'door':
                roller_door(g, p, T, n3)
            elif name == 'black' and p['h'][1] < .03 and not along_x:
                continue        # the door's slat lines: the door is modelled
            else:
                kit_part(g, p, T)
        return
    f = mat('clad')
    wins = sorted([p for p in parts if kname(p) == 'window'], key=lambda p: p['c'][0] if along_x else p['c'][2])
    lo, hi = 8.22, 10.18
    corrugated(g, plan(u0), plan(u1), y0, lo if wins else y1, n2, f, T)
    if wins:
        corrugated(g, plan(u0), plan(u1), hi, y1, n2, f, T)
        at = u0
        for p in wins:
            c, h = p['c'], p['h']
            wu, hu = (c[0], h[0]) if along_x else (c[2], h[2])
            if wu - hu - .08 > at + .05:
                corrugated(g, plan(at), plan(wu - hu - .08), lo, hi, n2, f, T)
            at = wu + hu + .08
            window(g, P, wu - hu, wu + hu, c[1] - h[1], c[1] + h[1], n3, T)
        if u1 > at + .05:
            corrugated(g, plan(at), plan(u1), lo, hi, n2, f, T)
    # the wall's top
    g.face((P(u0, y1, 0), P(u1, y1, 0), P(u1, y1, -1.7), P(u0, y1, -1.7)), mat('roof'), normal=Y)
    for p in parts:
        name = kname(p)
        if name == 'window':
            continue
        if name == 'frame' and p['h'][1] > .5:
            continue            # the mullions: the window models its own
        kit_part(g, p, T)


def window(g, P, u0, u1, ya, yb, n3, T):
    """A clerestory light: dusty daylight behind steel glazing bars, set back in its frame."""
    d = -.03
    g.face((P(u0, ya, d), P(u1, ya, d), P(u1, yb, d), P(u0, yb, d)), mat('daylight'), normal=n3)
    fr = mat('frame')
    # the reveal
    g.face((P(u0, ya, d), P(u1, ya, d), P(u1, ya, .06), P(u0, ya, .06)), fr, normal=Y)
    g.face((P(u0, yb, d), P(u1, yb, d), P(u1, yb, .06), P(u0, yb, .06)), fr, normal=(0, -1, 0))
    if T.i == 2:
        return
    n = max(1, int(round((u1 - u0) / 1.2)))
    rows = (ya + (yb - ya) / 3, ya + 2 * (yb - ya) / 3) if T.i == 0 else ()
    for k in range(n + 1):
        u = u0 + (u1 - u0) * k / n
        g.face((P(u - .03, ya, .012), P(u + .03, ya, .012), P(u + .03, yb, .012), P(u - .03, yb, .012)), fr, normal=n3)
    for y in rows:
        g.face((P(u0, y - .02, .012), P(u1, y - .02, .012), P(u1, y + .02, .012), P(u0, y + .02, .012)), fr, normal=n3)


def roller_door(g, p, T, n3):
    """A roller shutter: curved slats in their guides."""
    c, h = p['c'], p['h']
    x0, x1, ya, yb, z0, z1 = c[0] - h[0], c[0] + h[0], c[1] - h[1], c[1] + h[1], c[2] - h[2], c[2] + h[2]
    f = tint('clad', p['col'])
    face_x = x1 if n3[0] > 0 else x0
    n = int((yb - ya) / (.15 if T.i == 0 else .3 if T.i == 1 else 4.0)) + 1
    for k in range(n):
        y0, y1 = ya + (yb - ya) * k / n, ya + (yb - ya) * (k + 1) / n
        ym = (y0 + y1) / 2
        if T.i == 2:
            g.face(((face_x, y0, z0), (face_x, y0, z1), (face_x, y1, z1), (face_x, y1, z0)), f, normal=n3)
            continue
        out = face_x + n3[0] * .025
        g.face(((face_x, y0, z0), (face_x, y0, z1), (out, ym, z1), (out, ym, z0)), f, normals=((n3[0] * .7, -.7, 0),) * 2 + ((n3[0], 0, 0),) * 2)
        g.face(((out, ym, z0), (out, ym, z1), (face_x, y1, z1), (face_x, y1, z0)), f, normals=((n3[0], 0, 0),) * 2 + ((n3[0] * .7, .7, 0),) * 2)


# ---------------------------------------------------------------------------------------------------------------------------
# The bay walls, the columns, the runways
# ---------------------------------------------------------------------------------------------------------------------------
def baywall(g, b, parts, T):
    body(g, b, mat('baywall'), T, .04, cell=(1.8, 0, 0)[T.i])
    for p in parts:
        if kname(p) == 'stripe' and p['h'][0] < .02 and p['h'][1] > 1:
            # the guard on a jamb's end: a hair short of the wall's faces, so its edges do not lie in them
            p = dict(p, h=[p['h'][0], p['h'][1], p['h'][2] - .012])
        kit_part(g, p, T)


def column(g, b, parts, T):
    """A runway column: an H-section (its flanges across the hall), stiffened, on a bolted base plate."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    f = mat('steel')
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    if T.i == 2:
        bx(g, x0, x1, y0, y1, z0, z1, f, T, 0, 0, ('-y',))
    else:
        bx(g, x0, x0 + .05, y0, y1, z0, z1, f, T, .008 if T.i == 0 else 0, 0, ('-y',))
        bx(g, x1 - .05, x1, y0, y1, z0, z1, f, T, .008 if T.i == 0 else 0, 0, ('-y',))
        bx(g, x0 + .05, x1 - .05, y0, y1, cz - .025, cz + .025, f, T, 0, 0, ('-y', '-x', '+x'))
        if T.tiny:
            for y in (2.4, 4.8, 7.2, 9.6, 11.4):
                for (za, zb) in ((z0 + .03, cz - .025), (cz + .025, z1 - .03)):
                    bx(g, x0 + .05, x1 - .05, y - .012, y + .012, za, zb, f, T, 0, 0, ('-x', '+x'))
    for p in parts:
        name = kname(p)
        if (name in ('bracket', 'black')) and p['h'][1] > 3:
            continue            # the kit's flanges and its painted web: the section is modelled
        if name == 'bracket' and abs(p['q'][3]) < .999:
            # the haunch under the runway: a gusset plate
            c = p['c']
            zo = cz + (-.3 if cz > 0 else .3)
            zt = cz + (-.82 if cz > 0 else .82)
            g.face(((cx, y1 - 1.5, zo), (cx, y1 - .06, zo), (cx, y1 - .06, zt)), f, normal=(1, 0, 0))
            g.face(((cx, y1 - 1.5, zo), (cx, y1 - .06, zo), (cx, y1 - .06, zt)), f, normal=(-1, 0, 0))
            bar(g, (cx, y1 - 1.5, zo), (cx, y1 - .08, zt), .2, .03, f, T, up=(1, 0, 0))
            continue
        kit_part(g, p, T)


def runway(g, b, parts, T):
    """A crane runway: a plate girder with its stiffeners, the crane rail on top."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    f = mat('runway')
    cz = (z0 + z1) / 2
    if T.i == 2:
        bx(g, x0, x1, y0, y1, z0, z1, f, T, 0)
    else:
        cell = 3.0 if T.i == 0 else 0
        bx(g, x0, x1, y1 - .07, y1, z0, z1, f, T, .012, cell)
        bx(g, x0, x1, y0, y0 + .07, z0, z1, f, T, .012, cell)
        bx(g, x0, x1, y0 + .07, y1 - .07, cz - .03, cz + .03, f, T, 0, cell, ('-y', '+y'))
        step = 1.5 if T.i == 0 else 3.0
        x = x0 + .75
        while x < x1:
            for (za, zb) in ((z0 + .04, cz - .03), (cz + .03, z1 - .04)):
                bx(g, x - .012, x + .012, y0 + .07, y1 - .07, za, zb, f, T, 0, 0, ('-y', '+y'))
            x += step
    for p in parts:
        name = kname(p)
        if name == 'bracket':
            continue            # the kit's stiffeners and flange strips: the girder is modelled
        if name == 'track' and T.i < 2:
            c, h = p['c'], p['h']
            ibeam(g, (c[0] - h[0], c[1], c[2]), (c[0] + h[0], c[1], c[2]), .13, h[1] * 2, mat('iron'), T, tf=.03, tw=.04)
            continue
        kit_part(g, p, T)


# ---------------------------------------------------------------------------------------------------------------------------
# The hall roofs: sheets, trusses, lantern frames, turbine vents, the lamps
# ---------------------------------------------------------------------------------------------------------------------------
def roof_plate(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    f = mat('roof')
    under = tint('roof', 0x6A747C)
    corrugated_top(g, x0, x1, z0, z1, y0, under, T, up=False, pitch=.62 if T.i == 0 else .4)
    sk.grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, f, Y, (6.0, 12.0, 40.0)[T.i])
    edge = mat('truss')
    n = (12, 4, 1)[T.i]
    for k in range(n):
        xa, xb = x0 + (x1 - x0) * k / n, x0 + (x1 - x0) * (k + 1) / n
        g.face(((xa, y0 - .02, z0), (xb, y0 - .02, z0), (xb, y1 + .03, z0), (xa, y1 + .03, z0)), edge, normal=(0, 0, -1))
        g.face(((xa, y0 - .02, z1), (xb, y0 - .02, z1), (xb, y1 + .03, z1), (xa, y1 + .03, z1)), edge, normal=(0, 0, 1))


def vent(g, x, y, z, T):
    """A turbine ventilator on its upstand."""
    f, d = mat('roof'), mat('truss')
    n = T.round + 2
    lathe(g, [(.5, 0.0), (.5, .06), (.36, .1), (.36, .34), (.4, .36)], (x, y, z), d, n)
    prof = [(.4, .36), (.5, .46), (.56, .6), (.54, .76), (.42, .88), (.2, .95), (0.0, .97)]
    lathe(g, prof, (x, y, z), f, n)
    if T.tiny:
        for k in range(16):
            a = 2 * math.pi * k / 16
            pts = [(x + (r + .012) * math.cos(a + (h - .36) * .9), y + h, z + (r + .012) * math.sin(a + (h - .36) * .9)) for r, h in prof[:5]]
            tube(g, pts, .012, d, 3, caps=False)


def roofs(g, L, T, parts):
    tops = {}
    for p in parts:
        name = kname(p)
        if name == 'glass':
            continue            # the lanterns' glazing stays the kit's lit boxes (no sun shadow)
        if name == 'roof':
            c, h = p['c'], p['h']
            vent(g, c[0], c[1] - h[1], c[2], T)
            tops[(round(c[0], 2), round(c[2], 2))] = 1
            continue
        if name == 'bracket' and (round(p['c'][0], 2), round(p['c'][2], 2)) in tops:
            continue            # a vent's cap
        if name == 'frame' and T.i == 2 and p['h'][0] < .1 and p['h'][2] < .1:
            continue            # the Low tier keeps the lanterns' long rails only
        if name == 'truss' and T.i == 2 and min(p['h'][0], p['h'][1]) < .045:
            continue            # the Low tier leaves out the trusses' diagonals
        kit_part(g, p, T)


def highbay(g, x, y, z, T, lit=True):
    """A high-bay sodium lamp on its drop rod: a spun reflector and its glowing bowl."""
    d = mat('black')
    cyl(g, (x, y + .5, z), (x, y + 1.72, z), .02, d, T.small)
    lathe(g, [(.07, .5), (.1, .44), (.1, .3), (.16, .24), (.3, .02), (.32, -.04)], (x, y, z), mat('iron'), T.round)
    lathe(g, [(.3, -.03), (.2, -.09), (0.0, -.11)], (x, y, z), mat('sodium'), T.round)


def flood(g, x, y, z, wall_x, T):
    """A dock flood: twin heads on a wall arm."""
    d = mat('steel')
    s = 1 if wall_x > x else -1
    bar(g, (wall_x, y + .35, z), (x, y + .35, z), .1, .1, d, T)
    bar(g, (wall_x, y - .7, z), (x + s * 1.2, y + .3, z), .06, .06, d, T)
    for dz in (-.45, .45):
        bx(g, x - .18, x + .18, y - .02, y + .3, z + dz - .28, z + dz + .28, mat('black'), T, .03)
        g.face(((x - .15, y - .025, z + dz - .25), (x + .15, y - .025, z + dz - .25), (x + .15, y - .025, z + dz + .25), (x - .15, y - .025, z + dz + .25)), mat('flood'),
               normal=(0, -1, 0))


def lights(g, L, T):
    y = L['lights'][0][1]
    for z in sorted(set(l[2] for l in L['lights'][:4])):
        for x in (-26, -13, 0, 13, 26):
            highbay(g, x, y, z, T)
    for (x, y, z) in L['lights'][5:7]:
        flood(g, x, y, z, 38.0 if x > 0 else -38.0, T)


# ---------------------------------------------------------------------------------------------------------------------------
# Decks, stairs, pulpits
# ---------------------------------------------------------------------------------------------------------------------------
def deck(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    cell = (3.0, 0, 0)[T.i]
    box(g, x0, x1, y0, y1, z0, z1, mat('grating'), T, .008, cell)
    walls = [ext(o) for o in BL.values() if '-parapet-' in o['id']] if '-pulpit-' in b['id'] else []
    for p in parts:
        c = p['c']
        if any(e[0] - .06 <= c[0] <= e[1] + .06 and e[2] - .06 <= c[1] <= e[3] + .06 and e[4] - .06 <= c[2] <= e[5] + .06 for e in walls):
            continue            # a rail the kit runs inside a parapet's wall
        kit_part(g, p, T)


def step(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, mat('grating'), T, 0, 0, skip=('-y',) if y0 < .01 else ())


def leg(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    f = mat('steel')
    bx(g, x0, x1, y0, y1, z0, z1, f, T, .03, 0, ('-y',))
    if T.i < 2:
        bx(g, x0 - .1, x1 + .1, 0, .04, z0 - .1, z1 + .1, mat('iron'), T, .008, 0, ('-y',))
        fk.bolts(g, [(x, .04, z) for x in (x0 - .05, x1 + .05) for z in (z0 - .05, z1 + .05)], mat('black'), T)
        bx(g, x0 - .012, x1 + .012, y1 - .3, y1, z0 - .012, z1 + .012, f, T, .008, 0, ('-y', '+y'))


def parapet(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    f = tint('clad', 0x6E7C84)
    if (x1 - x0) < (z1 - z0):
        bx(g, x0, x1, y0, y1, z0, z1, f, T, 0, 0, ('-x', '+x'))
        corrugated(g, (x0, z0), (x0, z1), y0, y1, (-1, 0), f, T, pitch=.25, depth=.02)
        corrugated(g, (x1, z0), (x1, z1), y0, y1, (1, 0), f, T, pitch=.25, depth=.02)
        bx(g, x0 - .03, x1 + .03, y1 - .02, y1 + .05, z0, z1, mat('yellow'), T, .012)
    else:
        bx(g, x0, x1, y0, y1, z0, z1, f, T, 0, 0, ('-z', '+z'))
        corrugated(g, (x0, z0), (x1, z0), y0, y1, (0, -1), f, T, pitch=.25, depth=.02)
        corrugated(g, (x0, z1), (x1, z1), y0, y1, (0, 1), f, T, pitch=.25, depth=.02)
        bx(g, x0, x1, y1 - .02, y1 + .05, z0 - .03, z1 + .03, mat('yellow'), T, .012)
    for p in parts:
        kit_part(g, p, T)


def pulpit_roof(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    bx(g, x0, x1, y0, y1, z0, z1, mat('tar'), T, .02)
    bx(g, x0 - .04, x1 + .04, y0 - .06, y0 + .04, z0 - .04, z1 + .04, mat('steel'), T, .012, 0, ('+y',))
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    for p in parts:
        kit_part(g, p, T)
    # the beacon on its mast, a console lamp row under the eaves
    lathe(g, [(.06, .8), (.09, .82), (.09, .96), (.05, 1.02), (0.0, 1.03)], (cx, y1, cz), lamp(0xFFB02E, 2.2), T.small + 2)
    # bracing between the legs (above head height: the perch's legs stay open to walk between)
    f = mat('steel')
    zs = z0 + .4
    xw = x0 + .4 if (x0 + x1) < 0 else x1 - .4
    ya, yb = 2.5, 5.7
    for (p0, p1) in (((x0 + .4, ya, zs), (x1 - .4, yb, zs)), ((x0 + .4, yb, zs), (x1 - .4, ya, zs)), ((xw, ya, z0 + .4), (xw, yb, z1 - .4)), ((xw, yb, z0 + .4), (xw, ya, z1 - .4))):
        tube(g, [p0, p1], .035, f, T.small)


# ---------------------------------------------------------------------------------------------------------------------------
# The presses' fixed parts
# ---------------------------------------------------------------------------------------------------------------------------
def press_head(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    f = mat('press')
    bx(g, x0, x1, y0, y1, z0, z1, f, T, .05, 0)
    if T.i < 2:
        # a cast crown: ribs across the head, the maker's plate
        for k in range(5):
            x = x0 + .3 + (x1 - x0 - .6) * k / 4
            bx(g, x - .05, x + .05, y1, y1 + .07, z0 + .05, z1 - .05, f, T, .02, 0, ('-y',))
    for p in parts:
        name = kname(p)
        c, h = p['c'], p['h']
        if name == 'ram':
            cyl(g, (c[0], c[1] - h[1], c[2]), (c[0], c[1] + h[1], c[2]), min(h[0], h[2]) * .95, mat('chrome'), T.round)
        elif name == 'crane-dark':
            cyl(g, (c[0], c[1] - h[1], c[2]), (c[0], c[1] + h[1], c[2]), min(h[0], h[2]), mat('black'), T.round)
        elif name == 'red':
            if T.i < 2:
                kit_part(g, p, T, mat('lamp-r'))
        elif name == 'stripe' and h[0] < .05 and h[1] > 1:
            continue            # the tunnel's jambs: the bay wall's own guards paint them
        else:
            kit_part(g, p, T)


def bolster(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    f = mat('iron')
    bx(g, x0, x1, y0, y1, z0, z1, f, T, .015, 0, ('-y',))
    if T.i < 2:
        # T-slots across the die bed
        for k in range(5):
            x = x0 + .4 + (x1 - x0 - .8) * k / 4
            g.face(((x - .03, y1 + .001, z0 + .1), (x + .03, y1 + .001, z0 + .1), (x + .03, y1 + .001, z1 - .1), (x - .03, y1 + .001, z1 - .1)), mat('pit'), normal=Y)


# ---------------------------------------------------------------------------------------------------------------------------
# The whole static scenery for one tier
# ---------------------------------------------------------------------------------------------------------------------------
def canonical(g, b, parts, T, fn, L):
    """fn(g, block, parts, T, L) on the block as its north / A twin, turned back half round for a south / B block."""
    if not fk.turned(b['id']):
        fn(g, b, parts, T, L)
        return
    t = Geo()
    fn(t, fk.twin_block(b), [fk.twin_part(p) for p in parts], T, L)
    g.extend(t.transformed(fk.HALF))


def static_scene(L, T):
    import factory_plant as plant
    g = Geo()
    by_owner = {}
    for p in L['parts']:
        by_owner.setdefault(p['o'], []).append(p)
    used = set()

    def parts_of(o):
        used.add(o)
        return by_owner.get(o, [])
    floor(g, L, T, parts_of('factory-floor'))
    roofs(g, L, T, parts_of('roofs'))
    lights(g, L, T)
    for b in L['blocks']:
        i = b['id']
        if b['role'] != 'static' or i == 'factory-floor':
            continue
        parts = parts_of(i)
        if i.startswith('factory-wall-'):
            wall(g, b, parts, T)
        elif i.startswith('factory-baywall-'):
            baywall(g, b, parts, T)
        elif i.startswith('factory-column-'):
            column(g, b, parts, T)
        elif i.startswith('factory-runway-'):
            runway(g, b, parts, T)
        elif i.startswith('factory-roof-'):
            roof_plate(g, b, T)
        elif i.startswith('factory-gallery-') or i.startswith('factory-catwalk-') or i.endswith('-landing') or (i.startswith('factory-pulpit-') and i.endswith('-deck')):
            deck(g, b, parts, T)
        elif i.startswith('factory-stair-'):
            step(g, b, T)
        elif i.startswith('factory-pulpit-') and '-leg-' in i:
            leg(g, b, T)
        elif i.startswith('factory-pulpit-') and '-parapet-' in i:
            parapet(g, b, parts, T)
        elif i.startswith('factory-pulpit-') and i.endswith('-roof'):
            pulpit_roof(g, b, parts, T)
        elif i.startswith('factory-press-') and i.endswith('-head'):
            press_head(g, b, parts, T)
        elif i.startswith('factory-press-') and i.endswith('-bolster'):
            bolster(g, b, T)
        else:
            canonical(g, b, parts, T, plant.block, L)
    for o, parts in by_owner.items():
        if o.startswith('flight:'):
            used.add(o)
            for p in parts:
                kit_part(g, p, T)
        elif o.startswith('hazard:'):
            used.add(o)
            h = next(x for x in L['hazards'] if 'hazard:' + x['id'] == o)
            for p in parts:
                if h['kind'] == 'saw-slot' and kname(p) == 'void':
                    continue        # the trench is cut through the slab
                kit_part(g, p, T)
    left = [o for o in by_owner if o not in used]
    if left:
        raise SystemExit('kit parts without a model: ' + ', '.join(sorted(left)))
    return g


def play_space(L, g):
    """The stage rule: below 2 m over a floor the art stands on a block or keeps within .25 m of one (nothing stands where no
    collider is). Returns (vertices, proud, farthest, where)."""
    P = g.arrays()[0].astype(np.float64)
    boxes = np.array([ext(b) for b in L['blocks']])
    floors = [(0.0, None)] + [(ext(b)[3], ext(b)) for b in L['blocks'] if b['id'].startswith(('factory-gallery-', 'factory-catwalk-')) or b['id'].endswith(('-landing', '-deck'))]
    fl = np.zeros(len(P))
    for top, e in floors[1:]:
        m = (P[:, 1] >= top - .05) & (P[:, 0] >= e[0] - .3) & (P[:, 0] <= e[1] + .3) & (P[:, 2] >= e[4] - .3) & (P[:, 2] <= e[5] + .3)
        fl = np.where(m, np.maximum(fl, top), fl)
    sel = (P[:, 1] - fl <= 2.0) & (P[:, 1] >= fl - .05) & (np.abs(P[:, 0]) < 38) & (np.abs(P[:, 2]) < 30)
    Q = P[sel]
    best = np.full(len(Q), 1e9)
    for e in boxes:
        dx = np.maximum(0, np.maximum(e[0] - Q[:, 0], Q[:, 0] - e[1]))
        dy = np.maximum(0, e[2] - Q[:, 1])      # standing on a block is no way proud of it
        dz = np.maximum(0, np.maximum(e[4] - Q[:, 2], Q[:, 2] - e[5]))
        best = np.minimum(best, np.sqrt(dx * dx + dy * dy + dz * dz))
    where = {}
    for q in Q[best > .25]:
        k = (int(math.floor(q[0])), round(math.floor(q[1] * 2) / 2, 1), int(math.floor(q[2])))
        where[k] = where.get(k, 0) + 1
    return len(Q), int((best > .25).sum()), float(best.max()) if len(Q) else 0.0, sorted(where.items(), key=lambda kv: -kv[1])[:24]


VIEWS = [('bay-west', (-21.0, 1.8, -3.0), (0, 4, 0), 75), ('bay-gallery', (-12.0, 5.3, 8.6), (6, 2, -4), 75), ('ladle', (-6.0, 1.7, -4.5), (0, 2.4, 0), 62),
         ('hall-n', (-25.0, 1.8, 20.5), (5, 3, 20), 75), ('lathe', (-18.0, 1.7, 17.5), (-22.5, 1.0, 13.3), 60), ('catwalk', (-26.0, 5.3, 28.6), (8, 4.6, 28), 72),
         ('press', (-10.5, 1.7, 14.5), (-10.5, 1.8, 9.7), 65), ('dock', (-22.0, 1.8, 4.0), (-34, 2, -3), 72), ('pulpit', (-18.0, 2.0, -5.5), (-25, 6.5, 0), 65),
         ('roof', (0.0, 1.7, 24.0), (6, 11.5, 16), 75), ('crane', (4.0, 1.7, -7.0), (0, 13.5, 0), 70), ('racks', (10.0, 1.7, -21.0), (23, 1.2, -24.7), 62),
         ('over', (-30.0, 21.0, -27.0), (0, 2, 0), 65), ('stair', (-30.0, 1.7, -25.0), (-37, 2, -29.5), 65), ('mill', (-12.0, 1.7, 18.5), (-15.2, 1.2, 14.6), 55),
         ('cabinet', (0.5, 1.7, 16.5), (2, 1.2, 12.9), 55), ('coils', (9.5, 1.7, 21.5), (6.2, .8, 24.6), 55), ('crib', (-31.0, 1.7, 21.0), (-36.3, 1.3, 17.5), 60),
         ('shelf', (-30.0, 1.7, 16.5), (-33.3, 1.3, 13), 55), ('hammer', (-13.0, 1.8, 0.5), (-8.5, 2.0, 5), 60), ('stock', (-17.0, 1.7, -1.0), (-20, 1.0, -5.9), 60),
         ('office', (-30.0, 1.7, -2.0), (-36, 1.6, -6.8), 60), ('forklift', (-31.5, 1.6, 0.5), (-34.9, 1.1, 2.7), 55), ('container', (-25.0, 1.7, 5.0), (-29.4, 1.3, 0), 60),
         ('bins', (-29.0, 1.6, 22.0), (-32, .6, 24.6), 50), ('beams', (-2.0, 1.6, 4.0), (-5, .5, 6.6), 50), ('slot', (-12.0, 1.5, 22.5), (-6, 0, 19.5), 60),
         ('railsaw', (5.5, 5.2, 26.5), (8, 4.7, 29.5), 60), ('column', (-24.0, 1.7, 13.0), (-27, 2.5, 10.7), 55), ('truss', (-20.0, 4.6, 27.5), (-8, 11, 18), 70),
         ('plates', (-17.0, 1.6, 6.5), (-20, .5, 3.7), 50), ('ingots', (-6.0, 1.5, -0.5), (-8.6, .6, 1.6), 50), ('rack-bay', (-13.0, 1.6, -4.5), (-15.6, 1.3, -2), 50),
         ('screen', (-24.0, 1.7, 19.0), (-27.3, 1.5, 17.2), 60), ('drums', (-22.0, 1.4, 7.6), (-24, .6, 6), 45), ('crate', (-12.0, 1.6, 22.0), (-14, 1.0, 24.6), 50)]


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--layout', required=True)
    ap.add_argument('--out', help='Assets/LocalAssets to write the bank into')
    ap.add_argument('--preview', help='render views of the baked Fine tier')
    ap.add_argument('--tiers', default='0,1,2')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--static-only', action='store_true')
    ap.add_argument('--stats', action='store_true')
    ap.add_argument('--views', default='')
    args = ap.parse_args(argv)
    L = sk.load_layout(args.layout)
    fk.setup(L)
    BL.update({b['id']: b for b in L['blocks']})
    import factory_plant as plant
    import factory_rig as rig
    plant.BL = rig.BL = BL
    sb = sk.StageBank()
    # 20 m cells in two bands (the floor's storey, the roofs'): 32 chunks; the hall is one open room, and a chunk's renderer is a
    # draw in every shadow cascade, so fewer, larger chunks cost less than the triangles they save
    chunks = sk.Chunks(20.0, (-40.0, -40.0), (-1.0, 9.0))
    floors = (0.0, 3.6, 6.0)
    t0 = time.time()
    for ti in [int(x) for x in args.tiers.split(',')]:
        T = sk.Tier(ti)
        t = time.time()
        g = static_scene(L, T)
        # every pair of overlapping faces turned the same way ends a whole lift apart: the kit's paint lies 3 to 12 mm over the slab
        # (joints, washes, lines, numbers), closer than the depth buffer parts at the Mid and Low tiers' range
        print('  coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % sk.lift_coplanar(g, eps=sk.LIFT_TIER[ti] - .0002, lift=sk.LIFT_TIER[ti], passes=12))
        blocks = rig.block_scene(L, T)
        print('tier %s: static %d faces, %.1f s' % (T.name, g.faces, time.time() - t))
        if ti == 0:
            n, proud, far, where = play_space(L, g)
            print('  play space: %d vertices, %d over .25 m from a block, the farthest %.2f m' % (n, proud, far), where if proud else '')
        ao = None
        if not args.no_ao:
            ao = sk.bake_ao([g], distance=(1.3, 1.4, 1.6)[ti], samples=(32, 16, 12)[ti], occluders=[blocks])[0]
            ao = .32 + .68 * ao
        bk = sk.Bake(g, ao, floors=floors, seed=23)
        ids = chunks.ids(g)
        for cid in np.unique(ids):
            sb.add('static/%d/%d' % (cid, ti), 'static', sk.to_mesh('static/%d/%d' % (cid, ti), g, bk, ids == cid))
        if args.stats:
            P, N, S, F = g.arrays()
            fc = np.bincount(F, minlength=len(sk.FINS))
            print('  faces by finish:', ', '.join('%s %d' % (sk.FINS[i].name[2:], fc[i]) for i in np.argsort(-fc)[:26]))
        if args.preview and ti == 0:
            views = VIEWS
            if args.views:
                views = [v for v in views if v[0] in args.views.split(',')]
            pv = [(g, bk), (blocks, sk.Bake(blocks, None))] + rig.preview_rig(L, T)
            sk.preview(pv, views, args.preview)
        if not args.static_only:
            rig.bank_props(sb, L, T, floors, args.no_ao)
        del g
    if args.out:
        for b in L['blocks']:
            sb.cover(b['id'])
        sb.write(args.out, BANK)
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
