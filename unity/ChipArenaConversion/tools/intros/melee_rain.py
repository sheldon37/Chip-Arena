"""Still Rain's set (the Katana; docs/INTROS-MELEE.md): the Rain Garden, a walled stone courtyard at night. A granite path runs from
the wooden gate (behind the pilot) to the veranda (behind the opening camera) between two shallow rain pools; plastered walls with
tiled caps close the sides, bamboo rises over them, a stone lantern and a water basin stand in the moss on the pilot's left, and three
bamboo stalks inside the wall on its right are cut by the one cut (rigs `bamboo-cut-0..2`: each stalk's top above its slanted cut,
resting on its stump). Set frame: melee_kit's (the pilot at the origin facing +z)."""
import math

import numpy as np

import melee_kit as mk
from melee_kit import sk

F_GRANITE = mk.fin('rain.granite', 0x6b6d70, sk.DETAIL_CARVED, smooth=.55, dirt=.8, moss=.35, wear=.35)
F_GRANITE_DK = mk.fin('rain.granite.dark', 0x55575b, sk.DETAIL_CARVED, smooth=.6, dirt=.9, moss=.5, wear=.2)
F_JOINT = mk.fin('rain.joint', 0x26272a, smooth=.5, dirt=.4)
F_WATER = mk.fin('rain.water', 0x10171f, smooth=.97, dirt=0)
F_POOLBED = mk.fin('rain.poolbed', 0x1c1f22, smooth=.4, dirt=.3)
F_MOSS = mk.fin('rain.moss', 0x34452a, smooth=.25, dirt=.6)
F_GRAVEL = mk.fin('rain.gravel', 0x4a4a48, sk.DETAIL_FLAG, smooth=.45, dirt=.7, moss=.2)
F_WOOD = mk.fin('rain.wood', 0x3a281d, sk.DETAIL_WOOD, smooth=.35, dirt=.7, param=1, wear=.25)
F_WOOD_H = mk.fin('rain.wood.h', 0x3a281d, sk.DETAIL_WOOD, smooth=.35, dirt=.7, param=0, wear=.25)
F_WOOD_DECK = mk.fin('rain.wood.deck', 0x4a3527, sk.DETAIL_WOOD, smooth=.5, dirt=.6, param=2, wear=.3)
F_IRON = mk.fin('rain.iron', 0x1e1c1b, sk.DETAIL_IRON, smooth=.45, metal=.6, dirt=.6)
F_PLASTER = mk.fin('rain.plaster', 0xb9b3a6, sk.DETAIL_PLASTER, smooth=.15, dirt=1.2, moss=.15)
F_PLINTH = mk.fin('rain.plinth', 0x4b4c4d, sk.DETAIL_ASHLAR, smooth=.5, dirt=1, moss=.45)
F_TILE = mk.fin('rain.tile', 0x2a2f35, smooth=.65, metal=.1, dirt=.5)
F_BAMBOO = mk.fin('rain.bamboo', 0x6c8a43, smooth=.55, dirt=.3, wear=.2)
F_BAMBOO_OLD = mk.fin('rain.bamboo.old', 0x8f8d57, smooth=.5, dirt=.4)
F_NODE = mk.fin('rain.node', 0x95a35e, smooth=.5, dirt=.3)
F_CUT = mk.fin('rain.cut', 0xd8d2a4, smooth=.3, dirt=0)
F_LEAF = mk.fin('rain.leaf', 0x3e5a2a, smooth=.35, dirt=.2)
F_LEAF_LT = mk.fin('rain.leaf.light', 0x587a35, smooth=.35, dirt=.2)
F_SHRUB = mk.fin('rain.shrub', 0x2f4425, smooth=.3, dirt=.3)
F_LANTERN = mk.fin('rain.lantern', 0x76746c, sk.DETAIL_CARVED, smooth=.4, dirt=1.1, moss=.6, wear=.3)
F_FLAME = mk.fin('rain.lantern.glow', 0xffb35c, glow=2.6)
F_SHOJI = mk.fin('rain.shoji.glow', 0xffd7a0, glow=1.15)
F_FAR = mk.fin('rain.far', 0x15181d, smooth=.2, dirt=0)

PATH_HALF = .82
POOL_HALF = 3.4
WALL_Z = -7.6
GATE_HALF = 1.55
SIDE_X = 8.2
VERANDA_Z = 7.2
# the cut's plane (Core NativeIntroStillRain.PlaneY, here in Unity space): y = PLANE_BASE - PLANE_RISE x + PLANE_BACK z
PLANE_BASE, PLANE_RISE, PLANE_BACK = .95, .28, .08


def plane_y(x, z):
    return PLANE_BASE - PLANE_RISE * x + PLANE_BACK * z


# the three stalks the cut takes: (x, z, radius, height); each is cut where it crosses the plane
CUT_STALKS = ((-2.35, -3.0, .055, 6.6), (-2.85, -3.55, .05, 7.2), (-3.3, -2.75, .06, 6.2))


def build(T):
    s = mk.SetPiece('rain')
    g = mk.Geo()
    tl = mk.Tally(g)
    floor(g, T)
    tl('floor')
    gate(g, T)
    tl('gate')
    walls(g, T)
    tl('walls')
    lantern(g, T, (2.55, -1.9))
    basin(g, T, (3.6, -1.1))
    tl('lantern+basin')
    shrubs(g, T)
    tl('shrubs')
    grove(g, T)
    tl('grove')
    veranda(g, T)
    tl('veranda')
    far(g, T)
    tl('far')
    s.tally = tl
    s.static[T.i] = g
    for k, st in enumerate(CUT_STALKS):
        stump, top, rest = cut_stalk(T, st, k)
        g.extend(stump)
        s.rig('bamboo-cut-%d' % k, T.i, top, rest)
    s.views = [
        ('front', (0.0, 1.55, 6.2), (0.0, 1.3, -2.0), 46),
        ('low', (-1.4, .35, 3.4), (0.0, 1.1, -3.0), 50),
        ('side', (5.8, 1.6, .6), (-1.0, 1.0, -2.0), 52),
        ('reverse', (.6, 1.6, -5.8), (0.0, 1.2, 4.0), 50),
        ('high', (2.5, 6.5, 5.5), (0.0, 0.0, -1.5), 55),
        ('stalks', (1.4, 1.3, 1.6), (-2.8, 1.4, -3.1), 45),
    ]
    return s


# ---- the floor: the path, the pools, the moss beds ----
def floor(g, T):
    r = mk.rng(11)
    # the path's slabs: long granite blocks across it, joints between them
    z = 7.0
    while z > WALL_Z + .4:
        L = float(r.uniform(.7, 1.15))
        z0 = max(WALL_Z + .4, z - L)
        w0 = PATH_HALF + float(r.uniform(-.03, .03))
        jag = float(r.uniform(-.04, .04))
        poly = [(-w0, z0 + .012), (w0, z0 + .012 + jag), (w0, z - .012 + jag), (-w0, z - .012)]
        mk.slab(g, poly, -.14, float(r.uniform(-.006, .006)), F_GRANITE, T, bevel=.03)
        z = z0
    # the joint grit under the slabs
    sk.box(g, -PATH_HALF + .01, PATH_HALF - .01, -.16, -.012, WALL_Z + .4, 7.0, F_JOINT, T, bevel=0, cell=0, skip=('-y',))
    # the pools: their beds, their stone kerbs, the water
    for side in (-1, 1):
        x0, x1 = side * (PATH_HALF + .02), side * POOL_HALF
        lo, hi = min(x0, x1), max(x0, x1)
        sk.box(g, lo, hi, -.3, -.2, WALL_Z + .5, 5.8, F_POOLBED, T, bevel=0, cell=0, skip=('-y',))
        sk.grid_face(g, (lo, -.055, WALL_Z + .5), (hi - lo, 0, 0), (0, 0, 5.8 - WALL_Z - .5), 0, 0, F_WATER, (0, 1, 0), (2.0, 4.0, 8.0)[T.i])
        # the kerb on the pool's far side: rough granite blocks
        kx = side * (POOL_HALF + .18)
        z = 5.8
        while z > WALL_Z + .5:
            L = float(r.uniform(.6, 1.0))
            z0 = max(WALL_Z + .5, z - L)
            poly = [(kx - .2, z0 + .01), (kx + .2, z0 + .01), (kx + .2, z - .01), (kx - .2, z - .01)]
            mk.slab(g, poly, -.3, .05 + float(r.uniform(-.01, .02)), F_GRANITE_DK, T, bevel=.035)
            z = z0
        # stepping stones in the pools, a few, wet
        for k in range(3 if T.i < 2 else 1):
            cx = side * float(r.uniform(PATH_HALF + .7, POOL_HALF - .5))
            cz = float(r.uniform(-5.5, 4.0))
            mk.rock(g, (cx, -.06, cz), (float(r.uniform(.22, .34)), .07, float(r.uniform(.18, .3))), F_GRANITE_DK, T, seed=100 + k * 7 + side, rough=.18, sink=0)
        # pool across the front (the camera's side) ends at a kerb
    for side in (-1, 1):
        lo, hi = sorted((side * PATH_HALF, side * (POOL_HALF + .38)))
        sk.box(g, lo, hi, -.3, .04, 5.8, 6.1, F_GRANITE_DK, T, bevel=.03, cell=0)
        sk.box(g, lo, hi, -.3, .04, WALL_Z + .2, WALL_Z + .5, F_GRANITE_DK, T, bevel=.03, cell=0)
    # the moss beds outside the kerbs, up to the walls, and the gravel apron in front of the veranda
    for side in (-1, 1):
        lo, hi = sorted((side * (POOL_HALF + .38), side * SIDE_X))
        sk.box(g, lo, hi, -.3, -.01, WALL_Z, 6.1, F_MOSS, T, bevel=0, cell=(2.0, 4.0, 0)[T.i], skip=('-y',))
    sk.box(g, -SIDE_X, SIDE_X, -.3, -.015, 6.1, VERANDA_Z, F_GRAVEL, T, bevel=0, cell=(2.0, 4.0, 0)[T.i], skip=('-y',))
    # stones set in the moss
    for k in range((14, 8, 4)[T.i]):
        side = 1 if k % 2 else -1
        cx = side * float(r.uniform(POOL_HALF + .7, SIDE_X - .6))
        cz = float(r.uniform(WALL_Z + .8, 5.5))
        sz = float(r.uniform(.18, .55))
        mk.rock(g, (cx, -.02, cz), (sz, sz * float(r.uniform(.4, .8)), sz * float(r.uniform(.7, 1.1))), F_GRANITE_DK, T, seed=200 + k, rough=.25, turn=float(r.uniform(0, 3)))


# ---- the gate (a roofed wooden gate, its doors ajar) ----
def gate(g, T):
    z = WALL_Z
    post = .17
    H = 3.05
    for side in (-1, 1):
        x = side * GATE_HALF
        sk.box(g, x - post, x + post, -.1, H, z - post, z + post, F_WOOD, T, bevel=.02, cell=0)
        # stone foot under each post
        mk.slab(g, mk.circle(x, z, .3, (10, 8, 6)[T.i], .3), -.2, .12, F_GRANITE_DK, T, bevel=.03)
        # the side posts of the roof's wings
        sk.box(g, x + side * .9 - .1, x + side * .9 + .1, -.1, 2.55, z - .62 - .1, z - .62 + .1, F_WOOD, T, bevel=.015, cell=0)
    # the tie beam through the posts and the lintel
    sk.box(g, -GATE_HALF - .35, GATE_HALF + .35, 2.35, 2.55, z - .09, z + .09, F_WOOD_H, T, bevel=.015, cell=0)
    sk.box(g, -GATE_HALF - .2, GATE_HALF + .2, 2.85, 3.05, z - .12, z + .12, F_WOOD_H, T, bevel=.015, cell=0)
    # the doors: two leaves, the left ajar, iron bands and studs
    for side, ang in ((-1, .0), (1, -.55)):
        hinge = (side * (GATE_HALF - post), z - .05)
        leaf = mk.Geo()
        w = GATE_HALF - post - .02
        sk.box(leaf, 0, w, .05, 2.3, -.04, .04, F_WOOD, T, bevel=.01, cell=0)
        for y in (.45, 1.25, 2.0):
            sk.box(leaf, .02, w - .02, y, y + .09, .04, .055, F_IRON, T, bevel=0, cell=0)
            if T.detail:
                for k in range(4):
                    sk.box(leaf, .1 + k * (w - .2) / 3 - .02, .1 + k * (w - .2) / 3 + .02, y + .025, y + .065, .055, .075, F_IRON, T, bevel=0, cell=0)
        # the leaf opens from its hinge post, toward the inside (-z)
        q = sk.quat_axis(0, 1, 0, ang * -side)
        if side > 0:
            leafm = leaf.transformed(sk.affine(sk.quat_axis(0, 1, 0, math.pi), (0, 0, 0)))
        else:
            leafm = leaf
        g.extend(leafm.transformed(sk.affine(q, (hinge[0], 0, hinge[1]))))
    # the roof: a gabled roof along x with deep curved eaves, tiled
    roof(g, T, (0.0, 3.05, z), 2.75, 1.55, .55, .9)


def roof(g, T, at, half_x, half_z, rise, lift, f=F_TILE, tiles=True, under=True, coarse=False):
    """A small gabled tile roof, its ridge along x at `at` (centre of the ridge's foot) + rise; the eaves curving up at the ends by
    `lift` x the rise; tiles as rolled ridges down the slope; a ridge beam with raised ends."""
    cx, y0, cz = at
    nx = (6, 4, 3)[T.i] if coarse else (16, 9, 4)[T.i]
    nz = (2, 2, 1)[T.i] if coarse else (6, 4, 2)[T.i]

    def surf(u, v, side):
        # u across (x, -1..1), v down the slope (0 ridge, 1 eave)
        x = cx + u * half_x
        z = cz + side * v * half_z
        y = y0 + rise * (1 - v) + lift * rise * (abs(u) ** 3) * (v ** 1.5) + .08 * math.sin(v * math.pi) * 0
        return (x, y, z)
    for side in (-1, 1):
        for i in range(nx):
            for j in range(nz):
                a = surf(-1 + 2 * i / nx, j / nz, side)
                b = surf(-1 + 2 * (i + 1) / nx, j / nz, side)
                c = surf(-1 + 2 * (i + 1) / nx, (j + 1) / nz, side)
                d = surf(-1 + 2 * i / nx, (j + 1) / nz, side)
                n = sk._norm(sk._cross(sk._sub(b, a), sk._sub(d, a)))
                if n[1] < 0:
                    n = (-n[0], -n[1], -n[2])
                g.face((a, b, c, d), f, normal=n)
                # the underside, plain dark wood
                if under:
                    g.face((d, c, b, a), F_WOOD_H, normal=(-n[0], -n[1], -n[2]))
        # the rolled tiles: a ridge every 0.24 m down the slope
        if T.detail and tiles:
            k = int(half_x * 2 / .3)
            for i in range(k + 1):
                u = -1 + 2 * i / k
                path = [surf(u, v, side) for v in np.linspace(0, 1, (5, 4, 3)[T.i])]
                path = [(p[0], p[1] + .035, p[2]) for p in path]
                sk.sweep(g, [(-.05, 0), (-.035, .03), (0, .045), (.035, .03), (.05, 0)], path, f, caps=False, up=(0, 1, 0))
        # the eave's edge: a thin board
        e0, e1 = surf(-1, 1, side), surf(1, 1, side)
        pts = [surf(-1 + 2 * i / nx, 1, side) for i in range(nx + 1)]
        sk.sweep(g, sk.rect_section(.06, .1), [(p[0], p[1] - .02, p[2]) for p in pts], F_WOOD_H, up=(0, 1, 0))
    # ridge beam
    ridge = [surf(-1 + 2 * i / nx, 0, 1) for i in range(nx + 1)]
    ridge = [(p[0], p[1] + .06, p[2]) for p in ridge]
    sk.sweep(g, sk.rect_section(.16, .16, .03), ridge, f, up=(0, 1, 0))
    for side in (-1, 1):
        p = ridge[0 if side < 0 else -1]
        mk.rock(g, (p[0], p[1] + .08, p[2]), (.12, .14, .1), f, T, seed=7 + side, rough=.05, flat_bottom=False)


# ---- the walls: plaster over a stone plinth, a tiled cap ----
def walls(g, T):
    H = 2.35
    th = .22
    runs = [((GATE_HALF + .9, WALL_Z), (SIDE_X, WALL_Z)), ((-GATE_HALF - .9, WALL_Z), (-SIDE_X, WALL_Z)),
            ((SIDE_X, WALL_Z), (SIDE_X, 6.4)), ((-SIDE_X, WALL_Z), (-SIDE_X, 6.4))]
    for (a, b) in runs:
        x0, z0 = a
        x1, z1 = b
        along_x = abs(x1 - x0) > abs(z1 - z0)
        if along_x:
            lo, hi = sorted((x0, x1))
            sk.box(g, lo, hi, -.1, .55, z0 - th - .04, z0 + th + .04, F_PLINTH, T, bevel=.02, cell=(1.6, 4.0, 0)[T.i])
            sk.box(g, lo, hi, .55, H, z0 - th, z0 + th, F_PLASTER, T, bevel=.015, cell=(1.2, 3.0, 0)[T.i])
            # timber posts in the plaster every 1.8 m
            n = int((hi - lo) / 1.8)
            for k in range(n + 1):
                x = lo + (hi - lo) * k / max(1, n)
                sk.box(g, x - .08, x + .08, .55, H, z0 + th - .005, z0 + th + .03, F_WOOD, T, bevel=.01, cell=0)
            wall_cap(g, T, (lo, z0), (hi, z0), H)
        else:
            lo, hi = sorted((z0, z1))
            sk.box(g, x0 - th - .04, x0 + th + .04, -.1, .55, lo, hi, F_PLINTH, T, bevel=.02, cell=(1.6, 4.0, 0)[T.i])
            sk.box(g, x0 - th, x0 + th, .55, H, lo, hi, F_PLASTER, T, bevel=.015, cell=(1.2, 3.0, 0)[T.i])
            n = int((hi - lo) / 1.8)
            inner = -math.copysign(1, x0)
            for k in range(n + 1):
                z = lo + (hi - lo) * k / max(1, n)
                xi = x0 + inner * th
                sk.box(g, min(xi, xi + inner * .03) - (0 if inner > 0 else 0), max(xi, xi + inner * .03), .55, H, z - .08, z + .08, F_WOOD, T, bevel=.01, cell=0)
            wall_cap(g, T, (x0, lo), (x0, hi), H)


def wall_cap(g, T, a, b, H):
    """The wall's little tiled roof: a ridge and two short slopes along a..b."""
    (x0, z0), (x1, z1) = a, b
    d = sk._norm((x1 - x0, 0, z1 - z0))
    side = (-d[2], 0, d[0])
    L = math.hypot(x1 - x0, z1 - z0)
    n = max(1, int(L / (1.2, 2.4, 6.0)[T.i]))
    for k in range(n):
        u0, u1 = k / n, (k + 1) / n
        p0 = (x0 + (x1 - x0) * u0, H, z0 + (z1 - z0) * u0)
        p1 = (x0 + (x1 - x0) * u1, H, z0 + (z1 - z0) * u1)
        for s in (-1, 1):
            ridge0, ridge1 = (p0[0], H + .24, p0[2]), (p1[0], H + .24, p1[2])
            e0 = (p0[0] + side[0] * s * .42, H - .02, p0[2] + side[2] * s * .42)
            e1 = (p1[0] + side[0] * s * .42, H - .02, p1[2] + side[2] * s * .42)
            nrm = sk._norm(sk._cross(sk._sub(ridge1, ridge0), sk._sub(e0, ridge0)))
            if nrm[1] < 0:
                nrm = (-nrm[0], -nrm[1], -nrm[2])
            g.face((ridge0, ridge1, e1, e0), F_TILE, normal=nrm)
            g.face((e0, e1, ridge1, ridge0), F_WOOD_H, normal=(-nrm[0], -nrm[1], -nrm[2]))
    sk.sweep(g, sk.rect_section(.1, .1, .02), [(x0, H + .26, z0), (x1, H + .26, z1)], F_TILE, up=(0, 1, 0))


# ---- the stone lantern (a kasuga-style toro: base, shaft, firebox with lit windows, a curved cap, a jewel) ----
def lantern(g, T, at):
    x, z = at
    n = (10, 8, 6)[T.i]
    mk.slab(g, mk.circle(x, z, .34, 6, .26), -.05, .14, F_LANTERN, T, bevel=.03)
    sk.lathe(g, [(.12, .14), (.1, .3), (.09, .62), (.1, .78), (.13, .86)], (x, 0, z), F_LANTERN, n, cap1=True)
    mk.slab(g, mk.circle(x, z, .3, 6, .26), .86, .98, F_LANTERN, T, bevel=.025)
    # the firebox: four posts with the glowing paper between
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        px, pz = x + math.cos(a) * .17, z + math.sin(a) * .17
        sk.box(g, px - .035, px + .035, .98, 1.3, pz - .035, pz + .035, F_LANTERN, T, bevel=.008, cell=0)
    sk.box(g, x - .14, x + .14, 1.0, 1.28, z - .14, z + .14, F_FLAME, T, bevel=0, cell=0)
    # the cap: a hexagonal roof with up-turned corners
    hexa = mk.circle(x, z, .42, 6, .26)
    apex = (x, 1.58, z)
    for k in range(6):
        a, b = hexa[k], hexa[(k + 1) % 6]
        pa, pb = (a[0], 1.33, a[1]), (b[0], 1.33, b[1])
        nrm = sk._norm(sk._cross(sk._sub(pb, pa), sk._sub(apex, pa)))
        if nrm[1] < 0:
            nrm = (-nrm[0], -nrm[1], -nrm[2])
        g.face((pa, pb, apex), F_LANTERN, normal=nrm)
        g.face((pa, pb, (b[0], 1.3, b[1]), (a[0], 1.3, a[1])), F_LANTERN)
        if T.detail:
            mk.rock(g, (a[0], 1.36, a[1]), (.04, .05, .04), F_LANTERN, T, seed=31 + k, rough=.1, flat_bottom=False, level=1)
    g.face([(p[0], 1.3, p[1]) for p in hexa][::-1], F_LANTERN, normal=(0, -1, 0))
    sk.lathe(g, [(.0, 1.56), (.07, 1.6), (.08, 1.66), (.04, 1.74), (0, 1.79)], (x, 0, z), F_LANTERN, n)


def basin(g, T, at):
    """A stone water basin (a carved block with a dark water face) fed by a bamboo spout."""
    x, z = at
    mk.rock(g, (x, -.02, z), (.32, .3, .3), F_LANTERN, T, seed=41, rough=.12)
    sk.grid_face(g, (x - .16, .27, z - .16), (.32, 0, 0), (0, 0, .32), 0, 0, F_WATER, (0, 1, 0), 1.0)
    sp = [(x + .75, .62, z - .25), (x + .45, .58, z - .12), (x + .12, .5, z)]
    sk.tube(g, sp, .028, F_BAMBOO, (6, 5, 4)[T.i])
    sk.box(g, x + .72, x + .8, -.02, .66, z - .3, z - .2, F_BAMBOO_OLD, T, bevel=.005, cell=0)
    for k in range(3):
        mk.rock(g, (x - .45 + k * .3, -.02, z + .42 + .06 * k), (.12, .06, .1), F_GRANITE_DK, T, seed=50 + k, rough=.2)


def shrubs(g, T):
    """Clipped mounds (karikomi) on the moss, faceted."""
    r = mk.rng(61)
    spots = [(4.6, -5.6, .9), (5.6, -4.6, .7), (-5.2, 2.4, .8), (5.4, 3.2, 1.0), (-6.4, -1.2, .7), (6.6, -.8, .6)]
    for k, (x, z, s) in enumerate(spots):
        mk.rock(g, (x, -.05, z), (s, s * .6, s * .8), F_SHRUB, T, seed=70 + k, rough=.14, facets=True)


def stalk(g, T, base, height, r, lean, seed, top_leaves=True, f=F_BAMBOO, from_h=0.0, nodes=False, leaves=None):
    """A bamboo culm from `base` to `height`, leaning by (dx, dz) at the top, its nodes as rings; a fan of leaves at the top."""
    x, y, z = base
    sides = (6, 5, 4)[T.i] if nodes else (5, 4, 4)[T.i]
    n = max(2, int((height - from_h) / (1.0 if nodes else 1.6)))
    pts = []
    for i in range(n + 1):
        u = from_h / height + (1 - from_h / height) * i / n
        pts.append((x + lean[0] * u * u, y + height * u, z + lean[1] * u * u))
    sk.tube(g, pts, r, f, sides, caps=False)
    if T.detail and nodes:
        # the nodes every 0.42 m: a slight swelling ring
        k = int((height - from_h) / .42)
        for i in range(1, k):
            u = from_h / height + (1 - from_h / height) * i / k
            p = (x + lean[0] * u * u, y + height * u, z + lean[1] * u * u)
            sk.lathe(g, [(r * 1.14, -.01), (r * 1.14, .01)], p, F_NODE, sides)
    if top_leaves:
        rr = mk.rng(seed)
        nl = ((9, 5, 2) if nodes else (6, 3, 1))[T.i] if leaves is None else leaves[T.i]
        top = pts[-1]
        for k in range(nl):
            h = float(rr.uniform(.55, 1.0))
            i = min(len(pts) - 1, max(1, int(h * len(pts))))
            p = pts[i]
            a = float(rr.uniform(0, 2 * math.pi))
            d = (math.cos(a), float(rr.uniform(-.1, .3)), math.sin(a))
            mk.blade_leaf(g, p, d, float(rr.uniform(.35, .6)), float(rr.uniform(.035, .06)), F_LEAF if k % 3 else F_LEAF_LT, droop=.5)


def grove(g, T):
    """Bamboo behind the walls (both sides and behind the gate) and a clump inside on the right, round the cut stalks."""
    r = mk.rng(81)
    count = (34, 20, 12)[T.i]
    for k in range(count):
        side = (-1, 1)[k % 2]
        if k % 3 == 0:
            x = float(r.uniform(-SIDE_X + .5, SIDE_X - .5))
            z = float(r.uniform(WALL_Z - 3.5, WALL_Z - .8))
        else:
            x = side * float(r.uniform(SIDE_X + .7, SIDE_X + 3.5))
            z = float(r.uniform(WALL_Z - 2, 6))
        h = float(r.uniform(6.5, 10.5))
        stalk(g, T, (x, -.1, z), h, float(r.uniform(.045, .075)), (float(r.uniform(-.8, .8)), float(r.uniform(-.8, .8))), 300 + k,
              f=F_BAMBOO if k % 5 else F_BAMBOO_OLD)
    # the clump inside the wall on the right, round the cut stalks
    for k, (x, z) in enumerate(((-2.0, -3.6), (-3.7, -3.4), (-2.6, -2.3), (-3.9, -2.2), (-3.2, -4.2), (-1.9, -4.3))):
        if T.i == 2 and k > 2:
            break
        stalk(g, T, (x, -.05, z), float(r.uniform(5.5, 7.5)), float(r.uniform(.045, .065)), (float(r.uniform(-.5, .3)), float(r.uniform(-.4, .2))), 400 + k, nodes=True)
    # the clump's root mound
    mk.rock(g, (-2.9, -.08, -3.2), (1.4, .16, 1.2), F_MOSS, T, seed=91, rough=.2)


def cut_stalk(T, st, k):
    """A stalk the cut takes: the stump (static, its top a slanted cut face) and the top above the cut (the rig: in its own frame,
    the cut plane's centre at the origin; rest: the affine into the set frame)."""
    x, z, rad, height = st
    ch = plane_y(x, z)
    sx, sz = -PLANE_RISE, PLANE_BACK
    slope = math.hypot(sx, sz)
    sides = (8, 6, 5)[T.i]
    stump = mk.Geo()
    top = mk.Geo()
    # the stump: from the ground to just under the cut, its top face the plane (y = ch + sx dx + sz dz)
    pts = [(x, -.05, z), (x, ch * .5, z), (x, ch - rad * slope - .002, z)]
    sk.tube(stump, pts, rad, F_BAMBOO, sides, caps=False)
    ring = []
    for i in range(sides):
        a = 2 * math.pi * i / sides
        dx, dz = math.cos(a) * rad, math.sin(a) * rad
        ring.append((x + dx, ch + sx * dx + sz * dz, z + dz))
    # the stump's walls up to the slanted ring
    for i in range(sides):
        j = (i + 1) % sides
        a0, a1 = 2 * math.pi * i / sides, 2 * math.pi * j / sides
        b0 = (x + math.cos(a0) * rad, ch - rad * slope - .002, z + math.sin(a0) * rad)
        b1 = (x + math.cos(a1) * rad, ch - rad * slope - .002, z + math.sin(a1) * rad)
        nn = sk._norm((math.cos((a0 + a1) / 2), 0, math.sin((a0 + a1) / 2)))
        stump.face((b0, b1, ring[j], ring[i]), F_BAMBOO, normal=nn)
    cn = sk._norm((-sx, 1, -sz))
    stump.face(ring, F_CUT, normal=cn)
    # the top: in its own frame (origin at the cut's centre (x, ch, z)), the slanted underside, the culm to its height
    ring_t = [(p[0] - x, p[1] - ch, p[2] - z) for p in ring]
    top.face(ring_t[::-1], F_CUT, normal=(sx, -1, sz))
    lift = rad * slope + .002
    tp = mk.Geo()
    stalk(tp, T, (0, lift, 0), height - ch - lift, rad, (-.3 - .1 * k, -.2), 500 + k, top_leaves=True, nodes=True)
    top.extend(tp)
    for i in range(sides):
        j = (i + 1) % sides
        a0, a1 = 2 * math.pi * i / sides, 2 * math.pi * j / sides
        b0 = (math.cos(a0) * rad, lift, math.sin(a0) * rad)
        b1 = (math.cos(a1) * rad, lift, math.sin(a1) * rad)
        nn = sk._norm((math.cos((a0 + a1) / 2), 0, math.sin((a0 + a1) / 2)))
        top.face((ring_t[i], ring_t[j], b1, b0), F_BAMBOO, normal=nn)
    return stump, top, mk.translate(x, ch, z)


# ---- the veranda behind the opening camera: a deck, posts, warm shoji ----
def veranda(g, T):
    z0 = VERANDA_Z
    sk.box(g, -SIDE_X, SIDE_X, -.1, .45, z0, z0 + 1.4, F_WOOD_DECK, T, bevel=.02, cell=(1.6, 4.0, 0)[T.i])
    for k in range(9):
        x = -SIDE_X + .3 + k * (2 * SIDE_X - .6) / 8
        sk.box(g, x - .1, x + .1, .45, 3.0, z0 + .1, z0 + .3, F_WOOD, T, bevel=.015, cell=0)
        if k < 8:
            x1 = -SIDE_X + .3 + (k + 1) * (2 * SIDE_X - .6) / 8
            # the shoji between: warm paper, a lattice of thin bars
            sk.box(g, x + .1, x1 - .1, .5, 2.6, z0 + 1.2, z0 + 1.22, F_SHOJI, T, bevel=0, cell=0)
            if T.detail:
                for c in range(1, 4):
                    xx = x + .1 + (x1 - x - .2) * c / 4
                    sk.box(g, xx - .012, xx + .012, .5, 2.6, z0 + 1.18, z0 + 1.2, F_WOOD, T, bevel=0, cell=0)
                for c in range(1, 6):
                    yy = .5 + 2.1 * c / 6
                    sk.box(g, x + .1, x1 - .1, yy - .012, yy + .012, z0 + 1.18, z0 + 1.2, F_WOOD, T, bevel=0, cell=0)
    sk.box(g, -SIDE_X, SIDE_X, 2.6, 3.0, z0 + 1.1, z0 + 1.3, F_WOOD_H, T, bevel=.015, cell=0)
    sk.box(g, -SIDE_X - .5, SIDE_X + .5, 3.0, 3.12, z0 - .5, z0 + 1.6, F_WOOD_H, T, bevel=.015, cell=0)
    roof(g, T, (0.0, 3.1, z0 + .6), SIDE_X + .8, 1.4, .25, .4, tiles=False)
    # the step stone up to the deck
    mk.slab(g, [(-.7, z0 - .5), (.7, z0 - .5), (.7, z0), (-.7, z0)], -.1, .2, F_GRANITE, T, bevel=.03)


def far(g, T):
    """Beyond the grove: dark roofs and a pagoda in the night haze, and tall cedars (faceted cones)."""
    r = mk.rng(97)
    # the pagoda behind the gate, far off
    px, pz = 3.0, -32.0
    for k in range(5):
        y = 2.5 + k * 3.2
        w = 4.2 - k * .55
        sk.box(g, px - w * .55, px + w * .55, y - 2.2, y, pz - w * .55, pz + w * .55, F_FAR, T, bevel=0, cell=0)
        roof(g, T, (px, y, pz), w, w, .9, 1.0, f=F_FAR, tiles=False, under=False, coarse=True)
    sk.cylinder(g, (px, 17.5, pz), (px, 22.5, pz), .12, F_FAR, 4)
    for k in range((16, 10, 6)[T.i]):
        a = -1.2 + 2.4 * k / 15
        d = float(r.uniform(22, 34))
        x, z = math.sin(a) * d, -math.cos(a) * d
        h = float(r.uniform(9, 16))
        sk.lathe(g, [(float(r.uniform(1.6, 2.4)), 0), (.9, h * .6), (0, h)], (x, -.5, z), F_FAR, (6, 5, 4)[T.i], smooth=False)
