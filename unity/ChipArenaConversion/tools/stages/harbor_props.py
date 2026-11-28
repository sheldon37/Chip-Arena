"""Pier 9's rig props (docs/arenas/HARBOR.md, docs/arenas/STAGE-ART.md): the moving parts, each in its own frame, posed every
frame by Core NativePierArt from the box rig's tagged parts (Core NativePierRig).

Frames (Unity space; the origin is the tagged part's centre):
  wheel            the hub; the wheel in the y-z plane, its axle along x (a rim point at angle a: y = R sin a, z = R cos a)
  wheel-bulbs-<g>  the same frame: the rims' and spokes' bulbs in three chasing groups (white glow times the rig's tint)
  cabin-<k>        a gondola's middle, 1.5 m under its pivot on the rim; four paints
  table            the carousel's turntable: its centre .05 m over the upper deck, y up; it carries the mounts' poles
  mount-0, mount-1 a horse and a sea horse: the saddle's middle, +z the way it goes
  star             the band stand's star: its hub, the star in the x-y plane
  doll             a bandsman: its body's middle, +z its front
  boat             a moored boat: the hull's middle (.25 m over the water), +z its bow
  car, car-end     a coaster car: its body's middle (.62 m over the rails' top), +x along the track; an end car's nose toward +x
  wigwag           a crossing lamp: its lens's centre, faces toward +z and -z (white glow times the rig's red)
  lane-lamp        a lamp set in the crossing's boards: its centre .03 m over them
  ring             a drop tower's gondola: its collar's centre, .35 m over its underside, y up, the mast through its middle
  beacon           the lamp on a mast's crown: its centre
  marquee-<g>      the chasing bulbs of the whole pier in the world's frame (collected by harbor_art.py's static scene)
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, sweep, lathe  # noqa: E402
import harbor_kit as hk  # noqa: E402
from harbor_kit import F, rod, ball, prism, ngon, fo  # noqa: E402


def octa(g, x, y, z, r, f):
    top, bot = (x, y + r, z), (x, y - r, z)
    ring = [(x + r, y, z), (x, y, z + r), (x - r, y, z), (x, y, z - r)]
    for j in range(4):
        a, b = ring[j], ring[(j + 1) % 4]
        # corners shaded round (their normals from the centre), so they merge: six vertices a bulb
        na, nb = sk._norm(sk._sub(a, (x, y, z))), sk._norm(sk._sub(b, (x, y, z)))
        g.face((b, a, top), f, normals=(nb, na, (0, 1, 0)))
        g.face((a, b, bot), f, normals=(na, nb, (0, -1, 0)))


def wheel(T, P):
    g = Geo()
    R, gp, n = P['wheelR'], P['wheelGap'], int(P['gondolas'])
    seg = (64, 48, 32)[T.i]
    for sx in (-1, 1):
        for r, t in ((R, .12), (R - 2.2, .07), (R * .45, .06)):
            path = [(sx * gp, r * math.sin(2 * math.pi * i / seg), r * math.cos(2 * math.pi * i / seg)) for i in range(seg)]
            sk.tube(g, path, t, F['ironwhite'], (6, 5, 4)[T.i], closed=True)
        for i in range(n * 2):
            a = 2 * math.pi * i / (n * 2)
            rod(g, (sx * gp, 1.1 * math.sin(a), 1.1 * math.cos(a)), (sx * gp, R * math.sin(a), R * math.cos(a)), .05 if i % 2 == 0 else .035, F['ironwhite'], 4, False)
            if T.i < 2:
                b = 2 * math.pi * (i + 1) / (n * 2)
                rod(g, (sx * gp, (R - 2.2) * math.sin(a), (R - 2.2) * math.cos(a)), (sx * gp, R * math.sin(b), R * math.cos(b)), .03, F['ironwhite'], 3, False)
    for i in range(n * 2):
        a = 2 * math.pi * i / (n * 2)
        rr = R if i % 2 == 0 else R - 2.2
        rod(g, (-gp, rr * math.sin(a), rr * math.cos(a)), (gp, rr * math.sin(a), rr * math.cos(a)), .06 if i % 2 == 0 else .035, F['steel' if i % 2 == 0 else 'ironwhite'], 4, False)
    # the hub: a drum between the rims, a gold boss with a star each side
    lathe(g, [(1.15, -gp - .08), (1.25, -gp), (1.25, gp), (1.15, gp + .08)], (0, 0, 0), F['ironred'], (16, 12, 8)[T.i], axis=(1, 0, 0), cap0=True, cap1=True)
    for sx in (-1, 1):
        lathe(g, [(.02, 0), (.75, 0), (.6, .22), (.2, .34), (.02, .36)], (sx * (gp + .08), 0, 0), F['gold'], (12, 8, 6)[T.i], axis=(sx, 0, 0))
    return g


def wheel_bulbs(T, P, k):
    """The wheel's lamps of chase group k: along both rims and out along every spoke."""
    g = Geo()
    R, gp, n = P['wheelR'], P['wheelGap'], int(P['gondolas'])
    m = (96, 96, 48)[T.i]
    for sx in (-1, 1):
        for i in range(m):
            if i % 3 != k:
                continue
            a = 2 * math.pi * i / m
            octa(g, sx * (gp + .16), R * math.sin(a), R * math.cos(a), .14, F[('bulbp', 'bulbc', 'bulbw')[i % 3]])
        for i in range(n):
            a = 2 * math.pi * i / n
            for j in range(1, (9, 9, 5)[T.i]):
                if (i + j) % 3 != k:
                    continue
                r = 1.6 + (R - 3.4) * j / (9, 9, 5)[T.i]
                octa(g, sx * (gp + .1), r * math.sin(a), r * math.cos(a), .11, F[('bulbw', 'bulbp', 'bulbw', 'bulbc')[i % 4]])
    return g


def cabin(T, k):
    g = Geo()
    f = F[('red', 'teal', 'gold', 'lilac')[k]]
    # the tub: a rounded box flaring to its rim, a cream rim, a floor
    n = (4, 3, 2)[T.i]

    def rrect(hx, hz, r):
        pts = []
        for cx, cz, a0 in ((hx - r, hz - r, 0), (-hx + r, hz - r, math.pi / 2), (-hx + r, -hz + r, math.pi), (hx - r, -hz + r, 1.5 * math.pi)):
            for i in range(n + 1):
                a = a0 + math.pi / 2 * i / n
                pts.append((cx + r * math.cos(a), cz + r * math.sin(a)))
        return pts
    lo, hi = rrect(.72, .62, .22), rrect(.86, .76, .26)
    m = len(lo)
    for i in range(m):
        j = (i + 1) % m
        fo(g, ((lo[i][0], -.62, lo[i][1]), (lo[j][0], -.62, lo[j][1]), (hi[j][0], .0, hi[j][1]), (hi[i][0], .0, hi[i][1])), f, (0, -.3, 0))
    g.face([(p[0], -.62, p[1]) for p in lo], F['iron'], normal=(0, -1, 0))
    g.face([(p[0], -.5, p[1]) for p in lo], F['crate'], normal=(0, 1, 0))
    prism(g, hi, 0, .07, F['cream'], top=True)
    # the posts, the roof and the hanger up to the pivot
    for sx in (-1, 1):
        for sz in (-1, 1):
            rod(g, (sx * .7, .05, sz * .6), (sx * .7, .78, sz * .6), .025, F['brass'], 4, False)
    lathe(g, [(.98, .78), (1.0, .82), (.8, .98), (.45, 1.1), (.12, 1.16), (.02, 1.17)], (0, 0, 0), F['cream'], (12, 8, 6)[T.i], scale=(1, .88))
    lathe(g, [(.99, .76), (1.02, .8), (.99, .84)], (0, 0, 0), f, (12, 8, 6)[T.i], scale=(1, .88))
    for sx in (-1, 1):
        rod(g, (sx * .8, .9, 0), (sx * .98, 1.5, 0), .035, F['iron'], 4, False)
    rod(g, (-1.02, 1.5, 0), (1.02, 1.5, 0), .05, F['iron'], 5)
    ball(g, (0, .7, 0), .1, F['globe'], 6, 3)
    return g


def table(T, P):
    g = Geo()
    n = (32, 20, 12)[T.i]
    prism(g, ngon(0, 0, 3.72, n), -.03, .02, F['gold'], top=True, f_top=F['teal'], smooth=True)
    for r0, r1, f in ((3.5, 3.3, 'gold'), (1.2, 1.0, 'cream')):
        a, b = ngon(0, 0, r0, n), ngon(0, 0, r1, n)
        for i in range(n):
            g.face(((a[i][0], .026, a[i][1]), (a[(i + 1) % n][0], .026, a[(i + 1) % n][1]), (b[(i + 1) % n][0], .026, b[(i + 1) % n][1]), (b[i][0], .026, b[i][1])), F[f], normal=(0, 1, 0))
    # the hub: a mirrored octagon between gold bands
    top = P['rideEave'] - P['rideDeck'] - .05
    prism(g, ngon(0, 0, .68, 8, math.pi / 8), .02, top - .3, F['mirror'], top=False)
    lathe(g, [(.8, .02), (.82, .12), (.72, .22)], (0, 0, 0), F['gold'], 8, phase=math.pi / 8, smooth=False)
    lathe(g, [(.72, top - .5), (.84, top - .36), (.84, top - .26), (.5, top - .12), (.2, top)], (0, 0, 0), F['gold'], 8, phase=math.pi / 8, smooth=False)
    if T.i < 2:
        for k in range(8):
            a = k * math.pi / 4
            ball(g, (math.sin(a) * .86, top - .31, math.cos(a) * .86), .06, F['glasspink'], 5, 2)
    # the mounts' poles and the sweeps out to them under the canopy
    for dx, dz, yaw in P['mountAt']:
        rod(g, (dx, .02, dz), (dx, top, dz), .028, F['brass'], (6, 5, 4)[T.i], False)
        rod(g, (dx * .2, top - .2, dz * .2), (dx, top - .04, dz), .03, F['gold'], 4, False)
    return g


def mount(T, kind):
    """A carousel mount (0 a horse, 1 a sea horse): +z forward, the saddle's middle at the origin."""
    g = Geo()
    n = (10, 7, 5)[T.i]
    coat = F['white' if kind == 0 else 'teal']
    trim = F['gold']
    lathe(g, [(.02, -.6), (.15, -.52), (.2, -.3), (.185, .05), (.2, .34), (.14, .54), (.03, .62)], (0, 0, 0), coat, n, axis=(0, 0, 1), scale=(.92, 1.18))
    # the neck and the head
    rod(g, (0, .1, .4), (0, .56, .68), .12, coat, n, False, r1=.085)
    rod(g, (0, .62, .62), (0, .43, .98), .095, coat, n, True, r1=.052)
    for sx in (-1, 1):
        rod(g, (sx * .05, .66, .62), (sx * .07, .8, .6), .028, coat, 4, True, r1=.004)
    # the mane, the saddle and its cloth
    sweep(g, [(-.02, 0), (.02, 0), (0, .09)], [(0, .2, .44), (0, .45, .58), (0, .68, .64)], trim, up=(0, .5, -.8), caps=True, smooth_u=False)
    box(g, -.2, .2, .2, .27, -.3, .16, F['red'], T, .03, 0)
    box(g, -.215, .215, .02, .21, -.24, .1, F['navy' if kind == 0 else 'lilac'], T, .02, 0)
    lathe(g, [(.03, .26), (.035, .4), (.01, .42)], (0, 0, .14), trim, 5)
    if kind == 0:
        # galloping legs and gold hooves, a flowing tail
        for sx in (-1, 1):
            for path in ([(sx * .1, -.1, .36), (sx * .1, -.38, .56), (sx * .1, -.6, .44)], [(sx * .1, -.1, -.38), (sx * .1, -.36, -.6), (sx * .1, -.62, -.76)]):
                sk.tube(g, path, .042, coat, (5, 4, 3)[T.i], caps=False)
                ball(g, path[-1], .05, trim, 5, 2)
        sk.tube(g, sk.catmull([(0, .06, -.58), (0, .0, -.82), (0, -.22, -.96), (0, -.42, -.94)], 3), .04, trim, (5, 4, 3)[T.i])
    else:
        # the sea horse: a curled tail, a fin down its back, two flippers
        path = sk.catmull([(0, -.02, -.5), (0, -.2, -.74), (0, -.46, -.78), (0, -.62, -.58), (0, -.56, -.36), (0, -.42, -.42)], (4, 3, 2)[T.i])
        sweep(g, sk.circle_section(1.0, (6, 5, 4)[T.i]), path, coat, caps=True, scale=[.15 * (1 - .8 * i / (len(path) - 1)) for i in range(len(path))])
        for i in range(5):
            z = .3 - i * .17
            g.face(((0, .24, z), (0, .24, z - .15), (0, .4 - .03 * i, z - .12)), trim, normal=(1, 0, 0))
            g.face(((0, .24, z), (0, .24, z - .15), (0, .4 - .03 * i, z - .12)), trim, normal=(-1, 0, 0))
        for sx in (-1, 1):
            g.face(((sx * .18, -.05, .4), (sx * .2, -.2, .26), (sx * .42, -.26, .5)), trim, normal=(0, 1, 0))
            g.face(((sx * .18, -.05, .4), (sx * .2, -.2, .26), (sx * .42, -.26, .5)), trim, normal=(0, -1, 0))
    return g


def star(T):
    g = Geo()
    pts = []
    for i in range(16):
        r = (1.3, .42, .9, .42)[i % 4]
        a = i * math.pi / 8
        pts.append((r * math.sin(a), r * math.cos(a)))
    for sz in (-1, 1):
        for i in range(16):
            a, b = pts[i], pts[(i + 1) % 16]
            fo(g, ((0, 0, sz * .12), (a[0], a[1], sz * .03), (b[0], b[1], sz * .03)), F['gold'], (0, 0, 0))
    for i in range(16):
        a, b = pts[i], pts[(i + 1) % 16]
        fo(g, ((a[0], a[1], -.03), (b[0], b[1], -.03), (b[0], b[1], .03), (a[0], a[1], .03)), F['golddim'], (0, 0, 0))
    for i in range(0, 16, 2):
        for sz in (-1, 1):
            ball(g, (pts[i][0] * .96, pts[i][1] * .96, sz * .07), .075, F['globe' if i % 4 == 0 else 'glasscyan'], 5, 2)
    for sz in (-1, 1):
        ball(g, (0, 0, sz * .13), .16, F['glasscyan'], 6, 3)
    return g


def doll(T):
    g = Geo()
    n = (8, 6, 5)[T.i]
    lathe(g, [(.02, -.3), (.13, -.29), (.15, -.18), (.1, .04), (.13, .18), (.06, .29), (.02, .3)], (0, 0, 0), F['red'], n)
    lathe(g, [(.105, -.02), (.115, .0), (.105, .02)], (0, 0, 0), F['gold'], n)
    ball(g, (0, .41, 0), .1, F['cream'], n, 3)
    lathe(g, [(.105, .47), (.1, .62), (.06, .64), (.01, .7)], (0, 0, 0), F['navy'], n)
    lathe(g, [(.108, .47), (.112, .5), (.106, .52)], (0, 0, 0), F['gold'], n)
    # a drum on its front, the sticks raised
    lathe(g, [(.02, 0), (.1, 0), (.1, .1), (.02, .1)], (0, .0, .13), F['white'], n, axis=(0, .3, 1))
    for sx in (-1, 1):
        rod(g, (sx * .12, .16, 0), (sx * .17, .02, .14), .03, F['red'], 4, False)
        rod(g, (sx * .17, .02, .14), (sx * .1, .2, .24), .012, F['crate'], 3, False)
    return g


def boat(T, k=0):
    g = Geo()
    hull = F['hull']
    rows = []
    m = (9, 7, 5)[T.i]
    for i in range(m):
        t = -1 + 2 * i / (m - 1)
        z = t * 2.6
        bw = .95 * (1 - abs(t) ** 2.4 * (.95 if t > 0 else .45))
        sheer = .45 + .28 * t * t + (.12 * t if t > 0 else 0)
        rows.append([(-bw, sheer, z), (-bw * .82, -.1, z), (0, -.45 + .15 * t * t, z), (bw * .82, -.1, z), (bw, sheer, z)])
    for i in range(m - 1):
        for j in range(4):
            fo(g, (rows[i][j], rows[i + 1][j], rows[i + 1][j + 1], rows[i][j + 1]), hull, (0, .6, rows[i][0][2]))
    fo(g, rows[0], hull, (0, 0, 0))
    for i in range(m - 1):
        a, b = rows[i], rows[i + 1]
        g.face(((a[0][0], a[0][1] - .12, a[0][2]), (a[4][0], a[4][1] - .12, a[4][2]), (b[4][0], b[4][1] - .12, b[4][2]), (b[0][0], b[0][1] - .12, b[0][2])), F['crate'], normal=(0, 1, 0))
        for j in (0, 4):
            sweep(g, [(-.04, -.03), (.04, -.03), (.04, .03), (-.04, .03)], [a[j], b[j]], F['white'], up=(0, 1, 0), smooth_u=False, caps=False)
    # the wheelhouse, its lit window, the mast and its lantern
    box(g, -.55, .55, .35, 1.45, -1.4, .1, F['cream'], T, .04, 0)
    box(g, -.62, .62, 1.45, 1.53, -1.5, .2, F['navy'], T, .02, 0)
    box(g, -.4, .4, .95, 1.3, .1, .12, F['glass'], None, 0, 0)
    rod(g, (0, 1.5, .5), (0, 3.9, .5), .035, F['crate'], 5)
    ball(g, (0, 3.68, .5), .11, F['globe'], 6, 3)
    return g


def car(T, end):
    """A coaster car: +x along the track; the origin .62 m over the rails' top."""
    g = Geo()
    n = (10, 7, 5)[T.i]
    paint = F['ironred']
    box(g, -1.12, 1.12, -.52, -.34, -.62, .62, F['iron'], T, .03, 0)
    # the tub: a low rounded body with a cream stripe, two rows of seats with head rests, lap bars
    box(g, -1.2, 1.2, -.36, .12, -.76, .76, paint, T, .1, 0)
    box(g, -1.21, 1.21, -.14, -.04, -.775, .775, F['ironwhite'], T, .02, 0)
    box(g, -1.0, 1.0, .1, .14, -.6, .6, F['black'], None, 0, 0)
    for xs in (-.72, .26):
        box(g, xs - .14, xs + .06, .1, .78, -.62, .62, F['navy'], T, .06, 0)
        for sz in (-1, 1):
            box(g, xs - .15, xs + .02, .74, .98, sz * .34 - .16, sz * .34 + .16, F['navy'], T, .05, 0)
            path = [(xs + .1, .12, sz * .34 - .2), (xs + .45, .46, sz * .34 - .2), (xs + .45, .46, sz * .34 + .2), (xs + .1, .12, sz * .34 + .2)]
            sk.tube(g, path, .025, F['gold'], 4, caps=False)
    # the wheels: road wheels on the rails, bogie plates
    for xs in (-.8, .8):
        for sz in (-1, 1):
            lathe(g, [(.02, -.05), (.17, -.05), (.17, .05), (.02, .05)], (xs, -.47, sz * .72), F['steel'], n, axis=(0, 0, 1), cap0=True, cap1=True)
            box(g, xs - .24, xs + .24, -.56, -.36, sz * .8 - .03, sz * .8 + .03, F['iron'], T, .02, 0)
    if end:
        # the nose: a bullet cowl, gold wings, a headlamp
        lathe(g, [(.74, 0), (.7, .2), (.56, .42), (.34, .58), (.1, .66), (.02, .67)], (1.18, -.1, 0), paint, n, axis=(1, 0, 0), scale=(.62, 1.0))
        ball(g, (1.84, -.1, 0), .13, F['globe'], 6, 3)
        for sz in (-1, 1):
            fo(g, ((1.2, -.02, sz * .76), (1.75, -.1, sz * .2), (1.25, .3, sz * .6)), F['gold'], (1.3, 0, 0))
            fo(g, ((1.2, -.02, sz * .76), (1.25, .3, sz * .6), (1.75, -.1, sz * .2)), F['gold'], (1.5, .1, sz * 2))
    else:
        box(g, 1.2, 1.32, -.3, -.2, -.1, .1, F['iron'], None, 0, 0)
        box(g, -1.32, -1.2, -.3, -.2, -.1, .1, F['iron'], None, 0, 0)
    return g


def wigwag(T):
    g = Geo()
    lathe(g, [(.001, -.07), (.1, -.06), (.125, 0), (.1, .06), (.001, .07)], (0, 0, 0), F['white-glow'], (10, 8, 6)[T.i], axis=(0, 0, 1))
    return g


def lane_lamp(T):
    g = Geo()
    lathe(g, [(.1, -.03), (.1, -.01), (.06, .02), (.001, .03)], (0, 0, 0), F['white-glow'], (8, 6, 5)[T.i])
    return g


def ring(T, L):
    """A drop tower's gondola: a collar round the mast, a square frame, a row of four seats facing out on each side with
    their shoulder bars, lamps under it. The origin .35 m over its underside."""
    g = Geo()
    h = next(z for z in L['hazards'] if z['kind'] == 'press')
    r = h['size'][0] / 2
    # the collar and its rollers
    for sx in (-1, 1):
        box(g, sx * .82 - .12, sx * .82 + .12, -.35, .35, -.94, .94, F['iron'], T, .03, 0)
        box(g, -.7, .7, -.35, .35, sx * .82 - .12, sx * .82 + .12, F['iron'], T, .03, 0)
        for q in (-1, 1):
            lathe(g, [(.02, -.08), (.12, -.08), (.12, .08), (.02, .08)], (sx * .76, q * .2, 0), F['steel'], 8, axis=(0, 0, 1))
    # the frame: four beams under the seats, struts to the collar
    for sx in (-1, 1):
        box(g, sx * (r - .9), sx * r, -.35, -.05, -r, r, F['navy'], T, .05, 0) if sx > 0 else box(g, -r, -(r - .9), -.35, -.05, -r, r, F['navy'], T, .05, 0)
        box(g, -(r - .9), r - .9, -.35, -.05, sx * (r - .9) if sx > 0 else -r, sx * r if sx > 0 else -(r - .9), F['navy'], T, .05, 0)
        box(g, sx * .94 if sx > 0 else -(r - .9), sx * (r - .9) if sx > 0 else -.94, -.3, -.1, -.12, .12, F['iron'], T, .02, 0)
        box(g, -.12, .12, -.3, -.1, sx * .94 if sx > 0 else -(r - .9), sx * (r - .9) if sx > 0 else -.94, F['iron'], T, .02, 0)
    # the seats
    for side_ in range(4):
        a = side_ * math.pi / 2
        ca, sa = math.cos(a), math.sin(a)

        def W(u, v, w):
            # u along the side, v up, w outward
            return (ca * u + sa * w, v, -sa * u + ca * w)
        for k in range(4):
            u = (k - 1.5) * .92
            geo = Geo()
            box(geo, u - .36, u + .36, -.05, .12, r - .82, r - .12, F['black'], T, .04, 0)
            box(geo, u - .38, u + .38, .1, 1.3, r - .98, r - .78, F['ironred'], T, .08, 0)
            box(geo, u - .2, u + .2, 1.28, 1.66, r - .98, r - .8, F['ironred'], T, .08, 0)
            g.extend(geo.transformed([ca, 0, sa, 0, 0, 1, 0, 0, -sa, 0, ca, 0]))
            if T.i < 2:
                for q in (-1, 1):
                    path = [W(u + q * .22, 1.5, r - .86), W(u + q * .22, 1.6, r - .5), W(u + q * .22, 1.0, r - .28), W(u + q * .22, .45, r - .32)]
                    sk.tube(g, sk.catmull(path, 3) if T.i == 0 else path, .035, F['gold'], 5, caps=True)
        for k in range(5):
            ball(g, W((k - 2) * .95, -.36, r - .25), .09, F['lampred' if k % 2 == 0 else 'globe'], 5, 2)
    return g


def beacon(T):
    g = Geo()
    lathe(g, [(.3, -.3), (.32, -.1), (.26, .12), (.14, .26), (.02, .3)], (0, 0, 0), F['white-glow'], (10, 8, 6)[T.i])
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        rod(g, (math.sin(a) * .34, -.3, math.cos(a) * .34), (math.sin(a) * .1, .36, math.cos(a) * .1), .02, F['iron'], 3, False)
    return g


def items(L, T, marquee):
    P = L['pier']
    out = [('wheel', wheel(T, P), (P['wheelX'], P['wheelY'], P['wheelZ']))]
    for k in range(3):
        out.append(('wheel-bulbs-%d' % k, wheel_bulbs(T, P, k), (P['wheelX'], P['wheelY'], P['wheelZ'])))
    for k in range(4):
        out.append(('cabin-%d' % k, cabin(T, k), (P['wheelX'], 20, 0)))
    out += [('table', table(T, P), (P['carouselX'], P['rideDeck'], P['carouselZ'])), ('mount-0', mount(T, 0), (P['carouselX'], 4.2, P['carouselZ'])),
            ('mount-1', mount(T, 1), (P['carouselX'], 4.2, P['carouselZ'])), ('star', star(T), (P['bandX'], P['starY'], P['bandZ'])), ('doll', doll(T), (P['bandX'], 3.6, P['bandZ'])),
            ('boat', boat(T), (31, P['sea'] + .25, -21)), ('car', car(T, False), (0, .74, 0)), ('car-end', car(T, True), (0, .74, 0)), ('wigwag', wigwag(T), (0, 2.35, -5.7)),
            ('lane-lamp', lane_lamp(T), (0, .03, 1.5)), ('ring', ring(T, L), (10, 10, -15)), ('beacon', beacon(T), (10, 21.3, -15))]
    for k in range(3):
        out.append(('marquee-%d' % k, marquee[k], (0, 0, 0)))
    return out


def bank_props(sb, L, T, no_ao, marquee):
    ti = T.i
    its = items(L, T, marquee)
    geos = [it[1] for it in its]
    aos = [None] * len(geos)
    if not no_ao:
        # each prop baked on its own (apart from the others); the bulbs are light and take none
        idx = [i for i, it in enumerate(its) if 'bulbs' not in it[0] and not it[0].startswith('marquee')]
        apart = [geos[i].transformed([1, 0, 0, 80.0 * k, 0, 1, 0, 0, 0, 0, 1, 0]) for k, i in enumerate(idx)]
        res = sk.bake_ao(apart, distance=.5, samples=(24, 12, 8)[ti])
        for k, i in enumerate(idx):
            aos[i] = 1.0 - (1.0 - res[k]) * .8
    for (name, g, rest), ao in zip(its, aos):
        if g.faces == 0:
            raise ValueError('an empty rig prop: ' + name)
        Pw = g.arrays()[0].astype(np.float64) + np.array(rest)
        bk = sk.Bake(g, ao, world=Pw, floors=(), seed=7)
        sb.add('rig/%s/%d' % (name, ti), 'rig', sk.to_mesh('rig/%s/%d' % (name, ti), g, bk))
