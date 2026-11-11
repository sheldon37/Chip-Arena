"""Brickfall's stage art: the models of the blocks the simulation moves or breaks (see brickfall_art.py): the hotel's wall
panels, floor slabs and corner piers, the tenements' lane facades and piers, the skybridges, the kiln chimneys, the cover
props, and each landed segment's heap of rubble. Each is in its block's own frame (its centre), built facing local +z (the
side that looks out of its building) and turned to its place by its prop's own turn, so blocks alike share one mesh.

Kinds (classify): pier (a banded brick pier), base (a chimney's banded pedestal), drum (a length of the chimney; variant 3 its
crown), bearing (a skybridge's banded steel bearing), deck, rail, slab (a hotel floor; variant 2 the roof), panel (a storey of
wall: windows where it is wide enough; a parapet where it is a metre high), barricade, pallet, sandbag, stall, heap."""
import math
import zlib

import numpy as np

import stagekit as sk
from stagekit import Geo
import brickfall_kit as bk
from brickfall_kit import F, Frame, Hole, fbox


def turn(yaw):
    """The 3 x 4 rows of a turn about y (local +z to (sin, 0, cos))."""
    c, s = round(math.cos(yaw), 9), round(math.sin(yaw), 9)
    return [c, 0.0, s, 0.0, 0.0, 1.0, 0.0, 0.0, -s, 0.0, c, 0.0]


def local_size(b, yaw):
    sx, sy, sz = b['size']
    return (sx, sy, sz) if abs(math.sin(yaw)) < .5 else (sz, sy, sx)


def hsh(s):
    return zlib.crc32(s.encode('utf-8'))


def out_yaw(b, cx, cz):
    """The turn that takes local +z to the side of a thin block that looks away from (cx, cz)."""
    sx, sy, sz = b['size']
    if sz <= sx:
        return 0.0 if b['pos'][2] >= cz else math.pi
    return math.pi / 2 if b['pos'][0] >= cx else -math.pi / 2


def classify(S, b):
    """(kind, yaw, variant) of a moving block."""
    i = b['id']
    h = hsh(i)
    if b['role'] == 'structural':
        if 'bearing' in i:
            return 'bearing', (math.pi if 'ashby' in i else 0.0), 0
        if 'stack' in i:
            return 'base', (math.pi if i.startswith('stack-n') else 0.0), 0
        return 'pier', 0.0, (0 if i.startswith('hotel') else 1)
    if 'drum' in i:
        return 'drum', (math.pi if i.startswith('stack-n') else 0.0), int(i[-1])
    if i.startswith('bridge'):
        if 'deck' in i:
            return 'deck', (0.0 if 'ashby' in i else math.pi), 0
        return 'rail', (math.pi / 2 if b['pos'][0] > (-7.0 if 'ashby' in i else 7.0) else -math.pi / 2), 0
    if 'slab' in i:
        q = i.split('-')[1]
        return 'slab', {'nw': 0.0, 'se': math.pi, 'ne': 0.0, 'sw': math.pi}[q], int(i[-1]) + (10 if q in ('ne', 'sw') else 0)
    if b['role'] == 'masonry':
        if i.startswith('hotel'):
            yaw = out_yaw(b, 0.0, 0.0)
            w = local_size(b, yaw)[0]
            sign = 9 if i == S.sign else h % 3
            return 'panel', yaw, (0 if b['size'][1] < 1.5 or w < 2.4 else sign)
        yaw = 0.0 if i.startswith('ashby') else math.pi
        return 'lane', yaw, h % 2
    for k in ('barricade', 'pallet', 'sandbag', 'stall'):
        if k in i:
            yaw = (0.0 if '~m' not in i else math.pi) + (math.pi / 2 if k == 'stall' and b['size'][2] > b['size'][0] else 0)
            return k, yaw, (h % 3 if k in ('stall', 'pallet') else 0)
    return 'plain', 0.0, 0


# ---------------------------------------------------------------------------------------------------------------------------
# Members
# ---------------------------------------------------------------------------------------------------------------------------
def pier(g, T, hx, hy, hz, f, band=True, trim=None):
    """A brick pier on a stone base under a stone cap, its shaft's faces sunk between its corners; with the band."""
    trim = F['stone'] if trim is None else trim
    y0, y1 = -hy, hy
    sk.box(g, -hx - .04, hx + .04, y0, y0 + .5, -hz - .04, hz + .04, trim, T, .03, cell=0, skip=('-y',))
    sk.box(g, -hx, hx, y0 + .5, y1 - .34, -hz, hz, f, T, .025, cell=(.9, 0, 0)[T.i], skip=('-y', '+y'))
    sk.box(g, -hx - .05, hx + .05, y1 - .34, y1 - .12, -hz - .05, hz + .05, trim, T, .03, cell=0, skip=('-y',) if not T.detail else ())
    sk.box(g, -hx, hx, y1 - .12, y1, -hz, hz, trim, T, .02, cell=0, skip=('-y',))
    if band:
        bk.band(g, T, hx, hz, y0)


def base(g, T, hx, hy, hz):
    """A kiln chimney's pedestal: a stone plinth, brick, a stone cornice, the band."""
    y0, y1 = -hy, hy
    tr = F['stone-dark']
    sk.box(g, -hx, hx, y0, y0 + .45, -hz, hz, tr, T, .04, cell=0, skip=('-y',))
    sk.box(g, -hx + .05, hx - .05, y0 + .45, y1 - .3, -hz + .05, hz - .05, F['brick'], T, .02, cell=(1.2, 0, 0)[T.i], skip=('-y', '+y'))
    sk.box(g, -hx, hx, y1 - .3, y1, -hz, hz, tr, T, .05, cell=0)
    bk.band(g, T, hx - .05, hz - .05, y0)


def octagon(hx, hz, c):
    return [(-hx + c, -hz), (hx - c, -hz), (hx, -hz + c), (hx, hz - c), (hx - c, hz), (-hx + c, hz), (-hx, hz - c), (-hx, -hz + c)]


def drum(g, T, hx, hy, hz, k):
    """A length of the kiln chimney: eight brick sides, iron straps, a string; the fourth is the crown (oversailing courses,
    a stone rim, the flue's dark mouth)."""
    f = F['brick'] if k % 2 == 0 else F['brick-dark']
    c = .62

    def ring(y0, y1, inset, fin, cells=1):
        o = octagon(hx - inset, hz - inset, c - inset * .4)
        for j in range(cells):
            ya, yb = y0 + (y1 - y0) * j / cells, y0 + (y1 - y0) * (j + 1) / cells
            for a in range(8):
                p, q = o[a], o[(a + 1) % 8]
                nn = (q[1] - p[1], 0.0, -(q[0] - p[0]))
                g.face(((p[0], ya, p[1]), (q[0], ya, q[1]), (q[0], yb, q[1]), (p[0], yb, p[1])), fin, (-nn[0], 0, -nn[2]) if (p[0] + q[0]) * -nn[0] + (p[1] + q[1]) * -nn[2] > 0 else nn)

    def cap(y, inset, fin, up):
        o = octagon(hx - inset, hz - inset, c - inset * .4)
        g.face([(p[0], y, p[1]) for p in o], fin, (0, 1 if up else -1, 0))
    top = hy if k < 3 else hy - .55
    ring(-hy, top, .06, f, (4, 2, 1)[T.i])
    cap(-hy, .06, f, False)
    if k < 3:
        cap(hy, .06, f, True)
    # straps and a string course
    for y in ((-hy + .5, hy - .5) if T.detail else ()):
        ring(y - .05, y + .05, .035, F['iron'])
        cap(y + .05, .035, F['iron'], True)
        cap(y - .05, .035, F['iron'], False)
    if k == 0:
        ring(-hy, -hy + .22, 0.0, F['stone-dark'])
        cap(-hy + .22, 0.0, F['stone-dark'], True)
    if k == 3:
        # the crown: three oversailing courses, a stone rim, the mouth
        y = hy - .55
        for (dy, ins, fin) in ((.14, .03, f), (.14, .0, f), (.12, -.03, F['brick-dark'])):
            ring(y, y + dy, ins, fin)
            cap(y, ins, fin, False)
            cap(y + dy, ins, fin, True)
            y += dy
        ring(y, hy, -.05, F['stone-dark'])
        cap(y, -.05, F['stone-dark'], False)
        # the rim's top and the flue
        o = octagon(hx + .05, hz + .05, c + .02)
        i_ = octagon(hx - .4, hz - .4, c - .25)
        for a in range(8):
            g.face(((o[a][0], hy, o[a][1]), (o[(a + 1) % 8][0], hy, o[(a + 1) % 8][1]), (i_[(a + 1) % 8][0], hy, i_[(a + 1) % 8][1]), (i_[a][0], hy, i_[a][1])), F['stone-dark'], (0, 1, 0))
            p, q = i_[a], i_[(a + 1) % 8]
            g.face(((p[0], hy, p[1]), (q[0], hy, q[1]), (q[0], hy - .5, q[1]), (p[0], hy - .5, p[1])), F['brick-soot'], (-(p[0] + q[0]), 0, -(p[1] + q[1])))
        g.face([(p[0], hy - .5, p[1]) for p in i_], F['black'], (0, 1, 0))
        if T.detail:
            # a ladder's rungs up the yard side and a lightning rod
            sk.cylinder(g, (hx - .5, hy - .3, hz - .5), (hx - .5, hy + 1.1, hz - .5), .015, F['iron'], 5)
    if T.detail:
        for j in range(8):
            y = -hy + .25 + j * .42
            if y > top - .1:
                break
            sk.cylinder(g, (-.2, y, hz - .01), (-.2, y, hz + .1), .012, F['iron'], 4, caps=False)
            sk.cylinder(g, (.2, y, hz - .01), (.2, y, hz + .1), .012, F['iron'], 4, caps=False)
            sk.cylinder(g, (-.2, y, hz + .1), (.2, y, hz + .1), .012, F['iron'], 4, caps=False)


def bearing(g, T, hx, hy, hz):
    """A skybridge's bearing: a riveted steel bracket with gussets on the building's side, banded (local +z: the bridge)."""
    f = F['green']
    sk.box(g, -hx, hx, -hy, hy, -hz, hz, f, T, .02, cell=0)
    fr = Frame((-hx, -hy, hz), (1, 0, 0))
    if T.detail:
        for x in (.12, 2 * hx - .12):
            fbox(g, T, fr, x - .05, x + .05, .02, 2 * hy - .02, 0, .035, f, .008, skip=('-z',))
        bk.rivets(g, T, fr, [(x, y) for x in (.3, .5, .7, 1.3, 1.5, 1.7) for y in (.1, 2 * hy - .1)], 0.0)
    # the band's strap round its middle with the lamp on each long face (a short member: BandFit's centred band)
    lo, hi = -.2, .2
    for (nx, nz, half, reach) in ((0, 1, hx, hz), (0, -1, hx, hz), (1, 0, hz, hx), (-1, 0, hz, hx)):
        u = (nz, 0.0, -nx)
        fb = Frame((nx * reach - u[0] * half, 0.0, nz * reach - u[2] * half), u)
        w = 2 * half
        fbox(g, T, fb, -.01, w + .01, lo, hi, 0, .02, F['black'], .005, skip=('-z',))
        bk.chevrons(g, fb, 0, w, lo + .04, hi - .04, .024, F['yellow'], .22)
        sk.cylinder(g, fb.P(w / 2, 0, .018), fb.P(w / 2, 0, .055), .075, F['iron'], T.round)
        sk.lathe(g, [(.055, 0.0), (.05, .02), (.032, .04), (0.0, .05)], fb.P(w / 2, 0, .055), F['lamp'], T.round, axis=fb.n)


def deck(g, T, hx, hy, hz):
    """A skybridge's deck: steel chequer plate on two riveted girders and cross beams (the girders hang under it)."""
    f = F['green']
    sk.box(g, -hx, hx, -hy + .1, hy, -hz, hz, F['steel'], T, .012, cell=(1.0, 0, 0)[T.i])
    for s in (-1, 1):
        x = s * (hx - .1)
        sk.box(g, x - .09, x + .09, -hy - .22, -hy + .1, -hz, hz, f, T, .012, cell=0, skip=('+y',))
        sk.box(g, x - .14, x + .14, -hy - .26, -hy - .22, -hz, hz, f, T, .008, cell=0)
    if T.detail:
        n = 5
        for i in range(n):
            z = -hz + 2 * hz * (i + .5) / n
            sk.box(g, -hx + .2, hx - .2, -hy - .14, -hy + .1, z - .05, z + .05, f, T, .008, cell=0, skip=('+y',))
        # tread strips across the plate
        for i in range(12):
            z = -hz + 2 * hz * (i + .5) / 12
            sk.box(g, -hx + .12, hx - .12, hy, hy + .008, z - .03, z + .03, F['iron'], None, 0, cell=0, skip=('-y',))


def rail(g, T, hx, hy, hz):
    """A skybridge's parapet (local size: 6 long in x, .15 thick): a riveted plate girder: flanges, stiffeners, a hand rail."""
    f = F['green']
    t = hz
    sk.box(g, -hx, hx, -hy, hy - .06, -t * .45, t * .45, f, T, .008, cell=(1.0, 0, 0)[T.i])
    sk.box(g, -hx, hx, -hy, -hy + .07, -t, t, f, T, .01, cell=0, skip=('-y',))
    sk.box(g, -hx, hx, hy - .08, hy, -t, t, F['iron'], T, .02, cell=0)
    n = 6
    for i in range(n + 1):
        x = -hx + 2 * hx * i / n
        x = min(max(x, -hx + .03), hx - .03)
        sk.box(g, x - .03, x + .03, -hy + .07, hy - .08, -t, t, f, T, .006, cell=0, skip=('-y', '+y'))
    if T.detail:
        for s in (-1, 1):
            fr = Frame((-hx if s > 0 else hx, -hy, s * t * .45), (s, 0, 0))
            bk.rivets(g, T, fr, [(.25 + .5 * i, y) for i in range(12) for y in (.14, 2 * hy - .16)], 0.0, r=.012)


def slab(g, T, hx, hy, hz, k):
    """A hotel floor: a concrete slab, boards over it (the roof: felt and a gravel bed), a plastered soffit on joists."""
    roof_ = k % 10 == 2
    top = F['tar'] if roof_ else (F['floorboard-x'] if k < 10 else F['floorboard-z'])
    cell = (1.0, 3.0, 6.0)[T.i]
    sk.box(g, -hx, hx, -hy, hy - .03, -hz, hz, F['concrete'], T, .015, cell=cell, skip=('+y',))
    sk.box(g, -hx, hx, hy - .03, hy, -hz, hz, top, T, .01, cell=cell, skip=('-y',))
    if T.detail:
        # joists under it, a hand's breadth deep
        n = 5
        for i in range(n):
            x = -hx + 2 * hx * (i + .5) / n
            sk.box(g, x - .07, x + .07, -hy - .1, -hy, -hz + .15, hz - .15, F['timber-dark-z'], None, 0, cell=0, skip=('+y',))
        if roof_:
            # a skylight curb and a vent on the roof: low, so the roof still walks flat
            sk.box(g, -hx + 1.2, -hx + 2.6, hy, hy + .05, -hz + 1.3, -hz + 2.5, F['iron'], T, .012, cell=0, skip=('-y',))
            sk.box(g, -hx + 1.3, -hx + 2.5, hy + .05, hy + .06, -hz + 1.4, -hz + 2.4, F['glass-pale'], None, 0, cell=0, skip=('-y',))


def blind(w, h, lit, seed):
    """The windows of a storey panel w wide and h high: holes in the panel's outer face (shallow: the wall is solid)."""
    r = bk.rng(seed)
    n = 1 if w < 4.4 else 2
    ww = min(1.25, w - 1.0)
    out = []
    for i in range(n):
        x = w * (i + .5) / n
        fill = 'lit' if lit == 1 else 'board' if lit == 2 else 'glass'
        if n > 1 and i == 1 and lit == 1:
            fill = 'glass'
        out.append(Hole(x - ww / 2, x + ww / 2, .95, 2.45, .2, fill=fill, depth=.09, head='arch', lit=F['lit'] if r.random() < .6 else F['lit-dim'], bars=(2, 3)))
    return out


def panel(g, T, hx, hy, hz, v, f, inner, trim, lane=False):
    """A storey of wall (local x along it, +z the street side): brick outside with its windows, their sills and arches, a
    stone band under the floor above; plaster and paper inside with a skirting and the windows' recesses; a metre-high one is
    a parapet under a stone coping."""
    w, h, t = 2 * hx, 2 * hy, 2 * hz
    fo = Frame((-hx, -hy, hz), (1, 0, 0))            # the street face
    fi = Frame((hx, -hy, -hz), (-1, 0, 0))           # the room's
    parapet = h < 1.5
    holes = [] if (parapet or w < 2.4) and not lane else blind(w, h, v, int(w * 100) + v)
    if lane and not parapet:
        holes = [Hole(w / 2 - .36, w / 2 + .36, 1.0, 2.35, .36, fill=('glass', 'board')[v % 2], depth=.09, head='arch', bars=(1, 2))]
    bk.wallface(g, T, fo, w, h, f, holes)
    style = {'trim': trim, 'ring': F['brick-dark'] if f != F['brick-dark'] else F['brick'], 'key': False}
    for hl in holes:
        bk.opening(g, T, fo, hl, f, style=style)
    # inside
    if parapet:
        bk.wallface(g, T, fi, w, h, f)
    else:
        ih = [Hole(w - hl.u1, w - hl.u0, hl.v0, hl.v1, hl.rise, fill=('glass' if hl.fill == 'lit' else hl.fill), depth=.05, sill=False, head='none') for hl in holes]
        bk.wallface(g, T, fi, w, h, inner, ih)
        for hl in ih:
            bk.opening(g, T, fi, hl, inner, style={'trim': None})
        if T.detail:
            bk.mould(g, T, fi, 0, w, 0.0, [(0.0, 0.0), (.02, 0.0), (.02, .14), (0.0, .16)], F['timber-dark-x'], ends=False)
    # ends, top and foot
    for (x, nx) in ((-hx, -1), (hx, 1)):
        g.face(((x, -hy, -hz), (x, -hy, hz), (x, hy, hz), (x, hy, -hz)), f, (nx, 0, 0))
    g.face(((-hx, -hy, -hz), (hx, -hy, -hz), (hx, -hy, hz), (-hx, -hy, hz)), f, (0, -1, 0))
    if parapet:
        sk.box(g, -hx, hx, hy - .1, hy, -hz - .04, hz + .04, trim, T, .02, cell=0)
        if w >= 2.0 and T.detail:
            fbox(g, T, fo, .3, w - .3, .2, h - .3, -.001, .03, f, .012, skip=('-z',))
    else:
        g.face(((-hx, hy, -hz), (hx, hy, -hz), (hx, hy, hz), (-hx, hy, hz)), f, (0, 1, 0))
        # the stone band at the floor line over it, and quoin blocks on a narrow one
        bk.mould(g, T, fo, 0, w, h - .2, [(0.0, 0.0), (.035, .02), (.035, .18), (0.0, .2)], trim, ends=True)
        if w < 2.4 and not lane and T.detail:
            y, j = .3, 0
            while y < h - .7:
                a = .08 if j % 2 else 0
                fbox(g, T, fo, a, w - a, y, y + .34, -.001, .03, trim, .012, skip=('-z',))
                y += .68
                j += 1


def sign(g, T, hx, hy, hz):
    """The hotel's blade sign on its panel (the look's blue sign light is beside it): MERCER down a steel blade, neon tubes."""
    fo = Frame((-hx, -hy, hz), (1, 0, 0))
    x = .7
    y0, y1 = .3, 2 * hy - .15
    sk.box(g, x - hx - .07, x - hx + .07, -hy + y0, -hy + y1, hz + .12, hz + .82, F['iron'], T, .02, cell=0)
    for y in (y0 + .2, y1 - .2):
        sk.cylinder(g, (x - hx, -hy + y, hz), (x - hx, -hy + y, hz + .14), .025, F['iron'], T.small, caps=False)
    for s in (-1, 1):
        fs = Frame((x - hx + s * .072, -hy + y0, hz + (.12 if s < 0 else .82)), (0, 0, 1) if s < 0 else (0, 0, -1))
        # each face reads down the blade
        n = len('MERCER')
        ph = (y1 - y0 - .2) / n
        for k, ch in enumerate('MERCER'):
            bk.text(g, fs, .35, (y1 - y0) - .1 - (k + 1) * ph + .05, min(.07, ph / 8.5), ch, F['neon'], .004)
        if T.detail:
            for (a, b) in ((.05, .65),):
                g.face((fs.P(a, .04, .003), fs.P(b, .04, .003), fs.P(b, .07, .003), fs.P(a, .07, .003)), F['coral'], fs.n)
                g.face((fs.P(a, y1 - y0 - .07, .003), fs.P(b, y1 - y0 - .07, .003), fs.P(b, y1 - y0 - .04, .003), fs.P(a, y1 - y0 - .04, .003)), F['coral'], fs.n)


# ---------------------------------------------------------------------------------------------------------------------------
# Cover props
# ---------------------------------------------------------------------------------------------------------------------------
def barricade(g, T, hx, hy, hz):
    """A crib of heavy sleepers, a striped rail across its front, a road lamp on its corner."""
    r = bk.rng(5)
    rows = 5
    th = 2 * hy / rows
    for j in range(rows):
        y0 = -hy + j * th
        # four sleepers a layer, laid tight (it is cover: nothing shows through), each layer across the one under it
        for k in range(4):
            a, b = -1 + k * .5, -.5 + k * .5
            e = r.uniform(0, .05)
            if j % 2 == 0:
                sk.box(g, -hx + e, hx - r.uniform(0, .05), y0, y0 + th, a * hz, b * hz, F['timber-dark-x'], T, .03, cell=0, skip=('-y',) if j == 0 else ())
            else:
                sk.box(g, a * hx, b * hx, y0, y0 + th, -hz + e, hz - r.uniform(0, .05), F['timber-dark-z'], T, .03, cell=0)
    fr = Frame((-hx, -hy, hz), (1, 0, 0))
    fbox(g, T, fr, 0, 2 * hx, .62, .9, 0, .03, F['white-paint'], .008, skip=('-z',))
    bk.chevrons(g, fr, .02, 2 * hx - .02, .64, .88, .034, F['danger'], .36)


def pallet(g, T, hx, hy, hz, v):
    """A pallet of new bricks under bands (a third of them sheeted)."""
    y0 = -hy
    for z in (-hz + .06, -.05, hz - .16):
        sk.box(g, -hx, hx, y0, y0 + .1, z, z + .1, F['timber-z'] if False else F['timber'], T, .01, cell=0, skip=('-y',))
    n = 7
    for i in range(n):
        x = -hx + 2 * hx * i / n
        sk.box(g, x + .01, x + 2 * hx / n - .02, y0 + .1, y0 + .14, -hz, hz, F['timber-z'], T, .008, cell=0)
    f = (F['brick-rose'], F['brick'], F['brick-buff'])[v % 3]
    # the stack: four packs of bricks with a finger's gap between
    yb = y0 + .14
    for (a, b) in ((-hx + .03, -.02), (.02, hx - .03)):
        for (c, d) in ((-hz + .03, -.02), (.02, hz - .03)):
            sk.box(g, a, b, yb, hy, c, d, f, T, .012, cell=(.6, 0, 0)[T.i], skip=('-y',))
    for z in ((-hz * .5, hz * .5) if T.detail else ()):
        sk.box(g, -hx + .025, hx - .025, yb, hy + .004, z - .02, z + .02, F['steel'], None, 0, cell=0, skip=('-y',))
    if v == 2:
        # a tarpaulin over a corner
        sk.box(g, -hx + .0, .3, hy - .5, hy + .012, -hz + .0, .3, F['tarp'], T, .03, cell=0, skip=('-y',))


def sandbag(g, T, hx, hy, hz):
    """A wall of filled bags in bond."""
    rows = 5
    th = 2 * hy / rows
    r = bk.rng(9)
    per = (3, 3, 2)[T.i]
    for j in range(rows):
        y0 = -hy + j * th
        off = (j % 2) * .5
        for d in range(2):
            z0, z1 = (-hz, 0.0) if d == 0 else (0.0, hz)
            k = -1
            while True:
                a = -hx + (k + off) * (2 * hx / per)
                b = a + 2 * hx / per
                k += 1
                if a >= hx - .02:
                    break
                a, b = max(a, -hx), min(b, hx)
                if b - a < .1:
                    continue
                s = r.uniform(-.012, .012)
                sk.box(g, a + .008, b - .008, y0 + .004, y0 + th - .004 + s, z0 + .008, z1 - .008, F['burlap'], T, .055 if T.bevel else 0, cell=0, skip=('-y',) if j == 0 else ())


def stall(g, T, hx, hy, hz, v):
    """A market stall's counter: trestles, a board top, crates of goods, a canvas skirt on its front."""
    y0 = -hy
    cv = (F['canvas-red'], F['canvas-green'], F['canvas-cream'])[v % 3]
    # legs and top
    sk.box(g, -hx, hx, hy - .5, hy - .44, -hz, hz, F['timber'], T, .012, cell=0)
    for x in (-hx + .06, hx - .14):
        for z in (-hz + .06, hz - .14):
            sk.box(g, x, x + .08, y0, hy - .5, z, z + .08, F['timber-y'], T, .008, cell=0, skip=('-y', '+y'))
    # the skirt round it (cover reads as cover): canvas in shallow folds on the long sides, boards on the ends
    for (zz, nz) in ((hz, 1), (-hz, -1)):
        n = (12, 6, 2)[T.i]
        for i in range(n):
            a, b = -hx + 2 * hx * i / n, -hx + 2 * hx * (i + 1) / n
            da, db = (.0 if i % 2 == 0 else -.03), (-.03 if i % 2 == 0 else .0)
            g.face(((a, y0 + .04, zz + nz * da), (b, y0 + .04, zz + nz * db), (b, hy - .5, zz + nz * db), (a, hy - .5, zz + nz * da)), cv, (0, 0, nz))
    for (xx, nx) in ((hx, 1), (-hx, -1)):
        g.face(((xx, y0, -hz), (xx, y0, hz), (xx, hy - .5, hz), (xx, hy - .5, -hz)), F['ply'], (nx, 0, 0))
    # crates on top, to the block's top
    r = bk.rng(31 + v)
    goods = (F['goods-a'], F['goods-b'], F['canvas-green'], F['goods-c'], F['rust'])
    n = 4
    for i in range(n):
        a, b = -hx + 2 * hx * i / n + .03, -hx + 2 * hx * (i + 1) / n - .03
        for (c, d) in ((-hz + .03, -.02), (.02, hz - .03)):
            sk.box(g, a, b, hy - .44, hy - .02, c, d, F['timber-z'], T, .012, cell=0, skip=('-y', '+y'))
            sk.box(g, a + .03, b - .03, hy - .1, hy - .0, c + .03, d - .03, goods[r.randrange(len(goods))], T, .012, cell=0, skip=('-y',))


# ---------------------------------------------------------------------------------------------------------------------------
# Heaps: a landed segment's rubble
# ---------------------------------------------------------------------------------------------------------------------------
def heap(g, T, boxes, origin, seed, fins, extras='brick'):
    """A heap of rubble over the rubble blocks `boxes` [(x0, x1, top, z0, z1)] (world), in the frame of `origin`: a broken
    surface that follows the blocks' tops (so feet stand on it where they stand on them), skirted out to the ground, strewn
    with loose bricks, slab shards, joists and bent bars."""
    r = bk.rng(seed)
    cell = (.3, .6, 1.2)[T.i]
    x0 = min(b[0] for b in boxes) - .5
    x1 = max(b[1] for b in boxes) + .5
    z0 = min(b[3] for b in boxes) - .5
    z1 = max(b[4] for b in boxes) + .5
    nx, nz = int(math.ceil((x1 - x0) / cell)), int(math.ceil((z1 - z0) / cell))
    X = x0 + np.arange(nx + 1) * cell
    Z = z0 + np.arange(nz + 1) * cell
    XX, ZZ = np.meshgrid(X, Z, indexing='ij')
    Hh = np.zeros_like(XX)
    # the blocks' tops, each eased out over a skirt
    for (a, b, top, c, d) in boxes:
        dx = np.maximum(np.maximum(a - XX, XX - b), 0)
        dz = np.maximum(np.maximum(c - ZZ, ZZ - d), 0)
        dist = np.sqrt(dx * dx + dz * dz)
        skirt = .35 + .12 * top
        Hh = np.maximum(Hh, top * np.clip(1 - dist / skirt, 0, 1) ** 1.5)
    # soften the steps between neighbouring tops, then break the surface up
    for _ in range(1 if T.i < 2 else 0):
        K = Hh.copy()
        K[1:-1, 1:-1] = (Hh[1:-1, 1:-1] * 2 + Hh[:-2, 1:-1] + Hh[2:, 1:-1] + Hh[1:-1, :-2] + Hh[1:-1, 2:]) / 6
        Hh = K
    P = np.stack([XX.ravel(), np.zeros(XX.size), ZZ.ravel()], axis=1)
    nse = (sk.fbm(P, 1.7, 3, seed) - .5) * .22 + (sk.vnoise(P, 4.5, seed + 5) - .5) * .1
    Hh = np.where(Hh > .03, np.maximum(Hh + nse.reshape(Hh.shape) * np.clip(Hh * 3, 0, 1), .0), 0.0)
    Hh[0, :] = Hh[-1, :] = 0
    Hh[:, 0] = Hh[:, -1] = 0
    # normals from the height field
    gx = np.zeros_like(Hh)
    gz = np.zeros_like(Hh)
    gx[1:-1, :] = (Hh[2:, :] - Hh[:-2, :]) / (2 * cell)
    gz[:, 1:-1] = (Hh[:, 2:] - Hh[:, :-2]) / (2 * cell)
    ox, oy, oz = origin
    pick = sk.vnoise(P, .9, seed + 9).reshape(Hh.shape)
    for i in range(nx):
        for j in range(nz):
            hs = (Hh[i, j], Hh[i + 1, j], Hh[i + 1, j + 1], Hh[i, j + 1])
            if max(hs) <= 0.001:
                continue
            pts, nrm = [], []
            for (a, b) in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)):
                pts.append((X[a] - ox, Hh[a, b] - oy, Z[b] - oz))
                nrm.append(sk._norm((-gx[a, b], 1.0, -gz[a, b])))
            f = fins[min(len(fins) - 1, int(pick[i, j] * len(fins)))]
            g.face((pts[0], pts[1], pts[2]), f, normals=(nrm[0], nrm[1], nrm[2]))
            g.face((pts[0], pts[2], pts[3]), f, normals=(nrm[0], nrm[2], nrm[3]))

    def height(x, z):
        i = min(max(int((x - x0) / cell), 0), nx - 1)
        j = min(max(int((z - z0) / cell), 0), nz - 1)
        return float(Hh[i, j])
    # the strew
    area = (x1 - x0) * (z1 - z0)
    count = int(area * (1.6, .5, .0)[T.i])
    loose = fins + [F['brick-fresh'], F['mortar']]
    for _ in range(count):
        x, z = r.uniform(x0 + .2, x1 - .2), r.uniform(z0 + .2, z1 - .2)
        h = height(x, z)
        if h < .05 and r.random() < .6:
            continue
        q = bk.rand_quat(r)
        k = r.random()
        if k < .7:
            s = r.uniform(.8, 1.5)
            bk.brickbat(g, T, (x - ox, h + .03 - oy, z - oz), (.215 * s, .07 * s, .105 * s), q, loose[r.randrange(len(loose))])
        elif k < .9:
            s = r.uniform(.25, .6)
            bk.brickbat(g, T, (x - ox, h + .05 - oy, z - oz), (s, .09, s * r.uniform(.5, 1)), sk.quat_axis(r.uniform(-1, 1), .2, r.uniform(-1, 1), r.uniform(.1, .5)), F['concrete'] if extras != 'steel' else F['green'])
        else:
            s = r.uniform(.7, 1.6)
            bk.brickbat(g, T, (x - ox, h + .06 - oy, z - oz), (s, .08, .12), sk.quat_axis(r.uniform(-.3, .3), 1, r.uniform(-.3, .3), r.uniform(0, 6.28)), F['timber-dark-x'] if extras != 'steel' else F['green'])
    if T.i == 0:
        for _ in range(max(2, int(area * .12))):
            x, z = r.uniform(x0 + .5, x1 - .5), r.uniform(z0 + .5, z1 - .5)
            h = height(x, z)
            if h < .2:
                continue
            p0 = (x - ox, h - .1 - oy, z - oz)
            p1 = (p0[0] + r.uniform(-.3, .3), p0[1] + r.uniform(.3, .6), p0[2] + r.uniform(-.3, .3))
            p2 = (p1[0] + r.uniform(-.35, .35), p1[1] + r.uniform(-.05, .3), p1[2] + r.uniform(-.35, .35))
            sk.tube(g, sk.catmull([p0, p1, p2], per=3), .012, F['rust'], 4)


def heaps(S, T):
    """[(rubble block id, geo in that block's frame, its place in the world)]: each segment's heap."""
    out = []
    for s in S.segments:
        rb = s['rubble']
        o = tuple(rb[0]['pos'])
        boxes = [(b['pos'][0] - b['size'][0] / 2, b['pos'][0] + b['size'][0] / 2, b['pos'][1] + b['size'][1] / 2, b['pos'][2] - b['size'][2] / 2, b['pos'][2] + b['size'][2] / 2) for b in rb]
        g = Geo()
        sid = s['id']
        if sid.startswith('hotel'):
            fins = [F['brick-buff'], F['brick-buff'], F['rubble'], F['concrete'], F['dust']]
            ex = 'brick'
        elif sid.startswith('bridge'):
            fins = [F['green'], F['rust'], F['green'], F['iron']]
            ex = 'steel'
        else:
            fins = [F['brick'], F['brick-dark'], F['rubble'], F['brick'], F['dust']]
            ex = 'brick'
        heap(g, T, boxes, o, hsh(sid) % 1000, fins, ex)
        out.append((s['heap'], g, o))
    return out


# ---------------------------------------------------------------------------------------------------------------------------
def build(S, T, kind, size, variant, b):
    g = Geo()
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    if kind == 'pier':
        pier(g, T, hx, hy, hz, F['brick-buff'] if variant == 0 else F['brick'], True)
    elif kind == 'base':
        base(g, T, hx, hy, hz)
    elif kind == 'drum':
        drum(g, T, hx, hy, hz, variant)
    elif kind == 'bearing':
        bearing(g, T, hx, hy, hz)
    elif kind == 'deck':
        deck(g, T, hx, hy, hz)
    elif kind == 'rail':
        rail(g, T, hx, hy, hz)
    elif kind == 'slab':
        slab(g, T, hx, hy, hz, variant)
    elif kind == 'panel':
        inner = (F['paper-rose'], F['paper-sage'], F['paper-ochre'], F['paper-blue'])[int(size[0] * 10) % 4]
        panel(g, T, hx, hy, hz, 0 if variant == 9 else variant, F['brick-buff'], inner, F['stone'])
        if variant == 9:
            sign(g, T, hx, hy, hz)
    elif kind == 'lane':
        panel(g, T, hx, hy, hz, variant, F['brick'], F['plaster'], F['stone'], lane=True)
    elif kind == 'barricade':
        barricade(g, T, hx, hy, hz)
    elif kind == 'pallet':
        pallet(g, T, hx, hy, hz, variant)
    elif kind == 'sandbag':
        sandbag(g, T, hx, hy, hz)
    elif kind == 'stall':
        stall(g, T, hx, hy, hz, variant)
    else:
        sk.box(g, -hx, hx, -hy, hy, -hz, hz, bk.look_fin(b.get('look')), T, .03)
    return g


def block_models(S, T):
    """{key: (geo, [(block id, turn rows)], a block of the kind)} for every block that is not static."""
    out = {}
    for i, b in S.blocks.items():
        if b['role'] == 'static':
            continue
        kind, yaw, variant = classify(S, b)
        size = local_size(b, yaw)
        key = (kind, tuple(round(x, 3) for x in size), variant)
        if key not in out:
            out[key] = (build(S, T, kind, size, variant, b), [], b)
        out[key][1].append((i, turn(yaw)))
    return out


def rest_scene(S, T, landed=False):
    """The moving blocks' models where their blocks stand (the static bake's occluders and the previews); landed: the heaps
    instead of the segments' blocks."""
    g = Geo()
    gone = set()
    if landed:
        for s in S.segments:
            gone.update(s['blocks'])
            gone.update(s['members'])
        for (i, geo, pos) in heaps(S, T):
            g.extend(geo.transformed([1, 0, 0, pos[0], 0, 1, 0, pos[1], 0, 0, 1, pos[2]]))
    for key, (geo, uses, b0) in block_models(S, T).items():
        for (i, rows) in uses:
            if i in gone:
                continue
            p = S.blocks[i]['pos']
            m = list(rows)
            m[3], m[7], m[11] = p[0], p[1], p[2]
            g.extend(geo.transformed(m))
    return g


def bank_props(sb, S, T, floors, no_ao):
    ti = T.i
    items = []
    for key, (geo, uses, b0) in block_models(S, T).items():
        items.append((geo, uses, S.blocks[uses[0][0]]['pos'], uses[0][1], key[0]))
    for (i, geo, pos) in heaps(S, T):
        items.append((geo, [(i, turn(0.0))], pos, turn(0.0), 'heap'))
    aos = [None] * len(items)
    if not no_ao:
        apart = [it[0].transformed([1, 0, 0, 40.0 * (k % 12), 0, 1, 0, 0, 0, 0, 1, 40.0 * (k // 12)]) for k, it in enumerate(items)]
        aos = sk.bake_ao(apart, distance=.5, samples=(24, 12, 8)[ti])
    for (geo, uses, pos, rows, kind), ao in zip(items, aos):
        if geo.faces == 0:
            continue
        P = geo.arrays()[0].astype(np.float64)
        R = np.array(rows, dtype=np.float64).reshape(3, 4)[:, :3]
        world = P @ R.T + np.array(pos)
        if ao is not None:
            ao = np.clip(.3 + .7 * ao, 0, 1)
        bake = sk.Bake(geo, ao, world=world, soot=None, floors=floors if kind != 'heap' else (-99.0,), seed=5)
        name = 'block/%s/%d' % (uses[0][0], ti)
        om = sk.to_mesh(name, geo, bake)
        mi = sb.mesh(om)
        for (i, r) in uses:
            sb.bank.prop('block/%s/%d' % (i, ti), 'block', [(mi, list(r))])
        st = sb.stats.setdefault(('block ' + kind, str(ti)), [0, 0, 0])
        st[0] += 1
        st[1] += sum(len(s) for s in om.submeshes) // 3
        st[2] += len(om.positions)
