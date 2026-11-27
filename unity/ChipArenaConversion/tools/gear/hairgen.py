"""Generated anime hair for the hair bank (tools/gear/hair.py): locks swept along curves, each its own cloth chain.

A lock is a flattened, tapering blade: rings of six points round its centre line (wide across the scalp, thin off it), narrowing
to a point at the tip, its texture coordinate v running root to tip and u round it. Its root is buried a few millimetres in the
cap under it (so no seam shows), and every ring is kept a margin off the head's own surface (a bang never cuts the cheek).

The styles (owner 2026-11-26: "some anime ones as well (Think like sasuke from naruto)"; an original design in that spirit, not a
copy of any character):
- raven spikes: a short cap (the pack's ShotaCut) with a crown of long spikes that flare up and back from the top and back of the
  head in three layers, short spikes over the ears and the nape, two long bangs framing the face to the jaw and three shorter
  ones over the brow, one swept aside.
- hero spikes: the same cap under big spikes that stand up and out all round the crown (the tallest in the middle, each ring
  lower and wider), short side spikes, and three broad spikes falling over the brow.
"""
import io
import math

import numpy as np

# The colour weights the toon reads (red the hair's first colour, green its second, the pack's dark and its blue): root mostly the
# first, the tip lighter, a sheen band a quarter of the way down the lock's face.
def gradient_png(w=32, h=128):
    from PIL import Image
    img = np.zeros((h, w, 4), np.uint8)
    for y in range(h):
        v = 1 - y / (h - 1)          # image top = v 1 (the tip)
        g = .18 + .42 * max(0., (v - .5) / .5) ** 1.5
        for x in range(w):
            u = x / (w - 1)
            band = math.exp(-((v - .24) / .05) ** 2) * max(0., 1 - abs(u - .5) * 2.4)
            gg = min(1., g + .38 * band)
            img[y, x] = (int(255 * (1 - gg)), int(255 * gg), 0, 255)
    buf = io.BytesIO()
    Image.fromarray(img, 'RGBA').save(buf, 'PNG', optimize=True)
    return buf.getvalue()


def bezier(p0, p1, p2, p3, n):
    t = np.linspace(0, 1, n)[:, None]
    return (1 - t) ** 3 * p0 + 3 * (1 - t) ** 2 * t * p1 + 3 * (1 - t) * t ** 2 * p2 + t ** 3 * p3


def unit(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-9 else v


class Lock:
    def __init__(self, points, width, thick, side, joints, kind, taper=.9, curl=0.):
        self.points = points          # centre line, root first
        self.width = width            # at the root (m), across the scalp
        self.thick = thick            # at the root (m), off the scalp
        self.side = side              # the across direction at the root
        self.joints = joints          # chain joints (0: a rigid lock on the head)
        self.kind = kind
        self.taper = taper
        self.curl = curl


def lock_mesh(lock, rings=10):
    pts = lock.points
    # resample the centre line evenly by arc length
    seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    L = s[-1]
    ts = np.linspace(0, L, rings + 1)
    c = np.stack([np.interp(ts, s, pts[:, k]) for k in range(3)], 1)
    verts, uvs, tris = [], [], []
    side0 = unit(lock.side)
    for i in range(rings):
        t = i / rings
        tan = unit(c[min(i + 1, rings)] - c[max(i - 1, 0)])
        side = unit(side0 - tan * np.dot(side0, tan))
        up = unit(np.cross(tan, side))
        # a slight twist toward the tip (curl, radians)
        a = lock.curl * t
        side, up = side * math.cos(a) + up * math.sin(a), up * math.cos(a) - side * math.sin(a)
        w = lock.width * (1 - t) ** lock.taper * (1 + .18 * math.sin(math.pi * t) ** 2) / 2
        h = lock.thick * (1 - t) ** lock.taper / 2
        ring = [side * w, side * w * .45 + up * h, -side * w * .45 + up * h, -side * w, -side * w * .45 - up * h, side * w * .45 - up * h]
        for k, o in enumerate(ring):
            verts.append(c[i] + o)
            uvs.append((k / 6, t))
    tip = len(verts)
    verts.append(c[rings])
    uvs.append((.5, 1.))
    for i in range(rings - 1):
        for k in range(6):
            a, b = i * 6 + k, i * 6 + (k + 1) % 6
            a2, b2 = a + 6, b + 6
            tris += [(a, a2, b), (b, a2, b2)]
    last = (rings - 1) * 6
    for k in range(6):
        tris.append((last + k, tip, last + (k + 1) % 6))
    # the root closed (buried, but the outline's hull wants a closed blade)
    centre = len(verts)
    verts.append(c[0] - unit(c[1] - c[0]) * .002)
    uvs.append((.5, 0.))
    for k in range(6):
        tris.append((k, (k + 1) % 6, centre))
    return np.array(verts), np.array(uvs), np.array(tris, np.int64), c, ts / L


def normals(v, tris):
    n = np.zeros_like(v)
    fn = np.cross(v[tris[:, 1]] - v[tris[:, 0]], v[tris[:, 2]] - v[tris[:, 0]])
    for k in range(3):
        np.add.at(n, tris[:, k], fn)
    return n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-9)


def push_off(points, head, margin):
    """Each point at least `margin` off the head's surface (along the ray from the head's centre)."""
    out = points.copy()
    for i, p in enumerate(points):
        off = head.offset(p[None])[0]
        if off < margin:
            d = unit(p - head.c)
            out[i] = p + d * (margin - off)
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# Directions on the head (head frame: y up, z forward, x across; Unity's left-handed axes: +x is the pilot's left)
# ---------------------------------------------------------------------------------------------------------------------------
def scalp_point(cap_v, head, azimuth, elevation, inset=.004):
    """The cap's surface in the direction (azimuth from the back, degrees, + toward +x; elevation above the head centre's level),
    `inset` into it."""
    a, e = math.radians(azimuth), math.radians(elevation)
    d = np.array([math.sin(a) * math.cos(e), math.sin(e), -math.cos(a) * math.cos(e)])
    rel = cap_v - head.c
    r = np.linalg.norm(rel, axis=1)
    cosang = rel @ d / np.maximum(r, 1e-9)
    near = cosang > math.cos(math.radians(9))
    if not near.any():
        near = cosang > np.sort(cosang)[-12]
    far = float(np.percentile(r[near] * cosang[near], 92))
    return head.c + d * (far - inset), d


def raven_back(head, cap_v):
    """The crown: broad spikes that leave the scalp along it and flare up and back like a bird's tail, in overlapping layers
    (the top ones longest and highest, the low ones back and level), short ones over the ears and the nape."""
    rnd = np.random.RandomState(11)
    locks = []
    up = np.array([0, 1., 0])

    def spike(az, el, length, width, rise, bend, joints, kind='S', thick=.45, curl=0., taper=1.15):
        root, radial = scalp_point(cap_v, head, az, el, inset=.006)
        a = math.radians(az)
        # its heading: back (away from the face) turned out a little by its side of the head, raised by `rise` (radians)
        out = np.array([math.sin(a), 0, -math.cos(a)])
        head_dir = unit(np.array([0, 0, -1.]) * .75 + out * .45)
        d = unit(head_dir * math.cos(rise) + up * math.sin(rise))
        along = unit(d - radial * np.dot(d, radial))            # along the scalp first
        side = unit(np.cross(radial, along))
        p0 = root
        p1 = root + along * length * .33 + radial * .006
        tipdir = unit(d + up * bend)
        p3 = root + along * length * .45 + d * length * .35 + tipdir * length * .28
        p2 = p3 - tipdir * length * .32
        pts = bezier(p0, p1, p2, p3, 26)
        pts[2:] = push_off(pts[2:], head, .010)
        locks.append(Lock(pts, width, width * thick, side, joints, kind, taper=taper, curl=curl))

    # the top layer: long, high, up and back
    for az, el in ((-46, 60), (-16, 66), (16, 66), (46, 60)):
        spike(az + rnd.uniform(-3, 3), el, rnd.uniform(.165, .185), rnd.uniform(.058, .064), math.radians(rnd.uniform(36, 44)), .3, 3, curl=rnd.uniform(-.2, .2), taper=1.3)
    # the second: a little lower and flatter, between the first's
    for az, el in ((-64, 44), (-31, 46), (0, 50), (31, 46), (64, 44)):
        spike(az + rnd.uniform(-3, 3), el, rnd.uniform(.145, .16), rnd.uniform(.054, .06), math.radians(rnd.uniform(24, 32)), .22, 3, curl=rnd.uniform(-.25, .25), taper=1.3)
    # the third: back and level
    for az, el in ((-56, 24), (-20, 28), (20, 28), (56, 24)):
        spike(az + rnd.uniform(-4, 4), el, rnd.uniform(.115, .13), rnd.uniform(.05, .055), math.radians(rnd.uniform(8, 14)), .14, 3, curl=rnd.uniform(-.3, .3), taper=1.25)
    # the low back: short, back and a little down
    for az, el in ((-36, 4), (0, 8), (36, 4)):
        spike(az, el, rnd.uniform(.075, .085), .046, math.radians(-12), .05, 2)
    # the nape
    for az in (-22, 22):
        spike(az, -14, .06, .04, math.radians(-38), .0, 2)
    # over the ears: back and down
    for sgn in (-1, 1):
        spike(sgn * 96, 22, .085, .05, math.radians(-14), .05, 2)
        spike(sgn * 112, 4, .07, .045, math.radians(-30), .0, 2)
    # the top front: swept back flat over the crown (rigid, they lie on the head)
    for az, el in ((-158, 66), (158, 66), (180, 74), (-176, 56), (176, 56)):
        spike(az, el, rnd.uniform(.07, .085), .05, math.radians(10), -.05, 0, thick=.4)
    return locks


def raven_front(head, cap_v):
    """Two long bangs framing the face to the jaw, three shorter over the brow, one swept aside."""
    locks = []
    up = np.array([0, 1., 0])
    fwd = np.array([0, 0, 1.])
    for sgn in (-1, 1):
        # the long framing bang: from the temple's top, down in front of the ear to the jaw, curving in at the end
        root, radial = scalp_point(cap_v, head, sgn * 128, 40)
        side = unit(np.cross(up, radial)) * sgn
        p0 = root
        p1 = root + fwd * .012 + up * -.04 + radial * .012
        p2 = np.array([sgn * .095, -.0, .085])
        p3 = np.array([sgn * .072, -.055, .10])
        pts = push_off(bezier(p0, p1, p2, p3, 28), head, .014)
        locks.append(Lock(pts, .036, .014, np.array([0, 0, 1.]), 4, 'L', taper=.75, curl=.35 * sgn))
        # a second, shorter one behind it
        root, radial = scalp_point(cap_v, head, sgn * 115, 30)
        p0 = root
        p1 = root + up * -.03 + radial * .015
        p2 = np.array([sgn * .108, .02, .055])
        p3 = np.array([sgn * .098, -.02, .07])
        pts = push_off(bezier(p0, p1, p2, p3, 24), head, .016)
        locks.append(Lock(pts, .03, .013, np.array([0, 0, 1.]), 3, 'L', taper=.8, curl=.2 * sgn))
    # over the brow: three shorter bangs, the middle one swept aside
    for az, x_end, y_end, length_w in ((160, .03, .085, .03), (178, -.012, .07, .032), (-162, -.045, .088, .028)):
        root, radial = scalp_point(cap_v, head, az, 52)
        p0 = root
        p1 = root + fwd * .03 + up * .005
        p3 = np.array([x_end, y_end, .135])
        p2 = p3 + np.array([0, .03, -.005])
        pts = push_off(bezier(p0, p1, p2, p3, 22), head, .012)
        locks.append(Lock(pts, length_w, .012, np.array([1., 0, 0]), 3, 'L', taper=.85))
    return locks


def hero_back(head, cap_v):
    """Spikes that stand up and out all round the crown: the tallest in the middle, each ring lower and flaring wider."""
    rnd = np.random.RandomState(23)
    locks = []
    up = np.array([0, 1., 0])

    def spike(az, el, length, width, rise, bend, joints, thick=.48, curl=0., taper=1.3, lean_back=.25):
        root, radial = scalp_point(cap_v, head, az, el, inset=.006)
        a = math.radians(az)
        out = np.array([math.sin(a), 0, -math.cos(a)])
        hd = unit(out * (1 - lean_back) + np.array([0, 0, -1.]) * lean_back)
        d = unit(hd * math.cos(rise) + up * math.sin(rise))
        along = unit(d - radial * np.dot(d, radial))
        side = unit(np.cross(radial, along))
        p0 = root
        p1 = root + along * length * .25 + radial * .008
        tipdir = unit(d + up * bend)
        p3 = root + along * length * .3 + d * length * .45 + tipdir * length * .3
        p2 = p3 - tipdir * length * .3
        pts = bezier(p0, p1, p2, p3, 26)
        pts[2:] = push_off(pts[2:], head, .010)
        locks.append(Lock(pts, width, width * thick, side, joints, 'S', taper=taper, curl=curl))

    # the crown: tall, nearly straight up
    for az, el in ((0, 84), (-60, 76), (60, 76), (180, 80), (-120, 78), (120, 78)):
        spike(az + rnd.uniform(-5, 5), el, rnd.uniform(.15, .175), rnd.uniform(.06, .066), math.radians(rnd.uniform(66, 74)), .1, 3, curl=rnd.uniform(-.2, .2))
    # the upper ring: up and out
    for az in range(-150, 180, 40):
        spike(az + 20 + rnd.uniform(-5, 5), 55 + rnd.uniform(-3, 3), rnd.uniform(.13, .15), rnd.uniform(.056, .062), math.radians(rnd.uniform(42, 52)), .2, 3, curl=rnd.uniform(-.25, .25))
    # the middle ring round the back and sides: out and a little up
    for az in (-110, -72, -36, 0, 36, 72, 110):
        spike(az + rnd.uniform(-4, 4), 28 + rnd.uniform(-3, 3), rnd.uniform(.105, .125), rnd.uniform(.05, .056), math.radians(rnd.uniform(16, 26)), .25, 3, curl=rnd.uniform(-.3, .3))
    # the low back and over the ears
    for az in (-56, -18, 18, 56):
        spike(az, 4, .085, .046, math.radians(-8), .15, 2)
    for sgn in (-1, 1):
        spike(sgn * 100, 12, .08, .046, math.radians(-10), .1, 2)
    return locks


def hero_front(head, cap_v):
    """Three broad spikes falling over the brow and one at each temple."""
    locks = []
    up = np.array([0, 1., 0])
    fwd = np.array([0, 0, 1.])
    for az, el, end, width in ((168, 60, (.04, .10, .14), .042), (-178, 64, (-.008, .085, .145), .046), (-160, 58, (-.052, .098, .14), .04),
                               (134, 46, (.088, .07, .115), .036), (-134, 46, (-.088, .07, .115), .036)):
        root, radial = scalp_point(cap_v, head, az, el)
        p0 = root
        p1 = root + fwd * .035 + up * .012
        p3 = np.array(end)
        p2 = p3 + np.array([0, .032, -.01])
        pts = push_off(bezier(p0, p1, p2, p3, 22), head, .013)
        locks.append(Lock(pts, width, .015, np.array([1., 0, 0]), 2, 'L', taper=1.1))
    return locks


def build(locks, name_kind):
    """The locks as one piece: vertices, normals, uvs, triangles, and chains (each lock with joints > 0 a chain: its joints at
    even arc length down it, the root on the scalp)."""
    V, U, T, chains = [], [], [], []
    bi, bw = [], []
    base = 0
    for lk in locks:
        v, uv, t, centre, s = lock_mesh(lk)
        V.append(v)
        U.append(uv)
        T.append(t + base)
        n = len(v)
        if lk.joints > 0:
            js = np.linspace(0, 1, lk.joints + 1) * .92
            joints = np.stack([np.interp(js, s, centre[:, k]) for k in range(3)], 1)
            chains.append((lk.kind, joints))
            # per vertex its v (root to tip) between the joints
            vv = uv[:, 1]
            for i in range(n):
                t_ = np.interp(vv[i], js, np.arange(len(js)))
                j = min(int(t_), len(js) - 2)
                u = t_ - j
                bi.append((len(chains), j, j + 1))
                bw.append((1 - u, u))
        else:
            for i in range(n):
                bi.append((0, -1, -1))
                bw.append((1., 0.))
        base += n
    V = np.concatenate(V)
    return V, np.concatenate(U), np.concatenate(T), chains, bi, bw
