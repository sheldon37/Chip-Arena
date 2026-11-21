"""Cyber City's art: the breakable and carried blocks' models and the maglev train (cybercity_art.py runs it).

Block models are in their block's own frame (its centre): the market stalls, the vending machines, the bins, the hover bikes,
and the billboards' struts, trusses, frames and screens. Each carries its block's steady kit parts (lanterns, screens, lights).

The train's three cars (rig/train-nose, train-mid, train-tail) are in the train's frame: +x the travel direction, y 0 on the
guideway's top, z across; each car's origin is at its own front end (the nose's tip for the nose car), the car running back
along -x. Core NativeCyberCityArt.Pose puts them where NativeCyberCityKit.TrainPlace puts the kit's boxes."""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, lathe, sweep
import cybercity_kit as ck
from cybercity_kit import F, Fr, Blk, glow, rod, h01, pick
import cybercity_city as city


class Parts:
    """The steady kit parts by block (cybercity_city.pieces reads them through this)."""

    def __init__(self, L):
        self.L = L
        self.steady = {}
        for p in L['parts']:
            if p['drive'] == 0:
                self.steady.setdefault(p['o'], []).append(p)
        self.driven = {}

    def parts(self, blk):
        return self.steady.get(blk.id, [])


# ---------------------------------------------------------------------------------------------------------------------------
# Breakable blocks (in the world; block_models moves them into their frames)
# ---------------------------------------------------------------------------------------------------------------------------
def stall(g, S, b, T):
    """A market stall: a plank-fronted timber counter with a board top, a striped cloth skirt, crates of goods on it, two
    poles carrying the kit's paper lanterns, its menu screen."""
    sg = -1 if b.c[2] > 0 else 1
    box(g, b.x0 + .04, b.x1 - .04, b.y0, b.y1 - .05, b.z0 + .04, b.z1 - .04, F['timberdarkx'], T, .015, cell=0, skip=('-y',))
    box(g, b.x0 - .05, b.x1 + .05, b.y1 - .05, b.y1, b.z0 - .05, b.z1 + .05, F['timberx'], T, .015, cell=0)
    if T.i < 2:
        for s2 in (-1, 1):
            fr = b.face(2, s2)
            n = 6
            w = (2 * fr.half - .08) / n
            for k in range(n):
                fr.box(g, -fr.half + .04 + k * w + .012, -fr.half + .04 + (k + 1) * w - .012, .06, b.s[1] - .1, -.04, -.022, F['timberx'], None, 0)
        # goods: a few crates and bowls on the top
        for k in range(3):
            x = b.x0 + .35 + k * .75 + .1 * h01(b.id, 'g', k)
            hh = .14 + .1 * h01(b.id, 'gh', k)
            z = b.c[2] - sg * .15
            box(g, x - .22, x + .22, b.y1, b.y1 + hh, z - .2, z + .2, F['timberdarkx'] if k % 2 else F['bin'], T, .012, cell=0, skip=('-y',))
            if T.i == 0:
                for j in range(3):
                    ck.ball(g, (x - .12 + j * .12, b.y1 + hh + .03, z + (j % 2) * .08 - .04), .06, city.metal_fin(pick((0xD2532F, 0xE0A63A, 0x6FA84A), b.id, 'fruit', k, j)), 5, 3)
    for p in S.parts(b):
        c, h, look = p['c'], p['h'], p['look']
        if look == 'neon':
            city.lantern(g, c, 2 * h[0], 2 * h[1], p['col'], p['gain'], T)
        elif look == 'timber':
            continue
        else:
            city.piece(g, S, b, p, T)


def vending(g, S, b, T):
    """A vending machine: a rounded cabinet, a lit window of cans on shelves, the kit's screen and light line, a coin panel
    and a tray."""
    ax = 0 if b.s[0] < b.s[2] else 2
    sg = (1 if b.c[0] < 0 else -1) if ax == 0 else (1 if b.c[2] < 0 else -1)
    ck.rbox(g, b.x0, b.x1, b.y0 - .02, b.y1, b.z0, b.z1, .07, F['clad5'], 2 if T.i == 0 else 1)
    fr = b.face(ax, sg)
    for p in S.parts(b):
        if p['look'] == 'cyber-holo':
            pf = city.part_frame(b, p)
            if pf is None:
                continue
            f2, d = pf
            # the window: cans in rows against the light
            f2.box(g, -f2.half, f2.half, 0, f2.tall, 0, d, F['metaldark'], T, .01)
            f2.quad(g, -f2.half + .04, f2.half - .04, .04, f2.tall - .04, d + .003, glow(p['col'], p['gain'] * .8), 0)
            if T.i < 2:
                rows = 4
                for r in range(rows):
                    v = .08 + r * (f2.tall - .12) / rows
                    f2.box(g, -f2.half + .04, f2.half - .04, v - .02, v, d + .003, d + .012, F['metaldark'], None, 0)
                    if T.i == 0:
                        n = 5
                        for k in range(n):
                            u = -f2.half + .1 + k * (2 * f2.half - .2) / (n - 1)
                            f2.box(g, u - .035, u + .035, v, v + .16, d + .004, d + .012, city.metal_fin(pick((0xC8343A, 0x2E7FD0, 0xE0B02A, 0x3FA070, 0xE8E8F0), b.id, 'can', r, k)), None, 0)
        else:
            city.piece(g, S, b, p, T)
    if T.i < 2:
        fr.box(g, -fr.half + .08, fr.half - .08, .06, .2, 0, .03, F['void'], None, 0)
        fr.box(g, fr.half - .2, fr.half - .08, .9, 1.2, 0, .02, F['steel'], T, .006)


def dumpster(g, S, b, T):
    """A wheeled bin: a tapered ribbed body under two lid flaps, a lip, handles, wheels."""
    alongx = b.s[0] >= b.s[2]
    t = Geo()
    L, W, H = (b.s[0], b.s[2], b.s[1]) if alongx else (b.s[2], b.s[0], b.s[1])
    y0 = .14
    bot = [(-L / 2 + .1, -W / 2 + .1), (L / 2 - .1, -W / 2 + .1), (L / 2 - .1, W / 2 - .06), (-L / 2 + .1, W / 2 - .06)]
    topr = [(-L / 2 + .02, -W / 2 + .02), (L / 2 - .02, -W / 2 + .02), (L / 2 - .02, W / 2 - .02), (-L / 2 + .02, W / 2 - .02)]
    yt = H - .16
    for i in range(4):
        j = (i + 1) % 4
        a, e, c, d = bot[i], bot[j], topr[j], topr[i]
        mx, mz = (a[0] + e[0]) / 2, (a[1] + e[1]) / 2
        t.face(((a[0], y0, a[1]), (e[0], y0, e[1]), (c[0], yt, c[1]), (d[0], yt, d[1])), F['bin'], (mx, .1, mz))
    t.face([(p[0], y0, p[1]) for p in bot], F['bin'], (0, -1, 0))
    # the lip and the lids
    box(t, -L / 2, L / 2, yt, yt + .05, -W / 2, W / 2, F['bin'], T, .012, cell=0)
    for s in (-1, 1):
        xa, xb = (s * .01, s * (L / 2 - .01))
        t.face(((min(xa, xb), yt + .05, -W / 2 + .01), (max(xa, xb), yt + .05, -W / 2 + .01), (max(xa, xb), H, W / 2 - .2), (min(xa, xb), H, W / 2 - .2)), F['metaldark'], (0, 1, -.1))
        t.face(((min(xa, xb), H, W / 2 - .2), (max(xa, xb), H, W / 2 - .2), (max(xa, xb), yt + .05, W / 2 - .01), (min(xa, xb), yt + .05, W / 2 - .01)), F['metaldark'], (0, 1, .6))
    for xe in (-L / 2 + .01, L / 2 - .01, 0.0):
        t.face(((xe, yt + .05, -W / 2 + .01), (xe, H, W / 2 - .2), (xe, yt + .05, W / 2 - .01)), F['metaldark'], (1 if xe > 0 else -1, 0, 0))
        if xe == 0.0:
            t.face(((xe, yt + .05, -W / 2 + .01), (xe, H, W / 2 - .2), (xe, yt + .05, W / 2 - .01)), F['metaldark'], (1, 0, 0))
    if T.i < 2:
        # ribs on the long sides, handles on the ends, wheels
        for s in (-1, 1):
            for k in range(5):
                x = -L / 2 + .3 + k * (L - .6) / 4
                zb, zt = s * (W / 2 - (.1 if s < 0 else .06)), s * (W / 2 - .02)
                t.face(((x - .04, y0 + .05, zb + s * .02), (x + .04, y0 + .05, zb + s * .02), (x + .04, yt - .02, zt + s * .02), (x - .04, yt - .02, zt + s * .02)), F['bin'], (0, .05, s))
            xe = s * (L / 2 - .01)
            rod(t, (xe + s * .05, yt - .25, -.25), (xe + s * .05, yt - .25, .25), .02, F['steel'], 4, caps=False)
        for sx in (-1, 1):
            for sz in (-1, 1):
                c = (sx * (L / 2 - .25), .08, sz * (W / 2 - .2))
                lathe(t, [(.08, -.03), (.08, .03)], c, F['rubber'], (8, 6, 4)[T.i], axis=(0, 0, 1), cap0=True, cap1=True)
    q = (0, 0, 0, 1) if alongx else sk.quat_axis(0, 1, 0, -math.pi / 2)
    g.extend(t.transformed(sk.affine(q, (b.c[0], b.y0, b.c[2]))))
    city.pieces(g, S, b, T)


def bike(g, S, b, T):
    """A hover bike on its stand: a lofted body with a seat, a screen and bars, light bars fore and aft, two lift pods."""
    alongx = b.s[0] >= b.s[2]
    L, W, H = (b.s[0], b.s[2], b.s[1]) if alongx else (b.s[2], b.s[0], b.s[1])
    t = Geo()
    n = (10, 8, 6)[T.i]
    xs = [-L / 2 + .04, -L / 2 + .3, -L * .2, L * .05, L * .28, L / 2 - .2, L / 2 - .04]
    hw = [.06, .17, .2, .16, .2, .15, .05]
    yb = [.5, .34, .3, .3, .3, .36, .52]
    yt = [.62, .74, .7, .64, .84, .78, .6]
    rings = [ck.section_yz(xs[i], yb[i], yt[i], hw[i] * W / .8 * 1.6, n, 2.6) for i in range(len(xs))]
    ck.skin(t, rings, F['bike'])
    # the seat, the screen, the bars
    ck.rbox(t, -L * .28, L * .02, .66, .76, -.15, .15, .04, F['seat'], 1)
    t.face(((L * .26, .82, -.16), (L * .26, .82, .16), (L * .18, 1.06, .12), (L * .18, 1.06, -.12)), F['glass'], (1, .5, 0))
    t.face(((L * .26, .82, -.16), (L * .26, .82, .16), (L * .18, 1.06, .12), (L * .18, 1.06, -.12)), F['glass'], (-1, -.5, 0))
    rod(t, (L * .16, .9, -W / 2 + .06), (L * .16, .9, W / 2 - .06), .02, F['chrome'], 5)
    # the lift pods and the stand
    for x in (-L * .3, L * .3):
        lathe(t, [(.2, -.06), (.24, 0), (.2, .06)], (x, .22, 0), F['cabdark'], (10, 6, 5)[T.i], cap0=True, cap1=True)
    rod(t, (0, 0, .12), (0, .32, .05), .02, F['steel'], 4, caps=False)
    q = (0, 0, 0, 1) if alongx else sk.quat_axis(0, 1, 0, -math.pi / 2)
    g.extend(t.transformed(sk.affine(q, (b.c[0], b.y0, b.c[2]))))
    for p in S.parts(b):
        if p['look'] == 'cyber-glass':
            continue
        c, h = p['c'], p['h']
        if p['look'] == 'cyber-tube' and c[1] > b.y0 + .2:
            # a light bar at an end, drawn in to the body's tip
            box(g, c[0] - h[0] * (1 if alongx else .5), c[0] + h[0] * (1 if alongx else .5), b.y0 + .52, b.y0 + .6, c[2] - h[2] * (.5 if alongx else 1), c[2] + h[2] * (.5 if alongx else 1), glow(p['col'], p['gain']), None, 0, cell=0)
        else:
            city.piece(g, S, b, p, T)


def strut(g, S, b, T):
    """A billboard's strut: a lattice mast of four steel legs with zigzag bracing on a concrete foot, a striped collar and a
    red warning plate at shoulder height (what an explosive should take out), a cap plate under the truss."""
    r = b.s[0] / 2
    box(g, b.x0 - .02, b.x1 + .02, b.y0, b.y0 + .5, b.z0 - .02, b.z1 + .02, F['concrete'], T, .04, cell=0, skip=('-y',))
    leg = .07
    for (sx, sz) in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        x, z = b.c[0] + sx * (r - leg), b.c[2] + sz * (r - leg)
        box(g, x - leg, x + leg, b.y0 + .5, b.y1, z - leg, z + leg, F['gunmetal'], T if T.i == 0 else None, .015 if T.i == 0 else 0, cell=0, skip=('-y', '+y'))
    if T.i < 2:
        step = 1.0 if T.i == 0 else 2.0
        y = b.y0 + .5
        k = 0
        while y < b.y1 - step * .5:
            for (ax, sg) in ((0, 1), (0, -1), (2, 1), (2, -1)):
                fr = b.face(ax, sg)
                s = 1 if k % 2 == 0 else -1
                rod(g, fr.at(-s * (r - leg), y - b.y0, -leg), fr.at(s * (r - leg), min(y + step, b.y1) - b.y0, -leg), .028, F['steel'], 3, caps=False)
            y += step
            k += 1
    # the collar and the plate
    for (ax, sg) in ((0, 1), (0, -1), (2, 1), (2, -1)):
        fr = b.face(ax, sg)
        ck.stripes(g, fr.at(-r, 1.14, .012), (fr.ud[0] * 2 * r, 0, fr.ud[2] * 2 * r), (0, .52, 0), fr.n, .2, F['hazardy'], F['hazardk'])
        fr.box(g, -.2, .2, 1.85, 2.1, 0, .02, glow(ck.RED, 1.6), None, 0)
    box(g, b.x0 - .012, b.x1 + .012, b.y0 + 1.1, b.y0 + 1.7, b.z0 - .012, b.z1 + .012, F['hazardk'], None, 0, cell=0)
    city.pieces(g, S, b, T)


def billboard(g, S, b, T):
    """A billboard's carried pieces: the truss a lattice box girder; the screen a dark panel in its housing (its picture is the
    atmosphere's, 2 cm proud of the block's face) with ribs and a cable tray behind; the frames a bezel with the kit's lines."""
    s = b.stem
    sg = -1 if b.c[2] > 0 else 1
    if s.endswith('-truss'):
        if T.i == 2:
            box(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, F['gunmetal'], None, 0, cell=0)
        else:
            for (y, z) in ((b.y0 + .05, b.z0 + .05), (b.y0 + .05, b.z1 - .05), (b.y1 - .05, b.z0 + .05), (b.y1 - .05, b.z1 - .05)):
                box(g, b.x0, b.x1, y - .05, y + .05, z - .05, z + .05, F['gunmetal'], None, 0, cell=0)
            n = int(b.s[0] / (.8 if T.i == 0 else 1.6))
            for k in range(n):
                xa, xb = b.x0 + k * b.s[0] / n, b.x0 + (k + 1) * b.s[0] / n
                up = k % 2 == 0
                for z in (b.z0 + .05, b.z1 - .05):
                    rod(g, (xa, b.y0 + .05 if up else b.y1 - .05, z), (xb, b.y1 - .05 if up else b.y0 + .05, z), .025, F['steel'], 3, caps=False)
                rod(g, (xa, b.y1 - .05, b.z0 + .05), (xb, b.y1 - .05, b.z1 - .05), .02, F['steel'], 3, caps=False)
    elif s.endswith('-screen'):
        fr = b.face(2, sg)
        back = b.face(2, -sg)
        zf = b.z0 if sg < 0 else b.z1
        # the panel: its face 1 cm behind the block's (the picture plays 2 cm in front of that)
        fr.quad(g, -fr.half, fr.half, 0, b.s[1], -.01, F['screenoff'], 0)
        box(g, b.x0, b.x1, b.y0, b.y1, min(zf - sg * .012, b.c[2] - sg * b.s[2] / 2), max(zf - sg * .012, b.c[2] - sg * b.s[2] / 2), F['metaldark'], None, 0, cell=0, skip=('-z',) if sg < 0 else ('+z',))
        if T.i < 2:
            n = 7
            for k in range(n + 1):
                u = -back.half + k * 2 * back.half / n
                back.box(g, min(max(u - .04, -back.half), back.half - .08), min(max(u - .04, -back.half), back.half - .08) + .08, 0, b.s[1], 0, .06, F['gunmetal'], None, 0)
            for v in (1.2, 3.6):
                back.box(g, -back.half, back.half, v, v + .12, .06, .1, F['gunmetal'], None, 0)
    else:
        box(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, F['metaldark'], T, .03, cell=0)
    city.pieces(g, S, b, T)


BUILDERS = (('-stall-', stall), ('-vend-', vending), ('-bin-', dumpster), ('-bike', bike), ('-strut-', strut), ('billboard-', billboard))


def block_models(L, T):
    """The breakable and carried blocks' models, each in its block's frame: {block id: (Geo, block)}."""
    S = Parts(L)
    out = {}
    for bd in L['blocks']:
        if bd['role'] == 'static':
            continue
        b = Blk(bd)
        g = Geo()
        for key, fn in BUILDERS:
            if key in b.stem:
                fn(g, S, b, T)
                break
        else:
            city.fallback(g, S, b, T)
        out[b.id] = (g.transformed([1, 0, 0, -b.c[0], 0, 1, 0, -b.c[1], 0, 0, 1, -b.c[2]]), bd)
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# The maglev train
# ---------------------------------------------------------------------------------------------------------------------------
HALF = 2.45       # the body's half width
SKIRT, ROOF = .3, 2.6


def car(T, length, nose=False, tail=False):
    """A car in the train's frame with its front end at x 0, running back to -length. A nose car tapers over its first 3 m to
    the tip at x 0; a tail car over its last 3 m to its tip at -length."""
    g = Geo()
    n = (20, 14, 10)[T.i]
    body, roof = F['train'], F['trainroof']
    taper = 3.0

    def sect(x, k):
        # k: 1 the full section, 0 the tip
        e = k ** .55
        hw = HALF * (.5 + .5 * e)
        y1 = SKIRT + (ROOF - SKIRT) * (.38 + .62 * e) + .1
        return ck.section_yz(x, SKIRT, y1, hw, n, 3.2 + 1.6 * e, cy=None)
    xs = []
    if nose:
        steps = (6, 4, 3)[T.i]
        xs += [(-taper * i / steps, i / steps) for i in range(steps)]
        xs[0] = (-.06, .04)
    xs += [(-taper if nose else 0.0, 1.0), (-(length - taper) if tail else -length, 1.0)]
    if tail:
        steps = (6, 4, 3)[T.i]
        xs += [(-(length - taper) - taper * i / steps, 1 - i / steps) for i in range(1, steps + 1)]
        xs[-1] = (-length + .06, .04)
    rings = [sect(x, k) for (x, k) in xs]
    ck.skin(g, rings, body, close0=True, close1=True)
    x0b, x1b = (-taper if nose else 0.0), (-(length - taper) if tail else -length)
    # the roof's raised deck, the skirt and the levitation glow under it
    box(g, x1b + .3, x0b - .3, ROOF + .08, ROOF + .2, -2.0, 2.0, roof, T, .04, cell=0, skip=('-y',))
    box(g, x1b + .1, x0b - .1, .1, SKIRT + .06, -2.1, 2.1, F['trainskirt'], T, .03, cell=0)
    box(g, x1b + .3, x0b - .3, .06, .1, -1.9, 1.9, glow(ck.VIOLET, 2.8), None, 0, cell=0, skip=('+y',))
    for s in (-1, 1):
        z = s * (HALF + .012)
        zz = (min(z, z - s * .03), max(z, z - s * .03))
        # the window band with a few lit windows, the doors, the light lines
        nw = max(2, int((x0b - x1b) / 1.4))
        w = (x0b - x1b - 1.0) / nw
        for k in range(nw):
            xa = x1b + .5 + k * w
            lit = h01('trainwin', int(length), k, s) < .55
            box(g, xa + .08, xa + w - .08, 1.56, 2.14, zz[0], zz[1], glow(0xCFE4FF if (k + int(length)) % 3 else 0xFFD9A8, .9) if lit else F['glass'], None, 0, cell=0)
        if T.i < 2:
            box(g, x1b + .4, x0b - .4, 1.5, 1.56, zz[0], zz[1], F['trainstripe'], None, 0, cell=0)
            d = x1b + 1.2
            while d < x0b - 1:
                box(g, d - .45, d + .45, .45, 2.3, zz[0] - s * .0, zz[1] + s * .012, F['trainskirt'], None, 0, cell=0)
                box(g, d - .01, d + .01, .5, 2.25, zz[0], zz[1] + s * .016, F['void'], None, 0, cell=0)
                d += 4.2
        box(g, x1b, x0b, .52, .58, zz[0], zz[1] + s * .02, glow(ck.CYAN, 3.2), None, 0, cell=0)
        box(g, x1b + .3, x0b - .3, ROOF + .02, ROOF + .06, s * 2.2 - .02, s * 2.2 + .02, glow(ck.VIOLET, 2.6), None, 0, cell=0)
    for (on, sg, xt) in ((nose, 1, 0.0), (tail, -1, -length)):
        if not on:
            continue
        col = ck.ICE if sg > 0 else ck.RED
        # the windscreen: a dark band round the taper's upper half, and the light bar across the tip
        xa, xb = xt - sg * 2.3, xt - sg * 1.0
        ra, rb = sect(xa, .78), sect(xb, .36)
        m = len(ra)
        for j in range(m):
            j1 = (j + 1) % m
            if ra[j][1] < 1.55 or ra[j1][1] < 1.55:
                continue
            o = .02
            pa, pb, pc, pd = ra[j], ra[j1], rb[j1], rb[j]
            nrm = (sg * .5, .6, (pa[2] + pb[2]) * .2)
            off = sk._norm(nrm)
            g.face((sk._add(pa, off, o), sk._add(pb, off, o), sk._add(pc, off, o), sk._add(pd, off, o)), F['glass'], nrm)
        tip = sect(xt - sg * .45, .16)
        zmax = max(p[2] for p in tip)
        box(g, min(xt - sg * .5, xt - sg * .36), max(xt - sg * .5, xt - sg * .36), .86, 1.0, -zmax * .9, zmax * .9, glow(col, 5.0), None, 0, cell=0)
        for s in (-1, 1):
            xx = xt - sg * 1.0
            box(g, min(xx, xx + sg * .1), max(xx, xx + sg * .1), .56, .68, s * 1.5 - .35, s * 1.5 + .35, glow(col, 5.0), None, 0, cell=0)
    return g


def train_cars(L, T):
    cars = L['city']['cars']
    return [('train-nose', car(T, cars[0], nose=True)), ('train-mid', car(T, cars[1])), ('train-tail', car(T, cars[2], tail=True))]


def rest_scene(L, T, train=True):
    """The breakable and carried blocks on their places (the static bake's occluders and the previews), and for the previews
    the train standing in the Cut."""
    g = Geo()
    for bid, (geo, bd) in block_models(L, T).items():
        p = bd['pos']
        g.extend(geo.transformed([1, 0, 0, p[0], 0, 1, 0, p[1], 0, 0, 1, p[2]]))
    if train:
        C = L['city']
        x = -12.0
        for (name, geo), ln in zip(train_cars(L, T), C['cars']):
            g.extend(geo.transformed([1, 0, 0, x, 0, 1, 0, C['railTop'], 0, 0, 1, C['track']]))
            x -= ln + .5
    return g


def bank_props(sb, L, T, floors, no_ao):
    ti = T.i
    items = []
    for bid, (geo, bd) in block_models(L, T).items():
        items.append(('block/' + bid, 'block', geo, tuple(bd['pos'])))
    for (name, geo) in train_cars(L, T):
        items.append(('rig/' + name, 'rig', geo, (0.0, L['city']['railTop'], 0.0)))
    aos = [None] * len(items)
    if not no_ao:
        apart = [it[2].transformed([1, 0, 0, 60.0 * (i % 8), 0, 1, 0, 0, 0, 0, 1, 60.0 * (i // 8)]) for i, it in enumerate(items)]
        aos = sk.bake_ao(apart, distance=.5, samples=(24, 12, 8)[ti])
    lt = 1 if ti == 0 else ti
    for (name, group, g, rest), ao in zip(items, aos):
        if g.faces == 0:
            continue
        # a prop's layered faces (a stripe on its band, a screen's picture on its bezel) stand the tier's lift apart, as the
        # static scene's do (the Fine models also draw at the Mid range)
        sk.lift_coplanar(g, eps=(.0055, .0065, .0125)[lt], lift=sk.LIFT_TIER[lt], passes=4)
        P = g.arrays()[0].astype(np.float64)
        if ao is not None:
            ao = 1 - (1 - ao) * .8
        bk = sk.Bake(g, ao, world=P + np.array(rest), soot=None, floors=floors, seed=7)
        sb.add('%s/%d' % (name, ti), group, sk.to_mesh('%s/%d' % (name, ti), g, bk))
