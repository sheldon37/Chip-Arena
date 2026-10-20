"""The low-poly male base export (Art/Characters/male-base, Phase B of docs/CHARACTER-LOWPOLY.md): the binary mesh
library parses and matches its manifest and hash; its first 22 bones are CapturedRig.Bones exactly (names, order, parents
and rest offsets, read from Core/CapturedRig.cs; identity rest rotations), and every other bone is an optional,
secondary, `sec-` bone with a fallback joint; weights have at most 4 influences summing to 1 (1/255 units) and never use
motion-root; every vertex carries a hide region and its ragdoll death region (each hide region inside one part); the
triangle budgets hold; faces face out (stored normals agree with the winding, the body's signed volume is positive); the
soles sit on the floor, the head centre is near 1.50 m and the body stays inside the movement capsule; and the wrist
device rides the right wrist only. Static file checks only: no Unity import, no Blender run. Skips in a packaged source
tree, which has no Art folder."""
import hashlib
import json
import math
import re
import struct
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'Art/Characters/male-base'
EXPORTS = ART / 'exports'
MANIFEST = EXPORTS / 'male_base.manifest.json'
RIG_SOURCE = ROOT / 'Assets/ChipArena/Core/CapturedRig.cs'
PART_ROOTS = (1, 2, 5, 7, 8, 11, 12, 14, 15, 18, 19)


def captured_rig_bones():
    """(name, parent index, (x, y, z) source-frame offset) per bone, evaluated from CapturedRig.MakeBones."""
    src = RIG_SOURCE.read_text()
    consts = dict((k, int(v)) for k, v in re.findall(r'(\w+)=(\d+)', re.search(r'public const int ([^;]+);', src).group(1)))
    body = re.search(r'private static Bone\[\] MakeBones\(\)\s*\{(.*?)\n        \}', src, re.S).group(1)
    bones = []

    def num(expr, env):
        expr = re.sub(r'(\d*\.?\d+)f\b', r'\1', expr)
        return eval(expr, {}, env)  # literal arithmetic on the rig's own constants only

    def add(args, env):
        parts = [a.strip() for a in re.split(r',(?![^()]*\))', args)]
        name = num(parts[0].replace('+', ' + '), env)
        vals = [num(p, env) for p in parts[1:]] + [0] * (5 - len(parts))
        bones.append((name, int(vals[0]), (float(vals[1]), float(vals[2]), float(vals[3]))))

    blocks = re.split(r'foreach\(bool left in new\[\]\{true,false\}\)', body)
    for k, block in enumerate(blocks):
        calls = re.findall(r'\bAdd\(((?:"|side)[^;]*?)\);', block)
        if k == 0:
            for c in calls:
                add(c, dict(consts))
            continue
        for left in (True, False):
            env = dict(consts)
            env.update(side='left' if left else 'right', sign=1 if left else -1)
            for var in re.findall(r'int (\w+)=b\.Count', block):
                env[var] = len(bones)
            for c in calls:
                add(c, env)
    return bones


def load():
    doc = json.loads(MANIFEST.read_text())
    data = (EXPORTS / doc['binary']['file']).read_bytes()
    return doc, data


def parse(data):
    o = 0

    def take(fmt):
        nonlocal o
        v = struct.unpack_from('<' + fmt, data, o)
        o += struct.calcsize('<' + fmt)
        return v

    magic = data[:4]
    o = 4
    version, flags = take('HH')
    nb, npc, nv, nt = take('HHII')
    bones = []
    for _ in range(nb):
        (n,) = take('B')
        name = data[o:o + n].decode('ascii')
        o += n
        parent, bflags = take('hB')
        rest = take('3f')
        bones.append(dict(name=name, parent=parent, flags=bflags, rest=rest))
    pieces = []
    for _ in range(npc):
        (n,) = take('B')
        name = data[o:o + n].decode('ascii')
        o += n
        v0, vn, t0, tn = take('IIII')
        pieces.append(dict(name=name, v0=v0, vn=vn, t0=t0, tn=tn))
    pos = [take('3i') for _ in range(nv)]
    nrm = [take('3h') for _ in range(nv)]
    col = [take('4B') for _ in range(nv)]
    skin = [(take('4B'), take('4B')) for _ in range(nv)]
    reg = [take('BB') for _ in range(nv)]
    tri = [take('3H') for _ in range(nt)]
    slot = list(data[o:o + nt])
    o += nt
    return dict(magic=magic, version=version, bones=bones, pieces=pieces, pos=pos, nrm=nrm, col=col, skin=skin,
                reg=reg, tri=tri, slot=slot, end=o)


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


@unittest.skipUnless(MANIFEST.exists(), 'the Art folder is not in a packaged source tree')
class LowPolyMaleBase(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.doc, cls.data = load()
        cls.m = parse(cls.data)

    def test_binary_parses_and_matches_the_manifest(self):
        m, doc = self.m, self.doc
        self.assertEqual(m['magic'], b'CALP')
        self.assertEqual(m['version'], doc['version'])
        self.assertEqual(m['end'], len(self.data), 'no trailing bytes')
        self.assertEqual(hashlib.sha256(self.data).hexdigest(), doc['binary']['sha256'])
        self.assertEqual(len(self.data), doc['binary']['bytes'])
        self.assertLess(len(self.data), 1024 * 1024, 'the doc keeps each binary under 1 MB')
        self.assertEqual([p['name'] for p in m['pieces']], [p['name'] for p in doc['pieces']])
        for p, q in zip(m['pieces'], doc['pieces']):
            self.assertEqual((p['vn'], p['tn']), (q['vertices'], q['triangles']), p['name'])
        for f in ('male_base.fbx', 'male_base.glb'):
            self.assertTrue((EXPORTS / f).is_file(), f)
            self.assertLess((EXPORTS / f).stat().st_size, 2 * 1024 * 1024, f)

    def test_first_22_bones_are_captured_rig(self):
        rig = captured_rig_bones()
        self.assertEqual(len(rig), 22)
        bones = self.m['bones']
        man = self.doc['skeleton']['bones']
        for i, (name, parent, off) in enumerate(rig):
            self.assertEqual(bones[i]['name'], name)
            self.assertEqual(bones[i]['parent'], parent, name)
            want = (-off[0], off[1], off[2])           # Unity frame: source x mirrored
            for a, b in zip(bones[i]['rest'], want):
                self.assertAlmostEqual(a, b, places=5, msg=name)
            self.assertEqual(man[i]['restRotation'], [0.0, 0.0, 0.0, 1.0], name)
            self.assertTrue(man[i]['game'], name)
        self.assertFalse(bones[0]['flags'] & 2, 'motion-root does not deform')

    def test_extra_bones_are_optional_secondary_bones(self):
        bones = self.m['bones']
        man = self.doc['skeleton']['bones']
        names = [b['name'] for b in bones]
        for i in range(22, len(bones)):
            b, e = bones[i], man[i]
            self.assertTrue(b['name'].startswith('sec-'), b['name'])
            self.assertTrue(b['flags'] & 1 and e['secondary'], b['name'])
            self.assertIn(e['fallback'], ('head', 'right-wrist'), b['name'])
            # the chain hangs under its fallback joint
            j = b['parent']
            while j >= 22:
                j = bones[j]['parent']
            self.assertEqual(names[j], e['fallback'], b['name'])
        chains = self.doc['springs']['chains']
        self.assertTrue(3 <= len(chains) <= 6)
        for c, sp in chains.items():
            self.assertTrue(2 <= len(sp['bones']) <= 3, c)
            for k in ('stiffness', 'damping', 'gravity', 'drag', 'maxAngle', 'colliders'):
                self.assertIn(k, sp, c)
            self.assertTrue(0 < sp['maxAngle'] <= 25 and 0 < sp['damping'] <= 1, c)
            for b in sp['bones']:
                self.assertIn(b, names)

    def test_weights_are_normalized_with_at_most_four_influences(self):
        nb = len(self.m['bones'])
        for vi, (idx, w) in enumerate(self.m['skin']):
            self.assertEqual(sum(w), 255, vi)
            used = [(i, c) for i, c in zip(idx, w) if c > 0]
            self.assertTrue(1 <= len(used) <= 4, vi)
            self.assertEqual(len(set(i for i, _ in used)), len(used), vi)
            for i, _ in used:
                self.assertTrue(0 < i < nb, 'no weight on motion-root, valid index (%d)' % vi)

    def test_every_vertex_has_a_hide_and_death_region(self):
        hide = self.doc['regions']['hide']
        h2d = self.doc['regions']['hideToDeath']
        for vi, (h, d) in enumerate(self.m['reg']):
            self.assertLess(h, len(hide), vi)
            self.assertIn(d, PART_ROOTS, vi)
            self.assertEqual(h2d[hide[h]], d, 'hide region %s inside its ragdoll part (vertex %d)' % (hide[h], vi))
        seen = {hide[h] for h, _ in self.m['reg']}
        for name in ('scalp', 'face', 'neck', 'chest', 'abdomen', 'back', 'pelvis', 'hand-right', 'foot-left'):
            self.assertIn(name, seen)

    def test_triangle_budgets(self):
        tri = {p['name']: p['tn'] for p in self.m['pieces']}
        self.assertLessEqual(sum(tri.values()), 6000, 'LOD0 budget (doc h)')
        self.assertTrue(3500 <= tri['body'] <= 7000, tri['body'])
        self.assertLessEqual(tri['hair'], 1500)
        self.assertLessEqual(tri['core'], 300)
        self.assertLessEqual(tri['device'] + tri['device-lid'] + tri['device-chip'], 600)

    def test_faces_face_out(self):
        P = [(x * 1e-5, y * 1e-5, z * 1e-5) for x, y, z in self.m['pos']]
        for p in self.m['pieces']:
            bad = 0
            vol = 0.0
            for a, b, c in self.m['tri'][p['t0']:p['t0'] + p['tn']]:
                n = cross(sub(P[b], P[a]), sub(P[c], P[a]))
                L = math.sqrt(dot(n, n))
                if L < 1e-12:
                    continue
                s = tuple(x / 32767 for x in self.m['nrm'][a])
                if dot(n, s) / L < .99:
                    bad += 1
                vol += dot(P[a], cross(P[b], P[c])) / 6
            self.assertEqual(bad, 0, '%s: normals disagree with the winding' % p['name'])
            if p['name'] == 'body':
                self.assertGreater(vol, .05, 'the body encloses a positive volume (outward faces)')

    def test_fits_the_game_envelope(self):
        P = [(x * 1e-5, y * 1e-5 + .865, z * 1e-5) for x, y, z in self.m['pos']]
        body = self.m['pieces'][0]
        hide = self.doc['regions']['hide']
        bp = P[body['v0']:body['v0'] + body['vn']]
        self.assertAlmostEqual(min(p[1] for p in bp), 0.0, delta=.01, msg='soles on the floor')
        head = [P[body['v0'] + i] for i in range(body['vn']) if hide[self.m['reg'][body['v0'] + i][0]] in ('scalp', 'face')]
        centre = (min(p[1] for p in head) + max(p[1] for p in head)) / 2
        self.assertAlmostEqual(centre, 1.50, delta=.03, msg='head centre (hit sphere at 1.51 m)')
        for p in self.m['pieces']:
            reach = .42 + (.25 if p['name'] == 'hair' else .03)
            for q in P[p['v0']:p['v0'] + p['vn']]:
                self.assertLessEqual(math.hypot(q[0], q[2]), reach, p['name'])

    def test_wrist_device_on_the_right_wrist_only(self):
        names = [b['name'] for b in self.m['bones']]
        wrist = self.doc['skeleton']['bones'][names.index('right-wrist')]['restWorld']
        for p in self.m['pieces']:
            if not p['name'].startswith('device'):
                continue
            for vi in range(p['v0'], p['v0'] + p['vn']):
                idx, w = self.m['skin'][vi]
                for i, c in zip(idx, w):
                    if c:
                        self.assertIn(names[i], ('right-wrist', 'sec-device-lid', 'sec-device-chip'))
                x, y, z = (c * 1e-5 for c in self.m['pos'][vi])
                self.assertGreater(x, .25, 'Unity +x is the right side')
                self.assertLess(math.dist((x, y, z), wrist), .12)
        # and no device-like geometry on the left wrist: the body's left forearm and hand carry skin only
        hide = self.doc['regions']['hide']
        body = self.m['pieces'][0]
        for vi in range(body['v0'], body['v0'] + body['vn']):
            if hide[self.m['reg'][vi][0]] in ('forearm-left', 'hand-left'):
                self.assertEqual(self.m['col'][vi][3], 1, 'left forearm and hand are bare skin')

    def test_builder_and_exporter_are_the_committed_sources(self):
        for f in ('build_male_base.py', 'export_male_base.py', 'male_base.blend'):
            self.assertTrue((ART / f).is_file(), f)
        self.assertLess((ART / 'male_base.blend').stat().st_size, 5 * 1024 * 1024)
