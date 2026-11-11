"""Brickfall's stage art (docs/arenas/BRICKFALL-ART.md, docs/arenas/STAGE-ART.md): the demolition yard modelled on the map's own
blocks, into the stage bank ChipArenaStageBrickfall. Brickfall has no Core kit: its art is its blocks, so every block is
modelled here: the static ones (the frontage, the hotel's core, the tenements' shells, the shops, the market, the yard) as
world-space chunks; everything the simulation moves or breaks (brickfall_props.py) as block models.

    blender --background --factory-startup --python tools/stages/brickfall_art.py -- --layout LAYOUT.json --out <Assets>/LocalAssets
    (--preview DIR [--views a,b] renders the baked Fine tier; --tiers 0,1,2; --no-ao; --static-only; --stats)

The map is point symmetric (a half turn about the yard's middle): the north and west half is sculpted (twice, the second time with
other names, tones and seeds) and the second sculpt is turned half round. The hotel in the middle is sculpted whole.

Frames: a wall's frame is brickfall_kit.Frame (u along it, v up, n out of it). The north frontage's u runs west from x = +30,
the west frontage's south from z = +26.
"""
import argparse
import math
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo  # noqa: E402
import brickfall_kit as bk  # noqa: E402
from brickfall_kit import F, Frame, Hole, fbox  # noqa: E402

BANK = 'ChipArenaStageBrickfall'
TURN = [-1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, -1.0, 0]
STATS = bk.STATS
tally = bk.tally


def bb(b):
    p, s = b['pos'], b['size']
    return p[0] - s[0] / 2, p[0] + s[0] / 2, p[1] - s[1] / 2, p[1] + s[1] / 2, p[2] - s[2] / 2, p[2] + s[2] / 2


def secondary(i):
    """A block of the south and east half (its twin a half turn away is sculpted)."""
    return '~m' in i or i.startswith(('bray-', 'frontage--1-', 'bridge-bray', 'stack-s', 'shop-ramp-bray', 'yard-dock-ramp-bray')) or i == 'frontage-side-1'


class Stage:
    """The layout: blocks by id, each with its look; the half-turn twins; the segments."""

    def __init__(self, L):
        self.L = L
        self.blocks = {b['id']: b for b in L['blocks']}
        for k in L['looks']:
            self.blocks[k['id']].update(look=k['look'], color=k['color'], band=k['band'], segment=k.get('segment'))
        self.twin = {}
        key = {}
        for b in L['blocks']:
            key[(round(b['pos'][0], 3), round(b['pos'][1], 3), round(b['pos'][2], 3), round(b['size'][0], 3), round(b['size'][2], 3))] = b['id']
        for b in L['blocks']:
            t = key.get((round(-b['pos'][0], 3), round(b['pos'][1], 3), round(-b['pos'][2], 3), round(b['size'][0], 3), round(b['size'][2], 3)))
            if t is None and not b['id'].startswith('hotel-'):
                raise ValueError('no twin for ' + b['id'])
            self.twin[b['id']] = t
        self.hotel = {i: b for i, b in self.blocks.items() if i.startswith('hotel-')}
        self.segments = L['segments']
        # the panel that carries the hotel's blade sign: the north-east wing's east wall, first floor
        self.sign = next(i for i, b in self.blocks.items() if i.startswith('hotel-ne-hotel-e') and b['role'] == 'masonry' and abs(b['pos'][1] - 5.28) < .1 and b['size'][2] > 5)

    def half(self, v):
        """The north-west half's blocks by their own ids: itself (v 0), or the other half turned onto it (v 1)."""
        out = {}
        for i, b in self.blocks.items():
            if i.startswith('hotel-') or i == 'frontage-floor' or secondary(i):
                continue
            t = self.twin[i]
            if t == i or not secondary(t):
                raise ValueError('twin of %s is %s' % (i, t))
            if v == 0:
                out[i] = b
            else:
                tb = dict(self.blocks[t])
                tb['pos'] = [-tb['pos'][0], tb['pos'][1], -tb['pos'][2]]
                tb['id'] = i
                tb['twin'] = t
                out[i] = tb
        return out


# ---------------------------------------------------------------------------------------------------------------------------
# The yard's floor
# ---------------------------------------------------------------------------------------------------------------------------
def floor(g, T, S):
    """The slab's top (y 0): asphalt, the cobbled lanes along the frontages, paving round the buildings, patches; one cell is
    one face, so nothing lies on anything."""
    cell = (1.0, 2.0, 4.0)[T.i]
    solids = []
    for i, b in S.blocks.items():
        x0, x1, y0, y1, z0, z1 = bb(b)
        if i != 'frontage-floor' and y0 < .3 and (x1 - x0) * (z1 - z0) > 9 and (i.startswith(('hotel', 'ashby', 'bray', 'shop-k', 'market-st', 'market-k')) or 'floor' in i):
            solids.append((x0, x1, z0, z1))
    solids += [(-8.3, 8.3, -6.3, 6.3), (-25.2, -5.8, 11.8, 21.2), (5.8, 25.2, -21.2, -11.8)]

    def near(x, z, m):
        return any(x0 - m < x < x1 + m and z0 - m < z < z1 + m for (x0, x1, z0, z1) in solids)
    r = bk.rng(7)
    patch = [(r.uniform(-27, 27), r.uniform(-22, 22), r.uniform(1.2, 2.6)) for _ in range(9)]
    nx, nz = int(64 / cell), int(56 / cell)
    for i in range(nx):
        for j in range(nz):
            x0, z0 = -32 + i * cell, -28 + j * cell
            xm, zm = x0 + cell / 2, z0 + cell / 2
            if abs(zm) > 22.0 or abs(xm) > 28.5:
                f = F['cobble']
            elif near(xm, zm, 1.6):
                f = F['paving']
            elif any((xm - px) ** 2 + (zm - pz) ** 2 < pr * pr for (px, pz, pr) in patch):
                f = F['asphalt-patch']
            else:
                f = F['asphalt']
            g.face(((x0, 0.0, z0), (x0 + cell, 0.0, z0), (x0 + cell, 0.0, z0 + cell), (x0, 0.0, z0 + cell)), f, (0, 1, 0))
    if T.detail:
        # faded lane paint, manholes and gratings: small faces a hair over the road (the stack keeps them apart)
        for (x, z, a) in ((-13, 0, 0), (13, 0, 0), (-13, -4, 0), (13, 4, 0), (-18, 9, 0), (18, -9, 0)):
            fr = Frame((x - 1.1, .006, z - .07), (1, 0, 0), (0, 0, 1))
            g.face((fr.P(0, 0), fr.P(2.2, 0), fr.P(2.2, .14), fr.P(0, .14)), F['lane'], (0, 1, 0))
        for (x, z) in ((-12, 5.5), (12, -5.5), (-20, -8), (20, 8), (3, 11), (-3, -11), (-27, 6), (27, -6)):
            sk.lathe(g, [(0.0, 0.0), (.36, 0.0), (.4, -.004)], (x, .012, z), F['iron'], T.round, cap1=False)
            sk.lathe(g, [(.4, 0.0), (.46, 0.0)], (x, .008, z), F['concrete-dark'], T.round)
        for (x, z) in ((-9.5, 10.5), (9.5, -10.5), (-16, -11), (16, 11), (0, 22.5), (0, -22.5)):
            fbox(g, T, Frame((x - .3, 0, z - .2), (1, 0, 0)), 0, .6, .0, .012, 0, .4, F['iron'], .004, skip=('-y',))


# ---------------------------------------------------------------------------------------------------------------------------
# The frontage: the buildings that wall the yard
# ---------------------------------------------------------------------------------------------------------------------------
def building(g, T, fr, B, r):
    """A building of the frontage on the wall's frame, u0..u1: its brick face with the openings cut, the shopfronts, the
    windows of its upper floors, strings, cornice, parapet and coping, its downpipe. B: the building's record."""
    u0, u1 = B['u0'], B['u1']
    fw = F[B['fin']]
    top = B.get('top', 14.0)
    gh, fh, floors = B['gh'], B['fh'], B['floors']
    trim = F[B.get('trim', 'stone')]
    style = {'trim': trim, 'ring': F[B.get('ring', B['fin'])], 'frame': F[B.get('frame', 'white-paint')], 'blind': F[B.get('blind', 'brick-dark')]}
    holes, shops = [], []
    # the ground floor: the entrance recess and the shopfronts between
    alc = B.get('alc')
    spans = [(u0 + .55, u1 - .55)] if alc is None else [(u0 + .55, alc[0] - .5), (alc[1] + .5, u1 - .55)]
    sh = B.get('shop', 2.75)
    fills = B.get('shopfill', ('shutter', 'board', 'glass', 'brick'))
    for (a, b) in spans:
        ln = b - a
        if ln < 1.3:
            continue
        k = max(1, int(round(ln / B.get('shopbay', 3.1))))
        w = (ln - (k - 1) * .5) / k
        for i in range(k):
            x = a + i * (w + .5)
            fill = fills[r.randrange(len(fills))]
            h = Hole(x, x + w, .55 if fill in ('glass', 'board', 'brick') else 0.0, sh, 0.0, fill=fill, depth=.16, sill=fill in ('glass', 'board', 'brick'), head='steel', bars=(max(2, int(w / .7)), 2))
            holes.append(h)
            shops.append(h)
    if alc is not None:
        holes.append(Hole(alc[0], alc[1], 0.0, 2.5, 0.0, fill='none', depth=0.0, sill=False, head='lintel'))
    # the upper floors
    wn = B['win']
    n = max(1, int((u1 - u0 - .6) / B['bay']))
    lit = (F['lit'], F['lit-dim'], F['lit-cool'])
    for k in range(floors):
        y = gh + k * fh + B.get('sillh', .85)
        for i in range(n):
            x = u0 + (u1 - u0) * (i + .5) / n
            q = r.random()
            fill = 'lit' if q < .2 else 'board' if q < .32 else 'brick' if q < .4 else 'glass'
            if wn[3] == 'pair' and T.detail:
                for s in (-1, 1):
                    holes.append(Hole(x + s * (wn[0] / 2 + .12) - wn[0] / 2, x + s * (wn[0] / 2 + .12) + wn[0] / 2, y, y + wn[1], wn[2], fill=fill, depth=.13, head='arch' if wn[2] > 0 else 'lintel', lit=lit[r.randrange(3)], bars=(1, 2)))
            else:
                w = wn[0] * (2.1 if wn[3] == 'pair' else 1)
                holes.append(Hole(x - w / 2, x + w / 2, y, y + wn[1], wn[2], fill=fill, depth=.13, head='arch' if wn[2] > 0 else 'lintel', lit=lit[r.randrange(3)], bars=(2, 3) if wn[1] > 1.5 else (2, 2)))
    bk.wallface(g, T, fr, u1, top, fw, holes, u0=u0)
    for h in holes:
        if h.fill != 'none':
            bk.opening(g, T, fr, h, fw, style=style)
    # plinth, fascia with its name, strings, cornice, coping
    cuts = sorted([(h.u0, h.u1) for h in holes if h.v0 < .5])
    x = u0
    for (a, b) in cuts + [(u1, u1)]:
        if a - x > .05:
            bk.mould(g, T, fr, x, a, 0.0, [(0.0, 0.0), (.06, 0.0), (.06, .46), (.02, .52), (0.0, .52)], trim)
        x = b
    fy = sh + .25
    bk.mould(g, T, fr, u0 + .25, u1 - .25, fy, [(0.0, 0.0), (.09, 0.0), (.09, .5), (.14, .56), (.14, .62), (0.0, .62)], F[B.get('fascia', 'green-paint')])
    if T.detail and B.get('name'):
        mid = (spans[0][0] + spans[0][1]) / 2 if alc is None or (spans[0][1] - spans[0][0]) > (spans[1][1] - spans[1][0]) else (spans[1][0] + spans[1][1]) / 2
        bk.text(g, fr, mid, fy + .13, .045, B['name'], F[B.get('letters', 'stone')], .096)
    bk.mould(g, T, fr, u0, u1, gh - .1, bk.STRING, trim)
    if T.detail:
        for k in range(1, floors):
            bk.mould(g, T, fr, u0, u1, gh + k * fh - .1, [(0.0, 0.0), (.03, .02), (.03, .1), (0.0, .12)], fw)
    cy = B.get('cornice', top - .95)
    bk.cornice(g, T, fr, u0 + .02, u1 - .02, cy, trim, B.get('cscale', 1.0))
    bk.mould(g, T, fr, u0, u1, top - .16, bk.COPING, trim)
    # quoins at its ends and a downpipe
    if T.i == 0:
        for (a, b) in ((u0, u0 + .42), (u1 - .42, u1)):
            y = .52
            j = 0
            while y < cy - .3:
                fbox(g, T, fr, a + (0 if (j % 2 == 0) == (a == u0) else .1 if a == u0 else 0), b - (0 if (j % 2 == 0) == (a == u0) else 0 if a == u0 else .1), y, y + .42, 0, .04, trim, .012, skip=('-z',))
                y += .48
                j += 1
    bk.pipe(g, T, fr, B.get('pipe_u', u1 - .7), 0.2, cy, f=F[B.get('pipe', 'iron')])
    # over the yard's heads (nothing below 2.3 m): striped canvas awnings on some of the shopfronts, a hanging sign on a bracket
    cv = F[B.get('awning', ('canvas-red', 'canvas-green', 'canvas-cream')[r.randrange(3)])]
    for h in shops:
        if r.random() < .55:
            awning(g, T, fr, h.u0 - .1, h.u1 + .1, sh + .2, cv)
    hanging_sign(g, T, fr, u0 + (u1 - u0) * (.18 if alc is None or alc[0] - u0 > 4 else .8), gh + .75, F[B.get('fascia', 'green-paint')], F[B.get('letters', 'stone')])


def awning(g, T, fr, u0, u1, y, cv):
    """A canvas awning let down over a shopfront: stripes sloping out from the wall on iron arms, a hanging valance."""
    out, drop, val = .95, .62, .2
    n = max(1, int((u1 - u0) / (.32, .64, 99)[T.i]))
    nn = fr.D(0, out, drop)
    for i in range(n):
        a, b = u0 + (u1 - u0) * i / n, u0 + (u1 - u0) * (i + 1) / n
        f = cv if i % 2 == 0 else F['canvas-cream'] if cv != F['canvas-cream'] else F['canvas-green']
        g.face((fr.P(a, y, .06), fr.P(b, y, .06), fr.P(b, y - drop, out), fr.P(a, y - drop, out)), f, nn)
        g.face((fr.P(a, y - drop, out), fr.P(b, y - drop, out), fr.P(b, y - drop - val, out), fr.P(a, y - drop - val, out)), f, fr.n)
        if T.i == 0:
            g.face((fr.P(a, y, .06), fr.P(b, y, .06), fr.P(b, y - drop, out), fr.P(a, y - drop, out)), F['canvas-cream'], (-nn[0], -nn[1], -nn[2]))
    for (u, s) in ((u0, -1), (u1, 1)):
        g.face((fr.P(u, y, .06), fr.P(u, y - drop, out), fr.P(u, y - drop - val, out), fr.P(u, y - val * .4, .06)), cv, fr.D(s, 0, 0))
        if T.detail:
            sk.cylinder(g, fr.P(u, y - drop - val * .5, out - .02), fr.P(u, y - drop - .25, .03), .014, F['iron'], 4, caps=False)


def hanging_sign(g, T, fr, u, y, board, mark):
    """A trade sign hung from a wrought bracket, square to the wall."""
    if not T.detail:
        return
    side = Frame(fr.P(u, y, .04), fr.n)             # runs out from the wall; its faces look along the wall
    fbox(g, T, side, 0, .95, .86, .9, -.015, .015, F['iron'], 0)
    arm = sk.catmull([side.P(.05, .5, 0), side.P(.3, .62, 0), side.P(.62, .86, 0)], per=(3, 2, 2)[T.i])
    sk.tube(g, arm, .012, F['iron'], 4, caps=False)
    for a in (.3, .85):
        sk.cylinder(g, side.P(a, .86, 0), side.P(a, .74, 0), .008, F['iron'], 4, caps=False)
    fbox(g, T, side, .2, .95, .12, .74, -.02, .02, board, .01)
    for s in (-1, 1):
        sk.lathe(g, [(0.0, 0.0), (.2, 0.0), (.22, -.006)], side.P(.575, .43, s * .022), mark, T.round, axis=(side.n[0] * s, 0, side.n[2] * s), cap1=False)


def alcove(g, T, fr, a, b, f, door, trim):
    """An entrance recess in the frontage (the map's own: 2 m wide, 1.5 deep, 2.5 high): its cheeks, soffit and back wall
    with the door."""
    d, h = 1.5, 2.5
    left = Frame(fr.P(a, 0, 0), (-fr.n[0], -fr.n[1], -fr.n[2]))        # faces +u
    right = Frame(fr.P(b, 0, -d), fr.n)                                # faces -u
    bk.wallface(g, T, left, d, h, f)
    bk.wallface(g, T, right, d, h, f)
    cs = T.cell
    g.face((fr.P(a, h, 0), fr.P(b, h, 0), fr.P(b, h, -d), fr.P(a, h, -d)), F['concrete-dark'], (0, -1, 0))
    back = fr.shifted(0, 0, -d)
    w = b - a
    hl = Hole(a + .45, b - .45, 0.0, 2.05, .0, fill='door', depth=.1, sill=False, head='lintel')
    bk.wallface(g, T, back, b, h, f, [hl], u0=a)
    bk.opening(g, T, back, hl, f, style={'trim': trim, 'door': door})
    if T.detail:
        fbox(g, T, back, a + .3, b - .3, 0, .012, 0, .5, trim, .005, skip=('-y',))      # a worn step


NORTH = [
    dict(u0=0.0, u1=18.5, fin='brick', gh=3.8, fh=3.2, floors=3, bay=2.3, win=(1.05, 1.7, .2, 'arch'), alc=(10.0, 12.0), name='COAL & COKE', fascia='green-paint', trim='stone', roof='slate', door='timber-dark'),
    dict(u0=21.5, u1=37.0, fin='brick-plum', gh=3.8, fh=4.75, floors=2, bay=2.55, win=(1.2, 2.7, .6, 'arch'), alc=(27.0, 29.0), pipe_u=22.2, name='ASHBY DAIRY', fascia='white-paint', letters='green-paint', trim='stone', ring='brick', cornice=12.95, door='green-paint'),
    dict(u0=37.0, u1=50.0, fin='brick-tan', gh=3.6, fh=3.25, floors=3, bay=2.15, win=(.6, 1.75, .0, 'pair'), alc=(44.0, 46.0), pipe_u=49.3, name='PAWNBROKER', fascia='black', letters='yellow', trim='stone-dark', cornice=13.0, door='timber-dark'),
    dict(u0=50.0, u1=60.0, fin='brick-rose', gh=3.8, fh=3.2, floors=3, bay=2.4, win=(1.1, 1.75, .28, 'arch'), alc=(55.0, 57.0), name='TOBACCO', fascia='cab-red', letters='stone', trim='stone', cscale=1.15, door='timber-dark'),
]
NORTH_B = [
    dict(u0=0.0, u1=18.5, fin='brick-rose', gh=3.7, fh=3.25, floors=3, bay=2.6, win=(.62, 1.8, .0, 'pair'), alc=(10.0, 12.0), name='IRONMONGER', fascia='black', letters='stone', trim='stone-dark', door='green-paint'),
    dict(u0=21.5, u1=37.0, fin='brick', gh=3.8, fh=3.2, floors=3, bay=2.2, win=(1.05, 1.75, .26, 'arch'), alc=(27.0, 29.0), pipe_u=22.2, name='BRAY & SONS', fascia='green-paint', letters='stone', trim='stone', door='timber-dark'),
    dict(u0=37.0, u1=50.0, fin='brick-dark', gh=3.9, fh=4.7, floors=2, bay=2.6, win=(1.25, 2.6, .62, 'arch'), alc=(44.0, 46.0), pipe_u=49.3, name='THE ANVIL', fascia='cab-red', letters='stone', trim='stone', ring='brick-rose', cornice=12.9, door='timber-dark'),
    dict(u0=50.0, u1=60.0, fin='brick-tan', gh=3.7, fh=3.3, floors=3, bay=2.4, win=(1.1, 1.8, .0, 'arch'), alc=(55.0, 57.0), name='PRINTERS', fascia='white-paint', letters='black', trim='stone', door='green-paint'),
]


def frontage_north(g, T, v):
    """The north frontage (z 26, facing the yard): four buildings, the chimney's notch between the first two."""
    fr = Frame((30.0, 0.0, 26.0), (-1.0, 0.0, 0.0))
    r = bk.rng(11 + v * 17)
    for B in (NORTH, NORTH_B)[v]:
        building(g, T, fr, B, r)
        alcove(g, T, fr, B['alc'][0], B['alc'][1], F[B['fin']], F[B['door']], F[B.get('trim', 'stone')])
        roof(g, T, fr, B, r)
    # the notch the chimney stands in: the two buildings' flank walls
    for (u, n) in ((18.5, 1), (21.5, -1)):
        fl = Frame(fr.P(u, 0, 0.0 if n > 0 else -2.0), (-fr.n[0] * n, 0, -fr.n[2] * n))
        bk.wallface(g, T, fl, 2.0, 14.0, F['brick-dark'])
    # the lamps the look lights the lane with
    for x in (-18.0, 0.0):
        wall_lamp(g, T, fr, 30.0 - x, 4.6)


def wall_lamp(g, T, fr, u, y):
    """A swan-neck street lamp on a wall bracket: its bulb where the look's sodium light is (0.4 m off the wall)."""
    arm = sk.catmull([fr.P(u, y - .5, 0.0), fr.P(u, y + .25, .12), fr.P(u, y + .42, .3), fr.P(u, y + .2, .4)], per=(4, 3, 2)[T.i])
    sk.tube(g, arm, .022, F['iron'], T.small)
    fbox(g, T, fr, u - .07, u + .07, y - .62, y - .38, 0, .03, F['iron'], .008)
    sk.lathe(g, [(.05, .14), (.2, .0), (.21, -.02)], fr.P(u, y + .06, .4), F['green-paint'], T.round)
    sk.lathe(g, [(0.0, -.1), (.06, -.07), (.075, -.01), (.05, .05)], fr.P(u, y + .02, .4), F['sodium'], T.small + 2)


def roof(g, T, fr, B, r):
    """Over a frontage building, behind its parapet: a slate roof rising away from the yard, the party walls' chimney stacks
    and their pots (above the play space's walls: only seen from the hotel's top and from the air)."""
    u0, u1 = B['u0'], B['u1']
    top = B.get('top', 14.0)
    fs = F['slate']
    y0, y1, c0, c1 = top - .25, top + 1.45, -.42, -3.9
    cell = (2.0, 4.0, 8.0)[T.i]
    n = max(1, int((u1 - u0) / cell))
    nn = fr.D(0, (c0 - c1), (y1 - y0))
    for i in range(n):
        a, b = u0 + (u1 - u0) * i / n, u0 + (u1 - u0) * (i + 1) / n
        g.face((fr.P(a, y0, c0), fr.P(b, y0, c0), fr.P(b, y1, c1), fr.P(a, y1, c1)), fs, nn)
    # the parapet's back and its gutter
    g.face((fr.P(u0, top - .16, -.3), fr.P(u1, top - .16, -.3), fr.P(u1, y0, -.42), fr.P(u0, y0, -.42)), F['tar'], (0, 1, 0))
    g.face((fr.P(u0, top, 0), fr.P(u1, top, 0), fr.P(u1, top - .16, -.3), fr.P(u0, top - .16, -.3)), F[B.get('trim', 'stone')], (0, 1, 0))
    # party walls with chimney stacks
    fb = F[B['fin']]
    for (u, s) in ((u0, 1), (u1, -1)):
        fbox(g, T, fr, u - .0 if s > 0 else u - .34, u + .34 if s > 0 else u, top - .3, y1 + .35, -3.9, -.3, fb, .02, cell=0, skip=('-y',))
    k = max(1, int((u1 - u0) / 7))
    for i in range(k):
        x = u0 + (u1 - u0) * (i + .5) / k + r.uniform(-.8, .8)
        h = r.uniform(1.5, 2.3)
        fbox(g, T, fr, x - .55, x + .55, y0 + .3, y1 + h, -3.2, -2.5, fb, .02, cell=0, skip=('-y',))
        fbox(g, T, fr, x - .62, x + .62, y1 + h, y1 + h + .12, -3.27, -2.43, F['stone-dark'], .02, cell=0)
        if T.detail:
            for dx in (-.3, 0.0, .3):
                sk.lathe(g, [(.1, 0.0), (.085, .32), (.105, .36), (.105, .42)], fr.P(x + dx, y1 + h + .12, -2.85), F['stone'], T.small + 1)


WEST = dict(name='MERCER BRICK & TILE Co.', fin='brick', pier='brick-dark', trim='stone')
WEST_B = dict(name='BRAY WHARF WAREHOUSE', fin='brick-plum', pier='brick', trim='stone-dark')


def frontage_west(g, T, v):
    """The west frontage (x -30, facing the yard): a brick warehouse of ten arched bays between piers, its name painted along
    the frieze, a corbel table, a parapet."""
    fr = Frame((-30.0, 0.0, 26.0), (0.0, 0.0, -1.0))
    W = (WEST, WEST_B)[v]
    r = bk.rng(23 + v * 5)
    fw, fp, trim = F[W['fin']], F[W['pier']], F[W['trim']]
    width, top = 52.0, 14.0
    n = 9
    bay, b0 = 5.2, 3.4          # the bays' pitch and the first one's start: the look's tarp (u 30 to 34) hangs inside the sixth
    pw = .95
    holes = []
    ins = []
    lit = (F['lit'], F['lit-dim'], F['lit-cool'])
    for i in range(n):
        a, b = b0 + i * bay + pw / 2 + .25, b0 + (i + 1) * bay - pw / 2 - .25
        # the bay's arched recess
        ins.append((a, b))
        holes.append(Hole(a, b, .9, 9.2, (b - a) / 2, fill='none', depth=.22, sill=False, head='arch'))
    bk.wallface(g, T, fr, width, top, fw, holes)
    style = {'trim': trim, 'ring': fp, 'frame': F['iron'], 'blind': F['brick-dark']}
    for (i, h) in enumerate(holes):
        bk.opening(g, T, fr, h, fw, style=style)
        # the recess's back wall with its own openings: a loading door or a big window low, a lunette window in the arch
        back = fr.shifted(0, 0, -.22)
        a, b = h.u0, h.u1
        m = (a + b) / 2
        inner = []
        q = r.random()
        if q < .3:
            inner.append(Hole(m - 1.25, m + 1.25, .9, 3.6, 0.0, fill='shutter', depth=.1, sill=False, head='steel'))
        else:
            fill = 'lit' if r.random() < .25 else 'board' if r.random() < .2 else 'glass'
            inner.append(Hole(m - 1.3, m + 1.3, 1.9, 4.1, 0.0, fill=fill, depth=.1, head='lintel', lit=lit[r.randrange(3)], bars=(4, 3)))
        fill = 'lit' if r.random() < .22 else 'brick' if r.random() < .15 else 'glass'
        inner.append(Hole(m - 1.3, m + 1.3, 5.3, 8.4, 1.3, fill=fill, depth=.1, head='arch', lit=lit[r.randrange(3)], bars=(4, 4)))
        # the back wall follows the recess's arch: its face is cut by the same curve
        arched_back(g, T, back, h, fw, inner)
        for hh in inner:
            bk.opening(g, T, back, hh, fw, style=style)
    # piers with stone bases and caps, plinth, frieze with the name, corbel table, cornice, parapet
    for i in range(n + 1):
        c = b0 + i * bay
        fbox(g, T, fr, c - pw / 2, c + pw / 2, .0, 11.0, 0, .12, fp, .02, cell=(2.0, 0, 0)[T.i], skip=('-z', '-y'))
        fbox(g, T, fr, c - pw / 2 - .05, c + pw / 2 + .05, 0, .9, 0, .2, trim, .025, skip=('-z', '-y'))
        fbox(g, T, fr, c - pw / 2 - .06, c + pw / 2 + .06, 11.0, 11.3, 0, .2, trim, .025, skip=('-z',))
    for i in range(n):
        a, b = b0 + i * bay + pw / 2, b0 + (i + 1) * bay - pw / 2
        bk.mould(g, T, fr, a, b, 0.0, bk.PLINTH, trim, ends=False)
    bk.mould(g, T, fr, 0, width, 11.3, bk.STRING, trim)
    if T.detail:
        bk.text(g, fr, width / 2, 11.75, .1, W['name'], F['white-paint'], .012)
    bk.cornice(g, T, fr, 0, width, 12.75, trim, 1.2)
    bk.mould(g, T, fr, 0, width, top - .16, bk.COPING, trim)
    for u in (1.2, 24.75, 51.2):
        bk.pipe(g, T, fr, u, .2, 12.7, c=.2 if 3 < u < 50 else .09)
    for (a, b) in ((0.0, b0 - pw / 2), (b0 + n * bay + pw / 2, width)):
        bk.mould(g, T, fr, a, b, 0.0, bk.PLINTH, trim, ends=False)
    # its roof: a shallow slate pitch and a row of ridge vents
    B = dict(u0=0.0, u1=width, fin=W['fin'], trim=W['trim'])
    roof(g, T, fr, B, r)


def arched_back(g, T, fr, h, f, inner):
    """The back wall of an arched recess: the face inside the recess's outline, with its own openings left out."""
    cu = h.cuts(T)
    us = sorted(set(cu + [x for hl in inner for x in (hl.u0, hl.u1)] + sum([hl.cuts(T) for hl in inner], [])))
    us = [u for u in us if h.u0 - 1e-6 <= u <= h.u1 + 1e-6]
    n = fr.n
    for i in range(len(us) - 1):
        a, b = us[i], us[i + 1]
        if b - a < 1e-5:
            continue
        m = (a + b) / 2
        # the column from the recess's sill to its arch, minus the inner openings (their heads may be arched too)
        cuts = sorted([hl for hl in inner if hl.u0 < m < hl.u1], key=lambda hl: hl.v0)
        lo_a = lo_b = h.v0
        for hl in cuts:
            if hl.v0 > lo_a + 1e-6:
                g.face((fr.P(a, lo_a), fr.P(b, lo_b), fr.P(b, hl.v0), fr.P(a, hl.v0)), f, n)
            lo_a, lo_b = hl.top(a), hl.top(b)
        g.face((fr.P(a, lo_a), fr.P(b, lo_b), fr.P(b, h.top(b)), fr.P(a, h.top(a))), f, n)


# ---------------------------------------------------------------------------------------------------------------------------
# Whatever has no sculptor: its block as a finished piece
# ---------------------------------------------------------------------------------------------------------------------------
def plain(g, T, b, f=None, bevel=.03):
    x0, x1, y0, y1, z0, z1 = bb(b)
    sk.box(g, x0, x1, y0, y1, z0, z1, bk.look_fin(b.get('look')) if f is None else f, T, bevel, skip=('-y',) if y0 < .01 else ())


def static_half(S, T, v):
    """The north-west half's static scenery (v 1: the south-east half's, sculpted in the north-west's place)."""
    import brickfall_parts as parts
    g = Geo()
    H = S.half(v)
    done = set()
    m = g.faces
    frontage_north(g, T, v)
    frontage_west(g, T, v)
    done.update(i for i in H if i.startswith('frontage'))
    tally(g, 'frontage', m)
    parts.sculpt(g, T, S, H, v, done)
    m = g.faces
    for i, b in H.items():
        if b['role'] == 'static' and i not in done:
            plain(g, T, b)
    tally(g, 'plain', m)
    return g


def static_scene(S, T):
    import brickfall_parts as parts
    g = Geo()
    m = g.faces
    floor(g, T, S)
    tally(g, 'floor', m)
    g.extend(static_half(S, T, 0))
    g.extend(static_half(S, T, 1).transformed(TURN))
    parts.hotel_core(g, T, S)
    return g


def views():
    return [
        ('over-sw', (-27, 13, -23), (0, 5, 0), 60), ('over-ne', (27, 14, 23), (0, 5, 0), 60),
        ('north', (0, 7, 2), (0, 6, 26), 75), ('north-close', (-12, 1.7, 22), (-19, 3, 26), 60), ('alcove', (-14, 1.6, 23.5), (-15.2, 1.4, 27), 55),
        ('west', (-6, 6, 0), (-30, 6, 0), 75), ('west-close', (-24, 1.7, 8), (-30, 4, 14), 60),
        ('hotel', (-18, 1.7, 4), (-4, 6, 1), 60), ('hotel-in', (-7, 1.7, 1.5), (-3, 3, 5), 65), ('roof', (-6, 13.9, -4), (3, 12.5, 4), 60), ('sign', (-9, 13, 14), (0, 15, 5.6), 50),
        ('ashby', (-27.5, 1.7, 24), (-10, 4, 21), 60), ('ashby-south', (-15, 1.7, 4), (-15, 4, 13), 60), ('bridge', (-13, 2, 9), (-7, 7.5, 9), 60),
        ('shop', (11, 1.7, 11), (2, 2.5, 17), 60), ('stack', (14, 1.7, 21.5), (10, 6, 26.8), 70), ('stack-top', (4, 12.5, 20), (10, 14.5, 26.8), 50), ('market', (-14, 1.7, -6), (-24, 1.5, -2), 60),
        ('yard', (-11, 1.7, -14), (-22, 1.5, -21), 60), ('props', (-22, 1.6, 8), (-26.5, .8, 9.5), 50), ('kiosk', (3, 1.7, 21), (6.5, 1.4, 24), 55),
        ('tram', (4, 1.7, 13), (0, 1.3, 9.2), 55), ('monument', (9.2, 1.7, 4.2), (11.5, 1.2, 8), 50), ('phone', (-26.5, 1.7, 22), (-29, 1.3, 24.5), 50), ('cover', (-24, 1.6, 12), (-26.5, .6, 9.5), 50), ('pallet', (-18, 1.5, -14.5), (-15.5, .6, -12.8), 45), ('sandbag', (10.5, 1.5, 12.5), (13, .6, 14.5), 45), ('barricade', (-13.5, 1.6, 2.5), (-10.5, .6, 0), 45),
    ]


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--layout', required=True)
    ap.add_argument('--out', help='Assets/LocalAssets to write the bank into')
    ap.add_argument('--preview', help='render views of the baked Fine tier')
    ap.add_argument('--views', help='only these views (comma separated)')
    ap.add_argument('--tiers', default='0,1,2')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--static-only', action='store_true')
    ap.add_argument('--stats', action='store_true', help='print where the faces of each tier go')
    ap.add_argument('--landed', action='store_true', help='the preview shows every segment landed (the heaps) instead of standing')
    args = ap.parse_args(argv)
    L = sk.load_layout(args.layout)
    bk.finishes()
    S = Stage(L)
    import brickfall_props as props
    sb = sk.StageBank()
    chunks = sk.Chunks(16.0, (-32.0, -32.0), (-1.0, 14.05))
    floors = (0.0,)
    t0 = time.time()
    for ti in [int(x) for x in args.tiers.split(',')]:
        T = sk.Tier(ti)
        t = time.time()
        STATS.clear()
        g = static_scene(S, T)
        print('  coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % sk.lift_coplanar(g, eps=sk.NEAR_TIER[ti], lift=sk.LIFT_TIER[ti]))
        scene = props.rest_scene(S, T, args.landed)
        print('tier %s: static %d faces, props at rest %d faces, %.1f s' % (T.name, g.faces, scene.faces, time.time() - t))
        if args.stats:
            print('  ' + ', '.join('%s %d' % kv for kv in sorted(STATS.items(), key=lambda kv: -kv[1]) if kv[1] > 0))
        ao = None
        if not args.no_ao:
            ao = sk.bake_ao([g], distance=(1.6, 1.6, 2.0)[ti], samples=(32, 16, 12)[ti], occluders=[scene])[0]
            ao = np.clip(.25 + .75 * ao, 0, 1) ** .8   # open air: the baked shade eased
        bake = sk.Bake(g, ao, soot=None, floors=floors, seed=3)
        ids = chunks.ids(g)
        for cid in np.unique(ids):
            sb.add('static/%d/%d' % (cid, ti), 'static', sk.to_mesh('static/%d/%d' % (cid, ti), g, bake, ids == cid))
        if args.preview and ti == 0:
            vs = views()
            if args.views:
                vs = [v for v in vs if v[0] in args.views.split(',')]
            sk.preview([(g, bake), (scene, sk.Bake(scene, None))], vs, args.preview)
        if not args.static_only:
            props.bank_props(sb, S, T, floors, args.no_ao)
        del g
    if args.out:
        for b in L['blocks']:
            sb.cover(b['id'])
        for s in L['segments']:
            for rb in s['rubble']:
                sb.cover(rb['id'])
        sb.write(args.out, BANK)
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
