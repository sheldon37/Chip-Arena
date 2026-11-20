"""One Breath's set (the rifle's intro, docs/INTROS-RANGED.md): a rain-soaked rooftop at night, sixty metres over a city avenue. The
pilot crouches at the origin behind the front parapet, facing +z across the avenue to the skyline, where a surveillance drone hangs
between the towers with its red eye. A water tank on steel legs, the stair bulkhead with its caged bulb, humming condensers, an
antenna mast with its red beacon, puddles; neighbouring roofs left and right; the towers all round, their windows lit in patches.

Props (Unity space, the intro turns them by Facing):
  onebreath/roof      our roof, its parapets, its plant and the building's front falling to the street (static)
  onebreath/street    the avenue far below and the neighbours' near walls (static)
  onebreath/block-a..e  a city block of towers (several placings each; its windows face -z, toward the block's own origin side)
  onebreath/billboard  the rooftop billboard on the tower behind the drone
  onebreath/sky       the night dome, the city's glow on the low cloud (glow: unlit, painted)
  onebreath/beacon    the mast's red lamp (glow; the intro blinks it); origin at its centre
  onebreath/drone     the drone's body and arms, eye forward (+z); origin at its centre
  onebreath/eye       the drone's lens (glow; the intro lights it); origin at the drone's centre
  onebreath/rotor     one rotor's blades; origin at its hub, turning about +y
  onebreath/casing    a spent rifle casing along +z (origin at its middle)
  onebreath/shard     a shard of the lens; onebreath/panel a torn panel of the drone's shell
"""
import math

import numpy as np

import ranged_kit as rk
from ranged_kit import F, sk, Geo, box, cylinder, lathe, sweep, beam, quat_axis, affine

GROUND = .012            # the roof's top (the pilot's floor)
PARAPET = (1.15, 1.55)   # the front parapet's inner and outer faces (z)
COPING = .95             # the parapet's top (the coping's underside); the coping rises to 1.03
STREET_Y = -62.0         # the avenue far below
DRONE = (6.0, 13.0, 134.0)   # the drone's centre (it hovers here, the eye toward the roof)
MAST = (-7.4, GROUND, -.6)    # the antenna mast's foot; its beacon sits at MAST + (0, 8.3, 0)
BEACON = (MAST[0], MAST[1] + 8.3, MAST[2])
# The near city blocks (Unity space, inside the game's 200 m far plane): (block, x, z, yaw radians, scale). Each block's windows
# face its -z side, so every placing is turned toward the roof. Past them, onebreath/farcity rings the eye at about 165 m.
BLOCKS = [('a', -40.0, 120.0, 0.0, 1.0), ('b', 48.0, 145.0, .15, .9), ('c', -105.0, 70.0, -1.1, .85), ('d', 110.0, 60.0, 1.1, .85),
          ('e', -60.0, -110.0, math.pi - .5, .85)]
HERO_TOWER = (-6.0, 14.0, 152.0, 168.0, 26.0)   # the tower behind the drone: x0, x1, z0, z1, its roof
BILLBOARD = (4.0, 26.0, 153.0)                   # the billboard's foot on its roof (faces -z)
FAR_R = 165.0                                    # the far city's ring (drawn round the eye's ground point)


def finishes():
    A = sk
    rk.fin('membrane', 0x2A2C33, A.DETAIL_PLASTER, smooth=.55, dirt=.4)
    rk.fin('membranewet', 0x1E2028, A.DETAIL_NONE, smooth=.92, dirt=.1)
    rk.fin('gravel', 0x3A3B40, A.DETAIL_FLAG, smooth=.3, param=.3, dirt=.5)
    rk.fin('pavers', 0x45464D, A.DETAIL_FLAG, smooth=.5, param=.6, dirt=.6)
    rk.fin('brickroof', 0x5A3A34, A.DETAIL_BRICK, smooth=.25, dirt=.8, wear=.2)
    rk.fin('coping', 0x6A6A70, A.DETAIL_CARVED, smooth=.45, dirt=.6, wear=.4)
    rk.fin('concrete', 0x4A4C55, A.DETAIL_PLASTER, smooth=.35, dirt=.8, wear=.2)
    rk.fin('concretedark', 0x2E3038, A.DETAIL_PLASTER, smooth=.35, dirt=.7)
    rk.fin('facade', 0x3A3640, A.DETAIL_BRICK, smooth=.3, dirt=.7)
    rk.fin('steel', 0x4A5060, A.DETAIL_IRON, smooth=.5, metal=.5, dirt=.5, wear=.3)
    rk.fin('steeldark', 0x24272F, A.DETAIL_IRON, smooth=.5, metal=.5, dirt=.4, wear=.3)
    rk.fin('galv', 0x7A808A, A.DETAIL_IRON, smooth=.55, metal=.6, dirt=.4, wear=.2)
    rk.fin('tank', 0x4E3A2C, A.DETAIL_WOOD, smooth=.3, param=1, dirt=.8, wear=.2)
    rk.fin('tar', 0x16171B, A.DETAIL_NONE, smooth=.7, dirt=.2)
    rk.fin('glassdark', 0x0C1018, A.DETAIL_NONE, smooth=.95, metal=.4, dirt=.1)
    rk.fin('tower0', 0x1A1D26, A.DETAIL_TILE, smooth=.8, metal=.3, param=1.5, dirt=.2)
    rk.fin('tower1', 0x22252E, A.DETAIL_PLASTER, smooth=.5, dirt=.3)
    rk.fin('tower2', 0x2A2730, A.DETAIL_BRICK, smooth=.3, param=2.5, dirt=.4)
    rk.fin('tower3', 0x161A22, A.DETAIL_TILE, smooth=.85, metal=.35, param=3.0, dirt=.2)
    rk.fin('asphalt', 0x1A1B20, A.DETAIL_PLASTER, smooth=.8, dirt=.3)
    rk.fin('dronebody', 0xC8CCD4, A.DETAIL_NONE, smooth=.6, metal=.3, dirt=.2, wear=.2)
    rk.fin('dronedark', 0x22262E, A.DETAIL_IRON, smooth=.55, metal=.5, dirt=.2, wear=.3)
    rk.fin('brassr', 0xC8963E, A.DETAIL_BRASS, smooth=.75, metal=.8, dirt=.1)
    rk.fin('sky1', 0xFFFFFF, A.DETAIL_GLOW, glow=1.0)
    rk.fin('winwarm', 0xFFD49A, A.DETAIL_GLOW, glow=1.6)
    rk.fin('wincool', 0xCFE4FF, A.DETAIL_GLOW, glow=1.5)
    rk.fin('winteal', 0x7FE2FF, A.DETAIL_GLOW, glow=1.8)
    rk.fin('winmag', 0xFF6AD5, A.DETAIL_GLOW, glow=2.0)
    rk.fin('redlamp', 0xFF2A22, A.DETAIL_GLOW, glow=3.0)
    rk.fin('bulb', 0xFFE2A8, A.DETAIL_GLOW, glow=3.5)
    rk.fin('streetlamp', 0xFFB45A, A.DETAIL_GLOW, glow=3.0)
    rk.fin('neoncyan', 0x5AF2FF, A.DETAIL_GLOW, glow=4.0)
    rk.fin('neonmag', 0xFF4FC8, A.DETAIL_GLOW, glow=4.0)
    rk.fin('eyered', 0xFF2018, A.DETAIL_GLOW, glow=4.0)


WIN = ('winwarm', 'wincool', 'winwarm', 'winwarm', 'wincool', 'winteal', 'winwarm', 'wincool', 'winwarm', 'winmag')


def windows(g, rng, x0, x1, y0, y1, z, p, floor=3.6, bay=2.6, lift=.06, facing=-1, side_x=None):
    """Lit windows on a face at z (its normal (0, 0, facing)) from x0..x1, y0..y1: per floor, runs of lit bays as glow quads,
    about `p` lit; now and then a whole office floor lit end to end."""
    nb = max(1, int((x1 - x0) / bay))
    nf = int((y1 - y0) / floor)
    zz = z + facing * lift
    if side_x is not None:
        # on a +x side face: x0..x1 run along z there
        def quad(a, b, ya, yb, fin):
            g.face(((side_x + lift, ya, a), (side_x + lift, ya, b), (side_x + lift, yb, b), (side_x + lift, yb, a)), fin, (1, 0, 0))
    else:
        def quad(a, b, ya, yb, fin):
            g.face(((a, ya, zz), (b, ya, zz), (b, yb, zz), (a, yb, zz)), fin, (0, 0, facing))
    for f in range(nf):
        ya = y0 + f * floor + floor * .32
        yb = ya + floor * .36
        if rng.random() < .04:
            fin = F[WIN[int(rng.integers(0, 3))]]
            quad(x0 + .3, x1 - .3, ya, yb, fin)
            continue
        run = None
        for b in range(nb + 1):
            lit = b < nb and rng.random() < p
            if lit and run is None:
                run = b
            if (not lit) and run is not None:
                xa = x0 + run * (x1 - x0) / nb + .3
                xb = x0 + b * (x1 - x0) / nb - .3
                fin = F[WIN[int(rng.integers(0, len(WIN)))]]
                quad(xa, xb, ya, yb, fin)
                run = None


def tower(g, rng, x, z, w, d, h, kind, T, lit=.3, base=STREET_Y, coarse=1.0):
    """A tower on its plot (x, z the centre; w across, d deep) rising h from the street: setbacks, a crown, its lit face toward -z."""
    fin = F['tower%d' % kind]
    y = base
    bay_f, bay_w = rng.uniform(3.0, 3.8) * coarse, rng.uniform(1.5, 2.3) * coarse
    tiers = 1 + int(rng.integers(0, 3))
    ww, dd = w, d
    for k in range(tiers):
        hh = h * ((.6, .25, .15)[k] if tiers == 3 else (.75, .25)[k] if tiers == 2 else 1.0)
        box(g, x - ww / 2, x + ww / 2, y, y + hh, z - dd / 2, z + dd / 2, fin, T, 0, cell=0, skip=('-y',))
        windows(g, rng, x - ww / 2, x + ww / 2, y + 3, y + hh - 1.5, z - dd / 2, lit * (1.0 if k == 0 else .8), floor=bay_f, bay=bay_w)
        if T.fine and rng.random() < .35:
            # some lit windows round the side as well
            windows(g, rng, z - dd / 2, z + dd / 2, y + hh * .2, y + hh * .7, 0.0, lit * .5, floor=bay_f, bay=bay_w, side_x=x + ww / 2)
        y += hh
        ww *= rng.uniform(.62, .82)
        dd *= rng.uniform(.62, .82)
    # the crown: a lit cornice band, a spire or a mast with a red lamp
    c = rng.random()
    if c < .35:
        box(g, x - ww / 2 - .3, x + ww / 2 + .3, y - .6, y, z - dd / 2 - .3, z + dd / 2 + .3, F['winteal' if rng.random() < .5 else 'wincool'], T, 0, cell=0, skip=('-y', '+y'))
    elif c < .7:
        lathe(g, [(min(ww, dd) * .3, y), (.25, y + h * .18), (.05, y + h * .22)], (x, 0, z), fin, 4)
        box(g, x - .35, x + .35, y + h * .22, y + h * .22 + .7, z - .35, z + .35, F['redlamp'], T, 0, cell=0)
    else:
        cylinder(g, (x, y, z), (x, y + 9, z), .25, F['steel'], 4, caps=False)
        box(g, x - .3, x + .3, y + 9, y + 9.6, z - .3, z + .3, F['redlamp'], T, 0, cell=0)
    return y


def block(name, T, seed):
    """A block of towers about 70 x 70 m round its origin, its tallest at the back; windows on the faces toward -z."""
    rng = np.random.default_rng(seed)
    g = Geo()
    plots = []
    for i in range(3):
        for j in range(3):
            if rng.random() < .15:
                continue
            x = (i - 1) * 24 + rng.uniform(-3, 3)
            z = (j - 1) * 24 + rng.uniform(-3, 3)
            w = rng.uniform(13, 21)
            d = rng.uniform(13, 21)
            h = rng.uniform(60, 120) + j * 35 + (40 if rng.random() < .2 else 0)
            plots.append((x, z, w, d, h))
    for (x, z, w, d, h) in plots:
        tower(g, rng, x, z, w, d, h, int(rng.integers(0, 4)), T, lit=rng.uniform(.18, .4))
    return g


def farcity(T):
    """The city past the near blocks: a ring of towers about FAR_R out, their lit faces toward the middle (drawn round the eye)."""
    rng = np.random.default_rng(909)
    g = Geo()
    n = 52
    for k in range(n):
        a = 2 * math.pi * (k + rng.uniform(-.3, .3)) / n
        r = FAR_R + rng.uniform(-8, 10)
        w = rng.uniform(10, 18)
        h = rng.uniform(50, 150) + (60 if rng.random() < .12 else 0)
        t = Geo()
        tower(t, rng, 0.0, 0.0, w, rng.uniform(10, 16), h, int(rng.integers(0, 4)), T, lit=rng.uniform(.12, .3), coarse=1.9)
        # its lit face (-z) toward the middle: turn it to face the origin
        g.extend(t.transformed(rk.yaw(a + math.pi, (math.sin(a) * r, 0.0, math.cos(a) * r))))
    return g


def drone(T):
    """The surveillance drone: a pod body (eye forward, +z), four arms to ducted rotors, a searchlight under it."""
    g = Geo()
    n = 12 if T.fine else 7
    # the pod: a squat ellipsoid
    prof = [(.02, -.32), (.3, -.28), (.46, -.16), (.5, 0), (.46, .14), (.32, .26), (.02, .3)]
    lathe(g, prof, (0, 0, 0), F['dronebody'], n, scale=(1.0, 1.25))
    # a dark band round it and the eye's housing
    lathe(g, [(.505, -.03), (.505, .05)], (0, 0, 0), F['dronedark'], n, scale=(1.0, 1.25))
    lathe(g, [(.24, 0.0), (.26, .06), (.22, .12)], (0, 0, .58), F['dronedark'], n, axis=(0, 0, 1))
    for a in (45, 135, 225, 315):
        r = math.radians(a)
        d = (math.sin(r), 0, math.cos(r))
        p0 = (d[0] * .38, .02, d[2] * .38 * 1.2)
        p1 = (d[0] * 1.05, .08, d[2] * 1.05)
        beam(g, p0, p1, .1, .07, F['dronedark'], T)
        # the duct: a short ring round the rotor
        c = (d[0] * 1.15, .1, d[2] * 1.15)
        lathe(g, [(.42, -.05), (.44, .02), (.42, .09)], c, F['dronebody'], n * 2 if T.fine else n)
        lathe(g, [(.4, .09), (.4, -.05)], c, F['dronedark'], n * 2 if T.fine else n)
        cylinder(g, (c[0], -.04, c[2]), (c[0], .1, c[2]), .05, F['dronedark'], 5)
    # antennae and the searchlight
    cylinder(g, (.12, .25, -.2), (.16, .62, -.3), .012, F['dronedark'], 4)
    cylinder(g, (-.12, .25, -.2), (-.16, .55, -.3), .012, F['dronedark'], 4)
    lathe(g, [(.1, -.3), (.13, -.4), (.11, -.44)], (0, 0, .1), F['dronedark'], 8)
    return g


def eye(T):
    g = Geo()
    lathe(g, [(.2, .0), (.17, .07), (.1, .1), (.01, .11)], (0, 0, .62), F['eyered'], 14 if T.fine else 8, axis=(0, 0, 1))
    return g


def rotor(T):
    g = Geo()
    for k in range(3):
        a = 2 * math.pi * k / 3
        c, s = math.cos(a), math.sin(a)
        p = [(c * .05 - s * .04, 0, s * .05 + c * .04), (c * .38 - s * .05, .012, s * .38 + c * .05), (c * .38 + s * .05, -.012, s * .38 - c * .05), (c * .05 + s * .04, 0, s * .05 - c * .04)]
        g.face(p, F['dronedark'], (0, 1, 0))
        g.face(p[::-1], F['dronedark'], (0, -1, 0))
    return g


def casing(T):
    g = Geo()
    n = 8 if T.fine else 5
    lathe(g, [(.0062, -.035), (.0064, .01), (.0052, .02), (.0042, .028), (.0042, .035)], (0, 0, 0), F['brassr'], n, axis=(0, 0, 1), cap0=True, cap1=True)
    return g


def shard(T):
    g = Geo()
    g.face(((0, 0, 0), (.09, .02, 0), (.03, .1, 0)), F['glassdark'], (0, 0, 1))
    g.face(((0, 0, 0), (.03, .1, 0), (.09, .02, 0)), F['eyered'], (0, 0, -1))
    return g


def panel(T):
    g = Geo()
    rk.prism(g, [(0, 0), (.22, -.03), (.3, .12), (.14, .2), (.02, .14)], (0, 0, 0), (1, 0, 0), (0, 1, 0), .02, F['dronebody'])
    return g


def beacon(T):
    g = Geo()
    lathe(g, [(.09, 0), (.1, .08), (.07, .18), (.01, .2)], (0, -.1, 0), F['redlamp'], 10 if T.fine else 6, cap0=True)
    return g


def water_tank(g, at, T):
    x, z = at[0], at[2]
    legs = 4.2
    for sx in (-1, 1):
        for sz in (-1, 1):
            box(g, x + sx * 1.3 - .1, x + sx * 1.3 + .1, GROUND, legs, z + sz * 1.3 - .1, z + sz * 1.3 + .1, F['steel'], T, 0, cell=0, skip=('-y',))
    for y in (1.4, 2.8):
        for (a, b) in (((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))):
            beam(g, (x + a[0] * 1.3, y, z + a[1] * 1.3), (x + b[0] * 1.3, y, z + b[1] * 1.3), .08, .08, F['steel'], T, bevel=0)
    if T.detail:
        for (a, b) in (((-1, -1), (1, -1)), ((1, 1), (-1, 1)), ((1, -1), (1, 1)), ((-1, 1), (-1, -1))):
            beam(g, (x + a[0] * 1.3, .2, z + a[1] * 1.3), (x + b[0] * 1.3, 1.4, z + b[1] * 1.3), .05, .05, F['steel'], T, bevel=0)
    box(g, x - 1.7, x + 1.7, legs, legs + .18, z - 1.7, z + 1.7, F['steeldark'], T, .02, cell=0)
    n = 20 if T.fine else 8
    lathe(g, [(1.55, legs + .18), (1.6, legs + 2.0), (1.55, legs + 3.6)], (x, 0, z), F['tank'], n)
    for hh in (.5, 1.3, 2.1, 2.9):
        lathe(g, [(1.62, legs + .18 + hh - .04), (1.62, legs + .18 + hh + .04)], (x, 0, z), F['steeldark'], n)
    lathe(g, [(1.72, legs + 3.6), (.2, legs + 4.6), (.02, legs + 4.7)], (x, 0, z), F['tar'], n, cap0=True)
    # its ladder
    if T.detail:
        for sx in (-.22, .22):
            box(g, x + sx - .02, x + sx + .02, GROUND, legs + 3.6, z + 1.62, z + 1.66, F['steel'], T, 0, cell=0)
        for k in range(int((legs + 3.4) / .35)):
            y = .3 + k * .35
            box(g, x - .22, x + .22, y - .015, y + .015, z + 1.62, z + 1.66, F['steel'], T, 0, cell=0)


def condenser(g, at, w, d, h, T):
    x, z = at[0], at[2]
    box(g, x - w / 2, x + w / 2, GROUND, h, z - d / 2, z + d / 2, F['galv'], T, .03, cell=0)
    # the fans on top: dark discs with a hub
    n = 2 if w > 1.8 else 1
    for k in range(n):
        cx = x - w / 2 + (k + .5) * w / n
        rk.disc(g, (cx, h + .005, z), min(w / n, d) * .36, F['steeldark'], 12 if T.fine else 6)
        if T.detail:
            cylinder(g, (cx, h, z), (cx, h + .06, z), .08, F['galv'], 6)
    if T.detail:
        # louvres down the side
        for k in range(5):
            y = .2 + k * (h - .4) / 5
            box(g, x - w / 2 + .1, x + w / 2 - .1, y, y + .05, z - d / 2 - .03, z - d / 2, F['steeldark'], T, 0, cell=0)


def bulkhead(g, at, T):
    """The stair bulkhead: a small brick room with a steel door facing +z and a caged bulb over it."""
    x, z = at[0], at[2]
    box(g, x - 1.6, x + 1.6, GROUND, 2.9, z - 1.8, z + 1.8, F['brickroof'], T, .03, cell=1.5 if T.fine else 0)
    box(g, x - 1.75, x + 1.75, 2.9, 3.05, z - 1.95, z + 1.95, F['coping'], T, .02, cell=0)
    g.face(((x - .5, GROUND, z + 1.81), (x + .5, GROUND, z + 1.81), (x + .5, 2.1, z + 1.81), (x - .5, 2.1, z + 1.81)), F['steeldark'], (0, 0, 1))
    box(g, x - .6, x + .6, 2.1, 2.2, z + 1.8, z + 1.86, F['steel'], T, 0, cell=0)
    box(g, x - .09, x + .09, 2.3, 2.5, z + 1.8, z + 1.96, F['bulb'], T, 0, cell=0)
    if T.detail:
        for k in range(4):
            box(g, x - .11 + k * .07, x - .1 + k * .07, 2.28, 2.52, z + 1.95, z + 1.98, F['steeldark'], T, 0, cell=0)
        # a vent pipe and a stack on the bulkhead's roof
        cylinder(g, (x + .9, 3.05, z - .9), (x + .9, 3.9, z - .9), .12, F['galv'], 8)
        lathe(g, [(.22, 3.9), (.22, 4.0), (.02, 4.05)], (x + .9, 0, z - .9), F['galv'], 8)


def mast(g, T):
    x, y0, z = MAST
    top = BEACON[1] - .2
    for sx in (-1, 1):
        for sz in (-1, 1):
            beam(g, (x + sx * .45, y0, z + sz * .45), (x + sx * .12, top, z + sz * .12), .05, .05, F['steel'], T, bevel=0)
    if T.detail:
        k = 0
        y = y0 + .2
        while y < top - .6:
            f0 = .45 - .33 * (y - y0) / (top - y0)
            y1 = y + .9
            f1 = .45 - .33 * (y1 - y0) / (top - y0)
            for (a, b) in (((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))):
                p, q = ((a, f0, y), (b, f1, y1)) if k % 2 == 0 else ((b, f0, y), (a, f1, y1))
                beam(g, (x + p[0][0] * p[1], p[2], z + p[0][1] * p[1]), (x + q[0][0] * q[1], q[2], z + q[0][1] * q[1]), .025, .025, F['steel'], T, bevel=0)
            y = y1
            k += 1
    box(g, x - .3, x + .3, y0, y0 + .12, z - .3, z + .3, F['concrete'], T, .02, cell=0)
    cylinder(g, (x, top, z), (x, top + .1, z), .1, F['steeldark'], 8)
    # its guy wires
    if T.detail:
        for (dx, dz) in ((2.6, 1.6), (-2.2, 2.2), (.5, -3.0)):
            sweep(g, sk.circle_section(.008, 3), [(x + dx, y0 + .05, z + dz), (x, top - 1.2, z)], F['steel'], caps=False)


def roof(T):
    rng = np.random.default_rng(31)
    g = Geo()
    x0, x1, zb = -8.5, 8.5, -11.0
    z0 = PARAPET[0]
    # the deck: membrane in bays with darker seams and wet patches (paint_roof makes the puddles)
    rk.ground(g, x0, x1, zb, z0, GROUND, F['membrane'], 1.2 if T.fine else 3.0)
    if T.detail:
        for k in range(1, 7):
            xs = x0 + k * (x1 - x0) / 7
            g.face(((xs - .03, GROUND + .004, zb), (xs + .03, GROUND + .004, zb), (xs + .03, GROUND + .004, z0), (xs - .03, GROUND + .004, z0)), F['tar'], (0, 1, 0))
    # pavers: a walkway from the bulkhead's door to the front
    for k in range(9):
        z = -6.4 + k * .8
        box(g, 5.6, 6.5, GROUND, GROUND + .05, z, z + .7, F['pavers'], T, .01, cell=0)
    # the parapets: front (low, the coping the rifle rests on), sides and back (taller)
    box(g, x0 - .4, x1 + .4, GROUND, COPING, PARAPET[0], PARAPET[1], F['brickroof'], T, .02, cell=1.2 if T.fine else 0)
    box(g, x0 - .45, x1 + .45, COPING, COPING + .08, PARAPET[0] - .05, PARAPET[1] + .05, F['coping'], T, .015, cell=1.5 if T.fine else 0)
    for sx in (-1, 1):
        xa, xb = sorted((sx * x1, sx * (x1 + .4)))
        box(g, xa, xb, GROUND, 1.2, zb - .4, PARAPET[0], F['brickroof'], T, .02, cell=1.5 if T.fine else 0)
        box(g, xa - .05, xb + .05, 1.2, 1.28, zb - .45, PARAPET[0], F['coping'], T, .015, cell=0)
    box(g, x0 - .4, x1 + .4, GROUND, 1.2, zb - .4, zb, F['brickroof'], T, .02, cell=1.5 if T.fine else 0)
    # the building's front falling away to the street, its windows lit in rows, a cornice under the parapet
    fz = PARAPET[1]
    box(g, x0 - .4, x1 + .4, STREET_Y, GROUND, fz - 1.0, fz, F['facade'], T, 0, cell=0, skip=('-z', '+y'))
    box(g, x0 - .6, x1 + .6, -1.0, -.6, fz - .2, fz + .35, F['coping'], T, .02, cell=0)
    windows(g, rng, x0, x1, STREET_Y + 4, -2.5, fz, .35, floor=3.6, bay=2.2, lift=.05, facing=1)
    # plant: the water tank, condensers, the bulkhead, the mast, vents and pipes
    water_tank(g, (-4.6, 0, -6.6), T)
    condenser(g, (3.2, 0, -3.4), 2.2, 1.3, 1.25, T)
    condenser(g, (4.2, 0, -8.8), 1.6, 1.2, 1.1, T)
    bulkhead(g, (6.0, 0, -8.2), T)
    mast(g, T)
    for (x, z) in ((-1.5, -9.4), (1.0, -2.0), (-7.0, -9.0)):
        cylinder(g, (x, GROUND, z), (x, .7, z), .1, F['galv'], 8)
        lathe(g, [(.18, .7), (.18, .78), (.02, .83)], (x, 0, z), F['galv'], 8)
    sweep(g, sk.circle_section(.06, 6 if T.fine else 4), [(3.2, .35, -2.75), (3.2, .35, -1.0), (-2.0, .35, -1.0), (-2.0, .35, -9.6)], F['steel'], caps=True)
    for k in range(5):
        box(g, -2.0 + k * 1.3 - .12, -2.0 + k * 1.3 + .12, GROUND, .3, -1.08, -.92, F['concretedark'], T, 0, cell=0)
    # a skylight: a glass hip over a low curb
    box(g, -3.0, -1.0, GROUND, .35, -5.2, -3.6, F['concrete'], T, .02, cell=0)
    for (a, b, c) in (((-3.0, .35, -5.2), (-1.0, .35, -5.2), (-2.0, .9, -4.4)), ((-1.0, .35, -3.6), (-3.0, .35, -3.6), (-2.0, .9, -4.4)),
                      ((-3.0, .35, -3.6), (-3.0, .35, -5.2), (-2.0, .9, -4.4)), ((-1.0, .35, -5.2), (-1.0, .35, -3.6), (-2.0, .9, -4.4))):
        cen = ((a[0] + b[0] + c[0]) / 3 + 2.0, .3, (a[2] + b[2] + c[2]) / 3 + 4.4)
        g.face((a, b, c), F['glassdark'], sk._norm(cen))
    # a tarp-covered crate and cable reels by the side parapet
    box(g, -8.0, -6.9, GROUND, .8, -3.4, -2.2, F['steeldark'], T, .05, cell=0)
    if T.detail:
        for z in (-1.5, -.6):
            lathe(g, [(.36, -.12), (.36, .12)], (-7.7, .37, z), F['tank'], 10, axis=(1, 0, 0), cap0=True, cap1=True)
    return g


def street(T):
    """The avenue far below, its lamps, the neighbours' near walls and roofs left and right, the tower behind."""
    rng = np.random.default_rng(57)
    g = Geo()
    rk.ground(g, -80.0, 80.0, 1.6, 60.0, STREET_Y, F['asphalt'], 20.0 if T.fine else 40.0)
    for k in range(9):
        z = 6.0 + k * 6.0
        for sx in (-1, 1):
            x = sx * 14.0 + k * 1.5 - 6
            box(g, x - .1, x + .1, STREET_Y, STREET_Y + 7, z - .1, z + .1, F['steeldark'], T, 0, cell=0, skip=('-y',))
            box(g, x - .3, x + .3, STREET_Y + 6.8, STREET_Y + 7.0, z - .3, z + .3, F['streetlamp'], T, 0, cell=0)
    # the neighbour to the left: a lower roof (y -7) with its own parapet; the one to the right taller (its wall beside our roof)
    box(g, -34.0, -9.0, STREET_Y, -7.0, -14.0, 1.6, F['tower2'], T, 0, cell=0, skip=('-y',))
    box(g, -34.0, -9.0, -7.0, -6.1, 1.3, 1.6, F['coping'], T, 0, cell=0)
    windows(g, rng, -34.0, -9.0, STREET_Y + 4, -9.0, 1.6, .3, lift=.05, facing=1)
    box(g, 9.0, 28.0, STREET_Y, 8.5, -16.0, 1.2, F['tower1'], T, 0, cell=0, skip=('-y',))
    windows(g, rng, 9.0, 28.0, STREET_Y + 4, 7.0, 1.2, .3, lift=.05, facing=1)
    # its side wall toward our roof: a few lit windows over our parapet height
    for (z, y) in ((-3.0, 2.0), (-9.0, 2.0), (-6.0, 5.6)):
        g.face(((8.94, y, z - .7), (8.94, y, z + .7), (8.94, y + 1.6, z + .7), (8.94, y + 1.6, z - .7)), F['winwarm'], (-1, 0, 0))
    # the tower behind the drone, the billboard on its roof: the drone hangs against its lit windows
    hx0, hx1, hz0, hz1, hy = HERO_TOWER
    box(g, hx0, hx1, STREET_Y, hy, hz0, hz1, F['tower2'], T, 0, cell=0, skip=('-y',))
    box(g, hx0 - .3, hx1 + .3, hy - .5, hy + .4, hz0 - .3, hz1 + .3, F['coping'], T, 0, cell=0, skip=('-y',))
    windows(g, rng, hx0, hx1, STREET_Y + 4, hy - 1.5, hz0, .4, floor=3.4, bay=1.9)
    # the tower behind us
    box(g, -12.0, 14.0, STREET_Y, 46.0, -46.0, -20.0, F['tower3'], T, 0, cell=0, skip=('-y',))
    return g


def billboard(T):
    """A rooftop billboard: a steel frame and a dark board with ARENA in cyan neon in a magenta frame, facing -z; origin at its foot."""
    g = Geo()
    w, h = 14.0, 5.0
    for x in (-5.5, 0.0, 5.5):
        box(g, x - .15, x + .15, 0, 4.0, -.15, .15, F['steeldark'], T, 0, cell=0)
        beam(g, (x, 0, -.15), (x, 3.8, 1.8), .1, .1, F['steeldark'], T, bevel=0)
    box(g, -w / 2, w / 2, 4.0, 4.0 + h, -.2, .1, F['tower1'], T, 0, cell=0)
    cell = .55
    text = 'ARENA'
    width = (6 * len(text) - 2) * cell
    rk.lettering(g, text, (-width / 2, 4.0 + h / 2 - 3 * cell, -.3), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), cell, F['neoncyan'], .09, 6 if T.fine else 4)
    frame = [(-w / 2 + .3, 4.3, -.3), (w / 2 - .3, 4.3, -.3), (w / 2 - .3, 3.7 + h, -.3), (-w / 2 + .3, 3.7 + h, -.3)]
    sweep(g, sk.circle_section(.07, 5 if T.fine else 3), frame, F['neonmag'], closed=True, caps=False)
    return g


def sky(T):
    g = Geo()
    rk.dome(g, 180.0, F['sky1'], 12 if T.fine else 7, 36 if T.fine else 18, below=-.2)
    return g


def paint_sky(g, bk):
    def fn(P):
        d = P / np.linalg.norm(P, axis=1, keepdims=True)
        e = d[:, 1]
        az = np.arctan2(d[:, 0], d[:, 2])
        glow = rk.hexrgb(0x5A2E52)
        haze = rk.hexrgb(0x2A2440)
        zen = rk.hexrgb(0x05070E)
        low = rk.hexrgb(0x3A1E30)
        n = len(P)
        # the low cloud lit from below by the city: a band over the horizon, warmer toward the avenue's far end
        warm = .5 + .5 * np.cos(az)
        band = rk.lerp3(np.tile(haze, (n, 1)), np.tile(glow, (n, 1)), warm * .8)
        cloud = sk.fbm(np.stack([d[:, 0] * 3, e * 6, d[:, 2] * 3], axis=1), 1.0, 3, 5)
        band = band * (.75 + .5 * cloud)[:, None]
        c = rk.lerp3(band, np.tile(zen, (n, 1)), np.clip(e / .45, 0, 1) ** .7)
        c = np.where((e < 0)[:, None], rk.lerp3(c, np.tile(low, (n, 1)), np.clip(-e * 8, 0, 1)), c)
        return c
    rk.paint(g, bk, fn, F['sky1'])


def paint_roof(g, bk):
    """Puddles on the membrane: smooth dark patches (they mirror the city in the wet)."""
    P, N, S, Fi = g.arrays()
    fc = np.repeat(Fi, S)
    mask = (fc == F['membrane']) & (N[:, 1] > .7)
    if not mask.any():
        return
    W = P[mask].astype(np.float64)
    w = sk.smooth(.45, .62, sk.fbm(W * np.array([1.0, 0, 1.0]), .55, 3, 21))
    sm = bk.uv0[mask, 0]
    bk.uv0[mask, 0] = (sm + (.97 - sm) * w).astype(np.float32)
    col = bk.colour[mask, :3].astype(np.float64)
    bk.colour[mask, :3] = np.round(col * (1 - .45 * w)[:, None]).astype(np.uint8)


def build(out, T):
    t = out
    t.add('onebreath/roof', roof(T), T.i, floors=(GROUND,), ao_distance=1.0, ao_samples=16 if T.i == 0 else 8, paint_fn=paint_roof, show=True)
    t.add('onebreath/street', street(T), T.i, ao=False, show=True)
    for k, name in enumerate('abcde'):
        g = block(name, T, 100 + k)
        if T.i == 0:
            # (one tier: at their distances the Low tier would be the same towers)
            t.add('onebreath/block-' + name, g, T.i, ao=False)
        if T.i == 0:
            for (b, x, z, yw, s) in BLOCKS:
                if b == name:
                    gg = g.transformed(rk.yaw(yw, (x, 0, z), s))
                    out.preview.append((gg, sk.Bake(gg, None)))
    t.add('onebreath/billboard', billboard(T), T.i, ao=False, show=rk.yaw(0, BILLBOARD))
    if T.i == 0:
        t.add('onebreath/farcity', farcity(T), T.i, ao=False, show=True)
    t.add('onebreath/sky', sky(T), T.i, ao=False, paint_fn=paint_sky, show=True)
    t.add('onebreath/beacon', beacon(T), T.i, ao=False, show=rk.yaw(0, BEACON))
    t.add('onebreath/drone', drone(T), T.i, ao_distance=.3, show=rk.yaw(math.pi, DRONE))
    t.add('onebreath/eye', eye(T), T.i, ao=False, show=rk.yaw(math.pi, DRONE))
    t.add('onebreath/rotor', rotor(T), T.i, ao=False)
    for name, fn in (('casing', casing), ('shard', shard), ('panel', panel)):
        t.add('onebreath/' + name, fn(T), T.i, ao=False)


def views():
    return [('ob-roof', (2.5, 3.5, -9.0), (-1.0, .6, 2.0), 60), ('ob-skyline', (.3, 1.25, -.4), DRONE, 40), ('ob-tele', (.3, 1.25, -.4), DRONE, 6),
            ('ob-back', (DRONE[0] - 1.5, DRONE[1], DRONE[2] - 4.0), (0, .8, 0), 8), ('ob-wide', (-14.0, 12.0, 14.0), (2.0, 0, -4.0), 60),
            ('ob-hero', (1.4, .5, 2.9), (0, 1.0, 0), 34)]
