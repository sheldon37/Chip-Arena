"""Meridian's cast-iron bus shelter, its flag post, and the Type M kiosk under its eight-sided roof (art bible 6.4, 6.5; the
owner's pass-1 review: a cast-iron shelter in place of the modern aluminium one, and the phone box's roof eight-sided).

The shelter (3.6 x 1.6 x 2.6 m, on shelter- blocks): four cast columns with moulded bases and bell capitals, spandrel brackets
(a quarter-round bar and a roundel) where they meet the beams, a gently curved canopy of milky glass on cast glazing bars, a
pierced valance with a dagger fringe along the front and ends, gutters, two finials, a back screen and two half-depth side
screens (a cast dado with fielded panels and the city crest, small-paned milky glazing above), a timber bench on cast
brackets, the lit timetable case (LAST BUS 18:47) and the shelter lamp. The flag post is its own module on a lamp- block, so
the shelter keeps its 1,800-triangle cap with the cast work. Bus: municipal green and cream; tram (the Station's drift):
maroon and cream.

The kiosk keeps pass 1's body (municipal green frames over a green base, small-paned glazing lit warm on three sides, the cream
enamel back, the lit TELEPHONE frieze, the handset), re-proportioned so its new roof stays within 0.25 m over the 2.6 m stem:
a square cornice with four corner caps, then an octagonal drum and an eight-sided bell roof of concave facets with rolled hips,
up to a gilt finial. Never a dome, never a crown.

Frames: see mkit.py (origin on the block's middle, +z the front: the shelter's open side and the kiosk's door face the kerb).
"""
import math

import kit2 as k2
from kit2 import F, Fr, Module, add, rot_y, lathe, tube, bar, rod, box, fbox, quad, poly
import mkit as mk
import stagekit as sk

K8 = 1 / math.cos(math.pi / 8)


# ---------------------------------------------------------------------------------------------------------------------------
# The cast-iron shelter
# ---------------------------------------------------------------------------------------------------------------------------
XC = 1.72                   # the columns' x
ZF, ZB = .64, -.68          # the front and back column lines
YCAP = 2.2                  # the capitals' top (the beams' underside)
YB = 2.3                    # the beams' top (the glazing bars' foot)


def _canopy(z):
    """The canopy's glass height over z: a gentle barrel across the depth, its crown 0.14 m over the eaves."""
    return YB + .02 + .14 * (1 - (z / .9) ** 2)


def _column(g, T, x, z, f, slim=False):
    n = T.pick(8, 4)
    if T.lod > 0:
        rod(g, (x, 0.0, z), (x, YCAP, z), .05, f, 4, caps=False)
        return
    if slim:
        rod(g, (x, .1, z), (x, YCAP, z), .032, f, 6, caps=False)
        return
    lathe(g, [(.08 * K8, 0.0), (.08 * K8, .07), (.05 * K8, .2)], (x, 0, z), f, n, smooth=False, phase=math.pi / n)
    lathe(g, [(.05 * K8, .2), (.044 * K8, YCAP - .09)], (x, 0, z), f, n, smooth=False, phase=math.pi / n)
    # (no cap: the beam's underside closes the capital)
    lathe(g, [(.044 * K8, YCAP - .09), (.085 * K8, YCAP - .02), (.085 * K8, YCAP)], (x, 0, z), f, n, smooth=False, phase=math.pi / n)


def _bracket(g, T, corner, along, f, R=.3, roundel=None):
    """A spandrel bracket in the vertical plane through `corner` (the column's top inner edge) and the horizontal `along`: a
    quarter-round cast bar from the column to the beam (concave toward the open side), a rosette at its middle."""
    ax, az = along
    pts = []
    nseg = 3
    for i in range(nseg + 1):
        t = (math.pi / 2) * i / nseg
        s, y = R * (1 - math.cos(t)), -R * (1 - math.sin(t))
        pts.append((corner[0] + ax * s, corner[1] + y, corner[2] + az * s))
    side = (az, 0.0, -ax)
    bar(g, pts, .022, .026, f, caps=True, up=side)
    if roundel is not None:
        k = R * (1 - math.sqrt(.5))
        c = (corner[0] + ax * k, corner[1] - k, corner[2] + az * k)
        for s_ in (1, -1):
            mk.disc(g, add(c, side, .015 * s_), .032, roundel, 6, mk.scale(side, s_), phase=math.pi / 6)


def _valance(g, T, a0, a1, y_top, fr, f, n_dagger):
    """A cast valance on a frame's plane (both faces): a band and a dagger fringe under it."""
    band_b = y_top - .09
    for s_ in (1, -1):
        c = .004 * s_
        n = mk.scale(fr.n, s_)
        g.face([fr.P(a0, band_b, c), fr.P(a1, band_b, c), fr.P(a1, y_top, c), fr.P(a0, y_top, c)], f, n)
        w = (a1 - a0) / n_dagger
        for i in range(n_dagger):
            x = a0 + w * i
            g.face([fr.P(x, band_b, c), fr.P(x + w, band_b, c), fr.P(x + w / 2, band_b - .085, c)], f, n)
    # raised bosses along the band, street side only
    k = n_dagger // 2
    for i in range(k):
        x = a0 + (a1 - a0) * (i + .5) / k
        mk.disc(g, fr.P(x, band_b + .045, .008), .022, f, 6, fr.n, phase=math.pi / 6)


def shelter(T, tram=False):
    k2.finishes()
    name = 'furn.shelter.cast' + ('-tram' if tram else '')
    m = Module(name, T.lod, block='shelter- (3.6 x 1.6)', cap=(1800, 180),
               note='cast iron; %s; the flag post is furn.stop.flag on its own lamp- block' % ('maroon and cream, TRAM STOP' if tram else 'green and cream'))
    body, lamp = m.part('body'), m.part('lamp')
    iron = F['paint-maroon'] if tram else F['iron-green']
    cream = F['paint-cream']
    gilt = F['brass']
    glass = F['glass-frosted']
    wood = F['timber-old']
    # ---- columns
    for (x, z) in ((-XC, ZF), (XC, ZF), (-XC, ZB), (XC, ZB)):
        _column(body, T, x, z, iron)
    if T.lod == 0:
        _column(body, T, 0.0, ZB, iron, slim=True)
    # ---- beams: front and back eaves, the two ends
    if T.lod == 0:
        box(body, None, -1.86, 1.86, YCAP, YB, ZF - .05, ZF + .05, iron, 0.0, skip=('+y',))
        box(body, None, -1.86, 1.86, YCAP, YB, ZB - .05, ZB + .05, iron, 0.0, skip=('+y',))
        for s in (-1, 1):
            box(body, None, s * XC - .045, s * XC + .045, YCAP, YB, ZB + .05, ZF - .05, iron, 0.0, skip=('+y',))
    else:
        box(body, None, -1.86, 1.86, YCAP, YB, ZF - .05, ZF + .05, iron, 0.0, skip=('+y', '-y'))
    # ---- spandrel brackets: at the front columns toward the middle and toward the back; at the back toward the front
    if T.lod == 0:
        for s in (-1, 1):
            _bracket(body, T, (s * (XC - .045), YCAP, ZF), (-s, 0.0), iron, roundel=gilt)
            _bracket(body, T, (s * XC, YCAP, ZF - .045), (0.0, -1.0), iron, roundel=gilt)
            _bracket(body, T, (s * XC, YCAP, ZB + .045), (0.0, 1.0), iron, R=.24)
    # ---- the canopy: milky glass over cast glazing bars, both faces (seen from the street and from the roofs)
    nz = T.pick(6, 2)
    zs = [.9 - 1.8 * i / nz for i in range(nz + 1)]
    xs = (-1.9, 1.9)
    for j in range(nz):
        za, zb = zs[j], zs[j + 1]
        ya, yb = _canopy(za), _canopy(zb)
        up = sk._norm((0.0, za - zb, yb - ya))
        if up[1] < 0:
            up = (0.0, -up[1], -up[2])
        body.face([(xs[0], ya, za), (xs[1], ya, za), (xs[1], yb, zb), (xs[0], yb, zb)], glass, up)
        body.face([(xs[0], ya - .01, za), (xs[1], ya - .01, za), (xs[1], yb - .01, zb), (xs[0], yb - .01, zb)], glass,
                  (0.0, -up[1], -up[2]))
    if T.lod == 0:
        for x in (-XC, -.86, 0.0, .86, XC):
            for j in range(nz):
                za, zb = zs[j], zs[j + 1]
                ya, yb = _canopy(za), _canopy(zb)
                body.face([(x - .025, ya - .016, za), (x + .025, ya - .016, za), (x + .025, yb - .016, zb), (x - .025, yb - .016, zb)],
                          iron, (0, -1, 0))
                body.face([(x - .022, ya + .012, za), (x + .022, ya + .012, za), (x + .022, yb + .012, zb), (x - .022, yb + .012, zb)],
                          iron, (0, 1, 0))
        # gutters along the eaves, and the finials at the crown's ends
        for (z, s) in ((.9, 1), (-.9, -1)):
            y = _canopy(z)
            box(body, None, -1.92, 1.92, y - .1, y + .005, z - .04 * (s < 0) - .02, z + .04 * (s > 0) + .02, iron, 0.0)
        for s in (-1, 1):
            lathe(body, [(.05, 0.0), (.05, .03), (.03, .07), (.045, .11), (.001, .26)], (s * 1.9, _canopy(0.0) - .01, 0.0), gilt, 6)
    # ---- the valance: a pierced band with its dagger fringe along the front and both ends
    if T.lod == 0:
        _valance(body, T, -1.9, 1.9, YCAP + .01, Fr((0, 0, ZF + .07), (0, 0, 1)), iron, 22)
        for s in (-1, 1):
            a0, a1 = (ZB - .05, ZF + .05) if s > 0 else (-(ZF + .05), -(ZB - .05))     # the frame's a runs along +z on +x
            _valance(body, T, a0, a1, YCAP + .01, Fr((s * (XC + .07), 0, 0), (s, 0, 0)), iron, 7)
    else:
        fr = Fr((0, 0, ZF + .07), (0, 0, 1))
        for s_ in (1, -1):
            body.face([fr.P(-1.9, YCAP - .16, .004 * s_), fr.P(1.9, YCAP - .16, .004 * s_), fr.P(1.9, YCAP + .01, .004 * s_),
                       fr.P(-1.9, YCAP + .01, .004 * s_)], iron, mk.scale(fr.n, s_))
    # ---- the back screen and the two half-depth side screens: a cast dado, milky glazing above
    def screen(frs, a0, a1, crest=False):
        """A screen on a frame (a0..a1 along it): a cast dado (top rail), fielded panels and the crest on its + face, milky
        glazing above with its bars, both faces."""
        fbox(body, None, frs, a0, a1, .1, .95, -.02, .02, iron, 0.0, skip=('-y',))
        w = a1 - a0
        faces = ((frs, a0, a1), (Fr(frs.o, mk.scale(frs.n, -1)), -a1, -a0))   # the + face, then the - face (a negated)
        if T.lod > 0:
            for (ff, aa0, aa1) in faces:
                quad(body, ff, aa0, aa1, .95, YCAP, .004, glass)
            return
        npan = 2 if w > 2 else 1
        for i in range(npan):
            p0 = a0 + w * i / npan + .12
            p1 = a0 + w * (i + 1) / npan - .12
            for (b0, b1) in ((.24, .27), (.78, .81)):
                quad(body, frs, p0, p1, b0, b1, .026, cream)
            quad(body, frs, p0, p0 + .03, .27, .78, .026, cream)
            quad(body, frs, p1 - .03, p1, .27, .78, .026, cream)
        if crest:
            mk.crest(body, T, frs, (a0 + a1) / 2, .52, .13, .024, cream, gilt, F['ink'], F['enamel-green'], sides=10)
        nb = max(1, int(round(w / .58)))
        for (ff, aa0, aa1) in faces:
            quad(body, ff, aa0, aa1, .95, YCAP, .004, glass)
            for bb in (1.36, 1.78):
                quad(body, ff, aa0, aa1, bb - .018, bb + .018, .011, iron)
            for i in range(1, nb):
                aa = aa0 + (aa1 - aa0) * i / nb
                quad(body, ff, aa - .02, aa + .02, .95, YCAP, .011, iron)
    screen(Fr((0, 0, ZB), (0, 0, 1)), -XC, XC, crest=True)
    for s in (-1, 1):
        # the side screens run from the back column to 0.2 m short of the middle
        frs = Fr((s * XC, 0, 0), (-s, 0, 0))
        a0, a1 = sorted((s * (ZB + .05), s * .18))
        screen(frs, a0, a1)
        if T.lod == 0:
            rod(body, (s * XC, .1, .2), (s * XC, YCAP, .2), .026, iron, 6, caps=False)
    # ---- the bench: three slats on two cast brackets
    if T.lod == 0:
        for x in (-1.2, .9):
            bar(body, [(x, 0.0, -.6), (x, .44, -.6), (x, .44, -.28)], .04, .05, iron, caps=True, up=(1, 0, 0))
        for z in (-.6, -.48, -.36):
            mk.slat(body, (-1.5, .47, z), (1.2, .47, z), .1, .03, wood, up=(0, 1, 0), T=T)
    else:
        box(body, None, -1.5, 1.2, .42, .48, -.62, -.3, wood, 0.0)
    # ---- the lit timetable case on the back screen, facing the kerb: LAST BUS 18:47
    fr = Fr((1.15, 0.0, ZB + .02), (0, 0, 1))
    fbox(body, T, fr, -.3, .3, 1.02, 1.98, 0.0, .07, iron, .012)
    quad(lamp, fr, -.26, .26, 1.06, 1.94, .076, k2.glow(mk.OPAL, 1.8))
    if T.lod == 0:
        ink = F['ink']
        word = 'LAST TRAM' if tram else 'LAST BUS'
        mk.text(body, fr, 0.0, 1.74, .0105 if not tram else .0095, word, ink, c=.082)
        mk.text(body, fr, 0.0, 1.5, .02, '18:47', F['enamel-red'], c=.082)
        mk.text(body, fr, 0.0, 1.87, .0075, 'ROUTE 647' if not tram else 'TRAM 6', ink, c=.082)
        for i in range(5):
            b = 1.12 + i * .065
            quad(body, fr, -.22, -.08, b, b + .012, .082, ink)
            for a in (-.02, .08):
                quad(body, fr, a, a + .07, b, b + .012, .082, ink)
    # ---- the shelter lamp under the front beam
    yl = YCAP - .05
    box(body, None, -.3, .3, yl, YCAP, ZF - .14, ZF - .05, iron, 0.0, skip=('+y',))
    lamp.face([(-.27, yl - .002, ZF - .135), (.27, yl - .002, ZF - .135), (.27, yl - .002, ZF - .055), (-.27, yl - .002, ZF - .055)],
              k2.glow(mk.BRIGHT_ROOM, 2.2), (0, -1, 0))
    m.light((0.0, yl - .1, .3), mk.BRIGHT_ROOM, 5.0, kind='practical', note='the shelter lamp')
    m.light((1.15, 1.5, ZB + .3), mk.OPAL, 2.5, kind='practical', note='the timetable case')
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# The flag post: an enamel plate on a cast post (BUS STOP, the transit mark, the routes), on its own lamp- block
# ---------------------------------------------------------------------------------------------------------------------------
def stop_flag(T, tram=False):
    k2.finishes()
    name = 'furn.stop.flag' + ('-tram' if tram else '')
    m = Module(name, T.lod, block='lamp- (0.3 m)', cap=(600, 60), note='the plate reaches 0.6 m toward +z at 2.55-3.05 m')
    body = m.part('body')
    iron = F['paint-maroon'] if tram else F['iron-green']
    plate_ground = F['paint-cream'] if tram else F['enamel-cream']
    band = iron
    if T.lod == 0:
        lathe(body, [(.07, 0.0), (.07, .04), (.05, .09)], (0, 0, 0), iron, 8)
        rod(body, (0, .09, 0), (0, 3.12, 0), .04, iron, 8, caps=False)
        mk.ellipsoid(body, (0, 3.14, 0), ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.048, .048, .05), F['brass'], 8, 3, top=1.0, bottom=-.3)
        lathe(body, [(.04, 2.4), (.055, 2.43), (.055, 2.47), (.04, 2.5)], (0, 0, 0), iron, 8)
    else:
        rod(body, (0, 0.0, 0), (0, 3.12, 0), .045, iron, 4, caps=False)
    b0, b1 = 2.55, 3.05
    a_in, a_out = .05, .6
    box(body, T, -.012, .012, b0, b1, a_in, a_out, plate_ground, .006)
    for sgn in (-1, 1):
        fr = Fr((sgn * .012, 0.0, (a_in + a_out) / 2), (sgn, 0, 0))
        quad(body, fr, -.275, .275, 2.9, 3.05, .006, band)
        if T.lod == 0:
            mk.text(body, fr, 0.0, 2.94, .0105, 'TRAM STOP' if tram else 'BUS STOP', plate_ground, c=.012)
            mk.transit_mark(body, T, fr, 0.0, 2.76, .085, .006, band, F['ink'])
            mk.text(body, fr, 0.0, 2.585, .0078, '6 * 12 * 31' if tram else '647 * 12 * 31', F['ink'], c=.006)
        else:
            quad(body, fr, -.09, .09, 2.68, 2.85, .006, band)
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# The Type M kiosk under its eight-sided roof
# ---------------------------------------------------------------------------------------------------------------------------
YK = (.16, .58, 1.84, 1.98, 2.2)    # the base's top, the kick rail, the glazing's head, the top rail's top, the frieze's top


def kiosk(T):
    k2.finishes()
    m = Module('furn.kiosk.type-m', T.lod, block='kiosk- (1.2 x 2.6 x 1.2; needs the new stem from Astra)', cap=(1400, 150),
               note='door to +z; glazed three sides, cream back; the eight-sided roof stays within 0.25 m over the 2.6 m stem')
    body, lamp = m.part('body'), m.part('lamp')
    iron, cream, gilt = F['iron-green'], F['enamel-cream'], F['brass']
    H = .55
    yb, yk, yg, yt, yf = YK
    box(body, T, -H - .03, H + .03, 0.0, yb, -H - .03, H + .03, iron, .02, skip=('-y',))
    warm = k2.glow(0xFFC98A, 1.7)
    for k in range(4):
        yaw = k * math.pi / 2
        n = rot_y((0, 0, 1), yaw)
        fr = Fr(mk.scale(n, H), n)
        back = k == 2
        if T.lod > 0:
            fbox(body, T, fr, -H, H, yb, yt, -.06, 0.0, cream if back else iron, skip=('-z', '-y'))
            if not back:
                quad(lamp, fr, -.45, .45, yk + .04, yg - .02, .006, warm)
            continue
        E = H - .085
        fbox(body, T, fr, -H, -E, yb, yt, -.085, 0.0, iron, .012)
        fbox(body, None, fr, -E, E, yb, yk, -.035, 0.0, iron, 0.0, skip=('-z',))
        fbox(body, None, fr, -.36, .36, yb + .09, yk - .1, 0.0, .012, iron, 0.0, skip=('-z',))
        fbox(body, None, fr, -E, E, yg, yt, -.035, 0.0, iron, 0.0, skip=('-z',))
        if back:
            fbox(body, None, fr, -E, E, yk, yg, -.03, 0.0, cream, 0.0, skip=('-z',))
            mk.crest(body, T, fr, 0.0, 1.36, .15, .006, cream, gilt, F['ink'], F['enamel-green'])
            fbox(body, None, fr, -.3, .3, .98, 1.11, 0.0, .01, F['enamel-green'], 0.0, skip=('-z',))
            mk.text(body, fr, 0.0, 1.012, .011, 'MERIDIAN', cream, c=.016)
            continue
        quad(lamp, fr, -E, E, yk, yg, -.02, warm)
        fbox(body, None, fr, -E, E, yk - .02, yk + .02, -.03, .02, iron, 0.0, skip=('-z',))
        for a in (-.155, .155):
            fbox(body, None, fr, a - .022, a + .022, yk + .02, yg, -.02, .008, iron, 0.0, skip=('-z',))
        for i in range(1, 6):
            b = yk + (yg - yk) * i / 6
            fbox(body, None, fr, -E, E, b - .02, b + .02, -.02, .006, iron, 0.0, skip=('-z',))
        if k == 0:
            rod(body, fr.P(.38, .95, .05), fr.P(.38, 1.26, .05), .014, gilt, 6)
            for b in (.98, 1.23):
                rod(body, fr.P(.38, b, .0), fr.P(.38, b, .05), .01, gilt, 4, caps=False)
        if k == 1:
            ink = F['ink']
            c = -.012
            quad(body, fr, -.06, .1, 1.13, 1.4, c, ink)
            quad(body, fr, -.16, -.12, 1.12, 1.38, c, ink)
            quad(body, fr, -.185, -.1, 1.33, 1.4, c, ink)
            quad(body, fr, -.185, -.1, 1.09, 1.16, c, ink)
            poly(body, fr, [(-.14, 1.09), (-.12, 1.09), (-.02, .97), (-.04, .97)], c, ink)
            poly(body, fr, [(-.04, .97), (-.02, .97), (.02, 1.13), (0.0, 1.13)], c, ink)
    # the TELEPHONE frieze: teal enamel, lit, cream letters
    teal = k2.glow(mk.TEAL_SIGN, .9)
    letters = k2.glow(mk.OPAL, 2.4)
    if T.lod > 0:
        box(body, None, -H - .03, H + .03, yt, yf, -H - .03, H + .03, iron, 0.0, skip=('-y',))
    for k in range(4):
        n = rot_y((0, 0, 1), k * math.pi / 2)
        fr = Fr(mk.scale(n, H), n)
        if T.lod == 0:
            fbox(body, None, fr, -H - .02, H + .02, yt, yf, -.02, .03, iron, 0.0)
        quad(lamp, fr, -H + .02, H - .02, yt + .025, yf - .025, .036, teal)
        if T.lod == 0:
            mk.text(lamp, fr, 0.0, yt + .049, .0175, 'TELEPHONE', letters, c=.042)
    # ---- the roof: a square cornice with corner caps, an octagonal drum, eight concave facets with rolled hips, a finial
    yc0, yc1 = yf, yf + .07
    box(body, T, -.63, .63, yc0, yc1, -.63, .63, iron, .015)
    n8 = 8
    ph = math.pi / 8                     # flats on the axes, hips on the diagonals and between them
    yd = yc1 + .055
    prof = [(.5, yd), (.53, yd + .025), (.44, yd + .11), (.26, yd + .25), (.06, yd + .38)]
    if T.lod > 0:
        lathe(body, [(.5 * K8, yc1), (.5 * K8, yd)], (0, 0, 0), iron, n8, smooth=False, phase=ph)
        lathe(body, [(.53 * K8, yd), (.06 * K8, yd + .38), (.001, yd + .5)], (0, 0, 0), iron, n8, smooth=False, phase=ph)
        return _kiosk_done(m)
    lathe(body, [(.5 * K8, yc1), (.5 * K8, yd)], (0, 0, 0), iron, n8, smooth=False, phase=ph)
    # (the profile's first step runs out and up: the lip's underside, seen from the pavement)
    lathe(body, [(r * K8, y) for (r, y) in prof], (0, 0, 0), iron, n8, smooth=False, phase=ph)
    # the rolled hips: a ridge up each hip (the octagon's corners) above the lip, catching the highlight band
    for kk in range(n8):
        a = ph + 2 * math.pi * kk / n8
        d = (math.sin(a), 0.0, math.cos(a))
        side = (math.cos(a), 0.0, -math.sin(a))
        for i in range(1, len(prof) - 1):
            (r0, y0), (r1, y1) = prof[i], prof[i + 1]
            p0 = (d[0] * r0 * K8, y0, d[2] * r0 * K8)
            p1 = (d[0] * r1 * K8, y1, d[2] * r1 * K8)
            t = sk._norm(sk._sub(p1, p0))
            out = sk._norm(sk._cross(side, t))
            if sk._dot(out, d) < 0:
                out = mk.scale(out, -1)
            w0, w1 = .02, .015
            q0, q1 = add(p0, out, .014), add(p1, out, .011)
            for s_ in (-1, 1):
                nrm = sk._norm(add(out, side, .9 * s_))
                body.face([add(p0, side, s_ * w0), q0, q1, add(p1, side, s_ * w1)], iron, nrm)
    # the finial
    yt2 = yd + .38
    lathe(body, [(.06, yt2 - .005), (.075, yt2 + .03), (.045, yt2 + .07), (.028, yt2 + .1), (.04, yt2 + .13), (.001, yt2 + .2)],
          (0, 0, 0), gilt, 6)
    # the cornice's corner caps
    for (x, z) in ((-.6, -.6), (.6, -.6), (-.6, .6), (.6, .6)):
        lathe(body, [(.045, yc1), (.001, yc1 + .07)], (x, 0, z), iron, 4, smooth=False, phase=math.pi / 4)
    # the long night: rust runs down two corner posts from the frieze (on +x, and on the cream back's side)
    for (o, nrm) in (((H + .0005, 0, -.5), (1, 0, 0)), ((-.5, 0, -(H + .0005)), (0, 0, -1))):
        k2.run(body, Fr(o, nrm), 0.0, yt - .002, .3, .022, 0.0, F['rust'], drift=.004)
    return _kiosk_done(m)


def _kiosk_done(m):
    m.light((0.0, 1.85, 0.0), mk.TUNGSTEN, 4.0, kind='practical', note='the kiosk lit inside')
    return m
