"""Breakwater's art (docs/arenas/BREAKWATER.md, docs/arenas/STAGE-ART.md): the storm-battered granite mole modelled in Blender on
the map's own blocks, into the stage bank Resources/ChipArenaStageBreakwater (stagekit.py's format and bake). Runtime
NativeStageArt draws it in place of the boxes; without the bank the box kit draws.

usage (from unity/ChipArenaConversion):
  dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout breakwater LAYOUT.json
  blender --background --factory-startup --python tools/stages/breakwater_art.py -- --layout LAYOUT.json --out ASSETS/LocalAssets
          [--preview DIR] [--views a,b] [--preview-tier N] [--tiers 0,1,2] [--no-ao] [--static-only] [--no-far] [--stats]

This file: the mole (the deck's setts and puddles, the wave wall with its recurved sea face, buttresses and blind arches, the
storm parapet and its breaches, the harbour quay with its fenders and ladders, the bastion, the pulpit, the watch turret with
its belfry and slate roof, the Mariners' Stone and its obelisk, the harbour gate and the lighthouse's hall, the stairs, the
quarried blocks, the upturned boats, bollards and lamp posts) and the armour stones at the wall's foot.
breakwater_props.py: the breakable props and the rig (the optic, lanterns, the bell, the swells, the comber and the wash, the buoy,
the boats, the gulls, the flags). breakwater_far.py: the sea, the lighthouse, the mole running to the shore and the harbour town.
breakwater_kit.py: the finishes and helpers.
"""
import argparse
import math
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, sweep, lathe, grid_face  # noqa: E402
import breakwater_kit as bk  # noqa: E402
from breakwater_kit import F, Y, Face, ext, hashf  # noqa: E402

BANK = 'ChipArenaStageBreakwater'
SEA, LQ, L0, LW = -3.4, -2.4, 0.0, 2.4
FLOORS = (SEA, LQ, L0, LW)
WALL0, WALL1, QUAY0 = 5.0, 8.4, -9.5
END = 16.5                                    # the end buildings' fronts stand at x +-16.5

PLINTH = [(0, -.01), (0, .09), (.26, .09), (.34, .03), (.34, -.01)]
STRING = [(.1, -.01), (.1, .05), (.06, .09), (-.02, .09), (-.08, .04), (-.1, -.01)]
CORNICE = [(0, -.01), (0, .2), (-.08, .2), (-.12, .16), (-.2, .14), (-.3, .07), (-.4, .04), (-.46, -.01)]


def FX(xa, xb, z, s):
    """The face along x from xa to xb at z, turned toward s * z; fc.at(x) is the distance along it."""
    fc = Face((xa if s > 0 else xb, 0, z), (0, 0, s), xb - xa)
    fc.at = (lambda x: x - xa) if s > 0 else (lambda x: xb - x)
    return fc


def FZ(za, zb, x, s):
    """The face along z from za to zb at x, turned toward s * x."""
    fc = Face((x, 0, zb if s > 0 else za), (s, 0, 0), zb - za)
    fc.at = (lambda z: zb - z) if s > 0 else (lambda z: z - za)
    return fc


def span(fc, lo, hi):
    a, b = fc.at(lo), fc.at(hi)
    return (a, b) if a < b else (b, a)


def panel(g, fc, lo, hi, y0, y1, f, T, d=0.0):
    """A flat wall panel on a face between two world coordinates along it, cut to the tier's cells."""
    a0, a1 = span(fc, lo, hi)
    if a1 - a0 < 1e-6 or y1 - y0 < 1e-6:
        return
    o = fc.P(a0, y0, d)
    grid_face(g, o, sk._sub(fc.P(a1, y0, d), o), (0, y1 - y0, 0), 0, 0, f, fc.n, max(T.cell, 1.2))


def floor(g, x0, x1, z0, z1, y, f, T):
    if x1 - x0 < 1e-6 or z1 - z0 < 1e-6:
        return
    grid_face(g, (x0, y, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, f, Y, T.cell)


def cope(g, fc, lo, hi, top, T, seed, depth=.38, over=.07, h=.2, **k):
    """Coping stones along a face's top edge between two world coordinates, their tops at `top`."""
    a0, a1 = span(fc, lo, hi)
    if a1 - a0 > .2:
        bk.coping_run(g, fc, a0, a1, top - h + .02, depth, T, seed, over=over, h=h, **k)


def ashlar(g, fc, lo, hi, y0, y1, T, seed, **k):
    a0, a1 = span(fc, lo, hi)
    if a1 - a0 > .3 and y1 - y0 > .3:
        bk.courses(g, fc, a0, a1, y0, y1, T, seed, **k)


# ---------------------------------------------------------------------------------------------------------------------------
# The mole: deck, wave wall, quay, bastion, pulpit
# ---------------------------------------------------------------------------------------------------------------------------
def deck(g, L, T):
    # the setts, a runner of larger slabs down the middle, the halls' own floors; puddles that hold the lamps' light
    for z0, z1, f in ((-4.62, -.9, 'sett'), (-.9, .9, 'walk'), (.9, 5.0, 'sett')):
        floor(g, -22, 22, z0, z1, L0, F[f], T)
    for i in range((9, 6, 0)[T.i]):
        cx, cz, r = (hashf(71, i) - .5) * 30, (hashf(71, i, 1) - .5) * 7.4 - .4, .5 + hashf(71, i, 2) * .7
        if abs(cx) < 2.2 and abs(cz) < 2.4:
            continue
        n = 9
        g.face([(cx + math.cos(2 * math.pi * k / n) * r * (1 + .5 * hashf(71, i, k + 9)), L0, cz + math.sin(2 * math.pi * k / n) * r * (.6 + .3 * hashf(71, i, k + 29))) for k in range(n)], F['puddle'], Y)
    s = FX(-22, 22, -5.0, -1)
    for lo, hi in ((-15.0, -2.6), (2.6, 15.0)):
        cope(g, s, lo, hi, L0 + .03, T, 3 + int(lo), missing=.04)
    # the deck's harbour face over the quay
    for lo, hi in ((-22, -2.6), (2.6, 22)):
        panel(g, s, lo, hi, LQ, L0 - .17 if abs(lo) < END and abs(hi) < END else L0, F['granite'], T)
        ashlar(g, s, max(lo, -15) if lo < 0 else lo, hi if hi < 0 else min(hi, 15), LQ + .05, L0 - .2, T, 11 + int(lo))
    for x in (-9.2, -4.2, 4.2, 9.2):
        bk.ring(g, (x, -.7, -5.0), (0, 0, -1), .14, .022, F['rust'], T, hang=.1)
    # the mole's ends (seen only from outside)
    for sx in (-1, 1):
        e = FZ(-5, 5, sx * 22, sx)
        panel(g, e, -5, 5, -7, L0, F['granite'], T)


def sea_wall(g, T):
    """The wave wall's seaward face, the whole length of the mole: a recurved profile from the toe under the sea to the lip
    under the walk, weed below the tide line."""
    prof = [(2.7, -7.0), (2.25, -4.7), (1.75, -3.95), (1.2, -3.05), (.8, -1.9), (.54, -.5), (.44, .7), (.5, 1.4), (.66, 1.9), (.76, 2.12), (.72, 2.26), (.0, 2.36)]
    if T.i == 2:
        prof = [prof[0], prof[2], prof[4], prof[6], prof[9], prof[11]]
    xs = np.arange(-22, 22.01, (2.0, 4.0, 11.0)[T.i])
    P = np.zeros((len(xs), len(prof), 3))
    for i, x in enumerate(xs):
        for j, (o, y) in enumerate(prof):
            wob = 0 if (j == len(prof) - 1 or T.i == 2) else (hashf(81, i, j) - .5) * .05
            P[i, j] = (x, y, WALL1 + o + wob)
    ys = [(prof[j][1] + prof[j + 1][1]) / 2 for j in range(len(prof) - 1)]
    bk.grid_surface(g, P, F['granite'], inside=(0, -2, 0), fin_fn=lambda i, j: F['armourwet'] if ys[j] < SEA + .5 else F['granite'])
    # the armour stones heaped at its toe, awash
    n = (58, 40, 22)[T.i]
    for i in range(n):
        x = -23.5 + 47 * (i + hashf(83, i)) / n
        z = WALL1 + 1.7 + hashf(83, i, 1) * 2.4
        s = .7 + hashf(83, i, 2) * .8
        q = sk.quat_axis(hashf(83, i, 3) - .5, hashf(83, i, 4) - .5, hashf(83, i, 5) - .5, hashf(83, i, 6) * 6)
        bk.rock(g, (x, SEA - .15 + hashf(83, i, 7) * .5, z), (s * 1.25, s * .8, s), 900 + i, F['armourwet' if i % 3 else 'armour'], n=2 if T.i == 0 else 1, square=.7, rough=.22, quat=q)
    # the ends' faces
    for sx in (-1, 1):
        panel(g, FZ(WALL0, WALL1, sx * 22, sx), WALL0, WALL1, -7, LW, F['granite'], T)


def wall(g, b, T, seed):
    x0, x1, y0, y1, z0, z1 = ext(b)
    west = x0 < 0
    # the walk
    floor(g, x0, x1, z0 + .34, z1, LW, F['walk'], T)
    s = FX(x0, x1, z0, -1)
    lo, hi = (max(x0, -END + .4), x1 - 1.7) if west else (x0 + 1.7, min(x1, END - .4))
    cope(g, s, lo, hi, LW + .03, T, seed, depth=.34, over=.06)
    # its deck face: a plinth, buttress piers and blind arches along the open stretch between the two flights, ashlar over them
    vis0, vis1 = (-END, -3.0) if west else (3.0, END)
    panel(g, s, vis0, vis1, L0, LW - .17, F['granite'], T)
    a0, a1 = span(s, vis0, vis1)
    if T.i < 2:
        s.mould(g, PLINTH, L0, a0, a1, F['dressed'], T)
        s.mould(g, STRING, LW - .32, a0, a1, F['coping'], T)
    piers = [-12.3, -10.2, -8.1, -6.9] if west else [6.9, 8.1, 10.2, 12.3]
    for k, x in enumerate(piers):
        wide = .5 if k in ((3,) if west else (0,)) else .62
        a = s.at(x)
        s.box(g, a - wide / 2, a + wide / 2, L0, LW - .45, 0, .2, F['dressed'], T, bevel=.03, skip=('+z',))
        if T.i < 2:
            g.face((s.P(a - wide / 2, LW - .45, .2), s.P(a + wide / 2, LW - .45, .2), s.P(a + wide / 2, LW - .2, 0), s.P(a - wide / 2, LW - .2, 0)), F['coping'], normal=(0, .6, -.8))
    for k, (xa, xb) in enumerate(zip(piers[:2], piers[1:3])):
        s.arch(g, s.at((xa + xb) / 2), .62, L0 + .34, 1.15, .62, .008, F['deep'], F['dressed'], T)
        if k == (0 if west else 1):
            bk.lifebuoy(g, s, s.at((xa + xb) / 2), 1.12, T)             # a lifebuoy in the bay nearer the end
        elif T.i < 2:
            bk.ring(g, ((xa + xb) / 2, .95, z0), (0, 0, -1), .12, .02, F['rust'], T, hang=.08)
    ashlar(g, s, vis0, vis1, LW - .9, LW - .36, T, seed + 5, course=.54)


def parapet(g, b, blocks, T, seed):
    """A length of the storm parapet: a thick wall under a saddleback coping; an end that meets nothing stands broken in
    toothed courses (the sea took the rest)."""
    x0, x1, y0, y1, z0, z1 = ext(b)

    def meets(x):
        return any(o is not b and ext(o)[0] - 1e-3 <= x <= ext(o)[1] + 1e-3 and ext(o)[3] > y0 + .5 and ext(o)[4] < z1 and ext(o)[5] > z0 for o in blocks)
    lo_broken, hi_broken = not meets(x0 - .05), not meets(x1 + .05)
    j0, j1 = (.7 if lo_broken else 0), (.7 if hi_broken else 0)
    box(g, x0 + j0, x1 - j1, y0, y1 - .16, z0 + .02, z1 - .02, F['granite'], T, bevel=.02, skip=('-y',))
    # the coping: a saddleback over the wall, stone by stone
    n = max(1, int(round((x1 - j1 - x0 - j0) / 1.3)))
    for i in range(n if T.i < 2 else 1):
        m = n if T.i < 2 else 1
        a, c = x0 + j0 + (x1 - j1 - x0 - j0) * i / m + .012, x0 + j0 + (x1 - j1 - x0 - j0) * (i + 1) / m - .012
        zc = (z0 + z1) / 2
        sec = [(-.25, 0), (-.25, .07), (-.12, .15), (0, .18), (.12, .15), (.25, .07), (.25, 0)]
        sweep(g, [(u, v) for u, v in sec], [(a, y1 - .18, zc), (c, y1 - .18, zc)], F['coping'], up=Y, smooth_u=T.i == 0)
    courses_n = 3
    for end, j, broken in ((0, j0, lo_broken), (1, j1, hi_broken)):
        if not broken:
            continue
        for k in range(courses_n if T.i < 2 else 1):
            m = courses_n if T.i < 2 else 1
            v0, v1 = y0 + (y1 - .16 - y0) * k / m, y0 + (y1 - .16 - y0) * (k + 1) / m
            reach = j * (.2 + .75 * (1 - (k + .5) / m) * (.6 + .6 * hashf(seed, end, k)))
            if end == 0:
                box(g, x0 + j - reach, x0 + j + .02, v0, v1 - .02 * (k < m - 1), z0 + .03, z1 - .03, F['rough'], None, bevel=.03 if T.i == 0 else 0)
            else:
                box(g, x1 - j - .02, x1 - j + reach, v0, v1 - .02 * (k < m - 1), z0 + .03, z1 - .03, F['rough'], None, bevel=.03 if T.i == 0 else 0)
        if T.i == 0:
            ex = x0 + .2 if end == 0 else x1 - .2
            for i in range(5):
                s = .06 + hashf(seed, end, i, 5) * .06
                bk.rock(g, (ex + (hashf(seed, end, i, 6) - .5) * .5, y0 + s * .5, z0 + .05 + hashf(seed, end, i, 7) * .28), (s * 1.3, s * .6, s), seed * 7 + i, F['rough'], n=1, square=.6, rough=.2)
    ashlar(g, FX(x0 + j0, x1 - j1, z0 + .02, -1), x0 + j0, x1 - j1, y0 + .02, y1 - .2, T, seed, course=.31, d=.01)


def quay(g, b, T, seed):
    x0, x1, y0, y1, z0, z1 = ext(b)
    floor(g, x0, x1, z0 + .36, z1, LQ, F['quay'], T)
    s = FX(x0, x1, z0, -1)
    cope(g, s, x0 + (.6 if x0 <= -21.9 else 0), x1 - (.6 if x1 >= 21.9 else 0), LQ + .03, T, seed, depth=.36, over=.08, missing=.03)
    panel(g, s, x0, x1, -7, LQ - .17, F['granite'], T)
    ashlar(g, s, x0, x1, SEA + .3, LQ - .2, T, seed + 3, course=.6)
    x = x0 + 1.4
    k = 0
    while x < x1 - .6:
        bk.fender(g, s, s.at(x), SEA - .9, LQ - .12, T)
        if k % 3 == 1 and T.i < 2:
            bk.ring(g, (x + 1.5, LQ - .55, z0), (0, 0, -1), .13, .02, F['rust'], T, hang=.1)
        x += 3.0
        k += 1
    if abs(x1 - x0) > 10:
        bk.ladder(g, s, s.at(-12.4 if x0 < 0 else 12.4), SEA - .5, LQ - .1, T)
    for sx in (-1, 1):
        if abs(sx * 22 - (x0 if sx < 0 else x1)) < 1e-3:
            panel(g, FZ(z0, z1, sx * 22, sx), z0, z1, -7, LQ, F['granite'], T)


def bastion(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    floor(g, x0, x1, z0 + .4, z1, L0, F['sett'], T)
    s = FX(x0, x1, z0, -1)
    cope(g, s, x0, x1, L0 + .03, T, 41, depth=.42, over=.08, h=.24)
    panel(g, s, x0, x1, LQ, L0 - .2, F['granite'], T)
    ashlar(g, s, x0, x1, LQ + .05, L0 - .24, T, 43)
    bk.ring(g, (0, -.85, z0), (0, 0, -1), .2, .03, F['rust'], T, hang=.14)
    for sx in (-1, 1):
        e = FZ(z0, z1, sx * (x1 if sx > 0 else -x0), sx)
        cope(g, e, z0 + .4, z1, L0 + .03, T, 45 + sx, depth=.4, over=.08, h=.24)
        panel(g, e, z0, z1, LQ, L0 - .2, F['granite'], T)
        ashlar(g, e, z0, z1, LQ + .05, L0 - .24, T, 47 + sx)
    # a brass compass rose let into the setts, a coil of rope
    if T.i < 2:
        cz = (z0 + z1) / 2 - .1
        for k in range(8):
            a = k * math.pi / 4
            l = 1.0 if k % 2 == 0 else .6
            pt = lambda ang, r: (math.sin(ang) * r, L0, cz + math.cos(ang) * r)
            for sd in (-1, 1):
                g.face(((0, L0, cz), pt(a, l), pt(a + sd * math.pi / 8, .24)), F['brass' if (k % 2 == 0) == (sd > 0) else 'bronze'], Y)
        seg = (28, 14)[T.i]
        for k in range(seg):
            a0, a1 = 2 * math.pi * k / seg, 2 * math.pi * (k + 1) / seg
            g.face(((math.sin(a0) * 1.12, L0, cz + math.cos(a0) * 1.12), (math.sin(a1) * 1.12, L0, cz + math.cos(a1) * 1.12), (math.sin(a1) * 1.2, L0, cz + math.cos(a1) * 1.2), (math.sin(a0) * 1.2, L0, cz + math.cos(a0) * 1.2)), F['brass'], Y)
        bk.rope_coil(g, 1.75, L0, z0 + 1.0, .34, T)


def pulpit(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    floor(g, x0, x1, z0 + .34, z1, LW, F['walk'], T)
    s = FX(x0, x1, z0, -1)
    cope(g, s, x0, x1, LW + .03, T, 51, depth=.36, over=.08, h=.24)
    panel(g, s, x0, x1, L0, LW - .2, F['ashlar'], T)
    a0, a1 = span(s, x0, x1)
    if T.i < 2:
        s.mould(g, PLINTH, L0, a0, a1, F['dressed'], T)
        s.mould(g, STRING, LW - .36, a0, a1, F['coping'], T)
    for k, x in enumerate((-1.95, 0.0, 1.95)):
        s.arch(g, s.at(x), .66, L0 + .34, 1.1, .66, .008, F['deep'], F['dressed'], T)
        if k == 1:
            # the harbour board's bronze: a plaque with a ring of rope
            s.box(g, s.at(x) - .42, s.at(x) + .42, .62, 1.5, 0, .05, F['bronze'], T, bevel=.012, skip=('+z',))
            if T.i < 2:
                s.box(g, s.at(x) - .34, s.at(x) + .34, .7, 1.42, .05, .062, F['brass'], T, bevel=.006, skip=('+z',))
        elif T.i < 2:
            bk.ring(g, (x, 1.25, z0), (0, 0, -1), .13, .02, F['rust'], T, hang=.1)
    for x in (-2.93, -.98, .98, 2.93):
        a = s.at(x)
        s.box(g, a - .13, a + .13, L0 + .34, LW - .44, 0, .1, F['dressed'], T, bevel=.02, skip=('+z',))


# ---------------------------------------------------------------------------------------------------------------------------
# The watch turret and the Mariners' Stone
# ---------------------------------------------------------------------------------------------------------------------------
def slit(g, fc, a, y0, y1, T, w=.2, f='window'):
    """A lit slit window on a face: the glow sunk behind a dressed surround."""
    fc.quad(g, a - w / 2, a + w / 2, y0, y1, .012, F[f])
    if T.i < 2:
        for o in (-1, 1):
            fc.box(g, a + o * (w / 2 + .05) - .05, a + o * (w / 2 + .05) + .05, y0 - .06, y1 + .06, 0, .05, F['dressed'], T, bevel=.012, skip=())
        fc.box(g, a - w / 2 - .1, a + w / 2 + .1, y1, y1 + .12, 0, .07, F['dressed'], T, bevel=.012)
        fc.box(g, a - w / 2 - .12, a + w / 2 + .12, y0 - .1, y0, 0, .09, F['coping'], T, bevel=.012)
        fc.box(g, a - .012, a + .012, y0, y1, .012, .03, F['ironplain'], T, bevel=0)


def quoins(g, e, y0, y1, T, f='dressed', proud=.035, corners=((1, 1), (1, -1), (-1, 1), (-1, -1))):
    """Corner stones up an extent's corners, long and short by turns, a hair proud."""
    if T.i == 2:
        return
    x0, x1, _, _, z0, z1 = e
    n = max(1, int(round((y1 - y0) / .42)))
    h = (y1 - y0) / n
    for sx, sz in corners:
        cx, cz = (x1 if sx > 0 else x0), (z1 if sz > 0 else z0)
        for k in range(n):
            lx, lz = (.5, .28) if k % 2 == 0 else (.28, .5)
            xa, xb = sorted((cx + sx * proud, cx - sx * lx))
            za, zb = sorted((cz + sz * proud, cz - sz * lz))
            box(g, xa, xb, y0 + h * k + .012, y0 + h * (k + 1) - .012, za, zb, F[f], None, bevel=.022 if T.i == 0 else 0)


def turret(g, b, T, R):
    x0, x1, y0, y1, z0, z1 = ext(b)
    e = (x0, x1, y0, y1, z0, z1)
    cx, cz = 0.0, (z0 + z1) / 2
    s, n, w, ea = FX(x0, x1, z0, -1), FX(x0, x1, z1, 1), FZ(z0, z1, x0, -1), FZ(z0, z1, x1, 1)
    panel(g, s, x0, x1, LW, y1, F['ashlar'], T)
    panel(g, n, x0, x1, LW - .1, y1, F['ashlar'], T)
    panel(g, w, z0, z1, LW, y1, F['ashlar'], T)
    panel(g, ea, z0, z1, LW, y1, F['ashlar'], T)
    quoins(g, e, LW, y1 - .3, T)
    for fc, lo, hi in ((s, x0, x1), (n, x0, x1), (w, z0, z1), (ea, z0, z1)):
        a0, a1 = span(fc, lo, hi)
        if T.i < 2:
            fc.mould(g, STRING, 4.45, a0 + .04, a1 - .04, F['coping'], T)
    # the door from the pulpit: boards under a round head, iron straps, a lamp over it
    s.arch(g, s.at(0), .52, LW, LW + 1.35, .52, .01, F['door'], F['dressed'], T, trim=.16, proud=.08)
    if T.i < 2:
        for y in (LW + .35, LW + 1.2):
            s.box(g, s.at(0) - .5, s.at(0) + .32, y - .035, y + .035, .01, .03, F['ironplain'], T, bevel=.006)
        bk.ring(g, (-.28, LW + .82, z0 - .02), (0, 0, -1), .06, .012, F['brass'], T)
    for x in (-.95, .95):
        slit(g, s, s.at(x), 4.9, 5.75, T)
    slit(g, w, w.at(cz - .3), 4.75, 5.5, T)
    slit(g, ea, ea.at(cz + .3), 4.75, 5.5, T)
    slit(g, n, n.at(0), 4.6, 5.5, T, f='glass')
    # the corbel table and, over it, the open belfry: four piers, round-headed openings, the bell's beam
    top = y1
    for fc, lo, hi in ((s, x0, x1), (n, x0, x1), (w, z0, z1), (ea, z0, z1)):
        a0, a1 = span(fc, lo, hi)
        fc.box(g, a0 - .2, a1 + .2, top - .2, top, 0, .2, F['coping'], T, bevel=.03)
        if T.i < 2:
            a = a0 + .25
            while a < a1 - .1:
                fc.box(g, a - .09, a + .09, top - .44, top - .2, 0, .15, F['dressed'], T, bevel=.02)
                a += .5
    floor(g, x0, x1, z0, z1, top, F['lead'], T)
    py1 = top + 1.9
    for sx in (-1, 1):
        for sz in (-1, 1):
            px, pz = cx + sx * 1.3, cz + sz * 1.4
            box(g, px - .3, px + .3, top, py1, pz - .3, pz + .3, F['ashlar'], T, bevel=.03, skip=('-y', '+y'))
    # the heads over the openings: a lintel course with the arch cut under it
    seg = (8, 5, 2)[T.i]
    for fc, half, d in ((FX(-1.6, 1.6, cz - 1.7, -1), 1.0, .0), (FX(-1.6, 1.6, cz + 1.7, 1), 1.0, .0), (FZ(cz - 1.7, cz + 1.7, -1.6, -1), 1.1, .0), (FZ(cz - 1.7, cz + 1.7, 1.6, 1), 1.1, .0)):
        mid = fc.L / 2
        ys = py1 - .62
        pts = [(mid - half * math.cos(math.pi * k / seg), ys + .5 * math.sin(math.pi * k / seg)) for k in range(seg + 1)]
        for inner in (0.0, -.6):
            for k in range(seg):
                (a0, h0), (a1, h1) = pts[k], pts[k + 1]
                nn = fc.n if inner == 0 else (-fc.n[0], 0, -fc.n[2])
                g.face((fc.P(a0, h0, inner), fc.P(a1, h1, inner), fc.P(a1, py1, inner), fc.P(a0, py1, inner)), F['ashlar'], nn)
        for k in range(seg):
            (a0, h0), (a1, h1) = pts[k], pts[k + 1]
            g.face((fc.P(a0, h0, 0), fc.P(a1, h1, 0), fc.P(a1, h1, -.6), fc.P(a0, h0, -.6)), F['dressed'], normal=(-fc.u[0] * (h1 - h0) * (1 if a0 < mid else 1), -abs(a1 - a0) - 1e-3, -fc.u[2] * (h1 - h0)))
    # the bell's beam and yoke (the bell is the rig's)
    box(g, -1.3, 1.3, R['bellY'] + .02, R['bellY'] + .2, R['bellZ'] - .09, R['bellZ'] + .09, F['timber'], T, bevel=.02)
    # the cornice and the slate roof: a hipped cap with a bell-cast foot, a finial and its vane
    ch = py1
    for fc, lo, hi in ((FX(-1.6, 1.6, cz - 1.7, -1), -1.6, 1.6), (FX(-1.6, 1.6, cz + 1.7, 1), -1.6, 1.6), (FZ(cz - 1.7, cz + 1.7, -1.6, -1), cz - 1.7, cz + 1.7), (FZ(cz - 1.7, cz + 1.7, 1.6, 1), cz - 1.7, cz + 1.7)):
        a0, a1 = span(fc, lo, hi)
        fc.box(g, a0 - .26, a1 + .26, ch, ch + .26, -.3, .26, F['coping'], T, bevel=.04)
    rows = (9, 5, 2)[T.i]
    base_x, base_z, apex = 2.02, 2.12, ch + .26 + 2.75
    P = np.zeros((rows + 1, 5, 3))
    for r in range(rows + 1):
        t = r / rows
        k = (1 - t) ** 1.25                      # the bell-cast: the foot flares
        y = ch + .24 + (apex - ch - .24) * t
        for c, (sx, sz) in enumerate(((-1, -1), (1, -1), (1, 1), (-1, 1), (-1, -1))):
            P[r, c] = (cx + sx * base_x * k, y - (.02 if r % 2 else 0), cz + sz * base_z * k)
    for c in range(4):
        for r in range(rows):
            a, b2, c2, d = tuple(P[r, c]), tuple(P[r, c + 1]), tuple(P[r + 1, c + 1]), tuple(P[r + 1, c])
            hint = ((a[0] + b2[0]) / 2 - cx, .8, (a[2] + b2[2]) / 2 - cz)
            if r == rows - 1:
                bk.facet(g, (a, b2, c2), F['slate'], hint)
            else:
                bk.facet(g, (a, b2, c2, d), F['slate'], hint)
    lathe(g, [(.1, 0), (.07, .12), (.035, .2), (.03, .9), (.09, .98), (.09, 1.06), (.02, 1.14)], (cx, apex - .08, cz), F['ironplain'], (8, 6, 4)[T.i], cap1=True)
    if T.i < 2:
        box(g, cx - .32, cx + .22, apex + .52, apex + .56, cz - .008, cz + .008, F['brass'], None, bevel=0)
        g.face(((cx + .22, apex + .46, cz), (cx + .4, apex + .54, cz), (cx + .22, apex + .62, cz)), F['brass'], (0, 0, 1))
        g.face(((cx + .22, apex + .46, cz), (cx + .22, apex + .62, cz), (cx + .4, apex + .54, cz)), F['brass'], (0, 0, -1))


def stone(g, b, T):
    """The Mariners' Stone: a stepped plinth, a die with bronze plaques and a leaning anchor, a cornice; over it the obelisk
    and its lamp."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y0 + .26, z0, z1, F['dressed'], T, bevel=.04, skip=('-y',))
    box(g, x0 + .13, x1 - .13, y0 + .26, y0 + .5, z0 + .13, z1 - .13, F['dressed'], T, bevel=.04, skip=('-y',))
    d = .24
    e = (x0 + d, x1 - d, y0 + .5, y1 - .46, z0 + d, z1 - d)
    box(g, e[0], e[1], e[2], e[3], e[4], e[5], F['ashlar'], T, bevel=.03, skip=('-y', '+y'))
    quoins(g, e, e[2], e[3], T, proud=.03)
    # the cornice back out to the block's face, weathered on top
    box(g, x0 + .04, x1 - .04, y1 - .46, y1 - .2, z0 + .04, z1 - .04, F['coping'], T, bevel=.05, skip=('-y',))
    box(g, x0 + .2, x1 - .2, y1 - .2, y1, z0 + .2, z1 - .2, F['dressed'], T, bevel=.05, skip=('-y',))
    for sx in (-1, 1):
        fc = FZ(e[4], e[5], e[1] if sx > 0 else e[0], sx)
        mid = fc.L / 2
        fc.box(g, mid - .72, mid + .72, 1.05, 2.45, 0, .045, F['bronze'], T, bevel=.015, skip=())
        if T.i < 2:
            fc.box(g, mid - .62, mid + .62, 1.15, 2.35, .045, .058, F['brass'], T, bevel=.006)
            # a ship's wheel in relief: a rim and spokes
            seg = (14, 8)[T.i]
            path = [fc.P(mid + math.cos(2 * math.pi * k / seg) * .36, 1.78 + math.sin(2 * math.pi * k / seg) * .36, .075) for k in range(seg)]
            sk.tube(g, path, .028, F['bronze'], 4, closed=True)
            for k in range(4):
                a = k * math.pi / 4
                sk.cylinder(g, fc.P(mid - math.cos(a) * .46, 1.78 - math.sin(a) * .46, .072), fc.P(mid + math.cos(a) * .46, 1.78 + math.sin(a) * .46, .072), .016, F['bronze'], 4)
    # the anchor leaning on the south face, iron gone to rust
    fa = FX(e[0], e[1], e[4], -1)
    mid = fa.L / 2
    it = (6, 5, 4)[T.i]
    sk.cylinder(g, fa.P(mid, .56, .12), fa.P(mid, 2.5, .1), .05, F['rust'], it)
    arc = [fa.P(mid + math.sin(t) * .62, .92 - math.cos(t) * .36, .12) for t in np.linspace(-1.25, 1.25, (9, 6, 3)[T.i])]
    sk.tube(g, arc, .05, F['rust'], it)
    if T.i < 2:
        sk.cylinder(g, fa.P(mid - .5, 2.32, .11), fa.P(mid + .5, 2.32, .11), .04, F['timber'], it)
        bk.ring(g, fa.P(mid, 2.62, .06), (0, 0, -1), .11, .022, F['rust'], T)
        for o in (-1, 1):
            p = fa.P(mid + o * .59, .86, .12)
            box(g, p[0] - .11, p[0] + .11, p[1], p[1] + .2, p[2] - .03, p[2] + .03, F['rust'], None, bevel=.02)
    # the obelisk and the lamp
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    h0, h1 = y1, y1 + 2.5
    for (ra, rb, ya, yb) in ((.56, .34, h0, h1), (.34, .0, h1, h1 + .36)):
        for k, (sx, sz) in enumerate(((1, 0), (0, 1), (-1, 0), (0, -1))):
            px, pz = -sz, sx                    # along the face
            a = (cx + sx * ra - px * ra, ya, cz + sz * ra - pz * ra)
            b2 = (cx + sx * ra + px * ra, ya, cz + sz * ra + pz * ra)
            c2 = (cx + sx * rb + px * rb, yb, cz + sz * rb + pz * rb)
            d2 = (cx + sx * rb - px * rb, yb, cz + sz * rb - pz * rb)
            bk.facet(g, (a, b2, c2, d2) if rb > 0 else (a, b2, c2), F['dressed'], (sx, .2, sz))
    box(g, cx - .2, cx + .2, h1 + .3, h1 + .5, cz - .2, cz + .2, F['brass'], T, bevel=.03)
    box(g, cx - .15, cx + .15, h1 + .5, h1 + .84, cz - .15, cz + .15, F['lamp'], None, bevel=.02, skip=('-y',))
    if T.i < 2:
        for sx in (-1, 1):
            for sz in (-1, 1):
                sk.cylinder(g, (cx + sx * .16, h1 + .5, cz + sz * .16), (cx + sx * .16, h1 + .84, cz + sz * .16), .014, F['ironplain'], 4)
    lathe(g, [(.26, 0), (.22, .05), (.08, .16), (.03, .3)], (cx, h1 + .84, cz), F['ironplain'], (8, 6, 4)[T.i], cap0=True)


# ---------------------------------------------------------------------------------------------------------------------------
# The end buildings: the harbour gate (west) and the lighthouse's hall (east)
# ---------------------------------------------------------------------------------------------------------------------------
def hall(g, sx, T, R):
    """One end building, sx -1 west (the harbour gate: granite under a slate roof, the flagstaff) or +1 east (the
    lighthouse's hall: lime wash, a red band, a leaded roof behind a crenellated parapet)."""
    west = sx < 0
    wallf, trim = ('ashlar', 'dressed') if west else ('lime', 'dressed')
    xf, xb = sx * END, sx * 22                  # the front and the back
    xi, xo = sx * 17.1, sx * 21.4               # the front wall's inner face, the back wall's inner face
    front = FZ(-5, 5, xf, -sx)
    # ---- the front: three piers, two doorways under stone lintels, a string and a cornice
    for lo, hi in ((-5, -2.5), (-.5, .5), (2.5, 5)):
        panel(g, front, lo, hi, L0, 5.0, F[wallf], T)
    for lo, hi in ((-2.5, -.5), (.5, 2.5)):
        panel(g, front, lo, hi, 2.6, 5.0, F[wallf], T)
        # the doorway's reveals and soffit
        for z, s2 in ((lo, 1), (hi, -1)):
            g.face(((xf, L0, z), (xi, L0, z), (xi, 2.6, z), (xf, 2.6, z)), F[trim], (0, 0, s2))
        g.face(((xf, 2.6, lo), (xi, 2.6, lo), (xi, 2.6, hi), (xf, 2.6, hi)), F[trim], (0, -1, 0))
        a0, a1 = span(front, lo, hi)
        # the surround: jambs and a lintel with its keystone, standing proud; a storm lamp over the door
        for a in (a0, a1):
            front.box(g, a - .09 if a == a0 else a - .13, a + .13 if a == a0 else a + .09, L0, 2.6, 0, .08, F[trim], T, bevel=.02)
        front.box(g, a0 - .22, a1 + .22, 2.6, 2.92, 0, .1, F['coping'], T, bevel=.025)
        mid = (a0 + a1) / 2
        if T.i < 2:
            front.box(g, mid - .14, mid + .14, 2.56, 3.0, 0, .14, F['dressed'], T, bevel=.02)
        # the lamp: a bracket, a cage, the glass
        front.box(g, mid - .11, mid + .11, 3.14, 3.46, .12, .34, F['lamp'], None, bevel=.02)
        front.box(g, mid - .16, mid + .16, 3.46, 3.52, .06, .4, F['ironplain'], T, bevel=.01)
        front.box(g, mid - .14, mid + .14, 3.08, 3.14, .1, .36, F['ironplain'], T, bevel=.01)
        if T.i < 2:
            front.box(g, mid - .02, mid + .02, 3.52, 3.6, 0, .25, F['ironplain'], T, bevel=0)
    a0, a1 = span(front, -5, 5)
    if T.i < 2:
        for lo, hi in ((-5, -2.5), (-.5, .5), (2.5, 5)):
            b0, b1 = span(front, lo, hi)
            front.mould(g, PLINTH, L0, b0 + .0, b1 - .0, F[trim], T)
    if west:
        ashlar(g, front, -5, -2.72, .4, 4.3, T, 61)
        ashlar(g, front, 2.72, 5, .4, 4.3, T, 62)
        ashlar(g, front, -.5, .5, 2.7, 4.3, T, 63)
        front.mould(g, STRING, 4.5, a0, a1, F['coping'], T)
        # a net hung to dry on the south pier, a lifebuoy on the north one
        a0n, a1n = span(front, -4.3, -2.9)
        bk.net(g, front, a0n, a1n, .5, 2.35, T, 3)
        bk.lifebuoy(g, front, front.at(3.6), 1.6, T)
        # the harbour board's bronze on the middle pier
        front.box(g, front.at(0) - .3, front.at(0) + .3, 1.25, 2.3, 0, .045, F['bronze'], T, bevel=.012)
    else:
        front.quad(g, a0, a1, 4.05, 4.75, .008, F['band'])
        quoins(g, (min(xf, xb), max(xf, xb), 0, 5, -5, 5), L0, 5.0, T, corners=((-sx, 1), (-sx, -1)))
        front.box(g, front.at(0) - .3, front.at(0) + .3, 1.25, 2.3, 0, .045, F['band'], T, bevel=.012)
        bk.lifebuoy(g, front, front.at(-3.6), 1.6, T)
        bk.lifebuoy(g, front, front.at(3.6), 1.6, T)
    front.mould(g, CORNICE, 5.4, a0 - .2, a1 + .2, F['coping'], T)
    panel(g, front, -5, 5, 5.0, 5.2, F[wallf], T)
    # ---- the sides and the back (outside): seen from the walk, the quay and outside the map
    for z, s2 in ((5.0, 1), (-5.0, -1)):
        side = FX(min(xf, xb), max(xf, xb), z, s2)
        panel(g, side, min(xf, xb), max(xf, xb), LW if s2 > 0 else L0, 5.2, F[wallf], T)
        b0, b1 = span(side, min(xf, xb), max(xf, xb))
        side.mould(g, CORNICE, 5.4, b0 - .2, b1 + .2, F['coping'], T)
        if west:
            ashlar(g, side, min(xf, xb), max(xf, xb), (LW if s2 > 0 else L0) + .1, 4.3, T, 64 + s2)
            if T.i < 2:
                side.mould(g, STRING, 4.5, b0, b1, F['coping'], T)
        else:
            side.quad(g, b0, b1, 4.05, 4.75, .008, F['band'])
        slit(g, side, (b0 + b1) / 2, 3.2, 4.0, T, w=.3)
    back = FZ(-5, 5, xb, sx)
    panel(g, back, -5, 5, L0, 5.2, F[wallf], T)
    back.mould(g, CORNICE, 5.4, -.2, 10.2, F['coping'], T)
    # the gate in the back wall: boards under a round head, iron straps (the mole runs on behind it)
    back.arch(g, 5.0, 1.5, L0, 2.3, 1.5, .012, F['door'], F['dressed'], T, trim=.24, proud=.1)
    # ---- inside: two rooms, their walls and the partition, a boarded ceiling on beams, a lamp on the back wall of each
    inner = F['granite' if west else 'lime']
    ib = FZ(-4.4, 4.4, xo, -sx)
    panel(g, ib, -4.4, 4.4, L0, 5.0, inner, T)
    ifr = FZ(-4.4, 4.4, xi, sx)
    for lo, hi in ((-4.4, -2.5), (-.5, .5), (2.5, 4.4)):
        panel(g, ifr, lo, hi, L0, 5.0, inner, T)
    for lo, hi in ((-2.5, -.5), (.5, 2.5)):
        panel(g, ifr, lo, hi, 2.6, 5.0, inner, T)
    for z, s2 in ((4.4, -1), (-4.4, 1)):
        panel(g, FX(min(xi, xo), max(xi, xo), z, s2), min(xi, xo), max(xi, xo), L0, 5.0, inner, T)
    px0, px1 = sorted((xo, sx * 18.6))
    for z, s2 in ((.3, 1), (-.3, -1)):
        panel(g, FX(px0, px1, z, s2), px0, px1, L0, 5.0, inner, T)
    panel(g, FZ(-.3, .3, sx * 18.6, -sx), -.3, .3, L0, 5.0, F[trim], T)
    ix0, ix1 = sorted((xi, xo))
    grid_face(g, (ix0, 5.0, -4.4), (ix1 - ix0, 0, 0), (0, 0, 8.8), 0, 0, F['plank'], (0, -1, 0), max(T.cell, 1.5))
    if T.i < 2:
        for z in (-3.3, -1.6, 1.6, 3.3):
            box(g, ix0, ix1, 4.78, 5.0, z - .1, z + .1, F['timber'], None, bevel=.02, skip=('+y', '-x', '+x'))
    for z in (-2.35, 2.35):
        ib.box(g, ib.at(z) - .1, ib.at(z) + .1, 2.7, 3.0, .06, .26, F['lamp'], None, bevel=.02)
        ib.box(g, ib.at(z) - .13, ib.at(z) + .13, 3.0, 3.05, 0, .3, F['ironplain'], T, bevel=.01)
        if T.i < 2:
            # a bench under it, a barrel hoop's worth of rope on a peg
            ib.box(g, ib.at(z) - .9, ib.at(z) + .9, .42, .5, 0, .24, F['plank'], T, bevel=.015)
            for o in (-.75, .75):
                ib.box(g, ib.at(z) + o - .04, ib.at(z) + o + .04, L0, .42, .02, .22, F['timber'], T, bevel=.01)
    # ---- the roof
    rx0, rx1 = sorted((xf - sx * .25, xb + sx * .25))
    if west:
        # two slated pitches over a ridge along x, gables front and back, a chimney and the flagstaff
        ridge, eave, zh = 5.4 + 2.5, 5.4, 5.3
        rows = (10, 5, 2)[T.i]
        for s2 in (-1, 1):
            for r in range(rows):
                t0, t1 = r / rows, (r + 1) / rows
                za, zb = s2 * zh * (1 - t0), s2 * zh * (1 - t1)
                ya, yb = eave + (ridge - eave) * t0, eave + (ridge - eave) * t1 + (.035 if T.i == 0 else 0)
                cols = max(1, int((rx1 - rx0) / max(T.cell, 1.4)))
                for c in range(cols):
                    xa, xc = rx0 + (rx1 - rx0) * c / cols, rx0 + (rx1 - rx0) * (c + 1) / cols
                    g.face(((xa, ya, za), (xc, ya, za), (xc, yb, zb), (xa, yb, zb)), F['slate'], normal=(0, zh, s2 * (ridge - eave)))
        for x, s3 in ((rx0, -1), (rx1, 1)):
            g.face(((x, eave, -zh), (x, eave, zh), (x, ridge, 0)), F['ashlar'], (s3, 0, 0))
        sweep(g, sk.rect_section(.3, .16, .04), [(rx0 - .03, ridge + .03, 0), (rx1 + .03, ridge + .03, 0)], F['lead'], up=Y, smooth_u=False)
        # a lit round window in the front gable
        seg = (14, 8, 6)[T.i]
        fx = rx1 + .012
        g.face([(fx, eave + .95 + math.sin(2 * math.pi * k / seg) * .42, math.cos(2 * math.pi * k / seg) * .42) for k in range(seg)], F['window'], (1, 0, 0))
        if T.i < 2:
            path = [(fx + .02, eave + .95 + math.sin(2 * math.pi * k / seg) * .5, math.cos(2 * math.pi * k / seg) * .5) for k in range(seg)]
            sk.tube(g, path, .06, F['dressed'], 4, closed=True)
            for a in (0, math.pi / 2):
                sk.cylinder(g, (fx + .02, eave + .95 - math.sin(a) * .42, -math.cos(a) * .42), (fx + .02, eave + .95 + math.sin(a) * .42, math.cos(a) * .42), .016, F['ironplain'], 4)
        box(g, -21.5, -20.7, ridge - .9, ridge + .9, 2.0, 2.8, F['ashlar'], T, bevel=.03)
        box(g, -21.58, -20.62, ridge + .9, ridge + 1.05, 1.92, 2.88, F['coping'], T, bevel=.03)
        fxp, fzp = R['gateFlagX'], R['gateFlagZ']
        lathe(g, [(.09, 0), (.06, .1), (.045, .3), (.035, 3.5), (.07, 3.56), (.07, 3.64), (.0, 3.72)], (fxp, ridge - .02, fzp), F['ironplain'], (8, 6, 4)[T.i])
    else:
        # a leaded flat roof behind a crenellated parapet (the tower rises behind it)
        floor(g, min(xf, xb) + .02, max(xf, xb) - .02, -4.98, 4.98, 5.43, F['lead'], T)
        nx0, nx1 = (rx0 + .32, rx1) if sx > 0 else (rx0, rx1 - .32)
        for fc, lo, hi in ((FZ(-5.25, 5.25, xf - sx * .25, -sx), -5.25, 5.25), (FX(nx0, nx1, 5.25, 1), nx0, nx1), (FX(nx0, nx1, -5.25, -1), nx0, nx1)):
            b0, b1 = span(fc, lo, hi)
            fc.box(g, b0, b1, 5.4, 5.85, -.3, 0, F['lime'], T, bevel=.02)
            a = b0 + .1
            while a < b1 - .5:
                fc.box(g, a, a + .7, 5.85, 6.3, -.3, 0, F['lime'], T, bevel=.03)
                if T.i == 0:
                    fc.box(g, a - .03, a + .73, 6.3, 6.38, -.33, .03, F['coping'], T, bevel=.02)
                a += 1.3
    # ---- the wings across the walk and the quay
    for nm, z0, z1, y0, y1 in (('wall', WALL0, WALL1, LW, 5.4), ('quay', QUAY0, -5.0, LQ, L0)):
        wx0, wx1 = sorted((sx * 21.4, xb))
        wf = FZ(z0, z1, sx * 21.4, -sx)
        panel(g, wf, z0, z1, y0, y1, F[wallf], T)
        panel(g, FZ(z0, z1, xb, sx), z0, z1, y0, y1, F[wallf], T)
        if nm == 'wall':
            panel(g, FX(wx0, wx1, WALL1, 1), wx0, wx1, y0 - .1, y1, F[wallf], T)
            box(g, wx0 - .06, wx1 + .06, y1, y1 + .16, z0, z1 + .06, F['coping'], T, bevel=.04, skip=('-y',))
            # a barred sally port: a dark arch behind an iron grille
            wf.arch(g, wf.L / 2, .7, y0, y0 + 1.3, .7, .01, F['deep'], F[trim], T, trim=.16, proud=.08)
            if T.i < 2:
                for o in np.linspace(-.56, .56, 6):
                    sk.cylinder(g, wf.P(wf.L / 2 + o, y0, .04), wf.P(wf.L / 2 + o, y0 + 1.3 + math.sqrt(max(0, .49 - o * o)), .04), .018, F['ironplain'], 4)
            if west:
                ashlar(g, wf, z0, z1, y0 + 2.2, y1 - .1, T, 71)
        else:
            panel(g, FX(wx0, wx1, QUAY0, -1), wx0, wx1, -7, y1, F[wallf], T)
            # the store's door under the hall
            wf.arch(g, wf.L / 2, .62, y0, y0 + 1.25, .62, .01, F['door'], F[trim], T, trim=.16, proud=.08)
            if T.i < 2:
                for y in (y0 + .35, y0 + 1.1):
                    wf.box(g, wf.L / 2 - .6, wf.L / 2 + .4, y - .03, y + .03, .01, .03, F['ironplain'], T, bevel=.006)


# ---------------------------------------------------------------------------------------------------------------------------
# Stairs, landings, furniture
# ---------------------------------------------------------------------------------------------------------------------------
def step(g, b, flight, T):
    """One step of a solid flight along x: its tread and nosing, its riser, its open side (the south one)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    tops = sorted(ext(o)[3] for o in flight)
    rise = tops[1] - tops[0] if len(tops) > 1 else .2
    lower = min(flight, key=lambda o: ext(o)[3])
    up = 1 if (max(flight, key=lambda o: ext(o)[3])['pos'][0] > lower['pos'][0]) else -1     # the way it climbs along x
    # the tread: a slab with a bullnose over the riser below
    xa, xb = (x0 - .04, x1) if up > 0 else (x0, x1 + .04)
    box(g, xa, xb, y1 - .07, y1, z0 - .03, z1, F['coping'], None, bevel=.028 if T.i == 0 else (.02 if T.i == 1 else 0), skip=('-y', '+z'))
    xr = x0 if up > 0 else x1
    g.face(((xr, y1 - rise - .001, z0), (xr, y1 - rise - .001, z1), (xr, y1 - .07, z1), (xr, y1 - .07, z0)), F['dressed'], (-up, 0, 0))
    g.face(((x0, y0, z0), (x1, y0, z0), (x1, y1 - .07, z0), (x0, y1 - .07, z0)), F['granite'], (0, 0, -1))


def landing(g, b, T, seed):
    x0, x1, y0, y1, z0, z1 = ext(b)
    west = x0 < 0
    floor(g, x0, x1, z0 + .34, z1, y1, F['sett'], T)
    s = FX(x0, x1, z0, -1)
    cope(g, s, x0, x1, y1 + .03, T, seed, depth=.36, over=.07)
    panel(g, s, x0, x1, y0, y1 - .17, F['granite'], T)
    ashlar(g, s, x0, x1, y0 + .05, y1 - .2, T, seed + 1)
    e = FZ(z0, z1, x0 if west else x1, -1 if west else 1)
    panel(g, e, z0, z1, y0, y1, F['granite'], T)
    ashlar(g, e, z0, z1, y0 + .05, y1 - .05, T, seed + 2)


def boat(g, b, T, seed):
    """An upturned boat on two trestles (the block is its hull)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    h = Geo()
    bk.hull(h, x1 - x0 - .06, z1 - z0 - .04, .62, T, F['hull'], f_bottom=F['hullred'], sheer=.1, y0=0, flip=True)
    # keel up: turn the loft (along z) to lie along x
    g.extend(h.transformed([0, 0, 1, (x0 + x1) / 2, 0, 1, 0, y0 + .4, -1, 0, 0, (z0 + z1) / 2]))
    box(g, x0 + .12, x1 - .12, y1 - .06, y1 - .0, (z0 + z1) / 2 - .035, (z0 + z1) / 2 + .035, F['tar'], None, bevel=.012 if T.i == 0 else 0)
    for x in (x0 + .55, x1 - .55):
        box(g, x - .05, x + .05, y0 + .34, y0 + .44, z0 + .02, z1 - .02, F['timber'], None, bevel=.012 if T.i == 0 else 0)
        for sz in (-1, 1):
            zc = (z0 + z1) / 2 + sz * (z1 - z0) * .36
            sk.cylinder(g, (x, y0, zc + sz * .1), (x, y0 + .36, zc - sz * .06), .04, F['timber'], (5, 4, 3)[T.i])
    if T.i < 2:
        # an oar laid along it
        sk.cylinder(g, (x0 + .1, y0 + .05, z0 + .08), (x1 - .3, y0 + .05, z0 + .08), .028, F['plank'], 5)
        box(g, x1 - .5, x1 - .02, y0 + .02, y0 + .08, z0 + .02, z0 + .16, F['plank'], None, bevel=.012)


# ---------------------------------------------------------------------------------------------------------------------------
# The scene
# ---------------------------------------------------------------------------------------------------------------------------
def static_scene(L, T):
    g = Geo()
    blocks = L['blocks']
    R = L['rig']
    posts = {(round(p[0], 2), round(p[2], 2)): p for p in R['posts']}
    flights = {}
    for b in blocks:
        if '-stair-' in b['id']:
            flights.setdefault(b['id'].rsplit('-', 1)[0], []).append(b)
    deck(g, L, T)
    sea_wall(g, T)
    hall(g, -1, T, R)
    hall(g, 1, T, R)
    # gulls sitting out the weather: on the gate's ridge, the hall's merlons, the turret's cornice
    for x, y, z, yaw in ((-20.2, 7.98, .0, 1.2), (-18.1, 7.98, .0, 2.0), (-17.6, 7.98, .0, 1.5), (16.42, 6.38, -3.35, -1.3), (16.42, 6.38, 1.85, -1.9), (19.6, 6.38, 5.2, .3), (-1.5, 8.76, 4.8, 2.9), (1.2, 8.76, 8.55, .4)):
        perched(g, x, y, z, yaw, T)
    seed = 0
    for b in blocks:
        i = b['id']
        seed += 1
        if b['role'] != 'static':
            continue
        if i.startswith('breakwater-wall-'):
            wall(g, b, T, seed)
        elif i.startswith('breakwater-parapet-'):
            parapet(g, b, blocks, T, seed)
        elif i.startswith('breakwater-quay-') or i == 'breakwater-ledge':
            quay(g, b, T, seed)
        elif i == 'breakwater-bastion':
            bastion(g, b, T)
        elif i == 'breakwater-pulpit':
            pulpit(g, b, T)
        elif i == 'breakwater-turret':
            turret(g, b, T, R)
        elif i == 'breakwater-stone':
            stone(g, b, T)
        elif '-stair-' in i:
            step(g, b, flights[i.rsplit('-', 1)[0]], T)
        elif i.startswith('breakwater-landing-'):
            landing(g, b, T, seed)
        elif i.startswith('breakwater-blocks-'):
            bk.quarried(g, ext(b), T, seed)
        elif i.startswith('breakwater-boat-'):
            boat(g, b, T, seed)
        elif i.startswith('breakwater-bollard-'):
            e = ext(b)
            bk.bollard(g, (e[0] + e[1]) / 2, e[2], (e[4] + e[5]) / 2, T, r=(e[1] - e[0]) / 2 - .02, h=e[3] - e[2])
            if T.i < 2 and 'quay' not in i:
                cxb, czb = (e[0] + e[1]) / 2, (e[4] + e[5]) / 2
                sgn = 1 if cxb < 0 else -1
                bk.rope_line(g, [(cxb, e[2] + .3, czb - .3), (cxb + sgn * .2, e[2] + .06, czb - .5), (cxb + sgn * .5, e[2] + .05, -5.06), (cxb + sgn * .6, e[2] - .5, -5.1), (cxb + sgn * .62, e[2] - 1.3, -5.06)], T)
                bk.rope_coil(g, cxb - sgn * .8, e[2], czb + .55, .32, T)
            if T.i < 2 and 'quay' in i:
                bk.rope_coil(g, (e[0] + e[1]) / 2 + (.75 if e[0] < 0 else -.75), e[2], (e[4] + e[5]) / 2 + .5, .3, T)
        elif i.startswith('breakwater-lamp-'):
            e = ext(b)
            p = posts[(round(b['pos'][0], 2), round(b['pos'][2], 2))]
            bk.lamp_post(g, p[0], e[2], e[3], p[2], (p[3], p[4], p[5]), T)
    return g


def perched(g, x, y, z, yaw, T):
    """A gull sitting out the weather on a roof (only where nobody can stand)."""
    if T.i == 2:
        return
    import breakwater_props as props
    t = props.gull_body(T)
    # folded wings along its back, legs tucked: the body tipped up a little
    for sx in (-1, 1):
        t.face(((sx * .05, .05, .12), (sx * .085, .02, -.1), (sx * .03, .06, -.34), (sx * .02, .075, -.05)), F['gullgrey'], (sx, .6, 0))
    q = bk_quat(yaw, -.25)
    g.extend(t.transformed(sk.affine(q, (x, y + .075, z), (1.25, 1.25, 1.25))))


def bk_quat(yaw, pitch):
    a, b = sk.quat_axis(0, 1, 0, yaw), sk.quat_axis(1, 0, 0, pitch)
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx, aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz)


def moss_zone(W):
    """Weed where the sea reaches: thick from under the surface to the tide line, a trace higher on the sea side."""
    y = W[:, 1]
    tide = sk.smooth(SEA + 1.9, SEA + .5, y)
    north = np.clip((W[:, 2] - 7.5) / 2, 0, 1) * sk.smooth(2.6, .5, y) * .35
    return np.clip(tide + north, 0, 1) * 1.0 + .04


VIEWS = [('wide-sw', (-30, 16, -30), (0, 0, 0), 60), ('wide-ne', (26, 12, 26), (0, 1, 2), 60), ('deck-east', (-15.5, 1.7, -.5), (4, 1.6, 0), 72), ('deck-west', (14, 1.7, 1.5), (-6, 1.8, 0), 72),
         ('stone', (-5.5, 1.7, -3.2), (0, 2.2, 0), 65), ('pulpit', (-5.5, 1.6, -1.5), (0, 3.2, 6), 68), ('turret', (-6, 4.2, 6.6), (0, 6.4, 6.7), 62), ('walk', (-15.5, 4.1, 6.4), (-2, 3.6, 6.8), 72),
         ('breach', (-4.5, 4.2, 6.2), (-11, 3.0, 8.4), 70), ('quay', (-19, -.8, -8.2), (-4, -1.6, -6.5), 72), ('quay-up', (-9, -.7, -8.8), (-2, 1.2, -4), 70), ('gate', (-9, 1.7, 0), (-18, 3.0, 0), 62),
         ('hall-e', (9, 1.7, 0), (18, 3.0, 0), 62), ('room', (-20.8, 1.6, 3.9), (-17.5, 1.5, 1.2), 80), ('sea-side', (-10, 3, 24), (-2, 1, 8), 60), ('top', (0, 40, -.01), (0, 0, 0), 60),
         ('light', (2, 1.8, -1), (31, 16, 0), 60), ('shore', (-15, 4.2, 6.5), (-120, 4, 0), 55), ('harbour', (4, 1.7, -3), (-60, 0, -80), 60), ('stairs', (-9, 1.6, .5), (-15, 1.6, 4.4), 62),
         ('blocks', (-6.5, 1.6, -2.6), (-10, 1.1, .2), 60), ('lantern', (18, 20, -9), (31, 24.5, 0), 40), ('tower', (10, 6, -14), (31, 12, 0), 62), ('town', (-30, 6, -12), (-260, 14, -30), 30),
         ('boat', (2, -1, -11), (8.5, -2.6, -19.5), 50), ('walk-e', (4, 4.2, 6.6), (16, 3.4, 6.8), 70)]


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
    ap.add_argument('--no-far', action='store_true')
    ap.add_argument('--stats', action='store_true')
    args = ap.parse_args(argv)
    L = sk.load_layout(args.layout)
    bk.finishes()
    import breakwater_props as props
    import breakwater_far as far
    sb = sk.StageBank()
    # big chunks: the whole mole is 44 x 18 m and seen end to end, so fewer, larger draws cost less than finer culling saves
    chunks = sk.Chunks(22.0, (-44.0, -22.0), (-1.0, 9.0))
    t0 = time.time()
    for ti in [int(x) for x in args.tiers.split(',')]:
        T = sk.Tier(ti)
        t = time.time()
        g = static_scene(L, T)
        before = g.arrays()[0].astype(np.float64).copy() if args.stats else None
        print('  coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % sk.lift_coplanar(g, eps=sk.NEAR_TIER[ti], lift=sk.LIFT_TIER[ti]))
        # no face reaches out of its chunk's cell: a floor is never drawn twice where neighbouring chunks draw at different tiers
        print('  chunks: %d faces cut along the cells' % chunks.split(g))
        if args.stats:
            # where the stacks stand deepest (a face lifted by more than two layers is worth a look)
            P = g.arrays()[0].astype(np.float64)
            d = np.linalg.norm(P - before, axis=1)
            deep = np.nonzero(d > sk.LIFT_TIER[ti] * 2.5)[0]
            seen = {}
            for k in deep:
                key = tuple(np.round(before[k] / 2.0).astype(int))
                seen[key] = max(seen.get(key, 0), d[k])
            print('  deep stacks near (x, y, z in 2 m cells: mm): ' + ', '.join('%s: %.0f' % (str(tuple(int(v) * 2 for v in k)), v * 1000) for k, v in sorted(seen.items(), key=lambda q: -q[1])[:24]))
        print('tier %s: static %d faces, %.1f s' % (T.name, g.faces, time.time() - t))
        ao = None
        if not args.no_ao:
            # an open-air stage: the occlusion reaches a metre and is eased (the anime style bands it hard)
            ao = 1.0 - (1.0 - sk.bake_ao([g], distance=(1.1, 1.1, 1.3)[ti], samples=(32, 16, 12)[ti])[0]) * .75
        bake = sk.Bake(g, ao, floors=FLOORS, seed=3, moss_zone=moss_zone)
        ids = chunks.ids(g)
        for cid in np.unique(ids):
            sb.add('static/%d/%d' % (cid, ti), 'static', sk.to_mesh('static/%d/%d' % (cid, ti), g, bake, ids == cid))
        if args.preview and ti == args.preview_tier:
            views = [v for v in VIEWS if not args.views or v[0] in args.views.split(',')]
            extra = []
            if not args.no_far:
                for name, fg in far.items(L, sk.Tier(1)):
                    extra.append((fg, sk.Bake(fg, None, floors=(), seed=5)))
            if not args.views or 'rig' in args.views:
                row = Geo()
                for k, (name, pg, rest) in enumerate(props.items(L, T) + props.breakables(L, T)):
                    s = 3.0 if name in ('lantern', 'buoy-lamp', 'gull-body', 'gull-wing-l', 'gull-wing-r', 'flag') else (.25 if name in ('boat',) else (.5 if name in ('buoy', 'dinghy') else 1.0))
                    row.extend(pg.transformed([s, 0, 0, -9.0 + (k % 7) * 3.0, 0, s, 0, 300.0 - (k // 7) * 3.2, 0, 0, s, 0]))
                extra.append((row, sk.Bake(row, None, floors=(), seed=7)))
                views = views + [('rig', (0, 298.5, 12), (0, 296, 0), 62), ('rig-b', (0, 303, 6), (0, 297, 0), 60)]
            sk.preview([(g, bake)] + extra, views, args.preview)
        if not args.static_only:
            props.bank_props(sb, L, T, args.no_ao)
        del g
    if not args.no_far and not args.static_only:
        far.bank_far(sb, L, args.no_ao)
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
