"""Waygate's set: a canyon plaza at dusk with an ancient ring gate (docs/INTROS-GENERIC.md "Waygate"). Unity space; the hero mark (the
origin) is on the plaza, the pilot facing +z; the gate stands behind it on its dais, its ring's centre WG_RING. Pieces
(generic_sets.py writes them):
  waygate/plaza     the flagstones and their inlay, the dais, the gate's outer ring and its feet, the columns, the obelisks, the sand
                    (world frame)
  waygate/canyon    the canyon's walls (world frame)
  waygate/inner     the gate's inner ring, about the ring's axis (local: centred on the origin, the axis +z); its glyphs are its glow
  waygate/lens      one keystone lamp's lens, at the origin facing +z (Core draws nine round the outer ring)
  waygate/channels  the outer ring's glyph channels (glow; world frame)
  waygate/inlay     the plaza inlay's glow channel round the hero mark (glow; world frame)
  waygate/obglow    the obelisks' glyph bands (glow; world frame)
  waygate/membrane  the gate's disc of light, radius 1 at the origin facing +z (Core scales it, spins it)
  waygate/sky       the dusk sky dome (glow)
"""
import math

import numpy as np
import stagekit as sk
from stagekit import Geo, _add, _sub, _norm
from generic_kit import (PI, TAU, V, mul, M, place, noise2, heightfield, polar_field, rockball, mound, dome, disc, flipped, sky_fn,
                         mix_hex, Piece, smoothstep, hash01)

WG_RING = (0.0, 4.05, -7.0)     # the ring's centre (its axis along z, facing +z)
WG_R0, WG_R1 = 3.15, 3.85       # the outer ring's radii
WG_I0, WG_I1 = 2.55, 3.08       # the inner ring's radii
WG_KEYS = 6                     # keystone lamps round the outer ring, at WG_KEY_DEG (degrees from +x toward +y)
WG_KEY_DEG = (10, 50, 130, 170, 250, 290)
WG_FIN_DEG = (90, 210, 330)     # the three fins standing out of the ring
WG_KEY_R = 3.5                  # their radius
WG_DAIS = (3.3, 3.85, 4.4)      # the dais's three steps' radii round the gate's foot (0, WG_DAIS_Z)
WG_DAIS_Z = -6.8
WG_STEP = .22                   # each step's rise
WG_SUN = (-.86, -.5)            # the set sun's azimuth (Unity x, z)


def sand_h(x, z):
    r = math.hypot(x, z)
    y = .35 * (noise2(x, z, .06, 81) - .5) + .12 * math.sin(.7 * x + .25 * z + 3 * noise2(x, z, .04, 82))
    return y * smoothstep(9.5, 13.0, r) - .02


def build(T):
    F = dict(
        flag=sk.fin('wg-flag', 0xc9a27a, sk.DETAIL_FLAG, smooth=.15, dirt=.8, wear=.3),
        flag2=sk.fin('wg-flag2', 0xb48a63, sk.DETAIL_FLAG, smooth=.15, dirt=.9, wear=.3),
        inlay=sk.fin('wg-inlay', 0x5b4a5e, sk.DETAIL_MARBLE, smooth=.45, dirt=.4),
        stone=sk.fin('wg-stone', 0xd2ad82, sk.DETAIL_CARVED, smooth=.12, dirt=.9, wear=.4),
        stone2=sk.fin('wg-stone2', 0xbf9670, sk.DETAIL_ASHLAR, smooth=.12, dirt=1.0, wear=.4),
        dark=sk.fin('wg-dark', 0x4a3b40, sk.DETAIL_CARVED, smooth=.2, dirt=.6),
        metal=sk.fin('wg-metal', 0x6f7c86, sk.DETAIL_BRASS, smooth=.6, metal=.85, dirt=.5, wear=.5),
        gold=sk.fin('wg-gold', 0xc89a4a, sk.DETAIL_BRASS, smooth=.65, metal=.9, dirt=.4, wear=.5),
        sand=sk.fin('wg-sand', 0xdcb27c, smooth=.08, dirt=.15),
        cliff1=sk.fin('wg-cliff1', 0xc0784a, sk.DETAIL_CARVED, smooth=.06, dirt=.4),
        cliff2=sk.fin('wg-cliff2', 0xa65f3b, sk.DETAIL_CARVED, smooth=.06, dirt=.4),
        cliff3=sk.fin('wg-cliff3', 0xd49a68, sk.DETAIL_CARVED, smooth=.06, dirt=.4),
        cliff4=sk.fin('wg-cliff4', 0x8a4c35, sk.DETAIL_CARVED, smooth=.06, dirt=.4),
        glyph=sk.fin('wg-glyph', 0xffffff, glow=1.0, dirt=0),
        lens=sk.fin('wg-lens', 0xffffff, glow=1.0, dirt=0),
        membrane=sk.fin('wg-membrane', 0xffffff, glow=1.0, dirt=0),
        sky=sk.fin('wg-sky', 0xffffff, glow=1.0, dirt=0),
    )
    fine = T.i == 0
    g = Geo()
    cx, cy, cz = WG_RING
    # ---- the plaza: rings of flagstones round the hero mark, some lost to sand, a dark inlay ring with a glow channel ----
    pc = (0.0, 0.0)
    rings = [(.0, .9)] + [(.9 + 1.0 * k, 1.9 + 1.0 * k) for k in range(10)] if fine else [(0, 3.0), (3.0, 6.5), (6.5, 10.9)]
    inlay_glow = Geo()
    for ri, (r0, r1) in enumerate(rings):
        if r1 <= .95:
            disc(g, V(pc[0], .012, pc[1]), r1 - .03, F['stone'], 12 if fine else 8)
            continue
        nst = max(6, int(TAU * (r0 + r1) / 2 / (1.15 if fine else 3.0)))
        is_inlay = fine and abs((r0 + r1) / 2 - 1.4) < .55
        for k in range(nst):
            if fine and hash01('miss', ri, k) < (.08 if r0 > 6 else .02) and not is_inlay:
                continue
            a0 = TAU * k / nst + .006 * TAU + ri * .37
            a1 = TAU * (k + 1) / nst - .006 * TAU + ri * .37
            dy = .012 + (.012 * (hash01('h', ri, k) - .5) if fine else 0)
            gap = .025 if fine else 0
            pts = []
            for rr in (r0 + gap, r1 - gap):
                for aa in ((a0, a1) if rr == r0 + gap else (a1, a0)):
                    pts.append(V(pc[0] + math.cos(aa) * rr, dy, pc[1] + math.sin(aa) * rr))
            f = F['inlay'] if is_inlay else (F['flag'] if hash01('f', ri, k) > .4 else F['flag2'])
            g.face(pts, f, normal=(0, 1, 0))
            if fine:
                # each stone's sides down into the bed
                for e in range(4):
                    p0, p1 = pts[e], pts[(e + 1) % 4]
                    q0, q1 = V(p0[0], -.08, p0[2]), V(p1[0], -.08, p1[2])
                    mid = mul(_add(p0, p1), .5)
                    out = _norm((mid[0] - pc[0], 0, mid[2] - pc[1])) if e % 2 == 1 else _norm((-(mid[2] - pc[1]), 0, mid[0] - pc[0]))
                    g.face((p0, p1, q1, q0), f, normal=out if e != 0 else mul(out, -1) if False else out)
    # the inlay's glow channel: a thin circle with glyph ticks
    disc(inlay_glow, V(pc[0], .021, pc[1]), 2.0, F['glyph'], 64 if fine else 24, r_in=1.94)
    if fine:
        for k in range(24):
            a = TAU * k / 24
            p = V(pc[0] + math.cos(a) * 2.22, .021, pc[1] + math.sin(a) * 2.22)
            t = (-math.sin(a), 0, math.cos(a))
            r_ = (math.cos(a), 0, math.sin(a))
            L = .14 if k % 3 else .26
            inlay_glow.face((_add(_add(p, t, -.025), r_, -L / 2), _add(_add(p, t, .025), r_, -L / 2), _add(_add(p, t, .025), r_, L / 2), _add(_add(p, t, -.025), r_, L / 2)), F['glyph'], normal=(0, 1, 0))
    # ---- the sand round it ----
    heightfield(g, -40, 40, -40, 34, 40 if fine else 12, 37 if fine else 11, sand_h, lambda x, y, z, n: F['sand'], smooth=True)
    for k, (x, z, s) in enumerate(((-8.5, 4.0, 1.3), (9.0, 2.0, 1.1), (-6.0, -8.5, 1.6), (7.2, -9.0, 1.4), (-11.0, -2.0, 1.0), (10.5, 7.0, .9))):
        mound(g, V(x, -.05, z), (2.2 * s, .45 * s, 1.4 * s), F['sand'], 90 + k, 4 if fine else 3, 10 if fine else 6, .08, .7)
    # ---- the dais: three half-round steps at the gate's foot ----
    for k, r in enumerate(WG_DAIS[::-1]):
        y1 = WG_STEP * (k + 1)
        n = 28 if fine else 10
        pts_top = [V(math.cos(PI + PI * j / n) * r, y1, WG_DAIS_Z + math.sin(PI + PI * j / n) * -r) for j in range(n + 1)]
        # the step's top (a half disc toward +z, closed behind by the gate's plinth line z = cz + 1.6 - ...)
        back = V(0, y1, cz - .8)
        for j in range(n):
            g.face((back, pts_top[j], pts_top[j + 1]), F['stone2'], normal=(0, 1, 0))
        for j in range(n):
            a, b = pts_top[j], pts_top[j + 1]
            mid = mul(_add(a, b), .5)
            out = _norm((mid[0], 0, mid[2] - WG_DAIS_Z))
            g.face((a, b, V(b[0], y1 - WG_STEP - .01, b[2]), V(a[0], y1 - WG_STEP - .01, a[2])), F['stone2'], normal=out)
    top = WG_STEP * 3
    sk.box(g, -4.6, 4.6, -.1, top, cz - 1.6, cz + .8, F['stone2'], T, .04)
    # ---- the ring: carved stone with bands and a channel, standing on two feet ----
    ringg = Geo()
    nr = 72 if fine else 24
    d0, d1 = -.5, .5
    prof = [(WG_R0, d0), (WG_R1, d0), (WG_R1 + .05, -.3), (WG_R1 + .05, .3), (WG_R1, d1), (WG_R0 + .25, d1), (WG_R0 + .25, d1 - .06),
            (WG_R0 + .1, d1 - .06), (WG_R0 + .1, d1), (WG_R0, d1), (WG_R0 - .04, .25), (WG_R0 - .04, -.25), (WG_R0, d0)]
    for k in range(len(prof) - 1):
        (r0, z0), (r1, z1) = prof[k], prof[k + 1]
        for j in range(nr):
            a0, a1 = TAU * j / nr, TAU * (j + 1) / nr
            p = [V(math.cos(a0) * r0, math.sin(a0) * r0, z0), V(math.cos(a1) * r0, math.sin(a1) * r0, z0),
                 V(math.cos(a1) * r1, math.sin(a1) * r1, z1), V(math.cos(a0) * r1, math.sin(a0) * r1, z1)]
            am = (a0 + a1) / 2
            dz, dr = z1 - z0, r1 - r0
            nrm = _norm((math.cos(am) * dz, math.sin(am) * dz, -dr))
            f = F['stone'] if (j // (nr // 18)) % 2 == 0 or k not in (0, 4) else F['stone2']
            ringg.face(p, f, normal=nrm)
    # carved keystone housings round the face, a gold band in the channel's lip
    for k in range(WG_KEYS):
        a = math.radians(WG_KEY_DEG[k])
        hx, hy = math.cos(a) * WG_KEY_R, math.sin(a) * WG_KEY_R
        house = Geo()
        sk.box(house, -.24, .24, -.3, .3, 0, .14, F['stone2'], None, .03, cell=0)
        sk.box(house, -.16, .16, -.22, .22, .1, .16, F['dark'], None, .01, cell=0)
        place(ringg, house, M((hx, hy, d1), roll=a - PI / 2))
    channels = Geo()
    disc(channels, V(0, 0, d1 + .002), WG_R0 + .24, F['glyph'], nr, normal=(0, 0, 1), r_in=WG_R0 + .11)
    # the three fins: tapered blades of stone standing out of the ring, a gold edge, a lit seam
    for k, deg in enumerate(WG_FIN_DEG):
        a = math.radians(deg)
        fin_ = Geo()
        r0_, r1_ = WG_R1 - .1, WG_R1 + 1.15
        prof_ = [(-.34, r0_), (.34, r0_), (.13, r1_), (-.13, r1_)]
        for zz, nz_ in ((-.32, -1), (.32, 1)):
            fin_.face([V(u, r, zz) for u, r in prof_], F['stone'], normal=(0, 0, nz_))
        for e in range(4):
            (u0, r0e), (u1, r1e) = prof_[e], prof_[(e + 1) % 4]
            nrm = _norm((r1e - r0e, -(u1 - u0), 0))
            fin_.face((V(u0, r0e, -.32), V(u1, r1e, -.32), V(u1, r1e, .32), V(u0, r0e, .32)), F['gold'] if e == 2 else F['stone2'], normal=nrm)
        seam = Geo()
        seam.face((V(-.04, r0_ + .2, .325), V(.04, r0_ + .2, .325), V(.03, r1_ - .12, .325), V(-.03, r1_ - .12, .325)), F['glyph'], normal=(0, 0, 1))
        mf = M((0, 0, 0), roll=a - PI / 2)
        place(ringg, fin_, mf)
        place(channels, seam, mf)
    place(g, ringg, M(WG_RING))
    ch = Geo()
    place(ch, channels, M(WG_RING))
    # the feet: two raked buttresses and the plinth under the ring
    for sx in (-1, 1):
        foot = Geo()
        sk.box(foot, -.5, .5, 0, 1.6, -.6, .6, F['stone2'], T, .05)
        place(g, foot, M((sx * 2.7, top, cz), roll=sx * .25))
    sk.box(g, -2.0, 2.0, top, top + .35, cz - .7, cz + .7, F['stone'], T, .04)
    # ---- the inner ring: turns about the axis; carved, its glyph sockets on the glow ----
    inner = Geo()
    ni = 64 if fine else 20
    iprof = [(WG_I0, -.4), (WG_I1, -.4), (WG_I1, .36), (WG_I0 + .08, .42), (WG_I0, .36), (WG_I0, -.4)]
    for k in range(len(iprof) - 1):
        (r0, z0), (r1, z1) = iprof[k], iprof[k + 1]
        for j in range(ni):
            a0, a1 = TAU * j / ni, TAU * (j + 1) / ni
            p = [V(math.cos(a0) * r0, math.sin(a0) * r0, z0), V(math.cos(a1) * r0, math.sin(a1) * r0, z0),
                 V(math.cos(a1) * r1, math.sin(a1) * r1, z1), V(math.cos(a0) * r1, math.sin(a0) * r1, z1)]
            am = (a0 + a1) / 2
            nrm = _norm((math.cos(am) * (z1 - z0), math.sin(am) * (z1 - z0), -(r1 - r0)))
            inner.face(p, F['metal'] if k in (1, 3) else F['dark'], normal=nrm)
    nglyph = 36 if fine else 18
    for k in range(nglyph):
        a = TAU * (k + .5) / nglyph
        rr = (WG_I0 + WG_I1) / 2
        c = V(math.cos(a) * rr, math.sin(a) * rr, .37)
        tng = (-math.sin(a), math.cos(a), 0)
        rad = (math.cos(a), math.sin(a), 0)
        shape = int(hash01('glyph', k) * 4)
        bars = [((-.05, -.13), (.05, .13))] if shape == 0 else [((-.09, -.03), (.09, .03)), ((-.03, -.12), (.03, .12))] if shape == 1 \
            else [((-.1, -.1), (-.04, .1)), ((.04, -.1), (.1, .1))] if shape == 2 else [((-.1, .06), (.1, .12)), ((-.03, -.12), (.03, .06))]
        for (u0, v0), (u1, v1) in bars:
            q = [_add(_add(c, tng, u0), rad, v0), _add(_add(c, tng, u1), rad, v0), _add(_add(c, tng, u1), rad, v1), _add(_add(c, tng, u0), rad, v1)]
            inner.face(q, F['glyph'], normal=(0, 0, 1))
    # ---- the keystone lens, the membrane ----
    lens = Geo()
    lens.face((V(-.13, -.19, 0), V(.13, -.19, 0), V(.13, .19, 0), V(-.13, .19, 0)), F['lens'], normal=(0, 0, 1))
    if fine:
        lens.face((V(-.07, .19, 0), V(.07, .19, 0), V(0, .28, 0)), F['lens'], normal=(0, 0, 1))
    mem = Geo()
    disc(mem, V(0, 0, 0), 1.0, F['membrane'], 48 if fine else 16, normal=(0, 0, 1))
    if fine:
        disc(mem, V(0, 0, 0), .55, F['membrane'], 32, normal=(0, 0, 1))
    mem.extend(flipped(mem))

    def membrane_fn(P):
        r = np.clip(np.hypot(P[:, 0], P[:, 1]), 0, 1)
        return mix_hex(0xf4fbff, 0x5f7dff, r ** .7) * (1 - .55 * r ** 3)[:, None]
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
    # ---- two obelisks framing the gate, their glyph bands lit ----
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
        mo = M((sx * 4.9, top - .02, cz + .2), yaw=.2 * sx)
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
    nb = 22 if fine else 7
    P = []
    for i in range(13 if fine else 4):
        y = -1 + 40 * i / (12 if fine else 3)
        row = []
        for j in range(nb + 1):
            x = -24 + 48 * j / nb
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
    dome(sky, 185, F['sky'], 10 if fine else 5, 32 if fine else 14, -16, 90)
    skyfn = sky_fn(0x2c2f6a, 0xf08a52, WG_SUN, 0xffb46a, .8, below=0x6a4a58)
    return [
        Piece('waygate/plaza', g, True, (0.0, top), seed=7),
        Piece('waygate/canyon', canyon, False, (-1.0,), seed=8),
        Piece('waygate/inner', inner, False, (-50.0,), preview=[M(WG_RING)]),
        Piece('waygate/lens', lens, False, (-50.0,), preview=[M((cx + math.cos(math.radians(d)) * WG_KEY_R, cy + math.sin(math.radians(d)) * WG_KEY_R, cz + .66), roll=math.radians(d) - PI / 2) for d in WG_KEY_DEG]),
        Piece('waygate/channels', ch, False),
        Piece('waygate/inlay', inlay_glow, False),
        Piece('waygate/obglow', obg, False),
        Piece('waygate/membrane', mem, False, (-50.0,), recolour=({F['membrane']}, membrane_fn), preview=[M(WG_RING, s=2.5)]),
        Piece('waygate/sky', sky, False, (-500.0,), recolour=({F['sky']}, skyfn)),
    ]


VIEWS = [
    ('wg-hero', (0, 1.0, 4.4), (0, 1.6, -2), 44),
    ('wg-wide', (0, 2.2, 12), (0, 2.6, -5), 56),
    ('wg-side', (7.5, 2.5, 1.0), (0, 2.6, -4.5), 55),
    ('wg-high', (10, 14, 16), (0, 0, -4), 60),
]
