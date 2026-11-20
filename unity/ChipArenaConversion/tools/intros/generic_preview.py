"""generic_preview.py: an offline framing preview of the generic intros (review aid, Workbench, no game renderer).

  blender --background --factory-startup --python generic_preview.py -- VIEWS_DIR OUT_DIR [SET,...] [STEP[@FROM]] [fx]

Reads VIEWS_DIR/<set>.json (CoreTests --intro-generic with INTRO_GENERIC_VIEWS set: the camera, the keyed anchors and the set
pieces every 0.1 s, source frame) and renders every STEP-th frame with the set's Fine pieces placed as the intro places them and a
grey mannequin on the anchors (and with fx, the effects marked: yellow glows, cyan streaks, grey smoke), to
OUT_DIR/<set>/t<NNN>_<age>.png. Write the views with: INTRO_GENERIC_VIEWS=DIR dotnet run --project Tests/CoreTests.csproj -c Release
-- --intro-generic. The meshes come from tools/intros/generic_sets.py's modules.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', 'stages'))
import numpy as np  # noqa: E402
import stagekit as sk  # noqa: E402
import generic_kit as kit  # noqa: E402

argv = sys.argv[sys.argv.index('--') + 1:]
views, out = argv[0], argv[1]
only = argv[2].split(',') if len(argv) > 2 and argv[2] else ['cloudbreak', 'whiteout', 'slipstream', 'threshold', 'flashbulb']
step_arg = argv[3] if len(argv) > 3 else '4'
step = int(step_arg.split('@')[0])
start = float(step_arg.split('@')[1]) if '@' in step_arg else 0.0   # STEP@FROM: from that age on (the closing frames)
fx_on = len(argv) > 4 and argv[4] == 'fx'

for ob in list(bpy.data.objects):
    bpy.data.objects.remove(ob, do_unlink=True)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'VERTEX'
sc.display.shading.show_cavity = False
sc.render.resolution_x, sc.render.resolution_y = 640, 360
sc.view_settings.view_transform = 'Standard'
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.sensor_fit = 'VERTICAL'
cam.data.clip_start, cam.data.clip_end = .05, 400


def mesh_of(name, g, bk):
    P, N, S, F = g.arrays()
    me = bpy.data.meshes.new(name)
    n = len(P)
    me.vertices.add(n)
    me.vertices.foreach_set('co', (P.astype(np.float32) * np.array([-1, 1, 1], np.float32)).ravel())
    me.loops.add(n)
    me.loops.foreach_set('vertex_index', np.arange(n, dtype=np.int32))
    me.polygons.add(len(S))
    starts = np.zeros(len(S), dtype=np.int32)
    if len(S) > 1:
        starts[1:] = np.cumsum(S[:-1])
    me.polygons.foreach_set('loop_start', starts)
    me.update()
    attr = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
    c = bk.colour.astype(np.float32) / 255.0
    lin = np.where(c[:, :3] <= .04045, c[:, :3] / 12.92, ((c[:, :3] + .055) / 1.055) ** 2.4)
    glow = bk.glow
    rgb = np.where(glow[:, None], lin * 1.0, lin * (.35 + .65 * c[:, 3:4]))
    attr.data.foreach_set('color', np.concatenate([rgb, np.ones((n, 1), np.float32)], axis=1).ravel())
    me.color_attributes.active_color = attr
    return me


def grey_mesh(name, rgb):
    me = bpy.data.meshes.new(name)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=1)
    tmp = bpy.context.active_object
    me = tmp.data.copy()
    bpy.data.objects.remove(tmp, do_unlink=True)
    attr = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
    attr.data.foreach_set('color', np.tile(np.array(list(rgb) + [1], np.float32), len(me.loops)))
    me.color_attributes.active_color = attr
    return me


limb_mesh = None
for key in only:
    path = os.path.join(views, key + '.json')
    if not os.path.exists(path):
        continue
    data = json.load(open(path, encoding='utf-8'))
    mod = __import__('generic_' + key)
    T = kit.tier(0)
    meshes = {}
    for p in mod.build(T):
        if not p.g.faces:
            continue
        bk = sk.Bake(p.g, None, floors=p.floors, seed=p.seed or 1)
        if p.recolour:
            for fins, fn in (p.recolour if isinstance(p.recolour, list) else [p.recolour]):
                kit.recolour(p.g, bk, fins, fn)
        meshes[p.name] = mesh_of(p.name, p.g, bk)
    pool = {}
    if limb_mesh is None:
        limb_mesh = grey_mesh('limb', (.85, .86, .9))
    fxm = [grey_mesh('fxglow', (1.0, .85, .3)), grey_mesh('fxstreak', (.3, .9, 1.0)), grey_mesh('fxsmoke', (.55, .55, .6))]
    fxpool = []
    limbs = [bpy.data.objects.new('limb%d' % i, limb_mesh) for i in range(14)]
    for o in limbs:
        sc.collection.objects.link(o)
    bones = [(0, 0, .13), (1, 2, .16), (3, 4, .06), (4, 5, .05), (6, 7, .06), (7, 8, .05), (2, 9, .08), (9, 10, .06), (2, 11, .08), (11, 12, .06), (1, 3, .07), (1, 6, .07)]
    os.makedirs(os.path.join(out, key), exist_ok=True)
    for fi, fr in enumerate(data['frames']):
        if fi % step or fr['age'] < start - 1e-3:
            continue
        used = {}
        for ob in sc.collection.objects:
            if ob.name.startswith('p:'):
                ob.hide_render = True
        for pc in fr['pieces']:
            name, x, y, z, qx, qy, qz, qw, s, glow = pc
            if name not in meshes:
                continue
            k = used.get(name, 0)
            used[name] = k + 1
            lst = pool.setdefault(name, [])
            if k >= len(lst):
                o = bpy.data.objects.new('p:%s:%d' % (name, k), meshes[name])
                sc.collection.objects.link(o)
                lst.append(o)
            o = lst[k]
            o.hide_render = False
            o.matrix_world = Matrix.Translation(Vector((x, y, z))) @ Quaternion((qw, qx, qy, qz)).to_matrix().to_4x4() @ Matrix.Scale(s, 4)
        A = [Vector(a) for a in fr['anchors']]
        shown = fr['solid'] > .5
        for i, o in enumerate(limbs):
            if i >= len(bones) or not shown:
                o.hide_render = True
                continue
            a, b, r = bones[i]
            pa, pb = A[a], A[b]
            if a == b:
                o.matrix_world = Matrix.Translation(pa) @ Matrix.Scale(r, 4)
            else:
                d = pb - pa
                L = d.length
                q = Vector((0, 0, 1)).rotation_difference(d.normalized()) if L > 1e-6 else Quaternion()
                o.matrix_world = Matrix.Translation((pa + pb) / 2) @ q.to_matrix().to_4x4() @ Matrix.Diagonal((r, r, max(L / 2, r), 1))
            o.hide_render = False
        for o in fxpool:
            o.hide_render = True
        if fx_on:
            for j, f in enumerate(fr.get('fx', [])[:700]):
                kind, x, y, z, size = int(f[0]), f[1], f[2], f[3], f[4]
                while len(fxpool) <= j:
                    o = bpy.data.objects.new('fx%d' % len(fxpool), fxm[0])
                    sc.collection.objects.link(o)
                    fxpool.append(o)
                o = fxpool[j]
                o.data = fxm[kind]
                p = Vector((x, y, z))
                if kind == 1:
                    a = Vector((f[8], f[9], f[10]))
                    L = a.length
                    r = max(.006, min(.08, size))
                    if L < 1e-5:
                        continue
                    q = Vector((0, 0, 1)).rotation_difference(a.normalized())
                    o.matrix_world = Matrix.Translation(p - a / 2) @ q.to_matrix().to_4x4() @ Matrix.Diagonal((r, r, L / 2, 1))
                else:
                    r = max(.02, min(.5, size * (.25 if kind == 0 else .35)))
                    o.matrix_world = Matrix.Translation(p) @ Matrix.Scale(r, 4)
                o.hide_render = False
        e, t = Vector(fr['eye']), Vector(fr['look'])
        fwd = (t - e).normalized()
        right = fwd.cross(Vector((0, 1, 0))).normalized()
        upv = right.cross(fwd)
        cam.matrix_world = Matrix.Translation(e) @ Matrix(((right.x, upv.x, -fwd.x), (right.y, upv.y, -fwd.y), (right.z, upv.z, -fwd.z))).to_4x4()
        cam.data.angle = math.radians(fr['fov'])
        sc.render.filepath = os.path.join(out, key, 't%03d_%05.2f.png' % (fi, fr['age']))
        bpy.ops.render.render(write_still=True)
    for lst in pool.values():
        for o in lst:
            bpy.data.objects.remove(o, do_unlink=True)
    for o in limbs + fxpool:
        bpy.data.objects.remove(o, do_unlink=True)
    print('rendered', key)
