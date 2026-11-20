"""Meridian landmarks: the exit landmarks of the Navigation and the Arcades (registered into meridian_lm_exits.BUILDERS)."""
import math

import meridian_kit as mk
import meridian_lm_kit as lk
import stagekit as sk
from meridian_kit import F, Geo, bx, glow
from meridian_lm_kit import PI, WARM, ROOM
from meridian_lm_exits import builder, X


# ===========================================================================================================================
# The Navigation
# ===========================================================================================================================
@builder('iron-bridge-arches')
def iron_bridge_arches(t, S):
    """The Iron Bridge: two riveted bowstring arches of lattice iron spring from the abutments outside the parapets, their hangers
    down to the deck's edges, braced overhead across the deck with the bridge's cast name plate and three hanging lamps."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    iron = E['iron-lattice']
    za = S['terrace-62'][5]        # 50: the south abutment's end
    zb = S['terrace-67'][4]        # 79: the north abutment's start
    xp = S['iron-bridge-parapet-e'][1]   # 4: the parapet's outer face
    rise = 9.0
    n = 24 if T.detail else 10
    zc, half = (za + zb) / 2, (zb - za) / 2

    def y_at(z, r=rise):
        u = (z - zc) / half
        return r * max(0.0, 1 - u * u)
    for sgn in (-1, 1):
        x = sgn * (xp + .3)
        rib = [(x, y_at(za + (zb - za) * i / n), za + (zb - za) * i / n) for i in range(n + 1)]
        rib2 = [(x, y_at(za + (zb - za) * i / n, rise - 1.1), za + (zb - za) * i / n) for i in range(n + 1)]
        for p, q in zip(rib[:-1], rib[1:]):
            mk.seg(g, T, p, q, .18, .34, iron, up=(1, 0, 0))
        for p, q in zip(rib2[1:-2], rib2[2:-1]):
            mk.seg(g, T, p, q, .14, .2, iron, up=(1, 0, 0))
        # the web: verticals and diagonals between the two chords
        for i in range(1, n):
            p, q = rib[i], rib2[i]
            if q[1] > .3:
                mk.seg(g, T, p, q, .08, .08, iron)
                if T.detail and i < n - 1:
                    mk.seg(g, T, q, rib[i + 1], .06, .06, iron)
        # the hangers down to the deck's edge
        for i in range(2, n - 1, 2 if T.detail else 3):
            q = rib2[i]
            mk.seg(g, T, (x, 0.0, q[2]), q, .06, .06, iron)
        # the bearing shoes on the abutments
        for z in (za, zb):
            bx(g, T, x - .25, x + .25, -.7, -.05, z if z == za else z - .5, z + .5 if z == za else z, iron, bevel=.02)
    # the overhead bracing across the deck (6 m and more over it) and the cast name plate on it
    for z in (zc - 7.5, zc - 2.5, zc + 2.5, zc + 7.5):
        y = y_at(z) - .25
        if y < 6.2:
            continue
        mk.seg(g, T, (-(xp + .3), y, z), ((xp + .3), y, z), .12, .22, iron)
        if T.detail:
            mk.seg(g, T, (-(xp + .3), y, z), (0.0, y - .9, z), .07, .07, iron)
            mk.seg(g, T, ((xp + .3), y, z), (0.0, y - .9, z), .07, .07, iron)
    yp = y_at(zc) - .55
    bx(g, T, -2.4, 2.4, yp - .7, yp, zc - .1, zc + .1, F['iron-black'], bevel=.02)
    mk.seg(g, T, (-(xp + .3), yp - .1, zc), ((xp + .3), yp - .1, zc), .1, .14, iron)
    for face_z, rot in ((zc - .11, 0), (zc + .11, 180)):
        sub = Geo()
        mk.text_stroke(sub, 'IRON BRIDGE', 0.0, 0.0, 0.0, .055, E['gilt'], T, style='cast', w=.045, d=.02)
        mk.part(g, sub, mk.qy(rot), (0.0, yp - .55, face_z))
    for z in (zc - 7.5, zc + 7.5):
        lk.lantern(g, T, 0.0, y_at(z) - 1.3, z, s=.9, hang=.3)
    lk.lantern(g, T, 0.0, yp - .72, zc - .6, s=.9, hang=.2)
    return g


@builder('lock-gates')
def lock_gates(t, S):
    """The Lock ramp: the lock's timber mitre gates stand open against the channel's walls, and the lock-keeper's brick cottage
    on the bank carries the lock lamp and its LOCK 1 board."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    zs, zn = S['terrace-37'][5], S['terrace-46'][4]    # the channel's walls, -6 and 6
    x0 = 47.0

    def floor_at(x):
        return -14.0 * max(0.0, min(1.0, (x - x0) / 56.0))
    timber, strap = E['timber-tar'], F['iron-black']
    for (za, zb) in ((zs, zs + .18), (zn - .18, zn)):
        for (gx0, gx1) in ((57.0, 63.5), (66.0, 72.5)):
            k = int(gx1 - gx0)
            for i in range(k):
                xa = gx0 + i * (gx1 - gx0) / k
                xb = xa + (gx1 - gx0) / k
                bx(g, T, xa + .02, xb - .02, floor_at(xb) + .02, -.05, za, zb, timber, bevel=.015)
            if T.detail:
                for yy in (-.4, -1.6, -2.8, -4.0, -5.2):
                    if yy > floor_at(gx1) + .3:
                        bx(g, T, gx0, gx1, yy - .08, yy + .08, za - .02 if za > 0 else za, zb + .02 if za < 0 else zb, strap, bevel=0, cell=0)
            bx(g, T, gx0 - .35, gx0, floor_at(gx0) + .02, 0.0, za, zb, E['timber-oak'], bevel=.02)
    # the lock cottage: brick, two storeys, a slate band, a lamp on a swan neck over its door and the LOCK 1 board
    box = S['lock-cottage']
    x0b, x1b, y0, y1, z0, z1 = box
    brick = F['brick-stock']
    seed = 7841

    def face(sub, side, w):
        wins = []
        bays = max(2, int(w / 3.4))
        bw = w / bays
        for i in range(bays):
            cx = (i + .5) * bw
            if side == 'n' and i == bays // 2:
                wins.append((cx - .6, cx + .6, 0.0, 2.6, (ROOM, 1.3), .3))
            else:
                wins.append((cx - .55, cx + .55, .9, 2.5, lk.lit_or_dark(seed, i + 7 * ord(side), .4), .25))
            wins.append((cx - .5, cx + .5, 4.3, 5.9, lk.lit_or_dark(seed, i + 7 * ord(side) + 50, .3), .2))
        lk.facade(sub, T, -.02, 0.0, w, y0, y1, brick, wins, f_frame=F['paint-cream'], flat=side in ('s', 'e'))
        lk.string_course(sub, T, 3.5, 0.0, w, -.02, F['stone-dress'])
        lk.cornice(sub, T, y1, 0.0, w, -.02, F['stone-dress'], s=.7)
        bx(sub, T, 0.0, w, 0.0, .55, -.06, -.02, F['brick-blue'], bevel=.01, skip=('+z',))
    lk.skin(g, box, face)
    lk.roof_cap(g, T, box, F['slate'])
    # the lamp and the board on the north face (z = -26), over the lock
    zf = z1
    lk.scroll_bracket(g, T, (64.0, 6.4, zf + .02), (64.0, 6.4, zf + 1.1), F['iron-black'], w=.07, curl=.25)
    lk.lantern(g, T, 64.0, 6.35, zf + .95, s=.8, hang=0.0)
    bx(g, T, 61.6, 66.4, 3.55, 4.25, zf, zf + .06, F['paint-black'], bevel=.01)
    sub = Geo()
    mk.text_stroke(sub, 'LOCK 1', 0.0, 0.0, 0.0, .085, F['paint-cream'], T, style='flat', w=.07, d=.01)
    mk.part(g, sub, mk.qy(180), (64.0, 3.65, zf + .07))
    return g


@builder('causeway-lamps')
def causeway_lamps(t, S):
    """The Low Causeway: swan-neck lamps stand off the causeway's sides at a steady stride, their heads over the deck, and the far
    bank's water gate frames the stair slot: a rusticated stone arch with a keystone mask and a lantern."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    cw = S['low-causeway']           # x -3..3, y -8..-7, z -85..-48
    deck = cw[3]
    iron = F['iron-green']
    for z in (-52.0, -61.0, -70.0, -79.0):
        for sgn in (-1, 1):
            x = sgn * (cw[1] + .14)
            mk.rod(g, (x, deck - 2.0, z), (x, deck + 4.3, z), .1, iron, T.small, caps=True)
            bx(g, T, min(x, sgn * cw[1]), max(x, sgn * cw[1]), deck - 1.6, deck - .4, z - .2, z + .2, iron, bevel=.01)
            m = 6 if T.detail else 3
            pts = [(x - sgn * 1.0 * math.sin(i / m * PI / 2), deck + 4.3 + .6 * math.sin(i / m * PI), z) for i in range(m + 1)]
            for p, q in zip(pts[:-1], pts[1:]):
                mk.rod(g, p, q, .06, iron, T.small, caps=False)
            lk.lantern(g, T, pts[-1][0], pts[-1][1] - .02, z, s=.75, hang=.15)
    # the water gate in the far bank: its face at z -85 toward the camp, the stair slot x -3..3 climbing south from -7 (built
    # facing -z at z 0 and turned to face north)
    lk.placed(g, lambda s: _water_gate(s, T, S), 180.0, (0.0, 0.0, S['terrace-7'][5]))
    return g


def _water_gate(g, T, S):
    zf = 0.0
    stone, rust = F['stone-dress'], F['granite']
    xw, xe = S['terrace-7'][1], S['terrace-8'][0]
    y0 = -7.0 + 3.15
    n = 12 if T.detail else 6
    w = (xe - xw) / 2
    for i in range(n):
        a0, a1 = PI - PI * i / n, PI - PI * (i + 1) / n
        p0 = (w * math.cos(a0), y0 + 2.4 * math.sin(a0))
        p1 = (w * math.cos(a1), y0 + 2.4 * math.sin(a1))
        q1 = ((w + .9) * math.cos(a1), y0 + 3.3 * math.sin(a1) + .2)
        q0 = ((w + .9) * math.cos(a0), y0 + 3.3 * math.sin(a0) + .2)
        g.face(((p0[0], p0[1], zf - .2), (p1[0], p1[1], zf - .2), (q1[0], q1[1], zf - .2), (q0[0], q0[1], zf - .2)), stone, (0, 0, -1))
        g.face(((p0[0], p0[1], zf - .2), (p0[0], p0[1], zf + .6), (p1[0], p1[1], zf + .6), (p1[0], p1[1], zf - .2)), stone,
               mk._norm((-math.cos((a0 + a1) / 2), -math.sin((a0 + a1) / 2), 0)))
    bx(g, T, -.45, .45, y0 + 2.2, y0 + 3.75, zf - .32, zf, F['stone-civic'], bevel=.03)
    if T.detail:
        mk.ellipsoid(g, (0.0, y0 + 2.85, zf - .36), (.22, .26), F['stone-civic'], 8, rings=4)
    for sgn in (-1, 1):
        y = -13.8
        k = 0
        while y < -.4:
            wq = 1.2 if k % 2 == 0 else .7
            x0q = sgn * (w + .02)
            bx(g, T, min(x0q, x0q + sgn * wq), max(x0q, x0q + sgn * wq), y, y + .55, zf - .18, zf, rust, bevel=.03)
            y += .6
            k += 1
    lk.lantern(g, T, 0.0, y0 + 2.15, zf + .3, s=1.0, hang=.3)


@builder('mill-lucam')
def mill_lucam(t, S):
    """The covered mill bridge's mill: Meridian Flour Mills in brick, its timber lucam hung out over the lane from the top storey
    with a hoist beam, a chain and a sack, its door lit, and the mill's name painted fading across its east face."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    box = S['mill']                 # x -80..-52, z -40..-10, 0..13
    x0, x1, y0, y1, z0, z1 = box
    brick = F['brick-stock']
    seed = 7843
    zc = -14.0

    def face(sub, side, w):
        wins = []
        bays = max(3, int(w / 3.0))
        bw = w / bays
        for i in range(bays):
            cx = (i + .5) * bw
            for k, yy in enumerate((.9, 4.1, 7.2, 10.1)):
                # the east face's top storey leaves room for the lucam (local x runs from z0 at u 0 to z1)
                if side == 'e' and k >= 2 and abs((z0 + cx) - zc) < 2.2:
                    continue
                if side == 'e' and k in (1, 2) and 4.0 < cx < 26.0 and 3.6 < yy < 7.6:
                    continue
                wins.append((cx - .5, cx + .5, yy, yy + 1.7, lk.lit_or_dark(seed, i + 13 * k + 7 * ord(side), .22), .18))
        lk.facade(sub, T, -.02, 0.0, w, y0, y1, brick, wins, f_frame=F['paint-cream'], reveal=.2, flat=side in ('s', 'w'))
        for yy in (3.6, 6.8, 9.7):
            lk.string_course(sub, T, yy, 0.0, w, -.02, F['brick-blue'], h=.22, d=.06)
        lk.cornice(sub, T, y1, 0.0, w, -.02, F['brick-blue'], s=.8)
        bx(sub, T, 0.0, w, 0.0, .7, -.07, -.02, F['brick-blue'], bevel=.01, skip=('+z',))
        if side == 'e':
            mk.text_stroke(sub, 'MERIDIAN', w / 2, 5.0, -.025, .16, E['poster-paper'], T, style='flat', w=.13, d=.004)
            mk.text_stroke(sub, 'FLOUR MILLS', w / 2, 3.9, -.025, .1, E['poster-paper'], T, style='flat', w=.08, d=.004)
    lk.skin(g, box, face)
    lk.roof_cap(g, T, box, F['slate'])
    xf = x1
    timber = E['timber-oak']
    bx(g, T, xf, xf + 1.7, 8.9, 12.2, zc - 1.6, zc + 1.6, timber, bevel=.03)
    for sgn in (-1, 1):
        g.face(((xf, 12.2, zc + sgn * 1.75), (xf + 1.9, 12.2, zc + sgn * 1.75), (xf + 1.9, 12.95, zc), (xf, 12.95, zc)), F['slate'], (0, .9, sgn * .45))
    g.face(((xf + 1.9, 12.2, zc - 1.75), (xf + 1.9, 12.2, zc + 1.75), (xf + 1.9, 12.95, zc)), timber, (1, 0, 0))
    g.face(((xf + 1.71, 9.3, zc - .8), (xf + 1.71, 9.3, zc + .8), (xf + 1.71, 11.7, zc + .8), (xf + 1.71, 11.7, zc - .8)), glow(WARM, 1.6), (1, 0, 0))
    for sgn in (-1, 1):
        bx(g, T, xf + 1.7, xf + 2.6, 9.3, 11.7, zc + sgn * .8, zc + sgn * .86, timber, bevel=.01)
    mk.seg(g, T, (xf + 1.0, 12.0, zc), (xf + 3.4, 12.0, zc), .22, .22, timber)
    sk.lathe(g, [(.25, -.05), (.25, .05)], (xf + 3.1, 11.7, zc), F['iron-black'], T.small + 2, axis=(0, 0, 1), cap0=True, cap1=True)
    mk.rod(g, (xf + 3.35, 11.7, zc), (xf + 3.35, 8.0, zc), .03, F['iron-black'], 4, caps=False)
    mk.ellipsoid(g, (xf + 3.35, 7.5, zc), (.38, .55), F['canvas'], T.small + 2, rings=4)
    lk.lantern(g, T, xf + 1.6, 12.15, zc + 1.3, s=.6, hang=0.0)
    return g


# ===========================================================================================================================
# The Arcades
# ===========================================================================================================================
@builder('galleria-fan')
def galleria_fan(t, S):
    """The Galleria: a great fan of iron and lit glass closes the glass roof's end over the arcade, and opal globes hang down the
    Galleria under its roof, each on its brass rod."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    lid = S['n-lid-1']              # x -7..7, y 11..11.5, z 45..90
    arc = S['arcade-n-roof']        # z 45..55, y 6..7
    z = arc[5] + .3
    yb, yt = arc[3] + .05, lid[2]
    x0, x1 = lid[0], lid[1]
    cx, w = (x0 + x1) / 2, (x1 - x0) / 2
    iron = E['iron-lattice']
    glass = glow(0xF2DDB0, 1.2)
    n = 16 if T.detail else 8
    hub = (cx, yb)
    rim = [(cx + w * math.cos(PI - PI * i / n), yb + (yt - yb - .1) * math.sin(PI * i / n)) for i in range(n + 1)]
    for i in range(n):
        a, b = rim[i], rim[i + 1]
        g.face(((hub[0], hub[1] + .05, z), (a[0], a[1], z), (b[0], b[1], z)), glass, (0, 0, -1))
        g.face(((hub[0], hub[1] + .05, z + .02), (b[0], b[1], z + .02), (a[0], a[1], z + .02)), glass, (0, 0, 1))
    for i in range(n + 1):
        mk.seg(g, T, (hub[0], hub[1], z - .05), (rim[i][0], rim[i][1], z - .05), .07, .08, iron)
    for p, q in zip(rim[:-1], rim[1:]):
        mk.seg(g, T, (p[0], p[1], z - .06), (q[0], q[1], z - .06), .12, .14, iron)
    mid = [(cx + w * .55 * math.cos(PI - PI * i / n), yb + (yt - yb) * .55 * math.sin(PI * i / n)) for i in range(n + 1)]
    for p, q in zip(mid[:-1], mid[1:]):
        mk.seg(g, T, (p[0], p[1], z - .06), (q[0], q[1], z - .06), .08, .1, iron)
    mk.seg(g, T, (x0, yb + .06, z - .05), (x1, yb + .06, z - .05), .14, .14, iron)
    for zz in (62.0, 70.0, 78.0, 86.0):
        lk.globe(g, T, cx, 6.3 + .54, zz, r=.45)
        mk.rod(g, (cx, 6.84, zz), (cx, lid[2], zz), .025, F['brass'], 4, caps=False)
    return g


@builder('shambles-bull')
def shambles_bull(t, S):
    """The Shambles: a gilded bull's head hangs from a wrought bracket at the lane's mouth, lit by its own lamp, and a carved
    timber beam with THE SHAMBLES spans the lane's entrance between the jettied houses."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    s0 = S['shambles-0']            # x 71..90, z 23..40
    s2 = S['shambles-2']            # x 71..90, z -8..17
    zf = s0[4]
    gilt = E['gilt']
    lk.scroll_bracket(g, T, (72.6, 8.75, zf - .02), (72.6, 8.75, zf - 1.7), F['iron-black'], w=.09, curl=.35)
    bx(g, T, 72.3, 72.9, 7.6, 9.2, zf - .05, zf, F['iron-black'], bevel=0, cell=0)
    mk.rod(g, (72.6, 8.7, zf - 1.1), (72.6, 8.15, zf - 1.1), .02, F['iron-black'], 4, caps=False)
    hx, hy, hz = 72.6, 7.35, zf - 1.1
    mk.ellipsoid(g, (hx, hy, hz), (.42, .5), gilt, T.round, rings=5 if T.detail else 3)
    mk.ellipsoid(g, (hx, hy - .45, hz - .12), (.3, .28), gilt, T.round, rings=4 if T.detail else 3)
    for sgn in (-1, 1):
        mk.ellipsoid(g, (hx + sgn * .48, hy + .25, hz), (.13, .22), gilt, 6, rings=3, axis=(sgn, .3, 0))
        horn = [(hx + sgn * (.3 + .45 * u), hy + .35 + .55 * u * u, hz - .05 * u) for u in (0, .3, .6, 1.0)]
        mk.tube_path(g, horn, .09, gilt, T.small, radii=[.12, .1, .07, .03])
    if T.detail:
        sk.lathe(g, [(.11, -.015), (.13, 0), (.11, .015)], (hx, hy - .68, hz - .3), gilt, 8, axis=(0, 0, 1))
        for sgn in (-1, 1):
            mk.ellipsoid(g, (hx + sgn * .18, hy + .08, hz - .38), (.06, .05), F['iron-black'], 6, rings=3)
    lk.lantern(g, T, 72.6, 8.73, zf - .45, s=.6, hang=0.0)
    xb = s0[0] + .35
    zs, zn = s2[5], zf
    oak = E['timber-oak']
    bx(g, T, xb - .2, xb + .2, 5.4, 6.1, zs, zn, oak, bevel=.03)
    for z, sgn in ((zs, 1), (zn, -1)):
        mk.seg(g, T, (xb, 4.5, z + sgn * .1), (xb, 5.45, z + sgn * 1.0), .18, .18, oak)
    for face, rot in ((xb - .21, 90), (xb + .21, 270)):
        sub = Geo()
        mk.text_stroke(sub, 'THE SHAMBLES', 0.0, 0.0, 0.0, .045, E['gilt'], T, style='cast' if T.detail else 'flat', w=.04, d=.015)
        mk.part(g, sub, mk.qy(rot), (face, 5.55, (zs + zn) / 2))
    return g


@builder('undercroft-crypt')
def undercroft_crypt(t, S):
    """The Undercroft: a great Gothic lantern hangs on its chain under the arcade where the way turns down, and the ramp's mouth
    is carved as a crypt's portal: a hood mould on the roof's edge, shafts down its jambs, the name in the frieze."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    arc = S['arcade-s-roof']
    iron = F['iron-black']
    x, z = -15.0, -50.0
    y = 4.4
    glass = glow(0xFFB86A, 2.4)
    sk.lathe(g, [(.42, -.55), (.55, -.35), (.55, .35), (.48, .55)], (x, y, z), glass, 6, smooth=False, phase=PI / 6, cap0=True)
    sk.lathe(g, [(.65, .55), (.65, .63), (.3, 1.25), (.04, 1.75)], (x, y, z), iron, 6, smooth=False, phase=PI / 6)
    sk.lathe(g, [(.5, -.62), (.5, -.55), (.2, -.85), (.03, -1.05)], (x, y, z), iron, 6, smooth=False, phase=PI / 6)
    if T.detail:
        for k in range(6):
            a = PI / 6 + k * PI / 3
            mk.rod(g, (x + .55 * math.cos(a), y - .35, z + .55 * math.sin(a)), (x + .55 * math.cos(a), y + .4, z + .55 * math.sin(a)), .025, iron, 4, caps=False)
            mk.rod(g, (x + .48 * math.cos(a), y + .75, z + .48 * math.sin(a)), (x + .62 * math.cos(a), y + .85, z + .62 * math.sin(a)), .03, iron, 3)
    for i in range(int((arc[2] - (y + 1.75)) / .3)):
        a = y + 1.75 + i * .3
        mk.rod(g, (x, a, z), (x, a + .26, z), .028, iron, 4, caps=False)
    # the portal on the ramp's mouth (the lid's north edge at z -71, toward the arcade and the camp): built facing -z at z 0
    lid = S['undercroft-ramp-lid']
    lk.placed(g, lambda s: _crypt_portal(s, T, lid), 180.0, (0.0, 0.0, lid[5]))
    return g


def _crypt_portal(g, T, lid):
    zf = 0.0
    stone = F['stone-dress']
    xl, xr = -(lid[1] - 1.0), -(lid[0] + 1.0)
    bx(g, T, xl - .5, xr + .5, -1.0, 0.0, zf - .12, zf, stone, bevel=.02, skip=('+z',))
    mk.text_stroke(g, 'THE UNDERCROFT', (xl + xr) / 2, -.75, zf - .12, .07, F['stone-civic'], T, style='cast' if T.detail else 'flat', w=.06, d=.03)
    mk.mould(g, T, [(0, 0), (.12, .04), (.15, .12), (0, .16)], [(xl - .45, zf), (xr + .45, zf)], stone, y=-1.12, cell=0)
    for xw, sgn in ((xl, 1), (xr, -1)):
        for dz in (-.3, -.9):
            cxs = xw + sgn * .12
            mk.rod(g, (cxs, -4.6, zf + dz), (cxs, -1.05, zf + dz), .1, stone, T.small + 2)
            sk.lathe(g, [(.12, 0), (.12, .12), (.1, .22)], (cxs, -1.3, zf + dz), stone, T.small + 2, smooth=False)
        lk.lantern(g, T, xw + sgn * .17, -.15, zf - .25, s=.45, hang=0.0)


@builder('leads-cupola')
def leads_cupola(t, S):
    """The Leads: the stair tower's crown, an octagonal lantern cupola with a lead dome and a gilt vane, corbelled out from the
    upper terrace's face over the first flight, its windows lit; below it, the tower's brick face with a lit lancet."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    box = S['terrace-32']             # x -49..-37, z -3..4, top 7
    xf = box[1]
    za, zb = box[4], box[5]
    cz = (za + zb) / 2
    brick, stone = F['brick-stock'], F['stone-dress']

    def tower_face(s):
        w = zb - za
        lk.facade(s, T, -.02, 0.0, w, 0.0, 7.0, brick, [(w / 2 - .5, w / 2 + .5, 2.0, 5.2, (WARM, 1.5), .5)], f_frame=F['paint-black'])
        for u in (.15, w - .6):
            bx(s, T, u, u + .45, 0.0, 6.3, -.2, -.02, stone, bevel=.02, skip=('+z',))
        lk.string_course(s, T, 6.3, 0.0, w, -.02, stone, h=.25, d=.12)
    lk.placed(g, tower_face, 270.0, (xf, 0.0, za))
    px0, px1 = xf + .3, xf + 5.2
    for k in range(5):
        zz = za + .6 + k * (zb - za - 1.2) / 4
        g.face(((xf, 6.6, zz - .22), (xf, 6.6, zz + .22), (xf + .9, 7.15, zz + .22), (xf + .9, 7.15, zz - .22)), stone, (.5, -.85, 0))
    bx(g, T, px0, px1, 7.15, 7.45, za - .3, zb + .3, stone, bevel=.03)
    cx = (px0 + px1) / 2 + .2
    R = 2.25
    sides = 8
    sk.lathe(g, [(R, 7.45), (R, 9.8)], (cx, 0.0, cz), F['paint-cream'], sides, smooth=False, phase=PI / 8)
    for k in range(sides):
        a = PI / 8 + 2 * PI * k / sides
        nx, nz = math.cos(a + PI / 8), math.sin(a + PI / 8)
        wx, wz = cx + nx * (R * math.cos(PI / 8) + .02), cz + nz * (R * math.cos(PI / 8) + .02)
        tx, tz = -nz, nx
        w = .42
        pts = [(wx - tx * w, 7.85, wz - tz * w), (wx + tx * w, 7.85, wz + tz * w)]
        k2 = 5 if T.detail else 3
        for j in range(k2 + 1):
            ang = PI * j / k2
            pts.append((wx + tx * w * math.cos(ang), 9.05 + w * math.sin(ang), wz + tz * w * math.cos(ang)))
        g.face(pts, glow(WARM, 1.8), (nx, 0, nz))
    sk.lathe(g, [(R + .05, 9.8), (R + .3, 9.95), (R + .3, 10.15), (R + .05, 10.2)], (cx, 0.0, cz), stone, sides, smooth=False, phase=PI / 8)
    dome = [(R + .05, 10.2), (R * .95, 10.7), (R * .75, 11.2), (R * .45, 11.6), (.25, 11.85), (.25, 12.1)]
    sk.lathe(g, dome, (cx, 0.0, cz), F['lead'], 16 if T.detail else sides, smooth=T.detail, phase=PI / 8)
    sk.lathe(g, [(.25, 12.1), (.12, 12.4), (.04, 13.3)], (cx, 0.0, cz), E['gilt'], 6)
    if T.detail:
        g.face(((cx - .55, 12.9, cz), (cx + .45, 12.9, cz), (cx + .2, 13.15, cz), (cx - .4, 13.15, cz)), E['gilt'], (0, 0, -1))
        g.face(((cx - .55, 12.9, cz), (cx - .4, 13.15, cz), (cx + .2, 13.15, cz), (cx + .45, 12.9, cz)), E['gilt'], (0, 0, 1))
    return g
