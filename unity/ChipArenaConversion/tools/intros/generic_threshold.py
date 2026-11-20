"""Threshold's set: a canyon plaza at dusk where two leaning standing stones meet in a cleft (docs/INTROS-GENERIC.md "Threshold").
Unity space; the hero mark (the origin) is on the plaza, the pilot facing +z; the stones stand behind it, the seam between them on
the axis at z TH_SLIT_Z. No circles anywhere: the plaza is laid in a grid of flagstones. Pieces (generic_sets.py writes them):
  threshold/plaza   the flagstones, the sand and its mounds, the columns and the fallen drums, the obelisks, the rubble at the stones'
                    feet (world frame)
  threshold/canyon  the canyon's walls and boulders (world frame)
  threshold/stoneL  the left standing stone (Unity -x), about the foot of its inner face; its runes are its glow (Core parts it)
  threshold/stoneR  the right standing stone (Unity +x), likewise
  threshold/obglow  the obelisks' glyph bands (glow; world frame)
  threshold/sky     the dusk sky dome (glow)
"""
import math

import numpy as np
import stagekit as sk
from stagekit import Geo, _add, _sub, _norm
from generic_kit import (PI, TAU, V, mul, M, place, noise2, heightfield, polar_field, rockball, mound, dome, disc, flipped, sky_fn,
                         mix_hex, Piece, smoothstep, hash01)

TH_SLIT_Z = -3.2                # the seam's plane (z) between the standing stones
TH_GAP = .25                    # the seam's width at the stones' feet at rest (their tops lean in to touch)
TH_LEAN = .125                  # how far each stone's inner face leans in over its height
TH_SUN = (-.86, -.5)            # the set sun's azimuth (Unity x, z)


def sand_h(x, z):
    r = math.hypot(x, z)
    y = .35 * (noise2(x, z, .06, 81) - .5) + .12 * math.sin(.7 * x + .25 * z + 3 * noise2(x, z, .04, 82))
    return y * smoothstep(9.5, 13.0, r) - .02


def build(T):
    F = dict(
        flag=sk.fin('th-flag', 0xc9a27a, sk.DETAIL_FLAG, smooth=.15, dirt=.8, wear=.3),
        flag2=sk.fin('th-flag2', 0xb48a63, sk.DETAIL_FLAG, smooth=.15, dirt=.9, wear=.3),
        stone=sk.fin('th-stone', 0xd2ad82, sk.DETAIL_CARVED, smooth=.12, dirt=.9, wear=.4),
        stone2=sk.fin('th-stone2', 0xbf9670, sk.DETAIL_ASHLAR, smooth=.12, dirt=1.0, wear=.4),
        dark=sk.fin('th-dark', 0x4a3b40, sk.DETAIL_CARVED, smooth=.2, dirt=.6),
        metal=sk.fin('th-metal', 0x6f7c86, sk.DETAIL_BRASS, smooth=.6, metal=.85, dirt=.5, wear=.5),
        gold=sk.fin('th-gold', 0xc89a4a, sk.DETAIL_BRASS, smooth=.65, metal=.9, dirt=.4, wear=.5),
        sand=sk.fin('th-sand', 0xdcb27c, smooth=.08, dirt=.15),
        cliff1=sk.fin('th-cliff1', 0xc0784a, sk.DETAIL_CARVED, smooth=.06, dirt=.4),
        cliff2=sk.fin('th-cliff2', 0xa65f3b, sk.DETAIL_CARVED, smooth=.06, dirt=.4),
        cliff3=sk.fin('th-cliff3', 0xd49a68, sk.DETAIL_CARVED, smooth=.06, dirt=.4),
        cliff4=sk.fin('th-cliff4', 0x8a4c35, sk.DETAIL_CARVED, smooth=.06, dirt=.4),
        glyph=sk.fin('th-glyph', 0xffffff, glow=1.0, dirt=0),
        sky=sk.fin('th-sky', 0xffffff, glow=1.0, dirt=0),
    )
    fine = T.i == 0
    g = Geo()
    # ---- the plaza: flagstones in a grid round the hero mark, jittered, some lost to sand, their sides down into the bed ----
    step = 1.05 if fine else 3.0
    nx_, nz_ = int(20 / step), int(17 / step)
    for ix in range(nx_):
        for iz in range(nz_):
            x0, z0 = -10 + ix * step, -9 + iz * step
            if fine and hash01('miss', ix, iz) < (.12 if (abs(x0) > 7 or z0 > 5) else .02):
                continue
            x1, z1 = x0 + step, z0 + step
            gap = .028 if fine else 0
            dy = .012 + (.014 * (hash01('h', ix, iz) - .5) if fine else 0)
            j1, j2 = (.04 * (hash01('j1', ix, iz) - .5), .04 * (hash01('j2', ix, iz) - .5)) if fine else (0, 0)
            pts = [V(x0 + gap + j1, dy, z0 + gap), V(x0 + gap, dy, z1 - gap + j2), V(x1 - gap - j2, dy, z1 - gap),
                   V(x1 - gap, dy, z0 + gap - j1)]
            f = F['flag'] if hash01('f', ix, iz) > .4 else F['flag2']
            g.face(pts, f, normal=(0, 1, 0))
            if fine:
                for e in range(4):
                    p0, p1 = pts[e], pts[(e + 1) % 4]
                    q0, q1 = V(p0[0], -.08, p0[2]), V(p1[0], -.08, p1[2])
                    mid = mul(_add(p0, p1), .5)
                    out = _norm((mid[0] - (x0 + x1) / 2, 0, mid[2] - (z0 + z1) / 2))
                    g.face((p0, p1, q1, q0), f, normal=out)
    # ---- the sand round it ----
    heightfield(g, -40, 40, -40, 34, 40 if fine else 12, 37 if fine else 11, sand_h, lambda x, y, z, n: F['sand'], smooth=True)
    for k, (x, z, s) in enumerate(((-8.5, 4.0, 1.3), (9.0, 2.0, 1.1), (-6.0, -8.5, 1.6), (7.2, -9.0, 1.4), (-11.0, -2.0, 1.0), (10.5, 7.0, .9))):
        mound(g, V(x, -.05, z), (2.2 * s, .45 * s, 1.4 * s), F['sand'], 90 + k, 4 if fine else 3, 10 if fine else 6, .08, .7)
    top = 0.0
    # ---- the standing stones: two leaning megaliths meeting in a cleft. Each is built about the foot of its inner face (the seam
    # side) at the origin, the stone running away from the seam (side -1: toward -x, the left one; +1: toward +x); the inner face
    # leans in by TH_LEAN over the height so the two tops touch at rest. Rough outer faces, a slanted broken top, worn runes cut up
    # the front beside the seam (the runes are the glow) ----
    def standing(side, seed):
        sg = Geo()
        H = 6.9 if side < 0 else 6.5
        rows = 12 if fine else 4
        ring_pts = []
        for i_ in range(rows + 1):
            v = i_ / rows
            y = H * v
            lean = TH_LEAN * v
            w = 2.15 - .85 * v ** 1.25
            d = .78 - .22 * v
            jag = [.22 * (noise2(seed * 3.1 + v * 4.0, k * 1.7, 1.0, 140 + seed) - .5) for k in range(6)]
            sec = [(0.0, -d * .96), (0.0, d), (w * .45 + jag[0], d * 1.04 + jag[1] * .3), (w + jag[2], d * .5 + jag[3] * .3),
                   (w * .92 + jag[4], -d * .62), (w * .4 + jag[5], -d * 1.02)]
            ring_pts.append([V(side * (x - lean), y, z) for x, z in sec])
        for i_ in range(rows):
            a, b = ring_pts[i_], ring_pts[i_ + 1]
            for k in range(6):
                p0, p1, q1, q0 = a[k], a[(k + 1) % 6], b[(k + 1) % 6], b[k]
                mid = mul(_add(_add(p0, p1), _add(q0, q1)), .25)
                cxs = side * (1.0 - TH_LEAN * (i_ + .5) / rows)
                out = _norm((mid[0] - cxs, 0, mid[2]))
                sg.face((p0, p1, q1, q0), F['stone'] if k != 0 else F['stone2'], normal=out)
        topr = ring_pts[-1]
        apex = V(side * (.3 - TH_LEAN), H + .35, 0)
        for k in range(6):
            sg.face((topr[k], topr[(k + 1) % 6], apex), F['stone2'], normal=(0, 1, 0))
        nr_ = 9 if fine else 4
        for k in range(nr_):
            y0 = .55 + k * (H - 1.4) / nr_
            y1 = y0 + .26 + .1 * hash01('rl', seed, k)
            v0, v1 = y0 / H, y1 / H
            x_in0, x_in1 = .2 + .06 * hash01('rx', seed, k), .32 + .08 * hash01('rw', seed, k)
            d0, d1 = .78 - .22 * v0 + .012, .78 - .22 * v1 + .012
            q = [V(side * (x_in0 - TH_LEAN * v0), y0, d0), V(side * (x_in1 - TH_LEAN * v0), y0, d0),
                 V(side * (x_in1 - TH_LEAN * v1), y1, d1), V(side * (x_in0 - TH_LEAN * v1), y1, d1)]
            sg.face(q, F['glyph'], normal=(0, 0, 1))
            if k % 3 == 1:
                xm = (x_in0 + x_in1) / 2
                q2 = [V(side * (xm - .03 - TH_LEAN * v1), y1, d1), V(side * (xm + .03 - TH_LEAN * v1), y1, d1),
                      V(side * (xm + .03 - TH_LEAN * v1), y1 + .16, d1), V(side * (xm - .03 - TH_LEAN * v1), y1 + .16, d1)]
                sg.face(q2, F['glyph'], normal=(0, 0, 1))
        return sg
    stoneL, stoneR = standing(-1, 3), standing(1, 7)
    # rubble at the stones' feet (clear of the seam and the walk out of it)
    for k, (x, z, s_) in enumerate(((-2.9, -2.8, .5), (-2.4, -4.0, .35), (3.0, -3.0, .45), (2.6, -4.1, .6), (-3.6, -2.1, .3),
                                    (3.7, -2.3, .32))):
        rockball(g, V(x, s_ * .25, z), (s_ * 1.2, s_ * .8, s_), F['stone2'], 170 + k, 4 if fine else 3, 7 if fine else 5, .25, .45)

    # ---- columns: broken, fluted, in two rows; a fallen drum ----
    def column(x, z, h, k, capital=False):
        cg = Geo()
        sk.box(cg, -.62, .62, 0, .38, -.62, .62, F['stone2'], T, .05)
        n = 20 if fine else 8
        prof_ = [(.46, .38), (.42, .6)]
        sk.lathe(cg, prof_, (0, 0, 0), F['stone'], n)
        rows = 6 if fine else 2
        for j in range(rows):
            y0 = .6 + (h - .6) * j / rows
            y1 = .6 + (h - .6) * (j + 1) / rows
            pts0, pts1 = [], []
            for m in range(n):
                a = TAU * m / n
                fl = .38 - (.025 if (m % 2 and fine) else 0)
                pts0.append(V(math.cos(a) * fl, y0, math.sin(a) * fl))
                top_y = y1 if j < rows - 1 or capital else y1 - .35 * hash01('brk', k, m)
                pts1.append(V(math.cos(a) * fl * .97, top_y, math.sin(a) * fl * .97))
            for m in range(n):
                a, b, c, d = pts0[m], pts0[(m + 1) % n], pts1[(m + 1) % n], pts1[m]
                am = TAU * (m + .5) / n
                cg.face((a, b, c, d), F['stone'], normal=(math.cos(am), 0, math.sin(am)))
            if j == rows - 1 and not capital:
                centre = V(0, y1 - .2, 0)
                for m in range(n):
                    cg.face((centre, pts1[(m + 1) % n], pts1[m]), F['stone2'], normal=(0, 1, 0))
        if capital:
            sk.lathe(cg, [(.4, h), (.62, h + .3), (.66, h + .42)], (0, 0, 0), F['stone'], n, cap1=True)
            sk.box(cg, -.78, .78, h + .42, h + .62, -.78, .78, F['stone2'], T, .04)
        place(g, cg, M((x, -.02, z), yaw=k))
    for k, (x, z, h, cap) in enumerate(((-5.4, 2.6, 3.4, False), (5.4, 2.4, 4.6, True), (-5.6, -.6, 5.2, True), (5.6, -.8, 2.1, False),
                                         (-6.6, -3.9, 4.1, False), (6.5, -4.0, 5.6, True))):
        column(x, z, h, k, cap)
    drum = Geo()
    sk.lathe(drum, [(.001, -.5), (.38, -.5), (.38, .5), (.001, .5)], (0, 0, 0), F['stone'], 16 if fine else 8)
    place(g, drum, M((3.6, .33, 4.6), yaw=.6, pitch=PI / 2))
    place(g, drum, M((-4.2, .33, 5.4), yaw=2.0, pitch=PI / 2, s=.9))
    # ---- two obelisks framing the standing stones, their glyph bands lit ----
    obg = Geo()
    for sx in (-1, 1):
        ob = Geo()
        og = Geo()
        sk.box(ob, -.8, .8, 0, .5, -.8, .8, F['stone2'], T, .04)
        n = 4
        h = 6.2
        for j in range(n):
            a0, a1 = TAU * j / n + PI / 4, TAU * (j + 1) / n + PI / 4
            r0, r1 = .55, .36
            p = [V(math.cos(a0) * r0, .5, math.sin(a0) * r0), V(math.cos(a1) * r0, .5, math.sin(a1) * r0),
                 V(math.cos(a1) * r1, h, math.sin(a1) * r1), V(math.cos(a0) * r1, h, math.sin(a0) * r1)]
            am = (a0 + a1) / 2
            ob.face(p, F['stone'], normal=(math.cos(am), .03, math.sin(am)))
            ob.face((V(math.cos(a0) * r1, h, math.sin(a0) * r1), V(math.cos(a1) * r1, h, math.sin(a1) * r1), V(0, h + .7, 0)), F['gold'],
                    normal=(math.cos(am), .5, math.sin(am)))
            for b in range(4 if fine else 2):
                y0 = 1.2 + b * 1.1
                y1 = y0 + .55
                k0 = .5 - .5 * (y0 - .5) / (h - .5) * (.55 - .36) / .55
                rr0 = .55 + (.36 - .55) * (y0 - .5) / (h - .5) + .004
                rr1 = .55 + (.36 - .55) * (y1 - .5) / (h - .5) + .004
                w = .14
                mid0 = am
                q = [V(math.cos(mid0 - w) * rr0, y0, math.sin(mid0 - w) * rr0), V(math.cos(mid0 + w) * rr0, y0, math.sin(mid0 + w) * rr0),
                     V(math.cos(mid0 + w) * rr1, y1, math.sin(mid0 + w) * rr1), V(math.cos(mid0 - w) * rr1, y1, math.sin(mid0 - w) * rr1)]
                og.face(q, F['glyph'], normal=(math.cos(am), 0, math.sin(am)))
        mo = M((sx * 4.3, top - .02, TH_SLIT_Z - .4), yaw=.2 * sx)
        place(g, ob, mo)
        place(obg, og, mo)
    # ---- the canyon: layered walls either side and behind ----
    canyon = Geo()

    def strata(y):
        b = int(math.floor((y + 3) / 3.2))
        return (F['cliff1'], F['cliff2'], F['cliff3'], F['cliff4'])[b % 4]
    for sx in (-1, 1):
        nz_ = 26 if fine else 8
        ny_ = 16 if fine else 5
        P = []
        for i in range(ny_ + 1):
            y = -1 + 36 * i / ny_
            row = []
            for j in range(nz_ + 1):
                z = -60 + 110 * j / nz_
                ledge = 1.6 * (math.floor((y + 3) / 3.2) % 2)
                xx = sx * (17 + .12 * y + 4.5 * noise2(y * .7, z, .06, 100 + sx) + ledge + 6 * smoothstep(20, 50, z))
                row.append(V(xx, y, z))
            P.append(row)
        for i in range(ny_):
            for j in range(nz_):
                a, b, c, d = P[i][j], P[i][j + 1], P[i + 1][j + 1], P[i + 1][j]
                yy = (a[1] + c[1]) / 2
                for tri in ((a, b, c), (a, c, d)):
                    n = _norm(np.cross(np.subtract(tri[1], tri[0]), np.subtract(tri[2], tri[0])).tolist())
                    if n[0] * -sx < 0:
                        n = mul(n, -1)
                    canyon.face(tri, strata(yy), normal=n)
    # The far wall behind the stones, with a saddle worn down its middle (its top falls from 39 m to about 7 m within 6 m of the axis):
    # the cleft frames the dusk sky over the saddle, not a flat band of cliff.
    nb = 30 if fine else 9
    P = []
    rows_ = 12 if fine else 3
    for i in range(rows_ + 1):
        row = []
        for j in range(nb + 1):
            x = -24 + 48 * j / nb
            top = 39 - 32 * (1 - smoothstep(6, 14, abs(x))) + 1.2 * noise2(x, 0, .3, 121)
            y = -1 + (top + 1) * i / rows_
            ledge = 1.6 * (math.floor((y + 3) / 3.2) % 2)
            row.append(V(x, y, -32 - .1 * y - 4 * noise2(x, y, .07, 120) - ledge))
        P.append(row)
    for i in range(len(P) - 1):
        for j in range(nb):
            a, b, c, d = P[i][j], P[i][j + 1], P[i + 1][j + 1], P[i + 1][j]
            yy = (a[1] + c[1]) / 2
            for tri in ((a, b, c), (a, c, d)):
                n = _norm(np.cross(np.subtract(tri[1], tri[0]), np.subtract(tri[2], tri[0])).tolist())
                if n[2] < 0:
                    n = mul(n, -1)
                canyon.face(tri, strata(yy), normal=n)
    for k in range(10 if fine else 4):
        x = (-1) ** k * (12 + 3 * hash01('b', k))
        z = -20 + 30 * hash01('bz', k)
        s = .8 + 1.4 * hash01('bs', k)
        rockball(canyon, V(x, sand_h(x, z) + s * .3, z), (s * 1.3, s * .9, s), F['cliff2'], 130 + k, 4 if fine else 3, 7 if fine else 5, .25, .4)
    sky = Geo()
    dome(sky, 172, F['sky'], 10 if fine else 5, 32 if fine else 14, -16, 90)
    skyfn = sky_fn(0x2c2f6a, 0xf08a52, TH_SUN, 0xffb46a, .8, below=0x6a4a58)
    return [
        Piece('threshold/plaza', g, True, (0.0,), seed=7),
        Piece('threshold/canyon', canyon, False, (-1.0,), seed=8),
        Piece('threshold/stoneL', stoneL, True, (0.0,), seed=11, preview=[M((-TH_GAP / 2, 0, TH_SLIT_Z))]),
        Piece('threshold/stoneR', stoneR, True, (0.0,), seed=12, preview=[M((TH_GAP / 2, 0, TH_SLIT_Z))]),
        Piece('threshold/obglow', obg, False),
        Piece('threshold/sky', sky, False, (-500.0,), recolour=({F['sky']}, skyfn)),
    ]


VIEWS = [
    ('th-hero', (0, 1.0, 4.4), (0, 1.6, -2), 44),
    ('th-wide', (0, 2.2, 12), (0, 2.6, -5), 56),
    ('th-side', (7.5, 2.5, 1.0), (0, 2.6, -4.5), 55),
    ('th-high', (10, 14, 16), (0, 0, -4), 60),
]
