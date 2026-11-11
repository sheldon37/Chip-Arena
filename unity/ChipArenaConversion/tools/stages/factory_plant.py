"""The Factory's plant (docs/arenas/FACTORY_PROPOSAL.md "Stage art"): the machines (lathes, machining centres, switch cabinets,
the steam hammers), the stock (bar racks, coils, billets, beams, ingots, plates, ingot moulds), the rooms (tool cribs, shelving,
blockwork screens, parts bins) and the docks (site offices, containers, forklifts), and the ladle on its transfer car. Each is
modelled on its map block as the north / A piece (factory_art.canonical turns the south / B ones half round), so the operator's
side, the doors and the forks face as the kit's do. A block is cover: every model fills its box, so nothing that stops a shot is
left unseen. Used by factory_art.py."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
import factory_kit as fk  # noqa: E402
from stagekit import Geo, box, lathe  # noqa: E402
from factory_kit import mat, tint, lamp, ext, bx, cyl, tube, ibeam, bar, kit_part, kname, body, stripes, striped_box, corrugated, Y  # noqa: E402

BL = {}


def rnd(*k):
    """A hash in 0..1 of integers."""
    h = 2166136261
    for v in k:
        h = ((h ^ (int(v) & 0xffffffff)) * 16777619) & 0xffffffff
    h ^= h >> 15
    h = (h * 0x2c1b3c6d) & 0xffffffff
    h ^= h >> 12
    return (h & 0xffffff) / 16777216.0


def rest(g, parts, T, skip=()):
    for p in parts:
        if kname(p) not in skip:
            kit_part(g, p, T)


# ---------------------------------------------------------------------------------------------------------------------------
# Machines
# ---------------------------------------------------------------------------------------------------------------------------
def lathe_bed(g, b, parts, T, L):
    x0, x1, y0, y1, z0, z1 = ext(b)
    gr, g2 = mat('green'), mat('green2')
    bx(g, x0, x0 + 1.35, 0, .8, z0, z1, gr, T, .04, 0, ('-y',))
    bx(g, x1 - 1.0, x1, 0, .8, z0, z1, gr, T, .04, 0, ('-y',))
    bx(g, x0 + 1.35, x1 - 1.0, .12, .8, z0 + .1, z1 - .1, mat('iron'), T, .02, 0, ('-y', '-x', '+x'))
    bx(g, x0, x1, .78, y1, z0 + .04, z1 - .04, g2, T, .035, 0)
    if T.i < 2:
        # pedestal doors, the chip pan's lip
        for (xa, xb) in ((x0 + .15, x0 + 1.2), (x1 - .88, x1 - .12)):
            bx(g, xa, xb, .12, .68, z0 - .012, z0, tint('green', 0x466E5F), T, .008, 0, ('+z',))
            cyl(g, ((xa + xb) / 2, .5, z0 - .012), ((xa + xb) / 2, .5, z0 - .04), .025, mat('chrome'), T.small)
        bx(g, x0 + 1.35, x1 - 1.0, .74, .8, z0 + .02, z1 - .02, mat('iron'), T, .012, 0, ('-y',))
    cz = (z0 + z1) / 2
    for p in parts:
        name = kname(p)
        kit_part(g, p, T)
        if name == 'machine' and T.i < 2 and p['h'][2] > .5:
            # the carriage: its apron down the operator's side and two handwheels
            c, h = p['c'], p['h']
            bx(g, c[0] - h[0] * .8, c[0] + h[0] * .8, y1 - .34, y1, z0 - .05, z0 + .04, g2, T, .015)
            for dx in (-.14, .16):
                cyl(g, (c[0] + dx, y1 - .17, z0 - .05), (c[0] + dx, y1 - .17, z0 - .1), .075, mat('black'), T.round)
            # the tool post and its tool
            bx(g, c[0] - .08, c[0] + .08, c[1] + h[1], c[1] + h[1] + .09, cz - .2, cz - .04, mat('iron'), T, .01)


def lathe_head(g, b, parts, T, L):
    x0, x1, y0, y1, z0, z1 = ext(b)
    gr = mat('green')
    bx(g, x0, x1, y0, y1, z0, z1, gr, T, .07, 0)
    cz = (z0 + z1) / 2
    if T.i < 2:
        # the gear cover on the outer end and the spindle nose toward the bed
        bx(g, x0 - .03, x0, y0 + .08, y1 - .1, z0 + .12, z1 - .12, tint('green', 0x466E5F), T, .015, 0, ('+x',))
        cyl(g, (x1, (y0 + y1) / 2, cz), (x1 + .03, (y0 + y1) / 2, cz), .26, mat('iron'), T.round + 2)
        for k in range(2):
            cyl(g, (x0 + .5 + k * .28, y1 - .2, z0), (x0 + .5 + k * .28, y1 - .2, z0 - .06), .04, mat('black'), T.small + 1)
    for p in parts:
        if kname(p) == 'chrome' and p['h'][0] < .1:
            continue            # the chuck turns: it is the rig's
        kit_part(g, p, T)


def mill(g, b, parts, T, L):
    """A machining centre: its enclosure with the sliding door and window on the operator's side, the head's housing on top."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    gr, cab = tint('green2', 0x557F70), mat('cabinet')
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    bx(g, x0, x1, 0, 2.02, z0, z1, gr, T, .05, 0, ('-y',))
    bx(g, x0 + .22, x1 - .22, 2.0, y1, z0 + .22, z1 - .22, cab, T, .05, 0, ('-y',))
    if T.i < 2:
        fz = z0
        # the sliding door, its window and handle; the fixed light beside it
        bx(g, cx - .95, cx + .1, .42, 1.9, fz - .03, fz, cab, T, .015, 0, ('+z',))
        bx(g, cx - .8, cx - .05, 1.0, 1.75, fz - .042, fz - .03, mat('glass'), T, .008, 0, ('+z',))
        tube(g, [(cx + .02, .95, fz - .07), (cx + .02, 1.45, fz - .07)], .016, mat('chrome'), T.small)
        bx(g, x0 + .06, cx - 1.0, .2, 1.9, fz - .012, fz, tint('green2', 0x4B7364), T, .006, 0, ('+z',))
        # side and back panels, louvres on the back
        for (xa, s) in ((x0, -1), (x1, 1)):
            for (za, zb) in ((z0 + .12, cz - .04), (cz + .04, z1 - .12)):
                bx(g, min(xa, xa + s * .012), max(xa, xa + s * .012), .25, 1.85, za, zb, tint('green2', 0x4B7364), T, .006, 0, ('+x',) if s < 0 else ('-x',))
        if T.tiny:
            for k in range(7):
                y = .5 + k * .12
                bx(g, cx - .7, cx + .7, y, y + .05, z1, z1 + .02, mat('black'), T, .006, 0, ('-z',))
        # the chip conveyor's chute at the foot
        bx(g, x1 - .5, x1 - .08, .0, .32, fz - .18, fz, mat('iron'), T, .02, 0, ('-y', '+z'))
    for p in parts:
        name = kname(p)
        if name == 'black' and abs(p['q'][3]) < .999:
            c = p['c']
            cyl(g, (c[0], c[1], c[2] - .02), (c[0], c[1], c[2] + .02), .12, mat('black'), T.round)
            continue
        if name == 'lamp' and p['h'][0] > .2:
            kit_part(g, p, T, mat('screen'))
            continue
        kit_part(g, p, T)


def cabinet(g, b, parts, T, L):
    x0, x1, y0, y1, z0, z1 = ext(b)
    bx(g, x0, x1, .1, y1, z0, z1, mat('cabinet'), T, .025, 0, ('-y',))
    bx(g, x0 + .03, x1 - .03, 0, .1, z0 + .03, z1 - .03, mat('black'), T, 0, 0, ('-y', '+y'))
    if T.tiny:
        # door louvres low on each bay
        x = x0 + .12
        while x < x1 - .5:
            for k in range(4):
                bx(g, x + .1, x + .5, .3 + k * .06, .33 + k * .06, z0 - .012, z0, tint('cabinet', 0x77838A), T, 0, 0, ('+z',))
            x += .75
    rest(g, parts, T)


def hammer(g, b, parts, T, L):
    """A steam hammer: two cast frames, the crosshead, the tup in its guides over the anvil, the cylinder on top."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    gr, ir = mat('green'), mat('iron')
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    cell = 1.6 if T.i == 0 else 0
    for (xa, xb) in ((x0, x0 + .8), (x1 - .8, x1)):
        bx(g, xa, xb, 0, 2.7, z0 + .12, z1 - .12, gr, T, .09, cell, ('-y',))
    bx(g, x0, x1, 2.6, y1, z0, z1, gr, T, .1, cell)
    # the anvil and its die, the tup and its rod
    bx(g, x0 + .8, x1 - .8, 0, 1.0, z0 + .25, z1 - .25, ir, T, .06, 0, ('-y', '-x', '+x'))
    bx(g, cx - .45, cx + .45, 1.0, 1.16, cz - .7, cz + .7, mat('bright'), T, .03, 0, ('-y',))
    bx(g, x0 + .8, x1 - .8, 1.5, 2.45, cz - 1.1, cz + 1.1, tint('iron', 0x33302F), T, .06, 0, ('-x', '+x'))
    bx(g, cx - .4, cx + .4, 1.36, 1.5, cz - .65, cz + .65, mat('bright'), T, .03, 0, ('+y',))
    cyl(g, (cx, 2.45, cz), (cx, 2.62, cz), .16, mat('chrome'), T.round)
    if T.i < 2:
        for zz in (cz - 1.1, cz + 1.1):
            for xx in (x0 + .8, x1 - .8):
                s = 1 if xx < cx else -1
                bx(g, min(xx, xx + s * .05), max(xx, xx + s * .05), 1.05, 2.6, zz - .08, zz + .08, mat('chrome'), T, .01, 0)
    # the cylinder, its flanges and steam chest
    n = T.round + 4
    lathe(g, [(.7, 0.0), (.7, .08), (.6, .1), (.6, 1.42), (.72, 1.44), (.72, 1.56), (.5, 1.62), (.2, 1.72), (0.0, 1.74)], (cx, y1, cz), gr, n)
    bx(g, cx - .32, cx + .32, y1 + .3, y1 + 1.2, cz + .55, cz + .86, gr, T, .04)
    fk.bolts(g, [(cx + .65 * math.cos(a), y1 + 1.56, cz + .65 * math.sin(a)) for a in [k * math.pi / 6 for k in range(12)]], mat('black'), T, .03, .03)
    for p in parts:
        name = kname(p)
        if name == 'machine' or (name == 'black' and p['c'][1] > y1):
            continue
        kit_part(g, p, T)


# ---------------------------------------------------------------------------------------------------------------------------
# Stock
# ---------------------------------------------------------------------------------------------------------------------------
BAR_FINS = ('bright', 'billet', 'oxide', 'billet', 'bright')


def rack(g, b, parts, T, L):
    """A cantilever rack loaded with bar stock on every arm (it is cover: the load fills it)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    rest(g, parts, T, skip=('bar',))
    n = 6
    r = (z1 - z0 - .1) / n / 2
    seed = int(abs(x0) * 10 + abs(z0) * 7)
    sides = (T.small + 2, 5, 4)[T.i]
    for li, y in enumerate((.1, .75, 1.5, 2.25)):
        if y + r > y1:
            break
        for layer in range(2):
            cnt = n - layer
            yy = y + r + .005 + layer * r * 1.74
            if yy + r > (y1 + .12 if li == 3 else (.75, 1.5, 2.25, 9)[li] - .07):
                break
            for k in range(cnt):
                z = z0 + .05 + r + k * 2 * r + layer * r
                d = (rnd(seed, li, layer, k) - .5) * .16
                f = mat(BAR_FINS[int(rnd(seed, li, layer, k, 7) * 5) % 5])
                cyl(g, (x0 - .02 + d, yy, z), (x1 + .02 + d, yy, z), r * .97, f, sides)


def coils(g, b, parts, T, L):
    """Steel coils on their sides in a timber cradle: two rows of two, strapped."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    rest(g, parts, T, skip=('coil', 'black', 'strap'))
    cz = (z0 + z1) / 2
    R = min((y1 - .2) / 2, (z1 - z0) / 4 - .01)
    n = (T.round + 6, 12, 8)[T.i]
    f, dk = mat('bright'), mat('black')
    wlen = (x1 - x0 - .12) / 2
    for i in range(2):
        xa = x0 + .03 + i * (wlen + .06)
        for zc in (cz - R - .01, cz + R + .01):
            c = (xa, .19 + R, zc)
            prof = [(R * .36, .02), (R * .4, 0.0), (R - .02, 0.0), (R, .02), (R, wlen - .02), (R - .02, wlen), (R * .4, wlen), (R * .36, wlen - .02), (R * .36, .02)]
            lathe(g, prof, c, f, n, axis=(1, 0, 0))
            if T.i < 2:
                for u in (.3, .7):
                    lathe(g, [(R + .004, wlen * u - .03), (R + .012, wlen * u - .02), (R + .012, wlen * u + .02), (R + .004, wlen * u + .03)], c, dk, n, axis=(1, 0, 0))
            if T.tiny:
                for end, nx in ((0.0, -1), (wlen, 1)):
                    for rr in (.55, .7, .85):
                        fk.ring(g, (c[0] + end + nx * .002, c[1], c[2]), R * rr, R * rr + .012, tint('bright', 0x6E6A66), n, (nx, 0, 0))
    # chocks
    if T.i < 2:
        for zc in (z0 + .02, cz - .09, z1 - .2):
            bx(g, x0 + .1, x1 - .1, .18, .3, zc, zc + .18, mat('timber'), T, .012, 0, ('-y',))


def billets(g, b, parts, T, L):
    """A stack of square billets, each a little out of line."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    rest(g, parts, T, skip=('bar', 'joint'))
    cell = .14
    nz = int(round((z1 - z0) / cell))
    ny = int(round((y1 - .1) / cell))
    cz_, cy_ = (z1 - z0) / nz, (y1 - .1) / ny
    fa, fb = tint('billet', 0x7A6E64), tint('billet', 0x6A625C)
    if T.i == 0:
        for j in range(ny):
            for k in range(nz):
                if k not in (0, nz - 1) and j not in (ny - 1,) and (rnd(j, k, 3) > 2):
                    continue
                d = (rnd(j, k, 1) - .5) * .1
                box(g, x0 + .03 + d, x1 - .03 + d, .1 + j * cy_ + .005, .1 + (j + 1) * cy_ - .005, z0 + k * cz_ + .005, z0 + (k + 1) * cz_ - .005, fa if rnd(j, k, 2) > .4 else fb, T, 0, 0,
                    skip=() if j == ny - 1 else ('+y', '-y'))
    else:
        for j in range(ny if T.i == 1 else 1):
            n = ny if T.i == 1 else 1
            d = (rnd(j, 5) - .5) * .06
            box(g, x0 + .03 + d, x1 - .03 + d, .1 + (y1 - .1) * j / n, .1 + (y1 - .1) * (j + 1) / n - (.012 if T.i == 1 else 0), z0, z1, fa if j % 2 else fb, T, 0, 0)


def beams(g, b, parts, T, L):
    """A bundle of I-beams (red oxide) on dunnage, strapped."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    rest(g, parts, T, skip=('load',))
    f = mat('oxide')
    nz, ny = 3, 3
    w, h = (z1 - z0) / nz, (y1 - y0) / ny
    for j in range(ny):
        for k in range(nz):
            d = (rnd(j, k, 9) - .5) * .12
            y = y0 + h * (j + .5)
            z = z0 + w * (k + .5)
            if T.i == 2:
                continue
            ibeam(g, (x0 + .04 + d, y, z), (x1 - .04 + d, y, z), w * .86, h * .9, f, T, tf=.035, tw=.03)
    if T.i == 2:
        bx(g, x0, x1, y0, y1, z0, z1, f, T, 0, 0, ('-y',))


def ingots(g, b, parts, T, L):
    """Pig ingots in crossed courses."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    rest(g, parts, T, skip=('joint',))
    f = tint('billet', 0x5E5854)
    if T.i == 2:
        bx(g, x0, x1, .08, y1, z0, z1, f, T, 0, 0, ('-y',))
        return
    courses = 4
    hh = (y1 - .08) / courses
    for j in range(courses):
        ya, yb = .08 + j * hh, .08 + (j + 1) * hh - .008
        for k in range(4):
            if j % 2 == 0:
                za, zb = z0 + (z1 - z0) * k / 4 + .01, z0 + (z1 - z0) * (k + 1) / 4 - .01
                trap(g, x0 + .02, x1 - .02, za, zb, ya, yb, f, True)
            else:
                xa, xb = x0 + (x1 - x0) * k / 4 + .01, x0 + (x1 - x0) * (k + 1) / 4 - .01
                trap(g, xa, xb, z0 + .02, z1 - .02, ya, yb, f, False)


def trap(g, x0, x1, z0, z1, y0, y1, f, along_x, t=.06):
    """An ingot: a prism narrower at its top (along x, or along z)."""
    if along_x:
        a = [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)]
        c = [(x0 + t, y1, z0 + t), (x1 - t, y1, z0 + t), (x1 - t, y1, z1 - t), (x0 + t, y1, z1 - t)]
    else:
        a = [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)]
        c = [(x0 + t, y1, z0 + t), (x1 - t, y1, z0 + t), (x1 - t, y1, z1 - t), (x0 + t, y1, z1 - t)]
    g.face(c, f, normal=Y)
    mid = ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
    for i in range(4):
        j = (i + 1) % 4
        q = (a[i], a[j], c[j], c[i])
        cen = tuple(sum(p[k] for p in q) / 4 for k in range(3))
        g.face(q, f, normal=(cen[0] - mid[0], .2, cen[2] - mid[2]))


def plates(g, b, parts, T, L):
    """A stack of plate, each sheet a little askew."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    rest(g, parts, T, skip=('joint',))
    n = (12, 4, 1)[T.i]
    fa, fb = tint('billet', 0x6F6B67), tint('billet', 0x625E5B)
    for j in range(n):
        dx, dz = (rnd(j, 11) - .5) * .08, (rnd(j, 12) - .5) * .08
        ya, yb = .08 + (y1 - .08) * j / n, .08 + (y1 - .08) * (j + 1) / n - (.01 if n > 1 else 0)
        box(g, x0 + .04 + dx, x1 - .04 + dx, ya, yb, z0 + .04 + dz, z1 - .04 + dz, fa if j % 2 else fb, T, 0, 0, skip=('-y',) if j else ('-y',))


def moulds(g, b, parts, T, L):
    """Ingot moulds: four tapered cast-iron boxes, open-topped."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    rest(g, parts, T, skip=('joint',))
    f, dk = tint('iron', 0x3E3A38), mat('pit')
    for i in range(2):
        for k in range(2):
            xa, xb = x0 + (x1 - x0) * i / 2 + .02, x0 + (x1 - x0) * (i + 1) / 2 - .02
            za, zb = z0 + (z1 - z0) * k / 2 + .02, z0 + (z1 - z0) * (k + 1) / 2 - .02
            ya, yb, t, w = .08, y1, .07, .09
            if T.i == 2:
                bx(g, xa, xb, ya, yb, za, zb, f, T, 0, 0, ('-y',))
                continue
            lo = [(xa, ya, za), (xb, ya, za), (xb, ya, zb), (xa, ya, zb)]
            hi = [(xa + t, yb, za + t), (xb - t, yb, za + t), (xb - t, yb, zb - t), (xa + t, yb, zb - t)]
            inn = [(xa + t + w, yb, za + t + w), (xb - t - w, yb, za + t + w), (xb - t - w, yb, zb - t - w), (xa + t + w, yb, zb - t - w)]
            bot = [(p[0], yb - .5, p[2]) for p in inn]
            mid = ((xa + xb) / 2, 0, (za + zb) / 2)
            for a in range(4):
                c = (a + 1) % 4
                cen = ((lo[a][0] + lo[c][0]) / 2 - mid[0], .15, (lo[a][2] + lo[c][2]) / 2 - mid[2])
                g.face((lo[a], lo[c], hi[c], hi[a]), f, normal=cen)
                g.face((hi[a], hi[c], inn[c], inn[a]), f, normal=Y)
                g.face((inn[a], inn[c], bot[c], bot[a]), dk, normal=(-cen[0], 0, -cen[2]))
            g.face(bot, dk, normal=Y)
            if T.i == 0:
                # lifting lugs
                for (lx, lz, sx, sz) in (((xa + xb) / 2, za, 0, -1), ((xa + xb) / 2, zb, 0, 1)):
                    bx(g, lx - .08, lx + .08, yb - .42, yb - .3, min(lz, lz + sz * .05), max(lz, lz + sz * .05), f, T, .012)


# ---------------------------------------------------------------------------------------------------------------------------
# Rooms
# ---------------------------------------------------------------------------------------------------------------------------
def crib(g, b, parts, T, L):
    """A tool crib: a welded-mesh cage on a steel frame."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    m = mat('mesh')
    cell = 1.7 if T.i == 0 else 0
    bx(g, x0, x1, 0, y1, z0, z1, m, T, 0, cell, ('-y',))
    rest(g, parts, T)
    if T.i < 2:
        f = mat('steel')
        for y in (1.3,):
            bx(g, x0 - .03, x1 + .03, y - .03, y + .03, z0 - .03, z1 + .03, f, T, .008, 0)
        # the gate on the hall side: its frame, hasp and kick plate
        fx = x1 if x0 < 0 else x0
        s = 1 if x0 < 0 else -1
        cz = (z0 + z1) / 2
        bx(g, min(fx, fx + s * .04), max(fx, fx + s * .04), .05, 2.15, cz + .7, cz + 1.75, f, T, .01, 0)
        bx(g, min(fx, fx + s * .05), max(fx, fx + s * .05), .1, 2.1, cz + .76, cz + 1.69, m, T, 0, 0)
        bx(g, min(fx, fx + s * .07), max(fx, fx + s * .07), .98, 1.12, cz + .74, cz + .86, mat('black'), T, .01, 0)
        bx(g, min(fx, fx + s * .056), max(fx, fx + s * .056), .1, .4, cz + .76, cz + 1.69, f, T, 0, 0)


def shelf(g, b, parts, T, L):
    body(g, b, tint('steel', 0x4A545C), T, .012)
    rest(g, parts, T)


def screen(g, b, parts, T, L):
    body(g, b, mat('block'), T, .02, cell=(1.6, 0, 0)[T.i])
    for p in parts:
        if kname(p) == 'red' and T.i < 2:
            # a fire extinguisher on its bracket
            c, h = p['c'], p['h']
            cyl(g, (c[0], c[1] - h[1], c[2]), (c[0], c[1] + h[1] - .08, c[2]), min(h[0], h[2]) * .9, mat('danger'), T.round)
            cyl(g, (c[0], c[1] + h[1] - .08, c[2]), (c[0], c[1] + h[1], c[2]), .03, mat('black'), T.small)
            continue
        kit_part(g, p, T)


def bins(g, b, parts, T, L):
    """Parts stillages: steel bins with stacking posts, two high, two by two."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    f, st, ir = mat('bin'), mat('steel'), mat('iron')
    if T.i == 2:
        bx(g, x0, x1, 0, y1, z0, z1, f, T, 0, 0, ('-y',))
        return
    hx, hz, hy = (x1 - x0) / 2, (z1 - z0) / 2, y1 / 2
    for i in range(2):
        for k in range(2):
            for j in range(2):
                xa, za, ya = x0 + i * hx, z0 + k * hz, j * hy
                bx(g, xa + .03, xa + hx - .03, ya + .08, ya + hy - .03, za + .03, za + hz - .03, f if (i + k + j) % 3 else tint('bin', 0x4C7A56), T, .02, 0,
                   ('+y',) if j == 0 else ())
                if T.i == 0:
                    for (px, pz) in ((xa + .03, za + .03), (xa + hx - .03, za + .03), (xa + .03, za + hz - .03), (xa + hx - .03, za + hz - .03)):
                        bx(g, px - .03, px + .03, ya, ya + hy, pz - .03, pz + .03, st, T, 0, 0, ('-y',) if j == 0 else ())
                    bx(g, xa + .2, xa + hx - .2, ya + hy * .45, ya + hy * .7, za + .018, za + .03, mat('white'), T, 0, 0, ('+z',))


def office(g, b, parts, T, L):
    """A site office: a ribbed steel cabin with lit windows, a door and a roof unit."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    f = tint('office', b.get('col', 0xC8C1AE))
    bx(g, x0, x1, 0, y1, z0, z1, f, T, 0, 0, ('-y', '+x', '+z'))
    corrugated(g, (x1, z0), (x1, z1), 0, y1, (1, 0), f, T, pitch=.22, depth=.014, rows=9)
    corrugated(g, (x0, z1), (x1, z1), 0, y1, (0, 1), f, T, pitch=.22, depth=.014, rows=9)
    for p in parts:
        name = kname(p)
        if name == 'window':
            kit_part(g, p, T, mat('lit-window'))
            if T.i < 2:
                c, h = p['c'], p['h']
                fr = mat('frame')
                if h[0] < h[2]:
                    xo = c[0] + h[0]
                    for (za, zb, ya, yb) in ((c[2] - h[2] - .04, c[2] + h[2] + .04, c[1] + h[1], c[1] + h[1] + .05), (c[2] - h[2] - .04, c[2] - h[2], c[1] - h[1], c[1] + h[1]),
                                             (c[2] + h[2], c[2] + h[2] + .04, c[1] - h[1], c[1] + h[1]), (c[2] - .02, c[2] + .02, c[1] - h[1], c[1] + h[1])):
                        bx(g, xo - .02, xo + .02, ya, yb, za, zb, fr, T, 0, 0)
                else:
                    zo = c[2] + h[2]
                    for (xa, xb, ya, yb) in ((c[0] - h[0] - .04, c[0] + h[0] + .04, c[1] + h[1], c[1] + h[1] + .05), (c[0] - h[0] - .04, c[0] - h[0], c[1] - h[1], c[1] + h[1]),
                                             (c[0] + h[0], c[0] + h[0] + .04, c[1] - h[1], c[1] + h[1]), (c[0] - .02, c[0] + .02, c[1] - h[1], c[1] + h[1])):
                        bx(g, xa, xb, ya, yb, zo - .02, zo + .02, fr, T, 0, 0)
            continue
        if name == 'panel' and T.i < 2:
            # the roof unit: a condenser with its fan grille
            c, h = p['c'], p['h']
            kit_part(g, p, T)
            fk.ring(g, (c[0], c[1] + h[1] + .004, c[2]), .12, h[0] * .8, mat('black'), T.round + 2, Y)
            continue
        kit_part(g, p, T)
        if name == 'door' and T.i < 2:
            c, h = p['c'], p['h']
            cyl(g, (c[0] + h[0], 1.0, c[2] - h[2] + .12), (c[0] + h[0] + .05, 1.0, c[2] - h[2] + .12), .03, mat('chrome'), T.small)


def container(g, b, parts, T, L):
    """A 20 ft box: corrugated sides between its rails and corner posts, the doors and their lock rods on the ends."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    f = mat('box-rust' if '-a-' in b['id'] else 'box-green')
    dk = tint('box-rust', 0x7E3E20) if '-a-' in b['id'] else tint('box-green', 0x2F5139)
    bx(g, x0, x1, 0, y1, z0, z1, f, T, 0, 0, ('-y', '-x', '+x'))
    corrugated(g, (x0, z0 + .12), (x0, z1 - .12), .16, y1 - .14, (-1, 0), f, T, pitch=.28, depth=-.035, rows=9)
    corrugated(g, (x1, z0 + .12), (x1, z1 - .12), .16, y1 - .14, (1, 0), f, T, pitch=.28, depth=-.035, rows=9)
    for xx, s in ((x0, -1), (x1, 1)):
        for (ya, yb, za, zb) in ((0, .16, z0, z1), (y1 - .14, y1, z0, z1), (.16, y1 - .14, z0, z0 + .12), (.16, y1 - .14, z1 - .12, z1)):
            g.face(((xx, ya, za), (xx, ya, zb), (xx, yb, zb), (xx, yb, za)), dk, normal=(s, 0, 0))
    if T.i < 2:
        # the doors' seams and hinges on both ends
        for zz, s in ((z0, -1), (z1, 1)):
            cx = (x0 + x1) / 2
            bx(g, cx - .012, cx + .012, .16, y1 - .14, min(zz, zz + s * .012), max(zz, zz + s * .012), mat('black'), T, 0, 0)
            for yy in (.2, y1 - .2):
                bx(g, x0 + .1, x1 - .1, yy - .04, yy + .04, min(zz, zz + s * .02), max(zz, zz + s * .02), dk, T, .006, 0)
    rest(g, parts, T)


def forklift(g, b, parts, T, L):
    """A counterbalance truck: the chassis and counterweight, the overhead guard, the seat and wheel, the mast and forks."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    f, dk = mat('fork'), mat('black')
    cx = (x0 + x1) / 2
    bx(g, x0 + .04, x1 - .04, .2, 1.0, z0 + .45, z1 - .12, f, T, .07, 0)
    bx(g, x0, x1, .2, 1.18, z0, z0 + .55, tint('black', 0x2A2C2F), T, .12, 0)
    bx(g, x0 + .1, x1 - .1, 1.0, 1.12, z0 + .5, z0 + 1.25, tint('fork', 0xB98314), T, .04, 0, ('-y',))
    # the seat, the column and the wheel
    bx(g, cx - .25, cx + .25, 1.12, 1.24, z0 + .6, z0 + 1.1, dk, T, .04, 0, ('-y',))
    bx(g, cx - .25, cx + .25, 1.2, 1.72, z0 + .55, z0 + .68, dk, T, .04, 0)
    tube(g, [(cx, 1.0, z1 - .55), (cx, 1.42, z1 - .75)], .03, dk, T.small)
    if T.i < 2:
        lathe(g, [(.17, -.015), (.19, 0.0), (.17, .015)], (cx, 1.42, z1 - .75), dk, T.round, axis=(0, .9, -.43))
    # the overhead guard
    gy = y1 - .04
    for (px, pz) in ((x0 + .08, z0 + .5), (x1 - .08, z0 + .5), (x0 + .08, z1 - .3), (x1 - .08, z1 - .3)):
        tube(g, [(px, 1.0, pz), (px, gy, pz)], .035, f, T.small)
    bx(g, x0 + .03, x1 - .03, gy - .04, y1, z0 + .42, z1 - .22, f, T, .02, 0)
    if T.i < 2:
        for k in range(5):
            zz = z0 + .6 + k * .24
            bx(g, x0 + .08, x1 - .08, y1, y1 + .012, zz, zz + .05, dk, T, 0, 0, ('-y',))
    for p in parts:
        name = kname(p)
        if name == 'wheel':
            c, h = p['c'], p['h']
            s = -1 if c[0] < cx else 1
            cyl(g, (c[0] - s * .1, c[1], c[2]), (c[0] + s * .05, c[1], c[2]), .31, mat('rubber'), T.round + 2)
            cyl(g, (c[0] + s * .05, c[1], c[2]), (c[0] + s * .065, c[1], c[2]), .15, mat('yellow'), T.round)
            continue
        if name == 'crane-dark' or (name == 'black' and p['h'][0] > .5):
            continue            # the kit's counterweight and roof plate: modelled
        kit_part(g, p, T)
    if T.i < 2:
        # the carriage between the mast's uprights and its load backrest
        bx(g, cx - .5, cx + .5, .3, .9, z1 + .1, z1 + .15, dk, T, .01, 0)
        for k in range(4):
            xx = cx - .45 + k * .3
            bx(g, xx - .015, xx + .015, .9, 1.5, z1 + .1, z1 + .13, dk, T, 0, 0)
        bx(g, cx - .5, cx + .5, 1.47, 1.5, z1 + .1, z1 + .13, dk, T, 0, 0)


# ---------------------------------------------------------------------------------------------------------------------------
# The ladle on its car
# ---------------------------------------------------------------------------------------------------------------------------
def ladle_radius(h):
    """The shell's radius at height h over its foot."""
    pts = [(0.0, 1.36), (.05, 1.5), (1.5, 1.6), (2.95, 1.66), (3.1, 1.66)]
    for (h0, r0), (h1, r1) in zip(pts, pts[1:]):
        if h <= h1:
            return r0 + (r1 - r0) * (h - h0) / max(1e-6, h1 - h0)
    return pts[-1][1]


def ladle(g, b, parts, T, L):
    """The ladle: a riveted shell with hoops, trunnions and a pouring lip, a slag crust broken over the melt; it stands in a
    cradle frame whose posts fill its box's corners."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    f, ir = mat('ladle'), mat('iron')
    n = (28, 16, 10)[T.i]
    c = (cx, y0, cz)
    lathe(g, [(1.36, 0.0), (1.5, .05), (1.6, 1.5), (1.66, 2.95), (1.66, 3.1), (1.5, 3.1), (1.46, 2.94)], c, f, n, cap0=False)
    # the melt under its crust
    top = y0 + 2.94
    fk.disc(g, (cx, top, cz), 1.47, mat('melt'), n)
    slag = mat('slag')
    k = 0
    for i in range(-3, 4):
        for j in range(-3, 4):
            px, pz = cx + i * .48 + (rnd(i, j, 1) - .5) * .16, cz + j * .48 + (rnd(i, j, 2) - .5) * .16
            if math.hypot(px - cx, pz - cz) > 1.25 or rnd(i, j, 3) < .12:
                continue
            m = 5 + int(rnd(i, j, 4) * 3)
            rr = .2 + rnd(i, j, 5) * .09
            a0 = rnd(i, j, 6) * 6.28
            pts = [(px + rr * (.8 + .4 * rnd(i, j, 7 + q)) * math.cos(a0 + 6.283 * q / m), top + .012 + .01 * rnd(i, j, 30), pz + rr * (.8 + .4 * rnd(i, j, 17 + q)) * math.sin(a0 + 6.283 * q / m))
                   for q in range(m)]
            g.face(pts, slag, normal=Y)
            k += 1
    # the crust's rim against the lining
    fk.ring(g, (cx, top + .016, cz), 1.2, 1.47, slag, n)
    # hoops, trunnions, the lip
    for hh in (.5, 1.6, 2.75):
        r = ladle_radius(hh)
        lathe(g, [(r, hh - .1), (r + .06, hh - .07), (r + .06, hh + .07), (r, hh + .1)], c, ir, n)
        if T.tiny:
            for q in range(24):
                a = 6.283 * q / 24
                fk.bolts(g, [(cx + (r + .06) * math.cos(a), y0 + hh, cz + (r + .06) * math.sin(a))], mat('black'), T, .025, .02, (math.cos(a), 0, math.sin(a)))
    for s in (-1, 1):
        zt = cz + s * 1.58
        cyl(g, (cx, y0 + 2.1, zt), (cx, y0 + 2.1, cz + s * 1.93), .2, mat('chrome'), T.round)
        bx(g, cx - .42, cx + .42, y0 + 1.72, y0 + 2.48, min(zt, zt + s * .12), max(zt, zt + s * .12), ir, T, .04, 0)
    # the lip
    lx = cx + 1.6
    g.face(((lx - .1, y0 + 3.02, cz - .4), (lx + .3, y0 + 2.92, cz - .22), (lx + .3, y0 + 2.92, cz + .22), (lx - .1, y0 + 3.02, cz + .4)), ir, normal=Y)
    g.face(((lx - .1, y0 + 3.02, cz - .4), (lx + .3, y0 + 2.92, cz - .22), (lx + .3, y0 + 2.7, cz - .2), (lx - .1, y0 + 2.62, cz - .38)), ir, normal=(.3, 0, -1))
    g.face(((lx - .1, y0 + 3.02, cz + .4), (lx + .3, y0 + 2.92, cz + .22), (lx + .3, y0 + 2.7, cz + .2), (lx - .1, y0 + 2.62, cz + .38)), ir, normal=(.3, 0, 1))
    g.face(((lx + .3, y0 + 2.92, cz - .22), (lx + .3, y0 + 2.92, cz + .22), (lx + .3, y0 + 2.7, cz + .2), (lx + .3, y0 + 2.7, cz - .2)), ir, normal=(1, 0, 0))
    g.face(((lx - .1, y0 + 2.62, cz - .38), (lx + .3, y0 + 2.7, cz - .2), (lx + .3, y0 + 2.7, cz + .2), (lx - .1, y0 + 2.62, cz + .38)), ir, normal=(.2, -1, 0))
    # the cradle: corner posts and a top frame (the box's corners are its)
    st = tint('steel', 0x34424C)
    w = .2
    for (px, pz) in ((x0, z0), (x1 - w, z0), (x0, z1 - w), (x1 - w, z1 - w)):
        bx(g, px, px + w, y0, y1, pz, pz + w, st, T, .025, 0, ('-y',))
    for (xa, xb, za, zb) in ((x0 + w, x1 - w, z0, z0 + w), (x0 + w, x1 - w, z1 - w, z1), (x0, x0 + w, z0 + w, z1 - w), (x1 - w, x1, z0 + w, z1 - w)):
        bx(g, xa, xb, y1 - .16, y1, za, zb, st, T, .02, 0)
        bx(g, xa, xb, y0, y0 + .16, za, zb, st, T, .02, 0, ('-y',))
    if T.i < 2:
        for (px, pz, sx, sz) in ((x0 + w / 2, z0 + w / 2, 1, 1), (x1 - w / 2, z0 + w / 2, -1, 1), (x0 + w / 2, z1 - w / 2, 1, -1), (x1 - w / 2, z1 - w / 2, -1, -1)):
            bar(g, (px, y1 - .7, pz), (px + sx * .62, y1 - .08, pz), .05, .1, st, T, up=(0, 0, 1))
            bar(g, (px, y1 - .7, pz), (px, y1 - .08, pz + sz * .62), .05, .1, st, T, up=(1, 0, 0))


def ladle_car(g, b, parts, T, L):
    x0, x1, y0, y1, z0, z1 = ext(b)
    f = tint('steel', 0x6B4A2E)
    bx(g, x0, x1, .46, y1, z0, z1, f, T, .04, 0)
    bx(g, x0 + .3, x1 - .3, .14, .46, z0 + .25, z1 - .25, mat('black'), T, .03, 0, ('+y',))
    cz = (z0 + z1) / 2
    for s in (-1, 1):
        xx = x0 if s < 0 else x1
        for dz in (-.9, .9):
            cyl(g, (xx, .66, cz + dz), (xx + s * .2, .66, cz + dz), .1, mat('black'), T.round)
            cyl(g, (xx + s * .2, .66, cz + dz), (xx + s * .24, .66, cz + dz), .15, mat('iron'), T.round)
    for p in parts:
        name = kname(p)
        if name == 'wheel':
            c, h = p['c'], p['h']
            s = -1 if c[2] < cz else 1
            cyl(g, (c[0], c[1], c[2] - s * .12), (c[0], c[1], c[2] + s * .06), .32, mat('iron'), T.round + 2)
            cyl(g, (c[0], c[1], c[2] + s * .06), (c[0], c[1], c[2] + s * .09), .12, mat('black'), T.round)
            continue
        kit_part(g, p, T)


# ---------------------------------------------------------------------------------------------------------------------------
def block(g, b, parts, T, L):
    """Any static block the building does not model: by its id's family."""
    i = b['id']
    if i.startswith('factory-lathe-'):
        (lathe_head if i.endswith('-head') else lathe_bed)(g, b, parts, T, L)
    elif i.startswith('factory-mill-'):
        mill(g, b, parts, T, L)
    elif i.startswith('factory-cabinet-'):
        cabinet(g, b, parts, T, L)
    elif i.startswith('factory-rack-') or i.startswith('factory-bay-rack-'):
        rack(g, b, parts, T, L)
    elif i.startswith('factory-coils-'):
        coils(g, b, parts, T, L)
    elif i.startswith('factory-crib-'):
        crib(g, b, parts, T, L)
    elif i.startswith('factory-shelf-'):
        shelf(g, b, parts, T, L)
    elif i.startswith('factory-screen-'):
        screen(g, b, parts, T, L)
    elif i.startswith('factory-bins-'):
        bins(g, b, parts, T, L)
    elif i.endswith('-office'):
        office(g, b, parts, T, L)
    elif i.endswith('-container'):
        container(g, b, parts, T, L)
    elif i.endswith('-forklift'):
        forklift(g, b, parts, T, L)
    elif i == 'factory-ladle':
        ladle(g, b, parts, T, L)
    elif i == 'factory-ladle-car':
        ladle_car(g, b, parts, T, L)
    elif i.startswith('factory-bay-billets-'):
        billets(g, b, parts, T, L)
    elif i.startswith('factory-bay-beams-'):
        beams(g, b, parts, T, L)
    elif i.startswith('factory-bay-ingots-'):
        ingots(g, b, parts, T, L)
    elif i.startswith('factory-bay-plates-'):
        plates(g, b, parts, T, L)
    elif i.startswith('factory-bay-moulds-'):
        moulds(g, b, parts, T, L)
    elif i.startswith('factory-bay-hammer-'):
        hammer(g, b, parts, T, L)
    else:
        body(g, b, mat('steel'), T)
        rest(g, parts, T)
