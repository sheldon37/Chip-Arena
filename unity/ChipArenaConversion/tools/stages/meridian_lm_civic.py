"""Meridian landmarks: the final court, NOCTIEL's: City Hall, its 62 m clock tower at 6:47, the court's walls and its gateway.

In the court's frame (Core places it at the gateway point: x across the gateway, y up from the court's floor, z north into the
court; the hall faces -z, back toward the gateway and the camp). The blocks it dresses, relative to that frame and the same in all
five layouts (check() asserts them): the court's walls 7 m tall and 1.5 m thick round x -37..37, z 0..64; the gateway's piers
x 6..8 either side, z -1..2.5, 12.5 m tall, with the lintel from 10 m; City Hall x -27..27, z 38.5..62.5, 19 m; its tower
x -6..6, z 44.5..56.5, from 19 m to 62 m, above every layout's ceiling.

Keyed to NOCTIEL (art bible 9.7): black stone and white marble, the windows' stained glass lit white-gold, white-gold lamps, the
gateway's lintel carved with falling feathers. The tower's four 6 m opal dials read 6:47 and are the far layer's own read.
"""
import math

import meridian_kit as mk
import meridian_lm_kit as lk
import stagekit as sk
from meridian_kit import F, Geo, bx, glow
from meridian_lm_kit import PI, OPAL, WHITE_GOLD

HALL = (-27.0, 27.0, 0.0, 19.0, 38.5, 62.5)
TOWER = (-6.0, 6.0, 19.0, 62.0, 44.5, 56.5)
WALLS = [(-37.0, -6.0, 0.0, 7.0, 0.0, 1.5), (6.0, 37.0, 0.0, 7.0, 0.0, 1.5), (-37.0, 37.0, 0.0, 7.0, 62.5, 64.0),
         (-37.0, -35.5, 0.0, 7.0, 0.0, 64.0), (35.5, 37.0, 0.0, 7.0, 0.0, 64.0)]
PIERS = [(-8.0, -6.0, 0.0, 12.5, -1.0, 2.5), (6.0, 8.0, 0.0, 12.5, -1.0, 2.5)]
LINTEL = (-6.0, 6.0, 10.0, 12.5, -1.0, 2.5)
BLOCKS = {'city-hall': HALL, 'city-hall-tower': TOWER, 'court-wall-S-a': WALLS[0], 'court-wall-S-b': WALLS[1],
          'court-wall-N': WALLS[2], 'court-wall-W': WALLS[3], 'court-wall-E': WALLS[4], 'court-gate-pier-a': PIERS[0],
          'court-gate-pier-b': PIERS[1], 'court-gate-lintel': LINTEL}
DIAL_Y, DIAL_R = 47.0, 3.0


def check(S, g0):
    """The layout's court blocks are where this module builds, relative to the gateway point g0 (x, y, z)."""
    for bid, b in BLOCKS.items():
        have = S[bid]
        want = (b[0] + g0[0], b[1] + g0[0], b[2] + g0[1], b[3] + g0[1], b[4] + g0[2], b[5] + g0[2])
        if not all(abs(a - c) < 1e-3 for a, c in zip(have, want)):
            raise AssertionError('court block %s at %s, built for %s' % (bid, have, want))


def court(t, S=None):
    T = lk.tier(t)
    E = lk.finishes()
    if t == 'far':
        return far(T, E)
    g = Geo()
    hall(g, T, E)
    tower(g, T, E)
    walls(g, T, E)
    gateway(g, T, E)
    return g


def hall(g, T, E):
    """City Hall on its block: a rusticated ground storey, a piano nobile of tall stained-glass windows lit white-gold, a giant
    order of engaged columns across the middle under a deep entablature and a pediment with the crest clock at 6:47, and an
    attic with CITY HALL in 1.5 m letters either side of the pediment."""
    x0, x1, y0, y1, z0, z1 = HALL
    white, black = E['marble-white'], E['stone-black']

    def face(sub, side, w):
        front = side == 's'
        wins = []
        bays = max(3, int(round(w / 4.5)))
        bw = w / bays
        for i in range(bays):
            cx = (i + .5) * bw
            mid = front and abs(cx - w / 2) < 9.5
            if front and abs(cx - w / 2) < bw / 2:
                wins.append((cx - 1.6, cx + 1.6, 0.0, 5.0, (WHITE_GOLD, 1.3), 1.6))
            else:
                wins.append((cx - .9, cx + .9, .9, 4.4, (WHITE_GOLD, .9) if mid else None, .9))
            wins.append((cx - .85, cx + .85, 7.0, 11.8, (WHITE_GOLD, 1.6) if (front or i % 2 == 0) else None, .85))
            if not mid:
                wins.append((cx - .6, cx + .6, 14.8, 16.8, None))
        lk.facade(sub, T, -.02, 0.0, w, y0, y1, white, wins, f_frame=black, reveal=.22, f_sill=white, flat=side != 's')
        if side == 'n':
            return       # the back stands against the court's north wall: plain over it, nothing proud where the wall's top runs
        for y in ((.9, 1.95, 3.0, 4.05, 5.1) if T.detail else ()):
            bx(sub, T, 0.0, w, y, y + .07, -.05, -.02, black, bevel=0, cell=0)
        bx(sub, T, 0.0, w, 0.0, .9, -.12, -.02, black, bevel=.02, skip=('+z',))
        lk.string_course(sub, T, 5.6, 0.0, w, -.02, white, h=.4, d=.25)
        lk.cornice(sub, T, 13.9, 0.0, w, -.02, white, s=1.4)
        lk.cornice(sub, T, y1, 0.0, w, -.02, black, s=1.0)
        for i in range(bays + 1):
            u = min(max(i * bw, .35), w - .35)
            if front and abs(u - w / 2) < 9.8:
                continue
            bx(sub, T, u - .3, u + .3, 0.0, 3.1, -.15, -.02, white, bevel=.02, skip=('+z',))
            bx(sub, T, u - .3, u + .3, 3.1, 13.0, -.42, -.02, white, bevel=.02, skip=('+z',))
        if front:
            _portico(sub, T, E, w)
    lk.skin(g, HALL, face)
    lk.roof_cap(g, T, HALL, F['lead'])


def _portico(s, T, E, w):
    """The giant order across the front's middle (canonical frame: x 0..w, facing -z at z 0)."""
    white, black, gilt = E['marble-white'], E['stone-black'], E['gilt']
    cx = w / 2
    xs = [cx + d for d in (-8.4, -5.0, -1.7, 1.7, 5.0, 8.4)]
    n = T.round + 4
    for x in xs:
        bx(s, T, x - .75, x + .75, 0.0, 3.15, -.24, -.02, black, bevel=.03, skip=('+z',))
        if T.detail:
            lk.half_lathe(s, [(.7, 3.15), (.62, 3.45), (.62, 12.2), (.7, 12.5)], x, -.02, white, n)
        else:
            bx(s, T, x - .62, x + .62, 3.15, 12.5, -.64, -.02, white, bevel=0, skip=('+z',))
        if T.detail:
            for y in (5.0, 7.5, 10.0):
                lk.half_lathe(s, [(.665, y), (.665, y + .3)], x, -.02, black, n)
        bx(s, T, x - .85, x + .85, 12.5, 13.1, -.95, -.02, white, bevel=.03)
    bx(s, T, xs[0] - 1.2, xs[-1] + 1.2, 13.1, 14.6, -1.05, -.02, white, bevel=.03)
    bx(s, T, xs[0] - 1.3, xs[-1] + 1.3, 14.6, 14.95, -1.2, -.02, black, bevel=.02)
    mk.text_stroke(s, 'MERIDIAN CITY HALL', cx, 13.35, -1.05, .14, gilt, T, style='cast', w=.1, d=.04)
    pw, ph = 10.4, 3.6
    tri = [(cx - pw, 14.95), (cx + pw, 14.95), (cx, 14.95 + ph)]
    s.face([(p[0], p[1], -.9) for p in tri], white, (0, 0, -1))
    for (a, b) in ((tri[0], tri[2]), (tri[2], tri[1])):
        mk.seg(s, T, (a[0], a[1], -1.0), (b[0], b[1], -1.0), .45, .5, black)
    sub = Geo()
    mk.clock(sub, T, 1.25, glow(OPAL, 1.5), gilt, F['iron-black'], F['iron-black'], roman=True, depth=.06, rim=.14)
    mk.part(s, sub, (0, 0, 0, 1), (cx, 16.3, -.92))
    for (x, word) in ((cx - 15.5, 'CITY'), (cx + 15.5, 'HALL')):
        mk.text_stroke(s, word, x, 15.3, -.04, .25, glow(WHITE_GOLD, 1.8), T, style='cast', w=.18, d=.06)


def tower(g, T, E):
    """The tower on its block: a shaft with black quoins and a lit lancet, the clock stage with four 6 m opal dials at 6:47 in
    black frames, the louvred belfry, and above the block's top (over every layout's ceiling) the cupola with its stained glass,
    the copper dome, the lantern lit white-gold, the red light and the gilt sun vane."""
    x0, x1, y0, y1, z0, z1 = TOWER
    white, black, gilt = E['marble-white'], E['stone-black'], E['gilt']

    def face(sub, side, w):
        cx = w / 2
        lk.facade(sub, T, -.02, 0.0, w, y0, 38.0, white, [(cx - .55, cx + .55, 24.0, 35.0, (WHITE_GOLD, 1.2), .55)], f_frame=black, reveal=.25)
        for k, y in enumerate(range(int(y0), 38, 1 if T.detail else 99)):
            a = 1.1 if k % 2 else .7
            for (u0, u1) in ((0.0, a), (w - a, w)):
                bx(sub, T, u0, u1, y + .03, y + .97, -.12, -.02, black, bevel=.02, skip=('+z',))
        lk.cornice(sub, T, 38.4, 0.0, w, -.02, black, s=1.0)
        mk.wall_rect(sub, T, 0.0, w, 38.4, 54.0, -.02, white, n=-1, cell=3.0)
        for (u0, u1) in ((0.0, 1.4), (w - 1.4, w)):
            bx(sub, T, u0, u1, 38.4, 53.6, -.6, -.02, white, bevel=.04, skip=('+z',))
            bx(sub, T, u0 + .2, u1 - .2, 40.0, 52.0, -.68, -.6, black, bevel=.02)
        dial = Geo()
        mk.clock(dial, T, DIAL_R, glow(OPAL, 1.8), gilt, F['iron-black'], F['iron-black'], roman=True, depth=.08, rim=.28)
        mk.part(sub, dial, (0, 0, 0, 1), (cx, DIAL_Y, -.12))
        for (ya, yb) in ((DIAL_Y - DIAL_R - .6, DIAL_Y - DIAL_R - .3), (DIAL_Y + DIAL_R + .3, DIAL_Y + DIAL_R + .6)):
            bx(sub, T, cx - DIAL_R - .6, cx + DIAL_R + .6, ya, yb, -.3, -.02, black, bevel=.02)
        lk.cornice(sub, T, 54.4, -.4, w + .4, -.02, white, s=1.4)
        mk.wall_rect(sub, T, 0.0, w, 54.4, 62.0, -.02, white, n=-1, cell=3.0)
        for u in (cx - 3.4, cx + .6):
            lk.window(sub, T, u, u + 2.8, 55.4, 59.6, -.04, None, black, rise=1.4, f_dark=F['void'])
            if T.detail:
                for k in range(7):
                    y = 55.6 + k * .6
                    bx(sub, T, u + .1, u + 2.7, y, y + .1, -.2, -.05, black, bevel=0, cell=0)
        lk.cornice(sub, T, 62.0, -.2, w + .2, -.02, black, s=1.0)
    lk.skin(g, TOWER, face)
    cz = (z0 + z1) / 2
    sides = 8
    bx(g, T, x0 + .3, x1 - .3, 62.0, 62.6, z0 + .3, z1 - .3, white, bevel=.03)
    for (ux, uz) in ((x0 + 1.0, z0 + 1.0), (x1 - 1.0, z0 + 1.0), (x0 + 1.0, z1 - 1.0), (x1 - 1.0, z1 - 1.0)):
        sk.lathe(g, [(.5, 62.6), (.55, 63.0), (.3, 63.3), (.6, 64.0), (.45, 64.6), (.08, 65.2)], (ux, 0.0, uz), white, T.small + 2, smooth=True)
    sk.lathe(g, [(4.2, 62.6), (4.2, 67.6)], (0.0, 0.0, cz), white, sides, smooth=False, phase=PI / 8)
    for k in range(sides):
        a = 2 * PI * k / sides + PI / 4
        nx, nz = math.cos(a), math.sin(a)
        r = 4.2 * math.cos(PI / sides) + .03
        wx, wz = nx * r, cz + nz * r
        tx, tz = -nz, nx
        pts = [(wx - tx * .55, 63.2, wz - tz * .55), (wx + tx * .55, 63.2, wz + tz * .55)]
        kk = 5 if T.detail else 3
        for j in range(kk + 1):
            ang = PI * j / kk
            pts.append((wx + tx * .55 * math.cos(ang), 66.2 + .55 * math.sin(ang), wz + tz * .55 * math.cos(ang)))
        g.face(pts, glow(WHITE_GOLD, 1.6), (nx, 0, nz))
    sk.lathe(g, [(4.25, 67.6), (4.6, 67.85), (4.6, 68.2), (4.2, 68.3)], (0.0, 0.0, cz), black, sides, smooth=False, phase=PI / 8)
    dome = [(4.2, 68.3), (4.0, 69.4), (3.4, 70.5), (2.4, 71.5), (1.2, 72.1), (.9, 72.2)]
    sk.lathe(g, dome, (0.0, 0.0, cz), F['copper'], 16 if T.detail else 8, smooth=True)
    if T.detail:
        for k in range(8):
            a = 2 * PI * k / 8
            pts = [(math.cos(a) * r * 1.01, y, cz + math.sin(a) * r * 1.01) for (r, y) in dome]
            for p, q in zip(pts[:-1], pts[1:]):
                mk.seg(g, T, p, q, .1, .08, gilt)
    sk.lathe(g, [(.9, 72.2), (.9, 74.4)], (0.0, 0.0, cz), glow(WHITE_GOLD, 2.6), 8, smooth=False, phase=PI / 8)
    sk.lathe(g, [(1.1, 74.4), (1.1, 74.6), (.3, 75.2), (.12, 75.6)], (0.0, 0.0, cz), F['copper'], 8, smooth=False)
    mk.ellipsoid(g, (0.0, 75.8, cz), (.22, .22), glow(0xFF3A28, 3.4), 6, rings=3)
    sk.lathe(g, [(.08, 76.0), (.04, 78.4)], (0.0, 0.0, cz), gilt, 4, smooth=False)
    if T.detail:
        sub = Geo()
        n = 12
        disc = [(math.cos(2 * PI * i / n) * .45, math.sin(2 * PI * i / n) * .45, 0) for i in range(n)]
        sub.face(disc, gilt, (0, 0, -1))
        sub.face(disc[::-1], gilt, (0, 0, 1))
        for i in range(12):
            a = 2 * PI * i / 12
            pts = [(math.cos(a - .12) * .45, math.sin(a - .12) * .45, 0), (math.cos(a) * .85, math.sin(a) * .85, 0), (math.cos(a + .12) * .45, math.sin(a + .12) * .45, 0)]
            sub.face(pts, gilt, (0, 0, -1))
            sub.face(pts[::-1], gilt, (0, 0, 1))
        mk.part(g, sub, (0, 0, 0, 1), (0.0, 77.6, cz))


def walls(g, T, E):
    """The court's walls, 7 m: black stone buttressed in relief, white marble copings flush with their tops, white-gold lanterns on
    brackets along their faces over 4.4 m."""
    black, white = E['stone-black'], E['marble-white']
    sides = (('s', 'n'), ('s', 'n'), ('s', 'n'), ('w', 'e'), ('w', 'e'))
    lit = ('s', 's', 's', 'e', 'w')
    for wi, box in enumerate(WALLS):
        def face(sub, side, w, wi=wi):
            mk.wall_rect(sub, T, 0.0, w, 0.0, 7.0, -.02, black, n=-1, cell=3.0)
            bx(sub, T, 0.0, w, 0.0, .8, -.12, -.02, white, bevel=.02, skip=('+z',))
            bx(sub, T, -.02, w + .02, 6.55, 7.0, -.25, -.02, white, bevel=.02, skip=('+z',))
            k = max(1, int(w / 6.0))
            for j in range(1, k):
                u = j * w / k
                bx(sub, T, u - .4, u + .4, .8, 6.55, -.22, -.02, black, bevel=0, cell=0, skip=('+z',))
                if T.detail:
                    sub.face(((u - .12, 4.6, -.225), (u + .12, 4.6, -.225), (u + .12, 6.1, -.225), (u - .12, 6.1, -.225)), glow(WHITE_GOLD, 1.4), (0, 0, -1))
            if w > 10 and side == lit[wi]:
                kl = max(1, int(w / 12.0))
                for j in range(kl):
                    u = (j + .5) * w / kl
                    if T.detail:
                        lk.scroll_bracket(sub, T, (u, 5.4, -.02), (u, 5.4, -.95), F['iron-black'], w=.06, curl=.2)
                        lk.lantern(sub, T, u, 5.35, -.85, s=.7, glass=glow(WHITE_GOLD, 2.6), hang=0.0)
                    else:
                        sub.face(((u - .2, 4.8, -.3), (u + .2, 4.8, -.3), (u + .2, 5.4, -.3), (u - .2, 5.4, -.3)), glow(WHITE_GOLD, 2.6), (0, 0, -1))
        lk.skin(g, box, face, sides=sides[wi])
        x0, x1, y0, y1, z0, z1 = box
        if wi >= 3:
            z0, z1 = 1.5, 62.5
        mk.ground(g, T, x0, x1, z0, z1, 7.005, white, cell=0)


def _feather(s, x, y, z, f, tilt=0.0, L=1.1):
    """A stylised falling feather in relief: its vane as one flat leaf in the plane z facing -z."""
    c, d = math.cos(tilt), math.sin(tilt)

    def P(u, v):
        return (x + u * d + v * c, y - u * c + v * d, z)
    s.face([P(0, 0), P(L * .3, .16), P(L * .75, .12), P(L, 0), P(L * .75, -.1), P(L * .3, -.14)], f, (0, 0, -1))


def gateway(g, T, E):
    """The court's gateway: two rusticated black piers with white quoins, the lintel carved with falling feathers and the city's
    name, the portcullis drawn up into it (its teeth under the lintel), white-gold lanterns on the piers toward the camp."""
    black, white = E['stone-black'], E['marble-white']
    for box in PIERS:
        def face(sub, side, w):
            mk.wall_rect(sub, T, 0.0, w, 0.0, 12.5, -.02, black, n=-1, cell=3.0)
            y, k = 0.0, 0
            while y < 12.0 and T.detail and side in ('s', 'n'):
                a = .7 if k % 2 else .45
                bx(sub, T, 0.0, a, y + .03, y + .55, -.15, -.02, white, bevel=.02, skip=('+z',))
                bx(sub, T, w - a, w, y + .03, y + .55, -.15, -.02, white, bevel=.02, skip=('+z',))
                y += .6
                k += 1
            lk.cornice(sub, T, 12.5, -.02, w + .02, -.02, white, s=.8)
        lk.skin(g, box, face)
        x0, x1, y0, y1, z0, z1 = box
        mk.ground(g, T, x0, x1, z0, z1, 12.505, white, cell=0)
        xm = (x0 + x1) / 2
        lk.scroll_bracket(g, T, (xm, 8.6, z0 - .02), (xm, 8.6, z0 - 1.1), F['iron-black'], w=.08, curl=.3)
        lk.lantern(g, T, xm, 8.55, z0 - 1.0, s=1.1, glass=glow(WHITE_GOLD, 2.8), hang=.1)

    def lface(sub, side, w):
        mk.wall_rect(sub, T, 0.0, w, 10.0, 12.5, -.02, white, n=-1, cell=3.0)
        mk.text_stroke(sub, 'MERIDIAN', w / 2, 10.95, -.06, .1, black, T, style='cast', w=.08, d=.04)
        for k in range(6):
            u = .5 + k * .45 if k < 3 else w - .5 - (k - 3) * .45
            _feather(sub, u, 12.0 - (k % 3) * .25, -.05, black, tilt=(k % 3 - 1) * .4)
        bx(sub, T, 0.0, w, 10.0, 10.25, -.18, -.02, black, bevel=.01, skip=('+z',))
    lk.skin(g, LINTEL, lface, sides=('s', 'n'))
    mk.ground(g, T, -6.0, 6.0, -1.0, 2.5, 12.505, white, cell=0)
    g.face(((-6.0, 9.995, -1.0), (-6.0, 9.995, 2.5), (6.0, 9.995, 2.5), (6.0, 9.995, -1.0)), black, (0, -1, 0))
    for k in range(13):
        x = -5.7 + k * .95
        sk.lathe(g, [(.07, 9.99), (.07, 9.7), (0.0, 9.45)], (x, 0.0, .75), F['iron-black'], 4, smooth=False)


def far(T, E):
    """The court's far mesh (the far layer, to 900 m): the hall and the tower's stages as boxes, the four dials lit, the belfry's
    dark openings, the cupola, the dome, the lantern and the red light, the walls as a rim."""
    g = Geo()
    white, black = E['marble-white'], E['stone-black']

    def box(x0, x1, y0, y1, z0, z1, f):
        sk.box(g, x0, x1, y0, y1, z0, z1, f, None, 0.0, cell=0, skip=('-y',))
    x0, x1, y0, y1, z0, z1 = HALL
    box(x0, x1, y0, y1, z0, z1, white)
    box(-11.0, 11.0, 13.1, 18.6, z0 - 1.1, z0, white)
    for i in range(9):
        cx = x0 + (i + .5) * (x1 - x0) / 9
        g.face(((cx - .85, 7.0, z0 - .03), (cx + .85, 7.0, z0 - .03), (cx + .85, 11.8, z0 - .03), (cx - .85, 11.8, z0 - .03)), glow(WHITE_GOLD, 1.6), (0, 0, -1))
    tx0, tx1, ty0, ty1, tz0, tz1 = TOWER
    box(tx0, tx1, ty0, 62.0, tz0, tz1, white)
    cz = (tz0 + tz1) / 2
    for (n, c) in (((0, 0, -1), (0.0, DIAL_Y, tz0 - .05)), ((0, 0, 1), (0.0, DIAL_Y, tz1 + .05)), ((-1, 0, 0), (tx0 - .05, DIAL_Y, cz)), ((1, 0, 0), (tx1 + .05, DIAL_Y, cz))):
        mk.disc(g, c[0], c[1], c[2], DIAL_R, 12, glow(OPAL, 2.0), normal=n)
    sk.lathe(g, [(4.2, 62.0), (4.2, 67.6), (4.6, 68.2)], (0.0, 0.0, cz), white, 8, smooth=False, phase=PI / 8)
    sk.lathe(g, [(4.2, 68.3), (3.4, 70.5), (1.2, 72.1)], (0.0, 0.0, cz), F['copper'], 8, smooth=False)
    sk.lathe(g, [(.9, 72.2), (.9, 74.4), (.1, 75.6)], (0.0, 0.0, cz), glow(WHITE_GOLD, 2.6), 6, smooth=False)
    mk.ellipsoid(g, (0.0, 75.8, cz), (.5, .5), glow(0xFF3A28, 3.4), 6, rings=3)
    for b in WALLS + PIERS + [LINTEL]:
        box(*b, black)
    return g
