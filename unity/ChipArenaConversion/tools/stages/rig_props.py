"""Deepwell's rig props and breakable blocks (rig_art.py banks them). Each rig prop is modelled in the frame of the box part the
Core rig tags for it (Core NativeDeepwellRig; NativeDeepwellArt.Pose places it at that part's centre and turn, sized as
NativeDeepwellArt.Sizing says):

  ram          the drill's ram about its middle (4.4 m square, 4 m tall: its shoe's face at y -2)            as modelled
  ram-rod      a guide rod of unit half height along y                                                     y by the part's half height
  klaxon       a beacon about its lens (its glow white: the pose's light colours it)                        as modelled
  core         the wellhead's core about the grate's middle at floor height (white glow)                    as modelled
  grate        a vent's louvred grate about its middle (2.2 x 3.6 m)                                        as modelled
  whale-body   the cow's body about its middle, nose toward +z (17 m whale)                                 by the whale's share of 17 m
  whale-tail   the tail stock and flukes about the stock's middle                                           the same
  whale-fin-l/r a flipper about its middle, along x (the root toward the body)                              the same
  jelly-bell   a bell of unit half extents (white glow)                                                    by the part's half extents
  jelly-arms   a sheaf of arms of unit half height and unit radius (white glow)                            radius, half height
  fish         a fish of unit half length, nose toward +z                                                  by its half length
  kelp         a kelp joint of unit half height along y, its blades in the xy plane                         y by the part's half height
  drone        the survey drone about its middle, nose toward +z                                           as modelled

The supply cases (the map's breakable `rig-case-*` blocks) share one mesh in their block's frame (block/<id>/<tier>)."""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, lathe, cylinder, tube
import rig_kit as rk
from rig_kit import F, rod, rbox, rivets, disc, dome, ball, skin, limb, hashf, Y

SHOW = {'ram': .45, 'whale-body': .16, 'whale-tail': .16, 'whale-fin-l': .3, 'whale-fin-r': .3, 'grate': .6, 'core': .6, 'klaxon': 3.0, 'jelly-bell': .9, 'jelly-arms': .9, 'kelp': .8,
        'fish': 1.2, 'ram-rod': .8}


def ram(T):
    g = Geo()
    sides = (24, 14, 8)[T.i]
    # the shoe (its face strikes the wellhead at y -2), the body, the collars, the cap
    lathe(g, [(.001, -2.0), (2.05, -2.0), (2.2, -1.9), (2.2, -1.62), (2.05, -1.55)], (0, 0, 0), F['dark'], sides)
    lathe(g, [(2.05, -1.55), (1.9, -1.45), (1.9, 1.5), (2.0, 1.6), (2.0, 1.86), (1.7, 2.0), (.001, 2.0)], (0, 0, 0), F['steel'], sides)
    for y in (-1.2, .1, 1.25):
        lathe(g, [(1.9, y - .1), (1.97, y - .07), (1.97, y + .07), (1.9, y + .1)], (0, 0, 0), F['brass'], sides)
    # warning bands round the shoe and lit slits up the body
    n = (24, 12, 8)[T.i]
    for k in range(n):
        a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        f = F['yellow'] if k % 2 == 0 else F['black']
        r = 2.205
        g.face(((math.sin(a0) * r, -1.88, math.cos(a0) * r), (math.sin(a1) * r, -1.88, math.cos(a1) * r), (math.sin(a1) * r, -1.64, math.cos(a1) * r), (math.sin(a0) * r, -1.64, math.cos(a0) * r)),
               f, (math.sin((a0 + a1) / 2), 0, math.cos((a0 + a1) / 2)))
    for k in range(8):
        a = k * math.pi / 4 + math.pi / 8
        c, s = math.sin(a), math.cos(a)
        r = 1.905
        t = (s * .07, 0, -c * .07)
        g.face(((c * r - t[0], -.75, s * r - t[2]), (c * r + t[0], -.75, s * r + t[2]), (c * r + t[0], -.25, s * r + t[2]), (c * r - t[0], -.25, s * r - t[2])), F['amber'], (c, 0, s))
    if T.tiny:
        rivets(g, [(math.sin(k * math.pi / 12) * 1.99, 1.73, math.cos(k * math.pi / 12) * 1.99) for k in range(24)], (0, 0, 1), .03, F['brass'])
    # the rods' sockets on the cap
    for sx in (-1, 1):
        lathe(g, [(.34, 1.98), (.34, 2.14), (.26, 2.2)], (sx * .8, 0, 0), F['brass'], (10, 7, 5)[T.i])
    return g


def ram_rod(T):
    g = Geo()
    cylinder(g, (0, -1, 0), (0, 1, 0), .2, F['frame'], (10, 7, 5)[T.i], caps=False)
    return g


def klaxon(T):
    g = Geo()
    sides = (10, 7, 5)[T.i]
    lathe(g, [(.13, -.16), (.13, -.1), (.1, -.08)], (0, 0, 0), F['brass'], sides, cap0=True)
    lathe(g, [(.1, -.08), (.11, .02), (.08, .12), (.001, .16)], (0, 0, 0), F['white'], sides)
    return g


def core(T):
    g = Geo()
    bars = 13
    for k in range(bars + 1):
        x0 = -1.5 + 3.0 * k / bars - (3.0 / bars / 2 - .035)
        x1 = x0 + (3.0 / bars - .07)
        x0, x1 = max(x0, -1.46), min(x1, 1.46)
        if x1 - x0 > .02:
            g.face(((x0, 0, -1.46), (x1, 0, -1.46), (x1, 0, 1.46), (x0, 0, 1.46)), F['white'], Y)
    return g


def grate(L, T):
    g = Geo()
    v = [h for h in L.get('hazards', []) if h['kind'] == 'vent']
    hx, hz = (v[0]['size'][0] / 2 - .5, v[0]['size'][2] / 2 - .3) if v else (1.1, 1.8)
    n = (9, 5, 3)[T.i]
    for k in range(n):
        z = -hz + 2 * hz * (k + .5) / n
        w = 2 * hz / n * .36
        box(g, -hx, hx, -.03, .03, z - w, z + w, F['dark'], T, .01, cell=0)
    for sx in (-1, 1):
        box(g, sx * hx - .05, sx * hx + .05, -.035, .035, -hz, hz, F['brass'], T, .01, cell=0)
    return g


def half_rings(stations, top, n):
    """Rings for a half skin (the upper or the lower half of a body): `stations` [(centre, half width, half height up, down)]."""
    out = []
    for c, w, up, dn in stations:
        ring = []
        for j in range(n + 1):
            a = math.pi * j / n
            ca, sa = math.cos(a), math.sin(a)
            ring.append((c[0] + w * ca, c[1] + (up if top else -dn) * sa, c[2]))
        out.append(ring)
    return out


def whale_body(T):
    """The cow's body about its middle (z -5.44 .. +6.0): a blunt head, a deep chest, tapering to the tail stock; a pale belly."""
    g = Geo()
    n = (10, 6, 4)[T.i]
    zs = [(-5.44, .62, .55, .6), (-4.2, .95, .8, .95), (-2.4, 1.38, 1.15, 1.4), (-.4, 1.68, 1.4, 1.7), (1.6, 1.76, 1.45, 1.78), (3.4, 1.62, 1.3, 1.6), (4.8, 1.3, 1.0, 1.25), (5.6, .85, .62, .8), (6.0, .3, .22, .3)]
    if T.i == 2:
        zs = zs[::2]
    st = [((0, 0, z), w, u, d) for z, w, u, d in zs]
    skin(g, half_rings(st, True, n), F['whale'], close0=False, close1=False)
    skin(g, half_rings(st, False, n), F['belly'], close0=False, close1=False)
    if T.i < 2:
        # the dorsal hump and fin, the eyes, the throat's grooves
        limb(g, [(0, 1.1, -2.0), (0, 1.55, -2.5), (0, 1.75, -3.1)], [.5, .3, .05], F['whale'], 6, flat=.25, side=(0, 0, 1))
        for sx in (-1, 1):
            ball(g, (sx * 1.2, -.15, 4.6), .09, F['black'], 6, 3)
        if T.tiny:
            for k in range(7):
                x = -1.2 + .4 * k
                tube(g, [(x, -1.0 - .5 * (1 - (x / 1.4) ** 2), 1.0), (x, -1.05 - .55 * (1 - (x / 1.4) ** 2), 2.6), (x * .8, -.8 - .4 * (1 - (x / 1.4) ** 2), 4.6)], .03, F['whale'], 3, caps=False)
    return g


def whale_tail(T):
    """The tail stock about its middle (z +2.89 at the body .. -2.89) and the flukes beyond it."""
    g = Geo()
    n = (8, 5, 4)[T.i]
    limb(g, [(0, 0, 2.89), (0, 0, 1.2), (0, 0, -1.0), (0, 0, -2.89), (0, 0, -3.3)], [.6, .52, .36, .22, .14], F['whale'], n, flat=1.25, side=(1, 0, 0))
    for sx in (-1, 1):
        limb(g, [(0, 0, -2.9), (sx * 1.1, 0, -3.5), (sx * 2.1, 0, -4.25), (sx * 2.7, 0, -4.9)], [.4, .5, .34, .05], F['whale'], n, flat=.16, side=(0, 0, 1))
    return g


def whale_fin(T, sd):
    """A flipper about its middle, along x: the root at the body's side (x -sd*1.7), the tip out at sd*1.7."""
    g = Geo()
    n = (8, 5, 4)[T.i]
    limb(g, [(-sd * 1.7, 0, .1), (-sd * .6, 0, .05), (sd * .6, 0, -.1), (sd * 1.4, 0, -.25), (sd * 1.7, 0, -.38)], [.3, .46, .42, .26, .05], F['whale'], n, flat=.18, side=(0, 0, 1))
    return g


def jelly_bell(T):
    g = Geo()
    sides = (14, 9, 6)[T.i]
    lathe(g, [(.92, -1.0), (1.0, -.7), (.97, -.2), (.8, .4), (.5, .82), (.001, 1.0)], (0, 0, 0), F['white'], sides)
    lathe(g, [(.001, .55), (.42, .4), (.62, -.1), (.66, -.8), (.92, -1.0)], (0, 0, 0), F['white'], sides)
    if T.i < 2:
        # the rim's scallops
        for k in range(sides):
            a = 2 * math.pi * (k + .5) / sides
            ball(g, (math.sin(a) * .95, -1.0, math.cos(a) * .95), .1, F['white'], 4, 2)
    return g


def jelly_arms(T):
    g = Geo()
    n = (7, 5, 3)[T.i]
    seg = (7, 4, 2)[T.i]
    for k in range(n):
        a = 2 * math.pi * k / n + .3
        r = .75 + .2 * hashf(k, 3)
        path = []
        for s in range(seg + 1):
            t = s / seg
            w = math.sin(t * 7 + k * 1.9) * .12 * t
            path.append((math.sin(a) * r * (1 - .25 * t) + w, 1 - 2 * t * (.75 + .25 * hashf(k, 5)), math.cos(a) * r * (1 - .25 * t) + w * .6))
        tube(g, path, .035, F['white'], 3, caps=False)
    for k in range(4 if T.i < 2 else 2):
        a = k * math.pi / 2 + .6
        path = [(math.sin(a) * .25 * (1 + t), 1 - 1.3 * t, math.cos(a) * .25 * (1 + t)) for t in [s / 3 for s in range(4)]]
        tube(g, path, .09, F['white'], 4, caps=True)
    return g


def fish(T):
    g = Geo()
    if T.i == 2:
        g.face(((0, 0, 1), (0, .3, .1), (0, 0, -.6), (0, -.3, .1)), F['fish'], (1, 0, 0))
        g.face(((0, 0, 1), (0, .3, .1), (0, 0, -.6), (0, -.3, .1)), F['fish'], (-1, 0, 0))
        g.face(((0, 0, -.5), (0, .28, -1), (0, -.28, -1)), F['fishback'], (1, 0, 0))
        g.face(((0, 0, -.5), (0, .28, -1), (0, -.28, -1)), F['fishback'], (-1, 0, 0))
        return g
    n = (8, 6, 4)[T.i]
    limb(g, [(0, 0, 1.0), (0, 0, .7), (0, 0, .2), (0, 0, -.3), (0, 0, -.62)], [.02, .2, .3, .2, .06], F['fish'], n, flat=.4, side=(0, 1, 0))
    for s in (1, -1):
        g.face(((0, 0, -.55), (0, .3, -1.0), (0, 0, -.82), (0, -.3, -1.0)), F['fishback'], (s, 0, 0))
        g.face(((0, .27, .25), (0, .46, -.1), (0, .22, -.25)), F['fishback'], (s, 0, 0))
    return g


def kelp(T):
    """A kelp joint of unit half height along y: a stem and blades leaving it on both sides in the xy plane."""
    g = Geo()
    tube(g, [(0, -1, 0), (.02, -.3, 0), (-.02, .4, 0), (0, 1, 0)], .028, F['kelpstem'], (5, 4, 3)[T.i], caps=False)
    n = (6, 4, 2)[T.i]
    for k in range(n):
        y = -.9 + 1.8 * (k + .5) / n
        s = 1 if k % 2 == 0 else -1
        L = .5 + .14 * hashf(k, 7)
        pts = [(0, y, 0), (s * L * .35, y + .1, .05), (s * L * .75, y + .16, 0), (s * L, y + .12, -.04), (s * L * .7, y - .0, 0), (s * L * .3, y - .05, .03)]
        for nz in (1, -1):
            g.face(pts, F['kelp'], (0, 0, nz))
        if T.i == 0:
            ball(g, (s * .05, y - .02, 0), .035, F['kelpstem'], 4, 2)
    return g


def drone(T):
    g = Geo()
    rbox(g, -.42, .42, -.26, .26, -.66, .66, .14, F['yellow'], segs=(2, 1, 1)[T.i])
    box(g, -.3, .3, .26, .32, -.4, .4, F['dark'], T, .02, cell=0)
    lathe(g, [(.17, .62), (.19, .68), (.15, .72)], (0, -.05, 0), F['brass'], (10, 7, 5)[T.i], axis=(0, 0, 1))
    disc(g, (0, -.05, .715), .15, F['lamp'], (10, 7, 5)[T.i], (0, 0, 1))
    for sx in (-1, 1):
        cylinder(g, (sx * .5, 0, -.4), (sx * .5, 0, .1), .13, F['dark'], (8, 6, 4)[T.i])
        rod(g, (sx * .42, 0, -.15), (sx * .5, 0, -.15), .03, F['steel'], 4, caps=False)
        ball(g, (sx * .3, .33, -.3), .04, F['red'] if sx < 0 else F['cyan'], 5, 3)
    return g


def case(T):
    """A supply case about its block's middle (1.2 m): a rounded orange shell, black corner guards, brass latches, a stencil band."""
    g = Geo()
    h = .6
    rbox(g, -h + .02, h - .02, -h, h - .02, -h + .02, h - .02, .05, F['orange'], segs=(2, 1, 1)[T.i])
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                x0, y0, z0 = sx * h, sy * h - (.02 if sy > 0 else 0), sz * h
                box(g, min(x0, x0 - sx * .16), max(x0, x0 - sx * .16), min(y0, y0 - sy * .16), max(y0, y0 - sy * .16), min(z0, z0 - sz * .16), max(z0, z0 - sz * .16), F['black'], T, .02, cell=0)
    if T.i < 2:
        for (n, u) in (((1, 0, 0), (0, 0, 1)), ((-1, 0, 0), (0, 0, 1)), ((0, 0, 1), (1, 0, 0)), ((0, 0, -1), (1, 0, 0))):
            c = (n[0] * (h - .018), 0, n[2] * (h - .018))
            p = lambda a, y: (c[0] + u[0] * a, y, c[2] + u[2] * a)  # noqa: E731
            g.face((p(-.4, -.12), p(.4, -.12), p(.4, .0), p(-.4, .0)), F['yellow'], n)
            for a in (-.25, .25):
                q = p(a, .3)
                box(g, q[0] - (.012 if n[0] else .05), q[0] + (.012 if n[0] else .05), .24, .36, q[2] - (.012 if n[2] else .05), q[2] + (.012 if n[2] else .05), F['brass'], T, .006, cell=0)
        tube(g, [(-.2, h - .02, 0), (-.2, h + .04, 0), (.2, h + .04, 0), (.2, h - .02, 0)], .016, F['dark'], 4, caps=False)
    return g


def items(L, T):
    return [('ram', ram(T), (0, 9, 0)), ('ram-rod', ram_rod(T), (0, 11, 0)), ('klaxon', klaxon(T), (3.4, 2.5, 3.4)), ('core', core(T), (0, .02, 0)), ('grate', grate(L, T), (-6, -1.57, 16.7)),
            ('whale-body', whale_body(T), (58, 9, 0)), ('whale-tail', whale_tail(T), (58, 9, -8)), ('whale-fin-l', whale_fin(T, -1), (55, 8, 2)), ('whale-fin-r', whale_fin(T, 1), (61, 8, 2)),
            ('jelly-bell', jelly_bell(T), (-36, 4, 0)), ('jelly-arms', jelly_arms(T), (-36, 2, 0)), ('fish', fish(T), (0, 4, 31)), ('kelp', kelp(T), (-36, -4, 13)), ('drone', drone(T), (19, 10, 0))]


def bank_props(sb, L, T, no_ao):
    ti = T.i
    its = items(L, T) + [('case', case(T), (0, .6, 0))]
    geos = [it[1] for it in its]
    aos = [None] * len(geos)
    if not no_ao:
        apart = [g.transformed([1, 0, 0, 60.0 * i, 0, 1, 0, 0, 0, 0, 1, 0]) for i, g in enumerate(geos)]
        aos = sk.bake_ao(apart, distance=.5, samples=(24, 12, 8)[ti])
    for (name, g, rest), ao in zip(its, aos):
        if g.faces == 0:
            continue
        if ao is not None:
            ao = 1.0 - (1.0 - ao) * .7
        Pw = g.arrays()[0].astype(np.float64) + np.array(rest)
        bk = sk.Bake(g, ao, world=Pw, floors=(), seed=7)
        if name == 'case':
            ids = [b['id'] for b in L['blocks'] if b['id'].startswith('rig-case-')]
            om = sk.to_mesh('block/case/%d' % ti, g, bk)
            mi = sb.mesh(om)
            for k, bid in enumerate(ids):
                sb.share('block/%s/%d' % (bid, ti), 'block', mi)
            st = sb.stats.setdefault(('block', str(ti)), [0, 0, 0])
            st[0] += len(ids)
            st[1] += sum(len(s) for s in om.submeshes) // 3
            st[2] += len(om.positions)
        else:
            sb.add('rig/%s/%d' % (name, ti), 'rig', sk.to_mesh('rig/%s/%d' % (name, ti), g, bk))
