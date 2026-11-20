"""Belvedere Camp's props (tools/stages/camp_bank.py banks them; the frames are camp_kit.py's).

Part 1, the live camp on the fence and overhead (each fence prop in its own frame; the strings, sails and cables in the world), and
part 2, the DEV preview's structures on A1's blocks (each in its row's frame) and its glass. Every builder takes the tier (stagekit
Tier: 0 Fine, 2 Low) and returns a Geo.
"""
import math

import stagekit as sk
from stagekit import Geo
import meridian_gates_kit as mk
import camp_kit as ck
from camp_kit import bx, rod, tube, glow

PI = math.pi


def F():
    return ck.finishes()


# ===========================================================================================================================
# Part 1: on the fence (frame: the inner face is z 0, the fence z 0..0.2 and up to 3.2 m; the art faces -z)
# ===========================================================================================================================
def fence_lamp(T):
    """A caged bulkhead on a gooseneck over the fence top: a plate on the inner face, a saddle over the top, the pipe rising to 3.5 m
    before it reaches into the camp (nothing below 3.4 m stands more than 6 cm off the face)."""
    f = F()
    g = Geo()
    bx(g, T, -.07, .07, 2.9, 3.16, -.03, 0.0, f['clamp'], bevel=.006, cell=0)
    bx(g, T, -.06, .06, 3.2, 3.26, -.03, .21, f['clamp'], bevel=.006, cell=0)
    rod(g, (0, 3.0, -.035), (0, 3.5, -.035), .022, f['metal-dark'], 6)
    pts = [(0, 3.5, -.035), (0, 3.62, -.05), (0, 3.74, -.12), (0, 3.78, -.22), (0, 3.76, -.31), (0, 3.72, -.38)]
    tube(g, pts, .02, f['metal-dark'], 6)
    ck.caged_lamp(g, T, (0, 3.71, -.4), down=(0, -1, -.15), r=.07, length=.2)
    # the junction box on the plate, its cable down to the fence's run
    bx(g, T, -.05, .05, 2.92, 3.04, -.055, -.03, f['panel-grey'], bevel=.004, cell=0)
    return g


def sail_mast(T):
    """A scaffold tube clamped to the inner face (from the floor to 4.8 m), a small caged lamp on an arm at 4.3 m, the sail's eye."""
    f = F()
    g = Geo()
    bx(g, T, -.09, .09, .004, .024, -.14, 0.0, f['clamp'], bevel=0, cell=0)
    rod(g, (0, .024, -.07), (0, 4.8, -.07), .036, f['scaffold'], 8)
    for y in (1.0, 2.2, 3.0):
        bx(g, T, -.05, .05, y - .04, y + .04, -.12, 0.0, f['clamp'], bevel=.006, cell=0)
        rod(g, (-.05, y, -.07), (.05, y, -.07), .045, f['clamp'], 6)
    bx(g, T, -.05, .05, 4.8, 4.83, -.12, -.02, f['clamp'], bevel=0, cell=0)
    rod(g, (0, 4.35, -.07), (0, 4.35, -.34), .016, f['metal-dark'], 5)
    ck.caged_lamp(g, T, (0, 4.32, -.38), down=(0, -1, -.1), r=.06, length=.16)
    rod(g, (0, 4.62, -.07), (0, 4.62, -.13), .018, f['clamp'], 5)
    return g


def corner_mast(T):
    """The corner mast: a square lattice of four tubes in the fence's inner corner (both faces the planes x 0 and z 0, the camp at
    x, z < 0) to 7.2 m, braced to both faces, a warm flood at 6.7 m aimed into the middle, the strings' and sail's eyes."""
    f = F()
    g = Geo()
    legs = [(-.04, -.04), (-.19, -.04), (-.04, -.19), (-.19, -.19)]
    for (x, z) in legs:
        rod(g, (x, 0.0, z), (x, 7.2, z), .03, f['scaffold'], 6)
    k = 0
    y = .2
    while y < 7.0:
        y1 = min(7.1, y + .55)
        for i, (a, b) in enumerate(((0, 1), (1, 3), (3, 2), (2, 0))):
            pa, pb = legs[a], legs[b]
            if (i + k) % 2:
                pa, pb = pb, pa
            rod(g, (pa[0], y, pa[1]), (pb[0], y1, pb[1]), .012, f['scaffold'], 4, caps=False)
        y, k = y1, k + 1
    for y in (1.0, 2.2, 3.0):
        bx(g, T, -.23, 0.0, y - .03, y + .03, -.05, -.02, f['clamp'], bevel=0, cell=0)
        bx(g, T, -.05, -.02, y - .03, y + .03, -.23, 0.0, f['clamp'], bevel=0, cell=0)
    bx(g, T, -.23, 0.0, 7.2, 7.24, -.23, 0.0, f['clamp'], bevel=0, cell=0)
    # the flood's yoke and head
    rod(g, (-.12, 6.8, -.12), (-.38, 6.74, -.38), .025, f['metal-dark'], 5)
    ck.flood_head(g, T, (-.45, 6.7, -.45), (-.66, -.36, -.66), w=.36, h=.26, d=.16)
    for y in (7.0, 6.4):
        rod(g, (-.12, y, -.12), (-.27, y, -.27), .016, f['clamp'], 5)
    return g


def gate_mast(T, pa=False):
    """A mast on the fence top at a gate's edge: its shoe on the top (nothing on the faces below 3.4 m), the tube to 6.25 m, a flood
    down the lane at 4.5 m, the strings' eyes at 5.05 and 6.05 m; the north gate's east mast carries the camp PA's two horns."""
    f = F()
    g = Geo()
    bx(g, T, -.15, .15, 3.2, 3.25, 0.0, .2, f['clamp'], bevel=.005, cell=0)
    rod(g, (0, 3.25, .1), (0, 6.25, .1), .042, f['scaffold'], 8)
    rod(g, (0, 4.62, .1), (0, 4.6, -.36), .022, f['metal-dark'], 5)
    ck.flood_head(g, T, (0, 4.5, -.48), (0, -.5, -.86), w=.38, h=.28, d=.16)
    for y in (5.05, 6.05):
        rod(g, (-.09, y, .1), (.09, y, .1), .02, f['clamp'], 5)
    bx(g, T, -.05, .05, 6.25, 6.28, .05, .15, f['clamp'], bevel=0, cell=0)
    if pa:
        for sx in (-1, 1):
            d = sk._norm((sx * .35, -.2, -.92))
            base = (0, 5.55, .06)
            mouth = sk._add(base, d, .32)
            sk.lathe(g, [(.03, 0.0), (.05, .12), (.14, .32)], base, f['cd-olive'], 10 if T.detail else 6, axis=d, cap0=True)
            rod(g, (0, 5.55, .1), base, .02, f['metal-dark'], 5)
    return g


def fire_drum(T):
    """A half drum against the inner face (cut lengthwise, 0.46 m across, 0.245 m deep with its standoff): rusted, charred at the
    top, punched air holes glowing low, a grate, logs and a bed of coals."""
    f = F()
    g = Geo()
    r, h, z0 = .23, .74, -.015
    n = 12 if T.detail else 6
    rings = [(0.0, f['drum']), (.25, f['drum']), (.5, f['drum-char']), (h, f['drum-char'])]
    def P(a, y, rr=r):
        return (rr * math.cos(a), y, z0 - rr * math.sin(a))
    for (y0_, fin), (y1_, _) in zip(rings[:-1], rings[1:]):
        for i in range(n):
            a0, a1 = PI * i / n, PI * (i + 1) / n
            na, nb = (math.cos(a0), 0, -math.sin(a0)), (math.cos(a1), 0, -math.sin(a1))
            g.face((P(a0, y0_), P(a1, y0_), P(a1, y1_), P(a0, y1_)), fin, normals=(na, nb, nb, na))
    # the rolling hoops and the rim
    for y in (.25, .5, h - .02):
        for i in range(n):
            a0, a1 = PI * i / n, PI * (i + 1) / n
            rr = r + .008
            na = (math.cos((a0 + a1) / 2), 0, -math.sin((a0 + a1) / 2))
            g.face((P(a0, y - .015, rr), P(a1, y - .015, rr), P(a1, y + .015, rr), P(a0, y + .015, rr)), f['drum-char'] if y > .4 else f['drum'], normal=na)
    # the base and the brackets to the fence
    g.face([P(PI * i / n, .025, r - .005) for i in range(n + 1)], f['drum-char'], normal=(0, 1, 0))
    for sx in (-1, 1):
        bx(g, T, sx * .2 - .025, sx * .2 + .025, .55, .62, -.03, 0.0, f['clamp'], bevel=0, cell=0)
    # coals and embers: a glowing bed at the mouth, punched holes low on the drum
    g.face([P(PI * i / n, h - .07, r - .02) for i in range(n + 1)][::-1], glow(ck.EMBER, 3.0), normal=(0, 1, 0))
    if T.detail:
        for i in range(7):
            a = PI * (i + .5) / 7
            c = P(a, .16 + .05 * (i % 2), r + .003)
            nx, nz = math.cos(a), -math.sin(a)
            tx, tz = -nz, nx
            w_, hh = .022, .028
            g.face(((c[0] - tx * w_, c[1] - hh, c[2] - tz * w_), (c[0] + tx * w_, c[1] - hh, c[2] + tz * w_), (c[0] + tx * w_, c[1] + hh, c[2] + tz * w_),
                    (c[0] - tx * w_, c[1] + hh, c[2] - tz * w_)), glow(ck.EMBER, 2.4), normal=(nx, 0, nz))
        # the grate and the logs
        for i in range(5):
            x = -.16 + .08 * i
            rod(g, (x, h + .01, z0), (x, h + .01, z0 - math.sqrt(max(0, r * r - x * x)) + .01), .008, f['metal-dark'], 4, caps=False)
        for (a, b) in (((-.12, .62, -.06), (.06, .98, -.16)), ((.13, .62, -.08), (-.02, .95, -.18)), ((0, .64, -.2), (.03, .9, -.04))):
            rod(g, a, b, .032, f['log'], 6)
            sk.lathe(g, [(.031, 0.0), (.0, .0)], b, glow(ck.EMBER, 2.2), 6, axis=sk._norm(sk._sub(b, a)), cap0=False)
    # no soot painted on the fence: a dark patch at head height behind the flame read as a hooded figure standing at the fire
    # (the 2026-11-20 capture); the smoke and the fire's light do that work
    return g


def board_frame(T, x0, x1, y0, y1, fin=None):
    """A plywood sheet on battens, flat on the fence (its front 43 mm off the face), with a thin timber frame (50 mm)."""
    f = F()
    g = Geo()
    for y in (y0 + .1, (y0 + y1) / 2, y1 - .1):
        bx(g, T, x0 + .05, x1 - .05, y - .03, y + .03, -.025, 0.0, f['timber-dark'], bevel=0, cell=0)
    bx(g, T, x0, x1, y0, y1, -.043, -.025, fin or f['ply'], bevel=.004, cell=None)
    e = .04
    for (a0, a1, b0, b1) in ((x0, x1, y0, y0 + e), (x0, x1, y1 - e, y1), (x0, x0 + e, y0, y1), (x1 - e, x1, y0, y1)):
        bx(g, T, a0, a1, b0, b1, -.05, -.043, f['timber'], bevel=0, cell=0)
    return g


def flat(g, pts, z, fin):
    """A flat polygon (x, y) on a board's face at depth z (facing -z)."""
    g.face([(x, y, z) for (x, y) in pts][::-1], fin, normal=(0, 0, -1))


def rect(g, x0, x1, y0, y1, z, fin):
    flat(g, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)], z, fin)


def war_board(T, plan):
    """The war board for a plan: the rolled city painted on plywood (its streets and buildings from the plan's blocks, the routes
    in chalk from the camp to the doors, red pins and yarn), BELVEDERE CAMP · WAR BOARD and TILESET: CITY, three empty poster frames
    (the runtime pins the run's posters), notes. Flat on the north fence: 6 cm at most."""
    f = F()
    x0, x1, y0, y1 = -1.8, 1.8, .85, 2.65
    g = board_frame(T, x0, x1, y0, y1)
    zf = -.044
    # the header band and its lettering
    rect(g, x0 + .06, x1 - .06, 2.4, 2.6, zf, f['ink'])
    ck.lettering(g, T, 'WAR BOARD', -.6, 2.43, zf - .001, .02, f['spray-white'], depth=.001)
    ck.lettering(g, T, 'TILESET: CITY', 1.0, 2.445, zf - .001, .014, f['chalk-yellow'], depth=.001)
    # the map: the plan's bounds fitted into 1.72 x 1.38 m, north up
    b = plan['bounds']
    s = min(1.72 / (b[1] - b[0]), 1.3 / (b[3] - b[2]))
    cx, cy = -.86, 1.69
    def M(x, z):
        return (cx + x * s, cy + z * s)
    W, H = (b[1] - b[0]) * s, (b[3] - b[2]) * s
    rect(g, cx - W / 2 - .01, cx + W / 2 + .01, cy - H / 2 - .01, cy + H / 2 + .01, zf, f['paper-old'])
    # the city on a 6 m grid: buildings in ink by height, the ground's levels in washes
    cell = 6.0
    nx, nz = int((b[1] - b[0]) / cell), int((b[3] - b[2]) / cell)
    grid = [[None] * nx for _ in range(nz)]
    for (bx0, bx1, bz0, bz1, top, terrace) in plan['blocks']:
        i0, i1 = max(0, int((bx0 - b[0]) / cell)), min(nx - 1, int((bx1 - b[0]) / cell))
        k0, k1 = max(0, int((bz0 - b[2]) / cell)), min(nz - 1, int((bz1 - b[2]) / cell))
        for k in range(k0, k1 + 1):
            for i in range(i0, i1 + 1):
                cur = grid[k][i]
                v = (top, terrace)
                if cur is None or (not terrace and (cur[1] or top > cur[0])) or (terrace and cur[1] and top > cur[0]):
                    grid[k][i] = v
    def tone(v):
        if v is None:
            return None
        top, terrace = v
        if terrace:
            return 'paper-old' if top >= -.5 else 'paper-blue' if top >= -9 else 'paper-pink'
        return 'photo-dark' if top > 8 else 'photo'
    for k in range(nz):
        i = 0
        while i < nx:
            t = tone(grid[k][i])
            j = i
            while j < nx and tone(grid[k][j]) == t:
                j += 1
            if t is not None and t != 'paper-old':
                ax, az = b[0] + i * cell, b[2] + k * cell
                p0, p1 = M(ax, az), M(b[0] + j * cell, az + cell)
                rect(g, p0[0], p1[0], p0[1], p1[1], zf - .0012, f[t])
            i = j
    # the routes in chalk, the camp's square, the doors' pins and the yarn from the camp
    for r, route in enumerate(plan['routes']):
        pts = [M(p[0], p[2]) for p in route]
        for (a, c) in zip(pts[:-1], pts[1:]):
            dx, dy = c[0] - a[0], c[1] - a[1]
            L = math.hypot(dx, dy) or 1
            nx_, ny_ = -dy / L * .006, dx / L * .006
            flat(g, [(a[0] + nx_, a[1] + ny_), (c[0] + nx_, c[1] + ny_), (c[0] - nx_, c[1] - ny_), (a[0] - nx_, a[1] - ny_)], zf - .0024,
                 f['chalk-yellow'] if r else f['chalk'])
        end = pts[-1]
        if T.detail:
            ck.sphere(g, (end[0], end[1], zf - .007), .006, f['ink' if r == 0 else 'pin-red'], 6, 3)
            # the red yarn from the camp's pin
            c0 = M(0, 0)
            rod(g, (c0[0], c0[1], zf - .008), (end[0], end[1], zf - .009), .0018, f['ink-red'], 3, caps=False)
    c0 = M(0, 0)
    rect(g, c0[0] - .025, c0[0] + .025, c0[1] - .025, c0[1] + .025, zf - .0036, f['ink-red'])
    if T.detail:
        ck.sphere(g, (c0[0], c0[1], zf - .007), .006, f['pin-blue'], 6, 3)
    ck.lettering(g, T, plan['name'].upper()[:22], cx, cy - H / 2 - .075, zf - .001, .008, f['ink'], depth=.001)
    # the poster frames (pencil) and their captions; the notes
    for slot in range(3):
        yc = 2.06 - .46 * slot
        for (a0, a1, b0, b1) in ((1.0, 1.56, yc - .215, yc - .209), (1.0, 1.56, yc + .209, yc + .215), (1.0, 1.006, yc - .215, yc + .215), (1.554, 1.56, yc - .215, yc + .215)):
            rect(g, a0, a1, b0, b1, zf - .0008, f['ink'])
    for i, (x, y, w_, h_) in enumerate(((.2, 2.2, .18, .14), (.45, 2.25, .16, .12), (.25, 1.05, .2, .16), (.62, 1.0, .14, .18))):
        rect(g, x - w_ / 2, x + w_ / 2, y - h_ / 2, y + h_ / 2, zf - .0014, f['paper'] if i % 2 else f['paper-pink'])
        if T.detail:
            for k in range(3):
                rect(g, x - w_ / 2 + .015, x + w_ / 2 - .03 - .02 * k, y + h_ / 2 - .035 - .03 * k, y + h_ / 2 - .028 - .03 * k, zf - .002, f['ink'])
    return g


POSTERS = {'level0': ('LEVEL 0', 0xC8B25A), 'poolrooms': ('POOLROOMS', 0x9FD0EE), 'wardzero': ('WARD ZERO', 0xE8EEF0),
           'station': ('STATION', 0xEADFC2), 'ironworks': ('IRONWORKS', 0x6A3A22)}


def poster(T, fam):
    """An instance's poster for the war board (0.56 x 0.42 m, paper on the plywood): its title and a picture of its own."""
    f = F()
    g = Geo()
    title, hexc = POSTERS[fam]
    z = -.0456
    bg = sk.fin('camp-poster-' + fam, hexc, sk.DETAIL_NONE, smooth=.1, dirt=.5)
    rect(g, -.28, .28, -.21, .21, z, bg)
    dark = f['ink'] if fam != 'ironworks' else f['paper']
    ck.lettering(g, T, title, 0, -.19, z - .001, .0105, dark, depth=.001)
    zz = z - .0012
    if fam == 'level0':
        for i in range(7):
            x = -.25 + i * .08
            rect(g, x, x + .03, -.08, .18, zz, sk.fin('camp-wallpaper-stripe', 0xA8923E, sk.DETAIL_NONE, smooth=.1, dirt=.4))
        rect(g, -.1, .1, .02, .1, zz - .0005, sk.fin('camp-exit-green', 0x36E07A, sk.DETAIL_NONE, smooth=.2, dirt=.2))
        ck.lettering(g, T, 'EXIT', 0, .04, zz - .0012, .007, f['paper'], depth=.0005)
    elif fam == 'poolrooms':
        for i in range(6):
            for k in range(4):
                if (i + k) % 2:
                    rect(g, -.24 + i * .08, -.17 + i * .08, -.07 + k * .06, -.02 + k * .06, zz, sk.fin('camp-pool-tile', 0x2F7FD0, sk.DETAIL_NONE, smooth=.3, dirt=.3))
        for i in range(10):
            x0, x1 = -.25 + i * .05, -.2 + i * .05
            flat(g, [(x0, .16 + .012 * math.sin(i)), (x1, .16 + .012 * math.sin(i + 1)), (x1, .18 + .012 * math.sin(i + 1)), (x0, .18 + .012 * math.sin(i))], zz, f['pin-blue'])
    elif fam == 'wardzero':
        for k in range(3):
            a = PI / 2 + k * 2 * PI / 3
            c = (math.cos(a) * .065, .05 + math.sin(a) * .065)
            flat(g, [(c[0] + .045 * math.cos(t * 2 * PI / 10), c[1] + .045 * math.sin(t * 2 * PI / 10)) for t in range(10)], zz, f['ink-red'])
        flat(g, [(.022 * math.cos(t * 2 * PI / 8), .05 + .022 * math.sin(t * 2 * PI / 8)) for t in range(8)], zz - .0005, sk.fin('camp-poster-wardzero', 0xE8EEF0, sk.DETAIL_NONE, smooth=.1, dirt=.5))
        ck.lettering(g, T, 'KEEP OUT', 0, -.1, zz - .0012, .0065, f['ink-red'], depth=.0005)
    elif fam == 'station':
        flat(g, [(.12 * math.cos(t * 2 * PI / 16), .05 + .12 * math.sin(t * 2 * PI / 16)) for t in range(16)], zz, f['paper'])
        for (ang, L) in ((90 - 47 * 6, .1), (90 - (6 * 30 + 47 * .5), .065)):
            a = math.radians(ang)
            d = (math.cos(a), math.sin(a))
            flat(g, [(-d[1] * .006, .05 + d[0] * .006), (d[0] * L - d[1] * .006, .05 + d[1] * L + d[0] * .006), (d[0] * L + d[1] * .006, .05 + d[1] * L - d[0] * .006), (d[1] * .006, .05 - d[0] * .006)], zz - .0006, f['ink'])
        ck.lettering(g, T, '6:47', 0, -.115, zz - .0012, .008, f['ink'], depth=.0005)
    elif fam == 'ironworks':
        flat(g, [(-.13, -.1), (.13, -.1), (.08, .05), (.03, .0), (0, .16), (-.04, .02), (-.09, .06)], zz, sk.fin('camp-furnace', 0xFF8A3A, sk.DETAIL_NONE, smooth=.1, dirt=.3))
        flat(g, [(-.05, -.08), (.05, -.08), (.02, .03), (0, .08), (-.02, .03)], zz - .0006, sk.fin('camp-furnace-core', 0xFFE070, sk.DETAIL_NONE, smooth=.1, dirt=.2))
    if T.detail:
        ck.sphere(g, (0, .195, z - .005), .0045, f['pin-red'], 6, 3)
    return g


def cross(T):
    """A red cross painted over a cleared instance's poster."""
    f = F()
    g = Geo()
    z = -.0482
    for s_ in (-1, 1):
        a, b = (-.24, s_ * -.18), (.24, s_ * .18)
        dx, dy = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dy)
        nx, ny = -dy / L * .022, dx / L * .022
        flat(g, [(a[0] + nx, a[1] + ny), (b[0] + nx, b[1] + ny), (b[0] - nx, b[1] - ny), (a[0] - nx, a[1] - ny)], z + (.0004 if s_ > 0 else 0), f['ink-red'])
    return g


def war_lamp(T):
    """The war board's desk lamp: a long hooded strip lamp over the board on two arms from the fence top (its tube glows when it
    clicks on); below 3.4 m only the plates on the face and the saddles over the top."""
    f = F()
    g = Geo()
    for sx in (-.5, .5):
        bx(g, T, sx - .04, sx + .04, 2.95, 3.2, -.03, 0.0, f['clamp'], bevel=.004, cell=0)
        bx(g, T, sx - .04, sx + .04, 3.2, 3.25, -.03, .21, f['clamp'], bevel=.004, cell=0)
        tube(g, [(sx, 3.2, -.03), (sx, 3.52, -.035), (sx, 3.6, -.1), (sx, 3.6, -.26)], .016, f['metal-dark'], 5)
    bx(g, T, -.62, .62, 3.5, 3.6, -.4, -.22, f['cd-olive'], bevel=.01, cell=0)
    bx(g, T, -.6, .6, 3.47, 3.5, -.38, -.24, f['metal-dark'], bevel=0, cell=0)
    g.face(((-.56, 3.468, -.36), (.56, 3.468, -.36), (.56, 3.468, -.26), (-.56, 3.468, -.26)), glow(0xFFE0B0, 2.8), normal=(0, -1, 0))
    return g


def missing_board(T):
    """MISSING: a plywood sheet of flyers flat on the north fence, each with its photograph and the lines under it, pinned in rows."""
    f = F()
    x0, x1, y0, y1 = -1.75, 1.75, .9, 2.55
    g = board_frame(T, x0, x1, y0, y1, f['ply-dark'])
    zf = -.044
    ck.lettering(g, T, 'MISSING', 0, 2.29, zf - .001, .032, f['ink-red'], depth=.001)
    papers = (f['paper'], f['paper-old'], f['paper-pink'], f['paper-blue'])
    k = 0
    for row in range(3):
        for col in range(8):
            w_, h_ = .28 + .05 * mk.h01(row, col, 1), .32 + .04 * mk.h01(row, col, 2)
            cx = x0 + .24 + col * .43 + .04 * (mk.h01(row, col, 3) - .5)
            cy = 1.98 - row * .44 + .02 * (mk.h01(row, col, 4) - .5)
            tilt = math.radians(6 * (mk.h01(row, col, 5) - .5))
            def R(x, y):
                return (cx + x * math.cos(tilt) - y * math.sin(tilt), cy + x * math.sin(tilt) + y * math.cos(tilt))
            z = zf - .0006 * (1 + k % 3)
            flat(g, [R(-w_ / 2, -h_ / 2), R(w_ / 2, -h_ / 2), R(w_ / 2, h_ / 2), R(-w_ / 2, h_ / 2)], z, papers[k % 4])
            if T.detail:
                flat(g, [R(-.11, h_ / 2 - .05), R(.11, h_ / 2 - .05), R(.11, h_ / 2 - .025), R(-.11, h_ / 2 - .025)], z - .0006, f['ink'])
                flat(g, [R(-.08, -.02), R(.08, -.02), R(.08, .12), R(-.08, .12)], z - .0006, f['photo'])
                flat(g, [R(-.06, -.02), R(.06, -.02), R(.045, .03), R(-.045, .03)], z - .0012, f['photo-dark'])
                flat(g, [R(.032 * math.cos(t * PI / 4), .065 + .036 * math.sin(t * PI / 4)) for t in range(8)], z - .0012, f['photo-dark'])
                for li in range(3):
                    yy = -.055 - .03 * li
                    flat(g, [R(-.11, yy), R(.11 - .04 * (li % 2), yy), R(.11 - .04 * (li % 2), yy + .012), R(-.11, yy + .012)], z - .0006, f['ink'])
                ck.sphere(g, (R(0, h_ / 2 - .015)[0], R(0, h_ / 2 - .015)[1], zf - .007), .006, f['pin-red' if k % 3 else 'pin-blue'], 6, 3)
            k += 1
    return g


def junction(T):
    """The camp's fuse board on the east fence over A1's generator: a steel box, its lamps, the conduits up and over the fence top."""
    f = F()
    g = Geo()
    bx(g, T, -.25, .25, 1.75, 2.37, -.14, 0.0, f['panel-grey'], bevel=.01, cell=0)
    bx(g, T, -.23, .23, 1.77, 2.35, -.145, -.14, f['panel-grey'], bevel=0, cell=0)
    bx(g, T, .15, .18, 2.0, 2.12, -.16, -.145, f['metal-dark'], bevel=0, cell=0)
    for i, (x, hexc) in enumerate(((-.15, 0xFF3A28), (-.08, 0xFFB054))):
        sk.lathe(g, [(.0, .0), (.018, .0), (.016, .012), (.0, .02)], (x, 2.27, -.145), glow(hexc, 3.0), 6, axis=(0, 0, -1))
    bx(g, T, -.1, .1, 2.15, 2.25, -.146, -.142, f['cd-orange'], bevel=0, cell=0)
    ck.lettering(g, T, 'DANGER', 0, 2.17, -.147, .004, f['ink'], depth=.001)
    for x in (-.12, .12):
        rod(g, (x, 2.37, -.05), (x, 3.18, -.05), .02, f['metal-dark'], 5)
        tube(g, [(x, 3.18, -.05), (x, 3.23, -.02), (x, 3.23, .1), (x, 3.2, .2)], .018, f['metal-dark'], 5)
    return g


NOTICES = {'curfew': ('CURFEW', '6:47'), 'light': ('STAY IN', 'THE LIGHT'), 'water': ('BOIL ALL', 'WATER'), 'radio': ('RADIO', '64.7 FM')}


def notice(T, which):
    """A hand-painted plywood notice flat on the fence (3 cm)."""
    f = F()
    g = Geo()
    bx(g, T, -.45, .45, 1.2, 2.2, -.025, -.005, f['ply'], bevel=.004, cell=0)
    for (x, y) in ((-.4, 1.25), (.4, 1.25), (-.4, 2.15), (.4, 2.15)):
        bx(g, T, x - .01, x + .01, y - .01, y + .01, -.028, -.025, f['metal-dark'], bevel=0, cell=0)
    l1, l2 = NOTICES[which]
    paint = f['spray-white'] if which in ('light', 'water') else f['spray-orange']
    ck.lettering(g, T, l1, 0, 1.78, -.0255, .019, paint, depth=.001)
    ck.lettering(g, T, l2, 0, 1.45, -.0255, .019, paint, depth=.001)
    return g


SIGNS = {'north': (2.0, ('CITY HALL', 'BELVEDERE CAMP'), 'paint-green'), 'E': (1.6, ('EAST GATE', 'EAST GATE'), 'paint-navy'),
         'S': (1.6, ('SOUTH GATE', 'SOUTH GATE'), 'paint-navy'), 'W': (1.6, ('WEST GATE', 'WEST GATE'), 'paint-navy')}


def gate_sign(T, which):
    """A gate's plank sign hung by two chains from the gate's string at mid span (its board 4.0-4.62 m): the camp side says where
    the gate leads (CITY HALL over the north gate), the city side BELVEDERE CAMP."""
    f = F()
    g = Geo()
    half, (inside, outside), paint = SIGNS[which]
    bx(g, T, -half, half, 4.0, 4.62, -.02, .02, f[paint], bevel=.01, cell=0)
    bx(g, T, -half - .02, half + .02, 4.58, 4.64, -.025, .025, f['timber-dark'], bevel=0, cell=0)
    px = .055 if len(inside) < 10 else .045
    ck.lettering(g, T, inside, 0, 4.31 - 3.5 * px + .03, -.021, px, f['cream-letter'] if 'cream-letter' in f else f['spray-white'], depth=.002)
    out = Geo()
    px2 = .05 if len(outside) < 12 else .036
    ck.lettering(out, T, outside, 0, 4.31 - 3.5 * px2 + .03, -.021, px2, f['spray-orange'], depth=.002)
    g.extend(out.transformed(sk.affine(mk.qy(180))))
    for sx in (-1, 1):
        x = sx * (half - .4)
        tube(g, [(x, 4.62, 0), (x, 4.71, 0), (x, 4.78, 0)], .008, f['metal-dark'], 4, caps=False)
    return g


# ===========================================================================================================================
# Part 1, in the world: the strings, the sails, the cables
# ===========================================================================================================================
def strands(T, plan):
    """Every string's wire along its catenary and its bulbs (dark where the plan says dead)."""
    f = F()
    g = Geo()
    for s in plan['strands']:
        A, B, sag = s['a'], s['b'], s['sag']
        n = max(4, int(math.hypot(B[0] - A[0], B[2] - A[2]) / (.6 if T.detail else 1.6)))
        pts = []
        for i in range(n + 1):
            t = i / n
            pts.append((A[0] + (B[0] - A[0]) * t, A[1] + (B[1] - A[1]) * t - sag * 4 * t * (1 - t), A[2] + (B[2] - A[2]) * t))
        tube(g, pts, .011 if T.detail else .016, f['cable'], 5 if T.detail else 3, caps=False)
    for (x, y, z, lit) in plan['bulbs']:
        if T.detail:
            ck.bulb(g, T, (x, y, z), lit=bool(lit), s=1.35)
        else:
            ck.sphere(g, (x, y, z), .05, glow(ck.BULB, 3.0) if lit else f['glass'], 4, 2)
    return g


def sails(T, plan):
    """The shade sails across the corners: tied at three points, bellied, hemmed, their ropes to the masts."""
    f = F()
    g = Geo()
    fins = {0x4A5A6E: 'tarp-blue', 0x6A6A3E: 'tarp-olive', 0x8A4A2A: 'tarp-rust', 0x3A7A7A: 'tarp-teal'}
    n = 7 if T.detail else 2
    for s in plan['sails']:
        A, B, C, belly = s['a'], s['b'], s['c'], s['belly']
        fin = f[fins.get(s['hex'], 'tarp-blue')]
        def P(u, v):
            w = 1 - u - v
            p = [A[i] * w + B[i] * u + C[i] * v for i in range(3)]
            p[1] -= belly * 27 * u * v * w
            return tuple(p)
        for i in range(n):
            for j in range(n - i):
                tris = [((i, j), (i + 1, j), (i, j + 1))]
                if i + j + 1 < n:
                    tris.append(((i + 1, j), (i + 1, j + 1), (i, j + 1)))
                for (a, b, c) in tris:
                    pa, pb, pc = (P(a[0] / n, a[1] / n), P(b[0] / n, b[1] / n), P(c[0] / n, c[1] / n))
                    g.face((pa, pb, pc), fin, normal=(0, 1, 0))
                    g.face((pa, pc, pb), fin, normal=(0, -1, 0))
        if T.detail:
            for (p, q) in ((A, B), (B, C), (C, A)):
                tube(g, [tuple(p), tuple(q)], .012, f['rope'], 4, caps=False)
    return g


def cables(T, plan):
    """The camp's cable along the fence's inner face at 3.05 m from the fuse board round every run of the fence (it crosses the gates
    on their strings), clipped every 1.2 m and drooping a little between; the drop to the fuse board."""
    f = F()
    g = Geo()
    inner = plan['inner']
    for side in range(4):
        n = [(0, 0, 1), (1, 0, 0), (0, 0, -1), (-1, 0, 0)][side]
        r = [(1, 0, 0), (0, 0, -1), (-1, 0, 0), (0, 0, 1)][side]
        for sign in (-1, 1):
            a0, a1 = sign * 5.5, sign * (inner - .3)
            k = max(2, int(abs(a1 - a0) / 1.2))
            pts = []
            for i in range(k * 3 + 1):
                t = i / (k * 3)
                a = a0 + (a1 - a0) * t
                droop = .035 * math.sin(PI * ((i % 3) / 3.0)) if i % 3 else 0
                d = inner - .022
                pts.append((n[0] * d + r[0] * a, 3.05 - droop, n[2] * d + r[2] * a))
            tube(g, pts, .009 if T.detail else .014, f['cable'], 4 if T.detail else 3, caps=False)
    # the drop from the run to the fuse board on the east fence (z -12.5)
    tube(g, [(inner - .022, 3.05, -12.5 + .3), (inner - .03, 2.7, -12.5 + .3), (inner - .03, 2.4, -12.5 + .28)], .012, f['cable'], 4, caps=False)
    return g


# ===========================================================================================================================
# Part 2, the DEV preview: A1's structures on their blocks (frame: the row's centre on the floor)
# ===========================================================================================================================
def ring(T):
    """The mess ring: four corrugated roof panels on four timber posts at 2.6-2.8 m, open over the fire; beams and purlins tucked
    under the panels."""
    f = F()
    g = Geo()
    for (x, z) in ((4.4, 4.4), (-4.4, 4.4), (4.4, -4.4), (-4.4, -4.4)):
        bx(g, T, x - .15, x + .15, 0.0, 2.6, z - .15, z + .15, f['timber'], bevel=.02)
        bx(g, T, x - .17, x + .17, 0.0, .05, z - .17, z + .17, f['clamp'], bevel=0, cell=0)
    panels = (((-4.6, 4.6, 2.2, 4.6), 'sheet-rust'), ((-4.6, 4.6, -4.6, -2.2), 'sheet-teal'), ((2.2, 4.6, -2.2, 2.2), 'sheet-cream'), ((-4.6, -2.2, -2.2, 2.2), 'sheet-red'))
    for (x0, x1, z0, z1), fin in panels:
        # the roof sheet on top (ribs running out from the fire), the panel's edge trims and purlins underneath
        along_x = (x1 - x0) < (z1 - z0)
        sub = Geo()
        if along_x:
            ck.corrugated(sub, T, z0, z1, 0.0, x1 - x0, 0.0, f[fin], n=-1, depth=.02)
            q = mk.qmul(mk.qy(90), mk.qx(90))
            g.extend(sub.transformed(sk.affine(q, (x0, 2.78, 0))))
        else:
            ck.corrugated(sub, T, x0, x1, 0.0, z1 - z0, 0.0, f[fin], n=-1, depth=.02)
            g.extend(sub.transformed(sk.affine(mk.qx(90), (0, 2.78, z0))))
        bx(g, T, x0, x1, 2.6, 2.72, z0, z1, f['timber-dark'], bevel=.01, skip=('+y',))
    # the beams between the posts, under the outer edges
    for (a, b) in (((-4.4, 4.4), (4.4, 4.4)), ((-4.4, -4.4), (4.4, -4.4)), ((4.4, -4.4), (4.4, 4.4)), ((-4.4, -4.4), (-4.4, 4.4))):
        mk.seg(g, T, (a[0], 2.55, a[1]), (b[0], 2.55, b[1]), .14, .1, f['timber'])
    ck.lettering(g, T, 'MESS', 0, 2.62, -2.2 - .005, .02, f['spray-white'], depth=.002) if T.detail else None
    return g


def fountain(T):
    """The square stone fountain (3.6 x 0.9 x 3.6 m): its walls and coping, the basin filled with sand to 0.8 m and the iron brazier
    sunk in it (its rim at the fountain's top; the fire over it is the effects')."""
    f = F()
    g = Geo()
    h, w, t = .9, 1.8, .3
    for (x0, x1, z0, z1) in ((-w, w, w - t, w), (-w, w, -w, -w + t), (w - t, w, -w + t, w - t), (-w, -w + t, -w + t, w - t)):
        bx(g, T, x0, x1, 0.0, h - .08, z0, z1, f['stone'], bevel=.02)
    for (x0, x1, z0, z1) in ((-w, w, w - t - .02, w), (-w, w, -w, -w + t + .02), (w - t - .02, w, -w + t, w - t), (-w, -w + t + .02, -w + t, w - t)):
        bx(g, T, x0, x1, h - .08, h, z0, z1, f['stone-dark'], bevel=.02)
    bx(g, T, -w + t, w - t, 0.0, .8, -w + t, w - t, sk.fin('camp-sand', 0x8A7E66, sk.DETAIL_FLAG, smooth=.1, dirt=1.0, param=.3), bevel=0, skip=('-y',))
    g.face([(math.cos(2 * PI * k / 16) * .95, .802, math.sin(2 * PI * k / 16) * .95) for k in range(16)], f['ash'], normal=(0, 1, 0))
    # the brazier: an iron bowl on its plinth in the sand, the coals glowing in it
    n = 14 if T.detail else 7
    sk.lathe(g, [(.3, .62), (.45, .7), (.56, .82), (.6, .9)], (0, 0, 0), f['drum'], n)
    sk.lathe(g, [(.6, .9), (.55, .9)], (0, 0, 0), f['drum-char'], n)
    # the bowl's inside (its own faces, turned in)
    sk.lathe(g, [(.55, .9), (.5, .84), (.4, .74), (.25, .68)], (0, 0, 0), f['drum-char'], n)
    g.face([(math.cos(2 * PI * k / n) * .5, .83, math.sin(2 * PI * k / n) * .5) for k in range(n)], glow(ck.EMBER, 3.2), normal=(0, 1, 0))
    if T.detail:
        for k in range(5):
            a = 2 * PI * k / 5
            p0 = (math.cos(a) * .35, .82, math.sin(a) * .35)
            p1 = (math.cos(a + .6) * .05, .9, math.sin(a + .6) * .05)
            rod(g, p0, p1, .04, f['log'], 6)
    return g


def bench(T):
    """A bench round the fire (its block 0.9 x 1.4 x 0.7, the seated survivor fills the rest): the sitter faces +z."""
    f = F()
    g = Geo()
    bx(g, T, -.45, .45, .42, .47, -.27, .17, f['timber'], bevel=.01, cell=0)
    for x in (-.4, .4):
        for z in (-.24, .12):
            bx(g, T, x - .03, x + .03, 0.0, .42, z - .03, z + .03, f['timber-dark'], bevel=.005, cell=0)
        bx(g, T, x - .03, x + .03, .47, 1.02, -.33, -.27, f['timber-dark'], bevel=.005, cell=0)
    for y in (.62, .8, .96):
        bx(g, T, -.45, .45, y, y + .1, -.32, -.29, f['timber'], bevel=.005, cell=0)
    return g


def bench_waiting(T):
    """The fourth bench, kept for the next one rescued: a folded blanket stack, a crate and a lit lantern on its seat."""
    f = F()
    g = Geo()
    for i, fin in enumerate(('blanket-red', 'blanket-grey', 'blanket-green')):
        y = .47 + i * .11
        bx(g, T, -.38 + .02 * i, .02 + .02 * i, y, y + .11, -.22, .1, f[fin], bevel=.03, cell=0)
    bx(g, T, .08, .42, .47, .82, -.24, .12, f['crate'], bevel=.01, cell=0)
    ck.hand_lantern(g, T, .25, .82, -.06, 1.0, lit=True)
    return g


def _hatch(g, T, face, a0, a1, y0, y1, f, depth=.6, mesh_top=True, counter=True):
    """A serving hatch in a cabin's face: the opening's frame, its counter, the mesh over its upper half; `face` (axis, sign, at)."""
    axis, sign, at = face
    def P(a, y, d):
        # a along the face, d inward from it
        return (at - sign * d, y, a) if axis == 'x' else (a, y, at - sign * d)
    fins = (f['timber-dark'],)
    for (b0, b1, c0, c1) in ((a0 - .05, a0, y0, y1), (a1, a1 + .05, y0, y1), (a0 - .05, a1 + .05, y1, y1 + .06), (a0 - .05, a1 + .05, y0 - .06, y0)):
        p, q = P(b0, c0, 0), P(b1, c1, .06)
        bx(g, T, p[0], q[0], p[1], q[1], p[2], q[2], fins[0], bevel=0, cell=0)
    if counter:
        p, q = P(a0, y0 - .02, 0), P(a1, y0 + .03, .45)
        bx(g, T, p[0], q[0], p[1], q[1], p[2], q[2], f['timber'], bevel=.005, cell=0)
    if mesh_top and T.detail:
        ym = (y0 + y1) / 2 + .1
        k = int((a1 - a0) / .07)
        for i in range(k + 1):
            a = a0 + (a1 - a0) * i / k
            p, q = P(a, ym, .01), P(a, y1, .01)
            rod(g, p, q, .005, f['mesh'], 3, caps=False)
        for y in [ym + i * .07 for i in range(int((y1 - ym) / .07) + 1)]:
            rod(g, P(a0, y, .012), P(a1, y, .012), .005, f['mesh'], 3, caps=False)
    # the dark inside beyond the opening
    p, q = P(a0, y0, depth), P(a1, y1, depth + .02)
    bx(g, T, p[0], q[0], p[1], q[1], p[2], q[2], f['interior'], bevel=0, cell=0)


def _shelves(g, T, f, x0, x1, z, y0, y1, n=3, depth=.3, face=1):
    """Shelves of goods against a wall plane z (facing face*z)."""
    for i in range(n):
        y = y0 + (y1 - y0) * i / max(1, n - 1)
        bx(g, T, x0, x1, y, y + .025, z, z + face * depth, f['timber'], bevel=0, cell=0)
        if T.detail:
            k = 0
            x = x0 + .05
            while x < x1 - .1:
                w_ = .08 + .08 * mk.h01(i, k, 3)
                hh = .1 + .14 * mk.h01(i, k, 4)
                fin = (f['crate'], f['cd-olive'], f['paper-old'], f['cd-white'], f['sheet-red'])[k % 5]
                bx(g, T, x, x + w_, y + .025, y + .025 + hh, z + face * .03, z + face * (depth - .04), fin, bevel=.005, cell=0)
                x += w_ + .03
                k += 1


def kiosk(T):
    """The trading kiosk (2.6 x 3.2 x 2.6 m): mismatched corrugated walls on a timber frame, its hatches on the south and west faces
    at the corner nearest the chip machine (counter, mesh), shelves of goods inside, TRADE and QM painted on, and the striped awning
    over the machine from its top edge to the two awning posts (art above 3.1 m)."""
    f = F()
    g = Geo()
    h, w = 3.2, 1.3
    for (x, z) in ((w, w), (-w, w), (w, -w), (-w, -w)):
        bx(g, T, x - .06 if x > 0 else x, x if x > 0 else x + .06, 0.0, h, z - .06 if z > 0 else z, z if z > 0 else z + .06, f['timber-dark'], bevel=.005, cell=0)
    # walls (the hatch openings left out of the south and west sheets)
    ck.corrugated(g, T, -w, w, 0.0, h - .1, w, f['sheet-cream'], n=1)
    ck.side_corrugated(g, T, -w, w, 0.0, h - .1, w, f['sheet-red'], n=1)
    for (a0, a1, y0, y1) in ((-.1, w, 0.0, h - .1), (-w, -.1, 0.0, .95), (-w, -.1, 1.9, h - .1)):
        ck.corrugated(g, T, a0, a1, y0, y1, -w, f['sheet-teal'], n=-1)
    for (a0, a1, y0, y1) in ((-.1, w, 0.0, h - .1), (-w, -.1, 0.0, .95), (-w, -.1, 1.9, h - .1)):
        ck.side_corrugated(g, T, a0, a1, y0, y1, -w, f['sheet-rust'], n=-1)
    bx(g, T, -w + .04, w - .04, h - .14, h - .02, -w + .04, w - .04, f['timber-dark'], bevel=0, cell=0)
    sub = Geo()
    ck.corrugated(sub, T, -w - .05, w + .05, 0.0, 2 * w + .1, 0.0, f['sheet-galv'], n=-1, depth=.02)
    g.extend(sub.transformed(sk.affine(mk.qx(90), (0, h, -w - .05))))
    _hatch(g, T, ('z', -1, -w), -w + .15, -.15, .95, 1.9, f)
    _hatch(g, T, ('x', -1, -w), -w + .15, -.15, .95, 1.9, f)
    if T.detail:
        _shelves(g, T, f, -w + .1, w - .15, w - .05, .9, 2.4, n=4, depth=.3, face=-1)
        bulb_at = (-.4, h - .35, -.4)
        ck.bulb(g, T, bulb_at, lit=True, s=1.4)
        ck.lettering(g, T, 'TRADE', -.65, 2.15, -w - .02, .045, f['spray-orange'], depth=.002)
        side = Geo()
        ck.lettering(side, T, 'QM', 0.0, 2.15, 0.0, .06, f['spray-white'], depth=.002)
        g.extend(side.transformed(sk.affine(mk.qy(90), (-w - .02, 0, -.65))))
    # the awning posts (A1's 0.3 m blocks) and the awning over the chip machine
    posts = ((14.6 - 16.4, 12.4 - 16.4), (12.4 - 16.4, 14.6 - 16.4))
    for (x, z) in posts:
        bx(g, T, x - .15, x + .15, 0.0, 3.4, z - .15, z + .15, f['timber'], bevel=.015)
    corners = [(-w, h, .35), (.35, h, -w), (posts[0][0], 3.42, posts[0][1]), (posts[1][0], 3.42, posts[1][1])]
    k0, k1, p0, p1 = corners
    stripes = 8
    for i in range(stripes):
        t0, t1 = i / stripes, (i + 1) / stripes
        a, b = [k1[j] + (k0[j] - k1[j]) * t0 for j in range(3)], [k1[j] + (k0[j] - k1[j]) * t1 for j in range(3)]
        c, d = [p0[j] + (p1[j] - p0[j]) * t1 for j in range(3)], [p0[j] + (p1[j] - p0[j]) * t0 for j in range(3)]
        fin = f['canvas-red'] if i % 2 else f['canvas-stripe']
        g.face((tuple(a), tuple(b), tuple(c), tuple(d)), fin, normal=(0, 1, 0))
        g.face((tuple(a), tuple(d), tuple(c), tuple(b)), fin, normal=(0, -1, 0))
    mk.seg(g, T, (posts[0][0], 3.36, posts[0][1]), (posts[1][0], 3.36, posts[1][1]), .08, .08, f['timber'])
    return g


def infirmary(T):
    """The infirmary (3.0 x 2.6 x 3.0 m): a hard-sided field cabin, red crosses, Doc's hatches on the north and east faces at the corner
    nearest the med machine, the nurse's window, cots and medkits inside."""
    f = F()
    g = Geo()
    w, h = 1.5, 2.6
    for (x0, x1, z0, z1) in ((-w, w, w - .05, w), (-w, w, -w, -w + .05), (w - .05, w, -w, w), (-w, -w + .05, -w, w)):
        pass
    # panels with seams: south and west whole, north and east with their openings
    def panels(face, a0, a1, y0, y1, holes):
        axis, sign, at = face
        x = a0
        segs = []
        cuts = sorted(holes)
        cur = a0
        for (h0, h1, hy0, hy1) in cuts:
            segs.append((cur, h0, y0, y1))
            segs.append((h0, h1, y0, hy0))
            segs.append((h0, h1, hy1, y1))
            cur = h1
        segs.append((cur, a1, y0, y1))
        for (b0, b1, c0, c1) in segs:
            if b1 - b0 < 1e-3 or c1 - c0 < 1e-3:
                continue
            if axis == 'z':
                bx(g, T, b0, b1, c0, c1, at - .05 if sign > 0 else at, at if sign > 0 else at + .05, f['panel-white'], bevel=0)
            else:
                bx(g, T, at - .05 if sign > 0 else at, at if sign > 0 else at + .05, c0, c1, b0, b1, f['panel-white'], bevel=0)
    panels(('z', 1, w), -w, w, 0.0, h, [(-1.3, -.3, 1.05, 1.85), (.2, 1.3, 1.0, 1.85)])
    panels(('x', 1, w), -w, w, 0.0, h, [(.2, 1.3, 1.0, 1.85)])
    panels(('z', -1, -w), -w, w, 0.0, h, [])
    panels(('x', -1, -w), -w, w, 0.0, h, [])
    bx(g, T, -w - .03, w + .03, h - .1, h, -w - .03, w + .03, f['panel-grey'], bevel=.01)
    for (x, z) in ((w, w), (-w, w), (w, -w), (-w, -w)):
        bx(g, T, x - .05, x + .05, 0.0, h - .1, z - .05, z + .05, f['panel-grey'], bevel=.005, cell=0)
    _hatch(g, T, ('z', 1, w), .2, 1.3, 1.0, 1.85, f, mesh_top=False)
    _hatch(g, T, ('x', 1, w), .2, 1.3, 1.0, 1.85, f, mesh_top=False)
    # the nurse's window frame (its glass is camp/glass/infirmary)
    for (a0, a1, b0, b1) in ((-1.33, -1.3, 1.05, 1.85), (-.3, -.27, 1.05, 1.85), (-1.33, -.27, 1.02, 1.05), (-1.33, -.27, 1.85, 1.88)):
        bx(g, T, a0, a1, b0, b1, w - .07, w + .005, f['panel-grey'], bevel=0, cell=0)
    bx(g, T, -1.3, -.3, 1.05, 1.85, w - .6, w - .58, f['interior'], bevel=0, cell=0)
    # red crosses on the south and west faces
    for (axis, at) in (('z', -w), ('x', -w)):
        cr = Geo()
        for (a0, a1, b0, b1) in ((-.3, .3, 1.5, 1.7), (-.1, .1, 1.3, 1.9)):
            cr.face(((a0, b0, 0), (a1, b0, 0), (a1, b1, 0), (a0, b1, 0)), f['red-cross'], normal=(0, 0, -1))
        if axis == 'z':
            g.extend(cr.transformed(sk.affine((0, 0, 0, 1), (0, 0, at - .003))))
        else:
            g.extend(cr.transformed(sk.affine(mk.qy(90), (at - .003, 0, 0))))
    if T.detail:
        _shelves(g, T, f, -1.4, -.2, -w + .05, .9, 2.0, n=3, depth=.3, face=1)
        bx(g, T, .2, 1.3, .4, .6, -1.3, -.6, f['cd-white'], bevel=.02, cell=0)
        ck.bulb(g, T, (.6, h - .4, .6), lit=True, s=1.4)
    return g


def glass_infirmary(T):
    g = Geo()
    g.face(((-1.3, 1.05, 1.47), (-.3, 1.05, 1.47), (-.3, 1.85, 1.47), (-1.3, 1.85, 1.47)), F()['glass'], normal=(0, 0, 1))
    return g


def lookout(T):
    """The lookout: a 9 m tower clad in planks and sheet on a timber frame (it reads as solid, as its block is), braces outside, a
    door at its foot; the glazed cabin at 9-11.6 m with its walkway rail; the searchlight's turntable and drum on the roof."""
    f = F()
    g = Geo()
    w = 1.5
    for (x, z) in ((w, w), (-w, w), (w, -w), (-w, -w)):
        bx(g, T, x - .1 if x > 0 else x, x if x > 0 else x + .1, 0.0, 9.0, z - .1 if z > 0 else z, z if z > 0 else z + .1, f['timber-dark'], bevel=.01)
    for i, (y0, y1) in enumerate(((0.0, 3.0), (3.0, 6.0), (6.0, 9.0))):
        ck.corrugated(g, T, -w + .1, w - .1, y0, y1, -w, f['sheet-rust' if i % 2 else 'sheet-galv'], n=-1)
        ck.corrugated(g, T, -w + .1, w - .1, y0, y1, w, f['sheet-galv' if i % 2 else 'sheet-teal'], n=1)
        ck.side_corrugated(g, T, -w + .1, w - .1, y0, y1, -w, f['sheet-teal' if i % 2 else 'sheet-rust'], n=-1)
        ck.side_corrugated(g, T, -w + .1, w - .1, y0, y1, w, f['sheet-galv'], n=1)
    if T.detail:
        for (y0, y1) in ((.3, 2.9), (3.1, 5.9), (6.1, 8.9)):
            for (a, b) in (((-w, y0, -w - .03), (w, y1, -w - .03)), ((w, y0, w + .03), (-w, y1, w + .03))):
                mk.seg(g, T, a, b, .1, .05, f['timber'])
    bx(g, T, -.45, .45, 0.0, 2.1, w + .005, w + .04, f['timber'], bevel=.01, cell=0)
    # the cabin
    cw = 1.8
    bx(g, T, -cw, cw, 9.0, 9.15, -cw, cw, f['timber-dark'], bevel=.01)
    for (x, z) in ((cw, cw), (-cw, cw), (cw, -cw), (-cw, -cw)):
        bx(g, T, x - .08 if x > 0 else x, x if x > 0 else x + .08, 9.15, 11.45, z - .08 if z > 0 else z, z if z > 0 else z + .08, f['timber-dark'], bevel=.005, cell=0)
    for (axis, sign) in (('z', 1), ('z', -1), ('x', 1), ('x', -1)):
        if axis == 'z':
            ck.corrugated(g, T, -cw + .08, cw - .08, 9.15, 10.1, sign * cw, f['sheet-cream'], n=sign)
            bx(g, T, -cw, cw, 10.1, 10.16, sign * cw - .06 if sign > 0 else sign * cw, sign * cw if sign > 0 else sign * cw + .06, f['timber'], bevel=0, cell=0)
            for x in (-.6, .6):
                bx(g, T, x - .03, x + .03, 10.16, 11.35, sign * cw - .05 if sign > 0 else sign * cw, sign * cw if sign > 0 else sign * cw + .05, f['timber'], bevel=0, cell=0)
        else:
            ck.side_corrugated(g, T, -cw + .08, cw - .08, 9.15, 10.1, sign * cw, f['sheet-cream'], n=sign)
            bx(g, T, sign * cw - .06 if sign > 0 else sign * cw, sign * cw if sign > 0 else sign * cw + .06, 10.1, 10.16, -cw, cw, f['timber'], bevel=0, cell=0)
            for z in (-.6, .6):
                bx(g, T, sign * cw - .05 if sign > 0 else sign * cw, sign * cw if sign > 0 else sign * cw + .05, 10.16, 11.35, z - .03, z + .03, f['timber'], bevel=0, cell=0)
    bx(g, T, -cw - .15, cw + .15, 11.35, 11.6, -cw - .15, cw + .15, f['sheet-galv'], bevel=.02)
    bx(g, T, -cw + .1, cw - .1, 9.15, 9.2, -cw + .1, cw - .1, f['interior'], bevel=0, cell=0)
    # the searchlight: a turntable and the drum aimed over the city, its lens glowing
    sk.lathe(g, [(.5, 11.6), (.5, 11.72), (.4, 11.76)], (0, 0, 0), f['metal-dark'], 12 if T.detail else 6, cap1=True)
    bx(g, T, -.42, .42, 11.76, 12.0, -.15, .15, f['cd-olive'], bevel=.01, cell=0)
    d = sk._norm((-.5, -.25, .83))
    c = (0, 12.25, 0)
    sk.lathe(g, [(.0, -.28), (.34, -.28), (.38, -.1), (.38, .22), (.34, .26)], c, f['cd-olive'], 14 if T.detail else 7, axis=d, cap0=True)
    sk.lathe(g, [(.33, .25), (.0, .25)], c, glow(0xFFE6C0, 3.6), 14 if T.detail else 7, axis=d)
    return g


def glass_lookout(T):
    g = Geo()
    cw = 1.8
    for sign in (1, -1):
        g.face(((-cw + .1, 10.16, sign * (cw - .03)), (cw - .1, 10.16, sign * (cw - .03)), (cw - .1, 11.35, sign * (cw - .03)), (-cw + .1, 11.35, sign * (cw - .03))), F()['glass'], normal=(0, 0, sign))
        g.face(((sign * (cw - .03), 10.16, -cw + .1), (sign * (cw - .03), 10.16, cw - .1), (sign * (cw - .03), 11.35, cw - .1), (sign * (cw - .03), 11.35, -cw + .1)), F()['glass'], normal=(sign, 0, 0))
    return g


def sentry(T):
    """A sentry tower (2.0 x 5.2 x 2.0 m; +z toward the fence): a sandbagged base to 3.0 m, the mesh cabin to 5.2 m with its guard
    inside, a ladder flush on the camp side."""
    f = F()
    g = Geo()
    w = 1.0
    for (x, z) in ((w, w), (-w, w), (w, -w), (-w, -w)):
        bx(g, T, x - .08 if x > 0 else x, x if x > 0 else x + .08, 0.0, 5.0, z - .08 if z > 0 else z, z if z > 0 else z + .08, f['timber-dark'], bevel=.008, cell=0)
    # the base: sandbag courses on three faces, sheet on the fence side
    for i in range(10):
        y = i * .3
        off = (i % 2) * .22
        for (axis, sign) in (('z', -1), ('x', 1), ('x', -1)):
            k = 0
            a = -w + .05 - off
            while a < w - .05:
                a0, a1 = max(-w + .05, a), min(w - .05, a + .44)
                if a1 - a0 > .08:
                    if axis == 'z':
                        bx(g, T, a0, a1, y, y + .3, -w - .02, -w + .22, f['sandbag' if (i + k) % 3 else 'sandbag-dark'], bevel=.06 if T.detail else 0, cell=0)
                    else:
                        bx(g, T, sign * w - .22 if sign > 0 else -w - .02, sign * w + .02 if sign > 0 else -w + .22, y, y + .3, a0, a1, f['sandbag' if (i + k) % 3 else 'sandbag-dark'], bevel=.06 if T.detail else 0, cell=0)
                a += .44
                k += 1
    ck.corrugated(g, T, -w, w, 0.0, 3.0, w, f['sheet-galv'], n=1)
    # the cabin
    bx(g, T, -w, w, 3.0, 3.08, -w, w, f['timber-dark'], bevel=0)
    for (axis, sign) in (('z', 1), ('z', -1), ('x', 1), ('x', -1)):
        if axis == 'z':
            ck.corrugated(g, T, -w + .08, w - .08, 3.08, 3.95, sign * w, f['sheet-rust'], n=sign)
        else:
            ck.side_corrugated(g, T, -w + .08, w - .08, 3.08, 3.95, sign * w, f['sheet-rust'], n=sign)
        if T.detail:
            for k in range(12):
                a = -w + .1 + k * (2 * w - .2) / 11
                if axis == 'z':
                    rod(g, (a, 3.95, sign * (w - .02)), (a, 4.95, sign * (w - .02)), .006, f['mesh'], 3, caps=False)
                else:
                    rod(g, (sign * (w - .02), 3.95, a), (sign * (w - .02), 4.95, a), .006, f['mesh'], 3, caps=False)
            for y in (4.2, 4.45, 4.7, 4.95):
                if axis == 'z':
                    rod(g, (-w + .1, y, sign * (w - .02)), (w - .1, y, sign * (w - .02)), .006, f['mesh'], 3, caps=False)
                else:
                    rod(g, (sign * (w - .02), y, -w + .1), (sign * (w - .02), y, w - .1), .006, f['mesh'], 3, caps=False)
    bx(g, T, -w - .08, w + .08, 4.98, 5.2, -w - .08, w + .08, f['sheet-galv'], bevel=.02)
    ck.lettering(g, T, 'CD', 0, 3.3, -w - .02, .04, f['cd-white'], depth=.002) if T.detail else None
    # the ladder up the camp-side face
    for x in (-.25, .25):
        bx(g, T, x - .03, x + .03, 0.0, 3.1, -w - .07, -w - .02, f['timber'], bevel=0, cell=0)
    for i in range(10):
        y = .3 + i * .3
        rod(g, (-.25, y, -w - .045), (.25, y, -w - .045), .016, f['timber'], 4, caps=False)
    return g


def radio(T):
    """The radio shack (4.0 x 3.3 x 1.8 m, against the north fence): plank walls, two glazed windows on the camp side (its glass is
    camp/glass/radio), the desk, the set with its glowing dials and the lamp inside, 64.7 FM painted over the windows."""
    f = F()
    g = Geo()
    w, d, h = 2.0, .9, 3.3
    for (x, z) in ((w, d), (-w, d), (w, -d), (-w, -d)):
        bx(g, T, x - .08 if x > 0 else x, x if x > 0 else x + .08, 0.0, h - .15, z - .08 if z > 0 else z, z if z > 0 else z + .08, f['timber-dark'], bevel=.005, cell=0)
    holes = [(-1.6, -.2, 1.0, 2.1), (.2, 1.6, 1.0, 2.1)]
    cur = -w
    for (a0, a1, y0, y1) in holes:
        bx(g, T, cur, a0, 0.0, h - .15, -d, -d + .05, f['ply-dark'], bevel=0)
        bx(g, T, a0, a1, 0.0, y0, -d, -d + .05, f['ply-dark'], bevel=0)
        bx(g, T, a0, a1, y1, h - .15, -d, -d + .05, f['ply-dark'], bevel=0)
        cur = a1
        for (b0, b1, c0, c1) in ((a0 - .04, a0, y0, y1), (a1, a1 + .04, y0, y1), (a0 - .04, a1 + .04, y0 - .04, y0), (a0 - .04, a1 + .04, y1, y1 + .04)):
            bx(g, T, b0, b1, c0, c1, -d - .02, -d + .03, f['paint-cream'], bevel=0, cell=0)
    bx(g, T, cur, w, 0.0, h - .15, -d, -d + .05, f['ply-dark'], bevel=0)
    ck.side_corrugated(g, T, -d, d, 0.0, h - .15, w, f['sheet-teal'], n=1)
    ck.side_corrugated(g, T, -d, d, 0.0, h - .15, -w, f['sheet-teal'], n=-1)
    ck.corrugated(g, T, -w, w, 0.0, h - .15, d, f['sheet-galv'], n=1)
    sub = Geo()
    ck.corrugated(sub, T, -w - .05, w + .05, 0.0, 2 * d + .1, 0.0, f['sheet-galv'], n=-1, depth=.02)
    g.extend(sub.transformed(sk.affine(mk.qx(90), (0, h, -d - .05))))
    bx(g, T, -w, w, h - .15, h - .05, -d, d, f['timber-dark'], bevel=0, skip=('+y',))
    bx(g, T, -w + .05, w - .05, .02, .06, -d + .05, d - .05, f['interior'], bevel=0, cell=0)
    bx(g, T, -w + .05, w - .05, .05, h - .2, d - .1, d - .05, f['interior'], bevel=0, cell=0)
    # the desk, the set, the lamp
    bx(g, T, -1.9, 1.9, .72, .78, -.75, -.2, f['timber'], bevel=.005, cell=0)
    if T.detail:
        bx(g, T, -1.3, -.5, .78, 1.12, -.55, -.25, f['metal-dark'], bevel=.01, cell=0)
        for i in range(4):
            x = -1.2 + i * .2
            sk.lathe(g, [(.0, .0), (.035, .0), (.03, .01)], (x, 1.0, -.555), glow(0xFFB054, 2.6), 8, axis=(0, 0, -1))
        ck.bulb(g, T, (0, 2.6, -.3), lit=True, s=1.4)
        ck.lettering(g, T, '64.7 FM', 0, 2.3, -d - .022, .04, f['spray-orange'], depth=.002)
    return g


def glass_radio(T):
    g = Geo()
    for (a0, a1) in ((-1.6, -.2), (.2, 1.6)):
        g.face(((a0, 1.0, -.87), (a1, 1.0, -.87), (a1, 2.1, -.87), (a0, 2.1, -.87)), F()['glass'], normal=(0, 0, -1))
    return g


def supply(T):
    """The supply cabin (3.0 x 3.3 x 2.0 m, against the south fence; +z toward it): a container's corrugated steel in Civil Defence
    olive, SUPPLIES stencilled, a window on the camp side (camp/glass/supply) with the stores stacked behind it, a shut door."""
    f = F()
    g = Geo()
    w, d, h = 1.5, 1.0, 3.3
    ck.corrugated(g, T, -w, w, 0.0, h - .1, d, f['cd-olive'], n=1)
    ck.side_corrugated(g, T, -d, d, 0.0, h - .1, w, f['cd-olive'], n=1)
    ck.side_corrugated(g, T, -d, d, 0.0, h - .1, -w, f['cd-olive'], n=-1)
    for (a0, a1, y0, y1) in ((-w, -.7, 0.0, h - .1), (-.7, .7, 0.0, 1.1), (-.7, .7, 2.0, h - .1), (.7, w, 0.0, h - .1)):
        ck.corrugated(g, T, a0, a1, y0, y1, -d, f['cd-olive'], n=-1)
    for (b0, b1, c0, c1) in ((-.74, -.7, 1.1, 2.0), (.7, .74, 1.1, 2.0), (-.74, .74, 1.06, 1.1), (-.74, .74, 2.0, 2.04)):
        bx(g, T, b0, b1, c0, c1, -d - .03, -d + .02, f['metal-dark'], bevel=0, cell=0)
    bx(g, T, -w - .03, w + .03, h - .1, h, -d - .03, d + .03, f['cd-olive'], bevel=.01)
    for (x, z) in ((w, d), (-w, d), (w, -d), (-w, -d)):
        bx(g, T, x - .06 if x > 0 else x, x if x > 0 else x + .06, 0.0, h, z - .06 if z > 0 else z, z if z > 0 else z + .06, f['metal-dark'], bevel=.005, cell=0)
    bx(g, T, .85, 1.4, .05, 2.3, -d - .025, -d - .005, f['cd-olive'], bevel=.005, cell=0)
    if T.detail:
        ck.lettering(g, T, 'SUPPLIES', -.0, 2.45, -d - .03, .035, f['cd-white'], style='stencil', depth=.002)
        for i in range(3):
            for k in range(2):
                bx(g, T, -.65 + i * .44, -.27 + i * .44, .3 + k * .4, .68 + k * .4, -.5, -.1, f['crate'], bevel=.01, cell=0)
        bx(g, T, -.7, .7, 1.1, 2.0, -.05, 0.0, f['interior'], bevel=0, cell=0)
    return g


def glass_supply(T):
    g = Geo()
    g.face(((-.7, 1.1, -.985), (.7, 1.1, -.985), (.7, 2.0, -.985), (-.7, 2.0, -.985)), F()['glass'], normal=(0, 0, -1))
    return g


def generator(T):
    """A diesel generator in its canopy (local x 2.0 m long, z 1.0 deep with +z toward the east fence, 1.4 m tall): louvres, its
    control panel's lamps, the exhaust kept inside its block, the skid."""
    f = F()
    g = Geo()
    bx(g, T, -1.0, 1.0, 0.0, .1, -.5, .5, f['metal-dark'], bevel=0)
    bx(g, T, -.96, .96, .1, 1.36, -.46, .46, f['generator'], bevel=.03)
    bx(g, T, -1.0, 1.0, 1.36, 1.4, -.5, .5, f['generator'], bevel=.01)
    if T.detail:
        for i in range(8):
            y = .3 + i * .12
            bx(g, T, -.85, -.2, y, y + .05, -.48, -.46, f['metal-dark'], bevel=0, cell=0)
        bx(g, T, .3, .8, .7, 1.15, -.48, -.46, f['panel-grey'], bevel=.005, cell=0)
        for i, hexc in enumerate((0x36E07A, 0xFFB054)):
            sk.lathe(g, [(.0, .0), (.02, .0), (.018, .008)], (.42 + i * .1, 1.05, -.481), glow(hexc, 2.8), 6, axis=(0, 0, -1))
        rod(g, (.7, 1.4, .2), (.7, 1.4, .45), .05, f['metal-dark'], 6)
        bx(g, T, -.95, -.6, .1, .45, .25, .45, f['sheet-red'], bevel=.02, cell=0)
    return g


def sandbag(T):
    """A sandbag wall (2.4 x 1.0 x 0.6 m, local x along it): four courses, two bags deep, staggered."""
    f = F()
    g = Geo()
    for i in range(4):
        y = i * .25
        off = (i % 2) * .3
        for row, (z0, z1) in enumerate(((-.3, .0), (.0, .3))):
            a = -1.2 - off
            k = 0
            while a < 1.2:
                a0, a1 = max(-1.2, a), min(1.2, a + .6)
                if a1 - a0 > .1:
                    bx(g, T, a0 + .01, a1 - .01, y, y + .25, z0 + .01, z1 - .01, f['sandbag' if (i + k + row) % 3 else 'sandbag-dark'], bevel=.07 if T.detail else 0, cell=0)
                a += .6
                k += 1
    if T.detail:
        ck.lettering(g, T, 'CD', -.6, .55, -.305, .03, f['cd-white'], style='stencil', depth=.001)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# The survivors' props on the pilot model (the DEV preview; Core NativeCampSurvivors): each modelled about where it is held or worn.
# A hand prop's origin is its grip, which sits in the binding's weapon frame (a sword's grip runs along its +z through the fist);
# NativeCampSurvivors.Hold turns and moves it into the fist. The headset's origin is the middle of the skull (y up, z the face).
# ---------------------------------------------------------------------------------------------------------------------------
def prop_mug(T):
    """An enamel camp mug, held round its body: its axis along the grip (z, its rim up the thumb's side, its foot at -.045), the fist
    closing on it, its handle out to the side (+x)."""
    f = F()
    g = Geo()
    sides = 12 if T.detail else 8
    sk.lathe(g, [(.032, -.045), (.034, .043), (.031, .045)], (0, 0, 0), f['cd-white'], sides, axis=(0, 0, 1), cap0=True)
    sk.lathe(g, [(.031, .041), (.0, .041)], (0, 0, 0), f['metal-dark'], sides, axis=(0, 0, 1))
    tube(g, [(.03, 0, .026), (.052, 0, .02), (.056, 0, 0), (.052, 0, -.02), (.03, 0, -.026)], .006, f['cd-white'], 5, caps=False)
    return g


def prop_bowl(T):
    """A turned wooden bowl with a spoon in it, sat on the palm: the bowl's axis along y."""
    f = F()
    g = Geo()
    sides = 14 if T.detail else 8
    sk.lathe(g, [(.03, 0.0), (.065, .02), (.078, .055), (.072, .058), (.058, .028), (.0, .022)], (0, 0, 0), f['timber'], sides, cap0=True)
    rod(g, (.0, .05, .0), (.04, .11, -.05), .005, f['metal-dark'], 4)
    return g


def prop_hammer(T):
    """A claw hammer held by its handle: the handle along z (the butt at -.09), the head across x at z .2."""
    f = F()
    g = Geo()
    rod(g, (0, 0, -.09), (0, 0, .2), .014, f['timber'], 6)
    bx(g, T, -.055, .045, -.016, .016, .185, .215, f['metal-dark'], bevel=.004, cell=0)
    rod(g, (.045, 0, .2), (.075, -.012, .19), .01, f['metal-dark'], 4)
    return g


def prop_headset(T):
    """The radio operator's headset: a band over the top of the head, a cup on each ear, the mic on a boom to the mouth."""
    f = F()
    g = Geo()
    band = [(-.104, -.005, 0), (-.095, .07, 0), (-.05, .12, 0), (0, .135, 0), (.05, .12, 0), (.095, .07, 0), (.104, -.005, 0)]
    tube(g, band, .008, f['metal-dark'], 4, caps=False)
    for sx in (-1, 1):
        sk.lathe(g, [(.0, .0), (.042, .0), (.044, .02), (.036, .03), (.0, .03)], (sx * .094, -.02, .005), f['metal-dark'], 10, axis=(sx, 0, 0), cap0=True)
    tube(g, [(-.115, -.025, .01), (-.1, -.07, .06), (-.06, -.095, .1), (-.025, -.1, .11)], .004, f['metal-dark'], 4, caps=False)
    rod(g, (-.03, -.1, .108), (-.015, -.1, .112), .009, f['metal-dark'], 6)
    return g
