"""Cyber City's art: the buildings and street furniture on their blocks (cybercity_art.py runs it): the frontage's shopfronts
and storeys, the Nova and Vega podiums and their spires, the Helix station over the plaza, the two markets, the avenue's
vehicles, kiosks and lamps, the yards and the promenade; every kit part the families do not sculpt, rebuilt as a finished piece
(`piece`); the backdrop's near ring and tunnels (`far_scene`). `build` returns the family it modelled, or None."""
import math

import numpy as np

import stagekit as sk
from stagekit import box, sweep, lathe
import cybercity_kit as ck
from cybercity_kit import F, Fr, glow, rod, h01, pick, grid_face
from cybercity_street import top, rail, CELL

LIT = (0xFFC98A, 0xCFE4FF, 0xFFE2B8, 0x9FE8FF, 0xFF9AD8, 0xFFD27A)


def build(g, S, b, T):
    s = b.stem
    if s.startswith('frontage-'):
        frontage(g, S, b, T)
        return 'frontage'
    if s.startswith('station-') or s.startswith('skybridge-'):
        station(g, S, b, T)
        return 'station'
    if s.startswith(('nova-', 'vega-')):
        podium(g, S, b, T)
        return 'podium'
    if s.startswith(('lantern-', 'signal-')):
        market(g, S, b, T)
        return 'market'
    if s.startswith(('neon-', 'rain-', 'alley-', 'lamp-')):
        avenue(g, S, b, T)
        return 'avenue'
    if s.startswith(('rank-', 'dock-')):
        yard(g, S, b, T)
        return 'yard'
    if s.startswith(('prom-', 'plaza-planter')):
        promenade(g, S, b, T)
        return 'promenade'
    return None


# ---------------------------------------------------------------------------------------------------------------------------
# Kit parts rebuilt as finished pieces
# ---------------------------------------------------------------------------------------------------------------------------
def metal_fin(col):
    name = 'm-%06x' % col
    if name not in F:
        F[name] = sk.fin(name, col, sk.DETAIL_NONE, smooth=.6, metal=.5, dirt=.4, wear=.35)
    return F[name]


def part_frame(b, p):
    """The face frame a thin upright kit part lies on: its back plane, out along its thin axis away from its block. None for
    a part that is not thin and upright."""
    c, h = p['c'], p['h']
    a = 0 if h[0] <= h[2] else 2
    if h[a] > min(h[1], h[2 - a]) or h[a] > .3:
        return None
    sgn = 1 if c[a] >= b.c[a] else -1
    o = [c[0], c[1] - h[1], c[2]]
    o[a] = c[a] - sgn * h[a]
    n = (sgn, 0, 0) if a == 0 else (0, 0, sgn)
    return Fr(tuple(o), n, h[2 - a], 2 * h[1]), 2 * h[a]


def piece(g, S, b, p, T, seed=0):
    """A steady kit part as a finished piece: a neon tube or LED strip, a screen with its poster, a glass pane in its frame, a
    lit window, a chamfered metal or timber piece, a painted mark, hazard stripes."""
    c, h, look, col, gain = p['c'], p['h'], p['look'], p['col'], p['gain']
    x0, x1, y0, y1, z0, z1 = c[0] - h[0], c[0] + h[0], c[1] - h[1], c[1] + h[1], c[2] - h[2], c[2] + h[2]
    lo = min(h)
    if look in ('cyber-tube', 'neon'):
        if T.i == 2 and max(h) < .12:
            return
        box(g, x0, x1, y0, y1, z0, z1, glow(col, gain), None, 0, cell=0)
        return
    pf = part_frame(b, p)
    if look == 'cyber-holo' and pf:
        fr, d = pf
        ck.screen(g, fr, -fr.half, fr.half, 0, fr.tall, col, gain, T, seed + int(c[0] * 31 + c[2] * 17 + c[1] * 7), d=d, bezel=min(.06, fr.half * .12))
        return
    if look == 'cyber-glass' and pf:
        fr, d = pf
        pane(g, fr, -fr.half, fr.half, 0, fr.tall, d, T)
        return
    if look == 'window-lit' and pf:
        fr, d = pf
        fr.box(g, -fr.half, fr.half, 0, fr.tall, 0, d, F['trim'], T, .008)
        fr.quad(g, -fr.half + .05, fr.half - .05, .05, fr.tall - .05, d + .003, glow(0xFFC98A, 1.1), 0)
        return
    if look == 'demolition':
        box(g, x0, x1, y0, y1, z0, z1, F['hazardk'], None, 0, cell=0)
        if pf:
            fr, d = pf
            ck.stripes(g, fr.at(-fr.half, 0, d + .002), (fr.ud[0] * 2 * fr.half, 0, fr.ud[2] * 2 * fr.half), (0, fr.tall, 0), fr.n, max(.12, fr.tall * .6), F['hazardy'], F['hazardk'])
        elif h[1] <= lo + 1e-6:
            if h[0] >= h[2]:
                ck.stripes(g, (x0, y1 + .002, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), (0, 1, 0), max(.15, 2 * h[2]), F['hazardy'], F['hazardk'])
            else:
                ck.stripes(g, (x0, y1 + .002, z0), (0, 0, z1 - z0), (x1 - x0, 0, 0), (0, 1, 0), max(.15, 2 * h[0]), F['hazardy'], F['hazardk'])
        return
    if look == 'timber':
        f = F['timberx'] if h[0] >= h[2] else F['timberz']
    elif look == 'plain':
        f = F['leaf'] if (col >> 8 & 255) > (col >> 16 & 255) + 10 else F['paint']
    elif look == 'cyber-tile':
        f = F['paver']
    elif look == 'cyber-grate':
        f = F['grate']
    elif look == 'cyber-glass':
        f = F['glass']
    else:
        f = metal_fin(col)
    if T.i == 2 and max(h) < .08:
        return
    # a thin square bar becomes a round one
    order = sorted(range(3), key=lambda i: h[i])
    a, bb, cc = order
    if abs(h[a] - h[bb]) < .006 and h[cc] > 2.5 * h[bb] and h[bb] < .09:
        e = [0.0, 0.0, 0.0]
        e[cc] = h[cc]
        rod(g, sk._sub(c, e), sk._add(c, e), h[bb] * 1.1, f, (6, 5, 4)[T.i], caps=T.i == 0)
        return
    bevel = 0.0 if (lo < .012 or T.i == 2) else min(.02, lo * .35)
    box(g, x0, x1, y0, y1, z0, z1, f, T if bevel > 0 else None, bevel, cell=0)


def pieces(g, S, b, T, skip=None):
    for i, p in enumerate(S.parts(b)):
        if skip is not None and skip(p):
            continue
        piece(g, S, b, p, T, i)


def pane(g, fr, u0, u1, v0, v1, d, T, lit=None, bars=0):
    """A glass pane in a slim frame (lit: a glow colour behind it)."""
    fr.box(g, u0, u1, v0, v1, 0, d, F['trim'], T, .006)
    m = min(.04, (u1 - u0) * .1, (v1 - v0) * .1)
    fr.quad(g, u0 + m, u1 - m, v0 + m, v1 - m, d + .003, F['glass'] if lit is None else glow(lit, 1.0), 0)
    if bars and T.detail:
        for k in range(1, bars + 1):
            u = u0 + (u1 - u0) * k / (bars + 1)
            fr.box(g, u - .015, u + .015, v0 + m, v1 - m, d, d + .012, F['trim'], None, 0)


def slab(g, b, ftop, fside, fbot, cell=0, y0=None, y1=None):
    """A block as a plain slab: its top, bottom and sides in their own finishes."""
    y0 = b.y0 if y0 is None else y0
    y1 = b.y1 if y1 is None else y1
    top(g, b.x0, b.x1, b.z0, b.z1, y1, ftop, cell)
    if fbot is not None:
        grid_face(g, (b.x0, y0, b.z0), (b.x1 - b.x0, 0, 0), (0, 0, b.z1 - b.z0), 0, 0, fbot, (0, -1, 0), cell if cell else 99)
    if fside is not None:
        for (o, U, n) in (((b.x0, y0, b.z0), (b.x1 - b.x0, 0, 0), (0, 0, -1)), ((b.x0, y0, b.z1), (b.x1 - b.x0, 0, 0), (0, 0, 1)),
                          ((b.x0, y0, b.z0), (0, 0, b.z1 - b.z0), (-1, 0, 0)), ((b.x1, y0, b.z0), (0, 0, b.z1 - b.z0), (1, 0, 0))):
            g.face((o, sk._add(o, U), sk._add(sk._add(o, U), (0, y1 - y0, 0)), sk._add(o, (0, y1 - y0, 0))), fside, n)


def fallback(g, S, b, T):
    box(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, F['gunmetal'], T, .02, cell=0)
    pieces(g, S, b, T)


# ---------------------------------------------------------------------------------------------------------------------------
# The frontage
# ---------------------------------------------------------------------------------------------------------------------------
def frontage(g, S, b, T):
    """A frontage block as a row of buildings: shopfronts and shutters under fascia signs and awnings, three storeys of window
    bands with lit panes, pilasters, floor ledges with their LED lines, blade signs, condensers, screens, a cornice and the
    roofs' plant. The kit's parts say which bay carries what."""
    fr = b.inward()
    half, tall = fr.half, b.s[1]
    bays = max(1, int(half * 2 / 7.2))
    bay = half * 2 / bays
    parts = S.parts(b)
    # the frame's u of each part, and what it is
    items = []
    for p in parts:
        c, h = p['c'], p['h']
        a = 0 if fr.n[0] != 0 else 2
        along = 2 - a
        u = fr.world_u(c[along] - b.c[along])
        v0 = c[1] - h[1] - b.y0
        items.append((p, u, h[along], v0, 2 * h[1], 2 * h[a]))
    holes = []
    for (p, u, hu, v0, tv, dp) in items:
        if p['look'] == 'cyber-glass' or (p['look'] == 'cyber-metal' and tv > 2):
            holes.append((u - hu, u + hu, v0, v0 + tv))
    cell = (2.4, 4.8, 9.0)[T.i]
    # the wall, building by building (a building is one or two bays)
    i = 0
    k = 0
    while i < bays:
        n = 2 if (h01(b.id, 'bld', k) < .6 and i + 1 < bays) else 1
        u0, u1 = -half + i * bay, -half + (i + n) * bay
        clad = F['clad%d' % int(h01(b.id, 'clad', k) * 6)]
        ck.holed(g, fr, u0, u1, 0, tall, [h for h in holes if h[0] >= u0 - 1e-3 and h[1] <= u1 + 1e-3], clad, 0, cell, rev=.24 if T.i < 2 else .12, frev=F['trim'])
        if T.i < 2:
            # pilasters, the ground storey's plinth, the storeys' ledges and the cornice
            for u in (u0, u1):
                fr.box(g, u - .22, u + .22, 0, tall - .7, 0, .1, clad, T, .03, skip=('-y',))
            fr.box(g, u0 + .22, u1 - .22, 3.68, 3.84, 0, .16, F['trim'], T, .02)
            for v in (7.2, 10.8):
                fr.box(g, u0 + .22, u1 - .22, v + .06, v + .16, 0, .07, F['trim'], T, .012)
        fr.box(g, u0, u1, tall - .7, tall, 0, .22, F['trim'], T, .04)
        roof(g, fr, b, u0, u1, T, k)
        i += n
        k += 1
    # the top, behind the cornice
    top(g, b.x0, b.x1, b.z0, b.z1, b.y1, F['concretewet'], 0)
    seed = 0
    for (p, u, hu, v0, tv, dp) in items:
        seed += 1
        look = p['look']
        a, e = u - hu, u + hu
        if look == 'cyber-glass' and tv > 2.2:
            shopfront(g, fr, a, e, v0, v0 + tv, T, b.id, seed)
        elif look == 'cyber-glass':
            windows(g, fr, a, e, v0, v0 + tv, T, b.id, seed)
        elif look == 'cyber-metal' and tv > 2:
            shutter(g, fr, a, e, v0, v0 + tv, T)
        elif look == 'cyber-metal':
            condenser(g, fr, a, e, v0, v0 + tv, dp, T)
        elif look == 'cyber-holo' and tv < 1:
            # a fascia sign over the shop
            fr.box(g, a, e, v0, v0 + tv, 0, .1, F['metaldark'], T, .015)
            ck.screen(g, fr.shifted(dd=.1), a + .03, e - .03, v0 + .03, v0 + tv - .03, p['col'], p['gain'], T, seed * 13 + int(u * 7), d=.02, bezel=.02)
            if T.detail and h01(b.id, 'awn', seed) < .6:
                awning(g, fr, a - .1, e + .1, v0 - .12, T, pick(('awningred', 'awningteal', 'awningdark'), b.id, 'awc', seed))
        elif look == 'cyber-holo':
            # a big screen in a frame on struts
            if T.detail:
                for uu in (a + .4, e - .4):
                    fr.box(g, uu - .05, uu + .05, v0 - .5, v0 + tv + .3, 0, .1, F['gunmetal'], None, 0)
            ck.screen(g, fr.shifted(dd=.1), a, e, v0, v0 + tv, p['col'], p['gain'], T, seed * 29 + int(u * 3), d=.1, bezel=.1)
        elif look == 'cyber-tube' and tv > 5:
            blade(g, fr, u, hu * 2, v0, v0 + tv, dp, p['col'], p['gain'], T)
        elif look == 'cyber-tube':
            d0 = .16 if abs(v0 - 3.6) < .05 else .22 if v0 > 15 else 0
            ck.led(g, fr.shifted(dd=d0), a + .02, e - .02, v0, v0 + tv, p['col'], p['gain'], T, d=.045, housing=False)


def windows(g, fr, a, e, v0, v1, T, key, seed):
    """A storey's window band in its recess: mullions, a transom, a sill; some panes lit."""
    d = -.24 if T.i < 2 else -.12
    n = max(2, int(round((e - a) / 1.15)))
    w = (e - a) / n
    for i in range(n):
        r = h01(key, 'pane', seed, i)
        f = glow(pick(LIT, key, 'lit', seed, i), .75 + .5 * h01(key, 'gain', seed, i)) if r < .36 else (F['glass'] if r < .8 else F['glassblue'])
        fr.quad(g, a + i * w, a + (i + 1) * w, v0, v1, d, f, 0)
        if T.detail and sk.FINS[f].glow > 0 and h01(key, 'blind', seed, i) < .45:
            # a half-drawn blind
            vb = v0 + (v1 - v0) * (.45 + .3 * h01(key, 'bl', seed, i))
            fr.quad(g, a + i * w + .03, a + (i + 1) * w - .03, vb, v1, d + .03, F['plastic'], 0)
    if T.i == 2:
        return
    for i in range(1, n):
        fr.box(g, a + i * w - .025, a + i * w + .025, v0, v1, d, d + .07, F['trim'], None, 0)
    if T.i == 0:
        fr.box(g, a, e, v0 + (v1 - v0) * .7 - .02, v0 + (v1 - v0) * .7 + .02, d, d + .05, F['trim'], None, 0)
    fr.box(g, a - .06, e + .06, v0 - .07, v0, -.2, .07, F['trimlight'], T, .012)


def shopfront(g, fr, a, e, v0, v1, T, key, seed):
    """A shop's front in its recess: a lit window on a tiled stall riser, goods in silhouette, a glazed door."""
    d = -.24 if T.i < 2 else -.12
    lit = pick(LIT, key, 'shop', seed)
    door = (e - a) * (.22 if h01(key, 'door', seed) < .5 else .78)
    dw = .5
    fr.quad(g, a, e, v0, v1, d, glow(lit, .85), 0)
    if T.i == 2:
        return
    # the riser and the frame
    fr.box(g, a, a + door - dw, v0, v0 + .45, d, d + .1, F['clad5'], T, .015)
    fr.box(g, a + door + dw, e, v0, v0 + .45, d, d + .1, F['clad5'], T, .015)
    for u in (a + door - dw, a + door + dw):
        fr.box(g, u - .03, u + .03, v0, v1, d, d + .08, F['trim'], None, 0)
    fr.box(g, a, e, v1 - .32, v1 - .26, d, d + .08, F['trim'], None, 0)
    # the door: dark glass with a handle
    fr.quad(g, a + door - dw + .03, a + door + dw - .03, v0, v1 - .32, d + .02, F['glassblue'], 0)
    if T.i == 0:
        fr.box(g, a + door + dw - .14, a + door + dw - .1, v0 + .9, v0 + 1.3, d + .02, d + .07, F['chrome'], None, 0)
        # goods in the window, dark against the light
        for (s0, s1) in ((a + .1, a + door - dw - .1), (a + door + dw + .1, e - .1)):
            x = s0
            j = 0
            while x < s1 - .3:
                w = .25 + .35 * h01(key, 'gw', seed, j, int(s0 * 10))
                hh = .25 + .8 * h01(key, 'gh', seed, j, int(s0 * 10))
                fr.box(g, x, min(x + w, s1), v0 + .45, v0 + .45 + hh, d + .01, d + .07, F['metaldark'] if j % 3 else F['awningred'], None, 0)
                x += w + .08 + .2 * h01(key, 'gg', seed, j)
                j += 1


def shutter(g, fr, a, e, v0, v1, T):
    """A rolled-down shutter in its recess under its box."""
    d = -.24 if T.i < 2 else -.12
    fr.quad(g, a, e, v0, v1, d, F['gunmetal'], 0)
    if T.i == 2:
        return
    ck.slats(g, fr, a + .05, e - .05, v0 + .1, v1 - .3, d, d + .05, .14, F['steel'], T)
    fr.box(g, a, e, v1 - .3, v1, d, d + .16, F['metaldark'], T, .02)
    fr.box(g, a, e, v0, v0 + .1, d, d + .09, F['metaldark'], None, 0)
    for u in (a, e - .05):
        fr.box(g, u, u + .05, v0 + .1, v1 - .3, d, d + .08, F['metaldark'], None, 0)


def condenser(g, fr, a, e, v0, v1, dp, T):
    """An air conditioner's outdoor unit on its bracket, its pipe going up into the wall."""
    if T.i == 2:
        fr.box(g, a, e, v0, v1, 0, dp, F['steel'], None, 0)
        return
    fr.box(g, a, e, v0, v1, .04, dp, F['plastic'], T, .025)
    ck.fan_grille(g, fr, (a + e) / 2 - (e - a) * .12, (v0 + v1) / 2, (v1 - v0) * .38, dp + .004, T)
    fr.box(g, a + .05, e - .05, v0 - .05, v0, 0, dp, F['metaldark'], None, 0)
    if T.i == 0:
        ck.pipe(g, [fr.at(e - .08, v1, dp * .5), fr.at(e - .08, v1 + .3, dp * .5), fr.at(e - .08, v1 + .3, 0)], .02, F['cable'], 4, bend=.08)


def awning(g, fr, a, e, v, T, cloth):
    """A sloped cloth awning over a shopfront with a scalloped valance, on two arms."""
    out, drop = .75, .32
    f = F[cloth]
    g.face((fr.at(a, v, 0), fr.at(e, v, 0), fr.at(e, v - drop, out), fr.at(a, v - drop, out)), f, sk._norm(sk._add(fr.n, (0, 1, 0), 2.2)))
    g.face((fr.at(a, v - .02, 0), fr.at(e, v - .02, 0), fr.at(e, v - drop - .02, out), fr.at(a, v - drop - .02, out)), f, sk._norm(sk._add(fr.n, (0, -1, 0), -2.2)))
    n = max(2, int((e - a) / .45))
    w = (e - a) / n
    for i in range(n):
        u0, u1 = a + i * w, a + (i + 1) * w
        g.face((fr.at(u0, v - drop, out), fr.at(u1, v - drop, out), fr.at(u1 - w * .15, v - drop - .16, out), fr.at(u0 + w * .15, v - drop - .16, out)), f, fr.n)
    for u in (a + .04, e - .04):
        rod(g, fr.at(u, v - .1, 0), fr.at(u, v - drop - .01, out), .015, F['steel'], 4, caps=False)


def blade(g, fr, u, w, v0, v1, dp, col, gain, T):
    """A blade sign down the wall: a lit box between dark caps on brackets."""
    f = glow(col, gain)
    fr.box(g, u - w / 2, u + w / 2, v0, v1, .05, dp + .05, f, None, 0, skip=('-y', '+y'))
    if T.i == 2:
        return
    for v in (v0 - .12, v1):
        fr.box(g, u - w / 2 - .02, u + w / 2 + .02, v, v + .12, .03, dp + .07, F['metaldark'], T, .01)
    n = max(2, int((v1 - v0) / 3))
    for k in range(n + 1):
        v = v0 + (v1 - v0) * k / n
        fr.box(g, u - .03, u + .03, min(v, v1 - .06), min(v, v1 - .06) + .06, 0, .06, F['metaldark'], None, 0)


def roof(g, fr, b, u0, u1, T, k):
    """A building's roof plant behind its cornice: a water tank on legs, a mast with its beacon, condenser cabinets or a
    sign's scaffold, above the frontage's top."""
    y = b.s[1]
    kind = int(h01(b.id, 'roof', k) * 5)
    um = (u0 + u1) / 2 + (h01(b.id, 'roofu', k) - .5) * (u1 - u0) * .4
    dz = -1.0  # into the block from its inward face
    if kind == 0:
        c = fr.at(um, y, dz)
        r, hh = .75, 1.5
        sides = (12, 8, 6)[T.i]
        if T.detail:
            for a in range(4):
                p = (c[0] + r * .7 * math.cos(a * math.pi / 2 + .78), c[1], c[2] + r * .7 * math.sin(a * math.pi / 2 + .78))
                rod(g, p, sk._add(p, (0, .7, 0)), .04, F['steel'], 4, caps=False)
        lathe(g, [(r, 0), (r, hh), (r * .6, hh + .28), (1e-3, hh + .36)], sk._add(c, (0, .7, 0)), F['timbery'], sides, cap0=True)
        if T.detail:
            for vv in (.25, hh - .25):
                lathe(g, [(r + .015, -.03), (r + .015, .03)], sk._add(c, (0, .7 + vv, 0)), F['steel'], sides)
    elif kind == 1:
        c = fr.at(um, y, dz)
        rod(g, c, sk._add(c, (0, 3.2, 0)), .05, F['steel'], (6, 4, 4)[T.i], caps=False)
        if T.detail:
            for vv in (1.6, 2.3, 2.9):
                rod(g, sk._add(c, (-.5 + vv * .1, vv, 0)), sk._add(c, (.5 - vv * .1, vv, 0)), .02, F['steel'], 4, caps=False)
        ck.ball(g, sk._add(c, (0, 3.25, 0)), .07, glow(ck.RED, 2.4), 6, 4)
    elif kind == 2:
        for j in range(2):
            uu = um + (j - .5) * 1.8
            fr.box(g, uu - .7, uu + .7, y, y + 1.0, dz - .5, dz + .5, F['steel'], T, .03, skip=('-y',))
            if T.detail:
                ck.fan_grille(g, Fr(fr.at(uu, y, dz + .5), fr.n), 0, .5, .36, .004, T)
    elif kind == 3:
        # a rooftop sign: a lit board on a steel scaffold
        w = min(4.2, (u1 - u0) * .6)
        col = pick(ck.NEONS, b.id, 'rsign', k)
        if T.detail:
            for uu in (um - w / 2 + .2, um + w / 2 - .2):
                rod(g, fr.at(uu, y, -.3), fr.at(uu, y + 2.3, -.3), .035, F['steel'], 4, caps=False)
                rod(g, fr.at(uu, y, -1.5), fr.at(uu, y + 1.6, -.3), .03, F['steel'], 4, caps=False)
        ck.screen(g, Fr(fr.at(um, y + .9, -.26), fr.n), -w / 2, w / 2, 0, 1.5, col, 1.5, T, k * 7 + 3, d=.08, bezel=.07)


# ---------------------------------------------------------------------------------------------------------------------------
# The station and the skybridges
# ---------------------------------------------------------------------------------------------------------------------------
def station(g, S, b, T):
    s = b.stem
    if '-rail' in s:
        rail(g, S, b, T)
    elif s == 'station-deck':
        deck(g, S, b, T)
    elif s.startswith('station-col-'):
        column(g, S, b, T)
    elif s == 'station-lift':
        lift(g, S, b, T)
    elif s == 'station-lift-head':
        lift_head(g, S, b, T)
    elif '-bench-' in s:
        bench(g, b, T)
    elif '-stair-' in s:
        step(g, S, b, T, F['concrete'], F['paver'])
    else:
        # landings and skybridges: a paved deck between steel edge beams
        slab(g, b, F['paver'], F['gunmetal'], F['metaldark'], CELL[T.i] * 1.5)
        if T.i < 2 and b.y0 > 1:
            alongx = b.s[0] > b.s[2]
            for k in range(1, max(2, int((b.s[0] if alongx else b.s[2]) / 1.4))):
                if alongx:
                    x = b.x0 + k * 1.4
                    box(g, x - .05, x + .05, b.y0 - .1, b.y0, b.z0 + .05, b.z1 - .05, F['gunmetal'], None, 0, cell=0)
                else:
                    z = b.z0 + k * 1.4
                    box(g, b.x0 + .05, b.x1 - .05, b.y0 - .1, b.y0, z - .05, z + .05, F['gunmetal'], None, 0, cell=0)
        pieces(g, S, b, T)


def step(g, S, b, T, f, ftop=None):
    """A stair's step (solid to the ground, or a tread of an open flight): chamfered edges where the kit's nosing lights sit."""
    box(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, f, T if T.i < 2 else None, .045 if T.i < 2 else 0, cell=0, skip=('-y',) if b.y0 < .01 or abs(b.y0 + 4.5) < .01 else ())
    pieces(g, S, b, T)


def deck(g, S, b, T):
    """The concourse deck: paving, a steel fascia with the kit's LED line, cross girders under it carrying the under-lights."""
    cell = CELL[T.i]
    top(g, b.x0, b.x1, b.z0, b.z1, b.y1, F['paver'], cell)
    grid_face(g, (b.x0, b.y0, b.z0), (b.x1 - b.x0, 0, 0), (0, 0, b.z1 - b.z0), 0, 0, F['metaldark'], (0, -1, 0), cell * 1.5)
    for (ax, sg) in ((0, 1), (0, -1), (2, 1), (2, -1)):
        fr = b.face(ax, sg)
        fr.quad(g, -fr.half, fr.half, 0, b.s[1], 0, F['gunmetal'], 0)
        if T.detail:
            fr.box(g, -fr.half, fr.half, 0, .08, 0, .05, F['steel'], None, 0)
            fr.box(g, -fr.half, fr.half, b.s[1] - .08, b.s[1], 0, .05, F['steel'], None, 0)
    for p in S.parts(b):
        c, h = p['c'], p['h']
        if p['look'] == 'cyber-tube' and h[1] < .03 and c[1] < b.y0:
            # an under-light: a girder's lower flange with the light let into it
            x0, x1, z0, z1 = c[0] - h[0], c[0] + h[0], c[2] - h[2], c[2] + h[2]
            if T.i < 2:
                box(g, c[0] - .05, c[0] + .05, b.y0 - .3, b.y0, z0, z1, F['gunmetal'], None, 0, cell=0)
                box(g, c[0] - .16, c[0] + .16, b.y0 - .34, b.y0 - .3, z0, z1, F['gunmetal'], None, 0, cell=0)
            yb = b.y0 - (.34 if T.i < 2 else 0)
            g.face(((x0, yb - .004, z0), (x1, yb - .004, z0), (x1, yb - .004, z1), (x0, yb - .004, z1)), glow(p['col'], p['gain']), (0, -1, 0))
        else:
            piece(g, S, b, p, T)


def column(g, S, b, T):
    """A station column: a square shaft with broad chamfers, the kit's corner lights let into them, on a plinth under a
    flared head."""
    cx, cz = b.c[0], b.c[2]
    r = b.s[0] / 2
    c = .2
    col = ck.ICE
    gain = 1.8
    for p in S.parts(b):
        if p['look'] == 'cyber-tube':
            col, gain = p['col'], p['gain']
    y0, y1 = b.y0, b.y1
    oct_ = [(cx - r + c, cz - r), (cx + r - c, cz - r), (cx + r, cz - r + c), (cx + r, cz + r - c), (cx + r - c, cz + r), (cx - r + c, cz + r), (cx - r, cz + r - c), (cx - r, cz - r + c)]
    yb, yt = y0 + .35, y1 - .5
    ck.prism(g, oct_, yb, yt, F['concrete'], top=False)
    # the lights in the chamfers
    for (sx, sz) in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        a = (cx + sx * (r - c * .78), cz + sx * 0 + sz * (r - c * .22))
        e = (cx + sx * (r - c * .22), cz + sz * (r - c * .78))
        n = sk._norm((sx, 0, sz))
        o = .012
        g.face(((a[0] + n[0] * o, yb + .15, a[1] + n[2] * o), (e[0] + n[0] * o, yb + .15, e[1] + n[2] * o), (e[0] + n[0] * o, yt - .15, e[1] + n[2] * o), (a[0] + n[0] * o, yt - .15, a[1] + n[2] * o)), glow(col, gain), n)
    box(g, b.x0 - .04, b.x1 + .04, y0, yb, b.z0 - .04, b.z1 + .04, F['concretedark'], T, .04, cell=0, skip=('-y',))
    # the head
    for k, (e, ya, yc) in enumerate(((.0, yt, yt + .16), (.1, yt + .16, yt + .34), (.2, yt + .34, y1))):
        box(g, b.x0 - e, b.x1 + e, ya, yc, b.z0 - e, b.z1 + e, F['concrete'] if k < 2 else F['gunmetal'], T, .03, cell=0, skip=('+y',) if k == 2 else ())


def lift(g, S, b, T):
    """The lift's shaft: steel corner posts, glazing bars, glass with a few lit panes, the doors on the two faces the kit
    gives them, their lit frames and floor indicators."""
    r = b.s[0] / 2
    tall = b.s[1]
    for (ax, sg) in ((0, 1), (0, -1), (2, 1), (2, -1)):
        fr = b.face(ax, sg)
        n = 3
        w = (2 * r - .3) / n
        rows = max(1, int(round(tall / 1.65)))
        hh = tall / rows
        for i in range(n):
            for j in range(rows):
                lit = h01(b.id, ax, sg, i, j) < .22 and not (ax == 2 and j < 2 and i == 1)
                f = glow(pick((ck.ICE, ck.CYAN, 0xCFE4FF), b.id, 'l', i, j, ax + sg), .7) if lit else F['glassblue']
                fr.quad(g, -r + .15 + i * w, -r + .15 + (i + 1) * w, j * hh, (j + 1) * hh, -.03, f, 0)
        if T.i < 2:
            for i in range(1, n):
                fr.box(g, -r + .15 + i * w - .025, -r + .15 + i * w + .025, 0, tall, -.03, .0, F['steel'], None, 0)
            for j in range(1, rows):
                fr.box(g, -r + .15, r - .15, j * hh - .03, j * hh + .03, -.03, .0, F['steel'], None, 0)
    for (sx, sz) in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        x, z = b.c[0] + sx * (r - .075), b.c[2] + sz * (r - .075)
        box(g, x - .075, x + .075, b.y0, b.y1, z - .075, z + .075, F['gunmetal'], T, .02, cell=0, skip=('-y', '+y'))
    for p in S.parts(b):
        if p['look'] == 'cyber-metal':
            fr, d = part_frame(b, p)
            # the doors: two leaves with a seam, in a frame
            fr.box(g, -fr.half, fr.half, 0, fr.tall, 0, d, F['steel'], T, .01)
            if T.detail:
                fr.box(g, -.012, .012, .02, fr.tall - .02, d, d + .006, F['void'], None, 0)
                for s in (-1, 1):
                    fr.box(g, s * fr.half * .5 - .16, s * fr.half * .5 + .16, fr.tall * .55, fr.tall * .85, d, d + .008, F['glassblue'], None, 0)
        else:
            piece(g, S, b, p, T)


def lift_head(g, S, b, T):
    """The lift's head over the concourse: a housing with louvres, HELIX in neon on each face, a lit roof."""
    box(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, F['gunmetal'], T, .04, cell=0, skip=('-y',))
    r = b.s[0] / 2
    for (ax, sg) in ((0, 1), (0, -1), (2, 1), (2, -1)):
        fr = b.face(ax, sg)
        if T.detail:
            ck.slats(g, fr, -r + .25, r - .25, .2, 1.05, .005, .05, .12, F['steel'], T)
        ck.sign_board(g, fr, -1.25, 1.25, 1.2, 1.95, T, d=.02)
        ck.neon_text(g, fr, 'HELIX', 0, 1.3, .075, ck.CYAN if ax == 0 else ck.MAGENTA, 2.6, T, d=.06)
    for p in S.parts(b):
        if p['look'] == 'cyber-tube' and p['h'][1] < .05 and p['h'][0] > 1:
            c, h = p['c'], p['h']
            box(g, c[0] - h[0] + .12, c[0] + h[0] - .12, b.y1, b.y1 + .05, c[2] - h[2] + .12, c[2] + h[2] - .12, glow(p['col'], p['gain']), None, 0, cell=0, skip=('-y',))


def bench(g, b, T):
    """A bench: a slatted seat on two cast legs."""
    alongx = b.s[0] >= b.s[2]
    y = b.y1
    if T.i == 2:
        box(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, F['gunmetal'], None, 0, cell=0, skip=('-y',))
        return
    n = 3
    for i in range(n):
        if alongx:
            z = b.z0 + (i + .5) * b.s[2] / n
            box(g, b.x0, b.x1, y - .06, y, z - b.s[2] / n * .42, z + b.s[2] / n * .42, F['timberx'], T, .012, cell=0)
        else:
            x = b.x0 + (i + .5) * b.s[0] / n
            box(g, x - b.s[0] / n * .42, x + b.s[0] / n * .42, y - .06, y, b.z0, b.z1, F['timberz'], T, .012, cell=0)
    for s in (-1, 1):
        if alongx:
            x = b.c[0] + s * (b.s[0] / 2 - .25)
            box(g, x - .05, x + .05, b.y0, y - .06, b.z0 + .04, b.z1 - .04, F['gunmetal'], T, .012, cell=0, skip=('-y',))
        else:
            z = b.c[2] + s * (b.s[2] / 2 - .25)
            box(g, b.x0 + .04, b.x1 - .04, b.y0, y - .06, z - .05, z + .05, F['gunmetal'], T, .012, cell=0, skip=('-y',))


# ---------------------------------------------------------------------------------------------------------------------------
# The podiums
# ---------------------------------------------------------------------------------------------------------------------------
def podium(g, S, b, T):
    s = b.stem
    nova = s.startswith('nova')
    clad = F['clad4'] if nova else F['clad3']
    accent = ck.MAGENTA if nova else ck.CYAN
    if s.endswith('-spire'):
        spire(g, S, b, T, accent)
    elif '-roof-unit-' in s:
        roof_unit(g, S, b, T)
    elif '-stair-' in s:
        step(g, S, b, T, F['concrete'])
    elif '-floor-' in s or '-roof-' in s:
        slab(g, b, F['concretewet'] if '-roof-' in s else F['paver'], F['gunmetal'], F['concretedark'], CELL[T.i] * 1.3)
        for p in S.parts(b):
            c, h = p['c'], p['h']
            if p['look'] == 'cyber-tube' and c[1] < b.y0:
                # a ceiling light: a troffer let into the soffit
                if T.detail:
                    box(g, c[0] - h[0] - .04, c[0] + h[0] + .04, b.y0 - .03, b.y0, c[2] - h[2] - .04, c[2] + h[2] + .04, F['trim'], None, 0, cell=0, skip=('+y',))
                yb = b.y0 - (.034 if T.detail else .004)
                g.face(((c[0] - h[0], yb, c[2] - h[2]), (c[0] + h[0], yb, c[2] - h[2]), (c[0] + h[0], yb, c[2] + h[2]), (c[0] - h[0], yb, c[2] + h[2])), glow(p['col'], p['gain']), (0, -1, 0))
            else:
                piece(g, S, b, p, T)
    elif '-parapet-' in s:
        box(g, b.x0, b.x1, b.y0, b.y1 - .08, b.z0, b.z1, clad, T, .015, cell=0, skip=('-y', '+y'))
        e = .04
        box(g, b.x0 - e, b.x1 + e, b.y1 - .08, b.y1, b.z0 - e, b.z1 + e, F['trim'], T, .015, cell=0)
        pieces(g, S, b, T)
    elif s.endswith('-lobby-desk'):
        desk(g, S, b, T, accent)
    else:
        wall_piece(g, S, b, T, clad, accent)


def wall_piece(g, S, b, T, clad, accent):
    """A piece of a podium's shell: a pier with steel edges and a recessed fin, a sill with its rail, or a lintel beam. The
    kit's floor-line lights, edge frames and ribbons ride it."""
    alongx = b.s[0] > b.s[2]
    tall = b.s[1]
    pier = tall > 2.2
    box(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, clad if pier else F['clad5'], T, .015, cell=0, skip=('-y',) if b.y0 < .01 else ())
    if T.i < 2:
        L = b.s[0] if alongx else b.s[2]
        for sg in (-1, 1):
            fr = b.face(2 if alongx else 0, sg)
            if pier and L > 1.3:
                # a recessed panel between the edges, with a slim fin
                fr.box(g, -L / 2 + .22, L / 2 - .22, .3, tall - .3, 0, .012, F['trim'], None, 0)
                if T.i == 0 and L > 2.4:
                    fr.box(g, -.04, .04, .3, tall - .3, .012, .05, F['trimlight'], None, 0)
            elif not pier and tall <= 1.0 and b.y0 - math.floor(b.y0 / 3.6 + 1e-3) * 3.6 < .3:
                pass
        if not pier and (b.y0 % 3.6) < .2:
            # a sill: its rail cap
            e = .03
            box(g, b.x0 - (0 if alongx else e), b.x1 + (0 if alongx else e), b.y1 - .05, b.y1 + .0, b.z0 - (e if alongx else 0), b.z1 + (e if alongx else 0), F['steel'], T, .012, cell=0)
    pieces(g, S, b, T, skip=lambda p: p['look'] == 'cyber-metal' and min(p['h']) < .04 and T.i == 2)


def desk(g, S, b, T, accent):
    """The lobby's desk: a rounded counter with a lit front and a stone top."""
    alongx = b.s[0] >= b.s[2]
    ck.rbox(g, b.x0, b.x1, b.y0 - .02, b.y1 - .06, b.z0, b.z1, .12, F['clad5'], 2 if T.i < 2 else 1)
    box(g, b.x0 - .03, b.x1 + .03, b.y1 - .06, b.y1, b.z0 - .03, b.z1 + .03, F['ceramic'], T, .015, cell=0)
    for sg in (-1, 1):
        fr = b.face(2 if alongx else 0, sg)
        ck.led(g, fr, -fr.half + .3, fr.half - .3, .28, .34, accent, 1.8, T, d=.02, housing=False)


def roof_unit(g, S, b, T):
    """A rooftop plant cabinet: louvred sides, two fans under grilles on top, a status light."""
    box(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, F['steel'], T, .04, cell=0, skip=('-y',))
    for sg in (-1, 1):
        fr = b.face(2, sg)
        if T.detail:
            fr.box(g, -fr.half + .18, fr.half - .18, .35, b.s[1] - .35, 0, .012, F['metaldark'], None, 0)
            ck.slats(g, fr, -fr.half + .2, fr.half - .2, .38, b.s[1] - .38, .012, .05, .13, F['gunmetal'], T)
    if T.detail:
        for s in (-1, 1):
            ck.fan_grille(g, Fr((b.c[0] + s * b.s[0] * .22, b.y1, b.c[2]), (0, 1, 0)), 0, 0, min(b.s[0] * .18, b.s[2] * .36), .004, T) if False else None
            c = (b.c[0] + s * b.s[0] * .22, b.y1 + .004, b.c[2])
            r = min(b.s[0] * .18, b.s[2] * .36)
            ck.disc(g, c, r, F['void'], (12, 8, 6)[T.i], (0, 1, 0))
            ck.ring(g, c, r * .92, r * 1.06, F['gunmetal'], (12, 8, 6)[T.i], (0, 1, 0))
            if T.i == 0:
                for k in range(3):
                    a = math.pi * k / 3
                    rod(g, (c[0] - r * math.cos(a), c[1] + .01, c[2] - r * math.sin(a)), (c[0] + r * math.cos(a), c[1] + .01, c[2] + r * math.sin(a)), .012, F['steel'], 3, caps=False)
    pieces(g, S, b, T, skip=lambda p: p['look'] != 'cyber-tube')


def spire(g, S, b, T, accent):
    """A podium's spire: a curtain wall of glass between steel mullions and spandrels with scattered lit panes, corner posts,
    the kit's corner fins as light boxes up its top half, a stepped crown, the mast and its beacon, the name in neon."""
    r = b.s[0] / 2
    tall = b.s[1]
    storey = 3.6
    rows = int(round(tall / storey))
    bays = 4
    name = 'NOVA' if accent == ck.MAGENTA else 'VEGA'
    for (ax, sg) in ((0, 1), (0, -1), (2, 1), (2, -1)):
        fr = b.face(ax, sg)
        w = (2 * r - .5) / bays
        for j in range(rows):
            v0 = j * tall / rows
            v1 = v0 + tall / rows
            fr.quad(g, -r + .25, r - .25, v0, v0 + .5, 0, F['trim'], 0)
            for i in range(bays):
                lit = h01(b.id, 'sp', ax, sg, i, j) < .3
                f = glow(pick(LIT + (accent,), b.id, 'spc', ax + sg, i, j), .7 + .5 * h01(b.id, 'spg', ax, sg, i, j)) if lit else (F['glasstower'] if (i + j) % 3 else F['glassblue'])
                fr.quad(g, -r + .25 + i * w, -r + .25 + (i + 1) * w, v0 + .5, v1, -.04 if T.i < 2 else 0, f, 0)
            if T.i < 2:
                for i in range(1, bays):
                    fr.box(g, -r + .25 + i * w - .03, -r + .25 + i * w + .03, v0 + .5, v1, -.04, .03, F['steel'], None, 0)
    for (sx, sz) in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        x, z = b.c[0] + sx * (r - .125), b.c[2] + sz * (r - .125)
        box(g, x - .125, x + .125, b.y0, b.y1, z - .125, z + .125, F['gunmetal'], T, .03, cell=0, skip=('-y', '+y'))
    # the crown
    y = b.y1
    for k, (e, hh) in enumerate(((.12, .35), (-.35, .5), (-.9, .5))):
        box(g, b.x0 - e, b.x1 + e, y, y + hh, b.z0 - e, b.z1 + e, F['gunmetal'] if k != 1 else F['trim'], T, .05, cell=0, skip=('-y',))
        y += hh
    # the name on its boards (the kit's two faces)
    for (ax, sg) in ((2, -1 if b.c[2] > 0 else 1), (0, -1 if b.c[0] > 0 else 1)):
        fr = b.face(ax, sg)
        ck.sign_board(g, fr, -2.3, 2.3, 16.3, 18.1, T, d=.05, f=F['void'])
        ck.neon_text(g, fr, name, 0, 16.5, .2, accent, 2.8, T, d=.16)
    for p in S.parts(b):
        c, h = p['c'], p['h']
        if p['look'] == 'cyber-tube' and h[1] > 3:
            # a corner fin
            box(g, c[0] - h[0], c[0] + h[0], c[1] - h[1], c[1] + h[1], c[2] - h[2], c[2] + h[2], glow(p['col'], p['gain']), None, 0, cell=0)
        elif p['look'] == 'cyber-tube' and h[1] < .1 and h[0] > 2:
            # the crown's light line
            e = .16
            for (x0, x1, z0, z1) in ((b.x0 - e, b.x1 + e, b.z0 - e, b.z0 - e + .06), (b.x0 - e, b.x1 + e, b.z1 + e - .06, b.z1 + e), (b.x0 - e, b.x0 - e + .06, b.z0 - e + .06, b.z1 + e - .06), (b.x1 + e - .06, b.x1 + e, b.z0 - e + .06, b.z1 + e - .06)):
                box(g, x0, x1, b.y1 + .12, b.y1 + .2, z0, z1, glow(p['col'], p['gain']), None, 0, cell=0)
        elif p['look'] == 'cyber-metal':
            # the mast: a tapered pole with rings and a beacon
            x, z = c[0], c[2]
            yb, yt = b.y1 + 1.35, c[1] + h[1]
            lathe(g, [(.22, 0), (.16, (yt - yb) * .35), (.08, (yt - yb) * .7), (.04, yt - yb)], (x, yb, z), F['steel'], (8, 6, 4)[T.i])
            if T.detail:
                for k in (.35, .7):
                    lathe(g, [(.3 - k * .2, -.04), (.3 - k * .2, .04)], (x, yb + (yt - yb) * k, z), F['gunmetal'], 8)
            ck.ball(g, (x, yt + .08, z), .12, glow(ck.RED, 2.6), 6, 4)


# ---------------------------------------------------------------------------------------------------------------------------
# The markets
# ---------------------------------------------------------------------------------------------------------------------------
def market(g, S, b, T):
    s = b.stem
    north = s.startswith('lantern')
    if '-wall-' in s:
        market_wall(g, S, b, T, north)
    elif '-post-' in s:
        post(g, S, b, T, True)
    elif s.endswith('-bar'):
        bar(g, S, b, T, north)
    elif s.endswith('-counter'):
        counter(g, S, b, T)
    elif '-box-' in s:
        container(g, S, b, T)
    elif '-stair-' in s:
        step(g, S, b, T, F['gunmetal'])
    elif s.endswith('-booth'):
        booth(g, S, b, T)
    else:
        fallback(g, S, b, T)


def corrugated(g, fr, u0, u1, v0, v1, f, T, pitch=.22, depth=.035):
    """A corrugated sheet on a face."""
    if T.i == 2:
        fr.quad(g, u0, u1, v0, v1, depth * .5, f, 0)
        return
    n = max(1, int((u1 - u0) / (pitch * (1 if T.i == 0 else 2))))
    w = (u1 - u0) / n
    for i in range(n):
        a = u0 + i * w
        g.face((fr.at(a, v0, 0), fr.at(a + w * .5, v0, depth), fr.at(a + w * .5, v1, depth), fr.at(a, v1, 0)), f, sk._norm(sk._add(fr.n, fr.ud, -.8)))
        g.face((fr.at(a + w * .5, v0, depth), fr.at(a + w, v0, 0), fr.at(a + w, v1, 0), fr.at(a + w * .5, v1, depth)), f, sk._norm(sk._add(fr.n, fr.ud, .8)))


def market_wall(g, S, b, T, north):
    """A market wall: a steel hoarding of corrugated sheet between posts on a concrete footing, posters pasted on it, its lit
    cap; over a gateway, a lintel truss."""
    alongx = b.s[0] > b.s[2]
    tall = b.s[1]
    if tall < 1:
        box(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, F['gunmetal'], T, .03, cell=0)
        pieces(g, S, b, T)
        return
    t = min(b.s[0], b.s[2])
    box(g, b.x0, b.x1, b.y0, b.y0 + .4, b.z0, b.z1, F['concrete'], T, .03, cell=0, skip=('-y',))
    inset = t * .5 - .05
    if alongx:
        box(g, b.x0, b.x1, b.y0 + .4, b.y1, b.z0 + inset, b.z1 - inset, F['metaldark'], None, 0, cell=0, skip=('-y', '+y'))
    else:
        box(g, b.x0 + inset, b.x1 - inset, b.y0 + .4, b.y1, b.z0, b.z1, F['metaldark'], None, 0, cell=0, skip=('-y', '+y'))
    L = b.s[0] if alongx else b.s[2]
    sheet = F['container%d' % int(h01(b.id, 'sheet') * 4)]
    for sg in (-1, 1):
        fr = b.face(2 if alongx else 0, sg).shifted(dd=-inset)
        corrugated(g, fr, -L / 2 + .1, L / 2 - .1, .4, tall - .1, sheet, T, depth=inset * .8)
        n = max(1, int(round(L / 2.4)))
        if T.i < 2:
            for k in range(n + 1):
                u = -L / 2 + .06 + (L - .12) * k / n
                fr.box(g, u - .06, u + .06, .4, tall - .08, 0, inset, F['gunmetal'], None, 0)
        # posters
        if T.i < 2 and L > 2:
            for k in range(int(L / 1.6)):
                if h01(b.id, 'poster', sg, k) < .55:
                    u = -L / 2 + .5 + k * 1.6 + h01(b.id, 'pu', sg, k) * .3
                    w, hh = .7 + .3 * h01(b.id, 'pw', sg, k), 1.0 + .4 * h01(b.id, 'ph', sg, k)
                    if u + w > L / 2 - .2:
                        continue
                    col = pick((0xD8C8A0, 0xC04A5A, 0x3E8FA8, 0xD9A23A, 0x7A5AB8), b.id, 'pc', sg, k)
                    v = 1.0 + .5 * h01(b.id, 'pv', sg, k)
                    fr.quad(g, u, u + w, v, v + hh, inset + .004, metal_fin(col), 0)
                    fr.quad(g, u + w * .12, u + w * .88, v + hh * .55, v + hh * .88, inset + .008, metal_fin(ck.mix(col, 0x101018, .7)), 0)
    box(g, b.x0 - .02, b.x1 + .02, b.y1 - .1, b.y1, b.z0 - .02, b.z1 + .02, F['gunmetal'], T, .015, cell=0)
    pieces(g, S, b, T)


def lantern(g, c, w, hh, col, gain, T):
    """A paper lantern: a ribbed glowing body between dark caps, hung from above, a tassel under it."""
    sides = (10, 6, 5)[T.i]
    rings = (6, 4, 3)[T.i]
    r = w / 2 * 1.15
    prof = [(max(r * .35, r * math.sin(math.pi * (.14 + .72 * i / rings))), -hh / 2 * math.cos(math.pi * (.14 + .72 * i / rings)) / math.cos(math.pi * .14)) for i in range(rings + 1)]
    lathe(g, prof, c, glow(col, gain), sides)
    if T.i == 2:
        return
    for s in (-1, 1):
        y = c[1] + s * hh / 2
        lathe(g, [(r * .4, -.025), (r * .4, .025)], (c[0], y, c[2]), F['metaldark'], sides, cap0=True, cap1=True)
    rod(g, (c[0], c[1] + hh / 2, c[2]), (c[0], c[1] + hh / 2 + .12, c[2]), .008, F['cable'], 3, caps=False)
    if T.i == 0:
        rod(g, (c[0], c[1] - hh / 2 - .16, c[2]), (c[0], c[1] - hh / 2, c[2]), .014, F['awningred'], 4, caps=False)


def truss(g, a, e, depth, T, f=None):
    """An open lattice girder from a to e (horizontal), `depth` tall: two chords and a zigzag web."""
    f = F['gunmetal'] if f is None else f
    L = math.dist(a, e)
    if T.i == 2:
        d = sk._norm(sk._sub(e, a))
        if abs(d[0]) > abs(d[2]):
            box(g, min(a[0], e[0]), max(a[0], e[0]), a[1] - depth / 2, a[1] + depth / 2, a[2] - .03, a[2] + .03, f, None, 0, cell=0)
        else:
            box(g, a[0] - .03, a[0] + .03, a[1] - depth / 2, a[1] + depth / 2, min(a[2], e[2]), max(a[2], e[2]), f, None, 0, cell=0)
        return
    r = .028
    for s in (-1, 1):
        rod(g, sk._add(a, (0, s * depth / 2, 0)), sk._add(e, (0, s * depth / 2, 0)), r, f, 4, caps=False)
    n = max(2, int(L / (depth * (1.6 if T.i == 0 else 3.2))))
    for i in range(n):
        p = sk._lerp(a, e, i / n)
        q = sk._lerp(a, e, (i + 1) / n)
        s = 1 if i % 2 == 0 else -1
        rod(g, sk._add(p, (0, -s * depth / 2, 0)), sk._add(q, (0, s * depth / 2, 0)), r * .7, f, 3, caps=False)


def post(g, S, b, T, market_post):
    """A post: a steel pole on a base plate under a cap. A market's post carries the lantern strings (one cable through the
    kit's sagging pieces, paper lanterns where it hangs them) and the canopy's lattice girders."""
    cx, cz = b.c[0], b.c[2]
    r = b.s[0] / 2
    sides = (8, 6, 4)[T.i]
    sk.cylinder(g, (cx, b.y0, cz), (cx, b.y1, cz), r * .62, F['gunmetal'], sides, caps=True)
    if T.i < 2:
        box(g, b.x0 - .03, b.x1 + .03, b.y0, b.y0 + .08, b.z0 - .03, b.z1 + .03, F['metaldark'], T, .015, cell=0, skip=('-y',))
        lathe(g, [(r * .85, -.12), (r * .85, 0), (r * .5, .08)], (cx, b.y1, cz), F['metaldark'], sides)
    segs = []
    for p in S.parts(b):
        c, h, look = p['c'], p['h'], p['look']
        if look == 'neon':
            lantern(g, c, 2 * h[0], 2 * h[1], p['col'], p['gain'], T)
        elif look == 'cyber-metal' and h[1] < .03 and max(h) < .7:
            segs.append(c)
        elif look == 'cyber-metal' and max(h[0], h[2]) > 3:
            # a canopy girder
            if h[0] > h[2]:
                truss(g, (c[0] - h[0], c[1], c[2]), (c[0] + h[0], c[1], c[2]), .3, T)
            else:
                truss(g, (c[0], c[1], c[2] - h[2]), (c[0], c[1], c[2] + h[2]), .3, T)
        else:
            piece(g, S, b, p, T)
    if segs:
        segs.sort(key=lambda c: c[0])
        d = 1 if segs[-1][0] > cx else -1
        ends = [(cx, b.y1 - .9, cz), (cx + d * 11.3, b.y1 - .9, cz)]
        pts = [ends[0] if d > 0 else ends[1]] + [tuple(c) for c in segs] + [ends[1] if d > 0 else ends[0]]
        if T.i == 2:
            pts = pts[::3] + [pts[-1]]
        sweep(g, sk.circle_section(.012, 3), pts, F['cable'], caps=False)


def bar(g, S, b, T, north):
    """The noodle bar: a tiled front with a serving hatch lit from inside under a row of cloth flags, a red light box, its
    name in neon on a frame over the roof, a flue and ducting on the roof."""
    sg = -1 if b.c[2] > 0 else 1
    fr = b.face(2, sg)
    half, tall = fr.half, b.s[1]
    hatch = (-half + .5, half - .5, .9, 2.5)
    ck.holed(g, fr, -half, half, 0, tall, [hatch], F['clad1'], 0, 2.0, rev=.3, frev=F['timberdarkx'])
    fr.quad(g, hatch[0], hatch[1], hatch[2], hatch[3], -.3, glow(0xFFB86A, 1.0), 0)
    # the other three walls and the roof
    for (ax, s2) in ((2, -sg), (0, 1), (0, -1)):
        f2 = b.face(ax, s2)
        f2.quad(g, -f2.half, f2.half, 0, tall, 0, F['clad1'], 2.0)
    top(g, b.x0, b.x1, b.z0, b.z1, b.y1, F['concretewet'], 0)
    if T.i < 2:
        # the tiled dado and the counter ledge
        fr.box(g, -half, half, 0, .9, 0, .04, F['paverdark'], T, .01, skip=('-y',))
        fr.box(g, hatch[0] - .1, hatch[1] + .1, .86, .92, -.3, .22, F['timberx'], T, .012)
        # inside: shelves and pots in silhouette
        for k in range(5):
            u = hatch[0] + .5 + k * (hatch[1] - hatch[0] - 1) / 4
            fr.box(g, u - .16, u + .16, .92, 1.1 + .25 * h01('pot', k, sg), -.26, -.1, F['metaldark'], None, 0)
        fr.box(g, hatch[0], hatch[1], 1.95, 2.0, -.29, -.2, F['timberdarkx'], None, 0)
        # cloth flags across the hatch's head
        n = 7
        w = (hatch[1] - hatch[0]) / n
        for i in range(n):
            f = F['awningred'] if i % 2 == 0 else F['awningdark']
            g.face((fr.at(hatch[0] + i * w + .03, 2.02, .03), fr.at(hatch[0] + (i + 1) * w - .03, 2.02, .03), fr.at(hatch[0] + (i + 1) * w - .03, 2.5, .03), fr.at(hatch[0] + i * w + .03, 2.5, .03)), f, fr.n)
        # the eaves
        fr.box(g, -half - .1, half + .1, tall - .16, tall, 0, .35, F['timberdarkx'], T, .02)
    # the sign's frame over the roof and the name
    wrd = (len('NOODLE') * 6 - 1) * .1
    ck.sign_board(g, fr.shifted(dd=-.02), -wrd / 2 - .25, wrd / 2 + .25, tall + .0, tall + 1.15, T, d=.04, f=F['void'])
    ck.neon_text(g, fr, 'NOODLE', 0, 3.25, .1, ck.MAGENTA if b.c[2] > 0 else ck.CYAN, 2.8, T, d=.08)
    if T.detail:
        for u in (-wrd / 2, wrd / 2):
            rod(g, fr.at(u, tall, -.03), fr.at(u, tall + .9, -1.0), .025, F['steel'], 4, caps=False)
        # a low cowl and a hatch on the roof (nothing a pilot up there could walk through)
        c = fr.at(half - .8, tall, -2.6)
        lathe(g, [(.3, 0), (.3, .12), (.12, .22), (1e-3, .23)], c, F['steel'], (8, 6, 4)[T.i])
        fr.box(g, -half + .4, -half + 1.6, tall, tall + .12, -3.2, -2.2, F['steel'], T, .03, skip=('-y',))
    for p in S.parts(b):
        if p['look'] == 'neon':
            c, h = p['c'], p['h']
            pf = part_frame(b, p)
            if pf:
                f2, d = pf
                f2.box(g, -f2.half, f2.half, 0, f2.tall, 0, d, F['metaldark'], T, .01)
                f2.quad(g, -f2.half + .03, f2.half - .03, .03, f2.tall - .03, d + .003, glow(p['col'], p['gain']), 0)
                if T.i < 2:
                    ck.glyph_row(g, f2, -f2.half + .2, f2.half - .2, .08, f2.tall - .08, d + .008, glow(0xFFE6C8, 1.6), 5 + int(b.c[2]))


def counter(g, S, b, T):
    """The bar's counter: a timber top on a panelled front with a foot rail."""
    box(g, b.x0, b.x1, b.y0, b.y1 - .06, b.z0 + .04, b.z1 - .04, F['clad5'], T, .015, cell=0, skip=('-y',))
    box(g, b.x0 - .04, b.x1 + .04, b.y1 - .06, b.y1, b.z0 - .04, b.z1 + .04, F['timberx'], T, .015, cell=0)
    if T.i < 2:
        for sg in (-1, 1):
            fr = b.face(2, sg)
            n = int(b.s[0] / 1.0)
            for k in range(n):
                u = -fr.half + .1 + k * (2 * fr.half - .2) / n
                fr.box(g, u + .05, u + (2 * fr.half - .2) / n - .05, .15, b.s[1] - .2, -.04, -.025, F['timberdarkx'], None, 0)
            rod(g, fr.at(-fr.half + .1, .18, .06), fr.at(fr.half - .1, .18, .06), .02, F['chrome'], 5, caps=False)


def container(g, S, b, T):
    """A capsule hotel's container: corrugated sides between corner posts and rails, doors with locking bars on one end, a
    porthole window lit on the face the kit signs."""
    f = F['container%d' % int(h01(b.id, 'c') * 4)]
    box(g, b.x0 + .05, b.x1 - .05, b.y0 + .05, b.y1 - .05, b.z0 + .05, b.z1 - .05, f, None, 0, cell=0, skip=('-y',))
    # the frame
    for (sx, sz) in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        x, z = b.c[0] + sx * (b.s[0] / 2 - .06), b.c[2] + sz * (b.s[2] / 2 - .06)
        box(g, x - .06, x + .06, b.y0, b.y1, z - .06, z + .06, f, T, .015, cell=0, skip=('-y',))
    for y in (b.y0, b.y1 - .12):
        for sz in (-1, 1):
            z = b.c[2] + sz * (b.s[2] / 2 - .05)
            box(g, b.x0, b.x1, y, y + .12, z - .05, z + .05, f, T, .012, cell=0)
        for sx in (-1, 1):
            x = b.c[0] + sx * (b.s[0] / 2 - .05)
            box(g, x - .05, x + .05, y, y + .12, b.z0, b.z1, f, T, .012, cell=0)
    top(g, b.x0, b.x1, b.z0, b.z1, b.y1, f, 0)
    sg = -1 if b.c[2] > 0 else 1
    for s2 in (-1, 1):
        fr = b.face(2, s2).shifted(dd=-.05)
        corrugated(g, fr, -fr.half + .12, fr.half - .12, .12, b.s[1] - .12, f, T, pitch=.28, depth=.04)
        if s2 == sg and T.i < 2:
            # capsule windows: lit portholes in a row
            n = 3
            for k in range(n):
                u = -fr.half + (k + .5) * 2 * fr.half / n
                c = fr.at(u, b.s[1] * .42, .052)
                lit = h01(b.id, 'port', k) < .7
                ck.disc(g, c, .3, glow(pick(LIT, b.id, 'pl', k), .9) if lit else F['glass'], (12, 8, 6)[T.i], fr.n)
                ck.ring(g, sk._add(c, fr.n, .004), .3, .36, F['steel'], (12, 8, 6)[T.i], fr.n)
    # the doors on the east end
    fr = b.face(0, 1).shifted(dd=-.05)
    fr.quad(g, -fr.half + .12, fr.half - .12, .12, b.s[1] - .12, .02, f, 0)
    if T.i < 2:
        fr.box(g, -.012, .012, .12, b.s[1] - .12, .02, .03, F['void'], None, 0)
        for u in (-fr.half * .55, -fr.half * .2, fr.half * .2, fr.half * .55):
            rod(g, fr.at(u, .15, .05), fr.at(u, b.s[1] - .15, .05), .018, F['steel'], 4, caps=False)
            fr.box(g, u - .06, u + .06, b.s[1] * .45, b.s[1] * .45 + .05, .02, .07, F['steel'], None, 0)
    fr = b.face(0, -1).shifted(dd=-.05)
    corrugated(g, fr, -fr.half + .12, fr.half - .12, .12, b.s[1] - .12, f, T, pitch=.28, depth=.04)
    pieces(g, S, b, T, skip=lambda p: p['look'] == 'cyber-metal')


def booth(g, S, b, T):
    """The arcade booth: a cabinet-fronted kiosk with a lit screen wall, a canopy lip and the marquee board the kit's GAME
    sign flickers on."""
    sg = 1 if b.c[0] < 0 else -1
    box(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, F['clad4'], T, .04, cell=0, skip=('-y',))
    fr = b.face(0, sg)
    half, tall = fr.half, b.s[1]
    if T.i < 2:
        fr.box(g, -half, half, tall - .35, tall - .1, 0, .22, F['metaldark'], T, .03)
        ck.led(g, fr.shifted(dd=.22), -half + .1, half - .1, tall - .26, tall - .2, ck.VIOLET, 2.0, T, d=.02, housing=False)
        # arcade cabinets flanking the screen
        for s in (-1, 1):
            u = s * (half - .32)
            fr.box(g, u - .22, u + .22, 0, 1.7, 0, .2, F['metaldark'], T, .03, skip=('-y',))
            fr.quad(g, u - .16, u + .16, 1.0, 1.5, .204, glow(pick(ck.NEONS, 'cab', s, int(b.c[0])), 1.2), 0)
            fr.box(g, u - .2, u + .2, .8, .9, .2, .32, F['gunmetal'], T, .015)
    # the marquee's board behind the kit's sign
    fr.box(g, -1.5, 1.5, tall, tall + 1.05, -.1, -.012, F['void'], T, .015, skip=('-y',))
    if T.detail:
        for u in (-1.2, 1.2):
            rod(g, fr.at(u, tall, -.1), fr.at(u, tall + .8, -.9), .025, F['steel'], 4, caps=False)
    pieces(g, S, b, T)


# ---------------------------------------------------------------------------------------------------------------------------
# The avenue: vehicles, kiosks, pillars, lamps
# ---------------------------------------------------------------------------------------------------------------------------
def avenue(g, S, b, T):
    s = b.stem
    if '-van-' in s:
        vehicle(g, S, b, T, 'van')
    elif '-kiosk' in s:
        kiosk(g, S, b, T)
    elif '-pillar' in s:
        pillar(g, S, b, T)
    elif s.startswith('lamp-'):
        lamp(g, S, b, T)
    else:
        fallback(g, S, b, T)


def vehicle(g, S, b, T, kind):
    """A parked hover vehicle inside its block: a rounded body over a dark skirt, glazing, light bars at both ends, the
    under-glow, pods where wheels would be. A van is a box van with a raked cab; a cab a low saloon with a roof light."""
    alongx = b.s[0] >= b.s[2]
    L = b.s[0] if alongx else b.s[2]
    W = b.s[2] if alongx else b.s[0]
    H = b.s[1]
    # the vehicle's frame: u along its length (the kit's head lights at +), w across; built along x then turned
    t = sk.Geo()
    seg = 2 if T.i == 0 else 1
    body = F[pick(('vanwhite', 'vanblue', 'vanteal'), b.id, 'body')] if kind == 'van' else F['cab']
    x0, x1, z0, z1 = -L / 2, L / 2, -W / 2, W / 2
    skirt = .26
    # the skirt and the pods
    box(t, x0 + .12, x1 - .12, .1, skirt + .05, z0 + .1, z1 - .1, F['cabdark'], T, .03, cell=0)
    if T.i < 2:
        for sx in (-1, 1):
            for sz in (-1, 1):
                c = (sx * (L / 2 - .95), .16, sz * (W / 2 - .05))
                lathe(t, [(.3, -.1), (.34, 0), (.3, .1)], c, F['rubber'], (10, 6, 4)[T.i], axis=(0, 0, 1), cap0=True, cap1=True)
                if T.i == 0:
                    ck.disc(t, sk._add(c, (0, 0, sz * .101)), .18, F['steel'], 8, (0, 0, sz))
    if kind == 'van':
        cabl = 1.5
        # the cargo box and the cab
        ck.rbox(t, x0 + .02, x1 - cabl, skirt, H - .0, z0 + .0, z1 - .0, .14, body, seg)
        ck.rbox(t, x1 - cabl - .2, x1 - .02, skirt, H - .42, z0 + .04, z1 - .04, .22, body, seg)
        # the windscreen and the side glass
        t.face(((x1 - .9, H - .5, z0 + .16), (x1 - .9, H - .5, z1 - .16), (x1 - .03, H - 1.25, z1 - .16), (x1 - .03, H - 1.25, z0 + .16)), F['glass'], (1, .9, 0)) if False else None
        box(t, x1 - .06, x1 + .0, H - 1.42, H - .62, z0 + .2, z1 - .2, F['glass'], None, 0, cell=0)
        for sz in (-1, 1):
            zz = sz * (W / 2 - .04)
            box(t, x1 - cabl + .05, x1 - .3, H - 1.42, H - .7, min(zz, zz + sz * .012), max(zz, zz + sz * .012), F['glass'], None, 0, cell=0)
            if T.i < 2:
                # the side door's seams and a stripe
                box(t, x0 + 1.2, x0 + 1.22, skirt + .1, H - .2, min(zz + sz * .04, zz + sz * .046), max(zz + sz * .04, zz + sz * .046), F['void'], None, 0, cell=0)
                box(t, x0 + 2.6, x0 + 2.62, skirt + .1, H - .2, min(zz + sz * .04, zz + sz * .046), max(zz + sz * .04, zz + sz * .046), F['void'], None, 0, cell=0)
                box(t, x0 + .2, x1 - cabl - .1, skirt + .55, skirt + .75, min(zz + sz * .04, zz + sz * .047), max(zz + sz * .04, zz + sz * .047), glow(pick(ck.NEONS, b.id, 'stripe'), .9), None, 0, cell=0)
        if T.i < 2:
            # roof: a vent and rails
            box(t, x0 + 1.0, x0 + 2.0, H, H + .0 + .001, -.4, .4, F['cabdark'], None, 0, cell=0)
            for sz in (-1, 1):
                rod(t, (x0 + .4, H - .02, sz * (W / 2 - .2)), (x1 - cabl - .2, H - .02, sz * (W / 2 - .2)), .02, F['steel'], 4, caps=False)
    else:
        ck.rbox(t, x0 + .02, x1 - .02, skirt, H - .52, z0 + .0, z1 - .0, .2, body, seg)
        ck.rbox(t, x0 + .9, x1 - 1.1, H - .62, H - .0, z0 + .14, z1 - .14, .2, F['cabdark'], seg)
        for sz in (-1, 1):
            zz = sz * (W / 2 - .14)
            box(t, x0 + 1.05, x1 - 1.25, H - .5, H - .1, min(zz, zz + sz * .012), max(zz, zz + sz * .012), F['glass'], None, 0, cell=0)
        box(t, x1 - 1.1, x1 - 1.09 + .012, H - .5, H - .1, z0 + .22, z1 - .22, F['glass'], None, 0, cell=0)
        box(t, x0 + .9 - .012, x0 + .9, H - .5, H - .1, z0 + .22, z1 - .22, F['glass'], None, 0, cell=0)
        if T.i < 2:
            # the chequer band
            n = 10
            for k in range(n):
                for sz in (-1, 1):
                    zz = sz * (W / 2 + .001)
                    xa = x0 + .5 + k * (L - 1.0) / n
                    box(t, xa, xa + (L - 1.0) / n / 2, skirt + .32, skirt + .44, min(zz, zz + sz * .006), max(zz, zz + sz * .006), F['cabdark'], None, 0, cell=0)
    # the lights from the kit's parts (head: ice, tail: red), the under-glow, the cab's roof light
    q = (0, 0, 0, 1) if alongx else sk.quat_axis(0, 1, 0, -math.pi / 2)
    g.extend(t.transformed(sk.affine(q, (b.c[0], b.y0, b.c[2]))))
    for p in S.parts(b):
        c, h = p['c'], p['h']
        if p['look'] == 'cyber-glass':
            continue
        if p['look'] == 'cyber-tube' and h[1] > .05 and h[1] < .08 and c[1] > b.y0 + .3 and min(h[0], h[2]) < .04:
            # a light bar across an end: a slim lens in a dark housing
            box(g, c[0] - h[0], c[0] + h[0], c[1] - h[1] * .55, c[1] + h[1] * .55, c[2] - h[2], c[2] + h[2], glow(p['col'], p['gain']), None, 0, cell=0)
            continue
        if p['look'] == 'cyber-tube' and h[1] > .08 and c[1] > b.y1:
            # the roof light: TAXI's lit box
            ck.rbox(g, c[0] - h[0], c[0] + h[0], b.y1 - .01, b.y1 + 2 * h[1], c[2] - h[2], c[2] + h[2], .05, glow(p['col'], p['gain']), 1)
            continue
        piece(g, S, b, p, T)


def kiosk(g, S, b, T):
    """A kiosk: a steel cabin with a serving window under a roller blind box, a screen, a lit roof and the base its 24H sign
    stands on."""
    sg = -1 if b.c[2] > 0 else 1
    fr = b.face(2, sg)
    half, tall = fr.half, b.s[1]
    ck.rbox(g, b.x0, b.x1, b.y0 - .02, b.y1 - .12, b.z0, b.z1, .1, F['clad0'], 2 if T.i == 0 else 1)
    box(g, b.x0 - .1, b.x1 + .1, b.y1 - .12, b.y1, b.z0 - .1, b.z1 + .1, F['metaldark'], T, .03, cell=0)
    if T.i < 2:
        fr.box(g, -half + .15, half - .15, tall - .5, tall - .16, 0, .12, F['gunmetal'], T, .03)
        fr.box(g, -half + .1, half - .1, .86, .93, 0, .2, F['steel'], T, .012)
    fr.box(g, -1.0, 1.0, tall, tall + .72, -.1, -.012, F['void'], T, .015, skip=('-y',))
    for p in S.parts(b):
        c, h = p['c'], p['h']
        if p['look'] == 'cyber-tube' and h[1] < .05:
            box(g, c[0] - h[0] + .15, c[0] + h[0] - .15, b.y1, b.y1 + .04, c[2] - h[2] + .15, c[2] + h[2] - .15, glow(p['col'], p['gain']), None, 0, cell=0, skip=('-y',))
        else:
            piece(g, S, b, p, T)


def pillar(g, S, b, T):
    """An advertising pillar: a rounded steel body with a screen on two faces, a lit cap."""
    ck.rbox(g, b.x0 + .02, b.x1 - .02, b.y0 - .02, b.y1, b.z0, b.z1, .1, F['metaldark'], 2 if T.i == 0 else 1)
    box(g, b.x0 - .04, b.x1 + .04, b.y0, b.y0 + .25, b.z0 - .04, b.z1 + .04, F['gunmetal'], T, .03, cell=0, skip=('-y',))
    for p in S.parts(b):
        c, h = p['c'], p['h']
        if p['look'] == 'cyber-tube' and h[1] < .05:
            box(g, c[0] - h[0] + .08, c[0] + h[0] - .08, b.y1, b.y1 + .05, c[2] - h[2] + .08, c[2] + h[2] - .08, glow(p['col'], p['gain']), None, 0, cell=0, skip=('-y',))
        else:
            piece(g, S, b, p, T)


def lamp(g, S, b, T):
    """A street lamp: a tapered pole on a base, a curved arm and a slim head with the kit's light bar under it."""
    cx, cz = b.c[0], b.c[2]
    sides = (8, 6, 4)[T.i]
    lathe(g, [(.13, 0), (.13, .5), (.09, .7), (.06, b.s[1] - .2)], (cx, b.y0, cz), F['gunmetal'], sides)
    head = None
    for p in S.parts(b):
        if p['look'] == 'cyber-tube':
            head = p
    if head is None:
        return
    c, h = head['c'], head['h']
    sz = 1 if c[2] > cz else -1
    y = b.y1
    pts = [(cx, y - .25, cz), (cx, y - .05, cz + sz * .12), (cx, y + .02, cz + sz * .45), (cx, c[1] + h[1] + .04, c[2] - sz * h[2])]
    sweep(g, sk.circle_section(.05, (6, 4, 4)[T.i]), sk.catmull(pts, (4, 3, 2)[T.i]), F['gunmetal'], caps=True)
    box(g, c[0] - h[0] - .03, c[0] + h[0] + .03, c[1] + h[1], c[1] + h[1] + .07, c[2] - h[2] - .03, c[2] + h[2] + .03, F['metaldark'], T, .02, cell=0)
    box(g, c[0] - h[0], c[0] + h[0], c[1] - h[1], c[1] + h[1], c[2] - h[2], c[2] + h[2], glow(head['col'], head['gain']), None, 0, cell=0, skip=('+y',))


# ---------------------------------------------------------------------------------------------------------------------------
# The yards
# ---------------------------------------------------------------------------------------------------------------------------
def yard(g, S, b, T):
    s = b.stem
    if '-cab-' in s:
        vehicle(g, S, b, T, 'cab')
    elif s.endswith('-canopy'):
        canopy(g, S, b, T)
    elif '-post-' in s:
        post(g, S, b, T, False)
    elif '-pylon-' in s:
        charger(g, S, b, T)
    elif s.endswith('-barrier'):
        barrier(g, b, T)
    elif s.endswith('-wall'):
        market_wall(g, S, b, T, True)
    else:
        fallback(g, S, b, T)


def canopy(g, S, b, T):
    """A yard's canopy: a steel roof with a fascia, purlins under it carrying the kit's under-lights, the yard's name in neon
    on a board over its front edge."""
    slab(g, b, F['gunmetal'], F['metaldark'], F['metaldark'], 0)
    e = .12
    box(g, b.x0 - e, b.x1 + e, b.y1 - .1, b.y1 + .04, b.z0 - e, b.z1 + e, F['gunmetal'], T, .03, cell=0, skip=('-y',))
    sg = -1 if b.c[2] > 0 else 1
    fr = b.face(2, sg)
    word = 'TAXI' if b.c[2] > 0 else 'DOCK'
    wl = (len(word) * 6 - 1) * .1
    ck.sign_board(g, fr.shifted(dd=-.04 + e), -wl / 2 - .2, wl / 2 + .2, b.s[1] + .0, b.s[1] + .86, T, d=.03, f=F['void'])
    ck.neon_text(g, fr.shifted(dd=e), word, 0, b.s[1] + .04, .1, 0xFFE14D, 2.4, T, d=.05)
    for p in S.parts(b):
        c, h = p['c'], p['h']
        if p['look'] != 'cyber-tube':
            piece(g, S, b, p, T)
        elif c[1] < b.y0:
            if T.i < 2:
                box(g, c[0] - .04, c[0] + .04, b.y0 - .1, b.y0, c[2] - h[2], c[2] + h[2], F['gunmetal'], None, 0, cell=0)
            yb = b.y0 - (.104 if T.i < 2 else .004)
            g.face(((c[0] - h[0], yb, c[2] - h[2]), (c[0] + h[0], yb, c[2] - h[2]), (c[0] + h[0], yb, c[2] + h[2]), (c[0] - h[0], yb, c[2] + h[2])), glow(p['col'], p['gain']), (0, -1, 0))


def charger(g, S, b, T):
    """A charging pylon: a rounded cabinet with lit panels on two faces, a cable and its plug, a lit cap."""
    ck.rbox(g, b.x0 + .03, b.x1 - .03, b.y0 - .02, b.y1, b.z0 + .03, b.z1 - .03, .1, F['clad5'], 2 if T.i == 0 else 1)
    box(g, b.x0, b.x1, b.y0, b.y0 + .14, b.z0, b.z1, F['gunmetal'], T, .03, cell=0, skip=('-y',))
    if T.i == 0:
        fr = b.face(2, -1 if b.c[2] > 0 else 1)
        a, e = fr.at(.2, 1.3, 0), fr.at(-.15, .5, 0)
        ck.cables(g, sk._add(a, fr.n, .04), sk._add(e, fr.n, .06), .3, .02, F['cable'], T)
        fr.box(g, -.22, -.08, .42, .62, 0, .09, F['metaldark'], T, .015)
    for p in S.parts(b):
        c, h = p['c'], p['h']
        if p['look'] == 'cyber-tube' and h[1] < .05:
            box(g, c[0] - h[0] + .1, c[0] + h[0] - .1, b.y1, b.y1 + .05, c[2] - h[2] + .1, c[2] + h[2] - .1, glow(p['col'], p['gain']), None, 0, cell=0, skip=('-y',))
        else:
            pf = part_frame(b, p)
            if pf and p['look'] == 'cyber-tube':
                fr, d = pf
                fr.box(g, -fr.half, fr.half, 0, fr.tall, 0, d * .6, F['metaldark'], T, .008)
                fr.quad(g, -fr.half + .04, fr.half - .04, .04, fr.tall - .04, d, glow(p['col'], p['gain']), 0)
                if T.i < 2:
                    for k in range(4):
                        fr.quad(g, -fr.half + .1, -fr.half + .1 + (2 * fr.half - .2) * (k + 1) / 5, .1 + k * .12, .16 + k * .12, d + .006, glow(0xF0FFF0, 2.2), 0)
            else:
                piece(g, S, b, p, T)


def barrier(g, b, T):
    """A concrete barrier with a splayed foot and a striped top band."""
    alongx = b.s[0] >= b.s[2]
    sec = [(-.5, 0), (.5, 0), (.5, .16), (.24, .42), (.2, 1.0), (-.2, 1.0), (-.24, .42), (-.5, .16)]
    W = b.s[2] if alongx else b.s[0]
    sec = [(u * W, v * b.s[1]) for (u, v) in sec]
    if alongx:
        ck.extrude(g, sec, (b.x0, b.y0, b.c[2]), (0, 0, 1), (0, 1, 0), (b.s[0], 0, 0), F['concrete'])
    else:
        ck.extrude(g, sec, (b.c[0], b.y0, b.z0), (1, 0, 0), (0, 1, 0), (0, 0, b.s[2]), F['concrete'])
    for sg in (-1, 1):
        fr = b.face(2 if alongx else 0, sg)
        o = fr.at(-fr.half + .05, b.s[1] * .62, -W * .3 + .004)
        ck.stripes(g, o, (fr.ud[0] * (2 * fr.half - .1), 0, fr.ud[2] * (2 * fr.half - .1)), (0, b.s[1] * .3, 0), fr.n, .25, F['hazardy'], F['hazardk'])


# ---------------------------------------------------------------------------------------------------------------------------
# The promenade
# ---------------------------------------------------------------------------------------------------------------------------
def promenade(g, S, b, T):
    s = b.stem
    if 'planter' in s:
        planter(g, S, b, T)
    elif '-pillar-' in s:
        pillar(g, S, b, T)
    elif '-bench-' in s:
        bench(g, b, T)
    elif '-shelter-' in s:
        shelter(g, S, b, T)
    else:
        fallback(g, S, b, T)


def planter(g, S, b, T):
    """A planter: a concrete trough with a rim, soil, clipped shrubs where the kit grows them, its LED lines."""
    box(g, b.x0, b.x1, b.y0, b.y1 - .1, b.z0, b.z1, F['concrete'], T, .03, cell=0, skip=('-y', '+y'))
    box(g, b.x0 - .03, b.x1 + .03, b.y1 - .1, b.y1, b.z0 - .03, b.z1 + .03, F['concretedark'], T, .025, cell=0)
    top(g, b.x0 + .12, b.x1 - .12, b.z0 + .12, b.z1 - .12, b.y1 + .003, F['soil'], 0)
    for i, p in enumerate(S.parts(b)):
        c, h = p['c'], p['h']
        if p['look'] == 'plain':
            # a clipped shrub: low leafy mounds (no higher than a pilot's ankle: nothing to walk through)
            n = 3 if T.i < 2 else 1
            for k in range(n):
                u = (k - (n - 1) / 2) * h[0] * .7
                r = min(h[0], h[2]) * (.75 + .3 * h01(b.id, 'shrub', i, k))
                ck.dome(g, (c[0] + u, b.y1, c[2] + (h01(b.id, 'sz', i, k) - .5) * h[2] * .5), r, F['leaf'] if (i + k) % 2 else F['leaflight'], (8, 6, 5)[T.i], (3, 2, 2)[T.i], squash=.24 / r)
        else:
            piece(g, S, b, p, T)


def shelter(g, S, b, T):
    """A waiting pod: smoked glass in a steel frame on a kerb, a roof with a lit edge, the kit's poster screen on its back."""
    box(g, b.x0, b.x1, b.y0, b.y0 + .14, b.z0, b.z1, F['gunmetal'], T, .03, cell=0, skip=('-y',))
    box(g, b.x0 + .04, b.x1 - .04, b.y0 + .14, b.y1 - .1, b.z0 + .04, b.z1 - .04, F['glassblue'], None, 0, cell=0, skip=('-y', '+y'))
    for (sx, sz) in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        x, z = b.c[0] + sx * (b.s[0] / 2 - .04), b.c[2] + sz * (b.s[2] / 2 - .04)
        box(g, x - .04, x + .04, b.y0 + .14, b.y1 - .1, z - .04, z + .04, F['steel'], T, .012, cell=0, skip=('-y', '+y'))
    if T.i < 2:
        for k in range(1, 3):
            x = b.x0 + k * b.s[0] / 3
            for sz in (-1, 1):
                z = b.c[2] + sz * (b.s[2] / 2 - .03)
                box(g, x - .025, x + .025, b.y0 + .14, b.y1 - .1, z - .03, z + .03, F['steel'], None, 0, cell=0)
    for p in S.parts(b):
        c, h = p['c'], p['h']
        if p['look'] == 'cyber-metal' and h[1] < .1:
            box(g, c[0] - h[0], c[0] + h[0], b.y1 - .1, c[1] + h[1], c[2] - h[2], c[2] + h[2], F['gunmetal'], T, .03, cell=0)
        else:
            piece(g, S, b, p, T)


# ---------------------------------------------------------------------------------------------------------------------------
# The backdrop's near ring and the tunnels
# ---------------------------------------------------------------------------------------------------------------------------
FAR_NAMES = ('n', 's', 'e', 'w', 'tunnel-e', 'tunnel-w')


def backdrop(g, S, T):
    pass


def far_side(c):
    if c[1] < -.3 and abs(c[2]) < 5:
        return 4 if c[0] > 0 else 5
    if abs(c[2]) > 32.4:
        return 0 if c[2] > 0 else 1
    return 2 if c[0] > 0 else 3


def far_ids(far):
    P, N, S_, F_ = far.arrays()
    starts = np.zeros(len(S_), dtype=np.int64)
    if len(S_) > 1:
        starts[1:] = np.cumsum(S_[:-1])
    cen = np.add.reduceat(P.astype(np.float64), starts, axis=0) / S_[:, None]
    ids = np.where(np.abs(cen[:, 2]) > 32.4, np.where(cen[:, 2] > 0, 0, 1), np.where(cen[:, 0] > 0, 2, 3))
    tun = (cen[:, 1] < -.2) & (np.abs(cen[:, 2]) < 4.6) & (np.abs(cen[:, 0]) > 39.9)
    ids = np.where(tun, np.where(cen[:, 0] > 0, 4, 5), ids)
    return ids


def far_scene(S, T):
    """The near ring (the blocks right behind the frontage, seen over it) and the Helix Line's tunnels, from the backdrop's own
    boxes: each block a building with window bands of lit panes, a parapet, its neon edge, screens' frames (their pictures are
    the atmosphere's), roof plant, tanks and masts; each tunnel a ribbed tube with its lights."""
    g = sk.Geo()
    near = [bx for bx in S.L['backdrop'] if bx.get('near')]
    for i, bx in enumerate(near):
        c, h, look = bx['c'], bx['h'], bx['look']
        x0, x1, y0, y1, z0, z1 = c[0] - h[0], c[0] + h[0], c[1] - h[1], c[1] + h[1], c[2] - h[2], c[2] + h[2]
        tunnel = c[1] < -.3 and h[2] < 5
        if look == 'cyber-tube':
            box(g, x0, x1, y0, y1, z0, z1, glow(bx['col'], bx['gain']), None, 0, cell=0)
        elif tunnel:
            f = F['metal'] if look == 'cyber-metal' else F['concretedark']
            box(g, x0, x1, y0, y1, z0, z1, f, None, 0, cell=8.0)
            if look != 'cyber-metal' and h[1] > 1 and T.i < 2:
                # ribs along a tunnel wall
                sgn = -1 if c[2] > 0 else 1
                x = x0 + 2
                while x < x1:
                    zf = c[2] + sgn * h[2]
                    box(g, x - .15, x + .15, y0, y1, min(zf, zf + sgn * .12), max(zf, zf + sgn * .12), F['cut'], None, 0, cell=0)
                    x += 4
        elif look == 'cyber-panel' and h[1] > 5:
            building(g, bx, T, i)
        elif look == 'cyber-panel':
            box(g, x0, x1, y0, y1, z0, z1, F['clad5'], None, 0, cell=0, skip=('-y',))
        elif look == 'cyber-glass':
            fr = far_frame(bx)
            n = max(2, int(fr.half * 2 / 1.3))
            w = 2 * fr.half / n
            for k in range(n):
                r = h01('nw', i, k)
                f = glow(pick(LIT, 'nl', i, k), .8 + .5 * h01('ng', i, k)) if r < .4 else F['glasstower']
                fr.quad(g, -fr.half + k * w + .04, -fr.half + (k + 1) * w - .04, 0, fr.tall, .02, f, 0)
        elif look == 'cyber-holo':
            fr = far_frame(bx)
            fr.box(g, -fr.half - .12, fr.half + .12, -.12, fr.tall + .12, -.1, .0, F['metaldark'], None, 0)
        elif look == 'cyber-metal':
            box(g, x0, x1, y0, y1, z0, z1, F['steel'] if i % 2 else F['gunmetal'], T, .05, cell=0, skip=('-y',))
        else:
            box(g, x0, x1, y0, y1, z0, z1, F['concretedark'], None, 0, cell=0)
    return g


def far_frame(bx):
    c, h = bx['c'], bx['h']
    a = 0 if h[0] <= h[2] else 2
    sgn = -1 if c[a] > 0 else 1
    o = [c[0], c[1] - h[1], c[2]]
    o[a] = c[a] + sgn * h[a]
    return Fr(tuple(o), (sgn, 0, 0) if a == 0 else (0, 0, sgn), h[2 - a], 2 * h[1])


def building(g, bx, T, i):
    """A near-ring block: its faces, a parapet and a setback crown, a tank or a mast on the roof."""
    c, h = bx['c'], bx['h']
    x0, x1, y0, y1, z0, z1 = c[0] - h[0], c[0] + h[0], c[1] - h[1], c[1] + h[1], c[2] - h[2], c[2] + h[2]
    f = F['clad%d' % int(h01('nb', i) * 6)]
    box(g, x0, x1, 14.0, y1, z0, z1, f, None, 0, cell=6.0, skip=('-y',))
    e = .25
    box(g, x0 - e, x1 + e, y1 - .5, y1 + .25, z0 - e, z1 + e, F['trim'], None, 0, cell=0)
    k = int(h01('nr', i) * 4)
    cx, cz = c[0] + (h01('nx', i) - .5) * h[0], c[2] + (h01('nz', i) - .5) * h[2]
    if k == 0:
        lathe(g, [(1.1, 0), (1.1, 2.2), (.7, 2.6), (1e-3, 2.7)], (cx, y1 + .9, cz), F['timbery'], (10, 8, 6)[T.i], cap0=True)
        for a in range(4):
            p = (cx + .8 * math.cos(a * math.pi / 2 + .78), y1 + .25, cz + .8 * math.sin(a * math.pi / 2 + .78))
            rod(g, p, sk._add(p, (0, .65, 0)), .06, F['steel'], 4, caps=False)
    elif k == 1:
        rod(g, (cx, y1 + .25, cz), (cx, y1 + 5.5, cz), .07, F['steel'], 4, caps=False)
        for vv in (2.5, 3.6, 4.6):
            rod(g, (cx - .8 + vv * .1, y1 + vv, cz), (cx + .8 - vv * .1, y1 + vv, cz), .03, F['steel'], 3, caps=False)
        ck.ball(g, (cx, y1 + 5.6, cz), .12, glow(ck.RED, 2.4), 5, 3)
    elif k == 2:
        # a stair head with a lit door
        box(g, cx - 1.2, cx + 1.2, y1 + .25, y1 + 2.6, cz - 1.0, cz + 1.0, F['clad5'], None, 0, cell=0, skip=('-y',))
        sg = -1 if cz > 0 else 1
        box(g, cx - .4, cx + .4, y1 + .3, y1 + 2.1, cz + sg * 1.0, cz + sg * 1.02, glow(0xFFC98A, .9), None, 0, cell=0)


def wet(g, bk):
    """The rain on the ground: patches of the street, the paving and the Cut's floor are wetter (smoother and darker), so the
    neon lies in them."""
    P, N, S_, F_ = g.arrays()
    fc = np.repeat(F_, S_)
    names = ('asphalt', 'paver', 'paverdark', 'cutfloor', 'concretewet')
    mask = np.isin(fc, [F[n] for n in names]) & (N[:, 1] > .7)
    if not mask.any():
        return
    W = P[mask].astype(np.float64)
    n = sk.fbm(W * np.array([1.0, 0.0, 1.0]), .22, 3, 41)
    w = sk.smooth(.42, .68, n)
    sm = bk.uv0[mask, 0]
    bk.uv0[mask, 0] = (sm + (.94 - sm) * w * .9).astype(np.float32)
    col = bk.colour[mask, :3].astype(np.float64)
    bk.colour[mask, :3] = np.round(col * (1 - .3 * w)[:, None]).astype(np.uint8)
