"""The menu rooftop's own roof (docs/MENU-ROOFTOP-ART.md): the perch the player's pilot sits on, modelled on the pieces Core
NativeRooftopCity.Perch builds, each where it stands today (the lip the pilot sits on, the plant whose vents steam, the mast
under its beacon, the lamps under their glows, the stair house, the tank, the corner's blade sign) and dressed as a real
roof: a parapet under a moulded coping with the cyan strip let into it, a ballasted membrane with laps, drains and wet
patches, a cable tray and conduits, pipes on sleepers with a valve, vents, crates under a tarp, a tool case and the old TV
set on its crate (Core's `retro` screen: the Ricochet easter egg).

Unity's frame: Core's source x is negated (X()). The roof spans x -1.7 .. 26, z -1.8 .. 16; the camera sits off its x = -1.7,
z = -1.8 corner, so the strip along the front lip out to x ~ 12 is what a 16:9 frame sees (to x 26 at 21:9)."""
import math

import stagekit as sk
import cybercity_kit as ck
import rooftop_kit as rk
import rooftop_city as rc
from rooftop_kit import F, glow, h01, Fr, rod, box, lathe, sweep

# Core's own numbers (NativeRooftopCity): the roof, the lip, the retro screen
LIP, LIPD = .32, .42


def X(x):
    return -x


def build(L, T):
    """Our roof's solid geometry and our tower's facade faces below it, for a tier."""
    g = sk.Geo()
    fc = rk.Facade()
    P = L.perch
    x0, x1, z0, z1 = P['x0'], P['x1'], P['z0'], P['z1']
    fine = T.i == 0
    deck(g, x0, x1, z0, z1, T)
    tower_below(L, g, fc, x0, x1, z0, z1, T)
    lip(g, x0, x1, z0, z1, T)
    hvac(g, T)
    stack(g, T)
    mast(L, g, T)
    stairhouse(g, T)
    for v in L.loose.get('tank', []):
        rc.tank(g, v, rc.Ctx(T, True))
    pipes(g, T)
    crates(g, T)
    lamps(L, g, T)
    bladesign(g, x0, z0, T)
    tv(L, g, T)
    if fine:
        extras(g, x0, x1, z0, z1, T)
    return g, fc


def deck(g, x0, x1, z0, z1, T):
    """The membrane (one sheet at y 0, Core's puddles lie 12 mm over it), its laps, ballast beds and a walkway of pavers."""
    box(g, x0, x1, -.4, 0, z0, z1, F['membrane'], T, 0, cell=(1.0, 0, 3.0)[T.i], skip=('-y', '-x', '-z', '+x', '+z'))
    if T.i == 2:
        return
    # laps across the roof every 1.9 m (thin strips standing a membrane's thickness proud)
    xx = x0 + 1.2
    while xx < x1 - .5:
        box(g, xx, xx + .14, 0, .012, z0 + LIPD + .05, z1 - .2, F['membranelap'], None, 0, cell=0, skip=('-y',))
        xx += 1.9
    # ballast beds along the back and a paver walk from the stair house toward the plant
    box(g, 7.5, 14.4, 0, .05, 2.6, 6.5, F['gravel'], T, .02, cell=1.0, skip=('-y',))
    for k in range(9):
        px = 5.2 + k * 1.05
        box(g, px, px + .95, 0, .045, 1.25, 2.2, F['pad'], None, .012, cell=0, skip=('-y',))
    # a roof drain by the lip: a dished grate
    for (dx, dz) in ((1.15, -1.05), (9.2, -1.1)):
        lathe(g, [(.02, .012), (.16, .016), (.2, .03)], (dx, 0, dz), F['galvdark'], 10)
        ck.disc(g, (dx, .014, dz), .15, F['void'], 10, (0, 1, 0))


def tower_below(L, g, fc, x0, x1, z0, z1, T):
    """Our tower under the roof: its two faces the camera can see (the facade shader's corporate grid), the dark band under
    the lip and a moulded ledge at every storey, with a service run down the side."""
    tw = dict(seed=41.0, style=float(rk.CORPORATE), hue=.12, share=.55, top=0.0)
    # the front (z0, facing -z) and the side (x0, facing -x): counter-clockwise the front runs x0 -> x1, the side z1 -> z0
    u = fc.wall((x0, z1), (x0, z0), L.street, -.4, tw, 0.0)
    fc.wall((x0, z0), (x1, z0), L.street, -.4, tw, u)
    fz = Fr(((x0 + x1) / 2, -.4, z0), (0, 0, -1), (x1 - x0) / 2, .4)
    fx = Fr((x0, -.4, (z0 + z1) / 2), (-1, 0, 0), (z1 - z0) / 2, .4)
    fz.quad(g, -fz.half, fz.half, 0, .4, 0, F['concretedark'])
    fx.quad(g, -fx.half, fx.half, 0, .4, 0, F['concretedark'])
    y = -4.0
    k = 0
    while y > -60:
        near = k < 4
        for fr in (fz, fx):
            # Core's slab stands .35 proud, .25 thick; a drip mould under it near the top
            fr2 = Fr((fr.o[0], y - .25, fr.o[2]), fr.n, fr.half, 0)
            fr2.box(g, -fr.half - (.35 if fr is fz else 0), fr.half, 0, .25, 0, .35, F['ledge'], None, bevel=0, cell=0)
            if near and T.i == 0:
                fr2.box(g, -fr.half, fr.half, -.12, 0, 0, .18, F['ledgedark'], None, bevel=0, cell=0)
        y -= 3.6
        k += 1
    if T.i == 0:
        # a service run down the side face near the corner: a conduit bundle on clips and a downpipe
        for (dz, r, f) in ((-.9, .06, 'galvdark'), (-.72, .035, 'cable'), (-.6, .035, 'cable')):
            rod(g, (x0 - .08, -.05, dz), (x0 - .08, -40, dz), r, F[f], 5, caps=False)
        for yy in (-1.5, -5.2, -8.8, -12.4):
            box(g, x0 - .16, x0, yy - .03, yy + .03, -1.0, -.5, F['galv'], None, 0, cell=0)


def lip(g, x0, x1, z0, z1, T):
    """The parapet lip the pilot sits on (top .32, .42 deep: Core's) under a coping that overhangs, the cyan strip in a channel
    under the coping on both outer faces, expansion joints, flashing at its foot."""
    cop, wall = F['coping'], F['parapet']
    top = LIP
    b = .012 if T.i == 0 else 0
    # the lip's blocks: front (along x) and side (along z)
    box(g, x0, x1, 0, top - .06, z0, z0 + LIPD, wall, T, 0, cell=0, skip=('-y', '+y'))
    box(g, x0, x0 + LIPD, 0, top - .06, z0 + LIPD, z1, wall, T, 0, cell=0, skip=('-y', '+y', '-z'))
    # the coping: Core's overhang (.05 out, .03 in), its top at .32
    box(g, x0 - .05, x1, top - .06, top, z0 - .05, z0 + LIPD + .03, cop, T, b, cell=(.8, 0, 0)[T.i], skip=('-y',))
    box(g, x0 - .05, x0 + LIPD + .03, top - .06, top, z0 + LIPD + .03, z1, cop, T, b, cell=(.8, 0, 0)[T.i], skip=('-y', '-z'))
    # the coping's underside (the overhang seen from below and beside)
    g.face(((x0 - .05, top - .06, z0 - .05), (x1, top - .06, z0 - .05), (x1, top - .06, z0), (x0 - .05, top - .06, z0)), F['ledgedark'], (0, -1, 0))
    g.face(((x0 - .05, top - .06, z0), (x0, top - .06, z0), (x0, top - .06, z1), (x0 - .05, top - .06, z1)), F['ledgedark'], (0, -1, 0))
    # the cyan strip: Core's bar under the coping on the outer faces, as a diffuser in a dark channel
    cy = glow(rk.CYAN, 2.0)
    fz = Fr(((x0 + x1) / 2, 0, z0), (0, 0, -1), (x1 - x0) / 2, top)
    fx = Fr((x0, 0, (z0 + z1) / 2), (-1, 0, 0), (z1 - z0) / 2, top)
    for fr in (fz, fx):
        fr.box(g, -fr.half, fr.half, top - .13, top - .075, 0, .03, F['metaldark'], None, bevel=0, cell=0)
        fr.box(g, -fr.half, fr.half, top - .12, top - .085, .03, .05, cy, None, bevel=0, cell=0)
    if T.i == 2:
        return
    # the inner face's flashing at the lip's foot and expansion joints in the coping every 2.4 m
    box(g, x0 + LIPD, x1, 0, .07, z0 + LIPD, z0 + LIPD + .025, F['flashing'], None, 0, cell=0, skip=('-y',))
    box(g, x0 + LIPD, x0 + LIPD + .025, 0, .07, z0 + LIPD + .025, z1, F['flashing'], None, 0, cell=0, skip=('-y',))
    xx = x0 + 1.9
    while xx < x1 - .5:
        box(g, xx, xx + .014, top, top + .004, z0 - .05, z0 + LIPD + .03, F['void'], None, 0, cell=0, skip=('-y',))
        xx += 2.4


def hvac(g, T):
    """The HVAC unit where Core's stands (source x -3.9 .. -2.1, z -1.25 .. -.05, to y 1.15; its fan at (-3, -.65) steams):
    a panelled casing on feet and pads, louvred sides, the fan in its cowl under a guard, a control box with its green lamp, a
    condensate line and a conduit to the tray."""
    xa, xb, za, zb = X(-2.1), X(-3.9), -1.25, -.05          # 2.1 .. 3.9
    body, dark = F['galv'], F['metaldark']
    for k in range(2):
        fx = xa + .05 + k * 1.45
        box(g, fx, fx + .3, 0, .12, za + .05, zb - .05, dark, None, 0, cell=0, skip=('-y',))
        if T.i == 0:
            box(g, fx - .05, fx + .35, 0, .03, za, zb, F['pad'], None, 0, cell=0, skip=('-y',))
    box(g, xa, xb, .12, 1.15, za, zb, body, T, .025, cell=0)
    # louvres: the +z face (toward the roof) and the face toward the pilot and the camera (-x)
    fz = Fr(((xa + xb) / 2, .12, zb), (0, 0, 1), (xb - xa) / 2, 1.03)
    fxm = Fr((xa, .12, (za + zb) / 2), (-1, 0, 0), (zb - za) / 2, 1.03)
    for fr, half in ((fz, .78), (fxm, .46)):
        fr.quad(g, -half, half, .16, .86, .012, F['void'])
        ck.slats(g, fr, -half, half, .16, .86, .016, .05, .075, F['galvdark'], T)
        if T.i == 0:
            for (ua, ub, va, vb) in ((-half - .03, half + .03, .86, .89), (-half - .03, half + .03, .13, .16), (-half - .03, -half, .16, .86), (half, half + .03, .16, .86)):
                fr.box(g, ua, ub, va, vb, 0, .055, body, None, bevel=0, cell=0)
    # the fan: a cowl ring, the dark throat, blades and a guard
    cx, cz, r = X(-3.0), -.65, .48
    sides = (16, 10, 8)[T.i]
    lathe(g, [(r + .09, 1.15), (r + .09, 1.25), (r + .04, 1.28), (r, 1.27), (r, 1.15)], (cx, 0, cz), dark, sides)
    ck.disc(g, (cx, 1.19, cz), r, F['void'], sides, (0, 1, 0))
    if T.i == 0:
        for k in range(5):
            a = 2 * math.pi * k / 5
            b0 = (cx + .1 * math.cos(a), 1.215, cz + .1 * math.sin(a))
            b1 = (cx + .44 * math.cos(a + .5), 1.2, cz + .44 * math.sin(a + .5))
            b2 = (cx + .44 * math.cos(a + .1), 1.23, cz + .44 * math.sin(a + .1))
            g.face((b0, b1, b2), F['galvdark'], (0, 1, 0))
        ck.disc(g, (cx, 1.235, cz), .11, F['galv'], 8, (0, 1, 0))
        for k in range(4):
            a = math.pi * k / 4
            rod(g, (cx - (r + .04) * math.cos(a), 1.285, cz - (r + .04) * math.sin(a)), (cx + (r + .04) * math.cos(a), 1.285, cz + (r + .04) * math.sin(a)), .008, F['steel'], 3, caps=False)
        for rr in (.2, .34):
            sweep(g, sk.circle_section(.007, 3), [(cx + rr * math.cos(2 * math.pi * i / 14), 1.287, cz + rr * math.sin(2 * math.pi * i / 14)) for i in range(14)], F['steel'], closed=True, caps=False)
    # the control box on the pilot's side with its green lamp (Core's neon quad), the conduit out to the tray
    fxm.box(g, -.42, -.2, .6, .98, 0, .1, F['gunmetal'], None, bevel=.008 if T.i == 0 else 0, cell=0)
    fxm.box(g, -.34, -.28, .84, .9, .1, .112, glow(rk.LIME, 2.2), None, bevel=0, cell=0)
    if T.i < 2:
        ck.pipe(g, [(xa, .6, -.4), (xa - .16, .6, -.4), (xa - .16, .03, -.4), (xa - .16, .03, -1.0)], .035, F['galvdark'], 5, bend=.08)
        ck.pipe(g, [(xb - .2, .2, zb), (xb - .2, .2, zb + .25), (xb - .2, .03, zb + .25), (xb + .5, .03, zb + .25)], .02, F['copper'], 4, bend=.05)


def stack(g, T):
    """The vent stack (source (-4.9, -.6), .3 across, 1.6 tall, its cowl to 1.9: it steams) with a flashing collar and a
    conical cowl on stays, and two mushroom vents beside it."""
    cx, cz = X(-4.9), -.6
    sides = (12, 8, 6)[T.i]
    lathe(g, [(.46, 0), (.44, .05), (.33, .1), (.3, .12), (.3, 1.6)], (cx, 0, cz), F['galv'], sides)
    lathe(g, [(.42, 1.6), (.42, 1.66), (.3, 1.9), (.06, 1.94)], (cx, 0, cz), F['galvdark'], sides)
    if T.i == 0:
        ck.disc(g, (cx, 1.6, cz), .42, F['void'], sides, (0, -1, 0))
        for k in range(3):
            a = 2 * math.pi * k / 3
            rod(g, (cx + .3 * math.cos(a), 1.45, cz + .3 * math.sin(a)), (cx + .4 * math.cos(a), 1.62, cz + .4 * math.sin(a)), .012, F['steel'], 3, caps=False)
        for (vx, vz) in ((6.0, -.95), (6.55, -.95)):
            lathe(g, [(.11, 0), (.09, .04), (.07, .3)], (vx, 0, vz), F['galv'], 8)
            lathe(g, [(.15, .3), (.16, .34), (.08, .42), (.01, .43)], (vx, 0, vz), F['galvdark'], 8)


def mast(L, g, T):
    """The lattice mast (source (-6.95, -1.15): its footing, three legs to 8.6, the pole to 9.1 under Core's beacon) with rings
    and diagonals, the dish Core hangs at 6.2 m, antenna panels, a cable down a leg to a junction box and two guy wires."""
    bx, bz = X(-6.95), -1.15
    dark, steel = F['metaldark'], F['galv']
    box(g, X(-6.4), X(-7.5), 0, .25, -1.7, -.6, F['concrete'], T, .02, cell=0, skip=('-y',))
    base = (bx, .25, bz)
    legs = [(bx - .32, .25, bz), (bx + .16, .25, bz + .28), (bx + .16, .25, bz - .28)]
    top = (bx, 8.6, bz)
    sides = (4, 3, 3)[T.i]
    for p in legs:
        rod(g, p, top, .03, dark, sides, caps=False)
    n = (9, 5, 3)[T.i]
    for k in range(1, n):
        f = k / n
        pts = [sk._lerp(p, top, f) for p in legs]
        for i in range(3):
            rod(g, pts[i], pts[(i + 1) % 3], .015, dark, 3, caps=False)
            if T.i == 0:
                rod(g, sk._lerp(legs[i], top, (k - 1) / n), pts[(i + 1) % 3], .012, dark, 3, caps=False)
    rod(g, top, (bx, 9.1, bz), .025, steel, 4, caps=False)
    box(g, bx - .05, bx + .05, 9.08, 9.2, bz - .05, bz + .05, glow(rk.MAGENTA, 1.4), None, 0, cell=0)
    # the dish (Core: at source (-6.6, 6.2, -1.3), a .42 m rim tipped), on its arm
    d = (X(-6.6), 6.2, -1.3)
    rod(g, (X(-6.9), 6.1, bz), d, .025, steel, 4, caps=False)
    ax = sk._norm((-.75, .25, -.6))
    ck.dome(g, sk._add(d, ax, -.12), .42, F['creme'], (12, 8, 6)[T.i], 2, axis=ax, squash=.3)
    if T.i == 0:
        rod(g, d, sk._add(d, ax, .32), .012, steel, 3, caps=False)
        for k in range(3):
            p = sk._lerp(legs[k], top, .78)
            box(g, p[0] - .05, p[0] + .05, p[1] - .45, p[1] + .45, p[2] - .05, p[2] + .05, F['creme'], None, 0, cell=0)
        # the cable down a leg into a junction box on the footing
        rod(g, sk._lerp(legs[1], top, .8), (legs[1][0] + .02, .5, legs[1][2] + .02), .012, F['cable'], 3, caps=False)
        box(g, bx + .2, bx + .45, .25, .6, bz + .2, bz + .38, F['gunmetal'], None, .01, cell=0, skip=('-y',))
        # guy wires to the lip and the roof
        rod(g, (bx, 6.4, bz), (bx + 2.6, .02, bz + 2.2), .006, F['cable'], 3, caps=False)
        rod(g, (bx, 6.4, bz), (bx - 2.4, LIP, -1.55), .006, F['cable'], 3, caps=False)


def stairhouse(g, T):
    """The stair house (source x -24 .. -15, z 4 .. 13, 4.2 tall, its roof slab to 4.45): board-marked walls on a plinth, the
    overhanging roof with a fascia, the door in its lit frame under Core's lamp, a louvred vent on the roof, the cyan sign
    panel as a board with a word in tube."""
    xa, xb, za, zb = X(-15), X(-24), 4.0, 13.0             # 15 .. 24
    box(g, xa, xb, 0, 4.2, za, zb, F['concretedark'], T, 0, cell=(1.5, 0, 0)[T.i], skip=('-y', '+y'))
    box(g, xa - .4, xb + .4, 4.2, 4.45, za - .4, zb + .4, F['coping'], T, .02, cell=0)
    fr = Fr((xa, 0, (za + zb) / 2), (-1, 0, 0), (zb - za) / 2, 4.2)   # the face toward the plant, the pilot and the camera
    # the door (source z 7.0 .. 8.4): u = z - 8.5 on this face read from outside
    zu = lambda z: fr.world_u(z - (za + zb) / 2)  # noqa: E731
    ua, ub = sorted((zu(7.0), zu(8.4)))
    fr.box(g, ua, ub, 0, 2.3, 0, .05, F['metaldark'], None, bevel=0, cell=0, skip=('-y',))
    fr.box(g, ua - .1, ub + .1, 2.36, 2.44, 0, .07, glow(rk.WARM, 1.5), None, bevel=0, cell=0)
    # Core's lamp box over the door (source (-15 .. -14.6, 2.6 .. 2.8, 7.5 .. 7.9))
    box(g, xa - .4, xa, 2.6, 2.8, 7.5, 7.9, glow(0xFFC890, 1.6), None, 0, cell=0)
    # the sign panel (source z 9 .. 12, y 1.2 .. 3.3)
    sa, sb = sorted((zu(9.0), zu(12.0)))
    ck.sign_board(g, fr, sa, sb, 1.2, 3.3, T, d=.06)
    fr.quad(g, sa + .12, sb - .12, 1.32, 3.18, .075, glow(ck.mix(rk.CYAN, 0x05060A, .75), .7))
    ck.neon_text(g, fr, 'ROOF', (sa + sb) / 2, 2.2, .085, rk.CYAN, 2.4, T, d=.16)
    ck.neon_text(g, fr, 'ACCESS', (sa + sb) / 2, 1.5, .06, rk.ICE, 1.8, T, d=.16)
    # the vent box on the roof (source (-17.2, 4.45, 6) .. (-16, 5.4, 7.4))
    box(g, X(-16), X(-17.2), 4.45, 5.4, 6.0, 7.4, F['galv'], None, 0, cell=0, skip=('-y',))
    box(g, X(-16.1), X(-17.1), 5.4, 5.5, 6.1, 7.3, F['galvdark'], None, 0, cell=0)
    if T.i == 0:
        fr.box(g, ua - .12, ub + .12, 0, .1, 0, .3, F['pad'], None, bevel=0, cell=0, skip=('-y',))
        fr.box(g, ua + .9, ua + .96, 1.0, 1.15, .05, .11, F['chrome'], None, bevel=0, cell=0)


def pipes(g, T):
    """The pipes on sleepers from the stair house to the plant (Core's runs) with flanges and a valve wheel, the cable tray
    along the lip as a ladder tray with its cables, conduits up to the strip."""
    y = .32
    xa, xb = X(-4.2), X(-13.5)                              # 4.2 .. 13.5
    sides = (8, 6, 4)[T.i]
    xx = -13.5
    while xx < -4.5:
        box(g, X(xx + .3), X(xx), 0, .18, .25, .85, F['concrete'], None, .01 if T.i == 0 else 0, cell=0, skip=('-y',))
        xx += 1.6
    rod(g, (xa, y, .42), (xb, y, .42), .08, F['galv'], sides, caps=True)
    rod(g, (xa, y, .68), (xb, y, .68), .05, F['rust'], sides, caps=True)
    ck.pipe(g, [(xa + .01, y, .42), (X(-3.9), y, .25), (X(-3.6), y, -.05)], .08, F['galv'], sides, bend=.12)
    if T.i == 0:
        for k in range(5):
            fx = xa + 1.2 + k * 1.9
            lathe(g, [(.11, -.025), (.11, .025)], (fx, y, .42), F['galvdark'], 8, axis=(1, 0, 0), cap0=True, cap1=True)
        # a gate valve with its wheel
        vx = 8.3
        rod(g, (vx, y, .42), (vx, y + .3, .42), .03, F['galvdark'], 5, caps=False)
        sweep(g, sk.circle_section(.014, 4), [(vx + .12 * math.cos(2 * math.pi * i / 10), y + .3, .42 + .12 * math.sin(2 * math.pi * i / 10)) for i in range(10)], F['redpaint'], closed=True, caps=False)
        for a in (0, math.pi / 2):
            rod(g, (vx - .12 * math.cos(a), y + .3, .42 - .12 * math.sin(a)), (vx + .12 * math.cos(a), y + .3, .42 + .12 * math.sin(a)), .01, F['redpaint'], 3, caps=False)
    # the tray along the lip (Core's bar from source x -14 to -1.3 at z -1.05)
    ta, tb, tz = X(-1.3), X(-14), -1.05
    if T.i == 2:
        box(g, ta, tb, .02, .14, tz - .06, tz + .06, F['metaldark'], None, 0, cell=0, skip=('-y',))
        return
    for s in (-.1, .1):
        box(g, ta, tb, .05, .12, tz + s - .008, tz + s + .008, F['galvdark'], None, 0, cell=0)
    n = int((tb - ta) / (.3 if T.i == 0 else .9))
    for k in range(n + 1):
        px = ta + (tb - ta) * k / n
        box(g, px - .012, px + .012, .05, .07, tz - .1, tz + .1, F['galvdark'], None, 0, cell=0)
    for k in range(0, n + 1, 6):
        px = ta + (tb - ta) * k / n
        box(g, px - .03, px + .03, 0, .05, tz - .12, tz + .12, F['pad'], None, 0, cell=0, skip=('-y',))
    for (dz, r, f) in ((-.06, .018, 'cable'), (-.02, .014, 'cable'), (.03, .02, 'rubber'), (.07, .012, 'copper')):
        rod(g, (ta, .085 + r, tz + dz), (tb, .085 + r, tz + dz), r, F[f], 4, caps=False)


def crates(g, T):
    """The crates by the plant (Core's two, source x -6.1 .. -5.3) as banded equipment cases under a tarp, the tool case by
    the pilot (source x -1.9 .. -1.2, z .4 .. .75) with its handle and latches, and what a pilot leaves on a roof."""
    ca, cb = X(-5.3), X(-6.1)                               # 5.3 .. 6.1
    wood = F['timberdarkx']
    box(g, ca, cb, 0, .75, .1, .9, wood, T, .015, cell=0, skip=('-y',))
    box(g, ca + .15, cb - .15, .75, 1.15, .25, .75, F['gunmetal'], T, .02, cell=0, skip=('-y',))
    ta, tb = X(-1.2), X(-1.9)                               # 1.2 .. 1.9
    box(g, ta, tb, 0, .32, .4, .75, F['redpaint'], T, .03, cell=0, skip=('-y',))
    if T.i == 2:
        return
    for yy in (.12, .6):
        box(g, ca - .012, cb + .012, yy, yy + .06, .088, .912, F['metaldark'], None, 0, cell=0)
    # a tarp thrown over half the upper case, its corner hanging
    g.face(((ca + .1, 1.165, .2), (cb - .1, 1.165, .2), (cb - .1, 1.165, .55), (ca + .1, 1.165, .55)), F['tarp'], (0, 1, 0))
    g.face(((ca + .1, 1.165, .2), (cb - .1, 1.165, .2), (cb - .05, .62, .08), (ca + .05, .7, .1)), F['tarp'], (0, .3, -1))
    # the tool case: a lid seam, a handle, two latches
    box(g, ta - .004, tb + .004, .2, .215, .396, .754, F['metaldark'], None, 0, cell=0)
    ck.pipe(g, [(ta + .22, .32, .575), (ta + .22, .39, .575), (tb - .22, .39, .575), (tb - .22, .32, .575)], .012, F['metaldark'], 4, bend=.03)
    for lx in (ta + .14, tb - .2):
        box(g, lx, lx + .06, .17, .25, .385, .4, F['chrome'], None, 0, cell=0)
    if T.i == 0:
        # two cans and a thermos on the lip by the pilot's left hand, a small lantern on the roof behind
        for (cx, cz, f) in ((.62, -1.52, 'can'), (.74, -1.6, 'carred')):
            lathe(g, [(.03, 0), (.033, .01), (.033, .11), (.026, .122)], (cx, LIP, cz), F[f], 8, cap1=True)
        lathe(g, [(.04, 0), (.042, .02), (.042, .2), (.03, .23), (.03, .27)], (.95, LIP, -1.5), F['steel'], 8, cap1=True)
        lx, lz = .45, -.78
        lathe(g, [(.06, 0), (.07, .02), (.05, .05)], (lx, 0, lz), F['metaldark'], 8)
        lathe(g, [(.045, .05), (.045, .17)], (lx, 0, lz), glow(0xFFB060, 1.6), 8)
        lathe(g, [(.06, .17), (.065, .19), (.02, .23)], (lx, 0, lz), F['metaldark'], 8)


def lamps(L, g, T):
    """The string of lamps from the mast to its pole (Core's glows hang where the layout says): the pole with a bracket, the
    cable in a catenary, a shaded bulb at each glow."""
    pole = (X(-14.8), 0.0, -.9)
    rod(g, pole, (pole[0], 3.4, pole[2]), .04, F['metaldark'], (6, 4, 3)[T.i], caps=True)
    if T.i == 0:
        lathe(g, [(.14, 0), (.12, .04), (.05, .1)], pole, F['pad'], 8)
    s0, s1 = (X(-6.95), 3.6, -1.15), (pole[0], 3.3, pole[2])
    pts = []
    for k in range(13):
        f = k / 12
        p = sk._lerp(s0, s1, f)
        pts.append((p[0], p[1] - .55 * 4 * f * (1 - f), p[2]))
    sweep(g, sk.circle_section(.008, 3), pts, F['cable'], caps=False)
    bulbs = [p for (p, size, kind) in L.glows if abs(size - .32) < 1e-3 and kind == 0 and 2.5 < p[1] < 3.6 and 6 < p[0] < 15.5]
    for p in bulbs:
        if T.i < 2:
            lathe(g, [(.012, .1), (.07, .06), (.075, .035)], (p[0], p[1], p[2]), F['metaldark'], 6)
        lathe(g, [(.03, .04), (.035, .0), (.02, -.035), (.001, -.045)], (p[0], p[1], p[2]), glow(0xFFC27A, 1.8), 6)


def bladesign(g, x0, z0, T):
    """The blade sign off the corner below the lip (source x 1.8 .. 2.3, y -7.25 .. -.75, z -2.5 .. .1; Core's `sign` screen
    lies on its outer face): a dark casing between two brackets, its edges in magenta tube."""
    xa, xb, ya, yb, za, zb = X(2.3), X(1.8), -7.25, -.75, -2.5, .1     # -2.3 .. -1.8
    box(g, xa + .02, xb, ya, yb, za, zb, F['metaldark'], None, 0, cell=0)
    mg = glow(rk.MAGENTA, 2.4)
    r = .05
    sides = (4, 3, 3)[T.i]
    for zz in (za - .03, zb + .03):
        rod(g, (xa + .05, ya, zz), (xa + .05, yb, zz), r, mg, sides, caps=False)
        rod(g, (xb - .05, ya, zz), (xb - .05, yb, zz), r, mg, sides, caps=False)
    for yy in (ya - .03, yb + .03):
        rod(g, (xa + .05, yy, za), (xa + .05, yy, zb), r, mg, sides, caps=False)
    for yy in (-1.05, -6.8):
        box(g, xb, x0, yy - .1, yy + .1, -1.6, -.8, F['gunmetal'], None, 0, cell=0)


# Core's retro screen (NativeRooftopCity.Retro*): its centre, size and heading in the source frame
TV_C, TV_W, TV_H, TV_YAW = (-1.2, .78, -.45), .8, .6, 1.02


def tv(L, g, T):
    """The old TV set (the Ricochet easter egg's screen, Core's `retro`): a cream tube set on a plastic crate, its tube bulging
    behind the bezel, a dial and speaker slots at its side, rabbit ears, its lead running to the tray. The screen's quad is
    Core's: the bezel stands round it and nothing lies in front of it."""
    s = next((s for s in L.screens if s['kind'] == 'retro'), None)
    if s is None:
        c, w, h, n = (X(TV_C[0]), TV_C[1], TV_C[2]), TV_W, TV_H, (-math.sin(TV_YAW), 0.0, math.cos(TV_YAW))
    else:
        c, w, h, n = s['c'], s['w'], s['h'], s['n']
    shell, dark = F['crtshell'], F['crtdark']
    fr = Fr((c[0], c[1] - h / 2, c[2]), n, w / 2, h)       # v from the screen's bottom edge, d out of the screen
    side = .2                                              # the control strip at the reader's right
    bz = .07                                               # the bezel's width
    u0, u1, v0, v1 = -w / 2 - bz, w / 2 + bz + side, -bz - .03, h + bz
    # the shell: the front frame round the screen, the body tapering back to the tube's neck
    rc.lbox(g, fr, u0, u1, v0, v1, -.42, -.02, shell, bevel=.02 if T.i == 0 else 0, T=T)
    rc.lbox(g, fr, u0 + .12, u1 - .12, v0 + .08, v1 - .1, -.62, -.42, dark)
    # the bezel: four rails standing proud of the glass
    for (ua, ub, va, vb) in ((u0, u1, h, v1), (u0, u1, v0, 0), (u0, -w / 2, 0, h), (w / 2, u1, 0, h)):
        rc.lbox(g, fr, ua, ub, va, vb, -.02, .035, shell)
    if T.i < 2:
        # the inner lip (dark) just round the glass
        for (ua, ub, va, vb) in ((-w / 2, w / 2, h - .012, h), (-w / 2, w / 2, 0, .012), (-w / 2, -w / 2 + .012, 0, h), (w / 2 - .012, w / 2, 0, h)):
            rc.lbox(g, fr, ua, ub, va, vb, -.015, .02, dark)
        # the control strip: two dials, speaker slots, a red standby lamp
        cu = w / 2 + bz + side / 2 - .01
        for k, vv in enumerate((h * .8, h * .58)):
            ctr = fr.at(cu, vv, .035)
            lathe(g, [(.045, 0), (.045, .03), (.03, .04)], ctr, dark, 8, axis=fr.n, cap1=True)
        for k in range(5):
            vv = h * .12 + k * .035
            rc.lbox(g, fr, cu - .06, cu + .06, vv, vv + .012, .03, .04, dark)
        rc.lbox(g, fr, cu - .012, cu + .012, h * .42, h * .42 + .024, .035, .045, glow(rk.RED, 1.6))
        # rabbit ears from a puck on top
        top = fr.at(0, v1, -.25)
        lathe(g, [(.06, 0), (.06, .03), (.03, .045)], top, dark, 8, cap1=True)
        for sgn in (-1, 1):
            rod(g, (top[0], top[1] + .03, top[2]), sk._add(sk._add(top, fr.ud, sgn * .32), (0, 1, 0), .62), .006, F['chrome'], 3, caps=False)
    # the crate it stands on, and its lead
    base = fr.at((u0 + u1) / 2, v0, -.3)
    rc.lbox(g, Fr((base[0], 0, base[2]), n, 0, 0), -.36, .36, 0, base[1], -.26, .26, F['bluepaint'], bevel=.015 if T.i == 0 else 0, T=T)
    if T.i == 0:
        fb = Fr((base[0], 0, base[2]), n, 0, 0)
        for k in range(3):
            rc.lbox(g, fb, -.3, .3, .06 + k * .1, .11 + k * .1, .262, .27, F['void'])
        tail = fr.at((u0 + u1) / 2, v0 + .2, -.62)
        ck.pipe(g, [tail, (tail[0], .02, tail[2]), (tail[0] + .5, .02, tail[2] - .1), (tail[0] + .6, .02, -1.0)], .008, F['cable'], 3, bend=.05)


def extras(g, x0, x1, z0, z1, T):
    """Fine tier only: more of a working roof out toward the stair house (seen at 21:9): a bank of condensers on a frame, a
    skylight, a satellite dish on a ballast mount, a ladder up the stair house."""
    # three condensers on a steel frame
    for k in range(3):
        cx = 9.6 + k * 1.3
        box(g, cx, cx + 1.1, .25, 1.05, 3.2, 3.9, F['creme'], None, .02, cell=0, skip=('-y',))
        fr = Fr((cx + .55, .25, 3.2), (0, 0, -1), .55, .8)
        ck.fan_grille(g, fr, 0, .4, .3, .01, sk.Tier(1))
    for zz in (3.25, 3.85):
        box(g, 9.5, 13.4, .18, .25, zz - .03, zz + .03, F['galvdark'], None, 0, cell=0)
    for cx in (9.6, 11.4, 13.3):
        for zz in (3.25, 3.85):
            box(g, cx - .03, cx + .03, 0, .18, zz - .03, zz + .03, F['galvdark'], None, 0, cell=0, skip=('-y',))
    # a skylight: a kerb and a hipped glass roof
    sx0, sx1, sz0, sz1 = 4.6, 6.8, 4.2, 6.0
    box(g, sx0, sx1, 0, .35, sz0, sz1, F['coping'], None, .01, cell=0, skip=('-y', '+y'))
    apex = [(sx0 + .7, .8, sz0 + .7), (sx1 - .7, .8, sz0 + .7), (sx1 - .7, .8, sz1 - .7), (sx0 + .7, .8, sz1 - .7)]
    rim = [(sx0, .35, sz0), (sx1, .35, sz0), (sx1, .35, sz1), (sx0, .35, sz1)]
    for k in range(4):
        j = (k + 1) % 4
        g.face((rim[k], rim[j], apex[j], apex[k]), F['roofglass'])
        rod(g, rim[k], apex[k], .025, F['mullion'], 3, caps=False)
    g.face(apex, glow(0xBFD8FF, .35), (0, 1, 0))
    # a dish on a ballast mount
    dx, dz = 13.2, 1.0
    box(g, dx - .5, dx + .5, 0, .08, dz - .5, dz + .5, F['pad'], None, 0, cell=0, skip=('-y',))
    rod(g, (dx, .08, dz), (dx, .9, dz), .035, F['galvdark'], 5, caps=False)
    ax = sk._norm((-.5, .7, -.5))
    ck.dome(g, (dx, .95, dz), .5, F['creme'], 12, 2, axis=ax, squash=.3)
    # a ladder up the stair house's near face
    lx = 14.94
    for zz in (5.0, 5.4):
        rod(g, (lx, 0, zz), (lx, 4.7, zz), .02, F['galv'], 3, caps=False)
    for k in range(13):
        rod(g, (lx, .3 + k * .34, 5.0), (lx, .3 + k * .34, 5.4), .014, F['galv'], 3, caps=False)
