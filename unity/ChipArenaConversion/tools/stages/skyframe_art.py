"""Skyframe's stage art (docs/arenas/SKYFRAME.md "Stage art", docs/arenas/STAGE-ART.md): the sky garden at the top of a tower still
under construction, modelled on the map's own blocks, into the stage bank ChipArenaStageSkyframe.

  dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout skyframe LAYOUT.json
  blender --background --factory-startup --python tools/stages/skyframe_art.py -- --layout LAYOUT.json --out Assets/LocalAssets
  (--preview DIR [--views a,b] [--preview-tier N] renders the baked stage in Blender; --tiers, --no-ao, --no-sky, --static-only)

This file: the garden terrace and its fascia with the hanging gardens, the lift core and its lobbies, the gallery and its
columns, the pavilions, the glass bridges and balustrades, the stairs, the cloud terraces, the planters, hedges, living walls and
site stacks, and the unfinished frame overhead (the columns going on up, the beams thinning out storey by storey, the decked
bays, the core's rise and its starter bars, the crane's mast). skyframe_props.py: the breakable blocks and the rig (the crane's
top, trolley, ropes, hook and bundle, the crowns, windsocks, rotors, airships, birds and cloud islands). skyframe_sky.py: the far
chunks (the tower's body, the turbines' masts, the other towers, the cloud sea and its banks). skyframe_kit.py: finishes and
shapes. Everything stands on a block (within .25 m of it under 2 m over its floor) or overhead, or outside the floors' edges.
"""
import argparse
import math
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, grid_face, lathe  # noqa: E402
import skyframe_kit as kit  # noqa: E402
from skyframe_kit import F, blk, hashf, beam, ibeam, rod, slab, rect, blob, leafy, hsection, ly, bed  # noqa: E402

BANK = 'ChipArenaStageSkyframe'
FLOORS = (-3.2, 0.0, 3.6)
GREENS = ('leaf-dark', 'leaf', 'leaf')


def fins(names):
    return tuple(F[n] for n in names)


# ---------------------------------------------------------------------------------------------------------------------------
# The garden terrace
# ---------------------------------------------------------------------------------------------------------------------------
def zones(g, L, y, b, T, layer=1):
    """The painted drop zones lying on this floor (y): a frame, a hatch across it, corner marks."""
    for h in L['hazards']:
        if h['kind'] != 'crane':
            continue
        hx, hz = h['size'][0] / 2 + .5, h['size'][2] / 2 + .5
        for z in h['zones']:
            cx, cy, cz = z['center']
            if abs(cy - y) > .05 or not (b[0] <= cx <= b[1] and b[4] <= cz <= b[5]):
                continue
            Y = F['paint-yellow']
            y = y + ly(layer - 1)
            rect(g, cx - hx, cx + hx, y, cz - hz, cz - hz + .14, Y)
            rect(g, cx - hx, cx + hx, y, cz + hz - .14, cz + hz, Y)
            rect(g, cx - hx, cx - hx + .14, y, cz - hz + .14, cz + hz - .14, Y)
            rect(g, cx + hx - .14, cx + hx, y, cz - hz + .14, cz + hz - .14, Y)
            if T.detail:
                n = 9
                for k in range(n):
                    x = cx - hx + .5 + (2 * hx - 1.0) * k / (n - 1)
                    g.face(((x - .32, y + ly(1), cz - hz + .22), (x - .2, y + ly(1), cz - hz + .22), (x + .32, y + ly(1), cz + hz - .22), (x + .2, y + ly(1), cz + hz - .22)), Y, (0, 1, 0))


def garden(g, L, T):
    b = blk(L, 'skyframe-garden')
    x0, x1, y0, y1, z0, z1 = b
    c = (1.0, 3.0, 6.5)[T.i]
    grid_face(g, (x0, 0, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['paving'], (0, 1, 0), c)
    for s in (1, -1):
        def R(a0, a1, c0, c1, f, cell=0, layer=1):
            if s < 0:
                a0, a1, c0, c1 = -a1, -a0, -c1, -c0
            rect(g, a0, a1, 0, c0, c1, f, cell, layer)
        # the lawn of the drop-zone quarter in its stone kerb, the reflecting pool before the base, the border course
        R(-24.7, -10.1, 9.3, 15.5, F['coping'])
        R(-24.6, -10.2, 9.4, 15.4, F['lawn'], (1.2, 3, 0)[T.i], 2)
        if T.detail:
            for k in range(7):
                R(-24.6 + k * 2.1, -24.6 + k * 2.1 + 1.0, 9.4, 15.4, F['lawn-dark'], 0, 3)
        R(-16.1, -13.1, -2.1, 2.1, F['coping'])
        R(-15.95, -13.25, -1.95, 1.95, F['water'], 0, 2)
        if T.detail:
            # bronze inlays: a line round the pool, rays out from it across the forecourt, a dashed line down each lane
            for (a0, a1, c0, c1) in ((-16.45, -12.75, -2.45, -2.37), (-16.45, -12.75, 2.37, 2.45), (-16.45, -16.37, -2.37, 2.37), (-12.83, -12.75, -2.37, 2.37)):
                R(a0, a1, c0, c1, F['bronze'])
            for k in range(9):
                ang = -1.2 + k * .3
                x0, z0 = -14.6 + math.cos(ang) * 3.1, math.sin(ang) * 3.1
                x1, z1 = -14.6 + math.cos(ang) * 5.6, math.sin(ang) * 5.6
                if x0 < -12.2 or abs(z0) > 3.4:
                    continue
                px, pz = -math.sin(ang) * .03, math.cos(ang) * .03
                pts = [(x0 - px, ly(1), z0 - pz), (x1 - px, ly(1), z1 - pz), (x1 + px, ly(1), z1 + pz), (x0 + px, ly(1), z0 + pz)]
                if s < 0:
                    pts = [(-q[0], q[1], -q[2]) for q in pts]
                g.face(pts, F['bronze'], (0, 1, 0))
            for k in range(11):
                R(-8.0 + k * 1.9, -8.0 + k * 1.9 + 1.0, 12.47, 12.53, F['bronze'])
        R(-25.78, -25.4, -15.78, 15.78, F['paving-dark'])
        R(-25.4, 25.4, 15.4, 15.78, F['paving-dark'])
        if T.detail:
            # a band of darker paving round the core and bronze studs along the lanes
            R(-9.3, -9.0, -9.3, 9.3, F['paving-dark'])
            R(-9.0, 9.3, 9.0, 9.3, F['paving-dark'])
    if T.i < 2:
        # lights set flush in the paving: along the balustrades, round the core's arcade, before the lobbies
        for k in range(13):
            x = -24 + k * 4.0
            for z in (-15.1, 15.1):
                rect(g, x - .09, x + .09, 0, z - .09, z + .09, F['lamp-soft'])
        for k in range(8):
            z = -14 + k * 4.0
            for x in (-25.1, 25.1):
                rect(g, x - .09, x + .09, 0, z - .09, z + .09, F['lamp-soft'])
        for k in range(10):
            a = -9.6 + k * 19.2 / 9
            for (x, z) in ((a, -9.65), (a, 9.65), (-9.65, a), (9.65, a)):
                rect(g, x - .07, x + .07, 0, z - .07, z + .07, F['lamp-soft'])
    # planting beds set in the paving (ankle high): along the balustrades, round the core's foot, by the pavilions' walls
    BEDS = ((-8.6, -1.4, 14.75, 15.35), (-25.35, -24.85, 9.4, 15.3), (-25.35, -24.75, -5.9, -2.2), (-25.35, -24.75, 2.2, 5.6), (-5.75, -5.1, -4.4, 4.4),
            (-13.9, -13.3, -11.2, -7.2), (-13.9, -13.3, -15.4, -14.0), (-0.9, 4.4, 5.1, 5.7), (-21.0, -17.6, 6.9, 7.5))
    GF, FL = fins(('leaf-dark', 'leaf', 'leaf-light')), fins(('flower-pink', 'flower-white', 'flower-orange', 'leaf-gold'))
    for k, (a0, a1, c0, c1) in enumerate(BEDS):
        bed(g, a0, a1, 0, c0, c1, T, 300 + k * 37, GF, FL)
        bed(g, -a1, -a0, 0, -c1, -c0, T, 900 + k * 41, GF, FL)
    zones(g, L, 0.0, b, T, 4)
    fascia(g, b, T, L)


def fascia(g, b, T, L):
    """The terrace's edge: a stone coping, a steel channel, the top storey's glazing under it, and the hanging gardens (troughs
    under the lip with vines down the glass)."""
    x0, x1, y0, y1, z0, z1 = b
    for side in range(4):
        # the face's line: origin, along, outward
        if side == 0:
            o, u, n, ln = (x0, 0, z0), (0, 0, 1), (-1, 0, 0), z1 - z0
        elif side == 1:
            o, u, n, ln = (x1, 0, z0), (0, 0, 1), (1, 0, 0), z1 - z0
        elif side == 2:
            o, u, n, ln = (x0, 0, z0), (1, 0, 0), (0, 0, -1), x1 - x0
        else:
            o, u, n, ln = (x0, 0, z1), (1, 0, 0), (0, 0, 1), x1 - x0
        def P(a, y, out):
            return (o[0] + u[0] * a + n[0] * out, y, o[2] + u[2] * a + n[2] * out)
        def strip(a0, a1, ya, yb, out, f, top=True, bottom=True):
            g.face((P(a0, ya, out), P(a1, ya, out), P(a1, yb, out), P(a0, yb, out)), f, n)
            if top:
                g.face((P(a0, yb, 0), P(a1, yb, 0), P(a1, yb, out), P(a0, yb, out)), f, (0, 1, 0))
            if bottom:
                g.face((P(a0, ya, 0), P(a1, ya, 0), P(a1, ya, out), P(a0, ya, out)), f, (0, -1, 0))
        strip(0, ln, -.3, -.012, .1, F['coping'])
        strip(0, ln, -.62, -.3, .05, F['steel'], top=False)
        cells = max(1, int(ln / (2.0 if T.i < 2 else 8.0)))
        for k in range(cells):
            a0, a1 = ln * k / cells, ln * (k + 1) / cells
            g.face((P(a0, y0, 0), P(a1, y0, 0), P(a1, -.62, 0), P(a0, -.62, 0)), F['glass-dark'], n)
            if T.i < 2:
                strip(a0 - .04, a0 + .04, y0, -.62, .07, F['steel'], top=False, bottom=False)
        strip(0, ln, -3.95, -3.7, .09, F['steel'])
        if T.i == 2:
            continue
        # the hanging gardens: a trough under the lip in every other bay, leaves over its edge, vines down the glass
        for k in range(cells):
            if (k + side) % 2:
                continue
            a0, a1 = ln * k / cells + .25, ln * (k + 1) / cells - .25
            strip(a0, a1, -1.02, -.66, .22, F['stone'])
            nl = 6 if T.fine else 3
            for j in range(nl):
                h = hashf(side, k, j)
                a = a0 + (a1 - a0) * (j + .5) / nl
                w = .12 + .1 * hashf(side, k, j, 2)
                ln2 = .5 + 2.2 * hashf(side, k, j, 3)
                f = F[('leaf', 'leaf-dark', 'leaf-light', 'leaf')[int(h * 4) % 4]]
                out = .2 + .03 * (j % 2)
                g.face((P(a - w, -.7 - ln2, out - .06), P(a + w, -.7 - ln2, out - .06), P(a + w * 1.2, -.62, out), P(a - w * 1.2, -.62, out)), f, n)
                if T.fine and h > .6:
                    g.face((P(a - .05, -.9 - ln2 * .5, out + .012), P(a + .05, -.9 - ln2 * .5, out + .012), P(a + .05, -.8 - ln2 * .5, out + .012), P(a - .05, -.8 - ln2 * .5, out + .012)),
                           F[('flower-pink', 'flower-white', 'flower-orange')[j % 3]], n)


# ---------------------------------------------------------------------------------------------------------------------------
# The lift core and its lobbies
# ---------------------------------------------------------------------------------------------------------------------------
def core(g, L, T):
    C, CD = F['concrete'], F['concrete-dark']
    for i in kit.ids(L, 'skyframe-core-'):
        b = blk(L, i)
        x0, x1, y0, y1, z0, z1 = b
        box(g, x0, x1, y0, y1, z0, z1, C, T, 0, cell=(1.5, 4, 0)[T.i], skip=('+y',) + (() if y0 > .1 else ('-y',)))
        if T.detail:
            # formwork joints: a line every 2.5 m along the outer faces and every 3 m up them
            for a in (-2.5, 0.0, 2.5):
                for (cond, pa, pb, nn) in ((z0 <= -4.99 and x0 < a < x1, (a - .02, z0), (a + .02, z0), (0, 0, -1)), (z1 >= 4.99 and x0 < a < x1, (a - .02, z1), (a + .02, z1), (0, 0, 1)),
                                           (x0 <= -4.99 and z0 < a < z1, (x0, a - .02), (x0, a + .02), (-1, 0, 0)), (x1 >= 4.99 and z0 < a < z1, (x1, a - .02), (x1, a + .02), (1, 0, 0))):
                    if cond:
                        g.face(((pa[0], y0, pa[1]), (pb[0], y0, pb[1]), (pb[0], y1, pb[1]), (pa[0], y1, pa[1])), CD, nn)
            for y in (3.0, 6.0, 9.0):
                if y <= y0 + .1 or y >= y1:
                    continue
                if x0 <= -4.99:
                    g.face(((x0, y - .03, z0), (x0, y - .03, z1), (x0, y + .03, z1), (x0, y + .03, z0)), CD, (-1, 0, 0))
                if x1 >= 4.99:
                    g.face(((x1, y - .03, z0), (x1, y - .03, z1), (x1, y + .03, z1), (x1, y + .03, z0)), CD, (1, 0, 0))
                if z0 <= -4.99:
                    g.face(((x0, y - .03, z0), (x1, y - .03, z0), (x1, y + .03, z0), (x0, y + .03, z0)), CD, (0, 0, -1))
                if z1 >= 4.99:
                    g.face(((x0, y - .03, z1), (x1, y - .03, z1), (x1, y + .03, z1), (x0, y + .03, z1)), CD, (0, 0, 1))
        if '-lintel-' in i:
            # the lobby's ceiling light and the lit head over its mouth
            s = 1 if (z0 + z1) > 0 else -1
            cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
            g.face(((cx - 1.1, y0 - ly(1), cz - .6), (cx + 1.1, y0 - ly(1), cz - .6), (cx + 1.1, y0 - ly(1), cz + .6), (cx - 1.1, y0 - ly(1), cz + .6)), F['lamp-soft'], (0, -1, 0))
            zf = z1 if s > 0 else z0
            g.face(((x0 + .15, y0 + .06, zf + s * ly(1)), (x1 - .15, y0 + .06, zf + s * ly(1)), (x1 - .15, y0 + .2, zf + s * ly(1)), (x0 + .15, y0 + .2, zf + s * ly(1))), F['lamp'], (0, 0, s))
        if '-body-' in i:
            # the lift doors on the lobby's back wall: a steel frame, two stainless leaves, the call panel
            s = 1 if (z0 + z1) < 0 else -1          # the back wall faces the lobby's mouth
            zf = z1 if s > 0 else z0
            cx = (x0 + x1) / 2
            def Q(a0, a1, ya, yb, out, f):
                z = zf + s * out
                g.face(((a0, ya, z), (a1, ya, z), (a1, yb, z), (a0, yb, z)), f, (0, 0, s))
            box(g, cx - 1.2, cx - 1.05, 0, 2.5, min(zf, zf + s * .08), max(zf, zf + s * .08), F['steel'], T, .01, cell=0)
            box(g, cx + 1.05, cx + 1.2, 0, 2.5, min(zf, zf + s * .08), max(zf, zf + s * .08), F['steel'], T, .01, cell=0)
            box(g, cx - 1.2, cx + 1.2, 2.35, 2.5, min(zf, zf + s * .08), max(zf, zf + s * .08), F['steel'], T, .01, cell=0)
            Q(cx - 1.05, cx - .015, 0, 2.35, .03, F['stainless'])
            Q(cx + .015, cx + 1.05, 0, 2.35, .03, F['stainless'])
            Q(cx - .3, cx + .3, 2.56, 2.74, .012, F['screen'])
            Q(cx + 1.3, cx + 1.42, 1.05, 1.3, .012, F['screen'])


def columns(g, L, T):
    for i in kit.ids(L, 'skyframe-column-'):
        x0, x1, y0, y1, z0, z1 = blk(L, i)
        cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
        turn = abs(cz) > abs(cx)
        hsection(g, cx, cz, y0, y1, T, F['primer'], .52, .5, turn)
        if T.i < 2:
            box(g, x0 - .1, x1 + .1, 0, .05, z0 - .1, z1 + .1, F['steel'], T, .01, cell=0, skip=('-y',))
            for y in (3.0, 9.0):
                box(g, x0 - .04, x1 + .04, y - .16, y + .16, z0 - .04, z1 + .04, F['steel'], None, 0, cell=0, skip=('+y', '-y'))
        if T.fine:
            for sx in (-1, 1):
                for sz in (-1, 1):
                    sk.cylinder(g, (cx + sx * .33, .05, cz + sz * .33), (cx + sx * .33, .1, cz + sz * .33), .03, F['stainless'], 6)


# ---------------------------------------------------------------------------------------------------------------------------
# Decks, balustrades, stairs
# ---------------------------------------------------------------------------------------------------------------------------
def decks(g, L, T):
    for i in kit.ids(L, 'skyframe-gallery-'):
        b = blk(L, i)
        x0, x1, y0, y1, z0, z1 = b
        slab(g, b, T, F['paving'], F['steel'], F['concrete'], cell=(1.0, 3, 6)[T.i])
        if T.i < 2:
            # joists under it and a line of soffit lights
            lx = (x1 - x0) >= (z1 - z0)
            n = int(((x1 - x0) if lx else (z1 - z0)) / 2)
            for k in range(1, n):
                if lx:
                    x = x0 + (x1 - x0) * k / n
                    box(g, x - .06, x + .06, y0 - .16, y0, z0 + .1, z1 - .1, F['steel'], None, 0, cell=0, skip=('+y',))
                    g.face(((x + .5, y0, (z0 + z1) / 2 - .2), (x + 1.1, y0, (z0 + z1) / 2 - .2), (x + 1.1, y0, (z0 + z1) / 2 + .2), (x + .5, y0, (z0 + z1) / 2 + .2)), F['lamp-soft'], (0, -1, 0))
                else:
                    z = z0 + (z1 - z0) * k / n
                    box(g, x0 + .1, x1 - .1, y0 - .16, y0, z - .06, z + .06, F['steel'], None, 0, cell=0, skip=('+y',))
                    g.face((((x0 + x1) / 2 - .2, y0, z + .5), ((x0 + x1) / 2 + .2, y0, z + .5), ((x0 + x1) / 2 + .2, y0, z + 1.1), ((x0 + x1) / 2 - .2, y0, z + 1.1)), F['lamp-soft'], (0, -1, 0))
    for i in kit.ids(L, 'skyframe-bridge-'):
        b = blk(L, i)
        x0, x1, y0, y1, z0, z1 = b
        # a glass floor between two steel edge beams, ribs under it
        slab(g, b, T, F['glass-floor'], F['steel'], F['glass-dark'], cell=0)
        if T.i < 2:
            rect(g, x0, x1, y1, z0, z0 + .22, F['steel'])
            rect(g, x0, x1, y1, z1 - .22, z1, F['steel'])
            for k in range(1, 5):
                x = x0 + (x1 - x0) * k / 5
                rect(g, x - .03, x + .03, y1, z0 + .22, z1 - .22, F['steel'])
            ibeam(g, (x0, y0 - .2, z0 + .1), (x1, y0 - .2, z0 + .1), .2, .4, F['steel'], T)
            ibeam(g, (x0, y0 - .2, z1 - .1), (x1, y0 - .2, z1 - .1), .2, .4, F['steel'], T)
    for i in kit.ids(L, 'skyframe-terrace-'):
        b = blk(L, i)
        x0, x1, y0, y1, z0, z1 = b
        s = 1 if z0 > 0 else -1
        slab(g, b, T, F['paving'], F['steel'], F['concrete'], cell=(1.0, 3, 6)[T.i])
        zo = z1 if s > 0 else z0
        rect(g, x0 + .1, x1 - .1, y1, min(zo, zo - s * .35), max(zo, zo - s * .35), F['paving-dark'])
        # the crosswind's warning: chevrons across the gust's section, pointing off the edge
        for h in L['hazards']:
            if h['kind'] != 'vent' or (h['a'][2] > 0) != (s > 0):
                continue
            ax, hw = h['a'][0], h['size'][0] / 2
            rect(g, ax - hw, ax + hw, y1, min(zo - s * .1, zo - s * .5), max(zo - s * .1, zo - s * .5), F['paint-yellow'], 0, 2)
            if T.detail:
                for k in range(4):
                    cx = ax - hw + (k + .5) * hw / 2
                    for row in range(3):
                        zc = zo - s * (1.6 + row * 1.4)
                        g.face(((cx - .55, y1 + ly(1), zc - s * .35), (cx - .4, y1 + ly(1), zc - s * .45), (cx, y1 + ly(1), zc - s * .05), (cx, y1 + ly(1), zc + s * .15)), F['paint-yellow'], (0, 1, 0))
                        g.face(((cx + .55, y1 + ly(1), zc - s * .35), (cx + .4, y1 + ly(1), zc - s * .45), (cx, y1 + ly(1), zc - s * .05), (cx, y1 + ly(1), zc + s * .15)), F['paint-yellow'], (0, 1, 0))
        # knee braces back to the tower's wall under it
        if T.i < 2:
            zi = z0 if s > 0 else z1
            n = int((x1 - x0) / 3)
            for k in range(n + 1):
                x = x0 + .4 + (x1 - x0 - .8) * k / n
                ibeam(g, (x, y0 - .15, zi), (x, y0 - .15, zo - s * .3), .16, .3, F['steel'], T)
                beam(g, (x, y0 - 2.6, zi + s * .05), (x, y0 - .3, zo - s * .8), .12, .12, F['steel'])
    for i in kit.ids(L, 'skyframe-landing-'):
        b = blk(L, i)
        slab(g, b, T, F['paving'], F['stone'], F['concrete'], cell=(1.0, 3, 0)[T.i])


def rails(g, L, T):
    """Glass balustrades: a shoe, panes, slim posts and a handrail."""
    for i in kit.ids(L, 'skyframe-rail-'):
        x0, x1, y0, y1, z0, z1 = blk(L, i)
        lx = (x1 - x0) >= (z1 - z0)
        ln = (x1 - x0) if lx else (z1 - z0)
        cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
        def B(a0, a1, ya, yb, hw, f, bevel=0.0, skip=()):
            if lx:
                box(g, x0 + a0, x0 + a1, ya, yb, cz - hw, cz + hw, f, None, bevel, cell=0, skip=skip)
            else:
                box(g, cx - hw, cx + hw, ya, yb, z0 + a0, z0 + a1, f, None, bevel, cell=0, skip=skip)
        B(0, ln, y0, y0 + .1, .07, F['steel'])
        n = max(1, int(round(ln / 1.5))) if T.i < 2 else 1
        gap = .012 if T.fine else 0
        for k in range(n):
            B(ln * k / n + gap, ln * (k + 1) / n - gap, y0 + .1, y1 - .06, .014, F['glass'], skip=('-y',))
        if T.i < 2:
            for k in range(n + 1):
                a = min(max(ln * k / n, .03), ln - .03)
                B(a - .025, a + .025, y0 + .1, y1 - .05, .035, F['stainless'], skip=('-y',))
        if T.fine:
            p0 = (x0, y1 - .03, cz) if lx else (cx, y1 - .03, z0)
            p1 = (x1, y1 - .03, cz) if lx else (cx, y1 - .03, z1)
            sk.cylinder(g, p0, p1, .03, F['stainless'], 6)
        else:
            B(0, ln, y1 - .06, y1, .03, F['stainless'])


def stairs(g, L, T):
    flights = {}
    for i in kit.ids(L, 'skyframe-stair-'):
        flights.setdefault(i.rsplit('-', 1)[0], []).append(i)
    for name, steps in flights.items():
        steps.sort()
        bs = [blk(L, i) for i in steps]
        up = 1 if (bs[-1][0] + bs[-1][1]) > (bs[0][0] + bs[0][1]) else -1     # the flight rises toward +x or -x
        hung = 'terrace' in name
        for b in bs:
            x0, x1, y0, y1, z0, z1 = b
            lead = x0 if up > 0 else x1                                            # the tread's leading edge (toward the lower step)
            if hung:
                box(g, x0, x1, y1 - .09, y1, z0, z1, F['paving'], T, .012, cell=0)
            else:
                # the tread, its riser and the flight's two cheeks
                g.face(((x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)), F['paving'], (0, 1, 0))
                g.face(((lead, y1 - .2, z0), (lead, y1 - .2, z1), (lead, y1, z1), (lead, y1, z0)), F['stone'], (-up, 0, 0))
                g.face(((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)), F['stone'], (0, 0, -1))
                g.face(((x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)), F['stone'], (0, 0, 1))
            if T.i < 2:
                a, c = (lead - .012, lead + .05) if up > 0 else (lead - .05, lead + .012)
                box(g, a, c, y1 - .035, y1 + .006, z0, z1, F['coping'], None, .008 if T.fine else 0, cell=0, skip=('-y',))
        # the flight's far end face (under its top step) and, hung over the clouds, two steel stringers under the treads
        top = bs[-1]
        if not hung:
            xe = top[1] if up > 0 else top[0]
            g.face(((xe, top[2], top[4]), (xe, top[2], top[5]), (xe, top[3], top[5]), (xe, top[3], top[4])), F['stone'], (up, 0, 0))
        else:
            lo, hi = bs[0], bs[-1]
            xa, xb = ((lo[0], hi[1]) if up > 0 else (lo[1], hi[0]))
            for z in (lo[4] + .25, lo[5] - .25):
                ibeam(g, (xa, lo[3] - .42, z), (xb, hi[3] - .32, z), .14, .3, F['steel'], T)


# ---------------------------------------------------------------------------------------------------------------------------
# The pavilions
# ---------------------------------------------------------------------------------------------------------------------------
def pavilions(g, L, T):
    for i in kit.ids(L, 'skyframe-pavilion-'):
        b = blk(L, i)
        x0, x1, y0, y1, z0, z1 = b
        if i.endswith('-roof'):
            slab(g, b, T, F['paving'], F['coping'], F['timber'], cell=(1.0, 3, 6)[T.i])
            # the roof garden: a strip of lawn inside the balustrades, the drop zone painted on the paving
            s = 1 if x0 < 0 else -1
            xo = x0 if s > 0 else x1
            rect(g, min(xo + s * .5, xo + s * 2.2), max(xo + s * .5, xo + s * 2.2), y1, z0 + .5, z1 - .5, F['lawn'], (1.2, 0, 0)[T.i])
            zones(g, L, y1, b, T)
            if T.i < 2:
                for side, (a, c, n) in enumerate((((x0, z0), (x1, z0), (0, 0, -1)), ((x0, z1), (x1, z1), (0, 0, 1)), ((x0, z0), (x0, z1), (-1, 0, 0)), ((x1, z0), (x1, z1), (1, 0, 0)))):
                    p = [(a[0] + n[0] * .08, y0 - .08, a[1] + n[2] * .08), (c[0] + n[0] * .08, y0 - .08, c[1] + n[2] * .08), (c[0] + n[0] * .08, y1 - .05, c[1] + n[2] * .08), (a[0] + n[0] * .08, y1 - .05, a[1] + n[2] * .08)]
                    g.face(p, F['coping'], n)
                # the room's ceiling lights
                for k in range(3):
                    cx = x0 + (x1 - x0) * (k + .5) / 3
                    cz = (z0 + z1) / 2
                    g.face(((cx - .5, y0, cz - .25), (cx + .5, y0, cz - .25), (cx + .5, y0, cz + .25), (cx - .5, y0, cz + .25)), F['lamp-soft'], (0, -1, 0))
            continue
        lx = (x1 - x0) >= (z1 - z0)
        if '-head-' in i:
            # a door's head: a steel lintel with a light under it and the jambs down to the floor
            box(g, x0, x1, y0, y1, z0, z1, F['steel'], T, .01, cell=0)
            if lx:
                g.face(((x0 + .1, y0, z0 + .1), (x1 - .1, y0, z0 + .1), (x1 - .1, y0, z1 - .1), (x0 + .1, y0, z1 - .1)), F['lamp'], (0, -1, 0))
                for x in (x0, x1):
                    box(g, x - .05, x + .05, 0, y0, z0 - .03, z1 + .03, F['steel'], None, 0, cell=0, skip=('+y', '-y'))
            else:
                g.face(((x0 + .1, y0, z0 + .1), (x1 - .1, y0, z0 + .1), (x1 - .1, y0, z1 - .1), (x0 + .1, y0, z1 - .1)), F['lamp'], (0, -1, 0))
                for z in (z0, z1):
                    box(g, x0 - .03, x1 + .03, 0, y0, z - .05, z + .05, F['steel'], None, 0, cell=0, skip=('+y', '-y'))
            continue
        # a wall: stone, a base course, timber battens on both faces, a glazed clerestory band, a cornice
        box(g, x0, x1, y0, y1, z0, z1, F['stone'], T, 0, cell=(1.5, 4, 0)[T.i], skip=('+y', '-y'))
        ln = (x1 - x0) if lx else (z1 - z0)
        for sgn in (-1, 1):
            def Q(a0, a1, ya, yb, out, f, depth=0.0):
                if lx:
                    z = (z1 if sgn > 0 else z0) + sgn * out
                    if depth:
                        box(g, x0 + a0, x0 + a1, ya, yb, min(z, z - sgn * depth), max(z, z - sgn * depth), f, None, 0, cell=0, skip=('-z' if sgn > 0 else '+z', '-y'))
                    else:
                        g.face(((x0 + a0, ya, z), (x0 + a1, ya, z), (x0 + a1, yb, z), (x0 + a0, yb, z)), f, (0, 0, sgn))
                else:
                    x = (x1 if sgn > 0 else x0) + sgn * out
                    if depth:
                        box(g, min(x, x - sgn * depth), max(x, x - sgn * depth), ya, yb, z0 + a0, z0 + a1, f, None, 0, cell=0, skip=('-x' if sgn > 0 else '+x', '-y'))
                    else:
                        g.face(((x, ya, z0 + a0), (x, ya, z0 + a1), (x, yb, z0 + a1), (x, yb, z0 + a0)), f, (sgn, 0, 0))
            Q(0, ln, 0, .42, .05, F['paving-dark'], .05)
            Q(0, ln, 2.95, 3.2, .09, F['coping'], .09)
            Q(0, ln, 2.42, 2.9, ly(1), F['glass'])
            if T.i == 0:
                n = max(1, int(ln / .26))
                for k in range(n):
                    a = ln * (k + .5) / n
                    Q(a - .055, a + .055, .47, 2.36, .05, F['timber' if k % 5 else 'timber-dark'], .05)
                for a in np.arange(1.2, ln - .3, 1.2):
                    Q(a - .025, a + .025, 2.42, 2.9, .03, F['steel'], .03)
            elif T.i == 1:
                n = max(1, int(ln / .6))
                for k in range(n):
                    a = ln * (k + .5) / n
                    Q(a - .16, a + .16, .47, 2.36, .04, F['timber'], .04)
            else:
                Q(0, ln, .47, 2.36, ly(1), F['timber'])
    for i in kit.ids(L, 'skyframe-counter-'):
        x0, x1, y0, y1, z0, z1 = blk(L, i)
        box(g, x0, x1, y0, y1 - .06, z0, z1, F['timber'], T, .015, cell=0, skip=('-y',))
        box(g, x0 - .05, x1 + .05, y1 - .06, y1, z0 - .05, z1 + .05, F['coping'], T, .015, cell=0)
        if T.i < 2:
            for k in range(8):
                x = x0 + .3 + (x1 - x0 - .6) * k / 7
                blob(g, (x, y1 + .05, (z0 + z1) / 2 + (hashf(k, 3) - .5) * .3), .13 + .05 * hashf(k, 7), T, k + 1, fins(('leaf-light', 'leaf', 'leaf-dark')), squash=(1, .8, 1), cut=.3, sides=(8, 6, 5)[T.i])


# ---------------------------------------------------------------------------------------------------------------------------
# Planting and the site's stacks
# ---------------------------------------------------------------------------------------------------------------------------
def planting(g, L, T):
    for i in kit.ids(L, 'skyframe-planter-'):
        x0, x1, y0, y1, z0, z1 = blk(L, i)
        seed = int(hashf(x0, z0) * 1000)
        # a stone trough: walls with a rounded rim, soil, low planting round the tree
        box(g, x0, x1, y0, y1, z0, z1, F['stone'], T, .03, cell=0, skip=('+y', '-y'))
        r = .14
        for (a0, a1, c0, c1) in ((x0, x1, z0, z0 + r), (x0, x1, z1 - r, z1), (x0, x0 + r, z0 + r, z1 - r), (x1 - r, x1, z0 + r, z1 - r)):
            rect(g, a0, a1, y1, c0, c1, F['coping'])
        rect(g, x0 + r, x1 - r, y1 - .05, z0 + r, z1 - r, F['soil'])
        if T.i < 2:
            for (a, c, n) in (((x0 + r, z0 + r), (x1 - r, z0 + r), (0, 0, 1)), ((x0 + r, z1 - r), (x1 - r, z1 - r), (0, 0, -1)), ((x0 + r, z0 + r), (x0 + r, z1 - r), (1, 0, 0)), ((x1 - r, z0 + r), (x1 - r, z1 - r), (-1, 0, 0))):
                g.face(((a[0], y1 - .05, a[1]), (c[0], y1 - .05, c[1]), (c[0], y1, c[1]), (a[0], y1, a[1])), F['stone'], n)
            n = 14 if T.fine else 7
            for k in range(n):
                px = x0 + .32 + hashf(seed, k) * (x1 - x0 - .64)
                pz = z0 + .32 + hashf(seed, k, 1) * (z1 - z0 - .64)
                if abs(px - (x0 + x1) / 2) < .42 and abs(pz - (z0 + z1) / 2) < .42:
                    continue
                rr = .16 + .1 * hashf(seed, k, 2)
                fl = hashf(seed, k, 3)
                cols = ('flower-pink', 'leaf', 'leaf-dark') if fl > .8 else (('flower-white', 'leaf-light', 'leaf') if fl > .66 else ('leaf-light', 'leaf', 'leaf-dark'))
                blob(g, (px, y1 + .02, pz), rr, T, seed + k, fins(cols), squash=(1, .72, 1), cut=.35, sides=(8, 6, 5)[T.i])
    for i in kit.ids(L, 'skyframe-tree-'):
        x0, x1, y0, y1, z0, z1 = blk(L, i)
        cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
        prof = [(.3, 0), (.22, .12), (.19, .5), (.17, 1.6), (.15, 2.8), (.14, y1 - y0)]
        lathe(g, prof, (cx, y0 - .05, cz), F['bark'], (10, 7, 5)[T.i], cap1=True)
    for i in kit.ids(L, 'skyframe-hedge-'):
        x0, x1, y0, y1, z0, z1 = blk(L, i)
        seed = int(hashf(x0, z0, 2) * 1000)
        box(g, x0 - .04, x1 + .04, y0, y0 + .3, z0 - .04, z1 + .04, F['stone'], T, .02, cell=0, skip=('-y',))
        leafy(g, (x0 + .03, x1 - .03, y0 + .3, y1 - .03, z0 + .03, z1 - .03), T, seed, fins(GREENS), depth=.09)
    for i in kit.ids(L, 'skyframe-greenwall-'):
        x0, x1, y0, y1, z0, z1 = blk(L, i)
        seed = int(hashf(x0, z0, 3) * 1000)
        lx = (x1 - x0) >= (z1 - z0)
        # a steel frame on a stone trough, both faces planted in pockets of leaves and flowers
        box(g, x0 - .05, x1 + .05, y0, y0 + .32, z0 - .05, z1 + .05, F['stone'], T, .02, cell=0, skip=('-y',))
        if lx:
            box(g, x0, x0 + .1, y0 + .32, y1, z0 - .03, z1 + .03, F['steel'], None, 0, cell=0)
            box(g, x1 - .1, x1, y0 + .32, y1, z0 - .03, z1 + .03, F['steel'], None, 0, cell=0)
            box(g, x0, x1, y1 - .08, y1 + .02, z0 - .04, z1 + .04, F['steel'], None, 0, cell=0)
            leafy(g, (x0 + .1, x1 - .1, y0 + .32, y1 - .08, z0 + .07, z1 - .07), T, seed, fins(GREENS), depth=.13, faces=('+z', '-z'), flowers=fins(('flower-pink', 'flower-white', 'leaf-gold')))
        else:
            box(g, x0 - .03, x1 + .03, y0 + .32, y1, z0, z0 + .1, F['steel'], None, 0, cell=0)
            box(g, x0 - .03, x1 + .03, y0 + .32, y1, z1 - .1, z1, F['steel'], None, 0, cell=0)
            box(g, x0 - .04, x1 + .04, y1 - .08, y1 + .02, z0, z1, F['steel'], None, 0, cell=0)
            leafy(g, (x0 + .07, x1 - .07, y0 + .32, y1 - .08, z0 + .1, z1 - .1), T, seed, fins(GREENS), depth=.13, faces=('+x', '-x'), flowers=fins(('flower-pink', 'flower-white', 'leaf-gold')))
    for i in kit.ids(L, 'skyframe-bundle-'):
        x0, x1, y0, y1, z0, z1 = blk(L, i)
        bundle(g, (x0 + x1) / 2, y0, (z0 + z1) / 2, x1 - x0, y1 - y0, z1 - z0, T)


def bundle(g, cx, y0, cz, ln, h, w, T, rows=2, cols=3):
    """A bundle of steel beams lying along x on timber dunnage, strapped: its foot at y0, ln x h x w overall."""
    dun = .08
    bh = (h - dun * rows) / rows
    bw = w / cols - .02
    for r in range(rows):
        y = y0 + dun + r * (bh + dun) + bh / 2
        for c in range(cols):
            z = cz - w / 2 + (c + .5) * w / cols
            off = (hashf(r, c, cx) - .5) * .12
            ibeam(g, (cx - ln / 2 + .04 + off, y, z), (cx + ln / 2 - .04 + off, y, z), bw, bh, F['primer'], T, tf=.03, tw=.025)
        if T.i < 2:
            for k in range(3):
                x = cx + (k - 1) * ln * .36
                box(g, x - .06, x + .06, y - bh / 2 - dun, y - bh / 2, cz - w / 2, cz + w / 2, F['timber'], None, 0, cell=0)
    if T.i < 2:
        for k in (-1, 1):
            x = cx + k * ln * .22
            box(g, x - .04, x + .04, y0 + .02, y0 + h + .012, cz - w / 2 - .012, cz + w / 2 + .012, F['crane-dark'], None, 0, cell=0, skip=('-y',))


# ---------------------------------------------------------------------------------------------------------------------------
# Overhead: the unfinished frame, the core's rise, the crane's mast
# ---------------------------------------------------------------------------------------------------------------------------
def frame(g, L, T):
    S = L['skyframe']
    fr, cr = S['frame'], S['crane']
    cols = fr['columns']
    n = len(cols)
    fy, st, top, rise = fr['y'], fr['storey'], fr['coreTop'], fr['coreRise']
    P = F['primer']
    def corept(c):
        l = max(abs(c['x']), abs(c['z']))
        return (c['x'] * 5.0 / l, c['z'] * 5.0 / l)
    for ci, c in enumerate(cols):
        y1 = fy + (c['rise'] - 1) * st + 1.2
        hsection(g, c['x'], c['z'], top, y1, T, P, .5, .48, abs(c['z']) > abs(c['x']))
        d = cols[(ci + 1) % n]
        for s in range(fr['storeys']):
            y = fy + s * st
            if c['ring'][s]:
                ibeam(g, (c['x'], y, c['z']), (d['x'], y, d['z']), .24, .46, P, T)
            if c['ring'][s] and s < 2 and T.i < 2:
                for hh in (.75, 1.3):
                    rod(g, (c['x'], y + hh, c['z']), (d['x'], y + hh, d['z']), .022, F['paint-yellow'], 4)
                for k in range(1, 4):
                    px, pz = c['x'] + (d['x'] - c['x']) * k / 4, c['z'] + (d['z'] - c['z']) * k / 4
                    rod(g, (px, y + .23, pz), (px, y + 1.3, pz), .022, F['paint-yellow'], 4)
            if c['radial'][s]:
                kx, kz = corept(c)
                ibeam(g, (c['x'], y, c['z']), (kx, y, kz), .2, .4, P, T)
            # a decked bay: corrugated decking on the first floors where both its beams stand
            if s < 2 and c['ring'][s] and c['radial'][s] and d['radial'][s] and hashf(ci, s, 5) < (.55 if s == 0 else .3):
                a, b2 = corept(c), corept(d)
                yd = y + .25
                g.face(((c['x'], yd, c['z']), (d['x'], yd, d['z']), (b2[0], yd, b2[1]), (a[0], yd, a[1])), F['deck'], (0, 1, 0))
                g.face(((c['x'], yd - .04, c['z']), (d['x'], yd - .04, d['z']), (b2[0], yd - .04, b2[1]), (a[0], yd - .04, a[1])), F['deck'], (0, -1, 0))
                if T.i < 2 and hashf(ci, s, 9) < .5:
                    # a tarp over a stack on it
                    mx, mz = (c['x'] + d['x'] + a[0] + b2[0]) / 4, (c['z'] + d['z'] + a[1] + b2[1]) / 4
                    box(g, mx - .8, mx + .8, yd, yd + .7, mz - .6, mz + .6, F['tarp'], T, .06, cell=0, skip=('-y',))
            # debris netting on some bays of the upper storeys
            if T.i < 2 and s >= 1 and c['ring'][s] and s + 1 < fr['storeys'] and c['ring'][s + 1] and hashf(ci, s, 13) < .16:
                ox, oz = (c['x'] + d['x']) / 2, (c['z'] + d['z']) / 2
                l = math.hypot(ox, oz)
                nx, nz = ox / l * .06, oz / l * .06
                g.face(((c['x'] + nx, y + .3, c['z'] + nz), (d['x'] + nx, y + .3, d['z'] + nz), (d['x'] + nx, y + st - .3, d['z'] + nz), (c['x'] + nx, y + st - .3, c['z'] + nz)), F['net'], (ox / l, 0, oz / l))
                g.face(((c['x'] + nx, y + .3, c['z'] + nz), (d['x'] + nx, y + .3, d['z'] + nz), (d['x'] + nx, y + st - .3, d['z'] + nz), (c['x'] + nx, y + st - .3, c['z'] + nz)), F['net'], (-ox / l, 0, -oz / l))
    # the core going on up: concrete to its pour line, starter bars, steel posts above it for the upper beams, a formwork deck
    ct = top + rise
    box(g, -5, 5, top, ct, -5, 5, F['concrete'], T, 0, cell=(2.0, 5, 0)[T.i], skip=('-y',))
    if T.detail:
        for y in (top + 3,):
            for (a, b2, nn) in (((-5, -5), (5, -5), (0, 0, -1)), ((-5, 5), (5, 5), (0, 0, 1)), ((-5, -5), (-5, 5), (-1, 0, 0)), ((5, -5), (5, 5), (1, 0, 0))):
                g.face(((a[0], y - .03, a[1]), (b2[0], y - .03, b2[1]), (b2[0], y + .03, b2[1]), (a[0], y + .03, a[1])), F['concrete-dark'], nn)
        for k in range(36 if T.fine else 16):
            a = k / (36 if T.fine else 16) * 4
            x = -4.8 + a * 9.6 if a < 1 else (4.8 if a < 2 else (4.8 - (a - 2) * 9.6 if a < 3 else -4.8))
            z = -4.8 if a < 1 else (-4.8 + (a - 1) * 9.6 if a < 2 else (4.8 if a < 3 else 4.8 - (a - 3) * 9.6))
            rod(g, (x, ct, z), (x + (hashf(k, 1) - .5) * .08, ct + .8 + hashf(k) * .8, z + (hashf(k, 2) - .5) * .08), .02, F['crane-dark'], 4)
    # the climbing formwork's deck round the pour line and its rail
    if T.i < 2:
        for (a0, a1, c0, c1) in ((-6.1, 6.1, -6.1, -5.05), (-6.1, 6.1, 5.05, 6.1), (-6.1, -5.05, -5.05, 5.05), (5.05, 6.1, -5.05, 5.05)):
            box(g, a0, a1, ct - 1.6, ct - 1.5, c0, c1, F['timber'], None, 0, cell=0)
        for (a, b2) in (((-6.05, -6.05), (6.05, -6.05)), ((6.05, -6.05), (6.05, 6.05)), ((6.05, 6.05), (-6.05, 6.05)), ((-6.05, 6.05), (-6.05, -6.05))):
            rod(g, (a[0], ct - .5, a[1]), (b2[0], ct - .5, b2[1]), .025, F['paint-yellow'], 4)
            rod(g, (a[0], ct - 1.0, a[1]), (b2[0], ct - 1.0, b2[1]), .02, F['paint-yellow'], 4)
            rod(g, (a[0], ct - 1.5, a[1]), (a[0], ct - .5, a[1]), .025, F['paint-yellow'], 4)
    # the crane's mast: four chords, laced, up to the slewing ring
    m, my = cr['mastHalf'], cr['jibY'] - 1.2
    sides = (6, 5, 4)[T.i]
    for sx in (-1, 1):
        for sz in (-1, 1):
            rod(g, (sx * m, ct, sz * m), (sx * m, my, sz * m), .1, F['crane'], sides)
    step = 2.2 if T.i < 2 else 4.4
    y = ct + .4
    k = 0
    while y < my - step * .6:
        y2 = min(my, y + step)
        for sd in (-1, 1):
            a, b2 = (-m, m) if k % 2 == 0 else (m, -m)
            rod(g, (a, y, sd * m), (b2, y2, sd * m), .05, F['crane'], 4)
            rod(g, (sd * m, y, a), (sd * m, y2, b2), .05, F['crane'], 4)
            if T.i < 2:
                rod(g, (-m, y, sd * m), (m, y, sd * m), .04, F['crane'], 4)
                rod(g, (sd * m, y, -m), (sd * m, y, m), .04, F['crane'], 4)
        y = y2
        k += 1
    lathe(g, [(1.5, 0), (1.6, .25), (1.6, .9), (1.75, 1.06)], (0, my, 0), F['crane-dark'], (16, 10, 8)[T.i], cap1=True)


def overhead(g, L, T):
    """What hangs over the garden, above head height: strings of lights under the gallery from column to column, hanging
    troughs along the gallery's edge with vines down to just over 2 m, a glass canopy over each pavilion door."""
    cols = L['skyframe']['frame']['columns']
    n = len(cols)
    for ci, c in enumerate(cols):
        d = cols[(ci + 1) % n]
        # the string hangs between the columns' inner faces, sagging to 2.5 m
        seg = (14, 8, 4)[T.i]
        pts = []
        for k in range(seg + 1):
            t = k / seg
            pts.append((c['x'] + (d['x'] - c['x']) * t, 3.05 - .5 * math.sin(math.pi * t), c['z'] + (d['z'] - c['z']) * t))
        for a, b in zip(pts, pts[1:]):
            rod(g, a, b, .012, F['cable'], 3)
        if T.i < 2:
            for k in range(1, seg):
                p = pts[k]
                blob(g, (p[0], p[1] - .07, p[2]), .055, T, k, (F['lamp'], F['lamp'], F['lamp']), lumps=0, sides=5)
    if T.i < 2:
        for side in range(4):
            for k in range(-2, 3):
                a = k * 2.6 + (.6 if side % 2 else -.6)
                x, z, nx, nz = ((-9.0, a, -1, 0), (9.0, a, 1, 0), (a, -9.0, 0, -1), (a, 9.0, 0, 1))[side]
                if hashf(side, k, 77) < .25:
                    continue
                ux, uz = abs(nz), abs(nx)
                # a trough hung under the edge beam, leaves spilling over it, vines down to 2.15 m
                box(g, x + nx * .04 - ux * .55 - (.12 if nx else 0), x + nx * .04 + ux * .55 + (.12 if nx else 0), 2.86, 3.14, z + nz * .04 - uz * .55 - (.12 if nz else 0), z + nz * .04 + uz * .55 + (.12 if nz else 0), F['stone'], T, .03, cell=0, skip=('+y',))
                for j in range(5 if T.fine else 3):
                    o = (j - (2 if T.fine else 1)) * (.24 if T.fine else .4)
                    ln2 = .25 + .5 * hashf(side, k, j)
                    w = .09 + .05 * hashf(side, k, j, 1)
                    f = F[('leaf', 'leaf-light', 'leaf-dark')[j % 3]]
                    px, pz = x + nx * .18 + ux * o, z + nz * .18 + uz * o
                    g.face(((px - ux * w, 2.86 - ln2, pz - uz * w), (px + ux * w, 2.86 - ln2, pz + uz * w), (px + ux * w * 1.3, 3.1, pz + uz * w * 1.3), (px - ux * w * 1.3, 3.1, pz - uz * w * 1.3)), f, (nx, 0, nz))
                    g.face(((px - ux * w, 2.86 - ln2, pz - uz * w), (px + ux * w, 2.86 - ln2, pz + uz * w), (px + ux * w * 1.3, 3.1, pz + uz * w * 1.3), (px - ux * w * 1.3, 3.1, pz - uz * w * 1.3)), f, (-nx, 0, -nz))
                blob(g, (x + nx * .04, 3.13, z + nz * .04), .34, T, side * 10 + k + 20, fins(('leaf-light', 'leaf', 'leaf-dark')), squash=(1.5 if ux else 1, .45, 1.5 if uz else 1), sides=(8, 6, 5)[T.i])
    for i in kit.ids(L, 'skyframe-pavilion-'):
        if '-head-' not in i or T.i == 2:
            continue
        x0, x1, y0, y1, z0, z1 = blk(L, i)
        lx = (x1 - x0) >= (z1 - z0)
        # outward: away from the pavilion's middle
        pc = (-20.0, -11.3) if x0 < 0 else (20.0, 11.3)
        if lx:
            s = 1 if (z0 + z1) / 2 > pc[1] else -1
            zf = z1 if s > 0 else z0
            box(g, x0 - .2, x1 + .2, y0 + .28, y0 + .32, min(zf, zf + s * 1.3), max(zf, zf + s * 1.3), F['glass'], None, 0, cell=0)
            for x in (x0 - .2, x1 + .2):
                beam(g, (x, y0 + .25, zf), (x, y0 + .25, zf + s * 1.3), .05, .1, F['steel'])
                rod(g, (x, y1 + .5, zf), (x, y0 + .3, zf + s * 1.2), .012, F['stainless'], 3)
            beam(g, (x0 - .2, y0 + .25, zf + s * 1.3), (x1 + .2, y0 + .25, zf + s * 1.3), .05, .1, F['steel'])
        else:
            s = 1 if (x0 + x1) / 2 > pc[0] else -1
            xf = x1 if s > 0 else x0
            box(g, min(xf, xf + s * 1.3), max(xf, xf + s * 1.3), y0 + .28, y0 + .32, z0 - .2, z1 + .2, F['glass'], None, 0, cell=0)
            for z in (z0 - .2, z1 + .2):
                beam(g, (xf, y0 + .25, z), (xf + s * 1.3, y0 + .25, z), .05, .1, F['steel'])
                rod(g, (xf, y1 + .5, z), (xf + s * 1.2, y0 + .3, z), .012, F['stainless'], 3)
            beam(g, (xf + s * 1.3, y0 + .25, z0 - .2), (xf + s * 1.3, y0 + .25, z1 + .2), .05, .1, F['steel'])


def static_scene(L, T):
    g = Geo()
    kit.LAYER[0] = sk.LIFT_TIER[T.i]
    garden(g, L, T)
    core(g, L, T)
    columns(g, L, T)
    decks(g, L, T)
    rails(g, L, T)
    stairs(g, L, T)
    pavilions(g, L, T)
    planting(g, L, T)
    frame(g, L, T)
    overhead(g, L, T)
    import skyframe_props as props
    props.fixed(g, L, T)
    return g


VIEWS = [('wide-sw', (-40, 24, -36), (0, 3, 0), 55), ('wide-ne', (38, 20, 34), (0, 4, 0), 55), ('base-w', (-24.5, 1.7, 1.2), (-6, 2.6, 0), 75), ('north-lane', (-12, 1.7, 13), (10, 2.2, 11), 72),
         ('lobby-n', (-3, 1.6, 9.2), (-3, 1.5, 2), 70), ('gallery', (-8, 5.3, 7), (6, 5, 7.5), 75), ('gallery-up', (-7, 5.2, -7), (2, 22, 2), 80), ('roof-w', (-25, 5.3, -15), (-14, 4.6, -7), 75),
         ('pavilion-in', (-24.5, 1.6, -14.5), (-15, 1.6, -9), 80), ('bridge', (-16, 5.2, -7.3), (-8, 4.8, -7.3), 70), ('terrace-n', (-17, -1.6, 20.5), (2, -2.4, 19), 75),
         ('edge', (-27.5, 1.2, 12), (-20, .4, 2), 70), ('under', (-10, -9, 30), (-6, -3, 16), 60), ('top', (0, 60, -.01), (0, 0, 0), 60), ('frame', (22, 9, -20), (0, 20, 0), 60),
         ('greenwall', (-14.4, 1.5, 3.6), (-12.3, 1.4, 0), 65), ('hedge', (-19, 1.4, 1.5), (-17, .6, 0), 60), ('tree', (-19, 1.7, 9), (-22.4, 2.6, 12.6), 65)]


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--layout', required=True)
    ap.add_argument('--out', help='Assets/LocalAssets to write the bank into')
    ap.add_argument('--preview', help='render views of the baked tier')
    ap.add_argument('--preview-tier', type=int, default=0)
    ap.add_argument('--views', default='')
    ap.add_argument('--tiers', default='0,1,2')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--static-only', action='store_true')
    ap.add_argument('--no-sky', action='store_true')
    args = ap.parse_args(argv)
    L = kit.index(sk.load_layout(args.layout))
    kit.finishes()
    import skyframe_props as props
    import skyframe_sky as sky
    sb = sk.StageBank()
    chunks = sk.Chunks(12.0, (-36.0, -24.0), (-1.0, 9.0))
    t0 = time.time()
    for ti in [int(x) for x in args.tiers.split(',')]:
        T = sk.Tier(ti)
        t = time.time()
        g = static_scene(L, T)
        print('  coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % sk.lift_coplanar(g, eps=sk.NEAR_TIER[ti], lift=sk.LIFT_TIER[ti]))
        # no face reaches out of its chunk's cell: a floor is never drawn twice where neighbouring chunks draw at different tiers
        print('  chunks: %d faces cut along the cells' % chunks.split(g))
        print('tier %s: static %d faces, %.1f s' % (T.name, g.faces, time.time() - t))
        ao = None
        if not args.no_ao:
            # an open-air stage: the occlusion reaches a metre and is eased (the anime style bands it hard)
            ao = 1.0 - (1.0 - sk.bake_ao([g], distance=(1.1, 1.1, 1.3)[ti], samples=(32, 16, 12)[ti], occluders=props.occluders(L, T))[0]) * .7
        bk = sk.Bake(g, ao, floors=FLOORS, seed=11)
        ids = chunks.ids(g)
        for cid in np.unique(ids):
            sb.add('static/%d/%d' % (cid, ti), 'static', sk.to_mesh('static/%d/%d' % (cid, ti), g, bk, ids == cid))
        if args.preview and ti == args.preview_tier:
            views = [v for v in VIEWS if not args.views or v[0] in args.views.split(',')]
            extra = []
            if not args.no_sky:
                for name, fg in sky.far(L):
                    extra.append((fg, sk.Bake(fg, None, floors=(), seed=5)))
                views = views + [v for v in sky.VIEWS if not args.views or v[0] in args.views.split(',')]
            if not args.views or 'rig' in args.views:
                row = Geo()
                for k, (name, pg, rest, place) in enumerate(props.items(L, T)):
                    row.extend(pg.transformed([place[3], 0, 0, place[0], 0, place[3], 0, 400.0 + place[1], 0, 0, place[3], place[2]]))
                extra.append((row, sk.Bake(row, None, floors=(), seed=7)))
                views = views + props.VIEWS
            sk.preview([(g, bk)] + extra, views, args.preview)
        if not args.static_only:
            props.bank_props(sb, L, T, args.no_ao)
        del g
    if not args.no_sky and not args.static_only:
        sky.bank_far(sb, L)
    if args.out:
        for b in L['blocks']:
            sb.cover(b['id'])
        sb.write(args.out, BANK)
    else:
        for (group, tier), (n, tris, verts) in sorted(sb.stats.items()):
            print('bank %-7s %-5s %4d props %8d triangles %8d vertices' % (group, tier, n, tris, verts))
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
