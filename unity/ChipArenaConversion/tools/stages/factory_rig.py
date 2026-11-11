"""The Factory's rigs and breakable blocks (docs/arenas/FACTORY-ART.md): each a prop in its own frame, posed by
Core NativeFactoryArt from the hazards' states (the rig props) or drawn where its block stands (the block props).

Rig frames (Unity axes, metres; the names are NativeFactoryArt.Slots):
  blade            a saw blade of radius 1 in its own xy plane, its axle the z axis, at the blade's centre; scaled to the hazard's
                   radius in x and y, turned about z by the state's spin (a rail saw's is turned to face along x first)
  rail-saw         a rail saw's carriage, drop arm, guard and motor at the blade's centre (the blade itself .12 m to -x)
  ram              a press ram at its centre (the hazard's size)
  press-rod        a guide rod of length 1 up +y from the ram's top (scaled to reach into the head)
  crane-bridge     the bridge at (crane x, the rail's top, 0): end trucks at z +-10.7, girders, walkway, the cab hung at z -9.4
  crane-trolley    the trolley at (crane x, the girders' top, crane z)
  crane-ropes      the four hoist ropes, length 1 up +y from the hook block's top (scaled to the trolley)
  crane-hook(-high)  the hook block at the hook's underside (the slings' eye)
  crane-load(-high)  the beam bundle at its underside's centre (the hazard's size), swung about the hook
  crane-sling      a sling of length 1 up +y (laid from the hook to a corner of the bundle, or hanging slack)
  chuck            a lathe chuck on the headstock's face, its axis +x (out toward the bed), turning about x
Block props (the block's own frame, its centre): crate-big, crate-small, pallets, drums.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
import factory_kit as fk  # noqa: E402
from stagekit import Geo, box, lathe  # noqa: E402
from factory_kit import mat, tint, lamp, ext, bx, cyl, tube, ibeam, bar, stripes, striped_box, Y  # noqa: E402
from factory_plant import rnd  # noqa: E402

BL = {}


def steel_blade():
    return sk.fin('f-blade', 0xC9CED2, sk.DETAIL_NONE, .8, .55, .12, 0, 0, 0, 0)


# ---------------------------------------------------------------------------------------------------------------------------
# The saws
# ---------------------------------------------------------------------------------------------------------------------------
def blade(T):
    """A circular saw blade of radius 1: raked teeth, a ground body with a darker band and expansion slots, the arbor flanges."""
    g = Geo()
    f, band, dk = steel_blade(), tint('bright', 0x8E9498), mat('black')
    teeth = (32, 16, 8)[T.i]
    t = .02
    out = []
    for k in range(teeth):
        a0 = 2 * math.pi * k / teeth
        a1 = 2 * math.pi * (k + .72) / teeth
        out.append((math.cos(a0), math.sin(a0), 1.0))
        out.append((math.cos(a0 + .02) * .87, math.sin(a0 + .02) * .87, .87))
        out.append((math.cos(a1) * .93, math.sin(a1) * .93, .93))
    n = len(out)
    rb, ri = .8, .24
    for s in (-1, 1):
        z = s * t
        nz = (0, 0, s)
        for i in range(n):
            j = (i + 1) % n
            a, b = out[i], out[j]
            aa, ab = math.atan2(a[1], a[0]), math.atan2(b[1], b[0])
            g.face(((a[0], a[1], z), (b[0], b[1], z), (rb * math.cos(ab), rb * math.sin(ab), z), (rb * math.cos(aa), rb * math.sin(aa), z)), f, normal=nz)
        m = (36, 18, 10)[T.i]
        for (r0, r1, ff) in ((ri, .46, f), (.46, .66, band), (.66, rb, f)):
            for i in range(m):
                a, b = 2 * math.pi * i / m, 2 * math.pi * (i + 1) / m
                g.face(((r0 * math.cos(a), r0 * math.sin(a), z), (r1 * math.cos(a), r1 * math.sin(a), z), (r1 * math.cos(b), r1 * math.sin(b), z), (r0 * math.cos(b), r0 * math.sin(b), z)), ff,
                       normal=nz)
        if T.i < 2:
            # expansion slots
            for k in range(4):
                a = math.pi / 4 + k * math.pi / 2
                ca, sa = math.cos(a), math.sin(a)
                px, py = -sa * .012, ca * .012
                g.face(((ca * .74 - px, sa * .74 - py, z + s * .0015), (ca * .92 - px, sa * .92 - py, z + s * .0015), (ca * .92 + px, sa * .92 + py, z + s * .0015), (ca * .74 + px, sa * .74 + py, z + s * .0015)),
                       dk, normal=nz)
    # the rim
    for i in range(n):
        j = (i + 1) % n
        a, b = out[i], out[j]
        mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, 0)
        nx, ny = b[1] - a[1], -(b[0] - a[0])
        if nx * mid[0] + ny * mid[1] < 0:
            nx, ny = -nx, -ny
        g.face(((a[0], a[1], -t), (b[0], b[1], -t), (b[0], b[1], t), (a[0], a[1], t)), f, normal=(nx, ny, 0))
    # the arbor flanges and nut
    lathe(g, [(.0, -.05), (.2, -.05), (.26, -.035), (.26, .035), (.2, .05), (.0, .05)], (0, 0, 0), dk, (16, 10, 8)[T.i], axis=(0, 0, 1))
    if T.i < 2:
        lathe(g, [(.0, -.075), (.07, -.075), (.07, .075), (.0, .075)], (0, 0, 0), mat('chrome'), 6, axis=(0, 0, 1))
    return g


def rail_saw(T, r=.85):
    """A rail saw's carriage at the blade's centre: the trolley on its track, the drop arm, the guard over the blade, the motor."""
    g = Geo()
    dk, y, ir = mat('black'), mat('hazard-y'), mat('iron')
    top = r + .45
    bx(g, -.16, .16, top - .4, top - .15, -.28, .28, tint('black', 0x2F3439), T, .03, 0)
    if T.i < 2:
        # the cheeks that ride the track and their rollers
        for s in (-1, 1):
            bx(g, s * .13 - .02, s * .13 + .02, top - .16, top + .2, -.24, .24, dk, T, .008, 0)
            for dz in (-.16, .16):
                cyl(g, (s * .1, top + .17, dz), (s * .16, top + .17, dz), .045, ir, T.small + 1)
    bx(g, -.05, .05, .1, top - .4, -.06, .06, tint('black', 0x2F3439), T, .015, 0)
    bx(g, -.09, .09, -.1, .12, -.1, .1, ir, T, .03, 0)
    # the guard: a shroud over the blade's upper half (the blade runs .12 to -x)
    xa, xb = -.2, -.045
    n = (14, 8, 4)[T.i]
    R, ri = r + .08, r * .45
    for k in range(n):
        a0, a1 = math.pi * (.03 + .94 * k / n), math.pi * (.03 + .94 * (k + 1) / n)
        p0, p1 = (math.cos(a0), math.sin(a0)), (math.cos(a1), math.sin(a1))
        g.face(((xa, R * p0[1], R * p0[0]), (xb, R * p0[1], R * p0[0]), (xb, R * p1[1], R * p1[0]), (xa, R * p1[1], R * p1[0])), y,
               normals=((0, p0[1], p0[0]), (0, p0[1], p0[0]), (0, p1[1], p1[0]), (0, p1[1], p1[0])))
        for xx, s in ((xa, -1), (xb, 1)):
            ff = y if (k % 2 == 0 or T.i == 2) else mat('hazard-k')
            g.face(((xx, ri * p0[1], ri * p0[0]), (xx, R * p0[1], R * p0[0]), (xx, R * p1[1], R * p1[0]), (xx, ri * p1[1], ri * p1[0])), ff, normal=(s, 0, 0))
    # the motor and its belt case, the arbor
    cyl(g, (.08, 0, 0), (.34, 0, 0), .13, mat('motor'), T.round)
    if T.i < 2:
        for k in range(4):
            cyl(g, (.11 + k * .055, 0, 0), (.13 + k * .055, 0, 0), .15, tint('motor', 0xA84E20), T.round, caps=True)
    cyl(g, (-.22, 0, 0), (.08, 0, 0), .04, mat('chrome'), T.small + 1)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The presses
# ---------------------------------------------------------------------------------------------------------------------------
def ram(T, size, rx=.7, rz=.5):
    """A press ram: a cast platen with its die plate, striped along its lip, bosses where the guide rods enter."""
    g = Geo()
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    f = mat('press')
    bx(g, -hx, hx, -hy + .1, hy, -hz, hz, f, T, .045, 0, ('-y',))
    bx(g, -hx + .03, hx - .03, -hy, -hy + .1, -hz + .03, hz - .03, mat('bright'), T, .02, 0, ('+y',))
    if T.i < 2:
        for s in (-1, 1):
            stripes(g, (-hx + .05, -hy + .11, s * (hz + .002)), (1, 0, 0), Y, 2 * hx - .1, .2, (0, 0, s), .14)
        for ux in (-1, 1):
            for uz in (-1, 1):
                cyl(g, (ux * hx * rx, hy, uz * hz * rz), (ux * hx * rx, hy + .05, uz * hz * rz), .09, mat('iron'), T.round)
        for k in range(4):
            x = -hx + .5 + k * (2 * hx - 1) / 3
            bx(g, x - .05, x + .05, hy, hy + .035, -hz + .08, hz - .08, f, T, .012, 0, ('-y',))
    return g


def press_rod(T):
    g = Geo()
    cyl(g, (0, 0, 0), (0, 1, 0), .05, mat('chrome'), T.round, caps=False)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The crane
# ---------------------------------------------------------------------------------------------------------------------------
SPAN, TRUCK_Z, CAB_Z = 10.9, 10.7, -9.4


def crane_bridge(T):
    """The bridge: end trucks on the runway rails, two box girders, the walkway and its rail, the cab hung under the -z end."""
    g = Geo()
    yl, dk, ir = mat('crane'), tint('black', 0x2F3439), mat('iron')
    cell = 3.0 if T.i == 0 else 0
    for s in (-1, 1):
        ez = s * TRUCK_Z
        bx(g, -1.7, 1.7, .05, .56, ez - .3, ez + .3, dk, T, .04, 0)
        for xe in (-1.6, 1.6):
            striped_box(g, xe - .15, xe + .15, .05, .5, ez - .32, ez + .32, T, .1)
        for dx in (-1.2, 1.2):
            cyl(g, (dx, .18, ez - .2), (dx, .18, ez + .2), .2, ir, T.round + 2)
            cyl(g, (dx, .18, ez - .34), (dx, .18, ez + .34), .07, mat('black'), T.small + 1)
        # a girder: a welded box with stiffeners, striped at its ends
        x = s * .95
        bx(g, x - .23, x + .23, .56, 1.76, -SPAN + .5, SPAN - .5, yl, T, .02, cell)
        for e in (-1, 1):
            za, zb = (SPAN - .5, SPAN) if e > 0 else (-SPAN, -SPAN + .5)
            striped_box(g, x - .235, x + .235, .56, 1.76, za, zb, T, .25)
        if T.i < 2:
            step = 1.8 if T.i == 0 else 3.6
            z = -SPAN + 1.4
            while z < SPAN - 1:
                bx(g, x - .245, x + .245, .6, 1.72, z - .012, z + .012, yl, T, 0, 0)
                z += step
            bx(g, x - .26, x + .26, 1.74, 1.79, -SPAN + .5, SPAN - .5, tint('crane', 0xC98D12), T, .008, 0, ('-y',))
            # the trolley's rail
            bx(g, x - .04, x + .04, 1.79, 1.84, -SPAN + .6, SPAN - .6, ir, T, 0, 0, ('-y',))
    # the walkway and its handrail
    bx(g, 1.18, 1.9, .56, .62, -SPAN + .6, SPAN - .6, mat('grating'), T, .006, cell)
    rl = mat('yellow')
    z = -SPAN + .6
    k = 0
    while z <= SPAN - .59:
        if T.i < 2 or k % 3 == 0:
            cyl(g, (1.88, .62, z), (1.88, 1.6, z), .02, rl, T.small, caps=False)
        z += 1.8
        k += 1
    for yy in ((1.6, 1.1) if T.i < 2 else (1.6,)):
        cyl(g, (1.88, yy, -SPAN + .6), (1.88, yy, SPAN - .6), .022, rl, T.small)
    if T.i < 2:
        # brackets under the walkway, the capacity plate, the festoon along the far girder
        z = -SPAN + 1.5
        while z < SPAN - 1:
            bar(g, (1.18, .2, z), (1.86, .56, z), .04, .06, dk, T)
            z += 3.6
        bx(g, -1.19, -1.18, .8, 1.5, -1.6, 1.6, mat('white'), T, 0, 0, ('+x',))
        bx(g, -1.2, -1.19, .95, 1.35, -1.1, -.5, mat('black'), T, 0, 0, ('+x',))
        bx(g, -1.2, -1.19, .95, 1.35, -.3, .3, mat('black'), T, 0, 0, ('+x',))
        bx(g, -1.2, -1.19, .95, 1.1, .6, 1.2, mat('black'), T, 0, 0, ('+x',))
        if T.tiny:
            loops = 14
            for i in range(loops):
                za, zb = -SPAN + 1 + (2 * SPAN - 2) * i / loops, -SPAN + 1 + (2 * SPAN - 2) * (i + 1) / loops
                pts = [(-.66, 1.5 - .32 * math.sin(math.pi * q / 6), za + (zb - za) * q / 6) for q in range(7)]
                tube(g, pts, .014, mat('black'), 3, caps=False)
            cyl(g, (-.66, 1.52, -SPAN + .9), (-.66, 1.52, SPAN - .9), .015, ir, 3)
    # the cab
    cz, y0, y1 = CAB_Z, -1.9, -.35
    bx(g, -.8, .8, y0, y0 + .6, cz - .8, cz + .8, yl, T, .05, 0)
    bx(g, -.8, .8, y1 - .16, y1, cz - .8, cz + .8, yl, T, .04, 0)
    gl = mat('glass')
    bx(g, -.76, .76, y0 + .6, y1 - .16, cz - .76, cz + .76, gl, T, 0, 0, ('-y', '+y'))
    for (px, pz) in ((-.77, -.77), (.77, -.77), (-.77, .77), (.77, .77)):
        bx(g, px - .04, px + .04, y0 + .6, y1 - .16, cz + pz - .04, cz + pz + .04, yl, T, .008, 0, ('-y', '+y'))
    if T.i < 2:
        bx(g, -.03, .03, y0 + .6, y1 - .16, cz + .765, cz + .79, yl, T, 0, 0, ('-y', '+y'))
        bx(g, -.3, .3, y0 - .05, y0, cz - .3, cz + .3, dk, T, .01, 0)
        # a work lamp under the cab
        bx(g, -.12, .12, y0 - .12, y0 - .05, cz + .5, cz + .7, dk, T, .01, 0)
        g.face(((-.1, y0 - .122, cz + .52), (.1, y0 - .122, cz + .52), (.1, y0 - .122, cz + .68), (-.1, y0 - .122, cz + .68)), mat('flood'), normal=(0, -1, 0))
    for dx in (-.6, .6):
        bx(g, dx - .05, dx + .05, y1, .56, cz - .05, cz + .05, dk, T, .01, 0)
    return g


def crane_trolley(T):
    """The trolley: its frame on four wheels, the hoist drum, the motor and gear case, the beacon's mast."""
    g = Geo()
    dk, ir = tint('black', 0x2F3439), mat('iron')
    bx(g, -1.3, 1.3, .08, .42, -1.1, 1.1, dk, T, .04, 0)
    for sx in (-1, 1):
        for sz in (-1, 1):
            cyl(g, (sx * .95 - .08, .08, sz * .8), (sx * .95 + .08, .08, sz * .8), .14, ir, T.round)
    n = T.round + 4
    lathe(g, [(.0, -.72), (.36, -.72), (.36, -.66), (.3, -.64), (.3, .64), (.36, .66), (.36, .72), (.0, .72)], (0, .78, 0), mat('crane'), n, axis=(1, 0, 0))
    if T.i < 2:
        for k in range(9):
            x = -.56 + k * .14
            lathe(g, [(.3, x - .03), (.315, x), (.3, x + .03)], (0, .78, 0), mat('black'), n, axis=(1, 0, 0))
        for sx in (-.78, .78):
            bx(g, sx - .05, sx + .05, .42, .82, -.2, .2, dk, T, .015, 0, ('-y',))
    cyl(g, (.85, .68, 0), (1.3, .68, 0), .24, mat('motor'), T.round + 2)
    bx(g, -1.3, -.85, .42, .86, -.4, .4, mat('cabinet'), T, .03, 0, ('-y',))
    bx(g, -.035, .035, 1.0, 1.1, -.035, .035, dk, T, 0, 0, ('+y',))
    bx(g, -.25, .25, .42, 1.0, .36, .44, dk, T, .01, 0, ('-y',))
    return g


def crane_ropes(T):
    g = Geo()
    for dx in (-.18, .18):
        for dz in (-.12, .12):
            cyl(g, (dx, 0, dz), (dx, 1, dz), .016, mat('black'), 4 if T.i else 5, caps=False)
    return g


def crane_hook(T):
    """The hook block at the hook's underside: cheek plates round a sheave, striped along the foot, the swivel hook."""
    g = Geo()
    yl, dk, ir = mat('crane'), mat('black'), mat('iron')
    for s in (-1, 1):
        bx(g, -.28, .28, .55, 1.15, s * .15 - .03, s * .15 + .03, yl, T, .03, 0)
    striped_box(g, -.29, .29, .35, .55, -.19, .19, T, .09)
    cyl(g, (0, .86, -.12), (0, .86, .12), .25, ir, T.round + 2)
    cyl(g, (0, .86, -.2), (0, .86, .2), .06, dk, T.small + 1)
    cyl(g, (0, .16, 0), (0, .35, 0), .06, dk, T.small + 2)
    pts = [(0, .18, 0), (-.02, .1, 0), (-.09, .03, 0), (-.01, -.05, 0), (.1, -.03, 0), (.15, .05, 0), (.14, .14, 0)]
    pts = sk.catmull(pts, 3 if T.i == 0 else 2) if T.i < 2 else pts
    sk.sweep(g, sk.circle_section(.045, T.small + 1), pts, dk, scale=[1 - .55 * (i / (len(pts) - 1)) ** 3 for i in range(len(pts))])
    return g


def crane_load(T, size):
    """The beam bundle at its underside's centre: three I-beams, strapped twice."""
    g = Geo()
    sx, sy, sz = size
    f = mat('oxide')
    bw = sx / 3
    for k in range(3):
        x = -sx / 2 + bw * (k + .5)
        if T.i < 2:
            ibeam(g, (x, sy / 2, -sz / 2), (x, sy / 2, sz / 2), bw * .96, sy, f, T, tf=.08, tw=.07)
        else:
            bx(g, x - bw * .48, x + bw * .48, 0, sy, -sz / 2, sz / 2, f, T, 0, 0)
    for uz in (-1, 1):
        z = uz * sz * .3
        bx(g, -sx / 2 - .02, sx / 2 + .02, -.02, sy + .02, z - .05, z + .05, mat('black'), T, .008, 0)
    if T.i < 2:
        # the mill's paint marks on the beams' ends
        for k in range(3):
            x = -sx / 2 + bw * (k + .5)
            for e in (-1, 1):
                g.face(((x - .2, sy - .081, e * (sz / 2 + .002)), (x + .2, sy - .081, e * (sz / 2 + .002)), (x + .2, sy - .002, e * (sz / 2 + .002)), (x - .2, sy - .002, e * (sz / 2 + .002))),
                       mat('white'), normal=(0, 0, e))
    return g


def crane_sling(T):
    g = Geo()
    cyl(g, (0, 0, 0), (0, 1, 0), .02, mat('black'), 4 if T.i else 5, caps=False)
    return g


def chuck(T):
    """A three-jaw chuck on the spindle's nose (axis +x), a stub of bar in its jaws."""
    g = Geo()
    n = (20, 12, 8)[T.i]
    lathe(g, [(.0, 0.0), (.31, 0.0), (.31, .035), (.28, .045), (.28, .12), (.26, .14), (.09, .14)], (0, 0, 0), mat('chrome'), n, axis=(1, 0, 0))
    lathe(g, [(.068, .14), (.068, .3), (.0, .3)], (0, 0, 0), mat('bright'), T.small + 2, axis=(1, 0, 0))
    for k in range(3):
        a = 2 * math.pi * k / 3
        t = Geo()
        bx(t, .14, .18, .08, .27, -.04, .04, mat('iron'), T, .008 if T.i == 0 else 0, 0)
        g.extend(t.transformed(sk.affine(sk.quat_axis(1, 0, 0, a))))
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The breakable blocks
# ---------------------------------------------------------------------------------------------------------------------------
def crate(T, sx, sy, sz):
    """A planked crate with corner battens and braces, a stencil on two sides."""
    g = Geo()
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    w, d = mat('timber-y'), tint('timber', 0x74552F)
    t = .035
    if T.i == 2:
        bx(g, -hx, hx, -hy, hy, -hz, hz, w, T, 0, 0)
        return g
    bx(g, -hx + t, hx - t, -hy + .01, hy - t, -hz + t, hz - t, w, T, 0, 0, ('-y',))
    if T.i == 0:
        # plank seams
        nx = max(2, int(round(sx / .22)))
        for k in range(1, nx):
            x = -hx + sx * k / nx
            for z in (-hz + t - .001, hz - t + .001):
                bx(g, x - .006, x + .006, -hy + .02, hy - t, min(z, z), max(z, z) + .0005, mat('black'), T, 0, 0)
        nz = max(2, int(round(sz / .22)))
        for k in range(1, nz):
            z = -hz + sz * k / nz
            for x in (-hx + t - .001, hx - t + .001):
                bx(g, x, x + .0005, -hy + .02, hy - t, z - .006, z + .006, mat('black'), T, 0, 0)
    bw = .1
    # the frame: battens along every edge
    for sxx in (-1, 1):
        for szz in (-1, 1):
            bx(g, sxx * hx - (bw if sxx > 0 else 0), sxx * hx + (bw if sxx < 0 else 0), -hy, hy, szz * hz - (bw if szz > 0 else 0), szz * hz + (bw if szz < 0 else 0), d, T, .012, 0)
    for yy in (-hy, hy - bw):
        for szz in (-1, 1):
            bx(g, -hx + bw, hx - bw, yy, yy + bw, szz * hz - (t if szz > 0 else 0), szz * hz + (t if szz < 0 else 0), d, T, .01, 0)
        for sxx in (-1, 1):
            bx(g, sxx * hx - (t if sxx > 0 else 0), sxx * hx + (t if sxx < 0 else 0), yy, yy + bw, -hz + bw, hz - bw, d, T, .01, 0)
    # the lid's boards
    bx(g, -hx + .02, hx - .02, hy - t, hy, -hz + .02, hz - .02, w, T, .01, 0, ('-y',))
    if T.i == 0:
        for szz in (-1, 1):
            bar(g, (-hx + bw, -hy + bw, szz * (hz - t / 2)), (hx - bw, hy - bw, szz * (hz - t / 2)), .09, t, d, T, up=(0, 0, 1))
        for sxx in (-1, 1):
            g.face(((sxx * (hx - t + .002), -.12, -.28), (sxx * (hx - t + .002), -.12, .28), (sxx * (hx - t + .002), .12, .28), (sxx * (hx - t + .002), .12, -.28)), mat('black'), normal=(sxx, 0, 0))
    return g


def pallets(T, sx, sy, sz):
    """A stack of pallets."""
    g = Geo()
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    w, d = mat('timber'), tint('timber', 0x7A5C38)
    n = 7
    ph = sy / n
    if T.i == 2:
        bx(g, -hx, hx, -hy, hy, -hz, hz, w, T, 0, 0)
        return g
    for j in range(n):
        y = -hy + j * ph
        dx, dz = (rnd(j, 41) - .5) * .05, (rnd(j, 42) - .5) * .04
        ff = w if j % 2 else d
        if T.i == 1:
            bx(g, -hx + .02 + dx, hx - .02 + dx, y + ph * .62, y + ph - .004, -hz + .02 + dz, hz - .02 + dz, ff, T, 0, 0)
            for zz in (-hz + .06, -.04, hz - .14):
                bx(g, -hx + .02 + dx, hx - .02 + dx, y, y + ph * .62, zz + dz, zz + .08 + dz, ff, T, 0, 0, ('+y',))
            continue
        for k in range(5):
            xa = -hx + .02 + (sx - .04) * k / 5 + dx
            bx(g, xa + .012, xa + (sx - .04) / 5 - .012, y + ph * .72, y + ph - .004, -hz + .02 + dz, hz - .02 + dz, ff, T, .004, 0)
        for zz in (-hz + .05, -.045, hz - .14):
            bx(g, -hx + .03 + dx, hx - .03 + dx, y + ph * .18, y + ph * .72, zz + dz, zz + .09 + dz, ff, T, 0, 0, ('+y',))
        for k in (0, 2, 4):
            xa = -hx + .02 + (sx - .04) * k / 5 + dx
            bx(g, xa + .012, xa + (sx - .04) / 5 - .012, y, y + ph * .18, -hz + .02 + dz, hz - .02 + dz, ff, T, 0, 0, ('+y',))
    return g


def drums(T, sx, sy, sz):
    """Four oil drums on a pallet, banded."""
    g = Geo()
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    n = (16, 10, 7)[T.i]
    bx(g, -hx, hx, -hy, -hy + .12, -hz, hz, mat('timber'), T, .01, 0)
    r = min(hx, hz) / 2 - .012
    cols = ('drum', 'drum', 'drum-red', 'black')
    h = sy - .13
    k = 0
    for sxx in (-1, 1):
        for szz in (-1, 1):
            c = (sxx * (r + .008), -hy + .12, szz * (r + .008))
            f = mat(cols[k])
            k += 1
            prof = [(r - .02, 0.0), (r, .02), (r, h * .3), (r + .012, h * .32), (r + .012, h * .345), (r, h * .365), (r, h * .63), (r + .012, h * .65), (r + .012, h * .675), (r, h * .695), (r, h - .02),
                    (r + .008, h - .012), (r + .008, h), (r - .025, h), (r - .03, h - .025), (0.0, h - .025)] if T.i < 2 else [(r, 0.0), (r, h), (0.0, h)]
            lathe(g, prof, c, f, n)
            if T.i == 0:
                cyl(g, (c[0] + r * .5, c[1] + h - .025, c[2]), (c[0] + r * .5, c[1] + h - .005, c[2]), .035, mat('chrome'), 6)
    if T.i < 2:
        bx(g, -hx + .01, hx - .01, .12, .18, -hz + .01, hz - .01, mat('black'), T, 0, 0, ('-y', '+y'))
    return g


BLOCK_KINDS = (('crate-big', lambda b: b['id'].startswith('factory-crate-') and b['size'][0] > 1.5), ('crate-small', lambda b: b['id'].startswith('factory-crate-') and b['size'][0] <= 1.5),
               ('pallets', lambda b: b['id'].endswith('-pallets')), ('drums', lambda b: '-drums-' in b['id']))


def blocks_of(L):
    return [b for b in L['blocks'] if b['role'] != 'static']


def kind_of(b):
    for name, test in BLOCK_KINDS:
        if test(b):
            return name
    raise SystemExit('a breakable block without a model: ' + b['id'])


def block_model(T, b):
    k = kind_of(b)
    s = b['size']
    return (crate if k.startswith('crate') else pallets if k == 'pallets' else drums)(T, s[0], s[1], s[2])


def block_scene(L, T):
    g = Geo()
    for b in blocks_of(L):
        g.extend(fk.moved(block_model(T, b), tuple(b['pos'])))
    return g


def hazard(L, kind):
    return [h for h in L['hazards'] if h['kind'] == kind]


def rig_items(L, T):
    """(slot name, Geo, the rest place for the bake's patterns)."""
    press = hazard(L, 'press')[0]
    crane = hazard(L, 'crane')[0]
    R = L['rig']
    load = crane_load(T, crane['size'])
    hook = crane_hook(T)
    return [('blade', blade(T), (0, 1, 19.5)), ('rail-saw', rail_saw(T, hazard(L, 'saw-rail')[0]['radius']), (8, 4.65, 29)), ('ram', ram(T, press['size'], R['rodX'], R['rodZ']), tuple(press['a'])),
            ('press-rod', press_rod(T), (press['a'][0], 3, press['a'][2])), ('crane-bridge', crane_bridge(T), (0, crane['a'][1] + R['railUp'], 0)),
            ('crane-trolley', crane_trolley(T), (0, crane['a'][1] + R['railUp'] + R['trolleyUp'], 0)), ('crane-ropes', crane_ropes(T), (0, 11, 0)), ('crane-hook', hook, (0, 10.4, 0)),
            ('crane-hook-high', None, 'crane-hook'), ('crane-load', load, (0, 9.5, 0)), ('crane-load-high', None, 'crane-load'), ('crane-sling', crane_sling(T), (0, 10.4, 0)),
            ('chuck', chuck(T), tuple(R['chucks'][0][:3]))]


def preview_rig(L, T):
    """The rig at a telling moment for the previews: a slitter up, the rail saws out, the rams parked, the crane carrying."""
    out = []
    items = {n: g for n, g, r in rig_items(L, T) if g is not None}
    R = L['rig']

    def put(name, m):
        g = items[name].transformed(m)
        out.append((g, sk.Bake(g, None)))
    I = (0, 0, 0, 1)
    for h in hazard(L, 'saw-slot')[:1]:
        r = h['radius']
        put('blade', sk.affine(sk.quat_axis(0, 0, 1, .3), (-6, 0.0, h['a'][2]), (r, r, 1)))
    for h in hazard(L, 'saw-rail'):
        x, y, z = h['a'][0], h['a'][1], (h['a'][2] + h['b'][2]) / 2
        put('rail-saw', sk.affine(I, (x, y, z)))
        q = sk.quat_axis(0, 1, 0, math.pi / 2)
        put('blade', sk.affine(q, (x - R['bladeAside'], y, z), (h['radius'], h['radius'], 1)))
    for h in hazard(L, 'press'):
        a = h['a']
        hx, hy, hz = h['size'][0] / 2, h['size'][1] / 2, h['size'][2] / 2
        put('ram', sk.affine(I, tuple(a)))
        for ux in (-1, 1):
            for uz in (-1, 1):
                put('press-rod', sk.affine(I, (a[0] + ux * hx * R['rodX'], a[1] + hy, a[2] + uz * hz * R['rodZ']), (1, R['rodInto'], 1)))
    c = hazard(L, 'crane')[0]
    X, Z, rail = -6.0, 1.5, c['a'][1] + R['railUp']
    ty = rail + R['trolleyUp']
    hookY = c['carry'] + c['size'][1] + 1.6
    put('crane-bridge', sk.affine(I, (X, rail, 0)))
    put('crane-trolley', sk.affine(I, (X, ty, Z)))
    put('crane-ropes', sk.affine(I, (X, hookY + R['ropeFoot'], Z), (1, ty - hookY - R['ropeFoot'], 1)))
    put('crane-hook', sk.affine(I, (X, hookY, Z)))
    put('crane-load', sk.affine(I, (X, c['carry'], Z)))
    for ux in (-1, 1):
        for uz in (-1, 1):
            a, b = (X, hookY, Z), (X + ux * c['size'][0] * R['slingX'], c['carry'] + c['size'][1], Z + uz * c['size'][2] * R['slingZ'])
            d = sk._sub(b, a)
            l = math.sqrt(sk._dot(d, d))
            u, v, w = sk.frame(a, b)
            m = [v[0], w[0] * l, u[0], a[0], v[1], w[1] * l, u[1], a[1], v[2], w[2] * l, u[2], a[2]]
            if np.linalg.det(np.array(m).reshape(3, 4)[:, :3]) < 0:
                m = [u[0], w[0] * l, v[0], a[0], u[1], w[1] * l, v[1], a[1], u[2], w[2] * l, v[2], a[2]]
            put('crane-sling', m)
    for (x, y, z, d) in R['chucks']:
        put('chuck', sk.affine(I if d > 0 else sk.quat_axis(0, 1, 0, math.pi), (x, y, z)))
    return out


def bank_props(sb, L, T, floors, no_ao):
    ti = T.i
    items = [(n, g, r) for n, g, r in rig_items(L, T)]
    shared = [(n, r) for n, g, r in items if g is None]
    items = [(n, g, r) for n, g, r in items if g is not None]
    blocks = blocks_of(L)
    firsts = {}
    for b in blocks:
        firsts.setdefault(kind_of(b), b)
    for k, b in firsts.items():
        items.append((k, block_model(T, b), tuple(b['pos'])))
    geos = [it[1] for it in items]
    aos = [None] * len(geos)
    if not no_ao:
        # each prop on its own, well apart, so they do not shade each other
        spread = [g.transformed(sk.affine((0, 0, 0, 1), (i * 60.0, 0, 200.0))) for i, g in enumerate(geos)]
        aos = sk.bake_ao(spread, distance=.5, samples=(24, 12, 8)[ti])
        aos = [.35 + .65 * a for a in aos]
    mesh_of = {}
    for (name, g, rest), ao in zip(items, aos):
        if g.faces == 0:
            continue
        P = g.arrays()[0].astype(np.float64)
        bk = sk.Bake(g, ao, world=P + np.array(rest), floors=(-99.0,), seed=7)
        om = sk.to_mesh('%s/%d' % (name, ti), g, bk)
        mi = sb.mesh(om)
        group = 'block' if name in firsts else 'rig'
        if name in firsts:
            for b in blocks:
                if kind_of(b) == name:
                    sb.share('block/%s/%d' % (b['id'], ti), 'block', mi)
        else:
            sb.share('rig/%s/%d' % (name, ti), 'rig', mi)
            mesh_of[name] = mi
        st = sb.stats.setdefault((group, str(ti)), [0, 0, 0])
        st[0] += 1
        st[1] += sum(len(s) for s in om.submeshes) // 3
        st[2] += len(om.positions)
    for name, src in shared:
        sb.share('rig/%s/%d' % (name, ti), 'rig', mesh_of[src])
