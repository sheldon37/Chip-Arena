"""The story layers across Meridian's street, pass 2 (art bible 1.4-1.7, 4.4's 6:47 and long-night layers, 6.7's small kit):

- the survivors' SANCTUARY marks with the walked metres, all pointing home: sprayed on the road and on walls (white or orange,
  with overspray and drips), and chalked on walls and pavement flags;
- barricades: a Vale pushed over on its side with furniture piled against it (an alley or side-street mouth), and a plug of
  furniture alone; both on Astra's barrier- blocks, filling them within 0.25 m;
- posters in layers: Civil Defence notices under STAY INDOORS (torn, a corner peeling), under hand-made MISSING bills, all
  soaked and streaked; a bill taped round a lamp post; a strip of bills for shutters, kiosks and shelters;
- puddles with their wet rims (pools, a kerb run, a pothole with its broken edge, an overflowing drain) and the drip line
  under an eave.

Paint, chalk, posters and puddles are faces lying on their surface (6 cm or less; the bank layers them). Every mark and every
piece of furniture is original, drawn from the bible's words. Frames: see mkit.py; ground marks have their origin on the
ground with +z the way the arrow points (home); wall pieces have theirs on the wall's face, +z out of it.
"""
import math
import random

import kit2 as k2
from kit2 import F, Fr, Module, add, rot_y, lathe, tube, bar, rod, box, fbox, quad, poly
import mkit as mk
import stagekit as sk

UPF = (0.0, 1.0, 0.0)


def ground_frame(o=(0.0, 0.0, 0.0)):
    """A frame on the ground: a along +x, b along +z (text reads for someone walking toward +z), c up."""
    return Fr(o, UPF, (0.0, 0.0, 1.0))


# ---------------------------------------------------------------------------------------------------------------------------
# SANCTUARY marks
# ---------------------------------------------------------------------------------------------------------------------------
def sanctuary_road(T, metres=320, colour='white', seed=11):
    """Sprayed across a lane, read walking home (+z): SANCTUARY, the walked metres, and a big filled arrow."""
    k2.finishes()
    m = Module('mark.sanctuary.road' + ('' if colour == 'white' else '-' + colour), T.lod, block='flat paint (6 cm or less)', cap=(500, 8),
               note='%d M (the walked metres: a dressing parameter); arrow toward +z, home' % metres)
    g = m.part('body')
    f = F['spray-' + colour]
    fr = ground_frame((0.0, .004, 0.0))
    if T.lod == 0:
        k2.hand_text(g, fr, 0.0, 0.0, .42, 'SANCTUARY', f, 'spray', c=0.0, seed=seed, align='centre', slant=.05)
        k2.hand_text(g, fr, 0.0, .72, .42, '%d M' % metres, f, 'spray', c=.0004, seed=seed + 1, align='centre', slant=.05)
    # the arrow: along +z (b), so it is drawn in a frame whose a runs along +z
    fa = Fr((0.0, .0042, 0.0), UPF, (-1.0, 0.0, 0.0))
    k2.hand_arrow(g, fa, 1.45, 0.0, 2.1, .95, f, 'spray', c=0.0, seed=seed + 2, lod=T.lod, filled=True)
    return m


def sanctuary_wall(T, metres=320, colour='white', direction=1, seed=21):
    """Sprayed on a wall at 1.0-1.8 m: SANCTUARY, an arrow along the wall (direction +1: toward the reader's right), the metres;
    paint drips under the strokes."""
    k2.finishes()
    m = Module('mark.sanctuary.wall' + ('' if colour == 'white' else '-' + colour), T.lod, mount='wall', rest=(0.0, 0.0, 0.0),
               block='a wall (flat paint)', cap=(500, 10), note='%d M; arrow %s' % (metres, 'right' if direction > 0 else 'left'))
    g = m.part('body')
    f = F['spray-' + colour]
    fr = Fr((0.0, 0.0, 0.0), (0, 0, 1))
    if T.lod == 0:
        k2.hand_text(g, fr, 0.0, 1.5, .24, 'SANCTUARY', f, 'spray', c=.003, seed=seed, align='centre', wall=True)
        k2.hand_text(g, fr, -.15 * direction, 1.02, .2, '%d M' % metres, f, 'spray', c=.0034, seed=seed + 1, align='centre', wall=True)
    k2.hand_arrow(g, fr, -.55 * direction, 1.36, 1.1, .3, f, 'spray', c=.0038, seed=seed + 2, direction=direction, lod=T.lod)
    return m


def chalk_wall(T, metres=80, direction=1, seed=31):
    """Chalked at hand height (1.1-1.5 m): SANCTUARY, an arrow and the metres, a tally of the ones who passed, re-chalked."""
    k2.finishes()
    m = Module('mark.chalk.wall', T.lod, mount='wall', block='a wall (flat chalk)', cap=(600, 0),
               note='%d M; arrow %s; dropped at LOD1' % (metres, 'right' if direction > 0 else 'left'))
    g = m.part('body')
    f = F['chalk']
    fr = Fr((0.0, 0.0, 0.0), (0, 0, 1))
    if T.lod > 0:
        return m
    k2.hand_text(g, fr, 0.0, 1.42, .1, 'SANCTUARY', f, 'chalk', c=.002, seed=seed, align='centre', slant=.12)
    k2.hand_arrow(g, fr, -.32 * direction, 1.31, .64, .12, f, 'chalk', c=.0024, seed=seed + 1, direction=direction)
    k2.hand_text(g, fr, .02, 1.12, .085, '%d M' % metres, f, 'chalk', c=.0026, seed=seed + 2, align='centre', slant=.12)
    # the tally: two gates of five
    r = random.Random(seed)
    x0 = .34 if direction > 0 else -.62
    for gate in range(2):
        xg = x0 + gate * .15
        for i in range(4):
            x = xg + i * .025
            k2._strip(g, fr, [(x + r.uniform(-.004, .004), 1.13), (x + r.uniform(-.006, .006), 1.24)], .007, .006, .0028, f, False)
        k2._strip(g, fr, [(xg - .015, 1.15), (xg + .095, 1.22)], .007, .006, .0031, f, False)
    return m


def chalk_ground(T, metres=60, seed=41):
    """Chalked on a pavement flag: an arrow home (+z) in a ring, and the metres."""
    k2.finishes()
    m = Module('mark.chalk.ground', T.lod, block='flat chalk', cap=(300, 0), note='%d M; dropped at LOD1' % metres)
    g = m.part('body')
    if T.lod > 0:
        return m
    f = F['chalk']
    fa = Fr((0.0, .003, 0.0), UPF, (-1.0, 0.0, 0.0))
    k2.hand_arrow(g, fa, -.25, 0.0, .55, .22, f, 'chalk', c=0.0, seed=seed)
    r = random.Random(seed)
    ring = [(.34 * math.cos(a) + r.uniform(-.012, .012), .34 * math.sin(a) + r.uniform(-.012, .012)) for a in
            (2 * math.pi * i / 14 for i in range(15))]
    k2._strip(g, ground_frame((0.0, .0032, .05)), ring, .012, .01, 0.0, f, False)
    k2.hand_text(g, ground_frame((0.0, .0034, 0.0)), 0.0, -.62, .1, '%d M' % metres, f, 'chalk', c=0.0, seed=seed + 1, align='centre')
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# Furniture for the barricades (original pieces, each in its own frame: origin on the floor under its middle, +z its front)
# ---------------------------------------------------------------------------------------------------------------------------
def place(dst, src, q=(0.0, 0.0, 0.0, 1.0), at=(0.0, 0.0, 0.0)):
    dst.extend(src.transformed(sk.affine(q, at)))


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx, aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def Q(ax, ay, az, deg):
    return sk.quat_axis(ax, ay, az, math.radians(deg))


def wardrobe(T, w=1.0, h=1.9, d=.55, f='wood-dark'):
    g = sk.Geo()
    fw = F[f]
    box(g, T, -w / 2, w / 2, .08, h - .08, -d / 2, d / 2, fw, .012)
    box(g, None, -w / 2 - .03, w / 2 + .03, h - .08, h, -d / 2 - .03, d / 2 + .03, fw, 0.0)        # the cornice
    box(g, None, -w / 2 + .02, w / 2 - .02, 0.0, .08, -d / 2 + .02, d / 2 - .02, fw, 0.0)  # the plinth
    if T.lod == 0:
        fr = Fr((0, 0, d / 2), (0, 0, 1))
        for (a0, a1) in ((-w / 2 + .05, -.015), (.015, w / 2 - .05)):
            fbox(g, None, fr, a0, a1, .2, h - .2, 0.0, .012, fw, 0.0, skip=('-z',))
        quad(g, fr, .06, w / 2 - .09, .5, h - .4, .0135, F['glass-dark'])        # the mirror on one door
        for a in (-.04, .04):
            box(g, None, -a - .01, -a + .01, .95, 1.1, d / 2 + .012, d / 2 + .035, F['brass'], 0.0)
    return g


def mattress(T, w=.95, l=1.9, t=.17):
    g = sk.Geo()
    box(g, T, -w / 2, w / 2, 0.0, t, -l / 2, l / 2, F['mattress'], .045)
    if T.lod == 0:
        fr = Fr((0, t, 0), UPF, (0, 0, 1))
        for i in range(7):
            a = -w / 2 + .1 + i * (w - .2) / 6
            quad(g, fr, a - .018, a + .018, -l / 2 + .06, l / 2 - .06, .002, F['mattress-stripe'])
    return g


def chair(T, f='wood-mid'):
    g = sk.Geo()
    fw = F[f]
    for (x, z) in ((-.19, -.18), (.19, -.18), (-.19, .18), (.19, .18)):
        box(g, None, x - .02, x + .02, 0.0, .45, z - .02, z + .02, fw, 0.0)
    box(g, T, -.22, .22, .45, .49, -.21, .21, fw, .008)
    for x in (-.19, .19):
        box(g, None, x - .02, x + .02, .49, .95, -.2, -.16, fw, 0.0)
    for y in (.68, .86):
        box(g, None, -.19, .19, y, y + .05, -.195, -.165, fw, 0.0)
    return g


def table(T, w=1.2, d=.75, h=.74, f='wood-mid'):
    g = sk.Geo()
    fw = F[f]
    box(g, T, -w / 2, w / 2, h - .035, h, -d / 2, d / 2, fw, .01)
    for (x, z) in ((-w / 2 + .06, -d / 2 + .06), (w / 2 - .06, -d / 2 + .06), (-w / 2 + .06, d / 2 - .06), (w / 2 - .06, d / 2 - .06)):
        box(g, None, x - .03, x + .03, 0.0, h - .035, z - .03, z + .03, fw, 0.0)
    if T.lod == 0:
        box(g, None, -w / 2 + .09, w / 2 - .09, h - .12, h - .035, -d / 2 + .05, -d / 2 + .07, fw, 0.0)
        box(g, None, -w / 2 + .09, w / 2 - .09, h - .12, h - .035, d / 2 - .07, d / 2 - .05, fw, 0.0)
    return g


def door_slab(T, w=.82, h=2.0, f='paint-door-blue'):
    g = sk.Geo()
    fd = F[f]
    box(g, T, -w / 2, w / 2, 0.0, h, -.022, .022, fd, .006)
    if T.lod == 0:
        fr = Fr((0, 0, .022), (0, 0, 1))
        for (b0, b1) in ((.12, .82), (.98, 1.86)):
            for (a0, a1) in ((-w / 2 + .09, -.04), (.04, w / 2 - .09)):
                fbox(g, None, fr, a0, a1, b0, b1, 0.0, .01, fd, 0.0, skip=('-z',))
        mk.ellipsoid(g, fr.P(w / 2 - .09, .98, .03), ((1, 0, 0), (0, 1, 0), (0, 0, 1)), (.025, .025, .02), F['brass'], 6, 2, top=1.0, bottom=-1.0)
        quad(g, fr, -.12, .12, 1.0, 1.035, .0105, F['brass'])
    return g


def plank(T, l, w=.15, t=.025, f='wood-raw'):
    g = sk.Geo()
    box(g, None, -l / 2, l / 2, 0.0, t, -w / 2, w / 2, F[f], 0.0)
    return g


def sandbag(T, l=.62, w=.36, h=.16, seed=0):
    """A Civil Defence sandbag (hessian): a squashed hexagonal pillow, its tied end pinched."""
    g = sk.Geo()
    r = random.Random(seed)
    f = F['hessian'] if r.random() < .7 else F['hessian-dark']
    pts = [(-l / 2, 0.0), (-l / 2 + .07, -w / 2), (l / 2 - .05, -w / 2 + .02), (l / 2, 0.0), (l / 2 - .05, w / 2 - .02), (-l / 2 + .07, w / 2)]
    mk.prism(g, pts, 0.0, h * .7, f, top=False)
    mk.prism(g, [(x * 1.0, z * 1.0) for (x, z) in pts], h * .7, h, f, top=True, taper=.72)
    return g


def fridge(T, w=.6, h=1.45, d=.6):
    g = sk.Geo()
    box(g, T, -w / 2, w / 2, .04, h, -d / 2, d / 2, F['enamel-white'], .035)
    if T.lod == 0:
        fr = Fr((0, 0, d / 2), (0, 0, 1))
        quad(g, fr, -w / 2 + .03, w / 2 - .03, h - .32, h - .3, .002, F['ink'])
        box(g, None, w / 2 - .09, w / 2 - .06, h - .7, h - .42, d / 2, d / 2 + .04, F['chrome'], 0.0)
        mk.disc(g, fr.P(0, h - .2, .002), .03, F['chrome'], 8, fr.n)
        k2.run(g, fr, -w / 2 + .1, .4, .3, .05, .003, F['rust'])
    box(g, None, -w / 2 + .03, w / 2 - .03, 0.0, .04, -d / 2 + .03, d / 2 - .03, F['iron-black'], 0.0)
    return g


def sofa(T, w=1.8, d=.82, f='fabric-green'):
    g = sk.Geo()
    ff = F[f]
    if T.lod > 0:
        box(g, None, -w / 2, w / 2, 0.0, .86, -d / 2, d / 2, ff, 0.0)
        return g
    box(g, T, -w / 2, w / 2, .1, .42, -d / 2, d / 2, ff, .03)
    box(g, T, -w / 2, w / 2, .42, .86, -d / 2, -d / 2 + .2, ff, .04)
    for s in (-1, 1):
        box(g, T, s * w / 2 - .14 * (s > 0), s * w / 2 + .14 * (s < 0), .42, .64, -d / 2 + .2, d / 2, ff, .04)
    if T.lod == 0:
        for x in (-.38, .38):
            box(g, T, x - .36, x + .36, .42, .53, -d / 2 + .22, d / 2 - .02, ff, .035)
    for (x, z) in ((-w / 2 + .1, -d / 2 + .1), (w / 2 - .1, -d / 2 + .1), (-w / 2 + .1, d / 2 - .1), (w / 2 - .1, d / 2 - .1)):
        box(g, None, x - .03, x + .03, 0.0, .1, z - .03, z + .03, F['wood-dark'], 0.0)
    return g


def headboard(T, w=1.4, h=1.1, f='wood-dark-x'):
    g = sk.Geo()
    fw = F[f]
    if T.lod > 0:
        box(g, None, -w / 2 - .035, w / 2 + .035, 0.0, h, -.035, .035, fw, 0.0)
        return g
    for s in (-1, 1):
        box(g, None, s * w / 2 - .035, s * w / 2 + .035, 0.0, h, -.035, .035, fw, 0.0)
    box(g, T, -w / 2, w / 2, h - .14, h - .04, -.025, .025, fw, .01)
    box(g, None, -w / 2, w / 2, .3, .36, -.02, .02, fw, 0.0)
    if T.lod == 0:
        for i in range(1, 7):
            x = -w / 2 + w * i / 7
            box(g, None, x - .015, x + .015, .36, h - .14, -.015, .015, fw, 0.0)
    return g


def pallet(T, l=1.2, w=.8):
    g = sk.Geo()
    if T.lod > 0:
        box(g, None, -l / 2, l / 2, 0.0, .125, -w / 2, w / 2, F['wood-raw'], 0.0)
        return g
    for z in (-w / 2 + .05, 0.0, w / 2 - .05):
        box(g, None, -l / 2, l / 2, 0.0, .1, z - .045, z + .045, F['wood-raw'], 0.0)
    n = 5 if T.lod == 0 else 2
    for i in range(n):
        x = -l / 2 + .07 + i * (l - .14) / (n - 1)
        box(g, None, x - .06, x + .06, .1, .125, -w / 2, w / 2, F['wood-raw-z'], 0.0)
    return g


def wire_coil(T, length=2.0, radius=.24, pitch=.32):
    """Concertina wire along +x: a helix of raw wire (outward tube, three sides). Dropped at LOD1 (a 7 mm wire is gone by
    38 m)."""
    g = sk.Geo()
    if T.lod > 0:
        return g
    turns = max(1, int(length / pitch))
    per = T.pick(7, 3)
    pts = []
    for i in range(turns * per + 1):
        t = i / per
        a = 2 * math.pi * t
        pts.append((-length / 2 + length * i / (turns * per), radius + radius * math.sin(a), radius * math.cos(a)))
    tube(g, pts, .007, F['wire'], 3, caps=False)
    return g


def corrugated(T, w=1.9, h=1.0, f='corr-rust', ribs=9):
    """A sheet of corrugated iron (ribs modelled at LOD0), both faces: the survivors' first material (bible 3.4)."""
    g = sk.Geo()
    ff = F[f]
    if T.lod > 0:
        for s in (1, -1):
            g.face([(-w / 2, 0, .0), (w / 2, 0, .0), (w / 2, h, .0), (-w / 2, h, .0)], ff, (0, 0, s))
        return g
    n = ribs * 2
    xs = [-w / 2 + w * i / n for i in range(n + 1)]
    zs = [(.018 if i % 2 else -.018) for i in range(n + 1)]
    for i in range(n):
        a, b = (xs[i], zs[i]), (xs[i + 1], zs[i + 1])
        nx, nz = -(b[1] - a[1]), (b[0] - a[0])
        for s in (1, -1):
            g.face([(a[0], 0.0, a[1]), (b[0], 0.0, b[1]), (b[0], h, b[1]), (a[0], h, a[1])], ff, (s * nx, 0.0, s * nz))
    return g


def underbody(g, T):
    """What a parked Vale never shows: its floor's details (bible 6.6's car, pushed over): axles, the differential, the
    exhaust, leaf springs, the fuel tank, the sump, the wheels' inner faces, rust. In the car's own frame."""
    import car
    ub, rust = F['underbody'], F['rust']
    if T.lod > 0:
        return
    for zc in (car.ZF, car.ZR):
        rod(g, (-.62, car.YW, zc), (.62, car.YW, zc), .035, ub, 6, caps=False)
        for s in (-1, 1):
            mk.disc(g, (s * (.715 - .083), car.YW, zc), .2, F['rubber'], 10, (-s, 0, 0))
    mk.ellipsoid(g, (0.0, car.YW, car.ZR), ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.14, .12, .12), ub, 8, 3, top=1.0, bottom=-1.0)
    box(g, None, -.07, .07, .15, .24, -.6, car.ZR - .1, ub, 0.0, skip=('+y',))                     # the propeller shaft's tunnel
    tube(g, [(.32, .17, 1.6), (.3, .16, .4), (.3, .16, -1.0), (.38, .17, -1.75), (.38, .2, -2.12)], .028, F['iron-raw'], 6, caps=True)
    box(g, None, -.5, .2, .14, .24, -2.0, -1.55, ub, 0.0, skip=('+y',))                             # the fuel tank
    box(g, None, -.2, .2, .13, .24, 1.25, 1.75, ub, 0.0, skip=('+y',))                              # the sump
    for s in (-1, 1):
        box(g, None, s * .55 - .03, s * .55 + .03, .2, .24, car.ZR - .55, car.ZR + .55, ub, 0.0, skip=('+y',))
    for (x, z, w, l) in ((.2, .6, .5, .35), (-.4, -.3, .3, .45), (.5, -1.3, .25, .3)):
        fr = Fr((x, .135, z), (0, -1, 0), (0, 0, 1))
        poly(g, fr, [(-w / 2, -l / 2), (w / 2, -l / 2 + .05), (w / 2 - .04, l / 2), (-w / 2 + .05, l / 2 - .03)], 0.0, rust)


def overturned_vale(T, paint='mustard'):
    """The Vale pushed over onto its side (its left side down), its underbody facing +z, its nose toward -x."""
    import car
    m = car.vale(T, paint, 'smashed')
    g = sk.Geo()
    for p in m.parts.values():
        g.extend(p)
    underbody(g, T)
    tip = Q(0, 0, 1, 90)                     # the car's +y up turns to -x: it lies on its -x side, the floor toward +x
    t = g.transformed(sk.affine(tip, (0.0, .87, 0.0)))
    yaw = Q(0, 1, 0, -90)                    # the floor (now +x) turns to face +z
    return t.transformed(sk.affine(yaw, (0.0, 0.0, 0.0)))


# ---------------------------------------------------------------------------------------------------------------------------
# Barricades
# ---------------------------------------------------------------------------------------------------------------------------
def barricade_car(T, seed=5, arrow=-1, metres=210):
    """An overturned Vale across a side street's mouth with furniture piled against it, on a barrier- block (6.0 x 2.2 m,
    2.6 m): a wardrobe laid along its top under a coil of wire, a mattress and a door leaning on its floor, a table on its side,
    chairs, a fridge and pallets at the ends, sandbags along its foot; a sheet of corrugated iron sprayed SANCTUARY with an
    arrow (arrow -1: toward the reader's left) pointing home along the main street."""
    k2.finishes()
    m = Module('story.barricade.car', T.lod, block='barrier- (6.0 x 2.2 x 2.6; Astra)', cap=(7000, 700),
               note='+z faces the street it guards; the arrow points home (%s)' % ('left' if arrow < 0 else 'right'))
    g = m.part('body')
    r = random.Random(seed)
    # the car: its roof toward -z (z -1.0), its floor and wheels toward the street (z 0.18-0.42), its nose toward -x
    place(g, overturned_vale(T, 'mustard'), Q(0, 1, 0, 9.0), (.1, 0.0, .3))
    # along its upturned side (y 1.73): a wardrobe on its back, the wire over it; a chair thrown on its side beyond
    place(g, wardrobe(T, 1.0, 1.9, .55), qmul(Q(0, 1, 0, 90), Q(1, 0, 0, -90)), (.95, 1.73 + .265, -.12))
    place(g, wire_coil(T, 1.9, .16, .3), Q(0, 1, 0, 1.5), (0.0, 2.27, -.12))
    if T.lod == 0:
        place(g, chair(T, 'wood-dark'), Q(0, 0, 1, 95), (1.9, 1.95, -.15))
    # leaning on its floor, street side: a mattress (left), the corrugated sheet sprayed SANCTUARY (middle), a door (right)
    place(g, mattress(T), qmul(Q(0, 1, 0, -14), qmul(Q(1, 0, 0, 70), Q(0, 1, 0, 90))), (2.05, .45, .78))
    place(g, door_slab(T), qmul(Q(0, 1, 0, 8), Q(1, 0, 0, -10)), (-1.25, 0.0, .9))
    sheet = corrugated(T, 1.6, 1.05)
    q_sheet = qmul(Q(0, 1, 0, 2), Q(1, 0, 0, -16))
    at_sheet = (.35, .02, .8)
    place(g, sheet, q_sheet, at_sheet)
    if T.lod == 0:
        sp = sk.Geo()
        f = F['spray-white']
        # (the lowest 0.3 m of the sheet is behind the sandbags: the metres go above them)
        k2.hand_text(sp, Fr((0, 0, .025), (0, 0, 1)), 0.0, .76, .15, 'SANCTUARY', f, 'spray', c=0.0, seed=seed, align='centre', wall=True)
        k2.hand_arrow(sp, Fr((0, 0, .0254), (0, 0, 1)), -.48 * arrow, .6, .96, .18, f, 'spray', c=0.0, seed=seed + 1, direction=arrow)
        k2.hand_text(sp, Fr((0, 0, .0258), (0, 0, 1)), 0.0, .33, .14, '%d M' % metres, f, 'spray', c=0.0, seed=seed + 2, align='centre',
                     wall=True)
        place(g, sp, q_sheet, at_sheet)
    # a fridge at the right end, pallets stacked at the left, a chair in front; sandbags along the foot
    place(g, fridge(T), Q(0, 1, 0, -8), (2.75, 0.0, -.45))
    for i in range(3):
        place(g, pallet(T), Q(0, 1, 0, 90 + r.uniform(-6, 6)), (-2.7, .125 * i, -.45 + r.uniform(-.05, .05)))
    if T.lod == 0:
        place(g, chair(T), Q(0, 1, 0, 200), (-2.35, 0.0, .75))
    nb = 6 if T.lod == 0 else 3
    for i in range(nb):
        x = -.95 + 3.0 * i / (nb - 1)
        place(g, sandbag(T, seed=i), Q(0, 1, 0, r.uniform(-8, 8)), (x, 0.0, 1.06 + r.uniform(-.03, .03)))
    if T.lod == 0:
        for i in range(3):
            x = -.45 + 1.05 * i
            place(g, sandbag(T, seed=20 + i), Q(0, 1, 0, r.uniform(-10, 10)), (x, .16, 1.06 + r.uniform(-.03, .03)))
    return m


def barricade_furniture(T, seed=7):
    """A plug of furniture in an alley mouth, on a barrier- block (3.4 x 1.2 x 2.3 m): a wardrobe standing, a sofa on its end, a
    fridge, a headboard, a door and planks nailed across, chairs thrown on top, sandbags at the foot, a coil of wire, and
    KEEP OUT sprayed on the planks."""
    k2.finishes()
    m = Module('story.barricade.furniture', T.lod, block='barrier- (3.4 x 1.2 x 2.3; Astra)', cap=(3000, 300),
               note='+z faces the street it guards')
    g = m.part('body')
    r = random.Random(seed)
    place(g, wardrobe(T, .95, 1.95, .55, 'wood-mid'), Q(0, 1, 0, 4), (-1.18, 0.0, -.15))
    place(g, sofa(T, 1.7, .8, 'fabric-rose'), qmul(Q(0, 1, 0, -4), Q(0, 0, 1, 90)), (.15, .85 + .0, -.1))
    place(g, fridge(T), Q(0, 1, 0, 12), (1.32, 0.0, -.05))
    place(g, headboard(T), Q(0, 1, 0, -3), (.0, 0.0, .38))
    place(g, door_slab(T, f='paint-door-green'), qmul(Q(0, 1, 0, 2), Q(0, 0, 1, 88)), (.9, .45, .45))
    for (y, a) in ((.55, 3.0), (1.05, -2.0), (1.52, 4.0)):
        place(g, plank(T, 3.3, .22, .03), qmul(Q(0, 0, 1, a), Q(1, 0, 0, -90)), (0.0, y, .502))
    if T.lod == 0:
        sp = sk.Geo()
        k2.hand_text(sp, Fr((0, 0, .506), (0, 0, 1)), .1, .97, .17, 'KEEP OUT', F['spray-white'], 'spray', c=0.0, seed=seed, align='centre',
                     wall=True)
        g.extend(sp)
        place(g, chair(T), qmul(Q(0, 1, 0, 40), Q(1, 0, 0, 160)), (-.3, 2.05, -.2))
        place(g, chair(T, 'wood-dark'), qmul(Q(0, 1, 0, -70), Q(0, 0, 1, -100)), (.6, 1.75, -.25))
    place(g, wire_coil(T, 2.6, .2, .32), Q(0, 1, 0, 0), (0.0, 1.95 if T.lod == 0 else 1.9, -.05))
    nb = 6 if T.lod == 0 else 3
    for i in range(nb):
        x = -1.45 + 2.9 * i / (nb - 1)
        place(g, sandbag(T, seed=40 + i), Q(0, 1, 0, r.uniform(-8, 8)), (x, 0.0, .72 + r.uniform(-.03, .03)))
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# Posters in layers (bible 1.5: Civil Defence notices under STAY INDOORS, under MISSING)
# ---------------------------------------------------------------------------------------------------------------------------
def cd_notice(g, T, fr, a0, b0, w, h, c, kind, r):
    """A Civil Defence notice: cream paper under an olive band (CIVIL DEFENCE), its headline, body text, BY ORDER."""
    k2.sheet(g, fr, a0, b0, w, h, c, F['paper-cream'])
    quad(g, fr, a0, a0 + w, b0 + h - .13, b0 + h - .02, c + .0012, F['paper-olive'])
    if T.lod > 0:
        return
    ax = a0 + w / 2
    mk.text(g, fr, ax, b0 + h - .105, .0085, 'CIVIL DEFENCE', F['ink-white'], c=c + .0024)
    if kind == 'notice':
        mk.text(g, fr, ax, b0 + h - .25, .0135, 'NOTICE', F['ink-black'], c=c + .0012)
        k2.ink_lines(g, fr, a0 + .05, a0 + w - .05, b0 + h - .3, 7, .052, .014, c + .0012, F['ink-black'], r)
    else:
        mk.text(g, fr, ax, b0 + h - .21, .0056, 'WHEN THE SIREN SOUNDS', F['ink-black'], c=c + .0012)
        mk.text(g, fr, ax, b0 + h - .33, .0092, 'TAKE COVER', F['ink-red'], c=c + .0012)
        k2.ink_lines(g, fr, a0 + .05, a0 + w - .05, b0 + h - .38, 5, .052, .014, c + .0012, F['ink-black'], r)
    mk.text(g, fr, ax, b0 + .045, .005, 'BY ORDER', F['ink-black'], c=c + .0012)


def stay_indoors(g, T, fr, a0, b0, w, h, c, r):
    """The evening's poster pasted over the notices: STAY INDOORS in red on cream, a black foot band; its top torn ragged,
    its lower right corner peeling off the wall."""
    cut = .16
    # the sheet without its lower right corner (convex), its top torn in strips
    steps = 10
    xs = [a0 + w * i / steps for i in range(steps + 1)]
    tops = [b0 + h - max(0.0, r.uniform(-.3, 1.0)) * .1 * (1 if i > steps // 2 else .25) for i in range(steps + 1)]
    for i in range(steps):
        xa, xb = xs[i], xs[i + 1]
        ya = b0 + max(0.0, cut - (a0 + w - xa)) if xa > a0 + w - cut else b0
        yb = b0 + max(0.0, cut - (a0 + w - xb)) if xb > a0 + w - cut else b0
        g.face([fr.P(xa, ya, c), fr.P(xb, yb, c), fr.P(xb, tops[i + 1], c), fr.P(xa, tops[i], c)], F['paper-cream'], fr.n)
    # the peeling flap: the cut corner turned out from the wall, its blank back showing
    p0, p1, p2 = (a0 + w - cut, b0), (a0 + w, b0 + cut), (a0 + w - cut * .25, b0 + cut * .3)
    g.face([fr.P(p0[0], p0[1], c), fr.P(p1[0], p1[1], c), fr.P(p2[0], p2[1], c + .045)], F['paper-back'], fr.D(.4, -.6, .7))
    g.face([fr.P(p2[0], p2[1], c + .045), fr.P(p1[0], p1[1], c), fr.P(p0[0], p0[1], c)], F['paper-cream'], fr.D(-.4, .6, -.7))
    if T.lod > 0:
        quad(g, fr, a0 + .06, a0 + w - .06, b0 + h * .55, b0 + h * .8, c + .0012, F['ink-red'])
        return
    ax = a0 + w / 2
    mk.text(g, fr, ax, b0 + h - .38, .034, 'STAY', F['ink-red'], c=c + .0012)
    mk.text(g, fr, ax, b0 + h - .62, .02, 'INDOORS', F['ink-red'], c=c + .0012)
    k2.ink_lines(g, fr, a0 + .1, a0 + w - .1, b0 + h - .7, 4, .05, .013, c + .0012, F['ink-black'], r)
    quad(g, fr, a0, a0 + w - cut, b0 + .02, b0 + .13, c + .0012, F['ink-black'])
    mk.text(g, fr, a0 + (w - cut) / 2, b0 + .057, .0048, 'MERIDIAN CIVIL DEFENCE', F['ink-white'], c=c + .0024)


def missing_bill(g, T, fr, a0, b0, c, r, soaked=False, w=.34, h=.46):
    """A hand-made MISSING bill: marker headline, a small photo, handwriting, tape at its corners."""
    paper = F['paper-soaked'] if soaked else F['paper-white']
    sag = .015 if soaked else 0.0
    g.face([fr.P(a0, b0 - sag, c), fr.P(a0 + w, b0 + sag, c), fr.P(a0 + w, b0 + h, c), fr.P(a0, b0 + h, c)], paper, fr.n)
    if T.lod > 0:
        return
    k2.hand_text(g, fr, a0 + w / 2, b0 + h - .1, .062, 'MISSING', F['ink-black'], 'marker', c=c + .0012, seed=r.randrange(999),
                 align='centre', slant=.04)
    k2.photo(g, fr, a0 + .04, b0 + .14, .12, .15, c + .0012)
    rr = random.Random(r.randrange(9999))
    for i in range(3):
        y = b0 + .27 - i * .055
        x0 = a0 + .18
        pts = [(x0, y)]
        x = x0
        while x < a0 + w - .04:
            x += rr.uniform(.03, .045)
            pts.append((min(x, a0 + w - .04), y + rr.uniform(-.008, .008)))
        k2._strip(g, fr, pts, .006, .006, c + .0012, F['ink-black'], False)
    if not soaked:
        k2.hand_text(g, fr, a0 + w / 2, b0 + .05, .03, 'HAVE YOU SEEN', F['ink-black'], 'marker', c=c + .0012, seed=rr.randrange(999),
                     align='centre')
    for (ta, tb, ang) in ((a0 + .02, b0 + h - .02, .7), (a0 + w - .02, b0 + h - .02, -.7)):
        k2.tape(g, fr, ta, tb, ang, c + .0024)
    if soaked:
        for i in range(3):
            x = a0 + w * (.2 + .3 * i)
            k2.run(g, fr, x, b0 + h - .01, h * r.uniform(.4, .8), .03, c + .0018, F['paper-soaked'])


def posters_wall(T, seed=3):
    """A wall's layered posters, 1.9 x 1.6 m (its foot 0.55 m up when dressed): two notices, STAY INDOORS over them, three
    MISSING bills over that; rain run-off from their feet down the wall."""
    k2.finishes()
    m = Module('story.posters.wall', T.lod, mount='wall', rest=(0.0, .85, 0.0), block='a wall (faces on it, 6 cm or less)', cap=(2000, 40),
               note='layers 3 mm apart; origin at the cluster\'s foot')
    g = m.part('body')
    r = random.Random(seed)
    fr = Fr((0, 0, 0), (0, 0, 1))
    cd_notice(g, T, fr, -.95, .42, .62, .86, .003, 'notice', r)
    cd_notice(g, T, fr, .32, .3, .6, .86, .003, 'siren', r)
    stay_indoors(g, T, fr, -.46, .36, .92, 1.22, .006, r)
    missing_bill(g, T, fr, .5, .66, .009, r)
    missing_bill(g, T, fr, -.2, .02, .009, r)
    missing_bill(g, T, fr, -.92, -.04, .009, r, soaked=True)
    if T.lod == 0:
        for (a, b, L) in ((-.8, -.06, .4), (-.55, -.05, .28), (.05, -.01, .35), (.7, .29, .5), (-.1, .36, .1)):
            k2.run(g, fr, a, b, L, .035, .0012, F['stain'], drift=r.uniform(-.01, .01))
    return m


def posters_bills(T, seed=9):
    """A strip of MISSING bills and a small notice, for shutters, kiosks and shelters (1.3 x 0.55 m)."""
    k2.finishes()
    m = Module('story.posters.bills', T.lod, mount='wall', rest=(0.0, 1.1, 0.0), block='a wall, shutter or screen (flat)', cap=(900, 16))
    g = m.part('body')
    r = random.Random(seed)
    fr = Fr((0, 0, 0), (0, 0, 1))
    missing_bill(g, T, fr, -.66, 0.0, .003, r)
    missing_bill(g, T, fr, -.25, .05, .003, r, soaked=True)
    missing_bill(g, T, fr, .18, -.02, .006, r)
    if T.lod == 0:
        for (a, L) in ((-.5, .25), (.3, .3)):
            k2.run(g, fr, a, -.02, L, .03, .001, F['stain'])
    return m


def poster_post(T, r_shaft=.0985, seed=13, y0=1.4):
    """A MISSING bill taped round a lamp post's shaft from y0 (1.4 m: 0.45 m tall; on a route post, above its rag: y0 1.72 and
    the shaft's radius there), the paper wrapping round the octagon's corners, 200 degrees of it facing the road; two tape
    bands right round."""
    k2.finishes()
    m = Module('story.posters.post' + ('' if y0 == 1.4 else '-high'), T.lod, block='on a lamp post (lamp.sodium.*)', cap=(400, 16),
               note="origin on the post's axis; bill %.2f-%.2f m" % (y0, y0 + .45))
    g = m.part('body')
    R = r_shaft / math.cos(math.pi / 8) + .004
    y1 = y0 + .45
    nf = T.pick(10, 4)
    a0, a1 = math.radians(-100), math.radians(100)

    def arc(rr, aa0, aa1, ya, yb, f, n):
        for i in range(n):
            ta = aa0 + (aa1 - aa0) * i / n
            tb = aa0 + (aa1 - aa0) * (i + 1) / n
            tm = (ta + tb) / 2
            g.face([(rr * math.sin(ta), ya, rr * math.cos(ta)), (rr * math.sin(tb), ya, rr * math.cos(tb)),
                    (rr * math.sin(tb), yb, rr * math.cos(tb)), (rr * math.sin(ta), yb, rr * math.cos(ta))], f,
                   (math.sin(tm), 0.0, math.cos(tm)))
    arc(R, a0, a1, y0, y1, F['paper-white'], nf)
    if T.lod > 0:
        return m
    for yb in (y1 - .04, y0 + .01):
        arc(R + .002, 0.0, 2 * math.pi, yb, yb + .03, F['tape'], 16)
    mk.text_on_drum(g, (0, 0, 0), R + .0012, y0 + .34, .0052, 'MISSING', F['ink-black'], 0.0, lift=0.0)
    arc(R + .0012, math.radians(-26), math.radians(26), y0 + .1, y0 + .24, F['photo-dark'], 3)
    fr = Fr((0, 0, R + .0024), (0, 0, 1))
    poly(g, fr, mk.ngon_pts(.016, 8, 0.0, 0.0, y0 + .195), 0.0, F['photo-mid'])
    poly(g, fr, [(-.032, y0 + .105), (.032, y0 + .105), (.02, y0 + .165), (-.02, y0 + .165)], 0.0, F['photo-mid'])
    mk.text_on_drum(g, (0, 0, 0), R + .0012, y0 + .045, .0021, 'SEEN 6:40 ALBION ROW', F['ink-black'], 0.0, lift=0.0)
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# Puddles and the wet
# ---------------------------------------------------------------------------------------------------------------------------
def puddles(T, kind='pool', seed=1):
    """kind: pool-s, pool-m, pool-l (on asphalt or flags), kerb (a run along a kerb at z 0, the road toward +z), pothole (its
    broken asphalt rim and loose chunks), drain (an overflowing gully grate against the kerb)."""
    k2.finishes()
    caps = {'pool-s': (60, 8), 'pool-m': (60, 8), 'pool-l': (70, 10), 'kerb': (70, 10), 'pothole': (200, 16), 'drain': (160, 20)}
    m = Module('ground.puddle.' + kind, T.lod, block='flat (6 cm or less)', cap=caps[kind], note='water .88 smooth; the rim wet asphalt')
    g = m.part('body')
    r = random.Random(seed * 17 + len(kind))
    if kind.startswith('pool'):
        R = {'pool-s': .45, 'pool-m': .85, 'pool-l': 1.4}[kind]
        k2.puddle(g, T, r, 0.0, 0.0, 0.0, R, aspect=1.35, yaw=r.uniform(0, 3))
    elif kind == 'kerb':
        pts = k2.blob(r, 0.0, .28, .55, T.pick(18, 8), aspect=4.2, wobble=.18)
        pts = [(x, max(.02, z)) for (x, z) in pts]
        if T.lod == 0:
            k2.ring_fan(g, (0.0, .28), pts, 1.06, .05, .003, F['wet-asphalt'])
        k2.fan(g, (0.0, .28), pts, .006, F['puddle'])
    elif kind == 'pothole':
        pts = k2.blob(r, 0.0, 0.0, .5, T.pick(14, 7), aspect=1.25, wobble=.3)
        if T.lod == 0:
            outer = k2.ring_fan(g, (0.0, 0.0), pts, 1.0, .09, .004, F['iron-black'])
            k2.ring_fan(g, (0.0, 0.0), outer, 1.0, .08, .003, F['wet-asphalt'])
            for i in range(5):
                a = r.uniform(0, 2 * math.pi)
                d = r.uniform(.55, .75)
                x, z = d * math.cos(a), d * math.sin(a)
                s = r.uniform(.03, .06)
                sk.obox(g, (x, s * .4, z), (s, s * .4, s * .8), Q(0, 1, 0, r.uniform(0, 180)), F['asphalt'], None, 0.0)
        k2.fan(g, (0.0, 0.0), pts, .007, F['puddle'])
    elif kind == 'drain':
        # the gully grate against the kerb (z 0..0.4), water standing round it and spreading along the gutter
        pts = k2.blob(r, .1, .35, .5, T.pick(16, 7), aspect=2.2, wobble=.2)
        pts = [(x, max(.02, z)) for (x, z) in pts]
        if T.lod == 0:
            k2.ring_fan(g, (.1, .35), pts, 1.08, .06, .003, F['wet-asphalt'])
        k2.fan(g, (.1, .35), pts, .006, F['puddle'])
        box(g, None, -.3, .3, .006, .012, .04, .44, F['iron-black'], 0.0, skip=('-y',))
        if T.lod == 0:
            fr = Fr((0, .0121, 0), UPF, (0, 0, 1))
            for i in range(6):
                a = -.24 + i * .096
                quad(g, fr, a - .018, a + .018, .07, .41, 0.0, F['void'])
    return m


def drip_line(T, length=3.8, seed=4):
    """The drip line under an eave (a shelter's, a canopy's): a wet band along it and the spots where the drops fall."""
    k2.finishes()
    m = Module('ground.wet.dripline', T.lod, block='flat, under an eave', cap=(160, 4), note='%.1f m along x at z 0' % length)
    g = m.part('body')
    r = random.Random(seed)
    if T.lod > 0:
        g.face([(-length / 2, .003, -.12), (length / 2, .003, -.12), (length / 2, .003, .12), (-length / 2, .003, .12)], F['wet-asphalt'], UPF)
        return m
    xs = [-length / 2 + length * i / 12 for i in range(13)]
    for i in range(12):
        za, zb = r.uniform(-.16, -.1), r.uniform(.1, .16)
        g.face([(xs[i], .003, za), (xs[i + 1], .003, za), (xs[i + 1], .003, zb), (xs[i], .003, zb)], F['wet-asphalt'], UPF)
    for i in range(9):
        x = -length / 2 + .2 + (length - .4) * i / 8 + r.uniform(-.08, .08)
        pts = k2.blob(r, x, r.uniform(-.04, .04), r.uniform(.05, .1), 6, 1.3, .25)
        k2.fan(g, (x, 0.0), pts, .005, F['puddle'])
    return m
