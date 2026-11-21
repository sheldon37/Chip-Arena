"""Meridian's own street pieces, pass 2 (art bible 1.3, 2.4, 3.4, 6.7): the Meridian Time Service's street clock on its
column, stopped at 6:47 (unlit in the neutral town; lit opal in the Station's drift); the Civil Defence air-raid siren on its
pole; and the street-name plates (cream on green enamel), on a wall or on their own two legs.

Frames: see mkit.py. Free-standing pieces stand on their block's middle, +z toward the road; the clock's two dials face +x and
-x (up and down the street). Wall plates: origin on the wall's face at the plate's middle, +z out of the wall.
"""
import math
import random

import kit2 as k2
from kit2 import F, Fr, Module, add, rot_y, lathe, tube, rod, box, fbox, quad, poly
import mkit as mk
import stagekit as sk

K8 = 1 / math.cos(math.pi / 8)


# ---------------------------------------------------------------------------------------------------------------------------
# The street clock column (bible 6.7: a 0.6 m double face on a fluted post, reading 6:47)
# ---------------------------------------------------------------------------------------------------------------------------
def _fluted(g, T, y0, y1, r_out, r_in, f, taper=.85, flutes=8):
    """A fluted shaft: a star section of 2 x flutes points swept up from y0 to y1, tapering to `taper`."""
    sec = []
    for i in range(2 * flutes):
        a = 2 * math.pi * i / (2 * flutes)
        r = r_out if i % 2 == 0 else r_in
        sec.append((r * math.cos(a), r * math.sin(a)))
    mk.sweep(g, sec, [(0, y0, 0), (0, (y0 + y1) / 2, 0), (0, y1, 0)], f, caps=False, scale=[1.0, (1 + taper) / 2, taper], smooth_u=False)


def clock_column(T, lit=False):
    k2.finishes()
    name = 'furn.clock.column' + ('-lit' if lit else '')
    m = Module(name, T.lod, block='lamp- (0.3 m)', cap=(1500, 150),
               note='Meridian Time Service slave clock; dials face +x and -x at 3.72 m; %s' % (
                   'lit opal (the Station drift band and closer)' if lit else 'unlit (the neutral town: reads only in a lamp\'s pool)'))
    body, lamp = m.part('body'), m.part('lamp')
    green, gilt, cream = F['iron-green'], F['brass'], F['enamel-cream']
    n = T.pick(8, 6)
    # the octagonal pedestal with its mouldings (flats on the axes), the number plate on the road side
    prof = [(.26, 0.0), (.26, .06), (.235, .09), (.215, .12), (.215, .42), (.235, .45), (.2, .5), (.16, .56), (.125, .62)]
    if T.lod > 0:
        prof = [(.25, 0.0), (.215, .12), (.215, .45), (.125, .62)]
    lathe(body, [(r * K8, y) for r, y in prof], (0, 0, 0), green, n, smooth=False, phase=math.pi / n)
    if T.lod == 0:
        fr = Fr((0, 0, .215), (0, 0, 1))
        fbox(body, None, fr, -.08, .08, .22, .34, 0.0, .008, F['enamel-green'], 0.0, skip=('-z',))
        mk.text(body, fr, 0.0, .248, .0085, 'MTS 47', cream, c=.011)
    # the fluted column, its collar, the capital and the neck
    if T.lod == 0:
        _fluted(body, T, .62, 2.9, .085, .071, green)
        lathe(body, [(.072, 1.83), (.092, 1.85), (.092, 1.93), (.072, 1.95)], (0, 0, 0), green, 10)
        lathe(body, [(.07, 2.9), (.085, 2.94), (.125, 3.02), (.155, 3.07), (.155, 3.11), (.11, 3.14), (.07, 3.17)], (0, 0, 0), green, 10)
        # four cast leaves under the capital
        for kk in range(4):
            a = math.pi / 4 + kk * math.pi / 2
            nn = rot_y((0, 0, 1), a)
            rr = rot_y((-1, 0, 0), a)
            o = add((0, 2.97, 0), nn, .078)
            tip = add((0, 3.1, 0), nn, .165)
            body.face([add(o, rr, -.03), add(o, rr, .03), add(tip, rr, .018), add(tip, rr, -.018)], green, add(nn, (0, -.5, 0)))
        rod(body, (0, 3.16, 0), (0, 3.36, 0), .055, green, 8, caps=False)
        lathe(body, [(.055, 3.3), (.09, 3.33), (.07, 3.37)], (0, 0, 0), gilt, 8)
    else:
        rod(body, (0, .62, 0), (0, 3.36, 0), .085, green, 6, caps=False, r1=.07)
        lathe(body, [(.07, 2.9), (.155, 3.08), (.07, 3.17)], (0, 0, 0), green, 6)
    # the drum: a cast case round two dials back to back, its axis along x
    cy = 3.72
    c = (0.0, cy, 0.0)
    ax = (1.0, 0.0, 0.0)
    sides = T.pick(16, 8)
    case = [(.30, -.126), (.35, -.122), (.372, -.085), (.372, .085), (.35, .122), (.30, .126)] if T.lod == 0 else \
        [(.30, -.126), (.37, -.1), (.37, .1), (.30, .126)]
    lathe(body, case, c, green, sides, axis=ax)
    if T.lod == 0:
        # the gilt bezels, the case's crown ring and its foot where the neck meets it
        # (a lathe's faces look to the right of its profile's travel: outer to inner on the +x face, inner to outer on -x)
        lathe(body, [(.322, .127), (.31, .131), (.292, .127)], c, gilt, sides, axis=ax)
        lathe(body, [(.292, -.127), (.31, -.131), (.322, -.127)], c, gilt, sides, axis=ax)
        lathe(body, [(.06, cy - .375), (.11, cy - .36), (.13, cy - .34)], (0, 0, 0), green, 8)
    # the dials: opal at 6:47 (minute 282 deg, hour 203.5 deg), lit or not
    face = k2.glow(mk.OPAL, 2.2) if lit else F['opal']
    hands = F['ink']
    for s in (1, -1):
        fr = Fr((s * .126, cy, 0.0), (s, 0, 0))
        target = lamp if lit else body
        poly(target, fr, mk.ngon_pts(.3, sides, 0.0, 0.0, 0.0), 0.0, face)
        if T.lod == 0:
            for kk in range(12):
                ang = 2 * math.pi * kk / 12
                L = .3 * (.16 if kk % 3 == 0 else .09)
                mk.hand(body, fr, math.sin(ang) * (.3 * .84 - L), math.cos(ang) * (.3 * .84 - L), .003, ang, L, .3 * .045, hands)
            mk.hand(body, fr, 0.0, 0.0, .005, mk.HOUR_647, .3 * .5, .3 * .1, hands, tail=.03)
            mk.hand(body, fr, 0.0, 0.0, .007, mk.MINUTE_647, .3 * .76, .3 * .07, hands, tail=.04)
            mk.disc(body, fr.P(0, 0, .009), .018, gilt, 6, fr.n)
            mk.text(body, fr, 0.0, -.16, .0062, 'MERIDIAN', hands, c=.003)
        else:
            mk.hand(body, fr, 0.0, 0.0, .006, mk.HOUR_647, .15, .03, hands)
            mk.hand(body, fr, 0.0, 0.0, .008, mk.MINUTE_647, .23, .022, hands)
    # the cap: a gilt ring, an ogee crown and a finial (never a figure)
    if T.lod == 0:
        lathe(body, [(.1, cy + .355), (.13, cy + .37), (.11, cy + .41), (.065, cy + .46), (.035, cy + .53), (.022, cy + .6),
                     (.04, cy + .64), (.001, cy + .72)], (0, 0, 0), green, 8)
        mk.ellipsoid(body, (0, cy + .66, 0), ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.04, .04, .04), gilt, 8, 3, top=1.0, bottom=-1.0)
    else:
        lathe(body, [(.12, cy + .35), (.001, cy + .7)], (0, 0, 0), green, 6)
    if lit:
        m.light((.0, cy, .0), mk.OPAL, 3.0, kind='practical', note='the lit dials (Station drift)')
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# The Civil Defence air-raid siren on its pole (bible 6.7: above 3.1 m; livery 3.4: olive, hazard orange, stencil white)
# ---------------------------------------------------------------------------------------------------------------------------
def siren(T, height=11.0):
    k2.finishes()
    m = Module('furn.siren.cd', T.lod, block='lamp- (0.3 m)', cap=(1400, 140),
               note='Civil Defence siren No. 12, head at %.1f m; the control box on +x stands 0.1 m out of the block' % height)
    body = m.part('body')
    olive, orange, white = F['cd-olive'], F['cd-orange'], F['cd-white']
    n = T.pick(8, 6)

    def pr(y):
        return .085 + (.06 - .085) * y / height
    # the foot: a square flange and its bolts on the block
    box(body, T, -.18, .18, 0.0, .03, -.18, .18, F['iron-black'], .008, skip=('-y',))
    if T.lod == 0:
        for (x, z) in ((-.13, -.13), (.13, -.13), (-.13, .13), (.13, .13)):
            rod(body, (x, .03, z), (x, .07, z), .016, F['iron-black'], 6)
        lathe(body, [(.12, .03), (.1, .09), (pr(.12) + .004, .12)], (0, 0, 0), olive, n, smooth=False, phase=math.pi / n)
    # the pole: olive, a hazard band at 2.3-2.6 m (above the box), rings where the paint changes
    ys = [0.03, 2.3, 2.6, height] if T.lod == 0 else [0.03, height]
    for (y0, y1) in zip(ys[:-1], ys[1:]):
        f = orange if (y0 == 2.3) else olive
        lathe(body, [(pr(y0) * K8, y0), (pr(y1) * K8, y1)], (0, 0, 0), f, n, smooth=False, phase=math.pi / n)
    # step bolts, alternating, from 3.3 m (out of reach) to the top
    if T.lod == 0:
        y = 3.3
        s = 1
        while y < height - .45:
            r0 = pr(y)
            rod(body, (s * r0 * .95, y, 0.0), (s * (r0 + .14), y + .02, 0.0), .012, F['galv'], 4, caps=False)
            y += .38
            s = -s
    # the control box on +x (1.7-2.25 m), its door, the CD roundel, the stencil and the conduit up the pole
    fr = Fr((.07, 0, 0), (1, 0, 0))
    fbox(body, T, fr, -.15, .15, 1.7, 2.25, 0.0, .18, olive, .012)
    if T.lod == 0:
        frd = Fr((.25, 0, 0), (1, 0, 0))
        mk.disc(body, frd.P(0, 2.06, .003), .085, white, 12, frd.n)
        mk.crest(body, T, frd, 0, 2.06, .065, .007, white, olive, F['iron-black'], olive, sides=10)
        mk.text(body, frd, 0, 1.8, .0105, 'CD 12', white, c=.003)
        fbox(body, None, frd, .1, .13, 1.88, 1.95, 0.0, .02, F['galv'], 0.0, skip=('-z',))      # the hasp
        k2.run(body, frd, .115, 1.875, .16, .02, .001, F['rust'], drift=-.005)
        # the conduit up the pole's +x flat, and its clips
        tube(body, [(pr(2.25) + .018, 2.25, 0.0), (pr(height - .05) + .018, height - .05, 0.0)], .018, F['iron-black'], 4, caps=False)
        for y in (3.2, 4.7, 6.2, 7.5):
            xc = pr(y) + .018
            box(body, None, xc - .028, xc + .026, y - .03, y + .03, -.026, .026, F['galv'], 0.0)
    # the head: a mounting flange, the motor and its fins, the stator drum with its ports, the rain hood
    y0 = height
    lathe(body, [(pr(y0) * K8, y0), (.11, y0 + .01), (.11, y0 + .04)], (0, 0, 0), olive, n, smooth=False, phase=math.pi / n)
    ns = T.pick(12, 8)
    if T.lod == 0:
        lathe(body, [(.11, y0 + .04), (.15, y0 + .07), (.15, y0 + .33), (.13, y0 + .36)], (0, 0, 0), olive, ns)
        for kk in range(8):
            a = 2 * math.pi * kk / 8
            d = rot_y((0, 0, 1), a)
            side_ = rot_y((1, 0, 0), a)
            p0, p1 = add((0, y0 + .1, 0), d, .148), add((0, y0 + .1, 0), d, .2)
            p2, p3 = add((0, y0 + .3, 0), d, .2), add((0, y0 + .3, 0), d, .148)
            body.face([p0, p1, p2, p3], olive, side_)
            body.face([p3, p2, p1, p0], olive, mk.scale(side_, -1))
        # the stator: a drum whose side is 10 ports between posts, the ports dark and set in
        yb, yt = y0 + .4, y0 + .64
        lathe(body, [(.13, y0 + .36), (.31, y0 + .38), (.31, yb)], (0, 0, 0), olive, ns)
        lathe(body, [(.31, yt), (.31, yt + .02), (.15, yt + .05)], (0, 0, 0), olive, ns)
        nport = 10
        for kk in range(nport):
            a0 = 2 * math.pi * kk / nport
            a1 = 2 * math.pi * (kk + 1) / nport
            am = a0 + (a1 - a0) * .62
            ro, ri = .31, .27

            def P(a, r, y):
                return (r * math.sin(a), y, r * math.cos(a))
            # the post (olive), then the port (void, set in), its jambs and its sill and head
            body.face([P(a0, ro, yb), P(am, ro, yb), P(am, ro, yt), P(a0, ro, yt)], olive, rot_y((0, 0, 1), (a0 + am) / 2))
            body.face([P(am, ri, yb), P(a1, ri, yb), P(a1, ri, yt), P(am, ri, yt)], F['void'], rot_y((0, 0, 1), (am + a1) / 2))
            body.face([P(am, ri, yb), P(am, ro, yb), P(am, ro, yt), P(am, ri, yt)], olive, rot_y((0, 0, 1), am + math.pi / 2))
            body.face([P(a1, ro, yb), P(a1, ri, yb), P(a1, ri, yt), P(a1, ro, yt)], olive, rot_y((0, 0, 1), a1 - math.pi / 2))
            body.face([P(am, ri, yb), P(a1, ri, yb), P(a1, ro, yb), P(am, ro, yb)], olive, (0, 1, 0))
            body.face([P(am, ro, yt), P(a1, ro, yt), P(a1, ri, yt), P(am, ri, yt)], olive, (0, -1, 0))
            # rust run from the port's sill down the drum's foot
            if kk % 3 == 1:
                d = rot_y((0, 0, 1), (am + a1) / 2)
                frr = Fr(mk.scale(d, .312), d)
                k2.run(body, frr, 0.0, yb + .002, .1, .025, .0, F['rust'])
        # the rain hood (olive, an orange rim) and its lifting eye
        hy = yt + .05
        lathe(body, [(.37, hy - .02), (.37, hy + .01)], (0, 0, 0), orange, ns, smooth=False, cap0=True)
        lathe(body, [(.37, hy + .01), (.33, hy + .07), (.2, hy + .16), (.07, hy + .22), (.001, hy + .24)], (0, 0, 0), olive, ns)
        tube(body, [(-.04, hy + .21, 0), (-.035, hy + .27, 0), (0, hy + .3, 0), (.035, hy + .27, 0), (.04, hy + .21, 0)], .009,
             F['iron-black'], 4, caps=False)
        # a long rust run down the pole from the flange, on the -x flat
        frp = Fr((-pr(y0 - 1.2) - .001, 0, 0), (-1, 0, 0))
        k2.run(body, frp, 0.0, y0 - .03, 1.4, .035, 0.0, F['rust'], drift=.01)
        mk.text_on_drum(body, (0, 0, 0), .152, y0 + .17, .009, 'CD 12', white, 0.0, lift=.002)
    else:
        lathe(body, [(.15, y0 + .04), (.15, y0 + .36), (.31, y0 + .4), (.31, y0 + .66), (.37, y0 + .69), (.001, y0 + .93)],
              (0, 0, 0), olive, ns)
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# Street-name plates (bible 3.4 and 6.7: cream on green enamel; the municipal livery)
# ---------------------------------------------------------------------------------------------------------------------------
PX = .017          # the plate's letter cell: 0.12 m capitals, read across a street


def _plate(g, T, fr, text, district, c0, r, both=False):
    """The enamel plate on a frame's face (its back at c0): a chamfered slab, the cream border, the name, the district code,
    four screws, chips. Returns (half length, half height)."""
    green, cream = F['enamel-green'], F['enamel-cream']
    tw = mk.text_width(text, PX)
    dw = mk.text_width(district, PX * .62) if district else 0.0
    hl = tw / 2 + .09 + (dw + .1 if district else 0.0) / 2
    hh = .16
    t = .014
    ch = .03
    outline = [(-hl + ch, -hh), (hl - ch, -hh), (hl, -hh + ch), (hl, hh - ch), (hl - ch, hh), (-hl + ch, hh), (-hl, hh - ch), (-hl, -hh + ch)]
    for i in range(len(outline)):
        p, q = outline[i], outline[(i + 1) % len(outline)]
        mid = ((p[0] + q[0]) / 2, (p[1] + q[1]) / 2)
        g.face([fr.P(p[0], p[1], c0), fr.P(q[0], q[1], c0), fr.P(q[0], q[1], c0 + t), fr.P(p[0], p[1], c0 + t)], green, fr.D(mid[0], mid[1], 0))
    poly(g, fr, outline, c0 + t, green)
    if both:
        g.face([fr.P(p[0], p[1], c0) for p in outline], green, mk.scale(fr.n, -1))
    if T.lod > 0:
        return hl, hh
    cf = c0 + t + .0015
    # the cream border line
    bi, bw = .022, .011
    for (a0, a1, b0, b1) in ((-hl + bi, hl - bi, -hh + bi, -hh + bi + bw), (-hl + bi, hl - bi, hh - bi - bw, hh - bi),
                             (-hl + bi, -hl + bi + bw, -hh + bi + bw, hh - bi - bw), (hl - bi - bw, hl - bi, -hh + bi + bw, hh - bi - bw)):
        quad(g, fr, a0, a1, b0, b1, cf, cream)
    ax = -hl + .09 + tw / 2 if district else 0.0
    mk.text(g, fr, ax, -7 * PX / 2, PX, text, cream, c=cf)
    if district:
        xr = hl - .05 - dw / 2
        quad(g, fr, xr - dw / 2 - .05, xr - dw / 2 - .05 + bw, -hh + bi + bw, hh - bi - bw, cf, cream)
        mk.text(g, fr, xr, -7 * PX * .62 / 2, PX * .62, district, cream, c=cf)
    # screws, and the enamel chipped to black iron round two of them and at a corner
    for (sa, sb) in ((-hl + .045, hh - .045), (hl - .045, hh - .045), (-hl + .045, -hh + .045), (hl - .045, -hh + .045)):
        mk.disc(g, fr.P(sa, sb, cf + .001), .011, F['galv'], 6, fr.n)
    for (sa, sb, rr) in ((-hl + .045, -hh + .045, .022), (hl - .1, hh - .03, .018), (-hl + .012, hh - .06, .014)):
        pts = [(sa + rr * math.cos(a) * r.uniform(.6, 1.2), sb + rr * math.sin(a) * r.uniform(.6, 1.2)) for a in
               (2 * math.pi * i / 6 for i in range(6))]
        poly(g, fr, pts, cf - .0008, F['iron-black'])
    return hl, hh


def name_plate(T, text='ALBION ROW', district='N4', seed=1):
    """A wall plate: origin on the wall's face at the plate's middle (the dressing sets its height, 2.4-3.0 m)."""
    k2.finishes()
    m = Module('sign.street.wall.' + text.lower().replace(' ', '-'), T.lod, mount='wall', rest=(0.0, 2.7, 0.0),
               block='a facade, on its face (the 0.25 m rule)', cap=(300, 30), note='"%s %s": text from the dressing sheet' % (text, district))
    g = m.part('body')
    r = random.Random(seed)
    fr = Fr((0, 0, 0), (0, 0, 1))
    hl, hh = _plate(g, T, fr, text, district, .006, r)
    if T.lod == 0:
        # the long night: rust from the lower screws and the plate's foot run down the wall below it
        for (a, L) in ((-hl + .045, .42), (hl - .045, .3), (r.uniform(-hl * .5, hl * .5), .2)):
            k2.run(g, fr, a, -hh + .01, L, .022, .0012, F['rust'], drift=r.uniform(-.01, .01))
    return m


def name_plate_legs(T, text='KNOLL STREET', district='N2', seed=2):
    """The plate on its own two cast posts at the back of a pavement (a free-standing piece: it needs a barrier- block)."""
    k2.finishes()
    m = Module('sign.street.legs', T.lod, block='barrier- (1.6 x 0.2; Astra)', cap=(500, 50),
               note='"%s %s" both sides; plate 0.75-1.07 m' % (text, district))
    g = m.part('body')
    r = random.Random(seed)
    black, gilt = F['iron-black'], F['brass']
    tw = mk.text_width(text, PX)
    dw = mk.text_width(district, PX * .62)
    hl = tw / 2 + .09 + (dw + .1) / 2
    for s in (-1, 1):
        x = s * (hl + .035)
        box(g, T, x - .03, x + .03, 0.0, 1.16, -.03, .03, black, .008, skip=('-y',))
        if T.lod == 0:
            mk.ellipsoid(g, (x, 1.19, 0), ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.04, .04, .04), gilt, 6, 3, top=1.0, bottom=-.7)
            box(g, None, x - .045, x + .045, 0.0, .05, -.045, .045, black, 0.0, skip=('-y',))
    if T.lod > 0:
        box(g, None, -hl, hl, .75, 1.07, -.014, .014, F['enamel-green'], 0.0)
        return m
    for s in (1, -1):
        fr = Fr((0, .91, 0), (0, 0, s))
        _plate(g, T, fr, text, district, .0, r)
    if T.lod == 0:
        for s in (1, -1):
            fr = Fr((0, .91, 0), (0, 0, s))
            k2.run(g, fr, -hl * .3, -.16 + .003, .08, .02, .0158, F['rust'])
    return m
