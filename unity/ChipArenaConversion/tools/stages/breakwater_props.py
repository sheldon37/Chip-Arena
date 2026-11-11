"""Breakwater's moving art and breakable props (breakwater_art.py runs it): the rig as props in their own frames, posed each
frame by Core NativeBreakwaterArt from Core NativeBreakwaterRig's parts, and the breakable blocks' models in their blocks' frames.

The rig frames (Unity space, metres; Core NativeBreakwaterArt holds the same):
  optic        the lighthouse's lens, origin at the lamp (the lantern's centre): the beams leave along +-z; it turns about y
  lantern      a post's lantern, origin at its middle (the glass +-.11 x +-.15); its rod runs up .42 to the bracket's ring
  bell         the turret's storm bell, origin at its middle; it hangs .45 under the beam and swings about x
  swell        a swell of unit half extents (x +-1 along the crest, y -1 the sea .. +1 the crest, z +-1): the pose scales it;
               it runs toward -z
  comber       the sea that comes over the wall, of unit half extents like the swell: steeper, its lip curling, foam down it
  wash         the water on a floor, of unit half extents (a sheet: x +-1 across, y +-1 its thickness, z +-1 along its run)
  wash-fall    the water falling down a face, of unit half extents (x +-1 across, y +-1 its height, z +-1 its thickness)
  buoy         the bell buoy, origin on the waterline at its axis; buoy-lamp: its lamp (the pose tints the glow by the flash)
  boat         the fishing boat, origin on the waterline amidships, bow toward +z (8.4 m); dinghy: the same, 3 m
  gull-body    a gull, origin at its shoulders, flying toward +z; gull-wing-l / -r: a wing from its shoulder joint out along
               -x / +x (.6 m), flat at rest (the pose lifts it about z)
  flag         one length of a flag's cloth, origin at its middle: z along it (.62), y its height (.46), both faces
"""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, lathe, sweep
import breakwater_kit as bk
from breakwater_kit import F, Y


def optic(T):
    g = Geo()
    s = (14, 9, 6)[T.i]
    # the lamp at its heart, the two bull's-eye lenses the beams leave by, prisms round them, the brass cage
    lathe(g, [(.0, -.5), (.2, -.46), (.24, 0), (.2, .46), (.0, .5)], (0, 0, 0), F['lens'], s)
    for sz in (-1, 1):
        prof = [(.0, .9), (.2, .88), (.34, .82), (.46, .78), (.6, .74)]
        lathe(g, prof, (0, 0, 0), F['lens'], s, axis=(0, 0, sz))
        if T.i < 2:
            for r in (.3, .46, .6):
                path = [(math.cos(2 * math.pi * k / s) * r, math.sin(2 * math.pi * k / s) * r, sz * (.92 - r * .28)) for k in range(s)]
                sk.tube(g, path, .022, F['brass'], 4, closed=True)
    for y in (-.72, .72):
        lathe(g, [(.0, -.06), (.74, -.06), (.78, 0), (.74, .06), (.0, .06)], (0, y, 0), F['brass'], s)
    for k in range(4 if T.i < 2 else 0):
        a = math.pi / 4 + k * math.pi / 2
        sk.cylinder(g, (math.cos(a) * .7, -.7, math.sin(a) * .7), (math.cos(a) * .7, .7, math.sin(a) * .7), .03, F['brass'], 5)
    return g


def lantern(T, drop):
    g = Geo()
    s = (8, 6, 4)[T.i]
    lathe(g, [(.03, -.15), (.1, -.13), (.115, 0), (.1, .13), (.05, .15)], (0, 0, 0), F['lamp'], s, cap0=True)
    lathe(g, [(.13, -.19), (.13, -.15), (.04, -.14)], (0, 0, 0), F['ironplain'], s, cap0=True)
    lathe(g, [(.16, .14), (.15, .17), (.07, .24), (.03, .27), (.02, drop - .03)], (0, 0, 0), F['ironplain'], s, cap1=True)
    if T.i < 2:
        for k in range(4):
            a = math.pi / 4 + k * math.pi / 2
            sk.cylinder(g, (math.cos(a) * .125, -.15, math.sin(a) * .125), (math.cos(a) * .125, .15, math.sin(a) * .125), .008, F['ironplain'], 3)
        path = [(math.cos(2 * math.pi * k / 8) * .035, drop + math.sin(2 * math.pi * k / 8) * .035, 0) for k in range(8)]
        sk.tube(g, path, .008, F['ironplain'], 3, closed=True)
    return g


def bell(T, drop):
    g = Geo()
    s = (16, 10, 6)[T.i]
    prof = [(.3, -.36), (.34, -.34), (.3, -.28), (.24, -.12), (.2, .06), (.19, .2), (.15, .3), (.06, .35), (.0, .36)]
    lathe(g, prof, (0, 0, 0), F['bronze'], s)
    if T.i < 2:
        lathe(g, [(.28, -.36), (.2, -.1), (.0, .3)], (0, 0, 0), F['deep'], max(6, s // 2))        # the mouth's dark inside
        lathe(g, [(.0, -.42), (.06, -.4), (.07, -.34), (.02, -.3), (.015, .1)], (0, 0, 0), F['ironplain'], 5)   # the clapper
    # the stock it hangs by, up to the beam
    box(g, -.3, .3, .36, drop - .02, -.07, .07, F['timber'], None, bevel=.02 if T.i == 0 else 0)
    for x in (-.24, .24):
        box(g, x - .03, x + .03, .3, drop + .02, -.085, .085, F['ironplain'], None, bevel=0)
    return g


def swell(T, comber=False):
    g = Geo()
    bk.wave(g, 1.0, 2.0, 1.0, T, F['sea'], F['foamlit'], curl=.55 if comber else .12, foam=.5 if comber else .2, ends=.45 if comber else .35)
    out = g.transformed([1, 0, 0, 0, 0, 1, 0, -1.0, 0, 0, 1, 0])
    if comber and T.i < 2:
        # foam streaks down its face
        for k in range((9, 5)[T.i]):
            x = -.8 + 1.6 * (k + .5) / (9, 5)[T.i]
            out.face(((x - .03, .7, -.5), (x + .03, .7, -.5), (x + .05, -.6, -.92), (x - .05, -.6, -.92)), F['foamlit'], (0, .5, -1))
    return out


def wash(T, fall=False):
    """A sheet of water of unit half extents: streaked with foam along its run (z; down its height for the fall)."""
    g = Geo()
    n = (10, 6, 3)[T.i]
    m = (8, 5, 3)[T.i]
    for i in range(n):
        for j in range(m):
            u0, u1 = -1 + 2 * i / n, -1 + 2 * (i + 1) / n
            v0, v1 = -1 + 2 * j / m, -1 + 2 * (j + 1) / m
            f = F['foamlit'] if (bk.hashf(5, i, j // 2) < .5 or j == 0) else F['sealight']
            if fall:
                g.face(((u0, v0, -1), (u1, v0, -1), (u1, v1, -1), (u0, v1, -1)), f, (0, 0, -1))
            else:
                h = lambda u, v: 1.0 + (.0 if abs(u) > .99 else .5 * math.sin(u * 7 + v * 3) * (1 if T.i < 2 else 0)) * .0
                g.face(((u0, h(u0, v0), v0), (u1, h(u1, v0), v0), (u1, h(u1, v1), v1), (u0, h(u0, v1), v1)), f, Y)
    if fall:
        g.face(((-1, 1, -1), (1, 1, -1), (1, 1, 1), (-1, 1, 1)), F['foamlit'], Y)
        for sx in (-1, 1):
            g.face(((sx, -1, -1), (sx, 1, -1), (sx, 1, 1), (sx, -1, 1)), F['sealight'], (sx, 0, 0))
    else:
        # the front's foaming roll (toward -z) and the sides
        g.face(((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1)), F['foamlit'], (0, 0, -1))
        for sx in (-1, 1):
            g.face(((sx, -1, -1), (sx, 1, -1), (sx, 1, 1), (sx, -1, 1)), F['sealight'], (sx, 0, 0))
    return g


def buoy(T):
    g = Geo()
    s = (14, 9, 6)[T.i]
    lathe(g, [(.0, -.75), (.5, -.7), (.86, -.4), (.92, -.1)], (0, 0, 0), F['hullred'], s)
    lathe(g, [(.92, -.1), (.92, .12)], (0, 0, 0), F['hullwhite'], s)
    lathe(g, [(.92, .12), (.88, .3), (.6, .42), (.0, .46)], (0, 0, 0), F['red'], s)
    # the lattice: three legs to a ring, the bell hung in it, the lamp's cage on top
    for k in range(3):
        a = k * 2 * math.pi / 3 + .3
        sk.cylinder(g, (math.cos(a) * .7, .36, math.sin(a) * .7), (math.cos(a) * .22, 2.45, math.sin(a) * .22), .04, F['ironplain'], (6, 5, 4)[T.i])
        if T.i < 2:
            a2 = a + 2 * math.pi / 3
            sk.cylinder(g, (math.cos(a) * .52, 1.2, math.sin(a) * .52), (math.cos(a2) * .52, 1.2, math.sin(a2) * .52), .025, F['ironplain'], 4)
    lathe(g, [(.26, 2.42), (.26, 2.5), (.1, 2.56), (.1, 2.62)], (0, 0, 0), F['ironplain'], (8, 6, 4)[T.i], cap1=True)
    lathe(g, [(.2, 1.38), (.23, 1.4), (.16, 1.56), (.13, 1.74), (.05, 1.84), (.0, 1.86)], (0, 0, 0), F['bronze'], (10, 7, 5)[T.i])
    if T.i < 2:
        sk.cylinder(g, (0, 1.86, 0), (0, 2.44, 0), .015, F['ironplain'], 4)
    return g


def buoy_lamp(T):
    g = Geo()
    lathe(g, [(.0, -.12), (.09, -.08), (.1, 0), (.09, .08), (.0, .12)], (0, 0, 0), F['redlamp'], (8, 6, 4)[T.i])
    return g


def boat(T):
    g = Geo()
    P = bk.hull(g, 8.4, 3.0, 1.45, T, F['hull'], f_bottom=F['hullred'], sheer=.35, stern=.8, f_in=F['plank'], wale=F['hullwhite'], y0=-.62)
    # the deck, the wheelhouse aft with its lit windows, the mast and its boom, a net heaped forward, fenders over the side
    g.face(((-1.2, .5, -3.6), (1.2, .5, -3.6), (1.32, .5, 0), (.9, .5, 2.9), (-.9, .5, 2.9), (-1.32, .5, 0)), F['plank'], Y)
    box(g, -.85, .85, .5, 2.2, -2.5, -.7, F['hullwhite'], T, bevel=.04, skip=('-y',))
    box(g, -.98, .98, 2.2, 2.32, -2.64, -.5, F['hull'], T, bevel=.03)
    for z0, z1, x, n in ((-2.2, -1.0, .862, (1, 0, 0)), (-2.2, -1.0, -.862, (-1, 0, 0))):
        g.face(((x, 1.35, z0), (x, 1.35, z1), (x, 1.95, z1), (x, 1.95, z0)), F['window'], n)
    g.face(((-.6, 1.35, -.688), (.6, 1.35, -.688), (.6, 1.95, -.688), (-.6, 1.95, -.688)), F['window'], (0, 0, 1))
    sk.cylinder(g, (0, .5, .4), (0, 5.2, .4), .07, F['timber'], (7, 5, 4)[T.i])
    sk.cylinder(g, (0, 1.5, .4), (0, 2.3, 3.0), .045, F['timber'], (6, 5, 4)[T.i])
    if T.i < 2:
        sk.cylinder(g, (0, 5.1, .4), (0, 2.3, 3.0), .012, F['rope'], 3)
        sk.cylinder(g, (0, 5.1, .4), (0, 2.34, -1.6), .012, F['rope'], 3)
        bk.rock(g, (0, .72, 2.0), (.75, .26, .6), 77, F['net'], n=2, square=.2, rough=.25, flat=False)
        for sx in (-1, 1):
            for z in (-1.5, 1.2):
                lathe(g, [(.0, -.3), (.13, -.2), (.13, .2), (.0, .3)], (sx * 1.42, .25, z), F['hullwhite' if sx > 0 else 'red'], 6)
        lathe(g, [(.0, 0), (.06, .02), (.07, .1), (.0, .16)], (0, 2.32, -1.6), F['lamp'], 6)
    return g


def dinghy(T):
    g = Geo()
    bk.hull(g, 3.0, 1.24, .52, T, F['hullwhite'], f_bottom=F['hull'], sheer=.1, stern=.8, f_in=F['plank'], wale=F['timber'], y0=-.16)
    for z in (-.7, .35):
        box(g, -.5, .5, .2, .24, z - .12, z + .12, F['plank'], None, bevel=.01 if T.i == 0 else 0)
    if T.i < 2:
        sk.cylinder(g, (-.3, .28, -1.2), (-.36, .3, 1.0), .022, F['plank'], 4)
        sk.cylinder(g, (.3, .28, -1.2), (.36, .3, 1.0), .022, F['plank'], 4)
    return g


def gull_body(T):
    g = Geo()
    m, n = (9, 6, 4)[T.i], (8, 6, 4)[T.i]
    # a lofted body: tail (-z) to beak (+z)
    zs = np.linspace(-.3, .22, m + 1)
    P = np.zeros((m + 1, n, 3))
    for i, z in enumerate(zs):
        t = (z + .3) / .52
        r = .075 * math.sin(math.pi * min(1, t * 1.15)) ** .6 * (1 if t < .75 else 1 - (t - .75) * 1.6) + .004
        for j in range(n):
            a = 2 * math.pi * j / n
            P[i, j] = (math.cos(a) * r, math.sin(a) * r * .85 + (.02 if t > .75 else 0), z)
    bk.grid_surface(g, P, F['gull'], closed_v=True, fin_fn=lambda i, j: F['gullgrey'] if (j < n // 2 and i < m * .7) else F['gull'])
    bk.facet(g, ((-.07, 0, -.28), (.07, 0, -.28), (0, .0, -.42)), F['gull'], Y)
    bk.facet(g, ((-.07, 0, -.28), (0, .0, -.42), (.07, 0, -.28)), F['gull'], (0, -1, 0))
    lathe(g, [(.014, 0), (.0, .07)], (0, .02, .22), F['beak'], 4, axis=(0, 0, 1))
    return g


def gull_wing(T, sx):
    g = Geo()
    # root at the origin, out along sx * x: a tapered blade, grey above, white below, a dark tip
    pts = [(0, -.08, .1), (.3, -.1, .09), (.5, -.06, .06), (.6, .0, .0)] if T.i < 2 else [(0, -.08, .1), (.6, .0, .0)]
    for k in range(len(pts) - 1):
        (xa, za0, za1), (xb, zb0, zb1) = pts[k], pts[k + 1]
        up = .04 * math.sin(math.pi * (xa + xb) / 1.2)
        a, b, c, d = (sx * xa, .0, za0), (sx * xb, up * 0, zb0), (sx * xb, up * 0, zb1), (sx * xa, .0, za1)
        f = F['gulldark'] if k == len(pts) - 2 and T.i < 2 else F['gullgrey']
        g.face((a, b, c, d), f, Y)
        g.face((a, b, c, d), F['gull'] if f != F['gulldark'] else f, (0, -1, 0))
    return g


def flag(T, length, height):
    g = Geo()
    hl, hh = length / 2, height / 2
    for sx in (-1, 1):
        x = sx * .006
        g.face(((x, -hh, -hl), (x, -hh, hl), (x, hh, hl), (x, hh, -hl)), F['cloth'], (sx, 0, 0))
        g.face(((x * 1.6, -hh * .3, -hl), (x * 1.6, -hh * .3, hl), (x * 1.6, hh * .3, hl), (x * 1.6, hh * .3, -hl)), F['clothwhite'], (sx, 0, 0))
    return g


def items(L, T):
    R = L['rig']
    return [('optic', optic(T), (31, 24.6, 0)), ('lantern', lantern(T, R['lanternDrop']), (0, 2.8, 0)), ('bell', bell(T, R['bellDrop']), (0, 7.4, 6.7)),
            ('swell', swell(T), (0, SEA + 1, 30)), ('comber', swell(T, True), (0, SEA + 1, 20)), ('wash', wash(T), (10, .1, 0)), ('wash-fall', wash(T, True), (10, 1.2, 4.9)),
            ('buoy', buoy(T), (R['buoyX'], SEA, R['buoyZ'])), ('buoy-lamp', buoy_lamp(T), (R['buoyX'], SEA + 2.75, R['buoyZ'])), ('boat', boat(T), (R['boatX'], SEA, R['boatZ'])),
            ('dinghy', dinghy(T), (R['dinghyX'], SEA, R['dinghyZ'])), ('gull-body', gull_body(T), (0, 20, 0)), ('gull-wing-l', gull_wing(T, -1), (0, 20, 0)), ('gull-wing-r', gull_wing(T, 1), (0, 20, 0)),
            ('flag', flag(T, R['flagLength'], R['flagHeight']), (0, 11, 0))]


SEA = -3.4


# ---------------------------------------------------------------------------------------------------------------------------
# The breakable props (block models, in their blocks' frames: the origin is the block's centre)
# ---------------------------------------------------------------------------------------------------------------------------
def pots(T, s):
    """Two lobster pots stacked: half-round wicker creels on slatted bases, a rope's end."""
    g = Geo()
    hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
    seg = (8, 5, 3)[T.i]
    for k in range(2):
        y0 = -hy + k * hy
        x0, x1 = -hx + .04 + k * .05, hx - .04 - k * .05
        box(g, x0, x1, y0, y0 + .07, -hz + .05, hz - .05, F['plank'], None, bevel=.012 if T.i == 0 else 0)
        # the creel's hoops and its netting (a half barrel along x)
        r = hy / 2 - .05
        for i in range(3 if T.i < 2 else 0):
            x = x0 + .06 + (x1 - x0 - .12) * i / 2
            path = [(x, y0 + .07 + math.sin(math.pi * j / seg) * r * 1.5, -math.cos(math.pi * j / seg) * (hz - .08)) for j in range(seg + 1)]
            sk.tube(g, path, .022, F['wicker'], 4)
        P = np.zeros((2, seg + 1, 3))
        for j in range(seg + 1):
            for i, x in enumerate((x0 + .05, x1 - .05)):
                P[i, j] = (x, y0 + .07 + math.sin(math.pi * j / seg) * r * 1.42, -math.cos(math.pi * j / seg) * (hz - .1))
        bk.grid_surface(g, P, F['net'], inside=(0, y0, 0))
        for x, n in ((x0 + .05, (-1, 0, 0)), (x1 - .05, (1, 0, 0))):
            g.face([(x, y0 + .07 + math.sin(math.pi * j / seg) * r * 1.42, -math.cos(math.pi * j / seg) * (hz - .1)) for j in range(seg + 1)], F['net'], n)
    return g


def barrel(T, s):
    g = Geo()
    hy, r = s[1] / 2, min(s[0], s[2]) / 2 - .02
    sd = (14, 9, 6)[T.i]
    prof = [(r * .82, -hy), (r * .95, -hy * .55), (r, 0), (r * .95, hy * .55), (r * .82, hy)]
    lathe(g, prof, (0, 0, 0), F['plank'], sd, cap0=False, cap1=False)
    lathe(g, [(.0, hy - .05), (r * .8, hy - .05), (r * .82, hy)], (0, 0, 0), F['timber'], sd)
    for y in (-hy * .72, -hy * .3, hy * .3, hy * .72):
        rr = r * (1 - .18 * (abs(y) / hy) ** 1.6) + .008
        lathe(g, [(rr, y - .03), (rr + .008, y), (rr, y + .03)], (0, 0, 0), F['ironplain'], sd)
    return g


def crate(T, s):
    """Two fish crates stacked, slatted, the top one full of the catch."""
    g = Geo()
    hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
    for k in range(2):
        y0, y1 = -hy + k * hy, -hy + (k + 1) * hy - .03
        o = .03 * k
        if T.i == 2:
            box(g, -hx + o, hx - o, y0, y1, -hz + o, hz - o, F['plank'], None, bevel=0)
            continue
        for sx in (-1, 1):
            for sz in (-1, 1):
                box(g, sx * (hx - o) - (.07 if sx > 0 else 0), sx * (hx - o) + (.07 if sx < 0 else 0), y0, y1, sz * (hz - o) - (.07 if sz > 0 else 0), sz * (hz - o) + (.07 if sz < 0 else 0), F['timber'], None, bevel=.01)
        rows = 3
        for r in range(rows):
            ya, yb = y0 + .03 + (y1 - y0 - .06) * r / rows + .012, y0 + .03 + (y1 - y0 - .06) * (r + 1) / rows - .012
            box(g, -hx + o + .02, hx - o - .02, ya, yb, -hz + o + .015, hz - o - .015, F['plank'], None, bevel=.008 if T.i == 0 else 0, skip=('-y', '+y'))
        box(g, -hx + o + .03, hx - o - .03, y0 + .02, y0 + .05, -hz + o + .03, hz - o - .03, F['plank'], None, bevel=0, skip=('-y',))
    # the catch: ice and fish in the top crate
    top = hy - .03
    g.face(((-hx + .1, top - .06, -hz + .1), (hx - .1, top - .06, -hz + .1), (hx - .1, top - .06, hz - .1), (-hx + .1, top - .06, hz - .1)), F['hullwhite'], Y)
    for i in range((7, 4, 0)[T.i]):
        cx, cz, a = (bk.hashf(9, i) - .5) * (hx * 1.3), (bk.hashf(9, i, 1) - .5) * (hz * 1.3), bk.hashf(9, i, 2) * 3.14
        d = (math.cos(a) * .2, math.sin(a) * .2)
        lathe(g, [(.0, -.2), (.05, -.08), (.055, .06), (.0, .2)], (cx, top - .03, cz), F['fish'], 5, axis=(d[0], 0, d[1]), scale=(1, .5))
    return g


def breakables(L, T):
    """(name, Geo, rest) for each kind of breakable, named by its first block (the others share its mesh)."""
    out = []
    seen = {}
    for b in L['blocks']:
        if b['role'] == 'static':
            continue
        kind = b['id'].split('-')[1]
        if kind in seen:
            continue
        seen[kind] = b
        g = {'pots': pots, 'barrel': barrel, 'crate': crate}[kind](T, b['size'])
        out.append((kind, g, tuple(b['pos'])))
    return out


def bank_props(sb, L, T, no_ao):
    ti = T.i
    its = items(L, T)
    brk = breakables(L, T)
    geos = [it[1] for it in its] + [it[1] for it in brk]
    aos = [None] * len(geos)
    if not no_ao:
        # each prop baked on its own (apart from the others); the breakables on a floor
        apart = [g.transformed([1, 0, 0, 40.0 * i, 0, 1, 0, 0, 0, 0, 1, 0]) for i, g in enumerate(geos)]
        floors = []
        for i, (kind, g, rest) in enumerate(brk):
            fl = Geo()
            hy = [b for b in L['blocks'] if b['id'].split('-')[1] == kind and b['role'] != 'static'][0]['size'][1] / 2
            x = 40.0 * (len(its) + i)
            fl.face(((x - 3, -hy, -3), (x + 3, -hy, -3), (x + 3, -hy, 3), (x - 3, -hy, 3)), F['sett'], Y)
            floors.append(fl)
        aos = sk.bake_ao(apart, distance=.5, samples=(24, 12, 8)[ti], occluders=floors)
    for (name, g, rest), ao in zip(its, aos[:len(its)]):
        if g.faces == 0:
            continue
        Pw = g.arrays()[0].astype(np.float64) + np.array(rest)
        bake = sk.Bake(g, ao, world=Pw, floors=(), seed=7)
        sb.add('rig/%s/%d' % (name, ti), 'rig', sk.to_mesh('rig/%s/%d' % (name, ti), g, bake))
    for (kind, g, rest), ao in zip(brk, aos[len(its):]):
        Pw = g.arrays()[0].astype(np.float64) + np.array(rest)
        bake = sk.Bake(g, ao, world=Pw, floors=(rest[1] - [b for b in L['blocks'] if b['id'].split('-')[1] == kind][0]['size'][1] / 2,), seed=9)
        om = sk.to_mesh('block/%s/%d' % (kind, ti), g, bake)
        first = True
        mi = None
        for b in L['blocks']:
            if b['role'] == 'static' or b['id'].split('-')[1] != kind:
                continue
            name = 'block/%s/%d' % (b['id'], ti)
            if first:
                sb.add(name, 'block', om)
                mi = len(sb.bank.meshes) - 1 if hasattr(sb.bank, 'meshes') else None
                first = False
            elif mi is not None:
                sb.share(name, 'block', mi)
            else:
                sb.add(name, 'block', om)
