"""Lightfall's set (the bow's intro, docs/INTROS-RANGED.md): an old shrine in a forest clearing under a full moon. The pilot stands at the
origin on a mossy octagonal stone terrace facing +z, toward a stone gate, the full moon behind over the trees; six stone lanterns stand round
the terrace on the forest floor, ancient trees ring the clearing, ferns and roots and mossy boulders between them, stars over it all.
The arrow of light, the volley and the lanterns' fires are the intro's.

Props (Unity space; the intro turns them by Facing):
  lightfall/grove      the terrace, the gate, the lanterns' stone, the trees, the undergrowth (static)
  lightfall/sky        the night dome with its stars (glow: unlit, painted)
  lightfall/fire       one lantern's fire box glow (glow; the intro lights each); origin at the box's centre
  lightfall/arrow      a planted arrow of light, head down along -y (glow); origin at its nock
  lightfall/leaf       a fallen leaf (origin at its centre), lightfall/petal a petal of light (glow)
"""
import math

import numpy as np

import ranged_kit as rk
from ranged_kit import F, sk, Geo, box, cylinder, lathe, sweep, beam, quat_axis, affine

GROUND = .012
TERRACE_R = 3.2
FLOOR_Y = -.45              # the forest floor round the terrace
LANTERN_R = 4.5
LANTERN_DEG = (35.0, 90.0, 145.0, 215.0, 270.0, 325.0)   # bearings from the pilot's front (+z), + toward +x
LANTERN_FIRE_Y = FLOOR_Y + 1.42                          # the fire box's centre above the floor
GATE_Z = 6.6
MOON = (-.296, .5, -.814)   # behind the pilot, up to the left (the hero frame looks back at it)


def lantern_at(i):
    a = math.radians(LANTERN_DEG[i])
    return (math.sin(a) * LANTERN_R, FLOOR_Y, math.cos(a) * LANTERN_R)


def finishes():
    A = sk
    rk.fin('flag', 0x7A7A72, A.DETAIL_FLAG, smooth=.25, param=.8, dirt=.8, moss=.7, wear=.3)
    rk.fin('stoneg', 0x6E6E68, A.DETAIL_CARVED, smooth=.2, dirt=.8, moss=.8, wear=.35)
    rk.fin('stonedark', 0x4E4E4A, A.DETAIL_CARVED, smooth=.2, dirt=.8, moss=.9, wear=.3)
    rk.fin('earth', 0x2E2A22, A.DETAIL_PLASTER, smooth=.1, dirt=.4, moss=.6)
    rk.fin('moss', 0x3E5A2E, A.DETAIL_CLOTH, smooth=.1, dirt=.3)
    rk.fin('bark', 0x3A2E26, A.DETAIL_WOOD, smooth=.12, param=1, dirt=.7, moss=.6, wear=.2)
    rk.fin('barkdark', 0x2A221C, A.DETAIL_WOOD, smooth=.12, param=1, dirt=.7, moss=.5)
    rk.fin('canopy', 0x1E3A26, A.DETAIL_NONE, smooth=.15, dirt=.2)
    rk.fin('canopy2', 0x264A2E, A.DETAIL_NONE, smooth=.15, dirt=.2)
    rk.fin('fern', 0x2E5A30, A.DETAIL_NONE, smooth=.2, dirt=.2)
    rk.fin('grass', 0x3A5A2E, A.DETAIL_NONE, smooth=.15, dirt=.2)
    rk.fin('leaf', 0x8A6A2E, A.DETAIL_NONE, smooth=.2, dirt=.1)
    rk.fin('sky3', 0xFFFFFF, A.DETAIL_GLOW, glow=1.0)
    rk.fin('star', 0xDDE8FF, A.DETAIL_GLOW, glow=2.6)
    rk.fin('moonglow', 0xE8F0FF, A.DETAIL_GLOW, glow=3.0)
    rk.fin('fire', 0xFFC27A, A.DETAIL_GLOW, glow=3.0)
    rk.fin('lightgold', 0xFFE6A0, A.DETAIL_GLOW, glow=4.0)
    rk.fin('glyph', 0x9FE8FF, A.DETAIL_GLOW, glow=1.2)


def terrace(g, T):
    """The octagonal terrace: a flagged top, two steps down to the forest floor all round, worn and mossy."""
    n = 8
    rings = [(TERRACE_R, GROUND), (TERRACE_R + .5, GROUND - .225), (TERRACE_R + 1.0, FLOOR_Y)]
    ph = math.pi / 8

    def ring(r, y):
        return [(math.sin(ph + 2 * math.pi * k / n) * r, y, math.cos(ph + 2 * math.pi * k / n) * r) for k in range(n)]
    top = ring(TERRACE_R, GROUND)
    # the top, cut into a fan of flags (its occlusion and moss vary across it)
    cells = 3 if T.fine else 1
    for k in range(n):
        a, b = top[k], top[(k + 1) % n]
        # triangles from the centre in bands
        for i in range(cells):
            r0, r1 = i / cells, (i + 1) / cells
            p0, p1 = sk._lerp((0, GROUND, 0), a, r0), sk._lerp((0, GROUND, 0), b, r0)
            q0, q1 = sk._lerp((0, GROUND, 0), a, r1), sk._lerp((0, GROUND, 0), b, r1)
            if i == 0:
                g.face((p0, q1, q0), F['flag'], (0, 1, 0))
            else:
                g.face((p0, p1, q1, q0), F['flag'], (0, 1, 0))
    # the steps: a riser and a tread per step, then the skirt into the floor
    prev = top
    for (r, y) in rings[1:]:
        down = ring(r - .5, y)
        out_ = ring(r, y)
        for k in range(n):
            k1 = (k + 1) % n
            mid = sk._lerp(prev[k], prev[k1], .5)
            nrm = sk._norm((mid[0], 0, mid[2]))
            g.face((prev[k], prev[k1], down[k1], down[k]), F['stoneg'], nrm)
            g.face((down[k], down[k1], out_[k1], out_[k]), F['flag'], (0, 1, 0))
        prev = out_
    for k in range(n):
        k1 = (k + 1) % n
        mid = sk._lerp(prev[k], prev[k1], .5)
        g.face((prev[k], prev[k1], (prev[k1][0], FLOOR_Y - .3, prev[k1][2]), (prev[k][0], FLOOR_Y - .3, prev[k][2])), F['stonedark'], sk._norm((mid[0], 0, mid[2])))


def stone_lantern(g, at, T):
    """A tall stone lantern: footing, shaft, platform, the fire box (its openings are the fire prop), roof and finial."""
    x, y, z = at
    n = 8 if T.fine else 6
    box(g, x - .42, x + .42, y - .2, y + .18, z - .42, z + .42, F['stonedark'], T, .04, cell=0)
    lathe(g, [(.3, y + .18), (.26, y + .3), (.17, y + .4), (.15, y + .95), (.2, y + 1.05), (.32, y + 1.1)], (x, 0, z), F['stoneg'], n, cap1=False)
    box(g, x - .36, x + .36, y + 1.1, y + 1.2, z - .36, z + .36, F['stoneg'], T, .03, cell=0)
    # the fire box: four corner posts (the openings between them glow: the fire prop)
    for sx in (-1, 1):
        for sz in (-1, 1):
            box(g, x + sx * .24 - .05, x + sx * .24 + .05, y + 1.2, y + 1.66, z + sz * .24 - .05, z + sz * .24 + .05, F['stoneg'], T, .01, cell=0)
    box(g, x - .2, x + .2, y + 1.2, y + 1.24, z - .2, z + .2, F['stonedark'], T, 0, cell=0)
    # the roof: a flared hip with upturned corners, the finial on top
    e, top = .52, y + 1.66
    corners = [(x - e, top + .04, z - e), (x + e, top + .04, z - e), (x + e, top + .04, z + e), (x - e, top + .04, z + e)]
    apex = (x, top + .42, z)
    for i in range(4):
        a, b = corners[i], corners[(i + 1) % 4]
        cen = ((a[0] + b[0]) / 2 - x, .5, (a[2] + b[2]) / 2 - z)
        g.face((a, b, apex), F['stonedark'], sk._norm(cen))
    g.face([(c[0], c[1] - .08, c[2]) for c in corners], F['stonedark'], (0, -1, 0))
    for i in range(4):
        a, b = corners[i], corners[(i + 1) % 4]
        mid = sk._lerp(a, b, .5)
        g.face((a, b, (b[0], b[1] - .08, b[2]), (a[0], a[1] - .08, a[2])), F['stonedark'], sk._norm((mid[0] - x, 0, mid[2] - z)))
    lathe(g, [(.09, top + .4), (.12, top + .5), (.07, top + .62), (.01, top + .68)], (x, 0, z), F['stoneg'], 6)


def gate(g, T):
    """The stone gate in front of the terrace: two pillars, a tie beam, a lintel with upturned ends; glyphs cut down the pillars."""
    h = 3.6
    n = 10 if T.fine else 6
    for sx in (-1, 1):
        x = sx * 1.9
        lathe(g, [(.3, FLOOR_Y - .2), (.28, FLOOR_Y + .3), (.24, h - .2), (.25, h)], (x, 0, GATE_Z), F['stoneg'], n, cap1=True)
        box(g, x - .42, x + .42, FLOOR_Y - .2, FLOOR_Y + .25, GATE_Z - .42, GATE_Z + .42, F['stonedark'], T, .04, cell=0)
        # the glyphs: short glowing cuts down the pillar's face toward the terrace (the intro wakes them)
        if T.detail:
            for k in range(6):
                y = .6 + k * .42
                w = .05 + .06 * ((k * 7) % 3) / 2
                g.face(((x - w, y, GATE_Z - .245), (x + w, y, GATE_Z - .245), (x + w, y + .22, GATE_Z - .245), (x - w, y + .22, GATE_Z - .245)), F['glyph'], (0, 0, -1))
    box(g, -2.2, 2.2, h - .9, h - .62, GATE_Z - .14, GATE_Z + .14, F['stoneg'], T, .03, cell=0)
    # the lintel: a long stone whose ends sweep up
    pts = []
    for k in range(9):
        u = k / 8
        x = -3.0 + 6.0 * u
        y = h + .1 + .32 * (abs(u - .5) * 2) ** 3
        pts.append((x, y, GATE_Z))
    sweep(g, sk.rect_section(.3, .26, .03), pts, F['stonedark'], caps=True, smooth_u=False, up=(0, 0, 1))


def tree(g, at, h, r, seed, T):
    """An old broad tree: a flared trunk with buttress roots, a few great limbs, and clumps of canopy."""
    rng = np.random.default_rng(seed)
    x, y, z = at
    n = 10 if T.fine else 6
    prof = [(r * 1.9, y - .3), (r * 1.35, y + .4), (r * 1.05, y + 1.4), (r, y + h * .45), (r * .82, y + h * .7), (r * .55, y + h * .85)]
    lathe(g, prof, (x, 0, z), F['bark'], n, phase=rng.uniform(0, 1))
    # buttress roots
    for k in range(5 if T.fine else 3):
        a = 2 * math.pi * k / 5 + rng.uniform(-.3, .3)
        d = (math.cos(a), 0, math.sin(a))
        pts = [(x + d[0] * r * .6, y + 1.0, z + d[2] * r * .6), (x + d[0] * r * 1.6, y + .25, z + d[2] * r * 1.6), (x + d[0] * r * 2.6, y - .05, z + d[2] * r * 2.6)]
        sweep(g, sk.circle_section(r * .32, 5), pts, F['barkdark'], caps=True, scale=[1.0, .7, .35])
    # limbs and their canopy clumps
    top = (x, y + h * .85, z)
    for k in range(4 if T.fine else 3):
        a = rng.uniform(0, 2 * math.pi)
        d = (math.cos(a), 0, math.sin(a))
        L = h * rng.uniform(.28, .42)
        y0 = y + h * rng.uniform(.5, .8)
        p0 = (x + d[0] * r * .4, y0, z + d[2] * r * .4)
        p1 = (x + d[0] * L * .55, y0 + L * .45, z + d[2] * L * .55)
        p2 = (x + d[0] * L, y0 + L * .62, z + d[2] * L)
        sweep(g, sk.circle_section(r * .38, 6 if T.fine else 4), [p0, p1, p2], F['bark'], caps=True, scale=[1.0, .7, .4])
        for j in range(2 if T.fine else 1):
            c = sk._add(p2, (rng.uniform(-1, 1), rng.uniform(.3, 1.4), rng.uniform(-1, 1)))
            rk.rock(g, c, rng.uniform(2.2, 3.4), F['canopy' if j % 2 == 0 else 'canopy2'], int(rng.integers(1, 999)), squash=(1.2, .62, 1.2), detail=1)
    rk.rock(g, sk._add(top, (0, 2.2, 0)), rng.uniform(3.2, 4.2), F['canopy2'], int(rng.integers(1, 999)), squash=(1.3, .6, 1.3), detail=1)


def fern(g, at, s, seed, T):
    rng = np.random.default_rng(seed)
    x, y, z = at
    n = 7 if T.fine else 4
    for k in range(n):
        a = 2 * math.pi * k / n + rng.uniform(-.2, .2)
        d = (math.cos(a), 0, math.sin(a))
        L = s * rng.uniform(.7, 1.0)
        tip = (x + d[0] * L, y + L * .35, z + d[2] * L)
        mid = (x + d[0] * L * .5, y + L * .5, z + d[2] * L * .5)
        side = (-d[2] * .12 * s, 0, d[0] * .12 * s)
        for sgn in (1, -1):
            pts = ((x, y, z), sk._add(mid, side, sgn), tip)
            nrm = (0, 1, 0)
            g.face(pts, F['fern'], nrm)
            g.face(pts[::-1], F['fern'], (0, -1, 0))


def grove(T):
    rng = np.random.default_rng(23)
    g = Geo()
    terrace(g, T)
    # the forest floor: a rolling sheet round the terrace
    def floor_h(x, z):
        r = math.hypot(x, z)
        return (.35 * math.sin(x * .21 + 1) * math.cos(z * .17) + .2 * math.sin(x * .7) * math.sin(z * .5)) * min(1, max(0, (r - 5.0) / 6))
    rk.ground(g, -36.0, 36.0, -36.0, 40.0, FLOOR_Y, F['earth'], 2.0 if T.fine else 6.0, floor_h)
    for i in range(len(LANTERN_DEG)):
        stone_lantern(g, lantern_at(i), T)
    gate(g, T)
    # the ring of old trees round the clearing (gaps at the gate's sight line to the moon and behind for the light)
    for k, (a, d, h, r) in enumerate(((-150, 12.5, 16, .9), (-115, 10.5, 18, 1.1), (-75, 12.0, 15, .85), (-40, 13.5, 17, 1.0), (45, 13.0, 16, 1.0), (80, 11.0, 18, 1.15),
                                      (120, 12.5, 15, .9), (160, 11.5, 17, 1.0), (-180, 14.0, 19, 1.2), (20, 22.0, 20, 1.2), (-20, 24.0, 19, 1.1), (100, 20.0, 18, 1.0), (-95, 21.0, 18, 1.0))):
        if not T.fine and k >= 10:
            break
        ar = math.radians(a)
        tree(g, (math.sin(ar) * d, FLOOR_Y + floor_h(math.sin(ar) * d, math.cos(ar) * d), math.cos(ar) * d), h, r, 200 + k, T)
    # undergrowth: ferns and mossy boulders round the terrace's foot and between the trees
    for k in range(26 if T.fine else 10):
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(5.2, 11.0)
        x, z = math.sin(a) * d, math.cos(a) * d
        if abs(x) < 2.6 and z > 4:
            continue
        fern(g, (x, FLOOR_Y + floor_h(x, z), z), rng.uniform(.5, .9), 300 + k, T)
    for k in range(9 if T.fine else 4):
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(5.0, 10.0)
        x, z = math.sin(a) * d, math.cos(a) * d
        if abs(x) < 2.8 and z > 4:
            continue
        rk.rock(g, (x, FLOOR_Y + floor_h(x, z), z), rng.uniform(.4, 1.0), F['stonedark'], 400 + k, squash=(1.2, .6, 1.0), detail=1)
    return g


def sky(T):
    g = Geo()
    rk.dome(g, 180.0, F['sky3'], 12 if T.fine else 7, 36 if T.fine else 18, below=-.1)
    # the stars: small glowing quads facing in
    rng = np.random.default_rng(3)
    for k in range(220 if T.fine else 90):
        a = rng.uniform(0, 2 * math.pi)
        e = math.asin(rng.uniform(.12, .98))
        d = (math.cos(e) * math.sin(a), math.sin(e), math.cos(e) * math.cos(a))
        if (d[0] - MOON[0]) ** 2 + (d[1] - MOON[1]) ** 2 + (d[2] - MOON[2]) ** 2 < .05:
            continue
        c = (d[0] * 176, d[1] * 176, d[2] * 176)
        u, v, w = sk.frame((0, 0, 0), d)
        s = rng.uniform(.18, .52)
        pts = [sk._add(sk._add(c, u, -s), v, -s), sk._add(sk._add(c, u, s), v, -s), sk._add(sk._add(c, u, s), v, s), sk._add(sk._add(c, u, -s), v, s)]
        g.face(pts, F['star'], (-d[0], -d[1], -d[2]))
    # the moon: a disc with a soft halo ring of quads
    c = (MOON[0] * 170, MOON[1] * 170, MOON[2] * 170)
    u, v, w = sk.frame((0, 0, 0), MOON)
    R = 6.6
    pts = [sk._add(sk._add(c, u, math.cos(2 * math.pi * k / 24) * R), v, math.sin(2 * math.pi * k / 24) * R) for k in range(24)]
    g.face(pts, F['moonglow'], (-MOON[0], -MOON[1], -MOON[2]))
    return g


def paint_sky(g, bk):
    def fn(P):
        d = P / np.linalg.norm(P, axis=1, keepdims=True)
        e = d[:, 1]
        n = len(P)
        m = d @ np.array(MOON)
        hor, mid, zen = rk.hexrgb(0x2A3A5E), rk.hexrgb(0x142040), rk.hexrgb(0x05081A)
        c = rk.lerp3(np.tile(hor, (n, 1)), np.tile(mid, (n, 1)), np.clip(e / .3, 0, 1))
        c = rk.lerp3(c, np.tile(zen, (n, 1)), np.clip((e - .25) / .6, 0, 1))
        halo = np.exp(-(1 - np.clip(m, -1, 1)) * 14)
        c = c + halo[:, None] * (rk.hexrgb(0x8AA8D8) - c) * .7
        return c
    rk.paint(g, bk, fn, F['sky3'])


def fire(T):
    """The fire box's glow: a lit block between the lantern's posts."""
    g = Geo()
    box(g, -.19, .19, -.2, .2, -.19, .19, F['fire'], T, 0, cell=0)
    return g


def arrow(T):
    """An arrow of light standing in the ground: a thin shaft of light with fletching vanes, head down along -y."""
    g = Geo()
    L = .92
    n = 5 if T.fine else 4
    cylinder(g, (0, 0, 0), (0, -L, 0), .012, F['lightgold'], n)
    lathe(g, [(.0, -L - .1), (.03, -L), (.015, -L + .02)], (0, 0, 0), F['lightgold'], n)
    for k in range(3):
        a = 2 * math.pi * k / 3
        d = (math.cos(a), 0, math.sin(a))
        g.face(((0, -.02, 0), (d[0] * .05, -.04, d[2] * .05), (d[0] * .05, -.2, d[2] * .05), (0, -.24, 0)), F['lightgold'], sk._norm((d[2], 0, -d[0])))
        g.face(((0, -.24, 0), (d[0] * .05, -.2, d[2] * .05), (d[0] * .05, -.04, d[2] * .05), (0, -.02, 0)), F['lightgold'], sk._norm((-d[2], 0, d[0])))
    return g


def leaf(T):
    g = Geo()
    pts = [(0, 0, -.05), (.025, .004, -.01), (.018, .006, .03), (0, .004, .05), (-.018, .006, .03), (-.025, .004, -.01)]
    g.face(pts, F['leaf'], (0, 1, 0))
    g.face(pts[::-1], F['leaf'], (0, -1, 0))
    return g


def petal(T):
    g = Geo()
    pts = [(0, 0, -.04), (.02, 0, 0), (0, 0, .04), (-.02, 0, 0)]
    g.face(pts, F['lightgold'], (0, 1, 0))
    g.face(pts[::-1], F['lightgold'], (0, -1, 0))
    return g


def build(out, T):
    t = out
    t.add('lightfall/grove', grove(T), T.i, floors=(GROUND, FLOOR_Y), ao_distance=1.2, ao_samples=16 if T.i == 0 else 8, show=True)
    t.add('lightfall/sky', sky(T), T.i, ao=False, paint_fn=paint_sky, show=True)
    t.add('lightfall/fire', fire(T), T.i, ao=False, show=rk.yaw(0, sk._add(lantern_at(0), (0, 1.42, 0))))
    t.add('lightfall/arrow', arrow(T), T.i, ao=False, show=rk.yaw(0, (1.4, .9, 2.0)))
    t.add('lightfall/leaf', leaf(T), T.i, ao=False)
    t.add('lightfall/petal', petal(T), T.i, ao=False)


def views():
    return [('lf-front', (0, 1.6, -3.0), (0, 3.0, 12.0), 55), ('lf-hero', (1.6, .6, 3.4), (0, 1.05, 0), 36), ('lf-high', (6.0, 9.0, -9.0), (0, 0, 2.0), 60),
            ('lf-sky', (0, 1.4, 0), (0, 300.0, 520.0), 70), ('lf-lantern', (3.0, .8, 1.4), lantern_at(0), 45)]
