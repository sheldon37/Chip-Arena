"""The Hollow Sanctum's moving and breakable art (sanctum_art.py runs it): the oak pews (block props, in each pew block's own
frame), the rigs as props in their own frames, posed each frame by Core NativeSanctumArt from Core NativeSanctumRig's state, and
the rigs' fixed fittings (anchors, the bell's frame, the gates' posts and lanterns, the banners' poles) for the static scenery.

The rig frames (Unity space, metres; Core NativeSanctumArt holds the same):
  chandelier            origin at the hoop's foot (the rig's y: the crane's load height), +y up the hub (crown 1.6 m)
  chandelier-candles-K  chandelier K's sixteen candles in that frame (their heights are the rig's, so the flames sit on them)
  chandelier-wreck      a fallen chandelier upright in the same frame (the pose tilts it about a pivot .3 m up)
  wreck-candles-K       the candles still standing on wreck K
  floor-candles-K       wreck K's candles lying round its zone (origin at the zone's centre on the floor, unturned)
  chain                 one metre of chain from y 0 to 1 (drawn end to end, the last one scaled)
  censer                origin at the censer's centre, +y up its rope to the yoke (the rope included: its length is fixed)
  pull-rope             one metre of rope from y 0 to 1 (scaled between its ends)
  bell, clapper         origin at the headstock's pivot, the bell hanging down -y
  gate                  a portcullis's grille, origin at the foot of its middle (bars up +y to its height, across z)
  lanterns-<gate>       the gate's four lanterns' glass (world frame), tinted each frame
  fire-<hazard>-<zone>  a holy-fire curtain's channel and three baskets (world frame), their coals tinted by the heat
  banner-red, -blue     a banner hanging from its pole's pivot (origin), down -y, across z
"""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, lathe, sweep

F = {}
KIND = []
CANDLE = None     # sanctum_art.candle (the running script's, with its finishes)
Y = (0.0, 1.0, 0.0)


# ---------------------------------------------------------------------------------------------------------------------------
# The pews (block props: centred on the block, which faces east)
# ---------------------------------------------------------------------------------------------------------------------------
def pew(T, size):
    g = Geo()
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    x0, x1, y0, y1, z0, z1 = -hx, hx, -hy, hy, -hz, hz
    oz, ox, oy = F['oakz'], F['oakx'], F['oaky']
    od = F['oakdarkz']
    # the seat: a plank with a rounded front edge
    seat = [(-.0, -.035), (.45, -.035), (.47, -.02), (.475, .0), (.47, .02), (.45, .03), (-.0, .03)]
    box(g, x0 + .07, x0 + .52, y0 + .42, y0 + .48, z0 + .08, z1 - .08, oz, T, bevel=.018)
    sk.sweep(g, sk.circle_section(.03, T.small), [(x0 + .52, y0 + .45, z0 + .08), (x0 + .52, y0 + .45, z1 - .08)], oz)
    # the seat's apron and its legs under it
    box(g, x0 + .48, x0 + .52, y0 + .26, y0 + .42, z0 + .08, z1 - .08, oz, T, bevel=.008)
    # the back: a frame of rails and muntins round sunk panels
    bx0, bx1 = x0, x0 + .07
    box(g, bx0, bx1, y0 + .42, y1 - .06, z0 + .08, z1 - .08, od, T, bevel=.01)
    n = 4
    span = (z1 - z0 - .16)
    for i in range(n + 1):
        zc = z0 + .08 + span * i / n
        box(g, bx1, bx1 + .025, y0 + .5, y1 - .1, zc - .035, zc + .035, oy, T, bevel=.008)
    for yy in (y0 + .5, y1 - .1):
        box(g, bx1, bx1 + .028, yy - .03, yy + .03, z0 + .08, z1 - .08, oz, T, bevel=.008)
    if T.detail:
        for i in range(n):
            zc = z0 + .08 + span * (i + .5) / n
            # a pointed arch carved in each panel
            arc = sk.pointed_arch(span / n * .32, .16, max(4, T.arc // 3))
            yb = y1 - .22
            pts = [(zc - span / n * .32 + a, yb + b) for (a, b) in arc] + [(zc + span / n * .32 - a, yb + b) for (a, b) in reversed(arc[:-1])]
            pts = [(zc - span / n * .32, y0 + .58)] + pts + [(zc + span / n * .32, y0 + .58)]
            sweep(g, sk.circle_section(.012, 4), [(bx1 + .028, yy, zz) for (zz, yy) in pts], oz)
    # the top rail, rounded
    sweep(g, [(-.06, -.03), (.06, -.03), (.065, .0), (.05, .03), (0, .045), (-.05, .03), (-.065, .0)],
          [(x0 + .035, y1 - .03, z0 + .06), (x0 + .035, y1 - .03, z1 - .06)], oz, path_normals=[Y, Y])
    # the kneeler, padded in velvet
    box(g, x1 - .25, x1 - .02, y0 + .08, y0 + .17, z0 + .1, z1 - .1, od, T, bevel=.01)
    box(g, x1 - .24, x1 - .03, y0 + .17, y0 + .21, z0 + .12, z1 - .12, F['velvet'], T, bevel=.018)
    # the carved ends: a shaped board each side, a blind arch on its face, a poppyhead finial
    for side in (-1, 1):
        zf = z0 if side < 0 else z1
        za, zb = (zf, zf + .08) if side < 0 else (zf - .08, zf)
        outline = [(x0, y0), (x1, y0), (x1, y0 + .78), (x1 - .08, y0 + .88), (x0 + .4, y1 - .02), (x0 + .12, y1 + .02), (x0, y1 + .02)]
        for (zz, nz) in ((za, -1), (zb, 1)):
            g.face([(x, y, zz) for (x, y) in outline], oy, (0, 0, nz))
        for k in range(len(outline)):
            a, b = outline[k], outline[(k + 1) % len(outline)]
            d = (b[0] - a[0], b[1] - a[1])
            nrm = sk._norm((d[1], -d[0], 0))
            g.face(((a[0], a[1], za), (b[0], b[1], za), (b[0], b[1], zb), (a[0], a[1], zb)), ox, nrm)
        if T.detail:
            face = zf + side * .004
            arc = sk.pointed_arch(.3, .3, max(4, T.arc // 2))
            cx = (x0 + x1) / 2 + .02
            base, head = y0 + .12, y0 + .5
            pts = [(cx - .3, base), (cx - .3, head)] + [(cx - .3 + a, head + b) for (a, b) in arc[1:]] + [(cx + .3 - a, head + b) for (a, b) in reversed(arc[:-1])] + [(cx + .3, base)]
            sweep(g, sk.circle_section(.014, 4), [(x, y, face) for (x, y) in pts], oz, closed=True)
            mid = (cx, (base + head) / 2 + .1, face - side * .002)
            for k in range(len(pts)):
                a, b = pts[k], pts[(k + 1) % len(pts)]
                g.face((mid, (a[0], a[1], face - side * .002), (b[0], b[1], face - side * .002)), F['oakdarky'], (0, 0, side))
        # the poppyhead: a turned knop and three leaves
        top = (x0 + .06, y1 + .02, (za + zb) / 2)
        lathe(g, [(.04, 0), (.05, .03), (.035, .06), (.045, .1), (0, .14)], top, oy, max(6, T.small))
        if T.detail:
            for k in range(3):
                a = 2 * math.pi * k / 3
                c = (math.cos(a), 0, math.sin(a))
                p0 = (top[0] + c[0] * .03, top[1] + .04, top[2] + c[2] * .03)
                p1 = (top[0] + c[0] * .07, top[1] + .1, top[2] + c[2] * .07)
                p2 = (top[0] + c[0] * .055, top[1] + .14, top[2] + c[2] * .055)
                sk.tube(g, [p0, p1, p2], .014, oy, 4)
    return g


def pew_scene(L, T):
    """Every pew in its place (world space): the static bake's occluders and its previews."""
    g = Geo()
    for b in L['blocks']:
        if b['id'].startswith('sanctum-pew'):
            g.extend(pew(T, b['size']).transformed([1, 0, 0, b['pos'][0], 0, 1, 0, b['pos'][1], 0, 0, 1, b['pos'][2]]))
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The chandeliers
# ---------------------------------------------------------------------------------------------------------------------------
HOOP = 1.4
CROWN = 1.6


def scroll(g, p0, p1, bulge, f, T, r=.016):
    """A wrought-iron S scroll from p0 to p1, curling out by `bulge` (a vector)."""
    pts = []
    n = 10 if T.fine else 6
    for i in range(n + 1):
        t = i / n
        s = math.sin(2 * math.pi * t) * .5
        pts.append(sk._add(sk._lerp(p0, p1, t), bulge, s))
    sk.tube(g, pts, r, f, 5 if T.fine else 4)


def chandelier_body(T, wreck=False):
    g = Geo()
    I, G = F['iron'], F['gold']
    R = HOOP
    n = T.arc * 2
    crown = .6 if wreck else CROWN

    def hoop_pt(a, y, r):
        bend = (.12 * math.sin(a * 2.3 + .5) - .05) if wreck else 0.0
        return (math.cos(a) * r, y + bend * (1 + math.cos(a * 3)), math.sin(a) * r)
    ring = [hoop_pt(2 * math.pi * k / n, .12, R) for k in range(n)]
    sweep(g, sk.rect_section(.06, .1, .012), ring, I, closed=True)
    ring2 = [hoop_pt(2 * math.pi * k / n, .03, R - .16) for k in range(n)]
    sweep(g, sk.circle_section(.022, T.small), ring2, G, closed=True)
    # the scrollwork between the two hoops
    if T.detail:
        for k in range(16):
            a = 2 * math.pi * (k + .0) / 16
            p0 = hoop_pt(a, .06, R - .14)
            p1 = hoop_pt(a + .2, .12, R - .03)
            scroll(g, p0, p1, (0, .06, 0), I, T, .012)
    # the drip pans and their sockets
    for k in range(16):
        a = 2 * math.pi * (k + .5) / 16
        c = hoop_pt(a, .18, R)
        lathe(g, [(.02, -.04), (.04, -.03), (.09, .0), (.095, .015), (.08, .02)], c, F['brass'], T.small, cap0=True)
        lathe(g, [(.045, .015), (.045, .04), (.04, .045)], c, I, max(5, T.small - 2), cap1=True)
    # the arms: from the hoop up to the hub, curling at the knee
    for k in range(4):
        a = math.pi / 2 * k + math.pi / 4
        e = hoop_pt(a, .12, R - .05)
        if not wreck:
            m = (math.cos(a) * R * .55, crown * .55, math.sin(a) * R * .55)
            h = (math.cos(a) * .12, crown - .05, math.sin(a) * .12)
        else:
            m = (math.cos(a) * R * .55, .3, math.sin(a) * R * .55)
            h = (math.cos(a + .4) * .3, .55, math.sin(a + .4) * .3)
        path = sk.catmull([e, sk._lerp(e, m, .5), m, sk._lerp(m, h, .55), h], 3)
        sk.tube(g, path, .035, I, T.small)
        if T.detail:
            c = sk._lerp(e, m, .6)
            curl = [sk._add(c, (math.cos(a) * .1 * math.cos(t), .1 * math.sin(t), math.sin(a) * .1 * math.cos(t))) for t in np.linspace(0, 4.5, 10)]
            sk.tube(g, curl, .015, I, 4)
    # the hub: a turned baluster, the crown ring and its spikes, the finial
    if not wreck:
        hub = (0, crown, 0)
        lathe(g, [(0, -.45), (.06, -.42), (.12, -.3), (.1, -.2), (.16, -.1), (.18, 0), (.12, .1), (.1, .2), (.13, .26), (.06, .34), (.04, .42)], hub, I, T.round, cap0=False, cap1=True)
        cring = [(math.cos(2 * math.pi * k / n) * .55, crown - .05, math.sin(2 * math.pi * k / n) * .55) for k in range(n)]
        sweep(g, sk.rect_section(.03, .07, .008), cring, G, closed=True)
        for k in range(8 if T.detail else 0):
            a = 2 * math.pi * k / 8
            b = (math.cos(a) * .55, crown - .02, math.sin(a) * .55)
            lathe(g, [(.025, 0), (.02, .08), (0, .16)], b, G, 4)
        lathe(g, [(0, .42), (.05, .46), (.06, .52), (.03, .58), (0, .64)], hub, G, T.small, cap0=False)
        # the ring the chain hangs by
        sk.tube(g, [(math.cos(t) * .05, crown + .7 + math.sin(t) * .06, 0) for t in np.linspace(0, 2 * math.pi, 9)], .012, I, 4, closed=True)
    else:
        # the hub broken off, lying across the hoop
        lathe(g, [(0, -.3), (.1, -.25), (.16, -.1), (.18, 0), (.12, .1), (.06, .2)], (.1, .45, .1), I, T.round, axis=sk._norm((.8, .3, .5)), cap0=False, cap1=True)
    return g


def chandelier_candles(T, k, wreck_only=False):
    g = Geo()
    for i, c in enumerate(CAND[k]):
        if wreck_only and not c['stands']:
            continue
        a = c['a']
        CANDLE(g, math.cos(a) * HOOP, .22, math.sin(a) * HOOP, .035, c['tall'], T, 1000 + k * 37 + i)
    return g


def floor_candles(T, k):
    g = Geo()
    for i, c in enumerate(CAND[k]):
        if c['stands']:
            continue
        sa_, sr = c['sa'], c['sr']
        x, z = math.cos(sa_) * sr, math.sin(sa_) * sr
        turn = sa_ * 1.7
        d = (math.sin(turn), 0, math.cos(turn))
        hl = c['tall'] / 2
        p0 = (x - d[0] * hl, .035, z - d[2] * hl)
        p1 = (x + d[0] * hl, .035, z + d[2] * hl)
        sk.cylinder(g, p0, p1, .035, F['wax'], (8, 6, 4)[T.i])
        # a puddle of spilt wax
        if T.detail:
            lathe(g, [(0, .004), (.06, .004), (.07, 0)], (x, 0, z), F['wax'], 7, cap0=False)
    return g


def chain(T):
    """One metre of chain: oval links of round wire, each turned a quarter from the last."""
    g = Geo()
    if T.i == 2:
        sk.cylinder(g, (0, 0, 0), (0, 1, 0), .022, F['chain'], 4, caps=False)
        return g
    links = 10
    for i in range(links):
        y = (i + .5) / links
        turn = (i % 2) * math.pi / 2
        n = 12 if T.fine else 8
        pts = []
        for k in range(n):
            t = 2 * math.pi * k / n
            lx, ly = .028 * math.cos(t), .06 * math.sin(t)
            pts.append((lx * math.cos(turn), y + ly, lx * math.sin(turn)))
        sweep(g, sk.circle_section(.009, 5 if T.fine else 4), pts, F['chain'], closed=True)
    return g


def rope(T, r=.022):
    g = Geo()
    sides = (8, 6, 4)[T.i]
    if T.detail:
        for s in range(3):
            pts = [(math.cos(s * 2.094 + t * 14) * r * .45, t, math.sin(s * 2.094 + t * 14) * r * .45) for t in np.linspace(0, 1, 14 if T.fine else 8)]
            sk.tube(g, pts, r * .55, F['rope'], sides, caps=False)
    else:
        sk.cylinder(g, (0, 0, 0), (0, 1, 0), r, F['rope'], sides, caps=False)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The censer, the bell, the gates, the fire baskets, the banners
# ---------------------------------------------------------------------------------------------------------------------------
def censer(T, rope_len):
    g = Geo()
    S, G, E = F['silver'], F['gold'], F['ember']
    sides = T.round
    # the bowl: foot, belly, gold band
    lathe(g, [(0, -.94), (.2, -.92), (.24, -.86), (.12, -.8), (.14, -.74), (.32, -.66), (.48, -.5), (.56, -.3), (.58, -.12), (.6, -.04), (.62, 0.0), (.56, .02)],
          (0, 0, 0), S, sides, cap0=True)
    lathe(g, [(.6, -.05), (.64, -.03), (.64, .03), (.6, .05)], (0, 0, 0), G, sides)
    # the coals' glow under the lid
    lathe(g, [(0, .06), (.42, .04), (.5, .02)], (0, 0, 0), E, sides, cap0=False)
    # the pierced lid in tiers, its windows dark (the coals glow through them)
    lid = [(.56, .04), (.54, .12), (.48, .22), (.42, .3), (.36, .4), (.3, .5), (.22, .6), (.14, .7), (.1, .78), (.06, .84)]
    lathe(g, lid, (0, 0, 0), S, sides)
    if T.detail:
        for tier, (r0, y0) in enumerate([(.5, .16), (.39, .36), (.26, .56)]):
            m = 8
            for k in range(m):
                a = 2 * math.pi * (k + .5 * (tier % 2)) / m
                c = (math.cos(a) * (r0 + .005), y0, math.sin(a) * (r0 + .005))
                nrm = (math.cos(a), .3, math.sin(a))
                u = (-math.sin(a) * .045, 0, math.cos(a) * .045)
                v = (0, .055, 0)
                pts = [sk._add(sk._add(c, u, -1), v, -1), sk._add(sk._add(c, u, 1), v, -1), sk._add(sk._add(c, u, 1), v, 1), sk._add(sk._add(c, u, -1), v, 1)]
                g.face(pts, E, nrm)
    lathe(g, [(.06, .84), (.08, .9), (.05, .96), (.07, 1.0), (0, 1.08)], (0, 0, 0), G, T.small)
    # six chains to the crown ring, the ring, the rope to the yoke
    for k in range(6):
        a = math.pi / 3 * k
        p0 = (math.cos(a) * .6, .12, math.sin(a) * .6)
        p1 = (math.cos(a) * .18, 2.4, math.sin(a) * .18)
        sk.tube(g, [p0, p1], .012, F['chain'], 4, caps=False)
    sweep(g, sk.circle_section(.03, T.small), [(math.cos(t) * .2, 2.4, math.sin(t) * .2) for t in np.linspace(0, 2 * math.pi, 13)[:-1]], G, closed=True)
    lathe(g, [(.05, 2.38), (.07, 2.48), (.04, 2.56)], (0, 0, 0), G, T.small, cap1=True)
    n = 24 if T.fine else 10
    rp = [(0, 2.56 + (rope_len - .1 - 2.56) * t, 0) for t in np.linspace(0, 1, n)]
    sk.tube(g, rp, .035, F['rope'], (8, 6, 4)[T.i])
    return g


def bell(T, mouth, height):
    g = Geo()
    B = F['bronze']
    # the headstock
    box(g, -1.0, 1.0, -.01, .31, -.2, .2, F['oakdarkx'], T, bevel=.03)
    # the bell: crown, shoulder, waist, sound bow and lip; the inside under it
    prof = [(0, -.06), (.3, -.08), (.5, -.14), (.58, -.3), (.6, -.6), (.66, -1.1), (.8, -1.6), (1.0, -2.0), (1.2, -2.3), (mouth, -height - .1), (mouth + .06, -height - .14)]
    lathe(g, prof, (0, 0, 0), B, T.round + 8, cap0=False)
    inside = [(mouth - .06, -height - .14), (mouth - .12, -height + .1), (.95, -2.0), (.6, -1.4), (.5, -.6), (.3, -.24), (0, -.2)]
    lathe(g, inside, (0, 0, 0), F['dark'], T.round + 8)
    # the inscription bands
    for y, r in ((-1.0, .64), (-2.36, 1.26)):
        n = T.arc * 3
        sweep(g, sk.circle_section(.025, 4), [(math.cos(2 * math.pi * k / n) * r, y, math.sin(2 * math.pi * k / n) * r) for k in range(n)], B, closed=True)
    # the canons on the crown
    for k in range(4):
        a = math.pi / 2 * k
        pts = [(math.cos(a) * .12 * math.cos(t), -.06 + .16 * math.sin(t), math.sin(a) * .12 * math.cos(t)) for t in np.linspace(0, math.pi, 7)]
        sk.tube(g, pts, .03, B, 5)
    return g


def clapper(T):
    g = Geo()
    sk.cylinder(g, (0, -.1, 0), (0, -1.75, 0), .04, F['iron'], T.small)
    lathe(g, [(0, -2.05), (.1, -1.98), (.13, -1.9), (.1, -1.8), (.05, -1.74)], (0, 0, 0), F['iron'], T.round, cap1=True)
    return g


def gate(T, tall, width):
    g = Geo()
    I = F['iron']
    zs = [-width / 2 + .1 + (width - .2) * i / 8 for i in range(9)]
    for z in zs:
        box(g, -.04, .04, 0, tall, z - .035, z + .035, I, T, bevel=.008, cell=0)
        # the spike
        g.face(((-.04, 0, z - .035), (.04, 0, z - .035), (0, -.14, z)), I, (0, -.3, -1))
        g.face(((.04, 0, z - .035), (.04, 0, z + .035), (0, -.14, z)), I, (1, -.3, 0))
        g.face(((.04, 0, z + .035), (-.04, 0, z + .035), (0, -.14, z)), I, (0, -.3, 1))
        g.face(((-.04, 0, z + .035), (-.04, 0, z - .035), (0, -.14, z)), I, (-1, -.3, 0))
    for k in range(4):
        y = .3 + (tall - .5) * k / 3
        box(g, -.05, .05, y - .04, y + .04, zs[0] - .05, zs[-1] + .05, I, T, bevel=.01, cell=0)
        if T.detail:
            for z in zs:
                for sx in (-1, 1):
                    lathe(g, [(.018, 0), (.012, .012), (0, .016)], (sx * .05, y, z), I, 5, axis=(sx, 0, 0))
    return g


def lanterns(T, h):
    g = Geo()
    x = h['center'][0]
    z0 = h['center'][2] - h['size'][2] / 2
    z1 = h['center'][2] + h['size'][2] / 2
    top = h['center'][1] + h['size'][1] / 2
    for side in (-1, 1):
        for e in range(2):
            z = z0 - .12 if e == 0 else z1 + .12
            cx = x + side * .2
            lathe(g, [(0, top - .58), (.08, top - .56), (.09, top - .45), (.08, top - .34), (0, top - .32)], (cx, 0, z), F['lampred'], 8, cap0=False)
    return g


def lantern_cages(g, T, h):
    x = h['center'][0]
    z0 = h['center'][2] - h['size'][2] / 2
    z1 = h['center'][2] + h['size'][2] / 2
    top = h['center'][1] + h['size'][1] / 2
    bottom = h['center'][1] - h['size'][1] / 2
    tall = top - bottom
    I = F['iron']
    for e in range(2):
        z = z0 - .12 if e == 0 else z1 + .12
        box(g, x - .09, x + .09, bottom, top + tall + .2, z - .06, z + .06, I, T, bevel=.012)
        for side in (-1, 1):
            cx = x + side * .2
            lathe(g, [(.1, top - .62), (.1, top - .58), (.03, top - .58)], (cx, 0, z), I, 6, cap0=True)
            lathe(g, [(.03, top - .32), (.1, top - .32), (.06, top - .24), (.02, top - .18)], (cx, 0, z), I, 6, cap1=True)
            for k in range(4):
                a = math.pi / 2 * k + math.pi / 4
                sk.cylinder(g, (cx + math.cos(a) * .095, top - .58, z + math.sin(a) * .095), (cx + math.cos(a) * .095, top - .32, z + math.sin(a) * .095), .008, I, 4)
            box(g, min(x, cx), max(x, cx), top - .2, top - .16, z - .015, z + .015, I, T, bevel=.004, cell=0)
    box(g, x - .12, x + .12, top + tall + .2, top + tall + .45, z0 - .2, z1 + .2, I, T, bevel=.02)


def fire_zone(T, z, soffit):
    g = Geo()
    x = z['center'][0]
    z0 = z['center'][2] - z['size'][2] / 2
    z1 = z['center'][2] + z['size'][2] / 2
    # the floor's channel: a glowing bed under an iron grate
    box(g, x - .16, x + .16, 0, .012, z0 + .15, z1 - .15, F['ember'], None, bevel=0, cell=0, skip=('-y',))
    if T.detail:
        n = int((z1 - z0 - .3) / .12)
        for i in range(n + 1):
            zz = z0 + .15 + (z1 - z0 - .3) * i / n
            box(g, x - .17, x + .17, .012, .03, zz - .012, zz + .012, F['iron'], T, bevel=.004, cell=0)
    box(g, x - .19, x - .16, 0, .032, z0 + .14, z1 - .14, F['iron'], T, bevel=.005, cell=0)
    box(g, x + .16, x + .19, 0, .032, z0 + .14, z1 - .14, F['iron'], T, bevel=.005, cell=0)
    for i in range(3):
        bz = z0 + .75 + (z1 - z0 - 1.5) * i / 2
        by = soffit - .95
        c = (x, by, bz)
        # the basket: eight bars curving out from its foot to a ring
        for k in range(8):
            a = math.pi / 4 * k + math.pi / 8
            d = (math.cos(a), 0, math.sin(a))
            pts = [sk._add((x, by - .04, bz), d, .12), sk._add((x, by + .12, bz), d, .3), sk._add((x, by + .27, bz), d, .38)]
            sk.tube(g, pts, .016, F['iron'], 4)
        sweep(g, sk.circle_section(.022, 5), [sk._add((x, by + .27, bz), (math.cos(t), 0, math.sin(t)), .38) for t in np.linspace(0, 2 * math.pi, 17)[:-1]], F['iron'], closed=True)
        lathe(g, [(0, -.08), (.12, -.06), (.14, 0)], c, F['iron'], 8, cap0=True)
        # the coals heaped in it
        lathe(g, [(.32, .1), (.28, .18), (.18, .24), (0, .26)], c, F['ember'], 9, cap0=False)
        for k in range(3):
            a = 2 * math.pi * k / 3
            sk.tube(g, [(x + math.cos(a) * .36, by + .28, bz + math.sin(a) * .36), (x, soffit - .02, bz)], .01, F['chain'], 4, caps=False)
    return g


def banner(T, red, length=5.6):
    g = Geo()
    cloth = F['red'] if red else F['blue']
    G = F['clothgold']
    n = 24 if T.fine else (12 if T.i == 1 else 6)
    m = 8 if T.fine else (5 if T.i == 1 else 2)
    w = .7

    def P(i, j):
        t = j / m
        y = -.12 - (length - .25) * i / n
        z = -w + 2 * w * t
        fold = .03 * math.sin(t * math.pi * 5 + i * .08) * (.4 + .6 * i / n)
        # a pointed foot
        if i == n:
            y -= .35 * (1 - abs(2 * t - 1))
        return (fold, y, z)
    for i in range(n):
        for j in range(m):
            a, b, c, d = P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1)
            g.face((a, b, c, d), cloth, (1, 0, 0))
            g.face((d, c, b, a), cloth, (-1, 0, 0))
    # the gold rod, the border and the device
    sk.cylinder(g, (0, -.1, -w - .08), (0, -.1, w + .08), .03, F['gold'], T.small)
    for z in (-w - .1, w + .1):
        lathe(g, [(0, -.06), (.05, -.03), (.04, .02), (0, .05)], (0, -.1, z), F['gold'], 6, axis=(0, 0, 1))
    if T.detail:
        for (y, hz) in ((-.3, w - .02), (-length + .2, w - .02)):
            box(g, -.008, .008 + .02, y - .05, y + .05, -hz, hz, G, None, bevel=0, cell=0)
        for k in range(3):
            y = -length * (.3 + .2 * k)
            for sx in (-1, 1):
                pts = [(sx * .03, y + .2, 0), (sx * .03, y, .16), (sx * .03, y - .2, 0), (sx * .03, y, -.16)]
                g.face(pts, G, (sx, 0, 0))
    # the tassel
    lathe(g, [(0, 0), (.04, -.04), (.05, -.12), (.07, -.24), (0, -.26)], (0, -length - .35, 0), G, 6)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The rigs' fixed fittings (static) and the props into the bank
# ---------------------------------------------------------------------------------------------------------------------------
CAND = {}


def fittings(g, L, T):
    rig = L['rig']
    anchor = rig['anchor']
    for c in rig['chandeliers']:
        x, z = c['zone'][0], c['zone'][2]
        lathe(g, [(0, anchor - .1), (.16, anchor), (.24, anchor + .12), (.28, anchor + .3)], (x, 0, z), F['iron'], T.round, cap0=True)
        sk.tube(g, [(x + math.cos(t) * .06, anchor - .1 - .06 + math.sin(t) * .06, z) for t in np.linspace(0, 2 * math.pi, 9)], .014, F['iron'], 4, closed=True)
    # the censer's yoke across the oculus and its pulley block, the pull rope's cleat on the south bridge
    oc = rig['oculus']
    box(g, -oc - .3, oc + .3, 21.0, 21.35, -.16, .16, F['iron'], T, bevel=.02)
    box(g, -.3, .3, 20.75, 21.05, -.3, .3, F['iron'], T, bevel=.03)
    lathe(g, [(0, -.02), (.08, 0), (.08, .2), (0, .22)], (0, 20.7, -.12), F['iron'], 8, axis=(0, 0, 1))
    box(g, -.12, .12, 4.95, 5.15, -7.1, -6.95, F['iron'], T, bevel=.015)
    # the bell's frame posts
    bx, by, bz = rig['bell']
    for s in (-1, 1):
        box(g, bx + s * 1.0 - .1, bx + s * 1.0 + .1, by - .1, by + .4, bz - .12, bz + .12, F['iron'], T, bevel=.02)
    for h in L['hazards']:
        if h['kind'] == 'shutter':
            lantern_cages(g, T, h)
    # the banners' poles
    for s in (-1, 1):
        for px in (12.0, 18.0, 24.0):
            for sz in (-1, 1):
                x, z0, face = s * px, sz * 5.3, -sz
                sk.cylinder(g, (x, 15.5, z0), (x, 15.5, z0 + face * 1.72), .04, F['iron'], T.small)
                lathe(g, [(0, 0), (.06, .04), (0, .14)], (x, 15.5, z0 + face * 1.72), F['gold'], 6, axis=(0, 0, face))


def bank_props(sb, L, T, soot, floors, no_ao):
    rig = L['rig']
    for c in rig['chandeliers']:
        CAND[c['k']] = c['candles']
    hz = {h['id']: h for h in L['hazards']}
    ti = T.i
    items = []
    pew_block = next(b for b in L['blocks'] if b['id'].startswith('sanctum-pew'))
    items.append(('pew', pew(T, pew_block['size']), None, (pew_block['pos'][0], pew_block['pos'][1], pew_block['pos'][2])))
    items.append(('rig/chandelier', chandelier_body(T), None, (-12.6, 9.0, 0)))
    items.append(('rig/chandelier-wreck', chandelier_body(T, True), None, (-12.6, 0, 0)))
    for k in range(4):
        items.append(('rig/chandelier-candles-%d' % k, chandelier_candles(T, k), None, (-12.6, 9.0, 0)))
        items.append(('rig/wreck-candles-%d' % k, chandelier_candles(T, k, True), None, (-12.6, 0, 0)))
        items.append(('rig/floor-candles-%d' % k, floor_candles(T, k), None, (-12.6, 0, 0)))
    items.append(('rig/chain', chain(T), None, (-12.6, 12, 0)))
    cen = next(h for h in L['hazards'] if h['kind'] == 'saw-rail')
    items.append(('rig/censer', censer(T, rig['censerPivot'] - cen['a'][1]), None, (0, 1.3, -5)))
    items.append(('rig/pull-rope', rope(T), None, (0, 3, -6)))
    items.append(('rig/bell', bell(T, rig['bellMouth'], rig['bellHeight']), None, tuple(rig['bell'])))
    items.append(('rig/clapper', clapper(T), None, tuple(rig['bell'])))
    for h in L['hazards']:
        if h['kind'] == 'shutter':
            items.append(('rig/gate-' + h['id'], gate(T, h['size'][1], h['size'][2]), None, (h['center'][0], h['center'][1] - h['size'][1] / 2, h['center'][2])))
            items.append(('rig/lanterns-' + h['id'], lanterns(T, h), 'world', None))
        if h['kind'] == 'burner':
            for zi, z in enumerate(h['zones']):
                items.append(('rig/fire-%s-%d' % (h['id'], zi), fire_zone(T, z, rig['gallery'] - .4), 'world', None))
    items.append(('rig/banner-red', banner(T, True), None, (-12, 15.5, 4.45)))
    items.append(('rig/banner-blue', banner(T, False), None, (12, 15.5, 4.45)))
    geos = [it[1] for it in items]
    aos = [None] * len(geos)
    if not no_ao:
        aos = sk.bake_ao(geos, distance=.35, samples=(24, 12, 8)[ti])
    for (name, g, frame, rest), ao in zip(items, aos):
        if g.faces == 0:
            continue
        P = g.arrays()[0].astype(np.float64)
        world = P if frame == 'world' else P + np.array(rest)
        bk = sk.Bake(g, ao, world=world, soot=None, floors=floors, seed=7)
        om = sk.to_mesh('%s/%d' % (name, ti), g, bk)
        if name == 'pew':
            mi = sb.mesh(om)
            for b in L['blocks']:
                if b['id'].startswith('sanctum-pew'):
                    sb.share('block/%s/%d' % (b['id'], ti), 'block', mi)
            st = sb.stats.setdefault(('block', str(ti)), [0, 0, 0])
            st[0] += 1
            st[1] += sum(len(s) for s in om.submeshes) // 3
            st[2] += len(om.positions)
        else:
            sb.add('%s/%d' % (name, ti), 'rig', om)
