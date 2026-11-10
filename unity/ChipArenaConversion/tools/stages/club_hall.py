"""Substation 128's shell and what stands round it (club_art.py runs it): the floors and the LED dance floor's frame, the brick
side walls with their five arches onto the crowd galleries, the crane rail on its corbels, the south wall with the great
steel window and the neon sign, the LED wall's frame, the rooms' walls; above the play space the hall carried up in brick to
its riveted roof trusses, the purlins, the roof and its ridge lantern, the old gantry crane and the pendant lamps; outside the
bounds the crowd galleries and the sunken crowd pit (the backdrop's near sector, which the art replaces)."""
import math

import stagekit as sk
from stagekit import Geo, box, sweep, lathe, grid_face
import club_kit as ck
from club_kit import F, Plane, wall, arcade, ext, rod, rivets, ibeam, glow, rbox

ARCHES = ((2.0, 7.6), (9.6, 15.2), (17.2, 22.8), (24.8, 30.4), (32.4, 38.0))      # along z from -20
SILL, SPRING, APEX = 8.4, 10.5, 12.0
EAVES, RIDGE, CHORD = 15.4, 18.6, 14.7
TRUSS_Z = tuple(-18 + 4 * i for i in range(10))


def roof_y(x):
    return RIDGE - (RIDGE - EAVES) * abs(x) / 26.0


def floors(g, L, T, blocks):
    C = L['club']
    for i in ('club-floor-w', 'club-floor-e', 'club-floor-n', 'club-floor-s'):
        x0, x1, y0, y1, z0, z1 = ext(blocks[i])
        grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['concrete'], ck.Y, T.cell * 1.6)
    # the dance floor: black glass under the rig's lit tiles, an aluminium frame between them a hair proud
    x0, x1, z0, z1 = C['floorX0'], C['floorX1'], C['floorZ0'], C['floorZ1']
    grid_face(g, (x0, 0.0, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['glassfloor'], ck.Y, 6.0)
    w, top = .045, .019
    if T.i < 2:
        for i in range(int(C['tilesX']) + 1):
            x = x0 + i
            box(g, x - w, x + w, 0, top, z0 - w, z1 + w, F['alu'], T, bevel=.006, cell=0, skip=('-y',))
        for j in range(int(C['tilesZ']) + 1):
            z = z0 + j
            box(g, x0 - w, x1 + w, 0, top - .002, z - w, z + w, F['alu'], T, bevel=.006, cell=0, skip=('-y',))
    else:
        for (a, b, c, d) in ((x0 - w, x1 + w, z0 - w, z0 + w), (x0 - w, x1 + w, z1 - w, z1 + w), (x0 - w, x0 + w, z0, z1), (x1 - w, x1 + w, z0, z1)):
            box(g, a, b, 0, top, c, d, F['alu'], None, 0, cell=0, skip=('-y',))
    # the slabs' edges under the floor (seen from nowhere in play: one plain skirt round the bounds keeps the bank honest)


def side_walls(g, L, T):
    for sx in (-1, 1):
        pl = Plane((sx * 25.6, 0, -20.0), (0, 0, 1), (-sx, 0, 0))
        arcade(g, pl, 0, 40, 0, 12.0, ARCHES, SILL, SPRING, APEX, F['brick'], T, thick=.4, ring=F['brickdark'], stone=F['stone'], cell=(1.3, 3.0, 5.0)[T.i])
        # the gallery side of the wall (seen through the arches from the far mezzanine)
        back = Plane((sx * 26.0, 0, -20.0), (0, 0, 1), (sx, 0, 0))
        arcade(g, back, 0, 40, SILL - .6, 12.0, ARCHES, SILL, SPRING, APEX, F['brickdark'], T, thick=0.0, cell=3.0)
        # a stone impost band at the springing on each pier, a plinth course at the foot
        edges = [0.0] + [e for o in ARCHES for e in o] + [40.0]
        for i in range(0, len(edges), 2):
            pl.box(g, edges[i] + (.0 if i == 0 else -.02), edges[i + 1] + (.0 if i == len(edges) - 2 else .02), SPRING - .16, SPRING, 0, .06, F['stone'], T, bevel=.015)
        pl.box(g, .0, 40, 0, .22, 0, .05, F['stone'], T, bevel=.015, cell=4)
        # the old crane rail under the arches: an I-beam on stone corbels, riveted
        y = SILL - .32
        ibeam(g, [pl.at(.4, y, .2), pl.at(39.6, y, .2)], .46, .26, F['steel'], up=ck.Y)
        for k in range(10):
            u = 2 + 4 * k
            pl.box(g, u - .19, u + .19, y - .41, y - .23, 0, .36, F['stone'], T, bevel=.025, cell=0)
            pl.box(g, u - .15, u + .15, y - .62, y - .41, 0, .2, F['stone'], T, bevel=.025, cell=0)
        if T.tiny:
            pts = []
            for k in range(0, 98):
                u = .5 + k * .4
                pts.append(pl.at(u, y + (.1 if k % 2 else -.1), .33))
            rivets(g, pts, pl.n, .022, F['steel'])
        # the uplights at the wall's foot (the effects wash the brick from them)
        if T.detail:
            for k in range(11):
                u = 3 + k * 3.4
                c = pl.at(u, 0, .16)
                lathe(g, [(.1, 0.0), (.12, .03), (.12, .1), (.09, .13)], c, F['black'], T.small, cap1=False)
                lathe(g, [(.085, .0), (1e-4, 0.0)], sk._add(c, ck.Y, .115), glow(0xC8A0FF, 1.6), T.small)


def south_wall(g, L, T, blocks):
    x0, x1, y0, y1, z0, z1 = ext(blocks['club-wall-s'])
    pl = Plane((x0, 0, z1), (1, 0, 0), (0, 0, 1))
    W = x1 - x0
    wall(g, pl, 0, W, 0, y1, F['brick'], T, (1.3, 3.0, 5.0)[T.i])
    pl.box(g, 0, W, 0, .22, 0, .05, F['stone'], T, bevel=.015, cell=4)
    # the great window's stone sill, its riveted frame: I-section mullions, T transoms, slim glazing bars, the lintel beam
    pl.box(g, 0, W, y1 - .2, y1, -.42, .12, F['stone'], T, bevel=.03, cell=4)
    top = 12.0
    n = 12
    for i in range(n + 1):
        u = W * i / n
        ibeam(g, [pl.at(u, y1, -.2), pl.at(u, top - .3, -.2)], .26, .2, F['steel'], up=(1, 0, 0))
    for yy in (y1 + 1.5, y1 + 3.0):
        pl.box(g, 0, W, yy - .05, yy + .05, -.3, -.1, F['steel'], T, bevel=.012, cell=0)
    if T.i == 0:
        for i in range(n):
            for k in (1, 2, 3):
                u = W * (i + k / 4) / n
                pl.box(g, u - .018, u + .018, y1, top - .3, -.22, -.18, F['steeldark'], None, 0, cell=0)
            for yy in (y1 + .75, y1 + 2.25, y1 + 3.75):
                pl.box(g, W * i / n + .1, W * (i + 1) / n - .1, yy - .018, yy + .018, -.22, -.18, F['steeldark'], None, 0, cell=0)
    ibeam(g, [pl.at(0, top - .16, -.2), pl.at(W, top - .16, -.2)], .32, .4, F['steel'], up=ck.Y)
    # the sign's backing: a riveted steel panel on stand-offs; the neon tubes are the kit's strokes, round, on clips
    pl.box(g, W / 2 - 5.3, W / 2 + 5.3, 4.9, 7.1, .0, .035, F['steeldark'], T, bevel=.012, cell=1.5)
    if T.tiny:
        pts = [pl.at(W / 2 + sx * 5.2, 5.0 + k * .25, .035) for k in range(9) for sx in (-1, 1)] + [pl.at(W / 2 - 5.2 + k * .4, yy, .035) for k in range(27) for yy in (5.0, 7.0)]
        rivets(g, pts, pl.n, .02, F['steel'])
    for p in L['parts']:
        if p['f'] == 'south-wall' and ck.KIND[p['k']] is None:
            hx, hy, hz = p['h']
            c = p['c']
            f = ck.kit_fin(p, L)
            if hx > hy:
                a, b = (c[0] - hx, c[1], c[2] + .02), (c[0] + hx, c[1], c[2] + .02)
            else:
                a, b = (c[0], c[1] - hy, c[2] + .02), (c[0], c[1] + hy, c[2] + .02)
            rod(g, a, b, .032, f, T.small if T.i < 2 else 4, caps=True)
            if T.tiny:
                for q in (a, b):
                    rod(g, (q[0], q[1], c[2] - .03), (q[0], q[1], c[2] + .0), .012, F['chrome'], 4, caps=False)


def led_wall(g, L, T, blocks):
    C = L['club']
    x0, x1, y0, y1, z0, z1 = ext(blocks['club-wall-n'])
    grid_face(g, (x0, y0, z0), (x1 - x0, 0, 0), (0, y1 - y0, 0), 0, 0, F['screenoff'], (0, 0, -1), 6.0)
    box(g, x0, x1, y0, y1, z0 + .002, z1, F['steeldark'], None, 0, cell=0, skip=('-z', '-y'))
    # the cabinets' frame between the rig's panels, a hair proud of them; the surround
    W, Hn = int(C['wallW']), int(C['wallH'])
    wx, wy0, wy1 = C['wallX'], C['wallY0'], C['wallY1']
    pw, ph = 2 * wx / W, (wy1 - wy0) / Hn
    zf = z0 - .026
    if T.i < 2:
        for c in range(W + 1):
            x = -wx + c * pw
            box(g, x - .034, x + .034, wy0, wy1, zf, z0, F['black'], None, 0, cell=0, skip=('+z',))
        for r in range(Hn + 1):
            y = wy0 + r * ph
            box(g, -wx, wx, y - .034, y + .034, zf - .002, z0, F['black'], None, 0, cell=0, skip=('+z',))
    box(g, x0, x1, wy0 - .14, wy0 - .0, z0 - .07, z0, F['steeldark'], T, bevel=.012, cell=0)
    box(g, x0 - .0, x0 + .1, wy0, y1, z0 - .07, z0, F['steeldark'], T, bevel=.012, cell=0)
    box(g, x1 - .1, x1, wy0, y1, z0 - .07, z0, F['steeldark'], T, bevel=.012, cell=0)


def room_wall(g, b, T):
    """A room's wall (precast, painted): a dark dado under a moulding, a steel capping channel, a concrete plinth."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0 + .14, y1 - .08, z0, z1, F['plaster'], T, bevel=.012)
    box(g, x0 - .012, x1 + .012, y0, y0 + .14, z0 - .012, z1 + .012, F['slab'], T, bevel=.012, cell=0)
    box(g, x0 - .02, x1 + .02, y1 - .08, y1, z0 - .02, z1 + .02, F['steeldark'], T, bevel=.012, cell=0)
    if T.detail:
        along_x = (x1 - x0) > (z1 - z0)
        if along_x:
            for z in (z0 - .014, z1 + .002):
                box(g, x0, x1, y0 + .14, y0 + 1.05, z, z + .012, F['dado'], None, 0, cell=1.5)
                box(g, x0, x1, y0 + 1.05, y0 + 1.11, z - .008, z + .02, F['gunmetal'], T, bevel=.008, cell=0)
        else:
            for x in (x0 - .014, x1 + .002):
                box(g, x, x + .012, y0 + .14, y0 + 1.05, z0, z1, F['dado'], None, 0, cell=1.5)
                box(g, x - .008, x + .02, y0 + 1.05, y0 + 1.11, z0, z1, F['gunmetal'], T, bevel=.008, cell=0)


# ---------------------------------------------------------------------------------------------------------------------------
# Above the play space: the hall to its roof
# ---------------------------------------------------------------------------------------------------------------------------
def upper_hall(g, L, T):
    cell = 2.0 if T.i == 0 else 4.0
    # the side walls from the arches' heads to the eaves, a stone string course, brick piers under the trusses
    for sx in (-1, 1):
        pl = Plane((sx * 25.6, 0, -20.0), (0, 0, 1), (-sx, 0, 0))
        wall(g, pl, 0, 40, 12.0, EAVES, F['brickup'], T, cell)
        pl.box(g, 0, 40, 12.22, 12.4, 0, .07, F['stone'], T, bevel=.02, cell=4)
        for z in TRUSS_Z:
            u = z + 20
            pl.box(g, u - .3, u + .3, 12.4, CHORD - .2, 0, .14, F['brickup'], T, bevel=.012, cell=0)
            pl.box(g, u - .36, u + .36, CHORD - .2, CHORD - .02, 0, .2, F['stone'], T, bevel=.02, cell=0)
    # the gables: over the great window (south) and over the LED wall and the pit's mouths (north)
    for (z, nz, y0) in ((-19.6, 1, 12.0), (19.6, -1, 12.0)):
        steps = 13
        for i in range(-steps, steps):
            xa, xb = 26.0 * i / steps, 26.0 * (i + 1) / steps
            rows = max(1, int(round((EAVES - y0) / cell)))
            for r in range(rows):
                ya, yb = y0 + (EAVES - y0) * r / rows, y0 + (EAVES - y0) * (r + 1) / rows
                g.face(((xa, ya, z), (xb, ya, z), (xb, yb, z), (xa, yb, z)), F['brickup'], (0, 0, nz))
            g.face(((xa, EAVES, z), (xb, EAVES, z), (xb, roof_y(xb), z), (xa, roof_y(xa), z)), F['brickup'], (0, 0, nz))
        box(g, -25.6, 25.6, y0 + .22, y0 + .4, z - (.07 if nz < 0 else 0), z + (.07 if nz > 0 else 0), F['stone'], T, bevel=.02, cell=4)
    # the north wall over the pit's mouths (the pit's roof is 11.2 m up): a steel lintel and brick to the gable
    for sx in (-1, 1):
        xa, xb = sorted((sx * 10.8, sx * 25.6))
        grid_face(g, (xa, 11.3, 19.6), (xb - xa, 0, 0), (0, .7, 0), 0, 0, F['brickup'], (0, 0, -1), cell)
        ibeam(g, [(xa, 11.3, 19.7), (xb, 11.3, 19.7)], .4, .3, F['steel'], up=ck.Y)
    # the roof: two slopes of boarded deck, purlins, the ridge lantern with its night-blue panes
    for sx in (-1, 1):
        n = 8
        for i in range(n):
            xa, xb = sx * (1.7 + (26.0 - 1.7) * i / n), sx * (1.7 + (26.0 - 1.7) * (i + 1) / n)
            for k in range(10):
                za, zb = -20 + 4 * k, -16 + 4 * k
                g.face(((xa, roof_y(xa), za), (xb, roof_y(xb), za), (xb, roof_y(xb), zb), (xa, roof_y(xa), zb)), F['roof'], (0, -1, 0))
        for k in range(6):
            x = sx * (3.5 + k * 4.25)
            yy = roof_y(x) - .12
            ibeam(g, [(x, yy, -19.6), (x, yy, 19.6)], .22, .12, F['ironroof'], up=ck.Y)
    yl = roof_y(1.7)
    for sx in (-1, 1):
        grid_face(g, (sx * 1.7, yl, -20), (0, 0, 40), (0, 1.3, 0), 0, 0, glow(0x34448E, .5), (-sx, 0, 0), 8.0)
        if T.i == 0:
            for k in range(21):
                box(g, sx * 1.7 - .04, sx * 1.7 + .04, yl, yl + 1.3, -20 + 2 * k - .04, -20 + 2 * k + .04, F['ironroof'], None, 0, cell=0, skip=('-y', '+y'))
        box(g, sx * 1.7 - .06, sx * 1.7 + .06, yl - .08, yl + .06, -20, 20, F['ironroof'], None, 0, cell=0)
    grid_face(g, (-1.9, yl + 1.3, -20), (3.8, 0, 0), (0, 0, 40), 0, 0, F['roof'], (0, -1, 0), 8.0)
    for z in TRUSS_Z:
        roof_truss(g, z, T)
    # pendant lamps under the trusses: enamel shades on rods, a warm glow
    for z in TRUSS_Z:
        for x in (-16.0, 0.0, 16.0):
            if T.i < 2:
                rod(g, (x, CHORD - .15, z), (x, 13.05, z), .014, F['steeldark'], 3, caps=False)
            n = (8, 6, 4)[T.i]
            if T.i < 2:
                lathe(g, [(.03, .0), (.05, -.06), (.14, -.14), (.3, -.3), (.31, -.33)][::-1], (x, 13.05, z), F['steeldark'], n)
            lathe(g, [(.04, -.07), (.3, -.325)], (x, 13.05, z), glow(0xFFC88A, 1.1), n)
            if T.i == 0:
                ck.ball(g, (x, 13.05 - .26, z), .075, glow(0xFFE2B8, 2.6), 5, 3)
    crane(g, T)


def roof_truss(g, z, T):
    """A riveted Pratt truss: a flat bottom chord, top chords under the roof, verticals and diagonals of angle iron, gusset
    plates at the joints."""
    f = F['ironroof']
    top = lambda x: roof_y(x) - .3
    ibeam(g, [(-25.5, CHORD, z), (25.5, CHORD, z)], .3, .24, f, up=ck.Y)
    for sx in (-1, 1):
        ibeam(g, [(sx * 25.5, top(25.5), z), (0, top(0), z)], .26, .22, f, up=ck.Y)
    xs = [-21.25 + 4.25 * i for i in range(11)]
    for i, x in enumerate(xs):
        if T.i < 2:
            box(g, x - .06, x + .06, CHORD + .15, top(x) - .13, z - .05, z + .05, f, None, 0, cell=0, skip=('-y', '+y'))
        else:
            g.face(((x - .06, CHORD + .15, z), (x + .06, CHORD + .15, z), (x + .06, top(x) - .13, z), (x - .06, top(x) - .13, z)), f, (0, 0, -1 if z > 0 else 1))
        if x != 0 and T.i < 2:
            xn = x - 4.25 * (1 if x > 0 else -1)
            a, b = (x, CHORD + .15, z), (xn, top(xn) - .13, z)
            sweep(g, sk.rect_section(.1, .08), [a, b], f, up=(0, 0, 1), caps=False, smooth_u=False)
        if T.i == 0:
            box(g, x - .34, x + .34, CHORD - .18, CHORD + .22, z - .135, z + .135, f, None, 0, cell=0, skip=('-y', '+y'))


def crane(g, T):
    """The old gantry crane parked over the stage end on its rails: two riveted box girders, end trucks, the trolley, the hook."""
    f = F['crane']
    for sx in (-1, 1):
        ibeam(g, [(sx * 24.95, 12.95, -19.6), (sx * 24.95, 12.95, 19.6)], .4, .3, F['ironroof'], up=ck.Y)
        for k in range(10):
            z = -18 + 4 * k
            box(g, sx * 25.6, sx * 24.6, 12.55, 12.75, z - .12, z + .12, F['ironroof'], T, bevel=.02, cell=0)
        box(g, sx * 24.95 - .32, sx * 24.95 + .32, 13.16, 13.7, 14.6, 17.9, f, T, bevel=.03, cell=0)
        for z in (15.0, 17.5):
            rod(g, (sx * 24.95 - .2, 13.3, z), (sx * 24.95 + .2, 13.3, z), .17, F['steeldark'], T.round)
    for z in (15.5, 17.0):
        box(g, -24.7, 24.7, 13.3, 14.2, z - .3, z + .3, f, T, bevel=.03, cell=6)
        if T.i == 0:
            for k in range(25):
                x = -24 + 2 * k
                box(g, x - .04, x + .04, 13.32, 14.18, z - .315, z + .315, f, None, 0, cell=0, skip=('-y', '+y'))
    # the trolley, its drum and the hook block on two falls
    box(g, -7.1, -4.9, 14.2, 14.42, 15.2, 17.3, F['steeldark'], T, bevel=.02, cell=0)
    rod(g, (-6.8, 14.0, 16.25), (-5.2, 14.0, 16.25), .3, F['steel'], T.round)
    box(g, -5.2, -4.75, 13.75, 14.25, 15.9, 16.6, f, T, bevel=.03, cell=0)
    for dx in (-.12, .12):
        rod(g, (-6 + dx, 14.0, 16.25), (-6 + dx, 12.85, 16.25), .018, F['steeldark'], 4, caps=False)
    rbox(g, -6.28, -5.72, 12.4, 12.86, 16.1, 16.4, .08, f, 1)
    if T.detail:
        path = [(-6, 12.42, 16.25), (-6, 12.26, 16.25), (-5.9, 12.16, 16.25), (-5.84, 12.24, 16.25)]
        sk.tube(g, sk.catmull(path, 3), .035, F['steeldark'], 5)


# ---------------------------------------------------------------------------------------------------------------------------
# Outside the bounds: the crowd galleries beyond the arches, the crowd pit beyond the north barrier
# ---------------------------------------------------------------------------------------------------------------------------
def galleries(g, L, T):
    y = L['club']['galleryY']
    for sx in (-1, 1):
        inner, back = sx * 26.0, sx * 34.0
        xa, xb = sorted((inner, back))
        grid_face(g, (xa, y, -20.5), (xb - xa, 0, 0), (0, 0, 41), 0, 0, F['slab'], ck.Y, 4.0)
        pl = Plane((back, 0, -20.5), (0, 0, 1), (-sx, 0, 0))
        wall(g, pl, 0, 41, y, y + 5.4, F['brickdark'], T, 3.0)
        grid_face(g, (xa, y + 5.4, -20.5), (xb - xa, 0, 0), (0, 0, 41), 0, 0, F['roof'], (0, -1, 0), 8.0)
        for z in (-20.5, 20.5):
            grid_face(g, (xa, y, z), (xb - xa, 0, 0), (0, 5.4, 0), 0, 0, F['brickdark'], (0, 0, -1 if z > 0 else 1), 4.0)
        # the rail the crowd leans on: posts, three bars and a round cap (beyond the bounds: no cover to misread)
        rx = sx * 27.25
        for k in range(21):
            z = -20 + 2 * k
            rod(g, (rx, y, z), (rx, y + 1.08, z), .03, F['gunmetal'], (6, 4, 3)[T.i], caps=False)
        for (yy, r) in ((y + .35, .016), (y + .7, .016), (y + 1.1, .034)):
            rod(g, (rx, yy, -20.2), (rx, yy, 20.2), r, F['chrome'] if r > .02 else F['gunmetal'], (6, 4, 3)[T.i], caps=False)
        # the wall of light the crowd stands against: LED panels in steel frames, lamps over them, downlights in the ceiling
        for k in range(8):
            z = -17.5 + k * 5
            c = PARTY3[k % len(PARTY3)]
            pl.box(g, z + 20.5 - 2.3, z + 20.5 + 2.3, y + .5, y + 2.7, .02, .07, glow(c, 2.2), None, 0, cell=0)
            if T.i < 2:
                pl.box(g, z + 20.5 - 2.4, z + 20.5 + 2.4, y + .4, y + .5, 0, .1, F['steeldark'], None, 0, cell=0)
                pl.box(g, z + 20.5 - 2.4, z + 20.5 + 2.4, y + 2.7, y + 2.8, 0, .1, F['steeldark'], None, 0, cell=0)
                for dz in (-2.35, 0, 2.35):
                    pl.box(g, z + 20.5 + dz - .05, z + 20.5 + dz + .05, y + .5, y + 2.7, 0, .1, F['steeldark'], None, 0, cell=0)
        for k in range(10):
            z = -18 + k * 4
            c = ck.PARTY[k % len(ck.PARTY)]
            pl.box(g, z + 20.5 - .45, z + 20.5 + .45, y + 3.35, y + 3.85, .1, .2, glow(c, 1.8), None, 0, cell=0)
            lathe(g, [(.3, -.06), (.3, 0.0)], (sx * 29.5, y + 5.4, z), F['steeldark'], T.small)
            ck.disc(g, (sx * 29.5, y + 5.34, z), .27, glow(0xFFE0C0, 2.4), T.small + 2, (0, -1, 0))
        # the gallery's bar at each end
        for z in (-14.0, 14.0):
            box(g, sx * 31.5 - .6, sx * 31.5 + .6, y, y + 1.1, z - 2, z + 2, F['wooddarkz'], T, bevel=.03, cell=0)
            box(g, sx * 31.5 - .68, sx * 31.5 + .68, y + 1.1, y + 1.16, z - 2.08, z + 2.08, F['blackgloss'], T, bevel=.02, cell=0)


PARTY3 = (0x8B5CFF, 0x22E6FF, 0xFF3DCF, 0x3D6BFF, 0xFF6FD8, 0x3DFFD2)


def pit(g, L, T):
    yf = L['club']['pitFloor']
    for sx in (-1, 1):
        xa, xb = sorted((sx * 10.8, sx * 26.2))
        grid_face(g, (xa, yf, 20.0), (xb - xa, 0, 0), (0, 0, 11.2), 0, 0, F['slab'], ck.Y, 4.0)
        grid_face(g, (xa, yf - .01, 20.0), (xb - xa, 0, 0), (0, -.6, 0), 0, 0, F['slab'], (0, 0, -1), 6.0)
        back = Plane((xa, 0, 31.2), (1, 0, 0), (0, 0, -1))
        wall(g, back, 0, xb - xa, yf, 11.2, F['brickdark'], T, 3.0)
        grid_face(g, (sx * 26.0, yf, 20.0), (0, 0, 11.2), (0, 11.2 - yf, 0), 0, 0, F['brickdark'], (-sx, 0, 0), 4.0)
        grid_face(g, (sx * 10.8, yf, 20.0), (0, 0, 11.2), (0, 11.2 - yf, 0), 0, 0, F['black'], (sx, 0, 0), 6.0)
        grid_face(g, (xa, 11.2, 20.0), (xb - xa, 0, 0), (0, 0, 11.2), 0, 0, F['roof'], (0, -1, 0), 8.0)
        # neon on the back wall: magenta bars high, cyan uprights, a violet band the crowd stands against
        for k in range(4):
            x = sx * (10.8 + 2 + k * 3.6)
            rod(g, (x - .7, 8.6, 31.1), (x + .7, 8.6, 31.1), .05, glow(MAGENTA_, 1.6), T.small if T.i < 2 else 4)
            rod(g, (x, .9, 31.1), (x, 5.5, 31.1), .05, glow(0x22E6FF, .9), T.small if T.i < 2 else 4)
        box(g, xa + .3, xb - .3, .5, 2.7, 31.1, 31.16, glow(0x8B5CFF, 1.1), None, 0, cell=0)
        if T.i < 2:
            for k in range(8):
                x = xa + .3 + (xb - xa - .6) * k / 7
                box(g, x - .05, x + .05, .4, 2.8, 31.04, 31.2, F['steeldark'], None, 0, cell=0)


MAGENTA_ = 0xFF3DCF
