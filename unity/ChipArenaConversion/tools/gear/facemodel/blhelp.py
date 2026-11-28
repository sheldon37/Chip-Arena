"""blhelp: the face gear modelling kit inside Blender (import it in your model.py; docs/PILOT-BODY-HAIR.md section 6).

Frame: Blender's own, metres. The head faces -Y (Blender's Front view looks at the face), up is +Z, the pilot's left/right along X.
(The harness converts to the game's head-bone frame on export.) setup() has already run when your model.py starts: the scene is
empty but for 'REF head' (the pilot's head: reference only, never exported) and the five paint materials.

Export rule: every MESH object whose name starts with a gear prefix ('Helmet: ', 'Half mask: ', 'Visor: ', 'Mask: ', 'Ears: ') is
exported with its modifiers applied (subdivision, bevel, solidify, mirror, boolean... all fine), its face materials giving each
face's paint. Use only the paint materials (mat('SHELL') etc.; a material named 'SHELL.001' counts as SHELL). Shade smooth or
auto-smooth as you like: the exported normals are Blender's corner normals (sharp edges stay sharp). Other objects are ignored.
"""
import bpy, math, os
from mathutils import Vector

HEAD_OBJ = None  # the pilot's head (headobj.py's OBJ, the head bone's frame): set by run_model.py
PAINT_RGB = {'SHELL': (.95, .95, .93), 'METAL': (.17, .19, .22), 'DARK': (.10, .12, .14), 'LIGHT': (.2, .95, 1.), 'SOFT': (.5, .85, .95)}
PAINT_INDEX = {'SHELL': 0, 'METAL': 1, 'DARK': 2, 'LIGHT': 3, 'SOFT': 4}
GEARS = ('Helmet: ', 'Half mask: ', 'Visor: ', 'Mask: ', 'Ears: ')


def u2b(x, y, z):
    """Game head frame (x, y up, z forward) -> Blender."""
    return Vector((-x, -z, y))


def b2u(v):
    """Blender -> game head frame."""
    return (-v[0], v[2], -v[1])


# Head landmarks in Blender coordinates (metres)
LANDMARKS = {
    'crown_top': u2b(0, .212, .0), 'chin_point': u2b(0, -.05, .04), 'nose_tip': u2b(0, .035, .151), 'mouth': u2b(0, .005, .14),
    'eye_R': u2b(.04, .095, .13), 'eye_L': u2b(-.04, .095, .13), 'ear_R': u2b(.108, .061, .014), 'ear_L': u2b(-.108, .061, .014),
    'back_of_head': u2b(0, .12, -.084),
}


def mat(name):
    """The paint material `name` (SHELL, METAL, DARK, LIGHT, SOFT)."""
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.diffuse_color = (*PAINT_RGB[name], 1)
    return m


def load_head():
    vs, fs = [], []
    for line in open(HEAD_OBJ):
        a = line.split()
        if not a:
            continue
        if a[0] == 'v':
            x, y, z = map(float, a[1:4]); vs.append(tuple(u2b(x, y, z)))
        elif a[0] == 'f':
            fs.append(tuple(int(t.split('/')[0]) - 1 for t in a[1:4]))
    me = bpy.data.meshes.new('REF head'); me.from_pydata(vs, [], fs); me.update()
    for p in me.polygons:
        p.flip()
    ob = bpy.data.objects.new('REF head', me); bpy.context.scene.collection.objects.link(ob)
    return ob


def setup():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob)
    for n in PAINT_RGB:
        mat(n)
    load_head()


def head_points():
    """The head's vertices (Blender coordinates), e.g. for clearance checks while modelling."""
    ob = bpy.data.objects['REF head']
    return [ob.matrix_world @ v.co for v in ob.data.vertices]


def export(out_dir):
    import json
    os.makedirs(out_dir, exist_ok=True)
    dg = bpy.context.evaluated_depsgraph_get()
    recs, warn = [], []
    for ob in bpy.data.objects:
        if ob.type != 'MESH' or not ob.name.startswith(GEARS):
            continue
        ev = ob.evaluated_get(dg)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        mw = ob.matrix_world
        nm = mw.to_3x3().inverted_safe().transposed()
        cn = me.corner_normals
        names = [m.name.split('.')[0] if m else 'SHELL' for m in me.materials] or ['SHELL']
        for n in set(names):
            if n not in PAINT_INDEX:
                warn.append('%s: material %s is not a paint (drawn as SHELL)' % (ob.name, n))
        index, V, N, P, T = {}, [], [], [], []
        for lt in me.loop_triangles:
            paint = PAINT_INDEX.get(names[lt.material_index] if lt.material_index < len(names) else 'SHELL', 0)
            tri = []
            for li in lt.loops:
                vi = me.loops[li].vertex_index
                n = (nm @ Vector(cn[li].vector)).normalized()
                key = (vi, round(n.x, 3), round(n.y, 3), round(n.z, 3), paint)
                k = index.get(key)
                if k is None:
                    k = index[key] = len(V)
                    V.append(b2u(mw @ me.vertices[vi].co)); N.append(b2u(n)); P.append(paint)
                tri.append(k)
            # (the frame change mirrors: the winding turns over)
            T.append((tri[0], tri[2], tri[1]))
        ev.to_mesh_clear()
        if len(V) > 65535:
            warn.append('%s: %d vertices (over 65535): split it into more objects' % (ob.name, len(V)))
        recs.append({'name': ob.name, 'bone': 3, 'V': [[round(c, 6) for c in v] for v in V], 'N': [[round(c, 5) for c in v] for v in N], 'P': P, 'T': T})
    json.dump({'records': recs, 'warnings': warn}, open(os.path.join(out_dir, 'gear.json'), 'w'))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, 'model.blend'))
    print('EXPORTED', len(recs), 'records;', '; '.join(warn) if warn else 'no warnings')
