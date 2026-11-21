"""Meridian's street furniture (art bible 6.3-6.5): the municipal bench (and its broken and blanketed states), the cast-iron
litter bin, the cannon and sleeve bollards, the Meridian Telephones Type M kiosk and the wall payphone hood, the bus stop (and
its tram twin in the Station's drift), and the railings (cast spear-top, galvanised guard).

Frames: see mkit.py; free-standing modules stand on their prop block's middle with +z their front.
"""
import math

import mkit as mk
from mkit import F, Fr, Module, add, rot_y, lathe
import stagekit as sk


# ---------------------------------------------------------------------------------------------------------------------------
# The bench: 1.8 x 0.6 x 0.8 m, cast ends bearing the crest, three seat slats and two back slats
# ---------------------------------------------------------------------------------------------------------------------------
def _bench_end(g, T, x, outward):
    """One cast end frame in the plane x (its members swept as cast bars), the crest medallion on its outer face."""
    iron = F['iron-green']
    if T.lod > 0:
        fr = Fr((x, 0, 0), (outward, 0, 0))
        # a leg block and a back block, enough to keep the end's silhouette at range
        g.face([(x, 0.0, .28), (x, .44, .27), (x, .44, -.22), (x, 0.0, -.27)], iron, (outward, 0, 0))
        g.face([(x - outward * .04, 0.0, -.27), (x - outward * .04, .44, -.22), (x - outward * .04, .44, .27), (x - outward * .04, 0.0, .28)],
               iron, (-outward, 0, 0))
        g.face([(x, .42, -.2), (x, .84, -.31), (x, .84, -.25), (x, .42, -.13)], iron, (outward, 0, 0))
        g.face([(x - outward * .04, .42, -.13), (x - outward * .04, .84, -.25), (x - outward * .04, .84, -.31), (x - outward * .04, .42, -.2)],
               iron, (-outward, 0, 0))
        return
    up = (1, 0, 0)

    def path(pts):
        return [(x, y, z) for (z, y) in pts]
    # front leg; back leg rising into the back upright, curled over at its top; the seat bearer; the arm, scrolled in front
    mk.bar(g, path([(.3, 0.0), (.268, .16), (.255, .44)]), .042, .045, iron, up=up)
    mk.bar(g, path([(-.28, 0.0), (-.22, .3), (-.21, .5), (-.255, .72), (-.305, .85), (-.25, .87)]), .042, .045, iron, up=up)
    mk.bar(g, path([(.27, .425), (-.21, .41)]), .04, .045, iron, up=up)
    mk.bar(g, path([(-.24, .63), (.12, .635), (.27, .615), (.305, .57), (.27, .54)]), .036, .045, iron, up=up)
    # the cross rail through the medallion with the city crest
    mk.bar(g, path([(.26, .2), (-.24, .2)]), .03, .04, iron, up=up)
    c = (x, .21, 0.0)
    lathe(g, [(.095, -.026), (.095, .026)], c, iron, 8, axis=(1, 0, 0), cap0=True, cap1=True, phase=math.pi / 8)
    fr = Fr((x + outward * .026, 0, 0), (outward, 0, 0))
    mk.crest(g, T, fr, 0.0, .21, .08, .006, F['enamel-cream'], F['brass'], F['iron-green'], F['enamel-green'], sides=8)
    # feet
    for z in (.3, -.28):
        mk.box(g, None, x - .035, x + .035, 0.0, .03, z - .05, z + .05, iron, 0.0, skip=('-y',))


def bench(T, state='intact'):
    """state: intact, broken (a seat slat snapped and down), blanket (a survivor's blanket over it)."""
    mk.finishes()
    name = 'furn.bench' + ('' if state == 'intact' else '-' + state)
    m = Module(name, T.lod, block='bench- (1.8 x 0.6)', cap=(600, 60) if state == 'intact' else (720, 80))
    g = m.part('body')
    wood = F['timber-old']
    for s in (-1, 1):
        _bench_end(g, T, s * .84, s)
    if T.lod > 0:
        mk.box(g, None, -.89, .89, .42, .46, -.165, .225, wood, 0.0)
        q = sk.quat_axis(1, 0, 0, math.radians(-17))
        sk.obox(g, (0.0, .675, -.26), (.89, .13, .018), q, wood, None, 0.0)
    else:
        seats = [(.17, .43), (.03, .425), (-.11, .42)]
        for k, (z, y) in enumerate(seats):
            if state == 'broken' and k == 1:
                mk.slat(g, (-.89, y + .018, z), (.02, y + .018, z), .11, .036, wood, up=(0, 1, 0), T=T)
                a = (.06, .03, z + .02)
                d = (math.cos(math.radians(33)), math.sin(math.radians(33)), 0.0)
                mk.slat(g, a, mk.add(a, d, .86), .11, .036, wood, up=(0, 1, 0), T=T)
                continue
            mk.slat(g, (-.89, y + .018, z), (.89, y + .018, z), .11, .036, wood, up=(0, 1, 0), T=T)
        for (z, y) in ((-.235, .6), (-.285, .75)):
            mk.slat(g, (-.89, y, z), (.89, y, z), .036, .11, wood, up=(0, math.cos(math.radians(17)), -math.sin(math.radians(17))), T=T)
    if state == 'blanket':
        _blanket(g, T)
    return m


def _blanket(g, T):
    """A survivor's blanket thrown over the back and the seat: a draped sheet with folds."""
    drape = [(-.36, .74), (-.33, .86), (-.27, .9), (-.21, .76), (-.17, .6), (-.13, .49), (-.02, .475), (.12, .47), (.25, .46), (.31, .4),
             (.33, .28)]
    if T.lod > 0:
        drape = drape[::2]
    nx = T.pick(8, 3)
    x0, x1 = -.62, .28
    rows = []
    for (z, y) in drape:
        row = []
        for i in range(nx):
            t = i / (nx - 1)
            x = x0 + (x1 - x0) * t
            fold = .018 * math.sin(t * 9.0 + z * 7.0) + .012 * math.sin(t * 23.0 + y * 5.0)
            off = .02 + fold
            # out from the bench's surface: up on the seat, back behind the back
            nrm = (0.0, 1.0, 0.0) if y < .52 else sk._norm((0.0, .3, -1.0) if z < -.25 else (0.0, .4, 1.0))
            row.append((x, y + nrm[1] * off, z + nrm[2] * off))
        rows.append(row)
    # the grid wound so its faces look out of the drape
    for i in range(len(rows) - 1):
        for j in range(nx - 1):
            a, b, c, d = rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]
            e1 = sk._sub(b, a)
            e2 = sk._sub(d, a)
            n = sk._cross(e2, e1)
            mid = sk._lerp(a, c, .5)
            out = (0.0, mid[1] - .45, mid[2] + .05)
            if sk._dot(n, out) < 0:
                n = (-n[0], -n[1], -n[2])
            g.face((a, b, c, d), F['blanket'], n)
    if T.lod == 0:
        # the stripe woven into its hem
        for i in (len(rows) - 2,):
            for j in range(nx - 1):
                a, b, c, d = rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]
                aa, bb = sk._lerp(a, d, .35), sk._lerp(b, c, .35)
                cc, dd = sk._lerp(b, c, .6), sk._lerp(a, d, .6)
                n = sk._cross(sk._sub(dd, aa), sk._sub(bb, aa))
                if n[2] < 0 and n[1] < 0:
                    n = (-n[0], -n[1], -n[2])
                g.face(tuple(sk._add(p, sk._norm(n), .004) for p in (aa, bb, cc, dd)), F['blanket-b'], n)


# ---------------------------------------------------------------------------------------------------------------------------
# The litter bin: a cast drum 0.55 m across and 1.0 m tall, a domed lid, MERIDIAN cast in a band, litter round its foot
# ---------------------------------------------------------------------------------------------------------------------------
def litter_bin(T):
    mk.finishes()
    m = Module('furn.bin', T.lod, block='bin- (0.6 m)', cap=(350, 40))
    g = m.part('body')
    iron = F['iron-green']
    n = T.pick(10, 6)
    if T.lod == 0:
        lathe(g, [(.295, 0.0), (.295, .05), (.268, .08), (.262, .71), (.262, .84), (.27, .862)], (0, 0, 0), iron, n)
        lathe(g, [(.262, .705), (.285, .72), (.285, .82), (.262, .835)], (0, 0, 0), iron, 12, smooth=False, phase=math.pi / 12)
        lathe(g, [(.27, .862), (.24, .92), (.15, .975), (.03, .998)], (0, 0, 0), iron, n)
        lathe(g, [(.03, .998), (.03, 1.015), (.001, 1.035)], (0, 0, 0), F['brass'], 6)
    else:
        lathe(g, [(.29, 0.0), (.265, .1), (.27, .86)], (0, 0, 0), iron, n)
        lathe(g, [(.27, .86), (.001, 1.02)], (0, 0, 0), iron, n)
    # the mouth: a dark opening under a hood on the lid's front
    fr = Fr((0, 0, 0), (0, 0, 1))
    if T.lod == 0:
        g.face([fr.P(-.11, .875, .262), fr.P(.11, .875, .262), fr.P(.1, .945, .218), fr.P(-.1, .945, .218)], F['void'], fr.D(0, .55, 1))
        g.face([fr.P(-.13, .95, .21), fr.P(.13, .95, .21), fr.P(.13, .97, .285), fr.P(-.13, .97, .285)], iron, fr.D(0, 1, .3))
        g.face([fr.P(-.13, .97, .285), fr.P(.13, .97, .285), fr.P(.13, .945, .292), fr.P(-.13, .945, .292)], iron, fr.D(0, -.2, 1))
        for s in (-1, 1):
            g.face([fr.P(s * .13, .95, .21), fr.P(s * .13, .945, .292), fr.P(s * .13, .97, .285)], iron, fr.D(-s, 0, 0))
        mk.text_on_drum(g, (0, 0, 0), .285, .738, .0105, 'MERIDIAN', F['brass'], 0.0, lift=.004)
        # the litter, drawn flat round its foot
        bits = [(.33, .1, .11, .07, 0.4, 'paper-d'), (-.3, .2, .09, .06, 1.2, 'paper-b'), (.12, .36, .08, .05, 2.0, 'paper-b'),
                (-.18, -.33, .1, .07, 0.8, 'paper-d'), (.36, -.16, .07, .05, 2.6, 'paper-d'), (-.38, -.05, .06, .04, 0.2, 'paper-b')]
        for (x, z, w, d, yaw, f) in bits:
            pts = [rot_y((-w, 0, -d), yaw), rot_y((w, 0, -d * .6), yaw), rot_y((w * .8, 0, d), yaw), rot_y((-w * .9, 0, d * .8), yaw)]
            g.face([(x + p[0], .006, z + p[2]) for p in pts], F[f], (0, 1, 0))
    else:
        mk.quad(g, fr, -.1, .1, .88, .94, .27, F['void'])
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# Bollards
# ---------------------------------------------------------------------------------------------------------------------------
def bollard(T, kind='cannon'):
    mk.finishes()
    if kind == 'cannon':
        m = Module('furn.bollard.cannon', T.lod, block='bollard- (0.3 m)', cap=(160, 24), note='heritage, black with a gilt ring')
        g = m.part('body')
        n = T.pick(8, 6)
        if T.lod == 0:
            lathe(g, [(.125, 0.0), (.118, .45), (.098, .69), (.118, .77)], (0, 0, 0), F['iron-black'], n)
            lathe(g, [(.118, .77), (.128, .79), (.128, .815), (.118, .83)], (0, 0, 0), F['brass'], n)
            lathe(g, [(.118, .83), (.095, .88), (.05, .93), (.001, .95)], (0, 0, 0), F['iron-black'], n)
        else:
            lathe(g, [(.125, 0.0), (.11, .78), (.001, .95)], (0, 0, 0), F['iron-black'], n)
        return m
    m = Module('furn.bollard.sleeve', T.lod, block='bollard- (0.3 m)', cap=(160, 24), note='Ward Zero, Threshold')
    g = m.part('body')
    n = T.pick(8, 6)
    if T.lod == 0:
        lathe(g, [(.13, 0.0), (.13, .05), (.115, .07)], (0, 0, 0), F['iron-black'], n)
        lathe(g, [(.1, .07), (.1, .72)], (0, 0, 0), F['galv'], n)
        lathe(g, [(.101, .72), (.101, .8)], (0, 0, 0), F['reflector'], n)
        lathe(g, [(.1, .8), (.1, .9), (.07, .95), (.001, .97)], (0, 0, 0), F['galv'], n)
    else:
        lathe(g, [(.12, 0.0), (.1, .9), (.001, .97)], (0, 0, 0), F['galv'], n)
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# The phone box: Meridian Telephones Type M (1.1 x 1.1 x 2.6 m) and the wall payphone hood
# ---------------------------------------------------------------------------------------------------------------------------
def kiosk(T):
    mk.finishes()
    m = Module('furn.kiosk.type-m', T.lod, block='kiosk- (1.2 x 2.6 x 1.2; needs the new stem from Astra)', cap=(1400, 150),
               note='door to +z; glazed three sides, cream back')
    body, lamp = m.part('body'), m.part('lamp')
    iron, cream = F['iron-green'], F['enamel-cream']
    H = .55                       # the body's half width
    mk.box(body, T, -H - .03, H + .03, 0.0, .16, -H - .03, H + .03, iron, .02, skip=('-y',))
    warm = mk.glow(0xFFC98A, 1.7)
    for k in range(4):
        yaw = k * math.pi / 2
        n = rot_y((0, 0, 1), yaw)
        fr = Fr(mk.scale(n, H), n)
        back = k == 2
        if T.lod > 0:
            mk.fbox(body, T, fr, -H, H, .16, 2.12, -.06, 0.0, cream if back else iron, skip=('-z', '-y'))
            if not back:
                mk.quad(lamp, fr, -.45, .45, .62, 1.94, .006, warm)
            continue
        # the posts are shared corners: each side draws the one on its reader's left; its parts stop short of the next
        E = H - .085
        mk.fbox(body, T, fr, -H, -E, .16, 2.12, -.085, 0.0, iron, .012)
        # kick panel, its moulding, the top rail
        mk.fbox(body, None, fr, -E, E, .16, .6, -.035, 0.0, iron, 0.0, skip=('-z',))
        mk.fbox(body, None, fr, -.36, .36, .25, .5, 0.0, .012, iron, 0.0, skip=('-z',))
        mk.fbox(body, None, fr, -E, E, 1.95, 2.12, -.035, 0.0, iron, 0.0, skip=('-z',))
        if back:
            mk.fbox(body, None, fr, -E, E, .6, 1.95, -.03, 0.0, cream, 0.0, skip=('-z',))
            mk.crest(body, T, fr, 0.0, 1.42, .15, .006, cream, F['brass'], F['ink'], F['enamel-green'])
            mk.fbox(body, None, fr, -.3, .3, 1.02, 1.16, 0.0, .01, F['enamel-green'], 0.0, skip=('-z',))
            mk.text(body, fr, 0.0, 1.054, .011, 'MERIDIAN', cream, c=.016)
            continue
        # the lit glazing behind its bars (small panes: 3 across, 6 up)
        mk.quad(lamp, fr, -E, E, .6, 1.95, -.02, warm)
        mk.fbox(body, None, fr, -E, E, .58, .62, -.03, .02, iron, 0.0, skip=('-z',))
        for a in (-.155, .155):
            mk.fbox(body, None, fr, a - .022, a + .022, .62, 1.95, -.02, .008, iron, 0.0, skip=('-z',))
        for b in (.845, 1.07, 1.295, 1.52, 1.745):
            mk.fbox(body, None, fr, -E, E, b - .02, b + .02, -.02, .006, iron, 0.0, skip=('-z',))
        if k == 0:
            # the door's brass pull
            mk.rod(body, fr.P(.38, .95, .05), fr.P(.38, 1.28, .05), .014, F['brass'], 6)
            for b in (.98, 1.25):
                mk.rod(body, fr.P(.38, b, .0), fr.P(.38, b, .05), .01, F['brass'], 4, caps=False)
        if k == 1:
            # the telephone and its handset on the hook, seen through the lit glass
            ink = F['ink']
            c = -.012
            mk.quad(body, fr, -.06, .1, 1.16, 1.44, c, ink)
            mk.quad(body, fr, -.16, -.12, 1.15, 1.42, c, ink)
            mk.quad(body, fr, -.185, -.1, 1.37, 1.44, c, ink)
            mk.quad(body, fr, -.185, -.1, 1.12, 1.19, c, ink)
            mk.poly(body, fr, [(-.14, 1.12), (-.12, 1.12), (-.02, 1.0), (-.04, 1.0)], c, ink)
            mk.poly(body, fr, [(-.04, 1.0), (-.02, 1.0), (.02, 1.16), (0.0, 1.16)], c, ink)
    # the TELEPHONE sign round the canopy: teal enamel, lit, with cream letters
    teal = mk.glow(mk.TEAL_SIGN, .9)
    letters = mk.glow(mk.OPAL, 2.4)
    for k in range(4):
        n = rot_y((0, 0, 1), k * math.pi / 2)
        fr = Fr(mk.scale(n, H), n)
        mk.fbox(body, None, fr, -H - .02, H + .02, 2.12, 2.35, -.02, .03, iron, 0.0)
        mk.quad(lamp, fr, -H + .02, H - .02, 2.145, 2.325, .036, teal)
        if T.lod == 0:
            mk.text(lamp, fr, 0.0, 2.172, .0175, 'TELEPHONE', letters, c=.042)
    # the octagonal canopy roof (never a dome with a crown) and its knob
    n8 = T.pick(8, 8)
    k8 = 1 / math.cos(math.pi / 8)
    if T.lod == 0:
        lathe(body, [(.79 * k8, 2.35), (.79 * k8, 2.39)], (0, 0, 0), iron, n8, smooth=False, phase=math.pi / 8)
        lathe(body, [(.79 * k8, 2.39), (.69 * k8, 2.425), (.45 * k8, 2.5), (.16 * k8, 2.575), (.07 * k8, 2.59)], (0, 0, 0), F['iron-green'],
              n8, smooth=False, phase=math.pi / 8, cap1=True)
        body.face([(.79 * k8 * math.sin(math.pi / 8 + 2 * math.pi * i / 8), 2.35, .79 * k8 * math.cos(math.pi / 8 + 2 * math.pi * i / 8))
                   for i in range(8)], iron, (0, -1, 0))
        lathe(body, [(.05, 2.59), (.03, 2.615), (.04, 2.645), (.001, 2.68)], (0, 0, 0), F['brass'], 6)
    else:
        lathe(body, [(.79 * k8, 2.35), (.79 * k8, 2.39), (.001, 2.6)], (0, 0, 0), iron, n8, smooth=False, phase=math.pi / 8)
    m.light((0.0, 1.95, 0.0), mk.TUNGSTEN, 4.0, kind='practical', note='the kiosk lit inside')
    return m


def payphone_hood(T):
    """The interim wall payphone hood (0.25 m deep or less) until Astra's kiosk- stem exists."""
    mk.finishes()
    m = Module('furn.kiosk.hood', T.lod, mount='wall', rest=(0.0, 0.0, 0.0), block='a facade (0.25 m rule)', cap=(700, 70),
               note='origin at the wall face at street level')
    body, lamp = m.part('body'), m.part('lamp')
    iron, cream = F['iron-green'], F['enamel-cream']
    fr = Fr((0, 0, 0), (0, 0, 1))
    mk.fbox(body, T, fr, -.34, .34, .95, 2.06, 0.0, .02, cream, .006, skip=('-z',))
    # the hood: two cheeks and a curved canopy
    prof = [(0.0, 2.11), (.08, 2.095), (.16, 2.04), (.215, 1.95), (.235, 1.8)]
    cheek = [(0.0, 1.0), (.2, 1.0), (.235, 1.8), (.215, 1.95), (.16, 2.04), (.08, 2.095), (0.0, 2.11)]
    if T.lod > 0:
        prof = [prof[0], prof[2], prof[4]]
        cheek = [(0.0, 1.0), (.2, 1.0), (.235, 1.8), (.16, 2.04), (0.0, 2.11)]
    for s in (-1, 1):
        for (x, nx) in ((s * .37, s), (s * .35, -s)):
            body.face([(x, b, c) for (c, b) in cheek], iron, (nx, 0, 0))
        for i in range(len(cheek)):
            (c0, b0), (c1, b1) = cheek[i], cheek[(i + 1) % len(cheek)]
            if c0 == 0.0 and c1 == 0.0:
                continue
            nn = (0.0, (c1 - c0), -(b1 - b0))
            mid = ((c0 + c1) / 2 - .1, (b0 + b1) / 2 - 1.5)
            if nn[2] * mid[0] + nn[1] * mid[1] < 0:
                nn = (0.0, -nn[1], -nn[2])
            body.face([(s * .35, b0, c0), (s * .37, b0, c0), (s * .37, b1, c1), (s * .35, b1, c1)], iron, nn)
    for i in range(len(prof) - 1):
        (c0, b0), (c1, b1) = prof[i], prof[i + 1]
        nn = sk._norm((0.0, c1 - c0, -(b1 - b0)))
        if nn[1] < 0:
            nn = (0.0, -nn[1], -nn[2])
        body.face([(-.37, b0 + .015, c0), (.37, b0 + .015, c0), (.37, b1 + .015, c1), (-.37, b1 + .015, c1)], iron, nn)
        body.face([(-.35, b0, c0), (.35, b0, c0), (.35, b1, c1), (-.35, b1, c1)], cream, (0.0, -nn[1], -nn[2]))
    # the payphone and its handset on the hook
    mk.fbox(body, T, fr, -.11, .11, 1.25, 1.62, .02, .13, F['iron-black'], .012, skip=('-z',))
    if T.lod == 0:
        mk.quad(body, fr, -.07, .07, 1.53, 1.58, .134, F['void'])
        mk.disc(body, fr.P(.0, 1.39, .134), .065, F['chrome'], 12, fr.n)
        mk.disc(body, fr.P(.0, 1.39, .14), .025, F['iron-black'], 8, fr.n)
        hx = -.165
        mk.rod(body, fr.P(hx, 1.33, .1), fr.P(hx, 1.55, .1), .016, F['iron-black'], 6)
        mk.box(body, T, -hx - .04, -hx + .04, 1.52, 1.6, .06, .14, F['iron-black'], .012)
        mk.box(body, T, -hx - .04, -hx + .04, 1.28, 1.36, .06, .14, F['iron-black'], .012)
        cord = [fr.P(hx, 1.29, .1), fr.P(hx + .02, 1.2, .12), fr.P(hx + .07, 1.16, .12), fr.P(hx + .12, 1.2, .1), fr.P(.0, 1.25, .1)]
        mk.tube(body, mk.catmull(cord, 2), .008, F['iron-black'], 4, caps=False)
    # the lit sign on top and the lamp under the hood
    mk.fbox(body, T, fr, -.33, .33, 2.13, 2.29, 0.0, .07, iron, .01)
    mk.quad(lamp, fr, -.31, .31, 2.145, 2.275, .076, mk.glow(mk.TEAL_SIGN, .9))
    if T.lod == 0:
        mk.text(lamp, fr, 0.0, 2.165, .0125, 'TELEPHONE', mk.glow(mk.OPAL, 2.4), c=.082)
        mk.disc(lamp, (0.0, 2.035, .13), .05, mk.glow(mk.TUNGSTEN, 2.2), 8, (0, -1, 0))
    m.light((0.0, 1.9, .2), mk.TUNGSTEN, 3.0, kind='practical')
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# The bus stop (3.6 x 1.6 x 2.6 m) and its tram twin
# ---------------------------------------------------------------------------------------------------------------------------
def _canopy_y(z):
    """The canopy's underside over z: a shallow arc, higher at the front (the kerb, +z)."""
    base = 2.40 + .12 * (z + .86) / 1.72
    return base + .16 * (1 - (z / .86) ** 2)


def stop(T, tram=False):
    mk.finishes()
    name = 'furn.stop.' + ('tram' if tram else 'bus')
    m = Module(name, T.lod, block='shelter- (3.6 x 1.6)', cap=(1800, 180), note='flag post 0.2 m off the block\'s end')
    body, lamp = m.part('body'), m.part('lamp')
    iron = F['paint-maroon'] if tram else F['iron-green']
    plate_ground = F['paint-cream'] if tram else F['enamel-cream']
    band = iron
    glass = F['glass-frosted']
    n = T.pick(6, 4)
    posts = [(-1.72, .72), (1.72, .72), (-1.72, -.72), (0.0, -.72), (1.72, -.72)]
    for (x, z) in posts:
        top = _canopy_y(z) - .04
        if T.lod == 0:
            lathe(body, [(.075, 0.0), (.07, .06), (.048, .14)], (x, 0, z), iron, n, phase=math.pi / n)
            mk.rod(body, (x, .14, z), (x, top - .05, z), .045, iron, n, caps=False)
            lathe(body, [(.05, top - .05), (.068, top)], (x, 0, z), iron, n, cap1=True, phase=math.pi / n)
        else:
            mk.rod(body, (x, 0.0, z), (x, top, z), .05, iron, 4, caps=False)
    # the canopy: ribs over the posts' lines and the milky glass between
    zs = [.86 - 1.72 * i / T.pick(6, 2) for i in range(T.pick(6, 2) + 1)]
    xs = [-1.86, -1.72, -.57, .57, 1.72, 1.86] if T.lod == 0 else [-1.86, 1.86]
    for i in range(len(xs) - 1):
        for j in range(len(zs) - 1):
            za, zb = zs[j], zs[j + 1]
            ya, yb = _canopy_y(za) + .02, _canopy_y(zb) + .02
            nrm = sk._norm((0.0, za - zb, yb - ya))
            if nrm[1] < 0:
                nrm = (0.0, -nrm[1], -nrm[2])
            body.face([(xs[i], ya, za), (xs[i + 1], ya, za), (xs[i + 1], yb, zb), (xs[i], yb, zb)], glass, nrm)
            body.face([(xs[i], ya - .012, za), (xs[i + 1], ya - .012, za), (xs[i + 1], yb - .012, zb), (xs[i], yb - .012, zb)], glass,
                      (0.0, -nrm[1], -nrm[2]))
    if T.lod == 0:
        for x in (-1.72, -.57, .57, 1.72):
            path = [(x, _canopy_y(z) - .015, z) for z in [.9 - 1.8 * i / 5 for i in range(6)]]
            mk.bar(body, path, .05, .045, iron, up=(1, 0, 0))
    # the fascia beams along the front and back edges
    for (z, sgn) in ((.86, 1), (-.86, -1)):
        y = _canopy_y(z)
        mk.box(body, None, -1.88, 1.88, y - .11, y + .03, z - sgn * .05, z + sgn * .02, iron, 0.0)
    # the side screens and the back screen: frosted glass in iron frames
    zb = -.72
    wood = F['timber-old']
    if T.lod > 0:
        for sx in (-1, 1):
            mk.box(body, None, sx * 1.72 - .02, sx * 1.72 + .02, .1, 2.22, -.72, .3, glass, 0.0)
        mk.box(body, None, -1.72, 1.72, .1, 2.22, zb - .02, zb + .02, glass, 0.0)
        mk.box(body, None, -1.45, .85, .42, .48, -.7, -.4, wood, 0.0)
    else:
        for sx in (-1, 1):
            x = sx * 1.72
            for (z0, z1) in ((-.72, .3),):
                mk.box(body, None, x - .02, x + .02, .1, .16, z0, z1, iron, 0.0)
                mk.box(body, None, x - .02, x + .02, 2.16, 2.22, z0, z1, iron, 0.0)
                mk.box(body, None, x - .025, x + .025, .1, 2.22, z1 - .05, z1, iron, 0.0)
                for xx, nx in ((x - .008, -1), (x + .008, 1)):
                    body.face([(xx, .16, z0), (xx, .16, z1 - .05), (xx, 2.16, z1 - .05), (xx, 2.16, z0)], glass, (nx, 0, 0))
        mk.box(body, None, -1.72, 1.72, .1, .16, zb - .02, zb + .02, iron, 0.0)
        mk.box(body, None, -1.72, 1.72, 2.16, 2.22, zb - .02, zb + .02, iron, 0.0)
        for zz, nz in ((zb - .008, -1), (zb + .008, 1)):
            body.face([(-1.72, .16, zz), (1.72, .16, zz), (1.72, 2.16, zz), (-1.72, 2.16, zz)], glass, (0, 0, nz))
        mk.box(body, None, -1.72, 1.72, 1.02, 1.06, zb - .02, zb + .02, iron, 0.0)
        # the bench inside
        for x in (-1.3, .7):
            mk.box(body, None, x - .025, x + .025, .0, .45, -.66, -.62, iron, 0.0, skip=('-y',))
            mk.box(body, None, x - .025, x + .025, .42, .45, -.7, -.38, iron, 0.0)
        for z in (-.66, -.55, -.44):
            mk.slat(body, (-1.45, .465, z), (.85, .465, z), .09, .03, wood, up=(0, 1, 0), T=T)
    # the lit timetable case on the back screen, facing the kerb: LAST BUS 18:47
    fr = Fr((1.27, 0.0, zb + .02), (0, 0, 1))
    mk.fbox(body, T, fr, -.34, .34, .98, 2.02, 0.0, .07, iron, .012)
    mk.quad(lamp, fr, -.3, .3, 1.02, 1.98, .076, mk.glow(mk.OPAL, 1.8))
    ink = F['ink']
    if T.lod == 0:
        word = 'LAST TRAM' if tram else 'LAST BUS'
        mk.text(body, fr, 0.0, 1.76, .012 if not tram else .0105, word, ink, c=.082)
        mk.text(body, fr, 0.0, 1.5, .022, '18:47', F['enamel-red'], c=.082)
        mk.text(body, fr, 0.0, 1.89, .008, 'ROUTE 647' if not tram else 'TRAM 6', ink, c=.082)
        for i in range(6):
            b = 1.1 + i * .06
            mk.quad(body, fr, -.26, -.1, b, b + .012, .082, ink)
            for a in (-.04, .06, .16):
                mk.quad(body, fr, a, a + .07, b, b + .012, .082, ink)
    # the canopy lamp under the front beam
    yl = _canopy_y(.72) - .12
    if T.lod == 0:
        mk.rod(lamp, (-.5, yl, .74), (.5, yl, .74), .022, mk.glow(mk.BRIGHT_ROOM, 2.2), 6, caps=True)
    else:
        lamp.face([(-.5, yl, .72), (.5, yl, .72), (.5, yl, .76), (-.5, yl, .76)], mk.glow(mk.BRIGHT_ROOM, 2.2), (0, -1, 0))
    m.light((0.0, yl - .1, .6), mk.BRIGHT_ROOM, 5.0, kind='practical', note='the shelter lamp')
    m.light((1.27, 1.5, zb + .3), mk.OPAL, 2.5, kind='practical', note='the timetable case')
    # the flag post and its enamel plate: BUS STOP, the transit mark, the routes
    px, pz = -2.0, .72
    if T.lod == 0:
        lathe(body, [(.07, 0.0), (.07, .04), (.05, .07)], (px, 0, pz), iron, 8)
        mk.rod(body, (px, .07, pz), (px, 3.12, pz), .04, iron, 8, caps=False)
        mk.ellipsoid(body, (px, 3.14, pz), ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.045, .045, .05), iron, 8, 3, top=1.0, bottom=-.3)
    else:
        mk.rod(body, (px, 0.0, pz), (px, 3.12, pz), .045, iron, 4, caps=False)
    b0, b1 = 2.55, 3.05
    a_in, a_out = pz + .05, pz + .6
    mk.box(body, T, px - .012, px + .012, b0, b1, a_in, a_out, plate_ground, .006)
    for sgn in (-1, 1):
        fr = Fr((px + sgn * .012, 0.0, (a_in + a_out) / 2), (sgn, 0, 0))
        mk.quad(body, fr, -.275, .275, 2.9, 3.05, .006, band)
        if T.lod == 0:
            mk.text(body, fr, 0.0, 2.94, .0105, 'TRAM STOP' if tram else 'BUS STOP', plate_ground, c=.012)
            mk.transit_mark(body, T, fr, 0.0, 2.76, .085, .006, band, F['ink'])
            mk.text(body, fr, 0.0, 2.585, .0078, '6 * 12 * 31' if tram else '647 * 12 * 31', F['ink'], c=.006)
        else:
            mk.quad(body, fr, -.09, .09, 2.68, 2.85, .006, band)
    return m


# ---------------------------------------------------------------------------------------------------------------------------
# Railings (per 2.5 m bay)
# ---------------------------------------------------------------------------------------------------------------------------
def railing(T, kind='spear'):
    mk.finishes()
    if kind == 'spear':
        m = Module('furn.railing.spear', T.lod, block='barrier- (2.5 m bay)', cap=(500, 60), note='cast iron, gilt spear tips; per 2.5 m')
        g = m.part('body')
        black, gilt = F['iron-black'], F['brass']
        for s in (-1, 1):
            x = s * 1.215
            if T.lod > 0:
                mk.rod(g, (x, 0.0, 0.0), (x, 1.12, 0.0), .04, black, 4, caps=False)
                continue
            mk.box(g, T, x - .035, x + .035, 0.0, 1.1, -.035, .035, black, .008, skip=('-y',))
            if T.lod == 0:
                mk.box(g, None, x - .055, x + .055, 0.0, .05, -.055, .055, black, 0.0, skip=('-y',))
                lathe(g, [(.03, 1.1), (.045, 1.14), (.02, 1.19), (.001, 1.22)], (x, 0, 0), gilt, 6)
        for y0, y1 in ((.1, .14), (.92, .96)):
            if T.lod > 0 and y0 < .5:
                for nz in (-1, 1):
                    g.face([(-1.18, y0, nz * .022), (1.18, y0, nz * .022), (1.18, y1, nz * .022), (-1.18, y1, nz * .022)], black, (0, 0, nz))
                continue
            mk.box(g, None, -1.18, 1.18, y0, y1, -.022, .022, black, 0.0)
        nb = 15
        for i in range(nb):
            x = -1.08 + 2.16 * i / (nb - 1)
            if T.lod == 0:
                lathe(g, [(.0175 * 1.414, .1), (.0175 * 1.414, 1.0)], (x, 0, 0), black, 4, smooth=False, phase=math.pi / 4)
                lathe(g, [(.018 * 1.414, 1.0), (.03 * 1.414, 1.03)], (x, 0, 0), gilt, 4, smooth=False, phase=0.0)
                mk.spike(g, (x, 1.03, 0), .03 * 1.414, .08, 4, gilt)
            elif i % 2 == 1:
                for nz in (-1, 1):
                    g.face([(x - .02, .14, nz * .01), (x + .02, .14, nz * .01), (x + .02, 1.08, nz * .01), (x - .02, 1.08, nz * .01)], black,
                           (0, 0, nz))
        return m
    m = Module('furn.railing.guard', T.lod, block='barrier- (2.5 m bay)', cap=(300, 40), note='galvanised, painted green; per 2.5 m')
    g = m.part('body')
    green = F['iron-green']
    n = T.pick(8, 4)
    for s in (-1, 1):
        x = s * 1.2
        mk.rod(g, (x, 0.0, 0.0), (x, 1.0, 0.0), .035, green, n, caps=False)
        if T.lod == 0:
            mk.ellipsoid(g, (x, 1.0, 0.0), ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.04, .04, .035), green, n, 2, top=1.0, bottom=0.0)
            lathe(g, [(.06, 0.0), (.05, .03), (.036, .05)], (x, 0, 0), green, n)
    for y in (.96, .5):
        mk.rod(g, (-1.25, y, 0.0), (1.25, y, 0.0), .028, green, n, caps=T.lod == 0)
    if T.lod == 0:
        for i in range(9):
            x = -1.0 + 2.0 * i / 8
            mk.rod(g, (x, .5, 0.0), (x, .96, 0.0), .012, green, 4, caps=False)
    return m
