"""Brass Storm's set (the machine gun's intro, docs/INTROS-RANGED.md): a freight depot's yard at night. The pilot stands at the origin
in a sandbag emplacement facing +z, across the wet concrete to a corrugated warehouse with a roll-up shutter and a work light behind
it; oil drums and stacked crates before it, shipping containers stacked down both sides, sodium floodlights on poles, a guard booth
and the back wall behind the pilot. The rounds' holes and the light through them are the intro's effects; the brass that piles
round the pilot's boots is its set pieces.

Props (Unity space; the intro turns them by Facing):
  brass/yard        the yard, the warehouse, the containers, the emplacement and the plant (static)
  brass/sky         the overcast night, sodium haze low down (glow: unlit, painted)
  brass/flood       a sodium floodlight's lamp face (glow; the intro switches each on); origin at the lamp's centre, facing +z
  brass/drum        an oil drum standing (origin at its foot's centre)
  brass/crate       a wooden crate (origin at its foot's centre), brass/board a broken board from one
  brass/casing      a spent cartridge case along +z (origin at its middle)
  brass/pile        the heap of spent brass at the pilot's feet at its fullest (origin at the ground; the intro grows it)
  brass/can         the open ammunition can on the sandbags with its spare belt (origin at its foot)
"""
import math

import numpy as np

import ranged_kit as rk
from ranged_kit import F, sk, Geo, box, cylinder, lathe, sweep, beam, quat_axis, affine

GROUND = .012
SHUTTER = (-3.6, 3.6, 0.0, 5.0, 12.0)   # the shutter's x0, x1, y0, y1 and its face (z)
WALL_Z = 12.2                            # the warehouse's front wall
FLOODS = [(-8.6, 7.6, 2.6, -.55), (8.8, 7.6, 4.2, .6), (0.0, 7.0, 12.0 - .5, math.pi)]   # lamp centres (x, y, z) and their yaw
DRUMS = [(2.6, 8.4), (3.35, 8.9), (2.95, 9.6), (3.9, 9.9), (2.2, 9.9)]   # oil drums' feet (x, z)
CRATES = [(-4.6, 8.6, 1.0, .1), (-5.7, 9.1, 1.0, -.15), (-4.9, 9.4, .9, .05)]   # crates (x, z, size, yaw); the third stands on the first two
BOOTS = (0.0, 0.0)                        # where the brass piles (x, z): round the pilot's right boot


def finishes():
    A = sk
    rk.fin('slab', 0x55565C, A.DETAIL_PLASTER, smooth=.35, dirt=.9, wear=.2)
    rk.fin('slabwet', 0x3A3B42, A.DETAIL_NONE, smooth=.9, dirt=.3)
    rk.fin('joint', 0x2A2B30, A.DETAIL_NONE, smooth=.2, dirt=.3)
    rk.fin('lineyellow', 0xD8A830, A.DETAIL_PLASTER, smooth=.3, dirt=.8, wear=.3)
    rk.fin('corr', 0x6A7280, A.DETAIL_IRON, smooth=.5, metal=.5, dirt=.6, wear=.3)
    rk.fin('corrdark', 0x4A505C, A.DETAIL_IRON, smooth=.5, metal=.5, dirt=.6, wear=.3)
    rk.fin('shutter', 0x8A8E96, A.DETAIL_IRON, smooth=.45, metal=.55, dirt=.7, wear=.35)
    rk.fin('trimsteel', 0x2E3138, A.DETAIL_IRON, smooth=.5, metal=.5, dirt=.5, wear=.3)
    rk.fin('concrete', 0x6A6A6E, A.DETAIL_PLASTER, smooth=.25, dirt=.9, wear=.3)
    rk.fin('hazy', 0xE2B021, A.DETAIL_NONE, smooth=.35, dirt=.6, wear=.4)
    rk.fin('hazk', 0x18181C, A.DETAIL_NONE, smooth=.35, dirt=.5)
    rk.fin('sandbag', 0x8E7A56, A.DETAIL_CLOTH, smooth=.1, dirt=.9)
    rk.fin('sandbagdark', 0x6E5E42, A.DETAIL_CLOTH, smooth=.1, dirt=.9)
    rk.fin('drumblue', 0x2A4A7A, A.DETAIL_IRON, smooth=.5, metal=.4, dirt=.6, wear=.5)
    rk.fin('drumred', 0x8A2A22, A.DETAIL_IRON, smooth=.5, metal=.4, dirt=.6, wear=.5)
    rk.fin('crate', 0x9A7A4E, A.DETAIL_WOOD, smooth=.2, param=0, dirt=.8, wear=.3)
    rk.fin('cratez', 0x9A7A4E, A.DETAIL_WOOD, smooth=.2, param=2, dirt=.8, wear=.3)
    rk.fin('pallet', 0x7A6040, A.DETAIL_WOOD, smooth=.15, param=0, dirt=.9, wear=.3)
    for i, c in enumerate((0x8A2E28, 0x2E5A7A, 0x3A6A3E, 0xB0782A)):
        rk.fin('cont%d' % i, c, A.DETAIL_IRON, smooth=.45, metal=.4, dirt=.7, wear=.45)
    rk.fin('olive', 0x4A5034, A.DETAIL_IRON, smooth=.4, metal=.4, dirt=.6, wear=.4)
    rk.fin('brass', 0xC8963E, A.DETAIL_BRASS, smooth=.75, metal=.8, dirt=.2)
    rk.fin('brassdark', 0x8A6428, A.DETAIL_BRASS, smooth=.6, metal=.8, dirt=.3)
    rk.fin('steel', 0x50565E, A.DETAIL_IRON, smooth=.5, metal=.5, dirt=.5, wear=.3)
    rk.fin('glassd', 0x10141A, A.DETAIL_NONE, smooth=.95, metal=.3, dirt=.2)
    rk.fin('paintwhite', 0xC8C8C0, A.DETAIL_NONE, smooth=.3, dirt=.7, wear=.3)
    rk.fin('sky2', 0xFFFFFF, A.DETAIL_GLOW, glow=1.0)
    rk.fin('sodium', 0xFFB050, A.DETAIL_GLOW, glow=3.2)
    rk.fin('worklight', 0xFFE8C0, A.DETAIL_GLOW, glow=2.6)
    rk.fin('winlit', 0xFFD8A0, A.DETAIL_GLOW, glow=1.6)
    rk.fin('booth', 0x9AD0FF, A.DETAIL_GLOW, glow=1.4)
    rk.fin('redsign', 0xFF3A2A, A.DETAIL_GLOW, glow=2.6)


def corrugated(g, x0, x1, y0, y1, z, f, pitch, T, facing=-1, depth=.035, vertical=True):
    """Corrugated sheet on a face at z (normal 0, 0, facing): ribs `pitch` apart, flat-shaded in two slopes."""
    if not T.fine:
        g.face(((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)), f, (0, 0, facing))
        return
    if vertical:
        n = max(1, int((x1 - x0) / pitch))
        for k in range(n):
            a = x0 + k * (x1 - x0) / n
            m = a + .5 * (x1 - x0) / n
            b = a + (x1 - x0) / n
            zf = z + facing * depth
            for (p, q, zp, zq) in ((a, m, z, zf), (m, b, zf, z)):
                nrm = sk._norm((facing * (zp - zq), 0, facing * (q - p)))
                g.face(((p, y0, zp), (q, y0, zq), (q, y1, zq), (p, y1, zp)), f, nrm)
    else:
        n = max(1, int((y1 - y0) / pitch))
        for k in range(n):
            a = y0 + k * (y1 - y0) / n
            m = a + .5 * (y1 - y0) / n
            b = a + (y1 - y0) / n
            zf = z + facing * depth
            for (p, q, zp, zq) in ((a, m, z, zf), (m, b, zf, z)):
                nrm = sk._norm((0, facing * (zp - zq), facing * (q - p)))
                g.face(((x0, p, zp), (x1, p, zp), (x1, q, zq), (x0, q, zq)), f, nrm)


def warehouse(g, T, rng):
    x0, x1, h = -15.0, 15.0, 9.0
    s0, s1, sy0, sy1, sz = SHUTTER
    # the shell behind the front: a deep box (its front wall replaced by the cladding)
    box(g, x0, x1, GROUND, h, WALL_Z, WALL_Z + 22, F['corrdark'], T, 0, cell=0, skip=('-z', '-y'))
    # the front wall's cladding round the shutter's opening (vertical ribs) and a band of horizontal ribs on top
    for (a, b, c, d) in ((x0, s0 - .35, GROUND, h - 1.2), (s1 + .35, x1, GROUND, h - 1.2), (s0 - .35, s1 + .35, sy1 + .55, h - 1.2)):
        corrugated(g, a, b, c, d, WALL_Z, F['corr'], .18, T)
    corrugated(g, x0, x1, h - 1.2, h, WALL_Z, F['corrdark'], .22, T, vertical=False)
    # the roof's edge and gutter
    box(g, x0 - .2, x1 + .2, h, h + .25, WALL_Z - .3, WALL_Z + .2, F['trimsteel'], T, .02, cell=0)
    # the shutter: horizontal slats in a recess, its guides and the roller's housing
    corrugated(g, s0, s1, sy0 + GROUND, sy1, SHUTTER[4], F['shutter'], .11, T, vertical=False, depth=.03)
    box(g, s0, s1, sy0 + GROUND, sy0 + .1, SHUTTER[4] - .06, SHUTTER[4] + .02, F['trimsteel'], T, 0, cell=0)
    for x in (s0 - .35, s1):
        box(g, x, x + .35, GROUND, sy1 + .3, WALL_Z - .25, WALL_Z, F['trimsteel'], T, .02, cell=0)
    box(g, s0 - .45, s1 + .45, sy1, sy1 + .65, WALL_Z - .55, WALL_Z, F['trimsteel'], T, .03, cell=0)
    # hazard stripes up the guides
    if T.detail:
        for x in (s0 - .35, s1 + .002):
            for k in range(8):
                y = .2 + k * .55
                q = quat_axis(0, 0, 1, .7)
                t = Geo()
                box(t, -.05, .05, -.32, .32, 0, .01, F['hazy'], T, 0, cell=0)
                g.extend(t.transformed(affine(q, (x + .175, y + .25, WALL_Z - .26))))
    # the loading dock to the left, its door and bumpers; a man door and a lit office window to the right
    box(g, -14.5, -6.0, GROUND, 1.25, WALL_Z - 3.2, WALL_Z, F['concrete'], T, .03, cell=1.5 if T.fine else 0)
    for k in range(int((14.5 - 6.0) / .5) if T.detail else 0):
        x = -14.5 + k * .5
        box(g, x, x + .25, 1.0, 1.25, WALL_Z - 3.22, WALL_Z - 3.19, F['hazy' if k % 2 == 0 else 'hazk'], T, 0, cell=0)
    for x in (-12.8, -9.2):
        box(g, x - .2, x + .2, .55, 1.15, WALL_Z - 3.4, WALL_Z - 3.2, F['hazk'], T, .03, cell=0)
    corrugated(g, -12.5, -8.0, 1.25, 4.6, WALL_Z - .02, F['shutter'], .12, T, vertical=False, depth=.025)
    g.face(((7.5, GROUND, WALL_Z - .04), (8.6, GROUND, WALL_Z - .04), (8.6, 2.2, WALL_Z - .04), (7.5, 2.2, WALL_Z - .04)), F['trimsteel'], (0, 0, -1))
    g.face(((10.0, 1.2, WALL_Z - .04), (13.0, 1.2, WALL_Z - .04), (13.0, 2.4, WALL_Z - .04), (10.0, 2.4, WALL_Z - .04)), F['winlit'], (0, 0, -1))
    box(g, 9.9, 13.1, 1.1, 1.2, WALL_Z - .15, WALL_Z, F['trimsteel'], T, 0, cell=0)
    # the depot's number painted large over the shutter
    cell = .32
    text = 'DEPOT 7'
    width = (6 * len(text) - 2) * cell
    rk.lettering(g, text, (-width / 2, 6.25, WALL_Z - .06), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), cell, F['paintwhite'], .07, 4 if T.fine else 3)
    # conduit and a red sign over the man door
    sweep(g, sk.circle_section(.04, 5), [(6.8, GROUND + .3, WALL_Z - .08), (6.8, 6.5, WALL_Z - .08), (2.5, 6.5, WALL_Z - .08)], F['trimsteel'], caps=True)
    box(g, 7.6, 8.5, 2.35, 2.65, WALL_Z - .12, WALL_Z - .04, F['redsign'], T, 0, cell=0)


def sandbag(g, centre, yaw_, T, f):
    c = (centre[0], centre[1] + .085, centre[2])
    if T.fine:
        rk.pillow(g, c, (.31, .085, .18), yaw_, f, nu=10, nv=6, e=.42, sag=.012)
    else:
        rk.pillow(g, c, (.31, .085, .18), yaw_, f, nu=6, nv=4, e=.5)


def emplacement(g, T, rng):
    """The sandbag wall in an arc in front of the pilot (two courses and a third on the flanks), a firing step behind it."""
    r = 1.25
    for course in range(3):
        n = 7
        for k in range(n):
            if course == 2 and 1 < k < n - 2:
                continue
            a = (-.95 + 1.9 * (k + (.5 if course % 2 else 0)) / n)
            if a > .98:
                continue
            c = (math.sin(a) * r, GROUND + course * .155, math.cos(a) * r)
            sandbag(g, c, a + rng.uniform(-.06, .06), T, F['sandbag' if (k + course) % 3 else 'sandbagdark'])
    # a few loose bags at the ends
    for (x, z, a) in ((-1.45, .2, 1.2), (1.5, .1, -1.1)):
        sandbag(g, (x, GROUND, z), a, T, F['sandbagdark'])


def container(g, x, z, yaw_, f, T, stack=0):
    """A 6 m shipping container (2.44 wide, 2.6 tall) standing at (x, z) turned by yaw, ribbed on its long sides."""
    t = Geo()
    L, W, H = 6.06, 2.44, 2.59
    box(t, -W / 2, W / 2, 0, H, -L / 2, L / 2, f, T, .03, cell=0)
    if T.fine:
        for s in (-1, 1):
            n = 16
            for k in range(n):
                zc = -L / 2 + .3 + k * (L - .6) / (n - 1)
                box(t, s * W / 2, s * (W / 2 + .03), .1, H - .1, zc - .05, zc + .05, f, T, 0, cell=0, skip=('+x' if s < 0 else '-x',))
        # the doors' bars on one end
        for xx in (-.75, -.35, .35, .75):
            box(t, xx - .025, xx + .025, .15, H - .15, L / 2, L / 2 + .04, F['trimsteel'], T, 0, cell=0)
    g.extend(t.transformed(rk.yaw(yaw_, (x, GROUND + stack * 2.6, z))))


def floodpole(g, at, T):
    x, y, z, a = at
    box(g, x - .12, x + .12, GROUND, y - .1, z - .12, z + .12, F['steel'], T, .02, cell=0)
    box(g, x - .5, x + .5, GROUND, .3, z - .5, z + .5, F['concrete'], T, .03, cell=0)
    # the lamp's housing (the face is the flood prop) on a yoke
    t = Geo()
    box(t, -.42, .42, -.3, .3, -.4, .02, F['trimsteel'], T, .03, cell=0)
    beam(t, (-.5, 0, -.1), (-.5, -.35, -.1), .05, .05, F['steel'], T, bevel=0)
    beam(t, (.5, 0, -.1), (.5, -.35, -.1), .05, .05, F['steel'], T, bevel=0)
    g.extend(t.transformed(rk.yaw(a, (x, y, z))))


def booth(g, T):
    """The guard booth behind the pilot (its window lit blue) and the back wall with its gate."""
    x, z = -11.5, -10.5
    box(g, x - 1.4, x + 1.4, GROUND, 2.6, z - 1.4, z + 1.4, F['paintwhite'], T, .03, cell=0)
    box(g, x - 1.55, x + 1.55, 2.6, 2.75, z - 1.55, z + 1.55, F['trimsteel'], T, .02, cell=0)
    g.face(((x - 1.0, 1.1, z + 1.41), (x + 1.0, 1.1, z + 1.41), (x + 1.0, 2.1, z + 1.41), (x - 1.0, 2.1, z + 1.41)), F['booth'], (0, 0, 1))
    g.face(((x + 1.41, 1.1, z - .9), (x + 1.41, 1.1, z + .9), (x + 1.41, 2.1, z + .9), (x + 1.41, 2.1, z - .9)), F['booth'], (1, 0, 0))
    # the back wall: precast panels with a band of lit windows high up, the gate's two steel posts
    box(g, -24.0, 24.0, GROUND, 6.5, -26.4, -26.0, F['concrete'], T, 0, cell=2.5 if T.fine else 0)
    for k in range(12):
        xx = -22.0 + k * 4.0
        box(g, xx - .12, xx + .12, GROUND, 6.6, -26.05, -25.9, F['trimsteel'], T, 0, cell=0)
        if k % 3 != 1:
            g.face(((xx + .6, 4.6, -25.95), (xx + 3.2, 4.6, -25.95), (xx + 3.2, 5.6, -25.95), (xx + .6, 5.6, -25.95)), F['winlit'], (0, 0, 1))
    for x2 in (2.0, 7.0):
        box(g, x2 - .2, x2 + .2, GROUND, 3.2, -25.9, -25.5, F['hazk'], T, .02, cell=0)


def yard(T):
    rng = np.random.default_rng(77)
    g = Geo()
    rk.ground(g, -24.0, 24.0, -26.0, WALL_Z, GROUND, F['slab'], 1.5 if T.fine else 4.0)
    # expansion joints and painted lines
    if T.detail:
        for k in range(-4, 5):
            x = k * 5.0
            g.face(((x - .02, GROUND + .003, -26), (x + .02, GROUND + .003, -26), (x + .02, GROUND + .003, WALL_Z), (x - .02, GROUND + .003, WALL_Z)), F['joint'], (0, 1, 0))
        for k in range(-4, 6):
            z = k * 5.0 - 3.0
            g.face(((-24, GROUND + .003, z - .02), (24, GROUND + .003, z - .02), (24, GROUND + .003, z + .02), (-24, GROUND + .003, z + .02)), F['joint'], (0, 1, 0))
    for (x0, x1, z0, z1) in ((-4.4, -4.25, 2.0, 10.6), (4.25, 4.4, 2.0, 10.6), (-4.4, 4.4, 10.45, 10.6)):
        g.face(((x0, GROUND + .004, z0), (x1, GROUND + .004, z0), (x1, GROUND + .004, z1), (x0, GROUND + .004, z1)), F['lineyellow'], (0, 1, 0))
    warehouse(g, T, rng)
    emplacement(g, T, rng)
    # containers down both sides, two high in places
    for (x, z, a, f, st) in ((-17.5, 4.0, 0.0, 'cont0', 0), (-17.5, 4.0, 0.0, 'cont1', 1), (-17.5, -2.4, 0.0, 'cont2', 0), (-14.6, 9.0, .1, 'cont3', 0),
                             (17.8, 2.0, 0.0, 'cont1', 0), (17.8, 2.0, .02, 'cont2', 1), (17.8, -4.6, 0.0, 'cont0', 0), (15.0, -9.5, 1.45, 'cont3', 0),
                             # behind the pilot, under the hero frame's eye line: two stacks and a single, end on
                             (-3.8, -13.0, 1.52, 'cont2', 0), (-3.8, -13.0, 1.55, 'cont0', 1), (3.6, -15.5, 1.6, 'cont3', 0), (3.6, -15.5, 1.57, 'cont1', 1), (10.0, -12.0, 1.4, 'cont0', 0)):
        container(g, x, z, a, F[f], T, st)
    for p in FLOODS[:2]:
        floodpole(g, p, T)
    booth(g, T)
    # pallets and a jersey barrier or two
    for (x, z, a) in ((-7.5, 6.0, .3), (6.5, 2.5, -.2)):
        t = Geo()
        for k in range(5):
            box(t, -.6, .6, .1, .13, -.6 + k * .28, -.5 + k * .28, F['pallet'], T, 0, cell=0)
        for k in range(3):
            box(t, -.6 + k * .55, -.5 + k * .55, 0, .1, -.6, .6, F['pallet'], T, 0, cell=0)
        g.extend(t.transformed(rk.yaw(a, (x, GROUND, z))))
    for (x, z, a) in ((-10.0, -4.0, .1), (10.5, -6.0, -.15)):
        t = Geo()
        rk.prism_z(t, [(-.3, 0), (.3, 0), (.12, .3), (.1, .8), (-.1, .8), (-.12, .3)], -1.5, 1.5, F['concrete'])
        g.extend(t.transformed(rk.yaw(a, (x, GROUND, z))))
    return g


def paint_yard(g, bk):
    """Oil stains and wet patches on the slab."""
    P, N, S, Fi = g.arrays()
    fc = np.repeat(Fi, S)
    mask = (fc == F['slab']) & (N[:, 1] > .7)
    if not mask.any():
        return
    W = P[mask].astype(np.float64)
    wet = sk.smooth(.5, .7, sk.fbm(W * np.array([1.0, 0, 1.0]), .35, 3, 9))
    oil = sk.smooth(.62, .75, sk.fbm(W * np.array([1.0, 0, 1.0]) + 40, .9, 2, 13))
    sm = bk.uv0[mask, 0]
    bk.uv0[mask, 0] = (sm + (.95 - sm) * np.maximum(wet, oil * .8)).astype(np.float32)
    col = bk.colour[mask, :3].astype(np.float64)
    col *= (1 - .35 * wet - .45 * oil)[:, None]
    bk.colour[mask, :3] = np.round(np.clip(col, 0, 255)).astype(np.uint8)


def flood(T):
    g = Geo()
    g.face(((-.36, -.24, .03), (.36, -.24, .03), (.36, .24, .03), (-.36, .24, .03)), F['sodium'], (0, 0, 1))
    return g


def drum(T):
    g = Geo()
    n = 14 if T.fine else 7
    lathe(g, [(.29, 0), (.29, .88)], (0, GROUND, 0), F['drumblue'], n, cap0=True, cap1=True)
    for hh in (.3, .6):
        lathe(g, [(.296, hh - .02), (.302, hh), (.296, hh + .02)], (0, GROUND, 0), F['drumblue'], n)
    lathe(g, [(.3, .86), (.3, .9)], (0, GROUND, 0), F['trimsteel'], n)
    if T.detail:
        cylinder(g, (.15, .9, .05), (.15, .93, .05), .03, F['trimsteel'], 6)
    return g


def crate(T):
    g = Geo()
    s = 1.0
    box(g, -s / 2, s / 2, 0, s, -s / 2, s / 2, F['crate'], T, .02, cell=0)
    if T.detail:
        for sz in (-1, 1):
            beam(g, (-s / 2 + .05, .05, sz * (s / 2 + .012)), (s / 2 - .05, s - .05, sz * (s / 2 + .012)), .08, .024, F['pallet'], T, bevel=0)
            box(g, -s / 2, s / 2, 0, .1, sz * s / 2, sz * (s / 2 + .02), F['pallet'], T, 0, cell=0)
            box(g, -s / 2, s / 2, s - .1, s, sz * s / 2, sz * (s / 2 + .02), F['pallet'], T, 0, cell=0)
    return g


def board(T):
    g = Geo()
    box(g, -.45, .45, -.012, .012, -.05, .05, F['crate'], T, 0, cell=0)
    return g


def casing(T):
    g = Geo()
    n = 7 if T.fine else 5
    lathe(g, [(.0062, -.026), (.0064, .008), (.005, .018), (.0042, .026)], (0, 0, 0), F['brass'], n, axis=(0, 0, 1), cap0=True, cap1=True)
    return g


def pile(T):
    """Spent brass heaped round the boots: a low mound and casings lying every which way on it."""
    rng = np.random.default_rng(5)
    g = Geo()
    n = 160 if T.fine else 60
    for k in range(n):
        r = .55 * math.sqrt(rng.random())
        a = rng.uniform(0, 2 * math.pi)
        x, z = BOOTS[0] + .35 + math.cos(a) * r * 1.3, BOOTS[1] + .05 + math.sin(a) * r
        y = GROUND + .006 + .05 * (1 - r / .55) * rng.random()
        q = quat_axis(0, 1, 0, rng.uniform(0, 2 * math.pi))
        q2 = quat_axis(1, 0, 0, rng.uniform(-.25, .25))
        m = np.array(affine(q, (x, y, z))).reshape(3, 4)
        m2 = np.array(affine(q2)).reshape(3, 4)
        R = m[:, :3] @ m2[:, :3]
        t = Geo()
        lathe(t, [(.0062, -.026), (.0064, .008), (.0042, .026)], (0, 0, 0), F['brass' if k % 4 else 'brassdark'], 5 if T.fine else 4, axis=(0, 0, 1), cap1=True)
        g.extend(t.transformed(np.concatenate([R, m[:, 3:]], axis=1).ravel().tolist()))
    return g


def can(T):
    g = Geo()
    box(g, -.15, .15, 0, .2, -.06, .06, F['olive'], T, .01, cell=0)
    if T.detail:
        box(g, -.16, .16, .2, .22, -.07, .07, F['olive'], T, .005, cell=0)
        # the spare belt draped out of it: rounds side by side down over the bag's edge
        for k in range(14):
            u = k / 13
            x = -.12 + .24 * u
            y = .22 - .25 * u * u
            z = .07 + .28 * u
            cylinder(g, (x, y, z), (x + .0, y + .01, z + .065), .0065, F['brass'], 5)
    return g


def sky(T):
    g = Geo()
    rk.dome(g, 180.0, F['sky2'], 10 if T.fine else 6, 32 if T.fine else 16, below=-.15)
    return g


def paint_sky(g, bk):
    def fn(P):
        d = P / np.linalg.norm(P, axis=1, keepdims=True)
        e = d[:, 1]
        n = len(P)
        haze, mid, zen = rk.hexrgb(0x4A2E1C), rk.hexrgb(0x1E1820), rk.hexrgb(0x07080C)
        cloud = sk.fbm(np.stack([d[:, 0] * 4, e * 5, d[:, 2] * 4], axis=1), 1.0, 3, 3)
        c = rk.lerp3(np.tile(haze, (n, 1)), np.tile(mid, (n, 1)), np.clip(e / .25, 0, 1))
        c = rk.lerp3(c, np.tile(zen, (n, 1)), np.clip((e - .2) / .5, 0, 1))
        c = c * (.8 + .4 * cloud)[:, None]
        return c
    rk.paint(g, bk, fn, F['sky2'])


def build(out, T):
    t = out
    t.add('brass/yard', yard(T), T.i, floors=(GROUND, 1.25), ao_distance=1.0, ao_samples=16 if T.i == 0 else 8, paint_fn=paint_yard, show=True)
    t.add('brass/sky', sky(T), T.i, ao=False, paint_fn=paint_sky, show=True)
    t.add('brass/flood', flood(T), T.i, ao=False, show=rk.yaw(FLOODS[0][3], FLOODS[0][:3]))
    t.add('brass/drum', drum(T), T.i, ao_distance=.3, show=rk.yaw(0, (DRUMS[0][0], 0, DRUMS[0][1])))
    t.add('brass/crate', crate(T), T.i, ao_distance=.3, show=rk.yaw(CRATES[0][3], (CRATES[0][0], GROUND, CRATES[0][1])))
    t.add('brass/board', board(T), T.i, ao=False)
    t.add('brass/casing', casing(T), T.i, ao=False)
    t.add('brass/pile', pile(T), T.i, ao=False, show=True)
    t.add('brass/can', can(T), T.i, ao=False, show=rk.yaw(.2, (-.55, GROUND + .31, 1.05)))


def views():
    return [('bs-front', (.4, 1.6, -2.2), (0, 2.4, 12.0), 55), ('bs-back', (.6, 1.2, 9.0), (0, 1.0, 0), 50), ('bs-wide', (-12.0, 7.0, -8.0), (2.0, 1.0, 6.0), 60),
            ('bs-feet', (1.2, .25, -.8), (.3, .1, .4), 50), ('bs-hero', (1.5, .6, 3.2), (0, 1.05, 0), 36)]
