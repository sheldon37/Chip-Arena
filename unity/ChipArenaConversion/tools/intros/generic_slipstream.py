"""Slipstream's set: an underground maglev station and its train (docs/INTROS-GENERIC.md "Slipstream"). Unity space; the hero mark
(the origin) is on the platform, the pilot facing +z; the track runs along x behind it (its centre line ST_TRACK_Z), the train
arriving from +x and leaving toward -x. Pieces (generic_sets.py writes them):
  slipstream/hall     the platform, the trackbed and guideway, the walls, the vault and its ribs, the columns, the benches, the
                      signs' housings, the end walls with their tunnel mouths (world frame)
  slipstream/lights   the vault's light lines, the lit ad panels, the signs' faces (glow; world frame)
  slipstream/edge     the platform edge's light line (glow; world frame)
  slipstream/board    the departure board's flaps (glow; world frame)
  slipstream/tunnel   the tunnel beyond the +x mouth, its ribs and light strips (world frame); drawn again turned half round about
                      (0, 0, ST_TRACK_Z) for the -x tunnel
  slipstream/car      a middle car, centred on its own origin (x along the track, y 0 the platform's level, z 0 the track's centre)
  slipstream/nose     the lead car, its nose toward +x local, in the same frame (Core turns it to lead toward -x)
"""
import math

import stagekit as sk
from stagekit import Geo, _add, _sub, _norm
import numpy as np
from generic_kit import (PI, TAU, V, mul, M, place, lerp, disc, flipped, Piece, mix_hex)

ST_ADS = (-22.0, -9.0, -4.6, 4.6, 9.0, 22.0)   # the ad panels' centres (x) on the far wall
# each ad's two colours (a wordless poster: a gradient, a bright product disc, two headline bars, a logo block)
ST_AD_PAL = ((0xff7a4a, 0xffc36a), (0x5ac8ff, 0x8a6bff), (0xffd45a, 0xff7aa8), (0xff7a4a, 0x6affc8), (0xffd45a, 0x5ac8ff),
             (0x5ac8ff, 0xc8f0ff))

ST_TRACK_Z = -2.95     # the guideway's centre line
ST_EDGE_Z = -1.25      # the platform's edge
ST_HALF = 34.0         # the hall's half length (its end walls at x = +-ST_HALF)
ST_CAR = 16.0          # a car's length
ST_ROOF = 2.95         # the cars' roof (Unity y)
# The tunnel's lining (z across, y up; Core NativeIntroSlipstream.TunnelCeiling mirrors it) and the mouths' lintel: tall enough for a
# pilot standing and running on the roof (its helmet near 4.8 m), the ribs hanging .11 m under the lining.
ST_TUNNEL = [(-5.3, -1.55), (-5.3, 3.4), (-4.9, 4.5), (-4.0, 5.15), (-2.95, 5.35), (-1.9, 5.15), (-1.0, 4.5), (-.6, 3.4), (-.6, -1.55)]
ST_MOUTH = 5.25
ST_SPRING, ST_RISE, ST_ZC, ST_SPAN = 5.0, 3.2, .9, 7.3   # the vault: springing, rise, centre z, half span


def vault_y(z):
    d = ST_SPAN - abs(z - ST_ZC)
    return ST_SPRING + sk.arch_height(ST_SPAN, ST_RISE, max(0.0, d))


def car_section(nose=False):
    """The car's outline (z across, y up; anticlockwise from the bottom centre's right) with each edge's role:
    'skirt', 'body', 'stripe', 'window', 'roof'."""
    pts = []
    # a superellipse half-width 1.55, half-height 1.65 about (y 1.3)
    n = 28
    for k in range(n):
        a = -PI / 2 + TAU * k / n
        c, s = math.cos(a), math.sin(a)
        z = 1.55 * (abs(c) ** (2 / 4.0)) * (1 if c >= 0 else -1)
        y = 1.3 + 1.65 * (abs(s) ** (2 / 4.0)) * (1 if s >= 0 else -1)
        pts.append((z, y))
    return pts


def edge_role(y0, y1):
    ym = (y0 + y1) / 2
    if ym < -.05:
        return 'skirt'
    if ym < .3:
        return 'stripe'
    if .95 <= ym <= 1.95:
        return 'window'
    if ym > 2.75:
        return 'roof'
    return 'body'


def build_car(T, F, nose):
    """A car from x -8 to +8 (local), its nose (when `nose`) tapering from x +3.6 to +8.4."""
    g = Geo()
    sec = car_section()
    fine = T.i == 0
    xs = []
    bays = 10
    for b in range(bays):
        x0 = -7.6 + 15.2 * b / bays
        xs += [x0, x0 + .14]
    xs = [-8.0] + xs + [7.6, 8.0]
    if nose:
        xs = [x for x in xs if x <= 3.6] + [3.6 + 4.8 * k / (8 if fine else 3) for k in range(1, (8 if fine else 3) + 1)]
    xs = sorted(set(round(x, 4) for x in xs))

    def scale_at(x):
        if not nose or x <= 3.6:
            return 1.0, 0.0
        u = (x - 3.6) / 4.8
        return max(.06, 1 - u ** 1.6 * .94), -.95 * u ** 2
    rows = []
    for x in xs:
        s, dy = scale_at(x)
        rows.append([V(x, 1.3 + (y - 1.3) * s + dy * (1 if y > 1.3 else .2), z * s) for z, y in sec])
    n = len(sec)
    for i in range(len(xs) - 1):
        xm = (xs[i] + xs[i + 1]) / 2
        mullion = (xs[i + 1] - xs[i]) < .2
        door = any(abs(xm - (-7.6 + 15.2 * (b + .5) / bays)) < .76 for b in (1, 8)) and not nose
        tip = nose and xm > 3.6
        for k in range(n):
            a, b = rows[i][k], rows[i][(k + 1) % n]
            c, d = rows[i + 1][(k + 1) % n], rows[i + 1][k]
            role = edge_role(sec[k][1], sec[(k + 1) % n][1])
            if role == 'window':
                f = F['frame'] if (mullion or door) else (F['glass'] if tip else F['window'])
                if tip and xm > 6.6:
                    f = F['body']
            elif role == 'stripe':
                f = F['stripe']
            elif role == 'skirt':
                f = F['skirt']
            elif role == 'roof':
                f = F['roof']
            else:
                f = F['door'] if door and sec[k][1] < 2.4 else F['body']
            if tip and role == 'roof' and xm > 4.4:
                f = F['glass']
            mid = mul(_add(_add(a, b), _add(c, d)), .25)
            out = _norm((0, mid[1] - 1.3, mid[2]))
            if tip:
                out = _norm(_add(out, (.6, 0, 0)))
            g.face((a, b, c, d), f, normal=out)
    # the ends: the gangway bellows (middle cars), the nose's tip and its back
    for x, nx in ((xs[0], -1), (xs[-1], 1)):
        if nose and nx > 0:
            ring = rows[-1]
            centre = mul(tuple(sum(p[j] for p in ring) for j in range(3)), 1 / len(ring))
            for k in range(n):
                g.face((centre, ring[k], ring[(k + 1) % n]), F['body'], normal=(1, 0, 0))
            continue
        sk.box(g, x - (.12 if nx > 0 else -.12), x, .0, 2.6, -1.0, 1.0, F['bellows'], None, .01, cell=0)
        ring = rows[0] if nx < 0 else rows[-1]
        centre = V(x, 1.3, 0)
        for k in range(n):
            g.face((centre, ring[k], ring[(k + 1) % n]), F['frame'], normal=(nx, 0, 0))
    # the magnet skirts, roof pods, the nose's headlights
    sk.box(g, -7.4 if not nose else -7.4, 7.4 if not nose else 3.4, -.62, -.3, -1.25, 1.25, F['skirt'], None, .03, cell=0)
    for x0 in ((-5.5, 2.5) if not nose else (-5.5,)):
        sk.box(g, x0, x0 + 3.0, ST_ROOF - .02, ST_ROOF + .26, -.8, .8, F['roof'], T, .08)
        if fine:
            for k in range(6):
                sk.box(g, x0 + .3 + k * .45, x0 + .5 + k * .45, ST_ROOF + .26, ST_ROOF + .3, -.6, .6, F['frame'], None, 0, cell=0)
    lamps = Geo()
    if nose:
        for zz in (-.75, .75):
            lamps.face((V(7.15, .55, zz - .2), V(7.15, .55, zz + .2), V(7.0, .75, zz + .22), V(7.0, .75, zz - .22)), F['head'], normal=(1, .3, 0))
        g.extend(lamps)
    return g


def build(T):
    F = dict(
        floor=sk.fin('ss-floor', 0x8e9399, 16, smooth=.55, dirt=.6, param=.6),
        tactile=sk.fin('ss-tactile', 0xe8c23a, smooth=.3, dirt=.5),
        concrete=sk.fin('ss-concrete', 0x7b7e82, sk.DETAIL_PLASTER, smooth=.15, dirt=1.0),
        dark=sk.fin('ss-dark', 0x2d3136, sk.DETAIL_PLASTER, smooth=.15, dirt=1.0),
        wall=sk.fin('ss-wall', 0xc9d2da, 16, smooth=.6, dirt=.5, param=.3),
        band=sk.fin('ss-band', 0x1f8f9a, smooth=.5, dirt=.3),
        vault=sk.fin('ss-vault', 0xd9dde0, sk.DETAIL_PLASTER, smooth=.2, dirt=.6),
        rib=sk.fin('ss-rib', 0x9aa1a8, sk.DETAIL_IRON, smooth=.45, metal=.6, dirt=.4),
        steel=sk.fin('ss-steel', 0x8d949b, sk.DETAIL_IRON, smooth=.5, metal=.75, dirt=.4, wear=.3),
        rail=sk.fin('ss-rail', 0xa7adb3, smooth=.7, metal=.9, dirt=.3),
        column=sk.fin('ss-column', 0xe1e4e6, smooth=.4, dirt=.4),
        wood=sk.fin('ss-wood', 0x9b6b3e, sk.DETAIL_WOOD, smooth=.35, dirt=.4, param=0),
        frame=sk.fin('ss-frame', 0x1f2328, smooth=.4, metal=.5, dirt=.3),
        body=sk.fin('ss-body', 0xeceff2, smooth=.65, metal=.25, dirt=.2),
        roof=sk.fin('ss-roof', 0x9ea6ad, smooth=.4, metal=.5, dirt=.4),
        stripe=sk.fin('ss-stripe', 0x16b6c8, smooth=.6, metal=.2, dirt=.2),
        skirt=sk.fin('ss-skirt', 0x34393f, smooth=.3, metal=.4, dirt=.6),
        door=sk.fin('ss-door', 0xd2d7dc, smooth=.6, metal=.3, dirt=.2),
        bellows=sk.fin('ss-bellows', 0x23262b, sk.DETAIL_CLOTH, smooth=.2, dirt=.4),
        glass=sk.fin('ss-glass', 0x0f1a24, smooth=.95, metal=.3, dirt=.05),
        window=sk.fin('ss-window', 0xd8f2ff, glow=1.0, dirt=0),
        head=sk.fin('ss-head', 0xffffff, glow=1.0, dirt=0),
        light=sk.fin('ss-light', 0xf4fbff, glow=1.0, dirt=0),
        ad1=sk.fin('ss-ad1', 0xff7a4a, glow=1.0, dirt=0),
        ad2=sk.fin('ss-ad2', 0x5ac8ff, glow=1.0, dirt=0),
        ad3=sk.fin('ss-ad3', 0xffd45a, glow=1.0, dirt=0),
        sign=sk.fin('ss-sign', 0xffffff, glow=1.0, dirt=0),
        edge=sk.fin('ss-edge', 0x9ff3ff, glow=1.0, dirt=0),
        flap=sk.fin('ss-flap', 0xffd98a, glow=1.0, dirt=0),
        strip=sk.fin('ss-strip', 0xbfe9ff, glow=1.0, dirt=0),
    )
    fine = T.i == 0
    H = ST_HALF
    g = Geo()
    lights = Geo()
    # ---- the platform: tiles, the tactile strip, its face down to the trackbed ----
    sk.box(g, -H, H, -1.55, .012, ST_EDGE_Z, 8.2, F['floor'], T, .02, skip=('-z',))
    g.face((V(-H, -1.55, ST_EDGE_Z), V(H, -1.55, ST_EDGE_Z), V(H, .012, ST_EDGE_Z), V(-H, .012, ST_EDGE_Z)), F['dark'], normal=(0, 0, -1))
    sk.box(g, -H, H, .012, .018, ST_EDGE_Z + .08, ST_EDGE_Z + .62, F['tactile'], None, 0, cell=4.0 if fine else 0)
    edge = Geo()
    edge.face((V(-H, .019, ST_EDGE_Z + .01), V(H, .019, ST_EDGE_Z + .01), V(H, .019, ST_EDGE_Z + .07), V(-H, .019, ST_EDGE_Z + .07)), F['edge'], normal=(0, 1, 0))
    # ---- the trackbed and the guideway ----
    tz = ST_TRACK_Z
    g.face((V(-H, -1.55, -6.4), V(H, -1.55, -6.4), V(H, -1.55, ST_EDGE_Z), V(-H, -1.55, ST_EDGE_Z)), F['dark'], normal=(0, 1, 0))
    sk.box(g, -H, H, -1.55, -.68, tz - .8, tz + .8, F['concrete'], T, .03)
    for zz in (tz - .72, tz + .72):
        sk.box(g, -H, H, -.68, -.52, zz - .08, zz + .08, F['rail'], None, .01, cell=0)
    # ---- the far wall: tiles, a coloured band, ad panels, the station's signs ----
    g.face((V(-H, -1.55, -6.4), V(H, -1.55, -6.4), V(H, ST_SPRING, -6.4), V(-H, ST_SPRING, -6.4)), F['wall'], normal=(0, 0, 1))
    sk.box(g, -H, H, 2.3, 2.62, -6.4, -6.34, F['band'], None, 0, cell=0)
    for k, x in enumerate((-22, -9, -4.6, 4.6, 9, 22)):
        sk.box(g, x - 1.55, x + 1.55, .45, 2.05, -6.4, -6.3, F['frame'], None, .01, cell=0)
        f = (F['ad1'], F['ad2'], F['ad3'], F['ad1'], F['ad3'], F['ad2'])[k]
        # the poster in a grid, so its colour can carry its design (ad_fn)
        nu, nv = (24, 12) if fine else (6, 3)
        for i in range(nu):
            for j in range(nv):
                a0, a1 = x - 1.45 + 2.9 * i / nu, x - 1.45 + 2.9 * (i + 1) / nu
                b0, b1 = .55 + 1.4 * j / nv, .55 + 1.4 * (j + 1) / nv
                lights.face((V(a0, b0, -6.29), V(a1, b0, -6.29), V(a1, b1, -6.29), V(a0, b1, -6.29)), f, normal=(0, 0, 1))
    board = Geo()
    sk.box(g, -2.6, 2.6, 2.95, 4.25, -6.4, -6.22, F['frame'], None, .02, cell=0)
    for r in range(4):
        y = 3.07 + r * .29
        for c in range(14 if fine else 7):
            x0 = -2.45 + c * (4.9 / (14 if fine else 7))
            w = 4.9 / (14 if fine else 7) - .05
            board.face((V(x0, y, -6.21), V(x0 + w, y, -6.21), V(x0 + w, y + .22, -6.21), V(x0, y + .22, -6.21)), F['flap'], normal=(0, 0, 1))
    for x in (-16, 16):
        sk.box(g, x - 3.0, x + 3.0, 3.0, 3.7, -6.4, -6.3, F['frame'], None, .01, cell=0)
        for k in range(5):
            w = (.5, .9, .35, .7, .45)[k]
            x0 = x - 2.6 + k * 1.05
            lights.face((V(x0, 3.22, -6.29), V(x0 + w, 3.22, -6.29), V(x0 + w, 3.48, -6.29), V(x0, 3.48, -6.29)), F['sign'], normal=(0, 0, 1))
    # ---- the back wall, the vault and its ribs and light lines ----
    g.face((V(H, 0, 8.2), V(-H, 0, 8.2), V(-H, ST_SPRING, 8.2), V(H, ST_SPRING, 8.2)), F['wall'], normal=(0, 0, -1))
    sk.box(g, -H, H, 2.3, 2.62, 8.14, 8.2, F['band'], None, 0, cell=0)
    for x in (-24, -8, 8, 24):
        sk.box(g, x - 1.6, x + 1.6, 0, 3.0, 8.05, 8.2, F['dark'], None, .02, cell=0)
    na = 20 if fine else 8
    nx = 34 if fine else 8
    zs = [-6.4 + (8.2 + 6.4) * j / na for j in range(na + 1)]
    for i in range(nx):
        x0, x1 = -H + 2 * H * i / nx, -H + 2 * H * (i + 1) / nx
        for j in range(na):
            z0, z1 = zs[j], zs[j + 1]
            a, b = V(x0, vault_y(z0), z0), V(x1, vault_y(z0), z0)
            c, d = V(x1, vault_y(z1), z1), V(x0, vault_y(z1), z1)
            zm = (z0 + z1) / 2
            nrm = _norm((0, -1, (zm - ST_ZC) / ST_SPAN * .9))
            g.face((a, b, c, d), F['vault'], normal=nrm)
    for k in range(9):
        x = -32 + 8 * k
        path = [V(x, vault_y(z) - .02, z) for z in zs]
        sk.sweep(g, sk.rect_section(.28, .22), path, F['rib'], up=(1, 0, 0))
    for zl in (-3.4, -.2, 2.0, 5.2):
        y = vault_y(zl) - .05
        for k in range(8):
            x0 = -30 + 8 * k + .4
            lights.face((V(x0, y, zl - .14), V(x0 + 7.2, y, zl - .14), V(x0 + 7.2, y, zl + .14), V(x0, y, zl + .14)), F['light'], normal=(0, -1, 0))
    # ---- the columns, benches, hanging signs, a bin ----
    for k in range(8):
        x = -28 + 8 * k
        top = vault_y(2.4)
        sk.box(g, x - .45, x + .45, .012, .2, 2.4 - .45, 2.4 + .45, F['steel'], T, .02)
        sk.lathe(g, [(.32, .2), (.3, top - 1.1), (.42, top - .5), (.7, top - .05)], V(x, 0, 2.4), F['column'], 16 if fine else 6)
    bench = Geo()
    sk.box(bench, -1.0, 1.0, .42, .48, -.22, .22, F['wood'], T, .015)
    sk.box(bench, -1.0, 1.0, .48, .9, .18, .24, F['wood'], T, .015)
    for xx in (-.8, .8):
        sk.box(bench, xx - .04, xx + .04, .012, .44, -.2, .2, F['steel'], None, .01, cell=0)
    for x in (-24, -16, 12, 20):
        place(g, bench, M((x, 0, 5.4)))
    for x in (-10, 10):
        sk.cylinder(g, V(x - .8, vault_y(1.0), 1.0), V(x - .8, 4.3, 1.0), .02, F['steel'], 4)
        sk.cylinder(g, V(x + .8, vault_y(1.0), 1.0), V(x + .8, 4.3, 1.0), .02, F['steel'], 4)
        sk.box(g, x - 1.2, x + 1.2, 3.75, 4.32, .9, 1.1, F['frame'], None, .02, cell=0)
        for side in (1.105, .895):
            nz_ = 1 if side > 1 else -1
            for k in range(3):
                w = (.35, .8, .5)[k]
                x0 = x - 1.0 + k * .7
                lights.face((V(x0, 3.88, side), V(x0 + w, 3.88, side), V(x0 + w, 4.18, side), V(x0, 4.18, side)), F['sign'], normal=(0, 0, nz_))
    sk.lathe(g, [(.001, .012), (.24, .012), (.26, .8), (.22, .86), (.001, .86)], V(6.0, 0, 6.8), F['steel'], 12 if fine else 6)
    # ---- the end walls and their tunnel mouths ----
    for sx in (-1, 1):
        x = sx * H
        nrm = (-sx, 0, 0)
        g.face((V(x, -1.55, -6.4), V(x, -1.55, -5.3), V(x, 8.4, -5.3), V(x, 8.4, -6.4)), F['concrete'], normal=nrm)
        g.face((V(x, -1.55, -.6), V(x, -1.55, 8.2), V(x, 8.4, 8.2), V(x, 8.4, -.6)), F['concrete'], normal=nrm)
        g.face((V(x, ST_MOUTH, -5.3), V(x, ST_MOUTH, -.6), V(x, 8.4, -.6), V(x, 8.4, -5.3)), F['concrete'], normal=nrm)
        sk.box(g, x - .3 * sx, x + .02 * sx, -1.55, ST_MOUTH + .2, -5.55, -5.3, F['band'], None, .02, cell=0)
        sk.box(g, x - .3 * sx, x + .02 * sx, -1.55, ST_MOUTH + .2, -.6, -.35, F['band'], None, .02, cell=0)
        sk.box(g, x - .3 * sx, x + .02 * sx, ST_MOUTH, ST_MOUTH + .25, -5.55, -.35, F['band'], None, .02, cell=0)
    # ---- the tunnel beyond the +x mouth: its lining, ribs every 4 m, light strips ----
    tun = Geo()
    prof = ST_TUNNEL
    L0, L1 = H, H + 104
    nseg = 26 if fine else 6
    for i in range(nseg):
        x0, x1 = L0 + (L1 - L0) * i / nseg, L0 + (L1 - L0) * (i + 1) / nseg
        for k in range(len(prof) - 1):
            (z0, y0), (z1, y1) = prof[k], prof[k + 1]
            mid = ((z0 + z1) / 2, (y0 + y1) / 2)
            nrm = _norm((0, 1.3 - mid[1], -2.95 - mid[0]))
            tun.face((V(x0, y0, z0), V(x1, y0, z0), V(x1, y1, z1), V(x0, y1, z1)), F['concrete'], normal=nrm)
        tun.face((V(x0, -1.55, -5.3), V(x1, -1.55, -5.3), V(x1, -1.55, -.6), V(x0, -1.55, -.6)), F['dark'], normal=(0, 1, 0))
    sk.box(tun, L0, L1, -1.55, -.68, tz - .8, tz + .8, F['concrete'], T, .03)
    for zz in (tz - .72, tz + .72):
        sk.box(tun, L0, L1, -.68, -.52, zz - .08, zz + .08, F['rail'], None, .01, cell=0)
    nrib = 26 if fine else 0
    for i in range(nrib):
        x = L0 + 2 + 4 * i
        path = [V(x, y, z) for z, y in prof]
        sk.sweep(tun, sk.rect_section(.22, .14), path, F['rib'], up=(1, 0, 0))
    for zz, nz_ in ((-5.25, 1), (-.65, -1)):
        tun.face((V(L0, 1.45, zz), V(L1, 1.45, zz), V(L1, 1.58, zz), V(L0, 1.58, zz)), F['strip'], normal=(0, 0, nz_))
    for i in range(0 if not fine else 13):
        x = L0 + 4 + 8 * i
        tun.face((V(x, 5.28, -3.3), V(x + 3, 5.28, -3.3), V(x + 3, 5.28, -2.6), V(x, 5.28, -2.6)), F['strip'], normal=(0, -1, 0))
    # ---- the cars ----
    car = build_car(T, F, False)
    nose = build_car(T, F, True)
    def ad_fn(P):
        """A wordless poster on each ad panel: its two colours in a diagonal gradient, a bright product disc on the right, two
        headline bars and a logo block on the left (soft-edged, in the panel's grid)."""
        x, y = P[:, 0], P[:, 1]
        k = np.argmin(np.abs(x[:, None] - np.array(ST_ADS)[None, :]), axis=1)
        cx = np.array(ST_ADS)[k]
        u = np.clip((x - cx + 1.45) / 2.9, 0, 1)
        v = np.clip((y - .55) / 1.4, 0, 1)
        c0 = np.array([mix_hex(ST_AD_PAL[i][0], ST_AD_PAL[i][0], 0.0) for i in range(len(ST_ADS))])[k]
        c1 = np.array([mix_hex(ST_AD_PAL[i][1], ST_AD_PAL[i][1], 0.0) for i in range(len(ST_ADS))])[k]
        t = np.clip(u * .6 + v * .4, 0, 1)[:, None]
        col = (c0 * (1 - t) + c1 * t) * .62
        disc_ = np.clip(1 - np.hypot((u - .7) * 2.9, (v - .48) * 1.4) / .5, 0, 1)
        col = col * (1 - np.clip(disc_ * 3, 0, 1))[:, None] + (np.clip(disc_ * 3, 0, 1) * (.75 + .25 * disc_))[:, None] * (c1 * .5 + .5)
        bar1 = ((v > .7) & (v < .84) & (u > .07) & (u < .5)).astype(np.float64)
        bar2 = ((v > .55) & (v < .63) & (u > .07) & (u < .38)).astype(np.float64)
        logo = ((v > .1) & (v < .24) & (u > .07) & (u < .17)).astype(np.float64)
        col = col * (1 - bar1 * .9)[:, None] + bar1[:, None] * .95
        col = col * (1 - bar2 * .8)[:, None] + bar2[:, None] * .8
        col = col * (1 - logo * .85)[:, None] + logo[:, None] * np.array([.08, .09, .12])
        return np.clip(col, 0, 1)
    return [
        Piece('slipstream/hall', g, True, (0.0, -1.55), seed=5),
        Piece('slipstream/lights', lights, False, recolour=({F['ad1'], F['ad2'], F['ad3']}, ad_fn)),
        Piece('slipstream/edge', edge, False),
        Piece('slipstream/board', board, False),
        Piece('slipstream/tunnel', tun, False, (-1.55,), seed=6),
        Piece('slipstream/car', car, False, (-50.0,), preview=[M((-9.0, 0, ST_TRACK_Z))]),
        Piece('slipstream/nose', nose, False, (-50.0,), preview=[M((-25.0, 0, ST_TRACK_Z), yaw=PI)]),
    ]


VIEWS = [
    ('ss-hero', (.6, 1.3, 4.6), (0, 1.2, -1), 44),
    ('ss-hall', (14, 3.0, 6.5), (-6, 1.5, -3), 64),
    ('ss-roof', (-4, 3.9, -3.0), (-30, 3.0, -2.9), 60),
    ('ss-tunnel', (40, 1.5, -2.95), (80, 1.5, -2.95), 70),
]
