"""The menu rooftop's city (docs/MENU-ROOFTOP-ART.md): the towers the loop's camera sees to the middle distance, the actors' roofs,
their bridge and huts and the holo screens' frames, modelled on the pieces Core NativeRooftopCity builds (the layout's notes).
Walls go to the facade faces (rooftop_kit.Facade: the menu's facade shader draws their bays and rooms); everything else is
solid, finished geometry. Unity's frame throughout.

A tower keeps Core's lot, heights and setbacks (so the composition and every sight line hold) and takes a shape of its own
inside them: glazed towers with rounded corners, a raked or lanterned crown and fins; corporate towers stepping back on moulded
ledges under a ziggurat top, corner tubes, a crown ring or a helipad; residential slabs with balconies, condensers, tanks and
billboards; masonry mid-rises with a bracketed cornice, fire escapes and hanging signs."""
import math

import stagekit as sk
import cybercity_kit as ck
import rooftop_kit as rk
from rooftop_kit import F, glow, h01, pick, Fr, rod, box, lathe, sweep, GLAZED, RESIDENTIAL, CORPORATE, MASONRY

WORDS = ('NOVA', 'HELIX', 'VEGA', 'ORBIT', 'KITE', 'PULSE', 'RAMEN', 'ARCADE', 'HOTEL', 'SYNTH', 'CHIPS', 'DUEL', 'PILOT', '24H', 'TAXI', 'CLUB',
         'NOODLE', 'VOLT', 'BYTE', 'ECHO', 'LUNA', 'APEX', 'ONYX', 'DRIFT', 'ZEN', 'BAR')
SHORT = ('BAR', 'ZEN', '24H', 'VOLT', 'NOVA', 'LUNA', 'ECHO', 'BYTE', 'CLUB', 'TAXI', 'KITE', 'VEGA', 'APEX', 'ONYX')


class Ctx:
    """The tier being built and the lot's distance class: `fine` the High tier, `near` a lot within Core's near detail."""

    def __init__(self, T, near):
        self.T, self.near = T, near
        self.fine = T.i == 0
        self.full = self.fine and near                 # everything
        self.rs = (6, 5, 4)[T.i] if near else 4        # sides of a round member
        self.ts = 4 if (self.fine and near) else 3     # sides of a thin tube
        self.o = .03 if near else .12                  # a layer laid on a face (clear of the depth buffer's step at the lot's range)
        self.close = False                             # within 170 m: the smallest pieces (slats, treads, rail posts)


def inside(fp, x, z, margin=0.0):
    n = len(fp)
    for i in range(n):
        a, b = fp[i], fp[(i + 1) % n]
        ex, ez = b[0] - a[0], b[1] - a[1]
        l = math.hypot(ex, ez)
        if l < 1e-6:
            continue
        # outward normal (ez, -ex): inside when the point lies at least `margin` behind every edge
        if ((x - a[0]) * ez - (z - a[1]) * ex) / l > -margin:
            return False
    return True


def lbox(g, fr, u0, u1, v0, v1, d0, d1, f, bevel=0.0, T=None):
    """A box in a face frame of any heading (Fr.box needs an axis-aligned frame)."""
    n = fr.n
    if abs(n[0]) < 1e-6 or abs(n[2]) < 1e-6:
        fr.box(g, u0, u1, v0, v1, d0, d1, f, T, bevel=bevel, cell=0)
        return
    t = sk.Geo()
    box(t, -(u1 - u0) / 2, (u1 - u0) / 2, 0, v1 - v0, -(d1 - d0) / 2, (d1 - d0) / 2, f, T, bevel, cell=0)
    ang = math.atan2(n[0], n[2])
    # local +x goes to -ud (the reader's left): a box is symmetric, so only its centre matters
    c = fr.at((u0 + u1) / 2, v0, (d0 + d1) / 2)
    g.extend(t.transformed(sk.affine(sk.quat_axis(0, 1, 0, ang), c)))


def tube_path(g, pts, r, f, sides, closed=False):
    sweep(g, sk.circle_section(r, sides), pts, f, closed=closed, caps=False)


# ---------------------------------------------------------------------------------------------------------------------------
# Towers
# ---------------------------------------------------------------------------------------------------------------------------
def shape(lot, tier, C):
    """A tier's footprint: Core's rectangle with its chamfer; glazed towers round their corners generously."""
    x0, x1, z0, z1, ch, y0, y1 = tier
    if lot.bare:
        return rk.rect_fp(x0, x1, z0, z1, 0.0)
    if lot.style == GLAZED and ch > .01:
        r = min(ch * 2.2, min(x1 - x0, z1 - z0) * .46)
        return rk.rect_fp(x0, x1, z0, z1, r, (4 if C.near else 3) if C.fine else 2)
    if lot.style == CORPORATE and ch > .01:
        return rk.rect_fp(x0, x1, z0, z1, ch, 0)
    return rk.rect_fp(x0, x1, z0, z1, 0.0)


def build_lot(L, lot, T, g, fc):
    C = Ctx(T, lot.detail >= 2 or lot.bare)
    C.close = C.full and L.dist(lot.c[0], lot.c[1]) < 170
    sid = int(lot.seed) * 31 + lot.i
    fps = []
    nt = len(lot.tiers)
    for k, tier in enumerate(lot.tiers):
        fp = shape(lot, tier, C)
        fps.append(fp)
        y0, y1 = tier[5], tier[6]
        tw = rk.tower(lot, top=y1)
        last = k == nt - 1
        raked = None
        if last and lot.style == GLAZED and not lot.bare and lot.detail >= 1 and not lot.all('lantern') and not lot.all('fins'):
            pass
        fc.ring(fp, y0, y1, tw)
        if not last:
            # the setback's roof and its moulded ledge
            rk.cap(g, fp, y1, F['membrane'])
            if lot.detail >= 1:
                ledge(g, fp, y1, C, lot.style)
    top_fp = fps[-1]
    top = lot.top
    lot.fps = fps
    rk.cap(g, top_fp, top, F['membrane'])
    if lot.bare:
        bare_roof(L, lot, g, top_fp, top, C)
        trims(lot, g, fps, C)
        return
    if lot.detail >= 1:
        parapet(g, top_fp, top, C, lot.style)
    safe = rk.grow(top_fp, -1.2)
    for p in lot.all('plant'):
        if inside(safe, p[0], p[1]):
            plant(g, p, C, sid)
    for h in lot.all('stairhut'):
        if inside(safe, h[0] + 1.8, h[1] + 1.3):
            stairhut(g, h[0], h[0] + 3.6, h[1], h[1] + 2.6, h[2], C, sid)
    for v in lot.all('lantern'):
        lantern(lot, g, fc, v, C)
    for v in lot.all('mast'):
        mast(g, v, C, sid)
    if lot.all('fins'):
        fins(g, top_fp, L.street, top, C)
    if lot.style == GLAZED:
        crownband(g, top_fp, top, C)
    for v in lot.all('cornerneon'):
        cornerneon(lot, g, v, C)
    for v in lot.all('crown'):
        crown(g, top_fp, v, C)
    for v in lot.all('helipad'):
        helipad(g, v, C)
    for v in lot.all('tank'):
        if inside(rk.grow(top_fp, -.5), v[0], v[2]):
            tank(g, v, C)
    for v in lot.all('billboard'):
        billboard(g, v, C, sid)
    for v in lot.all('cornice'):
        cornice(g, top_fp, v[0], C)
    for v in lot.all('balcony'):
        balcony(g, v, C, sid)
    for v in (lot.all('aircon') if C.fine else ()):
        aircon(g, v, C)
    for v in lot.all('fireescape'):
        fireescape(g, v, C)
    for v in lot.all('hangsign'):
        hangsign(g, v, C, sid)
    trims(lot, g, fps, C)


def ledge(g, fp, y, C, style):
    """A setback's ledge: a two-step moulding under a coping (one band far off)."""
    pale, dark = F['ledge'], F['ledgedark']
    if not C.fine:
        rk.band(g, fp, y - .55, y + .12, pale, top=pale, bottom=dark, d0=.7)
        return
    rk.band(g, fp, y - .6, y - .22, dark, bottom=dark, d0=.32)
    rk.band(g, rk.grow(fp, .32), y - .22, y + .0, pale, bottom=dark, d0=.38)
    rk.band(g, fp, y + .0, y + .14, F['coping'], top=F['coping'], d0=.78)


def parapet(g, fp, y, C, style):
    """The roof's parapet: its outer and inner face, an overhanging coping."""
    h, th = 1.1, .35
    wall = F['resi'] if style == RESIDENTIAL else F['masonry'] if style == MASONRY else F['parapet']
    inner = rk.grow(fp, -th)
    rk.wall_ring(g, fp, y, y + h - .1, wall)
    rk.wall_ring(g, inner, y, y + h - .1, F['parapet'], inward=True)
    o, i2 = rk.grow(fp, .09), rk.grow(fp, -th - .06)
    n = len(fp)
    cop = F['coping']
    for k in range(n):
        j = (k + 1) % n
        g.face(((o[k][0], y + h, o[k][1]), (o[j][0], y + h, o[j][1]), (i2[j][0], y + h, i2[j][1]), (i2[k][0], y + h, i2[k][1])), cop, (0, 1, 0))
    rk.wall_ring(g, o, y + h - .1, y + h, cop)
    if C.fine:
        rk.wall_ring(g, i2, y + h - .1, y + h, cop, inward=True)
        for k in range(n):
            j = (k + 1) % n
            g.face(((o[k][0], y + h - .1, o[k][1]), (o[j][0], y + h - .1, o[j][1]), (fp[j][0], y + h - .1, fp[j][1]), (fp[k][0], y + h - .1, fp[k][1])), F['ledgedark'], (0, -1, 0))


def crownband(g, fp, y, C):
    """A glazed tower's top edge: a dark metal band standing a little proud under the parapet."""
    rk.band(g, fp, y - 1.6, y + .02, F['mullion'], bottom=F['spandrel'], d0=.22)


def fins(g, fp, y0, y1, C):
    """Vertical fins up a rectangular glazed tower: its corners and the thirds of each face, standing .5 m proud."""
    if len(fp) != 4:
        return
    f = F['trimlight']
    for k in range(4):
        a, b = fp[k], fp[(k + 1) % 4]
        fr = rk.edge_frame(a, b, y0)
        for j in range(4 if k % 2 == 0 else 3):
            if k % 2 == 1 and j == 0:
                continue
            u = -fr.half + 2 * fr.half * j / 3
            fr.box(g, u - .14, u + .14, 0, y1 + .9 - y0, -.05, .5, f, None, bevel=0, cell=0, skip=('-y',))


def lantern(lot, g, fc, v, C):
    """A glazed tower's lantern: its lit storeys under a hipped glass roof with metal hips."""
    x0, x1, z0, z1, ch, top, h, seed, share = v
    fp = rk.rect_fp(x0, x1, z0, z1, ch, 0)
    fc.ring(fp, top, top + h, rk.tower(lot, top=top + h, seed=seed, share=share, style=GLAZED))
    rk.band(g, fp, top + h - .1, top + h + .25, F['mullion'], top=F['mullion'], bottom=F['spandrel'], d0=.3)
    rise = min(4.5, (x1 - x0) * .22)
    apex = rk.grow(fp, -min(x1 - x0, z1 - z0) * .32)
    n = len(fp)
    yb, yt = top + h + .25, top + h + .25 + rise
    for k in range(n):
        j = (k + 1) % n
        g.face(((fp[k][0], yb, fp[k][1]), (fp[j][0], yb, fp[j][1]), (apex[j][0], yt, apex[j][1]), (apex[k][0], yt, apex[k][1])), F['roofglass'])
        if C.fine:
            rod(g, (fp[k][0], yb + .05, fp[k][1]), (apex[k][0], yt + .05, apex[k][1]), .12, F['mullion'], 4, caps=False)
    rk.cap(g, apex, yt, F['mullion'])


def mast(g, v, C, sid):
    """A lattice mast (three legs to a pole, rings and diagonals, antenna panels and a dish near) under its beacon."""
    x, y, z, h, detail = v
    dark, steel = F['metaldark'], F['galv']
    top = (x, y + h, z)
    if detail < 1 or not C.fine:
        rod(g, (x, y, z), top, .3 if detail < 1 else .22, dark, 3, caps=False)
    else:
        b = .9
        legs = [(x + b * math.cos(a), y, z + b * math.sin(a)) for a in (0, 2.094, 4.189)]
        tip = (x, y + h * .75, z)
        for p in legs:
            rod(g, p, tip, .075, dark, 3, caps=False)
        rings = 4 if C.near else 2
        for k in range(1, rings + 1):
            t = k / (rings + 1)
            pts = [sk._lerp(p, tip, t) for p in legs]
            for i in range(3):
                rod(g, pts[i], pts[(i + 1) % 3], .04, dark, 3, caps=False)
                if C.near:
                    lo = sk._lerp(legs[i], tip, (k - 1) / (rings + 1))
                    rod(g, lo, pts[(i + 1) % 3], .03, dark, 3, caps=False)
        rod(g, tip, top, .06, steel, 4, caps=False)
        if C.near:
            # antenna panels on the upper legs and a small dish
            for i in range(3):
                p = sk._lerp(legs[i], tip, .72)
                box(g, p[0] - .12, p[0] + .12, p[1] - .7, p[1] + .7, p[2] - .12, p[2] + .12, F['creme'], None, 0, cell=0)
            d = sk._lerp(legs[0], tip, .5)
            ck.dome(g, (d[0] + .3, d[1], d[2]), .55, steel, 8, 2, axis=(1, 0, 0), squash=.35)
    # the beacon's lamp (Core's blinking glow sits just above)
    box(g, x - .14, x + .14, y + h - .05, y + h + .3, z - .14, z + .14, glow(rk.RED, 1.6), None, 0, cell=0)


def cornerneon(lot, g, v, C):
    """Neon tubes up a corporate tower's corners, each tier's own, in the tower's hue."""
    y0, y1, hue = v
    col = rk.CYAN if hue < .55 else rk.MAGENTA
    f = glow(col, 2.2)
    for k, tier in enumerate(lot.tiers):
        fp = lot.fps[k]
        a, b = max(y0, tier[5] + .5), tier[6]
        if b - a < 1:
            continue
        out = rk.grow(fp, .16)
        step = 1 if len(fp) == 4 else 2
        for i in range(0, len(fp), step):
            p = out[i] if len(fp) == 4 else ((out[i][0] + out[i - 1][0]) / 2, (out[i][1] + out[i - 1][1]) / 2)
            rod(g, (p[0], a, p[1]), (p[0], b, p[1]), .16, f, C.ts, caps=False)


def crown(g, fp, v, C):
    """A corporate crown: a lit ring frame on posts over the roof, braced."""
    y, h, r_, g_, b_ = v
    col, gain = rk.hexlin(r_, g_, b_)
    ring = rk.grow(fp, -1.5)
    n = len(ring)
    dark = F['metaldark']
    for k in range(n):
        a, b = ring[k], ring[(k + 1) % n]
        box(g, a[0] - .16, a[0] + .16, y, y + h, a[1] - .16, a[1] + .16, dark, None, 0, cell=0, skip=('-y',))
        rod(g, (a[0], y + h, a[1]), (b[0], y + h, b[1]), .2, dark, 4, caps=False)
        rod(g, (a[0], y + h - .32, a[1]), (b[0], y + h - .32, b[1]), .13, glow(col, gain * .95), C.ts, caps=False)
        if C.fine:
            m = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
            rod(g, (a[0], y + h * .45, a[1]), (m[0], y + h, m[1]), .07, dark, 3, caps=False)
            rod(g, (b[0], y + h * .45, b[1]), (m[0], y + h, m[1]), .07, dark, 3, caps=False)


def helipad(g, v, C):
    """A helipad: the deck on a drum and struts, a safety net round it, the painted ring, the lit H and the edge lamps."""
    cx, cz, r, y = v
    sides = 16 if C.fine else 12
    off = .04 if C.near else .12   # a painted layer over the deck (the depth buffer's step at the lot's range)
    deck = rk.round_fp(cx, cz, r, r, sides, .13)
    rk.band(g, deck, y - .4, y, F['metaldark'], bottom=F['metaldark'])
    rk.cap(g, deck, y, F['helideck'])
    if C.fine:
        # the net: a ring tilting out and down from the deck's edge
        net = rk.round_fp(cx, cz, r + 1.1, r + 1.1, sides, .13)
        for k in range(sides):
            j = (k + 1) % sides
            g.face(((deck[k][0], y - .1, deck[k][1]), (deck[j][0], y - .1, deck[j][1]), (net[j][0], y - .45, net[j][1]), (net[k][0], y - .45, net[k][1])), F['grate'], (0, 1, 0))
        # the painted ring
        ro, ri = r * .78, r * .7
        for k in range(sides):
            a0, a1 = 2 * math.pi * k / sides, 2 * math.pi * (k + 1) / sides
            g.face(((cx + ro * math.cos(a0), y + off, cz + ro * math.sin(a0)), (cx + ro * math.cos(a1), y + off, cz + ro * math.sin(a1)),
                    (cx + ri * math.cos(a1), y + off, cz + ri * math.sin(a1)), (cx + ri * math.cos(a0), y + off, cz + ri * math.sin(a0))), F['paint'], (0, 1, 0))
        ck.rod(g, (cx, y - 1.6, cz), (cx, y - .4, cz), r * .3, F['metaldark'], 8, caps=False)
    s = r * .35
    yel = glow(0xFFD25A, 2.0)
    for (xa, xb, za, zb) in ((cx - s, cx - s + .6, cz - s, cz + s), (cx + s - .6, cx + s, cz - s, cz + s), (cx - s, cx + s, cz - .3, cz + .3)):
        g.face(((xa, y + 2 * off, za), (xb, y + 2 * off, za), (xb, y + 2 * off, zb), (xa, y + 2 * off, zb)), yel, (0, 1, 0))
    for k in range(0, 12, 2):
        a = .13 + 2 * math.pi * k / 12
        px, pz = cx - r * math.cos(a), cz - r * math.sin(a)
        box(g, px - .1, px + .1, y, y + .22, pz - .1, pz + .1, glow(0xFFD890, 1.6), None, 0, cell=0, skip=('-y',))


def tank(g, v, C):
    """A water tank on its stand: braced legs and a ring beam, the staves under their hoops, a conical lid, a ladder."""
    x, y, z, rad, leg, h, staves = v
    dark = F['metaldark']
    sides = (12 if C.near else 10) if C.fine else 6
    for k in range(4):
        a = k * math.pi / 2 + .785
        p = (x + rad * .8 * math.cos(a), y, z + rad * .8 * math.sin(a))
        rod(g, p, (p[0], y + leg, p[2]), .1, dark, 4, caps=False)
        if C.fine:
            q = (x + rad * .8 * math.cos(a + math.pi / 2), y + leg, z + rad * .8 * math.sin(a + math.pi / 2))
            rod(g, p, q, .045, dark, 3, caps=False)
    body = F['stave'] if staves > 0 else F['galv']
    yb = y + leg
    prof = [(rad * .98, 0), (rad, h * .08), (rad, h * .92), (rad * .98, h)]
    lathe(g, [(rad * .2, -.18), (rad * 1.02, -.18), (rad * 1.02, 0.0)], (x, yb, z), dark, sides)
    lathe(g, prof, (x, yb, z), body, sides, smooth=not staves, phase=.2)
    if C.fine:
        for b in (1, 2):
            by = yb + h * b / 3
            lathe(g, [(rad + .035, by - .06), (rad + .035, by + .06)], (x, 0, z), dark, sides, phase=.2)
    lathe(g, [(rad * 1.06, yb + h - .02), (rad * 1.06, yb + h + .06), (rad * .12, yb + h + rad * .55), (1e-4, yb + h + rad * .58)], (x, 0, z), dark, sides, phase=.2)
    if C.full:
        # a ladder up the side, a feed pipe down a leg
        for s in (-.2, .2):
            rod(g, (x + rad + .1, y, z + s), (x + rad + .1, yb + h, z + s), .025, F['galv'], 3, caps=False)
        n = int((leg + h) / .4)
        for k in range(n):
            yy = y + .2 + k * .4
            rod(g, (x + rad + .1, yy, z - .2), (x + rad + .1, yy, z + .2), .02, F['galv'], 3, caps=False)
        ck.pipe(g, [(x, yb - .18, z), (x, y + .25, z), (x - rad - .6, y + .25, z)], .09, F['galvdark'], 5, bend=.2)


def plant(g, p, C, sid):
    """Roof plant: an air handler on a skid with louvred sides, fans in their cowls on top, a duct run with flanges."""
    cx, cz, bw, bd, bh, y, pale, duct = p
    body = F['galv'] if pale > 0 else F['galvdark']
    x0, x1, z0, z1 = cx - bw / 2, cx + bw / 2, cz - bd / 2, cz + bd / 2
    if not C.fine:
        box(g, x0, x1, y, y + bh, z0, z1, body, None, 0, cell=0, skip=('-y',))
        box(g, x0 + .2, x1 - .2, y + bh, y + bh + .12, z0 + .2, z1 - .2, F['metaldark'], None, 0, cell=0, skip=('-y',))
        return
    box(g, x0 + .1, x1 - .1, y, y + .16, z0 + .15, z1 - .15, F['metaldark'], None, 0, cell=0, skip=('-y',))
    box(g, x0, x1, y + .16, y + bh, z0, z1, body, None, .05 if C.near else 0, cell=0, skip=('-y',))
    # louvre panels on the long sides (dark recesses with slats near), a door panel on an end
    for (zz, nz) in ((z0, -1), (z1, 1)):
        fr = Fr((cx, y + .16, zz), (0, 0, nz), bw / 2, bh)
        u0, u1, v0, v1 = -bw * .38, bw * .38, bh * .22, bh * .78
        fr.quad(g, u0, u1, v0, v1, C.o, F['void'])
        if C.close:
            ck.slats(g, fr, u0, u1, v0, v1, .035, .09, .22, F['galvdark'], sk.Tier(1))
    # fans on top: one or two cowls
    nf = 2 if bw > 3.6 else 1
    fr_ = min(bd, bw / nf) * .34
    for k in range(nf):
        fx = cx + (k - (nf - 1) / 2) * bw / nf
        lathe(g, [(fr_ * 1.06, 0), (fr_ * 1.06, .22), (fr_ * .94, .26)], (fx, y + bh, cz), F['metaldark'], 10 if C.near else 6)
        ck.disc(g, (fx, y + bh + .2, cz), fr_ * .96, F['void'], 10 if C.near else 6, (0, 1, 0))
        if C.close:
            for a in (0, math.pi / 2):
                rod(g, (fx - fr_ * math.cos(a), y + bh + .25, cz - fr_ * math.sin(a)), (fx + fr_ * math.cos(a), y + bh + .25, cz + fr_ * math.sin(a)), .03, F['galv'], 3, caps=False)
            ck.disc(g, (fx, y + bh + .27, cz), fr_ * .25, F['galv'], 6, (0, 1, 0))
    if duct > 0:
        # Core's duct runs 2.5 m toward source +x: Unity -x
        xa, xb = x0 - 2.5, x0
        box(g, xa, xb, y + .3, y + .8, cz - .25, cz + .25, F['galv'], None, 0, cell=0)
        for k in range(3):
            xf = xa + .2 + k * .9
            box(g, xf, xf + .08, y + .26, y + .84, cz - .29, cz + .29, F['galvdark'], None, 0, cell=0)
        box(g, xa - .1, xa + .5, y, y + .3, cz - .25, cz + .25, F['galv'], None, 0, cell=0, skip=('-y',))


def stairhut(g, x0, x1, z0, z1, y, C, sid, h=3.0):
    """A stair house: its walls under an overhanging roof slab, a door in a lit frame, a louvre."""
    box(g, x0, x1, y, y + h, z0, z1, F['concretedark'], None, 0, cell=0, skip=('-y',))
    box(g, x0 - .22, x1 + .22, y + h, y + h + .18, z0 - .22, z1 + .22, F['coping'], None, 0, cell=0)
    if not C.fine:
        return
    fr = Fr(((x0 + x1) / 2, y, z0), (0, 0, -1), (x1 - x0) / 2, h)
    fr.quad(g, -.55, .55, 0, 2.1, C.o, F['metaldark'])
    fr.box(g, -.62, .62, 2.12, 2.2, 0, .08, glow(rk.WARM, 1.2), None, bevel=0, cell=0)
    if C.close:
        fr.quad(g, .8, 1.5, 2.2, 2.7, .03, F['void'])
        ck.slats(g, fr, .8, 1.5, 2.2, 2.7, .035, .08, .12, F['galvdark'], sk.Tier(1))


def billboard(g, v, C, sid):
    """A billboard on the roof's edge: lattice posts, the board, its lit field under a word in neon tube inside a tube border,
    a catwalk, flood lamps on arms."""
    p0x, p0z, p1x, p1z, nx, nz, y, lift, bh, r_, g_, b_ = v
    col, gain = rk.hexlin(r_, g_, b_)
    L = math.hypot(p1x - p0x, p1z - p0z)
    c = ((p0x + p1x) / 2, (p0z + p1z) / 2)
    fr = Fr((c[0], y, c[1]), (nx, 0.0, nz), L / 2, lift + bh)
    dark = F['metaldark']
    for u in (-L / 2, 0, L / 2):
        if C.full:
            for du in (-.22, .22):
                for dd in (-.3, -.75):
                    rod(g, fr.at(u + du, 0, dd), fr.at(u + du, lift + bh, dd), .05, dark, 3, caps=False)
            n = int((lift + bh) / 1.1)
            for k in range(n):
                a, b = k * (lift + bh) / n, (k + 1) * (lift + bh) / n
                s = 1 if k % 2 == 0 else -1
                rod(g, fr.at(u - .22 * s, a, -.3), fr.at(u + .22 * s, b, -.3), .03, dark, 3, caps=False)
                rod(g, fr.at(u - .22, a, -.3 if k % 2 else -.75), fr.at(u - .22, b, -.75 if k % 2 else -.3), .03, dark, 3, caps=False)
        else:
            lbox(g, fr, u - .18, u + .18, 0, lift + bh, -.6, -.2, dark)
    # the board (its back toward the roof) and the lit face
    lbox(g, fr, -L / 2 - .15, L / 2 + .15, lift, lift + bh, -.2, .1, F['billback'])
    a, b, c0, c1 = -L / 2 + .3, L / 2 - .3, lift + .3, lift + bh - .3
    fr.quad(g, a, b, c0, c1, .26, glow(ck.mix(col, 0x06060C, .72), max(.5, gain * .32)))
    word = pick(WORDS, sid, 'bb')
    cell = min((b - a) * .82 / (len(word) * 6 - 1), (c1 - c0) * .5 / 7)
    Tt = sk.Tier(0 if C.full else 2)
    ck.neon_text(g, fr, word, 0, (c0 + c1) / 2 - 3.5 * cell + (c1 - c0) * .06, cell, ck.mix(col, 0xFFFFFF, .25), gain * 1.05, Tt, d=.5, weight=.42)
    # an underline bar and the border tube
    alt = pick((rk.CYAN, rk.MAGENTA, rk.AMBER, rk.ICE), sid, 'bb2')
    lbox(g, fr, -(b - a) * .36, (b - a) * .36, c0 + (c1 - c0) * .14, c0 + (c1 - c0) * .14 + cell * .9, .3, .46, glow(alt, 2.0))
    if C.fine:
        m = .5
        border = [fr.at(a + m, c0 + m, .4), fr.at(b - m, c0 + m, .4), fr.at(b - m, c1 - m, .4), fr.at(a + m, c1 - m, .4)]
        tube_path(g, border, .1, glow(col, gain * .8), C.ts, closed=True)
        # the catwalk and its rail
        lbox(g, fr, -L / 2, L / 2, lift - .25, lift - .15, .1, 1.0, F['grate'])
        rod(g, fr.at(-L / 2, lift + .8, .95), fr.at(L / 2, lift + .8, .95), .035, dark, 3, caps=False)
        for k in range(5):
            u = -L / 2 + L * k / 4
            rod(g, fr.at(u, lift - .15, .95), fr.at(u, lift + .8, .95), .03, dark, 3, caps=False)
        # flood lamps on arms over the top
        for k in range(3):
            u = -L / 2 + L * (k + .5) / 3
            rod(g, fr.at(u, lift + bh, -.1), fr.at(u, lift + bh + .5, 1.1), .04, dark, 3, caps=False)
            lbox(g, fr, u - .3, u + .3, lift + bh + .38, lift + bh + .6, .95, 1.3, glow(0xFFE2B8, 1.3))


def cornice(g, fp, y, C):
    """A masonry top: a stepped cornice on brackets round the parapet."""
    pale, dark = F['masonrypale'], F['ledgedark']
    rk.band(g, fp, y - 1.25, y - .65, pale, bottom=dark, d0=.45)
    rk.band(g, rk.grow(fp, .45), y - .65, y - .25, pale, top=pale, bottom=dark, d0=.4)
    if not C.full:
        return
    n = len(fp)
    for k in range(n):
        a, b = fp[k], fp[(k + 1) % n]
        fr = rk.edge_frame(a, b, y)
        m = max(2, int(fr.half * 2 / 2.4))
        for j in range(m):
            u = -fr.half + (j + .5) * 2 * fr.half / m
            fr.box(g, u - .18, u + .18, -1.9, -1.25, 0, .4, pale, None, bevel=0, cell=0)


def balcony(g, v, C, sid):
    """A balcony: its slab on brackets, a solid upstand under a rail on posts, now and then a planter or a drying line."""
    c0x, c0z, c1x, c1z, nx, nz, y = v
    L = math.hypot(c1x - c0x, c1z - c0z)
    fr = Fr(((c0x + c1x) / 2, y, (c0z + c1z) / 2), (nx, 0.0, nz), L / 2, 0)
    fr.box(g, -L / 2, L / 2, -.18, 0, 0, 1.2, F['concretedark'], None, bevel=0, cell=0)
    if not C.fine:
        fr.quad(g, -L / 2, L / 2, 0, 1.05, 1.2, F['metaldark'])
        return
    k = int(h01(sid, int(y * 10), int(c0x * 7 + c0z * 3)) * 4)
    if k == 0:
        # a glass balustrade
        fr.box(g, -L / 2, L / 2, 0, .12, 1.1, 1.2, F['metaldark'], None, bevel=0, cell=0, skip=('-y',))
        fr.quad(g, -L / 2, L / 2, .12, 1.0, 1.17, F['glassblue'])
        fr.box(g, -L / 2, L / 2, 1.0, 1.06, 1.1, 1.2, F['steel'], None, bevel=0, cell=0)
    else:
        fr.box(g, -L / 2, L / 2, 0, .55, 1.12, 1.2, F['resi'] if k < 3 else F['metaldark'], None, bevel=0, cell=0, skip=('-y',))
        rod(g, fr.at(-L / 2, 1.05, 1.16), fr.at(L / 2, 1.05, 1.16), .035, F['steel'], 3, caps=False)
        if C.close:
            n = max(2, int(L / 1.3))
            for j in range(n + 1):
                u = -L / 2 + L * j / n
                rod(g, fr.at(u, .55, 1.16), fr.at(u, 1.05, 1.16), .025, F['steel'], 3, caps=False)
    # the side returns
    for u in ((-L / 2, L / 2 - .08) if C.close else ()):
        fr.box(g, u, u + .08, 0, .9, 0, 1.12, F['metaldark'], None, bevel=0, cell=0, skip=('-y',))
    if C.close and k == 2:
        fr.box(g, -L / 2 + .3, -L / 2 + 1.5, 0, .35, .7, 1.08, F['timberdarkx'], None, bevel=0, cell=0, skip=('-y',))
        fr.box(g, -L / 2 + .35, -L / 2 + 1.45, .35, .62, .74, 1.04, F['leaf'], None, bevel=0, cell=0, skip=('-y',))


def aircon(g, v, C):
    """A condenser under a window: its casing on a bracket, the fan grille, a pipe back to the wall."""
    px, pz, tx, tz, nx, nz, y = v
    # Core's box runs from p along t for .9 m; the frame's u is the reader's right (-n.z, 0, n.x)
    ud = (-nz, 0.0, nx)
    s = 1 if (tx * ud[0] + tz * ud[2]) > 0 else -1
    fr = Fr((px + tx * .45, y, pz + tz * .45), (nx, 0.0, nz), .45, .6)
    back = ('-x' if nx > .5 else '+x' if nx < -.5 else '-z' if nz > .5 else '+z',)
    fr.box(g, -.45, .45, 0, .6, .04, .55, F['creme'] if h01(int(px * 9), int(y * 5)) < .5 else F['galv'], None, bevel=0, cell=0, skip=back)
    if not C.fine:
        return
    fr.poly(g, [(-.12 + .2 * math.cos(2 * math.pi * i / 6), .3 + .2 * math.sin(2 * math.pi * i / 6)) for i in range(6)], .56 + C.o, F['void'])
    if C.close:
        fr.box(g, -.5, .5, -.06, 0, 0, .58, F['metaldark'], None, bevel=0, cell=0, skip=back)
        rod(g, fr.at(.4 * s, -.06, .5), fr.at(.4 * s, -.5, 0), .02, F['metaldark'], 3, caps=False)


def fireescape(g, v, C):
    """A fire escape: a grated landing at every storey with its rails, the stair flights zigzagging between them."""
    c0x, c0z, c1x, c1z, nx, nz, y0, y1, storey = v
    L = math.hypot(c1x - c0x, c1z - c0z)
    fr = Fr(((c0x + c1x) / 2, 0, (c0z + c1z) / 2), (nx, 0.0, nz), L / 2, 0)
    rust = F['rust']
    flip = False
    y = y0
    while y < y1:
        fr.box(g, -L / 2, L / 2, y - .08, y, 0, 1.3, rust, None, bevel=0, cell=0)
        if C.fine:
            rod(g, fr.at(-L / 2, y + 1, 1.28), fr.at(L / 2, y + 1, 1.28), .035, rust, 3, caps=False)
            for u in ((-L / 2, -L / 6, L / 6, L / 2) if C.close else (-L / 2, L / 2)):
                rod(g, fr.at(u, y, 1.28), fr.at(u, y + 1, 1.28), .025, rust, 3, caps=False)
            for u in (-L / 2, L / 2):
                rod(g, fr.at(u, y + 1, 0), fr.at(u, y + 1, 1.28), .03, rust, 3, caps=False)
        if y + storey < y1 and C.fine:
            ua, ub = (L / 2 - .4, -L / 2 + .9) if flip else (-L / 2 + .4, L / 2 - .9)
            for d in (.3, .95):
                rod(g, fr.at(ua, y, d), fr.at(ub, y + storey, d), .045, rust, 3, caps=False)
            if C.close:
                for k in range(1, 6):
                    t = k / 6
                    u = ua + (ub - ua) * t
                    fr.box(g, min(u, u + .26), max(u, u + .26), y + storey * t - .03, y + storey * t, .3, .95, rust, None, bevel=0, cell=0)
        flip = not flip
        y += storey


def hangsign(g, v, C, sid):
    """A vertical sign hanging off a face: its casing on brackets, both faces a lit field under stacked neon letters."""
    ax, az, tx, tz, nx, nz, y, h, sw, dep, r_, g_, b_ = v
    col, gain = rk.hexlin(r_, g_, b_)
    # the sign's plane stands out along n; its two faces look along +t and -t
    mid = (ax + nx * (.6 + dep / 2), y, az + nz * (.6 + dep / 2))
    word = pick(SHORT, sid, int(y))
    for s in (1, -1):
        fr = Fr((mid[0] + tx * s * sw / 2 * 0, y, mid[2] + tz * s * 0), (tx * s, 0.0, tz * s), dep / 2, h)
        lbox(g, fr, -dep / 2, dep / 2, 0, h, 0, sw / 2, F['metaldark'])
        fr.quad(g, -dep / 2 + .15, dep / 2 - .15, .25, h - .25, sw / 2 + C.o, glow(ck.mix(col, 0x06060C, .7), max(.5, gain * .3)))
        n = len(word)
        cell = min((dep - .5) / 5, (h - 1.0) / (n * 8))
        Tt = sk.Tier(0 if C.full else 2)
        if not C.fine:
            # far off, the letters as lit blocks
            for i in range(n):
                fr.quad(g, -2.2 * cell, 2.2 * cell, h - .6 - (i + 1) * 8 * cell + cell * 1.2, h - .6 - (i + 1) * 8 * cell + cell * 7, sw / 2 + .2, glow(ck.mix(col, 0xFFFFFF, .3), gain))
            continue
        for i, ch in enumerate(word):
            ck.neon_text(g, fr, ch, 0, h - .6 - (i + 1) * 8 * cell + cell, cell, ck.mix(col, 0xFFFFFF, .3), gain, Tt, d=sw / 2 + .18, weight=.45)
    fr0 = rk.frame_n((ax, y, az), (nx, 0, nz))
    for yy in (y + h - .4, y + .4):
        rod(g, (ax, yy, az), (ax + nx * .6, yy, az + nz * .6), .08, F['metaldark'], 4, caps=False)
        if C.fine:
            rod(g, (ax, yy + .9, az), (ax + nx * (.6 + dep * .6), yy, az + nz * (.6 + dep * .6)), .03, F['metaldark'], 3, caps=False)


def trims(lot, g, fps, C):
    """The neon trims round roofs and setbacks (Core's bars) as tubes on the modelled footprints."""
    for v in lot.all('trim'):
        y, r_, g_, b_ = v[:4]
        col, gain = rk.hexlin(r_, g_, b_)
        fp = None
        for k, tier in enumerate(lot.tiers):
            if abs(tier[6] - y) < .01:
                fp = fps[k]
        if fp is None:
            fp = v[4:]
        out = rk.grow(fp, .2)
        yy = y - .22 if not lot.bare else y - .3
        tube_path(g, [(p[0], yy, p[1]) for p in out], .17, glow(col, gain * .9), C.ts, closed=True)


# ---------------------------------------------------------------------------------------------------------------------------
# The actors' roofs, the bridge, the huts
# ---------------------------------------------------------------------------------------------------------------------------
def bare_roof(L, lot, g, fp, y, C):
    """An actors' roof: flat and clear where they move, a low coping round it, laps in the membrane, hatches and drains at the
    edges, and on the duel's roof a painted ring."""
    x0, x1, z0, z1 = rk.bounds(fp)
    cop = F['coping']
    o = rk.grow(fp, .08)
    i2 = rk.grow(fp, -.3)
    n = len(fp)
    rk.wall_ring(g, o, y - .25, y + .16, cop)
    rk.wall_ring(g, i2, y, y + .16, cop, inward=True)
    for k in range(n):
        j = (k + 1) % n
        g.face(((o[k][0], y + .16, o[k][1]), (o[j][0], y + .16, o[j][1]), (i2[j][0], y + .16, i2[j][1]), (i2[k][0], y + .16, i2[k][1])), cop, (0, 1, 0))
    if not C.fine:
        return
    # the membrane's laps: darker strips every 4 m
    w = x1 - x0
    k = 0
    xx = x0 + 3
    while xx < x1 - 2:
        g.face(((xx, y + .04, z0 + .4), (xx + .5, y + .04, z0 + .4), (xx + .5, y + .04, z1 - .4), (xx, y + .04, z1 - .4)), F['membranelap'], (0, 1, 0))
        xx += 4.0
        k += 1
    # the duel's roof: a painted ring between the fighters
    b, gn = L.sites['blade'], L.sites['gunner']
    if x0 < b[0] < x1 and z0 < b[2] < z1 and abs(b[1] - y) < .5:
        cx, cz = (b[0] + gn[0]) / 2, (b[2] + gn[2]) / 2
        ro, ri = 9.2, 8.8
        for s in range(32):
            a0, a1 = 2 * math.pi * s / 32, 2 * math.pi * (s + 1) / 32
            g.face(((cx + ro * math.cos(a0), y + .09, cz + ro * math.sin(a0)), (cx + ro * math.cos(a1), y + .09, cz + ro * math.sin(a1)),
                    (cx + ri * math.cos(a1), y + .09, cz + ri * math.sin(a1)), (cx + ri * math.cos(a0), y + .09, cz + ri * math.sin(a0))), glow(rk.CYAN, .55), (0, 1, 0))
    # low plant along the edges, kept 5 m clear of where the actors stand, run and leap: skylights, condensers, vent
    # cowls, hatches, a cable tray
    near = [t for (_, t) in L.targets if abs(t[1] - y) < 5] + [p for p in L.sites.values() if abs(p[1] - y) < 5]

    def free(px, pz, r=5.0):
        return all((px - t[0]) ** 2 + (pz - t[2]) ** 2 > r * r for t in near)
    spots = []
    step = 6.5
    n = max(1, int((x1 - x0 - 6) / step))
    for k in range(n + 1):
        px = x0 + 3 + k * (x1 - x0 - 6) / n
        spots += [(px, z0 + 2.4, 0), (px, z1 - 2.4, 0)]
    m = max(1, int((z1 - z0 - 12) / step))
    for k in range(m + 1):
        pz = z0 + 6 + k * (z1 - z0 - 12) / max(1, m)
        spots += [(x0 + 2.4, pz, 1), (x1 - 2.4, pz, 1)]
    for i, (px, pz, turn) in enumerate(spots):
        if not free(px, pz):
            continue
        kind = int(h01(lot.i, i, 'roof') * 6)
        a, b = (1.6, .7) if turn == 0 else (.7, 1.6)
        if kind == 0:
            # a skylight: a kerb under a lit glass lid
            box(g, px - a, px + a, y, y + .3, pz - b, pz + b, F['coping'], None, 0, cell=0, skip=('-y', '+y'))
            g.face(((px - a, y + .3, pz - b), (px + a, y + .3, pz - b), (px + a * .7, y + .46, pz), (px - a * .7, y + .46, pz)), F['roofglass'])
            g.face(((px - a, y + .3, pz + b), (px + a, y + .3, pz + b), (px + a * .7, y + .46, pz), (px - a * .7, y + .46, pz)), F['roofglass'])
            g.face(((px - a + .15, y + .34, pz - b * .5), (px + a - .15, y + .34, pz - b * .5), (px + a - .15, y + .34, pz + b * .5), (px - a + .15, y + .34, pz + b * .5)), glow(0xBFD8FF, .5), (0, 1, 0))
        elif kind == 1:
            # two condensers on a frame
            for s_ in (-.8, .8):
                cx, cz = (px + s_, pz) if turn == 0 else (px, pz + s_)
                box(g, cx - .55, cx + .55, y + .16, y + .46, cz - .4, cz + .4, F['creme'], None, 0, cell=0)
                ck.disc(g, (cx, y + .47, cz), .3, F['void'], 8, (0, 1, 0))
            box(g, px - a, px + a, y + .08, y + .16, pz - b * .8, pz + b * .8, F['galvdark'], None, 0, cell=0)
        elif kind == 2:
            for s_ in (-.7, .7):
                cx, cz = (px + s_, pz) if turn == 0 else (px, pz + s_)
                lathe(g, [(.2, y), (.16, y + .08), (.14, y + .26)], (cx, 0, cz), F['galv'], 8)
                lathe(g, [(.3, y + .26), (.32, y + .32), (.14, y + .44), (.01, y + .46)], (cx, 0, cz), F['galvdark'], 8)
        elif kind == 3:
            box(g, px - .6, px + .6, y, y + .32, pz - .6, pz + .6, F['galvdark'], None, 0, cell=0, skip=('-y',))
            rod(g, (px - .35, y + .34, pz - .5), (px + .35, y + .34, pz - .5), .025, F['hazard'], 3, caps=False)
        elif kind == 4:
            # a run of cable tray with its cables
            box(g, px - a * 1.6, px + a * 1.6, y + .12, y + .2, pz - b * .25, pz + b * .25, F['galvdark'], None, 0, cell=0) if turn == 0 else                 box(g, px - a * .25, px + a * .25, y + .12, y + .2, pz - b * 1.6, pz + b * 1.6, F['galvdark'], None, 0, cell=0)
        else:
            # a puddle of light: a low bollard lamp
            lathe(g, [(.1, y), (.08, y + .28)], (px, 0, pz), F['metaldark'], 6)
            lathe(g, [(.09, y + .28), (.09, y + .4)], (px, 0, pz), glow(pick((rk.CYAN, rk.MAGENTA, rk.WARM), lot.i, i), 1.6), 6)
            lathe(g, [(.11, y + .4), (.02, y + .45)], (px, 0, pz), F['metaldark'], 6)


def bridge(g, b, C):
    """The runners' skybridge: a deck on a trussed girder, glass balustrades on posts, lit handrails."""
    x0, x1, z0, z1, y = b
    box(g, x0, x1, y - .35, y, z0, z1, F['gunmetal'], None, 0, cell=0)
    box(g, x0, x1, y - 1.2, y - .35, z0 + .5, z1 - .5, F['metaldark'], None, 0, cell=0)
    cy = glow(rk.CYAN, 2.4)
    for zz in (z0 + .08, z1 - .08):
        fr = Fr(((x0 + x1) / 2, y, zz), (0, 0, 1 if zz > (z0 + z1) / 2 else -1), (x1 - x0) / 2, 1)
        fr.quad(g, -fr.half, fr.half, .1, .92, .0, F['glassblue'])
        fr.quad(g, -fr.half, fr.half, .1, .92, -.03, F['glassblue'])
        rod(g, (x0, y + 1.0, zz), (x1, y + 1.0, zz), .07, cy, C.ts, caps=False)
        n = 6 if C.fine else 2
        for k in range(n + 1):
            xx = x0 + (x1 - x0) * k / n
            box(g, xx - .05, xx + .05, y, y + .95, zz - .05, zz + .05, F['steel'], None, 0, cell=0, skip=('-y',))
    if C.fine:
        for k in range(6):
            xa, xb = x0 + (x1 - x0) * k / 6, x0 + (x1 - x0) * (k + 1) / 6
            for zz in (z0 + .5, z1 - .5):
                rod(g, (xa, y - 1.2, zz), (xb, y - .35, zz), .05, F['gunmetal'], 3, caps=False)


def runhut(g, h, C):
    """A runner's hut: walls under an overhanging slab, a dark doorway at both ends, a magenta tube over its front."""
    x0, x1, z0, z1, y0, y1 = h
    box(g, x0, x1, y0, y1, z0, z1, F['concretedark'], None, 0, cell=0, skip=('-y',))
    box(g, x0 - .25, x1 + .25, y1, y1 + .18, z0 - .25, z1 + .25, F['coping'], None, 0, cell=0)
    mz = (z0 + z1) / 2
    for (xx, nx) in ((x0, -1), (x1, 1)):
        fr = Fr((xx, y0, mz), (nx, 0, 0), (z1 - z0) / 2, y1 - y0)
        fr.quad(g, -.7, .7, 0, 2.3, .04, F['void'])
        if C.fine:
            fr.box(g, -.78, .78, 2.32, 2.42, 0, .1, glow(rk.WARM, 1.1), None, bevel=0, cell=0)
    rod(g, (x0, y1 - .14, z1 + .12), (x1, y1 - .14, z1 + .12), .08, glow(rk.MAGENTA, 2.4), C.ts, caps=False)


# ---------------------------------------------------------------------------------------------------------------------------
# The holo screens' frames
# ---------------------------------------------------------------------------------------------------------------------------
def frame(g, fm, C):
    """A landmark screen's mount: a moulded bezel with a tube inside its lip, the ribbed casing behind the panel, lattice
    brackets back to its tower, a service catwalk under it, flood lamps on arms along its top (Core's lamps' glows)."""
    c, n, w, h, back = fm['c'], fm['n'], fm['w'], fm['h'], fm['back']
    size = max(w, h)
    b = min(1.1, max(.28, size * .022))
    depth = max(.5, size * .02)
    hw, hh = w / 2, h / 2
    fr = Fr((c[0], c[1] - hh, c[2]), n, hw, h)      # v from the panel's bottom edge
    dark, mid = F['metaldark'], F['gunmetal']
    d0, d1 = -.35 * depth, .65 * depth
    # the bezel: outer rails, a chamfer lip running in toward the panel
    lbox(g, fr, -hw - b, hw + b, h, h + b, d0, d1, dark)
    lbox(g, fr, -hw - b, hw + b, -b, 0, d0, d1, dark)
    lbox(g, fr, -hw - b, -hw, 0, h, d0, d1, dark)
    lbox(g, fr, hw, hw + b, 0, h, d0, d1, dark)
    if C.fine:
        e = b * .35
        for (ua, ub, va, vb) in ((-hw - b - e, hw + b + e, h + b, h + b + e), (-hw - b - e, hw + b + e, -b - e, -b), (-hw - b - e, -hw - b, -b, h + b), (hw + b, hw + b + e, -b, h + b)):
            lbox(g, fr, ua, ub, va, vb, d0 + depth * .15, d1 - depth * .25, mid)
        # the corner nodes
        for su in (-1, 1):
            for sv in (0, 1):
                cu, cv = su * (hw + b * .5), (h + b * .5) if sv else -b * .5
                lbox(g, fr, cu - b * .85, cu + b * .85, cv - b * .85, cv + b * .85, d1, d1 + depth * .25, mid)
    # the casing and its ribs
    cd = depth * 1.6
    lbox(g, fr, -hw, hw, 0, h, -.06 - cd, -.06, F['spandrel'])
    if C.fine:
        ribs = max(3, int(w / 6))
        for k in range(ribs + 1):
            u = -hw + w * k / ribs
            lbox(g, fr, u - b * .25, u + b * .25, 0, h, -.06 - cd - b * .5, -.06 - cd, dark)
    # the brackets back to the tower's face (it stands `back` behind the screen's centre along -z)
    face_z = c[2] - back + .05
    for su in (-1, 1):
        for sv in (0, 1):
            p = fr.at(su * (hw - b), sv * h + (b if sv == 0 else -b), -.06 - cd)
            if p[2] - face_z > .3:
                q = (p[0], p[1], face_z)
                r = max(.25, b * .7) * .5
                rod(g, p, q, r, dark, 4, caps=False)
                if C.fine:
                    rod(g, (p[0], p[1] + (3 if sv == 0 else -3) * b, p[2]), q, r * .55, dark, 3, caps=False)
    # the catwalk under the panel
    if C.fine:
        lbox(g, fr, -hw, hw, -b - b * .25, -b, d1, d1 + b * 1.6, F['grate'])
        ur = fr.ud
        ra, rb = fr.at(-hw, -b + b * 1.3, d1 + b * 1.55), fr.at(hw, -b + b * 1.3, d1 + b * 1.55)
        rod(g, ra, rb, b * .08, dark, 3, caps=False)
        for k in range(int(w / 4) + 1):
            u = -hw + k * w / max(1, int(w / 4))
            rod(g, fr.at(u, -b, d1 + b * 1.55), fr.at(u, -b + b * 1.3, d1 + b * 1.55), b * .06, dark, 3, caps=False)
    # the flood lamps: Core stands their glows at c + t f hw .92 + y (hh + b) + n (depth .35 + b .3 + b .6)
    lamps = max(2, int(round(w / 9)))
    for k in range(lamps):
        f = (k + .5) / lamps * 2 - 1
        u = f * hw * .92
        dl = depth * .35 + b * .3
        lbox(g, fr, u - b * .45, u + b * .45, h + b, h + b + b * .7, dl - b * .4, dl + b * .4, glow(0xFFE6C8, 1.5))
        if C.fine:
            lbox(g, fr, u - b * .55, u + b * .55, h + b + b * .7, h + b + b * .95, dl - b * .6, dl + b * .55, dark)


STATS = {}


def _tally(fn):
    def run(g, *a, **k):
        n = g.faces
        r = fn(g, *a, **k)
        STATS[fn.__name__] = STATS.get(fn.__name__, 0) + g.faces - n
        return r
    return run


for _n in ('ledge', 'parapet', 'crownband', 'fins', 'mast', 'crown', 'helipad', 'tank', 'plant', 'stairhut', 'billboard', 'cornice', 'balcony', 'aircon', 'fireescape', 'hangsign',
           'bridge', 'runhut', 'frame'):
    globals()[_n] = _tally(globals()[_n])


def build(L, T):
    """The city's solid geometry and its facade faces for a tier."""
    g = sk.Geo()
    fc = rk.Facade()
    STATS.clear()
    for lot in L.city:
        n = g.faces
        build_lot(L, lot, T, g, fc)
        STATS['lots'] = STATS.get('lots', 0) + g.faces - n
    C = Ctx(T, True)
    for b in L.bridges:
        bridge(g, b, C)
    for h in L.runhuts:
        runhut(g, h, C)
    for fm in L.frames:
        frame(g, fm, C)
    return g, fc
