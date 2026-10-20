"""Exports the low-poly male base from male_base.blend (built by build_male_base.py).

    blender -b --factory-startup unity/ChipArenaConversion/Art/Characters/male-base/male_base.blend \
        --python-exit-code 1 --python unity/ChipArenaConversion/Art/Characters/male-base/export_male_base.py \
        -- [--out <dir>]

Writes, deterministically (two runs are byte-identical):
  male_base.bytes          the runtime mesh library proposed in docs/CHARACTER-LOWPOLY.md (b): little-endian "CALP" v1,
                           Unity frame, motion-root origin, flat-shaded split vertices, <= 4 weights in 1/255,
                           hide and death regions, paint channel per vertex; layout in male_base.manifest.json
  male_base.manifest.json  bones (the 22 CapturedRig joints + optional secondary bones), bind poses, pieces, paints,
                           channels, regions, counts, hair springs, device hinge and states, attachment points
  male_base.fbx            preview/owner FBX (-Z forward, Y up, metres, transforms applied)
  male_base.glb            preview/owner glTF binary (custom attributes _HIDE and _DEATH)
Nothing here is imported by Unity: Phase D copies the binary and manifest into Resources/Characters.
"""
import datetime
import hashlib
import json
import math
import os
import struct
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build_male_base as M  # noqa: E402  (tables and constants only; its main() does not run on import)

ARGV = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.abspath(ARGV[ARGV.index('--out') + 1]) if '--out' in ARGV else os.path.join(HERE, 'exports')
PIECES = [('body', 'CA_Body'), ('hair', 'CA_Hair'), ('core', 'CA_Core'), ('device', 'CA_WristDevice'),
          ('device-lid', 'CA_WristLid'), ('device-chip', 'CA_WristChip')]
Q = 1e-5        # position quantum: 0.01 mm
VERSION = 1


def r6(x):
    return float('%.6f' % x)


def to_unity(v):
    """Blender -> Unity frame (x = -rig x, y up, z forward)."""
    return (-v[0], v[2], -v[1])


def rig_to_unity(p):
    return (-p[0], p[1], p[2])


def bone_table(rig):
    """Bones in export order: the 22 CapturedRig joints, then the secondary bones sorted by name."""
    names = list(M.BONE_NAMES)
    sec = sorted(b.name for b in rig.data.bones if b.name not in M.JOINT_INDEX)
    names += sec
    out = []
    for i, n in enumerate(names):
        b = rig.data.bones[n]
        head = M.rig_pos(b.head_local)                 # rig space, floor origin
        par = b.parent.name if b.parent else None
        ph = M.rig_pos(b.parent.head_local) if b.parent else Vector((0, 0, 0))
        local = head - ph
        if n in M.JOINT_INDEX:                         # the spec's offsets exactly (CapturedRig.Bones)
            local = Vector(M.OFFSET[n])
        entry = dict(index=i, name=n, parent=names.index(par) if par else -1,
                     restLocal=[r6(c) for c in rig_to_unity(local)],
                     restWorld=[r6(c) for c in rig_to_unity((head - Vector((0, M.ROOT_HEIGHT, 0))))],
                     restRotation=[0.0, 0.0, 0.0, 1.0], deform=bool(b.use_deform),
                     game=n in M.JOINT_INDEX)
        if n not in M.JOINT_INDEX:
            entry['secondary'] = True
            entry['kind'] = b.get('ca_kind', '')
            entry['fallback'] = 'head' if n.startswith('sec-hair') else 'right-wrist'
            if b.get('ca_chain'):
                entry['chain'] = b['ca_chain']
        out.append(entry)
    return out, names


def piece_data(obj, names):
    me = obj.data
    paint = [d.value for d in me.attributes['ca_paint'].data]
    hide = [d.value for d in me.attributes['ca_hide'].data]
    death = [d.value for d in me.attributes['ca_death'].data]
    gname = {g.index: g.name for g in obj.vertex_groups}
    bidx = {n: i for i, n in enumerate(names)}
    vw = []
    for v in me.vertices:
        ws = sorted(((gname[g.group], g.weight) for g in v.groups if g.weight > 0), key=lambda kv: (-kv[1], kv[0]))[:4]
        q = [(bidx[n], int(round(w * 255))) for n, w in ws]
        q = [(b, c) for b, c in q if c > 0]
        q[0] = (q[0][0], q[0][1] + 255 - sum(c for _, c in q))
        q += [(0, 0)] * (4 - len(q))
        vw.append(q)
    verts, index, tri_slot, keymap = [], [], [], {}
    for poly in me.polygons:
        if len(poly.vertices) != 3:
            raise RuntimeError('%s: face %d is not a triangle' % (obj.name, poly.index))
        n = to_unity(poly.normal)
        nq = tuple(int(round(max(-1, min(1, c)) * 32767)) for c in n)
        name, rgb, ch = M.PAINTS[paint[poly.index]]
        col = tuple(int(round(c * 255)) for c in rgb) + (ch,)
        tri = []
        for vi in poly.vertices:
            p = to_unity(me.vertices[vi].co)
            pq = (int(round(p[0] / Q)), int(round((p[1] - M.ROOT_HEIGHT) / Q)), int(round(p[2] / Q)))
            key = (pq, nq, col, tuple(vw[vi]), hide[vi], death[vi])
            if key not in keymap:
                keymap[key] = len(verts)
                verts.append(key)
            tri.append(keymap[key])
        # Unity's frame mirrors x: swap two corners so cross(b - a, c - a) faces out (the bank's convention)
        index.append((tri[0], tri[2], tri[1]))
        tri_slot.append(poly.material_index)
    return verts, index, tri_slot


def write_binary(path, bones, pieces):
    out = bytearray()
    out += b'CALP' + struct.pack('<HH', VERSION, 0)
    nv = sum(len(p['verts']) for p in pieces)
    nt = sum(len(p['index']) for p in pieces)
    out += struct.pack('<HHII', len(bones), len(pieces), nv, nt)
    for b in bones:
        nm = b['name'].encode('ascii')
        flags = (1 if b['secondary'] else 0) if 'secondary' in b else 0
        flags |= 2 if b['deform'] else 0
        out += struct.pack('<B', len(nm)) + nm + struct.pack('<hB', b['parent'], flags) + struct.pack('<3f', *b['restLocal'])
    v0 = t0 = 0
    for p in pieces:
        nm = p['name'].encode('ascii')
        out += struct.pack('<B', len(nm)) + nm + struct.pack('<IIII', v0, len(p['verts']), t0, len(p['index']))
        v0 += len(p['verts'])
        t0 += len(p['index'])
    allv = [v for p in pieces for v in p['verts']]
    for (pq, nq, col, w, h, d) in allv:
        out += struct.pack('<3i', *pq)
    for (pq, nq, col, w, h, d) in allv:
        out += struct.pack('<3h', *nq)
    for (pq, nq, col, w, h, d) in allv:
        out += struct.pack('<4B', *col)
    for (pq, nq, col, w, h, d) in allv:
        out += struct.pack('<4B', *[b for b, _ in w]) + struct.pack('<4B', *[c for _, c in w])
    for (pq, nq, col, w, h, d) in allv:
        out += struct.pack('<BB', h, d)
    base = 0
    for p in pieces:
        for tri in p['index']:
            out += struct.pack('<3H', *[base + i for i in tri])
        base += len(p['verts'])
    for p in pieces:
        out += bytes(p['slot'])
    with open(path, 'wb') as f:
        f.write(out)
    return bytes(out)


def sha(path):
    with open(path, 'rb') as f:
        return hashlib.sha256(f.read()).hexdigest()


def manifest(bones, pieces, binary, rig):
    counts = {p['name']: dict(triangles=len(p['index']), vertices=len(p['verts'])) for p in pieces}
    total = sum(c['triangles'] for c in counts.values())
    springs = {}
    for c in M.HAIR_CHAINS:
        sp = dict(M.SPRING[c])
        sp['bones'] = ['sec-hair-%s-1' % c, 'sec-hair-%s-2' % c]
        sp['colliders'] = [dict(kind='sphere', joint='head', centre=[0.0, .16, -.005], radius=.118),
                           dict(kind='capsule', joint='neck', a=[0.0, 0.0, 0.0], b=[0.0, .1, 0.0], radius=.07)]
        springs[c] = sp
    lid = rig.data.bones['sec-device-lid']
    hinge = M.rig_pos(lid.head_local) - M.J['right-wrist']
    chip = rig.data.bones['sec-device-chip']
    seat = M.rig_pos(chip.head_local) - M.J['right-wrist']
    off, tilt = M.CHIP_APPROACH
    core = rig  # placeholder for readability below
    states = [
        dict(state='closed', lid=0.0, chip='hidden', glow=dict(device=.25, lens=0.0)),
        dict(state='opening', lid=[0.0, M.LID_OPEN_DEG], seconds=.25, chip='hidden', glow=dict(device=.9, lens=0.0)),
        dict(state='insertion', lid=M.LID_OPEN_DEG, chip=[0.0, 1.0], seconds=.30, glow=dict(device=.9, lens=0.0)),
        dict(state='accepted', lid=[M.LID_OPEN_DEG, 0.0], seconds=.20, chip='seated',
             glow=dict(device=2.4, lens=2.0, tint='chip rarity colour')),
        dict(state='activated', lid=0.0, chip='seated', glow=dict(device=[1.2, .25], lens=[1.1, 0.0], decaySeconds=1.5)),
    ]
    doc = dict(
        format='CALP', version=VERSION, id='male-base', bodyType='m', hair=M.HAIR_ID,
        notes='Phase B proof of concept (docs/CHARACTER-LOWPOLY.md). Not imported by Unity yet.',
        frame=dict(units='metres', axes='Unity: x = -rig x (a left limb sits at -x), y up, z forward',
                   origin='motion-root at rest; the floor is y = %.3f' % -M.ROOT_HEIGHT,
                   winding='cross(b - a, c - a) faces out', normals='flat, one per triangle'),
        blender=bpy.app.version_string,
        binary=dict(file=os.path.basename(binary), bytes=os.path.getsize(binary), sha256=sha(binary),
                    layout=['header: "CALP", u16 version, u16 flags, u16 bones, u16 pieces, u32 vertices, u32 triangles',
                            'bones: u8 name length, name, i16 parent, u8 flags (1 secondary, 2 deform), 3 f32 rest local',
                            'pieces: u8 name length, name, u32 first vertex, u32 vertices, u32 first triangle, u32 triangles',
                            'positions: 3 i32 per vertex, 0.01 mm units', 'normals: 3 i16 per vertex (/32767)',
                            'colours: 4 u8 per vertex (sRGB paint, alpha = channel)',
                            'skin: 4 u8 bone indices then 4 u8 weights (sum 255) per vertex',
                            'regions: u8 hide region, u8 death region (part root joint) per vertex',
                            'triangles: 3 u16 per triangle (global vertex indices)', 'slots: u8 material slot per triangle']),
        skeleton=dict(matchesCapturedRig=True, joints=len(M.BONE_NAMES), bones=bones,
                      bindPose='bind(b) = inverse(translation(restWorld(b))); rest rotations are identity'),
        materials=[dict(slot=0, name='CA_Body', shading='flat, vertex colour x channel tint'),
                   dict(slot=1, name='CA_Glow', shading='emissive: vertex colour x glow tint x gain', emissive=True)],
        paints=[dict(index=i, name=n, srgb='#%02x%02x%02x' % tuple(int(round(c * 255)) for c in rgb), channel=ch,
                     slot=1 if ch in M.GLOW_CHANNELS else 0) for i, (n, rgb, ch) in enumerate(M.PAINTS)],
        channels={'0': 'fixed', '1': 'skin tone', '2': 'hair colour', '3': 'primary', '4': 'secondary (undergarment)',
                  '5': 'accent glow (wrist device, lid lens, chip)', '6': 'core glow', '7': 'team'},
        tint='albedo = paint * param / paint default for the paint channel; glow = paint * tint / default * gain',
        regions=dict(hide=M.HIDE_REGIONS, death={str(k): v for k, v in sorted(M.DEATH_NAMES.items())},
                     hideToDeath={k: v for k, v in M.HIDE_DEATH.items()}, coarse=M.COARSE,
                     note='death region = the ragdoll part root joint (NativeKillEffectRules.PartRoots); each hide region lies in one'),
        pieces=[dict(name=p['name'], object=p['object'], triangles=len(p['index']), vertices=len(p['verts']),
                     joint=p['joint'], permanent=p['name'] != 'hair', visibleByDefault=p['name'] != 'device-chip')
                for p in pieces],
        counts=dict(total=dict(triangles=total), budgets=dict(lod0=6000, bodyAndHead=[4000, 7000], hair=1500, core=300, device=600),
                    pieces=counts),
        springs=dict(chains=springs, solver='presentation only; see docs/CHARACTER-LOWPOLY.md "Secondary motion"',
                     units='stiffness Hz, damping ratio, gravity (x 9.81 m/s^2 x 0.1), drag 0..1, maxAngle degrees'),
        device=dict(joint='right-wrist',
                    lid=dict(bone='sec-device-lid', pivot=[r6(c) for c in rig_to_unity(hinge)], axis=[0.0, 0.0, 1.0],
                             openDegrees=M.LID_OPEN_DEG, note='opens outward: Unity frame, rotate about +z by +angle (source frame: -angle)'),
                    chip=dict(bone='sec-device-chip', seated=[r6(c) for c in rig_to_unity(seat)],
                              approachOffset=[r6(c) for c in rig_to_unity(off)], approachTiltDegrees=tilt,
                              note='insertion runs from the approach offset (Unity frame) and a +tilt about +z to seated',
                              size=[M.CHIP_W, M.CHIP_H, M.CHIP_T]),
                    states=states),
        attachments=dict(core=dict(joint='chest', at=[r6(c) for c in rig_to_unity(Vector((M.CORE_AT[0], M.CORE_AT[1], 0)) - M.J['chest'])]),
                         device=dict(joint='right-wrist'), hair=dict(joint='head')),
    )
    doc['severs'] = dict(note='stump-cap attachment points (doc f): cut plane through the joint (motion-root frame, Unity axes), '
                               'child part cross-section; the caps themselves are Phase D', points=sever_points(pieces[0]))
    return doc


SEVER_JOINTS = (2, 5, 7, 8, 11, 12, 14, 15, 18, 19)
CHILD = {2: 3, 5: None, 7: 8, 8: 9, 11: 12, 12: 13, 14: 15, 15: 16, 18: 19, 19: 20}


def sever_points(body_piece):
    """Stump-cap attachment points at the doc's sever joints (f): the cut plane through the joint along the limb axis,
    with the child part's measured cross-section there (centroid and mean radius of its first 5 cm past the cut)."""
    out = []
    verts = body_piece['verts']
    for j in SEVER_JOINTS:
        name = M.BONE_NAMES[j]
        at = M.J[name]
        c = CHILD[j]
        axis = (M.J[M.BONE_NAMES[c]] - at).normalized() if c is not None else Vector((0, 1, 0))
        part = j if j in M.PART_ROOTS else 2
        pts = []
        for (pq, nq, col, w, h, d) in verts:
            p = Vector((-pq[0] * Q, pq[1] * Q + M.ROOT_HEIGHT, pq[2] * Q))      # back to rig space
            if d != part:
                continue
            if 0 <= (p - at).dot(axis) <= .05:
                pts.append(p)
        cen = sum(pts, Vector()) / max(1, len(pts))
        rad = sum(((q - cen) - axis * (q - cen).dot(axis)).length for q in pts) / max(1, len(pts))
        out.append(dict(joint=name, index=j, plane=[r6(x) for x in rig_to_unity(at - Vector((0, M.ROOT_HEIGHT, 0)))],
                        normal=[r6(x) for x in rig_to_unity(axis)],
                        centroid=[r6(x) for x in rig_to_unity(cen - Vector((0, M.ROOT_HEIGHT, 0)))],
                        radius=r6(rad), samples=len(pts)))
    return out


def deterministic_fbx():
    """Blender's FBX writer stamps now() and derives node ids from Python's randomised hash(): pin both."""
    import io_scene_fbx.export_fbx_bin as efb
    import io_scene_fbx.fbx_utils as fu

    class _DT:
        @staticmethod
        def now():
            return datetime.datetime(2026, 10, 20, 0, 0, 0)

    class _Mod:
        datetime = _DT

    efb.datetime = _Mod

    def key_to_uuid(uuids, key):
        if isinstance(key, int) and 0 <= key < 2 ** 63:
            uuid = key
        else:
            uuid = int.from_bytes(hashlib.sha1(repr(key).encode()).digest()[:8], 'little') & (2 ** 62 - 1)
        while uuid in uuids:
            uuid = (uuid + 1) & (2 ** 62 - 1)
        return uuid

    fu._key_to_uuid = key_to_uuid
    efb._key_to_uuid = key_to_uuid if hasattr(efb, '_key_to_uuid') else None


def export_fbx(path):
    deterministic_fbx()
    for o in bpy.context.scene.objects:
        o.select_set(o.name.startswith('CA_'))
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'}, axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0, bake_space_transform=True,
        use_mesh_modifiers=False, mesh_smooth_type='FACE', use_triangles=True, use_custom_props=False,
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', use_armature_deform_only=False,
        armature_nodetype='NULL', bake_anim=False, path_mode='AUTO', embed_textures=False, colors_type='SRGB')


def export_glb(path):
    for _, name in PIECES:
        me = bpy.data.objects[name].data
        for src, dst in (('ca_hide', '_HIDE'), ('ca_death', '_DEATH')):
            vals = [d.value for d in me.attributes[src].data]
            a = me.attributes.get(dst) or me.attributes.new(dst, 'FLOAT', 'POINT')
            a.data.foreach_set('value', [float(v) for v in vals])
    for o in bpy.context.scene.objects:
        o.select_set(o.name.startswith('CA_'))
    bpy.ops.export_scene.gltf(
        filepath=path, export_format='GLB', use_selection=True, export_yup=True, export_apply=False,
        export_attributes=True, export_skins=True, export_animations=False, export_morph=False,
        export_normals=True, export_vertex_color='ACTIVE', export_texcoords=False, export_materials='EXPORT',
        export_extras=False, export_def_bones=False)


def main():
    os.makedirs(OUT, exist_ok=True)
    rig = bpy.data.objects['CA_Rig']
    bones, names = bone_table(rig)
    pieces = []
    for pname, oname in PIECES:
        obj = bpy.data.objects[oname]
        verts, index, slot = piece_data(obj, names)
        joint = {'body': 'skinned', 'hair': 'head', 'core': 'chest'}.get(pname, 'right-wrist')
        pieces.append(dict(name=pname, object=oname, verts=verts, index=index, slot=slot, joint=joint))
    binpath = os.path.join(OUT, 'male_base.bytes')
    write_binary(binpath, bones, pieces)
    doc = manifest(bones, pieces, binpath, rig)
    with open(os.path.join(OUT, 'male_base.manifest.json'), 'w', newline='\n') as f:
        json.dump(doc, f, indent=1, sort_keys=False)
        f.write('\n')
    export_fbx(os.path.join(OUT, 'male_base.fbx'))
    export_glb(os.path.join(OUT, 'male_base.glb'))
    for n in ('male_base.bytes', 'male_base.manifest.json', 'male_base.fbx', 'male_base.glb'):
        p = os.path.join(OUT, n)
        print('EXPORT', n, os.path.getsize(p), sha(p))
    print('TRIANGLES', doc['counts']['total']['triangles'], {k: v['triangles'] for k, v in doc['counts']['pieces'].items()})


main()
