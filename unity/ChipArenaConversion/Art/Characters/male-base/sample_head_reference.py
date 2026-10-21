"""Samples the head and face of the MakeHuman 1.1 base mesh as a FORM REFERENCE for build_male_base.py.

    blender -b --factory-startup --python sample_head_reference.py -- <base.obj> <macro target> <modifier dir> [out.json]

Provenance: the MakeHuman base mesh (makehuman/data/3dobjs/base.obj), the macro target
universal-male-young-maxmuscle-averageweight.target and the face modifier targets named in MODS (makehuman/data/targets/
head, chin, cheek, eyebrows, forehead, nose, mouth, eyes, neck) from github.com/makehumancommunity/makehuman, released
under CC0 1.0 Universal (the repository's LICENSE.md section C and LICENSE.ASSETS.md; each file says "explicitly released
as CC0 in september 2020"). None of those files is committed here and no MakeHuman geometry goes into the model.

The script only measures. The modifiers turn MakeHuman's neutral face into a young adult male (a defined jaw and chin,
cheekbones, a forward brow, a fuller nose, a thicker neck, modest lips and smaller eyes). It then records:
  - a radial field: the distance from a centre inside the skull to the skin along a (theta, phi) grid, theta 0 at the
    front centre line to 180 at the back (the left half), phi -80 (down through the neck) to 90 (the crown); the eye
    helpers are included, so the eye openings read as the eyeball;
  - which grid cells see the eyeball (the eye opening), and the face landmarks (eye centre and eyeball radius, the lid
    corners, the nasion, the nose tip, the subnasale, the alae, the mouth corners and lip line, the chin, the jaw angle,
    the ear) as (theta, phi, r).
Lengths are divided by the head height (chin to crown). The builder fits the field to its own head size on the visual
skeleton, places its own low-poly vertex layout on it and adds its own stylization.
"""
import json
import math
import sys

from mathutils import Vector
from mathutils.bvhtree import BVHTree

ARGV = sys.argv[sys.argv.index('--') + 1:]
OBJ, MACRO, MODDIR = ARGV[0], ARGV[1], ARGV[2]
OUT = ARGV[3] if len(ARGV) > 3 else None

# MakeHuman face modifiers (target file name: weight), chosen by eye from front, three-quarter and profile renders.
MODS = {
    'head-square': .25, 'head-back-scale-depth-incr': .30,
    'chin-prominent-incr': .50, 'chin-width-incr': .25, 'chin-bones-incr': .30, 'chin-height-incr': .15,
    'l-cheek-bones-incr': .45, 'r-cheek-bones-incr': .45, 'l-cheek-inner-decr': .30, 'r-cheek-inner-decr': .30,
    'eyebrows-trans-forward': .55, 'eyebrows-trans-down': .35, 'forehead-trans-backward': .25,
    'nose-width1-incr': .25, 'nose-scale-horiz-incr': .20, 'nose-point-width-incr': .20,
    'nose-nostrils-width-incr': .25, 'nose-scale-vert-incr': .15,
    'mouth-upperlip-volume-decr': .35, 'mouth-lowerlip-volume-decr': .25,
    'l-eye-scale-decr': .25, 'r-eye-scale-decr': .25,
    'neck-scale-horiz-incr': .50, 'neck-scale-depth-incr': .40,
}
THETA = [i * 2.5 for i in range(73)]            # 0 .. 180
PHI = [-80 + i * 2.5 for i in range(69)]        # -80 .. 90


def load():
    V, groups, g = [], {}, None
    faces = {'body': [], 'helper-l-eye': [], 'helper-r-eye': []}
    for line in open(OBJ):
        if line.startswith('v '):
            V.append([float(x) for x in line.split()[1:4]])
        elif line.startswith('g '):
            g = line.split()[1]
        elif line.startswith('f '):
            idx = [int(t.split('/')[0]) - 1 for t in line.split()[1:]]
            if g in faces:
                faces[g].append(idx)
            elif g and g.startswith('joint-'):
                groups.setdefault(g, set()).update(idx)

    def apply(path, wt):
        for line in open(path):
            if line.startswith('#') or not line.strip():
                continue
            i, dx, dy, dz = line.split()
            i = int(i)
            V[i][0] += float(dx) * wt
            V[i][1] += float(dy) * wt
            V[i][2] += float(dz) * wt

    apply(MACRO, 1.0)
    for name in sorted(MODS):
        apply('%s/%s.target' % (MODDIR, name), MODS[name])
    ground = min(V[i][1] for f in faces['body'] for i in f)
    P = [Vector((v[0] * .1, (v[1] - ground) * .1, v[2] * .1)) for v in V]   # metres, x left, y up, z forward
    joints = {k[6:]: sum((P[i] for i in s), Vector()) / len(s) for k, s in groups.items()}
    return P, faces, joints


P, FACES, JM = load()
ALL = FACES['body'] + FACES['helper-l-eye'] + FACES['helper-r-eye']
N_BODY = len(FACES['body'])
TREE = BVHTree.FromPolygons([tuple(p) for p in P], ALL)
HEAD_VERTS = sorted({i for f in FACES['body'] for i in f if P[i].y > JM['neck'].y + .05})
CROWN_Y = max(P[i].y for i in HEAD_VERTS)


def front_profile(y):
    """The front-most body point on the centre line at height y (a ray from far ahead, straight back)."""
    hit = TREE.ray_cast(Vector((0, y, .5)), Vector((0, 0, -1)), 1.0)
    return hit[0]


def chin_y():
    """The gnathion: the lowest point of the chin's front contour (where the front profile turns under the jaw)."""
    # scan from the mouth down: the chin is where the profile's z falls back fastest
    prof = []
    y = JM['head'].y
    while y > JM['neck'].y:
        p = front_profile(y)
        if p is not None:
            prof.append(p)
        y -= .001
    drop = max(range(1, len(prof)), key=lambda i: prof[i - 1].z - prof[i].z)
    return prof[drop - 1]


CHIN = chin_y()
HEIGHT = CROWN_Y - CHIN.y
EYE = JM['l-eye']
back = TREE.ray_cast(Vector((0, EYE.y, -.5)), Vector((0, 0, 1)), 1.0)[0]
brow = front_profile(EYE.y + .02)
CENTRE = Vector((0, CHIN.y + HEIGHT * .5, (back.z + brow.z) * .5))


def direction(th, ph):
    t, p = math.radians(th), math.radians(ph)
    return Vector((math.sin(t) * math.cos(p), math.sin(p), math.cos(t) * math.cos(p)))


def to_polar(p):
    d = p - CENTRE
    r = d.length
    ph = math.degrees(math.asin(max(-1, min(1, d.y / r))))
    th = math.degrees(math.atan2(abs(d.x), d.z))
    return [round(th, 3), round(ph, 3), round(r / HEIGHT, 5)]


def ear_face(i):
    """MakeHuman's ear (left): body faces standing out from the side of the skull behind the cheek."""
    c = sum((P[k] for k in ALL[i]), Vector()) / len(ALL[i])
    return (c.x > EYE.x + .0457 and EYE.y - .07 < c.y < EYE.y + .03 and EYE.z - .13 < c.z < EYE.z - .045)


def hits(d, limit):
    """Every surface crossing along the ray from the centre, nearest first, up to limit metres."""
    out, o, travelled = [], CENTRE.copy(), 0.0
    while travelled < limit:
        loc, nrm, idx, dist = TREE.ray_cast(o, d, limit - travelled)
        if loc is None:
            break
        travelled += dist
        out.append((travelled, idx))
        o = loc + d * .0003
        travelled += .0003
    return out


def r_limit(ph):
    """The search radius: past the head's outer skin, short of the shoulders (a .105 m cylinder below the jaw)."""
    return .16 if ph >= 0 else min(.16, .105 / max(.2, math.cos(math.radians(ph))))


def sample():
    """The outermost skin crossing (the eyeball in the eye openings) within the search radius, the ear left out: the
    sockets, the mouth bag and the nostrils are inner surfaces a ray from the centre meets first."""
    radii, eye = [], []
    for ph in PHI:
        row, erow = [], []
        for th in THETA:
            hs = [(t, i) for t, i in hits(direction(th, ph), r_limit(ph)) if not (i < N_BODY and ear_face(i))]
            if hs:
                t, i = hs[-1]
                row.append(t / HEIGHT)
                erow.append('e' if i >= N_BODY else '.')
            else:
                row.append(None)
                erow.append('.')
        radii.append(row)
        eye.append(''.join(erow))
    # holes: the mouth seam, the ear's attachment (no hit) take the mean of the neighbouring values
    for _ in range(3):
        for a in range(len(PHI)):
            for b in range(len(THETA)):
                nb = [radii[a2][b2] for a2, b2 in ((a - 1, b), (a + 1, b), (a, b - 1), (a, b + 1))
                      if 0 <= a2 < len(PHI) and 0 <= b2 < len(THETA) and radii[a2][b2] is not None]
                if not nb:
                    continue
                m = sorted(nb)[len(nb) // 2]
                if radii[a][b] is None or radii[a][b] < .9 * m:
                    radii[a][b] = sum(nb) / len(nb)
    return [[round(r, 5) for r in row] for row in radii], eye


def landmarks():
    out = {}
    # eye: centre and radius of the eyeball helper; lid corners from the eyeball's visible cells
    ev = sorted({i for f in FACES['helper-l-eye'] for i in f})
    ec = sum((P[i] for i in ev), Vector()) / len(ev)
    out['eyeball'] = to_polar(ec) + [round(max((P[i] - ec).length for i in ev) / HEIGHT, 5)]
    for k in ('l-upperlid', 'l-lowerlid', 'l-eye-target'):
        if k in JM:
            out[k] = to_polar(JM[k])
    # the profile line: forehead to chin on the centre line (y, z) normalized, every 2 mm
    prof = []
    y = CROWN_Y - .01
    while y > CHIN.y - .03:
        p = front_profile(y)
        if p is not None:
            prof.append([round((p.y - CHIN.y) / HEIGHT, 4), round((p.z - CENTRE.z) / HEIGHT, 4)])
        y -= .002
    out['profile'] = prof
    tip = max((front_profile(EYE.y - d * .001) for d in range(0, 70)), key=lambda p: p.z if p is not None else -1)
    out['nose_tip'] = to_polar(tip)
    out['chin'] = to_polar(CHIN)
    ear = [to_polar(P[i]) for i in sorted({i for k, f in enumerate(FACES['body']) if ear_face(k) for i in f})]
    out['ear_bounds'] = [[round(min(e[c] for e in ear), 3), round(max(e[c] for e in ear), 3)] for c in range(3)]
    return out


def main():
    radii, eye = sample()
    out = dict(
        provenance='MakeHuman 1.1 base.obj + universal-male-young-maxmuscle-averageweight.target + face modifiers '
                   '(CC0 1.0); see sample_head_reference.py',
        mods=MODS, height=round(HEIGHT, 5),
        centre_over_chin=round((CENTRE.y - CHIN.y) / HEIGHT, 5),
        theta=THETA, phi=PHI, radius=radii, eye=eye, landmarks=landmarks(),
    )
    s = json.dumps(out, separators=(',', ':'))
    print('head height', HEIGHT, 'centre', CENTRE, 'chin', CHIN, 'crown', CROWN_Y)
    if OUT:
        open(OUT, 'w').write(s + '\n')


main()
