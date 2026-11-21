"""What hangs in the void round Starfall Causeway (causeway_art.py runs it): the modelled backdrop that stands in for Core
NativeCausewayScenery's box-built sectors while the art draws (the planet and its rings stay that file's own meshes).

near(L, T): under the map, in the static chunks with their three tiers: the towers' stumps (battered tiers on buttresses, rune
bands, a broken underside, the violet crystal heart in its cradle, the masonry breaking away below), the hanging arches under
each half span with their hangers and pendant finials, the lantern chains under the walkways, the rubble frozen in its fall
under the Breach.

far(L, T): beyond the bounds, as a few chunks with one tier each (they are never near enough for a finer one): the causeway's
lost spans drifting on past both towers (lengths of the same span, both ends broken, with their balustrades and pylons), the
ruined shrine on its floating rock, the kneeling colossus and its sword, single columns on small islands, the asteroid belt,
the far islands and obelisks. Their places are Core NativeCausewayScenery's (same hashes), so the void reads as it did.
"""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, sweep, lathe, grid_face
import causeway_kit as ck
from causeway_kit import F, Y, Face, ext, blk, hashf, facet, facet2


def A():
    import causeway_art as art
    return art


def P():
    import causeway_parts as parts
    return parts


def H(a, b=0, c=0):
    return hashf(a + 5501, b + 97, c + 193)


def euler(pitch, yaw, roll):
    """Unity's Euler angles (degrees): yaw about y, then pitch about x, then roll about z."""
    q = ck.quat_mul(sk.quat_axis(0, 1, 0, math.radians(yaw)), sk.quat_axis(1, 0, 0, math.radians(pitch))) if True else None
    return ck.quat_mul(q, sk.quat_axis(0, 0, 1, math.radians(roll)))


# ---------------------------------------------------------------------------------------------------------------------------
# near
# ---------------------------------------------------------------------------------------------------------------------------
def stump(g, cx, T, seed):
    art = A()
    w, d, y = 10.0, 16.0, -7.0
    cell = max(T.cell, 3.0)
    for t in range(5):
        h = 3.2 + t * .6
        b = blk(cx - w / 2, cx + w / 2, y - h, y, -d / 2, d / 2)
        for key, fc in art.sides(b).items():
            fc.quad(g, 0, fc.L, y - h, y, 0, F['keel'], cell)
            piers = art_piers(key, fc.L)
            for a in piers:
                fc.box(g, a - .28, a + .28, y - h, y - .1, 0, .32, F['stone'], None, bevel=0, skip=('back', 'top'))
                g.face((fc.P(a - .28, y - .1, .32), fc.P(a + .28, y - .1, .32), fc.P(a + .28, y + .3, 0), fc.P(a - .28, y + .3, 0)), F['carved'], normal=sk._add(sk._lerp((0, 0, 0), fc.n, .4), Y, .32))
            fc.mould(g, art.STRING, y - .12, 0, fc.L, F['carved'], T)
            if t < 4:
                for i in range(len(piers) - 1):
                    a0, a1 = piers[i] + .55, piers[i + 1] - .55
                    if a1 - a0 > 1.0:
                        if T.fine:
                            fc.runes(g, a0, a1, y - h * .55, .5, T, F['rune'], seed * 7 + t * 5 + i, d=.01)
                        else:
                            fc.quad(g, a0, a1, y - h * .55 - .07, y - h * .55 + .07, .03, F['rune'])
        g.face(((b['pos'][0] - w / 2, y - h, -d / 2), (b['pos'][0] + w / 2, y - h, -d / 2), (b['pos'][0] + w / 2, y - h, d / 2), (b['pos'][0] - w / 2, y - h, d / 2)), F['broken'], (0, -1, 0))
        y -= h
        w -= 1.1
        d -= 1.6
    # the broken base: jagged stones hanging off it, masonry breaking away below
    n = (14, 10, 6)[T.i]
    for i in range(n):
        bx, bz, bh = cx + (H(seed, i, 1) - .5) * w, (H(seed, i, 2) - .5) * d, 2 + H(seed, i, 3) * 5
        ck.rock(g, (bx, y - bh / 2 + .6, bz), (.8 + H(seed, i, 4) * 1.3, bh / 2, .8 + H(seed, i, 5) * 1.3), seed * 13 + i, F['broken' if i % 3 else 'rockpale'], n=2 if T.i < 1 else 1,
                square=.5, rough=.4, quat=sk.quat_axis(H(seed, i, 6) - .5, 1, H(seed, i, 7) - .5, .25))
    for i in range((22, 16, 10)[T.i]):
        r, a = H(seed + 1, i) * 9, H(seed + 1, i, 1) * 2 * math.pi
        by, sz = y - 6 - H(seed + 1, i, 2) * 42 - i * .8, .5 + H(seed + 1, i, 3) * 1.5
        q = sk.quat_axis(H(seed + 1, i, 6) - .5, H(seed + 1, i, 7) - .5, H(seed + 1, i, 8) - .5, H(seed + 1, i, 4) * 6)
        ck.rock(g, (cx + math.cos(a) * r, by, math.sin(a) * r), (sz, sz * (.4 + H(seed + 1, i, 4) * .5), sz * (.5 + H(seed + 1, i, 5) * .4)), seed * 17 + i, F['rubble' if i % 3 == 0 else 'rockpale'],
                n=1, square=.72, rough=.2, quat=q)
        if i % 4 == 0:
            ck.crystal(g, (cx + math.cos(a) * r, by - sz * .5, math.sin(a) * r), .14, .3, F['cold'], sides=4, low=1.0)
    # the crystal heart in its star-metal cradle
    hy = y - 10
    ck.crystal(g, (cx, hy, 0), .95, 4.2, F['violet'], sides=6, low=.75)
    ck.crystal(g, (cx, hy - 3.6, 0), .42, 1.4, F['crystal'], sides=6, low=1.2)
    for k in range(4):
        a = k * math.pi / 2
        path = sk.catmull([(cx + math.cos(a) * 3.2, y + .4, math.sin(a) * 3.2), (cx + math.cos(a) * 2.6, hy + 3.5, math.sin(a) * 2.6), (cx + math.cos(a) * 1.5, hy + .6, math.sin(a) * 1.5),
                           (cx + math.cos(a) * 1.05, hy - .4, math.sin(a) * 1.05)], (4, 3, 2)[T.i])
        sweep(g, sk.circle_section(.16, T.small), path, F['metal'])


def art_piers(key, length):
    parts = P()
    base = parts.TOWER_PIERS_NS if key in ('+z', '-z') else parts.TOWER_PIERS_W
    full = 10.0 if key in ('+z', '-z') else 16.0
    return [.35 + (a - .32) / (full - .64) * (length - .7) for a in base]


def hanging_arches(g, x0, x1, T, seed):
    """Two inverted arches hung under a half span's keel: twin stone ribs, a curved soffit between them with a rune line,
    hangers up to the keel, a turned pendant at each low point with a cold crystal tip."""
    for bay in (0, 1):
        a0, a1 = x0 + (x1 - x0) * bay / 2, x0 + (x1 - x0) * (bay + 1) / 2
        sag = 6.2
        n = (16, 10, 6)[T.i]
        pts = [(a0 + (a1 - a0) * k / n, -7.6 - sag * math.sin(math.pi * k / n)) for k in range(n + 1)]
        for z in (-2.25, 2.25):
            sweep(g, sk.rect_section(.8, 1.0, .14 if T.i < 2 else 0), [(x, y, z) for x, y in pts], F['stone'], up=(0, 0, 1))
            if T.i < 2:
                for k in range(2, n - 1, 2):
                    x, y = pts[k]
                    if -7.05 - y > .9:
                        sk.cylinder(g, (x, y + .3, z), (x, -7.02, z), .13, F['carved'], T.small, caps=False)
        # the soffit between the ribs, seen from above and below
        for k in range(n):
            (xa, ya), (xb, yb) = pts[k], pts[k + 1]
            nr = sk._norm((-(yb - ya), (xb - xa), 0))
            for s in (1, -1):
                g.face(((xa, ya - .1 * s, -1.9), (xb, yb - .1 * s, -1.9), (xb, yb - .1 * s, 1.9), (xa, ya - .1 * s, 1.9)), F['keel'], normal=(nr[0] * -s, nr[1] * -s, 0))
            g.face(((xa, ya - .215, -.09), (xb, yb - .215, -.09), (xb, yb - .215, .09), (xa, ya - .215, .09)), F['rune'], normal=(-nr[0], -nr[1], 0))
        mid, py = (a0 + a1) / 2, -7.6 - sag - .5
        lathe(g, [(.0, 0), (.55, -.05), (.7, -.4), (.45, -1.0), (.24, -1.7), (.36, -2.1), (.3, -2.5), (.1, -3.2), (.0, -3.3)][::-1], (mid, py + .3, 0), F['carved'], T.round)
        ck.crystal(g, (mid, py - 3.4, 0), .2, .5, F['cold'], sides=6, low=2.4)


def lantern_chain(g, x, z, length, T):
    top = -7.02
    sk.cylinder(g, (x, top, z), (x, top - length, z), .03, F['metal'], 4, caps=False)
    c = (x, top - length - .62, z)
    lathe(g, [(.0, .62), (.1, .58), (.26, .46), (.24, .44)], c, F['bronze'], 6, smooth=False)
    lathe(g, [(.22, .44), (.25, .22), (.18, .06)], c, F['lamp'], 6, smooth=False)
    lathe(g, [(.19, .06), (.2, .03), (.08, -.04), (.0, -.14)], c, F['bronze'], 6, smooth=False)


def near(L, T):
    g = Geo()
    h = Geo()
    stump(h, -33.0, T, 3)
    hanging_arches(h, -20.0, -3.0, T, 5)
    g.extend(h)
    g.extend(h.transformed([-1, 0, 0, 0, 0, 1, 0, 0, 0, 0, -1, 0]))
    for side in (-1, 1):
        x = -14.0
        while x <= 14:
            lantern_chain(g, x, side * 8.3, 3.2 + H(61, int(x), side) * 3.2, T)
            x += 4
    # the rubble frozen in its fall below the Breach
    for i in range((26, 18, 10)[T.i]):
        x, z, y, sz = (H(71, i) - .5) * 7, (H(71, i, 1) - .5) * 9, -8 - H(71, i, 2) * 36 - i * .4, .35 + H(71, i, 3) * 1.1
        q = sk.quat_axis(H(71, i, 4) - .5, H(71, i, 5) - .5, H(71, i, 6) - .5, H(71, i, 7) * 6)
        ck.rock(g, (x, y, z), (sz * 1.3, sz * .65, sz), 500 + i, F['rubble' if i % 3 == 0 else 'rockpale'], n=2 if (T.i == 0 and sz > 1.0) else 1, square=.72, rough=.2, quat=q)
        if i % 5 == 0:
            ck.crystal(g, (x, y + sz * .5, z), .1, .34, F['rune'], sides=4, low=1.0, quat=q)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# far
# ---------------------------------------------------------------------------------------------------------------------------
def lost_span(x, y, z, length, yaw, roll, seed, T, far=False):
    art = A()
    h = Geo()
    art.span(h, -length / 2, length / 2, T, True, True, seed, inlays=False, corbels=not far)
    for f in (-1, 1):
        rb = blk(-length * .46 + 1.4, length * .46 - 1.4, 0, 1.1, f * 4.85 - .15, f * 4.85 + .15, 'r')
        if not far:
            art.rail(h, rb, T, True, True, seed + f)
        else:
            box(h, -length * .4, length * .4, 0, 1.1, f * 4.85 - .15, f * 4.85 + .15, F['stone'], None, bevel=0, cell=0, skip=('-y',))
        for u in (-length * .3, length * .3):
            art.pylon(h, blk(u - .6, u + .6, 0, 4.8, f * 4.4 - .6, f * 4.4 + .6), T, seed, lite=True)
            ck.crystal(h, (u, 5.6, f * 4.4), .2, .34, F['crystal'], sides=4, low=1.0)
        h.face(((-length / 2 + 1.7, .02, f * 1.6 - .1), (length / 2 - 1.7, .02, f * 1.6 - .1), (length / 2 - 1.7, .02, f * 1.6 + .1), (-length / 2 + 1.7, .02, f * 1.6 + .1)), F['rune'], Y)
    h.face(((-length * .35, -7.02, -.5), (length * .35, -7.02, -.5), (length * .35, -7.02, .5), (-length * .35, -7.02, .5)), F['rune'], (0, -1, 0))
    return h.transformed(sk.affine(euler(0, yaw, roll), (x, y, z)))


def island(g, x, y, z, r, seed, T, top=None):
    """A floating rock: a flat weathered top, a long broken underside tapering to a point, crystals in its flanks."""
    rows, cols = (7, 12) if T.i < 2 else (5, 9)
    Pn = np.zeros((rows + 1, cols, 3))
    depth = r * 1.5
    for i in range(rows + 1):
        t = i / rows
        k = (1 - t) ** .75
        for j in range(cols):
            a = 2 * math.pi * j / cols
            nz = .72 + .56 * H(seed, i * 5 + 1, j) if i else .85 + .3 * H(seed, 0, j)
            rr = r * (k * nz + .02)
            Pn[i, j] = (x + math.cos(a) * rr * 1.08 + (H(seed, i, 99) - .5) * r * .25 * t, y - depth * t ** 1.2 - (.0 if i else 0), z + math.sin(a) * rr * .92 + (H(seed, i, 98) - .5) * r * .25 * t)
    ck.grid_surface(g, Pn, F['rock'], closed_v=True, inside=(x, y - depth * .3, z), flat=True)
    g.face([tuple(Pn[0, j]) for j in range(cols)][::-1], F['rockpale'] if top is None else top, Y)
    for k in range(6 if T.i < 2 else 3):
        a, dd = H(seed, k, 3) * 2 * math.pi, r * .55
        ck.crystal(g, (x + math.cos(a) * dd, y - 2 - H(seed, k, 4) * 6, z + math.sin(a) * dd), .4, 1.6, F['cold' if k % 2 == 0 else 'violet'], sides=5, low=.8,
                   quat=sk.quat_axis(math.sin(a), 0, -math.cos(a), .5 + H(seed, k, 5) * .5))


def column(g, x, y, z, hgt, r, T, broken=False, seed=0):
    lathe(g, [(r * 1.5, 0), (r * 1.5, .25 * r), (r * 1.15, .5 * r), (r * 1.25, .7 * r), (r, 1.0 * r), (r * .9, hgt - r), (r * 1.05, hgt - .8 * r), (r * 1.45, hgt - .2 * r), (r * 1.5, hgt)] if not broken else
          [(r * 1.5, 0), (r * 1.5, .25 * r), (r * 1.15, .5 * r), (r * 1.25, .7 * r), (r, 1.0 * r), (r * .93, hgt)], (x, y, z), F['carved'], 8 if T.i < 2 else 6, cap1=not broken)
    if broken:
        ck.rock(g, (x, y + hgt, z), (r * .9, r * .7, r * .9), seed, F['broken'], n=1, square=.3, rough=.5)


def shrine(g, x, y, z, T):
    """A ring of columns under a broken dome on a stepped round platform, a lit altar within."""
    lathe(g, [(11.6, 0), (11.6, .5), (10.4, .5), (10.4, .9), (9.4, .9), (9.4, 1.2), (0, 1.2)], (x, y, z), F['stone'], 20, smooth=False)
    n = 10
    tall = []
    for k in range(n):
        a = k * 2 * math.pi / n
        cx, cz = x + math.cos(a) * 7.5, z + math.sin(a) * 7.5
        broken = k % 3 == 1
        hgt = 5 + H(311, k) * 3 if broken else 11
        column(g, cx, y + 1.2, cz, hgt, .58, T, broken, 311 + k)
        tall.append(not broken)
    # the entablature over the standing columns, in its curve
    for k in range(n):
        if not (tall[k] and tall[(k + 1) % n]):
            continue
        path = [(x + math.cos(a) * 7.5, y + 12.75, z + math.sin(a) * 7.5) for a in [(k + i / 4) * 2 * math.pi / n for i in range(5)]]
        sweep(g, sk.rect_section(1.25, 1.0, .12), path, F['carved'], up=Y)
    # the broken dome: ribs rising to a ring
    for k in range(5):
        if k == 3:
            continue
        a = k * 2 * math.pi / 5 + .4
        path = sk.catmull([(x + math.cos(a) * 7.4, y + 13.2, z + math.sin(a) * 7.4), (x + math.cos(a) * 6.2, y + 16.2, z + math.sin(a) * 6.2), (x + math.cos(a) * 4.0, y + 18.3, z + math.sin(a) * 4.0),
                           (x + math.cos(a) * 2.0, y + 19.2, z + math.sin(a) * 2.0)], 3)
        sweep(g, sk.rect_section(.6, .7, .1), path, F['stone'], up=Y)
    ring = [(x + math.cos(2 * math.pi * k / 12) * 2.0, y + 19.3, z + math.sin(2 * math.pi * k / 12) * 2.0) for k in range(12)]
    sweep(g, sk.circle_section(.42, 6), ring, F['carved'], closed=True)
    box(g, x - 1.2, x + 1.2, y + 1.2, y + 3.0, z - .7, z + .7, F['stone'], None, bevel=.12, cell=0)
    g.face(((x - 1.25, y + 3.03, z - .75), (x + 1.25, y + 3.03, z - .75), (x + 1.25, y + 3.03, z + .75), (x - 1.25, y + 3.03, z + .75)), F['rune'], Y)
    ck.crystal(g, (x, y + 4.7, z), .5, 1.3, F['crystal'], sides=6, low=.8)


def colossus(g, x, y, z, T):
    """A fallen guardian colossus on its rock: the robed, winged figure sunk to its waist and leaning, its star gone, the
    great sword planted beside it with a lit edge, a broken hand lying before it."""
    parts = P()
    h = Geo()
    parts.guardian(h, blk(-.7, .7, 0, 5.4, -.7, .7), T, True)
    # (the guardian in a block at x < 0 would face -x: this one is centred, so it faces +x; turn it to the bridge)
    q = ck.quat_mul(sk.quat_axis(0, 1, 0, math.radians(-70)), sk.quat_axis(0, 0, 1, math.radians(-8)))
    g.extend(h.transformed(sk.affine(q, (x, y - 3.0, z), (4.2, 4.2, 4.2))))
    # the sword
    s = Geo()
    prof = [(-.5, 0), (0, -.14), (.5, 0), (0, .14)]
    sweep(s, prof, [(0, 0, 0), (0, 15, 0)], F['silver'], up=(1, 0, 0), smooth_u=False, scale=[1.0, .9])
    for sx in (-1, 1):
        s.face(((sx * .5, 0, 0), (sx * .52, 0, .02), (sx * .47, 15, .02), (sx * .45, 15, 0)), F['cold'], normal=(sx, 0, .2))
        s.face(((sx * .5, 0, 0), (sx * .52, 0, -.02), (sx * .47, 15, -.02), (sx * .45, 15, 0)), F['cold'], normal=(sx, 0, -.2))
    sweep(s, sk.rect_section(.5, .5, .08), [(-2.2, 15.2, 0), (2.2, 15.2, 0)], F['metal'], up=Y)
    lathe(s, [(.22, 15.4), (.2, 18.2), (.4, 18.4), (.3, 18.9), (0, 19.0)], (0, 0, 0), F['metal'], 6)
    g.extend(s.transformed(sk.affine(ck.quat_mul(sk.quat_axis(0, 1, 0, math.radians(20)), sk.quat_axis(0, 0, 1, math.radians(6))), (x - 8, y - 1.5, z - 3))))
    ck.rock(g, (x + 9, y + 1.4, z + 6), (2.2, 1.5, 2.6), 77, F['rubble'], n=2, square=.4, rough=.4, quat=sk.quat_axis(.3, 1, .2, .8))
    ck.crystal(g, (x + 9, y + 3.2, z + 6), .3, .9, F['cold'], sides=5, low=.6)


def obelisk(g, x, y, z, hgt, T, fin, yaw=0.0):
    b = [(x + math.cos(yaw + k * math.pi / 2 + math.pi / 4) * 2.8, y, z + math.sin(yaw + k * math.pi / 2 + math.pi / 4) * 2.8) for k in range(4)]
    t = [(x + math.cos(yaw + k * math.pi / 2 + math.pi / 4) * 1.6, y + hgt, z + math.sin(yaw + k * math.pi / 2 + math.pi / 4) * 1.6) for k in range(4)]
    ck.prism(g, b, hgt, F['stone'], None, cap=False, top_poly=t)
    for k in range(4):
        facet(g, (t[k], t[(k + 1) % 4], (x, y + hgt + 3.2, z)), F['carved'], sk._sub(sk._lerp(t[k], t[(k + 1) % 4], .5), (x, y + hgt, z)))
    ck.crystal(g, (x, y + hgt + 5.2, z), .8, 1.8, fin, sides=4, low=1.0)


def far(L, T):
    """[(chunk name, Geo)]: each one renderer with a single tier."""
    TL = sk.Tier(2)
    out = []
    spans = [(-66, -6, 3, 22, 4, -5, 201), (-104, -14, -12, 18, -11, 9, 202), (-150, -4, 20, 24, 16, -14, 203), (-210, -30, -30, 20, -24, 20, 204),
             (66, -9, -4, 20, -6, 6, 205), (108, 2, 10, 18, 9, -11, 206), (158, -22, -24, 22, -18, 15, 207), (224, -8, 36, 18, 28, -22, 208)]
    for name, group in (('lost-w', spans[:4]), ('lost-e', spans[4:])):
        g = Geo()
        for i, (x, y, z, length, yaw, roll, seed) in enumerate(group):
            g.extend(lost_span(x, y, z, length, yaw, roll, seed, TL, far=i >= 2))
        out.append((name, g))
    g = Geo()
    island(g, 40, -26 + 14.2, 150, 15, 301, T, top=F['rockpale'])
    shrine(g, 40, -12, 150, T)
    island(g, -150, 10 + .7, 90, 7, 303, T)
    column(g, -150, 10.7, 90, 9, .8, T)
    ck.crystal(g, (-150, 21.2, 90), .4, 1.0, F['cold'], sides=4, low=1.0)
    island(g, 170, -40 + .7, 110, 8, 304, T)
    column(g, 170, -39.3, 110, 12, .8, T)
    ck.crystal(g, (170, -25.8, 110), .4, 1.0, F['cold'], sides=4, low=1.0)
    out.append(('north', g))
    g = Geo()
    island(g, -70, -34 + 14.2, -140, 14, 302, T)
    colossus(g, -70, -20, -140, T)
    island(g, 120, 26 + .7, -120, 6, 305, T)
    out.append(('south', g))
    # the asteroid belt and the far islands, by quadrant
    quads = [Geo(), Geo(), Geo(), Geo()]
    for i in range(170):
        a, r = H(401, i) * 2 * math.pi, 340 + H(401, i, 1) ** .8 * 520
        tilt = math.sin(a * 2 + .6) * .12
        y = -60 + math.sin(a) * r * tilt + (H(401, i, 2) - .5) * 70
        sz = (2.5 + H(401, i, 3) ** 3 * 30) * .62
        x, z = math.cos(a) * r, math.sin(a) * r
        q = sk.quat_axis(H(401, i, 7) - .5, H(401, i, 8) - .5, H(401, i, 9) - .5, H(401, i, 10) * 6)
        gq = quads[(0 if x < 0 else 1) + (0 if z < 0 else 2)]
        ck.rock(gq, (x, y, z), (sz, sz * (.55 + H(401, i, 5) * .4), sz * (.6 + H(401, i, 6) * .5)), 600 + i, F['rock'], n=2, square=.15, rough=.55, flat=sz > 10, quat=q, freq=1.6)
        if H(401, i, 13) < .05:
            ck.crystal(gq, (x, y + sz * .7, z), sz * .1, sz * .4, F['cold'], sides=4, low=.6)
    for i in range(12):
        a = i * 2 * math.pi / 12 + H(411, i) * .4
        r = 620 + H(411, i, 1) * 240
        x, z, y = math.cos(a) * r, math.sin(a) * r, -40 + (H(411, i, 2) - .5) * 160
        gq = quads[(0 if x < 0 else 1) + (0 if z < 0 else 2)]
        island(gq, x, y, z, 18 + H(411, i, 3) * 16, 420 + i, TL)
        if i % 2 == 0:
            obelisk(gq, x, y, z, 34, T, F['violet' if i % 4 == 0 else 'cold'], H(411, i, 4) * 1.5)
    # four far marks square to the map keep the art's bounds centred on it (Runtime NativeStageArtDraw puts its reflection probe
    # at the bounds' centre: on the Breach, clear of every block)
    for k, (sx, sz) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        ck.rock(quads[k], (sx * 940.0, -40.0, sz * 940.0), (2, 2, 2), 9, F['rock'], n=1, flat=False)
        ck.rock(quads[k], (sx * 300.0, 190.0 if k == 0 else -250.0, sz * 300.0), (1, 1, 1), 9, F['rock'], n=1, flat=False)
    for k in range(4):
        out.append(('belt-%d' % k, quads[k]))
    return out


def bank_far(sb, L, no_ao):
    T = sk.Tier(1)
    items = far(L, T)
    # the ruins far out in the void draw 60-150 m off (the depth buffer's step about 2 cm there): their coplanar faces parted wider
    for name, g in items:
        print('far %-8s coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % ((name,) + sk.lift_coplanar(g, eps=.02, lift=.05)))
    geos = [g for _, g in items]
    aos = [None] * len(geos)
    if not no_ao:
        aos = sk.bake_ao(geos, distance=3.0, samples=12)
    for (name, g), ao in zip(items, aos):
        bk = sk.Bake(g, ao, floors=(), seed=5)
        print('far %-8s %6d corners' % (name, len(g.p) // 3))
        sb.add('static/%s/2' % name, 'static', sk.to_mesh('static/%s/2' % name, g, bk))
