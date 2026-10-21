"""Samples cross-section profiles from the MakeHuman 1.1 base mesh as a FORM REFERENCE for build_male_base.py.

    blender -b --factory-startup --python sample_form_reference.py -- <makehuman base.obj> <target file> [out.json]

Provenance: the MakeHuman base mesh (makehuman/data/3dobjs/base.obj) and the macro target
universal-male-young-maxmuscle-averageweight.target from github.com/makehumancommunity/makehuman, released under
CC0 1.0 Universal (the repository's LICENSE.md section C and LICENSE.ASSETS.md; the file headers say "explicitly released
as CC0 in september 2020"). Neither file is committed here, and no MakeHuman geometry goes into the model.

The script only measures the reference. For each slice it records the normalized shape of the cross-section:
  - torso slices at landmark-normalized heights;
  - thigh, shin, upper arm and forearm slices perpendicular to the bone axis.
The builder scales these shapes to its own stylized widths and depths on the CapturedRig joints. The output is printed
(and optionally written) as JSON tables, which are pasted into build_male_base.py as FORM_*.
"""
import json
import math
import sys

from mathutils import Vector
from mathutils.bvhtree import BVHTree

ARGV = sys.argv[sys.argv.index('--') + 1:]
OBJ, TARGET = ARGV[0], ARGV[1]
OUT = ARGV[2] if len(ARGV) > 2 else None


def load():
    V, F, groups, g = [], [], {}, None
    for line in open(OBJ):
        if line.startswith('v '):
            V.append([float(x) for x in line.split()[1:4]])
        elif line.startswith('g '):
            g = line.split()[1]
        elif line.startswith('f '):
            idx = [int(t.split('/')[0]) - 1 for t in line.split()[1:]]
            if g == 'body':
                F.append(idx)
            elif g and g.startswith('joint-'):
                groups.setdefault(g, set()).update(idx)
    for line in open(TARGET):
        if line.startswith('#') or not line.strip():
            continue
        i, dx, dy, dz = line.split()
        i = int(i)
        V[i][0] += float(dx)
        V[i][1] += float(dy)
        V[i][2] += float(dz)
    ground = min(V[i][1] for f in F for i in f)
    P = [Vector((v[0] * .1, (v[1] - ground) * .1, v[2] * .1)) for v in V]   # metres, floor at 0, x left, z forward
    joints = {k[6:]: sum((P[i] for i in s), Vector()) / len(s) for k, s in groups.items()}
    return P, F, joints


P, F, JM = load()
TREE = BVHTree.FromPolygons([tuple(p) for p in P], F)


def first_hit(origin, d, far=.6):
    hit = TREE.ray_cast(Vector(origin), Vector(d), float(far))
    return (hit[0] - origin).length if hit[0] is not None else None


def crotch_height():
    hit = TREE.ray_cast(Vector((0, .2, JM['pelvis'].z)), Vector((0, 1, 0)), 1.0)
    return hit[0].y


# Torso landmarks: name -> MakeHuman height (the builder maps each to its own height on the CapturedRig joints).
LANDMARKS = {
    'crotch': crotch_height(), 'hip': JM['l-upper-leg'].y, 'pelvis': JM['pelvis'].y, 'navel': JM['spine-3'].y,
    'ribs': JM['spine-2'].y + .04, 'chest': JM['spine-1'].y - .02, 'shoulder': JM['l-shoulder'].y, 'neck': JM['neck'].y,
}
TORSO_THETA = [0, 22.5, 45, 67.5, 90, 112.5, 135, 157.5, 180]


def torso_slice(y):
    """Normalized half-ring (x/W, z/F or z/B) at height y: rays from the torso axis in the horizontal plane."""
    cz = JM['pelvis'].z
    pts = []
    for th in TORSO_THETA:
        t = math.radians(th)
        d = Vector((math.sin(t), 0, math.cos(t)))
        r = first_hit(Vector((0, y, cz)), d)
        pts.append(d * (r or 0))
    W = max(p.x for p in pts) or 1
    Fz = max(p.z for p in pts) or 1
    Bz = -min(p.z for p in pts) or 1
    return dict(W=W, F=Fz, B=Bz, shape=[[round(p.x / W, 4), round(p.z / (Fz if p.z >= 0 else Bz), 4)] for p in pts])


def limb_slices(a, b, ts, n, left=True):
    """Normalized ring radii r(phi)/mean along the bone a->b; phi 0 = outward, 90 = front (as build_male_base)."""
    d = (b - a).normalized()
    Z, X = Vector((0, 0, 1)), Vector((1 if left else -1, 0, 0))
    front = (Z - d * d.dot(Z)).normalized()
    out = (X - d * d.dot(X) - front * front.dot(X)).normalized()
    rows = []
    for t in ts:
        c = a + (b - a) * t
        rs = []
        for k in range(n):
            ph = 2 * math.pi * k / n
            r = first_hit(c, out * math.cos(ph) + front * math.sin(ph), .25)
            rs.append(r)
        good = sorted(r for r in rs if r)
        med = good[len(good) // 2]
        # a miss or a ray into the neighbouring part (torso, other leg) takes its neighbours' mean
        for k in range(n):
            if not rs[k] or rs[k] > 1.7 * med or rs[k] < .35 * med:
                lo, hi = rs[k - 1], rs[(k + 1) % n]
                rs[k] = ((lo if lo and lo <= 1.7 * med else med) + (hi if hi and hi <= 1.7 * med else med)) / 2
        m = sum(rs) / n
        rows.append(dict(t=t, mean=round(m, 4), shape=[round(r / m, 4) for r in rs]))
    return rows


def main():
    ys = sorted(LANDMARKS.values())
    torso = []
    names = sorted(LANDMARKS, key=lambda k: LANDMARKS[k])
    for i in range(len(names) - 1):
        for u in (0, .125, .25, .375, .5, .625, .75, .875):
            y = LANDMARKS[names[i]] + (LANDMARKS[names[i + 1]] - LANDMARKS[names[i]]) * u
            s = torso_slice(y)
            s.update(at=round(i + u, 4), y=round(y, 4))
            if not (i == 0 and u == 0):
                torso.append(s)
    out = dict(
        provenance='MakeHuman 1.1 base.obj + universal-male-young-maxmuscle-averageweight.target (CC0 1.0)',
        landmarks={k: round(v, 4) for k, v in LANDMARKS.items()},
        torso=torso,
        order=names,
        thigh=limb_slices(JM['l-upper-leg'], JM['l-knee'], [.25, .4, .55, .7, .85, .95], 10),
        shin=limb_slices(JM['l-knee'], JM['l-ankle'], [.08, .25, .45, .65, .85], 8),
        upperarm=limb_slices(JM['l-shoulder'], JM['l-elbow'], [.35, .5, .65, .8, .92], 10),
        forearm=limb_slices(JM['l-elbow'], JM['l-hand'], [.1, .25, .45, .65, .85, .95], 8),
    )
    s = json.dumps(out, indent=1)
    print(s)
    if OUT:
        open(OUT, 'w').write(s + '\n')


main()
