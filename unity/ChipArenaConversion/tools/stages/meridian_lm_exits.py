"""Meridian landmarks: the 20 exit landmarks, one per gate of each approved layout, in the world's own frame.

Each names its exit by its shape and holds the art rules: below 3.1 m over the floor beneath it, a piece lies within 0.25 m of a
block (a skin, a corbel, a plate), anything freer hangs 3.1 m or more over the floor or out over a drop. The read points in Core
NativeMeridianLandmarks.Exits are where these pieces put their named features.
"""
import math

import meridian_gates_kit as mk
import meridian_lm_kit as lk
import stagekit as sk
from meridian_gates_kit import F, Geo, bx, glow
from meridian_lm_kit import PI, SODIUM, SODIUM_GLASS, WARM, ROOM

BUILDERS = {}


def builder(name):
    def wrap(fn):
        BUILDERS[name] = fn
        return fn
    return wrap


def X():
    return lk.finishes()


# ===========================================================================================================================
# Crown Hill
# ===========================================================================================================================
@builder('steps-lanterns')
def steps_lanterns(t, S):
    """The Hundred Steps: a cast-iron lantern arch spanning the stair's slot between its retaining walls over the first landing,
    and the Crown's stair head on the upper terrace's wall: rusticated pylons in relief with great lanterns on scroll brackets
    and a carved cartouche."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    iron, stone = E['iron-lattice'], F['stone-civic']
    wl = S['terrace-46']           # the west retaining wall (x .. -6), top 7
    xw = wl[1]                     # -6
    xe = S['terrace-48'][0]        # 6
    z = 46.0
    # the arch: two concentric ribs from corbels at 6.7 m on the wall faces to a crown at 9.6 m, laced together
    n = 18 if T.detail else 8
    a, b = (xe - xw) / 2 - .1, 3.0
    cx, cy = (xw + xe) / 2, 6.7
    inner = [(cx + a * math.cos(PI - PI * i / n), cy + b * math.sin(PI * i / n)) for i in range(n + 1)]
    outer = [(cx + (a + .05) * math.cos(PI - PI * i / n), cy + (b + .55) * math.sin(PI * i / n)) for i in range(n + 1)]
    for zz in (z - .15, z + .15):
        for arc in (inner, outer):
            for p, q in zip(arc[:-1], arc[1:]):
                mk.seg(g, T, (p[0], p[1], zz), (q[0], q[1], zz), .09, .1, iron)
        for i in range(n + 1):
            p, q = inner[i], outer[i]
            mk.seg(g, T, (p[0], p[1], zz), (q[0], q[1], zz), .06, .06, iron)
            if i < n and T.detail:
                q2 = outer[i + 1]
                mk.seg(g, T, (p[0], p[1], zz), (q2[0], q2[1], zz), .05, .05, iron)
    if T.detail:
        for i in range(0, n + 1, 2):
            p = inner[i]
            mk.seg(g, T, (p[0], p[1], z - .15), (p[0], p[1], z + .15), .05, .05, iron)
    # the keystone crest and its gilt crown
    bx(g, T, cx - .45, cx + .45, cy + b + .1, cy + b + 1.05, z - .22, z + .22, iron, bevel=.03)
    crown = E['gilt']
    for k in range(5):
        x = cx - .4 + k * .2
        sk.lathe(g, [(.07, 0), (.04, .25 + (.12 if k % 2 == 0 else 0)), (0, .32 + (.12 if k % 2 == 0 else 0))], (x, cy + b + 1.05, z), crown, 5, smooth=False)
    # corbels on the walls (they hug the wall faces) carry the ribs' feet
    for x, sgn in ((xw, 1), (xe, -1)):
        bx(g, T, min(x, x + sgn * .24), max(x, x + sgn * .24), 5.7, 6.75, z - .45, z + .45, stone, bevel=.03)
        bx(g, T, min(x, x + sgn * .15), max(x, x + sgn * .15), 5.2, 5.7, z - .3, z + .3, stone, bevel=.02)
    # three lanterns hung from the arch: the middle one high and large, the two side ones lower
    lk.lantern(g, T, cx, cy + b - .05, z, s=1.35, hang=.0)
    for dx in (-2.4, 2.4):
        top = cy + b * math.sqrt(max(0.0, 1 - (dx / a) ** 2)) - .02
        lk.lantern(g, T, cx + dx, top - .5, z, s=1.0, hang=.5)
    # the stair head on the upper terrace's south face (z = 75): pylons in relief, lanterns on brackets, the cartouche
    head = S['terrace-65']        # x -60..60, z 75..105, top 14
    zf = head[4]
    rust = F['stone-dress']
    for sgn in (-1, 1):
        x0, x1 = sorted((sgn * 7.6, sgn * 11.4))
        # the pylon: rusticated courses 0.2 m proud, a plinth, a capped top just under the terrace's edge
        y = 7.0
        k = 0
        while y < 13.4:
            hgt = .62
            inset = .06 if k % 2 else 0.0
            bx(g, T, x0 + inset, x1 - inset, y + .03, y + hgt - .03, zf - .2, zf, rust, bevel=.04, skip=('+z',))
            y += hgt
            k += 1
        bx(g, T, x0 - .25, x1 + .25, 13.45, 14.0, zf - .45, zf, stone, bevel=.03, skip=('+z',))
        # the great lantern on its scroll bracket (above 10.1 m: 3.1 m over the +7 terrace in front)
        xm = (x0 + x1) / 2
        lk.scroll_bracket(g, T, (xm, 13.1, zf - .05), (xm, 13.1, zf - 1.15), F['iron-black'], w=.1, curl=.4)
        lk.lantern(g, T, xm, 12.95, zf - 1.05, s=1.5, hang=.15)
    # the cartouche over the stair head, in relief (the landing in front stands at 10.5 m)
    bx(g, T, -5.4, 5.4, 11.0, 13.8, zf - .12, zf, stone, bevel=.03, skip=('+z',))
    bx(g, T, -5.0, 5.0, 11.3, 13.5, zf - .2, zf - .12, rust, bevel=.02)
    mk.text_stroke(g, 'THE CROWN', 0.0, 12.0, zf - .2, .12, F['brass'] if T.detail else E['gilt'], T, style='cast', w=.09, d=.04)
    return g


@builder('belvedere-hotel')
def belvedere_hotel(t, S):
    """The Promenade: the Belvedere Hotel, a cream stucco seaside hotel on its lot, with a glazed verandah over its door and a
    tall neon blade sign at its north-west corner, reading BELVEDERE down the promenade."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    box = S['lot-shelf-s-7']      # x 80..100, z -25..-15, 0..10
    x0, x1, y0, y1, z0, z1 = box
    st, frame = E['stucco-hotel'], F['paint-white'] if 'paint-white' in F else F['paint-cream']
    seed = 6471

    def face(sub, side, w):
        wins = []
        bays = max(2, int(w / 3.2))
        bw = w / bays
        for i in range(bays):
            cx = (i + .5) * bw
            # the ground storey: tall arched windows (the north face's middle bay is the door)
            if side == 'n' and i == bays // 2:
                wins.append((cx - .8, cx + .8, 0.0, 2.9, (ROOM, 1.4), .6))
            else:
                wins.append((cx - .7, cx + .7, .7, 3.0, lk.lit_or_dark(seed, i + 10 * ord(side), .45), .5))
            for k, yy in enumerate((4.6, 7.4)):
                wins.append((cx - .55, cx + .55, yy, yy + 1.8, lk.lit_or_dark(seed, i + 10 * ord(side) + 100 * (k + 1), .3)))
        lk.facade(sub, T, -.02, 0.0, w, y0, y1, st, wins, f_frame=F['paint-green'], flat=side in ('s', 'e'))
        # rusticated ground storey bands, the string course, the cornice and a band of green under the eaves
        if T.detail:
            for yy in (1.0, 1.9, 2.8):
                bx(sub, T, 0.0, w, yy, yy + .04, -.05, -.02, F['stucco-joint'], bevel=0, cell=0)
        lk.string_course(sub, T, 3.9, 0.0, w, -.02, F['stone-civic'])
        if side != 's':          # the south face stands against its neighbours: nothing over their roofs
            lk.cornice(sub, T, y1, 0.0, w, -.02, F['stone-civic'], s=1.0)
        bx(sub, T, 0.0, w, y0, y0 + .6, -.06, -.02, F['paint-green'], bevel=.01, skip=('+z',))
    lk.skin(g, box, face)
    lk.roof_cap(g, T, box, F['tar-felt'])
    # the verandah: a glazed canopy on brackets over the door bays of the north face (above 3.6 m)
    zc = z1
    yv = 4.0
    bx(g, T, 84.5, 95.5, yv, yv + .16, zc, zc + 2.0, F['paint-green'], bevel=.02)
    g.face(((84.7, yv + .18, zc + .1), (95.3, yv + .18, zc + .1), (95.3, yv + .5, zc + 1.9), (84.7, yv + .5, zc + 1.9)), F['glass-canopy'], (0, .98, -.18))
    for xx in (85.0, 88.6, 92.2, 95.2):
        lk.scroll_bracket(g, T, (xx, yv, zc + .02), (xx, yv, zc + 1.9), F['paint-green'], w=.07, curl=.18)
    if T.detail:
        for xx in (86.5, 90.0, 93.5):
            lk.lantern(g, T, xx, yv - .02, zc + 1.6, s=.55, hang=.0)
        # the valance: a fretted edge under the canopy's front
        for i in range(22):
            xx = 84.7 + i * .5
            g.face(((xx, yv, zc + 2.0), (xx + .5, yv, zc + 2.0), (xx + .25, yv - .25, zc + 2.0)), F['paint-green'], (0, 0, 1))
    # the blade sign: off the north face near its west corner, its faces to the west and east, BELVEDERE down it in neon
    sx, sz0, sz1 = 81.2, zc + .05, zc + 1.45
    yb, yt = 3.5, 9.9
    bx(g, T, sx - .16, sx + .16, yb, yt, sz0 + .1, sz1, E['cinema-red'], bevel=.03)
    for yy in (yb + .6, yt - .8):
        mk.seg(g, T, (sx, yy, zc), (sx, yy, sz0 + .1), .1, .1, F['iron-black'])
    neon = glow(0xFF56B4, 3.0)
    letters = 'BELVEDERE'
    hgt = (yt - yb - .6) / len(letters)
    for k, ch in enumerate(letters):
        yy = yt - .4 - (k + 1) * hgt
        for side in (-1, 1):
            sub = Geo()
            mk.text_stroke(sub, ch, 0.0, 0.0, 0.0, hgt * .14, neon, T, style='neon', w=.07, standoff=.05)
            mk.part(g, sub, mk.qy(90 if side < 0 else 270), (sx + side * .17, yy, (sz0 + sz1) / 2 + .05))
    # its bulb border
    if T.detail:
        bulb = glow(0xFFD27A, 2.4)
        for side in (-1, 1):
            for k in range(int((yt - yb) / .35)):
                yy = yb + .2 + k * .35
                for zz in (sz0 + .2, sz1 - .1):
                    mk.disc(g, sx + side * .175, yy, zz, .045, 5, bulb, normal=(side, 0, 0))
    # a cap and a finial over the sign (it stands out from the face, not on the roof)
    bx(g, T, sx - .25, sx + .25, yt, yt + .08, sz0 + .1, sz1 + .1, F['brass'], bevel=.02)
    return g


@builder('chute-washing')
def chute_washing(t, S):
    """The Chute: washing strung back and forth across the tenements' slot over the stair, and the pawnbroker's three gilt balls
    on their bracket with a lamp under them."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    xw = S['chute-tenement-w'][1]    # -3
    xe = S['chute-tenement-e'][0]    # 3
    cloth = (E['linen'], E['linen-blue'], E['linen-red'], E['linen'], F['canvas'])
    seed = 6472
    lines = [(-33.5, 12.5), (-36.0, 8.6), (-38.5, 14.4), (-41.0, 10.0), (-43.5, 7.6), (-46.0, 12.0), (-48.5, 9.2)]
    for i, (z, y) in enumerate(lines):
        sag = .45
        n = 10 if T.detail else 4
        pts = [(xw + (xe - xw) * k / n, y - sag * math.sin(PI * k / n), z) for k in range(n + 1)]
        for p, q in zip(pts[:-1], pts[1:]):
            mk.rod(g, p, q, .012 if T.detail else .02, F['rope'], 3, caps=False)
        # the hooks
        for x in (xw, xe):
            bx(g, T, x - .05 if x > 0 else x, x if x > 0 else x + .05, y - .1, y + .1, z - .05, z + .05, F['iron-black'], bevel=0, cell=0)
        # the washing: sheets and shirts pegged along the line
        k = 0
        x = xw + .5
        while x < xe - .6:
            h01 = mk.h01(seed, i, k)
            w = .5 + h01 * .9
            if x + w > xe - .3:
                break
            ya = y - sag * math.sin(PI * (x - xw) / (xe - xw))
            yb_ = y - sag * math.sin(PI * (x + w - xw) / (xe - xw))
            drop = .6 + mk.h01(seed, i, k, 3) * .9
            f = cloth[int(mk.h01(seed, i, k, 5) * len(cloth)) % len(cloth)]
            if mk.h01(seed, i, k, 9) < .35 and T.detail:
                # a shirt: body and sleeves
                cxs = x + w / 2
                g.face(((x + .1, ya - .05, z), (x + w - .1, yb_ - .05, z), (x + w - .1, yb_ - drop, z), (x + .1, ya - drop, z)), f, (0, 0, -1))
                g.face(((x + .1, ya - drop, z), (x + w - .1, yb_ - drop, z), (x + w - .1, yb_ - .05, z), (x + .1, ya - .05, z)), f, (0, 0, 1))
                for sgn in (-1, 1):
                    sx_ = cxs + sgn * (w / 2 - .05)
                    for nn in ((0, 0, -1), (0, 0, 1)):
                        g.face(((sx_, ya - .08, z), (sx_ + sgn * .35, ya - .3, z), (sx_ + sgn * .25, ya - .45, z), (sx_, ya - .3, z)), f, nn)
            else:
                for nn in ((0, 0, -1), (0, 0, 1)):
                    g.face(((x, ya, z), (x + w, yb_, z), (x + w, yb_ - drop, z), (x, ya - drop, z)), f, nn)
            x += w + .15 + mk.h01(seed, i, k, 7) * .4
            k += 1
    # the pawnbroker's sign: three gilt balls hung from a scroll bracket off the west tenement, a lamp under the bracket
    z = -35.0
    lk.scroll_bracket(g, T, (xw + .02, 7.6, z), (xw + 1.5, 7.6, z), F['iron-black'], w=.07, curl=.3)
    bx(g, T, xw, xw + .04, 6.4, 8.0, z - .25, z + .25, F['iron-black'], bevel=0, cell=0)
    gilt = E['gilt']
    for (dx, dy) in ((-.24, 0.0), (.24, 0.0), (0.0, -.42)):
        cxb = xw + 1.15 + dx
        cyb = 6.45 + dy
        mk.ellipsoid(g, (cxb, cyb, z), (.21, .21), gilt, T.round, rings=5 if T.detail else 3)
    for dx in (-.24, .24):
        mk.rod(g, (xw + 1.15 + dx, 6.66, z), (xw + 1.15, 7.55, z), .012, gilt, 3, caps=False)
    mk.rod(g, (xw + 1.15, 6.24, z), (xw + 1.15, 6.66, z), .012, gilt, 3, caps=False)
    lk.lantern(g, T, xw + .6, 7.95, z, s=.55, hang=0.0)
    return g


@builder('cutting-struts')
def cutting_struts(t, S):
    """The Hill Cutting: where the trench deepens between the +7 terraces, a rusticated stone flying arch spans it with a lantern
    under its keystone, and beyond it cast-iron lattice struts brace the walls, each with a hanging lamp."""
    T = lk.tier(t)
    E = X()
    g = Geo()
    zs = S['terrace-21'][5]         # the south wall's face, z -4
    zn = S['terrace-39'][4]         # the north wall's face, z 4
    floor = -7.0
    stone, iron = F['stone-dress'], E['iron-lattice']
    x = -111.0
    # the arch: springing on corbels at 3.1 m over the trench floor, its crown at 5.5 m, voussoirs and a keystone
    cz, w = (zs + zn) / 2, (zn - zs) / 2
    y0 = floor + 3.15
    n = 13 if T.detail else 7
    rise = 7.85
    ring_in = [(cz + (w - .05) * math.cos(PI - PI * i / n), y0 + rise * math.sin(PI * i / n)) for i in range(n + 1)]
    ring_out = [(cz + (w - .05) * math.cos(PI - PI * i / n), y0 + rise * math.sin(PI * i / n) + .9 + .3 * math.sin(PI * i / n)) for i in range(n + 1)]
    for i in range(n):
        a, b = ring_in[i], ring_in[i + 1]
        c, d = ring_out[i + 1], ring_out[i]
        gap = .025 if T.detail else 0.0
        for (xa, xb, nn) in ((x - .5, x - .5, (-1, 0, 0)), (x + .5, x + .5, (1, 0, 0))):
            g.face(((xa, a[1], a[0] + gap), (xa, b[1], b[0] - gap), (xa, c[1], c[0] - gap), (xa, d[1], d[0] + gap)), stone, nn)
        # soffit and extrados
        g.face(((x - .5, a[1], a[0]), (x + .5, a[1], a[0]), (x + .5, b[1], b[0]), (x - .5, b[1], b[0])), stone, mk._norm((0, -1, 0)))
        g.face(((x - .5, d[1], d[0]), (x - .5, c[1], c[0]), (x + .5, c[1], c[0]), (x + .5, d[1], d[0])), stone, (0, 1, 0))
    # the keystone, proud
    kc = ring_out[n // 2]
    bx(g, T, x - .62, x + .62, kc[1] - 1.3, kc[1] + .15, cz - .45, cz + .45, F['stone-civic'], bevel=.04)
    # the corbels and pilaster strips on the walls (relief within 0.2 m)
    for zw, sgn in ((zs, 1), (zn, -1)):
        za, zb = sorted((zw, zw + sgn * .2))
        bx(g, T, x - .7, x + .7, floor + .02, y0 + .1, za, zb, F['stone-civic'], bevel=.03)
        za, zb = sorted((zw, zw + sgn * .24))
        bx(g, T, x - .65, x + .65, y0 - .5, y0 + .15, za, zb, stone, bevel=.03)
    lk.lantern(g, T, x, kc[1] - 1.32, cz, s=1.2, hang=.4)
    # the iron struts beyond: lattice girders across the trench, 10.5 m over its floor, each with a lamp
    for xs in (-126.0, -141.0, -156.0):
        lk.lattice(g, T, (xs, floor + 10.0, zs + .02), (xs, floor + 10.0, zn - .02), (0, 1, 0), .8, iron, bays=8, w=.11)
        for zw in (zs, zn):
            za, zb = sorted((zw, zw + (.3 if zw == zs else -.3)))
            bx(g, T, xs - .35, xs + .35, floor + 9.6, floor + 11.2, za, zb, iron, bevel=.02)
        lk.lantern(g, T, xs, floor + 9.95, cz, s=.8, hang=.6)
    return g
