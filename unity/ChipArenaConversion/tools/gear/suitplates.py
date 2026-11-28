"""The sleek pilot suit's plates (owner 2026-11-27: "Sleek pilot suit": slimmer sculpted plates over a fitted bodysuit with glowing
seams; docs/PILOT-BODY-HAIR.md section 5). Each plate is a designed outline (a rounded polygon, in metres on the surface) laid onto
the bodysuit (tools/gear/pilotsuit.py) at a few millimetres off it: built as rings inset from its outline, so its edge bevels down
to the suit and its middle stands full thickness, its rim in dark metal, a glowing accent strip inset along chosen edges. A plate
rides one of the robot suit's bones (Core/NativeRobotSuit.cs Bones) and is authored in the body's bind space, as today's plates
are; its paint is one of the suit's (shell: the pilot's body colour; metal; dark metal; light: the accent's glow).

usage (from hair.py): suitplates.build(body, top_surface, legs_surface) -> list of part dicts
"""
import math

import numpy as np

SHELL, METAL, DARK, LIGHT, SOFT = 0, 1, 2, 3, 4
PAINT_RGB = {SHELL: (.95, .95, .93), METAL: (.17, .19, .22), DARK: (.10, .12, .14), LIGHT: (.2, .95, 1.), SOFT: (.5, .85, .95)}
BONES = ["spine_05", "pelvis", "neck_01", "head", "upperarm_l", "upperarm_r", "lowerarm_l", "lowerarm_r", "thigh_l", "thigh_r",
         "calf_l", "calf_r", "hand_l", "hand_r", "foot_l", "foot_r"]


# ---------------------------------------------------------------------------------------------------------------------------
# Outlines: rounded polygons in 2D (x across the surface, y along it), metres
# ---------------------------------------------------------------------------------------------------------------------------
def rounded(poly, r, n=6):
    """A polygon's corners rounded (radius r, n points a corner), counter-clockwise."""
    P = np.asarray(poly, float)
    if signed_area(P) < 0:
        P = P[::-1]
    out = []
    m = len(P)
    for i in range(m):
        a, b, c = P[i - 1], P[i], P[(i + 1) % m]
        u, v = a - b, c - b
        lu, lv = np.linalg.norm(u), np.linalg.norm(v)
        u, v = u / lu, v / lv
        ang = math.acos(max(-1, min(1, np.dot(u, v))))
        rr = min(r, .45 * min(lu, lv) * math.tan(ang / 2))
        d = rr / max(math.tan(ang / 2), 1e-6)
        p0, p1 = b + u * d, b + v * d
        for t in np.linspace(0, 1, n):
            # a quadratic through the corner, close enough to an arc for a plate's corner
            out.append((1 - t) ** 2 * p0 + 2 * (1 - t) * t * b + t ** 2 * p1)
    return resample(np.array(out), 64)


def signed_area(P):
    return .5 * np.sum(P[:, 0] * np.roll(P[:, 1], -1) - np.roll(P[:, 0], -1) * P[:, 1])


def resample(P, n):
    Q = np.concatenate([P, P[:1]])
    seg = np.linalg.norm(np.diff(Q, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    t = np.linspace(0, s[-1], n, endpoint=False)
    return np.stack([np.interp(t, s, Q[:, 0]), np.interp(t, s, Q[:, 1])], 1)


def inset(P, d):
    """The outline moved `d` inward (each point along the inward bisector, resampled)."""
    n = len(P)
    out = []
    for i in range(n):
        a, b, c = P[i - 1], P[i], P[(i + 1) % n]
        t = c - a
        t /= max(np.linalg.norm(t), 1e-9)
        inward = np.array([-t[1], t[0]])
        out.append(b + inward * d)
    return np.array(out)


def folded(R, O):
    """Whether inset ring R of outline O has crossed over itself somewhere (a segment reversed against the outline's)."""
    return bool((((np.roll(R, -1, 0) - R) * (np.roll(O, -1, 0) - O)).sum(1) < 0).any())


def inside(L, p):
    """Whether 2D point p is inside polygon L (even-odd)."""
    x, y = p
    c = False
    for i in range(len(L)):
        (x0, y0), (x1, y1) = L[i - 1], L[i]
        if (y0 > y) != (y1 > y) and x < x0 + (y - y0) * (x1 - x0) / (y1 - y0):
            c = not c
    return c


def grid_fill(L, step=.008):
    """Points on a grid inside polygon L (half a step or more off its edge) and triangles over L's points and them (indices:
    L's first, then the grid's), counter-clockwise: the plate's middle laid on its frame point by point, so it follows a curved
    surface as far as the plate reaches."""
    from scipy.spatial import Delaunay
    lo, hi = L.min(0), L.max(0)
    G = []
    for x in np.arange(lo[0] + step * .5, hi[0], step):
        for y in np.arange(lo[1] + step * .5, hi[1], step):
            if not inside(L, (x, y)):
                continue
            e = np.roll(L, -1, 0) - L
            t = np.clip((((x, y) - L) * e).sum(1) / np.maximum((e * e).sum(1), 1e-12), 0, 1)
            if np.min(np.linalg.norm(L + e * t[:, None] - (x, y), axis=1)) > step * .5:
                G.append((x, y))
    P2 = np.vstack([L, np.array(G).reshape(-1, 2)])
    T = []
    for t in Delaunay(P2).simplices:
        a, b, c = P2[t]
        if not inside(L, (a + b + c) / 3):
            continue
        if (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]) < 0:
            t = t[[0, 2, 1]]
        T.append(tuple(int(i) for i in t))
    return np.array(G).reshape(-1, 2), T


# ---------------------------------------------------------------------------------------------------------------------------
# Surfaces to lay outlines on
# ---------------------------------------------------------------------------------------------------------------------------
class TorsoFrame:
    """(x, y) on the torso: x the arc across (metres at radius R from the axis, 0 ahead of the sternum or behind the spine), y
    the height; `back` puts x = 0 behind."""

    def __init__(self, surf, axis_z=.02, R=.13, back=False):
        self.surf, self.z, self.R, self.back = surf, axis_z, R, back

    def place(self, x, y, lift):
        th = x / self.R + (math.pi if self.back else 0)
        axis = np.array([0, y, self.z])
        d = np.array([math.sin(th), 0, math.cos(th)])
        r = self.R
        for _ in range(5):
            q, n = self.surf.project(axis + d * r)
            r = max(.03, float(np.dot(q - axis, d)))
        q, n = self.surf.project(axis + d * r)
        return q + n * lift, n


class LimbFrame:
    """(x, y) on a limb: x the arc round it from its outward side (metres at radius R), y the distance from `a` toward `b`."""

    def __init__(self, surf, a, b, outward, R=.05):
        self.surf, self.a, self.R = surf, np.asarray(a, float), R
        ax = np.asarray(b, float) - self.a
        self.L = np.linalg.norm(ax)
        self.ax = ax / self.L
        o = np.asarray(outward, float)
        o = o - self.ax * np.dot(o, self.ax)
        self.u = o / np.linalg.norm(o)
        self.v = np.cross(self.ax, self.u)

    def place(self, x, y, lift):
        th = x / self.R
        c = self.a + self.ax * y
        d = self.u * math.cos(th) + self.v * math.sin(th)
        r = self.R
        for _ in range(5):
            q, n = self.surf.project(c + d * r)
            r = max(.01, float(np.dot(q - c, d)))
        q, n = self.surf.project(c + d * r)
        return q + n * lift, n


# ---------------------------------------------------------------------------------------------------------------------------
# Plates
# ---------------------------------------------------------------------------------------------------------------------------
class Part:
    def __init__(self, name, bone):
        self.name, self.bone = name, bone
        self.V, self.N, self.P, self.T = [], [], [], []

    def add(self, V, N, paint, T):
        base = len(self.V)
        self.V += list(V)
        self.N += list(N)
        self.P += [paint] * len(V) if np.isscalar(paint) else list(paint)
        self.T += [(a + base, b + base, c + base) for a, b, c in T]


def plate(part, frame, outline, gap=.003, thick=.007, bevel=.006, rings=5, paint=SHELL, accent=None, accent_inset=.007,
          accent_w=.004, mirror=False, grooves=(), clean=False):
    """A plate on `frame` inside `outline` (2D, metres): its top a stack of rings inset from the outline (the bevel), its wall down
    to the suit, a glowing accent strip along the outline where `accent(point)` holds (inset `accent_inset`), dark grooves
    (pairs of 2D points) across it. `mirror` flips x (the other side's). `clean` stops the inner rings before one folds and fills
    the middle with a grid (grid_fill: the face gear's, round the head's tighter curves; the suit's plates are as released
    without it)."""
    O = np.asarray(outline, float)
    if mirror:
        O = O[::-1].copy()
        O[:, 0] = -O[:, 0]
    nr = len(O)
    ring_d = [0] + [bevel * (k / (rings - 1)) ** 1.3 for k in range(1, rings)]
    heights = [thick * .45] + [thick * (.45 + .55 * math.sin(.5 * math.pi * k / (rings - 1))) for k in range(1, rings)]
    rings2d = [inset(O, d) for d in ring_d]
    # the inner field: rings further in to the middle (full height)
    c2d = O.mean(0)
    size = max(np.ptp(O[:, 0]), np.ptp(O[:, 1]))
    k = 1
    while True:
        d = bevel + k * .012
        R = inset(O, d)
        # stop when the ring folds over itself (its area shrinks to nothing or flips)
        if signed_area(R) < signed_area(O) * .04 or d > size * .48 or (clean and folded(R, O)):
            break
        rings2d.append(R)
        heights.append(thick)
        k += 1
    V, N, T = [], [], []
    for r2, h in zip(rings2d, heights):
        for p in r2:
            q, n = frame.place(p[0], p[1], gap + h)
            V.append(q)
            N.append(n)
    # the top: quads between rings, a fan to the middle (clean: a grid over it)
    for i in range(len(rings2d) - 1):
        a, b = i * nr, (i + 1) * nr
        for j in range(nr):
            j2 = (j + 1) % nr
            T += [(a + j, a + j2, b + j), (b + j, a + j2, b + j2)]
    last = (len(rings2d) - 1) * nr
    if clean:
        G, GT = grid_fill(rings2d[-1])
        g0 = len(V)
        for p in G:
            q, n = frame.place(p[0], p[1], gap + heights[-1])
            V.append(q)
            N.append(n)
        T += [tuple(last + i if i < nr else g0 + i - nr for i in t) for t in GT]
    else:
        cq, cn = frame.place(*rings2d[-1].mean(0), gap + thick)
        V.append(cq)
        N.append(cn)
        ci = len(V) - 1
        for j in range(nr):
            T.append((last + j, last + (j + 1) % nr, ci))
    top_paint = [paint] * len(V)
    # the outer ring is the rim: dark metal
    for j in range(nr):
        top_paint[j] = METAL
    part.add(V, normals_of(np.array(V), T, np.array(N)), top_paint, T)
    # the wall: the outline from its rim height down to the suit
    W, WN, WT = [], [], []
    for p in O:
        q0, n = frame.place(p[0], p[1], gap * .3)
        q1, _ = frame.place(p[0], p[1], gap + heights[0])
        W += [q1, q0]
        WN += [n, n]
    for j in range(nr):
        j2 = (j + 1) % nr
        a, b, c, d = 2 * j, 2 * j + 1, 2 * j2, 2 * j2 + 1
        WT += [(a, b, c), (c, b, d)]
    part.add(W, normals_of(np.array(W), WT, np.array(WN), outward_ref=np.array(V).mean(0)), METAL, WT)
    # the accent: a glowing strip along the outline's chosen stretch, on the top, inset
    if accent is not None:
        A = inset(O, accent_inset)
        A2 = inset(O, accent_inset + accent_w)
        keep = [accent(O[j] if not mirror else O[j] * [-1, 1]) for j in range(nr)]
        S, SN, ST = [], [], []
        for j in range(nr):
            for p in (A[j], A2[j]):
                q, n = frame.place(p[0], p[1], gap + thick + .0009)
                S.append(q)
                SN.append(n)
        for j in range(nr):
            j2 = (j + 1) % nr
            if keep[j] and keep[j2]:
                a, b, c, d = 2 * j, 2 * j + 1, 2 * j2, 2 * j2 + 1
                ST += [(a, c, b), (b, c, d)]
        if ST:
            part.add(S, SN, LIGHT, fix_winding(np.array(S), ST, np.array(SN)))
    for g in grooves:
        a, b = np.asarray(g[0], float), np.asarray(g[1], float)
        if mirror:
            a, b = a * [-1, 1], b * [-1, 1]
        G, GN, GT = [], [], []
        n_ = 14
        for t in np.linspace(0, 1, n_):
            p = a + (b - a) * t
            dirv = (b - a) / np.linalg.norm(b - a)
            side = np.array([-dirv[1], dirv[0]]) * .0016
            for s in (p - side, p + side):
                q, n = frame.place(s[0], s[1], gap + thick + .0006)
                G.append(q)
                GN.append(n)
        for i in range(n_ - 1):
            a0, b0 = 2 * i, 2 * i + 2
            GT += [(a0, b0, a0 + 1), (a0 + 1, b0, b0 + 1)]
        part.add(G, GN, DARK, fix_winding(np.array(G), GT, np.array(GN)))


def normals_of(V, T, N_hint, outward_ref=None):
    """Smooth normals from the triangles, each face wound to face along the surface's normal there (or away from `outward_ref`)."""
    N = np.zeros_like(V)
    for t in T:
        a, b, c = t
        n = np.cross(V[b] - V[a], V[c] - V[a])
        N[a] += n
        N[b] += n
        N[c] += n
    N /= np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-9)
    return N


def fix_winding(V, T, N):
    """Each face wound so cross(b - a, c - a) points along its corners' normals (the pack's winding: Unity's front)."""
    out = []
    for a, b, c in T:
        n = np.cross(V[b] - V[a], V[c] - V[a])
        out.append((a, b, c) if np.dot(n, N[a] + N[b] + N[c]) >= 0 else (a, c, b))
    return out


def finish(part):
    """The part's arrays, every face wound outward (by its corners' normals), normals recomputed."""
    V = np.array(part.V)
    N = np.array(part.N)
    T = fix_winding(V, part.T, N)
    return dict(name=part.name, bone=part.bone, V=V, N=normals_of(V, T, N), P=np.array(part.P, np.int64), T=np.array(T, np.int64))


# ---------------------------------------------------------------------------------------------------------------------------
# The suit's plates
# ---------------------------------------------------------------------------------------------------------------------------
def build(body, top, legs):
    B = body.bone
    parts = []
    chest_y = B('spine_05')[1]
    hips = (B('thigh_l')[1] + B('pelvis')[1]) / 2

    # the chest: two angular plates over the pectorals (the suit's chevron seam glows in the channel between them), each with a
    # groove across and the accent along its lower edge
    fr = TorsoFrame(top, R=.13)
    for mirror, side in ((False, ' R'), (True, ' L')):
        p = Part('Chest plate' + side, 0)
        pec = rounded([(.014, chest_y + .068), (.098, chest_y + .078), (.148, chest_y + .038), (.138, chest_y - .022), (.088, chest_y - .07),
                       (.014, chest_y - .052)], .014)
        plate(p, fr, pec, thick=.009, bevel=.008, mirror=mirror, accent=lambda q: q[1] < chest_y - .02 and abs(q[0]) > .03,
              grooves=[((.03, chest_y + .028), (.12, chest_y + .04))])
        parts.append(finish(p))
    # the abdomen: two tapered chevron segments
    for k, (y0, w0, w1, h) in enumerate(((chest_y - .095, .078, .064, .04), (chest_y - .145, .066, .052, .036))):
        p = Part('Abdomen %d' % (k + 1), 0)
        plate(p, fr, rounded([(-w0, y0), (0, y0 - .008), (w0, y0), (w1, y0 - h), (0, y0 - h - .012), (-w1, y0 - h)], .008), thick=.006,
              bevel=.005)
        parts.append(finish(p))
    # the back: two plates over the shoulder blades beside a tapered spine plate with the reactor (a dark panel, a glowing cross)
    bf = TorsoFrame(top, R=.13, back=True)
    for mirror, side in ((False, ' R'), (True, ' L')):
        p = Part('Shoulder blade' + side, 0)
        plate(p, bf, rounded([(.04, chest_y + .075), (.112, chest_y + .062), (.13, chest_y + .005), (.098, chest_y - .07), (.044, chest_y - .05)], .014),
              thick=.008, bevel=.007, mirror=mirror, accent=lambda q: q[1] > chest_y + .05)
        parts.append(finish(p))
    p = Part('Back plate', 0)
    plate(p, bf, rounded([(-.03, chest_y + .085), (.03, chest_y + .085), (.034, chest_y - .06), (0, chest_y - .15), (-.034, chest_y - .06)], .01),
          thick=.009, bevel=.007, accent=lambda q: q[1] < chest_y - .09)
    reactor = Part('Reactor', 0)
    plate(reactor, bf, rounded([(-.019, chest_y + .05), (.019, chest_y + .05), (.019, chest_y - .05), (-.019, chest_y - .05)], .006), gap=.0115,
          thick=.003, bevel=.002, paint=DARK)
    p.add(reactor.V, reactor.N, reactor.P, reactor.T)
    for (a, b) in (((0, chest_y + .038), (0, chest_y - .04)), ((-.012, chest_y + .012), (.012, chest_y + .012))):
        cross = Part('Reactor cross', 0)
        plate(cross, bf, rounded([(min(a[0], b[0]) - .0032, max(a[1], b[1])), (max(a[0], b[0]) + .0032, max(a[1], b[1])),
                                  (max(a[0], b[0]) + .0032, min(a[1], b[1]) - .0064), (min(a[0], b[0]) - .0032, min(a[1], b[1]) - .0064)], .0015),
              gap=.0148, thick=.0012, bevel=.0008, rings=2, paint=LIGHT)
        p.add(cross.V, cross.N, cross.P, cross.T)
    parts.append(finish(p))
    # the belt: a slim band round the waist with a glowing line and a front plate
    p = Part('Belt', 1)
    lf = TorsoFrame(legs, R=.12)
    belt_y = hips + .04
    for side, xs in (('front', (-.2, .2)), ('back', (-.2, .2))):
        frm = lf if side == 'front' else TorsoFrame(legs, R=.12, back=True)
        plate(p, frm, rounded([(xs[0], belt_y + .018), (xs[1], belt_y + .018), (xs[1], belt_y - .018), (xs[0], belt_y - .018)], .01),
              thick=.006, bevel=.004, accent=lambda q: abs(q[1] - belt_y) < .02 and q[1] < belt_y, accent_inset=.006, accent_w=.003)
    plate(p, lf, rounded([(-.035, belt_y + .028), (.035, belt_y + .028), (.028, belt_y - .03), (-.028, belt_y - .03)], .01), gap=.009,
          thick=.006, bevel=.004, accent=lambda q: True, accent_inset=.009, accent_w=.003)
    parts.append(finish(p))
    # the collar: a slim ring at the foot of the neck, open at the front
    p = Part('Collar', 2)
    neck = B('neck_01')
    nf = TorsoFrame(top, axis_z=neck[2] - .005, R=.055)
    ny = neck[1] + .012
    for mirror in (False, True):
        plate(p, nf, rounded([(.012, ny + .02), (.12, ny + .016), (.12, ny - .012), (.012, ny - .016)], .008), gap=.004, thick=.006,
              bevel=.004, mirror=mirror, accent=lambda q: q[1] > ny + .01, accent_inset=.005, accent_w=.0025)
    parts.append(finish(p))

    # the arms (left, then right): a pauldron of two swept layers, a vambrace with its accent line
    for s, bone_arm, bone_fore in (('l', 4, 6), ('r', 5, 7)):
        sh, el, wr = B('upperarm_' + s), B('lowerarm_' + s), B('hand_' + s)
        out = np.array([np.sign(sh[0]), 0, 0])
        side = ' L' if s == 'l' else ' R'
        p = Part('Pauldron' + side, bone_arm)
        uf = LimbFrame(top, sh + (sh - el) * .18, el, out + np.array([0, .75, 0]), R=.055)
        plate(p, uf, rounded([(-.088, -.005), (-.03, -.02), (.03, -.02), (.088, -.005), (.078, .075), (.02, .118), (-.05, .1), (-.08, .07)], .02),
              thick=.009, bevel=.008, accent=lambda q: q[1] > .08, grooves=[((-.06, .03), (.06, .03))])
        plate(p, uf, rounded([(-.07, .095), (.065, .1), (.05, .165), (0, .178), (-.05, .158)], .016), gap=.006, thick=.007, bevel=.006,
              accent=lambda q: q[1] > .15)
        parts.append(finish(p))
        p = Part('Vambrace' + side, bone_fore)
        ff = LimbFrame(top, el, wr, out + np.array([0, .4, 0]), R=.04)
        L = np.linalg.norm(wr - el)
        plate(p, ff, rounded([(-.045, L * .18), (.045, L * .18), (.04, L * .86), (-.04, L * .86)], .012), thick=.007, bevel=.006,
              accent=lambda q: abs(q[0]) < .012, accent_inset=.0, accent_w=.0)
        # (its accent: a straight glowing line down the middle)
        line = Part('Vambrace line', bone_fore)
        plate(line, ff, rounded([(-.0035, L * .25), (.0035, L * .25), (.0035, L * .8), (-.0035, L * .8)], .002), gap=.0105, thick=.0012,
              bevel=.001, rings=2, paint=LIGHT)
        p.add(line.V, line.N, line.P, line.T)
        parts.append(finish(p))

    # the legs: a slim thigh plate, a rounded knee cap with a glowing dot, a long shin guard with its line
    for s, bone_thigh, bone_calf in (('l', 8, 10), ('r', 9, 11)):
        th, ca, ft = B('thigh_' + s), B('calf_' + s), B('foot_' + s)
        side = ' L' if s == 'l' else ' R'
        fwd = np.array([0, 0, 1.]) + np.array([np.sign(th[0]) * .55, 0, 0])
        p = Part('Thigh plate' + side, bone_thigh)
        tf = LimbFrame(legs, th, ca, fwd, R=.075)
        L = np.linalg.norm(ca - th)
        plate(p, tf, rounded([(-.042, L * .26), (.05, L * .2), (.056, L * .58), (.018, L * .76), (-.044, L * .66)], .014), thick=.008, bevel=.007,
              accent=lambda q: q[1] > L * .6, grooves=[((-.03, L * .45), (.045, L * .4))])
        parts.append(finish(p))
        p = Part('Knee' + side, bone_calf)
        kf = LimbFrame(legs, ca, ft, np.array([0, 0, 1.]), R=.055)
        plate(p, kf, rounded([(-.04, -.05), (.04, -.05), (.045, .02), (0, .05), (-.045, .02)], .02), thick=.012, bevel=.009)
        dot = Part('Knee light', bone_calf)
        plate(dot, kf, rounded([(-.009, -.012), (.009, -.012), (.009, .006), (-.009, .006)], .006), gap=.0155, thick=.0015, bevel=.001, rings=2, paint=LIGHT)
        p.add(dot.V, dot.N, dot.P, dot.T)
        parts.append(finish(p))
        p = Part('Shin' + side, bone_calf)
        L2 = np.linalg.norm(ft - ca)
        sf = LimbFrame(legs, ca, ft, np.array([0, 0, 1.]), R=.05)
        plate(p, sf, rounded([(-.042, L2 * .14), (.042, L2 * .14), (.036, L2 * .8), (0, L2 * .86), (-.036, L2 * .8)], .014), thick=.008,
              bevel=.007, grooves=[((-.03, L2 * .45), (.03, L2 * .45))])
        line = Part('Shin line', bone_calf)
        plate(line, sf, rounded([(-.0035, L2 * .2), (.0035, L2 * .2), (.0035, L2 * .4), (-.0035, L2 * .4)], .002), gap=.0115, thick=.0012,
              bevel=.001, rings=2, paint=LIGHT)
        p.add(line.V, line.N, line.P, line.T)
        parts.append(finish(p))
    return parts
