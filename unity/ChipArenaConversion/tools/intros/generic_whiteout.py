"""Whiteout's set: a polar outpost at night in a blizzard (docs/INTROS-GENERIC.md "Whiteout"). Unity space; the pilot kneels at the
origin facing +z. Pieces (generic_sets.py writes them):
  whiteout/ground    the snowfield, its drifts and the far plain and ridges (world frame)
  whiteout/outpost   the hut, the floodlight mast, the radio mast and its guys, the drums, the flag stakes, the sled, the ice blocks
                     (world frame)
  whiteout/windows   the hut's lit windows (glow; world frame)     whiteout/lamps   the door lamp (glow; world frame)
  whiteout/flood     the floodlights' four lenses (glow; world frame)
  whiteout/beacon    the radio mast's red beacon (glow; world frame)
  whiteout/thaw      the wet ground the heat pulse leaves: a disc of radius 1 on the floor at the origin (Core scales it)
  whiteout/aurora    the aurora's curtains (glow; world frame)  whiteout/sky   the night sky dome (glow)
"""
import math

import numpy as np
import stagekit as sk
from stagekit import Geo, _add, _sub, _norm
from generic_kit import (PI, TAU, V, mul, M, place, noise2, heightfield, polar_field, rockball, mound, dome, disc, lattice_tower,
                         flipped, mix_hex, Piece, smoothstep)

WO_HUT = (-7.2, 0.0, -10.4)
WO_HUT_YAW = .46
WO_FLOOD = (5.6, 0.0, -7.4)
WO_RADIO = (-15.5, 0.0, -21.0)
WO_RADIO_H = 17.0


def ground_h(x, z):
    r = math.hypot(x, z)
    y = .55 * (noise2(x, z, .045, 21) - .5) + .28 * (noise2(x, z, .14, 22) - .5)
    # wind-carved sastrugi: low ridges across the wind
    y += .09 * math.sin(.85 * x + .38 * z + 5 * noise2(x, z, .06, 23)) * smoothstep(3, 8, r)
    # drifts banked against the hut's windward side
    hx, hz = WO_HUT[0], WO_HUT[2]
    y += .9 * math.exp(-((x - hx - 2.6) ** 2 + (z - hz + 1.5) ** 2) / 6.0)
    y *= smoothstep(3.3, 6.8, r)
    return y + .006


def build(T):
    F = dict(
        snow=sk.fin('wo-snow', 0xe9eff7, smooth=.42, dirt=.04),
        far=sk.fin('wo-far', 0xdde6f0, smooth=.3, dirt=0),
        ice=sk.fin('wo-ice', 0xa8d2ea, smooth=.85, dirt=.05),
        quonset=sk.fin('wo-quonset', 0x9ba4a9, sk.DETAIL_IRON, smooth=.4, metal=.55, dirt=.6, wear=.3),
        wall=sk.fin('wo-wall', 0xc4482e, smooth=.3, dirt=.5, wear=.35),
        door=sk.fin('wo-door', 0x5e2a22, smooth=.3, dirt=.6),
        frame=sk.fin('wo-frame', 0x2c3036, smooth=.3, metal=.4, dirt=.5),
        steel=sk.fin('wo-steel', 0x7d858d, sk.DETAIL_IRON, smooth=.4, metal=.7, dirt=.5, wear=.3),
        yellow=sk.fin('wo-yellow', 0xe4ad2a, smooth=.3, dirt=.5, wear=.4),
        drumb=sk.fin('wo-drumb', 0x2f5d8a, smooth=.35, metal=.35, dirt=.6, wear=.4),
        drumr=sk.fin('wo-drumr', 0xb83a2a, smooth=.35, metal=.35, dirt=.6, wear=.4),
        flag=sk.fin('wo-flag', 0xf26a1b, sk.DETAIL_CLOTH, smooth=.15, dirt=.2),
        stake=sk.fin('wo-stake', 0x3a3530, sk.DETAIL_WOOD, smooth=.2, dirt=.5, param=1),
        wood=sk.fin('wo-wood', 0x8a6a46, sk.DETAIL_WOOD, smooth=.2, dirt=.7, param=2),
        crate=sk.fin('wo-crate', 0x5f6538, smooth=.25, dirt=.8, wear=.3),
        tarp=sk.fin('wo-tarp', 0x2e4a5e, sk.DETAIL_CLOTH, smooth=.2, dirt=.6),
        wet=sk.fin('wo-wet', 0x26313d, smooth=.92, dirt=0),
        window=sk.fin('wo-window', 0xffbf6a, glow=1.0, dirt=0),
        lamp=sk.fin('wo-lamp', 0xfff0d4, glow=1.0, dirt=0),
        flood=sk.fin('wo-flood', 0xf2f6ff, glow=1.0, dirt=0),
        beacon=sk.fin('wo-beacon', 0xff3524, glow=1.0, dirt=0),
        aurora=sk.fin('wo-aurora', 0xffffff, glow=1.0, dirt=0),
        sky=sk.fin('wo-sky', 0xffffff, glow=1.0, dirt=0),
    )
    fine = T.i == 0
    # ---- the snowfield, near and far ----
    ground = Geo()
    n = 48 if fine else 14
    heightfield(ground, -58, 58, -58, 58, n, n, ground_h, lambda x, y, z, nn: F['snow'], smooth=True)

    def far_h(r, t, x, z):
        y = .6 * (noise2(x, z, .03, 31) - .5) + ground_h(x * 58 / max(r, 1), z * 58 / max(r, 1)) * (1 - smoothstep(58, 70, r))
        ridge = 22 * max(0.0, noise2(math.cos(t) * 40, math.sin(t) * 40, .05, 33) - .35) * smoothstep(105, 165, r)
        return y + ridge

    polar_field(ground, (0, 0), 57.5, 182, 14 if fine else 6, 72 if fine else 28, far_h,
                lambda x, y, z, nn: F['far'] if y > 2.0 else F['snow'], smooth=True, rpow=1.3)

    o = Geo()
    # ---- the quonset hut: a corrugated half-round, red end walls, a door, two lit windows, a caged lamp, snow on its back ----
    hut, glow_win, glow_lamp = Geo(), Geo(), Geo()
    L, R = 7.0, 2.3
    na = 18 if fine else 7
    nz = 72 if fine else 6
    pts, nrm = [], []
    for i in range(nz + 1):
        z = -L / 2 + L * i / nz
        rip = .035 * math.sin(TAU * z / .36) if fine else 0.0
        slope = .035 * TAU / .36 * math.cos(TAU * z / .36) if fine else 0.0
        row, nrow = [], []
        for j in range(na + 1):
            a = PI * j / na
            rr = R + rip
            row.append(V(rr * math.cos(a), rr * math.sin(a), z))
            nrow.append(_norm((math.cos(a), math.sin(a), -slope)))
        pts.append(row)
        nrm.append(nrow)
    for i in range(nz):
        for j in range(na):
            a, b, c, d = pts[i][j], pts[i][j + 1], pts[i + 1][j + 1], pts[i + 1][j]
            hut.face((a, b, c, d), F['quonset'], normals=(nrm[i][j], nrm[i][j + 1], nrm[i + 1][j + 1], nrm[i + 1][j]))
    for zz, nz_ in ((L / 2, 1), (-L / 2, -1)):
        ring = [V(R * math.cos(PI * j / na), R * math.sin(PI * j / na), zz) for j in range(na + 1)]
        for j in range(na):
            hut.face((V(0, 0, zz), ring[j], ring[j + 1]), F['wall'], normal=(0, 0, nz_))
    zf = L / 2
    sk.box(hut, -.52, .52, 0, 2.02, zf, zf + .07, F['door'], T, .015)
    sk.box(hut, -.6, .6, 2.02, 2.1, zf, zf + .09, F['frame'], None, .01, cell=0)
    sk.cylinder(hut, V(.38, 1.0, zf + .07), V(.38, 1.0, zf + .14), .03, F['steel'], 6)
    for wx in (-1.15, 1.15):
        sk.box(hut, wx - .36, wx + .36, 1.12, 1.7, zf, zf + .05, F['frame'], None, .01, cell=0)
        glow_win.face((V(wx - .3, 1.18, zf + .052), V(wx + .3, 1.18, zf + .052), V(wx + .3, 1.64, zf + .052), V(wx - .3, 1.64, zf + .052)), F['window'], normal=(0, 0, 1))
        sk.box(hut, wx - .015, wx + .015, 1.18, 1.64, zf + .05, zf + .07, F['frame'], None, 0, cell=0)
    # the caged lamp over the door
    sk.box(hut, -.05, .05, 2.22, 2.3, zf, zf + .3, F['steel'], None, .005, cell=0)
    sk.lathe(glow_lamp, [(.001, -.12), (.07, -.1), (.09, -.03), (.08, .03), (.05, .06)], V(0, 2.14, zf + .3), F['lamp'], 10 if fine else 6)
    if fine:
        for k in range(4):
            a = TAU * k / 4 + PI / 4
            sk.cylinder(hut, V(.1 * math.cos(a), 2.0, zf + .3 + .1 * math.sin(a)), V(.1 * math.cos(a), 2.24, zf + .3 + .1 * math.sin(a)), .008, F['steel'], 3, caps=False)
    # snow on its back, and drifted against its foot
    cap = []
    for i in range(nz // 4 + 1 if fine else 3):
        z = -L / 2 - .05 + (L + .1) * i / (nz // 4 if fine else 2)
        row = []
        for j in range(9 if fine else 4):
            a = .55 + (PI - 1.1) * j / (8 if fine else 3)
            rr = R + .09 + .07 * noise2(z, a * 3, .9, 41)
            row.append(V(rr * math.cos(a), rr * math.sin(a), z))
        cap.append(row)
    for i in range(len(cap) - 1):
        for j in range(len(cap[0]) - 1):
            a, b, c, d = cap[i][j], cap[i][j + 1], cap[i + 1][j + 1], cap[i + 1][j]
            mid = mul(_add(_add(a, b), _add(c, d)), .25)
            hut.face((a, b, c, d), F['snow'], normal=_norm((mid[0], mid[1], 0)))
    for k, (x, z, s) in enumerate(((R + .3, 1.2, 1.0), (R + .2, -1.6, 1.2), (-R - .3, -.4, 1.1), (-R - .2, 2.2, .8))):
        mound(hut, V(x, 0, z), (.9 * s, .55 * s, 1.6 * s), F['snow'], 50 + k, 4 if fine else 3, 10 if fine else 6, .1, .6)
    mh = M(WO_HUT, yaw=WO_HUT_YAW)
    place(o, hut, mh)
    windows = Geo()
    place(windows, glow_win, mh)
    lamps = Geo()
    place(lamps, glow_lamp, mh)

    # ---- the floodlight mast: four lamps on a crossbar, aimed at the pilot ----
    fx_, fy_, fz_ = WO_FLOOD
    sk.box(o, fx_ - .7, fx_ + .7, -.4, .1, fz_ - .7, fz_ + .7, F['steel'], T, .02)
    top = lattice_tower(o, (fx_, .1, fz_), 8.6, .62, .32, 1.1, T, F['steel'], legs=4, yaw=PI / 4, leg_r=.05, tie_r=.025)
    ty = 8.7
    aim = math.atan2(-fx_, -fz_)
    flood = Geo()
    bar = Geo()
    sk.box(bar, -1.3, 1.3, -.06, .06, -.06, .06, F['steel'], None, .01, cell=0)
    sk.box(bar, -.55, .55, -.5, -.06, -.04, .04, F['steel'], None, .01, cell=0)
    lens = Geo()
    for k in range(4):
        x = -1.05 + .7 * k
        head = Geo()
        sk.box(head, -.24, .24, -.18, .18, -.22, .1, F['frame'], None, .02, cell=0)
        sk.box(head, -.27, .27, -.21, .21, .1, .16, F['steel'], None, .015, cell=0)
        lg = Geo()
        lg.face((V(-.22, -.16, .165), V(.22, -.16, .165), V(.22, .16, .165), V(-.22, .16, .165)), F['flood'], normal=(0, 0, 1))
        place(bar, head, M((x, .26, 0), pitch=.32))
        place(lens, lg, M((x, .26, 0), pitch=.32))
    mt = M((fx_, ty, fz_), yaw=aim)
    place(o, bar, mt)
    place(flood, lens, mt)

    # ---- the radio mast, its guy wires and its beacon ----
    rx, ry, rz = WO_RADIO
    lattice_tower(o, (rx, 0, rz), WO_RADIO_H, .42, .2, 1.4, T, F['steel'], legs=3, yaw=.2,
                  band=lambda i: F['wall'] if i % 2 == 0 else F['steel'], leg_r=.04, tie_r=.02)
    beacon = Geo()
    sk.lathe(beacon, [(.1, 0), (.11, .08), (.08, .16), (.001, .2)], V(rx, WO_RADIO_H + .05, rz), F['beacon'], 10 if fine else 6)
    sk.cylinder(o, V(rx, WO_RADIO_H, rz), V(rx, WO_RADIO_H + .06, rz), .2, F['steel'], 8 if fine else 5)
    for k in range(3):
        a = .2 + TAU * k / 3 + PI / 3
        ax, az = rx + 10 * math.sin(a), rz + 10 * math.cos(a)
        sk.box(o, ax - .25, ax + .25, -.2, .25, az - .25, az + .25, F['steel'], None, .02, cell=0)
        for h in (8.0, 15.0):
            sk.cylinder(o, V(rx, h, rz), V(ax, .25, az), .012, F['steel'], 3 if fine else 3, caps=False)

    # ---- fuel drums, flag stakes, the sled, ice blocks ----
    drum = Geo()
    prof = [(.001, 0), (.29, 0), (.3, .02), (.3, .28), (.31, .3), (.3, .32), (.3, .56), (.31, .58), (.3, .6), (.3, .86), (.29, .88), (.001, .88)]
    sk.lathe(drum, prof, (0, 0, 0), F['drumb'], 14 if fine else 7)
    drumr = Geo()
    sk.lathe(drumr, prof, (0, 0, 0), F['drumr'], 14 if fine else 7)
    snowcap = Geo()
    disc(snowcap, V(0, .885, 0), .27, F['snow'], 10 if fine else 6)
    for k, (x, z, lie, red) in enumerate(((-2.6, -6.8, 0, 0), (-2.0, -7.1, 0, 1), (-2.35, -7.6, 0, 0), (-3.1, -7.4, 0, 1), (-1.5, -6.4, 1, 0),
                                            (-2.9, -6.2, 0, 0))):
        y = ground_h(x, z) - .03
        d = drumr if red else drum
        if lie:
            place(o, d, M((x, y + .3, z), yaw=.7, roll=PI / 2))
        else:
            place(o, d, M((x, y, z), yaw=k))
            place(o, snowcap, M((x, y, z), yaw=k))
    for k in range(9):
        u = k / 8.0
        x = -3.4 - 6.2 * u + 1.2 * math.sin(u * 3)
        z = -4.6 + 12.0 * u
        y = ground_h(x, z) - .2
        sk.cylinder(o, V(x, y, z), V(x + .04, y + 1.85, z), .016, F['stake'], 5 if fine else 3)
        f = Geo()
        f.face((V(0, 0, 0), V(.26, -.03, 0), V(.27, -.2, 0), V(.01, -.18, 0)), F['flag'], normal=(0, 0, 1))
        f.extend(flipped(f))
        place(o, f, M((x + .03, y + 1.82, z), yaw=1.9 + .2 * math.sin(k)))
    sled = Geo()
    for sx in (-.42, .42):
        path = [V(sx, .04, -1.3), V(sx, .02, 0), V(sx, .04, 1.1), V(sx, .22, 1.45), V(sx, .38, 1.5)]
        sk.sweep(sled, sk.rect_section(.05, .04), sk.catmull(path, 3), F['steel'])
        for zz in (-1.0, -.2, .6):
            sk.box(sled, sx - .02, sx + .02, .04, .32, zz - .02, zz + .02, F['wood'], None, .005, cell=0)
    for zz in range(9):
        z0 = -1.15 + zz * .26
        sk.box(sled, -.5, .5, .32, .35, z0, z0 + .18, F['wood'], None, .005, cell=0)
    sk.box(sled, -.38, .38, .35, .85, -1.0, .1, F['crate'], T, .03)
    mound(sled, V(0, .35, .55), (.4, .32, .42), F['tarp'], 61, 4, 8, .15, .7)
    for zz in (-.75, -.25):
        sk.box(sled, -.4, .4, .84, .87, zz - .03, zz + .03, F['frame'], None, 0, cell=0)
    sx_, sz_ = 3.4, -3.8
    place(o, sled, M((sx_, ground_h(sx_, sz_) - .02, sz_), yaw=.85))
    for k, (x, z, s, yaw, tilt) in enumerate(((-6.4, -1.4, 1.0, .3, .25), (-7.6, .4, .7, 1.2, -.35), (-5.3, 1.6, .55, 2.2, .45), (-8.9, -2.6, 1.3, .8, .15),
                                               (-6.9, 2.9, .5, 3.0, -.2), (-4.9, -2.9, .45, 1.6, .3), (7.6, -1.0, .8, .4, .2))):
        ice = Geo()
        rockball(ice, V(0, 0, 0), (.9 * s, .7 * s, .6 * s), F['ice'], 70 + k, 3, 5, .3, .3)
        place(o, ice, M((x, ground_h(x, z) + .1 * s, z), yaw=yaw, roll=tilt))

    # ---- the thaw (a wet disc, scaled by Core) ----
    thaw = Geo()
    nn = 32 if fine else 12
    pts = []
    for k in range(nn):
        a = TAU * k / nn
        rr = 1.0 + .08 * math.sin(a * 5 + 1) + .05 * math.sin(a * 9)
        pts.append(V(rr * math.cos(a), .0, rr * math.sin(a)))
    for k in range(nn):
        thaw.face((V(0, 0, 0), pts[k], pts[(k + 1) % nn]), F['wet'], normal=(0, 1, 0))

    # ---- the aurora's curtains, folding in the sky behind ----
    aur = Geo()
    for c, (a0, a1, d, y0, h, seed) in enumerate(((-2.35, -1.45, 150, 26, 44, 1), (-1.75, -.85, 165, 34, 52, 2), (-2.9, -2.25, 140, 30, 36, 3))):
        cols = 44 if fine else 14
        rows = 5 if fine else 3
        P = []
        for i in range(rows + 1):
            v = i / rows
            row = []
            for j in range(cols + 1):
                u = j / cols
                a = a0 + (a1 - a0) * u
                dd = d + 9 * math.sin(u * 9 + seed) + 4 * math.sin(u * 23 + seed * 2)
                row.append(V(math.sin(a) * dd, y0 + h * v + 6 * math.sin(u * 7 + seed) * v, math.cos(a) * dd))
            P.append(row)
        for i in range(rows):
            for j in range(cols):
                a, b, cc, dd_ = P[i][j], P[i][j + 1], P[i + 1][j + 1], P[i + 1][j]
                mid = mul(_add(_add(a, b), _add(cc, dd_)), .25)
                aur.face((a, b, cc, dd_), F['aurora'], normal=_norm((-mid[0], 0, -mid[2])))

    def aurora_fn(P):
        y = P[:, 1]
        az = np.arctan2(P[:, 0], P[:, 2])
        # each curtain's own height band: bright green low, teal, violet at the top, black at both edges (the glow adds to the sky)
        lo = np.where(az > -1.5, 34, np.where(az > -2.3, 26, 30)).astype(np.float64)
        hh = np.where(az > -1.5, 52, np.where(az > -2.3, 44, 36)).astype(np.float64)
        v = np.clip((y - lo) / hh, 0, 1)
        col = mix_hex(0x38ff9a, 0x2fd6c8, np.clip(v / .45, 0, 1))
        col = col * (v < .45)[:, None] + mix_hex(0x2fd6c8, 0x8f5cff, np.clip((v - .45) / .55, 0, 1)) * (v >= .45)[:, None]
        edge = np.clip(v / .12, 0, 1) * np.clip((1 - v) / .5, 0, 1) ** 1.4
        flick = .75 + .25 * np.sin(az * 31)
        return col * (edge * flick)[:, None]

    sky = Geo()
    dome(sky, 185, F['sky'], 10 if fine else 5, 32 if fine else 14, -16, 90)

    def sky_night(P):
        d = P / np.maximum(np.linalg.norm(P, axis=1, keepdims=True), 1e-9)
        up = np.clip(d[:, 1], 0, 1) ** .5
        return mix_hex(0x1c2a44, 0x050913, up)

    return [
        Piece('whiteout/ground', ground, False, (-50.0,), seed=3),
        Piece('whiteout/outpost', o, True, (0.0,), seed=4),
        Piece('whiteout/windows', windows, False),
        Piece('whiteout/lamps', lamps, False),
        Piece('whiteout/flood', flood, False),
        Piece('whiteout/beacon', beacon, False),
        Piece('whiteout/thaw', thaw, False, (-50.0,), preview=[M((0, .02, 0), s=2.6)]),
        Piece('whiteout/aurora', aur, False, (-500.0,), recolour=({F['aurora']}, aurora_fn)),
        Piece('whiteout/sky', sky, False, (-500.0,), recolour=({F['sky']}, sky_night)),
    ]


VIEWS = [
    ('wo-hero', (1.2, .7, 4.2), (0, .9, 0), 42),
    ('wo-wide', (3, 2.2, 9), (-2, 1.5, -8), 60),
    ('wo-sky', (1, 1.2, 6), (-20, 30, -120), 70),
    ('wo-high', (8, 18, 18), (-3, 0, -8), 60),
]
