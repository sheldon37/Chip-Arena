"""Cyber City's art: the street and the Cut on their blocks (cybercity_art.py runs it): the street slabs with their paving,
kerbs, markings, manholes and puddles; the Cut's floor, drains and guide beam; its walls with their cable trays, junction
boxes, alcoves and exit doors; the tunnel portals and the HELIX LINE signs; the stair pits; the plaza bridge and the power
couplings. `build` returns the family it modelled, or None for a block that is not the street's."""
import math

import stagekit as sk
from stagekit import box, sweep
import cybercity_kit as ck
from cybercity_kit import F, Fr, glow, rod, h01, pick, grid_face

CELL = (1.5, 3.0, 6.0)


def build(g, S, b, T):
    s = b.stem
    if s.startswith('ground-'):
        if 'strip' in s:
            strip(g, S, b, T)
        else:
            ground(g, S, b, T)
        return 'ground'
    if s == 'trench-floor':
        trench_floor(g, S, b, T)
        return 'cut'
    if s == 'guideway':
        guideway(g, S, b, T)
        return 'cut'
    if s.startswith('trench-wall-'):
        trench_wall(g, S, b, T)
        return 'cut'
    if s.startswith('portal-'):
        portal(g, S, b, T)
        return 'portal'
    if s.startswith('pit-'):
        if '-rail' in s:
            rail(g, S, b, T)
        elif 'landing' in s:
            box(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, F['cutfloor'], T, .01, cell=0, skip=('-y', '-z', '+z'))
        else:
            pit_stair(g, S, b, T)
        return 'pit'
    if s == 'plaza-deck':
        plaza_deck(g, S, b, T)
        return 'plaza'
    if s.startswith('grid-'):
        if s.endswith('-deck'):
            grid_deck(g, S, b, T)
        elif '-pylon-' in s:
            grid_pylon(g, S, b, T)
        else:
            grid_beam(g, S, b, T)
        return 'grid'
    return None


# ---------------------------------------------------------------------------------------------------------------------------
# The street slabs
# ---------------------------------------------------------------------------------------------------------------------------
def top(g, x0, x1, z0, z1, y, f, cell):
    if x1 - x0 < 1e-4 or z1 - z0 < 1e-4:
        return
    grid_face(g, (x0, y, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, f, (0, 1, 0), cell)


def ground(g, S, b, T):
    n = 1 if b.c[2] > 0 else -1
    cell = CELL[T.i]

    def zone(za, zb, f, c=cell):
        z0, z1 = (za, zb) if n > 0 else (-zb, -za)
        top(g, b.x0, b.x1, z0, z1, b.y1, f, c)
    zone(6, 9, F['paver'])
    zone(9, 22, F['paverdark'])
    zone(22, 23, F['paver'], cell * 2)
    zone(23, 29.2, F['asphalt'])
    zone(29.2, 30, F['paver'], cell * 2)
    # the face toward the Cut (the stair pits' far wall)
    zf = b.z0 if n > 0 else b.z1
    grid_face(g, (b.x0, b.y0, zf), (b.x1 - b.x0, 0, 0), (0, b.y1 - b.y0, 0), 0, 0, F['cut'], (0, 0, -n), 4.0)
    # kerbs along the avenue
    for zk in (23.0, 29.2):
        z = n * zk
        box(g, b.x0 + 2, b.x1 - 2, b.y1 - .05, b.y1 + .02, z - .09, z + .09, F['kerb'], T, .012, cell=0, skip=('-y',))
    # a channel drain along the promenade's inner edge
    if T.detail:
        z = n * 9.0
        top(g, b.x0 + 2, b.x1 - 2, z - .07, z + .07, b.y1, F['grate'], 0)
    # the kit's plates: markings, the fell zone's border, manholes
    for p in S.parts(b):
        c, h = p['c'], p['h']
        look = p['look']
        if look == 'cyber-tile':
            continue
        x0, x1, z0, z1 = c[0] - h[0], c[0] + h[0], c[2] - h[2], c[2] + h[2]
        if look == 'plain':
            top(g, x0, x1, z0, z1, b.y1, F['paint'], 0)
        elif look == 'demolition':
            if h[0] >= h[2]:
                ck.stripes(g, (x0, b.y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), (0, 1, 0), .3, F['hazardy'], F['hazardk'])
            else:
                ck.stripes(g, (x0, b.y1, z0), (0, 0, z1 - z0), (x1 - x0, 0, 0), (0, 1, 0), .3, F['hazardy'], F['hazardk'])
        elif look == 'cyber-grate':
            manhole(g, c[0], b.y1, c[2], min(h[0], h[2]) * .95, T)
    # puddles: on the avenue and the plaza
    k = 0
    for i in range(26):
        x = -36 + 72 * h01('puddle-x', i, n)
        zz = 9.5 + 19 * h01('puddle-z', i, n)
        if 21.5 < zz < 23.5 or zz > 28.6:
            continue
        r = .5 + 1.1 * h01('puddle-r', i, n)
        if S.tops_below(x, n * zz, 30) != b.y1 or any(S.tops_below(x + dx * r, n * zz + dz * r, 30) != b.y1 for dx, dz in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            continue
        sides = (10, 8, 6)[T.i]
        pts = []
        for j in range(sides):
            a = 2 * math.pi * j / sides
            rr = r * (.7 + .5 * h01('puddle', i, j, n))
            pts.append((x + rr * math.cos(a) * 1.3, b.y1, n * zz + rr * math.sin(a) * .8))
        cen = (x, b.y1, n * zz)
        for j in range(sides):
            g.face((cen, pts[j], pts[(j + 1) % sides]), F['puddle'], (0, 1, 0))
        k += 1


def manhole(g, x, y, z, r, T):
    sides = (14, 10, 6)[T.i]
    ck.disc(g, (x, y, z), r, F['steel'], sides, (0, 1, 0))
    ck.disc(g, (x, y, z), r * .82, F['grate'], sides, (0, 1, 0))
    if T.tiny:
        for k in range(-2, 3):
            w = math.sqrt(max(0, (r * .74) ** 2 - (k * r * .26) ** 2))
            top(g, x - w, x + w, z + k * r * .26 - r * .05, z + k * r * .26 + r * .05, y, F['void'], 0)


def strip(g, S, b, T):
    """A piece of street between the stair pits: paving on top, concrete faces into the pits."""
    top(g, b.x0, b.x1, b.z0, b.z1, b.y1, F['paver'], CELL[T.i] * 2)
    for (x, nx) in ((b.x0, -1), (b.x1, 1)):
        if abs(x) < 39.9:
            grid_face(g, (x, b.y0, b.z0), (0, 0, b.z1 - b.z0), (0, b.y1 - b.y0, 0), 0, 0, F['cut'], (nx, 0, 0), 4.0)
    n = 1 if b.c[2] > 0 else -1
    zf = b.z0 if n > 0 else b.z1
    grid_face(g, (b.x0, b.y0, zf), (b.x1 - b.x0, 0, 0), (0, b.y1 - b.y0, 0), 0, 0, F['cut'], (0, 0, -n), 4.0)


# ---------------------------------------------------------------------------------------------------------------------------
# The Cut
# ---------------------------------------------------------------------------------------------------------------------------
def trench_floor(g, S, b, T):
    y = b.y1
    cell = CELL[T.i] * 1.3
    edges = [b.z0, -2.975, -2.625, -1.64, -1.56, 1.56, 1.64, 2.625, 2.975, b.z1]
    fins = [F['cutfloor'], F['grate'], F['cutfloor'], F['paint'], F['cutfloor'], F['paint'], F['cutfloor'], F['grate'], F['cutfloor']]
    for i in range(len(fins)):
        if fins[i] == F['grate']:
            # the drain: a sunk channel under a grating
            z0, z1 = edges[i], edges[i + 1]
            if T.i == 0:
                top(g, b.x0, b.x1, z0, z1, y - .06, F['void'], 0)
                for (zz, nz) in ((z0, 1), (z1, -1)):
                    g.face(((b.x0, y - .06, zz), (b.x1, y - .06, zz), (b.x1, y, zz), (b.x0, y, zz)), F['cutfloor'], (0, 0, nz))
                x = b.x0 + .1
                while x < b.x1 - .1:
                    top(g, x, x + .34, z0, z1, y, F['grate'], 0)
                    x += .5
            else:
                top(g, b.x0, b.x1, z0, z1, y, F['grate'], 0)
        else:
            top(g, b.x0, b.x1, edges[i], edges[i + 1], y, fins[i], cell if fins[i] == F['cutfloor'] else 0)


def guideway(g, S, b, T):
    """The maglev's guide beam: a chamfered beam, the two reaction rails on it, and between the kit's coils (they stay the
    kit's boxes on the beam's sides, every 2 m) the coils' brackets."""
    y0, y1 = b.y0, b.y1
    box(g, b.x0, b.x1, y0, y1, b.z0, b.z1, F['metal'], T, .03, cell=0, skip=('-y', '-x', '+x'))
    for s in (-1, 1):
        z = s * .62
        box(g, b.x0, b.x1, y1, y1 + .07, z - .11, z + .11, F['gunmetal'], T, .01, cell=0, skip=('-y', '-x', '+x'))
        box(g, b.x0, b.x1, y1 + .07, y1 + .12, z - .08, z + .08, F['steel'], T, .012, cell=0, skip=('-y', '-x', '+x'))
    # the stator's dark slot down the middle
    top(g, b.x0, b.x1, -.2, .2, y1, F['metaldark'], 0)
    if T.i < 2:
        x = b.x0 + 2
        while x < b.x1 - .5:
            for s in (-1, 1):
                zf = s * b.s[2] / 2
                box(g, x - .14, x + .14, y0 + .04, y0 + .22, min(zf, zf + s * .05), max(zf, zf + s * .05), F['gunmetal'], T, .01, cell=0)
            x += 2 if T.i == 0 else 4


def trench_wall(g, S, b, T):
    sg = -1 if b.c[2] > 0 else 1
    fr = b.face(2, sg)
    half, tall = fr.half, b.s[1]
    lintel = tall < 3
    exits = [p for p in S.driven.get(b.id, []) if p['drive'] in (9, 10)]
    # the top: the promenade's paving up to the lip's steel edge and its hazard band
    lipz = b.z0 if sg < 0 else b.z1
    zin = lipz - sg * .28
    top(g, b.x0, b.x1, min(zin, b.z1 if sg < 0 else b.z0), max(zin, b.z1 if sg < 0 else b.z0), b.y1, F['paver'], 0)
    ck.stripes(g, (b.x0, b.y1, min(lipz, zin)), (b.x1 - b.x0, 0, 0), (0, 0, abs(lipz - zin)), (0, 1, 0), .25, F['hazardy'], F['hazardk'])
    if lintel:
        # a steel lintel over an alcove (or an exit door: the kit's EXIT sign stays its boxes on a dark board)
        fr.quad(g, -half, half, 0, tall, 0, F['gunmetal'], 0)
        g.face(((b.x0, b.y0, b.z0), (b.x1, b.y0, b.z0), (b.x1, b.y0, b.z1), (b.x0, b.y0, b.z1)), F['gunmetal'], (0, -1, 0))
        if exits:
            fr.box(g, -half + .03, half - .03, .12, tall - .08, 0, .025, F['void'], T, .006)
        else:
            if T.detail:
                fr.box(g, -half, half, tall - .34, tall - .06, 0, .03, F['gunmetal'], T, .008)
            o = fr.at(-half, tall - .3, .032 if T.detail else 0)
            ck.stripes(g, o, (fr.ud[0] * 2 * half, 0, fr.ud[2] * 2 * half), (0, .2, 0), fr.n, .2, F['hazardy'], F['hazardk'])
            if T.detail:
                fr.box(g, -half, half, .16, .26, 0, .04, F['steel'], T, .008)
                if T.tiny:
                    for k in range(int(half * 2 / .4)):
                        u = -half + .2 + k * .4
                        ck.disc(g, fr.at(u, tall * .5, .002), .025, F['steel'], 5, fr.n)
        return
    # the wall: board-marked concrete between pilasters, on a plinth
    cell = (2.0, 4.0, 8.0)[T.i]
    fr.quad(g, -half, half, 0, tall, 0, F['cut'], cell)
    # the ends (into the alcoves and doors)
    for (x, nx) in ((b.x0, -1), (b.x1, 1)):
        if abs(x) < 39.9:
            g.face(((x, b.y0, b.z0), (x, b.y0, b.z1), (x, b.y1, b.z1), (x, b.y1, b.z0)), F['cut'], (nx, 0, 0))
    if T.i < 2:
        fr.box(g, -half, half, 0, .22, 0, .05, F['concretedark'], T, .015, skip=('-y',))
        np_ = max(1, int(round(half * 2 / 3.8)))
        for k in range(np_ + 1):
            u = -half + .22 + (2 * half - .44) * k / np_
            fr.box(g, u - .16, u + .16, .22, tall - .62, 0, .06, F['cut'], T, .02)
        # the cable tray with its brackets and cables
        v = tall - 1.3
        fr.box(g, -half, half, v, v + .03, 0, .16, F['gunmetal'], None, 0)
        fr.box(g, -half, half, v, v + .1, .14, .16, F['gunmetal'], None, 0)
        for k in range(int(half * 2 / 1.9) + 1):
            u = -half + .5 + k * 1.9
            if u > half - .2:
                break
            fr.box(g, u - .03, u + .03, v - .12, v, 0, .15, F['metaldark'], None, 0)
        if T.i == 0:
            for (d, r, f) in ((.04, .022, F['cable']), (.085, .016, F['copper']), (.12, .018, F['cable'])):
                rod(g, fr.at(-half, v + .03 + r, d), fr.at(half, v + .03 + r, d), r, f, 4, caps=False)
    # the kit's junction boxes, each with a conduit up to the tray
    for p in S.parts(b):
        if p['look'] != 'cyber-metal' or p['h'][0] > .4:
            continue
        u = fr.world_u(p['c'][0] - b.c[0])
        v0 = p['c'][1] - p['h'][1] - b.y0
        if T.i == 2:
            fr.box(g, u - .25, u + .25, v0, v0 + .5, 0, .06, F['metaldark'], None, 0)
            continue
        fr.box(g, u - .25, u + .25, v0, v0 + .5, 0, .09, F['metaldark'], T, .015)
        fr.box(g, u - .19, u + .19, v0 + .06, v0 + .44, .09, .1, F['gunmetal'], T, .006)
        fr.box(g, u + .1, u + .15, v0 + .2, v0 + .3, .1, .115, glow(ck.LIME, 1.4), None, 0)
        rod(g, fr.at(u - .12, v0 + .5, .045), fr.at(u - .12, tall - 1.3, .045), .022, F['steel'], 5, caps=False)


def portal(g, S, b, T):
    """A tunnel portal's headwall over the mouth: a concrete front with ribs, the hazard beam over the opening, the HELIX LINE
    sign in neon on its board, a lit strip of glass up the wall and the tunnel's light gate (the pilots' wall there)."""
    sg = -1 if b.c[0] > 0 else 1
    fr = b.face(0, sg)
    half, tall = fr.half, b.s[1]
    cell = (2.0, 4.0, 8.0)[T.i]
    slot = (-.55, .55, 4.6, tall - 1.6)
    ck.holed(g, fr, -half, half, 0, tall, [slot], F['clad5'], 0, cell, rev=.18, frev=F['trim'])
    fr.quad(g, slot[0], slot[1], slot[2], slot[3], -.18, glow(ck.mix(ck.CYAN, 0x203050, .35), .9), 0)
    if T.detail:
        n = int((slot[3] - slot[2]) / .9)
        for k in range(1, n):
            v = slot[2] + (slot[3] - slot[2]) * k / n
            fr.box(g, slot[0], slot[1], v - .03, v + .03, -.18, -.02, F['trim'], None, 0)
    # the soffit over the mouth
    g.face(((b.x0, b.y0, b.z0), (b.x1, b.y0, b.z0), (b.x1, b.y0, b.z1), (b.x0, b.y0, b.z1)), F['concretedark'], (0, -1, 0))
    # the hazard beam
    fr.box(g, -half, half, 0, .5, 0, .07, F['gunmetal'], T, .012)
    o = fr.at(-half + .05, .06, .072)
    ck.stripes(g, o, (fr.ud[0] * (2 * half - .1), 0, fr.ud[2] * (2 * half - .1)), (0, .38, 0), fr.n, .32, F['hazardy'], F['hazardk'])
    # the sign
    ck.sign_board(g, fr, -2.75, 2.75, .68, 3.45, T, d=.04)
    ck.neon_text(g, fr, 'HELIX', 0, 2.1, .16, ck.CYAN, 2.6, T, d=.11)
    ck.neon_text(g, fr, 'LINE', 0, .9, .12, ck.ICE, 2.0, T, d=.1)
    if T.detail:
        # the line's mark: a ring broken by a bar, either side of the name
        for s in (-1, 1):
            c = fr.at(s * 2.25, 2.62, .1)
            sweep(g, sk.circle_section(.035, (6, 4, 4)[T.i]), [fr.at(s * 2.25 + .26 * math.cos(a), 2.62 + .26 * math.sin(a), .1) for a in [2 * math.pi * i / 12 for i in range(13)]], glow(ck.MAGENTA, 2.2), caps=False)
            rod(g, sk._add(c, (0, -.34, 0)), sk._add(c, (0, .34, 0)), .03, glow(ck.MAGENTA, 2.2), 4)
    # ribs up the wall and a cornice
    if T.i < 2:
        for u in (-half + .25, -1.4, 1.4, half - .25):
            fr.box(g, u - .2, u + .2, 3.7, tall - .5, 0, .12, F['clad3'], T, .03)
        fr.box(g, -half, half, 3.5, 3.72, 0, .16, F['trim'], T, .02)
    fr.box(g, -half, half, tall - .5, tall, 0, .2, F['trim'], T, .03)
    ck.led(g, fr, -half + .3, half - .3, tall - .62, tall - .56, ck.VIOLET, 1.8, T, d=.14)
    # the light gate across the mouth: emitter posts on the walls' ends and thin beams between them
    xg = b.x1 - .08 if sg > 0 else b.x0 + .08
    for s in (-1, 1):
        z = s * 2.94
        box(g, xg - .07, xg + .07, -3.9, b.y0 - .1, min(z, z - s * .08), max(z, z - s * .08), F['metaldark'], T, .01, cell=0)
    for k in range(6 if T.i < 2 else 3):
        y = -3.6 + k * (.5 if T.i < 2 else 1.0)
        col = ck.VIOLET if k % 2 else ck.ICE
        box(g, xg - .012, xg + .012, y - .012, y + .012, -2.86, 2.86, glow(col, 1.3), None, 0, cell=0)


def pit_stair(g, S, b, T):
    """A step of a stair pit: its edges chamfered where the kit's climb lights sit (they stay its boxes)."""
    box(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, F['cut'], T if T.i < 2 else None, .05 if T.i < 2 else 0, cell=0, skip=('-y', '-z', '+z'))
    if T.i == 2:
        return


def rail(g, S, b, T, accent=None):
    """A parapet as a balustrade; the kit's lit cap strip becomes its glowing handrail."""
    cap = None
    for p in S.parts(b):
        if p['look'] == 'cyber-tube':
            cap = glow(p['col'], p['gain'])
    ck.glass_rail(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, T)
    if cap is not None:
        alongx = b.s[0] >= b.s[2]
        if alongx:
            box(g, b.x0 + .02, b.x1 - .02, b.y1, b.y1 + .03, b.c[2] - .035, b.c[2] + .035, cap, None, 0, cell=0, skip=('-y',))
        else:
            box(g, b.c[0] - .035, b.c[0] + .035, b.y1, b.y1 + .03, b.z0 + .02, b.z1 - .02, cap, None, 0, cell=0, skip=('-y',))


def plaza_deck(g, S, b, T):
    """The plaza's bridge over the Cut: paving between steel edge beams (the kit's edge strips ride their faces)."""
    cell = CELL[T.i]
    top(g, b.x0 + .28, b.x1 - .28, b.z0, b.z1, b.y1, F['paver'], cell)
    for s in (-1, 1):
        xe = b.x1 if s > 0 else b.x0
        xa, xb = (xe - .28, xe) if s > 0 else (xe, xe + .28)
        ck.stripes(g, (xa, b.y1, b.z0), (0, 0, b.z1 - b.z0), (xb - xa, 0, 0), (0, 1, 0), .25, F['hazardy'], F['hazardk'])
        grid_face(g, (xe, b.y0, b.z0), (0, 0, b.z1 - b.z0), (0, b.y1 - b.y0, 0), 0, 0, F['gunmetal'], (s, 0, 0), 0)
    g.face(((b.x0, b.y0, b.z0), (b.x1, b.y0, b.z0), (b.x1, b.y0, b.z1), (b.x0, b.y0, b.z1)), F['metaldark'], (0, -1, 0))


# ---------------------------------------------------------------------------------------------------------------------------
# The power couplings
# ---------------------------------------------------------------------------------------------------------------------------
def grid_deck(g, S, b, T):
    """A coupling's deck: a steel frame (the kit's grating plate, rails and stripes lie on it and stay its boxes)."""
    top(g, b.x0, b.x1, b.z0, b.z1, b.y1, F['void'], 0)
    for s in (-1, 1):
        xe = b.x1 if s > 0 else b.x0
        grid_face(g, (xe, b.y0, b.z0), (0, 0, b.z1 - b.z0), (0, b.y1 - b.y0, 0), 0, 0, F['gunmetal'], (s, 0, 0), 0)
        xa, xb = (xe - .24, xe) if s > 0 else (xe, xe + .24)
        ck.stripes(g, (xa, b.y1 + .041, b.z0 - .6), (0, 0, b.z1 - b.z0 + 1.2), (xb - xa, 0, 0), (0, 1, 0), .25, F['hazardy'], F['hazardk'])
    g.face(((b.x0, b.y0, b.z0), (b.x1, b.y0, b.z0), (b.x1, b.y0, b.z1), (b.x0, b.y0, b.z1)), F['metaldark'], (0, -1, 0))


def grid_pylon(g, S, b, T):
    """A coupling's pylon: a steel post on a striped collar, three ceramic insulator discs, a cap (the kit's lamp band stays
    its box round the top)."""
    cx, cz = b.c[0], b.c[2]
    sides = (10, 8, 6)[T.i]
    r = b.s[0] / 2
    # the base collar, striped
    box(g, b.x0 - .01, b.x1 + .01, b.y0, b.y0 + .8, b.z0 - .01, b.z1 + .01, F['gunmetal'], T, .02, cell=0, skip=('-y',))
    for (ax, sgn) in ((0, 1), (0, -1), (2, 1), (2, -1)):
        fr = b.face(ax, sgn)
        o = fr.at(-r + .03, .82, .011)
        ck.stripes(g, o, (fr.ud[0] * (2 * r - .06), 0, fr.ud[2] * (2 * r - .06)), (0, .36, 0), fr.n, .16, F['hazardy'], F['hazardk'])
        fr.quad(g, -r, r, .8, 1.2, .01, F['hazardk'], 0)
    sk.cylinder(g, (cx, b.y0 + 1.2, cz), (cx, b.y1 - .43, cz), r * .72, F['steel'], sides, caps=False)
    for i in range(3):
        y = b.y0 + 1.7 + i * .28
        prof = [(r * .75, -.05), (r * 1.3, -.035), (r * 1.32, 0.0), (r * .9, .05)]
        sk.lathe(g, prof, (cx, y, cz), F['ceramic'], sides)
    box(g, b.x0 + .03, b.x1 - .03, b.y1 - .43, b.y1 - .07, b.z0 + .03, b.z1 - .03, F['metaldark'], None, 0, cell=0)
    box(g, b.x0 - .02, b.x1 + .02, b.y1 - .075, b.y1, b.z0 - .02, b.z1 + .02, F['gunmetal'], T, .015, cell=0)


def grid_beam(g, S, b, T):
    """A coupling's bus beam between two pylons: a box girder with clamps, the HV mark in amber neon on its inner face (the
    kit's live bar under it stays its box)."""
    sg = -1 if b.c[2] > 0 else 1
    box(g, b.x0, b.x1, b.y0, b.y1, b.z0, b.z1, F['gunmetal'], T, .03, cell=0)
    fr = b.face(2, sg)
    if T.i < 2:
        for u in (-fr.half + .3, -.9, .9, fr.half - .3):
            fr.box(g, u - .06, u + .06, -.02, b.s[1] + .02, -b.s[2], .02, F['steel'], None, 0)
    ck.neon_text(g, fr, 'HV', 0, .02, .05, ck.AMBER, 2.2, T, d=.035, weight=.45)
