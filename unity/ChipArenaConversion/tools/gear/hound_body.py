"""The Hound's starved body (owner 2026-11-03: "Make more monster like, modify model with blender if needed (still looks too much like
player model)"; docs/ZOMBIE-MODELS.md 4j): the pack body's own meshes, sculpted. Reads a bind-pose dump of a Hound's worn meshes
(the local tool's binddump with bindroot: rest.<renderer>.obj, rest.<renderer>.w, bind.txt) and writes, for each body part, every
vertex's new place and normal in the same order (the local bank Resources/ChipArenaHoundBody.bytes; Runtime ZombieBeastBody swaps
the Hound's meshes for copies carrying them, its skinning and blend shapes as they were). Nothing of the pack is committed.

The sculpt, in the meshes' bind space (metres, y up from the soles, z ahead), each vertex moved by its two heaviest bones' fields
in proportion to their weights, then the moves smoothed over the welded mesh (the parts' seams stay shut):
- limbs to sticks with knobbed joints: the upper arm and forearm pulled in to .5 of their girth between the joints, the thigh and
  calf to .6, back to full at the elbow, wrist, knee and ankle;
- the trunk starved: the waist pinched, the belly sucked in under the ribs, the chest and seat flattened, the breasts gone;
- the bones showing: the spine's ridge down the back, the shoulder blades and the hip crests standing out, the collarbones;
- the neck thinned.

usage: blender --background --factory-startup --python tools/gear/hound_body.py -- --dump DIR --out ASSETS/LocalAssets [--preview DIR]
"""
import argparse
import math
import os
import struct
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'pack_bank'))
BANK = 'ChipArenaHoundBody'
RESOURCES = 'ChipArenaLocal/Resources'


def read_obj(path):
    vs, fs = [], []
    with open(path) as fh:
        for line in fh:
            if line.startswith('v '):
                vs.append([float(x) for x in line.split()[1:4]])
            elif line.startswith('f '):
                fs.append([int(x.split('/')[0]) - 1 for x in line.split()[1:4]])
    return np.array(vs, dtype=np.float64), np.array(fs, dtype=np.int64)


def read_weights(path):
    out = []
    with open(path) as fh:
        for line in fh:
            a = line.split()
            out.append((int(a[0]), float(a[1]), int(a[2]), float(a[3])))
    return out


def read_bind(path):
    """{renderer: [(bone, 4x4 bindpose)]} in the bones' order."""
    out = {}
    with open(path) as fh:
        for line in fh:
            a = line.split()
            if len(a) != 18 or a[1] == 'transform':
                continue
            m = np.array([float(x) for x in a[2:]]).reshape(4, 4)
            out.setdefault(a[0], []).append((a[1], m))
    return out


def bell(x, c, w):
    return math.exp(-((x - c) / w) ** 2)


def smooth01(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


class Sculpt:
    def __init__(self, bones):
        self.P = {n: np.linalg.inv(m)[:3, 3] for n, m in bones}
        P = self.P
        self.limbs = {}
        for s in ('l', 'r'):
            for name, a, b, thin in (('upperarm', 'upperarm', 'lowerarm', .5), ('lowerarm', 'lowerarm', 'hand', .5),
                                     ('thigh', 'thigh', 'calf', .62), ('calf', 'calf', 'foot', .6)):
                seg = (P[a + '_' + s], P[b + '_' + s], thin)
                for n in P:
                    if n == name + '_' + s or (n.startswith(name + '_twist') and n.endswith('_' + s)) or (n.startswith(name + '_twist') and n.endswith(s + '1')):
                        self.limbs[n] = seg
        axis = sorted([P[n] for n in ('pelvis', 'spine_01', 'spine_02', 'spine_03', 'spine_04', 'spine_05', 'neck_01') if n in P], key=lambda p: p[1])
        self.axis = axis

    def spine_at(self, y):
        a = self.axis
        if y <= a[0][1]:
            return a[0]
        for i in range(len(a) - 1):
            if y <= a[i + 1][1]:
                t = (y - a[i][1]) / max(1e-6, a[i + 1][1] - a[i][1])
                return a[i] + (a[i + 1] - a[i]) * t
        return a[-1]

    def move(self, bone, p):
        """The field of `bone` at `p` (bind space): the move it asks for."""
        if bone in self.limbs:
            A, B, thin = self.limbs[bone]
            ab = B - A
            t = max(0.0, min(1.0, float(np.dot(p - A, ab) / max(1e-9, np.dot(ab, ab)))))
            c = A + ab * t
            knob = max(bell(t, 0, .16), bell(t, 1, .16))
            f = thin + (1 - thin) * knob
            return (c + (p - c) * f) - p
        if bone.startswith('neck'):
            A, B = self.P.get('neck_01', p), self.P.get('head', p)
            ab = B - A
            t = max(0.0, min(1.0, float(np.dot(p - A, ab) / max(1e-9, np.dot(ab, ab)))))
            c = A + ab * t
            return (c + (p - c) * .8) - p
        if bone.startswith('breast'):
            a = self.spine_at(p[1])
            q = p.copy()
            q[2] = a[2] + (p[2] - a[2]) * .82
            return q - p
        if bone in ('pelvis', 'spine_01', 'spine_02', 'spine_03', 'spine_04', 'spine_05') or bone.startswith('clavicle'):
            a = self.spine_at(p[1])
            x, y, z = p[0] - a[0], p[1], p[2] - a[2]
            nx, nz = x, z
            ax = abs(x)
            s = 1 if x >= 0 else -1
            # the waist pinched, the belly sucked in under the ribs, the chest and the seat flattened
            nx *= 1 - .2 * bell(y, 1.02, .06) - .06 * bell(y, 1.22, .12)
            if z > 0:
                nz *= 1 - .45 * bell(y, 1.0, .06) - .04 * bell(y, 1.25, .1)
            elif y < .98:
                nz *= .7
            # the spine's ridge, the shoulder blades, the hip crests, the collarbones
            if z < 0:
                nz -= .011 * math.exp(-(x / .022) ** 2) * bell(y, 1.18, .24)
                nz += .004 * math.exp(-((ax - .04) / .015) ** 2) * bell(y, 1.15, .2)
                nz -= .017 * math.exp(-((ax - .085) / .045) ** 2 - ((y - 1.32) / .065) ** 2)
            nx += s * .012 * bell(y, .985, .03) * math.exp(-((ax - .11) / .03) ** 2)
            if z > 0:
                nz += .007 * bell(y, 1.425, .016) * smooth01(.02, .05, ax) * (1 - smooth01(.12, .16, ax))
            return np.array([nx - x, 0.0, nz - z])
        return np.zeros(3)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument('--dump', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--preview')
    a = ap.parse_args(argv)
    binds = read_bind(os.path.join(a.dump, 'bind.txt'))
    parts = sorted(n[len('rest.'):-len('.obj')] for n in os.listdir(a.dump) if n.startswith('rest.Body.') and n.endswith('.obj'))
    meshes = []
    for name in parts:
        v, f = read_obj(os.path.join(a.dump, 'rest.' + name + '.obj'))
        w = read_weights(os.path.join(a.dump, 'rest.' + name + '.w'))
        meshes.append((name, v, f, w, [b for b, _ in binds[name]]))
    sculpt = Sculpt(binds[parts[0]])
    # Weld every part's vertices by place: one move per place, so the seams stay shut.
    key_of, places = {}, []
    index = []
    for name, v, f, w, bones in meshes:
        ids = []
        for p in v:
            k = (round(p[0], 5), round(p[1], 5), round(p[2], 5))
            if k not in key_of:
                key_of[k] = len(places)
                places.append(p)
            ids.append(key_of[k])
        index.append(np.array(ids))
    places = np.array(places)
    moves = np.zeros_like(places)
    seen = np.zeros(len(places))
    for (name, v, f, w, bones), ids in zip(meshes, index):
        for i, (b0, w0, b1, w1) in enumerate(w):
            p = v[i]
            d = sculpt.move(bones[b0], p) * w0 + (sculpt.move(bones[b1], p) * w1 if w1 > 0 else 0)
            tot = w0 + w1
            moves[ids[i]] += d / max(1e-6, tot)
            seen[ids[i]] += 1
    moves /= np.maximum(1, seen)[:, None]
    # Smooth the moves over the welded mesh's edges.
    nbr = [set() for _ in places]
    for (name, v, f, w, bones), ids in zip(meshes, index):
        for t in f:
            a0, b0, c0 = ids[t[0]], ids[t[1]], ids[t[2]]
            nbr[a0].update((b0, c0))
            nbr[b0].update((a0, c0))
            nbr[c0].update((a0, b0))
    for _ in range(3):
        avg = np.array([moves[list(n)].mean(axis=0) if n else moves[i] for i, n in enumerate(nbr)])
        moves = moves * .5 + avg * .5
    shaped = places + moves
    # Normals on the welded mesh (area-weighted), so the parts' seams and the UV splits shade as one.
    normals = np.zeros_like(places)
    for (name, v, f, w, bones), ids in zip(meshes, index):
        for t in f:
            a0, b0, c0 = ids[t[0]], ids[t[1]], ids[t[2]]
            n = np.cross(shaped[b0] - shaped[a0], shaped[c0] - shaped[a0])
            normals[a0] += n
            normals[b0] += n
            normals[c0] += n
    ln = np.linalg.norm(normals, axis=1)
    normals /= np.maximum(1e-9, ln)[:, None]
    # Unity's meshes are left-handed: the dump's winding is Unity's, so the cross above points inward; turn it out.
    centre = places.mean(axis=0)
    # (A sign check against the rest normals' outward sense: most face away from the trunk's axis.)
    out = np.einsum('ij,ij->i', normals, places - np.array([0, 0, 0]) * 0 - np.array([centre[0], places[:, 1].mean(), centre[2]]))
    if (out < 0).mean() > .5:
        normals = -normals
    blob = bytearray(b'HBDY')
    blob += struct.pack('<ii', 1, len(meshes))
    for (name, v, f, w, bones), ids in zip(meshes, index):
        nb = name.encode('utf-8')
        blob += struct.pack('<H', len(nb)) + nb + struct.pack('<i', len(v))
        blob += np.asarray(shaped[ids], dtype='<f4').tobytes()
        blob += np.asarray(normals[ids], dtype='<f4').tobytes()
    folder = os.path.join(a.out, RESOURCES)
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, BANK + '.bytes')
    with open(path, 'wb') as fh:
        fh.write(bytes(blob))
    meta = path + '.meta'
    if not os.path.exists(meta):
        from build_pack_bank import bytes_meta
        with open(meta, 'w', newline='\n') as fh:
            fh.write(bytes_meta(RESOURCES + '/' + BANK + '.bytes'))
    mv = np.linalg.norm(moves, axis=1)
    print('hound body: %d parts, %d places, moves mean %.4f max %.4f, wrote %s (%d bytes)' % (len(meshes), len(places), mv.mean(), mv.max(), path, len(blob)))
    if a.preview:
        preview(meshes, index, places, shaped, a.preview)


def preview(meshes, index, places, shaped, out_dir):
    import bpy
    from mathutils import Vector
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    scene = bpy.context.scene
    tris = []
    for (name, v, f, w, bones), ids in zip(meshes, index):
        for t in f:
            tris.append((int(ids[t[0]]), int(ids[t[2]]), int(ids[t[1]])))
    for label, pts, dx in (('before', places, -.7), ('after', shaped, .7)):
        me = bpy.data.meshes.new(label)
        me.from_pydata([(float(p[0]) + dx, float(-p[2]), float(p[1])) for p in pts], [], tris)
        for poly in me.polygons:
            poly.use_smooth = True
        ob = bpy.data.objects.new(label, me)
        scene.collection.objects.link(ob)
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.render.resolution_x, scene.render.resolution_y = 960, 720
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.lens = 50
    os.makedirs(out_dir, exist_ok=True)
    for yaw, lab in ((0, 'front'), (90, 'side'), (180, 'back'), (35, 'tq')):
        r = math.radians(yaw)
        target = Vector((0, 0, .85))
        cam.location = target + Vector((math.sin(r) * 4.2, -math.cos(r) * 4.2, .3))
        cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out_dir, 'body_%s.png' % lab)
        bpy.ops.render.render(write_still=True)


if __name__ == '__main__':
    main()
