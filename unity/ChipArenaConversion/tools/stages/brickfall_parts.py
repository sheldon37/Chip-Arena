"""Brickfall's stage art: the static pieces' sculptors (see brickfall_art.py): the tenements' shells (walls with their blind
windows, floors, stairs, rear piers), the corner shops and their ramps, the market (kiosk, storeroom, walls), the yard (dock,
ramp, the box truck), the street furniture that is cover (monument, news kiosk, phone box, tram shelter) and the hotel's core
(the stair tower, its landings and flights, the seams, the stair house on the roof). Each piece keeps to its block's box, so
what is seen is what stops a pilot and a shot."""
import math

import stagekit as sk
from stagekit import Geo
import brickfall_kit as bk
from brickfall_kit import F, Frame, Hole, fbox
import brickfall_props as props


def bb(b):
    p, s = b['pos'], b['size']
    return p[0] - s[0] / 2, p[0] + s[0] / 2, p[1] - s[1] / 2, p[1] + s[1] / 2, p[2] - s[2] / 2, p[2] + s[2] / 2


def faces_of(b, cx, cz):
    """A thin wall block's two frames (outer: looking away from (cx, cz); inner), its length and thickness."""
    x0, x1, y0, y1, z0, z1 = bb(b)
    if (z1 - z0) <= (x1 - x0):
        s = 1 if b['pos'][2] >= cz else -1
        ln, t = x1 - x0, z1 - z0
        n = (0.0, 0.0, float(s))
    else:
        s = 1 if b['pos'][0] >= cx else -1
        ln, t = z1 - z0, x1 - x0
        n = (float(s), 0.0, 0.0)
    u = (n[2], 0.0, -n[0])
    c = (b['pos'][0], y0, b['pos'][2])
    outer = Frame((c[0] + n[0] * t / 2 - u[0] * ln / 2, y0, c[2] + n[2] * t / 2 - u[2] * ln / 2), u)
    inner = Frame((c[0] - n[0] * t / 2 + u[0] * ln / 2, y0, c[2] - n[2] * t / 2 + u[2] * ln / 2), (-u[0], 0.0, -u[2]))
    return outer, inner, ln, t


def wall(g, T, b, cx, cz, f, inner=None, storeys=(), coping=None, string=None, seed=0, win=(1.1, 1.55, .22), plinth=None, ends=True):
    """A static wall (a thin block): its outer face in brick with blind windows in each storey it is long enough for, strings
    at the floor lines, a coping; its inner face plastered by storey (or as the outer). storeys: [(y0, y1, inner finish)] over
    the block's foot."""
    outer, inn, ln, t = faces_of(b, cx, cz)
    h = b['size'][1]
    r = bk.rng(seed + int(b['pos'][0] * 7 + b['pos'][2] * 13))
    holes = []
    for (y0, y1, fi) in storeys:
        if ln < 2.4 or y1 - y0 < 2.6 or y1 > h + .01:
            continue
        k = max(1, int(ln / 3.2))
        for i in range(k):
            x = ln * (i + .5) / k
            q = r.random()
            fill = 'lit' if q < .22 else 'board' if q < .5 else 'brick' if q < .62 else 'glass'
            holes.append(Hole(x - win[0] / 2, x + win[0] / 2, y0 + .95, y0 + .95 + win[1], win[2], fill=fill, depth=.09, head='arch', lit=F['lit'] if r.random() < .5 else F['lit-dim']))
    bk.wallface(g, T, outer, ln, h, f, holes)
    st = {'trim': F['stone'], 'ring': F['brick-dark'] if f != F['brick-dark'] else F['brick'], 'key': False}
    for hl in holes:
        bk.opening(g, T, outer, hl, f, style=st)
    # the inner face in bands
    if inner is None and not storeys:
        bk.wallface(g, T, inn, ln, h, f)
    else:
        y = 0.0
        for (y0, y1, fi) in storeys:
            y1 = min(y1, h)
            if y0 >= h:
                break
            if y0 > y + 1e-4:
                bk.wallface(g, T, inn, ln, y0, F['concrete'], v0=y)
            ih = [Hole(ln - hl.u1, ln - hl.u0, hl.v0, hl.v1, hl.rise, fill=('glass' if hl.fill == 'lit' else hl.fill), depth=.05, sill=False, head='none') for hl in holes if y0 < hl.v0 < y1]
            bk.wallface(g, T, inn, ln, y1, fi, ih, v0=y0)
            for hl in ih:
                bk.opening(g, T, inn, hl, fi, style={'trim': None})
            if T.detail:
                bk.mould(g, T, inn, 0, ln, y0, [(0.0, 0.0), (.02, 0.0), (.02, .14), (0.0, .16)], F['timber-dark-x'], ends=False)
            y = y1
        if y < h - 1e-4:
            bk.wallface(g, T, inn, ln, h, f if inner is None else inner, v0=y)
    # ends and top
    if ends:
        for fr_ in (outer, inn):
            e = Frame(fr_.P(0, 0, -t), fr_.n)
            bk.wallface(g, T, e, t, h, f)
    x0, x1, y0b, y1b, z0, z1 = bb(b)
    if coping is not None:
        sk.box(g, x0 - .04, x1 + .04, y1b - .12, y1b, z0 - .04, z1 + .04, coping, T, .025, cell=0)
    else:
        g.face(((x0, y1b, z0), (x1, y1b, z0), (x1, y1b, z1), (x0, y1b, z1)), f, (0, 1, 0))
    for y in (string or ()):
        if y < h - .2:
            bk.mould(g, T, outer, 0, ln, y, [(0.0, 0.0), (.035, .02), (.035, .2), (0.0, .22)], F['stone'])
    if plinth is not None:
        bk.mould(g, T, outer, 0, ln, 0.0, [(0.0, 0.0), (.04, 0.0), (.04, .5), (0.0, .56)], plinth)


def floor_slab(g, T, b, top, soffit=True, edge=None):
    x0, x1, y0, y1, z0, z1 = bb(b)
    cell = (1.0, 3.0, 6.0)[T.i]
    sk.box(g, x0, x1, y0, y1 - .03, z0, z1, F['concrete'] if edge is None else edge, T, .012, cell=cell, skip=('+y',))
    sk.box(g, x0, x1, y1 - .03, y1, z0, z1, top, T, .008, cell=cell, skip=('-y',))
    if soffit and T.detail:
        along_x = (x1 - x0) > (z1 - z0)
        n = max(2, int(((x1 - x0) if along_x else (z1 - z0)) / 1.3))
        for i in range(n):
            if along_x:
                x = x0 + (x1 - x0) * (i + .5) / n
                sk.box(g, x - .07, x + .07, y0 - .1, y0, z0 + .1, z1 - .1, F['timber-dark-z'], None, 0, cell=0, skip=('+y',))
            else:
                z = z0 + (z1 - z0) * (i + .5) / n
                sk.box(g, x0 + .1, x1 - .1, y0 - .1, y0, z - .07, z + .07, F['timber-dark-x'], None, 0, cell=0, skip=('+y',))


def steps(g, T, blocks, f=None):
    """Stair treads (each its own block): concrete with a worn nosing."""
    f = F['concrete'] if f is None else f
    for b in blocks:
        x0, x1, y0, y1, z0, z1 = bb(b)
        sk.box(g, x0, x1, y0, y1, z0, z1, f, T if T.i == 0 else None, .014 if T.i == 0 else 0, cell=0, skip=('-y',) if y0 < .05 else ())


def ramp(g, T, slices, f, kerb=None):
    """A ramp the map builds from thin slices: one slope through the slices' top edges, cheeks down to the ground, kerbs."""
    sl = sorted(slices, key=lambda b: b['pos'][0])
    x0, _, _, _, z0, z1 = bb(sl[0])
    x1 = bb(sl[-1])[1]
    tops = [bb(b)[3] for b in sl]
    lo_first = tops[0] < tops[-1]
    step = abs(tops[1] - tops[0])
    # the slope's line: through each slice's upper edge on the high side
    ya = 0.0 if lo_first else tops[0]
    yb = tops[-1] if lo_first else 0.0
    n = (len(sl), max(2, len(sl) // 4), 1)[T.i]
    nn = sk._norm((ya - yb, x1 - x0, 0.0))
    for i in range(n):
        a, b = x0 + (x1 - x0) * i / n, x0 + (x1 - x0) * (i + 1) / n
        pa, pb = ya + (yb - ya) * i / n, ya + (yb - ya) * (i + 1) / n
        g.face(((a, pa, z0), (b, pa + (pb - pa), z0), (b, pb, z1), (a, pa, z1)), f, nn)
        for (z, nz) in ((z0, -1), (z1, 1)):
            g.face(((a, 0.0, z), (b, 0.0, z), (b, pb, z), (a, pa, z)), f, (0, 0, nz))
    hi_x, hi_y = (x1, yb) if lo_first else (x0, ya)
    g.face(((hi_x, 0.0, z0), (hi_x, 0.0, z1), (hi_x, hi_y, z1), (hi_x, hi_y, z0)), f, (1 if lo_first else -1, 0, 0))
    if kerb is not None and T.detail:
        # grip battens across the slope
        k = int((x1 - x0) / .45)
        for i in range(1, k):
            x = x0 + (x1 - x0) * i / k
            y = ya + (yb - ya) * i / k
            fr = Frame((x - .03, y, z0 + .06), sk._norm((x1 - x0, yb - ya, 0)), nn)
            fbox(g, T, fr, 0, .06, 0, .012, 0, z1 - z0 - .12, kerb, .003, skip=('-y',))


# ---------------------------------------------------------------------------------------------------------------------------
def tenement(g, T, H, v, done):
    """Ashby House's shell (the lane facade and its piers are the breakable models): brick walls with blind windows, papered
    rooms inside, board floors, a felted roof behind its parapet, the stair core and its flights, the rear piers."""
    cx, cz = -15.5, 16.5
    papers = ((F['paper-rose'], F['paper-sage']), (F['paper-ochre'], F['paper-blue']))[v]
    fw = F['brick'] if v == 0 else F['brick-rose']
    st = [(0.0, 3.36, papers[0]), (3.6, 6.96, papers[1])]
    m = g.faces
    for i, b in H.items():
        if not i.startswith('ashby-') or b['role'] != 'static':
            continue
        if i.startswith('ashby-ashby-'):
            wall(g, T, b, cx, cz, fw, storeys=st, coping=F['stone'], string=(3.4, 7.0), seed=v * 31, plinth=F['stone-dark'])
        elif i == 'ashby-core-wall':
            wall(g, T, b, -30.0, cz, F['plaster'], inner=F['plaster'], storeys=[(0.0, 3.36, F['plaster']), (3.6, 6.96, F['plaster'])])
        elif i.startswith('ashby-floor'):
            floor_slab(g, T, b, F['tar'] if b['pos'][1] > 5 else (F['floorboard-x'], F['floorboard-z'])[v])
        elif i.startswith('ashby-stair-landing'):
            floor_slab(g, T, b, F['concrete'], soffit=False)
        elif i.startswith('ashby-stair'):
            steps(g, T, [b])
        elif i.startswith('ashby-rear-pier'):
            t = Geo()
            props.pier(t, T, b['size'][0] / 2, b['size'][1] / 2, b['size'][2] / 2, fw, False)
            g.extend(t.transformed([1, 0, 0, b['pos'][0], 0, 1, 0, b['pos'][1], 0, 0, 1, b['pos'][2]]))
        else:
            continue
        done.add(i)
    if T.detail:
        # the house's name over the south wall's long run
        b = H.get('ashby-ashby-south2')
        if b is not None:
            outer, inn, ln, t = faces_of(b, cx, cz)
            bk.text(g, outer, ln / 2, 7.42, .06, ('ASHBY HOUSE', 'BRAY HOUSE')[v], F['stone'], .012)


def shop(g, T, H, v, done):
    """The corner shop: cream faience on a dark plinth, boarded and shuttered fronts under a lettered fascia, a cornice, its
    parapet with a coping; the long ramp up its lane side."""
    b = H['shop-kessels']
    x0, x1, y0, y1, z0, z1 = bb(b)
    fw = F['stone']
    name = ("KESSEL'S", "DUNMORE'S")[v]
    r = bk.rng(41 + v)
    for (ax, az, bx, bz, kind) in ((x1, z0, x0, z0, 'front'), (x0, z0, x0, z1, 'side'), (x1, z1, x1, z0, 'side'), (x0, z1, x1, z1, 'back')):
        fr = Frame((ax, 0.0, az), (bx - ax, 0.0, bz - az))
        ln = math.hypot(bx - ax, bz - az)
        holes = []
        if kind != 'back':
            k = max(1, int(ln / 2.6))
            w = (ln - .5 * (k + 1)) / k
            for i in range(k):
                a = .5 + i * (w + .5)
                door = kind == 'front' and i == k // 2
                fill = 'door' if door else ('board', 'shutter', 'glass')[r.randrange(3)]
                holes.append(Hole(a, a + w, 0.0 if fill in ('door', 'shutter') else .6, 2.45, 0.0, fill=fill, depth=.14, sill=fill not in ('door', 'shutter'), head='none', bars=(max(2, int(w / .6)), 2)))
        bk.wallface(g, T, fr, ln, y1, fw, holes)
        st = {'trim': F['stone-dark'], 'frame': F['green-paint'], 'door': F['green-paint']}
        for hl in holes:
            bk.opening(g, T, fr, hl, fw, style=st)
        # plinth between the openings, pilasters, fascia, cornice
        x = 0.0
        for hl in sorted(holes, key=lambda h: h.u0) + [None]:
            a = ln if hl is None else hl.u0
            if a - x > .05:
                bk.mould(g, T, fr, x, a, 0.0, [(0.0, 0.0), (.04, 0.0), (.04, .5), (0.0, .56)], F['concrete-dark'], ends=True)
            if hl is not None:
                x = hl.u1 if hl.v0 < .3 else hl.u0
                if hl.v0 >= .3:
                    bk.mould(g, T, fr, hl.u0, hl.u1, 0.0, [(0.0, 0.0), (.04, 0.0), (.04, .5), (0.0, .56)], F['concrete-dark'], ends=False)
                    x = hl.u1
        if kind != 'back':
            bk.mould(g, T, fr, .1, ln - .1, 2.6, [(0.0, 0.0), (.06, 0.0), (.06, .42), (.1, .46), (.1, .52), (0.0, .52)], F['green-paint'])
            if T.detail:
                bk.text(g, fr, ln / 2, 2.7, .04, name if kind == 'front' else ('GROCER', 'PROVISIONS', 'CHEMIST', 'NEWS')[r.randrange(4)], F['stone'], .066)
        bk.cornice(g, T, fr, 0, ln, y1 - .46, F['stone-dark'], .7, dentils=T.i == 0)
    # the roof
    cell = (1.0, 3.0, 6.0)[T.i]
    sk.grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['tar'], (0, 1, 0), cell)
    done.add('shop-kessels')
    for i, pb in H.items():
        if i.startswith('shop-parapet-kessels'):
            wall(g, T, pb, (x0 + x1) / 2, (z0 + z1) / 2, F['stone'], coping=F['stone-dark'], ends=False)
            done.add(i)
    rs = [pb for i, pb in H.items() if i.startswith('shop-ramp-ashby')]
    ramp(g, T, rs, F['concrete'], F['iron'])
    done.update(pb['id'] for pb in rs)


def kiosk(g, T, b, name, posters=True, roof_fin=None, seed=0):
    """A kiosk (market, news): green panelled sides between corner posts, shuttered hatches, a poster board, a flat roof with
    an eaves board all round."""
    x0, x1, y0, y1, z0, z1 = bb(b)
    f = F['green-paint']
    r = bk.rng(seed)
    pc = (F['poster-a'], F['poster-b'], F['poster-c'], F['poster-d'])
    for k, (ax, az, bx, bz) in enumerate(((x1, z0, x0, z0), (x0, z0, x0, z1), (x1, z1, x1, z0), (x0, z1, x1, z1))):
        fr = Frame((ax, 0.0, az), (bx - ax, 0.0, bz - az))
        ln = math.hypot(bx - ax, bz - az)
        top = y1 - .3
        holes = []
        if k in (0, 3):
            holes.append(Hole(.3, ln - .3, 1.0, top - .25, 0.0, fill='shutter', depth=.07, sill=True, head='none'))
        elif k == 1:
            holes.append(Hole(ln / 2 - .42, ln / 2 + .42, 0.0, 2.0, 0.0, fill='door', depth=.06, sill=False, head='none'))
        bk.wallface(g, T, fr, ln, top, f, holes)
        for hl in holes:
            bk.opening(g, T, fr, hl, f, style={'trim': F['timber-dark-x'], 'door': F['green-paint']})
        if T.detail:
            # corner posts, a dado rail and panels under the hatch
            for a in (0.0, ln - .1):
                fbox(g, T, fr, a, a + .1, 0, top, 0, .03, F['timber-dark'], .008, skip=('-z', '-y'))
            fbox(g, T, fr, .1, ln - .1, .92, 1.0, 0, .035, F['timber-dark-x'], .008, skip=('-z',))
            if k in (0, 3):
                n = max(2, int(ln / .7))
                for i in range(n):
                    a, c = .16 + (ln - .32) * i / n, .16 + (ln - .32) * (i + 1) / n
                    fbox(g, T, fr, a + .04, c - .04, .12, .84, 0, .02, f, .012, skip=('-z',))
            elif posters and k == 2:
                for i in range(3):
                    a = .25 + i * (ln - .5) / 3
                    fbox(g, T, fr, a + .05, a + (ln - .5) / 3 - .05, .8 + r.uniform(0, .2), 2.0 + r.uniform(-.1, .2), 0, .012, pc[r.randrange(4)], 0, skip=('-z',))
        # the eaves board with the name
        fbox(g, T, fr, -.06, ln + .06, top, y1, -.02, .08, F['timber-dark-x'], .015, skip=('-z', '+y'))
        if T.detail and name and k in (0, 3):
            bk.text(g, fr, ln / 2, top + .07, .024, name, F['stone'], .086)
    g.face(((x0 - .08, y1, z0 - .08), (x1 + .08, y1, z0 - .08), (x1 + .08, y1, z1 + .08), (x0 - .08, y1, z1 + .08)), F['tar'] if roof_fin is None else roof_fin, (0, 1, 0))


def storeroom(g, T, b, v):
    """A market storeroom: rendered brick, a roller door to the yard, high slot windows, a parapet round a flat roof."""
    x0, x1, y0, y1, z0, z1 = bb(b)
    f = F['concrete']
    for k, (ax, az, bx, bz) in enumerate(((x1, z0, x0, z0), (x0, z0, x0, z1), (x1, z1, x1, z0), (x0, z1, x1, z1))):
        fr = Frame((ax, 0.0, az), (bx - ax, 0.0, bz - az))
        ln = math.hypot(bx - ax, bz - az)
        holes = []
        if k == 2:
            holes.append(Hole(ln / 2 - 1.4, ln / 2 + 1.4, 0.0, 2.6, 0.0, fill='shutter', depth=.12, sill=False, head='steel'))
        elif k in (0, 3):
            for i in range(3):
                a = ln * (i + .5) / 3
                holes.append(Hole(a - .6, a + .6, 2.3, 2.85, 0.0, fill='glass' if (i + v) % 3 else 'lit', depth=.1, head='lintel', lit=F['lit-dim'], bars=(3, 1)))
        bk.wallface(g, T, fr, ln, y1, f, holes)
        for hl in holes:
            bk.opening(g, T, fr, hl, f, style={'trim': F['concrete-dark'], 'frame': F['iron']})
        bk.mould(g, T, fr, 0, ln, 0.0, [(0.0, 0.0), (.04, 0.0), (.04, .4), (0.0, .46)], F['concrete-dark'])
        bk.mould(g, T, fr, 0, ln, y1 - .2, [(0.0, 0.0), (.05, .03), (.05, .2), (0.0, .2)], F['concrete-dark'])
        if T.detail and k == 2:
            bk.text(g, fr, ln / 2, 2.95, .035, ('STORE No 2', 'STORE No 5')[v].replace('2', 'II').replace('5', 'V'), F['black'], .012)
            bk.pipe(g, T, fr, .4, .1, y1 - .25, c=.07, r=.04)
    cell = (1.0, 3.0, 6.0)[T.i]
    sk.grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['tar'], (0, 1, 0), cell)


def market_wall(g, T, b, v):
    """A length of the market's wall: brick both sides with blind arches, a stone coping, piers at its ends."""
    x0, x1, y0, y1, z0, z1 = bb(b)
    f = F['brick'] if v == 0 else F['brick-plum']
    outer, inn, ln, t = faces_of(b, b['pos'][0], -99.0)
    for fr in (outer, inn):
        holes = []
        if ln >= 2.4:
            k = max(1, int(ln / 2.6))
            for i in range(k):
                a = ln * (i + .5) / k
                holes.append(Hole(a - .85, a + .85, .7, 2.3, .85, fill='brick', depth=.07, sill=False, head='arch'))
        bk.wallface(g, T, fr, ln, y1, f, holes)
        for hl in holes:
            bk.opening(g, T, fr, hl, f, style={'trim': None, 'ring': F['brick-dark'], 'blind': F['brick-dark'], 'key': False})
        bk.mould(g, T, fr, 0, ln, 0.0, [(0.0, 0.0), (.035, 0.0), (.035, .45), (0.0, .5)], F['stone-dark'], ends=False)
        e = Frame(fr.P(0, 0, -t), fr.n)
        bk.wallface(g, T, e, t, y1, f)
    sk.box(g, x0 - .03, x1 + .03, y1 - .14, y1, z0 - .05, z1 + .05, F['stone'], T, .03, cell=0)


def dock(g, T, b, slices):
    """The loading dock: a concrete platform, a steel nosing, rubber buffers, hazard paint on its lip; its ramp."""
    x0, x1, y0, y1, z0, z1 = bb(b)
    f = F['concrete-dark']
    sk.box(g, x0, x1, y0, y1, z0, z1, f, T, .02, cell=(1.0, 0, 0)[T.i], skip=('-y',))
    for (ax, az, bx, bz) in ((x0, z1, x1, z1), (x1, z0, x0, z0)):
        fr = Frame((ax, 0.0, az), (bx - ax, 0.0, bz - az))
        ln = abs(bx - ax)
        fbox(g, T, fr, 0, ln, y1 - .12, y1, 0, .025, F['iron'], .006, skip=('-z',))
        bk.chevrons(g, fr, .02, ln - .02, y1 - .34, y1 - .14, .008, F['yellow'], .3)
        if T.detail:
            for i in range(4):
                a = ln * (i + .5) / 4
                fbox(g, T, fr, a - .12, a + .12, .35, .8, 0, .07, F['rubber'], .02, skip=('-z',))
    ramp(g, T, slices, F['concrete'], F['iron'])


def truck(g, T, b, v):
    """The box truck (its cab toward the yard's middle): a Luton body over the cab, so its top is one deck like its block's."""
    x0, x1, y0, y1, z0, z1 = bb(b)
    ln, wd = x1 - x0, z1 - z0
    body = F['corrugated']
    cab = F['cab-red'] if v == 0 else F['green-paint']
    cabx = ln - 1.75
    # the body: from the tail to the cab's back, and over the cab
    sk.box(g, x0, x0 + cabx, .95, y1, z0, z1, body, T, .04, cell=(1.2, 0, 0)[T.i])
    sk.box(g, x0 + cabx, x1, 2.25, y1, z0, z1, body, T, .04, cell=0)
    # ribs down its sides
    if T.detail:
        for (z, nz) in ((z0, -1), (z1, 1)):
            k = 9
            for i in range(k):
                x = x0 + .25 + (cabx - .5) * i / (k - 1)
                sk.box(g, x - .03, x + .03, 1.0, y1 - .06, z - .02 if nz < 0 else z, z if nz < 0 else z + .02, body, None, 0, cell=0, skip=('+z' if nz < 0 else '-z',))
        fs = Frame((x0 + .3, 0.0, z1), (1, 0, 0))
        bk.text(g, fs, (cabx - .6) / 2, 1.75, .07, ('MERCER HAULAGE', 'BRAY CARRIERS')[v], cab, .012)
        fs2 = Frame((x0 + cabx - .3, 0.0, z0), (-1, 0, 0))
        bk.text(g, fs2, (cabx - .6) / 2, 1.75, .07, ('MERCER HAULAGE', 'BRAY CARRIERS')[v], cab, .012)
    # the tail's roller door and step
    ft = Frame((x0, 0.0, z0), (0, 0, 1))
    hl = Hole(.2, wd - .2, 1.05, y1 - .2, 0.0, fill='shutter', depth=.05, sill=False, head='none')
    bk.opening(g, T, ft, hl, body, style={'trim': None})
    fbox(g, T, ft, .1, wd - .1, .55, .65, 0, .3, F['iron'], .01)
    # the cab
    sk.box(g, x0 + cabx, x1 - .02, .55, 2.25, z0 + .04, z1 - .04, cab, T, .08, cell=0, skip=('+y',))
    ff = Frame((x1 - .02, 0.0, z1 - .04), (0, 0, -1))
    cw = wd - .08
    fbox(g, T, ff, .12, cw - .12, 1.45, 2.1, -.005, .012, F['glass'], .0, skip=('-z',))        # the windscreen
    fbox(g, T, ff, cw / 2 - .02, cw / 2 + .02, 1.45, 2.1, 0, .02, cab, 0, skip=('-z',))
    fbox(g, T, ff, .5, cw - .5, .8, 1.25, 0, .02, F['iron'], .01, skip=('-z',))                  # the grille
    fbox(g, T, ff, .0, cw, .45, .65, 0, .1, F['iron'], .02)                                      # the bumper
    for a in (.25, cw - .25):
        sk.lathe(g, [(.1, 0.0), (.1, .03), (.07, .05), (0.0, .06)], ff.P(a, 1.02, .0), F['glass-pale'], T.small + 2, axis=ff.n)
    for (z, nz) in ((z0 + .04, -1), (z1 - .04, 1)):
        fd = Frame((x0 + cabx + (0 if nz > 0 else 1.73), 0.0, z), (nz, 0, 0))
        fbox(g, T, fd, .5 if nz > 0 else .25, 1.5 if nz > 0 else 1.25, 1.45, 2.05, -.004, .01, F['glass'], 0, skip=('-z',))
        if T.detail:
            fbox(g, T, fd, .28, 1.52, .7, 1.4, 0, .012, cab, .01, skip=('-z',))
    # chassis, skirts and wheels
    sk.box(g, x0 + .3, x0 + cabx, .5, .95, z0 + .25, z1 - .25, F['iron'], T, .02, cell=0, skip=('+y',))
    for (z, nz) in ((z0, -1), (z1, 1)):
        sk.box(g, x0 + 1.9, x0 + cabx - 1.1, .28, .95, z + (0 if nz < 0 else -.06), z + (.06 if nz < 0 else 0), F['iron'], T, .01, cell=0, skip=('+y',))
        for wx in (x0 + 1.15, x0 + cabx - .45, x1 - .75):
            if wx == x0 + cabx - .45:
                continue
            c = (wx, .46, z + nz * -.02)
            ax = (0, 0, nz)
            sk.lathe(g, [(.25, -.26), (.46, -.24), (.46, -.02), (.28, .0), (.24, -.05), (0.0, -.05)], c, F['rubber'], (16, 10, 6)[T.i], axis=ax)
            if T.detail:
                sk.lathe(g, [(.2, -.045), (.1, -.02), (0.0, -.02)], c, F['steel'], T.small + 2, axis=ax)


def monument(g, T, b, v):
    """The monument's pedestal (its figure long gone): stepped stone, a bronze tablet on each face, a moulded cap."""
    x0, x1, y0, y1, z0, z1 = bb(b)
    f = F['stone']
    sk.box(g, x0, x1, 0, .32, z0, z1, F['stone-dark'], T, .03, cell=0, skip=('-y',))
    sk.box(g, x0 + .1, x1 - .1, .32, .62, z0 + .1, z1 - .1, f, T, .03, cell=0, skip=('-y',))
    sk.box(g, x0 + .2, x1 - .2, .62, y1 - .5, z0 + .2, z1 - .2, f, T, .03, cell=(.8, 0, 0)[T.i], skip=('-y', '+y'))
    sk.box(g, x0 + .1, x1 - .1, y1 - .5, y1 - .3, z0 + .1, z1 - .1, f, T, .04, cell=0)
    sk.box(g, x0, x1, y1 - .3, y1, z0, z1, f, T, .05, cell=0)
    for (ax, az, bx, bz) in ((x1 - .2, z0 + .2, x0 + .2, z0 + .2), (x0 + .2, z0 + .2, x0 + .2, z1 - .2), (x1 - .2, z1 - .2, x1 - .2, z0 + .2), (x0 + .2, z1 - .2, x1 - .2, z1 - .2)):
        fr = Frame((ax, 0.0, az), (bx - ax, 0.0, bz - az))
        ln = math.hypot(bx - ax, bz - az)
        fbox(g, T, fr, .3, ln - .3, .9, 1.6, -.001, .03, F['bronze'], .012, skip=('-z',))
        if T.detail:
            bk.text(g, fr, ln / 2, 1.3, .028, ('TO THE', 'BUILDERS')[0], F['stone'], .034)
            bk.text(g, fr, ln / 2, 1.02, .028, ('BRICKMAKERS', 'MASONS')[v], F['stone'], .034)
    if T.detail:
        # what is left of the figure: two bronze dowels and a stump
        for (dx, dz) in ((-.2, .1), (.22, -.12)):
            sk.cylinder(g, (b['pos'][0] + dx, y1, b['pos'][2] + dz), (b['pos'][0] + dx, y1 + .06, b['pos'][2] + dz), .05, F['bronze'], T.small)


def phonebox(g, T, b):
    """A cast-iron phone box: panelled below, glazed above in small panes (one face lit), a shallow crown."""
    x0, x1, y0, y1, z0, z1 = bb(b)
    f = F['green-paint']
    for k, (ax, az, bx, bz) in enumerate(((x1, z0, x0, z0), (x0, z0, x0, z1), (x1, z1, x1, z0), (x0, z1, x1, z1))):
        fr = Frame((ax, 0.0, az), (bx - ax, 0.0, bz - az))
        ln = math.hypot(bx - ax, bz - az)
        hl = Hole(.14, ln - .14, .75, y1 - .5, 0.0, fill='lit' if k == 0 else 'glass', depth=.04, sill=False, head='none', lit=F['lit-dim'], bars=(3, 5))
        bk.wallface(g, T, fr, ln, y1 - .22, f, [hl])
        bk.opening(g, T, fr, hl, f, style={'trim': None, 'frame': f})
        if T.detail:
            fbox(g, T, fr, .16, ln - .16, .14, .64, 0, .018, f, .012, skip=('-z',))
            fbox(g, T, fr, .1, ln - .1, y1 - .46, y1 - .26, 0, .014, F['stone'], 0, skip=('-z',))
            bk.text(g, fr, ln / 2, y1 - .42, .017, 'TELEPHONE', F['black'], .02)
    sk.box(g, x0 - .03, x1 + .03, y1 - .22, y1 - .1, z0 - .03, z1 + .03, f, T, .03, cell=0)
    sk.box(g, x0 + .08, x1 - .08, y1 - .1, y1, z0 + .08, z1 - .08, f, T, .05, cell=0, skip=('-y',))


def shelter(g, T, b, v):
    """The tram shelter: closed all round (it is cover): cast-iron posts, boarded below, glazed above, a flat roof with a
    scalloped valance and the stop's name."""
    x0, x1, y0, y1, z0, z1 = bb(b)
    f = F['green-paint']
    for k, (ax, az, bx, bz) in enumerate(((x1, z0, x0, z0), (x0, z0, x0, z1), (x1, z1, x1, z0), (x0, z1, x1, z1))):
        fr = Frame((ax, 0.0, az), (bx - ax, 0.0, bz - az))
        ln = math.hypot(bx - ax, bz - az)
        n = max(1, int(round(ln / 1.0)))
        holes = [Hole(.1 + (ln - .2) * i / n + .05, .1 + (ln - .2) * (i + 1) / n - .05, 1.05, y1 - .45, 0.0, fill='glass', depth=.05, sill=False, head='none', bars=(2, 2)) for i in range(n)]
        bk.wallface(g, T, fr, ln, y1 - .26, f, holes)
        for hl in holes:
            bk.opening(g, T, fr, hl, f, style={'trim': None, 'frame': F['white-paint']})
        if T.detail:
            for i in range(n):
                a, c = .1 + (ln - .2) * i / n + .05, .1 + (ln - .2) * (i + 1) / n - .05
                fbox(g, T, fr, a, c, .12, .95, 0, .016, F['timber-dark'], .012, skip=('-z',))
            for i in range(n + 1):
                a = min(max(.1 + (ln - .2) * i / n, .05), ln - .05)
                sk.cylinder(g, fr.P(a, 0, .0), fr.P(a, y1 - .26, .0), .05, F['iron'], T.small, caps=False)
            if k in (0, 3):
                bk.text(g, fr, ln / 2, y1 - .2, .022, ('MERCER STREET', 'BRAY ROW')[v], F['stone'], .072)
        fbox(g, T, fr, -.06, ln + .06, y1 - .26, y1, -.02, .06, F['timber-dark-x'], .012, skip=('-z', '+y'))
    g.face(((x0 - .06, y1, z0 - .06), (x1 + .06, y1, z0 - .06), (x1 + .06, y1, z1 + .06), (x0 - .06, y1, z1 + .06)), F['corrugated'], (0, 1, 0))


def sculpt(g, T, S, H, v, done):
    art = bk
    m = g.faces
    tenement(g, T, H, v, done)
    art.tally(g, 'tenement', m)
    m = g.faces
    shop(g, T, H, v, done)
    art.tally(g, 'shop', m)
    m = g.faces
    kiosk(g, T, H['market-kiosk-w'], ('FRUIT & FLOWERS', 'PIES & TEA')[v], seed=3 + v)
    storeroom(g, T, H['market-storeroom-w'], v)
    for i in ('market-market-wall-w0', 'market-market-wall-w2', 'market-market-wall-w4'):
        market_wall(g, T, H[i], v)
    done.update(i for i in H if i.startswith('market-'))
    art.tally(g, 'market', m)
    m = g.faces
    rs = [b for i, b in H.items() if i.startswith('yard-dock-ramp-ashby')]
    dock(g, T, H['yard-dock-sw'], rs)
    truck(g, T, H['yard-truck-sw'], v)
    done.update(i for i in H if i.startswith('yard-'))
    art.tally(g, 'yard', m)
    m = g.faces
    monument(g, T, H['cover-monument-ne'], v)
    kiosk(g, T, H['cover-news-kiosk-n'], ('NEWS', 'PAPERS')[v], seed=9 + v)
    phonebox(g, T, H['cover-phonebox-nw'])
    shelter(g, T, H['cover-tram-shelter-n'], v)
    done.update(('cover-monument-ne', 'cover-news-kiosk-n', 'cover-phonebox-nw', 'cover-tram-shelter-n'))
    art.tally(g, 'street', m)


# ---------------------------------------------------------------------------------------------------------------------------
def hotel_core(g, T, S):
    """The Hotel Mercer's core, which never falls: the concrete stair tower with a painted dado, its flights and landings, the
    ground floor's seam walls in buff brick, the stair house on the roof."""
    art = bk
    m = g.faces
    conc = F['concrete']
    for i, b in S.hotel.items():
        if b['role'] != 'static':
            continue
        x0, x1, y0, y1, z0, z1 = bb(b)
        if i.startswith('hotel-stair-landing'):
            floor_slab(g, T, b, F['concrete'], soffit=False)
        elif i == 'hotel-stairhouse-roof':
            sk.box(g, x0 - .06, x1 + .06, y0, y1, z0 - .06, z1 + .06, F['concrete'], T, .02, cell=0)
            g.face(((x0, y1 + .004, z0), (x1, y1 + .004, z0), (x1, y1 + .004, z1), (x0, y1 + .004, z1)), F['tar'], (0, 1, 0))
        elif i.startswith('hotel-stairhouse'):
            wall(g, T, b, 0.0, 4.0, F['brick-buff'], coping=None)
        elif i.startswith('hotel-stair-'):
            steps(g, T, [b])
        elif i.startswith('hotel-core-hotel-seam'):
            # a thick ground-floor wall between the wings: buff brick, a blind arch each side, a stone cap
            for (ax, az, bx, bz) in ((x1, z0, x0, z0), (x0, z1, x1, z1)):
                fr = Frame((ax, 0.0, az), (bx - ax, 0.0, bz - az))
                ln = x1 - x0
                hl = Hole(ln / 2 - 1.1, ln / 2 + 1.1, .0, 1.5, 1.1, fill='brick', depth=.12, sill=False, head='arch')
                bk.wallface(g, T, fr, ln, y1, F['brick-buff'], [hl])
                bk.opening(g, T, fr, hl, F['brick-buff'], style={'trim': None, 'ring': F['stone'], 'blind': F['brick-dark'], 'key': True})
                bk.mould(g, T, fr, 0, ln / 2 - 1.1, 0.0, [(0.0, 0.0), (.04, 0.0), (.04, .5), (0.0, .56)], F['stone'], ends=False)
                bk.mould(g, T, fr, ln / 2 + 1.1, ln, 0.0, [(0.0, 0.0), (.04, 0.0), (.04, .5), (0.0, .56)], F['stone'], ends=False)
            for (x, nx) in ((x0, -1), (x1, 1)):
                g.face(((x, 0, z0), (x, 0, z1), (x, y1, z1), (x, y1, z0)), F['brick-buff'], (nx, 0, 0))
            sk.box(g, x0, x1, y1 - .14, y1, z0 - .04, z1 + .04, F['stone'], T, .03, cell=0)
        else:
            # the tower's walls: concrete, a green dado up to a rail on every storey's foot
            outer, inn, ln, t = faces_of(b, 0.0, 0.0)
            h = y1 - y0
            for fr in (outer, inn):
                if h > 2:
                    bk.wallface(g, T, fr, ln, 1.15, F['green-paint'])
                    bk.wallface(g, T, fr, ln, h, conc, v0=1.15)
                    if T.detail and ln > .5:
                        bk.mould(g, T, fr, 0, ln, 1.15, [(0.0, 0.0), (.015, 0.0), (.015, .05), (0.0, .05)], F['black'], ends=False)
                else:
                    bk.wallface(g, T, fr, ln, h, conc)
                e = Frame(fr.P(0, 0, -t), fr.n)
                bk.wallface(g, T, e, t, h, conc)
            g.face(((x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)), conc, (0, 1, 0))
            if y0 > .1:
                g.face(((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)), conc, (0, -1, 0))
    art.tally(g, 'hotel core', m)
