"""Polaris Station's icebreaker and observatory (docs/arenas/POLAR.md "Stage art"): the hull lofted through the bow's blocks,
her planked foredeck, the bulwarks, winch house, lifeboat pods, windlass and foremast on their blocks, the gangways, the
wheelhouse front and, past the map's north bound, her house, funnel and mainmast; the observatory's terrace, stairs, windbreak,
racks, consoles, parapet, the dish's pedestal and the shelf beacon. Everything below 2 m over its floor keeps within .25 m of a
block (the stage rule); higher is overhead. Built whole about x = 0 (the map mirrors there)."""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo
import polar_kit as pk
from polar_kit import F, H, bx

DECK = 3.2


def hull_half(L):
    """The hull's half breadth along z: through the middle of each bow block's step, the full beam aft of them."""
    pts = []
    for b in L['blocks']:
        if b['id'].startswith('polar-ship-bow-'):
            x0, x1, y0, y1, z0, z1 = bx(b)
            pts.append(((z0 + z1) / 2, x1))
    pts.sort()
    stem = min(bx(b)[4] for b in L['blocks'] if b['id'].startswith('polar-ship-bow-'))
    zs = [stem] + [p[0] for p in pts] + [14.0]
    ws = [.04] + [p[1] for p in pts] + [6.0]
    return zs, ws


def hull(g, L, T):
    zs, ws = hull_half(L)
    # stations: the bow's, then the parallel body north out of the map, then the stern's round
    fine = np.linspace(zs[0], 14.0, (40, 22, 12)[T.i])
    wf = np.interp(fine, zs, ws)
    aft = [16, 20, 24, 27, 34, 44, 54, 58, 60.5, 62.5, 64, 65]
    wa = [6, 6, 6, 6, 6, 6, 6, 5.8, 5.2, 4.2, 2.6, .3]
    Z = np.concatenate([fine, aft])
    W = np.concatenate([wf, wa])
    rows = [(0.0, -.16), (.5, -.06), (.9, 0.0), (1.7, 0.0), (2.5, 0.0), (2.7, 0.0), (3.2, 0.0)]
    def band(n, c):
        y = c[1]
        return F['hull-dark'] if y < .9 else (F['white'] if 2.5 < y < 2.7 else F['hull'])
    for side in (-1, 1):
        P = np.zeros((len(Z), len(rows), 3))
        for i in range(len(Z)):
            for j, (y, dw) in enumerate(rows):
                P[i, j] = (side * max(.02, W[i] + dw * min(1.0, W[i] / 2.0)), y, Z[i])
        pk.surf(g, P, F['hull'], facing=(side, 0, -.2), pick=band)
    # plate seams down her sides (the Fine tier)
    if T.tiny:
        for side in (-1, 1):
            for z in np.arange(10.4, 58, 2.4):
                w = float(np.interp(z, Z, W))
                sk.box(g, side * w - .012, side * w + .012, .92, 2.48, z - .02, z + .02, F['hull'], T, bevel=.008, cell=0)
    # the stem bar and the rubbing strake
    sk.box(g, -.1, .1, 0, DECK + .25, zs[0] - .1, zs[0] + .1, F['hull-dark'], T, bevel=.03, cell=0)
    for side in (-1, 1):
        path = [(side * (np.interp(z, Z, W) + .075), 2.6, z) for z in np.linspace(9.2, 58, (40, 16, 8)[T.i])]
        sk.sweep(g, sk.rect_section(.12, .16, .03), path, F['hull-dark'], up=(0, 1, 0), caps=True)
    # the deck: planks between the margins, bow to the wheelhouse front and on aft of the house at the same level
    for i in range(len(Z) - 1):
        z0, z1, w0, w1 = Z[i], Z[i + 1], W[i], W[i + 1]
        m0, m1 = max(0.0, w0 - .28), max(0.0, w1 - .28)
        g.face(((-m0, DECK, z0), (m0, DECK, z0), (m1, DECK, z1), (-m1, DECK, z1)), F['deck'], normal=(0, 1, 0))
        for s in (-1, 1):
            g.face(((s * m0, DECK, z0), (s * w0, DECK, z0), (s * w1, DECK, z1), (s * m1, DECK, z1)), F['steel-dark'], normal=(0, 1, 0))
    # the bow's toe rail (the bulwarks begin at the parallel body) and the ice piled at her waterline
    for side in (-1, 1):
        path = [(side * max(.06, np.interp(z, Z, W) - .07), DECK + .07, z) for z in np.linspace(zs[0] + .15, 14.0, (26, 12, 6)[T.i])]
        sk.sweep(g, sk.rect_section(.1, .14, .02), path, F['hull-dark'], up=(0, 1, 0), caps=True)
        if T.detail:
            for k, z in enumerate(np.linspace(9.0, 26.5, 13)):
                w = float(np.interp(z, Z, W))
                if abs(z - 15.2) < 3.6 or abs(z - 19.2) < 1.2:
                    continue          # the gangway stands here
                pk.mound(g, side * w - .22 if side < 0 else w - .02, side * w + .02 if side < 0 else w + .22, z - .7, z + .7, 0, .16 + .08 * H(k, side + 2), F['snow'], T, seed=k, steep=.1, bulge=.03, cell=.35)
    # portholes along her sides, lit
    if T.detail:
        for side in (-1, 1):
            for k, z in enumerate(np.arange(15.4, 57, 2.4)):
                if H(k, side + 5, 3) < .25:
                    continue
                o = pk.ngon(.17, (10, 6, 6)[T.i])
                g.face([(side * 6.014, 2.05 + c, z + s) for c, s in o], F['window-dim'], normal=(side, 0, 0))
                o2 = pk.ngon(.23, (10, 6, 6)[T.i])
                g.face([(side * 6.007, 2.05 + c, z + s) for c, s in o2], F['steel-dark'], normal=(side, 0, 0))
    # the anchors in their hawses (over 2 m up the bow)
    if T.detail:
        for side in (-1, 1):
            w = float(np.interp(10.3, Z, W))
            sk.box(g, side * w - .07, side * w + .07, 2.1, 2.9, 10.22, 10.38, F['steel-dark'], T, bevel=.02, cell=0)
            sk.box(g, side * w - .06, side * w + .06, 2.1, 2.26, 9.95, 10.65, F['steel-dark'], T, bevel=.02, cell=0)


def deck_fittings(g, L, T, B):
    # bulwarks: plate, stanchions and a timber cap
    for bid in ('polar-ship-rail-w-0', 'polar-ship-rail-w-1', 'polar-ship-rail-e-0', 'polar-ship-rail-e-1'):
        x0, x1, y0, y1, z0, z1 = bx(B[bid])
        s = -1 if x0 < 0 else 1
        outer = x0 if s < 0 else x1
        sk.box(g, outer - (0 if s < 0 else .08), outer + (.08 if s < 0 else 0), y0, y1 - .06, z0, z1, F['hull'], T, bevel=.01)
        sk.box(g, min(x0, x1) - .03, max(x0, x1) + .03, y1 - .08, y1 + .02, z0, z1, F['timber-z'], T, bevel=.025, cell=0)
        if T.detail:
            for z in np.arange(z0 + .3, z1 - .1, .9):
                sk.box(g, x0 + .03, x1 - .03, y0, y1 - .08, z - .04, z + .04, F['hull-dark'], T, bevel=.01, cell=0)
    # the winch house: a steel house with round corners, a door, lit ports, a railed roof
    x0, x1, y0, y1, z0, z1 = bx(B['polar-ship-house'])
    pk.body(g, x0, x1, y0, y1, z0, z1, F['white'], r=.22, T=T, f_top=F['steel-dark'])
    sk.box(g, -.48, .48, y0, y0 + 2.0, z0 - .05, z0 + .02, F['steel-dark'], T, bevel=.02, cell=0)
    if T.detail:
        g.face([(.0 + c, y0 + 1.5 + s, z0 - .056) for c, s in pk.ngon(.14, 8)], F['window'], normal=(0, 0, -1))
        for x in (-1.6, 1.6):
            g.face([(x + c, y0 + 1.55 + s, z0 - .012) for c, s in pk.ngon(.2, 10)], F['window'], normal=(0, 0, -1))
            g.face([(x + c, y0 + 1.55 + s, z0 - .006) for c, s in pk.ngon(.26, 10)], F['steel-dark'], normal=(0, 0, -1))
            g.face([(x + c, y0 + 1.55 + s, z1 + .012) for c, s in pk.ngon(.2, 10)], F['window'], normal=(0, 0, 1))
        sk.box(g, x0 + .15, x1 - .15, y1, y1 + .05, z0 + .15, z1 - .15, F['snow'], T, bevel=.02, cell=0)
        sk.box(g, -.25, .25, y0 + 2.12, y0 + 2.24, z0 - .14, z0, F['lamp'], T, bevel=0, cell=0)
    # lifeboat pods on their cradles
    for bid in ('polar-ship-boat-w', 'polar-ship-boat-e'):
        x0, x1, y0, y1, z0, z1 = bx(B[bid])
        cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
        for z in (z0 + .35, z1 - .35):
            sk.box(g, x0 + .05, x1 - .05, y0, y0 + .5, z - .1, z + .1, F['steel-dark'], T, bevel=.02, cell=0)
            sk.box(g, x0 + .02, x0 + .2, y0, y0 + 1.5, z - .09, z + .09, F['steel-dark'], T, bevel=.02, cell=0)
            sk.box(g, x1 - .2, x1 - .02, y0, y0 + 1.5, z - .09, z + .09, F['steel-dark'], T, bevel=.02, cell=0)
        n = (9, 6, 4)[T.i]
        rings = []
        for i in range(n + 1):
            u = -1 + 2 * i / n
            r = max(.05, math.sqrt(max(0.0, 1 - u * u)) ** .75)
            out = pk.ngon(1.0, (12, 8, 6)[T.i], sx=(x1 - x0) / 2 * r, sz=.95 * r)
            rings.append([(cx + x, y0 + 1.42 + z, cz + u * (z1 - z0) / 2 * .99) for x, z in out])
        pk.loft(g, rings, F['orange'])
        sk.box(g, cx - .5, cx + .5, y0 + 2.2, y1, cz - .7, cz + .7, F['orange'], T, bevel=.1, cell=0)
        if T.detail:
            for z in (cz - .4, cz, cz + .4):
                s = -1 if cx < 0 else 1
                g.face([(cx - s * .505, y0 + 2.26 + .12 + c * .6, z + sn) for c, sn in pk.ngon(.12, 6)], F['window-dim'], normal=(-s, 0, 0))
            sk.box(g, x0 + .04, x1 - .04, y0 + 1.36, y0 + 1.46, z0 + .02, z1 - .02, F['white'], T, bevel=.02, cell=0)
    # the windlass: two warping drums on a bed, the gypsies outboard
    x0, x1, y0, y1, z0, z1 = bx(B['polar-ship-windlass'])
    sk.box(g, x0, x1, y0, y0 + .3, z0, z1, F['steel-dark'], T, bevel=.04, cell=0)
    cz = (z0 + z1) / 2
    for (a, b, r) in ((x0 + .1, -.2, .36), (.2, x1 - .1, .36), (-.2, .2, .22)):
        prof = [(r * .7, 0), (r, .06), (r * .8, .14), (r * .8, b - a - .14), (r, b - a - .06), (r * .7, b - a)]
        sk.lathe(g, prof, (a, y0 + .66, cz), F['steel'], (12, 8, 6)[T.i], axis=(1, 0, 0), cap0=True, cap1=True)
    for x in (x0 + .06, x1 - .06):
        sk.box(g, x - .06, x + .06, y0 + .3, y1, cz - .3, cz + .3, F['steel-dark'], T, bevel=.02, cell=0)
    # the foremast: a mast house, the mast, a crow's nest, a yard with lamps, stays to the bulwarks
    x0, x1, y0, y1, z0, z1 = bx(B['polar-ship-mast'])
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    pk.body(g, x0, x1, y0, y0 + 2.3, z0, z1, F['white'], r=.14, T=T, f_top=F['steel-dark'])
    sk.lathe(g, [(.3, 0), (.26, 3), (.2, 7.5), (.13, y1 - y0 - 2.6), (.05, y1 - y0 - 2.2)], (cx, y0 + 2.3, cz), F['white'], (10, 6, 5)[T.i], cap1=True)
    sk.lathe(g, [(.3, 0), (.72, .12), (.76, .9), (.7, .94), (.7, .14), (.3, .14)], (cx, y0 + 6.6, cz), F['white'], (12, 8, 6)[T.i])
    sk.box(g, cx - 2.6, cx + 2.6, y0 + 8.6, y0 + 8.74, cz - .07, cz + .07, F['white'], T, bevel=.02, cell=0)
    for x in (-2.5, 2.5):
        sk.box(g, cx + x - .08, cx + x + .08, y0 + 8.4, y0 + 8.6, cz - .08, cz + .08, F['lamp'], T, bevel=0, cell=0)
    sk.box(g, cx - .1, cx + .1, y1 - .35, y1 - .1, cz - .1, cz + .1, F['beacon'], T, bevel=0, cell=0)
    if T.detail:
        for (x, z) in ((-5.86, 14.3), (5.86, 14.3), (-5.86, 17.9), (5.86, 17.9)):
            pk.pipe(g, (cx, y0 + 8.4, cz), (x, DECK + 1.12, z), .022, F['black'], T, caps=False)
        pk.pipe(g, (cx, y0 + 10.6, cz), (0, DECK + .3, 8.5), .022, F['black'], T, caps=False)
        sk.box(g, cx - .5, cx + .5, y0 + .9, y0 + 1.3, z0 - .012, z0 - .004, F['red'], T, bevel=0, cell=0)
    # dressing lights: lamps down the forestay to the stem and along the stays to the wheelhouse's roof
    # (down the forestay only as far as 2.4 m over the bow's deck)
    pk.festoon(g, (cx, y0 + 10.4, cz - .2), (0, DECK + 2.5, 9.6), .3, T, F['lamp'], step=.9)
    for s in (-1, 1):
        pk.festoon(g, (cx, y0 + 10.4, cz + .2), (s * 5.2, 12.5, 24.3), 1.2, T, F['lamp'], step=1.0)
    # signal pennants down a halyard from the yard to each bulwark's after end (over the deck: none within 2 m of it)
    if T.detail:
        cols = (F['red'], F['yellow'], F['white'], F['blue-paint'], F['orange'])
        for s in (-1, 1):
            a, b = (cx + s * 2.3, y0 + 8.55, cz), (s * 5.86, DECK + 3.4, 21.5)
            pk.pipe(g, a, b, .012, F['black'], T, caps=False)
            n = 9
            for k in range(1, n):
                u = k / n
                p = (a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u, a[2] + (b[2] - a[2]) * u)
                q = (a[0] + (b[0] - a[0]) * (u + .045), a[1] + (b[1] - a[1]) * (u + .045), a[2] + (b[2] - a[2]) * (u + .045))
                tip = ((p[0] + q[0]) / 2 + s * .08, (p[1] + q[1]) / 2 - .5, (p[2] + q[2]) / 2)
                g.face((p, q, tip), cols[k % 5], normal=(s, 0, 0))
                g.face((p, q, tip), cols[k % 5], normal=(-s, 0, 0))
    # gangways: timber flights and their landings
    for b in L['blocks']:
        i = b['id']
        if i.startswith('polar-stair-gang-'):
            x0, x1, y0, y1, z0, z1 = bx(b)
            sk.box(g, x0, x1, max(y0, y1 - .5), y1, z0, z1 + .03, F['timber'], T, bevel=.02, cell=0, skip=('-y', '+x' if x0 < 0 else '-x'))
            out = x0 if x0 < 0 else x1
            sk.box(g, min(out, out + (-.0 if x0 < 0 else -.1)), max(out, out + (.1 if x0 < 0 else .0)), y0, y1, z0, z1, F['timber-dark'], T, bevel=0, cell=0, skip=('-y', '+y'))
        elif i.startswith('polar-ship-landing-'):
            x0, x1, y0, y1, z0, z1 = bx(b)
            sk.box(g, x0, x1, y1 - .14, y1, z0, z1, F['timber'], T, bevel=.02, cell=0)
            for (px, pz) in ((x0 + .12, z0 + .12), (x1 - .12, z0 + .12), (x0 + .12, z1 - .12), (x1 - .12, z1 - .12)):
                sk.box(g, px - .12, px + .12, y0, y1 - .14, pz - .12, pz + .12, F['timber-dark'], T, bevel=.02, cell=0, skip=('-y',))
            out = x0 if x0 < 0 else x1
            for k in range(3):
                y = y0 + .5 + k * 1.0
                sk.box(g, x0 + .1, x1 - .1, y, y + .12, z0 + .04, z0 + .12, F['timber-dark'], T, bevel=.01, cell=0)
                sk.box(g, x0 + .1, x1 - .1, y, y + .12, z1 - .12, z1 - .04, F['timber-dark'], T, bevel=.01, cell=0)
                sk.box(g, out + (.04 if x0 < 0 else -.12), out + (.12 if x0 < 0 else -.04), y, y + .12, z0 + .1, z1 - .1, F['timber-dark'], T, bevel=.01, cell=0)


def wheelhouse(g, L, T, B):
    """The wheelhouse front (its block: x +-6, z 24..27, y 3.2..12) and, above and behind it, the house, funnel and mainmast."""
    x0, x1, y0, y1, z0, z1 = bx(B['polar-ship-bridge'])
    # three storeys stepping back a little, a walkway before the bridge windows
    sk.box(g, x0, x1, y0, 6.2, z0, 44, F['white'], T, bevel=.05, skip=('-y',))
    sk.box(g, x0 + .3, x1 - .3, 6.2, 9.0, z0 + .12, 40, F['white'], T, bevel=.05, skip=('-y',))
    sk.box(g, x0 - .1, x1 + .1, 6.2, 6.32, z0 - .3, 41, F['steel-dark'], T, bevel=.02, cell=0)
    sk.box(g, x0 + .3, x1 - .3, 9.0, 12.0, z0 + .12, 34, F['white'], T, bevel=.05, skip=('-y',))
    sk.box(g, x0 - 1.7, x1 + 1.7, 8.86, 9.0, z0 + .0, z0 + 2.6, F['steel-dark'], T, bevel=.02, cell=0)       # the bridge wings
    sk.box(g, x0 - .3, x1 + .3, 12.0, 12.14, z0 - .5, 35, F['steel-dark'], T, bevel=.02, cell=0)
    sk.box(g, x0 + .2, x1 - .2, 12.14, 12.3, z0 - .3, 34.6, F['snow'], T, bevel=.06, cell=0)
    # doors and ports on the deck's level, lit; a red band under the top
    for x in (-3.6, 3.6):
        sk.box(g, x - .5, x + .5, y0, y0 + 2.05, z0 - .05, z0 + .02, F['steel-dark'], T, bevel=.03, cell=0)
        if T.detail:
            g.face([(x + c, y0 + 1.5 + s, z0 - .056) for c, s in pk.ngon(.15, 8)], F['window'], normal=(0, 0, -1))
            sk.box(g, x - .3, x + .3, y0 + 2.2, y0 + 2.32, z0 - .16, z0, F['lamp'], T, bevel=0, cell=0)
    for k, x in enumerate((-1.8, 0, 1.8, -5.2, 5.2)):
        g.face([(x + c, y0 + 1.6 + s, z0 - .012) for c, s in pk.ngon(.22, (10, 8, 6)[T.i])], F['window'], normal=(0, 0, -1))
        g.face([(x + c, y0 + 1.6 + s, z0 - .006) for c, s in pk.ngon(.29, (10, 8, 6)[T.i])], F['steel-dark'], normal=(0, 0, -1))
    for k in range(6):
        x = -4.6 + k * 1.84
        pk.frame_window(g, '-z', x - .5, x + .5, 7.1, 7.9, z0 + .12, T, F['steel-dark'], F['window-dim'], bars=(0, 0), sill=False)
    # the bridge's windows: a band of seven, raked forward under a visor
    for k in range(7):
        x = -4.74 + k * 1.58
        pk.frame_window(g, '-z', x - .62, x + .62, 9.75, 11.0, z0 + .12, T, F['steel-dark'], F['window'], bars=(0, 0), sill=False)
    sk.box(g, x0 + .25, x1 - .25, 11.15, 11.3, z0 - .45, z0 + .12, F['steel-dark'], T, bevel=.02, cell=0)
    sk.box(g, x0 + .3, x1 - .3, 11.5, 11.9, z0 + .1, z0 + .12, F['red'], T, bevel=0, cell=0)
    # rails along the walkway and the wings
    if T.detail:
        for (ax, a0, a1, fixed, y) in (('x', x0 - .1, x1 + .1, z0 - .26, 6.32), ('x', x0 - 1.66, x1 + 1.66, z0 + .04, 9.0), ('x', x0 - .26, x1 + .26, z0 - .46, 12.14)):
            for h in (.5, 1.0):
                pk.pipe(g, (a0, y + h, fixed), (a1, y + h, fixed), .025, F['white'], T)
            for u in np.arange(a0, a1 + .01, 1.5):
                pk.pipe(g, (u, y, fixed), (u, y + 1.0, fixed), .025, F['white'], T)
    # the searchlight's stand on the roof (the lamp itself is the rig's)
    sk.lathe(g, [(.3, 0), (.2, .2), (.12, .4), (.12, 1.5), (.2, 1.6)], (-3.4, 12.14, 26.4), F['steel-dark'], (8, 6, 5)[T.i], cap1=True)
    # the funnel, raked, with its black top; the mainmast with the radar's scanner bar and the lights; boats in davits
    rings = []
    for k, (y, s, dz) in enumerate(((12.0, 1.0, 0), (15, .96, .5), (19.5, .9, 1.3), (20.4, .93, 1.45), (21.2, .9, 1.6))):
        rings.append([(x, y, 40 + dz + z) for x, z in [(c * s, sn * s) for c, sn in pk.rrect(2.0, 2.7, 1.2, (4, 3, 2)[T.i])]])
    pk.loft(g, rings[:3], F['yellow'], cap0=False, cap1=False)
    pk.loft(g, rings[2:], F['hull-dark'], cap0=False, cap1=True)
    sk.lathe(g, [(.34, 0), (.3, 6), (.2, 14), (.08, 20)], (0, 12.14, 31.4), F['white'], (10, 6, 5)[T.i], cap1=True)
    sk.box(g, -3.8, 3.8, 29.4, 29.6, 31.3, 31.5, F['white'], T, bevel=.03, cell=0)
    sk.box(g, -1.5, 1.5, 24.2, 24.36, 31.25, 31.55, F['white'], T, bevel=.03, cell=0)
    sk.box(g, -.9, .9, 22.2, 22.5, 30.2, 32.6, F['steel-dark'], T, bevel=.05, cell=0)
    sk.box(g, -.12, .12, 32.1, 32.4, 31.28, 31.52, F['beacon'], T, bevel=0, cell=0)
    sk.box(g, -3.87, -3.63, 29.62, 29.84, 31.3, 31.5, F['beacon'], T, bevel=0, cell=0)
    sk.box(g, 3.63, 3.87, 29.62, 29.84, 31.3, 31.5, F['green'], T, bevel=0, cell=0)
    for side in (-1, 1):
        for zc in (36.5,):
            rings = []
            n = (8, 5, 4)[T.i]
            for i in range(n + 1):
                u = -1 + 2 * i / n
                r = max(.06, math.sqrt(max(0.0, 1 - u * u)) ** .7)
                rings.append([(side * 6.6 + x, 10.3 + z, zc + u * 3.0) for x, z in pk.ngon(1.0, (10, 8, 6)[T.i], sx=.95 * r, sz=.8 * r)])
            pk.loft(g, rings, F['orange'])
            for z in (zc - 1.8, zc + 1.8):
                pk.pipe(g, (side * 5.8, 9.0, z), (side * 6.2, 11.6, z), .07, F['white'], T)
                pk.pipe(g, (side * 6.2, 11.6, z), (side * 6.9, 11.4, z), .07, F['white'], T)
        # lit ports down the house's sides
        for k, z in enumerate(np.arange(28.5, 43, 2.2)):
            for (y, x) in ((4.9, 6.0), (7.6, 5.7), (10.4, 5.7)):
                if (y > 9.5 and z > 33.5) or (y > 6.5 and z > 39.5) or H(k, int(y), side + 3) < .22:
                    continue
                g.face([(side * (x + .012), y + c * .3, z + s * .5) for c, s in pk.rrect(1.0, 1.0, .5, 2)], F['window-dim'], normal=(side, 0, 0))
    # the after deck's house and a crane post, far aft
    sk.box(g, -4.2, 4.2, DECK, 5.6, 48, 56, F['white'], T, bevel=.06, skip=('-y',))
    sk.box(g, -4.4, 4.4, 5.6, 5.76, 47.8, 56.2, F['snow'], T, bevel=.05, cell=0)
    sk.lathe(g, [(.3, 0), (.24, 7)], (3.2, 5.76, 50), F['yellow'], (8, 6, 5)[T.i], cap1=True)
    pk.pipe(g, (3.2, 12.6, 50), (-2.6, 9.8, 58.5), .16, F['yellow'], T)


def observatory(g, L, T, B):
    # the terrace: a concrete platform with a battered plinth, a coping, a lit strip under it, panel joints
    x0, x1, y0, y1, z0, z1 = bx(B['polar-obs-terrace'])
    sk.box(g, x0 + .06, x1 - .06, y0, y1 - .22, z0 + .06, z1 - .06, F['concrete'], T, bevel=.02, skip=('-y', '+y'))
    sk.box(g, x0, x1, y0, y0 + .45, z0, z1, F['concrete-dark'], T, bevel=.05, cell=0, skip=('-y',))
    sk.box(g, x0, x1, y1 - .22, y1, z0, z1, F['concrete'], T, bevel=.04)
    sk.box(g, x0 + .3, x1 - .3, y1 - .34, y1 - .26, z1 - .05, z1 + .012, F['lamp'], T, bevel=0, cell=0)
    if T.detail:
        for x in np.arange(x0 + 2, x1 - 1, 2.0):
            sk.box(g, x - .02, x + .02, y0 + .45, y1 - .36, z1 - .05, z1 - .045 + .0, F['concrete-dark'], T, bevel=0, cell=0)
    # stairs
    for b in L['blocks']:
        if b['id'].startswith('polar-stair-obs-'):
            a0, a1, b0, b1, c0, c1 = bx(b)
            sk.box(g, a0, a1, max(b0, b1 - .45), b1, c0, c1, F['concrete'], T, bevel=.025, cell=0, skip=('-y',))
            sk.box(g, a0 + .0, a1 - .0, b0, b1 - .1, c0 + .04, c1 - .04, F['concrete-dark'], T, bevel=0, cell=0, skip=('-y', '+y'))
    # the windbreak: timber slats between steel posts, snow along its head
    x0, x1, y0, y1, z0, z1 = bx(B['polar-obs-back'])
    for k, y in enumerate(np.arange(y0 + .04, y1 - .1, .36 if T.i == 0 else 2.6)):
        h = .32 if T.i == 0 else y1 - y0 - .1
        sk.box(g, x0, x1, y, y + h, z0 + .1, z1 - .1, F['timber'], T, bevel=.02, cell=3.0)
    for x in np.arange(x0 + .1, x1 + .01, 2.63):
        sk.box(g, x - .1, x + .1, y0, y1, z0, z1, F['steel-dark'], T, bevel=.02, cell=0)
    pk.mound(g, x0, x1, z0, z1, y1 - .02, .12, F['snow'], T, seed=5, steep=.08, bulge=.04, cell=.5)
    # instrument racks: cabinets with lit screens and status lamps toward the spawn
    for bid in ('polar-obs-rack-w', 'polar-obs-rack-e'):
        x0, x1, y0, y1, z0, z1 = bx(B[bid])
        s = 1 if x0 < 0 else -1
        inner = x1 if s > 0 else x0
        pk.body(g, x0, x1, y0, y1, z0, z1, F['steel-dark'], r=.06, T=T, f_top=F['steel'])
        if T.detail:
            for k, z in enumerate(np.arange(z0 + .35, z1 - .6, .95)):
                g.face(((inner + s * .012, y0 + 1.25, z), (inner + s * .012, y0 + 1.25, z + .6), (inner + s * .012, y0 + 1.68, z + .6), (inner + s * .012, y0 + 1.68, z)), F['screen'] if k % 2 == 0 else F['window-dim'], normal=(s, 0, 0))
                for j in range(4):
                    g.face(((inner + s * .012, y0 + .7, z + j * .16), (inner + s * .012, y0 + .7, z + j * .16 + .08), (inner + s * .012, y0 + .78, z + j * .16 + .08), (inner + s * .012, y0 + .78, z + j * .16)), F['beacon'] if (k + j) % 3 == 0 else F['green'], normal=(s, 0, 0))
    # consoles: a desk with a sloped lit top
    for bid in ('polar-obs-console-w', 'polar-obs-console-e'):
        x0, x1, y0, y1, z0, z1 = bx(B[bid])
        pk.body(g, x0, x1, y0, y1 - .12, z0, z1, F['steel-dark'], r=.06, T=T)
        g.face(((x0 + .08, y1 - .1, z0 + .06), (x1 - .08, y1 - .1, z0 + .06), (x1 - .08, y1, z1 - .06), (x0 + .08, y1, z1 - .06)), F['screen'], normal=(0, 1, -.15))
        g.face(((x0 + .08, y1 - .12, z1 - .06), (x1 - .08, y1 - .12, z1 - .06), (x1 - .08, y1, z1 - .06), (x0 + .08, y1, z1 - .06)), F['steel-dark'], normal=(0, 0, 1))
    # the parapet: posts and solid panels (it is cover), a yellow rail
    for bid in ('polar-obs-rail-w', 'polar-obs-rail-e'):
        x0, x1, y0, y1, z0, z1 = bx(B[bid])
        sk.box(g, x0, x1, y0 + .1, y1 - .12, z0 + .08, z1 - .08, F['steel'], T, bevel=.01, cell=2.0)
        sk.box(g, x0, x1, y1 - .1, y1, z0, z1, F['yellow'], T, bevel=.03, cell=0)
        for x in np.arange(x0 + .12, x1, 1.34):
            sk.box(g, x - .08, x + .08, y0, y1 - .1, z0, z1, F['yellow'], T, bevel=.02, cell=0)
    # the dish's pedestal: square to 2 m over the terrace, then drawn in to an eight-sided tower with a gallery under the yoke
    x0, x1, y0, y1, z0, z1 = bx(B['polar-obs-pedestal'])
    cx, cz, hx, hz = (x0 + x1) / 2, (z0 + z1) / 2, (x1 - x0) / 2, (z1 - z0) / 2
    n = (3, 2, 1)[T.i]
    rings = [pk.ring_at(pk.rrect(hx, hz, .18, n), y0, cx, cz), pk.ring_at(pk.rrect(hx, hz, .18, n), y0 + 2.2, cx, cz),
             pk.ring_at(pk.rrect(hx * .86, hz * .86, .6, n), y0 + 3.0, cx, cz), pk.ring_at(pk.rrect(hx * .8, hz * .8, .75, n), y1 - 1.2, cx, cz),
             pk.ring_at(pk.rrect(hx * 1.02, hz * 1.02, .8, n), y1 - .7, cx, cz), pk.ring_at(pk.rrect(hx * 1.02, hz * 1.02, .8, n), y1, cx, cz)]
    pk.loft(g, rings, F['dish'], cap0=False, cap1=True, f_cap=F['steel-dark'])
    for y in (y0 + 2.2, y1 - 1.25):
        sk.lathe(g, [(hx * 1.03, 0), (hx * 1.1, .08), (hx * 1.1, .2), (hx * 1.0, .28)], (cx, y, cz), F['steel-dark'], (16, 10, 8)[T.i])
    sk.box(g, cx - .5, cx + .5, y0, y0 + 2.05, z1 - .02, z1 + .05, F['steel-dark'], T, bevel=.03, cell=0)
    if T.detail:
        g.face([(cx + c, y0 + 1.55 + s, z1 + .056) for c, s in pk.ngon(.14, 8)], F['window'], normal=(0, 0, 1))
        sk.box(g, cx - .3, cx + .3, y0 + 2.14, y0 + 2.24, z1 + .0, z1 + .14, F['lamp'], T, bevel=0, cell=0)
        for k in range(8):
            a = k * math.pi / 4 + math.pi / 8
            px, pz = cx + math.cos(a) * hx * 1.12, cz + math.sin(a) * hz * 1.12
            sk.box(g, px - .07, px + .07, y1 - 1.0, y1 - .78, pz - .07, pz + .07, F['beacon'], T, bevel=0, cell=0)
        # a ladder up the south face (over 2 m up it is free of the block)
        for x in (cx - .25, cx + .25):
            pk.pipe(g, (x, y0 + 2.2, z0 - .12), (x, y1 - .7, z0 - .2), .025, F['steel'], T)
    # the shelf beacon: a lattice tower with a red lamp
    x0, x1, y0, y1, z0, z1 = bx(B['polar-obs-beacon'])
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    pk.body(g, x0, x1, y0, y0 + 2.2, z0, z1, F['white'], r=.12, T=T, f_top=F['steel-dark'])
    sk.box(g, x0 - .004, x1 + .004, y0 + .9, y0 + 1.4, z0 - .004, z1 + .004, F['red'], T, bevel=.12, cell=0, skip=('-y', '+y'))
    sk.lathe(g, [(.34, 0), (.2, 1.4), (.16, 2.3), (.3, 2.36), (.3, 2.44)], (cx, y0 + 2.2, cz), F['white'], (8, 6, 5)[T.i])
    sk.lathe(g, [(.24, 0), (.26, .2), (.2, .36), (.02, .42)], (cx, y0 + 4.64, cz), F['beacon'], (8, 6, 5)[T.i])


def build(g, L, T):
    B = {b['id']: b for b in L['blocks']}
    hull(g, L, T)
    deck_fittings(g, L, T, B)
    wheelhouse(g, L, T, B)
    observatory(g, L, T, B)
