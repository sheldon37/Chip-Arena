"""Meridian landmarks: the exit landmarks of the Stride and the Yards (registered into meridian_lm_exits.BUILDERS)."""
import math

import meridian_kit as mk
import meridian_lm_kit as lk
import stagekit as sk
from meridian_kit import F, Geo, bx, glow
from meridian_lm_kit import PI, WARM, ROOM, OPAL
from meridian_lm_exits import builder, X


# ===========================================================================================================================
# The Stride
# ===========================================================================================================================
@builder('arch-nought-clock')
def arch_nought_clock(t, S):
    """Arch Nought and Crag Road: under the viaduct's arch over the north way hangs the railway's double-faced bracket clock at
    6:47 on its iron yoke, two lamps either side, and the arch's face carries the cast 0 and the railway's crest."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    deck = S['viaduct-deck']          # y 6..7, z 45..57
    soffit = deck[2]
    zc = (deck[4] + deck[5]) / 2
    iron = F['iron-black']
    # the yoke from the soffit and the clock's drum
    mk.seg(g, T, (-1.6, soffit - .02, zc), (1.6, soffit - .02, zc), .12, .12, iron)
    for sgn in (-1, 1):
        mk.seg(g, T, (sgn * 1.2, soffit - .02, zc), (sgn * .2, 5.85, zc), .07, .07, iron)
    mk.rod(g, (0.0, 5.85, zc), (0.0, soffit, zc), .05, iron, 6)
    R = 1.2
    sk.lathe(g, [(R + .14, -.24), (R + .14, .24)], (0.0, 4.6, zc), F['cast-green'], T.round + 6, axis=(0, 0, 1), smooth=True)
    for sgn in (-1, 1):
        sub = Geo()
        mk.clock(sub, T, R, glow(OPAL, 1.4), F['brass'], F['iron-black'], F['iron-black'], roman=True, depth=.05, rim=.1)
        mk.part(g, sub, mk.qy(0 if sgn < 0 else 180), (0.0, 4.6, zc + sgn * .24))
    sk.lathe(g, [(.3, 5.8), (.12, 5.95)], (0.0, 0.0, zc), F['brass'], 8, smooth=False)
    # the lamps hung either side under the arch
    for x in (-6.0, 6.0):
        mk.rod(g, (x, soffit, zc), (x, 5.2, zc), .03, iron, 4, caps=False)
        lk.lantern(g, T, x, 5.2, zc, s=.85, hang=0.0)
    # the arch's face: the cast 0 in its roundel-less plaque and the crest, on the deck's south edge (relief within 0.12 m)
    zf = deck[4]
    bx(g, T, -1.2, 1.2, 6.08, 6.95, zf - .1, zf, F['cast-maroon'], bevel=.02, skip=('+z',))
    mk.text_stroke(g, '0', 0.0, 6.2, zf - .1, .1, E['gilt'], T, style='cast', w=.08, d=.03)
    for sgn in (-1, 1):
        bx(g, T, sgn * 4.0 - .55, sgn * 4.0 + .55, 6.1, 6.92, zf - .08, zf, E['gilt'], bevel=.02, skip=('+z',))
    return g


@builder('deck-hoarding')
def deck_hoarding(t, S):
    """The deck stair: on the deck approach's side a great lit railway hoarding in a lattice frame, MERIDIAN & VALE and the 6:47's
    engine painted large, two goose-neck lamps over it."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    box = S['terrace-41']            # x 37..43, z 17..54, top 7
    xf = box[0]
    z0, z1 = 21.0, 33.0
    y0, y1 = 3.4, 7.6
    iron = E['iron-lattice']
    # the frame: a lattice box standing off the face by 0.2 m
    x = xf - .22

    def canonical(s):
        w = z1 - z0
        # the poster: a lit paper face (it glows softly under its lamps)
        paper = glow(0xE8DCC0, .55)
        s.face(((0.0, y0, 0.0), (w, y0, 0.0), (w, y1, 0.0), (0.0, y1, 0.0)), paper, (0, 0, -1))
        ink = glow(0x6A2626, .5)
        sky = glow(0x2A3A66, .45)
        s.face(((.4, y0 + 2.2, -.005), (w - .4, y0 + 2.2, -.005), (w - .4, y1 - .3, -.005), (.4, y1 - .3, -.005)), sky, (0, 0, -1))
        # the engine: a black boiler, a funnel, wheels, and the 6:47's red buffer beam
        k = glow(0x202024, .4)
        s.face(((2.0, y0 + .6, -.01), (8.6, y0 + .6, -.01), (8.6, y0 + 2.0, -.01), (2.0, y0 + 2.0, -.01)), k, (0, 0, -1))
        s.face(((7.2, y0 + 2.0, -.01), (8.6, y0 + 2.0, -.01), (8.6, y0 + 3.0, -.01), (7.2, y0 + 3.0, -.01)), k, (0, 0, -1))
        s.face(((3.0, y0 + 2.0, -.01), (3.6, y0 + 2.0, -.01), (3.6, y0 + 2.8, -.01), (3.0, y0 + 2.8, -.01)), k, (0, 0, -1))
        s.face(((1.7, y0 + .55, -.012), (2.1, y0 + .55, -.012), (2.1, y0 + 1.3, -.012), (1.7, y0 + 1.3, -.012)), glow(0xC02828, .7), (0, 0, -1))
        for cx in (3.2, 5.0, 6.8):
            mk.disc(s, cx, y0 + .6, -.012, .55, 10 if T.detail else 6, k, normal=(0, 0, -1))
        mk.text_stroke(s, 'MERIDIAN & VALE', w * .62, y0 + 2.5, -.01, .1, ink, T, style='flat', w=.08, d=.002)
        mk.text_stroke(s, 'THE STRIDE LINE', w * .62, y0 + 1.2, -.01, .07, ink, T, style='flat', w=.055, d=.002)
        # the frame's moulded edge and its lattice back
        for (a, b) in (((0, y0), (w, y0)), ((0, y1), (w, y1)), ((0, y0), (0, y1)), ((w, y0), (w, y1))):
            mk.seg(s, T, (a[0], a[1], -.04), (b[0], b[1], -.04), .14, .1, iron)
        for u in (0.0, 4.0, 8.0, w):
            mk.seg(s, T, (u, y0, .02), (u, y1, .02), .1, .1, iron)
        # the goose-neck lamps on the frame's top, out over the poster
        for u in (w * .25, w * .75):
            pts = [(u, y1, .05), (u, y1 + .6, .05), (u, y1 + .9, -.35), (u, y1 + .85, -.9)]
            for p, q in zip(pts[:-1], pts[1:]):
                mk.rod(s, p, q, .04, iron, 5, caps=False)
            sk.lathe(s, [(.05, 0), (.32, -.18), (.34, -.25)], (u, y1 + .9, -.95), F['iron-green'], T.small + 2, smooth=False)
            mk.disc(s, u, y1 + .65, -.95, .3, T.small + 2, glow(0xFFE8B0, 2.4), normal=(0, -1, 0))
    lk.placed(g, canonical, 90.0, (x, 0.0, z1))
    return g


@builder('vale-wheel')
def vale_wheel(t, S):
    """The Vale haul ramp: the winding house at the ramp's foot, brick with tall lit windows, and its great spoked winding wheel
    held out from the face on a girder frame over the yard, a lamp at its top."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    box = S['lot-vale-c-109']        # x -30..-10, z -75..-60, -7..9
    x0, x1, y0, y1, z0, z1 = box
    brick = F['brick-stock']
    seed = 13322

    def face(sub, side, w):
        wins = []
        bays = max(3, int(w / 3.4))
        bw = w / bays
        for i in range(bays):
            cx = (i + .5) * bw
            wins.append((cx - .65, cx + .65, y0 + 1.0, y0 + 6.2, lk.lit_or_dark(seed, i + 7 * ord(side), .5, gain=1.3), .65))
            wins.append((cx - .5, cx + .5, y0 + 8.4, y0 + 10.6, lk.lit_or_dark(seed, i + 7 * ord(side) + 40, .3), .3))
        lk.facade(sub, T, -.02, 0.0, w, y0, y1, brick, wins, f_frame=F['paint-black'], reveal=.22, flat=side in ('s', 'w'))
        lk.string_course(sub, T, y0 + 7.4, 0.0, w, -.02, F['stone-dress'], h=.28, d=.1)
        lk.cornice(sub, T, y1, 0.0, w, -.02, F['stone-dress'], s=.9)
        bx(sub, T, 0.0, w, y0, y0 + .8, -.07, -.02, F['brick-blue'], bevel=.01, skip=('+z',))
    lk.skin(g, box, face)
    lk.roof_cap(g, T, box, F['slate'])
    # the wheel: rim, spokes, hub, its axle into a bearing frame off the east face (all 6 m over the yard)
    iron, red = E['iron-lattice'], F['paint-oxblood']
    cx, cy, cz = x1 + 2.4, 3.6, -65.0
    R = 4.0
    n = 32 if T.detail else 16
    rim0 = [(cx, cy + R * math.sin(2 * PI * i / n), cz + R * math.cos(2 * PI * i / n)) for i in range(n + 1)]
    for p, q in zip(rim0[:-1], rim0[1:]):
        mk.seg(g, T, p, q, .3, .22, red, up=(1, 0, 0))
    spokes = 12 if T.detail else 8
    for k in range(spokes):
        a = 2 * PI * k / spokes
        mk.seg(g, T, (cx, cy, cz), (cx, cy + (R - .1) * math.sin(a), cz + (R - .1) * math.cos(a)), .1, .14, red)
    sk.lathe(g, [(.5, -.3), (.5, .3)], (cx, cy, cz), iron, T.small + 4, axis=(1, 0, 0), cap0=True, cap1=True)
    mk.rod(g, (x1, cy, cz), (cx + .5, cy, cz), .14, iron, T.small + 2)
    # the bearing frame: girders from the face out to the axle, above and below it
    for (ya, za_) in ((cy + 2.6, cz - 3.0), (cy + 2.6, cz + 3.0), (cy - 2.4, cz)):
        mk.seg(g, T, (x1, ya, za_), (cx - .3, cy, cz), .22, .22, iron)
    for (ya, za_) in ((cy + 2.6, cz - 3.0), (cy + 2.6, cz + 3.0), (cy - 2.4, cz)):
        bx(g, T, x1, x1 + .2, ya - .4, ya + .4, za_ - .4, za_ + .4, iron, bevel=.02)
    lk.lantern(g, T, x1 + .9, 8.6, -64.0, s=.8, hang=0.0)
    mk.seg(g, T, (x1, 8.7, -64.0), (x1 + 1.0, 8.7, -64.0), .08, .08, F['iron-black'])
    return g


@builder('rialto-marquee')
def rialto_marquee(t, S):
    """The Valley Steps: the Rialto picture house, cream faience and red, its marquee of chaser bulbs out over the pavement with
    RIALTO in red neon, the name again in tall letters above and lit poster frames by the doors."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    box = S['lot-mid-131']           # x -90..-78, z 13..25, 0..13
    x0, x1, y0, y1, z0, z1 = box
    cream, red = E['cinema-cream'], E['cinema-red']
    seed = 13324

    def face(sub, side, w):
        if side == 's':
            lk.facade(sub, T, -.02, 0.0, w, y0, y1, cream, [(w / 2 - 2.2, w / 2 + 2.2, 0.0, 3.2, (ROOM, 1.4)),
                                                            (w / 2 - 3.0, w / 2 + 3.0, 6.6, 7.8, (WARM, 1.2))], f_frame=F['brass'])
            # the Deco fins and the stepped crest in relief (within the face's top)
            for u in (1.2, w - 2.0):
                bx(sub, T, u, u + .8, 3.6, 12.6, -.25, -.02, red, bevel=.03, skip=('+z',))
                for k in range(3):
                    bx(sub, T, u + .1 * k, u + .8 - .1 * k, 12.6 + .12 * k, 12.72 + .12 * k, -.22, -.02, red, bevel=.01, skip=('+z',))
            mk.text_stroke(sub, 'RIALTO', w / 2, 8.6, -.03, .26, glow(0xFF3448, 3.0), T, style='neon', w=.11, standoff=.08)
            for u in (1.0, w - 1.0):
                sub.face(((u - .55, .8, -.06), (u + .55, .8, -.06), (u + .55, 2.6, -.06), (u - .55, 2.6, -.06)), glow(0xF0D8A0, 1.1), (0, 0, -1))
                for (a, b) in (((u - .6, .75), (u + .6, .75)), ((u - .6, 2.65), (u + .6, 2.65)), ((u - .6, .75), (u - .6, 2.65)), ((u + .6, .75), (u + .6, 2.65))):
                    mk.seg(sub, T, (a[0], a[1], -.05), (b[0], b[1], -.05), .06, .06, F['brass'])
        else:
            wins = []
            bays = max(2, int(w / 3.6))
            for i in range(bays):
                cx = (i + .5) * w / bays
                wins.append((cx - .5, cx + .5, 7.0, 9.0, lk.lit_or_dark(seed, i + 9 * ord(side), .25), .25))
            lk.facade(sub, T, -.02, 0.0, w, y0, y1, F['brick-stock'], wins, f_frame=F['paint-cream'], flat=True)
        lk.cornice(sub, T, y1, 0.0, w, -.02, cream, s=.7)
        bx(sub, T, 0.0, w, 0.0, .6, -.06, -.02, red, bevel=.01, skip=('+z',))
    lk.skin(g, box, face)
    lk.roof_cap(g, T, box, F['tar-felt'])
    # the marquee: a canted box out over the pavement (3.6 m up), bulbs round its faces, RIALTO on its front
    zf = z0
    xa, xb = x0 + 1.5, x1 - 1.5
    ya, yb = 3.7, 5.3
    zo = zf - 3.0
    bx(g, T, xa, xb, ya, ya + .25, zo, zf, red, bevel=.03)
    g.face(((xa, ya + .25, zo), (xb, ya + .25, zo), (xb, yb, zo + .5), (xa, yb, zo + .5)), cream, (0, .3, -.95))
    for x in (xa, xb):
        sgn = -1 if x == xa else 1
        g.face(((x, ya + .25, zo), (x, yb, zo + .5), (x, yb, zf), (x, ya + .25, zf)), cream, (sgn, 0, 0))
    g.face(((xa, yb, zo + .5), (xb, yb, zo + .5), (xb, yb, zf), (xa, yb, zf)), F['tar-felt'], (0, 1, 0))
    for zz in (zo + .6, zf - .3):
        lk.scroll_bracket(g, T, (xa + .3, yb + 1.5, zf - .02), (xa + .3, yb + 1.5, zz), F['iron-black'], w=.06, curl=.2) if False else None
    mk.text_stroke(g, 'RIALTO', (xa + xb) / 2, ya + .55, zo + .1, .14, glow(0xFF3448, 3.2), T, style='neon', w=.08, standoff=.06)
    bulb = glow(0xFFD27A, 2.6)
    if True:
        step = .32 if T.detail else .64
        k = int((xb - xa) / step)
        for i in range(k + 1):
            x = xa + i * (xb - xa) / k
            for (yy, zz) in ((ya + .13, zo - .02), (yb - .05, zo + .45)):
                mk.disc(g, x, yy, zz, .06, 6 if T.detail else 4, bulb, normal=(0, 0, -1))
        kz = int((zf - zo) / step)
        for i in range(kz + 1):
            zz = zo + i * (zf - zo) / kz
            for x, sgn in ((xa - .01, -1), (xb + .01, 1)):
                mk.disc(g, x, ya + .13, zz, .06, 6 if T.detail else 4, bulb, normal=(sgn, 0, 0))
    # the marquee's tie rods to the face above (all over 5 m)
    for x in (xa + .5, xb - .5):
        mk.rod(g, (x, yb, zo + .6), (x, yb + 2.0, zf), .03, F['iron-black'], 4, caps=False)
    return g


# ===========================================================================================================================
# The Yards
# ===========================================================================================================================
@builder('gantry-semaphores')
def gantry_semaphores(t, S):
    """The Signal Gantry: two lattice signal posts on the gantry's outer sides carry semaphore arms and their red and green
    spectacle lamps high over the yard; the junction's signal box beside it, brick under a glazed lever room."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    gy = S['signal-gantry']           # x -4..4, y 6..7, z 70..130
    iron = E['iron-lattice']
    z = gy[4] + 3.0
    for sgn in (-1, 1):
        x = sgn * (gy[1] + .22)
        # the post: a tapered lattice of four angles, 3.6 m to 16 m, clamped to the gantry's side
        for (dx, dz) in ((-.16, -.16), (.16, -.16), (.16, .16), (-.16, .16)):
            mk.seg(g, T, (x + dx * 1.2, 3.6, z + dz * 1.2), (x + dx * .7, 16.0, z + dz * .7), .05, .05, iron)
        if T.detail:
            for k in range(14):
                y = 3.8 + k * .88
                mk.seg(g, T, (x - .17, y, z - .17), (x + .17, y + .44, z + .17), .03, .03, iron)
        bx(g, T, min(x, sgn * gy[1]), max(x, sgn * gy[1]), 5.6, 7.2, z - .3, z + .3, iron, bevel=.01)
        sk.lathe(g, [(.25, 16.0), (.25, 16.15), (.05, 16.6)], (x, 0.0, z), F['iron-black'], 6, smooth=False)
        # two arms: home (red with a white band) at 13.4 m, distant (yellow with a black chevron) at 11.2 m
        for (y, fa, lamp) in ((13.4, F['enamel-red'], 0xFF3A2E), (11.2, F['paint-yellow'], 0x38E06C)):
            ax = x + sgn * .25
            bx(g, T, min(ax, ax + sgn * 1.8), max(ax, ax + sgn * 1.8), y - .14, y + .14, z - .2, z - .14, fa, bevel=.01)
            if T.detail:
                bx(g, T, min(ax + sgn * 1.2, ax + sgn * 1.35), max(ax + sgn * 1.2, ax + sgn * 1.35), y - .15, y + .15, z - .21, z - .19, F['paint-cream'], bevel=0, cell=0)
            # the spectacle: a lit lens on the arm's root
            mk.disc(g, ax + sgn * .05, y - .45, z - .25, .24, T.small + 4, glow(lamp, 3.2), normal=(0, 0, -1))
            bx(g, T, min(ax, ax + sgn * .3), max(ax, ax + sgn * .3), y - .75, y - .15, z - .22, z - .17, F['iron-black'], bevel=0, cell=0)
        # a ladder up the post's face
        if T.detail:
            for k in range(30):
                y = 4.0 + k * .4
                mk.seg(g, T, (x - .2, y, z + .22), (x + .2, y, z + .22), .025, .025, F['iron-black'])
    # the signal box (its block's faces): engineering brick under a glazed lever room, a bracketed eave, the name board
    box = S['signal-box']             # x 56..66, z 86..94, 0..10
    seed = 16061

    def face(sub, side, w):
        lk.facade(sub, T, -.02, 0.0, w, 0.0, 5.6, F['brick-stock'], [(w / 2 - .5, w / 2 + .5, 1.6, 3.6, None, .3)], f_frame=F['paint-cream'])
        # the lever room: lit panes in a timber frame
        panes = max(3, int(w / 1.1))
        for i in range(panes):
            a = i * w / panes
            sub.face(((a + .06, 5.9, -.08), (a + w / panes - .06, 5.9, -.08), (a + w / panes - .06, 8.9, -.08), (a + .06, 8.9, -.08)),
                     glow(0xFFD08A, 1.4) if mk.h01(seed, i, ord(side)) < .8 else F['glass-dark'], (0, 0, -1))
            mk.seg(sub, T, (a, 5.6, -.05), (a, 9.4, -.05), .1, .1, F['paint-maroon'])
        bx(sub, T, 0.0, w, 5.6, 5.9, -.12, -.02, F['paint-maroon'], bevel=.01, skip=('+z',))
        bx(sub, T, 0.0, w, 8.9, 9.4, -.1, -.02, F['paint-maroon'], bevel=.01, skip=('+z',))
        # the eave: a deep timber overhang at the top (over 3.1 m)
        bx(sub, T, -.6, w + .6, 9.4, 10.0, -.9, 0.0, F['paint-cream'], bevel=.02)
        if side == 'w':
            bx(sub, T, w / 2 - 2.2, w / 2 + 2.2, 4.6, 5.3, -.08, -.02, F['paint-maroon'], bevel=.01)
            mk.text_stroke(sub, 'JUNCTION', w / 2, 4.75, -.08, .06, F['paint-cream'], T, style='flat', w=.05, d=.004)
    lk.skin(g, box, face)
    lk.roof_cap(g, T, box, F['slate'])
    return g


@builder('coaling-tower')
def coaling_tower(t, S):
    """The sidings incline's landmark: the yard's 32 m coaling tower in board-marked concrete, its hopper banded, a skip hoist up
    its west face, coal chutes out over the tracks, COAL in white, its control cabin's windows lit and a red lamp at the top."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    box = S['coaling-tower']          # x 150..166, z 30..46, -7..25
    x0, x1, y0, y1, z0, z1 = box
    conc = E['concrete-coal']

    def face(sub, side, w):
        lk.facade(sub, T, -.02, 0.0, w, y0, y1, conc, [], f_frame=F['steel-dark'])
        # the hopper's bands and the board marks
        for y in (y0 + 6.5, y0 + 14.0, y0 + 22.0):
            bx(sub, T, -.02, w + .02, y, y + .6, -.24, -.02, conc, bevel=.03, skip=('+z',))
        if T.detail:
            for y in range(int(y0) + 1, int(y1), 1):
                bx(sub, T, 0.0, w, y, y + .04, -.035, -.02, F['concrete-dark'], bevel=0, cell=0)
        # the control cabin's windows (lit) under the top
        for i in range(5):
            a = 1.0 + i * (w - 2.0) / 5
            sub.face(((a + .1, y1 - 4.6, -.03), (a + (w - 2.0) / 5 - .1, y1 - 4.6, -.03), (a + (w - 2.0) / 5 - .1, y1 - 2.6, -.03), (a + .1, y1 - 2.6, -.03)),
                     glow(0xFFC883, 1.5) if (i + ord(side)) % 3 else F['glass-dark'], (0, 0, -1))
        lk.cornice(sub, T, y1, 0.0, w, -.02, conc, s=.8)
        if side == 'w':
            mk.text_stroke(sub, 'COAL', w / 2, y0 + 15.2, -.03, .55, F['paint-line'], T, style='flat', w=.45, d=.005)
            # the skip hoist's rails up the face (within 0.2 m) and the skip near the top
            for u in (w / 2 - 1.2, w / 2 + 1.2):
                bx(sub, T, u - .1, u + .1, y0, y1 - 1.0, -.2, -.02, F['steel-dark'], bevel=0, cell=0)
            bx(sub, T, w / 2 - 1.4, w / 2 + 1.4, y1 - 7.5, y1 - 5.6, -1.6, -.2, F['steel-dark'], bevel=.03)
    lk.skin(g, box, face)
    lk.roof_cap(g, T, box, conc)
    # the chutes out over the tracks on the north and south faces (from 1 m up: over 3.1 m from the yard at -7)
    for (zf, sgn) in ((z0, -1), (z1, 1)):
        for xc in (153.5, 162.5):
            p0 = (xc, y0 + 9.0, zf)
            p1 = (xc, y0 + 6.6, zf + sgn * 3.2)
            mk.seg(g, T, p0, p1, .9, .5, F['steel-dark'])
            mk.seg(g, T, (xc, y0 + 10.0, zf), (xc, y0 + 8.4, zf + sgn * 2.4), .08, .08, F['iron-black'])
    # the red lamp at the top corner, out over the face
    mk.seg(g, T, (x0 + 1.0, y1 - .3, z0), (x0 + 1.0, y1 - .3, z0 - 1.0), .08, .08, F['iron-black'])
    mk.ellipsoid(g, (x0 + 1.0, y1 - .55, z0 - 1.0), (.28, .28), glow(0xFF3A28, 3.2), T.small + 2, rings=3)
    return g


@builder('footbridge-gable')
def footbridge_gable(t, S):
    """The covered footbridge: its cover ends in a timber gable over the stair's head, bargeboards with a fretted valance, a lit
    lancet in the gable and a railway lamp on a bracket out over the stair, all over 10 m."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    lid = S['s-lid-4']                # x -3.5..3.5, y 10.4..10.9, z -74..-48
    x0, x1 = lid[0] - .1, lid[1] + .1
    zb = lid[5] + .2                  # 0.2 m off the cover's north end (-48)
    zf = zb + .9
    yb, yp = 10.15, 12.9
    cream, maroon = F['paint-cream'], F['paint-maroon']
    cx = (x0 + x1) / 2
    # the gable's boarding (both faces) and its sides
    pts = [(x0, yb), (x1, yb), (x1, 11.2), (cx, yp), (x0, 11.2)]
    g.face([(p[0], p[1], zf) for p in pts], cream, (0, 0, 1))
    g.face([(p[0], p[1], zb + .02) for p in pts][::-1], cream, (0, 0, -1))
    if T.detail:
        for i in range(1, 14):
            x = x0 + i * (x1 - x0) / 14
            ytop = 11.2 + (yp - 11.2) * (1 - abs(x - cx) / (cx - x0))
            bx(g, T, x - .015, x + .015, yb + .25, ytop - .1, zf, zf + .02, F['timber-oak'], bevel=0, cell=0)
    # the bargeboards and the finial
    for sgn in (-1, 1):
        a = (cx + sgn * (x1 - cx + .25), 11.05)
        mk.seg(g, T, (a[0], a[1], zf + .08), (cx, yp + .2, zf + .08), .32, .1, maroon)
        mk.seg(g, T, (a[0], a[1], zb + .1), (cx, yp + .2, zb + .1), .3, .1, maroon)
        g.face(((a[0], a[1] + .15, zb + .05), (cx, yp + .35, zb + .05), (cx, yp + .35, zf + .1), (a[0], a[1] + .15, zf + .1)), F['slate'], (sgn * .55, .83, 0))
    sk.lathe(g, [(.08, yp + .2), (.08, yp + .6), (.02, yp + 1.0)], (cx, 0.0, zf), maroon, 6, smooth=False)
    # the valance: a fretted edge along the gable's foot
    k = 24 if T.detail else 8
    for i in range(k):
        x = x0 + i * (x1 - x0) / k
        w = (x1 - x0) / k
        g.face(((x, yb + .25, zf + .03), (x + w, yb + .25, zf + .03), (x + w / 2, yb, zf + .03)), maroon, (0, 0, 1))
    bx(g, T, x0, x1, yb + .25, yb + .42, zb, zf + .06, maroon, bevel=.01)
    # the lit lancet in the gable
    h = mk.Hole(cx - .35, cx + .35, 10.95, 11.8, .35)
    lk.window(g, T, cx - .35, cx + .35, 10.95, 11.8, zf + .03, (0xFFD08A, 1.7), F['paint-black'], rise=.35)
    # the lamp on its bracket out over the stair
    lk.scroll_bracket(g, T, (cx, 11.3, zf + .04), (cx, 11.3, zf + 1.1), F['iron-black'], w=.07, curl=.18) if False else \
        mk.seg(g, T, (cx + 1.2, 11.35, zf + .05), (cx + 1.2, 11.35, zf + 1.0), .07, .07, F['iron-black'])
    lk.lantern(g, T, cx + 1.2, 11.33, zf + .95, s=.75, hang=0.0)
    return g


@builder('water-tower')
def water_tower(t, S):
    """The High Yard haul ramp's landmark: the yard's water tower, a brick base with blind arches under a sectional cast-iron tank
    with its bolted flanges, a ladder up its east face, MR in white and a lamp at the tank's foot."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    box = S['water-tower']            # x -120..-110, z -40..-30, 7..19
    x0, x1, y0, y1, z0, z1 = box
    yt = y1 - 4.4

    def face(sub, side, w):
        lk.facade(sub, T, -.02, 0.0, w, y0, yt, F['brick-stock'], [(w / 2 - .7, w / 2 + .7, y0, y0 + 2.4, None, .5)] if side == 'e' else [],
                  f_frame=F['paint-black'])
        # blind arches in the base (relief 0.15)
        if T.detail:
            for u in (1.2, w - 2.6):
                sub.face(((u, y0 + 3.0, -.0), (u + 1.4, y0 + 3.0, -.0), (u + 1.4, y0 + 6.2, -.0), (u, y0 + 6.2, -.0)), F['brick-soot'], (0, 0, -1))
        bx(sub, T, -.05, w + .05, yt - .3, yt, -.25, -.02, F['stone-dress'], bevel=.02, skip=('+z',))
        # the tank: plates with bolted flanges
        mk.wall_rect(sub, T, 0.0, w, yt, y1, -.04, F['iron-green'], n=-1, cell=1.5)
        for u in range(1, int(w / 1.25)):
            bx(sub, T, u * 1.25 - .05, u * 1.25 + .05, yt, y1 - .05, -.16, -.04, F['iron-green'], bevel=0, cell=0)
        for y in (yt + 1.47, yt + 2.94):
            bx(sub, T, 0.0, w, y - .05, y + .05, -.16, -.04, F['iron-green'], bevel=0, cell=0)
        bx(sub, T, -.1, w + .1, y1 - .2, y1, -.25, -.04, F['iron-green'], bevel=.01, skip=('+z',))
        if side == 'e':
            mk.text_stroke(sub, 'MR', w / 2, yt + .9, -.17, .3, F['paint-line'], T, style='flat', w=.24, d=.004)
            for u in (w / 2 + 2.6, w / 2 + 3.2):
                bx(sub, T, u - .03, u + .03, y0 + .1, y1 + .4, -.22, -.16, F['iron-black'], bevel=0, cell=0)
            for k in range(int((y1 - y0) / .35)):
                bx(sub, T, w / 2 + 2.6, w / 2 + 3.2, y0 + .3 + k * .35, y0 + .34 + k * .35, -.21, -.17, F['iron-black'], bevel=0, cell=0)
    lk.skin(g, box, face)
    lk.roof_cap(g, T, box, F['iron-green'])
    # the lamp at the tank's foot on the east face
    mk.seg(g, T, (x1, yt + 1.95, -35.0), (x1 + .7, yt + 1.95, -35.0), .08, .08, F['iron-black'])
    lk.lantern(g, T, x1 + .6, yt + 1.92, -35.0, s=.85, hang=0.0)
    return g
