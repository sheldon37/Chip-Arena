"""Polaris Station's props (docs/arenas/POLAR.md "Stage art"): the rig's moving parts, the blocks that break or sink, and the
far backdrop.

Rig props (the bank's rig/<name>/<tier>; Core NativePolarArt poses each at its tagged rig part's centre and turn):
  dish         origin the elevation bearing; boresight +z, up +y. The reflector's vertex stands `dishBack` behind the bearing,
               its rim `dishDepth` before the vertex; the feed `dishFeed` along the boresight from the vertex.
  yoke         origin the turntable's centre on the pedestal's top (`yokeDrop` under the bearing); it turns about y; its arms
               carry the bearings at x +-2.05, y +yokeDrop.
  searchlight  origin the lamp's centre; its beam along +z.
  balloon      origin its centre (radius `balloonRadius`), scaled alike as it climbs.
  sonde        origin the sonde's box (its line runs up +y `sondeDrop` to the balloon's neck).
  cups         origin .14 m over a weather mast's top; turns about y.
  vent-lamp    origin on the shelf at a vent's corner; its dome is white light times the draw's tint.
  floe         a floe of unit half extent in x and z (scaled by the draw), .24 m thick about its origin.
Block props (block/<id>/<tier>, the block's own frame): the crates and drums (breakable cover), the thin ice's plates and
seams (they sink with their segment).
The far backdrop (static/far-*/2, one tier): the sea, the shelf out to the horizon, its cliff, icebergs, the ice cap's rim."""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo
import polar_kit as pk
from polar_kit import F, H, bx


# ---------------------------------------------------------------------------------------------------------------------------
# The rig
# ---------------------------------------------------------------------------------------------------------------------------
def dish(L, T):
    P = L['polar']
    R, depth, back, feed, petals = P['dishRadius'], P['dishDepth'], P['dishBack'], P['dishFeed'], int(P['petals'])
    g = Geo()
    def zs(r):
        return -back + depth * (r / R) ** 2
    na = (48, 24, 16)[T.i]
    nr = (9, 5, 3)[T.i]
    rr = [.9 + (R - .9) * (k / nr) for k in range(nr + 1)]
    grid = np.zeros((nr + 1, na, 3))
    for i, r in enumerate(rr):
        for j in range(na):
            a = 2 * math.pi * j / na
            grid[i, j] = (math.cos(a) * r, math.sin(a) * r, zs(r))
    # the reflector's face (panels a shade apart, ring by ring and petal by petal) and its back skin
    pk.surf(g, grid, F['dish'], wrap_j=True, facing=(0, 0, 1), pick=lambda n, c: F['dish'] if (int((math.atan2(c[1], c[0]) % (2 * math.pi)) / (2 * math.pi) * petals) + int(math.hypot(c[0], c[1]) / R * 3)) % 2 == 0 else F['white'])
    backg = grid.copy()
    backg[:, :, 2] -= .07
    pk.surf(g, backg, F['steel'], wrap_j=True, facing=(0, 0, -1))
    rim = [(math.cos(2 * math.pi * j / na) * R, math.sin(2 * math.pi * j / na) * R, zs(R) - .035) for j in range(na)]
    sk.sweep(g, sk.rect_section(.1, .14, .03), rim, F['steel-dark'], closed=True, up=(0, 0, 1))
    # the back's structure: radial trusses, two ring girders, the hub and its counterweights
    for k in range(petals if T.i < 2 else petals // 2):
        a = (k + .5) * 2 * math.pi / (petals if T.i < 2 else petals // 2)
        path = [(math.cos(a) * r, math.sin(a) * r, zs(r) - .07 - .5 * (1 - r / R) - .06) for r in (.9, R * .5, R * .97)]
        sk.sweep(g, sk.rect_section(.07, .3, 0), path, F['steel-dark'], up=(0, 0, 1), caps=True, scale=[1.5, 1.0, .35])
    if T.i < 2:
        for r in (R * .45, R * .8):
            ring = [(math.cos(2 * math.pi * j / na) * r, math.sin(2 * math.pi * j / na) * r, zs(r) - .07 - .5 * (1 - r / R) - .1) for j in range(na)]
            sk.sweep(g, sk.rect_section(.08, .16, 0), ring, F['steel-dark'], closed=True, up=(0, 0, 1))
    sk.lathe(g, [(.5, -back - 1.3), (.95, -back - 1.2), (.95, -back - .1), (.9, -back + .02)], (0, 0, 0), F['steel'], (16, 10, 8)[T.i], axis=(0, 0, 1), cap0=True)
    sk.box(g, -2.05, 2.05, -.2, .2, -.2, .2, F['steel-dark'], T, bevel=.04, cell=0)
    for x in (-1.3, 1.3):
        sk.box(g, x - .5, x + .5, -1.5, -.3, -back - 2.3, -back - 1.2, F['steel-dark'], T, bevel=.06, cell=0)
        sk.box(g, x - .12, x + .12, -.4, .1, -back - 1.4, -.1, F['steel'], T, bevel=.03, cell=0)
    # the feed: four legs from the face to the horn at the focus, a red lamp on its tip
    fz = feed - back
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        pk.pipe(g, (math.cos(a) * R * .62, math.sin(a) * R * .62, zs(R * .62)), (math.cos(a) * .28, math.sin(a) * .28, fz - .2), .06 if T.i < 2 else .08, F['steel-dark'], T, caps=False)
    sk.lathe(g, [(.5, -.9), (.34, -.5), (.3, .25), (.2, .4), (.06, .5)], (0, 0, fz), F['white'], (12, 8, 6)[T.i], axis=(0, 0, 1), cap0=True)
    sk.lathe(g, [(.09, .5), (.11, .58), (.02, .68)], (0, 0, fz), F['beacon'], (8, 6, 5)[T.i], axis=(0, 0, 1))
    return g


def yoke(L, T):
    P = L['polar']
    drop = P['yokeDrop']
    g = Geo()
    sk.lathe(g, [(2.0, -.22), (2.28, -.16), (2.28, .14), (2.1, .22), (.4, .22)], (0, 0, 0), F['steel-dark'], (28, 16, 10)[T.i], cap0=True)
    for s in (-1, 1):
        # an arm: a tapering box leg from the turntable up to the bearing's housing
        rings = []
        for (y, hx, hz) in ((.2, .3, 1.15), (drop * .55, .27, .7), (drop - .35, .25, .46), (drop + .42, .25, .42)):
            rings.append([(s * 2.05 + x, y, z) for x, z in pk.rrect(hx, hz, .1, (2, 1, 1)[T.i])])
        pk.loft(g, rings, F['dish'], cap0=False, cap1=True, f_cap=F['steel-dark'])
        sk.lathe(g, [(.5, -.08), (.56, 0), (.56, .16), (.3, .24)], (s * 2.3, drop, 0), F['steel-dark'], (14, 8, 6)[T.i], axis=(s, 0, 0), cap1=True)
        if T.detail:
            sk.box(g, s * 2.05 - .31, s * 2.05 + .31, .9, 1.0, -.95, .95, F['steel-dark'], T, bevel=.02, cell=0)
            sk.box(g, s * 2.33 - .02, s * 2.33 + .02 + .12, drop - .9, drop - .6, -.12, .12, F['beacon'], T, bevel=0, cell=0)
    # the drive's cabin on the turntable and a brace between the arms' feet
    pk.body(g, -.7, .7, .22, 1.4, -1.95, -.95, F['white'], r=.12, T=T, f_top=F['steel-dark'])
    sk.box(g, -2.0, 2.0, .22, .5, .7, 1.0, F['steel-dark'], T, bevel=.04, cell=0)
    if T.detail:
        g.face(((-.4, .8, -1.962), (.4, .8, -1.962), (.4, 1.15, -1.962), (-.4, 1.15, -1.962)), F['window-dim'], normal=(0, 0, -1))
    return g


def searchlight(L, T):
    g = Geo()
    sk.lathe(g, [(.2, -.44), (.33, -.36), (.35, .3), (.38, .42), (.36, .44)], (0, 0, 0), F['steel-dark'], (14, 8, 6)[T.i], axis=(0, 0, 1), cap0=True)
    g.face([(c * .34, s * .34, .445) for c, s in pk.ngon(1.0, (14, 8, 6)[T.i])], F['lamp'], normal=(0, 0, 1))
    for s in (-1, 1):
        sk.box(g, s * .37 - .03, s * .37 + .03, -.5, .08, -.07, .07, F['steel'], T, bevel=.01, cell=0)
    sk.box(g, -.4, .4, -.56, -.48, -.1, .1, F['steel'], T, bevel=.01, cell=0)
    return g


def balloon(L, T):
    r = L['polar']['balloonRadius']
    g = Geo()
    n = (14, 8, 5)[T.i]
    prof = []
    for k in range(n + 1):
        a = -math.pi / 2 + math.pi * k / n
        # a teardrop: fuller above its middle, drawn to a neck below
        w = math.cos(a) * (1.0 if a > 0 else (1 - .18 * (math.sin(-a)) ** 2))
        prof.append((max(.03, r * w), r * math.sin(a)))
    sk.lathe(g, prof, (0, 0, 0), F['balloon'], (20, 12, 8)[T.i])
    sk.lathe(g, [(.03, -.16), (.07, -.1), (.05, 0)], (0, -r, 0), F['rope'], 6)
    return g


def sonde(L, T):
    drop = L['polar']['sondeDrop']
    g = Geo()
    pk.body(g, -.16, .16, -.2, .2, -.16, .16, F['white'], r=.04, T=T, f_top=F['orange'])
    sk.box(g, -.164, .164, -.06, .06, -.164, .164, F['orange'], T, bevel=.04, cell=0, skip=('-y', '+y'))
    sk.box(g, -.05, .05, -.3, -.2, -.05, .05, F['beacon'], T, bevel=0, cell=0)
    pk.pipe(g, (0, .2, 0), (0, .18 + drop, 0), .012, F['black'], T, caps=False)
    pk.pipe(g, (.1, .2, .1), (.1, .7, .1), .008, F['steel'], T, caps=False)
    return g


def cups(L, T):
    g = Geo()
    sk.lathe(g, [(.05, -.14), (.05, .04), (.02, .08)], (0, 0, 0), F['steel-dark'], 6, cap0=True)
    for k in range(4):
        a = k * math.pi / 2
        c, s = math.cos(a), math.sin(a)
        pk.pipe(g, (0, 0, 0), (c * .3, 0, -s * .3), .014, F['steel-dark'], T, caps=False)
        n = (5, 3, 2)[T.i]
        prof = [(max(.004, .075 * math.sin(math.pi / 2 * j / n)), -.075 * math.cos(math.pi / 2 * j / n)) for j in range(n + 1)]
        # each cup opens the same way round the hub
        sk.lathe(g, prof, (c * .3, 0, -s * .3), F['red'] if k % 2 == 0 else F['white'], (8, 6, 4)[T.i], axis=(s, 0, c), cap1=True)
    return g


def vent_lamp(L, T):
    g = Geo()
    sk.lathe(g, [(.14, -.03), (.14, .0), (.1, .02)], (0, 0, 0), F['black'], (10, 6, 6)[T.i])
    sk.lathe(g, [(.1, .02), (.09, .06), (.05, .1), (.004, .115)], (0, 0, 0), F['light'], (10, 6, 6)[T.i])
    return g


def floe(L, T):
    g = Geo()
    o = [(math.cos(2 * math.pi * k / 7 + .3) * (.8 + .2 * H(k, 91)), math.sin(2 * math.pi * k / 7 + .3) * (.8 + .2 * H(k, 92))) for k in range(7)]
    rings = [[(x * .86, -.12, z * .86) for x, z in o], [(x, .02, z) for x, z in o], [(x * .93, .12, z * .93) for x, z in o]]
    pk.loft(g, rings, F['ice'], cap0=True, cap1=True, flat=True, f_cap=F['snow'])
    return g


def items(L, T):
    """(name, Geo, rest): every rig prop and where it rests (for the bake's patterns and the previews)."""
    P = L['polar']
    d = P['dish']
    lamp = P['lamp']
    masts = [b for b in L['blocks'] if b['id'].startswith('polar-mast-')]
    m = bx(masts[0])
    vent = [h for h in L['hazards'] if h['kind'] == 'vent'][0]
    r = vent['size'][0] / 2 + .16
    return [('dish', dish(L, T), (d[0], d[1], d[2])), ('yoke', yoke(L, T), (d[0], d[1] - P['yokeDrop'], d[2])), ('searchlight', searchlight(L, T), tuple(lamp)),
            ('balloon', balloon(L, T), (-20.0, 24.0, 10.0)), ('sonde', sonde(L, T), (-20.0, 18.0, 10.0)), ('cups', cups(L, T), ((m[0] + m[1]) / 2, m[3] + .14, (m[4] + m[5]) / 2)),
            ('vent-lamp', vent_lamp(L, T), (vent['a'][0] - r, .03, vent['a'][2] - r)), ('floe', floe(L, T), (1.0, P['sea'] - .14, 4.0))]


# ---------------------------------------------------------------------------------------------------------------------------
# Blocks that break or sink (each in its block's own frame)
# ---------------------------------------------------------------------------------------------------------------------------
def crate(g, hx, hy, hz, T, seed, y0=None):
    """A timber crate: boards in a frame of battens, a brace across each side."""
    y0 = -hy if y0 is None else y0
    y1 = y0 + 2 * hy
    sk.box(g, -hx + .03, hx - .03, y0 + .03, y1 - .03, -hz + .03, hz - .03, F['timber'], T, bevel=.01, cell=0)
    t = .07
    for sx in (-1, 1):
        for sz in (-1, 1):
            sk.box(g, sx * hx - (t if sx > 0 else 0), sx * hx + (t if sx < 0 else 0), y0, y1, sz * hz - (t if sz > 0 else 0), sz * hz + (t if sz < 0 else 0), F['timber-dark'], T, bevel=.012, cell=0)
    for yy in (y0, y1 - t):
        for sz in (-1, 1):
            sk.box(g, -hx + t, hx - t, yy, yy + t, sz * hz - (t if sz > 0 else 0), sz * hz + (t if sz < 0 else 0), F['timber-dark'], T, bevel=.012, cell=0)
        for sx in (-1, 1):
            sk.box(g, sx * hx - (t if sx > 0 else 0), sx * hx + (t if sx < 0 else 0), yy, yy + t, -hz + t, hz - t, F['timber-dark'], T, bevel=.012, cell=0)
    if T.detail:
        for sz in (-1, 1):
            g.face(((-hx + t, y0 + t, sz * (hz - .018)), (-hx + t + .1, y0 + t, sz * (hz - .018)), (hx - t, y1 - t, sz * (hz - .018)), (hx - t - .1, y1 - t, sz * (hz - .018))), F['timber-dark'], normal=(0, 0, sz))
        for sx in (-1, 1):
            g.face(((sx * (hx - .018), y0 + t, -hz + t), (sx * (hx - .018), y0 + t, -hz + t + .1), (sx * (hx - .018), y1 - t, hz - t), (sx * (hx - .018), y1 - t, hz - t - .1)), F['timber-dark'], normal=(sx, 0, 0))
        sk.box(g, -hx + .2, hx - .2, y1 - .001, y1 + .03, -hz + .2, hz - .2, F['snow'], T, bevel=.012, cell=0)


def block_model(b, T):
    i = b['id']
    hx, hy, hz = b['size'][0] / 2, b['size'][1] / 2, b['size'][2] / 2
    g = Geo()
    seed = int(abs(b['pos'][0]) * 5 + abs(b['pos'][2]) * 11) % 499
    if i.startswith('polar-crate-'):
        if hy > .9:
            crate(g, hx, hy / 2, hz, T, seed, y0=-hy)
            crate(g, hx - .04, hy / 2, hz - .04, T, seed + 1, y0=0)
        else:
            crate(g, hx, hy, hz, T, seed)
    elif i.startswith('polar-drums-'):
        sk.box(g, -hx, hx, -hy, -hy + .14, -hz, hz, F['timber-dark'], T, bevel=.02, cell=0)
        for k, (x, z) in enumerate(((-.3, -.3), (.3, -.3), (-.3, .3), (.3, .3))):
            h = 2 * hy - .14
            sk.lathe(g, [(.27, 0), (.29, .03), (.29, .3), (.3, .32), (.29, .34), (.29, h - .36), (.3, h - .34), (.29, h - .32), (.29, h - .03), (.26, h)], (x, -hy + .14, z),
                     F['drum'] if k != 1 else F['red'], (12, 8, 6)[T.i], cap1=True)
    elif i.startswith('polar-thin-'):
        # a plate of thin ice: clear blue, frost and cracks on its top, its broken edge below
        sk.box(g, -hx, hx, -hy, hy, -hz, hz, F['thin'], T, bevel=0, cell=(.8, 0, 0)[T.i], skip=('-y',))
        if T.detail:
            for k in range(7):
                x, z = (H(seed, k, 1) - .5) * 2 * (hx - .5), (H(seed, k, 2) - .5) * 2 * (hz - .5)
                w, l = .25 + H(seed, k, 3) * .5, .5 + H(seed, k, 4) * .9
                g.face(((x - w, hy + .005, z - l), (x + w, hy + .005, z - l * .7), (x + w * .8, hy + .005, z + l), (x - w * .9, hy + .005, z + l * .8)), F['snow'], normal=(0, 1, 0))
            for k in range(5):
                x0, z0 = (H(seed, k, 5) - .5) * 2 * (hx - .2), (H(seed, k, 6) - .5) * 2 * (hz - .2)
                a = H(seed, k, 7) * math.pi
                l = .5 + H(seed, k, 8) * 1.0
                dx, dz = math.cos(a) * l, math.sin(a) * l
                nx, nz = -dz / l * .012, dx / l * .012
                x1, z1 = max(-hx + .05, min(hx - .05, x0 + dx)), max(-hz + .05, min(hz - .05, z0 + dz))
                g.face(((x0 - nx, hy + .008, z0 - nz), (x0 + nx, hy + .008, z0 + nz), (x1 + nx, hy + .008, z1 + nz), (x1 - nx, hy + .008, z1 - nz)), F['seam'], normal=(0, 1, 0))
    elif i.startswith('polar-seam-'):
        # the fracture seam: a dark refrozen strip, a little sunk, a flagged stake at each end
        sk.box(g, -hx, hx, -hy, hy - .012, -hz, hz, F['seam'], T, bevel=0, cell=0, skip=('-y',))
        if T.detail:
            for s in (-1, 1):
                z = s * (hz - .25)
                pk.pipe(g, (0, hy - .012, z), (0, hy + .82, z), .014, F['black'], T)
                g.face(((0, hy + .8, z), (0, hy + .62, z), (.26, hy + .68, z + s * .02), (.26, hy + .78, z + s * .02)), F['orange'], normal=(0, 0, s))
                g.face(((0, hy + .8, z), (0, hy + .62, z), (.26, hy + .68, z + s * .02), (.26, hy + .78, z + s * .02)), F['orange'], normal=(0, 0, -s))
    return g


def blocks(L, T):
    return [(b['id'], block_model(b, T)) for b in L['blocks'] if b['role'] != 'static']


def bank_props(sb, L, T, no_ao):
    ti = T.i
    its = items(L, T)
    bl = blocks(L, T)
    geos = [it[1] for it in its] + [g for _, g in bl]
    aos = [None] * len(geos)
    if not no_ao:
        apart = [g.transformed([1, 0, 0, 40.0 * i, 0, 1, 0, 0, 0, 0, 1, 0]) for i, g in enumerate(geos)]
        aos = sk.bake_ao(apart, distance=.4, samples=(24, 12, 8)[ti])
        aos = [1.0 - (1.0 - a) * .75 for a in aos]
    for (name, g, rest), ao in zip(its, aos):
        if g.faces == 0:
            continue
        Pw = g.arrays()[0].astype(np.float64) + np.array(rest)
        bk = sk.Bake(g, ao, world=Pw, floors=(), seed=7)
        sb.add('rig/%s/%d' % (name, ti), 'rig', sk.to_mesh('rig/%s/%d' % (name, ti), g, bk))
    by = {b['id']: b for b in L['blocks']}
    for (bid, g), ao in zip(bl, aos[len(its):]):
        if g.faces == 0:
            continue
        Pw = g.arrays()[0].astype(np.float64) + np.array(by[bid]['pos'])
        bk = sk.Bake(g, ao, world=Pw, floors=(0.0, 3.2), seed=9)
        sb.add('block/%s/%d' % (bid, ti), 'block', sk.to_mesh('block/%s/%d' % (bid, ti), g, bk))


# ---------------------------------------------------------------------------------------------------------------------------
# The far backdrop (one tier)
# ---------------------------------------------------------------------------------------------------------------------------
def berg(g, x, z, w, h, seed, sea):
    """An iceberg: a faceted mass, snow on its shoulders, blue where it is sheer, dark at the waterline."""
    n, m = 12, 6
    rings = []
    for j in range(m):
        v = j / (m - 1)
        y = sea - 2 + (h + 2) * v
        s = (1 - v ** 1.6 * .8) * (1 + .12 * math.sin(v * 5 + seed))
        rings.append([(x + math.cos(2 * math.pi * k / n) * w * .5 * s * (.7 + .5 * H(seed, k, j)) + (H(seed, j, 3) - .5) * w * .2 * v,
                       y + (H(seed, k, j, 5) - .5) * h * .12 * (1 if j else 0),
                       z + math.sin(2 * math.pi * k / n) * w * .34 * s * (.7 + .5 * H(seed, j, k))) for k in range(n)])
    pk.loft(g, rings, F['ice'], cap0=False, cap1=True, flat=True, f_cap=F['snow'],
            pick=lambda nn, cc: F['snow'] if nn[1] > .45 else (F['ice-dark'] if cc[1] < sea + 1.5 else (F['ice-deep'] if H(seed, int(cc[0] * .3), int(cc[1] * .5)) > .5 else F['ice'])))


def far(L):
    """(name, Geo): the backdrop's chunks."""
    P = L['polar']
    sea, edge, sx = P['sea'], P['shelfZ'], P['shelfX']
    out = []
    # the sea, south of the shelf out to the horizon (the play space's own strip is the near scene's)
    g = Geo()
    for (x0, x1, z0, z1) in ((-1100, 1100, -1000, edge - 36), (-1100, -60, edge - 36, edge + 1.2), (60, 1100, edge - 36, edge + 1.2)):
        sk.grid_face(g, (x0, sea, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['water'], (0, 1, 0), 120)
    out.append(('far-sea', g))
    # the shelf beyond the ridges: snow out to the horizon, the wind's ridges across it, its cliff along the sea east and west
    g = Geo()
    def plain(x0, x1, z0, z1, cell):
        nx, nz = max(2, int((x1 - x0) / cell) + 1), max(2, int((z1 - z0) / cell) + 1)
        X, Z = np.meshgrid(np.linspace(x0, x1, nx), np.linspace(z0, z1, nz), indexing='ij')
        d = np.maximum(np.maximum(np.abs(X) - 60, 0), np.maximum(np.abs(Z) - 60, 0))
        Y = pk.noise(np.stack([X, np.zeros_like(X), Z], axis=-1), .012, 5, 3).reshape(X.shape) * np.minimum(d / 120, 1) * 9 + np.minimum(d / 500, 1) ** 2 * 14
        pk.surf(g, np.stack([X, Y, Z], axis=2), F['snow'], facing=(0, 1, 0), pick=lambda nn, cc: F['snow'] if H(int(cc[0] / 14), int(cc[2] / 14), 2) > .35 else F['snow-shade'])
    plain(-1100, -sx - 3.4, edge, 1000, 46)
    plain(sx + 3.4, 1100, edge, 1000, 46)
    plain(-sx - 3.4, sx + 3.4, 31.4, 1000, 46)
    for s in (-1, 1):
        n = 40
        Pw = np.zeros((n, 4, 3))
        for i in range(n):
            x = s * (sx + 3.4 + (1100 - sx - 3.4) * (i / (n - 1)) ** 1.6)
            for j, (y, d) in enumerate(((.0, 0), (-.5, .4), (sea, 1.2), (sea - 3, 1.6))):
                Pw[i, j] = (x, y, edge - d - 2.2 * H(i, j, 41) * (j > 0))
        pk.surf(g, Pw, F['ice-deep'], facing=(0, 0, -1), flat=True, pick=lambda nn, cc: F['ice'] if cc[1] > -.6 else (F['ice-deep'] if cc[1] > sea - .5 else F['ice-dark']))
    for k in range(120):
        a = H(k, 51) * 2 * math.pi
        r = 60 + H(k, 52) ** .8 * 380
        x, z = math.cos(a) * r, math.sin(a) * r
        if z < edge + 6 or (abs(x) < sx + 8 and z < 36):
            continue
        l, w, h = 5 + H(k, 53) * 16, 1.4 + H(k, 54) * 2.6, .3 + H(k, 55) * .9
        yaw = .45 + (H(k, 56) - .5) * .4
        c, sn = math.cos(yaw), math.sin(yaw)
        ring = [(x + c * l * u - sn * w * v, 0 if abs(u) > .99 or abs(v) > .99 else h, z + sn * l * u + c * w * v) for (u, v) in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        top = [(x + c * l * u * .7 - sn * w * v * .3, h, z + sn * l * u * .7 + c * w * v * .3) for (u, v) in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        pk.loft(g, [ring, top], F['snow-shade'], cap0=False, cap1=True, flat=True, f_cap=F['snow'])
    out.append(('far-shelf', g))
    # icebergs standing in the sea, and floes
    g = Geo()
    for k, (x, z, w, h) in enumerate(((-58, -100, 34, 24), (46, -132, 46, 32), (-150, -214, 84, 46), (124, -264, 72, 40), (10, -350, 120, 56), (-270, -150, 64, 30), (256, -142, 56, 28), (-420, -420, 150, 70), (460, -520, 170, 80))):
        berg(g, x, z, w, h, k * 7 + 3, sea)
    for k in range(70):
        x, z = (H(k, 61) - .5) * 420, edge - 44 - H(k, 62) ** 1.4 * 260
        r = 1.5 + H(k, 63) * 6
        o = pk.ngon(r, 6, phase=H(k, 64) * 3, sz=.6 + .4 * H(k, 65))
        pk.loft(g, [[(x + a, sea - .05, z + b) for a, b in o], [(x + a * .9, sea + .2 + k * .004, z + b * .9) for a, b in o]], F['ice'], cap0=False, cap1=True, flat=True, f_cap=F['snow'])
    out.append(('far-bergs', g))
    # the ice cap's rim: ranges round the north of the horizon, snow on their shoulders, dark rock where they are sheer
    g = Geo()
    na, nr = 150, 9
    Pm = np.zeros((na, nr, 3))
    for i in range(na):
        a = -.42 + (math.pi + .84) * i / (na - 1)
        for j in range(nr):
            v = j / (nr - 1)
            r = 560 + 400 * v
            x, z = math.cos(a) * r, math.sin(a) * r
            Pm[i, j] = (x, 0, z)
    n1 = pk.noise(Pm, .0042, 61, 4).reshape(na, nr)
    n2 = pk.noise(Pm, .016, 67, 3).reshape(na, nr)
    V = np.linspace(0, 1, nr)[None, :]
    prof = np.sin(np.clip(V * 1.25, 0, 1) * math.pi) ** .8
    ends = np.sin(np.linspace(0, 1, na) * math.pi)[:, None] ** .35
    Pm[:, :, 1] = 4 + prof * ends * (70 + 190 * np.clip(n1 + .5, 0, 1) ** 1.6 + 60 * n2)
    pk.surf(g, Pm, F['snow'], facing=(0, 1, 0), flat=True, pick=lambda nn, cc: F['snow'] if nn[1] > .62 else (F['snow-shade'] if nn[1] > .48 else F['rock']))
    out.append(('far-range', g))
    # the far station across the shelf: a met tower with its lamp, the fuel depot's lit huts
    g = Geo()
    T1 = sk.Tier(1)
    sk.lathe(g, [(.6, 0), (.3, 30)], (-118, 2, 84), F['white'], 6, cap1=True)
    sk.box(g, -118.5, -117.5, 32, 33, 83.5, 84.5, F['beacon'], T1, bevel=0, cell=0)
    for k in range(4):
        x = 96 + k * 7
        sk.box(g, x - 2.7, x + 2.7, 1, 3.8, 68.2, 71.8, F['red-paint'] if k % 2 == 0 else F['blue-paint'], T1, bevel=.1, cell=0)
        sk.box(g, x - 2.9, x + 2.9, 3.8, 4.1, 68, 72, F['snow'], T1, bevel=.1, cell=0)
        g.face(((x - .7, 2.2, 68.19), (x + .7, 2.2, 68.19), (x + .7, 3.0, 68.19), (x - .7, 3.0, 68.19)), F['window'], normal=(0, 0, -1))
    out.append(('far-station', g))
    return out


def bank_far(sb, L):
    for name, g in far(L):
        bk = sk.Bake(g, None, floors=(), seed=5)
        sb.add('static/%s/2' % name, 'static', sk.to_mesh('static/%s/2' % name, g, bk))
