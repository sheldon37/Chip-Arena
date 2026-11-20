"""Flashbulb's set: a premiere's red carpet outside a theatre at night (docs/INTROS-GENERIC.md "Flashbulb"). Unity space; the hero mark
(the origin) is the carpet's mark, the pilot facing +z; the theatre's doors are behind it at FB_FRONT. Pieces (generic_sets.py writes
them):
  flashbulb/street    the pavement, the kerb and the road, the carpet, the stanchions and ropes, the barriers, the theatre front,
                      its marquee's housing, the blade sign's box, the poster cases, the lamp posts (world frame)
  flashbulb/bulbs0..3 the marquee's bulbs in four interleaved runs (glow; world frame): Core chases them
  flashbulb/neon      the blade sign's tubes (glow; world frame)
  flashbulb/posters   the lit posters (glow; world frame)
  flashbulb/glass     the doors' and the foyer's lit glass (glow; world frame), the swinging leaves excepted
  flashbulb/leaf      one swinging door leaf, its hinge at the origin, the leaf along +x (local), 0.95 m wide, its glass a glow
  flashbulb/globes    the lamp posts' globes (glow; world frame)
  flashbulb/sky       the night sky dome (glow)
"""
import math

import numpy as np
import stagekit as sk
from stagekit import Geo, _add, _sub, _norm
from generic_kit import (PI, TAU, V, mul, M, place, catenary, dome, disc, flipped, mix_hex, Piece, hash01)

FB_FRONT = -6.6          # the theatre's front plane (z)
FB_DOORS = (-1.0, 1.0)   # the swinging pair's hinges (x), at the front plane
FB_CARPET = (-6.3, 4.2)  # the carpet's z range
FB_POSTS = 8             # stanchions each side


def build(T):
    F = dict(
        pave=sk.fin('fb-pave', 0x8a8a8c, sk.DETAIL_FLAG, smooth=.35, dirt=.8),
        kerb=sk.fin('fb-kerb', 0xa3a19c, sk.DETAIL_PLASTER, smooth=.2, dirt=.9),
        road=sk.fin('fb-road', 0x2c2d31, smooth=.55, dirt=.6),
        line=sk.fin('fb-line', 0xe8e2d0, smooth=.3, dirt=.5),
        carpet=sk.fin('fb-carpet', 0xb3122b, sk.DETAIL_CLOTH, smooth=.12, dirt=.2),
        braid=sk.fin('fb-braid', 0xd8a84a, sk.DETAIL_BRASS, smooth=.55, metal=.8, dirt=.2),
        brass=sk.fin('fb-brass', 0xd9a84b, sk.DETAIL_BRASS, smooth=.75, metal=.95, dirt=.2, wear=.3),
        rope=sk.fin('fb-rope', 0x7a0c1c, sk.DETAIL_CLOTH, smooth=.25, dirt=.1),
        barrier=sk.fin('fb-barrier', 0x9aa1a8, sk.DETAIL_IRON, smooth=.5, metal=.8, dirt=.4),
        stone=sk.fin('fb-stone', 0xe3d6bf, sk.DETAIL_CARVED, smooth=.2, dirt=.7, wear=.3),
        stone2=sk.fin('fb-stone2', 0xcbbb9f, sk.DETAIL_ASHLAR, smooth=.2, dirt=.8),
        deco=sk.fin('fb-deco', 0x1c4a52, smooth=.5, metal=.4, dirt=.3),
        gold=sk.fin('fb-gold', 0xd6a440, sk.DETAIL_BRASS, smooth=.7, metal=.9, dirt=.3, wear=.4),
        dark=sk.fin('fb-dark', 0x16181d, smooth=.6, metal=.3, dirt=.2),
        marquee=sk.fin('fb-marquee', 0xe9e2d2, smooth=.4, dirt=.4),
        letters=sk.fin('fb-letters', 0x1b1c20, smooth=.3, dirt=.2),
        bulb=sk.fin('fb-bulb', 0xfff0c8, glow=1.0, dirt=0),
        neon=sk.fin('fb-neon', 0xff5aa8, glow=1.0, dirt=0),
        neon2=sk.fin('fb-neon2', 0x6fe0ff, glow=1.0, dirt=0),
        poster1=sk.fin('fb-poster1', 0xff8a3c, glow=1.0, dirt=0),
        poster2=sk.fin('fb-poster2', 0x6a5cff, glow=1.0, dirt=0),
        glass=sk.fin('fb-glass', 0xffd9a0, glow=1.0, dirt=0),
        globe=sk.fin('fb-globe', 0xfff4dc, glow=1.0, dirt=0),
        sky=sk.fin('fb-sky', 0xffffff, glow=1.0, dirt=0),
    )
    fine = T.i == 0
    g = Geo()
    # ---- the pavement, kerb, road ----
    sk.box(g, -16, 16, -.3, .012, FB_FRONT, 5.6, F['pave'], T, .02, skip=('-z',))
    sk.box(g, -16, 16, -.3, .012, 5.6, 5.85, F['kerb'], T, .02)
    g.face((V(-40, -.14, 5.85), V(40, -.14, 5.85), V(40, -.14, 40), V(-40, -.14, 40)), F['road'], normal=(0, 1, 0))
    for k in range(8):
        x0 = -28 + 8 * k
        g.face((V(x0, -.135, 11.6), V(x0 + 3.5, -.135, 11.6), V(x0 + 3.5, -.135, 11.8), V(x0, -.135, 11.8)), F['line'], normal=(0, 1, 0))
    # ---- the carpet: red, a raised edge, a gold braid ----
    z0, z1 = FB_CARPET
    sk.box(g, -1.22, 1.22, .012, .03, z0, z1, F['carpet'], T, .008)
    for sx in (-1, 1):
        sk.box(g, sx * 1.22 - .03, sx * 1.22 + .03, .012, .034, z0, z1, F['braid'], None, .004, cell=0)
    # ---- the stanchions and their velvet ropes ----
    posts = []
    for sx in (-1, 1):
        row = []
        for k in range(FB_POSTS):
            z = z0 + .9 + (z1 - z0 - 1.6) * k / (FB_POSTS - 1)
            x = sx * 1.68
            sk.lathe(g, [(.001, .03), (.17, .03), (.17, .06), (.05, .1), (.028, .14), (.026, .9), (.04, .93), (.035, .96)], V(x, 0, z), F['brass'], 14 if fine else 6)
            sk.lathe(g, [(.001, .95), (.035, .97), (.048, 1.0), (.035, 1.04), (.001, 1.06)], V(x, 0, z), F['brass'], 10 if fine else 5)
            row.append(V(x, .9, z))
        posts.append(row)
        for k in range(FB_POSTS - 1):
            sk.tube(g, catenary(row[k], row[k + 1], .16, 8 if fine else 3), .03, F['rope'], 7 if fine else 4)
    # ---- the press barriers either side ----
    bar = Geo()
    sk.tube(bar, [V(-1.0, 0, 0), V(-1.0, 1.08, 0), V(1.0, 1.08, 0), V(1.0, 0, 0)], .022, F['barrier'], 6 if fine else 4, caps=True)
    sk.cylinder(bar, V(-1.0, .2, 0), V(1.0, .2, 0), .02, F['barrier'], 5 if fine else 3)
    if fine:
        for k in range(1, 12):
            x = -1.0 + 2.0 * k / 12
            sk.cylinder(bar, V(x, .2, 0), V(x, 1.08, 0), .009, F['barrier'], 4, caps=False)
    for sx in (-1, 1):
        sk.box(bar, sx * 1.0 - .03, sx * 1.0 + .03, 0, .03, -.32, .32, F['barrier'], None, .005, cell=0)
    for sx in (-1, 1):
        for k in range(5):
            z = FB_FRONT + 2.2 + 2.1 * k
            place(g, bar, M((sx * 3.6, 0, z), yaw=PI / 2))
    # ---- the theatre's front: stone, pilasters with deco caps, setbacks, deep doors, the marquee ----
    zf = FB_FRONT
    g.face((V(-14, 0, zf), V(14, 0, zf), V(14, 14, zf), V(-14, 14, zf)), F['stone2'], normal=(0, 0, 1))
    # the entrance recess and its glass (the swinging pair's frame left open)
    glass = Geo()
    sk.box(g, -4.2, 4.2, 0, 3.3, zf - .02, zf + .06, F['dark'], None, .01, cell=0)
    for k, x in enumerate((-3.0, -1.0, 1.0, 3.0)):
        sk.box(g, x - .06, x + .06, 0, 3.0, zf + .05, zf + .12, F['gold'], None, .01, cell=0)
    sk.box(g, -4.2, 4.2, 2.9, 3.05, zf + .05, zf + .14, F['gold'], None, .01, cell=0)
    for x0 in (-2.95, 1.05):
        glass.face((V(x0, .05, zf + .07), V(x0 + 1.9, .05, zf + .07), V(x0 + 1.9, 2.88, zf + .07), V(x0, 2.88, zf + .07)), F['glass'], normal=(0, 0, 1))
        sk.box(g, x0 + .9, x0 + 1.0, .05, 2.88, zf + .07, zf + .1, F['gold'], None, 0, cell=0)
    # the foyer's glow behind the swinging pair (seen when they open)
    glass.face((V(-1.0, 0, zf - .6), V(1.0, 0, zf - .6), V(1.0, 2.95, zf - .6), V(-1.0, 2.95, zf - .6)), F['glass'], normal=(0, 0, 1))
    for sx in (-1, 1):
        g.face((V(sx * 1.0, 0, zf - .6), V(sx * 1.0, 0, zf + .05), V(sx * 1.0, 2.95, zf + .05), V(sx * 1.0, 2.95, zf - .6)), F['dark'], normal=(-sx, 0, 0))
    g.face((V(-1.0, 2.95, zf - .6), V(1.0, 2.95, zf - .6), V(1.0, 2.95, zf + .05), V(-1.0, 2.95, zf + .05)), F['dark'], normal=(0, -1, 0))
    g.face((V(-1.0, .005, zf - .6), V(1.0, .005, zf - .6), V(1.0, .005, zf + .05), V(-1.0, .005, zf + .05)), F['carpet'], normal=(0, 1, 0))
    # pilasters: fluted, rising to stepped deco caps
    for x in (-13.0, -8.6, -4.8, 4.8, 8.6, 13.0):
        h = 12.4 if abs(x) < 6 else 11.0 if abs(x) < 10 else 9.6
        sk.box(g, x - .55, x + .55, 0, h, zf, zf + .45, F['stone'], T, .03)
        if fine:
            for k in range(3):
                xx = x - .3 + .3 * k
                sk.box(g, xx - .05, xx + .05, .4, h - .6, zf + .45, zf + .48, F['stone2'], None, 0, cell=0)
        for k in range(3):
            w = .7 - .17 * k
            sk.box(g, x - w, x + w, h + .3 * k, h + .3 * (k + 1), zf, zf + .5 - .08 * k, F['stone'], T, .02)
        sk.box(g, x - .12, x + .12, h + .9, h + 1.6, zf + .1, zf + .3, F['gold'], None, .01, cell=0)
    # the upper bands: deco chevrons in teal and gold between the pilasters, tall dark windows
    for (xa, xb) in ((-8.05, -5.35), (5.35, 8.05), (-4.25, 4.25)):
        sk.box(g, xa, xb, 5.0, 9.6, zf, zf + .05, F['deco'], None, .01, cell=0)
        n = max(2, int((xb - xa) / .9))
        for k in range(n):
            x = xa + (xb - xa) * (k + .5) / n
            sk.box(g, x - .28, x + .28, 5.5, 9.0, zf + .05, zf + .1, F['dark'], None, .01, cell=0)
            if fine:
                for c in range(3):
                    y = 9.15 + c * .14
                    sk.box(g, x - .3 + c * .1, x + .3 - c * .1, y, y + .08, zf + .05, zf + .12, F['gold'], None, 0, cell=0)
    sk.box(g, -14, 14, 10.4, 10.7, zf, zf + .35, F['stone'], T, .02)
    # ---- the marquee: a canopy over the doors, a letter board on its face, bulbs round it in four runs ----
    mz0, mz1, my0, my1 = zf, zf + 2.6, 3.5, 4.55
    sk.box(g, -5.6, 5.6, my0, my1, mz0, mz1, F['marquee'], T, .04)
    sk.box(g, -5.75, 5.75, my1, my1 + .12, mz0, mz1 + .12, F['gold'], None, .02, cell=0)
    sk.box(g, -5.75, 5.75, my0 - .1, my0, mz0, mz1 + .12, F['gold'], None, .02, cell=0)
    # the letter board: a white field with rows of dark bars (no words)
    sk.box(g, -4.4, 4.4, 4.62, 5.95, mz1 - .25, mz1 - .15, F['marquee'], None, .01, cell=0)
    sk.box(g, -4.5, 4.5, 4.55, 4.62, mz1 - .3, mz1 - .1, F['gold'], None, 0, cell=0)
    sk.box(g, -4.5, 4.5, 5.95, 6.02, mz1 - .3, mz1 - .1, F['gold'], None, 0, cell=0)
    if fine:
        for r_ in range(3):
            y = 4.78 + r_ * .38
            x = -4.1
            k = 0
            while x < 4.0:
                w = .16 + .32 * hash01('lt', r_, k)
                if x + w > 4.1:
                    break
                if hash01('gap', r_, k) > .18:
                    sk.box(g, x, x + w, y, y + .24, mz1 - .14, mz1 - .12, F['letters'], None, 0, cell=0)
                x += w + .07
                k += 1
    bulbs = [Geo() for _ in range(4)]
    ring_pts = []
    for row in range(3 if fine else 2):
        y = my0 + .22 + row * .32
        for k in range(44 if fine else 22):
            x = -5.5 + 11.0 * (k + .5) / (44 if fine else 22)
            ring_pts.append((V(x, y, mz1 + .14), (0, 0, 1), k + row))
        for sx in (-1, 1):
            for k in range(10 if fine else 5):
                z = mz0 + .2 + (mz1 - mz0 - .3) * (k + .5) / (10 if fine else 5)
                ring_pts.append((V(sx * 5.72, y, z), (sx, 0, 0), k + row + 3))
    for k, (p, nrm, idx) in enumerate(ring_pts):
        b = bulbs[idx % 4]
        sk.lathe(b, [(.001, -.06), (.045, -.04), (.055, 0), (.045, .04), (.001, .06)], p, F['bulb'], 6 if fine else 4, axis=nrm)
    # ---- the blade sign: a tall box off the front with neon on both faces ----
    neon = Geo()
    bx = 6.7
    sk.box(g, bx - .22, bx + .22, 5.4, 13.6, zf, zf + 1.7, F['dark'], T, .03)
    sk.box(g, bx - .26, bx + .26, 13.6, 13.8, zf - .02, zf + 1.78, F['gold'], None, .02, cell=0)
    for sx in (-1, 1):
        x = bx + sx * .225
        nr_ = (sx, 0, 0)
        # a column of stars and a double stripe (no words)
        for k in range(5):
            cy = 6.3 + k * 1.55
            cz = zf + .85
            pts = []
            for m in range(10):
                a = PI / 2 + TAU * m / 10
                rr = .5 if m % 2 == 0 else .21
                pts.append(V(x + sx * .005, cy + math.sin(a) * rr, cz + math.cos(a) * rr * sx))
            centre = V(x + sx * .005, cy, cz)
            for m in range(10):
                neon.face((centre, pts[m], pts[(m + 1) % 10]), F['neon'] if k % 2 == 0 else F['neon2'], normal=nr_)
        for zz in (zf + .2, zf + 1.5):
            neon.face((V(x + sx * .006, 5.7, zz - .03), V(x + sx * .006, 5.7, zz + .03), V(x + sx * .006, 13.3, zz + .03), V(x + sx * .006, 13.3, zz - .03)), F['neon2'], normal=nr_)
    # ---- the poster cases ----
    posters = Geo()
    for sx in (-1, 1):
        x = sx * 6.75
        sk.box(g, x - .85, x + .85, .45, 2.95, zf, zf + .18, F['gold'], None, .02, cell=0)
        for k in range(4):
            y0 = .58 + k * .58
            posters.face((V(x - .72, y0, zf + .19), V(x + .72, y0, zf + .19), V(x + .72, y0 + .58, zf + .19), V(x - .72, y0 + .58, zf + .19)),
                         F['poster1'] if (k + (sx > 0)) % 2 == 0 else F['poster2'], normal=(0, 0, 1))

    def poster_fn(P):
        y = P[:, 1]
        x = P[:, 0]
        a = np.clip((y - .58) / 2.3, 0, 1)
        warm = mix_hex(0xffb24a, 0xff3f6a, a)
        cool = mix_hex(0x3a5cff, 0xc45cff, a)
        return np.where((x > 0)[:, None], warm, cool)
    # ---- lamp posts at the kerb ----
    globes = Geo()
    for x in (-12.5, -6.2, 6.2, 12.5):
        z = 5.1
        sk.lathe(g, [(.001, 0), (.22, 0), (.22, .25), (.14, .35), (.09, .6), (.065, 3.6), (.1, 3.75), (.06, 3.85)], V(x, 0, z), F['dark'], 12 if fine else 6)
        sk.lathe(globes, [(.001, 3.85), (.16, 3.92), (.21, 4.1), (.17, 4.3), (.08, 4.4), (.001, 4.42)], V(x, 0, z), F['globe'], 12 if fine else 6)
        sk.lathe(g, [(.001, 4.4), (.07, 4.42), (.02, 4.6)], V(x, 0, z), F['gold'], 8 if fine else 4)
    # ---- the swinging door leaf (its hinge at the origin) ----
    leaf = Geo()
    sk.box(leaf, 0, .95, 0, 2.9, -.03, .03, F['gold'], None, .01, cell=0)
    leaf.face((V(.08, .1, .032), V(.87, .1, .032), V(.87, 2.8, .032), V(.08, 2.8, .032)), F['glass'], normal=(0, 0, 1))
    leaf.face((V(.08, .1, -.032), V(.87, .1, -.032), V(.87, 2.8, -.032), V(.08, 2.8, -.032)), F['glass'], normal=(0, 0, -1))
    sk.cylinder(leaf, V(.82, .9, .04), V(.82, 2.0, .04), .025, F['brass'], 6)
    sky = Geo()
    dome(sky, 172, F['sky'], 10 if fine else 5, 32 if fine else 14, -16, 90)

    def sky_night(P):
        d = P / np.maximum(np.linalg.norm(P, axis=1, keepdims=True), 1e-9)
        up = np.clip(d[:, 1], 0, 1) ** .45
        return mix_hex(0x2a2440, 0x06070f, up)
    return [
        Piece('flashbulb/street', g, True, (0.0,), seed=9),
        Piece('flashbulb/bulbs0', bulbs[0], False),
        Piece('flashbulb/bulbs1', bulbs[1], False),
        Piece('flashbulb/bulbs2', bulbs[2], False),
        Piece('flashbulb/bulbs3', bulbs[3], False),
        Piece('flashbulb/neon', neon, False),
        Piece('flashbulb/posters', posters, False, recolour=({F['poster1'], F['poster2']}, poster_fn)),
        Piece('flashbulb/glass', glass, False),
        Piece('flashbulb/leaf', leaf, False, (-50.0,), preview=[M((FB_DOORS[0], 0, FB_FRONT + .08), yaw=-.9), M((FB_DOORS[1], 0, FB_FRONT + .08), yaw=PI + .9)]),
        Piece('flashbulb/globes', globes, False),
        Piece('flashbulb/sky', sky, False, (-500.0,), recolour=({F['sky']}, sky_night)),
    ]


VIEWS = [
    ('fb-hero', (.3, 1.1, 3.6), (0, 1.2, -2), 42),
    ('fb-wide', (0, 2.0, 12), (0, 3.0, -8), 60),
    ('fb-left', (-5.5, 1.6, 4.0), (1, 1.5, -6), 56),
    ('fb-high', (12, 12, 14), (0, 1, -5), 60),
]
