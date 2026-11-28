"""Deepwell's modelling helpers (rig_art.py, rig_sea.py and rig_props.py build on them; stagekit.py is the shared kit, and the
round and lofted shapes are club_kit.py's and causeway_kit.py's): its finishes (riveted steel, brass, deck plate, cream hull
paint, the lamps), walls of ribbed plate, glass walls' frames, rails, roof ribs, portholes, lamps, warning stripes, floor inlays.
Everything is in the game's space (Unity: metres, +y up). docs/arenas/DEEPWELL.md "Stage art"."""
import math

import stagekit as sk
from stagekit import Geo, box, sweep, lathe, cylinder, tube
from club_kit import rod, rbox, rivets, disc, dome, ball, skin, ring, limb, ibeam  # noqa: F401
from causeway_kit import rock  # noqa: F401

Y = (0.0, 1.0, 0.0)
F = {}
# The places the Core shares (NativeDeepwellLooks): the seabed, the pools' water, the Well's dome, the north pool
SEABED, WATER, DOME_SPRING, DOME_APEX, WELL = -9.0, -1.95, 7.2, 12.6, 10.4
POOL = (-4.0, 4.0, 13.6, 18.8)
LP, L0, LG = -1.6, 0.0, 3.2


def finishes():
    A = sk
    # steel and plate: calm surfaces (the anime style's stepped light breaks a rough grain into blotches), worn bright at the edges
    F['deck'] = A.fin('dw-deck', 0x46565E, A.DETAIL_TILE, smooth=.42, metal=.25, dirt=.55, param=1.0, wear=.2)
    F['deck2'] = A.fin('dw-deck2', 0x52636B, A.DETAIL_TILE, smooth=.4, metal=.25, dirt=.45, param=1.0, wear=.2)
    F['bay'] = A.fin('dw-bay', 0x3E5058, A.DETAIL_TILE, smooth=.55, metal=.2, dirt=.6, param=.8)
    F['steel'] = A.fin('dw-steel', 0x5F7681, A.DETAIL_NONE, smooth=.46, metal=.3, dirt=.5, wear=.4)
    F['plate'] = A.fin('dw-plate', 0x546A75, A.DETAIL_NONE, smooth=.4, metal=.3, dirt=.6, wear=.3)
    F['dark'] = A.fin('dw-dark', 0x27343B, A.DETAIL_NONE, smooth=.35, metal=.3, dirt=.5, wear=.3)
    F['hull'] = A.fin('dw-hull', 0x3B4D56, A.DETAIL_NONE, smooth=.3, metal=.3, dirt=.9, wear=.2)
    F['frame'] = A.fin('dw-frame', 0x6C8590, A.DETAIL_NONE, smooth=.55, metal=.35, dirt=.35, wear=.5)
    F['brass'] = A.fin('dw-brass', 0xC99A4B, A.DETAIL_BRASS, smooth=.72, metal=.5, dirt=.3, wear=.25)
    F['copper'] = A.fin('dw-copper', 0xB06A42, A.DETAIL_BRASS, smooth=.6, metal=.5, dirt=.45)
    F['cream'] = A.fin('dw-cream', 0xD9D3BA, A.DETAIL_NONE, smooth=.45, dirt=.5, wear=.2)
    F['yellow'] = A.fin('dw-yellow', 0xE7BE34, A.DETAIL_NONE, smooth=.55, dirt=.45, wear=.25)
    F['orange'] = A.fin('dw-orange', 0xCB7428, A.DETAIL_NONE, smooth=.4, dirt=.5, wear=.3)
    F['black'] = A.fin('dw-black', 0x161B1E, A.DETAIL_NONE, smooth=.3, dirt=.3)
    F['rubber'] = A.fin('dw-rubber', 0x1B2226, A.DETAIL_NONE, smooth=.15, dirt=.4)
    F['pane'] = A.fin('dw-pane', 0x7FC9D2, A.DETAIL_NONE, smooth=.92, metal=.1, dirt=.1)
    F['lens'] = A.fin('dw-lens', 0x0E2A33, A.DETAIL_NONE, smooth=.95, metal=.2, dirt=.1)
    F['teak'] = A.fin('dw-teak', 0x8A5E3A, A.DETAIL_WOOD, smooth=.4, dirt=.35, param=2)
    F['seafoam'] = A.fin('dw-seafoam', 0x6FA89C, A.DETAIL_NONE, smooth=.4, dirt=.5, wear=.2)
    F['cushion'] = A.fin('dw-cushion', 0x245C66, A.DETAIL_CLOTH, smooth=.2, dirt=.3)
    # the lights: emission above 1 blooms
    F['lamp'] = A.fin('dw-lamp', 0xFFC98A, glow=2.6)
    F['lampdim'] = A.fin('dw-lampdim', 0xFFB870, glow=1.2)
    F['cyan'] = A.fin('dw-cyan', 0x5FF0E0, glow=2.2)
    F['cyandim'] = A.fin('dw-cyandim', 0x38B8C0, glow=.9)
    F['screen'] = A.fin('dw-screen', 0x4FD8E8, glow=1.5)
    F['amber'] = A.fin('dw-amber', 0xFFA030, glow=2.0)
    F['red'] = A.fin('dw-red', 0xFF4A3A, glow=2.2)
    F['coreglow'] = A.fin('dw-coreglow', 0xFF8A3A, glow=1.2)
    F['white'] = A.fin('dw-white', 0xFFFFFF, glow=1.0)
    # the sea
    F['sand'] = A.fin('dw-sand', 0x5A665C, A.DETAIL_NONE, smooth=.08, dirt=.7)
    F['silt'] = A.fin('dw-silt', 0x4C5852, A.DETAIL_NONE, smooth=.06, dirt=.7)
    F['rock'] = A.fin('dw-rock', 0x2F3A3E, A.DETAIL_NONE, smooth=.1, dirt=.6)
    F['rockpale'] = A.fin('dw-rockpale', 0x566058, A.DETAIL_NONE, smooth=.1, dirt=.6)
    F['coral'] = A.fin('dw-coral', 0xD0627C, A.DETAIL_NONE, smooth=.2, dirt=.3)
    F['coral2'] = A.fin('dw-coral2', 0xE0934A, A.DETAIL_NONE, smooth=.2, dirt=.3)
    F['coralglow'] = A.fin('dw-coralglow', 0x8A6CFF, glow=.9)
    F['kelp'] = A.fin('dw-kelp', 0x2F7C4A, A.DETAIL_NONE, smooth=.3, dirt=.2)
    F['kelpstem'] = A.fin('dw-kelpstem', 0x5A6A2E, A.DETAIL_NONE, smooth=.25, dirt=.2)
    F['whale'] = A.fin('dw-whale', 0x34495C, A.DETAIL_NONE, smooth=.45, dirt=.2)
    F['belly'] = A.fin('dw-belly', 0xB9C6CB, A.DETAIL_NONE, smooth=.4, dirt=.2)
    F['fish'] = A.fin('dw-fish', 0xA9C2CE, A.DETAIL_NONE, smooth=.8, metal=.3, dirt=.1)
    F['fishback'] = A.fin('dw-fishback', 0x3E6478, A.DETAIL_NONE, smooth=.7, metal=.2, dirt=.1)
    F['vent'] = A.fin('dw-vent', 0xFF7A2A, glow=2.4)
    return F


def ext(b):
    p, s = b['pos'], b['size']
    return (p[0] - s[0] / 2, p[0] + s[0] / 2, p[1] - s[1] / 2, p[1] + s[1] / 2, p[2] - s[2] / 2, p[2] + s[2] / 2)


def long_x(b):
    return b['size'][0] >= b['size'][2]


def hashf(*k):
    h = 2166136261
    for v in k:
        h = ((h ^ (int(v) & 0xFFFFFFFF)) * 16777619) & 0xFFFFFFFF
    h ^= h >> 15
    h = (h * 2246822519) & 0xFFFFFFFF
    h ^= h >> 13
    return (h & 0xFFFFFF) / 16777216.0


class Run:
    """A straight run in plan: u along it from its start, d across it (to the left of u), y up. For walls, rails and frames."""

    def __init__(self, x0, z0, x1, z1):
        self.o = (x0, z0)
        L = math.hypot(x1 - x0, z1 - z0)
        self.len = L
        self.u = ((x1 - x0) / L, (z1 - z0) / L)
        self.n = (-self.u[1], self.u[0])

    def at(self, u, y, d=0.0):
        return (self.o[0] + self.u[0] * u + self.n[0] * d, y, self.o[1] + self.u[1] * u + self.n[1] * d)

    def box(self, g, u0, u1, y0, y1, d0, d1, f, T=None, bevel=.012, cell=0, skip=()):
        a, b = self.at(u0, y0, d0), self.at(u1, y1, d1)
        box(g, a[0], b[0], a[1], b[1], a[2], b[2], f, T, bevel, cell=cell, skip=skip)

    def N(self, s=1.0):
        return (self.n[0] * s, 0.0, self.n[1] * s)

    def U(self, s=1.0):
        return (self.u[0] * s, 0.0, self.u[1] * s)


def run_of(b):
    """The block's long axis as a Run through its middle plane, with its half thickness."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    if long_x(b):
        return Run(x0, (z0 + z1) / 2, x1, (z0 + z1) / 2), (z1 - z0) / 2
    return Run((x0 + x1) / 2, z0, (x0 + x1) / 2, z1), (x1 - x0) / 2


def stripes(g, x0, x1, z0, z1, y, T, band=.3):
    """A flat band of warning stripes on a floor (yellow and black lengths along its long side)."""
    if x1 - x0 < 1e-3 or z1 - z0 < 1e-3:
        return
    alongx = (x1 - x0) >= (z1 - z0)
    L = (x1 - x0) if alongx else (z1 - z0)
    n = max(1, int(round(L / band))) if T.i < 2 else max(1, int(round(L / (band * 3))))
    for k in range(n):
        a, b = k / n, (k + 1) / n
        f = F['yellow'] if k % 2 == 0 else F['black']
        if alongx:
            g.face(((x0 + L * a, y, z0), (x0 + L * b, y, z0), (x0 + L * b, y, z1), (x0 + L * a, y, z1)), f, Y)
        else:
            g.face(((x0, y, z0 + L * a), (x1, y, z0 + L * a), (x1, y, z0 + L * b), (x0, y, z0 + L * b)), f, Y)


def flat_ring(g, cx, cz, r0, r1, y, f, n, down=False):
    for k in range(n):
        a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        g.face(((cx + math.sin(a0) * r0, y, cz + math.cos(a0) * r0), (cx + math.sin(a1) * r0, y, cz + math.cos(a1) * r0),
                (cx + math.sin(a1) * r1, y, cz + math.cos(a1) * r1), (cx + math.sin(a0) * r1, y, cz + math.cos(a0) * r1)), f, (0, -1, 0) if down else Y)


def arc_path(p0, p1, rise, n, power=1.0):
    """Points of an arch from p0 to p1 rising `rise` at its middle (a half sine; power < 1 squarer shoulders)."""
    out = []
    for k in range(n + 1):
        t = k / n
        s = math.sin(math.pi * t) ** power
        out.append((p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t + rise * s, p0[2] + (p1[2] - p0[2]) * t))
    return out


def rib(g, p0, p1, rise, r, f, T, n=None, power=1.0):
    n = n or (12, 8, 4)[T.i]
    tube(g, arc_path(p0, p1, rise, n, power), r, f, (6, 5, 3)[T.i], caps=False)


def porthole(g, c, n, r, T):
    """A blind porthole on a wall face at c (normal n): a brass ring round a dark lens with a faint light in it."""
    u, v, w = sk.frame((0, 0, 0), n)
    sides = (16, 10, 6)[T.i]
    lathe(g, [(r * 1.22, 0.0), (r * 1.22, .05), (r * 1.08, .07), (r * .96, .05), (r * .96, .012)], c, F['brass'], sides, axis=w)
    disc(g, sk._add(c, w, .014), r * .97, F['lens'], sides, w)
    if T.tiny:
        rivets(g, [sk._add(sk._add(sk._add(c, u, r * 1.1 * math.cos(a)), v, r * 1.1 * math.sin(a)), w, .05) for a in [k * math.pi / 4 for k in range(8)]], w, .018, F['brass'])


def sconce(g, c, n, T, f=None):
    """A wall lamp at c on a face of normal n: a brass back plate and cage round a glowing lens."""
    u, v, w = sk.frame((0, 0, 0), n)
    q = lambda a, b, d: sk._add(sk._add(sk._add(c, u, a), v, b), w, d)  # noqa: E731
    lo, hi = q(-.3, -.07, 0), q(.3, .07, .035)
    box(g, min(lo[0], hi[0]), max(lo[0], hi[0]), min(lo[1], hi[1]), max(lo[1], hi[1]), min(lo[2], hi[2]), max(lo[2], hi[2]), F['brass'], T, .008, cell=0)
    lo, hi = q(-.25, -.045, .035), q(.25, .045, .085)
    box(g, min(lo[0], hi[0]), max(lo[0], hi[0]), min(lo[1], hi[1]), max(lo[1], hi[1]), min(lo[2], hi[2]), max(lo[2], hi[2]), f if f is not None else F['lamp'], None, 0, cell=0)
    if T.tiny:
        for a in (-.25, -.085, .085, .25):
            rod(g, q(a, -.06, .03), q(a, .06, .03 + .07), .008, F['brass'], 4, caps=False)


def pendant(g, top, drop, T, r=.26):
    """A pendant lamp hung from `top`: a rod, a brass shade, a glowing bowl."""
    x, y, z = top
    sides = (12, 8, 6)[T.i]
    rod(g, (x, y, z), (x, y - drop + .22, z), .018, F['brass'], 4, caps=False)
    lathe(g, [(r, 0.0), (r * .92, .05), (r * .5, .16), (.05, .22)], (x, y - drop, z), F['brass'], sides)
    lathe(g, [(.02, -.12), (r * .55, -.09), (r * .8, -.02), (r * .86, 0.0)], (x, y - drop, z), F['lamp'], sides)


def flush_lamp(g, x, y, z, hx, hz, T):
    """A ceiling lamp set under a deck: a brass tray and a glowing panel facing down."""
    box(g, x - hx, x + hx, y - .05, y, z - hz, z + hz, F['brass'], T, .008, cell=0, skip=('+y',))
    g.face(((x - hx + .04, y - .052, z - hz + .04), (x + hx - .04, y - .052, z - hz + .04), (x + hx - .04, y - .052, z + hz - .04), (x - hx + .04, y - .052, z + hz - .04)), F['lamp'], (0, -1, 0))


def plate_wall(g, b, T, seed, lamps=True, ports=True, paint=None):
    """A steel wall block as ribbed plate: the body, a skirting, a rib every 2 m on both faces with rivets, a brass band at
    hand height, portholes between the ribs and wall lamps above them."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    R, h = run_of(b)
    box(g, x0, x1, y0, y1, z0, z1, F['plate'], T, .015, cell=None if T.i == 0 else 0)
    L, H = R.len, y1 - y0
    for side in (-1, 1):
        d0 = side * h
        R.box(g, 0.02, L - .02, y0 + .004, y0 + .24, d0, d0 + side * .035, F['dark'], T, .01, skip=('-y',))
        ribs = []
        u = .5
        while u < L - .2:
            ribs.append(u)
            u += 2.0
        for u in ribs:
            R.box(g, u - .075, u + .075, y0 + .24, y1 - .02, d0, d0 + side * .06, F['steel'], T, .012)
            if T.tiny:
                pts = [R.at(u + du, y0 + .5 + k * .62, d0 + side * .06) for k in range(int((H - .7) / .62) + 1) for du in (-.045, .045)]
                rivets(g, pts, R.N(side), .013, F['frame'])
        if H > 2.2:
            R.box(g, 0.02, L - .02, y0 + 1.04, y0 + 1.14, d0, d0 + side * .022, F['brass'], T, .008)
            if paint is not None:
                # painted panels between the ribs, from the band up to the head rail (and again on the storey above)
                edges = [0.02] + [u for u in ribs] + [L - .02]
                for a, c in zip(edges, edges[1:]):
                    if c - a > .5:
                        for (ya, yb) in ((y0 + 1.2, min(y1, y0 + 2.7) - .06),) + (((3.2 + .16, y1 - .26),) if H > 6.0 else ()):
                            R.box(g, a + .13, c - .13, ya, yb, d0, d0 + side * .018, paint, T, .008)
        if H > 3.0 and T.tiny:
            R.box(g, 0.02, L - .02, y1 - .2, y1 - .02, d0, d0 + side * .04, F['steel'], T, .01)
        if H > 3.0:
            k = 0
            u = 1.63
            while u < L - .6:
                if lamps:
                    sconce(g, R.at(u, y0 + 2.48, d0), R.N(side), T)
                if ports and T.i < 2 and u + 1.0 < L - .6 and (k + seed) % 2 == 0 and H > 3.0:
                    porthole(g, R.at(u + .87, y0 + 1.72, d0 + side * (.024 if paint is not None else 0)), R.N(side), .27, T)
                k += 1
                u += 4.0
            if H > 6.0:
                u = 1.63
                while u < L - .6:
                    if lamps:
                        sconce(g, R.at(u, 3.2 + 2.48, d0), R.N(side), T, F['lampdim'])
                    u += 4.0
                R.box(g, 0.02, L - .02, 3.2 - .45, 3.2 + .06, d0, d0 + side * .05, F['steel'], T, .012)


def glass_wall(g, b, T, decks=(0.0, 3.2), every=2.4, mid=True):
    """A glass block's frame: a sill beam and a head beam, a T-section mullion every 2.4 m or so with brass shoes, a brass
    transom at each deck line, slim glazing bars between; the pane itself is not modelled (the effects glaze it)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    R, h = run_of(b)
    L, H = R.len, y1 - y0
    n = max(1, int(round(L / every)))
    R.box(g, 0, L, y0, y0 + .24, -h - .03, h + .03, F['steel'], T, .02)
    R.box(g, 0, L, y1 - .22, y1, -h - .03, h + .03, F['steel'], T, .02)
    if T.detail:
        R.box(g, 0, L, y0 + .24, y0 + .3, -h - .05, h + .05, F['brass'], T, .012)
    for k in range(n + 1):
        u = L * k / n
        a, c = max(0.0, u - .07), min(L, u + .07)
        R.box(g, a, c, y0 + .24, y1 - .22, -h * .55, h * .55, F['frame'], T, .015)
        if T.i == 0:
            R.box(g, max(0.0, u - .11), min(L, u + .11), y0 + .24, y1 - .22, -h, -h + .05, F['frame'], T, .01)
            R.box(g, max(0.0, u - .11), min(L, u + .11), y0 + .24, y1 - .22, h - .05, h, F['frame'], T, .01)
        if T.i == 0:
            R.box(g, max(0.0, u - .13), min(L, u + .13), y0 + .3, y0 + .5, -h - .02, h + .02, F['brass'], T, .012)
    for y in decks:
        if y0 + .5 < y < y1 - .5:
            R.box(g, 0, L, y - .22, y + .04, -h - .04, h + .04, F['brass'], T, .015)
    if mid and T.i < 2:
        # slim glazing bars: one across each storey's pane at its middle
        tops = [y0 + .24] + [y for y in decks if y0 + .5 < y < y1 - .5] + [y1 - .22]
        for a, c in zip(tops, tops[1:]):
            if c - a > 2.4:
                R.box(g, 0, L, (a + c) / 2 - .025, (a + c) / 2 + .025, -.04, .04, F['frame'], T, 0)


def rail(g, b, T):
    """A rail: brass posts and a round hand rail over a pane of thick glass in a shoe."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    R, h = run_of(b)
    L = R.len
    R.box(g, 0, L, y0, y0 + .08, -h, h, F['steel'], T, .01)
    R.box(g, .03, L - .03, y0 + .08, y1 - .1, -.02, .02, F['pane'], T, 0)
    n = max(1, int(round(L / 1.3)))
    sides = (8, 6, 4)[T.i]
    for k in range(n + 1):
        u = min(max(L * k / n, .04), L - .04)
        rod(g, R.at(u, y0 + .08, 0), R.at(u, y1 - .04, 0), .028, F['brass'], sides, caps=False)
    rod(g, R.at(0, y1 - .04, 0), R.at(L, y1 - .04, 0), .042, F['brass'], sides)


def door_frame(g, R, u0, u1, y0, y1, h, T, stripe=False):
    """A pressure door's frame round an opening u0..u1, y0..y1 in a wall of half thickness h on run R: rounded brass-bound
    jambs and a head, proud of both faces, a light bar under the head."""
    t, p = .16, h + .06
    for (a, c) in ((u0 - t * .2, u0 + t * .8), (u1 - t * .8, u1 + t * .2)):
        R.box(g, a, c, y0 + .003, y1, -p, p, F['steel'], T, .03, skip=('-y',))
        if T.detail:
            R.box(g, a - .02, c + .02, y0 + .006, y0 + .12, -p - .02, p + .02, F['brass'], T, .012, skip=('-y',))
    R.box(g, u0 - t * .2, u1 + t * .2, y1 - t * .8, y1 + t * .2, -p, p, F['steel'], T, .03)
    R.box(g, u0 + t, u1 - t, y1 - t * .8 - .035, y1 - t * .8, -h * .6, h * .6, F['cyan'], None, 0)
    if T.tiny:
        for side in (-1, 1):
            pts = [R.at(u, y0 + .3 + k * .5, side * p) for u in (u0 + t * .3, u1 - t * .3) for k in range(int((y1 - y0 - .4) / .5) + 1)]
            rivets(g, pts, R.N(side), .016, F['brass'])
    if stripe and T.i < 2:
        for side in (-1, 1):
            for (a, c) in ((u0 + t * .8, u0 + t * .8 + .01), (u1 - t * .8 - .01, u1 - t * .8)):
                for k in range(int((y1 - y0 - t) / .3)):
                    R.box(g, a, c, y0 + k * .3, y0 + k * .3 + .3, side * (p - .14), side * p, F['yellow'] if k % 2 == 0 else F['black'], None, 0)
