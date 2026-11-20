"""The Meridian Motors Vale (art bible 6.6): an original compact saloon of the 1960s-70s, 4.1 x 1.7 x 1.42 m. A rounded
three-box body lofted from sections (the wheel arches cut into its sills), a narrower glasshouse on its belt, chrome bumpers
with over-riders, round headlamps either side of a split grille, wheels with domed hubcaps. Paints: blue, cream, maroon,
green, mustard, white. States: parked (beaded with rain: the wet bake), windscreen smashed, luggage on a roof rack.

Frame: origin on the ground under the car's middle, its nose toward +z (a car- block).
"""
import math

import numpy as np

import mkit as mk
from mkit import F, Fr, Module, add, lathe
import stagekit as sk

ZF, ZR = 1.25, -1.15      # the axles
RA, YW = .37, .29         # the arch's radius, the wheel's centre height
BELT, ROOF = .84, 1.42
HB, HR = .71, .6          # the glasshouse's half width at the belt and at the roof

_TOP = [(-2.06, .76), (-2.0, .795), (-1.9, .82), (-1.6, .84), (-1.12, .845), (.72, .84), (1.0, .8), (1.6, .762), (1.9, .745),
        (2.0, .73), (2.06, .70)]
_HW = [(-2.06, .72), (-1.98, .8), (-1.9, .83), (-1.75, .85), (1.6, .85), (1.85, .84), (1.96, .82), (2.02, .78), (2.06, .7)]


def _interp(tab, z):
    xs = [p[0] for p in tab]
    ys = [p[1] for p in tab]
    return float(np.interp(z, xs, ys))


def _bottom(z):
    y = .24
    if z > 1.85:
        y = .24 + (z - 1.85) / .21 * .06
    if z < -1.9:
        y = .24 + (-1.9 - z) / .16 * .08
    for zc in (ZF, ZR):
        d = abs(z - zc)
        if d < RA - 1e-4:
            y = max(y, YW + math.sqrt(RA * RA - d * d))
    return y


def _section(z, lod):
    """The lower body's ring at z: a rounded section from the sill (or the arch) to the bonnet, belt or deck line."""
    hw, yt, yb = _interp(_HW, z), _interp(_TOP, z), _bottom(z)
    rt = min(.14, (yt - yb) * .3)
    rb = min(.06, (yt - yb) * .15)
    crown = .012
    right = []
    if lod == 0:
        right.append((hw * .5, yt + crown * .7))
        right.append((hw - rt, yt))
        for a in (60, 30, 0):
            t = math.radians(a)
            right.append((hw - rt + rt * math.cos(t), yt - rt + rt * math.sin(t)))
        right.append((hw + .008, (yt - rt + yb + rb) / 2))
        right.append((hw - .02, yb + rb))
        right.append((hw - .02 - rb + rb * math.cos(math.radians(45)), yb + rb - rb * math.sin(math.radians(45))))
        right.append((hw - .02 - rb, yb))
    else:
        right += [(hw - rt, yt), (hw, yt - rt), (hw - .01, yb + rb), (hw - .02 - rb, yb)]
    ring = [(0.0, yt + crown)] + right + [(0.0, yb)] + [(-x, y) for (x, y) in reversed(right)]
    return [(x, y, z) for (x, y) in ring], len(right)


def _cabin(z, lod):
    """The glasshouse's open ring at z (belt to belt over the roof), scaled to the windscreen's and rear window's rake."""
    if z >= .2:
        k = ((.72 - z) / .52) ** .9
    elif z <= -.82:
        k = ((z + 1.12) / .3) ** .85
    else:
        k = 1.0
    k = max(0.0, min(1.0, k))
    h = ROOF - BELT
    hr = HR - (.02 if (z > .15 or z < -.78) else 0.0)
    if lod == 0:
        half = [(HB, 0.0), (HB - .012, .035), (hr + .035, h - .085), (hr, h - .03), (hr * .5, h - .006)]
    else:
        half = [(HB, 0.0), (hr + .035, h - .085), (hr, h - .03)]
    ring = [(-x, y) for (x, y) in half] + [(0.0, h)] + [(x, y) for (x, y) in reversed(half)]
    return [(x, BELT + k * y, z) for (x, y) in ring]


def vale(T, paint='blue', state='parked'):
    """paint: blue, cream, maroon, green, mustard, white. state: parked, smashed, rack."""
    mk.finishes()
    name = 'car.vale.' + paint + ('' if state == 'parked' else '-' + state)
    m = Module(name, T.lod, block='car- (4.1 x 1.7)', cap=(4500, 450), note='Meridian Motors Vale; nose to +z')
    g = m.part('body')
    body = F['car-' + paint]
    chrome, glass, ink, under = F['chrome'], F['glass-crazed' if state == 'smashed' else 'glass-dark'], F['ink'], F['rubber']
    lod = T.lod
    # ---- the lower body
    if lod == 0:
        zs = [2.06, 2.035, 1.99, 1.9, 1.75, ZF + RA + .006, ZF + RA - .004, ZF + .3, ZF + .19, ZF, ZF - .19, ZF - .3, ZF - RA + .004,
              ZF - RA - .006, .808, .792, .72, .708, .692, .4, .05, -.326, -.342, -.6, ZR + RA + .006, ZR + RA - .004, ZR + .3, ZR + .19,
              ZR, ZR - .19, ZR - .3, ZR - RA + .004, ZR - RA - .006, -1.152, -1.168, -1.7, -1.9, -1.98, -2.03, -2.06]
    else:
        zs = [2.06, 1.95, ZF + RA, ZF, ZF - RA, 0.0, ZR + RA, ZR, ZR - RA, -1.95, -2.06]
    zs = sorted(zs, reverse=True)
    rings, nr = [], 0
    for z in zs:
        r, nr = _section(z, lod)
        rings.append(r)
    npts = len(rings[0])
    seams_side = [(.692, .708), (-.342, -.326)]
    seams_top = [(.792, .808), (-1.168, -1.152)]
    if lod == 0:
        side_spans = {4, 5, 6, 7, npts - 5, npts - 6, npts - 7, npts - 8}
        top_spans = {0, 1, 2, npts - 3, npts - 2, npts - 1}
        bottom_spans = {nr - 1, nr, nr + 1, nr + 2}
    else:
        side_spans, top_spans, bottom_spans = set(), set(), {nr, nr + 1}

    def within(za, zb, pair):
        return min(za, zb) >= min(pair) - 1e-6 and max(za, zb) <= max(pair) + 1e-6

    def fin_lower(i, j):
        za, zb = zs[i], zs[i + 1]
        if j in bottom_spans:
            return under
        if any(within(za, zb, p) for p in seams_side) and j in side_spans:
            return ink
        if any(within(za, zb, p) for p in seams_top) and j in top_spans:
            return ink
        return body
    mk.loft(g, rings, body, close0=True, close1=True, fins=fin_lower)
    # ---- the glasshouse
    if lod == 0:
        cz = [.72, .62, .5, .38, .28, .2, .05, -.1, -.27, -.31, -.35, -.55, -.78, -.82, -.92, -1.02, -1.12]
    else:
        cz = [.72, .2, -.82, -1.12]
    crings = [_cabin(z, lod) for z in cz]
    nc = len(crings[0])

    def fin_cabin(i, j):
        za, zb = cz[i], cz[i + 1]
        zm = (za + zb) / 2
        if lod > 0:
            if (zm > .2 or zm < -.82) and 1 <= j <= nc - 3:
                return glass
            if -.82 < zm < .2 and j in (0, nc - 2):
                return glass
            return body
        if j in (0, nc - 2):
            return chrome
        if .22 < zm < .72 and 2 <= j <= nc - 4:
            return glass
        if -1.12 < zm < -.84 and 2 <= j <= nc - 4:
            return glass
        if j in (1, nc - 3) and (-.27 < zm < .2 or -.78 < zm < -.35):
            return glass
        return body
    mk.loft(g, crings, body, closed=False, fins=fin_cabin)
    # ---- the face: headlamps, the split grille, indicators, badge; the tail: lamps, plate, handle
    zf = 2.06
    for s in (-1, 1):
        c = (s * .56, .575, zf)
        if lod == 0:
            lathe(g, [(.094, 0.0), (.097, .018), (.088, .036), (.078, .042)], c, chrome, 12, axis=(0, 0, 1))
            mk.ellipsoid(g, add(c, (0, 0, .036)), ((1, 0, 0), (0, 1, 0), (0, 0, 1)), (.079, .079, .026), F['lens'], 12, 2, top=1.0, bottom=0.0)
            mk.box(g, T, s * .56 - .07, s * .56 + .07, .43, .47, zf - .01, zf + .022, F['lens'], .008)
        else:
            g.face([(s * .56 + .08 * math.cos(a), .575 + .08 * math.sin(a), zf + .02) for a in (2 * math.pi * k / 6 for k in range(6))],
                   F['lens'], (0, 0, 1))
    fr = Fr((0, 0, zf), (0, 0, 1))
    for s in (-1, 1):
        a0, a1 = (-.42, -.045) if s < 0 else (.045, .42)
        if lod == 0:
            mk.quad(g, fr, a0, a1, .46, .64, .006, ink)
            for (b0, b1) in ((.45, .465), (.635, .65)):
                mk.fbox(g, None, fr, a0 - .012, a1 + .012, b0, b1, 0.0, .03, chrome, 0.0, skip=('-z',))
            for a in (a0 - .012, a1):
                mk.fbox(g, None, fr, a, a + .012, .465, .635, 0.0, .03, chrome, 0.0, skip=('-z',))
            for b in (.505, .55, .595):
                mk.fbox(g, None, fr, a0, a1, b - .006, b + .006, .006, .022, chrome, 0.0, skip=('-z',))
        else:
            mk.quad(g, fr, a0, a1, .46, .64, .012, chrome)
    if lod == 0:
        mk.disc(g, fr.P(0, .68, .006), .03, chrome, 8, fr.n)
    zr = -2.06
    fb = Fr((0, 0, zr), (0, 0, -1))
    for s in (-1, 1):
        a = s * .6
        if lod == 0:
            mk.fbox(g, None, fb, a - .045, a + .045, .53, .7, 0.0, .02, F['lens-red'], 0.0, skip=('-z',))
            mk.fbox(g, None, fb, a - .045, a + .045, .46, .52, 0.0, .02, F['lens'], 0.0, skip=('-z',))
            mk.fbox(g, None, fb, a - .055, a + .055, .45, .71, -.004, .012, chrome, 0.0, skip=('-z',))
        else:
            mk.quad(g, fb, a - .05, a + .05, .46, .7, .01, F['lens-red'])
    if lod == 0:
        mk.fbox(g, None, fb, -.24, .24, .44, .55, 0.0, .012, ink, 0.0, skip=('-z',))
        mk.text(g, fb, 0.0, .458, .011, 'MV 647', chrome, c=.018)
        mk.fbox(g, None, fb, -.1, .1, .6, .625, 0.0, .03, chrome, 0.0, skip=('-z',))
    # ---- bumpers and over-riders, the front plate
    for (sgn, zb) in ((1, 2.1), (-1, -2.1)):
        pts = [(-.86, .36, zb - sgn * .22), (-.8, .36, zb - sgn * .06), (-.55, .36, zb), (0.0, .36, zb + sgn * .015), (.55, .36, zb),
               (.8, .36, zb - sgn * .06), (.86, .36, zb - sgn * .22)]
        if lod > 0:
            pts = [pts[0], pts[2], pts[4], pts[6]]
        mk.sweep(g, sk.rect_section(.07, .12, .022 if lod == 0 else 0.0), pts, chrome, caps=True, up=(0, 1, 0), smooth_u=lod == 0)
        if lod == 0:
            for x in (-.34, .34):
                mk.box(g, T, x - .035, x + .035, .27, .5, zb + sgn * .01 - .03, zb + sgn * .01 + .03, chrome, .012)
    if lod == 0:
        fp = Fr((0, 0, 2.13), (0, 0, 1))
        mk.fbox(g, None, fp, -.24, .24, .175, .285, -.012, 0.0, ink, 0.0)
        mk.text(g, fp, 0.0, .192, .011, 'MV 647', chrome, c=.006)
    # ---- wheels
    for zc in (ZF, ZR):
        for s in (-1, 1):
            c = (s * .715, YW, zc)
            ax = (float(s), 0.0, 0.0)
            if lod == 0:
                lathe(g, [(.19, -.083), (.255, -.086), (.284, -.06), (.29, 0.0), (.284, .06), (.255, .086), (.2, .083)], c, F['rubber'], 12, axis=ax)
                lathe(g, [(.2, .083), (.175, .09), (.14, .102), (.07, .114), (.001, .118)], c, chrome, 12, axis=ax)
            else:
                lathe(g, [(.27, -.08), (.27, .08)], c, F['rubber'], 6, axis=ax, cap1=True)
    # ---- mirrors, handles
    if lod == 0:
        for s in (-1, 1):
            mk.rod(g, (s * .815, .76, 1.05), (s * .8, .87, 1.04), .012, chrome, 5, caps=False)
            mk.ellipsoid(g, (s * .8, .9, 1.04), ((0, 0, 1), (1, 0, 0), (0, 1, 0)), (.045, .03, .035), chrome, 8, 3, top=1.0, bottom=-1.0)
            mk.box(g, T, s * .852 - .012, s * .852 + .012, .74, .765, -.24, -.12, chrome, .006)
            mk.rod(g, (s * .45, .87, .66), (s * .08, .955, .56), .008, ink, 4, caps=False)
    # ---- states
    if state == 'smashed':
        _smash(g, T)
    if state == 'rack':
        _rack(g, T)
    m.note += '; ' + paint + ', ' + state
    return m


def _smash(g, T):
    """The windscreen smashed: a hole punched through near the passenger's side, cracks running out of it."""
    if T.lod > 0:
        return
    ring = _cabin(.44, 0)
    xs = [p[0] for p in ring]
    ys = [p[1] for p in ring]
    order = np.argsort(xs)
    c = (-.28, float(np.interp(-.28, np.array(xs)[order], np.array(ys)[order])), .44)
    n = sk._norm((0.0, .69, .72))
    fr = Fr(c, n, (0, 1, 0))
    mk.poly(g, fr, mk.ngon_pts(.075, 7, .3), .012, F['ink'])
    for k in range(7):
        a = 2 * math.pi * k / 7 + .4
        L = .18 + .12 * ((k * 37) % 5) / 5
        mk.hand(g, fr, 0.0, 0.0, .009, a, L, .012, F['ink'], tail=-.06)


def _rack(g, T):
    """A roof rack with the evacuation's luggage strapped on."""
    ch = F['chrome']
    yr = ROOF + .05
    for z in (.05, -.55):
        mk.rod(g, (-.6, yr, z), (.6, yr, z), .014, ch, T.pick(5, 3), caps=False)
        if T.lod == 0:
            for s in (-1, 1):
                mk.rod(g, (s * .6, ROOF - .02, z), (s * .6, yr, z), .014, ch, 4, caps=False)
    for s in (-1, 1):
        mk.rod(g, (s * .6, yr + .02, .15), (s * .6, yr + .02, -.65), .014, ch, T.pick(5, 3), caps=False)
    cases = [((-.25, -.1), (.42, .16, .6), 'leather', 0.08), ((.24, -.05), (.38, .2, .52), 'tarp', -0.1),
             ((.0, -.48), (.5, .14, .32), 'blanket-b', 0.25)]
    for (x, z), (w, h, d), f, yaw in cases:
        q = sk.quat_axis(0, 1, 0, yaw)
        sk.obox(g, (x, yr + .015 + h / 2, z), (w / 2, h / 2, d / 2), q, F[f], T if T.lod == 0 else None, .02 if T.lod == 0 else 0.0)
