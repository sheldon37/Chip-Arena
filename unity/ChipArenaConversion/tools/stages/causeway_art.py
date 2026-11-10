"""Starfall Causeway's art (docs/arenas/CAUSEWAY.md, docs/arenas/STAGE-ART.md): the ancient bridge modelled in Blender on the
map's own blocks, into the stage bank Resources/ChipArenaStageCauseway (stagekit.py's format and bake). Runtime NativeStageArt
draws it in place of the boxes; without the bank the box kit draws as before.

usage (from unity/ChipArenaConversion):
  dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout causeway LAYOUT.json
  blender --background --factory-startup --python tools/stages/causeway_art.py -- --layout LAYOUT.json --out ASSETS/LocalAssets
          [--preview DIR] [--tiers 0,1,2] [--no-ao] [--static-only]

The map is point symmetric ((x, z) -> (-x, -z)): the west half (tower A's) is sculpted block by block and turned half round for
the east half. This file: the span (its deck, the keel's arcaded flanks and corbelled wings, the Breach's broken faces), the
balustrades, pylons, guardians, braziers, fallen columns, the plazas, the gate halls and their arches, the star altars, the
crowns with their gables and broken spires, the lantern gates, the walkways, the stairs, the balconies and the keystone.
causeway_props.py: the rig (falling stars, the comet, lane glyphs, banners, crystals, the Shattered Arch, the crowns' drifting
ruins, asteroids). causeway_void.py: what hangs in the void (the towers' stumps, the hanging arches under the keel, the lost
spans, the shrine, the colossus, islands, the asteroid belt). causeway_kit.py: the helpers and the finishes.
"""
import argparse
import math
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, sweep, lathe, grid_face  # noqa: E402
import causeway_kit as ck  # noqa: E402
from causeway_kit import F, Y, Face, ext, blk, hashf, facet, facet2  # noqa: E402

BANK = 'ChipArenaStageCauseway'
FLOORS = (-3.2, 0.0, 4.4)

# mouldings along a face: (up, out) from the path's height
CORNICE = [(0, -.01), (0, .14), (-.08, .14), (-.11, .11), (-.2, .1), (-.3, .05), (-.42, .035), (-.5, -.01)]
STRING = [(.1, -.01), (.1, .05), (.06, .09), (-.02, .09), (-.08, .04), (-.1, -.01)]
PLINTH = [(0, -.01), (0, .14), (.24, .14), (.3, .1), (.38, .08), (.46, .03), (.5, -.01)]
FASCIA = [(0, -.01), (0, .1), (-.06, .12), (-.14, .12), (-.2, .08), (-.34, .06), (-.42, .02), (-.6, .0), (-.6, -.01)]
COPE = [(.2, -.01), (.2, .06), (.14, .1), (.06, .1), (0, .05), (0, -.01)]
# round an opening: (outward from the opening, out of the wall)
ARCHIVOLT = [(.16, 0), (.16, .05), (.11, .1), (.05, .1), (0, .05), (0, 0)]
ARCHLITE = [(.07, 0), (.035, .035), (0, 0)]
# round a plan: (out, up)
BASE = [(-.02, 0.0), (.07, 0.0), (.07, .1), (.04, .14), (.05, .18), (.01, .22), (-.02, .22)]
CAP = [(-.02, 0.0), (.0, 0.0), (.05, .04), (.08, .09), (.08, .14), (-.02, .14)]


def sides(b):
    x0, x1, y0, y1, z0, z1 = ext(b)
    return {'+z': Face((x0, 0, z1), (0, 0, 1), x1 - x0), '-z': Face((x1, 0, z0), (0, 0, -1), x1 - x0),
            '+x': Face((x1, 0, z1), (1, 0, 0), z1 - z0), '-x': Face((x0, 0, z0), (-1, 0, 0), z1 - z0)}


# ---------------------------------------------------------------------------------------------------------------------------
# The span: deck, wings, keel; its broken ends
# ---------------------------------------------------------------------------------------------------------------------------
KEEL, WING, TAPER_Y, TAPER = 3.4, 5.0, -5.6, .9


def keel_lim(y):
    return KEEL - TAPER * (TAPER_Y - y) / (TAPER_Y + 7.0) if y < TAPER_Y else KEEL


def fracture(g, X, s, T, seed, bottom=-7.0):
    """The span's broken end at x = X, the stone on the side s (+1: toward +x): a chiselled face eaten back under the deck's
    lip, and the strips that carry the deck, wings and keel to it from 1.6 m inside. Returns the x the whole faces end at."""
    back = X + s * 1.6
    k1, k2 = (3, 2, 1)[T.i], (14, 7, 4)[T.i]
    S = np.concatenate([np.linspace(-1, -.68, k1 + 1)[:-1], np.linspace(-.68, .68, k2 + 1), np.linspace(.68, 1, k1 + 1)[1:]])
    rows = [(0.0, True), (-.3, True), (-.6, True), (-.6, False)]
    step = (.45, .9, 1.6)[T.i]
    n = max(1, int(round((-.6 - TAPER_Y) / step)))
    rows += [(-.6 + (TAPER_Y + .6) * (i + 1) / n, False) for i in range(n)]
    n = max(1, int(round((TAPER_Y - bottom) / step)))
    rows += [(TAPER_Y + (bottom - TAPER_Y) * (i + 1) / n, False) for i in range(n)]
    m, c = len(rows), len(S)
    P = np.zeros((m, c, 3))
    for i, (y, wing) in enumerate(rows):
        lim = keel_lim(y)
        P[i, :, 2] = S * WING if wing else np.clip(S / .68, -1, 1) * lim
        P[i, :, 1] = y
    Q = P.reshape(-1, 3)
    pts = np.stack([Q[:, 2] * 1.1, Q[:, 1] * 1.1, np.full(len(Q), seed * 3.7)], axis=1)
    n1 = sk.fbm(pts, .8, 3, seed)
    cy = np.floor(Q[:, 1] / .46)
    cz = np.floor((Q[:, 2] + (cy % 2) * .45) / .9)
    blocky = sk._hash3(cz.astype(np.int64), cy.astype(np.int64), np.zeros(len(Q), dtype=np.int64), seed + 5)
    t = np.clip(-Q[:, 1] / 2.4, 0, 1)
    D = .05 + .22 * n1 * (1 - t) + 1.35 * t * (.12 + .42 * blocky + .46 * n1)
    D = np.clip(D, .02, 1.5)
    P[:, :, 0] = X + s * D.reshape(m, c)
    fb = F['broken']
    for i in range(m - 1):
        for j in range(c - 1):
            facet2(g, tuple(P[i, j]), tuple(P[i, j + 1]), tuple(P[i + 1, j + 1]), tuple(P[i + 1, j]), fb, (-s, -.35, 0))
    # the strips from the whole faces to the break
    for j in range(c - 1):
        facet2(g, tuple(P[0, j]), tuple(P[0, j + 1]), (back, 0.0, P[0, j + 1, 2]), (back, 0.0, P[0, j, 2]), F['flag'], Y)
        facet2(g, tuple(P[-1, j]), tuple(P[-1, j + 1]), (back, bottom, P[-1, j + 1, 2]), (back, bottom, P[-1, j, 2]), F['keel'], (0, -1, 0))
        if j < k1 or j >= k1 + k2:
            facet2(g, tuple(P[2, j]), tuple(P[2, j + 1]), (back, -.6, P[2, j + 1, 2]), (back, -.6, P[2, j, 2]), F['stone'], (0, -1, 0))
    for col, sz in ((0, -1), (c - 1, 1)):
        for i in range(m - 1):
            if i == 2:
                continue
            f = F['carved'] if i < 2 else F['keel']
            facet2(g, tuple(P[i, col]), tuple(P[i + 1, col]), (back, P[i + 1, col, 1], P[i + 1, col, 2]), (back, P[i, col, 1], P[i, col, 2]), f, (0, -.2 if i > 2 else 0, sz))
    # light in the cracks: a few cold veins down the broken face
    if T.detail:
        for v in range(4 if T.fine else 2):
            j = 2 + int(hashf(seed, v, 7) * (c - 5))
            i0 = 4 + int(hashf(seed, v, 8) * 3)
            for i in range(i0, min(m - 2, i0 + 4 + int(hashf(seed, v, 9) * 4))):
                a, b = P[i, j], P[i + 1, j + (1 if hashf(seed, v, i) > .6 and j + 2 < c else 0)]
                w = .035
                g.face(((a[0] - s * .012, a[1], a[2] - w), (a[0] - s * .012, a[1], a[2] + w), (b[0] - s * .012, b[1], b[2] + w), (b[0] - s * .012, b[1], b[2] - w)), F['cold'], normal=(-s, 0, 0))
    return back


def flank(g, fc, a_lo, a_hi, T, seed, top=-.6, string=TAPER_Y, foot=-7.0, hull=True, corbels=True, wall='keel', cornice=CORNICE, piers=None):
    """A flank of the bridge's stone body between a_lo and a_hi along its face: buttress piers (carrying the wing on corbels),
    a blind pointed arch in each bay with a starred roundel, a rune frieze, a string course, the cornice under the deck.
    hull: the body turns in under the string course (the keel); else it stands plumb to `foot`."""
    cell = max(T.cell, 1.6)
    tall = top - string > 4
    fc.quad(g, a_lo, a_hi, string if hull else foot, top, 0, F[wall], cell)
    if hull:
        A, B = fc.P(a_lo, string, 0), fc.P(a_hi, string, 0)
        grid_face(g, fc.P(a_lo, foot, -TAPER), sk._sub(B, A), sk._sub(fc.P(a_lo, string, 0), fc.P(a_lo, foot, -TAPER)), 0, 0, F[wall],
                  sk._norm(sk._add(sk._lerp((0, 0, 0), fc.n, string - foot), Y, -TAPER)), cell)
    fc.mould(g, STRING, string, a_lo, a_hi, F['carved'], T)
    if cornice is not None:
        fc.mould(g, cornice, top, a_lo, a_hi, F['carved'], T)
    length = a_hi - a_lo
    w = .56
    if piers is None:
        n = max(1, int(round(length / 3.85)))
        bay = (length - (n + 1) * w) / n
        piers = [a_lo + w / 2 + i * (bay + w) for i in range(n + 1)]
    H = top - string
    for a in piers:
        fc.buttress(g, a, w, [((string - .32) if hull else foot, string + .38 * H, .44), (string + .38 * H, string + .7 * H, .32), (string + .7 * H, top - .46, .2)], F['stone'], T, cap=F['carved'])
        if corbels:
            ck.bracket(g, fc, a, .44, top, 1.32, 1.5, .2, F['carved'], T)
        if T.detail and hull:
            lathe(g, [(.2, 0), (.16, -.12), (.06, -.3), (.1, -.38), (0, -.52)], fc.P(a, string - .32, .22), F['carved'], T.small + 2)
    fr = top - (1.15 if tall else .8)
    for i in range(len(piers) - 1):
        a0, a1 = piers[i] + w / 2, piers[i + 1] - w / 2
        ac = (a0 + a1) / 2
        half = min(1.2 if tall else .9, (a1 - a0) / 2 - .28)
        ys = string + (1.05 if tall else .55)
        rise = min(half * 1.42, fr - .5 - ys)
        fc.arch_fill(g, ac, half, string + .1, ys, rise, F['deep'], T, d=.004 if T.i < 2 else .02)
        if T.i < 2:
            fc.arch(g, ac, half, ys, rise, ARCHIVOLT, F['carved'], T, legs=ys - string - .1)
            fc.roundel(g, ac, ys + rise * .3, .44 if tall else .34, T, F['carved'], F['cold'], d=.006)
            if T.fine and tall:
                for k in range(3):
                    ck.glyph(g, lambda x, v, k=k: fc.P(ac + x * .16, ys - .3 - k * .3 + v * .24, .012), fc.n, seed * 53 + i * 7 + k, .022, F['runedim'], 3)
            fc.box(g, a0 + .12, a1 - .12, fr - .23, fr + .23, 0, .04, F['carved'], T, bevel=.012)
        fc.runes(g, a0 + .3, a1 - .3, fr, .27, T, F['rune'], seed * 17 + i, d=.047)


def span(g, x0, x1, T, brk_lo, brk_hi, seed, inlays=True, compass=None):
    """A length of the causeway from x0 to x1 (the deck at y 0, 10 m wide over a 6.8 m keel 7 m deep), either end broken."""
    xa = fracture(g, x0, 1, T, seed * 2 + 1) if brk_lo else x0
    xb = fracture(g, x1, -1, T, seed * 2 + 2) if brk_hi else x1
    cell = max(T.cell, 1.7)
    grid_face(g, (xa, 0.0, -WING), (xb - xa, 0, 0), (0, 0, 2 * WING), 0, 0, F['flag'], Y, cell)
    g.face(((xa, -7.0, -KEEL + TAPER), (xb, -7.0, -KEEL + TAPER), (xb, -7.0, KEEL - TAPER), (xa, -7.0, KEEL - TAPER)), F['keel'], (0, -1, 0))
    b = blk(xa, xb, -7, 0, -KEEL, KEEL)
    for key, fc in sides(b).items():
        if key in ('+z', '-z'):
            flank(g, fc, 0, xb - xa, T, seed + (3 if key == '+z' else 9))
    for sz in (-1, 1):
        wb = blk(xa, xb, -.6, 0, min(sz * KEEL, sz * WING), max(sz * KEEL, sz * WING))
        fc = sides(wb)['+z' if sz > 0 else '-z']
        fc.quad(g, 0, xb - xa, -.6, 0, 0, F['carved'], cell)
        fc.mould(g, FASCIA, 0.0, 0, xb - xa, F['carved'], T)
        g.face(((xa, -.6, sz * KEEL), (xb, -.6, sz * KEEL), (xb, -.6, sz * WING), (xa, -.6, sz * WING)), F['stone'], (0, -1, 0))
    if not inlays:
        return
    # the comet's channel: star-silver rails with a dim rune line between them and the lane's glyph tiles
    for sz in (-1, 1):
        z = sz * 1.6
        ck.floor_strip(g, xa, xb, z - .08, z + .08, .008, F['silver'])
        ck.floor_strip(g, xa, xb, z - .022, z + .022, .013, F['runedim'])
        ck.floor_strip(g, xa, xb, sz * 4.5 - .025, sz * 4.5 + .025, .008, F['gold'])
    if compass is not None:
        compass_rose(g, compass, 0.0, 0.0, T, seed)


def compass_rose(g, cx, y, cz, T, seed, r=1.5):
    M = ck.floor_map(cx, y + .009, cz)
    ck.star(g, M, Y, r, r * .2, 4, F['gold'])
    ck.star(g, M, Y, r * .72, r * .17, 4, F['silver'], rot=math.pi / 4)
    ck.floor_ring(g, cx, cz, r * 1.22, .05, y + .009, F['gold'], T.arc * 2)
    ck.floor_ring(g, cx, cz, r * 1.62, .05, y + .009, F['gold'], T.arc * 2)
    ck.floor_rune_ring(g, cx, cz, r * 1.3, .3, y + .012, F['rune'], T, seed)
    if T.detail:
        ck.crystal(g, (cx, y + .01, cz), .1, .05, F['cold'], sides=8, low=.0)


def lane_tiles(g, x0, x1, T):
    """The comet lane's glyph tiles at rest (the rig lights them): where Core NativeCausewayRig.LaneRig puts them."""
    if T.i == 2:
        return
    x = -20 + .625
    k = 0
    while x < 20:
        if x0 <= x <= x1 and abs(x) >= 3.1:
            for e in (-1, 1):
                ck.floor_strip(g, x - .16, x + .16, e * 1.25 - .07, e * 1.25 + .07, .008, F['deep'])
                if T.fine:
                    ck.glyph(g, lambda u, v, x=x, e=e: (x + u * .2, .012, e * 1.25 + v * .11), Y, 900 + k, .014, F['runedim'], 2)
        x += 1.25
        k += 1


# ---------------------------------------------------------------------------------------------------------------------------
# Balustrades and parapets
# ---------------------------------------------------------------------------------------------------------------------------
def rail(g, b, T, lo, hi, seed):
    """A carved balustrade in a rail block: a moulded plinth and a rounded coping, blind pointed arcading on both faces,
    posts with pyramid caps and crystal finials; a broken end (lo, hi) stands toothed, without its post."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    along_x = (x1 - x0) >= (z1 - z0)
    a0, a1 = (x0, x1) if along_x else (z0, z1)
    t = (z1 - z0) if along_x else (x1 - x0)
    c = (z0 + z1) / 2 if along_x else (x0 + x1) / 2
    L = a1 - a0
    jl, jh = (min(.55, L * .3) if lo else 0.0), (min(.55, L * .3) if hi else 0.0)
    body = dict(b)
    body['pos'] = [b['pos'][0], (y0 + .12 + y1 - .1) / 2, b['pos'][2]]
    body['size'] = [b['size'][0], y1 - .1 - y0 - .12, b['size'][2]]
    ck.toothed(g, body, F['stone'], T, lo, hi, seed, course=.29, jag=.55, bevel=0, skip=('-y', '+y'), cell=0)

    def at(u, y, w):
        return (u, y, c + w) if along_x else (c + w, y, u)
    # plinth and coping, swept along the rail (section: across, up)
    h = t / 2
    p0, p1 = a0 + jl * .35, a1 - jh * .35
    sweep(g, ck.simp([(-h - .05, 0), (h + .05, 0), (h + .05, .07), (h + .01, .12), (-h - .01, .12), (-h - .05, .07)], T) if T.i < 2 else [(-h - .04, 0), (h + .04, 0), (h + .04, .12), (-h - .04, .12)],
          [at(p0, y0, 0), at(p1, y0, 0)], F['carved'], path_normals=[Y, Y], smooth_u=False)
    q0, q1 = a0 + jl * .9, a1 - jh * .9
    cope = [(-h - .06, 0), (h + .06, 0), (h + .07, .04), (h + .03, .09), (h - .04, .115), (-h + .04, .115), (-h - .03, .09), (-h - .07, .04)]
    if T.i == 2:
        cope = [(-h - .06, 0), (h + .06, 0), (h + .03, .11), (-h - .03, .11)]
    sweep(g, cope, [at(q0, y1 - .1, 0), at(q1, y1 - .1, 0)], F['carved'], path_normals=[Y, Y], smooth_u=T.i < 2)
    # posts
    us = []
    n = max(1, int(math.ceil((L - .3) / 3.0)))
    for i in range(n + 1):
        u = a0 + .17 + (L - .34) * i / n
        if (i == 0 and lo) or (i == n and hi):
            continue
        us.append(u)
    for u in us:
        pw, pt = .17, h + .055
        if along_x:
            box(g, u - pw, u + pw, y0, y1 + .05, c - pt, c + pt, F['carved'], None, bevel=0, cell=0, skip=('-y', '+y'))
            base = [(u - pw, y1 + .05, c - pt), (u + pw, y1 + .05, c - pt), (u + pw, y1 + .05, c + pt), (u - pw, y1 + .05, c + pt)]
            apex = (u, y1 + .11, c)
        else:
            box(g, c - pt, c + pt, y0, y1 + .05, u - pw, u + pw, F['carved'], None, bevel=0, cell=0, skip=('-y', '+y'))
            base = [(c - pt, y1 + .05, u - pw), (c + pt, y1 + .05, u - pw), (c + pt, y1 + .05, u + pw), (c - pt, y1 + .05, u + pw)]
            apex = (c, y1 + .11, u)
        if T.i < 2:
            for k in range(4):
                facet(g, (base[k], base[(k + 1) % 4], apex), F['carved'], sk._add(sk._sub(sk._lerp(base[k], base[(k + 1) % 4], .5), apex), Y, .5))
        else:
            g.face(base, F['carved'], Y)
        ck.crystal(g, (apex[0], apex[1] + .05, apex[2]), .058, .085, F['crystal'], sides=4, low=.8)
    # the blind arcade between the posts, on both faces
    fs = sides(b)
    for key in (('+z', '-z') if along_x else ('+x', '-x')):
        fc = fs[key]
        flip = key in ('-z', '+x')          # a runs against the block's own axis on these faces
        lo_a, hi_a = (jh, L - jl) if flip else (jl, L - jh)
        stops = sorted([0.0, L] + [(a1 - u) if flip else (u - a0) for u in us])
        for s0, s1 in zip(stops[:-1], stops[1:]):
            e0, e1 = max(s0 + .2, lo_a + .05), min(s1 - .2, hi_a - .05)
            if e1 - e0 < .4:
                continue
            if T.i == 2:
                fc.quad(g, e0, e1, y0 + .3, y1 - .3, .02, F['deep'])
                continue
            m = max(1, int(round((e1 - e0) / .95)))
            pitch = (e1 - e0) / m
            for i in range(m):
                ac = e0 + pitch * (i + .5)
                half = pitch / 2 - .07
                ys = y1 - .2 - half * 1.25
                fc.arch_fill(g, ac, half, y0 + .2, ys, half * 1.25, F['deep'], T, d=.005, n=3 if T.fine else 2)
                if T.fine:
                    fc.arch(g, ac, half, ys, half * 1.25, ARCHLITE, F['carved'], T, legs=ys - y0 - .2, n=3)
    for end, broken, u in ((0, lo, a0), (1, hi, a1)):
        if broken and T.detail:
            p = at(u + (.25 if end == 0 else -.25), y0, 0)
            ck.debris(g, p[0], y0, p[2], .5, 4 if T.fine else 2, seed * 7 + end, size=.14)


# ---------------------------------------------------------------------------------------------------------------------------
# Star-metal pylons
# ---------------------------------------------------------------------------------------------------------------------------
def pylon(g, b, T, seed, lite=False):
    """A star-metal obelisk in a pylon block: a stepped foot, a tapering shaft with silver arrises and a lit crystal seam down
    each face, gold bands, a flared head whose four claws hold the floating crystal (the rig's)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    h = (x1 - x0) / 2
    m, sv, gd = F['metal'], F['silver'], F['gold']
    TB = T if T.i == 0 else None
    box(g, x0, x1, y0, y0 + .22, z0, z1, m, TB, bevel=.03 if TB else 0, cell=0, skip=('-y',))
    box(g, x0 + .07, x1 - .07, y0 + .22, y0 + .42, z0 + .07, z1 - .07, m, TB, bevel=.03 if TB else 0, cell=0, skip=('-y',))
    ya, yb = y0 + .42, y1 - .62
    ha, hb = h - .13, h - .25
    ca, cb = .12, .1
    ck.prism(g, ck.octagon(cx, cz, ha, ca, ya), yb - ya, m, T, cap=False, top_poly=ck.octagon(cx, cz, hb, cb, yb))
    # silver arrises on the four cut corners
    if T.i < 1 or (T.i < 2 and not lite):
        for sx in (-1, 1):
            for sz in (-1, 1):
                p0 = (cx + sx * (ha - ca * .5 + .012), ya, cz + sz * (ha - ca * .5 + .012))
                p1 = (cx + sx * (hb - cb * .5 + .012), yb, cz + sz * (hb - cb * .5 + .012))
                sk.cylinder(g, p0, p1, .034, sv, T.small, caps=False)
    # the lit seam and the bands on each face
    for (nx, nz) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        def P(t, w, d, nx=nx, nz=nz):
            y = ya + (yb - ya) * t
            hh = ha + (hb - ha) * t + d
            return (cx + nx * hh - nz * w, y, cz + nz * hh + nx * w)
        nrm = (nx, .03, nz)
        segs = 6 if T.fine else (3 if T.i == 1 else 1)
        for k in range(segs):
            t0, t1 = .2 + .58 * k / segs + (.012 if segs > 1 else 0), .2 + .58 * (k + 1) / segs - (.012 if segs > 1 else 0)
            g.face((P(t0, -.035, .006), P(t0, .035, .006), P(t1, .035, .006), P(t1, -.035, .006)), F['crystal'], normal=nrm)
        if T.detail:
            ck.glyph(g, lambda u, v, P=P: P(.885 + v * .07, u * .2, .008), nrm, seed * 11 + nx * 3 + nz, .03, F['rune'], 3)
    for t, hh in (((.115, .06), (.985, .07)) if not lite else ()):
        y = ya + (yb - ya) * t
        hw = ha + (hb - ha) * t
        ck.mould_round(g, ck.octagon(cx, cz, hw, ca + (cb - ca) * t, y - hh / 2), [(-.01, 0), (.03, 0), (.045, hh / 2), (.03, hh), (-.01, hh)], gd, T)
    # the head: a flared block with a silver cornice and four claws
    hp = [(hb + .02, 0), (hb + .1, .1), (h - .02, .3), (h + .04, .38), (h + .04, .5), (h - .08, .62)]
    lathe(g, [(r * 1.38, y) for r, y in hp], (cx, yb, cz), m, 4, phase=math.pi / 4, smooth=False, cap1=True)
    if not lite:
        ck.mould_round(g, ck.rect_plan(cx - h - .03, cx + h + .03, cz - h - .03, cz + h + .03, y1 - .22), [(-.02, 0), (.03, 0), (.05, .04), (.03, .09), (-.02, .09)], sv, T)
    for sx in (-1, 1):
        for sz in (-1, 1):
            base = (cx + sx * (h - .2), y1 - .02, cz + sz * (h - .2))
            path = [base, (cx + sx * (h - .1), y1 + .1, cz + sz * (h - .1)), (cx + sx * (h - .16), y1 + .2, cz + sz * (h - .16))]
            if lite:
                continue
            if T.i < 1:
                sweep(g, sk.circle_section(.05, 6), sk.catmull(path, 3), sv, scale=[1, .95, .85, .75, .6, .45, .2], caps=True)
            else:
                ck.crystal(g, (base[0], base[1] + .02, base[2]), .05, .2, sv, sides=4, low=.1)


# ---------------------------------------------------------------------------------------------------------------------------
# The whole static scenery for one tier
# ---------------------------------------------------------------------------------------------------------------------------
def touches(blocks, b, end):
    """Whether another block stands against the low (0) or high (1) end of a rail's long axis (else that end is broken)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    along_x = (x1 - x0) >= (z1 - z0)
    if along_x:
        p = (x0 - .06 if end == 0 else x1 + .06, (y0 + y1) / 2, (z0 + z1) / 2)
    else:
        p = ((x0 + x1) / 2, (y0 + y1) / 2, z0 - .06 if end == 0 else z1 + .06)
    for o in blocks:
        if o is b:
            continue
        a = ext(o)
        if a[0] - 1e-3 <= p[0] <= a[1] + 1e-3 and a[2] - 1e-3 <= p[1] <= a[3] + 1e-3 and a[4] - 1e-3 <= p[2] <= a[5] + 1e-3:
            return True
    return False


STATS = {}


def canonical(b):
    x, z = b['pos'][0], b['pos'][2]
    return x < -1e-6 or (abs(x) < 1e-6 and z > 1e-6)


def half_scene(L, T):
    """The west half (and the pieces on the centre line that own their twins)."""
    import causeway_parts as cp
    g = Geo()
    blocks = L['blocks']
    seed = 0
    for b in blocks:
        i = b['id']
        seed += 1
        if not canonical(b):
            continue
        STATS.setdefault(i.split('-')[1], [0, 0])
        _n = len(g.p) // 3
        if i == 'causeway-span-w':
            x0, x1 = ext(b)[0], ext(b)[1]
            span(g, x0, x1, T, False, True, 1, compass=(x0 + x1) / 2 - 1.5)
            lane_tiles(g, x0, x1 - 1.7, T)
        elif i.startswith('causeway-span-'):
            pass                                             # the wings are the span's
        elif i.startswith('causeway-rail-'):
            rail(g, b, T, not touches(blocks, b, 0), not touches(blocks, b, 1), seed)
        elif i.startswith('causeway-pylon-'):
            pylon(g, b, T, seed)
        else:
            cp.sculpt(g, L, b, T, seed)
        STATS[i.split('-')[1]][0] += 1
        STATS[i.split('-')[1]][1] += len(g.p) // 3 - _n
    if os.environ.get('CW_STATS'):
        print('corners by family (half):', ' '.join('%s:%dx=%d' % (k, v[0], v[1]) for k, v in sorted(STATS.items(), key=lambda q: -q[1][1])))
    STATS.clear()
    return g


def static_scene(L, T):
    import causeway_parts as cp
    h = half_scene(L, T)
    g = Geo()
    g.extend(h)
    g.extend(h.transformed([-1, 0, 0, 0, 0, 1, 0, 0, 0, 0, -1, 0]))
    cp.centre(g, L, T)
    return g


VIEWS = [('wide-sw', (-30, 14, -26), (-4, -1, 0), 60), ('span-east', (-19.4, 1.8, .6), (2, 1.4, -.4), 70), ('breach', (-9, 2.6, -3.2), (1, -1.2, 1), 65),
         ('walk-n', (-13.5, -1.5, 8.9), (4, -2.2, 5.5), 70), ('keel-side', (-2, -1.6, 9.2), (-12, -2.6, 3.4), 62), ('plaza-a', (-21.5, 1.8, -1.5), (-30, 2.6, 2), 75),
         ('hall-a', (-36.5, 1.7, -5.8), (-29, 1.9, 2), 75), ('crown-a', (-37, 6.1, 8.6), (-29, 6.4, -3), 72), ('warden', (-17.2, 1.7, -.6), (-21.9, 2.9, 3.3), 60),
         ('pylon-rail', (-13.6, 1.5, 2.2), (-16.6, 1.5, 5.2), 60), ('under', (-10, -9.5, 13), (-10, -6, 0), 70), ('top', (-12, 26, -.01), (-12, 0, 0), 60),
         ('tower-out', (-50, 3, -20), (-33, 2, 0), 60), ('gate', (-17.4, 1.6, 6.4), (-20.6, 1.8, 7.6), 70),
         ('below', (-26, -16, 30), (-14, -12, 0), 65), ('stump', (-52, -14, 24), (-33, -18, 0), 60), ('lost', (-36, 7, 2), (-80, -4, 2), 50), ('shrine', (30, 4, 100), (40, -4, 150), 50),
         ('colossus', (-55, -6, -100), (-70, -8, -140), 55), ('far-wide', (-90, 60, -150), (0, -10, 0), 60), ('belt', (0, 8, 0), (300, -40, 300), 50)]


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--layout', required=True)
    ap.add_argument('--out', help='Assets/LocalAssets to write the bank into')
    ap.add_argument('--preview', help='render views of the baked tier')
    ap.add_argument('--preview-tier', type=int, default=0)
    ap.add_argument('--views', default='')
    ap.add_argument('--tiers', default='0,1,2')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--static-only', action='store_true')
    ap.add_argument('--no-void', action='store_true')
    args = ap.parse_args(argv)
    L = sk.load_layout(args.layout)
    ck.finishes()
    import causeway_props as props
    import causeway_void as void
    sb = sk.StageBank()
    chunks = sk.Chunks(12.0, (-48.0, -24.0), (-1.0, 9.0))
    t0 = time.time()
    for ti in [int(x) for x in args.tiers.split(',')]:
        T = sk.Tier(ti)
        t = time.time()
        g = static_scene(L, T)
        near = Geo() if args.no_void else void.near(L, T)
        g.extend(near)
        print('tier %s: static %d faces, %.1f s' % (T.name, g.faces, time.time() - t))
        ao = None
        if not args.no_ao:
            ao = sk.bake_ao([g], distance=(1.5, 1.5, 1.8)[ti], samples=(32, 16, 12)[ti])[0]
        bk = sk.Bake(g, ao, floors=FLOORS, seed=3)
        ids = chunks.ids(g)
        for cid in np.unique(ids):
            sb.add('static/%d/%d' % (cid, ti), 'static', sk.to_mesh('static/%d/%d' % (cid, ti), g, bk, ids == cid))
        if args.preview and ti == args.preview_tier:
            views = [v for v in VIEWS if not args.views or v[0] in args.views.split(',')]
            extra = []
            if not args.no_void:
                for name, fg in void.far(L, sk.Tier(1)):
                    extra.append((fg, sk.Bake(fg, None, floors=(), seed=5)))
            sk.preview([(g, bk)] + extra, views, args.preview)
        if not args.static_only:
            props.bank_props(sb, L, T, args.no_ao)
        del g
    if not args.no_void and not args.static_only:
        void.bank_far(sb, L, args.no_ao)
    if args.out:
        for b in L['blocks']:
            sb.cover(b['id'])
        sb.write(args.out, BANK)
    else:
        for (group, tier), (n, tris, verts) in sorted(sb.stats.items()):
            print('bank %-7s %-5s %4d props %8d triangles %8d vertices' % (group, tier, n, tris, verts))
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
