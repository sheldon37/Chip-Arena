"""Pier 9's art (docs/arenas/HARBOR.md, docs/arenas/STAGE-ART.md): the amusement pier modelled in Blender on the map's own
blocks, into the stage bank Resources/ChipArenaStagePier (stagekit.py's format and bake). Runtime NativeStageArt draws it in
place of the boxes; without the bank the box kit draws as before.

usage (from unity/ChipArenaConversion):
  dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout harbor LAYOUT.json
  blender --background --factory-startup --python tools/stages/harbor_art.py -- --layout LAYOUT.json --out ASSETS/LocalAssets
          [--preview DIR] [--views a,b] [--preview-tier N] [--tiers 0,1,2] [--no-ao] [--static-only] [--stats]

This file: the static scene, block by block (the boards in planks, the boarded crossing and its rails, the halls with their
pilasters, lit windows, cornices and arches, the kiosks, the terraces' turrets and onion domes, the PIER 9 signs, the stalls
with their prizes and awnings, the booths, the carousel's and the band stand's bases, drums, upper decks, columns, canopy and
dome, the drop towers' masts, planters and lamp posts, crates, carts, signals, rails, stairs, the caisson and the piles), what
hangs overhead (the strings of bulbs) and stands under the boards (the pile bents), and the near backdrop (the Ferris wheel's
frame and landing stage, the coaster's trestles and spikes, the neck to the shore, the pier head's landing). harbor_props.py:
the rig (the wheel, its cabins, the turntable and mounts, the star and dolls, the boats, the train, the gondolas, the lamps,
the chasing bulbs). harbor_kit.py: the finishes and helpers.

Frames: everything static is in world space. The chasing bulbs are collected while the static scene is built (harbor_kit.MARQUEE)
and banked as rig/marquee-<g> in the world's frame.
"""
import argparse
import math
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, sweep, lathe  # noqa: E402
import harbor_kit as hk  # noqa: E402
from harbor_kit import F, E, hashf, rod, ball, bulb, bulbs, slab, pt, mould, prism, ngon, cone, side  # noqa: E402

BANK = 'ChipArenaStagePier'
FLOORS = (-2.6, 0.0, 4.2)


def starts(b, p):
    return b['id'].startswith(p)


# ---------------------------------------------------------------------------------------------------------------------------
# The boards
# ---------------------------------------------------------------------------------------------------------------------------
def boards(g, b, T, seed=0, w=.375):
    """A plate of planks running along x: the body, a dark bed a little under the top, the boards with gaps between."""
    x0, x1, y0, y1, z0, z1 = E(b)
    box(g, x0, x1, y0, y1 - .03, z0, z1, F['joist'], T, 0, skip=('+y',))
    if T.i == 2:
        g.quad((x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1), F['deck0'], (0, 1, 0))
        for s in range(4):
            c, sg, ax, a0, a1 = side(E(b), s)
            g.quad(pt(E(b), s, a0, y1 - .03), pt(E(b), s, a1, y1 - .03), pt(E(b), s, a1, y1), pt(E(b), s, a0, y1), F['joist'], (sg, 0, 0) if ax == 'z' else (0, 0, sg))
        return
    g.quad((x0, y1 - .03, z0), (x1, y1 - .03, z0), (x1, y1 - .03, z1), (x0, y1 - .03, z1), F['gap'], (0, 1, 0))
    w = w * (1 if T.i == 0 else 2)
    gap = .012 if T.i == 0 else .02
    k0 = int(math.floor(z0 / w))
    z = k0 * w
    while z < z1 - 1e-6:
        a, c = max(z0, z + gap / 2), min(z1, z + w - gap / 2)
        if c - a > .02:
            f = F['deck%d' % int(hashf(int(round(z / w)), seed, 7) * 3)]
            # a board: its top in lengths (the bake's cells), its long sides down into the gap
            n = max(1, int(math.ceil((x1 - x0) / (1.5 if T.i == 0 else 4.0))))
            for i in range(n):
                xa, xb = x0 + (x1 - x0) * i / n, x0 + (x1 - x0) * (i + 1) / n
                g.quad((xa, y1, a), (xb, y1, a), (xb, y1, c), (xa, y1, c), f, (0, 1, 0))
            g.quad((x0, y1 - .03, a), (x1, y1 - .03, a), (x1, y1, a), (x0, y1, a), f, (0, 0, -1))
            g.quad((x0, y1 - .03, c), (x1, y1 - .03, c), (x1, y1, c), (x0, y1, c), f, (0, 0, 1))
            g.quad((x0, y1 - .03, a), (x0, y1 - .03, c), (x0, y1, c), (x0, y1, a), f, (-1, 0, 0))
            g.quad((x1, y1 - .03, a), (x1, y1 - .03, c), (x1, y1, c), (x1, y1, a), f, (1, 0, 0))
        z += w


def track(g, b, T):
    """The boarded crossing: sleepers' boards across, two steel rails, the lane's brass edge strips."""
    x0, x1, y0, y1, z0, z1 = E(b)
    box(g, x0, x1, y0, y1 - .03, z0, z1, F['joist'], T, 0, skip=('+y',))
    g.quad((x0, y1 - .03, z0), (x1, y1 - .03, z0), (x1, y1 - .03, z1), (x0, y1 - .03, z1), F['gap'], (0, 1, 0))
    w = .5 if T.i == 0 else (1.0 if T.i == 1 else 40.0)
    x = x0
    while x < x1 - 1e-6:
        a, c = x + .008, min(x1, x + w) - .008
        f = F['deck%d' % (1 if int(round(x / .5)) % 4 else 0)]
        box(g, a, c, y1 - .03, y1, z0, z1, f, None, 0, 0, skip=('-y',))
        x += w
    for z in (-.72, .72):
        rail(g, [(x0, y1, z), (x1, y1, z)], T)
    for z in (z0 + .08, z1 - .08):
        box(g, x0, x1, y1, y1 + .012, z - .04, z + .04, F['brass'], T, 0, 0, skip=('-y',))


def rail(g, path, T):
    """A coaster rail along a path (its foot on the path): a steel head on a web."""
    sec = [(-.045, 0), (.045, 0), (.045, .02), (.012, .035), (.012, .085), (.035, .095), (.035, .12), (-.035, .12), (-.035, .095), (-.012, .085), (-.012, .035), (-.045, .02)] if T.i == 0 \
        else [(-.045, 0), (.045, 0), (.035, .12), (-.035, .12)]
    sweep(g, sec, path, F['steel'], up=(0, 1, 0), smooth_u=False)


# ---------------------------------------------------------------------------------------------------------------------------
# The halls
# ---------------------------------------------------------------------------------------------------------------------------
def wall(g, b, T, L):
    e = E(b)
    x0, x1, y0, y1, z0, z1 = e
    box(g, x0, x1, y0, y1, z0, z1, F['cream' if x1 - x0 < z1 - z0 else 'creamx'], T, .01)
    along_x = x1 - x0 >= z1 - z0
    hz = -29.5 if (z0 + z1) / 2 < 0 else 29.5
    for s in ((2, 3) if along_x else (0, 1)):
        c, sg, ax, a0, a1 = side(e, s)
        outer = (c - (hz if along_x else 0)) * sg > (3 if along_x else 8)
        if y0 > .5:
            # an arch's head or a lintel: a gold band, its bulbs, and the arch's spandrels in the opening's corners
            mould(g, e, s, a0, a1, y1, hk.CORNICE, F['gold'])
            if outer:
                bulbs(pt(e, s, a0 + .15, y0 + .28, .07), pt(e, s, a1 - .15, y0 + .28, .07), .32, T)
            continue
        mould(g, e, s, a0, a1, y0, hk.PLINTH, F['navy'])
        mould(g, e, s, a0, a1, y1, hk.CORNICE, F['gold'])
        n = max(1, int(round((a1 - a0) / 2.5)))
        for k in range(n + 1):
            u = min(a1 - .11, max(a0 + .11, a0 + (a1 - a0) * k / n))
            slab(g, e, s, u - .11, u + .11, y0 + .4, y1 - .4, .05, F['red'], T)
            slab(g, e, s, u - .15, u + .15, y1 - .62, y1 - .4, .08, F['gold'], T)
        if outer:
            bulbs(pt(e, s, a0 + .2, y1 - .5, .16), pt(e, s, a1 - .2, y1 - .5, .16), .4, T)
        # a lit arched window in each bay wide enough (blind: the wall is whole)
        for k in range(n):
            u0, u1 = a0 + (a1 - a0) * k / n + .45, a0 + (a1 - a0) * (k + 1) / n - .45
            if u1 - u0 < .9 or not T.detail:
                continue
            window(g, e, s, (u0 + u1) / 2, min(1.5, u1 - u0), y0 + 1.15, y0 + 2.75, T, 'glass' if outer else 'glasspink', hashf(k, int(c * 10)) < .8)


def window(g, e, s, u, w, v0, v1, T, glass='glass', lit=True):
    """A round-headed window on face s: a gold frame and mullions over lit glass."""
    n = (8, 5, 3)[T.i]
    r = w / 2
    spring = v1 - r
    out = [(u - r, v0), (u + r, v0)] + [(u + r * math.cos(math.pi * i / n), spring + r * math.sin(math.pi * i / n)) for i in range(n + 1)]
    c, sg, ax, _, _ = side(e, s)
    nrm = (sg, 0, 0) if ax == 'z' else (0, 0, sg)
    g.face([pt(e, s, a, v, .022) for a, v in out], F[glass if lit else 'glassdark'], normal=nrm)
    path = [pt(e, s, a, v, .0) for a, v in out]
    sweep(g, [(-.05, 0), (.05, 0), (.05, .05), (-.05, .05)], path, F['gold'], closed=True, up=nrm, caps=False, smooth_u=False,
          path_normals=[nrm] * len(path))
    if T.i == 0:
        slab(g, e, s, u - .02, u + .02, v0, v1 - .05, .03, F['gold'], None, .02, 0, 0)
        slab(g, e, s, u - r, u + r, spring - .02, spring + .02, .03, F['gold'], None, .02, 0, 0)


def arch_fill(g, L, T):
    """The great arches: spandrels in the top corners of each hall's front opening (overhead), a keystone lamp."""
    for b in L['blocks']:
        if not b['id'].endswith('-arch'):
            continue
        e = E(b)
        x0, x1, y0, y1, z0, z1 = e
        r = 1.0
        n = T.arc // 2 if T.i < 2 else 3
        for sx in (-1, 1):
            cx = x0 + r if sx < 0 else x1 - r
            corner = x0 if sx < 0 else x1
            arc = [(cx + sx * r * math.cos(math.pi / 2 * i / n), y0 - r + r * math.sin(math.pi / 2 * i / n)) for i in range(n + 1)]
            for z, nz in ((z0, -1), (z1, 1)):
                g.face([(corner, y0, z)] + [(a, v, z) for a, v in arc], F['creamx'], normal=(0, 0, nz))
            for i in range(n):
                (a0, v0), (a1, v1) = arc[i], arc[i + 1]
                g.face(((a0, v0, z0), (a1, v1, z0), (a1, v1, z1), (a0, v0, z1)), F['gold'], normal=(-sx * math.cos(math.pi / 2 * (i + .5) / n), -math.sin(math.pi / 2 * (i + .5) / n), 0))
        zf, sg = (z1, 1) if (z0 + z1) / 2 < 0 else (z0, -1)
        box(g, -.22, .22, y0 - .12, y0 + .34, zf, zf + sg * .1, F['gold'], T, .02)
        ball(g, (0, y0 + .1, zf + sg * .16), .1, F['globe'], 8, 4)


def kiosk(g, b, T):
    """The fortune teller's cabinet: a plinth, three glazed bays a side in gold frames, a crown and its bulbs."""
    e = E(b)
    x0, x1, y0, y1, z0, z1 = e
    box(g, x0 + .04, x1 - .04, y0, y1, z0 + .04, z1 - .04, F['navy'], T, .03)
    for s in (0, 1, 2, 3):
        c, sg, ax, a0, a1 = side(e, s)
        mould(g, e, s, a0, a1, y0, hk.PLINTH, F['navy'], -.04)
        mould(g, e, s, a0, a1, y1, hk.CORNICE, F['gold'], -.04)
        if s >= 2:
            for k in range(3):
                u = a0 + (a1 - a0) * (k + .5) / 3
                window(g, e, s, u, 1.5, y0 + .75, y1 - .7, T, ('glass', 'glasspink', 'glasscyan')[k], True)
            bulbs(pt(e, s, a0 + .2, y1 - .52, .12), pt(e, s, a1 - .2, y1 - .52, .12), .36, T)


def turret(g, b, T):
    """A terrace turret: a striped square shaft with a lit window each side, a cornice, and over it (overhead) an onion dome
    with its ribs, finial and lamp."""
    e = E(b)
    x0, x1, y0, y1, z0, z1 = e
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    box(g, x0 + .03, x1 - .03, y0, y1, z0 + .03, z1 - .03, F['cream'], T, .06)
    for s in (0, 1, 2, 3):
        c, sg, ax, a0, a1 = side(e, s)
        mould(g, e, s, a0, a1, y0, hk.PLINTH, F['navy'], -.03)
        mould(g, e, s, a0, a1, y1, hk.CORNICE, F['gold'], -.03)
        for k in range(4):
            u = a0 + .25 + k * .5
            slab(g, e, s, u - .1, u + .1, y0 + .42, y1 - .45, .02, F['red'], T, 0, .004, 0)
        if T.detail:
            window(g, e, s, (a0 + a1) / 2, .8, y0 + 1.5, y0 + 3.1, T)
        bulbs(pt(e, s, a0 + .15, y1 - .5, .1), pt(e, s, a1 - .15, y1 - .5, .1), .34, T)
    n = (16, 10, 8)[T.i]
    lathe(g, [(r * 1.02, y1 + .28 + h * 1.1) for r, h in hk.onion()], (cx, 0, cz), F['teal'], n)
    lathe(g, [(1.12, y1), (1.22, y1 + .1), (1.22, y1 + .2), (1.02, y1 + .28)], (cx, 0, cz), F['gold'], n)
    if T.detail:
        for k in range(8):
            a = k * math.pi / 4
            path = [(cx + math.sin(a) * (r * 1.02 + .02), y1 + .28 + h * 1.1, cz + math.cos(a) * (r * 1.02 + .02)) for r, h in hk.onion()]
            sweep(g, [(-.03, 0), (.03, 0), (0, .04)], path, F['gold'], caps=False, smooth_u=False)
    top = y1 + .28 + 2.1 * 1.1
    rod(g, (cx, top - .1, cz), (cx, top + .9, cz), .03, F['gold'], 5)
    ball(g, (cx, top + .12, cz), .12, F['gold'], 8, 4)
    ball(g, (cx, top + .98, cz), .14, F['globe'], 8, 4)


FONT = {'P': '11110100011000111110100001000010000', 'I': '11111001000010000100001000010011111', 'E': '11111100001000011110100001000011111',
        'R': '11110100011000111110101001001010001', '9': '01110100011000101111000010000101110', ' ': '0' * 35}


def sign(g, b, T):
    """PIER 9 in bulbs on a navy board in a gold frame, toward the midway; a plain back."""
    e = E(b)
    x0, x1, y0, y1, z0, z1 = e
    box(g, x0, x1, y0, y1, z0, z1, F['navy'], T, .02)
    s = 2 if (z0 + z1) / 2 < 0 else 3
    d = -1 if s == 2 else 1
    for ss in (2, 3):
        c, sg, ax, a0, a1 = side(e, ss)
        for v0, v1 in ((y0, y0 + .14), (y1 - .14, y1)):
            slab(g, e, ss, a0, a1, v0, v1, .06, F['gold'], T)
        for u0, u1 in ((a0, a0 + .14), (a1 - .14, a1)):
            slab(g, e, ss, u0, u1, y0 + .14, y1 - .14, .06, F['gold'], T)
    pitch = .16
    wd = 6 * 6 * pitch - pitch
    xs = (x0 + x1) / 2 - d * wd / 2
    yt = y1 - .62
    k = 0
    for gi, ch in enumerate('PIER 9'):
        for r in range(7):
            for c in range(5):
                if FONT[ch][r * 5 + c] == '1':
                    p = pt(e, s, xs + d * (gi * 6 + c) * pitch, yt - r * pitch, .06)
                    bulb(p[0], p[1], p[2], gi + r + c, r=.075)
                    k += 1
    bulbs(pt(e, s, x0 + .3, y0 + .3, .08), pt(e, s, x1 - .3, y0 + .3, .08), .3, T)
    bulbs(pt(e, s, x0 + .3, y1 - .3, .08), pt(e, s, x1 - .3, y1 - .3, .08), .3, T)
    # a low cresting along the top
    if T.detail:
        for i in range(13):
            x = x0 + .4 + i * (x1 - x0 - .8) / 12
            ball(g, (x, y1 + .07, (z0 + z1) / 2), .09, F['gold'], 6, 3)


# ---------------------------------------------------------------------------------------------------------------------------
# The midway
# ---------------------------------------------------------------------------------------------------------------------------
def stall(g, L, stem, T):
    by = {b['id'][len(stem) + 1:]: E(b) for b in L['blocks'] if b['id'].startswith(stem + '-')}
    back, counter, roof = by['back'], by['counter'], by['roof']
    xfront = back[1] - back[0] < back[5] - back[4]
    if xfront:
        front = 0 if (counter[0] + counter[1]) / 2 > (back[0] + back[1]) / 2 else 1
    else:
        front = 2 if (counter[4] + counter[5]) / 2 > (back[4] + back[5]) / 2 else 3
    seed = int(abs(back[0] * 7 + back[4] * 13))
    colour = ('teal', 'red', 'navy')[seed % 3]
    # the back: boards, shelves of prizes on the nook's side
    x0, x1, y0, y1, z0, z1 = back
    box(g, x0, x1, y0, y1, z0, z1, F[colour], T, .01)
    c, sg, ax, a0, a1 = side(back, front)
    for row in range(3):
        y = y0 + .72 + row * .62
        slab(g, back, front, a0 + .25, a1 - .25, y - .04, y, .2, F['crate'], T)
        if not T.detail:
            continue
        u = a0 + .5
        k = 0
        while u < a1 - .45:
            p = pt(back, front, u, y, .1)
            r = .075 + .03 * hashf(seed, row, k)
            f = F[('red', 'lilac', 'gold', 'white', 'teal')[int(hashf(seed, row, k, 3) * 5)]]
            if (k + row) % 3 == 0:
                # a plush bear: a body, a head, two ears
                ball(g, (p[0], p[1] + r, p[2]), r, f, 6, 3)
                ball(g, (p[0], p[1] + 2 * r + .05, p[2]), r * .72, f, 6, 3)
                if T.i == 0:
                    for q in (-1, 1):
                        ball(g, pt(back, front, u + q * r * .55, y + 2 * r + .05 + r * .6, .1), r * .28, f, 5, 2)
            elif (k + row) % 3 == 1:
                lathe(g, [(.02, y), (r, y + .01), (r * .8, y + .12), (r * .35, y + .2), (r * .45, y + .26), (.01, y + .27)], (p[0], 0, p[2]), f, 6)
            else:
                box(g, p[0] - r, p[0] + r, y, y + 2.2 * r, p[2] - r, p[2] + r, f, T, .015, 0)
            u += .34 + .1 * hashf(seed, row, k, 5)
            k += 1
    bulbs(pt(back, front, a0 + .3, y1 - .22, .06), pt(back, front, a1 - .3, y1 - .22, .06), .4, T)
    # the cheeks: striped boards
    for name in ('cheek-0', 'cheek-1'):
        e = by[name]
        box(g, e[0], e[1], e[2], e[3], e[4], e[5], F['cream'], T, .01)
        for s in ((2, 3) if xfront else (0, 1)):
            c, sg, ax, a0, a1 = side(e, s)
            u = a0 + .2
            while u < a1 - .1:
                slab(g, e, s, u - .2, min(a1, u + .2), e[2], e[3], .012, F['red'], T, 0, 0, 2.0)
                u += .8
    # the counter: a panelled front, a bullnosed top
    e = counter
    box(g, e[0] + .03, e[1] - .03, e[2], e[3] - .06, e[4] + .03, e[5] - .03, F['cream'], T, .01)
    box(g, e[0] - .05, e[1] + .05, e[3] - .06, e[3], e[4] - .05, e[5] + .05, F['crate'], T, .025)
    for s in (0, 1, 2, 3):
        c, sg, ax, a0, a1 = side(e, s)
        u = a0 + .2
        while u < a1 - .1:
            slab(g, e, s, u - .2, min(a1, u + .2), e[2] + .1, e[3] - .14, .012, F['red'], T, -.03, 0, 2.0)
            u += .8
    # the roof: a striped awning, a scalloped valance and bulbs along the front
    e = roof
    x0, x1, y0, y1, z0, z1 = e
    box(g, x0, x1, y0, y1 - .02, z0, z1, F['canvas'], T, .0, skip=('+y',))
    wide_x = not xfront
    a0, a1 = (x0, x1) if wide_x else (z0, z1)
    u = a0
    k = 0
    while u < a1 - 1e-6:
        ub = min(a1, u + .5)
        f = F['canvasred' if k % 2 == 0 else 'canvas']
        if wide_x:
            box(g, u, ub, y1 - .02, y1, z0, z1, f, T, 0, 1.5, skip=('-y',))
        else:
            box(g, x0, x1, y1 - .02, y1, u, ub, f, T, 0, 1.5, skip=('-y',))
        # the valance's tab under the front edge
        c, sg, ax, _, _ = side(e, front)
        n = 4 if T.i == 0 else 2
        pts = [pt(e, front, u, y0, .02), pt(e, front, ub, y0, .02)] + [pt(e, front, ub - (ub - u) * i / n, y0 - .2 * math.sin(math.pi * i / n), .02) for i in range(1, n)]
        g.face(pts, f, normal=(sg, 0, 0) if ax == 'z' else (0, 0, sg))
        g.face(pts, f, normal=(-sg, 0, 0) if ax == 'z' else (0, 0, -sg))
        u += .5
        k += 1
    c, sg, ax, a0, a1 = side(e, front)
    bulbs(pt(e, front, a0 + .15, (y0 + y1) / 2, .06), pt(e, front, a1 - .15, (y0 + y1) / 2, .06), .32, T)


def booth(g, b, T):
    """A ticket booth: striped, chamfered, a lit window with a brass grille each long side, a gold cornice."""
    e = E(b)
    x0, x1, y0, y1, z0, z1 = e
    c = .22
    poly = [(x0 + c, z0), (x1 - c, z0), (x1, z0 + c), (x1, z1 - c), (x1 - c, z1), (x0 + c, z1), (x0, z1 - c), (x0, z0 + c)]
    prism(g, poly, y0, y1, F['cream'])
    for s in (0, 1, 2, 3):
        cc, sg, ax, a0, a1 = side(e, s)
        u = a0 + c + .2
        while u < a1 - c - .1:
            slab(g, e, s, u - .2, min(a1 - c, u + .2), y0 + .4, y1 - .5, .012, F['red'], T, 0, 0, 2.0)
            u += .8
        mould(g, e, s, a0 + c, a1 - c, y1 - .08, hk.CORNICE, F['gold'])
        mould(g, e, s, a0 + c, a1 - c, y0, hk.PLINTH, F['navy'])
        if s >= 2:
            m = (a0 + a1) / 2
            slab(g, e, s, m - .75, m + .75, y0 + 1.1, y0 + 2.05, .03, F['gold'], T)
            slab(g, e, s, m - .68, m + .68, y0 + 1.17, y0 + 1.98, .02, F['glass'], None, .02, 0, 0)
            if T.i == 0:
                for k in range(7):
                    u = m - .6 + k * .2
                    rod(g, pt(e, s, u, y0 + 1.17, .05), pt(e, s, u, y0 + 1.98, .05), .012, F['brass'], 4, False)
            slab(g, e, s, m - .8, m + .8, y0 + 1.02, y0 + 1.1, .16, F['crate'], T)
        bulbs(pt(e, s, a0 + c + .1, y1 - .56, .08), pt(e, s, a1 - c - .1, y1 - .56, .08), .34, T)


def ride(g, L, stem, T, P):
    """A ride: the octagonal base (a step up), the drum at its heart, the upper deck's fascia and soffit; over the deck
    (overhead) eight columns and, on the carousel, the striped canopy; on the band stand, the dome and its lantern."""
    drum = next(E(b) for b in L['blocks'] if b['id'] == stem + '-drum')
    cx, cz = (drum[0] + drum[1]) / 2, (drum[4] + drum[5]) / 2
    carousel = cz < 0
    lamp = 'glasspink' if carousel else 'glasscyan'
    n8 = math.pi / 8
    # the base: an octagon of boards with a painted ring and a moulded gold rim
    oct_ = ngon(cx, cz, 4.6, 8, n8, apothem=True)
    prism(g, oct_, 0, .17, F['gold'], top=False)
    prism(g, ngon(cx, cz, 4.56, 8, n8, apothem=True), .17, .2, F['golddim'], f_top=F['deck1'])
    if T.i < 2:
        for r0, r1, f in ((4.3, 4.05, 'teal' if carousel else 'navy'), (2.2, 1.95, 'red')):
            a, bq = ngon(cx, cz, r0, 8, n8, apothem=True), ngon(cx, cz, r1, 8, n8, apothem=True)
            for i in range(8):
                g.face(((a[i][0], .206, a[i][1]), (a[(i + 1) % 8][0], .206, a[(i + 1) % 8][1]), (bq[(i + 1) % 8][0], .206, bq[(i + 1) % 8][1]), (bq[i][0], .206, bq[i][1])), F[f], normal=(0, 1, 0))
    # the drum: a chamfered square of mirrors (the carousel) or organ pipes (the band stand) between gold pilasters
    x0, x1, y0, y1, z0, z1 = drum
    c = .3
    prism(g, [(x0 + c, z0), (x1 - c, z0), (x1, z0 + c), (x1, z1 - c), (x1 - c, z1), (x0 + c, z1), (x0, z1 - c), (x0, z0 + c)], y0, y1, F['red' if carousel else 'navy'], top=False)
    for s in (0, 1, 2, 3):
        cc, sg, ax, a0, a1 = side(drum, s)
        a0, a1 = a0 + c, a1 - c
        mould(g, drum, s, a0, a1, y0, hk.PLINTH, F['gold'])
        mould(g, drum, s, a0, a1, y1 - .02, hk.CORNICE, F['gold'])
        for k in range(3):
            u0, u1 = a0 + (a1 - a0) * k / 3 + .08, a0 + (a1 - a0) * (k + 1) / 3 - .08
            if carousel:
                slab(g, drum, s, u0, u1, y0 + .5, y1 - .5, .03, F['gold'], T)
                slab(g, drum, s, u0 + .05, u1 - .05, y0 + .55, y1 - .55, .02, F['mirror'], None, .02, .004, 0)
            else:
                slab(g, drum, s, u0, u1, y0 + .5, y1 - .5, .02, F['black'], T)
                m = 5 if T.i == 0 else 3
                for j in range(m):
                    u = u0 + (u1 - u0) * (j + .5) / m
                    h = (y1 - y0 - 1.3) * (.55 + .45 * math.sin(math.pi * (j + .5) / m))
                    rod(g, pt(drum, s, u, y0 + .55, .06), pt(drum, s, u, y0 + .55 + h, .06), (u1 - u0) / m * .42, F['gold'], 6)
        bulbs(pt(drum, s, a0 + .1, y1 - .46, .07), pt(drum, s, a1 - .1, y1 - .46, .07), .3, T)
    # the upper deck (overhead): a fascia in painted panels between gold rails, bulbs along it, a lit soffit ring
    deck0, deck1 = P['rideDeck'] - .2, P['rideDeck']
    o = ngon(cx, cz, 4.42, 8, n8, apothem=True)
    prism(g, o, deck0, deck1, F['cream'], top=True, bottom=True, f_top=F['deck1'])
    for i in range(8):
        a, bq = o[i], o[(i + 1) % 8]
        nx, nz = (a[0] + bq[0]) / 2 - cx, (a[1] + bq[1]) / 2 - cz
        ln = math.hypot(nx, nz)
        nx, nz = nx / ln, nz / ln
        for y in (deck0 - .02, deck1 - .04):
            sweep(g, [(-.03, 0), (.03, 0), (.03, .05), (-.03, .05)], [(a[0] + nx * .0, y + .03, a[1] + nz * .0), (bq[0], y + .03, bq[1])], F['gold'], up=(nx, 0, nz), smooth_u=False, path_normals=[(nx, 0, nz)] * 2)
        bulbs((a[0] + nx * .09, (deck0 + deck1) / 2, a[1] + nz * .09), (bq[0] + nx * .09, (deck0 + deck1) / 2, bq[1] + nz * .09), .3, T)
        # the soffit's lamps over the ring below
        if T.detail:
            for t in (.25, .75):
                ball(g, ((a[0] + (bq[0] - a[0]) * t) * .86 + cx * .14, deck0 - .03, (a[1] + (bq[1] - a[1]) * t) * .86 + cz * .14), .09, F[lamp], 6, 3, .6)
    eave = P['rideEave']
    cols = ngon(cx, cz, 3.75, 8, 0)
    for (x, z) in cols:
        if carousel:
            rod(g, (x, deck1, z), (x, eave, z), .045, F['brass'], T.small)
        else:
            lathe(g, [(.16, deck1), (.16, deck1 + .12), (.1, deck1 + .2), (.085, eave - .3), (.11, eave - .22), (.17, eave - .12), (.17, eave)], (x, 0, z), F['cream'], T.small + 2)
            lathe(g, [(.172, eave - .14), (.18, eave - .08), (.172, eave - .02)], (x, 0, z), F['gold'], T.small + 2)
    # the eave: a ring beam with bulbs
    ev = ngon(cx, cz, 4.2, 8, n8, apothem=True)
    ev_in = ngon(cx, cz, 3.95, 8, n8, apothem=True)
    for i in range(8):
        a, bq, ia, ib = ev[i], ev[(i + 1) % 8], ev_in[i], ev_in[(i + 1) % 8]
        nx, nz = (a[0] + bq[0]) / 2 - cx, (a[1] + bq[1]) / 2 - cz
        ln = math.hypot(nx, nz)
        nx, nz = nx / ln, nz / ln
        g.face(((a[0], eave, a[1]), (bq[0], eave, bq[1]), (bq[0], eave + .34, bq[1]), (a[0], eave + .34, a[1])), F['gold'], normal=(nx, 0, nz))
        g.face(((ia[0], eave, ia[1]), (ib[0], eave, ib[1]), (ib[0], eave + .34, ib[1]), (ia[0], eave + .34, ia[1])), F['golddim'], normal=(-nx, 0, -nz))
        g.face(((a[0], eave, a[1]), (bq[0], eave, bq[1]), (ib[0], eave, ib[1]), (ia[0], eave, ia[1])), F['golddim'], normal=(0, -1, 0))
        bulbs((a[0] + nx * .07, eave + .17, a[1] + nz * .07), (bq[0] + nx * .07, eave + .17, bq[1] + nz * .07), .3, T)
        # the scalloped valance under the eave
        m = 6
        for k in range(m):
            t0, t1 = k / m, (k + 1) / m
            p0 = (a[0] + (bq[0] - a[0]) * t0, a[1] + (bq[1] - a[1]) * t0)
            p1 = (a[0] + (bq[0] - a[0]) * t1, a[1] + (bq[1] - a[1]) * t1)
            f = F[('canvasred' if carousel else 'canvasteal') if (k + i) % 2 == 0 else 'canvas']
            pts = [(p0[0], eave, p0[1]), (p1[0], eave, p1[1]), ((p0[0] * .25 + p1[0] * .75), eave - .2, (p0[1] * .25 + p1[1] * .75)), ((p0[0] + p1[0]) / 2, eave - .28, (p0[1] + p1[1]) / 2),
                   ((p0[0] * .75 + p1[0] * .25), eave - .2, (p0[1] * .75 + p1[1] * .25))]
            g.face(pts, f, normal=(nx, 0, nz))
            g.face(pts, f, normal=(-nx, 0, -nz))
    if carousel:
        # the canopy: sixteen leaves in red and cream sagging to the crown, a gold crown, a ball and a lamp
        cone(g, cx, eave + .34, eave + 2.05, cz, 4.2 / math.cos(n8), .45, 16, [F['canvasred'], F['canvas']], n8, sag=.22, steps=(4, 2, 1)[T.i])
        lathe(g, [(.5, eave + 1.98), (.56, eave + 2.1), (.46, eave + 2.2), (.2, eave + 2.36), (.08, eave + 2.5), (.05, eave + 2.9)], (cx, 0, cz), F['gold'], T.round)
        ball(g, (cx, eave + 3.0, cz), .16, F['globe'], 8, 4)
    else:
        # the dome: a bell of teal leaves with gold ribs, a lantern; the star (the rig's) turns over it
        prof = [(4.15, eave + .34), (4.05, eave + .7), (3.7, eave + 1.2), (3.05, eave + 1.75), (2.2, eave + 2.2), (1.3, eave + 2.52), (.62, eave + 2.7), (.5, eave + 2.76)]
        lathe(g, prof, (cx, 0, cz), F['teal'], (24, 16, 8)[T.i])
        if T.detail:
            for k in range(8):
                a = k * math.pi / 4 + n8
                sweep(g, [(-.05, 0), (.05, 0), (0, .06)], [(cx + math.sin(a) * (r + .01), y, cz + math.cos(a) * (r + .01)) for r, y in prof], F['gold'], caps=False, smooth_u=False)
        lathe(g, [(.5, eave + 2.76), (.56, eave + 2.86), (.42, eave + 2.92), (.42, eave + 3.3), (.5, eave + 3.36), (.3, eave + 3.5), (.06, eave + 3.62), (.05, P['starY'] - 1.45)], (cx, 0, cz), F['gold'], T.round)
        for k in range(4):
            a = k * math.pi / 2 + n8 * 2
            box(g, cx + math.sin(a) * .43 - .1, cx + math.sin(a) * .43 + .1, eave + 2.98, eave + 3.26, cz + math.cos(a) * .43 - .1, cz + math.cos(a) * .43 + .1, F[lamp], None, 0, 0)


def mast(g, b, T, P):
    """A drop tower's mast: a riveted steel tube in red and white bands, a guide rail up each face, lamps at the corners; over
    it (overhead) its crown and machinery."""
    e = E(b)
    x0, x1, y0, y1, z0, z1 = e
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    y = y0
    k = 0
    while y < y1 - 1e-6:
        yb = min(y1, y + 2)
        box(g, x0 + .03, x1 - .03, y, yb, z0 + .03, z1 - .03, F['ironred' if k % 2 == 0 else 'ironwhite'], T, .05, 2.0 if T.i == 0 else 0, skip=('-y', '+y') if 0 < k and yb < y1 else ())
        if T.detail:
            for s in (0, 1, 2, 3):
                c, sg, ax, a0, a1 = side(e, s)
                slab(g, e, s, a0 + .02, a1 - .02, yb - .06, yb + .0, .03, F['iron'], T, -.03, .01, 0)
        y = yb
        k += 1
    for s in (0, 1, 2, 3):
        c, sg, ax, a0, a1 = side(e, s)
        m = (a0 + a1) / 2
        slab(g, e, s, m - .09, m + .09, y0, y1, .07, F['iron'], T, 0, .01, 4.0)
        if T.i < 2:
            for q in (a0 + .1, a1 - .1):
                yy = y0 + 1.0
                while yy < y1:
                    ball(g, pt(e, s, q, yy, .03), .06, F['lampred' if int(yy) % 4 < 2 else 'globe'], 5, 2)
                    yy += 2.0
    # the drop square painted on the boards: a gold border and chevrons toward the mast
    r = 2.3
    for sx, sz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        if sx:
            box(g, cx + sx * r - .07, cx + sx * r + .07, 0, .008, cz - r, cz + r, F['gold'], None, 0, 0, skip=('-y',))
        else:
            box(g, cx - r + .07, cx + r - .07, 0, .008, cz + sz * r - .07, cz + sz * r + .07, F['gold'], None, 0, 0, skip=('-y',))
    if T.i < 2:
        for i in range(4):
            a = i * math.pi / 2
            for k in range(3):
                d = 1.05 + k * .4
                p = (cx + math.sin(a) * d, cz + math.cos(a) * d)
                tx, tz = math.cos(a), -math.sin(a)
                g.face(((p[0] - tx * .45, .008, p[1] - tz * .45), (p[0] + tx * .45, .008, p[1] + tz * .45), (p[0] + tx * .45 + math.sin(a) * .12, .008, p[1] + tz * .45 + math.cos(a) * .12),
                        (p[0] - tx * .45 + math.sin(a) * .12, .008, p[1] - tz * .45 + math.cos(a) * .12)), F['ironred'], normal=(0, 1, 0))
    # the crown
    box(g, x0 - .25, x1 + .25, y1, y1 + .3, z0 - .25, z1 + .25, F['iron'], T, .04)
    box(g, x0 - .05, x1 + .05, y1 + .3, y1 + 1.5, z0 - .05, z1 + .05, F['ironred'], T, .08)
    lathe(g, [(.78, y1 + 1.5), (.84, y1 + 1.62), (.6, y1 + 1.8), (.3, y1 + 2.05), (.12, y1 + 2.2), (.08, y1 + 3.0)], (cx, 0, cz), F['gold'], T.round)
    for s in (0, 1, 2, 3):
        c, sg, ax, a0, a1 = side((x0 - .05, x1 + .05, y1 + .3, y1 + 1.5, z0 - .05, z1 + .05), s)
        bulbs(pt((x0 - .05, x1 + .05, 0, 0, z0 - .05, z1 + .05), s, a0 + .15, y1 + .9, .07), pt((x0 - .05, x1 + .05, 0, 0, z0 - .05, z1 + .05), s, a1 - .15, y1 + .9, .07), .28, T)


def planter(g, b, T):
    """A planter of green boards with shrubs and, standing in it, a cast-iron lamp post with two globes (thin; overhead)."""
    e = E(b)
    x0, x1, y0, y1, z0, z1 = e
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    box(g, x0 + .03, x1 - .03, y0, y1 - .06, z0 + .03, z1 - .03, F['green'], T, .01)
    box(g, x0, x1, y1 - .1, y1, z0, z1, F['cratedark'], T, .02)
    box(g, x0 + .08, x1 - .08, y1 - .02, y1 + .01, z0 + .08, z1 - .08, F['soil'], None, 0, 0, skip=('-y',))
    for s in (0, 1, 2, 3):
        c, sg, ax, a0, a1 = side(e, s)
        for q in (a0, a1 - .1):
            slab(g, e, s, q, q + .1, y0, y1 - .1, .03, F['cratedark'], T, -.03)
        slab(g, e, s, a0 + .1, a1 - .1, y0 + .02, y0 + .14, .02, F['cratedark'], T, -.03)
    lx = x1 - x0 >= z1 - z0
    ln = (x1 - x0) if lx else (z1 - z0)
    if T.i < 2:
        u = .3
        k = 0
        while u < ln - .2:
            if abs(u - ln / 2) > .25:
                r = .1 + .06 * hashf(k, int(x0 * 10), int(z0 * 10))
                px, pz = (x0 + u, cz) if lx else (cx, z0 + u)
                ball(g, (px, y1 + r * .7, pz), r, F['leaf'], 6, 3, .9)
                if k % 2 == 0 and T.i == 0:
                    ball(g, (px + .05, y1 + r * 1.5, pz + .03), .04, F[('red', 'lilac', 'gold', 'white')[k % 4]], 5, 2)
            u += .3
            k += 1
    # the lamp post
    n = T.small
    lathe(g, [(.07, y1), (.075, y1 + .1), (.05, y1 + .2), (.035, y1 + .5), (.03, y1 + 2.1), (.045, y1 + 2.16), (.03, y1 + 2.26)], (cx, 0, cz), F['iron'], n)
    for q in (-1, 1):
        ox, oz = (0, q * .3) if lx else (q * .3, 0)
        path = [(cx, y1 + 2.05, cz), (cx + ox * .5, y1 + 2.22, cz + oz * .5), (cx + ox, y1 + 2.2, cz + oz)]
        sk.tube(g, sk.catmull(path, 3) if T.i == 0 else path, .018, F['iron'], 4, caps=False)
        ball(g, (cx + ox, y1 + 2.37, cz + oz), .15, F['globe'], (10, 8, 6)[T.i], (5, 4, 3)[T.i])
        lathe(g, [(.05, y1 + 2.2), (.07, y1 + 2.24), (.03, y1 + 2.27)], (cx + ox, 0, cz + oz), F['iron'], 6)
        lathe(g, [(.06, y1 + 2.5), (.02, y1 + 2.56), (.005, y1 + 2.62)], (cx + ox, 0, cz + oz), F['iron'], 6)


def crate(g, b, T):
    e = E(b)
    x0, x1, y0, y1, z0, z1 = e
    box(g, x0 + .03, x1 - .03, y0, y1 - .0, z0 + .03, z1 - .03, F['crate'], T, .01)
    for s in (0, 1, 2, 3):
        c, sg, ax, a0, a1 = side(e, s)
        for q in (a0, a1 - .12):
            slab(g, e, s, q, q + .12, y0, y1, .03, F['cratedark'], T, -.03)
        y = y0
        while y < y1 - .2:
            slab(g, e, s, a0 + .12, a1 - .12, y, y + .1, .025, F['cratedark'], T, -.03)
            top = min(y1, y + 1.15)
            slab(g, e, s, a0 + .12, a1 - .12, top - .1, top, .025, F['cratedark'], T, -.03)
            if T.i == 0:
                # a stencilled star
                m, my = (a0 + a1) / 2, (y + top) / 2
                star = [(m + .2 * (1 if j % 2 == 0 else .42) * math.sin(j * math.pi / 5), my + .2 * (1 if j % 2 == 0 else .42) * math.cos(j * math.pi / 5)) for j in range(10)]
                for j in range(10):
                    g.face([pt(e, s, m, my, -.021), pt(e, s, *star[j], -.021), pt(e, s, *star[(j + 1) % 10], -.021)], F['red'], normal=(sg, 0, 0) if ax == 'z' else (0, 0, sg))
            y += 1.15


def cart(g, b, T):
    """A sweet cart: a striped body on spoked wheels, a brass rail, trays of sweets under glass in its top."""
    e = E(b)
    x0, x1, y0, y1, z0, z1 = e
    box(g, x0 + .04, x1 - .04, y0 + .28, y1 - .04, z0 + .04, z1 - .04, F['cream'], T, .03)
    box(g, x0, x1, y1 - .06, y1, z0, z1, F['brass'], T, .02)
    box(g, x0 + .1, x1 - .1, y1 - .01, y1 + .012, z0 + .1, z1 - .1, F['glasspink'], None, 0, 0, skip=('-y',))
    for s in (0, 1, 2, 3):
        c, sg, ax, a0, a1 = side(e, s)
        u = a0 + .2
        while u < a1 - .1:
            slab(g, e, s, u - .15, min(a1 - .04, u + .15), y0 + .3, y1 - .08, .012, F['red'], T, -.04, 0, 0)
            u += .6
    lx = x1 - x0 >= z1 - z0
    for s in ((2, 3) if lx else (0, 1)):
        c, sg, ax, a0, a1 = side(e, s)
        for u in (a0 + .42, a1 - .42):
            p = pt(e, s, u, y0 + .38, .0)
            axis = (0, 0, sg) if ax == 'x' else (sg, 0, 0)
            lathe(g, [(.02, -.02), (.38, -.02), (.38, .05), (.3, .05), (.3, .02), (.06, .02), (.06, .08), (.02, .08)], p, F['red'], (14, 10, 8)[T.i], axis=axis)


def signal(g, b, T):
    """A crossing signal: a banded post on a base, a crossbuck and a bell (overhead); the wig-wag lamps are the rig's."""
    e = E(b)
    x0, x1, y0, y1, z0, z1 = e
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    lathe(g, [(.2, y0), (.2, y0 + .16), (.12, y0 + .3)], (cx, 0, cz), F['iron'], T.round)
    y = y0 + .3
    k = 0
    while y < y1 - .01:
        yb = min(y1, y + .4)
        rod(g, (cx, y, cz), (cx, yb, cz), .085, F['ironwhite' if k % 2 == 0 else 'iron'], T.small + 2, caps=yb >= y1)
        y = yb
        k += 1
    # the lamps' bracket and hoods (the lamps themselves are the rig's), the crossbuck toward the piers, the bell
    box(g, cx - .44, cx + .44, 2.3, 2.4, cz - .04, cz + .04, F['iron'], T, .01)
    for q in (-1, 1):
        lathe(g, [(.16, -.07), (.17, .0), (.16, .07)], (cx + q * .3, 2.35, cz), F['black'], (10, 8, 6)[T.i], axis=(0, 0, 1))
    zf = cz + (.1 if cz < 0 else -.1) * -1
    for q in (-1, 1):
        sk.obox(g, (cx, y1 - .45, zf), (.62, .075, .02), sk.quat_axis(0, 0, 1, q * .6), F['ironwhite'], T, .01)
        sk.obox(g, (cx, y1 - .45, zf + (.021 if cz < 0 else -.021) * -1), (.6, .03, .004), sk.quat_axis(0, 0, 1, q * .6), F['ironred'], None, 0)
    lathe(g, [(.02, y1), (.13, y1 + .02), (.14, y1 + .08), (.1, y1 + .16), (.03, y1 + .2)], (cx, 0, cz), F['brass'], T.round)


def fence(g, b, T, L):
    """A rail: a boarded panel between posts with turned caps, a moulded hand rail (what looks like cover is cover)."""
    e = E(b)
    x0, x1, y0, y1, z0, z1 = e
    low = y0 < -1
    f = F['teal' if low else 'cream']
    along_x = x1 - x0 >= z1 - z0
    a0, a1 = (x0, x1) if along_x else (z0, z1)
    t0, t1 = (z0, z1) if along_x else (x0, x1)
    m = (t0 + t1) / 2

    def bx(u0, u1, v0, v1, w0, w1, ff, bev=0):
        if along_x:
            box(g, u0, u1, v0, v1, w0, w1, ff, T, bev, 1.5 if T.i == 0 else 0)
        else:
            box(g, w0, w1, v0, v1, u0, u1, ff, T, bev, 1.5 if T.i == 0 else 0)
    bx(a0, a1, y0 + .1, y1 - .13, m - .045, m + .045, f)
    bx(a0, a1, y0, y0 + .12, t0, t1, F['navy' if not low else 'cratedark'])
    # the hand rail
    sec = [(-.115, 0), (.115, 0), (.115, .05), (.07, .09), (-.07, .09), (-.115, .05)] if T.i < 2 else [(-.115, 0), (.115, 0), (.115, .09), (-.115, .09)]
    p0, p1 = ((a0, y1 - .09, m), (a1, y1 - .09, m)) if along_x else ((m, y1 - .09, a0), (m, y1 - .09, a1))
    sweep(g, sec, [p0, p1], F['crate'], up=(0, 1, 0), smooth_u=False)
    n = max(1, int(round((a1 - a0) / 1.6)))
    for k in range(n + 1):
        u = min(a1 - .1, max(a0 + .1, a0 + (a1 - a0) * k / n))
        bx(u - .1, u + .1, y0, y1 - .02, t0, t1, F['red' if not low else 'cratedark'], .02 if T.i == 0 else 0)
        px, pz = (u, m) if along_x else (m, u)
        if T.i == 0:
            lathe(g, [(.11, y1 - .02), (.12, y1 + .03), (.05, y1 + .08), (.085, y1 + .14), (.005, y1 + .215)], (px, 0, pz), F['gold'], 6)
        else:
            lathe(g, [(.11, y1 - .02), (.11, y1 + .04), (.005, y1 + .2)], (px, 0, pz), F['gold'], 4, phase=math.pi / 4, smooth=False)
    if T.i == 0:
        # the panel's boards: grooves between them
        u = a0 + .12
        while u < a1 - .1:
            for w in (t0 + .053, t1 - .053):
                if along_x:
                    g.quad((u - .006, y0 + .14, w), (u + .006, y0 + .14, w), (u + .006, y1 - .12, w), (u - .006, y1 - .12, w), F['gap'], (0, 0, 1 if w > m else -1))
                else:
                    g.quad((w, y0 + .14, u - .006), (w, y0 + .14, u + .006), (w, y1 - .12, u + .006), (w, y1 - .12, u - .006), F['gap'], (1 if w > m else -1, 0, 0))
            u += .22


def step(g, b, T):
    """A stair's step: a board tread with a rounded nosing over a riser."""
    x0, x1, y0, y1, z0, z1 = E(b)
    f = F['deck%d' % (int(round(y1 * 5)) % 3)]
    box(g, x0, x1, y0, y1 - .04, z0, z1, F['joist'], T, 0, None if y1 - y0 > .5 else 0)
    box(g, x0 - .0, x1 + .0, y1 - .04, y1, z0, z1, f, T, .012, 0)


def pile(g, b, T):
    """A pile (round timber in its square collider) or the caisson (banded concrete), weed at the water line."""
    e = E(b)
    x0, x1, y0, y1, z0, z1 = e
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    sea = -4.6
    if x1 - x0 > 2:
        box(g, x0, x1, y0, y1, z0, z1, F['concrete'], T, .08)
        for y in (-1.2, -2.2, sea + .9):
            for s in (0, 1, 2, 3):
                c, sg, ax, a0, a1 = side(e, s)
                slab(g, e, s, a0, a1, y, y + .16, .03, F['iron'], T)
        for s in (0, 1, 2, 3):
            c, sg, ax, a0, a1 = side(e, s)
            slab(g, e, s, a0, a1, sea - .4, sea + .5, .02, F['weed'], T, 0, 0)
        return
    r = (x1 - x0) / 2
    lathe(g, [(r, y0), (r, sea - .3), (r * .96, sea + .6), (r * .92, y1)], (cx, 0, cz), F['pile'], (12, 8, 6)[T.i])
    lathe(g, [(r + .012, sea - .5), (r + .02, sea + .1), (r + .005, sea + .7)], (cx, 0, cz), F['weed'], (12, 8, 6)[T.i])
    for y in (-1.0, -2.9):
        lathe(g, [(r * .93 + .012, y), (r * .93 + .03, y + .03), (r * .93 + .03, y + .13), (r * .93 + .012, y + .16)], (cx, 0, cz), F['iron'], (12, 8, 6)[T.i])


# ---------------------------------------------------------------------------------------------------------------------------
# Overhead, under the boards, past the edges
# ---------------------------------------------------------------------------------------------------------------------------
def strings(g, P, T):
    """The strings of bulbs, each sagging between two high points; the north pier's are the south's turned half round."""
    S = P['strings']
    for turn in (1, -1):
        for k in range(len(S) // 7):
            a, bq, sag = S[k * 7:k * 7 + 3], S[k * 7 + 3:k * 7 + 6], S[k * 7 + 6]
            p0, p1 = (turn * a[0], a[1], turn * a[2]), (turn * bq[0], bq[1], turn * bq[2])
            path = hk.catenary(p0, p1, sag, (12, 8, 4)[T.i])
            sk.tube(g, path, .012, F['black'], 3, caps=False)
            ln = sum(math.dist(path[i], path[i + 1]) for i in range(len(path) - 1))
            n = max(2, int(ln / .55))
            for i in range(1, n):
                q = hk.catenary(p0, p1, sag, n)[i]
                bulb(q[0], q[1] - .07, q[2], i, r=.06, tier=None)


def under(g, P, T):
    """The pile bents under the boards: round piles, cross braces, the cap beams; weed at the water line."""
    sea = P['sea']
    foot = sea - 2.2
    n = (10, 6, 5)[T.i]
    for z in P['pileZ']:
        xs = P['pileX']
        for i, x in enumerate(xs):
            top = -1.04 if (abs(x) < 10 and abs(z) > 25) else P['under'] - .04
            lathe(g, [(.24, foot), (.24, sea - .3), (.225, sea + .7), (.21, top)], (x, 0, z), F['pile'], n)
            lathe(g, [(.25, sea - .5), (.262, sea + .1), (.235, sea + .7)], (x, 0, z), F['weed'], n)
            if i + 1 < len(xs) and not (xs[i] < 0 < xs[i + 1]):
                x1 = xs[i + 1]
                for ya, yb in ((-1.3, -3.6), (-3.6, -1.3)):
                    sweep(g, sk.rect_section(.07, .2), [(x, ya, z + (.26 if ya > yb else -.26)), (x1, yb, z + (.26 if ya > yb else -.26))], F['joist'], up=(0, 0, 1), smooth_u=False)
        for xa, xb in ((-18.3, -5.7), (5.7, 18.3)):
            box(g, xa, xb, -1.27, -1.08, z - .15, z + .15, F['joist'], T, .02, 3.0)


def beyond(g, L, T, P):
    """The track on past both edges to the play space's margin: bare rails on open sleepers over the water."""
    t = next(E(b) for b in L['blocks'] if b['id'] == 'harbor-track')
    far = L['bounds']['maxX'] + 1.0
    for s in (-1, 1):
        xa, xb = (t[1] + .06, far) if s > 0 else (-far, t[0] - .06)
        for z in (-.72, .72):
            rail(g, [(xa, t[3], z), (xb, t[3], z)], T)
        x = math.ceil(min(abs(xa), abs(xb)) * 2) / 2 + .25
        while x < far - .1:
            box(g, s * x - .1, s * x + .1, t[3] - .15, t[3], -1.1, 1.1, F['joist'], T, .015, 0)
            x += .5 if T.i == 0 else 1.0
        for z in (-.95, .95):
            box(g, xa, xb, t[3] - .42, t[3] - .15, z - .09, z + .09, F['ironred'], T, .02, 2.0)


# ---------------------------------------------------------------------------------------------------------------------------
# The near backdrop (named chunks, one tier): the wheel's frame, the coaster's trestles and spikes, the neck, the pier head
# ---------------------------------------------------------------------------------------------------------------------------
def track_at(s, half, P):
    sg = -1 if s < 0 else 1
    d = abs(s) - half
    ry = P['railY']
    if d <= P['trackLevel']:
        return (s, ry, 0.0)
    arc = P['trackR'] * P['trackTilt']
    e = d - P['trackLevel']
    if e <= arc:
        f = e / P['trackR']
        return (sg * (half + P['trackLevel'] + P['trackR'] * math.sin(f)), ry + P['trackR'] * (1 - math.cos(f)), sg * f)
    e -= arc
    tl = P['trackTilt']
    return (sg * (half + P['trackLevel'] + P['trackR'] * math.sin(tl) + e * math.cos(tl)), ry + P['trackR'] * (1 - math.cos(tl)) + e * math.sin(tl), sg * tl)


def wheel_frame(L, T, P):
    g = Geo()
    cx, cy, gp = P['wheelX'], P['wheelY'], P['wheelGap'] + 1.6
    sea = P['sea']
    for sd in (-1, 1):
        # the landing stage: boards on piles, a rail round it
        box(g, cx - 6.5, cx + 6.5, -.4, 0, sd * 2.2, sd * 12.6, F['deck0'], T, .02, 3.0)
        for x in (cx - 5.5, cx, cx + 5.5):
            for z in (3.4, 7.4, 11.4):
                lathe(g, [(.24, sea - 2.2), (.22, -.4)], (x, 0, sd * z), F['pile'], 6)
        box(g, cx - 6.5, cx + 6.5, 0, 1.1, sd * 12.5 - .08, sd * 12.5 + .08, F['cream'], T, .02, 0)
        box(g, cx - 6.5, cx - 6.34, 0, 1.1, min(sd * 2.2, sd * 12.5), max(sd * 2.2, sd * 12.5), F['cream'], T, .02, 0)
        # the A-frames' legs: tapered box columns to the axle, feet, their lamps
        for f in (-1, 1):
            x = cx + f * gp
            path = [(x, 0, sd * 10.4), (x, cy, 0)]
            sweep(g, sk.rect_section(.8, .55, .08), path, F['ironwhite'], up=(1, 0, 0), smooth_u=False)
            box(g, x - .8, x + .8, 0, .7, sd * 10.4 - .8, sd * 10.4 + .8, F['iron'], T, .06, 0)
            for k in range(1, 14):
                u = k / 14.0
                ball(g, (x + f * .46, u * cy, sd * 10.4 * (1 - u)), .11, F['globe'], 5, 2)
        for k in (1, 2, 3):
            u = k / 4.0
            rod(g, (cx - gp, u * cy, sd * 10.4 * (1 - u)), (cx + gp, u * cy, sd * 10.4 * (1 - u)), .14, F['ironwhite'], 6)
    for f in (-1, 1):
        for k in (1, 2):
            u = k / 3.0
            rod(g, (cx + f * gp, u * cy, -10.4 * (1 - u)), (cx + f * gp, u * cy, 10.4 * (1 - u)), .14, F['ironwhite'], 6)
    lathe(g, [(.5, -gp - .6), (.62, -gp - .4), (.62, gp + .4), (.5, gp + .6)], (cx, cy, 0), F['iron'], 12, axis=(1, 0, 0), cap0=True, cap1=True)
    # the ticket hut and the queue's lamps
    box(g, cx + 2.7, cx + 5.3, 0, 2.8, 7.0, 9.4, F['red'], T, .04, 0)
    cone(g, cx + 4, 2.8, 3.9, 8.2, 2.1, .1, 4, [F['canvas'], F['canvasred']], math.pi / 4)
    box(g, cx + 3.3, cx + 4.7, 1.2, 2.1, 6.96, 7.0, F['glass'], None, 0, 0)
    for k in range(7):
        ball(g, (cx - 6 + k * 2, 2.5, -12.5), .14, F['glasspink' if k % 2 == 0 else 'glasscyan'], 5, 2)
        rod(g, (cx - 6 + k * 2, 0, -12.5), (cx - 6 + k * 2, 2.4, -12.5), .03, F['iron'], 4)
    return g


def spikes(L, T, P):
    """The trestle past each edge (from the play space's margin) and the spike it climbs: rails, sleepers, a red spine, bents
    of white tube braced in X, lamps up the edges, a stop at the top."""
    g = Geo()
    half = 20.0
    sea = P['sea']
    for sd in (-1, 1):
        pts = []
        d = 5.0
        while d <= P['trackTop'] + .01:
            pts.append(track_at(sd * (half + d), half, P))
            d += 1.25 if d > P['trackLevel'] - 1 else 3.0
        for z in (-.72, .72):
            rail(g, [(x, y, z) for x, y, _ in pts], sk.Tier(1))
        for z in (-.95, .95):
            sweep(g, sk.rect_section(.18, .28), [(x, y - .29, z) for x, y, _ in pts], F['ironred'], up=(0, 1, 0), smooth_u=False)
        d = 5.25
        k = 0
        while d <= P['trackTop']:
            x, y, pitch = track_at(sd * (half + d), half, P)
            sk.obox(g, (x, y - .08, 0), (.1, .07, 1.1), sk.quat_axis(0, 0, 1, pitch), F['joist'], None, 0)
            if d > P['trackLevel'] and k % 3 == 0:
                for z in (-1.14, 1.14):
                    ball(g, (x, y - .3, z), .11, F['globe' if k % 6 == 0 else 'glasscyan'], 5, 2)
            d += 1.0
            k += 1
        # the bents
        last = None
        d = 6.5
        while d <= P['trackTop'] - 1:
            x, y, _ = track_at(sd * (half + d), half, P)
            top, foot = y - .45, sea - 2.4
            w = lambda yy: 1.25 * (1 + .012 * (yy - foot))  # noqa: E731
            for q in (-1, 1):
                rod(g, (x, foot, q * w(foot)), (x, top, q * w(top)), .15, F['ironwhite'], 6)
            yb = sea + 1.2
            while yb < top - 2.2:
                rod(g, (x, yb, -w(yb)), (x, yb + 3.6, w(yb + 3.6)), .06, F['ironwhite'], 4, False)
                rod(g, (x, yb, w(yb)), (x, yb + 3.6, -w(yb + 3.6)), .06, F['ironwhite'], 4, False)
                yb += 4.4
            if last is not None and top > 4:
                for q in (-1, 1):
                    rod(g, (last[0], max(sea + 1, last[1] - 9), q * 1.25), (x, top - 1, q * 1.25), .06, F['ironwhite'], 4, False)
                    rod(g, (last[0], last[1] - 1, q * 1.25), (x, max(sea + 1, top - 9), q * 1.25), .06, F['ironwhite'], 4, False)
            last = (x, top)
            d += 5 if d < P['trackLevel'] else 3.4
        x, y, pitch = track_at(sd * (half + P['trackTop']), half, P)
        sk.obox(g, (x, y + .5, 0), (.25, .7, 1.2), sk.quat_axis(0, 0, 1, pitch), F['ironred'], None, 0)
        ball(g, (x, y + 1.5, 0), .3, F['lampred'], 8, 4)
    return g


def neck(L, T, P):
    """The boardwalk south from the Gate to the shore with its rails and lamp posts; the landing stage off the pier head."""
    g = Geo()
    sea = P['sea']
    shore = P['shoreZ']
    box(g, -6, 6, -.4, 0, shore, -39.2, F['deck1'], T, .02, 4.0)
    z = -46.0
    while z > shore:
        for x in (-5.2, 5.2):
            lathe(g, [(.24, sea - 2.2), (.22, -.4)], (x, 0, z), F['pile'], 6)
        z -= 6
    for sd in (-1, 1):
        box(g, sd * 5.9 - .07, sd * 5.9 + .07, 0, 1.1, shore, -39.7, F['cream'], T, .02, 0)
        z = -46.0
        while z > shore + 4:
            lathe(g, [(.08, 0), (.05, .3), (.035, 3.6), (.05, 3.7)], (sd * 5.6, 0, z), F['iron'], 6)
            ball(g, (sd * 5.6, 3.9, z), .22, F['globe'], 8, 4)
            z -= 9
    # the pier head's landing and its flag pole
    box(g, -4.5, 4.5, -.4, 0, 39.2, 45.2, F['deck1'], T, .02, 3.0)
    for x in (-4, 4):
        for z in (40.0, 44.4):
            lathe(g, [(.24, sea - 2.2), (.22, -.4)], (x, 0, z), F['pile'], 6)
    rod(g, (0, 0, 44.6), (0, 7.4, 44.6), .06, F['ironwhite'], 6)
    ball(g, (0, 7.5, 44.6), .12, F['gold'], 6, 3)
    g.face(((0.06, 7.2, 44.6), (1.4, 7.0, 44.7), (1.4, 6.3, 44.6), (0.06, 6.4, 44.6)), F['canvasred'], normal=(0, 0, 1))
    g.face(((0.06, 7.2, 44.6), (1.4, 7.0, 44.7), (1.4, 6.3, 44.6), (0.06, 6.4, 44.6)), F['canvasred'], normal=(0, 0, -1))
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The scene
# ---------------------------------------------------------------------------------------------------------------------------
def static_scene(L, T):
    P = L['pier']
    g = Geo()
    hk._count[0] = 0
    for m in hk.MARQUEE:
        m.__init__()
    done = set()
    for b in L['blocks']:
        i = b['id']
        if i == 'harbor-track':
            track(g, b, T)
        elif any(i.startswith('harbor-' + p) for p in ('deck-', 'bridge-', 'landing-', 'catwalk-', 'crown-')) or i.endswith('-floor'):
            boards(g, b, T, seed=int(abs(b['pos'][0] * 3 + b['pos'][2])))
        elif i.startswith('harbor-hall-'):
            wall(g, b, T, L)
        elif i.startswith('harbor-kiosk-'):
            kiosk(g, b, T)
        elif i.startswith('harbor-turret-'):
            turret(g, b, T)
        elif i.startswith('harbor-sign-'):
            sign(g, b, T)
        elif i.startswith('harbor-stall-'):
            stem = '-'.join(i.split('-')[:4])
            if stem not in done:
                done.add(stem)
                stall(g, L, stem, T)
        elif i.startswith('harbor-booth-'):
            booth(g, b, T)
        elif i.startswith('harbor-ride-'):
            stem = '-'.join(i.split('-')[:3])
            if stem not in done:
                done.add(stem)
                ride(g, L, stem, T, P)
        elif i.startswith('harbor-tower-'):
            mast(g, b, T, P)
        elif i.startswith('harbor-planter-'):
            planter(g, b, T)
        elif i.startswith('harbor-crate-'):
            crate(g, b, T)
        elif i.startswith('harbor-cart-'):
            cart(g, b, T)
        elif i.startswith('harbor-signal-'):
            signal(g, b, T)
        elif i.startswith('harbor-rail-'):
            fence(g, b, T, L)
        elif i.startswith('harbor-stair-'):
            step(g, b, T)
        elif i == 'harbor-caisson' or i.startswith('harbor-pile-'):
            pile(g, b, T)
        else:
            raise ValueError('no model for block ' + i)
    arch_fill(g, L, T)
    strings(g, P, T)
    under(g, P, T)
    beyond(g, L, T, P)
    return g


VIEWS = [('wide-se', (34, 22, -44), (-4, 2, -4), 60), ('midway', (0, 1.8, -23.5), (0, 2.5, 0), 70), ('carousel', (-3, 2.0, -21), (-10, 3.6, -15), 70),
         ('wheel', (6, 1.8, -7.5), (-34, 22, 0), 70), ('channel', (18.5, 2.2, -6.6), (-10, .5, 0), 70), ('tower', (3.5, 1.7, -9.5), (10, 7, -15), 75),
         ('band', (3, 2.0, 21), (10, 4.2, 15), 70), ('under', (0, -1.2, -12.5), (0, -2, 4), 75), ('terrace', (-6, 5.9, -31), (2, 3, 10), 70),
         ('stall', (11.5, 1.7, -17), (18.6, 1.6, -23.2), 65), ('gate', (0, 1.7, -17), (0, 3.4, -26), 75), ('hall', (6, 1.7, -32), (-4, 1.8, -27), 80),
         ('planter', (2.5, 1.6, -21.5), (5.2, 1.4, -18.8), 60), ('rail', (-16, 1.6, -30), (-20, .8, -32.5), 60), ('top', (0, 60, -17.01), (0, 0, -17), 55),
         ('spike', (-10, 1.8, 8), (70, 22, 24), 60), ('wheelframe', (-10, 3, -16), (-34, 14, 0), 70)]


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
    ap.add_argument('--stats', action='store_true')
    ap.add_argument('--own-mid', action='store_true', help='a Mid tier of its own for the static scene (over the budget)')
    args = ap.parse_args(argv)
    L = sk.load_layout(args.layout)
    P = L['pier']
    hk.finishes()
    import harbor_props as props
    sb = sk.StageBank()
    chunks = sk.Chunks(12.0, (-36.0, -48.0), (-1.0, 9.0))
    t0 = time.time()
    for ti in [int(x) for x in args.tiers.split(',')]:
        T = sk.Tier(ti)
        t = time.time()
        g = static_scene(L, T)
        marquee = [Geo() for _ in range(3)]
        for k in range(3):
            marquee[k].extend(hk.MARQUEE[k])
        print('  coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % sk.lift_coplanar(g, eps=max(sk.NEAR_TIER[ti], .0055), lift=sk.LIFT_TIER[ti], passes=8))
        print('tier %s: static %d faces, bulbs %d, %.1f s' % (T.name, g.faces, sum(m.faces for m in marquee) // 8, time.time() - t))
        ao = None
        if not args.no_ao:
            # an open-air stage at night: the occlusion reaches a metre and is eased
            ao = 1.0 - (1.0 - sk.bake_ao([g], distance=(1.1, 1.1, 1.3)[ti], samples=(32, 16, 12)[ti])[0]) * .75
        bk = sk.Bake(g, ao, floors=FLOORS, seed=9)
        ids = chunks.ids(g)
        for cid in np.unique(ids):
            om = sk.to_mesh('static/%d/%d' % (cid, ti), g, bk, ids == cid)
            if ti == 0 and not args.own_mid:
                # the Mid tier's props share the Fine meshes: every chunk has its three props, nothing is stored twice
                mi = sb.mesh(om)
                sb.bank.prop('static/%d/0' % cid, 'static', [(mi, [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0])])
                sb.share('static/%d/1' % cid, 'static', mi)
                st = sb.stats.setdefault(('static', '0'), [0, 0, 0])
                st[0] += 1
                st[1] += sum(len(x) for x in om.submeshes) // 3
                st[2] += len(om.positions)
            elif ti != 1 or args.own_mid:
                sb.add('static/%d/%d' % (cid, ti), 'static', om)
        if args.stats:
            P_, N_, S_, F_ = g.arrays()
            cnt = {}
            for f, s in zip(F_, S_):
                cnt[sk.FINS[f].name] = cnt.get(sk.FINS[f].name, 0) + int(s)
            print('   corners by finish:', sorted(cnt.items(), key=lambda q: -q[1])[:16])
        if args.preview and ti == args.preview_tier:
            views = [v for v in VIEWS if not args.views or v[0] in args.views.split(',')]
            extra = [(m, sk.Bake(m, None, floors=(), seed=5)) for m in marquee if m.faces]
            for fg in (wheel_frame(L, sk.Tier(1), P), spikes(L, sk.Tier(1), P), neck(L, sk.Tier(1), P)):
                extra.append((fg, sk.Bake(fg, None, floors=(), seed=5)))
            if not args.views or 'rig' in args.views:
                row = Geo()
                for k, (name, pg, rest) in enumerate(props.items(L, T, marquee)):
                    if name.startswith('marquee') or name.startswith('wheel'):
                        continue
                    row.extend(pg.transformed([1, 0, 0, -9.0 + (k % 6) * 3.6, 0, 1, 0, 300.0 - (k // 6) * 3.4, 0, 0, 1, 0]))
                extra.append((row, sk.Bake(row, None, floors=(), seed=7)))
                wg = Geo()
                for name, pg, rest in props.items(L, T, marquee):
                    if name.startswith('wheel'):
                        wg.extend(pg.transformed([1, 0, 0, P['wheelX'], 0, 1, 0, P['wheelY'], 0, 0, 1, P['wheelZ']]))
                extra.append((wg, sk.Bake(wg, None, floors=(), seed=7)))
                views = views + [('rig', (-1, 297.5, 12), (0, 295.4, 0), 62), ('rig-b', (6, 299, 6), (2, 296.5, 0), 55), ('rig-c', (-7, 293.6, 5), (-5, 293.0, 0), 50)]
            sk.preview([(g, bk)] + extra, views, args.preview)
        if not args.static_only:
            props.bank_props(sb, L, T, args.no_ao, marquee)
        del g
    if not args.static_only:
        for name, fg in (('wheelframe', wheel_frame(L, sk.Tier(1), P)), ('spikes', spikes(L, sk.Tier(1), P)), ('neck', neck(L, sk.Tier(1), P))):
            ao = None if args.no_ao else 1.0 - (1.0 - sk.bake_ao([fg], distance=1.5, samples=12)[0]) * .6
            sb.add('static/%s/1' % name, 'static', sk.to_mesh('static/%s/1' % name, fg, sk.Bake(fg, ao, floors=(), seed=5)))
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
