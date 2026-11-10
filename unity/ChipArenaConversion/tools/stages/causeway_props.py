"""Starfall Causeway's moving art (causeway_art.py runs it): the rig as props in their own frames, posed each frame by Core
NativeCausewayArt from Core NativeCausewayRig's state.

The rig frames (Unity space, metres; Core NativeCausewayArt holds the same):
  star             a falling star's burning stone, origin at its centre (about .65 m in radius): scorched rock, molten within
  shard            a thrown shard of unit half size, origin at its centre (the pose scales it); shard-ember: one still burning
  glyph            a lane glyph tile lit, origin at its centre on the deck (x +-.16, z +-.07), white: the pose tints it
  banner           one third of a pylon's banner, origin at its centre, hanging down -y (.82 m), across x (+-.42), both faces
  banner-hem       the banner's gold hem bar, origin at its centre, across x
  banner-star      the banner's rune lozenge, origin at its centre, in the cloth's plane (x, y)
  crystal          a floating crystal of unit half height along y (the pose scales it): cold blue; crystal-violet: violet
  voussoir         a stone of the Shattered Arch, origin at its centre: x along the arch, y out of it, z across the bridge
                   (half 0.62 x 0.5 x 1.1), a rune band round it; voussoir-key: the keystone, a crystal band
  crown-K          the K-th drifting fragment of the towers' lost storeys (K 0..6), origin at its centre, at its own size
  asteroid-K       a tumbling rock (K 0..3) of unit size (the pose scales it)
  comet            the comet's nucleus of unit size (the pose scales it): dark ice with bright facets
"""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, lathe, sweep
import causeway_kit as ck
from causeway_kit import F, Y, Face

CROWN_STONES = 7


def rigfin():
    A = sk
    # moving stone takes no world-space fine surface (it would swim across it)
    F['rigstone'] = A.fin('cw-rigstone', 0xCFC4AE, A.DETAIL_NONE, smooth=.2, wear=.25)
    F['rigcarved'] = A.fin('cw-rigcarved', 0xDCD3C2, A.DETAIL_NONE, smooth=.24, wear=.3)
    F['rigdeep'] = A.fin('cw-rigdeep', 0x6A6470, A.DETAIL_NONE, smooth=.1, dirt=.5)


def star(T):
    g = Geo()
    n = (3, 2, 1)[T.i]
    ck.rock(g, (0, 0, 0), (.62, .55, .58), 11, F['ember'], n=n, square=.1, rough=.2)
    ck.rock(g, (0, 0, 0), (.66, .58, .62), 23, F['scorch'], n=max(n, 2), square=.2, rough=.75, freq=2.6)
    return g


def shard(T, ember):
    g = Geo()
    ck.rock(g, (0, 0, 0), (1.0, .8, 1.0), 31 if ember else 37, F['ember' if ember else 'scorch'], n=1, square=.4, rough=.4)
    return g


def glyph(T):
    g = Geo()
    g.face(((-.16, .012, -.07), (.16, .012, -.07), (.16, .012, .07), (-.16, .012, .07)), F['white'], Y)
    return g


def banner(T):
    g = Geo()
    w, h = .42, .41
    cols, rows = (4, 2, 1)[T.i], (3, 2, 1)[T.i]

    def Z(x):
        return .012 * math.sin(x * 9.0)
    for i in range(cols):
        for j in range(rows):
            x0, x1 = -w + 2 * w * i / cols, -w + 2 * w * (i + 1) / cols
            y0, y1 = -h + 2 * h * j / rows, -h + 2 * h * (j + 1) / rows
            f = F['clothedge'] if ((i == 0 or i == cols - 1) and cols > 2) else F['cloth']
            for s in (1, -1):
                g.face(((x0, y0, Z(x0) + s * .004), (x1, y0, Z(x1) + s * .004), (x1, y1, Z(x1) + s * .004), (x0, y1, Z(x0) + s * .004)), f, normal=(0, 0, s))
    return g


def banner_hem(T):
    g = Geo()
    sweep(g, sk.circle_section(.03, 6 if T.i == 0 else 4), [(-.45, 0, 0), (.45, 0, 0)], F['clothedge'])
    if T.i < 2:
        for x in (-.45, .45):
            ck.crystal(g, (x, 0, 0), .045, .06, F['gold'], sides=4, low=1.0)
    return g


def banner_star(T):
    g = Geo()
    for s in (1, -1):
        ck.star(g, lambda x, v, s=s: (x, v, s * .02), (0, 0, s), .15, .05, 4, F['rune'])
    return g


def crystal(T, fin):
    g = Geo()
    ck.crystal(g, (0, 0, 0), 1.2, 1.3, F[fin], sides=6 if T.i < 2 else 4, low=1.0)
    return g


def voussoir(T, key):
    """A wedge of carved stone (wider on the arch's outside, +y), its edges chamfered, a lit band round its middle and a
    sunk star on each face across the bridge."""
    g = Geo()
    hx, hy, hz = .62, .5, 1.1
    lo, hi = .9, 1.06
    c = .07 if T.i < 2 else 0.0
    f = F['rigstone']
    # the six faces drawn in by the chamfer, then the chamfers between them
    def X(y):
        return hx * (lo + (hi - lo) * (y + hy) / (2 * hy))
    corners = {}
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                corners[(sx, sy, sz)] = (sx * X(sy * hy), sy * hy, sz * hz)

    def pull(p, ax):
        q = list(p)
        for a in range(3):
            if a != ax:
                q[a] -= math.copysign(c, q[a])
        return tuple(q)
    for ax in range(3):
        for s in (-1, 1):
            ks = [k for k in corners if k[ax] == s]
            o = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
            b, cc = (ax + 1) % 3, (ax + 2) % 3
            pts = [pull(corners[next(k for k in ks if k[b] == u and k[cc] == v)], ax) for u, v in o]
            n = [0.0, 0.0, 0.0]
            n[ax] = s
            g.face(tuple(pts), f, normal=tuple(n))
    if c > 0:
        # edges: between two faces' drawn-in edges
        for ax in range(3):
            b, cc = (ax + 1) % 3, (ax + 2) % 3
            for u in (-1, 1):
                for v in (-1, 1):
                    k0 = [0, 0, 0]
                    k0[b], k0[cc] = u, v
                    k0[ax] = -1
                    k1 = list(k0)
                    k1[ax] = 1
                    p0, p1 = corners[tuple(k0)], corners[tuple(k1)]
                    nb, nc = [0.0, 0.0, 0.0], [0.0, 0.0, 0.0]
                    nb[b], nc[cc] = u, v
                    g.face((pull(p0, b), pull(p1, b), pull(p1, cc), pull(p0, cc)), F['rigcarved'], normals=(tuple(nb), tuple(nb), tuple(nc), tuple(nc)))
        for k, p in corners.items():
            g.face((pull(p, 0), pull(p, 1), pull(p, 2)), F['rigcarved'], normals=((k[0], 0, 0), (0, k[1], 0), (0, 0, k[2])))
    band = F['crystal'] if key else F['rune']
    e = .012
    for sx in (-1, 1):
        g.face(((sx * (X(-.08) + e), -.08, -hz + c), (sx * (X(.08) + e), .08, -hz + c), (sx * (X(.08) + e), .08, hz - c), (sx * (X(-.08) + e), -.08, hz - c)), band, normal=(sx, 0, 0))
    for sz in (-1, 1):
        g.face(((-X(-.08) + c, -.08, sz * (hz + e)), (X(-.08) - c, -.08, sz * (hz + e)), (X(.08) - c, .08, sz * (hz + e)), (-X(.08) + c, .08, sz * (hz + e))), band, normal=(0, 0, sz))
        if T.i < 2:
            for yy in (-.27, .27):
                ck.star(g, lambda x, v, sz=sz, yy=yy: (x, v + yy, sz * (hz + e)), (0, 0, sz), .13, .045, 4, F['rigdeep'])
    if key:
        lathe(g, [(.3, hy), (.24, hy + .1), (.1, hy + .16)], (0, 0, 0), F['gold'], 6, smooth=False)
    return g


def crown_stone(T, k, s):
    """A fragment of the tower's lost storeys: a length of finished wall torn out of it (a moulded course under it, a string
    over it, a lit rune band, blind lancets under their archivolts on both faces), its ends and its top ragged with broken
    stone; one in three still carries the stub of a pier."""
    g = Geo()
    hx, hy, hz = s, s * .55, s * .8
    st, cv = F['rigstone'], F['rigcarved']
    wx = hx * .8
    box(g, -wx, wx, -hy, hy * .86, -hz, hz, st, T if T.i == 0 else None, bevel=.03 * s if T.i == 0 else 0, cell=0)
    box(g, -wx - .02 * s, wx + .02 * s, -hy - .16 * s, -hy, -hz - .05 * s, hz + .05 * s, cv, T if T.i == 0 else None, bevel=.03 * s if T.i == 0 else 0, cell=0)
    band = F['cold'] if k % 3 == 0 else F['rune']
    y = s * .05
    for sz in (-1, 1):
        fc = Face((-wx if sz > 0 else wx, 0, sz * hz), (0, 0, sz), 2 * wx)
        fc.mould(g, [(.07 * s, -.01), (.07 * s, .05 * s), (0, .08 * s), (-.06 * s, .03 * s), (-.07 * s, -.01)], hy * .7, 0, 2 * wx, cv, T)
        fc.quad(g, .04 * s, 2 * wx - .04 * s, y + .19 * s, y + .27 * s, .006, band)
        for ac in (wx * .55, wx * 1.45):
            fc.arch_fill(g, ac, s * .17, -hy * .82, -hy * .25, s * .22, F['rigdeep'], T, d=.006 if T.i < 2 else .02, n=3)
            if T.i < 2:
                fc.arch(g, ac, s * .17, -hy * .25, s * .22, [(.05 * s, 0), (.025 * s, .03 * s), (0, 0)], cv, T, legs=hy * .55, n=3)
    # the ragged ends and top
    n = 2 if T.i < 2 else 1
    for sx in (-1, 1):
        for j in range(2 if T.i < 2 else 1):
            ck.rock(g, (sx * (wx + s * .08), -hy * .35 + j * hy * .8, (j - .5) * hz * .7 * sx), (s * .26, hy * (.62 - .2 * j), hz * (.72 - .15 * j)), 90 + k * 7 + j + sx, F['broken'], n=n, square=.45, rough=.45)
    for j in range(3 if T.i < 2 else 2):
        ck.rock(g, (wx * (-.6 + .6 * j), hy * .9, hz * (.25 - .25 * j)), (wx * .36, hy * (.22 + .1 * ((j + k) % 2)), hz * .7), 130 + k * 5 + j, F['broken' if j % 2 else 'rigstone'], n=1, square=.4, rough=.45)
    if k % 3 == 1:
        box(g, hx * .1, hx * .5, hy * .8, s * 1.2, -s * .2, s * .2, st, None, bevel=0, cell=0)
        ck.rock(g, (hx * .3, s * 1.22, 0), (s * .24, s * .14, s * .24), 80 + k, F['broken'], n=1, square=.5, rough=.4)
    return g


def asteroid(T, v):
    g = Geo()
    n = 2 if T.i < 2 else 1
    ck.rock(g, (0, 0, 0), (1.0, .62, .78), 100 + v, F['rock'], n=n, square=.2, rough=.5)
    ck.rock(g, (0, 0, 0), (.72, .8, .58), 110 + v, F['rock'], n=n, square=.2, rough=.5, quat=sk.quat_axis(.3, 1, .2, .9))
    ck.rock(g, (.35, .2, -.15), (.5, .42, .55), 120 + v, F['rock'], n=1, square=.3, rough=.4, quat=sk.quat_axis(1, .4, 0, 1.3))
    return g


def comet(T):
    g = Geo()
    n = 2 if T.i < 2 else 1
    ck.rock(g, (0, 0, 0), (.9, .72, .8), 131, F['comet'], n=n, square=.1, rough=.2)
    ck.rock(g, (0, 0, 0), (1.0, .82, .9), 137, F['ice'], n=n, square=.15, rough=.55, freq=2.0)
    return g


def items(L, T):
    if 'rigstone' not in F:
        rigfin()
    sizes = (L.get('rig') or {}).get('crownStones') or [1.0 + .25 * ((k * 7 + 3) % 5) for k in range(CROWN_STONES)]
    out = [('star', star(T), (0, 30, 0)), ('shard', shard(T, False), (0, 2, 0)), ('shard-ember', shard(T, True), (0, 2, 0)), ('glyph', glyph(T), (0, 0, 0)),
           ('banner', banner(T), (0, 3, 0)), ('banner-hem', banner_hem(T), (0, 2, 0)), ('banner-star', banner_star(T), (0, 3, 0)),
           ('crystal', crystal(T, 'crystal'), (0, 6, 0)), ('crystal-violet', crystal(T, 'violet'), (0, 6, 0)),
           ('voussoir', voussoir(T, False), (0, 9, 0)), ('voussoir-key', voussoir(T, True), (0, 11, 0))]
    for k in range(CROWN_STONES):
        out.append(('crown-%d' % k, crown_stone(T, k, sizes[k]), (33, 12 + k, 0)))
    for v in range(4):
        out.append(('asteroid-%d' % v, asteroid(T, v), (90, 0, 0)))
    out.append(('comet', comet(T), (0, 20, 0)))
    return out


def bank_props(sb, L, T, no_ao):
    ti = T.i
    its = items(L, T)
    geos = [it[1] for it in its]
    aos = [None] * len(geos)
    if not no_ao:
        # each prop baked on its own (apart from the others)
        apart = [g.transformed([1, 0, 0, 40.0 * i, 0, 1, 0, 0, 0, 0, 1, 0]) for i, g in enumerate(geos)]
        aos = sk.bake_ao(apart, distance=.4, samples=(24, 12, 8)[ti])
    for (name, g, rest), ao in zip(its, aos):
        if g.faces == 0:
            continue
        Pw = g.arrays()[0].astype(np.float64) + np.array(rest)
        bk = sk.Bake(g, ao, world=Pw, floors=(), seed=7)
        sb.add('rig/%s/%d' % (name, ti), 'rig', sk.to_mesh('rig/%s/%d' % (name, ti), g, bk))
