"""The sleek face gear (owner 2026-11-27: "think we need to update the "masks" section now, they look out of place now with the
latest robot armor"; docs/PILOT-BODY-HAIR.md section 6): the MASKS row's helmet, half mask, mask and visor remade in the sleek pilot
suit's style (tools/gear/suitplates.py: designed outlines laid on a surface, bevelled rings, metal rims, glowing accents), in the
head bone's frame (x across, y up, z ahead: as the old ones, Core/NativeRobotHelm.cs, and the hats).

- The helmet: a dark inner shell close round the head (the old helmet's superellipsoid fitted to it; a hat sits on it by
  NativeRobotHelm.SleekHatFit), segmented white panels over it (the crown with its glowing crest, the sides, the back, the cheeks, the
  chin guard with its vents), a wraparound visor of glowing glass in a metal frame, a rounded ear pod with a light ring each side.
- The half mask: an angular muzzle plate over the nose, mouth and chin with glowing vents, a cheek guard each side running back
  towards a small pod on the ear.
- The visor: a wraparound band of glowing glass, a slim arm back over each ear (to the suit's ear pod, when it is worn).
- The ear pods: the sleek suit's, a small rounded pod with a light ring on each ear (in place of the robot suit's ear modules;
  hidden under the helmet and the half mask, which bring their own).
- The mask: a fitted dark mask over the nose, mouth, chin and cheeks (made on the face as the bodysuit is on the body), its
  seams glowing.

Each part is a plate record (suitplates' paints), its name prefixed by its gear ("Helmet: ", "Half mask: ", "Visor: ", "Mask: ",
"Ears: "; Core/NativeSleekFace.cs); hair.py --face writes them with the suit's plates (bone: the head; the ear pods moved into the
body's bind space, as the suit's plates are, the gear left in the head's frame, as its models are).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import suitplates as SPL  # noqa: E402
from suitplates import SHELL, METAL, DARK, LIGHT, SOFT, Part, finish, rounded, normals_of, fix_winding  # noqa: E402


def plate(*a, **k):
    """suitplates.plate, clean: its inner rings stop before they fold, the middle a grid laid on the surface."""
    k.setdefault('clean', True)
    return SPL.plate(*a, **k)

HEAD_BONE = 3  # (Core/NativeRobotSuit.cs Bones: the head)

# The helmet's inner surface: the old helmet's superellipsoid (Core/NativeRobotHelm.cs Shell) refitted to the head (owner
# 2026-11-27: "They look to bulky / wide"): of its family (with a lean: the front's reach shrinking up the upper half, as the
# forehead slopes back from the nose) the one closest round the head, 6 mm or more off it everywhere but the ears (a cup covers
# each): 1.6 cm in from the old one at the sides, 8 mm behind, 6 mm at the nose, 2-4 cm up the forehead, 7 mm at the crown.
AXIS_Z, MID, SIDE, AHEAD, BEHIND, CROWN, UNDER, N, NB, M, ML, LEAN = .033, .07, .105, .1245, .126, .221, .145, 2.15, 2.75, 3.0, 3.2, .3
# The head's ear (Head_AnimeYoung: its outer face at 10.8 cm, 4.2 cm tall, round this centre) and the plates' standoff.
EAR = (.108, .061, .014)
GAP, THICK, BEVEL = .002, .0045, .0035


def spow(a, e):
    return math.copysign(abs(a) ** e, a)


def shell(ph, ps, scale=1.0, hscale=1.0):
    s, c = math.sin(ps), math.cos(ps)
    m = M if s > 0 else ML
    r = abs(c) ** (2 / m)
    h = spow(s, 2 / m)
    n = N if math.cos(ph) > 0 else NB
    sx, sz = spow(math.sin(ph), 2 / n), spow(math.cos(ph), 2 / n)
    ahead = AHEAD * (1 - LEAN * max(h, 0))
    p = np.array([SIDE * r * sx, MID + (CROWN - MID if s > 0 else UNDER) * h, AXIS_Z + (ahead if sz > 0 else BEHIND) * r * sz])
    c0 = np.array([0, MID, AXIS_Z])
    q = c0 + (p - c0) * scale
    q[0] *= hscale
    q[2] = AXIS_Z + (q[2] - AXIS_Z) * hscale
    return q


def latitude(y):
    up = y > MID
    h = max(-1, min(1, (y - MID) / ((CROWN - MID) if up else UNDER)))
    return math.asin(spow(h, (M if up else ML) / 2))


def shell_normal(ph, ps, scale=1.0, hscale=1.0):
    e = 1e-3
    a = shell(ph + e, ps, scale, hscale) - shell(ph - e, ps, scale, hscale)
    b = shell(ph, min(ps + e, math.pi / 2), scale, hscale) - shell(ph, max(ps - e, -math.pi / 2), scale, hscale)
    n = np.cross(a, b)
    if np.linalg.norm(n) < 1e-12:
        n = np.array([0, 1., 0])
    n /= np.linalg.norm(n)
    p = shell(ph, ps, scale, hscale)
    if np.dot(n, p - np.array([0, MID, AXIS_Z])) < 0:
        n = -n
    return n


class ShellFrame:
    """(x, y) on the helmet: x the arc round it (metres at radius R, 0 ahead), y the height; `hscale` draws it in round the head's
    axis (the masks: the helmet's own smooth line, closer to the face)."""

    def __init__(self, R=.125, scale=1.0, hscale=1.0):
        self.R, self.scale, self.hscale = R, scale, hscale

    def place(self, x, y, lift):
        ph, ps = x / self.R, latitude(y)
        k = self.hscale(y) if callable(self.hscale) else self.hscale
        n = shell_normal(ph, ps, self.scale, k)
        return shell(ph, ps, self.scale, k) + n * lift, n


def clear_profile(head_V, y0, y1, bearing, margin, step=.008):
    """The horizontal scale by height (a function of y) at which the helmet's surface clears the head's points near each height
    within `bearing` of the front by `margin`: the face's own line (narrowing down the jaw), smoothed."""
    ys = np.arange(y0, y1 + 1e-9, step)
    ks = np.array([clear_scale(head_V, y - step * 1.5, y + step * 1.5, bearing, margin) for y in ys])
    for i in range(len(ks)):
        if ks[i] == 0:
            ks[i] = ks[max(0, i - 1)]
    for _ in range(3):
        ks = np.convolve(np.pad(ks, 1, mode='edge'), [.25, .5, .25], mode='valid')
    return lambda y: float(np.interp(y, ys, ks))


def clear_scale(head_V, y0, y1, bearing, margin):
    """The horizontal scale at which the helmet's surface clears the head's points between heights y0 and y1, within `bearing`
    of the front, by `margin` (m)."""
    k = 0.0
    for p in head_V:
        if not (y0 < p[1] < y1):
            continue
        ph = math.atan2(p[0], p[2] - AXIS_Z)
        if abs(ph) > bearing:
            continue
        q = shell(ph, latitude(p[1]))
        rs = math.hypot(q[0], q[2] - AXIS_Z)
        rp = math.hypot(p[0], p[2] - AXIS_Z)
        k = max(k, (rp + margin) / rs)
    return k


class TopFrame:
    """(x, z) seen from above onto the helmet's upper half (the crown, where the arc frame folds up)."""

    def __init__(self, scale=1.0):
        self.scale = scale

    def place(self, x, z, lift):
        ph = math.atan2(x, z - AXIS_Z)
        d = math.hypot(x, z - AXIS_Z)
        lo, hi = latitude(.12), math.pi / 2 - 1e-4
        for _ in range(40):
            mid = (lo + hi) / 2
            q = shell(ph, mid, self.scale)
            if math.hypot(q[0], q[2] - AXIS_Z) > d:
                lo = mid
            else:
                hi = mid
        ps = (lo + hi) / 2
        n = shell_normal(ph, ps, self.scale)
        return shell(ph, ps, self.scale) + n * lift, n


def shell_at_ear(sx, scale=1.0):
    """The point of the helmet's surface level with the ear's centre and as far back (bisecting round from the side), and its
    normal."""
    lo, hi = math.radians(60), math.radians(130)
    ps = latitude(EAR[1])
    for _ in range(40):
        mid = (lo + hi) / 2
        if shell(mid, ps, scale)[2] > EAR[2]:
            lo = mid
        else:
            hi = mid
    ph = sx * (lo + hi) / 2
    return shell(ph, ps, scale), shell_normal(ph, ps, scale)


def on_ear(sx, lift=.001):
    """A spot on the ear's outer face (a small pod sits there), facing out and a little back as the ear does."""
    n = np.array([sx * 1.0, 0, -.12])
    n /= np.linalg.norm(n)
    return np.array([sx * EAR[0], EAR[1], EAR[2]]) + n * lift, n


def surface_grid(part, P, nu, nv, paint, wrap=True, cap_top=False):
    """A surface over (u, v) in [0,1]^2 (u wraps), its faces wound outward by the given normals."""
    V, Nn, T = [], [], []
    cols = nu if wrap else nu + 1
    for j in range(nv + 1):
        for i in range(cols):
            q, n = P(i / nu, j / nv)
            V.append(q)
            Nn.append(n)
    for j in range(nv):
        for i in range(nu if wrap else nu):
            a = j * cols + i
            b = j * cols + (i + 1) % cols if wrap else j * cols + i + 1
            c, d = a + cols, b + cols
            T += [(a, b, c), (c, b, d)]
    V, Nn = np.array(V), np.array(Nn)
    T = fix_winding(V, T, Nn)
    part.add(list(V), list(normals_of(V, T, Nn)), paint, T)


def ear_pod(part, centre, outward, up, radius=.027, depth=.012, ring=LIGHT):
    """A rounded pod on the side of the head: a short drum facing `outward`, its face a shell with a glowing ring and a dark
    middle."""
    outward = outward / np.linalg.norm(outward)
    u = np.cross(up, outward)
    u /= np.linalg.norm(u)
    v = np.cross(outward, u)
    n = 32
    rings = [(radius, 0, METAL), (radius, depth * .7, SHELL), (radius * .92, depth, SHELL), (radius * .78, depth * 1.04, LIGHT if ring else SHELL),
             (radius * .66, depth * 1.04, LIGHT if ring else SHELL), (radius * .6, depth * 1.0, DARK), (0, depth * .96, DARK)]
    V, P_, T, Nn = [], [], [], []
    for k, (r, h, pt) in enumerate(rings):
        for i in range(n):
            a = 2 * math.pi * i / n
            q = centre + (u * math.cos(a) + v * math.sin(a)) * r + outward * h
            V.append(q)
            P_.append(pt)
            Nn.append(outward if k > 1 else u * math.cos(a) + v * math.sin(a))
    for k in range(len(rings) - 1):
        for i in range(n):
            a, b = k * n + i, k * n + (i + 1) % n
            T += [(a, b, a + n), (a + n, b, b + n)]
    V, Nn = np.array(V), np.array(Nn)
    T = fix_winding(V, T, Nn)
    part.add(list(V), list(normals_of(V, T, Nn)), P_, T)


# ---------------------------------------------------------------------------------------------------------------------------
# The helmet
# ---------------------------------------------------------------------------------------------------------------------------
def helmet():
    parts = []
    # the inner shell: dark, on the old helmet's surface a little in, from its foot (low ahead, high at the nape) to the top
    inner = Part('Helmet: inner shell', HEAD_BONE)

    def P(u, v):
        ph = -math.pi + 2 * math.pi * u
        foot = -.0415 - .0265 * math.cos(ph)
        y = foot + (CROWN - .002 - foot) * v
        ps = latitude(y) if v < 1 else math.pi / 2 - 1e-3
        return shell(ph, ps), shell_normal(ph, ps)
    surface_grid(inner, P, 72, 34, DARK)
    parts.append(finish(inner))
    sf, tf = ShellFrame(R=.125, scale=1.0), TopFrame(1.0)
    R = .125
    # the visor: glowing glass in a metal frame round the front, over the eyes
    v = Part('Helmet: visor', HEAD_BONE)
    vis = rounded([(-.15, .118), (.15, .118), (.162, .09), (.148, .064), (.05, .06), (0, .057), (-.05, .06), (-.148, .064), (-.162, .09)], .012)
    plate(v, sf, vis, gap=.0015, thick=.0035, bevel=.003, rings=3, paint=SOFT)
    parts.append(finish(v))
    # the crown: from the brow over the top to the back, its crest glowing down the middle
    c = Part('Helmet: crown', HEAD_BONE)
    crown = rounded([(-.062, .104), (.062, .104), (.095, .04), (.092, -.06), (.06, -.112), (-.06, -.112), (-.092, -.06), (-.095, .04)], .022)
    plate(c, tf, crown, gap=GAP, thick=THICK, bevel=BEVEL, grooves=[((-.055, .07), (.055, .07)), ((-.06, -.03), (.06, -.03))])
    crest = Part('Helmet: crest', HEAD_BONE)
    plate(crest, tf, rounded([(-.0045, .098), (.0045, .098), (.0045, -.1), (-.0045, -.1)], .003), gap=GAP + THICK + .0007, thick=.0012, bevel=.0008, rings=2, paint=LIGHT)
    c.add(crest.V, crest.N, crest.P, crest.T)
    parts.append(finish(c))
    # the brow: across the forehead over the visor, up to the crown
    bw = Part('Helmet: brow', HEAD_BONE)
    plate(bw, sf, rounded([(-R * math.radians(52), .186), (R * math.radians(52), .186), (R * math.radians(66), .126), (-R * math.radians(66), .126)], .012),
          gap=GAP, thick=THICK, bevel=BEVEL, accent=lambda q: q[1] < .135, accent_inset=.005, accent_w=.0025)
    parts.append(finish(bw))
    # the sides: from the temple back over the ear's height to the nape, each with an accent line along its top
    for mirror, name in ((False, 'R'), (True, 'L')):
        s = Part('Helmet: side ' + name, HEAD_BONE)
        side = rounded([(R * math.radians(70), .168), (R * math.radians(150), .178), (R * math.radians(166), .07), (R * math.radians(160), .0),
                        (R * math.radians(118), -.01), (R * math.radians(98), .03), (R * math.radians(78), .062)], .012)
        plate(s, sf, side, gap=GAP, thick=THICK, bevel=BEVEL, mirror=mirror, accent=lambda q: q[1] > .1, accent_inset=.006, accent_w=.0028)
        parts.append(finish(s))
        ch = Part('Helmet: cheek ' + name, HEAD_BONE)
        cheek = rounded([(R * math.radians(42), .054), (R * math.radians(94), .028), (R * math.radians(108), -.012), (R * math.radians(84), -.045),
                         (R * math.radians(48), -.042)], .01)
        plate(ch, sf, cheek, gap=GAP, thick=THICK, bevel=BEVEL, mirror=mirror, accent=lambda q: abs(q[0]) < R * math.radians(52), accent_inset=.005, accent_w=.0025)
        parts.append(finish(ch))
    # the back: a plate round the back of the head
    b = Part('Helmet: back', HEAD_BONE)
    back = rounded([(R * math.radians(164), .176), (R * math.radians(196), .176), (R * math.radians(200), .02), (R * math.radians(180), -.005),
                    (R * math.radians(160), .02)], .014)
    plate(b, sf, back, gap=GAP, thick=THICK, bevel=BEVEL, grooves=[((R * math.radians(168), .09), (R * math.radians(192), .09))])
    parts.append(finish(b))
    # the chin guard: an angular plate under the visor, two glowing vents each side of its ridge
    j = Part('Helmet: chin', HEAD_BONE)
    chin = rounded([(-R * math.radians(40), .05), (R * math.radians(40), .05), (R * math.radians(46), -.03), (R * math.radians(26), -.062),
                    (0, -.068), (-R * math.radians(26), -.062), (-R * math.radians(46), -.03)], .012)
    plate(j, sf, chin, gap=GAP, thick=THICK, bevel=BEVEL, grooves=[((0, .046), (0, -.062))])
    for sx in (-1, 1):
        for yy in (.012, -.006, -.024):
            vent = Part('vent', HEAD_BONE)
            x0, x1 = sx * R * math.radians(9), sx * R * math.radians(30)
            plate(vent, sf, rounded([(min(x0, x1), yy + .0035), (max(x0, x1), yy + .0035), (max(x0, x1), yy - .0035), (min(x0, x1), yy - .0035)], .002),
                  gap=GAP + THICK + .0007, thick=.0008, bevel=.0006, rings=2, paint=LIGHT)
            j.add(vent.V, vent.N, vent.P, vent.T)
    parts.append(finish(j))
    # the ear cups: over the ears (which stand 4 mm out of the inner shell), just proud of the panels
    for sx, name in ((1, 'R'), (-1, 'L')):
        e = Part('Helmet: ear ' + name, HEAD_BONE)
        q, n = shell_at_ear(sx)
        ear_pod(e, q, n, np.array([0, 1., 0]), radius=.026, depth=.0095)
        parts.append(finish(e))
    return parts


# ---------------------------------------------------------------------------------------------------------------------------
# The masks: on a smoothed copy of the face
# ---------------------------------------------------------------------------------------------------------------------------
class FaceSurface:
    """The head's surface smoothed (its nose and lips softened into a mask's line), for laying gear on: pilotsuit.Surface over
    it, and the head's own points to keep clear of."""

    def __init__(self, head_V, head_F, rounds=30):
        import garments as G
        import pilotsuit
        V = head_V.copy()
        nb = G.neighbours(len(V), head_F)
        for _ in range(rounds):
            for lam in (.6, -.62):
                V = V + lam * (np.array([V[n].mean(0) if len(n) else V[i] for i, n in enumerate(nb)]) - V)
        self.surf = pilotsuit.Surface(V, head_F)
        from scipy.spatial import cKDTree
        self.head = cKDTree(head_V)
        self.head_V = head_V

    def project(self, p, lift=0.0):
        return self.surf.project(p, lift)


class MaskFrame(SPL.TorsoFrame):
    """(x, y) round the face (x the arc from the front at radius R, y the height), on the smoothed face, held `clear` off the
    head's own surface."""

    def __init__(self, face, R=.1, clear=.004):
        super().__init__(face, axis_z=.02, R=R)
        self.face, self.clear = face, clear

    def place(self, x, y, lift):
        q, n = super().place(x, y, 0)
        # out until it clears the head by `clear` (the nose and lips stand out of the smoothed face)
        for _ in range(12):
            d, j = self.face.head.query(q + n * lift)
            if d >= self.clear + lift * .35:
                break
            q = q + n * .002
        return q + n * lift, n


def half_mask(face):
    parts = []
    mf = ShellFrame(R=.11, hscale=clear_profile(face.head_V, -.07, .08, math.radians(86), .005))
    m = Part('Half mask: muzzle', HEAD_BONE)
    muzzle = rounded([(-.03, .074), (.03, .074), (.05, .05), (.056, .0), (.036, -.042), (0, -.056), (-.036, -.042), (-.056, .0), (-.05, .05)], .012)
    plate(m, mf, muzzle, gap=GAP, thick=THICK, bevel=BEVEL, grooves=[((0, .068), (0, -.05))])
    for sx in (-1, 1):
        for yy in (.026, .01, -.006):
            vent = Part('vent', HEAD_BONE)
            x0, x1 = sx * .009, sx * .036
            plate(vent, mf, rounded([(min(x0, x1), yy + .0032), (max(x0, x1), yy + .0032), (max(x0, x1), yy - .0032), (min(x0, x1), yy - .0032)], .002),
                  gap=GAP + THICK + .0007, thick=.0008, bevel=.0006, rings=2, paint=LIGHT)
            m.add(vent.V, vent.N, vent.P, vent.T)
    parts.append(finish(m))
    for mirror, name in ((False, 'R'), (True, 'L')):
        g = Part('Half mask: cheek ' + name, HEAD_BONE)
        guard = rounded([(.05, .06), (.1, .066), (.145, .064), (.146, .05), (.108, .03), (.064, -.026), (.046, -.014)], .009)
        plate(g, mf, guard, gap=GAP, thick=.004, bevel=.003, mirror=mirror, accent=lambda q: q[1] > .058 and abs(q[0]) > .07, accent_inset=.005, accent_w=.0025)
        parts.append(finish(g))
    for sx, name in ((1, 'R'), (-1, 'L')):
        e = Part('Half mask: ear ' + name, HEAD_BONE)
        q, n = on_ear(sx)
        ear_pod(e, q, n, np.array([0, 1., 0]), radius=.016, depth=.006)
        parts.append(finish(e))
    return parts


def visor(face):
    parts = []
    mf = ShellFrame(R=.11, hscale=clear_profile(face.head_V, .055, .135, math.radians(86), .007))
    v = Part('Visor: glass', HEAD_BONE)
    band = rounded([(-.13, .122), (.13, .122), (.146, .1), (.132, .077), (.03, .073), (0, .069), (-.03, .073), (-.132, .077), (-.146, .1)], .012)
    plate(v, mf, band, gap=.0, thick=.003, bevel=.0025, rings=3, paint=SOFT)
    parts.append(finish(v))
    for mirror, name in ((False, 'R'), (True, 'L')):
        a = Part('Visor: arm ' + name, HEAD_BONE)
        plate(a, mf, rounded([(.138, .104), (.166, .099), (.166, .088), (.138, .091)], .004), gap=.0, thick=.0032, bevel=.002, mirror=mirror, paint=METAL)
        parts.append(finish(a))
    return parts


def mask(face, head_V, head_F):
    """The fitted mask: a thin dark shell over the nose, mouth, chin and cheeks, on the face's smooth line (the helmet's surface
    drawn in to 4 mm off the face), its seams glowing: down the middle, under the eyes, along the jaw."""
    mf = ShellFrame(R=.11, hscale=clear_profile(face.head_V, -.07, .08, math.radians(76), .004))
    part = Part('Mask: shell', HEAD_BONE)
    outline = rounded([(-.03, .072), (.03, .072), (.088, .07), (.11, .058), (.104, .014), (.074, -.032), (.036, -.053), (0, -.057), (-.036, -.053),
                       (-.074, -.032), (-.104, .014), (-.11, .058), (-.088, .07)], .014)
    plate(part, mf, outline, gap=.0, thick=.0025, bevel=.0025, rings=3, paint=DARK,
          accent=lambda q: q[1] > .062, accent_inset=.004, accent_w=.002)
    for line in (((0, .066), (0, -.05)), ((.04, -.04), (.094, .01)), ((-.04, -.04), (-.094, .01))):
        (x0, y0), (x1, y1) = line
        n_ = 24
        seam = Part('seam', HEAD_BONE)
        V, Nn, T = [], [], []
        for t in np.linspace(0, 1, n_):
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            dx, dy = x1 - x0, y1 - y0
            L = math.hypot(dx, dy)
            ox, oy = -dy / L * .0013, dx / L * .0013
            for sgn in (-1, 1):
                q, n = mf.place(x + ox * sgn, y + oy * sgn, .0034)
                V.append(q)
                Nn.append(n)
        for i in range(n_ - 1):
            a0, b0 = 2 * i, 2 * i + 2
            T += [(a0, b0, a0 + 1), (a0 + 1, b0, b0 + 1)]
        V, Nn = np.array(V), np.array(Nn)
        T = fix_winding(V, T, Nn)
        part.add(list(V), list(Nn), LIGHT, T)
    return [finish(part)]


def ears():
    """The sleek suit's ear pods: a small pod on each ear."""
    parts = []
    for sx, name in ((1, 'R'), (-1, 'L')):
        e = Part('Ears: ' + name, HEAD_BONE)
        q, n = on_ear(sx)
        ear_pod(e, q, n, np.array([0, 1., 0]), radius=.017, depth=.0065)
        parts.append(finish(e))
    return parts


def build(dump_dir):
    """Every gear's parts (plate records), from the hair lab's dump of the head (Head_AnimeYoung.json: the head bone's frame)."""
    import json
    d = json.load(open(os.path.join(dump_dir, 'Head_AnimeYoung.json')))
    r = max(d['renderers'], key=lambda r: len(r['v']))
    V = np.array(r['v'], float).reshape(-1, 3)
    F = np.concatenate([np.array(s, np.int64) for s in r['submeshes']]).reshape(-1, 3)
    import garments as G
    V, F, _ = G.compact(V, F)
    face = FaceSurface(V, F)
    return helmet() + half_mask(face) + visor(face) + mask(face, V, F) + ears()
