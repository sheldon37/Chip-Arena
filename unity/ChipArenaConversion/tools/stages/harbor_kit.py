"""Pier 9's art: the finishes and the helpers its scripts share (harbor_art.py, harbor_props.py; docs/arenas/HARBOR.md,
docs/arenas/STAGE-ART.md). Unity space (x east, y up, z north), metres."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, lathe, sweep  # noqa: E402

F = {}
# the bulbs that chase: three groups of faces, lit by the rig's tint (rig/marquee-<g>), filled while a static scene is built
MARQUEE = [Geo(), Geo(), Geo()]
_count = [0]


def finishes():
    def add(name, col, detail=sk.DETAIL_NONE, **k):
        F[name] = sk.fin('pier-' + name, col, detail, **k)
    W, I, B, C, P = sk.DETAIL_WOOD, sk.DETAIL_IRON, sk.DETAIL_BRASS, sk.DETAIL_CLOTH, sk.DETAIL_PLASTER
    # the boards (grain along x), three shades, and what lies under them
    add('deck0', 0x8E6A4C, W, smooth=.3, param=0, wear=.25, dirt=.9)
    add('deck1', 0x7E5C40, W, smooth=.28, param=0, wear=.25, dirt=.9)
    add('deck2', 0x9A7656, W, smooth=.32, param=0, wear=.2, dirt=.9)
    add('gap', 0x140E0A, smooth=.05, dirt=.2)
    add('joist', 0x4A3626, W, smooth=.2, param=0, dirt=1.0)
    add('pile', 0x3A2A20, W, smooth=.25, param=1, dirt=1.0, moss=.6)
    add('weed', 0x1E2A22, smooth=.5, dirt=.5)
    # painted timber
    add('cream', 0xEFE4C8, W, smooth=.42, param=1, wear=.3, dirt=.55)
    add('creamx', 0xEFE4C8, W, smooth=.42, param=0, wear=.3, dirt=.55)
    add('red', 0xC0302C, W, smooth=.5, param=1, wear=.3, dirt=.5)
    add('teal', 0x1E8A92, W, smooth=.5, param=1, wear=.3, dirt=.5)
    add('navy', 0x1A2848, W, smooth=.5, param=1, wear=.3, dirt=.5)
    add('lilac', 0x9478CC, smooth=.5, wear=.2, dirt=.4)
    add('white', 0xF6F2E8, smooth=.5, wear=.2, dirt=.4)
    add('gold', 0xE2B040, B, smooth=.72, metal=.45, wear=.2, dirt=.4)
    add('golddim', 0xB8892E, B, smooth=.6, metal=.4, dirt=.6)
    add('crate', 0xA8804E, W, smooth=.25, param=0, wear=.2, dirt=1.0)
    add('cratedark', 0x6A4A2C, W, smooth=.25, param=1, dirt=1.0)
    add('green', 0x3E7048, W, smooth=.4, param=0, wear=.3, dirt=.8)
    add('soil', 0x241A14, smooth=.1, dirt=.4)
    add('leaf', 0x2E6A3A, smooth=.3, dirt=.3)
    # metal, glass, cloth, concrete
    add('iron', 0x24262E, I, smooth=.45, metal=.5, wear=.35, dirt=.8)
    add('ironred', 0xB42E2A, I, smooth=.5, metal=.3, wear=.35, dirt=.6)
    add('ironwhite', 0xE8E4DA, I, smooth=.5, metal=.3, wear=.35, dirt=.6)
    add('steel', 0x9AA2AA, I, smooth=.7, metal=.5, wear=.3, dirt=.5)
    add('brass', 0xC09848, B, smooth=.78, metal=.5, wear=.2, dirt=.4)
    add('mirror', 0xC8D8E6, B, smooth=.95, metal=.5, dirt=.2)
    add('glassdark', 0x1A2430, smooth=.9, dirt=.2)
    add('canvasred', 0xC83A34, C, smooth=.2, dirt=.5)
    add('canvas', 0xECE2CC, C, smooth=.2, dirt=.5)
    add('canvasteal', 0x1F8E96, C, smooth=.2, dirt=.5)
    add('concrete', 0x6E6E76, P, smooth=.2, dirt=1.0, moss=.5)
    add('rope', 0xB89868, C, smooth=.15, dirt=.8)
    add('black', 0x101014, smooth=.3, dirt=.2)
    add('hull', 0x7A2E2A, W, smooth=.45, param=2, wear=.3, dirt=.8)
    add('hullnavy', 0x1C2A4A, W, smooth=.45, param=2, wear=.3, dirt=.8)
    # light (the glow sub-mesh: albedo times the gain; the rig's tint on the chasing groups)
    add('glass', 0xFFD890, glow=1.5, dirt=0)
    add('glasspink', 0xFF8AC0, glow=1.5, dirt=0)
    add('glasscyan', 0x8AE8FF, glow=1.5, dirt=0)
    add('globe', 0xFFE2B0, glow=2.3, dirt=0)
    add('neonpink', 0xFF4FA8, glow=2.6, dirt=0)
    add('neoncyan', 0x4FE4FF, glow=2.6, dirt=0)
    add('neongold', 0xFFB84A, glow=2.6, dirt=0)
    add('lampred', 0xFF3A28, glow=2.4, dirt=0)
    add('bulbw', 0xFFD494, glow=2.6, dirt=0)
    add('bulbp', 0xFF6AB0, glow=2.6, dirt=0)
    add('bulbc', 0x6AE6FF, glow=2.6, dirt=0)
    add('white-glow', 0xFFFFFF, glow=1.0, dirt=0)


def E(b):
    """A layout block's box: x0, x1, y0, y1, z0, z1."""
    p, s = b['pos'], b['size']
    return (p[0] - s[0] / 2, p[0] + s[0] / 2, p[1] - s[1] / 2, p[1] + s[1] / 2, p[2] - s[2] / 2, p[2] + s[2] / 2)


def hashf(*k):
    h = 2166136261
    for v in k:
        h = (h ^ (int(v) & 0xFFFFFFFF)) * 16777619 & 0xFFFFFFFF
    h ^= h >> 15
    h = h * 2246822519 & 0xFFFFFFFF
    h ^= h >> 13
    return (h & 0xFFFFFF) / 16777216.0


def fo(g, pts, f, c):
    """A flat face turned to face away from the point c."""
    n = (0.0, 0.0, 0.0)
    for i in range(1, len(pts) - 1):
        n = sk._add(n, sk._cross(sk._sub(pts[i], pts[0]), sk._sub(pts[i + 1], pts[0])))
    if n[0] * n[0] + n[1] * n[1] + n[2] * n[2] < 1e-18:
        return
    m = [sum(p[k] for p in pts) / len(pts) - c[k] for k in range(3)]
    if n[0] * m[0] + n[1] * m[1] + n[2] * m[2] < 0:
        n = (-n[0], -n[1], -n[2])
    g.face(pts, f, normal=n)


def rod(g, p0, p1, r, f, n=6, caps=True, r1=None):
    sk.cylinder(g, p0, p1, r, f, n, caps=caps, r1=r1)


def ball(g, c, r, f, n=8, m=4, sy=1.0):
    prof = [(r * math.sin(math.pi * i / m), -r * sy * math.cos(math.pi * i / m)) for i in range(m + 1)]
    prof[0] = (1e-4, prof[0][1])
    prof[-1] = (1e-4, prof[-1][1])
    lathe(g, prof, c, f, n)


def bulb(x, y, z, k=None, r=.05, tier=None):
    """A chasing bulb (an octahedron) into the marquee group k % 3 (None: the next in turn), its colour by its turn."""
    if tier is not None and tier.i == 2 and _count[0] % 2 == 1 and k is None:
        _count[0] += 1
        return
    i = _count[0] if k is None else k
    _count[0] += 1
    g = MARQUEE[i % 3]
    f = F[('bulbw', 'bulbp', 'bulbw', 'bulbc')[i % 4]]
    top, bot = (x, y + r, z), (x, y - r, z)
    ring = [(x + r, y, z), (x, y, z + r), (x - r, y, z), (x, y, z - r)]
    for j in range(4):
        a, b = ring[j], ring[(j + 1) % 4]
        # corners shaded round (their normals from the centre), so they merge: six vertices a bulb
        na, nb = sk._norm(sk._sub(a, (x, y, z))), sk._norm(sk._sub(b, (x, y, z)))
        g.face((b, a, top), f, normals=(nb, na, (0, 1, 0)))
        g.face((a, b, bot), f, normals=(na, nb, (0, -1, 0)))


def bulbs(p0, p1, step, tier=None, k0=None):
    """A row of chasing bulbs from p0 to p1, about `step` apart."""
    d = math.dist(p0, p1)
    n = max(1, int(round(d / step)))
    for i in range(n + 1):
        t = i / n
        bulb(p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t, p0[2] + (p1[2] - p0[2]) * t, None if k0 is None else k0 + i, tier=tier)


def side(b, s):
    """Face s of a box (0 +x, 1 -x, 2 +z, 3 -z): its plane's coordinate, its outward sign, the axis it runs along and that
    axis's range."""
    x0, x1, y0, y1, z0, z1 = b
    return ((x1, 1, 'z', z0, z1), (x0, -1, 'z', z0, z1), (z1, 1, 'x', x0, x1), (z0, -1, 'x', x0, x1))[s]


def slab(g, b, s, u0, u1, v0, v1, t, f, T, o=0.0, bevel=.012, cell=None):
    """A slab on face s of box b: u0..u1 along the face, v0..v1 in height, t thick, its back o proud of the face."""
    if u1 - u0 < 1e-4 or v1 - v0 < 1e-4:
        return
    c, sg, ax, _, _ = side(b, s)
    if T is None or T.i > 0 or t < .04:
        bevel = 0
    a0, a1 = sorted((c + sg * o, c + sg * (o + t)))
    if ax == 'z':
        box(g, a0, a1, v0, v1, u0, u1, f, T, bevel, cell)
    else:
        box(g, u0, u1, v0, v1, a0, a1, f, T, bevel, cell)


def pt(b, s, u, v, o=0.0):
    """The point at u along and v up face s of box b, o proud of it."""
    c, sg, ax, _, _ = side(b, s)
    return (c + sg * o, v, u) if ax == 'z' else (u, v, c + sg * o)


def mould(g, b, s, u0, u1, y, prof, f, o=0.0):
    """A moulding along face s of box b at height y: prof [(up, out)] (a closed outline against the face), its ends closed."""
    pts = [[pt(b, s, u, y + up, o + out) for up, out in prof] for u in (u0, u1)]
    n = len(prof)
    c, sg, ax, _, _ = side(b, s)
    cu = sum(p[0] for p in prof) / n
    co = sum(p[1] for p in prof) / n
    for i in range(n - 1):
        mu, mo = (prof[i][0] + prof[i + 1][0]) / 2 - cu, (prof[i][1] + prof[i + 1][1]) / 2 - co
        du, do = prof[i + 1][0] - prof[i][0], prof[i + 1][1] - prof[i][1]
        nu, no = do, -du          # a normal of the segment in (up, out)
        if nu * mu + no * mo < 0:
            nu, no = -nu, -no
        nn = (sg * no, nu, 0) if ax == 'z' else (0, nu, sg * no)
        g.face((pts[0][i], pts[1][i], pts[1][i + 1], pts[0][i + 1]), f, normal=nn)
    # the end caps stand 8 mm inside the ends, so a cap never lies in a neighbouring wall's plane
    along = (0, 0, 1) if ax == 'z' else (1, 0, 0)
    for u, nn in ((u0 + .008, (-along[0], 0, -along[2])), (u1 - .008, along)):
        # (the outline is not convex: the cap in strips between the profile and the face)
        for i in range(n - 1):
            (ua, oa), (ub, ob) = prof[i], prof[i + 1]
            g.face((pt(b, s, u, y + ua, o + oa), pt(b, s, u, y + ub, o + ob), pt(b, s, u, y + ub, o), pt(b, s, u, y + ua, o)), f, normal=nn)


CORNICE = [(0, 0), (0, .12), (-.06, .14), (-.1, .1), (-.18, .09), (-.26, .04), (-.34, .03), (-.4, 0)]
PLINTH = [(0, 0), (0, .07), (.26, .07), (.31, .045), (.36, .03), (.4, 0)]
RAILCAP = [(0, -.04), (0, .04)]


def prism(g, poly, y0, y1, f, top=True, bottom=False, smooth=False, f_top=None):
    """A polygon [(x, z)] (convex, any winding) extruded from y0 to y1."""
    n = len(poly)
    cx = sum(p[0] for p in poly) / n
    cz = sum(p[1] for p in poly) / n
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        mx, mz = (a[0] + b[0]) / 2 - cx, (a[1] + b[1]) / 2 - cz
        ex, ez = b[0] - a[0], b[1] - a[1]
        nx, nz = ez, -ex
        if nx * mx + nz * mz < 0:
            nx, nz = -nx, -nz
        if smooth:
            na = sk._norm((a[0] - cx, 0, a[1] - cz))
            nb = sk._norm((b[0] - cx, 0, b[1] - cz))
            g.face(((a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], y1, b[1]), (a[0], y1, a[1])), f, normals=(na, nb, nb, na))
        else:
            g.face(((a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], y1, b[1]), (a[0], y1, a[1])), f, normal=(nx, 0, nz))
    if top:
        g.face([(p[0], y1, p[1]) for p in poly], f if f_top is None else f_top, normal=(0, 1, 0))
    if bottom:
        g.face([(p[0], y0, p[1]) for p in poly], f, normal=(0, -1, 0))


def ngon(cx, cz, r, n, phase=0.0, apothem=False):
    if apothem:
        r = r / math.cos(math.pi / n)
    return [(cx + r * math.sin(phase + 2 * math.pi * i / n), cz + r * math.cos(phase + 2 * math.pi * i / n)) for i in range(n)]


def cone(g, cx, y0, y1, cz, r0, r1, n, fins, phase=0.0, sag=0.0, steps=1):
    """A tent of n flat leaves from radius r0 at y0 to r1 at y1, the leaves' finishes in turn; sag bellies each leaf inward."""
    for i in range(n):
        a0, a1 = phase + 2 * math.pi * i / n, phase + 2 * math.pi * (i + 1) / n
        for k in range(steps):
            t0, t1 = k / steps, (k + 1) / steps

            def P(a, t):
                r = r0 + (r1 - r0) * t
                y = y0 + (y1 - y0) * t - sag * math.sin(math.pi * t)
                return (cx + r * math.sin(a), y, cz + r * math.cos(a))
            fo(g, (P(a0, t0), P(a1, t0), P(a1, t1), P(a0, t1)), fins[i % len(fins)], (cx, y0 - 4.0, cz))


def onion(profile_scale=1.0):
    """An onion dome's profile [(radius, height)] of unit base radius."""
    pts = [(1.0, 0.0), (1.12, .18), (1.2, .42), (1.16, .7), (1.0, .98), (.74, 1.24), (.46, 1.46), (.24, 1.66), (.1, 1.86), (.03, 2.1)]
    return [(r * profile_scale, h * profile_scale) for r, h in pts]


def catenary(p0, p1, sag, n):
    return [(p0[0] + (p1[0] - p0[0]) * i / n, p0[1] + (p1[1] - p0[1]) * i / n - sag * 4 * (i / n) * (1 - i / n), p0[2] + (p1[2] - p0[2]) * i / n) for i in range(n + 1)]
