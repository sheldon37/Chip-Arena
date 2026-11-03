"""The zombies' bespoke garments (owner 2026-11-03: "May have to create the outfits, kimono doesnt work for those well", then
"Utilize magica cloth for the farbric on the clothes"; docs/ZOMBIE-MODELS.md 4l): Ward Zero's hospital gown, the lab coat and the
scrubs (top and trousers), made on the pack body's bind pose and skinned to its bones, in place of the nearest pack pieces.

Reads a bind-pose dump of a zombie model (the local tool's binddump with bindall=1: rest.<renderer>.obj, .w4 with all four bone
weights, .shapes with each body shape's deltas, bind.txt) and writes the local bank Resources/ChipArenaGarments.bytes (made from the
pack's body, so kept local like it; Runtime NativeZombieGarments.cs loads it). Each garment is cloth laid over the body:
- the skin it covers (a region of the body's own surface, welded across its parts) eased out from it and smoothed into fabric:
  the shell is relaxed (Taubin) and pushed back out wherever it comes nearer the skin than its ease, so it bridges the hollows a
  body has and cloth does not (the spine's groove, under the chest, the navel), its openings' edges relaxed into clean hems;
- a skirt where it has one (the gown's to the knee, the coat's past it), grown down from the shell's hip line in rings that clear
  the legs and flare;
- its details: the coat's collar, lapels, buttons and pockets, the gown's ties at the back, the scrubs' V-neck and chest pocket,
  the trousers' waistband and drawstring;
- a lining (the shell again a few millimetres in, facing inward) so a sleeve or a hem seen from inside is cloth, joined to it round
  every opening;
- its skin weights (the nearest skin's, the skirt's eased toward the pelvis as it falls) and the body's nine shapes carried over
  (so it follows a zombie's build: a heavy belly, a woman's chest); vertex colours at a middle grey (the zombie tint makes them its
  colour), a texture coordinate round it and down it (the wear's noise; the cloth paint by its height: v), and the height its
  fabric hangs free from (Magica Cloth through the local bridge: its paint map, red pinned above that, green free below).

usage: blender --background --factory-startup --python tools/gear/garments.py -- --dump DIR --out ASSETS/LocalAssets [--preview DIR] [--only ids]
"""
import argparse
import math
import os
import struct
import sys

import numpy as np

BANK = 'ChipArenaGarments'
RESOURCES = 'ChipArenaLocal/Resources'
SHAPES = ['BodyType', 'Weight', 'Muscle', 'Belly', 'Chest', 'NeckThickness', 'Curvy', 'ButtSize', 'WaistSize']
GREY = (.5, .5, .5)


# ---------------------------------------------------------------------------------------------------------------------------
# The dump
# ---------------------------------------------------------------------------------------------------------------------------
def read_obj(path):
    vs, fs = [], []
    with open(path) as fh:
        for line in fh:
            if line.startswith('v '):
                vs.append([float(x) for x in line.split()[1:4]])
            elif line.startswith('f '):
                fs.append([int(x) - 1 for x in line.split()[1:4]])
    return np.array(vs), np.array(fs, dtype=np.int64)


def read_bind(path):
    out = {}
    with open(path) as fh:
        for line in fh:
            a = line.split()
            if len(a) != 18 or a[1] == 'transform':
                continue
            out.setdefault(a[0], []).append((a[1], np.array([float(x) for x in a[2:]]).reshape(4, 4)))
    return out


def read_w4(path, names):
    rows = []
    with open(path) as fh:
        for line in fh:
            a = line.split()
            rows.append({names[int(a[i])]: float(a[i + 1]) for i in range(0, 8, 2) if float(a[i + 1]) > 0})
    return rows


def read_shapes(path, n):
    out = {}
    with open(path) as fh:
        lines = fh.read().split('\n')
    i = 0
    while i < len(lines):
        a = lines[i].split()
        if a and a[0] == 's':
            d = np.array([[float(x) for x in l.split()] for l in lines[i + 1:i + 1 + n]])
            out[a[1]] = d
            i += 1 + n
        else:
            i += 1
    return out


class Body:
    """The pack body's skin (its parts welded into one), each vertex's bone weights and shape deltas, and the bones' places."""

    def __init__(self, dump):
        bind = read_bind(os.path.join(dump, 'bind.txt'))
        parts = sorted(r for r in bind if r.startswith('Body.Body_AnimeBasic'))
        V, F, W, S = [], [], [], {k: [] for k in SHAPES}
        for r in parts:
            v, f = read_obj(os.path.join(dump, 'rest.%s.obj' % r))
            names = [b for b, _ in bind[r]]
            w = read_w4(os.path.join(dump, 'rest.%s.w4' % r), names)
            sh = read_shapes(os.path.join(dump, 'rest.%s.shapes' % r), len(v))
            F.append(f + sum(len(x) for x in V))
            V.append(v)
            W += w
            for k in SHAPES:
                S[k].append(sh.get(k, np.zeros_like(v)))
        self.bindposes = {b: m for b, m in bind[parts[0]]}
        self.P = {b: np.linalg.inv(m)[:3, 3] for b, m in bind[parts[0]]}
        V = np.concatenate(V)
        F = np.concatenate(F)
        # Weld the parts' seams (their vertices meet exactly).
        key = np.round(V / 2e-4).astype(np.int64)
        _, first, remap = np.unique(key, axis=0, return_index=True, return_inverse=True)
        remap = remap.reshape(-1)
        self.V = V[first]
        self.W = [W[i] for i in first]
        self.S = {k: np.concatenate(S[k])[first] for k in SHAPES}
        F = remap[F]
        self.F = F[(F[:, 0] != F[:, 1]) & (F[:, 1] != F[:, 2]) & (F[:, 0] != F[:, 2])]
        self.N = normals(self.V, self.F)
        import mathutils
        self.tree = mathutils.kdtree.KDTree(len(self.V))
        for i, p in enumerate(self.V):
            self.tree.insert(p, i)
        self.tree.balance()

    def dominant(self, i):
        return max(self.W[i].items(), key=lambda x: x[1])[0] if self.W[i] else ''

    def leg(self, i):
        d = self.dominant(i)
        return d.startswith('thigh') or d.startswith('calf') or d == 'pelvis' or d == 'spine_01'

    def near(self, p, k=4):
        return self.tree.find_n(p, k)

    def bone(self, name):
        return self.P[name]


def normals(V, F):
    n = np.zeros_like(V)
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    for c in range(3):
        np.add.at(n, F[:, c], fn)
    ln = np.linalg.norm(n, axis=1, keepdims=True)
    return n / np.maximum(ln, 1e-12)


def compact(V, F):
    used = np.unique(F)
    m = -np.ones(len(V), dtype=np.int64)
    m[used] = np.arange(len(used))
    return V[used], m[F], used


def neighbours(n, F):
    nb = [set() for _ in range(n)]
    for a, b, c in F:
        nb[a].update((b, c))
        nb[b].update((a, c))
        nb[c].update((a, b))
    return [np.array(sorted(s), dtype=np.int64) for s in nb]


def boundary_edges(F):
    e = {}
    for a, b, c in F:
        for x, y in ((a, b), (b, c), (c, a)):
            k = (min(x, y), max(x, y))
            e[k] = e.get(k, 0) + 1
    return [k for k, c in e.items() if c == 1]


def loops(F):
    """The shell's openings: each boundary as an ordered loop of vertices."""
    adj = {}
    for a, b in boundary_edges(F):
        adj.setdefault(a, []).append(b)
        adj.setdefault(b, []).append(a)
    seen, out = set(), []
    for s in adj:
        if s in seen:
            continue
        loop, prev, cur = [s], None, s
        seen.add(s)
        while True:
            nxt = [x for x in adj[cur] if x != prev and x not in seen]
            if not nxt:
                break
            prev, cur = cur, nxt[0]
            loop.append(cur)
            seen.add(cur)
        if len(loop) > 2:
            out.append(loop)
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# Cloth over the body
# ---------------------------------------------------------------------------------------------------------------------------
def region_i(body, keep):
    """region by index and place."""
    ok = np.array([keep(i, p) for i, p in enumerate(body.V)])
    F = body.F[ok[body.F].all(axis=1)]
    V, F, used = compact(body.V, F)
    return V.copy(), F


def region(body, keep):
    """The body's surface where keep(p) holds for every corner: a shell to make cloth of."""
    ok = np.array([keep(p) for p in body.V])
    F = body.F[ok[body.F].all(axis=1)]
    V, F, used = compact(body.V, F)
    return V.copy(), F


def drape(body, V, F, ease, rounds=24, open_ease=None):
    """Relaxed over the skin: smoothed (Taubin) and pushed back out to `ease` (a function of the point: metres) from it, the
    openings' edges relaxed along themselves only, so they hem cleanly."""
    nb = neighbours(len(V), F)
    edge = set(v for e in boundary_edges(F) for v in e)
    lp = loops(F)
    ring_nb = {}
    for loop in lp:
        for i, v in enumerate(loop):
            ring_nb[v] = np.array([loop[i - 1], loop[(i + 1) % len(loop)]])
    for it in range(rounds):
        for lam in (.5, -.53):
            new = V.copy()
            for i in range(len(V)):
                ns = ring_nb.get(i) if i in edge else nb[i]
                if ns is None or len(ns) == 0:
                    continue
                new[i] = V[i] + lam * (V[ns].mean(axis=0) - V[i])
            V = new
        # Out to the ease from the skin's nearest points (their normals).
        for i in range(len(V)):
            want = ease(V[i])
            for (q, j, d) in body.near(V[i], 3):
                n = body.N[j]
                d = float(np.dot(V[i] - np.array(q), n))
                if d < want:
                    V[i] = V[i] + n * (want - d)
    return V


def extend_skirt(body, V, F, loop, hem_y, flare, rings=10, front_gap=0.0):
    """A skirt grown down from the shell's opening `loop` (round the hips) to `hem_y`: rings that clear the legs, flaring by
    `flare` (metres at the hem)."""
    # (The opening's own order round it, turned one way: sorting by bearing crossed faces where the waist dips.)
    loop = list(loop)
    P = V[loop]
    c = P.mean(axis=0)
    ang = np.unwrap(np.arctan2(P[:, 0] - c[0], P[:, 2] - c[2]))
    if ang[-1] < ang[0]:
        loop = loop[::-1]
    P = V[loop]
    top_y = P[:, 1].mean()
    legs = body.V[[i for i in range(len(body.V)) if hem_y - .05 < body.V[i, 1] < top_y and body.leg(i)]]
    newV = [V]
    rows = [loop]
    base = len(V)
    prev = P.copy()
    for r in range(1, rings + 1):
        t = r / rings
        y = top_y + (hem_y - top_y) * t
        ring = prev.copy()
        ring[:, 1] = y
        cx, cz = c[0], c[2]
        for i in range(len(ring)):
            dx, dz = ring[i, 0] - cx, ring[i, 2] - cz
            rad = math.hypot(dx, dz)
            a = math.atan2(dx, dz)
            near = legs[np.abs(legs[:, 1] - y) < .04]
            need = 0
            if len(near):
                da = np.arctan2(near[:, 0] - cx, near[:, 2] - cz)
                diff = np.abs((da - a + np.pi) % (2 * np.pi) - np.pi)
                sector = near[diff < .35]
                if len(sector):
                    need = np.hypot(sector[:, 0] - cx, sector[:, 2] - cz).max() + .025
            rad = max(rad, need) + flare * (1 / rings)
            ring[i, 0], ring[i, 2] = cx + math.sin(a) * rad, cz + math.cos(a) * rad
        idx = list(range(base, base + len(ring)))
        base += len(ring)
        newV.append(ring)
        rows.append(idx)
        prev = ring
    V = np.concatenate(newV)
    faces = []
    for r in range(len(rows) - 1):
        a, b = rows[r], rows[r + 1]
        n = len(a)
        for i in range(n):
            j = (i + 1) % n
            faces.append(np.array([[a[i], b[i], b[j]], [a[i], b[j], a[j]]]))
    skirt = orient(V, np.concatenate(faces), lambda p: np.array([p[0] - c[0], 0, p[2] - c[2]]))
    return V, np.concatenate([F, skirt]), rows[-1]


class Garment:
    def __init__(self, name, slot):
        self.name, self.slot = name, slot
        self.V = np.zeros((0, 3))
        self.F = np.zeros((0, 3), dtype=np.int64)
        self.C = np.zeros((0, 3))
        self.free_y = None

    def add(self, V, F, colour=GREY, colours=None):
        base = len(self.V)
        self.V = np.concatenate([self.V, V])
        self.F = np.concatenate([self.F, F + base])
        c = np.tile(np.array(colour), (len(V), 1)) if colours is None else colours
        self.C = np.concatenate([self.C, c])
        return base


def orient(V, F, outward):
    """Each face wound so its normal cross(b - a, c - a) faces outward(centroid) (the body's winding: Unity's)."""
    out = F.copy()
    for k, (a, b, c) in enumerate(F):
        n = np.cross(V[b] - V[a], V[c] - V[a])
        if np.dot(n, outward((V[a] + V[b] + V[c]) / 3)) < 0:
            out[k] = (a, c, b)
    return out


def lined(V, F, inset=.004):
    """The shell with its lining a few millimetres inside (faces reversed), joined to it round each opening."""
    N = normals(V, F)
    Vi = V - N * inset
    n = len(V)
    Fi = F[:, ::-1] + n
    rims = []
    for a, b in boundary_edges(F):
        rims.append([a, b, b + n])
        rims.append([a, b + n, a + n])
    allV = np.concatenate([V, Vi])
    rims = np.array(rims, dtype=np.int64).reshape(-1, 3)
    # (Each rim faces out of the opening: away from the face its edge belongs to.)
    third = {}
    for a, b, c in F:
        for x, y, z in ((a, b, c), (b, c, a), (c, a, b)):
            third[(min(x, y), max(x, y))] = z
    out = rims.copy()
    for k in range(0, len(rims), 2):
        a, b = rims[k][0], rims[k][1]
        mid = (V[a] + V[b]) / 2
        away = mid - V[third[(min(a, b), max(a, b))]]
        for m in (k, k + 1):
            x, y, z = out[m]
            n = np.cross(allV[y] - allV[x], allV[z] - allV[x])
            if np.dot(n, away) < 0:
                out[m] = (x, z, y)
    return allV, np.concatenate([F, Fi, out])


def box(c, size, frame, colour):
    """A small box (a button, a pocket's flap) at c in frame (r, u, f)."""
    r, u, f = frame
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    pts = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                pts.append(c + r * sx * hx + u * sy * hy + f * sz * hz)
    V = np.array(pts)
    F = np.array([[0, 1, 3], [0, 3, 2], [4, 6, 7], [4, 7, 5], [0, 4, 5], [0, 5, 1], [2, 3, 7], [2, 7, 6], [0, 2, 6], [0, 6, 4], [1, 5, 7], [1, 7, 3]])
    F = orient(V, F, lambda p: p - c)
    return V, F, np.tile(np.array(colour), (8, 1))


def cloth_frame(V, F, x, y, front=True):
    """On the garment's own outer surface at (x, y), its front (or back): the point, its normal, up and across."""
    N = normals(V, F)
    pick = np.where((np.abs(V[:, 0] - x) < .025) & (np.abs(V[:, 1] - y) < .025))[0]
    i = pick[np.argmax(V[pick, 2])] if front else pick[np.argmin(V[pick, 2])]
    n = N[i]
    up = np.array([0, 1.0, 0]) - n * n[1]
    up /= max(np.linalg.norm(up), 1e-9)
    return V[i], n, up, np.cross(up, n)


def surface_frame(body, p):
    """At the skin nearest p: its normal (out), up the body, across."""
    q, j, d = body.near(p, 1)[0]
    n = body.N[j]
    up = np.array([0, 1.0, 0]) - n * n[1]
    up /= max(np.linalg.norm(up), 1e-9)
    r = np.cross(up, n)
    return np.array(q), n, up, r


# ---------------------------------------------------------------------------------------------------------------------------
# The garments
# ---------------------------------------------------------------------------------------------------------------------------
def arm_t(body, p, side):
    """How far along the arm (0 the shoulder, 1 the wrist) p lies, and its distance from the arm's line."""
    a = body.bone('upperarm_' + side)
    b = body.bone('hand_' + side)
    ab = b - a
    t = float(np.dot(p - a, ab) / np.dot(ab, ab))
    return t


def side_of(p):
    # (The pack's left is -x in its bind space.)
    return 'l' if p[0] < 0 else 'r'


def torso_top(body, sleeve, low_y, neck_v=0.0):
    """The skin from `low_y` up to the neck's foot, the arms to `sleeve` of their length (0 none, 1 the wrist), the neckline cut
    lower at the front by neck_v (a V)."""
    neck = body.bone('neck_01')
    clav = (body.bone('clavicle_l') + body.bone('clavicle_r')) / 2

    def keep(p):
        if p[1] < low_y:
            return False
        if abs(p[0]) > .13 and p[1] < clav[1] + .05:
            # the arm (or the armpit) by its share of the arm's length
            t = arm_t(body, p, side_of(p))
            if t > sleeve:
                return False
        if p[1] > neck[1] - .005:
            return False
        if neck_v > 0 and p[2] > .02 and abs(p[0]) < .5 * neck_v and p[1] > neck[1] - neck_v - .005 + abs(p[0]) * 1.2:
            return False
        if abs(p[0]) > .2 and p[1] < .8:
            return False
        return True
    return region(body, keep)


def waist_y(body):
    """The waist (the narrowest, smoothest line round the trunk): where the gown's and the coat's skirts begin."""
    return body.bone('spine_01')[1] + .015


def hips_y(body):
    return (body.bone('thigh_l')[1] + body.bone('pelvis')[1]) / 2


def lowest_loop(V, F):
    """The waist's opening: the lowest of those round the body's middle (not a sleeve's)."""
    lp = [l for l in loops(F) if abs(V[l][:, 0].mean()) < .08 and len(l) > 16]
    return min(lp, key=lambda l: V[l][:, 1].mean())


def make_gown(body):
    """The hospital gown: short loose sleeves, a round neckline, a skirt to the knee, ties at the back; a faint print."""
    g = Garment('g-gown', 1)
    V, F = torso_top(body, .42, waist_y(body))
    V = drape(body, V, F, lambda p: .03 if p[1] < 1.15 else .02, rounds=30)
    loop = lowest_loop(V, F)
    knee = body.bone('calf_l')[1] + .02
    V, F, hem = extend_skirt(body, V, F, loop, knee, .11)
    # (Relaxed again as one: the skirt runs on from the hips without a step.)
    V = drape(body, V, F, lambda p: .03 if p[1] < 1.15 else .02, rounds=10)
    V, F = lined(V, F)
    # The print: small diamonds, a little darker (by the vertex: the drape is fine enough).
    u = np.arctan2(V[:, 0], V[:, 2]) / (2 * np.pi)
    pat = (np.abs(((u * 40) % 1) - .5) + np.abs(((V[:, 1] * 22) % 1) - .5)) < .18
    C = np.tile(np.array(GREY), (len(V), 1))
    C[pat] *= .78
    g.add(V, F, colours=C)
    # Ties at the back: two short strips at the neck and the waist.
    shell = (g.V.copy(), g.F.copy())
    for y in (body.bone('neck_01')[1] - .06, waist_y(body) + .03):
        q, n, up, r = cloth_frame(*shell, 0, y, front=False)
        c = q + n * .004
        for s in (-1, 1):
            Vb, Fb, Cb = box(c + r * s * .025 - up * .03, (.012, .07, .004), (r, up, n), (.42, .42, .42))
            g.add(Vb, Fb, colours=Cb)
    g.free_y = hips_y(body) - .02
    return g


def make_coat(body):
    """The lab coat: long sleeves, a collar and lapels, buttoned down the front, two hip pockets and a breast pocket, a skirt
    past the knee."""
    g = Garment('g-lab-coat', 1)
    V, F = torso_top(body, .96, waist_y(body), neck_v=.07)
    V = drape(body, V, F, lambda p: .026 if p[1] < 1.15 else .018, rounds=30)
    loop = lowest_loop(V, F)
    hem = body.bone('calf_l')[1] - .06
    V, F, _ = extend_skirt(body, V, F, loop, hem, .09)
    V = drape(body, V, F, lambda p: .026 if p[1] < 1.15 else .018, rounds=10)
    V, F = lined(V, F)
    g.add(V, F)
    dark = (.16, .16, .17)
    # The buttons down the front, the pockets (a flap each), the collar's points.
    shell = (g.V.copy(), g.F.copy())
    for k in range(6):
        q, n, up, r = cloth_frame(*shell, 0, 1.28 - k * .1)
        Vb, Fb, Cb = box(q + n * .004, (.014, .014, .006), (r, up, n), dark)
        g.add(Vb, Fb, colours=Cb)
    for x, y, w, h in ((.13, hips_y(body) - .05, .13, .12), (-.13, hips_y(body) - .05, .13, .12), (-.085, 1.3, .08, .08)):
        q, n, up, r = cloth_frame(*shell, x, y)
        Vb, Fb, Cb = box(q + n * .003, (w, h, .004), (r, up, n), (.47, .47, .47))
        g.add(Vb, Fb, colours=Cb)
    neck = body.bone('neck_01')
    for s in (-1, 1):
        q, n, up, r = cloth_frame(*shell, s * .05, neck[1] - .07)
        Vb, Fb, Cb = box(q + n * .004, (.05, .11, .005), (r, up, n), (.46, .46, .46))
        g.add(Vb, Fb, colours=Cb)
    g.free_y = hips_y(body) - .06
    return g


def make_scrubs_top(body):
    """The scrubs top: short sleeves, a V-neck, a breast pocket, its hem at the hip."""
    g = Garment('g-scrubs-top', 1)
    V, F = torso_top(body, .38, hips_y(body) - .02, neck_v=.1)
    # (Looser toward its hem, which hangs over the trousers' waistband.)
    V = drape(body, V, F, lambda p: .015 + .016 * max(0, min(1, (hips_y(body) + .16 - p[1]) / .1)), rounds=28)
    V, F = lined(V, F)
    g.add(V, F)
    q, n, up, r = cloth_frame(V, F, -.08, 1.3)
    Vb, Fb, Cb = box(q + n * .003, (.075, .07, .004), (r, up, n), (.46, .46, .46))
    g.add(Vb, Fb, colours=Cb)
    return g


def make_scrubs_pants(body):
    """The scrubs trousers: loose legs to the ankle, a waistband and its drawstring."""
    g = Garment('g-scrubs-pants', 2)
    waist = hips_y(body) + .06
    ankle = body.bone('foot_l')[1] + .06

    legs = set(i for i in range(len(body.V)) if body.leg(i) or body.dominant(i).startswith('spine_01'))
    V, F = region_i(body, lambda i, p: ankle < p[1] < waist and i in legs)
    V = drape(body, V, F, lambda p: .016 + .02 * max(0, min(1, (hips_y(body) - p[1]) / .6)))
    V, F = lined(V, F)
    g.add(V, F)
    q, n, up, r = surface_frame(body, np.array([0, waist - .03, .3]))
    for s in (-1, 1):
        Vb, Fb, Cb = box(q + n * .025 + r * s * .012 - up * .04, (.006, .08, .004), (r, up, n), (.62, .62, .62))
        g.add(Vb, Fb, colours=Cb)
    return g


GARMENTS = {'g-gown': make_gown, 'g-lab-coat': make_coat, 'g-scrubs-top': make_scrubs_top, 'g-scrubs-pants': make_scrubs_pants}


# ---------------------------------------------------------------------------------------------------------------------------
# Skin weights, shapes, the bank
# ---------------------------------------------------------------------------------------------------------------------------
def skin(body, g):
    """Each vertex's four heaviest bones (the nearest skin's, weighted by distance; below the hips eased toward the pelvis as
    the skirt falls) and each body shape's delta (the nearest skin's)."""
    hy = hips_y(body)
    W, S = [], {k: np.zeros_like(g.V) for k in SHAPES}
    for i, p in enumerate(g.V):
        near = body.near(p, 6)
        acc = {}
        tot = 0
        for (q, j, d) in near:
            k = 1 / max(d, 1e-4) ** 2
            tot += k
            for b, w in body.W[j].items():
                acc[b] = acc.get(b, 0) + w * k
            for s in SHAPES:
                S[s][i] += body.S[s][j] * k
        for s in SHAPES:
            S[s][i] /= tot
        acc = {b: w / tot for b, w in acc.items()}
        if g.slot == 1 and p[1] < hy - .05 and abs(p[0]) < .3:
            fall = min(1.0, (hy - .05 - p[1]) / .35) * .6
            acc = {b: w * (1 - fall) for b, w in acc.items()}
            acc['pelvis'] = acc.get('pelvis', 0) + fall
        top = sorted(acc.items(), key=lambda x: -x[1])[:4]
        t = sum(w for _, w in top)
        W.append([(b, w / t) for b, w in top])
    return W, S


def uvs(g):
    u = (np.arctan2(g.V[:, 0], g.V[:, 2]) / (2 * np.pi)) % 1
    top, low = g.V[:, 1].max(), g.V[:, 1].min()
    v = (top - g.V[:, 1]) / max(1e-6, top - low)
    return np.stack([u, v], axis=1), top, low


def write_bank(path, garments, body):
    bones = sorted({b for g in garments for row in g.W for b, _ in row})
    bi = {b: i for i, b in enumerate(bones)}
    out = bytearray(b'GARM')
    out += struct.pack('<ii', 1, len(bones))
    for b in bones:
        e = b.encode()
        out += struct.pack('<H', len(e)) + e
    out += struct.pack('<i', len(garments))
    for g in garments:
        e = g.name.encode()
        out += struct.pack('<H', len(e)) + e
        uv, top, low = uvs(g)
        free = -1.0 if g.free_y is None else (top - g.free_y) / max(1e-6, top - low)
        n = len(g.V)
        N = normals(g.V, g.F)
        out += struct.pack('<Bfi', g.slot, free, n)
        out += np.asarray(g.V, '<f4').tobytes() + np.asarray(N, '<f4').tobytes() + np.asarray(uv, '<f4').tobytes()
        # (RGBA: the alpha 1, outlined.)
        rgba = np.concatenate([np.asarray(g.C), np.ones((n, 1))], axis=1)
        out += np.clip(np.round(rgba * 255), 0, 255).astype(np.uint8).tobytes()
        idx = np.zeros((n, 4), dtype='<u2')
        wts = np.zeros((n, 4), dtype='<f4')
        for i, row in enumerate(g.W):
            for k, (b, w) in enumerate(row):
                idx[i, k] = bi[b]
                wts[i, k] = w
        out += idx.tobytes() + wts.tobytes()
        # (Unity's winding: the body's own, each face's normal cross(b - a, c - a) outward; the shells keep it, the rest is
        # wound to it.)
        tri = np.asarray(g.F, '<u4')
        out += struct.pack('<i', len(tri)) + tri.tobytes()
        out += struct.pack('<i', len(SHAPES))
        for s in SHAPES:
            e = s.encode()
            out += struct.pack('<H', len(e)) + e + np.asarray(g.S[s], '<f4').tobytes()
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as fh:
        fh.write(out)
    meta = path + '.meta'
    if not os.path.exists(meta):
        import uuid
        with open(meta, 'w', newline='\n') as fh:
            fh.write('fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % uuid.uuid4().hex)
    return len(out)


def preview(body, garments, out_dir):
    import bpy
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    def mesh(name, V, F, colour):
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(p) for p in V], [], [tuple(int(x) for x in f) for f in F])
        me.update()
        ob = bpy.data.objects.new(name, me)
        scene.collection.objects.link(ob)
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*colour, 1)
        me.materials.append(mat)
        for p in me.polygons:
            p.use_smooth = True
        return ob
    # (Blender: z up; the body's y up, z ahead.)
    def bl(V):
        return np.stack([V[:, 0], -V[:, 2], V[:, 1]], axis=1)
    mesh('body', bl(body.V), body.F, (.78, .62, .56))
    cols = {'g-gown': (.62, .78, .8), 'g-lab-coat': (.92, .92, .9), 'g-scrubs-top': (.25, .55, .55), 'g-scrubs-pants': (.25, .55, .55)}
    obs = [mesh(g.name, bl(g.V), g.F, cols.get(g.name, (.6, .6, .6))) for g in garments]
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.render.resolution_x, scene.render.resolution_y = 520, 760
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.lens = 50
    # (Each garment alone over the body; the scrubs' top and trousers together.)
    sets = [[g.name] for g in garments if not g.name.startswith('g-scrubs')] + [[g.name for g in garments if g.name.startswith('g-scrubs')]]
    for show in [s for s in sets if s]:
        for ob in obs:
            ob.hide_render = ob.name not in show
        for name, yaw in (('front', 0), ('tq', 40), ('side', 90), ('back', 180)):
            a = math.radians(yaw)
            cam.location = (math.sin(a) * 3.6, -math.cos(a) * 3.6, .95)
            cam.rotation_euler = (math.radians(90), 0, a)
            scene.render.filepath = os.path.join(out_dir, '%s_%s.png' % (show[0], name))
            bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--dump', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--preview')
    ap.add_argument('--only')
    a = ap.parse_args(argv)
    body = Body(a.dump)
    ids = a.only.split(',') if a.only else list(GARMENTS)
    garments = []
    for gid in ids:
        g = GARMENTS[gid](body)
        g.W, g.S = skin(body, g)
        print('garment %-16s %6d vertices %6d triangles' % (g.name, len(g.V), len(g.F)))
        garments.append(g)
    size = write_bank(os.path.join(a.out, RESOURCES, BANK + '.bytes'), garments, body)
    print('bank %s: %d garments, %.2f MB' % (BANK, len(garments), size / 1e6))
    if a.preview:
        preview(body, garments, a.preview)


if __name__ == '__main__':
    main()
