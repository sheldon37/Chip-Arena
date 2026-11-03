"""The gear kit: modelling helpers, the dirt and wear bake and the bank writer for gear made in Blender (docs/ZOMBIE-MODELS.md
"Gear"). Run inside Blender 5.x (bpy, bmesh, mathutils); the bank format is tools/pack_bank/bank.py's (Core/NativePackBank.cs).

A gear prop is modelled in a design frame (Blender's: X right, Y forward, Z up, metres, the origin at the bone the prop rides)
and written in that bone's own frame (Unity's axes), one mesh per prop with the bone's name as its group:
- every vertex coloured (sRGB bytes): the finish's colour with its dirt and wear baked in (creases and undersides darker, soft
  grime patches, paint chipped back to the primer or metal on its convex edges, bright worn edges on bare metal);
- the vertex alpha is the outline (Runtime: the toon's inverted hull where it is .5 or more): off on the small parts
  (screws, lights, decals), whose lines would only blot them;
- the glowing finishes (a lens, the vents) are their own sub-mesh, on a material with an emission colour (Runtime: drawn
  glowing).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'pack_bank'))
import bank as bankfmt  # noqa: E402

RESOURCES = 'ChipArenaLocal/Resources'


# ---------------------------------------------------------------------------------------------------------------------------
# Finishes
# ---------------------------------------------------------------------------------------------------------------------------
class Finish:
    """kind: paint (chips to `under` on convex edges), metal (bright worn edges), rubber, cloth, plastic, sticker (thin, clean),
    glow (as it is, its own sub-mesh). outline: drawn with the toon's line."""

    def __init__(self, name, hexcol, kind='paint', under=0x8A8780, outline=True, dirt=1.0):
        self.name, self.colour, self.kind, self.under, self.outline, self.dirt = name, srgb(hexcol), kind, srgb(under), outline, dirt
        self.glow = kind == 'glow'


FINISHES = {}


def srgb(h):
    return ((h >> 16 & 255) / 255.0, (h >> 8 & 255) / 255.0, (h & 255) / 255.0)


def finish(name, hexcol, kind='paint', under=0x8A8780, outline=True, dirt=1.0):
    FINISHES[name] = Finish(name, hexcol, kind, under, outline, dirt)
    if name not in bpy.data.materials:
        bpy.data.materials.new(name)
    return name


# ---------------------------------------------------------------------------------------------------------------------------
# Building: bmesh pieces, each face tagged with its finish (the object's material slots)
# ---------------------------------------------------------------------------------------------------------------------------
class Piece:
    """One object's bmesh, its finishes (material slots) and the modifiers it gets."""

    def __init__(self, name):
        self.name, self.bm, self.slots, self.mods, self.sharp = name, bmesh.new(), [], [], 70.0

    def slot(self, fin):
        if fin not in self.slots:
            self.slots.append(fin)
        return self.slots.index(fin)

    def tag(self, faces, fin):
        i = self.slot(fin)
        for f in faces:
            f.material_index = i
        return faces

    def bevel(self, width, segments=2, angle=35.0):
        self.mods.append(('BEVEL', dict(width=width, segments=segments, limit_method='ANGLE', angle_limit=math.radians(angle))))
        return self

    def solidify(self, thickness, offset=1.0):
        self.mods.append(('SOLIDIFY', dict(thickness=thickness, offset=offset, use_even_offset=True, use_quality_normals=True)))
        return self

    def subsurf(self, levels=1):
        self.mods.append(('SUBSURF', dict(levels=levels, render_levels=levels)))
        return self

    def make(self, recalc=True):
        if recalc:
            bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in self.bm.faces if f.material_index >= 0 and not f.tag])
        for f in self.bm.faces:
            f.smooth = True
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        me.set_sharp_from_angle(angle=math.radians(self.sharp))
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        for fin in self.slots:
            ob.data.materials.append(bpy.data.materials[fin])
        for kind, kw in self.mods:
            m = ob.modifiers.new(kind.lower(), kind)
            for k, v in kw.items():
                setattr(m, k, v)
        return ob


def _faces_of(verts):
    out = set()
    for v in verts:
        out.update(v.link_faces)
    return list(out)


def _place(bm, verts, m):
    bmesh.ops.transform(bm, matrix=m, verts=verts)


def frame_to(p0, p1):
    """A matrix taking +Z to the direction p0 -> p1, its origin at the midpoint."""
    d = Vector(p1) - Vector(p0)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    return Matrix.Translation((Vector(p0) + Vector(p1)) * .5) @ q.to_matrix().to_4x4(), d.length


def box(pc, centre, size, fin, rot=None):
    r = bmesh.ops.create_cube(pc.bm, size=1.0)
    m = Matrix.Translation(Vector(centre)) @ (rot.to_4x4() if rot is not None else Matrix()) @ Matrix.Diagonal((*size, 1))
    _place(pc.bm, r['verts'], m)
    return pc.tag(_faces_of(r['verts']), fin)


def cyl(pc, p0, p1, r0, fin, segs=16, r1=None, caps=True):
    m, length = frame_to(p0, p1)
    r = bmesh.ops.create_cone(pc.bm, cap_ends=caps, cap_tris=False, segments=segs, radius1=r0, radius2=r0 if r1 is None else r1, depth=length)
    _place(pc.bm, r['verts'], m)
    return pc.tag(_faces_of(r['verts']), fin)


def sphere(pc, centre, radii, fin, u=16, v=10):
    r = bmesh.ops.create_uvsphere(pc.bm, u_segments=u, v_segments=v, radius=1.0)
    _place(pc.bm, r['verts'], Matrix.Translation(Vector(centre)) @ Matrix.Diagonal((*radii, 1)))
    return pc.tag(_faces_of(r['verts']), fin)


def torus(pc, centre, axis, major, minor, fin, segs=24, rsegs=8):
    """A ring about `axis` through `centre`."""
    q = Vector((0, 0, 1)).rotation_difference(Vector(axis).normalized()).to_matrix()
    pts = [Vector(centre) + q @ Vector((major * math.cos(a), major * math.sin(a), 0)) for a in [2 * math.pi * i / segs for i in range(segs)]]
    return tube(pc, pts, minor, fin, segs=rsegs, closed=True)


def catmull(pts, per=6, closed=False):
    """A Catmull-Rom curve through the points, `per` samples a span."""
    P = [Vector(p) for p in pts]
    n = len(P)
    out = []
    spans = n if closed else n - 1
    for i in range(spans):
        p0, p1 = P[(i - 1) % n] if closed or i > 0 else P[0], P[i]
        p2 = P[(i + 1) % n]
        p3 = P[(i + 2) % n] if closed or i + 2 < n else P[-1]
        for k in range(per):
            t = k / per
            t2, t3 = t * t, t * t * t
            out.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    if not closed:
        out.append(P[-1])
    return out


def _frames(pts, closed=False, up=None):
    n = len(pts)
    T = []
    for i in range(n):
        a = pts[(i - 1) % n] if closed or i > 0 else pts[i]
        b = pts[(i + 1) % n] if closed or i < n - 1 else pts[i]
        T.append((b - a).normalized())
    ref = Vector(up) if up is not None else (Vector((0, 0, 1)) if abs(T[0].z) < .9 else Vector((1, 0, 0)))
    N = [T[0].cross(ref).cross(T[0]).normalized()]
    for i in range(1, n):
        axis = T[i - 1].cross(T[i])
        if axis.length < 1e-9:
            N.append(N[-1].copy())
        else:
            N.append((Matrix.Rotation(T[i - 1].angle(T[i]), 3, axis.normalized()) @ N[-1]).normalized())
    return T, N


def tube(pc, pts, radius, fin, segs=10, closed=False, caps=True, radii=None):
    """A round tube along the points (rotation-minimizing frames); `radii` per point overrides `radius`."""
    pts = [Vector(p) for p in pts]
    T, N = _frames(pts, closed)
    rings = []
    for i, p in enumerate(pts):
        r = radii[i] if radii else radius
        B = T[i].cross(N[i])
        rings.append([pc.bm.verts.new(p + r * (math.cos(a) * N[i] + math.sin(a) * B)) for a in [2 * math.pi * k / segs for k in range(segs)]])
    faces = []
    count = len(rings) if closed else len(rings) - 1
    for i in range(count):
        a, b = rings[i], rings[(i + 1) % len(rings)]
        for k in range(segs):
            faces.append(pc.bm.faces.new((a[k], b[k], b[(k + 1) % segs], a[(k + 1) % segs])))
    if caps and not closed:
        faces.append(pc.bm.faces.new(list(reversed(rings[0]))))
        faces.append(pc.bm.faces.new(rings[-1]))
    return pc.tag(faces, fin)


def ribbon(pc, pts, normals, width, thick, fin, widths=None):
    """A flat strap along the points: `width` across, `thick` out along each point's normal (its inner face on the path)."""
    pts = [Vector(p) for p in pts]
    n = len(pts)
    T = [((pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)])).normalized() for i in range(n)]
    rings = []
    for i in range(n):
        nn = Vector(normals[i])
        nn = (nn - T[i] * nn.dot(T[i])).normalized()
        s = T[i].cross(nn).normalized()
        w = (widths[i] if widths else width) * .5
        rings.append([pc.bm.verts.new(pts[i] + s * a + nn * b) for a, b in ((-w, 0), (w, 0), (w, thick), (-w, thick))])
    faces = []
    for i in range(n - 1):
        a, b = rings[i], rings[i + 1]
        for k in range(4):
            faces.append(pc.bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k])))
    faces.append(pc.bm.faces.new(rings[0]))
    faces.append(pc.bm.faces.new(list(reversed(rings[-1]))))
    return pc.tag(faces, fin)


def decal(pc, centre, u, v, w, h, step, pattern, project=None):
    """A sticker or painted mark: a grid `w` x `h` about `centre` in the plane of `u` (across) and `v` (up), its face outward
    (u x v), each cell given the finish `pattern(a, b)` returns (a, b from -.5 to .5; None leaves it out), every point moved by
    `project` (onto a curved surface). Faces are wound by hand (not recalculated)."""
    centre, u, v = Vector(centre), Vector(u).normalized(), Vector(v).normalized()
    nx, ny = max(1, round(w / step)), max(1, round(h / step))
    grid = {}

    def at(i, j):
        if (i, j) not in grid:
            p = centre + u * (w * (i / nx - .5)) + v * (h * (j / ny - .5))
            grid[i, j] = pc.bm.verts.new(project(p) if project else p)
        return grid[i, j]
    for i in range(nx):
        for j in range(ny):
            fin = pattern((i + .5) / nx - .5, (j + .5) / ny - .5)
            if fin is None:
                continue
            f = pc.bm.faces.new((at(i, j), at(i + 1, j), at(i + 1, j + 1), at(i, j + 1)))
            f.material_index = pc.slot(fin)
            f.tag = True


# ---------------------------------------------------------------------------------------------------------------------------
# The dirt and wear bake, and the export into a bone's frame
# ---------------------------------------------------------------------------------------------------------------------------
def _smooth(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def shade(f, p, n, cav, seed):
    """A vertex's colour (sRGB 0..1): the finish's, darkened in creases (cav > 0), on undersides and in grime patches, its
    convex edges (cav < 0) chipped or worn bright, by the design-space point `p`."""
    c = Vector(f.colour)
    if f.glow:
        return c
    q = Vector(p) * 1.0 + Vector((seed * 3.1, seed * 1.7, seed * 2.3))
    patch = _smooth(.05, .55, noise.noise(q * 14.0) * .7 + noise.noise(q * 41.0) * .3)
    crease = _smooth(.02, .35, cav)
    under = max(0.0, -n.z) * .5 + _smooth(.0, 1.0, -p.z * 4) * 0.0
    d = f.dirt
    if f.kind == 'flesh':
        # Raw flesh: creases deep red, bruised patches, a paler sheen over the swellings.
        c = c.lerp(Vector((.36, .05, .05)), min(.85, crease * 1.3))
        c = c.lerp(Vector((.42, .3, .4)), .35 * patch)
        c = c.lerp(c * 1.18, _smooth(.05, .3, -cav) * .6)
        return Vector((max(0, min(1, c.x)), max(0, min(1, c.y)), max(0, min(1, c.z))))
    if f.kind == 'sticker':
        dark = .25 * crease + .18 * patch
    elif f.kind == 'rubber':
        dark = .35 * crease + .1 * under
        c = c.lerp(Vector((.32, .3, .27)), .22 * patch * d)
    else:
        dark = .42 * crease + .14 * under + .26 * patch
    c = c * (1.0 - min(.75, dark * d))
    edge = _smooth(.06, .4, -cav)
    if f.kind == 'paint':
        chip = edge * _smooth(.15, .55, noise.noise(q * 95.0) * .5 + .5)
        c = c.lerp(Vector(f.under), min(1.0, chip * 1.1))
    elif f.kind == 'metal':
        c = c.lerp(Vector((min(1, f.colour[0] * 1.5 + .12), min(1, f.colour[1] * 1.5 + .12), min(1, f.colour[2] * 1.5 + .12))), edge * .7)
    elif f.kind == 'plastic':
        c = c.lerp(c * 1.25 + Vector((.04, .04, .04)), edge * .4)
    # Soot and grime, a brown-grey: the station's air.
    c = c.lerp(Vector((.2, .18, .15)), .18 * patch * d * (0 if f.kind == 'sticker' else 1))
    return Vector((max(0, min(1, c.x)), max(0, min(1, c.y)), max(0, min(1, c.z))))


def _cavity(me):
    acc = [0.0] * len(me.vertices)
    cnt = [0] * len(me.vertices)
    for e in me.edges:
        a, b = e.vertices
        pa, pb = me.vertices[a].co, me.vertices[b].co
        d = pb - pa
        if d.length < 1e-9:
            continue
        d.normalize()
        acc[a] += d.dot(me.vertices[a].normal)
        acc[b] -= d.dot(me.vertices[b].normal)
        cnt[a] += 1
        cnt[b] += 1
    return [acc[i] / max(1, cnt[i]) for i in range(len(acc))]


class GearMesh:
    """The bank writer's mesh (fbx.Mesh's fields): Unity space, two sub-meshes (solid, glow)."""

    def __init__(self, name):
        self.name = name
        self.positions, self.normals, self.uv0, self.uv1, self.colors = [], [], [], [], []
        self.submeshes = [[], []]

    def bounds(self):
        if not self.positions:
            return (0, 0, 0), (0, 0, 0)
        return tuple(min(p[i] for p in self.positions) for i in range(3)), tuple(max(p[i] for p in self.positions) for i in range(3))


def bake(name, objects, to_bone, seed=0.0):
    """The objects (evaluated, modifiers applied) as one GearMesh in the bone's frame: `to_bone` takes the design frame there
    (a 4x4; its determinant's sign says whether the winding turns over: Blender's right-handed frame into Unity's)."""
    gm = GearMesh(name)
    flip = to_bone.to_3x3().determinant() < 0
    deps = bpy.context.evaluated_depsgraph_get()
    index = {}
    for ob in objects:
        ev = ob.evaluated_get(deps)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        cav = _cavity(me)
        cn = me.corner_normals
        W = ob.matrix_world
        Wn = W.to_3x3().inverted().transposed()
        M = to_bone @ W
        Mn = M.to_3x3().inverted().transposed()
        for tri in me.loop_triangles:
            mat = ob.material_slots[tri.material_index].material if ob.material_slots else None
            f = FINISHES[mat.name] if mat is not None else next(iter(FINISHES.values()))
            ids = []
            for li in tri.loops:
                vi = me.loops[li].vertex_index
                co = me.vertices[vi].co
                pd = W @ co
                nd = (Wn @ cn[li].vector).normalized()
                col = shade(f, pd, nd, cav[vi], seed)
                pu = M @ co
                nu = (Mn @ cn[li].vector).normalized()
                rgba = (round(col.x * 255), round(col.y * 255), round(col.z * 255), 255 if f.outline else 0)
                key = (round(pu.x, 5), round(pu.y, 5), round(pu.z, 5), round(nu.x, 3), round(nu.y, 3), round(nu.z, 3), rgba)
                k = index.get(key)
                if k is None:
                    k = index[key] = len(gm.positions)
                    gm.positions.append((pu.x, pu.y, pu.z))
                    gm.normals.append((nu.x, nu.y, nu.z))
                    gm.uv0.append((0.0, 0.0))
                    gm.colors.append(tuple(c / 255.0 for c in rgba))
                ids.append(k)
            if flip:
                ids = [ids[0], ids[2], ids[1]]
            gm.submeshes[1 if f.glow else 0].extend(ids)
        if os.environ.get('GEAR_PARTS'):
            print('   %-20s %6d triangles' % (ob.name, len(me.loop_triangles)))
        ev.to_mesh_clear()
    if not gm.submeshes[1]:
        gm.submeshes = [gm.submeshes[0]]
    return gm


class GearBank:
    """The gear bank: props by id, each one mesh in its bone's frame, the bone's name its group."""

    def __init__(self):
        self.bank = bankfmt.Bank()
        self.solid = self.bank.material('gear')
        self.glow = self.bank.material('gear-glow', emission_color=(1, 1, 1))
        self.meshes = []

    def add(self, prop, bone, gm):
        mats = [self.solid, self.glow][:len(gm.submeshes)]
        mi = self.bank.mesh(prop, gm, mats)
        self.bank.prop(prop, bone, [(mi, [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0])])
        self.meshes.append((prop, bone, gm))
        tris = sum(len(s) for s in gm.submeshes) // 3
        print('gear %-28s on %-12s %6d triangles %6d vertices' % (prop, bone, tris, len(gm.positions)))

    def write(self, assets, name):
        from build_pack_bank import bytes_meta
        folder = os.path.join(assets, RESOURCES)
        os.makedirs(folder, exist_ok=True)
        path = os.path.join(folder, name + '.bytes')
        data = self.bank.write()
        with open(path, 'wb') as fh:
            fh.write(data)
        meta = path + '.meta'
        if not os.path.exists(meta):
            with open(meta, 'w', newline='\n') as fh:
                fh.write(bytes_meta(RESOURCES + '/' + name + '.bytes'))
        print('wrote', path, len(data), 'bytes')


def clear_scene():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)


# ---------------------------------------------------------------------------------------------------------------------------
# Previews: what the bank carries, rendered with its vertex colours (workbench)
# ---------------------------------------------------------------------------------------------------------------------------
def preview(gm, to_bone, out_dir, name, views=((0, 'front'), (40, 'tq'), (90, 'side'), (180, 'back'), (-90, 'left'))):
    """Renders the baked mesh back in the design frame (the inverse of `to_bone`) from each view (yaw degrees about Z, 0 looks
    at its front from +Y), 480 x 480."""
    inv = to_bone.inverted()
    flip = to_bone.to_3x3().determinant() < 0
    me = bpy.data.meshes.new('preview-' + name)
    verts = [tuple(inv @ Vector(p)) for p in gm.positions]
    tris = []
    for sub in gm.submeshes:
        for i in range(0, len(sub), 3):
            t = sub[i:i + 3]
            tris.append((t[0], t[2], t[1]) if flip else tuple(t))
    me.from_pydata(verts, [], tris)
    attr = me.color_attributes.new('Col', 'BYTE_COLOR', 'POINT')
    for i, c in enumerate(gm.colors):
        attr.data[i].color_srgb = (c[0], c[1], c[2], 1.0)
    ob = bpy.data.objects.new('preview-' + name, me)
    bpy.context.scene.collection.objects.link(ob)
    for o in bpy.context.scene.collection.objects:
        o.hide_render = o is not ob
    lo = Vector([min(v[i] for v in verts) for i in range(3)])
    hi = Vector([max(v[i] for v in verts) for i in range(3)])
    centre, size = (lo + hi) * .5, max((hi - lo).length, .05)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'VERTEX'
    scene.display.shading.show_cavity = False
    scene.render.resolution_x = scene.render.resolution_y = 480
    scene.render.film_transparent = False
    scene.world = scene.world or bpy.data.worlds.new('w')
    cam = bpy.data.objects.get('preview-camera')
    if cam is None:
        cam = bpy.data.objects.new('preview-camera', bpy.data.cameras.new('preview-camera'))
        scene.collection.objects.link(cam)
    cam.data.lens = 60
    scene.camera = cam
    os.makedirs(out_dir, exist_ok=True)
    for yaw, label in views:
        a = math.radians(yaw)
        d = Vector((math.sin(a), math.cos(a), .35)).normalized()
        cam.location = centre + d * size * 2.4
        cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out_dir, '%s_%s.png' % (name, label))
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(ob, do_unlink=True)
