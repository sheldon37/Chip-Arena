"""Top Dog Emporium's furniture (docs/arenas/EMPORIUM.md "Stage art"): the diner's booths (channel-backed vinyl benches, banded
tables with their sauce bottles), the tiled counters with their stools, roller grills and cake domes, the soda fountains, the
arched jukeboxes, the pump dispensers, the menu kiosks, fridges, prep tables, the bun bridges (a frankfurter in its bun to walk
on), the stairs and the sauce balconies with their columns and rails. Called by emporium_art.static_scene per map block; every
piece stays on its block (the stage rule: below 2 m the art keeps within .25 m of a collider)."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
import emporium_kit as ek  # noqa: E402
from stagekit import box, lathe  # noqa: E402
from emporium_kit import Face, ext, kmat, mat, pfin, kit_part, capsule, ellipsoid, loft, part_round, rrect, KINDS  # noqa: E402

BL = {}
RIG = {}
Y = (0.0, 1.0, 0.0)


def kn(p):
    return KINDS[p['k']]


def thin(p):
    return min(p['h']) <= .0125


def span(fc, p):
    c, h = p['c'], p['h']
    u = c[2] if fc.side < 2 else c[0]
    d = (c[0] - fc.o[0]) * fc.N[0] + (c[2] - fc.o[2]) * fc.N[2]
    hu, hn = (h[2], h[0]) if fc.side < 2 else (h[0], h[2])
    return u, c[1], hu, h[1], d + hn


def ring(g, c, w, d, r, y, rad, f, sides=5, n=3):
    sk.tube(g, [(c[0] + x, y, c[1] + z) for x, z in rrect(w, d, r, n)], rad, f, sides, closed=True)


def steel():
    return mat('stainless', 0xD8DEE6, smooth=.72, metal=.9, dirt=.2)


# ---------------------------------------------------------------------------------------------------------------------------
def grill(g, b, T):
    """The roller grill on a counter or a prep table (Core NativeEmporiumRig.Grill): a tray, the end housings and the rollers the
    rig's sausages turn on."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    along_x = b['size'][0] >= b['size'][2]
    length = min(3.0, (b['size'][0] if along_x else b['size'][2]) - .6)
    cross = min(.7, (b['size'][2] if along_x else b['size'][0]) - .25)
    n = int(math.floor(length / .26))
    cx, cz = b['pos'][0], b['pos'][2]
    chrome, dark = kmat('chrome'), kmat('dark')

    def P(u, y, w):
        return (cx + u, y, cz + w) if along_x else (cx + w, y, cz + u)

    def bx(u0, u1, ya, yb, w0, w1, f, bevel=.012):
        a, c = P(u0, ya, w0), P(u1, yb, w1)
        box(g, a[0], c[0], a[1], c[1], a[2], c[2], f, T, bevel, cell=0)
    bx(-length / 2 - .12, length / 2 + .12, y1, y1 + .045, -cross / 2 - .03, cross / 2 + .03, chrome)
    for s in (-1, 1):
        bx(s * (length / 2 + .02), s * (length / 2 + .16), y1 + .045, y1 + .2, -cross / 2 - .03, cross / 2 + .03, dark, .02)
        if T.i == 0:
            lathe(g, [(.03, 0), (.03, .012), (0, .016)], P(s * (length / 2 + .09), y1 + .2, 0), mat('grill-lamp', 0xFF5A2A, glow=1.4), 6)
    if T.i < 2:
        for k in range(n + 1):
            u = -length / 2 + k * length / n
            sk.cylinder(g, P(u, y1 + .083, -cross / 2), P(u, y1 + .083, cross / 2), .022, chrome, 5 if T.i == 0 else 4, caps=False)
    return y1


def prep(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, mat('prep', 0xD6DDE4, smooth=.72, metal=.9, dirt=.2), T, .03, cell=0, skip=('-y',))
    grill(g, b, T)
    for p in parts:
        if p['c'][1] > y1 + .01:
            continue                                        # the kit's grill housing: the modelled grill stands there
        kit_part(g, p, T, b)


def bridge(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    cx = (x0 + x1) / 2
    frank, dark = mat('frank', 0xC4502C, smooth=.6, dirt=.2), kmat('sausage-dark')
    # the frankfurter: a flat walk on top, a round belly under it
    k = (8, 5, 3)[T.i]
    g.face(((x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)), frank, Y) if T.i else sk.grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, frank, Y, 1.1)
    part_round(g, (cx, y1 - .02, z0), (cx, y1 - .02, z1), (x1 - x0) / 2, frank, (1, 0, 0), (0, -1, 0), 0, 180, k, .2)
    for z, n in ((z0, (0, 0, -1)), (z1, (0, 0, 1))):
        pts = [(cx + (x1 - x0) / 2 * math.cos(math.pi * i / k), y1 - .02 - .2 * math.sin(math.pi * i / k), z) for i in range(k + 1)]
        g.face(pts, dark, n)
    for s in (-1, 1):
        xe = x1 if s > 0 else x0
        # the bun's halves for kerbs
        capsule(g, (xe + s * .1, y1 - .13, z0 + .12), (xe + s * .1, y1 - .13, z1 - .12), .28, kmat('bun-top'), (9, 6, 4)[T.i], caps=2 if T.i == 0 else 1, squash=(.55, 1.0), up=(1, 0, 0), bulge=.04, body=3 if T.i == 0 else 1)
    bars = []
    for p in parts:
        name = kn(p)
        if name == 'sesame':
            if T.i == 0:
                s = 1 if p['c'][0] > cx else -1
                xe = x1 if s > 0 else x0
                ellipsoid(g, (xe + s * .1, y1 + .145, p['c'][2]), .03, .012, .055, pfin(p), 5, 2)
        elif name == 'mustard':
            bars.append(p)
    if bars:
        # the mustard: one sinuous ribbon down the deck
        bars.sort(key=lambda p: p['c'][2])
        pts = []
        for i, p in enumerate(bars):
            ax, half, _ = ek.long_axis(p)
            if ax[2] < 0:
                ax = (-ax[0], -ax[1], -ax[2])
            a = sk._add(p['c'], ax, -half)
            if i == 0:
                pts.append((a[0], y1 + .012, a[2]))
            e = sk._add(p['c'], ax, half)
            pts.append((e[0], y1 + .012, e[2]))
        path = sk.catmull(pts, (4, 2, 1)[T.i])
        sk.sweep(g, [(-.07, 0), (-.05, .02), (0, .03), (.05, .02), (.07, 0)], path, kmat('mustard'), up=(0, 1, 0), caps=False)


def flight(g, blocks, T, bun):
    """A flight of stairs on its step blocks (each a map block from the floor to its tread): tread slabs that overhang their
    risers and the open sides, with a chrome bullnose and a dark grip strip; a cut stringer whose stepped top follows the treads
    over a chrome bead, the side below it panelled (glazed tile on the balcony stairs, the bun with its sesame on the bridges');
    a tubular chrome handrail on each open side (thin, over the treads' ends: a pilot passes it as he passes a stool) and a
    wall rail on brackets where the flight runs along a wall."""
    blocks = sorted(blocks, key=lambda b: b['size'][1])
    along_x = blocks[0]['size'][0] < blocks[0]['size'][2]
    a, c = (0, 2) if along_x else (2, 0)
    up = 1 if blocks[-1]['pos'][a] > blocks[0]['pos'][a] else -1
    n = len(blocks)
    d, r = blocks[0]['size'][a], blocks[0]['size'][1]
    W = blocks[0]['size'][c]
    uo, wc = blocks[0]['pos'][a] - up * d / 2, blocks[0]['pos'][c]
    tops = [b['size'][1] for b in blocks]
    H = tops[-1]

    def P(u, y, w):
        return (uo + up * u, y, wc + w) if along_x else (wc + w, y, uo + up * u)

    def V(u, w, y=0.0):
        return (up * u, y, w) if along_x else (w, y, up * u)
    NU, NB = V(-1, 0), V(1, 0)
    # a side is open unless a wall stands along it
    opened = {}
    for s in (-1, 1):
        q = P(n * d / 2, 1.0, s * (W / 2 + .1))
        opened[s] = not any(i.startswith('emporium-wall-') and abs(q[0] - b['pos'][0]) <= b['size'][0] / 2 and abs(q[2] - b['pos'][2]) <= b['size'][2] / 2 for i, b in BL.items())
    cream, chrome, dark = kmat('cream'), kmat('chrome'), kmat('rubber')
    if bun:
        riser, string = kmat('mustard'), mat('flight-string-bun', 0xC4502C, smooth=.62, dirt=.25)
        panel = mat('flight-e9a85a', 0xE9A85A, sk.DETAIL_NONE, smooth=.4, dirt=.3, param=.2)
    else:
        riser, string = kmat('teal'), mat('flight-string', 0xFF6FAE, smooth=.7, dirt=.25)
        panel = mat('flight-23a59b', 0x23A59B, sk.DETAIL_TILE, smooth=.75, dirt=.3, param=.2)
    t, nose, ov, proud = .05, .035, .03, .018
    rb = t / 2
    S = 2 * r                                               # the stringer's depth under the line of the nosings

    def yb(u):
        return max(0.0, u / d * r + r - S)
    k = (4, 3, 2)[T.i]
    for i in range(n):
        y, yp = tops[i], (tops[i - 1] if i else 0.0)
        u0, u1 = i * d, (i + 1) * d
        ua = u0 - nose + rb
        w0 = -W / 2 - (ov if opened[-1] else 0)
        w1 = W / 2 + (ov if opened[1] else 0)
        # the tread: its top (a grip strip behind the nosing), the bullnose, the lip under it
        if T.i == 0:
            for (ca, cb, f) in ((ua, ua + .035, cream), (ua + .035, ua + .08, dark), (ua + .08, u1, cream)):
                g.face((P(ca, y, w0), P(cb, y, w0), P(cb, y, w1), P(ca, y, w1)), f, Y)
        else:
            g.face((P(ua, y, w0), P(u1, y, w0), P(u1, y, w1), P(ua, y, w1)), cream, Y)
        part_round(g, P(ua, y - rb, w0), P(ua, y - rb, w1), rb, chrome, NU, Y, -90, 90, k)
        if T.i < 2:
            g.face((P(ua, y - t, w0), P(u0, y - t, w0), P(u0, y - t, w1), P(ua, y - t, w1)), chrome, (0, -1, 0))
        g.face((P(u0, yp, -W / 2), P(u0, yp, W / 2), P(u0, y - t, W / 2), P(u0, y - t, -W / 2)), riser, NU)
        for s in (-1, 1):
            if not opened[s]:
                continue
            NS = V(0, s)
            we, wp, ws = s * (W / 2 + ov), s * W / 2, s * (W / 2 + proud)
            # the tread's end: a chrome edge, the bullnose's end, the overhang's underside
            g.face((P(ua, y - t, we), P(u1, y - t, we), P(u1, y, we), P(ua, y, we)), chrome, NS)
            arc = [P(ua - rb * math.cos(math.radians(-90 + 180 * j / k)), y - rb + rb * math.sin(math.radians(-90 + 180 * j / k)), we) for j in range(k + 1)]
            g.face(arc, chrome, NS)
            if T.i < 2:
                g.face((P(u0, y - t, wp), P(u1, y - t, wp), P(u1, y - t, we), P(u0, y - t, we)), chrome, (0, -1, 0))
            # the cut stringer under it (its top the tread, its foot the line of the bead), the step's edge, the panel below
            g.face((P(u0, yb(u0), ws), P(u1, yb(u1), ws), P(u1, y - t, ws), P(u0, y - t, ws)), string, NS)
            g.face((P(u0, max(yb(u0), yp - t), wp), P(u0, max(yb(u0), yp - t), ws), P(u0, y - t, ws), P(u0, y - t, wp)), string, NU)
            if yb(u1) > 0:
                pts = [P(u0, 0, wp), P(u1, 0, wp), P(u1, yb(u1), wp)] + ([P(u0, yb(u0), wp)] if yb(u0) > 0 else [])
                g.face(pts, panel, NS)
    # the back, under the landing
    for (ya, yc) in ((0.0, H / 2), (H / 2, H)):
        g.face((P(n * d, ya, -W / 2), P(n * d, ya, W / 2), P(n * d, yc, W / 2), P(n * d, yc, -W / 2)), panel, NB)
    # ... framed: a chrome surround, the stringer's colour within it, a rubber kick strip at the floor
    fy0, fy1 = .2, H - (.42 if bun else .5)
    fr = [P(n * d + .004, (fy0 + fy1) / 2 + q, w) for w, q in rrect(W - .36, fy1 - fy0, .16, (3, 2, 1)[T.i])]
    g.face(fr, string, NB)
    sk.tube(g, fr, .026, chrome, (5, 4, 3)[T.i], closed=True)
    if T.i < 2:
        g.face((P(n * d + .012, 0, -W / 2), P(n * d + .012, 0, W / 2), P(n * d + .012, .1, W / 2), P(n * d + .012, .1, -W / 2)), dark, NB)
        g.face((P(n * d, .1, -W / 2), P(n * d, .1, W / 2), P(n * d + .012, .1, W / 2), P(n * d + .012, .1, -W / 2)), dark, Y)
    slope = sk._norm(V(d, 0, r))
    hr = .78                                                # the handrail over the line of the nosings

    def line(u):
        return u / d * r + r + hr
    for s in (-1, 1):
        NS = V(0, s)
        wp, ws = s * W / 2, s * (W / 2 + proud)
        if opened[s]:
            # the stringer's back edge, the bead along its foot
            g.face((P(n * d, yb(n * d), wp), P(n * d, yb(n * d), ws), P(n * d, H - t, ws), P(n * d, H - t, wp)), string, NB)
            perp = sk._cross(NS, slope)
            if perp[1] > 0:
                perp = (-perp[0], -perp[1], -perp[2])
            part_round(g, P(d, 0, wp), P(n * d, yb(n * d), wp), .03, chrome, perp, NS, -90 if T.i < 2 else 0, 90 if T.i < 2 else 180, k + 1 if T.i < 2 else 2)
            if bun and T.i == 0:
                # the bun's sesame on the panel
                for j in range(3, n):
                    for q in range(1 + (j * 7 + (s > 0)) % 2):
                        hu, hy = ((j * 37 + q * 53 + (11 if s > 0 else 0)) % 100) / 100, ((j * 61 + q * 29 + (7 if s > 0 else 0)) % 100) / 100
                        u = (j + .15 + .7 * hu) * d
                        e = [.026, .026, .026]
                        e[c] = .01
                        e[a] = .05
                        ellipsoid(g, P(u, .12 + (yb(u) - .28) * hy, wp), e[0], e[1], e[2], kmat('sesame'), 5, 2)
            # the handrail: one chrome tube up from the first tread, along the flight and down to the last, on slender posts
            wr = s * (W / 2 - .05)
            ua, ub = .1, (n - 1) * d + .1
            sk.tube(g, [P(ua, tops[0] - .01, wr), P(ua, line(ua), wr), P(ub, line(ub), wr), P(ub, H - .01, wr)], .028, chrome, (5, 5, 4)[T.i], caps=False)
            m = max(1, int(round((n - 1) / 5.5)))
            posts = [int(round(j * (n - 1) / m)) for j in range(1, m)]
            for j in posts:
                u = j * d + .1
                sk.cylinder(g, P(u, tops[j] - .01, wr), P(u, line(u), wr), .016, chrome, 4, caps=False)
            if T.i == 0:
                for j in [0] + posts + [n - 1]:
                    lathe(g, [(.05, 0), (.05, .01), (.032, .022)], P(j * d + .1, tops[j], wr), chrome, 6)
        elif T.i < 2:
            # a wall rail on its brackets
            wr = s * (W / 2 - .075)
            ua, ub = .12, n * d - .12
            sk.tube(g, [P(ua, line(ua) - .05, wp), P(ua, line(ua), wr), P(ub, line(ub), wr), P(ub, line(ub) - .05, wp)], .024, chrome, (6, 4)[T.i], caps=False)
            for j in range(3, n - 1, 3):
                u = (j + .5) * d
                sk.cylinder(g, P(u, line(u) - .07, wp), P(u, line(u) - .01, wr), .012, chrome, 4, caps=False)


def balcony(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    front = 3 if b['pos'][2] > 0 else 2
    fc = Face(b, front)
    wood = mat('balcony-floor', 0xB98454, sk.DETAIL_WOOD, smooth=.5, dirt=.35, param=0, wear=.1)
    soffit = mat('soffit', 0xF3E9D6, smooth=.35, dirt=.3)
    cell = (1.5, 3.0, 6.0)[T.i]
    sk.grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, wood, Y, cell)
    sk.grid_face(g, (x0, y0, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, soffit, (0, -1, 0), cell)
    for s in (0, 1):
        f2 = Face(b, s)
        f2.quad(g, f2.u0, f2.u1, y0, y1, 0, kmat('teal'))
    fc.quad(g, fc.u0, fc.u1, y0 + .03, y1 - .04, .02, kmat('teal'), cell * 2)
    fc.bead(g, fc.u0, fc.u1, y1 - .02, .03, kmat('chrome'), .02, 3 if T.i == 0 else 2, .045)
    fc.bead(g, fc.u0, fc.u1, y0 + .02, .025, kmat('chrome'), .02, 3 if T.i == 0 else 2, .035)
    for p in parts:
        name = kn(p)
        if name == 'tube':
            u, v, hu, hv, d = span(fc, p)
            capsule(g, fc.P(u - hu, v, d + .03), fc.P(u + hu, v, d + .03), .032, pfin(p), (6, 4, 3)[T.i], caps=1)
            if T.i == 0:
                x = u - hu + .6
                while x < u + hu:
                    sk.cylinder(g, fc.P(x, v, .02), fc.P(x, v, d + .03), .012, kmat('chrome'), 3, caps=False)
                    x += 2.4
        elif name == 'lamp':
            c = p['c']
            ek.disc(g, (c[0], y0 - .012, c[2]), .2, pfin(p), (12, 8, 6)[T.i], (0, -1, 0))
            if T.i < 2:
                sk.tube(g, [(c[0] + .23 * math.cos(2 * math.pi * i / 10), y0 - .01, c[2] + .23 * math.sin(2 * math.pi * i / 10)) for i in range(10)], .025, kmat('chrome'), 4, closed=True)


def post(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    c = (b['pos'][0], 0, b['pos'][2])
    red, chrome = kmat('red'), kmat('chrome')
    lathe(g, [(.24, 0), (.24, .06), (.2, .1), (.2, .26), (.16, .3)], c, red, T.round)
    lathe(g, [(.14, .3), (.15, 1.2), (.14, 2.0)], c, mat('column', 0xF4EEE2, smooth=.6, dirt=.3), T.round)
    lathe(g, [(.14, 2.0), (.17, 2.02), (.17, 2.1), (.14, 2.12)], c, red, T.round)
    lathe(g, [(.14, 2.12), (.135, 3.0), (.15, 3.06), (.15, 3.1), (.22, 3.2), (.26, 3.26), (.26, y1)], c, chrome, T.round)
    if T.i == 0:
        for k in range(8):
            a = 2 * math.pi * k / 8
            part_round(g, (c[0] + .145 * math.cos(a), .32, c[2] + .145 * math.sin(a)), (c[0] + .14 * math.cos(a), 1.98, c[2] + .14 * math.sin(a)), .028, mat('column', 0xF4EEE2), (-math.sin(a), 0, math.cos(a)), (math.cos(a), 0, math.sin(a)), 0, 180, 2, .014)


def rail(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    cz = (z0 + z1) / 2
    chrome, teal = kmat('chrome'), mat('rail-panel', 0x1FA89C, smooth=.7, dirt=.25)
    capsule(g, (x0 + .04, y1 - .04, cz), (x1 - .04, y1 - .04, cz), .05, chrome, (8, 5, 4)[T.i], caps=1)
    sk.cylinder(g, (x0, y0 + .05, cz), (x1, y0 + .05, cz), .03, chrome, T.small)
    n = max(1, int(round((x1 - x0) / 1.2)))
    for k in range(n + 1):
        x = min(x1 - .035, max(x0 + .035, x0 + (x1 - x0) * k / n))
        sk.cylinder(g, (x, y0, cz), (x, y1 - .04, cz), .032, chrome, T.small, caps=False)
        if k < n:
            xa, xb = x0 + (x1 - x0) * k / n + .06, x0 + (x1 - x0) * (k + 1) / n - .06
            box(g, xa, xb, y0 + .12, y1 - .13, cz - .02, cz + .02, teal, T, .012, cell=0)
            if T.i == 0:
                for zz, nn in ((cz - .021, (0, 0, -1)), (cz + .021, (0, 0, 1))):
                    sk.tube(g, [((xa + xb) / 2 + .2 * math.cos(2 * math.pi * i / 12), (y0 + y1) / 2 + .2 * math.sin(2 * math.pi * i / 12), zz) for i in range(12)], .014, chrome, 3, closed=True)


def bench(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    i = b['id']
    toward = 3 if i.endswith('-back-n') else 2 if i.endswith('-back-s') else (3 if b['pos'][2] > 0 else 2)
    fc = Face(b, toward)
    vinyl, seat = mat('vinyl', 0xC8202E, smooth=.68, dirt=.25), mat('vinyl-seat', 0xE0303E, smooth=.68, dirt=.25)
    chrome, white = kmat('chrome'), mat('piping', 0xFFF7EA, smooth=.55, dirt=.2)
    box(g, x0 + .02, x1 - .02, y0, y0 + .3, z0 + .02, z1 - .02, chrome, T, .015, cell=0, skip=('-y',))
    box(g, x0, x1, y0 + .3, y1 - .06, z0, z1, vinyl, T, .04, cell=0)
    # the seat cushion and the rolled top
    a, c = fc.P(fc.u0 + .02, y0 + .3, -.02), fc.P(fc.u1 - .02, y0 + .47, .23)
    box(g, a[0], c[0], a[1], c[1], a[2], c[2], seat, T, .05, cell=0)
    mid = fc.P(0, 0, -(z1 - z0) / 2)
    capsule(g, (x0 + .08, y1 - .07, mid[2]), (x1 - .08, y1 - .07, mid[2]), .085, white, (8, 6, 4)[T.i], caps=1, squash=(((z1 - z0) / 2 - .02) / .085, 1.0), up=(0, 1, 0))
    if T.i < 2:
        # the channel back: upright rolls of vinyl
        n = max(2, int(round((fc.u1 - fc.u0 - .1) / .29)))
        w = (fc.u1 - fc.u0 - .1) / n
        for k in range(n):
            u = fc.u0 + .05 + (k + .5) * w
            fc.stile(g, u, y0 + .5, y1 - .1, w / 2, vinyl, 0, 4 if T.i == 0 else 2, .05)
            if T.i == 0:
                ellipsoid(g, fc.P(u, y1 - .1, 0), w / 2, .04, .05, vinyl, 6, 2, 0, 1)


def table(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    c = (b['pos'][0], b['pos'][2])
    w, d = b['size'][0], b['size'][2]
    n = (3, 2, 1)[T.i]
    top = mat('table-top', 0xFFF6E8, smooth=.75, dirt=.2)
    loft(g, c, [(y1 - .07, w, d, .14), (y1, w, d, .14)], top, n, cap0=True, cap1=True)
    ring(g, c, w + .01, d + .01, .14, y1 - .035, .04, kmat('chrome'), T.small, n)
    loft(g, c, [(0, w - .16, d - .16, .08), (y1 - .07, w - .16, d - .16, .08)], kmat('dark'), n, cap1=False)
    loft(g, c, [(0, w - .08, d - .08, .1), (.06, w - .1, d - .1, .1)], kmat('chrome'), n, cap1=True)
    if T.i == 2:
        return
    for p in parts:
        name = kn(p)
        if p['c'][1] < y1:
            continue
        if name in ('ketchup', 'mustard'):
            pc = p['c']
            lathe(g, [(.045, 0), (.05, .015), (.05, .13), (.03, .19), (.025, .2)], (pc[0], y1, pc[2]), pfin(p), max(6, T.small))
            lathe(g, [(.028, .2), (.012, .26), (0, .265)], (pc[0], y1, pc[2]), kmat('white'), max(5, T.small))
        elif name == 'chrome':
            kit_part(g, p, T, b)


def counter(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    front = 3 if b['pos'][2] > 0 else 2
    fc = Face(b, front)
    c = (b['pos'][0], b['pos'][2])
    w, d = b['size'][0], b['size'][2]
    chrome, mint = kmat('chrome'), kmat('mint')
    box(g, x0, x1, y0, y1 - .06, z0, z1, mint, T, .03, cell=(1.2, 3, 6)[T.i], skip=('-y',))
    fc.quad(g, fc.u0 + .02, fc.u1 - .02, 0, .14, .006, kmat('dark'))
    n = (3, 2, 1)[T.i]
    loft(g, c, [(y1 - .06, w + .08, d + .08, .1), (y1, w + .08, d + .08, .1)], mat('counter-top', 0xFF9EC4, smooth=.8, dirt=.2), n, cap0=True, cap1=True)
    ring(g, c, w + .09, d + .09, .1, y1 - .03, .034, chrome, T.small, n)
    v = .32
    while v < y1 - .2:
        fc.bead(g, fc.u0 + .03, fc.u1 - .03, v + .017, .017, chrome, 0, 3 if T.i == 0 else 2)
        v += .22
    grill(g, b, T)
    for p in parts:
        name = kn(p)
        pc, h = p['c'], p['h']
        if p['f'] != 'counter':
            kit_part(g, p, T, b)
            continue
        if pc[1] < y1 - .001:
            if name == 'red' and abs(h[1] - .05) < .01:
                # a stool: a chrome pedestal, a foot ring, a round red cushion
                base = (pc[0] + fc.N[0] * .07, 0, pc[2] + fc.N[2] * .07)
                pc = [base[0], pc[1], base[2]]
                lathe(g, [(.17, 0), (.17, .02), (.05, .05), (.04, .3), (.04, .68), (.12, .71)], base, chrome, T.round)
                lathe(g, [(.13, .71), (.19, .72), (.2, .76), (.19, .8), (.1, .825), (0, .83)], base, pfin(p), T.round)
                if T.i == 0:
                    sk.tube(g, [(pc[0] + .13 * math.cos(2 * math.pi * i / 10), .3, pc[2] + .13 * math.sin(2 * math.pi * i / 10)) for i in range(10)], .012, chrome, 4, closed=True)
                    for a in (0, 2.1, 4.2):
                        sk.cylinder(g, (pc[0] + .04 * math.cos(a), .3, pc[2] + .04 * math.sin(a)), (pc[0] + .13 * math.cos(a), .3, pc[2] + .13 * math.sin(a)), .01, chrome, 3, caps=False)
            continue                                        # the fronts' bands and the stools' boxes are modelled above
        if name == 'chrome' and abs(h[1] - .05) < .005 and max(h[0], h[2]) > 1:
            continue                                        # the grill's box
        if name == 'dark' and h[1] < .02:
            continue
        if name == 'glass' and h[1] < .01:
            # the sneeze guard: a pane on four posts
            pane = mat('pane', 0xBFE8FF, smooth=.95, metal=.15, dirt=.1)
            box(g, pc[0] - h[0], pc[0] + h[0], pc[1] - .006, pc[1] + .006, pc[2] - h[2], pc[2] + h[2], pane, T, .004, cell=0)
            if T.i < 2:
                for sx in (-1, 1):
                    for sz in (-1, 1):
                        sk.cylinder(g, (pc[0] + sx * (h[0] - .05), y1 + .04, pc[2] + sz * (h[2] - .04)), (pc[0] + sx * (h[0] - .05), pc[1], pc[2] + sz * (h[2] - .04)), .012, chrome, 4, caps=False)
            continue
        if name == 'glass':
            ellipsoid(g, (pc[0], pc[1] - h[1], pc[2]), h[0], 2 * h[1] + .03, h[2], mat('cake-dome', 0xFFD8E8, smooth=.92, dirt=.1), (10, 7, 5)[T.i], 4, 0, 1)
            lathe(g, [(.02, 0), (.03, .02), (0, .035)], (pc[0], pc[1] + h[1] + .03, pc[2]), chrome, 6)
            continue
        kit_part(g, p, T, b)


def soda(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    front = 3 if b['pos'][2] > 0 else 2
    fc = Face(b, front)
    c = (b['pos'][0], b['pos'][2])
    w, d = b['size'][0], b['size'][2]
    n = (3, 2, 1)[T.i]
    loft(g, c, [(0, w, d, .16), (2.42, w, d, .16)], steel(), n, cap1=False)
    loft(g, c, [(2.42, w, d, .16), (2.46, w + .06, d + .06, .18), (2.56, w + .06, d + .06, .18), (2.6, w, d, .16)], kmat('red'), n, cap1=True)
    loft(g, c, [(0, w + .02, d + .02, .16), (.2, w + .02, d + .02, .16)], kmat('dark'), n, cap1=False)
    chrome = kmat('chrome')
    for p in parts:
        name = kn(p)
        u, v, hu, hv, dd = span(fc, p)
        if p['f'] != 'soda':
            kit_part(g, p, T, b)
            continue
        if name in ('dark', 'red') and (v < .25 or v > 2.4):
            continue
        if name == 'screen':
            kit_part(g, p, T, b)
            fc.frame(g, u - hu, u + hu, v - hv, v + hv, dd, .035, chrome, T.small, .1, 2)
        elif name == 'chrome' and hu < .08:
            # a tap: its spout and, on it, the lever
            capsule(g, fc.P(u, v + hv - .04, 0), fc.P(u, v - hv + .03, .14), .028, chrome, max(5, T.small), caps=1)
            sk.cylinder(g, fc.P(u, v - hv + .03, .14), fc.P(u, v - hv - .03, .14), .02, chrome, max(5, T.small))
        elif name == 'red' and hu < .08:
            capsule(g, fc.P(u, v - hv + .02, .12), fc.P(u, v + hv, .17), .036, pfin(p), max(5, T.small), caps=1, r1=.046)
        elif name == 'dark' and hv < .06:
            kit_part(g, p, T, b)
            if T.i == 0:
                k = int(2 * hu / .09)
                for i in range(k):
                    x = u - hu + (i + .5) * 2 * hu / k
                    sk.cylinder(g, fc.P(x, v + hv + .004, dd - .23), fc.P(x, v + hv + .004, dd - .01), .008, chrome, 3, caps=False)
        else:
            kit_part(g, p, T, b)


def jukebox(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    front = 3 if b['pos'][2] > 0 else 2
    fc = Face(b, front)
    depth = b['size'][2]
    body = mat('jukebox', 0xD8202E, smooth=.72, dirt=.25)
    chrome = kmat('chrome')
    k = (12, 8, 4)[T.i]
    hw = (fc.u1 - fc.u0) / 2
    spring, rise = y1 - .42, .42
    outline = [(fc.u0, 0.0), (fc.u1, 0.0)] + [(fc.uc + hw * math.cos(math.pi * i / k), spring + rise * math.sin(math.pi * i / k)) for i in range(k + 1)]
    rf, rb = [fc.P(u, v, 0) for u, v in outline], [fc.P(u, v, -depth) for u, v in outline]
    ek.skin(g, [rb, rf], body)
    g.face(rf, body, fc.N)
    arc = [(fc.uc + (hw - .02) * math.cos(math.pi * i / k), spring + (rise - .02) * math.sin(math.pi * i / k), .02) for i in range(k + 1)]
    sk.tube(g, fc.path([(fc.u1 - .02, .06, .02)] + arc + [(fc.u0 + .02, .06, .02)]), .05, chrome, T.small, caps=True)
    if T.i < 2:
        inner = [(fc.uc + (hw - .3) * math.cos(math.pi * i / k), spring - .2 + (rise - .1) * math.sin(math.pi * i / k), .012) for i in range(k + 1)]
        sk.tube(g, fc.path(inner), .025, mat('juke-gold', 0xF6C84A, smooth=.8, metal=1.0, dirt=.15), T.small, caps=True)
        for s in (-1, 1):
            u = fc.uc + s * (hw - .12)
            for v in (.38, 2.32):
                lathe(g, [(.075, -.03), (.08, 0), (.075, .03)], fc.P(u, v, .03), chrome, 8, cap0=True, cap1=True)
    # the cabinet's sides: a speaker grille (cloth behind chrome louvres in a chrome surround); its back: a service panel
    cloth, steel_dark = mat('juke-cloth', 0xE9C27A, smooth=.25, dirt=.4), kmat('dark')
    for fs in (Face(b, 0), Face(b, 1)):
        ua, ub, va, vb = fs.u0 + .2, fs.u1 - .2, .36, spring - .2
        fs.quad(g, ua, ub, va, vb, .004, cloth)
        fs.frame(g, ua, ub, va, vb, .004, .03, chrome, T.small, .14, (3, 2, 1)[T.i])
        if T.i < 2:
            m = 7 if T.i == 0 else 4
            for j in range(1, m):
                fs.bead(g, ua + .03, ub - .03, va + (vb - va) * j / m, .02, chrome, .004, 3 if T.i == 0 else 2, .03)
        fs.bead(g, fs.u0 + .06, fs.u1 - .06, spring + .02, .03, chrome, 0, 3 if T.i == 0 else 2, .035)
        fs.bead(g, fs.u0 + .06, fs.u1 - .06, .14, .03, chrome, 0, 3 if T.i == 0 else 2, .035)
    fk = Face(b, 2 if front == 3 else 3)
    g.face([fk.P(u, v, 0) for u, v in outline], body, fk.N)
    fk.quad(g, fk.u0 + .22, fk.u1 - .22, .3, spring - .3, .004, steel_dark)
    fk.frame(g, fk.u0 + .22, fk.u1 - .22, .3, spring - .3, .004, .022, chrome, T.small, .08, (3, 2, 1)[T.i])
    if T.i < 2:
        for j in range(1, 5):
            fk.bead(g, fk.u0 + .36, fk.u1 - .36, spring - .42 - .1 * j, .016, chrome, .004, 2, .025)
    for p in parts:
        name = kn(p)
        u, v, hu, hv, dd = span(fc, p)
        if p['f'] == 'jukebox' and name == 'chrome' and hv > .1:
            continue                                        # the flat top band: the arch stands there
        if p['f'] == 'jukebox' and name == 'chrome':
            fc.bead(g, u - hu, u + hu, v, hv, chrome, .012, 3 if T.i == 0 else 2)
            continue
        if p['f'] == 'jukebox' and name == 'glass':
            kit_part(g, p, T, b)
            fc.frame(g, u - hu, u + hu, v - hv, v + hv, dd, .03, chrome, T.small, .12, 3)
            continue
        kit_part(g, p, T, b)


def pump(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    c = (b['pos'][0], b['pos'][2])
    w = b['size'][0]
    col = 0xE3241C if c[0] < 0 else 0xFFC81E
    sauce = mat('bottle-%06x' % col, col, smooth=.78, dirt=.15)
    n = (4, 2, 1)[T.i]
    r = .22
    loft(g, c, [(0, w - .04, w - .04, r), (.04, w, w, r), (2.62, w, w, r), (2.86, w - .16, w - .16, r + .06), (y1, w - .5, w - .5, .28)], sauce, n, cap1=True)
    loft(g, c, [(1.3, w + .016, w + .016, r), (2.25, w + .016, w + .016, r)], mat('label', 0xFFFFFF, smooth=.5, dirt=.25), n, cap1=False)
    lathe(g, [(.3, y1), (.3, y1 + .03), (.2, y1 + .06), (.16, y1 + .06)], (c[0], 0, c[1]), kmat('white'), T.round, cap1=True)
    for p in parts:
        name = kn(p)
        if p['f'] == 'pump' and name in ('ketchup', 'mustard', 'white'):
            continue
        kit_part(g, p, T, b)


def kiosk(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    c = (b['pos'][0], b['pos'][2])
    n = (3, 2, 1)[T.i]
    loft(g, c, [(y0, b['size'][0], b['size'][2], .1), (y1 - .08, b['size'][0], b['size'][2], .1), (y1, b['size'][0] - .1, b['size'][2] - .1, .08)], mat('kiosk', 0x2A2030, smooth=.55, dirt=.3), n, cap1=True)
    chrome = kmat('chrome')
    for s in (2, 3):
        fc = Face(b, s)
        fc.frame(g, fc.u0 + .08, fc.u1 - .08, y0 + .36, y1 - .08, .01, .03, chrome, T.small, .12, 2)
    for p in parts:
        kit_part(g, p, T, b)


def fridge(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, mat('fridge', 0xF4F8FA, smooth=.74, dirt=.25), T, .09, cell=(1.2, 3, 6)[T.i], skip=('-y',))
    chrome = kmat('chrome')
    for p in parts:
        name = kn(p)
        if p['f'] == 'fridge' and name == 'chrome':
            for s in range(4):
                fc = Face(b, s)
                if sk._dot(sk._sub(p['c'], b['pos']), fc.N) > 0 and abs(span(fc, p)[4] - .1) < .03:
                    u, v, hu, hv, dd = span(fc, p)
                    capsule(g, fc.P(u, v - hv, .08), fc.P(u, v + hv, .08), .028, chrome, max(5, T.small), caps=1)
                    for vv in (v - hv + .06, v + hv - .06):
                        sk.cylinder(g, fc.P(u, vv, 0), fc.P(u, vv, .08), .016, chrome, 4, caps=False)
            continue
        kit_part(g, p, T, b)


def generic(g, b, parts, T):
    ek.body(g, b, mat('block-metal', 0xDDE3EA, smooth=.7, metal=.8, dirt=.25), T, .04)
    for p in parts:
        kit_part(g, p, T, b)


def block(g, b, parts, T, L):
    i = b['id']
    if '-prep-' in i:
        prep(g, b, parts, T)
    elif i.startswith('emporium-bridge-'):
        bridge(g, b, parts, T)
    elif i.startswith('emporium-balcony-') and '-post-' in i:
        post(g, b, parts, T)
    elif i.startswith('emporium-rail-'):
        rail(g, b, parts, T)
    elif i.startswith('emporium-balcony-'):
        balcony(g, b, parts, T)
    elif '-booth-' in i and i.endswith('-table'):
        table(g, b, parts, T)
    elif '-booth-' in i:
        bench(g, b, parts, T)
    elif '-counter-' in i:
        counter(g, b, parts, T)
    elif '-soda-' in i:
        soda(g, b, parts, T)
    elif '-jukebox-' in i:
        jukebox(g, b, parts, T)
    elif '-pump-' in i:
        pump(g, b, parts, T)
    elif '-kiosk-' in i:
        kiosk(g, b, parts, T)
    elif '-freezer-' in i or '-fridge-' in i:
        fridge(g, b, parts, T)
    else:
        generic(g, b, parts, T)
