"""Sundown's set (the pistol's intro, docs/INTROS-RANGED.md): a frontier street at sundown. The pilot stands in the dirt street at the
origin facing +z, down the street to the edge of town, where an old timber belfry stands against the low sun; false-fronted
storefronts and their boardwalks line both sides, a saloon with a neon sign, a water tower, a windmill, telegraph poles, a
graveyard, saguaros, and the desert running out to the mesas.

Props (Unity space, the intro turns them by Facing):
  sundown/town        the street, the buildings, the boardwalks, the props, the belfry's frame (static)
  sundown/far         the desert past the town and the mesas (static, coarse)
  sundown/sky         the sunset dome (glow: unlit, painted)
  sundown/bell        the bell, its yoke and clapper; origin at its hang point (the headstock's underside)
  sundown/headstock   the beam the bell hangs from, whole; origin at its middle
  sundown/beam-half   one half of the snapped headstock; origin at its outer end
  sundown/roof        the belfry's pyramid roof and vane; origin at its eaves' centre
  sundown/shingle, sundown/plank, sundown/splinter    debris (origin at their centres)
  sundown/tumbleweed  origin at its centre (radius .36)
  sundown/blades      the windmill's fan; origin at its hub, turning about +z
  sundown/batwing     one of the saloon's swing doors; origin at its hinge, +x across it
"""
import math

import numpy as np

import ranged_kit as rk
from ranged_kit import F, sk, Geo, box, obox, cylinder, lathe, sweep, tube, beam, quat_axis, affine

# The places the intro aims at (Unity space; NativeRangedSets.Sundown mirrors them into the source frame).
BELFRY = (-2.4, 0.0, 10.8)          # the belfry's foot (centre of its plinth)
PLINTH = .9                         # its plinth's top
POST_TOP = 6.9                      # the posts' tops (the roof's eaves)
HANG = 6.32                         # the bell's hang point (the headstock's underside)
BELL_H, BELL_R = 1.1, .56           # the bell's height (crown to lip) and its mouth's radius
STREET = 7.6                        # half the street's width (the boardwalks' edge)
SALOON_DOOR = -7.1                  # the saloon's door (its swing doors hang either side of it, z +-.56, at x = -(FRONT - FT))
FRONT = 10.0                        # the storefronts' faces (|x|)
GROUND = .012                       # the street's top (the pilot's floor)


def finishes():
    A = sk
    rk.fin('dirt', 0xB0865A, A.DETAIL_PLASTER, smooth=.08, dirt=.7)
    rk.fin('rut', 0x8E6A46, A.DETAIL_PLASTER, smooth=.08, dirt=.6)
    rk.fin('sand', 0xC89A68, A.DETAIL_PLASTER, smooth=.06, dirt=.4)
    rk.fin('sandfar', 0xB88A60, A.DETAIL_NONE, smooth=.05, dirt=.2)
    rk.fin('mesa', 0x9A5638, A.DETAIL_PLASTER, smooth=.1, dirt=.5)
    rk.fin('mesadark', 0x6E3C2A, A.DETAIL_PLASTER, smooth=.1, dirt=.5)
    rk.fin('rock', 0x8A6046, A.DETAIL_CARVED, smooth=.15, dirt=.6, wear=.3)
    rk.fin('stone', 0x8C7C68, A.DETAIL_ASHLAR, smooth=.12, dirt=.8, wear=.25)
    for i, c in enumerate((0x8A6A4A, 0x9C8B76, 0x6E5440, 0x7C6450)):
        rk.fin('siding%d' % i, c, A.DETAIL_WOOD, smooth=.18, param=2, dirt=.9, wear=.3)
        rk.fin('sidingx%d' % i, c, A.DETAIL_WOOD, smooth=.18, param=0, dirt=.9, wear=.3)
    for i, c in enumerate((0x8E3B2E, 0x4E7A78, 0xB08A3A, 0xC8BCA6, 0x5A4A6E)):
        rk.fin('paint%d' % i, c, A.DETAIL_WOOD, smooth=.22, param=2, dirt=.8, wear=.45)
        rk.fin('paintx%d' % i, c, A.DETAIL_WOOD, smooth=.22, param=0, dirt=.8, wear=.45)
    rk.fin('trim', 0x4A3626, A.DETAIL_WOOD, smooth=.2, param=1, dirt=.7, wear=.4)
    rk.fin('trimz', 0x4A3626, A.DETAIL_WOOD, smooth=.2, param=2, dirt=.7, wear=.4)
    rk.fin('trimx', 0x4A3626, A.DETAIL_WOOD, smooth=.2, param=0, dirt=.7, wear=.4)
    rk.fin('timber', 0x6A5038, A.DETAIL_WOOD, smooth=.15, param=1, dirt=.9, wear=.35)
    rk.fin('timberz', 0x6A5038, A.DETAIL_WOOD, smooth=.15, param=2, dirt=.9, wear=.35)
    rk.fin('timberx', 0x6A5038, A.DETAIL_WOOD, smooth=.15, param=0, dirt=.9, wear=.35)
    rk.fin('grey', 0x7E7466, A.DETAIL_WOOD, smooth=.12, param=1, dirt=.9, wear=.4)
    rk.fin('deck', 0x7A5E42, A.DETAIL_WOOD, smooth=.2, param=0, dirt=.9, wear=.35)
    rk.fin('shingle', 0x5E4634, A.DETAIL_WOOD, smooth=.12, param=1, dirt=.9, wear=.3)
    rk.fin('tin', 0x7A7468, A.DETAIL_IRON, smooth=.35, metal=.4, dirt=.7, wear=.3)
    rk.fin('iron', 0x2E2A28, A.DETAIL_IRON, smooth=.4, metal=.5, dirt=.5, wear=.3)
    rk.fin('bronze', 0xA8723A, A.DETAIL_BRASS, smooth=.62, metal=.6, dirt=.35, wear=.45)
    rk.fin('bronzedark', 0x6A4422, A.DETAIL_BRASS, smooth=.5, metal=.6, dirt=.4)
    rk.fin('glass', 0x1A1C22, A.DETAIL_NONE, smooth=.92, metal=.3, dirt=.3)
    rk.fin('door', 0x2A1E16, A.DETAIL_WOOD, smooth=.2, param=1, dirt=.6)
    rk.fin('cactus', 0x4E6B3A, A.DETAIL_NONE, smooth=.25, dirt=.4)
    rk.fin('scrub', 0x6E6A3E, A.DETAIL_NONE, smooth=.1, dirt=.3)
    rk.fin('weed', 0x9A7A4E, A.DETAIL_NONE, smooth=.1, dirt=.2)
    rk.fin('canvas', 0xC8B898, A.DETAIL_CLOTH, smooth=.1, dirt=.8)
    rk.fin('rope', 0x8A7450, A.DETAIL_CLOTH, smooth=.1, dirt=.5)
    rk.fin('sky', 0xFFFFFF, A.DETAIL_GLOW, glow=1.0)
    rk.fin('window', 0xFFB066, A.DETAIL_GLOW, glow=1.7)
    rk.fin('lamp', 0xFFC27A, A.DETAIL_GLOW, glow=3.2)
    rk.fin('neonpink', 0xFF4FA0, A.DETAIL_GLOW, glow=4.0)
    rk.fin('neonblue', 0x58E1FF, A.DETAIL_GLOW, glow=3.4)


# ---------------------------------------------------------------------------------------------------------------------------
# Buildings
# ---------------------------------------------------------------------------------------------------------------------------
FT = .14                            # the false fronts' thickness: their faces stand at |x| = FRONT - FT


def window(g, side, z0, z1, y0, y1, T, lit):
    """A sash window in the storefront's face (x = side * (FRONT - FT)): frame, mullions, glass (lit warm or dark)."""
    x = side * (FRONT - FT)
    out = -side
    glass = F['window'] if lit else F['glass']
    back = ('+x',) if side > 0 else ('-x',)
    g.face(((x + out * .02, y0, z0), (x + out * .02, y0, z1), (x + out * .02, y1, z1), (x + out * .02, y1, z0)), glass, (out, 0, 0))
    w = .07
    if not T.detail:
        # the Low tier: one frame round the glass
        box(g, x, x + out * .015, y0 - w, y1 + w, z0 - w, z1 + w, F['trimz'], T, 0, cell=0, skip=back)
        return
    for (a0, a1, b0, b1) in ((y0 - w, y0, z0 - w, z1 + w), (y1, y1 + w, z0 - w, z1 + w)):
        box(g, x, x + out * .07, a0, a1, b0, b1, F['trimz'], T, 0, cell=0, skip=back)
    for zz in (z0 - w, z1):
        box(g, x, x + out * .07, y0, y1, zz, zz + w, F['trim'], T, 0, cell=0, skip=back)
    if T.detail:
        zm = (z0 + z1) / 2
        box(g, x, x + out * .05, y0, y1, zm - .02, zm + .02, F['trim'], T, 0, cell=0, skip=back)
        ym = (y0 + y1) / 2
        box(g, x, x + out * .05, ym - .02, ym + .02, z0, z1, F['trimz'], T, 0, cell=0, skip=back)
    # the sill
    box(g, x, x + out * .14, y0 - .1, y0 - .05, z0 - .12, z1 + .12, F['trimz'], T, 0, cell=0, skip=back)


def door(g, side, z0, z1, h, T):
    x = side * (FRONT - FT)
    out = -side
    back = ('+x',) if side > 0 else ('-x',)
    g.face(((x + out * .01, GROUND + .42, z0), (x + out * .01, GROUND + .42, z1), (x + out * .01, h, z1), (x + out * .01, h, z0)), F['door'], (out, 0, 0))
    box(g, x, x + out * .09, h, h + .1, z0 - .1, z1 + .1, F['trimz'], T, 0, cell=0, skip=back)
    for zz in (z0 - .1, z1):
        box(g, x, x + out * .09, GROUND + .42, h, zz, zz + .1, F['trim'], T, 0, cell=0, skip=back)


def storefront(g, side, z0, z1, h, front_h, top, siding, paint, T, rng, lit=(False, False), sign=True, upper=False, door_z=None, name=None):
    """A false-fronted storefront on side `side` (+1: x > 0, its front facing -x) from z0 to z1: the body (h tall, 7 m deep),
    the false front up to front_h with its `top` outline ('flat', 'step', 'peak', 'round'), the cornice, windows, door, sign."""
    x0 = side * FRONT
    x1 = side * (FRONT + 7.0)
    box(g, x0, x1, GROUND, h, z0, z1, F[siding], T, .03, cell=2.5 if T.fine else 0)
    # a shallow gable roof behind the false front, its ridge along x
    zm = (z0 + z1) / 2
    rp = [(z0 - .15, h), (z1 + .15, h), (zm, h + 1.1)]
    ra, rb = (x0 + .02, x1 + .15) if side > 0 else (x1 - .15, x0 - .02)
    rk.prism_x(g, rp, ra, rb, F['shingle'])
    # the false front: a board wall .14 thick standing proud of the body
    ft = FT
    L = z1 - z0
    # its plain part a box (cut into cells, so the occlusion grades smoothly up it), its shaped top a prism over that
    ycut = front_h if top == 'flat' else front_h - (.8 if top == 'peak' else .7 if top == 'step' else .6)
    xa, xb = (x0 - ft, x0) if side > 0 else (x0, x0 + ft)
    box(g, xa, xb, GROUND, ycut, z0, z1, F[paint], T, 0, cell=1.4 if T.fine else 0)
    outline = None
    if top == 'step':
        s = L * .18
        outline = [(z0, ycut), (z1, ycut), (z1 - s, ycut), (z1 - s, front_h - .35), (z1 - 2 * s, front_h - .35), (z1 - 2 * s, front_h),
                   (z0 + 2 * s, front_h), (z0 + 2 * s, front_h - .35), (z0 + s, front_h - .35), (z0 + s, ycut)]
    elif top == 'peak':
        outline = [(z0, ycut), (z1, ycut), (zm, front_h)]
    elif top == 'round':
        outline = [(z0, ycut), (z1, ycut)]
        n = 8 if T.fine else 4
        for k in range(1, n):
            a = math.pi * k / n
            outline.append((zm + math.cos(a) * L / 2, ycut + math.sin(a) * .6))
    if outline:
        # (drop the corners that coincide with the box's top edge: a degenerate first span would fold the cap)
        clean = []
        for p_ in outline:
            if not clean or abs(clean[-1][0] - p_[0]) + abs(clean[-1][1] - p_[1]) > 1e-4:
                clean.append(p_)
        rk.prism_x(g, clean, xa, xb, F[paint])
    # the cornice along the top edge of the false front (where it runs flat), and a band under the sign
    out = -side
    if top in ('flat', 'step', 'round'):
        ztop0, ztop1 = (z0 + (L * .36 if top == 'step' else 0)), (z1 - (L * .36 if top == 'step' else 0))
        ytop = front_h if top != 'round' else front_h - .6
        back = ('+x',) if side > 0 else ('-x',)
        box(g, x0 + out * ft, x0 + out * (ft + .16), ytop - .12, ytop + .06, ztop0 - .06, ztop1 + .06, F['trimz'], T, .02, cell=0, skip=back)
        if T.detail:
            for k in range(int((ztop1 - ztop0) / .7)):
                zz = ztop0 + .35 + k * .7
                box(g, x0 + out * ft, x0 + out * (ft + .12), ytop - .26, ytop - .12, zz - .05, zz + .05, F['trim'], T, 0, cell=0, skip=back + ('+y',))
    # windows and the door on the ground floor; the upper floor's windows
    dz = zm + (rng.uniform(-.6, .6) if L > 6 else 0)
    if door_z is not None:
        dz = door_z
    door(g, side, dz - .55, dz + .55, GROUND + 2.55, T)
    for (a, b) in ((z0 + .7, dz - 1.0), (dz + 1.0, z1 - .7)):
        if b - a > .9:
            n = max(1, int(round((b - a) / 2.4)))
            gap = .55
            w = (b - a - gap * (n - 1)) / n
            for k in range(n):
                window(g, side, a + k * (w + gap), a + k * (w + gap) + w, GROUND + 1.2, GROUND + 2.45, T, lit[0] and (k + int(abs(a))) % 3 != 1)
    if upper:
        n = max(1, int((L - 1.0) / 2.4))
        for k in range(n):
            c = z0 + (k + .5) * L / n
            window(g, side, c - .55, c + .55, 4.0, 5.3, T, lit[1] and k % 2 == 0)
    # the sign board across the false front over the awning
    if sign:
        sy0, sy1 = 3.45, 4.15 if front_h > 4.7 else 3.95
        back = ('+x',) if side > 0 else ('-x',)
        box(g, x0 + out * ft, x0 + out * (ft + .06), sy0, sy1, z0 + .5, z1 - .5, F['paint%d' % ((int(abs(z0)) + 2) % 4)], T, 0, cell=0, skip=back)
        box(g, x0 + out * (ft + .06), x0 + out * (ft + .1), sy0 - .05, sy0, z0 + .45, z1 - .45, F['trimz'], T, 0, cell=0, skip=back)
        box(g, x0 + out * (ft + .06), x0 + out * (ft + .1), sy1, sy1 + .05, z0 + .45, z1 - .45, F['trimz'], T, 0, cell=0, skip=back)
        if name and T.fine:
            # the business's name in raised dark letters on the board, read from the street
            cell = min((sy1 - sy0) * .62 / 6, (z1 - z0 - 1.4) / (6 * len(name) - 2))
            width = (6 * len(name) - 2) * cell
            right = (0.0, 0.0, -float(side))
            zc = (z0 + z1) / 2
            origin = (x0 + out * (ft + .085), (sy0 + sy1) / 2 - 3 * cell, zc + side * width / 2)
            rk.lettering(g, name, origin, right, (0.0, 1.0, 0.0), cell, F['door'], max(.016, cell * .22), 4 if T.fine else 3)


def boardwalk(g, side, z0, z1, T, roof=True, balcony=False, posts=3.0):
    """The raised plank walk in front of a run of storefronts, with its posts, its awning (or a balcony) and steps at the ends."""
    xa, xb = side * STREET, side * FRONT
    lo, hi = min(xa, xb), max(xa, xb)
    box(g, lo, hi, GROUND, GROUND + .42, z0, z1, F['deck'], T, .02, cell=1.6 if T.fine else 0)
    if T.detail:
        # the plank joints: thin dark strips across
        z = z0 + .44
        y = GROUND + .423
        while z < z1 - .1:
            g.face(((lo + .02, y, z), (hi - .02, y, z), (hi - .02, y, z + .02), (lo + .02, y, z + .02)), F['trimx'], (0, 1, 0))
            z += .44
    # the step down at each end
    for zz, dz in ((z0, -.4), (z1, .4)):
        za, zb = sorted((zz, zz + dz))
        box(g, lo + .2, hi - .2, GROUND, GROUND + .21, za, zb, F['deck'], T, .02, cell=0)
    if not roof:
        return
    xp = side * (STREET + .2)
    n = max(1, int(round((z1 - z0) / posts)))
    for k in range(n + 1):
        zp = z0 + .15 + (z1 - z0 - .3) * k / n
        box(g, xp - .08, xp + .08, GROUND + .42, 3.0, zp - .08, zp + .08, F['timber'], T, 0, cell=0, skip=('-y',))
        if T.detail:
            # knee braces
            beam(g, (xp, 2.45, zp), (xp, 2.95, zp + (.45 if k < n else -.45)), .07, .07, F['timber'], T, bevel=0)
    if balcony:
        box(g, lo, hi, 3.0, 3.18, z0 - .1, z1 + .1, F['deck'], T, .02, cell=0)
        # the railing: a top rail and balusters
        box(g, xp - .05, xp + .05, 4.05, 4.12, z0, z1, F['trimz'], T, .01, cell=0)
        box(g, xp - .04, xp + .04, 3.25, 3.3, z0, z1, F['trimz'], T, 0, cell=0)
        step = .32 if T.fine else .64
        z = z0 + .1
        while z < z1:
            box(g, xp - .025, xp + .025, 3.18, 4.05, z - .025, z + .025, F['trim'], T, 0, cell=0)
            z += step
        for k in range(n + 1):
            zp = z0 + .15 + (z1 - z0 - .3) * k / n
            box(g, xp - .07, xp + .07, 3.18, 4.12, zp - .07, zp + .07, F['timber'], T, 0, cell=0)
    else:
        # the awning: a sloped plank roof from the storefront to the posts, and its fascia
        y_in, y_out = 3.28, 3.0
        pts = [(xb, y_in, z0 - .1), (xb, y_in, z1 + .1), (xp - side * .25, y_out, z1 + .1), (xp - side * .25, y_out, z0 - .1)]
        top = Geo()
        nrm = sk._norm((-side * (y_in - y_out), abs(xb - xp) + .25, 0))
        top.face(pts, F['shingle'], nrm)
        g.extend(top)
        under = [(p[0], p[1] - .05, p[2]) for p in pts]
        g.face(under[::-1], F['timberz'], (-nrm[0], -nrm[1], 0))
        box(g, xp - side * .25 - .03, xp - side * .25 + .03, y_out - .22, y_out + .02, z0 - .1, z1 + .1, F['trimz'], T, .01, cell=0)


def neon_sign(g, side, zc, y, T):
    """The saloon's neon: SALOON in pink tubes on a dark board, a blue star either side."""
    x = side * (FRONT - .32)
    out = -side
    box(g, x, x + out * .06, y - .2, y + .95, zc - 2.3, zc + 2.3, F['door'], T, .02, cell=0)
    right = (0, 0, -side)  # reading left to right from the street
    cell = .11
    width = (6 * 6 - 2) * cell
    origin = (x + out * .1, y + .05, zc + side * width / 2)
    rk.lettering(g, 'SALOON', origin, right, (0, 1, 0), cell, F['neonpink'], .022 if T.fine else .03, 5 if T.fine else 3)
    for s in (-1, 1):
        c = (x + out * .1, y + .38, zc + s * 2.0)
        pts = []
        for k in range(11):
            a = math.pi / 2 + k * 2 * math.pi / 10
            r = .2 if k % 2 == 0 else .09
            pts.append((c[0], c[1] + math.sin(a) * r, c[2] + math.cos(a) * r))
        sweep(g, sk.circle_section(.016, 4), pts, F['neonblue'], caps=True)


def barrel(g, at, T, lying=False):
    r, h = .3, .86
    prof = [(r * .86, 0), (r, h * .3), (r * 1.02, h * .5), (r, h * .7), (r * .86, h)]
    q = (0, 0, 0, 1) if not lying else quat_axis(1, 0, 0, math.pi / 2)
    t = Geo()
    lathe(t, prof, (0, 0, 0), F['timber'], T.round, cap0=True, cap1=True)
    for hh in ((.12, h - .12) if T.detail else ()):
        rr = r * .92 + .015
        lathe(t, [(rr, hh - .03), (rr, hh + .03)], (0, 0, 0), F['iron'], T.round)
    m = affine(q, at)
    g.extend(t.transformed(m))


def crate(g, at, s, yaw_, T):
    t = Geo()
    box(t, -s / 2, s / 2, 0, s, -s / 2, s / 2, F['timberx'], T, .02)
    if T.detail:
        for sgn in (-1, 1):
            rk.beam(t, (-s / 2 + .04, .04, sgn * (s / 2 + .01)), (s / 2 - .04, s - .04, sgn * (s / 2 + .01)), .06, .02, F['timber'], T)
    g.extend(t.transformed(rk.yaw(yaw_, at)))


def wheel(g, centre, axis_yaw, r, T, spokes=10):
    """A wagon wheel standing upright: rim, hub and spokes; its axle along x turned by axis_yaw."""
    t = Geo()
    n = T.arc * 2 if T.fine else 12
    rim = [(0, math.sin(2 * math.pi * k / n) * r, math.cos(2 * math.pi * k / n) * r) for k in range(n)]
    sweep(t, sk.rect_section(.07, .05), rim, F['timberx'], closed=True, caps=False)
    cylinder(t, (-.09, 0, 0), (.09, 0, 0), .07, F['timberx'], T.small)
    for k in range(spokes if T.fine else spokes // 2):
        a = 2 * math.pi * k / (spokes if T.fine else spokes // 2)
        beam(t, (0, math.sin(a) * .06, math.cos(a) * .06), (0, math.sin(a) * (r - .03), math.cos(a) * (r - .03)), .03, .03, F['timber'], T, bevel=0)
    g.extend(t.transformed(rk.yaw(axis_yaw, centre)))


def saguaro(g, at, h, seed, T):
    rng = np.random.default_rng(seed)
    r = .26
    n = 9 if T.fine else 5
    prof = [(r * .9, 0), (r, .3), (r, h - .4), (r * .82, h - .12), (r * .4, h - .02), (.02, h)]
    if not T.detail:
        prof = [(r * .9, 0), (r, h - .3), (.05, h)]
    lathe(g, [(a, b) for a, b in prof], at, F['cactus'], n, cap0=False)
    for arm in range((int(rng.integers(1, 3)) + 1) if T.detail else 1):
        a = rng.uniform(0, 2 * math.pi)
        y0 = h * rng.uniform(.35, .55)
        out = rng.uniform(.55, .8)
        up = rng.uniform(.9, 1.5)
        d = (math.cos(a), 0, math.sin(a))
        pts = [(at[0] + d[0] * r * .8, at[1] + y0, at[2] + d[2] * r * .8)]
        for k in range(1, 4):
            u = k / 3
            ox = out * math.sin(u * math.pi / 2)
            oy = up * (1 - math.cos(u * math.pi / 2)) * 1.15
            pts.append((at[0] + d[0] * (r * .8 + ox), at[1] + y0 + oy, at[2] + d[2] * (r * .8 + ox)))
        pts.append((pts[-1][0], pts[-1][1] + .25, pts[-1][2]))
        sweep(g, sk.circle_section(r * .62, n), pts, F['cactus'], caps=True)


def telegraph(g, at, T):
    box(g, at[0] - .1, at[0] + .1, GROUND, 6.4, at[2] - .1, at[2] + .1, F['grey'], T, .02, cell=0)
    for y, w in ((6.0, .9), (5.4, .7)):
        box(g, at[0] - w, at[0] + w, y, y + .1, at[2] - .06, at[2] + .06, F['grey'], T, 0, cell=0)
        if T.detail:
            for sx in (-w + .12, w - .12):
                cylinder(g, (at[0] + sx, y + .1, at[2]), (at[0] + sx, y + .22, at[2]), .035, F['glass'], 4)


def wire(g, a, b, sag, T):
    pts = []
    n = 10 if T.fine else 5
    for k in range(n + 1):
        u = k / n
        p = sk._lerp(a, b, u)
        pts.append((p[0], p[1] - sag * 4 * u * (1 - u), p[2]))
    sweep(g, sk.circle_section(.012, 3), pts, F['iron'], caps=False)


def water_tower(g, at, T):
    x, z = at[0], at[2]
    legs_h = 5.6
    for sx in (-1.5, 1.5):
        for sz in (-1.5, 1.5):
            beam(g, (x + sx * 1.15, GROUND, z + sz * 1.15), (x + sx, legs_h, z + sz), .22, .22, F['timber'], T)
    for y in (1.6, 3.6):
        for (a, b) in (((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))):
            beam(g, (x + a[0] * 1.45, y, z + a[1] * 1.45), (x + b[0] * 1.45, y, z + b[1] * 1.45), .12, .12, F['timber'], T)
    if T.detail:
        for (a, b) in (((-1, -1), (1, -1)), ((1, 1), (-1, 1))):
            beam(g, (x + a[0] * 1.45, 1.6, z + a[1] * 1.45), (x + b[0] * 1.4, 3.6, z + b[1] * 1.4), .08, .08, F['timber'], T)
    box(g, x - 1.9, x + 1.9, legs_h, legs_h + .2, z - 1.9, z + 1.9, F['deck'], T, .02)
    r = 1.75
    lathe(g, [(r, legs_h + .2), (r * 1.02, legs_h + 1.7), (r, legs_h + 3.2)], (x, 0, z), F['siding1'], T.round * 2 if T.fine else 8, cap0=False)
    for hh in (.6, 1.6, 2.6):
        lathe(g, [(r * 1.035, legs_h + .2 + hh - .04), (r * 1.035, legs_h + .2 + hh + .04)], (x, 0, z), F['iron'], T.round * 2 if T.fine else 8)
    lathe(g, [(r + .15, legs_h + 3.2), (.15, legs_h + 4.3), (0.01, legs_h + 4.35)], (x, 0, z), F['shingle'], T.round * 2 if T.fine else 8, cap0=True)


def windmill_tower(g, at, T):
    x, z = at[0], at[2]
    top = 9.2
    for sx in (-1, 1):
        for sz in (-1, 1):
            beam(g, (x + sx * 1.2, GROUND, z + sz * 1.2), (x + sx * .3, top, z + sz * .3), .1, .1, F['grey'], T)
    if T.detail:
        for y0, y1 in ((0.5, 3.0), (3.0, 5.5), (5.5, 8.0)):
            f0, f1 = 1.2 - .9 * y0 / top, 1.2 - .9 * y1 / top
            for (a, b) in (((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))):
                beam(g, (x + a[0] * f0, y0, z + a[1] * f0), (x + b[0] * f1, y1, z + b[1] * f1), .05, .05, F['grey'], T)
    box(g, x - .45, x + .45, top, top + .1, z - .45, z + .45, F['deck'], T, .01)
    # the head and its tail vane (the fan is the blades prop)
    box(g, x - .16, x + .16, top + .1, top + .45, z - .5, z + .35, F['iron'], T, .02)
    beam(g, (x, top + .3, z - .5), (x, top + .35, z - 2.3), .06, .06, F['iron'], T)
    rk.prism(g, [(0, 0), (1.3, 0), (1.3, .9), (.2, .4)], (x - .02, top + .1, z - 2.3), (0, 0, -1), (0, 1, 0), .04, F['tin'])


def blades(T):
    g = Geo()
    n = 16 if T.fine else 10
    for k in range(n):
        a = 2 * math.pi * k / n
        c, s = math.cos(a), math.sin(a)
        # each vane a slightly twisted plate from .35 to 1.6 m
        p = [(c * .35 - s * .06, s * .35 + c * .06, 0), (c * 1.6 - s * .16, s * 1.6 + c * .16, -.05), (c * 1.6 + s * .16, s * 1.6 - c * .16, .05), (c * .35 + s * .06, s * .35 - c * .06, 0)]
        g.face(p, F['tin'], (0, 0, 1))
        g.face(p[::-1], F['tin'], (0, 0, -1))
    lathe(g, [(.18, -.12), (.18, .1), (.05, .2)], (0, 0, 0), F['iron'], T.small, axis=(0, 0, 1), cap0=True, cap1=True)
    sweep(g, sk.rect_section(.04, .03), [(math.cos(2 * math.pi * k / 24) * 1.2, math.sin(2 * math.pi * k / 24) * 1.2, 0) for k in range(24)], F['iron'], closed=True, caps=False)
    return g


def graveyard(g, at, T, rng):
    for k in range(6):
        x = at[0] + (k % 3) * 1.6 - 1.6 + rng.uniform(-.25, .25)
        z = at[2] + (k // 3) * 1.8 + rng.uniform(-.2, .2)
        lean = rng.uniform(-.12, .12)
        t = Geo()
        box(t, -.05, .05, 0, 1.05, -.04, .04, F['grey'], T, 0, cell=0, skip=('-y',))
        box(t, -.32, .32, .7, .8, -.04, .04, F['grey'], T, 0, cell=0)
        q = quat_axis(0, 0, 1, lean)
        q2 = quat_axis(0, 1, 0, rng.uniform(-.3, .3))
        m = np.array(affine(q2, (x, GROUND - .05, z))).reshape(3, 4)
        m2 = np.array(affine(q)).reshape(3, 4)
        R = m[:, :3] @ m2[:, :3]
        g.extend(t.transformed(np.concatenate([R, m[:, 3:]], axis=1).ravel().tolist()))
        if T.detail:
            rk.rock(g, (x, GROUND, z + .5), .28, F['sand'], 40 + k, squash=(1.0, .25, .6), detail=1)


def fence(g, pts, T):
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        box(g, a[0] - .07, a[0] + .07, GROUND, 1.15, a[2] - .07, a[2] + .07, F['grey'], T, 0, cell=0, skip=('-y',))
        for y in (.45, .9):
            beam(g, (a[0], y, a[2]), (b[0], y + .03, b[2]), .07, .05, F['grey'], T, bevel=0)
    b = pts[-1]
    box(g, b[0] - .07, b[0] + .07, GROUND, 1.15, b[2] - .07, b[2] + .07, F['grey'], T, 0, cell=0, skip=('-y',))


def belfry_frame(g, T):
    """The belfry's stone plinth, its four leaning posts, girts and braces (what stays standing when the top goes)."""
    bx, _, bz = BELFRY
    # the plinth: rubble stone, a little battered
    box(g, bx - 1.5, bx + 1.5, GROUND - .1, PLINTH, bz - 1.5, bz + 1.5, F['stone'], T, .06)
    box(g, bx - 1.62, bx + 1.62, GROUND - .1, .18, bz - 1.62, bz + 1.62, F['stone'], T, .05)
    # steps up the plinth's front (toward the street)
    for k in range(3):
        box(g, bx - .6, bx + .6, GROUND - .05, .18 + k * .24, bz - 1.62 - .36 * (3 - k), bz - 1.62 - .36 * (2 - k), F['stone'], T, .03)
    base, top = 1.12, .92

    def post(sx, sz, y):
        f = base + (top - base) * (y - PLINTH) / (POST_TOP - PLINTH)
        return (bx + sx * f, y, bz + sz * f)
    for sx in (-1, 1):
        for sz in (-1, 1):
            beam(g, post(sx, sz, PLINTH), post(sx, sz, POST_TOP), .24, .24, F['timber'], T)
    # the girts and the cross braces; the front (toward the street, -z) stands open below its top girt, so the bell falls clear
    for y in (PLINTH + .25, 3.85, POST_TOP - .12):
        for (a, b) in (((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))):
            if a[1] == b[1] == -1 and y == 3.85:
                continue
            beam(g, post(a[0], a[1], y), post(b[0], b[1], y), .16, .16, F['timberx'] if a[1] == b[1] else F['timberz'], T)
    for (y0, y1) in ((PLINTH + .25, 3.85), (3.85, POST_TOP - .12)):
        for (a, b) in (((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))):
            beam(g, post(a[0], a[1], y0), post(b[0], b[1], y1), .1, .1, F['timber'], T)
            if T.detail:
                beam(g, post(b[0], b[1], y0), post(a[0], a[1], y1), .1, .1, F['timber'], T)
    # the bell rope hanging down to a cleat on a post
    if T.detail:
        sweep(g, sk.circle_section(.018, 4), [(bx + .1, HANG - .2, bz), (bx + .35, 4.2, bz - .2), (bx + .78, 1.9, bz - .78)], F['rope'], caps=True)


def headstock(T):
    """The beam the bell hangs from, whole: along x, its underside at y 0, its middle at x 0."""
    g = Geo()
    box(g, -1.15, 1.15, 0, .3, -.15, .15, F['timberx'], T, .03)
    for s in (-1, 1):
        box(g, s * .95 - .08, s * .95 + .08, -.06, .36, -.17, .17, F['iron'], T, .01, cell=0)
    return g


def beam_half(T):
    """One half of the snapped headstock: from its outer end (origin) inward 1.15 m along +x, the inner end splintered."""
    g = Geo()
    box(g, 0, 1.0, 0, .3, -.15, .15, F['timberx'], T, .03)
    # the jagged break: a few splinters standing out of the end
    rng = np.random.default_rng(7)
    for k in range(5 if T.fine else 2):
        y = rng.uniform(.04, .26)
        z = rng.uniform(-.11, .11)
        L = rng.uniform(.12, .32)
        beam(g, (1.0, y, z), (1.0 + L, y + rng.uniform(-.04, .04), z + rng.uniform(-.03, .03)), .04, .04, F['timberx'], T, bevel=0)
    return g


def bell(T):
    """The bell: hung from its crown at the origin, mouth down at y = -BELL_H; its yoke and clapper."""
    g = Geo()
    H, R = BELL_H, BELL_R
    prof = [(R * 1.0, -H), (R * .98, -H + .05), (R * .9, -H + .12), (R * .74, -H + .32), (R * .62, -H + .6), (R * .56, -H + .82), (R * .52, -H + .96), (R * .36, -H + 1.03), (R * .08, -H + 1.06)]
    n = T.round * 2 if T.fine else 10
    lathe(g, prof, (0, 0, 0), F['bronze'], n, cap1=True)
    # the lip's inside, so it reads hollow from below
    lathe(g, [(R * .9, -H + .04), (R * .66, -H + .45), (R * .45, -H + .8), (R * .1, -H + .95)], (0, 0, 0), F['bronzedark'], n, cap1=True)
    # raised bands
    for hh, rr in ((-H + .2, .83), (-H + .85, .55)):
        lathe(g, [(R * rr + .012, hh - .02), (R * rr + .02, hh), (R * rr + .012, hh + .02)], (0, 0, 0), F['bronze'], n)
    # the crown's loops and the yoke
    box(g, -.16, .16, -.06, 0, -.06, .06, F['iron'], T, .01, cell=0)
    if T.detail:
        cylinder(g, (0, -.95, 0), (0, -.25, 0), .025, F['iron'], 6)
        lathe(g, [(.01, -.98), (.07, -.94), (.08, -.88), (.06, -.84), (.01, -.82)], (0, 0, 0), F['iron'], 8)
    return g


def roof(T):
    """The belfry's pyramid roof from its eaves (origin, y 0) to the apex, with a weather vane."""
    g = Geo()
    e = 1.55
    apex = (0, 1.45, 0)
    corners = [(-e, 0, -e), (e, 0, -e), (e, 0, e), (-e, 0, e)]
    for i in range(4):
        a, b = corners[i], corners[(i + 1) % 4]
        cen = ((a[0] + b[0] + apex[0]) / 3, (a[1] + b[1] + apex[1]) / 3, (a[2] + b[2] + apex[2]) / 3)
        g.face((a, b, apex), F['shingle'], sk._norm((cen[0], .9, cen[2])))
        # the eaves' fascia
        beam(g, (a[0], -.06, a[2]), (b[0], -.06, b[2]), .12, .14, F['trim'], T)
    under = [(c[0], -.02, c[2]) for c in corners]
    g.face(under, F['timberx'], (0, -1, 0))
    if T.detail:
        # shingle courses: thin steps across each face
        for i in range(4):
            a, b = corners[i], corners[(i + 1) % 4]
            for k in range(1, 5):
                u = k / 5
                p0 = sk._lerp(a, apex, u)
                p1 = sk._lerp(b, apex, u)
                beam(g, p0, p1, .03, .02, F['shingle'], T, bevel=0)
    cylinder(g, (0, 1.4, 0), (0, 2.1, 0), .025, F['iron'], 5)
    rk.prism(g, [(-.35, 0), (.25, 0), (.35, .05), (.25, .1), (-.35, .1), (-.25, .05)], (0, 1.95, -.02), (1, 0, 0), (0, 1, 0), .04, F['iron'])
    return g


def shingle(T):
    g = Geo()
    box(g, -.16, .16, -.012, .012, -.1, .1, F['shingle'], T, .006, cell=0)
    return g


def plank(T):
    g = Geo()
    box(g, -.5, .5, -.025, .025, -.07, .07, F['timberx'], T, .01, cell=0)
    if T.fine:
        beam(g, (.5, 0, -.03), (.68, .01, -.02), .03, .03, F['timberx'], T, bevel=0)
        beam(g, (-.5, 0, .03), (-.62, -.01, .04), .03, .03, F['timberx'], T, bevel=0)
    return g


def splinter(T):
    g = Geo()
    beam(g, (-.2, 0, 0), (.2, .01, 0), .025, .02, F['timberx'], T, bevel=0)
    return g


def tumbleweed(T):
    g = Geo()
    rng = np.random.default_rng(11)
    n = 9 if T.fine else 5
    for k in range(n):
        ax = rng.normal(size=3)
        ax /= np.linalg.norm(ax)
        ref = np.array([0, 1, 0]) if abs(ax[1]) < .9 else np.array([1, 0, 0])
        u = np.cross(ax, ref)
        u /= np.linalg.norm(u)
        v = np.cross(ax, u)
        r = .36 * rng.uniform(.75, 1.0)
        a0 = rng.uniform(0, 2 * math.pi)
        span = rng.uniform(2.2, 4.4)
        pts = []
        m = 9 if T.fine else 5
        for j in range(m + 1):
            a = a0 + span * j / m
            rr = r * (1 + .12 * math.sin(a * 3 + k))
            p = (u * math.cos(a) + v * math.sin(a)) * rr
            pts.append(tuple(p))
        sweep(g, sk.circle_section(.011, 3), pts, F['weed'], caps=False)
    return g


def batwing(T):
    """A saloon swing door: hinge at the origin, the leaf across +x (.55 m), from .55 to 1.65 m up."""
    g = Geo()
    box(g, 0, .55, .55, 1.65, -.025, .025, F['paintx1'], T, .01, cell=0)
    if T.detail:
        for k in range(4):
            y = .75 + k * .24
            box(g, .06, .49, y, y + .12, -.03, .03, F['trimx'], T, 0, cell=0)
    return g


def sky(T):
    g = Geo()
    # (inside the game's 200 m far plane; the intro draws it round the camera's eye, so it never parallaxes)
    rk.dome(g, 180.0, F['sky'], 14 if T.fine else 8, 40 if T.fine else 20)
    return g


SUN = np.array([0.0, math.sin(math.radians(2.2)), math.cos(math.radians(2.2))])


def paint_sky(g, bk):
    def fn(P):
        d = P / np.linalg.norm(P, axis=1, keepdims=True)
        e = d[:, 1]
        s = d @ SUN
        near, mid, opp = rk.hexrgb(0xFFC879), rk.hexrgb(0xF2884A), rk.hexrgb(0x8C5A7A)
        zen, deep, haze = rk.hexrgb(0x3A3A72), rk.hexrgb(0x232655), rk.hexrgb(0xB87A5E)
        w = np.clip((s + 1) / 2, 0, 1) ** 2.2
        hor = rk.lerp3(rk.lerp3(np.tile(opp, (len(P), 1)), np.tile(mid, (len(P), 1)), np.clip(w * 1.6, 0, 1)), np.tile(near, (len(P), 1)), np.clip(w * 1.6 - .6, 0, 1))
        up = np.clip(e / .55, 0, 1) ** .8
        top = rk.lerp3(np.tile(zen, (len(P), 1)), np.tile(deep, (len(P), 1)), np.clip(e * 1.4 - .4, 0, 1))
        c = rk.lerp3(hor, top, up)
        halo = np.exp(-(1 - np.clip(s, -1, 1)) * 18) * (1 - np.clip(e * 3, 0, 1))
        c = c + halo[:, None] * (rk.hexrgb(0xFFE6A8) - c) * .85
        c = np.where((e < 0)[:, None], rk.lerp3(c, np.tile(haze, (len(P), 1)), np.clip(-e * 12, 0, 1)), c)
        return c
    rk.paint(g, bk, fn, F['sky'])


def mesas(g, T, rng):
    """Flat-topped buttes far out past the belfry and round the town (seen against the low sun and in the hero frame)."""
    places = [(-120, 270, 70, 28), (100, 320, 80, 34), (190, 360, 60, 26), (-210, 340, 80, 32), (230, 230, 70, 22),
              (-240, 160, 90, 30), (260, 80, 80, 24), (-260, -90, 90, 28), (240, -160, 100, 34), (0, -320, 140, 38), (-120, -260, 70, 30)]
    for (x, z, w, h) in places:
        # brought inside the far plane at the same bearing and angular size (the intro draws them round the eye's ground point)
        d = math.hypot(x, z)
        k = 160.0 / d
        x, z, w, h = x * k, z * k, w * k, h * k
        n = 9 if T.fine else 6
        base, topp = [], []
        for k in range(n):
            a = 2 * math.pi * k / n + rng.uniform(-.2, .2)
            r = w * rng.uniform(.7, 1.0)
            base.append((x + math.cos(a) * r, z + math.sin(a) * r * .6))
            topp.append((x + math.cos(a) * r * .7, z + math.sin(a) * r * .6 * .7))
        ytop = h * rng.uniform(.85, 1.1)
        ycliff = ytop * .55
        mid = [(x + (bx - x) * .82, z + (bz - z) * .82) for bx, bz in base]
        rings = [(base, -2.0), (mid, ycliff), (topp, ytop)]
        for i in range(2):
            ra, ya = rings[i]
            rb, yb = rings[i + 1]
            for k in range(n):
                k1 = (k + 1) % n
                pts = ((ra[k][0], ya, ra[k][1]), (ra[k1][0], ya, ra[k1][1]), (rb[k1][0], yb, rb[k1][1]), (rb[k][0], yb, rb[k][1]))
                cen = ((pts[0][0] + pts[2][0]) / 2 - x, 0, (pts[0][2] + pts[2][2]) / 2 - z)
                g.face(pts, F['mesa'] if i == 0 else F['mesadark'], sk._norm((cen[0], (yb - ya) * .3 if i == 0 else .15, cen[2])))
        g.face([(p[0], ytop, p[1]) for p in topp], F['mesadark'], (0, 1, 0))


def town(T):
    rng = np.random.default_rng(1964)
    g = Geo()

    def street_height(x, z):
        h = 0.0
        for rx in (-1.3, 1.3, -3.6, 3.6):
            h -= .035 * math.exp(-((x - rx) / .35) ** 2)
        return h
    # the street (fine cells for the occlusion under the walks), the ground under and between the buildings
    rk.ground(g, -STREET, STREET, -53.0, 8.0, GROUND, F['dirt'], 2.5 if T.fine else 6.0, street_height)
    for s in (-1, 1):
        x0, x1 = sorted((s * STREET, s * 40.0))
        rk.ground(g, x0, x1, -60.0, 8.0, GROUND, F['dirt'], 8.0 if T.fine else 16.0)

    def desert(x, z):
        d = math.hypot(x - BELFRY[0], z - BELFRY[2])
        h = .35 * math.sin(x * .07 + 1) * math.sin(z * .05) * min(1, max(0, (abs(x) - 6) / 10)) * min(1, max(0, (z - 10) / 10))
        return h + .04 * math.sin(x * .9) * math.sin(z * .7) - .1 * max(0, 1 - d / 4.0) * 0
    rk.ground(g, -40.0, 40.0, 8.0, 64.0, GROUND, F['sand'], 4.0 if T.fine else 10.0, desert)
    # left side (x < 0): from the far end to the town's edge; the saloon nearest
    left = [(-46, -37.5, 4.6, 6.2, 'step', 'siding0', 'paint3', True, False, 'ASSAY'), (-37, -28.5, 4.4, 5.6, 'flat', 'siding1', 'paint0', True, False, 'GENERAL STORE'),
            (-28, -20, 4.8, 6.6, 'round', 'siding2', 'paint1', False, True, 'BARBER'), (-19.5, -13.5, 4.2, 5.4, 'peak', 'siding3', 'paint2', True, False, 'GUNSMITH'),
            (-13, -1.2, 6.4, 8.0, 'step', 'siding1', 'paint4', True, True, None), (-0.6, 6.0, 4.4, 5.9, 'flat', 'siding0', 'paint3', False, False, 'BANK')]
    right = [(-46, -38.5, 4.4, 5.6, 'flat', 'siding2', 'paint2', False, False, 'FEED'), (-38, -29, 5.0, 6.8, 'round', 'siding0', 'paint1', True, True, 'THEATRE'),
             (-28.5, -21.5, 4.4, 5.8, 'step', 'siding3', 'paint0', True, False, 'SHERIFF'), (-21, -12.5, 4.6, 6.0, 'flat', 'siding1', 'paint3', False, False, 'TELEGRAPH'),
             (-12, -5.5, 4.2, 5.2, 'peak', 'siding2', 'paint4', True, False, 'DOCTOR'), (-3.2, 6.0, 6.2, 7.6, 'flat', 'siding0', 'paint0', True, True, 'LAND OFFICE')]
    for side, lots in ((-1, left), (1, right)):
        for (z0, z1, h, fh, top, siding, paint, lit, upper, name) in lots:
            storefront(g, side, z0, z1, h, fh, top, siding, paint, T, rng, lit=(lit, upper), upper=upper or fh > 7, door_z=SALOON_DOOR if (side < 0 and z0 == -13) else None, name=name)
        zs = [l[0] for l in lots] + [l[1] for l in lots]
        # the boardwalk runs (gaps at the alleys)
        runs = []
        for l in lots:
            if runs and l[0] - runs[-1][1] < 1.0:
                runs[-1][1] = l[1]
            else:
                runs.append([l[0], l[1]])
        for (z0, z1) in runs:
            if side < 0:
                # the saloon's balcony over its walk, the awnings elsewhere
                boardwalk(g, side, z0, -13.0, T)
                boardwalk(g, side, -13.0, -1.2, T, balcony=True)
                boardwalk(g, side, -1.2, z1, T)
            else:
                boardwalk(g, side, z0, z1, T)
    # the saloon's neon and its swing doors' frame
    neon_sign(g, -1, -7.1, 4.55, T)
    # the hotel closing the street's far end: a two-storey storefront with its balcony walk, turned to face up the street
    hotel = Geo()
    storefront(hotel, 1, -12.0, 12.0, 7.0, 9.0, 'step', 'siding1', 'paint3', T, rng, lit=(True, True), upper=True, name='GRAND HOTEL')
    boardwalk(hotel, 1, -12.0, 12.0, T, balcony=True, posts=3.0)
    g.extend(hotel.transformed(rk.yaw(math.pi / 2, (0.0, 0.0, -42.5))))
    # props along the walks: barrels, crates, a trough, hitching rails, lanterns
    for (x, z) in ((-7.0, -25.0), (-7.1, -24.3), (7.2, -17.0), (-7.2, 3.6), (7.0, -33.0), (7.15, 1.2)):
        barrel(g, (x, GROUND, z), T)
    barrel(g, (6.6, GROUND + .3, -16.2), T, lying=True)
    for (x, z, s, a) in ((7.1, -9.0, .8, .2), (7.0, -8.3, .6, -.3), (7.15, -8.55, .5, .9), (-7.1, -31.0, .9, .1)):
        crate(g, (x, GROUND if s > .55 else GROUND + .8, z), s, a, T)
    box(g, 5.8, 6.4, GROUND, .55, -26.0, -23.5, F['timberz'], T, .03, cell=0)
    for (x, z0, z1) in ((-6.3, -20.0, -16.5), (6.3, -4.5, -1.0), (-6.3, -40.0, -36.0)):
        for z in (z0, z1):
            box(g, x - .06, x + .06, GROUND, 1.05, z - .06, z + .06, F['grey'], T, 0, cell=0, skip=('-y',))
        box(g, x - .05, x + .05, .95, 1.03, z0, z1, F['grey'], T, 0, cell=0)
    if T.detail:
        for (x, z) in ((-7.8, -6.0), (-7.8, 2.5), (7.8, -10.0), (7.8, 3.5), (-7.8, -22.0), (7.8, -26.0)):
            box(g, x - .07, x + .07, 2.3, 2.62, z - .07, z + .07, F['lamp'], T, 0, cell=0)
            box(g, x - .09, x + .09, 2.62, 2.68, z - .09, z + .09, F['iron'], T, 0, cell=0)
    # a cart by the walk
    box(g, 4.8, 6.2, .55, .95, -13.6, -10.4, F['timberz'], T, .03, cell=0)
    for z in (-12.0,):
        for x in (4.7, 6.3):
            wheel(g, (x, .62, z), 0.0, .6, T)
    beam(g, (5.5, .7, -13.6), (5.5, .1, -16.0), .07, .07, F['timber'], T)
    # the telegraph line down the right side and out into the desert
    poles = [(7.25, GROUND, z) for z in (-42.0, -28.0, -14.0, 0.0)] + [(11.0, GROUND, 14.0), (13.5, GROUND, 30.0), (16.0, GROUND, 47.0)]
    for p in poles:
        telegraph(g, p, T)
    for i in range(len(poles) - 1 if T.detail else 0):
        for dx in (-.78, .78):
            wire(g, (poles[i][0] + dx, 6.22, poles[i][2]), (poles[i + 1][0] + dx, 6.22, poles[i + 1][2]), .5, T)
    water_tower(g, (21.0, 0, -14.0), T)
    windmill_tower(g, WINDMILL, T)
    belfry_frame(g, T)
    graveyard(g, (-9.0, 0, 13.5), T, rng)
    fence(g, [(-13.5, 0, 11.0), (-11.0, 0, 11.0), (-8.5, 0, 11.2), (-6.0, 0, 11.0), (-5.0, 0, 12.5)], T)
    fence(g, [(-13.5, 0, 11.0), (-13.5, 0, 14.5), (-13.3, 0, 17.5)], T)
    for (x, z, h, s) in ((7.5, 16.0, 4.2, 3), (12.0, 22.0, 3.4, 5), (-16.0, 20.0, 4.6, 8), (3.5, 26.0, 3.0, 9), (-4.5, 31.0, 4.0, 12), (18.0, 12.0, 3.2, 14)):
        saguaro(g, (x, GROUND, z), h, s, T)
    for k in range(14 if T.fine else 6):
        x = rng.uniform(-30, 30)
        z = rng.uniform(12, 60)
        if abs(x - BELFRY[0]) < 3 and abs(z - BELFRY[2]) < 3 or abs(x) < 5 and z < 28:
            continue
        rk.rock(g, (x, GROUND, z), rng.uniform(.3, 1.2), F['rock'], 100 + k, squash=(1.0, rng.uniform(.4, .8), 1.0), detail=1)
    return g


WINDMILL = (-18.5, 0.0, 22.0)
WINDMILL_HUB = (WINDMILL[0], 9.2 + .28, WINDMILL[2] + .42)


def far(T):
    rng = np.random.default_rng(7)
    g = Geo()
    # the desert floor out to the horizon: a whole sheet under the near ground (drawn round the eye, so no seam can open)
    R = 180.0
    rk.ground(g, -R, R, -R, R, GROUND - .08, F['sandfar'], 60.0 if T.fine else 120.0)
    mesas(g, T, rng)
    return g


def build(out, T):
    t = out
    bx, _, bz = BELFRY
    t.add('sundown/town', town(T), T.i, floors=(GROUND, .42, 3.18), ao_distance=1.2, ao_samples=16 if T.i == 0 else 8, show=True)
    t.add('sundown/far', far(T), T.i, ao=False, show=True)
    t.add('sundown/sky', sky(T), T.i, ao=False, paint_fn=paint_sky, show=True)
    t.add('sundown/bell', bell(T), T.i, ao_distance=.3, show=rk.yaw(0, (bx, HANG, bz)))
    t.add('sundown/headstock', headstock(T), T.i, ao_distance=.2, show=rk.yaw(0, (bx, HANG, bz)))
    t.add('sundown/beam-half', beam_half(T), T.i, ao=False)
    t.add('sundown/roof', roof(T), T.i, ao_distance=.4, show=rk.yaw(0, (bx, POST_TOP, bz)))
    t.add('sundown/blades', blades(T), T.i, ao=False, show=rk.yaw(0, WINDMILL_HUB))
    t.add('sundown/batwing', batwing(T), T.i, ao=False, show=rk.yaw(math.pi / 2, (-(FRONT - FT) + .04, GROUND + .42, SALOON_DOOR + .56)))
    t.add('sundown/tumbleweed', tumbleweed(T), T.i, ao=False, show=rk.yaw(0, (1.2, .36, 3.0)))
    for name, fn in (('shingle', shingle), ('plank', plank), ('splinter', splinter)):
        t.add('sundown/' + name, fn(T), T.i, ao=False)


def views():
    b = BELFRY
    return [('sd-behind', (.6, .45, -3.4), (b[0], 3.0, b[2]), 34), ('sd-front', (1.4, .9, 4.0), (0, 1.0, -6.0), 40),
            ('sd-wide', (9.0, 6.0, -14.0), (-1.0, 2.0, 6.0), 55), ('sd-belfry', (2.5, 1.6, 2.0), (b[0], 5.5, b[2]), 34),
            ('sd-street', (0, 1.6, 8.0), (0, 2.5, -40.0), 50), ('sd-hero', (1.6, .55, 3.4), (0, 1.05, 0), 34),
            ('sd-saloon', (4.5, 2.0, -3.0), (-9.0, 4.0, -8.0), 50)]
