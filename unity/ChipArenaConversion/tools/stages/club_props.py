"""Substation 128's breakable props and its rig (club_art.py runs it): the block models (in each block's own frame, its
centre), the moving parts as props in their own frames, posed each frame by Core NativeSubstationArt from Core NativeClubRig's
outputs, and the rig's fixed fittings for the static scenery (the moving heads' base plates). The Greys: club_greys.py.

Block models (one mesh per kind, shared by its blocks):
  cable-drum table   club-table-*, club-vip-table-*: an old cable drum on its side as a bar table, a lamp on it
  crate              club-crates-*: a slatted pine crate with corner posts and a lid
  kegs               club-kegs-*: eight steel kegs in two layers on boards
  road case          club-case-*, club-backstage-*-cases (two side by side): laminate, aluminium edges, ball corners, latches

The rig frames (Unity space, metres; Core NativeSubstationArt holds the same):
  cones-w, cones-e   a stack's six woofer cones in the world's frame at rest (the pose throws them along -z by the pump);
                     the glow ring round each dust cap is tinted by the build and the burst
  yoke-hang, -stand  a moving head's yoke, origin at the head's pivot, turned by the pan: hung from a base .32 m above, or
                     standing on one .32 m below (the tower tops)
  head               the moving head, origin at its pivot, the beam along +z, the lens at +.25 (its glow the show's colour)
  laser-post         an emitter post, origin on the floor under it when fully risen, its three lenses facing -z
  ball               a mirror ball, origin at its centre (radius the rig's), turned about y
"""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, lathe
import club_kit as ck
from club_kit import F, ext, rod, rbox, glow, ball, disc, dome
import club_greys as greys

IDENT = [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0]


# ---------------------------------------------------------------------------------------------------------------------------
# Block models (centred on the block)
# ---------------------------------------------------------------------------------------------------------------------------
def table(T, size):
    """An old cable drum on its side: two plank flanges with steel hoops and bolts, a slatted core, a lamp on top."""
    g = Geo()
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    R = min(hx, hz) - .01
    n = (20, 12, 8)[T.i]
    wood = F['pinez']
    for (ya, yb) in ((-hy, -hy + .09), (hy - .09, hy)):
        lathe(g, [(R, ya), (R, yb)], (0, 0, 0), wood, n)
        if T.i == 0:
            lathe(g, [(R - .012, ya - .0), (R + .012, ya + .02), (R + .012, yb - .02), (R - .012, yb)], (0, 0, 0), F['steel'], n)
        disc(g, (0, yb, 0), R, wood, n, (0, 1, 0))
        disc(g, (0, ya, 0), R, wood, n, (0, -1, 0))
    lathe(g, [(R * .62, -hy + .09), (R * .62, hy - .09)], (0, 0, 0), F['pinez'], n)
    if T.detail:
        # the core's slats and the top's plank joints, bolts and the middle plate
        for i in range(n):
            a = 2 * math.pi * (i + .5) / n
            c, s = math.cos(a), math.sin(a)
            rod(g, (c * R * .625, -hy + .1, s * R * .625), (c * R * .625, hy - .1, s * R * .625), .012, F['wooddarky'], 4, caps=False)
        for k in range(-3, 4):
            x = k * R * .27
            l = math.sqrt(max(0.0, R * R - x * x)) * .98
            box(g, x - .006, x + .006, hy - .002, hy + .003, -l, l, F['wooddarkz'], None, 0, cell=0)
        lathe(g, [(.12, hy), (.12, hy + .012), (.1, hy + .016)], (0, 0, 0), F['steel'], 8)
        disc(g, (0, hy + .016, 0), .1, F['steel'], 8, (0, 1, 0))
        if T.tiny:
            ck.rivets(g, [(math.cos(a) * R * .8, hy, math.sin(a) * R * .8) for a in [k * math.pi / 3 for k in range(6)]], (0, 1, 0), .022, F['steel'])
    # the lamp: a small glass dome over a warm glow
    if T.i < 2:
        lathe(g, [(.06, hy + .016), (.07, hy + .03), (.05, hy + .04)], (0, 0, 0), F['brass'], 8)
        dome(g, (0, hy + .04, 0), .05, glow(0xFFB070, 2.2), 8, 3, squash=1.3)
    return g


def crate(T, size):
    """A slatted pine crate: corner posts, boards with gaps (a dark lining behind them), a diagonal brace, a boarded lid."""
    g = Geo()
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    if T.i == 2:
        box(g, -hx, hx, -hy, hy, -hz, hz, F['pinex'], None, 0, cell=0)
        return g
    box(g, -hx + .035, hx - .035, -hy + .02, hy - .03, -hz + .035, hz - .035, F['black'], None, 0, cell=0)
    bev = .008 if T.i == 0 else 0.0
    TT = T if T.i == 0 else None
    for sx in (-1, 1):
        for sz in (-1, 1):
            box(g, sx * hx - sx * .08, sx * hx, -hy, hy - .03, sz * hz - sz * .08, sz * hz, F['piney'], TT, bev, cell=0)
    nb = 5
    for k in range(nb):
        ya = -hy + .03 + (2 * hy - .09) * k / nb
        yb = ya + (2 * hy - .09) / nb - .035
        for sz in (-1, 1):
            box(g, -hx + .08, hx - .08, ya, yb, sz * hz - sz * .03, sz * hz - sz * .006, F['pinex'], TT, bev, cell=0)
        for sx in (-1, 1):
            box(g, sx * hx - sx * .03, sx * hx - sx * .006, ya, yb, -hz + .08, hz - .08, F['pinez'], TT, bev, cell=0)
    for k in range(6):
        za = -hz + 2 * hz * k / 6 + .008
        box(g, -hx - .01, hx + .01, hy - .03, hy, za, za + 2 * hz / 6 - .016, F['pinex'], TT, bev, cell=0)
    if T.detail:
        for sz in (-1, 1):
            sk.sweep(g, sk.rect_section(.09, .02), [(-hx + .1, -hy + .08, sz * hz), (hx - .1, hy - .1, sz * hz)], F['pinex'], up=(0, 0, 1), smooth_u=False)
    return g


def kegs(T, size):
    """Eight steel kegs, two layers of four with boards between: rolled chimes, two ribs, a slight belly, a valve on top."""
    g = Geo()
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    r = min(hx, hz) / 2 - .012
    tall = hy - .02
    n = (14, 8, 6)[T.i]
    prof = [(r * .86, 0.0), (r * .9, .012), (r * .88, .04), (r * .97, .07), (r, tall * .3), (r * 1.02, tall * .33), (r, tall * .36), (r, tall * .64),
            (r * 1.02, tall * .67), (r, tall * .7), (r * .97, tall - .07), (r * .88, tall - .04), (r * .9, tall - .012), (r * .86, tall)]
    if T.i:
        prof = [prof[0], prof[3], prof[4], prof[9], prof[10], prof[13]]
    for layer in (0, 1):
        y0 = -hy + layer * (hy + .0) + (.02 if layer else 0)
        for sx in (-1, 1):
            for sz in (-1, 1):
                c = (sx * hx / 2, y0, sz * hz / 2)
                lathe(g, prof, c, F['keg'], n)
                disc(g, (c[0], y0 + tall - .03, c[2]), r * .87, F['keg'], n, (0, 1, 0))
                lathe(g, [(r * .86, tall), (r * .86, tall - .03)], c, F['keg'], n)
                if T.i < 2 and layer == 1:
                    lathe(g, [(.03, tall - .03), (.03, tall - .005), (.02, tall)], c, F['steeldark'], 6, cap1=True)
        if layer == 1:
            box(g, -hx, hx, y0 - .02, y0, -hz, hz, F['pinex'], T, bevel=.006, cell=0)
    return g


def case(T, size):
    """A road case on casters: black laminate, aluminium edge extrusions, steel ball corners, a lid seam, butterfly latches,
    recessed handles. A wide block holds two side by side."""
    g = Geo()
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    count = 2 if hx > .9 else 1
    for ci in range(count):
        x0 = -hx + ci * (2 * hx / count) + (.01 if count > 1 else 0)
        x1 = x0 + 2 * hx / count - (.02 if count > 1 else 0)
        y0, y1, z0, z1 = -hy + .09, hy, -hz, hz
        e = .028
        box(g, x0 + .006, x1 - .006, y0 + .006, y1 - .006, z0 + .006, z1 - .006, F['case'], None, 0, cell=T.cell * 2 if T.i == 0 else 0)
        if T.i == 2:
            continue
        TT = T if T.i == 0 else None
        bev = .006 if T.i == 0 else 0
        for (ya, yb) in ((y0, y0 + e), (y1 - e, y1)):
            for (za, zb) in ((z0, z0 + e), (z1 - e, z1)):
                box(g, x0, x1, ya, yb, za, zb, F['alu'], TT, bev, cell=0)
            for (xa, xb) in ((x0, x0 + e), (x1 - e, x1)):
                box(g, xa, xb, ya, yb, z0, z1, F['alu'], TT, bev, cell=0)
        for (xa, xb) in ((x0, x0 + e), (x1 - e, x1)):
            for (za, zb) in ((z0, z0 + e), (z1 - e, z1)):
                box(g, xa, xb, y0, y1, za, zb, F['alu'], TT, bev, cell=0)
        ys = y0 + (y1 - y0) * .68                              # the lid's seam: a twin extrusion round the case
        for (za, zb) in ((z0 - .002, z0 + .012), (z1 - .012, z1 + .002)):
            box(g, x0 + e, x1 - e, ys - .022, ys + .022, za, zb, F['alu'], None, 0, cell=0)
        for (xa, xb) in ((x0 - .002, x0 + .012), (x1 - .012, x1 + .002)):
            box(g, xa, xb, ys - .022, ys + .022, z0 + e, z1 - e, F['alu'], None, 0, cell=0)
        for sx in (x0, x1):
            for sy in (y0, y1):
                for sz in (z0, z1):
                    ball(g, (sx + (.02 if sx == x0 else -.02), sy + (.02 if sy == y0 else -.02), sz + (.02 if sz == z0 else -.02)), .042, F['steel'], 6, 4)
        if T.detail:
            for sz in (z0, z1):
                s = -1 if sz == z0 else 1
                for fx in (.28, .72):
                    x = x0 + (x1 - x0) * fx
                    box(g, x - .06, x + .06, ys - .07, ys + .07, sz - s * .0, sz + s * .012, F['steeldark'], None, 0, cell=0)
                    box(g, x - .035, x + .035, ys - .03, ys + .03, sz + s * .012, sz + s * .022, F['chrome'], None, 0, cell=0)
            for sx in (x0, x1):
                s = -1 if sx == x0 else 1
                box(g, sx, sx + s * .01, y0 + (y1 - y0) * .32, y0 + (y1 - y0) * .52, -.11, .11, F['steeldark'], None, 0, cell=0)
                rod(g, (sx + s * .024, y0 + (y1 - y0) * .42, -.08), (sx + s * .024, y0 + (y1 - y0) * .42, .08), .012, F['chrome'], 4)
            for sx in (x0 + .12, x1 - .12):
                for sz in (z0 + .12, z1 - .12):
                    rod(g, (sx - .025, -hy + .05, sz), (sx + .025, -hy + .05, sz), .05, F['rubber'], 8)
                    box(g, sx - .04, sx + .04, -hy + .085, -hy + .1, sz - .04, sz + .04, F['steeldark'], None, 0, cell=0)
    return g


def block_kind(i):
    if 'table' in i:
        return 'table'
    if 'crates' in i:
        return 'crate'
    if 'kegs' in i:
        return 'kegs'
    return 'case'


MAKE = {'table': table, 'crate': crate, 'kegs': kegs, 'case': case}


def block_models(L, T):
    """{(kind, size): (Geo, [block ids])} for the breakable blocks."""
    out = {}
    for b in L['blocks']:
        if b['role'] == 'static':
            continue
        key = (block_kind(b['id']), tuple(round(v, 3) for v in b['size']))
        if key not in out:
            out[key] = (MAKE[key[0]](T, b['size']), [], b)
        out[key][1].append(b['id'])
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# The rig
# ---------------------------------------------------------------------------------------------------------------------------
def cones(T, L, side):
    """A stack's six woofers (world frame): the surround's roll, the cone falling back to the dust cap, a glow ring round it."""
    C = L['club']
    g = Geo()
    x0, x1 = side * C['speakerX0'], side * C['speakerX1']
    cx, front, cab = (x0 + x1) / 2, C['speakerZ'], C['speakerTop'] / 4
    n = (18, 10, 8)[T.i]
    for k in range(3):
        y = (k + .5) * cab
        for dx in (-.66, .66):
            c = (cx + dx, y, front - .02)
            # heights along +z (into the cabinet); the listener is at -z
            surround = [(.415, .03), (.4, .004), (.375, -.006), (.35, .004), (.34, .024)] if T.i == 0 else [(.415, .03), (.38, -.004), (.34, .024)]
            lathe(g, surround[::-1], c, F['rubber'], n, axis=(0, 0, 1))
            lathe(g, [(.11, .15), (.34, .024)], c, F['cone'], n, axis=(0, 0, 1))
            lathe(g, [(.11, .127), (.148, .106)], c, glow(0xFFFFFF, 1.0), n, axis=(0, 0, 1))
            cap = [(1e-4, .075), (.05, .082), (.09, .104), (.11, .13)]
            lathe(g, cap, c, F['gunmetal'], n, axis=(0, 0, 1))
    return g


def yoke(T, stand=False):
    """A moving head's yoke: the neck under the base, the cross arm, the two arms down to the pivot caps."""
    g = Geo()
    f = F['black']
    segs = 1
    rbox(g, -.235, .235, .24, .3, -.075, .075, .025, f, segs)
    for sx in (-1, 1):
        rbox(g, sx * .2 - .034, sx * .2 + .034, -.07, .27, -.06, .06, .025, f, segs)
        if T.i < 2:
            lathe(g, [(.045, .0), (.045, .014), (.03, .02)], (sx * .234, 0, 0), F['chrome'], T.small + 2, axis=(sx, 0, 0), cap1=True)
    if stand:
        return g.transformed([-1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1, 0])
    return g


def moving_head(T):
    """The head: a ribbed barrel between the yoke's arms, a rear cap with a fan grille, the front bezel and the lens."""
    g = Geo()
    n = (16, 10, 6)[T.i]
    prof = [(1e-4, -.225), (.07, -.22), (.11, -.19), (.15, -.15), (.158, -.1), (.158, .12), (.165, .14), (.165, .2), (.15, .235), (.135, .246)]
    if T.i:
        prof = [prof[0], prof[2], prof[4], prof[5], prof[7], prof[9]]
    lathe(g, prof, (0, 0, 0), F['blackgloss'], n, axis=(0, 0, 1))
    lathe(g, [(.135, .246), (.125, .236)], (0, 0, 0), F['chrome'], n, axis=(0, 0, 1))
    disc(g, (0, 0, .237), .125, glow(0xFFFFFF, 1.0), n, (0, 0, 1))
    if T.detail:
        for z in (-.07, -.02, .03, .08):
            lathe(g, [(.158, z - .012), (.166, z - .006), (.166, z + .006), (.158, z + .012)], (0, 0, 0), F['gunmetal'], n, axis=(0, 0, 1))
    return g


def laser_post(T):
    g = Geo()
    rbox(g, -.12, .12, -.03, .63, -.12, .12, .03, F['black'], 1)
    rbox(g, -.15, .15, .63, .69, -.15, .15, .02, F['gunmetal'], 1)
    n = T.small + 2 if T.i < 2 else 5
    for y in (.15, .35, .55):
        lathe(g, [(.06, .0), (.06, .02), (.045, .028)], (0, y, -.118), F['chrome'], n, axis=(0, 0, -1))
        disc(g, (0, y, -.147), .045, glow(0xFFFFFF, 1.0), n, (0, 0, -1))
    if T.detail:
        for sx in (-1, 1):
            box(g, sx * .121, sx * .124, .05, .58, -.03, .03, F['hazard'], None, 0, cell=0)
    return g


def mirror_ball(T, R):
    """A mirror ball: rows of flat mirror facets round a sphere (each face flat, so each throws its own glint), a cap and a ring."""
    g = Geo()
    rows, per = ((16, 30), (10, 18), (6, 10))[T.i]
    for i in range(rows):
        la0, la1 = -math.pi / 2 + math.pi * i / rows, -math.pi / 2 + math.pi * (i + 1) / rows
        n = max(5, int(round(per * math.cos((la0 + la1) / 2))))
        off = i * .37
        for j in range(n):
            lo0, lo1 = off + 2 * math.pi * j / n, off + 2 * math.pi * (j + 1) / n
            pts = [(R * math.cos(la) * math.cos(lo), R * math.sin(la), R * math.cos(la) * math.sin(lo)) for (la, lo) in ((la0, lo0), (la0, lo1), (la1, lo1), (la1, lo0))]
            lm, om = (la0 + la1) / 2, (lo0 + lo1) / 2
            nrm = (math.cos(lm) * math.cos(om), math.sin(lm), math.cos(lm) * math.sin(om))
            g.face(pts, F['mirrorball'], nrm)
    lathe(g, [(.1, R * .985), (.09, R + .05), (.02, R + .07)], (0, 0, 0), F['steeldark'], 8, cap1=True)
    return g


def fittings(g, L, T):
    """The rig's fixed fittings in the static scenery: each moving head's base plate and its riser."""
    for h in L['heads']:
        x, y, z = h['at']
        up = -1 if h['top'] else 1
        ya, yb = sorted((y + up * .3, y + up * .37))
        rbox(g, x - .16, x + .16, ya, yb, z - .16, z + .16, .03, F['black'], 1)
        if T.detail:
            yc, yd = sorted((y + up * .37, y + up * .55))
            rod(g, (x, yc, z), (x, yd, z), .035, F['gunmetal'], T.small, caps=False)


RIG = (('cones-w', lambda T, L: cones(T, L, -1), 'world'), ('cones-e', lambda T, L: cones(T, L, 1), 'world'),
       ('yoke-hang', lambda T, L: yoke(T), (-7.5, 9.64, 8.2)), ('yoke-stand', lambda T, L: yoke(T, True), (-9.8, 11.11, -8.2)),
       ('head', lambda T, L: moving_head(T), (-4.5, 9.64, 8.2)), ('laser-post', lambda T, L: laser_post(T), (-9.0, 0, 7.75)),
       ('ball', lambda T, L: mirror_ball(T, L['club']['ballRadius']), (-6.2, 7.4, 0)),
       ('dj-legs', lambda T, L: greys.dj_legs(T), (0, 1.0, 17.0)), ('dj-torso', lambda T, L: greys.dj_torso(T), (0, 2.25, 17.0)),
       ('dj-head', lambda T, L: greys.dj_head(T), (0, 2.73, 16.96)), ('dj-arm-ear', lambda T, L: greys.dj_arm_ear(T), (.26, 2.5, 16.95)),
       ('dj-upper', lambda T, L: greys.dj_upper(T), (-.27, 2.55, 17.0)), ('dj-fore', lambda T, L: greys.dj_fore(T), (-.27, 2.3, 16.9)))


def rig_items(L, T):
    items = [(name, make(T, L), rest) for (name, make, rest) in RIG]
    for v in range(int(L['club']['variants'])):
        items.append(('grey-legs-%d' % v, greys.crowd_legs(T, v), (-27.8, 8.4, 0)))
        items.append(('grey-body-%d' % v, greys.crowd_body(T, v), (-27.8, 9.24, 0)))
        items.append(('grey-arm-%d' % v, greys.crowd_arm(T, v), (-27.8, 9.74, .24)))
    return items


def rest_scene(L, T):
    """The breakable props on their blocks and the rig at rest, in the world: the static bake's occluders and the previews."""
    g = Geo()
    for (kind, size), (geo, ids, b0) in block_models(L, T).items():
        blocks = {b['id']: b for b in L['blocks']}
        for i in ids:
            p = blocks[i]['pos']
            g.extend(geo.transformed([1, 0, 0, p[0], 0, 1, 0, p[1], 0, 0, 1, p[2]]))
    C = L['club']
    for side in (-1, 1):
        g.extend(cones(T, L, side))
        g.extend(mirror_ball(T, C['ballRadius']).transformed([1, 0, 0, side * C['ballX'], 0, 1, 0, C['ballY'], 0, 0, 1, C['ballZ']]))
    yk, ys, hd = yoke(T), yoke(T, True), moving_head(T)
    for i, h in enumerate(L['heads']):
        x, y, z = h['at']
        g.extend((ys if h['top'] else yk).transformed([1, 0, 0, x, 0, 1, 0, y, 0, 0, 1, z]))
        m = ck.mat_rot((1, 0, 0), math.pi / 2 - .5 + .2 * (i % 3))
        g.extend(hd.transformed([m[0][0], m[0][1], m[0][2], x, m[1][0], m[1][1], m[1][2], y, m[2][0], m[2][1], m[2][2], z]))
    g.extend(greys.dj_figure(T).transformed([1, 0, 0, C['djX'], 0, 1, 0, C['stageY'], 0, 0, 1, C['djZ']]))
    figs = {}
    for k, (x, y, z, face, v) in enumerate(L['crowd']):
        v = int(v)
        up = k % 3 == 0
        key = (v, up)
        if key not in figs:
            figs[key] = greys.crowd_figure(T, v, up)
        c, s = math.cos(face), math.sin(face)
        g.extend(figs[key].transformed([c, 0, s, x, 0, 1, 0, y, -s, 0, c, z]))
    return g


def bank_props(sb, L, T, floors, no_ao):
    ti = T.i
    models = block_models(L, T)
    items = []
    for (kind, size), (geo, ids, b0) in models.items():
        items.append(('block', geo, tuple(b0['pos']), ids))
    for (name, geo, rest) in rig_items(L, T):
        items.append(('rig/' + name, geo, rest, None))
    aos = [None] * len(items)
    if not no_ao:
        # each prop baked on its own (set apart, so they do not shade one another)
        apart = [it[1].transformed([1, 0, 0, 40.0 * (i % 8), 0, 1, 0, 0, 0, 0, 1, 40.0 * (i // 8)]) for i, it in enumerate(items)]
        aos = sk.bake_ao(apart, distance=.35, samples=(24, 12, 8)[ti])
    for (name, g, rest, ids), ao in zip(items, aos):
        if g.faces == 0:
            continue
        P = g.arrays()[0].astype(np.float64)
        world = P if rest == 'world' else P + np.array(rest)
        bk = sk.Bake(g, ao, world=world, soot=None, floors=floors, seed=7)
        if ids is not None:
            om = sk.to_mesh('block/%s/%d' % (ids[0], ti), g, bk)
            mi = sb.mesh(om)
            for i in ids:
                sb.share('block/%s/%d' % (i, ti), 'block', mi)
            st = sb.stats.setdefault(('block', str(ti)), [0, 0, 0])
            st[0] += 1
            st[1] += sum(len(s) for s in om.submeshes) // 3
            st[2] += len(om.positions)
        else:
            sb.add('%s/%d' % (name, ti), 'rig', sk.to_mesh('%s/%d' % (name, ti), g, bk))
