"""Starfall Causeway's sculpted pieces (causeway_art.py runs it), each in its map block of the west half: the star-winged
guardians, the crystal braziers, the fallen columns, the plaza, the gate hall (its base, walls, great arch and doors), the
star altar, the crown (its slabs, the gable with its window and rose, the broken spire), the lantern gates, the walkway, the
stairs (solid flights and floating treads), the balconies on their flying brackets, and the keystone."""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, sweep, lathe, grid_face
import causeway_kit as ck
from causeway_kit import F, Y, Face, ext, blk, hashf, facet, facet2


def A():
    import causeway_art as art
    return art


def turned(h, angle, t):
    """A local Geo turned about y and moved to t."""
    return h.transformed(sk.affine(sk.quat_axis(0, 1, 0, angle), t))


# ---------------------------------------------------------------------------------------------------------------------------
# The guardians
# ---------------------------------------------------------------------------------------------------------------------------
def guardian(g, b, T, watcher):
    """A star-winged guardian in its block, facing +x in its own frame: a moulded plinth with a rune band, a robed figure
    with a deep hood and a halo, wings folded down its back, and the star it holds: over its head (the warden) or before
    its breast (the watcher)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    H = y1 - y0
    hw = (x1 - x0) / 2
    st = F['statue']
    h = Geo()
    # the plinth
    box(h, -hw + .04, hw - .04, .14, .5, -hw + .04, hw - .04, F['stone'], T, bevel=.02, skip=('-y', '+y'), cell=0)
    ck.mould_round(h, ck.rect_plan(-hw + .04, hw - .04, -hw + .04, hw - .04, 0), [(-.02, 0), (.035, 0), (.035, .08), (.015, .12), (.02, .16), (-.02, .16)], F['carved'], T)
    ck.mould_round(h, ck.rect_plan(-hw + .04, hw - .04, -hw + .04, hw - .04, .48), [(-.02, 0), (0, 0), (.03, .04), (.035, .1), (-.02, .1)], F['carved'], T)
    box(h, -hw + .1, hw - .1, .58, .78, -hw + .1, hw - .1, F['carved'], T, bevel=.03, skip=('-y',), cell=0)
    pl = blk(-hw + .04, hw - .04, .14, .5, -hw + .04, hw - .04)
    for key, fc in A().sides(pl).items():
        fc.runes(h, .12, fc.L - .12, .32, .2, T, F['rune'], 77 + len(key) + int(cx), d=.006)
    s = H - .78

    def R(t):
        return .78 + s * t
    # the robe to the shoulders: an ellipse deeper at the hem, its folds dying out at the belt
    ts = [0, .05, .2, .36, .5, .56, .6, .68, .74, .775, .79]
    dep = [.5, .5, .44, .38, .33, .3, .31, .33, .3, .16, .02]
    wid = [.56, .56, .5, .44, .4, .37, .4, .47, .5, .2, .02]
    if T.i == 2:
        keep = [0, 2, 4, 5, 7, 8, 9, 10]
        ts, dep, wid = [ts[i] for i in keep], [dep[i] for i in keep], [wid[i] for i in keep]
    sides_n = (24, 14, 8)[T.i]
    fold = [max(0.0, 1 - t / .54) for t in ts]
    ck.limb(h, [(0, R(t), 0) for t in ts], dep, wid, st, sides_n, ref=(1, 0, 0),
            mod=(lambda i, a: 1 + .055 * fold[i] * math.sin(a * 7 + .6)) if T.i < 2 else None)
    # the belt and its clasp
    n = T.round
    ck.mould_round(h, [(math.cos(2 * math.pi * k / n) * .305, R(.555), math.sin(2 * math.pi * k / n) * .375) for k in range(n)],
                   [(-.02, 0), (.025, 0), (.035, .04), (.025, .08), (-.02, .08)], F['gold'], T)
    # the hood: its mouth hollowed toward the face, dark within
    ht = [.755, .785, .825, .865, .9, .925]
    hr = [.15, .23, .275, .25, .16, 0.0]
    hs = (14, 10, 6)[T.i]

    def hmod(i, a):
        d = min(a, 2 * math.pi - a)
        return 1 - (.3 * math.exp(-(d / .6) ** 2) if 0 < i < 4 else 0)

    def hfin(i, j):
        a = 2 * math.pi * (j + .5) / hs
        d = min(a, 2 * math.pi - a)
        return F['shade'] if (1 <= i < 3 and d < .62) else st
    ck.limb(h, [(.02, R(t), 0) for t in ht], hr, [r * .95 for r in hr], st, hs, ref=(1, 0, 0), mod=hmod, fin_fn=hfin)
    # the halo behind the head
    if T.i < 2:
        n = T.arc
        ring = [(-.2, R(.84) + math.cos(2 * math.pi * k / n) * .43, math.sin(2 * math.pi * k / n) * .43) for k in range(n)]
        sweep(h, sk.circle_section(.028, 5), ring, F['gold'], closed=True)
        if T.fine:
            ring = [(-.2, R(.84) + math.cos(2 * math.pi * k / n) * .34, math.sin(2 * math.pi * k / n) * .34) for k in range(n)]
            sweep(h, sk.circle_section(.014, 4), ring, F['gold'], closed=True)
    # the wings, folded: long feathered blades from above the shoulders down the back
    for sz in (-1, 1):
        spine = sk.catmull([(-.33, R(.985), sz * .3), (-.47, R(.8), sz * .5), (-.52, R(.5), sz * .47), (-.47, R(.14), sz * .3)], (5, 3, 2)[T.i])
        m = len(spine)
        ra, rb = [], []
        for i in range(m):
            v = i / (m - 1)
            wv = .33 * math.sin(math.pi * min(1.0, v ** .7)) ** .8 if 0 < v < 1 else 0.0
            ra.append(wv)
            rb.append(wv * .22 + (.012 if 0 < v < 1 else 0))
        tiers = 5

        def wmod(i, a, m=m):
            v = i / (m - 1)
            return 1 + .16 * (1 - (v * tiers) % 1.0) if T.i == 0 else 1.0
        ck.limb(h, spine, ra, rb, st, (8, 6, 4)[T.i], ref=(-.45, 0, sz * 1.0), mod=wmod)
    # the arms to the star
    for sz in (-1, 1):
        sh = (0, R(.715), sz * .43)
        if watcher:
            el, hand = (.2, R(.6), sz * .5), (.46, R(.665), sz * .1)
        else:
            el, hand = (.16, R(.86), sz * .52), (.08, min(H - .34, R(.975)), sz * .1)
        path = sk.catmull([sh, el, hand], (3, 2, 1)[T.i])
        m = len(path)
        r = [.155 - .06 * i / (m - 1) for i in range(m)]
        r[-1] = .03
        ck.limb(h, path, r, r, st, (8, 6, 4)[T.i], ref=(1, 0, 0) if watcher else (0, 0, 1))
    star = (.5, R(.68), 0) if watcher else (.08, min(H - .22, R(1) - .2), 0)
    for q in ((0, 0, 0, 1), sk.quat_axis(1, 0, 0, math.pi / 2), sk.quat_axis(0, 0, 1, math.pi / 2)):
        ck.crystal(h, star, .085 if q[3] == 1 else .075, .3 if q[3] == 1 else .24, F['crystal'], sides=4, quat=q, waist=.5, low=1.0)
    if T.fine:
        for k in range(5):
            a = 2 * math.pi * k / 5
            ck.crystal(h, (.02 + math.cos(a) * .15, R(.905), math.sin(a) * .15), .03, .16, F['gold'], sides=4, low=.2, quat=sk.quat_axis(math.sin(a), 0, -math.cos(a), .3))
    fx = (-1 if cx < 0 else 1) if watcher else (1 if cx < 0 else -1)
    g.extend(turned(h, 0.0 if fx > 0 else math.pi, (cx, y0, cz)))


# ---------------------------------------------------------------------------------------------------------------------------
# Braziers, fallen columns
# ---------------------------------------------------------------------------------------------------------------------------
def brazier(g, b, T, seed):
    x0, x1, y0, y1, z0, z1 = ext(b)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    br = F['bronze']
    ck.prism(g, ck.octagon(cx, cz, .52, .2, y0), .1, F['stone'], T)
    for k in range(3):
        a = k * 2 * math.pi / 3 + .4
        path = sk.catmull([(cx + math.cos(a) * .47, y0 + .1, cz + math.sin(a) * .47), (cx + math.cos(a) * .4, y0 + .3, cz + math.sin(a) * .4),
                           (cx + math.cos(a) * .2, y0 + .5, cz + math.sin(a) * .2), (cx + math.cos(a) * .24, y0 + .64, cz + math.sin(a) * .24)], (3, 2, 1)[T.i])
        sweep(g, sk.circle_section(.05, 6 if T.i == 0 else 4), path, br)
        if T.detail:
            lathe(g, [(.0, 0), (.08, .01), (.09, .05), (.05, .09)], (cx + math.cos(a) * .47, y0 + .1, cz + math.sin(a) * .47), br, T.small)
    lathe(g, [(.08, .5), (.2, .56), (.38, .68), (.5, .83), (.53, .95), (.5, .975), (.46, .93), (.38, .86), (.0, .84)], (cx, y0, cz), br, (12, 8, 6)[T.i])
    if T.i < 2:
        lathe(g, [(.5, .93), (.555, .95), (.555, .985), (.5, 1.0), (.47, .97)], (cx, y0, cz), F['gold'], (12, 8, 6)[T.i])
    n = 8
    g.face([(cx + math.cos(2 * math.pi * k / n) * .44, y0 + .9, cz + math.sin(2 * math.pi * k / n) * .44) for k in range(n)], F['crystal'], Y)
    if T.detail:
        for k in range(5):
            a = k * 1.9 + seed
            ck.crystal(g, (cx + math.cos(a) * .2, y0 + .9, cz + math.sin(a) * .2), .06, .1 + .05 * (k % 3), F['violet' if k % 2 else 'crystal'], sides=5, low=.2,
                       quat=sk.quat_axis(math.sin(a), 0, -math.cos(a), .35))


def fluted(n, r, depth=.09):
    pts = []
    for k in range(n):
        a0 = 2 * math.pi * k / n
        a1 = 2 * math.pi * (k + .5) / n
        pts.append((r * math.cos(a0), r * math.sin(a0)))
        pts.append((r * (1 - depth) * math.cos(a1), r * (1 - depth) * math.sin(a1)))
    return pts


def fallen(g, b, T, seed):
    """A fallen column drum lying along its block (fluted, an astragal at one end, the other broken), or, in a squarer
    block, a column's stump on its base."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    lx = (x1 - x0) >= (z1 - z0)
    length = (x1 - x0) if lx else (z1 - z0)
    cross = (z1 - z0) if lx else (x1 - x0)
    r = min(y1 - y0, cross) / 2 - .02
    st = F['carved']
    h = Geo()
    flutes = (12, 8, 6)[T.i]
    if length / (2 * r) > 1.4:
        a, c = -length / 2 + .04, length / 2 - .3
        sec = fluted(flutes, r * .94) if T.i < 2 else sk.circle_section(r * .92, 8)
        sweep(h, sec, [(a + .16, r, 0), (c, r, 0)], st, smooth_u=False, up=Y)
        lathe(h, [(r * .5, 0), (r * .99, 0), (r * 1.0, .05), (r * .93, .1), (r * .97, .13), (r * .93, .16)], (a, r, 0), st, T.round, axis=(1, 0, 0), cap0=True)
        ck.rock(h, (c + .03, r, 0), (.13, r * .86, r * .86), seed, F['broken'], n=3 if T.i < 1 else 2, square=.55, rough=.5, freq=2.4)
        if T.detail:
            ck.rock(h, (c * .3, .1, r * .8), (.3, .12, .14), seed + 1, F['rubble'], n=1, square=.6)
            ck.rock(h, (-c * .5, .09, -r * .82), (.24, .1, .12), seed + 2, F['rubble'], n=1, square=.6)
        g.extend(turned(h, 0.0 if lx else math.pi / 2, (cx, y0, cz)))
    else:
        hx, hz = (x1 - x0) / 2 - .03, (z1 - z0) / 2 - .03
        box(h, -hx, hx, 0, .2, -hz, hz, st, T, bevel=.03, skip=('-y',), cell=0)
        rr = min(hx, hz)
        lathe(h, [(rr * .98, .2), (rr * 1.0, .27), (rr * .93, .33), (rr * .86, .36), (rr * .84, .42), (rr * .9, .46), (rr * .9, .5), (rr * .82, .54)], (0, 0, 0), st, T.round, scale=(hx / rr, hz / rr))
        sec = fluted(flutes, rr * .82) if T.i < 2 else sk.circle_section(rr * .8, 8)
        top = (y1 - y0) - .22
        sweep(h, [(p[0] * hx / rr, p[1] * hz / rr) for p in sec], [(0, .54, 0), (0, top, 0)], st, smooth_u=False, up=(1, 0, 0), caps=False)
        ck.rock(h, (0, top + .02, 0), (hx * .8, .2, hz * .8), seed, F['broken'], n=2 if T.i < 2 else 1, square=.35, rough=.5)
        g.extend(turned(h, 0.0, (cx, y0, cz)))


# ---------------------------------------------------------------------------------------------------------------------------
# Stairs
# ---------------------------------------------------------------------------------------------------------------------------
def stair(g, b, T, seed):
    """A step (the flights climb toward -x in the west half): a solid step with a rounded nosing, or a floating tread with
    a keel under it and a cold line along the keel."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    floating = (y1 - y0) < .3
    st = F['stone']
    if not floating:
        box(g, x0, x1, y0, y1, z0, z1, st, None, bevel=0, cell=0, skip=('-y',))
        if T.i < 1:
            sweep(g, sk.circle_section(.028, 6), [(x1, y1 - .028, z0 + .01), (x1, y1 - .028, z1 - .01)], F['carved'], caps=False)
        return
    box(g, x0, x1, y0, y1, z0, z1, F['carved'], T if T.i == 0 else None, bevel=.025 if T.i == 0 else 0, cell=0)
    if T.i < 1:
        sweep(g, sk.circle_section(.03, 6), [(x1, y1 - .03, z0 + .02), (x1, y1 - .03, z1 - .02)], F['carved'], caps=False)
    xc = (x0 + x1) / 2
    a, c, k = (x0 + .03, y0, 0), (x1 - .03, y0, 0), (xc, y0 - .17, 0)
    za, zb = z0 + .12, z1 - .12
    g.face(((a[0], a[1], za), (k[0], k[1], za + .1), (k[0], k[1], zb - .1), (a[0], a[1], zb)), st, normal=(-.17, -.12, 0))
    g.face(((c[0], c[1], za), (k[0], k[1], za + .1), (k[0], k[1], zb - .1), (c[0], c[1], zb)), st, normal=(.17, -.12, 0))
    g.face(((a[0], a[1], za), (c[0], c[1], za), (k[0], k[1], za + .1)), st, normal=(0, -.1, -.17))
    g.face(((a[0], a[1], zb), (c[0], c[1], zb), (k[0], k[1], zb - .1)), st, normal=(0, -.1, .17))
    g.face(((xc - .02, y0 - .172, za + .2), (xc + .02, y0 - .172, za + .2), (xc + .02, y0 - .172, zb - .2), (xc - .02, y0 - .172, zb - .2)), F['cold'], normal=(0, -1, 0))


# ---------------------------------------------------------------------------------------------------------------------------
# The plaza, the walkway, the balconies
# ---------------------------------------------------------------------------------------------------------------------------
def plaza(g, b, T, seed):
    art = A()
    x0, x1, y0, y1, z0, z1 = ext(b)
    cell = max(T.cell, 1.7)
    grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['flag'], Y, cell)
    g.face(((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)), F['keel'], (0, -1, 0))
    fs = art.sides(b)
    for key in ('+z', '-z'):
        art.flank(g, fs[key], 0, fs[key].L, T, seed + len(key), top=0.0, hull=False, corbels=False, wall='ashlar', cornice=art.FASCIA)
    fc = fs['+x']
    for a0, a1 in ((0.0, 4.8), (fc.L - 4.8, fc.L)):
        art.flank(g, fc, a0, a1, T, seed + 5, top=0.0, hull=False, corbels=False, wall='ashlar', cornice=art.FASCIA)
    fc = fs['-x']
    for a0, a1 in ((0.0, 1.8), (fc.L - 1.8, fc.L)):
        fc.quad(g, a0, a1, y0, y1, 0, F['ashlar'], cell)
    # the bridgehead's rose before the tower's arch
    art.compass_rose(g, (x0 + x1) / 2, y1, 0.0, T, seed, r=1.9)
    for sz in (-1, 1):
        ck.floor_strip(g, x0 + .2, x1 - .2, sz * 9.4 - .025, sz * 9.4 + .025, y1 + .008, F['gold'])
        ck.floor_strip(g, x0, x1, sz * 1.6 - .08, sz * 1.6 + .08, y1 + .008, F['silver'])


def walk(g, b, T, seed):
    art = A()
    x0, x1, y0, y1, z0, z1 = ext(b)
    cell = max(T.cell, 1.7)
    grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['flag'], Y, cell)
    g.face(((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)), F['keel'], (0, -1, 0))
    fs = art.sides(b)
    small = [(0, -.01), (0, .1), (-.08, .12), (-.16, .08), (-.3, .04), (-.36, -.01)]
    for key in ('+z', '-z'):
        art.flank(g, fs[key], 0, fs[key].L, T, seed + len(key) * 3 + (1 if key == '+z' else 0), top=y1, string=y0 + .55, foot=y0, hull=False, corbels=False, cornice=small)
    for key in ('+x', '-x'):
        fc = fs[key]
        fc.quad(g, 0, fc.L, y0, y1, 0, F['keel'], cell)
        fc.mould(g, small, y1, 0, fc.L, F['carved'], T)
        fc.mould(g, art.STRING, y0 + .55, 0, fc.L, F['carved'], T)
        if T.i < 2:
            fc.roundel(g, fc.L / 2, (y0 + y1) / 2 + .2, .6, T, F['carved'], F['cold'], d=.006)
    zc = (z0 + z1) / 2
    ck.floor_strip(g, x0 + .2, x1 - .2, zc - .025, zc + .025, y1 + .008, F['gold'])


def balcony(g, b, T, seed):
    art = A()
    x0, x1, y0, y1, z0, z1 = ext(b)
    north = (z0 + z1) > 0
    grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['flag'], Y, max(T.cell, 1.7))
    g.face(((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)), F['stone'], (0, -1, 0))
    fs = art.sides(b)
    edge = [(0, -.01), (0, .1), (-.06, .12), (-.14, .12), (-.2, .08), (-.32, .05), (-.5, .0), (-.5, -.01)]
    for key in ('+z' if north else '-z', '+x', '-x'):
        fc = fs[key]
        fc.quad(g, 0, fc.L, y0, y1, 0, F['carved'])
        fc.mould(g, edge, y1, 0, fc.L, F['carved'], T)
    # flying brackets from the keel's flank out under the slab
    kf = art.sides(blk(x0, x1, -7, 0, -art.KEEL, art.KEEL))['+z' if north else '-z']
    for i in range(3):
        a = .34 + (kf.L - .68) * i / 2
        ck.bracket(g, kf, a, .36, y0 + .02, (z1 - z0) + 1.6 - .2, 2.4, .22, F['carved'], T)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    ck.floor_ring(g, cx, cz, .8, .045, y1 + .008, F['gold'], T.arc)
    ck.star(g, ck.floor_map(cx, y1 + .009, cz), Y, .62, .14, 4, F['gold'], rot=math.pi / 4)


# ---------------------------------------------------------------------------------------------------------------------------
# The tower: the gate hall's base and walls, the front's arches, the altar
# ---------------------------------------------------------------------------------------------------------------------------
TOWER_PIERS_NS = (.32, 3.45, 6.55, 9.68)
TOWER_PIERS_W = (.32, 5.4, 10.6, 15.68)


def tower(g, b, T, seed):
    """The hall's floor block and the tower's outer faces from its foot to the crown: stepped buttresses the whole height,
    a blind arch in each bay below the floor line and a rune-lit lancet above it, corbels under the crown's overhang."""
    art = A()
    x0, x1, y0, y1, z0, z1 = ext(b)                                   # -38 -28, -7 0, -8 8
    cell = max(T.cell, 1.7)
    grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['flag'], Y, cell)
    g.face(((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)), F['keel'], (0, -1, 0))
    fs = art.sides(b)
    top = 4.0
    for key in ('+z', '-z', '-x'):
        fc = fs[key]
        piers = TOWER_PIERS_NS if key != '-x' else TOWER_PIERS_W
        fc.quad(g, 0, fc.L, y0, y1, 0, F['ashlar'], cell)
        fc.mould(g, art.STRING, art.TAPER_Y, 0, fc.L, F['carved'], T)
        fc.mould(g, art.STRING, 0.0, 0, fc.L, F['carved'], T)
        over = key != '-x'
        for a in piers:
            fc.buttress(g, a, .6, [(y0, -3.4, .62), (-3.4, .25, .48), (.25, 2.55, .34), (2.55, top - (.5 if over else .12), .2)], F['stone'], T, cap=F['carved'])
            if over:
                ck.bracket(g, fc, a, .46, top, 1.72, 1.9, .24, F['carved'], T)
        for i in range(len(piers) - 1):
            a0, a1 = piers[i] + .3, piers[i + 1] - .3
            ac = (a0 + a1) / 2
            half = min(1.2, (a1 - a0) / 2 - .3)
            ys, rise = -4.5, half * 1.42
            fc.arch_fill(g, ac, half, art.TAPER_Y + .1, ys, rise, F['deep'], T, d=.004 if T.i < 2 else .02)
            if T.i < 2:
                fc.arch(g, ac, half, ys, rise, art.ARCHIVOLT, F['carved'], T, legs=ys - art.TAPER_Y - .1)
                fc.roundel(g, ac, ys + rise * .3, .44, T, F['carved'], F['cold'], d=.006)
                fc.box(g, a0 + .12, a1 - .12, -1.38, -.92, 0, .04, F['carved'], T, bevel=.012)
            fc.runes(g, a0 + .3, a1 - .3, -1.15, .27, T, F['rune'], seed * 19 + i + len(key), d=.047)
            # the hall's storey: a lancet with a lit rune down it
            lh = .46
            fc.arch_fill(g, ac, lh, .8, 2.3, .78, F['shade'], T, d=.004 if T.i < 2 else .02, n=4 if T.fine else 3)
            if T.i < 2:
                fc.arch(g, ac, lh, 2.3, .78, art.ARCHIVOLT, F['carved'], T, legs=1.5, n=4 if T.fine else 3)
                fc.box(g, ac - lh - .2, ac + lh + .2, .66, .8, 0, .12, F['carved'], T, bevel=.02)
            if T.fine:
                for k in range(4):
                    ck.glyph(g, lambda x, v, k=k: fc.P(ac + x * .2, 2.25 - k * .36 + v * .3, .012), fc.n, seed * 71 + i * 5 + k, .028, F['rune'], 3)
            else:
                fc.quad(g, ac - .04, ac + .04, 1.0, 2.4, .012 if T.i < 2 else .03, F['rune'])
            if over and T.i < 2:
                m = max(1, int((a1 - a0) / .8))
                for k in range(m):
                    ck.bracket(g, fc, a0 + (a1 - a0) * (k + .5) / m, .26, top, .5, .55, .14, F['carved'], T)
        if over:
            fc.mould(g, [(0, -.01), (0, .08), (-.08, .08), (-.14, .03), (-.14, -.01)], top - .5, 0, fc.L, F['carved'], T)


def wall(g, b, T, seed):
    """A hall wall's block; its inner face carries a plinth, a rune band and a cornice (the outer faces are the tower's)."""
    art = A()
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, F['ashlar'], T, bevel=.012, cell=max(T.cell, 1.7), skip=('-y', '+y'))
    i = b['id']
    fs = art.sides(b)
    inner = {'n': '-z', 's': '+z', 'w': '+x'}.get(i.rsplit('-', 1)[-1])
    if inner is None or '-wall-e' in i:
        return
    fc = fs[inner]
    fc.mould(g, [(0, -.01), (0, .07), (.2, .07), (.26, .03), (.26, -.01)], y0, 0, fc.L, F['carved'], T)
    fc.mould(g, art.STRING, 3.55, 0, fc.L, F['carved'], T)
    if T.i < 2:
        fc.box(g, .3, fc.L - .3, 2.0, 2.4, 0, .03, F['carved'], T, bevel=.01)
    fc.runes(g, .5, fc.L - .5, 2.2, .26, T, F['rune'], seed, d=.036)


def hall_front(g, b, T, seed):
    """The tower's front to the plaza (and its back to the hall): pilasters between the openings, the great arch and the two
    doors (moulded jambs, arches sprung above head height inside the square openings, a lit keystone), a string course."""
    art = A()
    fo = Face((-28.0, 0, 8.0), (1, 0, 0), 16.0)             # a = 8 - z
    fi = Face((-28.6, 0, -8.0), (-1, 0, 0), 16.0)           # a = z + 8
    openings = ((2.5, 1.0, 2.6, 1.93, .62), (8.0, 2.2, 3.4, 2.36, .98), (13.5, 1.0, 2.6, 1.93, .62))
    for fc, outer in ((fo, True), (fi, False)):
        for (ac0, half, head, ys, rise) in openings:
            ac = ac0 if outer else 16 - ac0
            n = None if half > 1.5 else max(3, T.arc // 4)
            fc.spandrel(g, ac, half, ys, rise, head, F['ashlar'], T, n=n)
            if outer:
                fc.soffit(g, ac, half, ys, rise, .6, F['carved'], T, n=n)
            if T.i < 2:
                fc.arch(g, ac, half, ys, rise, art.ARCHIVOLT if outer else art.ARCHLITE, F['carved'], T, legs=ys - .02, n=n)
            big = half > 1.5
            if outer:
                fc.box(g, ac - (.2 if big else .14), ac + (.2 if big else .14), ys + rise - .12, ys + rise + (.46 if big else .3), 0, .17, F['carved'], T, bevel=.03)
                ck.glyph(g, lambda x, v: fc.P(ac + x * (.2 if big else .14), ys + rise + (.17 if big else .09) + v * (.34 if big else .24), .178), fc.n, seed + int(ac0), .035, F['rune'], 3 if T.i < 2 else 2)
        for a in ((.75, 4.65, 11.35, 15.25) if outer else (4.65, 11.35)):
            w, d = .46, .13
            fc.box(g, a - w / 2, a + w / 2, .2, 3.26, 0, d, F['stone'], T, bevel=.02)
            fc.box(g, a - w / 2 - .05, a + w / 2 + .05, 0, .2, 0, d + .05, F['carved'], T, bevel=.02)
            fc.box(g, a - w / 2 - .06, a + w / 2 + .06, 3.26, 3.44, 0, d + .06, F['carved'], T, bevel=.03)
            if T.fine:
                for k in range(6):
                    ck.glyph(g, lambda x, v, k=k: fc.P(a + x * .22, 2.7 - k * .36 + v * .3, d + .006), fc.n, seed * 37 + int(a * 10) + k, .03, F['rune'], 3)
            elif T.i < 2:
                fc.quad(g, a - .03, a + .03, .7, 2.9, d + .004, F['rune'])
        fc.mould(g, art.STRING, 3.55, 0 if outer else .6, 16 if outer else 15.4, F['carved'], T)


def altar(g, b, T, seed):
    """The star altar: a carved stele from the floor to the vault, on each broad face a niche under a pointed arch holding
    a gold star round a cold crystal, between engaged columns; star-silver posts at its ends."""
    art = A()
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0 + .2, y1 - .16, z0, z1, F['stone'], T, bevel=.02, cell=0, skip=('-y', '+y'))
    ck.mould_round(g, ck.rect_plan(x0, x1, z0, z1, y0), art.BASE, F['carved'], T)
    ck.mould_round(g, ck.rect_plan(x0, x1, z0, z1, y1 - .16), art.CAP, F['carved'], T)
    fs = art.sides(b)
    for key in ('+x', '-x'):
        fc = fs[key]
        ac = fc.L / 2
        fc.arch_fill(g, ac, 1.05, .45, 2.25, 1.1, F['shade'], T, d=.004 if T.i < 2 else .02)
        if T.i < 2:
            fc.arch(g, ac, 1.05, 2.25, 1.1, art.ARCHIVOLT, F['carved'], T, legs=1.8)
        ck.star(g, lambda x, v: fc.P(ac + x, 2.05 + v, .014 if T.i < 2 else .03), fc.n, .66, .15, 8, F['gold'])
        if T.i < 2:
            ck.star(g, lambda x, v: fc.P(ac + x, 2.05 + v, .018), fc.n, .4, .09, 8, F['gold'], rot=math.pi / 8)
            q = sk.quat_axis(fc.n[2], 0, -fc.n[0], math.pi / 2)
            ck.crystal(g, fc.P(ac, 2.05, .02), .15, .1, F['crystal'], sides=8, quat=q, low=.0)
        else:
            ck.star(g, lambda x, v: fc.P(ac + x, 2.05 + v, .04), fc.n, .18, .12, 4, F['crystal'])
        for a in (ac - 1.85, ac + 1.85):
            prof = [(.2, .22), (.24, .26), (.24, .34), (.17, .4), (.15, .46), (.15, 3.3), (.17, 3.36), (.23, 3.5), (.24, 3.62), (.2, 3.66)]
            lathe(g, prof, fc.P(a, y0, .02), F['carved'], T.round)
            if T.i < 2:
                fc.quad(g, a - .025, a + .025, .8, 3.0, .176, F['rune'])
        fc.runes(g, .5, fc.L - .5, 3.66, .15, T, F['cold'], seed + len(key), d=.006)
    for z in (z0, z1):
        box(g, x0 - .04, x1 + .04, y0 + .22, y1 - .18, z - .06, z + .06, F['silver'], T, bevel=.02, cell=0)


# ---------------------------------------------------------------------------------------------------------------------------
# The crown: slabs, gable, spire
# ---------------------------------------------------------------------------------------------------------------------------
def crown(g, b, T, seed):
    x0, x1, y0, y1, z0, z1 = ext(b)
    cell = max(T.cell, 1.7)
    grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['flag'], Y, cell)
    grid_face(g, (x0, y0, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['stone'], (0, -1, 0), max(cell, 3))
    art = A()
    edge = [(0, -.01), (0, .07), (-.07, .1), (-.16, .1), (-.22, .06), (-.4, .03), (-.4, -.01)]
    for key, fc in art.sides(b).items():
        fc.quad(g, 0, fc.L, y0, y1, 0, F['carved'])
        o = fc.P(fc.L / 2, 0, 0)
        outer = (key == '+z' and o[2] > 9.7) or (key == '-z' and o[2] < -9.7) or (key == '-x' and o[0] < -37.9) or (key == '+x' and o[0] > -28.05)
        if outer:
            fc.mould(g, edge, y1, 0, fc.L, F['carved'], T)
    if b['id'].endswith('-1'):
        cx, cz = -33.0, -1.0
        ck.floor_ring(g, cx, cz, 2.2, .05, y1 + .008, F['silver'], T.arc * 2)
        ck.floor_rune_ring(g, cx, cz, 1.75, .3, y1 + .012, F['cold'], T, seed)
        ck.star(g, ck.floor_map(cx, y1 + .009, cz), Y, 1.45, .3, 8, F['silver'])
        # the hall's ceiling: stone ribs across it
        for x in (-35.4, -33.0, -30.6):
            box(g, x - .16, x + .16, y0 - .28, y0, -7.4, z1, F['stone'], T, bevel=.04, cell=0, skip=('+y',))


def gable(g, L, T, seed):
    """The crown's gable over the tower's front (four blocks: two wings, the sill, the head): buttress pilasters with
    pinnacles, the window's moulded jambs and arch, a rose of lit petals over it, blind lancets in the wings, and the ragged
    courses of the lost upper storey along its top."""
    art = A()
    y0, y1 = 4.4, 9.2
    fo = Face((-28.0, 0, 7.0), (1, 0, 0), 14.0)             # a = 7 - z
    fi = Face((-28.6, 0, -7.0), (-1, 0, 0), 14.0)           # a = z + 7
    cell = max(T.cell, 1.7)
    for (z0, z1, ya, yb) in ((-7.0, -1.4, y0, y1), (1.4, 7.0, y0, y1), (-1.4, 1.4, y0, 5.4), (-1.4, 1.4, 7.4, y1)):
        box(g, -28.6, -28.0, ya, yb, z0, z1, F['ashlar'], T, bevel=.012, cell=cell, skip=('-y',) if ya == y0 else ())
    for fc, outer in ((fo, True), (fi, False)):
        d = .2 if outer else .13
        for a in (.3, 4.95, 9.05, 13.7):
            fc.box(g, a - .26, a + .26, y0, y1 - .1, 0, d, F['stone'], T, bevel=.025)
            if outer:
                fc.box(g, a - .3, a + .3, y1 - .1, y1 + .04, -.02, d + .05, F['carved'], T, bevel=.03, skip=())
                p = fc.P(a, y1 + .04, d / 2 - .02)
                lathe(g, [(.24, 0), (.2, .1), (.11, .4), (.05, .66), (.08, .71), (0, .82)], p, F['carved'], 4, phase=math.pi / 4, smooth=False)
        fc.mould(g, [(0, -.01), (0, .06), (.16, .06), (.22, .02), (.22, -.01)], y0, 0, 14, F['carved'], T)
        fc.mould(g, art.STRING, 7.55, 0, 5.2, F['carved'], T)
        fc.mould(g, art.STRING, 7.55, 8.8, 14, F['carved'], T)
        fc.mould(g, art.COPE, y1 - .2, 0, 14, F['carved'], T)
        # the window
        fc.spandrel(g, 7, 1.4, 6.78, .6, 7.4, F['ashlar'], T)
        if outer:
            fc.soffit(g, 7, 1.4, 6.78, .6, .6, F['carved'], T)
        if T.i < 2:
            fc.arch(g, 7, 1.4, 6.78, .6, art.ARCHIVOLT if outer else art.ARCHLITE, F['carved'], T, legs=6.78 - 5.42)
        fc.box(g, 5.45, 8.55, 5.28, 5.42, 0, .12, F['carved'], T, bevel=.03)
        fc.quad(g, 5.6, 8.4, 5.33, 5.37, .124, F['gold'])
        # the rose over it
        if outer:
            fc.roundel(g, 7, 8.36, .66, T, F['carved'], F['rune'], d=.0, points=8)
            if T.i < 2:
                n = T.arc
                ring = [fc.P(7 + math.cos(2 * math.pi * k / n) * .78, 8.36 + math.sin(2 * math.pi * k / n) * .78, .01) for k in range(n)]
                sweep(g, sk.circle_section(.025, 4), ring, F['gold'], closed=True)
                q = sk.quat_axis(0, 0, -1, math.pi / 2)
                ck.crystal(g, fc.P(7, 8.36, .03), .12, .09, F['crystal'], sides=8, quat=q, low=.0)
        else:
            fc.runes(g, 5.5, 8.5, 8.3, .3, T, F['cold'], seed, d=.006)
        # blind lancets in the wings
        for ac in (1.75, 3.5, 10.5, 12.25):
            fc.arch_fill(g, ac, .46, y0 + .7, 6.5, .8, F['deep'] if outer else F['shade'], T, d=.004 if T.i < 2 else .02, n=4 if T.fine else 3)
            if T.i < 2:
                fc.arch(g, ac, .46, 6.5, .8, art.ARCHLITE if not outer else art.ARCHIVOLT, F['carved'], T, legs=6.5 - y0 - .7, n=4 if T.fine else 3)
            if outer and T.fine:
                for k in range(4):
                    ck.glyph(g, lambda x, v, k=k: fc.P(ac + x * .2, 6.45 - k * .36 + v * .3, .012), fc.n, seed * 13 + int(ac * 4) + k, .028, F['runedim'], 3)
            if not outer:
                fc.quad(g, ac - .03, ac + .03, y0 + 1.0, 6.4, .012 if T.i < 2 else .03, F['cold'])
    # the lost storey's ragged courses
    k = 0
    z = -6.6
    while z < 6.7:
        w = .34 + .3 * hashf(seed, k, 1)
        hgt = (.1 + .14 * hashf(seed, k, 2)) * (1.0 if hashf(seed, k, 3) > .3 else .0)
        if hgt > .09 and abs(abs(z) - 2.1) > .45 and abs(abs(z) - 6.7) > .5:
            ck.rock(g, (-28.3, y1 + hgt / 2 - .03, z + w / 2), (.27, hgt / 2 + .03, w / 2), seed * 5 + k, F['broken' if k % 3 == 0 else 'stone'], n=1 if T.i else 2, square=.78, rough=.16)
        z += w + .02
        k += 1


def spire(g, b, T, seed):
    """The broken spire: an eight-sided turret on a moulded foot, three storeys of rune-lit lancets between string courses,
    its top a ring of broken stones."""
    art = A()
    x0, x1, y0, y1, z0, z1 = ext(b)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    h = (x1 - x0) / 2
    box(g, x0, x1, y0, y0 + .5, z0, z1, F['stone'], T, bevel=.03, cell=0, skip=('-y',))
    ck.mould_round(g, ck.rect_plan(x0, x1, z0, z1, y0 + .5), [(-.02, 0), (.0, 0), (-.1, .22), (-.02, .22)], F['carved'], T)
    ya, yb = y0 + .5, y1 - .55
    ha, hb = h - .1, h - .22
    cut_a, cut_b = ha * .6, hb * .6
    ck.prism(g, ck.octagon(cx, cz, ha, cut_a, ya), yb - ya, F['ashlar'], T, cap=False, top_poly=ck.octagon(cx, cz, hb, cut_b, yb))
    g.face(ck.octagon(cx, cz, hb, cut_b, yb), F['broken'], Y)
    storeys = 3
    for s in range(storeys + 1):
        t = s / storeys
        y = ya + (yb - ya) * t
        hh = ha + (hb - ha) * t
        if 0 < s < storeys or s == 0:
            ck.mould_round(g, ck.octagon(cx, cz, hh, hh * .6, y - (.0 if s else 0)), [(-.02, 0), (.05, 0), (.08, .05), (.05, .1), (.02, .14), (-.02, .14)], F['carved'], T)
    for s in range(storeys):
        tm = (s + .5) / storeys
        ym = ya + (yb - ya) * tm
        hh = ha + (hb - ha) * tm
        for (nx, nz) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            w = hh * .8
            o = (cx + nx * (hh + .004) - nz * w / 2, 0, cz + nz * (hh + .004) + nx * w / 2)
            fc = Face(o, (nx, 0, nz), w)
            lh = min(.3, w / 2 - .12)
            fc.arch_fill(g, w / 2, lh, ym - .8, ym + .35, lh * 1.5, F['shade'], T, d=.0 if T.i < 2 else .02, n=3)
            if T.i < 2:
                fc.arch(g, w / 2, lh, ym + .35, lh * 1.5, art.ARCHLITE, F['carved'], T, legs=1.15, n=3)
                fc.quad(g, w / 2 - .03, w / 2 + .03, ym - .6, ym + .45, .008, F['rune' if s % 2 == 0 else 'cold'])
    # the broken top
    n = 9 if T.i < 2 else 6
    for k in range(n):
        a = 2 * math.pi * k / n + .3
        hgt = .25 + .55 * hashf(seed, k, 4)
        ck.rock(g, (cx + math.cos(a) * hb * .78, yb + hgt / 2 - .05, cz + math.sin(a) * hb * .78), (.3, hgt / 2 + .06, .26), seed * 3 + k, F['broken' if k % 2 else 'stone'], n=1 if T.i else 2,
                square=.7, rough=.25, quat=sk.quat_axis(0, 1, 0, -a))


# ---------------------------------------------------------------------------------------------------------------------------
# The lantern gates
# ---------------------------------------------------------------------------------------------------------------------------
def lantern(g, fc, a, y, T):
    """A lantern on a wall: a scrolled bracket and a six-sided lamp of warm glass in a bronze frame (within a hand of the
    wall)."""
    br = F['bronze']
    p0, p1 = fc.P(a, y + .52, 0), fc.P(a, y + .5, .15)
    if T.i < 2:
        sweep(g, sk.circle_section(.014, 4), sk.catmull([p0, fc.P(a, y + .58, .08), p1], 3), br)
    c = fc.P(a, y, .145)
    lathe(g, [(.0, .5), (.05, .47), (.095, .41), (.085, .4)], c, br, 6, smooth=False)
    lathe(g, [(.08, .4), (.09, .22), (.07, .08)], c, F['lamp'], 6, smooth=False)
    lathe(g, [(.075, .08), (.085, .06), (.04, .02), (.0, -.04)], c, br, 6, smooth=False)


def gate(g, L, b, T, seed):
    art = A()
    x0, x1, y0, y1, z0, z1 = ext(b)
    north = (z0 + z1) > 0
    cell = max(T.cell, 1.7)
    if b['id'].endswith('-head'):
        # the lintel over the door to the walkway's stair: carried on a corbelled half arch from the wall, its far end broken
        ck.toothed(g, b, F['ashlar'], T, not north, north, seed, course=.35, jag=.7, bevel=.015)
        wallb = blk(x0, x1, 0, 4, 4.0 if north else -7.4, 7.4 if north else -4.0)
        fc = art.sides(wallb)['+z' if north else '-z']
        ck.bracket(g, fc, fc.L / 2, fc.L - .08, y0 + .01, 1.5, .62, .1, F['carved'], T)
        for key in ('+x', '-x'):
            f2 = art.sides(b)[key]
            lo, hi = (.0, f2.L - .7) if ((key == '-x') == north) else (.7, f2.L)
            f2.mould(g, art.COPE, y1 - .2, lo, hi, F['carved'], T)
        return
    box(g, x0, x1, y0, y1, z0, z1, F['ashlar'], T, bevel=.012, cell=cell, skip=('-y',))
    fs = art.sides(b)
    for key in ('+x', '-x'):
        fc = fs[key]
        fc.mould(g, [(0, -.01), (0, .07), (.2, .07), (.26, .03), (.26, -.01)], y0, 0, fc.L, F['carved'], T)
        fc.mould(g, art.COPE, y1 - .2, 0, fc.L, F['carved'], T)
        fc.mould(g, art.STRING, 3.3, 0, fc.L, F['carved'], T)
        for a in (.3, fc.L - .3):
            fc.box(g, a - .22, a + .22, .26, 3.2, 0, .12, F['stone'], T, bevel=.02)
        ac = fc.L / 2
        fc.arch_fill(g, ac, .82, .45, 1.9, 1.0, F['deep'], T, d=.004 if T.i < 2 else .02)
        if T.i < 2:
            fc.arch(g, ac, .82, 1.9, 1.0, art.ARCHIVOLT, F['carved'], T, legs=1.45)
            fc.roundel(g, ac, 2.15, .3, T, F['carved'], F['rune'], d=.006, points=4)
        if T.fine:
            for k in range(3):
                ck.glyph(g, lambda x, v, k=k: fc.P(ac + x * .2, 1.6 - k * .36 + v * .3, .012), fc.n, seed * 29 + k + len(key), .028, F['runedim'], 3)
        # the lantern by the door
        door_hi = (key == '+x') != north
        lantern(g, fc, (fc.L - .3) if door_hi else .3, 1.84, T)


# ---------------------------------------------------------------------------------------------------------------------------
# The keystone (on the centre line: its own twin)
# ---------------------------------------------------------------------------------------------------------------------------
def keystone(g, b, T):
    """The floating keystone of the Breach: a carved cap with a rune band, the stone below it broken to a point that holds
    a cold crystal."""
    art = A()
    x0, x1, y0, y1, z0, z1 = ext(b)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    hx, hz = (x1 - x0) / 2, (z1 - z0) / 2
    grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['flag'], Y, 1.2)
    for key, fc in art.sides(b).items():
        fc.quad(g, 0, fc.L, y1 - .62, y1, 0, F['carved'])
        fc.mould(g, [(0, -.01), (0, .05), (-.05, .08), (-.12, .08), (-.16, .03), (-.16, -.01)], y1, 0, fc.L, F['carved'], T)
        fc.mould(g, art.STRING, y1 - .62, 0, fc.L, F['carved'], T)
        fc.runes(g, .2, fc.L - .2, y1 - .34, .22, T, F['rune'], 400 + len(key) + int(fc.n[0] * 3), d=.006)
    # the broken body: rings shrinking to a point, chiselled
    rows = (7, 5, 3)[T.i]
    cols = (16, 12, 8)[T.i]
    P = np.zeros((rows + 1, cols, 3))
    for i in range(rows + 1):
        t = i / rows
        y = y1 - .62 - (y1 - .62 - (y0 + .5)) * t
        k = (1 - t) ** .8 * .96 + .1
        for j in range(cols):
            a = 2 * math.pi * j / cols
            sq = max(abs(math.cos(a)), abs(math.sin(a)))
            r = (1 / sq) * (1 - t * .25) + (1 - (1 - t * .25))
            nz = .82 + .36 * hashf(i * 3, j * 7, 5) if 0 < i else 1.0
            P[i, j] = (cx + math.cos(a) * r * hx * k * nz, y, cz + math.sin(a) * r * hz * k * nz)
    ck.grid_surface(g, P, F['broken'], closed_v=True, inside=(cx, (y0 + y1) / 2, cz), flat=True)
    ck.crystal(g, (cx, y0 + .62, cz), .26, .3, F['crystal'], sides=6, low=2.0, waist=.5)
    if T.i < 2:
        for k in range(4):
            a = k * math.pi / 2 + math.pi / 4
            path = sk.catmull([(cx + math.cos(a) * .3, y0 + .95, cz + math.sin(a) * .3), (cx + math.cos(a) * .34, y0 + .7, cz + math.sin(a) * .34), (cx + math.cos(a) * .16, y0 + .45, cz + math.sin(a) * .16)], 3)
            sweep(g, sk.circle_section(.035, T.small), path, F['gold'], scale=[1, 1, .95, .9, .8, .65, .4])
    M = ck.floor_map(cx, y1 + .009, cz)
    ck.star(g, M, Y, .9, .2, 8, F['gold'])
    ck.floor_ring(g, cx, cz, 1.0, .04, y1 + .009, F['gold'], T.arc)


# ---------------------------------------------------------------------------------------------------------------------------
def sculpt(g, L, b, T, seed):
    i = b['id']
    if i.startswith('causeway-warden-'):
        guardian(g, b, T, False)
    elif i.startswith('causeway-watcher-'):
        guardian(g, b, T, True)
    elif i.startswith('causeway-brazier-'):
        brazier(g, b, T, seed)
    elif i.startswith('causeway-fallen-'):
        fallen(g, b, T, seed)
    elif i.startswith('causeway-stair-'):
        stair(g, b, T, seed)
    elif i.startswith('causeway-plaza-'):
        plaza(g, b, T, seed)
    elif i.startswith('causeway-walk-'):
        walk(g, b, T, seed)
    elif i.startswith('causeway-balcony-'):
        balcony(g, b, T, seed)
    elif i.endswith('-floor'):
        tower(g, b, T, seed)
        hall_front(g, b, T, seed)
    elif i.startswith('causeway-hall-'):
        wall(g, b, T, seed)
    elif i.startswith('causeway-altar-'):
        altar(g, b, T, seed)
    elif i.startswith('causeway-crown-'):
        crown(g, b, T, seed)
    elif i.startswith('causeway-gable-'):
        if i.endswith('-s'):
            gable(g, L, T, seed)
    elif i.startswith('causeway-spire-'):
        spire(g, b, T, seed)
    elif i.startswith('causeway-gate-'):
        gate(g, L, b, T, seed)
    else:
        x0, x1, y0, y1, z0, z1 = ext(b)
        box(g, x0, x1, y0, y1, z0, z1, F['ashlar'], T, bevel=.03)


def centre(g, L, T):
    for b in L['blocks']:
        if b['id'] == 'causeway-keystone':
            keystone(g, b, T)
