"""A review sheet of THE DEEP END as the bank holds it (tools/gear/deep_end_boss.py --sheet DIR): the skin bent along a leaping arc over a sheet of
water with the rigid parts placed as the game places them, rendered with its vertex colours (workbench), then close views. Blender only; the game's
own views are the local capture tool's."""
import math
import os

import bpy
from mathutils import Matrix, Vector


def _spine_arc(s):
    """A point on a pose's spine (design frame): rising out of the water toward +Y, over, and back in."""
    # The head at the far end: a long sine arc, then the rest of the body under the water.
    L = 26.0
    x = s
    y = 15.0 - x
    z = 6.2 * math.sin(math.pi * min(1.0, max(-.4, x / L))) - .8 + .2 * math.sin(x * .35)
    side = 1.4 * math.sin(x * .24 + .4)
    return Vector((side, y, z))


def _pose(p, n, s, spine):
    """A rest vertex (Unity frame in the mesh's axes taken back to design: x right, y head, z back) on the bent spine."""
    c = spine(s)
    t = (spine(s - .1) - spine(s + .1)).normalized()
    up0 = Vector((0, 0, 1))
    right = t.cross(up0).normalized()
    up = right.cross(t).normalized()
    # Rest: spine along -Y from the mouth: (x, y + s, z) offsets.
    local = Vector((p[0], p[1] + s, p[2]))
    world = c + right * local.x + t * local.y + up * local.z
    nw = (right * n[0] + t * n[1] + up * n[2]).normalized()
    return world, nw


def _mesh(name, gm, xform):
    """A Blender mesh of a baked mesh, each vertex through `xform(design position, design normal, uv)`."""
    me = bpy.data.meshes.new(name)
    verts, tris, cols, norms = [], [], [], []
    for i, p in enumerate(gm.positions):
        d = (p[0], p[2], p[1])
        n = gm.normals[i]
        w, nw = xform(d, (n[0], n[2], n[1]), gm.uv0[i])
        verts.append(tuple(w))
        norms.append(tuple(nw))
        cols.append(gm.colors[i])
    for sub in gm.submeshes:
        for i in range(0, len(sub), 3):
            t = sub[i:i + 3]
            tris.append((t[0], t[2], t[1]))
    me.from_pydata(verts, [], tris)
    attr = me.color_attributes.new('Col', 'BYTE_COLOR', 'POINT')
    for i, c in enumerate(cols):
        attr.data[i].color_srgb = (c[0], c[1], c[2], 1.0)
    me.validate(clean_customdata=False)
    me.update()
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    me.update()
    if len(norms) == len(me.vertices):
        me.normals_split_custom_set_from_vertices(norms)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _plane(z, half, rgb):
    me = bpy.data.meshes.new('water')
    me.from_pydata([(-half, -half, z), (half, -half, z), (half, half, z), (-half, half, z)], [], [(0, 1, 2, 3)])
    attr = me.color_attributes.new('Col', 'BYTE_COLOR', 'POINT')
    for i in range(4):
        attr.data[i].color_srgb = (rgb[0], rgb[1], rgb[2], 1.0)
    ob = bpy.data.objects.new('water', me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def render(built, to_joint, out_dir, tier, env):
    os.makedirs(out_dir, exist_ok=True)
    radius, surf, surf_n = env['radius'], env['surf'], env['surf_n']
    gk = env['gk']
    SEG = env['SEG']
    gk.clear_scene()
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'VERTEX'
    scene.display.shading.show_cavity = False
    scene.display.shading.show_specular_highlight = True
    scene.display.shading.show_shadows = False
    scene.render.resolution_x, scene.render.resolution_y = 1100, 620
    cam = bpy.data.objects.new('sheet-cam', bpy.data.cameras.new('sheet-cam'))
    scene.collection.objects.link(cam)
    cam.data.lens = 40
    scene.camera = cam

    def shoot(label, at, frm, lens=40):
        cam.data.lens = lens
        cam.location = Vector(frm)
        cam.rotation_euler = (Vector(at) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out_dir, 'boss_%s.png' % label)
        bpy.ops.render.render(write_still=True)

    def rigid(name, m, gmkey=None):
        gm = built[gmkey or name, tier]
        def x(d, n, uv):
            w = m @ Vector(d)
            nn = (m.to_3x3() @ Vector(n)).normalized()
            return w, nn
        return _mesh('r-' + name, gm, x)

    # ---- A: straight, for the lettering and the tile (the mesh as it is) ----
    body = built['body', tier]
    def rest(d, n, uv):
        return Vector(d), Vector(n)
    ob = _mesh('body-rest', body, rest)
    objs = [ob]
    # The rigid parts as the game places them at rest.
    from_mouth = Vector((0, 0, -.16))
    def place_parts(base=Matrix()):
        made = []
        made.append(rigid('throat', base @ Matrix.Translation(from_mouth + Vector((0, -.4, 0)))))
        made.append(rigid('grate', base @ Matrix.Translation(from_mouth + Vector((0, -.3, 0)))))
        for k in range(9):
            a = 2 * math.pi * k / 9
            m = base @ Matrix.Translation(from_mouth + Vector((0, -.06 - .012 * k, 0))) @ Matrix.Rotation(a, 4, 'Y') @ Matrix.Scale(.98, 4)
            made.append(rigid('blade', m))
        for side, nm in ((1, 'rail-r'), (-1, 'rail-l')):
            made.append(rigid(nm, base @ Matrix.Translation(surf(.95, side * 1.7))))
        for side, nm in ((1, 'fin-r'), (-1, 'fin-l')):
            made.append(rigid(nm, base @ Matrix.Translation(surf(env['FIN_S'], side * 1.58))))
        for s in env['LAMPS']:
            for side in (1, -1):
                th = side * (env['THETA_B'] + env['THETA_C']) * .5
                n = surf_n(s, th)
                c = surf(s, th) + n * .05
                r = .34 * min(1.0, radius(s) / 1.0) + .04
                q = Vector((0, 1, 0)).rotation_difference(n).to_matrix().to_4x4()
                made.append(rigid('lamp', base @ Matrix.Translation(c) @ q @ Matrix.Scale(r, 4)))
        return made
    objs += place_parts()

    # Close views at rest.
    shoot('head_front', (0, 0, -.1), (2.4, 7.5, 1.6), 45)
    shoot('head_tq', (0, -1.2, 0), (5.2, 4.2, 2.8), 45)
    shoot('crown', (0, -2.2, .9), (0, -7.0, 7.5), 50)
    shoot('crown_front', (0, -2.2, .9), (0, 3.5, 6.5), 50)
    shoot('flank_r', (1, -10.5, .4), (11.5, -10.5, 3.2), 45)
    shoot('flank_l', (-1, -10.5, .4), (-11.5, -10.5, 3.2), 45)
    shoot('flank_tail', (1, -21, .3), (11, -21, 3.2), 45)
    shoot('back', (0, -14, 0), (0, -14, 27), 40)
    shoot('tile_close', (1.0, -8.6, .9), (3.1, -7.5, 2.0), 50)
    shoot('tail', (0, -28.5, 0), (6, -26, 3), 45)
    shoot('fin_tq', (2, -7, 0), (9, -1, 6), 40)
    for o in objs:
        o.hide_render = True

    # ---- B: on a leaping arc over water, the silhouette at game distance ----
    def arc(d, n, uv):
        s = max(0.0, min(env['LENGTH'], -d[1] if uv[0] * SEG == 0 else uv[0] * SEG))
        return _pose(d, n, s, _spine_arc)
    ob = _mesh('body-arc', body, arc)
    # (The parts follow the spine at the head.)
    def head_frame(s):
        c = _spine_arc(s)
        t = (_spine_arc(s - .1) - _spine_arc(s + .1)).normalized()
        right = t.cross(Vector((0, 0, 1))).normalized()
        up = right.cross(t).normalized()
        m = Matrix((right, t, up)).transposed().to_4x4()
        return Matrix.Translation(c) @ m
    parts = []
    hf = head_frame(0.0)
    parts.append(rigid('grate', hf @ Matrix.Translation(from_mouth + Vector((0, -.3, 0)))))
    for k in range(9):
        a = 2 * math.pi * k / 9
        parts.append(rigid('blade', hf @ Matrix.Translation(from_mouth + Vector((0, -.06 - .012 * k, 0))) @ Matrix.Rotation(a, 4, 'Y') @ Matrix.Scale(.98, 4)))
    for side, nm in ((1, 'rail-r'), (-1, 'rail-l')):
        parts.append(rigid(nm, hf @ Matrix.Translation(surf(.95, side * 1.7))))
    for side, nm in ((1, 'fin-r'), (-1, 'fin-l')):
        parts.append(rigid(nm, head_frame(env['FIN_S']) @ Matrix.Translation(surf(env['FIN_S'], side * 1.58) + Vector((0, env['FIN_S'], 0)))))
    for s in env['LAMPS']:
        fr = head_frame(s)
        for side in (1, -1):
            th = side * (env['THETA_B'] + env['THETA_C']) * .5
            n = surf_n(s, th)
            c = surf(s, th) + n * .05 + Vector((0, s, 0))
            r = .34 * min(1.0, radius(s) / 1.0) + .04
            q = Vector((0, 1, 0)).rotation_difference(n).to_matrix().to_4x4()
            parts.append(rigid('lamp', fr @ Matrix.Translation(c) @ q @ Matrix.Scale(r, 4)))
    water = _plane(0.0, 60, (.16, .42, .44))
    shoot('arc_side', (0, 2, 2.2), (34, 2, 4.5), 40)
    shoot('arc_tq', (0, 4, 2.2), (24, -18, 8), 40)
    shoot('arc_low', (0, 10, 4), (8, 30, 1.2), 32)
    shoot('arc_above', (0, 2, 2), (10, -10, 26), 40)
    shoot('arc_game', (0, 6, 3), (14, 22, 2.6), 60)
    print('sheet: rendered to', out_dir)
