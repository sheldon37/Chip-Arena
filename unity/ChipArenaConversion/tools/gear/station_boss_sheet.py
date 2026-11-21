"""A review sheet of THE 6:47 as the bank holds it (tools/gear/station_boss.py --sheet DIR): every joint's mesh placed in the standing
stance (Core NativeStationBossModel.Stance, the legs solved by two-bone IK with the concept's poles) and rendered with its vertex
colours (workbench) from the sides, the nose, the tail and above. Blender only; the game's own views are the local tool's."""
import math
import os

import bpy
from mathutils import Matrix, Vector

ROW_STANCE = ((3.55, 6.4), (4.6, .7), (4.3, -4.3))
PALM_LIFT = .14


def _stance(leg):
    s = -1 if leg & 1 == 0 else 1
    x, z = ROW_STANCE[leg >> 1]
    return Vector((s * x, PALM_LIFT, z))


def _pole(leg):
    s = -1 if leg & 1 == 0 else 1
    r = leg >> 1
    return Vector((s * .85, .6, -.15)).normalized() if r == 0 else Vector((s * .45, 1, -.2 if r == 2 else 0)).normalized()


def _frame(along, hinge):
    y = (-along).normalized()
    x = hinge - y * hinge.dot(y)
    x = x.normalized()
    z = x.cross(y)
    return Matrix((x, y, z)).transposed()


def pose(rest, joints):
    """Each joint's matrix in the boss's axes for the standing stance (rotation 3x3, position)."""
    world = {}
    for name, (parent, off) in joints.items():
        if parent is None:
            world[name] = (Matrix.Identity(3), Vector(off))
    # (The joints' table is parents first.)
    for name, (parent, off) in joints.items():
        if parent is not None:
            rp, tp = world[parent]
            world[name] = (rp.copy(), tp + rp @ Vector(off))
    names = ['front', 'mid', 'rear']
    thigh = [2.6, 2.6, 2.7, 2.7, 2.7, 2.7]
    shin = [3.3, 3.3, 3.5, 3.5, 3.5, 3.5]
    for leg in range(6):
        s = 'l' if leg & 1 == 0 else 'r'
        n = s + '-' + names[leg >> 1]
        hip = world['hip-' + n][1]
        target = _stance(leg)
        l1, l2 = thigh[leg], shin[leg]
        d = target - hip
        dist = max(abs(l1 - l2) + .02, min(l1 + l2 - .02, d.length))
        dr = d.normalized()
        a = (l1 * l1 + dist * dist - l2 * l2) / (2 * dist)
        up = math.sqrt(max(0.0, l1 * l1 - a * a))
        pole = _pole(leg)
        pp = (pole - dr * pole.dot(dr)).normalized()
        knee = hip + dr * a + pp * up
        foot = hip + dr * dist
        hinge = (knee - hip).cross(foot - knee)
        ru = _frame(knee - hip, hinge)
        rl = _frame(foot - knee, hinge)
        yaw = (-1 if leg & 1 == 0 else 1) * (35 if leg >> 1 == 0 else 90 if leg >> 1 == 1 else 145)
        world['hip-' + n] = (ru, hip)
        world['knee-' + n] = (rl, knee)
        world['foot-' + n] = (Matrix.Rotation(math.radians(yaw), 3, 'Y'), foot)
    return world


def render(built, to_joint, out_dir, rest, joints, tier=0):
    """`built`: prop name to the bake's GearMesh; the props of `tier` are placed and rendered."""
    os.makedirs(out_dir, exist_ok=True)
    world = pose(rest, joints)
    verts, tris, cols, norms = [], [], [], []
    hidden = ('breach-', 'grab-', 'watch-648')
    for prop, gm in built.items():
        name, level = prop[4:].rsplit('-', 1)
        if int(level) != tier or name.startswith(hidden):
            continue
        rot, at = world[name]
        base = len(verts)
        for p in gm.positions:
            q = rot @ Vector(p) + at
            verts.append((q.x, q.z, q.y))
        for c in gm.colors:
            cols.append(c)
        for nn in gm.normals:
            q = rot @ Vector(nn)
            norms.append((q.x, q.z, q.y))
        for sub in gm.submeshes:
            for i in range(0, len(sub), 3):
                t = sub[i:i + 3]
                tris.append((base + t[0], base + t[2], base + t[1]))
    me = bpy.data.meshes.new('sheet')
    me.from_pydata(verts, [], tris)
    attr = me.color_attributes.new('Col', 'BYTE_COLOR', 'POINT')
    for i, c in enumerate(cols):
        attr.data[i].color_srgb = (c[0], c[1], c[2], 1.0)
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    me.normals_split_custom_set_from_vertices(norms)
    ob = bpy.data.objects.new('sheet', me)
    bpy.context.scene.collection.objects.link(ob)
    print('sheet: %d triangles' % len(tris))
    scene = bpy.context.scene
    for o in list(scene.collection.objects):
        if o is not ob and o.type == 'MESH':
            o.hide_render = True
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'VERTEX'
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = 'WORLD'
    scene.render.resolution_x, scene.render.resolution_y = 960, 640
    cam = bpy.data.objects.new('sheet-cam', bpy.data.cameras.new('sheet-cam'))
    scene.collection.objects.link(cam)
    cam.data.lens = 50
    scene.camera = cam
    centre = Vector((0, .6, 3.2))
    views = {'tq-front': (35, 12, 21), 'side': (90, 6, 20), 'nose': (0, 8, 21), 'tq-rear': (150, 18, 21), 'above': (55, 45, 22)}
    for label, (yaw, pitch, dist) in views.items():
        a, e = math.radians(yaw), math.radians(pitch)
        d = Vector((math.sin(a) * math.cos(e), math.cos(a) * math.cos(e), math.sin(e)))
        cam.location = centre + d * dist
        cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out_dir, 'boss_%s.png' % label)
        bpy.ops.render.render(write_still=True)
    # Close-ups: the nose, a leg, the flank.
    closes = {'close-nose': (Vector((0, 5.0, 2.0)), Vector((.2, 1, .1)), 6.5), 'close-flank': (Vector((1.5, -1.0, 3.2)), Vector((1, .1, .1)), 5.5), 'close-leg': (Vector((4.0, .7, 2.0)), Vector((1, .3, .1)), 5.0), 'close-rear': (Vector((0, -4.3, 2.8)), Vector((.2, -1, .15)), 5.0)}
    for label, (at, d, dist) in closes.items():
        d = d.normalized()
        cam.location = at + d * dist
        cam.rotation_euler = (at - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out_dir, 'boss_%s.png' % label)
        bpy.ops.render.render(write_still=True)
